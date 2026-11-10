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

// ---- 13. state schema upgrade: legacy fingerprint state is backed up and rebuilt; corrupt/unknown fail loudly ----
var legacyPath = Path.Combine(root, "legacy-state.json");
var legacyText = """{"fingerprint":"old","updatedAt":"now","commit":"old"}""";
File.WriteAllText(legacyPath, legacyText);
var loadedLegacy = WebProgressScheduler.LoadState(legacyPath, out var legacyBackup);
Assert(loadedLegacy == null && legacyBackup != null && File.Exists(legacyBackup) && File.ReadAllText(legacyBackup!) == legacyText, "legacy fingerprint state is preserved as a backup and never read as a queue");
Assert(File.Exists(legacyPath), "the legacy state file itself stays in place until the rebuilt queue is saved");
var corruptPath = Path.Combine(root, "corrupt-state.json");
var corruptText = """{"fingerprint":""";
File.WriteAllText(corruptPath, corruptText);
var corruptFailed = false;
try { WebProgressScheduler.LoadState(corruptPath, out _); } catch (InvalidDataException) { corruptFailed = true; }
Assert(corruptFailed && File.ReadAllText(corruptPath) == corruptText, "corrupt state JSON fails explicitly and keeps the original data untouched");
var unknownPath = Path.Combine(root, "unknown-state.json");
var unknownText = """{"scheduler":"other-scheduler-v1","schemaVersion":1}""";
File.WriteAllText(unknownPath, unknownText);
var unknownFailed = false;
try { WebProgressScheduler.LoadState(unknownPath, out _); } catch (InvalidDataException) { unknownFailed = true; }
Assert(unknownFailed && File.ReadAllText(unknownPath) == unknownText, "unknown state schemas fail explicitly instead of silently starting empty");
var missingPath = Path.Combine(root, "missing-state.json");
var missingText = """{"scheduler":"web-progress-scheduler-v1","schemaVersion":1,"sourceRevision":"r"}""";
File.WriteAllText(missingPath, missingText);
var missingFailed = false;
try { WebProgressScheduler.LoadState(missingPath, out _); } catch (InvalidDataException) { missingFailed = true; }
Assert(missingFailed, "a v1 state missing required queue collections fails explicitly");
var okPath = Path.Combine(root, "ok-state.json");
WebProgressScheduler.Save(okPath, WebProgressScheduler.NewState("rev-0", "h", "v"));
Assert(WebProgressScheduler.LoadState(okPath, out var noBackup) is not null && noBackup == null, "a valid v1 queue state loads without migration");

// ---- 14. working-tree fingerprints cover staged content and reverse changes ----
var wsStaged = WebProgressScheduler.WorkingState("", "M src/source.cs\n+// v2\n", "", "src/source.cs");
var wsClean = WebProgressScheduler.WorkingState("", "", "", "");
Assert(wsStaged.Hash != wsClean.Hash && wsStaged.Paths.SequenceEqual(new[] { "src/source.cs" }), "pure staged content changes the fingerprint and maps its path");
var wsSame = WebProgressScheduler.WorkingState("", "M src/source.cs\n+// v2\n", "", "src/source.cs");
Assert(wsStaged.Hash == wsSame.Hash, "identical staged content keeps a stable fingerprint");
var wsMixed = WebProgressScheduler.WorkingState("M src/source.cs\n", "M src/a.cs\n", "src/source.cs", "src/a.cs\nsrc/b.cs");
Assert(wsMixed.Paths.SequenceEqual(new[] { "src/a.cs", "src/b.cs", "src/source.cs" }), "staged and unstaged names union distinctly for mixed same-source changes");
var mergedRevert = WebProgressScheduler.MergeChangePaths(["docs/x.md"], ["src/source.cs"], []);
Assert(mergedRevert.Contains("src/source.cs") && mergedRevert.Contains("docs/x.md"), "reverting a dirty file keeps the previously dirty path in the impact set");
var mergedRename = WebProgressScheduler.MergeChangePaths([], ["src/a.cs", "src/b.cs"], ["src/a.cs"]);
Assert(mergedRename.Count == 2, "rename/delete reverses keep both names in the union");
var state4 = WebProgressScheduler.NewState("rev-0", "h4", "test-registry-v1");
WebProgressScheduler.Reconcile(state4, Registry(alpha), "h4", "test-registry-v1");
WebProgressScheduler.RecordOutcome(state4, "blend.alpha", "completed", "pass", "run-4", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
WebProgressScheduler.MarkChanges(state4, ["scripts/web-progress-registry.json.bak"], Registry(alpha), "rev-5");
Assert(state4["completed"]!.AsObject().Count == 0 && state4["stale"]!.AsObject().ContainsKey("blend.alpha"), "registry carveout is exact-match only; adjacent kernel-affecting files invalidate conservatively");

// ---- 15. eligibility sweep: records outside the active selection retire with history ----
// (a) adapter withdrawal: no crash, nothing planned, history preserved
var s5 = WebProgressScheduler.NewState("rev-0", "h5", "test-registry-v1");
WebProgressScheduler.Reconcile(s5, Registry(alpha), "h5", "test-registry-v1");
var alphaUnadapted = alpha.DeepClone().AsObject();
alphaUnadapted["meshAdapter"] = false;
var unadaptedRegistry = Registry(alphaUnadapted);
WebProgressScheduler.Reconcile(s5, unadaptedRegistry, "h5b", "test-registry-v1");
Assert(s5["pending"]!.AsArray().Count == 0, "adapter withdrawal cleans the pending queue instead of leaving unselected work");
var s5Plan = WebProgressScheduler.PlanBatch(s5, unadaptedRegistry, 4);
Assert(s5Plan.Count == 0, "an unadapted id can never be planned (was KeyNotFoundException)");
Assert(s5["retired"]?["blend.alpha"]?["records"]?.AsArray().Count > 0, "withdrawn records are archived as history, not silently dropped");
// (b) narrowed selection cannot execute excluded cases; completed observations survive
var s6 = WebProgressScheduler.NewState("rev-0", "h6", "test-registry-v1");
var bothRegistry = Registry(alpha, beta);
WebProgressScheduler.Reconcile(s6, bothRegistry, "h6", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s6, "blend.alpha", "completed", "pass", "run-6", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
WebProgressScheduler.Reconcile(s6, bothRegistry, "h6", "test-registry-v1", e => e["id"]!.GetValue<string>() == "blend.alpha");
Assert(s6["pending"]!.AsArray().Count == 0 && WebProgressScheduler.PlanBatch(s6, bothRegistry, 4).Count == 0, "narrowed selection neither keeps nor plans excluded pending cases");
Assert(s6["completed"]!.AsObject().ContainsKey("blend.alpha"), "narrowed selection never deletes old valid observations");
// (c) restoring the selection re-queues unfinished work with its history
WebProgressScheduler.Reconcile(s6, bothRegistry, "h6", "test-registry-v1");
var restored = s6["pending"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == "blend.beta");
Assert(restored != null && restored["reason"]!.GetValue<string>() == "initial", "restoring the selection accurately re-queues the unfinished case with its original reason");
// (d) blocked history survives a filter round-trip and stays parked
var s7 = WebProgressScheduler.NewState("rev-0", "h7", "test-registry-v1");
WebProgressScheduler.Reconcile(s7, Registry(beta), "h7", "test-registry-v1");
for (var i = 0; i < WebProgressScheduler.MaxAttempts; i++) WebProgressScheduler.RecordOutcome(s7, "blend.beta", "export-failure", "missing", "run-7", "rev-0", WebProgressScheduler.EntryHash(beta), 0);
Assert(s7["blocked"]!.AsObject().ContainsKey("blend.beta"), "bounded retries park the broken case");
WebProgressScheduler.Reconcile(s7, Registry(beta), "h7", "test-registry-v1", _ => false);
WebProgressScheduler.Reconcile(s7, Registry(beta), "h7", "test-registry-v1");
Assert(s7["blocked"]!.AsObject().ContainsKey("blend.beta") && s7["pending"]!.AsArray().Count == 0, "a parked broken case is restored parked (with attempts/lastRunId), not silently re-queued or lost");
Assert(s7["blocked"]!["blend.beta"]!["lastRunId"]!.GetValue<string>() == "run-7", "the restored blocked record keeps its failure evidence reference");
// (e) removed recipes retire their records but keep completed history
var s8b = WebProgressScheduler.NewState("rev-0", "h8b", "test-registry-v1");
WebProgressScheduler.Reconcile(s8b, Registry(alpha, beta), "h8b", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s8b, "blend.alpha", "completed", "pass", "run-8", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
WebProgressScheduler.Reconcile(s8b, Registry(beta), "h8c", "test-registry-v1");
Assert(s8b["completed"]!.AsObject().ContainsKey("blend.alpha") && s8b["pending"]!.AsArray().Count == 1, "a removed recipe leaves the active queue while its completed observation is preserved");

// ---- 16. stale+blocked coexistence: recipe/source changes still unblock and re-queue ----
var s9 = WebProgressScheduler.NewState("rev-0", "h9", "test-registry-v1");
var regAlpha = Registry(alpha);
WebProgressScheduler.Reconcile(s9, regAlpha, "h9", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s9, "blend.alpha", "completed", "pass", "run-9", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
WebProgressScheduler.MarkChanges(s9, ["src/Changed.cs"], regAlpha, "rev-1");
for (var i = 0; i < WebProgressScheduler.MaxAttempts; i++) WebProgressScheduler.RecordOutcome(s9, "blend.alpha", "export-failure", "missing", "run-9b", "rev-1", WebProgressScheduler.EntryHash(alpha), 1);
Assert(s9["blocked"]!.AsObject().ContainsKey("blend.alpha") && s9["stale"]!.AsObject().ContainsKey("blend.alpha") && s9["pending"]!.AsArray().Count == 0, "precondition: stale and blocked coexist with an empty queue");
var alphaChanged = alpha.DeepClone().AsObject();
alphaChanged["parameters"] = JsonNode.Parse("""{"radius":0.2}""");
WebProgressScheduler.Reconcile(s9, Registry(alphaChanged), "h9b", "test-registry-v1");
var unblocked = s9["pending"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == "blend.alpha");
Assert(unblocked != null && unblocked["reason"]!.GetValue<string>() == "recipe-change" && unblocked["attempts"]!.GetValue<int>() == 0 && !s9["blocked"]!.AsObject().ContainsKey("blend.alpha"), "a recipe change with stale+blocked unblocks and re-queues the case for the new recipe (was permanently stuck)");
Assert(s9["stale"]!["blend.alpha"]!["runId"]!.GetValue<string>() == "run-9" && s9["stale"]!["blend.alpha"]!["revision"]!.GetValue<string>() == "rev-0", "the stale display keeps the old measurement source and revision");
var s10 = WebProgressScheduler.NewState("rev-0", "h10", "test-registry-v1");
WebProgressScheduler.Reconcile(s10, regAlpha, "h10", "test-registry-v1");
for (var i = 0; i < WebProgressScheduler.MaxAttempts; i++) WebProgressScheduler.RecordOutcome(s10, "blend.alpha", "export-failure", "missing", "run-10", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
WebProgressScheduler.MarkChanges(s10, ["src/Changed.cs"], regAlpha, "rev-1");
Assert(s10["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == "blend.alpha" && p["reason"]!.GetValue<string>() == "affected") && !s10["blocked"]!.AsObject().ContainsKey("blend.alpha"), "a source change also unblocks a parked case and re-queues it as affected");

// ---- 17. measurement provenance: real check versions only, unknown when untraceable ----
JsonObject ProvManifest(string runId, string? checkVersion)
{
    var provenance = new JsonObject();
    if (checkVersion != null) provenance["checkVersion"] = checkVersion;
    return new JsonObject
    {
        ["schemaVersion"] = 1,
        ["run"] = new JsonObject { ["id"] = runId, ["commit"] = "rev-old", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = provenance },
        ["cases"] = new JsonArray((JsonNode)Entry("blend.alpha", "missing", "", "")),
    };
}
JsonObject ProvRaw() => new()
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject { ["id"] = "run-new", ["commit"] = "rev-1", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject() },
    ["cases"] = new JsonArray((JsonNode)Entry("blend.alpha", "missing", "", "")),
};
var s11 = WebProgressScheduler.NewState("rev-0", "h11", "test-registry-v1");
WebProgressScheduler.Reconcile(s11, Registry(alpha), "h11", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s11, "blend.alpha", "completed", "pass", "run-old", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
var mergedProv1 = WebProgressScheduler.BuildAggregate(ProvRaw(), Registry(alpha), s11, dataRoot, "run-new", "rev-1", "NEW-CHECK-VERSION", 2, id => id == "run-old" ? ProvManifest("run-old", null) : null);
Assert(mergedProv1["cases"]![0]!["measurement"]!["checkVersion"]!.GetValue<string>() == "unknown", "an untraceable old measurement reports checkVersion=unknown, never the current version (was NEW-CHECK-VERSION)");
Assert(mergedProv1["cases"]![0]!["measurement"]!["revision"]!.GetValue<string>() == "rev-0", "carry-forward keeps the original measurement revision");
var s12 = WebProgressScheduler.NewState("rev-0", "h12", "test-registry-v1");
WebProgressScheduler.Reconcile(s12, Registry(alpha), "h12", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s12, "blend.alpha", "completed", "pass", "run-old", "rev-0", WebProgressScheduler.EntryHash(alpha), 0, "REAL-OLD-CHECK");
var mergedProv2 = WebProgressScheduler.BuildAggregate(ProvRaw(), Registry(alpha), s12, dataRoot, "run-new", "rev-1", "NEW-CHECK-VERSION", 2, id => id == "run-old" ? ProvManifest("run-old", null) : null);
Assert(mergedProv2["cases"]![0]!["measurement"]!["checkVersion"]!.GetValue<string>() == "REAL-OLD-CHECK", "the check version persisted at measurement confirmation is reported verbatim");
var s13 = WebProgressScheduler.NewState("rev-0", "h13", "test-registry-v1");
WebProgressScheduler.Reconcile(s13, Registry(alpha), "h13", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s13, "blend.alpha", "completed", "pass", "run-old", "rev-0", WebProgressScheduler.EntryHash(alpha), 0);
var mergedProv3 = WebProgressScheduler.BuildAggregate(ProvRaw(), Registry(alpha), s13, dataRoot, "run-new", "rev-1", "NEW-CHECK-VERSION", 2, id => id == "run-old" ? ProvManifest("run-old", "check-run-old") : null);
Assert(mergedProv3["cases"]![0]!["measurement"]!["checkVersion"]!.GetValue<string>() == "check-run-old", "a traceable source-run check version is preferred over any current value");

// ---- 18. bad-run quarantine: evidence kept, index clean, next batch advances ----
var qRoot = Path.Combine(root, "quarantine");
Directory.CreateDirectory(qRoot);
var qGood = Entry("blend.alpha", "pass", "cases/blend.alpha/sample-0/meshes/m.json", "cases/blend.alpha/sample-0/oracle.log");
var qBad = Entry("bool.delta", "pass", "cases/bool.delta/sample-0/meshes/m.json", "cases/bool.delta/sample-0/oracle.log");
WriteRun(qRoot, "run-good", "rev-0", true, qGood);
WriteRun(qRoot, "run-bad", "rev-0", true, qBad);
var badManifestPath = Path.Combine(qRoot, "runs", "run-bad", "manifest.json");
var badManifest = JsonNode.Parse(File.ReadAllText(badManifestPath))!.AsObject();
badManifest["cases"]![0]!["steps"]![0]!["gm"]!["before"] = "cases/bool.delta/sample-0/meshes/missing.json";
File.WriteAllText(badManifestPath, badManifest.ToJsonString());
File.WriteAllText(Path.Combine(qRoot, "runs", "run-bad", "collection-manifest.json"), badManifest.ToJsonString());
WriteIndex(qRoot, "run-good", "run-bad");
var polluted = WebProgressDataValidation.Validate(qRoot);
Assert(polluted["errors"]!.AsArray().Count > 0 && polluted["runErrors"]!["run-bad"]!.AsArray().Count > 0 && polluted["runErrors"]!["run-good"] is not JsonArray { Count: > 0 }, "validation attributes the pollution to the bad run only (runErrors)");
var s14 = WebProgressScheduler.NewState("rev-0", "h14", "test-registry-v1");
WebProgressScheduler.Reconcile(s14, Registry(alpha, beta), "h14", "test-registry-v1");
WebProgressScheduler.RecordOutcome(s14, "blend.alpha", "completed", "pass", "run-bad", "rev-0", WebProgressScheduler.EntryHash(alpha), 0, "check-bad");
WebProgressScheduler.QuarantineRun(s14, qRoot, "run-bad", "contract-failed", polluted["runErrors"]!["run-bad"]!.AsArray());
var qIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(qRoot, "index.json")))!;
Assert(!qIndex["runs"]!.AsArray().Any(r => r!["id"]!.GetValue<string>() == "run-bad"), "the quarantined run leaves the validated/published index");
Assert(File.Exists(badManifestPath) && Directory.Exists(Path.Combine(qRoot, "runs", "run-bad", "cases")), "the quarantined run's raw evidence is retained untouched (no log or manifest deleted)");
Assert(WebProgressDataValidation.Validate(qRoot)["errors"]!.AsArray().Count == 0, "after isolation the next valid batch advances: same validator, no standard lowered, no error ignored");
Assert(qIndex["latestRunId"]!.GetValue<string>() == "run-good", "the index follows the newest remaining validated run");
Assert(s14["stale"]!.AsObject().ContainsKey("blend.alpha") && s14["stale"]!["blend.alpha"]!["reason"]!.GetValue<string>() == "evidence-quarantined" && s14["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == "blend.alpha" && p["reason"]!.GetValue<string>() == "evidence-quarantined"), "observations relying on quarantined evidence become stale history and re-queue; they are never kept as current passes");
var rawQ = new JsonObject
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject { ["id"] = "run-new", ["commit"] = "rev-1", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject { ["checkVersion"] = "check-new" } },
    ["cases"] = new JsonArray((JsonNode)Entry("blend.alpha", "missing", "", ""), (JsonNode)Entry("blend.beta", "missing", "", "")),
};
var mergedQ = WebProgressScheduler.BuildAggregate(rawQ, Registry(alpha, beta), s14, qRoot, "run-new", "rev-1", "check-new", 1, id => id == "run-bad" ? badManifest : null);
var mergedQText = mergedQ.ToJsonString();
Assert(!mergedQText.Contains("../run-bad/cases", StringComparison.Ordinal), "the aggregate never points at the quarantined run's meshes/manifests");
Assert(mergedQ["cases"]![0]!["notes"]!.AsArray().Any(n => n!.GetValue<string>().Contains("证据隔离")), "the degraded stale display explains the quarantine explicitly");

// ---- 19. aggregate-invalid recovery: rollback keeps a valid raw run, then the next batch advances ----
var qRoot2 = Path.Combine(root, "aggregate-recovery");
Directory.CreateDirectory(Path.Combine(qRoot2, "runs", "run-19"));
var s16 = WebProgressScheduler.NewState("rev-0", "h16", "test-registry-v1");
WebProgressScheduler.Reconcile(s16, Registry(alpha, beta), "h16", "test-registry-v1");
for (var i = 0; i < WebProgressScheduler.MaxAttempts; i++) WebProgressScheduler.RecordOutcome(s16, "blend.beta", "export-failure", "missing", "run-ghost", "rev-0", WebProgressScheduler.EntryHash(beta), 0);
var raw19 = new JsonObject
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject { ["id"] = "run-19", ["commit"] = "rev-1", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject { ["checkVersion"] = "check-19" } },
    ["cases"] = new JsonArray((JsonNode)Entry("blend.alpha", "missing", "", ""), (JsonNode)Entry("blend.beta", "missing", "", "")),
};
var merged19 = WebProgressScheduler.BuildAggregate(raw19, Registry(alpha, beta), s16, qRoot2, "run-19", "rev-1", "check-19", 0, _ => null);
File.WriteAllText(Path.Combine(qRoot2, "runs", "run-19", "collection-manifest.json"), raw19.ToJsonString());
File.WriteAllText(Path.Combine(qRoot2, "runs", "run-19", "manifest.json"), merged19.ToJsonString());
WriteIndex(qRoot2, "run-19");
Assert(WebProgressDataValidation.Validate(qRoot2)["errors"]!.AsArray().Count > 0, "an aggregate whose blocked evidence dangles is invalid and must roll back");
File.WriteAllText(Path.Combine(qRoot2, "runs", "run-19", "manifest.json"), raw19.ToJsonString());
Assert(WebProgressDataValidation.Validate(qRoot2)["errors"]!.AsArray().Count == 0 && s16["completed"]!.AsObject().Count == 0, "rollback restores the raw manifest, nothing advances as completed");
Directory.CreateDirectory(Path.Combine(qRoot2, "runs", "run-ghost", "cases", "blend.beta", "sample-0"));
File.WriteAllText(Path.Combine(qRoot2, "runs", "run-ghost", "cases", "blend.beta", "sample-0", "oracle.log"), "failure log\n");
var merged19b = WebProgressScheduler.BuildAggregate(raw19, Registry(alpha, beta), s16, qRoot2, "run-19", "rev-1", "check-19", 0, _ => null);
File.WriteAllText(Path.Combine(qRoot2, "runs", "run-19", "manifest.json"), merged19b.ToJsonString());
Assert(WebProgressDataValidation.Validate(qRoot2)["errors"]!.AsArray().Count == 0, "after the cause is resolved the next aggregate validates and the batch can advance");

// ---- 20. publish closure: complete references, pre-validated target, atomic index ----
var s7Dest = Path.Combine(root, "publish-dest");
Directory.CreateDirectory(s7Dest);
WebProgressPublishing.PublishClosure(dataRoot, ["run-1"], "run-1", s7Dest);
Assert(Directory.Exists(Path.Combine(s7Dest, "runs", "run-0")) && Directory.Exists(Path.Combine(s7Dest, "runs", "run-1")), "the full reference closure is collected even when the referenced run is no longer a direct state record (was a subset publish)");
Assert(WebProgressDataValidation.Validate(s7Dest)["errors"]!.AsArray().Count == 0, "a fresh destination validates clean (was 11 missing ../run-0 references)");
var s7Index = JsonNode.Parse(File.ReadAllText(Path.Combine(s7Dest, "index.json")))!;
Assert(s7Index["latestRunId"]!.GetValue<string>() == "run-1" && s7Index["runs"]!.AsArray().Count == 2, "the destination index switches atomically to the current run with its validated closure");
// retry after failure covers the same closure and keeps the old index on error
var s7Dest2 = Path.Combine(root, "publish-dest2");
Directory.CreateDirectory(s7Dest2);
WebProgressPublishing.PublishClosure(dataRoot, ["run-1"], "run-1", s7Dest2);
var s7IndexBefore = File.ReadAllText(Path.Combine(s7Dest2, "index.json"));
var s7BadRoot = Path.Combine(root, "publish-bad");
WriteRun(s7BadRoot, "run-x", "rev-0", true, Entry("blend.alpha", "pass", "cases/blend.alpha/sample-0/meshes/m.json", "cases/blend.alpha/sample-0/oracle.log"));
var xManifest = JsonNode.Parse(File.ReadAllText(Path.Combine(s7BadRoot, "runs", "run-x", "manifest.json")))!;
xManifest["cases"]![0]!["evidence"]!.AsArray().Add((JsonNode)new JsonObject { ["label"] = "dangling", ["path"] = "../run-missing/cases/x/oracle.log" });
File.WriteAllText(Path.Combine(s7BadRoot, "runs", "run-x", "manifest.json"), xManifest.ToJsonString());
WriteIndex(s7BadRoot, "run-x");
var s7Failed = false;
try { WebProgressPublishing.PublishClosure(s7BadRoot, ["run-x"], "run-x", s7Dest2); } catch (InvalidDataException) { s7Failed = true; }
Assert(s7Failed && File.ReadAllText(Path.Combine(s7Dest2, "index.json")) == s7IndexBefore, "a failed publish keeps the previous destination index untouched");
File.AppendAllText(Path.Combine(s7Dest2, "runs", "run-0", "cases", "blend.alpha", "sample-0", "oracle.log"), "tamper");
var s7Failed2 = false;
try { WebProgressPublishing.PublishClosure(dataRoot, ["run-1"], "run-1", s7Dest2); } catch (IOException) { s7Failed2 = true; }
Assert(s7Failed2 && File.ReadAllText(Path.Combine(s7Dest2, "index.json")) == s7IndexBefore, "immutable evidence tampering fails the publish without touching the index");
// quarantined runs referenced only for failure logs are copied as evidence, never indexed
var s7Root = Path.Combine(root, "publish-evidence");
Directory.CreateDirectory(s7Root);
WriteRun(s7Root, "agg", "rev-0", true, Entry("blend.alpha", "pass", "cases/blend.alpha/sample-0/meshes/m.json", "cases/blend.alpha/sample-0/oracle.log"));
var aggManifest = JsonNode.Parse(File.ReadAllText(Path.Combine(s7Root, "runs", "agg", "manifest.json")))!;
aggManifest["cases"]![0]!["evidence"]!.AsArray().Add((JsonNode)new JsonObject { ["label"] = "failure log", ["path"] = "../run-bad/cases/blend.beta/sample-0/oracle.log" });
File.WriteAllText(Path.Combine(s7Root, "runs", "agg", "manifest.json"), aggManifest.ToJsonString());
Directory.CreateDirectory(Path.Combine(s7Root, "runs", "run-bad", "cases", "blend.beta", "sample-0"));
File.WriteAllText(Path.Combine(s7Root, "runs", "run-bad", "cases", "blend.beta", "sample-0", "oracle.log"), "failure log\n");
File.WriteAllText(Path.Combine(s7Root, "runs", "run-bad", "manifest.json"), "{ quarantined raw evidence");
WriteIndex(s7Root, "agg");
var s7Dest3 = Path.Combine(root, "publish-evidence-dest");
Directory.CreateDirectory(s7Dest3);
WebProgressPublishing.PublishClosure(s7Root, ["agg", "run-bad"], "agg", s7Dest3);
Assert(WebProgressDataValidation.Validate(s7Dest3)["errors"]!.AsArray().Count == 0, "evidence-only closure copies keep the destination self-consistent");
Assert(File.Exists(Path.Combine(s7Dest3, "runs", "run-bad", "cases", "blend.beta", "sample-0", "oracle.log")), "blocked failure logs referenced by the aggregate reach the destination");
Assert(!JsonNode.Parse(File.ReadAllText(Path.Combine(s7Dest3, "index.json")))!["runs"]!.AsArray().Any(r => r!["id"]!.GetValue<string>() == "run-bad"), "a quarantined run never enters the published index");

// ---- 21. symlink/reparse escapes are rejected at every path component ----
var symRoot = Path.Combine(root, "symlink");
Directory.CreateDirectory(Path.Combine(symRoot, "runs", "run-1"));
var outsideSecret = Path.Combine(root, "outside-secret.log");
File.WriteAllText(outsideSecret, "outside public root");
JsonObject SymManifest(string evidencePath)
{
    var entry = Entry("blend.alpha", "missing", "", "");
    entry["evidence"] = new JsonArray((JsonNode)new JsonObject { ["label"] = "escaped", ["path"] = evidencePath });
    return new JsonObject
    {
        ["schemaVersion"] = 1,
        ["run"] = new JsonObject { ["id"] = "run-1", ["commit"] = "rev", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["pkVersion"] = "test", ["host"] = new JsonObject { ["os"] = "test-host" }, ["configuration"] = new JsonObject(), ["scope"] = new JsonObject(), ["provenance"] = new JsonObject() },
        ["cases"] = new JsonArray((JsonNode)entry),
    };
}
void SymCheck(string evidencePath, bool expectValid, string label)
{
    File.WriteAllText(Path.Combine(symRoot, "runs", "run-1", "manifest.json"), SymManifest(evidencePath).ToJsonString());
    WriteIndex(symRoot, "run-1");
    var result = WebProgressDataValidation.Validate(symRoot);
    var errors = result["errors"]!.AsArray().Select(e => e!.GetValue<string>()).ToList();
    Assert((errors.Count == 0) == expectValid, label + ": " + string.Join(" | ", errors));
}
File.CreateSymbolicLink(Path.Combine(symRoot, "runs", "external.log"), Path.GetRelativePath(Path.Combine(symRoot, "runs"), outsideSecret));
SymCheck("../external.log", false, "a file symlink escaping the data root is rejected (review Probe S8 accepted it)");
Directory.CreateSymbolicLink(Path.Combine(symRoot, "runs", "linked"), Path.GetFullPath(root));
SymCheck("../linked/outside-secret.log", false, "a directory symlink escaping the data root is rejected at an intermediate component");
Directory.CreateDirectory(Path.Combine(symRoot, "other"));
File.WriteAllText(Path.Combine(symRoot, "other", "adjacent.log"), "adjacent\n");
SymCheck("../../other/adjacent.log", false, "an adjacent-directory reference beyond the runs tree is rejected");
File.WriteAllText(Path.Combine(symRoot, "runs", "external-real.log"), "inside\n");
SymCheck("../external-real.log", true, "real files inside the runs tree still validate (contract not tightened beyond link escapes)");

Console.WriteLine("ALL PASS " + passed + " scheduling contract checks");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;
