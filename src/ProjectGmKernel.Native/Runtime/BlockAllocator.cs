using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>16-byte aligned blocks, two-level size bins, physical coalescing
/// and immediate return of empty pages. Payload never stores allocator metadata.</summary>
internal unsafe struct BlockAllocator
{
    internal const nuint LargeBlockThreshold = 32 * 1024;
    private const int FirstLevels = 17, SecondLevels = 16;
    private const nuint HeaderBytes = 64, PageHeaderBytes = 64, MinimumRegion = 80;

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct Header
    {
        internal nuint Size;
        internal nuint Requested;
        internal Header* Previous;
        internal Header* Next;
        internal Header* PhysicalPrevious;
        internal Header* PhysicalNext;
        internal Page* Page;
        internal DataSlot Handle;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct Page
    {
        internal Page* Previous;
        internal Page* Next;
        internal BufferCount Live;
    }

    private struct HandleEntry
    {
        internal void* Pointer;
        internal DataSlot NextFree;
    }

    private SessionMemory* owner;
    private Page* pages;
    private Header** heads;
    private uint firstBitmap;
    private fixed uint secondBitmap[FirstLevels];
    private HandleEntry* handles;
    private BufferCount handleCount, handleCapacity;
    private DataSlot freeHandle;
    private ulong liveBytes, liveBlocks, peakLiveBytes;
    private int gate;

    public void Attach(SessionMemory* memory) { this = default; owner = memory; freeHandle = -1; }
    public readonly ulong LiveBytes => liveBytes;
    public readonly BlockStatistics Statistics => new(liveBytes, liveBlocks, peakLiveBytes);

    public void* TryAllocate(nuint bytes)
    {
        if (bytes == 0) bytes = 1;
        if (bytes > nuint.MaxValue - HeaderBytes - 15) return null;
        var needed = ((bytes + 15) & ~(nuint)15) + HeaderBytes;
        Enter();
        try
        {
            Header* block;
            if (bytes > LargeBlockThreshold)
            {
                block = (Header*)owner->TryAllocate(needed);
                if (block == null) return null;
                *block = default;
                block->Size = needed;
            }
            else
            {
                if (!EnsureHeads()) return null;
                block = FindFree(needed);
                if (block == null)
                {
                    if (!AddPage()) return null;
                    block = FindFree(needed);
                }
                Remove(block);
                if (block->Size - needed >= MinimumRegion) Split(block, needed);
                block->Page->Live++;
            }
            block->Size |= 1;
            block->Requested = bytes;
            block->Handle = 0;
            liveBytes += (ulong)bytes;
            liveBlocks++;
            peakLiveBytes = Math.Max(peakLiveBytes, liveBytes);
            return (byte*)block + HeaderBytes;
        }
        finally { Exit(); }
    }

    public void Free(void* pointer)
    {
        if (pointer == null) return;
        Enter();
        try
        {
            var block = (Header*)((byte*)pointer - HeaderBytes);
            System.Diagnostics.Debug.Assert((block->Size & 1) != 0);
            if (block->Handle > 0)
            {
                var index = block->Handle - 1;
                handles[index].Pointer = null;
                handles[index].NextFree = freeHandle;
                freeHandle = index;
                block->Handle = 0;
            }
            liveBytes -= (ulong)block->Requested;
            liveBlocks--;
            if (block->Page == null) { owner->Free(block); return; }
            block->Size &= ~(nuint)1;
            var page = block->Page;
            page->Live--;
            var before = block->PhysicalPrevious;
            if (before != null && (before->Size & 1) == 0)
            {
                Remove(before);
                before->Size += block->Size;
                before->PhysicalNext = block->PhysicalNext;
                if (before->PhysicalNext != null) before->PhysicalNext->PhysicalPrevious = before;
                block = before;
            }
            var after = block->PhysicalNext;
            if (after != null && (after->Size & 1) == 0)
            {
                Remove(after);
                block->Size += after->Size;
                block->PhysicalNext = after->PhysicalNext;
                if (block->PhysicalNext != null) block->PhysicalNext->PhysicalPrevious = block;
            }
            if (page->Live == 0)
            {
                if (page->Previous != null) page->Previous->Next = page->Next;
                else pages = page->Next;
                if (page->Next != null) page->Next->Previous = page->Previous;
                owner->Free(page);
            }
            else Insert(block);
        }
        finally { Exit(); }
    }

    public DataSlot HandleOf(void* pointer)
    {
        if (pointer == null) return -1;
        Enter();
        try
        {
            var header = (Header*)((byte*)pointer - HeaderBytes);
            if (header->Handle > 0) return header->Handle;
            DataSlot index;
            if (freeHandle >= 0) { index = freeHandle; freeHandle = handles[index].NextFree; }
            else
            {
                if (handleCount == handleCapacity)
                {
                    if (handleCapacity >= 1 << 29) return -1;
                    var size = handleCapacity == 0 ? 64 : handleCapacity * 2;
                    var grown = (HandleEntry*)owner->TryAllocate((nuint)size * (nuint)sizeof(HandleEntry));
                    if (grown == null) return -1;
                    new ReadOnlySpan<HandleEntry>(handles, handleCount).CopyTo(new Span<HandleEntry>(grown, size));
                    if (handles != null) owner->Free(handles);
                    handles = grown;
                    handleCapacity = size;
                }
                index = handleCount++;
            }
            handles[index].Pointer = pointer;
            header->Handle = index + 1;
            return index + 1;
        }
        finally { Exit(); }
    }

    public void* BlockPointer(DataSlot handle)
    {
        Enter();
        var pointer = handle > 0 && handle <= handleCount ? handles[handle - 1].Pointer : null;
        Exit();
        return pointer; // the entity read authorization keeps this block alive
    }

    private bool EnsureHeads()
    {
        if (heads != null) return true;
        heads = (Header**)owner->TryAllocate((nuint)(FirstLevels * SecondLevels * sizeof(nint)));
        if (heads == null) return false;
        new Span<nint>(heads, FirstLevels * SecondLevels).Clear();
        return true;
    }

    private bool AddPage()
    {
        var page = (Page*)owner->TryAllocate(SessionMemory.PageSize);
        if (page == null) return false;
        *page = default;
        page->Next = pages;
        if (pages != null) pages->Previous = page;
        pages = page;
        var first = (Header*)((byte*)page + PageHeaderBytes);
        *first = default;
        first->Size = SessionMemory.PageSize - PageHeaderBytes;
        first->Page = page;
        Insert(first);
        return true;
    }

    private static int Index(nuint size)
    {
        var first = BitOperations.Log2((ulong)size);
        var second = (int)(size >> (first - 4)) - SecondLevels;
        return first * SecondLevels + second;
    }

    private Header* FindFree(nuint needed)
    {
        var index = Index(needed);
        // The lower bin contains sizes on both sides of the request. Never
        // return an undersized block just because it shares a size class.
        for (var block = heads[index]; block != null; block = block->Next)
            if (block->Size >= needed) return block;
        var first = index / SecondLevels;
        var second = index % SecondLevels;
        var mask = secondBitmap[first] & (0xffffu << (second + 1));
        if (mask != 0) return heads[first * SecondLevels + BitOperations.TrailingZeroCount(mask)];
        var higher = firstBitmap & (uint.MaxValue << (first + 1));
        if (higher == 0) return null;
        first = BitOperations.TrailingZeroCount(higher);
        return heads[first * SecondLevels + BitOperations.TrailingZeroCount(secondBitmap[first])];
    }

    private void Split(Header* block, nuint needed)
    {
        var rest = (Header*)((byte*)block + needed);
        *rest = default;
        rest->Size = block->Size - needed;
        rest->Page = block->Page;
        rest->PhysicalPrevious = block;
        rest->PhysicalNext = block->PhysicalNext;
        if (rest->PhysicalNext != null) rest->PhysicalNext->PhysicalPrevious = rest;
        block->PhysicalNext = rest;
        block->Size = needed;
        Insert(rest);
    }

    private void Insert(Header* block)
    {
        var index = Index(block->Size);
        block->Previous = null;
        block->Next = heads[index];
        if (block->Next != null) block->Next->Previous = block;
        heads[index] = block;
        firstBitmap |= 1u << (index / SecondLevels);
        secondBitmap[index / SecondLevels] |= 1u << (index % SecondLevels);
    }

    private void Remove(Header* block)
    {
        var index = Index(block->Size);
        if (block->Previous != null) block->Previous->Next = block->Next;
        else heads[index] = block->Next;
        if (block->Next != null) block->Next->Previous = block->Previous;
        if (heads[index] == null)
        {
            secondBitmap[index / SecondLevels] &= ~(1u << (index % SecondLevels));
            if (secondBitmap[index / SecondLevels] == 0) firstBitmap &= ~(1u << (index / SecondLevels));
        }
    }

    public void Trim() { /* Empty pages are returned immediately by Free. */ }
    public void ReleaseHandles()
    {
        if (handles != null) owner->Free(handles);
        handles = null;
        handleCount = handleCapacity = 0;
        freeHandle = -1;
    }
    private void Enter() { while (Interlocked.CompareExchange(ref gate, 1, 0) != 0) Thread.SpinWait(16); }
    private void Exit() => Volatile.Write(ref gate, 0);
}

internal readonly record struct BlockStatistics(ulong LiveBytes, ulong LiveBlocks, ulong PeakLiveBytes);
