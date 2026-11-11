#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Prepare-only two-round reproduction with exact frozen 1b tools. No service or portal calls.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
var work = Path.Combine(root, "temp_docs/fusion-retention-repro");
try
{
    if (Directory.Exists(work)) throw new IOException("Use a fresh isolated work directory; preserve previous evidence.");
    const string candidate = "3c650671f30efad266531b0512ccfa600adffe38";
    const string toolCommit = "1b935972eff529e13b46cb9b08ad9550e0a6aeb5";
    const string uiHash = "e0429cf28007de740018ebfc1898a630c1535be0bcdd668e4ed38b415a811ee4";
    const string bundleHash = "5f43710720d36735d1972666c4f80013fa00b8d3d58fc0a623b4ad2601c3c7e5";
    var ui = Path.Combine(root, "temp_docs/fusion-3c-source");
    var tools = Path.Combine(root, "temp_docs/fusion-tool-1b/scripts");
    var supplied = Path.GetFullPath("../gm-web-real-uv/temp_docs/reviewed-inspection/bundle-final", root);
    var producer = Path.GetFullPath("../gm-web-focus-layout/temp_docs/ui-c710-review/data", root);
    var original = Hashes(supplied); var history = Hashes(producer);
    Require(TreeHash(original) == bundleHash, "fixed supplied bundle hash");
    Require(TreeHash(Hashes(Path.Combine(ui, "web/dist"), true)) == uiHash, "independent source build identity");
    Require(history.Count == 607 && history["index.json"] == "117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990", "historical producer identity");
    Require(history.All(p => original["data/" + p.Key] == p.Value), "all original historical file bytes");
    var uv = original.Where(p => p.Key.StartsWith("data/display/real-uv/", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    Require(uv.Count == 15, "exact independent display package");
    var first = Path.Combine(work, "first-bundle"); var fresh = Path.Combine(work, "fresh-main610-data");
    Copy(supplied, first); Copy(producer, fresh);
    Require(!Directory.Exists(Path.Combine(fresh, "display")), "fresh producer has no UV overlay");
    var release = Path.Combine(work, "release");
    // All-zero review is an explicit local test fixture, never a release approval.
    Run(tools, "FreezeReviewedWebProgressUi.cs", "--tools-commit", toolCommit, "--ui-commit", candidate,
        "--review", new string('0', 40), "--dist-hash", uiHash, "--previous-tree-hash", bundleHash,
        "--ui-tree", Rel(tools, ui), "--previous-bundle", Rel(tools, first), "--release", Rel(tools, release));
    var frozenScripts = Path.Combine(release, "tools/scripts");
    var frozenUi = Hashes(Path.Combine(release, "ui"));
    Require(frozenUi.Count == 3 && frozenUi.Keys.All(p => !p.StartsWith("data/", StringComparison.Ordinal)), "actual freeze excluded all dist/data");
    foreach (var name in new[] { "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs", "ReviewedWebProgressRelease.cs" })
        Require(Hash(File.ReadAllBytes(Path.Combine(frozenScripts, name))) == Hash(File.ReadAllBytes(Path.Combine(tools, name))), "frozen exact tool " + name);
    var releaseWork = Path.Combine(release, "work");
    JsonNode Prepare(string data)
    {
        Run(frozenScripts, "PublishReviewedWebProgressUi.cs", "--mode", "prepare", "--dist", "../../ui",
            "--data", Rel(frozenScripts, data), "--baseline-data", "../../previous-static/data", "--work", "../../work",
            "--ui-commit", candidate, "--dist-hash", uiHash, "--review", new string('0', 40));
        return JsonNode.Parse(File.ReadAllBytes(Path.Combine(releaseWork, "prepared.json")))!;
    }
    var round1 = Prepare(Path.Combine(first, "data"));
    var firstPrepared = Path.GetFullPath(round1["bundle"]!.GetValue<string>(), releaseWork);
    Require(TreeHash(Hashes(firstPrepared)) == bundleHash, "round one exact complete bundle");
    var round2 = Prepare(fresh);
    var next = Path.GetFullPath(round2["bundle"]!.GetValue<string>(), releaseWork);
    var nextFiles = Hashes(next); var missing = uv.Keys.Where(p => !nextFiles.ContainsKey(p)).ToArray();
    Require(history.All(p => nextFiles["data/" + p.Key] == p.Value), "round two preserved original history/index");
    Require(TreeHash(Hashes(supplied)) == bundleHash && TreeHash(Hashes(producer)) == TreeHash(history), "read-only source bytes unchanged");
    var report = new JsonObject
    {
        ["candidate"] = candidate, ["tools"] = toolCommit, ["fixtureReviewPin"] = new string('0', 40),
        ["uiHash"] = uiHash, ["firstBundleHash"] = bundleHash, ["frozenUiFiles"] = frozenUi.Count,
        ["round1"] = round1, ["round2"] = round2, ["round2Bundle"] = Rel(root, next),
        ["round2BundleHash"] = TreeHash(nextFiles), ["missingUvPaths"] = new JsonArray(missing.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray()),
        ["historicalFilesUnchanged"] = history.Count, ["sourceFilesUnchanged"] = true,
        ["verdict"] = missing.Length == 0 ? "retained" : "BLOCKED: fresh producer refresh loses independent display namespace"
    };
    File.WriteAllText(Path.Combine(work, "retention-report.json"), report.ToJsonString());
    Console.WriteLine($"RETENTION_GATE missing={missing.Length}/15 historicalUnchanged={history.Count} round1={round1["files"]}/{round1["bytes"]} round2={round2["files"]}/{round2["bytes"]}");
    return missing.Length == 0 ? 0 : 1;
}
catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 2; }

static string ScriptPath([CallerFilePath] string path = "") => path;
static string Rel(string from, string to) => Path.GetRelativePath(from, to).Replace('\\', '/');
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static Dictionary<string, string> Hashes(string directory, bool uiOnly = false) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
    .Select(f => (File: f, Relative: Rel(directory, f))).Where(p => !uiOnly || !p.Relative.StartsWith("data/", StringComparison.Ordinal))
    .ToDictionary(p => p.Relative, p => Hash(File.ReadAllBytes(p.File)), StringComparer.Ordinal);
static string TreeHash(Dictionary<string, string> files)
{
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (var (path, sha) in files.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.AppendData(Encoding.UTF8.GetBytes(path)); hash.AppendData(Encoding.UTF8.GetBytes(sha)); }
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}
static void Require(bool ok, string name) { if (!ok) throw new InvalidDataException(name); }
static void Copy(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Rel(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
    }
}
static void Run(string directory, string script, params string[] arguments)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory };
    start.ArgumentList.Add("run"); start.ArgumentList.Add(script); start.ArgumentList.Add("--");
    foreach (var arg in arguments) start.ArgumentList.Add(arg);
    start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var child = Process.Start(start)!; child.WaitForExit();
    Require(child.ExitCode == 0, script + " exit=" + child.ExitCode);
}
