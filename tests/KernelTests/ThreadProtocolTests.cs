using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public unsafe class ThreadProtocolTests : IDisposable
{
    [Fact]
    public void LockSetsGrowAndAllocationFailureDoesNotPublishOwnership()
    {
        int* partitions = stackalloc int[32];
        for (var i = 0; i < 32; i++) Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(partitions + i));
        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        var options = new PK_THREAD_lock_partitions_o_s { o_t_version = 1, want_locked_partitions = 1 };
        PK_THREAD_lock_partitions_r_s result;
        KernelRuntime.InjectAllocationFailure(0);
        try
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, KernelRuntime.ThreadLockPartitions(32, partitions,
                ParasolidConstants.PK_THREAD_lock_all_c, ParasolidConstants.PK_THREAD_wait_no_c, &options, &result));
        }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        Assert.Equal(0, KernelRuntime.ThreadContext()->LockCount);
        for (var i = 0; i < 32; i++) Assert.Equal(0, KernelRuntime.State.Session->Partitions[partitions[i]].LockOwnerThread);
        Assert.Equal(0, KernelRuntime.ThreadLockPartitions(32, partitions, ParasolidConstants.PK_THREAD_lock_all_c,
            ParasolidConstants.PK_THREAD_wait_no_c, &options, &result));
        Assert.Equal(32, result.n_locked_partitions);
        Assert.Equal(32, KernelRuntime.ThreadContext()->LockCount);
        for (var i = 0; i < 32; i++) Assert.Equal(partitions[i], result.locked_partitions[i]);
        Assert.Equal(0, KernelRuntime.ThreadLockPartitionsResultFree(&result));
        int count;
        int* unlocked;
        Assert.Equal(0, KernelRuntime.ThreadUnlockPartitions(null, &count, &unlocked));
        Assert.Equal(32, count);
        Assert.Equal(0, KernelRuntime.MemoryFree(unlocked));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    public void SessionStopWakesPartitionWaitersWithoutAccessingDisposedState(int count)
    {
        int* partitions = stackalloc int[16];
        for (var i = 0; i < count; i++) Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(partitions + i));
        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        PK_THREAD_lock_partitions_r_s locked;
        Assert.Equal(0, KernelRuntime.ThreadLockPartitions(count, partitions, ParasolidConstants.PK_THREAD_lock_all_c,
            ParasolidConstants.PK_THREAD_wait_no_c, null, &locked));
        using var entered = new ManualResetEventSlim();
        var captured = partitions;
        var error = 0;
        var worker = new Thread(() =>
        {
            entered.Set();
            PK_THREAD_lock_partitions_r_s result;
            error = KernelRuntime.ThreadLockPartitions(count, captured, ParasolidConstants.PK_THREAD_lock_all_c,
                ParasolidConstants.PK_THREAD_wait_yes_c, null, &result);
        });
        worker.Start();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        Thread.Sleep(50);
        Assert.Equal(0, KernelRuntime.SessionStop());
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        Assert.Equal(ParasolidConstants.PK_ERROR_not_in_PK, error);
    }

    public ThreadProtocolTests()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose()
    {
        KernelRuntime.ThreadChainStop(null);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void FunctionClassificationReflectsChainAndPartitionProtection()
    {
        int* functions = stackalloc int[3];
        int* values = stackalloc int[3];
        byte** names = stackalloc byte*[3];
        fixed (byte* point = "PK_POINT_create\0"u8)
        fixed (byte* evaluate = "PK_CURVE_eval\0"u8)
        fixed (byte* query = "PK_FUNCTION_find\0"u8)
        {
            names[0] = point; names[1] = evaluate; names[2] = query;
            Assert.Equal(0, KernelRuntime.FunctionFind(3, names, null, functions));
        }
        Assert.Equal(0, KernelRuntime.ThreadAskFunctionRun(3, functions, null, values));
        Assert.Equal(ParasolidConstants.PK_FUNCTION_run_exclusive_c, values[0]);
        Assert.Equal(ParasolidConstants.PK_FUNCTION_run_concurrent_c, values[1]);
        var chain = new PK_THREAD_chain_start_o_s
        { o_t_version = 2, length = 0, local_level = ParasolidConstants.PK_THREAD_local_none_c };
        Assert.Equal(0, KernelRuntime.ThreadChainStart(ParasolidConstants.PK_THREAD_chain_exclusive_c, &chain));
        Assert.Equal(0, KernelRuntime.ThreadAskFunctionRun(3, functions, null, values));
        Assert.Equal(ParasolidConstants.PK_FUNCTION_run_exclusive_c, values[1]);
        Assert.Equal(ParasolidConstants.PK_FUNCTION_run_concurrent_c, values[2]);
        Assert.Equal(0, KernelRuntime.ThreadChainStop(null));
        int mark, partition = 0;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        PK_THREAD_lock_partitions_r_s locked;
        Assert.Equal(0, KernelRuntime.ThreadLockPartitions(1, &partition, ParasolidConstants.PK_THREAD_lock_all_c,
            ParasolidConstants.PK_THREAD_wait_no_c, null, &locked));
        Assert.Equal(0, KernelRuntime.ThreadAskFunctionRun(3, functions, null, values));
        Assert.Equal(ParasolidConstants.PK_FUNCTION_run_concurrent_c, values[0]);
        functions[2] = -1;
        values[0] = 999;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_value, KernelRuntime.ThreadAskFunctionRun(3, functions, null, values));
        Assert.Equal(999, values[0]);
    }

    [Fact]
    public void ModifiedPartitionMustReachAnotherCheckpointBeforeRelocking()
    {
        int partition, mark, point;
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&partition));
        PK_THREAD_lock_partitions_r_s result;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_at_pmark,
            KernelRuntime.ThreadLockPartitions(1, &partition, ParasolidConstants.PK_THREAD_lock_all_c,
                ParasolidConstants.PK_THREAD_wait_no_c, null, &result));
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.PartitionSetCurrent(partition));
        var data = new PK_POINT_sf_s();
        Assert.Equal(0, KernelRuntime.PointCreate(&data, &point));
        Assert.Equal(ParasolidConstants.PK_ERROR_not_at_pmark,
            KernelRuntime.ThreadLockPartitions(1, &partition, ParasolidConstants.PK_THREAD_lock_all_c,
                ParasolidConstants.PK_THREAD_wait_no_c, null, &result));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(0, KernelRuntime.ThreadLockPartitions(1, &partition, ParasolidConstants.PK_THREAD_lock_all_c,
            ParasolidConstants.PK_THREAD_wait_no_c, null, &result));
    }
}
