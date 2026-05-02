using System.Diagnostics;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Contiguous bump-pointer allocator for DOD entity storage.
/// Zero GC allocation on the main path — all slots are pre-allocated struct elements.
/// Supports SaveMark/RestoreMark for rollback semantics.
/// </summary>
internal struct Arena<T> where T : struct
{
    private T[] _data;
    private int _count;

    public Arena(int capacity)
    {
        _data = new T[capacity];
        _count = 0;
    }

    public readonly int Count => _count;
    public readonly int Capacity => _data.Length;
    public readonly bool IsEmpty => _count == 0;

    public readonly ref T this[int index]
    {
        get
        {
            Debug.Assert((uint)index < (uint)_count);
            return ref _data[index];
        }
    }

    public readonly Span<T> AsSpan() => _data.AsSpan(0, _count);
    public readonly Span<T> AsSpan(int start, int length) => _data.AsSpan(start, length);

    /// <summary>
    /// Allocate one slot. Returns the index of the new slot.
    /// </summary>
    public int Allocate()
    {
        var index = _count;
        if (index >= _data.Length)
            ThrowCapacityExceeded();
        _count = index + 1;
        return index;
    }

    /// <summary>
    /// Allocate multiple contiguous slots. Returns the start index.
    /// </summary>
    public int Allocate(int count)
    {
        Debug.Assert(count > 0);
        var index = _count;
        var newCount = index + count;
        if (newCount > _data.Length)
            ThrowCapacityExceeded();
        _count = newCount;
        return index;
    }

    /// <summary>
    /// Save the current allocation position for later rollback.
    /// </summary>
    public readonly int SaveMark() => _count;

    /// <summary>
    /// Roll back to a previously saved mark. Clears all slots beyond the mark.
    /// </summary>
    public void RestoreMark(int mark)
    {
        Debug.Assert(mark >= 0 && mark <= _count);
        if (mark < _count)
        {
            _data.AsSpan(mark, _count - mark).Clear();
            _count = mark;
        }
    }

    /// <summary>
    /// Clear all allocations. Does not release the backing array.
    /// </summary>
    public void Reset()
    {
        _data.AsSpan(0, _count).Clear();
        _count = 0;
    }

    private static void ThrowCapacityExceeded()
    {
        throw new InvalidOperationException($"Arena<{typeof(T).Name}> capacity exceeded");
    }
}
