using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class PlanarEditingTests : IDisposable
{
    public PlanarEditingTests()
    { KernelRuntime.SessionStop(); var o = new PK_SESSION_start_o_s { o_t_version = 1 }; Assert.Equal(0, KernelRuntime.SessionStart(&o)); }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Fact]
    public void ImprintSplitsFacesRetainsHandlesAndReportsNewEdges()
    {
        var body = BooleanOperationTests.Block(0, 0, 0, 2, 2, 2); var plane = Plane(0, 0, 1, 0, 0, 1);
        int nf = 0; int* original = null; Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &nf, &original));
        int count = 0; int* edges = null;
        Assert.Equal(0, KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &count, &edges));
        Assert.Equal(4, count); BooleanOperationTests.Counts(body, 10, 20, 12);
        for (var i = 0; i < nf; i++) { int code = 0; Assert.Equal(0, KernelRuntime.EntityAskClass(original[i], &code)); Assert.Equal(ParasolidConstants.PK_CLASS_face, code); }
        for (var i = 0; i < count; i++) { int code = 0; Assert.Equal(0, KernelRuntime.EntityAskClass(edges[i], &code)); Assert.Equal(ParasolidConstants.PK_CLASS_edge, code); }
        KernelRuntime.MemoryFree(original); KernelRuntime.MemoryFree(edges);
        Assert.Equal(0, KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &count, &edges));
        Assert.Equal(4, count); KernelRuntime.MemoryFree(edges); BooleanOperationTests.Counts(body, 10, 20, 12);
    }

    [Theory]
    [InlineData(0, 4)] [InlineData(1, 4)] [InlineData(3, 0)]
    public void ModernImprintReturnsOneCreateRecordIncludingExistingEdges(double z, int expected)
    {
        var body = BooleanOperationTests.Block(0, 0, 0, 2, 2, 2); var plane = Plane(0, 0, z, 0, 0, 1);
        var options = new PK_BODY_imprint_plane_o_s { o_t_version = 1, imprint_dir = ParasolidConstants.PK_imprint_dir_no_check_c };
        PK_TOPOL_track_r_s tracking = default;
        var error = KernelRuntime.BodyImprintPlane2(body, plane, &options, &tracking);
        Assert.Equal(expected == 0 ? ParasolidConstants.PK_ERROR_partial_no_intersect : 0, error);
        if (expected == 0) { Assert.Equal(0, tracking.n_track_records); Assert.True(tracking.track_records == null); }
        else
        {
            Assert.Equal(1, tracking.n_track_records); var record = tracking.track_records[0];
            Assert.Equal(ParasolidConstants.PK_TOPOL_track_create_c, record.track);
            Assert.Equal(0, record.n_original_topols); Assert.Equal(expected, record.n_product_topols);
            for (var i = 0; i < expected; i++) { int code = 0; Assert.Equal(0, KernelRuntime.EntityAskClass(record.product_topols[i], &code)); Assert.Equal(ParasolidConstants.PK_CLASS_edge, code); }
        }
        Assert.Equal(0, KernelRuntime.TopolTrackResultFree(&tracking));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ImprintThenTrimKeepsSelectedConnectedSheetComponent(bool keep)
    {
        var body = Sheet(); var plane = Plane(0, 0, 0, 1, 0, 0);
        int count = 0; int* edges = null; Assert.Equal(0, KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &count, &edges));
        Assert.Equal(1, count); BooleanOperationTests.Counts(body, 2, 7, 6);
        var selected = Locate(body, -.5, 0, 0, ParasolidConstants.PK_enclosure_on_c, ParasolidConstants.PK_CLASS_face);
        Assert.Equal(0, KernelRuntime.BodyTrim(body, count, edges, 1, &selected, keep ? (byte)1 : (byte)0));
        KernelRuntime.MemoryFree(edges); BooleanOperationTests.Counts(body, 1, 4, 4);
        var retained = Locate(body, keep ? -.5 : .5, 0, 0, ParasolidConstants.PK_enclosure_on_c, ParasolidConstants.PK_CLASS_face);
        if (keep) Assert.Equal(selected, retained);
        Locate(body, keep ? .5 : -.5, 0, 0, ParasolidConstants.PK_enclosure_outside_c, ParasolidConstants.PK_CLASS_region);
    }

    [Fact]
    public void ContainsVectorReturnsLowestDimensionalBoundaryAndCorrectCavityRegion()
    {
        var body = BooleanOperationTests.Block(0, 0, 0, 4, 4, 4);
        Locate(body, -2, -2, 0, ParasolidConstants.PK_enclosure_on_c, ParasolidConstants.PK_CLASS_vertex);
        Locate(body, -2, 0, 0, ParasolidConstants.PK_enclosure_on_c, ParasolidConstants.PK_CLASS_edge);
        Locate(body, 0, 0, 0, ParasolidConstants.PK_enclosure_on_c, ParasolidConstants.PK_CLASS_face);
        var cavity = BooleanOperationTests.Block(0, 0, 1, 2, 2, 2); var options = BooleanOperationTests.Options(ParasolidConstants.PK_boolean_subtract_c);
        int n = 0; int* results = null; Assert.Equal(0, KernelRuntime.BodyBoolean(body, 1, &cavity, &options, &n, &results)); KernelRuntime.MemoryFree(results);
        var material = Locate(body, 1.5, 0, 2, ParasolidConstants.PK_enclosure_inside_c, ParasolidConstants.PK_CLASS_region);
        var bounded = Locate(body, 0, 0, 2, ParasolidConstants.PK_enclosure_outside_c, ParasolidConstants.PK_CLASS_region);
        var exterior = Locate(body, 10, 0, 2, ParasolidConstants.PK_enclosure_outside_c, ParasolidConstants.PK_CLASS_region);
        Assert.NotEqual(material, bounded); Assert.NotEqual(bounded, exterior);
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(2, 0)] [InlineData(3, 0)]
    [InlineData(-1, 1)] [InlineData(0, 1)] [InlineData(1, 1)] [InlineData(2, 1)] [InlineData(3, 1)]
    [InlineData(-1, 2)] [InlineData(0, 2)] [InlineData(1, 2)] [InlineData(2, 2)] [InlineData(3, 2)]
    public void SectionFencesCapOnlyActualCutsAndRetainTargetIdentity(double z, int side)
    {
        var body = BooleanOperationTests.Block(0, 0, 0, 2, 2, 2); var plane = Plane(0, 0, z, 0, 0, 1);
        var options = SectionOptions(side); PK_section_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, plane, &options, &result));
        var front = z < 2 && side != 2; var back = z > 0 && side != 1;
        Assert.Equal(front ? 1 : 0, result.front_bodies.length); Assert.Equal(back ? 1 : 0, result.back_bodies.length);
        Assert.Equal(front && z > 0 ? 1 : 0, result.front_faces.length); Assert.Equal(back && z < 2 ? 1 : 0, result.back_faces.length);
        if (front) { Assert.Equal(body, result.front_bodies.array[0]); BooleanOperationTests.Counts(result.front_bodies.array[0], 6, 12, 8); }
        if (back) { if (!front) Assert.Equal(body, result.back_bodies.array[0]); BooleanOperationTests.Counts(result.back_bodies.array[0], 6, 12, 8); }
        Assert.Equal(0, KernelRuntime.SectionResultFree(&result)); Assert.Equal(0, KernelRuntime.SectionResultFree(&result));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(8)] [InlineData(12)] [InlineData(25)]
    public void SectionAllocationBoundariesEitherRestoreInputsOrPublishCompleteResults(int budget)
    {
        var body = BooleanOperationTests.Block(0, 0, 0, 2, 2, 2); var plane = Plane(0, 0, 1, 0, 0, 1);
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Faces.AliveCount, KernelRuntime.Edges.AliveCount,
            KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        var options = SectionOptions(0); PK_section_r_s result = default; KernelRuntime.InjectAllocationFailure(budget);
        var error = KernelRuntime.BodySectionWithSurf(body, plane, &options, &result); KernelRuntime.InjectAllocationFailure(-1);
        if (error == 0)
        {
            Assert.Equal(1, result.front_bodies.length); Assert.Equal(1, result.back_bodies.length);
            BooleanOperationTests.Counts(result.front_bodies.array[0], 6, 12, 8); BooleanOperationTests.Counts(result.back_bodies.array[0], 6, 12, 8);
            Assert.Equal(0, KernelRuntime.SectionResultFree(&result)); return;
        }
        Assert.Equal(0, result.front_bodies.length); Assert.Equal(0, result.back_bodies.length);
        Assert.True(result.front_bodies.array == null && result.back_bodies.array == null && result.front_faces.array == null && result.back_faces.array == null);
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Faces.AliveCount, KernelRuntime.Edges.AliveCount,
            KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
        BooleanOperationTests.Counts(body, 6, 12, 8);
    }

    private static PK_BODY_section_o_s SectionOptions(int side) => new()
    { o_t_version = 5, fence = side == 0 ? ParasolidConstants.PK_section_fence_both_c : side == 1 ? ParasolidConstants.PK_section_fence_front_c : ParasolidConstants.PK_section_fence_back_c, default_tol = 1e-5 };
    private static int Locate(int body, double x, double y, double z, int expected, int expectedClass)
    {
        var vector = new PK_VECTOR_s(); vector.coord[0] = x; vector.coord[1] = y; vector.coord[2] = z;
        int enclosure = 0, entity = 0, code = 0; Assert.Equal(0, KernelRuntime.BodyContainsVector(body, vector, &enclosure, &entity));
        Assert.Equal(expected, enclosure); Assert.Equal(0, KernelRuntime.EntityAskClass(entity, &code)); Assert.Equal(expectedClass, code); return entity;
    }
    private static int Plane(double x, double y, double z, double nx, double ny, double nz)
    {
        var d = new PK_PLANE_sf_s(); d.basis_set.location.coord[0] = x; d.basis_set.location.coord[1] = y; d.basis_set.location.coord[2] = z;
        d.basis_set.axis.coord[0] = nx; d.basis_set.axis.coord[1] = ny; d.basis_set.axis.coord[2] = nz;
        d.basis_set.ref_direction.coord[nx == 0 ? 0 : 1] = 1;
        int plane = 0; Assert.Equal(0, KernelRuntime.PlaneCreate(&d, &plane)); return plane;
    }
    private static int Sheet()
    {
        PK_VECTOR_s* points = stackalloc PK_VECTOR_s[4];
        points[0].coord[0] = -1; points[0].coord[1] = -1; points[1].coord[0] = 1; points[1].coord[1] = -1;
        points[2].coord[0] = 1; points[2].coord[1] = 1; points[3].coord[0] = -1; points[3].coord[1] = 1;
        var o = new PK_BODY_create_sheet_planar_o_s { o_t_version = 1 }; o.plane.axis.coord[2] = 1; o.basis_set.axis.coord[2] = 1; o.basis_set.ref_direction.coord[0] = 1;
        int body = 0; Assert.Equal(0, KernelRuntime.BodyCreateSheetPlanar(4, points, &o, &body)); return body;
    }
}
