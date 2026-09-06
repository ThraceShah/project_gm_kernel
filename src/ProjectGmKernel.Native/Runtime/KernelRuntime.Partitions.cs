using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// PK partition and thread protocol subset: partition creation/selection,
/// thread chains, partition locks and per-thread memory callbacks. Partition
/// ids equal their slot index in the partition table and are never reused.
/// Lock and wait tokens come from the generated Parasolid SDK bindings.
/// </summary>
internal static unsafe partial class KernelRuntime
{
    [ThreadStatic] private static SessionData.ThreadContext* cachedThreadContext;
    [ThreadStatic] private static int cachedSessionGeneration;
    private static readonly object ThreadRegistrationGate = new();
    internal const int PK_lock_status_ok_c = 26680;
    internal const int PK_lock_status_fail_c = 26681;

    /// <summary>Find or create the calling thread's context; re-init stale generations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SessionData.ThreadContext* ThreadContext()
    {
        var session = State.Session;
        if (session == null)
            return null;
        if (cachedThreadContext != null && cachedSessionGeneration == session->SessionGeneration)
            return cachedThreadContext;
        return RegisterThreadContext(session);
    }

    private static SessionData.ThreadContext* RegisterThreadContext(SessionData* session)
    {
        lock (ThreadRegistrationGate)
        {
        var threadId = Environment.CurrentManagedThreadId;
        var slot = session->FindThread(threadId);
        if (slot < 0)
            slot = session->TryRegisterThread(threadId, session->SessionGeneration);
        if (slot < 0)
            return null;
        ref var ctx = ref session->Threads[slot];
        if (ctx.SessionGeneration != session->SessionGeneration)
        {
            // Session restarted since this context was issued: stale pointers
            // and session-scoped state must not be reused.
            ctx.SessionGeneration = session->SessionGeneration;
            ctx.ChainDepth = 0;
            ctx.ChainUserId = 0;
            ctx.CurrentPartition = 0;
            ctx.InKernel = 0;
            ctx.LockCount = 0;
        }
        cachedSessionGeneration = session->SessionGeneration;
        cachedThreadContext = (SessionData.ThreadContext*)Unsafe.AsPointer(ref ctx);
        return cachedThreadContext;
        }
    }

    // ── Partition management ────────────────────────────────────

    private static int PartitionCreateEmptyImplementation(PartitionSlot* partition)
    {
        if (partition is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var session = State.Session;
        if (!session->TryAllocatePartitionId(out var id))
            return ParasolidConstants.PK_ERROR_memory_full;
        if (!session->TryCreatePartition(id, out var slot))
            return ParasolidConstants.PK_ERROR_memory_full;
        *partition = id;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int PartitionSetCurrentImplementation(PartitionSlot partition)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var session = State.Session;
        if (!session->TryFindPartition(partition, out _))
            return ParasolidConstants.PK_ERROR_bad_value;
        if (ThreadContext() == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        ThreadContext()->CurrentPartition = partition;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int PartitionDeleteImplementation(PartitionSlot partition, PK_PARTITION_delete_o_s* options)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var session = State.Session;
        if (!session->TryFindPartition(partition, out var slot))
            return ParasolidConstants.PK_ERROR_bad_value;
        if (partition == 0)
            return ParasolidConstants.PK_ERROR_bad_value;   // default partition persists
        ref var record = ref session->Partitions[slot];
        if (record.BodyCount != 0)
            return ParasolidConstants.PK_ERROR_partition_not_empty;
        if (record.LockOwnerThread != 0)
            return ParasolidConstants.PK_ERROR_bad_value;   // locked partitions cannot be deleted
        record.Alive = 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    /// <summary>Number of bodies owned by a partition (test/inspection path).</summary>
    public static int PartitionAskBodiesCount(PartitionSlot partition, int* count)
    {
        if (count is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var session = State.Session;
        if (!session->TryFindPartition(partition, out var slot))
            return ParasolidConstants.PK_ERROR_bad_value;
        *count = session->Partitions[slot].BodyCount;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int PartitionAskTypeImplementation(PartitionSlot partition, int* partitionType)
    {
        if (partitionType is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        if (!State.Session->TryFindPartition(partition, out _))
            return ParasolidConstants.PK_ERROR_bad_value;
        *partitionType = ParasolidConstants.PK_PARTITION_type_standard_c;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static SessionData.ThreadContext* ThreadContextRef()
        => ThreadContext();

    // ── Partition locks (short critical section on the table) ──

    /// <summary>
    /// Lock all requested partitions, or one available partition, using the
    /// SDK lock_all/lock_one and wait_yes/wait_no options.
    /// </summary>
    private static int ThreadLockPartitionsImplementation(int nPartitions, PartitionSlot* partitions,
        int lockType, int waitType, PK_THREAD_lock_partitions_o_s* options,
        PK_THREAD_lock_partitions_r_s* result)
    {
        if (result == null || partitions == null || nPartitions <= 0)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        *result = default;
        result->status = ParasolidConstants.PK_lock_status_fail_c;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (lockType != ParasolidConstants.PK_THREAD_lock_all_c && lockType != ParasolidConstants.PK_THREAD_lock_one_c)
            return ParasolidConstants.PK_ERROR_bad_value;
        if (waitType != ParasolidConstants.PK_THREAD_wait_yes_c && waitType != ParasolidConstants.PK_THREAD_wait_no_c)
            return ParasolidConstants.PK_ERROR_bad_value;
        if (nPartitions > SessionData.MaxLockedPartitions || (options != null && options->o_t_version != 1))
            return ParasolidConstants.PK_ERROR_bad_value;

        var session = State.Session;
        var generation = session->SessionGeneration;
        var context = ThreadContext();
        if (context == null) return ParasolidConstants.PK_ERROR_memory_full;
        Span<int> selected = stackalloc int[SessionData.MaxLockedPartitions];
        Span<int> unavailable = stackalloc int[SessionData.MaxLockedPartitions];
        lock (SessionDispatchGate)
        {
            while (true)
            {
                var selectedCount = 0;
                var unavailableCount = 0;
                var newLocks = 0;
                for (var i = 0; i < nPartitions; i++)
                {
                    if (!session->TryFindPartition(partitions[i], out var slot)) return ParasolidConstants.PK_ERROR_bad_value;
                    for (var j = 0; j < i; j++)
                        if (partitions[i] == partitions[j]) return ParasolidConstants.PK_ERROR_bad_value;
                    var owner = session->Partitions[slot].LockOwnerThread;
                    if (owner != 0 && owner != context->ManagedThreadId)
                    { unavailable[unavailableCount++] = partitions[i]; continue; }
                    if (lockType == ParasolidConstants.PK_THREAD_lock_all_c || selectedCount == 0)
                    {
                        selected[selectedCount++] = partitions[i];
                        if (owner == 0) newLocks++;
                    }
                }
                var granted = lockType == ParasolidConstants.PK_THREAD_lock_all_c ? unavailableCount == 0 : selectedCount != 0;
                if (!granted && waitType == ParasolidConstants.PK_THREAD_wait_yes_c)
                {
                    Dispatcher.WaitForPartitionUnlock();
                    if (State.Session == null || State.Session->SessionGeneration != generation)
                        return ParasolidConstants.PK_ERROR_not_in_PK;
                    continue;
                }
                if (!granted) selectedCount = 0;
                if (context->LockCount + newLocks > SessionData.MaxLockedPartitions)
                    return ParasolidConstants.PK_ERROR_memory_full;
                int* locked = null;
                int* missing = null;
                if (options != null && options->want_locked_partitions != 0 && selectedCount != 0)
                {
                    locked = (int*)session->Returns.TryAllocate((nuint)selectedCount * sizeof(int));
                    if (locked == null) return ParasolidConstants.PK_ERROR_memory_full;
                    selected[..selectedCount].CopyTo(new Span<int>(locked, selectedCount));
                }
                if (options != null && options->want_unavailable_partitions != 0 && unavailableCount != 0)
                {
                    missing = (int*)session->Returns.TryAllocate((nuint)unavailableCount * sizeof(int));
                    if (missing == null)
                    { session->Returns.TryFree(locked); return ParasolidConstants.PK_ERROR_memory_full; }
                    unavailable[..unavailableCount].CopyTo(new Span<int>(missing, unavailableCount));
                }
                // No allocation/failure points remain after ownership publication.
                for (var i = 0; i < selectedCount; i++)
                {
                    ref var record = ref session->Partitions[selected[i]];
                    if (record.LockOwnerThread == context->ManagedThreadId) continue;
                    record.LockOwnerThread = context->ManagedThreadId;
                    record.LockState = SessionData.PartitionLockState.ExclusiveWrite;
                    context->LockedPartitions[context->LockCount++] = selected[i];
                }
                result->status = granted ? ParasolidConstants.PK_lock_status_ok_c : ParasolidConstants.PK_lock_status_fail_c;
                result->n_locked_partitions = selectedCount;
                result->locked_partitions = locked;
                result->n_unavailable_partitions = unavailableCount;
                result->unavailable_partitions = missing;
                return ParasolidConstants.PK_ERROR_no_errors;
            }
        }
    }
    private static int ThreadLockPartitionsResultFreeImplementation(PK_THREAD_lock_partitions_r_s* result)
    {
        if (result is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        var session = State.Session;
        if (session != null)
        {
            if (result->locked_partitions is not null)
                session->Returns.TryFree(result->locked_partitions);
            if (result->unavailable_partitions is not null)
                session->Returns.TryFree(result->unavailable_partitions);
        }
        result->locked_partitions = null;
        result->unavailable_partitions = null;
        result->n_locked_partitions = 0;
        result->n_unavailable_partitions = 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    /// <summary>Unlock everything locked to the calling thread and list it.</summary>
    private static int ThreadUnlockPartitionsImplementation(PK_THREAD_unlock_partitions_o_s* options,
        int* nPartitions, PartitionSlot** partitions)
    {
        if (nPartitions is null || partitions is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var session = State.Session;
        var threadId = Environment.CurrentManagedThreadId;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        *nPartitions = ctx->LockCount;
        *partitions = null;
        if (ctx->LockCount > 0)
        {
            var list = (PartitionSlot*)session->Returns.TryAllocate((nuint)(ctx->LockCount * sizeof(PartitionSlot)));
            if (list is null)
                return ParasolidConstants.PK_ERROR_memory_full;
            for (int p = 0; p < ctx->LockCount; p++)
            {
                list[p] = ctx->LockedPartitions[p];
                if (session->TryFindPartition(ctx->LockedPartitions[p], out var slot))
                {
                    session->Partitions[slot].LockOwnerThread = 0;
                    session->Partitions[slot].LockState = SessionData.PartitionLockState.Unlocked;
                }
            }
            *partitions = list;
        }
        ctx->LockCount = 0;
        lock (SessionDispatchGate) Monitor.PulseAll(SessionDispatchGate);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadAskLockedPartitionsImplementation(PK_THREAD_ask_partitions_o_s* options,
        int* nPartitions, PartitionSlot** partitions)
    {
        if (nPartitions is null || partitions is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        var session = State.Session;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;

        *nPartitions = ctx->LockCount;
        *partitions = null;
        if (ctx->LockCount > 0)
        {
            var list = (PartitionSlot*)session->Returns.TryAllocate((nuint)(ctx->LockCount * sizeof(PartitionSlot)));
            if (list is null)
                return ParasolidConstants.PK_ERROR_memory_full;
            for (int p = 0; p < ctx->LockCount; p++)
                list[p] = ctx->LockedPartitions[p];
            *partitions = list;
        }
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── Thread chains and ids ───────────────────────────────────

    private static int ThreadSetIdImplementation(int threadId, PK_THREAD_set_id_o_s* options, PK_THREAD_set_id_r_s* result)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        ctx->ChainUserId = threadId;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadAskIdImplementation(int* nThreadIds, int* threadIds, byte* moreIds)
    {
        if (nThreadIds is null || threadIds is null || moreIds is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        *nThreadIds = 1;
        *threadIds = ctx->ChainUserId;
        *moreIds = 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadChainStartImplementation(int threadId, PK_THREAD_chain_start_o_s* options)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        ctx->ChainUserId = threadId;
        ctx->ChainDepth = options is null ? 1 : Math.Max(1, options->length);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadChainStopImplementation(PK_THREAD_chain_stop_o_s* options)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        ctx->ChainDepth = 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadIsInChainImplementation(int* threadId, int* chainId, int* localLevel)
    {
        if (threadId is null || chainId is null || localLevel is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null || ctx->ChainDepth == 0)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        *threadId = ctx->ChainUserId;
        *chainId = ctx->ChainUserId;
        *localLevel = ctx->ChainDepth;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadIsInKernelImplementation(byte* inKernel, byte* inChain, byte* busy, byte* atTopLevel)
    {
        if (inKernel is null || inChain is null || busy is null || atTopLevel is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        *inKernel = 1;
        *inChain = ctx->ChainDepth != 0 ? (byte)1 : (byte)0;
        *busy = ctx->InKernel != 0 ? (byte)1 : (byte)0;
        *atTopLevel = ctx->ChainDepth <= 1 ? (byte)1 : (byte)0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    // ── Per-thread memory callbacks ─────────────────────────────

    private static int ThreadRegisterMemoryCbsImplementation(PK_MEMORY_frustrum_s cbs)
    {
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        ctx->AllocFn = cbs.alloc_fn;
        ctx->FreeFn = cbs.free_fn;
        ctx->HasMemoryCbs = cbs.alloc_fn != null && cbs.free_fn != null ? 1 : 0;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static int ThreadAskMemoryCbsImplementation(PK_MEMORY_frustrum_s* cbs)
    {
        if (cbs is null)
            return ParasolidConstants.PK_ERROR_bad_field_number;
        if (!IsSessionStarted)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        var ctx = ThreadContextRef();
        if (ctx == null)
            return ParasolidConstants.PK_ERROR_not_in_PK;
        if (ctx->HasMemoryCbs == 0)
            return ParasolidConstants.PK_ERROR_bad_value;
        cbs->alloc_fn = ctx->AllocFn;
        cbs->free_fn = ctx->FreeFn;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    /// <summary>Test/diagnostic helper: lock one partition to the calling thread.</summary>
    public static void ThreadLockOne(PartitionSlot partition)
    {
        var session = State.Session;
        var threadId = Environment.CurrentManagedThreadId;
        var ctx = ThreadContextRef();
        if (session->TryFindPartition(partition, out var slot))
        {
            session->Partitions[slot].LockOwnerThread = threadId;
            session->Partitions[slot].LockState = SessionData.PartitionLockState.ExclusiveWrite;
        }
        if (ctx != null && ctx->LockCount < SessionData.MaxLockedPartitions)
            ctx->LockedPartitions[ctx->LockCount++] = partition;
    }

    /// <summary>Gate shared by the scheduler and partition-lock mutations.</summary>
    internal static readonly object SessionDispatchGate = new();
}
