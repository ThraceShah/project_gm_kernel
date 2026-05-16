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
    internal static EntityPool<CylinderData> CylinderDataPool;
    internal static EntityPool<PlaneData> PlaneDataPool;

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
        body.FirstShell = -1;
        body.FirstRegion = -1;
        body.FirstFaceBody = -1;
        body.FirstEdgeBody = -1;
        body.FirstVertexBody = -1;

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
                    Shells[slots[i]].Body = -1;
                    Shells[slots[i]].Region = -1;
                    Shells[slots[i]].FirstFaceUseShell = -1;
                    Shells[slots[i]].AcornVertex = -1;
                    Shells[slots[i]].NextInBody = -1;
                    Shells[slots[i]].NextInRegion = -1;
                    poolKinds[i] = (byte)PoolKind.Shell;
                    break;
                case ParasolidConstants.PK_CLASS_face:
                    slots[i] = Faces.Allocate();
                    Faces[slots[i]].BackShell = -1;
                    Faces[slots[i]].FrontShell = -1;
                    Faces[slots[i]].BackFaceUse = -1;
                    Faces[slots[i]].FrontFaceUse = -1;
                    Faces[slots[i]].FirstLoop = -1;
                    Faces[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Face;
                    break;
                case ParasolidConstants.PK_CLASS_loop:
                    slots[i] = Loops.Allocate();
                    Loops[slots[i]].Face = -1;
                    Loops[slots[i]].FirstFin = -1;
                    Loops[slots[i]].NextInFace = -1;
                    poolKinds[i] = (byte)PoolKind.Loop;
                    break;
                case ParasolidConstants.PK_CLASS_edge:
                    slots[i] = Edges.Allocate();
                    Edges[slots[i]].Body = -1;
                    Edges[slots[i]].FirstFinEdge = -1;
                    Edges[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Edge;
                    break;
                case ParasolidConstants.PK_CLASS_fin:
                    slots[i] = Fins.Allocate();
                    Fins[slots[i]].Edge = -1;
                    Fins[slots[i]].Loop = -1;
                    Fins[slots[i]].Face = -1;
                    Fins[slots[i]].NextInLoop = -1;
                    Fins[slots[i]].PrevInLoop = -1;
                    Fins[slots[i]].NextOfEdge = -1;
                    Fins[slots[i]].PrevOfEdge = -1;
                    poolKinds[i] = (byte)PoolKind.Fin;
                    break;
                case ParasolidConstants.PK_CLASS_vertex:
                    slots[i] = Vertices.Allocate();
                    Vertices[slots[i]].Body = -1;
                    Vertices[slots[i]].NextInBody = -1;
                    poolKinds[i] = (byte)PoolKind.Vertex;
                    break;
                case ParasolidConstants.PK_CLASS_region:
                    slots[i] = Regions.Allocate();
                    Regions[slots[i]].Body = -1;
                    Regions[slots[i]].IsSolid = 0;
                    Regions[slots[i]].FirstShell = -1;
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
                    ref var body = ref Bodies[parentSlot];
                    ref var shell = ref Shells[childSlot];
                    shell.NextInBody = -1;
                    shell.Body = parentSlot;
                    shell.Region = -1;
                    if (body.FirstShell < 0)
                    {
                        body.FirstShell = childSlot;
                    }
                    else
                    {
                        int cur = body.FirstShell;
                        while (Shells[cur].NextInBody >= 0) cur = Shells[cur].NextInBody;
                        Shells[cur].NextInBody = childSlot;
                    }
                    body.ShellCount++;
                }
                break;

            case PoolKind.Body when childPool == (byte)PoolKind.Region:
                {
                    ref var body = ref Bodies[parentSlot];
                    ref var region = ref Regions[childSlot];
                    region.NextInBody = -1;
                    region.Body = parentSlot;
                    if (body.FirstRegion < 0)
                    {
                        body.FirstRegion = childSlot;
                    }
                    else
                    {
                        int cur = body.FirstRegion;
                        while (Regions[cur].NextInBody >= 0) cur = Regions[cur].NextInBody;
                        Regions[cur].NextInBody = childSlot;
                    }
                    body.RegionCount++;
                }
                break;

            case PoolKind.Region when childPool == (byte)PoolKind.Shell:
                {
                    ref var region = ref Regions[parentSlot];
                    ref var shell = ref Shells[childSlot];
                    shell.Body = region.Body;
                    shell.Region = parentSlot;
                    shell.NextInRegion = -1;
                    if (region.FirstShell < 0)
                    {
                        region.FirstShell = childSlot;
                    }
                    else
                    {
                        int cur = region.FirstShell;
                        while (Shells[cur].NextInRegion >= 0) cur = Shells[cur].NextInRegion;
                        Shells[cur].NextInRegion = childSlot;
                    }
                    region.ShellCount++;
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
                    loop.NextInFace = -1;
                    loop.Face = parentSlot;
                    if (face.FirstLoop < 0)
                    {
                        face.FirstLoop = childSlot;
                    }
                    else
                    {
                        int cur = face.FirstLoop;
                        while (Loops[cur].NextInFace >= 0) cur = Loops[cur].NextInFace;
                        Loops[cur].NextInFace = childSlot;
                    }
                    face.LoopCount++;
                }
                break;

            case PoolKind.Loop when childPool == (byte)PoolKind.Fin:
                {
                    ref var loop = ref Loops[parentSlot];
                    ref var fin = ref Fins[childSlot];
                    fin.NextInLoop = -1;
                    fin.Loop = parentSlot;
                    fin.Face = loop.Face;  // derive face from parent loop
                    if (loop.FirstFin < 0)
                    {
                        loop.FirstFin = childSlot;
                    }
                    else
                    {
                        int cur = loop.FirstFin;
                        while (Fins[cur].NextInLoop >= 0) cur = Fins[cur].NextInLoop;
                        Fins[cur].NextInLoop = childSlot;
                    }
                    loop.FinCount++;
                }
                break;

            case PoolKind.Edge when childPool == (byte)PoolKind.Fin:
                {
                    ref var edge = ref Edges[parentSlot];
                    ref var fin = ref Fins[childSlot];
                    fin.NextOfEdge = -1;
                    fin.Edge = parentSlot;
                    if (edge.FirstFinEdge < 0)
                    {
                        edge.FirstFinEdge = childSlot;
                    }
                    else
                    {
                        int cur = edge.FirstFinEdge;
                        while (Fins[cur].NextOfEdge >= 0) cur = Fins[cur].NextOfEdge;
                        Fins[cur].NextOfEdge = childSlot;
                    }
                    edge.FinCount++;
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
        faceUse.NextInShell = -1;

        ref var shell = ref Shells[shellSlot];
        if (shell.FirstFaceUseShell < 0)
        {
            shell.FirstFaceUseShell = faceUseSlot;
        }
        else
        {
            int cur = shell.FirstFaceUseShell;
            while (FaceUses[cur].NextInShell >= 0) cur = FaceUses[cur].NextInShell;
            FaceUses[cur].NextInShell = faceUseSlot;
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
        region.NextInBody = -1;

        if (body.FirstRegion < 0)
        {
            body.FirstRegion = regionSlot;
        }
        else
        {
            int cur = body.FirstRegion;
            while (Regions[cur].NextInBody >= 0) cur = Regions[cur].NextInBody;
            Regions[cur].NextInBody = regionSlot;
        }
        body.RegionCount++;
    }

    private static void AppendShellToBody(BodySlot bodySlot, ShellSlot shellSlot)
    {
        ref var body = ref Bodies[bodySlot];
        ref var shell = ref Shells[shellSlot];
        shell.Body = bodySlot;
        shell.NextInBody = -1;

        if (body.FirstShell < 0)
        {
            body.FirstShell = shellSlot;
        }
        else
        {
            int cur = body.FirstShell;
            while (Shells[cur].NextInBody >= 0) cur = Shells[cur].NextInBody;
            Shells[cur].NextInBody = shellSlot;
        }
        body.ShellCount++;
    }

    private static void AppendShellToRegion(RegionSlot regionSlot, ShellSlot shellSlot)
    {
        ref var region = ref Regions[regionSlot];
        ref var shell = ref Shells[shellSlot];
        shell.Region = regionSlot;
        shell.NextInRegion = -1;

        if (region.FirstShell < 0)
        {
            region.FirstShell = shellSlot;
        }
        else
        {
            int cur = region.FirstShell;
            while (Shells[cur].NextInRegion >= 0) cur = Shells[cur].NextInRegion;
            Shells[cur].NextInRegion = shellSlot;
        }
        region.ShellCount++;
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

        int lastFace = -1, lastEdge = -1, lastVertex = -1;

        for (int i = 0; i < nTopols; i++)
        {
            var slot = slots[i];
            switch ((PoolKind)poolKinds[i])
            {
                case PoolKind.Face:
                    if (body.FirstFaceBody < 0) body.FirstFaceBody = slot;
                    if (lastFace >= 0) Faces[lastFace].NextInBody = slot;
                    lastFace = slot;
                    body.FaceCountBody++;
                    break;

                case PoolKind.Edge:
                    if (body.FirstEdgeBody < 0) body.FirstEdgeBody = slot;
                    if (lastEdge >= 0) Edges[lastEdge].NextInBody = slot;
                    lastEdge = slot;
                    body.EdgeCountBody++;
                    Edges[slot].Body = bodySlot;
                    break;

                case PoolKind.Vertex:
                    if (body.FirstVertexBody < 0) body.FirstVertexBody = slot;
                    if (lastVertex >= 0) Vertices[lastVertex].NextInBody = slot;
                    lastVertex = slot;
                    body.VertexCountBody++;
                    Vertices[slot].Body = bodySlot;
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

        for (int regionSlot = Bodies[bodySlot].FirstRegion; regionSlot >= 0; regionSlot = Regions[regionSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Region, PoolKind.Region, regionSlot))
                return -1;
        }

        for (int shellSlot = Bodies[bodySlot].FirstShell; shellSlot >= 0; shellSlot = Shells[shellSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Shell, PoolKind.Shell, shellSlot))
                return -1;

        }

        for (int faceSlot = Bodies[bodySlot].FirstFaceBody; faceSlot >= 0; faceSlot = Faces[faceSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Face, PoolKind.Face, faceSlot))
                return -1;

            for (int loopSlot = Faces[faceSlot].FirstLoop; loopSlot >= 0; loopSlot = Loops[loopSlot].NextInFace)
            {
                if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Loop, PoolKind.Loop, loopSlot))
                    return -1;

                for (int finSlot = Loops[loopSlot].FirstFin; finSlot >= 0; finSlot = Fins[finSlot].NextInLoop)
                {
                    if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Fin, PoolKind.Fin, finSlot))
                        return -1;
                }
            }
        }

        for (int edgeSlot = Bodies[bodySlot].FirstEdgeBody; edgeSlot >= 0; edgeSlot = Edges[edgeSlot].NextInBody)
        {
            if (!AppendTopology(ref index, maxTopols, topols, classes, EntityClass.Edge, PoolKind.Edge, edgeSlot))
                return -1;
        }

        for (int vertexSlot = Bodies[bodySlot].FirstVertexBody; vertexSlot >= 0; vertexSlot = Vertices[vertexSlot].NextInBody)
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

        for (int regionSlot = Bodies[bodySlot].FirstRegion; regionSlot >= 0; regionSlot = Regions[regionSlot].NextInBody)
        {
            int regionTag = GetOrAllocateTag(EntityClass.Region, PoolKind.Region, regionSlot);
            if (!AppendRelation(ref relation, maxRelations, parents, children, senses, bodyTag, regionTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                return -1;

            for (int shellSlot = Regions[regionSlot].FirstShell; shellSlot >= 0; shellSlot = Shells[shellSlot].NextInRegion)
            {
                int shellTag = GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, shellSlot);
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, regionTag, shellTag, ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;
            }
        }

        for (int shellSlot = Bodies[bodySlot].FirstShell; shellSlot >= 0; shellSlot = Shells[shellSlot].NextInBody)
        {
            if (Shells[shellSlot].Region < 0)
            {
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, bodyTag, GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, shellSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;
            }

            for (int faceUseSlot = Shells[shellSlot].FirstFaceUseShell; faceUseSlot >= 0; faceUseSlot = FaceUses[faceUseSlot].NextInShell)
            {
                ref var faceUse = ref FaceUses[faceUseSlot];
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Shell, PoolKind.Shell, shellSlot), GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceUse.Face), faceUse.Sense, topols, topolCount))
                    return -1;
            }
        }

        for (int faceSlot = Bodies[bodySlot].FirstFaceBody; faceSlot >= 0; faceSlot = Faces[faceSlot].NextInBody)
        {
            for (int loopSlot = Faces[faceSlot].FirstLoop; loopSlot >= 0; loopSlot = Loops[loopSlot].NextInFace)
            {
                if (!AppendRelation(ref relation, maxRelations, parents, children, senses, GetOrAllocateTag(EntityClass.Face, PoolKind.Face, faceSlot), GetOrAllocateTag(EntityClass.Loop, PoolKind.Loop, loopSlot), ParasolidConstants.PK_TOPOL_sense_none_c, topols, topolCount))
                    return -1;

                for (int finSlot = Loops[loopSlot].FirstFin; finSlot >= 0; finSlot = Fins[finSlot].NextInLoop)
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
        for (int shellSlot = body.FirstShell; shellSlot >= 0; shellSlot = Shells[shellSlot].NextInBody)
        {
            faceUseCount += Shells[shellSlot].FaceUseCount;
        }
        for (int faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = Faces[faceSlot].NextInBody)
        {
            for (int loopSlot = Faces[faceSlot].FirstLoop; loopSlot >= 0; loopSlot = Loops[loopSlot].NextInFace)
            {
                topolCount += 1 + Loops[loopSlot].FinCount;
                finCount += Loops[loopSlot].FinCount;
            }
        }

        int edgeFinRelationCount = 0;
        for (int edgeSlot = body.FirstEdgeBody; edgeSlot >= 0; edgeSlot = Edges[edgeSlot].NextInBody)
        {
            edgeFinRelationCount += Edges[edgeSlot].FinCount;
        }

        int relationCount = body.RegionCount + body.ShellCount + faceUseCount + finCount + edgeFinRelationCount;
        for (int faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = Faces[faceSlot].NextInBody)
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

        var bodySlot = Bodies.Allocate();
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);

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
        for (int i = 0; i < 8; i++)
        {
            vtxSlots[i] = Vertices.Allocate();
        }
        for (int i = 0; i < 8; i++)
        {
            ref var vtx = ref Vertices[vtxSlots[i]];
            vtx.PointTag = 0; // no standalone point entity yet
            vtx.Body = bodySlot;
            vtx.NextInBody = i < 7 ? vtxSlots[i + 1] : -1;
        }
        body.FirstVertexBody = vtxSlots[0];
        body.VertexCountBody = 8;

        // Allocate 12 edges
        Span<int> edgeSlots = stackalloc int[12];
        for (int i = 0; i < 12; i++)
        {
            edgeSlots[i] = Edges.Allocate();
        }
        for (int i = 0; i < 12; i++)
        {
            ref var edge = ref Edges[edgeSlots[i]];
            edge.Body = bodySlot;
            edge.CurveTag = 0; // no standalone curve entity yet
            edge.FirstFinEdge = -1;
            edge.NextInBody = i < 11 ? edgeSlots[i + 1] : -1;
        }
        body.FirstEdgeBody = edgeSlots[0];
        body.EdgeCountBody = 12;

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

        int lastBodyFace = -1;

        for (int f = 0; f < 6; f++)
        {
            faceSlots[f] = Faces.Allocate();
            loopSlots[f] = Loops.Allocate();

            ref var face = ref Faces[faceSlots[f]];
            ref var loop = ref Loops[loopSlots[f]];

            InitializeFace(ref face);
            face.SurfTag = 0; // no standalone surface entity yet
            face.FirstLoop = loopSlots[f];
            face.LoopCount = 1;
            face.NextInBody = -1;

            if (lastBodyFace >= 0)
                Faces[lastBodyFace].NextInBody = faceSlots[f];
            lastBodyFace = faceSlots[f];

            loop.Face = faceSlots[f];
            loop.FirstFin = -1;
            loop.NextInFace = -1;

            // Allocate 4 fins per loop
            int firstFinSlot = -1;
            int prevFinSlot = -1;

            for (int e = 0; e < 4; e++)
            {
                int ei = faceEdgeIndices[f * 4 + e];
                int finSlot = Fins.Allocate();
                ref var fin = ref Fins[finSlot];

                fin.Edge = edgeSlots[ei];
                fin.Loop = loopSlots[f];
                fin.Face = faceSlots[f];
                fin.NextInLoop = -1;
                fin.PrevInLoop = prevFinSlot;
                fin.NextOfEdge = -1;
                fin.PrevOfEdge = -1;

                // Wire into edge's fin chain
                ref var edge = ref Edges[edgeSlots[ei]];
                if (edge.FirstFinEdge < 0)
                    edge.FirstFinEdge = finSlot;
                else
                {
                    int cur = edge.FirstFinEdge;
                    while (Fins[cur].NextOfEdge >= 0) cur = Fins[cur].NextOfEdge;
                    Fins[cur].NextOfEdge = finSlot;
                    fin.PrevOfEdge = cur;
                }
                edge.FinCount++;

                if (prevFinSlot >= 0)
                    Fins[prevFinSlot].NextInLoop = finSlot;
                if (firstFinSlot < 0)
                    firstFinSlot = finSlot;

                prevFinSlot = finSlot;
            }

            loop.FirstFin = firstFinSlot;
            loop.FinCount = 4;

            AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c);
            AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c);
        }

        body.FirstFaceBody = faceSlots[0];
        body.FaceCountBody = 6;

        // Build result tag
        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *bodyTag = tag;
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

        var bodySlot = Bodies.Allocate();
        ref var body = ref Bodies[bodySlot];
        InitializeBody(ref body);

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
        edgeCurves[1] = CreateCircleCurveTag(ox + height * axX, oy + height * axY, oz + height * axZ, axX, axY, axZ, refX, refY, refZ, radius);
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
            edge.CurveTag = edgeCurves[i];
            edge.FirstFinEdge = -1;
            edge.NextInBody = i == 0 ? edgeSlots[1] : -1;
        }
        body.FirstEdgeBody = edgeSlots[0];
        body.EdgeCountBody = 2;
        body.FirstVertexBody = -1;
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
            face.NextInBody = f < 2 ? faceSlots[f + 1] : -1;

            int previousLoop = -1;
            for (int l = 0; l < loopCounts[f]; l++)
            {
                int loopSlot = Loops.Allocate();
                ref var loop = ref Loops[loopSlot];
                loop.Face = faceSlots[f];
                loop.FirstFin = -1;
                loop.NextInFace = -1;

                int edgeIndex = f == 0 ? l : f - 1;
                int finSlot = AddFinToLoopAndEdge(loopSlot, faceSlots[f], edgeSlots[edgeIndex]);
                loop.FirstFin = finSlot;
                loop.FinCount = 1;

                if (previousLoop >= 0)
                    Loops[previousLoop].NextInFace = loopSlot;
                else
                    face.FirstLoop = loopSlot;
                previousLoop = loopSlot;
                face.LoopCount++;
            }

            AddFaceUse(solidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_negative_c);
            AddFaceUse(voidShellSlot, faceSlots[f], ParasolidConstants.PK_TOPOL_sense_positive_c);
        }

        body.FirstFaceBody = faceSlots[0];
        body.FaceCountBody = 3;

        var tag = AllocateTag(EntityClass.Body, PoolKind.Body, bodySlot, body.Header.Generation);
        if (tag < 0)
            return ParasolidConstants.PK_ERROR_general_body;

        *bodyTag = tag;
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
        body.ShellCount = 0;
        body.FirstRegion = -1;
        body.RegionCount = 0;
        body.FirstFaceBody = -1;
        body.FaceCountBody = 0;
        body.FirstEdgeBody = -1;
        body.EdgeCountBody = 0;
        body.FirstVertexBody = -1;
        body.VertexCountBody = 0;
    }

    private static void InitializeShell(ref ShellRecord shell, BodySlot bodySlot)
    {
        shell.Body = bodySlot;
        shell.Region = -1;
        shell.ShellType = 0;
        shell.FirstFaceUseShell = -1;
        shell.FaceUseCount = 0;
        shell.AcornVertex = -1;
        shell.NextInBody = -1;
        shell.NextInRegion = -1;
    }

    private static void InitializeFace(ref FaceRecord face)
    {
        face.BackShell = -1;
        face.FrontShell = -1;
        face.BackFaceUse = -1;
        face.FrontFaceUse = -1;
        face.FirstLoop = -1;
        face.LoopCount = 0;
        face.SurfTag = 0;
        face.Orientation = ParasolidConstants.PK_TOPOL_sense_none_c;
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
        voidRegion.ShellCount = 0;
        solidRegion.IsSolid = 1;
        solidRegion.FirstShell = -1;
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
        fin.NextInLoop = -1;
        fin.PrevInLoop = -1;
        fin.NextOfEdge = -1;
        fin.PrevOfEdge = -1;

        ref var edge = ref Edges[edgeSlot];
        if (edge.FirstFinEdge < 0)
        {
            edge.FirstFinEdge = finSlot;
        }
        else
        {
            int cur = edge.FirstFinEdge;
            while (Fins[cur].NextOfEdge >= 0) cur = Fins[cur].NextOfEdge;
            Fins[cur].NextOfEdge = finSlot;
            fin.PrevOfEdge = cur;
        }
        edge.FinCount++;
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
        curve.Class = CurveClass.Circle;
        curve.DataIndex = dataSlot;
        curve.TMin = 0;
        curve.TMax = Math.Tau;
        curve.Sense = ParasolidConstants.PK_TOPOL_sense_positive_c;
        return AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
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
        };
        return DispatchState.Execute(ref descriptor, ref command);
    }

    public static int NotImplemented()
    {
        return ParasolidConstants.PK_ERROR_not_implemented;
    }
}
