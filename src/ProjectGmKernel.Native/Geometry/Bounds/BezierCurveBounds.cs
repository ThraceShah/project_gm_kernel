using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Bounds;

internal static class BezierCurveBounds
{
    internal static AlgorithmStatus Extract(in BCurveView curve, KnotIndex span, Span<IntervalNumber> poles)
    {
        if (curve.Degree < 1 || curve.Degree > 16 || curve.Dimension != (curve.Rational ? 4 : 3)) return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> input = stackalloc IntervalNumber[4 * 17];
        var scale = 1.0;
        if (curve.Rational)
        {
            scale = 0;
            for (var i = 0; i <= curve.Degree; i++) scale = Math.Max(scale, curve.Vertices[(span - curve.Degree + i) * curve.Dimension + 3]);
        }
        if (!(scale > 0) || !double.IsFinite(scale)) return AlgorithmStatus.InvalidInput;
        var exponent = Math.ILogB(scale);
        for (var i = 0; i <= curve.Degree; i++) for (var d = 0; d < 4; d++)
        {
            var value = d == 3 && !curve.Rational ? 1 : curve.Vertices[(span - curve.Degree + i) * curve.Dimension + d];
            var normalized = Math.ScaleB(value, -exponent);
            if (!double.IsFinite(normalized) || value != 0 && normalized == 0) return AlgorithmStatus.NumericalFailure;
            input[4 * i + d] = normalized;
        }
        return BezierSurfaceBounds.ExtractCurve(input, curve.Degree, curve.Knots, span, poles);
    }
}
