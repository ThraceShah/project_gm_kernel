using System.Text.Json;

namespace GmProgressHost;

/// <summary>
/// Reads the atomic activation pointer (<c>site.json</c> inside the site root)
/// and resolves the immutable artifacts it names: a prepared UI release under
/// <c>ui-releases/</c> and a data GENERATION (exact index bytes plus per-run
/// file hashes) whose files are served from the content-addressed store
/// <c>data-store/objects/</c>. The pointer is only ever replaced by atomic
/// rename (tmp + move), so a request observes either the previous or the new
/// activation, never a mixture. Invalid pointers — and pointers whose roots
/// fail the full symlink chain check — are ignored and the last known-good
/// mapping keeps resolving; each request re-verifies its chain and refuses on
/// mismatch. Because serving reads the store (never the mutable generation
/// source cache), writes to that cache cannot become visible without a new
/// activation, and old generations stay byte-observable through rollback.
/// </summary>
internal sealed class SiteState
{
    private readonly string _pointerPath;
    private readonly object _gate = new();
    private DateTime _pointerStamp = DateTime.MinValue;
    private SiteRoots? _roots;

    internal SiteState(string siteRoot)
    {
        SiteRoot = Path.GetFullPath(siteRoot);
        _pointerPath = Path.Combine(SiteRoot, "site.json");
    }

    internal string SiteRoot { get; }

    /// <summary>Current roots, or null when no valid activation exists yet.</summary>
    internal SiteRoots? Current()
    {
        var stamp = PointerStamp();
        lock (_gate)
        {
            if (_roots is not null && stamp == _pointerStamp) return _roots;
            var loaded = Load();
            if (loaded is not null) { _roots = loaded; _pointerStamp = stamp; }
            return _roots;
        }
    }

    private DateTime PointerStamp()
    {
        try { return File.GetLastWriteTimeUtc(_pointerPath); }
        catch { return DateTime.MinValue; }
    }

    private SiteRoots? Load()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(_pointerPath));
            var root = doc.RootElement;
            var release = root.GetProperty("uiRelease").GetString();
            var generation = root.TryGetProperty("generation", out var g) ? g.GetString() : null;
            if (release is null || generation is null) return null;
            if (!SafePaths.IsSafeRequestPath("/" + release) || !SafePaths.IsSafeRequestPath("/" + generation)) return null;
            var uiRoot = Path.GetFullPath(Path.Combine(SiteRoot, "ui-releases", release));
            var uiExpected = Path.GetFullPath(Path.Combine(SiteRoot, "ui-releases")) + Path.DirectorySeparatorChar;
            if (!uiRoot.StartsWith(uiExpected, StringComparison.Ordinal)) return null;
            if (!SafePaths.IsChainSafe(uiRoot) || !Directory.Exists(uiRoot)) return null;
            var generationDir = Path.GetFullPath(Path.Combine(SiteRoot, "generations", generation));
            var generationExpected = Path.GetFullPath(Path.Combine(SiteRoot, "generations")) + Path.DirectorySeparatorChar;
            if (!generationDir.StartsWith(generationExpected, StringComparison.Ordinal)) return null;
            if (!SafePaths.IsChainSafe(generationDir) || !File.Exists(Path.Combine(generationDir, "index.json"))) return null;
            var storeRoot = Path.Combine(SiteRoot, "data-store");
            if (!SafePaths.IsChainSafe(storeRoot) || !Directory.Exists(storeRoot)) return null;
            var runId = root.TryGetProperty("dataRunId", out var r) ? r.GetString() : null;
            var store = new GenerationStore(SiteRoot, generation);
            return new SiteRoots(uiRoot, release, runId, generationDir, generation, store);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record SiteRoots(
    string UiRoot,
    string UiRelease,
    string? DataRunId,
    string GenerationDir,
    string Generation,
    GenerationStore Store);

/// <summary>
/// Resolves <c>/data/runs/&lt;runId&gt;/&lt;file&gt;</c> against the retained
/// generations' run-to-tree mappings and the content-addressed object store.
/// The active generation's mapping wins; retained older generations keep their
/// run sets addressable so already-served HTML completes its data references.
/// Objects are exact content hashes: a lookup returns the identical bytes that
/// were imported for that generation.
/// </summary>
internal sealed class GenerationStore
{
    private readonly string _objects;
    private readonly string _trees;
    private readonly List<(string RunId, string TreeHash)> _mapping = new(); // active first, then retained newest-first
    private readonly Dictionary<string, Dictionary<string, string>?> _treeMaps = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    internal GenerationStore(string siteRoot, string activeGeneration)
    {
        _objects = Path.Combine(siteRoot, "data-store", "objects");
        _trees = Path.Combine(siteRoot, "data-store", "trees");
        var generations = Path.Combine(siteRoot, "generations");
        var seenRuns = new HashSet<string>(StringComparer.Ordinal);
        void AddGeneration(string generationId)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(generations, generationId + ".meta.json")));
                foreach (var run in doc.RootElement.GetProperty("runs").EnumerateObject())
                {
                    if (!seenRuns.Add(run.Name)) continue;
                    _mapping.Add((run.Name, run.Value.GetString() ?? ""));
                }
            }
            catch { /* incomplete generation record: not addressable */ }
        }
        AddGeneration(activeGeneration);
        foreach (var meta in Directory.EnumerateFiles(generations, "gen-*.meta.json").OrderDescending())
        {
            var id = Path.GetFileName(meta)!["gen-".Length..];
            id = "gen-" + id[..^".meta.json".Length];
            if (id != activeGeneration) AddGeneration(id);
        }
    }

    internal bool TryResolveRunFile(string runId, string relative, out string objectPath)
    {
        objectPath = string.Empty;
        if (runId.Length == 0 || relative.Length == 0) return false;
        string? tree = null;
        foreach (var (id, hash) in _mapping)
        {
            if (id == runId) { tree = hash; break; }
        }
        if (tree is null) return false;
        var map = TreeMap(tree);
        if (map is null || !map.TryGetValue(relative, out var sha) || sha.Length != 64) return false;
        var candidate = Path.Combine(_objects, sha);
        if (!SafePaths.TryResolve(_objects, sha, out var resolved) || !File.Exists(resolved)) return false;
        objectPath = resolved;
        return true;
    }

    private Dictionary<string, string>? TreeMap(string treeHash)
    {
        lock (_gate)
        {
            if (_treeMaps.TryGetValue(treeHash, out var cached)) return cached;
            Dictionary<string, string>? map = null;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_trees, treeHash + ".json")));
                map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in doc.RootElement.GetProperty("files").EnumerateArray())
                {
                    var rel = pair[0].GetString();
                    var sha = pair[1].GetString();
                    if (rel is not null && sha is not null) map[rel] = sha;
                }
            }
            catch { map = null; }
            _treeMaps[treeHash] = map;
            return map;
        }
    }
}
