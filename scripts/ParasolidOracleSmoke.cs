#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=ParasolidOracleSmoke
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
// Bidirectional XT oracle validation against real Parasolid.
//
// This script is intentionally diagnostic. If the local Parasolid runtime is
// unavailable, it reports a skip instead of failing the normal verification
// pipeline.

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

var tempDir = Path.Combine(Path.GetTempPath(), "project-gm-parasolid-oracle-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
var keepTemp = false;
try
{
    var ourBlockPath = Path.Combine(tempDir, "our_block.x_t");
    var ourCylinderPath = Path.Combine(tempDir, "our_cylinder.x_t");
    var ourMultiPath = Path.Combine(tempDir, "our_multi.x_t");
    var parasolidBlockPath = Path.Combine(tempDir, "parasolid_block.x_t");
    var parasolidCylinderPath = Path.Combine(tempDir, "parasolid_cylinder.x_t");
    var parasolidMultiPath = Path.Combine(tempDir, "parasolid_multi.x_t");

    var managedExportResult = WriteOurXtFiles(ourBlockPath, ourCylinderPath, ourMultiPath);
    if (!managedExportResult.Success)
    {
        Console.WriteLine(managedExportResult.Message);
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return managedExportResult.Skipped ? 0 : 1;
    }

    var parasolidResult = WriteParasolidXtFilesAndCheckOurXt(ourBlockPath, ourCylinderPath, ourMultiPath, parasolidBlockPath, parasolidCylinderPath, parasolidMultiPath);
    if (!parasolidResult.Success)
    {
        Console.WriteLine(parasolidResult.Message);
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return parasolidResult.Skipped ? 0 : 1;
    }

    unsafe
    {
        try
        {
            var managedImportResult = ReadParasolidXtFiles(parasolidBlockPath, parasolidCylinderPath, parasolidMultiPath);
            if (!managedImportResult.Success)
            {
                Console.WriteLine(managedImportResult.Message);
                keepTemp = true;
                Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
                return managedImportResult.Skipped ? 0 : 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Parasolid oracle failed: managed kernel could not receive Parasolid x_t: " + ex.Message);
            keepTemp = true;
            Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
            return 1;
        }
    }

    Console.WriteLine("Parasolid oracle bidirectional smoke passed");
    return 0;
}
finally
{
    if (!keepTemp)
    {
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // Temp cleanup is best effort only.
        }
    }
}

static unsafe OracleStepResult WriteParasolidXtFilesAndCheckOurXt(
    string ourBlockPath,
    string ourCylinderPath,
    string ourMultiPath,
    string parasolidBlockPath,
    string parasolidCylinderPath,
    string parasolidMultiPath)
{
    if (!ParasolidScriptHost.TryStartSession("Parasolid oracle", out var session, out var skipMessage))
        return OracleStepResult.Skip(skipMessage);

    using (session)
    {
        Console.WriteLine("oracle: ask schema");
        PK_SESSION_schema_version_t schema;
        CheckParasolid(PK_SESSION_ask_schema_version(&schema), "PK_SESSION_ask_schema_version");
        Console.WriteLine("schema_version=" + schema.schema_version);

        Console.WriteLine("oracle: create block");
        PK_BODY_t block;
        CheckParasolid(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &block), "PK_BODY_create_solid_block");
        AssertParasolidBodyCounts(block, 2, 2, 6, 12, 8);

        Console.WriteLine("oracle: create cylinder");
        PK_BODY_t cylinder;
        CheckParasolid(PK_BODY_create_solid_cyl(2.0, 5.0, null, &cylinder), "PK_BODY_create_solid_cyl");
        AssertParasolidBodyCounts(cylinder, 2, 2, 3, 2, 0);

        Console.WriteLine("oracle: write parasolid block x_t");
        WriteParasolidPartToFile(block, parasolidBlockPath, "parasolid block");

        Console.WriteLine("oracle: write parasolid cylinder x_t");
        WriteParasolidPartToFile(cylinder, parasolidCylinderPath, "parasolid cylinder");

        Console.WriteLine("oracle: write parasolid multi-body x_t");
        var parts = stackalloc PK_PART_t[2] { block, cylinder };
        WriteParasolidPartsToFile(2, parts, parasolidMultiPath, "parasolid multi-body");

        var failures = 0;
        Console.WriteLine("oracle: receive our block x_t");
        if (!ReceiveParasolidFileAndCompare(ourBlockPath, block, 2, 2, 6, 12, 8, "our block"))
            failures++;

        Console.WriteLine("oracle: receive our cylinder x_t");
        if (!ReceiveParasolidFileAndCompare(ourCylinderPath, cylinder, 2, 2, 3, 2, 0, "our cylinder"))
            failures++;

        Console.WriteLine("oracle: receive our multi-body x_t");
        if (!ReceiveParasolidFileAndCompareMany(ourMultiPath, block, cylinder))
            failures++;

        if (failures != 0)
            return OracleStepResult.Fail("parasolid oracle failures=" + failures);

        Console.WriteLine("parasolid oracle ok");
        return OracleStepResult.Ok();
    }
}

static unsafe void WriteParasolidPartToFile(PK_PART_t part, string path, string label)
{
    WriteParasolidPartsToFile(1, &part, path, label);
}

static unsafe void WriteParasolidPartsToFile(int partCount, PK_PART_t* parts, string path, string label)
{
    var options = new PK_PART_transmit_o_t
    {
        transmit_format = PK_transmit_format_text_c,
        transmit_user_fields = PK_LOGICAL_false,
        transmit_version = 371,
    };
    var block = new PK_MEMORY_block_t();
    CheckParasolid(PK_PART_transmit_b(partCount, parts, &options, &block), "PK_PART_transmit_b " + label);
    try
    {
        using var output = File.Create(path);
        for (var current = &block; current is not null; current = current->next)
        {
            if (current->bytes is not null && current->n_bytes != 0)
                output.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        }
    }
    finally
    {
        CheckParasolid(PK_MEMORY_block_f(&block), "PK_MEMORY_block_f " + label);
    }
}

static unsafe bool ReceiveParasolidFileAndCompare(
    string path,
    PK_BODY_t expectedBody,
    int regionsExpected,
    int shellsExpected,
    int facesExpected,
    int edgesExpected,
    int verticesExpected,
    string label)
{
    try
    {
        var bytes = File.ReadAllBytes(path);
        fixed (byte* bytesPtr = bytes)
        {
            var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPtr);
            var options = new PK_PART_receive_o_t
            {
                transmit_format = PK_transmit_format_text_c,
            };
            int receivedCount;
            PK_PART_t* received;
            CheckParasolid(PK_PART_receive_b(block, &options, &receivedCount, &received), "PK_PART_receive_b " + label);
            try
            {
                RequireParasolid(receivedCount == 1, label + " received part count");
                var receivedBody = received[0];
                AssertParasolidBodyCounts(receivedBody, regionsExpected, shellsExpected, facesExpected, edgesExpected, verticesExpected);
                RequireParasolid(AssertBodyCompare(expectedBody, receivedBody, label), label + " body compare");
            }
            finally
            {
                if (received is not null)
                    CheckParasolid(PK_MEMORY_free(received), "PK_MEMORY_free " + label);
            }
        }

        Console.WriteLine("PASS " + label + " -> Parasolid receive");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL " + label + " -> Parasolid receive: " + ex.Message);
        return false;
    }
}

static unsafe bool ReceiveParasolidFileAndCompareMany(string path, PK_BODY_t expectedBlock, PK_BODY_t expectedCylinder)
{
    try
    {
        var bytes = File.ReadAllBytes(path);
        fixed (byte* bytesPtr = bytes)
        {
            var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPtr);
            var options = new PK_PART_receive_o_t
            {
                transmit_format = PK_transmit_format_text_c,
            };
            int receivedCount;
            PK_PART_t* received;
            CheckParasolid(PK_PART_receive_b(block, &options, &receivedCount, &received), "PK_PART_receive_b our multi-body");
            try
            {
                RequireParasolid(receivedCount == 2, "our multi-body received part count");
                AssertParasolidBodyCounts(received[0], 2, 2, 6, 12, 8);
                RequireParasolid(AssertBodyCompare(expectedBlock, received[0], "our multi block"), "our multi block body compare");
                AssertParasolidBodyCounts(received[1], 2, 2, 3, 2, 0);
                RequireParasolid(AssertBodyCompare(expectedCylinder, received[1], "our multi cylinder"), "our multi cylinder body compare");
            }
            finally
            {
                if (received is not null)
                    CheckParasolid(PK_MEMORY_free(received), "PK_MEMORY_free our multi-body");
            }
        }

        Console.WriteLine("PASS our multi-body -> Parasolid receive");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL our multi-body -> Parasolid receive: " + ex.Message);
        return false;
    }
}

static unsafe void AssertParasolidBodyCounts(
    PK_BODY_t body,
    int regionsExpected,
    int shellsExpected,
    int facesExpected,
    int edgesExpected,
    int verticesExpected)
{
    PK_REGION_t* regions;
    int regionCount;
    CheckParasolid(PK_BODY_ask_regions(body, &regionCount, &regions), "PK_BODY_ask_regions");
    RequireCount(regionCount, regionsExpected, "region");
    try
    {
        if (regionsExpected == 2)
        {
            PK_LOGICAL_t isSolid;
            var solidCount = 0;
            CheckParasolid(PK_REGION_is_solid(regions[0], &isSolid), "PK_REGION_is_solid[0]");
            solidCount += isSolid ? 1 : 0;
            CheckParasolid(PK_REGION_is_solid(regions[1], &isSolid), "PK_REGION_is_solid[1]");
            solidCount += isSolid ? 1 : 0;
            RequireParasolid(solidCount == 1, "region solid/void mix");
        }
    }
    finally
    {
        FreeParasolidArray(regions);
    }

    CheckParasolidShellCount(body, shellsExpected);
    CheckParasolidFaceCount(body, facesExpected);
    CheckParasolidEdgeCount(body, edgesExpected);
    CheckParasolidVertexCount(body, verticesExpected);
}

static unsafe void CheckParasolidShellCount(PK_BODY_t body, int expected)
{
    int count;
    PK_SHELL_t* values;
    CheckParasolid(PK_BODY_ask_shells(body, &count, &values), "PK_BODY_ask_shells");
    try { RequireCount(count, expected, "shell"); }
    finally { FreeParasolidArray(values); }
}

static unsafe void CheckParasolidFaceCount(PK_BODY_t body, int expected)
{
    int count;
    PK_FACE_t* values;
    CheckParasolid(PK_BODY_ask_faces(body, &count, &values), "PK_BODY_ask_faces");
    try { RequireCount(count, expected, "face"); }
    finally { FreeParasolidArray(values); }
}

static unsafe void CheckParasolidEdgeCount(PK_BODY_t body, int expected)
{
    int count;
    PK_EDGE_t* values;
    CheckParasolid(PK_BODY_ask_edges(body, &count, &values), "PK_BODY_ask_edges");
    try { RequireCount(count, expected, "edge"); }
    finally { FreeParasolidArray(values); }
}

static unsafe void CheckParasolidVertexCount(PK_BODY_t body, int expected)
{
    int count;
    PK_VERTEX_t* values;
    CheckParasolid(PK_BODY_ask_vertices(body, &count, &values), "PK_BODY_ask_vertices");
    try { RequireCount(count, expected, "vertex"); }
    finally { FreeParasolidArray(values); }
}

static unsafe void FreeParasolidArray<T>(T* values)
    where T : unmanaged
{
    if (values is not null)
        CheckParasolid(PK_MEMORY_free(values), "PK_MEMORY_free");
}

static unsafe bool AssertBodyCompare(PK_BODY_t master, PK_BODY_t similar, string label)
{
    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var results = new PK_DEBUG_BODY_compare_r_t();
    CheckParasolid(PK_DEBUG_BODY_compare(master, similar, &options, &results), "PK_DEBUG_BODY_compare");
    try
    {
        var ok = results.global_result == PK_DEBUG_global_res_no_diffs_c;
        if (!ok)
        {
            Console.WriteLine($"{label} body compare mismatch: global={results.global_result} local={results.local_result} global_diffs={results.n_global_diffs} face_pairs={results.n_face_pairs}");
            PrintBodyCompareDiffs(&results);
        }
        else if (results.local_result != PK_DEBUG_local_res_no_diffs_c)
        {
            Console.WriteLine($"{label} body compare local diagnostics: local={results.local_result} face_pairs={results.n_face_pairs}");
        }

        return ok;
    }
    finally
    {
        CheckParasolid(PK_DEBUG_BODY_compare_r_f(&results), "PK_DEBUG_BODY_compare_r_f");
    }
}

static unsafe void PrintBodyCompareDiffs(PK_DEBUG_BODY_compare_r_t* results)
{
    for (var i = 0; i < results->n_global_diffs; i++)
    {
        var diff = &results->global_diffs[i];
        Console.WriteLine($"  global diff {i}: {DiffName(diff->diff)}({diff->diff}) master={diff->n_masters} similar={diff->n_similars}");
    }

    for (var i = 0; i < results->n_face_pairs; i++)
    {
        var pair = &results->face_pairs[i];
        for (var j = 0; j < pair->n_local_diffs; j++)
        {
            var diff = &pair->local_diffs[j];
            Console.WriteLine(
                $"  local diff face_pair={i} diff={DiffName(diff->diff)}({diff->diff}) master_entity={diff->master_entity} similar_entity={diff->similar_entity} master_int={diff->master_int} similar_int={diff->similar_int} master_double={diff->master_double:R} similar_double={diff->similar_double:R} master_logical={diff->master_logical} similar_logical={diff->similar_logical}");
        }
    }
}

static string DiffName(PK_DEBUG_diff_t diff)
{
    return diff switch
    {
        PK_DEBUG_diff_n_shells_c => "n_shells",
        PK_DEBUG_diff_n_faces_c => "n_faces",
        PK_DEBUG_diff_n_loops_c => "n_loops",
        PK_DEBUG_diff_n_acc_edges_c => "n_acc_edges",
        PK_DEBUG_diff_n_tol_edges_c => "n_tol_edges",
        PK_DEBUG_diff_n_fins_c => "n_fins",
        PK_DEBUG_diff_n_acc_vxs_c => "n_acc_vxs",
        PK_DEBUG_diff_n_tol_vxs_c => "n_tol_vxs",
        PK_DEBUG_diff_n_vxs_c => "n_vxs",
        PK_DEBUG_diff_n_cht_pts_c => "n_cht_pts",
        PK_DEBUG_diff_surf_dev_c => "surf_dev",
        PK_DEBUG_diff_curve_dev_c => "curve_dev",
        PK_DEBUG_diff_vx_dev_c => "vx_dev",
        PK_DEBUG_diff_surf_class_c => "surf_class",
        PK_DEBUG_diff_curve_class_c => "curve_class",
        PK_DEBUG_diff_edge_tol_c => "edge_tol",
        PK_DEBUG_diff_vx_tol_c => "vx_tol",
        PK_DEBUG_diff_face_sense_c => "face_sense",
        PK_DEBUG_diff_surf_sense_c => "surf_sense",
        PK_DEBUG_diff_curve_sense_c => "curve_sense",
        PK_DEBUG_diff_vx_missing_c => "vx_missing",
        PK_DEBUG_diff_face_match_c => "face_match",
        PK_DEBUG_diff_fin_match_c => "fin_match",
        PK_DEBUG_diff_vx_match_c => "vx_match",
        _ => "unknown",
    };
}

static void CheckParasolid(PK_ERROR_code_t error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void RequireCount(int actual, int expected, string label)
{
    if (actual != expected)
        throw new InvalidOperationException($"{label} count mismatch: expected {expected}, got {actual}");
}

static void RequireParasolid(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException("Unexpected " + name);
}

static unsafe OracleStepResult WriteOurXtFiles(string blockPath, string cylinderPath, string multiPath)
{
    var startOptions = new ProjectGmKernel.Native.Generated.PK_SESSION_start_o_s { o_t_version = 1 };
    CheckManaged(KernelRuntime.SessionStart(&startOptions), "our PK_SESSION_start");

    try
    {
        int block;
        CheckManaged(KernelRuntime.BodyCreateSolidBlock(1, 2, 3, null, &block), "our PK_BODY_create_solid_block");
        WritePartToFile(block, blockPath, "our block");

        int cylinder;
        CheckManaged(KernelRuntime.BodyCreateSolidCyl(2, 5, null, &cylinder), "our PK_BODY_create_solid_cyl");
        WritePartToFile(cylinder, cylinderPath, "our cylinder");

        var parts = stackalloc int[2] { block, cylinder };
        WritePartsToFile(2, parts, multiPath, "our multi-body");
    }
    finally
    {
        KernelRuntime.SessionStop();
    }

    return OracleStepResult.Ok();
}

static unsafe OracleStepResult ReadParasolidXtFiles(string blockPath, string cylinderPath, string multiPath)
{
    var startOptions = new ProjectGmKernel.Native.Generated.PK_SESSION_start_o_s { o_t_version = 1 };
    CheckManaged(KernelRuntime.SessionStart(&startOptions), "our PK_SESSION_start");

    try
    {
        var failures = 0;
        if (!TryReadFileAndCheck(blockPath, 2, 2, 6, 12, 8, "parasolid block"))
            failures++;
        if (!TryReadFileAndCheck(cylinderPath, 2, 2, 3, 2, 0, "parasolid cylinder"))
            failures++;
        if (!TryReadFileAndCheckMany(multiPath, "parasolid multi-body"))
            failures++;

        if (failures != 0)
            return OracleStepResult.Fail("Parasolid oracle failed: managed kernel receive failures=" + failures);
    }
    finally
    {
        KernelRuntime.SessionStop();
    }

    return OracleStepResult.Ok();
}

static unsafe void WritePartToFile(int part, string path, string label)
{
    WritePartsToFile(1, &part, path, label);
}

static unsafe void WritePartsToFile(int partCount, int* parts, string path, string label)
{
    var options = new ProjectGmKernel.Native.Generated.PK_PART_transmit_o_s
    {
        o_t_version = 10,
        transmit_format = ProjectGmKernel.Native.Generated.ParasolidConstants.PK_transmit_format_text_c,
        transmit_user_fields = 0,
        transmit_version = 371,
    };
    var block = new ProjectGmKernel.Native.Generated.PK_MEMORY_block_s();
    CheckManaged(KernelRuntime.PartTransmitB(partCount, parts, &options, &block), "our PK_PART_transmit_b " + label);
    try
    {
        using var output = File.Create(path);
        for (var current = &block; current is not null; current = current->next)
        {
            if (current->bytes is not null && current->n_bytes != 0)
                output.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
        }
    }
    finally
    {
        CheckManaged(KernelRuntime.MemoryBlockFree(&block), "our PK_MEMORY_block_f " + label);
    }
}

static unsafe bool TryReadFileAndCheckMany(string path, string label)
{
    try
    {
        var bytes = File.ReadAllBytes(path);
        fixed (byte* bytesPtr = bytes)
        {
            var block = new ProjectGmKernel.Native.Generated.PK_MEMORY_block_s
            {
                next = null,
                n_bytes = (nuint)bytes.Length,
                bytes = bytesPtr,
            };
            var options = new ProjectGmKernel.Native.Generated.PK_PART_receive_o_s
            {
                o_t_version = 14,
                transmit_format = ProjectGmKernel.Native.Generated.ParasolidConstants.PK_transmit_format_text_c,
            };
            int nParts;
            int* parts;
            CheckManaged(KernelRuntime.PartReceiveB(block, &options, &nParts, &parts), "our PK_PART_receive_b " + label);
            try
            {
                RequireManaged(nParts == 2, label + " received part count");
                CheckCount(parts[0], 2, label + " block regions", &KernelRuntime.BodyAskRegions);
                CheckCount(parts[0], 2, label + " block shells", &KernelRuntime.BodyAskShells);
                CheckCount(parts[0], 6, label + " block faces", &KernelRuntime.BodyAskFaces);
                CheckCount(parts[0], 12, label + " block edges", &KernelRuntime.BodyAskEdges);
                CheckCount(parts[0], 8, label + " block vertices", &KernelRuntime.BodyAskVertices);
                CheckCount(parts[1], 2, label + " cylinder regions", &KernelRuntime.BodyAskRegions);
                CheckCount(parts[1], 2, label + " cylinder shells", &KernelRuntime.BodyAskShells);
                CheckCount(parts[1], 3, label + " cylinder faces", &KernelRuntime.BodyAskFaces);
                CheckCount(parts[1], 2, label + " cylinder edges", &KernelRuntime.BodyAskEdges);
                CheckCount(parts[1], 0, label + " cylinder vertices", &KernelRuntime.BodyAskVertices);
            }
            finally
            {
                if (parts is not null)
                    CheckManaged(KernelRuntime.MemoryFree(parts), "our PK_MEMORY_free " + label);
            }
        }

        Console.WriteLine("PASS Parasolid " + label + " -> managed receive");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL Parasolid " + label + " -> managed receive: " + ex.Message);
        return false;
    }
}

static unsafe bool TryReadFileAndCheck(
    string path,
    int regionsExpected,
    int shellsExpected,
    int facesExpected,
    int edgesExpected,
    int verticesExpected,
    string label)
{
    try
    {
        var bytes = File.ReadAllBytes(path);
        fixed (byte* bytesPtr = bytes)
        {
            var block = new ProjectGmKernel.Native.Generated.PK_MEMORY_block_s
            {
                next = null,
                n_bytes = (nuint)bytes.Length,
                bytes = bytesPtr,
            };
            var options = new ProjectGmKernel.Native.Generated.PK_PART_receive_o_s
            {
                o_t_version = 14,
                transmit_format = ProjectGmKernel.Native.Generated.ParasolidConstants.PK_transmit_format_text_c,
            };
            int nParts;
            int* parts;
            CheckManaged(KernelRuntime.PartReceiveB(block, &options, &nParts, &parts), "our PK_PART_receive_b " + label);
            try
            {
                RequireManaged(nParts == 1, label + " received part count");
                var body = parts[0];
                CheckCount(body, regionsExpected, label + " regions", &KernelRuntime.BodyAskRegions);
                CheckCount(body, shellsExpected, label + " shells", &KernelRuntime.BodyAskShells);
                CheckCount(body, facesExpected, label + " faces", &KernelRuntime.BodyAskFaces);
                CheckCount(body, edgesExpected, label + " edges", &KernelRuntime.BodyAskEdges);
                CheckCount(body, verticesExpected, label + " vertices", &KernelRuntime.BodyAskVertices);
            }
            finally
            {
                if (parts is not null)
                    CheckManaged(KernelRuntime.MemoryFree(parts), "our PK_MEMORY_free " + label);
            }
        }

        Console.WriteLine("PASS Parasolid " + label + " -> managed receive");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL Parasolid " + label + " -> managed receive: " + ex.Message);
        return false;
    }
}

static unsafe void CheckCount(int body, int expected, string label, delegate* managed<int, int*, int**, int> query)
{
    int count;
    int* values;
    CheckManaged(query(body, &count, &values), "our query " + label);
    RequireManaged(count == expected, label);
}

static void CheckManaged(int error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void RequireManaged(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException("Unexpected " + name);
}

readonly record struct OracleStepResult(bool Success, bool Skipped, string Message)
{
    public static OracleStepResult Ok() => new(true, false, "");
    public static OracleStepResult Skip(string message) => new(false, true, message);
    public static OracleStepResult Fail(string message) => new(false, false, message);
}
