using System.Text.Json;

namespace GmProgressHost;

/// <summary>
/// Reads the atomic activation pointer (<c>site.json</c> inside the site root)
/// and caches the two immutable roots it names: a prepared UI release under
/// <c>ui-releases/</c> and the validated data root. The pointer is only ever
/// replaced by atomic rename (tmp + move), so a request observes either the
/// previous or the new activation, never a mixture. Invalid pointers — and
/// pointers whose roots fail the full symlink chain check — are ignored and
/// the last known-good roots keep serving, except where those roots themselves
/// have been swapped (each request re-verifies its chain and then refuses).
/// Roots are refreshed per request by stat-ing the pointer file; release and
/// data trees are immutable between activations.
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
            var dataRoot = root.GetProperty("dataRoot").GetString();
            if (release is null || dataRoot is null) return null;
            if (!SafePaths.IsSafeRequestPath("/" + release)) return null;
            var uiRoot = Path.GetFullPath(Path.Combine(SiteRoot, "ui-releases", release));
            var expected = Path.GetFullPath(Path.Combine(SiteRoot, "ui-releases")) + Path.DirectorySeparatorChar;
            if (!uiRoot.StartsWith(expected, StringComparison.Ordinal)) return null;
            if (!SafePaths.IsChainSafe(uiRoot) || !Directory.Exists(uiRoot)) return null;
            dataRoot = Path.GetFullPath(dataRoot);
            if (!SafePaths.IsChainSafe(dataRoot) || !Directory.Exists(dataRoot)) return null;
            var runId = root.TryGetProperty("dataRunId", out var r) ? r.GetString() : null;
            return new SiteRoots(uiRoot, dataRoot, release, runId);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record SiteRoots(string UiRoot, string DataRoot, string UiRelease, string? DataRunId);
