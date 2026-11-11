#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
var here=Path.GetFullPath("../../../../temp_docs/operation-shared-a2-review",Path.GetDirectoryName(Here())!);
var original=Path.GetFullPath("../web-progress/validator-tests",here);var root=Path.Combine(here,"public-validator");Directory.CreateDirectory(Path.Combine(root,"runs","test"));
foreach(var path in new[]{"index.json","runs/test/manifest.json","runs/test/mesh.json"})File.Copy(Path.Combine(original,path),Path.Combine(root,path),true);
var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"runs/test/manifest.json")))!;
void Validate(string name,JsonObject node)
{
    var payload=JsonNode.Parse(node.ToJsonString())!;manifest["cases"]![0]!["operations"]=new JsonArray(payload);File.WriteAllText(Path.Combine(root,"runs/test/manifest.json"),manifest.ToJsonString());
    var errors=WebProgressDataValidation.Validate(root)["errors"]!.AsArray();Console.WriteLine($"PUBLIC {name} errors={errors.Count} {errors}");
}
foreach(var name in new[]{"known-error-not-run","not-run-failed-primary"})
{
    var file=Path.GetFullPath("../../docs/reviews/advanced-modeling/operation-shared-12e-evidence/"+name+".json",here);
    Console.WriteLine("ORIGINAL "+name+" SHA="+OperationAcceptanceLedger.HashBytes(File.ReadAllBytes(file)));Validate(name,JsonNode.Parse(File.ReadAllText(file))!.AsObject());
}
OperationRecord Complete(OperationAcceptanceLedger ledger,string id)
{
    var op=ledger.BeginOperation(id,"synthetic",api:"PK_BODY_boolean",options:new JsonObject{["gm"]=new JsonObject{["function"]=1},["pk"]=new JsonObject{["function"]=1}},inputProvenance:new JsonObject{["scope"]="isolated-same-input",["sameInputEvidence"]="synthetic-only"},sameInput:true);op.PrimaryContract="capture-body-result-v1";
    foreach(var suffix in new[]{"body-count","True-body-check","False-body-check","equivalence"}.Concat(Enumerable.Range(0,5).Select(i=>"count-"+i)).Concat(Enumerable.Range(0,15).Select(i=>"mass-"+i)))ledger.RecordCheck(id,"primary",op.Ordinal+"-"+suffix,suffix,"pass");
    foreach(var side in new[]{"gm","pk"}){ledger.RecordReturn(id,side,0);ledger.RecordInputHash(id,side,new string('a',64),[]);ledger.RecordTiming(id,side,side=="gm"?2:1);}ledger.EndOperation(id);return op;
}
var ledger=new OperationAcceptanceLedger("a2","a2");var op=Complete(ledger,"positive");ledger.RecordOutputHashes(op.Id,"gm",[new string('a',64)]);
var inputBorrow=op.InputHashes["gm"]!.AsObject();inputBorrow["target"]=new string('b',64);var outputBorrow=op.OutputHashes["gm"]!["bodies"]!.AsArray();outputBorrow[0]=new string('c',64);
Console.WriteLine($"GETTERS unchangedInput={op.InputHashes["gm"]!["target"]!.GetValue<string>()==new string('a',64)} unchangedOutput={op.OutputHashes["gm"]!["bodies"]![0]!.GetValue<string>()==new string('a',64)} qualified={op.ToJsonObject()["timing"]!["correctnessQualified"]}");
Validate("complete-after-borrowed-mutation",op.ToJsonObject());ledger.RecordCheck(op.Id,"auxiliary","aux","aux","fail");ledger.RecordCheck(op.Id,"representation","rep","rep","fail");ledger.EndScenario(1);
Console.WriteLine($"LAYERS primary={op.PrimaryStatus} aux={op.AuxiliaryStatus} rep={op.RepresentationStatus} scenario={ledger.ScenarioStatus}");Validate("owned-aux-fail-primary-pass",op.ToJsonObject());
var fail=Complete(ledger,"fail");ledger.RecordReturn(fail.Id,"gm",901);ledger.EndOperation(fail.Id,"not-run");ledger.EndScenario(0);Console.WriteLine($"KNOWN_ERROR op={fail.Status} scenario={ledger.ScenarioStatus} first={ledger.FirstFailedOperationId}");
var untouched=new OperationAcceptanceLedger("unreached","unreached");untouched.BeginOperation("unreached","unreached");untouched.EndOperation("unreached","not-run");Console.WriteLine("UNREACHED="+untouched.Operations[0].Status);Validate("genuine-not-run",untouched.Operations[0].ToJsonObject());
static string Here([CallerFilePath]string p="")=>p;
