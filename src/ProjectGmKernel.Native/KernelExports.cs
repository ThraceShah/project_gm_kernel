using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    // ── Session ──────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_SESSION_start")]
    public static int PK_SESSION_start(PK_SESSION_start_o_s* options)
    {
        return KernelRuntime.Dispatch(
            ApiId.SessionStart,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            () => KernelRuntime.SessionStart(options));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SESSION_stop")]
    public static int PK_SESSION_stop()
    {
        return KernelRuntime.Dispatch(
            ApiId.SessionStop,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            KernelRuntime.SessionStop);
    }

    // ── Point ────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_POINT_create")]
    public static int PK_POINT_create(PK_POINT_sf_s* pointSf, int* point)
    {
        return KernelRuntime.Dispatch(
            ApiId.PointCreate,
            ConcurrencyKind.Exclusive,
            AccessKind.GlobalWrite,
            () => KernelRuntime.PointCreate(pointSf, point));
    }

    // ── Entity queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_ask_class")]
    public static int PK_ENTITY_ask_class(int entity, int* @class)
    {
        return KernelRuntime.Dispatch(
            ApiId.EntityAskClass,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.EntityAskClass(entity, @class));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_delete")]
    public static int PK_ENTITY_delete(int nEntities, int* entities)
    {
        return KernelRuntime.Dispatch(
            ApiId.EntityDelete,
            ConcurrencyKind.Exclusive,
            AccessKind.GlobalWrite,
            () => KernelRuntime.EntityDelete(nEntities, entities));
    }

    // ── Body topology creation ───────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_topology_2")]
    public static int PK_BODY_create_topology_2(
        int nTopols, PK_CLASS_t* classes,
        int nRelations, int* parents, int* children, int* senses,
        PK_BODY_create_topology_2_o_s* options,
        PK_BODY_create_topology_2_r_s* results)
    {
        return KernelRuntime.Dispatch(
            ApiId.BodyCreateTopology2,
            ConcurrencyKind.Exclusive,
            AccessKind.GlobalWrite,
            () => KernelRuntime.BodyCreateTopology2(nTopols, classes, nRelations, parents, children, senses, options, results));
    }

    // ── Body queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_shells")]
    public static int PK_BODY_ask_shells(int body, int* nShells, int** shells)
    {
        return KernelRuntime.Dispatch(
            ApiId.BodyAskShells,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.BodyAskShells(body, nShells, shells));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_faces")]
    public static int PK_BODY_ask_faces(int body, int* nFaces, int** faces)
    {
        return KernelRuntime.Dispatch(
            ApiId.BodyAskFaces,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.BodyAskFaces(body, nFaces, faces));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_edges")]
    public static int PK_BODY_ask_edges(int body, int* nEdges, int** edges)
    {
        return KernelRuntime.Dispatch(
            ApiId.BodyAskEdges,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.BodyAskEdges(body, nEdges, edges));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_vertices")]
    public static int PK_BODY_ask_vertices(int body, int* nVertices, int** vertices)
    {
        return KernelRuntime.Dispatch(
            ApiId.BodyAskVertices,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.BodyAskVertices(body, nVertices, vertices));
    }

    // ── Face queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_loops")]
    public static int PK_FACE_ask_loops(int face, int* nLoops, int** loops)
    {
        return KernelRuntime.Dispatch(
            ApiId.FaceAskLoops,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.FaceAskLoops(face, nLoops, loops));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_surf")]
    public static int PK_FACE_ask_surf(int face, int* surf)
    {
        return KernelRuntime.Dispatch(
            ApiId.FaceAskSurf,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.FaceAskSurf(face, surf));
    }

    // ── Loop queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_LOOP_ask_face")]
    public static int PK_LOOP_ask_face(int loop, int* face)
    {
        return KernelRuntime.Dispatch(
            ApiId.LoopAskFace,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.LoopAskFace(loop, face));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_LOOP_ask_fins")]
    public static int PK_LOOP_ask_fins(int loop, int* nFins, int** fins)
    {
        return KernelRuntime.Dispatch(
            ApiId.LoopAskFins,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.LoopAskFins(loop, nFins, fins));
    }

    // ── Edge queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_fins")]
    public static int PK_EDGE_ask_fins(int edge, int* nFins, int** fins)
    {
        return KernelRuntime.Dispatch(
            ApiId.EdgeAskFins,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.EdgeAskFins(edge, nFins, fins));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_curve")]
    public static int PK_EDGE_ask_curve(int edge, int* curve)
    {
        return KernelRuntime.Dispatch(
            ApiId.EdgeAskCurve,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.EdgeAskCurve(edge, curve));
    }

    // ── Vertex queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_VERTEX_ask_point")]
    public static int PK_VERTEX_ask_point(int vertex, int* point)
    {
        return KernelRuntime.Dispatch(
            ApiId.VertexAskPoint,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.VertexAskPoint(vertex, point));
    }

    // ── Fin queries ──────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_edge")]
    public static int PK_FIN_ask_edge(int fin, int* edge)
    {
        return KernelRuntime.Dispatch(
            ApiId.FinAskEdge,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.FinAskEdge(fin, edge));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_loop")]
    public static int PK_FIN_ask_loop(int fin, int* loop)
    {
        return KernelRuntime.Dispatch(
            ApiId.FinAskLoop,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.FinAskLoop(fin, loop));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_face")]
    public static int PK_FIN_ask_face(int fin, int* face)
    {
        return KernelRuntime.Dispatch(
            ApiId.FinAskFace,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.FinAskFace(fin, face));
    }

    // ── Transform ────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_TRANSF_create")]
    public static int PK_TRANSF_create(PK_TRANSF_sf_s* transfSf, int* transf)
    {
        return KernelRuntime.Dispatch(
            ApiId.TransfCreate,
            ConcurrencyKind.Exclusive,
            AccessKind.GlobalWrite,
            () => KernelRuntime.TransfCreate(transfSf, transf));
    }

    // ── Body creation primitives ─────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_block")]
    public static int PK_BODY_create_solid_block(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* body)
    {
        return KernelRuntime.Dispatch(
            ApiId.BodyCreateSolidBlock,
            ConcurrencyKind.Exclusive,
            AccessKind.GlobalWrite,
            () => KernelRuntime.BodyCreateSolidBlock(x, y, z, basisSet, body));
    }

    // ── Mark / Rollback ──────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_MARK_create")]
    public static int PK_MARK_create(int* mark)
    {
        return KernelRuntime.Dispatch(
            ApiId.MarkCreate,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            () => KernelRuntime.MarkCreate(mark));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MARK_goto")]
    public static int PK_MARK_goto(int mark)
    {
        return KernelRuntime.Dispatch(
            ApiId.MarkGoto,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            () => KernelRuntime.MarkGoto(mark));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MARK_delete")]
    public static int PK_MARK_delete(int mark)
    {
        return KernelRuntime.Dispatch(
            ApiId.MarkDelete,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            () => KernelRuntime.MarkDelete(mark));
    }
}
