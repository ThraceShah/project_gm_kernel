using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KernelTests;

public unsafe class MemoryReviewTests
{
    private static SessionMemory* callbackMemory;
    private static int callbackAllocations, callbackFrees, callbackReentryError;
    private static bool reenterOnFree;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint CallbackAllocate(nuint bytes)
    {
        callbackAllocations++;
        return (nint)callbackMemory->TryAllocate(bytes);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CallbackFree(nint pointer)
    {
        callbackFrees++;
        callbackMemory->Free((void*)pointer);
        if (!reenterOnFree) return;
        reenterOnFree = false;
        KernelRuntime.InjectAllocationFailure(-1);
        callbackReentryError |= KernelRuntime.ThreadRegisterMemoryCbs(default);
        var temporary = KernelRuntime.State.Session->Returns.TryAllocate(8);
        if (temporary == null) callbackReentryError = -1;
        else if (!KernelRuntime.State.Session->Returns.TryFree(temporary)) callbackReentryError = -1;
    }

    [Fact]
    public void ThreadReturnCallbacksAreUsedAndFailureFreeCanReenter()
    {
        Start();
        SessionMemory foreign = default;
        callbackMemory = &foreign;
        callbackAllocations = callbackFrees = callbackReentryError = 0;
        reenterOnFree = false;
        try
        {
            int body, count;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            var callbacks = new PK_MEMORY_frustrum_s { alloc_fn = &CallbackAllocate, free_fn = &CallbackFree };
            Assert.Equal(0, KernelRuntime.ThreadRegisterMemoryCbs(callbacks));
            // Callback allocation succeeds but the kernel's ownership table
            // allocation fails. Reentrant cleanup must run outside its gate.
            KernelRuntime.InjectAllocationFailure(0);
            reenterOnFree = true;
            int* faces = null;
            Assert.NotEqual(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
            Assert.Equal(1, callbackAllocations);
            Assert.Equal(1, callbackFrees);
            Assert.Equal(0, callbackReentryError);
            Assert.Equal(0UL, KernelRuntime.ReturnOutstandingBlocks);
            Assert.Equal(0UL, foreign.Statistics.LiveBytes);

            Assert.Equal(0, KernelRuntime.ThreadRegisterMemoryCbs(callbacks));
            Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
            Assert.Equal(2, callbackAllocations);
            Assert.Equal(0, KernelRuntime.MemoryFree(faces));
            Assert.Equal(2, callbackFrees);
        }
        finally
        {
            KernelRuntime.InjectAllocationFailure(-1);
            KernelRuntime.SessionStop();
            foreign.Dispose();
            callbackMemory = null;
        }
    }
    private struct ConcurrentPointCommand : IKernelCommand
    {
        internal Barrier Barrier;
        internal bool Fail;
        public int Execute()
        {
            if (!Barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new InvalidOperationException("two partition write claims did not overlap");
            var definition = new PK_POINT_sf_s();
            for (var i = 0; i < 4000; i++)
            {
                int point;
                var error = KernelRuntime.PointCreate(&definition, &point);
                if (error != 0) return error;
            }
            return Fail ? ParasolidConstants.PK_ERROR_bad_parameter : 0;
        }
    }

    [Fact]
    public void ConcurrentClaimsGrowIndependentLogsAndFailureCannotCancelPeer()
    {
        Start();
        try
        {
            int first, second, mark;
            Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&first));
            Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&second));
            Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
            using var barrier = new Barrier(2);
            Exception? aFailure = null, bFailure = null;
            var aPartition = first;
            var bPartition = second;
            var a = new Thread(() => { try { Run(aPartition, true); } catch (Exception error) { aFailure = error; } });
            var b = new Thread(() => { try { Run(bPartition, false); } catch (Exception error) { bFailure = error; } });
            a.Start(); b.Start();
            Assert.True(a.Join(TimeSpan.FromSeconds(20)));
            Assert.True(b.Join(TimeSpan.FromSeconds(20)));
            Assert.Null(aFailure); Assert.Null(bFailure);
            Assert.Equal(4000, KernelRuntime.Points.AliveCount);
            Assert.Equal(4000, KernelRuntime.State.Session->Tags.LiveOrKeptCount);
            Assert.Equal(0, KernelRuntime.MarkGoto(mark));
            Assert.Equal(0, KernelRuntime.Points.AliveCount);
            Assert.Equal(0, KernelRuntime.State.Session->Tags.LiveOrKeptCount);

            void Run(int partition, bool fail)
            {
                Assert.Equal(0, KernelRuntime.PartitionSetCurrent(partition));
                var options = new PK_THREAD_lock_partitions_o_s { o_t_version = 1 };
                PK_THREAD_lock_partitions_r_s result;
                Assert.Equal(0, KernelRuntime.ThreadLockPartitions(1, &partition,
                    ParasolidConstants.PK_THREAD_lock_all_c, ParasolidConstants.PK_THREAD_wait_yes_c, &options, &result));
                var command = new ConcurrentPointCommand { Barrier = barrier, Fail = fail };
                var error = KernelRuntime.Dispatch(ApiId.PointCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
                Assert.Equal(fail ? ParasolidConstants.PK_ERROR_bad_parameter : 0, error);
            }
        }
        finally { KernelRuntime.SessionStop(); }
    }

    [Fact]
    public void WaitingPartitionLockLetsTheOwnerUnlock()
    {
        Start();
        try
        {
            int partition;
            Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&partition));
            using var locked = new ManualResetEventSlim();
            using var attempted = new ManualResetEventSlim();
            Exception? ownerFailure = null, waiterFailure = null;
            var p = partition;
            var owner = new Thread(() =>
            {
                try
                {
                    Lock(p);
                    locked.Set();
                    Assert.True(attempted.Wait(TimeSpan.FromSeconds(10)));
                    Thread.Sleep(50); // give the waiter time to enter its wait path
                    Unlock();
                }
                catch (Exception error) { ownerFailure = error; }
            });
            var waiter = new Thread(() =>
            {
                try
                {
                    Assert.True(locked.Wait(TimeSpan.FromSeconds(10)));
                    attempted.Set();
                    Lock(p);
                    Unlock();
                }
                catch (Exception error) { waiterFailure = error; }
            });
            owner.Start(); waiter.Start();
            Assert.True(owner.Join(TimeSpan.FromSeconds(15)));
            Assert.True(waiter.Join(TimeSpan.FromSeconds(15)));
            Assert.Null(ownerFailure); Assert.Null(waiterFailure);

            static void Lock(int part)
            {
                var options = new PK_THREAD_lock_partitions_o_s { o_t_version = 1 };
                PK_THREAD_lock_partitions_r_s result;
                Assert.Equal(0, KernelRuntime.ThreadLockPartitions(1, &part, ParasolidConstants.PK_THREAD_lock_all_c,
                    ParasolidConstants.PK_THREAD_wait_yes_c, &options, &result));
                Assert.Equal(ParasolidConstants.PK_lock_status_ok_c, result.status);
            }
            static void Unlock()
            {
                int count; int* parts;
                Assert.Equal(0, KernelRuntime.ThreadUnlockPartitions(null, &count, &parts));
                Assert.Equal(0, KernelRuntime.MemoryFree(parts));
            }
        }
        finally { KernelRuntime.SessionStop(); }
    }
    private static int LiveRecords => KernelRuntime.Points.AliveCount + KernelRuntime.Vectors.AliveCount
        + KernelRuntime.Bodies.AliveCount + KernelRuntime.Shells.AliveCount + KernelRuntime.FaceUses.AliveCount
        + KernelRuntime.Faces.AliveCount + KernelRuntime.Loops.AliveCount + KernelRuntime.Edges.AliveCount
        + KernelRuntime.Fins.AliveCount + KernelRuntime.Vertices.AliveCount + KernelRuntime.Regions.AliveCount
        + KernelRuntime.Curves.AliveCount + KernelRuntime.Surfaces.AliveCount + KernelRuntime.Transforms.AliveCount
        + KernelRuntime.CircleDataPool.AliveCount + KernelRuntime.LineDataPool.AliveCount
        + KernelRuntime.CylinderDataPool.AliveCount + KernelRuntime.PlaneDataPool.AliveCount
        + KernelRuntime.ConeDataPool.AliveCount + KernelRuntime.SphereDataPool.AliveCount
        + KernelRuntime.TorusDataPool.AliveCount + KernelRuntime.BCurveDataStore.AliveCount;

    [Fact]
    public void DeleteUnderMarkReservesItsJournalBeforeChangingTheBody()
    {
        for (var fault = 0; fault < 5; fault++)
        {
            Start();
            try
            {
                int body, mark;
                Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
                var alive = LiveRecords;
                Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
                KernelRuntime.InjectAllocationFailure(fault);
                var error = KernelRuntime.EntityDelete(1, &body);
                KernelRuntime.InjectAllocationFailure(-1);
                if (error != 0)
                {
                    Assert.True(KernelRuntime.IsValidTag(body));
                    Assert.Equal(alive, LiveRecords);
                }
                Assert.Equal(0, KernelRuntime.MarkGoto(mark));
                Assert.True(KernelRuntime.IsValidTag(body));
                Assert.Equal(alive, LiveRecords);
            }
            finally { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }
        }
    }

    [Fact]
    public void EveryColdBlockAllocationFailureRollsBackAllRecordPools()
    {
        var reachedSuccess = false;
        for (var fault = 0; fault < 128; fault++)
        {
            Start();
            try
            {
                int body = 0;
                KernelRuntime.InjectAllocationFailure(fault);
                var error = KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body);
                KernelRuntime.InjectAllocationFailure(-1);
                if (error == 0)
                {
                    Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
                    reachedSuccess = true;
                }
                Assert.Equal(0, LiveRecords);
                Assert.Equal(0, KernelRuntime.State.Session->Tags.LiveOrKeptCount);
                int bodies;
                Assert.Equal(0, KernelRuntime.PartitionAskBodiesCount(0, &bodies));
                Assert.Equal(0, bodies);
                if (reachedSuccess) break;
            }
            finally { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }
        }
        Assert.True(reachedSuccess, "the sweep must include a successful command after all allocation failure points");
    }

    [Fact]
    public void AtomicPointFastPathReleasesSlotWhenPublicationFails()
    {
        for (var fault = 0; fault < 6; fault++)
        {
            Start();
            try
            {
                var definition = new PK_POINT_sf_s();
                int point = 0;
                KernelRuntime.InjectAllocationFailure(fault);
                var error = KernelRuntime.PointCreate(&definition, &point);
                KernelRuntime.InjectAllocationFailure(-1);
                if (error == 0) Assert.Equal(0, KernelRuntime.EntityDelete(1, &point));
                Assert.Equal(0, KernelRuntime.Points.AliveCount);
                Assert.Equal(0, KernelRuntime.State.Session->Tags.LiveOrKeptCount);
            }
            finally { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }
        }
    }

    [Fact]
    public void TopologyWiringAllocationFailuresAreNotReportedAsSuccess()
    {
        int* classes = stackalloc int[2] { ParasolidConstants.PK_CLASS_shell, ParasolidConstants.PK_CLASS_face };
        int* parents = stackalloc int[2] { -1, 0 };
        int* children = stackalloc int[2] { 0, 1 };
        int* senses = stackalloc int[2] { ParasolidConstants.PK_TOPOL_sense_none_c, ParasolidConstants.PK_TOPOL_sense_positive_c };
        for (var fault = 0; fault < 24; fault++)
        {
            Start();
            try
            {
                PK_BODY_create_topology_2_r_s result = default;
                KernelRuntime.InjectAllocationFailure(fault);
                var error = KernelRuntime.BodyCreateTopology2(2, classes, 2, parents, children, senses, null, &result);
                KernelRuntime.InjectAllocationFailure(-1);
                if (error == 0)
                {
                    Assert.Equal(1, KernelRuntime.FaceUses.AliveCount);
                    Assert.Equal(0, KernelRuntime.EntityDelete(1, &result.body));
                }
                Assert.Equal(0, LiveRecords);
                Assert.Equal(0, KernelRuntime.State.Session->Tags.LiveOrKeptCount);
            }
            finally { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }
        }
    }

    [Fact]
    public void MarkGotoReleasesAllTopologyAndGeometrySlots()
    {
        Start();
        try
        {
            int mark, body;
            Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            Assert.Equal(0, KernelRuntime.MarkGoto(mark));
            Assert.Equal(0, LiveRecords);
            Assert.Equal(0, KernelRuntime.State.Session->Tags.LiveOrKeptCount);
        }
        finally { KernelRuntime.SessionStop(); }
    }

    [Fact]
    public void AdjacentFreeBlocksCoalesceAndEmptyPagesAreReturned()
    {
        SessionMemory memory = default;
        BlockAllocator blocks = default;
        blocks.Attach(&memory);
        try
        {
            var a = blocks.TryAllocate(12_000);
            var b = blocks.TryAllocate(12_000);
            var guard = blocks.TryAllocate(30_000);
            var allocations = memory.Statistics.AllocationCount;
            blocks.Free(a);
            blocks.Free(b);
            var merged = blocks.TryAllocate(23_000);
            Assert.True(merged != null);
            Assert.Equal(allocations, memory.Statistics.AllocationCount);
            Assert.Equal((nuint)0, (nuint)merged % 16);
            blocks.Free(merged);
            blocks.Free(guard);
            Assert.Equal(0UL, blocks.LiveBytes);
            Assert.Equal((ulong)SessionMemory.PageSize, memory.Statistics.CachedBytes);
        }
        finally { memory.Dispose(); }
    }
    private static void Start()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    private struct FailingPointCommand : IKernelCommand
    {
        public int* Tag;
        public int Execute()
        {
            var definition = new PK_POINT_sf_s();
            var error = KernelRuntime.PointCreate(&definition, Tag);
            return error != 0 ? error : ParasolidConstants.PK_ERROR_bad_parameter;
        }
    }

    [Fact]
    public void FailedCommandRevokesItsCreatedEntity()
    {
        Start();
        try
        {
            int tag = 0;
            var command = new FailingPointCommand { Tag = &tag };
            Assert.NotEqual(0, KernelRuntime.Dispatch(ApiId.PointCreate, ConcurrencyKind.Local,
                AccessKind.GlobalWrite, ref command));
            Assert.False(KernelRuntime.IsValidTag(tag));
            Assert.Equal(0, KernelRuntime.Points.AliveCount);
        }
        finally { KernelRuntime.SessionStop(); }
    }

    [Fact]
    public void TagsFromStoppedSessionNeverResolveInNewSession()
    {
        Start();
        try
        {
            var definition = new PK_POINT_sf_s();
            int oldTag, newTag;
            Assert.Equal(0, KernelRuntime.PointCreate(&definition, &oldTag));
            Start();
            Assert.Equal(0, KernelRuntime.PointCreate(&definition, &newTag));
            Assert.True(newTag > oldTag);
            Assert.False(KernelRuntime.IsValidTag(oldTag));
        }
        finally { KernelRuntime.SessionStop(); }
    }

    [Fact]
    public void LargePayloadContentsCannotCorruptAllocatorAccounting()
    {
        SessionMemory memory = default;
        BlockAllocator blocks = default;
        blocks.Attach(&memory);
        try
        {
            var payload = blocks.TryAllocate(40_000);
            Assert.True(payload != null);
            new Span<byte>(payload, 40_000).Fill(0xA5);
            blocks.Free(payload);
            Assert.Equal(0UL, blocks.LiveBytes);
            Assert.Equal(0UL, blocks.Statistics.LiveBlocks);
        }
        finally { memory.Dispose(); }
    }

    [Fact]
    public void FreedBlockHandleStopsResolving()
    {
        SessionMemory memory = default;
        BlockAllocator blocks = default;
        blocks.Attach(&memory);
        try
        {
            var payload = blocks.TryAllocate(128);
            var handle = blocks.HandleOf(payload);
            Assert.True(handle > 0);
            blocks.Free(payload);
            Assert.True(blocks.BlockPointer(handle) == null);
        }
        finally { memory.Dispose(); }
    }

    [Fact]
    public void FreeListDoesNotOverwriteLivenessMetadata()
    {
        SessionMemory memory = default;
        PagedEntityPool<PointRecord> pool = default;
        pool.Attach(&memory);
        try
        {
            Assert.True(pool.TryAllocate(out var a));
            Assert.True(pool.TryAllocate(out var b));
            pool.Free(a);
            pool.Free(b);
            Assert.False(pool.IsAlive(a));
            Assert.False(pool.IsAlive(b));
            Assert.True(pool.TryAllocate(out var reused));
            Assert.Equal(b, reused);
            Assert.Equal(2, pool.GetGeneration(reused));
        }
        finally { pool.Dispose(); memory.Dispose(); }
    }

    [Fact]
    public void MarkRestoresDeletedBodyToItsPartition()
    {
        Start();
        try
        {
            int body, mark, count;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            KernelRuntime.TrimAllocator(); // retired records are still owned by the mark
            Assert.Equal(0, KernelRuntime.MarkGoto(mark));
            Assert.True(KernelRuntime.IsValidTag(body));
            Assert.Equal(0, KernelRuntime.PartitionAskBodiesCount(0, &count));
            Assert.Equal(1, count);
        }
        finally { KernelRuntime.SessionStop(); }
    }

    [Fact]
    public void TrimReleasesEmptyEntityStorageWithoutReusingOldTags()
    {
        Start();
        try
        {
            int body, replacement;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            var capacity = KernelRuntime.AllocatorStatistics.CapacityBytes;
            KernelRuntime.TrimAllocator();
            Assert.Equal(0, KernelRuntime.Bodies.AllocatedCount);
            Assert.True(KernelRuntime.AllocatorStatistics.CapacityBytes < capacity);
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &replacement));
            Assert.True(replacement > body);
            Assert.False(KernelRuntime.IsValidTag(body));
            Assert.True(KernelRuntime.IsValidTag(replacement));
        }
        finally { KernelRuntime.SessionStop(); }
    }
}
