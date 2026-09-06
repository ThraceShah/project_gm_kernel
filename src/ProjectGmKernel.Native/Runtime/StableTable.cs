using System.Runtime.CompilerServices;
using System.Threading;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Unmanaged metadata in stable 64-record segments. Growth publishes a new
/// directory while retaining old directories for concurrent readers. Callers
/// serialize growth; individual records remain independently writable.
/// </summary>
internal unsafe struct StableTable<T> where T : unmanaged
{
    private const int Shift = 6;
    private SessionMemory* owner;
    private nint directory;
    private nint* allocations;
    private BufferCount directoryCapacity;
    private BufferCount segments;
    private int gate;

    internal void Attach(SessionMemory* memory) { this = default; owner = memory; }
    internal readonly BufferCount Capacity => Volatile.Read(ref Unsafe.AsRef(in segments)) << Shift;

    internal ref T this[BufferOffset index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref *Pointer(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T* Pointer(BufferOffset index)
        => ((T**)Volatile.Read(ref directory))[index >> Shift] + (index & 63);

    internal bool TryEnsure(BufferOffset index)
    {
        if (index < 0 || index >= int.MaxValue - 63) return false;
        if (index < Capacity) return true;
        while (Interlocked.CompareExchange(ref gate, 1, 0) != 0) Thread.SpinWait(16);
        try { return TryGrow(index); }
        finally { Volatile.Write(ref gate, 0); }
    }

    private bool TryGrow(BufferOffset index)
    {
        while (index >= Capacity)
        {
            if (segments == directoryCapacity)
            {
                var nextCapacity = directoryCapacity == 0 ? 4 : directoryCapacity * 2;
                var allocation = (nint*)owner->TryAllocate((nuint)(nextCapacity + 1) * (nuint)sizeof(nint));
                if (allocation == null) return false;
                allocation[0] = (nint)allocations;
                if (segments != 0)
                    new ReadOnlySpan<nint>((void*)directory, segments).CopyTo(new Span<nint>(allocation + 1, segments));
                allocations = allocation;
                directoryCapacity = nextCapacity;
                Volatile.Write(ref directory, (nint)(allocation + 1));
            }
            var page = (T*)owner->TryAllocate(64 * (nuint)sizeof(T));
            if (page == null) return false;
            new Span<T>(page, 64).Clear();
            ((T**)directory)[segments] = page;
            Volatile.Write(ref segments, segments + 1);
        }
        return true;
    }

    internal void Dispose()
    {
        for (var i = 0; i < segments; i++) owner->Free(((T**)directory)[i]);
        while (allocations != null)
        {
            var previous = (nint*)allocations[0];
            owner->Free(allocations);
            allocations = previous;
        }
        Attach(owner);
    }
}
