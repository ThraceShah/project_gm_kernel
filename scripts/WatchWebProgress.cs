#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressScheduler=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// An ordinary local runner, not an agent. No service is started by installing it.
// Persistent scheduling driver: every round plans one bounded batch from the
// durable queue (per-case 300s / total 1200s budgets by default, serial PK),
// records only confirmed observations, and publishes the latest accumulated
// aggregate view. Real kernel failures/timeouts are completed results; exporter
// failures keep the item pending with bounded retries. The queue is never
// skipped just because the source fingerprint is unchanged: a round only skips
// when nothing is left to observe or retry.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
Directory.SetCurrentDirectory(root);
var interval = int.Parse(Option("--poll-seconds", "60"));
if (interval < 5) throw new ArgumentException("poll-seconds >= 5 required");
var statePath = Path.GetFullPath(Option("--state", "../temp_docs/web-progress/watch-state.json"), scripts);
Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
var registryPath = Path.GetFullPath(Option("--registry", "web-progress-registry.json"), scripts);
var data = Path.GetFullPath(Option("--output", "../temp_docs/web-progress/data"), scripts);
Directory.CreateDirectory(data);
var publishTo = Array.IndexOf(args, "--publish-data-to") >= 0 ? Path.GetFullPath(Option("--publish-data-to", "../web/data"), scripts) : null;
var once = args.Contains("--once");
var batchSize = int.Parse(Option("--batch-size", "4"));
if (batchSize < 1) throw new ArgumentException("batch-size >= 1 required");
var config = Option("--configuration", "Release");
var samples = Option("--samples", "1");
var seconds = Option("--seconds", "600");
var caseSeconds = Option("--case-seconds", "120");

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
do
{
    using var instanceLock = WebProgressScheduler.InstanceLock.TryAcquire(statePath);
    if (instanceLock == null)
    {
        Console.WriteLine("SCHEDULER_LOCKED another watcher instance holds " + Path.GetFileName(statePath) + "; this tick skipped.");
        if (once) return 0;
    }
    else
    {
        var exit = await RunOnce();
        if (exit != null) return exit.Value;
    }
    if (!once) try { await Task.Delay(TimeSpan.FromSeconds(interval), stop.Token); } catch (OperationCanceledException) { break; }
} while (!once && !stop.IsCancellationRequested);
return 0;

async Task<int?> RunOnce()
{
    var registryText = File.ReadAllText(registryPath);
    var registry = JsonNode.Parse(registryText)!.AsObject();
    var registryHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(registryText)));
    var registryVersion = registry["version"]?.GetValue<string>() ?? "unversioned";
    var revision = Run("git", ["rev-parse", "HEAD"]);
    var workingDiff = Run("git", ["diff", "--no-ext-diff"]);
    var workingDiffHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(workingDiff)));
    var state = File.Exists(statePath) ? WebProgressScheduler.LoadState(statePath) : WebProgressScheduler.NewState(revision, registryHash, registryVersion);
    WebProgressScheduler.RecoverInterrupted(state);
    if (args.Contains("--force"))
    {
        var completed = state["completed"]!.AsObject(); var stale = state["stale"]!.AsObject();
        foreach (var pair in completed.ToList())
        {
            var moved = pair.Value!.DeepClone().AsObject();
            moved["markedAt"] = DateTimeOffset.UtcNow.ToString("O"); moved["reason"] = "forced";
            stale[pair.Key] = moved;
        }
        completed.Clear(); state["blocked"] = new JsonObject();
        foreach (var id in stale) if (!state["pending"]!.AsArray().Any(p => p!["caseId"]!.GetValue<string>() == id.Key)) state["pending"]!.AsArray().Add((JsonNode)new JsonObject { ["caseId"] = id.Key, ["reason"] = "affected", ["attempts"] = 0, ["retryReason"] = null });
        Console.WriteLine("FORCE re-verification requested for all adapted cases.");
    }
    var previousRevision = state["sourceRevision"]?.GetValue<string>();
    var previousWorking = state["workingDiffHash"]?.GetValue<string>();
    var sourceChanged = false;
    if (revision != previousRevision || registryHash != state["registryHash"]?.GetValue<string>() || workingDiffHash != previousWorking)
    {
        sourceChanged = true;
        var committedChanged = revision != previousRevision ? ChangedPaths(previousRevision) : new List<string>();
        var workingChanged = workingDiffHash != previousWorking
            ? Run("git", ["diff", "--name-only"]).Split('\n', StringSplitOptions.RemoveEmptyEntries).Concat(Run("git", ["diff", "--cached", "--name-only"]).Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToList()
            : new List<string>();
        var changed = committedChanged.Concat(workingChanged).Distinct(StringComparer.Ordinal).ToList();
        WebProgressScheduler.MarkChanges(state, changed, registry, revision);
        state["workingDiffHash"] = workingDiffHash;
        Console.WriteLine("SOURCE_CHANGED revision=" + revision[..8] + " paths=" + changed.Count);
    }
    Func<JsonObject, bool> eligible = entry =>
        (Option("--family", "all") is "all" || entry["family"]!.GetValue<string>() == Option("--family", "all"))
        && (Option("--suite", "all") is "all" || entry["suite"]?.GetValue<string>() == Option("--suite", "all"))
        && (Option("--case", "all") is "all" || Option("--case", "all") == "representatives" && entry["representative"]?.GetValue<bool>() == true || Option("--case", "all").Split(',').Contains(entry["id"]!.GetValue<string>(), StringComparer.Ordinal));
    WebProgressScheduler.Reconcile(state, registry, registryHash, registryVersion, eligible);
    var plan = WebProgressScheduler.PlanBatch(state, registry, batchSize);
    if (plan.Count == 0)
    {
        if (state["publish"]!["pending"]?.GetValue<bool>() == true) return FinishPublish(state) ? 0 : 2;
        Console.WriteLine((sourceChanged ? "Changes observed without measurement impact; queue complete; " : "Source and registry unchanged; queue complete; ") + "retained current static data. pending=0 completed=" + state["completed"]!.AsObject().Count + " stale=" + state["stale"]!.AsObject().Count + " blocked=" + state["blocked"]!.AsObject().Count);
        WebProgressScheduler.Save(statePath, state);
        return null;
    }
    var batchIndex = state["batches"]!.AsArray().Count;
    var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + revision[..8] + "-b" + batchIndex.ToString("D3");
    var run = Path.Combine(data, "runs", runId);
    if (Directory.Exists(run)) throw new IOException("Run already exists: choose a new run identity; existing evidence is immutable.");
    Console.WriteLine("BATCH " + runId + " cases=" + string.Join(",", plan));
    var batch = new JsonObject { ["index"] = batchIndex, ["runId"] = runId, ["revision"] = revision, ["startedAt"] = DateTimeOffset.UtcNow.ToString("O"), ["status"] = "running", ["caseIds"] = new JsonArray(plan.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()) };
    WebProgressScheduler.RecordBatch(state, batch);
    WebProgressScheduler.Save(statePath, state);
    var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, WorkingDirectory = root };
    foreach (var arg in new[] { "run", "--file", "scripts/RefreshWebProgress.cs", "--", "--registry", registryPath, "--case", string.Join(",", plan), "--run-id", runId, "--output", data, "--configuration", config, "--samples", samples, "--seconds", seconds, "--case-seconds", caseSeconds }) info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using (var process = Process.Start(info)!)
    {
        try { await process.WaitForExitAsync(stop.Token); }
        catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); Console.WriteLine("BATCH_INTERRUPTED " + runId); return 130; }
    }
    var manifestPath = Path.Combine(run, "manifest.json");
    var manifest = File.Exists(manifestPath) ? JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject() : null;
    var finished = manifest?["run"]?["finishedAt"] != null;
    if (!finished)
    {
        batch["status"] = "interrupted"; batch["finishedAt"] = DateTimeOffset.UtcNow.ToString("O");
        WebProgressScheduler.Save(statePath, state);
        Console.WriteLine("BATCH_INTERRUPTED " + runId + " partial evidence retained; unconfirmed cases stay pending.");
        return null;
    }
    var validation = JsonNode.Parse(File.ReadAllText(Path.Combine(run, "validation.json")))!;
    if (validation["errors"]!.AsArray().Count > 0)
    {
        batch["status"] = "contract-failed"; batch["finishedAt"] = DateTimeOffset.UtcNow.ToString("O"); batch["validationErrors"] = validation["errors"]!.DeepClone();
        foreach (var id in plan)
        {
            var attempted = manifest!["cases"]!.AsArray().OfType<JsonObject>().FirstOrDefault(c => c["id"]!.GetValue<string>() == id)?["execution"]?["attempted"]?.GetValue<bool>() == true;
            if (attempted) WebProgressScheduler.RecordOutcome(state, id, "export-failure", "missing", runId, revision, EntryHashOf(registry, id), batchIndex);
        }
        WebProgressScheduler.Save(statePath, state);
        Console.Error.WriteLine("EXPORTER_CONTRACT_FAILED " + runId + " no case advanced to completed; bounded retries scheduled.");
        return 2;
    }
    var checkVersion = WebProgressScheduler.ComputeCheckVersion(scripts);
    var stateSnapshot = state.ToJsonString();
    foreach (var id in plan)
    {
        var entry = manifest["cases"]!.AsArray().OfType<JsonObject>().First(c => c["id"]!.GetValue<string>() == id);
        var attempted = entry["execution"]?["attempted"]?.GetValue<bool>() == true;
        var status = entry["status"]!.GetValue<string>();
        if (!attempted) continue;
        if (status is "pass" or "fail" or "timeout") { WebProgressScheduler.RecordOutcome(state, id, "completed", status, runId, revision, EntryHashOf(registry, id), batchIndex); Console.WriteLine("RESULT " + id + ": " + status + " completed"); }
        else { WebProgressScheduler.RecordOutcome(state, id, "export-failure", status, runId, revision, EntryHashOf(registry, id), batchIndex); Console.WriteLine("RESULT " + id + ": exporter failure, retry scheduled"); }
    }
    batch["status"] = "complete"; batch["finishedAt"] = DateTimeOffset.UtcNow.ToString("O");
    state["lastRunId"] = runId; state["lastBatchIndex"] = batchIndex;
    state["publish"]!["pending"] = true;
    Dictionary<string, JsonObject?> runCache = new(StringComparer.Ordinal);
    Func<string, JsonObject?> loadRun = id =>
    {
        if (runCache.TryGetValue(id, out var cached)) return cached;
        JsonObject? value = null;
        foreach (var name in new[] { "collection-manifest.json", "manifest.json" })
        {
            var path = Path.Combine(data, "runs", id, name);
            if (File.Exists(path)) { value = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); break; }
        }
        runCache[id] = value;
        return value;
    };
    var merged = WebProgressScheduler.BuildAggregate(manifest!, registry, state, data, runId, revision, checkVersion, batchIndex, loadRun);
    File.WriteAllText(Path.Combine(run, "collection-manifest.json"), manifest!.ToJsonString());
    File.WriteAllText(Path.Combine(run, "manifest.json.tmp"), merged.ToJsonString());
    File.Move(Path.Combine(run, "manifest.json.tmp"), manifestPath, true);
    var mergedValidation = WebProgressDataValidation.Validate(data);
    if (mergedValidation["errors"]!.AsArray().Count > 0)
    {
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        state = JsonNode.Parse(stateSnapshot)!.AsObject();
        var failedBatch = state["batches"]!.AsArray().OfType<JsonObject>().First(b => b["runId"]!.GetValue<string>() == runId);
        failedBatch["status"] = "aggregate-invalid"; failedBatch["finishedAt"] = DateTimeOffset.UtcNow.ToString("O"); failedBatch["validationErrors"] = mergedValidation["errors"]!.DeepClone();
        WebProgressScheduler.Save(statePath, state);
        Console.Error.WriteLine("AGGREGATE_INVALID raw manifest restored; no case advanced. " + mergedValidation["errors"]!.ToJsonString());
        return 2;
    }
    WebProgressScheduler.Save(statePath, state);
    var published = FinishPublish(state);
    WebProgressScheduler.Save(statePath, state);
    Console.WriteLine("COUNTS " + string.Join(", ", merged["cases"]!.AsArray().GroupBy(c => c!["caseProgress"]?["classification"]?.GetValue<string>() ?? c!["status"]!.GetValue<string>()).Select(g => g.Key + "=" + g.Count())));
    return published ? null : 2;
}

// Publish the accumulated evidence: every run referenced by the current
// aggregate first, then the aggregate run itself so latestRunId advances. A
// publish failure keeps the validated cache pending and retries on the next
// tick without re-running any oracle case.
bool FinishPublish(JsonObject state)
{
    if (publishTo == null) { state["publish"]!["pending"] = false; return true; }
    try
    {
        var runIds = new List<string>();
        foreach (var map in new[] { state["completed"]!.AsObject(), state["stale"]!.AsObject(), state["blocked"]!.AsObject() })
            foreach (var record in map)
            {
                var id = record.Value?["runId"]?.GetValue<string>() ?? record.Value?["lastRunId"]?.GetValue<string>();
                if (id != null && !runIds.Contains(id, StringComparer.Ordinal) && Directory.Exists(Path.Combine(data, "runs", id))) runIds.Add(id);
            }
        var current = state["lastRunId"]?.GetValue<string>();
        if (current != null && Directory.Exists(Path.Combine(data, "runs", current))) { runIds.Remove(current); runIds.Add(current); }
        foreach (var id in runIds) WebProgressPublishing.Publish(data, id, publishTo);
        state["publish"]!["pending"] = false; state["publish"]!["lastError"] = null;
        Console.WriteLine("PUBLISHED_DATA " + Path.GetRelativePath(root, publishTo) + " runs=" + runIds.Count + " latest=" + current);
        return true;
    }
    catch (Exception e)
    {
        state["publish"]!["pending"] = true; state["publish"]!["lastError"] = e.Message;
        Console.Error.WriteLine("PUBLISH_FAILED " + e.Message + "; validated cache kept, retrying without re-running cases.");
        return false;
    }
}

List<string> ChangedPaths(string? previous)
{
    if (previous == null) return [];
    try
    {
        var names = Run("git", ["diff", "--name-only", previous, "HEAD"]);
        return names.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    catch (IOException)
    {
        Console.Error.WriteLine("SOURCE_HISTORY_UNAVAILABLE conservative invalidation of all adapted results.");
        return ["(history-unavailable)"];
    }
}

string EntryHashOf(JsonObject registry, string caseId)
{
    var entry = registry["cases"]!.AsArray().OfType<JsonObject>().First(c => c["id"]!.GetValue<string>() == caseId);
    return WebProgressScheduler.EntryHash(entry);
}

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
static string Run(string command, string[] arguments)
{
    var info = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    using var p = Process.Start(info)!;
    var output = p.StandardOutput.ReadToEnd();
    var error = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new IOException(command + " failed: " + error);
    return output.Trim();
}
