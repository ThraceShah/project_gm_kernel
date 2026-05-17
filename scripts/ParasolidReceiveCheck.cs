#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
// Receives one text x_t file with real Parasolid and checks topology counts.

using static parasolid;

if (args.Length != 6)
{
    Console.Error.WriteLine("usage: dotnet run scripts/ParasolidReceiveCheck.cs -- FILE REGIONS SHELLS FACES EDGES VERTICES");
    return 2;
}

var inputPath = args[0];
var regionsExpected = int.Parse(args[1]);
var shellsExpected = int.Parse(args[2]);
var facesExpected = int.Parse(args[3]);
var edgesExpected = int.Parse(args[4]);
var verticesExpected = int.Parse(args[5]);

if (!ParasolidScriptHost.TryStartSession("Parasolid receive check", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

unsafe
{
    using (session)
    {
        var bytes = File.ReadAllBytes(inputPath);
        fixed (byte* bytesPtr = bytes)
        {
            var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPtr);
            var receiveOptions = new PK_PART_receive_o_t
            {
                transmit_format = PK_transmit_format_text_c,
            };

            int receivedCount;
            PK_PART_t* received;
            Check(PK_PART_receive_b(block, &receiveOptions, &receivedCount, &received), "PK_PART_receive_b");
            try
            {
                Require(receivedCount == 1, "received part count", receivedCount, 1);
                AssertBodyCounts(received[0], regionsExpected, shellsExpected, facesExpected, edgesExpected, verticesExpected);
            }
            finally
            {
                if (received is not null)
                    Check(PK_MEMORY_free(received), "PK_MEMORY_free(received)");
            }
        }
    }
}

Console.WriteLine("receive ok");
return 0;

static unsafe void AssertBodyCounts(
    PK_BODY_t body,
    int regionsExpected,
    int shellsExpected,
    int facesExpected,
    int edgesExpected,
    int verticesExpected)
{
    PK_REGION_t* regions;
    int regionCount;
    Check(PK_BODY_ask_regions(body, &regionCount, &regions), "PK_BODY_ask_regions");
    Require(regionCount == regionsExpected, "region count", regionCount, regionsExpected);
    try
    {
        if (regionsExpected == 2)
        {
            PK_LOGICAL_t isSolid;
            var solidCount = 0;
            Check(PK_REGION_is_solid(regions[0], &isSolid), "PK_REGION_is_solid[0]");
            solidCount += isSolid ? 1 : 0;
            Check(PK_REGION_is_solid(regions[1], &isSolid), "PK_REGION_is_solid[1]");
            solidCount += isSolid ? 1 : 0;
            Require(solidCount == 1, "region solid count", solidCount, 1);
        }
    }
    finally
    {
        FreeArray(regions);
    }

    CheckShellCount(body, shellsExpected);
    CheckFaceCount(body, facesExpected);
    CheckEdgeCount(body, edgesExpected);
    CheckVertexCount(body, verticesExpected);
}

static unsafe void CheckShellCount(PK_BODY_t body, int expected)
{
    int count;
    PK_SHELL_t* values;
    Check(PK_BODY_ask_shells(body, &count, &values), "PK_BODY_ask_shells");
    try { Require(count == expected, "shell count", count, expected); }
    finally { FreeArray(values); }
}

static unsafe void CheckFaceCount(PK_BODY_t body, int expected)
{
    int count;
    PK_FACE_t* values;
    Check(PK_BODY_ask_faces(body, &count, &values), "PK_BODY_ask_faces");
    try { Require(count == expected, "face count", count, expected); }
    finally { FreeArray(values); }
}

static unsafe void CheckEdgeCount(PK_BODY_t body, int expected)
{
    int count;
    PK_EDGE_t* values;
    Check(PK_BODY_ask_edges(body, &count, &values), "PK_BODY_ask_edges");
    try { Require(count == expected, "edge count", count, expected); }
    finally { FreeArray(values); }
}

static unsafe void CheckVertexCount(PK_BODY_t body, int expected)
{
    int count;
    PK_VERTEX_t* values;
    Check(PK_BODY_ask_vertices(body, &count, &values), "PK_BODY_ask_vertices");
    try { Require(count == expected, "vertex count", count, expected); }
    finally { FreeArray(values); }
}

static unsafe void FreeArray<T>(T* values)
    where T : unmanaged
{
    if (values is not null)
        Check(PK_MEMORY_free(values), "PK_MEMORY_free");
}

static void Check(PK_ERROR_code_t error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void Require(bool condition, string label, int actual, int expected)
{
    if (!condition)
        throw new InvalidOperationException($"{label} mismatch: expected {expected}, got {actual}");
}
