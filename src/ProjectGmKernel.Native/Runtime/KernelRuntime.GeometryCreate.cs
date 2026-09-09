using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Creation of standalone curve/surface geometry that carries no topology:
/// ellipses, trimmed curves, surface parameter curves, B-spline surfaces,
/// offset/swept/spun surfaces. These exist so evaluation (PK_CURVE_eval /
/// PK_SURF_eval) can reach every class the XT schema models. Geometry-to-
/// geometry references (basis, support, section) are validated at creation
/// and resolved lazily at evaluation; ownership wiring for geometric owners
/// is not part of this scope.
/// </summary>
internal static unsafe partial class KernelRuntime
{
    private static int EllipseCreateImplementation(PK_ELLIPSE_sf_s* sf, int* ellipseTag)
    {
        if (ellipseTag != null) *ellipseTag = 0;
        if (sf is null || ellipseTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        if (!double.IsFinite(sf->R1) || !double.IsFinite(sf->R2) || sf->R2 <= 0 || sf->R1 < sf->R2)
            return sf->R1 <= 0 || sf->R2 <= 0 ? ParasolidConstants.PK_ERROR_distance_le_0 : ParasolidConstants.PK_ERROR_bad_parameter;

        ReadAxis2(&sf->basis_set, out double cx, out double cy, out double cz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        int dataSlot = TryAllocateEllipseData();
        if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        ref var data = ref EllipseDataPool[dataSlot];
        data.CenterX = cx; data.CenterY = cy; data.CenterZ = cz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.R1 = sf->R1;
        data.R2 = sf->R2;

        if (!Curves.TryAllocate(out int slot))
        {
            EllipseDataPool.Free(dataSlot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var curve = ref Curves[slot];
        AssignPartition(ref curve.Header, CurrentPartition);
        curve.Class = CurveClass.Ellipse;
        curve.DataIndex = dataSlot;
        curve.TMin = 0;
        curve.TMax = Math.Tau;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        curve.OwnerEdge = -1;
        curve.OwnerCount = 0;
        curve.PrevInBody = curve.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Curve, PoolKind.Curve, slot, curve.Header.Generation);
        if (tag <= 0)
        {
            EllipseDataPool.Free(dataSlot);
            Curves.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *ellipseTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int TrCurveCreateImplementation(PK_TRCURVE_sf_s* sf, int* trCurveTag)
    {
        if (trCurveTag != null) *trCurveTag = 0;
        if (sf is null || trCurveTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var basisSlot = GetCurveSlotByTag(sf->basis_curve);
        if (basisSlot < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;
        var parm1 = sf->t_int.value[0];
        var parm2 = sf->t_int.value[1];
        if (!double.IsFinite(parm1) || !double.IsFinite(parm2) || parm1 == parm2)
            return ParasolidConstants.PK_ERROR_bad_parameter;

        var ownsScratch = EnsureCommandScratch();
        try
        {
            Span<KernelVector3> start = stackalloc KernelVector3[1];
            Span<KernelVector3> end = stackalloc KernelVector3[1];
            ref readonly var basis = ref Curves[basisSlot];
            if (EvaluateCurveCore(in basis, parm1, 0, start, out _) != AlgorithmStatus.Success
                || EvaluateCurveCore(in basis, parm2, 0, end, out _) != AlgorithmStatus.Success)
                return ParasolidConstants.PK_ERROR_eval_failure;

            int dataSlot = TryAllocateTrCurveData();
            if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
            ref var data = ref TrCurveDataPool[dataSlot];
            data.BasisCurveTag = sf->basis_curve;
            data.Point1 = start[0];
            data.Point2 = end[0];
            data.Parm1 = parm1;
            data.Parm2 = parm2;

            if (!Curves.TryAllocate(out int slot))
            {
                TrCurveDataPool.Free(dataSlot);
                return ParasolidConstants.PK_ERROR_memory_full;
            }
            ref var curve = ref Curves[slot];
            AssignPartition(ref curve.Header, CurrentPartition);
            curve.Class = CurveClass.TRCurve;
            curve.DataIndex = dataSlot;
            curve.TMin = Math.Min(parm1, parm2);
            curve.TMax = Math.Max(parm1, parm2);
            // A negatively sensed trimmed curve runs from point_1 to point_2 in
            // decreasing basis parameter; evaluation keeps the basis parameter.
            curve.Sense = parm2 > parm1 ? ParasolidConstants.PK_TOPOL_sense_positive_c : ParasolidConstants.PK_TOPOL_sense_negative_c;
            curve.OwnerEdge = -1;
            curve.OwnerCount = 0;
            curve.PrevInBody = curve.NextInBody = 0;
            var tag = AllocateTag(EntityClass.Curve, PoolKind.Curve, slot, curve.Header.Generation);
            if (tag <= 0)
            {
                TrCurveDataPool.Free(dataSlot);
                Curves.Free(slot);
                return ParasolidConstants.PK_ERROR_memory_full;
            }
            *trCurveTag = tag;
            return ParasolidConstants.PK_ERROR_no_errors;
        }
        finally { if (ownsScratch) ReleaseCommandScratch(State.Session); }
    }

    private static int SpCurveCreateImplementation(PK_SPCURVE_sf_s* sf, int* spCurveTag)
    {
        if (spCurveTag != null) *spCurveTag = 0;
        if (sf is null || spCurveTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var surfaceSlot = GetSurfaceSlotByTag(sf->surf);
        var curveSlot = GetCurveSlotByTag(sf->curve);
        if (surfaceSlot < 0 || curveSlot < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var basis = ref BCurveDataStore[Curves[curveSlot].DataIndex];
        if (Curves[curveSlot].Class != CurveClass.BCurve
            || basis.VertexDim != (basis.IsRational != 0 ? 3 : 2))
            return ParasolidConstants.PK_ERROR_bad_dimension;

        int dataSlot = TryAllocateSpCurveData();
        if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        ref var data = ref SpCurveDataPool[dataSlot];
        data.SurfTag = sf->surf;
        data.BCurveTag = sf->curve;

        if (!Curves.TryAllocate(out int slot))
        {
            SpCurveDataPool.Free(dataSlot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var curve = ref Curves[slot];
        AssignPartition(ref curve.Header, CurrentPartition);
        curve.Class = CurveClass.SPCurve;
        curve.DataIndex = dataSlot;
        var basisView = GetBCurveView(in basis);
        curve.TMin = basisView.Start;
        curve.TMax = basisView.End;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        curve.OwnerEdge = -1;
        curve.OwnerCount = 0;
        curve.PrevInBody = curve.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Curve, PoolKind.Curve, slot, curve.Header.Generation);
        if (tag <= 0)
        {
            SpCurveDataPool.Free(dataSlot);
            Curves.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *spCurveTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int OffsetCreateImplementation(PK_OFFSET_sf_s* sf, int* offsetTag)
    {
        if (offsetTag != null) *offsetTag = 0;
        if (sf is null || offsetTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var baseSlot = GetSurfaceSlotByTag(sf->underlying_surface);
        if (baseSlot < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;
        ref readonly var baseSurface = ref Surfaces[baseSlot];
        if (baseSurface.Class is not (SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Cone
            or SurfaceClass.Sphere or SurfaceClass.Torus or SurfaceClass.BSurface
            or SurfaceClass.Swept or SurfaceClass.Spun or SurfaceClass.Offset))
            return ParasolidConstants.PK_ERROR_not_implemented;
        // The offset distance must not be within linear resolution of zero (XT 5.2.2.8).
        if (!double.IsFinite(sf->offset_distance) || Math.Abs(sf->offset_distance) <= 1e-12)
            return ParasolidConstants.PK_ERROR_bad_value;

        int dataSlot = TryAllocateOffsetData();
        if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        ref var data = ref OffsetDataPool[dataSlot];
        data.BaseSurfTag = sf->underlying_surface;
        data.Offset = sf->offset_distance;
        data.Check = OffsetCheckState.Unchecked;
        data.Scale = 0;

        if (!Surfaces.TryAllocate(out int slot))
        {
            OffsetDataPool.Free(dataSlot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var surface = ref Surfaces[slot];
        AssignPartition(ref surface.Header, CurrentPartition);
        surface.Class = SurfaceClass.Offset;
        surface.DataIndex = dataSlot;
        surface.OwnerFace = -1;
        surface.OwnerCount = 0;
        surface.PrevInBody = surface.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Surface, PoolKind.Surface, slot, surface.Header.Generation);
        if (tag <= 0)
        {
            OffsetDataPool.Free(dataSlot);
            Surfaces.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *offsetTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int SweptCreateImplementation(PK_SWEPT_sf_s* sf, int* sweptTag)
    {
        if (sweptTag != null) *sweptTag = 0;
        if (sf is null || sweptTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var sectionSlot = GetCurveSlotByTag(sf->curve);
        if (sectionSlot < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;
        // XT 5.2.2.10: the section must be analytic or a B-curve.
        if (Curves[sectionSlot].Class is not (CurveClass.Line or CurveClass.Circle or CurveClass.Ellipse or CurveClass.BCurve))
            return ParasolidConstants.PK_ERROR_not_implemented;
        var dx = sf->direction.coord[0];
        var dy = sf->direction.coord[1];
        var dz = sf->direction.coord[2];
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (!double.IsFinite(length) || Math.Abs(length - 1) > 1e-9)
            return ParasolidConstants.PK_ERROR_bad_parameter;

        int dataSlot = TryAllocateSweptData();
        if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        ref var data = ref SweptDataPool[dataSlot];
        data.SectionCurveTag = sf->curve;
        data.Sweep = Vector(dx, dy, dz);
        data.Scale = 0;

        if (!Surfaces.TryAllocate(out int slot))
        {
            SweptDataPool.Free(dataSlot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var surface = ref Surfaces[slot];
        AssignPartition(ref surface.Header, CurrentPartition);
        surface.Class = SurfaceClass.Swept;
        surface.DataIndex = dataSlot;
        surface.OwnerFace = -1;
        surface.OwnerCount = 0;
        surface.PrevInBody = surface.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Surface, PoolKind.Surface, slot, surface.Header.Generation);
        if (tag <= 0)
        {
            SweptDataPool.Free(dataSlot);
            Surfaces.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *sweptTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int SpunCreateImplementation(PK_SPUN_sf_s* sf, int* spunTag)
    {
        if (spunTag != null) *spunTag = 0;
        if (sf is null || spunTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var profileSlot = GetCurveSlotByTag(sf->curve);
        if (profileSlot < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;
        // XT 5.2.2.11: the profile must be analytic or a B-curve.
        if (Curves[profileSlot].Class is not (CurveClass.Line or CurveClass.Circle or CurveClass.Ellipse or CurveClass.BCurve))
            return ParasolidConstants.PK_ERROR_not_implemented;
        var ax = sf->axis.axis.coord[0];
        var ay = sf->axis.axis.coord[1];
        var az = sf->axis.axis.coord[2];
        var length = Math.Sqrt(ax * ax + ay * ay + az * az);
        if (!double.IsFinite(length) || Math.Abs(length - 1) > 1e-9)
            return ParasolidConstants.PK_ERROR_bad_parameter;

        int dataSlot = TryAllocateSpunData();
        if (dataSlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        ref var data = ref SpunDataPool[dataSlot];
        data.ProfileCurveTag = sf->curve;
        data.Base = Vector(sf->axis.location.coord[0], sf->axis.location.coord[1], sf->axis.location.coord[2]);
        data.Axis = Vector(ax, ay, az);
        data.StartParam = 0;
        data.EndParam = 0;
        data.Scale = 0;

        if (!Surfaces.TryAllocate(out int slot))
        {
            SpunDataPool.Free(dataSlot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var surface = ref Surfaces[slot];
        AssignPartition(ref surface.Header, CurrentPartition);
        surface.Class = SurfaceClass.Spun;
        surface.DataIndex = dataSlot;
        surface.OwnerFace = -1;
        surface.OwnerCount = 0;
        surface.PrevInBody = surface.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Surface, PoolKind.Surface, slot, surface.Header.Generation);
        if (tag <= 0)
        {
            SpunDataPool.Free(dataSlot);
            Surfaces.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *spunTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }
    private static int BSurfCreateImplementation(PK_BSURF_sf_s* sf, int* bsurfTag)
    {
        if (bsurfTag != null) *bsurfTag = 0;
        if (sf is null || bsurfTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        if (sf->u_degree < 1 || sf->v_degree < 1 || sf->n_u_vertices <= sf->u_degree || sf->n_v_vertices <= sf->v_degree
            || sf->n_u_knots < 2 || sf->n_v_knots < 2)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (sf->vertex_dim != (sf->is_rational != 0 ? 4 : 3))
            return ParasolidConstants.PK_ERROR_bad_dimension;
        if (sf->vertex is null || sf->u_knot is null || sf->v_knot is null || sf->u_knot_mult is null || sf->v_knot_mult is null)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var workspaceSize = BSurfaceEvaluation.WorkspaceSize(sf->u_degree, sf->v_degree, 10, 10);
        if (workspaceSize == 0 || workspaceSize > MaxEvalWorkspace)
            return ParasolidConstants.PK_ERROR_not_implemented;

        var uExpandedCount = (long)sf->n_u_vertices + sf->u_degree + 1;
        var vExpandedCount = (long)sf->n_v_vertices + sf->v_degree + 1;
        if (ExpandKnots(sf->u_knot, sf->u_knot_mult, sf->n_u_knots, sf->u_degree, uExpandedCount, null) != 0
            || ExpandKnots(sf->v_knot, sf->v_knot_mult, sf->n_v_knots, sf->v_degree, vExpandedCount, null) != 0)
            return ParasolidConstants.PK_ERROR_bad_knots;

        var scalarCount = (long)sf->n_u_vertices * sf->n_v_vertices * sf->vertex_dim;
        for (BufferOffset i = 0; i < scalarCount; i++)
        {
            if (!double.IsFinite(sf->vertex[i]))
                return ParasolidConstants.PK_ERROR_bad_vertex;
            if (sf->is_rational != 0 && i % 4 == 3 && sf->vertex[i] <= 0)
                return ParasolidConstants.PK_ERROR_weight_le_0;
        }

        // Periodic directions repeat their first poles at the far end (pole wrap).
        if (sf->is_u_periodic != 0 && !PolesWrapPeriodically(sf->vertex, sf->n_u_vertices, sf->n_v_vertices, sf->vertex_dim, sf->u_degree, alongU: true))
            return ParasolidConstants.PK_ERROR_periodic_open;
        if (sf->is_v_periodic != 0 && !PolesWrapPeriodically(sf->vertex, sf->n_u_vertices, sf->n_v_vertices, sf->vertex_dim, sf->v_degree, alongU: false))
            return ParasolidConstants.PK_ERROR_periodic_open;

        var session = State.Session;
        int dataIndex;
        if (!BSurfaceDataStore.TryAllocate(out dataIndex))
            return ParasolidConstants.PK_ERROR_memory_full;

        var uExpandedBlock = session->Blocks.TryAllocate((nuint)(uExpandedCount * sizeof(double)));
        var vExpandedBlock = session->Blocks.TryAllocate((nuint)(vExpandedCount * sizeof(double)));
        var vertexBlock = session->Blocks.TryAllocate((nuint)(scalarCount * sizeof(double)));
        var uKnotBlock = session->Blocks.TryAllocate((nuint)((long)sf->n_u_knots * sizeof(double)));
        var vKnotBlock = session->Blocks.TryAllocate((nuint)((long)sf->n_v_knots * sizeof(double)));
        var uKnotMultBlock = session->Blocks.TryAllocate((nuint)((long)sf->n_u_knots * sizeof(int)));
        var vKnotMultBlock = session->Blocks.TryAllocate((nuint)((long)sf->n_v_knots * sizeof(int)));
        if (uExpandedBlock == null || vExpandedBlock == null || vertexBlock == null || uKnotBlock == null
            || vKnotBlock == null || uKnotMultBlock == null || vKnotMultBlock == null)
        {
            if (uExpandedBlock != null) session->Blocks.Free(uExpandedBlock);
            if (vExpandedBlock != null) session->Blocks.Free(vExpandedBlock);
            if (vertexBlock != null) session->Blocks.Free(vertexBlock);
            if (uKnotBlock != null) session->Blocks.Free(uKnotBlock);
            if (vKnotBlock != null) session->Blocks.Free(vKnotBlock);
            if (uKnotMultBlock != null) session->Blocks.Free(uKnotMultBlock);
            if (vKnotMultBlock != null) session->Blocks.Free(vKnotMultBlock);
            BSurfaceDataStore.Free(dataIndex);
            return ParasolidConstants.PK_ERROR_memory_full;
        }

        var expandError = ExpandKnots(sf->u_knot, sf->u_knot_mult, sf->n_u_knots, sf->u_degree, uExpandedCount, uExpandedBlock)
            | ExpandKnots(sf->v_knot, sf->v_knot_mult, sf->n_v_knots, sf->v_degree, vExpandedCount, vExpandedBlock);
        if (expandError != 0)
        {
            session->Blocks.Free(uExpandedBlock);
            session->Blocks.Free(vExpandedBlock);
            session->Blocks.Free(vertexBlock);
            session->Blocks.Free(uKnotBlock);
            session->Blocks.Free(vKnotBlock);
            session->Blocks.Free(uKnotMultBlock);
            session->Blocks.Free(vKnotMultBlock);
            BSurfaceDataStore.Free(dataIndex);
            return ParasolidConstants.PK_ERROR_bad_knots;
        }
        new ReadOnlySpan<double>(sf->vertex, (BufferCount)scalarCount).CopyTo(new Span<double>(vertexBlock, (BufferCount)scalarCount));
        new ReadOnlySpan<double>(sf->u_knot, sf->n_u_knots).CopyTo(new Span<double>(uKnotBlock, sf->n_u_knots));
        new ReadOnlySpan<double>(sf->v_knot, sf->n_v_knots).CopyTo(new Span<double>(vKnotBlock, sf->n_v_knots));
        new ReadOnlySpan<int>(sf->u_knot_mult, sf->n_u_knots).CopyTo(new Span<int>(uKnotMultBlock, sf->n_u_knots));
        new ReadOnlySpan<int>(sf->v_knot_mult, sf->n_v_knots).CopyTo(new Span<int>(vKnotMultBlock, sf->n_v_knots));

        var header = BSurfaceDataStore[dataIndex].Header;   // pool slot header survives the metadata write
        BSurfaceDataStore[dataIndex] = new BSurfaceData
        {
            Header = header,
            UDegree = sf->u_degree,
            VDegree = sf->v_degree,
            NUVertices = sf->n_u_vertices,
            NVVertices = sf->n_v_vertices,
            VertexDim = sf->vertex_dim,
            IsRational = sf->is_rational,
            Form = sf->form,
            NUKnots = sf->n_u_knots,
            NVKnots = sf->n_v_knots,
            UKnotType = sf->u_knot_type,
            VKnotType = sf->v_knot_type,
            IsUPeriodic = sf->is_u_periodic,
            IsVPeriodic = sf->is_v_periodic,
            IsUClosed = (byte)(sf->is_u_closed != 0 || sf->is_u_periodic != 0 ? 1 : 0),
            IsVClosed = (byte)(sf->is_v_closed != 0 || sf->is_v_periodic != 0 ? 1 : 0),
            SelfIntersecting = sf->self_intersecting,
            Convexity = sf->convexity,
            VertexBlock = session->Blocks.HandleOf(vertexBlock),
            UKnotBlock = session->Blocks.HandleOf(uKnotBlock),
            VKnotBlock = session->Blocks.HandleOf(vKnotBlock),
            UKnotMultBlock = session->Blocks.HandleOf(uKnotMultBlock),
            VKnotMultBlock = session->Blocks.HandleOf(vKnotMultBlock),
            UExpandedKnotBlock = session->Blocks.HandleOf(uExpandedBlock),
            VExpandedKnotBlock = session->Blocks.HandleOf(vExpandedBlock),
            UExpandedKnotCount = (BufferCount)uExpandedCount,
            VExpandedKnotCount = (BufferCount)vExpandedCount,
        };
        ref readonly var stored = ref BSurfaceDataStore[dataIndex];
        if (stored.VertexBlock <= 0 || stored.UKnotBlock <= 0 || stored.VKnotBlock <= 0
            || stored.UKnotMultBlock <= 0 || stored.VKnotMultBlock <= 0
            || stored.UExpandedKnotBlock <= 0 || stored.VExpandedKnotBlock <= 0)
        {
            FreeBSurfaceData(dataIndex);
            return ParasolidConstants.PK_ERROR_memory_full;
        }

        if (!Surfaces.TryAllocate(out int slot))
        {
            FreeBSurfaceData(dataIndex);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var record = ref Surfaces[slot];
        AssignPartition(ref record.Header, CurrentPartition);
        record.Class = SurfaceClass.BSurface;
        record.DataIndex = dataIndex;
        var uExpanded = (double*)uExpandedBlock;
        var vExpanded = (double*)vExpandedBlock;
        record.UMin = uExpanded[sf->u_degree];
        record.UMax = uExpanded[sf->n_u_vertices];
        record.VMin = vExpanded[sf->v_degree];
        record.VMax = vExpanded[sf->n_v_vertices];
        record.OwnerFace = -1;
        record.OwnerCount = 0;
        record.PrevInBody = record.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Surface, PoolKind.Surface, slot, record.Header.Generation);
        if (tag <= 0)
        {
            FreeBSurfaceData(dataIndex);
            Surfaces.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *bsurfTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    /// <summary>
    /// Validates the compact knot vector and optionally expands it
    /// (multiplicities) into destination. Returns a PK error code.
    /// </summary>
    private static int ExpandKnots(double* knots, int* mults, int nKnots, int degree, long expandedCount, void* destination)
    {
        long total = 0;
        for (KnotIndex i = 0; i < nKnots; i++)
        {
            if (!double.IsFinite(knots[i]) || (i > 0 && knots[i] <= knots[i - 1])
                || mults[i] < 1 || mults[i] > degree + (i == 0 || i == nKnots - 1 ? 1 : 0))
                return ParasolidConstants.PK_ERROR_bad_knots;
            total += mults[i];
        }
        if (total != expandedCount) return ParasolidConstants.PK_ERROR_wrong_number_knots;
        if (destination == null) return 0;
        var expanded = new Span<double>(destination, (BufferCount)expandedCount);
        BufferOffset offset = 0;
        for (KnotIndex i = 0; i < nKnots; i++)
        {
            expanded.Slice(offset, mults[i]).Fill(knots[i]);
            offset += mults[i];
        }
        return 0;
    }

    /// <summary>
    /// A periodic direction stores one period of distinct poles followed by a
    /// repeat of the first `degree` poles, so evaluation can wrap the seam.
    /// </summary>
    private static bool PolesWrapPeriodically(double* vertex, int nU, int nV, int dim, int degree, bool alongU)
    {
        if (alongU)
        {
            for (var i = 0; i < degree; i++)
            {
                var lead = vertex + (long)i * nV * dim;
                var trail = vertex + (long)(nU - degree + i) * nV * dim;
                for (var k = 0; k < (long)nV * dim; k++)
                    if (Math.Abs(lead[k] - trail[k]) > 1e-12 * (1 + Math.Max(Math.Abs(lead[k]), Math.Abs(trail[k]))))
                        return false;
            }
            return true;
        }
        for (var i = 0; i < nU; i++)
        {
            var row = vertex + (long)i * nV * dim;
            for (var j = 0; j < degree; j++)
            {
                var lead = row + (long)j * dim;
                var trail = row + (long)(nV - degree + j) * dim;
                for (var k = 0; k < dim; k++)
                    if (Math.Abs(lead[k] - trail[k]) > 1e-12 * (1 + Math.Max(Math.Abs(lead[k]), Math.Abs(trail[k]))))
                        return false;
            }
        }
        return true;
    }
}
