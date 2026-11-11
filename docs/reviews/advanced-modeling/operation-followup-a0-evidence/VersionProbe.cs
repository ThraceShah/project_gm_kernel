#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Synthetic recorded-fact fixtures only. Invokes the real public disk validator;
// no oracle, geometry, native binary, or speed qualification is executed.
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var output=Path.Combine(root,"temp_docs/operation-followup-a0-review/version-controls");
Directory.CreateDirectory(output);
var fixtures=new (string Label,string Api,JsonNode? Version,bool Unverified,bool Qualified,int Expected)[]
{
    ("v2-complete","PK_BODY_boolean_2",JsonValue.Create("operation-rules-v2"),false,true,0),
    ("missing-legacy","PK_BODY_boolean",null,false,true,0),
    ("missing-boolean2-conservative","PK_BODY_boolean_2",null,true,false,0),
    ("missing-boolean2-forged-pass","PK_BODY_boolean_2",null,false,true,1),
    ("unknown-conservative","PK_BODY_boolean",JsonValue.Create("operation-rules-v99"),true,false,0),
    ("unknown-forged-pass","PK_BODY_boolean",JsonValue.Create("operation-rules-v99"),false,true,1),
    ("wrong-explicit-status","PK_BODY_boolean_2",JsonValue.Create("operation-rules-v2"),true,false,1),
    ("numeric-version-forged-pass","PK_BODY_boolean",JsonValue.Create(99),false,true,1)
};
var summary=new JsonArray();var failed=false;
foreach(var f in fixtures)
{
    var data=Path.Combine(output,f.Label);var run=Path.Combine(data,"runs/test");Directory.CreateDirectory(run);
    var options=new JsonObject{["gm"]=new JsonObject{["function"]=1},["pk"]=new JsonObject{["function"]=1}};
    var checks=new JsonArray();var suffixes=new List<string>{"body-count","True-body-check","False-body-check","equivalence"};
    for(var i=0;i<5;i++)suffixes.Add("count-"+i);for(var i=0;i<15;i++)suffixes.Add("mass-"+i);
    foreach(var suffix in suffixes)checks.Add((JsonNode)new JsonObject{["id"]="0-"+suffix,["owner"]="op-1",["category"]="primary",["label"]=suffix,["status"]="pass"});
    var reasons=f.Unverified?new JsonArray("undeclared-or-unsupported-primary-contract","missing-required-primary","primary-not-pass"):new JsonArray();
    var op=new JsonObject{["operationId"]="op-1",["ordinal"]=0,["name"]="recording-only fixture",["role"]="primary",["api"]=f.Api,["primaryContract"]="capture-body-result-v1",["status"]=f.Unverified?"unverified":"pass",["primaryStatus"]=f.Unverified?"unverified":"pass",["sameInput"]=true,["options"]=options,["parameterHash"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(options.ToJsonString()))),["inputProvenance"]=new JsonObject{["scope"]="isolated-same-input",["sameInputEvidence"]="synthetic JSON control, never a real geometry/timing result"},["inputHashes"]=new JsonObject(),["returns"]=new JsonObject{["gm"]=0,["pk"]=0},["primaryChecks"]=checks,["auxiliaryChecks"]=new JsonArray(),["representationContracts"]=new JsonArray(),["timing"]=new JsonObject{["boundary"]="api-only",["measurementKind"]="api-elapsed-wall",["gmMs"]=2,["pkMs"]=1,["coldKind"]="fixture",["warmup"]=0,["samples"]=1,["correctnessQualified"]=f.Qualified,["ratio"]=f.Qualified?JsonValue.Create(2):null,["unqualifiedReasons"]=reasons}};
    foreach(var side in new[]{"gm","pk"})op["inputHashes"]![side]=new JsonObject{["target"]=new string('a',64),["tools"]=new JsonArray()};
    if(f.Version!=null)op["interpretationVersion"]=f.Version.DeepClone();
    var c=new JsonObject{["id"]="synthetic",["title"]="Synthetic schema recording only",["family"]="boolean",["operation"]="test",["status"]="pass",["operationSchemaVersion"]="operation-layered-v2",["checkVersion"]="not-an-interpretation-signature",["parameters"]=new JsonObject(),["apis"]=new JsonObject(),["steps"]=new JsonArray(),["operations"]=new JsonArray((JsonNode)op),["checks"]=new JsonArray((JsonNode)new JsonObject{["id"]="fixture",["label"]="fixture only",["status"]="pass",["gm"]=0,["pk"]=0,["absDelta"]=0,["normalizedDelta"]=0,["threshold"]=0,["unit"]="",["evidence"]=null,["note"]="synthetic"}),["performance"]=new JsonObject{["kind"]="single-operation",["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=1,["warmup"]=0,["scope"]="synthetic schema control only",["correctnessQualified"]=false,["host"]=new JsonObject(),["configuration"]=new JsonObject(),["note"]="not a measurement"},["evidence"]=new JsonArray(),["notes"]=new JsonArray(),["source"]=new JsonObject{["path"]="fixture",["revision"]="fixture"}};
    foreach(var side in new[]{"gm","pk"})c["apis"]![side]=new JsonObject{["name"]=f.Api,["parameters"]=new JsonObject()};
    var manifest=new JsonObject{["schemaVersion"]=1,["operationSchemaVersion"]="operation-layered-v2",["run"]=new JsonObject{["id"]="test",["commit"]="fixture",["generatedAt"]="fixture",["pkVersion"]=null,["host"]=new JsonObject(),["configuration"]=new JsonObject(),["scope"]="fixture",["provenance"]=new JsonObject()},["cases"]=new JsonArray((JsonNode)c)};
    File.WriteAllText(Path.Combine(run,"manifest.json"),manifest.ToJsonString());
    File.WriteAllText(Path.Combine(data,"index.json"),new JsonObject{["schemaVersion"]=1,["generatedAt"]="fixture",["latestRunId"]="test",["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]="test",["commit"]="fixture",["generatedAt"]="fixture",["label"]="fixture",["manifest"]="runs/test/manifest.json"})}.ToJsonString());
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var arg in new[]{"run","--file","scripts/ValidateWebProgress.cs","-c","Release","-p:UseSharedCompilation=false","--",Path.GetRelativePath(Path.Combine(root,"scripts"),data)})start.ArgumentList.Add(arg);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();process.WaitForExit();
    var log=await stdout+await stderr;File.WriteAllText(Path.Combine(output,f.Label+".log"),log);File.WriteAllText(Path.Combine(output,f.Label+".exit"),process.ExitCode.ToString());
    var ok=process.ExitCode==f.Expected;failed|=!ok;Console.WriteLine($"{(ok?"PASS":"FAIL")} {f.Label} exit={process.ExitCode} expected={f.Expected}");
    summary.Add((JsonNode)new JsonObject{["label"]=f.Label,["exit"]=process.ExitCode,["expected"]=f.Expected,["matched"]=ok,["manifestSha256"]=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(run,"manifest.json"))))});
}
File.WriteAllText(Path.Combine(output,"summary.json"),summary.ToJsonString());return failed?1:0;
static string Here([CallerFilePath]string path="")=>path;
