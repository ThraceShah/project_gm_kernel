#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
var scripts=Path.GetDirectoryName(ScriptPath())!;
var data=Path.GetFullPath(Option("--data","../temp_docs/web-progress-publish/data"),scripts);
var web=Path.GetFullPath(Option("--web","../web"),scripts);
var state=Path.GetFullPath(Option("--state","../temp_docs/web-progress-publish/site-state.json"),scripts);
var validation=WebProgressDataValidation.Validate(data);if(validation["errors"]!.AsArray().Count>0)throw new InvalidDataException(validation.ToJsonString());
var index=JsonNode.Parse(File.ReadAllText(Path.Combine(data,"index.json")))!;
var latest=index["latestRunId"]!.GetValue<string>();
var root=Path.GetFullPath("..",scripts);var commit=Run("git",["rev-parse","HEAD"],root,true);
var fingerprint=commit+":"+latest;var dist=Path.Combine(web,"dist");
if(File.Exists(state)&&File.Exists(Path.Combine(dist,"data","index.json"))&&JsonNode.Parse(File.ReadAllText(state))?["fingerprint"]?.GetValue<string>()==fingerprint)
{Console.WriteLine("SITE_UNCHANGED "+latest);return 0;}
if(!args.Contains("--no-build"))
{
    if(!File.Exists(Path.Combine(web,"package.json")))throw new FileNotFoundException("Merged frontend package.json is required.");
    var builder=Option("--builder","pnpm");
    if(builder is not ("pnpm" or "npm"))throw new ArgumentException("builder must be pnpm or npm");
    Run(builder,builder=="pnpm"?["install","--frozen-lockfile"]:["ci"],web,false);
    Run(builder,["run","build"],web,false);
}
Directory.CreateDirectory(dist);
foreach(var run in index["runs"]!.AsArray().Reverse())WebProgressPublishing.Publish(data,run!["id"]!.GetValue<string>(),Path.Combine(dist,"data"));
var result=WebProgressDataValidation.Validate(Path.Combine(dist,"data"));if(result["errors"]!.AsArray().Count>0)throw new InvalidDataException(result.ToJsonString());
Directory.CreateDirectory(Path.GetDirectoryName(state)!);File.WriteAllText(state+".tmp",new JsonObject{["fingerprint"]=fingerprint,["commit"]=commit,["runId"]=latest,["generatedAt"]=DateTimeOffset.UtcNow.ToString("O")}.ToJsonString());File.Move(state+".tmp",state,true);
Console.WriteLine("SITE_READY "+Path.GetRelativePath(root,dist)+" run="+latest+" commit="+commit);return 0;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Run(string command,string[] args,string cwd,bool capture)
{
    var info=new ProcessStartInfo(command){WorkingDirectory=cwd,UseShellExecute=false,RedirectStandardOutput=capture};foreach(var argument in args)info.ArgumentList.Add(argument);
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";using var process=Process.Start(info)!;var output=capture?process.StandardOutput.ReadToEnd():"";process.WaitForExit();if(process.ExitCode!=0)throw new IOException(command+" exited "+process.ExitCode);return output.Trim();
}
