#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
#:property UseWebProgressScheduler=true
#:property UseHostedWebProgressSite=true
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// BCL-only interface gate. All mutable inputs and the host belong to this run;
// real Watch/Refresh/Prepare/Activate CLIs are used, with a controlled capture
// producer explicitly labelled as a fixture, never as Parasolid evidence.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
var work = Path.Combine(root, "temp_docs", "publishing-integration", "fixture-" + Guid.NewGuid().ToString("N"));
var fixture = Path.Combine(work, "producer");
var logs = Path.Combine(work, "logs");
var data = Path.Combine(fixture, "data");
var published = Path.Combine(work, "published");
var site = Path.Combine(work, "site");
var state = Path.Combine(fixture, "watch-state.json");
var mirror = Path.Combine(work, "activation-state.json");
Directory.CreateDirectory(Path.Combine(fixture, "scripts"));
Directory.CreateDirectory(Path.Combine(fixture, "src"));
Directory.CreateDirectory(logs);
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(root, work));
var count = 0;
void Check(bool ok, string label)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + label);
    if (!ok) throw new InvalidDataException(label);
    count++;
}
string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
void Write(string path, JsonNode node) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, node.ToJsonString()); }
Dictionary<string, string> Hashes(string path) => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
    .ToDictionary(f => Path.GetRelativePath(path, f).Replace('\\', '/'), f => Hash(File.ReadAllBytes(f)), StringComparer.Ordinal);
bool Same(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var h) && h == p.Value);
async Task<(int Exit, string Text)> Command(string label, string cwd, string executable, params string[] arguments)
{
    var info = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { process.Kill(true); await process.WaitForExitAsync(); throw; }
    var text = await output + "\n" + await error;
    File.WriteAllText(Path.Combine(logs, label + ".log"), "exit=" + process.ExitCode + "\n" + text);
    Console.WriteLine("CLI " + label + " exit=" + process.ExitCode);
    return (process.ExitCode, text);
}
Task<(int Exit, string Text)> Tick(string label) => Command(label, fixture, "dotnet", "run", "--file", "scripts/WatchWebProgress.cs", "--", "--once", "--state", "../watch-state.json", "--registry", "registry.json", "--output", "../data", "--publish-data-to", Path.GetRelativePath(Path.Combine(fixture, "scripts"), published), "--batch-size", "1", "--samples", "1", "--seconds", "30", "--case-seconds", "20");
Task<(int Exit, string Text)> Activate(string label, string release, string stateFile, params string[] extra) => Command(label, root, "dotnet", ["run", "--file", "scripts/ActivateHostedWebProgressSite.cs", "--", "--site", Path.GetRelativePath(scripts, site), "--data", Path.GetRelativePath(scripts, published), "--state", Path.GetRelativePath(scripts, stateFile), "--release", release, .. extra]);
foreach (var name in new[] { "Directory.Build.props", "WatchWebProgress.cs", "WebProgressScheduler.cs", "RefreshWebProgress.cs", "WebProgressDataValidation.cs", "WebProgressPublishing.cs", "WebProgressMetadata.cs" })
    File.Copy(Path.Combine(scripts, name), Path.Combine(fixture, "scripts", name));
File.WriteAllText(Path.Combine(fixture, "scripts", "FixtureCapture.cs"), """"
#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Text.Json.Nodes;
var dir = Environment.GetEnvironmentVariable("GM_WEB_CAPTURE")!;
File.WriteAllText(Path.Combine(dir,"mesh.json"), """
{"schemaVersion":1,"bodyId":"fixture","source":{"kernel":"fixture","geometryAuthor":"BCL fixture","tessellator":"fixture","commit":"fixture","chordTolerance":0.01},"faces":[{"id":"f1","positions":[0,0,0,1,0,0,0,1,0],"indices":[0,1,2],"role":"unchanged"}],"edges":[]}
"""
);
File.WriteAllText(Path.Combine(dir,"capture.json"), """
{"pkVersion":"BCL-fixture-not-PK","sessionPrecision":"fixture","steps":[{"id":"s","title":"fixture","gm":{"before":"mesh.json","after":"mesh.json"},"pk":{"before":"mesh.json","after":"mesh.json"}}],"checks":[{"id":"fixture","label":"controlled failure microvalue","status":"fail","gm":2.01065554e-7,"pk":0,"absDelta":2.01065554e-7,"normalizedDelta":2.01065554e-7,"threshold":1e-7,"unit":"fixture","evidence":null,"note":"synthetic; not geometry acceptance"}],"timings":[]}
"""
);
Console.WriteLine("FIXTURE_ORIGINAL_EXIT_7");
return 7;
"""");
Write(Path.Combine(fixture, "scripts", "registry.json"), JsonNode.Parse("""
{"schemaVersion":1,"version":"integration-fixture","scope":"BCL only","limitations":[],"reviewSources":[],"sourceCoverage":[],"cases":[{"id":"fixture.alpha","title":"fixture.alpha","suite":"fixture","source":"scripts/FixtureCapture.cs","family":"blend","operation":"fixture","parameters":{"radius":0.1},"api":"fixture","arguments":[],"meshAdapter":true,"representative":true}]}
""")!);
File.WriteAllText(Path.Combine(fixture, "src", "source.cs"), "// fixed fixture source\n");
var pk = Path.Combine(fixture, "third_party", "PKToy");
Directory.CreateDirectory(pk);
foreach (var gitRoot in new[] { pk, fixture })
{
    await Command("git-init-" + Path.GetFileName(gitRoot), gitRoot, "git", "init", "--quiet");
    await Command("git-add-" + Path.GetFileName(gitRoot), gitRoot, "git", "add", ".");
    var commit = await Command("git-commit-" + Path.GetFileName(gitRoot), gitRoot, "git", "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "--allow-empty", "--quiet", "-m", "fixed fixture");
    Check(commit.Exit == 0, "fixture source committed " + Path.GetFileName(gitRoot));
}
var sourceHashes = Hashes(Path.Combine(fixture, "scripts"));
Write(Path.Combine(work, "source-hashes.json"), new JsonObject(sourceHashes.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value)))));
Directory.CreateDirectory(published);
var sentinel = Path.Combine(work, "sentinel.log");
File.WriteAllText(sentinel, "OUTSIDE_UNCHANGED");
File.CreateSymbolicLink(Path.Combine(published, "index.json.tmp"), sentinel);
var first = await Tick("watch-publish-failed");
Check(first.Exit == 2 && first.Text.Contains("PUBLISH_FAILED"), "Watch persists closure publish failure with real exit2");
var failedState = Read(state);
Check(failedState["publish"]!["pending"]!.GetValue<bool>() && failedState["publish"]!["lastErrorAt"] is not null, "publish retry metadata persisted");
var runId = failedState["lastRunId"]!.GetValue<string>();
var runRoot = Path.Combine(data, "runs", runId);
var originalHashes = Hashes(runRoot);
File.Delete(Path.Combine(published, "index.json.tmp"));
var retry = await Tick("watch-publish-retry");
Check(retry.Exit == 0 && retry.Text.Contains("PUBLISHED_DATA") && !retry.Text.Contains("BATCH "), "Watch retry publishes without collector rerun");
Check(!Read(state)["publish"]!["pending"]!.GetValue<bool>() && Same(originalHashes, Hashes(runRoot)), "success clears pending and preserves immutable run");
var outsideRuns = Path.Combine(work, "outside-runs"); Directory.CreateDirectory(outsideRuns);
File.WriteAllText(Path.Combine(outsideRuns, "sentinel.log"), "OUTSIDE_RUNS");
var badOutput = Path.Combine(fixture, "bad-output"); Directory.CreateDirectory(badOutput);
Directory.CreateSymbolicLink(Path.Combine(badOutput, "runs"), outsideRuns);
var unsafeRefresh = await Command("refresh-unsafe-runs", fixture, "dotnet", "run", "--file", "scripts/RefreshWebProgress.cs", "--", "--registry", "registry.json", "--case", "fixture.alpha", "--output", "../bad-output", "--run-id", "unsafe");
Check(unsafeRefresh.Exit != 0 && unsafeRefresh.Text.Contains("REFRESH_PATH_UNSAFE") && Directory.GetFileSystemEntries(outsideRuns).Length == 1, "direct producer refuses a linked runs ancestor before writing");
var manifest = Read(Path.Combine(runRoot, "manifest.json"));
Check(manifest["cases"]![0]!["execution"]!["exitCode"]!.GetValue<int>() == 7 && manifest["cases"]![0]!["checks"]![0]!["gm"]!.GetValue<double>() == 2.01065554e-7, "original collector exit7 and microvalue preserved");
var joined = manifest.DeepClone().AsObject();
joined["run"]!["id"] = "joined";
var meshRef = "../" + runId + "/cases/fixture.alpha/sample-0/mesh.json";
foreach (var side in new[] { "gm", "pk" }) { joined["cases"]![0]!["steps"]![0]![side]!["before"] = meshRef; joined["cases"]![0]!["steps"]![0]![side]!["after"] = meshRef; }
foreach (var check in joined["cases"]![0]!["checks"]!.AsArray()) check!["evidence"] = "../quarantined/raw.log";
joined["cases"]![0]!["evidence"] = new JsonArray(new JsonObject { ["label"] = "unindexed raw log", ["path"] = "../quarantined/raw.log" });
Write(Path.Combine(data, "runs", "joined", "manifest.json"), joined);
Directory.CreateDirectory(Path.Combine(data, "runs", "quarantined"));
File.WriteAllText(Path.Combine(data, "runs", "quarantined", "manifest.json"), "{broken unindexed manifest");
File.WriteAllText(Path.Combine(data, "runs", "quarantined", "raw.log"), "ORIGINAL_QUARANTINED_LOG\n");
var index = Read(Path.Combine(data, "index.json"));
var descriptor = index["runs"]![0]!.DeepClone();
descriptor["id"] = "joined"; descriptor["manifest"] = "runs/joined/manifest.json";
index["runs"]!.AsArray().Add(descriptor); index["latestRunId"] = "joined";
Write(Path.Combine(data, "index.json"), index);
var sourceValidation = WebProgressDataValidation.Validate(data);
Write(Path.Combine(work, "source-validation.json"), sourceValidation);
Check(sourceValidation["errors"]!.AsArray().Count == 0, "full source validator accepts cross-run closure and quarantined raw log: " + sourceValidation["errors"]);
WebProgressPublishing.PublishClosure(data, new[] { "joined" }, "joined", published);
Check(File.ReadAllText(Path.Combine(published, "runs", "quarantined", "manifest.json")).StartsWith("{broken"), "PublishClosure retains raw unindexed broken run");
var publishedBefore = Hashes(published);
var watchBefore = File.ReadAllBytes(state);
var dist = Path.Combine(work, "web", "dist");
Directory.CreateDirectory(Path.Combine(dist, "assets"));
File.WriteAllText(Path.Combine(dist, "index.html"), "<html><script src=\"./assets/old-123abc.js\"></script>OLD</html>");
File.WriteAllText(Path.Combine(dist, "assets", "old-123abc.js"), "console.log('OLD');");
var prepare = await Command("prepare-old", root, "dotnet", "run", "--no-cache", "--file", "scripts/PrepareHostedWebProgressSite.cs", "--", "--no-build", "--dist", Path.GetRelativePath(scripts, dist), "--site", Path.GetRelativePath(scripts, site));
Check(prepare.Exit == 0, "real Prepare CLI independent compilation");
string Release() => Directory.GetFiles(Path.Combine(site, "ui-releases"), "*.meta.json").Select(Read).OrderBy(m => m["createdAt"]!.GetValue<string>(), StringComparer.Ordinal).Last()["id"]!.GetValue<string>();
var oldRelease = Release();
var activated = await Activate("activate-old", oldRelease, mirror);
Check(activated.Exit == 0, "real Activate CLI imports scheduler closure into CAS");
var oldPointer = Read(Path.Combine(site, "site.json"));
var oldGeneration = oldPointer["generation"]!.GetValue<string>();
Check(HostedWebProgressSite.VerifyGenerationClosure(site, oldGeneration, out var error), "activated CAS closure independently valid: " + error);
var linkedWeb = Path.Combine(work, "linked-web"); Directory.CreateSymbolicLink(linkedWeb, Path.GetDirectoryName(dist)!);
var unsafePrepare = await Command("prepare-unsafe-ancestor", root, "dotnet", "run", "--file", "scripts/PrepareHostedWebProgressSite.cs", "--", "--no-build", "--dist", Path.GetRelativePath(scripts, Path.Combine(linkedWeb, "dist")), "--site", Path.GetRelativePath(scripts, Path.Combine(work, "unsafe-site")));
Check(unsafePrepare.Exit != 0 && unsafePrepare.Text.Contains("HOST_PATH_UNSAFE") && !Directory.Exists(Path.Combine(work, "unsafe-site")), "Prepare refuses a linked dist ancestor before destination creation");
var dangling = Path.Combine(work, "dangling"); File.CreateSymbolicLink(dangling, Path.Combine(work, "does-not-exist"));
Check(!WebProgressSafePath.IsSafeChain(dangling, out _) && !HostedWebProgressSite.IsChainSafe(dangling), "both producer and host reject dangling write-target symlinks");
Check(Same(publishedBefore, Hashes(published)) && watchBefore.SequenceEqual(File.ReadAllBytes(state)), "activation never mutates publish source or scheduler state");
var build = await Command("host-build", root, "dotnet", "build", "web/host/GmProgressHost.csproj", "--configuration", "Release", "--nologo");
Check(build.Exit == 0, "independent managed host compile");
using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
using var host = new Process { StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
foreach (var arg in new[] { "web/host/bin/Release/net10.0/GmProgressHost.dll", "--site", site, "--listen", "127.0.0.1:" + port }) host.StartInfo.ArgumentList.Add(arg);
using var hostLog = new StreamWriter(Path.Combine(logs, "host.log"));
host.OutputDataReceived += (_, e) => { lock (hostLog) hostLog.WriteLine(e.Data); };
host.ErrorDataReceived += (_, e) => { lock (hostLog) hostLog.WriteLine(e.Data); };
host.Start(); host.BeginOutputReadLine(); host.BeginErrorReadLine();
Console.WriteLine("OWNED_HOST pid=" + host.Id + " port=" + port);
using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(10) };
async Task<byte[]> Get(string path)
{
    using var response = await http.GetAsync(path);
    Check(response.StatusCode == HttpStatusCode.OK, "HTTP200 " + path);
    return await response.Content.ReadAsByteArrayAsync();
}
try
{
    var healthy = false;
    for (var i = 0; i < 100; i++) { try { using var response = await http.GetAsync("/healthz"); healthy = response.IsSuccessStatusCode; if (healthy) break; } catch (HttpRequestException) { } await Task.Delay(100); }
    Check(healthy, "own loopback HTTP health");
    var oldIndex = File.ReadAllBytes(Path.Combine(published, "index.json"));
    var oldHtml = await Get("/");
    var oldAsset = await Get("/assets/old-123abc.js");
    foreach (var (path, hash) in publishedBefore.Where(p => p.Key == "index.json" || p.Key.StartsWith("runs/", StringComparison.Ordinal)))
        Check(Hash(await Get("/data/" + path)) == hash, "scheduler published bytes match CAS HTTP " + path);
    File.WriteAllText(Path.Combine(published, "index.json"), "{source cache advanced");
    File.AppendAllText(Path.Combine(published, "runs", "quarantined", "raw.log"), "SOURCE_ONLY\n");
    Check((await Get("/data/index.json")).SequenceEqual(oldIndex), "later cache index writes cannot leak to active");
    Check(System.Text.Encoding.UTF8.GetString(await Get("/data/runs/quarantined/raw.log")) == "ORIGINAL_QUARANTINED_LOG\n", "later cache object writes cannot leak to active");
    var refusal = await Activate("activate-invalid-source", oldRelease, mirror);
    Check(refusal.Exit != 0 && Read(Path.Combine(site, "site.json")).ToJsonString() == oldPointer.ToJsonString(), "invalid source activation preserves authority bytes");
    File.WriteAllBytes(Path.Combine(published, "index.json"), oldIndex);
    File.WriteAllText(Path.Combine(published, "runs", "quarantined", "raw.log"), "ORIGINAL_QUARANTINED_LOG\n");
    var overlap = await Activate("refuse-scheduler-as-mirror", oldRelease, state);
    Check(overlap.Exit != 0 && watchBefore.SequenceEqual(File.ReadAllBytes(state)), "hosting cannot overwrite scheduler state");
    var authorityOverlap = await Activate("refuse-authority-as-mirror", oldRelease, Path.Combine(site, "site.json"));
    Check(authorityOverlap.Exit != 0 && Read(Path.Combine(site, "site.json")).ToJsonString() == oldPointer.ToJsonString(), "mirror cannot alias hosting authority");
    var successor = Read(Path.Combine(published, "runs", "joined", "manifest.json"));
    successor["run"]!["id"] = "successor";
    Write(Path.Combine(published, "runs", "successor", "manifest.json"), successor);
    var nextIndex = index.DeepClone();
    nextIndex["latestRunId"] = "successor";
    var nextDescriptor = descriptor.DeepClone(); nextDescriptor["id"] = "successor"; nextDescriptor["manifest"] = "runs/successor/manifest.json";
    nextIndex["runs"] = new JsonArray(nextDescriptor);
    Write(Path.Combine(published, "index.json"), nextIndex);
    File.Delete(Path.Combine(dist, "assets", "old-123abc.js"));
    File.WriteAllText(Path.Combine(dist, "assets", "new-456def.js"), "console.log('NEW');");
    File.WriteAllText(Path.Combine(dist, "index.html"), "<html><script src=\"./assets/new-456def.js\"></script>NEW</html>");
    Check((await Command("prepare-new", root, "dotnet", "run", "--file", "scripts/PrepareHostedWebProgressSite.cs", "--", "--no-build", "--dist", Path.GetRelativePath(scripts, dist), "--site", Path.GetRelativePath(scripts, site))).Exit == 0, "prepare successor UI release");
    var newRelease = Release();
    var brokenMirror = Path.Combine(work, "broken-mirror"); Directory.CreateDirectory(brokenMirror);
    var forward = await Activate("activate-mirror-failed", newRelease, brokenMirror);
    Check(forward.Exit == 0 && forward.Text.Contains("ACTIVATION_STATE_MIRROR_FAILED"), "authority commit succeeds truthfully despite mirror failure");
    var currentBytes = File.ReadAllBytes(Path.Combine(site, "site.json"));
    var currentIndex = await Get("/data/index.json");
    Check((await Get("/assets/old-123abc.js")).SequenceEqual(oldAsset), "old hashed asset survives successor activation");
    Check(Hash(await Get("/data/runs/joined/manifest.json")) == publishedBefore["runs/joined/manifest.json"], "cross-generation old run remains GET-addressable");
    var heal = await Activate("activate-heal", newRelease, mirror);
    Check(heal.Exit == 0 && heal.Text.Contains("ACTIVATION_STATE_REPAIRED") && currentBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(site, "site.json"))), "same-pair retry heals complete mirror without authority recommit");
    Check(JsonNode.DeepEquals(Read(mirror)["previous"], Read(Path.Combine(site, "site.json"))["previous"]), "mirror retains exact previous pair");
    var targetMeta = Path.Combine(site, "generations", oldGeneration + ".meta.json");
    var targetBytes = File.ReadAllBytes(targetMeta);
    var corrupt = Read(targetMeta); corrupt["runs"] = new JsonObject(); Write(targetMeta, corrupt);
    var mirrorBefore = File.ReadAllBytes(mirror);
    var badRollback = await Activate("rollback-corrupt", "", mirror, "--rollback");
    Check(badRollback.Exit == 3 && badRollback.Text.Contains("ACTIVATE_REFUSED_GENERATION_CORRUPT"), "bad target rollback real CLI exit3");
    Check(currentBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(site, "site.json"))) && mirrorBefore.SequenceEqual(File.ReadAllBytes(mirror)) && (await Get("/data/index.json")).SequenceEqual(currentIndex), "rejected rollback retains exact authority/mirror/current HTTP bytes");
    File.WriteAllBytes(targetMeta, targetBytes);
    var rollback = await Activate("rollback-valid", "", mirror, "--rollback");
    Check(rollback.Exit == 0 && (await Get("/data/index.json")).SequenceEqual(oldIndex) && (await Get("/")).SequenceEqual(oldHtml), "normal rollback restores exact old index and UI");
    Check(System.Text.Encoding.UTF8.GetString(await Get("/assets/new-456def.js")) == "console.log('NEW');", "successor hashed asset survives rollback");
    Check((await Get("/data/runs/quarantined/raw.log")).SequenceEqual(File.ReadAllBytes(Path.Combine(data, "runs", "quarantined", "raw.log"))), "rollback restores exact raw evidence reference");
    var hookSite = Path.Combine(work, "hook-site"); var hookState = Path.Combine(work, "hook-state.json");
    string[] HookArgs() => ["run", "--file", "scripts/PublishHostedWebProgressSite.cs", "--", "--no-build", "--site", Path.GetRelativePath(scripts, hookSite), "--state", Path.GetRelativePath(scripts, hookState), "--data", Path.GetRelativePath(scripts, published), "--web", Path.GetRelativePath(scripts, Path.GetDirectoryName(dist)!)];
    var hookSuccess = await Command("host-hook-success", root, "dotnet", HookArgs());
    Check(hookSuccess.Exit == 0 && hookSuccess.Text.Contains("HOST_PUBLISH"), "new serialized local hook validates/prepares/activates without portal or geometry");
    var hookPointer = File.ReadAllBytes(Path.Combine(hookSite, "site.json"));
    var savedIndex = File.ReadAllBytes(Path.Combine(published, "index.json"));
    File.WriteAllText(Path.Combine(published, "index.json"), "{broken snapshot");
    var hookFailure = await Command("host-hook-failure", root, "dotnet", HookArgs());
    Check(hookFailure.Exit != 0 && hookPointer.SequenceEqual(File.ReadAllBytes(Path.Combine(hookSite, "site.json"))), "hook failure preserves previous authority");
    File.WriteAllBytes(Path.Combine(published, "index.json"), savedIndex);
    var hookRecovery = await Command("host-hook-recovery", root, "dotnet", HookArgs());
    Check(hookRecovery.Exit == 0 && hookRecovery.Text.Contains("skip-same") && hookPointer.SequenceEqual(File.ReadAllBytes(Path.Combine(hookSite, "site.json"))), "hook recovery reuses exact pair without recommit or collector");
}
finally { if (!host.HasExited) host.Kill(true); await host.WaitForExitAsync(); Console.WriteLine("OWNED_HOST_STOPPED pid=" + host.Id + " exit=" + host.ExitCode); }
Check(Same(sourceHashes, Hashes(Path.Combine(fixture, "scripts"))) && Same(originalHashes, Hashes(runRoot)), "fixed producer sources and original per-run evidence unchanged");
Check(File.ReadAllText(sentinel) == "OUTSIDE_UNCHANGED", "destination symlink refusal preserves outside bytes");
Write(Path.Combine(work, "artifact-hashes.json"), new JsonObject(Hashes(site).Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value)))));
Console.WriteLine("ALL PASS " + count + " publishing integration checks; no PK or geometry acceptance");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;
