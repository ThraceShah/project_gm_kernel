#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseHostedWebProgressSite=true
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Substantive verification for the read-only hosted web progress site
// (web/host + Prepare/ActivateHostedWebProgressSite.cs). It exercises the real
// scripts and the real host process against real validated data and asserts:
//   * HTTP content identity for index -> manifests -> gm/pk meshes -> evidence
//     across runs (byte-for-byte, full tree checksum sweep),
//   * GET/HEAD/404/MIME/nosniff behaviour, sub-path rules, cross-run refs,
//   * path traversal / double-encoding / illegal separator / symlink escape
//     negatives and no access to repo / SDK / token / .git paths,
//   * no directory listing, no write methods,
//   * a >32 MiB controlled public resource streamed with checksum verification
//     and bounded host RSS (service capacity test, not a kernel case claim),
//   * atomic UI release activation, failure-keeps-old, data-gate refusal,
//   * same-run skip, activation retry without regeneration, restart persistence,
//   * pointer-swap atomicity under concurrent readers.
// Commands and exit codes are recorded under temp_docs/web-progress-hosting/logs/.
//
// Usage (all paths resolve against this script's own directory):
//   dotnet run --file scripts/VerifyHostedWebProgressSite.cs -- \
//     [--real-data ../web/data] [--work ../temp_docs/web-progress-hosting] \
//     [--port 8792] [--tailnet-ip 100.97.117.95] [--big-mib 64]
var scripts = Path.GetDirectoryName(ScriptPath())!;
var realData = Path.GetFullPath(Option("--real-data", "../web/data"), scripts);
var work = Path.GetFullPath(Option("--work", "../temp_docs/web-progress-hosting"), scripts);
var port = int.Parse(Option("--port", "8792"));
var tailnetIp = Option("--tailnet-ip", "");
var bigMib = int.Parse(Option("--big-mib", "64"));
var dist = Path.GetFullPath("../web/dist", scripts);
var hostProject = Path.GetFullPath("../web/host", scripts);
var logs = Path.Combine(work, "logs");
var fixtures = Path.Combine(work, "fixtures");
var site = Path.Combine(work, "site");
var statePath = Path.Combine(work, "activation-state.json");
Directory.CreateDirectory(logs);

var results = new List<(string Name, bool Ok, string Detail)>();
void Check(string name, bool ok, string detail = "") { results.Add((name, ok, detail)); Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? " -- " + detail : "")); }
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

// Raw HTTP/1.1 over a socket: sends the request target exactly as written so
// server-side path handling is tested without client-side dot-segment or
// backslash normalization.
(string Status, string Body) RawGet(string rawPath)
{
    using var client = new TcpClient();
    client.Connect(IPAddress.Loopback, port);
    using var stream = client.GetStream();
    var request = Encoding.ASCII.GetBytes($"GET {rawPath} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n");
    stream.Write(request);
    using var buffer = new MemoryStream();
    stream.CopyTo(buffer);
    var text = Encoding.UTF8.GetString(buffer.ToArray());
    var lines = text.Split('\n');
    return (lines[0].TrimEnd('\r'), text);
}

// ---------------------------------------------------------------- fixtures
Console.WriteLine("[verify] fixtures in " + work);
if (Directory.Exists(fixtures)) Directory.Delete(fixtures, true);
if (Directory.Exists(site)) Directory.Delete(site, true);
if (File.Exists(statePath)) File.Delete(statePath);
Directory.CreateDirectory(fixtures);
var dataReal = Path.Combine(fixtures, "data-real");
CopyTree(realData, dataReal);
var fingerprintBefore = TreeFingerprint(dataReal);
var realClosure = HostedWebProgressSite.CheckDataRoot(dataReal);
Check("fixture: real validated cache passes host closure gate",
    realClosure["errors"]!.AsArray().Count == 0, realClosure.ToJsonString());

// Locate a real case with meshes for cross-run references and sweep checks.
var realIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(dataReal, "index.json")))!;
var realLatest = realIndex["latestRunId"]!.GetValue<string>();
var realRunDir = Path.Combine(dataReal, "runs", realLatest);
var realManifest = JsonNode.Parse(File.ReadAllText(Path.Combine(realRunDir, "manifest.json")))!;
var realCase = realManifest["cases"]!.AsArray().First(c => c!["steps"]!.AsArray().Count > 0 && c["steps"]![0]!["gm"]!["before"] != null)!;
var realCaseId = realCase["id"]!.GetValue<string>();
var crossMesh = realCase["steps"]![0]!["gm"]!["before"]!.GetValue<string>();
var crossEvidence = realCase["evidence"]!.AsArray().FirstOrDefault(e => e!["path"]!.GetValue<string>().EndsWith(".log"))?["path"]?.GetValue<string>();
if (crossEvidence is null || !File.Exists(Path.Combine(realRunDir, crossEvidence.Replace('/', Path.DirectorySeparatorChar))))
{
    crossEvidence = Directory.EnumerateFiles(realRunDir, "oracle.log", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(realRunDir, f).Replace('\\', '/')).FirstOrDefault()!;
}
if (crossEvidence is null || !File.Exists(Path.Combine(realRunDir, crossEvidence.Replace('/', Path.DirectorySeparatorChar))))
{
    Console.Error.WriteLine("[verify] real run has no .log evidence to cross-reference");
    return 2;
}

// data-cap: real runs + a synthetic run carrying a >32 MiB controlled evidence
// resource and cross-run mesh/evidence references. This is a service capacity
// fixture, NOT a kernel/graphics case result.
var dataCap = Path.Combine(fixtures, "data-cap");
CopyTree(dataReal, dataCap);
var capRunId = "xrun-cap-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
var capRunDir = Path.Combine(dataCap, "runs", capRunId);
var capCaseId = "capacity.synthetic-large-evidence";
var capSample = Path.Combine(capRunDir, "cases", capCaseId, "sample-0");
Directory.CreateDirectory(Path.Combine(capSample, "meshes"));
var bigRel = $"cases/{capCaseId}/sample-0/capacity-evidence.log";
var bigPath = Path.Combine(capRunDir, bigRel.Replace('/', Path.DirectorySeparatorChar));
var bigBytes = bigMib * 1024L * 1024L;
using (var stream = File.Create(bigPath))
{
    var block = new byte[1024 * 1024];
    for (var offset = 0; offset < bigBytes; offset += block.Length)
    {
        for (var i = 0; i < block.Length; i++) block[i] = (byte)((offset + i) * 31);
        stream.Write(block, 0, block.Length);
    }
}
var crossRunPrefix = "../" + realLatest + "/";
var capCase = new JsonObject
{
    ["id"] = capCaseId,
    ["title"] = "Service capacity fixture: >32 MiB controlled public resource (not a kernel case)",
    ["family"] = "blend",
    ["operation"] = "capacity-fixture",
    ["status"] = "diagnostic",
    ["parameters"] = new JsonObject { ["note"] = "hosting capacity test only" },
    ["apis"] = new JsonObject
    {
        ["gm"] = new JsonObject { ["name"] = "none", ["parameters"] = new JsonObject() },
        ["pk"] = new JsonObject { ["name"] = "none", ["parameters"] = new JsonObject() },
    },
    ["steps"] = new JsonArray(new JsonObject
    {
        ["id"] = "capacity-1",
        ["title"] = "cross-run reference closure",
        ["gm"] = new JsonObject { ["before"] = crossRunPrefix + crossMesh, ["after"] = crossRunPrefix + crossMesh },
        ["pk"] = new JsonObject { ["before"] = crossRunPrefix + crossMesh, ["after"] = crossRunPrefix + crossMesh },
    }),
    ["checks"] = new JsonArray(new JsonObject
    {
        ["id"] = "capacity-check",
        ["label"] = "streaming capacity check",
        ["status"] = "diagnostic",
        ["gm"] = null,
        ["pk"] = null,
        ["absDelta"] = 0,
        ["normalizedDelta"] = 0,
        ["threshold"] = 0,
        ["unit"] = "bytes",
        ["evidence"] = bigRel,
        ["note"] = "capacity fixture; no kernel verdict",
    }),
    ["performance"] = new JsonObject
    {
        ["gmMs"] = null, ["pkMs"] = null, ["ratioGmOverPk"] = null, ["samples"] = 0, ["warmup"] = 0,
        ["scope"] = "none", ["correctnessQualified"] = false, ["host"] = new JsonObject(),
        ["configuration"] = new JsonObject(), ["note"] = "capacity fixture; no performance claim",
    },
    ["evidence"] = new JsonArray(
        new JsonObject { ["label"] = "Controlled large evidence resource", ["path"] = bigRel },
        new JsonObject { ["label"] = "Cross-run real oracle log", ["path"] = crossRunPrefix + crossEvidence }),
    ["notes"] = new JsonArray("Hosting capacity fixture generated by scripts/VerifyHostedWebProgressSite.cs; not a measured case."),
    ["source"] = new JsonObject { ["path"] = "scripts/VerifyHostedWebProgressSite.cs", ["revision"] = "verify" },
};
var capManifest = new JsonObject
{
    ["schemaVersion"] = 1,
    ["run"] = new JsonObject
    {
        ["id"] = capRunId, ["commit"] = "capacity-fixture", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        ["pkVersion"] = null, ["host"] = new JsonObject(), ["configuration"] = new JsonObject(),
        ["scope"] = "hosting-capacity-fixture", ["provenance"] = new JsonObject(),
    },
    ["cases"] = new JsonArray(capCase),
};
Directory.CreateDirectory(capRunDir);
File.WriteAllText(Path.Combine(capRunDir, "manifest.json"), capManifest.ToJsonString());
var capIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(dataCap, "index.json")))!.AsObject();
var capRuns = capIndex["runs"]!.AsArray();
capRuns.Insert(0, new JsonObject
{
    ["id"] = capRunId, ["commit"] = "capacity-fixture", ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
    ["label"] = "Hosting capacity fixture (synthetic, not a kernel run)", ["manifest"] = $"runs/{capRunId}/manifest.json",
});
capIndex["latestRunId"] = capRunId;
File.WriteAllText(Path.Combine(dataCap, "index.json"), capIndex.ToJsonString());
var capClosure = HostedWebProgressSite.CheckDataRoot(dataCap);
Check("fixture: multi-run dataset with cross-run refs passes closure gate (incl. >32MiB resource)",
    capClosure["errors"]!.AsArray().Count == 0 && bigBytes > 32L * 1024 * 1024,
    $"runs={capClosure["runs"]} refs={capClosure["references"]} bigBytes={bigBytes}");

// broken-data-missing-ref: the S7-style gap -- source valid, destination missing
// a cross-run target. The host activation gate must refuse it.
var brokenRef = Path.Combine(fixtures, "broken-missing-ref");
CopyTree(dataCap, brokenRef);
File.Delete(Path.Combine(brokenRef, "runs", realLatest, crossMesh.Replace('/', Path.DirectorySeparatorChar)));
var brokenRefClosure = HostedWebProgressSite.CheckDataRoot(brokenRef);
Check("fixture/gate: missing cross-run reference target refused (publish-closure gap)",
    brokenRefClosure["errors"]!.AsArray().Count > 0
        && brokenRefClosure["errors"]!.AsArray().Any(e => e!.GetValue<string>().Contains("missing or escaping")),
    brokenRefClosure.ToJsonString());

// broken-data-symlink: a symlink planted into the served tree must be refused.
var brokenLink = Path.Combine(fixtures, "broken-symlink");
CopyTree(dataReal, brokenLink);
var secretFile = Path.Combine(fixtures, "outside-secret.log");
File.WriteAllText(secretFile, "OUTSIDE_SECRET_MARKER must never be served\n");
File.CreateSymbolicLink(Path.Combine(brokenLink, "runs", "external.log"), secretFile);
var brokenLinkClosure = HostedWebProgressSite.CheckDataRoot(brokenLink);
Check("fixture/gate: symlink inside data root refused",
    brokenLinkClosure["errors"]!.AsArray().Count > 0
        && brokenLinkClosure["errors"]!.AsArray().Any(e => e!.GetValue<string>().Contains("reparse point")),
    brokenLinkClosure.ToJsonString());

// ---------------------------------------------------------------- prepare
if (!File.Exists(Path.Combine(dist, "index.html")))
{
    Console.Error.WriteLine("[verify] web/dist is missing; run scripts/PrepareHostedWebProgressSite.cs first");
    return 2;
}
var dist2 = Path.Combine(fixtures, "dist-ui-update");
CopyTree(dist, dist2);
File.AppendAllText(Path.Combine(dist2, "index.html"), "\n<!-- ui-update-marker -->\n");
var distNoIndex = Path.Combine(fixtures, "dist-no-index");
CopyTree(dist, distNoIndex);
File.Delete(Path.Combine(distNoIndex, "index.html"));
var distSymlink = Path.Combine(fixtures, "dist-symlink");
CopyTree(dist, distSymlink);
File.CreateSymbolicLink(Path.Combine(distSymlink, "escape.txt"), secretFile);
var distUnsafe = Path.Combine(fixtures, "dist-unsafe-name");
CopyTree(dist, distUnsafe);
File.WriteAllText(Path.Combine(distUnsafe, "bad name.txt"), "unsafe");

var prepareScript = Path.Combine(scripts, "PrepareHostedWebProgressSite.cs");
int Prepare(string distPath, out string stdout) => Cmd("prepare " + Path.GetFileName(distPath), "dotnet",
    ["run", "--file", prepareScript, "--", "--no-build", "--dist", distPath, "--site", site], out stdout);
string ReleaseId(string output) => output.Split('\n').Select(l => l.Split(' ')).First(p => p.Length >= 2 && p[0] is "RELEASE_PREPARED" or "RELEASE_CACHED")[1];
Check("prepare: refuses dist without index.html", Prepare(distNoIndex, out _) != 0);
Check("prepare: refuses dist containing symlinks", Prepare(distSymlink, out _) != 0);
Check("prepare: refuses unsafe file names in dist", Prepare(distUnsafe, out _) != 0);
Check("prepare: immutable UI release R1 prepared", Prepare(dist, out var prepared1) == 0 && prepared1.Contains("RELEASE_PREPARED"), prepared1.Trim());
var release1 = ReleaseId(prepared1);
Check("prepare: idempotent re-run is a cache hit", Prepare(dist, out var prepared1Again) == 0 && prepared1Again.Contains("RELEASE_CACHED"), prepared1Again.Trim());
Check("prepare: UI update prepared as new release R2", Prepare(dist2, out var prepared2) == 0 && prepared2.Contains("RELEASE_PREPARED"), prepared2.Trim());
var release2 = ReleaseId(prepared2);
var releaseIds = Directory.GetFiles(Path.Combine(site, "ui-releases"), "*.meta.json")
    .Select(Path.GetFileNameWithoutExtension).Select(n => n![..^".meta".Length]).Order().ToArray();
Check("prepare: two distinct checksummed releases retained (rollback material)",
    release1 != release2 && releaseIds.Length == 2 && releaseIds.Contains(release1) && releaseIds.Contains(release2),
    string.Join(", ", releaseIds));

// ---------------------------------------------------------------- activate
var activateScript = Path.Combine(scripts, "ActivateHostedWebProgressSite.cs");
int Activate(string release, string data, params string[] extra) =>
    Cmd("activate " + release + " " + Path.GetFileName(data), "dotnet",
        ["run", "--file", activateScript, "--", "--site", site, "--data", data, "--state", statePath, "--release", release, .. extra], out _);
Check("activate: broken data (missing cross-run target) refused, exit 3", Activate(release1, brokenRef) == 3);
Check("activate: symlinked data root refused, exit 3", Activate(release1, brokenLink) == 3);
Check("activate: unknown release refused, exit 3", Activate("no-such-release", dataReal) == 3);
Check("activate: --expect-run mismatch refused, exit 3", Activate(release1, dataReal, "--expect-run", "wrong-run") == 3);
Check("activate: site pointer untouched after refusals", !File.Exists(Path.Combine(site, "site.json")));
Check("activate: R1 + real data activated", Activate(release1, dataReal) == 0);
Check("activate: same run + same release skipped", Activate(release1, dataReal) == 0 && commands[^1].Contains("ACTIVATE_SKIP_SAME"), commands[^1].Split('\n')[3]);
Check("activate: activation state separate from watch/publish state",
    HostedWebProgressSite.ReadJsonObject(statePath)?["active"]?["dataRunId"]?.GetValue<string>() == realLatest
    && !File.Exists(Path.Combine(work, "watch-state.json")) && !File.Exists(Path.Combine(work, "publish-state.json")));
Check("activate: R1 + capacity dataset activated (superset incl. real runs)", Activate(release1, dataCap) == 0);

// ---------------------------------------------------------------- host process
var hostDll = Path.Combine(hostProject, "bin", "Release", "net10.0", "GmProgressHost.dll");
Cmd("build host", "dotnet", ["build", Path.Combine(hostProject, "GmProgressHost.csproj"), "-c", "Release", "-v", "q"], out _);
if (!File.Exists(hostDll)) { Console.Error.WriteLine("[verify] host build failed"); return 2; }
if (tailnetIp.Length == 0)
{
    Cmd("tailscale ip", "tailscale", ["ip", "-4"], out var tailscaleOut);
    tailnetIp = tailscaleOut.Trim().Split('\n')[0].Trim();
}
if (!IPAddress.TryParse(tailnetIp, out var tailnetAddress))
{
    Console.Error.WriteLine("[verify] cannot determine Tailscale IP (got '" + tailnetIp + "'); pass --tailnet-ip");
    return 2;
}
EnsurePortFree(IPAddress.Loopback, port);
EnsurePortFree(tailnetAddress, port);
var hostLog = Path.Combine(logs, "host-process.log");
using var hostOutput = File.Create(hostLog);
var host = new Process
{
    StartInfo = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = hostProject,
    },
};
foreach (var argument in new[] { hostDll, "--site", site, "--listen", $"127.0.0.1:{port}", "--listen", $"{tailnetIp}:{port}" })
    host.StartInfo.ArgumentList.Add(argument);
host.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (hostOutput) { var b = Encoding.UTF8.GetBytes(e.Data + "\n"); hostOutput.Write(b); } };
host.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (hostOutput) { var b = Encoding.UTF8.GetBytes(e.Data + "\n"); hostOutput.Write(b); } };
host.Start();
host.BeginOutputReadLine();
host.BeginErrorReadLine();
commands.Add($"$ dotnet {hostDll} --site {site} --listen 127.0.0.1:{port} --listen {tailnetIp}:{port}\n[host pid {host.Id}, journal: logs/host-process.log]");

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
var baseUrl = $"http://127.0.0.1:{port}";
async Task<HttpResponseMessage> Get(string path, string method = "GET", HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
{
    using var request = new HttpRequestMessage(new HttpMethod(method), baseUrl + path);
    return await http.SendAsync(request, completion);
}
async Task<bool> WaitForHost()
{
    for (var i = 0; i < 100; i++)
    {
        try { using var response = await Get("/healthz"); return true; }
        catch { await Task.Delay(200); }
    }
    return false;
}
Check("host: starts and answers health probe", await WaitForHost());

// Deployment-shaped bind: the portal on the other node reaches the Tailscale IP.
using (var tailnetHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
{
    try
    {
        using var response = await tailnetHttp.GetAsync($"http://{tailnetIp}:{port}/healthz");
        Check("host: reachable on Tailscale IP bind (portal proxy shape)", response.IsSuccessStatusCode,
            $"http://{tailnetIp}:{port}/healthz -> {(int)response.StatusCode} ui={(response.Headers.TryGetValues("X-Gm-Ui-Release", out var v) ? v.First() : "-")}");
    }
    catch (Exception e) { Check("host: reachable on Tailscale IP bind (portal proxy shape)", false, e.Message); }
}

// ---------------------------------------------------------------- http: content identity
string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
async Task<(bool Ok, string Detail)> SameContent(string urlPath, string filePath)
{
    var disk = await File.ReadAllBytesAsync(filePath);
    using var response = await Get(urlPath);
    if (!response.IsSuccessStatusCode) return (false, $"HTTP {(int)response.StatusCode}");
    var body = await response.Content.ReadAsByteArrayAsync();
    var lengthOk = response.Content.Headers.ContentLength == disk.LongLength;
    return (body.AsSpan().SequenceEqual(disk) && lengthOk, $"http={Sha256(body)[..16]} disk={Sha256(disk)[..16]} bytes={disk.Length}");
}

// Full multi-run dataset sweep: every served data file over HTTP, checksummed.
var sweepFailures = new List<string>();
var sweepCount = 0;
long sweepBytes = 0;
foreach (var file in Directory.EnumerateFiles(Path.Combine(dataCap, "runs"), "*", SearchOption.AllDirectories))
{
    var relative = Path.GetRelativePath(dataCap, file).Replace('\\', '/');
    var (ok, detail) = await SameContent("/data/" + relative, file);
    sweepCount++;
    sweepBytes += new FileInfo(file).Length;
    if (!ok) sweepFailures.Add(relative + " " + detail);
}
var (rootIndexOk, rootIndexDetail) = await SameContent("/data/index.json", Path.Combine(dataCap, "index.json"));
Check($"http: multi-run dataset streamed with identical checksums ({sweepCount} files, {sweepBytes / 1024 / 1024} MiB incl. {bigMib} MiB resource)",
    sweepFailures.Count == 0 && rootIndexOk, sweepFailures.Count == 0 ? rootIndexDetail : string.Join("; ", sweepFailures.Take(3)));
var (manifestOk, manifestDetail) = await SameContent($"/data/runs/{capRunId}/manifest.json", Path.Combine(capRunDir, "manifest.json"));
Check("http: index->manifest content identity", rootIndexOk && manifestOk, manifestDetail);

// Real run gm+pk meshes and evidence content identity (double-sided).
var realMeshSamples = new[] { crossMesh, crossMesh.Replace("gm-0-before", "pk-0-before"), crossEvidence };
var sampleFailures = new List<string>();
foreach (var sample in realMeshSamples)
{
    var path = Path.Combine(realRunDir, sample.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(path)) { sampleFailures.Add(sample + " (absent)"); continue; }
    var (ok, detail) = await SameContent($"/data/runs/{realLatest}/{sample}", path);
    if (!ok) sampleFailures.Add(sample + " " + detail);
}
Check("http: real run gm/pk mesh + evidence content identity", sampleFailures.Count == 0, string.Join("; ", sampleFailures));

// HEAD semantics.
var headOk = true;
var headDetail = "";
foreach (var sample in new[] { "/data/index.json", $"/data/runs/{realLatest}/{crossMesh}", $"/data/runs/{capRunId}/{bigRel}" })
{
    using var response = await Get(sample, "HEAD");
    var body = await response.Content.ReadAsByteArrayAsync();
    var diskSize = new FileInfo(Path.Combine(dataCap, sample["/data/".Length..].Replace('/', Path.DirectorySeparatorChar))).Length;
    if (!response.IsSuccessStatusCode || body.Length != 0 || response.Content.Headers.ContentLength != diskSize)
    {
        headOk = false;
        headDetail += $"{sample}: {(int)response.StatusCode} body={body.Length} len={response.Content.Headers.ContentLength} disk={diskSize}; ";
    }
}
Check("http: HEAD returns headers only with exact Content-Length", headOk, headDetail);

// MIME + nosniff.
var mimeChecks = new (string Path, string Expect)[]
{
    ("/data/index.json", "application/json"),
    ($"/data/runs/{capRunId}/{bigRel}", "text/plain"),
    ("/index.html", "text/html"),
};
var mimeOk = true;
var mimeDetail = "";
foreach (var (path, expect) in mimeChecks)
{
    using var response = await Get(path);
    var type = response.Content.Headers.ContentType?.MediaType ?? "-";
    var nosniff = response.Headers.TryGetValues("X-Content-Type-Options", out var ns) && ns.Contains("nosniff");
    if (!type.StartsWith(expect, StringComparison.Ordinal) || !nosniff) { mimeOk = false; mimeDetail += $"{path}: {type} nosniff={nosniff}; "; }
}
foreach (var asset in Directory.GetFiles(Path.Combine(site, "ui-releases", release2, "assets")))
{
    using var response = await Get("/assets/" + Path.GetFileName(asset));
    var type = response.Content.Headers.ContentType?.MediaType ?? "-";
    if (!(type is "text/javascript" or "text/css" or "application/javascript")) { mimeOk = false; mimeDetail += $"asset {Path.GetFileName(asset)}: {type}; "; }
}
Check("http: MIME types + X-Content-Type-Options: nosniff", mimeOk, mimeDetail);

// No directory listing / no missing fallthrough.
var listingOk = true;
var listingDetail = "";
foreach (var path in new[] { "/data/runs/", "/data", "/data/", "/data/index.html", $"/data/runs/{realLatest}/cases/", "/data/does-not-exist.json", "/does-not-exist" })
{
    using var response = await Get(path);
    var body = await response.Content.ReadAsStringAsync();
    if (response.IsSuccessStatusCode || body.Contains("Index of") || body.Contains("<dir")) { listingOk = false; listingDetail += $"{path}: {(int)response.StatusCode}; "; }
}
Check("http: no directory listing, missing paths are 4xx", listingOk, listingDetail);

// Read-only verbs.
var verbsOk = true;
var verbsDetail = "";
foreach (var method in new[] { "POST", "PUT", "DELETE", "PATCH", "TRACE", "OPTIONS" })
{
    using var response = await Get("/data/index.json", method);
    if (response.StatusCode != HttpStatusCode.MethodNotAllowed) { verbsOk = false; verbsDetail += $"{method}: {(int)response.StatusCode}; "; }
    var allow = response.Headers.TryGetValues("Allow", out var h) ? string.Join(",", h)
        : response.Content.Headers.TryGetValues("Allow", out var c) ? string.Join(",", c) : "";
    var tokens = allow.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (!(tokens.Contains("GET") && tokens.Contains("HEAD"))) { verbsOk = false; verbsDetail += $"{method}: Allow={allow}; "; }
}
Check("http: only GET/HEAD served, writes rejected with 405 + Allow", verbsOk, verbsDetail);

// Security negatives: traversal, double-encoding, separators, repo/SDK/token
// paths. Request targets are sent raw (see RawGet) so client-side URI
// normalization cannot mask server behaviour.
var negatives = new[]
{
    "/data/../../../etc/passwd", "/data/..%2f..%2f..%2fetc/passwd", "/%2e%2e%2fetc/passwd",
    "/%252e%252e%252fetc/passwd", "/data/%252e%252e/secret", "/data/runs/..%5c..%5csecret",
    "/data\\..\\..\\secret", "/data/runs/%00", "/data/runs/x%3b/etc/passwd", "/..;/secret",
    "//etc/passwd", "/./../../secret", $"/data/runs/{realLatest}/../../{capRunId}/../../index.json",
    "/.git/config", "/AGENTS.md", "/docs/Kernel_PRD.md", "/scripts/AbiSmoke.cs",
    "/web/package.json", "/temp_docs/web-progress-hosting/activation-state.json",
    "/portal-publish-token", "/third_party/parasolid/lib/linux-x64/libpskernel.so",
    "/data/../site.json", "/data/runs/../../site.json",
};
var negativeFailures = new List<string>();
foreach (var path in negatives)
{
    var (status, body) = RawGet(path);
    var code = status.Split(' ').ElementAtOrDefault(1) ?? "000";
    if (code is "200" || body.Contains("OUTSIDE_SECRET_MARKER") || body.Contains("root:") || body.Contains("publish-token"))
        negativeFailures.Add($"{path}: {status}");
}
Check($"http: {negatives.Length} traversal/encoding/repo/token negatives rejected", negativeFailures.Count == 0, string.Join("; ", negativeFailures.Take(3)));

// Runtime symlink escape: a link planted AFTER activation into a served root
// must not be readable ("validated" roots are not blindly trusted).
File.CreateSymbolicLink(Path.Combine(dataCap, "runs", "planted-escape.log"), secretFile);
using (var response = await Get($"/data/runs/planted-escape.log"))
{
    var body = await response.Content.ReadAsStringAsync();
    Check("http: symlink planted after activation is not served", !response.IsSuccessStatusCode && !body.Contains("OUTSIDE_SECRET_MARKER"), $"HTTP {(int)response.StatusCode}");
}
File.Delete(Path.Combine(dataCap, "runs", "planted-escape.log"));

// Cross-run reference resolution as the browser sees it: ../<runId>/... from the
// synthetic manifest URL resolves to the real run's file, byte identical.
var resolved = new Uri(new Uri(baseUrl + $"/data/runs/{capRunId}/manifest.json"), crossRunPrefix + crossMesh).PathAndQuery;
var (crossOk, crossDetail) = await SameContent(resolved, Path.Combine(realRunDir, crossMesh.Replace('/', Path.DirectorySeparatorChar)));
Check("http: cross-run mesh reference resolves inside runs/ tree, content identical", crossOk, resolved + " " + crossDetail);

// Caching contract.
var cacheChecks = new (string Path, bool Immutable)[]
{
    ("/data/index.json", false), ($"/data/runs/{capRunId}/manifest.json", false), ("/index.html", false),
    ($"/data/runs/{realLatest}/{crossMesh}", true), ($"/data/runs/{capRunId}/{bigRel}", true),
};
var cacheOk = true;
var cacheDetail = "";
foreach (var (path, immutable) in cacheChecks)
{
    using var response = await Get(path);
    var cache = response.Headers.TryGetValues("Cache-Control", out var cc) ? string.Join(",", cc) : "-";
    var good = immutable ? cache.Contains("immutable") : cache.Contains("no-cache");
    if (!good) { cacheOk = false; cacheDetail += $"{path}: {cache}; "; }
}
foreach (var asset in Directory.GetFiles(Path.Combine(site, "ui-releases", release2, "assets")))
{
    using var response = await Get("/assets/" + Path.GetFileName(asset));
    var cache = response.Headers.TryGetValues("Cache-Control", out var acc) ? string.Join(",", acc) : "-";
    if (!cache.Contains("immutable")) { cacheOk = false; cacheDetail += $"asset: {cache}; "; }
}
Check("http: index/manifest no-cache, immutable meshes/assets cacheable", cacheOk, cacheDetail);

// ---------------------------------------------------------------- streaming capacity
var bigHashDisk = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(bigPath))).ToLowerInvariant();
long received = 0;
var maxRssKb = 0L;
using (var response = await Get($"/data/runs/{capRunId}/{bigRel}", "GET", HttpCompletionOption.ResponseHeadersRead))
{
    Check("stream: >32MiB resource served with Content-Length",
        response.IsSuccessStatusCode && response.Content.Headers.ContentLength == bigBytes,
        $"HTTP {(int)response.StatusCode} Content-Length={response.Content.Headers.ContentLength} disk={bigBytes}");
    using var stream = await response.Content.ReadAsStreamAsync();
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var buffer = new byte[128 * 1024];
    int read;
    while ((read = await stream.ReadAsync(buffer)) > 0)
    {
        hash.AppendData(buffer, 0, read);
        received += read;
        maxRssKb = Math.Max(maxRssKb, HostRssKb(host.Id));
    }
    var same = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() == bigHashDisk;
    Check("stream: full streamed checksum matches disk (service capacity test, not a kernel case)",
        same && received == bigBytes, $"received={received} maxVmRSS={maxRssKb / 1024}MiB");
}
Check("stream: host memory bounded while streaming", maxRssKb > 0 && maxRssKb < 512 * 1024, $"maxVmRSS={maxRssKb / 1024}MiB while streaming {bigMib}MiB");

// ---------------------------------------------------------------- atomic activation semantics
// Failure keeps the previous root.
Check("activate: failure keeps previous UI release serving", Activate("no-such-release", dataReal) == 3);
var activeUi = (await Get("/healthz")).Headers.TryGetValues("X-Gm-Ui-Release", out var uiNow) ? uiNow.First() : "";
Check("activate: previous release still active after refusal", activeUi == release1, $"active={activeUi}");

// Data validation failure must not switch the data index.
var servedBeforeRefusal = JsonNode.Parse(await http.GetStringAsync(baseUrl + "/data/index.json"))!["latestRunId"]!.GetValue<string>();
Check("activate: data validation failure refuses index switch", Activate(release2, brokenRef) == 3);
using (var response = await Get("/data/index.json"))
{
    var body = await response.Content.ReadAsStringAsync();
    Check("activate: served data index unchanged after refusal",
        JsonNode.Parse(body)!["latestRunId"]!.GetValue<string>() == servedBeforeRefusal, body[..Math.Min(80, body.Length)]);
}

// UI release update activates atomically; rollback restores the previous release.
Check("activate: UI release update R2 activates", Activate(release2, dataCap) == 0);
Check("activate: new release content served", (await http.GetStringAsync(baseUrl + "/")).Contains("ui-update-marker"));
Check("activate: rollback restores previous release", Rollback());
var rolled = await http.GetStringAsync(baseUrl + "/");
Check("activate: rolled-back content served again", !rolled.Contains("ui-update-marker"));

// Retry after failure uses the existing validated cache without regeneration.
Check("activate: retry after refusal succeeds", Activate(release1, dataReal) == 0);
Check("activate: retry used existing validated cache (source tree untouched)", TreeFingerprint(dataReal) == fingerprintBefore);

// Same-run skip for a repeated identical activation.
Check("activate: same-run skip for repeated real activation", Activate(release1, dataReal) == 0 && commands[^1].Contains("ACTIVATE_SKIP_SAME"), commands[^1].Split('\n')[3]);

// Pointer-swap atomicity under concurrent readers.
var flipFailures = new List<string>();
var flipCount = 0;
var stopReaders = false;
var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
{
    while (!Volatile.Read(ref stopReaders))
    {
        try
        {
            using var response = await Get("/data/index.json");
            var body = await response.Content.ReadAsByteArrayAsync();
            var node = JsonNode.Parse(body);
            var id = node?["latestRunId"]?.GetValue<string>();
            if (id != realLatest && id != capRunId) lock (flipFailures) flipFailures.Add("unexpected run " + id);
        }
        catch (Exception e) { lock (flipFailures) flipFailures.Add(e.GetType().Name + ": " + e.Message); }
    }
})).ToArray();
for (var i = 0; i < 12; i++)
{
    var target = i % 2 == 0 ? dataCap : dataReal;
    var result = HostedWebProgressSite.Activate(site, release1, target, statePath);
    if (result["status"]?.GetValue<string>() is not ("activated" or "skip-same")) flipFailures.Add("activate failed: " + result.ToJsonString());
    flipCount++;
}
Volatile.Write(ref stopReaders, true);
await Task.WhenAll(readers);
Check($"atomic: pointer swaps under concurrent readers always complete ({flipCount} flips, 4 readers)", flipFailures.Count == 0, string.Join("; ", flipFailures.Take(3)));

// Restart persistence: the host must serve the last activated versions again.
Check("restart: host terminated (SIGKILL)", KillHost(host), $"pid={host.Id}");
host = StartHost();
Check("restart: host restarts and answers", await WaitForHost());
var afterRestart = await http.GetStringAsync(baseUrl + "/data/index.json");
var afterRestartUi = await http.GetStringAsync(baseUrl + "/");
var restartedRun = JsonNode.Parse(afterRestart)?["latestRunId"]?.GetValue<string>();
Check("restart: last activated data + UI version served after restart",
    (restartedRun == realLatest || restartedRun == capRunId) && afterRestartUi.Contains("ui-update-marker") == rolled.Contains("ui-update-marker"),
    "dataRun=" + restartedRun);

// Read-only proof for the whole session.
Check("read-only: source validated cache never modified by host/activation",
    TreeFingerprint(dataReal) == fingerprintBefore, fingerprintBefore);

// ---------------------------------------------------------------- report
Check("host: process still healthy at end of session", !host.HasExited, $"pid={host.Id}");
KillHost(host);

var logPath = Path.Combine(logs, "verify-hosting-log.txt");
var failed = results.Where(r => !r.Ok).ToArray();
var report = new StringBuilder()
    .AppendLine($"verify hosted web progress site -- {DateTimeOffset.UtcNow:O}")
    .AppendLine($"real data source: {realData}")
    .AppendLine($"data-cap runs={capClosure["runs"]} refs={capClosure["references"]} big resource={bigBytes} bytes")
    .AppendLine($"releases: {string.Join(", ", releaseIds)}")
    .AppendLine()
    .AppendJoin('\n', results.Select(r => $"{(r.Ok ? "PASS" : "FAIL")} {r.Name}{(r.Detail.Length > 0 ? " -- " + r.Detail : "")}"))
    .AppendLine().AppendLine()
    .AppendLine($"SUMMARY: {results.Count - failed.Length}/{results.Count} passed, {failed.Length} failed")
    .AppendLine().AppendLine("COMMANDS:")
    .AppendJoin('\n', commands);
File.WriteAllText(logPath, report.ToString());
File.WriteAllText(Path.Combine(logs, "verify-commands.txt"), string.Join("\n\n", commands));
Console.WriteLine();
Console.WriteLine($"SUMMARY: {results.Count - failed.Length}/{results.Count} passed, {failed.Length} failed");
Console.WriteLine("log: " + logPath);
return failed.Length == 0 ? 0 : 1;

// ---------------------------------------------------------------- helpers
bool Rollback()
{
    var code = Cmd("rollback", "dotnet", ["run", "--file", activateScript, "--", "--site", site, "--data", dataReal, "--state", statePath, "--rollback"], out _);
    return code == 0;
}
bool KillHost(Process process)
{
    try { process.Kill(entireProcessTree: true); process.WaitForExit(10000); return true; }
    catch { return false; }
}
Process StartHost()
{
    var again = new Process
    {
        StartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = hostProject,
        },
    };
    foreach (var argument in new[] { hostDll, "--site", site, "--listen", $"127.0.0.1:{port}", "--listen", $"{tailnetIp}:{port}" })
        again.StartInfo.ArgumentList.Add(argument);
    again.Start();
    again.BeginOutputReadLine();
    again.BeginErrorReadLine();
    return again;
}
static long HostRssKb(int pid)
{
    try
    {
        foreach (var line in File.ReadLines("/proc/" + pid + "/status"))
            if (line.StartsWith("VmRSS:")) return long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]);
    }
    catch { }
    return 0;
}
static string TreeFingerprint(string root)
{
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order())
    {
        hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file)));
        hash.AppendData(SHA256.HashData(File.ReadAllBytes(file)));
    }
    return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
}
static void CopyTree(string source, string target)
{
    Directory.CreateDirectory(target);
    foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
    foreach (var dir in Directory.GetDirectories(source)) CopyTree(dir, Path.Combine(target, Path.GetFileName(dir)));
}
static void EnsurePortFree(IPAddress address, int port)
{
    using var probe = new TcpListener(address, port);
    probe.Start();
    probe.Stop();
}
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 || at + 1 >= args.Length ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
