using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public unsafe class SessionMemoryTests
{
    [Fact]
    public void PagesAreAlignedReusedAndCacheIsBounded()
    {
        SessionMemory memory = default;
        try
        {
            var pages = stackalloc nint[40];
            for (var i = 0; i < 40; i++)
            {
                pages[i] = (nint)memory.TryAllocate(SessionMemory.PageSize);
                Assert.NotEqual(0, pages[i]);
                Assert.Equal((nuint)0, (nuint)pages[i] % 64);
            }
            for (var i = 0; i < 40; i++) memory.Free((void*)pages[i]);
            Assert.Equal((ulong)SessionMemory.CacheLimit, memory.Statistics.CachedBytes);
            Assert.Equal(0UL, memory.Statistics.LiveBytes);
            var allocations = memory.Statistics.AllocationCount;
            var reused = memory.TryAllocate(SessionMemory.PageSize);
            Assert.Equal(allocations, memory.Statistics.AllocationCount);
            memory.Free(reused);
            memory.Trim();
            Assert.Equal(0UL, memory.Statistics.CapacityBytes);
        }
        finally { memory.Dispose(); }
    }

    [Fact]
    public void FailureDoesNotChangeOwnershipAndLargeBlocksAreNotCached()
    {
        SessionMemory memory = default;
        try
        {
            memory.FailAfter(1);
            var block = memory.TryAllocate(100_000);
            Assert.True(block != null);
            var before = memory.Statistics;
            Assert.True(memory.TryAllocate(64) == null);
            Assert.Equal(before, memory.Statistics);
            memory.Free(block);
            Assert.Equal(0UL, memory.Statistics.CapacityBytes);
            memory.FailAfter(-1);
            Assert.True(memory.TryAllocate(1) != null);
        }
        finally { memory.Dispose(); }
        Assert.Equal(0UL, memory.Statistics.LiveBlocks);
        Assert.Equal(0UL, memory.Statistics.CapacityBytes);
    }

    [Fact]
    public void ReusedPageRequestsDoNotAllocateManagedMemory()
    {
        SessionMemory memory = default;
        try
        {
            memory.Free(memory.TryAllocate(SessionMemory.PageSize));
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++)
                memory.Free(memory.TryAllocate(SessionMemory.PageSize));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
        }
        finally { memory.Dispose(); }
    }
}
