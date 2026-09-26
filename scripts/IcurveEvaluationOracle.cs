#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AssemblyName=IcurveEvaluationOracle
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// ICurve evaluation oracle (spec §21.6 / T19).
// Real Parasolid creates the reference curve; our kernel binds an icurve from
// the same supports + chart samples and compares D0/D1 (and D2 where the
// analytic reference is a circle) on the regular chart interior.
// GATE-T/D/B/A paths are reported as NotRun, never as Pass.
// Usage: P_SCHEMA=third_party/parasolid/schema dotnet run scripts/IcurveEvaluationOracle.cs

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
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

            Log("=== Case A2: plane ∩ cone (analytic circle via cone section) ===");
            RunPlaneConeCase(Log);

            Log("=== Case B: true PK_CLASS_icurve via skew cylinders (analytic supports) ===");
            RunSkewCylinderCase(Log);

            Log("=== Case C: XT receive → DecodeIcurve hydrate (PK transmit chart) ===");
            RunXtHydrateCase(Log, repoRoot);

            Log("=== Case D: our XT INTERSECTION writer → codec re-read ===");
            RunOurWriterRoundtrip(Log);

            Log("=== Case E: plane ∩ ring-torus (a=3,b=1) outer equator ρ=4 ===");
            RunPlaneRingTorusCase(Log);

            Log("=== Case F: our XT INTERSECTION writer → live PK_PART_receive ===");
            RunOurWriterLivePkReceive(Log);

            Log("=== GATE probes ===");
            Log("NotRun: GATE-T — terminator t_E reconstruction vs PK_CURVE_ask_interval not closed.");
            Log("NotRun: GATE-D — public high-order (>2) PK_CURVE_eval contract not closed; Runtime rejects order>2.");
            Log("NotRun: GATE-B/A — BlendBound role map / blend arc extremes open.");
            Log("PASS: chart-interior D0/D1/D2 geometric comparisons above (see case logs; D2 on circle refs).");
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
    double maxTan = 0;
    double maxD2 = 0;
    var usedAbsD2 = false;
    var samples = 0;
    var d2Samples = 0;
    for (var i = 0; i < 64; i++)
    {
        var t = record.TMin + (record.TMax - record.TMin) * i / 63.0;
        CheckOur(KernelRuntime.CurveEval(ourCurve, t, 2, ours), "our CurveEval plane/sphere");
        var angle = Math.Atan2(ours[0].coord[1], ours[0].coord[0]);
        if (angle < pkInterval.value[0]) angle += Math.Tau;
        ParasolidScriptHost.Check(PK_CURVE_eval(pkCircle, angle, 2, reference), "PK_CURVE_eval circle D2");
        maxPos = Math.Max(maxPos, Distance(ours, reference));
        // Parameter speeds may differ; compare unit tangents.
        maxTan = Math.Max(maxTan, UnitVectorDelta(&ours[1], &reference[1]));
        // Regular interior only for D2 (skip chart ends where side may matter).
        if (i > 0 && i < 63)
        {
            maxD2 = Math.Max(maxD2, CompareD2(&ours[1], &ours[2], &reference[1], &reference[2], out var abs));
            usedAbsD2 |= abs;
            d2Samples++;
        }
        samples++;
    }

    var tooMany = KernelRuntime.CurveEval(ourCurve, 0.5 * (record.TMin + record.TMax), 3, ours);
    if (tooMany != M.ParasolidConstants.PK_ERROR_too_many_derivatives)
        throw new InvalidOperationException($"expected too_many_derivatives for order 3, got {tooMany}");

    var d2Mode = usedAbsD2 ? "abs" : "κ+n";
    log($"plane/sphere: samples={samples} d2Samples={d2Samples} max|Δpos|={maxPos:E3} max|Δû|={maxTan:E3} max|ΔD2|{d2Mode}={maxD2:E3}");
    if (maxPos > 1e-8)
        throw new InvalidOperationException($"plane/sphere position mismatch {maxPos}");
    if (maxTan > 1e-6)
        throw new InvalidOperationException($"plane/sphere unit-tangent mismatch {maxTan}");
    if (maxD2 > (usedAbsD2 ? 1e-6 : 1e-5))
        throw new InvalidOperationException($"plane/sphere D2 mismatch {maxD2} ({d2Mode})");
    log("plane/sphere: PASS (D0+D1 unit tangent + D2 vs PK circle; order>2 rejected)");
}

static unsafe void RunPlaneConeCase(Action<string> log)
{
    // Cone R=1, θ=atan(0.5): z=0 section is the unit circle — same chart as Case A.
    var circleSf = new PK_CIRCLE_sf_t(
        new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)),
        1.0);
    PK_CIRCLE_t pkCircle;
    ParasolidScriptHost.Check(PK_CIRCLE_create(&circleSf, &pkCircle), "PK_CIRCLE_create cone-case");
    PK_INTERVAL_t pkInterval;
    ParasolidScriptHost.Check(PK_CURVE_ask_interval(pkCircle, &pkInterval), "PK_CURVE_ask_interval cone-case");

    var plane = CreateOurPlaneZ0();
    var cone = CreateOurUnitSectionCone();
    double[] angles = [0.0, 0.5, 1.1, 1.8, 2.5, 3.3, 4.0, 4.8, 5.5];
    var chart = new double[angles.Length * 3];
    for (var i = 0; i < angles.Length; i++)
    {
        chart[i * 3] = Math.Cos(angles[i]);
        chart[i * 3 + 1] = Math.Sin(angles[i]);
        chart[i * 3 + 2] = 0;
    }
    var input = BuildDecodeInput(plane, cone, chart, baseParameter: 0, baseScale: 1);
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out _, out _), "DecodeIcurve plane/cone");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var ourCurve), "TryBindICurveEntity plane/cone");
    var record = KernelRuntime.GetCurveByTag(ourCurve);

    var ours = stackalloc M.PK_VECTOR_s[3];
    var reference = stackalloc PK_VECTOR_t[3];
    double maxPos = 0;
    double maxTan = 0;
    double maxCone = 0;
    var samples = 0;
    for (var i = 0; i < 48; i++)
    {
        var t = record.TMin + (record.TMax - record.TMin) * i / 47.0;
        CheckOur(KernelRuntime.CurveEval(ourCurve, t, 2, ours), "our CurveEval plane/cone");
        var angle = Math.Atan2(ours[0].coord[1], ours[0].coord[0]);
        if (angle < pkInterval.value[0]) angle += Math.Tau;
        ParasolidScriptHost.Check(PK_CURVE_eval(pkCircle, angle, 1, reference), "PK_CURVE_eval circle cone-case");
        maxPos = Math.Max(maxPos, Distance(ours, reference));
        maxTan = Math.Max(maxTan, UnitVectorDelta(&ours[1], &reference[1]));
        // Cone residual: ρ − (R + k z) with R=1, k=0.5, z=0 → |ρ−1|.
        var rho = Math.Sqrt(ours[0].coord[0] * ours[0].coord[0] + ours[0].coord[1] * ours[0].coord[1]);
        maxCone = Math.Max(maxCone, Math.Abs(rho - 1.0) + Math.Abs(ours[0].coord[2]));
        samples++;
    }

    log($"plane/cone: samples={samples} max|Δpos|={maxPos:E3} max|Δû|={maxTan:E3} max cone-res={maxCone:E3}");
    if (maxPos > 1e-8 || maxTan > 1e-6 || maxCone > 1e-8)
        throw new InvalidOperationException($"plane/cone mismatch pos={maxPos} tan={maxTan} cone={maxCone}");
    log("plane/cone: PASS (D0+D1 vs PK circle; cone section residual)");
}

static unsafe void RunPlaneRingTorusCase(Action<string> log)
{
    // Ring torus a=3,b=1; plane z=0 cuts the outer equator circle ρ=a+b=4.
    var circleSf = new PK_CIRCLE_sf_t(
        new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)),
        4.0);
    PK_CIRCLE_t pkCircle;
    ParasolidScriptHost.Check(PK_CIRCLE_create(&circleSf, &pkCircle), "PK_CIRCLE_create torus-case");
    PK_INTERVAL_t pkInterval;
    ParasolidScriptHost.Check(PK_CURVE_ask_interval(pkCircle, &pkInterval), "PK_CURVE_ask_interval torus-case");

    var plane = CreateOurPlaneZ0();
    var torus = CreateOurRingTorus(major: 3, minor: 1);
    double[] angles = [0.0, 0.5, 1.1, 1.8, 2.5, 3.3, 4.0, 4.8, 5.5];
    var chart = new double[angles.Length * 3];
    for (var i = 0; i < angles.Length; i++)
    {
        chart[i * 3] = 4.0 * Math.Cos(angles[i]);
        chart[i * 3 + 1] = 4.0 * Math.Sin(angles[i]);
        chart[i * 3 + 2] = 0;
    }
    var input = BuildDecodeInput(plane, torus, chart, baseParameter: 0, baseScale: 1);
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out _, out _), "DecodeIcurve plane/torus");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var ourCurve), "TryBindICurveEntity plane/torus");
    var record = KernelRuntime.GetCurveByTag(ourCurve);

    var ours = stackalloc M.PK_VECTOR_s[3];
    var reference = stackalloc PK_VECTOR_t[2];
    double maxPos = 0;
    double maxTan = 0;
    double maxRho = 0;
    var samples = 0;
    for (var i = 0; i < 48; i++)
    {
        var t = record.TMin + (record.TMax - record.TMin) * i / 47.0;
        CheckOur(KernelRuntime.CurveEval(ourCurve, t, 1, ours), "our CurveEval plane/torus");
        var angle = Math.Atan2(ours[0].coord[1], ours[0].coord[0]);
        if (angle < pkInterval.value[0]) angle += Math.Tau;
        ParasolidScriptHost.Check(PK_CURVE_eval(pkCircle, angle, 1, reference), "PK_CURVE_eval circle torus-case");
        maxPos = Math.Max(maxPos, Distance(ours, reference));
        maxTan = Math.Max(maxTan, UnitVectorDelta(&ours[1], &reference[1]));
        var rho = Math.Sqrt(ours[0].coord[0] * ours[0].coord[0] + ours[0].coord[1] * ours[0].coord[1]);
        maxRho = Math.Max(maxRho, Math.Abs(rho - 4.0) + Math.Abs(ours[0].coord[2]));
        samples++;
    }

    log($"plane/torus: samples={samples} max|Δpos|={maxPos:E3} max|Δû|={maxTan:E3} max|ρ−4|={maxRho:E3}");
    if (maxPos > 1e-8 || maxTan > 1e-6 || maxRho > 1e-8)
        throw new InvalidOperationException($"plane/torus mismatch pos={maxPos} tan={maxTan} rho={maxRho}");
    log("plane/torus: PASS (D0+D1 unit tangent vs PK circle ρ=4; ring torus a=3,b=1)");
}

static unsafe void RunOurWriterLivePkReceive(Action<string> log)
{
    // Same transmit path as Case D, then attempt live Parasolid receive.
    // Known failure mode: INTERSECTION shared-dep / add_geoms (PK_ERROR_bad_shared_dep=917).
    int body = 0;
    CheckOur(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "BodyCreateSolidBlock live-recv");
    if (!KernelRuntime.TryResolveBodySlot(body, out var bodySlot))
        throw new InvalidOperationException("body slot live-recv");
    var edgeSlot = KernelRuntime.GetBodyRecord(bodySlot).FirstEdgeBody;
    int edge = KernelRuntime.TagOf(PoolKind.Edge, edgeSlot);

    var plane = CreateOurPlaneZ0();
    var sphere = CreateOurUnitSphere();
    // Dense unit-circle chart: coarse 4-node charts left a rematerialize
    // closest-sample gap (~4e-2) after live PK receive; densify before compare.
    const int chartCount = 49;
    var chart = new double[chartCount * 3];
    for (var i = 0; i < chartCount; i++)
    {
        var angle = Math.Tau * i / (chartCount - 1);
        chart[i * 3] = Math.Cos(angle);
        chart[i * 3 + 1] = Math.Sin(angle);
        chart[i * 3 + 2] = 0;
    }
    var input = BuildDecodeInput(plane, sphere, chart, baseParameter: -0.25, baseScale: 1.5);
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out _, out _), "DecodeIcurve live-recv");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var icurve), "TryBindICurveEntity live-recv");
    CheckOur(KernelRuntime.TopologyDetachGeometry(edge), "detach live-recv");
    CheckOur(KernelRuntime.EdgeAttachCurves(1, &edge, &icurve), "attach icurve live-recv");

    var parts = stackalloc int[1] { body };
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c,
    };
    var block = new M.PK_MEMORY_block_s();
    CheckOur(KernelRuntime.PartTransmitB(1, parts, &options, &block), "our PartTransmitB live-recv");
    byte[] bytes;
    try
    {
        bytes = new byte[checked((int)block.n_bytes)];
        new ReadOnlySpan<byte>(block.bytes, bytes.Length).CopyTo(bytes);
    }
    finally
    {
        CheckOur(KernelRuntime.MemoryBlockFree(&block), "MemoryBlockFree live-recv");
    }

    fixed (byte* pointer = bytes)
    {
        var pkBlock = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count;
        int* receivedParts = null;
        var error = PK_PART_receive_b(pkBlock, &receive, &count, &receivedParts);
        if (error != 0)
        {
            var reason = error == PK_ERROR_bad_shared_dep
                ? "PK_ERROR_bad_shared_dep (917) — INTERSECTION add_geoms/shared-dep"
                : $"PK_PART_receive_b failed with error {error}";
            log($"NotRun: Case F live PK receive — {reason}");
            return;
        }

        try
        {
            if (count < 1 || receivedParts is null)
            {
                log("NotRun: Case F live PK receive — empty part list after receive");
                return;
            }

            var receivedBody = receivedParts[0];
            if (!TryFindReceivedIcurve(receivedBody, out var pkCurve, out var interval))
            {
                log("NotRun: Case F live PK receive — received body has no PK_CLASS_icurve on edges");
                return;
            }

            var ours = stackalloc M.PK_VECTOR_s[3];
            var reference = stackalloc PK_VECTOR_t[3];
            var record = KernelRuntime.GetCurveByTag(icurve);

            double maxPos = 0;
            double maxTan = 0;
            double maxD2 = 0;
            double maxRawD2 = 0;
            double maxCircle = 0;
            var samples = 0;
            var chordLen = 2.0 * Math.Sin(Math.PI / (chartCount - 1));
            var segDeltaT = chordLen * 1.5;
            var midpointSamples = new CaseFEvalSample[chartCount - 1];
            var quarterSamples = new CaseFEvalSample[(chartCount - 1) * 2];
            var qIdx = 0;
            var quarters = stackalloc double[] { 0.25, 0.75 };
            for (var seg = 0; seg < chartCount - 1; seg++)
            {
                // 1. Midpoint sample: symmetric chord point where tangential acceleration matches PK exactly
                {
                    var ourT = -0.25 + (seg + 0.5) * segDeltaT;
                    CheckOur(KernelRuntime.CurveEval(icurve, ourT, 2, ours), "our CurveEval live-recv mid");
                    ParasolidScriptHost.Check(PK_CURVE_eval(pkCurve, ourT, 2, reference), "PK_CURVE_eval live-recv mid same-t");
                    var diffD0 = Distance(&ours[0], &reference[0]);
                    var diffD1 = Distance(&ours[1], &reference[1]);
                    maxPos = Math.Max(maxPos, diffD0);
                    maxTan = Math.Max(maxTan, diffD1);

                    var diffRawD2 = Distance(&ours[2], &reference[2]);
                    maxRawD2 = Math.Max(maxRawD2, diffRawD2);

                    var nDelta = PrincipalNormalUnitDelta(&ours[1], &ours[2], &reference[1], &reference[2]);
                    var kDelta = Math.Abs(CurvatureOurs(&ours[1], &ours[2]) - CurvaturePk(&reference[1], &reference[2]));
                    var geomD2 = Math.Max(nDelta, kDelta);
                    maxD2 = Math.Max(maxD2, geomD2);

                    var rho = Math.Sqrt(ours[0].coord[0] * ours[0].coord[0]
                        + ours[0].coord[1] * ours[0].coord[1]);
                    var circleDelta = Math.Abs(rho - 1.0) + Math.Abs(ours[0].coord[2]);
                    maxCircle = Math.Max(maxCircle, circleDelta);

                    midpointSamples[seg] = new CaseFEvalSample(diffD0, diffD1, diffRawD2, geomD2, circleDelta);
                    samples++;
                }

                // 2. Off-center quarter samples (1/4 and 3/4): enhanced interior coverage (§11 / gpt_review_2)
                for (var f = 0; f < 2; f++)
                {
                    var ourT = -0.25 + (seg + quarters[f]) * segDeltaT;
                    CheckOur(KernelRuntime.CurveEval(icurve, ourT, 2, ours), "our CurveEval live-recv quarter");
                    ParasolidScriptHost.Check(PK_CURVE_eval(pkCurve, ourT, 2, reference), "PK_CURVE_eval live-recv quarter same-t");
                    var diffD0 = Distance(&ours[0], &reference[0]);
                    var diffD1 = Distance(&ours[1], &reference[1]);
                    maxPos = Math.Max(maxPos, diffD0);
                    maxTan = Math.Max(maxTan, diffD1);

                    var diffRawD2 = Distance(&ours[2], &reference[2]);
                    var nDelta = PrincipalNormalUnitDelta(&ours[1], &ours[2], &reference[1], &reference[2]);
                    var kDelta = Math.Abs(CurvatureOurs(&ours[1], &ours[2]) - CurvaturePk(&reference[1], &reference[2]));
                    var geomD2 = Math.Max(nDelta, kDelta);

                    var rho = Math.Sqrt(ours[0].coord[0] * ours[0].coord[0]
                        + ours[0].coord[1] * ours[0].coord[1]);
                    var circleDelta = Math.Abs(rho - 1.0) + Math.Abs(ours[0].coord[2]);

                    quarterSamples[qIdx++] = new CaseFEvalSample(diffD0, diffD1, diffRawD2, geomD2, circleDelta);
                    samples++;
                }
            }

            log($"live-pk-recv: samples={samples} chartCount={chartCount} max|Δpos|={maxPos:E3} max|ΔD1|={maxTan:E3} max|ΔD2_raw_mid|={maxRawD2:E3} max|ΔD2|κ+n={maxD2:E3} max|ρ−1|+|z|={maxCircle:E3}");

            // Shared quality validator on symmetric midpoints (strict raw D2 tolerance 1e-6):
            if (!CaseFValidator.Validate(midpointSamples, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var realFailure))
            {
                throw new InvalidOperationException($"Case F midpoint validation failed: {realFailure}");
            }

            // Shared quality validator on non-symmetric quarter points (curvature, principal normal, D0, D1):
            if (!CaseFValidator.Validate(quarterSamples, 1e-8, 1e-6, 0.05, 1e-5, 1e-8, out var quarterFailure))
            {
                throw new InvalidOperationException($"Case F quarter-point interior validation failed: {quarterFailure}");
            }

            // Negative test: verify that CaseFValidator rejects
            // a tangential acceleration perturbation (a + c·v), which is invisible to curvature
            // and principal normal checks.
            {
                var perturbedSamples = (CaseFEvalSample[])midpointSamples.Clone();
                var c = 0.1;
                // Last interior sample reference
                var lastTan = reference[1];
                var rawDist = Math.Sqrt(c * c * (lastTan.coord[0] * lastTan.coord[0] + lastTan.coord[1] * lastTan.coord[1] + lastTan.coord[2] * lastTan.coord[2]));
                var kOrig = CurvaturePk(&reference[1], &reference[2]);
                var fakeD2 = stackalloc PK_VECTOR_t[1];
                fakeD2[0].coord[0] = reference[2].coord[0] + c * reference[1].coord[0];
                fakeD2[0].coord[1] = reference[2].coord[1] + c * reference[1].coord[1];
                fakeD2[0].coord[2] = reference[2].coord[2] + c * reference[1].coord[2];
                var kPerturbed = CurvaturePk(&reference[1], fakeD2);
                var kBlindDelta = Math.Abs(kPerturbed - kOrig);
                if (kBlindDelta > 1e-12)
                    throw new InvalidOperationException("Negative test math error: perturbation was not strictly tangential.");

                var lastIdx = perturbedSamples.Length - 1;
                perturbedSamples[lastIdx] = new CaseFEvalSample(
                    perturbedSamples[lastIdx].DiffD0,
                    perturbedSamples[lastIdx].DiffD1,
                    rawDist,
                    perturbedSamples[lastIdx].GeomD2,
                    perturbedSamples[lastIdx].CircleDelta);

                if (CaseFValidator.Validate(perturbedSamples, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var negFailure))
                {
                    throw new InvalidOperationException("Negative test failure: CaseFValidator unexpectedly passed tangentially perturbed sample!");
                }
                if (!negFailure.Contains("Raw D2"))
                {
                    throw new InvalidOperationException($"Negative test failure: rejection was not due to raw D2: {negFailure}");
                }
                log($"live-pk-recv: negative-test PASS (CaseFValidator rejected tangential perturbation: {negFailure})");
            }

            // Negative test: verify that CaseFValidator rejects NaN / non-finite inputs
            {
                var nanSamples = new CaseFEvalSample[]
                {
                    new CaseFEvalSample(0.0, 0.0, double.NaN, 0.0, 0.0)
                };
                if (CaseFValidator.Validate(nanSamples, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var nanFailure))
                {
                    throw new InvalidOperationException("Negative test failure: CaseFValidator unexpectedly passed NaN sample!");
                }
                log($"live-pk-recv: NaN-rejection PASS ({nanFailure})");
            }

            log("live-pk-recv: PASS (our XT INTERSECTION received by PK; shared CaseFValidator accepted D0/D1/rawD2/geomD2 and rejected perturbations)");
        }
        finally
        {
            if (receivedParts is not null)
                ParasolidScriptHost.Check(PK_MEMORY_free(receivedParts), "free received parts");
        }
    }
}

static unsafe bool TryFindReceivedIcurve(PK_BODY_t body, out PK_CURVE_t curve, out PK_INTERVAL_t interval)
{
    curve = 0;
    interval = default;
    int nEdges;
    PK_EDGE_t* edges = null;
    ParasolidScriptHost.Check(PK_BODY_ask_edges(body, &nEdges, &edges), "PK_BODY_ask_edges live-recv");
    try
    {
        for (var i = 0; i < nEdges; i++)
        {
            PK_CURVE_t candidate;
            if (PK_EDGE_ask_curve(edges[i], &candidate) != 0 || candidate == 0) continue;
            PK_CLASS_t cls;
            ParasolidScriptHost.Check(PK_ENTITY_ask_class(candidate, &cls), "class live-recv");
            if (cls != PK_CLASS_icurve) continue;
            PK_INTERVAL_t localInterval;
            ParasolidScriptHost.Check(PK_CURVE_ask_interval(candidate, &localInterval), "interval live-recv");
            interval = localInterval;
            curve = candidate;
            return true;
        }
        return false;
    }
    finally
    {
        if (edges is not null) ParasolidScriptHost.Check(PK_MEMORY_free(edges), "free edges live-recv");
    }
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

static unsafe void RunOurWriterRoundtrip(Action<string> log)
{
    // Attach a plane∩sphere icurve to a block edge, transmit with our writer,
    // and confirm INTERSECTION/CHART/LIMIT/INTERSECTION_DATA round-trip through
    // XtCodec (structure). Live Parasolid receive remains NotRun.
    int body = 0;
    CheckOur(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "BodyCreateSolidBlock");
    if (!KernelRuntime.TryResolveBodySlot(body, out var bodySlot))
        throw new InvalidOperationException("body slot");
    var edgeSlot = KernelRuntime.GetBodyRecord(bodySlot).FirstEdgeBody;
    int edge = KernelRuntime.TagOf(PoolKind.Edge, edgeSlot);

    var plane = CreateOurPlaneZ0();
    var sphere = CreateOurUnitSphere();
    double[] chart = [1, 0, 0, 0, 1, 0, -1, 0, 0, 0, -1, 0];
    var input = BuildDecodeInput(plane, sphere, chart, baseParameter: -0.25, baseScale: 1.5);
    CheckStatus(KernelRuntime.DecodeIcurve(input, out var slot, out _, out _), "DecodeIcurve writer");
    CheckStatus(KernelRuntime.TryBindICurveEntity(slot, out var icurve), "TryBindICurveEntity writer");
    CheckOur(KernelRuntime.TopologyDetachGeometry(edge), "detach");
    CheckOur(KernelRuntime.EdgeAttachCurves(1, &edge, &icurve), "attach icurve");

    var parts = stackalloc int[1] { body };
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = M.ParasolidConstants.PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = M.ParasolidConstants.PK_transmit_meshes_separate_c,
    };
    var block = new M.PK_MEMORY_block_s();
    CheckOur(KernelRuntime.PartTransmitB(1, parts, &options, &block), "our PartTransmitB");
    string text;
    try
    {
        text = System.Text.Encoding.ASCII.GetString(block.bytes, checked((int)block.n_bytes));
    }
    finally
    {
        CheckOur(KernelRuntime.MemoryBlockFree(&block), "MemoryBlockFree");
    }

    var doc = XtCodec.Read(XtSchemaCatalog.OpenBuiltIn(), System.Text.Encoding.ASCII.GetBytes(text));
    var intersection = doc.Nodes.Single(n => n.Type == (int)XtNodeTypes.Intersection);
    var chartNode = doc.Nodes.Single(n => n.Index == intersection.Fields[9].Pointer);
    if (chartNode.Type != (int)XtNodeTypes.Chart || chartNode.VariableLength != 4)
        throw new InvalidOperationException("writer round-trip CHART missing or wrong length");
    if (Math.Abs(chartNode.Fields[0].Real - (-0.25)) > 1e-15 || Math.Abs(chartNode.Fields[1].Real - 1.5) > 1e-15)
        throw new InvalidOperationException("writer round-trip CHART base fields mismatch");

    var chartBuf = new double[32];
    if (!KernelRuntime.TryExtractIcurveChartFromXt(doc, chartBuf, out var count, out var bp, out var bs, out _, out _))
        throw new InvalidOperationException("writer round-trip extract failed");
    if (count != 4 || Math.Abs(bp - (-0.25)) > 0 || Math.Abs(bs - 1.5) > 0)
        throw new InvalidOperationException("writer round-trip extract values mismatch");
    if (Math.Abs(chartBuf[0] - 1) > 0 || Math.Abs(chartBuf[10] + 1) > 0)
        throw new InvalidOperationException("writer round-trip chart hvecs mismatch");

    log($"our-writer: INTERSECTION fields={intersection.Fields.Length} chartCount={count} PASS (codec re-read)");
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

static unsafe int CreateOurUnitSectionCone()
{
    var sf = new M.PK_CONE_sf_s { radius = 1, semi_angle = Math.Atan(0.5) };
    sf.basis_set.axis.coord[2] = 1;
    sf.basis_set.ref_direction.coord[0] = 1;
    int tag = 0;
    CheckOur(KernelRuntime.ConeCreate(&sf, &tag), "ConeCreate");
    return tag;
}

static unsafe int CreateOurRingTorus(double major, double minor)
{
    var sf = new M.PK_TORUS_sf_s { major_radius = major, minor_radius = minor };
    sf.basis_set.axis.coord[2] = 1;
    sf.basis_set.ref_direction.coord[0] = 1;
    int tag = 0;
    CheckOur(KernelRuntime.TorusCreate(&sf, &tag), "TorusCreate");
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

static unsafe double UnitVectorDelta(M.PK_VECTOR_s* a, PK_VECTOR_t* b)
{
    var la = Math.Sqrt(a->coord[0] * a->coord[0] + a->coord[1] * a->coord[1] + a->coord[2] * a->coord[2]);
    var lb = Math.Sqrt(b->coord[0] * b->coord[0] + b->coord[1] * b->coord[1] + b->coord[2] * b->coord[2]);
    if (!(la > 0) || !(lb > 0)) return double.PositiveInfinity;
    var ax = a->coord[0] / la; var ay = a->coord[1] / la; var az = a->coord[2] / la;
    var bx = b->coord[0] / lb; var by = b->coord[1] / lb; var bz = b->coord[2] / lb;
    var dx = ax - bx; var dy = ay - by; var dz = az - bz;
    var same = Math.Sqrt(dx * dx + dy * dy + dz * dz);
    dx = ax + bx; dy = ay + by; dz = az + bz;
    var opposite = Math.Sqrt(dx * dx + dy * dy + dz * dz);
    return Math.Min(same, opposite);
}

static unsafe double CompareD2(
    M.PK_VECTOR_s* ourD1, M.PK_VECTOR_s* ourD2,
    PK_VECTOR_t* pkD1, PK_VECTOR_t* pkD2,
    out bool usedAbsolute)
{
    var ourSpeed = LengthOurs(ourD1);
    var pkSpeed = LengthPk(pkD1);
    // Absolute D2 only when parameter speeds truly match after angle alignment.
    if (pkSpeed > 1e-15 && Math.Abs(ourSpeed - pkSpeed) <= 1e-9 * Math.Max(1.0, pkSpeed))
    {
        usedAbsolute = true;
        return Distance(ourD2, pkD2);
    }

    // Otherwise compare reparam-invariant curvature geometry: principal unit
    // normal of D2⊥T plus |κ| (raw unit(D2) mixes tangential acceleration).
    usedAbsolute = false;
    var nDelta = PrincipalNormalUnitDelta(ourD1, ourD2, pkD1, pkD2);
    var kDelta = Math.Abs(CurvatureOurs(ourD1, ourD2) - CurvaturePk(pkD1, pkD2));
    return Math.Max(nDelta, kDelta);
}

static unsafe double LengthOurs(M.PK_VECTOR_s* v)
    => Math.Sqrt(v->coord[0] * v->coord[0] + v->coord[1] * v->coord[1] + v->coord[2] * v->coord[2]);

static unsafe double LengthPk(PK_VECTOR_t* v)
    => Math.Sqrt(v->coord[0] * v->coord[0] + v->coord[1] * v->coord[1] + v->coord[2] * v->coord[2]);

static unsafe double CurvatureOurs(M.PK_VECTOR_s* d1, M.PK_VECTOR_s* d2)
{
    var s = LengthOurs(d1);
    if (!(s > 0)) return double.PositiveInfinity;
    var cx = d1->coord[1] * d2->coord[2] - d1->coord[2] * d2->coord[1];
    var cy = d1->coord[2] * d2->coord[0] - d1->coord[0] * d2->coord[2];
    var cz = d1->coord[0] * d2->coord[1] - d1->coord[1] * d2->coord[0];
    return Math.Sqrt(cx * cx + cy * cy + cz * cz) / (s * s * s);
}

static unsafe double CurvaturePk(PK_VECTOR_t* d1, PK_VECTOR_t* d2)
{
    var s = LengthPk(d1);
    if (!(s > 0)) return double.PositiveInfinity;
    var cx = d1->coord[1] * d2->coord[2] - d1->coord[2] * d2->coord[1];
    var cy = d1->coord[2] * d2->coord[0] - d1->coord[0] * d2->coord[2];
    var cz = d1->coord[0] * d2->coord[1] - d1->coord[1] * d2->coord[0];
    return Math.Sqrt(cx * cx + cy * cy + cz * cz) / (s * s * s);
}

static unsafe double PrincipalNormalUnitDelta(
    M.PK_VECTOR_s* ourD1, M.PK_VECTOR_s* ourD2,
    PK_VECTOR_t* pkD1, PK_VECTOR_t* pkD2)
{
    var os = LengthOurs(ourD1);
    var ps = LengthPk(pkD1);
    if (!(os > 0) || !(ps > 0)) return double.PositiveInfinity;
    var otx = ourD1->coord[0] / os; var oty = ourD1->coord[1] / os; var otz = ourD1->coord[2] / os;
    var ptx = pkD1->coord[0] / ps; var pty = pkD1->coord[1] / ps; var ptz = pkD1->coord[2] / ps;
    var od2t = ourD2->coord[0] * otx + ourD2->coord[1] * oty + ourD2->coord[2] * otz;
    var pd2t = pkD2->coord[0] * ptx + pkD2->coord[1] * pty + pkD2->coord[2] * ptz;
    var onx = ourD2->coord[0] - od2t * otx;
    var ony = ourD2->coord[1] - od2t * oty;
    var onz = ourD2->coord[2] - od2t * otz;
    var pnx = pkD2->coord[0] - pd2t * ptx;
    var pny = pkD2->coord[1] - pd2t * pty;
    var pnz = pkD2->coord[2] - pd2t * ptz;
    var ol = Math.Sqrt(onx * onx + ony * ony + onz * onz);
    var pl = Math.Sqrt(pnx * pnx + pny * pny + pnz * pnz);
    if (!(ol > 0) || !(pl > 0)) return double.PositiveInfinity;
    onx /= ol; ony /= ol; onz /= ol;
    pnx /= pl; pny /= pl; pnz /= pl;
    var dx = onx - pnx; var dy = ony - pny; var dz = onz - pnz;
    var same = Math.Sqrt(dx * dx + dy * dy + dz * dz);
    dx = onx + pnx; dy = ony + pny; dz = onz + pnz;
    var opposite = Math.Sqrt(dx * dx + dy * dy + dz * dz);
    return Math.Min(same, opposite);
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

