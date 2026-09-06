using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public unsafe class GeometryOwnershipTests : IDisposable
{
    public GeometryOwnershipTests()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose()
    {
        KernelRuntime.InjectAllocationFailure(-1);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void SharedCurveDeletionAndRollbackPreserveReferenceCounts()
    {
        int body, mark;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var first = KernelRuntime.Bodies[bodySlot].FirstEdgeBody;
        var second = KernelRuntime.Edges[first].NextInBody;
        var firstCurve = KernelRuntime.Edges[first].CurveTag;
        var secondCurve = KernelRuntime.Edges[second].CurveTag;
        var secondTag = KernelRuntime.TagOf(PoolKind.Edge, second);
        var curveSlot = KernelRuntime.GetCurveSlotByTag(firstCurve);
        Assert.Equal(1, KernelRuntime.Curves[curveSlot].OwnerCount);
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(secondTag));
        Assert.Equal(0, KernelRuntime.EdgeAttachCurves(1, &secondTag, &firstCurve));
        Assert.Equal(2, KernelRuntime.Curves[curveSlot].OwnerCount);
        Assert.Equal(first, KernelRuntime.Curves[curveSlot].OwnerEdge);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &secondCurve));
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(firstCurve));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.True(KernelRuntime.IsValidTag(firstCurve));
        Assert.Equal(2, KernelRuntime.Curves[curveSlot].OwnerCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(firstCurve));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void ReattachmentAndDetachmentRestoreAllBeforeImages()
    {
        int body, mark;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var first = KernelRuntime.Bodies[bodySlot].FirstEdgeBody;
        var second = KernelRuntime.Edges[first].NextInBody;
        var firstCurve = KernelRuntime.Edges[first].CurveTag;
        var secondCurve = KernelRuntime.Edges[second].CurveTag;
        var secondTag = KernelRuntime.TagOf(PoolKind.Edge, second);
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(secondTag));
        Assert.Equal(0, KernelRuntime.EdgeAttachCurves(1, &secondTag, &firstCurve));
        Assert.Equal(2, KernelRuntime.GetCurveByTag(firstCurve).OwnerCount);
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(secondCurve, KernelRuntime.Edges[second].CurveTag);
        Assert.Equal(1, KernelRuntime.GetCurveByTag(firstCurve).OwnerCount);
        Assert.Equal(1, KernelRuntime.GetCurveByTag(secondCurve).OwnerCount);
        Assert.Equal(first, KernelRuntime.GetCurveByTag(firstCurve).OwnerEdge);
        Assert.Equal(second, KernelRuntime.GetCurveByTag(secondCurve).OwnerEdge);
    }

    [Fact]
    public void FailedSnapshotLeavesExistingTopologyUntouched()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var edge = KernelRuntime.Bodies[bodySlot].FirstEdgeBody;
        var tag = KernelRuntime.TagOf(PoolKind.Edge, edge);
        var curve = KernelRuntime.Edges[edge].CurveTag;
        KernelRuntime.InjectAllocationFailure(0);
        Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, KernelRuntime.TopologyDetachGeometry(tag));
        Assert.Equal(curve, KernelRuntime.Edges[edge].CurveTag);
        Assert.Equal(1, KernelRuntime.GetCurveByTag(curve).OwnerCount);
        Assert.Equal(0, KernelRuntime.State.Session->UndoEntryCount);
    }

    [Fact]
    public void CrossPartitionAttachmentAndMixedDeletionFailBeforeMutating()
    {
        int body, partition, point;
        var position = new PK_POINT_sf_s();
        Assert.Equal(0, KernelRuntime.PointCreate(&position, &point));
        Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&partition));
        Assert.Equal(0, KernelRuntime.PartitionSetCurrent(partition));
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var vertex = KernelRuntime.Bodies[bodySlot].FirstVertexBody;
        var tag = KernelRuntime.TagOf(PoolKind.Vertex, vertex);
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(tag));
        Assert.Equal(ParasolidConstants.PK_ERROR_not_in_same_partition, KernelRuntime.VertexAttachPoints(1, &tag, &point));
        Assert.Equal(0, KernelRuntime.Vertices[vertex].PointTag);
        int* deletion = stackalloc int[2] { body, tag };
        Assert.Equal(ParasolidConstants.PK_ERROR_is_attached, KernelRuntime.EntityDelete(2, deletion));
        Assert.True(KernelRuntime.IsValidTag(body));
        Assert.True(KernelRuntime.IsValidTag(tag));
    }
}
