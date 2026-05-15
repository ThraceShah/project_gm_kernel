#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=AllocationBaseline
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Diagnostics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

unsafe
{
    Measure("session start/stop", static () =>
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&options), "SessionStart");
        Check(KernelRuntime.SessionStop(), "SessionStop");
    });

    Measure("point create + class query", static () =>
    {
        RestartSession();
        var pointSf = new PK_POINT_sf_s();
        int point;
        Check(KernelRuntime.PointCreate(&pointSf, &point), "PointCreate");
        int cls;
        Check(KernelRuntime.EntityAskClass(point, &cls), "EntityAskClass");
        Check(KernelRuntime.SessionStop(), "SessionStop");
    });

    Measure("transform create", static () =>
    {
        RestartSession();
        var transformSf = new PK_TRANSF_sf_s();
        for (int i = 0; i < 16; i++)
            transformSf.matrix[i] = i % 5 == 0 ? 1 : 0;
        int transform;
        Check(KernelRuntime.TransfCreate(&transformSf, &transform), "TransfCreate");
        Check(KernelRuntime.SessionStop(), "SessionStop");
    });

    Measure("block + topology queries", static () =>
    {
        RestartSession();
        int body;
        Check(KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body), "BodyCreateSolidBlock");
        int count;
        int* tags;
        Check(KernelRuntime.BodyAskFaces(body, &count, &tags), "BodyAskFaces");
        Check(KernelRuntime.BodyAskEdges(body, &count, &tags), "BodyAskEdges");
        Check(KernelRuntime.BodyAskVertices(body, &count, &tags), "BodyAskVertices");
        Check(KernelRuntime.SessionStop(), "SessionStop");
    });

    Measure("mark rollback", static () =>
    {
        RestartSession();
        int mark;
        Check(KernelRuntime.MarkCreate(&mark), "MarkCreate");
        var pointSf = new PK_POINT_sf_s();
        int point;
        Check(KernelRuntime.PointCreate(&pointSf, &point), "PointCreate");
        Check(KernelRuntime.MarkGoto(mark), "MarkGoto");
        Check(KernelRuntime.SessionStop(), "SessionStop");
    });

    Measure("return arena repeated query", static () =>
    {
        RestartSession();
        int body;
        Check(KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &body), "BodyCreateSolidBlock");
        int count;
        int* faces;
        for (int i = 0; i < 1024; i++)
            Check(KernelRuntime.BodyAskFaces(body, &count, &faces), "BodyAskFaces");
        Check(KernelRuntime.SessionStop(), "SessionStop");
    });
}

static void Measure(string name, Action action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long before = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    action();
    stopwatch.Stop();
    long after = GC.GetAllocatedBytesForCurrentThread();

    Console.WriteLine($"{name}: allocated={after - before} bytes elapsed={stopwatch.Elapsed.TotalMilliseconds:F3} ms");
}

static unsafe void RestartSession()
{
    KernelRuntime.SessionStop();
    var options = new PK_SESSION_start_o_s { o_t_version = 1 };
    Check(KernelRuntime.SessionStart(&options), "SessionStart");
}

static void Check(int error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}
