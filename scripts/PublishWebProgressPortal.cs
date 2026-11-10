#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Deployment-owner publish step for the validated GM Kernel progress site.
// Publishes a staged web/dist bundle via portalctl static (atomic snapshot
// publish, stable slug gm-kernel-progress). Publish state is deliberately
// independent of the watcher generation state: a failed or interrupted publish
// keeps the previous portal version and leaves publish-state untouched, so the
// next timer tick retries the publication from the already validated cache. No
// oracle case is ever re-run for a publish retry; same-run publications are
// skipped. The staged bundle is bounded by the portal's 32 MiB static limit:
// run directories not referenced by the staged index are dropped and, if still
// oversized, the oldest runs are dropped down to the newest ones (the validated
// cache retains the full history).
//
// Usage (all paths resolve against this script's own directory):
//   dotnet run --file scripts/PublishWebProgressPortal.cs -- \
//     [--data ../temp_docs/web-progress-publish/data] [--web ../web] \
//     [--state ../temp_docs/web-progress-publish/publish-state.json] \
//     [--slug gm-kernel-progress] [--portalctl portalctl] \
//     [--name ...] [--description ...]
var scripts = Path.GetDirectoryName(ScriptPath())!;
var data = Path.GetFullPath(Option("--data", "../temp_docs/web-progress-publish/data"), scripts);
var web = Path.GetFullPath(Option("--web", "../web"), scripts);
var dist = Path.Combine(web, "dist");
var statePath = Path.GetFullPath(Option("--state", "../temp_docs/web-progress-publish/publish-state.json"), scripts);
var slug = Option("--slug", "gm-kernel-progress");
var portalctl = Option("--portalctl", "portalctl");
var name = Option("--name", "GM Kernel vs Parasolid 对比工作台");
var description = Option("--description", "GM 几何建模内核与 Parasolid 真实对比与工程指标追踪工作台");
const long BundleBudgetBytes = 30L * 1024 * 1024; // portal hard limit is 32 MiB decoded

// 1. The validated data cache must carry a complete latest run.
var dataIndex = Path.Combine(data, "index.json");
if (!File.Exists(dataIndex)) { Console.Error.WriteLine("PUBLISH_REFUSED no validated data index: " + dataIndex); return 1; }
var index = JsonNode.Parse(File.ReadAllText(dataIndex))!;
var latest = index["latestRunId"]!.GetValue<string>();
var descriptor = index["runs"]!.AsArray().Single(r => r!["id"]!.GetValue<string>() == latest)!;
var commit = descriptor["commit"]!.GetValue<string>();
var manifestRel = descriptor["manifest"]!.GetValue<string>();
var manifestPath = Path.GetFullPath(manifestRel, data);
if (!File.Exists(manifestPath)) { Console.Error.WriteLine("PUBLISH_REFUSED missing validated manifest: " + manifestRel); return 1; }

// 2. The prepared site must reference exactly this validated run ("only publish
//    the validated package"); otherwise keep the previous portal version.
var distIndex = Path.Combine(dist, "data", "index.json");
if (!File.Exists(distIndex)) { Console.Error.WriteLine("PUBLISH_REFUSED site not prepared (missing dist/data/index.json)"); return 1; }
var distLatest = JsonNode.Parse(File.ReadAllText(distIndex))!["latestRunId"]!.GetValue<string>();
if (distLatest != latest)
{
    Console.Error.WriteLine($"PUBLISH_REFUSED site prepared for {distLatest} but validated run is {latest}");
    return 1;
}
if (!File.Exists(Path.GetFullPath(manifestRel, Path.Combine(dist, "data"))))
{
    Console.Error.WriteLine("PUBLISH_REFUSED dist/data missing manifest " + manifestRel);
    return 1;
}

// 3. Stage a bundle bounded by the portal's static size limit. The latest
//    aggregate manifest references the measurement runs of its accumulated
//    cases ("../<runId>/..."); those runs are required evidence and are never
//    silently dropped. Older history runs are kept while they fit, newest
//    first. If the latest aggregate and its evidence alone exceed the budget,
//    the publication is refused loudly with measured sizes instead of cutting
//    cases or evidence.
var bundle = Path.Combine(Path.GetDirectoryName(statePath)!, "bundle");
if (Directory.Exists(bundle)) Directory.Delete(bundle, true);
try { CopyTree(dist, bundle); }
catch (IOException e) { Console.Error.WriteLine("PUBLISH_REFUSED " + e.Message); return 1; }
var bundleIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(bundle, "data", "index.json")))!;
var bundleRunsDir = Path.Combine(bundle, "data", "runs");
var orderedRuns = bundleIndex["runs"]!.AsArray()
    .OrderByDescending(r => r!["id"]!.GetValue<string>() == latest)
    .ThenByDescending(r => r!["generatedAt"]?.GetValue<string>() ?? string.Empty)
    .ToList();
long NonRunSize()
{
    long total = TreeSize(bundle);
    if (Directory.Exists(bundleRunsDir)) foreach (var dir in Directory.GetDirectories(bundleRunsDir)) total -= TreeSize(dir);
    return total;
}
HashSet<string> ReferencesOf(string runId)
{
    var references = new HashSet<string>(StringComparer.Ordinal);
    var manifestPath2 = Path.Combine(bundleRunsDir, runId, "manifest.json");
    if (!File.Exists(manifestPath2)) return references;
    foreach (Match match in Regex.Matches(File.ReadAllText(manifestPath2), "\\.\\./([^/]+)/")) references.Add(match.Groups[1].Value);
    return references;
}
var keepRuns = new List<JsonObject>();
var keepIds = new HashSet<string>(StringComparer.Ordinal);
foreach (var run in orderedRuns)
{
    var id = run!["id"]!.GetValue<string>();
    var candidate = new HashSet<string>(keepIds, StringComparer.Ordinal) { id };
    foreach (var reference in ReferencesOf(id)) candidate.Add(reference);
    var projected = NonRunSize();
    foreach (var candidateId in candidate) if (Directory.Exists(Path.Combine(bundleRunsDir, candidateId))) projected += TreeSize(Path.Combine(bundleRunsDir, candidateId));
    if (projected > BundleBudgetBytes && keepRuns.Count > 0) { Console.WriteLine("PUBLISH_PRUNED_HISTORY_RUN " + id + " projected=" + projected); continue; }
    keepRuns.Add(run.AsObject());
    foreach (var candidateId in candidate) keepIds.Add(candidateId);
}
if (Directory.Exists(bundleRunsDir))
{
    foreach (var dir in Directory.GetDirectories(bundleRunsDir))
        if (!keepIds.Contains(Path.GetFileName(dir))) { Directory.Delete(dir, true); Console.WriteLine("PUBLISH_PRUNED_UNREFERENCED_RUN " + Path.GetFileName(dir)); }
}
// Required referenced runs that are not themselves index entries stay as
// directory evidence even when their index descriptor was pruned.
bundleIndex["runs"] = new JsonArray(keepRuns.Select(r => (JsonNode?)r.DeepClone()).ToArray());
File.WriteAllText(Path.Combine(bundle, "data", "index.json"), bundleIndex.ToJsonString());
var bundleSize = TreeSize(bundle);
if (bundleSize > 32L * 1024 * 1024)
{
    Console.Error.WriteLine($"PUBLISH_REFUSED_BUNDLE_OVERSIZE bundle {bundleSize} bytes exceeds the 32 MiB portal limit; the latest aggregate and its measurement evidence alone do not fit. Validated cache keeps the full history; use a persistent static serving path (docs/web-progress-persistent-scheduling.md capacity plan) instead of dropping case geometry or evidence.");
    return 1;
}
Console.WriteLine($"PUBLISH_BUNDLE runs={keepRuns.Count} bytes={bundleSize}");

// 4. Same run already published -> skip without touching any state.
var publishedRunId = File.Exists(statePath) ? JsonNode.Parse(File.ReadAllText(statePath))?["runId"]?.GetValue<string>() : null;
var publishedSha = File.Exists(statePath) ? JsonNode.Parse(File.ReadAllText(statePath))?["sha"]?.GetValue<string>() : null;
if (publishedRunId == latest && publishedSha == commit)
{
    Console.WriteLine($"PUBLISH_SKIP_SAME_RUN {latest} {commit}");
    return 0;
}

// 5. Publish atomically via portalctl. On failure keep publish-state untouched:
//    the validated cache stays pending and the next timer tick retries without
//    regenerating any case.
var info = new ProcessStartInfo(portalctl) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (var a in new[] { "static", "--slug", slug, "--name", name, "--description", description, "--dir", bundle }) info.ArgumentList.Add(a);
using var process = Process.Start(info)!;
var stdout = process.StandardOutput.ReadToEnd();
var stderr = process.StandardError.ReadToEnd();
process.WaitForExit();
if (process.ExitCode != 0)
{
    Console.Error.WriteLine($"PUBLISH_FAILED portalctl exit {process.ExitCode}; publish-state untouched, pending cache kept for retry");
    Console.Error.WriteLine(stderr);
    return process.ExitCode;
}

// 6. Record the successful publication (atomic replace).
Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
File.WriteAllText(statePath + ".tmp", new JsonObject
{
    ["runId"] = latest,
    ["sha"] = commit,
    ["slug"] = slug,
    ["publishedAt"] = DateTimeOffset.UtcNow.ToString("O"),
}.ToJsonString());
File.Move(statePath + ".tmp", statePath, true);
Console.WriteLine($"PUBLISH_OK {latest} {commit}");
Console.WriteLine(stdout.Trim());
return 0;

string Option(string optionName, string fallback) { var at = Array.IndexOf(args, optionName); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
static void CopyTree(string source, string target)
{
    // Same contract as the data publisher: symlinks and other reparse points
    // are not static data and must never be followed into a bundle.
    Directory.CreateDirectory(target);
    foreach (var entry in Directory.EnumerateFileSystemEntries(source))
    {
        if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symlinks are not static data: " + Path.GetFileName(entry));
        if (Directory.Exists(entry)) CopyTree(entry, Path.Combine(target, Path.GetFileName(entry)));
        else File.Copy(entry, Path.Combine(target, Path.GetFileName(entry)));
    }
}
static long TreeSize(string root)
{
    long total = 0;
    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) total += new FileInfo(file).Length;
    return total;
}
