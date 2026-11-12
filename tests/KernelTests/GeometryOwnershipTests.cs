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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AdoptedBlendUnsharesExternalGeometryAndPreservesMarkLifecycle(bool commit)
    {
        var blend = BlendIntersectionTests.CreateBlend(); var definition = new PK_BLENDSF_sf_s();
        Assert.Equal(0, KernelRuntime.BlendSfAsk(blend, &definition)); var source = definition.geom_1;
        double* poles = stackalloc double[4] { .2, .2, .8, .8 }; double* knots = stackalloc double[2] { 0, 1 }; int* mult = stackalloc int[2] { 2, 2 };
        var uv = new PK_BCURVE_sf_s { degree = 1, n_vertices = 2, vertex_dim = 2, vertex = poles,
            n_knots = 2, knot = knots, knot_mult = mult, form = ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            knot_type = ParasolidConstants.PK_knot_non_uniform_c, self_intersecting = ParasolidConstants.PK_self_intersect_false_c };
        int basis, first, second, offset, mark, body;
        Assert.Equal(0, KernelRuntime.BCurveCreate(&uv, &basis));
        var sp = new PK_SPCURVE_sf_s { surf = source, curve = basis };
        Assert.Equal(0, KernelRuntime.SpCurveCreate(&sp, &first));
        Assert.Equal(ParasolidConstants.PK_ERROR_unsuitable_entity, KernelRuntime.SpCurveCreate(&sp, &second)); Assert.Equal(0, second);
        Assert.Equal(0, KernelRuntime.BCurveCreate(&uv, &sp.curve)); Assert.Equal(0, KernelRuntime.SpCurveCreate(&sp, &second));
        var displaced = new PK_OFFSET_sf_s { underlying_surface = source, offset_distance = .3 };
        Assert.Equal(0, KernelRuntime.OffsetCreate(&displaced, &offset));
        var spineDefinition = KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(definition.spine).DataIndex];
        var sphereDefinition = new PK_SPHERE_sf_s { radius = 1 }; sphereDefinition.basis_set.axis.coord[2] = 1; sphereDefinition.basis_set.ref_direction.coord[0] = 1;
        int sphere; Assert.Equal(0, KernelRuntime.SphereCreate(&sphereDefinition, &sphere));
        var borrower = IcurveRuntimeEvalTests.BindCircle(spineDefinition.Surface0Tag, sphere);
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        var box = new PK_UVBOX_s(); box.param[0] = box.param[1] = .1; box.param[2] = box.param[3] = .9;
        Assert.Equal(0, KernelRuntime.SurfMakeSheetBody(blend, box, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        Assert.Equal(blend, KernelRuntime.Faces[KernelRuntime.Bodies[slot].FirstFaceBody].SurfTag);
        PK_SPCURVE_sf_s firstAfter, secondAfter; PK_OFFSET_sf_s offsetAfter;
        Assert.Equal(0, KernelRuntime.SpCurveAsk(first, &firstAfter)); Assert.Equal(0, KernelRuntime.SpCurveAsk(second, &secondAfter));
        Assert.Equal(0, KernelRuntime.OffsetAsk(offset, &offsetAfter));
        Assert.NotEqual(source, firstAfter.surf); Assert.Equal(firstAfter.surf, secondAfter.surf);
        Assert.Equal(firstAfter.surf, offsetAfter.underlying_surface); Assert.Equal(basis, firstAfter.curve);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(blend), "adopted Blend root survived body deletion");
        var alive = KernelRuntime.IsValidTag(source);
        Assert.False(alive, alive ? $"adopted support survived body deletion: owners={KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(source)].OwnerCount}, geometryOwners={KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(source)].GeometricOwnerCount}" : "");
        PK_VECTOR_s point; Assert.Equal(0, KernelRuntime.CurveEval(first, .5, 0, &point)); Assert.Equal(0, KernelRuntime.CurveEval(second, .5, 0, &point));
        if (commit)
        {
            Assert.Equal(0, KernelRuntime.MarkDelete(mark));
            Assert.False(KernelRuntime.IsValidTag(blend)); Assert.False(KernelRuntime.IsValidTag(source));
            Assert.Equal(0, KernelRuntime.CurveEval(first, .5, 0, &point)); Assert.Equal(0, KernelRuntime.CurveEval(second, .5, 0, &point));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &first)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &second)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &offset));
            var borrowed = KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(borrower).DataIndex];
            Assert.NotEqual(spineDefinition.Surface0Tag, borrowed.Surface0Tag); Assert.Equal(sphere, borrowed.Surface1Tag);
            Assert.Equal(0, KernelRuntime.CurveEval(borrower, .5, 0, &point));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &borrower)); Assert.False(KernelRuntime.IsValidTag(borrowed.Surface0Tag));
            Assert.True(KernelRuntime.IsValidTag(sphere)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &sphere));
            Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
            return;
        }
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
        Assert.True(KernelRuntime.IsValidTag(blend)); Assert.True(KernelRuntime.IsValidTag(source)); Assert.False(KernelRuntime.IsValidTag(firstAfter.surf));
        Assert.Equal(0, KernelRuntime.SpCurveAsk(first, &firstAfter)); Assert.Equal(0, KernelRuntime.OffsetAsk(offset, &offsetAfter));
        Assert.Equal(source, firstAfter.surf); Assert.Equal(source, offsetAfter.underlying_surface);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var created = KernelRuntime.SurfMakeSheetBody(blend, box, &body);
        var deleted = created == 0 ? KernelRuntime.EntityDelete(1, &body) : created;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, created); Assert.Equal(0, deleted); Assert.Equal(0, allocated);
        Assert.Equal(0, KernelRuntime.CurveEval(first, .5, 0, &point));
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
