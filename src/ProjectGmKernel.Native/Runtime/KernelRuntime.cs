using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe class KernelRuntime
{
    private const int DefaultSessionId = 1;
    private const int MaxHandles = 4096;

    // ── Pool capacities ──────────────────────────────────────────
    private const int MaxPoints = 2048;
    private const int MaxVectors = 2048;
    private const int MaxBodies = 512;
    private const int MaxShells = 1024;
    private const int MaxFaceUses = 8192;
    private const int MaxFaces = 4096;
    private const int MaxLoops = 8192;
    private const int MaxEdges = 8192;
    private const int MaxFins = 16384;
    private const int MaxVertices = 8192;
    private const int MaxRegions = 512;
    private const int MaxCurves = 4096;
    private const int MaxSurfaces = 4096;
    private const int MaxTransforms = 256;
    private const int MaxCircleData = 4096;
    private const int MaxLineData = 4096;
    private const int MaxCylinderData = 1024;
    private const int MaxPlaneData = 1024;

    // ── Shared state ─────────────────────────────────────────────
    private static readonly System.Threading.Lock RuntimeLock = new();
    private static readonly SessionDispatchState DispatchState = new();
    private static SessionState? session;

    // ── Handle table (tag → entity mapping) ──────────────────────
    private static readonly HandleRecord[] Handles = new HandleRecord[MaxHandles];
    private static int nextTag = 1;

    // ── Slot → tag reverse mapping (per pool) ────────────────────
    // Avoids creating duplicate tags for the same entity slot.
    private static readonly int[] PointSlotToTag = new int[MaxPoints];
    private static readonly int[] VectorSlotToTag = new int[MaxVectors];
    private static readonly int[] BodySlotToTag = new int[MaxBodies];
    private static readonly int[] ShellSlotToTag = new int[MaxShells];
    private static readonly int[] FaceSlotToTag = new int[MaxFaces];
    private static readonly int[] LoopSlotToTag = new int[MaxLoops];
    private static readonly int[] EdgeSlotToTag = new int[MaxEdges];
    private static readonly int[] FinSlotToTag = new int[MaxFins];
    private static readonly int[] VertexSlotToTag = new int[MaxVertices];
    private static readonly int[] RegionSlotToTag = new int[MaxRegions];
    private static readonly int[] CurveSlotToTag = new int[MaxCurves];
    private static readonly int[] SurfaceSlotToTag = new int[MaxSurfaces];
    private static readonly int[] TransformSlotToTag = new int[MaxTransforms];

    // ── Return arena for int** query outputs ─────────────────────
    // Session-managed unmanaged buffers. Each query allocates a contiguous
    // slice, and returned pointers remain valid until the arena is reset.
    private const int InitialReturnBlockCapacity = 256;
    private const int MaxReturnBlocks = 64;
    private static readonly nint[] ReturnBlocks = new nint[MaxReturnBlocks];
    private static readonly int[] ReturnBlockCapacities = new int[MaxReturnBlocks];
    private static int returnBlockCount;
    private static int returnBlockIndex = -1;
    private static int returnCursor;

    // ── Entity pools ─────────────────────────────────────────────
    internal static EntityPool<PointRecord> Points;
    internal static EntityPool<VectorRecord> Vectors;
    internal static EntityPool<BodyRecord> Bodies;
    internal static EntityPool<ShellRecord> Shells;
    internal static EntityPool<FaceUseRecord> FaceUses;
    internal static EntityPool<FaceRecord> Faces;
    internal static EntityPool<LoopRecord> Loops;
    internal static EntityPool<EdgeRecord> Edges;
    internal static EntityPool<FinRecord> Fins;
    internal static EntityPool<VertexRecord> Vertices;
    internal static EntityPool<RegionRecord> Regions;
    internal static EntityPool<CurveRecord> Curves;
    internal static EntityPool<SurfaceRecord> Surfaces;
    internal static EntityPool<TransformRecord> Transforms;
    internal static EntityPool<CircleData> CircleDataPool;
    internal static EntityPool<LineData> LineDataPool;
    internal static EntityPool<CylinderData> CylinderDataPool;
    internal static EntityPool<PlaneData> PlaneDataPool;

    internal static bool IsSessionStarted => session is not null && session.Started;

    internal static bool TryResolveBodySlot(EntityTag bodyTag, out BodySlot bodySlot)
    {
        bodySlot = -1;
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return false;

        bodySlot = Handles[bodyTag].SlotIndex;
        return true;
    }

    internal static BodyRecord GetBodyRecord(BodySlot bodySlot) => Bodies[bodySlot];
    internal static RegionRecord GetRegionRecord(RegionSlot regionSlot) => Regions[regionSlot];
    internal static ShellRecord GetShellRecord(ShellSlot shellSlot) => Shells[shellSlot];
    internal static FaceUseRecord GetFaceUseRecord(FaceUseSlot faceUseSlot) => FaceUses[faceUseSlot];
    internal static FaceRecord GetFaceRecord(FaceSlot faceSlot) => Faces[faceSlot];
    internal static LoopRecord GetLoopRecord(LoopSlot loopSlot) => Loops[loopSlot];
    internal static FinRecord GetFinRecord(FinSlot finSlot) => Fins[finSlot];
    internal static EdgeRecord GetEdgeRecord(EdgeSlot edgeSlot) => Edges[edgeSlot];
    internal static VertexRecord GetVertexRecord(VertexSlot vertexSlot) => Vertices[vertexSlot];

    internal static SurfaceRecord GetSurfaceByTag(SurfTag surfaceTag)
    {
        if (!IsValidTag(surfaceTag) || Handles[surfaceTag].Class != EntityClass.Surface)
            return default;
        return Surfaces[Handles[surfaceTag].SlotIndex];
    }

    internal static SurfaceSlot GetSurfaceSlotByTag(SurfTag surfaceTag)
    {
        return IsValidTag(surfaceTag) && Handles[surfaceTag].Class == EntityClass.Surface
            ? Handles[surfaceTag].SlotIndex
            : -1;
    }

    internal static PointRecord GetPointByTag(PointTag pointTag)
    {
        if (!IsValidTag(pointTag) || Handles[pointTag].Class != EntityClass.Point)
            return default;
        return Points[Handles[pointTag].SlotIndex];
    }

    internal static PointSlot GetPointSlotByTag(PointTag pointTag)
    {
        return IsValidTag(pointTag) && Handles[pointTag].Class == EntityClass.Point
            ? Handles[pointTag].SlotIndex
            : -1;
    }

    internal static CurveRecord GetCurveByTag(CurveTag curveTag)
    {
        if (!IsValidTag(curveTag) || Handles[curveTag].Class != EntityClass.Curve)
            return default;
        return Curves[Handles[curveTag].SlotIndex];
    }

    internal static CurveSlot GetCurveSlotByTag(CurveTag curveTag)
    {
        return IsValidTag(curveTag) && Handles[curveTag].Class == EntityClass.Curve
            ? Handles[curveTag].SlotIndex
            : -1;
    }

    internal static CylinderData GetCylinderData(DataSlot dataSlot) => CylinderDataPool[dataSlot];
    internal static PlaneData GetPlaneData(DataSlot dataSlot) => PlaneDataPool[dataSlot];
    internal static CircleData GetCircleData(DataSlot dataSlot) => CircleDataPool[dataSlot];
    internal static LineData GetLineData(DataSlot dataSlot) => LineDataPool[dataSlot];

    // Pool index constants for mark/rollback
    private const int PoolHandles = 0;
    private const int PoolPoints = 1;
    private const int PoolVectors = 2;
    private const int PoolBodies = 3;
    private const int PoolShells = 4;
    private const int PoolFaceUses = 5;
    private const int PoolFaces = 6;
    private const int PoolLoops = 7;
    private const int PoolEdges = 8;
    private const int PoolFins = 9;
    private const int PoolVertices = 10;
    private const int PoolRegions = 11;
    private const int PoolCurves = 12;
    private const int PoolSurfaces = 13;
    private const int PoolTransforms = 14;
    private const int PoolCircleData = 15;
    private const int PoolCylinderData = 16;
    private const int PoolPlaneData = 17;
    private const int PoolLineData = 18;

    static KernelRuntime()
    {
        Points = new EntityPool<PointRecord>(MaxPoints);
        Vectors = new EntityPool<VectorRecord>(MaxVectors);
        Bodies = new EntityPool<BodyRecord>(MaxBodies);
        Shells = new EntityPool<ShellRecord>(MaxShells);
        FaceUses = new EntityPool<FaceUseRecord>(MaxFaceUses);
        Faces = new EntityPool<FaceRecord>(MaxFaces);
        Loops = new EntityPool<LoopRecord>(MaxLoops);
        Edges = new EntityPool<EdgeRecord>(MaxEdges);
        Fins = new EntityPool<FinRecord>(MaxFins);
        Vertices = new EntityPool<VertexRecord>(MaxVertices);
        Regions = new EntityPool<RegionRecord>(MaxRegions);
        Curves = new EntityPool<CurveRecord>(MaxCurves);
        Surfaces = new EntityPool<SurfaceRecord>(MaxSurfaces);
        Transforms = new EntityPool<TransformRecord>(MaxTransforms);
        CircleDataPool = new EntityPool<CircleData>(MaxCircleData);
        LineDataPool = new EntityPool<LineData>(MaxLineData);
        CylinderDataPool = new EntityPool<CylinderData>(MaxCylinderData);
        PlaneDataPool = new EntityPool<PlaneData>(MaxPlaneData);
    }

    // ── Tag allocation ───────────────────────────────────────────

    private static int AllocateTag(EntityClass entityClass, PoolKind pool, int slotIndex, int generation)
    {
        if (nextTag >= MaxHandles)
            return -1;

        var tag = nextTag++;
        Handles[tag] = new HandleRecord
        {
            Alive = 1,
            Class = entityClass,
            Pool = pool,
            SlotIndex = slotIndex,
            Generation = generation,
            SessionId = DefaultSessionId,
        };
        SetSlotToTag(pool, slotIndex, tag);
        return tag;
    }

    /// <summary>
    /// Get existing tag for a slot, or allocate a new one if none exists.
    /// Ensures stable tag identity for the same entity.
    /// </summary>
    private static int GetOrAllocateTag(EntityClass entityClass, PoolKind pool, int slotIndex)
    {
        var existing = GetSlotToTag(pool, slotIndex);
        if (existing > 0 && existing < nextTag && Handles[existing].Alive != 0)
            return existing;
        return AllocateEntityTag(entityClass, pool, slotIndex);
    }

    private static void SetSlotToTag(PoolKind pool, int slot, int tag)
    {
        switch (pool)
        {
            case PoolKind.Point: PointSlotToTag[slot] = tag; break;
            case PoolKind.Vector: VectorSlotToTag[slot] = tag; break;
            case PoolKind.Body: BodySlotToTag[slot] = tag; break;
            case PoolKind.Shell: ShellSlotToTag[slot] = tag; break;
            case PoolKind.Face: FaceSlotToTag[slot] = tag; break;
            case PoolKind.Loop: LoopSlotToTag[slot] = tag; break;
            case PoolKind.Edge: EdgeSlotToTag[slot] = tag; break;
            case PoolKind.Fin: FinSlotToTag[slot] = tag; break;
            case PoolKind.Vertex: VertexSlotToTag[slot] = tag; break;
            case PoolKind.Region: RegionSlotToTag[slot] = tag; break;
            case PoolKind.Curve: CurveSlotToTag[slot] = tag; break;
            case PoolKind.Surface: SurfaceSlotToTag[slot] = tag; break;
            case PoolKind.Transform: TransformSlotToTag[slot] = tag; break;
        }
    }

    private static int GetSlotToTag(PoolKind pool, int slot)
    {
        return pool switch
        {
            PoolKind.Point => PointSlotToTag[slot],
            PoolKind.Vector => VectorSlotToTag[slot],
            PoolKind.Body => BodySlotToTag[slot],
            PoolKind.Shell => ShellSlotToTag[slot],
            PoolKind.Face => FaceSlotToTag[slot],
            PoolKind.Loop => LoopSlotToTag[slot],
            PoolKind.Edge => EdgeSlotToTag[slot],
            PoolKind.Fin => FinSlotToTag[slot],
            PoolKind.Vertex => VertexSlotToTag[slot],
            PoolKind.Region => RegionSlotToTag[slot],
            PoolKind.Curve => CurveSlotToTag[slot],
            PoolKind.Surface => SurfaceSlotToTag[slot],
            PoolKind.Transform => TransformSlotToTag[slot],
            _ => 0,
        };
    }

    private static void ClearSlotToTagMaps()
    {
        Array.Clear(PointSlotToTag);
        Array.Clear(VectorSlotToTag);
        Array.Clear(BodySlotToTag);
        Array.Clear(ShellSlotToTag);
        Array.Clear(FaceSlotToTag);
        Array.Clear(LoopSlotToTag);
        Array.Clear(EdgeSlotToTag);
        Array.Clear(FinSlotToTag);
        Array.Clear(VertexSlotToTag);
        Array.Clear(RegionSlotToTag);
        Array.Clear(CurveSlotToTag);
        Array.Clear(SurfaceSlotToTag);
        Array.Clear(TransformSlotToTag);
    }

    private static void ResetReturnArena(bool freeBlocks)
    {
        if (freeBlocks)
        {
            for (int i = 0; i < returnBlockCount; i++)
            {
                if (ReturnBlocks[i] != 0)
                {
                    NativeMemory.Free((void*)ReturnBlocks[i]);
                    ReturnBlocks[i] = 0;
                }
            }
            Array.Clear(ReturnBlockCapacities, 0, returnBlockCount);
            returnBlockCount = 0;
        }

        returnBlockIndex = -1;
        returnCursor = 0;
    }

    private static int* AllocateReturnSlice(int count)
    {
        if (count <= 0)
            return null;

        if (returnBlockIndex < 0 || returnCursor + count > ReturnBlockCapacities[returnBlockIndex])
        {
            int nextCapacity = InitialReturnBlockCapacity;
            if (returnBlockIndex >= 0)
                nextCapacity = ReturnBlockCapacities[returnBlockIndex] * 2;
            if (nextCapacity < count)
                nextCapacity = count;

            if (returnBlockCount == MaxReturnBlocks)
                return null;

            ReturnBlocks[returnBlockCount] = (nint)NativeMemory.Alloc((nuint)nextCapacity, (nuint)sizeof(int));
            ReturnBlockCapacities[returnBlockCount] = nextCapacity;
            returnBlockIndex = returnBlockCount;
            returnBlockCount++;
            returnCursor = 0;
        }

        int* result = ((int*)ReturnBlocks[returnBlockIndex]) + returnCursor;
        returnCursor += count;
        return result;
    }

    private static bool IsValidTag(int tag)
    {
        if (tag <= 0 || tag >= nextTag)
            return false;
        var h = Handles[tag];
        return h.Alive != 0 &&
            h.SessionId == DefaultSessionId &&
            IsValidSlot(h.Pool, h.SlotIndex, h.Generation);
    }

    private static ref HandleRecord ResolveTag(int tag)
    {
        if (!IsValidTag(tag))
            throw new InvalidOperationException($"Invalid tag {tag}");
        return ref Handles[tag];
    }

    private static bool IsValidSlot(PoolKind pool, int slot, int generation)
    {
        return pool switch
        {
            PoolKind.Point => Points.IsValid(slot, generation),
            PoolKind.Vector => Vectors.IsValid(slot, generation),
            PoolKind.Body => Bodies.IsValid(slot, generation),
            PoolKind.Shell => Shells.IsValid(slot, generation),
            PoolKind.Face => Faces.IsValid(slot, generation),
            PoolKind.Loop => Loops.IsValid(slot, generation),
            PoolKind.Edge => Edges.IsValid(slot, generation),
            PoolKind.Fin => Fins.IsValid(slot, generation),
            PoolKind.Vertex => Vertices.IsValid(slot, generation),
            PoolKind.Region => Regions.IsValid(slot, generation),
            PoolKind.Curve => Curves.IsValid(slot, generation),
            PoolKind.Surface => Surfaces.IsValid(slot, generation),
            PoolKind.Transform => Transforms.IsValid(slot, generation),
            _ => false,
        };
    }

    // ── Session lifecycle ────────────────────────────────────────

    public static int SessionStart(PK_SESSION_start_o_s* options)
    {
        if (options is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (options->o_t_version != 1)
            return ParasolidConstants.PK_ERROR_o_t_version_incorrect;

        using var scope = RuntimeLock.EnterScope();
        if (session is not null && session.Started)
            return ParasolidConstants.PK_ERROR_rollback_started;

        session = new SessionState(DefaultSessionId) { Started = true };
        session.ResetPartitions();
        nextTag = 1;
        Array.Clear(Handles);
        ClearSlotToTagMaps();
        ResetReturnArena(freeBlocks: true);

        Points.Reset();
        Vectors.Reset();
        Bodies.Reset();
        Shells.Reset();
        FaceUses.Reset();
        Faces.Reset();
        Loops.Reset();
        Edges.Reset();
        Fins.Reset();
        Vertices.Reset();
        Regions.Reset();
        Curves.Reset();
        Surfaces.Reset();
        Transforms.Reset();
        CircleDataPool.Reset();
        LineDataPool.Reset();
        CylinderDataPool.Reset();
        PlaneDataPool.Reset();

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int SessionStop()
    {
        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        session.Started = false;
        session = null;
        nextTag = 1;
        Array.Clear(Handles);
        ClearSlotToTagMaps();
        ResetReturnArena(freeBlocks: true);

        Points.Reset();
        Vectors.Reset();
        Bodies.Reset();
        Shells.Reset();
        FaceUses.Reset();
        Faces.Reset();
        Loops.Reset();
        Edges.Reset();
        Fins.Reset();
        Vertices.Reset();
        Regions.Reset();
        Curves.Reset();
        Surfaces.Reset();
        Transforms.Reset();
        CircleDataPool.Reset();
        LineDataPool.Reset();
        CylinderDataPool.Reset();
        PlaneDataPool.Reset();

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_POINT_create ──────────────────────────────────────────

    public static int PointCreate(PK_POINT_sf_s* pointSf, int* pointTag)
    {
        if (pointSf is null || pointTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var slot = Points.Allocate();
        ref var rec = ref Points[slot];
        AssignPartition(ref rec.Header, CurrentPartition);
        // Zero-cost reinterpret: PK_VECTOR_s and KernelVector3 have identical layout
        rec.Position = Unsafe.As<PK_VECTOR_s, KernelVector3>(ref pointSf->position);

        var tag = AllocateTag(EntityClass.Point, PoolKind.Point, slot, rec.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *pointTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_ENTITY_ask_class ──────────────────────────────────────

    public static int EntityAskClass(int entityTag, int* classCode)
    {
        if (classCode is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (!IsValidTag(entityTag))
            return ParasolidConstants.PK_ERROR_unknown_class;

        *classCode = (int)Handles[entityTag].Class;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_BODY_create_topology_2 ────────────────────────────────

    public static int BodyCreateTopology2(
        int nTopols, PK_CLASS_t* classes,
        int nRelations, int* parents, int* children, int* senses,
        PK_BODY_create_topology_2_o_s* options,
        PK_BODY_create_topology_2_r_s* results)
    {
        if (classes is null || nTopols <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        // Allocate a body
        var bodySlot = Bodies.Allocate();
        ref var body = ref Bodies[bodySlot];
        body.BodyType = ParasolidConstants.PK_BODY_type_general_c;
        AssignPartition(ref body.Header, CurrentPartition);
        body.FirstShell = -1;
        body.LastShell = -1;
        body.FirstRegion = -1;
        body.LastRegion = -1;
        body.FirstFaceBody = -1;
        body.LastFaceBody = -1;
        body.FirstEdgeBody = -1;
        body.LastEdgeBody = -1;
        body.FirstVertexBody = -1;
        body.LastVertexBody = -1;

        // Allocate topology entities and record their pool slots
        // We use stack-allocated arrays for the slot mapping (max 256 topologies)
        const int MaxTopols = 256;
        if (nTopols > MaxTopols)
            return ParasolidConstants.PK_ERROR_general_body;

        int* slots = stackalloc int[nTopols];
        byte* poolKinds = stackalloc byte[nTopols]; // PoolKind for each topology

        // First pass: allocate all topology entities
        for (int i = 0; i < nTopols; i++)
        {
            var cls = classes[i];
            switch (cls)
            {
                case ParasolidConstants.PK_CLASS_shell:
                    slots[i] = Shells.Allocate();
                    AssignPartition(ref Shells[slots[i]].Header, CurrentPartition);
                    Shells[slots[i]].Body = -1;
                    Shells[slots[i]].Region = -1;
                    Shells[slots[i]].FirstFaceUseShell = -1;
                    Shells[slots[i]].LastFaceUseShell = -1;
                    Shells[slots[i]].AcornVertex = -1;
                    Shells[slots[i]].PrevInBody = -1;
                    Shells[slots[i]].NextInBody = -1;
                    Shells[slots[i]].PrevInRegion = -1;
                    Shells[slots[i]].NextInRegion = -1;
                    poolKinds[i] = (byte)PoolKind.Shell;
                    break;
                case ParasolidConstants.PK_CLASS_face:
                    slots[i] = Faces.Allocate();
                    AssignPartition(ref Faces[slots[i]].Header, CurrentPartition);
                    Faces[slots[i]].BackShell = -1;
                    Faces[slots[i]].FrontShell = -1;
                    Faces[slots[i]].BackFaceUse = -1;
                    Faces[slots[i]].FrontFaceUse = -1;
                    Faces[slots[i]].FirstLoop = -1;
                    Faces[slots[i]].LastLoop = -1;
                    Faces[slots[i]].PrevInBody = -1;
                    Faces[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Face;
                    break;
                case ParasolidConstants.PK_CLASS_loop:
                    slots[i] = Loops.Allocate();
                    AssignPartition(ref Loops[slots[i]].Header, CurrentPartition);
                    Loops[slots[i]].Face = -1;
                    Loops[slots[i]].FirstFin = -1;
                    Loops[slots[i]].LastFin = -1;
                    Loops[slots[i]].PrevInFace = -1;
                    Loops[slots[i]].NextInFace = -1;
                    poolKinds[i] = (byte)PoolKind.Loop;
                    break;
                case ParasolidConstants.PK_CLASS_edge:
                    slots[i] = Edges.Allocate();
                    AssignPartition(ref Edges[slots[i]].Header, CurrentPartition);
                    Edges[slots[i]].Body = -1;
                    Edges[slots[i]].StartVertex = -1;
                    Edges[slots[i]].EndVertex = -1;
                    Edges[slots[i]].FirstFinEdge = -1;
                    Edges[slots[i]].LastFinEdge = -1;
                    Edges[slots[i]].PrevInBody = -1;
                    Edges[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Edge;
                    break;
                case ParasolidConstants.PK_CLASS_fin:
                    slots[i] = Fins.Allocate();
                    AssignPartition(ref Fins[slots[i]].Header, CurrentPartition);
                    Fins[slots[i]].Edge = -1;
                    Fins[slots[i]].Loop = -1;
                    Fins[slots[i]].Face = -1;
                    Fins[slots[i]].NextInLoop = -1;
                    Fins[slots[i]].PrevInLoop = -1;
                    Fins[slots[i]].NextOfEdge = -1;
                    Fins[slots[i]].PrevOfEdge = -1;
                    Fins[slots[i]].Vertex = -1;
                    Fins[slots[i]].NextAtVertex = -1;
                    Fins[slots[i]].PrevAtVertex = -1;
                    poolKinds[i] = (byte)PoolKind.Fin;
                    break;
                case ParasolidConstants.PK_CLASS_vertex:
                    slots[i] = Vertices.Allocate();
                    AssignPartition(ref Vertices[slots[i]].Header, CurrentPartition);
                    Vertices[slots[i]].Body = -1;
                    Vertices[slots[i]].FirstFinVertex = -1;
                    Vertices[slots[i]].LastFinVertex = -1;
                    Vertices[slots[i]].PrevInBody = -1;
                    Vertices[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Vertex;
                    break;
                case ParasolidConstants.PK_CLASS_region:
                    slots[i] = Regions.Allocate();
                    AssignPartition(ref Regions[slots[i]].Header, CurrentPartition);
                    Regions[slots[i]].Body = -1;
                    Regions[slots[i]].IsSolid = 0;
                    Regions[slots[i]].FirstShell = -1;
                    Regions[slots[i]].LastShell = -1;
                    Regions[slots[i]].PrevInBody = -1;
                    Regions[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Region;
                    break;
                default:
                    return ParasolidConstants.PK_ERROR_bad_class;
            }
        }

        // Second pass: wire parent-child relationships
        for (int r = 0; r < nRelations; r++)
        {
            int pi = parents[r];   // parent topology index in the classes array (-1 = body)
            int ci = children[r];  // child topology index
            int sense = senses is not null ? senses[r] : 0;

            if (ci < 0 || ci >= nTopols)
                return ParasolidConstants.PK_ERROR_bad_field_number;
            if (pi < -1 || pi >= nTopols)
                return ParasolidConstants.PK_ERROR_bad_field_number;

            // Special case: pi == -1 means the body is the parent
            if (pi == -1)
            {
                var childPool = poolKinds[ci];
                var childSlot = slots[ci];
                WireRelation((byte)PoolKind.Body, bodySlot, childPool, childSlot, sense, bodySlot);
            }
            else
            {
                var parentPool = poolKinds[pi];
                var childPool = poolKinds[ci];
                var parentSlot = slots[pi];
                var childSlot = slots[ci];
                WireRelation(parentPool, parentSlot, childPool, childSlot, sense, bodySlot);
            }
        }

        // Assign body-level flat iteration (all faces/edges/vertices)
        AssignBodyFlatIteration(bodySlot, nTopols, classes, slots, poolKinds);
        AppendBodyToPartition(CurrentPartition, bodySlot);

        // Build result tags
        if (results is not null)
        {
            results->body = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        }

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static void WireRelation(
        byte parentPool, int parentSlot,
        byte childPool, int childSlot,
        int sense, int bodySlot)
    {
        switch ((PoolKind)parentPool)
        {
            case PoolKind.Body when childPool == (byte)PoolKind.Shell:
                {
                    ref var shell = ref Shells[childSlot];
                    shell.Body = parentSlot;
                    shell.Region = -1;
                    AppendShellToBody(parentSlot, childSlot);
                }
                break;

            case PoolKind.Body when childPool == (byte)PoolKind.Region:
                {
                    AppendRegionToBody(parentSlot, childSlot);
                }
                break;

            case PoolKind.Region when childPool == (byte)PoolKind.Shell:
                {
                    ref var region = ref Regions[parentSlot];
                    ref var shell = ref Shells[childSlot];
                    shell.Body = region.Body;
                    AppendShellToRegion(parentSlot, childSlot);
                }
                break;

            case PoolKind.Shell when childPool == (byte)PoolKind.Face:
                {
                    AddFaceUse(parentSlot, childSlot, sense);
                }
                break;

            case PoolKind.Face when childPool == (byte)PoolKind.Loop:
                {
                    ref var face = ref Faces[parentSlot];
                    ref var loop = ref Loops[childSlot];
                    loop.Face = parentSlot;
                    AppendLoopToFace(parentSlot, childSlot);
                }
                break;

            case PoolKind.Loop when childPool == (byte)PoolKind.Fin:
                {
                    ref var loop = ref Loops[parentSlot];
                    ref var fin = ref Fins[childSlot];
                    fin.Loop = parentSlot;
                    fin.Face = loop.Face;  // derive face from parent loop
                    AppendFinToLoop(parentSlot, childSlot);
                }
                break;

            case PoolKind.Edge when childPool == (byte)PoolKind.Fin:
                {
                    ref var fin = ref Fins[childSlot];
                    fin.Edge = parentSlot;
                    AppendFinToEdge(parentSlot, childSlot);
                }
                break;

            case PoolKind.Fin when childPool == (byte)PoolKind.Edge:
                Fins[parentSlot].Edge = childSlot;
                break;
        }
    }

    private static FaceUseSlot AddFaceUse(ShellSlot shellSlot, FaceSlot faceSlot, KernelSense sense)
    {
        var faceUseSlot = FaceUses.Allocate();
        ref var faceUse = ref FaceUses[faceUseSlot];
        faceUse.Shell = shellSlot;
        faceUse.Face = faceSlot;
        faceUse.Sense = NormalizeFaceUseSense(sense);
        faceUse.PrevInShell = -1;
        faceUse.NextInShell = -1;

        ref var shell = ref Shells[shellSlot];
        if (shell.FirstFaceUseShell < 0)
        {
            shell.FirstFaceUseShell = faceUseSlot;
            shell.LastFaceUseShell = faceUseSlot;
            faceUse.PrevInShell = faceUseSlot;
            faceUse.NextInShell = faceUseSlot;
        }
        else
        {
            var first = shell.FirstFaceUseShell;
            var last = shell.LastFaceUseShell;
            faceUse.PrevInShell = last;
            faceUse.NextInShell = first;
            FaceUses[last].NextInShell = faceUseSlot;
            FaceUses[first].PrevInShell = faceUseSlot;
            shell.LastFaceUseShell = faceUseSlot;
        }
        shell.FaceUseCount++;

        ref var face = ref Faces[faceSlot];
        if (faceUse.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c)
        {
            face.BackShell = shellSlot;
            face.BackFaceUse = faceUseSlot;
        }
        else
        {
            face.FrontShell = shellSlot;
            face.FrontFaceUse = faceUseSlot;
        }

        return faceUseSlot;
    }

    private static KernelSense NormalizeFaceUseSense(KernelSense sense)
    {
        return sense == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? ParasolidConstants.PK_TOPOL_sense_negative_c
            : ParasolidConstants.PK_TOPOL_sense_positive_c;
    }

    private static void AppendRegionToBody(BodySlot bodySlot, RegionSlot regionSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var region = ref Regions[regionSlot];
        region.Body = bodySlot;

        if (body.FirstRegion < 0)
        {
            body.FirstRegion = regionSlot;
            body.LastRegion = regionSlot;
            region.PrevInBody = regionSlot;
            region.NextInBody = regionSlot;
        }
        else
        {
            var first = body.FirstRegion;
            var last = body.LastRegion;
            region.PrevInBody = last;
            region.NextInBody = first;
            Regions[last].NextInBody = regionSlot;
            Regions[first].PrevInBody = regionSlot;
            body.LastRegion = regionSlot;
        }
        body.RegionCount++;
    }

    private static void AppendShellToBody(BodySlot bodySlot, ShellSlot shellSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var shell = ref Shells[shellSlot];
        shell.Body = bodySlot;

        if (body.FirstShell < 0)
        {
            body.FirstShell = shellSlot;
            body.LastShell = shellSlot;
            shell.PrevInBody = shellSlot;
            shell.NextInBody = shellSlot;
        }
        else
        {
            var first = body.FirstShell;
            var last = body.LastShell;
            shell.PrevInBody = last;
            shell.NextInBody = first;
            Shells[last].NextInBody = shellSlot;
            Shells[first].PrevInBody = shellSlot;
            body.LastShell = shellSlot;
        }
        body.ShellCount++;
    }

    private static void AppendShellToRegion(RegionSlot regionSlot, ShellSlot shellSlot)
    {
        ref var region = ref Regions[regionSlot];
        ref var shell = ref Shells[shellSlot];
        shell.Region = regionSlot;

        if (region.FirstShell < 0)
        {
            region.FirstShell = shellSlot;
            region.LastShell = shellSlot;
            shell.PrevInRegion = shellSlot;
            shell.NextInRegion = shellSlot;
        }
        else
        {
            var first = region.FirstShell;
            var last = region.LastShell;
            shell.PrevInRegion = last;
            shell.NextInRegion = first;
            Shells[last].NextInRegion = shellSlot;
            Shells[first].PrevInRegion = shellSlot;
            region.LastShell = shellSlot;
        }
        region.ShellCount++;
    }

    private static void AppendFaceToBody(BodySlot bodySlot, FaceSlot faceSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var face = ref Faces[faceSlot];
        if (body.FirstFaceBody < 0)
        {
            body.FirstFaceBody = faceSlot;
            body.LastFaceBody = faceSlot;
            face.PrevInBody = faceSlot;
            face.NextInBody = faceSlot;
        }
        else
        {
            var first = body.FirstFaceBody;
            var last = body.LastFaceBody;
            face.PrevInBody = last;
            face.NextInBody = first;
            Faces[last].NextInBody = faceSlot;
            Faces[first].PrevInBody = faceSlot;
            body.LastFaceBody = faceSlot;
        }
        body.FaceCountBody++;
    }

    private static void AppendEdgeToBody(BodySlot bodySlot, EdgeSlot edgeSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var edge = ref Edges[edgeSlot];
        edge.Body = bodySlot;
        if (body.FirstEdgeBody < 0)
        {
            body.FirstEdgeBody = edgeSlot;
            body.LastEdgeBody = edgeSlot;
            edge.PrevInBody = edgeSlot;
            edge.NextInBody = edgeSlot;
        }
        else
        {
            var first = body.FirstEdgeBody;
            var last = body.LastEdgeBody;
            edge.PrevInBody = last;
            edge.NextInBody = first;
            Edges[last].NextInBody = edgeSlot;
            Edges[first].PrevInBody = edgeSlot;
            body.LastEdgeBody = edgeSlot;
        }
        body.EdgeCountBody++;
    }

    private static void AppendVertexToBody(BodySlot bodySlot, VertexSlot vertexSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var vertex = ref Vertices[vertexSlot];
        vertex.Body = bodySlot;
        if (body.FirstVertexBody < 0)
        {
            body.FirstVertexBody = vertexSlot;
            body.LastVertexBody = vertexSlot;
            vertex.PrevInBody = vertexSlot;
            vertex.NextInBody = vertexSlot;
        }
        else
        {
            var first = body.FirstVertexBody;
            var last = body.LastVertexBody;
            vertex.PrevInBody = last;
            vertex.NextInBody = first;
            Vertices[last].NextInBody = vertexSlot;
            Vertices[first].PrevInBody = vertexSlot;
            body.LastVertexBody = vertexSlot;
        }
        body.VertexCountBody++;
    }

    private static void AppendLoopToFace(FaceSlot faceSlot, LoopSlot loopSlot)
    {
        ref var face = ref Faces[faceSlot];
        ref var loop = ref Loops[loopSlot];
        loop.Face = faceSlot;
        if (face.FirstLoop < 0)
        {
            face.FirstLoop = loopSlot;
            face.LastLoop = loopSlot;
            loop.PrevInFace = loopSlot;
            loop.NextInFace = loopSlot;
        }
        else
        {
            var first = face.FirstLoop;
            var last = face.LastLoop;
            loop.PrevInFace = last;
            loop.NextInFace = first;
            Loops[last].NextInFace = loopSlot;
            Loops[first].PrevInFace = loopSlot;
            face.LastLoop = loopSlot;
        }
        face.LoopCount++;
    }

    private static void AppendFinToLoop(LoopSlot loopSlot, FinSlot finSlot)
    {
        ref var loop = ref Loops[loopSlot];
        ref var fin = ref Fins[finSlot];
        fin.Loop = loopSlot;
        fin.Face = loop.Face;
        if (loop.FirstFin < 0)
        {
            loop.FirstFin = finSlot;
            loop.LastFin = finSlot;
            fin.PrevInLoop = finSlot;
            fin.NextInLoop = finSlot;
        }
        else
        {
            var first = loop.FirstFin;
            var last = loop.LastFin;
            fin.PrevInLoop = last;
            fin.NextInLoop = first;
            Fins[last].NextInLoop = finSlot;
            Fins[first].PrevInLoop = finSlot;
            loop.LastFin = finSlot;
        }
        loop.FinCount++;
    }

    private static void AppendFinToEdge(EdgeSlot edgeSlot, FinSlot finSlot)
    {
        ref var edge = ref Edges[edgeSlot];
        ref var fin = ref Fins[finSlot];
        fin.Edge = edgeSlot;
        if (edge.FirstFinEdge < 0)
        {
            edge.FirstFinEdge = finSlot;
            edge.LastFinEdge = finSlot;
            fin.PrevOfEdge = finSlot;
            fin.NextOfEdge = finSlot;
        }
        else
        {
            var first = edge.FirstFinEdge;
            var last = edge.LastFinEdge;
            fin.PrevOfEdge = last;
            fin.NextOfEdge = first;
            Fins[last].NextOfEdge = finSlot;
            Fins[first].PrevOfEdge = finSlot;
            edge.LastFinEdge = finSlot;
        }
        edge.FinCount++;
    }

    private static void AppendFinToVertex(VertexSlot vertexSlot, FinSlot finSlot)
    {
        if (vertexSlot < 0)
            return;

        ref var vertex = ref Vertices[vertexSlot];
        ref var fin = ref Fins[finSlot];
        fin.Vertex = vertexSlot;
        if (vertex.FirstFinVertex < 0)
        {
            vertex.FirstFinVertex = finSlot;
            vertex.LastFinVertex = finSlot;
            fin.PrevAtVertex = finSlot;
            fin.NextAtVertex = finSlot;
        }
        else
        {
            var first = vertex.FirstFinVertex;
            var last = vertex.LastFinVertex;
            fin.PrevAtVertex = last;
            fin.NextAtVertex = first;
            Fins[last].NextAtVertex = finSlot;
            Fins[first].PrevAtVertex = finSlot;
            vertex.LastFinVertex = finSlot;
        }
    }

    private static void AppendBodyToPartition(PartitionSlot partitionSlot, BodySlot bodySlot)
    {
        if (session is null)
            return;

        ref var partition = ref session.Partitions[partitionSlot];
        ref var body = ref Bodies[bodySlot];
        AssignPartition(ref body.Header, partitionSlot);

        if (partition.FirstBody < 0)
        {
            partition.FirstBody = bodySlot;
            partition.LastBody = bodySlot;
            body.PrevInPartition = bodySlot;
            body.NextInPartition = bodySlot;
        }
        else
        {
            var first = partition.FirstBody;
            var last = partition.LastBody;
            body.PrevInPartition = last;
            body.NextInPartition = first;
            Bodies[last].NextInPartition = bodySlot;
            Bodies[first].PrevInPartition = bodySlot;
            partition.LastBody = bodySlot;
        }

        partition.BodyCount++;
        PropagateBodyPartition(bodySlot, partitionSlot);
    }

    private static void PropagateBodyPartition(BodySlot bodySlot, PartitionSlot partitionSlot)
    {
        AssignPartition(ref Bodies[bodySlot].Header, partitionSlot);
        var body = Bodies[bodySlot];

        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
            AssignPartition(ref Regions[regionSlot].Header, partitionSlot);

        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            AssignPartition(ref Shells[shellSlot].Header, partitionSlot);
            var shell = Shells[shellSlot];
            var faceUseSlot = shell.FirstFaceUseShell;
            for (var j = 0; j < shell.FaceUseCount; j++, faceUseSlot = FaceUses[faceUseSlot].NextInShell)
                AssignPartition(ref FaceUses[faceUseSlot].Header, partitionSlot);
        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            AssignPartition(ref Faces[faceSlot].Header, partitionSlot);
            var face = Faces[faceSlot];
            if (face.SurfTag > 0)
            {
                var surfaceSlot = GetSurfaceSlotByTag(face.SurfTag);
                if (surfaceSlot >= 0)
                    AssignPartition(ref Surfaces[surfaceSlot].Header, partitionSlot);
            }

            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                AssignPartition(ref Loops[loopSlot].Header, partitionSlot);
                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                    AssignPartition(ref Fins[finSlot].Header, partitionSlot);
            }
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            AssignPartition(ref Edges[edgeSlot].Header, partitionSlot);
            if (Edges[edgeSlot].CurveTag > 0)
            {
                var curveSlot = GetCurveSlotByTag(Edges[edgeSlot].CurveTag);
                if (curveSlot >= 0)
                    AssignPartition(ref Curves[curveSlot].Header, partitionSlot);
            }
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            AssignPartition(ref Vertices[vertexSlot].Header, partitionSlot);
            if (Vertices[vertexSlot].PointTag > 0)
            {
                var pointSlot = GetPointSlotByTag(Vertices[vertexSlot].PointTag);
                if (pointSlot >= 0)
                    AssignPartition(ref Points[pointSlot].Header, partitionSlot);
            }
        }
    }

    private static VertexSlot EdgeFinVertex(FinSlot finSlot, EdgeRecord edge)
    {
        if (edge.StartVertex < 0 || edge.EndVertex < 0)
            return -1;

        return finSlot == edge.FirstFinEdge ? edge.StartVertex : edge.EndVertex;
    }

    private static void SetPointOwner(PointTag pointTag, VertexSlot ownerVertex)
    {
        var pointSlot = GetPointSlotByTag(pointTag);
        if (pointSlot < 0)
            return;

        ref var point = ref Points[pointSlot];
        point.OwnerVertex = ownerVertex;
        var bodySlot = Vertices[ownerVertex].Body;
        if (bodySlot < 0)
            return;

        var vertexSlot = Bodies[bodySlot].FirstVertexBody;
        for (var i = 0; i < Bodies[bodySlot].VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            if (vertexSlot == ownerVertex)
            {
                point.PrevInBody = VertexPointTag(Vertices[vertexSlot].PrevInBody);
                point.NextInBody = VertexPointTag(Vertices[vertexSlot].NextInBody);
                return;
            }
        }
    }

    private static void SetCurveOwner(CurveTag curveTag, EdgeSlot ownerEdge)
    {
        var curveSlot = GetCurveSlotByTag(curveTag);
        if (curveSlot < 0)
            return;

        ref var curve = ref Curves[curveSlot];
        curve.OwnerEdge = ownerEdge;
        var bodySlot = Edges[ownerEdge].Body;
        if (bodySlot < 0)
            return;

        var edgeSlot = Bodies[bodySlot].FirstEdgeBody;
        for (var i = 0; i < Bodies[bodySlot].EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            if (edgeSlot == ownerEdge)
            {
                curve.PrevInBody = EdgeCurveTag(Edges[edgeSlot].PrevInBody);
                curve.NextInBody = EdgeCurveTag(Edges[edgeSlot].NextInBody);
                return;
            }
        }
    }

    private static void SetSurfaceOwner(SurfTag surfaceTag, FaceSlot ownerFace)
    {
        var surfaceSlot = GetSurfaceSlotByTag(surfaceTag);
        if (surfaceSlot < 0)
            return;

        ref var surface = ref Surfaces[surfaceSlot];
        surface.OwnerFace = ownerFace;
        var bodySlot = Shells[Faces[ownerFace].BackShell >= 0 ? Faces[ownerFace].BackShell : Faces[ownerFace].FrontShell].Body;
        if (bodySlot < 0)
            return;

        var faceSlot = Bodies[bodySlot].FirstFaceBody;
        for (var i = 0; i < Bodies[bodySlot].FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            if (faceSlot == ownerFace)
            {
                surface.PrevInBody = FaceSurfaceTag(Faces[faceSlot].PrevInBody);
                surface.NextInBody = FaceSurfaceTag(Faces[faceSlot].NextInBody);
                return;
            }
        }
    }

    private static PointTag VertexPointTag(VertexSlot vertexSlot) => vertexSlot >= 0 ? Vertices[vertexSlot].PointTag : 0;
    private static CurveTag EdgeCurveTag(EdgeSlot edgeSlot) => edgeSlot >= 0 ? Edges[edgeSlot].CurveTag : 0;
    private static SurfTag FaceSurfaceTag(FaceSlot faceSlot) => faceSlot >= 0 ? Faces[faceSlot].SurfTag : 0;

    private static void RebuildBoundaryGeometryLinks(BodySlot bodySlot)
    {
        var body = Bodies[bodySlot];

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            var surfTag = Faces[faceSlot].SurfTag;
            var surfaceSlot = GetSurfaceSlotByTag(surfTag);
            if (surfaceSlot < 0)
                continue;

            ref var surface = ref Surfaces[surfaceSlot];
            surface.OwnerFace = faceSlot;
            surface.PrevInBody = FaceSurfaceTag(Faces[faceSlot].PrevInBody);
            surface.NextInBody = FaceSurfaceTag(Faces[faceSlot].NextInBody);
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            var curveTag = Edges[edgeSlot].CurveTag;
            var curveSlot = GetCurveSlotByTag(curveTag);
            if (curveSlot < 0)
                continue;

            ref var curve = ref Curves[curveSlot];
            curve.OwnerEdge = edgeSlot;
            curve.PrevInBody = EdgeCurveTag(Edges[edgeSlot].PrevInBody);
            curve.NextInBody = EdgeCurveTag(Edges[edgeSlot].NextInBody);
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            var pointTag = Vertices[vertexSlot].PointTag;
            var pointSlot = GetPointSlotByTag(pointTag);
            if (pointSlot < 0)
                continue;

            ref var point = ref Points[pointSlot];
            point.OwnerVertex = vertexSlot;
            point.PrevInBody = VertexPointTag(Vertices[vertexSlot].PrevInBody);
            point.NextInBody = VertexPointTag(Vertices[vertexSlot].NextInBody);
        }
    }

    /// <summary>
    /// Assign body-level flat iteration arrays.
    /// </summary>
    private static void AssignBodyFlatIteration(
        int bodySlot, int nTopols, PK_CLASS_t* classes, int* slots, byte* poolKinds)
    {
        ref var body = ref Bodies[bodySlot];
        body.FirstFaceBody = -1;
        body.FirstEdgeBody = -1;
        body.FirstVertexBody = -1;

        for (int i = 0; i < nTopols; i++)
        {
            var slot = slots[i];
            switch ((PoolKind)poolKinds[i])
            {
                case PoolKind.Face:
                    AppendFaceToBody(bodySlot, slot);
                    break;

                case PoolKind.Edge:
                    AppendEdgeToBody(bodySlot, slot);
                    break;

                case PoolKind.Vertex:
                    AppendVertexToBody(bodySlot, slot);
                    break;

                case PoolKind.Region:
                    Regions[slot].Body = bodySlot;
                    break;
            }
        }
    }

    // ── Query APIs ───────────────────────────────────────────────

    /// <summary>
    /// Which sibling chain to follow when traversing fins.
    /// </summary>
    private enum FinChain : byte
    {
        ByLoop = 0,  // FinRecord.NextInLoop  (for LoopAskFins)
        ByEdge = 1,  // FinRecord.NextOfEdge  (for EdgeAskFins)
    }

    /// <summary>
    /// Write tags into the session return arena (bump-allocated) and set the output pointer.
    /// Returns 0 on success, -1 if the arena is exhausted.
    /// </summary>
    private static int WriteTagList(int** output, int count, int startSlot, PoolKind pool, EntityClass entityClass, FinChain finChain = FinChain.ByLoop)
    {
        if (count <= 0)
        {
            *output = null;
            return 0;
        }

        int* buffer = AllocateReturnSlice(count);
        if (buffer is null)
            return -1;

        int slot = startSlot;
        for (int i = 0; i < count; i++)
        {
            if (slot < 0)
                return -1;
            int tag = GetOrAllocateTag(entityClass, pool, slot);
            if (tag <= 0)
                return -1;
            buffer[i] = tag;
            slot = GetNextInChain(pool, slot, finChain);
        }
        *output = buffer;
        return 0;
    }

    private static int WriteBodyTopologyList(
        BodySlot bodySlot,
        int* topols,
        int* classes,
        int maxTopols)
    {
        int index = 0;
        if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Body, PoolKind.Body, bodySlot))
            return -1;

        var body = Bodies[bodySlot];
        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Region, PoolKind.Region, regionSlot))
                return -1;
        }

        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Shell, PoolKind.Shell, shellSlot))
                return -1;

        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Face, PoolKind.Face, faceSlot))
                return -1;

            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Loop, PoolKind.Loop, loopSlot))
                    return -1;

                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                {
                    if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Fin, PoolKind.Fin, finSlot))
                        return -1;
                }
            }
        }

        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Edge, PoolKind.Edge, edgeSlot))
                return -1;
        }

        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = Vertices[vertexSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Vertex, PoolKind.Vertex, vertexSlot))
                return -1;
        }

        return index;
    }

    private static bool AppendTopology(
        ref int index,
        int maxTopols,
        int* topols,
        int* classes,
        EntityClass entityClass,
        PoolKind pool,
        int slot)
    {
        if (index == maxTopols)
            return false;

        topols[index] = GetOrAllocateTag(entityClass, pool, slot);
        if (topols[index] <= 0)
            return false;

        classes[index] = ToPkClass(entityClass);
        index++;
        return true;
    }

    private static int WriteBodyTopologyRelations(
        BodySlot bodySlot,
        int* topols,
        int topolCount,
        int* parents,
        int* children,
        int* senses,
        int maxRelations)
    {
        int relation = 0;
        int bodyTag = GetOrAllocateTag(EntityClass.Body, PoolKind.Body, bodySlot);
        if (bodyTag <= 0)
            return -1;

        var body = Bodies[bodySlot];
        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = Regions[regionSlot].NextInBody)
        {
            int regionTag = GetOrAllocateTag(EntityClass.Region, PoolKind.Region, regionSlot);
            if (!AppendRelation(ref relation, maxRelations, parents, children, senses, bodyTag, regionTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                return -1;

            var region = Regions[regionSlot];
            var shellSlot = region.FirstShell;
            for (var j = 0; j < region.ShellCount; j++, shellSlot = Shells[shellSlot].NextInRegion)
            {
                int shellTag = GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, shellSlot);
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, regionTag, shellTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;
            }
        }

        var bodyShellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, bodyShellSlot = Shells[bodyShellSlot].NextInBody)
        {
            if (Shells[bodyShellSlot].Region < 0)
            {
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, bodyTag, GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, bodyShellSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;
            }

            var shell = Shells[bodyShellSlot];
            var faceUseSlot = shell.FirstFaceUseShell;
            for (var j = 0; j < shell.FaceUseCount; j++, faceUseSlot = FaceUses[faceUseSlot].NextInShell)
            {
                ref var faceUse = ref FaceUses[faceUseSlot];
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, bodyShellSlot), GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceUse.Face), faceUse.Sense, topols, topolCount))
                    return -1;
            }
        }

        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceSlot), GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, loopSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;

                var loop = Loops[loopSlot];
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = Fins[finSlot].NextInLoop)
                {
                    if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, loopSlot), GetOrAllocateTag(EntityClass.Fin, PoolKind.Fin, finSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                        return -1;

                    if (Fins[finSlot].Edge >= 0)
                    {
                        int edgeTag = GetOrAllocateTag(EntityClass.Edge, PoolKind.Edge, Fins[finSlot].Edge);
                        int finTag = GetOrAllocateTag(EntityClass.Fin, PoolKind.Fin, finSlot);
                        if (!AppendRelation(ref relation, maxRelations, parents, children, senses, edgeTag, finTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                            return -1;
                    }
                }
            }
        }

        return relation;
    }

    private static bool AppendRelation(
        ref int relation,
        int maxRelations,
        int* parents,
        int* children,
        int* senses,
        int parentTag,
        int childTag,
        KernelSense sense,
        int* topols,
        int topolCount)
    {
        if (parentTag <= 0 || childTag <= 0 || relation == maxRelations)
            return false;

        int parentIndex = FindTagIndex(topols, topolCount, parentTag);
        int childIndex = FindTagIndex(topols, topolCount, childTag);
        if (parentIndex < 0 || childIndex < 0)
            return false;

        parents[relation] = parentIndex;
        children[relation] = childIndex;
        senses[relation] = sense;
        relation++;
        return true;
    }

    private static int FindTagIndex(int* topols, int count, int tag)
    {
        for (int i = 0; i < count; i++)
        {
            if (topols[i] == tag)
                return i;
        }

        return -1;
    }

    private static int ToPkClass(EntityClass entityClass)
    {
        return entityClass switch
        {
            EntityClass.Body => ParasolidConstants.PK_CLASS_body,
            EntityClass.Shell => ParasolidConstants.PK_CLASS_shell,
            EntityClass.Face => ParasolidConstants.PK_CLASS_face,
            EntityClass.Loop => ParasolidConstants.PK_CLASS_loop,
            EntityClass.Fin => ParasolidConstants.PK_CLASS_fin,
            EntityClass.Edge => ParasolidConstants.PK_CLASS_edge,
            EntityClass.Vertex => ParasolidConstants.PK_CLASS_vertex,
            EntityClass.Region => ParasolidConstants.PK_CLASS_region,
            EntityClass.Point => ParasolidConstants.PK_CLASS_point,
            EntityClass.Curve => ParasolidConstants.PK_CLASS_curve,
            EntityClass.Surface => ParasolidConstants.PK_CLASS_surf,
            _ => (int)entityClass,
        };
    }

    /// <summary>
    /// Follow the sibling chain for a pool type.
    /// </summary>
    private static int GetNextInChain(PoolKind pool, int slot, FinChain finChain = FinChain.ByLoop)
    {
        return pool switch
        {
            PoolKind.Shell => Shells[slot].NextInBody,
            PoolKind.Face => Faces[slot].NextInBody,
            PoolKind.Loop => Loops[slot].NextInFace,
            PoolKind.Fin => finChain == FinChain.ByEdge ? Fins[slot].NextOfEdge : Fins[slot].NextInLoop,
            PoolKind.Edge => Edges[slot].NextInBody,
            PoolKind.Vertex => Vertices[slot].NextInBody,
            PoolKind.Region => Regions[slot].NextInBody,
            _ => -1,
        };
    }

    public static int BodyAskShells(int bodyTag, int* nShells, int** shells)
    {
        if (nShells is null || shells is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[Handles[bodyTag].SlotIndex];
        *nShells = body.ShellCount;
        if (WriteTagList(shells, body.ShellCount, body.FirstShell, PoolKind.Shell, EntityClass.Shell, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int BodyAskFaces(int bodyTag, int* nFaces, int** faces)
    {
        if (nFaces is null || faces is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[Handles[bodyTag].SlotIndex];
        *nFaces = body.FaceCountBody;
        if (WriteTagList(faces, body.FaceCountBody, body.FirstFaceBody, PoolKind.Face, EntityClass.Face, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int BodyAskEdges(int bodyTag, int* nEdges, int** edges)
    {
        if (nEdges is null || edges is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[Handles[bodyTag].SlotIndex];
        *nEdges = body.EdgeCountBody;
        if (WriteTagList(edges, body.EdgeCountBody, body.FirstEdgeBody, PoolKind.Edge, EntityClass.Edge, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int BodyAskVertices(int bodyTag, int* nVertices, int** vertices)
    {
        if (nVertices is null || vertices is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[Handles[bodyTag].SlotIndex];
        *nVertices = body.VertexCountBody;
        if (WriteTagList(vertices, body.VertexCountBody, body.FirstVertexBody, PoolKind.Vertex, EntityClass.Vertex, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int BodyAskRegions(int bodyTag, int* nRegions, int** regions)
    {
        if (nRegions is null || regions is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var body = ref Bodies[Handles[bodyTag].SlotIndex];
        *nRegions = body.RegionCount;
        if (WriteTagList(regions, body.RegionCount, body.FirstRegion, PoolKind.Region, EntityClass.Region, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int RegionIsSolid(int regionTag, KernelLogical* isSolid)
    {
        if (isSolid is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(regionTag) || Handles[regionTag].Class != EntityClass.Region)
            return ParasolidConstants.PK_ERROR_unknown_class;

        *isSolid = Regions[Handles[regionTag].SlotIndex].IsSolid;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int EntityAskPartition(EntityTag entityTag, PartitionSlot* partition)
    {
        if (partition is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        var entityPartition = GetEntityPartition(entityTag);
        if (entityPartition < 0)
            return ParasolidConstants.PK_ERROR_unknown_class;

        *partition = entityPartition;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int SessionAskCurrentPartition(PartitionSlot* partition)
    {
        if (partition is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        *partition = session.CurrentPartition;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int BodyAskTopology(
        int bodyTag,
        PK_BODY_ask_topology_o_s* options,
        int* nTopols,
        nint* topols,
        nint* classes,
        int* nRelations,
        nint* parents,
        nint* children,
        nint* senses)
    {
        if (nTopols is null || topols is null || classes is null || nRelations is null || parents is null || children is null || senses is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(bodyTag) || Handles[bodyTag].Class != EntityClass.Body)
            return ParasolidConstants.PK_ERROR_unknown_class;

        int bodySlot = Handles[bodyTag].SlotIndex;
        ref var body = ref Bodies[bodySlot];
        int topolCount = 1 + body.RegionCount + body.ShellCount + body.FaceCountBody + body.EdgeCountBody + body.VertexCountBody;
        int finCount = 0;
        int faceUseCount = 0;
        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = Shells[shellSlot].NextInBody)
        {
            faceUseCount += Shells[shellSlot].FaceUseCount;
        }
        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            var face = Faces[faceSlot];
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = Loops[loopSlot].NextInFace)
            {
                topolCount += 1 + Loops[loopSlot].FinCount;
                finCount += Loops[loopSlot].FinCount;
            }
        }

        int edgeFinRelationCount = 0;
        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = Edges[edgeSlot].NextInBody)
        {
            edgeFinRelationCount += Edges[edgeSlot].FinCount;
        }

        int relationCount = body.RegionCount + body.ShellCount + faceUseCount + finCount + edgeFinRelationCount;
        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = Faces[faceSlot].NextInBody)
        {
            ref var face = ref Faces[faceSlot];
            relationCount += face.LoopCount;
        }

        int* topolBuffer = AllocateReturnSlice(topolCount);
        int* classBuffer = AllocateReturnSlice(topolCount);
        int* parentBuffer = AllocateReturnSlice(relationCount);
        int* childBuffer = AllocateReturnSlice(relationCount);
        int* senseBuffer = AllocateReturnSlice(relationCount);
        if (topolBuffer is null || classBuffer is null || parentBuffer is null || childBuffer is null || senseBuffer is null)
            return ParasolidConstants.PK_ERROR_general_body;

        int writtenTopols = WriteBodyTopologyList(bodySlot, topolBuffer, classBuffer, topolCount);
        if (writtenTopols != topolCount)
            return ParasolidConstants.PK_ERROR_general_body;

        int writtenRelations = WriteBodyTopologyRelations(bodySlot, topolBuffer, topolCount, parentBuffer, childBuffer, senseBuffer, relationCount);
        if (writtenRelations != relationCount)
            return ParasolidConstants.PK_ERROR_general_body;

        *nTopols = topolCount;
        *topols = (nint)topolBuffer;
        *classes = (nint)classBuffer;
        *nRelations = relationCount;
        *parents = (nint)parentBuffer;
        *children = (nint)childBuffer;
        *senses = (nint)senseBuffer;
        _ = options;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int FaceAskLoops(int faceTag, int* nLoops, int** loops)
    {
        if (nLoops is null || loops is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(faceTag) || Handles[faceTag].Class != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var face = ref Faces[Handles[faceTag].SlotIndex];
        *nLoops = face.LoopCount;
        if (WriteTagList(loops, face.LoopCount, face.FirstLoop, PoolKind.Loop, EntityClass.Loop, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int FaceAskSurf(int faceTag, int* surfTag)
    {
        if (surfTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(faceTag) || Handles[faceTag].Class != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var face = ref Faces[Handles[faceTag].SlotIndex];
        *surfTag = face.SurfTag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int FaceAskShells(int faceTag, int* shells)
    {
        if (shells is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(faceTag) || Handles[faceTag].Class != EntityClass.Face)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var face = ref Faces[Handles[faceTag].SlotIndex];
        shells[0] = face.BackShell >= 0 ? GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, face.BackShell) : 0;
        shells[1] = face.FrontShell >= 0 ? GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, face.FrontShell) : 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int LoopAskFace(int loopTag, int* faceTag)
    {
        if (faceTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(loopTag) || Handles[loopTag].Class != EntityClass.Loop)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var loop = ref Loops[Handles[loopTag].SlotIndex];
        *faceTag = GetOrAllocateTag(EntityClass.Face, PoolKind.Face, loop.Face);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int LoopAskFins(int loopTag, int* nFins, int** fins)
    {
        if (nFins is null || fins is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(loopTag) || Handles[loopTag].Class != EntityClass.Loop)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var loop = ref Loops[Handles[loopTag].SlotIndex];
        *nFins = loop.FinCount;
        if (WriteTagList(fins, loop.FinCount, loop.FirstFin, PoolKind.Fin, EntityClass.Fin, 0) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int EdgeAskFins(int edgeTag, int* nFins, int** fins)
    {
        if (nFins is null || fins is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(edgeTag) || Handles[edgeTag].Class != EntityClass.Edge)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var edge = ref Edges[Handles[edgeTag].SlotIndex];
        *nFins = edge.FinCount;
        if (WriteTagList(fins, edge.FinCount, edge.FirstFinEdge, PoolKind.Fin, EntityClass.Fin, FinChain.ByEdge) < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int EdgeAskCurve(int edgeTag, int* curveTag)
    {
        if (curveTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(edgeTag) || Handles[edgeTag].Class != EntityClass.Edge)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var edge = ref Edges[Handles[edgeTag].SlotIndex];
        *curveTag = edge.CurveTag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int VertexAskPoint(int vertexTag, int* pointTag)
    {
        if (pointTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(vertexTag) || Handles[vertexTag].Class != EntityClass.Vertex)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var vert = ref Vertices[Handles[vertexTag].SlotIndex];
        *pointTag = vert.PointTag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int FinAskEdge(int finTag, int* edgeTag)
    {
        if (edgeTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(finTag) || Handles[finTag].Class != EntityClass.Fin)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var fin = ref Fins[Handles[finTag].SlotIndex];
        *edgeTag = GetOrAllocateTag(EntityClass.Edge, PoolKind.Edge, fin.Edge);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int FinAskLoop(int finTag, int* loopTag)
    {
        if (loopTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(finTag) || Handles[finTag].Class != EntityClass.Fin)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var fin = ref Fins[Handles[finTag].SlotIndex];
        *loopTag = GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, fin.Loop);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int FinAskFace(int finTag, int* faceTag)
    {
        if (faceTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(finTag) || Handles[finTag].Class != EntityClass.Fin)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var fin = ref Fins[Handles[finTag].SlotIndex];
        *faceTag = GetOrAllocateTag(EntityClass.Face, PoolKind.Face, fin.Face);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_TRANSF_create ──────────────────────────────────────────

    public static int TransfCreate(PK_TRANSF_sf_s* transfSf, int* transfTag)
    {
        if (transfSf is null || transfTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var slot = Transforms.Allocate();
        ref var rec = ref Transforms[slot];
        AssignPartition(ref rec.Header, CurrentPartition);
        Unsafe.CopyBlock(ref Unsafe.As<double, byte>(ref rec.Matrix[0]),
                          ref Unsafe.As<double, byte>(ref transfSf->matrix[0]),
                          16 * sizeof(double));

        var tag = AllocateTag(EntityClass.Transform, PoolKind.Transform, slot, rec.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *transfTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int CylCreate(PK_CYL_sf_s* cylSf, int* cylTag)
    {
        if (cylSf is null || cylTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (cylSf->radius <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        ReadAxis2(&cylSf->basis_set, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        int dataSlot = CylinderDataPool.Allocate();
        ref var data = ref CylinderDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = cylSf->radius;

        int surfSlot = Surfaces.Allocate();
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Cylinder;
        surf.DataIndex = dataSlot;

        int tag = AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *cylTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int CylAsk(int cylTag, PK_CYL_sf_s* cylSf)
    {
        if (cylSf is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (!IsValidTag(cylTag) || Handles[cylTag].Class != EntityClass.Surface)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var surf = ref Surfaces[Handles[cylTag].SlotIndex];
        if (surf.Class != SurfaceClass.Cylinder)
            return ParasolidConstants.PK_ERROR_unknown_class;

        ref var data = ref CylinderDataPool[surf.DataIndex];
        cylSf->basis_set.location.coord[0] = data.LocationX;
        cylSf->basis_set.location.coord[1] = data.LocationY;
        cylSf->basis_set.location.coord[2] = data.LocationZ;
        cylSf->basis_set.axis.coord[0] = data.AxisX;
        cylSf->basis_set.axis.coord[1] = data.AxisY;
        cylSf->basis_set.axis.coord[2] = data.AxisZ;
        cylSf->basis_set.ref_direction.coord[0] = data.RefDirX;
        cylSf->basis_set.ref_direction.coord[1] = data.RefDirY;
        cylSf->basis_set.ref_direction.coord[2] = data.RefDirZ;
        cylSf->radius = data.Radius;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── PK_BODY_create_solid_block ─────────────────────────────────

    public static int BodyCreateSolidBlock(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (x <= 0 || y <= 0 || z <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSolidBlockCore(x, y, z, basisSet, bodyTag);
    }

    internal static int CreateSolidBlockCore(double x, double y, double z, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        var bodySlot = Bodies.Allocate();
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot);

        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);
        Cross(axX, axY, axZ, refX, refY, refZ, out double thirdX, out double thirdY, out double thirdZ);

        // 8 corner points of the block
        // p0 = origin
        // p1 = origin + x*ref
        // p2 = origin + x*ref + y*third
        // p3 = origin + y*third
        // p4..p7 = p0..p3 + z*axis
        Span<double> px = stackalloc double[8];
        Span<double> py = stackalloc double[8];
        Span<double> pz = stackalloc double[8];

        px[0] = ox; py[0] = oy; pz[0] = oz;
        px[1] = ox + x * refX; py[1] = oy + x * refY; pz[1] = oz + x * refZ;
        px[2] = px[1] + y * thirdX; py[2] = py[1] + y * thirdY; pz[2] = pz[1] + y * thirdZ;
        px[3] = ox + y * thirdX; py[3] = oy + y * thirdY; pz[3] = oz + y * thirdZ;
        for (int i = 0; i < 4; i++)
        {
            px[i + 4] = px[i] + z * axX;
            py[i + 4] = py[i] + z * axY;
            pz[i + 4] = pz[i] + z * axZ;
        }

        // Allocate 8 vertices
        Span<int> vtxSlots = stackalloc int[8];
        Span<int> pointTags = stackalloc int[8];
        for (int i = 0; i < 8; i++)
        {
            vtxSlots[i] = Vertices.Allocate();
            pointTags[i] = CreatePointTag(px[i], py[i], pz[i]);
            if (pointTags[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }
        for (int i = 0; i < 8; i++)
        {
            ref var vtx = ref Vertices[vtxSlots[i]];
            vtx.PointTag = pointTags[i];
            vtx.FirstFinVertex = -1;
            vtx.LastFinVertex = -1;
            AppendVertexToBody(bodySlot, vtxSlots[i]);
        }

        // Allocate 12 edges
        Span<int> edgeSlots = stackalloc int[12];
        Span<int> edgeCurveTags = stackalloc int[12];
        ReadOnlySpan<int> edgeVertexPairs = stackalloc int[24]
        {
            0, 1,
            1, 2,
            2, 3,
            3, 0,
            4, 5,
            5, 6,
            6, 7,
            7, 4,
            0, 4,
            1, 5,
            2, 6,
            3, 7,
        };
        for (int i = 0; i < 12; i++)
        {
            edgeSlots[i] = Edges.Allocate();
            int v0 = edgeVertexPairs[i * 2];
            int v1 = edgeVertexPairs[i * 2 + 1];
            edgeCurveTags[i] = CreateLineCurveTag(px[v0], py[v0], pz[v0], px[v1] - px[v0], py[v1] - py[v0], pz[v1] - pz[v0]);
            if (edgeCurveTags[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }
        for (int i = 0; i < 12; i++)
        {
            ref var edge = ref Edges[edgeSlots[i]];
            edge.Body = bodySlot;
            edge.StartVertex = vtxSlots[edgeVertexPairs[i * 2]];
            edge.EndVertex = vtxSlots[edgeVertexPairs[i * 2 + 1]];
            edge.CurveTag = edgeCurveTags[i];
            edge.FirstFinEdge = -1;
            edge.LastFinEdge = -1;
            AppendEdgeToBody(bodySlot, edgeSlots[i]);
        }

        // Face definitions: 6 faces, each with 4 edge indices
        // Face 0: bottom (z=0)  edges 0,1,2,3
        // Face 1: top (z=h)     edges 4,5,6,7
        // Face 2: front         edges 0,9,4,8
        // Face 3: right         edges 1,10,5,9
        // Face 4: back          edges 2,11,6,10
        // Face 5: left          edges 3,8,7,11
        ReadOnlySpan<int> faceEdgeIndices = stackalloc int[24]
        {
            0, 1, 2, 3,
            4, 5, 6, 7,
            0, 9, 4, 8,
            1, 10, 5, 9,
            2, 11, 6, 10,
            3, 8, 7, 11,
        };

        Span<int> faceSlots = stackalloc int[6];
        Span<int> loopSlots = stackalloc int[6];
        Span<int> faceSurfTags = stackalloc int[6];
        faceSurfTags[0] = CreatePlaneSurfaceTag(px[0], py[0], pz[0], -axX, -axY, -axZ, -refX, -refY, -refZ);
        faceSurfTags[1] = CreatePlaneSurfaceTag(px[4], py[4], pz[4], axX, axY, axZ, refX, refY, refZ);
        faceSurfTags[2] = CreatePlaneSurfaceTag(px[0], py[0], pz[0], -thirdX, -thirdY, -thirdZ, axX, axY, axZ);
        faceSurfTags[3] = CreatePlaneSurfaceTag(px[1], py[1], pz[1], refX, refY, refZ, axX, axY, axZ);
        faceSurfTags[4] = CreatePlaneSurfaceTag(px[2], py[2], pz[2], thirdX, thirdY, thirdZ, axX, axY, axZ);
        faceSurfTags[5] = CreatePlaneSurfaceTag(px[3], py[3], pz[3], -refX, -refY, -refZ, axX, axY, axZ);
        for (int i = 0; i < 6; i++)
        {
            if (faceSurfTags[i] <= 0)
                return ParasolidConstants.PK_ERROR_general_body;
        }

        for (int f = 0; f < 6; f++)
        {
            faceSlots[f] = Faces.Allocate();
            loopSlots[f] = Loops.Allocate();

            ref var face = ref Faces[faceSlots[f]];
            ref var loop = ref Loops[loopSlots[f]];

            InitializeFace(ref face);
            face.SurfTag = faceSurfTags[f];
            AppendFaceToBody(bodySlot, faceSlots[f]);

            loop.Face = faceSlots[f];
            loop.FirstFin = -1;
            loop.LastFin = -1;
            loop.PrevInFace = -1;
            loop.NextInFace = -1;
            AppendLoopToFace(faceSlots[f], loopSlots[f]);

            // Allocate 4 fins per loop
            for (int e = 0; e < 4; e++)
            {
                int ei = faceEdgeIndices[f * 4 + e];
                int finSlot = Fins.Allocate();
                ref var fin = ref Fins[finSlot];

                fin.Edge = edgeSlots[ei];
                fin.Loop = loopSlots[f];
                fin.Face = faceSlots[f];
                fin.NextInLoop = fin.PrevInLoop = -1;
                fin.NextOfEdge = fin.PrevOfEdge = -1;
                fin.Vertex = -1;
                fin.NextAtVertex = fin.PrevAtVertex = -1;

                AppendFinToLoop(loopSlots[f], finSlot);
                AppendFinToEdge(edgeSlots[ei], finSlot);

                var nextEi = faceEdgeIndices[f * 4 + ((e + 1) & 3)];
                var edgeStart = edgeVertexPairs[ei * 2];
                var edgeEnd = edgeVertexPairs[ei * 2 + 1];
                var nextStart = edgeVertexPairs[nextEi * 2];
                var nextEnd = edgeVertexPairs[nextEi * 2 + 1];
                var finVertex = edgeEnd == nextStart || edgeEnd == nextEnd ? edgeEnd : edgeStart;
                AppendFinToVertex(vtxSlots[finVertex], finSlot);
            }

            AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c);
            AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c);
        }

        // Build result tag
        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *bodyTag = tag;
        RebuildBoundaryGeometryLinks(bodySlot);
        AppendBodyToPartition(CurrentPartition, bodySlot);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int BodyCreateSolidCyl(double radius, double height, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        if (bodyTag is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (radius <= 0 || height <= 0)
            return ParasolidConstants.PK_ERROR_distance_le_0;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        return CreateSolidCylinderCore(radius, height, basisSet, bodyTag);
    }

    public static int PartTransmitB(int nParts, EntityTag* parts, PK_PART_transmit_o_s* options, PK_MEMORY_block_t* block)
    {
        if (nParts <= 0 || parts is null || block is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (options is not null)
        {
            if (options->transmit_format != 0 && options->transmit_format != ParasolidConstants.PK_transmit_format_text_c)
                return ParasolidConstants.PK_ERROR_bad_file_format;
            if (options->transmit_user_fields != 0)
                return ParasolidConstants.PK_ERROR_bad_file_format;
            if (options->transmit_indexed_context != 0)
                return ParasolidConstants.PK_ERROR_bad_file_format;
            if (options->transmit_meshes != 0 && options->transmit_meshes != ParasolidConstants.PK_transmit_meshes_separate_c)
                return ParasolidConstants.PK_ERROR_bad_file_format;
        }

        var partList = new EntityTag[nParts];
        for (var i = 0; i < nParts; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (partList[j] == parts[i])
                    return ParasolidConstants.PK_ERROR_duplicate_parts;
            }

            if (!TryResolveBodySlot(parts[i], out _))
                return ParasolidConstants.PK_ERROR_unsuitable_entity;
            partList[i] = parts[i];
        }

        var error = XtWriter.WriteText(partList, out var text);
        if (error != ParasolidConstants.PK_ERROR_no_errors)
            return error;

        var byteCount = System.Text.Encoding.ASCII.GetByteCount(text);
        var bytes = (byte*)NativeMemory.Alloc((nuint)byteCount);
        if (bytes is null)
            return ParasolidConstants.PK_ERROR_write_memory_full;
        System.Text.Encoding.ASCII.GetBytes(text, new Span<byte>(bytes, byteCount));

        block->next = null;
        block->n_bytes = (nuint)byteCount;
        block->bytes = bytes;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int PartReceiveB(PK_MEMORY_block_t block, PK_PART_receive_o_s* options, int* nParts, EntityTag** parts)
    {
        if (nParts is null || parts is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (options is not null)
        {
            if (options->transmit_format != 0 && options->transmit_format != ParasolidConstants.PK_transmit_format_text_c)
                return ParasolidConstants.PK_ERROR_wrong_format;
            if (options->receive_user_fields != 0)
                return ParasolidConstants.PK_ERROR_bad_file_format;
        }

        var builder = new System.Text.StringBuilder((int)Math.Min(block.n_bytes, 1_000_000));
        for (var current = &block; current is not null; current = current->next)
        {
            if (current->bytes is null || current->n_bytes == 0)
                continue;
            builder.Append(System.Text.Encoding.ASCII.GetString(current->bytes, checked((int)current->n_bytes)));
        }

        var error = XtReader.ReadText(builder.ToString(), out var received);
        if (error != ParasolidConstants.PK_ERROR_no_errors)
            return error;

        var buffer = (EntityTag*)NativeMemory.Alloc((nuint)received.Length, (nuint)sizeof(EntityTag));
        if (buffer is null)
            return ParasolidConstants.PK_ERROR_write_memory_full;

        for (var i = 0; i < received.Length; i++)
            buffer[i] = received[i];

        *nParts = received.Length;
        *parts = buffer;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int MemoryBlockFree(PK_MEMORY_block_t* block)
    {
        if (block is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        var current = block;
        while (current is not null)
        {
            if (current->bytes is not null)
            {
                NativeMemory.Free(current->bytes);
                current->bytes = null;
            }

            var next = current->next;
            if (current != block)
                NativeMemory.Free(current);
            current = next;
        }

        block->next = null;
        block->n_bytes = 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int MemoryFree(void* pointer)
    {
        if (pointer is not null)
            NativeMemory.Free(pointer);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    internal static int CreateSolidCylinderCore(double radius, double height, PK_AXIS2_sf_s* basisSet, int* bodyTag)
    {
        var bodySlot = Bodies.Allocate();
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);
        AssignPartition(ref body.Header, CurrentPartition);

        CreateSolidRegionsAndShells(bodySlot, out var voidShellSlot, out var solidShellSlot);
        ReadAxis2(basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ);

        int sideSurf = CreateCylinderSurfaceTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, radius);
        int bottomSurf = CreatePlaneSurfaceTag(ox, oy, oz, -axX, -axY, -axZ, refX, refY, refZ);
        int topSurf = CreatePlaneSurfaceTag(ox + height * axX, oy + height * axY, oz + height * axZ, axX, axY, axZ, refX, refY, refZ);
        if (sideSurf <= 0 || bottomSurf <= 0 || topSurf <= 0)
            return ParasolidConstants.PK_ERROR_general_body;

        Span<int> edgeSlots = stackalloc int[2];
        Span<int> edgeCurves = stackalloc int[2];
        edgeCurves[0] = CreateCircleCurveTag(ox, oy, oz, axX, axY, axZ, refX, refY, refZ, radius);
        edgeCurves[1] = CreateCircleCurveTag(ox + height * axX, oy + height * axY, oz + height * axZ, -axX, -axY, -axZ, refX, refY, refZ, radius);
        if (edgeCurves[0] <= 0 || edgeCurves[1] <= 0)
            return ParasolidConstants.PK_ERROR_general_body;

        for (int i = 0; i < 2; i++)
        {
            edgeSlots[i] = Edges.Allocate();
        }
        for (int i = 0; i < 2; i++)
        {
            ref var edge = ref Edges[edgeSlots[i]];
            edge.Body = bodySlot;
            edge.StartVertex = -1;
            edge.EndVertex = -1;
            edge.CurveTag = edgeCurves[i];
            edge.FirstFinEdge = -1;
            edge.LastFinEdge = -1;
            AppendEdgeToBody(bodySlot, edgeSlots[i]);
        }
        body.FirstVertexBody = -1;
        body.LastVertexBody = -1;
        body.VertexCountBody = 0;

        Span<int> faceSlots = stackalloc int[3];
        Span<int> surfTags = stackalloc int[3] { sideSurf, bottomSurf, topSurf };
        Span<int> loopCounts = stackalloc int[3] { 2, 1, 1 };

        for (int f = 0; f < 3; f++)
        {
            faceSlots[f] = Faces.Allocate();
        }
        for (int f = 0; f < 3; f++)
        {
            ref var face = ref Faces[faceSlots[f]];
            InitializeFace(ref face);
            face.SurfTag = surfTags[f];
            AppendFaceToBody(bodySlot, faceSlots[f]);

            for (int l = 0; l < loopCounts[f]; l++)
            {
                int loopSlot = Loops.Allocate();
                ref var loop = ref Loops[loopSlot];
                loop.Face = faceSlots[f];
                loop.FirstFin = -1;
                loop.LastFin = -1;
                loop.PrevInFace = -1;
                loop.NextInFace = -1;
                AppendLoopToFace(faceSlots[f], loopSlot);

                int edgeIndex = f == 0 ? l : f - 1;
                AddFinToLoopAndEdge(loopSlot, faceSlots[f], edgeSlots[edgeIndex]);
            }

            AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c);
            AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c);
        }

        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *bodyTag = tag;
        RebuildBoundaryGeometryLinks(bodySlot);
        AppendBodyToPartition(CurrentPartition, bodySlot);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── Mark / Rollback ──────────────────────────────────────────

    public static int MarkCreate(int* mark)
    {
        if (mark is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (session.HasMark)
            return ParasolidConstants.PK_ERROR_rollback_started; // nested marks not supported yet

        ref var m = ref session.CurrentMark;
        m.SequenceNo = session.NextRollbackStamp++;
        m.HandleCount = nextTag;
        m.PoolCounts[PoolHandles] = nextTag;
        m.PoolCounts[PoolPoints] = Points.AllocatedCount;
        m.PoolCounts[PoolVectors] = Vectors.AllocatedCount;
        m.PoolCounts[PoolBodies] = Bodies.AllocatedCount;
        m.PoolCounts[PoolShells] = Shells.AllocatedCount;
        m.PoolCounts[PoolFaceUses] = FaceUses.AllocatedCount;
        m.PoolCounts[PoolFaces] = Faces.AllocatedCount;
        m.PoolCounts[PoolLoops] = Loops.AllocatedCount;
        m.PoolCounts[PoolEdges] = Edges.AllocatedCount;
        m.PoolCounts[PoolFins] = Fins.AllocatedCount;
        m.PoolCounts[PoolVertices] = Vertices.AllocatedCount;
        m.PoolCounts[PoolRegions] = Regions.AllocatedCount;
        m.PoolCounts[PoolCurves] = Curves.AllocatedCount;
        m.PoolCounts[PoolSurfaces] = Surfaces.AllocatedCount;
        m.PoolCounts[PoolTransforms] = Transforms.AllocatedCount;
        m.PoolCounts[PoolCircleData] = CircleDataPool.AllocatedCount;
        m.PoolCounts[PoolCylinderData] = CylinderDataPool.AllocatedCount;
        m.PoolCounts[PoolPlaneData] = PlaneDataPool.AllocatedCount;
        m.PoolCounts[PoolLineData] = LineDataPool.AllocatedCount;
        m.RollbackStamp = session.NextRollbackStamp;

        session.HasMark = true;
        session.TombstoneCount = 0;

        *mark = m.SequenceNo;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int MarkGoto(int mark)
    {
        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (!session.HasMark || session.CurrentMark.SequenceNo != mark)
            return ParasolidConstants.PK_ERROR_bad_mark;

        ref var m = ref session.CurrentMark;

        Array.Clear(Handles, m.HandleCount, nextTag - m.HandleCount);

        // Restore all entity pools
        Points.RestoreMark(m.PoolCounts[PoolPoints]);
        Vectors.RestoreMark(m.PoolCounts[PoolVectors]);
        Bodies.RestoreMark(m.PoolCounts[PoolBodies]);
        Shells.RestoreMark(m.PoolCounts[PoolShells]);
        FaceUses.RestoreMark(m.PoolCounts[PoolFaceUses]);
        Faces.RestoreMark(m.PoolCounts[PoolFaces]);
        Loops.RestoreMark(m.PoolCounts[PoolLoops]);
        Edges.RestoreMark(m.PoolCounts[PoolEdges]);
        Fins.RestoreMark(m.PoolCounts[PoolFins]);
        Vertices.RestoreMark(m.PoolCounts[PoolVertices]);
        Regions.RestoreMark(m.PoolCounts[PoolRegions]);
        Curves.RestoreMark(m.PoolCounts[PoolCurves]);
        Surfaces.RestoreMark(m.PoolCounts[PoolSurfaces]);
        Transforms.RestoreMark(m.PoolCounts[PoolTransforms]);
        CircleDataPool.RestoreMark(m.PoolCounts[PoolCircleData]);
        CylinderDataPool.RestoreMark(m.PoolCounts[PoolCylinderData]);
        PlaneDataPool.RestoreMark(m.PoolCounts[PoolPlaneData]);
        LineDataPool.RestoreMark(m.PoolCounts[PoolLineData]);

        // Restore deleted entities (tombstones)
        for (int i = 0; i < session.TombstoneCount; i++)
        {
            ref var ts = ref session.Tombstones[i];
            if (ts.Slot < m.PoolCounts[ts.PoolIndex])
            {
                // This entity was alive at mark time — restore it
                RestoreEntity(ts.PoolIndex, ts.Slot, ts.HandleTag);
            }
        }

        session.HasMark = false;
        session.ClearTombstones();
        ResetReturnArena(freeBlocks: true); // all previously returned pointers are now invalid

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int MarkDelete(int mark)
    {
        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        if (!session.HasMark || session.CurrentMark.SequenceNo != mark)
            return ParasolidConstants.PK_ERROR_bad_mark;

        for (int i = 0; i < session.TombstoneCount; i++)
        {
            ref var ts = ref session.Tombstones[i];
            RecycleRetiredEntity(ts.PoolIndex, ts.Slot);
        }

        session.HasMark = false;
        session.ClearTombstones();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── Entity delete ────────────────────────────────────────────

    public static int EntityDelete(int nEntities, int* entities)
    {
        if (entities is null || nEntities <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;

        using var scope = RuntimeLock.EnterScope();
        if (session is null || !session.Started)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        for (int i = 0; i < nEntities; i++)
        {
            var tag = entities[i];
            if (!IsValidTag(tag))
                return ParasolidConstants.PK_ERROR_unknown_class;

            ref var handle = ref Handles[tag];
            var poolIndex = (int)handle.Pool;
            var slot = handle.SlotIndex;
            var generation = handle.Generation;

            // Record tombstone if we have an active mark
            if (session.HasMark)
                session.AddTombstone(poolIndex, slot, generation, tag);

            if (session.HasMark)
                RetireEntity(poolIndex, slot);
            else
                KillEntity(poolIndex, slot);

            // Kill the handle
            handle.Alive = 0;
        }

        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static void KillEntity(int poolIndex, int slot)
    {
        switch ((PoolKind)poolIndex)
        {
            case PoolKind.Point: Points.Free(slot); break;
            case PoolKind.Vector: Vectors.Free(slot); break;
            case PoolKind.Body: Bodies.Free(slot); break;
            case PoolKind.Shell: Shells.Free(slot); break;
            case PoolKind.Face: Faces.Free(slot); break;
            case PoolKind.Loop: Loops.Free(slot); break;
            case PoolKind.Edge: Edges.Free(slot); break;
            case PoolKind.Fin: Fins.Free(slot); break;
            case PoolKind.Vertex: Vertices.Free(slot); break;
            case PoolKind.Region: Regions.Free(slot); break;
            case PoolKind.Curve: Curves.Free(slot); break;
            case PoolKind.Surface: Surfaces.Free(slot); break;
            case PoolKind.Transform: Transforms.Free(slot); break;
        }
    }

    private static void RetireEntity(int poolIndex, int slot)
    {
        switch ((PoolKind)poolIndex)
        {
            case PoolKind.Point: Points.Retire(slot); break;
            case PoolKind.Vector: Vectors.Retire(slot); break;
            case PoolKind.Body: Bodies.Retire(slot); break;
            case PoolKind.Shell: Shells.Retire(slot); break;
            case PoolKind.Face: Faces.Retire(slot); break;
            case PoolKind.Loop: Loops.Retire(slot); break;
            case PoolKind.Edge: Edges.Retire(slot); break;
            case PoolKind.Fin: Fins.Retire(slot); break;
            case PoolKind.Vertex: Vertices.Retire(slot); break;
            case PoolKind.Region: Regions.Retire(slot); break;
            case PoolKind.Curve: Curves.Retire(slot); break;
            case PoolKind.Surface: Surfaces.Retire(slot); break;
            case PoolKind.Transform: Transforms.Retire(slot); break;
        }
    }

    private static void RecycleRetiredEntity(int poolIndex, int slot)
    {
        switch ((PoolKind)poolIndex)
        {
            case PoolKind.Point: Points.RecycleRetired(slot); break;
            case PoolKind.Vector: Vectors.RecycleRetired(slot); break;
            case PoolKind.Body: Bodies.RecycleRetired(slot); break;
            case PoolKind.Shell: Shells.RecycleRetired(slot); break;
            case PoolKind.Face: Faces.RecycleRetired(slot); break;
            case PoolKind.Loop: Loops.RecycleRetired(slot); break;
            case PoolKind.Edge: Edges.RecycleRetired(slot); break;
            case PoolKind.Fin: Fins.RecycleRetired(slot); break;
            case PoolKind.Vertex: Vertices.RecycleRetired(slot); break;
            case PoolKind.Region: Regions.RecycleRetired(slot); break;
            case PoolKind.Curve: Curves.RecycleRetired(slot); break;
            case PoolKind.Surface: Surfaces.RecycleRetired(slot); break;
            case PoolKind.Transform: Transforms.RecycleRetired(slot); break;
        }
    }

    private static void RestoreEntity(int poolIndex, int slot, int handleTag)
    {
        // Restore the entity's alive bit
        switch ((PoolKind)poolIndex)
        {
            case PoolKind.Point: RestoreSlot(ref Points, slot); break;
            case PoolKind.Vector: RestoreSlot(ref Vectors, slot); break;
            case PoolKind.Body: RestoreSlot(ref Bodies, slot); break;
            case PoolKind.Shell: RestoreSlot(ref Shells, slot); break;
            case PoolKind.Face: RestoreSlot(ref Faces, slot); break;
            case PoolKind.Loop: RestoreSlot(ref Loops, slot); break;
            case PoolKind.Edge: RestoreSlot(ref Edges, slot); break;
            case PoolKind.Fin: RestoreSlot(ref Fins, slot); break;
            case PoolKind.Vertex: RestoreSlot(ref Vertices, slot); break;
            case PoolKind.Region: RestoreSlot(ref Regions, slot); break;
            case PoolKind.Curve: RestoreSlot(ref Curves, slot); break;
            case PoolKind.Surface: RestoreSlot(ref Surfaces, slot); break;
            case PoolKind.Transform: RestoreSlot(ref Transforms, slot); break;
        }

        // Restore the handle
        if (handleTag > 0 && handleTag < MaxHandles)
            Handles[handleTag].Alive = 1;
    }

    private static void RestoreSlot<T>(ref EntityPool<T> pool, int slot) where T : struct
    {
        pool.MarkAlive(slot);
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Allocate a tag for an existing entity slot (for query result arrays).
    /// Does not allocate a new pool slot — just creates a tag pointing to an existing one.
    /// </summary>
    private static int AllocateEntityTag(EntityClass entityClass, PoolKind pool, int slotIndex)
    {
        int generation = pool switch
        {
            PoolKind.Point => Points.GetGeneration(slotIndex),
            PoolKind.Vector => Vectors.GetGeneration(slotIndex),
            PoolKind.Body => Bodies.GetGeneration(slotIndex),
            PoolKind.Shell => Shells.GetGeneration(slotIndex),
            PoolKind.Face => Faces.GetGeneration(slotIndex),
            PoolKind.Loop => Loops.GetGeneration(slotIndex),
            PoolKind.Edge => Edges.GetGeneration(slotIndex),
            PoolKind.Fin => Fins.GetGeneration(slotIndex),
            PoolKind.Vertex => Vertices.GetGeneration(slotIndex),
            PoolKind.Region => Regions.GetGeneration(slotIndex),
            PoolKind.Curve => Curves.GetGeneration(slotIndex),
            PoolKind.Surface => Surfaces.GetGeneration(slotIndex),
            PoolKind.Transform => Transforms.GetGeneration(slotIndex),
            _ => 0,
        };
        return AllocateTag(entityClass, pool, slotIndex, generation);
    }

    private static void InitializeBody(ref BodyRecord body)
    {
        body.BodyType = ParasolidConstants.PK_BODY_type_solid_c;
        body.BodyConfig = ParasolidConstants.PK_BODY_config_standard_c;
        body.FirstShell = -1;
        body.LastShell = -1;
        body.ShellCount = 0;
        body.FirstRegion = -1;
        body.LastRegion = -1;
        body.RegionCount = 0;
        body.FirstFaceBody = -1;
        body.LastFaceBody = -1;
        body.FaceCountBody = 0;
        body.FirstEdgeBody = -1;
        body.LastEdgeBody = -1;
        body.EdgeCountBody = 0;
        body.FirstVertexBody = -1;
        body.LastVertexBody = -1;
        body.VertexCountBody = 0;
        body.PrevInPartition = -1;
        body.NextInPartition = -1;
    }

    private static PartitionSlot CurrentPartition => session?.CurrentPartition ?? 0;

    private static void AssignPartition(ref RecordHeader header, PartitionSlot partition)
    {
        header.Partition = (short)partition;
    }

    private static PartitionSlot GetEntityPartition(EntityTag entityTag)
    {
        if (!IsValidTag(entityTag))
            return -1;

        var handle = Handles[entityTag];
        return handle.Pool switch
        {
            PoolKind.Point => Points[handle.SlotIndex].Header.Partition,
            PoolKind.Vector => Vectors[handle.SlotIndex].Header.Partition,
            PoolKind.Body => Bodies[handle.SlotIndex].Header.Partition,
            PoolKind.Shell => Shells[handle.SlotIndex].Header.Partition,
            PoolKind.Face => Faces[handle.SlotIndex].Header.Partition,
            PoolKind.Loop => Loops[handle.SlotIndex].Header.Partition,
            PoolKind.Edge => Edges[handle.SlotIndex].Header.Partition,
            PoolKind.Fin => Fins[handle.SlotIndex].Header.Partition,
            PoolKind.Vertex => Vertices[handle.SlotIndex].Header.Partition,
            PoolKind.Region => Regions[handle.SlotIndex].Header.Partition,
            PoolKind.Curve => Curves[handle.SlotIndex].Header.Partition,
            PoolKind.Surface => Surfaces[handle.SlotIndex].Header.Partition,
            PoolKind.Transform => Transforms[handle.SlotIndex].Header.Partition,
            _ => -1,
        };
    }

    private static void InitializeShell(ref ShellRecord shell, BodySlot bodySlot)
    {
        shell.Body = bodySlot;
        shell.Region = -1;
        shell.ShellType = 0;
        shell.FirstFaceUseShell = -1;
        shell.LastFaceUseShell = -1;
        shell.FaceUseCount = 0;
        shell.AcornVertex = -1;
        shell.PrevInBody = -1;
        shell.NextInBody = -1;
        shell.PrevInRegion = -1;
        shell.NextInRegion = -1;
    }

    private static void InitializeFace(ref FaceRecord face)
    {
        face.BackShell = -1;
        face.FrontShell = -1;
        face.BackFaceUse = -1;
        face.FrontFaceUse = -1;
        face.FirstLoop = -1;
        face.LastLoop = -1;
        face.LoopCount = 0;
        face.SurfTag = 0;
        face.Orientation = ParasolidConstants.PK_TOPOL_sense_none_c;
        face.PrevInBody = -1;
        face.NextInBody = -1;
    }

    private static void CreateSolidRegionsAndShells(BodySlot bodySlot, out ShellSlot voidShellSlot, out ShellSlot solidShellSlot)
    {
        RegionSlot voidRegionSlot = Regions.Allocate();
        RegionSlot solidRegionSlot = Regions.Allocate();
        ref var voidRegion = ref Regions[voidRegionSlot];
        ref var solidRegion = ref Regions[solidRegionSlot];
        voidRegion.IsSolid = 0;
        voidRegion.FirstShell = -1;
        voidRegion.LastShell = -1;
        voidRegion.ShellCount = 0;
        solidRegion.IsSolid = 1;
        solidRegion.FirstShell = -1;
        solidRegion.LastShell = -1;
        solidRegion.ShellCount = 0;

        AppendRegionToBody(bodySlot, voidRegionSlot);
        AppendRegionToBody(bodySlot, solidRegionSlot);

        voidShellSlot = Shells.Allocate();
        solidShellSlot = Shells.Allocate();
        InitializeShell(ref Shells[voidShellSlot], bodySlot);
        InitializeShell(ref Shells[solidShellSlot], bodySlot);

        AppendShellToBody(bodySlot, voidShellSlot);
        AppendShellToRegion(voidRegionSlot, voidShellSlot);
        AppendShellToBody(bodySlot, solidShellSlot);
        AppendShellToRegion(solidRegionSlot, solidShellSlot);
    }

    private static int AddFinToLoopAndEdge(LoopSlot loopSlot, FaceSlot faceSlot, EdgeSlot edgeSlot)
    {
        int finSlot = Fins.Allocate();
        ref var fin = ref Fins[finSlot];
        fin.Edge = edgeSlot;
        fin.Loop = loopSlot;
        fin.Face = faceSlot;
        fin.NextInLoop = fin.PrevInLoop = -1;
        fin.NextOfEdge = fin.PrevOfEdge = -1;
        fin.Vertex = -1;
        fin.NextAtVertex = fin.PrevAtVertex = -1;

        AppendFinToLoop(loopSlot, finSlot);
        AppendFinToEdge(edgeSlot, finSlot);
        AppendFinToVertex(EdgeFinVertex(finSlot, Edges[edgeSlot]), finSlot);
        return finSlot;
    }

    private static int CreateCircleCurveTag(double cx, double cy, double cz, double axX, double axY, double axZ, double refX, double refY, double refZ, double radius)
    {
        int dataSlot = CircleDataPool.Allocate();
        ref var data = ref CircleDataPool[dataSlot];
        data.CenterX = cx; data.CenterY = cy; data.CenterZ = cz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = radius;

        int curveSlot = Curves.Allocate();
        ref var curve = ref Curves[curveSlot];
        AssignPartition(ref curve.Header, CurrentPartition);
        curve.Class = CurveClass.Circle;
        curve.DataIndex = dataSlot;
        curve.TMin = 0;
        curve.TMax = Math.Tau;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        return AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
    }

    private static int CreateLineCurveTag(double ox, double oy, double oz, double axX, double axY, double axZ)
    {
        var length = Math.Sqrt(axX * axX + axY * axY + axZ * axZ);
        if (length <= 0)
            return 0;

        int dataSlot = LineDataPool.Allocate();
        ref var data = ref LineDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX / length; data.AxisY = axY / length; data.AxisZ = axZ / length;

        int curveSlot = Curves.Allocate();
        ref var curve = ref Curves[curveSlot];
        AssignPartition(ref curve.Header, CurrentPartition);
        curve.Class = CurveClass.Line;
        curve.DataIndex = dataSlot;
        curve.TMin = 0;
        curve.TMax = length;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        return AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
    }

    private static int CreatePointTag(double x, double y, double z)
    {
        int pointSlot = Points.Allocate();
        ref var point = ref Points[pointSlot];
        AssignPartition(ref point.Header, CurrentPartition);
        point.Position.X = x;
        point.Position.Y = y;
        point.Position.Z = z;
        return AllocateTag(EntityClass.Point, PoolKind.Point, pointSlot, point.Header.Generation);
    }

    private static int CreateCylinderSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ, double radius)
    {
        int dataSlot = CylinderDataPool.Allocate();
        ref var data = ref CylinderDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.AxisX = axX; data.AxisY = axY; data.AxisZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;
        data.Radius = radius;

        int surfSlot = Surfaces.Allocate();
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Cylinder;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = Math.Tau;
        surf.VMin = 0;
        surf.VMax = 0;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static int CreatePlaneSurfaceTag(double ox, double oy, double oz, double axX, double axY, double axZ, double refX, double refY, double refZ)
    {
        int dataSlot = PlaneDataPool.Allocate();
        ref var data = ref PlaneDataPool[dataSlot];
        data.LocationX = ox; data.LocationY = oy; data.LocationZ = oz;
        data.NormalX = axX; data.NormalY = axY; data.NormalZ = axZ;
        data.RefDirX = refX; data.RefDirY = refY; data.RefDirZ = refZ;

        int surfSlot = Surfaces.Allocate();
        ref var surf = ref Surfaces[surfSlot];
        AssignPartition(ref surf.Header, CurrentPartition);
        surf.Class = SurfaceClass.Plane;
        surf.DataIndex = dataSlot;
        surf.UMin = 0;
        surf.UMax = 0;
        surf.VMin = 0;
        surf.VMax = 0;
        return AllocateTag(EntityClass.Surface, PoolKind.Surface, surfSlot, surf.Header.Generation);
    }

    private static void ReadAxis2(PK_AXIS2_sf_s* basisSet, out double ox, out double oy, out double oz, out double axX, out double axY, out double axZ, out double refX, out double refY, out double refZ)
    {
        ox = 0; oy = 0; oz = 0;
        axX = 0; axY = 0; axZ = 1;
        refX = 1; refY = 0; refZ = 0;
        if (basisSet is null)
            return;

        ox = basisSet->location.coord[0];
        oy = basisSet->location.coord[1];
        oz = basisSet->location.coord[2];
        axX = basisSet->axis.coord[0];
        axY = basisSet->axis.coord[1];
        axZ = basisSet->axis.coord[2];
        refX = basisSet->ref_direction.coord[0];
        refY = basisSet->ref_direction.coord[1];
        refZ = basisSet->ref_direction.coord[2];
    }

    private static void Cross(double ax, double ay, double az, double bx, double by, double bz, out double cx, out double cy, out double cz)
    {
        cx = ay * bz - az * by;
        cy = az * bx - ax * bz;
        cz = ax * by - ay * bx;
    }

    // ── Dispatch ─────────────────────────────────────────────────

    public static int Dispatch<TCommand>(ApiId apiId, ConcurrencyKind concurrencyKind, AccessKind accessKind, ref TCommand command)
        where TCommand : struct, IKernelCommand
    {
        var descriptor = new CommandDescriptor
        {
            ApiId = apiId,
            ConcurrencyKind = concurrencyKind,
            AccessKind = accessKind,
            SessionId = DefaultSessionId,
            PartitionId = CurrentPartition,
        };
        return DispatchState.Execute(ref descriptor, ref command);
    }

    public static int NotImplemented()
    {
        return ParasolidConstants.PK_ERROR_not_implemented;
    }
}
