#!/usr/bin/env dotnet run
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AllowUnsafeBlocks=true
#:property AssemblyName=IcurveEvalBenchmark

// ICurve evaluation micro-benchmark skeleton (spec §22, task T20 subset).
// Cold/hot wall times for ChartPoint and RegularChartInterval through the
// Runtime PK_CURVE_eval path (decode → bind → prepare → eval). Not a publish
// gate — oracle/correctness thresholds wait on T19 closure.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
// Same P_SCHEMA normalization as VerifyKernel — SessionStart needs the schema dir.
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
    KernelRuntime.SessionStop();
    var options = new PK_SESSION_start_o_s { o_t_version = 1 };
    var start = KernelRuntime.SessionStart(&options);
    if (start != 0)
        throw new InvalidOperationException($"SessionStart failed: {start}");

    var planeSf = new PK_PLANE_sf_s();
    planeSf.basis_set.axis.coord[2] = 1;
    planeSf.basis_set.ref_direction.coord[0] = 1;
    int plane = 0;
    if (KernelRuntime.PlaneCreate(&planeSf, &plane) != 0)
        throw new InvalidOperationException("PlaneCreate failed");

    var sphereSf = new PK_SPHERE_sf_s();
    sphereSf.basis_set.axis.coord[2] = 1;
    sphereSf.basis_set.ref_direction.coord[0] = 1;
    sphereSf.radius = 1;
    int sphere = 0;
    if (KernelRuntime.SphereCreate(&sphereSf, &sphere) != 0)
        throw new InvalidOperationException("SphereCreate failed");

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
        Surface0Tag = plane,
        Surface1Tag = sphere,
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
        || KernelRuntime.TryBindICurveEntity(slot, out var curve) != AlgorithmStatus.Success)
        throw new InvalidOperationException("icurve bind failed");

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

    const int iterations = 500;
    sw.Restart();
    for (var i = 0; i < iterations; i++)
        _ = KernelRuntime.CurveEval(curve, tNode, 0, output);
    var hotNodeNs = sw.Elapsed.TotalNanoseconds / iterations;
    sw.Restart();
    for (var i = 0; i < iterations; i++)
        _ = KernelRuntime.CurveEval(curve, tMid, 2, output);
    var hotMidNs = sw.Elapsed.TotalNanoseconds / iterations;

    KernelRuntime.SessionStop();

    var report = $"""
icurve evaluation benchmark (T20 skeleton)
  fixture: plane ∩ sphere unit circle via Runtime PK_CURVE_eval
  cold ChartPoint D0:              {coldNodeNs:F0} ns
  cold RegularInterval D0–D2:      {coldMidNs:F0} ns
  hot  ChartPoint D0 ({iterations}×):  {hotNodeNs:F1} ns/eval
  hot  RegularInterval D0–D2:      {hotMidNs:F1} ns/eval
  note: not a publish gate; oracle/correctness thresholds live in T19/T20 matrix
""";

    Console.Write(report);
    if (!string.IsNullOrWhiteSpace(outPath))
    {
        var full = Path.IsPathRooted(outPath) ? outPath : Path.GetFullPath(Path.Combine(scriptDir, outPath));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, report);
    }
}
