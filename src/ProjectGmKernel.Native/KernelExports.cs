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
        var command = new SessionStartCommand { Options = options };
        return KernelRuntime.Dispatch(ApiId.SessionStart, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SESSION_stop")]
    public static int PK_SESSION_stop()
    {
        var command = new SessionStopCommand();
        return KernelRuntime.Dispatch(ApiId.SessionStop, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    // ── Point ────────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_POINT_create")]
    public static int PK_POINT_create(PK_POINT_sf_s* pointSf, int* point)
    {
        var command = new PointCreateCommand { PointSf = pointSf, Point = point };
        return KernelRuntime.Dispatch(ApiId.PointCreate, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    // ── Entity queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_ask_class")]
    public static int PK_ENTITY_ask_class(int entity, int* @class)
    {
        var command = new EntityAskClassCommand { Entity = entity, Class = @class };
        return KernelRuntime.Dispatch(ApiId.EntityAskClass, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_delete")]
    public static int PK_ENTITY_delete(int nEntities, int* entities)
    {
        var command = new EntityDeleteCommand { EntityCount = nEntities, Entities = entities };
        return KernelRuntime.Dispatch(ApiId.EntityDelete, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    // ── Body topology creation ───────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_topology_2")]
    public static int PK_BODY_create_topology_2(
        int nTopols, PK_CLASS_t* classes,
        int nRelations, int* parents, int* children, int* senses,
        PK_BODY_create_topology_2_o_s* options,
        PK_BODY_create_topology_2_r_s* results)
    {
        var command = new BodyCreateTopology2Command { TopologyCount = nTopols, Classes = classes, RelationCount = nRelations, Parents = parents, Children = children, Senses = senses, Options = options, Results = results };
        return KernelRuntime.Dispatch(ApiId.BodyCreateTopology2, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    // ── Body queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_shells")]
    public static int PK_BODY_ask_shells(int body, int* nShells, int** shells)
    {
        var command = new BodyAskShellsCommand { Body = body, ShellCount = nShells, Shells = shells };
        return KernelRuntime.Dispatch(ApiId.BodyAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_faces")]
    public static int PK_BODY_ask_faces(int body, int* nFaces, int** faces)
    {
        var command = new BodyAskFacesCommand { Body = body, FaceCount = nFaces, Faces = faces };
        return KernelRuntime.Dispatch(ApiId.BodyAskFaces, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_edges")]
    public static int PK_BODY_ask_edges(int body, int* nEdges, int** edges)
    {
        var command = new BodyAskEdgesCommand { Body = body, EdgeCount = nEdges, Edges = edges };
        return KernelRuntime.Dispatch(ApiId.BodyAskEdges, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_vertices")]
    public static int PK_BODY_ask_vertices(int body, int* nVertices, int** vertices)
    {
        var command = new BodyAskVerticesCommand { Body = body, VertexCount = nVertices, Vertices = vertices };
        return KernelRuntime.Dispatch(ApiId.BodyAskVertices, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_topology")]
    public static int PK_BODY_ask_topology(
        int body,
        PK_BODY_ask_topology_o_t* options,
        int* nTopols,
        nint* topols,
        nint* classes,
        int* nRelations,
        nint* parents,
        nint* children,
        nint* senses)
    {
        var command = new BodyAskTopologyCommand
        {
            Body = body,
            Options = options,
            TopologyCount = nTopols,
            Topologies = topols,
            Classes = classes,
            RelationCount = nRelations,
            Parents = parents,
            Children = children,
            Senses = senses,
        };
        return KernelRuntime.Dispatch(ApiId.BodyAskTopology, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    // ── Face queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_loops")]
    public static int PK_FACE_ask_loops(int face, int* nLoops, int** loops)
    {
        var command = new FaceAskLoopsCommand { Face = face, LoopCount = nLoops, Loops = loops };
        return KernelRuntime.Dispatch(ApiId.FaceAskLoops, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_surf")]
    public static int PK_FACE_ask_surf(int face, int* surf)
    {
        var command = new FaceAskSurfCommand { Face = face, Surf = surf };
        return KernelRuntime.Dispatch(ApiId.FaceAskSurf, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    // ── Loop queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_LOOP_ask_face")]
    public static int PK_LOOP_ask_face(int loop, int* face)
    {
        var command = new LoopAskFaceCommand { Loop = loop, Face = face };
        return KernelRuntime.Dispatch(ApiId.LoopAskFace, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_LOOP_ask_fins")]
    public static int PK_LOOP_ask_fins(int loop, int* nFins, int** fins)
    {
        var command = new LoopAskFinsCommand { Loop = loop, FinCount = nFins, Fins = fins };
        return KernelRuntime.Dispatch(ApiId.LoopAskFins, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    // ── Edge queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_fins")]
    public static int PK_EDGE_ask_fins(int edge, int* nFins, int** fins)
    {
        var command = new EdgeAskFinsCommand { Edge = edge, FinCount = nFins, Fins = fins };
        return KernelRuntime.Dispatch(ApiId.EdgeAskFins, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_curve")]
    public static int PK_EDGE_ask_curve(int edge, int* curve)
    {
        var command = new EdgeAskCurveCommand { Edge = edge, Curve = curve };
        return KernelRuntime.Dispatch(ApiId.EdgeAskCurve, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    // ── Vertex queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_VERTEX_ask_point")]
    public static int PK_VERTEX_ask_point(int vertex, int* point)
    {
        var command = new VertexAskPointCommand { Vertex = vertex, Point = point };
        return KernelRuntime.Dispatch(ApiId.VertexAskPoint, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    // ── Fin queries ──────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_edge")]
    public static int PK_FIN_ask_edge(int fin, int* edge)
    {
        var command = new FinAskEdgeCommand { Fin = fin, Edge = edge };
        return KernelRuntime.Dispatch(ApiId.FinAskEdge, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_loop")]
    public static int PK_FIN_ask_loop(int fin, int* loop)
    {
        var command = new FinAskLoopCommand { Fin = fin, Loop = loop };
        return KernelRuntime.Dispatch(ApiId.FinAskLoop, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_face")]
    public static int PK_FIN_ask_face(int fin, int* face)
    {
        var command = new FinAskFaceCommand { Fin = fin, Face = face };
        return KernelRuntime.Dispatch(ApiId.FinAskFace, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    // ── Transform ────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_TRANSF_create")]
    public static int PK_TRANSF_create(PK_TRANSF_sf_s* transfSf, int* transf)
    {
        var command = new TransfCreateCommand { TransfSf = transfSf, Transf = transf };
        return KernelRuntime.Dispatch(ApiId.TransfCreate, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    // ── Body creation primitives ─────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_block")]
    public static int PK_BODY_create_solid_block(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidBlockCommand { X = x, Y = y, Z = z, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidBlock, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    // ── Mark / Rollback ──────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_MARK_create")]
    public static int PK_MARK_create(int* mark)
    {
        var command = new MarkCreateCommand { Mark = mark };
        return KernelRuntime.Dispatch(ApiId.MarkCreate, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MARK_goto")]
    public static int PK_MARK_goto(int mark)
    {
        var command = new MarkGotoCommand { Mark = mark };
        return KernelRuntime.Dispatch(ApiId.MarkGoto, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MARK_delete")]
    public static int PK_MARK_delete(int mark)
    {
        var command = new MarkDeleteCommand { Mark = mark };
        return KernelRuntime.Dispatch(ApiId.MarkDelete, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }
}
