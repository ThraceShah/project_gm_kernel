#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var script=Path.GetDirectoryName(Source())!;var root=Path.GetFullPath(Path.Combine(script,".."));
var output=Path.GetFullPath(Path.Combine(script,args.Length==1?args[0]:"../temp_docs/unified-delivery/b1-gate-v1"));
if(Directory.Exists(output))throw new IOException("Use a new output directory");
Directory.CreateDirectory(output);
var prior=Path.GetFullPath(Path.Combine(root,"../gm-native-unified-review/temp_docs/unified-review-4b2405e"));
var original=Path.Combine(root,"temp_docs/unified-delivery/current-runner-v2");
var planar=Path.Combine(original,"01-planar-candidate");var originalHash=Hash(Path.Combine(planar,"capture.json"));
var handoff=Path.Combine(root,"temp_docs/unified-delivery/handoff-current-v2.zip");var handoffHash=Hash(handoff);
var results=new JsonArray();var failed=false;
foreach(var name in new[]{"intact","no-modulus","no-body-check","no-compare","no-estimates-lower","no-evidence-links","rejected-compare-evidence"})
    await Audit(name,Path.Combine(prior,"audit-"+name),name=="intact"?0:name=="rejected-compare-evidence"?1:2);
foreach(var name in new[]{"wrong-step","duplicate-component","source-contradiction","report-no-estimate","false-verified-bounds","native-api-error","command-source-contradiction"})
{
    var derived=Path.Combine(output,name);
    foreach(var source in Directory.EnumerateFiles(planar,"*",SearchOption.AllDirectories))
    {
        if(Path.GetExtension(source) is ".dll" or ".pdb")continue;
        var target=Path.Combine(derived,Path.GetRelativePath(planar,source));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);
    }
    var capture=Read(Path.Combine(derived,"capture.json"));var checks=capture["checks"]!.AsArray();
    var mass=checks.First(c=>Text(c,"id").StartsWith("quality-mass-"))!;
    var compare=checks.First(c=>Text(c,"id").StartsWith("quality-compare-"))!;
    if(name=="wrong-step")
    {
        compare["sourceBinding"]!["step"]=99;
        var path=Path.Combine(derived,Text(compare,"evidence"));var report=Read(path);report["sourceBinding"]!["step"]=99;Save(path,report);
    }
    if(name=="duplicate-component")checks.Last(c=>Text(c,"id").StartsWith("quality-mass-"))!["label"]=mass["label"]!.DeepClone();
    if(name=="source-contradiction")mass["sourceBinding"]!["referenceNativeBody"]=-1;
    if(name=="command-source-contradiction")
    {
        var path=Path.Combine(derived,"command.json");var command=Read(path);command["commit"]=new string('0',40);Save(path,command);
    }
    if(name is "report-no-estimate" or "false-verified-bounds" or "native-api-error")
    {
        var path=Path.Combine(derived,Text(mass,"evidence"));var report=Read(path);
        if(name=="report-no-estimate")report["properties"]![0]!.AsObject().Remove("actualEstimate");
        if(name=="native-api-error")report["actualError"]=157;
        if(name=="false-verified-bounds")
        {
            report["properties"]![0]!["normalizedUpper"]=1.0;
            mass["normalizedUpper"]=1.0;
        }
        Save(path,report);
    }
    Save(Path.Combine(derived,"capture.json"),capture);
    Save(Path.Combine(derived,"derived-from.json"),new JsonObject{["sourceCaptureSha256"]=originalHash,["mutation"]=name,["modelingExecuted"]=false});
    await Audit(name,derived,name is "duplicate-component" or "report-no-estimate"?2:1);
}
await Audit("original-planar",planar,0);
foreach(var name in new[]{"00-sphere-candidate","02-curved-candidate","03-solidblend-candidate","04-shortedge-candidate"})
    await Audit(name,Path.Combine(original,name),2);
if(Hash(Path.Combine(planar,"capture.json"))!=originalHash||Hash(handoff)!=handoffHash)throw new InvalidOperationException("Original bytes changed");
Save(Path.Combine(output,"receipt.json"),new JsonObject{["runnerSha256"]=Hash(Path.Combine(script,"NativeModelingWorkComparison.cs")),
    ["checksSha256"]=Hash(Source()),["sourceCaptureSha256"]=originalHash,["unchangedHandoffSha256"]=handoffHash,
    ["modelingExecuted"]=false,["integrationReview"]= "583d917890a9f654dbd4a1f4e96e5e5f261f68eb",["results"]=results,["checkerExit"]=failed?1:0});
Console.WriteLine("Quality gate checks "+(failed?"FAILED":"PASS")+"; child raw exits retained");return failed?1:0;

async Task Audit(string name,string directory,int expected)
{
    string[] argv=["run","scripts/NativeModelingWorkComparison.cs","-c","Release","-p:UseSharedCompilation=false","--","--audit",Path.GetRelativePath(script,directory),"--repeat","1"];
    var info=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var arg in argv)info.ArgumentList.Add(arg);info.Environment["MSBUILDDISABLENODEREUSE"]="1";
    Save(Path.Combine(output,name+".command.json"),new JsonObject{["executable"]="dotnet",["argv"]=new JsonArray(argv.Select(a=>(JsonNode?)JsonValue.Create(a)).ToArray()),
        ["runnerSha256"]=Hash(Path.Combine(script,"NativeModelingWorkComparison.cs")),["inputCaptureSha256"]=Hash(Path.Combine(directory,"capture.json")),["expectedExit"]=expected});
    using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    File.WriteAllText(Path.Combine(output,name+".log"),await stdout+"\nSTDERR\n"+await stderr);
    File.WriteAllText(Path.Combine(output,name+".exit"),process.ExitCode+"\n");
    results.Add((JsonNode)new JsonObject{["name"]=name,["rawExit"]=process.ExitCode,["expectedExit"]=expected});failed|=process.ExitCode!=expected;
    Console.WriteLine($"{name}: rawExit={process.ExitCode} expected={expected}");
}
static string Text(JsonNode? node,string field)=>node?[field]?.GetValue<string>()??"";
static JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(path))!;
static void Save(string path,JsonNode node)=>File.WriteAllText(path,node.ToJsonString());
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Source([CallerFilePath]string path="")=>path;
