using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyTrim(EntityTag body, int edgeCount, EntityTag* barriers, int faceCount, EntityTag* seeds, byte keep)
    {
        if (Dispatcher.IsExecuting) return BodyTrimCore(body, edgeCount, barriers, faceCount, seeds, keep);
        var command = new TrimCommand { Body = body, EdgeCount = edgeCount, Edges = barriers, FaceCount = faceCount, Faces = seeds, Keep = keep };
        return Dispatch(ApiId.BodyTrim, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, body);
    }

    private struct TrimCommand : IKernelCommand
    {
        internal EntityTag Body;
        internal BufferCount EdgeCount, FaceCount;
        internal EntityTag* Edges;
        internal EntityTag* Faces;
        internal byte Keep;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return BodyTrimCore(Body, EdgeCount, Edges, FaceCount, Faces, Keep); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int BodyTrimCore(EntityTag body, int edgeCount, EntityTag* barriers, int faceCount, EntityTag* seeds, byte keep)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (edgeCount <= 0 || faceCount <= 0 || barriers == null || seeds == null || keep > 1)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (!TryResolveBodySlot(body, out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
        if (Bodies[slot].Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if (Bodies[slot].BodyType != ParasolidConstants.PK_BODY_type_sheet_c || Xt.IsOpaque(slot)) return ParasolidConstants.PK_ERROR_not_implemented;
        var total = Bodies[slot].FaceCountBody;
        if (total > 2048 || edgeCount > 8192 || faceCount > total) return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> faces = stackalloc FaceSlot[total]; Span<byte> selected = stackalloc byte[total]; selected.Clear();
        Span<EdgeSlot> edges = stackalloc EdgeSlot[edgeCount];
        var f = Bodies[slot].FirstFaceBody;
        for (var i = 0; i < total; i++, f = Faces[f].NextInBody) faces[i] = f;
        for (var i = 0; i < edgeCount; i++)
        {
            if (!State.Session->Tags.TryResolve(barriers[i], CurrentSessionId, out var record) || record.Pool != (byte)PoolKind.Edge)
                return ParasolidConstants.PK_ERROR_bad_class;
            if (Edges[record.Slot].Body != slot) return ParasolidConstants.PK_ERROR_not_in_same_body;
            if (edges[..i].Contains(record.Slot)) return ParasolidConstants.PK_ERROR_duplicate_list_item;
            edges[i] = record.Slot;
        }
        for (var i = 0; i < faceCount; i++)
        {
            if (!State.Session->Tags.TryResolve(seeds[i], CurrentSessionId, out var record) || record.Pool != (byte)PoolKind.Face)
                return ParasolidConstants.PK_ERROR_bad_class;
            var index = faces.IndexOf(record.Slot);
            if (index < 0) return ParasolidConstants.PK_ERROR_not_in_same_body;
            selected[index] = 1;
        }
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < total; i++) if (selected[i] != 0)
            {
                var loop = Faces[faces[i]].FirstLoop;
                for (var l = 0; l < Faces[faces[i]].LoopCount; l++, loop = Loops[loop].NextInFace)
                {
                    var fin = Loops[loop].FirstFin;
                    for (var k = 0; k < Loops[loop].FinCount; k++, fin = Fins[fin].NextInLoop)
                    {
                        if (edges.Contains(Fins[fin].Edge)) continue;
                        var other = Fins[fin].Other;
                        if (other < 0 || Fins[other].Loop < 0) continue;
                        var neighbor = faces.IndexOf(Loops[Fins[other].Loop].Face);
                        if (neighbor >= 0 && selected[neighbor] == 0) { selected[neighbor] = 1; changed = true; }
                    }
                }
            }
        }
        Span<FaceSlot> retained = stackalloc FaceSlot[total]; var count = 0;
        for (var i = 0; i < total; i++) if ((selected[i] != 0) == (keep != 0)) retained[count++] = faces[i];
        if (count == 0 || count == total) return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<FaceSlot> products = stackalloc FaceSlot[count];
        var error = CopyModelingFaces(retained[..count], false, out var replacement, products);
        if (error != 0) return error;
        var resultSlot = TagRec(replacement).Slot;
        error = PreserveModelingFaceHandles(slot, resultSlot, retained[..count], products);
        if (error != 0) return error;
        return ReplaceProfileBody(slot, resultSlot);
    }
}
