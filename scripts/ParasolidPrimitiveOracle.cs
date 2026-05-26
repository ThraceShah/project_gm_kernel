#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

var tempDir = Path.Combine(Path.GetTempPath(), "project-gm-kernel-primitive-oracle-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);

unsafe
{
    try
    {
        WriteOurPrimitives(tempDir);
        if (!ParasolidScriptHost.TryStartSession("Parasolid primitive oracle", out var session, out var skipMessage))
        {
            Console.WriteLine(skipMessage);
            return 0;
        }

        using (session)
        {
            CheckParasolidFile(tempDir, "cone", CreateParasolidCone(), 2, 2, 3, 2, 0);
            CheckParasolidFile(tempDir, "prism", CreateParasolidPrism(), 2, 2, 7, 15, 10);
            CheckParasolidFile(tempDir, "sphere", CreateParasolidSphere(), 2, 2, 1, 0, 0);
            CheckParasolidFile(tempDir, "torus", CreateParasolidTorus(), 2, 2, 1, 0, 0);
        }
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

Console.WriteLine("primitive oracle ok");
return 0;

static unsafe void WriteOurPrimitives(string dir)
{
    var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
    CheckManaged(KernelRuntime.SessionStart(&startOptions), "KernelRuntime.SessionStart");
    try
    {
        int body;
        CheckManaged(KernelRuntime.BodyCreateSolidCone(1, 5, 0.25, null, &body), "BodyCreateSolidCone");
        WriteOurPart(dir, "cone", body);

        CheckManaged(KernelRuntime.BodyCreateSolidPrism(2, 5, 5, null, &body), "BodyCreateSolidPrism");
        WriteOurPart(dir, "prism", body);

        CheckManaged(KernelRuntime.BodyCreateSolidSphere(2, null, &body), "BodyCreateSolidSphere");
        WriteOurPart(dir, "sphere", body);

        CheckManaged(KernelRuntime.BodyCreateSolidTorus(5, 1, null, &body), "BodyCreateSolidTorus");
        WriteOurPart(dir, "torus", body);
    }
    finally
    {
        CheckManaged(KernelRuntime.SessionStop(), "KernelRuntime.SessionStop");
    }
}

static unsafe void WriteOurPart(string dir, string label, int body)
{
    var options = new ProjectGmKernel.Native.Generated.PK_PART_transmit_o_s
    {
        o_t_version = 10,
        transmit_format = ParasolidConstants.PK_transmit_format_text_c,
    };
    var block = new ProjectGmKernel.Native.Generated.PK_MEMORY_block_s();
    CheckManaged(KernelRuntime.PartTransmitB(1, &body, &options, &block), "PartTransmitB " + label);
    try
    {
        using var stream = File.Create(Path.Combine(dir, label + ".x_t"));
        for (var current = &block; current is not null; current = current->next)
            stream.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
    }
    finally
    {
        CheckManaged(KernelRuntime.MemoryBlockFree(&block), "MemoryBlockFree " + label);
    }
}

static unsafe PK_BODY_t CreateParasolidCone()
{
    PK_BODY_t body;
    CheckParasolid(PK_BODY_create_solid_cone(1, 5, 0.25, null, &body), "PK_BODY_create_solid_cone");
    return body;
}

static unsafe PK_BODY_t CreateParasolidPrism()
{
    PK_BODY_t body;
    CheckParasolid(PK_BODY_create_solid_prism(2, 5, 5, null, &body), "PK_BODY_create_solid_prism");
    return body;
}

static unsafe PK_BODY_t CreateParasolidSphere()
{
    PK_BODY_t body;
    CheckParasolid(PK_BODY_create_solid_sphere(2, null, &body), "PK_BODY_create_solid_sphere");
    return body;
}

static unsafe PK_BODY_t CreateParasolidTorus()
{
    PK_BODY_t body;
    CheckParasolid(PK_BODY_create_solid_torus(5, 1, null, &body), "PK_BODY_create_solid_torus");
    return body;
}

static unsafe void CheckParasolidFile(string dir, string label, PK_BODY_t expectedBody, int regionsExpected, int shellsExpected, int facesExpected, int edgesExpected, int verticesExpected)
{
    var bytes = File.ReadAllBytes(Path.Combine(dir, label + ".x_t"));
    fixed (byte* bytesPtr = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPtr);
        var options = new PK_PART_receive_o_t
        {
            transmit_format = PK_transmit_format_text_c,
        };

        int nParts;
        PK_PART_t* parts;
        CheckParasolid(PK_PART_receive_b(block, &options, &nParts, &parts), "PK_PART_receive_b " + label);
        try
        {
            Require(nParts == 1, label + " part count", nParts, 1);
            AssertBodyCounts(parts[0], regionsExpected, shellsExpected, facesExpected, edgesExpected, verticesExpected);
            AssertBodyCompare(expectedBody, parts[0], label);
        }
        finally
        {
            if (parts is not null)
                CheckParasolid(PK_MEMORY_free(parts), "PK_MEMORY_free " + label);
        }
    }
}

static unsafe void AssertBodyCompare(PK_BODY_t master, PK_BODY_t similar, string label)
{
    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var results = new PK_DEBUG_BODY_compare_r_t();
    CheckParasolid(PK_DEBUG_BODY_compare(master, similar, &options, &results), "PK_DEBUG_BODY_compare " + label);
    try
    {
        if (results.global_result != PK_DEBUG_global_res_no_diffs_c)
            throw new InvalidOperationException($"{label} body compare mismatch: global={results.global_result} local={results.local_result} global_diffs={results.n_global_diffs} face_pairs={results.n_face_pairs}");
        if (results.local_result != PK_DEBUG_local_res_no_diffs_c)
            Console.WriteLine($"{label} body compare local diagnostics: local={results.local_result} face_pairs={results.n_face_pairs}");
    }
    finally
    {
        CheckParasolid(PK_DEBUG_BODY_compare_r_f(&results), "PK_DEBUG_BODY_compare_r_f " + label);
    }
}

static unsafe void AssertBodyCounts(PK_BODY_t body, int regionsExpected, int shellsExpected, int facesExpected, int edgesExpected, int verticesExpected)
{
    CheckRegionCount(body, regionsExpected);
    CheckShellCount(body, shellsExpected);
    CheckFaceCount(body, facesExpected);
    CheckEdgeCount(body, edgesExpected);
    CheckVertexCount(body, verticesExpected);
}

static unsafe void CheckRegionCount(PK_BODY_t body, int expected)
{
    int count;
    PK_REGION_t* values;
    CheckParasolid(PK_BODY_ask_regions(body, &count, &values), "PK_BODY_ask_regions");
    try { Require(count == expected, "regions", count, expected); }
    finally { Free(values, "regions"); }
}

static unsafe void CheckShellCount(PK_BODY_t body, int expected)
{
    int count;
    PK_SHELL_t* values;
    CheckParasolid(PK_BODY_ask_shells(body, &count, &values), "PK_BODY_ask_shells");
    try { Require(count == expected, "shells", count, expected); }
    finally { Free(values, "shells"); }
}

static unsafe void CheckFaceCount(PK_BODY_t body, int expected)
{
    int count;
    PK_FACE_t* values;
    CheckParasolid(PK_BODY_ask_faces(body, &count, &values), "PK_BODY_ask_faces");
    try { Require(count == expected, "faces", count, expected); }
    finally { Free(values, "faces"); }
}

static unsafe void CheckEdgeCount(PK_BODY_t body, int expected)
{
    int count;
    PK_EDGE_t* values;
    CheckParasolid(PK_BODY_ask_edges(body, &count, &values), "PK_BODY_ask_edges");
    try { Require(count == expected, "edges", count, expected); }
    finally { Free(values, "edges"); }
}

static unsafe void CheckVertexCount(PK_BODY_t body, int expected)
{
    int count;
    PK_VERTEX_t* values;
    CheckParasolid(PK_BODY_ask_vertices(body, &count, &values), "PK_BODY_ask_vertices");
    try { Require(count == expected, "vertices", count, expected); }
    finally { Free(values, "vertices"); }
}

static unsafe void Free<T>(T* values, string label)
    where T : unmanaged
{
    if (values is not null)
        CheckParasolid(PK_MEMORY_free(values), "PK_MEMORY_free " + label);
}

static void CheckManaged(int error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void CheckParasolid(PK_ERROR_code_t error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void Require(bool condition, string label, int actual, int expected)
{
    if (!condition)
        throw new InvalidOperationException($"{label} mismatch: expected {expected}, got {actual}");
}
