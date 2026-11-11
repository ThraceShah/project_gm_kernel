#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Narrow fixed-015 replay: exact old tool, no overlay, fresh producer inputs at one path.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
var work = Path.Combine(root, "temp_docs/fusion-015-retention");
try
{
    const string candidate = "015cd308e147356fecac96bb7354ab1e92c36a03";
    const string toolCommit = "1b935972eff529e13b46cb9b08ad9550e0a6aeb5";
    const string expectedUi = "f0593760ed1e22dd73e94f609e9e2353366370b542019a90cb8f486f796a4dd6";
    const string expectedBundle = "8dca4b3500652fe0ff5161a8a5943abdcc5a69d0a40b472de17e95b1ca583069";
    const string oldBundle = "5f43710720d36735d1972666c4f80013fa00b8d3d58fc0a623b4ad2601c3c7e5";
    if (Directory.Exists(work)) throw new IOException("Preserve existing evidence; require fresh work.");
    var ui = Path.Combine(root, "temp_docs/fusion-015-source");
    var tools = Path.Combine(root, "temp_docs/fusion-tool-1b/scripts");
    var previous = Path.Combine(root, "temp_docs/fusion-retention-repro/first-bundle");
    var historySource = Path.Combine(root, "temp_docs/fusion-retention-repro/fresh-main610-data");
    var history = Files(historySource); var old = Files(previous);
    Require(TreeHash(old) == oldBundle && history.Count == 607, "preserved original 3c/history fixtures");
    var display = Files(Path.Combine(previous, "data/display/real-uv"));
    Require(display.Count == 15 && TreeHash(display) == TreeHash(Files(Path.Combine(ui, "web/public/display/real-uv"))), "all original 15 bytes unchanged after relocation");
    Require(TreeHash(Files(Path.Combine(ui, "web/dist"), true)) == expectedUi, "independent 015 build UI hash");
    var release = Path.Combine(work, "release");
    Run(tools, "FreezeReviewedWebProgressUi.cs", "--tools-commit", toolCommit, "--ui-commit", candidate,
        "--review", new string('0', 40), "--dist-hash", expectedUi, "--previous-tree-hash", oldBundle,
        "--ui-tree", Rel(tools, ui), "--previous-bundle", Rel(tools, previous), "--release", Rel(tools, release));
    var frozenUi = Files(Path.Combine(release, "ui"));
    Require(frozenUi.Count == 18 && display.All(p => frozenUi["display/real-uv/" + p.Key] == p.Value), "fixed freeze includes all display assets");
    var frozenScripts = Path.Combine(release, "tools/scripts");
    foreach (var name in new[] { "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs", "ReviewedWebProgressRelease.cs" })
        Require(Sha(File.ReadAllBytes(Path.Combine(frozenScripts, name))) == Sha(File.ReadAllBytes(Path.Combine(tools, name))), "unchanged frozen tool " + name);
    var active = Path.Combine(work, "active-main610-data"); var releaseWork = Path.Combine(release, "work");
    var rounds = new JsonArray();
    for (var round = 1; round <= 2; round++)
    {
        Copy(historySource, active);
        Require(!Directory.Exists(Path.Combine(active, "display")) && Files(active).Count == 607, "fresh input has no overlay");
        Run(frozenScripts, "PublishReviewedWebProgressUi.cs", "--mode", "prepare", "--dist", "../../ui",
            "--data", Rel(frozenScripts, active), "--baseline-data", "../../previous-static/data", "--work", "../../work",
            "--ui-commit", candidate, "--dist-hash", expectedUi, "--review", new string('0', 40));
        var metadata = JsonNode.Parse(File.ReadAllBytes(Path.Combine(releaseWork, "prepared.json")))!;
        var bundle = Path.GetFullPath(metadata["bundle"]!.GetValue<string>(), releaseWork); var files = Files(bundle);
        Require(files.Count == 625 && TreeHash(files) == expectedBundle, "round full bundle identity");
        Require(history.All(p => files["data/" + p.Key] == p.Value), "every old historical/index byte");
        Require(display.All(p => files["display/real-uv/" + p.Key] == p.Value), "every relocated display byte");
        var entry = new JsonObject { ["round"] = round, ["metadata"] = metadata, ["bundle"] = Rel(root, bundle), ["bundleHash"] = TreeHash(files), ["displayFiles"] = 15, ["historicalFilesUnchanged"] = 607 };
        rounds.Add(entry);
        Directory.Move(active, Path.Combine(work, "round-" + round + "-input-preserved"));
        if (round == 1)
        {
            var preserved = Path.Combine(work, "round-1-work-preserved");
            Directory.Move(releaseWork, preserved);
            entry["bundle"] = Rel(root, Path.Combine(preserved, metadata["bundle"]!.GetValue<string>()));
        }
    }
    Require(TreeHash(Files(previous)) == oldBundle && TreeHash(Files(historySource)) == TreeHash(history), "original evidence unchanged");
    File.WriteAllText(Path.Combine(work, "retention-report.json"), new JsonObject { ["candidate"] = candidate, ["tools"] = toolCommit,
        ["fixtureReviewPin"] = new string('0', 40), ["uiHash"] = expectedUi, ["frozenUiFiles"] = frozenUi.Count,
        ["sameDataArgumentBothRounds"] = true, ["freshInputsWithoutDisplayOverlay"] = true, ["independentWorkDirectories"] = true,
        ["rounds"] = rounds, ["verdict"] = "PASS fixed 015 namespace retention only" }.ToJsonString());
    Console.WriteLine("PASS 015: exact frozen UI18, fresh producer607 twice, bundle625/28214939B, original display15 retained");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 1; }

static string ScriptPath([CallerFilePath] string path = "") => path;
static string Rel(string from, string to) => Path.GetRelativePath(from, to).Replace('\\', '/');
static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static Dictionary<string, string> Files(string root, bool uiOnly = false) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
    .Select(f => (File: f, Path: Rel(root, f))).Where(p => !uiOnly || !p.Path.StartsWith("data/", StringComparison.Ordinal)).ToDictionary(p => p.Path, p => Sha(File.ReadAllBytes(p.File)), StringComparer.Ordinal);
static string TreeHash(Dictionary<string, string> files)
{
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (var (path, sha) in files.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.AppendData(Encoding.UTF8.GetBytes(path)); hash.AppendData(Encoding.UTF8.GetBytes(sha)); }
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}
static void Require(bool ok, string label) { if (!ok) throw new InvalidDataException(label); }
static void Copy(string source, string target)
{
    Directory.CreateDirectory(target);
    foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { var to = Path.Combine(target, Rel(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(file, to); }
}
static void Run(string directory, string script, params string[] arguments)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory };
    foreach (var arg in new[] { "run", script, "--" }.Concat(arguments)) start.ArgumentList.Add(arg);
    start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var child = Process.Start(start)!; child.WaitForExit(); Require(child.ExitCode == 0, script + " exit=" + child.ExitCode);
}
