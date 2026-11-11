#!/usr/bin/env dotnet run
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// BCL only: immutable external artifacts are read; only this audit's docs are written.
var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, ".."));
const string dataRoot = "../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/bundle/data";
const string runId = "20261110T195008Z-610e3ce8";
const string caseId = "support-chain.asymmetric43-base";
var runPath = $"runs/{runId}";
var sample = $"cases/{caseId}/sample-0";
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, dataRoot, path)))!;
string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, dataRoot, path))));
var index = Read("index.json");
var manifest = Read(runPath + "/manifest.json");
var item = manifest["cases"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == caseId)!;
var capture = Read($"{runPath}/{sample}/capture.json");
var comparison = Read($"{runPath}/{sample}/evidence/compare-0.json");
var checks = item["checks"]!.AsArray();
if (capture["checks"]!.AsArray().Count != 28 || checks.Count != 29)
    throw new InvalidOperationException("Pinned check inventory changed");
if (comparison["localDiffs"]!.AsArray().Any(d => d!["type"]!.GetValue<int>() != 21949))
    throw new InvalidOperationException("Pinned local differences changed");
var groups = new JsonArray();
var seen = new HashSet<string>(StringComparer.Ordinal);
void Group(string id, string title, string coverage, bool collapsed, Func<string, bool> select)
{
    var members = checks.Where(c => select(c!["id"]!.GetValue<string>())).ToArray();
    foreach (var member in members)
        if (!seen.Add(member!["id"]!.GetValue<string>())) throw new InvalidOperationException("Overlapping groups");
    groups.Add((JsonNode)new JsonObject
    {
        ["id"] = id, ["title"] = title, ["coverage"] = coverage,
        ["defaultCollapsed"] = collapsed,
        ["checks"] = new JsonArray(members.Select(c => c!.DeepClone()).ToArray()),
    });
}
Group("primary", "PK_DEBUG_BODY_compare + type/regions", "Raw compare is primary geometry/structure evidence; original equivalence gate also checks type and region material/shell-count multiset.", false, id => id == "0-equivalence");
Group("overlap-counts", "Topology counts", "Shell/face/edge/vertex totals overlap compare numerical coverage; regions overlap the helper, not the six raw global records. Preserve all five original values.", true, id => id.StartsWith("0-count-", StringComparison.Ordinal));
Group("operation-contract", "API return, blend fault, output count", "Cannot be replaced by body comparison; output body-set correspondence precedes per-body comparison.", true, id => id.EndsWith("-api", StringComparison.Ordinal) || id.EndsWith("-blend-fault", StringComparison.Ordinal) || id == "0-body-count");
Group("body-validity", "Full PK_BODY_check on both bodies", "Validity is independent of pairwise similarity; keep both original zero-fault results.", true, id => id.EndsWith("-body-check", StringComparison.Ordinal));
Group("mass", "15 independent mass properties", "Independent numeric contract: normalized delta < 1e-7 at accuracy .99999999; compare does not emit these values.", true, id => id.StartsWith("0-mass-", StringComparison.Ordinal));
Group("scenario", "Original oracle exit", "Process/scenario acceptance also includes uncaptured downstream checks; cannot infer individual operation qualification from exit alone.", true, id => id == "oracle-exit");
if (seen.Count != checks.Count) throw new InvalidOperationException("Unmapped original checks");
var summary = new JsonObject
{
    ["schemaVersion"] = 1,
    ["purpose"] = "Read-only presentation proposal, not an acceptance or published-data update",
    ["baselineCommit"] = "610e3ce844bf99c85af6069905f9f3deaab08133",
    ["paths"] = new JsonObject { ["relativeToProjectRoot"] = dataRoot, ["runRelativeToDataRoot"] = runPath, ["source"] = item["source"]!.DeepClone(), ["captureRelativeToRun"] = sample + "/capture.json", ["primaryEvidenceRelativeToRun"] = sample + "/evidence/compare-0.json", ["oracleRelativeToRun"] = sample + "/oracle.log", ["executionRelativeToRun"] = sample + "/execution.json" },
    ["sourceFingerprints"] = new JsonObject { ["index"] = Hash("index.json"), ["manifest"] = Hash(runPath + "/manifest.json"), ["capture"] = Hash(runPath + "/" + sample + "/capture.json"), ["comparison"] = Hash(runPath + "/" + sample + "/evidence/compare-0.json") },
    ["freshness"] = new JsonObject { ["indexGeneratedAt"] = index["generatedAt"]!.DeepClone(), ["latestRunId"] = index["latestRunId"]!.DeepClone(), ["run"] = manifest["run"]!.DeepClone(), ["attemptedCount"] = manifest["cases"]!.AsArray().Count(c => c!["execution"]?["attempted"]?.GetValue<bool>() == true), ["statusCounts"] = new JsonArray(manifest["cases"]!.AsArray().GroupBy(c => c!["status"]!.GetValue<string>()).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (JsonNode)new JsonObject { ["status"] = g.Key, ["count"] = g.Count() }).ToArray()), ["uiDeploymentUpdatesData"] = false },
    ["case"] = new JsonObject { ["id"] = caseId, ["originalStatus"] = item["status"]!.DeepClone(), ["parameters"] = item["parameters"]!.DeepClone(), ["captureCheckCount"] = 28, ["manifestCheckCount"] = 29, ["capabilityCount"] = null, ["sessionPrecision"] = capture["sessionPrecision"]!.DeepClone(), ["capturedSteps"] = capture["steps"]!.AsArray().Count, ["performance"] = item["performance"]!.DeepClone() },
    ["primary"] = new JsonObject { ["api"] = "PK_DEBUG_BODY_compare", ["raw"] = comparison.DeepClone(), ["globalResultName"] = "PK_DEBUG_global_res_no_diffs_c", ["localResultName"] = "PK_DEBUG_local_res_diffs_c", ["interpretation"] = "No global differences; 12 chart-point diagnostics in 7 returned differing face pairs; original helper pass with only n_cht_pts diagnostic.", ["blockingReturnedLocalDiffCount"] = 0, ["diagnosticLocalDiffCount"] = 12, ["returnedFacePairsAreAllMatchedFaces"] = false, ["maxDiffCapReached"] = false, ["capReason"] = "13 faces per body < max_diffs 1024; 7 returned pairs < cap", ["accurateDeviationTestsEnabled"] = false, ["wireLocalGeometryCoveredByRawCompare"] = false, ["rawShellCount"] = 1, ["askShellCount"] = 2, ["shellCountNote"] = "Keep separate query semantics; do not replace original BODY_ask_shells values with raw compare counts." },
    ["coverageGroups"] = groups,
    ["duplicateCompareAudit"] = new JsonObject { ["source"] = "scripts/WebProgressCapture.cs:172-198 + scripts/ParasolidBodyCompare.cs:24-68 @610e3ce", ["sameReferenceAndReceivedTags"] = true, ["sameEffectiveMask"] = true, ["samePostOperationStage"] = true, ["betweenCalls"] = "Raw result serialization and r_f; helper then asks body types and region/shell properties. No intervening body mutation or precision setting in these paths.", ["recommendation"] = "One lexical-scope real-PK comparison; write existing raw evidence, then run the existing shared verdict over that same live result, including type/region and applicable finite-wire checks; one owner frees in finally.", ["preserve"] = new JsonArray("All original check IDs, values, thresholds and statuses", "Raw evidence even when equivalence fails", "Exact current global/local and n_cht_pts-only verdict", "Full type/region semantics, BODY_check, operation output/rollback, independent mass and wire/API contracts", "Standalone helper and original oracle checks at their own distinct stages"), ["notImplemented"] = true, ["doNotCacheAcrossOperations"] = true },
    ["renderRules"] = new JsonArray("Primary evidence opens compare-0.json, plus source and equivalence gate; original links remain available.", "Show raw local differences with n_cht_pts diagnostic annotation; do not display raw local_result as no differences.", "Expand every fail/missing/timeout/unsupported regardless of defaultCollapsed; never drop original status, value, threshold or evidence.", "29 is the original check-row count, not independent capabilities or 29 geometric tests.", "Before is a viewer state; comparison applies to captured operation output/fresh XT, not the currently displayed Before mesh.", "Show run commit, UTC and UTC+08 collection time, attempted/registered, sample count and timing scope beside performance.", "UI version, index-write time, data collection time, accepted candidate and newly collected candidate evidence are separate fields.", "Do not upgrade diagnostics to operation pass or historical case aggregate to new operation qualification."),
    ["candidateLedger"] = new JsonArray(new JsonObject { ["kind"] = "finite-source-and-foot", ["implementation"] = "a5ab85d", ["review"] = "7c1606e", ["scope"] = "Accepted fixed finite coverage layer / 39 finite plus foot checks; not Boolean acceptance", ["collectedInPinnedRun"] = false }, new JsonObject { ["kind"] = "empty-readset-guard", ["implementation"] = "7aca6f0275cdb8c50fd7fb370103b66cf315ff7a", ["review"] = "957ce708", ["scope"] = "Accepted bounded source correctness and controls; no measured speedup", ["collectedInPinnedRun"] = false }),
};
var destination = Path.Combine(root, "docs", "reviews", "advanced-modeling", "web-primary-evidence-audit-610e3ce.json");
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
File.WriteAllText(destination, summary.ToJsonString(new() { WriteIndented = true }) + "\n");
Console.WriteLine($"PASS read-only snapshot: 28 capture / 29 manifest rows, {groups.Count} groups; output={Path.GetRelativePath(root, destination)}");
static string ScriptPath([CallerFilePath] string path = "") => path;
