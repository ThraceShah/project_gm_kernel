#!/usr/bin/env dotnet run
// Receives one text x_t file with real Parasolid and checks topology counts.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

const int PK_transmit_format_text_c = 18220;
const int PK_LOGICAL_false = 0;

const string HarnessSource = """
#include <stdio.h>
#include <stdlib.h>
#include "parasolid_kernel.h"

static void oracle_fstart(int *ifail) { *ifail = 0; }
static void oracle_fabort(int *ifail) { *ifail = 0; }
static void oracle_fstop(int *ifail) { *ifail = 0; }
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
    if (ptr != NULL) (void)PK_MEMORY_free(ptr);
}

static int read_file_block(const char *path, PK_MEMORY_block_t *block)
{
    FILE *file = fopen(path, "rb");
    long size = 0;
    char *bytes = NULL;
    if (file == NULL) return 0;
    if (fseek(file, 0, SEEK_END) != 0) { fclose(file); return 0; }
    size = ftell(file);
    if (size <= 0) { fclose(file); return 0; }
    rewind(file);
    bytes = (char*)malloc((size_t)size);
    if (bytes == NULL) { fclose(file); return 0; }
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

static int assert_body_counts(PK_BODY_t body, int regions_expected, int shells_expected, int faces_expected, int edges_expected, int vertices_expected)
{
    int count = 0;
    PK_REGION_t *regions = NULL;
    PK_SHELL_t *shells = NULL;
    PK_FACE_t *faces = NULL;
    PK_EDGE_t *edges = NULL;
    PK_VERTEX_t *vertices = NULL;
    PK_LOGICAL_t is_solid = PK_LOGICAL_false;

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

int main(int argc, char **argv)
{
    if (argc != 7)
    {
        printf("usage: receive_check FILE REGIONS SHELLS FACES EDGES VERTICES\n");
        return 2;
    }

    PK_SESSION_frustrum_t frustrum;
    PK_SESSION_frustrum_o_m(frustrum);
    frustrum.fstart = oracle_fstart;
    frustrum.fabort = oracle_fabort;
    frustrum.fstop = oracle_fstop;
    frustrum.fmallo = oracle_fmallo;
    frustrum.fmfree = oracle_fmfree;
    if (!check(PK_SESSION_register_frustrum(&frustrum), "PK_SESSION_register_frustrum")) return 77;

    PK_SESSION_start_o_t start_options;
    PK_SESSION_start_o_m(start_options);
    if (!check(PK_SESSION_start(&start_options), "PK_SESSION_start")) return 77;

    PK_MEMORY_block_t block;
    PK_PART_receive_o_t receive_options;
    int n_received = 0;
    PK_PART_t *received = NULL;
    int ok = 0;
    if (!read_file_block(argv[1], &block)) return 2;

    PK_PART_receive_o_m(receive_options);
    receive_options.transmit_format = PK_transmit_format_text_c;
    if (check(PK_PART_receive_b(block, &receive_options, &n_received, &received), "PK_PART_receive_b") && n_received == 1)
    {
        ok = assert_body_counts((PK_BODY_t)received[0], atoi(argv[2]), atoi(argv[3]), atoi(argv[4]), atoi(argv[5]), atoi(argv[6]));
    }

    free_array(received);
    free((void*)block.bytes);
    (void)PK_SESSION_stop();
    if (ok) { printf("receive ok\n"); return 0; }
    return 1;
}
""";

static string GetScriptPath([CallerFilePath] string path = "") => path;

if (args.Length != 6)
{
    Console.Error.WriteLine("usage: dotnet run scripts/ParasolidReceiveCheck.cs -- FILE REGIONS SHELLS FACES EDGES VERTICES");
    return 2;
}

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var platform = RuntimeInformation.ProcessArchitecture switch
{
    Architecture.Arm64 when OperatingSystem.IsMacOS() => "mac-arm64",
    Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
    Architecture.Arm64 when OperatingSystem.IsLinux() => "linux-arm64",
    _ => "",
};
if (platform.Length == 0)
{
    Console.WriteLine("Parasolid receive check skipped: unsupported host.");
    return 0;
}

var includeDir = Path.Combine(repoRoot, "third_party", "parasolid", "include");
var schemaDir = Path.Combine(repoRoot, "third_party", "parasolid", "schema");
var libPath = Path.Combine(repoRoot, "third_party", "parasolid", "lib", platform, "pskernel_archive.a");
if (!Directory.Exists(includeDir) || !Directory.Exists(schemaDir) || !File.Exists(libPath))
{
    Console.WriteLine("Parasolid receive check skipped: third_party/parasolid files are missing.");
    return 0;
}

var compiler = FindCompiler();
if (compiler is null)
{
    Console.WriteLine("Parasolid receive check skipped: no C compiler found.");
    return 0;
}

var tempDir = Path.Combine(Path.GetTempPath(), "project-gm-parasolid-receive-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    var sourcePath = Path.Combine(tempDir, "receive_check.c");
    var exePath = Path.Combine(tempDir, "receive_check");
    File.WriteAllText(sourcePath, HarnessSource, Encoding.UTF8);

    var compile = Run(compiler, ["-I" + includeDir, sourcePath, libPath, "-lc++", "-lm", "-o", exePath], repoRoot, 30000);
    if (compile.ExitCode != 0)
    {
        Console.Write(compile.Output);
        return 1;
    }

    var run = Run(exePath, args, tempDir, 10000, schemaDir);
    Console.Write(run.Output);
    return run.ExitCode;
}
finally
{
    try { Directory.Delete(tempDir, recursive: true); } catch { }
}

static string? FindCompiler()
{
    foreach (var name in new[] { "cc", "clang", "gcc" })
    {
        var result = Run(name, ["--version"], Directory.GetCurrentDirectory(), 5000);
        if (result.ExitCode == 0)
            return name;
    }
    return null;
}

static RunResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, int timeoutMilliseconds, string? schemaDir = null)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var argument in arguments)
        startInfo.ArgumentList.Add(argument);
    if (schemaDir is not null)
        startInfo.Environment["P_SCHEMA"] = schemaDir;

    var output = new StringBuilder();
    try
    {
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            return new RunResult(124, output.ToString());
        }
        process.WaitForExit();
        return new RunResult(process.ExitCode, output.ToString());
    }
    catch (Win32Exception ex)
    {
        return new RunResult(127, ex.Message + Environment.NewLine);
    }
}

readonly record struct RunResult(int ExitCode, string Output);
