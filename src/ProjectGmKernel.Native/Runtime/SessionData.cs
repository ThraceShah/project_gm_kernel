using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Unmanaged per-session state: partitions, the undo log, deferred-release
/// queues, per-thread contexts and scheduler bookkeeping. All storage comes
/// from <see cref="SessionMemory"/>. Owned by the session; freed on stop.
/// </summary>
internal unsafe struct SessionData
{
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
        internal byte AtPmark;
        internal byte MarkHasEntities;
    }

    internal enum PartitionLockState : int
    {
        Unlocked = 0,
        SharedRead = 1,
        ExclusiveWrite = 2,
    }

    internal StableTable<PartitionRecord> Partitions;

    // ── Undo log (single global mark; per-partition ordered entries) ──

    internal enum UndoKind : byte
    {
        EntityCreated = 1,                        // entity added after mark: destroy on goto
        EntityDeleted = 2,                        // entity deleted after mark: restore on goto
        FieldSnapshot = 3,                        // full-record image before modification
        BlockReplaced = 4,                        // variable-length block swap
        PartitionCreated = 5,
        PartitionDeleted = 6,
        CurrentPartitionChanged = 7,
        BodyUnlinked = 8,
        GeometryReferenceReleased = 9,
        PartitionModified = 10,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UndoEntry
    {
        internal UndoKind Kind;
        internal byte Pool;                       // PoolKind for entity entries
        internal PartitionSlot Partition;
        internal DataSlot Slot;
        internal EntityGeneration Generation;
        internal EntityTag Tag;
        internal void* Data;                      // snapshot copy / old block handle
        internal void* Target;                    // stable address of the snapshotted fields
        internal nuint DataBytes;
        internal int Sequence;                    // global ordering for reverse replay
        internal KernelThreadId ThreadId;         // owning command's thread (failure undo)
        internal int Next;                        // chain per partition, -1 ends
        internal BodySlot PreviousBody;
        internal BodySlot FollowingBody;
        internal BodySlot FirstBody;
        internal BodySlot LastBody;
    }

    internal enum UndoBookkeeping : byte
    {
        Cancelled = 0,                            // must not alias EntityCreated (= 1)
    }

    internal UndoEntry* UndoEntries { get => KernelRuntime.ThreadContext()->UndoEntries; set => KernelRuntime.ThreadContext()->UndoEntries = value; }
    internal int UndoEntryCount { get => KernelRuntime.ThreadContext()->UndoEntryCount; set => KernelRuntime.ThreadContext()->UndoEntryCount = value; }
    internal int UndoEntryCapacity { get => KernelRuntime.ThreadContext()->UndoEntryCapacity; set => KernelRuntime.ThreadContext()->UndoEntryCapacity = value; }
    internal int UndoSequence;

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

    internal StableTable<ScratchArena> Scratch;

    // ── Thread contexts ─────────────────────────────────────────

    internal const int StackLockedPartitions = 8;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ThreadContext
    {
        internal KernelThreadId ManagedThreadId;
        internal int SessionGeneration;
        internal BufferCount ChainLength;          // configured link length; 0 = unlimited
        internal KernelChainType ChainType;       // 0 = not chained
        internal BufferCount ChainRemaining;
        internal byte ChainHeld;
        internal ApplicationThreadId UserThreadId;
        internal PartitionSlot CurrentPartition;
        internal int InKernel;
        internal BufferCount LockCount;
        internal BufferCount LockCapacity;
        internal PartitionSlot* LockedPartitions;
        internal int HasMemoryCbs;
        internal delegate* unmanaged[Cdecl]<nuint, nint> AllocFn;
        internal delegate* unmanaged[Cdecl]<nint, void> FreeFn;
        internal int Allocated;                    // slot in use
        internal byte SkipCreationUndo;           // atomic scalar-create command, no active mark
        internal byte DeferCommandDeletes;
        internal byte PartitionDeletionPending;
        internal byte ExecutionIsExclusive;
        internal UndoEntry* UndoEntries;
        internal BufferCount UndoEntryCount;
        internal BufferCount UndoEntryCapacity;
        internal BufferCount UndoSnapshotCount;
        internal BufferOffset ReplayPosition;
        internal DeferredRelease* Deferred;
        internal BufferCount DeferredCount;
        internal BufferCount DeferredCapacity;
    }

    internal StableTable<ThreadContext> Threads;
    internal int ThreadCount;

    // ── Scheduler ───────────────────────────────────────────────

    internal int SessionGeneration;
    internal int Started;
    internal int HasMark;                         // access via IsMarkActive/SetMarkActive (cross-thread)
    internal int MarkSequence;
    internal PartitionSlot MarkCurrentPartition;
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
    internal readonly BufferCount PartitionHighWater => nextPartitionId;
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
        Partitions.Attach(Memory);
        Scratch.Attach(Memory);
        Threads.Attach(Memory);
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
        if (id != nextPartitionId || !Partitions.TryEnsure(id)) return false;
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
            PartitionCount++;
            nextPartitionId++;
            slot = i;
            return true;
        }
        return false;
    }

    public bool TryFindPartition(int id, out PartitionSlot slot)
    {
        if ((uint)id < (uint)nextPartitionId && Partitions[id].Alive == 1)
        {
            slot = id;
            return true;
        }
        slot = -1;
        return false;
    }

    public bool TryAllocatePartitionId(out int id)
    {
        id = nextPartitionId;
        return id < int.MaxValue - 63;
    }

    public void Dispose()
    {
        // Undo snapshots and deferred entries die with the session; the
        // underlying pages return through Trim/Dispose of SessionMemory.
        Started = 0;
        for (var i = 0; i < ThreadCount; i++)
        {
            if (Threads[i].Allocated == 0) continue;
            FreeUndoSnapshots(Threads.Pointer(i));
            Memory->Free(Threads[i].UndoEntries);
            Memory->Free(Threads[i].Deferred);
            Memory->Free(Threads[i].LockedPartitions);
        }
        Partitions.Dispose();
        for (int i = 0; i < Scratch.Capacity; i++)
            if (Scratch[i].Base != null) Memory->Free(Scratch[i].Base);
        Scratch.Dispose();
        Threads.Dispose();
    }

    private void FreeUndoSnapshots(ThreadContext* context)
    {
        if (context->UndoSnapshotCount == 0) return;
        for (int i = 0; i < context->UndoEntryCount; i++)
        {
            ref var entry = ref context->UndoEntries[i];
            if (entry.Data != null && entry.Kind == UndoKind.FieldSnapshot)
                Blocks.Free(entry.Data);
        }
        context->UndoSnapshotCount = 0;
    }

    /// <summary>Before-image of an entity record beginning with RecordHeader.</summary>
    internal bool TrySnapshot<T>(ref T value) where T : unmanaged
    {
        var copy = Blocks.TryAllocate((nuint)sizeof(T));
        if (copy == null) return false;
        *(T*)copy = value;
        var partition = System.Runtime.CompilerServices.Unsafe.As<T, RecordHeader>(ref value).Partition;
        if (!TryAppendUndo(UndoKind.FieldSnapshot, 0, partition, 0, 0, 0, copy, (nuint)sizeof(T)))
        {
            Blocks.Free(copy);
            return false;
        }
        UndoEntries[UndoEntryCount - 1].Target = System.Runtime.CompilerServices.Unsafe.AsPointer(ref value);
        KernelRuntime.ThreadContext()->UndoSnapshotCount++;
        return true;
    }

    internal void DiscardUndoFrom(BufferOffset boundary)
    {
        var context = KernelRuntime.ThreadContext();
        if (context->UndoSnapshotCount != 0)
        for (var i = boundary; i < UndoEntryCount; i++)
        {
            ref var entry = ref UndoEntries[i];
            if (entry.Kind == UndoKind.FieldSnapshot && entry.Data != null)
            {
                Blocks.Free(entry.Data);
                context->UndoSnapshotCount--;
            }
        }
        UndoEntryCount = boundary;
    }

    // ── Undo log ────────────────────────────────────────────────

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryAppendUndo(UndoKind kind, byte pool, PartitionSlot partition, int slot,
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
            FreeUndoSnapshots(Threads.Pointer(i));
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
        arena = null;
        if (!Scratch.TryEnsure(threadSlot)) return false;
        arena = Scratch.Pointer(threadSlot);
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
        if (!Threads.TryEnsure(ThreadCount)) return -1;
        for (int i = 0; i <= ThreadCount; i++)
        {
            if (Threads[i].Allocated == 0)
            {
                Threads[i] = new ThreadContext
                {
                    ManagedThreadId = managedThreadId,
                    SessionGeneration = sessionGeneration,
                    ChainLength = 0,
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

    internal bool TryReserveLocks(ThreadContext* context, BufferCount count)
    {
        if (count <= context->LockCapacity) return true;
        var capacity = context->LockCapacity == 0 ? StackLockedPartitions : context->LockCapacity;
        while (capacity < count) capacity = capacity > int.MaxValue / 2 ? count : capacity * 2;
        var bytes = (nuint)capacity * sizeof(PartitionSlot);
        var storage = (PartitionSlot*)Memory->TryAllocate(bytes);
        if (storage == null) return false;
        if (context->LockedPartitions != null)
        {
            Buffer.MemoryCopy(context->LockedPartitions, storage, bytes, (nuint)context->LockCount * sizeof(PartitionSlot));
            Memory->Free(context->LockedPartitions);
        }
        context->LockedPartitions = storage;
        context->LockCapacity = capacity;
        return true;
    }

    public int FindThread(int managedThreadId)
    {
        for (int i = 0; i < ThreadCount; i++)
            if (Threads[i].Allocated == 1 && Threads[i].ManagedThreadId == managedThreadId)
                return i;
        return -1;
    }

    public void UnregisterThread(int slot)
    {
        if (slot < Scratch.Capacity && Scratch[slot].Base != null)
        {
            Memory->Free(Scratch[slot].Base);
            Scratch[slot].Base = null;
        }
        Threads[slot].Allocated = 0;
    }
}
