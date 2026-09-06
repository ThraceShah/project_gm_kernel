using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int CurveEvalImplementation(CurveTag curve, double t, DerivativeOrder order, PK_VECTOR_s* output)
    {
        var ownsScratch = EnsureCommandScratch();
        try { return EvaluateCurve(curve, t, order, output, null); }
        finally { if (ownsScratch) ReleaseCommandScratch(State.Session); }
    }

    private static int CurveEvalWithTangentImplementation(CurveTag curve, double t, DerivativeOrder order,
        PK_VECTOR_s* output, PK_VECTOR_s* tangent)
    {
        if (tangent is null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var ownsScratch = EnsureCommandScratch();
        try { return EvaluateCurve(curve, t, order, output, tangent); }
        finally { if (ownsScratch) ReleaseCommandScratch(State.Session); }
    }

    private static int EvaluateCurve(CurveTag curve, double t, DerivativeOrder order,
        PK_VECTOR_s* output, PK_VECTOR_s* tangent)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (!IsValidTag(curve)) return ParasolidConstants.PK_ERROR_not_a_tag;
        if ((EntityClass)TagRec(curve).ClassCode != EntityClass.Curve) return ParasolidConstants.PK_ERROR_wrong_entity;
        if (output is null || !double.IsFinite(t) || order < 0) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (order > 10) return ParasolidConstants.PK_ERROR_too_many_derivatives;
        ref readonly var record = ref Curves[TagRec(curve).Slot];
        Span<KernelVector3> values = stackalloc KernelVector3[11];
        KernelVector3 direction;
        AlgorithmStatus status;
        switch (record.Class)
        {
            case CurveClass.Line:
                status = CurveEvaluation.Evaluate(in LineDataPool[record.DataIndex], t, order, values, out direction);
                break;
            case CurveClass.Circle:
                status = CurveEvaluation.Evaluate(in CircleDataPool[record.DataIndex], t, order, values, out direction);
                break;
            case CurveClass.BCurve:
                var view = GetBCurveView(in BCurveDataStore[record.DataIndex]);
                status = BCurveEvaluation.Evaluate(in view, t, Math.Min(Math.Max(order, 1), view.Degree), values, CommandScratch.Current.AvailableSpanOfDoubles(), out direction);
                if (status == AlgorithmStatus.Success && tangent is not null)
                {
                    var first = values[1];
                    if (first.X * first.X + first.Y * first.Y + first.Z * first.Z <= 1e-22)
                        direction = default;
                }
                // PK_CURVE_eval V38 zero-fills orders above the spline degree, including rational curves.
                // The numerical evaluator retains the full rational derivative recurrence for internal use.
                if (status == AlgorithmStatus.Success && order > view.Degree)
                    values.Slice(view.Degree + 1, order - view.Degree).Clear();
                break;
            default:
                return ParasolidConstants.PK_ERROR_not_implemented;
        }
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        if (tangent is not null && direction.X == 0 && direction.Y == 0 && direction.Z == 0)
            return ParasolidConstants.PK_ERROR_at_singularity;
        for (DerivativeOrder i = 0; i <= order; i++) WriteVector(output + i, in values[i]);
        if (tangent is not null) WriteVector(tangent, in direction);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int SurfEvalImplementation(SurfTag surface, PK_UV_s uv, DerivativeOrder uOrder, DerivativeOrder vOrder,
        KernelLogical triangular, PK_VECTOR_s* output)
    {
        var ownsScratch = EnsureCommandScratch();
        try { return SurfEvalCore(surface, uv, uOrder, vOrder, triangular, output); }
        finally { if (ownsScratch) ReleaseCommandScratch(State.Session); }
    }

    private static int SurfEvalCore(SurfTag surface, PK_UV_s uv, DerivativeOrder uOrder, DerivativeOrder vOrder,
        KernelLogical triangular, PK_VECTOR_s* output)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (!IsValidTag(surface)) return ParasolidConstants.PK_ERROR_not_a_tag;
        if ((EntityClass)TagRec(surface).ClassCode != EntityClass.Surface) return ParasolidConstants.PK_ERROR_wrong_entity;
        if (output is null || !double.IsFinite(uv.param[0]) || !double.IsFinite(uv.param[1]))
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (triangular != 0 && uOrder != vOrder) return ParasolidConstants.PK_ERROR_num_derivs_not_equal;
        if (uOrder > 10 || vOrder > 10) return ParasolidConstants.PK_ERROR_too_many_derivatives;
        // These counts are marked [NF] in the PK contract; V38 treats negative counts as zero.
        uOrder = Math.Max(0, uOrder);
        vOrder = Math.Max(0, vOrder);
        ref readonly var record = ref Surfaces[TagRec(surface).Slot];
        if (!TryPrepareSurface(in record, out var prepared)) return ParasolidConstants.PK_ERROR_not_implemented;
        if (!SurfaceDerivativeLayout.TryCreate(uOrder, vOrder, out var layout))
            return ParasolidConstants.PK_ERROR_bad_value;
        Span<KernelVector3> values = stackalloc KernelVector3[121];
        var status = SurfaceEvaluation.Evaluate(in prepared, uv.param[0], uv.param[1], in layout, values);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        BufferOffset index = 0;
        // PK uses u-fast rows; triangular rows shorten as the v derivative increases.
        for (DerivativeOrder j = 0; j <= vOrder; j++)
        for (DerivativeOrder i = 0; i <= (triangular != 0 ? uOrder - j : uOrder); i++)
            WriteVector(output + index++, in values[layout.GetIndex(i, j)]);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static bool TryPrepareSurface(in SurfaceRecord record, out AnalyticSurface surface)
    {
        switch (record.Class)
        {
            case SurfaceClass.Plane:
                ref readonly var p = ref PlaneDataPool[record.DataIndex];
                surface = new(record.Class, Vector(p.LocationX, p.LocationY, p.LocationZ),
                    Vector(p.NormalX, p.NormalY, p.NormalZ), Vector(p.RefDirX, p.RefDirY, p.RefDirZ));
                return true;
            case SurfaceClass.Cylinder:
                ref readonly var c = ref CylinderDataPool[record.DataIndex];
                surface = new(record.Class, Vector(c.LocationX, c.LocationY, c.LocationZ),
                    Vector(c.AxisX, c.AxisY, c.AxisZ), Vector(c.RefDirX, c.RefDirY, c.RefDirZ), c.Radius);
                return true;
            case SurfaceClass.Cone:
                ref readonly var cone = ref ConeDataPool[record.DataIndex];
                surface = new(record.Class, Vector(cone.LocationX, cone.LocationY, cone.LocationZ),
                    Vector(cone.AxisX, cone.AxisY, cone.AxisZ), Vector(cone.RefDirX, cone.RefDirY, cone.RefDirZ),
                    cone.Radius, Math.Tan(cone.SemiAngle));
                return true;
            case SurfaceClass.Sphere:
                ref readonly var s = ref SphereDataPool[record.DataIndex];
                surface = new(record.Class, Vector(s.CenterX, s.CenterY, s.CenterZ),
                    Vector(s.AxisX, s.AxisY, s.AxisZ), Vector(s.RefDirX, s.RefDirY, s.RefDirZ), s.Radius);
                return true;
            case SurfaceClass.Torus:
                ref readonly var t = ref TorusDataPool[record.DataIndex];
                surface = new(record.Class, Vector(t.LocationX, t.LocationY, t.LocationZ),
                    Vector(t.AxisX, t.AxisY, t.AxisZ), Vector(t.RefDirX, t.RefDirY, t.RefDirZ), t.MajorRadius, t.MinorRadius);
                return true;
            default:
                surface = default;
                return false;
        }
    }

    private static int EvaluationError(AlgorithmStatus status) => status switch
    {
        AlgorithmStatus.InvalidInput => ParasolidConstants.PK_ERROR_bad_parameter,
        AlgorithmStatus.Unsupported => ParasolidConstants.PK_ERROR_not_implemented,
        _ => ParasolidConstants.PK_ERROR_eval_failure,
    };

    private static void WriteVector(PK_VECTOR_s* target, in KernelVector3 source)
    {
        target->coord[0] = source.X;
        target->coord[1] = source.Y;
        target->coord[2] = source.Z;
    }
}
