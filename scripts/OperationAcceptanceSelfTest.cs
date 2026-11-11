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
OperationRecord Complete(OperationAcceptanceLedger ledger, string id)
{
    var options = new JsonObject { ["gm"] = new JsonObject { ["function"] = 1 }, ["pk"] = new JsonObject { ["function"] = 1 } };
    var op = ledger.BeginOperation(id, "fixture Boolean", api: "PK_BODY_boolean", options: options,
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
Validate(false, "rejects nonzero return", o => o["returns"]!["gm"] = 901);
Validate(false, "rejects unknown return", o => o["returns"]!["pk"] = null);
Validate(false, "rejects missing owner", o => o["primaryChecks"]![0]!.AsObject().Remove("owner"));
Validate(false, "rejects wrong category", o => o["primaryChecks"]![0]!["category"] = "auxiliary");
Validate(false, "rejects forged parameter hash", o => o["parameterHash"] = "PK_BODY_boolean");
Validate(false, "rejects tag pretending to be hash", o => o["inputHashes"]!["gm"]!["target"] = "tag27");
Validate(false, "rejects unsupported API", o => o["api"] = "unknown");
Validate(false, "rejects unproven same input", o => o["inputProvenance"] = null);
Validate(false, "rejects zero GM", o => { o["timing"]!["gmMs"] = 0; o["timing"]!["ratio"] = 0; });
Validate(false, "rejects wrong boundary", o => o["timing"]!["boundary"] = "scenario");
Validate(false, "rejects wrong ratio", o => o["timing"]!["ratio"] = 3);
Console.WriteLine("Recording contract tests passed; synthetic evidence only, no PK modeling claim.");
static string ScriptPath([CallerFilePath] string path = "") => path;
