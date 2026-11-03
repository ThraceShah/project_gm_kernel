#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=FreeformModelingOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Independent producers: the implementation never consumes a PK body or uses
// PK to generate its geometry. Real PK validates fresh transmitted products.
global using ManagedBodyTag = System.Int32;
using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "freeform-modeling-oracle"));
Directory.CreateDirectory(directory);
var report = new StringBuilder();
void Log(string line) { Console.WriteLine(line); report.AppendLine(line); }
try
{
    if (!ParasolidScriptHost.TryStartSession("Independent freeform modeling oracle", out var host, out var message)) throw new Exception(message);
    using (host)
    unsafe
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 }; Check(KernelRuntime.SessionStart(&start), "self session start");
        try
        {
            var names = new[] { "spline-interpolation", "rational-surface-sweep", "natural-three-section-loft", "rational-surface-spin",
                "cylinder-rectangular-sheet", "torus-rectangular-sheet", "parallel-nurbs-path-solid", "parallel-nurbs-path-hole-solid",
                "two-profile-loft", "three-profile-natural-loft", "nurbs-sheet-sewing", "closed-nurbs-cube-sewing", "least-squares-point-fit",
                "automatic-grid-surface", "explicit-grid-surface", "convex-changing-sections-loft" };
            var failures = 0;
            for (var scenario = 0; scenario < names.Length; scenario++)
            {
                var name = names[scenario];
                try
                {
                    var ours = Produce(true, scenario); var reference = Produce(false, scenario);
                    var bytes = TransmitManaged(ours); File.WriteAllBytes(Path.Combine(directory, name + "-self.x_t"), bytes);
                    var referenceBytes = TransmitReference(reference);
                    File.WriteAllBytes(Path.Combine(directory, name + "-reference.x_t"), referenceBytes);
                    var received = ReceiveOracle(bytes);
                    CheckReferenceBody(reference, name + " independent reference", Log); CheckReferenceBody(received, name + " self receive", Log);
                    for (var kind = 0; kind < 5; kind++)
                    {
                        var actual = OracleCount(received, kind); var expected = OracleCount(reference, kind);
                        Log($"{name} topology {kind}: reference={expected}, self={actual}"); Require(actual == expected, name + " topology mismatch " + kind);
                    }
                    CompareBodies(reference, received, name, Log);
                    if (scenario is 6 or 9)
                    {
                        RoundTripTolerant(bytes, reference, name + " self XT lifecycle", Log);
                        RoundTripTolerant(referenceBytes, reference, name + " PK XT lifecycle", Log);
                    }
                    Log("PASS " + name);
                }
                catch (Exception ex) { failures++; Log("FAIL " + name + ": " + ex.Message); }
            }
            CheckLoftHoleRejection(2, Log); CheckLoftHoleRejection(3, Log);
            Require(failures == 0, $"{failures}/{names.Length} independent freeform operations failed.");
            Log($"PASS all {names.Length} freeform modeling cases; default PK_BODY_check and structural comparison passed.");
        }
        finally { Check(KernelRuntime.SessionStop(), "self session stop"); }
    }
}
catch (Exception ex) { Log("FAIL: " + ex.Message); Environment.ExitCode = 1; }
finally { File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString()); }

static unsafe int Produce(bool self, int scenario)
{
    if (scenario == 15)
    {
        var coordinates = new[]
        {
            new double[] { -2,-2, 2,-2, 2,2, -2,2 },
            new double[] { -3,-1.5, 3,-1.5, 3,1.5, -3,1.5 },
            new double[] { -1.5,-2.5, 1.5,-2.5, 1.5,2.5, -1.5,2.5 },
        };
        int* profiles = stackalloc int[3]; int* starts = stackalloc int[3];
        for (var i = 0; i < 3; i++)
        {
            profiles[i] = ArbitraryProfile(self, i * 1.5, coordinates[i], i == 1);
            int n = 0; int* vertices = null; Check(self ? KernelRuntime.BodyAskVertices(profiles[i], &n, &vertices) : PK_BODY_ask_vertices(profiles[i], &n, &vertices), "convex loft vertices");
            starts[i] = SelectVertex(self, n, vertices, coordinates[i][0], coordinates[i][1], i * 1.5);
            Check(self ? KernelRuntime.MemoryFree(vertices) : PK_MEMORY_free(vertices), "free convex loft vertices");
        }
        var options = new PK_BODY_make_lofted_body_o_t { simplify = PK_BODY_simplify_no_c, tolerance = 1e-6 };
        options.end_conditions.start.curvature_condition = options.end_conditions.end.curvature_condition = PK_BODY_loft_natural_c;
        PK_BODY_tracked_loft_r_t result;
        Check(self ? KernelRuntime.BodyMakeLoftedBody(3, profiles, starts, (M.PK_BODY_make_lofted_body_o_s*)&options, (M.PK_BODY_tracked_loft_s*)&result)
            : PK_BODY_make_lofted_body(3, profiles, starts, &options, &result), "certified convex section loft");
        Require(result.status.fault == PK_BODY_loft_ok_c, $"convex loft self={self} fault=" + result.status.fault); var body = result.body;
        ValidateFaceTracking(self, &result.tracking_info, 4, 3, "convex loft");
        Check(self ? KernelRuntime.BodyTrackedLoftResultFree((M.PK_BODY_tracked_loft_s*)&result) : PK_BODY_tracked_loft_r_f(&result), "free convex loft"); return body;
    }
    if (scenario is 13 or 14)
    {
        const int nu = 4, nv = 3; PK_VECTOR_t* positions = stackalloc PK_VECTOR_t[nu * nv];
        for (var i = 0; i < nu; i++) for (var j = 0; j < nv; j++) positions[j * nu + i] = new(i, j, .2 * i * j + .1 * i * i);
        double* u = stackalloc double[nu] { 0, .2, .7, 1 }; double* v = stackalloc double[nv] { 0, .3, 1 };
        var definition = new PK_BSURF_splinewise_sf_t { u_degree = 3, v_degree = 3, n_u_positions = nu, n_v_positions = nv, positions = positions,
            u_end = PK_PARAM_end_natural_c, U_end = PK_PARAM_end_natural_c, v_end = PK_PARAM_end_natural_c, V_end = PK_PARAM_end_natural_c,
            u_how_knot = scenario == 13 ? PK_PARAM_knot_auto_c : PK_PARAM_knot_defined_c,
            v_how_knot = scenario == 13 ? PK_PARAM_knot_auto_c : PK_PARAM_knot_defined_c, u_knot = u, v_knot = v,
            u_v = PK_PARAM_twist_no_c, u_V = PK_PARAM_twist_no_c, U_v = PK_PARAM_twist_no_c, U_V = PK_PARAM_twist_no_c };
        int surface = 0; Check(self ? KernelRuntime.BSurfCreateSplinewise((M.PK_BSURF_splinewise_sf_s*)&definition, &surface)
            : PK_BSURF_create_splinewise(&definition, &surface), "grid surface interpolation");
        if (scenario == 13)
        {
            u[0] = v[0] = 0; u[1] = .3094690792966274; u[2] = .6400121962078179; u[3] = 1; v[1] = .5; v[2] = 1;
        }
        for (var i = 0; i < nu; i++) for (var j = 0; j < nv; j++)
        {
            var uv = new PK_UV_t(); uv.param[0] = u[i]; uv.param[1] = v[j]; PK_VECTOR_t actual;
            Check(self ? KernelRuntime.SurfEval(surface, *(M.PK_UV_s*)&uv, 0, 0, 0, (M.PK_VECTOR_s*)&actual)
                : PK_SURF_eval(surface, uv, 0, 0, PK_LOGICAL_false, &actual), "grid interpolation sample");
            for (var d = 0; d < 3; d++) Require(Math.Abs(actual.coord[d] - positions[j * nu + i].coord[d]) < 1e-10, "grid position mismatch");
        }
        var box = Box(0, 0, 1, 1); int body = 0;
        Check(self ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&box, &body) : PK_SURF_make_sheet_body(surface, box, &body), "grid surface sheet"); return body;
    }
    if (scenario == 12)
    {
        const int count = 32; PK_VECTOR_t* points = stackalloc PK_VECTOR_t[count]; double* parameters = stackalloc double[count]; int* indices = stackalloc int[count];
        for (var i = 0; i < count; i++) { var t = (double)i / (count - 1); points[i] = new(t, t * t, .2 * t * t * t); parameters[i] = t; indices[i] = i; }
        var options = new PK_BCURVE_create_spline_2_o_t { spline_method = PK_spline_method_fit_c, have_fit_tol = PK_LOGICAL_true, fit_tol = 1e-6,
            n_parameters = count, parameters = parameters, param_indices = indices };
        PK_BCURVE_spline_r_t result;
        Check(self ? KernelRuntime.BCurveCreateSpline2(count, (M.PK_VECTOR_s*)points, (M.PK_BCURVE_create_spline_2_o_s*)&options, (M.PK_BCURVE_spline_r_s*)&result)
            : PK_BCURVE_create_spline_2(count, points, &options, &result), "least-squares point fit");
        Require(result.n_bcurves == 1 && result.fault == PK_BCURVE_spline_ok_c, "point fit status"); var curve = result.bcurves[0].bcurve;
        Require(result.bcurves[0].distance_error <= options.fit_tol, "reported point fit tolerance");
        for (var i = 0; i < count; i++)
        {
            PK_VECTOR_t actual; Check(self ? KernelRuntime.CurveEval(curve, parameters[i], 0, (M.PK_VECTOR_s*)&actual) : PK_CURVE_eval(curve, parameters[i], 0, &actual), "fit sample");
            double distance = 0; for (var d = 0; d < 3; d++) distance += Math.Pow(actual.coord[d] - points[i].coord[d], 2);
            Require(Math.Sqrt(distance) <= options.fit_tol, "independently measured input residual");
        }
        Check(self ? KernelRuntime.BCurveSplineResultFree((M.PK_BCURVE_spline_r_s*)&result) : PK_BCURVE_spline_r_f(&result), "free point fit"); return Wire(self, curve, 0, 1);
    }
    if (scenario == 0)
    {
        PK_VECTOR_t* points = stackalloc PK_VECTOR_t[4];
        points[0] = new(0, 0, 0); points[1] = new(1, 1, .3); points[2] = new(2, -.5, .4); points[3] = new(3, 0, 1);
        double* parameters = stackalloc double[4] { 0, 1, 2, 3 }; int* indices = stackalloc int[4] { 0, 1, 2, 3 };
        var options = new PK_BCURVE_create_spline_2_o_t(); options.n_parameters = 4; options.parameters = parameters; options.param_indices = indices;
        options.param_interval.value[1] = 3;
        PK_BCURVE_spline_r_t result;
        Check(self ? KernelRuntime.BCurveCreateSpline2(4, (M.PK_VECTOR_s*)points, (M.PK_BCURVE_create_spline_2_o_s*)&options, (M.PK_BCURVE_spline_r_s*)&result)
            : PK_BCURVE_create_spline_2(4, points, &options, &result), "create interpolating spline");
        Require(result.fault == PK_BCURVE_spline_ok_c && result.n_bcurves == 1, "spline status"); var curve = result.bcurves[0].bcurve;
        Check(self ? KernelRuntime.BCurveSplineResultFree((M.PK_BCURVE_spline_r_s*)&result) : PK_BCURVE_spline_r_f(&result), "free spline results");
        return Wire(self, curve, 0, 3);
    }
    if (scenario is 1 or 2 or 3 or 4 or 5)
    {
        var surface = Surface(self, scenario); var box = Box(0, 0, 1, 1);
        if (scenario == 4) box = Box(.15, -.5, 1.8, 2.3);
        if (scenario == 5) box = Box(.1, -.4, 1.5, .9);
        int body = 0; Check(self ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&box, &body) : PK_SURF_make_sheet_body(surface, box, &body), "rectangular surface sheet"); return body;
    }
    if (scenario is 6 or 7)
    {
        var profile = Polygon(self, 0, 1, 0, scenario == 7);
        var curve = BCurve(self, 3, new double[] { 0, 0, 0, .5, 0, 1, .2, .8, 2, 1, .5, 3 }, false);
        var path = Wire(self, curve, 0, 1);
        var options = new PK_BODY_make_swept_body_o_t { alignment = M.ParasolidConstants.PK_BODY_sweep_align_parallel_c, simplify = PK_BODY_simplify_no_c, tolerance = 1e-6 };
        int n = 0; int* vertices = null;
        Check(self ? KernelRuntime.BodyAskVertices(path, &n, &vertices) : PK_BODY_ask_vertices(path, &n, &vertices), "path vertices");
        var pathStart = SelectVertex(self, n, vertices, 0, 0, 0); Check(self ? KernelRuntime.MemoryFree(vertices) : PK_MEMORY_free(vertices), "free vertices");
        PK_BODY_tracked_sweep_r_t result;
        Check(self ? KernelRuntime.BodyMakeSweptBody(profile, path, pathStart, (M.PK_BODY_make_swept_body_o_s*)&options, (M.PK_BODY_tracked_sweep_r_s*)&result)
            : PK_BODY_make_swept_body(profile, path, pathStart, &options, &result), "curved parallel solid sweep");
        Require(result.status.fault == PK_BODY_sweep_ok_c, "body sweep fault=" + result.status.fault); var body = result.body;
        ValidateSweepTracking(self, &result.tracking_info, scenario == 7 ? 8 : 4);
        Check(self ? KernelRuntime.BodyTrackedSweepResultFree((M.PK_BODY_tracked_sweep_r_s*)&result) : PK_BODY_tracked_sweep_r_f(&result), "free body sweep"); return body;
    }
    if (scenario is 8 or 9)
    {
        var count = scenario == 8 ? 2 : 3;
        int* profiles = stackalloc int[count]; int* vertices = stackalloc int[count];
        profiles[0] = Polygon(self, 0, 1, 0, false);
        if (count == 3) profiles[1] = PolygonWire(self, 1.5, 1.4, .3);
        profiles[count - 1] = Polygon(self, 3, .8, 1, false);
        for (var i = 0; i < count; i++)
        {
            int n = 0; int* list = null; Check(self ? KernelRuntime.BodyAskVertices(profiles[i], &n, &list) : PK_BODY_ask_vertices(profiles[i], &n, &list), "loft profile vertices");
            var z = i == 0 ? 0 : i == count - 1 ? 3 : 1.5; var scale = i == 0 ? 1 : i == count - 1 ? .8 : 1.4; var dx = i == 0 ? 0 : i == count - 1 ? 1 : .3;
            vertices[i] = SelectVertex(self, n, list, dx - 2 * scale, -2 * scale, z); Check(self ? KernelRuntime.MemoryFree(list) : PK_MEMORY_free(list), "free loft vertex list");
        }
        var options = new PK_BODY_make_lofted_body_o_t { simplify = PK_BODY_simplify_no_c, tolerance = 1e-6 };
        options.end_conditions.start.curvature_condition = options.end_conditions.end.curvature_condition = PK_BODY_loft_natural_c;
        PK_BODY_tracked_loft_r_t result;
        Check(self ? KernelRuntime.BodyMakeLoftedBody(count, profiles, vertices, (M.PK_BODY_make_lofted_body_o_s*)&options, (M.PK_BODY_tracked_loft_s*)&result)
            : PK_BODY_make_lofted_body(count, profiles, vertices, &options, &result), "body loft");
        Require(result.status.fault == PK_BODY_loft_ok_c, "body loft fault=" + result.status.fault); var body = result.body;
        ValidateFaceTracking(self, &result.tracking_info, 4, count, "body loft");
        Check(self ? KernelRuntime.BodyTrackedLoftResultFree((M.PK_BODY_tracked_loft_s*)&result) : PK_BODY_tracked_loft_r_f(&result), "free body loft"); return body;
    }
    if (scenario == 10)
    {
        var surface = Surface(self, 1); int* bodies = stackalloc int[2];
        var low = Box(0, 0, 1, .5); var high = Box(0, .5, 1, 1);
        Check(self ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&low, &bodies[0]) : PK_SURF_make_sheet_body(surface, low, &bodies[0]), "low NURBS sheet");
        Check(self ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&high, &bodies[1]) : PK_SURF_make_sheet_body(surface, high, &bodies[1]), "high NURBS sheet");
        return Sew(self, 2, bodies);
    }
    if (scenario == 11)
    {
        // Each side is independently created as a tensor-product NURBS patch.
        var corners = new double[][]
        {
            new double[]{0,0,0,0,1,0,1,0,0,1,1,0},
            new double[]{0,0,1,1,0,1,0,1,1,1,1,1},
            new double[]{0,0,0,1,0,0,0,0,1,1,0,1},
            new double[]{0,1,0,0,1,1,1,1,0,1,1,1},
            new double[]{0,0,0,0,0,1,0,1,0,0,1,1},
            new double[]{1,0,0,1,1,0,1,0,1,1,1,1},
        };
        int* bodies = stackalloc int[6]; double* knots = stackalloc double[2] { 0, 1 }; int* mults = stackalloc int[2] { 2, 2 };
        for (var i = 0; i < 6; i++)
        fixed (double* poles = corners[i])
        {
            var definition = new M.PK_BSURF_sf_s { u_degree = 1, v_degree = 1, n_u_vertices = 2, n_v_vertices = 2, vertex_dim = 3, vertex = poles,
                n_u_knots = 2, n_v_knots = 2, u_knot = knots, v_knot = knots, u_knot_mult = mults, v_knot_mult = mults,
                u_knot_type = PK_knot_bezier_ends_c, v_knot_type = PK_knot_bezier_ends_c, form = PK_BSURF_form_arbitrary_c,
                self_intersecting = PK_self_intersect_false_c, convexity = PK_convexity_unset_c };
            int surface = 0; Check(self ? KernelRuntime.BSurfCreate(&definition, &surface) : PK_BSURF_create((PK_BSURF_sf_t*)&definition, &surface), "independent bilinear patch");
            var box = Box(0, 0, 1, 1); Check(self ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&box, &bodies[i]) : PK_SURF_make_sheet_body(surface, box, &bodies[i]), "bilinear face");
        }
        return Sew(self, 6, bodies);
    }
    throw new Exception("Unknown scenario");
}

static unsafe int Surface(bool self, int scenario)
{
    if (scenario == 4)
    {
        var definition = new PK_CYL_sf_t(); definition.radius = 2; definition.basis_set.axis = new PK_VECTOR1_t(0, 0, 1); definition.basis_set.ref_direction = new PK_VECTOR1_t(1, 0, 0);
        int surface = 0; Check(self ? KernelRuntime.CylCreate((M.PK_CYL_sf_s*)&definition, &surface) : PK_CYL_create(&definition, &surface), "cylinder"); return surface;
    }
    if (scenario == 5)
    {
        var definition = new PK_TORUS_sf_t(); definition.major_radius = 3; definition.minor_radius = .7;
        definition.basis_set.axis = new PK_VECTOR1_t(0, 0, 1); definition.basis_set.ref_direction = new PK_VECTOR1_t(1, 0, 0);
        int surface = 0; Check(self ? KernelRuntime.TorusCreate((M.PK_TORUS_sf_s*)&definition, &surface) : PK_TORUS_create(&definition, &surface), "torus"); return surface;
    }
    if (scenario == 2)
    {
        int* curves = stackalloc int[3];
        for (var i = 0; i < 3; i++) curves[i] = BCurve(self, 2, new double[] { 0, 0, i, 1, i + 1, i + .4, 2, 0, i }, false);
        var options = new PK_BCURVE_make_bsurf_lofted_o_t(); int surface = 0;
        Check(self ? KernelRuntime.BCurveMakeBSurfLofted(3, curves, (M.PK_BCURVE_make_bsurf_lofted_o_s*)&options, &surface)
            : PK_BCURVE_make_bsurf_lofted(3, curves, &options, &surface), "natural surface loft"); return surface;
    }
    var profile = BCurve(self, 2, new double[] { 2, 0, 0, 1, 1.8, 0, .8, .6, 4, 0, .4, 1 }, true);
    int product = 0;
    if (scenario == 3)
    {
        var axis = new PK_AXIS1_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1));
        Check(self ? KernelRuntime.BCurveSpin(profile, (M.PK_AXIS1_sf_s*)&axis, 1.2, &product) : PK_BCURVE_spin(profile, &axis, 1.2, &product), "rational surface spin");
    }
    else
    {
        var path = new PK_VECTOR_t(.2, 2, 3);
        Check(self ? KernelRuntime.BCurveSweep(profile, *(M.PK_VECTOR_s*)&path, &product) : PK_BCURVE_sweep(profile, path, &product), "rational surface sweep");
    }
    return product;
}
static unsafe int BCurve(bool self, int degree, double[] values, bool rational)
{
    fixed (double* poles = values)
    {
        double* knots = stackalloc double[2] { 0, 1 }; int* mults = stackalloc int[2] { degree + 1, degree + 1 }; var dim = rational ? 4 : 3;
        var definition = new M.PK_BCURVE_sf_s { degree = degree, n_vertices = values.Length / dim, vertex_dim = dim, vertex = poles, is_rational = (byte)(rational ? 1 : 0),
            n_knots = 2, knot = knots, knot_mult = mults, knot_type = PK_knot_bezier_ends_c, form = PK_BCURVE_form_arbitrary_c, self_intersecting = PK_self_intersect_false_c };
        int curve = 0; Check(self ? KernelRuntime.BCurveCreate(&definition, &curve) : PK_BCURVE_create((PK_BCURVE_sf_t*)&definition, &curve), "B-curve"); return curve;
    }
}
static unsafe int Polygon(bool self, double z, double scale, double dx, bool hole)
{
    PK_VECTOR_t* points = stackalloc PK_VECTOR_t[8];
    points[0] = new(dx - 2 * scale, -2 * scale, z); points[1] = new(dx + 2 * scale, -2 * scale, z);
    points[2] = new(dx + 2 * scale, 2 * scale, z); points[3] = new(dx - 2 * scale, 2 * scale, z);
    points[4] = new(dx - .5 * scale, -.5 * scale, z); points[5] = new(dx + .5 * scale, -.5 * scale, z);
    points[6] = new(dx + .5 * scale, .5 * scale, z); points[7] = new(dx - .5 * scale, .5 * scale, z);
    int* loops = stackalloc int[8] { 0, 0, 0, 0, 1, 1, 1, 1 };
    var options = new PK_BODY_create_sheet_planar_o_t { loops = hole ? loops : null }; options.plane.location.coord[2] = z;
    int body = 0; Check(self ? KernelRuntime.BodyCreateSheetPlanar(hole ? 8 : 4, (M.PK_VECTOR_s*)points, (M.PK_BODY_create_sheet_planar_o_s*)&options, &body)
        : PK_BODY_create_sheet_planar(hole ? 8 : 4, points, &options, &body), "planar profile"); return body;
}
static unsafe int PolygonWire(bool self, double z, double scale, double dx)
{
    PK_VECTOR_t* points = stackalloc PK_VECTOR_t[4];
    points[0] = new(dx - 2 * scale, -2 * scale, z); points[1] = new(dx + 2 * scale, -2 * scale, z);
    points[2] = new(dx + 2 * scale, 2 * scale, z); points[3] = new(dx - 2 * scale, 2 * scale, z);
    int* curves = stackalloc int[4]; PK_INTERVAL_t* ranges = stackalloc PK_INTERVAL_t[4];
    for (var i = 0; i < 4; i++)
    {
        var next = (i + 1) % 4; var x = points[next].coord[0] - points[i].coord[0]; var y = points[next].coord[1] - points[i].coord[1];
        var length = Math.Sqrt(x * x + y * y); var line = new PK_LINE_sf_t(new PK_AXIS1_sf_t(points[i], new PK_VECTOR1_t(x / length, y / length, 0)));
        Check(self ? KernelRuntime.LineCreate((M.PK_LINE_sf_s*)&line, &curves[i]) : PK_LINE_create(&line, &curves[i]), "profile wire line"); ranges[i] = new(0, length);
    }
    var options = new PK_CURVE_make_wire_body_o_t { check = PK_LOGICAL_true, tolerance = 1e-8 }; int body = 0, count = 0; int* edges = null;
    Check(self ? KernelRuntime.CurveMakeWireBody2(4, curves, (M.PK_INTERVAL_s*)ranges, (M.PK_CURVE_make_wire_body_o_s*)&options, &body, &count, &edges, null)
        : PK_CURVE_make_wire_body_2(4, curves, ranges, &options, &body, &count, &edges, null), "intermediate wire profile");
    if (edges != null) Check(self ? KernelRuntime.MemoryFree(edges) : PK_MEMORY_free(edges), "free intermediate edges"); return body;
}
static unsafe int ArbitraryProfile(bool self, double z, double[] xy, bool wire)
{
    var count = xy.Length / 2; PK_VECTOR_t* points = stackalloc PK_VECTOR_t[count]; int* loops = stackalloc int[count];
    for (var i = 0; i < count; i++) { points[i] = new(xy[2 * i], xy[2 * i + 1], z); loops[i] = i < 4 ? 0 : 1; }
    int body = 0;
    if (!wire)
    {
        var options = new PK_BODY_create_sheet_planar_o_t { loops = loops }; options.plane.location.coord[2] = z;
        Check(self ? KernelRuntime.BodyCreateSheetPlanar(count, (M.PK_VECTOR_s*)points, (M.PK_BODY_create_sheet_planar_o_s*)&options, &body)
            : PK_BODY_create_sheet_planar(count, points, &options, &body), "convex planar cap"); return body;
    }
    int* curves = stackalloc int[count]; PK_INTERVAL_t* ranges = stackalloc PK_INTERVAL_t[count];
    for (var i = 0; i < count; i++)
    {
        var next = i / 4 * 4 + (i + 1) % 4; var x = points[next].coord[0] - points[i].coord[0]; var y = points[next].coord[1] - points[i].coord[1];
        var length = Math.Sqrt(x * x + y * y); var line = new PK_LINE_sf_t(new PK_AXIS1_sf_t(points[i], new PK_VECTOR1_t(x / length, y / length, 0)));
        Check(self ? KernelRuntime.LineCreate((M.PK_LINE_sf_s*)&line, &curves[i]) : PK_LINE_create(&line, &curves[i]), "convex profile boundary"); ranges[i] = new(0, length);
    }
    var wireOptions = new PK_CURVE_make_wire_body_o_t { allow_disjoint = PK_LOGICAL_true, check = PK_LOGICAL_true, tolerance = 1e-8 };
    int returned = 0; int* edges = null;
    Check(self ? KernelRuntime.CurveMakeWireBody2(count, curves, (M.PK_INTERVAL_s*)ranges, (M.PK_CURVE_make_wire_body_o_s*)&wireOptions, &body, &returned, &edges, null)
        : PK_CURVE_make_wire_body_2(count, curves, ranges, &wireOptions, &body, &returned, &edges, null), "convex intermediate wire");
    if (edges != null) Check(self ? KernelRuntime.MemoryFree(edges) : PK_MEMORY_free(edges), "free convex boundary edges"); return body;
}
static unsafe int Wire(bool self, int curve, double start, double end)
{
    var range = new PK_INTERVAL_t(start, end); int body = 0;
    Check(self ? KernelRuntime.CurveMakeWireBody(curve, *(M.PK_INTERVAL_s*)&range, &body) : PK_CURVE_make_wire_body(curve, range, &body), "curve wire"); return body;
}
static unsafe int SelectVertex(bool self, int count, int* vertices, double x, double y, double z)
{
    for (var i = 0; i < count; i++)
    {
        int point = 0; Check(self ? KernelRuntime.VertexAskPoint(vertices[i], &point) : PK_VERTEX_ask_point(vertices[i], &point), "vertex point");
        PK_POINT_sf_t definition; Check(self ? KernelRuntime.PointAsk(point, (M.PK_POINT_sf_s*)&definition) : PK_POINT_ask(point, &definition), "point position");
        if (Math.Abs(definition.position.coord[0] - x) < 1e-9 && Math.Abs(definition.position.coord[1] - y) < 1e-9 && Math.Abs(definition.position.coord[2] - z) < 1e-9) return vertices[i];
    }
    throw new Exception("Expected start vertex not found");
}
static unsafe int Sew(bool self, int count, int* bodies)
{
    var options = new PK_BODY_sew_bodies_o_t(); int n = 0, u = 0, p = 0; int* output = null; int* unsewn = null; PK_BODY_problem_group_t* problems = null;
    Check(self ? KernelRuntime.BodySewBodies(count, bodies, 1e-7, (M.PK_BODY_sew_bodies_o_s*)&options, &n, &output, &u, &unsewn, &p, (M.PK_BODY_problem_group_s**)&problems)
        : PK_BODY_sew_bodies(count, bodies, 1e-7, &options, &n, &output, &u, &unsewn, &p, &problems), "full-edge sewing");
    Require(n == 1 && u == 0 && p == 0, $"sewing result: sewn={n}, unsewn={u}, problems={p}"); var body = output[0];
    Check(self ? KernelRuntime.MemoryFree(output) : PK_MEMORY_free(output), "free sewn list"); return body;
}
static unsafe PK_UVBOX_t Box(double u0, double v0, double u1, double v1) { var box = new PK_UVBOX_t(); box.param[0] = u0; box.param[1] = v0; box.param[2] = u1; box.param[3] = v1; return box; }
static string ScriptPath([CallerFilePath] string path = "") => path;
static void Check(int error, string operation) { if (error != 0) throw new InvalidOperationException(operation + ": " + error); }
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

static unsafe byte[] TransmitManaged(int body)
{
    var options = new M.PK_PART_transmit_o_s { o_t_version = 4, transmit_format = PK_transmit_format_text_c, transmit_version = 371, transmit_meshes = PK_transmit_meshes_separate_c };
    M.PK_MEMORY_block_s block; Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "self text transmit");
    try { using var stream = new MemoryStream(); for (var b = &block; b != null; b = b->next) stream.Write(new ReadOnlySpan<byte>(b->bytes, checked((int)b->n_bytes))); return stream.ToArray(); }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free self text"); }
}
static unsafe byte[] TransmitReference(int body)
{
    var options = new PK_PART_transmit_o_t { transmit_format = PK_transmit_format_text_c, transmit_version = 371 }; PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1, &body, &options, &block), "reference text transmit");
    try { using var stream = new MemoryStream(); for (var b = &block; b != null; b = b->next) stream.Write(new ReadOnlySpan<byte>(b->bytes, checked((int)b->n_bytes))); return stream.ToArray(); }
    finally { Check(PK_MEMORY_block_f(&block), "free reference text"); }
}
static unsafe int ReceiveOracle(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer); var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count = 0; int* parts = null; Check(PK_PART_receive_b(block, &options, &count, &parts), "real PK receive self XT");
        try { Require(count == 1 && parts != null, "received body count"); return parts[0]; }
        finally { if (parts != null) PK_MEMORY_free(parts); }
    }
}
static unsafe int ReceiveManaged(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new M.PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
        var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c }; int count = 0; int* parts = null;
        Check(KernelRuntime.PartReceiveB(block, (M.PK_PART_receive_o_s*)&options, &count, &parts), "self receive tolerant XT");
        try { Require(count == 1 && parts != null, "self receive body count"); return parts[0]; }
        finally { if (parts != null) Check(KernelRuntime.MemoryFree(parts), "free self receive list"); }
    }
}
static unsafe void RoundTripTolerant(byte[] bytes, int reference, string label, Action<string> log)
{
    var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount);
    var imported = ReceiveManaged(bytes); int copy = 0, referenceCopy = 0;
    Check(KernelRuntime.EntityCopy(imported, &copy), label + " copy");
    Check(KernelRuntime.EntityDelete(1, &imported), label + " delete source");
    Check(PK_ENTITY_copy(reference, &referenceCopy), label + " reference copy");
    var form = new M.PK_TRANSF_sf_s(); form.matrix[0] = form.matrix[5] = form.matrix[10] = form.matrix[15] = 1;
    form.matrix[3] = 2; form.matrix[7] = -1; form.matrix[11] = .5;
    int transform = 0, referenceTransform = 0;
    Check(KernelRuntime.TransfCreate(&form, &transform), label + " transform create");
    Check(PK_TRANSF_create((PK_TRANSF_sf_t*)&form, &referenceTransform), label + " reference transform create");
    int count = 0; int* replaced = null; byte* exact = null;
    Check(KernelRuntime.BodyTransform(copy, transform, 0, &count, &replaced, &exact), label + " transform copy");
    if (replaced != null) KernelRuntime.MemoryFree(replaced); if (exact != null) KernelRuntime.MemoryFree(exact);
    PK_LOGICAL_t* referenceExact = null;
    Check(PK_BODY_transform(referenceCopy, referenceTransform, 0, &count, &replaced, &referenceExact), label + " reference transform");
    if (replaced != null) PK_MEMORY_free(replaced); if (referenceExact != null) PK_MEMORY_free(referenceExact);
    var received = ReceiveOracle(TransmitManaged(copy)); CheckReferenceBody(received, label, log);
    CompareBodies(referenceCopy, received, label, log);
    Check(KernelRuntime.EntityDelete(1, &copy), label + " delete copied body"); Check(KernelRuntime.EntityDelete(1, &transform), label + " delete transform");
    Require(before == (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount), label + " geometry ownership leak");
    Check(PK_ENTITY_delete(1, &referenceCopy), label + " delete reference copy"); Check(PK_ENTITY_delete(1, &referenceTransform), label + " delete reference transform");
    Check(PK_ENTITY_delete(1, &received), label + " delete received body"); log("PASS " + label);
}
static unsafe void ValidateFaceTracking(bool self, PK_TOPOL_track_r_t* tracking, int sideCount, int sections, string label)
{
    Require(tracking->n_track_records == sideCount + 2 && tracking->track_records != null, label + " face tracking record count");
    var sides = 0; var caps = 0;
    for (var i = 0; i < tracking->n_track_records; i++)
    {
        var record = tracking->track_records[i];
        Require(record.track == PK_TOPOL_track_create_c && record.n_product_topols == 1 && record.product_topols != null
            && record.n_original_topols > 0 && record.original_topols != null && record.original_classes != null, label + " malformed face tracking");
        int cls = 0; Check(self ? KernelRuntime.EntityAskClass(record.product_topols[0], &cls) : PK_ENTITY_ask_class(record.product_topols[0], &cls), label + " tracking product class");
        Require(cls == PK_CLASS_face, label + " tracking product is not face");
        var originalClass = record.original_classes[0];
        if (originalClass == PK_CLASS_edge) { Require(record.n_original_topols == sections, label + " section edge associations"); sides++; }
        else { Require(originalClass == PK_CLASS_face && record.n_original_topols == 1, label + " cap face association"); caps++; }
        for (var p = 0; p < record.n_original_topols; p++)
        {
            Check(self ? KernelRuntime.EntityAskClass(record.original_topols[p], &cls) : PK_ENTITY_ask_class(record.original_topols[p], &cls), label + " tracking source class");
            Require(cls == originalClass && record.original_classes[p] == originalClass, label + " source class mismatch");
        }
    }
    Require(sides == sideCount && caps == 2, label + " missing side or cap associations");
}
static unsafe void ValidateSweepTracking(bool self, PK_TOPOL_track_r_t* tracking, int sideCount)
{
    const string label = "body sweep";
    Require(tracking->n_track_records == sideCount + 2 && tracking->track_records != null, label + " face tracking record count");
    var sides = 0; var caps = 0;
    for (var i = 0; i < tracking->n_track_records; i++)
    {
        var record = tracking->track_records[i];
        Require(record.track == PK_TOPOL_track_create_c && record.n_original_topols == 2 && record.n_product_topols == 1
            && record.original_topols != null && record.original_classes != null && record.product_topols != null, label + " malformed tracking");
        int cls = 0; Check(self ? KernelRuntime.EntityAskClass(record.product_topols[0], &cls) : PK_ENTITY_ask_class(record.product_topols[0], &cls), "sweep tracking product");
        Require(cls == PK_CLASS_face, "sweep tracking product is not face");
        if (record.original_classes[0] == PK_CLASS_edge) { Require(record.original_classes[1] == PK_CLASS_edge, "missing sweep path edge"); sides++; }
        else { Require(record.original_classes[0] == PK_CLASS_face && record.original_classes[1] == PK_CLASS_vertex, "missing sweep cap path endpoint"); caps++; }
        for (var p = 0; p < 2; p++)
        {
            Check(self ? KernelRuntime.EntityAskClass(record.original_topols[p], &cls) : PK_ENTITY_ask_class(record.original_topols[p], &cls), "sweep tracking source");
            Require(cls == record.original_classes[p], "sweep tracking class mismatch");
        }
    }
    Require(sides == sideCount && caps == 2, "missing sweep side/cap tracking");
}
static unsafe void CheckLoftHoleRejection(int count, Action<string> log)
{
    int* profiles = stackalloc int[count]; int* starts = stackalloc int[count];
    foreach (var self in new[] { true, false })
    {
        profiles[0] = Polygon(self, 0, 1, 0, true); profiles[count - 1] = Polygon(self, 3, .8, 1, true);
        if (count == 3) profiles[1] = ArbitraryProfile(self, 1.5, new double[] { -2.5,-2.8, 3.1,-2.8, 3.1,2.8, -2.5,2.8, -.4,-.7, 1,-.7, 1,.7, -.4,.7 }, true);
        for (var i = 0; i < count; i++)
        {
            int n = 0; int* vertices = null; Check(self ? KernelRuntime.BodyAskVertices(profiles[i], &n, &vertices) : PK_BODY_ask_vertices(profiles[i], &n, &vertices), "rejected loft profile vertices");
            starts[i] = SelectVertex(self, n, vertices, i == 0 ? -2 : i == count - 1 ? -.6 : -2.5, i == 0 ? -2 : i == count - 1 ? -1.6 : -2.8, i == 0 ? 0 : i == count - 1 ? 3 : 1.5);
            Check(self ? KernelRuntime.MemoryFree(vertices) : PK_MEMORY_free(vertices), "free rejected profile vertices");
        }
        var options = new PK_BODY_make_lofted_body_o_t { simplify = PK_BODY_simplify_no_c, tolerance = 1e-6 };
        options.end_conditions.start.curvature_condition = options.end_conditions.end.curvature_condition = PK_BODY_loft_natural_c;
        PK_BODY_tracked_loft_r_t result;
        Check(self ? KernelRuntime.BodyMakeLoftedBody(count, profiles, starts, (M.PK_BODY_make_lofted_body_o_s*)&options, (M.PK_BODY_tracked_loft_s*)&result)
            : PK_BODY_make_lofted_body(count, profiles, starts, &options, &result), "invalid multi-loop loft status");
        Require(result.status.fault == M.ParasolidConstants.PK_BODY_loft_bad_profile_type_c && result.body == 0 && result.status.fault_topol == profiles[0], "multi-loop loft rejection mismatch");
        Check(self ? KernelRuntime.BodyTrackedLoftResultFree((M.PK_BODY_tracked_loft_s*)&result) : PK_BODY_tracked_loft_r_f(&result), "free rejected loft result");
    }
    log($"PASS native/self {count}-section multi-loop loft rejection parity");
}
static unsafe void CheckReferenceBody(int body, string name, Action<string> log)
{
    var options = new PK_BODY_check_o_t { max_faults = 100 }; int count = 0; PK_check_fault_t* faults = null;
    Check(PK_BODY_check(body, &options, &count, &faults), name + " default BODY_check");
    try { for (var i = 0; i < count; i++) log($"{name} fault={faults[i].state}, entity={faults[i].entity_1}, other={faults[i].entity_2}"); Require(count == 0, name + " faults=" + count); }
    finally { if (faults != null) PK_MEMORY_free(faults); }
}
static unsafe int OracleCount(int body, int kind)
{
    int count = 0; int* entities = null;
    Check(kind switch { 0 => PK_BODY_ask_regions(body, &count, &entities), 1 => PK_BODY_ask_shells(body, &count, &entities),
        2 => PK_BODY_ask_faces(body, &count, &entities), 3 => PK_BODY_ask_edges(body, &count, &entities), _ => PK_BODY_ask_vertices(body, &count, &entities) }, "topology count");
    try { return count; } finally { if (entities != null) PK_MEMORY_free(entities); }
}
static unsafe void CompareBodies(int reference, int actual, string label, Action<string> log)
{
    var options = new PK_DEBUG_BODY_compare_o_t { max_diffs = 64, all_tests = PK_LOGICAL_false, num_tests = PK_LOGICAL_true,
        tol_dev_tests = PK_LOGICAL_true, tol_tests = PK_LOGICAL_true, class_tests = PK_LOGICAL_true, sense_tests = PK_LOGICAL_true,
        missing_tests = PK_LOGICAL_true, acc_dev_tests = PK_LOGICAL_false, non_match_tests = PK_LOGICAL_false };
    PK_DEBUG_BODY_compare_r_t result; Check(PK_DEBUG_BODY_compare(reference, actual, &options, &result), label + " compare");
    try
    {
        log($"{label} compare global={result.global_result}, local={result.local_result}, entries={result.n_global_diffs}, pairs={result.n_face_pairs}");
        for (var i = 0; i < result.n_global_diffs; i++) log($"  global {result.global_diffs[i].diff}: {result.global_diffs[i].n_masters}/{result.global_diffs[i].n_similars}");
        var differences = 0;
        for (var f = 0; f < result.n_face_pairs; f++)
            for (var i = 0; i < result.face_pairs[f].n_local_diffs; i++)
            {
                var diff = result.face_pairs[f].local_diffs[i]; differences++;
                log($"  face {f} diff={diff.diff} entities={diff.master_entity}/{diff.similar_entity} values={diff.master_double:R}/{diff.similar_double:R}");
            }
        Require(result.global_result == PK_DEBUG_global_res_no_diffs_c && result.local_result == PK_DEBUG_local_res_no_diffs_c && differences == 0,
            label + " topology/class/sense/tolerance/missing gate");
    }
    finally { PK_DEBUG_BODY_compare_r_f(&result); }
}
