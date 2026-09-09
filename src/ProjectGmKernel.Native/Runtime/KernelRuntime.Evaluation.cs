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
        // PK evaluates SP-curve derivatives only up to second order and
        // rejects higher orders outright (verified against PK_CURVE_eval).
        if (record.Class == CurveClass.SPCurve && order > 2)
            return ParasolidConstants.PK_ERROR_too_many_derivatives;
        Span<KernelVector3> values = stackalloc KernelVector3[11];
        var status = EvaluateCurveCore(in record, t, order, values, out KernelVector3 direction);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        if (tangent is not null && direction.X == 0 && direction.Y == 0 && direction.Z == 0)
            return ParasolidConstants.PK_ERROR_at_singularity;
        for (DerivativeOrder i = 0; i <= order; i++) WriteVector(output + i, in values[i]);
        if (tangent is not null) WriteVector(tangent, in direction);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    /// <summary>
    /// Per-class curve evaluation into values[0..order] plus the unit tangent.
    /// Callers must hold the command scratch. Dependent curves (trimmed,
    /// surface parameter) delegate to their supporting geometry.
    /// </summary>
    private static AlgorithmStatus EvaluateCurveCore(ref readonly CurveRecord record, double t, DerivativeOrder order,
        Span<KernelVector3> values, out KernelVector3 direction)
    {
        direction = default;
        switch (record.Class)
        {
            case CurveClass.Line:
                return CurveEvaluation.Evaluate(in LineDataPool[record.DataIndex], t, order, values, out direction);
            case CurveClass.Circle:
                return CurveEvaluation.Evaluate(in CircleDataPool[record.DataIndex], t, order, values, out direction);
            case CurveClass.Ellipse:
                return CurveEvaluation.Evaluate(in EllipseDataPool[record.DataIndex], t, order, values, out direction);
            case CurveClass.BCurve:
            {
                var view = GetBCurveView(in BCurveDataStore[record.DataIndex]);
                if (view.Dimension < 3) return AlgorithmStatus.Unsupported;    // planar (u, v) B-curves only feed SP-curves
                var bCurveStatus = BCurveEvaluation.Evaluate(in view, t, Math.Min(Math.Max(order, 1), view.Degree), values, CommandScratch.Current.AvailableSpanOfDoubles(), out direction);
                if (bCurveStatus == AlgorithmStatus.Success && values[1].X * values[1].X + values[1].Y * values[1].Y + values[1].Z * values[1].Z <= 1e-22)
                    direction = default;
                // PK_CURVE_eval V38 zero-fills orders above the spline degree, including rational curves.
                // The numerical evaluator retains the full rational derivative recurrence for internal use.
                if (bCurveStatus == AlgorithmStatus.Success && order > view.Degree)
                    values.Slice(view.Degree + 1, order - view.Degree).Clear();
                return bCurveStatus;
            }
            case CurveClass.TRCurve:
            {
                var basisSlot = GetCurveSlotByTag(TrCurveDataPool[record.DataIndex].BasisCurveTag);
                if (basisSlot < 0 || !Curves.IsAlive(basisSlot)) return AlgorithmStatus.Unsupported;
                return EvaluateCurveCore(in Curves[basisSlot], t, order, values, out direction);
            }
            case CurveClass.SPCurve:
            {
                ref readonly var sp = ref SpCurveDataPool[record.DataIndex];
                var surfaceSlot = GetSurfaceSlotByTag(sp.SurfTag);
                var basisSlot = GetCurveSlotByTag(sp.BCurveTag);
                if (surfaceSlot < 0 || !Surfaces.IsAlive(surfaceSlot) || basisSlot < 0 || !Curves.IsAlive(basisSlot)
                    || Curves[basisSlot].Class != CurveClass.BCurve)
                    return AlgorithmStatus.Unsupported;
                Span<KernelVector3> uv = stackalloc KernelVector3[11];
                var view = GetBCurveView(in BCurveDataStore[Curves[basisSlot].DataIndex]);
                // The tangent always needs the first parameter derivative.
                var tangentOrder = Math.Max(order, 1);
                var uvStatus = SpCurveEvaluation.EvaluateUV(in view, t, tangentOrder, CommandScratch.Current.AvailableSpanOfDoubles(), uv);
                if (uvStatus != AlgorithmStatus.Success) return uvStatus;
                Span<KernelVector3> jet = stackalloc KernelVector3[121];
                if (!SurfaceDerivativeLayout.TryCreate(tangentOrder, tangentOrder, out var jetLayout))
                    return AlgorithmStatus.InvalidInput;
                var surfaceStatus = EvaluateSurfaceCore(in Surfaces[surfaceSlot], uv[0].X, uv[0].Y, in jetLayout, jet, 0);
                if (surfaceStatus != AlgorithmStatus.Success) return surfaceStatus;
                SpCurveEvaluation.Compose(in jetLayout, jet, uv, tangentOrder, values);
                direction = Unit(values[1]);
                return IsFinite(direction) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
            }
            default:
                return AlgorithmStatus.Unsupported;
        }
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
        // PK evaluates offset surface derivatives only up to second order in
        // each direction and rejects higher orders outright (verified against
        // PK_SURF_eval).
        if (record.Class == SurfaceClass.Offset && (uOrder > 2 || vOrder > 2))
            return ParasolidConstants.PK_ERROR_too_many_derivatives;
        if (!SurfaceDerivativeLayout.TryCreate(uOrder, vOrder, out var layout))
            return ParasolidConstants.PK_ERROR_bad_value;
        Span<KernelVector3> values = stackalloc KernelVector3[121];
        var status = EvaluateSurfaceCore(in record, uv.param[0], uv.param[1], in layout, values, 0);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        BufferOffset index = 0;
        // PK uses u-fast rows; triangular rows shorten as the v derivative increases.
        for (DerivativeOrder j = 0; j <= vOrder; j++)
        for (DerivativeOrder i = 0; i <= (triangular != 0 ? uOrder - j : uOrder); i++)
            WriteVector(output + index++, in values[layout.GetIndex(i, j)]);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    /// <summary>
    /// Per-class surface evaluation into the rectangular derivative layout.
    /// Callers must hold the command scratch. Dependent surfaces (B-surface,
    /// offset, swept, spun) compose the evaluation of their supporting
    /// geometry; depth bounds offset-of-offset recursion.
    /// </summary>
    private static AlgorithmStatus EvaluateSurfaceCore(ref readonly SurfaceRecord record, double u, double v,
        in SurfaceDerivativeLayout layout, Span<KernelVector3> values, int depth)
    {
        switch (record.Class)
        {
            case SurfaceClass.Plane:
            case SurfaceClass.Cylinder:
            case SurfaceClass.Cone:
            case SurfaceClass.Sphere:
            case SurfaceClass.Torus:
                if (!TryPrepareSurface(in record, out var prepared)) return AlgorithmStatus.Unsupported;
                return SurfaceEvaluation.Evaluate(in prepared, u, v, in layout, values);
            case SurfaceClass.BSurface:
            {
                ref readonly var data = ref BSurfaceDataStore[record.DataIndex];
                return BSurfaceEvaluation.Evaluate(GetBSurfaceView(in data), u, v, in layout,
                    CommandScratch.Current.AvailableSpanOfDoubles(), values);
            }
            case SurfaceClass.Swept:
            {
                ref readonly var swept = ref SweptDataPool[record.DataIndex];
                Span<KernelVector3> section = stackalloc KernelVector3[11];
                var sectionStatus = EvaluateSectionCurve(swept.SectionCurveTag, u, layout.UOrder, section);
                if (sectionStatus != AlgorithmStatus.Success) return sectionStatus;
                return SweptSurfaceEvaluation.Evaluate(section, swept.Sweep, v, in layout, values);
            }
            case SurfaceClass.Spun:
            {
                ref readonly var spun = ref SpunDataPool[record.DataIndex];
                Span<KernelVector3> profile = stackalloc KernelVector3[11];
                var profileStatus = EvaluateSectionCurve(spun.ProfileCurveTag, u, layout.UOrder, profile);
                if (profileStatus != AlgorithmStatus.Success) return profileStatus;
                return SpunSurfaceEvaluation.Evaluate(profile, spun.Base, spun.Axis, v, in layout, values);
            }
            case SurfaceClass.Offset:
            {
                if (depth >= 8) return AlgorithmStatus.Unsupported;
                ref readonly var offset = ref OffsetDataPool[record.DataIndex];
                var baseSlot = GetSurfaceSlotByTag(offset.BaseSurfTag);
                if (baseSlot < 0 || !Surfaces.IsAlive(baseSlot)) return AlgorithmStatus.Unsupported;
                if (!SurfaceDerivativeLayout.TryCreate(layout.UOrder + 1, layout.VOrder + 1, out var baseLayout))
                    return AlgorithmStatus.InvalidInput;
                Span<KernelVector3> baseValues = stackalloc KernelVector3[144];
                var baseStatus = EvaluateSurfaceCore(in Surfaces[baseSlot], u, v, in baseLayout, baseValues, depth + 1);
                if (baseStatus != AlgorithmStatus.Success) return baseStatus;
                return OffsetSurfaceEvaluation.Evaluate(baseValues, layout.VOrder + 1, offset.Offset, in layout, values);
            }
            default:
                return AlgorithmStatus.Unsupported;
        }
    }

    /// <summary>Section/profile curve evaluation (analytic or B-curve per the XT swept/spun restrictions).</summary>
    private static AlgorithmStatus EvaluateSectionCurve(CurveTag curveTag, double u, DerivativeOrder order,
        Span<KernelVector3> section)
    {
        var slot = GetCurveSlotByTag(curveTag);
        if (slot < 0 || !Curves.IsAlive(slot)) return AlgorithmStatus.Unsupported;
        ref readonly var curve = ref Curves[slot];
        if (curve.Class is not (CurveClass.Line or CurveClass.Circle or CurveClass.Ellipse or CurveClass.BCurve))
            return AlgorithmStatus.Unsupported;
        return EvaluateCurveCore(in curve, u, order, section, out _);
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
