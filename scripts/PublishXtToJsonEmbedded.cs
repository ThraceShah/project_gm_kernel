#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

// Publishes XtToJson with every schema file from an XT schema directory embedded
// into the executable (MSBuild property XtEmbeddedSchemaDir), so it converts x_t
// files without --schema-dir / PARASOLID_SCHEMA_DIR / P_SCHEMA.
//
// Internal use only: published artifacts must not contain raw .sch_txt text
// (docs/xt_all_versions_support_status.md; scripts/ScanXtArtifacts.cs fails by
// design while embedding is on), so regular builds keep embedding off and this
// script is the sanctioned way to opt in.
//
// Usage:
//   dotnet run --file scripts/PublishXtToJsonEmbedded.cs
//     --schema-dir <dir>   XT schema directory; default third_party/parasolid/schema
//     --rid <rid>          target runtime; default linux-x64
//     --output <dir>       output directory; default bin/xt-json-tool-embedded/<rid>
//
// The script self-verifies the embedding by converting a probe x_t whose schema
// exists only in the directory (not among the built-in bindings): resolution
// without --schema-dir can only succeed through the embedded resources.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Xt;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath())!;
var repositoryRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var schemaDirectory = ReadOption(args, "--schema-dir") ?? Path.Combine(repositoryRoot, "third_party", "parasolid", "schema");
schemaDirectory = Path.GetFullPath(schemaDirectory);
var rid = ReadOption(args, "--rid") ?? "linux-x64";
var output = ReadOption(args, "--output") ?? Path.Combine(repositoryRoot, "bin", "xt-json-tool-embedded", rid);
var project = Path.Combine(repositoryRoot, "src", "ProjectGmKernel.Xt.JsonTool", "ProjectGmKernel.Xt.JsonTool.csproj");

if (!Directory.Exists(schemaDirectory))
{
    Console.Error.WriteLine($"XT schema directory does not exist: {schemaDirectory}");
    return 1;
}
if (!Directory.EnumerateFiles(schemaDirectory, "sch_*.sch_txt").Any() && !Directory.EnumerateFiles(schemaDirectory, "sch_*.s_t").Any())
{
    Console.Error.WriteLine($"XT schema directory contains no sch_*.sch_txt / sch_*.s_t files: {schemaDirectory}");
    return 1;
}

// Publish with embedding enabled; the linker keeps every resource.
Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
if (Run("dotnet", $"publish \"{project}\" -c Release -r {rid} -o \"{output}\" -p:XtEmbeddedSchemaDir=\"{schemaDirectory}\"") != 0)
{
    Console.Error.WriteLine("Publish failed.");
    return 1;
}
var executable = Path.Combine(output, rid.StartsWith("win", StringComparison.OrdinalIgnoreCase) ? "XtToJson.exe" : "XtToJson");
if (!File.Exists(executable))
{
    Console.Error.WriteLine($"Published executable is missing: {executable}");
    return 1;
}

// Self-verify: pick a schema that is present in the directory but not among the
// built-in bindings, and check that XtToJson resolves it without --schema-dir.
var catalog = XtSchemaCatalog.OpenDirectory(schemaDirectory);
var builtIn = XtBuiltInSchemas.Identities.ToHashSet(StringComparer.Ordinal);
XtSchemaInfo? probe = null;
foreach (var info in catalog.Schemas)
{
    if (!builtIn.Contains(info.Identity))
    {
        probe = info;
        break;
    }
}
if (probe is null)
{
    Console.WriteLine($"Published {executable} (directory holds only built-in schemas; embedding smoke test skipped).");
    return 0;
}
var probeInfo = probe.Value;
var probePath = Path.Combine(Path.GetTempPath(), "pgm-xt-embed-smoke-" + Guid.NewGuid().ToString("N") + ".x_t");
var probeJson = Path.ChangeExtension(probePath, ".json");
var versionText = ": TRANSMIT FILE created by modeller version " + probeInfo.ModelerVersion;
File.WriteAllText(probePath, $"T {versionText.Length} {versionText} {probeInfo.Identity.Length} {probeInfo.Identity} 0 1 0\n");
try
{
    var (exitCode, stdout, stderr) = Capture(executable, $"\"{probePath}\" -o \"{probeJson}\"");
    if (exitCode == 2 && stderr.Contains("No generated model binding exists for " + probeInfo.Identity, StringComparison.Ordinal))
    {
        // Expected: the embedded schema resolved (catalog stage passed); only the
        // typed model binding is absent, which is out of scope for a probe.
    }
    else if (stderr.Contains("is not present in", StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"Embedding verification failed: {probeInfo.Identity} did not resolve without --schema-dir. stdout:\n{stdout}\nstderr:\n{stderr}");
        return 1;
    }
    else
    {
        Console.Error.WriteLine($"Unexpected probe result (exit {exitCode}). stdout:\n{stdout}\nstderr:\n{stderr}");
        return 1;
    }
}
finally
{
    File.Delete(probePath);
    if (File.Exists(probeJson)) File.Delete(probeJson);
}

Console.WriteLine($"Published {executable}");
Console.WriteLine($"Embedding verified: {probeInfo.Identity} resolved without --schema-dir.");
Console.WriteLine("Note: this artifact contains raw Parasolid schema text - internal use only, do not redistribute.");
return 0;

static string? ReadOption(string[] values, string option)
{
    for (var index = 0; index < values.Length; index++)
    {
        if (values[index] != option)
            continue;
        return index + 1 < values.Length ? values[index + 1] : throw new ArgumentException(option + " requires a value.");
    }
    return null;
}

static int Run(string fileName, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo(fileName, arguments));
    process!.WaitForExit();
    return process.ExitCode;
}

static (int ExitCode, string StdOut, string StdErr) Capture(string fileName, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    })!;
    process.WaitForExit();
    return (process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
}
