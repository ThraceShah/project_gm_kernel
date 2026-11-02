#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=ParametricICurveOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Real PK produces an intersection wire from explicit support parameters.
// The managed public receive/eval/transmit APIs consume that original chart;
// real PK then receives both preserved and freshly regenerated managed XT and
// compares each with the independent producer body. The fresh path explicitly
// drops the source-document association. This does not claim a managed
// surface-intersection API.
// Usage: MSBUILDDISABLENODEREUSE=1 dotnet run scripts/ParametricICurveOracle.cs

global using ManagedBodyTag = System.Int32;
global using ManagedCurveTag = System.Int32;
global using ManagedEdgeTag = System.Int32;

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var outputDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,
    "..", "temp_docs", "parametric-icurve-oracle"));
var report = new StringBuilder();
void Log(string text)
{
    Console.WriteLine(text);
    report.AppendLine(text);
}

try
{
    Log("Producer: real PK_SURF_intersect_surf + PK_CURVE_make_wire_body.");
    Log("Managed target: public PK_PART_receive_b -> PK_CURVE_eval D0-D2 -> PK_PART_transmit_b.");
    Log("Acceptance: preserved and fresh-runtime exports -> real PK receive, topology/type checks, native-parameter jets and PK_DEBUG_BODY_compare.");
    if (!ParasolidScriptHost.TryStartSession("Parametric icurve oracle", out var host, out var message))
    {
        Log("NotRun: " + message);
        Environment.ExitCode = 2;
    }
    else
    {
        using (host)
        {
            unsafe
            {
                var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
                Check(KernelRuntime.SessionStart(&start), "managed session start");
                try
                {
                    var allCasesPassed = true;
                    foreach (var (name, reversed, twoBSurfaces) in new[]
                    {
                        ("bsurf-cylinder", false, false),
                        ("cylinder-bsurf", true, false),
                        ("bsurf-bsurf", false, true),
                    })
                    {
                        try
                        {
                            allCasesPassed &= RunCase(name, reversed, twoBSurfaces, outputDirectory, Log);
                        }
                        catch (Exception ex)
                        {
                            Log($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
                            allCasesPassed = false;
                        }
                    }
                    Require(allCasesPassed, "At least one live case failed; see the separate jet and structure results above.");
                    Log("PASS: all three live producer/receive/eval/transmit/receive/compare cases completed.");
                }
                finally
                {
                    Check(KernelRuntime.SessionStop(), "managed session stop");
                }
            }
        }
    }
}
catch (Exception ex)
{
    Log($"FAIL: {ex.GetType().Name}: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    Directory.CreateDirectory(outputDirectory);
    File.WriteAllText(Path.Combine(outputDirectory, "oracle-latest.txt"), report.ToString());
}

static string ScriptPath([CallerFilePath] string path = "") => path;

static unsafe bool RunCase(string name, bool reversed, bool twoBSurfaces,
    string outputDirectory, Action<string> log)
{
    var reference = CreateReferenceWire(reversed, twoBSurfaces);
    var referenceBytes = TransmitReference(reference.Body);
    Directory.CreateDirectory(outputDirectory);
    File.WriteAllBytes(Path.Combine(outputDirectory, name + "-reference.x_t"), referenceBytes);
    var managedBody = ReceiveManaged(referenceBytes);
    var managedCurve = ManagedEdgeCurve(managedBody);
    var referenceCounts = OracleCounts(reference.Body);
    var managedCounts = ManagedCounts(managedBody);
    Require(referenceCounts.Edges == 1 && referenceCounts.Vertices == 2 && referenceCounts.Faces == 0,
        name + " producer did not create the required open one-edge wire");
    Require(referenceCounts == managedCounts,
        $"{name} managed topology mismatch: reference={referenceCounts} managed={managedCounts}");
    var curveRecord = KernelRuntime.GetCurveByTag(managedCurve);
    Require(curveRecord.Class == CurveClass.ICurve, name + " receive did not materialize an executable icurve");
    var lower = reference.Lower;
    var upper = reference.Upper;
    Require(double.IsFinite(curveRecord.TMin) && double.IsFinite(curveRecord.TMax)
        && lower >= curveRecord.TMin && upper <= curveRecord.TMax && upper > lower,
        name + " managed chart does not preserve the complete selected native-parameter interval");
    var jetsMatch = CompareJets(managedCurve, reference.Curve, lower, upper, name + " producer", log);

    Require(KernelRuntime.TryGetReceivedXt(managedBody, out _, out _),
        name + " source association is missing before the preserved export");
    foreach (var fresh in new[] { false, true })
    {
        var stage = fresh ? "fresh-runtime" : "preserved";
        var label = name + " " + stage;
        if (fresh)
        {
            Require(KernelRuntime.TryResolveBodySlot(managedBody, out var bodySlot)
                && !KernelRuntime.Xt.IsOpaque(bodySlot), label + " requires a materialized runtime body");
            // Public transmit otherwise re-encodes the received document.
            // Remove only that adapter copy so this call must traverse the
            // actual runtime topology, B-surfaces, icurve, UVs and chart.
            KernelRuntime.Xt.Drop(bodySlot);
            Require(!KernelRuntime.TryGetReceivedXt(managedBody, out _, out _),
                label + " source association was not removed");
        }
        var managedBytes = TransmitManaged(managedBody);
        File.WriteAllBytes(Path.Combine(outputDirectory, name + "-" + stage + ".x_t"), managedBytes);
        var receivedBody = ReceiveOracle(managedBytes);
        var receivedCurve = OracleEdgeCurve(receivedBody);
        var receivedCounts = OracleCounts(receivedBody);
        Require(referenceCounts == receivedCounts,
            $"{label} PK round-trip topology mismatch: reference={referenceCounts} received={receivedCounts}");
        PK_BODY_type_t referenceType;
        PK_BODY_type_t receivedType;
        Check(PK_BODY_ask_type(reference.Body, &referenceType), label + " producer body type");
        Check(PK_BODY_ask_type(receivedBody, &receivedType), label + " received body type");
        Require(referenceType == PK_BODY_type_wire_c && receivedType == referenceType,
            $"{label} wire body type changed: reference={referenceType} received={receivedType}");
        jetsMatch &= CompareJets(managedCurve, receivedCurve, lower, upper, label, log);
        CompareBodies(reference.Body, receivedBody, label, log);
        log($"PASS {label} structure: topology={receivedCounts}; independent PK body compare completed.");
    }
    return jetsMatch;
}

static unsafe (PK_BODY_t Body, PK_CURVE_t Curve, double Lower, double Upper) CreateReferenceWire(
    bool reversed, bool twoBSurfaces)
{
    // Same warped patch as the repository's ICurveSurfaceMatrix producer:
    // x,y cover [-3,3], and z=-x²*y²/81. Its cylinder section is a true icurve.
    double[] firstPoles =
    [
        -3, -3, -1, 0, -3, 1, 3, -3, -1,
        -3, 0, 1, 0, 0, -1, 3, 0, 1,
        -3, 3, -1, 0, 3, 1, 3, 3, -1,
    ];
    var first = CreateBSurface(firstPoles);
    PK_SURF_t second;
    if (twoBSurfaces)
    {
        // z=0.1*(x²+y²)-0.1. The intersection is a closed regular curve
        // x²*y²/81+0.1*(x²+y²)=0.1 entirely inside both parameter boxes.
        double[] secondPoles =
        [
            -3, -3, 1.7, 0, -3, -0.1, 3, -3, 1.7,
            -3, 0, -0.1, 0, 0, -1.9, 3, 0, -0.1,
            -3, 3, 1.7, 0, 3, -0.1, 3, 3, 1.7,
        ];
        second = CreateBSurface(secondPoles);
    }
    else
    {
        var form = new PK_CYL_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0),
            new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)), 2);
        Check(PK_CYL_create(&form, &second), "reference cylinder create");
    }

    var left = reversed ? second : first;
    var right = reversed ? first : second;
    // The SDK reserves the tolerance member for future use.
    var options = new PK_SURF_intersect_surf_o_t();
    if (!reversed || twoBSurfaces)
    {
        options.have_uvbox_1 = PK_LOGICAL_true;
        Check(PK_SURF_ask_uvbox(left, &options.uvbox_1), "reference left uv box");
    }
    if (reversed || twoBSurfaces)
    {
        options.have_uvbox_2 = PK_LOGICAL_true;
        Check(PK_SURF_ask_uvbox(right, &options.uvbox_2), "reference right uv box");
    }
    int vectorCount = 0, curveCount = 0;
    PK_VECTOR_t* vectors = null;
    PK_CURVE_t* curves = null;
    PK_INTERVAL_t* bounds = null;
    PK_intersect_curve_t* kinds = null;
    Check(PK_SURF_intersect_surf(left, right, &options, &vectorCount, &vectors,
        &curveCount, &curves, &bounds, &kinds), "reference surface intersection");
    try
    {
        for (var i = 0; i < curveCount; i++)
        {
            PK_CLASS_t kind;
            Check(PK_ENTITY_ask_class(curves[i], &kind), "reference intersection class");
            if (kind != PK_CLASS_icurve) continue;
            var lower = bounds[i].value[0];
            var upper = bounds[i].value[1];
            if (!double.IsFinite(lower) || !double.IsFinite(upper) || !(upper > lower)) continue;
            // Trim a proper subinterval so the body is open even when the
            // underlying intersection is periodic. Preserve its native t.
            var start = lower + 0.17 * (upper - lower);
            var end = lower + 0.63 * (upper - lower);
            PK_BODY_t body;
            Check(PK_CURVE_make_wire_body(curves[i], new PK_INTERVAL_t(start, end), &body),
                "reference icurve wire create");
            return (body, OracleEdgeCurve(body), start, end);
        }
        throw new InvalidOperationException($"Reference producer returned no finite PK_CLASS_icurve (curves={curveCount}).");
    }
    finally
    {
        if (vectors != null) Check(PK_MEMORY_free(vectors), "free intersection points");
        if (curves != null) Check(PK_MEMORY_free(curves), "free intersection curves");
        if (bounds != null) Check(PK_MEMORY_free(bounds), "free intersection intervals");
        if (kinds != null) Check(PK_MEMORY_free(kinds), "free intersection kinds");
    }
}

static unsafe PK_BSURF_t CreateBSurface(double[] poles)
{
    double* knots = stackalloc double[2] { 0, 1 };
    int* multiplicities = stackalloc int[2] { 3, 3 };
    fixed (double* vertices = poles)
    {
        var form = new PK_BSURF_sf_t(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices,
            PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots,
            PK_knot_bezier_ends_c, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false,
            PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c);
        PK_BSURF_t surface;
        Check(PK_BSURF_create(&form, &surface), "reference B-surface create");
        return surface;
    }
}

static unsafe byte[] TransmitReference(PK_BODY_t body)
{
    var options = new PK_PART_transmit_o_t { transmit_format = PK_transmit_format_text_c, transmit_version = 371 };
    var block = new PK_MEMORY_block_t();
    Check(PK_PART_transmit_b(1, &body, &options, &block), "reference text transmit");
    try
    {
        using var bytes = new MemoryStream();
        for (var current = &block; current != null; current = current->next)
            bytes.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        return bytes.ToArray();
    }
    finally { Check(PK_MEMORY_block_f(&block), "free reference text"); }
}

static unsafe byte[] TransmitManaged(ManagedBodyTag body)
{
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c,
    };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "managed text transmit");
    try
    {
        using var bytes = new MemoryStream();
        for (var current = &block; current != null; current = current->next)
            bytes.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        return bytes.ToArray();
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free managed text"); }
}

static unsafe ManagedBodyTag ReceiveManaged(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new M.PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
        var options = new M.PK_PART_receive_o_s
        {
            o_t_version = 1,
            transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        };
        int count = 0;
        ManagedBodyTag* parts = null;
        Check(KernelRuntime.PartReceiveB(block, &options, &count, &parts), "managed public receive");
        try
        {
            Require(count == 1 && parts != null, $"managed receive returned {count} parts");
            return parts[0];
        }
        finally { if (parts != null) Check(KernelRuntime.MemoryFree(parts), "free managed receive list"); }
    }
}

static unsafe PK_BODY_t ReceiveOracle(byte[] bytes)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count = 0;
        PK_PART_t* parts = null;
        Check(PK_PART_receive_b(block, &options, &count, &parts), "PK receive managed text");
        try
        {
            Require(count == 1 && parts != null, $"PK receive returned {count} parts");
            return parts[0];
        }
        finally { if (parts != null) Check(PK_MEMORY_free(parts), "free PK receive list"); }
    }
}

static unsafe ManagedCurveTag ManagedEdgeCurve(ManagedBodyTag body)
{
    int count = 0;
    ManagedEdgeTag* edges = null;
    Check(KernelRuntime.BodyAskEdges(body, &count, &edges), "managed wire edges");
    try
    {
        Require(count == 1 && edges != null, $"managed wire has {count} edges");
        ManagedCurveTag curve = 0;
        Check(KernelRuntime.EdgeAskCurve(edges[0], &curve), "managed wire curve");
        return curve;
    }
    finally { if (edges != null) Check(KernelRuntime.MemoryFree(edges), "free managed edges"); }
}

static unsafe PK_CURVE_t OracleEdgeCurve(PK_BODY_t body)
{
    int count = 0;
    PK_EDGE_t* edges = null;
    Check(PK_BODY_ask_edges(body, &count, &edges), "PK wire edges");
    try
    {
        Require(count == 1 && edges != null, $"PK wire has {count} edges");
        PK_CURVE_t curve;
        Check(PK_EDGE_ask_curve(edges[0], &curve), "PK wire curve");
        PK_CLASS_t kind;
        Check(PK_ENTITY_ask_class(curve, &kind), "PK wire curve class");
        Require(kind == PK_CLASS_icurve, $"PK wire curve class changed to {kind}");
        return curve;
    }
    finally { if (edges != null) Check(PK_MEMORY_free(edges), "free PK edges"); }
}

static unsafe bool CompareJets(ManagedCurveTag managedCurve, PK_CURVE_t referenceCurve,
    double lower, double upper, string label, Action<string> log)
{
    var actual = stackalloc M.PK_VECTOR_s[3];
    var expected = stackalloc PK_VECTOR_t[3];
    var maxima = new double[3];
    const int samples = 49;
    var nonzeroD2 = false;
    string? firstMismatch = null;
    double maxTangentialD2 = 0, maxNormalD2 = 0;
    // Second pass reverses query order and therefore exercises warm public
    // evaluation without allowing an earlier result to replace another t.
    for (var pass = 0; pass < 2; pass++)
    for (var index = 0; index < samples; index++)
    {
        var sample = pass == 0 ? index : samples - 1 - index;
        var t = lower + (upper - lower) * (sample + 0.37) / samples;
        Check(KernelRuntime.CurveEval(managedCurve, t, 2, actual), label + $" managed eval t={t:R}");
        Check(PK_CURVE_eval(referenceCurve, t, 2, expected), label + $" PK eval t={t:R}");
        for (var order = 0; order <= 2; order++)
        for (var axis = 0; axis < 3; axis++)
        {
            var a = actual[order].coord[axis];
            var e = expected[order].coord[axis];
            Require(double.IsFinite(a) && double.IsFinite(e), $"{label} non-finite jet at t={t:R}, order={order}, axis={axis}");
            var error = Math.Abs(a - e) / Math.Max(1, Math.Abs(e));
            maxima[order] = Math.Max(maxima[order], error);
            var tolerance = order == 2 ? 1e-5 : 1e-7;
            if (error > tolerance)
                firstMismatch ??= $"{label} t={t:R} order={order} axis={axis}: managed={a:R}, PK={e:R}, relative error={error:R}";
            if (order == 2 && Math.Abs(e) > 1e-6) nonzeroD2 = true;
        }
        var tx = expected[1].coord[0]; var ty = expected[1].coord[1]; var tz = expected[1].coord[2];
        var speed = Math.Sqrt(tx * tx + ty * ty + tz * tz);
        Require(double.IsFinite(speed) && speed > 0, label + " has no regular reference tangent");
        tx /= speed; ty /= speed; tz /= speed;
        var dx = actual[2].coord[0] - expected[2].coord[0];
        var dy = actual[2].coord[1] - expected[2].coord[1];
        var dz = actual[2].coord[2] - expected[2].coord[2];
        var tangential = dx * tx + dy * ty + dz * tz;
        dx -= tangential * tx; dy -= tangential * ty; dz -= tangential * tz;
        var normal = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        Require(double.IsFinite(tangential) && double.IsFinite(normal), label + " has a non-finite D2 decomposition");
        maxTangentialD2 = Math.Max(maxTangentialD2, Math.Abs(tangential));
        maxNormalD2 = Math.Max(maxNormalD2, normal);
    }
    Require(nonzeroD2, label + " did not exercise any nonzero second derivative");
    log($"{label}: {samples} native parameters, cold/reversed warm pass; normalized max error D0={maxima[0]:R}, D1={maxima[1]:R}, D2={maxima[2]:R}.");
    log($"{label} D2 mismatch decomposition: max normal={maxNormalD2:R}, max tangential={maxTangentialD2:R} (diagnostics only; full-vector tolerance remains mandatory).");
    // Collect finite jet mismatches so fresh-runtime export and the remaining
    // cases still run. The original tolerances remain mandatory: any mismatch
    // keeps this case, and the complete oracle, failing.
    if (firstMismatch is not null) log("FAIL jets: " + firstMismatch);
    return firstMismatch is null;
}

static unsafe void CompareBodies(PK_BODY_t reference, PK_BODY_t actual, string label, Action<string> log)
{
    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        num_tests = PK_LOGICAL_true,
        tol_dev_tests = PK_LOGICAL_true,
        tol_tests = PK_LOGICAL_true,
        class_tests = PK_LOGICAL_true,
        sense_tests = PK_LOGICAL_true,
        missing_tests = PK_LOGICAL_true,
        // Exact deviation and matching depend on internal parameterization.
        // Native-parameter D0-D2 and topology are separately strict gates.
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var result = new PK_DEBUG_BODY_compare_r_t();
    Check(PK_DEBUG_BODY_compare(reference, actual, &options, &result), label + " body compare");
    try
    {
        log($"{label} body compare: global={result.global_result}, local={result.local_result}, global entries={result.n_global_diffs}, face pairs={result.n_face_pairs}.");
        for (var i = 0; i < result.n_global_diffs; i++)
        {
            var diff = result.global_diffs[i];
            log($"  global entry={diff.diff}, master count={diff.n_masters}, received count={diff.n_similars}");
        }
        var localDifferences = 0;
        for (var i = 0; i < result.n_face_pairs; i++)
        for (var j = 0; j < result.face_pairs[i].n_local_diffs; j++)
        {
            var diff = result.face_pairs[i].local_diffs[j];
            localDifferences++;
            log($"  face pair={i}, diff={diff.diff}, master entity={diff.master_entity}, received entity={diff.similar_entity}, master value={diff.master_double:R}, received value={diff.similar_double:R}");
        }
        // PK includes equal-count records in global_diffs even when its
        // global and local result flags both report no differences.
        Require(result.global_result == PK_DEBUG_global_res_no_diffs_c
            && result.local_result == PK_DEBUG_local_res_no_diffs_c
            && localDifferences == 0, label + " body structure/sense/tolerance comparison failed");
    }
    finally { Check(PK_DEBUG_BODY_compare_r_f(&result), label + " free body compare"); }
}

static unsafe BodyCounts OracleCounts(PK_BODY_t body)
    => new(OracleCount(body, 0), OracleCount(body, 1), OracleCount(body, 2), OracleCount(body, 3), OracleCount(body, 4));

static unsafe BodyCounts ManagedCounts(ManagedBodyTag body)
    => new(ManagedCount(body, 0), ManagedCount(body, 1), ManagedCount(body, 2), ManagedCount(body, 3), ManagedCount(body, 4));

static unsafe int OracleCount(PK_BODY_t body, int kind)
{
    int count = 0;
    int* entities = null;
    var error = kind switch
    {
        0 => PK_BODY_ask_regions(body, &count, &entities),
        1 => PK_BODY_ask_shells(body, &count, &entities),
        2 => PK_BODY_ask_faces(body, &count, &entities),
        3 => PK_BODY_ask_edges(body, &count, &entities),
        _ => PK_BODY_ask_vertices(body, &count, &entities),
    };
    Check(error, "PK topology count " + kind);
    try { return count; }
    finally { if (entities != null) Check(PK_MEMORY_free(entities), "free PK topology list"); }
}

static unsafe int ManagedCount(ManagedBodyTag body, int kind)
{
    int count = 0;
    int* entities = null;
    var error = kind switch
    {
        0 => KernelRuntime.BodyAskRegions(body, &count, &entities),
        1 => KernelRuntime.BodyAskShells(body, &count, &entities),
        2 => KernelRuntime.BodyAskFaces(body, &count, &entities),
        3 => KernelRuntime.BodyAskEdges(body, &count, &entities),
        _ => KernelRuntime.BodyAskVertices(body, &count, &entities),
    };
    Check(error, "managed topology count " + kind);
    try { return count; }
    finally { if (entities != null) Check(KernelRuntime.MemoryFree(entities), "free managed topology list"); }
}

static void Check(int error, string operation)
{
    if (error != 0) throw new InvalidOperationException($"{operation} failed: {error}");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

readonly record struct BodyCounts(int Regions, int Shells, int Faces, int Edges, int Vertices);
