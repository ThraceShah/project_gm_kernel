using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using Xunit;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class FreeformModelingTests : IDisposable
{
    public FreeformModelingTests()
    {
        KernelRuntime.SessionStop(); var start = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&start));
    }
    public void Dispose() { KernelRuntime.InjectAllocationFailure(-1); KernelRuntime.SessionStop(); }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void SplineInterpolatesNonuniformPositionsAndOwnsReturnedMemory(int degree)
    {
        PK_VECTOR_s* points = stackalloc PK_VECTOR_s[5];
        double* parameters = stackalloc double[5] { -2, -.8, .1, 1.4, 3 };
        int* indices = stackalloc int[5] { 0, 1, 2, 3, 4 };
        for (var i = 0; i < 5; i++) Set(points + i, i, Math.Sin(i), .2 * i * i);
        var options = SplineOptions(); options.degree = degree; options.n_parameters = 5; options.parameters = parameters; options.param_indices = indices;
        options.param_interval.value[0] = -2; options.param_interval.value[1] = 3;
        var output = new PK_BCURVE_spline_r_s();
        Assert.Equal(0, KernelRuntime.BCurveCreateSpline2(5, points, &options, &output));
        Assert.Equal(ParasolidConstants.PK_BCURVE_spline_ok_c, output.fault); Assert.Equal(1, output.n_bcurves);
        var curve = output.bcurves[0].bcurve;
        for (var i = 0; i < 5; i++)
        {
            var actual = EvalCurve(curve, parameters[i]);
            for (var d = 0; d < 3; d++) Assert.Equal(points[i].coord[d], actual.coord[d], 10);
        }
        Assert.Equal(0, KernelRuntime.BCurveSplineResultFree(&output));
        Assert.Equal(0, output.n_bcurves); Assert.True(output.bcurves == null);
        Assert.Equal(0, KernelRuntime.BCurveSplineResultFree(&output));
        var afterFree = EvalCurve(curve, parameters[2]);
        Assert.Equal(points[2].coord[0], afterFree.coord[0], 10);
    }

    [Fact]
    public void SplineEndpointDerivativesAreHonoredAndBadParameterOrderRollsBack()
    {
        PK_VECTOR_s* points = stackalloc PK_VECTOR_s[4];
        for (var i = 0; i < 4; i++) Set(points + i, i, i == 1 ? 1 : 0, 0);
        double* parameters = stackalloc double[4] { 0, 1, 2, 3 }; int* indices = stackalloc int[4] { 0, 1, 2, 3 };
        PK_VECTOR_s* derivatives = stackalloc PK_VECTOR_s[2]; Set(derivatives, 1, 2, 0); Set(derivatives + 1, 1, -1, 0);
        int* orders = stackalloc int[2] { 1, 1 }; int* derivativeIndices = stackalloc int[2] { 0, 3 };
        var options = SplineOptions(); options.param_interval.value[1] = 3; options.n_parameters = 4; options.parameters = parameters; options.param_indices = indices;
        options.n_derivs = 2; options.derivs = derivatives; options.deriv_orders = orders; options.deriv_indices = derivativeIndices;
        var output = new PK_BCURVE_spline_r_s(); Assert.Equal(0, KernelRuntime.BCurveCreateSpline2(4, points, &options, &output));
        PK_VECTOR_s* jet = stackalloc PK_VECTOR_s[2];
        for (var end = 0; end < 2; end++)
        {
            Assert.Equal(0, KernelRuntime.CurveEval(output.bcurves[0].bcurve, end * 3, 1, jet));
            for (var d = 0; d < 3; d++) Assert.Equal(derivatives[end].coord[d], jet[1].coord[d], 11);
        }
        KernelRuntime.BCurveSplineResultFree(&output); var before = KernelRuntime.Curves.AliveCount;
        parameters[2] = .5;
        Assert.NotEqual(0, KernelRuntime.BCurveCreateSpline2(4, points, &options, &output));
        Assert.Equal(0, output.n_bcurves); Assert.Equal(before, KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void MatchedCurvesPreserveShapeAcrossDegreeAndKnotDifferences()
    {
        var line = Curve(1, new double[] { 0, 0, 0, 3, 2, 1 }, new double[] { 0, 1 }, new[] { 2, 2 });
        var quadratic = Curve(2, new double[] { 0, 0, 0, .5, 2, 0, 1.5, -1, .5, 3, 2, 1 }, new double[] { 0, .4, 1 }, new[] { 3, 1, 3 });
        int* inputs = stackalloc int[2] { line, quadratic }; int* output = stackalloc int[2];
        var options = new PK_BCURVE_make_matched_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.BCurveMakeMatched(2, inputs, &options, output));
        for (var c = 0; c < 2; c++)
            for (var i = 0; i <= 20; i++)
            {
                var original = EvalCurve(inputs[c], i / 20.0); var matched = EvalCurve(output[c], i / 20.0);
                for (var d = 0; d < 3; d++) Assert.Equal(original.coord[d], matched.coord[d], 11);
            }
        var first = KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(output[0]).DataIndex];
        var second = KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(output[1]).DataIndex];
        Assert.Equal(first.Degree, second.Degree); Assert.True(first.KnotsSpan().SequenceEqual(second.KnotsSpan()));
        Assert.True(first.KnotMultsSpan().SequenceEqual(second.KnotMultsSpan()));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SweepAndIsoparamsPreserveRationalGeometry(bool rational)
    {
        var curve = rational ? Curve(2, new double[] { 0, 0, 0, 1, .3, .8, .2, .6, 2, 0, .4, 1 }, new double[] { 0, 1 }, new[] { 3, 3 }, true)
            : Curve(2, new double[] { 0, 0, 0, 1, 2, .5, 2, 0, .4 }, new double[] { 0, 1 }, new[] { 3, 3 });
        var path = new PK_VECTOR_s(); Set(&path, .7, -.3, 3);
        int surface = 0; Assert.Equal(0, KernelRuntime.BCurveSweep(curve, path, &surface));
        for (var i = 0; i <= 8; i++)
        {
            var u = i / 8.0; var source = EvalCurve(curve, u); var actual = EvalSurface(surface, u, .37);
            for (var d = 0; d < 3; d++) Assert.Equal(source.coord[d] + .37 * path.coord[d], actual.coord[d], 11);
        }
        int alongV = 0, alongU = 0;
        Assert.Equal(0, KernelRuntime.BSurfMakeBCurveUIsoparam(surface, .3, &alongV));
        Assert.Equal(0, KernelRuntime.BSurfMakeBCurveVIsoparam(surface, .7, &alongU));
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8.0; var a = EvalCurve(alongV, t); var b = EvalSurface(surface, .3, t);
            var c = EvalCurve(alongU, t); var d = EvalSurface(surface, t, .7);
            for (var k = 0; k < 3; k++) { Assert.Equal(a.coord[k], b.coord[k], 11); Assert.Equal(c.coord[k], d.coord[k], 11); }
        }
        Assert.NotEqual(0, KernelRuntime.BSurfMakeBCurveUIsoparam(surface, 1.1, &alongV)); Assert.Equal(0, alongV);
    }

    [Fact]
    public void NaturalLoftInterpolatesEverySectionAndHasZeroEndCurvature()
    {
        int* curves = stackalloc int[4];
        for (var i = 0; i < 4; i++) curves[i] = Curve(2, new double[] { 0, 0, i, 1, i + 1, i + .4, 2, 0, i }, new double[] { 0, 1 }, new[] { 3, 3 });
        var options = LoftSurfaceOptions(); int surface = 0;
        Assert.Equal(0, KernelRuntime.BCurveMakeBSurfLofted(4, curves, &options, &surface));
        for (var i = 0; i < 4; i++)
            for (var k = 0; k <= 8; k++)
            {
                var a = EvalCurve(curves[i], k / 8.0); var b = EvalSurface(surface, k / 8.0, i / 3.0);
                for (var d = 0; d < 3; d++) Assert.Equal(a.coord[d], b.coord[d], 11);
            }
        PK_VECTOR_s* jet = stackalloc PK_VECTOR_s[3];
        for (var end = 0; end < 2; end++)
        {
            var uv = new PK_UV_s(); uv.param[0] = .4; uv.param[1] = end;
            Assert.Equal(0, KernelRuntime.SurfEval(surface, uv, 0, 2, 0, jet));
            for (var d = 0; d < 3; d++) Assert.True(Math.Abs(jet[2].coord[d]) < 1e-10);
        }
    }

    [Fact]
    public void RectangularNurbsSheetHasFourAccurateBoundaryEdges()
    {
        var curve = Curve(2, new double[] { 0, 0, 0, 1, 2, .5, 2, 0, 1 }, new double[] { 0, 1 }, new[] { 3, 3 });
        var path = new PK_VECTOR_s(); Set(&path, 0, 0, 3); int surface = 0, body = 0;
        Assert.Equal(0, KernelRuntime.BCurveSweep(curve, path, &surface));
        var box = Box(.2, .1, .8, .9); Assert.Equal(0, KernelRuntime.SurfMakeSheetBody(surface, box, &body));
        var record = Record(body); Assert.Equal(1, record.FaceCountBody); Assert.Equal(4, record.EdgeCountBody); Assert.Equal(4, record.VertexCountBody);
        Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c, record.BodyType); CheckBody(body);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CurvedParallelSweepBuildsClosedSolidWithHolesAndPreservesInputs(bool hole)
    {
        var profile = Polygon(0, 1, 0, hole); var pathCurve = Curve(3,
            new double[] { 0, 0, 0, .5, 0, 1, .2, .8, 2, 1, .5, 3 }, new double[] { 0, 1 }, new[] { 4, 4 });
        var interval = new PK_INTERVAL_s(); interval.value[1] = 1; int path = 0;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody(pathCurve, interval, &path));
        var options = SweepOptions(); var result = new PK_BODY_tracked_sweep_r_s();
        Assert.Equal(0, KernelRuntime.BodyMakeSweptBody(profile, path, 0, &options, &result));
        Assert.Equal(ParasolidConstants.PK_BODY_sweep_ok_c, result.status.fault);
        var record = Record(result.body); Assert.Equal(hole ? 10 : 6, record.FaceCountBody);
        Assert.Equal(hole ? 24 : 12, record.EdgeCountBody); Assert.Equal(hole ? 16 : 8, record.VertexCountBody);
        Assert.Equal(record.FaceCountBody, result.tracking_info.n_track_records);
        for (var i = 0; i < result.tracking_info.n_track_records; i++)
        {
            var tracking = result.tracking_info.track_records[i]; Assert.Equal(2, tracking.n_original_topols); Assert.Equal(1, tracking.n_product_topols);
            Assert.Equal(i < record.FaceCountBody - 2 ? ParasolidConstants.PK_CLASS_edge : ParasolidConstants.PK_CLASS_face, tracking.original_classes[0]);
            Assert.Equal(i < record.FaceCountBody - 2 ? ParasolidConstants.PK_CLASS_edge : ParasolidConstants.PK_CLASS_vertex, tracking.original_classes[1]);
        }
        CheckBody(result.body); Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c, Record(profile).BodyType);
        Assert.Equal(0, KernelRuntime.BodyTrackedSweepResultFree(&result));
    }

    [Fact]
    public void BodyLoftBuildsThreeHomotheticSectionsWithCurvedCenterAndFaceTracking()
    {
        int* profiles = stackalloc int[3] { Polygon(0, 1, 0, false), PolygonWire(1.5, 1.4, .3, false), Polygon(3, .8, 1, false) };
        var options = BodyLoftOptions(); var result = new PK_BODY_tracked_loft_s();
        Assert.Equal(0, KernelRuntime.BodyMakeLoftedBody(3, profiles, null, &options, &result));
        Assert.Equal(ParasolidConstants.PK_BODY_loft_ok_c, result.status.fault);
        Assert.Equal(6, Record(result.body).FaceCountBody); CheckBody(result.body);
        Assert.Equal(6, result.tracking_info.n_track_records);
        for (var i = 0; i < 6; i++)
        {
            var tracking = result.tracking_info.track_records[i]; Assert.Equal(ParasolidConstants.PK_TOPOL_track_create_c, tracking.track);
            Assert.Equal(i < 4 ? 3 : 1, tracking.n_original_topols); Assert.Equal(1, tracking.n_product_topols);
            for (var p = 0; p < tracking.n_original_topols; p++) Assert.Equal(i < 4 ? ParasolidConstants.PK_CLASS_edge : ParasolidConstants.PK_CLASS_face, tracking.original_classes[p]);
        }
        Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c, Record(profiles[0]).BodyType);
        Assert.Equal(ParasolidConstants.PK_BODY_type_wire_c, Record(profiles[1]).BodyType);
        Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c, Record(profiles[2]).BodyType);
        Assert.Equal(0, KernelRuntime.BodyTrackedLoftResultFree(&result));
    }

    [Fact]
    public void LoftRejectsIntermediateSheetWithPkStatusAndDoesNotInventEndConditions()
    {
        int* profiles = stackalloc int[3] { Polygon(0, 1, 0, false), Polygon(1.5, 1.2, .2, false), Polygon(3, .8, 1, false) };
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        var options = BodyLoftOptions(); var result = new PK_BODY_tracked_loft_s();
        Assert.Equal(0, KernelRuntime.BodyMakeLoftedBody(3, profiles, null, &options, &result));
        Assert.Equal(ParasolidConstants.PK_BODY_loft_bad_profile_type_c, result.status.fault); Assert.Equal(0, result.body);
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
        options.end_conditions.start.curvature_condition = ParasolidConstants.PK_BODY_loft_unconstrained_c;
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyMakeLoftedBody(3, profiles, null, &options, &result));
        Assert.Equal(0, result.body);
    }

    [Fact]
    public void CertifiedLoftAllowsChangingConvexSections()
    {
        int* profiles = stackalloc int[3]
        {
            ArbitraryPolygon(0, new double[] { -2,-2, 2,-2, 2,2, -2,2 }, false),
            ArbitraryPolygon(1.5, new double[] { -3,-1.4, 2.6,-1.8, 3,1.6, -2.4,2 }, true),
            ArbitraryPolygon(3, new double[] { -2.2,-1, 3,-1.6, 2.7,2.4, -1.8,1.8 }, false),
        };
        var options = BodyLoftOptions(); var result = new PK_BODY_tracked_loft_s();
        Assert.Equal(0, KernelRuntime.BodyMakeLoftedBody(3, profiles, null, &options, &result));
        Assert.Equal(ParasolidConstants.PK_BODY_loft_ok_c, result.status.fault);
        var body = Record(result.body); Assert.Equal(6, body.FaceCountBody); CheckBody(result.body);
        var surface = KernelRuntime.Faces[body.FirstFaceBody].SurfTag;
        var point = EvalSurface(surface, 0, .5);
        Assert.Equal(-3, point.coord[0], 10); Assert.Equal(-1.4, point.coord[1], 10); Assert.Equal(1.5, point.coord[2], 10);
        KernelRuntime.BodyTrackedLoftResultFree(&result);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public void LoftReportsNativeBadProfileTypeForCapsWithInnerLoops(int count)
    {
        int* profiles = stackalloc int[3] { Polygon(0, 1, 0, true), count == 2 ? Polygon(3, .8, 1, true) : PolygonWire(1.5, 1.4, .3, true), Polygon(3, .8, 1, true) };
        var options = BodyLoftOptions(); var result = new PK_BODY_tracked_loft_s();
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(0, KernelRuntime.BodyMakeLoftedBody(count, profiles, null, &options, &result));
        Assert.Equal(ParasolidConstants.PK_BODY_loft_bad_profile_type_c, result.status.fault); Assert.Equal(profiles[0], result.status.fault_topol); Assert.Equal(0, result.body);
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
    }

    [Fact]
    public void LoftCertificateRejectsCollapsingCorrespondenceBetweenValidProfiles()
    {
        int* profiles = stackalloc int[2]
        {
            ArbitraryPolygon(0, new double[] { -2,-2, 2,-2, 2,2, -2,2 }, false),
            ArbitraryPolygon(3, new double[] { 2,2, -2,2, -2,-2, 2,-2 }, false),
        };
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        var options = BodyLoftOptions(); var result = new PK_BODY_tracked_loft_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyMakeLoftedBody(2, profiles, null, &options, &result));
        Assert.Equal(0, result.body);
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(3, 3)] [InlineData(1, 3)]
    public void SplinewiseSurfaceInterpolatesPointGridAndRejectsNonfiniteData(int uDegree, int vDegree)
    {
        const int nu = 4, nv = 3; PK_VECTOR_s* positions = stackalloc PK_VECTOR_s[nu * nv];
        for (var i = 0; i < nu; i++) for (var j = 0; j < nv; j++) Set(positions + j * nu + i, i, j, .2 * i * j + .1 * i * i);
        double* u = stackalloc double[nu] { -1, -.1, .4, 2 }; double* v = stackalloc double[nv] { 0, .3, 1 };
        var definition = new PK_BSURF_splinewise_sf_s { u_degree = uDegree, v_degree = vDegree,
            n_u_positions = nu, n_v_positions = nv, positions = positions,
            u_end = ParasolidConstants.PK_PARAM_end_natural_c, U_end = ParasolidConstants.PK_PARAM_end_natural_c,
            v_end = ParasolidConstants.PK_PARAM_end_natural_c, V_end = ParasolidConstants.PK_PARAM_end_natural_c,
            u_how_knot = ParasolidConstants.PK_PARAM_knot_defined_c, v_how_knot = ParasolidConstants.PK_PARAM_knot_defined_c, u_knot = u, v_knot = v,
            u_v = ParasolidConstants.PK_PARAM_twist_no_c, u_V = ParasolidConstants.PK_PARAM_twist_no_c,
            U_v = ParasolidConstants.PK_PARAM_twist_no_c, U_V = ParasolidConstants.PK_PARAM_twist_no_c };
        int surface = 0; Assert.Equal(0, KernelRuntime.BSurfCreateSplinewise(&definition, &surface));
        for (var i = 0; i < nu; i++) for (var j = 0; j < nv; j++)
        {
            var actual = EvalSurface(surface, u[i], v[j]);
            for (var d = 0; d < 3; d++) Assert.Equal(positions[j * nu + i].coord[d], actual.coord[d], 10);
        }
        var before = KernelRuntime.Surfaces.AliveCount; positions[5].coord[1] = double.NaN;
        Assert.NotEqual(0, KernelRuntime.BSurfCreateSplinewise(&definition, &surface)); Assert.Equal(0, surface);
        Assert.Equal(before, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void LeastSquaresFitUsesFewerPolesAndVerifiesEverySuppliedPoint()
    {
        const int count = 40; PK_VECTOR_s* points = stackalloc PK_VECTOR_s[count];
        double* parameters = stackalloc double[count]; int* indices = stackalloc int[count];
        for (var i = 0; i < count; i++)
        {
            parameters[i] = (double)i / (count - 1); indices[i] = i;
            Set(points + i, parameters[i], parameters[i] * parameters[i] + .00001 * Math.Sin(i), 0);
        }
        var options = SplineOptions(); options.spline_method = ParasolidConstants.PK_spline_method_fit_c; options.have_fit_tol = 1; options.fit_tol = 2e-5;
        options.n_parameters = count; options.parameters = parameters; options.param_indices = indices;
        var result = new PK_BCURVE_spline_r_s(); Assert.Equal(0, KernelRuntime.BCurveCreateSpline2(count, points, &options, &result));
        Assert.True(result.bcurves[0].distance_error <= options.fit_tol);
        var curve = result.bcurves[0].bcurve; var data = KernelRuntime.BCurveDataStore[KernelRuntime.GetCurveByTag(curve).DataIndex];
        Assert.True(data.NVertices < count / 2);
        for (var i = 0; i < count; i++)
        {
            var p = EvalCurve(curve, parameters[i]); var dx = p.coord[0] - points[i].coord[0]; var dy = p.coord[1] - points[i].coord[1];
            Assert.True(Math.Sqrt(dx * dx + dy * dy) <= options.fit_tol);
        }
        KernelRuntime.BCurveSplineResultFree(&result);
    }

    [Fact]
    public void SewingMatchesNurbsIsoparamsReversesSheetAndRetainsTargetIdentity()
    {
        var curve = Curve(2, new double[] { 0, 0, 0, 1, .3, .2, 2, 0, .5 }, new double[] { 0, 1 }, new[] { 3, 3 });
        var path = new PK_VECTOR_s(); Set(&path, 0, 2, 1); int surface = 0;
        Assert.Equal(0, KernelRuntime.BCurveSweep(curve, path, &surface));
        int* bodies = stackalloc int[2];
        Assert.Equal(0, KernelRuntime.SurfMakeSheetBody(surface, Box(0, 0, 1, .5), &bodies[0]));
        Assert.Equal(0, KernelRuntime.SurfMakeSheetBody(surface, Box(0, .5, 1, 1), &bodies[1]));
        var target = bodies[0]; var consumed = bodies[1]; var options = SewOptions();
        int n = 0, u = 0, p = 0; int* sewn = null; int* unsewn = null; PK_BODY_problem_group_s* problems = null;
        Assert.Equal(0, KernelRuntime.BodySewBodies(2, bodies, 1e-7, &options, &n, &sewn, &u, &unsewn, &p, &problems));
        Assert.Equal(1, n); Assert.Equal(target, sewn[0]); Assert.Equal(0, u); Assert.Equal(0, p);
        Assert.Equal(2, Record(target).FaceCountBody); Assert.Equal(7, Record(target).EdgeCountBody); Assert.Equal(6, Record(target).VertexCountBody);
        Assert.False(KernelRuntime.TryResolveBodySlot(consumed, out _)); CheckBody(target);
        KernelRuntime.MemoryFree(sewn);
    }

    [Fact]
    public void SweepFailureLeavesInputsAndNoPublishedResult()
    {
        var profile = Polygon(0, 1, 0, false); var pathCurve = Curve(3,
            new double[] { 0, 0, 0, .5, 0, 2, .2, .8, -1, 1, .5, 3 }, new double[] { 0, 1 }, new[] { 4, 4 });
        var interval = new PK_INTERVAL_s(); interval.value[1] = 1; int path = 0; Assert.Equal(0, KernelRuntime.CurveMakeWireBody(pathCurve, interval, &path));
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        var options = SweepOptions(); var result = new PK_BODY_tracked_sweep_r_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyMakeSweptBody(profile, path, 0, &options, &result));
        Assert.Equal(0, result.body); Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
    }

    [Fact]
    public void SolidSweepRejectsWireProfileWithoutReadingMissingCapFaces()
    {
        var profile = PolygonWire(0, 1, 0, false);
        var curve = Curve(1, new double[] { 0,0,0, 0,0,3 }, new double[] { 0,1 }, new[] { 2,2 });
        var range = new PK_INTERVAL_s(); range.value[1] = 1; int path = 0;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody(curve, range, &path));
        var options = SweepOptions(); var result = new PK_BODY_tracked_sweep_r_s();
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount);
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyMakeSweptBody(profile, path, 0, &options, &result));
        Assert.Equal(0, result.body); Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount));
    }

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(30)]
    public void SweepAllocationFailureRollsBackFinDependenciesAndReturnedTracking(int budget)
    {
        var profile = Polygon(0, 1, 0, false); var curve = Curve(3,
            new double[] { 0,0,0, .5,0,1, .2,.8,2, 1,.5,3 }, new double[] { 0,1 }, new[] { 4,4 });
        var range = new PK_INTERVAL_s(); range.value[1] = 1; int path = 0;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody(curve, range, &path));
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount);
        var options = SweepOptions(); var result = new PK_BODY_tracked_sweep_r_s(); int error;
        KernelRuntime.InjectAllocationFailure(budget);
        try { error = KernelRuntime.BodyMakeSweptBody(profile, path, 0, &options, &result); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0)
        {
            var body = result.body; CheckBody(body); Assert.Equal(0, KernelRuntime.BodyTrackedSweepResultFree(&result));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        }
        else { Assert.Equal(0, result.body); Assert.True(result.tracking_info.track_records == null); }
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount));
        CheckBody(profile);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)] [InlineData(1, 1)] [InlineData(1, 2)]
    public void DetachedGeometryIsRejectedBeforeProfileSewingOrPathPoolReads(int pathTarget, int geometryKind)
    {
        var profile = Polygon(0, 1, 0, false); var top = Polygon(3, 1, 0, false);
        var curve = Curve(1, new double[] { 0,0,0, 0,0,3 }, new double[] { 0,1 }, new[] { 2,2 });
        var range = new PK_INTERVAL_s(); range.value[1] = 1; int path = 0;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody(curve, range, &path));
        var source = Record(pathTarget != 0 ? path : profile);
        var tag = geometryKind == 0 ? KernelRuntime.Faces[source.FirstFaceBody].Header.Tag
            : geometryKind == 1 ? KernelRuntime.Edges[source.FirstEdgeBody].Header.Tag : KernelRuntime.Vertices[source.FirstVertexBody].Header.Tag;
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(tag));
        var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount);
        var sweep = SweepOptions(); var sweepResult = new PK_BODY_tracked_sweep_r_s();
        Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyMakeSweptBody(profile, path, 0, &sweep, &sweepResult));
        Assert.Equal(0, sweepResult.body);
        if (pathTarget == 0)
        {
            int* profiles = stackalloc int[2] { profile, top }; var loft = BodyLoftOptions(); var loftResult = new PK_BODY_tracked_loft_s();
            Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodyMakeLoftedBody(2, profiles, null, &loft, &loftResult));
            Assert.Equal(0, loftResult.body);
            var sew = SewOptions(); int n = 0, u = 0, p = 0; int* sewn = null; int* unsewn = null; PK_BODY_problem_group_s* problems = null;
            Assert.Equal(ParasolidConstants.PK_ERROR_not_implemented, KernelRuntime.BodySewBodies(2, profiles, 1e-7, &sew, &n, &sewn, &u, &unsewn, &p, &problems));
            Assert.Equal(0, n); Assert.Equal(0, u); Assert.True(sewn == null && unsewn == null && problems == null);
        }
        Assert.Equal(before, (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount));
    }

    private static PK_BCURVE_create_spline_2_o_s SplineOptions()
    {
        var value = new PK_BCURVE_create_spline_2_o_s { o_t_version = 2, spline_method = ParasolidConstants.PK_spline_method_interpolate_c,
            overdefined = ParasolidConstants.PK_BCURVE_overdefined_fail_c, clamp = ParasolidConstants.PK_BCURVE_clamp_no_c };
        value.param_interval.value[1] = 1; return value;
    }
    private static PK_BCURVE_make_bsurf_lofted_o_s LoftSurfaceOptions() => new()
    {
        o_t_version = 1, v_end = ParasolidConstants.PK_PARAM_end_natural_c, V_end = ParasolidConstants.PK_PARAM_end_natural_c,
        u_v = ParasolidConstants.PK_PARAM_twist_no_c, u_V = ParasolidConstants.PK_PARAM_twist_no_c, U_v = ParasolidConstants.PK_PARAM_twist_no_c, U_V = ParasolidConstants.PK_PARAM_twist_no_c,
        v_degen = ParasolidConstants.PK_PARAM_degen_no_c, V_degen = ParasolidConstants.PK_PARAM_degen_no_c, how_knot = ParasolidConstants.PK_PARAM_knot_auto_c,
    };
    private static PK_BODY_make_swept_body_o_s SweepOptions() => new()
    {
        o_t_version = 8, alignment = ParasolidConstants.PK_BODY_sweep_align_parallel_c, simplify = ParasolidConstants.PK_BODY_simplify_no_c,
        topology_form = ParasolidConstants.PK_BODY_topology_minimal_c, tolerance = 1e-6, allow_rationals = 1,
        twist = new PK_BODY_sweep_law_s { law_type = ParasolidConstants.PK_BODY_sweep_law_none_c },
        scale = new PK_BODY_sweep_law_s { law_type = ParasolidConstants.PK_BODY_sweep_law_none_c },
        fair = ParasolidConstants.PK_BODY_sweep_fair_default_c, scale_type = ParasolidConstants.PK_BODY_sweep_scale_both_c,
    };
    private static PK_BODY_make_lofted_body_o_s BodyLoftOptions() => new()
    {
        o_t_version = 16, tolerance = 1e-6, simplify = ParasolidConstants.PK_BODY_simplify_no_c, topology_form = ParasolidConstants.PK_BODY_topology_minimal_c,
        end_conditions = new PK_BODY_loft_end_conds_s { periodic = ParasolidConstants.PK_PARAM_periodic_no_c,
            start = new PK_BODY_loft_deriv_conds_s { type = ParasolidConstants.PK_BODY_loft_clamp_no_c, curvature_condition = ParasolidConstants.PK_BODY_loft_natural_c },
            end = new PK_BODY_loft_deriv_conds_s { type = ParasolidConstants.PK_BODY_loft_clamp_no_c, curvature_condition = ParasolidConstants.PK_BODY_loft_natural_c } },
    };
    private static PK_BODY_sew_bodies_o_s SewOptions() => new()
    {
        o_t_version = 8, treat_as_manifold = 1, prefered_body_type = ParasolidConstants.PK_BODY_sewing_any_c,
        duplicate_removal = ParasolidConstants.PK_BODY_sewing_remove_none_c, number_of_iterations = 1,
        assembly_sewing = ParasolidConstants.PK_BODY_sewing_assy_no_c, general_sewing = ParasolidConstants.PK_BODY_sewing_gen_no_c,
        sew_up_inner_loops = ParasolidConstants.PK_LOOP_sew_up_loop_c, reduce_edge_tolerance = ParasolidConstants.PK_EDGE_reduce_tol_no_c,
    };
    private static int Polygon(double z, double scale, double dx, bool hole)
    {
        PK_VECTOR_s* points = stackalloc PK_VECTOR_s[8];
        Set(points, dx - 2 * scale, -2 * scale, z); Set(points + 1, dx + 2 * scale, -2 * scale, z);
        Set(points + 2, dx + 2 * scale, 2 * scale, z); Set(points + 3, dx - 2 * scale, 2 * scale, z);
        Set(points + 4, dx - .5 * scale, -.5 * scale, z); Set(points + 5, dx + .5 * scale, -.5 * scale, z);
        Set(points + 6, dx + .5 * scale, .5 * scale, z); Set(points + 7, dx - .5 * scale, .5 * scale, z);
        int* loops = stackalloc int[8] { 0, 0, 0, 0, 1, 1, 1, 1 };
        var options = new PK_BODY_create_sheet_planar_o_s { o_t_version = 1, loops = hole ? loops : null };
        options.plane.axis.coord[2] = 1; options.plane.location.coord[2] = z;
        options.basis_set.axis.coord[2] = 1; options.basis_set.ref_direction.coord[0] = 1;
        int body = 0; Assert.Equal(0, KernelRuntime.BodyCreateSheetPlanar(hole ? 8 : 4, points, &options, &body)); return body;
    }
    private static int PolygonWire(double z, double scale, double dx, bool hole)
        => WireFromSheet(Polygon(z, scale, dx, hole));
    private static int ArbitraryPolygon(double z, double[] xy, bool wire)
    {
        var count = xy.Length / 2; PK_VECTOR_s* points = stackalloc PK_VECTOR_s[count]; int* loops = stackalloc int[count];
        for (var i = 0; i < count; i++) { Set(points + i, xy[2 * i], xy[2 * i + 1], z); loops[i] = i < 4 ? 0 : 1; }
        var options = new PK_BODY_create_sheet_planar_o_s { o_t_version = 1, loops = count > 4 ? loops : null };
        options.plane.axis.coord[2] = 1; options.plane.location.coord[2] = z;
        options.basis_set.axis.coord[2] = 1; options.basis_set.ref_direction.coord[0] = 1;
        int body = 0; Assert.Equal(0, KernelRuntime.BodyCreateSheetPlanar(count, points, &options, &body));
        return wire ? WireFromSheet(body) : body;
    }
    private static int WireFromSheet(int sheet)
    {
        var source = Record(sheet); var count = source.EdgeCountBody;
        int* curves = stackalloc int[count]; PK_INTERVAL_s* ranges = stackalloc PK_INTERVAL_s[count];
        var edge = source.FirstEdgeBody;
        for (var i = 0; i < count; i++, edge = KernelRuntime.Edges[edge].NextInBody)
        {
            Assert.Equal(0, KernelRuntime.EntityCopy(KernelRuntime.Edges[edge].CurveTag, &curves[i]));
            ranges[i].value[0] = KernelRuntime.Edges[edge].ParameterStart; ranges[i].value[1] = KernelRuntime.Edges[edge].ParameterEnd;
        }
        var options = new PK_CURVE_make_wire_body_o_s { o_t_version = 3, tolerance = 1e-8, allow_disjoint = 1, check = 1,
            sequential = ParasolidConstants.PK_CURVE_sequential_no_c };
        int body = 0, returned = 0; int* output = null;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody2(count, curves, ranges, &options, &body, &returned, &output, null));
        if (output != null) KernelRuntime.MemoryFree(output); return body;
    }
    private static int Curve(int degree, double[] poles, double[] knots, int[] mults, bool rational = false)
    {
        fixed (double* p = poles, k = knots) fixed (int* m = mults)
        {
            var dimension = rational ? 4 : 3;
            var definition = new PK_BCURVE_sf_s { degree = degree, n_vertices = poles.Length / dimension, vertex_dim = dimension,
                vertex = p, is_rational = (byte)(rational ? 1 : 0), n_knots = knots.Length, knot = k, knot_mult = m,
                form = ParasolidConstants.PK_BCURVE_form_arbitrary_c, knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_false_c };
            int curve = 0; Assert.Equal(0, KernelRuntime.BCurveCreate(&definition, &curve)); return curve;
        }
    }
    private static PK_VECTOR_s EvalCurve(int curve, double parameter) { PK_VECTOR_s output = default; Assert.Equal(0, KernelRuntime.CurveEval(curve, parameter, 0, &output)); return output; }
    private static PK_VECTOR_s EvalSurface(int surface, double u, double v) { var uv = new PK_UV_s(); uv.param[0] = u; uv.param[1] = v; PK_VECTOR_s output = default; Assert.Equal(0, KernelRuntime.SurfEval(surface, uv, 0, 0, 0, &output)); return output; }
    private static PK_UVBOX_s Box(double u0, double v0, double u1, double v1) { var box = new PK_UVBOX_s(); box.param[0] = u0; box.param[1] = v0; box.param[2] = u1; box.param[3] = v1; return box; }
    private static void Set(PK_VECTOR_s* point, double x, double y, double z) { point->coord[0] = x; point->coord[1] = y; point->coord[2] = z; }
    private static BodyRecord Record(int body) { Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot)); return KernelRuntime.GetBodyRecord(slot); }
    private static void CheckBody(int body)
    {
        var options = new PK_BODY_check_o_s
        {
            o_t_version=6,max_faults=32,geom=ParasolidConstants.PK_check_geom_no_c,bgeom=ParasolidConstants.PK_check_bgeom_no_c,
            mesh=ParasolidConstants.PK_check_mesh_no_c,top_geo=ParasolidConstants.PK_check_top_geo_no_c,size_box=ParasolidConstants.PK_check_size_box_no_c,
            fa_X=ParasolidConstants.PK_check_fa_X_no_c,fa_fa=ParasolidConstants.PK_check_fa_fa_no_c,nmnl_geom=ParasolidConstants.PK_check_nmnl_geom_no_c,
            attribs=ParasolidConstants.PK_check_attribs_no_c,returns=ParasolidConstants.PK_check_returns_0_c,extra_faults=ParasolidConstants.PK_check_extra_faults_0_c,
            loops=ParasolidConstants.PK_check_loops_yes_c,sh=ParasolidConstants.PK_check_sh_yes_c,corrupt=ParasolidConstants.PK_check_corrupt_yes_c,
            missing_geom=ParasolidConstants.PK_check_missing_geom_yes_c,
        }; int n = 0; PK_check_fault_s* faults = null;
        Assert.Equal(0, KernelRuntime.BodyCheck(body, &options, &n, &faults)); Assert.Equal(0, n); if (faults != null) KernelRuntime.MemoryFree(faults);
    }
}
