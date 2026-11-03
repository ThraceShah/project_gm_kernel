using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_type")]
    public static int PK_BODY_ask_type(EntityTag body, KernelBodyType* type)
    {
        var command = new BodyAskTypeCommand { Body = body, Type = type };
        return KernelRuntime.Dispatch(ApiId.BodyAskType, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, body);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_config")]
    public static int PK_BODY_ask_config(EntityTag body, KernelBodyConfig* config)
    {
        var command = new BodyAskConfigCommand { Body = body, Config = config };
        return KernelRuntime.Dispatch(ApiId.BodyAskConfig, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, body);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_vertices")]
    public static int PK_EDGE_ask_vertices(EntityTag edge, EntityTag* vertices)
    {
        var command = new EdgeAskVerticesCommand { Edge = edge, Vertices = vertices };
        return KernelRuntime.Dispatch(ApiId.EdgeAskVertices, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, edge);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_edges")]
    public static int PK_FACE_ask_edges(EntityTag face, BufferCount* count, EntityTag** edges)
    {
        var command = new FaceAskEdgesCommand { Face = face, Count = count, Edges = edges };
        return KernelRuntime.Dispatch(ApiId.FaceAskEdges, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, face);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_oriented_surf")]
    public static int PK_FACE_ask_oriented_surf(EntityTag face, SurfTag* surface, KernelLogical* orientation)
    {
        var command = new FaceAskOrientedSurfCommand { Face = face, Surface = surface, Orientation = orientation };
        return KernelRuntime.Dispatch(ApiId.FaceAskOrientedSurf, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, face);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_REGION_ask_shells")]
    public static int PK_REGION_ask_shells(EntityTag region, BufferCount* count, EntityTag** shells)
    {
        var command = new RegionAskShellsCommand { Region = region, Count = count, Shells = shells };
        return KernelRuntime.Dispatch(ApiId.RegionAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, region);
    }
}
