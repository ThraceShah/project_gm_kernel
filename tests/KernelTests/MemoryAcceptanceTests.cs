using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Memory architecture acceptance: zero managed allocations on the modeling
/// path, loop stability (surviving bytes return to baseline, free cache
/// bounded, tags beyond the old 4096 limit), full release across session
/// rounds, allocator fault injection and the tag monotonicity contract.
/// </summary>
public unsafe class MemoryAcceptanceTests
{
    private static void StartSession()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    // ── Exported entry points (the real caller path; every call is a
    //    dispatched command with its own undo boundary) ─────────────

    private static readonly delegate* unmanaged<double, double, double, PK_AXIS2_sf_s*, int*, int> CreateBlock
        = &KernelExports.PK_BODY_create_solid_block;
    private static readonly delegate* unmanaged<PK_POINT_sf_s*, int*, int> CreatePoint
        = &KernelExports.PK_POINT_create;
    private static readonly delegate* unmanaged<int, int*, int> DeleteEntity
        = &KernelExports.PK_ENTITY_delete;
    private static readonly delegate* unmanaged<int, double, int, PK_VECTOR_s*, int> EvalCurve
        = &KernelExports.PK_CURVE_eval;
    private static readonly delegate* unmanaged<int*, int> CreateMark
        = &KernelExports.PK_MARK_create;
    private static readonly delegate* unmanaged<int, int> GotoMark
        = &KernelExports.PK_MARK_goto;
    private static readonly delegate* unmanaged<int, int*, int**, int> AskFaces
        = &KernelExports.PK_BODY_ask_faces;
    private static readonly delegate* unmanaged<PK_MEMORY_block_s*, int> BlockFree
        = &KernelExports.PK_MEMORY_block_f;
    private static readonly delegate* unmanaged<void*, int> MemFree
        = &KernelExports.PK_MEMORY_free;

    private static int FindEdgeCurve(int bodyTag)
    {
        var bodySlot = KernelRuntime.ResolveTagRecord(bodyTag).Slot;
        return KernelRuntime.Edges[KernelRuntime.Bodies[bodySlot].FirstEdgeBody].CurveTag;
    }

    private static void Check(int error)
    {
        if (error != 0)
            throw new InvalidOperationException($"kernel call failed with {error}");
    }

    [Fact]
    public void ModelingQueryDeleteMark_ZeroManagedAllocation()
    {
        StartSession();
        // Warm every path (JIT, thread registration, scratch, free chains).
        RunModelingCycle();
        RunMarkCycle();
        RunQueryCycle();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
            RunModelingCycle();
        var afterModeling = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
            RunMarkCycle();
        var afterMark = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
            RunQueryCycle();
        var afterQuery = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, afterModeling - before);
        Assert.Equal(0, afterMark - afterModeling);
        Assert.Equal(0, afterQuery - afterMark);
        KernelRuntime.SessionStop();
    }

    private static void RunModelingCycle()
    {
        int body;
        Check(CreateBlock(1, 2, 3, null, &body));
        int point;
        var sf = new PK_POINT_sf_s();
        Check(CreatePoint(&sf, &point));

        // Evaluate one edge curve of the body.
        var bodySlot = KernelRuntime.ResolveTagRecord(body).Slot;
        var curveTag = KernelRuntime.Edges[KernelRuntime.Bodies[bodySlot].FirstEdgeBody].CurveTag;
        // order=1 writes position AND first derivative (two vectors).
        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[2];
        Check(EvalCurve(curveTag, 0.5, 1, output));

        Check(DeleteEntity(1, &body));
        Check(DeleteEntity(1, &point));
    }

    private static void RunMarkCycle()
    {
        int mark;
        Check(CreateMark(&mark));
        int body;
        Check(CreateBlock(1, 1, 1, null, &body));
        Check(DeleteEntity(1, &body));
        Check(GotoMark(mark));
    }

    private static void RunQueryCycle()
    {
        int body;
        Check(CreateBlock(1, 2, 3, null, &body));
        int count;
        int* facesOut = null;
        Check(AskFaces(body, &count, &facesOut));
        if (count != 6) throw new InvalidOperationException("block query returned the wrong face count");
        Check(MemFree(facesOut));
        Check(DeleteEntity(1, &body));
    }

    [Fact]
    public void CreateDeleteLoops_BytesReturnToBaseline_TagsExceed4096_FreeCacheBounded()
    {
        StartSession();
        RunModelingCycle();
        var baselineBlocks = KernelRuntime.BlocksLiveBytes;
        var baselineTags = KernelRuntime.NextTagValue;
        var baselineCapacity = KernelRuntime.AllocatorStatistics.CapacityBytes;

        for (int i = 0; i < 2000; i++)
            RunModelingCycle();

        // Tags are monotonic and must have passed the old 4096 ceiling.
        Assert.True(KernelRuntime.NextTagValue > 4096, "tags must exceed the legacy 4096 limit");
        Assert.True(KernelRuntime.NextTagValue > baselineTags, "tags keep growing monotonically");
        Assert.Equal(baselineBlocks, KernelRuntime.BlocksLiveBytes);

        var stats = KernelRuntime.AllocatorStatistics;
        Assert.True(stats.CachedBytes <= 2 * 1024 * 1024, $"free cache {stats.CachedBytes} exceeds 2 MiB budget");
        Assert.Equal(baselineCapacity, stats.CapacityBytes);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void SessionRoundsReleaseEverything()
    {
        for (int round = 0; round < 3; round++)
        {
            StartSession();
            for (int i = 0; i < 20; i++)
                RunModelingCycle();
            int body;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 2, 2, null, &body));   // survives until stop
            KernelRuntime.TrimAllocator();
            KernelRuntime.SessionStop();   // releases the still-held result buffer too

            var stats = KernelRuntime.AllocatorStatistics;
            Assert.Equal(0UL, stats.LiveBytes);
            Assert.Equal(0UL, stats.CapacityBytes);
            Assert.Equal(0UL, stats.LiveBlocks);
            Assert.Equal(0UL, KernelRuntime.ReturnOutstandingBlocks);
        }
    }

    [Fact]
    public void QueryResultsSurviveLaterQueriesAndMarkGoto()
    {
        StartSession();
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        int count;
        int* faces = null; int* outFaces = null;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &outFaces));
        var firstFaces = outFaces;

        // Later queries must not invalidate earlier results.
        int* edges = null; int* outEdges = null;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &outEdges));

        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        int other;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &other));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));

        // Earlier returned buffers stay valid; the face tags still resolve.
        int kind;
        Assert.Equal(0, KernelRuntime.EntityAskClass(firstFaces[0], &kind));
        Assert.Equal(ParasolidConstants.PK_CLASS_face, kind);
        Assert.Equal(0, KernelRuntime.MemoryFree(firstFaces));
        Assert.Equal(0, KernelRuntime.MemoryFree(outEdges));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void AllocatorFaultInjection_LeavesModelConsistent()
    {
        // Each injection point runs on a fresh session so the Nth allocation
        // of the create command is actually reached.
        for (long failAfter = 0; failAfter < 24; failAfter++)
        {
            StartSession();
            int anchor;
            Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &anchor));
            var baselineBlocks = KernelRuntime.BlocksLiveBytes;

            KernelRuntime.InjectAllocationFailure(failAfter);
            int body;
            var error = KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body);
            KernelRuntime.InjectAllocationFailure(-1);   // disarm

            if (error != 0)
            {
                // The failed command left no published tag behind.
                Assert.True(body == 0 || !KernelRuntime.IsValidTag(body), "failed create left a published tag");
            }

            // Model consistency: the anchor body still evaluates and deletes,
            // payload/undo/return accounting returns to its baseline.
            int kind;
            Assert.Equal(0, KernelRuntime.EntityAskClass(anchor, &kind));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &anchor));
            Assert.Equal(baselineBlocks, KernelRuntime.BlocksLiveBytes);
            Assert.Equal(0UL, KernelRuntime.ReturnOutstandingBlocks);
            KernelRuntime.SessionStop();
            var final = KernelRuntime.AllocatorStatistics;
            Assert.Equal(0UL, final.LiveBlocks);
        }
    }

    [Fact]
    public void TagMonotonicity_OldTagsNeverReborn()
    {
        StartSession();
        int first;
        var sf = new PK_POINT_sf_s();
        Assert.Equal(0, KernelRuntime.PointCreate(&sf, &first));

        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        int doomed;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf, &doomed));
        int afterDoomed = KernelRuntime.NextTagValue;
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));

        // The rolled-back tag is not reused: the next creation gets a fresh tag.
        int next;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf, &next));
        Assert.True(next >= afterDoomed && next != doomed, "the rolled-back tag is never reused");

        // Deleting and recreating never resurrects an old tag either.
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &next));
        int again;
        Assert.Equal(0, KernelRuntime.PointCreate(&sf, &again));
        Assert.True(again > next);
        // The never-deleted first point survives the whole cycle.
        int kind;
        Assert.Equal(0, KernelRuntime.EntityAskClass(first, &kind));
        Assert.Equal(ParasolidConstants.PK_CLASS_point, kind);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void CommandFailureLeavesNothingBehind()
    {
        StartSession();
        // A prism with an invalid side count fails before allocating anything.
        int body;
        Assert.NotEqual(0, KernelRuntime.BodyCreateSolidPrism(1, 1, 2, null, &body));

        // A failure deep inside creation (injected) must not leave half a body.
        KernelRuntime.InjectAllocationFailure(10);
        Assert.NotEqual(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        KernelRuntime.InjectAllocationFailure(-1);

        int count;
        Assert.Equal(0, KernelRuntime.PartitionAskBodiesCount(0, &count));
        Assert.Equal(0, count);
        KernelRuntime.SessionStop();
    }
}
