using System.Runtime.CompilerServices;
using System.Threading;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Process-monotonic tags, with storage proportional to live/rollback-kept
/// entities rather than all tags ever issued. Table mutation and lookup share
/// a short gate; neither growth nor backward-shift deletion races readers.
/// </summary>
internal unsafe struct TagMap
{
    internal struct TagRecord
    {
        internal EntityTag Tag;
        internal DataSlot Slot;
        internal EntityGeneration Generation;
        public byte Pool;
        internal KernelClassCode ClassCode;
        internal byte Alive;
        internal KernelSessionId SessionId;
    }

    private static EntityTag nextProcessTag = 1;
    private SessionMemory* owner;
    private TagRecord* records;
    private BufferCount capacity;
    private BufferCount used;
    private int gate;

    public void Attach(SessionMemory* memory) { this = default; owner = memory; }
    public readonly int NextTag => Volatile.Read(ref nextProcessTag);
    public readonly int LiveOrKeptCount => used;

    internal bool HasLiveEntities(PartitionSlot partition)
    {
        if (used == 0) return false;
        Enter();
        try
        {
            for (var i = 0; i < capacity; i++)
                if (records[i].Tag != 0 && records[i].Alive != 0
                    && KernelRuntime.PoolPartitionOf((PoolKind)records[i].Pool, records[i].Slot) == partition)
                    return true;
            return false;
        }
        finally { Exit(); }
    }

    internal BufferCount CountLiveEntities(PartitionSlot partition)
    {
        Enter();
        try
        {
            var count = 0;
            for (var i = 0; i < capacity; i++)
                if (records[i].Tag != 0 && records[i].Alive != 0
                    && KernelRuntime.PoolPartitionOf((PoolKind)records[i].Pool, records[i].Slot) == partition) count++;
            return count;
        }
        finally { Exit(); }
    }

    internal bool TryFirstLiveEntity(PartitionSlot partition, out TagRecord record)
    {
        Enter();
        try
        {
            for (var i = 0; i < capacity; i++)
                if (records[i].Tag != 0 && records[i].Alive != 0
                    && KernelRuntime.PoolPartitionOf((PoolKind)records[i].Pool, records[i].Slot) == partition)
                { record = records[i]; return true; }
            record = default;
            return false;
        }
        finally { Exit(); }
    }

    public EntityTag Publish(int classCode, byte pool, DataSlot slot, EntityGeneration generation, int sessionId)
    {
        Enter();
        try
        {
            if (nextProcessTag == int.MaxValue) return 0;
            if ((capacity == 0 || used >= capacity / 2) && !Grow()) return 0;
            var tag = nextProcessTag++;
            var index = Home(tag, capacity);
            while (records[index].Tag != 0) index = (index + 1) & (capacity - 1);
            records[index] = new TagRecord
            { Tag = tag, Slot = slot, Generation = generation, Pool = pool, ClassCode = classCode, Alive = 1, SessionId = sessionId };
            used++;
            return tag;
        }
        finally { Exit(); }
    }

    public void Revoke(EntityTag tag)
    {
        if (tag <= 0) return;
        Enter();
        var hole = Find(tag);
        if (hole >= 0)
        {
            var mask = capacity - 1;
            records[hole].Tag = 0;
            for (var index = (hole + 1) & mask; records[index].Tag != 0; index = (index + 1) & mask)
            {
                var home = Home(records[index].Tag, capacity);
                if (((index - home) & mask) >= ((index - hole) & mask))
                {
                    records[hole] = records[index];
                    records[index].Tag = 0;
                    hole = index;
                }
            }
            used--;
        }
        Exit();
    }

    public void Suspend(EntityTag tag) => SetAlive(tag, 0);
    public void Restore(EntityTag tag) => SetAlive(tag, 1);
    private void SetAlive(EntityTag tag, byte alive)
    {
        Enter();
        var index = Find(tag);
        if (index >= 0) records[index].Alive = alive;
        Exit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolve(EntityTag tag, int sessionId, out TagRecord record)
    {
        record = default;
        if (tag <= 0) return false;
        Enter();
        var index = Find(tag);
        if (index >= 0) record = records[index];
        Exit();
        return index >= 0 && record.Alive != 0 && record.SessionId == sessionId;
    }

    public void BeginSession() { }
    public void Dispose()
    {
        if (records != null) owner->Free(records);
        Attach(owner);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(EntityTag tag)
    {
        if (capacity == 0) return -1;
        var index = Home(tag, capacity);
        while (records[index].Tag != tag)
        {
            if (records[index].Tag == 0) return -1;
            index = (index + 1) & (capacity - 1);
        }
        return index;
    }

    private bool Grow()
    {
        if (capacity >= 1 << 29) return false;
        var size = capacity == 0 ? 256 : capacity * 2;
        var grown = (TagRecord*)owner->TryAllocate((nuint)size * (nuint)sizeof(TagRecord));
        if (grown == null) return false;
        new Span<TagRecord>(grown, size).Clear();
        for (var i = 0; i < capacity; i++)
        {
            if (records[i].Tag == 0) continue;
            var index = Home(records[i].Tag, size);
            while (grown[index].Tag != 0) index = (index + 1) & (size - 1);
            grown[index] = records[i];
        }
        if (records != null) owner->Free(records);
        records = grown;
        capacity = size;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Home(EntityTag tag, BufferCount size) => (int)((uint)tag * 2654435761u) & (size - 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Enter() { while (Interlocked.CompareExchange(ref gate, 1, 0) != 0) Thread.SpinWait(16); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Exit() => Volatile.Write(ref gate, 0);
}
