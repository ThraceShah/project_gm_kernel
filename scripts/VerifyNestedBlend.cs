#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.Runtime.CompilerServices;

var scriptDirectory=Path.GetDirectoryName(ScriptPath())!;var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
var output=Path.Combine(root,"temp_docs","nested-blend-verification");Directory.CreateDirectory(output);
foreach(var name in new[]{"NestedBlendOracle","NestedBlendBenchmark"})
{
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var argument in new[]{"run","--no-cache",$"scripts/{name}.cs","-c","Release","-p:UseSharedCompilation=false","-p:BuildInParallel=false"})start.ArgumentList.Add(argument);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";start.Environment["DOTNET_TieredCompilation"]="0";
    Console.WriteLine("> "+name);using var process=Process.Start(start)??throw new Exception("Cannot start "+name);
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    File.WriteAllText(Path.Combine(output,name+".log"),await stdout+await stderr);Console.WriteLine($"{(process.ExitCode==0?"PASS":"FAIL")} {name}, exit={process.ExitCode}");
    if(process.ExitCode!=0)return process.ExitCode;
}
Console.WriteLine("PASS nested Blend correctness and paired 70% performance gates.");return 0;
static string ScriptPath([CallerFilePath]string path="")=>path;
