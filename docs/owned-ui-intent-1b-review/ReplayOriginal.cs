#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// The original d9 diagnostic is executed verbatim. Only a NEW input fixture
// receives the fixed Configure source; old inputs, receipts and probe stay intact.
var repo = Path.GetFullPath("../..", Path.GetDirectoryName(Source())!);
var originalProbe = Path.Combine(repo, "docs", "owned-ui-d9-review", "MissingOwnedProbe.cs");
var priorInput = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "ee5bdd5ec81f4be4baca876eb3af711f");
var oldReceipt = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "missing-owned-afcccdbee8354d599aa007ee9e788dee", "receipt.json");
var firstReceipt = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "missing-owned-7be1d950cee2400388dfec46408d1a37", "receipt.json");
var input = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "intent-input-" + Guid.NewGuid().ToString("N"));
var work = Path.Combine(repo, "temp_docs", "owned-ui-intent-1b-review", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
void Save(string path, JsonNode node) => File.WriteAllText(path, node.ToJsonString());
void Require(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
void Copy(string from, string to) { foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); } }
Require(Sha(originalProbe) == "26d1039ce7322273ae5102a00810bd9a21712e71cf2eaad37dbd984336d2d646" && Sha(oldReceipt) == "870dc55a3a6effa735e30d393a791e2c0a9035a3c4d88725da7e9f8e17579eb0" && Sha(firstReceipt) == "09d75f4bac049e6bf433c24322f19c3cc539d57c96d5b9ce081839d71023b78c", "original probe and both original d9 receipts retain pinned bytes");
var originalSource = Sha(Path.Combine(priorInput, "scenario", "next", "tools", "scripts", "ConfigureReviewedWebProgressUiHook.cs"));
foreach (var dir in new[] { "prior", "next", "producer", "units", "current" }) Copy(Path.Combine(priorInput, "scenario", dir), Path.Combine(input, "scenario", dir)); Copy(Path.Combine(priorInput, "stub"), Path.Combine(input, "stub"));
File.Copy(Path.Combine(priorInput, "scenario", "stub.json"), Path.Combine(input, "scenario", "stub.json"));
var configure = Path.Combine(input, "scenario", "next", "tools", "scripts", "ConfigureReviewedWebProgressUiHook.cs"); File.SetUnixFileMode(configure, UnixFileMode.UserRead | UnixFileMode.UserWrite); File.WriteAllBytes(configure, File.ReadAllBytes(Path.Combine(repo, "scripts", "ConfigureReviewedWebProgressUiHook.cs"))); File.SetUnixFileMode(configure, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
Require(Sha(configure) == "929082a9771efc025aadfb19b55c98b4007f7e0623c1c071ccba6fe0e72d2173", "new input runs exact fixed Configure source");
var manifestPath = Path.Combine(input, "scenario", "next", "release.json"); var manifest = Read(manifestPath); manifest["toolsCommit"] = "1b935972eff529e13b46cb9b08ad9550e0a6aeb5"; manifest["files"]!["tools/scripts/ConfigureReviewedWebProgressUiHook.cs"] = Sha(configure); Save(manifestPath, manifest);
File.Delete(Path.Combine(input, "scenario", "next", "work", "hook.lock"));
// File.Copy preserves file mode, not directory mode. Restore the declared
// frozen directory modes only in this new input before the unchanged probe.
foreach (var release in new[] { "prior", "next" }) foreach (var dir in new[] { "tools", "ui", "previous-static" }) { var root = Path.Combine(input, "scenario", release, dir); foreach (var path in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).Append(root)) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); }
async Task<(int Code, string Text)> Run(string label, string[] arguments, string? state = null, string? bin = null)
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in arguments) info.ArgumentList.Add(arg); info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    if (state is not null) { info.Environment["OWNED_REVIEW_STATE"] = state; info.Environment["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"); }
    using var process = Process.Start(info)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); try { await process.WaitForExitAsync(timeout.Token); } catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
    var text = await stdout + "\n" + await stderr; File.WriteAllText(Path.Combine(work, label + ".log"), "exit=" + process.ExitCode + "\n" + text); Console.WriteLine("CLI " + label + " exit=" + process.ExitCode); return (process.ExitCode, text);
}
var original = await Run("original-probe-verbatim", ["run", "--no-cache", "--file", originalProbe, "--", Path.GetRelativePath(Path.GetDirectoryName(originalProbe)!, input)]);
var match = Regex.Match(original.Text, @"EVIDENCE ([^\r\n]+)"); Require(match.Success, "unchanged original probe emitted its own evidence directory"); var evidence = Path.GetFullPath(match.Groups[1].Value, repo); var receipt = Read(Path.Combine(evidence, "receipt.json")); var installText = File.ReadAllText(Path.Combine(evidence, "install.log"));
Require(original.Code == 134 && receipt["cliExit"]!.GetValue<int>() == 134 && installText.Contains("UI_OWNED_TRANSITION_HOOK_MISSING") && !receipt["newHookExists"]!.GetValue<bool>() && receipt["timerActiveAfter"]!.GetValue<bool>(), "verbatim d9 assertion now fails because fixed inner CLI134 refuses missing hook");
var scenario = Path.Combine(evidence, "scenario"); var next = Path.Combine(scenario, "next"); var scripts = Path.Combine(next, "tools", "scripts"); var units = Path.Combine(scenario, "units"); var stateFile = Path.Combine(evidence, "stub.json"); var calls = stateFile + ".calls"; var bin = Path.Combine(evidence, "stub", "bin", "Debug", "net10.0"); var nextWork = Path.Combine(next, "work");
Require(!File.Exists(calls) && !File.Exists(Path.Combine(nextWork, "hook.lock")) && !Directory.Exists(Path.Combine(nextWork, "hook-backup")), "first verbatim replay made zero stub calls and no lease or backup");
string Rel(string path) => Path.GetRelativePath(scripts, path);
var prior = Path.Combine(scenario, "prior"); var newRecord = Read(Path.Combine(next, "release.json")); var portal = Path.Combine(input, "stub", "bin", "Debug", "net10.0", "portalctl");
var arguments = new[] { "--mode", "install", "--execute", "--work", "../../work", "--unit-dir", Rel(units), "--dist", "../../ui", "--data", Rel(Path.Combine(scenario, "producer")), "--baseline-data", "../../previous-static/data", "--portalctl", Rel(portal), "--previous-bundle", "../../previous-static", "--ui-commit", newRecord["uiCommit"]!.GetValue<string>(), "--dist-hash", newRecord["uiHash"]!.GetValue<string>(), "--review", newRecord["review"]!.GetValue<string>(), "--release-sha256", Sha(Path.Combine(next, "release.json")), "--previous-release", Rel(prior), "--previous-release-sha256", Sha(Path.Combine(prior, "release.json")), "--previous-state-sha256", Sha(Path.Combine(prior, "work", "hook-backup", "state.json")) };
JsonObject Snapshot()
{
    var files = new JsonObject(); foreach (var file in Directory.GetFiles(scenario, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)) files[Path.GetRelativePath(scenario, file).Replace('\\', '/')] = Sha(file);
    return new JsonObject { ["scenarioFiles"] = files, ["directories"] = new JsonArray(Directory.GetDirectories(scenario, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(scenario, p)).Order(StringComparer.Ordinal).Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()), ["timerState"] = Sha(stateFile), ["publicationPointer"] = Sha(Path.Combine(evidence, "served.txt")), ["calls"] = File.Exists(calls) ? Sha(calls) : null };
}
var cases = new List<(string Label, string Marker, string[] Arguments)> { ("missing-owned-zero-effects", "UI_OWNED_TRANSITION_HOOK_MISSING", arguments) };
var previousNames = new[] { "--previous-release", "--previous-release-sha256", "--previous-state-sha256" };
foreach (var name in new[] { "only-release", "missing-state", "duplicate-state", "inline-state", "no-state-value" })
{
    var a = arguments.ToList(); void Remove(string key) { var at = a.IndexOf(key); a.RemoveRange(at, 2); }
    if (name == "only-release") { Remove(previousNames[1]); Remove(previousNames[2]); } else if (name == "missing-state") Remove(previousNames[2]); else if (name == "duplicate-state") { a.Add(previousNames[2]); a.Add(new string('0', 64)); } else if (name == "inline-state") { foreach (var key in previousNames) Remove(key); a.Add(previousNames[2] + "=" + new string('0', 64)); } else a.RemoveAt(a.IndexOf(previousNames[2]) + 1);
    cases.Add((name, "UI_OWNED_TRANSITION_IDENTITY_REQUIRED", a.ToArray()));
}
var results = new JsonArray();
foreach (var c in cases)
{
    var before = Snapshot(); var result = await Run(c.Label, ["run", "--no-cache", "--file", Path.Combine(scripts, "ConfigureReviewedWebProgressUiHook.cs"), "--", .. c.Arguments], stateFile, bin); var after = Snapshot();
    var unchanged = JsonNode.DeepEquals(before, after); Require(result.Code == 134 && result.Text.Contains(c.Marker) && unchanged && !File.Exists(calls), c.Label + " CLI134 correct reason and zero scenario/work/timer/publication/control effects");
    Save(Path.Combine(work, c.Label + "-effects.json"), new JsonObject { ["before"] = before, ["after"] = after, ["unchanged"] = unchanged }); results.Add((JsonNode)new JsonObject { ["case"] = c.Label, ["exit"] = result.Code, ["unchanged"] = unchanged, ["systemctlCalls"] = 0 });
}
Require(Sha(originalProbe) == "26d1039ce7322273ae5102a00810bd9a21712e71cf2eaad37dbd984336d2d646" && Sha(oldReceipt) == "870dc55a3a6effa735e30d393a791e2c0a9035a3c4d88725da7e9f8e17579eb0" && Sha(firstReceipt) == "09d75f4bac049e6bf433c24322f19c3cc539d57c96d5b9ce081839d71023b78c" && Sha(Path.Combine(priorInput, "scenario", "next", "tools", "scripts", "ConfigureReviewedWebProgressUiHook.cs")) == originalSource, "original d9 probe receipts and input source unchanged after all replays");
Save(Path.Combine(work, "receipt.json"), new JsonObject { ["candidate"] = "1b935972eff529e13b46cb9b08ad9550e0a6aeb5", ["sourceSha256"] = Sha(configure), ["originalProbeSha256"] = Sha(originalProbe), ["originalOuterExit"] = original.Code, ["originalInnerExit"] = receipt["cliExit"]!.DeepClone(), ["originalEvidence"] = Path.GetRelativePath(repo, evidence), ["controls"] = results });
Console.WriteLine("SUMMARY original verbatim replay plus 6/6 independent zero-effects controls; " + Path.GetRelativePath(repo, work));
static string Source([CallerFilePath] string path = "") => path;
