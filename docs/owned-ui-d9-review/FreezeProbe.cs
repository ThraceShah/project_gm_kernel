#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
var repo = Path.GetFullPath("../..", Path.GetDirectoryName(Source())!); var work = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "freeze-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
const string fixedTools = "d9fd0605f9beb3e23630c6b54fed3642b2d842da"; var input = Path.Combine(work, "editable", "scripts"); var ui = Path.Combine(work, "ui-input"); var previous = Path.Combine(work, "current-bundle"); var frozen = Path.Combine(work, "frozen");
void Put(string file, string value) { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, value); }
string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Dictionary<string, string> Files(string dir) => Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), f => Hash(File.ReadAllBytes(f)), StringComparer.Ordinal);
string Tree(string dir) { using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); foreach (var p in Files(dir).OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.AppendData(Encoding.UTF8.GetBytes(p.Key)); hash.AppendData(Encoding.UTF8.GetBytes(p.Value)); } return Convert.ToHexStringLower(hash.GetHashAndReset()); }
async Task<(int Code, string Text)> Run(string label, string cwd, string exe, params string[] arguments) { var info = new ProcessStartInfo(exe) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in arguments) info.ArgumentList.Add(arg); info.Environment["MSBUILDDISABLENODEREUSE"] = "1"; using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); var text = await output + "\n" + await error; Put(Path.Combine(work, label + ".log"), "exit=" + process.ExitCode + "\n" + text); Console.WriteLine(label + " exit=" + process.ExitCode); return (process.ExitCode, text); }
var names = new[] { "Directory.Build.props", "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs", "ReviewedWebProgressRelease.cs", "WebProgressDataValidation.cs", "WebProgressPublishing.cs", "WebProgressMetadata.cs" }; var source = new Dictionary<string, string>();
foreach (var name in names.Append("FreezeReviewedWebProgressUi.cs")) { var result = await Run("git-" + name, repo, "git", "show", fixedTools + ":scripts/" + name); if (result.Code != 0) throw new Exception("git object unavailable"); var path = Path.Combine(input, name); Put(path, result.Text[..^1]); source[name] = Hash(File.ReadAllBytes(path)); }
// Deliberately edit ONLY the private input copy, never the implementation tree.
File.AppendAllText(Path.Combine(input, "PublishReviewedWebProgressUi.cs"), "// EDITABLE_INPUT_MUST_NOT_ENTER_RELEASE\n");
var dist = Path.Combine(ui, "web", "dist"); Put(Path.Combine(dist, "index.html"), "<script src=\"/apps/gm-kernel-progress/assets/app.js\"></script>FROZEN_FIXTURE"); Put(Path.Combine(dist, "assets", "app.js"), "FIXED_UI_FIXTURE"); Put(Path.Combine(ui, "web", "source.txt"), "accepted fixture source");
Directory.CreateDirectory(Path.Combine(previous, "data", "runs", "r")); Put(Path.Combine(previous, "index.html"), "CURRENT_UI424_FIXTURE");
Put(Path.Combine(previous, "data", "index.json"), "{\"schemaVersion\":1,\"generatedAt\":\"fixture\",\"latestRunId\":\"r\",\"runs\":[{\"id\":\"r\",\"commit\":\"fixture\",\"generatedAt\":\"fixture\",\"manifest\":\"runs/r/manifest.json\",\"label\":\"fixture\"}]}");
Put(Path.Combine(previous, "data", "runs", "r", "manifest.json"), "{\"schemaVersion\":1,\"run\":{\"id\":\"r\",\"commit\":\"fixture\",\"generatedAt\":\"fixture\",\"pkVersion\":null,\"host\":{},\"configuration\":{},\"scope\":\"BCL fixture\",\"provenance\":{}},\"cases\":[]}");
foreach (var arguments in new[] { new[] { "init", "-q" }, new[] { "add", "web" }, new[] { "-c", "user.name=Review", "-c", "user.email=review@example.invalid", "commit", "-qm", "fixed fixture" } }) if ((await Run("ui-git-" + Guid.NewGuid().ToString("N"), ui, "git", arguments)).Code != 0) throw new Exception("fixture git setup");
var commit = (await Run("ui-head", ui, "git", "rev-parse", "HEAD")).Text.Trim(); var review = new string('b', 40);
string Relative(string path) => Path.GetRelativePath(input, path);
var freezeArgs = new[] { "run", "--no-cache", "--file", Path.Combine(input, "FreezeReviewedWebProgressUi.cs"), "--", "--tools-commit", fixedTools, "--ui-commit", commit, "--review", review, "--dist-hash", Tree(dist), "--previous-tree-hash", Tree(previous), "--ui-tree", Relative(ui), "--previous-bundle", Relative(previous), "--release", Relative(frozen) };
if ((await Run("freeze", repo, "dotnet", freezeArgs)).Code != 0) throw new Exception("freeze");
var frozenScripts = Path.Combine(frozen, "tools", "scripts"); if (names.Any(name => Hash(File.ReadAllBytes(Path.Combine(frozenScripts, name))) != source[name]) || Hash(File.ReadAllBytes(Path.Combine(input, "PublishReviewedWebProgressUi.cs"))) == source["PublishReviewedWebProgressUi.cs"]) throw new Exception("editable input entered frozen release");
Console.WriteLine("PASS seven frozen sources equal exact Git objects despite private editable input drift");
if ((await Run("prepare", repo, "dotnet", "run", "--no-cache", "--file", Path.Combine(frozenScripts, "PublishReviewedWebProgressUi.cs"), "--", "--mode", "prepare", "--work", "../../work", "--dist", "../../ui", "--data", Path.GetRelativePath(frozenScripts, Path.Combine(previous, "data")), "--ui-commit", commit, "--dist-hash", Tree(dist), "--review", review)).Code != 0 || File.Exists(Path.Combine(frozen, "work", "published.json"))) throw new Exception("prepare");
Console.WriteLine("PASS fixed frozen tools prepare independently without publication");
Put(Path.Combine(ui, "web", "source.txt"), "unreviewed source edit"); freezeArgs[^1] = Relative(Path.Combine(work, "dirty-rejected")); var dirty = await Run("dirty-ui", repo, "dotnet", freezeArgs); if (dirty.Code != 134 || !dirty.Text.Contains("accepted UI source checkout drifted") || Directory.Exists(Path.Combine(work, "dirty-rejected"))) throw new Exception("dirty UI accepted");
Console.WriteLine("PASS dirty accepted UI checkout refused before release creation");
Put(Path.Combine(work, "receipt.json"), new JsonObject { ["toolsCommit"] = fixedTools, ["uiCommit"] = commit, ["releaseJsonSha256"] = Hash(File.ReadAllBytes(Path.Combine(frozen, "release.json"))), ["fixedSourceHashes"] = new JsonObject(source.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value)))), ["published"] = false }.ToJsonString());
Console.WriteLine("SUMMARY 3/3 fixed object freeze checks; " + Path.GetRelativePath(repo, work));
static string Source([CallerFilePath] string path = "") => path;
