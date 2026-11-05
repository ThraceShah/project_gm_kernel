using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int MakeCurvedCutICurve(CurveTag exact, double start, double end, SurfTag surface, SurfTag plane, out CurveTag curve)
    {
        curve = 0;
        var record = Curves[GetCurveSlotByTag(exact)];
        if (record.Class is not (CurveClass.BCurve or CurveClass.Line)) return ParasolidConstants.PK_ERROR_not_implemented;
        var count = record.Class == CurveClass.Line ? 2 : 33;
        Span<double> chartBuffer = stackalloc double[3 * 33]; Span<double> uvBuffer = stackalloc double[4 * 33];
        var chart = chartBuffer[..(3 * count)]; var uv = uvBuffer[..(4 * count)];
        Span<KernelVector3> jet = stackalloc KernelVector3[2];
        var status = EvaluateCurveCore(in record, start, 1, false, jet, out _);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        status = TryIntersectionPairedParameters(surface, plane, jet[0], out var firstUv, out _);
        if (status != AlgorithmStatus.Success) return IntersectionError(status);
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout); Span<KernelVector3> surfaceJet = stackalloc KernelVector3[4];
        status = EvaluateSurfaceCore(in Surfaces[GetSurfaceSlotByTag(surface)], firstUv.param[0], firstUv.param[1], in layout, surfaceJet, 0);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        var normal = ProfileCross(surfaceJet[layout.GetIndex(1, 0)], surfaceJet[layout.GetIndex(0, 1)]);
        TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(plane)], out var cutting);
        var forward = Dot(ProfileCross(normal, Scale(cutting.Axis, LocalSenseSign(Surfaces[GetSurfaceSlotByTag(plane)].Sense))), jet[1]) > 0;
        var first = forward ? surface : plane; var second = forward ? plane : surface;
        for (var i = 0; i < count; i++)
        {
            var parameter = start + (end - start) * i / (count - 1);
            status = EvaluateCurveCore(in record, parameter, 0, false, jet, out _);
            if (status != AlgorithmStatus.Success) return EvaluationError(status);
            chart[3 * i] = jet[0].X; chart[3 * i + 1] = jet[0].Y; chart[3 * i + 2] = jet[0].Z;
            status = TryIntersectionPairedParameters(first, second, jet[0], out var a, out var b);
            if (status != AlgorithmStatus.Success) return IntersectionError(status);
            uv[4 * i] = a.param[0]; uv[4 * i + 1] = a.param[1]; uv[4 * i + 2] = b.param[0]; uv[4 * i + 3] = b.param[1];
        }
        double chordalError = 1e-12, angularError = 1e-12;
        if (record.Class == CurveClass.BCurve)
        {
            var view = GetBCurveView(BCurveDataStore[record.DataIndex]);
            if (view.VertexCount != view.Degree + 1 || !view.Rational || view.Dimension != 4)
                return ParasolidConstants.PK_ERROR_not_implemented;
            var error = CurvedChartBounds(view, out chordalError, out angularError); if (error != 0) return error;
        }
        var input = new IcurveDecodeInput
        {
            Surface0Tag = first, Surface1Tag = second, BaseScale = 1, ChartCount = count, ChartHvecs = chart,
            UvType = IntersectionUvType.Both, UvValues = uv, ChordalError = chordalError, AngularError = angularError,
            Start = new() { Type = LimitType.Artificial, TermUse = LimitTermUse.Unset, Hvecs = chart[..3] },
            End = new() { Type = LimitType.Artificial, TermUse = LimitTermUse.Unset, Hvecs = chart[^3..] },
            Sense = ParasolidConstants.PK_TOPOL_sense_positive_c,
        };
        status = DecodeIcurve(input, out var data, out _, out _); if (status != AlgorithmStatus.Success) return IntersectionError(status);
        status = TryBindICurveEntity(data, out curve); if (status != AlgorithmStatus.Success) return IntersectionError(status);
        ICurveDataPool[data].OwnsSupportSurfaces = 1;
        return 0;
    }

    private static int CurvedChartBounds(in BCurveView view, out double chordal, out double angular)
    {
        chordal = angular = 0;
        Span<double> remainder = stackalloc double[68]; Span<double> work = stackalloc double[68];
        Span<double> segment = stackalloc double[68];
        view.Vertices.CopyTo(remainder); var degree = view.Degree;
        for (var interval = 0; interval < 32; interval++)
        {
            remainder.CopyTo(work); var t = 1.0 / (32 - interval);
            for (var level = 0; level <= degree; level++)
            {
                for (var d = 0; d < 4; d++)
                { segment[4 * level + d] = work[d]; remainder[4 * (degree - level) + d] = work[4 * (degree - level) + d]; }
                for (var j = 0; j < degree - level; j++) for (var d = 0; d < 4; d++)
                    work[4 * j + d] = (1 - t) * work[4 * j + d] + t * work[4 * (j + 1) + d];
            }
            var origin = Vector(segment[0] / segment[3], segment[1] / segment[3], segment[2] / segment[3]);
            var last = 4 * degree; var end = Vector(segment[last] / segment[last + 3], segment[last + 1] / segment[last + 3], segment[last + 2] / segment[last + 3]);
            var chord = Sub(end, origin); var length = OperationIntersection.Norm(chord);
            if (!(length > 0)) return ParasolidConstants.PK_ERROR_cant_do_intersect;
            chord = Scale(chord, 1 / length);
            for (var i = 0; i <= degree; i++)
            {
                var at = 4 * i; if (!(segment[at + 3] > 0)) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var p = Vector(segment[at] / segment[at + 3], segment[at + 1] / segment[at + 3], segment[at + 2] / segment[at + 3]);
                var delta = Sub(p, origin); var projection = Dot(delta, chord);
                chordal = Math.Max(chordal, OperationIntersection.Norm(Sub(delta, Scale(chord, Math.Clamp(projection, 0, length)))));
                for (var j = 0; j < i; j++)
                {
                    var a = 4 * j; var q = Vector(segment[a] / segment[a + 3], segment[a + 1] / segment[a + 3], segment[a + 2] / segment[a + 3]);
                    var direction = Sub(p, q); var norm = OperationIntersection.Norm(direction);
                    if (norm == 0) continue;
                    var cosine = Dot(direction, chord) / norm;
                    if (!(cosine > 1e-8)) return ParasolidConstants.PK_ERROR_not_implemented;
                    angular = Math.Max(angular, Math.Acos(Math.Min(1, cosine)));
                }
            }
        }
        chordal = Math.Max(chordal * 1.000001, 1e-12); angular = Math.Max(angular * 1.000001, 1e-12);
        return double.IsFinite(chordal) && double.IsFinite(angular) ? 0 : ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

}
