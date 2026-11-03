using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class GeometryPublicApiTests : IDisposable
{
    public GeometryPublicApiTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose()
    {
        KernelRuntime.InjectAllocationFailure(-1);
        KernelRuntime.SessionStop();
    }

    [Fact]
    public void PointAndEllipse_ExportsRoundTripStandardForms()
    {
        var pointSf = new PK_POINT_sf_s();
        pointSf.position.coord[0] = 2; pointSf.position.coord[1] = -3; pointSf.position.coord[2] = 5;
        int point;
        Assert.Equal(0, KernelRuntime.PointCreate(&pointSf, &point));
        delegate* unmanaged<int, PK_POINT_sf_s*, int> askPoint = &KernelExports.PK_POINT_ask;
        var pointResult = new PK_POINT_sf_s();
        Assert.Equal(0, askPoint(point, &pointResult));
        for (var axis = 0; axis < 3; axis++) Assert.Equal(pointSf.position.coord[axis], pointResult.position.coord[axis]);

        var sf = new PK_ELLIPSE_sf_s { basis_set = Axis(), R1 = 3, R2 = 1.5 };
        sf.basis_set.location.coord[0] = 4;
        delegate* unmanaged<PK_ELLIPSE_sf_s*, int*, int> create = &KernelExports.PK_ELLIPSE_create;
        delegate* unmanaged<int, PK_ELLIPSE_sf_s*, int> ask = &KernelExports.PK_ELLIPSE_ask;
        int ellipse;
        Assert.Equal(0, create(&sf, &ellipse));
        var result = new PK_ELLIPSE_sf_s();
        Assert.Equal(0, ask(ellipse, &result));
        Assert.Equal(3, result.R1); Assert.Equal(1.5, result.R2);
        Assert.Equal(4, result.basis_set.location.coord[0]);
        Assert.Equal(1, result.basis_set.axis.coord[2]);
        Assert.Equal(1, result.basis_set.ref_direction.coord[0]);
        AssertInterval(ellipse, 0, Math.Tau);
        sf.basis_set.axis.coord[2] = 2;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_a_unit_vector, create(&sf, &ellipse));
        Assert.Equal(0, ellipse);
    }

    [Fact]
    public void LegacyTrimmedCurveExport_UsesRealAbiAndPreservesNativeInterval()
    {
        var line = CreateLine();
        var start = -1.25; var end = 2.5;
        int trimmed = -1, error = -1;
        delegate* unmanaged<int*, double*, double*, int*, int*, void> create = &KernelExports.CRTRCU;
        delegate* unmanaged<int, PK_TRCURVE_sf_s*, int> ask = &KernelExports.PK_TRCURVE_ask;
        create(&line, &start, &end, &trimmed, &error);
        Assert.Equal(0, error);
        var sf = new PK_TRCURVE_sf_s();
        Assert.Equal(0, ask(trimmed, &sf));
        Assert.Equal(line, sf.basis_curve);
        Assert.Equal(start, sf.t_int.value[0]); Assert.Equal(end, sf.t_int.value[1]);
        AssertInterval(trimmed, 0, end - start);
        var reversed = -1;
        create(&line, &end, &start, &reversed, &error);
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter, error);
        Assert.Equal(0, reversed);
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &line));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &trimmed));
        Assert.False(KernelRuntime.IsValidTag(line));
    }

    [Fact]
    public void BCurveAsk_IndependentArraysSurviveSourceDeletionAndPreserveMetadata()
    {
        var curve = CreateBCurve(rational: true);
        var before = KernelRuntime.ReturnOutstandingBlocks;
        delegate* unmanaged<int, PK_BCURVE_sf_s*, int> ask = &KernelExports.PK_BCURVE_ask;
        var result = new PK_BCURVE_sf_s();
        Assert.Equal(0, ask(curve, &result));
        try
        {
            Assert.Equal(1, result.degree); Assert.Equal(2, result.n_vertices);
            Assert.Equal(4, result.vertex_dim); Assert.Equal((byte)1, result.is_rational);
            Assert.Equal(ParasolidConstants.PK_BCURVE_form_arbitrary_c, result.form);
            Assert.Equal(ParasolidConstants.PK_knot_unset_c, result.knot_type);
            Assert.Equal(ParasolidConstants.PK_self_intersect_false_c, result.self_intersecting);
            Assert.Equal((byte)0, result.is_periodic); Assert.Equal((byte)0, result.is_closed);
            Assert.Equal(2, result.n_knots); Assert.Equal(2, result.knot_mult[0]); Assert.Equal(2, result.knot_mult[1]);
            Assert.Equal(3, result.knot[0]); Assert.Equal(7, result.knot[1]);
            Assert.Equal(2, result.vertex[7]);
            result.vertex[0] = 900;
            var value = new PK_VECTOR_s();
            Assert.Equal(0, KernelRuntime.CurveEval(curve, 3, 0, &value));
            Assert.Equal(0, value.coord[0]);
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &curve));
            Assert.Equal(900, result.vertex[0]); Assert.Equal(7, result.knot[1]);
        }
        finally { Free(result); }
        Assert.Equal(before, KernelRuntime.ReturnOutstandingBlocks);
    }

    [Fact]
    public void BSurfAsk_IndependentFiveArraysAndNaturalKnotDomain()
    {
        var surface = CreateBSurface();
        var before = KernelRuntime.ReturnOutstandingBlocks;
        delegate* unmanaged<int, PK_BSURF_sf_s*, int> ask = &KernelExports.PK_BSURF_ask;
        var result = new PK_BSURF_sf_s();
        Assert.Equal(0, ask(surface, &result));
        try
        {
            Assert.Equal(1, result.u_degree); Assert.Equal(1, result.v_degree);
            Assert.Equal(2, result.n_u_vertices); Assert.Equal(2, result.n_v_vertices);
            Assert.Equal(3, result.vertex_dim); Assert.Equal((byte)0, result.is_rational);
            Assert.Equal(ParasolidConstants.PK_BSURF_form_unset_c, result.form);
            Assert.Equal(ParasolidConstants.PK_convexity_arbitrary_c, result.convexity);
            Assert.Equal(ParasolidConstants.PK_self_intersect_false_c, result.self_intersecting);
            Assert.Equal(2, result.n_u_knots); Assert.Equal(2, result.n_v_knots);
            Assert.Equal(ParasolidConstants.PK_knot_unset_c, result.u_knot_type); Assert.Equal(ParasolidConstants.PK_knot_unset_c, result.v_knot_type);
            Assert.Equal(2, result.u_knot_mult[0]); Assert.Equal(2, result.v_knot_mult[1]);
            Assert.Equal(2, result.u_knot[0]); Assert.Equal(4, result.u_knot[1]);
            Assert.Equal(-2, result.v_knot[0]); Assert.Equal(3, result.v_knot[1]);
            AssertBox(surface, 2, -2, 4, 3);
            result.vertex[0] = 800;
            var uv = new PK_UV_s(); uv.param[0] = 2; uv.param[1] = -2;
            var point = new PK_VECTOR_s();
            Assert.Equal(0, KernelRuntime.SurfEval(surface, uv, 0, 0, 0, &point));
            Assert.Equal(0, point.coord[0]);
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &surface));
            Assert.Equal(800, result.vertex[0]); Assert.Equal(3, result.v_knot[1]);
        }
        finally { Free(result); }
        Assert.Equal(before, KernelRuntime.ReturnOutstandingBlocks);
    }

    [Fact]
    public void BSurfaceCreate_RejectsOverflowingScalarCountBeforeWalkingInputBuffers()
    {
        var poles = stackalloc double[4] { 0, 0, 0, 1 };
        var knots = stackalloc double[2] { 0, 1 }; var mult = stackalloc int[2] { 2, 2 };
        var definition = new PK_BSURF_sf_s { u_degree = 1, v_degree = 1, n_u_vertices = int.MaxValue,
            n_v_vertices = int.MaxValue, vertex_dim = 4, is_rational = 1, vertex = poles,
            n_u_knots = 2, n_v_knots = 2, u_knot = knots, v_knot = knots, u_knot_mult = mult, v_knot_mult = mult };
        int surface = -1;
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_parameter, KernelRuntime.BSurfCreate(&definition, &surface));
        Assert.Equal(0, surface); Assert.Equal(0, KernelRuntime.BSurfaceDataStore.AliveCount);
    }

    [Fact]
    public void DependentCreateAskExports_PreserveInputIdentityAndDomains()
    {
        var curve = CreateBCurve();
        var sweptSf = new PK_SWEPT_sf_s { curve = curve };
        sweptSf.direction.coord[2] = 1;
        delegate* unmanaged<PK_SWEPT_sf_s*, int*, int> createSwept = &KernelExports.PK_SWEPT_create;
        delegate* unmanaged<int, PK_SWEPT_sf_s*, int> askSwept = &KernelExports.PK_SWEPT_ask;
        int swept;
        Assert.Equal(0, createSwept(&sweptSf, &swept));
        var sweptResult = new PK_SWEPT_sf_s();
        Assert.Equal(0, askSwept(swept, &sweptResult));
        Assert.Equal(curve, sweptResult.curve); Assert.Equal(1, sweptResult.direction.coord[2]);
        AssertBox(swept, 3, -Math.BitIncrement(1e4), 7, Math.BitIncrement(1e4));

        var spunSf = new PK_SPUN_sf_s { curve = curve }; spunSf.axis.axis.coord[2] = 1;
        delegate* unmanaged<PK_SPUN_sf_s*, int*, int> createSpun = &KernelExports.PK_SPUN_create;
        delegate* unmanaged<int, PK_SPUN_sf_s*, int> askSpun = &KernelExports.PK_SPUN_ask;
        int spun;
        Assert.Equal(0, createSpun(&spunSf, &spun));
        var spunResult = new PK_SPUN_sf_s();
        Assert.Equal(0, askSpun(spun, &spunResult));
        Assert.Equal(curve, spunResult.curve); Assert.Equal(1, spunResult.axis.axis.coord[2]);
        AssertBox(spun, 3, 0, 7, Math.Tau);

        var baseSurface = CreateBSurface();
        var offsetSf = new PK_OFFSET_sf_s { underlying_surface = baseSurface, offset_distance = .25 };
        delegate* unmanaged<PK_OFFSET_sf_s*, int*, int> createOffset = &KernelExports.PK_OFFSET_create;
        delegate* unmanaged<int, PK_OFFSET_sf_s*, int> askOffset = &KernelExports.PK_OFFSET_ask;
        int offset;
        Assert.Equal(0, createOffset(&offsetSf, &offset));
        var offsetResult = new PK_OFFSET_sf_s();
        Assert.Equal(0, askOffset(offset, &offsetResult));
        Assert.Equal(baseSurface, offsetResult.underlying_surface); Assert.Equal(.25, offsetResult.offset_distance);
        AssertBox(offset, 2, -2, 4, 3);
    }

    [Fact]
    public void SpCurvePublicApi_EmbedsTwoDimensionalCurveAndRejectsWrongClassSafely()
    {
        var planeSf = new PK_PLANE_sf_s { basis_set = Axis() };
        int plane; Assert.Equal(0, KernelRuntime.PlaneCreate(&planeSf, &plane));
        var uvCurve = CreateBCurve(planar: true);
        var sf = new PK_SPCURVE_sf_s { surf = plane, curve = uvCurve };
        delegate* unmanaged<PK_SPCURVE_sf_s*, int*, int> create = &KernelExports.PK_SPCURVE_create;
        delegate* unmanaged<int, PK_SPCURVE_sf_s*, int> ask = &KernelExports.PK_SPCURVE_ask;
        int curve;
        Assert.Equal(0, create(&sf, &curve));
        var result = new PK_SPCURVE_sf_s();
        Assert.Equal(0, ask(curve, &result));
        Assert.Equal(plane, result.surf); Assert.Equal(uvCurve, result.curve);
        AssertInterval(curve, 3, 7);
        var point = new PK_VECTOR_s();
        Assert.Equal(0, KernelRuntime.CurveEval(curve, 5, 0, &point));
        Assert.Equal(.5, point.coord[0], 12); Assert.Equal(.25, point.coord[1], 12);
        sf.curve = CreateLine();
        Assert.Equal(ParasolidConstants.PK_ERROR_bad_dimension, create(&sf, &curve));
        Assert.Equal(0, curve);
    }

    [Theory]
    [InlineData("plane")]
    [InlineData("cylinder")]
    [InlineData("cone")]
    [InlineData("sphere")]
    [InlineData("torus")]
    public void AnalyticSurfaceQueries_ReportNaturalDomains(string kind)
    {
        var axis = Axis(); int surface = 0; var limit = Math.BitIncrement(1e4);
        switch (kind)
        {
            case "plane":
                var plane = new PK_PLANE_sf_s { basis_set = axis };
                Assert.Equal(0, KernelRuntime.PlaneCreate(&plane, &surface));
                AssertBox(surface, -limit, -limit, limit, limit); break;
            case "cylinder":
                var cylinder = new PK_CYL_sf_s { basis_set = axis, radius = 2 };
                Assert.Equal(0, KernelRuntime.CylCreate(&cylinder, &surface));
                AssertBox(surface, 0, -limit, Math.Tau, limit); break;
            case "cone":
                var cone = new PK_CONE_sf_s { basis_set = axis, radius = 2, semi_angle = Math.PI / 6 };
                Assert.Equal(0, KernelRuntime.ConeCreate(&cone, &surface));
                AssertBox(surface, 0, Math.BitDecrement(-2 / Math.Tan(Math.PI / 6)), Math.Tau, limit); break;
            case "sphere":
                var sphere = new PK_SPHERE_sf_s { basis_set = axis, radius = 2 };
                Assert.Equal(0, KernelRuntime.SphereCreate(&sphere, &surface));
                AssertBox(surface, 0, -Math.PI / 2, Math.Tau, Math.PI / 2); break;
            case "torus":
                var torus = new PK_TORUS_sf_s { basis_set = axis, major_radius = 4, minor_radius = 1 };
                Assert.Equal(0, KernelRuntime.TorusCreate(&torus, &surface));
                AssertBox(surface, 0, -Math.PI, Math.Tau, Math.PI); break;
        }
    }

    [Fact]
    public void SharedDependentOwners_ProtectSourceAndReleaseItAfterLastOwner()
    {
        var line = CreateLine();
        var first = CreateSwept(line); var second = CreateSwept(line);
        Assert.Equal(2, KernelRuntime.GetCurveByTag(line).GeometricOwnerCount);
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &line));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &first));
        Assert.True(KernelRuntime.IsValidTag(line));
        Assert.Equal(1, KernelRuntime.GetCurveByTag(line).GeometricOwnerCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &second));
        Assert.False(KernelRuntime.IsValidTag(line));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void UndoOfDependentCreation_PreservesPreexistingSourceAndEarlierOwner()
    {
        var line = CreateLine(); var first = CreateSwept(line);
        int mark; Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        var second = CreateSwept(line);
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.False(KernelRuntime.IsValidTag(second));
        Assert.True(KernelRuntime.IsValidTag(line)); Assert.True(KernelRuntime.IsValidTag(first));
        Assert.Equal(1, KernelRuntime.GetCurveByTag(line).GeometricOwnerCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &first));
        Assert.False(KernelRuntime.IsValidTag(line));

        var standalone = CreateLine();
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        var owner = CreateSwept(standalone);
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.False(KernelRuntime.IsValidTag(owner)); Assert.True(KernelRuntime.IsValidTag(standalone));
        Assert.Equal(0, KernelRuntime.GetCurveByTag(standalone).GeometricOwnerCount);
    }

    [Fact]
    public void BodyDeletion_WithSharedEdgeProfileRestoresOnUndoAndReleasesAtCommit()
    {
        int body; Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var edge = KernelRuntime.Bodies[bodySlot].FirstEdgeBody;
        var profile = KernelRuntime.Edges[edge].CurveTag;
        var surface = CreateSwept(profile);
        var faceSlot = KernelRuntime.Bodies[bodySlot].FirstFaceBody;
        var face = KernelRuntime.TagOf(PoolKind.Face, faceSlot);
        var oldSurface = KernelRuntime.Faces[faceSlot].SurfTag;
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face));
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &face, &surface, &sense));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &oldSurface));
        Assert.Equal(1, KernelRuntime.GetCurveByTag(profile).OwnerCount);
        Assert.Equal(1, KernelRuntime.GetCurveByTag(profile).GeometricOwnerCount);
        int mark; Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.True(KernelRuntime.IsValidTag(profile)); Assert.True(KernelRuntime.IsValidTag(surface));
        Assert.Equal(1, KernelRuntime.GetCurveByTag(profile).OwnerCount);
        Assert.Equal(1, KernelRuntime.GetCurveByTag(profile).GeometricOwnerCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(profile)); Assert.False(KernelRuntime.IsValidTag(surface));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(0)] // a surface already attached to the first body
    [InlineData(1)] // a new surface whose profile is an edge of the first body
    [InlineData(2)] // two surfaces share an unattached profile; one already belongs to the first body
    [InlineData(3)] // the two bodies are assigned shared defining geometry in one batch
    public void GeometryAttachment_RejectsCrossBodyDependenciesWithoutMutation(int arrangement)
    {
        int bodyA, bodyB;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &bodyA));
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &bodyB));
        Assert.True(KernelRuntime.TryResolveBodySlot(bodyA, out var slotA));
        Assert.True(KernelRuntime.TryResolveBodySlot(bodyB, out var slotB));
        var faceSlotA = KernelRuntime.Bodies[slotA].FirstFaceBody;
        var faceSlotB = KernelRuntime.Bodies[slotB].FirstFaceBody;
        var faceA = KernelRuntime.TagOf(PoolKind.Face, faceSlotA);
        var faceB = KernelRuntime.TagOf(PoolKind.Face, faceSlotB);
        var firstSurface = KernelRuntime.Faces[faceSlotA].SurfTag;
        var surface = firstSurface;
        var profile = 0;
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(faceB));
        if (arrangement == 1)
        {
            profile = KernelRuntime.Edges[KernelRuntime.Bodies[slotA].FirstEdgeBody].CurveTag;
            surface = CreateSwept(profile);
        }
        else if (arrangement >= 2)
        {
            profile = CreateBCurve();
            firstSurface = CreateSwept(profile);
            Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(faceA));
            if (arrangement == 2)
                Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &faceA, &firstSurface, &sense));
            surface = CreateSwept(profile);
        }
        var sourceOwners = profile == 0 ? 0 : KernelRuntime.GetCurveByTag(profile).GeometricOwnerCount;
        var originalA = KernelRuntime.Faces[faceSlotA].SurfTag;
        var originalSurfaceOwners = KernelRuntime.GetSurfaceByTag(surface).OwnerCount;
        var faces = stackalloc int[2] { faceA, faceB };
        var surfaces = stackalloc int[2] { firstSurface, surface };
        var senses = stackalloc byte[2] { 1, 1 };
        var error = arrangement == 3
            ? KernelRuntime.FaceAttachSurfaces(2, faces, surfaces, senses)
            : KernelRuntime.FaceAttachSurfaces(1, &faceB, &surface, &sense);
        Assert.Equal(arrangement == 3 ? ParasolidConstants.PK_ERROR_bad_shared_entity : ParasolidConstants.PK_ERROR_has_parent, error);
        Assert.Equal(originalA, KernelRuntime.Faces[faceSlotA].SurfTag);
        Assert.Equal(0, KernelRuntime.Faces[faceSlotB].SurfTag);
        Assert.Equal(originalSurfaceOwners, KernelRuntime.GetSurfaceByTag(surface).OwnerCount);
        if (profile != 0) Assert.Equal(sourceOwners, KernelRuntime.GetCurveByTag(profile).GeometricOwnerCount);
    }

    [Theory]
    [InlineData(true, 0)] [InlineData(false, 0)]
    [InlineData(true, 1)] [InlineData(false, 1)]
    [InlineData(true, 2)] [InlineData(false, 2)]
    [InlineData(true, 3)] [InlineData(false, 3)]
    [InlineData(true, 4)] [InlineData(false, 4)]
    [InlineData(true, 5)] [InlineData(false, 5)]
    [InlineData(true, 6)] [InlineData(false, 6)]
    public void ClampedPeriodicBSurface_ProvesWholeRationalSeam(bool alongU, int defect)
    {
        var poles = stackalloc double[108];
        var weights = stackalloc double[3] { 1, .5, 2 };
        for (var i = 0; i < 9; i++)
        {
            var cosine = i == 8 ? 1 : Math.Cos(i * Math.PI / 4);
            var sine = i == 8 ? 0 : Math.Sin(i * Math.PI / 4);
            var pathWeight = (i & 1) == 0 ? 1 : Math.Sqrt(.5);
            for (var j = 0; j < 3; j++)
            {
                var offset = (alongU ? i * 3 + j : j * 9 + i) * 4;
                poles[offset] = weights[j] * (j + 2) * cosine;
                poles[offset + 1] = weights[j] * (j + 2) * sine;
                poles[offset + 2] = weights[j] * (j == 1 ? 1.5 : 0) * pathWeight;
                poles[offset + 3] = weights[j] * pathWeight;
            }
        }
        if (defect >= 4)
            for (var offset = 0; offset < 108; offset += 4) poles[offset] += 1e6 * poles[offset + 3];
        if (defect != 0 && defect != 6)
        {
            var row = defect is 2 or 5 ? 7 : 8;
            var offset = (alongU ? row * 3 + 1 : 9 + row) * 4;
            poles[offset + (defect == 3 ? 3 : 0)] += defect >= 4 ? 1e-7 : .01;
        }
        var pathKnots = stackalloc double[5] { 0, .25, .5, .75, 1 };
        var pathMults = stackalloc int[5] { 3, 2, 2, 2, 3 };
        var profileKnots = stackalloc double[2] { 0, 1 };
        var profileMults = stackalloc int[2] { 3, 3 };
        var sf = new PK_BSURF_sf_s
        {
            u_degree = 2, v_degree = 2, n_u_vertices = alongU ? 9 : 3, n_v_vertices = alongU ? 3 : 9,
            vertex_dim = 4, is_rational = 1, vertex = poles,
            n_u_knots = alongU ? 5 : 2, n_v_knots = alongU ? 2 : 5,
            u_knot = alongU ? pathKnots : profileKnots, v_knot = alongU ? profileKnots : pathKnots,
            u_knot_mult = alongU ? pathMults : profileMults, v_knot_mult = alongU ? profileMults : pathMults,
            is_u_periodic = (byte)(alongU ? 1 : 0), is_v_periodic = (byte)(alongU ? 0 : 1),
            is_u_closed = (byte)(alongU ? 1 : 0), is_v_closed = (byte)(alongU ? 0 : 1),
            form = ParasolidConstants.PK_BSURF_form_arbitrary_c,
            u_knot_type = ParasolidConstants.PK_knot_non_uniform_c, v_knot_type = ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c, convexity = ParasolidConstants.PK_convexity_unset_c,
        };
        int surface = -1;
        var error = KernelRuntime.BSurfCreate(&sf, &surface);
        if (defect is not (0 or 6))
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_periodic_not_smooth, error);
            Assert.Equal(0, surface); Assert.Equal(0, KernelRuntime.BSurfaceDataStore.AliveCount);
            return;
        }
        Assert.Equal(0, error);
        // Use exactly representable parameters for periodic reuse. With .37,
        // adding then reducing one period gives .3700000000000001 instead;
        // those different inputs also produce different high-order jets in
        // real PK after a large homogeneous-coordinate translation.
        var first = new PK_UV_s(); first.param[0] = alongU ? .375 : .4375; first.param[1] = alongU ? .4375 : .375;
        var second = first; second.param[alongU ? 0 : 1] += 1;
        var a = stackalloc PK_VECTOR_s[9]; var b = stackalloc PK_VECTOR_s[9];
        Assert.Equal(0, KernelRuntime.SurfEval(surface, first, 2, 2, 0, a));
        Assert.Equal(0, KernelRuntime.SurfEval(surface, second, 2, 2, 0, b));
        Assert.Equal(first.param[alongU ? 0 : 1], second.param[alongU ? 0 : 1] % 1);
        for (var i = 0; i < 9; i++)
            for (var axis = 0; axis < 3; axis++) Assert.True(Math.Abs(a[i].coord[axis] - b[i].coord[axis]) <= 2e-11 * Math.Max(1, Math.Abs(a[i].coord[axis])),
                $"Periodic reuse differs at jet {i}, component {axis}: {a[i].coord[axis]:R} / {b[i].coord[axis]:R}.");
    }

    [Fact]
    public void FinPcurveDependencyChain_ReleasesSupportSharedWithFaceOnBodyDelete()
    {
        int body; Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var face = KernelRuntime.Bodies[bodySlot].FirstFaceBody;
        var surface = KernelRuntime.Faces[face].SurfTag;
        var loop = KernelRuntime.Faces[face].FirstLoop;
        var fin = KernelRuntime.Loops[loop].FirstFin;
        var uvCurve = CreateBCurve(planar: true);
        var spSf = new PK_SPCURVE_sf_s { surf = surface, curve = uvCurve };
        int sp; Assert.Equal(0, KernelRuntime.SpCurveCreate(&spSf, &sp));
        var trSf = new PK_TRCURVE_sf_s { basis_curve = sp };
        trSf.t_int.value[0] = 3; trSf.t_int.value[1] = 7;
        int tr; Assert.Equal(0, KernelRuntime.TrCurveCreate(&trSf, &tr));
        Assert.Equal(0, KernelRuntime.AcquireFinCurveReference(fin, tr));
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &tr));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.False(KernelRuntime.IsValidTag(tr)); Assert.False(KernelRuntime.IsValidTag(sp));
        Assert.False(KernelRuntime.IsValidTag(uvCurve)); Assert.False(KernelRuntime.IsValidTag(surface));
        Assert.Equal(0, KernelRuntime.Curves.AliveCount); Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(5)] [InlineData(8)]
    public void FailedDependentCreation_DoesNotAcquireOrDeleteItsSource(int fault)
    {
        var curve = CreateLine();
        var sf = new PK_SWEPT_sf_s { curve = curve }; sf.direction.coord[2] = 1;
        int surface = 0, error;
        KernelRuntime.InjectAllocationFailure(fault);
        try { error = KernelRuntime.SweptCreate(&sf, &surface); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0)
        {
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &surface));
            Assert.False(KernelRuntime.IsValidTag(curve));
        }
        else
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, error); Assert.Equal(0, surface);
            Assert.True(KernelRuntime.IsValidTag(curve));
            Assert.Equal(0, KernelRuntime.GetCurveByTag(curve).GeometricOwnerCount);
            Assert.Equal(1, KernelRuntime.Curves.AliveCount);
        }
        Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(8)]
    public void FailedArrayAsk_PublishesNothingAndReleasesAllPartialResults(int fault)
    {
        var surface = CreateBSurface();
        var beforeBytes = KernelRuntime.ReturnOutstandingBytes;
        var beforeBlocks = KernelRuntime.ReturnOutstandingBlocks;
        var result = new PK_BSURF_sf_s { n_u_vertices = -7 };
        int error;
        KernelRuntime.InjectAllocationFailure(fault);
        try { error = KernelRuntime.BSurfAsk(surface, &result); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0) Free(result);
        else
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, error);
            Assert.Equal(-7, result.n_u_vertices);
            Assert.True(result.vertex == null && result.u_knot == null && result.v_knot == null
                && result.u_knot_mult == null && result.v_knot_mult == null);
        }
        Assert.True(KernelRuntime.IsValidTag(surface));
        Assert.Equal(beforeBytes, KernelRuntime.ReturnOutstandingBytes);
        Assert.Equal(beforeBlocks, KernelRuntime.ReturnOutstandingBlocks);
    }

    [Fact]
    public void Dependencies_RejectCrossPartitionBeforeMutation()
    {
        var curve = CreateLine();
        int partition; Assert.Equal(0, KernelRuntime.PartitionCreateEmpty(&partition));
        Assert.Equal(0, KernelRuntime.PartitionSetCurrent(partition));
        var sf = new PK_SWEPT_sf_s { curve = curve }; sf.direction.coord[2] = 1;
        int swept;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_in_same_partition, KernelRuntime.SweptCreate(&sf, &swept));
        Assert.Equal(0, swept); Assert.Equal(0, KernelRuntime.GetCurveByTag(curve).GeometricOwnerCount);
    }

    private static PK_AXIS2_sf_s Axis()
    {
        var axis = new PK_AXIS2_sf_s(); axis.axis.coord[2] = 1; axis.ref_direction.coord[0] = 1;
        return axis;
    }

    private static int CreateLine()
    {
        var sf = new PK_LINE_sf_s(); sf.basis_set.location.coord[0] = 2; sf.basis_set.axis.coord[0] = 1;
        int line; Assert.Equal(0, KernelRuntime.LineCreate(&sf, &line)); return line;
    }

    private static int CreateSwept(int curve)
    {
        var sf = new PK_SWEPT_sf_s { curve = curve }; sf.direction.coord[2] = 1;
        int surface; Assert.Equal(0, KernelRuntime.SweptCreate(&sf, &surface)); return surface;
    }

    private static int CreateBCurve(bool planar = false, bool rational = false)
    {
        double* poles = stackalloc double[8];
        new Span<double>(poles, 8).Clear();
        var dimension = planar ? 2 : rational ? 4 : 3;
        poles[0] = 0; poles[1] = 0;
        if (rational) { poles[2] = 0; poles[3] = 1; poles[4] = 2; poles[5] = 1; poles[6] = 0; poles[7] = 2; }
        else { poles[dimension] = 1; poles[dimension + 1] = .5; }
        double* knots = stackalloc double[2] { 3, 7 };
        int* multiplicities = stackalloc int[2] { 2, 2 };
        var sf = new PK_BCURVE_sf_s
        {
            degree = 1, n_vertices = 2, vertex_dim = dimension, is_rational = (byte)(rational ? 1 : 0),
            vertex = poles, form = ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            n_knots = 2, knot = knots, knot_mult = multiplicities,
            knot_type = ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c,
        };
        int curve; Assert.Equal(0, KernelRuntime.BCurveCreate(&sf, &curve)); return curve;
    }

    private static int CreateBSurface()
    {
        double* poles = stackalloc double[12] { 0,0,0, 1,0,.3, 0,1,.3, 1,1,0 };
        double* uKnots = stackalloc double[2] { 2, 4 };
        double* vKnots = stackalloc double[2] { -2, 3 };
        int* multiplicities = stackalloc int[2] { 2, 2 };
        var sf = new PK_BSURF_sf_s
        {
            u_degree = 1, v_degree = 1, n_u_vertices = 2, n_v_vertices = 2, vertex_dim = 3, vertex = poles,
            form = ParasolidConstants.PK_BSURF_form_arbitrary_c,
            n_u_knots = 2, n_v_knots = 2, u_knot = uKnots, v_knot = vKnots,
            u_knot_mult = multiplicities, v_knot_mult = multiplicities,
            u_knot_type = ParasolidConstants.PK_knot_non_uniform_c, v_knot_type = ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c, convexity = ParasolidConstants.PK_convexity_arbitrary_c,
        };
        delegate* unmanaged<PK_BSURF_sf_s*, int*, int> create = &KernelExports.PK_BSURF_create;
        int surface; Assert.Equal(0, create(&sf, &surface)); return surface;
    }

    private static void AssertInterval(int curve, double start, double end)
    {
        delegate* unmanaged<int, PK_INTERVAL_s*, int> ask = &KernelExports.PK_CURVE_ask_interval;
        var interval = new PK_INTERVAL_s(); Assert.Equal(0, ask(curve, &interval));
        Assert.Equal(start, interval.value[0], 12); Assert.Equal(end, interval.value[1], 12);
    }

    private static void AssertBox(int surface, double u0, double v0, double u1, double v1)
    {
        delegate* unmanaged<int, PK_UVBOX_s*, int> ask = &KernelExports.PK_SURF_ask_uvbox;
        var box = new PK_UVBOX_s(); Assert.Equal(0, ask(surface, &box));
        Assert.Equal(u0, box.param[0], 12); Assert.Equal(v0, box.param[1], 12);
        Assert.Equal(u1, box.param[2], 12); Assert.Equal(v1, box.param[3], 12);
    }

    private static void Free(PK_BCURVE_sf_s result)
    {
        Assert.Equal(0, KernelRuntime.MemoryFree(result.vertex));
        Assert.Equal(0, KernelRuntime.MemoryFree(result.knot));
        Assert.Equal(0, KernelRuntime.MemoryFree(result.knot_mult));
    }

    private static void Free(PK_BSURF_sf_s result)
    {
        Assert.Equal(0, KernelRuntime.MemoryFree(result.vertex));
        Assert.Equal(0, KernelRuntime.MemoryFree(result.u_knot)); Assert.Equal(0, KernelRuntime.MemoryFree(result.v_knot));
        Assert.Equal(0, KernelRuntime.MemoryFree(result.u_knot_mult)); Assert.Equal(0, KernelRuntime.MemoryFree(result.v_knot_mult));
    }
}
