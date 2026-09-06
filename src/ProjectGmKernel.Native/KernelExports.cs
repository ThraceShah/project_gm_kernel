using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_create")]
    public static int PK_BCURVE_create(PK_BCURVE_sf_s* definition, CurveTag* curve)
    {
        var command = new BCurveCreateCommand { Definition = definition, Curve = curve };
        return KernelRuntime.Dispatch(ApiId.BCurveCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_CURVE_eval")]
    public static int PK_CURVE_eval(CurveTag curve, double t, DerivativeOrder nDerivs, PK_VECTOR_s* output)
    {
        var command = new CurveEvalCommand { Curve = curve, Parameter = t, Order = nDerivs, Output = output };
        return KernelRuntime.Dispatch(ApiId.CurveEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Curve);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_CURVE_eval_with_tangent")]
    public static int PK_CURVE_eval_with_tangent(CurveTag curve, double t, DerivativeOrder nDerivs,
        PK_VECTOR_s* output, PK_VECTOR_s* tangent)
    {
        var command = new CurveEvalWithTangentCommand
        { Curve = curve, Parameter = t, Order = nDerivs, Output = output, Tangent = tangent };
        return KernelRuntime.Dispatch(ApiId.CurveEvalWithTangent, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Curve);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SURF_eval")]
    public static int PK_SURF_eval(SurfTag surface, PK_UV_s uv, DerivativeOrder nUDerivs,
        DerivativeOrder nVDerivs, KernelLogical triangular, PK_VECTOR_s* output)
    {
        var command = new SurfEvalCommand
        { Surface = surface, Parameter = uv, UOrder = nUDerivs, VOrder = nVDerivs, Triangular = triangular, Output = output };
        return KernelRuntime.Dispatch(ApiId.SurfEval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Surface);
    }

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
        return KernelRuntime.Dispatch(ApiId.PointCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    // ── Entity queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_ask_class")]
    public static int PK_ENTITY_ask_class(int entity, int* @class)
    {
        var command = new EntityAskClassCommand { Entity = entity, Class = @class };
        return KernelRuntime.Dispatch(ApiId.EntityAskClass, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Entity);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_delete")]
    public static int PK_ENTITY_delete(int nEntities, int* entities)
    {
        var command = new EntityDeleteCommand { EntityCount = nEntities, Entities = entities };
        return KernelRuntime.Dispatch(ApiId.EntityDelete, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_ask_partition")]
    public static int PK_ENTITY_ask_partition(int entity, int* partition)
    {
        var command = new EntityAskPartitionCommand { Entity = entity, Partition = partition };
        return KernelRuntime.Dispatch(ApiId.EntityAskPartition, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Entity);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SESSION_ask_curr_partition")]
    public static int PK_SESSION_ask_curr_partition(int* partition)
    {
        var command = new SessionAskCurrentPartitionCommand { Partition = partition };
        return KernelRuntime.Dispatch(ApiId.SessionAskCurrentPartition, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
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
        return KernelRuntime.Dispatch(ApiId.BodyCreateTopology2, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    // ── Body queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_shells")]
    public static int PK_BODY_ask_shells(int body, int* nShells, int** shells)
    {
        var command = new BodyAskShellsCommand { Body = body, ShellCount = nShells, Shells = shells };
        return KernelRuntime.Dispatch(ApiId.BodyAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Body);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_faces")]
    public static int PK_BODY_ask_faces(int body, int* nFaces, int** faces)
    {
        var command = new BodyAskFacesCommand { Body = body, FaceCount = nFaces, Faces = faces };
        return KernelRuntime.Dispatch(ApiId.BodyAskFaces, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Body);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_edges")]
    public static int PK_BODY_ask_edges(int body, int* nEdges, int** edges)
    {
        var command = new BodyAskEdgesCommand { Body = body, EdgeCount = nEdges, Edges = edges };
        return KernelRuntime.Dispatch(ApiId.BodyAskEdges, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Body);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_vertices")]
    public static int PK_BODY_ask_vertices(int body, int* nVertices, int** vertices)
    {
        var command = new BodyAskVerticesCommand { Body = body, VertexCount = nVertices, Vertices = vertices };
        return KernelRuntime.Dispatch(ApiId.BodyAskVertices, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Body);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_ask_regions")]
    public static int PK_BODY_ask_regions(int body, int* nRegions, int** regions)
    {
        var command = new BodyAskRegionsCommand { Body = body, RegionCount = nRegions, Regions = regions };
        return KernelRuntime.Dispatch(ApiId.BodyAskRegions, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Body);
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
        return KernelRuntime.Dispatch(ApiId.BodyAskTopology, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Body);
    }

    // ── Face queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_loops")]
    public static int PK_FACE_ask_loops(int face, int* nLoops, int** loops)
    {
        var command = new FaceAskLoopsCommand { Face = face, LoopCount = nLoops, Loops = loops };
        return KernelRuntime.Dispatch(ApiId.FaceAskLoops, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Face);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_surf")]
    public static int PK_FACE_ask_surf(int face, int* surf)
    {
        var command = new FaceAskSurfCommand { Face = face, Surf = surf };
        return KernelRuntime.Dispatch(ApiId.FaceAskSurf, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Face);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_ask_shells")]
    public static int PK_FACE_ask_shells(int face, int* shells)
    {
        var command = new FaceAskShellsCommand { Face = face, Shells = shells };
        return KernelRuntime.Dispatch(ApiId.FaceAskShells, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Face);
    }

    // ── Region queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_REGION_is_solid")]
    public static int PK_REGION_is_solid(int region, byte* isSolid)
    {
        var command = new RegionIsSolidCommand { Region = region, IsSolid = isSolid };
        return KernelRuntime.Dispatch(ApiId.RegionIsSolid, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Region);
    }

    // ── Loop queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_LOOP_ask_face")]
    public static int PK_LOOP_ask_face(int loop, int* face)
    {
        var command = new LoopAskFaceCommand { Loop = loop, Face = face };
        return KernelRuntime.Dispatch(ApiId.LoopAskFace, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Loop);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_LOOP_ask_fins")]
    public static int PK_LOOP_ask_fins(int loop, int* nFins, int** fins)
    {
        var command = new LoopAskFinsCommand { Loop = loop, FinCount = nFins, Fins = fins };
        return KernelRuntime.Dispatch(ApiId.LoopAskFins, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Loop);
    }

    // ── Edge queries ─────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_fins")]
    public static int PK_EDGE_ask_fins(int edge, int* nFins, int** fins)
    {
        var command = new EdgeAskFinsCommand { Edge = edge, FinCount = nFins, Fins = fins };
        return KernelRuntime.Dispatch(ApiId.EdgeAskFins, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Edge);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_ask_curve")]
    public static int PK_EDGE_ask_curve(int edge, int* curve)
    {
        var command = new EdgeAskCurveCommand { Edge = edge, Curve = curve };
        return KernelRuntime.Dispatch(ApiId.EdgeAskCurve, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Edge);
    }

    // ── Vertex queries ───────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_VERTEX_ask_point")]
    public static int PK_VERTEX_ask_point(int vertex, int* point)
    {
        var command = new VertexAskPointCommand { Vertex = vertex, Point = point };
        return KernelRuntime.Dispatch(ApiId.VertexAskPoint, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Vertex);
    }

    // ── Fin queries ──────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_edge")]
    public static int PK_FIN_ask_edge(int fin, int* edge)
    {
        var command = new FinAskEdgeCommand { Fin = fin, Edge = edge };
        return KernelRuntime.Dispatch(ApiId.FinAskEdge, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Fin);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_loop")]
    public static int PK_FIN_ask_loop(int fin, int* loop)
    {
        var command = new FinAskLoopCommand { Fin = fin, Loop = loop };
        return KernelRuntime.Dispatch(ApiId.FinAskLoop, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Fin);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_FIN_ask_face")]
    public static int PK_FIN_ask_face(int fin, int* face)
    {
        var command = new FinAskFaceCommand { Fin = fin, Face = face };
        return KernelRuntime.Dispatch(ApiId.FinAskFace, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Fin);
    }

    // ── Transform ────────────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_TRANSF_create")]
    public static int PK_TRANSF_create(PK_TRANSF_sf_s* transfSf, int* transf)
    {
        var command = new TransfCreateCommand { TransfSf = transfSf, Transf = transf };
        return KernelRuntime.Dispatch(ApiId.TransfCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    // ── Analytic geometry ────────────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_CYL_create")]
    public static int PK_CYL_create(PK_CYL_sf_s* cylSf, int* cyl)
    {
        var command = new CylCreateCommand { CylinderSf = cylSf, Cylinder = cyl };
        return KernelRuntime.Dispatch(ApiId.CylCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_CYL_ask")]
    public static int PK_CYL_ask(int cyl, PK_CYL_sf_s* cylSf)
    {
        var command = new CylAskCommand { Cylinder = cyl, CylinderSf = cylSf };
        return KernelRuntime.Dispatch(ApiId.CylAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Cylinder);
    }

    // ── Body creation primitives ─────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_block")]
    public static int PK_BODY_create_solid_block(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidBlockCommand { X = x, Y = y, Z = z, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidBlock, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_cyl")]
    public static int PK_BODY_create_solid_cyl(double radius, double height, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidCylCommand { Radius = radius, Height = height, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidCyl, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_cone")]
    public static int PK_BODY_create_solid_cone(double radius, double height, double semiAngle, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidConeCommand { Radius = radius, Height = height, SemiAngle = semiAngle, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidCone, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_prism")]
    public static int PK_BODY_create_solid_prism(double radius, double height, int nSides, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidPrismCommand { Radius = radius, Height = height, SideCount = nSides, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidPrism, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_sphere")]
    public static int PK_BODY_create_solid_sphere(double radius, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidSphereCommand { Radius = radius, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidSphere, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_solid_torus")]
    public static int PK_BODY_create_solid_torus(double majorRadius, double minorRadius, PK_AXIS2_sf_s* basisSet, int* body)
    {
        var command = new BodyCreateSolidTorusCommand { MajorRadius = majorRadius, MinorRadius = minorRadius, BasisSet = basisSet, Body = body };
        return KernelRuntime.Dispatch(ApiId.BodyCreateSolidTorus, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    // ── XT transmit / receive ───────────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_PART_transmit_b")]
    public static int PK_PART_transmit_b(int nParts, int* parts, PK_PART_transmit_o_s* options, PK_MEMORY_block_t* block)
    {
        var command = new PartTransmitBCommand { PartCount = nParts, Parts = parts, Options = options, Block = block };
        return KernelRuntime.Dispatch(ApiId.PartTransmitB, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_PART_receive_b")]
    public static int PK_PART_receive_b(PK_MEMORY_block_t block, PK_PART_receive_o_s* options, int* nParts, int** parts)
    {
        var command = new PartReceiveBCommand { Block = block, Options = options, PartCount = nParts, Parts = parts };
        return KernelRuntime.Dispatch(ApiId.PartReceiveB, ConcurrencyKind.Exclusive, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MEMORY_block_f")]
    public static int PK_MEMORY_block_f(PK_MEMORY_block_t* block)
    {
        var command = new MemoryBlockFreeCommand { Block = block };
        return KernelRuntime.Dispatch(ApiId.MemoryBlockFree, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MEMORY_free")]
    public static int PK_MEMORY_free(void* pointer)
    {
        var command = new MemoryFreeCommand { Pointer = pointer };
        return KernelRuntime.Dispatch(ApiId.MemoryFree, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
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

    // ── Partition / thread protocol ─────────────────────────────

    [UnmanagedCallersOnly(EntryPoint = "PK_PARTITION_create_empty")]
    public static int PK_PARTITION_create_empty(int* partition)
    {
        var command = new PartitionCreateEmptyCommand { Partition = partition };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_PARTITION_set_current")]
    public static int PK_PARTITION_set_current(int partition)
    {
        var command = new PartitionSetCurrentCommand { Partition = partition };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Local, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_PARTITION_delete")]
    public static int PK_PARTITION_delete(int partition, PK_PARTITION_delete_o_s* options)
    {
        var command = new PartitionDeleteCommand { Partition = partition, Options = options };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_PARTITION_ask_type")]
    public static int PK_PARTITION_ask_type(int partition, int* partitionType)
    {
        var command = new PartitionAskTypeCommand { Partition = partition, PartitionType = partitionType };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_lock_partitions")]
    public static int PK_THREAD_lock_partitions(int nPartitions, int* partitions, int lockType, int waitType,
        PK_THREAD_lock_partitions_o_s* options, PK_THREAD_lock_partitions_r_s* result)
    {
        var command = new ThreadLockPartitionsCommand
        { Count = nPartitions, Partitions = partitions, LockType = lockType, WaitType = waitType, Options = options, Result = result };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_lock_partitions_r_f")]
    public static int PK_THREAD_lock_partitions_r_f(PK_THREAD_lock_partitions_r_s* result)
    {
        var command = new ThreadLockPartitionsResultFreeCommand { Result = result };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_unlock_partitions")]
    public static int PK_THREAD_unlock_partitions(PK_THREAD_unlock_partitions_o_s* options, int* nPartitions, int** partitions)
    {
        var command = new ThreadUnlockPartitionsCommand { Options = options, NPartitions = nPartitions, Partitions = partitions };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_ask_partitions")]
    public static int PK_THREAD_ask_partitions(PK_THREAD_ask_partitions_o_s* options, int* nPartitions, int** partitions)
    {
        var command = new ThreadAskLockedPartitionsCommand { Options = options, NPartitions = nPartitions, Partitions = partitions };
        return KernelRuntime.Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_set_id")]
    public static int PK_THREAD_set_id(int threadId, PK_THREAD_set_id_o_s* options, PK_THREAD_set_id_r_s* result)
    {
        return KernelRuntime.ThreadSetId(threadId, options, result);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_ask_id")]
    public static int PK_THREAD_ask_id(int* threadId, int* parasolidId, byte* isSubthread)
    {
        return KernelRuntime.ThreadAskId(threadId, parasolidId, isSubthread);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_chain_start")]
    public static int PK_THREAD_chain_start(int type, PK_THREAD_chain_start_o_s* options)
    {
        return KernelRuntime.ThreadChainStart(type, options);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_chain_stop")]
    public static int PK_THREAD_chain_stop(PK_THREAD_chain_stop_o_s* options)
    {
        return KernelRuntime.ThreadChainStop(options);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_is_in_chain")]
    public static int PK_THREAD_is_in_chain(int* type, int* length, int* remaining)
    {
        return KernelRuntime.ThreadIsInChain(type, length, remaining);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_is_in_kernel")]
    public static int PK_THREAD_is_in_kernel(byte* inKernel, byte* isProtected, byte* isSubthread, byte* isExcluding)
    {
        return KernelRuntime.ThreadIsInKernel(inKernel, isProtected, isSubthread, isExcluding);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_register_memory_cbs")]
    public static int PK_THREAD_register_memory_cbs(PK_MEMORY_frustrum_t cbs)
    {
        return KernelRuntime.ThreadRegisterMemoryCbs(cbs);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_MEMORY_register_callbacks")]
    public static int PK_MEMORY_register_callbacks(PK_MEMORY_frustrum_t callbacks)
        => KernelRuntime.MemoryRegisterCallbacks(callbacks);

    [UnmanagedCallersOnly(EntryPoint = "PK_MEMORY_ask_callbacks")]
    public static int PK_MEMORY_ask_callbacks(PK_MEMORY_frustrum_t* callbacks)
        => KernelRuntime.MemoryAskCallbacks(callbacks);

    [UnmanagedCallersOnly(EntryPoint = "PK_FUNCTION_find")]
    public static int PK_FUNCTION_find(int count, byte** names, PK_FUNCTION_find_o_t* options, int* functions)
        => KernelRuntime.FunctionFind(count, names, options, functions);

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_ask_function_run")]
    public static int PK_THREAD_ask_function_run(int count, int* functions, PK_THREAD_ask_function_run_o_t* options, int* values)
        => KernelRuntime.ThreadAskFunctionRun(count, functions, options, values);

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_ask_local_level")]
    public static int PK_THREAD_ask_local_level(PK_THREAD_ask_local_level_o_t* options, int* level)
        => KernelRuntime.ThreadAskLocalLevel(options, level);

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_attach_curves")]
    public static int PK_EDGE_attach_curves(int count, int* edges, int* curves)
        => KernelRuntime.EdgeAttachCurves(count, edges, curves);

    [UnmanagedCallersOnly(EntryPoint = "PK_FACE_attach_surfs")]
    public static int PK_FACE_attach_surfs(int count, int* faces, int* surfaces, byte* senses)
        => KernelRuntime.FaceAttachSurfaces(count, faces, surfaces, senses);

    [UnmanagedCallersOnly(EntryPoint = "PK_VERTEX_attach_points")]
    public static int PK_VERTEX_attach_points(int count, int* vertices, int* points)
        => KernelRuntime.VertexAttachPoints(count, vertices, points);

    [UnmanagedCallersOnly(EntryPoint = "PK_TOPOL_detach_geom")]
    public static int PK_TOPOL_detach_geom(int topology)
        => KernelRuntime.TopologyDetachGeometry(topology);

    [UnmanagedCallersOnly(EntryPoint = "PK_THREAD_ask_memory_cbs")]
    public static int PK_THREAD_ask_memory_cbs(PK_MEMORY_frustrum_t* cbs)
    {
        return KernelRuntime.ThreadAskMemoryCbs(cbs);
    }
}
