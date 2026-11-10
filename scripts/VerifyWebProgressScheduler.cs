#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressScheduler=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Offline scheduling contract tests over small controlled records: queue
// semantics, fair rotation, source-change impact mapping with conservative
// invalidation, bounded exporter retries, restart/interruption recovery, the
// single-instance lock and the accumulating aggregate manifest (cross-run
// evidence references, stale marking, precision retention). No PK, no network,
// no real oracle process is involved.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("../temp_docs/web-progress-scheduler-tests", scripts);
if (Directory.Exists(root)) Directory.Delete(root, true);
Directory.CreateDirectory(root);
var passed = 0;
void Assert(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL " + label);
    passed++;
    Console.WriteLine("PASS " + label);
}

// ---- controlled registry ----
JsonObject Case(string id, string suite, string source, bool representative, bool adapted, string family = "blend", string parameters = """{"radius":0.1}""") => new()
{
    ["id"] = id, ["title"] = id, ["suite"] = suite, ["source"] = source, ["family"] = family, ["operation"] = "test-op",
    ["parameters"] = JsonNode.Parse(parameters), ["api"] = "test_api", ["arguments"] = new JsonArray("fixture"),
    ["meshAdapter"] = adapted, ["representative"] = representative,
};
JsonObject Registry(params JsonObject[] cases) => new()
{
    ["schemaVersion"] = 1, ["version"] = "test-registry-v1", ["scope"] = "test", ["limitations"] = new JsonArray(),
    ["cases"] = new JsonArray(cases.Select(c => (JsonNode?)c?.DeepClone()).ToArray()),
    ["reviewSources"] = new JsonArray(), ["sourceCoverage"] = new JsonArray(),
};
var alpha = Case("blend.alpha", "fake-blend", "scripts/FakeBlendOracle.cs", true, true);
var beta = Case("blend.beta", "fake-blend", "scripts/FakeBlendOracle.cs", false, true);
var gamma = Case("bool.gamma", "fake-bool", "scripts/FakeBoolOracle.cs", false, true, "boolean");
var delta = Case("bool.delta", "fake-bool", "scripts/FakeBoolOracle.cs", true, true, "boolean");
var legacy = Case("legacy.unadapted", "fake-legacy", "scripts/FakeLegacyOracle.cs", false, false);
var registry = Registry(alpha, beta, gamma, delta, legacy);

// ---- controlled run manifests that satisfy the published data contract ----
JsonObject Mesh() => JsonNode.Parse("""{"schemaVersion":1,"bodyId":"fixture","source":{"kernel":"GM","geometryAuthor":"fixture","tessellator":"test-only","commit":"test","chordTolerance":0.01},"faces":[{"id":"face","positions":[0,0,0,1,0,0,0,1,0],"indices":[0,1,2],"role":"selected"}],"edges":[]}""")!.AsObject();
JsonObject Entry(string id, string status, string meshPath, string logPath, double gm = 2, double pk = 1, double? ratio = 2, string checkStatus = "pass")
{
    var empty = meshPath.Length == 0;
    var steps = empty
        ? new JsonArray()
        : new JsonArray((JsonNode)new JsonObject
        {
            ["id"] = "step", ["title"] = "fixture",
            ["gm"] = new JsonObject { ["before"] = meshPath, ["after"] = meshPath },
            ["pk"] = new JsonObject { ["before"] = meshPath, ["after"] = meshPath },
        });
    var checks = empty
        ? new JsonArray()
        : new JsonArray((JsonNode)new JsonObject { ["id"] = "check", ["label"] = "fixture", ["status"] = checkStatus, ["gm"] = gm, ["pk"] = pk, ["absDelta"] = Math.Abs(gm - pk), ["normalizedDelta"] = Math.Abs(gm - pk) / (1 + Math.Abs(pk)), ["threshold"] = 1e-7, ["unit"] = "count", ["evidence"] = null, ["note"] = "test" });
    return new JsonObject
    {
        ["id"] = id, ["title"] = id, ["family"] = "blend", ["operation"] = "test-op", ["status"] = status,
        ["parameters"] = new JsonObject { ["radius"] = 0.1 },
        ["apis"] = new JsonObject { ["gm"] = new JsonObject { ["name"] = "test_api", ["parameters"] = new JsonObject() }, ["pk"] = new JsonObject { ["name"] = "test_api", ["parameters"] = new JsonObject() } },
        ["steps"] = steps, ["checks"] = checks,
        ["performance"] = new JsonObject { ["gmMs"] = empty ? (JsonNode?)null : JsonValue.Create(gm), ["pkMs"] = empty ? (JsonNode?)null : JsonValue.Create(pk), ["ratioGmOverPk"] = status == "pass" && ratio != null ? (JsonNode?)JsonValue.Create(ratio.Value) : null, ["samples"] = empty ? 0 : 1, ["warmup"] = 0, ["scope"] = "test only", ["correctnessQualified"] = checkStatus == "pass", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["note"] = "test" },
        ["evidence"] = logPath.Length == 0 ? new JsonArray() : new JsonArray((JsonNode)new JsonObject { ["label"] = "Oracle log", ["path"] = logPath }),
        ["notes"] = new JsonArray(), ["source"] = new JsonObject { ["path"] = "scripts/FakeOracle.cs", ["revision"] = "rev-measured" },
        ["execution"] = new JsonObject { ["selected"] = true, ["attempted"] = status != "missing", ["exitCode"] = 0 },
    };
}
void WriteRun(string dataRoot, string runId, string revision, bool collection, params JsonObject[] cases)
{
    var dir = Path.Combine(dataRoot, "runs", runId);
    Directory.CreateDirectory(dir);
    foreach (var c in cases)
        foreach (var step in c["steps"]?.AsArray() ?? [])
            foreach (var side in new[] { "gm", "pk" })
                foreach (var key in new[] { "before", "after" })
                    if (step![side]![key] is JsonValue value && value.TryGetValue<string>(out var relative) && relative.Length > 0)
                    {
                        var file = Path.GetFullPath(relative, dir);
                        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                        File.WriteAllText(file, Mesh().ToJsonString());
                    }
    foreach (var c in cases)
        foreach (var item in c["evidence"]?.AsArray() ?? [])
            if (item?["path"] is JsonValue value && value.TryGetValue<string>(out var relative) && relative.Length > 0)
            {
                var file = Path.GetFullPath(relative, dir);
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllText(file, "controlled fixture log\n");
            }
    var manifest = new JsonObject
    {
        ["schemaVersion"] = 1,
        ["run"] = new JsonObject
        {
            ["id"] = runId, ["commit"] = revision, ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test",
            ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(),
            ["provenance"] = new JsonObject { ["checkVersion"] = "check-" + runId },
        },
        ["cases"] = new JsonArray(cases.Select(c => (JsonNode?)c).ToArray()),
    };
    File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest.ToJsonString());
    if (collection) File.WriteAllText(Path.Combine(dir, "collection-manifest.json"), manifest.ToJsonString());
}
void WriteIndex(string dataRoot, params string[] runIds)
{
    var runs = new JsonArray();
    foreach (var id in runIds) runs.Add((JsonNode)new JsonObject { ["id"] = id, ["commit"] = "rev", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["label"] = "test", ["manifest"] = "runs/" + id + "/manifest.json" });
    File.WriteAllText(Path.Combine(dataRoot, "index.json"), new JsonObject { ["schemaVersion"] = 1, ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["latestRunId"] = runIds[^1], ["runs"] = runs }.ToJsonString());
}

// ---- 1. initial enqueue: adapted cases only, representatives first ----
var statePath = Path.Combine(root, "queue-state.json");
var state = WebProgressScheduler.NewState("rev-0", WebProgressScheduler.Hash("registry"), "test-registry-v1");
WebProgressScheduler.Reconcile(state, registry, WebProgressScheduler.Hash("registry"), "test-registry-v1");
var pending = state["pending"]!.AsArray().Select(p => p!["caseId"]!.GetValue<string>()).ToList();
Assert(pending.Count == 4 && !pending.Contains("legacy.unadapted"), "initial enqueue covers exactly the 347-style adapted set (here 4), unadapted never queued");
Assert(pending.All(id => state["pending"]!.AsArray().OfType<JsonObject>().First(p => p["caseId"]!.GetValue<string>() == id)["reason"]!.GetValue<string>() == "initial"), "first enqueue reason is initial");

// ---- 2. deterministic plan: representatives first, then fair suite rotation ----
var plan = WebProgressScheduler.PlanBatch(state, registry, 4);
Assert(plan.SequenceEqual(new[] { "blend.alpha", "bool.delta", "blend.beta", "bool.gamma" }), "plan orders representatives first then fair rotation across suites: " + string.Join(",", plan));

// ---- 3. real failures complete; exporter failures retry bounded and park ----
WebProgressScheduler.RecordOutcome(state, "blend.alpha", "completed", "pass", "run-0", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
WebProgressScheduler.RecordOutcome(state, "bool.delta", "completed", "fail", "run-0", "rev-0", WebProgressScheduler.EntryHash(delta), 0);
WebProgressScheduler.RecordOutcome(state, "bool.gamma", "export-failure", "missing", "run-0", "rev-0", WebProgressScheduler.EntryHash(gamma), 0);
Assert(state["completed"]!.AsObject().Count == 2 && state["pending"]!.AsArray().Count == 2, "pass/fail observations complete; exporter failure stays pending");
Assert(state["pending"]!.AsArray().OfType<JsonObject>().First(p => p["caseId"]!.GetValue<string>() == "bool.gamma")["retryReason"]!.GetValue<string>() == "exporter-compile-or-contract-failure", "exporter failure carries a retry reason");
plan = WebProgressScheduler.PlanBatch(state, registry, 4);
Assert(plan.SequenceEqual(new[] { "blend.beta", "bool.gamma" }), "completed cases are never re-planned; broken case is retried once at a time: " + string.Join(",", plan));
WebProgressScheduler.RecordOutcome(state, "bool.gamma", "export-failure", "missing", "run-0", "rev-0", WebProgressScheduler.EntryHash(gamma), 0);
WebProgressScheduler.RecordOutcome(state, "bool.gamma", "export-failure", "missing", "run-0", "rev-0", WebProgressScheduler.EntryHash(gamma), 0);
Assert(state["blocked"]!.AsObject().ContainsKey("bool.gamma") && !state["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == "bool.gamma"), "after the bounded retries the broken case is parked as export-blocked");
plan = WebProgressScheduler.PlanBatch(state, registry, 4);
Assert(plan.SequenceEqual(new[] { "blend.beta" }), "a parked broken case does not starve the rest of the queue: " + string.Join(",", plan));

// ---- 4. outcome classification from a raw manifest ----
var classifyRaw = new JsonObject
{
    ["cases"] = new JsonArray(
        (JsonNode)new JsonObject { ["id"] = "ok", ["status"] = "pass", ["execution"] = new JsonObject { ["attempted"] = true } },
        (JsonNode)new JsonObject { ["id"] = "crash", ["status"] = "missing", ["execution"] = new JsonObject { ["attempted"] = true } },
        (JsonNode)new JsonObject { ["id"] = "budget", ["status"] = "missing", ["execution"] = new JsonObject { ["attempted"] = false } },
        (JsonNode)new JsonObject { ["id"] = "slow", ["status"] = "timeout", ["execution"] = new JsonObject { ["attempted"] = true } }),
};
var outcomes = WebProgressScheduler.ClassifyOutcomes(classifyRaw, new JsonArray("ok", "crash", "budget", "slow"));
Assert(outcomes[0].Kind == "completed" && outcomes[1].Kind == "export-failure" && outcomes[2].Kind == "not-attempted" && outcomes[3].Kind == "completed" && outcomes[3].Status == "timeout", "real fail/timeout complete, compile-broken does not, budget-unattempted stays pending");

// ---- 5. interruption recovery only re-runs unconfirmed items ----
WebProgressScheduler.RecordBatch(state, new JsonObject { ["index"] = 9, ["runId"] = "run-crashed", ["revision"] = "rev-0", ["startedAt"] = "now", ["status"] = "running", ["caseIds"] = new JsonArray("blend.beta") });
WebProgressScheduler.RecoverInterrupted(state);
Assert(state["batches"]!.AsArray().OfType<JsonObject>().Last()["status"]!.GetValue<string>() == "interrupted", "restart marks the in-flight batch interrupted");
Assert(state["completed"]!.AsObject().Count == 2 && state["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == "blend.beta"), "confirmed observations survive restart; unconfirmed items stay pending and are never promoted to pass");

// ---- 6. single-instance lock ----
using (var first = WebProgressScheduler.InstanceLock.TryAcquire(statePath))
{
    Assert(first != null, "first scheduler instance acquires the lock");
    Assert(WebProgressScheduler.InstanceLock.TryAcquire(statePath) == null, "a concurrent second instance is refused");
}
Assert(WebProgressScheduler.InstanceLock.TryAcquire(statePath) is not null, "the lock is released for the next tick");

// ---- 7. accumulating aggregate manifest over controlled runs ----
var dataRoot = Path.Combine(root, "data");
Directory.CreateDirectory(dataRoot);
var alphaEntry = Entry("blend.alpha", "pass", "cases/blend.alpha/sample-0/meshes/m.json", "cases/blend.alpha/sample-0/oracle.log");
var deltaEntry = Entry("bool.delta", "fail", "cases/bool.delta/sample-0/meshes/m.json", "cases/bool.delta/sample-0/oracle.log", 1.2044436355262826e-7, 1.1265452632651103e-7, null, "fail");
var gammaLog = Entry("bool.gamma", "missing", "", "cases/bool.gamma/sample-0/oracle.log");
gammaLog["execution"]!["attempted"] = true;
WriteRun(dataRoot, "run-0", "rev-0", true, alphaEntry, deltaEntry, gammaLog);
var betaEntry = Entry("blend.beta", "pass", "cases/blend.beta/sample-0/meshes/m.json", "cases/blend.beta/sample-0/oracle.log");
var rawRun1 = new JsonArray(
    (JsonNode)Entry("blend.alpha", "missing", "", ""),
    (JsonNode)betaEntry,
    (JsonNode)Entry("bool.gamma", "missing", "", ""),
    (JsonNode)Entry("bool.delta", "missing", "", ""),
    (JsonNode)Entry("legacy.unadapted", "unsupported", "", ""));
var rawManifest1 = new JsonObject
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject { ["id"] = "run-1", ["commit"] = "rev-1", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject { ["checkVersion"] = "check-run-1" } },
    ["cases"] = rawRun1,
};
var run1Dir = Path.Combine(dataRoot, "runs", "run-1");
Directory.CreateDirectory(Path.Combine(run1Dir, "cases", "blend.beta", "sample-0", "meshes"));
File.WriteAllText(Path.Combine(run1Dir, "cases", "blend.beta", "sample-0", "meshes", "m.json"), Mesh().ToJsonString());
File.WriteAllText(Path.Combine(run1Dir, "cases", "blend.beta", "sample-0", "oracle.log"), "controlled fixture log\n");
File.WriteAllText(Path.Combine(run1Dir, "collection-manifest.json"), rawManifest1.ToJsonString());
File.WriteAllText(Path.Combine(run1Dir, "manifest.json"), rawManifest1.ToJsonString());
WebProgressScheduler.RecordOutcome(state, "blend.beta", "completed", "pass", "run-1", "rev-1", WebProgressScheduler.EntryHash(beta), 1);
Func<string, JsonObject?> loadRun = id =>
{
    foreach (var name in new[] { "collection-manifest.json", "manifest.json" })
    {
        var path = Path.Combine(dataRoot, "runs", id, name);
        if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }
    return null;
};
var merged = WebProgressScheduler.BuildAggregate(rawManifest1, registry, state, dataRoot, "run-1", "rev-1", "check-run-1", 1, loadRun);
File.WriteAllText(Path.Combine(run1Dir, "manifest.json"), merged.ToJsonString());
WriteIndex(dataRoot, "run-0", "run-1");
var byId = merged["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
Assert(byId["blend.alpha"]!["status"]!.GetValue<string>() == "pass" && byId["blend.alpha"]!["measurement"]!["runId"]!.GetValue<string>() == "run-0" && byId["blend.alpha"]!["measurement"]!["carriedForward"]!.GetValue<bool>(), "same-validity results accumulate across batches with their real measurement run");
Assert(byId["blend.alpha"]!["steps"]![0]!["gm"]!["before"]!.GetValue<string>() == "../run-0/cases/blend.alpha/sample-0/meshes/m.json", "carried evidence references resolve into the measurement run");
Assert(byId["bool.delta"]!["checks"]![0]!["gm"]!.GetValue<double>() == 1.2044436355262826e-7, "small numeric values keep full precision through aggregation");
Assert(byId["legacy.unadapted"]!["caseProgress"]!["classification"]!.GetValue<string>() == "export-unadapted", "export-unadapted stays distinct from real kernel capability");
Assert(byId["bool.gamma"]!["caseProgress"]!["classification"]!.GetValue<string>() == "export-blocked" && byId["bool.gamma"]!["evidence"]!.AsArray().Any(e => e!["path"]!.GetValue<string>() == "../run-0/cases/bool.gamma/sample-0/oracle.log"), "export-blocked keeps the original failure log referenced");
var validation = WebProgressDataValidation.Validate(dataRoot);
Assert(validation["errors"]!.AsArray().Count == 0, "accumulated manifest with cross-run references passes the published data contract: " + validation["errors"]!.ToJsonString());

// ---- 8. stale marking on real source changes; UI/doc changes never invalidate ----
WebProgressScheduler.MarkChanges(state, ["scripts/FakeBlendOracle.cs"], registry, "rev-2");
Assert(state["stale"]!.AsObject().ContainsKey("blend.alpha") && state["stale"]!.AsObject().ContainsKey("blend.beta") && state["completed"]!.AsObject().ContainsKey("bool.delta"), "case-source changes invalidate exactly the mapped cases");
Assert(state["stale"]!.AsObject()["blend.alpha"]!["status"]!.GetValue<string>() == "pass" && state["stale"]!.AsObject()["blend.alpha"]!["reason"]!.GetValue<string>() == "source-change", "the stale record retains the real previous observation and marks it stale");
plan = WebProgressScheduler.PlanBatch(state, registry, 4);
Assert(plan.First() == "blend.alpha" && plan.Contains("blend.beta"), "affected cases are re-verified first: " + string.Join(",", plan));
WebProgressScheduler.MarkChanges(state, ["docs/web-progress-ui.md", "web/src/App.tsx"], registry, "rev-3");
Assert(state["completed"]!.AsObject().ContainsKey("bool.delta") && state["stale"]!.AsObject().Count == 2, "UI/doc-only commits do not invalidate any measurement");

// ---- 9. aggregate at the new revision: stale display retained, never a current pass ----
var rawManifest2 = new JsonObject
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject { ["id"] = "run-2", ["commit"] = "rev-3", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject { ["checkVersion"] = "check-run-2" } },
    ["cases"] = new JsonArray(
        (JsonNode)Entry("blend.alpha", "missing", "", ""),
        (JsonNode)Entry("blend.beta", "missing", "", ""),
        (JsonNode)Entry("bool.gamma", "missing", "", ""),
        (JsonNode)Entry("bool.delta", "missing", "", ""),
        (JsonNode)Entry("legacy.unadapted", "unsupported", "", "")),
};
var merged2 = WebProgressScheduler.BuildAggregate(rawManifest2, registry, state, dataRoot, "run-2", "rev-3", "check-run-2", 2, loadRun);
var byId2 = merged2["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
Assert(byId2["blend.alpha"]!["status"]!.GetValue<string>() == "missing" && byId2["blend.alpha"]!["stale"]!["previousStatus"]!.GetValue<string>() == "pass", "stale results are explicit history: retained display, never a current pass");
Assert(byId2["blend.alpha"]!["performance"]!["ratioGmOverPk"] is null && byId2["blend.alpha"]!["stale"]!["previousRatioGmOverPk"]!.GetValue<double?>() == 2, "fail/stale ratios stay empty in the contract field while the old qualified ratio is preserved as stale history");
Assert(byId2["blend.alpha"]!["notes"]!.AsArray().Any(n => n!.GetValue<string>().Contains("过时结果")), "stale cases carry an explicit stale note");
Assert(byId2["bool.delta"]!["caseProgress"]!["classification"]!.GetValue<string>() == "current" && byId2["bool.delta"]!["measurement"]!["carriedForward"]!.GetValue<bool>() && byId2["bool.delta"]!["measurement"]!["revision"]!.GetValue<string>() == "rev-0", "unaffected results carry forward with their exact measured revision (no fake relabeling)");
Assert(byId2["blend.beta"]!["caseProgress"]!["classification"]!.GetValue<string>() == "stale", "un-run-at-current-revision stale cases are classified separately from not-run");

// ---- 10. conservative invalidation when coverage cannot assert impact ----
WebProgressScheduler.MarkChanges(state, ["scripts/Mystery.cs"], registry, "rev-4");
Assert(state["completed"]!.AsObject().Count == 0 && state["stale"]!.AsObject().ContainsKey("bool.delta"), "unknown paths conservatively invalidate everything; nothing is skipped into a pass");

// ---- 11. new recipes are auto-queued; changed recipes invalidate their old result ----
var epsilon = Case("blend.epsilon", "fake-blend", "scripts/FakeBlendOracle.cs", false, true);
registry["cases"]!.AsArray().Add(epsilon);
WebProgressScheduler.Reconcile(state, registry, WebProgressScheduler.Hash("registry2"), "test-registry-v2");
Assert(state["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == "blend.epsilon" && p["reason"]!.GetValue<string>() == "new-recipe"), "a registry extension by another worker is recognized and queued automatically");
var state2 = WebProgressScheduler.NewState("rev-0", "h", "test-registry-v1");
WebProgressScheduler.Reconcile(state2, Registry(alpha, beta), "h", "test-registry-v1");
WebProgressScheduler.RecordOutcome(state2, "blend.alpha", "completed", "pass", "run-0", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
var changed = alpha.DeepClone().AsObject();
changed["parameters"] = JsonNode.Parse("""{"radius":0.2}""");
WebProgressScheduler.Reconcile(state2, Registry(changed, beta), "h2", "test-registry-v1");
Assert(state2["stale"]!.AsObject().ContainsKey("blend.alpha") && state2["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == "blend.alpha" && p["reason"]!.GetValue<string>() == "recipe-change"), "a changed recipe definition invalidates its old observation as recipe-change");

// ---- 11b. retry-pending exporter failures stay visibly unfinished ----
var state3 = WebProgressScheduler.NewState("rev-0", "h3", "test-registry-v1");
var onlyBeta = Registry(beta);
WebProgressScheduler.Reconcile(state3, onlyBeta, "h3", "test-registry-v1");
WebProgressScheduler.RecordOutcome(state3, "blend.beta", "export-failure", "missing", "run-9", "rev-0", WebProgressScheduler.EntryHash(beta), 0);
WebProgressScheduler.RecordOutcome(state3, "blend.beta", "export-failure", "missing", "run-9", "rev-0", WebProgressScheduler.EntryHash(beta), 0);
var rawRetry = new JsonObject
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject { ["id"] = "run-10", ["commit"] = "rev-0", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject { ["checkVersion"] = "check-run-10" } },
    ["cases"] = new JsonArray((JsonNode)Entry("blend.beta", "missing", "", "")),
};
var mergedRetry = WebProgressScheduler.BuildAggregate(rawRetry, onlyBeta, state3, dataRoot, "run-10", "rev-0", "check-run-10", 2, loadRun);
var retryEntry = mergedRetry["cases"]![0]!.AsObject();
Assert(retryEntry["caseProgress"]!["classification"]!.GetValue<string>() == "export-retry" && retryEntry["caseProgress"]!["attempts"]!.GetValue<int>() == 2 && retryEntry["caseProgress"]!["retryReason"]!.GetValue<string>() == "exporter-compile-or-contract-failure", "bounded-retry exporter failures stay pending with attempts and retry reason, never completed or passed");

// ---- 12. aggregate contract negatives: escaping references are rejected ----
var bad = merged.DeepClone().AsObject();
bad["cases"]![0]!["steps"]![0]!["gm"]!["before"] = "../../escape.json";
var badDir = Path.Combine(root, "bad");
Directory.CreateDirectory(Path.Combine(badDir, "runs", "run-1"));
File.WriteAllText(Path.Combine(badDir, "runs", "run-1", "manifest.json"), bad.ToJsonString());
WriteIndex(badDir, "run-1");
Assert(WebProgressDataValidation.Validate(badDir)["errors"]!.AsArray().Count > 0, "cross-run references that escape the data root are rejected");
var bad2 = merged.DeepClone().AsObject();
bad2["cases"]![0]!["steps"]![0]!["gm"]!["before"] = "../run-0/cases/blend.alpha/sample-0/meshes/missing.json";
File.WriteAllText(Path.Combine(badDir, "runs", "run-1", "manifest.json"), bad2.ToJsonString());
Assert(WebProgressDataValidation.Validate(badDir)["errors"]!.AsArray().Count > 0, "dangling references into pruned or absent runs are rejected instead of silently dropped");

Console.WriteLine("ALL PASS " + passed + " scheduling contract checks");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;
