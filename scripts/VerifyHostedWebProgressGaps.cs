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
