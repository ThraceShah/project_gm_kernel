#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Run the unchanged strict construction/comparison gates in both modes. Each
// worker repeats fresh inputs in one process; the second API call is JIT/pool
// warm, while command-owned proof caches are empty at every API entry.
var root=Path.GetDirectoryName(ScriptPath())!;var logDirectory=Path.Combine(root,"..","temp_docs","modeling-performance");Directory.CreateDirectory(logDirectory);
var cases=args.Length==0?new[]{"blend-subtract"}:args;
foreach(var cached in new[]{false,true})
{
    var mode=cached?"cached":"uncached";var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
    foreach(var argument in new[]{"run","--no-cache",Path.Combine(root,"MixedCurvedBooleanOracle.cs"),"-c","Release","-p:BuildProjectReferences=false","-p:ProduceReferenceAssembly=false","-p:UseSharedCompilation=false","-p:BuildInParallel=false","--","--profile","--repeat","2","--seconds","1800"})info.ArgumentList.Add(argument);
    if(!cached)info.ArgumentList.Add("--no-bound-cache");foreach(var name in cases)info.ArgumentList.Add(name);info.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var writer=new StreamWriter(Path.Combine(logDirectory,mode+".log"),false){AutoFlush=true};var gate=new object();
    using var process=new Process{StartInfo=info};process.OutputDataReceived+=(_,e)=>{if(e.Data!=null)lock(gate){writer.WriteLine(e.Data);Console.WriteLine(mode+": "+e.Data);}};
    process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)lock(gate)writer.WriteLine("STDERR "+e.Data);};
    if(!process.Start())throw new Exception("Could not start strict modeling worker");process.BeginOutputReadLine();process.BeginErrorReadLine();await process.WaitForExitAsync();process.WaitForExit();
    if(process.ExitCode!=0)throw new Exception($"Strict {mode} oracle failed: {process.ExitCode}");
}
Console.WriteLine("PASS strict cold/warm modeling in cached and uncached command modes; compare full API and PROFILE records under temp_docs/mixed-curved-boolean-oracle/profile-*");
static string ScriptPath([CallerFilePath]string path="")=>path;
