using System.Diagnostics;

namespace ProjectGmKernel.Native.Runtime;

internal static class PoolConstants
{
    public const int PoolCount = 19;
}

/// <summary>
/// Snapshot of all pool states at mark creation time.
/// Uses pre-allocated array to avoid GC allocation on mark creation.
/// </summary>
internal unsafe struct MarkRecord
{
    public int SequenceNo;
    public int RollbackStamp;
    public int HandleCount;
    /// <summary>
    /// Saved allocation counts per pool. Index matches pool order:
    /// [0]=Handles, [1]=Points, [2]=Vectors, [3]=Bodies, [4]=Shells,
    /// [5]=FaceUses, [6]=Faces, [7]=Loops, [8]=Edges, [9]=Fins,
    /// [10]=Vertices, [11]=Regions, [12]=Curves, [13]=Surfaces,
    /// [14]=Transforms, [15]=CircleData, [16]=CylinderData, [17]=PlaneData,
    /// [18]=LineData
    /// </summary>
    public fixed int PoolCounts[PoolConstants.PoolCount];
}

/// <summary>
/// Partition state for concurrent access control.
/// </summary>
internal struct PartitionRecord
{
    public PartitionSlot PartitionId;
    public int LockState;     // PartitionLockState
    public int LockOwner;     // thread id of lock owner
    public int GuardState;
    public int ActiveCommandCount;
    public BodySlot FirstBody;
    public BodySlot LastBody;
    public int BodyCount;
}

internal enum PartitionLockState : int
{
    Unlocked = 0,
    SharedRead = 1,
    LocalWrite = 2,
    ExclusiveWrite = 3,
    GuardTransition = 4,
    RollbackTransition = 5,
}

/// <summary>
/// Deleted-slot tombstone for rollback restoration.
/// </summary>
internal struct Tombstone
{
    public int PoolIndex;
    public int Slot;
    public int Generation;
    public int HandleTag;
}

/// <summary>
/// Session-level state: marks, partitions, rollback counter.
/// </summary>
internal sealed class SessionState
{
    public const int MaxMarks = 64;
    public const int MaxPartitions = 256;
    public const int MaxTombstones = 4096;

    public int SessionId;
    public bool Started;
    public int NextRollbackStamp;

    // Mark stack — single mark for Phase 2 (simplified from Parasolid's nested marks)
    public bool HasMark;
    public MarkRecord CurrentMark;

    public PartitionSlot CurrentPartition;
    public int PartitionCount;
    public readonly PartitionRecord[] Partitions = new PartitionRecord[MaxPartitions];

    // Pre-allocated tombstone array — avoids List<T> allocation
    public readonly Tombstone[] Tombstones = new Tombstone[MaxTombstones];
    public int TombstoneCount;

    public SessionState(int sessionId)
    {
        SessionId = sessionId;
        ResetPartitions();
    }

    public void ResetPartitions()
    {
        Array.Clear(Partitions);
        CurrentPartition = 0;
        PartitionCount = 1;
        Partitions[0] = new PartitionRecord
        {
            PartitionId = 0,
            FirstBody = -1,
            LastBody = -1,
        };
    }

    /// <summary>
    /// Record a deleted entity as a tombstone for potential rollback restoration.
    /// </summary>
    public void AddTombstone(int poolIndex, int slot, int generation, int handleTag)
    {
        Debug.Assert(TombstoneCount < MaxTombstones);
        Tombstones[TombstoneCount] = new Tombstone
        {
            PoolIndex = poolIndex,
            Slot = slot,
            Generation = generation,
            HandleTag = handleTag,
        };
        TombstoneCount++;
    }

    /// <summary>
    /// Clear all tombstones (called after successful goto or reset).
    /// </summary>
    public void ClearTombstones()
    {
        TombstoneCount = 0;
    }
}
