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
    private nint* directoryAllocations;
    private BufferCount directoryLength;
    private BufferCount pageCount;
    private BufferCount allocatedCount;
    private PoolKind kind;
    private nint localsAddress;
    private LocalState* locals
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (LocalState*)Volatile.Read(ref localsAddress);
        set => Volatile.Write(ref localsAddress, (nint)value);
    }

    private struct LocalState
    {
        internal DataSlot FreeHead;
        internal DataSlot Next;
        internal DataSlot End;
        internal BufferCount Alive;
    }

    public void Attach(SessionMemory* memory, PoolKind poolKind = PoolKind.None)
    {
        this = default;
        owner = memory;
        kind = poolKind;
    }

    public readonly int AllocatedCount => allocatedCount;
    public int AliveCount
    {
        get
        {
            var total = 0;
            if (locals != null)
                for (var i = 0; i < SessionData.MaxPartitions; i++) total += locals[i].Alive;
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
            if (locals == null && !InitializeLocals(authorized)) { slot = -1; return false; }
            ref var local = ref locals[partition];
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
                        var first = Capacity;
                        if (pageCount >= (int.MaxValue >> PageShift) || !TryAddPage()) { slot = -1; return false; }
                        local.Next = first;
                        local.End = Capacity;
                        allocatedCount = Capacity;
                    }
                    finally { if (authorized) Exit(); }
                }
                slot = local.Next++;
                generation = 1;
            }
            ref var record = ref this[slot];
            record = default;
            ref var header = ref Unsafe.As<T, RecordHeader>(ref record);
            header.Generation = generation;
            header.Alive = 1;
            header.Partition = (short)partition;
            local.Alive++;
            if (kind != PoolKind.None && !KernelRuntime.RecordSlotAllocation(kind, slot, generation, Unsafe.AsPointer(ref record)))
            {
                header.Alive = 0;
                header.Tag = local.FreeHead;
                local.FreeHead = slot;
                local.Alive--;
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
        ref var local = ref locals[header.Partition];
        System.Diagnostics.Debug.Assert(header.Alive == 1);
        header.Alive = 0;
        local.Alive--;
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
        locals[header.Partition].Alive--;
        if (!authorized) Exit();
    }

    public void RecycleRetired(DataSlot slot)
    {
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        ref var header = ref Header(slot);
        ref var local = ref locals[header.Partition];
        header.Tag = local.FreeHead;
        local.FreeHead = slot;
        if (!authorized) Exit();
    }

    public void MarkAlive(DataSlot slot)
    {
        var authorized = kind != PoolKind.None && KernelRuntime.Dispatcher.IsExecuting;
        if (!authorized) Enter();
        if (Header(slot).Alive == 0)
        { Header(slot).Alive = 1; locals[Header(slot).Partition].Alive++; }
        if (!authorized) Exit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive(DataSlot slot) => (uint)slot < (uint)allocatedCount && Header(slot).Alive != 0;
    public bool IsValid(DataSlot slot, EntityGeneration generation)
        => IsAlive(slot) && Header(slot).Generation == generation;
    public EntityGeneration GetGeneration(DataSlot slot) => Header(slot).Generation;
    public void* PageOf(DataSlot slot) => ((T**)directoryAddress)[slot >> PageShift];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref RecordHeader Header(DataSlot slot) => ref Unsafe.As<T, RecordHeader>(ref this[slot]);

    private bool TryAddPage()
    {
        if (pageCount == directoryLength)
        {
            var newLength = directoryLength == 0 ? 8 : directoryLength * 2;
            var allocation = (nint*)owner->TryAllocate((nuint)(newLength + 1) * (nuint)sizeof(nint));
            if (allocation == null) return false;
            allocation[0] = (nint)directoryAllocations;
            var grown = (T**)(allocation + 1);
            if (directoryAddress != 0)
                new ReadOnlySpan<nint>((void*)directoryAddress, pageCount).CopyTo(new Span<nint>(grown, pageCount));
            directoryAllocations = allocation;
            directoryLength = newLength;
            Volatile.Write(ref directoryAddress, (nint)grown);
        }
        var page = owner->TryAllocate(SessionMemory.PageSize);
        if (page == null) return false;
        new Span<byte>(page, (int)SessionMemory.PageSize).Clear();
        ((T**)directoryAddress)[pageCount++] = (T*)page;
        return true;
    }

    public void Dispose()
    {
        for (var p = 0; p < pageCount; p++) owner->Free(((T**)directoryAddress)[p]);
        while (directoryAllocations != null)
        {
            var previous = (nint*)directoryAllocations[0];
            owner->Free(directoryAllocations);
            directoryAllocations = previous;
        }
        if (locals != null) owner->Free(locals);
        Attach(owner, kind);
    }

    private bool InitializeLocals(bool authorized)
    {
        if (authorized) Enter();
        try
        {
            if (locals != null) return true;
            var storage = (LocalState*)owner->TryAllocate((nuint)(SessionData.MaxPartitions * sizeof(LocalState)));
            if (storage == null) return false;
            new Span<LocalState>(storage, SessionData.MaxPartitions).Clear();
            for (var i = 0; i < SessionData.MaxPartitions; i++) storage[i].FreeHead = -1;
            locals = storage;
            return true;
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
