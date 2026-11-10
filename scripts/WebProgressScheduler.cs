using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Persistent web-progress scheduling core. Pure data logic over the existing
// registry (read-only: source / meshAdapter / representative are consumed as
// registered, cases are never renamed or deleted here): a durable per-case
// queue, source-change impact mapping with conservative invalidation, bounded
// retries for exporter failures and an accumulating aggregate manifest that
// keeps every measured case's real run/host/commit/checkVersion. No process
// spawning and no git here: the caller supplies revisions and changed paths so
// controlled tests can drive every transition with small fixture records.
internal static class WebProgressScheduler
{
    internal const string SchedulerName = "web-progress-scheduler-v1";
    internal const int MaxAttempts = 3;

    internal sealed class InstanceLock(Stream stream) : IDisposable
    {
        // Single writer per queue state. A second watcher tick exits politely
        // instead of racing the batch in flight.
        internal static InstanceLock? TryAcquire(string statePath)
        {
            try { return new InstanceLock(new FileStream(statePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
            catch (IOException) { return null; }
        }
        public void Dispose() => stream.Dispose();
    }

    internal static JsonObject NewState(string revision, string registryHash, string registryVersion) => new()
    {
        ["schemaVersion"] = 1,
        ["scheduler"] = SchedulerName,
        ["sourceRevision"] = revision,
        ["registryHash"] = registryHash,
        ["registryVersion"] = registryVersion,
        ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        ["pending"] = new JsonArray(),
        ["completed"] = new JsonObject(),
        ["blocked"] = new JsonObject(),
        ["stale"] = new JsonObject(),
        ["batches"] = new JsonArray(),
        ["publish"] = new JsonObject { ["pending"] = false, ["lastError"] = null },
    };

    internal static JsonObject LoadState(string statePath) => JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();

    internal static void Save(string statePath, JsonObject state)
    {
        state["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        File.WriteAllText(statePath + ".tmp", state.ToJsonString());
        File.Move(statePath + ".tmp", statePath, true);
    }

    // Registry reconcile: adapted (meshAdapter) cases are the queue unit. New
    // recipes (e.g. another worker extending the registry) are recognized and
    // queued automatically; changed recipe definitions invalidate their old
    // observation; removed recipes leave the pending queue. Unadapted
    // registrations are never queued.
    internal static void Reconcile(JsonObject state, JsonObject registry, string registryHash, string registryVersion, Func<JsonObject, bool>? eligible = null)
    {
        state["registryHash"] = registryHash;
        state["registryVersion"] = registryVersion;
        var pending = state["pending"]!.AsArray();
        var pendingIds = PendingIds(state);
        var completed = state["completed"]!.AsObject();
        var blocked = state["blocked"]!.AsObject();
        var stale = state["stale"]!.AsObject();
        var known = new HashSet<string>(StringComparer.Ordinal);
        var firstEnqueue = state["batches"]!.AsArray().Count == 0 && completed.Count == 0;
        foreach (var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
        {
            var id = entry["id"]!.GetValue<string>();
            known.Add(id);
            if (entry["meshAdapter"]!.GetValue<bool>() != true || (eligible != null && !eligible(entry))) continue;
            var hash = EntryHash(entry);
            if (completed[id] is JsonObject record && record["entryHash"]!.GetValue<string>() != hash)
            {
                var moved = record.DeepClone().AsObject();
                moved["entryHash"] = hash;
                moved["markedAt"] = DateTimeOffset.UtcNow.ToString("O");
                moved["reason"] = "recipe-change";
                stale[id] = moved;
                completed.Remove(id);
                pending.Add(Pending(id, "recipe-change"));
            }
            else if (stale[id] is JsonObject old && old["entryHash"]!.GetValue<string>() != hash) old["entryHash"] = hash;
            else if (blocked[id] is JsonObject bad && bad["entryHash"]!.GetValue<string>() != hash)
            {
                blocked.Remove(id);
                pending.Add(Pending(id, "recipe-change"));
            }
            else if (!completed.ContainsKey(id) && !blocked.ContainsKey(id) && !pendingIds.Contains(id)) pending.Add(Pending(id, firstEnqueue ? "initial" : "new-recipe"));
        }
        for (var i = pending.Count - 1; i >= 0; i--)
            if (!known.Contains(pending[i]!["caseId"]!.GetValue<string>())) pending.RemoveAt(i);
    }

    // Source-change impact mapping. Paths map to affected cases through the
    // registry case source paths and the sourceCoverage list; generic kernel
    // (src/), PKToy, fixtures and shared measurement host changes invalidate
    // every adapted result; UI/doc-only paths invalidate nothing (results are
    // carried forward, each entry still records its real measurement commit).
    // Anything that cannot be asserted from the coverage information is
    // conservatively treated as affecting everything: nothing is ever skipped
    // into a pass.
    internal static void MarkChanges(JsonObject state, IReadOnlyList<string> changedPaths, JsonObject registry, string newRevision)
    {
        var sourcePaths = registry["cases"]!.AsArray().OfType<JsonObject>()
            .Where(c => c["meshAdapter"]!.GetValue<bool>() == true)
            .Select(c => c["source"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var affected = new HashSet<string>(StringComparer.Ordinal);
        var all = false;
        foreach (var path in changedPaths)
            switch (ClassifyPath(path, sourcePaths))
            {
                case "none": break;
                case "registry": break; // entry-wise handled by Reconcile
                case "all": all = true; break;
                case var source:
                    foreach (var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
                        if (entry["meshAdapter"]!.GetValue<bool>() == true && entry["source"]!.GetValue<string>() == source) affected.Add(entry["id"]!.GetValue<string>());
                    break;
            }
        var pending = state["pending"]!.AsArray();
        var pendingIds = PendingIds(state);
        var completed = state["completed"]!.AsObject();
        var blocked = state["blocked"]!.AsObject();
        var stale = state["stale"]!.AsObject();
        var now = DateTimeOffset.UtcNow.ToString("O");
        foreach (var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
        {
            var id = entry["id"]!.GetValue<string>();
            if (entry["meshAdapter"]!.GetValue<bool>() != true || (!all && !affected.Contains(id))) continue;
            if (completed[id] is JsonObject record)
            {
                var moved = record.DeepClone().AsObject();
                moved["markedAt"] = now;
                moved["reason"] = "source-change";
                stale[id] = moved;
                completed.Remove(id);
                if (!pendingIds.Contains(id)) pending.Add(Pending(id, "affected"));
            }
            else if (blocked.ContainsKey(id))
            {
                blocked.Remove(id);
                if (!pendingIds.Contains(id)) pending.Add(Pending(id, "affected"));
            }
            else if (pendingIds.Contains(id))
            {
                var item = pending.First(p => p!["caseId"]!.GetValue<string>() == id)!;
                if (item["reason"]!.GetValue<string>() is not ("affected" or "recipe-change")) item["reason"] = "affected";
            }
        }
        state["sourceRevision"] = newRevision;
    }

    // Batch planning: source-change affected recipes first (representatives
    // before the rest), then the declared representatives, then the remaining
    // adapted recipes in fair rotation across suites so no suite starves.
    // Completed cases are never planned again; bounded exporter retries join
    // the tail of their group so a broken case cannot starve the queue.
    internal static List<string> PlanBatch(JsonObject state, JsonObject registry, int batchSize)
    {
        var entries = registry["cases"]!.AsArray().OfType<JsonObject>()
            .Where(c => c["meshAdapter"]!.GetValue<bool>() == true)
            .ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
        var pending = state["pending"]!.AsArray().OfType<JsonObject>().ToArray();
        var affected = pending.Where(p => p["reason"]!.GetValue<string>() is "affected" or "recipe-change").Select(p => p["caseId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        List<string> Order(IEnumerable<string> ids)
        {
            var list = new List<string>();
            var reps = ids.Where(id => entries[id]["representative"]?.GetValue<bool>() == true).Order(StringComparer.Ordinal);
            var rest = ids.Where(id => entries[id]["representative"]?.GetValue<bool>() != true).GroupBy(id => entries[id]["suite"]?.GetValue<string>() ?? "", StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
            list.AddRange(reps);
            var queues = rest.Select(g => new Queue<string>(g.Order(StringComparer.Ordinal))).ToArray();
            while (queues.Any(q => q.Count > 0)) foreach (var queue in queues) if (queue.Count > 0) list.Add(queue.Dequeue());
            return list;
        }
        var ordered = Order(pending.Where(p => affected.Contains(p["caseId"]!.GetValue<string>())).Select(p => p["caseId"]!.GetValue<string>()))
            .Concat(Order(pending.Where(p => !affected.Contains(p["caseId"]!.GetValue<string>())).Select(p => p["caseId"]!.GetValue<string>())));
        return ordered.Distinct(StringComparer.Ordinal).Take(batchSize).ToList();
    }

    // Real kernel acceptance failures and timeouts are completed observations.
    // Only an attempted case without a structured capture (exporter compile or
    // contract failure) stays unfinished: bounded retry, then parked as
    // export-blocked without holding up the rest of the queue.
    internal static List<(string CaseId, string Kind, string? Status)> ClassifyOutcomes(JsonObject manifest, JsonArray batchIds)
    {
        var outcomes = new List<(string, string, string?)>();
        var byId = manifest["cases"]!.AsArray().OfType<JsonObject>().ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
        foreach (var raw in batchIds)
        {
            var id = raw!.GetValue<string>();
            if (!byId.TryGetValue(id, out var entry)) { outcomes.Add((id, "not-attempted", null)); continue; }
            var attempted = entry["execution"]?["attempted"]?.GetValue<bool>() == true;
            var status = entry["status"]!.GetValue<string>();
            if (!attempted) outcomes.Add((id, "not-attempted", null));
            else if (status is "pass" or "fail" or "timeout") outcomes.Add((id, "completed", status));
            else outcomes.Add((id, "export-failure", status));
        }
        return outcomes;
    }

    internal static void RecordOutcome(JsonObject state, string caseId, string kind, string? status, string runId, string revision, string entryHash, int batchIndex)
    {
        var pending = state["pending"]!.AsArray();
        var completed = state["completed"]!.AsObject();
        var blocked = state["blocked"]!.AsObject();
        var stale = state["stale"]!.AsObject();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var item = pending.OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == caseId);
        var attempts = (item?["attempts"]?.GetValue<int>() ?? 0) + 1;
        switch (kind)
        {
            case "completed":
                if (item != null) pending.Remove(item);
                stale.Remove(caseId);
                completed[caseId] = new JsonObject
                {
                    ["runId"] = runId, ["revision"] = revision, ["status"] = status, ["attempts"] = attempts,
                    ["completedAt"] = now, ["batchIndex"] = batchIndex, ["entryHash"] = entryHash,
                };
                break;
            case "export-failure":
                if (item == null) break;
                item["attempts"] = attempts;
                item["retryReason"] = "exporter-compile-or-contract-failure";
                item["lastRunId"] = runId;
                if (attempts >= MaxAttempts)
                {
                    pending.Remove(item);
                    blocked[caseId] = new JsonObject
                    {
                        ["attempts"] = attempts, ["retryReason"] = "exporter-compile-or-contract-failure",
                        ["lastRunId"] = runId, ["entryHash"] = entryHash, ["blockedAt"] = now,
                    };
                }
                break;
        }
    }

    internal static void RecordBatch(JsonObject state, JsonObject batch)
    {
        var batches = state["batches"]!.AsArray();
        var existing = batches.OfType<JsonObject>().FirstOrDefault(b => b["runId"]!.GetValue<string>() == batch["runId"]!.GetValue<string>());
        if (existing != null) batches.Remove(existing);
        batches.Add(batch);
        while (batches.Count > 200) batches.RemoveAt(0);
    }

    // A batch still marked running when the state is loaded belongs to an
    // interrupted process: its cases were never confirmed and stay pending
    // (never promoted to pass), while the partial run directory is retained as
    // evidence. Confirmed completed records survive restarts untouched.
    internal static void RecoverInterrupted(JsonObject state)
    {
        foreach (var batch in state["batches"]!.AsArray().OfType<JsonObject>())
            if (batch["status"]!.GetValue<string>() == "running") batch["status"] = "interrupted";
    }

    // Aggregate manifest for the current run. The raw collection manifest is
    // the base; cases confirmed at the current validity are accumulated with
    // their real measurement run (paths rewritten to that run), stale results
    // keep their previous display but are explicitly marked stale and never
    // carry a current pass, and unrun/unadapted/export-blocked stay distinct.
    internal static JsonObject BuildAggregate(JsonObject raw, JsonObject registry, JsonObject state, string dataRoot, string runId, string revision, string checkVersion, int batchIndex, Func<string, JsonObject?> loadRun)
    {
        var merged = raw.DeepClone().AsObject();
        var cases = merged["cases"]!.AsArray();
        var completed = state["completed"]!.AsObject();
        var staleRecords = state["stale"]!.AsObject();
        var blocked = state["blocked"]!.AsObject();
        var pendingIds = PendingIds(state);
        var adapted = registry["cases"]!.AsArray().OfType<JsonObject>().Where(c => c["meshAdapter"]!.GetValue<bool>() == true).Select(c => c["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var unadapted = 0; var staleCount = 0; var blockedCount = 0; var carried = 0; var measured = 0; var notRun = 0;
        for (var i = 0; i < cases.Count; i++)
        {
            var entry = cases[i]!.AsObject();
            var id = entry["id"]!.GetValue<string>();
            var baseEntry = entry.DeepClone().AsObject();
            JsonObject BuildEntry(string sourceRunId)
            {
                if (sourceRunId == runId) return baseEntry;
                var run = loadRun(sourceRunId);
                var sourceEntry = run?["cases"]!.AsArray().OfType<JsonObject>().FirstOrDefault(c => c["id"]!.GetValue<string>() == id);
                if (sourceEntry == null) return baseEntry;
                var copy = sourceEntry.DeepClone().AsObject();
                RewritePaths(copy, "../" + sourceRunId + "/");
                return copy;
            }
            if (completed[id] is JsonObject record)
            {
                var sourceRunId = record["runId"]!.GetValue<string>();
                var fresh = sourceRunId == runId;
                var built = BuildEntry(sourceRunId);
                built["measurement"] = Measurement(sourceRunId, record, loadRun, checkVersion);
                built["measurement"]!["freshness"] = "current";
                built["measurement"]!["carriedForward"] = !fresh;
                built["caseProgress"] = new JsonObject { ["classification"] = "current", ["attempts"] = record["attempts"]?.DeepClone(), ["batchIndex"] = record["batchIndex"]?.DeepClone() };
                cases[i] = built;
                if (fresh) measured++; else carried++;
            }
            else if (staleRecords[id] is not null || blocked[id] is not null)
            {
                var previous = staleRecords[id] as JsonObject;
                var bad = blocked[id] as JsonObject;
                var isBlocked = bad != null;
                var fromStale = previous != null;
                var sourceRunId = previous?["runId"]?.GetValue<string>();
                var built = sourceRunId != null ? BuildEntry(sourceRunId) : baseEntry;
                if (fromStale)
                {
                    built["status"] = "missing";
                    var perf = built["performance"]!.AsObject();
                    var oldRatio = perf["ratioGmOverPk"]?.DeepClone();
                    perf["ratioGmOverPk"] = null;
                    perf["note"] = (perf["note"]?.GetValue<string>() ?? "") + " Ratio withheld: the measurement is stale at the current revision and is not a current acceptance pass.";
                    built["stale"] = new JsonObject
                    {
                        ["previousStatus"] = previous!["status"]?.DeepClone(), ["previousRunId"] = previous["runId"]?.DeepClone(),
                        ["previousRevision"] = previous["revision"]?.DeepClone(), ["previousRatioGmOverPk"] = oldRatio,
                        ["previousCompletedAt"] = previous["completedAt"]?.DeepClone(), ["markedAt"] = previous["markedAt"]?.DeepClone(),
                        ["reason"] = previous["reason"]?.DeepClone(),
                    };
                    built["measurement"] = Measurement(sourceRunId!, previous!, loadRun, checkVersion);
                    built["measurement"]!["freshness"] = "stale";
                    built["measurement"]!["carriedForward"] = sourceRunId != runId;
                    var revisionText = previous!["revision"]?.GetValue<string>() ?? "unknown";
                    Note(built, "过时结果：本条为 " + Short(revisionText) + "（run " + sourceRunId + "）的真实测量，源/配方变更影响本 case，等待重验；当前状态不是通过，网格/检查仅作 stale 历史展示。");
                }
                if (isBlocked)
                {
                    built["caseProgress"] = new JsonObject { ["classification"] = "export-blocked", ["pending"] = true, ["attempts"] = bad!["attempts"]?.DeepClone(), ["retryReason"] = bad["retryReason"]?.DeepClone() };
                    Note(built, "导出器编译/契约失败，未推进完成：" + bad["retryReason"]!.GetValue<string>() + "；已有限重试 " + bad["attempts"]!.GetValue<int>() + " 次并暂停排队（不阻塞其余 case），原始日志与旧有效展示保留，源变更或 --force 后重新排队。");
                    if (bad["lastRunId"]?.GetValue<string>() is string lastRun) (built["evidence"] ??= new JsonArray()).AsArray().Add((JsonNode)new JsonObject { ["label"] = "导出失败原始日志（有限重试记录）", ["path"] = "../" + lastRun + "/cases/" + id + "/sample-0/oracle.log" });
                    blockedCount++;
                }
                else
                {
                    built["caseProgress"] = new JsonObject { ["classification"] = "stale", ["pending"] = true, ["attempts"] = 0, ["retryReason"] = null };
                    staleCount++;
                }
                cases[i] = built;
            }
            else if (!adapted.Contains(id))
            {
                entry["caseProgress"] = new JsonObject { ["classification"] = "export-unadapted", ["pending"] = false };
                unadapted++;
            }
            else
            {
                var status = entry["status"]!.GetValue<string>();
                if (status is "pass" or "fail" or "timeout")
                {
                    entry["measurement"] = new JsonObject
                    {
                        ["runId"] = runId, ["revision"] = revision, ["checkVersion"] = checkVersion,
                        ["measuredAt"] = DateTimeOffset.UtcNow.ToString("O"), ["batchIndex"] = batchIndex,
                        ["freshness"] = "current", ["carriedForward"] = false,
                        ["host"] = merged["run"]!["host"]?.DeepClone(),
                    };
                    entry["caseProgress"] = new JsonObject { ["classification"] = "current", ["attempts"] = 1, ["batchIndex"] = batchIndex };
                    measured++;
                }
                else
                {
                    var item = state["pending"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == id);
                    var attempts = item?["attempts"]?.GetValue<int>() ?? 0;
                    entry["caseProgress"] = new JsonObject
                    {
                        ["classification"] = attempts > 0 ? "export-retry" : "not-run", ["pending"] = pendingIds.Contains(id),
                        ["attempts"] = attempts, ["retryReason"] = item?["retryReason"]?.DeepClone(),
                    };
                    if (attempts > 0) Note(entry, "导出器编译/契约失败，未推进完成；有限重试中（" + attempts + "/" + MaxAttempts + "），原始日志与旧有效展示保留。");
                    notRun++;
                }
            }
        }
        var run = merged["run"]!.AsObject();
        run["provenance"]!["checkVersion"] = checkVersion;
        run["provenance"]!["freshnessDefinitions"] = new JsonObject
        {
            ["current"] = "已在当前几何/源有效性下真实测量（同 SHA 累计或未受影响的精确可追溯携带）。",
            ["stale"] = "旧 SHA 的真实历史测量；相关源已变更、待重验，不得当作当前通过。",
            ["not-run"] = "尚未运行；绝不默认为通过。",
            ["export-retry"] = "导出器编译/契约失败，未推进完成；有限重试中，保留日志与旧展示。",
            ["export-unadapted"] = "web 导出适配未实现，与真实内核能力无关。",
            ["export-blocked"] = "导出器编译/契约失败，有限重试后暂停排队，保留日志与旧展示。",
        };
        run["accumulation"] = new JsonObject
        {
            ["scheduler"] = SchedulerName, ["batchIndex"] = batchIndex, ["currentRevision"] = revision,
            ["registryHash"] = state["registryHash"]!.DeepClone(), ["registryVersion"] = state["registryVersion"]!.DeepClone(),
            ["maxAttempts"] = MaxAttempts, ["measuredThisRun"] = measured, ["carriedForward"] = carried,
            ["stale"] = staleCount, ["exportBlocked"] = blockedCount, ["notRun"] = notRun, ["exportUnadapted"] = unadapted,
        };
        return merged;
    }

    internal static string ComputeCheckVersion(string scriptsDirectory)
    {
        var builder = new StringBuilder();
        foreach (var name in new[] { "WebProgressCapture.cs", "ParasolidBodyCompare.cs" })
        {
            var path = Path.Combine(scriptsDirectory, name);
            builder.Append(name).Append('\n').Append(File.Exists(path) ? Hash(File.ReadAllText(path)) : "absent").Append('\n');
        }
        return Hash(builder.ToString());
    }

    // Measurement-affecting registry fields only: selection flags
    // (representative) and labels do not invalidate an observation.
    internal static string EntryHash(JsonObject entry)
    {
        var builder = new StringBuilder();
        foreach (var key in new[] { "id", "source", "operation", "family", "api", "selector", "geometryAuthor", "expectedStepCount" })
            builder.Append(key).Append('=').Append(entry[key]?.ToJsonString() ?? "null").Append('\n');
        builder.Append("arguments=").Append(entry["arguments"]?.ToJsonString() ?? "null").Append('\n');
        builder.Append("parameters=").Append(entry["parameters"]?.ToJsonString() ?? "null").Append('\n');
        return Hash(builder.ToString());
    }

    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static HashSet<string> PendingIds(JsonObject state) => state["pending"]!.AsArray().Select(p => p!["caseId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    private static JsonObject Pending(string caseId, string reason) => new() { ["caseId"] = caseId, ["reason"] = reason, ["attempts"] = 0, ["retryReason"] = null };

    private static string ClassifyPath(string path, HashSet<string> caseSources)
    {
        if (path.StartsWith("docs/", StringComparison.Ordinal) || path.StartsWith("web/", StringComparison.Ordinal)) return "none";
        if (path.StartsWith("scripts/web-progress-registry.json", StringComparison.Ordinal)) return "registry";
        if (!path.Contains('/') && (path.EndsWith(".md", StringComparison.Ordinal) || path == "LICENSE")) return "none";
        if (path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("third_party/", StringComparison.Ordinal)
            || path.StartsWith("tests/KernelTests/Fixtures/", StringComparison.Ordinal) || path == "download-parasolid.sh"
            || path is "scripts/ParasolidScriptHost.cs" or "scripts/ParasolidBodyCompare.cs" or "scripts/WebProgressCapture.cs" or "scripts/Directory.Build.props") return "all";
        return caseSources.Contains(path) ? path : "all";
    }

    private static void RewritePaths(JsonObject entry, string prefix)
    {
        static string Join(string prefix,string text) => text.StartsWith("../",StringComparison.Ordinal) ? text : prefix + text;
        foreach (var step in entry["steps"]?.AsArray() ?? [])
            foreach (var side in new[] { "gm", "pk" })
            {
                var node = step?[side]; if (node == null) continue;
                foreach (var key in new[] { "before", "after" }) if (node[key] is JsonValue value && value.TryGetValue<string>(out var text)) node[key] = Join(prefix,text);
                foreach (var key in new[] { "tools", "afterBodies" })
                    if (node[key] is JsonArray list) for (var i = 0; i < list.Count; i++) if (list[i] is JsonValue value && value.TryGetValue<string>(out var text)) list[i] = Join(prefix,text);
            }
        foreach (var check in entry["checks"]?.AsArray() ?? [])
            if (check?["evidence"] is JsonValue evidence && evidence.TryGetValue<string>(out var text)) check["evidence"] = Join(prefix,text);
        foreach (var item in entry["evidence"]?.AsArray() ?? [])
            if (item?["path"] is JsonValue path && path.TryGetValue<string>(out var text)) item["path"] = Join(prefix,text);
    }

    private static JsonObject Measurement(string sourceRunId, JsonObject record, Func<string, JsonObject?> loadRun, string checkVersion)
    {
        var run = loadRun(sourceRunId);
        var measuredCheck = run?["run"]?["provenance"]?["checkVersion"]?.GetValue<string>() ?? checkVersion;
        return new JsonObject
        {
            ["runId"] = sourceRunId, ["revision"] = record["revision"]?.DeepClone(),
            ["checkVersion"] = measuredCheck, ["host"] = run?["run"]?["host"]?.DeepClone(),
            ["measuredAt"] = (record["completedAt"] ?? record["markedAt"])?.DeepClone(), ["batchIndex"] = record["batchIndex"]?.DeepClone(),
        };
    }

    private static string Short(string revision) => revision.Length > 12 ? revision[..12] : revision;

    private static void Note(JsonObject entry, string note)
    {
        var notes = (entry["notes"] ??= new JsonArray()).AsArray();
        if (!notes.Any(n => n?.GetValue<string>() == note)) notes.Add((JsonNode?)JsonValue.Create(note));
    }
}
