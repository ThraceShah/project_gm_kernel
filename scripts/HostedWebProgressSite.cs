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
        var registered = new HashSet<string>(StringComparer.Ordinal);
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

    // Atomic activation: validate release + full data closure first, then swap
    // the single site.json pointer via tmp+rename. Refusals leave the previous
    // activation serving untouched.
    internal static JsonObject Activate(string siteRoot, string releaseId, string dataRoot, string statePath)
    {
        var release = VerifyRelease(siteRoot, releaseId);
        if (release["errors"]!.AsArray().Count > 0) return Refused("ACTIVATE_REFUSED_RELEASE_INVALID", release);
        var data = CheckDataRoot(dataRoot);
        if (data["errors"]!.AsArray().Count > 0) return Refused("ACTIVATE_REFUSED_DATA_INVALID", data);
        var contentHash = JsonNode.Parse(File.ReadAllText(Path.Combine(siteRoot, "ui-releases", releaseId + ".meta.json")))!["contentHash"]!.GetValue<string>();
        dataRoot = Path.GetFullPath(dataRoot);
        var active = new JsonObject
        {
            ["uiRelease"] = releaseId,
            ["uiHash"] = contentHash,
            ["dataRoot"] = dataRoot,
            ["dataRunId"] = data["latestRunId"]?.DeepClone(),
            ["dataSha"] = JsonNode.Parse(File.ReadAllText(Path.Combine(dataRoot, "index.json")))!["runs"]!.AsArray()
                .First(r => r!["id"]!.GetValue<string>() == data["latestRunId"]!.GetValue<string>())!["commit"]!.GetValue<string>(),
            ["activatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        var previous = ReadJsonObject(Path.Combine(siteRoot, "site.json"));
        if (previous is not null
            && previous["uiRelease"]?.GetValue<string>() == releaseId
            && previous["dataRoot"]?.GetValue<string>() == dataRoot
            && previous["dataRunId"]?.GetValue<string>() == active["dataRunId"]?.GetValue<string>()
            && previous["dataSha"]?.GetValue<string>() == active["dataSha"]?.GetValue<string>())
        {
            Console.WriteLine("ACTIVATE_SKIP_SAME ui=" + releaseId + " run=" + active["dataRunId"]);
            return new JsonObject { ["status"] = "skip-same", ["active"] = active.DeepClone() };
        }
        WriteJsonAtomic(Path.Combine(siteRoot, "site.json"), active);
        var state = ReadJsonObject(statePath) ?? new JsonObject();
        state["schemaVersion"] = 1;
        state["previous"] = previous is null ? null : previous.DeepClone();
        state["active"] = active.DeepClone();
        state["activationCount"] = (state["activationCount"]?.GetValue<int>() ?? 0) + 1;
        WriteJsonAtomic(statePath, state);
        Console.WriteLine("ACTIVATED ui=" + releaseId + " run=" + active["dataRunId"] + " sha=" + active["dataSha"] + " dataRoot=" + dataRoot);
        return new JsonObject { ["status"] = "activated", ["active"] = active, ["data"] = data };
        static JsonObject Refused(string status, JsonObject detail)
        {
            Console.Error.WriteLine(status + " " + detail.ToJsonString());
            return new JsonObject { ["status"] = status, ["detail"] = detail };
        }
    }

    // Rollback re-activates the previously active ui/data pair recorded in the
    // activation state; both trees are retained by contract, so this needs no
    // rebuild and no data regeneration.
    internal static JsonObject Rollback(string siteRoot, string statePath)
    {
        var state = ReadJsonObject(statePath);
        var previous = state?["previous"] as JsonObject;
        if (previous is null)
        {
            Console.Error.WriteLine("ACTIVATE_REFUSED_NO_PREVIOUS activation state has no previous activation");
            return new JsonObject { ["status"] = "ACTIVATE_REFUSED_NO_PREVIOUS" };
        }
        var dataRoot = previous["dataRoot"]?.GetValue<string>();
        var releaseId = previous["uiRelease"]?.GetValue<string>();
        if (dataRoot is null || releaseId is null)
        {
            Console.Error.WriteLine("ACTIVATE_REFUSED_NO_PREVIOUS previous activation record incomplete");
            return new JsonObject { ["status"] = "ACTIVATE_REFUSED_NO_PREVIOUS" };
        }
        var result = Activate(siteRoot, releaseId, dataRoot, statePath);
        if (result["status"]?.GetValue<string>() == "activated")
        {
            // Rollback swaps the history roles: the failed attempt can be retried.
            var rolled = ReadJsonObject(statePath)!;
            rolled["previous"] = state!["active"]?.DeepClone();
            rolled["rollbackCount"] = (state["rollbackCount"]?.GetValue<int>() ?? 0) + 1;
            WriteJsonAtomic(statePath, rolled);
            Console.WriteLine("ROLLBACK_ACTIVATED ui=" + releaseId);
        }
        return result;
    }

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
