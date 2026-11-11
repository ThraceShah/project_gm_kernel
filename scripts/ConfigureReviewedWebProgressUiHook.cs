#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Operational companion to PublishReviewedWebProgressUi. Default is help;
// install/restore/resume are intentionally NOT run by integration verification.
var mode = Option("--mode", "plan");
if (mode == "plan" || args.Contains("--help"))
{
    Console.WriteLine("ConfigureReviewedWebProgressUiHook --mode install|restore|resume --execute --work <relative UI release work> --unit-dir <relative user units> [install: same fixed arguments as PublishReviewedWebProgressUi; owned upgrade also --release-sha256 <pin> --previous-release <relative> --previous-release-sha256 <pin> --previous-state-sha256 <pin>]");
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
    Directory.CreateDirectory(to);
    foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
    foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories)) { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
}
void ValidState(JsonObject state)
{
    if (TreeHash(Hashes(Path.Combine(backup, "dropins"))) != state["originalDropinsHash"]!.GetValue<string>()) throw new IOException("UI_HOOK_BACKUP_INVALID");
    var original = Hashes(Path.Combine(backup, "dropins"));
    var installed = new Dictionary<string, string>(original) { [ownName] = state["hookHash"]!.GetValue<string>() };
    var current = TreeHash(Hashes(dropins));
    if (current != TreeHash(installed) && current != TreeHash(original)) throw new IOException("drop-ins changed externally; refusing to delete user edits");
}
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
    ValidState(state);
    if (Path.GetRelativePath(scripts, units) != state["unitDir"]!.GetValue<string>()) throw new IOException("unit directory differs from backup");
    if (mode == "resume")
    {
        if (File.Exists(Path.Combine(work, "ROLLED_BACK")) || !File.Exists(Path.Combine(work, "published.json"))) throw new IOException("no successful new release to resume");
        var published = JsonNode.Parse(File.ReadAllBytes(Path.Combine(work, "published.json")))!;
        if (Sha(File.ReadAllBytes(Path.Combine(dropins, ownName))) != state["hookHash"]!.GetValue<string>()) throw new IOException("installed hook differs from successful publication");
        foreach (var field in new[] { "uiCommit", "uiHash", "review" })
            if (!JsonNode.DeepEquals(published[field], state[field])) throw new IOException("successful publication does not match installed fixed hook");
        var currentPrepared = JsonNode.Parse(File.ReadAllBytes(Path.Combine(work, "prepared.json")))!;
        if (!JsonNode.DeepEquals(published["fingerprint"], currentPrepared["fingerprint"])) throw new IOException("successful publication fingerprint differs from prepared bundle");
        if (!ReviewedWebProgressRelease.ValidBackup(Path.Combine(work, "previous-snapshot")) && state["ownedUpgrade"]?.GetValue<bool>() == true) throw new IOException("UI_BACKUP_INVALID");
        var currentPost = await Systemctl("show", service, "--property=ExecStartPost", "--value");
        if (currentPost.Exit != 0) throw new IOException("effective post unavailable");
        ReviewedWebProgressRelease.Effective(currentPost.Text, ReviewedWebProgressRelease.HookCommand(File.ReadAllText(Path.Combine(dropins, ownName))));
        await RequireIdleWriter();
        await RestoreActive(state); return 0;
    }
    await Require("stop", timer);
    await RequireIdleWriter();
    if (state["ownedUpgrade"]?.GetValue<bool>() == true)
    {
        var snapshot = Path.Combine(work, "previous-snapshot");
        if (!File.Exists(Path.Combine(work, "ROLLED_BACK")) || !ReviewedWebProgressRelease.ValidBackup(snapshot, served: true)
            || ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.Combine(snapshot, "bundle"))) != state["previousBundleHash"]?.GetValue<string>()) throw new IOException("UI_BACKUP_INVALID rollback exact current-before-upgrade bundle before restoring hook");
        await ReviewedWebProgressRelease.VerifyServed(Path.Combine(snapshot, "bundle"));
    }
    var original = Hashes(Path.Combine(backup, "dropins"));
    if (TreeHash(original) != state["originalDropinsHash"]!.GetValue<string>()) throw new IOException("backup changed");
    var expected = new Dictionary<string, string>(original) { [ownName] = state["hookHash"]!.GetValue<string>() };
    var currentHash = TreeHash(Hashes(dropins));
    if (currentHash != TreeHash(expected) && currentHash != TreeHash(original)) throw new IOException("drop-ins changed externally; refusing to delete user edits");
    if (Directory.Exists(dropins)) Directory.Move(dropins, Path.Combine(backup, "replaced-" + Guid.NewGuid().ToString("N")));
    Copy(Path.Combine(backup, "dropins"), dropins);
    if (state["dropinsExisted"]!.GetValue<bool>()) Directory.CreateDirectory(dropins);
    if (TreeHash(Hashes(dropins)) != TreeHash(original)) throw new IOException("restored drop-ins differ");
    await Require("daemon-reload"); await Require("cat", service);
    if (original.ContainsKey(ownName))
    {
        var restoredPost = await Systemctl("show", service, "--property=ExecStartPost", "--value");
        if (restoredPost.Exit != 0) throw new IOException("effective post unavailable after restore");
        ReviewedWebProgressRelease.Effective(restoredPost.Text, ReviewedWebProgressRelease.HookCommand(File.ReadAllText(Path.Combine(dropins, ownName))));
    }
    await RestoreActive(state);
    return 0;
}
if (Directory.Exists(backup)) throw new IOException("use a fresh release work directory; preserve previous hook evidence");
var ownedUpgrade = File.Exists(Path.Combine(dropins, ownName));
var prepared = JsonNode.Parse(File.ReadAllBytes(Path.Combine(work, "prepared.json")))!.AsObject();
var ui = Option("--ui-commit", ""); var hash = Option("--dist-hash", ""); var review = Option("--review", "");
if (ui.Length != 40 || review.Length != 40 || hash.Length != 64 || !ui.All(char.IsAsciiHexDigit) || !review.All(char.IsAsciiHexDigit) || !hash.All(char.IsAsciiHexDigit)
    || prepared["uiCommit"]!.GetValue<string>() != ui || prepared["uiHash"]!.GetValue<string>() != hash
    || prepared["review"]?.GetValue<string>() != review) throw new IOException("fixed accepted UI identity must match prepared artifact");
var tool = Path.Combine(scripts, "PublishReviewedWebProgressUi.cs");
var homeRelative = Path.GetRelativePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), tool).Replace('\\', '/');
if (homeRelative.StartsWith("..", StringComparison.Ordinal) || homeRelative.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-'))) throw new IOException("tool path must be safe and under current home");
var command = new StringBuilder("[Service]\nExecStartPost=\nExecStartPost=dotnet run --file %h/").Append(homeRelative).Append(" -- --mode publish --execute");
foreach (var name in new[] { "--dist", "--data", "--baseline-data", "--work", "--portalctl", "--previous-bundle" }) command.Append(' ').Append(name).Append(' ').Append(Relative(name));
command.Append(" --ui-commit ").Append(ui).Append(" --dist-hash ").Append(hash).Append(" --review ").Append(review).Append('\n');
string? oldCommand = null;
if (ownedUpgrade)
{
    var prior = Resolve("--previous-release"); Safe(prior);
    if (WebProgressSafePath.IsWithin(work, prior) || WebProgressSafePath.IsWithin(prior, work)) throw new IOException("UI_HOOK_PATH_OVERLAP");
    var previousRelease = ReviewedWebProgressRelease.Frozen(prior, Option("--previous-release-sha256", ""));
    var oldScripts = Path.Combine(prior, "tools", "scripts");
    var oldState = ReviewedWebProgressRelease.Pinned(Path.Combine(prior, "work", "hook-backup", "state.json"), Option("--previous-state-sha256", ""));
    var oldPublished = JsonNode.Parse(File.ReadAllBytes(Path.Combine(prior, "work", "published.json")))!;
    if (File.Exists(Path.Combine(prior, "work", "ROLLED_BACK")) || Path.GetFullPath(oldState["unitDir"]!.GetValue<string>(), oldScripts) != units) throw new IOException("UI_PREVIOUS_RELEASE_INVALID");
    foreach (var field in new[] { "uiCommit", "uiHash", "review" })
        if (!JsonNode.DeepEquals(oldState[field], previousRelease[field]) || !JsonNode.DeepEquals(oldPublished[field], previousRelease[field])) throw new IOException("UI_PREVIOUS_RELEASE_IDENTITY_MISMATCH");
    var priorDropins = Path.Combine(prior, "work", "hook-backup", "dropins");
    if (TreeHash(Hashes(priorDropins)) != oldState["originalDropinsHash"]!.GetValue<string>()) throw new IOException("UI_PREVIOUS_HOOK_BACKUP_INVALID");
    var expected = Hashes(priorDropins); expected[ownName] = oldState["hookHash"]!.GetValue<string>();
    if (TreeHash(Hashes(dropins)) != TreeHash(expected)) throw new IOException("drop-ins changed externally; unknown owned hook refused");
    oldCommand = ReviewedWebProgressRelease.HookCommand(File.ReadAllText(Path.Combine(dropins, ownName)));
    if (!oldCommand.StartsWith("dotnet run --file " + Path.Combine(oldScripts, "PublishReviewedWebProgressUi.cs") + " -- --mode publish --execute ", StringComparison.Ordinal)
        || !oldCommand.EndsWith(" --ui-commit " + oldState["uiCommit"]!.GetValue<string>() + " --dist-hash " + oldState["uiHash"]!.GetValue<string>() + " --review " + oldState["review"]!.GetValue<string>(), StringComparison.Ordinal)) throw new IOException("UI_PREVIOUS_TOOL_IDENTITY_MISMATCH");
    var oldArguments = oldCommand.Split(' ');
    foreach (var name in new[] { "--data", "--portalctl" })
    {
        var at = Array.IndexOf(oldArguments, name);
        if (at < 0 || Path.GetFullPath(oldArguments[at + 1], oldScripts) != Resolve(name)) throw new IOException("UI_PRODUCER_OR_PORTAL_CHANGED use separate host cutover review");
    }
    var release = Path.GetFullPath("../..", scripts);
    var newRelease = ReviewedWebProgressRelease.Frozen(release, Option("--release-sha256", ""));
    foreach (var field in new[] { "uiCommit", "uiHash", "review" })
        if (!JsonNode.DeepEquals(newRelease[field], prepared[field])) throw new IOException("UI_NEW_RELEASE_IDENTITY_MISMATCH");
    if (Resolve("--dist") != Path.Combine(release, "ui") || work != Path.Combine(release, "work") || Resolve("--previous-bundle") != Path.Combine(release, "previous-static")
        || Resolve("--baseline-data") != Path.Combine(release, "previous-static", "data")) throw new IOException("UI_FIXED_RELEASE_PATH_REQUIRED");
    var snapshot = Path.Combine(work, "previous-snapshot");
    if (!ReviewedWebProgressRelease.ValidBackup(snapshot, served: true)) throw new IOException("UI_BACKUP_INVALID verified current served snapshot required before owned upgrade");
    var saved = Path.Combine(snapshot, "bundle");
    if (ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(saved, true)) != previousRelease["uiHash"]!.GetValue<string>()
        || ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(saved)) != newRelease["oldBundleHash"]!.GetValue<string>()) throw new IOException("UI_PREVIOUS_BUNDLE_IDENTITY_MISMATCH");
}
var timerStatus = await Systemctl("is-active", timer);
if (timerStatus.Text is not ("active" or "inactive") || timerStatus.Exit != (timerStatus.Text == "active" ? 0 : 3)) throw new IOException("UI_TIMER_NOT_STABLE");
var timerActive = timerStatus.Text == "active";
var timerEnabled = (await Systemctl("is-enabled", timer)).Text;
if (timerEnabled is not ("enabled" or "disabled")) throw new IOException("UI_TIMER_PREFERENCE_UNKNOWN");
var effective = await Systemctl("cat", service); if (effective.Exit != 0) throw new IOException("expected existing refresh unit");
var postBefore = await Systemctl("show", service, "--property=ExecStartPost", "--value");
if (postBefore.Exit != 0) throw new IOException("effective post unavailable");
if (oldCommand is not null) ReviewedWebProgressRelease.Effective(postBefore.Text, oldCommand);
var staging = backup + ".staging-" + Guid.NewGuid().ToString("N");
Directory.CreateDirectory(staging); Copy(dropins, Path.Combine(staging, "dropins"));
File.WriteAllText(Path.Combine(staging, "effective-before.txt"), effective.Text);
File.WriteAllText(Path.Combine(staging, "effective-post-before.txt"), postBefore.Text);
if (TreeHash(Hashes(dropins)) != TreeHash(Hashes(Path.Combine(staging, "dropins")))) throw new IOException("drop-ins changed during backup; staging retained, no hook installed");
var record = new JsonObject { ["schemaVersion"] = 1, ["unitDir"] = Path.GetRelativePath(scripts, units), ["uiCommit"] = ui, ["uiHash"] = hash, ["review"] = review, ["ownedUpgrade"] = ownedUpgrade, ["timerActive"] = timerActive, ["timerEnabled"] = timerEnabled, ["dropinsExisted"] = Directory.Exists(dropins), ["originalDropinsHash"] = TreeHash(Hashes(dropins)), ["hookHash"] = Sha(Encoding.UTF8.GetBytes(command.ToString())) };
if (ownedUpgrade) record["previousBundleHash"] = ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.Combine(work, "previous-snapshot", "bundle")));
File.WriteAllText(Path.Combine(staging, "state.json"), record.ToJsonString());
Directory.Move(staging, backup);
await Require("stop", timer);
await RequireIdleWriter();
if (TreeHash(Hashes(dropins)) != record["originalDropinsHash"]!.GetValue<string>()) throw new IOException("drop-ins changed after backup; no hook installed");
if (oldCommand is not null)
{
    await ReviewedWebProgressRelease.VerifyServed(Path.Combine(work, "previous-snapshot", "bundle"));
    var currentPost = await Systemctl("show", service, "--property=ExecStartPost", "--value");
    if (currentPost.Exit != 0) throw new IOException("effective post unavailable");
    ReviewedWebProgressRelease.Effective(currentPost.Text, oldCommand);
}
Directory.CreateDirectory(dropins);
var hookStaging = Path.Combine(dropins, ".reviewed-ui-" + Guid.NewGuid().ToString("N") + ".tmp");
File.WriteAllText(hookStaging, command.ToString());
File.Move(hookStaging, Path.Combine(dropins, ownName), true);
await Require("daemon-reload");
var after = await Systemctl("show", service, "--property=ExecStartPost", "--value");
File.WriteAllText(Path.Combine(backup, "effective-post-after.txt"), after.Text);
if (after.Exit != 0) throw new IOException("effective post unavailable; timer left stopped for restore");
ReviewedWebProgressRelease.Effective(after.Text, ReviewedWebProgressRelease.HookCommand(command.ToString()));
Console.WriteLine("UI_HOOK_INSTALLED_TIMER_STOPPED run one explicit publish and browser acceptance, then --mode resume; rollback bundle first then --mode restore");
return 0;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
