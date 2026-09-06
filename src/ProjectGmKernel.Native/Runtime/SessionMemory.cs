using System.Runtime.InteropServices;
using System.Threading;

namespace ProjectGmKernel.Native.Runtime;

internal readonly record struct MemoryStatistics(
    ulong RequestedBytes, ulong LiveBytes, ulong CapacityBytes, ulong CachedBytes,
    ulong PeakCapacityBytes, ulong AllocationCount, ulong LiveBlocks);

/// <summary>
/// Sole system allocator for a session. The owner must outlive all its clients.
/// Its gate protects bookkeeping only; model operations never run under it.
/// </summary>
internal unsafe struct SessionMemory
{
    internal const nuint PageSize = 64 * 1024;
    internal const nuint CacheLimit = 2 * 1024 * 1024;
    private const nuint Alignment = 64;
    private const nuint HeaderSize = 64;

    [StructLayout(LayoutKind.Sequential)]
    private struct Block
    {
        internal Block* Previous;
        internal Block* Next;
        internal nuint Requested;
        internal nuint Capacity;
    }

    private Block* active;
    private Block* cached;
    private int gate;
    private ulong requestedBytes;
    private ulong liveBytes;
    private ulong capacityBytes;
    private ulong cachedBytes;
    private ulong peakCapacityBytes;
    private ulong allocationCount;
    private ulong liveBlocks;
    private AllocationSequence failureCountdown;
    private bool injectFailure;

    public void FailAfter(AllocationSequence successfulRequests)
    {
        Enter();
        failureCountdown = successfulRequests;
        injectFailure = successfulRequests >= 0;
        Exit();
    }

    public void* TryAllocate(nuint bytes)
    {
        if (bytes == 0) return null;
        if (bytes > nuint.MaxValue - HeaderSize - Alignment) return null;
        var capacity = (bytes + Alignment - 1) & ~(Alignment - 1);
        Enter();
        try
        {
            if (injectFailure && failureCountdown-- <= 0) return null;
            Block* block;
            if (capacity == PageSize && cached != null)
            {
                block = cached;
                cached = block->Next;
                cachedBytes -= (ulong)PageSize;
            }
            else
            {
                block = (Block*)NativeMemory.AlignedAlloc(capacity + HeaderSize, Alignment);
                if (block == null) return null;
                capacityBytes += (ulong)(capacity + HeaderSize);
                peakCapacityBytes = Math.Max(peakCapacityBytes, capacityBytes);
                allocationCount++;
            }
            block->Requested = bytes;
            block->Capacity = capacity;
            block->Previous = null;
            block->Next = active;
            if (active != null) active->Previous = block;
            active = block;
            requestedBytes += (ulong)bytes;
            liveBytes += (ulong)bytes;
            liveBlocks++;
            return (byte*)block + HeaderSize;
        }
        finally { Exit(); }
    }

    public void Free(void* pointer)
    {
        if (pointer == null) return;
        var block = (Block*)((byte*)pointer - HeaderSize);
        Enter();
        try
        {
            if (block->Previous != null) block->Previous->Next = block->Next;
            else active = block->Next;
            if (block->Next != null) block->Next->Previous = block->Previous;
            liveBytes -= (ulong)block->Requested;
            liveBlocks--;
            if (block->Capacity == PageSize && cachedBytes + (ulong)PageSize <= (ulong)CacheLimit)
            {
                block->Next = cached;
                cached = block;
                cachedBytes += (ulong)PageSize;
            }
            else
            {
                capacityBytes -= (ulong)(block->Capacity + HeaderSize);
                NativeMemory.AlignedFree(block);
            }
        }
        finally { Exit(); }
    }

    public MemoryStatistics Statistics
    {
        get
        {
            Enter();
            var result = new MemoryStatistics(requestedBytes, liveBytes, capacityBytes,
                cachedBytes, peakCapacityBytes, allocationCount, liveBlocks);
            Exit();
            return result;
        }
    }

    public void Trim()
    {
        Enter();
        while (cached != null)
        {
            var next = cached->Next;
            capacityBytes -= (ulong)(cached->Capacity + HeaderSize);
            NativeMemory.AlignedFree(cached);
            cached = next;
        }
        cachedBytes = 0;
        Exit();
    }

    // Caller has already drained commands and released external ownership.
    public void Dispose()
    {
        Trim();
        while (active != null)
        {
            var next = active->Next;
            NativeMemory.AlignedFree(active);
            active = next;
        }
        liveBytes = capacityBytes = liveBlocks = 0;
        injectFailure = false;
    }

    private void Enter()
    {
        while (Interlocked.CompareExchange(ref gate, 1, 0) != 0) Thread.SpinWait(32);
    }

    private void Exit() => Volatile.Write(ref gate, 0);
}
