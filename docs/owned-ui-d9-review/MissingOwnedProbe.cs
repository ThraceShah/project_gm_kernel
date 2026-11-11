#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Use only an OwnedProbe fixture; copy it before testing external deletion.
var here = Path.GetDirectoryName(Source())!; var repo = Path.GetFullPath("../..", here); var input = Path.GetFullPath(args.Single(), here);
var relative = Path.GetRelativePath(Path.Combine(repo, "temp_docs", "owned-ui-d9-review"), input); if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal)) throw new Exception("own fixture required");
var work = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "missing-owned-" + Guid.NewGuid().ToString("N")); var scenario = Path.Combine(work, "scenario"); var original = Path.Combine(input, "scenario");
void Copy(string from, string to) { foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); } }
void Frozen(string dir) { foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead); foreach (var folder in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).Append(dir)) File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); }
string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))); JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
foreach (var dir in new[] { "prior", "next", "producer", "units", "current" }) Copy(Path.Combine(original, dir), Path.Combine(scenario, dir));
var prior = Path.Combine(scenario, "prior"); var next = Path.Combine(scenario, "next"); var scripts = Path.Combine(next, "tools", "scripts"); var nextWork = Path.Combine(next, "work"); var units = Path.Combine(scenario, "units"); var hook = Path.Combine(units, "gm-kernel-progress-refresh.service.d", "zz-reviewed-ui.conf");
Directory.Delete(Path.Combine(nextWork, "hook-backup"), true); File.Delete(Path.Combine(nextWork, "ROLLED_BACK")); File.Delete(Path.Combine(nextWork, "published.json"));
var portal = Path.Combine(input, "stub", "bin", "Debug", "net10.0", "portalctl");
var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); var hookText = File.ReadAllText(hook).Replace(Path.GetRelativePath(profile, original), Path.GetRelativePath(profile, scenario), StringComparison.Ordinal);
var oldWords = hookText.Split(' '); var portalAt = Array.IndexOf(oldWords, "--portalctl"); hookText = hookText.Replace("--portalctl " + oldWords[portalAt + 1], "--portalctl " + Path.GetRelativePath(Path.Combine(prior, "tools", "scripts"), portal), StringComparison.Ordinal); File.WriteAllText(hook, hookText);
var statePath = Path.Combine(prior, "work", "hook-backup", "state.json"); var oldState = Read(statePath); oldState["hookHash"] = Sha(hook); File.WriteAllText(statePath, oldState.ToJsonString());
foreach (var release in new[] { prior, next }) foreach (var dir in new[] { "tools", "ui", "previous-static" }) Frozen(Path.Combine(release, dir));
var stub = Read(Path.Combine(original, "stub.json")); stub["hook"] = hook; stub["servedFile"] = Path.Combine(work, "served.txt"); stub["active"] = true; File.WriteAllText(Path.Combine(work, "served.txt"), Path.Combine(scenario, "current")); var stubState = Path.Combine(work, "stub.json"); File.WriteAllText(stubState, stub.ToJsonString());
// Unlink without daemon-reload leaves the old command loaded in a real manager.
// Model that read behavior explicitly instead of making the stub crash on unlink.
stub["missingHookCommand"] = hookText.Split('\n').Single(line => line.StartsWith("ExecStartPost=dotnet "))["ExecStartPost=".Length..]; File.WriteAllText(stubState, stub.ToJsonString());
var stubProject = Path.Combine(work, "stub"); Directory.CreateDirectory(stubProject); File.Copy(Path.Combine(input, "stub", "Stub.csproj"), Path.Combine(stubProject, "Stub.csproj"));
var stubSource = File.ReadAllText(Path.Combine(input, "stub", "Program.cs")); const string readHook = "File.ReadAllLines(s[\"hook\"]!.GetValue<string>())";
stubSource = stubSource.Replace(readHook, "(File.Exists(s[\"hook\"]!.GetValue<string>())?" + readHook + ":new[]{\"ExecStartPost=\"+s[\"missingHookCommand\"]!.GetValue<string>()})", StringComparison.Ordinal); File.WriteAllText(Path.Combine(stubProject, "Program.cs"), stubSource);
var buildInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in new[] { "build", Path.Combine(stubProject, "Stub.csproj"), "-m:1", "-p:UseSharedCompilation=false" }) buildInfo.ArgumentList.Add(arg); buildInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
using (var build = Process.Start(buildInfo)!) { var stdout = build.StandardOutput.ReadToEndAsync(); var stderr = build.StandardError.ReadToEndAsync(); await build.WaitForExitAsync(); File.WriteAllText(Path.Combine(work, "stub-build.log"), "exit=" + build.ExitCode + "\n" + await stdout + "\n" + await stderr); if (build.ExitCode != 0) throw new Exception("private stub build"); }
var bin = Path.Combine(stubProject, "bin", "Debug", "net10.0"); File.Copy(Path.Combine(bin, "Stub"), Path.Combine(bin, "systemctl")); File.SetUnixFileMode(Path.Combine(bin, "systemctl"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
var manifest = Read(Path.Combine(next, "release.json")); string Rel(string path) => Path.GetRelativePath(scripts, path);
// Independently check relocated fixture pins and old/new path identity before
// unlinking; the negative is not explained by a stale relocation or bad pin.
foreach (var release in new[] { prior, next })
{
    var pinned = Read(Path.Combine(release, "release.json"))["files"]!.AsObject(); var count = 0;
    foreach (var dir in new[] { "tools", "ui", "previous-static" }) foreach (var file in Directory.GetFiles(Path.Combine(release, dir), "*", SearchOption.AllDirectories)) { var key = Path.GetRelativePath(release, file).Replace('\\', '/'); if (pinned[key]?.GetValue<string>() != Sha(file)) throw new Exception("relocated frozen file differs"); count++; }
    if (count != pinned.Count) throw new Exception("relocated file set differs");
}
var oldScriptRoot = Path.Combine(prior, "tools", "scripts"); var words = hookText.Split(' ');
foreach (var pair in new[] { ("--data", Path.Combine(scenario, "producer")), ("--portalctl", portal) }) if (Path.GetFullPath(words[Array.IndexOf(words, pair.Item1) + 1], oldScriptRoot) != pair.Item2) throw new Exception("old/new path identity differs");
if (Path.GetFullPath(oldState["unitDir"]!.GetValue<string>(), oldScriptRoot) != units || oldState["hookHash"]!.GetValue<string>() != Sha(hook)) throw new Exception("old state identity differs");
Console.WriteLine("PASS relocated frozen sets pins and old/new producer portal unit identities before deletion");
// The only ownership change under test: the proven old hook is externally removed.
var deletedHookSha = Sha(hook); File.Delete(hook);
var info = new ProcessStartInfo("dotnet") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (var arg in new[] { "run", "--no-cache", "--file", Path.Combine(scripts, "ConfigureReviewedWebProgressUiHook.cs"), "--", "--mode", "install", "--execute", "--work", "../../work", "--unit-dir", Rel(units), "--dist", "../../ui", "--data", Rel(Path.Combine(scenario, "producer")), "--baseline-data", "../../previous-static/data", "--portalctl", Rel(portal), "--previous-bundle", "../../previous-static", "--ui-commit", manifest["uiCommit"]!.GetValue<string>(), "--dist-hash", manifest["uiHash"]!.GetValue<string>(), "--review", manifest["review"]!.GetValue<string>(), "--release-sha256", Sha(Path.Combine(next, "release.json")), "--previous-release", Rel(prior), "--previous-release-sha256", Sha(Path.Combine(prior, "release.json")), "--previous-state-sha256", Sha(statePath) }) info.ArgumentList.Add(arg);
info.Environment["MSBUILDDISABLENODEREUSE"] = "1"; info.Environment["OWNED_REVIEW_STATE"] = stubState; info.Environment["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); var text = await output + "\n" + await error; File.WriteAllText(Path.Combine(work, "install.log"), "exit=" + process.ExitCode + "\n" + text);
var backup = Path.Combine(nextWork, "hook-backup", "state.json"); var bypass = process.ExitCode == 0 && File.Exists(hook) && File.Exists(backup) && Read(backup)["ownedUpgrade"]!.GetValue<bool>() == false && !Read(stubState)["active"]!.GetValue<bool>();
File.WriteAllText(Path.Combine(work, "receipt.json"), new JsonObject { ["cliExit"] = process.ExitCode, ["setupPinsAndPathIdentityValid"] = true, ["deletedOwnedHookSha256"] = deletedHookSha, ["newHookExists"] = File.Exists(hook), ["recordedOwnedUpgrade"] = File.Exists(backup) ? Read(backup)["ownedUpgrade"]!.DeepClone() : null, ["timerActiveAfter"] = Read(stubState)["active"]!.DeepClone(), ["counterexample"] = bypass }.ToJsonString());
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(repo, work)); Console.WriteLine("CLI exit=" + process.ExitCode + " counterexample=" + bypass); if (!bypass) throw new Exception("expected counterexample not observed; inspect evidence");
static string Source([CallerFilePath] string path = "") => path;
