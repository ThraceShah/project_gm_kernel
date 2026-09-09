using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class BCurveEvaluation
{
    // PK's default knot-side selection uses angular/parameter resolution, without snapping t itself.
    internal const double KnotSideResolution = 1e-11;
    internal static BufferCount WorkspaceSize(SplineDegree degree, DerivativeOrder order)
    {
        if (degree < 1 || order < 0 || order > 10) return 0;
        var count = ((long)degree + 1) * (Math.Max(1, Math.Min(degree, order)) + 1) * 4;
        return count > int.MaxValue ? 0 : (BufferCount)count;
    }

    internal static AlgorithmStatus Evaluate(in BCurveView curve, double parameter, DerivativeOrder order,
        Span<KernelVector3> output, Span<double> workspace, out KernelVector3 tangent)
    {
        tangent = default;
        if (!double.IsFinite(parameter) || order < 0 || order > 10) return AlgorithmStatus.InvalidInput;
        if (output.Length <= order) return AlgorithmStatus.OutputTooSmall;
        var size = WorkspaceSize(curve.Degree, order);
        if (size == 0 || workspace.Length < size) return AlgorithmStatus.WorkspaceTooSmall;
        workspace = workspace[..size];
        workspace.Clear();
        var t = parameter;
        if (curve.Periodic && (t < curve.Start || t > curve.End))
        {
            var period = curve.End - curve.Start;
            t = (t - curve.Start) % period;
            if (t < 0) t += period;
            t += curve.Start;
        }
        var extrapolation = curve.Periodic ? 0 : t - Math.Clamp(t, curve.Start, curve.End);
        t = Math.Clamp(t, curve.Start, curve.End);
        var p = curve.Degree;
        var derivatives = Math.Max(1, Math.Min(order, p));
        var stride = (derivatives + 1) * 4;
        var knots = curve.Knots;
        KnotIndex low = p, high = curve.VertexCount;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (knots[middle] <= t || knots[middle] - t <= KnotSideResolution) low = middle; else high = middle - 1;
        }
        var span = Math.Min(low, curve.VertexCount - 1);
        for (ControlPointIndex j = 0; j <= p; j++)
        {
            var pole = (span - p + j) * curve.Dimension;
            workspace[j * stride] = curve.Vertices[pole];
            workspace[j * stride + 1] = curve.Vertices[pole + 1];
            workspace[j * stride + 2] = curve.Vertices[pole + 2];
            workspace[j * stride + 3] = curve.Rational ? curve.Vertices[pole + 3] : 1;
        }
        // Differentiate each de Boor interpolation. Descending rows/orders preserve its inputs.
        for (SplineDegree r = 1; r <= p; r++)
            for (ControlPointIndex j = p; j >= r; j--)
            {
                var index = span - p + j;
                var denominator = knots[index + p - r + 1] - knots[index];
                if (!(denominator > 0) || !double.IsFinite(denominator)) return AlgorithmStatus.NumericalFailure;
                var alpha = (t - knots[index]) / denominator;
                for (DerivativeOrder k = Math.Min(r, derivatives); k >= 0; k--)
                    for (var c = 0; c < 4; c++)
                    {
                        var target = j * stride + k * 4 + c;
                        var left = workspace[target - stride];
                        var value = left + alpha * (workspace[target] - left);
                        if (k > 0) value += k * (workspace[target - 4] - workspace[target - stride - 4]) / denominator;
                        workspace[target] = value;
                    }
            }
        var result = workspace.Slice(p * stride, stride);
        var w = curve.Rational ? result[3] : 1;
        if (!(w > 0) || !double.IsFinite(w)) return AlgorithmStatus.NumericalFailure;
        for (DerivativeOrder k = 0; k <= order; k++)
        {
            var value = k <= derivatives ? Vector(result[k * 4], result[k * 4 + 1], result[k * 4 + 2]) : default;
            if (curve.Rational)
            {
                double binomial = 1;
                for (DerivativeOrder j = 1; j <= Math.Min(k, derivatives); j++)
                {
                    binomial *= (double)(k - j + 1) / j;
                    value = Add(value, Scale(output[k - j], -binomial * result[j * 4 + 3]));
                }
                value = Scale(value, 1 / w);
            }
            if (!IsFinite(value)) return AlgorithmStatus.NumericalFailure;
            output[k] = value;
        }
        var first = Vector(result[4], result[5], result[6]);
        if (curve.Rational) first = Scale(Add(first, Scale(output[0], -result[7])), 1 / w);
        tangent = Unit(first);
        // PK extends non-periodic B-curves linearly using their endpoint derivative.
        if (extrapolation != 0)
        {
            output[0] = Add(output[0], Scale(first, extrapolation));
            if (order >= 1) output[1] = first;
            if (order >= 2) output.Slice(2, order - 1).Clear();
            if (!IsFinite(output[0])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }
}
