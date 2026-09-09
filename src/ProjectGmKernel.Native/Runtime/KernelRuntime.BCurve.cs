using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // B-curve metadata lives in a paged pool; pole/knot payloads live in
    // independently released variable-length blocks owned by the record.
    internal static PagedEntityPool<BCurveData> BCurveDataStore = default;

    private static Span<double> CommandScratchBorrow(int doubles)
        => CommandScratch.Current.Take(doubles);

    private static void CommandScratchReturn(Span<double> workspace)
        => CommandScratch.Current.Return(workspace);
    // Evaluation workspace comes from the command scratch arena; the shared
    // static array is gone.
    internal const int MaxEvalWorkspace = 1 << 15;   // doubles; scratch segment is 256 KiB

    private static void ResetBCurves()
    {
        BCurveDataStore.Dispose();
    }

    internal static BCurveView GetBCurveView(in BCurveData data) => new(
        data.Degree, data.VertexDim, data.IsRational != 0, data.IsPeriodic != 0,
        new ReadOnlySpan<double>(DereferenceBlock(data.VertexBlock), (BufferCount)(data.NVertices * data.VertexDim)),
        new ReadOnlySpan<double>(DereferenceBlock(data.ExpandedKnotBlock), data.ExpandedKnotCount));

    internal static BSurfaceView GetBSurfaceView(in BSurfaceData data) => new(
        data.UDegree, data.VDegree, data.NUVertices, data.NVVertices, data.VertexDim,
        data.IsRational != 0, data.IsUPeriodic != 0, data.IsVPeriodic != 0,
        new ReadOnlySpan<double>(DereferenceBlock(data.VertexBlock), (BufferCount)(data.NUVertices * data.NVVertices * data.VertexDim)),
        new ReadOnlySpan<double>(DereferenceBlock(data.UExpandedKnotBlock), data.UExpandedKnotCount),
        new ReadOnlySpan<double>(DereferenceBlock(data.VExpandedKnotBlock), data.VExpandedKnotCount));

    internal static void* DereferenceBlock(DataSlot blockHandle)
        => State.Session == null ? null : State.Session->Blocks.BlockPointer((int)blockHandle);



    private static int BCurveCreateImplementation(PK_BCURVE_sf_s* sf, CurveTag* curve)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var ownsScratch = EnsureCommandScratch();
        try { return BCurveCreateCore(sf, curve); }
        finally { if (ownsScratch) ReleaseCommandScratch(State.Session); }
    }

    private static int BCurveCreateCore(PK_BCURVE_sf_s* sf, CurveTag* curve)
    {
        if (sf is null || curve is null || sf->vertex is null || sf->knot is null || sf->knot_mult is null)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (sf->degree < 1 || sf->n_vertices <= sf->degree || sf->n_knots < 2)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        // Planar (u, v) B-curves feed SP-curves: plain dimension 2 or rational dimension 3.
        var planar = sf->vertex_dim == (sf->is_rational != 0 ? 3 : 2);
        if (sf->vertex_dim != (sf->is_rational != 0 ? 4 : 3) && !planar)
            return ParasolidConstants.PK_ERROR_bad_dimension;
        if (sf->form < ParasolidConstants.PK_BCURVE_form_unset_c || sf->form > ParasolidConstants.PK_BCURVE_form_hyperbolic_c
            || sf->knot_type < ParasolidConstants.PK_knot_unset_c || sf->knot_type > ParasolidConstants.PK_knot_smooth_seam_c
            || sf->self_intersecting < ParasolidConstants.PK_self_intersect_unset_c || sf->self_intersecting > ParasolidConstants.PK_self_intersect_true_c)
            return ParasolidConstants.PK_ERROR_bad_parameter;

        var session = State.Session;
        var workspaceSize = BCurveEvaluation.WorkspaceSize(sf->degree, 10);
        if (workspaceSize == 0 || workspaceSize > MaxEvalWorkspace)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var scalarCount = (long)sf->n_vertices * sf->vertex_dim;
        var expandedCount = (long)sf->n_vertices + sf->degree + 1;

        long total = 0;
        for (KnotIndex i = 0; i < sf->n_knots; i++)
        {
            if (!double.IsFinite(sf->knot[i]) || (i > 0 && sf->knot[i] <= sf->knot[i - 1])
                || sf->knot_mult[i] < 1 || sf->knot_mult[i] > sf->degree + (i == 0 || i == sf->n_knots - 1 ? 1 : 0))
                return ParasolidConstants.PK_ERROR_bad_knots;
            total += sf->knot_mult[i];
        }
        if (total != expandedCount) return ParasolidConstants.PK_ERROR_wrong_number_knots;
        for (BufferOffset i = 0; i < scalarCount; i++)
        {
            if (!double.IsFinite(sf->vertex[i]))
                return ParasolidConstants.PK_ERROR_bad_vertex;
            if (sf->is_rational != 0 && i % sf->vertex_dim == sf->vertex_dim - 1 && sf->vertex[i] <= 0)
                return ParasolidConstants.PK_ERROR_weight_le_0;
        }

        // Allocate the metadata slot first; on failure nothing is left behind.
        int dataIndex;
        if (!BCurveDataStore.TryAllocate(out dataIndex))
            return ParasolidConstants.PK_ERROR_memory_full;

        // Payload blocks (expanded knots built first for validation).
        var expandedBlock = session->Blocks.TryAllocate((nuint)(expandedCount * sizeof(double)));
        var vertexBlock = session->Blocks.TryAllocate((nuint)(scalarCount * sizeof(double)));
        var knotBlock = session->Blocks.TryAllocate((nuint)((long)sf->n_knots * sizeof(double)));
        var knotMultBlock = session->Blocks.TryAllocate((nuint)((long)sf->n_knots * sizeof(int)));
        if (expandedBlock == null || vertexBlock == null || knotBlock == null || knotMultBlock == null)
        {
            if (expandedBlock != null) session->Blocks.Free(expandedBlock);
            if (vertexBlock != null) session->Blocks.Free(vertexBlock);
            if (knotBlock != null) session->Blocks.Free(knotBlock);
            if (knotMultBlock != null) session->Blocks.Free(knotMultBlock);
            BCurveDataStore.Free(dataIndex);
            return ParasolidConstants.PK_ERROR_memory_full;
        }

        var expanded = new Span<double>(expandedBlock, (BufferCount)expandedCount);
        BufferOffset offset = 0;
        for (KnotIndex i = 0; i < sf->n_knots; i++)
        {
            expanded.Slice(offset, sf->knot_mult[i]).Fill(sf->knot[i]);
            offset += sf->knot_mult[i];
        }
        if (!(expanded[sf->degree] < expanded[sf->n_vertices]))
        {
            ReleaseBCurveBlocks(dataIndex, expandedBlock, vertexBlock, knotBlock, knotMultBlock);
            return ParasolidConstants.PK_ERROR_bad_knots;
        }
        if (sf->is_closed != 0 || sf->is_periodic != 0)
        {
            if (planar)
                return ParasolidConstants.PK_ERROR_bad_parameter;
            var workspace = CommandScratchBorrow(workspaceSize);
            var view = new BCurveView(sf->degree, sf->vertex_dim, sf->is_rational != 0, false,
                new ReadOnlySpan<double>(sf->vertex, (BufferCount)scalarCount), expanded);
            Span<KernelVector3> first = stackalloc KernelVector3[2];
            Span<KernelVector3> last = stackalloc KernelVector3[2];
            var startStatus = BCurveEvaluation.Evaluate(in view, view.Start, 1, first, workspace, out var startTangent);
            var endStatus = BCurveEvaluation.Evaluate(in view, view.End, 1, last, workspace, out var endTangent);
            CommandScratchReturn(workspace);
            if (startStatus != AlgorithmStatus.Success || endStatus != AlgorithmStatus.Success)
            {
                ReleaseBCurveBlocks(dataIndex, expandedBlock, vertexBlock, knotBlock, knotMultBlock);
                return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            var dx = first[0].X - last[0].X;
            var dy = first[0].Y - last[0].Y;
            var dz = first[0].Z - last[0].Z;
            var error = ParasolidConstants.PK_ERROR_no_errors;
            if (dx * dx + dy * dy + dz * dz > 1e-16)
                error = sf->is_periodic != 0 ? ParasolidConstants.PK_ERROR_periodic_open : ParasolidConstants.PK_ERROR_bad_parameter;
            else if (sf->is_periodic != 0)
            {
                dx = startTangent.X - endTangent.X;
                dy = startTangent.Y - endTangent.Y;
                dz = startTangent.Z - endTangent.Z;
                if (dx * dx + dy * dy + dz * dz > 1e-22
                    || startTangent.X == 0 && startTangent.Y == 0 && startTangent.Z == 0
                    || endTangent.X == 0 && endTangent.Y == 0 && endTangent.Z == 0)
                    error = ParasolidConstants.PK_ERROR_periodic_not_smooth;
            }
            if (error != 0)
            {
                ReleaseBCurveBlocks(dataIndex, expandedBlock, vertexBlock, knotBlock, knotMultBlock);
                return error;
            }
        }

        new ReadOnlySpan<double>(sf->vertex, (BufferCount)scalarCount).CopyTo(new Span<double>(vertexBlock, (BufferCount)scalarCount));
        new ReadOnlySpan<double>(sf->knot, sf->n_knots).CopyTo(new Span<double>(knotBlock, sf->n_knots));
        new ReadOnlySpan<int>(sf->knot_mult, sf->n_knots).CopyTo(new Span<int>(knotMultBlock, sf->n_knots));

        var header = BCurveDataStore[dataIndex].Header;   // pool slot header survives the metadata write
        BCurveDataStore[dataIndex] = new BCurveData
        {
            Header = header,
            Degree = sf->degree,
            NVertices = sf->n_vertices,
            VertexDim = sf->vertex_dim,
            IsRational = sf->is_rational,
            IsPeriodic = sf->is_periodic,
            IsClosed = (byte)(sf->is_closed != 0 || sf->is_periodic != 0 ? 1 : 0),
            Form = sf->form,
            KnotType = sf->knot_type,
            SelfIntersecting = sf->self_intersecting,
            NKnots = sf->n_knots,
            VertexBlock = session->Blocks.HandleOf(vertexBlock),
            KnotBlock = session->Blocks.HandleOf(knotBlock),
            KnotMultBlock = session->Blocks.HandleOf(knotMultBlock),
            ExpandedKnotBlock = session->Blocks.HandleOf(expandedBlock),
            ExpandedKnotCount = (BufferCount)expandedCount,
        };
        ref readonly var stored = ref BCurveDataStore[dataIndex];
        if (stored.VertexBlock <= 0 || stored.KnotBlock <= 0 || stored.KnotMultBlock <= 0 || stored.ExpandedKnotBlock <= 0)
        {
            ReleaseBCurveBlocks(dataIndex, expandedBlock, vertexBlock, knotBlock, knotMultBlock);
            return ParasolidConstants.PK_ERROR_memory_full;
        }

        if (!Curves.TryAllocate(out int slot))
        {
            ReleaseBCurveBlocks(dataIndex, expandedBlock, vertexBlock, knotBlock, knotMultBlock);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        ref var record = ref Curves[slot];
        AssignPartition(ref record.Header, CurrentPartition);
        record.Class = CurveClass.BCurve;
        record.DataIndex = dataIndex;
        record.TMin = expanded[sf->degree];
        record.TMax = expanded[sf->n_vertices];
        record.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        record.OwnerEdge = -1;
        record.OwnerCount = 0;
        record.PrevInBody = record.NextInBody = 0;
        var tag = AllocateTag(EntityClass.Curve, PoolKind.Curve, slot, record.Header.Generation);
        if (tag <= 0)
        {
            FreeBCurveData(dataIndex);
            Curves.Free(slot);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        *curve = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static void ReleaseBCurveBlocks(int dataIndex, void* expandedBlock, void* vertexBlock, void* knotBlock, void* knotMultBlock)
    {
        var blocks = &State.Session->Blocks;
        blocks->Free(expandedBlock);
        blocks->Free(vertexBlock);
        blocks->Free(knotBlock);
        blocks->Free(knotMultBlock);
        BCurveDataStore.Free(dataIndex);
    }

    internal static void FreeBCurveData(int dataIndex)
    {
        var blocks = &State.Session->Blocks;
        ref var data = ref BCurveDataStore[dataIndex];
        blocks->Free(DereferenceBlock(data.VertexBlock));
        blocks->Free(DereferenceBlock(data.KnotBlock));
        blocks->Free(DereferenceBlock(data.KnotMultBlock));
        blocks->Free(DereferenceBlock(data.ExpandedKnotBlock));
        BCurveDataStore.Free(dataIndex);
    }

    internal static void FreeBSurfaceData(int dataIndex)
    {
        var blocks = &State.Session->Blocks;
        ref var data = ref BSurfaceDataStore[dataIndex];
        blocks->Free(DereferenceBlock(data.VertexBlock));
        blocks->Free(DereferenceBlock(data.UKnotBlock));
        blocks->Free(DereferenceBlock(data.VKnotBlock));
        blocks->Free(DereferenceBlock(data.UKnotMultBlock));
        blocks->Free(DereferenceBlock(data.VKnotMultBlock));
        blocks->Free(DereferenceBlock(data.UExpandedKnotBlock));
        blocks->Free(DereferenceBlock(data.VExpandedKnotBlock));
        BSurfaceDataStore.Free(dataIndex);
    }

    private static void RollbackBCurveBlock(int slot, void* oldBlock)
    {
        // Block swap undo: the owner record keeps its handle; the snapshot in
        // the undo entry restores the payload contents.
        // Handled generically through FieldSnapshot of the owner record today.
    }
}
