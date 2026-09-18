#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=IcurveEvaluationOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// ICurve evaluation oracle (spec §21.6 / T19).
// Real Parasolid creates the reference curve; our kernel binds an icurve from
// the same supports + chart samples and compares D0 on the regular chart
// interior. GATE-T/D/B/A paths are reported as NotRun, never as Pass.
// Usage: P_SCHEMA=third_party/parasolid/schema dotnet run scripts/IcurveEvaluationOracle.cs

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var report = new StringBuilder();
void Log(string line)
{
    Console.WriteLine(line);
    report.AppendLine(line);
}

if (!ParasolidScriptHost.TryStartSession("ICurve evaluation oracle", out var host, out var message))
{
    Log("NotRun: Parasolid runtime unavailable — " + message);
    WriteReport(repoRoot, report);
    return;
}

using (host)
{
    unsafe
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        CheckOur(KernelRuntime.SessionStart(&start), "our SessionStart");
        try
        {
            Log("=== Case A: plane ∩ sphere (analytic circle as icurve chart) ===");
            RunPlaneSphereCase(Log);

            Log("=== Case B: true PK_CLASS_icurve via skew cylinders (analytic supports) ===");
            RunSkewCylinderCase(Log);

            Log("=== Case C: XT receive → DecodeIcurve hydrate (PK transmit chart) ===");
            RunXtHydrateCase(Log, repoRoot);

            Log("=== GATE probes ===");
            Log("NotRun: GATE-T — terminator t_E reconstruction vs PK_CURVE_ask_interval not closed.");
            Log("NotRun: GATE-D — public high-order (>2) PK_CURVE_eval contract not closed; Runtime rejects order>2.");
            Log("NotRun: GATE-B/A — BlendBound role map / blend arc extremes open.");
            Log("NotRun: XT INTERSECTION writer + live PK_PART_transmit hydrate — pending (add_geoms shared-dep / writer).");
            Log("PASS: chart-interior D0 geometric comparisons above (see case logs).");
        }
        finally
        {
            CheckOur(KernelRuntime.SessionStop(), "our SessionStop");
        }
    }
}

WriteReport(repoRoot, report);

static string GetScriptPath([CallerFilePath] string path = "") => path;

static void WriteReport(string repoRoot, StringBuilder report)
{
    var outDir = Path.Combine(repoRoot, "temp_docs", "icurve-evaluation");
    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "oracle-latest.txt"), report.ToString());
}

static unsafe void RunPlaneSphereCase(Action<string> log)
{
    var circleSf = new PK_CIRCLE_sf_t(
        new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)),
        1.0);
    PK_CIRCLE_t pkCircle;
    ParasolidScriptHost.Check(PK_CIRCLE_create(&circleSf, &pkCircle), "PK_CIRCLE_create");
    PK_INTERVAL_t pkInterval;
    ParasolidScriptHost.Check(PK_CURVE_ask_interval(pkCircle, &pkInterval), "PK_CURVE_ask_interval circle");

    var plane = CreateOurPlaneZ0();
    var sphere = CreateOurUnitSphere();
    double[] angles = [0.0, 0.4, 0.9, 1.4, 2.0, 2.6, 3.2, 3.8, 4.5, 5.2, 5.8];
    var chart = new double[angles.Length * 3];
    for (var i = 0; i < angles.Length; i++)
    {
        chart[i * 3] = Math.Cos(angles[i]);
        chart[i * 3 + 1] = Math.Sin(angles[i]);
        chart[i * 3 + 2] = 0;
    }
    var input = BuildDecodeInput(plane, sphere, chart, baseParameter: 0, baseScale: 1);
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out _, out _), "DecodeIcurve plane/sphere");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var ourCurve), "TryBindICurveEntity plane/sphere");
    var record = KernelRuntime.GetCurveByTag(ourCurve);

    var ours = stackalloc M.PK_VECTOR_s[3];
    var reference = stackalloc PK_VECTOR_t[3];
    double maxPos = 0;
    var samples = 0;
    for (var i = 0; i < 64; i++)
    {
        var t = record.TMin + (record.TMax - record.TMin) * i / 63.0;
        CheckOur(KernelRuntime.CurveEval(ourCurve, t, 2, ours), "our CurveEval plane/sphere");
        var angle = Math.Atan2(ours[0].coord[1], ours[0].coord[0]);
        if (angle < pkInterval.value[0]) angle += Math.Tau;
        ParasolidScriptHost.Check(PK_CURVE_eval(pkCircle, angle, 0, reference), "PK_CURVE_eval circle");
        maxPos = Math.Max(maxPos, Distance(ours, reference));
        samples++;
    }

    var tooMany = KernelRuntime.CurveEval(ourCurve, 0.5 * (record.TMin + record.TMax), 3, ours);
    if (tooMany != M.ParasolidConstants.PK_ERROR_too_many_derivatives)
        throw new InvalidOperationException($"expected too_many_derivatives for order 3, got {tooMany}");

    log($"plane/sphere: samples={samples} max|Δpos|={maxPos:E3}");
    if (maxPos > 1e-8)
        throw new InvalidOperationException($"plane/sphere position mismatch {maxPos}");
    log("plane/sphere: PASS (D0 vs PK circle; order>2 rejected)");
}

static unsafe void RunSkewCylinderCase(Action<string> log)
{
    // Offset, unequal cylinders: analytic∩analytic that Parasolid keeps as I_CURVE
    // (coaxial/symmetric pairs collapse to ellipse/circle).
    var left = CreatePkCylinder(radius: 1.6, axis: (0, 0, 1), location: (0, 0, 0), refDir: (1, 0, 0));
    var right = CreatePkCylinder(radius: 1.1, axis: (1, 0, 0), location: (0, 0.35, 0.2), refDir: (0, 1, 0));
    if (!TryIntersectFaces(left, right, out var pkIcurve, out var interval, out var className))
        throw new InvalidOperationException($"skew cylinders produced no curve (class probe={className})");
    if (className != "icurve")
        throw new InvalidOperationException($"expected PK_CLASS_icurve, got {className}");

    log($"skew-cyl: PK interval=[{interval.value[0]:R}, {interval.value[1]:R}]");

    const int chartCount = 10;
    var chart = new double[chartCount * 3];
    var pkSamples = stackalloc PK_VECTOR_t[1];
    for (var i = 0; i < chartCount; i++)
    {
        var t = interval.value[0] + (interval.value[1] - interval.value[0]) * i / (chartCount - 1);
        ParasolidScriptHost.Check(PK_CURVE_eval(pkIcurve, t, 0, pkSamples), "PK_CURVE_eval chart sample");
        chart[i * 3] = pkSamples[0].coord[0];
        chart[i * 3 + 1] = pkSamples[0].coord[1];
        chart[i * 3 + 2] = pkSamples[0].coord[2];
    }

    var our0 = CreateOurCylinder(radius: 1.6, axis: (0, 0, 1), location: (0, 0, 0), refDir: (1, 0, 0));
    var our1 = CreateOurCylinder(radius: 1.1, axis: (1, 0, 0), location: (0, 0.35, 0.2), refDir: (0, 1, 0));
    var input = BuildDecodeInput(our0, our1, chart, baseParameter: interval.value[0], baseScale: 1);
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out _, out _), "DecodeIcurve skew-cyl");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var ourCurve), "TryBindICurveEntity skew-cyl");
    var record = KernelRuntime.GetCurveByTag(ourCurve);

    var ours = stackalloc M.PK_VECTOR_s[3];
    var reference = stackalloc PK_VECTOR_t[1];
    // Chart anchors must reproduce the PK samples used to build them.
    double maxAnchor = 0;
    for (var i = 0; i < chartCount; i++)
    {
        ParasolidScriptHost.Check(PK_CURVE_eval(pkIcurve,
            interval.value[0] + (interval.value[1] - interval.value[0]) * i / (chartCount - 1), 0, reference),
            "PK chart re-eval");
        var dx = chart[i * 3] - reference[0].coord[0];
        var dy = chart[i * 3 + 1] - reference[0].coord[1];
        var dz = chart[i * 3 + 2] - reference[0].coord[2];
        maxAnchor = Math.Max(maxAnchor, Math.Sqrt(dx * dx + dy * dy + dz * dz));
    }

    double maxRes = 0;
    var samples = 0;
    for (var i = 1; i < 32; i++)
    {
        var alpha = i / 32.0;
        var ourT = record.TMin + (record.TMax - record.TMin) * alpha;
        CheckOur(KernelRuntime.CurveEval(ourCurve, ourT, 0, ours), "our CurveEval skew-cyl");
        maxRes = Math.Max(maxRes, CylinderPairResidual(ours, 1.6, (0, 0, 0), (0, 0, 1), 1.1, (0, 0.35, 0.2), (1, 0, 0)));
        samples++;
    }

    // Ends must land on the defining chart points.
    CheckOur(KernelRuntime.CurveEval(ourCurve, record.TMin, 0, ours), "our CurveEval start");
    var endGap0 = Math.Sqrt(
        (ours[0].coord[0] - chart[0]) * (ours[0].coord[0] - chart[0])
        + (ours[0].coord[1] - chart[1]) * (ours[0].coord[1] - chart[1])
        + (ours[0].coord[2] - chart[2]) * (ours[0].coord[2] - chart[2]));
    CheckOur(KernelRuntime.CurveEval(ourCurve, record.TMax, 0, ours), "our CurveEval end");
    var endGap1 = Math.Sqrt(
        (ours[0].coord[0] - chart[^3]) * (ours[0].coord[0] - chart[^3])
        + (ours[0].coord[1] - chart[^2]) * (ours[0].coord[1] - chart[^2])
        + (ours[0].coord[2] - chart[^1]) * (ours[0].coord[2] - chart[^1]));

    log($"skew-cyl: samples={samples} chart-vs-PK={maxAnchor:E3} endGaps=({endGap0:E3},{endGap1:E3}) max cyl-residual={maxRes:E3}");
    if (maxAnchor > 1e-12 || endGap0 > 1e-10 || endGap1 > 1e-10 || maxRes > 1e-8)
        throw new InvalidOperationException($"skew-cyl geometric failure anchor={maxAnchor} ends=({endGap0},{endGap1}) res={maxRes}");
    log("skew-cyl: PASS (true PK I_CURVE; chart anchors + dual-cylinder residual)");
}

static unsafe void RunXtHydrateCase(Action<string> log, string repoRoot)
{
    // Real Parasolid INTERSECTION/CHART layout via corpus fixture, then the same
    // chart-field conventions feed DecodeIcurve for the analytic skew-cylinder
    // recipe (live face∩face PART_add_geoms often hits bad_shared_dep 917).
    var corpusXt = Path.Combine(repoRoot, "bin/parasolid-xt-corpus/icurve-surface-matrix/icurve.bsurf-cylinder.depth1/model.x_t");
    if (!File.Exists(corpusXt))
    {
        log("NotRun: XT hydrate — corpus fixture missing at " + corpusXt);
        return;
    }

    var doc = XtCodec.Read(XtSchemaCatalog.OpenBuiltIn(), File.ReadAllBytes(corpusXt));
    var chartBuf = new double[256 * 3];
    if (!KernelRuntime.TryExtractIcurveChartFromXt(doc, chartBuf, out var corpusChartCount,
            out var bp, out var bs, out var ch, out var an))
    {
        log("NotRun: XT hydrate — corpus CHART optional-field layout not recognized.");
        return;
    }
    if (corpusChartCount < 2)
        throw new InvalidOperationException("corpus CHART extract too short");

    var left = CreatePkCylinder(radius: 1.6, axis: (0, 0, 1), location: (0, 0, 0), refDir: (1, 0, 0));
    var right = CreatePkCylinder(radius: 1.1, axis: (1, 0, 0), location: (0, 0.35, 0.2), refDir: (0, 1, 0));
    if (!TryIntersectFaces(left, right, out var pkIcurve, out var interval, out var className) || className != "icurve")
        throw new InvalidOperationException("hydrate sample: expected icurve, got " + className);

    const int chartCount = 10;
    var chart = new double[chartCount * 3];
    var pkSamples = stackalloc PK_VECTOR_t[1];
    for (var i = 0; i < chartCount; i++)
    {
        var t = interval.value[0] + (interval.value[1] - interval.value[0]) * i / (chartCount - 1);
        ParasolidScriptHost.Check(PK_CURVE_eval(pkIcurve, t, 0, pkSamples), "hydrate chart sample");
        chart[i * 3] = pkSamples[0].coord[0];
        chart[i * 3 + 1] = pkSamples[0].coord[1];
        chart[i * 3 + 2] = pkSamples[0].coord[2];
    }

    // Apply CHART summary fields extracted from the real Parasolid fixture.
    var our0 = CreateOurCylinder(radius: 1.6, axis: (0, 0, 1), location: (0, 0, 0), refDir: (1, 0, 0));
    var our1 = CreateOurCylinder(radius: 1.1, axis: (1, 0, 0), location: (0, 0.35, 0.2), refDir: (0, 1, 0));
    var input = BuildDecodeInput(our0, our1, chart, baseParameter: interval.value[0], baseScale: bs);
    input.ChordalError = ch;
    input.AngularError = an;
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out var failure, out _),
        "DecodeIcurve XT hydrate (" + failure + ")");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var ourCurve), "TryBindICurveEntity XT hydrate");
    var record = KernelRuntime.GetCurveByTag(ourCurve);

    var ours = stackalloc M.PK_VECTOR_s[1];
    double maxRes = 0;
    var samples = 0;
    for (var i = 1; i < 24; i++)
    {
        var alpha = i / 24.0;
        var ourT = record.TMin + (record.TMax - record.TMin) * alpha;
        CheckOur(KernelRuntime.CurveEval(ourCurve, ourT, 0, ours), "our CurveEval XT hydrate");
        maxRes = Math.Max(maxRes, CylinderPairResidual(ours, 1.6, (0, 0, 0), (0, 0, 1), 1.1, (0, 0.35, 0.2), (1, 0, 0)));
        samples++;
    }

    log($"xt-hydrate: corpusChart={corpusChartCount} fields=(bp={bp:G6},bs={bs:G6},ch={ch:G6},an={an:G6}) samples={samples} max cyl-residual={maxRes:E3}");
    if (maxRes > 1e-8)
        throw new InvalidOperationException($"xt-hydrate residual {maxRes}");
    log("xt-hydrate: PASS (corpus CHART extract + DecodeIcurve with PK chart samples)");
    log("NotRun: XT full INTERSECTION entity hydrate via live PK_PART_transmit (add_geoms shared-dep).");
}

static unsafe bool TryIntersectFaces(PK_SURF_t leftSurf, PK_SURF_t rightSurf,
    out PK_CURVE_t curve, out PK_INTERVAL_t interval, out string className)
{
    curve = 0;
    interval = default;
    className = "none";
    PK_UVBOX_t leftBox, rightBox;
    ParasolidScriptHost.Check(PK_SURF_ask_uvbox(leftSurf, &leftBox), "uvbox");
    ParasolidScriptHost.Check(PK_SURF_ask_uvbox(rightSurf, &rightBox), "uvbox");
    PK_BODY_t leftBody, rightBody;
    ParasolidScriptHost.Check(PK_SURF_make_sheet_body(leftSurf, leftBox, &leftBody), "sheet");
    ParasolidScriptHost.Check(PK_SURF_make_sheet_body(rightSurf, rightBox, &rightBody), "sheet");
    var options = new PK_FACE_intersect_face_o_t();
    int nVectors; PK_VECTOR_t* vectors = null;
    int nCurves; PK_CURVE_t* curves = null;
    PK_INTERVAL_t* bounds = null;
    PK_intersect_curve_t* types = null;
    ParasolidScriptHost.Check(
        PK_FACE_intersect_face(FirstFace(leftBody), FirstFace(rightBody), &options,
            &nVectors, &vectors, &nCurves, &curves, &bounds, &types),
        "PK_FACE_intersect_face");
    try
    {
        for (var i = 0; i < nCurves; i++)
        {
            PK_CLASS_t cls;
            ParasolidScriptHost.Check(PK_ENTITY_ask_class(curves[i], &cls), "class");
            className = cls switch
            {
                _ when cls == PK_CLASS_icurve => "icurve",
                _ when cls == PK_CLASS_circle => "circle",
                _ when cls == PK_CLASS_ellipse => "ellipse",
                _ when cls == PK_CLASS_line => "line",
                _ => cls.ToString(),
            };
            if (cls != PK_CLASS_icurve) continue;
            curve = curves[i];
            interval = bounds[i];
            return true;
        }
        return false;
    }
    finally
    {
        if (vectors is not null) ParasolidScriptHost.Check(PK_MEMORY_free(vectors), "free vectors");
        if (curves is not null) ParasolidScriptHost.Check(PK_MEMORY_free(curves), "free curves");
        if (bounds is not null) ParasolidScriptHost.Check(PK_MEMORY_free(bounds), "free bounds");
        if (types is not null) ParasolidScriptHost.Check(PK_MEMORY_free(types), "free types");
    }
}

static unsafe PK_SURF_t CreatePkCylinder(double radius,
    (double x, double y, double z) axis,
    (double x, double y, double z) location,
    (double x, double y, double z) refDir)
{
    var form = new PK_CYL_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(location.x, location.y, location.z),
            new PK_VECTOR1_t(axis.x, axis.y, axis.z),
            new PK_VECTOR1_t(refDir.x, refDir.y, refDir.z)),
        radius);
    PK_CYL_t cylinder;
    ParasolidScriptHost.Check(PK_CYL_create(&form, &cylinder), "PK_CYL_create");
    return cylinder;
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int nFaces; PK_FACE_t* faces = null;
    ParasolidScriptHost.Check(PK_BODY_ask_faces(body, &nFaces, &faces), "PK_BODY_ask_faces");
    try
    {
        if (nFaces < 1) throw new InvalidOperationException("body has no faces");
        return faces[0];
    }
    finally
    {
        if (faces is not null) ParasolidScriptHost.Check(PK_MEMORY_free(faces), "free faces");
    }
}

static unsafe int CreateOurPlaneZ0()
{
    var sf = new M.PK_PLANE_sf_s();
    sf.basis_set.axis.coord[2] = 1;
    sf.basis_set.ref_direction.coord[0] = 1;
    int tag = 0;
    CheckOur(KernelRuntime.PlaneCreate(&sf, &tag), "PlaneCreate");
    return tag;
}

static unsafe int CreateOurUnitSphere()
{
    var sf = new M.PK_SPHERE_sf_s();
    sf.basis_set.axis.coord[2] = 1;
    sf.basis_set.ref_direction.coord[0] = 1;
    sf.radius = 1;
    int tag = 0;
    CheckOur(KernelRuntime.SphereCreate(&sf, &tag), "SphereCreate");
    return tag;
}

static unsafe int CreateOurCylinder(double radius,
    (double x, double y, double z) axis,
    (double x, double y, double z) location,
    (double x, double y, double z) refDir)
{
    var sf = new M.PK_CYL_sf_s();
    sf.basis_set.location.coord[0] = location.x;
    sf.basis_set.location.coord[1] = location.y;
    sf.basis_set.location.coord[2] = location.z;
    sf.basis_set.axis.coord[0] = axis.x;
    sf.basis_set.axis.coord[1] = axis.y;
    sf.basis_set.axis.coord[2] = axis.z;
    sf.basis_set.ref_direction.coord[0] = refDir.x;
    sf.basis_set.ref_direction.coord[1] = refDir.y;
    sf.basis_set.ref_direction.coord[2] = refDir.z;
    sf.radius = radius;
    int tag = 0;
    CheckOur(KernelRuntime.CylCreate(&sf, &tag), "CylCreate");
    return tag;
}

static IcurveDecodeInput BuildDecodeInput(int s0, int s1, double[] chart, double baseParameter, double baseScale)
    => new()
    {
        Surface0Tag = s0,
        Surface1Tag = s1,
        BaseParameter = baseParameter,
        BaseScale = baseScale,
        ChartCount = chart.Length / 3,
        ChartHvecs = chart,
        Start = new IcurveLimitInput
        {
            Type = LimitType.Help,
            TermUse = LimitTermUse.Unset,
            Hvecs = chart.AsSpan(0, 3).ToArray(),
        },
        End = new IcurveLimitInput
        {
            Type = LimitType.Help,
            TermUse = LimitTermUse.Unset,
            Hvecs = chart.AsSpan(chart.Length - 3, 3).ToArray(),
        },
        UvType = IntersectionUvType.None,
        ChordalError = 1e-4,
        AngularError = 1e-6,
    };

static unsafe double Distance(M.PK_VECTOR_s* a, PK_VECTOR_t* b)
{
    var dx = a->coord[0] - b->coord[0];
    var dy = a->coord[1] - b->coord[1];
    var dz = a->coord[2] - b->coord[2];
    return Math.Sqrt(dx * dx + dy * dy + dz * dz);
}

static unsafe double CylinderPairResidual(M.PK_VECTOR_s* p,
    double r0, (double x, double y, double z) loc0, (double x, double y, double z) axis0,
    double r1, (double x, double y, double z) loc1, (double x, double y, double z) axis1)
{
    static double Radial(M.PK_VECTOR_s* point, (double x, double y, double z) loc, (double x, double y, double z) axis)
    {
        var dx = point->coord[0] - loc.x;
        var dy = point->coord[1] - loc.y;
        var dz = point->coord[2] - loc.z;
        var axial = dx * axis.x + dy * axis.y + dz * axis.z;
        var rx = dx - axial * axis.x;
        var ry = dy - axial * axis.y;
        var rz = dz - axial * axis.z;
        return Math.Sqrt(rx * rx + ry * ry + rz * rz);
    }

    var e0 = Math.Abs(Radial(p, loc0, axis0) - r0);
    var e1 = Math.Abs(Radial(p, loc1, axis1) - r1);
    return Math.Max(e0, e1);
}

static void CheckOur(int error, string name)
{
    if (error != 0) throw new InvalidOperationException($"{name} failed with error {error}");
}

static void CheckStatus(AlgorithmStatus status, string name)
{
    if (status != AlgorithmStatus.Success)
        throw new InvalidOperationException($"{name} failed with status {status}");
}
