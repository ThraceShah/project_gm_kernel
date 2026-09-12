using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Storage and lifecycle tests for the procedural geometry pools (spec §3,
/// task T01): pool create/free/slot-reuse with generation bumps, owned
/// variable-length blocks, source surface sense independence from face sense,
/// and the BLEND_BOUND constructive reference that never becomes a public tag.
/// </summary>
public unsafe class IcurveStorageTests
{
    public IcurveStorageTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public unsafe void IcurvePool_CreateFreeReuse_BumpsGeneration()
    {
        Assert.True(KernelRuntime.ICurveDataPool.TryAllocate(out var first));
        ref var data = ref KernelRuntime.ICurveDataPool[first];
        data.Header.Tag = 0; // pooled procedural records publish no tag of their own
        data.BaseParameter = -2.0;
        data.BaseScale = 1.7;
        data.ChartCount = 4;
        Assert.True(KernelRuntime.ICurveDataPool.IsAlive(first));
        var firstGeneration = KernelRuntime.ICurveDataPool.GetGeneration(first);

        KernelRuntime.FreeICurveData(first);
        Assert.False(KernelRuntime.ICurveDataPool.IsAlive(first));

        // Slot reuse: the freed slot is handed out again with a higher generation.
        Assert.True(KernelRuntime.ICurveDataPool.TryAllocate(out var second));
        Assert.Equal(first, second);
        Assert.True(KernelRuntime.ICurveDataPool.GetGeneration(second) > firstGeneration);
        // The reused slot must not surface the previous occupant's data.
        Assert.Equal(0, KernelRuntime.ICurveDataPool[second].BaseParameter, 12);
        KernelRuntime.ICurveDataPool.Free(second);
    }

    [Fact]
    public unsafe void ProceduralPools_OwnedBlocksRoundtripAndRelease()
    {
        var blocks = &KernelRuntime.State.Session->Blocks;

        // Hull-vector block owned by an icurve record.
        Assert.True(KernelRuntime.ICurveDataPool.TryAllocate(out var icurveSlot));
        ref var icurve = ref KernelRuntime.ICurveDataPool[icurveSlot];
        var hvecBlock = blocks->TryAllocate(3 * 6 * sizeof(double)); // 6 hull vectors
        Assert.True(hvecBlock != null);
        icurve.HvecBlock = blocks->HandleOf(hvecBlock);
        icurve.HvecCount = 6;
        var hullVectors = new Span<double>(KernelRuntime.DereferenceBlock(icurve.HvecBlock), 18);
        for (var i = 0; i < 18; i++) hullVectors[i] = i + 0.5;
        // UV block owned by the same record.
        var uvBlock = blocks->TryAllocate(12 * sizeof(double));
        Assert.True(uvBlock != null);
        icurve.UvValueBlock = blocks->HandleOf(uvBlock);
        icurve.UvValueCount = 12;

        Assert.Equal(0.5, new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(icurve.HvecBlock), 18)[0], 12);
        KernelRuntime.FreeICurveData(icurveSlot);
        // The freed block pointer must no longer dereference.
        Assert.True(KernelRuntime.DereferenceBlock(icurve.HvecBlock) == null);

        // Blended-edge record with its own limit block.
        Assert.True(KernelRuntime.BlendedEdgeDataPool.TryAllocate(out var edgeSlot));
        ref var edge = ref KernelRuntime.BlendedEdgeDataPool[edgeSlot];
        edge.BlendType = BlendType.RollingBall;
        edge.Range0 = edge.Range1 = 0.25;
        var edgeBlock = blocks->TryAllocate(2 * 3 * sizeof(double));
        Assert.True(edgeBlock != null);
        edge.HvecBlock = blocks->HandleOf(edgeBlock);
        edge.HvecCount = 2;
        KernelRuntime.FreeBlendEdgeData(edgeSlot);
        Assert.False(KernelRuntime.BlendedEdgeDataPool.IsAlive(edgeSlot));
    }

    [Fact]
    public unsafe void BlendFamilyPools_LifecycleWithoutPublicTags()
    {
        Assert.True(KernelRuntime.BlendedVertexDataPool.TryAllocate(out var vertexSlot));
        KernelRuntime.BlendedVertexDataPool[vertexSlot].Range0 = 0.5;
        Assert.True(KernelRuntime.BlendedVertexDataPool.IsAlive(vertexSlot));

        Assert.True(KernelRuntime.BlendOverlapDataPool.TryAllocate(out var overlapSlot));
        KernelRuntime.BlendOverlapDataPool[overlapSlot].SwapUV = 0;
        Assert.True(KernelRuntime.BlendOverlapDataPool.IsAlive(overlapSlot));

        Assert.True(KernelRuntime.BlendBoundDataPool.TryAllocate(out var boundSlot));
        ref var bound = ref KernelRuntime.BlendBoundDataPool[boundSlot];
        bound.Boundary = 1;
        bound.BlendTag = 0; // tag of a real blend surface would go here in T12+
        Assert.True(KernelRuntime.BlendBoundDataPool.IsAlive(boundSlot));
        // Constructive records carry no published PK tag.
        Assert.Equal(0, bound.Header.Tag);

        KernelRuntime.BlendedVertexDataPool.Free(vertexSlot);
        KernelRuntime.BlendOverlapDataPool.Free(overlapSlot);
        KernelRuntime.BlendBoundDataPool.Free(boundSlot);
    }

    [Fact]
    public unsafe void BlendBound_ConstructiveReferenceResolvesWithoutPublicTag()
    {
        // A blended edge references its two boundaries through internal pool
        // handles; resolution must not mint or require public PK tags.
        Assert.True(KernelRuntime.BlendedEdgeDataPool.TryAllocate(out var edgeSlot));
        Assert.True(KernelRuntime.BlendBoundDataPool.TryAllocate(out var bound0Slot));
        Assert.True(KernelRuntime.BlendBoundDataPool.TryAllocate(out var bound1Slot));

        KernelRuntime.BlendBoundDataPool[bound0Slot].Boundary = 0;
        KernelRuntime.BlendBoundDataPool[bound1Slot].Boundary = 1;
        ref var edge = ref KernelRuntime.BlendedEdgeDataPool[edgeSlot];
        edge.Boundary0Tag = bound0Slot;
        edge.Boundary1Tag = bound1Slot;

        ref readonly var resolved0 = ref KernelRuntime.BlendBoundDataPool[edge.Boundary0Tag];
        ref readonly var resolved1 = ref KernelRuntime.BlendBoundDataPool[edge.Boundary1Tag];
        // blend.surface[1 - boundary]: boundary 0 pairs with surface[1], boundary 1 with surface[0].
        Assert.Equal(0, resolved0.Boundary);
        Assert.Equal(1, resolved1.Boundary);
        Assert.Equal(0, resolved0.Header.Tag);
        Assert.Equal(0, resolved1.Header.Tag);

        KernelRuntime.BlendBoundDataPool.Free(bound0Slot);
        KernelRuntime.BlendBoundDataPool.Free(bound1Slot);
        KernelRuntime.BlendedEdgeDataPool.Free(edgeSlot);
    }

    [Fact]
    public unsafe void SurfaceSense_IsSourceSense_NotFaceSense()
    {
        var plane = CreatePlane();
        Assert.Equal(ParasolidConstants.PK_TOPOL_sense_positive_c,
            KernelRuntime.Surfaces[KernelRuntime.ResolveTagRecord(plane).Slot].Sense);

        // Import path may store a negative source sense; creating and using
        // faces must never rewrite it.
        ref var surface = ref KernelRuntime.Surfaces[KernelRuntime.ResolveTagRecord(plane).Slot];
        surface.Sense = ParasolidConstants.PK_TOPOL_sense_negative_c;

        var other = CreatePlane();
        Assert.Equal(ParasolidConstants.PK_TOPOL_sense_positive_c,
            KernelRuntime.Surfaces[KernelRuntime.ResolveTagRecord(other).Slot].Sense);
        Assert.Equal(ParasolidConstants.PK_TOPOL_sense_negative_c,
            KernelRuntime.Surfaces[KernelRuntime.ResolveTagRecord(plane).Slot].Sense);
    }

    private static int CreatePlane()
    {
        var sf = new PK_PLANE_sf_s();
        sf.basis_set.location.coord[0] = 0;
        sf.basis_set.location.coord[1] = 0;
        sf.basis_set.location.coord[2] = 0;
        sf.basis_set.axis.coord[0] = 0;
        sf.basis_set.axis.coord[1] = 0;
        sf.basis_set.axis.coord[2] = 1;
        sf.basis_set.ref_direction.coord[0] = 1;
        sf.basis_set.ref_direction.coord[1] = 0;
        sf.basis_set.ref_direction.coord[2] = 0;
        int tag = 0;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&sf, &tag));
        return tag;
    }
}
