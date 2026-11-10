using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Shared contract helpers for the read-only hosted web progress site (see
// docs/web-progress-hosting.md). Everything here is deliberately independent of
// the scheduler-side validator: the host does not treat a "validated" cache as
// trusted. Activation re-checks the full reference closure of every indexed
// manifest (including cross-run `../<runId>/...` mesh/evidence references, all
// confined to the data root's runs/ tree) and rejects any reparse point in the
// served trees, so symlink escapes planted after a prior validation cannot be
// activated or served.
internal static class HostedWebProgressSite
{
    internal static bool IsSafeName(string name)
    {
        if (name.Length == 0 || !char.IsAsciiLetterOrDigit(name[0])) return false;
        foreach (var c in name) if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')) return false;
        return true;
    }

    internal static bool IsSafeRelativePath(string relative)
    {
        if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Contains(':')) return false;
        foreach (var segment in relative.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..") return false;
            foreach (var c in segment) if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')) return false;
        }
        return true;
    }

    // Data references may use ".." segments for cross-run links; they must stay
    // inside the allowed subtree and no component may be a symlink.
    internal static bool TryResolveDataReference(string allowedRoot, string baseDirectory, string reference, out string physical)
    {
        physical = string.Empty;
        if (reference.Length == 0 || Path.IsPathRooted(reference) || reference.Contains('\\') || reference.Contains(':')) return false;
        foreach (var c in reference) if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/')) return false;
        var combined = Path.GetFullPath(reference, baseDirectory);
        var prefix = allowedRoot[^1] == Path.DirectorySeparatorChar ? allowedRoot : allowedRoot + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.Ordinal)) return false;
        // Full chain from "/": allowed root, its parents, all intermediates and
        // the final file must not be symlinks (root swaps cannot escape).
        if (!IsChainSafe(combined)) return false;
        physical = combined;
        return true;
    }

    // True when no component of the path (from "/" inclusive) is a symlink.
    internal static bool IsChainSafe(string path)
    {
        var full = Path.GetFullPath(path);
        for (var i = 1; i <= full.Length; i++)
        {
            if (i == full.Length || full[i] == Path.DirectorySeparatorChar)
            {
                var segmentPath = full[..i];
                if ((File.Exists(segmentPath) || Directory.Exists(segmentPath))
                    && (File.GetAttributes(segmentPath) & FileAttributes.ReparsePoint) != 0) return false;
            }
        }
        return true;
    }

    internal static bool HasReparsePoint(string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    // Closure gate over the validated data cache: index.json must register runs
    // whose manifests and every mesh/evidence reference (including cross-run
    // references) resolve inside runs/ and exist. This is the same gate the
    // publisher must satisfy before an atomic index switch; the host keeps it so
    // half-written or pruned trees never become client-visible.
    internal static JsonObject CheckDataRoot(string data)
    {
        var errors = new JsonArray();
        var runCount = 0;
        var referenceCount = 0;
        string? latest = null;
        var registered = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        void Error(string message) => errors.Add((JsonNode?)JsonValue.Create(message));
        var runsRoot = Path.Combine(data, "runs");
        if (!Directory.Exists(data)) { Error("data root missing"); return Result(); }
        if (!Directory.Exists(runsRoot)) { Error("runs tree missing"); return Result(); }
        if (!IsChainSafe(data)) Error("reparse point/symlink on the data root chain");
        if (HasReparsePoint(data)) Error("reparse point/symlink inside data root");
        var indexPath = Path.Combine(data, "index.json");
        if (!File.Exists(indexPath)) { Error("index.json missing"); return Result(); }
        JsonNode? index;
        try { index = JsonNode.Parse(File.ReadAllText(indexPath)); }
        catch (Exception e) { Error("index.json: " + e.Message); return Result(); }
        if (index?["schemaVersion"]?.GetValue<int>() != 1) Error("index schemaVersion");
        latest = index?["latestRunId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(latest)) Error("index latestRunId missing");
        foreach (var descriptor in index?["runs"]?.AsArray() ?? [])
        {
            var id = descriptor?["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id) || !IsSafeName(id!)) { Error("run descriptor with invalid id"); continue; }
            if (!registered.Add(id!)) Error("duplicate run id " + id);
            if (string.IsNullOrEmpty(descriptor?["commit"]?.GetValue<string>())) Error(id + ": commit missing");
            var manifestRel = descriptor?["manifest"]?.GetValue<string>();
            if (manifestRel is null
                || !TryResolveDataReference(runsRoot, data, manifestRel, out var manifestPath)
                || !manifestPath.EndsWith("manifest.json", StringComparison.Ordinal))
            {
                Error(id + ": manifest reference escapes runs tree");
                continue;
            }
            if (!File.Exists(manifestPath)) { Error(id + ": manifest missing " + manifestRel); continue; }
            runCount++;
            JsonNode? manifest;
            try { manifest = JsonNode.Parse(File.ReadAllText(manifestPath)); }
            catch (Exception e) { Error(id + ": manifest " + e.Message); continue; }
            if (manifest?["schemaVersion"]?.GetValue<int>() != 1) Error(id + ": manifest schemaVersion");
            if (manifest?["run"]?["id"] is JsonValue runId && runId.TryGetValue<string>(out var actual) && actual != id)
                Error(id + ": manifest run id mismatch " + actual);
            var runDirectory = Path.GetDirectoryName(manifestPath)!;
            foreach (var c in manifest?["cases"]?.AsArray() ?? [])
            {
                var caseId = c?["id"]?.GetValue<string>() ?? "<case>";
                foreach (var step in c?["steps"]?.AsArray() ?? [])
                    foreach (var side in new[] { "gm", "pk" })
                    {
                        foreach (var key in new[] { "before", "after" })
                            if (step?[side]?[key] is JsonValue single) Reference(single, caseId + " " + key);
                        foreach (var key in new[] { "tools", "afterBodies" })
                            foreach (var entry in step?[side]?[key]?.AsArray() ?? []) if (entry is JsonValue many) Reference(many, caseId + " " + key);
                    }
                foreach (var check in c?["checks"]?.AsArray() ?? [])
                    if (check?["evidence"] is JsonValue evidence) Reference(evidence, caseId + " check evidence");
                foreach (var evidence in c?["evidence"]?.AsArray() ?? [])
                    if (evidence?["path"] is JsonValue path) Reference(path, caseId + " evidence");
            }
            void Reference(JsonValue value, string context)
            {
                if (!value.TryGetValue<string>(out var relative)) { Error(id + ": " + context + " not a path"); return; }
                referenceCount++;
                if (!TryResolveDataReference(runsRoot, runDirectory, relative, out var target) || !File.Exists(target))
                {
                    Error(id + ": " + context + " missing or escaping " + relative);
                    return;
                }
                if (Path.GetExtension(target) is not (".json" or ".log")) Error(id + ": " + context + " non-public extension " + relative);
                var referencedRelative = Path.GetRelativePath(runsRoot, target).Replace('\\', '/');
                var referencedRun = referencedRelative.Split('/')[0];
                if (referencedRun.Length > 0 && referencedRun != ".") referenced.Add(referencedRun);
            }
        }
        if (latest is not null && !registered.Contains(latest)) Error("latestRunId not registered: " + latest);
        return Result();
        JsonObject Result() => new()
        {
            ["schemaVersion"] = 1,
            ["runs"] = runCount,
            ["references"] = referenceCount,
            ["latestRunId"] = latest,
            ["runIds"] = new JsonArray(registered.Order().Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
            ["referencedRunIds"] = new JsonArray(referenced.Order().Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
            ["errors"] = errors,
        };
    }

    // Immutable UI release snapshot: copy the built dist (without the bundled
    // data/ tree; data is served from the validated cache) into
    // ui-releases/<id>/ and record per-file checksums beside it. The release id
    // is content-derived, so preparing an unchanged build is a no-op.
    internal static JsonObject PrepareRelease(string siteRoot, string distDirectory)
    {
        var releases = Path.Combine(siteRoot, "ui-releases");
        if (Directory.Exists(siteRoot) && !IsChainSafe(siteRoot)) throw new InvalidDataException("Site root chain contains symlinks; refusing to write: " + siteRoot);
        Directory.CreateDirectory(releases);
        if (!IsChainSafe(releases)) throw new InvalidDataException("Releases root chain contains symlinks; refusing to write: " + releases);
        var indexHtml = Path.Combine(distDirectory, "index.html");
        if (!File.Exists(indexHtml)) throw new FileNotFoundException("Built dist is missing index.html: " + distDirectory);
        if (HasReparsePoint(distDirectory)) throw new InvalidDataException("Built dist contains symlinks; refusing to snapshot");
        var files = new SortedDictionary<string, (long Bytes, string Hash)>(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (var file in Directory.EnumerateFiles(distDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(distDirectory, file).Replace('\\', '/');
            if (relative == "data" || relative.StartsWith("data/", StringComparison.Ordinal)) continue; // served from the data cache
            if (!IsSafeRelativePath(relative)) throw new InvalidDataException("Unsafe file name in dist: " + relative);
            var bytes = new FileInfo(file).Length;
            files[relative] = (bytes, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant());
            totalBytes += bytes;
        }
        using var content = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (relative, entry) in files)
        {
            content.AppendData(System.Text.Encoding.UTF8.GetBytes(relative));
            content.AppendData(System.Text.Encoding.UTF8.GetBytes(entry.Hash));
        }
        var contentHash = Convert.ToHexString(content.GetHashAndReset()).ToLowerInvariant();
        // Content-derived id: preparing an unchanged build always maps to the
        // same immutable release directory and becomes a cache hit.
        var releaseId = "ui-" + contentHash[..16];
        if (!IsSafeName(releaseId)) throw new InvalidDataException("Derived release id is unsafe");
        var final = Path.Combine(releases, releaseId);
        var metaPath = Path.Combine(releases, releaseId + ".meta.json");
        // A cache hit requires the complete candidate: content directory plus
        // verified metadata. A directory without valid metadata is an
        // interrupted prepare; it can never have been activated (activation
        // verifies the metadata) and is rebuilt idempotently here instead of
        // requiring manual cache cleanup.
        if (Directory.Exists(final))
        {
            if (File.Exists(metaPath) && VerifyRelease(siteRoot, releaseId)["errors"]!.AsArray().Count == 0)
            {
                Console.WriteLine("RELEASE_CACHED " + releaseId + " files=" + files.Count + " bytes=" + totalBytes);
                return new JsonObject { ["release"] = releaseId, ["files"] = files.Count, ["bytes"] = totalBytes, ["contentHash"] = contentHash };
            }
            Console.WriteLine("RELEASE_INCOMPLETE_REBUILDING " + releaseId);
            Directory.Delete(final, true);
        }
        if (File.Exists(metaPath)) File.Delete(metaPath);
        var staging = Path.Combine(releases, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var (relative, _) in files)
            {
                var target = Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(distDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), target);
            }
            // Commit boundary: the metadata is written (atomic tmp+rename)
            // before the final directory rename, so every interruption window
            // recovers on retry and the final directory never appears without
            // its metadata.
            var manifest = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["id"] = releaseId,
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["contentHash"] = contentHash,
                ["bytes"] = totalBytes,
                ["files"] = new JsonArray(files.Select(f => (JsonNode?)new JsonObject
                {
                    ["path"] = f.Key,
                    ["bytes"] = f.Value.Bytes,
                    ["sha256"] = f.Value.Hash,
                }).ToArray()),
            };
            WriteJsonAtomic(metaPath, manifest);
            Directory.Move(staging, final);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        Console.WriteLine("RELEASE_PREPARED " + releaseId + " files=" + files.Count + " bytes=" + totalBytes);
        return new JsonObject { ["release"] = releaseId, ["files"] = files.Count, ["bytes"] = totalBytes, ["contentHash"] = contentHash };
    }

    // Verifies a prepared release against its checksum manifest: exact file set,
    // every hash matching, no reparse points. A mismatch blocks activation.
    internal static JsonObject VerifyRelease(string siteRoot, string releaseId)
    {
        var errors = new JsonArray();
        void Error(string message) => errors.Add((JsonNode?)JsonValue.Create(message));
        if (!IsSafeName(releaseId)) { Error("unsafe release id"); return new JsonObject { ["errors"] = errors }; }
        var releases = Path.Combine(siteRoot, "ui-releases");
        var releaseRoot = Path.Combine(releases, releaseId);
        var metaPath = Path.Combine(releases, releaseId + ".meta.json");
        if (!Directory.Exists(releaseRoot)) { Error("release directory missing"); return new JsonObject { ["errors"] = errors }; }
        if (!File.Exists(metaPath)) { Error("release manifest missing"); return new JsonObject { ["errors"] = errors }; }
        if (!IsChainSafe(releaseRoot)) Error("reparse point/symlink on the release root chain");
        if (HasReparsePoint(releaseRoot)) Error("reparse point/symlink inside release");
        JsonNode? manifest;
        try { manifest = JsonNode.Parse(File.ReadAllText(metaPath)); }
        catch (Exception e) { Error("release manifest: " + e.Message); return new JsonObject { ["errors"] = errors }; }
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest?["files"]?.AsArray() ?? [])
        {
            var relative = entry?["path"]?.GetValue<string>();
            if (relative is null || !IsSafeRelativePath(relative)) { Error("unsafe release entry"); continue; }
            expected.Add(relative);
            var target = Path.Combine(releaseRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(target)) { Error("missing " + relative); continue; }
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target))).ToLowerInvariant();
            if (hash != entry?["sha256"]?.GetValue<string>()) Error("checksum mismatch " + relative);
        }
        foreach (var file in Directory.EnumerateFiles(releaseRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(releaseRoot, file).Replace('\\', '/');
            if (!expected.Contains(relative)) Error("unexpected file " + relative);
        }
        return new JsonObject { ["errors"] = errors, ["release"] = releaseId, ["files"] = expected.Count };
    }

    // Atomic activation: validate release + full data closure first, import the
    // closure into the site-controlled immutable store, then swap the single
    // site.json pointer via tmp+rename. Refusals leave the previous activation
    // serving untouched. The activated data is pinned to a content-addressed
    // generation (exact index bytes + per-run file hashes): later writes to the
    // mutable source cache cannot become visible, and rollback re-activates the
    // exact previous generation instead of re-reading "current latest".
    //
    // Commit protocol: site.json is the SINGLE authoritative commit point and
    // carries everything needed for recovery (active pair, previous pair,
    // activation count) in one atomic rename. activation-state.json is a
    // derived mirror written best-effort AFTER the commit; a mirror failure is
    // reported explicitly (never swallowed into a false claim), keeps exit 0
    // because the activation itself committed, and is repaired on the next run
    // (skip-same never masks the repair). Two file renames are NOT claimed to
    // be one transaction; only the pointer is.
    internal static JsonObject Activate(string siteRoot, string releaseId, string dataRoot, string statePath)
    {
        var release = VerifyRelease(siteRoot, releaseId);
        if (release["errors"]!.AsArray().Count > 0) return Refused("ACTIVATE_REFUSED_RELEASE_INVALID", release);
        var data = CheckDataRoot(dataRoot);
        if (data["errors"]!.AsArray().Count > 0) return Refused("ACTIVATE_REFUSED_DATA_INVALID", data);
        var contentHash = JsonNode.Parse(File.ReadAllText(Path.Combine(siteRoot, "ui-releases", releaseId + ".meta.json")))!["contentHash"]!.GetValue<string>();
        dataRoot = Path.GetFullPath(dataRoot);
        var generation = EnsureGeneration(siteRoot, dataRoot, data);
        var pointerPath = Path.Combine(siteRoot, "site.json");
        var current = ReadJsonObject(pointerPath);
        var active = new JsonObject
        {
            ["uiRelease"] = releaseId,
            ["uiHash"] = contentHash,
            ["generation"] = generation["id"]!.GetValue<string>(),
            ["dataRunId"] = data["latestRunId"]?.DeepClone(),
            ["dataSha"] = JsonNode.Parse(File.ReadAllText(Path.Combine(dataRoot, "index.json")))!["runs"]!.AsArray()
                .First(r => r!["id"]!.GetValue<string>() == data["latestRunId"]!.GetValue<string>())!["commit"]!.GetValue<string>(),
            ["dataRoot"] = dataRoot, // informational: the import source, never served
            ["activatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["previous"] = current is null ? null : PairOf(current),
            ["activationCount"] = (current?["activationCount"]?.GetValue<int>() ?? 0) + 1,
        };
        if (current is not null
            && current["uiRelease"]?.GetValue<string>() == releaseId
            && current["generation"]?.GetValue<string>() == active["generation"]?.GetValue<string>()
            && current["dataRunId"]?.GetValue<string>() == active["dataRunId"]?.GetValue<string>())
        {
            var repaired = ReconcileMirror(statePath, current);
            Console.WriteLine("ACTIVATE_SKIP_SAME ui=" + releaseId + " generation=" + active["generation"] + " run=" + active["dataRunId"] + (repaired ? " ACTIVATION_STATE_REPAIRED" : ""));
            return new JsonObject { ["status"] = "skip-same", ["active"] = active.DeepClone() };
        }
        WriteJsonAtomic(pointerPath, active); // the single commit point
        MirrorOrReport(statePath, active);
        Console.WriteLine("ACTIVATED ui=" + releaseId + " generation=" + active["generation"] + " run=" + active["dataRunId"] + " sha=" + active["dataSha"] + " source=" + dataRoot);
        return new JsonObject { ["status"] = "activated", ["active"] = active, ["data"] = data, ["generation"] = generation };
        static JsonObject Refused(string status, JsonObject detail)
        {
            Console.Error.WriteLine(status + " " + detail.ToJsonString());
            return new JsonObject { ["status"] = status, ["detail"] = detail };
        }
    }

    // Imports the closure's run trees into the site-controlled content-addressed
    // store and materializes the immutable generation record that binds the
    // exact index bytes to every run's file hashes. Reuse rule: files are real
    // copies (never links into the mutable source cache), keyed by SHA-256 and
    // shared across generations; anything already stored is never modified or
    // re-read from the source. Only this script writes the store (single
    // trusted updater, tmp+rename, write-once).
    internal static JsonObject EnsureGeneration(string siteRoot, string sourceDataRoot, JsonObject check)
    {
        var store = Path.Combine(siteRoot, "data-store");
        var objects = Path.Combine(store, "objects");
        var trees = Path.Combine(store, "trees");
        if (!IsChainSafe(store)) throw new InvalidDataException("data-store chain contains symlinks");
        Directory.CreateDirectory(objects);
        Directory.CreateDirectory(trees);
        var runsRoot = Path.Combine(sourceDataRoot, "runs");
        var import = check["runIds"]!.AsArray().Select(r => r!.GetValue<string>())
            .Concat(check["referencedRunIds"]!.AsArray().Select(r => r!.GetValue<string>()))
            .Distinct(StringComparer.Ordinal).Order().ToArray();
        var runs = new JsonObject();
        // Per-invocation dedup of object hash verification: every unique
        // content key is verified (or repaired) exactly once per activation.
        var verifiedObjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var runId in import)
        {
            if (!IsSafeName(runId)) throw new InvalidDataException("unsafe run id " + runId);
            var sourceDir = Path.Combine(runsRoot, runId);
            if (!Directory.Exists(sourceDir)) throw new InvalidDataException("run tree missing " + runId);
            if (!IsChainSafe(sourceDir) || HasReparsePoint(sourceDir)) throw new InvalidDataException("symlink inside run " + runId);
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                if (!IsSafeRelativePath(relative)) throw new InvalidDataException("unsafe run file name " + runId + "/" + relative);
                if (Path.GetExtension(file) is not (".json" or ".log")) throw new InvalidDataException("non-public extension in run " + runId + ": " + relative);
                var sha = HashHex(File.ReadAllBytes(file));
                EnsureObject(objects, sha, file, verifiedObjects);
                map[relative] = sha;
            }
            using var tree = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var (relative, sha) in map)
            {
                tree.AppendData(System.Text.Encoding.UTF8.GetBytes(relative));
                tree.AppendData(System.Text.Encoding.UTF8.GetBytes(sha));
            }
            var treeHash = Convert.ToHexString(tree.GetHashAndReset()).ToLowerInvariant();
            EnsureTreeRecord(trees, treeHash, map);
            runs[runId] = treeHash;
        }
        var indexBytes = File.ReadAllBytes(Path.Combine(sourceDataRoot, "index.json"));
        var indexSha = HashHex(indexBytes);
        var identity = string.Join(",", runs.Select(kv => kv.Key + ":" + kv.Value));
        var generationId = "gen-" + HashHex(System.Text.Encoding.UTF8.GetBytes(indexSha + "|" + identity))[..16];
        if (!IsSafeName(generationId)) throw new InvalidDataException("derived generation id is unsafe");
        var generations = Path.Combine(siteRoot, "generations");
        Directory.CreateDirectory(generations);
        if (!IsChainSafe(generations)) throw new InvalidDataException("generations root chain contains symlinks");
        var generationDir = Path.Combine(generations, generationId);
        var generationMeta = Path.Combine(generations, generationId + ".meta.json");
        if (Directory.Exists(generationDir))
        {
            // Reuse requires verified integrity, not just existence: the record
            // must bind exactly this index and run mapping.
            if (VerifyGenerationRecord(generationDir, generationMeta, generationId, indexSha, runs))
            {
                Console.WriteLine("GENERATION_CACHED " + generationId + " runs=" + runs.Count + " indexSha=" + indexSha[..16]);
                return new JsonObject { ["id"] = generationId, ["indexSha256"] = indexSha, ["runs"] = runs };
            }
            Console.WriteLine("GENERATION_INCOMPLETE_REBUILDING " + generationId);
            Directory.Delete(generationDir, true);
        }
        if (File.Exists(generationMeta)) File.Delete(generationMeta);
        var staging = Path.Combine(generations, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            File.WriteAllBytes(Path.Combine(staging, "index.json"), indexBytes);
            // Commit boundary: metadata lands before the directory rename, so a
            // complete generation directory always has its metadata.
            WriteJsonAtomic(generationMeta, new JsonObject
            {
                ["schemaVersion"] = 1,
                ["id"] = generationId,
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["indexSha256"] = indexSha,
                ["sourceLatestRunId"] = check["latestRunId"]?.DeepClone(),
                ["sourceDataRoot"] = sourceDataRoot,
                ["runs"] = runs.DeepClone(),
            });
            Directory.Move(staging, generationDir);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        Console.WriteLine("GENERATION_PREPARED " + generationId + " runs=" + runs.Count + " indexSha=" + indexSha[..16]);
        return new JsonObject { ["id"] = generationId, ["indexSha256"] = indexSha, ["runs"] = runs };
    }

    // Content-verified object reuse: the object must hash to its content key.
    // A mismatch (controlled storage fault) is repaired atomically from the
    // verified source copy; existence alone is never accepted as integrity.
    private static void EnsureObject(string objects, string sha, string sourceFile, HashSet<string> verified)
    {
        var objectPath = Path.Combine(objects, sha);
        if (verified.Add(sha) || !File.Exists(objectPath))
        {
            if (File.Exists(objectPath))
            {
                if (HashHex(File.ReadAllBytes(objectPath)) == sha) return;
                Console.WriteLine("STORE_OBJECT_REPAIRED " + sha);
            }
            else Console.WriteLine("STORE_OBJECT_RESTORED " + sha);
            var tmp = objectPath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.Copy(sourceFile, tmp);
            File.Move(tmp, objectPath, true);
        }
    }

    // The tree record must exactly describe the computed file map (its key is
    // the hash of that map); a mismatched record is replaced, never trusted.
    private static void EnsureTreeRecord(string trees, string treeHash, SortedDictionary<string, string> map)
    {
        var record = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["treeHash"] = treeHash,
            ["files"] = new JsonArray(map.Select(kv => (JsonNode?)new JsonArray(kv.Key, kv.Value)).ToArray()),
        };
        var treeMeta = Path.Combine(trees, treeHash + ".json");
        var existing = ReadJsonObject(treeMeta);
        if (existing is not null && JsonNode.DeepEquals(existing, record)) return;
        if (existing is not null) Console.WriteLine("STORE_TREE_REPAIRED " + treeHash);
        WriteJsonAtomic(treeMeta, record);
    }

    // A cached generation may only be reused when its record binds exactly this
    // index and run mapping and its index snapshot matches the metadata hash.
    private static bool VerifyGenerationRecord(string generationDir, string generationMeta, string generationId, string indexSha, JsonObject runs)
    {
        var meta = ReadJsonObject(generationMeta);
        if (meta is null) return false;
        if (meta["id"]?.GetValue<string>() != generationId) return false;
        if (meta["indexSha256"]?.GetValue<string>() != indexSha) return false;
        if (!JsonNode.DeepEquals(meta["runs"], runs)) return false;
        var indexPath = Path.Combine(generationDir, "index.json");
        return File.Exists(indexPath) && HashHex(File.ReadAllBytes(indexPath)) == indexSha;
    }

    // Full referenced-closure verification of a generation: every tree record
    // must match its content key and every referenced object must hash to its
    // key. Used before rollback restores a generation; refuses on corruption
    // instead of claiming bad bytes are correct.
    internal static bool VerifyGenerationClosure(string siteRoot, string generationId, out string errors)
    {
        var problems = new List<string>();
        var meta = ReadJsonObject(Path.Combine(siteRoot, "generations", generationId + ".meta.json"));
        if (meta is null) { errors = "generation metadata unreadable"; return false; }
        var trees = Path.Combine(siteRoot, "data-store", "trees");
        var objects = Path.Combine(siteRoot, "data-store", "objects");
        foreach (var (runId, node) in (meta["runs"] as JsonObject) ?? new JsonObject())
        {
            var treeHash = node?.GetValue<string>();
            if (treeHash is null || treeHash.Length == 0) { problems.Add(runId + ": tree binding missing"); continue; }
            var treeMeta = ReadJsonObject(Path.Combine(trees, treeHash + ".json"));
            if (treeMeta?["files"] is not JsonArray files) { problems.Add(runId + ": tree record missing or malformed"); continue; }
            using var recomputed = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var pair in files)
            {
                var rel = pair?[0]?.GetValue<string>();
                var sha = pair?[1]?.GetValue<string>();
                if (rel is null || sha is null) { problems.Add(runId + ": malformed tree entry"); continue; }
                recomputed.AppendData(System.Text.Encoding.UTF8.GetBytes(rel));
                recomputed.AppendData(System.Text.Encoding.UTF8.GetBytes(sha));
                var objectPath = Path.Combine(objects, sha);
                if (!File.Exists(objectPath)) problems.Add(runId + "/" + rel + ": object missing " + sha);
                else if (HashHex(File.ReadAllBytes(objectPath)) != sha) problems.Add(runId + "/" + rel + ": object content does not match key " + sha);
            }
            if (Convert.ToHexString(recomputed.GetHashAndReset()).ToLowerInvariant() != treeHash) problems.Add(runId + ": tree record does not match its key");
        }
        var indexPath = Path.Combine(siteRoot, "generations", generationId, "index.json");
        if (!File.Exists(indexPath)) problems.Add("index snapshot missing");
        else if (HashHex(File.ReadAllBytes(indexPath)) != meta["indexSha256"]?.GetValue<string>()) problems.Add("index snapshot does not match metadata hash");
        errors = string.Join("; ", problems);
        return problems.Count == 0;
    }

    // Rollback re-activates the exact previously active generation and UI
    // release recorded IN THE AUTHORITATIVE POINTER: same index bytes, same run
    // file hashes, no re-resolution of "current latest" anywhere. Works even
    // when the derived state mirror is broken or missing.
    internal static JsonObject Rollback(string siteRoot, string statePath)
    {
        var pointerPath = Path.Combine(siteRoot, "site.json");
        var current = ReadJsonObject(pointerPath);
        var previous = current?["previous"] as JsonObject;
        var releaseId = previous?["uiRelease"]?.GetValue<string>();
        var generationId = previous?["generation"]?.GetValue<string>();
        if (releaseId is null || generationId is null)
        {
            Console.Error.WriteLine("ACTIVATE_REFUSED_NO_PREVIOUS authoritative pointer has no previous ui/generation pair");
            return new JsonObject { ["status"] = "ACTIVATE_REFUSED_NO_PREVIOUS" };
        }
        if (VerifyRelease(siteRoot, releaseId)["errors"]!.AsArray().Count > 0)
        {
            Console.Error.WriteLine("ACTIVATE_REFUSED_NO_PREVIOUS previous release is not retained");
            return new JsonObject { ["status"] = "ACTIVATE_REFUSED_NO_PREVIOUS" };
        }
        if (!File.Exists(Path.Combine(siteRoot, "generations", generationId + ".meta.json"))
            || !File.Exists(Path.Combine(siteRoot, "generations", generationId, "index.json")))
        {
            Console.Error.WriteLine("ACTIVATE_REFUSED_NO_PREVIOUS previous generation is not retained");
            return new JsonObject { ["status"] = "ACTIVATE_REFUSED_NO_PREVIOUS" };
        }
        if (!VerifyGenerationClosure(siteRoot, generationId, out var closureErrors))
        {
            // Explicit refusal: do NOT claim the compromised generation's bytes
            // are correct and do not restore it.
            Console.Error.WriteLine("ACTIVATE_REFUSED_GENERATION_CORRUPT previous generation integrity compromised, refusing rollback: " + closureErrors);
            return new JsonObject { ["status"] = "ACTIVATE_REFUSED_GENERATION_CORRUPT" };
        }
        var restored = new JsonObject
        {
            ["uiRelease"] = releaseId,
            ["uiHash"] = previous!["uiHash"]?.DeepClone(),
            ["generation"] = generationId,
            ["dataRunId"] = previous["dataRunId"]?.DeepClone(),
            ["dataSha"] = previous["dataSha"]?.DeepClone(),
            ["dataRoot"] = previous["dataRoot"]?.DeepClone(),
            ["activatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["previous"] = current is null ? null : PairOf(current),
            ["activationCount"] = (current?["activationCount"]?.GetValue<int>() ?? 0) + 1,
        };
        WriteJsonAtomic(pointerPath, restored); // the single commit point
        MirrorOrReport(statePath, restored);
        Console.WriteLine("ROLLBACK_ACTIVATED ui=" + releaseId + " generation=" + generationId + " run=" + restored["dataRunId"]);
        return new JsonObject { ["status"] = "activated", ["active"] = restored };
    }

    // The activation record that recovery needs: active pair + previous pair.
    private static JsonObject PairOf(JsonObject pointer) => new()
    {
        ["uiRelease"] = pointer["uiRelease"]?.DeepClone(),
        ["uiHash"] = pointer["uiHash"]?.DeepClone(),
        ["generation"] = pointer["generation"]?.DeepClone(),
        ["dataRunId"] = pointer["dataRunId"]?.DeepClone(),
        ["dataSha"] = pointer["dataSha"]?.DeepClone(),
        ["dataRoot"] = pointer["dataRoot"]?.DeepClone(),
    };

    // Best-effort derived mirror write; failures are reported, never swallowed
    // into a false consistency claim (the pointer already committed).
    private static void MirrorOrReport(string statePath, JsonObject pointer)
    {
        try
        {
            WriteJsonAtomic(statePath, MirrorOf(pointer));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("ACTIVATION_STATE_MIRROR_FAILED " + e.Message + " (site.json is authoritative; next run repairs the derived state)");
        }
    }

    // Repairs the derived mirror from the authoritative pointer. Returns true
    // when a repair was performed. The mirror must be the COMPLETE derived
    // state: active pair, previous pair and activation count are all compared,
    // so a same-pair retry after a mirror failure still recovers history.
    private static bool ReconcileMirror(string statePath, JsonObject pointer)
    {
        var mirror = ReadJsonObject(statePath);
        if (mirror is not null && JsonNode.DeepEquals(mirror, MirrorOf(pointer))) return false;
        try
        {
            WriteJsonAtomic(statePath, MirrorOf(pointer));
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("ACTIVATION_STATE_MIRROR_FAILED " + e.Message + " (site.json is authoritative; next run repairs the derived state)");
            return false;
        }
    }

    private static JsonObject MirrorOf(JsonObject pointer) => new()
    {
        ["schemaVersion"] = 1,
        ["active"] = PairOf(pointer),
        ["previous"] = pointer["previous"] is null ? null : pointer["previous"]!.DeepClone(),
        ["activationCount"] = pointer["activationCount"]?.DeepClone(),
    };

    internal static string HashHex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static JsonObject? ReadJsonObject(string path)
    {
        try { return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null; }
        catch { return null; }
    }

    internal static void WriteJsonAtomic(string path, JsonObject value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, value.ToJsonString());
        File.Move(tmp, path, true);
    }
}
