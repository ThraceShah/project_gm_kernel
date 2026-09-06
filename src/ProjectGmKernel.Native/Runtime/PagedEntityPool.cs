using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Stable record pages with a short allocator gate. Old directories remain
/// readable until session quiescence: growing a directory cannot free a
/// concurrent reader's lookup. A free record retains its generation/liveness;
/// only its no-longer-published Tag field is reused as the next free slot.
/// </summary>
internal unsafe struct PagedEntityPool<T> where T : unmanaged
{
    private static readonly int PageShift = BitOperations.Log2((uint)(SessionMemory.PageSize / (nuint)sizeof(T)));
    private static readonly int PageMask = (1 << PageShift) - 1;
    private SessionMemory* owner;
    private int gate;
    private nint directoryAddress;
    private nint pageStatesAddress;
    private MemoryPageIndex freePage;
    private nint* directoryAllocations;
    private BufferCount directoryLength;
    private BufferCount pageCount;
    private BufferCount allocatedCount;
    private PoolKind kind;
    private StableTable<LocalState> locals;
    private LocalState defaultLocal;

    private struct LocalState
    {
        internal DataSlot FreeHead;
        internal DataSlot Next;
        internal DataSlot End;
        internal BufferCount Alive;
    }

    private struct PageState
    {
        internal BufferCount Live;
        internal EntityGeneration GenerationBase;
        internal MemoryPageIndex NextFree;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PageState* PageStateOf(DataSlot slot)
        => ((PageState**)Volatile.Read(ref pageStatesAddress))[slot >> PageShift];

    public void Attach(SessionMemory* memory, PoolKind poolKind = PoolKind.None)
    {
        this = default;
        owner = memory;
        kind = poolKind;
        freePage = -1;
        locals.Attach(memory);
        defaultLocal.FreeHead = -1;
    }

    public readonly int AllocatedCount => allocatedCount;
    public int AliveCount
    {
        get
        {
            var total = defaultLocal.Alive;
            for (var i = 1; i < locals.Capacity; i++) total += locals[i].Alive;
            return total;
        }
    }
    public readonly int Capacity => pageCount << PageShift;

    public ref T this[DataSlot slot]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref ((T**)Volatile.Read(ref directoryAddress))[slot >> PageShift][slot & PageMask];
    }

    public bool TryAllocate(out DataSlot slot)
    {
        // The dispatcher already owns this partition's write permission.
        // Only new page/directory acquisition needs the shared allocator gate.
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        try
        {
            var partition = authorized ? KernelRuntime.AllocationPartition : 0;
            if (partition != 0 && partition >= locals.Capacity && !InitializeLocals(partition, authorized)) { slot = -1; return false; }
            ref var local = ref (partition == 0 ? ref defaultLocal : ref locals[partition]);
            EntityGeneration generation;
            if (local.FreeHead >= 0)
            {
                slot = local.FreeHead;
                ref var old = ref Header(slot);
                local.FreeHead = old.Tag;
                generation = old.Generation + 1;
            }
            else
            {
                if (local.Next == local.End)
                {
                    if (authorized) Enter();
                    try
                    {
                        if (!TryAddPage(out var page)) { slot = -1; return false; }
                        local.Next = page << PageShift;
                        local.End = local.Next + PageMask + 1;
                        allocatedCount = Capacity;
                    }
                    finally { if (authorized) Exit(); }
                }
                slot = local.Next++;
                generation = PageStateOf(slot)->GenerationBase;
            }
            ref var record = ref this[slot];
            record = default;
            ref var header = ref Unsafe.As<T, RecordHeader>(ref record);
            header.Generation = generation;
            header.Alive = 1;
            header.Partition = partition;
            local.Alive++;
            PageStateOf(slot)->Live++;
            if (kind != PoolKind.None && !KernelRuntime.RecordSlotAllocation(kind, slot, generation, Unsafe.AsPointer(ref record)))
            {
                header.Alive = 0;
                header.Tag = local.FreeHead;
                local.FreeHead = slot;
                local.Alive--;
                PageStateOf(slot)->Live--;
                slot = -1;
                return false;
            }
            return true;
        }
        finally { if (!authorized) Exit(); }
    }

    public void Free(DataSlot slot)
    {
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        ref var header = ref Header(slot);
        ref var local = ref (header.Partition == 0 ? ref defaultLocal : ref locals[header.Partition]);
        System.Diagnostics.Debug.Assert(header.Alive == 1);
        header.Alive = 0;
        local.Alive--;
        PageStateOf(slot)->Live--;
        header.Tag = local.FreeHead;
        local.FreeHead = slot;
        if (!authorized) Exit();
    }

    public void Retire(DataSlot slot)
    {
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        ref var header = ref Header(slot);
        header.Alive = 0;
        ref var local = ref (header.Partition == 0 ? ref defaultLocal : ref locals[header.Partition]);
        local.Alive--;
        PageStateOf(slot)->Live--;
        if (!authorized) Exit();
    }

    public void RecycleRetired(DataSlot slot)
    {
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        ref var header = ref Header(slot);
        ref var local = ref (header.Partition == 0 ? ref defaultLocal : ref locals[header.Partition]);
        header.Tag = local.FreeHead;
        local.FreeHead = slot;
        if (!authorized) Exit();
    }

    public void MarkAlive(DataSlot slot)
    {
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        if (Header(slot).Alive == 0)
        {
            ref var header = ref Header(slot);
            header.Alive = 1;
            ref var local = ref (header.Partition == 0 ? ref defaultLocal : ref locals[header.Partition]);
            local.Alive++;
            PageStateOf(slot)->Live++;
        }
        if (!authorized) Exit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive(DataSlot slot) => (uint)slot < (uint)allocatedCount && PageOf(slot) != null && Header(slot).Alive != 0;
    public bool IsValid(DataSlot slot, EntityGeneration generation)
        => IsAlive(slot) && Header(slot).Generation == generation;
    public EntityGeneration GetGeneration(DataSlot slot) => Header(slot).Generation;
    public void* PageOf(DataSlot slot) => ((T**)directoryAddress)[slot >> PageShift];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref RecordHeader Header(DataSlot slot) => ref Unsafe.As<T, RecordHeader>(ref this[slot]);

    private bool TryAddPage(out MemoryPageIndex index)
    {
        index = freePage >= 0 ? freePage : pageCount;
        if (index >= (int.MaxValue >> PageShift)) return false;
        if (index == directoryLength)
        {
            var newLength = directoryLength == 0 ? 8 : directoryLength * 2;
            var allocation = (nint*)owner->TryAllocate((nuint)(2 * newLength + 1) * (nuint)sizeof(nint));
            if (allocation == null) return false;
            allocation[0] = (nint)directoryAllocations;
            var grown = (T**)(allocation + 1);
            var states = (PageState**)(allocation + 1 + newLength);
            if (directoryAddress != 0)
            {
                new ReadOnlySpan<nint>((void*)directoryAddress, pageCount).CopyTo(new Span<nint>(grown, pageCount));
                new ReadOnlySpan<nint>((void*)pageStatesAddress, pageCount).CopyTo(new Span<nint>(states, pageCount));
            }
            directoryAllocations = allocation;
            directoryLength = newLength;
            Volatile.Write(ref pageStatesAddress, (nint)states);
            Volatile.Write(ref directoryAddress, (nint)grown);
        }
        var page = owner->TryAllocate(SessionMemory.PageSize);
        if (page == null) return false;
        if (index == pageCount)
        {
            var state = (PageState*)owner->TryAllocate((nuint)sizeof(PageState));
            if (state == null) { owner->Free(page); return false; }
            *state = new PageState { GenerationBase = 1, NextFree = -1 };
            ((PageState**)pageStatesAddress)[index] = state;
            pageCount++;
        }
        else freePage = ((PageState**)pageStatesAddress)[index]->NextFree;
        new Span<byte>(page, (int)SessionMemory.PageSize).Clear();
        ((T**)directoryAddress)[index] = (T*)page;
        return true;
    }

    // Caller must hold session exclusivity with no retained rollback records.
    public void TrimEmptyPages()
    {
        if (pageCount == 0) return;
        // Remove free slots before returning their backing pages.
        for (var p = 0; p < Math.Max(1, locals.Capacity); p++)
        {
            ref var local = ref (p == 0 ? ref defaultLocal : ref locals[p]);
            var slot = local.FreeHead;
            DataSlot previous = -1;
            while (slot >= 0)
            {
                var next = Header(slot).Tag;
                if (PageStateOf(slot)->Live == 0)
                {
                    if (previous < 0) local.FreeHead = next;
                    else Header(previous).Tag = next;
                }
                else previous = slot;
                slot = next;
            }
            if (local.Next != local.End && PageStateOf(local.Next)->Live == 0)
                local.Next = local.End = 0;
        }
        for (var p = 0; p < pageCount; p++)
        {
            var page = ((T**)directoryAddress)[p];
            var state = ((PageState**)pageStatesAddress)[p];
            if (page == null || state->Live != 0) continue;
            // A re-created page must not validate any generation previously
            // issued from it, even when its slots are assigned to a new partition.
            for (var i = 0; i <= PageMask; i++)
                state->GenerationBase = Math.Max(state->GenerationBase, Unsafe.As<T, RecordHeader>(ref page[i]).Generation);
            state->GenerationBase++;
            owner->Free(page);
            ((T**)directoryAddress)[p] = null;
            state->NextFree = freePage;
            freePage = p;
        }
    }

    public void Dispose()
    {
        for (var p = 0; p < pageCount; p++)
        {
            owner->Free(((T**)directoryAddress)[p]);
            owner->Free(((PageState**)pageStatesAddress)[p]);
        }
        while (directoryAllocations != null)
        {
            var previous = (nint*)directoryAllocations[0];
            owner->Free(directoryAllocations);
            directoryAllocations = previous;
        }
        locals.Dispose();
        Attach(owner, kind);
    }

    private bool InitializeLocals(PartitionSlot partition, bool authorized)
    {
        if (authorized) Enter();
        try
        {
            var previous = locals.Capacity;
            var success = locals.TryEnsure(partition);
            for (var i = previous; i < locals.Capacity; i++) locals[i].FreeHead = -1;
            return success;
        }
        finally { if (authorized) Exit(); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Enter()
    {
        while (Interlocked.CompareExchange(ref gate, 1, 0) != 0) Thread.SpinWait(16);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Exit() => Volatile.Write(ref gate, 0);
}
