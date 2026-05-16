#!/usr/bin/env dotnet run
// Runs the project verification checks that are easy to skip by hand.
//
// Usage: dotnet run scripts/VerifyKernel.cs

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var rid = RuntimeInformation.ProcessArchitecture switch
{
    Architecture.Arm64 when OperatingSystem.IsMacOS() => "osx-arm64",
    Architecture.X64 when OperatingSystem.IsMacOS() => "osx-x64",
    Architecture.Arm64 when OperatingSystem.IsLinux() => "linux-arm64",
    Architecture.X64 when OperatingSystem.IsLinux() => "linux-x64",
    Architecture.X64 when OperatingSystem.IsWindows() => "win-x64",
    _ => throw new NotSupportedException($"Unsupported verification host: {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture}"),
};

var testExit = Run("dotnet", "test tests/KernelTests", cleanupTesthost: true);
if (testExit != 0)
    return testExit;

var publishExit = Run("dotnet", $"publish src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj -c Release -r {rid}", cleanupTesthost: false);
if (publishExit != 0)
    return publishExit;

var abiSmokeExit = Run("dotnet", "run scripts/AbiSmoke.cs", cleanupTesthost: false);
if (abiSmokeExit != 0)
    return abiSmokeExit;

var topologyDumpExit = Run("dotnet", "run scripts/TopologyDump.cs", cleanupTesthost: false);
if (topologyDumpExit != 0)
    return topologyDumpExit;

var allocationBaselineExit = Run("dotnet", "run scripts/AllocationBaseline.cs", cleanupTesthost: false);
if (allocationBaselineExit != 0)
    return allocationBaselineExit;

var manualExportsPath = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "KernelExports.cs");
var generatedExportsPath = Path.Combine(repoRoot, "src", "ProjectGmKernel.Native", "Generated", "KernelExports.generated.cs");
var manualExports = Count(manualExportsPath, @"UnmanagedCallersOnly\(EntryPoint");
var generatedExports = Count(generatedExportsPath, @"UnmanagedCallersOnly\(EntryPoint");
var generatedStubs = Count(generatedExportsPath, @"KernelRuntime\.NotImplemented\(\)");

Console.WriteLine();
Console.WriteLine("Verification summary");
Console.WriteLine($"  RuntimeIdentifier: {rid}");
Console.WriteLine($"  Implemented manual exports: {manualExports}");
Console.WriteLine($"  Generated export stubs: {generatedExports}");
Console.WriteLine($"  NotImplemented generated stubs: {generatedStubs}");

return generatedExports == generatedStubs ? 0 : 1;

int Run(string fileName, string arguments, bool cleanupTesthost)
{
    Console.WriteLine($"> {fileName} {arguments}");
    var psi = new ProcessStartInfo(fileName, arguments)
    {
        WorkingDirectory = repoRoot,
        UseShellExecute = false,
    };
    psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}");
    process.WaitForExit();

    if (cleanupTesthost)
        CleanupTesthost();

    return process.ExitCode;
}

void CleanupTesthost()
{
    if (OperatingSystem.IsWindows())
        return;

    using var process = Process.Start(new ProcessStartInfo("pkill", "-f testhost.dll")
    {
        WorkingDirectory = repoRoot,
        UseShellExecute = false,
    });
    process?.WaitForExit();
}

static int Count(string path, string pattern)
{
    var text = File.ReadAllText(path);
    return Regex.Count(text, pattern);
}
