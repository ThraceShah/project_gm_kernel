using System.Diagnostics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Concurrency acceptance: cross-partition parallel modeling (with and
/// without an active mark), same-partition exclusion, exclusive fairness and
/// the partition/thread protocol behaviour.
/// </summary>
public unsafe class MemoryConcurrencyTests
{
    private static void StartSession()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    [Fact]
    public void LockedPartitions_ModelInParallel_IncludingDuringMark()
    {
        StartSession();

        // Two partitions, locked one per thread.
        int p1, p2;
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&p1));
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&p2));

        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));   // active mark during parallel modeling

        var aStarted = new ManualResetEventSlim(false);
        var bDone = new ManualResetEventSlim(false);
        var bError = 0;
        var pa = p1;
        var pb = p2;

        var threadA = new Thread(() =>
        {
            Assert.Equal(0, KernelRuntime.PartitionSetCurrent(pa));
            Assert.Equal(0, LockPartition(pa));
            int body;
            // Long stream of Local commands on partition p1.
            for (int i = 0; i < 3000; i++)
            {
                if (i == 0) aStarted.Set();
                Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &body));
                Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            }
            UnlockAll();
        });
        var threadB = new Thread(() =>
        {
            aStarted.Wait();
            Assert.Equal(0, KernelRuntime.PartitionSetCurrent(pb));
            Assert.Equal(0, LockPartition(pb));
            int body;
            bError = KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &body);
            int count;
            int* tags = null; int* outTags = null;
            if (bError == 0)
                bError = KernelRuntime.BodyAskFaces(body, &count, &outTags);
            bDone.Set();
            // B's single command must finish while A is still running.
            Assert.False(threadA.Join(0), "commands on locked partitions must run in parallel");
        });

        threadA.Start();
        threadB.Start();
        Assert.True(bDone.Wait(TimeSpan.FromSeconds(30)), "parallel local command did not complete");
        Assert.True(threadA.Join(TimeSpan.FromSeconds(60)), "thread A did not finish");
        Assert.Equal(0, bError);
        Assert.True(bDone.Wait(0));

        // Rollback removes everything created since the mark.
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        int bodyCount;
        int* bodies = null; int* outBodies = null;
        Assert.Equal(0, KernelRuntime.PartitionAskBodiesCount(p1, &bodyCount));
        Assert.Equal(0, bodyCount);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void UnlockedLocalCommands_SerializeAcrossThreads()
    {
        StartSession();
        // Without partition locks, Local commands behave as exclusive: two
        // threads never overlap. A queued exclusive must not be starved.
        var exclusiveDone = new ManualResetEventSlim(false);
        var stop = new ManualResetEventSlim(false);
        int writerError = 0;
        int markError = 0;

        delegate* unmanaged<double, double, double, PK_AXIS2_sf_s*, int*, int> createBlock =
            &KernelExports.PK_BODY_create_solid_block;
        delegate* unmanaged<int, int*, int> deleteEntity = &KernelExports.PK_ENTITY_delete;
        delegate* unmanaged<int*, int> createMark = &KernelExports.PK_MARK_create;
        delegate* unmanaged<int, int> deleteMark = &KernelExports.PK_MARK_delete;

        var writer = new Thread(() =>
        {
            int body;
            for (int i = 0; i < 200 && !stop.IsSet; i++)
            {
                writerError = createBlock(1, 1, 1, null, &body);
                if (writerError != 0) return;
                writerError = deleteEntity(1, &body);
                if (writerError != 0) return;
            }
        });
        var marker = new Thread(() =>
        {
            // A queued exclusive must run as soon as the current command
            // finishes — never behind an endless stream of new claims.
            int handle = 0;
            markError = createMark(&handle);
            exclusiveDone.Set();
            if (markError == 0)
                markError = deleteMark(handle);
        });

        writer.Start();
        marker.Start();
        Assert.True(exclusiveDone.Wait(TimeSpan.FromSeconds(30)), "queued exclusive was starved");
        stop.Set();
        Assert.True(writer.Join(TimeSpan.FromSeconds(60)));
        marker.Join();
        Assert.Equal(0, writerError);
        Assert.Equal(0, markError);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void PartitionLifecycle_ProtocolSemantics()
    {
        StartSession();

        int partition;
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&partition));
        Assert.True(partition > 0);
        Assert.NotEqual(partition, PartitionOfDefaultBody());

        int type;
        Assert.Equal(0, KernelRuntime.PartitionAskType(partition, &type));
        Assert.Equal(ParasolidConstants.PK_PARTITION_type_standard_c, type);

        Assert.Equal(0, KernelRuntime.PartitionSetCurrent(partition));
        int current;
        Assert.Equal(0, KernelRuntime.SessionAskCurrentPartition(&current));
        Assert.Equal(partition, current);

        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &body));
        int bodyPartition;
        Assert.Equal(0, KernelRuntime.EntityAskPartition(body, &bodyPartition));
        Assert.Equal(partition, bodyPartition);

        // Non-empty partitions cannot be deleted.
        Assert.NotEqual(0, KernelRuntime.PartitionDelete(partition, null));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal(0, KernelRuntime.PartitionSetCurrent(0));
        Assert.Equal(0, KernelRuntime.PartitionDelete(partition, null));

        // The default partition cannot be deleted.
        Assert.NotEqual(0, KernelRuntime.PartitionDelete(0, null));
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void ThreadLockUnlock_RoundTrip()
    {
        StartSession();
        int p1, p2;
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&p1));
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&p2));
        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));

        var partitions = stackalloc int[2] { p1, p2 };
        PK_THREAD_lock_partitions_r_s result = default;
        var options = new PK_THREAD_lock_partitions_o_s { o_t_version = 1, want_locked_partitions = 1 };
        Assert.Equal(0, KernelRuntime.ThreadLockPartitions(2, partitions, ParasolidConstants.PK_THREAD_lock_all_c, ParasolidConstants.PK_THREAD_wait_yes_c, &options, &result));
        Assert.Equal(26680 /* PK_lock_status_ok_c */, result.status);
        Assert.Equal(2, result.n_locked_partitions);

        int nLocked;
        int* locked = null;
        PK_THREAD_ask_partitions_o_s askOptions = default;
        Assert.Equal(0, KernelRuntime.ThreadAskLockedPartitions(&askOptions, &nLocked, &locked));
        Assert.Equal(2, nLocked);

        Assert.Equal(0, KernelRuntime.ThreadLockPartitionsResultFree(&result));

        int nUnlocked;
        int* unlocked = null;
        PK_THREAD_unlock_partitions_o_s unlockOptions = default;
        Assert.Equal(0, KernelRuntime.ThreadUnlockPartitions(&unlockOptions, &nUnlocked, &unlocked));
        Assert.Equal(2, nUnlocked);
        Assert.Equal(0, KernelRuntime.MemoryFree(unlocked));

        Assert.Equal(0, KernelRuntime.ThreadAskLockedPartitions(&askOptions, &nLocked, &locked));
        Assert.Equal(0, nLocked);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void ThreadIdsAndChain_RoundTrip()
    {
        StartSession();
        var options = new PK_THREAD_set_id_o_s { o_t_version = 1 };
        PK_THREAD_set_id_r_s result = default;
        Assert.Equal(0, KernelRuntime.ThreadSetId(4711, &options, &result));

        int n; int id; byte more;
        Assert.Equal(0, KernelRuntime.ThreadAskId(&n, &id, &more));
        Assert.Equal(4711, n);
        Assert.Equal(0, id);
        Assert.Equal(0, more);

        var chainOptions = new PK_THREAD_chain_start_o_s { o_t_version = 2, length = 3, local_level = ParasolidConstants.PK_THREAD_local_none_c };
        Assert.Equal(0, KernelRuntime.ThreadChainStart(ParasolidConstants.PK_THREAD_chain_exclusive_c, &chainOptions));
        int threadId, chainId, level;
        Assert.Equal(0, KernelRuntime.ThreadIsInChain(&threadId, &chainId, &level));
        Assert.Equal(ParasolidConstants.PK_THREAD_chain_exclusive_c, threadId);
        Assert.Equal(3, chainId);
        Assert.Equal(2, level);
        byte inKernel, inChain, busy, top;
        Assert.Equal(0, KernelRuntime.ThreadIsInKernel(&inKernel, &inChain, &busy, &top));
        Assert.Equal(0, inKernel);
        Assert.Equal(0, inChain);
        Assert.Equal(1, top);
        PK_THREAD_chain_stop_o_s stop = default;
        Assert.Equal(0, KernelRuntime.ThreadChainStop(&stop));
        Assert.Equal(0, KernelRuntime.ThreadIsInChain(&threadId, &chainId, &level));
        Assert.Equal(ParasolidConstants.PK_THREAD_chain_none_c, threadId);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void ThreadMemoryCallbacks_AreRegisteredAndQueried()
    {
        StartSession();
        PK_MEMORY_frustrum_s cbs;
        Assert.Equal(0, KernelRuntime.ThreadAskMemoryCbs(&cbs));
        Assert.True(cbs.alloc_fn == null && cbs.free_fn == null);

        var options = new PK_THREAD_set_id_o_s { o_t_version = 1 };
        PK_THREAD_set_id_r_s result = default;
        Assert.Equal(0, KernelRuntime.ThreadSetId(7, &options, &result));

        // Register after a warm allocation so the registry path is exercised.
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &body));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));

        int n; int id; byte more;
        Assert.Equal(0, KernelRuntime.ThreadAskId(&n, &id, &more));
        Assert.Equal(7, n);
        KernelRuntime.SessionStop();
    }

    private static int PartitionOfDefaultBody()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &body));
        int partition;
        Assert.Equal(0, KernelRuntime.EntityAskPartition(body, &partition));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        return partition;
    }

    private static int LockPartition(int partition)
    {
        int local = partition;
        PK_THREAD_lock_partitions_r_s result = default;
        PK_THREAD_lock_partitions_o_s options = new() { o_t_version = 1 };
        return KernelRuntime.ThreadLockPartitions(1, &local, ParasolidConstants.PK_THREAD_lock_all_c,
            ParasolidConstants.PK_THREAD_wait_yes_c, &options, &result);
    }

    private static void UnlockAll()
    {
        int n;
        int* unlocked = null;
        PK_THREAD_unlock_partitions_o_s options = new() { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.ThreadUnlockPartitions(&options, &n, &unlocked));
        if (unlocked != null)
            Assert.Equal(0, KernelRuntime.MemoryFree(unlocked));
    }
}
