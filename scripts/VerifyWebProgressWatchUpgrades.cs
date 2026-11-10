#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Driver-level regression harness for the persistent scheduler watch loop.
// Runs the real WatchWebProgress/RefreshWebProgress pair against a nested
// fixture git repository with one cheap controlled oracle (no PK): pure staged
// changes, dirty->clean reverts and rename/delete reverses must all invalidate
// the affected observations through the union of previously and currently
// dirty paths, and a poisoned historical run must be quarantined out of the
// validated index on the next tick. Offline: no network, no PK, no geometry.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
var work = Path.Combine(root, "temp_docs", "web-progress-watch-tests");
if (Directory.Exists(work)) Directory.Delete(work, true);
var fixture = Path.Combine(work, "fixture");
Directory.CreateDirectory(Path.Combine(fixture, "scripts"));
Directory.CreateDirectory(Path.Combine(fixture, "src"));
var pkToy = Path.Combine(fixture, "third_party", "PKToy");
Directory.CreateDirectory(pkToy);
var failures = new List<string>();
var passed = 0;
void Assert(bool condition, string label)
{
    if (!condition) { failures.Add(label); Console.WriteLine("FAIL " + label); return; }
    passed++;
    Console.WriteLine("PASS " + label);
}

foreach (var name in new[] { "WatchWebProgress.cs", "WebProgressScheduler.cs", "WebProgressDataValidation.cs", "WebProgressPublishing.cs", "WebProgressMetadata.cs", "Directory.Build.props", "RefreshWebProgress.cs" })
    File.Copy(Path.Combine(scripts, name), Path.Combine(fixture, "scripts", name));
File.WriteAllText(Path.Combine(fixture, "scripts", "FakeOracle.cs"),
    "#!/usr/bin/env dotnet run\n" +
    "#:property TargetFramework=net10.0\n" +
    "var capture = Environment.GetEnvironmentVariable(\"GM_WEB_CAPTURE\") ?? throw new Exception(\"GM_WEB_CAPTURE required\");\n" +
    "File.WriteAllText(Path.Combine(capture, \"capture.json\"), \"{\\\"pkVersion\\\":\\\"fake\\\",\\\"sessionPrecision\\\":\\\"test\\\",\\\"steps\\\":[],\\\"checks\\\":[{\\\"id\\\":\\\"fake-check\\\",\\\"label\\\":\\\"fixture acceptance\\\",\\\"status\\\":\\\"pass\\\",\\\"gm\\\":1,\\\"pk\\\":1,\\\"absDelta\\\":0,\\\"normalizedDelta\\\":0,\\\"threshold\\\":1e-7,\\\"unit\\\":\\\"count\\\",\\\"evidence\\\":null,\\\"note\\\":\\\"controlled fixture check\\\"}],\\\"timings\\\":[]}\");\n" +
    "return 0;\n");
File.WriteAllText(Path.Combine(fixture, "scripts", "registry.json"), """
{"schemaVersion":1,"version":"fixture-v1","scope":"test","limitations":[],"reviewSources":[],"sourceCoverage":[],
 "cases":[{"id":"fake.alpha","title":"fake.alpha","suite":"fake-suite","source":"scripts/FakeOracle.cs","family":"blend","operation":"test-op","parameters":{"radius":0.1},"api":"test_api","arguments":[],"meshAdapter":true,"representative":true}]}
""");
File.WriteAllText(Path.Combine(fixture, "src", "source.cs"), "// kernel source v1\n");
File.WriteAllText(Path.Combine(pkToy, "README"), "fixture PKToy stand-in\n");

string Git(string wd, params string[] args)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = wd, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var a in args) info.ArgumentList.Add(a);
    using var p = Process.Start(info)!;
    var output = p.StandardOutput.ReadToEnd();
    var error = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new Exception("git " + string.Join(" ", args) + ": " + error);
    return output.Trim();
}
Git(pkToy, "init", "--quiet");
Git(pkToy, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "--quiet", "--allow-empty", "-m", "fixture");
Git(fixture, "init", "--quiet");
Git(fixture, "add", ".");
Git(fixture, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "--quiet", "-m", "fixture");

// One watcher tick: builds the fixture watcher, runs one bounded batch.
(string Text, int Exit) Tick(string label, params string[] extra)
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = fixture, RedirectStandardOutput = true, RedirectStandardError = true };
    var defaults = new[] { "run", "--file", "scripts/WatchWebProgress.cs", "--", "--once", "--state", "../state.json", "--registry", "registry.json", "--output", "../data", "--batch-size", "1", "--seconds", "30", "--case-seconds", "20", "--samples", "1" };
    for (var i = 0; i < defaults.Length; i++)
    {
        if (defaults[i].StartsWith("--", StringComparison.Ordinal) && extra.Contains(defaults[i], StringComparer.Ordinal)) { i++; continue; }
        info.ArgumentList.Add(defaults[i]);
    }
    foreach (var a in extra) info.ArgumentList.Add(a);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var p = Process.Start(info)!;
    var output = p.StandardOutput.ReadToEnd();
    var error = p.StandardError.ReadToEnd();
    p.WaitForExit();
    File.WriteAllText(Path.Combine(work, label + ".log"), output + "\n----- STDERR -----\n" + error);
    return (output + "\n" + error, p.ExitCode);
}
string? StateRunId()
{
    var state = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "state.json")))!;
    return state["completed"]!["fake.alpha"]?["runId"]?.GetValue<string>();
}
int ChangedPathCount(string text)
{
    var line = text.Split('\n').FirstOrDefault(l => l.StartsWith("SOURCE_CHANGED", StringComparison.Ordinal));
    if (line == null) return -1;
    var at = line.IndexOf("paths=", StringComparison.Ordinal);
    return at < 0 ? -1 : int.Parse(line[(at + 6)..].Trim());
}
void ResetTree() => Git(fixture, "reset", "--hard", "--quiet");
void FreshState()
{
    ResetTree();
    File.Delete(Path.Combine(fixture, "state.json"));
    File.Delete(Path.Combine(fixture, "state.json.lock"));
    if (Directory.Exists(Path.Combine(fixture, "data"))) Directory.Delete(Path.Combine(fixture, "data"), true);
}

// ---- setup: one completed observation of fake.alpha at the current tree ----
FreshState();
var setup = Tick("setup");
Assert(setup.Exit == 0 && StateRunId() != null, "setup tick completes fake.alpha as a real observation (exit 0, completed record)");

// ---- A. pure staged source change invalidates ----
File.WriteAllText(Path.Combine(fixture, "src", "source.cs"), "// kernel source v2 staged\n");
Git(fixture, "add", "src/source.cs");
var beforeA = StateRunId();
var tickA = Tick("staged");
Assert(ChangedPathCount(tickA.Text) == 1, "pure staged source change reports exactly one changed path (was silently skipped): " + ChangedPathCount(tickA.Text));
Assert(StateRunId() != beforeA, "pure staged source change re-verifies instead of carrying the old measurement as current");

// ---- B. dirty source measured, then reverted to clean ----
FreshState();
File.WriteAllText(Path.Combine(fixture, "src", "source.cs"), "// kernel source v2 dirty\n");
var setupB = Tick("revert-setup");
Assert(setupB.Exit == 0 && StateRunId() != null, "dirty-tree setup tick completes fake.alpha at dirty source content");
var beforeB = StateRunId();
Git(fixture, "checkout", "--", "src/source.cs");
var tickB = Tick("revert");
Assert(ChangedPathCount(tickB.Text) == 1, "dirty->clean revert reports the previously dirty path (was paths=0, observation kept current): " + ChangedPathCount(tickB.Text));
Assert(StateRunId() != beforeB, "dirty->clean revert re-verifies; the dirty measurement never becomes the clean current result");

// ---- C. staged rename, then reversed ----
ResetTree();
var beforeC = StateRunId();
Git(fixture, "mv", "src/source.cs", "src/renamed.cs");
var tickC = Tick("rename");
Assert(ChangedPathCount(tickC.Text) == 2, "staged rename maps both old and new names into the impact set: " + ChangedPathCount(tickC.Text));
Assert(StateRunId() != beforeC, "staged rename re-verifies the observation");
var beforeC2 = StateRunId();
ResetTree();
var tickC2 = Tick("rename-revert");
Assert(ChangedPathCount(tickC2.Text) == 2, "rename reversal keeps both names in the union (reverse change is not lost): " + ChangedPathCount(tickC2.Text));
Assert(StateRunId() != beforeC2, "rename reversal re-verifies instead of silently keeping the old measurement");

// ---- D. staged delete, then restored ----
var beforeD = StateRunId();
Git(fixture, "rm", "--quiet", "src/source.cs");
var tickD = Tick("delete");
Assert(ChangedPathCount(tickD.Text) == 1, "staged delete maps the deleted path into the impact set: " + ChangedPathCount(tickD.Text));
Assert(StateRunId() != beforeD, "staged delete re-verifies the observation");
var beforeD2 = StateRunId();
ResetTree();
var tickD2 = Tick("delete-restore");
Assert(ChangedPathCount(tickD2.Text) == 1, "delete restore (reverse) still invalidates through the previous dirty path set: " + ChangedPathCount(tickD2.Text));
Assert(StateRunId() != beforeD2, "delete restore re-verifies instead of silently keeping the old measurement");

// ---- E. historical poison recovery: a bad indexed run is isolated on the next tick ----
ResetTree();
var runDirs = Directory.GetDirectories(Path.Combine(fixture, "data", "runs")).Order().ToList();
var victim = runDirs.First();
var victimId = Path.GetFileName(victim);
var victimManifest = Path.Combine(victim, "manifest.json");
var victimDoc = JsonNode.Parse(File.ReadAllText(victimManifest))!;
victimDoc["cases"]![0]!["status"] = "bogus-status";
File.WriteAllText(victimManifest, victimDoc.ToJsonString());
var tickE = Tick("quarantine");
Assert(tickE.Exit == 0, "a poisoned historical run neither crashes nor blocks the tick (was permanent full-root pollution): " + tickE.Exit);
var stateE = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "state.json")))!;
Assert(stateE["quarantined"]?[victimId] != null, "the tick-start sweep quarantines the poisoned run");
var indexE = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "data", "index.json")))!;
Assert(!indexE["runs"]!.AsArray().Any(r => r!["id"]!.GetValue<string>() == victimId), "the poisoned run leaves the validated index so later batches can proceed");
Assert(File.Exists(victimManifest), "the poisoned run's raw evidence is retained untouched");

// ---- F. input errors are rejected before a batch; definite collector failures are bounded ----
FreshState();
var tickF0 = Tick("config-reject", "--samples", "0");
Assert(tickF0.Exit == 2 && tickF0.Text.Contains("WATCH_CONFIG_INVALID", StringComparison.Ordinal), "invalid --samples is rejected before any batch starts (was swallowed as an interrupted round): " + tickF0.Exit);
Assert(!Directory.Exists(Path.Combine(fixture, "data", "runs")) || Directory.GetDirectories(Path.Combine(fixture, "data", "runs")).Length == 0, "a config error creates no run and starts no batch");
var refreshPath = Path.Combine(fixture, "scripts", "RefreshWebProgress.cs");
var refreshOriginal = File.ReadAllText(refreshPath);
File.WriteAllText(refreshPath, "this is not valid C# !!!\n");
int StateAttempts()
{
    var state = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "state.json")))!;
    var item = state["pending"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => p["caseId"]!.GetValue<string>() == "fake.alpha");
    return item?["attempts"]?.GetValue<int>() ?? state["blocked"]!["fake.alpha"]?["attempts"]?.GetValue<int>() ?? 0;
}
var tickF1 = Tick("run-failed-1");
Assert(tickF1.Exit == 2 && tickF1.Text.Contains("BATCH_RUN_FAILED", StringComparison.Ordinal) && StateAttempts() == 1, "a collector failing without a manifest burns a bounded attempt and exits non-zero (was exit 0 forever): " + tickF1.Exit + " attempts=" + StateAttempts());
var tickF2 = Tick("run-failed-2");
Assert(tickF2.Exit == 2 && StateAttempts() == 2, "the second definite failure keeps retrying bounded: attempts=" + StateAttempts());
var tickF3 = Tick("run-failed-3");
var stateF = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, "state.json")))!;
Assert(tickF3.Exit == 2 && stateF["blocked"]!.AsObject().ContainsKey("fake.alpha") && stateF["pending"]!.AsArray().Count == 0, "after the bounded retries the case parks instead of retrying forever: " + tickF3.Exit);
File.WriteAllText(refreshPath, refreshOriginal);
var tickF4 = Tick("run-failed-recovered");
Assert(tickF4.Exit == 0 && tickF4.Text.Contains("RESULT fake.alpha", StringComparison.Ordinal), "restoring the collector re-verifies the affected case normally (tracked source change): " + tickF4.Exit);
var tickF5 = Tick("run-failed-complete");
Assert(tickF5.Exit == 0 && tickF5.Text.Contains("queue complete", StringComparison.Ordinal), "the queue then completes normally; failures were never declared success with exit 0: " + tickF5.Exit);

if (failures.Count > 0)
{
    Console.WriteLine("FAILURES " + failures.Count + "/" + (passed + failures.Count));
    return 1;
}
Console.WriteLine("ALL PASS " + passed + " watch driver regression checks");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;
