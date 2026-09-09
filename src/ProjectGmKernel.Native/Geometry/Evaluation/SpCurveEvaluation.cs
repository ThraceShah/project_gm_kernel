using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// SP-curve: a 3D curve produced by embedding a 2D B-curve in the parameter
/// space of a surface (XT 5.2.1.8). The 2D B-curve carries (u, v) (vertex
/// dimension 2, plain) or (u, v, w) (vertex dimension 3, rational). Its
/// evaluation mirrors <see cref="BCurveEvaluation"/> (PK knot-side selection,
/// periodic wrap, linear extension); the surface is then evaluated as a jet
/// and the t-derivatives follow from the multivariate chain rule:
/// S(u(t), v(t)) = Σ J[a][b]/(a!b!)·Δu^a·Δv^b with Δu = Σ u^(k)·t^k/k!, whose
/// n-th t-derivative is assembled from the truncated series products of the
/// normalized coefficient sequences.
/// </summary>
internal static class SpCurveEvaluation
{
    /// <summary>Doubles of command scratch for the 2D curve de Boor stage.</summary>
    internal static long CurveWorkspaceSize(SplineDegree degree, DerivativeOrder order)
    {
        if (degree < 1 || order < 0 || order > 10) return 0;
        // The extension path always tracks the first derivative.
        return ((long)degree + 1) * (Math.Min(degree, Math.Max(order, 1)) + 1) * 3;
    }

    /// <summary>
    /// Evaluates the 2D parameter curve: uvDerivs[k] = (u^(k), v^(k)) for k ≤ order.
    /// </summary>
    internal static AlgorithmStatus EvaluateUV(in BCurveView curve, double t, DerivativeOrder order,
        Span<double> workspace, Span<KernelVector3> uvDerivs)
    {
        if (!double.IsFinite(t) || order < 0 || order > 10) return AlgorithmStatus.InvalidInput;
        if (uvDerivs.Length <= order) return AlgorithmStatus.OutputTooSmall;
        var size = CurveWorkspaceSize(curve.Degree, order);
        if (size == 0 || workspace.Length < size) return AlgorithmStatus.WorkspaceTooSmall;

        if (curve.Periodic && (t < curve.Start || t > curve.End))
        {
            var period = curve.End - curve.Start;
            t = (t - curve.Start) % period;
            if (t < 0) t += period;
            t += curve.Start;
        }
        var extrapolation = curve.Periodic ? 0 : t - Math.Clamp(t, curve.Start, curve.End);
        t = Math.Clamp(t, curve.Start, curve.End);
        // The linear extension needs the first derivative even for order 0.
        var derivatives = Math.Min(Math.Max(order, extrapolation != 0 ? 1 : 0), curve.Degree);
        var p = curve.Degree;
        var stride = (derivatives + 1) * 3;
        var knots = curve.Knots;
        KnotIndex low = p, high = curve.VertexCount;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (knots[middle] <= t || knots[middle] - t <= BCurveEvaluation.KnotSideResolution) low = middle; else high = middle - 1;
        }
        var span = Math.Min(low, curve.VertexCount - 1);
        var work = workspace[..(stride * (p + 1))];
        work.Clear();
        for (ControlPointIndex j = 0; j <= p; j++)
        {
            var pole = (span - p + j) * curve.Dimension;
            work[j * stride] = curve.Vertices[pole];
            work[j * stride + 1] = curve.Vertices[pole + 1];
            work[j * stride + 2] = curve.Rational ? curve.Vertices[pole + 2] : 1;
        }
        for (SplineDegree r = 1; r <= p; r++)
        for (ControlPointIndex j = p; j >= r; j--)
        {
            var index = span - p + j;
            var denominator = knots[index + p - r + 1] - knots[index];
            if (!(denominator > 0) || !double.IsFinite(denominator)) return AlgorithmStatus.NumericalFailure;
            var alpha = (t - knots[index]) / denominator;
            for (DerivativeOrder k = Math.Min(r, derivatives); k >= 0; k--)
            for (var c = 0; c < 3; c++)
            {
                var target = j * stride + k * 3 + c;
                var left = work[target - stride];
                var value = left + alpha * (work[target] - left);
                if (k > 0) value += k * (work[target - 3] - work[target - stride - 3]) / denominator;
                work[target] = value;
            }
        }

        // Project the homogeneous result row (u, v, w) with the quotient rule.
        var result = work[(p * stride)..];
        var w = curve.Rational ? result[2] : 1;
        if (!(w > 0) || !double.IsFinite(w)) return AlgorithmStatus.NumericalFailure;
        var projectedOrders = Math.Max(order, extrapolation != 0 ? 1 : 0);
        for (DerivativeOrder k = 0; k <= projectedOrders; k++)
        {
            double u, v;
            if (k > derivatives)
            {
                u = v = 0;
            }
            else
            {
                u = result[k * 3];
                v = result[k * 3 + 1];
                if (curve.Rational)
                {
                    double binomial = 1;
                    for (DerivativeOrder j = 1; j <= k; j++)
                    {
                        binomial *= (double)(k - j + 1) / j;
                        var previous = uvDerivs[k - j];
                        u -= binomial * result[j * 3 + 2] * previous.X;
                        v -= binomial * result[j * 3 + 2] * previous.Y;
                    }
                    u /= w;
                    v /= w;
                }
            }
            if (!double.IsFinite(u) || !double.IsFinite(v)) return AlgorithmStatus.NumericalFailure;
            uvDerivs[k] = Vector(u, v, 0);
        }
        if (extrapolation != 0)
        {
            uvDerivs[0] = Vector(uvDerivs[0].X + uvDerivs[1].X * extrapolation,
                uvDerivs[0].Y + uvDerivs[1].Y * extrapolation, 0);
            for (DerivativeOrder k = 2; k <= order; k++) uvDerivs[k] = default;
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Composes the surface jet J[a][b] (rectangular layout, both orders ≥ the
    /// requested curve order) with the scalar parameter curves u(t), v(t):
    /// output[n] = d^n/dt^n S(u(t), v(t)) for n ≤ order.
    /// </summary>
    internal static void Compose(in SurfaceDerivativeLayout jetLayout, ReadOnlySpan<KernelVector3> jet,
        ReadOnlySpan<KernelVector3> uvDerivs, DerivativeOrder order, Span<KernelVector3> output)
    {
        output[0] = jet[0];
        var n1 = order + 1;
        Span<double> au = stackalloc double[11];
        Span<double> av = stackalloc double[11];
        for (DerivativeOrder k = 1; k <= order; k++)
        {
            au[k] = uvDerivs[k].X / Factorial(k);
            av[k] = uvDerivs[k].Y / Factorial(k);
        }
        // c[a][b][n] = [s^n] Δu^a Δv^b, packed at (a * n1 + b) * n1 + n.
        Span<double> c = stackalloc double[11 * 11 * 11];
        Span<double> destination = stackalloc double[11];
        c.Clear();
        c[0] = 1;                                     // a = b = 0: δ(n = 0)
        for (var a = 0; a <= order; a++)
        for (var b = a == 0 ? 1 : 0; a + b <= order; b++)
        {
            var target = c[((a * n1 + b) * n1)..];
            target.Clear();
            if (a > 0)
            {
                var source = c[(((a - 1) * n1 + b) * n1)..];
                Convolve(au, source, target, order, n1);
            }
            if (b > 0)
            {
                var source = c[((a * n1 + b - 1) * n1)..];
                for (var n = 0; n <= order; n++)
                {
                    double sum = 0;
                    for (var k = 1; k <= n; k++) sum += av[k] * source[n - k];
                    destination[n] = target[n] + sum;
                }
                destination[..n1].CopyTo(target);
            }
        }
        for (DerivativeOrder n = 1; n <= order; n++)
        {
            var factorial = Factorial(n);
            var value = default(KernelVector3);
            for (var a = 0; a <= n; a++)
            for (var b = 0; a + b <= n; b++)
            {
                var series = c[((a * n1 + b) * n1) + n];
                if (series == 0) continue;
                var weight = factorial / (Factorial(a) * Factorial(b)) * series;
                value = Add(value, Scale(jet[jetLayout.GetIndex(a, b)], weight));
            }
            output[n] = value;
        }
    }

    /// <summary>destination[n] = Σ_{k=1..n} coefficients[k]·source[n−k].</summary>
    private static void Convolve(ReadOnlySpan<double> coefficients, ReadOnlySpan<double> source,
        Span<double> destination, DerivativeOrder order, int n1)
    {
        for (var n = 0; n <= order; n++)
        {
            double sum = 0;
            for (var k = 1; k <= n; k++) sum += coefficients[k] * source[n - k];
            destination[n] = sum;
        }
    }

    internal static double Factorial(int n)
    {
        double result = 1;
        for (var i = 2; i <= n; i++) result *= i;
        return result;
    }
}
