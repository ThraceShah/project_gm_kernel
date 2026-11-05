using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectBCurves(in IntersectionCurveSpan a, in IntersectionCurveSpan b,
        Span<OperationIntersectionPoint> output, out BufferCount count)
    {
        count = 0;
        Span<IntervalNumber> ap = stackalloc IntervalNumber[4 * 17]; Span<IntervalNumber> bp = stackalloc IntervalNumber[4 * 17];
        Span<OperationIntersectionPoint> local = stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        var ac = GeneralCurvePieceCount(a); var bc = GeneralCurvePieceCount(b);
        if (ac <= 0 || bc <= 0) return AlgorithmStatus.Unsupported;
        for (var u = 0; u < ac; u++)
        {
            var status = GeneralCurvePiece(a, u, ap, out var p, out var domain, out var lo, out var hi); if (status != AlgorithmStatus.Success) return status;
            if (!(hi > lo)) continue;
            var patch = new BezierSurfacePatch(ap[..(4 * (p + 1))], p, 0, domain);
            for (var v = 0; v < bc; v++)
            {
                status = GeneralCurvePiece(b, v, bp, out var q, out var otherDomain, out var start, out var end); if (status != AlgorithmStatus.Success) return status;
                if (!(end > start)) continue;
                var other = new BezierSurfacePatch(bp[..(4 * (q + 1))], q, 0, otherDomain);
                var nc = 0;
                status = BezierCurveIntersection.Find(patch, other, lo, hi, start, end, local, ref nc);
                if (status != AlgorithmStatus.Success) return status;
                for (var i = 0; i < nc; i++)
                {
                    if (!GeneralCurveNativeParameter(a, local[i].Point, local[i].First, out var firstParameter)
                        || !GeneralCurveNativeParameter(b, local[i].Point, local[i].Second, out var secondParameter)) return AlgorithmStatus.NumericalFailure;
                    if (!OperationIntersection.AddPoint(output, ref count, local[i].Point, firstParameter, secondParameter, IntersectionVectorKind.Simple)) return AlgorithmStatus.OutputTooSmall;
                }
            }
        }
        OperationIntersection.Sort(output[..count]); return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus IntersectBSurfaceCurve(SurfaceSlot surface, in IntersectionCurveSpan span,
        Span<OperationIntersectionPoint> output, out BufferCount count)
    {
        count = 0; var pieces = GeneralCurvePieceCount(span); if (pieces <= 0) return AlgorithmStatus.Unsupported;
        var view = GetBSurfaceView(BSurfaceDataStore[Surfaces[surface].DataIndex]);
        Span<IntervalNumber> sp = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<IntervalNumber> cp = stackalloc IntervalNumber[4 * 17];
        Span<SurfaceIntersectionNode> roots = stackalloc SurfaceIntersectionNode[64];
        for (var u = view.UDegree; u < view.UVertexCount; u++) for (var v = view.VDegree; v < view.VVertexCount; v++)
        {
            if (!(view.UKnots[u + 1] > view.UKnots[u]) || !(view.VKnots[v + 1] > view.VKnots[v])) continue;
            var status = BezierSurfaceBounds.Extract(view, u, v, sp); if (status != AlgorithmStatus.Success) return status;
            var patch = new BezierSurfacePatch(sp[..(4 * (view.UDegree + 1) * (view.VDegree + 1))], view.UDegree, view.VDegree,
                new(view.UKnots[u], view.UKnots[u + 1], view.VKnots[v], view.VKnots[v + 1]));
            for (var s = 0; s < pieces; s++)
            {
                status = GeneralCurvePiece(span, s, cp, out var degree, out var curveDomain, out var lo, out var hi); if (status != AlgorithmStatus.Success) return status;
                if (!(hi > lo)) continue;
                var piece = new BezierSurfacePatch(cp[..(4 * (degree + 1))], degree, 0, curveDomain);
                var domain = new ParameterCell { Lower = new() { U = patch.Domain.U0, V = patch.Domain.V0, S = lo },
                    Upper = new() { U = patch.Domain.U1, V = patch.Domain.V1, S = hi } };
                status = ParametricSurfaceIntersection.Isolate(patch, piece, domain, 3, roots, out var n);
                if (status != AlgorithmStatus.Success) return status;
                for (var i = 0; i < n; i++)
                {
                    if (!GeneralCurveNativeParameter(span, roots[i].Point, roots[i].S, out var parameter)) return AlgorithmStatus.NumericalFailure;
                    if (!OperationIntersection.AddPoint(output, ref count, roots[i].Point, parameter, 0, IntersectionVectorKind.Simple)) return AlgorithmStatus.OutputTooSmall;
                    for (var j = 0; j < count; j++) if (Math.Abs(output[j].First - parameter) < 1e-10)
                    { output[j].U = roots[i].U; output[j].V = roots[i].V; }
                }
            }
        }
        OperationIntersection.Sort(output[..count]); return AlgorithmStatus.Success;
    }

    private static BufferCount GeneralCurvePieceCount(in IntersectionCurveSpan span)
    {
        var record = Curves[span.Slot];
        if (record.Class == CurveClass.BCurve) { var view = GetBCurveView(BCurveDataStore[record.DataIndex]); return view.VertexCount - view.Degree; }
        if (record.Class == CurveClass.Line) return 1;
        if (TryGetIntersectionConic(record, out _))
        {
            var n = Math.Floor(span.Upper / (Math.PI * .5)) - Math.Floor(span.Lower / (Math.PI * .5)) + 1;
            return n <= 128 ? (int)n : 0;
        }
        return 0;
    }

    private static AlgorithmStatus GeneralCurvePiece(in IntersectionCurveSpan span, BufferOffset index, Span<IntervalNumber> poles,
        out SplineDegree degree, out SurfaceParameterBox domain, out double lo, out double hi)
    {
        var record = Curves[span.Slot]; degree = 1; lo = span.Lower; hi = span.Upper; domain = new(lo, hi, 0, 1);
        if (record.Class == CurveClass.BCurve)
        {
            var view = GetBCurveView(BCurveDataStore[record.DataIndex]); var knot = index + view.Degree;
            degree = view.Degree; lo = Math.Max(lo, view.Knots[knot]); hi = Math.Min(hi, view.Knots[knot + 1]); domain = new(view.Knots[knot], view.Knots[knot + 1], 0, 1);
            if (!(hi > lo)) return AlgorithmStatus.Success;
            return BezierCurveBounds.Extract(view, knot, poles);
        }
        if (record.Class == CurveClass.Line)
        {
            GetIntersectionLine(record, out var origin, out var direction);
            for (var end = 0; end < 2; end++)
            {
                var t = end == 0 ? lo : hi;
                poles[4 * end] = new IntervalNumber(origin.X) + direction.X * new IntervalNumber(t);
                poles[4 * end + 1] = new IntervalNumber(origin.Y) + direction.Y * new IntervalNumber(t);
                poles[4 * end + 2] = new IntervalNumber(origin.Z) + direction.Z * new IntervalNumber(t); poles[4 * end + 3] = 1;
            }
            return AlgorithmStatus.Success;
        }
        if (!TryGetIntersectionConic(record, out var conic)) return AlgorithmStatus.Unsupported;
        degree = 2; var quadrant = (int)Math.Floor(span.Lower / (Math.PI * .5)) + index;
        var a = Math.Max(span.Lower, quadrant * (Math.PI * .5)); var b = Math.Min(span.Upper, (quadrant + 1) * (Math.PI * .5));
        var ta = Math.Tan((a - quadrant * (Math.PI * .5)) * .5); var tb = Math.Tan((b - quadrant * (Math.PI * .5)) * .5); var w = Math.Sqrt(.5);
        lo = quadrant + ta / (w + (1 - w) * ta); hi = quadrant + tb / (w + (1 - w) * tb); domain = new(quadrant, quadrant + 1, 0, 1);
        var y = ProfileCross(conic.Axis, conic.Reference);
        for (var i = 0; i < 3; i++)
        {
            AnalyticBezierPatches.CirclePole(quadrant, i, out var x, out var cy, out var weight);
            poles[4 * i] = conic.Origin.X * weight + conic.Radius * conic.Reference.X * x + conic.Minor * y.X * cy;
            poles[4 * i + 1] = conic.Origin.Y * weight + conic.Radius * conic.Reference.Y * x + conic.Minor * y.Y * cy;
            poles[4 * i + 2] = conic.Origin.Z * weight + conic.Radius * conic.Reference.Z * x + conic.Minor * y.Z * cy;
            poles[4 * i + 3] = weight;
        }
        return AlgorithmStatus.Success;
    }

    private static bool GeneralCurveNativeParameter(in IntersectionCurveSpan span, in KernelVector3 point, double raw, out double parameter)
    {
        var record = Curves[span.Slot]; parameter = raw;
        if (TryGetIntersectionConic(record, out var conic)) return ConicParameter(conic, point, span.Lower, span.Upper, out parameter);
        return double.IsFinite(raw);
    }
}
