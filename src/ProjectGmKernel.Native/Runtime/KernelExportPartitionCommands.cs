using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

// Command structs for the partition / thread protocol exports.

internal unsafe struct PartitionCreateEmptyCommand : IKernelCommand
{
    public PartitionSlot* Partition;
    public int Execute() => KernelRuntime.PartitionCreateEmpty(Partition);
}

internal unsafe struct PartitionSetCurrentCommand : IKernelCommand
{
    public PartitionSlot Partition;
    public int Execute() => KernelRuntime.PartitionSetCurrent(Partition);
}

internal unsafe struct PartitionDeleteCommand : IKernelCommand
{
    public PartitionSlot Partition;
    public PK_PARTITION_delete_o_s* Options;
    public int Execute() => KernelRuntime.PartitionDelete(Partition, Options);
}

internal unsafe struct PartitionAskTypeCommand : IKernelCommand
{
    public PartitionSlot Partition;
    public int* PartitionType;
    public int Execute() => KernelRuntime.PartitionAskType(Partition, PartitionType);
}

internal unsafe struct ThreadLockPartitionsCommand : IKernelCommand
{
    public int Count;
    public PartitionSlot* Partitions;
    public int LockType;
    public int LockStatus;
    public PK_THREAD_lock_partitions_o_s* Options;
    public PK_THREAD_lock_partitions_r_s* Result;
    public int Execute() => KernelRuntime.ThreadLockPartitions(Count, Partitions, LockType, LockStatus, Options, Result);
}

internal unsafe struct ThreadLockPartitionsResultFreeCommand : IKernelCommand
{
    public PK_THREAD_lock_partitions_r_s* Result;
    public int Execute() => KernelRuntime.ThreadLockPartitionsResultFree(Result);
}

internal unsafe struct ThreadUnlockPartitionsCommand : IKernelCommand
{
    public PK_THREAD_unlock_partitions_o_s* Options;
    public int* NPartitions;
    public PartitionSlot** Partitions;
    public int Execute() => KernelRuntime.ThreadUnlockPartitions(Options, NPartitions, Partitions);
}

internal unsafe struct ThreadAskLockedPartitionsCommand : IKernelCommand
{
    public PK_THREAD_ask_partitions_o_s* Options;
    public int* NPartitions;
    public PartitionSlot** Partitions;
    public int Execute() => KernelRuntime.ThreadAskLockedPartitions(Options, NPartitions, Partitions);
}

internal unsafe struct ThreadSetIdCommand : IKernelCommand
{
    public int ThreadId;
    public PK_THREAD_set_id_o_s* Options;
    public PK_THREAD_set_id_r_s* Result;
    public int Execute() => KernelRuntime.ThreadSetId(ThreadId, Options, Result);
}

internal unsafe struct ThreadAskIdCommand : IKernelCommand
{
    public int* NThreadIds;
    public int* ThreadIds;
    public byte* MoreIds;
    public int Execute() => KernelRuntime.ThreadAskId(NThreadIds, ThreadIds, MoreIds);
}

internal unsafe struct ThreadChainStartCommand : IKernelCommand
{
    public int ThreadId;
    public PK_THREAD_chain_start_o_s* Options;
    public int Execute() => KernelRuntime.ThreadChainStart(ThreadId, Options);
}

internal unsafe struct ThreadChainStopCommand : IKernelCommand
{
    public PK_THREAD_chain_stop_o_s* Options;
    public int Execute() => KernelRuntime.ThreadChainStop(Options);
}

internal unsafe struct ThreadIsInChainCommand : IKernelCommand
{
    public int* ThreadId;
    public int* ChainId;
    public int* LocalLevel;
    public int Execute() => KernelRuntime.ThreadIsInChain(ThreadId, ChainId, LocalLevel);
}

internal unsafe struct ThreadIsInKernelCommand : IKernelCommand
{
    public byte* InKernel;
    public byte* InChain;
    public byte* Busy;
    public byte* AtTopLevel;
    public int Execute() => KernelRuntime.ThreadIsInKernel(InKernel, InChain, Busy, AtTopLevel);
}

internal unsafe struct ThreadRegisterMemoryCbsCommand : IKernelCommand
{
    public PK_MEMORY_frustrum_s Cbs;
    public int Execute() => KernelRuntime.ThreadRegisterMemoryCbs(Cbs);
}

internal unsafe struct ThreadAskMemoryCbsCommand : IKernelCommand
{
    public PK_MEMORY_frustrum_s* Cbs;
    public int Execute() => KernelRuntime.ThreadAskMemoryCbs(Cbs);
}
