#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseHostedWebProgressSite=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Atomically activates a prepared UI release plus the validated data cache for
// the read-only hosted web progress site (see docs/web-progress-hosting.md).
// This replaces the portalctl static publish hook in the refresh chain: no bulk
// upload, no portal write. Activation re-checks the release checksums and the
// full reference closure of the data cache (every mesh/evidence reference of
// every indexed manifest, including cross-run ../<runId>/ links, must resolve
// inside runs/), then swaps the single site.json pointer with tmp+rename. Any
// refusal leaves the previous activation serving untouched; a retry reuses the
// already validated cache and never re-runs geometry. Activation state
// (activation-state.json) stays separate from generation state (watch-state)
// and from lastPublishedRun/SHA (publish-state).
//
// Usage (all paths resolve against this script's own directory):
//   dotnet run --file scripts/ActivateHostedWebProgressSite.cs -- \
//     [--site ../temp_docs/web-progress-hosting/site] \
//     [--data ../temp_docs/web-progress-publish/data] \
//     [--state ../temp_docs/web-progress-hosting/activation-state.json] \
//     [--release <id>] [--expect-run <runId>] [--rollback]
var scripts = Path.GetDirectoryName(ScriptPath())!;
var site = Path.GetFullPath(Option("--site", "../temp_docs/web-progress-hosting/site"), scripts);
var data = Path.GetFullPath(Option("--data", "../temp_docs/web-progress-publish/data"), scripts);
var state = Path.GetFullPath(Option("--state", "../temp_docs/web-progress-hosting/activation-state.json"), scripts);
if (args.Contains("--rollback"))
{
    var rollback = HostedWebProgressSite.Rollback(site, state);
    return rollback["status"]?.GetValue<string>() == "activated" ? 0 : 3;
}
var release = Option("--release", "");
if (string.IsNullOrEmpty(release)) release = NewestRelease(site);
if (string.IsNullOrEmpty(release))
{
    Console.Error.WriteLine("ACTIVATE_REFUSED_NO_RELEASE no prepared UI release under " + site);
    return 3;
}
var expectRun = Option("--expect-run", "");
if (expectRun.Length > 0)
{
    var index = HostedWebProgressSite.ReadJsonObject(Path.Combine(data, "index.json"));
    var latest = index?["latestRunId"]?.GetValue<string>();
    if (latest != expectRun)
    {
        Console.Error.WriteLine($"ACTIVATE_REFUSED_RUN_MISMATCH expected {expectRun} but validated cache latest is {latest}");
        return 3;
    }
}
var result = HostedWebProgressSite.Activate(site, release, data, state);
return result["status"]?.GetValue<string>() is "activated" or "skip-same" ? 0 : 3;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
static string NewestRelease(string site)
{
    var releases = Path.Combine(site, "ui-releases");
    if (!Directory.Exists(releases)) return "";
    string newest = "";
    var newestAt = "";
    foreach (var meta in Directory.EnumerateFiles(releases, "*.meta.json"))
    {
        var manifest = HostedWebProgressSite.ReadJsonObject(meta);
        var id = manifest?["id"]?.GetValue<string>();
        var createdAt = manifest?["createdAt"]?.GetValue<string>() ?? "";
        if (id is null || !HostedWebProgressSite.IsSafeName(id)) continue;
        if (newest.Length == 0 || string.CompareOrdinal(createdAt, newestAt) > 0) { newest = id; newestAt = createdAt; }
    }
    return newest;
}
