using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // The managed reader validates and indexes the graph before this pool-only
    // adapter runs. Geometry is created in dependency order, without fitting a
    // primitive to the topology or changing the source coordinate system.
    internal static AlgorithmStatus MaterializeXtBrep(XtDocument document, XtNode root,
        ReadOnlySpan<XtNode> topology, Span<int> slots,
        ReadOnlySpan<XtNode> geometry, Span<int> tags,
        ReadOnlySpan<XtNode> construction, out EntityTag bodyTag)
    {
        bodyTag = 0;
        tags.Clear();
        for (var i = 0; i < geometry.Length; i++)
        {
            var status = MaterializeXtBrepGeometry(document, geometry[i], geometry, tags, out var tag);
            if (status != AlgorithmStatus.Success)
            {
                // Unsupported geometry takes the opaque path in the same
                // successful receive transaction, so its temporary entities
                // must be explicitly retired rather than relying on rollback.
                if (status == AlgorithmStatus.Unsupported)
                    for (var j = i - 1; j >= 0; j--)
                    {
                        var created = tags[j];
                        if (IsValidTag(created)) EntityDelete(1, &created);
                    }
                return status;
            }
            tags[i] = tag;
        }

        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return AlgorithmStatus.WorkspaceTooSmall;
        InitializeBody(ref Bodies[bodySlot]);
        AssignPartition(ref Bodies[bodySlot].Header, CurrentPartition);
        Bodies[bodySlot].BodyType = root.Fields[17].Integer switch
        {
            1 => ParasolidConstants.PK_BODY_type_solid_c,
            2 => ParasolidConstants.PK_BODY_type_wire_c,
            3 => ParasolidConstants.PK_BODY_type_sheet_c,
            _ => ParasolidConstants.PK_BODY_type_general_c,
        };
        for (var i = 0; i < topology.Length; i++)
        {
            slots[i] = AllocateXtTopology((XtNodeTypes)topology[i].Type);
            if (slots[i] < 0) return AlgorithmStatus.WorkspaceTooSmall;
        }

        for (var i = 0; i < topology.Length; i++)
        {
            var node = topology[i];
            var slot = slots[i];
            switch ((XtNodeTypes)node.Type)
            {
                case XtNodeTypes.Region:
                    Regions[slot].IsSolid = (byte)(node.Fields[7].Character == 'S' ? 1 : 0);
                    AppendRegionToBody(bodySlot, slot);
                    break;
                case XtNodeTypes.Shell:
                    AppendShellToBody(bodySlot, slot);
                    AppendShellToRegion(XtMapped(topology, slots, node.Fields[7].Pointer), slot);
                    break;
                case XtNodeTypes.Face:
                    Faces[slot].SurfTag = XtMapped(geometry, tags, node.Fields[7].Pointer, 0);
                    Faces[slot].Orientation = XtSense(node.Fields[8].Character);
                    Faces[slot].Tolerance = XtTolerance(node.Fields[2]);
                    AppendFaceToBody(bodySlot, slot);
                    var back = XtMapped(topology, slots, node.Fields[6].Pointer);
                    var front = XtMapped(topology, slots, node.Fields[13].Pointer);
                    if ((back >= 0 && AddFaceUse(back, slot, ParasolidConstants.PK_TOPOL_sense_negative_c) < 0)
                        || (front >= 0 && AddFaceUse(front, slot, ParasolidConstants.PK_TOPOL_sense_positive_c) < 0))
                        return AlgorithmStatus.WorkspaceTooSmall;
                    break;
                case XtNodeTypes.Loop:
                    AppendLoopToFace(XtMapped(topology, slots, node.Fields[3].Pointer), slot);
                    break;
                case XtNodeTypes.Edge:
                    Edges[slot].CurveTag = XtMapped(geometry, tags, node.Fields[6].Pointer, 0);
                    Edges[slot].Tolerance = XtTolerance(node.Fields[2]);
                    AppendEdgeToBody(bodySlot, slot);
                    break;
                case XtNodeTypes.Vertex:
                    Vertices[slot].PointTag = XtMapped(geometry, tags, node.Fields[5].Pointer, 0);
                    Vertices[slot].Tolerance = XtTolerance(node.Fields[6]);
                    AppendVertexToBody(bodySlot, slot);
                    break;
            }
        }

        // The three fin orders are independent: the loop boundary, edge ring
        // and vertex fan. Restore them independently, preserving every sense.
        for (var i = 0; i < topology.Length; i++)
        {
            var node = topology[i];
            if (node.Type != (int)XtNodeTypes.Halfedge) continue;
            var slot = slots[i];
            ref var fin = ref Fins[slot];
            fin.Loop = XtMapped(topology, slots, node.Fields[1].Pointer);
            fin.NextInLoop = XtMapped(topology, slots, node.Fields[2].Pointer);
            fin.PrevInLoop = XtMapped(topology, slots, node.Fields[3].Pointer);
            fin.Vertex = XtMapped(topology, slots, node.Fields[4].Pointer);
            fin.Other = fin.NextOfEdge = XtMapped(topology, slots, node.Fields[5].Pointer);
            fin.Edge = XtMapped(topology, slots, node.Fields[6].Pointer);
            fin.NextAtVertex = XtMapped(topology, slots, node.Fields[8].Pointer);
            fin.Sense = node.Fields[9].Character;
            var pcurve = XtMapped(geometry, tags, node.Fields[7].Pointer);
            if (pcurve > 0 && AcquireFinCurveReference(slot, pcurve) != 0) return AlgorithmStatus.InvalidInput;
            if (fin.NextOfEdge >= 0) Fins[fin.NextOfEdge].PrevOfEdge = slot;
            if (fin.Loop >= 0) Loops[fin.Loop].FinCount++;
            if (fin.Edge >= 0) Edges[fin.Edge].FinCount++;
        }
        for (var i = 0; i < topology.Length; i++)
        {
            var node = topology[i];
            var slot = slots[i];
            switch ((XtNodeTypes)node.Type)
            {
                case XtNodeTypes.Loop:
                    Loops[slot].FirstFin = XtMapped(topology, slots, node.Fields[2].Pointer);
                    Loops[slot].LastFin = Fins[Loops[slot].FirstFin].PrevInLoop;
                    break;
                case XtNodeTypes.Vertex:
                    Vertices[slot].FirstFinVertex = XtMapped(topology, slots, node.Fields[2].Pointer);
                    var first = Vertices[slot].FirstFinVertex;
                    var current = first;
                    for (var n = 0; current >= 0 && n < topology.Length; n++)
                    {
                        var next = Fins[current].NextAtVertex;
                        if (next < 0) Fins[current].NextAtVertex = next = first;
                        Fins[next].PrevAtVertex = current;
                        if (next == first) { Vertices[slot].LastFinVertex = current; break; }
                        current = next;
                    }
                    break;
                case XtNodeTypes.Edge:
                    ref var edge = ref Edges[slot];
                    edge.FirstFinEdge = XtMapped(topology, slots, node.Fields[3].Pointer);
                    edge.LastFinEdge = Fins[edge.FirstFinEdge].PrevOfEdge;
                    var finSlot = edge.FirstFinEdge;
                    for (var n = 0; n < edge.FinCount; n++, finSlot = Fins[finSlot].NextOfEdge)
                    {
                        ref var fin = ref Fins[finSlot];
                        if (fin.Sense == '+') edge.EndVertex = fin.Vertex;
                        else edge.StartVertex = fin.Vertex;
                        if (fin.Loop >= 0)
                        {
                            var previousVertex = Fins[fin.PrevInLoop].Vertex;
                            if (fin.Sense == '+') edge.StartVertex = previousVertex;
                            else edge.EndVertex = previousVertex;
                        }
                    }
                    var rangeStatus = RestoreXtEdgeRange(ref edge);
                    if (rangeStatus != AlgorithmStatus.Success) return rangeStatus;
                    break;
            }
        }
        EntityTag completed = 0;
        var error = FinishCreatedBody(bodySlot, &completed);
        if (error != 0) return AlgorithmStatus.WorkspaceTooSmall;
        for (var i = 0; i < topology.Length; i++)
        {
            var node = topology[i];
            var slot = slots[i];
            if (node.Type == (int)XtNodeTypes.Halfedge)
            {
                if (Fins[slot].Header.Tag == 0)
                    Fins[slot].Header.Tag = AllocateTag(EntityClass.Fin, PoolKind.Fin, slot, Fins[slot].Header.Generation);
                if (Fins[slot].Header.Tag <= 0)
                    return AlgorithmStatus.WorkspaceTooSmall;
            }
            else if (node.Type == (int)XtNodeTypes.Edge && node.Fields[9].Pointer != root.Index)
                Edges[slot].WireShellTag = TagOf(PoolKind.Shell, XtMapped(topology, slots, node.Fields[9].Pointer));
        }
        foreach (var node in construction)
        {
            error = TryRegisterConstructionGeometry(bodySlot, XtMapped(geometry, tags, node.Index, 0));
            if (error != 0) return error == ParasolidConstants.PK_ERROR_memory_full
                ? AlgorithmStatus.WorkspaceTooSmall : AlgorithmStatus.InvalidInput;
        }
        bodyTag = completed;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus RestoreXtEdgeRange(ref EdgeRecord edge)
    {
        var curveSlot = GetCurveSlotByTag(edge.CurveTag);
        if (curveSlot < 0 || edge.StartVertex < 0 || edge.EndVertex < 0 || edge.StartVertex == edge.EndVertex)
            return AlgorithmStatus.Success;
        var curve = Curves[curveSlot];
        if (curve.Class is not (CurveClass.BCurve or CurveClass.ICurve or CurveClass.TRCurve or CurveClass.Circle or CurveClass.Ellipse)) return AlgorithmStatus.Success;
        if (curve.Class == CurveClass.BCurve)
        {
            var data = BCurveDataStore[curve.DataIndex];
            if (data.IsPeriodic != 0 || data.IsClosed != 0 || data.SelfIntersecting == ParasolidConstants.PK_self_intersect_true_c)
                return AlgorithmStatus.Success;
        }
        var start = GetPointSlotByTag(Vertices[edge.StartVertex].PointTag);
        var end = GetPointSlotByTag(Vertices[edge.EndVertex].PointTag);
        if (start < 0 || end < 0) return AlgorithmStatus.Success;
        var ownsScratch = EnsureCommandScratch();
        try
        {
            // An XT EDGE is trimmed by its vertices, independently of the
            // curve's natural domain. Recover those parameters before any
            // finite-boundary operation consumes the materialized edge.
            var point = new PK_VECTOR_s(); double a = 0, b = 0;
            WriteVector(&point, Points[start].Position);
            var error = CurveParameteriseVectorCore(edge.CurveTag, point, &a);
            if (error == 0)
            {
                WriteVector(&point, Points[end].Position);
                error = CurveParameteriseVectorCore(edge.CurveTag, point, &b);
            }
            if (error == ParasolidConstants.PK_ERROR_memory_full) return AlgorithmStatus.WorkspaceTooSmall;
            // Tolerant or unsupported branches remain readable; no guessed
            // natural-domain interval is installed when inversion fails.
            if (error != 0) return AlgorithmStatus.Success;
            if (curve.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c) (a, b) = (b, a);
            if (curve.Class is CurveClass.Circle or CurveClass.Ellipse && b <= a) b += Math.Tau;
            if (!double.IsFinite(a) || !double.IsFinite(b) || !(b > a)) return AlgorithmStatus.Success;
            edge.ParameterStart = a; edge.ParameterEnd = b; edge.HasParameterRange = 1;
            return AlgorithmStatus.Success;
        }
        finally { if (ownsScratch) ReleaseCommandScratch(State.Session); }
    }

    private static int XtMapped(ReadOnlySpan<XtNode> nodes, ReadOnlySpan<int> values, XtNodeIndex index, int missing = -1)
    {
        if (index != 0)
            for (var i = 0; i < nodes.Length; i++) if (nodes[i].Index == index) return values[i];
        return missing;
    }

    private static KernelSense XtSense(char sense) => sense == '-'
        ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c;

    private static double XtTolerance(XtFieldValue field) => field.Kind == XtFieldKind.Empty ? 0 : field.Real;

    private static int AllocateXtTopology(XtNodeTypes type)
    {
        int slot;
        switch (type)
        {
            case XtNodeTypes.Region:
                slot = TryAllocateRegions(); if (slot < 0) return slot;
                AssignPartition(ref Regions[slot].Header, CurrentPartition);
                Regions[slot].Body = Regions[slot].FirstShell = Regions[slot].LastShell = Regions[slot].Frame = -1;
                Regions[slot].PrevInBody = Regions[slot].NextInBody = -1;
                return slot;
            case XtNodeTypes.Shell:
                slot = TryAllocateShells(); if (slot < 0) return slot;
                AssignPartition(ref Shells[slot].Header, CurrentPartition);
                Shells[slot].Body = Shells[slot].Region = Shells[slot].FirstFaceUseShell = Shells[slot].LastFaceUseShell = -1;
                Shells[slot].AcornVertex = Shells[slot].PrevInBody = Shells[slot].NextInBody = -1;
                Shells[slot].PrevInRegion = Shells[slot].NextInRegion = -1;
                return slot;
            case XtNodeTypes.Face:
                slot = TryAllocateFaces(); if (slot < 0) return slot;
                AssignPartition(ref Faces[slot].Header, CurrentPartition);
                Faces[slot].BackShell = Faces[slot].FrontShell = Faces[slot].BackFaceUse = Faces[slot].FrontFaceUse = -1;
                Faces[slot].FirstLoop = Faces[slot].LastLoop = Faces[slot].PrevOnSurf = Faces[slot].NextOnSurf = -1;
                Faces[slot].PrevInBody = Faces[slot].NextInBody = -1;
                return slot;
            case XtNodeTypes.Loop:
                slot = TryAllocateLoops(); if (slot < 0) return slot;
                AssignPartition(ref Loops[slot].Header, CurrentPartition);
                Loops[slot].Face = Loops[slot].FirstFin = Loops[slot].LastFin = -1;
                Loops[slot].PrevInFace = Loops[slot].NextInFace = -1;
                return slot;
            case XtNodeTypes.Edge:
                slot = TryAllocateEdges(); if (slot < 0) return slot;
                AssignPartition(ref Edges[slot].Header, CurrentPartition);
                Edges[slot].Body = Edges[slot].StartVertex = Edges[slot].EndVertex = -1;
                Edges[slot].FirstFinEdge = Edges[slot].LastFinEdge = Edges[slot].PrevInBody = Edges[slot].NextInBody = -1;
                return slot;
            case XtNodeTypes.Halfedge:
                slot = TryAllocateFins(); if (slot < 0) return slot;
                AssignPartition(ref Fins[slot].Header, CurrentPartition);
                Fins[slot].Loop = Fins[slot].Edge = Fins[slot].Curve = Fins[slot].Vertex = Fins[slot].Other = -1;
                Fins[slot].NextInLoop = Fins[slot].PrevInLoop = Fins[slot].NextOfEdge = Fins[slot].PrevOfEdge = -1;
                Fins[slot].NextAtVertex = Fins[slot].PrevAtVertex = -1;
                return slot;
            case XtNodeTypes.Vertex:
                slot = TryAllocateVertices(); if (slot < 0) return slot;
                AssignPartition(ref Vertices[slot].Header, CurrentPartition);
                Vertices[slot].Body = Vertices[slot].FirstFinVertex = Vertices[slot].LastFinVertex = -1;
                Vertices[slot].PrevInBody = Vertices[slot].NextInBody = -1;
                return slot;
            default: return -1;
        }
    }
}
