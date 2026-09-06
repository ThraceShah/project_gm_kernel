using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Unmanaged per-session state: partitions, the undo log, deferred-release
/// queues, per-thread contexts and scheduler bookkeeping. All storage comes
/// from <see cref="SessionMemory"/>. Owned by the session; freed on stop.
/// </summary>
internal unsafe struct SessionData
{
    internal const int MaxPartitions = 256;
    internal const int MaxThreads = 128;
    internal const int UndoSegmentBytes = 32 * 1024;

    // ── Partition table ─────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct PartitionRecord
    {
        internal PartitionSlot PartitionId;       // == slot index; ids are never reused
        internal PartitionLockState LockState;
        internal int SharedReaders;               // scheduler readers on this partition
        internal int WriterCount;                 // scheduler writers (0 or 1)
        internal KernelThreadId LockOwnerThread;  // PK lock owner, 0 = none
        internal BodySlot FirstBody;
        internal BodySlot LastBody;
        internal int BodyCount;
        internal int UndoTop;                     // index into undo segments (per partition)
        internal int Alive;
    }

    internal enum PartitionLockState : int
    {
        Unlocked = 0,
        SharedRead = 1,
        ExclusiveWrite = 2,
    }

    internal PartitionRecord* Partitions;

    // ── Undo log (single global mark; per-partition ordered entries) ──

    internal enum UndoKind : byte
    {
        EntityCreated = 1,                        // entity added after mark: destroy on goto
        EntityDeleted = 2,                        // entity deleted after mark: restore on goto
        FieldSnapshot = 3,                        // full-record image before modification
        BlockReplaced = 4,                        // variable-length block swap
        PartitionCreated = 5,
        PartitionDeleted = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UndoEntry
    {
        internal UndoKind Kind;
        internal byte Pool;                       // PoolKind for entity entries
        internal short Partition;
        internal DataSlot Slot;
        internal EntityGeneration Generation;
        internal EntityTag Tag;
        internal void* Data;                      // snapshot copy / old block handle
        internal nuint DataBytes;
        internal int Sequence;                    // global ordering for reverse replay
        internal KernelThreadId ThreadId;         // owning command's thread (failure undo)
        internal int Next;                        // chain per partition, -1 ends
    }

    internal enum UndoBookkeeping : byte
    {
        Cancelled = 0,                            // must not alias EntityCreated (= 1)
    }

    internal UndoEntry* UndoEntries { get => KernelRuntime.ThreadContext()->UndoEntries; set => KernelRuntime.ThreadContext()->UndoEntries = value; }
    internal int UndoEntryCount { get => KernelRuntime.ThreadContext()->UndoEntryCount; set => KernelRuntime.ThreadContext()->UndoEntryCount = value; }
    internal int UndoEntryCapacity { get => KernelRuntime.ThreadContext()->UndoEntryCapacity; set => KernelRuntime.ThreadContext()->UndoEntryCapacity = value; }
    internal int UndoSequence;
    internal int* PartitionUndoHeads;             // per-partition newest entry, -1 ends

    // ── Deferred release (delete under active mark) ─────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeferredRelease
    {
        internal byte Pool;
        internal DataSlot Slot;
        internal EntityTag Tag;
    }

    internal DeferredRelease* Deferred { get => KernelRuntime.ThreadContext()->Deferred; set => KernelRuntime.ThreadContext()->Deferred = value; }
    internal int DeferredCount { get => KernelRuntime.ThreadContext()->DeferredCount; set => KernelRuntime.ThreadContext()->DeferredCount = value; }

    // ── Scratch arenas for command workspaces ───────────────────

    internal const int ScratchSegmentBytes = 256 * 1024;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ScratchArena
    {
        internal byte* Base;
        internal nuint Bytes;
        internal nuint Offset;
    }

    internal ScratchArena* Scratch;               // per thread slot

    // ── Thread contexts ─────────────────────────────────────────

    internal const int MaxLockedPartitions = 8;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ThreadContext
    {
        internal KernelThreadId ManagedThreadId;
        internal int SessionGeneration;
        internal int ChainDepth;                  // 0 = not chained
        internal int ChainUserId;
        internal PartitionSlot CurrentPartition;
        internal int InKernel;
        internal int LockCount;                   // partitions locked via PK_THREAD_lock_partitions
        internal fixed int LockedPartitions[MaxLockedPartitions];
        internal int HasMemoryCbs;
        internal delegate* unmanaged[Cdecl]<nuint, nint> AllocFn;
        internal delegate* unmanaged[Cdecl]<nint, void> FreeFn;
        internal int Allocated;                    // slot in use
        internal byte SkipCreationUndo;           // atomic scalar-create command, no active mark
        internal UndoEntry* UndoEntries;
        internal BufferCount UndoEntryCount;
        internal BufferCount UndoEntryCapacity;
        internal DeferredRelease* Deferred;
        internal BufferCount DeferredCount;
        internal BufferCount DeferredCapacity;
    }

    internal ThreadContext* Threads;
    internal int ThreadCount;

    // ── Scheduler ───────────────────────────────────────────────

    internal int SessionGeneration;
    internal int Started;
    internal int HasMark;                         // access via IsMarkActive/SetMarkActive (cross-thread)
    internal int MarkSequence;
    internal int UndoGate;                        // short critical section for undo appends

    public int IsMarkActive => System.Threading.Volatile.Read(ref HasMark);
    public void SetMarkActive(int value) => System.Threading.Volatile.Write(ref HasMark, value);
    internal PartitionSlot DefaultPartition;
    internal int ExclusiveActive;                 // scheduler: exclusive command in progress
    internal int ExclusiveWaiting;                // scheduler: queued exclusives (fairness barrier)
    internal int GlobalWriterActive;              // scheduler: session-wide writers (receive)
    internal int NonLockingLocalActive;           // scheduler: local writers without locked partitions
    internal int ShieldedLocalActive;             // scheduler: locals running under a partition lock
    internal long QueueSequence;

    internal SessionMemory* Memory;
    internal BlockAllocator Blocks;
    internal ReturnAllocator Returns;
    internal TagMap Tags;

    public int PartitionCount;
    private PartitionSlot nextPartitionId;
    public int CurrentPartitionId;                // default partition for threads without context

    public bool IsStarted => Started != 0;

    // ── Construction / teardown ─────────────────────────────────

    public static SessionData* Create(SessionMemory* memory)
    {
        var data = (SessionData*)memory->TryAllocate((nuint)sizeof(SessionData));
        if (data == null) return null;
        *data = default;
        data->Memory = memory;
        data->Blocks.Attach(memory);
        data->Returns.Attach(memory);
        data->Tags.Attach(memory);
        return data;
    }

    public bool Initialize()
    {
        Partitions = (PartitionRecord*)Memory->TryAllocate((nuint)(MaxPartitions * sizeof(PartitionRecord)));
        PartitionUndoHeads = (int*)Memory->TryAllocate((nuint)(MaxPartitions * sizeof(int)));
        Scratch = (ScratchArena*)Memory->TryAllocate((nuint)(MaxThreads * sizeof(ScratchArena)));
        Threads = (ThreadContext*)Memory->TryAllocate((nuint)(MaxThreads * sizeof(ThreadContext)));
        if (Partitions != null) new Span<PartitionRecord>(Partitions, MaxPartitions).Clear();
        if (Scratch != null) new Span<ScratchArena>(Scratch, MaxThreads).Clear();
        if (Threads != null) new Span<ThreadContext>(Threads, MaxThreads).Clear();
        if (Partitions == null || PartitionUndoHeads == null || Scratch == null || Threads == null)
            return false;
        for (int i = 0; i < MaxPartitions; i++)
        {
            Partitions[i].Alive = 0;
            Partitions[i].FirstBody = -1;
            Partitions[i].LastBody = -1;
            PartitionUndoHeads[i] = -1;
        }
        for (int i = 0; i < MaxThreads; i++)
        {
            Scratch[i].Base = null;
            Scratch[i].Bytes = Scratch[i].Offset = 0;
            Threads[i].Allocated = 0;
        }
        PartitionCount = 0;
        CurrentPartitionId = 0;
        if (!TryCreatePartition(0, out _)) return false;
        Started = 1;
        SessionGeneration++;
        Tags.BeginSession();
        return true;
    }

    public bool TryCreatePartition(int id, out PartitionSlot slot)
    {
        slot = -1;
        if (id != nextPartitionId || id >= MaxPartitions) return false;
        for (int i = id; i <= id; i++)
        {
            if (Partitions[i].Alive != 0) continue;
            Partitions[i] = new PartitionRecord
            {
                PartitionId = id,
                Alive = 1,
                FirstBody = -1,
                LastBody = -1,
            };
            PartitionUndoHeads[i] = -1;
            PartitionCount++;
            nextPartitionId++;
            slot = i;
            return true;
        }
        return false;
    }

    public bool TryFindPartition(int id, out PartitionSlot slot)
    {
        for (int i = 0; i < MaxPartitions; i++)
        {
            if (Partitions[i].Alive == 1 && Partitions[i].PartitionId == id)
            {
                slot = i;
                return true;
            }
        }
        slot = -1;
        return false;
    }

    public bool TryAllocatePartitionId(out int id)
    {
        id = nextPartitionId;
        return id < MaxPartitions;
    }

    public void Dispose()
    {
        // Undo snapshots and deferred entries die with the session; the
        // underlying pages return through Trim/Dispose of SessionMemory.
        Started = 0;
        for (var i = 0; i < ThreadCount; i++)
        {
            if (Threads == null || Threads[i].Allocated == 0) continue;
            FreeUndoSnapshots(&Threads[i]);
            Memory->Free(Threads[i].UndoEntries);
            Memory->Free(Threads[i].Deferred);
        }
        if (Partitions != null) { Memory->Free(Partitions); Partitions = null; }
        if (PartitionUndoHeads != null) { Memory->Free(PartitionUndoHeads); PartitionUndoHeads = null; }
        if (Scratch != null)
        {
            for (int i = 0; i < MaxThreads; i++)
                if (Scratch[i].Base != null) Memory->Free(Scratch[i].Base);
            Memory->Free(Scratch);
            Scratch = null;
        }
        if (Threads != null) { Memory->Free(Threads); Threads = null; }
    }

    private void FreeUndoSnapshots(ThreadContext* context)
    {
        for (int i = 0; i < context->UndoEntryCount; i++)
        {
            ref var entry = ref context->UndoEntries[i];
            if (entry.Data != null && entry.Kind == UndoKind.FieldSnapshot)
                Blocks.Free(entry.Data);
        }
    }

    // ── Undo log ────────────────────────────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryAppendUndo(UndoKind kind, byte pool, short partition, int slot,
        int generation, int tag, void* data, nuint dataBytes)
    {
        var context = KernelRuntime.ThreadContext();
        if (context == null) return false;
        if (context->UndoEntryCount == context->UndoEntryCapacity && !TryGrowUndo()) return false;
        ref var entry = ref context->UndoEntries[context->UndoEntryCount++];
        entry = new UndoEntry
        {
            Kind = kind, Pool = pool, Partition = partition, Slot = slot, Generation = generation,
            Tag = tag, Data = data, DataBytes = dataBytes,
            Sequence = IsMarkActive != 0 ? System.Threading.Interlocked.Increment(ref UndoSequence) : 0,
            ThreadId = context->ManagedThreadId, Next = -1,
        };
        return true;
    }

    /// <summary>Undo appends may race growth; callers retry on false.</summary>
    public bool UndoAppendRetried => false;

    private bool TryGrowUndo()
    {
        var newCapacity = UndoEntryCapacity == 0 ? 64 : UndoEntryCapacity * 2;
        var grown = (UndoEntry*)Memory->TryAllocate((nuint)(newCapacity * sizeof(UndoEntry)));
        if (grown == null) return false;
        if (UndoEntries != null)
        {
            Buffer.MemoryCopy(UndoEntries, grown, (nuint)(newCapacity * sizeof(UndoEntry)), (nuint)(UndoEntryCapacity * sizeof(UndoEntry)));
            Memory->Free(UndoEntries);
        }
        UndoEntries = grown;
        UndoEntryCapacity = newCapacity;
        return true;
    }

    public void ClearUndo()
    {
        for (var i = 0; i < ThreadCount; i++)
        {
            if (Threads[i].Allocated == 0) continue;
            FreeUndoSnapshots(&Threads[i]);
            Threads[i].UndoEntryCount = 0;
        }
        UndoSequence = 0;
    }

    public bool TryDeferRelease(byte pool, int slot, int tag)
    {
        if (Deferred == null)
        {
            Deferred = (DeferredRelease*)Memory->TryAllocate((nuint)(32 * sizeof(DeferredRelease)));
            if (Deferred == null) return false;
            DeferredCapacity = 32;            // the initial block sets the capacity too
        }
        // Grows on demand: simple doubling array.
        if (DeferredCount == DeferredCapacity && !TryGrowDeferred())
            return false;
        Deferred[DeferredCount++] = new DeferredRelease { Pool = pool, Slot = slot, Tag = tag };
        return true;
    }

    // Delete never changes liveness before all its log/deferred capacity is
    // secured. These buffers have exactly one writer (the calling context).
    internal bool TryReserveDeletion(BufferCount count)
    {
        if (count < 0 || count > int.MaxValue - UndoEntryCount || count > int.MaxValue - DeferredCount)
            return false;
        while (UndoEntryCapacity - UndoEntryCount < count)
            if (!TryGrowUndo()) return false;
        while (DeferredCapacity - DeferredCount < count)
            if (!TryGrowDeferred()) return false;
        return true;
    }

    private int DeferredCapacity { get => KernelRuntime.ThreadContext()->DeferredCapacity; set => KernelRuntime.ThreadContext()->DeferredCapacity = value; }

    public int DeferredReleaseCapacity => DeferredCapacity;

    private bool TryGrowDeferred()
    {
        var newCapacity = DeferredCapacity == 0 ? 32 : DeferredCapacity * 2;
        var grown = (DeferredRelease*)Memory->TryAllocate((nuint)(newCapacity * sizeof(DeferredRelease)));
        if (grown == null) return false;
        if (Deferred != null)
        {
            Buffer.MemoryCopy(Deferred, grown, (nuint)(newCapacity * sizeof(DeferredRelease)), (nuint)(DeferredCapacity * sizeof(DeferredRelease)));
            Memory->Free(Deferred);
        }
        Deferred = grown;
        DeferredCapacity = newCapacity;
        return true;
    }

    public void ClearDeferred()
    {
        for (var i = 0; i < ThreadCount; i++) Threads[i].DeferredCount = 0;
    }

    // ── Scratch ─────────────────────────────────────────────────

    public bool TryAcquireScratch(int threadSlot, out ScratchArena* arena)
    {
        arena = &Scratch[threadSlot];
        if (arena->Base != null) return true;
        arena->Base = (byte*)Memory->TryAllocate(ScratchSegmentBytes);
        if (arena->Base == null) return false;
        arena->Bytes = ScratchSegmentBytes;
        arena->Offset = 0;
        return true;
    }

    public void ReleaseScratch(int threadSlot) => Scratch[threadSlot].Offset = 0;

    // ── Thread contexts ─────────────────────────────────────────

    public int TryRegisterThread(int managedThreadId, int sessionGeneration)
    {
        for (int i = 0; i < MaxThreads; i++)
        {
            if (Threads[i].Allocated == 0)
            {
                Threads[i] = new ThreadContext
                {
                    ManagedThreadId = managedThreadId,
                    SessionGeneration = sessionGeneration,
                    ChainDepth = 0,
                    CurrentPartition = 0,
                    InKernel = 0,
                    Allocated = 1,
                };
                ThreadCount = Math.Max(ThreadCount, i + 1);
                return i;
            }
        }
        return -1;
    }

    public int FindThread(int managedThreadId)
    {
        for (int i = 0; i < MaxThreads; i++)
            if (Threads[i].Allocated == 1 && Threads[i].ManagedThreadId == managedThreadId)
                return i;
        return -1;
    }

    public void UnregisterThread(int slot)
    {
        if (Scratch[slot].Base != null)
        {
            Memory->Free(Scratch[slot].Base);
            Scratch[slot].Base = null;
        }
        Threads[slot].Allocated = 0;
    }
}
