using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Bounds;

internal readonly struct SurfaceParameterBox(double u0, double u1, double v0, double v1)
{
    internal readonly double U0 = u0, U1 = u1, V0 = v0, V1 = v1;
    internal bool Valid => double.IsFinite(U0) && double.IsFinite(U1) && double.IsFinite(V0) && double.IsFinite(V1) && U1 >= U0 && V1 >= V0;
}

/// <summary>One homogeneous Bezier span. Interval poles enclose extraction rounding.</summary>
internal readonly ref struct BezierSurfacePatch(ReadOnlySpan<IntervalNumber> poles, SplineDegree uDegree,
    SplineDegree vDegree, SurfaceParameterBox domain)
{
    internal ReadOnlySpan<IntervalNumber> Poles { get; } = poles;
    internal SplineDegree UDegree { get; } = uDegree;
    internal SplineDegree VDegree { get; } = vDegree;
    internal SurfaceParameterBox Domain { get; } = domain;
}

internal static class BezierSurfaceBounds
{
    internal const SplineDegree MaxDegree = 16;
    internal const BufferCount MaxScalars = 4 * 17 * 17;

    // Symbolic de Boor with Bernstein-polynomial coefficients. No interpolation
    // or sampled bounds: every coefficient operation encloses the input spline.
    internal static AlgorithmStatus Extract(in BSurfaceView surface, KnotIndex uSpan, KnotIndex vSpan,
        Span<IntervalNumber> poles)
    {
        var p = surface.UDegree; var q = surface.VDegree;
        if (p < 1 || q < 1 || p > MaxDegree || q > MaxDegree || surface.Dimension != (surface.Rational ? 4 : 3)
            || uSpan < p || uSpan >= surface.UVertexCount || vSpan < q || vSpan >= surface.VVertexCount)
            return AlgorithmStatus.Unsupported;
        if (poles.Length < 4 * (p + 1) * (q + 1)) return AlgorithmStatus.WorkspaceTooSmall;
        Span<IntervalNumber> layers = stackalloc IntervalNumber[MaxScalars];
        Span<IntervalNumber> input = stackalloc IntervalNumber[4 * 17];
        Span<IntervalNumber> result = stackalloc IntervalNumber[4 * 17];
        var maxWeight = 1.0;
        if (surface.Rational)
        {
            maxWeight = 0;
            for (var u = uSpan - p; u <= uSpan; u++) for (var v = vSpan - q; v <= vSpan; v++)
                maxWeight = Math.Max(maxWeight, surface.Vertices[(u * surface.VVertexCount + v) * 4 + 3]);
        }
        if (!(maxWeight > 0) || !double.IsFinite(maxWeight)) return AlgorithmStatus.InvalidInput;
        var exponent = Math.ILogB(maxWeight);
        for (var v = 0; v <= q; v++)
        {
            for (var u = 0; u <= p; u++)
            {
                var at = ((uSpan - p + u) * surface.VVertexCount + vSpan - q + v) * surface.Dimension;
                for (var d = 0; d < 4; d++)
                {
                    var value = d == 3 && !surface.Rational ? 1 : surface.Vertices[at + d];
                    var scaled = Math.ScaleB(value, -exponent);
                    if (!double.IsFinite(scaled) || value != 0 && scaled == 0) return AlgorithmStatus.NumericalFailure;
                    input[4 * u + d] = scaled;
                }
            }
            var status = ExtractCurve(input, p, surface.UKnots, uSpan, result);
            if (status != AlgorithmStatus.Success) return status;
            for (var u = 0; u <= p; u++) for (var d = 0; d < 4; d++) layers[4 * (u * (q + 1) + v) + d] = result[4 * u + d];
        }
        for (var u = 0; u <= p; u++)
        {
            layers.Slice(4 * u * (q + 1), 4 * (q + 1)).CopyTo(input);
            var status = ExtractCurve(input, q, surface.VKnots, vSpan, result);
            if (status != AlgorithmStatus.Success) return status;
            result[..(4 * (q + 1))].CopyTo(poles[(4 * u * (q + 1))..]);
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus ExtractCurve(ReadOnlySpan<IntervalNumber> input, SplineDegree degree,
        ReadOnlySpan<double> knots, KnotIndex span, Span<IntervalNumber> output)
    {
        var a = knots[span]; var b = knots[span + 1];
        if (!(b > a)) return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> work = stackalloc IntervalNumber[4 * 17 * 17]; work.Clear();
        var stride = 4 * (degree + 1);
        for (var j = 0; j <= degree; j++) for (var d = 0; d < 4; d++) work[j * stride + d] = input[4 * j + d];
        for (var r = 1; r <= degree; r++) for (var j = degree; j >= r; j--)
        {
            var index = span - degree + j;
            var denominator = new IntervalNumber(knots[index + degree - r + 1]) - knots[index];
            if (denominator.Lower <= 0) return AlgorithmStatus.NumericalFailure;
            var alpha0 = (new IntervalNumber(a) - knots[index]) / denominator;
            var alpha1 = (new IntervalNumber(b) - knots[index]) / denominator;
            for (var k = r; k >= 0; k--) for (var d = 0; d < 4; d++)
            {
                IntervalNumber value = 0;
                var fraction = new IntervalNumber(k) / r;
                if (k < r) value += (1 - fraction) * ((1 - alpha0) * work[(j - 1) * stride + 4 * k + d] + alpha0 * work[j * stride + 4 * k + d]);
                if (k > 0) value += fraction * ((1 - alpha1) * work[(j - 1) * stride + 4 * (k - 1) + d] + alpha1 * work[j * stride + 4 * (k - 1) + d]);
                work[j * stride + 4 * k + d] = value;
            }
        }
        work.Slice(degree * stride, stride).CopyTo(output); return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Restrict(in BezierSurfacePatch patch, in SurfaceParameterBox box, Span<IntervalNumber> poles)
    {
        if (!box.Valid || !(patch.Domain.U1 > patch.Domain.U0) || !(patch.Domain.V1 > patch.Domain.V0)) return AlgorithmStatus.InvalidInput;
        var p = patch.UDegree; var q = patch.VDegree;
        var size = 4 * (p + 1) * (q + 1); if (poles.Length < size) return AlgorithmStatus.WorkspaceTooSmall;
        var u0 = Normalize(box.U0, patch.Domain.U0, patch.Domain.U1);
        var u1 = Normalize(box.U1, patch.Domain.U0, patch.Domain.U1);
        var v0 = Normalize(box.V0, patch.Domain.V0, patch.Domain.V1);
        var v1 = Normalize(box.V1, patch.Domain.V0, patch.Domain.V1);
        Span<IntervalNumber> layer = stackalloc IntervalNumber[MaxScalars];
        Span<IntervalNumber> input = stackalloc IntervalNumber[4 * 17]; Span<IntervalNumber> result = stackalloc IntervalNumber[4 * 17];
        for (var v = 0; v <= q; v++)
        {
            for (var u = 0; u <= p; u++) for (var d = 0; d < 4; d++) input[4 * u + d] = patch.Poles[4 * (u * (q + 1) + v) + d];
            RestrictCurve(input, p, u0, u1, result);
            for (var u = 0; u <= p; u++) for (var d = 0; d < 4; d++) layer[4 * (u * (q + 1) + v) + d] = result[4 * u + d];
        }
        for (var u = 0; u <= p; u++)
        {
            layer.Slice(4 * u * (q + 1), 4 * (q + 1)).CopyTo(input);
            RestrictCurve(input, q, v0, v1, result); result[..(4 * (q + 1))].CopyTo(poles[(4 * u * (q + 1))..]);
        }
        return AlgorithmStatus.Success;
    }

    private static IntervalNumber Normalize(double parameter, double first, double last)
        => parameter == first ? 0 : parameter == last ? 1 : (new IntervalNumber(parameter) - first) / (new IntervalNumber(last) - first);

    private static void RestrictCurve(ReadOnlySpan<IntervalNumber> input, SplineDegree degree, IntervalNumber a,
        IntervalNumber b, Span<IntervalNumber> output)
    {
        Span<IntervalNumber> work = stackalloc IntervalNumber[4 * 17];
        // Blossom evaluation: kth restricted pole has degree-k copies of a
        // and k copies of b. This also supports tiny certification extensions.
        for (var k = 0; k <= degree; k++)
        {
            input[..(4 * (degree + 1))].CopyTo(work);
            for (var r = 0; r < degree; r++)
            {
                var t = r < degree - k ? a : b;
                for (var j = 0; j < degree - r; j++) for (var d = 0; d < 4; d++) work[4 * j + d] = (1 - t) * work[4 * j + d] + t * work[4 * (j + 1) + d];
            }
            work[..4].CopyTo(output[(4 * k)..]);
        }
    }

    /// <summary>XYZ, d/du and d/dv intervals; output layout is component-major.</summary>
    internal static AlgorithmStatus Bound(in BezierSurfacePatch patch, in SurfaceParameterBox box, Span<IntervalNumber> output)
    {
        if (output.Length < 9) return AlgorithmStatus.WorkspaceTooSmall;
        Span<IntervalNumber> poles = stackalloc IntervalNumber[MaxScalars];
        var status = Restrict(patch, box, poles); if (status != AlgorithmStatus.Success) return status;
        var p = patch.UDegree; var q = patch.VDegree;
        Span<IntervalNumber> h = stackalloc IntervalNumber[4]; Span<IntervalNumber> hu = stackalloc IntervalNumber[4]; Span<IntervalNumber> hv = stackalloc IntervalNumber[4];
        for (var d = 0; d < 4; d++)
        {
            h[d] = poles[d];
            for (var i = 1; i < (p + 1) * (q + 1); i++) h[d] = IntervalNumber.Hull(h[d], poles[4 * i + d]);
        }
        HomogeneousDerivativeHull(patch, box, true, hu);
        HomogeneousDerivativeHull(patch, box, false, hv);
        if (!(h[3].Lower > 0)) return AlgorithmStatus.NumericalFailure;
        for (var d = 0; d < 3; d++)
        {
            // A rational surface lies in the convex hull of its Cartesian
            // poles. This avoids independent numerator/weight overestimation.
            var value = poles[d] / poles[3];
            for (var i = 1; i < (p + 1) * (q + 1); i++) value = IntervalNumber.Hull(value, poles[4 * i + d] / poles[4 * i + 3]);
            output[d] = value;
            output[3 + d] = (hu[d] - value * hu[3]) / h[3];
            output[6 + d] = (hv[d] - value * hv[3]) / h[3];
            if (!value.Finite) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    private static void HomogeneousDerivativeHull(in BezierSurfacePatch patch, in SurfaceParameterBox box,
        bool alongU, Span<IntervalNumber> hull)
    {
        if ((alongU ? patch.UDegree : patch.VDegree) == 0) { hull.Clear(); return; }
        Span<IntervalNumber> derivatives = stackalloc IntervalNumber[MaxScalars];
        Span<IntervalNumber> restricted = stackalloc IntervalNumber[MaxScalars];
        var p = patch.UDegree; var q = patch.VDegree; var dp = alongU ? p - 1 : p; var dq = alongU ? q : q - 1;
        var scale = alongU ? p / (new IntervalNumber(patch.Domain.U1) - patch.Domain.U0) : q / (new IntervalNumber(patch.Domain.V1) - patch.Domain.V0);
        for (var u = 0; u <= dp; u++) for (var v = 0; v <= dq; v++) for (var d = 0; d < 4; d++)
        {
            var at = 4 * (u * (q + 1) + v) + d;
            derivatives[4 * (u * (dq + 1) + v) + d] = scale * (patch.Poles[at + (alongU ? 4 * (q + 1) : 4)] - patch.Poles[at]);
        }
        var derivative = new BezierSurfacePatch(derivatives[..(4 * (dp + 1) * (dq + 1))], dp, dq, patch.Domain);
        Restrict(derivative, box, restricted);
        for (var d = 0; d < 4; d++)
        {
            hull[d] = restricted[d];
            for (var i = 1; i < (dp + 1) * (dq + 1); i++) hull[d] = IntervalNumber.Hull(hull[d], restricted[4 * i + d]);
        }
    }
}
