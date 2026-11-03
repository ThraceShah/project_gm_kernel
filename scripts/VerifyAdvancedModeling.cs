#!/usr/bin/env dotnet run
// Reproducible C/D/E acceptance plus the complete existing A/B regression suite.
using System.Diagnostics;
using System.Runtime.CompilerServices;

var scriptDirectory=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
var configured=Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR")??Environment.GetEnvironmentVariable("P_SCHEMA")
    ??Path.Combine("..","third_party","parasolid","schema");
var schema=Path.GetFullPath(configured,scriptDirectory);
if(!Directory.Exists(schema)){Console.Error.WriteLine("The advanced modeling suite requires the real Parasolid SDK schema. Configure PARASOLID_SCHEMA_DIR relative to this script.");return 1;}
var output=Path.Combine(root,"temp_docs","advanced-modeling-verification");Directory.CreateDirectory(output);
var steps=new List<(string Name,string[] Arguments)>
{
    ("production-build",["build","src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj","-m:1","-p:UseSharedCompilation=false"]),
};
foreach(var oracle in new[]{"IntersectionOperationOracle","BooleanOperationOracle","PlanarTrackingOracle","AnalyticBooleanOracle","AnalyticSectionOracle","FreeformModelingOracle","LocalOperationOracle"})
    steps.Add((oracle,["run","--no-cache",$"scripts/{oracle}.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:BuildProjectReferences=false"]));
// This child preserves the actual test exit before cleanup, checks all kernel
// and XT tests, replays the A/B oracles, publishes NativeAOT and exercises display.
steps.Add(("public-modeling-regression",["run","--no-cache","scripts/VerifyPublicModeling.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false"]));
steps.Add(("native-advanced-modeling",["run","--no-cache","scripts/NativeAdvancedModelingSmoke.cs","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:BuildProjectReferences=false"]));
var summary=new List<string>();
foreach(var step in steps)
{
    Console.WriteLine("> "+step.Name);var elapsed=Stopwatch.StartNew();
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var argument in step.Arguments)start.ArgumentList.Add(argument);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";start.Environment["P_SCHEMA"]=schema;start.Environment["PARASOLID_SCHEMA_DIR"]=schema;
    using var process=Process.Start(start)??throw new Exception("Could not start "+step.Name);
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    File.WriteAllText(Path.Combine(output,step.Name+".log"),await stdout+await stderr);
    var line=$"{(process.ExitCode==0?"PASS":"FAIL")} {step.Name}, exit={process.ExitCode}, seconds={elapsed.Elapsed.TotalSeconds:F1}";
    summary.Add(line);Console.WriteLine(line);File.WriteAllLines(Path.Combine(output,"summary.txt"),summary);
    if(process.ExitCode!=0)return process.ExitCode;
}
Console.WriteLine("PASS all advanced modeling gates and existing public modeling regressions.");return 0;
static string ScriptPath([CallerFilePath]string path="")=>path;
