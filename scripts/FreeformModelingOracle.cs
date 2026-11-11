#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseOperationAcceptanceLedger=true
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

var selectedCase=args.FirstOrDefault(a=>a.StartsWith("--case="))?[7..];
var output=args.FirstOrDefault(a=>a.StartsWith("--output="))?[9..];
var directory = Path.GetFullPath(output??"../temp_docs/freeform-modeling-oracle",Path.GetDirectoryName(ScriptPath())!);
Directory.CreateDirectory(directory);
var report = new StringBuilder();
var ledger = new OperationAcceptanceLedger("freeform-modeling-oracle", "Freeform Modeling Oracle", directory);
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
                "automatic-grid-surface", "explicit-grid-surface", "convex-changing-sections-loft", "clamped-bicubic-twists", "clamped-multispan-twists",
                "edited-polynomial-surface", "edited-rational-surface", "reparameterised-polynomial-surface", "reparameterised-rational-surface" };
            if(selectedCase!=null&&!names.Contains(selectedCase))throw new ArgumentException("Unknown selected case: "+selectedCase);
            var failures = 0;var executed=0;
            for (var scenario = 0; scenario < names.Length; scenario++)
            {
                var name = names[scenario];
                if(selectedCase!=null&&name!=selectedCase)continue;
                executed++;
                var constructOpId = $"op-{scenario}-construct";
                var constructOp = ledger.BeginOperation(
                    constructOpId,
                    name,
                    "primary",
                    scenario switch
                    {
                        6 or 7 => "PK_BODY_make_swept_body",
                        8 or 9 or 15 => "PK_BODY_make_lofted_body",
                        10 or 11 => "PK_BODY_sew_bodies",
                        12 or 13 or 14 => "PK_SURF_make_sheet_body",
                        >= 20 => "PK_BSURF_reparameterise",
                        >= 18 => "PK_BSURF_insert_knots+PK_BSURF_raise_degree",
                        >= 16 => "PK_SURF_make_sheet_body",
                        _ => "PK_SURF_make_sheet_body"
                    },
                    null,
                    "independent geometric definition parameters; GM text XT vs PK"
                );
                var activeOpId=constructOpId;
                var selfLifecycleId=$"op-{scenario}-self-lifecycle";
                var pkLifecycleId=$"op-{scenario}-pk-lifecycle";
                try
                {
                    var ours = Produce(true, scenario); var reference = Produce(false, scenario);
                    constructOp.EntityReferences["gm"] = new System.Text.Json.Nodes.JsonObject { ["outputEntityTag"] = ours };
                    constructOp.EntityReferences["pk"] = new System.Text.Json.Nodes.JsonObject { ["outputEntityTag"] = reference };
                    var bytes = TransmitManaged(ours); File.WriteAllBytes(Path.Combine(directory, name + "-self.x_t"), bytes);
                    var referenceBytes = TransmitReference(reference);
                    File.WriteAllBytes(Path.Combine(directory, name + "-reference.x_t"), referenceBytes);
                    var received = ReceiveOracle(bytes);
                    ledger.RecordOutputHashes(constructOpId,"gm",[OperationAcceptanceLedger.HashBytes(bytes)]);
                    ledger.RecordOutputHashes(constructOpId,"pk",[OperationAcceptanceLedger.HashBytes(referenceBytes)]);
                    CheckReferenceBody(reference, name + " independent reference", Log, ledger, constructOpId, "auxiliary");
                    CheckReferenceBody(received, name + " self receive", Log, ledger, constructOpId, "primary");
                    for (var kind = 0; kind < 5; kind++)
                    {
                        var actual = OracleCount(received, kind); var expected = OracleCount(reference, kind);
                        Log($"{name} topology {kind}: reference={expected}, self={actual}");
                        var pass = actual == expected;
                        ledger.RecordCheck(constructOpId, "primary", $"{name}-topology-{kind}", $"{name} topology kind {kind}", pass ? "pass" : "fail", gm: actual, pk: expected, threshold: 0);
                        Require(pass, name + " topology mismatch " + kind);
                    }
                    CompareBodies(reference, received, name, Log, ledger, constructOpId);
                    ledger.EndOperation(constructOpId);
                    Log("PASS " + name);
                    if (scenario is 6 or 9)
                    {
                        activeOpId=selfLifecycleId;
                        ledger.BeginOperation(selfLifecycleId, name + " self XT lifecycle", "lifecycle", "integration-lifecycle", null, "GM produced XT received into fresh body and transformed; actual child APIs recorded separately");
                        RoundTripTolerant(bytes, reference, name + " self XT lifecycle", Log, ledger, selfLifecycleId);
                        ledger.EndOperation(selfLifecycleId);

                        activeOpId=pkLifecycleId;
                        ledger.BeginOperation(pkLifecycleId, name + " PK XT lifecycle", "lifecycle", "integration-lifecycle", null, "PK reference XT received into fresh body and transformed; actual child APIs recorded separately");
                        RoundTripTolerant(referenceBytes, reference, name + " PK XT lifecycle", Log, ledger, pkLifecycleId);
                        ledger.EndOperation(pkLifecycleId);
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    ledger.EndOperation(activeOpId, "fail");
                    if(scenario is 6 or 9)
                    {
                        if(ledger.GetOperation(selfLifecycleId)==null)ledger.AddUnreachedOperation(selfLifecycleId,ledger.Operations.Count,name+" self lifecycle not reached","lifecycle");
                        if(ledger.GetOperation(pkLifecycleId)==null)ledger.AddUnreachedOperation(pkLifecycleId,ledger.Operations.Count,name+" PK lifecycle not reached","lifecycle");
                    }
                    Log("FAIL " + name + ": " + ex.Message);
                }
            }
            if(selectedCase==null){CheckLoftHoleRejection(2, Log);CheckLoftHoleRejection(3, Log);}
            Require(failures == 0, $"{failures}/{executed} independent freeform operations failed.");
            Log($"PASS all {executed} selected freeform modeling cases; default PK_BODY_check and structural comparison passed.");
        }
        finally { Check(KernelRuntime.SessionStop(), "self session stop"); }
    }
}
catch (Exception ex) { Log("FAIL: " + ex.Message); Environment.ExitCode = 1; }
finally
{
    ledger.EndScenario(Environment.ExitCode);
    ledger.SaveSidecar(Path.Combine(directory, "ledger.json"));
    ledger.PrintConsoleSummary(Log);
    File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString());
}

static unsafe int Produce(bool self, int scenario)
{
    if(scenario>=18)return EditedSurface(self,(scenario-18)%2!=0,scenario>=20);
    if(scenario>=16)return ConstrainedSurface(self,scenario-14);
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

static unsafe int ConstrainedSurface(bool self,int count)
{
    double* u=stackalloc double[3]{0,.7,2};double* v=stackalloc double[3]{-1,.2,1};if(count==2){u[1]=2;v[1]=1;}
    PK_VECTOR_t* points=stackalloc PK_VECTOR_t[count*count];PK_VECTOR_t* ul=stackalloc PK_VECTOR_t[count];PK_VECTOR_t* uh=stackalloc PK_VECTOR_t[count];PK_VECTOR_t* vl=stackalloc PK_VECTOR_t[count];PK_VECTOR_t* vh=stackalloc PK_VECTOR_t[count];
    for(var j=0;j<count;j++)for(var i=0;i<count;i++)points[j*count+i]=ConstrainedJet(u[i],v[j],0,0);
    for(var i=0;i<count;i++){ul[i]=ConstrainedJet(0,v[i],1,0);uh[i]=ConstrainedJet(2,v[i],1,0);vl[i]=ConstrainedJet(u[i],-1,0,1);vh[i]=ConstrainedJet(u[i],1,0,1);}
    var hu=u[1]-u[0];var hU=u[count-1]-u[count-2];var hv=v[1]-v[0];var hV=v[count-1]-v[count-2];
    for(var i=0;i<count;i++)for(var d=0;d<3;d++){ul[i].coord[d]*=hu;uh[i].coord[d]*=hU;vl[i].coord[d]*=hv;vh[i].coord[d]*=hV;}
    var sf=new PK_BSURF_splinewise_sf_t{u_degree=3,v_degree=3,n_u_positions=count,n_v_positions=count,positions=points,
        u_end=PK_PARAM_end_clamped_vector_c,U_end=PK_PARAM_end_clamped_vector_c,v_end=PK_PARAM_end_clamped_vector_c,V_end=PK_PARAM_end_clamped_vector_c,
        u_derivs=ul,U_derivs=uh,v_derivs=vl,V_derivs=vh,u_how_knot=PK_PARAM_knot_defined_c,v_how_knot=PK_PARAM_knot_defined_c,u_knot=u,v_knot=v,
        u_v=PK_PARAM_twist_yes_c,u_V=PK_PARAM_twist_yes_c,U_v=PK_PARAM_twist_yes_c,U_V=PK_PARAM_twist_yes_c,
        u_v_twist=ConstrainedJet(0,-1,1,1),u_V_twist=ConstrainedJet(0,1,1,1),U_v_twist=ConstrainedJet(2,-1,1,1),U_V_twist=ConstrainedJet(2,1,1,1)};
    for(var d=0;d<3;d++){sf.u_v_twist.coord[d]*=hu*hv;sf.u_V_twist.coord[d]*=hu*hV;sf.U_v_twist.coord[d]*=hU*hv;sf.U_V_twist.coord[d]*=hU*hV;}
    int surface=0;Check(self?KernelRuntime.BSurfCreateSplinewise((M.PK_BSURF_splinewise_sf_s*)&sf,&surface):PK_BSURF_create_splinewise(&sf,&surface),"constrained surface create");
    PK_VECTOR_t* jet=stackalloc PK_VECTOR_t[9];
    for(var i=0;i<=6;i++)for(var j=0;j<=6;j++)
    {
        var uv=new PK_UV_t(i/3.0,-1+j/3.0);Check(self?KernelRuntime.SurfEval(surface,*(M.PK_UV_s*)&uv,2,2,0,(M.PK_VECTOR_s*)jet):PK_SURF_eval(surface,uv,2,2,PK_LOGICAL_false,jet),"constrained surface jets");
        for(var a=0;a<=2;a++)for(var b=0;b<=2;b++)
        {var expected=ConstrainedJet(uv.param[0],uv.param[1],a,b);for(var d=0;d<3;d++)Require(Math.Abs(jet[b*3+a].coord[d]-expected.coord[d])<2e-9,$"independent bicubic jet mismatch self={self} uv={uv.param[0]:R}/{uv.param[1]:R} order={a}/{b} component={d} expected={expected.coord[d]:R} actual={jet[b*3+a].coord[d]:R}");}
    }
    var box=Box(0,-1,2,1);int body=0;Check(self?KernelRuntime.SurfMakeSheetBody(surface,*(M.PK_UVBOX_s*)&box,&body):PK_SURF_make_sheet_body(surface,box,&body),"constrained sheet");return body;
}

static unsafe PK_VECTOR_t ConstrainedJet(double u,double v,int a,int b)
{
    var x=b==0?(a==0?u:a==1?1:0):0;var y=a==0?(b==0?v:b==1?1:0):0;
    var z=(a,b) switch{(0,0)=>u*u*v+u*v*v+.3*u*u*u-.2*v*v*v,(1,0)=>2*u*v+v*v+.9*u*u,(0,1)=>u*u+2*u*v-.6*v*v,
        (2,0)=>2*v+1.8*u,(0,2)=>2*u-1.2*v,(1,1)=>2*u+2*v,(2,1)=>2,(1,2)=>2,_=>0};return new(x,y,z);
}

static unsafe int EditedSurface(bool self,bool rational,bool reparameterise)
{
    var dim=rational?4:3;double* poles=stackalloc double[36];double* z=stackalloc double[3]{.25,-.25,.25};
    for(var u=0;u<3;u++)for(var v=0;v<3;v++)
    {var i=(3*u+v)*dim;var w=rational?1+.2*u+.3*v:1;poles[i]=u*.5*w;poles[i+1]=v*.5*w;poles[i+2]=(z[u]+z[v]-.0625)*w;if(rational)poles[i+3]=w;}
    double* knots=stackalloc double[2]{0,1};int* mult=stackalloc int[2]{3,3};
    var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=dim,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,
        vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,form=PK_BSURF_form_arbitrary_c,
        u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
    int source=0,surface=0;Check(self?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&source):PK_BSURF_create(&sf,&source),"editing source");
    Check(self?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&surface):PK_BSURF_create(&sf,&surface),"editing target");
    Check(self?KernelRuntime.BSurfAddUKnot(surface,.37):PK_BSURF_add_u_knot(surface,.37),"surface insert U");
    Check(self?KernelRuntime.BSurfAddVKnot(surface,.63):PK_BSURF_add_v_knot(surface,.63),"surface insert V");
    var raise=new PK_BSURF_raise_degree_o_t();Check(self?KernelRuntime.BSurfRaiseDegree(surface,PK_PARAM_direction_u_c,1,(M.PK_BSURF_raise_degree_o_s*)&raise):PK_BSURF_raise_degree(surface,PK_PARAM_direction_u_c,1,&raise),"surface elevate U");
    Check(self?KernelRuntime.BSurfRaiseDegree(surface,PK_PARAM_direction_v_c,2,(M.PK_BSURF_raise_degree_o_s*)&raise):PK_BSURF_raise_degree(surface,PK_PARAM_direction_v_c,2,&raise),"surface elevate V");
    if(reparameterise)
    {
        var options=new PK_BSURF_reparameterise_o_t{have_u_interval=PK_LOGICAL_true,have_v_interval=PK_LOGICAL_true,transpose=PK_LOGICAL_true,reverse_u=PK_LOGICAL_true};
        options.u_interval.value[0]=-2;options.u_interval.value[1]=3;options.v_interval.value[0]=7;options.v_interval.value[1]=9;
        Check(self?KernelRuntime.BSurfReparameterise(surface,(M.PK_BSURF_reparameterise_o_s*)&options):PK_BSURF_reparameterise(surface,&options),"surface parameter map");
    }
    PK_UVBOX_t domain;Check(self?KernelRuntime.SurfAskUvbox(surface,(M.PK_UVBOX_s*)&domain):PK_SURF_ask_uvbox(surface,&domain),"edited domain");
    PK_VECTOR_t* actual=stackalloc PK_VECTOR_t[9];PK_VECTOR_t* expected=stackalloc PK_VECTOR_t[9];
    for(var i=0;i<=6;i++)for(var j=0;j<=6;j++)
    {
        var x=i/6.0;var y=j/6.0;var uv=new PK_UV_t(domain.param[0]+(domain.param[2]-domain.param[0])*x,domain.param[1]+(domain.param[3]-domain.param[1])*y);
        var original=new PK_UV_t(reparameterise?y:x,reparameterise?1-x:y);
        Check(self?KernelRuntime.SurfEval(surface,*(M.PK_UV_s*)&uv,2,2,0,(M.PK_VECTOR_s*)actual):PK_SURF_eval(surface,uv,2,2,PK_LOGICAL_false,actual),"edited jets");
        Check(self?KernelRuntime.SurfEval(source,*(M.PK_UV_s*)&original,2,2,0,(M.PK_VECTOR_s*)expected):PK_SURF_eval(source,original,2,2,PK_LOGICAL_false,expected),"original jets");
        for(var a=0;a<=2;a++)for(var b=0;b<=2;b++)for(var d=0;d<3;d++)
        {
            var wanted=reparameterise?expected[a*3+b].coord[d]*Math.Pow(-1/(domain.param[2]-domain.param[0]),a)*Math.Pow(1/(domain.param[3]-domain.param[1]),b):expected[b*3+a].coord[d];
            Require(Math.Abs(wanted-actual[b*3+a].coord[d])<2e-8,$"editing jet mismatch self={self}, map={reparameterise}, derivative={a}/{b}, expected={wanted:R}, actual={actual[b*3+a].coord[d]:R}");
        }
    }
    int body=0;Check(self?KernelRuntime.SurfMakeSheetBody(surface,*(M.PK_UVBOX_s*)&domain,&body):PK_SURF_make_sheet_body(surface,domain,&body),"edited sheet");return body;
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
static unsafe int ReceiveManaged(byte[] bytes,OperationAcceptanceLedger? ledger=null,string? opId=null)
{
    fixed (byte* pointer = bytes)
    {
        var block = new M.PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
        var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c }; int count = 0; int* parts = null;
        var error=KernelRuntime.PartReceiveB(block,(M.PK_PART_receive_o_s*)&options,&count,&parts);
        if(ledger!=null&&opId!=null)ledger.RecordReturn(opId,"gm",error);
        Check(error,"self receive tolerant XT");
        try { Require(count == 1 && parts != null, "self receive body count"); return parts[0]; }
        finally { if (parts != null) Check(KernelRuntime.MemoryFree(parts), "free self receive list"); }
    }
}
static unsafe void RoundTripTolerant(byte[] bytes, int reference, string label, Action<string> log, OperationAcceptanceLedger? ledger = null, string? opId = null)
{
    var before = (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount);
    int imported,copy=0,referenceCopy=0;
    using(var step=new OperationStepScope(ledger,opId,"receive","PK_PART_receive_b"))
    {imported=ReceiveManaged(bytes,ledger,step.Id);step.Complete();}
    using(var step=new OperationStepScope(ledger,opId,"copy-self","PK_ENTITY_copy"))
    {Check(step.Return("gm",KernelRuntime.EntityCopy(imported,&copy)),label+" copy");step.Complete();}
    using(var step=new OperationStepScope(ledger,opId,"delete-source","PK_ENTITY_delete"))
    {Check(step.Return("gm",KernelRuntime.EntityDelete(1,&imported)),label+" delete source");step.Complete();}
    using(var step=new OperationStepScope(ledger,opId,"copy-reference","PK_ENTITY_copy"))
    {Check(step.Return("pk",PK_ENTITY_copy(reference,&referenceCopy)),label+" reference copy");step.Complete();}
    var form = new M.PK_TRANSF_sf_s(); form.matrix[0] = form.matrix[5] = form.matrix[10] = form.matrix[15] = 1;
    form.matrix[3] = 2; form.matrix[7] = -1; form.matrix[11] = .5;
    int transform = 0, referenceTransform = 0;
    using(var step=new OperationStepScope(ledger,opId,"create-transform","PK_TRANSF_create"))
    {
        Check(step.Return("gm",KernelRuntime.TransfCreate(&form,&transform)),label+" transform create");
        Check(step.Return("pk",PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&referenceTransform)),label+" reference transform create");step.Complete();
    }
    int count = 0; int* replaced = null; byte* exact = null;
    PK_LOGICAL_t* referenceExact = null;
    int received;
    using(var step=new OperationStepScope(ledger,opId,"transform","PK_BODY_transform"))
    {
        Check(step.Return("gm",KernelRuntime.BodyTransform(copy,transform,0,&count,&replaced,&exact)),label+" transform copy");
        if(replaced!=null)KernelRuntime.MemoryFree(replaced);if(exact!=null)KernelRuntime.MemoryFree(exact);
        Check(step.Return("pk",PK_BODY_transform(referenceCopy,referenceTransform,0,&count,&replaced,&referenceExact)),label+" reference transform");
        if(replaced!=null)PK_MEMORY_free(replaced);if(referenceExact!=null)PK_MEMORY_free(referenceExact);
        received=ReceiveOracle(TransmitManaged(copy));
        CheckReferenceBody(received,label,log,ledger,step.Id,"primary");CompareBodies(referenceCopy,received,label,log,ledger,step.Id);step.Complete();
    }
    using(var step=new OperationStepScope(ledger,opId,"delete-self-copy","PK_ENTITY_delete"))
    {Check(step.Return("gm",KernelRuntime.EntityDelete(1,&copy)),label+" delete copied body");step.Complete();}
    using(var step=new OperationStepScope(ledger,opId,"delete-self-transform","PK_ENTITY_delete"))
    {Check(step.Return("gm",KernelRuntime.EntityDelete(1,&transform)),label+" delete transform");step.Complete();}
    var leakPass = before == (KernelRuntime.Bodies.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount, KernelRuntime.Points.AliveCount);
    if (ledger != null && opId != null)
    {
        ledger.RecordCheck(opId, "representation", label + "-ownership-leak", label + " geometry ownership leak", leakPass ? "pass" : "fail");
    }
    Require(leakPass, label + " geometry ownership leak");
    using(var step=new OperationStepScope(ledger,opId,"delete-reference-copy","PK_ENTITY_delete"))
    {Check(step.Return("pk",PK_ENTITY_delete(1,&referenceCopy)),label+" delete reference copy");step.Complete();}
    using(var step=new OperationStepScope(ledger,opId,"delete-reference-transform","PK_ENTITY_delete"))
    {Check(step.Return("pk",PK_ENTITY_delete(1,&referenceTransform)),label+" delete reference transform");step.Complete();}
    using(var step=new OperationStepScope(ledger,opId,"delete-received","PK_ENTITY_delete"))
    {Check(step.Return("pk",PK_ENTITY_delete(1,&received)),label+" delete received body");step.Complete();}log("PASS "+label);
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
static unsafe void CheckReferenceBody(int body, string name, Action<string> log, OperationAcceptanceLedger? ledger = null, string? opId = null, string category = "primary")
{
    var options = new PK_BODY_check_o_t { max_faults = 100 }; int count = 0; PK_check_fault_t* faults = null;
    Check(PK_BODY_check(body, &options, &count, &faults), name + " default BODY_check");
    try
    {
        for (var i = 0; i < count; i++) log($"{name} fault={faults[i].state}, entity={faults[i].entity_1}, other={faults[i].entity_2}");
        var pass = count == 0;
        if (ledger != null && opId != null)
        {
            ledger.RecordCheck(opId, category, name + "-body-check", name + " PK_BODY_check", pass ? "pass" : "fail", gm: count, pk: 0, threshold: 0);
        }
        Require(pass, name + " faults=" + count);
    }
    finally { if (faults != null) PK_MEMORY_free(faults); }
}
static unsafe int OracleCount(int body, int kind)
{
    int count = 0; int* entities = null;
    Check(kind switch { 0 => PK_BODY_ask_regions(body, &count, &entities), 1 => PK_BODY_ask_shells(body, &count, &entities),
        2 => PK_BODY_ask_faces(body, &count, &entities), 3 => PK_BODY_ask_edges(body, &count, &entities), _ => PK_BODY_ask_vertices(body, &count, &entities) }, "topology count");
    try { return count; } finally { if (entities != null) PK_MEMORY_free(entities); }
}
static unsafe void CompareBodies(int reference, int actual, string label, Action<string> log, OperationAcceptanceLedger? ledger = null, string? opId = null)
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
        var pass = result.global_result == PK_DEBUG_global_res_no_diffs_c && result.local_result == PK_DEBUG_local_res_no_diffs_c && differences == 0;
        if (ledger != null && opId != null)
        {
            ledger.RecordCheck(opId, "primary", label + "-compare", label + " PK_DEBUG_BODY_compare", pass ? "pass" : "fail");
        }
        Require(pass, label + " topology/class/sense/tolerance/missing gate");
    }
    finally { PK_DEBUG_BODY_compare_r_f(&result); }
}
