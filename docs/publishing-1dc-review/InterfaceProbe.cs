#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
global using CliExit = System.Int32;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Independent black-box interface probe; every write belongs to a fresh local fixture.
var repo = Path.GetFullPath("../..", Path.GetDirectoryName(Source())!);
var scripts = Path.Combine(repo, "scripts");
var work = Path.Combine(repo, "temp_docs", "publishing-1dc-review", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(repo, work));
var producer = Path.Combine(work, "producer");
var ps = Path.Combine(producer, "scripts");
var data = Path.Combine(producer, "data");
var publish = Path.Combine(work, "publish");
var site = Path.Combine(work, "site");
var state = Path.Combine(producer, "queue.json");
var mirror = Path.Combine(work, "mirror.json");
var web = Path.Combine(work, "web");
var dist = Path.Combine(web, "dist");
Directory.CreateDirectory(ps);
Directory.CreateDirectory(Path.Combine(producer, "src"));
Directory.CreateDirectory(Path.Combine(dist, "assets"));
var checks = 0;
void Require(bool condition, string text) { if (!condition) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
void Put(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
void Write(string path, JsonNode node) => Put(path, node.ToJsonString());
Dictionary<string, string> Tree(string dir) => Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), f => Hash(File.ReadAllBytes(f)));
bool Same(Dictionary<string, string> left, Dictionary<string, string> right) => left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var value) && p.Value == value);
bool Bytes(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
async Task<(CliExit Code, string Text)> Run(string label, string cwd, string exe, params string[] arguments)
{
    var info = new ProcessStartInfo(exe) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var process = Process.Start(info)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
    var text = await stdout + "\n" + await stderr;
    Put(Path.Combine(work, label + ".log"), "exit=" + process.ExitCode + "\n" + text);
    Console.WriteLine("CLI " + label + " exit=" + process.ExitCode);
    return (process.ExitCode, text);
}
Task<(CliExit Code, string Text)> Cli(string label, string file, params string[] arguments) => Run(label, repo, "dotnet", ["run", "--no-cache", "--file", "scripts/" + file + ".cs", "--", .. arguments]);
string Relative(string path) => Path.GetRelativePath(scripts, path);
Task<(CliExit Code, string Text)> Watch(string label) => Run(label, producer, "dotnet", "run", "--no-cache", "--file", "scripts/WatchWebProgress.cs", "--", "--once", "--registry", "registry.json", "--state", "../queue.json", "--output", "../data", "--publish-data-to", Path.GetRelativePath(ps, publish), "--case", "probe", "--samples", "1", "--seconds", "60", "--case-seconds", "45");
Task<(CliExit Code, string Text)> Activate(string label, string release, string stateFile, params string[] extra) => Cli(label, "ActivateHostedWebProgressSite", ["--site", Relative(site), "--data", Relative(publish), "--state", Relative(stateFile), "--release", release, .. extra]);
string Release() => Directory.GetFiles(Path.Combine(site, "ui-releases"), "*.meta.json").Select(Read).OrderBy(x => x["createdAt"]!.GetValue<string>(), StringComparer.Ordinal).Last()["id"]!.GetValue<string>();
foreach (var file in new[] { "Directory.Build.props", "WatchWebProgress.cs", "RefreshWebProgress.cs", "WebProgressScheduler.cs", "WebProgressPublishing.cs", "WebProgressDataValidation.cs", "WebProgressMetadata.cs" }) File.Copy(Path.Combine(scripts, file), Path.Combine(ps, file));
Put(Path.Combine(ps, "Capture.cs"), """"
#:property TargetFramework=net10.0
var dir = Environment.GetEnvironmentVariable("GM_WEB_CAPTURE")!;
File.WriteAllText(Path.Combine(dir, "mesh.json"), """
{"schemaVersion":1,"bodyId":"probe","source":{"kernel":"BCL fixture","geometryAuthor":"independent review fixture","tessellator":"fixture","commit":"fixture","chordTolerance":0.001},"faces":[],"edges":[]}
""");
File.WriteAllText(Path.Combine(dir, "capture.json"), """
{"pkVersion":"fixture-no-PK","sessionPrecision":"fixture","steps":[{"id":"s","title":"probe","gm":{"before":"mesh.json","after":"mesh.json"},"pk":{"before":"mesh.json","after":"mesh.json"}}],"checks":[{"id":"micro","label":"fixture failure","status":"fail","gm":2.01065554e-7,"pk":0,"absDelta":2.01065554e-7,"normalizedDelta":2.01065554e-7,"threshold":1e-7,"unit":"fixture","evidence":null,"note":"not geometry acceptance"}],"timings":[]}
""");
Console.WriteLine("INDEPENDENT_CAPTURE_EXIT7");
return 7;
"""");
Put(Path.Combine(ps, "registry.json"), """
{"schemaVersion":1,"version":"review","scope":"BCL fixture","limitations":["No geometry acceptance"],"reviewSources":[],"sourceCoverage":[],"cases":[{"id":"probe","title":"probe","suite":"review","family":"blend","operation":"fixture","parameters":{},"api":"fixture","source":"scripts/Capture.cs","arguments":[],"meshAdapter":true,"representative":true}]}
""");
Put(Path.Combine(producer, "src", "placeholder.cs"), "// BCL fixture only\n");
var pk = Path.Combine(producer, "third_party", "PKToy"); Directory.CreateDirectory(pk);
foreach (var git in new[] { pk, producer })
{
    Require((await Run("init-" + Path.GetFileName(git), git, "git", "init", "-q")).Code == 0, "fixture git init");
    Require((await Run("add-" + Path.GetFileName(git), git, "git", "add", ".")).Code == 0, "fixture git add");
    Require((await Run("commit-" + Path.GetFileName(git), git, "git", "-c", "user.name=Review", "-c", "user.email=review@example.invalid", "commit", "--allow-empty", "-qm", "fixed fixture")).Code == 0, "fixture git commit");
}
var sourceBefore = Tree(ps);
Write(Path.Combine(work, "source-hashes.json"), new JsonObject(Directory.GetFiles(scripts, "*.cs").Select(f => KeyValuePair.Create<string, JsonNode?>(Path.GetFileName(f), JsonValue.Create(Hash(File.ReadAllBytes(f)))))));
Directory.CreateDirectory(publish);
var outside = Path.Combine(work, "outside.log"); Put(outside, "UNCHANGED");
File.CreateSymbolicLink(Path.Combine(publish, "index.json.tmp"), outside);
var failure = await Watch("watch-failure");
Require(failure.Code == 2 && failure.Text.Contains("PUBLISH_FAILED"), "real Watch publish refusal exit2");
Require(Read(state)["publish"]!["pending"]!.GetValue<bool>() && Read(state)["publish"]!["lastErrorAt"] is not null, "scheduler persists pending with failure timestamp");
var run = Read(state)["lastRunId"]!.GetValue<string>();
var runDir = Path.Combine(data, "runs", run);
var runBefore = Tree(runDir);
var result = Read(Path.Combine(runDir, "manifest.json"))["cases"]![0]!;
Require(result["execution"]!["exitCode"]!.GetValue<CliExit>() == 7 && result["status"]!.GetValue<string>() != "pass" && result["checks"]![0]!["gm"]!.GetValue<double>() == 2.01065554e-7, "collector exit7 and exact microvalue remain failure");
File.Delete(Path.Combine(publish, "index.json.tmp"));
var retry = await Watch("watch-recovery");
Require(retry.Code == 0 && retry.Text.Contains("PUBLISHED_DATA") && !retry.Text.Contains("BATCH ") && !Read(state)["publish"]!["pending"]!.GetValue<bool>(), "retry publishes without collector and clears pending");
var aggregate = Read(Path.Combine(runDir, "manifest.json")); aggregate["run"]!["id"] = "aggregate";
foreach (var side in new[] { "gm", "pk" }) foreach (var field in new[] { "before", "after" }) aggregate["cases"]![0]!["steps"]![0]![side]![field] = "../" + run + "/cases/probe/sample-0/mesh.json";
foreach (var check in aggregate["cases"]![0]!["checks"]!.AsArray()) check!["evidence"] = "../raw/e.log";
aggregate["cases"]![0]!["evidence"] = new JsonArray(new JsonObject { ["label"] = "raw", ["path"] = "../raw/e.log" });
Write(Path.Combine(data, "runs", "aggregate", "manifest.json"), aggregate);
Put(Path.Combine(data, "runs", "raw", "manifest.json"), "{invalid unindexed manifest"); Put(Path.Combine(data, "runs", "raw", "e.log"), "RAW_REVIEW_EVIDENCE");
var index = Read(Path.Combine(data, "index.json")); var descriptor = index["runs"]![0]!.DeepClone(); descriptor["id"] = "aggregate"; descriptor["manifest"] = "runs/aggregate/manifest.json";
index["runs"]!.AsArray().Add(descriptor); index["latestRunId"] = "aggregate"; Write(Path.Combine(data, "index.json"), index);
Require((await Cli("publish-cross", "PublishWebProgress", "--source", Relative(data), "--destination", Relative(publish), "--run-id", "aggregate")).Code == 0, "public publisher imports cross-run mesh and raw log closure");
var publishedBefore = Tree(publish); var stateBefore = File.ReadAllBytes(state);
Put(Path.Combine(dist, "index.html"), "<script src='./assets/old-123abc.js'></script>REVIEW_OLD"); Put(Path.Combine(dist, "assets", "old-123abc.js"), "REVIEW_OLD_ASSET");
Require((await Cli("prepare", "PrepareHostedWebProgressSite", "--no-build", "--site", Relative(site), "--dist", Relative(dist))).Code == 0, "Prepare independent CLI");
var release = Release(); Require((await Activate("activate", release, mirror)).Code == 0, "Activate independent CLI");
var pointer = Path.Combine(site, "site.json"); var oldPointer = Read(pointer); var oldGeneration = oldPointer["generation"]!.GetValue<string>();
Require(oldPointer["schemaVersion"]!.GetValue<int>() == 1 && oldPointer["dataSha"]!.GetValue<string>() == descriptor["commit"]!.GetValue<string>(), "typed authority matches scheduler commit");
Require((await Run("build-host", repo, "dotnet", "build", "web/host", "-c", "Release", "-m:1", "-p:UseSharedCompilation=false")).Code == 0, "managed host build");
using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
using var host = new Process { StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true } };
foreach (var arg in new[] { "web/host/bin/Release/net10.0/GmProgressHost.dll", "--site", site, "--listen", "127.0.0.1:" + port }) host.StartInfo.ArgumentList.Add(arg);
host.Start(); var hostOut = host.StandardOutput.ReadToEndAsync(); var hostErr = host.StandardError.ReadToEndAsync(); Console.WriteLine($"OWN_HOST pid={host.Id} port={port}");
using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(10) };
var httpRecords = new JsonArray();
async Task<byte[]> Get(string path) { using var response = await http.GetAsync(path); var bytes = await response.Content.ReadAsByteArrayAsync(); httpRecords.Add((JsonNode)new JsonObject { ["path"] = path, ["status"] = (int)response.StatusCode, ["length"] = bytes.Length, ["sha256"] = Hash(bytes) }); if (response.StatusCode != HttpStatusCode.OK) throw new Exception("HTTP " + path + " " + response.StatusCode); return bytes; }
try
{
    var ready = false; for (var i = 0; i < 100; i++) { try { using var health = await http.GetAsync("/healthz"); if (health.IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { } await Task.Delay(100); } Require(ready, "own host healthy");
    var oldIndex = File.ReadAllBytes(Path.Combine(publish, "index.json")); var oldHtml = await Get("/"); var oldAsset = await Get("/assets/old-123abc.js");
    foreach (var file in publishedBefore) Require(Hash(await Get("/data/" + file.Key)) == file.Value, "HTTP exact closure " + file.Key);
    Put(Path.Combine(publish, "index.json"), "{cache changed"); Put(Path.Combine(publish, "runs", "raw", "e.log"), "CACHE_ONLY");
    Require((await Get("/data/index.json")).SequenceEqual(oldIndex) && System.Text.Encoding.UTF8.GetString(await Get("/data/runs/raw/e.log")) == "RAW_REVIEW_EVIDENCE", "cache writes remain invisible to active generation");
    File.WriteAllBytes(Path.Combine(publish, "index.json"), oldIndex); Put(Path.Combine(publish, "runs", "raw", "e.log"), "RAW_REVIEW_EVIDENCE");
    var queueRefusal = await Activate("queue-as-mirror", release, state); Require(queueRefusal.Code != 0 && queueRefusal.Text.Contains("HOST_MIRROR_SCHEMA_INVALID") && stateBefore.SequenceEqual(File.ReadAllBytes(state)), "typed queue cannot become host mirror");
    var overlap = await Activate("authority-as-mirror", release, pointer); Require(overlap.Code != 0 && overlap.Text.Contains("HOST_PATH_OVERLAP"), "authority cannot alias mirror");
    var badAggregate = aggregate.DeepClone(); badAggregate["cases"]![0]!["steps"]![0]!["gm"]!["before"] = "../missing/mesh.json"; Write(Path.Combine(data, "runs", "aggregate", "manifest.json"), badAggregate);
    var invalid = await Cli("publish-missing-cross", "PublishWebProgress", "--source", Relative(data), "--destination", Relative(publish), "--run-id", "aggregate"); Require(invalid.Code != 0 && Same(publishedBefore, Tree(publish)), "missing cross-run reference refuses without changing published bytes"); Write(Path.Combine(data, "runs", "aggregate", "manifest.json"), aggregate);
    var dangling = Path.Combine(work, "dangling"); File.CreateSymbolicLink(dangling, Path.Combine(work, "absent"));
    var unsafePrepare = await Cli("prepare-dangling", "PrepareHostedWebProgressSite", "--no-build", "--dist", Relative(dist), "--site", Relative(dangling)); Require(unsafePrepare.Code != 0 && unsafePrepare.Text.Contains("HOST_PATH_UNSAFE") && !Directory.Exists(Path.Combine(work, "absent")), "dangling destination refuses before create");
    Put(Path.Combine(dist, "index.html"), "<script src='./assets/new-456def.js'></script>REVIEW_NEW"); File.Delete(Path.Combine(dist, "assets", "old-123abc.js")); Put(Path.Combine(dist, "assets", "new-456def.js"), "REVIEW_NEW_ASSET");
    Require((await Cli("prepare-next", "PrepareHostedWebProgressSite", "--no-build", "--site", Relative(site), "--dist", Relative(dist))).Code == 0, "prepare next UI"); var next = Release();
    var nextIndex = index.DeepClone(); nextIndex["reviewGeneration"] = "second"; Write(Path.Combine(publish, "index.json"), nextIndex);
    var brokenMirror = Path.Combine(work, "mirror-directory"); Directory.CreateDirectory(brokenMirror); var forward = await Activate("mirror-write-failure", next, brokenMirror);
    Require(forward.Code == 0 && forward.Text.Contains("ACTIVATION_STATE_MIRROR_FAILED"), "mirror failure leaves truthful successful authority commit");
    var current = File.ReadAllBytes(pointer); var currentIndex = await Get("/data/index.json"); var currentHtml = await Get("/");
    Require((await Get("/assets/old-123abc.js")).SequenceEqual(oldAsset), "retained hashed asset survives new UI");
    var heal = await Activate("mirror-heal", next, mirror); var authority = Read(pointer); var mirrorObject = Read(mirror);
    Require(heal.Code == 0 && heal.Text.Contains("ACTIVATION_STATE_REPAIRED") && current.SequenceEqual(File.ReadAllBytes(pointer)) && JsonNode.DeepEquals(mirrorObject["previous"], authority["previous"]) && mirrorObject["activationCount"]!.GetValue<int>() == 2, "same-pair retry heals history and count without recommit");
    var target = Path.Combine(site, "generations", oldGeneration + ".meta.json"); var targetBytes = File.ReadAllBytes(target); var corrupt = Read(target); corrupt["runs"]!.AsObject().Remove("raw"); Write(target, corrupt); var mirrorBefore = File.ReadAllBytes(mirror);
    var rollbackBad = await Activate("rollback-missing-raw", "", mirror, "--rollback"); Require(rollbackBad.Code == 3 && rollbackBad.Text.Contains("raw: required run mapping missing") && current.SequenceEqual(File.ReadAllBytes(pointer)) && mirrorBefore.SequenceEqual(File.ReadAllBytes(mirror)) && Bytes(currentIndex, await Get("/data/index.json")) && Bytes(currentHtml, await Get("/")), "bad raw target rollback keeps pointer count previous mirror and HTTP bytes");
    File.WriteAllBytes(target, targetBytes); Require((await Activate("rollback-good", "", mirror, "--rollback")).Code == 0 && Bytes(oldIndex, await Get("/data/index.json")) && Bytes(oldHtml, await Get("/")), "valid rollback restores exact index and UI");
    Require(System.Text.Encoding.UTF8.GetString(await Get("/assets/new-456def.js")) == "REVIEW_NEW_ASSET" && Hash(await Get("/data/runs/aggregate/manifest.json")) == publishedBefore["runs/aggregate/manifest.json"], "new hashed asset and retained evidence still GET after rollback");
    var hookSite = Path.Combine(work, "hook-site"); var hookMirror = Path.Combine(work, "hook-mirror.json");
    Task<(CliExit Code, string Text)> Hook(string label) => Cli(label, "PublishHostedWebProgressSite", "--no-build", "--web", Relative(web), "--site", Relative(hookSite), "--data", Relative(publish), "--state", Relative(hookMirror));
    Require((await Hook("hook-success")).Code == 0, "serialized hook actual success"); var hookPointer = File.ReadAllBytes(Path.Combine(hookSite, "site.json")); var saved = File.ReadAllBytes(Path.Combine(publish, "index.json"));
    Put(Path.Combine(publish, "index.json"), "{bad"); Require((await Hook("hook-failure")).Code == 3 && hookPointer.SequenceEqual(File.ReadAllBytes(Path.Combine(hookSite, "site.json"))), "serialized hook real exit3 preserves authority");
    File.WriteAllBytes(Path.Combine(publish, "index.json"), saved); var recovery = await Hook("hook-recovery"); Require(recovery.Code == 0 && recovery.Text.Contains("skip-same") && hookPointer.SequenceEqual(File.ReadAllBytes(Path.Combine(hookSite, "site.json"))), "serialized hook recovery no recommit");
}
finally { if (!host.HasExited) host.Kill(true); await host.WaitForExitAsync(); Put(Path.Combine(work, "host.log"), await hostOut + "\n" + await hostErr); Write(Path.Combine(work, "http.json"), httpRecords); Console.WriteLine($"OWN_HOST_CLEANED pid={host.Id} port={port} exit={host.ExitCode}"); }
Require(Same(sourceBefore, Tree(ps)) && Same(runBefore, Tree(runDir)) && stateBefore.SequenceEqual(File.ReadAllBytes(state)) && File.ReadAllText(outside) == "UNCHANGED", "producer sources per-run queue and outside sentinel preserved");
Write(Path.Combine(work, "artifact-hashes.json"), new JsonObject(Tree(site).Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value)))));
Console.WriteLine("SUMMARY " + checks + "/" + checks + " passed; BCL only, no geometry acceptance");
static string Source([CallerFilePath] string path = "") => path;
