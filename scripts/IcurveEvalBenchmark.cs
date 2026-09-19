#!/usr/bin/env dotnet run
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AllowUnsafeBlocks=true
#:property AssemblyName=IcurveEvalBenchmark

// ICurve evaluation micro-benchmark (spec §22, task T20 subset).
// Cold/hot wall times and sample percentiles for ChartPoint and RegularChartInterval
// through Runtime PK_CURVE_eval (decode → bind → prepare → eval). Not a publish
// gate — oracle/correctness thresholds wait on T19 closure.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
foreach (var name in new[] { "P_SCHEMA", "PARASOLID_SCHEMA_DIR" })
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
        Environment.SetEnvironmentVariable(name, Path.Combine(repoRoot, "third_party", "parasolid", "schema"));
    else if (!Path.IsPathRooted(value))
        Environment.SetEnvironmentVariable(name, Path.GetFullPath(Path.Combine(scriptDir, value)));
}
var outPath = args.SkipWhile(a => a != "--out").Skip(1).FirstOrDefault();

unsafe
{
    string TryHead()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi)!;
            var text = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return string.IsNullOrEmpty(text) ? "unknown" : text;
        }
        catch { return "unknown"; }
    }

    static double Pct(double[] sorted, double q)
    {
        var idx = (int)Math.Clamp(Math.Ceiling(q * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[idx];
    }

    int BindUnitCircleChart(int s0, int s1)
    {
        double[] angles = [0.0, 0.4, 0.9, 1.4, 2.0, 2.6];
        var chart = new double[angles.Length * 3];
        for (var i = 0; i < angles.Length; i++)
        {
            chart[i * 3] = Math.Cos(angles[i]);
            chart[i * 3 + 1] = Math.Sin(angles[i]);
            chart[i * 3 + 2] = 0;
        }

        var input = new IcurveDecodeInput
        {
            Surface0Tag = s0,
            Surface1Tag = s1,
            BaseParameter = 0,
            BaseScale = 1,
            ChartCount = angles.Length,
            ChartHvecs = chart,
            Start = new IcurveLimitInput
            {
                Type = LimitType.Help,
                TermUse = LimitTermUse.Unset,
                Hvecs = [chart[0], chart[1], chart[2]],
            },
            End = new IcurveLimitInput
            {
                Type = LimitType.Help,
                TermUse = LimitTermUse.Unset,
                Hvecs = [chart[^3], chart[^2], chart[^1]],
            },
            UvType = IntersectionUvType.None,
            ChordalError = 1e-4,
            AngularError = 1e-6,
        };

        if (KernelRuntime.DecodeIcurve(input, out var slot, out _, out _) != AlgorithmStatus.Success
            || KernelRuntime.TryBindICurveEntity(slot, out var tag) != AlgorithmStatus.Success)
            throw new InvalidOperationException("icurve bind failed");
        return tag;
    }

    int CreatePlaneSphere()
    {
        var planeSf = new PK_PLANE_sf_s();
        planeSf.basis_set.axis.coord[2] = 1;
        planeSf.basis_set.ref_direction.coord[0] = 1;
        int plane = 0;
        if (KernelRuntime.PlaneCreate(&planeSf, &plane) != 0)
            throw new InvalidOperationException("PlaneCreate failed");

        var sphereSf = new PK_SPHERE_sf_s { radius = 1 };
        sphereSf.basis_set.axis.coord[2] = 1;
        sphereSf.basis_set.ref_direction.coord[0] = 1;
        int sphere = 0;
        if (KernelRuntime.SphereCreate(&sphereSf, &sphere) != 0)
            throw new InvalidOperationException("SphereCreate failed");

        return BindUnitCircleChart(plane, sphere);
    }

    int CreatePlaneCylinder()
    {
        var planeSf = new PK_PLANE_sf_s();
        planeSf.basis_set.axis.coord[2] = 1;
        planeSf.basis_set.ref_direction.coord[0] = 1;
        int plane = 0;
        if (KernelRuntime.PlaneCreate(&planeSf, &plane) != 0)
            throw new InvalidOperationException("PlaneCreate failed");

        var cylSf = new PK_CYL_sf_s { radius = 1 };
        cylSf.basis_set.axis.coord[2] = 1;
        cylSf.basis_set.ref_direction.coord[0] = 1;
        int cyl = 0;
        if (KernelRuntime.CylCreate(&cylSf, &cyl) != 0)
            throw new InvalidOperationException("CylCreate failed");

        return BindUnitCircleChart(plane, cyl);
    }

    int CreatePlaneCone()
    {
        var planeSf = new PK_PLANE_sf_s();
        planeSf.basis_set.axis.coord[2] = 1;
        planeSf.basis_set.ref_direction.coord[0] = 1;
        int plane = 0;
        if (KernelRuntime.PlaneCreate(&planeSf, &plane) != 0)
            throw new InvalidOperationException("PlaneCreate failed");

        var coneSf = new PK_CONE_sf_s { radius = 1, semi_angle = Math.Atan(0.5) };
        coneSf.basis_set.axis.coord[2] = 1;
        coneSf.basis_set.ref_direction.coord[0] = 1;
        int cone = 0;
        if (KernelRuntime.ConeCreate(&coneSf, &cone) != 0)
            throw new InvalidOperationException("ConeCreate failed");

        return BindUnitCircleChart(plane, cone);
    }

    void MeasureFixture(System.Text.StringBuilder sb, string name, Func<int> build)
    {
        var curve = build();
        var record = KernelRuntime.GetCurveByTag(curve);
        var tNode = record.TMin;
        var tMid = 0.5 * (record.TMin + record.TMax);
        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[3];

        var sw = Stopwatch.StartNew();
        _ = KernelRuntime.CurveEval(curve, tNode, 0, output);
        var coldNodeNs = sw.Elapsed.TotalNanoseconds;
        sw.Restart();
        _ = KernelRuntime.CurveEval(curve, tMid, 2, output);
        var coldMidNs = sw.Elapsed.TotalNanoseconds;

        const int warmup = 50;
        const int samples = 400;
        for (var i = 0; i < warmup; i++)
        {
            _ = KernelRuntime.CurveEval(curve, tNode, 0, output);
            _ = KernelRuntime.CurveEval(curve, tMid, 2, output);
        }

        var nodeSamples = new double[samples];
        var midSamples = new double[samples];
        for (var i = 0; i < samples; i++)
        {
            sw.Restart();
            _ = KernelRuntime.CurveEval(curve, tNode, 0, output);
            nodeSamples[i] = sw.Elapsed.TotalNanoseconds;
            sw.Restart();
            _ = KernelRuntime.CurveEval(curve, tMid, 2, output);
            midSamples[i] = sw.Elapsed.TotalNanoseconds;
        }

        Array.Sort(nodeSamples);
        Array.Sort(midSamples);
        sb.AppendLine($"fixture: {name}");
        sb.AppendLine($"  cold ChartPoint D0:         {coldNodeNs:F0} ns");
        sb.AppendLine($"  cold RegularInterval D0–D2: {coldMidNs:F0} ns");
        sb.AppendLine($"  hot  ChartPoint D0:         p50={Pct(nodeSamples, 0.50):F1} p95={Pct(nodeSamples, 0.95):F1} ns/eval (n={samples})");
        sb.AppendLine($"  hot  RegularInterval D0–D2: p50={Pct(midSamples, 0.50):F1} p95={Pct(midSamples, 0.95):F1} ns/eval");
        sb.AppendLine();
    }

    void MeasurePlanMatrix(System.Text.StringBuilder sb)
    {
        // Same correctness gate as KernelTests plan equivalence: cold+hot cost
        // under identical query/order for each named plan (§22 / T20).
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cyl = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        double[] angles = [0.0, 0.17, 0.62, 1.03];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(Math.Cos(angles[i]), Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        if (OriginalChartParameterMap.Build(positions, tangents, -2.0, 1.7,
                parameters, scales, chords, out _, out _) != AlgorithmStatus.Success)
            throw new InvalidOperationException("plan-matrix chart build failed");
        var view = new ICurveView(in plane, 1, in cyl, 1, positions, parameters, scales, chords);
        var tMid = 0.5 * (parameters[0] + parameters[^1]);
        ICurveConstraintPlan[] plans =
        [
            ICurveConstraintPlan.I1,
            ICurveConstraintPlan.P2,
            ICurveConstraintPlan.I3,
            ICurveConstraintPlan.I2,
            ICurveConstraintPlan.P4,
        ];

        sb.AppendLine("plan-cost matrix (plane∩cylinder RegularInterval D0–D2, same t)");
        const int warmup = 40;
        const int samples = 300;
        var sw = Stopwatch.StartNew();
        Span<KernelVector3> derivatives = stackalloc KernelVector3[3];
        foreach (var plan in plans)
        {
            var status = ICurveEvaluation.EvaluateWithPlan(in view, tMid, 2, plan, derivatives, out _);
            if (status != AlgorithmStatus.Success)
            {
                sb.AppendLine($"  {plan}: SKIP status={status}");
                continue;
            }
            for (var i = 0; i < warmup; i++)
                _ = ICurveEvaluation.EvaluateWithPlan(in view, tMid, 2, plan, derivatives, out _);

            var times = new double[samples];
            for (var i = 0; i < samples; i++)
            {
                sw.Restart();
                _ = ICurveEvaluation.EvaluateWithPlan(in view, tMid, 2, plan, derivatives, out _);
                times[i] = sw.Elapsed.TotalNanoseconds;
            }
            Array.Sort(times);
            sb.AppendLine($"  {plan}: p50={Pct(times, 0.50):F1} p95={Pct(times, 0.95):F1} p99={Pct(times, 0.99):F1} ns/eval (n={samples})");
        }
        sb.AppendLine();
    }

    KernelRuntime.SessionStop();
    var options = new PK_SESSION_start_o_s { o_t_version = 1 };
    var start = KernelRuntime.SessionStart(&options);
    if (start != 0)
        throw new InvalidOperationException($"SessionStart failed: {start}");

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("icurve evaluation benchmark (T20)");
    sb.AppendLine($"  RID={System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier} HEAD={TryHead()}");
    sb.AppendLine("  note: not a publish gate; correctness lives in T19 oracle / KernelTests");
    sb.AppendLine();

    MeasureFixture(sb, "plane∩sphere", CreatePlaneSphere);
    MeasureFixture(sb, "plane∩cylinder", CreatePlaneCylinder);
    MeasureFixture(sb, "plane∩cone", CreatePlaneCone);
    MeasurePlanMatrix(sb);

    KernelRuntime.SessionStop();

    var report = sb.ToString();
    Console.Write(report);
    if (!string.IsNullOrWhiteSpace(outPath))
    {
        var full = Path.IsPathRooted(outPath) ? outPath : Path.GetFullPath(Path.Combine(scriptDir, outPath));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, report);
    }
}
