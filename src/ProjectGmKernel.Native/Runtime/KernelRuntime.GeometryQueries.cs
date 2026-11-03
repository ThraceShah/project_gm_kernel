using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int PointAskImplementation(PointTag point, PK_POINT_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (!State.Session->Tags.TryResolve(point, CurrentSessionId, out var record)
            || record.Pool != (byte)PoolKind.Point)
            return ParasolidConstants.PK_ERROR_unknown_class;
        var value = new PK_POINT_sf_s();
        WriteVector(&value.position, in Points[record.Slot].Position);
        *result = value;
        return 0;
    }

    private static int EllipseAskImplementation(CurveTag curve, PK_ELLIPSE_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetCurveSlotByTag(curve);
        if (slot < 0 || Curves[slot].Class != CurveClass.Ellipse) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref EllipseDataPool[Curves[slot].DataIndex];
        var value = new PK_ELLIPSE_sf_s { R1 = data.R1, R2 = data.R2 };
        value.basis_set.location.coord[0] = data.CenterX;
        value.basis_set.location.coord[1] = data.CenterY;
        value.basis_set.location.coord[2] = data.CenterZ;
        value.basis_set.axis.coord[0] = data.AxisX;
        value.basis_set.axis.coord[1] = data.AxisY;
        value.basis_set.axis.coord[2] = data.AxisZ;
        value.basis_set.ref_direction.coord[0] = data.RefDirX;
        value.basis_set.ref_direction.coord[1] = data.RefDirY;
        value.basis_set.ref_direction.coord[2] = data.RefDirZ;
        *result = value;
        return 0;
    }

    private static int TrCurveAskImplementation(CurveTag curve, PK_TRCURVE_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetCurveSlotByTag(curve);
        if (slot < 0 || Curves[slot].Class != CurveClass.TRCurve) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref TrCurveDataPool[Curves[slot].DataIndex];
        var value = new PK_TRCURVE_sf_s { basis_curve = data.BasisCurveTag };
        value.t_int.value[0] = data.Parm1;
        value.t_int.value[1] = data.Parm2;
        *result = value;
        return 0;
    }

    private static int SpCurveAskImplementation(CurveTag curve, PK_SPCURVE_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetCurveSlotByTag(curve);
        if (slot < 0 || Curves[slot].Class != CurveClass.SPCurve) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref SpCurveDataPool[Curves[slot].DataIndex];
        *result = new PK_SPCURVE_sf_s { surf = data.SurfTag, curve = data.BCurveTag };
        return 0;
    }

    private static int OffsetAskImplementation(SurfTag surface, PK_OFFSET_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetSurfaceSlotByTag(surface);
        if (slot < 0 || Surfaces[slot].Class != SurfaceClass.Offset) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref OffsetDataPool[Surfaces[slot].DataIndex];
        *result = new PK_OFFSET_sf_s { underlying_surface = data.BaseSurfTag, offset_distance = data.Offset };
        return 0;
    }

    private static int SweptAskImplementation(SurfTag surface, PK_SWEPT_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetSurfaceSlotByTag(surface);
        if (slot < 0 || Surfaces[slot].Class != SurfaceClass.Swept) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref SweptDataPool[Surfaces[slot].DataIndex];
        var value = new PK_SWEPT_sf_s { curve = data.SectionCurveTag };
        value.direction.coord[0] = data.Sweep.X;
        value.direction.coord[1] = data.Sweep.Y;
        value.direction.coord[2] = data.Sweep.Z;
        *result = value;
        return 0;
    }

    private static int SpunAskImplementation(SurfTag surface, PK_SPUN_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetSurfaceSlotByTag(surface);
        if (slot < 0 || Surfaces[slot].Class != SurfaceClass.Spun) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref SpunDataPool[Surfaces[slot].DataIndex];
        var value = new PK_SPUN_sf_s { curve = data.ProfileCurveTag };
        WriteVector(&value.axis.location, in data.Base);
        value.axis.axis.coord[0] = data.Axis.X;
        value.axis.axis.coord[1] = data.Axis.Y;
        value.axis.axis.coord[2] = data.Axis.Z;
        *result = value;
        return 0;
    }

    private static int BCurveAskImplementation(CurveTag curve, PK_BCURVE_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetCurveSlotByTag(curve);
        if (slot < 0 || Curves[slot].Class != CurveClass.BCurve) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref BCurveDataStore[Curves[slot].DataIndex];
        var value = new PK_BCURVE_sf_s
        {
            degree = data.Degree, n_vertices = data.NVertices, vertex_dim = data.VertexDim,
            is_rational = data.IsRational, form = data.Form, n_knots = data.NKnots,
            // PK ask clears the constructor's knot classification hint.
            knot_type = ParasolidConstants.PK_knot_unset_c, is_periodic = data.IsPeriodic, is_closed = data.IsClosed,
            self_intersecting = data.SelfIntersecting,
        };
        if (!TryCopyGeometryResult(new ReadOnlySpan<double>(DereferenceBlock(data.VertexBlock), data.NVertices * data.VertexDim), out value.vertex)
            || !TryCopyGeometryResult(new ReadOnlySpan<double>(DereferenceBlock(data.KnotBlock), data.NKnots), out value.knot)
            || !TryCopyGeometryResult(new ReadOnlySpan<int>(DereferenceBlock(data.KnotMultBlock), data.NKnots), out value.knot_mult))
        {
            State.Session->Returns.TryFree(value.vertex);
            State.Session->Returns.TryFree(value.knot);
            State.Session->Returns.TryFree(value.knot_mult);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        // Each result array is a caller-owned return buffer. Publish only
        // after all three copies exist; none aliases the geometry payload.
        *result = value;
        return 0;
    }

    private static int BSurfAskImplementation(SurfTag surface, PK_BSURF_sf_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetSurfaceSlotByTag(surface);
        if (slot < 0 || Surfaces[slot].Class != SurfaceClass.BSurface) return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var data = ref BSurfaceDataStore[Surfaces[slot].DataIndex];
        var value = new PK_BSURF_sf_s
        {
            u_degree = data.UDegree, v_degree = data.VDegree,
            n_u_vertices = data.NUVertices, n_v_vertices = data.NVVertices,
            vertex_dim = data.VertexDim, is_rational = data.IsRational, form = ParasolidConstants.PK_BSURF_form_unset_c,
            n_u_knots = data.NUKnots, n_v_knots = data.NVKnots,
            u_knot_type = ParasolidConstants.PK_knot_unset_c, v_knot_type = ParasolidConstants.PK_knot_unset_c,
            is_u_periodic = data.IsUPeriodic, is_v_periodic = data.IsVPeriodic,
            is_u_closed = data.IsUClosed, is_v_closed = data.IsVClosed,
            self_intersecting = data.SelfIntersecting, convexity = data.Convexity,
        };
        if (!TryCopyGeometryResult<double>(data.PolesSpan(), out value.vertex)
            || !TryCopyGeometryResult<double>(data.UKnotsSpan(), out value.u_knot)
            || !TryCopyGeometryResult<double>(data.VKnotsSpan(), out value.v_knot)
            || !TryCopyGeometryResult<int>(data.UKnotMultsSpan(), out value.u_knot_mult)
            || !TryCopyGeometryResult<int>(data.VKnotMultsSpan(), out value.v_knot_mult))
        {
            State.Session->Returns.TryFree(value.vertex);
            State.Session->Returns.TryFree(value.u_knot);
            State.Session->Returns.TryFree(value.v_knot);
            State.Session->Returns.TryFree(value.u_knot_mult);
            State.Session->Returns.TryFree(value.v_knot_mult);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *result = value;
        return 0;
    }

    private static bool TryCopyGeometryResult<T>(ReadOnlySpan<T> source, out T* result) where T : unmanaged
    {
        result = (T*)State.Session->Returns.TryAllocate((nuint)source.Length * (nuint)sizeof(T));
        if (result == null) return source.IsEmpty;
        source.CopyTo(new Span<T>(result, source.Length));
        return true;
    }

    // Parasolid's default finite representation of an unbounded parameter
    // is the outward-rounded model-space limit (measured against V38).
    private static double NaturalParameterLimit => Math.BitIncrement(1e4);

    private static int CurveAskIntervalImplementation(CurveTag curve, PK_INTERVAL_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetCurveSlotByTag(curve);
        if (slot < 0) return ParasolidConstants.PK_ERROR_unknown_class;
        var error = TryGetNaturalCurveInterval(in Curves[slot], out var lower, out var upper);
        if (error != 0) return error;
        var value = new PK_INTERVAL_s();
        value.value[0] = lower;
        value.value[1] = upper;
        *result = value;
        return 0;
    }

    private static int TryGetNaturalCurveInterval(in CurveRecord curve, out double lower, out double upper)
    {
        lower = curve.TMin;
        upper = curve.TMax;
        switch (curve.Class)
        {
            case CurveClass.Line: lower = -NaturalParameterLimit; upper = NaturalParameterLimit; break;
            case CurveClass.Circle:
            case CurveClass.Ellipse: lower = 0; upper = Math.Tau; break;
            case CurveClass.BCurve:
            {
                var view = GetBCurveView(in BCurveDataStore[curve.DataIndex]);
                lower = view.Start; upper = view.End;
                break;
            }
            case CurveClass.TRCurve:
            {
                ref readonly var trim = ref TrCurveDataPool[curve.DataIndex];
                lower = 0; upper = Math.Abs(trim.Parm2 - trim.Parm1);
                break;
            }
            case CurveClass.SPCurve:
            case CurveClass.ICurve: break;
            default: return ParasolidConstants.PK_ERROR_not_implemented;
        }
        return double.IsFinite(lower) && double.IsFinite(upper) && lower < upper
            ? 0 : ParasolidConstants.PK_ERROR_bad_interval;
    }

    private static int SurfAskUvboxImplementation(SurfTag surface, PK_UVBOX_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var slot = GetSurfaceSlotByTag(surface);
        if (slot < 0) return ParasolidConstants.PK_ERROR_unknown_class;
        var error = TryGetNaturalSurfaceBox(slot, out var u0, out var u1, out var v0, out var v1);
        if (error != 0) return error;
        var value = new PK_UVBOX_s();
        // PK_UVBOX_t stores (u_min, v_min, u_max, v_max).
        value.param[0] = u0; value.param[1] = v0;
        value.param[2] = u1; value.param[3] = v1;
        *result = value;
        return 0;
    }

    private static int TryGetNaturalSurfaceBox(SurfaceSlot slot,
        out double u0, out double u1, out double v0, out double v1)
    {
        u0 = v0 = -NaturalParameterLimit;
        u1 = v1 = NaturalParameterLimit;
        // Offset chains inherit the same UV parameterization. Bound the
        // walk as evaluation does, including malformed/cyclic imported data.
        for (BufferOffset depth = 0; depth <= 8; depth++)
        {
            ref readonly var surface = ref Surfaces[slot];
            switch (surface.Class)
            {
                case SurfaceClass.Plane: return 0;
                case SurfaceClass.Cylinder: u0 = 0; u1 = Math.Tau; return 0;
                case SurfaceClass.Cone:
                {
                    ref readonly var cone = ref ConeDataPool[surface.DataIndex];
                    u0 = 0; u1 = Math.Tau;
                    v0 = -cone.Radius / Math.Tan(cone.SemiAngle);
                    if (v0 != 0) v0 = Math.BitDecrement(v0);
                    return double.IsFinite(v0) ? 0 : ParasolidConstants.PK_ERROR_bad_interval;
                }
                case SurfaceClass.Sphere: u0 = 0; u1 = Math.Tau; v0 = -Math.PI / 2; v1 = Math.PI / 2; return 0;
                case SurfaceClass.Torus: u0 = 0; u1 = Math.Tau; v0 = -Math.PI; v1 = Math.PI; return 0;
                case SurfaceClass.BSurface:
                {
                    var view = GetBSurfaceView(in BSurfaceDataStore[surface.DataIndex]);
                    u0 = view.UStart; u1 = view.UEnd; v0 = view.VStart; v1 = view.VEnd;
                    return 0;
                }
                case SurfaceClass.Swept:
                case SurfaceClass.Spun:
                {
                    var profile = surface.Class == SurfaceClass.Swept
                        ? SweptDataPool[surface.DataIndex].SectionCurveTag
                        : SpunDataPool[surface.DataIndex].ProfileCurveTag;
                    var curveSlot = GetCurveSlotByTag(profile);
                    if (curveSlot < 0) return ParasolidConstants.PK_ERROR_unknown_class;
                    var error = TryGetNaturalCurveInterval(in Curves[curveSlot], out u0, out u1);
                    if (surface.Class == SurfaceClass.Spun) { v0 = 0; v1 = Math.Tau; }
                    return error;
                }
                case SurfaceClass.Offset:
                    slot = GetSurfaceSlotByTag(OffsetDataPool[surface.DataIndex].BaseSurfTag);
                    if (slot < 0) return ParasolidConstants.PK_ERROR_unknown_class;
                    break;
                default: return ParasolidConstants.PK_ERROR_not_implemented;
            }
        }
        return ParasolidConstants.PK_ERROR_not_implemented;
    }
}
