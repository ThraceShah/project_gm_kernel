using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Ownership registry for memory handed to callers. Every returned pointer is
/// an independent allocation; freeing routes through this table so the right
/// owner (kernel session pool or caller frustrum callback) releases it.
/// The registry is an open-addressing hash of pointer → entry with linear
/// probing; free slots hold null pointers. Callbacks run outside the lock.
/// Blocks still held by callers are neither trimmed nor reused.
/// </summary>
internal unsafe struct ReturnAllocator
{
    private enum Source : byte { SessionPool, CallerFrustrum }

    /// <summary>One live registry slot; Pointer == null marks an empty slot.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Entry
    {
        internal void* Pointer;
        internal nuint Bytes;
        internal Source Source;
        internal byte Pad1, Pad2, Pad3, Pad4, Pad5, Pad6, Pad7;
        internal delegate* unmanaged[Cdecl]<nint, void> FreeFn;
        internal AllocationSequence Sequence;
        internal KernelThreadId ThreadId;
    }

    private const int InitialCapacity = 256;      // power of two

    private SessionMemory* owner;
    private BlockAllocator blocks;
    private Entry* table;
    private int capacity;                         // power-of-two slot count
    private int used;                             // live entries
    private ulong outstandingBytes;
    private int gate;
    private AllocationSequence sequence;
    internal readonly AllocationSequence Sequence => sequence;

    private delegate* unmanaged[Cdecl]<nuint, nint> frustrumAlloc;
    private delegate* unmanaged[Cdecl]<nint, void> frustrumFree;

    public void Attach(SessionMemory* memory)
    {
        owner = memory;
        blocks.Attach(memory);
        table = null;
        capacity = used = 0;
        outstandingBytes = 0;
        gate = 0;
    }

    public readonly ulong OutstandingBytes => outstandingBytes;
    public readonly ulong OutstandingBlocks => (ulong)used;
    public readonly bool HasFrustrumCallbacks => frustrumFree != null;

    /// <summary>Register thread-level frustrum callbacks used for new allocations.</summary>
    public void SetFrustrumCallbacks(
        delegate* unmanaged[Cdecl]<nuint, nint> allocFn,
        delegate* unmanaged[Cdecl]<nint, void> freeFn)
    {
        Enter();
        frustrumAlloc = allocFn;
        frustrumFree = freeFn;
        Exit();
    }

    public void ClearFrustrumCallbacks()
    {
        Enter();
        frustrumAlloc = null;
        frustrumFree = null;
        Exit();
    }

    /// <summary>
    /// Allocate an independently freed result buffer. When frustrum callbacks
    /// are registered the allocation happens through them (outside the lock);
    /// otherwise it comes from the session pool.
    /// </summary>
    public void* TryAllocate(nuint bytes)
    {
        if (bytes == 0) return null;
        var context = owner == KernelRuntime.MemoryPointer() ? KernelRuntime.ThreadContext() : null;
        Enter();
        var allocFn = context != null && context->HasMemoryCbs != 0 ? context->AllocFn : frustrumAlloc;
        var freeFn = context != null && context->HasMemoryCbs != 0 ? context->FreeFn : frustrumFree;
        Exit();
        var useCallback = allocFn != null && freeFn != null;
        void* pointer = useCallback ? (void*)allocFn(bytes) : blocks.TryAllocate(bytes);
        if (pointer == null) return null;
        bool registered;
        Enter();
        try
        {
            registered = TryInsert(pointer, bytes,
                useCallback ? Source.CallerFrustrum : Source.SessionPool,
                useCallback ? freeFn : null);
            if (registered) outstandingBytes += bytes;
        }
        finally { Exit(); }
        if (registered) return pointer;
        if (useCallback) freeFn((nint)pointer);
        else blocks.Free(pointer);
        return null;
    }

    /// <summary>Free a previously returned pointer. Returns false for foreign pointers.</summary>
    public bool TryFree(void* pointer)
    {
        if (pointer == null) return true;
        Entry saved;
        Enter();
        try
        {
            var slot = FindSlot(pointer);
            if (slot < 0) return false;
            saved = table[slot];
            DeleteSlot(slot);
            used--;
            outstandingBytes -= saved.Bytes;
        }
        finally { Exit(); }

        // Release outside the lock; callbacks and pool frees may contend.
        if (saved.Source == Source.SessionPool) blocks.Free(saved.Pointer);
        else if (saved.FreeFn != null) saved.FreeFn((nint)saved.Pointer);
        return true;
    }

    /// <summary>
    /// Register an externally produced buffer (e.g. transmit bytes from the XT
    /// adapter) so <c>PK_MEMORY_block_f</c> can free it correctly.
    /// </summary>
    public bool TryRegister(void* pointer, nuint bytes,
        delegate* unmanaged[Cdecl]<nint, void> freeFn)
    {
        if (pointer == null || bytes == 0) return false;
        Enter();
        try
        {
            if (FindSlot(pointer) >= 0) return false;
            if (!TryInsert(pointer, bytes, Source.CallerFrustrum, freeFn))
                return false;
            outstandingBytes += bytes;
            return true;
        }
        finally { Exit(); }
    }

    /// <summary>Force-free every outstanding block (session stop path).</summary>
    public void ReleaseAll()
    {
        while (true)
        {
            Enter();
            var pending = table;
            var count = capacity;
            table = null;
            capacity = used = 0;
            outstandingBytes = 0;
            Exit();
            if (pending == null) return;
            // Visit each slot once, rather than rescanning the table from
            // zero for every returned buffer (quadratic session shutdown).
            for (var i = 0; i < count; i++)
            {
                if (pending[i].Pointer == null) continue;
                if (pending[i].Source == Source.CallerFrustrum && pending[i].FreeFn != null)
                    pending[i].FreeFn((nint)pending[i].Pointer);
                else blocks.Free(pending[i].Pointer);
            }
            owner->Free(pending);
        }
    }

    public void Reset() => ReleaseAll();

    // ── Open-addressing table (linear probe, backward-shift delete) ──

    private bool TryInsert(void* pointer, nuint bytes, Source source,
        delegate* unmanaged[Cdecl]<nint, void> freeFn)
    {
        if (table == null && !TryGrow(InitialCapacity))
            return false;
        if ((uint)(used + 1) > (uint)capacity / 2 && !TryGrow(capacity * 2))
            return false;
        var mask = capacity - 1;
        var slot = (int)(Hash(pointer) & (nuint)mask);
        while (table[slot].Pointer != null)
            slot = (slot + 1) & mask;
        table[slot] = new Entry
        {
            Pointer = pointer,
            Bytes = bytes,
            Source = source,
            FreeFn = freeFn,
            Sequence = ++sequence,
            ThreadId = Environment.CurrentManagedThreadId,
        };
        used++;
        return true;
    }

    private int FindSlot(void* pointer)
    {
        if (table == null) return -1;
        var mask = capacity - 1;
        var slot = (int)(Hash(pointer) & (nuint)mask);
        while (true)
        {
            if (table[slot].Pointer == pointer) return slot;
            if (table[slot].Pointer == null) return -1;   // probe ends at empty slot
            slot = (slot + 1) & mask;
        }
    }

    // Called before a failed command releases its scheduler claim. Only
    // unpublished results of that calling thread are reclaimed.
    internal void ReleaseSince(AllocationSequence boundary, int threadId)
    {
        Enter();
        var end = sequence;
        Exit();
        while (true)
        {
            Entry saved = default;
            Enter();
            for (var i = 0; i < capacity; i++)
            {
                if (table[i].Pointer == null || table[i].ThreadId != threadId
                    || table[i].Sequence <= boundary || table[i].Sequence > end) continue;
                saved = table[i];
                DeleteSlot(i);
                used--;
                outstandingBytes -= saved.Bytes;
                break;
            }
            Exit();
            if (saved.Pointer == null) return;
            if (saved.Source == Source.SessionPool) blocks.Free(saved.Pointer);
            else if (saved.FreeFn != null) saved.FreeFn((nint)saved.Pointer);
        }
    }

    /// <summary>Backward-shift deletion keeps probe chains contiguous.</summary>
    private void DeleteSlot(int slot)
    {
        var mask = capacity - 1;
        table[slot].Pointer = null;
        var current = (slot + 1) & mask;
        while (table[current].Pointer != null)
        {
            var home = (int)(Hash(table[current].Pointer) & (nuint)mask);
            // Move when home is NOT cyclically in (hole, current]; otherwise the
            // entry keeps probing past the hole and stays where it is.
            var inRange = slot >= current
                ? home > slot || home <= current
                : home > slot && home <= current;
            if (!inRange)
            {
                table[slot] = table[current];
                table[current].Pointer = null;
                slot = current;
            }
            current = (current + 1) & mask;
        }
    }

    private bool TryGrow(int newCapacity)
    {
        var grown = (Entry*)owner->TryAllocate((nuint)(newCapacity * sizeof(Entry)));
        if (grown == null) return false;
        for (int i = 0; i < newCapacity; i++) grown[i].Pointer = null;
        var old = table;
        var oldCapacity = capacity;
        table = grown;
        capacity = newCapacity;
        var mask = newCapacity - 1;
        for (int i = 0; i < oldCapacity; i++)
        {
            if (old[i].Pointer == null) continue;
            var slot = (int)(Hash(old[i].Pointer) & (nuint)mask);
            while (table[slot].Pointer != null)
                slot = (slot + 1) & mask;
            table[slot] = old[i];
        }
        if (old != null) owner->Free(old);
        return true;
    }

    private static nuint Hash(void* pointer)
        => (nuint)(((nuint)pointer >> 4) * 0x9E3779B97F4A7C15ul);

    private void Enter()
    {
        while (System.Threading.Interlocked.CompareExchange(ref gate, 1, 0) != 0)
            System.Threading.Thread.SpinWait(32);
    }

    private void Exit() => System.Threading.Volatile.Write(ref gate, 0);
}
