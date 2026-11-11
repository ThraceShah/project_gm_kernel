using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyAskType(EntityTag body, KernelBodyType* type)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new BodyAskTypeCommand { Body = body, Type = type };
            return Dispatch(ApiId.BodyAskType, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, body);
        }
        if (type == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsValidTag(body) || (EntityClass)TagRec(body).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;
        var slot = TagRec(body).Slot;
        if (!Xt.IsOpaque(slot)) { *type = Bodies[slot].BodyType; return 0; }
        if (!TryOpaqueBodyField(slot, "body_type", out var value)) return ParasolidConstants.PK_ERROR_not_implemented;
        *type = value.Integer switch
        {
            1 => ParasolidConstants.PK_BODY_type_solid_c, 2 => ParasolidConstants.PK_BODY_type_wire_c,
            3 => ParasolidConstants.PK_BODY_type_sheet_c, 6 => ParasolidConstants.PK_BODY_type_general_c, _ => 0,
        };
        return *type != 0 ? 0 : ParasolidConstants.PK_ERROR_not_implemented;
    }

    public static int BodyAskConfig(EntityTag body, KernelBodyConfig* config)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new BodyAskConfigCommand { Body = body, Config = config };
            return Dispatch(ApiId.BodyAskConfig, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, body);
        }
        if (config == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsValidTag(body) || (EntityClass)TagRec(body).ClassCode != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;
        var slot = TagRec(body).Slot;
        *config = Bodies[slot].BodyConfig;
        if (!Xt.IsOpaque(slot)) return 0;
        if (TryOpaqueBodyField(slot, "child", out var child) && child.Pointer != 0)
            *config = ParasolidConstants.PK_BODY_config_compound_c;
        else if (TryOpaqueBodyField(slot, "owner", out var owner) && owner.Pointer != 0)
        {
            var document = Xt.GetDocument(slot);
            if (document is not null)
                foreach (var node in document.Nodes)
                    if (node.Index == owner.Pointer && node.Type == (int)XtNodeTypes.Body)
                        *config = ParasolidConstants.PK_BODY_config_child_c;
        }
        return 0;
    }

    private static bool TryOpaqueBodyField(BodySlot slot, string name, out XtFieldValue value)
    {
        value = default;
        var document = Xt.GetDocument(slot);
        if (document is null) return false;
        var root = Xt.GetRootIndex(slot);
        foreach (var node in document.Nodes)
        {
            if (node.Index != root) continue;
            var descriptor = document.Schema.GetNode(node.Type);
            var offset = 0;
            foreach (var field in document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
            {
                if (!field.Transmit) continue;
                var count = field.ElementCount > 1 ? field.ElementCount
                    : descriptor.Variable && field.ElementCount == 1 ? node.VariableLength : 1;
                if (field.Name == name && count == 1 && offset < node.Fields.Length)
                { value = node.Fields[offset]; return true; }
                offset += count;
            }
            break;
        }
        return false;
    }

    private static bool EdgeHasFaceFin(EdgeSlot edgeSlot)
    {
        ref var edge = ref Edges[edgeSlot];
        var fin = edge.FirstFinEdge;
        for (var i = 0; i < edge.FinCount && fin >= 0; i++, fin = Fins[fin].NextOfEdge)
            if (Fins[fin].Loop >= 0) return true;
        return false;
    }

    private static EntityTag WireEdgeOwnerTag(BodySlot bodySlot, EdgeSlot edgeSlot)
    {
        var explicitShell = Edges[edgeSlot].WireShellTag;
        if (explicitShell != 0) return explicitShell;
        ref var body = ref Bodies[bodySlot];
        // Legacy one-edge icurve receive predates explicit wire-shell links.
        if (body.BodyType == ParasolidConstants.PK_BODY_type_wire_c && body.ShellCount == 1
            && body.FaceCountBody == 0 && body.EdgeCountBody == 1)
            return TagOf(PoolKind.Shell, body.FirstShell);
        return body.Header.Tag;
    }

    public static int EdgeAskVertices(EntityTag edge, EntityTag* vertices)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new EdgeAskVerticesCommand { Edge = edge, Vertices = vertices };
            return Dispatch(ApiId.EdgeAskVertices, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, edge);
        }
        if (vertices == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsValidTag(edge) || (EntityClass)TagRec(edge).ClassCode != EntityClass.Edge)
            return ParasolidConstants.PK_ERROR_unknown_class;
        ref var record = ref Edges[TagRec(edge).Slot];
        vertices[0] = record.StartVertex >= 0 ? TagOf(PoolKind.Vertex, record.StartVertex) : 0;
        vertices[1] = record.EndVertex >= 0 ? TagOf(PoolKind.Vertex, record.EndVertex) : 0;
        return 0;
    }

    public static int FaceAskEdges(EntityTag face, BufferCount* count, EntityTag** edges)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new FaceAskEdgesCommand { Face = face, Count = count, Edges = edges };
            return Dispatch(ApiId.FaceAskEdges, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, face);
        }
        if (count == null || edges == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        *count = 0; *edges = null;
        if (!IsValidTag(face) || (EntityClass)TagRec(face).ClassCode != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;
        ref var record = ref Faces[TagRec(face).Slot];
        BufferCount maximum = 0;
        var loop = record.FirstLoop;
        for (var i = 0; i < record.LoopCount; i++, loop = Loops[loop].NextInFace) maximum += Loops[loop].FinCount;
        if (maximum == 0) return 0;
        var result = AllocateReturnSlice(maximum);
        if (result == null) return ParasolidConstants.PK_ERROR_memory_full;
        BufferCount found = 0;
        loop = record.FirstLoop;
        for (var i = 0; i < record.LoopCount; i++, loop = Loops[loop].NextInFace)
        {
            var fin = Loops[loop].FirstFin;
            for (var j = 0; j < Loops[loop].FinCount; j++, fin = Fins[fin].NextInLoop)
            {
                if (Fins[fin].Edge < 0) continue;
                var tag = TagOf(PoolKind.Edge, Fins[fin].Edge);
                var duplicate = false;
                for (var k = 0; k < found; k++) if (result[k] == tag) { duplicate = true; break; }
                if (!duplicate) result[found++] = tag;
            }
        }
        *count = found; *edges = result;
        return 0;
    }

    public static int FaceAskOrientedSurf(EntityTag face, SurfTag* surface, KernelLogical* orientation)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new FaceAskOrientedSurfCommand { Face = face, Surface = surface, Orientation = orientation };
            return Dispatch(ApiId.FaceAskOrientedSurf, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, face);
        }
        if (surface == null || orientation == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsValidTag(face) || (EntityClass)TagRec(face).ClassCode != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;
        ref var record = ref Faces[TagRec(face).Slot];
        *surface = record.SurfTag;
        // XT stores the face/natural-surface orientation in both sense fields.
        // The public query reports their composition, without changing either.
        var positive = record.Orientation != ParasolidConstants.PK_TOPOL_sense_negative_c;
        var surfaceSlot = GetSurfaceSlotByTag(record.SurfTag);
        if (surfaceSlot >= 0 && Surfaces[surfaceSlot].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c) positive = !positive;
        *orientation = (byte)(positive ? 1 : 0);
        return 0;
    }

    public static int RegionAskShells(EntityTag region, BufferCount* count, EntityTag** shells)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new RegionAskShellsCommand { Region = region, Count = count, Shells = shells };
            return Dispatch(ApiId.RegionAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, region);
        }
        if (count == null || shells == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        *count = 0; *shells = null;
        if (!IsValidTag(region) || (EntityClass)TagRec(region).ClassCode != EntityClass.Region)
            return ParasolidConstants.PK_ERROR_unknown_class;
        ref var record = ref Regions[TagRec(region).Slot];
        if (record.ShellCount == 0) return 0;
        var result = AllocateReturnSlice(record.ShellCount);
        if (result == null) return ParasolidConstants.PK_ERROR_memory_full;
        var shell = record.FirstShell;
        for (var i = 0; i < record.ShellCount; i++, shell = Shells[shell].NextInRegion)
            result[i] = TagOf(PoolKind.Shell, shell);
        *count = record.ShellCount; *shells = result;
        return 0;
    }
}

internal unsafe struct BodyAskTypeCommand : IKernelCommand
{
    public EntityTag Body;
    public KernelBodyType* Type;
    public int Execute() => KernelRuntime.BodyAskType(Body, Type);
}

internal unsafe struct BodyAskConfigCommand : IKernelCommand
{
    public EntityTag Body;
    public KernelBodyConfig* Config;
    public int Execute() => KernelRuntime.BodyAskConfig(Body, Config);
}

internal unsafe struct EdgeAskVerticesCommand : IKernelCommand
{
    public EntityTag Edge;
    public EntityTag* Vertices;
    public int Execute() => KernelRuntime.EdgeAskVertices(Edge, Vertices);
}

internal unsafe struct FaceAskEdgesCommand : IKernelCommand
{
    public EntityTag Face;
    public BufferCount* Count;
    public EntityTag** Edges;
    public int Execute() => KernelRuntime.FaceAskEdges(Face, Count, Edges);
}

internal unsafe struct FaceAskOrientedSurfCommand : IKernelCommand
{
    public EntityTag Face;
    public SurfTag* Surface;
    public KernelLogical* Orientation;
    public int Execute() => KernelRuntime.FaceAskOrientedSurf(Face, Surface, Orientation);
}

internal unsafe struct RegionAskShellsCommand : IKernelCommand
{
    public EntityTag Region;
    public BufferCount* Count;
    public EntityTag** Shells;
    public int Execute() => KernelRuntime.RegionAskShells(Region, Count, Shells);
}
