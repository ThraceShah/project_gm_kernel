using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class CurvedSheetPartitionTests : IDisposable
{
    public CurvedSheetPartitionTests()
    {
        KernelRuntime.SessionStop(); var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactHyperbolaImprintFeedsSeedTrimWithIndependentGeometry(bool keep)
    {
        var body = Sheet(); var plane = Plane(.25); Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        var original = KernelRuntime.Bodies[slot].FirstFaceBody;
        var originalTag = KernelRuntime.Faces[original].Header.Tag;
        int n = 0; int* edges = null;
        Assert.Equal(0, KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &n, &edges));
        try
        {
            Assert.Equal(1, n); BooleanOperationTests.Counts(body, 2, 7, 6);
            Assert.True(KernelRuntime.IsValidTag(originalTag));
            var face = KernelRuntime.Bodies[slot].FirstFaceBody;
            var seed = KernelRuntime.Faces[face].Header.Tag;
            Assert.Equal(0, KernelRuntime.BodyTrim(body, n, edges, 1, &seed, keep ? (byte)1 : (byte)0));
            var bs = slot; Assert.Equal(1, KernelRuntime.Bodies[bs].FaceCountBody);
            var surface = KernelRuntime.Faces[KernelRuntime.Bodies[bs].FirstFaceBody].SurfTag;
            Assert.Equal(SurfaceClass.BSurface, KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface)].Class);
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            Assert.Equal(0, KernelRuntime.Curves.AliveCount);
            // Original orphan support and the cutting plane remain independent.
            Assert.Equal(2, KernelRuntime.Surfaces.AliveCount);
        }
        finally { KernelRuntime.MemoryFree(edges); }
    }

    [Theory]
    [InlineData(18200, 1, 0)] [InlineData(18201, 0, 1)] [InlineData(18202, 1, 1)]
    public void SectionClassifiesExactCellsAndReturnsTrimmedSheets(int fence, int front, int back)
    {
        var body = Sheet(); var plane = Plane(.25);
        var options = new PK_BODY_section_o_s { o_t_version = 5, fence = fence, default_tol = 1e-8 };
        PK_section_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, plane, &options, &result));
        try
        {
            Assert.Equal(front, result.front_bodies.length); Assert.Equal(back, result.back_bodies.length);
            Assert.Equal(0, result.front_faces.length); Assert.Equal(0, result.back_faces.length);
            if (front != 0) { Assert.Equal(body, result.front_bodies.array[0]); BooleanOperationTests.Counts(body, 1, 3, 3); }
            if (back != 0) BooleanOperationTests.Counts(result.back_bodies.array[0], 1, 5, 5);
            if (front != 0) Assert.Equal(0, KernelRuntime.EntityDelete(front, result.front_bodies.array));
            if (back != 0) Assert.Equal(0, KernelRuntime.EntityDelete(back, result.back_bodies.array));
            Assert.Equal(0, KernelRuntime.Bodies.AliveCount); Assert.Equal(0, KernelRuntime.Curves.AliveCount);
            Assert.Equal(2, KernelRuntime.Surfaces.AliveCount);
        }
        finally { KernelRuntime.SectionResultFree(&result); }
    }

    [Fact]
    public void SingularCrossingFailsAtomically()
    {
        var body = Sheet(true); var plane = Plane(0); int n = 5; int* edges = (int*)1;
        Assert.Contains(KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &n, &edges),new[]{ParasolidConstants.PK_ERROR_not_implemented,ParasolidConstants.PK_ERROR_eval_failure,ParasolidConstants.PK_ERROR_cant_do_intersect});
        Assert.Equal(0, n); Assert.True(edges == null); BooleanOperationTests.Counts(body, 1, 4, 4);
    }

    [Fact]
    public void CreasesAreRejectedWithoutChangingTheSheet()
    {
        var body = Sheet(cubic: true, crease: true); var plane = Plane(0);
        var curves = KernelRuntime.Curves.AliveCount; var surfaces = KernelRuntime.Surfaces.AliveCount;
        int n = 0; int* edges = null;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &n, &edges));
        Assert.Equal(0, n); Assert.True(edges == null); BooleanOperationTests.Counts(body, 1, 4, 4);
        Assert.Equal(curves, KernelRuntime.Curves.AliveCount); Assert.Equal(surfaces, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void PretrimmedRectangularDomainClipsTheIntersectionToItsActualBoundary()
    {
        var body=Sheet(subdomain:true);var plane=Plane(.25);int n=0;int* edges=null;
        Assert.Equal(0,KernelRuntime.BodyImprintPlane(body,plane,1e-8,&n,&edges));
        try{Assert.Equal(1,n);BooleanOperationTests.Counts(body,2,7,6);}finally{KernelRuntime.MemoryFree(edges);}
    }

    [Fact]
    public void CubicCutFindsBothComponentsAndSectionsDisconnectedBackSheets()
    {
        var body = Sheet(cubic: true); var plane = Plane(0); int n = 0; int* edges = null;
        Assert.Equal(0, KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &n, &edges));
        try { Assert.Equal(2, n); BooleanOperationTests.Counts(body, 3, 10, 8); }
        finally { KernelRuntime.MemoryFree(edges); }
        body = Sheet(cubic: true);
        var options = new PK_BODY_section_o_s { o_t_version = 5, fence = 18202, default_tol = 1e-8 }; PK_section_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, plane, &options, &result));
        try
        {
            Assert.Equal(1, result.front_bodies.length); Assert.Equal(2, result.back_bodies.length);
            Assert.Equal(body, result.front_bodies.array[0]);
        }
        finally { KernelRuntime.SectionResultFree(&result); }
    }

    [Fact]
    public void ModernImprintTracksAllNewCurvedEdges()
    {
        var body = Sheet(); var plane = Plane(.25);
        var options = new PK_BODY_imprint_plane_o_s { o_t_version = 1, imprint_dir = ParasolidConstants.PK_imprint_dir_no_check_c };
        PK_TOPOL_track_r_s tracking = default;
        Assert.Equal(0, KernelRuntime.BodyImprintPlane2(body, plane, &options, &tracking));
        try
        {
            Assert.Equal(1, tracking.n_track_records); Assert.Equal(1, tracking.track_records[0].n_product_topols);
            Assert.Equal(ParasolidConstants.PK_TOPOL_track_create_c, tracking.track_records[0].track);
            Assert.Equal(0, tracking.track_records[0].n_original_topols);
        }
        finally { KernelRuntime.TopolTrackResultFree(&tracking); }
    }

    [Fact]
    public void StraightRulingUsesTheBoundedBranchRatherThanTheDefaultInfiniteLineDomain()
    {
        var body = Sheet(); var plane = Plane(.5, xNormal: true);
        var options = new PK_BODY_section_o_s { o_t_version = 5, fence = 18202, default_tol = 1e-8 }; PK_section_r_s result = default;
        Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, plane, &options, &result));
        try
        {
            Assert.Equal(1, result.front_bodies.length); Assert.Equal(1, result.back_bodies.length);
            BooleanOperationTests.Counts(result.front_bodies.array[0], 1, 4, 4);
            BooleanOperationTests.Counts(result.back_bodies.array[0], 1, 4, 4);
        }
        finally { KernelRuntime.SectionResultFree(&result); }
    }

    [Theory]
    [InlineData(18200, false)] [InlineData(18201, true)] [InlineData(18202, true)]
    public void MissingPlanePreservesAllOriginalHandlesOrConsumesBody(int fence, bool retained)
    {
        var body = Sheet(); var plane = Plane(2); int n = 0; int* oldEdges = null;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &n, &oldEdges));
        try
        {
            var options = new PK_BODY_section_o_s { o_t_version = 5, fence = fence, default_tol = 1e-8 }; PK_section_r_s result = default;
            Assert.Equal(0, KernelRuntime.BodySectionWithSurf(body, plane, &options, &result));
            try
            {
                Assert.Equal(retained, KernelRuntime.IsValidTag(body));
                Assert.Equal(retained ? 1 : 0, result.back_bodies.length);
                Assert.Equal(0, result.front_bodies.length);
                for (var i = 0; i < n; i++) Assert.Equal(retained, KernelRuntime.IsValidTag(oldEdges[i]));
            }
            finally { KernelRuntime.SectionResultFree(&result); }
        }
        finally { KernelRuntime.MemoryFree(oldEdges); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AllocationFailuresRestoreInputsAndReleaseTemporaryGeometry(bool section)
    {
        var success = false;
        for (var budget = 0; budget < 200; budget += 3)
        {
            var body = Sheet(); var plane = Plane(.25); var curves = KernelRuntime.Curves.AliveCount;
            var surfaces = KernelRuntime.Surfaces.AliveCount; var outstanding = KernelRuntime.ReturnOutstandingBlocks;
            int n = 0; int* edges = null; PK_section_r_s result = default;
            var options = new PK_BODY_section_o_s { o_t_version = 5, fence = 18202, default_tol = 1e-8 };
            KernelRuntime.InjectAllocationFailure(budget);
            var error = section ? KernelRuntime.BodySectionWithSurf(body, plane, &options, &result)
                : KernelRuntime.BodyImprintPlane(body, plane, 1e-8, &n, &edges);
            KernelRuntime.InjectAllocationFailure(-1);
            if (error != 0)
            {
                Assert.True(edges == null && result.front_bodies.array == null && result.back_bodies.array == null);
                BooleanOperationTests.Counts(body, 1, 4, 4);
                Assert.Equal(curves, KernelRuntime.Curves.AliveCount); Assert.Equal(surfaces, KernelRuntime.Surfaces.AliveCount);
            }
            else
            {
                success = true;
                if (section)
                {
                    Assert.Equal(0, KernelRuntime.EntityDelete(result.back_bodies.length, result.back_bodies.array));
                    KernelRuntime.SectionResultFree(&result);
                }
                else KernelRuntime.MemoryFree(edges);
            }
            Assert.Equal(outstanding, KernelRuntime.ReturnOutstandingBlocks);
            KernelRuntime.SessionStop(); var start = new PK_SESSION_start_o_s { o_t_version = 1 }; Assert.Equal(0, KernelRuntime.SessionStart(&start));
        }
        Assert.True(success);
    }

    private static int Sheet(bool saddle = false, bool cubic = false, bool crease = false, bool subdomain = false)
    {
        double* poles = stackalloc double[42]; double* heights = stackalloc double[7] { -.08, .14, -.14, .08, -.14, .14, -.08 };
        for (var u = 0; u < (crease ? 7 : cubic ? 4 : 2); u++) for (var v = 0; v < 2; v++)
        { var i = (u * 2 + v) * 3; poles[i] = cubic ? u / 3.0 : saddle ? 2 * u - 1 : u; poles[i + 1] = saddle ? 2 * v - 1 : v; poles[i + 2] = cubic ? heights[u] + v : poles[i] * poles[i + 1]; }
        double* knots = stackalloc double[3] { 0, 1, 2 }; int* mult = stackalloc int[2] { 2, 2 }; int* um = stackalloc int[3] { 4, crease ? 3 : 4, 4 };
        var definition = new PK_BSURF_sf_s { u_degree = cubic ? 3 : 1, v_degree = 1, n_u_vertices = crease ? 7 : cubic ? 4 : 2, n_v_vertices = 2, vertex_dim = 3,
            vertex = poles, n_u_knots = crease ? 3 : 2, n_v_knots = 2, u_knot = knots, v_knot = knots, u_knot_mult = cubic ? um : mult, v_knot_mult = mult,
            form = ParasolidConstants.PK_BSURF_form_arbitrary_c, u_knot_type = ParasolidConstants.PK_knot_non_uniform_c,
            v_knot_type = ParasolidConstants.PK_knot_non_uniform_c, self_intersecting = ParasolidConstants.PK_self_intersect_false_c,
            convexity = ParasolidConstants.PK_convexity_unset_c };
        int surface = 0; Assert.Equal(0, KernelRuntime.BSurfCreate(&definition, &surface));
        var box = new PK_UVBOX_s(); box.param[2] = crease ? 2 : subdomain ? .9 : 1; box.param[3] = 1;
        int body = 0; Assert.Equal(0, KernelRuntime.SurfMakeSheetBody(surface, box, &body)); return body;
    }
    private static int Plane(double z, bool xNormal = false)
    {
        var definition = new PK_PLANE_sf_s(); definition.basis_set.location.coord[2] = z;
        definition.basis_set.axis.coord[2] = 1; definition.basis_set.ref_direction.coord[0] = 1;
        if (xNormal)
        {
            definition.basis_set.location.coord[2] = 0; definition.basis_set.location.coord[0] = z;
            definition.basis_set.axis.coord[2] = 0; definition.basis_set.axis.coord[0] = 1;
            definition.basis_set.ref_direction.coord[0] = 0; definition.basis_set.ref_direction.coord[1] = 1;
        }
        int plane = 0; Assert.Equal(0, KernelRuntime.PlaneCreate(&definition, &plane)); return plane;
    }
}
