#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// An ordinary local runner, not an agent. No service is started by installing it.
var scripts=Path.GetDirectoryName(ScriptPath())!;var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
var interval=int.Parse(Option("--poll-seconds","60"));if(interval<5)throw new ArgumentException("poll-seconds >= 5 required");
var statePath=Path.GetFullPath(Option("--state","../temp_docs/web-progress/watch-state.json"),scripts);Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
var once=args.Contains("--once");var last=File.Exists(statePath)?JsonNode.Parse(File.ReadAllText(statePath))?["fingerprint"]?.GetValue<string>():null;
using var stop=new CancellationTokenSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;stop.Cancel();};
do
{
    var source=Run("git",["rev-parse","HEAD"])+Run("git",["diff","--no-ext-diff","--","src","scripts","tests/KernelTests/Fixtures"])+File.ReadAllText(Path.GetFullPath(Option("--registry","web-progress-registry.json"),scripts));
    var fingerprint=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    if(last!=fingerprint||args.Contains("--force")||!CacheAvailable())
    {
        var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,WorkingDirectory=root};
        foreach(var arg in new[]{"run","--file","scripts/RefreshWebProgress.cs","--"})info.ArgumentList.Add(arg);
        foreach(var option in new[]{"--registry","--family","--suite","--case","--seconds","--case-seconds","--samples","--configuration","--output","--publish-data-to"})
        {var at=Array.IndexOf(args,option);if(at>=0){info.ArgumentList.Add(option);info.ArgumentList.Add(args[at+1]);}}
        info.Environment["MSBUILDDISABLENODEREUSE"]="1";
        using var process=Process.Start(info)!;
        try{await process.WaitForExitAsync(stop.Token);}catch(OperationCanceledException){process.Kill(true);await process.WaitForExitAsync();return 130;}
        if(process.ExitCode!=0){Console.Error.WriteLine("Exporter failed, state was not advanced: "+process.ExitCode);if(once)return process.ExitCode;}
        else{last=fingerprint;File.WriteAllText(statePath+".tmp",new JsonObject{["fingerprint"]=fingerprint,["updatedAt"]=DateTimeOffset.UtcNow.ToString("O"),["commit"]=Run("git",["rev-parse","HEAD"])}.ToJsonString());File.Move(statePath+".tmp",statePath,true);}
    }
    else Console.WriteLine("Source and registry unchanged; retained current static data.");
    if(!once)try{await Task.Delay(TimeSpan.FromSeconds(interval),stop.Token);}catch(OperationCanceledException){break;}
}while(!once&&!stop.IsCancellationRequested);
return 0;
bool CacheAvailable()
{
    try
    {
        var cache=Path.GetFullPath(Option("--publish-data-to",Option("--output","../temp_docs/web-progress/data")),scripts);
        var indexPath=Path.Combine(cache,"index.json");if(!File.Exists(indexPath))return false;
        var index=JsonNode.Parse(File.ReadAllText(indexPath))!;var latest=index["latestRunId"]!.GetValue<string>();
        var descriptor=index["runs"]!.AsArray().Single(r=>r!["id"]!.GetValue<string>()==latest);
        return File.Exists(Path.GetFullPath(descriptor!["manifest"]!.GetValue<string>(),cache));
    }
    catch(IOException){return false;}
    catch(System.Text.Json.JsonException){return false;}
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Run(string command,string[] args){var info=new ProcessStartInfo(command){RedirectStandardOutput=true,UseShellExecute=false};foreach(var arg in args)info.ArgumentList.Add(arg);using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new IOException(command+" failed");return output.Trim();}
