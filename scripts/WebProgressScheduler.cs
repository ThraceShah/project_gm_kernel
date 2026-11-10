using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    // Load the durable queue state. The legacy watcher's fingerprint state
    // ({fingerprint,updatedAt,commit}) is not a queue: it is preserved as a
    // timestamped backup and the caller safely rebuilds a full queue (an old
    // fingerprint is never treated as measured). Corrupt JSON or unknown
    // schemas fail explicitly with the original file untouched — never
    // silently treated as an empty queue.
    internal static JsonObject? LoadState(string statePath, out string? legacyBackup)
    {
        legacyBackup = null;
        InvalidDataException Bad(string message) => new("STATE_SCHEMA_INVALID " + Path.GetFileName(statePath) + " " + message + "; original file kept untouched at " + statePath);
        if (!WebProgressSafePath.IsSafeChain(statePath, out var safeOffender)) throw Bad("symlink/reparse state path rejected " + safeOffender);
        JsonNode? node;
        try { node = JsonNode.Parse(File.ReadAllText(statePath)); }
        catch (JsonException e) { throw Bad("is not valid JSON (" + e.Message + ")"); }
        if (node is not JsonObject state) throw Bad("is not a JSON object");
        if (state["scheduler"] is null && state["fingerprint"] is JsonValue legacyFingerprint && legacyFingerprint.TryGetValue<string>(out _))
        {
            legacyBackup = statePath + ".legacy-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".bak";
            File.Copy(statePath, legacyBackup, false);
            return null;
        }
        // Typed schema rejection: an unknown or mistyped scheduler/schemaVersion
        // and any container/record/scalar/null mismatch fails explicitly instead
        // of a silent reset or a late cast crash. Real IO errors still propagate.
        static bool Str(JsonNode? value, bool required) => value is JsonValue v && v.TryGetValue<string>(out _) || !required && value is null;
        static bool Num(JsonNode? value, bool required) => value is JsonValue v && v.TryGetValue<int>(out _) || !required && value is null;
        static bool Bool(JsonNode? value, bool required) => value is JsonValue v && v.TryGetValue<bool>(out _) || !required && value is null;
        if (!Str(state["scheduler"], true) || state["scheduler"]!.GetValue<string>() != SchedulerName
            || !Num(state["schemaVersion"], true) || state["schemaVersion"]!.GetValue<int>() != 1)
            throw Bad("has an unknown or mistyped scheduler/schemaVersion");
        foreach (var key in new[] { "sourceRevision", "registryHash", "registryVersion", "updatedAt" })
            if (!Str(state[key], true)) throw Bad("misses or mistypes " + key);
        if (state["publish"] is not JsonObject publish || !Bool(publish["pending"], true) || !Str(publish["lastError"], false) || !Str(publish["lastErrorAt"], false)
            || state["pending"] is not JsonArray pendingArray || state["batches"] is not JsonArray batchArray
            || state["completed"] is not JsonObject completed || state["blocked"] is not JsonObject blocked || state["stale"] is not JsonObject stale)
            throw Bad("has corrupt queue collections");
        if (state["retired"] is not null && state["retired"] is not JsonObject) throw Bad("has corrupt retired collections");
        if (state["quarantined"] is not null)
        {
            if (state["quarantined"] is not JsonObject qObj) throw Bad("has corrupt quarantined collections");
            foreach (var pair in qObj)
                if (pair.Value is not JsonObject qEntry || !Str(qEntry["runId"], false) || !Str(qEntry["reason"], false) || !Str(qEntry["quarantinedAt"], false) || (qEntry["errors"] is not null && (qEntry["errors"] is not JsonArray qErrors || qErrors.Any(e => !Str(e, true)))))
                    throw Bad("has corrupt quarantined entries");
        }
        if (state["workingPaths"] is not null)
        {
            if (state["workingPaths"] is not JsonArray wpArray) throw Bad("has corrupt workingPaths");
            foreach (var item in wpArray)
                if (!Str(item, true)) throw Bad("has non-string workingPaths elements");
        }
        if (state["workingDiffHash"] is not null && !Str(state["workingDiffHash"], false)) throw Bad("has corrupt workingDiffHash");
        if (state["lastRunId"] is not null && !Str(state["lastRunId"], false)) throw Bad("has corrupt lastRunId");
        foreach (var item in pendingArray)
            if (item is not JsonObject entry || !Str(entry["caseId"], true) || !Str(entry["reason"], true) || !Num(entry["attempts"], false) || !Str(entry["retryReason"], false) || !Str(entry["lastRunId"], false) || !Str(entry["entryHash"], false))
                throw Bad("has corrupt pending entries");
        foreach (var item in batchArray)
            if (item is not JsonObject batch || !Str(batch["runId"], true) || !Str(batch["status"], true) || !Num(batch["exitCode"], false)
                || (batch["cases"] is not null && (batch["cases"] is not JsonArray bCases || bCases.Any(c => !Str(c, true)))))
                throw Bad("has corrupt batch entries");
        foreach (var pair in completed)
            if (pair.Value is not JsonObject done || !Str(done["entryHash"], true) || !Str(done["runId"], true) || !Str(done["revision"], false) || !Str(done["status"], false) || !Num(done["attempts"], false) || !Num(done["batchIndex"], false) || !Str(done["completedAt"], false) || !Str(done["checkVersion"], false))
                throw Bad("has corrupt completed records");
        foreach (var pair in blocked)
            if (pair.Value is not JsonObject bad1 || !Str(bad1["entryHash"], true) || !Num(bad1["attempts"], true) || !Str(bad1["retryReason"], true) || !Str(bad1["lastRunId"], false) || !Str(bad1["blockedAt"], false))
                throw Bad("has corrupt blocked records");
        foreach (var pair in stale)
            if (pair.Value is not JsonObject old || !Str(old["entryHash"], true) || !Str(old["runId"], true) || !Str(old["revision"], false) || !Str(old["status"], false) || !Str(old["markedAt"], false) || !Str(old["reason"], false))
                throw Bad("has corrupt stale records");
        if (state["retired"] is JsonObject retObj)
        {
            foreach (var pair in retObj)
            {
                if (pair.Value is not JsonObject tomb || !Str(tomb["retiredAt"], false) || !Str(tomb["retireReason"], false)
                    || !Str(tomb["invalidateReason"], false) || !Str(tomb["invalidateRevision"], false) || !Str(tomb["invalidatedAt"], false)
                    || tomb["records"] is not JsonArray recArray)
                    throw Bad("has corrupt retired records");
                foreach (var rec in recArray)
                {
                    if (rec is not JsonObject r || !Str(r["kind"], true) || r["record"] is not JsonObject rObj)
                        throw Bad("has corrupt retired record entries");
                    var rKind = r["kind"]!.GetValue<string>();
                    if (rKind == "pending" && (!Str(rObj["caseId"], true) || !Str(rObj["reason"], true) || !Num(rObj["attempts"], false) || !Str(rObj["retryReason"], false) || !Str(rObj["lastRunId"], false) || !Str(rObj["entryHash"], false)))
                        throw Bad("has corrupt retired pending record");
                    if (rKind == "blocked" && (!Str(rObj["entryHash"], true) || !Num(rObj["attempts"], true) || !Str(rObj["retryReason"], true) || !Str(rObj["lastRunId"], false) || !Str(rObj["blockedAt"], false)))
                        throw Bad("has corrupt retired blocked record");
                    if (rKind == "stale" && (!Str(rObj["entryHash"], true) || !Str(rObj["runId"], true) || !Str(rObj["revision"], false) || !Str(rObj["status"], false) || !Str(rObj["markedAt"], false) || !Str(rObj["reason"], false)))
                        throw Bad("has corrupt retired stale record");
                }
            }
        }
        return state;
    }

    internal static void Save(string statePath, JsonObject state)
    {
        if (!WebProgressSafePath.IsSafeChain(statePath, out var offending))
            throw new IOException("Symlinks are not static data: " + offending);
        state["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        File.WriteAllText(statePath + ".tmp", state.ToJsonString());
        File.Move(statePath + ".tmp", statePath, true);
    }

    // Registry reconcile: adapted (meshAdapter) cases that pass the caller's
    // selection are the queue unit. New recipes (e.g. another worker extending
    // the registry) are recognized and queued automatically; changed recipe
    // definitions invalidate their old observation; unadapted registrations
    // are never queued. Records that leave the active adapted/selection set
    // (registry removal, adapter withdrawal, narrowed --case/--suite/--family)
    // are cleaned from the active maps into a retired history archive, so
    // planning can never execute an unselected case and an unadapted id can
    // never break planning. Old valid observations (completed) are never
    // deleted; when a case becomes active again its retired pending/blocked/
    // stale records are restored so unfinished work is re-queued accurately.
    internal static void Reconcile(JsonObject state, JsonObject registry, string registryHash, string registryVersion, Func<JsonObject, bool>? eligible = null)
    {
        state["registryHash"] = registryHash;
        state["registryVersion"] = registryVersion;
        var pending = state["pending"]!.AsArray();
        var completed = state["completed"]!.AsObject();
        var blocked = state["blocked"]!.AsObject();
        var stale = state["stale"]!.AsObject();
        var retired = (state["retired"] ??= new JsonObject()).AsObject();
        var entries = registry["cases"]!.AsArray().OfType<JsonObject>().ToList();
        var known = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var id = entry["id"]!.GetValue<string>();
            known.Add(id);
            if (entry["meshAdapter"]!.GetValue<bool>() == true && (eligible == null || eligible(entry))) active.Add(id);
        }
        string RetireReason(string id) => !known.Contains(id) ? "removed" : active.Contains(id) ? "ineligible" : "unadapted";
        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var item = pending[i]!.AsObject();
            var id = item["caseId"]!.GetValue<string>();
            if (!active.Contains(id)) { Retire(retired, id, "pending", item, RetireReason(id)); pending.RemoveAt(i); }
        }
        foreach (var pair in blocked.ToList())
            if (!active.Contains(pair.Key)) { Retire(retired, pair.Key, "blocked", pair.Value!.AsObject(), RetireReason(pair.Key)); blocked.Remove(pair.Key); }
        foreach (var pair in stale.ToList())
            if (!active.Contains(pair.Key)) { Retire(retired, pair.Key, "stale", pair.Value!.AsObject(), RetireReason(pair.Key)); stale.Remove(pair.Key); }
        foreach (var pair in retired.ToList())
            if (active.Contains(pair.Key) && !completed.ContainsKey(pair.Key)) Restore(state, retired, pair.Key);
        var pendingIds = PendingIds(state);
        var firstEnqueue = state["batches"]!.AsArray().Count == 0 && completed.Count == 0;
        foreach (var entry in entries)
        {
            var id = entry["id"]!.GetValue<string>();
            var hash = EntryHash(entry);
            if (retired[id] is JsonObject tomb)
            {
                foreach (var rec in tomb["records"]!.AsArray().OfType<JsonObject>())
                {
                    if (rec["kind"]?.GetValue<string>() == "pending" && rec["record"] is JsonObject pendingRec)
                    {
                        var oldHash = pendingRec["entryHash"]?.GetValue<string>();
                        if (oldHash != null && oldHash != hash)
                        {
                            pendingRec["reason"] = "recipe-change";
                            pendingRec["attempts"] = 0;
                            pendingRec["entryHash"] = hash;
                        }
                        else if (oldHash == null)
                        {
                            pendingRec["entryHash"] = hash;
                        }
                    }
                }
            }
            if (!active.Contains(id)) continue;
            // Explicit transition priority: recipe changes act on each record
            // independently so stale display can never swallow a blocked park
            // (stale+blocked must still unblock and re-queue on recipe change).
            if (completed[id] is JsonObject record && record["entryHash"]!.GetValue<string>() != hash)
            {
                var moved = record.DeepClone().AsObject();
                moved["entryHash"] = hash;
                moved["markedAt"] = DateTimeOffset.UtcNow.ToString("O");
                moved["reason"] = "recipe-change";
                stale[id] = moved;
                completed.Remove(id);
                if (pendingIds.Add(id)) pending.Add(Pending(id, "recipe-change", hash));
            }
            if (blocked[id] is JsonObject bad && bad["entryHash"]!.GetValue<string>() != hash)
            {
                blocked.Remove(id);
                if (pendingIds.Add(id))
                {
                    var item = Pending(id, "recipe-change", hash);
                    if (bad["lastRunId"]?.GetValue<string>() is string lastRun) item["lastRunId"] = lastRun;
                    if (bad["retryReason"]?.GetValue<string>() is string retry) item["retryReason"] = retry;
                    pending.Add(item);
                }
            }
            if (stale[id] is JsonObject old && old["entryHash"]!.GetValue<string>() != hash)
            {
                old["entryHash"] = hash;
                var item = pending.OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == id);
                if (item != null)
                {
                    item["reason"] = "recipe-change";
                    item["attempts"] = 0;
                    item["entryHash"] = hash;
                }
            }
            var pendingItem = pending.OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == id);
            if (pendingItem != null)
            {
                var oldHash = pendingItem["entryHash"]?.GetValue<string>();
                if (oldHash != null && oldHash != hash)
                {
                    pendingItem["reason"] = "recipe-change";
                    pendingItem["attempts"] = 0;
                    pendingItem["entryHash"] = hash;
                }
                else if (oldHash == null)
                {
                    pendingItem["entryHash"] = hash;
                }
            }
            if (!completed.ContainsKey(id) && !blocked.ContainsKey(id) && !pendingIds.Contains(id)) pending.Add(Pending(id, firstEnqueue ? "initial" : "new-recipe", hash));
        }
    }

    private static void Retire(JsonObject retired, string id, string kind, JsonObject record, string reason)
    {
        if (retired[id] is not JsonObject tomb)
        {
            tomb = new JsonObject { ["retiredAt"] = DateTimeOffset.UtcNow.ToString("O"), ["retireReason"] = reason, ["records"] = new JsonArray() };
            retired[id] = tomb;
        }
        tomb["records"]!.AsArray().Add((JsonNode)new JsonObject { ["kind"] = kind, ["record"] = record.DeepClone() });
    }

    // Invalidation markers for retired records: a case can sit retired across
    // source/force changes while filtered out. The marker preserves the
    // measurement/attempt invalidation basis during retirement and Restore
    // consumes it to make exactly the decision the active paths would make.
    internal static void MarkRetiredInvalidation(JsonObject state, string reason, string? revision, string? onlyCaseId = null)
    {
        if (state["retired"] is not JsonObject retired) return;
        foreach (var pair in retired)
        {
            if (onlyCaseId != null && pair.Key != onlyCaseId) continue;
            var tomb = pair.Value!.AsObject();
            tomb["invalidateReason"] = reason;
            tomb["invalidateRevision"] = revision;
            tomb["invalidatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        }
    }

    private static void Restore(JsonObject state, JsonObject retired, string id)
    {
        var tomb = retired[id]!.AsObject();
        var pending = state["pending"]!.AsArray();
        var pendingIds = PendingIds(state);
        var blocked = state["blocked"]!.AsObject();
        var stale = state["stale"]!.AsObject();
        var records = tomb["records"]!.AsArray().OfType<JsonObject>().ToList();
        // Blocked and stale restore first so a pending record can never
        // coexist with a parked blocked record.
        foreach (var raw in records)
        {
            var kind = raw["kind"]!.GetValue<string>();
            if (kind == "stale") stale[id] = raw["record"]!.DeepClone().AsObject();
            else if (kind == "blocked") blocked[id] = raw["record"]!.DeepClone().AsObject();
        }
        foreach (var raw in records)
            if (raw["kind"]!.GetValue<string>() == "pending" && !pendingIds.Contains(id) && !blocked.ContainsKey(id)) pending.Add(raw["record"]!.DeepClone());
        // Consume invalidations that arrived while the case was retired. The
        // active paths decide what a source change/force does to a park, and
        // restore makes the same decision: the park is lifted, the case is
        // re-queued with a fresh attempt cycle, and the old failure evidence
        // (lastRunId/retryReason) is carried instead of erased. Recipe changes
        // are consumed by the reconcile chain through the stored entry hashes.
        // Nothing here promotes old state to a current pass.
        if (tomb["invalidateReason"]?.GetValue<string>() is "source-change" or "forced")
        {
            var parked = blocked[id] as JsonObject;
            if (parked != null) blocked.Remove(id);
            var item = pending.OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == id);
            if (item == null)
            {
                item = Pending(id, "affected");
                if (parked?["lastRunId"]?.GetValue<string>() is string lastRun) item["lastRunId"] = lastRun;
                if (parked?["retryReason"]?.GetValue<string>() is string retry) item["retryReason"] = retry;
                pending.Add(item);
            }
            else
            {
                item["reason"] = "affected";
                item["attempts"] = 0;
            }
        }
        retired.Remove(id);
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
            // A case sitting retired (filtered out) must not swallow the
            // invalidation: the marker is preserved on its tombstone and
            // consumed on restore exactly like the active paths decide.
            MarkRetiredInvalidation(state, "source-change", newRevision, id);
            var entryH = EntryHash(entry);
            if (completed[id] is JsonObject record)
            {
                var moved = record.DeepClone().AsObject();
                moved["markedAt"] = now;
                moved["reason"] = "source-change";
                stale[id] = moved;
                completed.Remove(id);
                if (!pendingIds.Contains(id)) pending.Add(Pending(id, "affected", entryH));
            }
            else if (blocked.ContainsKey(id))
            {
                var parked = blocked[id] as JsonObject;
                blocked.Remove(id);
                if (!pendingIds.Contains(id))
                {
                    var item = Pending(id, "affected", entryH);
                    if (parked?["lastRunId"]?.GetValue<string>() is string lastRun) item["lastRunId"] = lastRun;
                    if (parked?["retryReason"]?.GetValue<string>() is string retry) item["retryReason"] = retry;
                    pending.Add(item);
                }
            }
            else if (pendingIds.Contains(id))
            {
                var item = pending.First(p => p!["caseId"]!.GetValue<string>() == id)!;
                if (item["reason"]!.GetValue<string>() is not ("affected" or "recipe-change")) item["reason"] = "affected";
                item["attempts"] = 0;
                item["entryHash"] = entryH;
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
        var pending = state["pending"]!.AsArray().OfType<JsonObject>()
            .Where(p => entries.ContainsKey(p["caseId"]!.GetValue<string>())).ToArray();
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

    internal static void RecordOutcome(JsonObject state, string caseId, string kind, string? status, string runId, string revision, string entryHash, int batchIndex, string? checkVersion = null)
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
                    // The real check version is persisted at measurement
                    // confirmation so later carry-forward/stale display can
                    // report provenance without guessing.
                    ["checkVersion"] = checkVersion,
                };
                break;
            case "export-failure":
                if (item == null) break;
                item["attempts"] = attempts;
                item["retryReason"] = "exporter-compile-or-contract-failure";
                item["lastRunId"] = runId;
                item["entryHash"] = entryHash;
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

    // Isolate a bad or unconfirmed run: its directory and raw evidence are kept
    // exactly as-is, but it leaves the validated/published index so one broken
    // historical manifest can never poison later batches. Completed
    // observations that relied on the quarantined evidence are demoted to
    // stale (history retained) and re-queued; aggregates never reference the
    // quarantined run's meshes/manifests.
    internal static void QuarantineRun(JsonObject state, string dataRoot, string runId, string reason, JsonArray errors)
    {
        var quarantined = (state["quarantined"] ??= new JsonObject()).AsObject();
        quarantined[runId] = new JsonObject { ["reason"] = reason, ["errors"] = errors.DeepClone(), ["quarantinedAt"] = DateTimeOffset.UtcNow.ToString("O") };
        var indexPath = Path.Combine(dataRoot, "index.json");
        if (File.Exists(indexPath))
        {
            var index = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
            var runs = index["runs"]!.AsArray();
            for (var i = runs.Count - 1; i >= 0; i--)
                if (runs[i]!["id"]!.GetValue<string>() == runId) runs.RemoveAt(i);
            if (index["latestRunId"]?.GetValue<string>() == runId && runs.Count > 0) index["latestRunId"] = runs[0]!["id"]!.DeepClone();
            File.WriteAllText(indexPath + ".tmp", index.ToJsonString());
            File.Move(indexPath + ".tmp", indexPath, true);
        }
        var completed = state["completed"]!.AsObject();
        var stale = state["stale"]!.AsObject();
        var pending = state["pending"]!.AsArray();
        var pendingIds = PendingIds(state);
        foreach (var pair in completed.ToList())
        {
            if (pair.Value?["runId"]?.GetValue<string>() != runId) continue;
            var moved = pair.Value!.DeepClone().AsObject();
            moved["markedAt"] = DateTimeOffset.UtcNow.ToString("O");
            moved["reason"] = "evidence-quarantined";
            stale[pair.Key] = moved;
            completed.Remove(pair.Key);
            if (!pendingIds.Contains(pair.Key)) pending.Add(Pending(pair.Key, "evidence-quarantined"));
        }
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
        var quarantined = (state["quarantined"] ?? new JsonObject()).AsObject();
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
                built["measurement"] = Measurement(sourceRunId, record, loadRun);
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
                // A quarantined source run's meshes/manifests must never be
                // referenced from an aggregate: the display degrades to the
                // placeholder entry plus stale history metadata and a note.
                var quarantinedSource = sourceRunId != null && quarantined.ContainsKey(sourceRunId);
                var built = sourceRunId != null && !quarantinedSource ? BuildEntry(sourceRunId) : baseEntry;
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
                    built["measurement"] = Measurement(sourceRunId!, previous!, loadRun);
                    built["measurement"]!["freshness"] = "stale";
                    built["measurement"]!["carriedForward"] = sourceRunId != runId;
                    var revisionText = previous!["revision"]?.GetValue<string>() ?? "unknown";
                    Note(built, "过时结果：本条为 " + Short(revisionText) + "（run " + sourceRunId + "）的真实测量，源/配方变更影响本 case，等待重验；当前状态不是通过，网格/检查仅作 stale 历史展示。");
                    if (quarantinedSource) Note(built, "证据隔离：历史测量 run " + sourceRunId + " 因契约失败已隔离（原始证据留存但不进入发布索引），不展示其网格/检查，仅保留历史元数据；本条等待重验。");
                }
                if (isBlocked)
                {
                    built["caseProgress"] = new JsonObject { ["classification"] = "export-blocked", ["pending"] = true, ["attempts"] = bad!["attempts"]?.DeepClone(), ["retryReason"] = bad["retryReason"]?.DeepClone() };
                    Note(built, "导出器编译/契约失败，未推进完成：" + bad["retryReason"]!.GetValue<string>() + "；已有限重试 " + bad["attempts"]!.GetValue<int>() + " 次并暂停排队（不阻塞其余 case），原始日志与旧有效展示保留，源变更或 --force 后重新排队。");
                    if (bad["lastRunId"]?.GetValue<string>() is string lastRun && File.Exists(Path.Combine(dataRoot, "runs", lastRun, "cases", id, "sample-0", "oracle.log")))
                        (built["evidence"] ??= new JsonArray()).AsArray().Add((JsonNode)new JsonObject { ["label"] = "导出失败原始日志（有限重试记录）", ["path"] = "../" + lastRun + "/cases/" + id + "/sample-0/oracle.log" });
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

    // Working-tree change fingerprint and impact paths. Both derive from the
    // same two diff scopes (unstaged + staged, rename detection off so a rename
    // maps both names) so the content fingerprint and the path impact mapping
    // can never disagree; reverse changes (revert, rename/delete undo) are
    // caught through the union of the previously and currently dirty paths.
    internal static (string Hash, List<string> Paths) WorkingState(string unstagedDiff, string stagedDiff, string unstagedNames, string stagedNames)
    {
        var hash = Hash(unstagedDiff + "\n\u0000staged\u0000\n" + stagedDiff);
        var paths = Split(unstagedNames).Concat(Split(stagedNames)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return (hash, paths);
        static IEnumerable<string> Split(string names) => names.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    internal static List<string> MergeChangePaths(IEnumerable<string> committed, IEnumerable<string> previousWorking, IEnumerable<string> currentWorking)
        => committed.Concat(previousWorking).Concat(currentWorking).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static HashSet<string> PendingIds(JsonObject state) => state["pending"]!.AsArray().Select(p => p!["caseId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    private static JsonObject Pending(string caseId, string reason, string? entryHash = null) => new()
    {
        ["caseId"] = caseId,
        ["reason"] = reason,
        ["attempts"] = 0,
        ["retryReason"] = null,
        ["lastRunId"] = null,
        ["entryHash"] = entryHash,
    };

    private static string ClassifyPath(string path, HashSet<string> caseSources)
    {
        if (path.StartsWith("docs/", StringComparison.Ordinal) || path.StartsWith("web/", StringComparison.Ordinal)) return "none";
        if (path == "scripts/web-progress-registry.json") return "registry";
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

    private static JsonObject Measurement(string sourceRunId, JsonObject record, Func<string, JsonObject?> loadRun)
    {
        var run = loadRun(sourceRunId);
        // Provenance is never backfilled from the current round: the version
        // recorded at confirmation wins, then the source run's own manifest;
        // untraceable history is explicitly unknown.
        var measuredCheck = record["checkVersion"]?.GetValue<string>()
            ?? run?["run"]?["provenance"]?["checkVersion"]?.GetValue<string>()
            ?? "unknown";
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
