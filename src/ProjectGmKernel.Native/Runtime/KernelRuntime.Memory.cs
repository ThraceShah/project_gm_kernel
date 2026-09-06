using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

// Session lifecycle, tag plumbing, ownership cascade delete and undo log
// integration. All state lives in the unmanaged SessionData; KernelRuntime
// keeps only the session pointer and its gate.

internal static unsafe partial class KernelRuntime
{
    // The session allocator is pinned on the unmanaged heap for the process
    // lifetime; every session object hangs off this stable pointer.
    private static readonly SessionMemory* memory = (SessionMemory*)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)sizeof(SessionMemory));

    internal static SessionMemory* MemoryPointer() => memory;

    internal struct KernelState
    {
        public SessionData* Session;

        public bool IsStarted => Session != null && Session->IsStarted;
    }

    internal static KernelState State;
    private static readonly SessionDispatchState dispatchState = new();
    internal static SessionDispatchState Dispatcher => dispatchState;

    internal static ulong BlocksLiveBytes => State.Session != null ? State.Session->Blocks.LiveBytes : 0;

    internal static int NextTagValue => State.Session != null ? State.Session->Tags.NextTag : 0;

    internal static MemoryStatistics AllocatorStatistics => MemoryPointer()->Statistics;

    internal static ulong ReturnOutstandingBytes => State.Session != null ? State.Session->Returns.OutstandingBytes : 0;

    internal static ulong ReturnOutstandingBlocks => State.Session != null ? State.Session->Returns.OutstandingBlocks : 0;

    /// <summary>Test hook: simulate the Nth successful allocation failing.</summary>
    internal static void InjectAllocationFailure(AllocationSequence successfulRequests)
        => MemoryPointer()->FailAfter(successfulRequests);

    /// <summary>Test hook: drop all cached pages (scheduler quiescent point).</summary>
    internal static void TrimAllocator()
    {
        var command = new TrimMemoryCommand();
        Dispatch(ApiId.GeneratedStub, ConcurrencyKind.Exclusive, AccessKind.SessionControl, ref command);
    }

    private struct TrimMemoryCommand : IKernelCommand
    {
        public int Execute()
        {
            if (State.Session != null && State.Session->IsMarkActive == 0)
            {
                TrimEmptyPool(ref Points); TrimEmptyPool(ref Vectors); TrimEmptyPool(ref Bodies);
                TrimEmptyPool(ref Shells); TrimEmptyPool(ref FaceUses); TrimEmptyPool(ref Faces);
                TrimEmptyPool(ref Loops); TrimEmptyPool(ref Edges); TrimEmptyPool(ref Fins);
                TrimEmptyPool(ref Vertices); TrimEmptyPool(ref Regions); TrimEmptyPool(ref Curves);
                TrimEmptyPool(ref Surfaces); TrimEmptyPool(ref Transforms);
                TrimEmptyPool(ref CircleDataPool); TrimEmptyPool(ref LineDataPool);
                TrimEmptyPool(ref CylinderDataPool); TrimEmptyPool(ref PlaneDataPool);
                TrimEmptyPool(ref ConeDataPool); TrimEmptyPool(ref SphereDataPool); TrimEmptyPool(ref TorusDataPool);
                TrimEmptyPool(ref BCurveDataStore);
                if (State.Session->Tags.LiveOrKeptCount == 0) State.Session->Tags.Dispose();
            }
            MemoryPointer()->Trim();
            return 0;
        }
    }

    private static void TrimEmptyPool<T>(ref PagedEntityPool<T> pool) where T : unmanaged
    {
        if (pool.AliveCount == 0) pool.Dispose();
    }

    internal static int UndoEntryCountValue => State.Session != null ? State.Session->UndoEntryCount : 0;

    /// <summary>
    /// Bind the calling thread's scratch arena when entering the kernel
    /// without a dispatch (internal evaluation and creation helpers). Returns
    /// true when the caller must release it.
    /// </summary>
    internal static bool EnsureCommandScratch()
    {
        if (CommandScratch.IsBound) return false;
        var session = State.Session;
        if (session == null) return false;
        var threadId = Environment.CurrentManagedThreadId;
        var slot = session->FindThread(threadId);
        if (slot < 0)
            slot = session->TryRegisterThread(threadId, session->SessionGeneration);
        if (slot < 0) return false;
        CommandScratch.Bind(session, slot);
        return true;
    }

    internal static void ReleaseCommandScratch(SessionData* session)
        => CommandScratch.Unbind(session, session->FindThread(Environment.CurrentManagedThreadId));

    // ── Entity pools (paged, unmanaged) ─────────────────────────

    internal static PagedEntityPool<PointRecord> Points = default;
    internal static PagedEntityPool<VectorRecord> Vectors = default;
    internal static PagedEntityPool<BodyRecord> Bodies = default;
    internal static PagedEntityPool<ShellRecord> Shells = default;
    internal static PagedEntityPool<FaceUseRecord> FaceUses = default;
    internal static PagedEntityPool<FaceRecord> Faces = default;
    internal static PagedEntityPool<LoopRecord> Loops = default;
    internal static PagedEntityPool<EdgeRecord> Edges = default;
    internal static PagedEntityPool<FinRecord> Fins = default;
    internal static PagedEntityPool<VertexRecord> Vertices = default;
    internal static PagedEntityPool<RegionRecord> Regions = default;
    internal static PagedEntityPool<CurveRecord> Curves = default;
    internal static PagedEntityPool<SurfaceRecord> Surfaces = default;
    internal static PagedEntityPool<TransformRecord> Transforms = default;
    internal static PagedEntityPool<CircleData> CircleDataPool = default;
    internal static PagedEntityPool<LineData> LineDataPool = default;
    internal static PagedEntityPool<CylinderData> CylinderDataPool = default;
    internal static PagedEntityPool<PlaneData> PlaneDataPool = default;
    internal static PagedEntityPool<ConeData> ConeDataPool = default;
    internal static PagedEntityPool<SphereData> SphereDataPool = default;
    internal static PagedEntityPool<TorusData> TorusDataPool = default;

    // ── Per-session attach/detach of pool storage ───────────────

    private static void AttachPools(SessionMemory* memory)
    {
        BCurveDataStore.Attach(memory, PoolKind.BCurveData);
        Points.Attach(memory, PoolKind.Point);
        Vectors.Attach(memory, PoolKind.Vector);
        Bodies.Attach(memory, PoolKind.Body);
        Shells.Attach(memory, PoolKind.Shell);
        FaceUses.Attach(memory, PoolKind.FaceUse);
        Faces.Attach(memory, PoolKind.Face);
        Loops.Attach(memory, PoolKind.Loop);
        Edges.Attach(memory, PoolKind.Edge);
        Fins.Attach(memory, PoolKind.Fin);
        Vertices.Attach(memory, PoolKind.Vertex);
        Regions.Attach(memory, PoolKind.Region);
        Curves.Attach(memory, PoolKind.Curve);
        Surfaces.Attach(memory, PoolKind.Surface);
        Transforms.Attach(memory, PoolKind.Transform);
        CircleDataPool.Attach(memory, PoolKind.CircleData);
        LineDataPool.Attach(memory, PoolKind.LineData);
        CylinderDataPool.Attach(memory, PoolKind.CylinderData);
        PlaneDataPool.Attach(memory, PoolKind.PlaneData);
        ConeDataPool.Attach(memory, PoolKind.ConeData);
        SphereDataPool.Attach(memory, PoolKind.SphereData);
        TorusDataPool.Attach(memory, PoolKind.TorusData);
    }

    private static void DisposePools()
    {
        Points.Dispose();
        Vectors.Dispose();
        Bodies.Dispose();
        Shells.Dispose();
        FaceUses.Dispose();
        Faces.Dispose();
        Loops.Dispose();
        Edges.Dispose();
        Fins.Dispose();
        Vertices.Dispose();
        Regions.Dispose();
        Curves.Dispose();
        Surfaces.Dispose();
        Transforms.Dispose();
        CircleDataPool.Dispose();
        LineDataPool.Dispose();
        CylinderDataPool.Dispose();
        PlaneDataPool.Dispose();
        ConeDataPool.Dispose();
        SphereDataPool.Dispose();
        TorusDataPool.Dispose();
    }

    // ── Session lifecycle ───────────────────────────────────────

    internal static int SessionStartCore(PK_SESSION_start_o_s* options)
    {
        if (options is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (options->o_t_version != 1)
            return ParasolidConstants.PK_ERROR_o_t_version_incorrect;

        if (State.Session != null && State.Session->IsStarted)
            return ParasolidConstants.PK_ERROR_rollback_started;
        if (!XtSchemaRegistry.ConfigureFromEnvironment())
            return ParasolidConstants.PK_ERROR_schema_access_error;

        var memory = MemoryPointer();
        // Reset per-session bookkeeping of the persistent allocator: cached
        // pages survive only within a session's lifetime per the design, and a
        // stop must have released everything already.
        var session = SessionData.Create(memory);
        if (session == null)
            return ParasolidConstants.PK_ERROR_memory_full;
        if (!session->Initialize())
        {
            session->Dispose();
            memory->Free(session);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        AttachPools(memory);
        session->SessionGeneration = System.Threading.Interlocked.Increment(ref nextSessionGeneration);
        ResetBCurves();
        State.Session = session;
        SessionMemoryOwner.ResetXtAssociations();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    internal static int SessionStopCore()
    {
        var session = State.Session;
        if (session == null || !session->IsStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        // Drain: the dispatch layer guarantees no command is running. Release
        // caller-held result memory; callers must not touch it afterwards.
        session->Returns.ReleaseAll();
        session->Dispose();
        MemoryPointer()->Free(session);
        DisposePools();
        ResetBCurves();
        State.Session = null;
        SessionMemoryOwner.ResetXtAssociations();
        // Sweep the allocator: every block the session owned is returned and
        // the counters restart from zero for the next session.
        MemoryPointer()->Dispose();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private const int CurrentSessionId = 1;
    private static int nextSessionGeneration;

    // ── Tag plumbing ────────────────────────────────────────────

    internal static bool IsValidTag(EntityTag tag)
        => State.Session != null
           && State.Session->Tags.TryResolve(tag, CurrentSessionId, out _);

    internal static TagMap.TagRecord ResolveTagRecord(EntityTag tag)
    {
        State.Session->Tags.TryResolve(tag, CurrentSessionId, out var record);
        return record;
    }

    internal static EntityTag AllocateTag(EntityClass entityClass, PoolKind pool, int slot, EntityGeneration generation)
    {
        var session = State.Session;
        var tag = session->Tags.Publish((int)entityClass, (byte)pool, slot, generation, CurrentSessionId);
        if (tag > 0)
        {
            SetHeaderTag(pool, slot, tag);
        }
        return tag > 0 ? tag : -1;
    }

    internal static bool RecordSlotAllocation(PoolKind pool, DataSlot slot, EntityGeneration generation, void* record)
        => cachedThreadContext->SkipCreationUndo != 0 || State.Session->TryAppendUndo(SessionData.UndoKind.EntityCreated, (byte)pool,
            (short)CurrentPartition, slot, generation, 0, record, 0);

    internal static PartitionSlot AllocationPartition => cachedThreadContext->CurrentPartition;

    private static short PoolPartitionOf(PoolKind pool, int slot)
    {
        return pool switch
        {
            PoolKind.Point => Points[slot].Header.Partition,
            PoolKind.Vector => Vectors[slot].Header.Partition,
            PoolKind.Body => Bodies[slot].Header.Partition,
            PoolKind.Shell => Shells[slot].Header.Partition,
            PoolKind.FaceUse => FaceUses[slot].Header.Partition,
            PoolKind.Face => Faces[slot].Header.Partition,
            PoolKind.Loop => Loops[slot].Header.Partition,
            PoolKind.Edge => Edges[slot].Header.Partition,
            PoolKind.Fin => Fins[slot].Header.Partition,
            PoolKind.Vertex => Vertices[slot].Header.Partition,
            PoolKind.Region => Regions[slot].Header.Partition,
            PoolKind.Curve => Curves[slot].Header.Partition,
            PoolKind.Surface => Surfaces[slot].Header.Partition,
            PoolKind.Transform => Transforms[slot].Header.Partition,
            _ => 0,
        };
    }

    private static void SetHeaderTag(PoolKind pool, int slot, int tag)
    {
        switch (pool)
        {
            case PoolKind.Point: Points[slot].Header.Tag = tag; break;
            case PoolKind.Vector: Vectors[slot].Header.Tag = tag; break;
            case PoolKind.Body: Bodies[slot].Header.Tag = tag; break;
            case PoolKind.Shell: Shells[slot].Header.Tag = tag; break;
            case PoolKind.FaceUse: FaceUses[slot].Header.Tag = tag; break;
            case PoolKind.Face: Faces[slot].Header.Tag = tag; break;
            case PoolKind.Loop: Loops[slot].Header.Tag = tag; break;
            case PoolKind.Edge: Edges[slot].Header.Tag = tag; break;
            case PoolKind.Fin: Fins[slot].Header.Tag = tag; break;
            case PoolKind.Vertex: Vertices[slot].Header.Tag = tag; break;
            case PoolKind.Region: Regions[slot].Header.Tag = tag; break;
            case PoolKind.Curve: Curves[slot].Header.Tag = tag; break;
            case PoolKind.Surface: Surfaces[slot].Header.Tag = tag; break;
            case PoolKind.Transform: Transforms[slot].Header.Tag = tag; break;
        }
    }

    /// <summary>Tag of an entity slot that must already carry one in its header.</summary>
    internal static EntityTag TagOf(PoolKind pool, int slot)
        => SessionMemoryOwner.SlotTag(pool, slot);

    // ── Pool allocate helpers: slot index or -1 on allocation failure ──

    internal static int TryAllocatePoints() => Points.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateVectors() => Vectors.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateBodies()
    {
        if (!Bodies.TryAllocate(out var slot)) return -1;
        InitializeBody(ref Bodies[slot]);
        return slot;
    }
    internal static int TryAllocateShells() => Shells.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateFaceUses() => FaceUses.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateFaces() => Faces.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateLoops() => Loops.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateEdges() => Edges.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateFins() => Fins.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateVertices() => Vertices.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateRegions() => Regions.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateCurves() => Curves.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateSurfaces() => Surfaces.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateTransforms() => Transforms.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateCircleData() => CircleDataPool.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateLineData() => LineDataPool.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateCylinderData() => CylinderDataPool.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocatePlaneData() => PlaneDataPool.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateConeData() => ConeDataPool.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateSphereData() => SphereDataPool.TryAllocate(out var s) ? s : -1;
    internal static int TryAllocateTorusData() => TorusDataPool.TryAllocate(out var s) ? s : -1;

    internal static int* AllocateReturnSlice(int count)
    {
        if (count <= 0)
            return null;
        var session = State.Session;
        return (int*)session->Returns.TryAllocate((nuint)(count * sizeof(int)));
    }

    // ── Slot/tag registry kept in record headers ────────────────

    internal static bool EnsureSession()
        => State.Session != null && State.Session->IsStarted;
}

/// <summary>Bridge for the XT association side table (managed, adapter-owned).</summary>
internal static unsafe class SessionMemoryOwner
{
    internal static void ResetXtAssociations() => KernelRuntime.Xt.Reset();
    internal static void PrepareBodyXt(int slot) { }
    internal static void DropBodyXt(int slot) => KernelRuntime.Xt.Drop(slot);

    internal static int SlotTag(ProjectGmKernel.Native.Runtime.PoolKind pool, int slot)
    {
        return pool switch
        {
            ProjectGmKernel.Native.Runtime.PoolKind.Point => KernelRuntime.Points[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Vector => KernelRuntime.Vectors[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Body => KernelRuntime.Bodies[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Shell => KernelRuntime.Shells[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Face => KernelRuntime.Faces[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Loop => KernelRuntime.Loops[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Edge => KernelRuntime.Edges[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Fin => KernelRuntime.Fins[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Vertex => KernelRuntime.Vertices[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Region => KernelRuntime.Regions[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Curve => KernelRuntime.Curves[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Surface => KernelRuntime.Surfaces[slot].Header.Tag,
            ProjectGmKernel.Native.Runtime.PoolKind.Transform => KernelRuntime.Transforms[slot].Header.Tag,
            _ => 0,
        };
    }
}
