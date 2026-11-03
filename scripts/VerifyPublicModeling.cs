#!/usr/bin/env dotnet run
// Reproduce the stage A/B acceptance gates using two independent kernels.
// Usage: dotnet run scripts/VerifyPublicModeling.cs
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

var scriptDirectory=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
var configuredSchema=Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR");
if(string.IsNullOrWhiteSpace(configuredSchema))configuredSchema=Environment.GetEnvironmentVariable("P_SCHEMA");
if(string.IsNullOrWhiteSpace(configuredSchema))configuredSchema=Path.Combine("..","third_party","parasolid","schema");
var schemaDirectory=Path.GetFullPath(configuredSchema,scriptDirectory);
if(!Directory.Exists(schemaDirectory))
{
    Console.Error.WriteLine("The A/B acceptance suite requires an accessible Parasolid schema directory. Set PARASOLID_SCHEMA_DIR relative to this script or install the configured SDK.");
    return 1;
}
var directory=Path.Combine(root,"temp_docs","public-modeling-verification");Directory.CreateDirectory(directory);
var rid=RuntimeInformation.RuntimeIdentifier;
var steps=new List<(string Name,string[] Arguments,bool Cleanup)>
{
    ("kernel-tests",["test","tests/KernelTests/KernelTests.csproj","-m:1","-p:UseSharedCompilation=false"],true),
    ("xt-tests",["test","tests/ProjectGmKernel.Xt.Tests/ProjectGmKernel.Xt.Tests.csproj","-m:1","-p:UseSharedCompilation=false"],true),
    ("display-build",["build","src/ProjectGmKernel.DisplayBridge/ProjectGmKernel.DisplayBridge.csproj","-m:1","-p:UseSharedCompilation=false"],false),
};
foreach(var oracle in new[]{"GeometryPublicApiOracle","GeometryTransformOracle","BrepImportOracle","ProfileModelingOracle","IcurveEvaluationOracle","DisplayBridgeOracle"})
{
    steps.Add((oracle,["run","--no-cache",$"scripts/{oracle}.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:BuildProjectReferences=false"],false));
    if(oracle=="ProfileModelingOracle")
    {
        // Test the A/B boundary in both directions: our new solids and the
        // independently produced PK reference solids must survive import,
        // fresh export, deep copy and deletion with identical topology.
        steps.Add(("modeled-body-import",["run","--no-cache","scripts/BrepImportOracle.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:BuildProjectReferences=false","--","../temp_docs/profile-modeling-oracle"],false));
        steps.Add(("reference-model-import",["run","--no-cache","scripts/BrepImportOracle.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:BuildProjectReferences=false","--","../temp_docs/profile-modeling-oracle","--reference"],false));
    }
}
// A crashed analyzer can otherwise leave a successful build with incomplete
// AOT/trim checks (AD0001). Keep that failure visible in the release gate.
var publish=new List<string>{"publish","src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj","-c","Release","-r",rid,"-m:1","-p:UseSharedCompilation=false","-p:WarningsAsErrors=AD0001"};
if(OperatingSystem.IsLinux())publish.Add("-p:CppCompilerAndLinker=gcc");
if(Environment.GetEnvironmentVariable("GM_IN_PROCESS_LINK_TASKS")=="1")publish.Add("-p:GmInProcessLinkTasks=true");
steps.Add(("native-publish",publish.ToArray(),false));
steps.Add(("native-abi",["run","scripts/AbiSmoke.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false"],false));
steps.Add(("native-display",["run","--no-cache","scripts/DisplayBridgeOracle.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:BuildProjectReferences=false","--","--native"],false));

var summary=new List<string>();
foreach(var step in steps)
{
    var watch=Stopwatch.StartNew();Console.WriteLine($"> {step.Name}");
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var argument in step.Arguments)start.ArgumentList.Add(argument);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    // Pass the schema to each process, including the separately loaded
    // NativeAOT runtime. A previous oracle's in-process setup cannot do this.
    start.Environment["PARASOLID_SCHEMA_DIR"]=schemaDirectory;
    start.Environment["P_SCHEMA"]=schemaDirectory;
    using var process=Process.Start(start)??throw new InvalidOperationException("Could not start dotnet.");
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    File.WriteAllText(Path.Combine(directory,step.Name+".log"),await stdout+await stderr);
    if(step.Cleanup&&!OperatingSystem.IsWindows())
    {
        using var cleanup=Process.Start(new ProcessStartInfo("pkill"){ArgumentList={"-f","[t]esthost.dll"},UseShellExecute=false});
        if(cleanup!=null)await cleanup.WaitForExitAsync();
    }
    var line=$"{(process.ExitCode==0?"PASS":"FAIL")} {step.Name}, exit={process.ExitCode}, seconds={watch.Elapsed.TotalSeconds:F1}";
    summary.Add(line);Console.WriteLine(line);File.WriteAllLines(Path.Combine(directory,"summary.txt"),summary);
    if(process.ExitCode!=0)return process.ExitCode;
}
Console.WriteLine("PASS all stage A/B acceptance gates.");
return 0;

static string ScriptPath([CallerFilePath]string path="")=>path;
