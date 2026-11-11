#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

void Assert(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    Console.WriteLine("PASS " + label);
}
OperationRecord Complete(OperationAcceptanceLedger ledger, string id,string api="PK_BODY_boolean")
{
    var options = new JsonObject { ["gm"] = new JsonObject { ["function"] = 1 }, ["pk"] = new JsonObject { ["function"] = 1 } };
    var op = ledger.BeginOperation(id, "fixture Boolean", api: api, options: options,
        inputProvenance: new JsonObject { ["scope"] = "isolated-same-input", ["sameInputEvidence"] = "fixture-only" }, sameInput: true);
    op.PrimaryContract = "capture-body-result-v1";
    var requirements = new List<string> { "body-count", "True-body-check", "False-body-check", "equivalence" };
    for (var i = 0; i < 5; i++) requirements.Add("count-" + i);
    for (var i = 0; i < 15; i++) requirements.Add("mass-" + i);
    foreach (var requirement in requirements) ledger.RecordCheck(id, "primary", op.Ordinal + "-" + requirement, requirement, "pass");
    foreach (var side in new[] { "gm", "pk" })
    {
        ledger.RecordReturn(id, side, 0);
        ledger.RecordInputHash(id, side, new string('a', 64), []);
        ledger.RecordTiming(id, side, side == "gm" ? 2 : 1);
    }
    return op;
}
var ledger = new OperationAcceptanceLedger("self-test", "recording only");
var op = Complete(ledger, "body");
Assert(op.ToJsonObject()["timing"]!["correctnessQualified"]!.GetValue<bool>(), "complete recorded contract qualifies");
var versioned=Complete(ledger,"boolean-v2","PK_BODY_boolean_2");
Assert(versioned.ToJsonObject()["interpretationVersion"]!.GetValue<string>()=="operation-rules-v2"&&versioned.PrimaryStatus=="pass","Boolean v2 support requires the recorded v2 interpretation");
ledger.RecordCheck(op.Id, "auxiliary", "classification", "classification", "fail");
ledger.RecordCheck(op.Id, "representation", "lifetime", "lifetime", "fail");
Assert(op.ToJsonObject()["status"]!.GetValue<string>() == "pass" && op.HasAuxiliaryFailure, "primary survives owned auxiliary and representation failures");
ledger.EndOperation(op.Id, "fail");
ledger.EndScenario(1);
Assert(op.ToJsonObject()["status"]!.GetValue<string>() == "fail" && !op.Timing["correctnessQualified"]!.GetValue<bool>(), "explicit failure survives serialization and scenario recomputation");
Assert(ledger.FirstFailedOperationId == op.Id, "first failed operation retained");
try { ledger.BeginOperation(op.Id, "other call"); throw new Exception("duplicate accepted"); }
catch (InvalidOperationException e) { Assert(e.Message.Contains("Duplicate"), "duplicate is an explicit contract error"); }
var options = new JsonObject { ["nested"] = new JsonObject { ["value"] = 1 } };
var provenance = new JsonObject { ["value"] = 1 };
var frozen = ledger.BeginOperation("frozen", "snapshot", options: options, inputProvenance: provenance);
options["nested"]!["value"] = 2; provenance["value"] = 2; frozen.Options!["nested"]!["value"] = 3;
Assert(frozen.Options!["nested"]!["value"]!.GetValue<int>() == 1 && frozen.InputProvenance!["value"]!.GetValue<int>() == 1 && frozen.ParameterHash == OperationAcceptanceLedger.HashString(frozen.Options.ToJsonString()), "deep snapshots retain recomputable parameter hash");
Assert(frozen.Returns["gm"] == null && frozen.Returns["pk"] == null, "unrecorded returns remain unknown");
var borrowed = Complete(ledger,"borrowed");
ledger.RecordOutputHashes(borrowed.Id,"gm",[new string('c',64)]);
borrowed.InputHashes["gm"]!["target"]=new string('b',64);
borrowed.OutputHashes["gm"]!["bodies"]![0]=new string('d',64);
Assert(borrowed.InputHashes["gm"]!["target"]!.GetValue<string>()==new string('a',64)&&borrowed.OutputHashes["gm"]!["bodies"]![0]!.GetValue<string>()==new string('c',64),"input/output getter mutation cannot change recorded hashes");
var rejectedNotRun=Complete(ledger,"executed-not-run");ledger.RecordReturn(rejectedNotRun.Id,"gm",901);ledger.EndOperation(rejectedNotRun.Id,"not-run");ledger.EndScenario(0);
Assert(rejectedNotRun.Status=="fail"&&ledger.ScenarioStatus=="fail"&&ledger.FirstFailedOperationId!=null,"not-run cannot hide a known API failure");
var failedNotRun=Complete(ledger,"primary-not-run");ledger.RecordCheck(failedNotRun.Id,"primary","failure","failure","fail");ledger.EndOperation(failedNotRun.Id,"not-run");
Assert(failedNotRun.ToJsonObject()["status"]!.GetValue<string>()=="fail","not-run cannot hide an explicit primary failure");
var stickyApi=Complete(ledger,"sticky-api");ledger.RecordReturn(stickyApi.Id,"gm",901);ledger.RecordReturn(stickyApi.Id,"gm",0);
Assert(stickyApi.ToJsonObject()["status"]!.GetValue<string>()=="fail"&&stickyApi.PrimaryChecks.Any(c=>c?["gm"]?.GetValue<double>()==901),"later return recording cannot erase an unexpected API failure");
var scoped=new OperationAcceptanceLedger("scoped","scoped");var previous=Complete(scoped,"previous");
scoped.BeginOperation("lifecycle","lifecycle",api:"integration-lifecycle");
try
{
    using var step=new OperationStepScope(scoped,"lifecycle","delete","PK_ENTITY_delete");
    step.Return("gm",0);throw new InvalidOperationException("injected child failure");
}
catch(InvalidOperationException e) when(e.Message=="injected child failure"){scoped.EndOperation("lifecycle","fail");}
scoped.InProcessExitCode=0;scoped.EndScenario(null);
var failedChild=scoped.GetOperation("lifecycle/delete")!;
Assert(previous.PrimaryStatus=="pass"&&failedChild.Status=="fail"&&failedChild.Returns["pk"]==null&&scoped.FirstFailedOperationId==failedChild.Id,"scope failure keeps prior primary and exact child owner without fabricating missing side");
Assert(scoped.LegacyExitCode==null&&scoped.InProcessExitCode==0&&scoped.ScenarioStatus=="fail","in-process zero is not an observed process exit");
var expected=scoped.BeginOperation("expected","expected",api:"PK_BODY_hollow_2",interpretationVersion:"operation-rules-v3");
expected.AcceptanceKind="expected-rejection";expected.PrimaryContract="expected-rejection-rollback-v1";expected.ExpectedReturns["gm"]=901;expected.ExpectedReturns["pk"]=901;
scoped.RecordReturn(expected.Id,"gm",901);scoped.RecordReturn(expected.Id,"pk",901);
Assert(expected.ToJsonObject()["status"]!.GetValue<string>()=="unverified"&&expected.Returns["gm"]!.GetValue<int>()==901&&!expected.Timing["correctnessQualified"]!.GetValue<bool>(),"expected nonzero returns remain raw and cannot qualify an undeclared primary contract");
foreach (var value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
{
    var timingLedger = new OperationAcceptanceLedger("timing", "timing");
    var timed = Complete(timingLedger, "timed");
    timed.GmMs = value;
    timingLedger.EndScenario(0);
    var serialized = timingLedger.ToJsonObject().ToJsonString();
    Assert(!timed.Timing["correctnessQualified"]!.GetValue<bool>() && timed.Timing["ratio"] == null && timed.Timing["unqualifiedReasons"]!.AsArray().Count > 0 && serialized.Length > 0, "invalid timing safely exports: " + value);
}
var unknown = new OperationAcceptanceLedger("unknown", "unknown");
unknown.BeginOperation("unknown", "unknown"); unknown.EndScenario(0);
Assert(unknown.ScenarioStatus == "unverified", "unknown scenario cannot pass");
var timeout = Complete(ledger, "timeout"); ledger.EndOperation(timeout.Id, "timeout"); ledger.EndScenario(137);
Assert(timeout.Status == "timeout" && ledger.ScenarioStatus == "timeout", "timeout terminal survives recomputation");

// Exercise the actual on-disk validator, rather than asserting against Evaluate.
var here = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("../temp_docs/operation-contract-tests", here);
var original = Path.GetFullPath("../temp_docs/web-progress/validator-tests", here);
Directory.CreateDirectory(Path.Combine(root, "runs", "test"));
foreach (var file in new[] { "index.json", "runs/test/manifest.json", "runs/test/mesh.json" }) File.Copy(Path.Combine(original, file), Path.Combine(root, file), true);
var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "runs/test/manifest.json")))!;
var c = manifest["cases"]![0]!;
var positive = Complete(new OperationAcceptanceLedger("valid", "valid"), "valid").ToJsonObject();
void Validate(bool accepted, string label, Action<JsonObject> mutate)
{
    var sample = (JsonObject)positive.DeepClone(); mutate(sample);
    c["operations"] = new JsonArray((JsonNode)sample);
    File.WriteAllText(Path.Combine(root, "runs/test/manifest.json"), manifest.ToJsonString());
    var errors = WebProgressDataValidation.Validate(root)["errors"]!.AsArray();
    Assert((errors.Count == 0) == accepted, "validator " + label + (errors.Count == 0 ? "" : " rejected"));
}
Validate(true, "accepts complete fixture", _ => { });
Validate(false, "rejects missing required check", o => o["primaryChecks"]!.AsArray().RemoveAt(0));
Validate(false, "rejects primary failure with pass status", o => o["primaryChecks"]![0]!["status"] = "fail");
Validate(false, "rejects terminal failure with pass status", o => o["terminalStatus"] = "fail");
Validate(false,"rejects not-run with failed return",o=>{o["terminalStatus"]="not-run";o["status"]="not-run";o["returns"]!["gm"]=901;o["timing"]!["ratio"]=null;o["timing"]!["correctnessQualified"]=false;});
Validate(false,"rejects not-run with failed primary",o=>{o["terminalStatus"]="not-run";o["status"]="not-run";o["primaryChecks"]![0]!["status"]="fail";o["timing"]!["ratio"]=null;o["timing"]!["correctnessQualified"]=false;});
Validate(false, "rejects nonzero return", o => o["returns"]!["gm"] = 901);
Validate(false, "rejects unknown return", o => o["returns"]!["pk"] = null);
Validate(false, "rejects missing owner", o => o["primaryChecks"]![0]!.AsObject().Remove("owner"));
Validate(false, "rejects wrong category", o => o["primaryChecks"]![0]!["category"] = "auxiliary");
Validate(false, "rejects forged parameter hash", o => o["parameterHash"] = "PK_BODY_boolean");
Validate(false, "rejects tag pretending to be hash", o => o["inputHashes"]!["gm"]!["target"] = "tag27");
Validate(false, "rejects unsupported API", o => o["api"] = "unknown");
Validate(false,"rejects unknown interpretation version",o=>o["interpretationVersion"]="unknown-version");
Validate(false,"rejects numeric interpretation version",o=>o["interpretationVersion"]=99);
Validate(false,"rejects explicit null interpretation version",o=>o["interpretationVersion"]=null);
Validate(false,"rejects object interpretation version",o=>o["interpretationVersion"]=new JsonObject());
Validate(false,"expected return declaration cannot qualify geometry",o=>{o["interpretationVersion"]="operation-rules-v3";o["api"]="PK_BODY_hollow_2";o["primaryContract"]="expected-rejection-rollback-v1";o["acceptanceKind"]="expected-rejection";o["expectedReturns"]=new JsonObject{["gm"]=901,["pk"]=901};o["returns"]=new JsonObject{["gm"]=901,["pk"]=901};});
Validate(false,"expected-return version cannot qualify a body-result contract",o=>o["interpretationVersion"]="operation-rules-v3");
Validate(false,"old interpretation cannot enable expected-return policy",o=>{o["api"]="PK_BODY_hollow_2";o["primaryContract"]="expected-rejection-rollback-v1";o["acceptanceKind"]="expected-rejection";o["expectedReturns"]=new JsonObject{["gm"]=901,["pk"]=901};o["returns"]=new JsonObject{["gm"]=901,["pk"]=901};});
Validate(false,"rejects Boolean v2 upgrade under legacy interpretation",o=>{o["api"]="PK_BODY_boolean_2";o.Remove("interpretationVersion");});
Validate(false, "rejects unproven same input", o => o["inputProvenance"] = null);
Validate(false, "rejects zero GM", o => { o["timing"]!["gmMs"] = 0; o["timing"]!["ratio"] = 0; });
Validate(false, "rejects wrong boundary", o => o["timing"]!["boundary"] = "scenario");
Validate(false, "rejects wrong ratio", o => o["timing"]!["ratio"] = 3);
Console.WriteLine("Recording contract tests passed; synthetic evidence only, no PK modeling claim.");
static string ScriptPath([CallerFilePath] string path = "") => path;
