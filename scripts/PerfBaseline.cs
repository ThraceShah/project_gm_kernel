#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=PerfBaseline
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Run with -c Release and DOTNET_TieredCompilation=0 for repeatable A/B runs.
// All three revisions use this exact file. Session setup/teardown is outside
// the timed/allocation interval. Small batches also fit the legacy tag table.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

unsafe
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("P_SCHEMA")))
        Environment.SetEnvironmentVariable("P_SCHEMA", Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ScriptPath())!, "..", "third_party", "parasolid", "schema")));
    Console.WriteLine($"runtime={Environment.Version}; tiered={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default"}");
    Console.WriteLine("Create+delete is a workload comparison: legacy Body delete does NOT reclaim child entities.");
    foreach (var exported in new[] { false, true })
    {
        Measure("block-create", 8, exported, static (abi, _) =>
        {
            int body;
            Check(abi ? ((delegate* unmanaged<double, double, double, PK_AXIS2_sf_s*, int*, int>)
                &KernelExports.PK_BODY_create_solid_block)(1, 2, 3, null, &body)
                : KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
        });
        Measure("block-create-delete", 8, exported, static (abi, _) =>
        {
            int body;
            Check(abi ? ((delegate* unmanaged<double, double, double, PK_AXIS2_sf_s*, int*, int>)
                &KernelExports.PK_BODY_create_solid_block)(1, 2, 3, null, &body)
                : KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            Check(abi ? ((delegate* unmanaged<int, int*, int>)&KernelExports.PK_ENTITY_delete)(1, &body)
                : KernelRuntime.EntityDelete(1, &body));
        });
        Measure("point-create-delete", 512, exported, static (abi, _) =>
        {
            var sf = new PK_POINT_sf_s();
            int point;
            Check(abi ? ((delegate* unmanaged<PK_POINT_sf_s*, int*, int>)&KernelExports.PK_POINT_create)(&sf, &point)
                : KernelRuntime.PointCreate(&sf, &point));
            Check(abi ? ((delegate* unmanaged<int, int*, int>)&KernelExports.PK_ENTITY_delete)(1, &point)
                : KernelRuntime.EntityDelete(1, &point));
        });
        Measure("mark-create-block-goto", 8, exported, static (abi, _) =>
        {
            int mark, body;
            Check(abi ? ((delegate* unmanaged<int*, int>)&KernelExports.PK_MARK_create)(&mark)
                : KernelRuntime.MarkCreate(&mark));
            Check(abi ? ((delegate* unmanaged<double, double, double, PK_AXIS2_sf_s*, int*, int>)
                &KernelExports.PK_BODY_create_solid_block)(1, 2, 3, null, &body)
                : KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body));
            Check(abi ? ((delegate* unmanaged<int, int>)&KernelExports.PK_MARK_goto)(mark)
                : KernelRuntime.MarkGoto(mark));
        });
    }
}

static string ScriptPath([CallerFilePath] string path = "") => path;

static unsafe void Measure(string name, int batchSize, bool exported, Action<bool, int> action)
{
    const int sampleCount = 7, batchesPerSample = 32;
    var samples = new double[sampleCount];
    var allocations = new double[sampleCount];
    for (var batch = 0; batch < 24; batch++) RunBatch();
    for (var sample = 0; sample < sampleCount; sample++)
    {
        long ticks = 0, allocated = 0;
        for (var batch = 0; batch < batchesPerSample; batch++)
        {
            var measurement = RunBatch();
            ticks += measurement.ticks;
            allocated += measurement.allocated;
        }
        samples[sample] = (double)ticks / Stopwatch.Frequency * 1e9 / (batchSize * batchesPerSample);
        allocations[sample] = (double)allocated / (batchSize * batchesPerSample);
    }
    Array.Sort(samples);
    Array.Sort(allocations);
    Console.WriteLine($"{(exported ? "export" : "managed")}/{name}: median={samples[3]:F1} ns/op; " +
        $"min={samples[0]:F1}; max={samples[^1]:F1}; ops/s={1e9 / samples[3]:F0}; " +
        $"managed={allocations[3]:F1} B/op");

    (long ticks, long allocated) RunBatch()
    {
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&options));
        try
        {
            action(exported, 0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < batchSize; i++) action(exported, i);
            var elapsed = Stopwatch.GetTimestamp() - start;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            return (elapsed, bytes);
        }
        finally { Check(KernelRuntime.SessionStop()); }
    }
}

static void Check(int error)
{
    if (error != 0) throw new InvalidOperationException($"kernel error: {error}");
}
