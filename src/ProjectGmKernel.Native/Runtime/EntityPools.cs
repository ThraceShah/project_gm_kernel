using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Entity pool: manages a fixed-size array of entity records with generation-based
/// use-after-free detection and a free list for slot reuse.
///
/// Constraint: T must have a RecordHeader as its FIRST field (enforced by usage, not type system).
/// </summary>
internal struct EntityPool<T> where T : struct
{
    private Arena<T> _arena;
    private int[] _freeList;    // separate free list — does not repurpose Generation
    private int _freeCount;     // number of entries in free list
    private int _aliveCount;

    public EntityPool(int capacity)
    {
        _arena = new Arena<T>(capacity);
        _freeList = new int[capacity];
        _freeCount = 0;
        _aliveCount = 0;
    }

    public readonly int Capacity => _arena.Capacity;
    public readonly int AliveCount => _aliveCount;
    public readonly int AllocatedCount => _arena.Count;

    public readonly ref T this[int index] => ref _arena[index];

    /// <summary>
    /// Allocate a slot. Returns the slot index. The record's header is initialized
    /// with incremented generation and Alive=1.
    /// </summary>
    public int Allocate()
    {
        int slot;
        if (_freeCount > 0)
        {
            slot = _freeList[--_freeCount];
        }
        else
        {
            slot = _arena.Allocate();
        }

        ref var h = ref GetHeader(slot);
        var gen = h.Generation;             // save generation before zeroing
        h = default;                        // zero the entire record
        h.Generation = gen + 1;             // bump generation
        h.Alive = 1;
        _aliveCount++;
        return slot;
    }

    /// <summary>
    /// Free a slot. The generation counter is preserved so that stale handles
    /// (pointing to this slot with an old generation) will fail validation.
    /// </summary>
    public void Free(int slot)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        ref var header = ref GetHeader(slot);
        Debug.Assert(header.Alive == 1);

        header.Alive = 0;                   // mark dead
        _freeList[_freeCount++] = slot;     // push onto free list
        _aliveCount--;
    }

    /// <summary>
    /// Mark a slot dead without adding it to the free list.
    /// Used for deletes inside an active mark, where rollback may restore the slot.
    /// </summary>
    public void Retire(int slot)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        ref var header = ref GetHeader(slot);
        Debug.Assert(header.Alive == 1);

        header.Alive = 0;
        _aliveCount--;
    }

    /// <summary>
    /// Add a retired dead slot to the free list after the active mark is deleted.
    /// </summary>
    public void RecycleRetired(int slot)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        ref var header = ref GetHeader(slot);
        Debug.Assert(header.Alive == 0);

        _freeList[_freeCount++] = slot;
    }

    /// <summary>
    /// Check if a slot is alive.
    /// </summary>
    public bool IsAlive(int slot)
    {
        if ((uint)slot >= (uint)_arena.Count)
            return false;
        return GetHeader(slot).Alive != 0;
    }

    /// <summary>
    /// Validate a slot reference with generation check.
    /// </summary>
    public bool IsValid(int slot, int generation)
    {
        if ((uint)slot >= (uint)_arena.Count)
            return false;
        ref readonly var header = ref GetHeaderReadonly(slot);
        return header.Alive != 0 && header.Generation == generation;
    }

    /// <summary>
    /// Get the generation of a slot.
    /// </summary>
    public int GetGeneration(int slot)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        return GetHeader(slot).Generation;
    }

    /// <summary>
    /// Get the rollback stamp of a slot.
    /// </summary>
    public int GetRollbackStamp(int slot)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        return GetHeader(slot).RollbackStamp;
    }

    /// <summary>
    /// Set the rollback stamp of a slot.
    /// </summary>
    public void SetRollbackStamp(int slot, int stamp)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        GetHeader(slot).RollbackStamp = stamp;
    }

    /// <summary>
    /// Save the arena's allocation mark for rollback.
    /// </summary>
    public int SaveMark() => _arena.SaveMark();

    /// <summary>
    /// Restore to a saved mark. Kills all slots allocated beyond the mark.
    /// Also rebuilds the free list for the restored range.
    /// </summary>
    public void RestoreMark(int mark)
    {
        int currentCount = _arena.Count;
        if (mark >= currentCount)
            return;

        // Kill all slots beyond the mark
        for (int i = mark; i < currentCount; i++)
        {
            ref var header = ref GetHeader(i);
            if (header.Alive != 0)
            {
                header.Alive = 0;
                _aliveCount--;
            }
        }

        // Rebuild free list: remove entries that point to slots >= mark
        int writeIdx = 0;
        for (int i = 0; i < _freeCount; i++)
        {
            if (_freeList[i] < mark)
                _freeList[writeIdx++] = _freeList[i];
        }
        _freeCount = writeIdx;

        _arena.RestoreMark(mark);
    }

    /// <summary>
    /// Mark a slot as alive (used during rollback restoration).
    /// Also removes the slot from the free list if present.
    /// </summary>
    public void MarkAlive(int slot)
    {
        Debug.Assert((uint)slot < (uint)_arena.Count);
        ref var header = ref GetHeader(slot);
        if (header.Alive == 0)
        {
            header.Alive = 1;
            _aliveCount++;
            // Remove from free list
            RemoveFromFreeList(slot);
        }
    }

    /// <summary>
    /// Reset the entire pool.
    /// </summary>
    public void Reset()
    {
        _arena.Reset();
        _freeCount = 0;
        _aliveCount = 0;
    }

    /// <summary>
    /// Access the RecordHeader of a slot via unsafe reinterpret cast.
    /// Requires T's first field to be RecordHeader.
    /// </summary>
    private ref RecordHeader GetHeader(int slot)
    {
        return ref Unsafe.As<T, RecordHeader>(ref _arena[slot]);
    }

    private readonly ref readonly RecordHeader GetHeaderReadonly(int slot)
    {
        return ref Unsafe.As<T, RecordHeader>(ref Unsafe.AsRef(in _arena[slot]));
    }

    /// <summary>
    /// Remove a slot from the free list (linear scan — acceptable for rollback path).
    /// </summary>
    private void RemoveFromFreeList(int slot)
    {
        for (int i = 0; i < _freeCount; i++)
        {
            if (_freeList[i] == slot)
            {
                _freeList[i] = _freeList[--_freeCount];
                return;
            }
        }
    }
}

/// <summary>
/// Universal handle record: maps a tag (array index) to a specific entity pool slot.
/// </summary>
internal struct HandleRecord
{
    public EntityClass Class;
    public PoolKind Pool;
    public int SlotIndex;
    public int Generation;
    public int SessionId;
    public byte Alive;
}

/// <summary>
/// Entity class identifiers mapping to PK_CLASS_t values.
/// </summary>
internal enum EntityClass : int
{
    Session = 504,
    Point = 2501,
    Vector = 2502,
    Body = 1501,
    Shell = 1502,
    Face = 1503,
    Loop = 1504,
    Edge = 1505,
    Fin = 1506,
    Vertex = 1507,
    Region = 1508,
    Curve = 2002,
    Surface = 2003,
    Transform = 2500,
}

/// <summary>
/// Pool kind identifiers for HandleRecord indirection.
/// </summary>
internal enum PoolKind : byte
{
    None = 0,
    Point = 1,
    Vector = 2,
    Body = 3,
    Shell = 4,
    Face = 5,
    Loop = 6,
    Edge = 7,
    Fin = 8,
    Vertex = 9,
    Region = 10,
    Curve = 11,
    Surface = 12,
    Transform = 13,
}
