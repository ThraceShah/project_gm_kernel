namespace ProjectGmKernel.Native.Computation;

/// <summary>
/// Typed, caller-owned storage for one computation. Does not allocate, grow, or clear memory.
/// Pass by ref when sharing its cursor. Returned slices remain valid until storage is reused.
/// </summary>
internal ref struct WorkBuffer<T> where T : unmanaged
{
    private readonly Span<T> storage;
    private BufferCount count;

    public WorkBuffer(Span<T> storage)
    {
        this.storage = storage;
        count = 0;
    }

    public readonly BufferCount Count => count;
    public readonly BufferCount Remaining => storage.Length - count;

    /// <summary>
    /// On failure the cursor is unchanged. The caller must initialize the returned slice.
    /// </summary>
    public bool TryAllocate(BufferCount length, out Span<T> slice)
    {
        if ((uint)length > (uint)Remaining)
        {
            slice = default;
            return false;
        }

        slice = storage.Slice(count, length);
        count += length;
        return true;
    }
}
