#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);var output=Path.Combine(root,"temp_docs/operation-2c06-review/disk");Directory.CreateDirectory(output);
var summary=new JsonArray();var failures=0;
async Task Run(string label,string data,int expected)
{
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
    foreach(var arg in new[]{"run","--file","scripts/ValidateWebProgress.cs","-c","Release","-p:UseSharedCompilation=false","--",Path.GetRelativePath(Path.Combine(root,"scripts"),data)})start.ArgumentList.Add(arg);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    File.WriteAllText(Path.Combine(output,label+".log"),(await stdout)+(await stderr));File.WriteAllText(Path.Combine(output,label+".exit"),process.ExitCode.ToString());
    var good=process.ExitCode==expected;if(!good)failures++;Console.WriteLine((good?"PASS ":"FAIL ")+label+" exit="+process.ExitCode+" expected="+expected);
    summary.Add((JsonNode)new JsonObject{["label"]=label,["exit"]=process.ExitCode,["expected"]=expected});
}
void Copy(string from,string to)
{
    Directory.CreateDirectory(to);foreach(var file in Directory.GetFiles(from,"*",SearchOption.AllDirectories)){var target=Path.Combine(to,Path.GetRelativePath(from,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target,false);}
}
foreach(var source in new[]{("old-full","../gm-scheduler-review/temp_docs/operation-followup-a0-review/full-data",0),("short-final","../gm-operation-recording-r2/temp_docs/operation-short-case-final/data",0),("old-observer-failure","../gm-operation-recording-r2/temp_docs/operation-short-capture-final/data",0)})
{
    var data=Path.Combine(output,source.Item1);Copy(Path.GetFullPath(source.Item2,root),data);await Run(source.Item1,data,source.Item3);
}
// Frozen, independent earlier review fixture: synthetic complete v1 body-result
// facts. No generated model, real input-identity or measured timing is asserted.
var templateRoot=Path.GetFullPath("../gm-scheduler-review/temp_docs/operation-followup-a0-review/version-controls/missing-legacy",root);
var template=JsonNode.Parse(File.ReadAllBytes(Path.Combine(templateRoot,"runs/test/manifest.json")))!;
var hollow=JsonNode.Parse(File.ReadAllBytes(Path.GetFullPath("../gm-operation-recording-r2/temp_docs/operation-leaf-r2/local-final-rejection/ledger.json",root)))!["operations"]![0]!["api"]!.GetValue<string>();
var controls=new (string Label,int Exit,Action<JsonObject> Change)[]
{
    ("legacy-missing",0,o=>{}),
    ("v2-complete",0,o=>o["interpretationVersion"]="operation-rules-v2"),
    ("unknown-forged-pass",1,o=>o["interpretationVersion"]="operation-rules-v99"),
    ("numeric-forged-pass",1,o=>o["interpretationVersion"]=99),
    ("v3-forged-body-pass",1,o=>o["interpretationVersion"]="operation-rules-v3"),
    ("arbitrary-expected-forged-pass",1,o=>{o["interpretationVersion"]="operation-rules-v3";o["api"]=hollow;o["primaryContract"]="expected-rejection-rollback-v1";o["acceptanceKind"]="expected-rejection";o["returns"]=new JsonObject{["gm"]=901,["pk"]=901};o["expectedReturns"]=new JsonObject{["gm"]=901,["pk"]=901};}),
    ("arbitrary-expected-conservative",0,o=>{o["interpretationVersion"]="operation-rules-v3";o["api"]=hollow;o["primaryContract"]="expected-rejection-rollback-v1";o["acceptanceKind"]="expected-rejection";o["returns"]=new JsonObject{["gm"]=901,["pk"]=901};o["expectedReturns"]=new JsonObject{["gm"]=901,["pk"]=901};Unverified(o);}),
    ("v2-does-not-opt-in-rejection",1,o=>{o["interpretationVersion"]="operation-rules-v2";o["api"]=hollow;o["primaryContract"]="expected-rejection-rollback-v1";o["acceptanceKind"]="expected-rejection";o["returns"]=new JsonObject{["gm"]=13,["pk"]=13};o["expectedReturns"]=new JsonObject{["gm"]=13,["pk"]=13};Unverified(o);}),
    ("schema-cannot-upgrade-v1-boolean2",1,o=>o["api"]=System.Text.RegularExpressions.Regex.Match(File.ReadAllText(Path.Combine(root,"scripts/OperationAcceptanceRules.cs")),"interpretation == \"operation-rules-v2\" && api == \"([^\"]+)\"").Groups[1].Value),
    ("nonfinite-numeric-string-forged-ratio",1,o=>o["timing"]!["gmMs"]="NaN"),
    ("missing-structural-primary-forged-pass",1,o=>o["primaryChecks"]!.AsArray().RemoveAt(2)),
    ("primary-owner-mismatch",1,o=>o["primaryChecks"]![0]!["owner"]="other-rep")
};
void Unverified(JsonObject op){op["status"]="unverified";op["primaryStatus"]="unverified";op["timing"]!["correctnessQualified"]=false;op["timing"]!["ratio"]=null;}
foreach(var control in controls)
{
    var data=Path.Combine(output,control.Label);Copy(templateRoot,data);var manifest=template.DeepClone();var op=manifest["cases"]![0]!["operations"]![0]!.AsObject();op["timing"]!.AsObject().Remove("unqualifiedReasons");
    control.Change(op);File.WriteAllText(Path.Combine(data,"runs/test/manifest.json"),manifest.ToJsonString());await Run(control.Label,data,control.Exit);
}
// Real v3 rejection facts are also fed through the public file entry point.
var rejectionData=Path.Combine(output,"real-rejection");Copy(templateRoot,rejectionData);var rejectionManifest=template.DeepClone();
rejectionManifest["cases"]![0]!["operations"]=JsonNode.Parse(File.ReadAllBytes(Path.GetFullPath("../gm-operation-recording-r2/temp_docs/operation-leaf-r2/local-final-rejection/ledger.json",root)))!["operations"]!.DeepClone();
File.WriteAllText(Path.Combine(rejectionData,"runs/test/manifest.json"),rejectionManifest.ToJsonString());await Run("real-rejection",rejectionData,0);
File.WriteAllText(Path.Combine(output,"summary.json"),summary.ToJsonString());
Console.WriteLine("CONTROLS "+summary.Count+" failures="+failures);return failures==0?0:1;
static string Here([CallerFilePath]string path="")=>path;
