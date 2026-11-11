#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Independent black-box regressions for the hosting review findings H1-H5.
// Drives the real Prepare/Activate CLI scripts and the real host binary over
// HTTP only, using one continuously-updated data cache (publisher-style atomic
// index swaps), so each check fails before its fix and passes after:
//   H1  un-activated / refused / mutated source data never becomes visible;
//       rollback restores the exact previously active index bytes; served
//       run files are controlled copies, not links into the mutable source.
//   H2  swapping a serving root (or its parents) for an outside symlink never
//       serves outside content; raw percent-encoded dot segments are rejected.
//   H3  an already-served HTML keeps loading its hashed assets across a UI
//       switch and across rollback; index.html never falls back; unknown
//       paths never fall back.
//   H4  activation-state write failure after the pointer commit is reported
//       truthfully, the retry repairs the derived state (never a bare
//       ACTIVATE_SKIP_SAME), and rollback works from the authoritative pointer.
//   H5  an interrupted Prepare (release dir without metadata) self-heals on
//       retry; cache hits require verified metadata; active releases are not
//       destructively overwritten.
//
// Usage (all paths resolve against this script's own directory):
//   dotnet run --file scripts/VerifyHostedWebProgressGaps.cs -- \
//     [--work ../temp_docs/web-progress-hosting/gaps] [--port 8795] [--section all]
var scripts = Path.GetDirectoryName(ScriptPath())!;
var work = Path.GetFullPath(Option("--work", "../temp_docs/web-progress-hosting/gaps"), scripts);
var port = int.Parse(Option("--port", "8795"));
var section = Option("--section", "all");
var hostProject = Path.GetFullPath("../web/host", scripts);
var logs = Path.Combine(work, "logs");
var results = new List<(string Name, bool Ok, string Detail)>();
void Check(string name, bool ok, string detail = "") { results.Add((name, ok, detail)); Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " -- " + detail : "")); }
bool Wanted(string h) => section == "all" || section == h;
var commands = new List<string>();
int Cmd(string label, string file, string[] arguments, out string stdout)
{
    var info = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    using var process = Process.Start(info)!;
    stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    commands.Add($"# {label}\n$ {file} {string.Join(' ', arguments)}\n[exit {process.ExitCode}]\n{stdout.Trim()}\n{stderr.Trim()}");
    return process.ExitCode;
}

// ------------------------------------------------------------------ fixtures
if (Directory.Exists(work)) Directory.Delete(work, true);
Directory.CreateDirectory(logs);
var site = Path.Combine(work, "site");
var cache = Path.Combine(work, "cache");
var outside = Path.Combine(work, "outside");
Directory.CreateDirectory(Path.Combine(site, "ui-releases"));
foreach (var dir in new[] { cache, outside }) Directory.CreateDirectory(dir);

// Outside-root marker trees: must never become servable.
void WriteOutside(string name, string content)
{
    Directory.CreateDirectory(Path.Combine(outside, name));
    File.WriteAllText(Path.Combine(outside, name, "index.html"), content);
    File.WriteAllText(Path.Combine(outside, name, "token.log"), content);
}
WriteOutside("ui", "OUTSIDE_ROOT_MARKER ui\n");
WriteOutside("releases", "OUTSIDE_ROOT_MARKER releases\n");
WriteOutside("data", "OUTSIDE_ROOT_MARKER data\n");

// Two handcrafted UI releases with genuinely different hashed asset sets.
string MakeDist(string name, string assetName, string htmlMarker)
{
    var dist = Path.Combine(work, name);
    Directory.CreateDirectory(Path.Combine(dist, "assets"));
    File.WriteAllText(Path.Combine(dist, "index.html"), $"<html><script src=\"./assets/{assetName}\"></script><!-- {htmlMarker} --></html>\n");
    File.WriteAllText(Path.Combine(dist, "assets", assetName), $"// {assetName} payload for {htmlMarker}\n");
    return dist;
}
var distOld = MakeDist("dist-old", "old-111.js", "OLD-HTML");
var distNew = MakeDist("dist-new", "new-222.js", "NEW-HTML");

// One continuously-updated validated data cache, mutated publisher-style:
// run directories staged/renamed first, then the index atomically swapped.
void WriteRun(string runId, string commit, string evidenceName, string evidenceBody, bool createEvidence = true)
{
    var runDir = Path.Combine(cache, "runs", runId);
    var sample = Path.Combine(runDir, "cases", "c1", "sample-0");
    Directory.CreateDirectory(Path.Combine(sample, "meshes"));
    File.WriteAllText(Path.Combine(sample, "meshes", "m.json"), $"{{\"schemaVersion\":1,\"bodyId\":\"{runId}\"}}\n");
    if (createEvidence) File.WriteAllText(Path.Combine(sample, evidenceName), evidenceBody);
    File.WriteAllText(Path.Combine(runDir, "manifest.json"), new JsonObject
    {
        ["schemaVersion"] = 1,
        ["run"] = new JsonObject { ["id"] = runId, ["commit"] = commit, ["generatedAt"] = "now", ["pkVersion"] = null, ["host"] = new JsonObject(), ["configuration"] = new JsonObject(), ["scope"] = "gaps", ["provenance"] = new JsonObject() },
        ["cases"] = new JsonArray(new JsonObject
        {
            ["id"] = "c1", ["title"] = "gaps fixture", ["family"] = "blend", ["operation"] = "gaps", ["status"] = "fail",
            ["parameters"] = new JsonObject(), ["apis"] = new JsonObject { ["gm"] = new JsonObject { ["name"] = "none", ["parameters"] = new JsonObject() }, ["pk"] = new JsonObject { ["name"] = "none", ["parameters"] = new JsonObject() } },
            ["steps"] = new JsonArray(new JsonObject { ["id"] = "s1", ["title"] = "gaps", ["gm"] = new JsonObject { ["before"] = "cases/c1/sample-0/meshes/m.json", ["after"] = "cases/c1/sample-0/meshes/m.json" }, ["pk"] = new JsonObject { ["before"] = "cases/c1/sample-0/meshes/m.json", ["after"] = "cases/c1/sample-0/meshes/m.json" } }),
            ["checks"] = new JsonArray(new JsonObject { ["id"] = "k1", ["label"] = "gaps", ["status"] = "fail", ["gm"] = 1, ["pk"] = 0, ["absDelta"] = 1, ["normalizedDelta"] = 1, ["threshold"] = 0.5, ["unit"] = "count", ["evidence"] = $"cases/c1/sample-0/{evidenceName}", ["note"] = "gaps" }),
            ["performance"] = new JsonObject { ["gmMs"] = 1, ["pkMs"] = 1, ["ratioGmOverPk"] = null, ["samples"] = 1, ["warmup"] = 0, ["scope"] = "gaps", ["correctnessQualified"] = false, ["host"] = new JsonObject(), ["configuration"] = new JsonObject(), ["note"] = "gaps" },
            ["evidence"] = new JsonArray(new JsonObject { ["label"] = "log", ["path"] = $"cases/c1/sample-0/{evidenceName}" }),
            ["notes"] = new JsonArray(), ["source"] = new JsonObject { ["path"] = "gaps", ["revision"] = "gaps" },
        }),
    }.ToJsonString());
}
void WriteIndex(string latestRunId, params (string Id, string Commit)[] runs)
{
    var index = new JsonObject
    {
        ["schemaVersion"] = 1,
        ["generatedAt"] = "now",
        ["latestRunId"] = latestRunId,
        ["runs"] = new JsonArray(runs.Select(r => (JsonNode?)new JsonObject
        {
            ["id"] = r.Id, ["commit"] = r.Commit, ["generatedAt"] = "now", ["label"] = "gaps fixture", ["manifest"] = $"runs/{r.Id}/manifest.json",
        }).ToArray()),
    };
    // Publisher-style atomic index replacement.
    File.WriteAllText(Path.Combine(cache, "index.json.tmp"), index.ToJsonString());
    File.Move(Path.Combine(cache, "index.json.tmp"), Path.Combine(cache, "index.json"), true);
}
WriteRun("r1", "sha-r1", "e.log", "R1_EVIDENCE_V1\n");
WriteIndex("r1", ("r1", "sha-r1"));
var r1IndexBytes = File.ReadAllBytes(Path.Combine(cache, "index.json"));
string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

// ------------------------------------------------------------------ helpers
var hostDll = Path.Combine(hostProject, "bin", "Release", "net10.0", "GmProgressHost.dll");
Cmd("build host", "dotnet", ["build", Path.Combine(hostProject, "GmProgressHost.csproj"), "-c", "Release", "-v", "q"], out _);
var prepareScript = Path.Combine(scripts, "PrepareHostedWebProgressSite.cs");
var activateScript = Path.Combine(scripts, "ActivateHostedWebProgressSite.cs");
var statePath = Path.Combine(work, "activation-state.json");
int Prepare(string dist) => Cmd("prepare " + Path.GetFileName(dist), "dotnet", ["run", "--file", prepareScript, "--", "--no-build", "--dist", dist, "--site", site], out _);
(int Code, string Out) Activate(string release, string data, params string[] extra)
{
    var code = Cmd("activate " + release + " " + Path.GetFileName(data), "dotnet",
        ["run", "--file", activateScript, "--", "--site", site, "--data", data, "--state", statePath, "--release", release, .. extra], out var stdout);
    return (code, stdout + "\n" + commands[^1]);
}
(int Code, string Out) Rollback() => Activate("", cache, "--rollback");
string ReleaseOf(string assetName)
{
    foreach (var meta in Directory.GetFiles(Path.Combine(site, "ui-releases"), "*.meta.json").Order())
    {
        var node = JsonNode.Parse(File.ReadAllText(meta))!;
        if (node["files"]!.AsArray().Any(f => f!["path"]!.GetValue<string>() == "assets/" + assetName)) return node["id"]!.GetValue<string>();
    }
    return "";
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
var baseUrl = $"http://127.0.0.1:{port}";
async Task<(int Status, string Body, byte[] Bytes)> Get(string path)
{
    using var response = await http.GetAsync(baseUrl + path, HttpCompletionOption.ResponseContentRead);
    var bytes = await response.Content.ReadAsByteArrayAsync();
    return ((int)response.StatusCode, Encoding.UTF8.GetString(bytes), bytes);
}
(string Status, string Body) RawGet(string rawPath)
{
    using var client = new TcpClient();
    client.Connect(IPAddress.Loopback, port);
    using var stream = client.GetStream();
    stream.Write(Encoding.ASCII.GetBytes($"GET {rawPath} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n"));
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    var text = Encoding.UTF8.GetString(buffer.ToArray());
    return (text.Split('\n')[0].TrimEnd('\r'), text);
}
Process? host = null;
void StopHost() { try { host?.Kill(entireProcessTree: true); host?.WaitForExit(10000); } catch { } host = null; }
async Task<bool> StartHost()
{
    StopHost();
    host = new Process
    {
        StartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = hostProject,
            ArgumentList = { hostDll, "--site", site, "--listen", $"127.0.0.1:{port}" },
        },
    };
    host.Start();
    host.BeginOutputReadLine();
    host.BeginErrorReadLine();
    for (var i = 0; i < 100; i++)
    {
        try { using var probe = await http.GetAsync(baseUrl + "/healthz"); return true; }
        catch { await Task.Delay(200); }
    }
    return false;
}
string SitePointer() => File.Exists(Path.Combine(site, "site.json")) ? File.ReadAllText(Path.Combine(site, "site.json")) : "";

// ------------------------------------------------------------------ H5: Prepare interruption recovery
if (Wanted("H5"))
{
    Check("H5 prepare: complete release prepared", Prepare(distOld) == 0);
    var releaseOld = JsonNode.Parse(File.ReadAllText(Directory.GetFiles(Path.Combine(site, "ui-releases"), "*.meta.json").First()))!["id"]!.GetValue<string>();
    var releaseDir = Path.Combine(site, "ui-releases", releaseOld);
    var metaPath = Path.Combine(site, "ui-releases", releaseOld + ".meta.json");
    // Interruption window of the old code: final dir renamed, metadata missing.
    File.Delete(metaPath);
    var retry = Prepare(distOld);
    var metaBack = File.Exists(metaPath);
    Check("H5: interrupted release (dir without metadata) self-heals on retry", retry == 0 && metaBack,
        $"prepareExit={retry} metaRestored={metaBack} last={commands[^1].Split('\n')[3]}");
    // Metadata without directory: also recoverable.
    Directory.Delete(releaseDir, true);
    var retry2 = Prepare(distOld);
    Check("H5: metadata-without-directory state self-heals on retry", retry2 == 0 && Directory.Exists(releaseDir), $"prepareExit={retry2}");
    // Cache hit must verify metadata, not just see a directory.
    var before = Directory.GetLastWriteTimeUtc(releaseDir);
    var retry3 = Prepare(distOld);
    Check("H5: verified cache hit is idempotent and non-destructive", retry3 == 0 && commands[^1].Contains("RELEASE_CACHED") && Directory.GetLastWriteTimeUtc(releaseDir) == before,
        $"prepareExit={retry3} last={commands[^1].Split('\n')[3]}");
}

// ------------------------------------------------------------------ H1: immutable activation of a continuously-updated cache
if (Wanted("H1"))
{
    Prepare(distOld);
    var releaseOld = ReleaseOf("old-111.js");
    var act1 = Activate(releaseOld, cache);
    Check("H1: activation of r1 succeeds", act1.Code == 0 && act1.Out.Contains("ACTIVATED"), $"exit={act1.Code}");
    await StartHost();
    var served1 = await Get("/data/index.json");
    Check("H1: activated index bytes are the validated r1 snapshot",
        served1.Status == 200 && Sha(served1.Bytes) == Sha(r1IndexBytes), $"http={served1.Status} sha={Sha(served1.Bytes)[..16]} want={Sha(r1IndexBytes)[..16]}");

    // Publisher update lands WITHOUT activation: r2 valid, then an invalid draft.
    WriteRun("r2", "sha-r2", "e.log", "R2_EVIDENCE\n");
    WriteIndex("r2", ("r1", "sha-r1"), ("r2", "sha-r2"));
    var r2IndexBytes = File.ReadAllBytes(Path.Combine(cache, "index.json"));
    var served2 = await Get("/data/index.json");
    Check("H1: un-activated publisher update is not user-visible",
        served2.Status == 200 && Sha(served2.Bytes) == Sha(r1IndexBytes), $"http={served2.Status} sha={Sha(served2.Bytes)[..16]} want={Sha(r1IndexBytes)[..16]}");

    WriteRun("r-broken", "sha-broken", "missing.log", "", createEvidence: false); // referenced file absent
    WriteIndex("r-broken", ("r-broken", "sha-broken"));
    var refused = Activate(releaseOld, cache);
    var served3 = await Get("/data/index.json");
    Check("H1: refused activation leaves the last activated snapshot serving",
        refused.Code == 3 && refused.Out.Contains("ACTIVATE_REFUSED_DATA_INVALID") && Sha(served3.Bytes) == Sha(r1IndexBytes),
        $"exit={refused.Code} sha={Sha(served3.Bytes)[..16]}");

    // Publisher recovers with a valid r2 index; activate it.
    WriteIndex("r2", ("r1", "sha-r1"), ("r2", "sha-r2"));
    var act2 = Activate(releaseOld, cache);
    var served4 = await Get("/data/index.json");
    Check("H1: second activation switches to the exact r2 snapshot",
        act2.Code == 0 && Sha(served4.Bytes) == Sha(r2IndexBytes), $"exit={act2.Code} sha={Sha(served4.Bytes)[..16]} want={Sha(r2IndexBytes)[..16]}");

    // Rollback must restore the exact r1 bytes, not "current latest of the cache".
    var rolled = Rollback();
    var served5 = await Get("/data/index.json");
    Check("H1: rollback restores the exact previously active index bytes",
        rolled.Code == 0 && Sha(served5.Bytes) == Sha(r1IndexBytes), $"exit={rolled.Code} sha={Sha(served5.Bytes)[..16]} want={Sha(r1IndexBytes)[..16]}");
    var rolledHtml = await Get("/");
    Check("H1: rollback restores the exact previously active UI", rolledHtml.Body.Contains("OLD-HTML"), "");

    // Same-runId content evolution: rewrite r1's manifest in the source cache,
    // activate a new generation, then roll back — the old generation must serve
    // the EXACT old manifest bytes captured with it.
    var oldManifest = await Get("/data/runs/r1/manifest.json");
    File.WriteAllText(Path.Combine(cache, "runs", "r1", "manifest.json"),
        "{\"schemaVersion\":1,\"run\":{\"id\":\"r1\",\"commit\":\"sha-r1\"},\"cases\":[],\"REWRITTEN\":true}\n");
    WriteIndex("r2", ("r1", "sha-r1"), ("r2", "sha-r2"));
    var act3 = Activate(releaseOld, cache);
    var rewritten = await Get("/data/runs/r1/manifest.json");
    Check("H1: rewritten run content becomes visible only with its new generation",
        act3.Code == 0 && rewritten.Body.Contains("REWRITTEN"), $"exit={act3.Code}");
    var rolled2 = Rollback();
    var restoredManifest = await Get("/data/runs/r1/manifest.json");
    var restoredIndex = await Get("/data/index.json");
    Check("H1: rollback after content evolution serves exact old run bytes",
        rolled2.Code == 0 && Sha(restoredManifest.Bytes) == Sha(oldManifest.Bytes) && Sha(restoredIndex.Bytes) == Sha(r1IndexBytes),
        $"manifestSha={Sha(restoredManifest.Bytes)[..16]} want={Sha(oldManifest.Bytes)[..16]}");

    // Source mutation after activation must not leak into serving.
    File.WriteAllText(Path.Combine(cache, "index.json"), "{\"schemaVersion\":1,\"latestRunId\":\"MUTATED\",\"runs\":[]}");
    File.AppendAllText(Path.Combine(cache, "runs", "r1", "cases", "c1", "sample-0", "e.log"), "MUTATED_SOURCE_BYTES\n");
    var served6 = await Get("/data/index.json");
    var servedLog = await Get("/data/runs/r1/cases/c1/sample-0/e.log");
    Check("H1: source cache mutation after activation is not user-visible",
        Sha(served6.Bytes) == Sha(r1IndexBytes) && servedLog.Body == "R1_EVIDENCE_V1\n",
        $"indexSha={Sha(served6.Bytes)[..16]} log={servedLog.Body.Trim()}");

    // Old-generation references stay byte-identical (cross-generation retention).
    var oldMesh = await Get("/data/runs/r1/cases/c1/sample-0/meshes/m.json");
    Check("H1: previously referenced run files stay byte-identical after later activations",
        oldMesh.Status == 200 && oldMesh.Body.Contains("r1"), $"http={oldMesh.Status}");

    // Served data must be controlled copies, never links into the mutable source.
    var storeRoot = Path.Combine(site, "data-store");
    var copied = Directory.Exists(storeRoot);
    var hardlinks = 0;
    if (copied)
    {
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(cache, "runs"), "*", SearchOption.AllDirectories).ToArray();
        foreach (var file in Directory.EnumerateFiles(storeRoot, "*", SearchOption.AllDirectories))
        {
            var length = new FileInfo(file).Length;
            foreach (var source in sourceFiles)
            {
                if (new FileInfo(source).Length == length && SameEntry(file, source)) { hardlinks++; break; }
            }
        }
    }
    Check("H1: served run files are controlled copies, not hardlinks into the source", copied && hardlinks == 0,
        $"storePresent={copied} hardlinked={hardlinks}");

    // Restore the cache for the later sections (publisher self-heal).
    File.WriteAllText(Path.Combine(cache, "runs", "r1", "cases", "c1", "sample-0", "e.log"), "R1_EVIDENCE_V1\n");
    WriteIndex("r2", ("r1", "sha-r1"), ("r2", "sha-r2"));
    StopHost();
}

// ------------------------------------------------------------------ H2: root and parent symlink swaps
if (Wanted("H2"))
{
    Prepare(distOld);
    var releaseOld = ReleaseOf("old-111.js");
    Activate(releaseOld, cache);
    await StartHost();

    // Active UI release root replaced by an outside symlink after activation.
    var releaseDir = Path.Combine(site, "ui-releases", releaseOld);
    var backup = releaseDir + ".backup";
    Directory.Move(releaseDir, backup);
    File.CreateSymbolicLink(releaseDir, Path.Combine(outside, "ui"));
    var swapped = await Get("/");
    var swappedAsset = await Get("/assets/old-111.js");
    Check("H2: active UI root swapped to outside symlink serves no outside content",
        swapped.Status != 200 || (!swapped.Body.Contains("OUTSIDE_ROOT_MARKER") && !swapped.Body.Contains("NEW-HTML") && !swapped.Body.Contains("OLD-HTML")),
        $"http={swapped.Status} body={swapped.Body.Trim()}");
    Check("H2: asset request through swapped root serves no outside content",
        swappedAsset.Status != 200 || !swappedAsset.Body.Contains("OUTSIDE_ROOT_MARKER"), $"http={swappedAsset.Status}");
    File.Delete(releaseDir);
    Directory.Move(backup, releaseDir);

    // Parent directory (ui-releases) replaced by an outside symlink.
    var releasesDir = Path.Combine(site, "ui-releases");
    var releasesBackup = releasesDir + ".backup";
    Directory.Move(releasesDir, releasesBackup);
    File.CreateSymbolicLink(releasesDir, Path.Combine(outside, "releases"));
    var parentSwap = await Get("/");
    Check("H2: ui-releases parent swapped to outside symlink serves no outside content",
        parentSwap.Status != 200 || !parentSwap.Body.Contains("OUTSIDE_ROOT_MARKER"), $"http={parentSwap.Status} body={parentSwap.Body.Trim()}");
    File.Delete(releasesDir);
    Directory.Move(releasesBackup, releasesDir);

    // Data root swapped to an outside symlink (old dataRoot or new store layout).
    var pointer = SitePointer();
    var dataTarget = pointer.Contains("\"generation\"")
        ? Path.Combine(site, "data-store")
        : JsonNode.Parse(pointer)!["dataRoot"]!.GetValue<string>();
    var dataBackup = dataTarget + ".backup";
    Directory.Move(dataTarget, dataBackup);
    File.CreateSymbolicLink(dataTarget, Path.Combine(outside, "data"));
    var dataSwap = await Get("/data/token.log");
    var dataIndex = await Get("/data/index.json");
    Check("H2: data root swapped to outside symlink serves no outside content",
        (dataSwap.Status != 200 || !dataSwap.Body.Contains("OUTSIDE_ROOT_MARKER"))
        && (dataIndex.Status != 200 || !dataIndex.Body.Contains("OUTSIDE_ROOT_MARKER")),
        $"token=http {dataSwap.Status} index=http {dataIndex.Status}");
    File.Delete(dataTarget);
    Directory.Move(dataBackup, dataTarget);

    // Activation through a swapped root must refuse.
    StopHost();
    var site2 = Path.Combine(work, "site2");
    Directory.CreateDirectory(site2);
    File.CreateSymbolicLink(Path.Combine(site2, "ui-releases"), Path.Combine(outside, "releases"));
    var prepareThroughLink = Cmd("prepare through swapped root", "dotnet",
        ["run", "--file", prepareScript, "--", "--no-build", "--dist", distOld, "--site", site2], out _);
    Check("H2: Prepare refuses to write through a symlinked releases root", prepareThroughLink != 0, $"exit={prepareThroughLink}");

    // Raw percent-encoded dot segments are rejected by the application guard.
    await StartHost();
    var rawDot = RawGet("/data/%2e%2e/index.html");
    var rawDotCode = rawDot.Status.Split(' ').ElementAtOrDefault(1) ?? "000";
    Check("H2: raw percent-encoded dot segment is rejected", rawDotCode != "200", rawDot.Status);
    var rawAbs = RawGet($"http://127.0.0.1:{port}/data/index.json");
    var rawAbsCode = rawAbs.Status.Split(' ').ElementAtOrDefault(1) ?? "000";
    Check("H2: absolute-form request target stays inside the public roots", rawAbsCode != "200" || rawAbs.Body.Contains("\"gaps"), rawAbs.Status);
    StopHost();
}

// ------------------------------------------------------------------ H3: cross-switch asset compatibility
if (Wanted("H3"))
{
    Prepare(distOld);
    Prepare(distNew);
    var releaseOld = ReleaseOf("old-111.js");
    var releaseNew = ReleaseOf("new-222.js");
    Activate(releaseOld, cache);
    await StartHost();
    var oldHtml = await Get("/");
    Check("H3: pre-switch HTML is the old release", oldHtml.Body.Contains("OLD-HTML") && oldHtml.Body.Contains("old-111.js"), "");
    Activate(releaseNew, cache);
    var delayedAsset = await Get("/assets/old-111.js");
    Check("H3: delayed asset request of already-served HTML completes across the switch",
        delayedAsset.Status == 200 && delayedAsset.Body.Contains("old-111.js payload"),
        $"http={delayedAsset.Status} body={delayedAsset.Body.Trim()}");
    var newHtml = await Get("/");
    Check("H3: index.html switches atomically (no stale, no fallback)", newHtml.Body.Contains("NEW-HTML") && !newHtml.Body.Contains("OLD-HTML"), "");
    var unknown = await Get("/assets/never-existed.js");
    Check("H3: unknown asset path does not fall back", unknown.Status == 404, $"http={unknown.Status}");
    var oldIndexNoFallback = await Get("/index.html");
    Check("H3: index.html never serves an old release copy", oldIndexNoFallback.Body.Contains("NEW-HTML"), "");
    // Rollback keeps the newer HTML's assets addressable as well.
    Rollback();
    var rollbackHtml = await Get("/");
    var delayedNewAsset = await Get("/assets/new-222.js");
    Check("H3: rollback keeps the rolled-away release's assets addressable",
        rollbackHtml.Body.Contains("OLD-HTML") && delayedNewAsset.Status == 200 && delayedNewAsset.Body.Contains("new-222.js payload"),
        $"asset http={delayedNewAsset.Status}");
    // Data references follow the same rule: a new generation that stops
    // registering a run must not break earlier HTML's data requests.
    WriteRun("r3", "sha-r3", "e3.log", "R3_EVIDENCE\n");
    WriteIndex("r3", ("r3", "sha-r3"));
    var switch3 = Activate(releaseNew, cache);
    var retainedOldLog = await Get("/data/runs/r1/cases/c1/sample-0/e.log");
    var newIndex = await Get("/data/index.json");
    Check("H3: data refs of earlier HTML stay served across generations (atomic index switch)",
        switch3.Code == 0 && retainedOldLog.Status == 200 && retainedOldLog.Body == "R1_EVIDENCE_V1\n" && newIndex.Body.Contains("\"r3\"") && !newIndex.Body.Contains("\"r1\""),
        $"log=http {retainedOldLog.Status} indexLatest={newIndex.Body[..Math.Min(60, newIndex.Body.Length)]}");
    StopHost();
}

// ------------------------------------------------------------------ H4: commit protocol and state repair
if (Wanted("H4"))
{
    Prepare(distOld);
    Prepare(distNew);
    var releaseOld = ReleaseOf("old-111.js");
    var releaseNew = ReleaseOf("new-222.js");
    Activate(releaseOld, cache);
    WriteRun("r2", "sha-r2", "e.log", "R2_EVIDENCE\n");
    WriteIndex("r2", ("r1", "sha-r1"), ("r2", "sha-r2"));

    // Deterministic derived-state failure: the state path is a directory.
    var stateDir = Path.Combine(work, "state-as-directory");
    Directory.CreateDirectory(stateDir);
    var act = Cmd("activate with state path as directory", "dotnet",
        ["run", "--file", activateScript, "--", "--site", site, "--data", cache, "--state", stateDir, "--release", releaseNew], out var actOut);
    var pointerNode = JsonNode.Parse(SitePointer());
    Check("H4: derived-state failure after commit exits cleanly and reports truthfully",
        act == 0 && actOut.Contains("ACTIVATED") && pointerNode?["uiRelease"]?.GetValue<string>() == releaseNew,
        $"exit={act} out={actOut.Split('\n').FirstOrDefault(l => l.Contains("ACTIVAT") || l.Contains("STATE"))}");

    // Retry with a normal state path must repair the derived state, not just skip.
    var retry = Activate(releaseNew, cache);
    var stateExists = File.Exists(statePath);
    var stateMatches = stateExists && JsonNode.Parse(File.ReadAllText(statePath))?["active"]?["uiRelease"]?.GetValue<string>() == releaseNew;
    Check("H4: retry repairs the derived state alongside ACTIVATE_SKIP_SAME",
        retry.Code == 0 && retry.Out.Contains("ACTIVATE_SKIP_SAME") && stateMatches,
        $"exit={retry.Code} stateExists={stateExists} stateMatches={stateMatches}");

    // The authoritative pointer carries previous + count consistently.
    var countOk = pointerNode?["activationCount"]?.GetValue<int>() is >= 2;
    var previousOk = pointerNode?["previous"]?["uiRelease"]?.GetValue<string>() is not null;
    Check("H4: pointer record carries previous activation and count consistently",
        pointerNode?["uiRelease"]?.GetValue<string>() == releaseNew && countOk && previousOk,
        $"count={pointerNode?["activationCount"]} previous={pointerNode?["previous"]?["uiRelease"]}");

    // Rollback works from the authoritative pointer even with a broken mirror.
    var rollbackCmd = Cmd("rollback with state path as directory", "dotnet",
        ["run", "--file", activateScript, "--", "--site", site, "--data", cache, "--state", stateDir, "--rollback"], out var rollbackOut);
    var afterRollback = JsonNode.Parse(SitePointer());
    Check("H4: rollback is exact from the authoritative pointer despite broken state mirror",
        rollbackCmd == 0 && afterRollback?["uiRelease"]?.GetValue<string>() == releaseOld && rollbackOut.Contains("ROLLBACK"),
        $"exit={rollbackCmd} ui={afterRollback?["uiRelease"]}");
}

// ------------------------------------------------------------------ H1b: store integrity on reuse (CAS/tree/meta/index/object faults)
if (Wanted("H1b"))
{
    // Restore the r1 fixture (later sections may have evolved the cache).
    WriteRun("r1", "sha-r1", "e.log", "R1_EVIDENCE_V1\n");
    WriteIndex("r1", ("r1", "sha-r1"));
    Prepare(distOld);
    var releaseOld = ReleaseOf("old-111.js");
    var act = Activate(releaseOld, cache);
    Check("H1b: initial activation imports and serves source bytes",
        act.Code == 0 && (act.Out.Contains("GENERATION_PREPARED") || act.Out.Contains("GENERATION_CACHED")), $"exit={act.Code}");
    var genId = JsonNode.Parse(SitePointer())!["generation"]!.GetValue<string>();
    var genMetaPath = Path.Combine(site, "generations", genId + ".meta.json");
    var genDir = Path.Combine(site, "generations", genId);
    var treeHash = JsonNode.Parse(File.ReadAllText(genMetaPath))!["runs"]!["r1"]!.GetValue<string>();
    var objectSha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(cache, "runs", "r1", "cases", "c1", "sample-0", "e.log")))).ToLowerInvariant();
    var objectPath = Path.Combine(site, "data-store", "objects", objectSha);
    var treePath = Path.Combine(site, "data-store", "trees", treeHash + ".json");
    await StartHost();

    // Normal cache hit: same input, intact store -> cached + skip, no rebuild.
    var hit = Activate(releaseOld, cache);
    Check("H1b: intact store cache hit is verified, idempotent and non-destructive",
        hit.Code == 0 && hit.Out.Contains("GENERATION_CACHED") && hit.Out.Contains("ACTIVATE_SKIP_SAME"), $"exit={hit.Code}");

    // CAS object corrupted in place (content key unchanged): must be detected
    // and repaired from the still-valid source, never served as correct.
    File.WriteAllText(objectPath, "CONTROLLED_OBJECT_STORAGE_FAULT\n");
    var casRepair = Activate(releaseOld, cache);
    var servedLog = await Get("/data/runs/r1/cases/c1/sample-0/e.log");
    Check("H1b: corrupted CAS object detected and repaired from source on reactivation",
        casRepair.Code == 0 && casRepair.Out.Contains("STORE_OBJECT_REPAIRED") && servedLog.Body == "R1_EVIDENCE_V1\n"
        && Sha(servedLog.Bytes) == objectSha,
        $"exit={casRepair.Code} body={servedLog.Body.Trim()} sha={Sha(servedLog.Bytes)[..16]}");

    // Tree record corrupted: rebuilt from verified objects, not trusted.
    File.WriteAllText(treePath, "{\"corrupt\":true}\n");
    var treeRepair = Activate(releaseOld, cache);
    var servedManifest = await Get("/data/runs/r1/manifest.json");
    Check("H1b: corrupted tree record detected and rebuilt",
        treeRepair.Code == 0 && treeRepair.Out.Contains("STORE_TREE_REPAIRED") && servedManifest.Status == 200 && servedManifest.Body.Contains("gaps fixture"),
        $"exit={treeRepair.Code} manifest=http {servedManifest.Status}");

    // Generation metadata corrupted: record rebuilt, same content identity.
    File.WriteAllText(genMetaPath, "NOT-JSON\n");
    var metaRepair = Activate(releaseOld, cache);
    var servedIndex = await Get("/data/index.json");
    Check("H1b: corrupted generation metadata detected and rebuilt",
        metaRepair.Code == 0 && metaRepair.Out.Contains("GENERATION_INCOMPLETE_REBUILDING") && Sha(servedIndex.Bytes) == Sha(r1IndexBytes),
        $"exit={metaRepair.Code} indexSha={Sha(servedIndex.Bytes)[..16]}");

    // Generation index snapshot corrupted: rebuilt from the source snapshot.
    File.WriteAllText(Path.Combine(genDir, "index.json"), "{\"corrupt\":true}\n");
    var indexRepair = Activate(releaseOld, cache);
    var servedIndex2 = await Get("/data/index.json");
    Check("H1b: corrupted generation index snapshot detected and rebuilt",
        indexRepair.Code == 0 && indexRepair.Out.Contains("GENERATION_INCOMPLETE_REBUILDING") && Sha(servedIndex2.Bytes) == Sha(r1IndexBytes),
        $"exit={indexRepair.Code} indexSha={Sha(servedIndex2.Bytes)[..16]}");

    // Missing object: restored from the source copy.
    File.Delete(objectPath);
    var restore = Activate(releaseOld, cache);
    var servedLog2 = await Get("/data/runs/r1/cases/c1/sample-0/e.log");
    Check("H1b: missing object restored from source on reactivation",
        restore.Code == 0 && restore.Out.Contains("STORE_OBJECT_RESTORED") && servedLog2.Body == "R1_EVIDENCE_V1\n",
        $"exit={restore.Code} body={servedLog2.Body.Trim()}");

    // Unreparable corruption (source no longer provides the bytes): rollback
    // must refuse the compromised generation with an explicit non-claim and
    // keep the verifiable current activation serving.
    var cache2 = Path.Combine(work, "cache2");
    WriteRun("r9", "sha-r9", "e9.log", "R9_EVIDENCE\n");
    WriteIndex("r9", ("r9", "sha-r9"));
    Directory.Move(cache, cache2);
    var act9 = Activate(releaseOld, cache2);
    Directory.Move(cache2, cache);
    File.WriteAllText(objectPath, "CONTROLLED_OBJECT_STORAGE_FAULT\n");
    File.Delete(Path.Combine(cache, "runs", "r1", "cases", "c1", "sample-0", "e.log"));
    var refuse = Rollback();
    var stillServing = await Get("/data/runs/r9/cases/c1/sample-0/e9.log");
    Check("H1b: rollback to a corrupted generation refuses without false claims",
        act9.Code == 0 && refuse.Code == 3 && refuse.Out.Contains("ACTIVATE_REFUSED_GENERATION_CORRUPT")
        && refuse.Out.Contains("integrity compromised") && stillServing.Body == "R9_EVIDENCE\n",
        $"activate9={act9.Code} rollback={refuse.Code} currentServing={stillServing.Body.Trim()}");
    StopHost();
}

// ------------------------------------------------------------------ H4b: complete mirror reconciliation across fault chains
if (Wanted("H4b"))
{
    // Restore the r1 fixture and a healthy store for this section.
    WriteRun("r1", "sha-r1", "e.log", "R1_EVIDENCE_V1\n");
    WriteIndex("r1", ("r1", "sha-r1"));
    Prepare(distOld);
    Prepare(distNew);
    var releaseOld = ReleaseOf("old-111.js");
    var releaseNew = ReleaseOf("new-222.js");
    var baseCount = JsonNode.Parse(SitePointer())?["activationCount"]?.GetValue<int>() ?? 0;
    var generationsBefore = Directory.GetFiles(Path.Combine(site, "generations"), "gen-*.meta.json").Length;
    var act1 = Activate(releaseOld, cache);
    var pointer1 = JsonNode.Parse(SitePointer());
    var mirror1 = JsonNode.Parse(File.ReadAllText(statePath));
    Check("H4b: activation writes a complete derived mirror (count + previous match pointer)",
        act1.Code == 0 && pointer1?["activationCount"]?.GetValue<int>() == baseCount + 1
        && mirror1?["activationCount"]?.GetValue<int>() == baseCount + 1
        && JsonNode.DeepEquals(mirror1?["previous"], pointer1?["previous"])
        && mirror1?["active"]?["uiRelease"]?.GetValue<string>() == releaseOld,
        $"count={pointer1?["activationCount"]}");

    // Forward activation with a failing mirror path; the pointer commits.
    var badMirror = Path.Combine(work, "mirror-as-directory");
    Directory.CreateDirectory(badMirror);
    var act2 = Cmd("H4b activate with broken mirror", "dotnet",
        ["run", "--file", activateScript, "--", "--site", site, "--data", cache, "--state", badMirror, "--release", releaseNew], out _);
    var pointer2 = JsonNode.Parse(SitePointer());
    Check("H4b: broken mirror does not block commit or truthful reporting",
        act2 == 0 && pointer2?["activationCount"]?.GetValue<int>() == baseCount + 2 && pointer2?["uiRelease"]?.GetValue<string>() == releaseNew,
        $"exit={act2} count={pointer2?["activationCount"]}");

    // Rollback with the same broken mirror path; the pointer commits again.
    var rollback2 = Cmd("H4b rollback with broken mirror", "dotnet",
        ["run", "--file", activateScript, "--", "--site", site, "--data", cache, "--state", badMirror, "--rollback"], out _);
    var pointer3 = JsonNode.Parse(SitePointer());
    Check("H4b: rollback through broken mirror restores the exact previous pair",
        rollback2 == 0 && pointer3?["activationCount"]?.GetValue<int>() == baseCount + 3 && pointer3?["uiRelease"]?.GetValue<string>() == releaseOld
        && pointer3?["previous"]?["uiRelease"]?.GetValue<string>() == releaseNew,
        $"exit={rollback2} count={pointer3?["activationCount"]}");

    // Same-pair retry with a healthy mirror path must rebuild the COMPLETE
    // derived state (count + previous), not just the active pair.
    var retry = Activate(releaseOld, cache);
    var mirror = JsonNode.Parse(File.ReadAllText(statePath));
    var pointer4 = JsonNode.Parse(SitePointer());
    Check("H4b: same-pair retry repairs full mirror (count/previous), no new activation",
        retry.Code == 0 && retry.Out.Contains("ACTIVATE_SKIP_SAME") && retry.Out.Contains("ACTIVATION_STATE_REPAIRED")
        && retry.Out.Contains("GENERATION_CACHED")
        && mirror?["activationCount"]?.GetValue<int>() == baseCount + 3
        && JsonNode.DeepEquals(mirror?["previous"], pointer4?["previous"])
        && mirror?["previous"]?["uiRelease"]?.GetValue<string>() == releaseNew
        && mirror?["active"]?["uiRelease"]?.GetValue<string>() == releaseOld
        && mirror?["active"]?["generation"]?.GetValue<string>() == pointer4?["generation"]?.GetValue<string>(),
        $"mirrorCount={mirror?["activationCount"]} pointerCount={pointer4?["activationCount"]} mirrorPrev={mirror?["previous"]?["uiRelease"]}");

    // The pointer's real activation history is unchanged by the repair.
    Check("H4b: repair does not alter authoritative counts or create generations",
        pointer4?["activationCount"]?.GetValue<int>() == baseCount + 3
        && Directory.GetFiles(Path.Combine(site, "generations"), "gen-*.meta.json").Length == generationsBefore,
        $"count={pointer4?["activationCount"]} generations={Directory.GetFiles(Path.Combine(site, "generations"), "gen-*.meta.json").Length}");
}

// ------------------------------------------------------------------ H1c: rollback target's complete logical closure
if (Wanted("H1c"))
{
    WriteRun("r0", "sha-r0", "e.log", "R0_CLOSURE\n");
    WriteRun("r1", "sha-r1", "e.log", "R1_CLOSURE\n");
    var rawDir = Path.Combine(cache, "runs", "quarantine-0");
    Directory.CreateDirectory(rawDir);
    File.WriteAllText(Path.Combine(rawDir, "raw.log"), "QUARANTINE_RAW\n");
    // This unindexed manifest is deliberately invalid. Only its safe raw log
    // is referenced; closure must not promote it into a valid indexed run.
    File.WriteAllText(Path.Combine(rawDir, "manifest.json"), "{invalid quarantined manifest");
    var sourceManifestPath = Path.Combine(cache, "runs", "r1", "manifest.json");
    var sourceManifest = JsonNode.Parse(File.ReadAllText(sourceManifestPath))!.AsObject();
    sourceManifest["cases"]![0]!["evidence"]!.AsArray().Add(new JsonObject { ["path"] = "../quarantine-0/raw.log" });
    File.WriteAllText(sourceManifestPath, sourceManifest.ToJsonString());
    WriteIndex("r1", ("r0", "sha-r0"), ("r1", "sha-r1"));
    Prepare(distOld);
    var releaseOld = ReleaseOf("old-111.js");
    var first = Activate(releaseOld, cache);
    var target = JsonNode.Parse(SitePointer())!["generation"]!.GetValue<string>();
    var metaPath = Path.Combine(site, "generations", target + ".meta.json");
    var metaOriginal = File.ReadAllText(metaPath);
    var treeHash = JsonNode.Parse(metaOriginal)!["runs"]!["r1"]!.GetValue<string>();
    var treePath = Path.Combine(site, "data-store", "trees", treeHash + ".json");
    var treeOriginal = File.ReadAllText(treePath);
    var indexPath = Path.Combine(site, "generations", target, "index.json");
    var indexOriginal = File.ReadAllText(indexPath);
    WriteRun("r2", "sha-r2", "e.log", "R2_ACTIVE\n");
    WriteIndex("r2", ("r2", "sha-r2"));
    Prepare(distNew);
    var second = Activate(ReleaseOf("new-222.js"), cache);
    Check("H1c: complete multi-run target and successor activate", first.Code == 0 && second.Code == 0);
    await StartHost();

    void Meta(Action<JsonObject> mutate)
    {
        var meta = JsonNode.Parse(metaOriginal)!.AsObject();
        mutate(meta);
        File.WriteAllText(metaPath, meta.ToJsonString());
    }
    // Use actual content keys for changed records so closure negatives cannot
    // pass merely by detecting an unrelated stale tree/object checksum.
    void Tree(Action<JsonObject> mutate)
    {
        var tree = JsonNode.Parse(treeOriginal)!.AsObject();
        mutate(tree);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var pair in tree["files"]!.AsArray())
        {
            hash.AppendData(Encoding.UTF8.GetBytes(pair![0]!.GetValue<string>()));
            hash.AppendData(Encoding.UTF8.GetBytes(pair[1]!.GetValue<string>()));
        }
        var key = Convert.ToHexStringLower(hash.GetHashAndReset());
        tree["treeHash"] = key;
        File.WriteAllText(Path.Combine(site, "data-store", "trees", key + ".json"), tree.ToJsonString());
        Meta(meta => meta["runs"]!["r1"] = key);
    }
    void Manifest(Action<JsonObject> mutate)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(sourceManifestPath))!.AsObject();
        mutate(manifest);
        var bytes = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        var sha = Sha(bytes);
        File.WriteAllBytes(Path.Combine(site, "data-store", "objects", sha), bytes);
        Tree(tree => tree["files"]!.AsArray().First(p => p![0]!.GetValue<string>() == "manifest.json")![1] = sha);
    }
    void Index(Action<JsonObject> mutate)
    {
        var index = JsonNode.Parse(indexOriginal)!.AsObject();
        mutate(index);
        var bytes = Encoding.UTF8.GetBytes(index.ToJsonString());
        File.WriteAllBytes(indexPath, bytes);
        Meta(meta => meta["indexSha256"] = Sha(bytes));
    }
    async Task Refuse(string name, Action fault, string diagnostic)
    {
        var pointerBefore = SitePointer();
        var mirrorBefore = File.ReadAllText(statePath);
        try
        {
            fault();
            var result = Rollback();
            var activeIndex = await Get("/data/index.json");
            var activeManifest = await Get("/data/runs/r2/manifest.json");
            var activeLog = await Get("/data/runs/r2/cases/c1/sample-0/e.log");
            Check("H1c: " + name, result.Code == 3
                && result.Out.Contains("ACTIVATE_REFUSED_GENERATION_CORRUPT") && result.Out.Contains(diagnostic)
                && SitePointer() == pointerBefore && File.ReadAllText(statePath) == mirrorBefore
                && activeIndex.Status == 200 && activeIndex.Body.Contains("r2")
                && activeManifest.Status == 200 && activeLog.Status == 200 && activeLog.Body == "R2_ACTIVE\n",
                $"exit={result.Code}; pointer/previous/count/mirror unchanged={SitePointer() == pointerBefore && File.ReadAllText(statePath) == mirrorBefore}");
        }
        finally
        {
            File.WriteAllText(metaPath, metaOriginal);
            File.WriteAllText(treePath, treeOriginal);
            File.WriteAllText(indexPath, indexOriginal);
        }
    }
    foreach (var value in new JsonNode?[] { null, new JsonArray(), JsonValue.Create("wrong"), JsonValue.Create(1) })
        await Refuse("runs map rejects " + (value?.ToJsonString() ?? "null"), () => Meta(m => m["runs"] = value?.DeepClone()), "expected object");
    await Refuse("missing runs map", () => Meta(m => m.Remove("runs")), "expected object");
    await Refuse("metadata missing", () => File.Delete(metaPath), target + ".meta.json");
    await Refuse("invalid metadata JSON", () => File.WriteAllText(metaPath, "{"), "generation closure:");
    await Refuse("metadata schema wrong type", () => Meta(m => m["schemaVersion"] = new JsonArray()), "generation closure:");
    await Refuse("empty required runs map", () => Meta(m => m["runs"] = new JsonObject()), "required run mapping missing");
    await Refuse("one indexed run missing", () => Meta(m => m["runs"]!.AsObject().Remove("r0")), "r0: required run mapping missing");
    await Refuse("cross-run evidence mapping missing", () => Meta(m => m["runs"]!.AsObject().Remove("quarantine-0")), "quarantine-0: required run mapping missing");
    await Refuse("tree hash wrong type", () => Meta(m => m["runs"]!["r1"] = new JsonObject()), "expected string");
    await Refuse("unsafe tree hash", () => Meta(m => m["runs"]!["r1"] = new string('/', 64)), "invalid tree hash");
    await Refuse("tree record missing", () => File.Delete(treePath), treeHash + ".json");
    await Refuse("tree key mismatch", () =>
    {
        var tree = JsonNode.Parse(treeOriginal)!;
        tree["treeHash"] = new string('0', 64);
        File.WriteAllText(treePath, tree.ToJsonString());
    }, "tree record key mismatch");
    await Refuse("duplicate tree path", () => Tree(t => t["files"]!.AsArray().Add(t["files"]![0]!.DeepClone())), "unordered or duplicate");
    await Refuse("manifest mapping missing", () => Tree(t => t["files"]!.AsArray().Remove(t["files"]!.AsArray().First(p => p![0]!.GetValue<string>() == "manifest.json"))), "manifest.json: required file missing");
    await Refuse("mesh mapping missing", () => Tree(t => t["files"]!.AsArray().Remove(t["files"]!.AsArray().First(p => p![0]!.GetValue<string>().EndsWith("m.json")))), "m.json: required file missing");
    await Refuse("log mapping missing", () => Tree(t => t["files"]!.AsArray().Remove(t["files"]!.AsArray().First(p => p![0]!.GetValue<string>().EndsWith("e.log")))), "e.log: required file missing");
    await Refuse("wrong tree from another run", () => Meta(m => m["runs"]!["r1"] = m["runs"]!["r0"]!.DeepClone()), "manifest run id mismatch");
    await Refuse("manifest identity missing", () => Manifest(m => m["run"]!.AsObject().Remove("id")), "manifest run id: expected string");
    await Refuse("manifest commit mismatch", () => Manifest(m => m["run"]!["commit"] = "wrong"), "manifest commit mismatch");
    await Refuse("manifest cases wrong type", () => Manifest(m => m["cases"] = new JsonObject()), "manifest cases: expected array");
    await Refuse("mesh reference wrong type", () => Manifest(m => m["cases"]![0]!["steps"]![0]!["gm"]!["after"] = new JsonObject()), "manifest reference: expected string");
    await Refuse("evidence path missing", () => Manifest(m => m["cases"]![0]!["evidence"]![0]!.AsObject().Remove("path")), "manifest reference: expected string");
    await Refuse("cross-run reference missing file", () => Manifest(m => m["cases"]![0]!["evidence"]![1]!["path"] = "../quarantine-0/missing.log"), "missing.log: required file missing");
    await Refuse("reference escapes runs", () => Manifest(m => m["cases"]![0]!["checks"]![0]!["evidence"] = "../../outside.log"), "invalid reference");
    await Refuse("generation metadata id mismatch", () => Meta(m => m["id"] = "gen-wrong"), "metadata id mismatch");
    await Refuse("generation latest run mismatch", () => Meta(m => m["sourceLatestRunId"] = "r0"), "latest run does not match index");
    await Refuse("index hash binding mismatch", () => Meta(m => m["indexSha256"] = new string('0', 64)), "index snapshot does not match metadata hash");
    await Refuse("index missing", () => File.Delete(indexPath), "index.json");
    await Refuse("index descriptor binds another run", () => Index(i => i["runs"]![0]!["manifest"] = "runs/r1/manifest.json"), "manifest path does not bind indexed run");
    await Refuse("latest run is not registered", () => Index(i => i["latestRunId"] = "quarantine-0"), "latestRunId not registered");
    await Refuse("empty index cannot keep a latest run", () => Index(i => i["runs"] = new JsonArray()), "latestRunId not registered");
    await Refuse("index runs wrong type", () => Index(i => i["runs"] = new JsonObject()), "index runs: expected array");
    // Valid index JSON and a matching metadata checksum do not establish the
    // old generation identity when any index bytes have changed.
    await Refuse("generation identity binds exact index bytes", () => Index(i => i["generatedAt"] = "changed"), "generation identity mismatch");
    await Refuse("generation identity binds tree hashes", () => Tree(t =>
    {
        var files = t["files"]!.AsArray();
        var logHash = files.First(p => p![0]!.GetValue<string>().EndsWith("e.log"))![1]!.DeepClone();
        files.First(p => p![0]!.GetValue<string>().EndsWith("m.json"))![1] = logHash;
    }), "generation identity mismatch");

    var rollback = Rollback();
    var exactIndex = await Get("/data/index.json");
    var oldLog = await Get("/data/runs/r1/cases/c1/sample-0/e.log");
    var rawLog = await Get("/data/runs/quarantine-0/raw.log");
    Check("H1c: intact target restores exact index and indexed/cross-run refs", rollback.Code == 0
        && rollback.Out.Contains("ROLLBACK_ACTIVATED") && exactIndex.Body == indexOriginal
        && oldLog.Status == 200 && oldLog.Body == "R1_CLOSURE\n" && rawLog.Status == 200 && rawLog.Body == "QUARANTINE_RAW\n",
        $"exit={rollback.Code}; raw quarantined manifest was not indexed");
    StopHost();
}

// ------------------------------------------------------------------ report
StopHost();
var failed = results.Where(r => !r.Ok).ToArray();
var report = new StringBuilder()
    .AppendLine($"hosting gaps regressions -- {DateTimeOffset.UtcNow:O} -- section={section}")
    .AppendJoin('\n', results.Select(r => $"{(r.Ok ? "PASS" : "FAIL")} {r.Name}{(r.Detail.Length > 0 ? " -- " + r.Detail : "")}"))
    .AppendLine().AppendLine()
    .AppendLine($"SUMMARY: {results.Count - failed.Length}/{results.Count} passed, {failed.Length} failed")
    .AppendLine().AppendLine("COMMANDS:")
    .AppendJoin('\n', commands);
var logPath = Path.Combine(logs, $"gaps-{section}.log");
File.WriteAllText(logPath, report.ToString());
Console.WriteLine();
Console.WriteLine($"SUMMARY: {results.Count - failed.Length}/{results.Count} passed, {failed.Length} failed");
Console.WriteLine("log: " + logPath);
return failed.Length == 0 ? 0 : 1;

static bool SameEntry(string a, string b)
{
    // True only when both paths are the same filesystem entry (hardlink): equal
    // (st_dev, st_ino) via lstat. Layout assumption: glibc stat on Linux
    // x86-64/arm64 starts with dev_t then ino_t (8 bytes each).
    var ba = new byte[256];
    var bb = new byte[256];
    if (LStat(a, ba) != 0 || LStat(b, bb) != 0) return false;
    return BitConverter.ToUInt64(ba, 0) == BitConverter.ToUInt64(bb, 0)
        && BitConverter.ToUInt64(ba, 8) == BitConverter.ToUInt64(bb, 8);
}
[DllImport("libc", SetLastError = true, EntryPoint = "lstat")]
static extern int LStat(string path, byte[] buffer);
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 || at + 1 >= args.Length ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
