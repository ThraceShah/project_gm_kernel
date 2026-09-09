using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Tensor-product B-spline surface evaluation. Mirrors the semantics of
/// <see cref="BCurveEvaluation"/> per parameter direction: PK knot-side
/// selection, periodic wrap, linear extension outside the parameter box and
/// zero-fill of derivative orders above the spline degree. Rational surfaces
/// keep the homogeneous de Boor recurrence in both directions and apply the
/// bivariate quotient rule once at the end.
/// </summary>
internal static class BSurfaceEvaluation
{
    /// <summary>Doubles of command scratch needed to evaluate the given orders.</summary>
    internal static long WorkspaceSize(SplineDegree uDegree, SplineDegree vDegree, DerivativeOrder uOrder, DerivativeOrder vOrder)
    {
        if (uDegree < 1 || vDegree < 1 || uOrder < 0 || vOrder < 0 || uOrder > 10 || vOrder > 10) return 0;
        var du = Math.Min(uOrder, uDegree);
        var dv = Math.Min(vOrder, vDegree);
        // Stage A: one (p+1)-row de Boor work buffer plus the per-column result
        // table; stage B: one (q+1)-row de Boor work buffer; stage C: results.
        return (long)(uDegree + 1) * (du + 1) * 4
            + (long)(vDegree + 1) * (du + 1) * 4
            + (long)(vDegree + 1) * (dv + 1) * 4
            + (long)(du + 1) * (dv + 1) * 4;
    }

    internal static AlgorithmStatus Evaluate(in BSurfaceView surface, double u, double v,
        in SurfaceDerivativeLayout layout, Span<double> workspace, Span<KernelVector3> output)
    {
        if (!double.IsFinite(u) || !double.IsFinite(v)) return AlgorithmStatus.InvalidInput;
        if (output.Length < layout.Count) return AlgorithmStatus.OutputTooSmall;

        // Periodic parameters wrap. Outside the parameter box PK_SURF_eval
        // extends the surface linearly along each out-of-range parameter: the
        // position picks up first-derivative·δ in every in-range direction,
        // the first derivative along an out-of-range direction is the clamped
        // value, and higher derivatives along it vanish (oracle-verified).
        u = WrapPeriodic(u, surface.UStart, surface.UEnd, surface.UPeriodic);
        v = WrapPeriodic(v, surface.VStart, surface.VEnd, surface.VPeriodic);
        var uExtrapolation = u - Math.Clamp(u, surface.UStart, surface.UEnd);
        var vExtrapolation = v - Math.Clamp(v, surface.VStart, surface.VEnd);
        if (uExtrapolation != 0 || vExtrapolation != 0)
            return EvaluateExtended(surface, u, v, uExtrapolation, vExtrapolation, in layout, workspace, output);
        return EvaluateCore(surface, u, v, in layout, workspace, output);
    }

    private static AlgorithmStatus EvaluateExtended(in BSurfaceView surface, double u, double v,
        double uExtrapolation, double vExtrapolation, in SurfaceDerivativeLayout layout,
        Span<double> workspace, Span<KernelVector3> output)
    {
        var uc = Math.Clamp(u, surface.UStart, surface.UEnd);
        var vc = Math.Clamp(v, surface.VStart, surface.VEnd);
        if (!SurfaceDerivativeLayout.TryCreate(
                Math.Max(layout.UOrder, uExtrapolation != 0 ? 1 : 0),
                Math.Max(layout.VOrder, vExtrapolation != 0 ? 1 : 0), out var clampedLayout))
            return AlgorithmStatus.InvalidInput;
        Span<KernelVector3> clamped = stackalloc KernelVector3[clampedLayout.Count <= 144 ? clampedLayout.Count : 144];
        var status = EvaluateCore(surface, uc, vc, in clampedLayout, workspace, clamped);
        if (status != AlgorithmStatus.Success) return status;

        if (uExtrapolation != 0)
        {
            for (DerivativeOrder j = 0; j <= clampedLayout.VOrder; j++)
            {
                var position = Add(clamped[clampedLayout.GetIndex(0, j)], Scale(clamped[clampedLayout.GetIndex(1, j)], uExtrapolation));
                for (DerivativeOrder i = 2; i <= clampedLayout.UOrder; i++) clamped[clampedLayout.GetIndex(i, j)] = default;
                clamped[clampedLayout.GetIndex(0, j)] = position;
            }
        }
        if (vExtrapolation != 0)
        {
            for (DerivativeOrder i = 0; i <= clampedLayout.UOrder; i++)
            {
                var position = Add(clamped[clampedLayout.GetIndex(i, 0)], Scale(clamped[clampedLayout.GetIndex(i, 1)], vExtrapolation));
                for (DerivativeOrder j = 2; j <= clampedLayout.VOrder; j++) clamped[clampedLayout.GetIndex(i, j)] = default;
                clamped[clampedLayout.GetIndex(i, 0)] = position;
            }
        }
        for (DerivativeOrder a = 0; a <= layout.UOrder; a++)
        for (DerivativeOrder b = 0; b <= layout.VOrder; b++)
            output[layout.GetIndex(a, b)] = clamped[clampedLayout.GetIndex(a, b)];
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus EvaluateCore(in BSurfaceView surface, double u, double v,
        in SurfaceDerivativeLayout layout, Span<double> workspace, Span<KernelVector3> output)
    {
        var size = WorkspaceSize(surface.UDegree, surface.VDegree, layout.UOrder, layout.VOrder);
        if (size == 0 || workspace.Length < size) return AlgorithmStatus.WorkspaceTooSmall;

        var p = surface.UDegree;
        var q = surface.VDegree;
        var du = Math.Min(layout.UOrder, p);
        var dv = Math.Min(layout.VOrder, q);
        var spanU = KnotSpan(surface.UKnots, p, surface.UVertexCount, u);
        var spanV = KnotSpan(surface.VKnots, q, surface.VVertexCount, v);

        // Stage A: homogeneous u-direction de Boor with derivative tracking for
        // each v-column of the v-span window. table[j, k, c] = derivative k,
        // component c of column j (the last de Boor row).
        var columnWork = workspace[..(int)((p + 1) * (du + 1) * 4)];
        var table = workspace[(int)((p + 1) * (du + 1) * 4)..(int)((p + 1) * (du + 1) * 4 + (q + 1) * (du + 1) * 4)];
        var vWork = workspace[(int)((p + 1) * (du + 1) * 4 + (q + 1) * (du + 1) * 4)..(int)((p + 1) * (du + 1) * 4 + (q + 1) * (du + 1) * 4 + (q + 1) * (dv + 1) * 4)];
        var results = workspace[(int)((p + 1) * (du + 1) * 4 + (q + 1) * (du + 1) * 4 + (q + 1) * (dv + 1) * 4)..(int)size];
        var strideU = (du + 1) * 4;
        for (ControlPointIndex j = 0; j <= q; j++)
        {
            columnWork.Clear();
            for (ControlPointIndex i = 0; i <= p; i++)
            {
                var pole = ((spanU - p + i) * surface.VVertexCount + (spanV - q + j)) * surface.Dimension;
                columnWork[i * strideU] = surface.Vertices[pole];
                columnWork[i * strideU + 1] = surface.Vertices[pole + 1];
                columnWork[i * strideU + 2] = surface.Vertices[pole + 2];
                columnWork[i * strideU + 3] = surface.Rational ? surface.Vertices[pole + 3] : 1;
            }
            DeBoorWithDerivatives(columnWork, surface.UKnots, spanU, p, u, du);
            columnWork[(p * strideU)..((p + 1) * strideU)].CopyTo(table[(j * strideU)..((j + 1) * strideU)]);
        }

        // Stage B: v-direction de Boor over the q+1 column values, per u order.
        // Only the position slot is seeded; the recurrence builds the v
        // derivatives through the zero-initialised higher slots.
        var rowStride = (dv + 1) * 4;
        var row = vWork[..((q + 1) * rowStride)];
        for (DerivativeOrder i = 0; i <= du; i++)
        {
            row.Clear();
            for (ControlPointIndex j = 0; j <= q; j++)
            for (BufferOffset c = 0; c < 4; c++)
                row[j * rowStride + c] = table[j * strideU + i * 4 + c];
            DeBoorWithDerivatives(row, surface.VKnots, spanV, q, v, dv);
            var target = results[(i * (dv + 1) * 4)..];
            for (BufferOffset offset = 0; offset < (dv + 1) * 4; offset++)
                target[offset] = row[q * rowStride + offset];
        }

        // Stage C: project to 3D with the bivariate quotient rule in
        // lexicographic order (each correction term is already projected).
        var resultStride = dv + 1;
        Span<KernelVector3> projected = stackalloc KernelVector3[(du + 1) * (dv + 1)];
        for (DerivativeOrder a = 0; a <= layout.UOrder; a++)
        for (DerivativeOrder b = 0; b <= layout.VOrder; b++)
        {
            KernelVector3 value;
            if (a > du || b > dv)
                value = default;    // PK V38 zero-fills orders above the spline degree
            else
            {
                if (!surface.Rational)
                    value = Vector(results[(a * resultStride + b) * 4], results[(a * resultStride + b) * 4 + 1], results[(a * resultStride + b) * 4 + 2]);
                else
                    value = QuotientRule(projected, results, a, b, resultStride);
                projected[a * resultStride + b] = value;
            }
            if (!IsFinite(value)) return AlgorithmStatus.NumericalFailure;
            output[layout.GetIndex(a, b)] = value;
        }
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Leibniz recursion on S·w = A: S^(a,b) = (A^(a,b) − Σ binom·w^(p,q)·S^(a−p,b−q)) / w,
    /// where `projected` holds S for every (i, j) lexicographically below (a, b).
    /// </summary>
    private static KernelVector3 QuotientRule(Span<KernelVector3> projected, ReadOnlySpan<double> raw,
        DerivativeOrder a, DerivativeOrder b, BufferCount resultStride)
    {
        var index = a * resultStride + b;
        var value = Vector(raw[index * 4], raw[index * 4 + 1], raw[index * 4 + 2]);
        for (DerivativeOrder p = 0; p <= a; p++)
        for (DerivativeOrder q = 0; q <= b; q++)
        {
            if (p == 0 && q == 0) continue;
            var weight = raw[(p * resultStride + q) * 4 + 3];
            if (weight == 0) continue;
            value = Add(value, Scale(projected[(a - p) * resultStride + (b - q)], -Binomial(a, p) * Binomial(b, q) * weight));
        }
        return Scale(value, 1 / raw[3]);
    }

    /// <summary>In-place de Boor interpolation with simultaneous derivative tracking (4-wide homogeneous slots).</summary>
    private static void DeBoorWithDerivatives(Span<double> work, ReadOnlySpan<double> knots,
        KnotIndex span, SplineDegree degree, double t, DerivativeOrder derivatives)
    {
        var stride = (derivatives + 1) * 4;
        for (SplineDegree r = 1; r <= degree; r++)
        for (ControlPointIndex j = degree; j >= r; j--)
        {
            var index = span - degree + j;
            var denominator = knots[index + degree - r + 1] - knots[index];
            var alpha = (t - knots[index]) / denominator;
            for (DerivativeOrder k = Math.Min(r, derivatives); k >= 0; k--)
            for (var c = 0; c < 4; c++)
            {
                var target = j * stride + k * 4 + c;
                var left = work[target - stride];
                var value = left + alpha * (work[target] - left);
                if (k > 0) value += k * (work[target - 4] - work[target - stride - 4]) / denominator;
                work[target] = value;
            }
        }
    }

    internal static double Binomial(int n, int k)
    {
        double result = 1;
        for (var i = 1; i <= k; i++) result = result * (n - i + 1) / i;
        return result;
    }

    private static KnotIndex KnotSpan(ReadOnlySpan<double> knots, SplineDegree degree, BufferCount vertexCount, double t)
    {
        KnotIndex low = degree, high = vertexCount;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (knots[middle] <= t || knots[middle] - t <= BCurveEvaluation.KnotSideResolution) low = middle; else high = middle - 1;
        }
        return Math.Min(low, vertexCount - 1);
    }

    private static double WrapPeriodic(double t, double start, double end, bool periodic)
    {
        if (!periodic || (t >= start && t <= end)) return t;
        var period = end - start;
        t = (t - start) % period;
        if (t < 0) t += period;
        return t + start;
    }

    /// <summary>
}
