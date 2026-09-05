using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal static Arena<BCurveData> BCurveDataStore = new(MaxCurves);
    internal static Arena<double> BCurveVertices = new(1 << 20);
    internal static Arena<double> BCurveKnots = new(1 << 18);
    internal static Arena<int> BCurveKnotMults = new(1 << 18);
    internal static Arena<double> BCurveExpandedKnots = new(1 << 19);
    // Protected by RuntimeLock; numerical evaluators receive a caller-owned workspace instead.
    private static readonly double[] BCurveWorkspace = new double[1 << 16];

    private static void ResetBCurves()
    {
        BCurveDataStore.Reset();
        BCurveVertices.Reset();
        BCurveKnots.Reset();
        BCurveKnotMults.Reset();
        BCurveExpandedKnots.Reset();
    }

    internal static BCurveView GetBCurveView(in BCurveData data) => new(
        data.Degree, data.VertexDim, data.IsRational != 0, data.IsPeriodic != 0,
        BCurveVertices.AsSpan(data.VertexOffset, data.NVertices * data.VertexDim),
        BCurveExpandedKnots.AsSpan(data.ExpandedKnotOffset, data.ExpandedKnotCount));

    public static int BCurveCreate(PK_BCURVE_sf_s* sf, CurveTag* curve)
    {
        using var scope = RuntimeLock.EnterScope();
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (sf is null || curve is null || sf->vertex is null || sf->knot is null || sf->knot_mult is null)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (sf->degree < 1 || sf->n_vertices <= sf->degree || sf->n_knots < 2)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (sf->vertex_dim != (sf->is_rational != 0 ? 4 : 3))
            return ParasolidConstants.PK_ERROR_bad_dimension;
        if (sf->form < ParasolidConstants.PK_BCURVE_form_unset_c || sf->form > ParasolidConstants.PK_BCURVE_form_hyperbolic_c
            || sf->knot_type < ParasolidConstants.PK_knot_unset_c || sf->knot_type > ParasolidConstants.PK_knot_smooth_seam_c
            || sf->self_intersecting < ParasolidConstants.PK_self_intersect_unset_c || sf->self_intersecting > ParasolidConstants.PK_self_intersect_true_c)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var workspaceSize = BCurveEvaluation.WorkspaceSize(sf->degree, 10);
        if (workspaceSize == 0 || workspaceSize > BCurveWorkspace.Length)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var scalarCount = (long)sf->n_vertices * sf->vertex_dim;
        var expandedCount = (long)sf->n_vertices + sf->degree + 1;
        if (scalarCount > BCurveVertices.Capacity - BCurveVertices.Count
            || expandedCount > BCurveExpandedKnots.Capacity - BCurveExpandedKnots.Count
            || sf->n_knots > BCurveKnots.Capacity - BCurveKnots.Count
            || sf->n_knots > BCurveKnotMults.Capacity - BCurveKnotMults.Count
            || BCurveDataStore.Count == BCurveDataStore.Capacity || !Curves.CanAllocate
            || nextTag >= MaxHandles)
            return ParasolidConstants.PK_ERROR_memory_full;

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
            if (sf->is_rational != 0 && i % 4 == 3 && sf->vertex[i] <= 0)
                return ParasolidConstants.PK_ERROR_weight_le_0;
        }

        var expandedMark = BCurveExpandedKnots.SaveMark();
        var expandedOffset = BCurveExpandedKnots.Allocate((BufferCount)expandedCount);
        var expanded = BCurveExpandedKnots.AsSpan(expandedOffset, (BufferCount)expandedCount);
        BufferOffset offset = 0;
        for (KnotIndex i = 0; i < sf->n_knots; i++)
        {
            expanded.Slice(offset, sf->knot_mult[i]).Fill(sf->knot[i]);
            offset += sf->knot_mult[i];
        }
        if (!(expanded[sf->degree] < expanded[sf->n_vertices]))
        {
            BCurveExpandedKnots.RestoreMark(expandedMark);
            return ParasolidConstants.PK_ERROR_bad_knots;
        }
        if (sf->is_closed != 0 || sf->is_periodic != 0)
        {
            var view = new BCurveView(sf->degree, sf->vertex_dim, sf->is_rational != 0, false,
                new ReadOnlySpan<double>(sf->vertex, (BufferCount)scalarCount), expanded);
            Span<KernelVector3> first = stackalloc KernelVector3[2];
            Span<KernelVector3> last = stackalloc KernelVector3[2];
            var startStatus = BCurveEvaluation.Evaluate(in view, view.Start, 1, first, BCurveWorkspace, out var startTangent);
            var endStatus = BCurveEvaluation.Evaluate(in view, view.End, 1, last, BCurveWorkspace, out var endTangent);
            if (startStatus != AlgorithmStatus.Success || endStatus != AlgorithmStatus.Success)
            {
                BCurveExpandedKnots.RestoreMark(expandedMark);
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
                BCurveExpandedKnots.RestoreMark(expandedMark);
                return error;
            }
        }

        var dataIndex = BCurveDataStore.Allocate();
        ref var data = ref BCurveDataStore[dataIndex];
        data = new BCurveData
        {
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
            VertexOffset = BCurveVertices.Allocate((BufferCount)scalarCount),
            KnotOffset = BCurveKnots.Allocate(sf->n_knots),
            KnotMultOffset = BCurveKnotMults.Allocate(sf->n_knots),
            ExpandedKnotOffset = expandedOffset,
            ExpandedKnotCount = (BufferCount)expandedCount,
        };
        new ReadOnlySpan<double>(sf->vertex, (BufferCount)scalarCount).CopyTo(BCurveVertices.AsSpan(data.VertexOffset, (BufferCount)scalarCount));
        new ReadOnlySpan<double>(sf->knot, sf->n_knots).CopyTo(BCurveKnots.AsSpan(data.KnotOffset, data.NKnots));
        new ReadOnlySpan<int>(sf->knot_mult, sf->n_knots).CopyTo(BCurveKnotMults.AsSpan(data.KnotMultOffset, data.NKnots));
        var slot = Curves.Allocate();
        ref var record = ref Curves[slot];
        AssignPartition(ref record.Header, CurrentPartition);
        record.Class = CurveClass.BCurve;
        record.DataIndex = dataIndex;
        record.TMin = expanded[sf->degree];
        record.TMax = expanded[sf->n_vertices];
        record.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        record.OwnerEdge = -1;
        record.PrevInBody = record.NextInBody = 0;
        *curve = AllocateTag(EntityClass.Curve, PoolKind.Curve, slot, record.Header.Generation);
        return ParasolidConstants.PK_ERROR_no_errors;
    }
}
