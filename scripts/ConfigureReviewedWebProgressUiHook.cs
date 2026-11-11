#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Operational companion to PublishReviewedWebProgressUi. Default is help;
// install/restore/resume are intentionally NOT run by integration verification.
var mode = Option("--mode", "plan");
if (mode == "plan" || args.Contains("--help"))
{
    Console.WriteLine("ConfigureReviewedWebProgressUiHook --mode install|restore|resume --execute --work <relative UI release work> --unit-dir <relative user units> [install: same --dist/--ui-commit/--dist-hash/--review/--data/--portalctl/--previous-bundle as PublishReviewedWebProgressUi]");
    return 0;
}
if (!args.Contains("--execute") || mode is not ("install" or "restore" or "resume")) throw new ArgumentException("explicit operational mode and --execute required after independent review");
var scripts = Path.GetDirectoryName(ScriptPath())!;
string Relative(string name)
{
    var text = Option(name, "");
    if (text.Length == 0 || Path.IsPathRooted(text) || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-'))) throw new ArgumentException(name + " requires a safe script-relative path");
    return text;
}
string Resolve(string name) => Path.GetFullPath(Relative(name), scripts);
var work = Resolve("--work"); var units = Resolve("--unit-dir");
var dropins = Path.Combine(units, "gm-kernel-progress-refresh.service.d");
var backup = Path.Combine(work, "hook-backup");
var recordPath = Path.Combine(backup, "state.json");
const string timer = "gm-kernel-progress-refresh.timer";
const string service = "gm-kernel-progress-refresh.service";
const string ownName = "zz-reviewed-ui.conf";
void Safe(string path)
{
    if (!WebProgressSafePath.IsSafeChain(path, out _)) throw new IOException("UI_HOOK_PATH_UNSAFE");
    if (Directory.Exists(path)) foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
        if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("UI_HOOK_PATH_UNSAFE");
}
Safe(work);
if (!WebProgressSafePath.IsSafeChain(units, out _)) throw new IOException("UI_HOOK_PATH_UNSAFE unit root");
Safe(dropins); // unrelated systemd enablement links are outside our write set
if (WebProgressSafePath.IsWithin(work, units) || WebProgressSafePath.IsWithin(units, work)) throw new IOException("UI_HOOK_PATH_OVERLAP");
Directory.CreateDirectory(work);
using var lease = new FileStream(Path.Combine(work, "hook.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
async Task<(int Exit, string Text)> Systemctl(params string[] arguments)
{
    var info = new ProcessStartInfo("systemctl") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add("--user"); foreach (var arg in arguments) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var result = (process.ExitCode, (await output).Trim());
    Console.WriteLine("systemctl --user " + string.Join(" ", arguments) + " exit=" + result.ExitCode + (arguments[0] == "cat" ? " [saved privately]" : " " + result.Item2));
    Console.Error.WriteLine((await error).Trim()); return result;
}
async Task Require(params string[] arguments) { if ((await Systemctl(arguments)).Exit != 0) throw new IOException("systemctl failed"); }
async Task RequireIdleWriter()
{
    var result = await Systemctl("show", service, "--property=ActiveState", "--property=SubState", "--property=MainPID", "--property=Job", "--property=TasksCurrent", "--property=ControlGroup");
    var values = result.Text.Split('\n').Select(s => s.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);
    if (result.Exit != 0 || !values.TryGetValue("ActiveState", out var active) || active is not ("inactive" or "failed")
        || !values.TryGetValue("SubState", out var sub) || sub is not ("dead" or "failed")
        || !values.TryGetValue("MainPID", out var pid) || pid != "0"
        || !values.TryGetValue("Job", out var job) || job.Length != 0
        || !values.TryGetValue("ControlGroup", out var group) || !values.TryGetValue("TasksCurrent", out var tasks)
        || group.Length > 0 && tasks != "0") throw new IOException("UI_WRITER_NOT_IDLE wait for no active/transitional writer or pending service job; timer remains stopped");
}
string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Dictionary<string, string> Hashes(string path) => Directory.Exists(path) ? Directory.GetFiles(path, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(path, f), f => Sha(File.ReadAllBytes(f)), StringComparer.Ordinal) : new(StringComparer.Ordinal);
string TreeHash(Dictionary<string, string> hashes) => Sha(Encoding.UTF8.GetBytes(string.Join("\n", hashes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value))));
void Copy(string from, string to)
{
    if (!Directory.Exists(from)) return;
    foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
}
void Save(JsonObject state) { File.WriteAllText(recordPath + ".tmp", state.ToJsonString()); File.Move(recordPath + ".tmp", recordPath, true); }
async Task RestoreActive(JsonObject state)
{
    var enabled = await Systemctl("is-enabled", timer);
    if (enabled.Text != state["timerEnabled"]!.GetValue<string>()) throw new IOException("timer enabled state changed externally; left stopped, do not override user preference");
    // This script never enables/disables a timer. Only recover the saved
    // active bit; a previously inactive timer remains stopped.
    if (state["timerActive"]!.GetValue<bool>()) await Require("start", timer);
    else await Require("stop", timer);
}
if ((await Systemctl("show", timer, "--property=LoadState", "--value")).Text != "loaded") throw new IOException("ENV_NOT_READY expected existing user timer");
if (mode is "restore" or "resume")
{
    var state = JsonNode.Parse(File.ReadAllBytes(recordPath))!.AsObject();
    if (Path.GetRelativePath(scripts, units) != state["unitDir"]!.GetValue<string>()) throw new IOException("unit directory differs from backup");
    if (mode == "resume")
    {
        if (File.Exists(Path.Combine(work, "ROLLED_BACK")) || !File.Exists(Path.Combine(work, "published.json"))) throw new IOException("no successful new release to resume");
        var published = JsonNode.Parse(File.ReadAllBytes(Path.Combine(work, "published.json")))!;
        foreach (var field in new[] { "uiCommit", "uiHash", "review" })
            if (!JsonNode.DeepEquals(published[field], state[field])) throw new IOException("successful publication does not match installed fixed hook");
        await RequireIdleWriter();
        await RestoreActive(state); return 0;
    }
    await Require("stop", timer);
    await RequireIdleWriter();
    var original = Hashes(Path.Combine(backup, "dropins"));
    if (TreeHash(original) != state["originalDropinsHash"]!.GetValue<string>()) throw new IOException("backup changed");
    var expected = new Dictionary<string, string>(original) { [ownName] = state["hookHash"]!.GetValue<string>() };
    var currentHash = TreeHash(Hashes(dropins));
    if (currentHash != TreeHash(expected) && currentHash != TreeHash(original)) throw new IOException("drop-ins changed externally; refusing to delete user edits");
    if (Directory.Exists(dropins)) Directory.Delete(dropins, true);
    Copy(Path.Combine(backup, "dropins"), dropins);
    if (state["dropinsExisted"]!.GetValue<bool>()) Directory.CreateDirectory(dropins);
    if (TreeHash(Hashes(dropins)) != TreeHash(original)) throw new IOException("restored drop-ins differ");
    await Require("daemon-reload"); await Require("cat", service); await RestoreActive(state);
    return 0;
}
if (Directory.Exists(backup) || File.Exists(Path.Combine(dropins, ownName))) throw new IOException("use a fresh release work directory; preserve previous hook evidence");
var prepared = JsonNode.Parse(File.ReadAllBytes(Path.Combine(work, "prepared.json")))!.AsObject();
var ui = Option("--ui-commit", ""); var hash = Option("--dist-hash", ""); var review = Option("--review", "");
if (ui.Length != 40 || review.Length != 40 || hash.Length != 64 || !ui.All(char.IsAsciiHexDigit) || !review.All(char.IsAsciiHexDigit) || !hash.All(char.IsAsciiHexDigit)
    || prepared["uiCommit"]!.GetValue<string>() != ui || prepared["uiHash"]!.GetValue<string>() != hash) throw new IOException("fixed accepted UI identity must match prepared artifact");
var tool = Path.Combine(scripts, "PublishReviewedWebProgressUi.cs");
var homeRelative = Path.GetRelativePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), tool).Replace('\\', '/');
if (homeRelative.StartsWith("..", StringComparison.Ordinal) || homeRelative.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-'))) throw new IOException("tool path must be safe and under current home");
var command = new StringBuilder("[Service]\nExecStartPost=\nExecStartPost=dotnet run --file %h/").Append(homeRelative).Append(" -- --mode publish --execute");
foreach (var name in new[] { "--dist", "--data", "--baseline-data", "--work", "--portalctl", "--previous-bundle" }) command.Append(' ').Append(name).Append(' ').Append(Relative(name));
command.Append(" --ui-commit ").Append(ui).Append(" --dist-hash ").Append(hash).Append(" --review ").Append(review).Append('\n');
var timerActive = (await Systemctl("is-active", timer)).Exit == 0;
var timerEnabled = (await Systemctl("is-enabled", timer)).Text;
var effective = await Systemctl("cat", service); if (effective.Exit != 0) throw new IOException("expected existing refresh unit");
Directory.CreateDirectory(backup); Copy(dropins, Path.Combine(backup, "dropins"));
File.WriteAllText(Path.Combine(backup, "effective-before.txt"), effective.Text);
if (TreeHash(Hashes(dropins)) != TreeHash(Hashes(Path.Combine(backup, "dropins")))) throw new IOException("drop-ins changed during backup; no hook installed");
var record = new JsonObject { ["schemaVersion"] = 1, ["unitDir"] = Path.GetRelativePath(scripts, units), ["uiCommit"] = ui, ["uiHash"] = hash, ["review"] = review, ["timerActive"] = timerActive, ["timerEnabled"] = timerEnabled, ["dropinsExisted"] = Directory.Exists(dropins), ["originalDropinsHash"] = TreeHash(Hashes(dropins)), ["hookHash"] = Sha(Encoding.UTF8.GetBytes(command.ToString())) };
Save(record); await Require("stop", timer);
await RequireIdleWriter();
if (TreeHash(Hashes(dropins)) != record["originalDropinsHash"]!.GetValue<string>()) throw new IOException("drop-ins changed after backup; no hook installed");
Directory.CreateDirectory(dropins); File.WriteAllText(Path.Combine(dropins, ownName), command.ToString());
await Require("daemon-reload");
var after = await Systemctl("show", service, "--property=ExecStartPost", "--value");
File.WriteAllText(Path.Combine(backup, "effective-post-after.txt"), after.Text);
if (after.Exit != 0 || Regex.Matches(after.Text, @"\bpath=").Count != 1 || !after.Text.Contains("PublishReviewedWebProgressUi.cs", StringComparison.Ordinal)) throw new IOException("effective post chain is not exactly the fixed UI hook; timer left stopped for restore");
Console.WriteLine("UI_HOOK_INSTALLED_TIMER_STOPPED run one explicit publish and browser acceptance, then --mode resume; rollback bundle first then --mode restore");
return 0;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
