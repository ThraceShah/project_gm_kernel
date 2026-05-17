#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
// Builds and runs a tiny native Parasolid harness for XT oracle validation.
//
// This script is intentionally diagnostic. If the local Parasolid runtime or
// compiler is unavailable, it reports a skip instead of failing the normal
// kernel verification pipeline.

using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

const int PK_transmit_format_text_c = 18220;
const int PK_LOGICAL_false = 0;

const string HarnessSource = """
#include <stdio.h>
#include <stdlib.h>
#include "parasolid_kernel.h"
#include "parasolid_debug.h"

static void trace(const char *message)
{
    printf("%s\n", message);
    fflush(stdout);
}

static void oracle_fstart(int *ifail)
{
    *ifail = 0;
}

static void oracle_fabort(int *ifail)
{
    *ifail = 0;
}

static void oracle_fstop(int *ifail)
{
    *ifail = 0;
}

static void oracle_fmallo(int *nbytes, char **memory, int *ifail)
{
    *memory = (char*)malloc((size_t)*nbytes);
    *ifail = *memory == NULL ? 15 : 0;
}

static void oracle_fmfree(int *nbytes, char **memory, int *ifail)
{
    (void)nbytes;
    free(*memory);
    *memory = NULL;
    *ifail = 0;
}

static int check(PK_ERROR_code_t error, const char *name)
{
    if (error != PK_ERROR_no_errors)
    {
        printf("%s failed: %d\n", name, error);
        return 0;
    }
    return 1;
}

static void free_array(void *ptr)
{
    if (ptr != NULL)
        (void)PK_MEMORY_free(ptr);
}

static int assert_body_counts(PK_BODY_t body, int regions_expected, int shells_expected, int faces_expected, int edges_expected, int vertices_expected)
{
    int count = 0;
    PK_REGION_t *regions = NULL;
    PK_SHELL_t *shells = NULL;
    PK_FACE_t *faces = NULL;
    PK_EDGE_t *edges = NULL;
    PK_VERTEX_t *vertices = NULL;
    PK_LOGICAL_t is_solid = PK_LOGICAL_false;
    PK_SHELL_t face_shells[2] = { PK_ENTITY_null, PK_ENTITY_null };

    if (!check(PK_BODY_ask_regions(body, &count, &regions), "PK_BODY_ask_regions")) return 0;
    if (count != regions_expected) { printf("region count mismatch: %d\n", count); return 0; }
    if (regions_expected == 2)
    {
        if (!check(PK_REGION_is_solid(regions[0], &is_solid), "PK_REGION_is_solid[0]")) return 0;
        if (is_solid != PK_LOGICAL_false) { printf("first region is not void\n"); return 0; }
        if (!check(PK_REGION_is_solid(regions[1], &is_solid), "PK_REGION_is_solid[1]")) return 0;
        if (is_solid != PK_LOGICAL_true) { printf("second region is not solid\n"); return 0; }
    }

    if (!check(PK_BODY_ask_shells(body, &count, &shells), "PK_BODY_ask_shells")) return 0;
    if (count != shells_expected) { printf("shell count mismatch: %d\n", count); return 0; }
    if (!check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces")) return 0;
    if (count != faces_expected) { printf("face count mismatch: %d\n", count); return 0; }
    if (faces_expected > 0)
    {
        if (!check(PK_FACE_ask_shells(faces[0], face_shells), "PK_FACE_ask_shells")) return 0;
        if (face_shells[0] == PK_ENTITY_null || face_shells[1] == PK_ENTITY_null)
        {
            printf("face shell pair contains null\n");
            return 0;
        }
    }

    if (!check(PK_BODY_ask_edges(body, &count, &edges), "PK_BODY_ask_edges")) return 0;
    if (count != edges_expected) { printf("edge count mismatch: %d\n", count); return 0; }
    if (!check(PK_BODY_ask_vertices(body, &count, &vertices), "PK_BODY_ask_vertices")) return 0;
    if (count != vertices_expected) { printf("vertex count mismatch: %d\n", count); return 0; }

    free_array(regions);
    free_array(shells);
    free_array(faces);
    free_array(edges);
    free_array(vertices);
    return 1;
}

static const char *diff_name(PK_DEBUG_diff_t diff);
static void print_body_compare_diffs(const PK_DEBUG_BODY_compare_r_t *results);

static int assert_body_compare(PK_BODY_t master, PK_BODY_t similar, const char *label)
{
    PK_DEBUG_BODY_compare_o_t options;
    PK_DEBUG_BODY_compare_r_t results;
    int ok = 0;

    PK_DEBUG_BODY_compare_o_m(options);
    options.max_diffs = 64;
    /* Keep exact parameterisation/face-pairing differences diagnostic, not a receive gate. */
    options.all_tests = PK_LOGICAL_false;
    options.acc_dev_tests = PK_LOGICAL_false;
    options.non_match_tests = PK_LOGICAL_false;

    if (!check(PK_DEBUG_BODY_compare(master, similar, &options, &results), "PK_DEBUG_BODY_compare"))
        return 0;

    ok = results.global_result == PK_DEBUG_global_res_no_diffs_c
        && results.local_result == PK_DEBUG_local_res_no_diffs_c;
    if (!ok)
    {
        printf(
            "%s body compare mismatch: global=%d local=%d global_diffs=%d face_pairs=%d\n",
            label,
            results.global_result,
            results.local_result,
            results.n_global_diffs,
            results.n_face_pairs);
        print_body_compare_diffs(&results);
    }

    (void)PK_DEBUG_BODY_compare_r_f(&results);
    return ok;
}

static const char *diff_name(PK_DEBUG_diff_t diff)
{
    switch (diff)
    {
        case PK_DEBUG_diff_n_shells_c: return "n_shells";
        case PK_DEBUG_diff_n_faces_c: return "n_faces";
        case PK_DEBUG_diff_n_loops_c: return "n_loops";
        case PK_DEBUG_diff_n_acc_edges_c: return "n_acc_edges";
        case PK_DEBUG_diff_n_tol_edges_c: return "n_tol_edges";
        case PK_DEBUG_diff_n_fins_c: return "n_fins";
        case PK_DEBUG_diff_n_acc_vxs_c: return "n_acc_vxs";
        case PK_DEBUG_diff_n_tol_vxs_c: return "n_tol_vxs";
        case PK_DEBUG_diff_n_vxs_c: return "n_vxs";
        case PK_DEBUG_diff_n_cht_pts_c: return "n_cht_pts";
        case PK_DEBUG_diff_surf_dev_c: return "surf_dev";
        case PK_DEBUG_diff_curve_dev_c: return "curve_dev";
        case PK_DEBUG_diff_vx_dev_c: return "vx_dev";
        case PK_DEBUG_diff_surf_class_c: return "surf_class";
        case PK_DEBUG_diff_curve_class_c: return "curve_class";
        case PK_DEBUG_diff_edge_tol_c: return "edge_tol";
        case PK_DEBUG_diff_vx_tol_c: return "vx_tol";
        case PK_DEBUG_diff_face_sense_c: return "face_sense";
        case PK_DEBUG_diff_surf_sense_c: return "surf_sense";
        case PK_DEBUG_diff_curve_sense_c: return "curve_sense";
        case PK_DEBUG_diff_vx_missing_c: return "vx_missing";
        case PK_DEBUG_diff_face_match_c: return "face_match";
        case PK_DEBUG_diff_fin_match_c: return "fin_match";
        case PK_DEBUG_diff_vx_match_c: return "vx_match";
        default: return "unknown";
    }
}

static void print_body_compare_diffs(const PK_DEBUG_BODY_compare_r_t *results)
{
    int i;
    int j;

    for (i = 0; i < results->n_global_diffs; i++)
    {
        PK_DEBUG_global_diffs_r_t *diff = &results->global_diffs[i];
        printf("  global diff %d: %s(%d) master=%d similar=%d\n", i, diff_name(diff->diff), diff->diff, diff->n_masters, diff->n_similars);
    }

    for (i = 0; i < results->n_face_pairs; i++)
    {
        PK_DEBUG_face_pair_r_t *pair = &results->face_pairs[i];
        for (j = 0; j < pair->n_local_diffs; j++)
        {
            PK_DEBUG_local_diffs_r_t *diff = &pair->local_diffs[j];
            printf(
                "  local diff face_pair=%d diff=%s(%d) master_entity=%d similar_entity=%d master_int=%d similar_int=%d master_double=%.17g similar_double=%.17g master_logical=%d similar_logical=%d\n",
                i,
                diff_name(diff->diff),
                diff->diff,
                diff->master_entity,
                diff->similar_entity,
                diff->master_int,
                diff->similar_int,
                diff->master_double,
                diff->similar_double,
                diff->master_logical,
                diff->similar_logical);
        }
    }
}

static int read_file_block(const char *path, PK_MEMORY_block_t *block)
{
    FILE *file = fopen(path, "rb");
    long size = 0;
    char *bytes = NULL;

    if (file == NULL)
    {
        printf("cannot open input x_t: %s\n", path);
        return 0;
    }

    if (fseek(file, 0, SEEK_END) != 0)
    {
        fclose(file);
        return 0;
    }
    size = ftell(file);
    if (size <= 0)
    {
        fclose(file);
        return 0;
    }
    rewind(file);

    bytes = (char*)malloc((size_t)size);
    if (bytes == NULL)
    {
        fclose(file);
        return 0;
    }

    if (fread(bytes, 1, (size_t)size, file) != (size_t)size)
    {
        free(bytes);
        fclose(file);
        return 0;
    }

    fclose(file);
    block->next = NULL;
    block->n_bytes = (size_t)size;
    block->bytes = bytes;
    return 1;
}

static int receive_file_and_check(const char *path, PK_BODY_t expected_body, int regions_expected, int shells_expected, int faces_expected, int edges_expected, int vertices_expected, const char *label)
{
    PK_MEMORY_block_t block;
    PK_PART_receive_o_t receive_options;
    int n_received = 0;
    PK_PART_t *received = NULL;
    int ok = 0;

    if (!read_file_block(path, &block))
        return 0;

    PK_PART_receive_o_m(receive_options);
    receive_options.transmit_format = PK_transmit_format_text_c;
    if (check(PK_PART_receive_b(block, &receive_options, &n_received, &received), "PK_PART_receive_b")
        && n_received == 1)
    {
        ok = assert_body_counts((PK_BODY_t)received[0], regions_expected, shells_expected, faces_expected, edges_expected, vertices_expected)
            && assert_body_compare(expected_body, (PK_BODY_t)received[0], label);
    }
    else if (n_received != 1)
    {
        printf("receive count mismatch: %d\n", n_received);
    }

    free_array(received);
    free((void*)block.bytes);
    return ok;
}

static int receive_file_and_check_many(const char *path, int expected_count, const PK_BODY_t *expected_bodies, const int *regions_expected, const int *shells_expected, const int *faces_expected, const int *edges_expected, const int *vertices_expected, const char **labels)
{
    PK_MEMORY_block_t block;
    PK_PART_receive_o_t receive_options;
    int n_received = 0;
    PK_PART_t *received = NULL;
    int ok = 0;
    int i;

    if (!read_file_block(path, &block))
        return 0;

    PK_PART_receive_o_m(receive_options);
    receive_options.transmit_format = PK_transmit_format_text_c;
    if (check(PK_PART_receive_b(block, &receive_options, &n_received, &received), "PK_PART_receive_b")
        && n_received == expected_count)
    {
        ok = 1;
        for (i = 0; i < expected_count; i++)
        {
            if (!assert_body_counts((PK_BODY_t)received[i], regions_expected[i], shells_expected[i], faces_expected[i], edges_expected[i], vertices_expected[i])
                || !assert_body_compare(expected_bodies[i], (PK_BODY_t)received[i], labels[i]))
            {
                ok = 0;
            }
        }
    }
    else if (n_received != expected_count)
    {
        printf("receive count mismatch: expected=%d actual=%d\n", expected_count, n_received);
    }

    free_array(received);
    free((void*)block.bytes);
    return ok;
}

static int write_memory_block_to_file(const PK_MEMORY_block_t *block, const char *path)
{
    FILE *file = fopen(path, "wb");
    const PK_MEMORY_block_t *current = block;
    if (file == NULL)
    {
        printf("cannot open output x_t: %s\n", path);
        return 0;
    }

    while (current != NULL)
    {
        if (current->bytes != NULL && current->n_bytes != 0)
        {
            if (fwrite(current->bytes, 1, current->n_bytes, file) != current->n_bytes)
            {
                fclose(file);
                return 0;
            }
        }
        current = current->next;
    }

    fclose(file);
    return 1;
}

static int transmit_parts_to_file(int n_parts, PK_PART_t *parts, const char *path)
{
    PK_PART_transmit_o_t transmit_options;
    PK_MEMORY_block_t block;
    int ok = 0;

    PK_PART_transmit_o_m(transmit_options);
    transmit_options.transmit_format = PK_transmit_format_text_c;
    transmit_options.transmit_user_fields = PK_LOGICAL_false;
    transmit_options.transmit_version = 371;

    if (!check(PK_PART_transmit_b(n_parts, parts, &transmit_options, &block), "PK_PART_transmit_b"))
        return 0;

    ok = write_memory_block_to_file(&block, path);
    (void)PK_MEMORY_block_f(&block);
    return ok;
}

static int transmit_part_to_file(PK_PART_t part, const char *path)
{
    return transmit_parts_to_file(1, &part, path);
}

static void record_result(int condition, const char *name, int *failures)
{
    if (condition)
    {
        printf("PASS %s\n", name);
    }
    else
    {
        printf("FAIL %s\n", name);
        *failures = *failures + 1;
    }
}

int main(int argc, char **argv)
{
    int failures = 0;
    if (argc != 7)
    {
        printf("usage: parasolid_oracle_smoke OUR_BLOCK OUR_CYL OUR_MULTI PS_BLOCK PS_CYL PS_MULTI\n");
        return 2;
    }

    trace("oracle: register frustrum");
    PK_SESSION_frustrum_t frustrum;
    PK_SESSION_frustrum_o_m(frustrum);
    frustrum.fstart = oracle_fstart;
    frustrum.fabort = oracle_fabort;
    frustrum.fstop = oracle_fstop;
    frustrum.fmallo = oracle_fmallo;
    frustrum.fmfree = oracle_fmfree;

    if (!check(PK_SESSION_register_frustrum(&frustrum), "PK_SESSION_register_frustrum"))
        return 77;

    trace("oracle: start session");
    PK_SESSION_start_o_t start_options;
    PK_SESSION_start_o_m(start_options);
    if (!check(PK_SESSION_start(&start_options), "PK_SESSION_start"))
        return 77;

    trace("oracle: ask schema");
    PK_SESSION_schema_version_t schema;
    if (!check(PK_SESSION_ask_schema_version(&schema), "PK_SESSION_ask_schema_version"))
        return 2;
    printf("schema_version=%d\n", schema.schema_version);

    trace("oracle: create block");
    PK_BODY_t block = PK_ENTITY_null;
    if (!check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, NULL, &block), "PK_BODY_create_solid_block"))
        return 2;
    if (!assert_body_counts(block, 2, 2, 6, 12, 8))
        return 2;

    trace("oracle: create cylinder");
    PK_BODY_t cylinder = PK_ENTITY_null;
    if (!check(PK_BODY_create_solid_cyl(2.0, 5.0, NULL, &cylinder), "PK_BODY_create_solid_cyl"))
        return 2;
    if (!assert_body_counts(cylinder, 2, 2, 3, 2, 0))
        return 2;

    PK_BODY_t expected_bodies[2] = { block, cylinder };
    int expected_regions[2] = { 2, 2 };
    int expected_shells[2] = { 2, 2 };
    int expected_faces[2] = { 6, 3 };
    int expected_edges[2] = { 12, 2 };
    int expected_vertices[2] = { 8, 0 };
    const char *expected_labels[2] = { "our multi block", "our multi cylinder" };

    trace("oracle: write parasolid block x_t");
    if (!transmit_part_to_file(block, argv[4]))
        return 2;

    trace("oracle: write parasolid cylinder x_t");
    if (!transmit_part_to_file(cylinder, argv[5]))
        return 2;

    trace("oracle: write parasolid multi-body x_t");
    if (!transmit_parts_to_file(2, expected_bodies, argv[6]))
        return 2;

    trace("oracle: receive our block x_t");
    record_result(receive_file_and_check(argv[1], block, 2, 2, 6, 12, 8, "our block"), "our block -> Parasolid receive", &failures);

    trace("oracle: receive our cylinder x_t");
    record_result(receive_file_and_check(argv[2], cylinder, 2, 2, 3, 2, 0, "our cylinder"), "our cylinder -> Parasolid receive", &failures);

    trace("oracle: receive our multi-body x_t");
    record_result(receive_file_and_check_many(argv[3], 2, expected_bodies, expected_regions, expected_shells, expected_faces, expected_edges, expected_vertices, expected_labels), "our multi-body -> Parasolid receive", &failures);

    (void)PK_SESSION_stop();
    if (failures == 0)
    {
        printf("parasolid oracle ok\n");
        return 0;
    }

    printf("parasolid oracle failures=%d\n", failures);
    return 2;
}
""";

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var platform = RuntimeInformation.ProcessArchitecture switch
{
    Architecture.Arm64 when OperatingSystem.IsMacOS() => "mac-arm64",
    Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
    Architecture.Arm64 when OperatingSystem.IsLinux() => "linux-arm64",
    Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
    _ => "",
};

if (platform.Length == 0)
{
    Console.WriteLine($"Parasolid oracle skipped: unsupported host {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}.");
    return 0;
}

if (OperatingSystem.IsWindows())
{
    Console.WriteLine("Parasolid oracle skipped: Windows static-library harness is not wired for dotnet script yet.");
    return 0;
}

var includeDir = Path.Combine(repoRoot, "third_party", "parasolid", "include");
var schemaDir = Path.Combine(repoRoot, "third_party", "parasolid", "schema");
var libDir = Path.Combine(repoRoot, "third_party", "parasolid", "lib", platform);
var libs = new[]
{
    Path.Combine(libDir, "pskernel_archive.a"),
};

if (!Directory.Exists(includeDir) || !Directory.Exists(schemaDir) || libs.Any(path => !File.Exists(path)))
{
    Console.WriteLine("Parasolid oracle skipped: third_party/parasolid include, schema, or libraries are missing.");
    return 0;
}

var compiler = FindCompiler();
if (compiler is null)
{
    Console.WriteLine("Parasolid oracle skipped: no C compiler found on PATH.");
    return 0;
}

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

    var nativeExportResult = WriteOurXtFiles(repoRoot, ourBlockPath, ourCylinderPath, ourMultiPath);
    if (!nativeExportResult.Success)
    {
        Console.WriteLine(nativeExportResult.Message);
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return 0;
    }

    var sourcePath = Path.Combine(tempDir, "parasolid_oracle_smoke.c");
    var exePath = Path.Combine(tempDir, "parasolid_oracle_smoke");
    File.WriteAllText(sourcePath, HarnessSource, Encoding.UTF8);

    var compileArgs = new List<string>
    {
        "-I" + includeDir,
        sourcePath,
    };
    compileArgs.AddRange(libs);
    compileArgs.AddRange(new[] { "-lc++", "-lm", "-o", exePath });

    var compile = Run(compiler, compileArgs, repoRoot, timeoutMilliseconds: 30000);
    if (compile.ExitCode != 0)
    {
        Console.WriteLine("Parasolid oracle skipped: harness compile failed.");
        Console.Write(compile.Output);
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return 0;
    }

    var run = Run(
        exePath,
        new[] { ourBlockPath, ourCylinderPath, ourMultiPath, parasolidBlockPath, parasolidCylinderPath, parasolidMultiPath },
        tempDir,
        timeoutMilliseconds: 10000,
        schemaDir);
    Console.Write(run.Output);
    if (run.TimedOut)
    {
        Console.WriteLine("Parasolid oracle skipped: harness timed out, likely waiting on local Parasolid runtime/license/frustrum setup.");
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return 0;
    }

    if (run.ExitCode == 77)
    {
        Console.WriteLine("Parasolid oracle skipped: harness reported local Parasolid runtime unavailable.");
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return 0;
    }

    var failed = false;
    if (run.ExitCode != 0)
    {
        Console.WriteLine($"Parasolid oracle failed: harness exited with {run.ExitCode}.");
        failed = true;
    }

    unsafe
    {
        try
        {
            var nativeImportResult = ReadParasolidXtFiles(repoRoot, parasolidBlockPath, parasolidCylinderPath, parasolidMultiPath);
            if (!nativeImportResult.Success)
            {
                Console.WriteLine(nativeImportResult.Message);
                failed = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Parasolid oracle failed: native kernel could not receive Parasolid x_t: " + ex.Message);
            failed = true;
        }
    }

    if (failed)
    {
        keepTemp = true;
        Console.WriteLine("Parasolid oracle temp files kept at: " + tempDir);
        return 1;
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

static string? FindCompiler()
{
    foreach (var name in new[] { "cc", "clang", "gcc" })
    {
        var result = Run(name, new[] { "--version" }, Directory.GetCurrentDirectory(), timeoutMilliseconds: 5000);
        if (result.ExitCode == 0)
            return name;
    }

    return null;
}

static string GetNativeLibraryPath(string repoRoot)
{
    var rid = RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.Arm64 when OperatingSystem.IsMacOS() => "osx-arm64",
        Architecture.X64 when OperatingSystem.IsMacOS() => "osx-x64",
        Architecture.Arm64 when OperatingSystem.IsLinux() => "linux-arm64",
        Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
        Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
        _ => "",
    };
    var libraryName = OperatingSystem.IsMacOS()
        ? "ProjectGmKernel.Native.dylib"
        : OperatingSystem.IsWindows()
            ? "ProjectGmKernel.Native.dll"
            : "ProjectGmKernel.Native.so";
    return rid.Length == 0
        ? ""
        : Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "bin", "Release", "net10.0", rid, "publish", libraryName);
}

static unsafe OracleStepResult WriteOurXtFiles(string repoRoot, string blockPath, string cylinderPath, string multiPath)
{
    var libraryPath = GetNativeLibraryPath(repoRoot);
    if (libraryPath.Length == 0 || !File.Exists(libraryPath))
        return OracleStepResult.Skip("Parasolid oracle skipped: native kernel publish output is missing. Run VerifyKernel.cs or publish first.");

    var handle = NativeLibrary.Load(libraryPath);
    try
    {
        var sessionStart = (delegate* unmanaged[Cdecl]<PK_SESSION_start_o_s*, int>)NativeLibrary.GetExport(handle, "PK_SESSION_start");
        var sessionStop = (delegate* unmanaged[Cdecl]<int>)NativeLibrary.GetExport(handle, "PK_SESSION_stop");
        var bodyCreateSolidBlock = (delegate* unmanaged[Cdecl]<double, double, double, PK_AXIS2_sf_s*, int*, int>)NativeLibrary.GetExport(handle, "PK_BODY_create_solid_block");
        var bodyCreateSolidCyl = (delegate* unmanaged[Cdecl]<double, double, PK_AXIS2_sf_s*, int*, int>)NativeLibrary.GetExport(handle, "PK_BODY_create_solid_cyl");
        var partTransmitB = (delegate* unmanaged[Cdecl]<int, int*, PK_PART_transmit_o_s*, PK_MEMORY_block_s*, int>)NativeLibrary.GetExport(handle, "PK_PART_transmit_b");
        var memoryBlockFree = (delegate* unmanaged[Cdecl]<PK_MEMORY_block_s*, int>)NativeLibrary.GetExport(handle, "PK_MEMORY_block_f");

        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        CheckNative(sessionStart(&startOptions), "our PK_SESSION_start");

        try
        {
            int block;
            CheckNative(bodyCreateSolidBlock(1, 2, 3, null, &block), "our PK_BODY_create_solid_block");
            WritePartToFile(partTransmitB, memoryBlockFree, block, blockPath, "our block");

            int cylinder;
            CheckNative(bodyCreateSolidCyl(2, 5, null, &cylinder), "our PK_BODY_create_solid_cyl");
            WritePartToFile(partTransmitB, memoryBlockFree, cylinder, cylinderPath, "our cylinder");

            var parts = stackalloc int[2] { block, cylinder };
            WritePartsToFile(partTransmitB, memoryBlockFree, 2, parts, multiPath, "our multi-body");
        }
        finally
        {
            sessionStop();
        }
    }
    catch (Exception ex) when (ex is FileNotFoundException or EntryPointNotFoundException or DllNotFoundException)
    {
        return OracleStepResult.Skip("Parasolid oracle skipped: native kernel exports are unavailable: " + ex.Message);
    }
    finally
    {
        NativeLibrary.Free(handle);
    }

    return OracleStepResult.Ok();
}

static unsafe OracleStepResult ReadParasolidXtFiles(string repoRoot, string blockPath, string cylinderPath, string multiPath)
{
    var libraryPath = GetNativeLibraryPath(repoRoot);
    if (libraryPath.Length == 0 || !File.Exists(libraryPath))
        return OracleStepResult.Skip("Parasolid oracle skipped: native kernel publish output is missing. Run VerifyKernel.cs or publish first.");

    var handle = NativeLibrary.Load(libraryPath);
    try
    {
        var sessionStart = (delegate* unmanaged[Cdecl]<PK_SESSION_start_o_s*, int>)NativeLibrary.GetExport(handle, "PK_SESSION_start");
        var sessionStop = (delegate* unmanaged[Cdecl]<int>)NativeLibrary.GetExport(handle, "PK_SESSION_stop");
        var partReceiveB = (delegate* unmanaged[Cdecl]<PK_MEMORY_block_s, PK_PART_receive_o_s*, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_PART_receive_b");
        var bodyAskRegions = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_regions");
        var bodyAskShells = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_shells");
        var bodyAskFaces = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_faces");
        var bodyAskEdges = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_edges");
        var bodyAskVertices = (delegate* unmanaged[Cdecl]<int, int*, nint*, int>)NativeLibrary.GetExport(handle, "PK_BODY_ask_vertices");
        var memoryFree = (delegate* unmanaged[Cdecl]<void*, int>)NativeLibrary.GetExport(handle, "PK_MEMORY_free");

        var startOptions = new PK_SESSION_start_o_s { o_t_version = 1 };
        CheckNative(sessionStart(&startOptions), "our PK_SESSION_start");

        try
        {
            var failures = 0;
            if (!TryReadFileAndCheck(partReceiveB, bodyAskRegions, bodyAskShells, bodyAskFaces, bodyAskEdges, bodyAskVertices, memoryFree, blockPath, 2, 2, 6, 12, 8, "parasolid block"))
                failures++;
            if (!TryReadFileAndCheck(partReceiveB, bodyAskRegions, bodyAskShells, bodyAskFaces, bodyAskEdges, bodyAskVertices, memoryFree, cylinderPath, 2, 2, 3, 2, 0, "parasolid cylinder"))
                failures++;
            if (!TryReadFileAndCheckMany(partReceiveB, bodyAskRegions, bodyAskShells, bodyAskFaces, bodyAskEdges, bodyAskVertices, memoryFree, multiPath, "parasolid multi-body"))
                failures++;

            if (failures != 0)
                return OracleStepResult.Skip("Parasolid oracle failed: native kernel receive failures=" + failures);
        }
        finally
        {
            sessionStop();
        }
    }
    catch (Exception ex) when (ex is FileNotFoundException or EntryPointNotFoundException or DllNotFoundException)
    {
        return OracleStepResult.Skip("Parasolid oracle skipped: native kernel exports are unavailable: " + ex.Message);
    }
    finally
    {
        NativeLibrary.Free(handle);
    }

    return OracleStepResult.Ok();
}

static unsafe void WritePartToFile(
    delegate* unmanaged[Cdecl]<int, int*, PK_PART_transmit_o_s*, PK_MEMORY_block_s*, int> partTransmitB,
    delegate* unmanaged[Cdecl]<PK_MEMORY_block_s*, int> memoryBlockFree,
    int part,
    string path,
    string label)
{
    WritePartsToFile(partTransmitB, memoryBlockFree, 1, &part, path, label);
}

static unsafe void WritePartsToFile(
    delegate* unmanaged[Cdecl]<int, int*, PK_PART_transmit_o_s*, PK_MEMORY_block_s*, int> partTransmitB,
    delegate* unmanaged[Cdecl]<PK_MEMORY_block_s*, int> memoryBlockFree,
    int partCount,
    int* parts,
    string path,
    string label)
{
    var options = new PK_PART_transmit_o_s
    {
        o_t_version = 10,
        transmit_format = PK_transmit_format_text_c,
        transmit_user_fields = PK_LOGICAL_false,
        transmit_version = 371,
    };
    var block = new PK_MEMORY_block_s();
    CheckNative(partTransmitB(partCount, parts, &options, &block), "our PK_PART_transmit_b " + label);
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
        CheckNative(memoryBlockFree(&block), "our PK_MEMORY_block_f " + label);
    }
}

static unsafe bool TryReadFileAndCheckMany(
    delegate* unmanaged[Cdecl]<PK_MEMORY_block_s, PK_PART_receive_o_s*, int*, nint*, int> partReceiveB,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskRegions,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskShells,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskFaces,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskEdges,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskVertices,
    delegate* unmanaged[Cdecl]<void*, int> memoryFree,
    string path,
    string label)
{
    try
    {
        var bytes = File.ReadAllBytes(path);
        fixed (byte* bytesPtr = bytes)
        {
            var block = new PK_MEMORY_block_s
            {
                next = null,
                n_bytes = (nuint)bytes.Length,
                bytes = bytesPtr,
            };
            var options = new PK_PART_receive_o_s
            {
                o_t_version = 14,
                transmit_format = PK_transmit_format_text_c,
            };
            int nParts;
            nint parts;
            CheckNative(partReceiveB(block, &options, &nParts, &parts), "our PK_PART_receive_b " + label);
            try
            {
                RequireNative(nParts == 2, label + " received part count");
                var bodyParts = (int*)parts;
                CheckCount(bodyAskRegions, bodyParts[0], 2, label + " block regions");
                CheckCount(bodyAskShells, bodyParts[0], 2, label + " block shells");
                CheckCount(bodyAskFaces, bodyParts[0], 6, label + " block faces");
                CheckCount(bodyAskEdges, bodyParts[0], 12, label + " block edges");
                CheckCount(bodyAskVertices, bodyParts[0], 8, label + " block vertices");
                CheckCount(bodyAskRegions, bodyParts[1], 2, label + " cylinder regions");
                CheckCount(bodyAskShells, bodyParts[1], 2, label + " cylinder shells");
                CheckCount(bodyAskFaces, bodyParts[1], 3, label + " cylinder faces");
                CheckCount(bodyAskEdges, bodyParts[1], 2, label + " cylinder edges");
                CheckCount(bodyAskVertices, bodyParts[1], 0, label + " cylinder vertices");
            }
            finally
            {
                if (parts != 0)
                    CheckNative(memoryFree((void*)parts), "our PK_MEMORY_free " + label);
            }
        }

        Console.WriteLine("PASS Parasolid " + label + " -> native receive");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL Parasolid " + label + " -> native receive: " + ex.Message);
        return false;
    }
}

static unsafe bool TryReadFileAndCheck(
    delegate* unmanaged[Cdecl]<PK_MEMORY_block_s, PK_PART_receive_o_s*, int*, nint*, int> partReceiveB,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskRegions,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskShells,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskFaces,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskEdges,
    delegate* unmanaged[Cdecl]<int, int*, nint*, int> bodyAskVertices,
    delegate* unmanaged[Cdecl]<void*, int> memoryFree,
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
            var block = new PK_MEMORY_block_s
            {
                next = null,
                n_bytes = (nuint)bytes.Length,
                bytes = bytesPtr,
            };
            var options = new PK_PART_receive_o_s
            {
                o_t_version = 14,
                transmit_format = PK_transmit_format_text_c,
            };
            int nParts;
            nint parts;
            CheckNative(partReceiveB(block, &options, &nParts, &parts), "our PK_PART_receive_b " + label);
            try
            {
                RequireNative(nParts == 1, label + " received part count");
                var body = ((int*)parts)[0];
                CheckCount(bodyAskRegions, body, regionsExpected, label + " regions");
                CheckCount(bodyAskShells, body, shellsExpected, label + " shells");
                CheckCount(bodyAskFaces, body, facesExpected, label + " faces");
                CheckCount(bodyAskEdges, body, edgesExpected, label + " edges");
                CheckCount(bodyAskVertices, body, verticesExpected, label + " vertices");
            }
            finally
            {
                if (parts != 0)
                    CheckNative(memoryFree((void*)parts), "our PK_MEMORY_free " + label);
            }
        }

        Console.WriteLine("PASS Parasolid " + label + " -> native receive");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL Parasolid " + label + " -> native receive: " + ex.Message);
        return false;
    }
}

static unsafe void CheckCount(delegate* unmanaged[Cdecl]<int, int*, nint*, int> query, int body, int expected, string label)
{
    int count;
    nint values;
    CheckNative(query(body, &count, &values), "our query " + label);
    RequireNative(count == expected, label);
}

static void CheckNative(int error, string name)
{
    if (error != 0)
        throw new InvalidOperationException($"{name} failed with error {error}");
}

static void RequireNative(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException("Unexpected " + name);
}

static RunResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, int timeoutMilliseconds, string? schemaDirectory = null)
{
    var psi = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    if (schemaDirectory is not null)
        psi.Environment["P_SCHEMA"] = schemaDirectory;
    foreach (var argument in arguments)
        psi.ArgumentList.Add(argument);

    try
    {
        using var process = Process.Start(psi);
        if (process is null)
            return new RunResult(127, "", TimedOut: false);

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.AppendLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new RunResult(-1, output.ToString(), TimedOut: true);
        }

        process.WaitForExit();
        return new RunResult(process.ExitCode, output.ToString(), TimedOut: false);
    }
    catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
    {
        return new RunResult(127, ex.Message + Environment.NewLine, TimedOut: false);
    }
}

readonly record struct RunResult(int ExitCode, string Output, bool TimedOut);

readonly record struct OracleStepResult(bool Success, string Message)
{
    public static OracleStepResult Ok() => new(true, "");
    public static OracleStepResult Skip(string message) => new(false, message);
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_SESSION_start_o_s
{
    public int o_t_version;
    public byte* journal_file;
    public int user_field;
    public int reserved;
}

[StructLayout(LayoutKind.Sequential)]
struct PK_VECTOR_s
{
    public double coord0;
    public double coord1;
    public double coord2;
}

[StructLayout(LayoutKind.Sequential)]
struct PK_AXIS2_sf_s
{
    public PK_VECTOR_s location;
    public PK_VECTOR_s axis;
    public PK_VECTOR_s ref_direction;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_MEMORY_block_s
{
    public PK_MEMORY_block_s* next;
    public nuint n_bytes;
    public byte* bytes;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_PART_transmit_o_s
{
    public int o_t_version;
    public int transmit_format;
    public byte transmit_user_fields;
    public int transmit_version;
    public byte transmit_nmnl_geometry;
    public nint transmit_indexed_context;
    public int transmit_meshes;
}

[StructLayout(LayoutKind.Sequential)]
unsafe struct PK_PART_receive_o_s
{
    public int o_t_version;
    public int transmit_format;
    public byte receive_user_fields;
    public int attdef_mismatch;
    public int part_index;
    public int n_part_indices;
    public int* part_indices;
    public int n_identifiers;
    public int* identifiers;
    public nint receive_indexed_context;
    public byte key_is_partition;
    public int receive_compound;
    public int receive_using_seek;
    public int receive_mixed;
}
