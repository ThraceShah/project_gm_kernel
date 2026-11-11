#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
var here=Path.GetFullPath("../../../../temp_docs/operation-shared-12e-review",Path.GetDirectoryName(Here())!);
OperationRecord Complete(OperationAcceptanceLedger ledger,string id)
{
    var op=ledger.BeginOperation(id,"synthetic body",api:"PK_BODY_boolean",options:new JsonObject{["gm"]=new JsonObject{["function"]=1},["pk"]=new JsonObject{["function"]=1}},inputProvenance:new JsonObject{["scope"]="isolated-same-input",["sameInputEvidence"]="synthetic-only"},sameInput:true);
    op.PrimaryContract="capture-body-result-v1";
    var required=new[]{"body-count","True-body-check","False-body-check","equivalence"}.Concat(Enumerable.Range(0,5).Select(i=>"count-"+i)).Concat(Enumerable.Range(0,15).Select(i=>"mass-"+i));
    foreach(var suffix in required)ledger.RecordCheck(id,"primary",op.Ordinal+"-"+suffix,suffix,"pass");
    foreach(var side in new[]{"gm","pk"}){ledger.RecordReturn(id,side,0);ledger.RecordInputHash(id,side,new string('a',64),[]);ledger.RecordTiming(id,side,side=="gm"?2:1);}
    ledger.EndOperation(id);return op;
}
var ledger=new OperationAcceptanceLedger("isolated-tail","independent tail probe");var op=Complete(ledger,"ok");
Console.WriteLine("COMPLETE qualified="+op.Timing["correctnessQualified"]);
ledger.RecordCheck(op.Id,"auxiliary","aux","classification","fail");ledger.RecordCheck(op.Id,"representation","rep","lifetime","fail");ledger.EndScenario(1);
Console.WriteLine($"LAYERS primary={op.PrimaryStatus} aux={op.AuxiliaryStatus} rep={op.RepresentationStatus} scenario={ledger.ScenarioStatus} qualified={op.Timing["correctnessQualified"]}");
foreach(var value in new[]{0d,-1,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
{
    var timed=Complete(ledger,"time-"+value);timed.GmMs=value;ledger.EndScenario(1);var text=ledger.ToJsonObject().ToJsonString();Console.WriteLine($"TIME {value} qualified={timed.Timing["correctnessQualified"]} jsonLength={text.Length} earlier={op.PrimaryStatus}");
}
var options=new JsonObject{["nested"]=new JsonObject{["v"]=1}};var provenance=new JsonObject{["nested"]=new JsonObject{["v"]=1}};
var freeze=ledger.BeginOperation("freeze","freeze",options:options,inputProvenance:provenance);options["nested"]!["v"]=2;provenance["nested"]!["v"]=2;freeze.Options!["nested"]!["v"]=3;freeze.InputProvenance!["nested"]!["v"]=3;
Console.WriteLine($"FROZEN options={freeze.Options} provenance={freeze.InputProvenance} hashMatches={freeze.ParameterHash==OperationAcceptanceLedger.HashString(freeze.Options!.ToJsonString())}");
var borrowed=op.InputHashes["gm"]!.AsObject();borrowed["target"]=new string('b',64);op.OutputHashes["gm"]=new JsonObject{["bodies"]=new JsonArray(new string('a',64))};var outputBorrow=op.OutputHashes["gm"]!["bodies"]!.AsArray();outputBorrow[0]=new string('c',64);
Console.WriteLine("BORROWED_INPUT actual="+op.ToJsonObject()["inputHashes"]!["gm"]!["target"]+" output="+op.ToJsonObject()["outputHashes"]!["gm"]!["bodies"]![0]);
var exported=op.ToJsonObject();exported["inputHashes"]!["gm"]!["target"]="export mutation";Console.WriteLine("EXPORTED_COPY_INDEPENDENT="+(op.ToJsonObject()["inputHashes"]!["gm"]!["target"]!.GetValue<string>()==new string('b',64)));
var sticky=new OperationAcceptanceLedger("sticky","sticky");var failed=Complete(sticky,"fail");sticky.EndOperation(failed.Id,"fail");sticky.EndOperation(failed.Id,"pass");sticky.EndScenario(0);Console.WriteLine($"STICKY_FAIL op={failed.Status} scenario={sticky.ScenarioStatus}");
var timeout=Complete(sticky,"timeout");sticky.EndOperation(timeout.Id,"timeout");sticky.EndOperation(timeout.Id,"not-run");sticky.EndScenario(0);Console.WriteLine($"STICKY_TIMEOUT op={timeout.Status} scenario={sticky.ScenarioStatus}");
var contradicted=new OperationAcceptanceLedger("contradiction","contradiction");var error=Complete(contradicted,"error");contradicted.RecordReturn(error.Id,"gm",901);contradicted.EndOperation(error.Id,"not-run");contradicted.EndScenario(0);
Console.WriteLine($"NOT_RUN_WITH_ERROR op={error.Status} primary={error.PrimaryStatus} scenario={contradicted.ScenarioStatus} firstFailed={contradicted.FirstFailedOperationId??"null"} returns={error.Returns}");
var original=Path.GetFullPath("../web-progress/validator-tests",here);var root=Path.Combine(here,"public-validator");Directory.CreateDirectory(Path.Combine(root,"runs","test"));Directory.CreateDirectory(Path.Combine(here,"inputs"));
foreach(var file in new[]{"index.json","runs/test/manifest.json","runs/test/mesh.json"})File.Copy(Path.Combine(original,file),Path.Combine(root,file),true);
var baseManifest=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"runs/test/manifest.json")))!;
void Validate(string name,JsonObject node)
{
    var payload=JsonNode.Parse(node.ToJsonString())!;File.WriteAllText(Path.Combine(here,"inputs",name+".json"),payload.ToJsonString());var manifest=baseManifest.DeepClone();manifest["cases"]![0]!["operations"]=new JsonArray(payload);File.WriteAllText(Path.Combine(root,"runs/test/manifest.json"),manifest.ToJsonString());
    try{var result=WebProgressDataValidation.Validate(root);Console.WriteLine($"PUBLIC {name} errors={result["errors"]!.AsArray().Count} {result["errors"]}");}catch(Exception ex){Console.WriteLine($"PUBLIC {name} THROWS={ex.GetType().Name}");}
}
var positive=Complete(new OperationAcceptanceLedger("positive","positive"),"positive").ToJsonObject();
void Mutate(string name,Action<JsonObject> mutate){var node=(JsonObject)positive.DeepClone();mutate(node);Validate(name,node);}
Validate("complete",positive);
Validate("known-error-not-run",error.ToJsonObject());
Validate("mutated-input-getter",op.ToJsonObject());
Mutate("required-diagnostic",n=>n["primaryChecks"]![0]!["status"]="diagnostic");
Mutate("missing-required",n=>n["primaryChecks"]!.AsArray().RemoveAt(0));
Mutate("wrong-owner",n=>n["primaryChecks"]![0]!["owner"]="other");
Mutate("return-901",n=>n["returns"]!["gm"]=901);
Mutate("forged-hash",n=>n["inputHashes"]!["gm"]!["target"]="tag27");
Mutate("unknown-same-input",n=>n["inputProvenance"]=null);
Mutate("unknown-contract",n=>n["primaryContract"]="unknown");
Mutate("unknown-api",n=>n["api"]="unknown");
Mutate("wrong-ratio",n=>n["timing"]!["ratio"]=3);
Mutate("inconsistent-status",n=>n["status"]="fail");
Mutate("bad-nested-returns",n=>n["returns"]=new JsonArray(0,0));
Mutate("not-run-failed-primary",n=>{n["terminalStatus"]="not-run";n["status"]="not-run";n["primaryStatus"]="not-run";n["primaryChecks"]![0]!["status"]="fail";n["timing"]!["correctnessQualified"]=false;n["timing"]!["ratio"]=null;n["timing"]!.AsObject().Remove("unqualifiedReasons");});
static string Here([CallerFilePath]string p="")=>p;
