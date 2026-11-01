using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Xt;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>
    /// Build an open, one-edge wire from the complete original chart interval.
    /// The caller's curve is attached only after all other allocations succeed.
    /// </summary>
    internal static int CreateICurveWireBody(CurveTag curve, out EntityTag bodyTag)
    {
        bodyTag = 0;
        var curveSlot = GetCurveSlotByTag(curve);
        if (curveSlot < 0 || Curves[curveSlot].Class != CurveClass.ICurve)
            return ParasolidConstants.PK_ERROR_bad_class;
        ref readonly var data = ref ICurveDataPool[Curves[curveSlot].DataIndex];
        var hvecs = HvecSpan(in data);
        var start = data.StartLimit.HvecCount * 3;
        var end = start + (data.ChartCount - 1) * 3;
        var p0 = EvaluationMath.Vector(hvecs[start], hvecs[start + 1], hvecs[start + 2]);
        var p1 = EvaluationMath.Vector(hvecs[end], hvecs[end + 1], hvecs[end + 2]);
        return CreateICurveWireBodyCore(curve, in p0, in p1, 0, 0, 0, out bodyTag);
    }

    private static int CreateICurveWireBodyCore(CurveTag curve, in KernelVector3 start,
        in KernelVector3 end, double edgeTolerance, double startTolerance, double endTolerance,
        out EntityTag bodyTag)
    {
        bodyTag = 0;
        var curveSlot = GetCurveSlotByTag(curve);
        if (curveSlot < 0 || Curves[curveSlot].OwnerCount != 0)
            return ParasolidConstants.PK_ERROR_is_attached;
        int* classes = stackalloc int[5]
        {
            ParasolidConstants.PK_CLASS_edge,
            ParasolidConstants.PK_CLASS_vertex, ParasolidConstants.PK_CLASS_vertex,
            ParasolidConstants.PK_CLASS_fin, ParasolidConstants.PK_CLASS_fin,
        };
        int* parents = stackalloc int[2] { 0, 0 };
        int* children = stackalloc int[2] { 3, 4 };
        var result = new PK_BODY_create_topology_2_r_s();
        var error = BodyCreateTopology2(5, classes, 2, parents, children, null, null, &result);
        if (error != 0) return error;
        var complete = false;
        try
        {
            var bodySlot = TagRec(result.body).Slot;
            ref var body = ref Bodies[bodySlot];
            body.BodyType = ParasolidConstants.PK_BODY_type_wire_c;
            body.BodyConfig = ParasolidConstants.PK_BODY_config_standard_c;
            var edgeSlot = body.FirstEdgeBody;
            var startSlot = body.FirstVertexBody;
            var endSlot = Vertices[startSlot].NextInBody;
            ref var edge = ref Edges[edgeSlot];
            edge.StartVertex = startSlot;
            edge.EndVertex = endSlot;
            edge.Tolerance = edgeTolerance;
            Vertices[startSlot].Tolerance = startTolerance;
            Vertices[endSlot].Tolerance = endTolerance;
            var forward = edge.FirstFinEdge;
            var reverse = edge.LastFinEdge;
            AppendFinToVertex(endSlot, forward);
            AppendFinToVertex(startSlot, reverse);
            // Dummy fins have no loop but remain public topology entities.
            Fins[forward].Header.Tag = AllocateTag(EntityClass.Fin, PoolKind.Fin, forward, Fins[forward].Header.Generation);
            Fins[reverse].Header.Tag = AllocateTag(EntityClass.Fin, PoolKind.Fin, reverse, Fins[reverse].Header.Generation);
            if (Fins[forward].Header.Tag <= 0 || Fins[reverse].Header.Tag <= 0)
                return ParasolidConstants.PK_ERROR_memory_full;

            error = AttachWirePoint(startSlot, in start);
            if (error != 0) return error;
            error = AttachWirePoint(endSlot, in end);
            if (error != 0) return error;
            var edgeTag = TagOf(PoolKind.Edge, edgeSlot);
            error = EdgeAttachCurves(1, &edgeTag, &curve);
            if (error != 0) return error;
            bodyTag = result.body;
            complete = true;
            return ParasolidConstants.PK_ERROR_no_errors;
        }
        finally
        {
            if (!complete) EntityDelete(1, &result.body);
        }
    }

    private static int AttachWirePoint(VertexSlot vertexSlot, in KernelVector3 position)
    {
        var definition = new PK_POINT_sf_s();
        definition.position.coord[0] = position.X;
        definition.position.coord[1] = position.Y;
        definition.position.coord[2] = position.Z;
        PointTag point = 0;
        var error = PointCreate(&definition, &point);
        if (error != 0) return error;
        var vertex = TagOf(PoolKind.Vertex, vertexSlot);
        error = VertexAttachPoints(1, &vertex, &point);
        if (error != 0) EntityDelete(1, &point);
        return error;
    }

    /// <summary>
    /// Materialize the supported public receive graph: an open wire with one
    /// INTERSECTION edge, two vertices/points and two dummy halfedges. Other
    /// topology remains on the existing opaque receive path.
    /// </summary>
    internal static AlgorithmStatus TryMaterializeICurveWireBodyFromXt(XtDocument document,
        XtNode body, out EntityTag bodyTag)
    {
        bodyTag = 0;
        if (!HasCanonicalICurveWireBodyLayout(document, body)
            || body.Fields[17].Kind != XtFieldKind.Unsigned || body.Fields[17].Integer != 2)
            return AlgorithmStatus.Unsupported;
        ReadOnlySpan<BufferOffset> unsupportedLinks = [3, 4, 5, 6, 7, 8, 20, 24, 25, 33];
        foreach (var index in unsupportedLinks)
        {
            if (body.Fields[index].Kind != XtFieldKind.Pointer) return AlgorithmStatus.InvalidInput;
            if (body.Fields[index].Pointer != 0) return AlgorithmStatus.Unsupported;
        }
        if (body.Fields[19].Pointer != 0 || body.Fields[26].Pointer != 0)
            return AlgorithmStatus.Unsupported;
        var edge = FindNodeByIndex(document.Nodes, body.Fields[27].Pointer);
        if (edge is null || edge.Type != (int)XtNodeTypes.Edge || edge.Fields.Length < 10)
            return AlgorithmStatus.InvalidInput;
        if (edge.Fields[9].Pointer != body.Index
            || (edge.Fields[4].Pointer != 0 && edge.Fields[4].Pointer != edge.Index))
            return AlgorithmStatus.InvalidInput;
        if (edge.Fields[5].Pointer != 0 && edge.Fields[5].Pointer != edge.Index)
            return AlgorithmStatus.Unsupported;
        var intersection = FindNodeByIndex(document.Nodes, edge.Fields[6].Pointer);
        if (intersection is null || intersection.Type != (int)XtNodeTypes.Intersection)
            return AlgorithmStatus.Unsupported;

        var forward = FindNodeByIndex(document.Nodes, edge.Fields[3].Pointer);
        if (!IsWireFin(forward, edge.Index, '+')) return AlgorithmStatus.Unsupported;
        var reverse = FindNodeByIndex(document.Nodes, forward!.Fields[5].Pointer);
        if (!IsWireFin(reverse, edge.Index, '-') || reverse!.Fields[5].Pointer != forward.Index)
            return AlgorithmStatus.Unsupported;
        if (!IsLocalWireLink(forward.Fields[2].Pointer, forward.Index, reverse.Index)
            || !IsLocalWireLink(forward.Fields[3].Pointer, forward.Index, reverse.Index)
            || !IsLocalWireLink(reverse.Fields[2].Pointer, forward.Index, reverse.Index)
            || !IsLocalWireLink(reverse.Fields[3].Pointer, forward.Index, reverse.Index))
            return AlgorithmStatus.Unsupported;
        var startIndex = reverse.Fields[4].Pointer;
        var endIndex = forward.Fields[4].Pointer;
        // Closed and vertexless wires are valid XT outside this open-edge
        // materializer. Preserve their existing opaque receive behavior.
        if (startIndex == 0 && endIndex == 0) return AlgorithmStatus.Unsupported;
        var startVertex = FindNodeByIndex(document.Nodes, startIndex);
        var endVertex = FindNodeByIndex(document.Nodes, endIndex);
        if (startVertex is null || startVertex.Type != (int)XtNodeTypes.Vertex
            || endVertex is null || endVertex.Type != (int)XtNodeTypes.Vertex)
            return AlgorithmStatus.InvalidInput;
        if (startVertex.Index == endVertex.Index) return AlgorithmStatus.Unsupported;
        if (!TryReadWireVertex(document, startVertex, out var start, out var startTolerance)
            || !TryReadWireVertex(document, endVertex, out var end, out var endTolerance)
            || !TryReadWireTolerance(edge.Fields[2], out var edgeTolerance))
            return AlgorithmStatus.InvalidInput;
        if (startVertex.Fields[7].Pointer != body.Index || endVertex.Fields[7].Pointer != body.Index
            || startVertex.Fields[2].Pointer != reverse.Index || endVertex.Fields[2].Pointer != forward.Index
            || (forward.Fields[8].Pointer != 0 && forward.Fields[8].Pointer != forward.Index)
            || (reverse.Fields[8].Pointer != 0 && reverse.Fields[8].Pointer != reverse.Index))
            return AlgorithmStatus.InvalidInput;
        var firstVertex = body.Fields[28].Pointer;
        if (firstVertex != startVertex.Index && firstVertex != endVertex.Index)
            return AlgorithmStatus.InvalidInput;
        var first = firstVertex == startVertex.Index ? startVertex : endVertex;
        var second = firstVertex == startVertex.Index ? endVertex : startVertex;
        if (first.Fields[4].Pointer != second.Index
            || (second.Fields[4].Pointer != 0 && second.Fields[4].Pointer != first.Index))
            return AlgorithmStatus.Unsupported;

        var status = TryMaterializeICurveFromXt(document, intersection.Index, out var curve, out _);
        if (status != AlgorithmStatus.Success) return status;
        var error = CreateICurveWireBodyCore(curve, in start, in end,
            edgeTolerance, startTolerance, endTolerance, out bodyTag);
        if (error != ParasolidConstants.PK_ERROR_no_errors)
        {
            EntityDelete(1, &curve);
            return error == ParasolidConstants.PK_ERROR_memory_full || error < 0
                ? AlgorithmStatus.WorkspaceTooSmall : AlgorithmStatus.InvalidInput;
        }
        var bodySlot = TagRec(bodyTag).Slot;
        var edgeSlot = Bodies[bodySlot].FirstEdgeBody;
        var forwardSlot = Edges[edgeSlot].FirstFinEdge;
        var reverseSlot = Edges[edgeSlot].LastFinEdge;
        Fins[forwardSlot].NextInLoop = WireFinSlot(forward.Fields[2].Pointer, forward.Index, reverse.Index, forwardSlot, reverseSlot);
        Fins[forwardSlot].PrevInLoop = WireFinSlot(forward.Fields[3].Pointer, forward.Index, reverse.Index, forwardSlot, reverseSlot);
        Fins[reverseSlot].NextInLoop = WireFinSlot(reverse.Fields[2].Pointer, forward.Index, reverse.Index, forwardSlot, reverseSlot);
        Fins[reverseSlot].PrevInLoop = WireFinSlot(reverse.Fields[3].Pointer, forward.Index, reverse.Index, forwardSlot, reverseSlot);
        return AlgorithmStatus.Success;
    }

    internal static bool HasCanonicalICurveWireBodyLayout(XtDocument document, XtNode body)
        => body.Type == (int)XtNodeTypes.Body && body.Fields.Length == 36
            && document.Schema.ModelerVersion / 100000 is >= 34 and <= 38;

    private static bool IsWireFin(XtNode? node, XtNodeIndex edge, char sense)
        => node is not null && node.Type == (int)XtNodeTypes.Halfedge && node.Fields.Length >= 10
            && node.Fields[1].Pointer == 0 && node.Fields[6].Pointer == edge
            && node.Fields[7].Pointer == 0 && node.Fields[9].Kind == XtFieldKind.Character
            && node.Fields[9].Character == sense;

    private static bool IsLocalWireLink(XtNodeIndex index, XtNodeIndex forward, XtNodeIndex reverse)
        => index == 0 || index == forward || index == reverse;

    private static FinSlot WireFinSlot(XtNodeIndex index, XtNodeIndex forward, XtNodeIndex reverse,
        FinSlot forwardSlot, FinSlot reverseSlot)
        => index == forward ? forwardSlot : index == reverse ? reverseSlot : -1;

    private static bool TryReadWireVertex(XtDocument document, XtNode? vertex,
        out KernelVector3 point, out double tolerance)
    {
        point = default;
        tolerance = 0;
        if (vertex is null || vertex.Type != (int)XtNodeTypes.Vertex || vertex.Fields.Length < 8
            || !TryReadWireTolerance(vertex.Fields[6], out tolerance)) return false;
        var node = FindNodeByIndex(document.Nodes, vertex.Fields[5].Pointer);
        if (node is null || node.Type != (int)XtNodeTypes.Point || node.Fields.Length < 6
            || node.Fields[5].Kind != XtFieldKind.Vector) return false;
        var vector = node.Fields[5].Vector;
        point = EvaluationMath.Vector(vector.X, vector.Y, vector.Z);
        return EvaluationMath.IsFinite(point);
    }

    private static bool TryReadWireTolerance(XtFieldValue value, out double tolerance)
    {
        tolerance = value.Kind == XtFieldKind.Empty ? 0 : value.Real;
        return (value.Kind == XtFieldKind.Empty || value.Kind == XtFieldKind.Real)
            && double.IsFinite(tolerance) && tolerance >= 0;
    }
}
