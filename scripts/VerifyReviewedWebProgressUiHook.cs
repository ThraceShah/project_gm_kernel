#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

// A finite PATH stub: only this harness's child processes see fake systemctl.
// No real user service/timer/portal is touched. Stub implementation is BCL C#.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts);
var work = Path.Combine(root, "temp_docs", "publishing-integration", "ui-hook-" + Guid.NewGuid().ToString("N"));
var project = Path.Combine(work, "stub"); Directory.CreateDirectory(project);
File.WriteAllText(Path.Combine(project, "Stub.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>");
File.WriteAllText(Path.Combine(project, "Program.cs"), """
using System.Text.Json.Nodes;
var file=Environment.GetEnvironmentVariable("GM_UI_HOOK_STUB")??throw new Exception("stub env required");
if(args.Length>0&&args[0]=="static"){
 var s=JsonNode.Parse(File.ReadAllText(file))!;
 if(s["portalExit"]?.GetValue<int>() is int exit&&exit!=0)return exit;
 File.WriteAllText(Environment.GetEnvironmentVariable("GM_UI_SERVED")!,args[Array.IndexOf(args,"--dir")+1]);return 0;
}
var state=JsonNode.Parse(File.ReadAllText(file))!;
var a=args.Where(s=>s!="--user").ToArray();
File.AppendAllText(file+".commands",string.Join(" ",a)+"\n");
var timer=a.Length>1&&a[1].EndsWith(".timer",StringComparison.Ordinal);
switch(a[0]){
case "show":
 if(a.Contains("--property=LoadState"))Console.WriteLine("loaded");
 else if(a.Contains("--property=ExecStartPost")){
  var hook=Path.Combine(state["units"]!.GetValue<string>(),"gm-kernel-progress-refresh.service.d","zz-reviewed-ui.conf");
  var command=File.Exists(hook)?File.ReadAllLines(hook).Single(l=>l.StartsWith("ExecStartPost=dotnet "))["ExecStartPost=".Length..].Replace("%h/",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"/"):"old-static-chain";
  Console.WriteLine(state["postOverride"]?.GetValue<string>()??("{ path=dotnet ; argv[]="+command+" ; ignore_errors=no ; }"));
 }
 else{Console.WriteLine("ActiveState="+state["writerActive"]);Console.WriteLine("SubState="+state["writerSub"]);Console.WriteLine("MainPID="+state["pid"]);Console.WriteLine("Job="+state["job"]);Console.WriteLine("TasksCurrent="+state["tasks"]);Console.WriteLine("ControlGroup="+state["group"]);}return 0;
case "cat":
 if(state["editOnCat"]?.GetValue<bool>()==true){File.AppendAllText(Path.Combine(state["units"]!.GetValue<string>(),"gm-kernel-progress-refresh.service.d","zz-reviewed-ui.conf"),"# late external edit\n");state.AsObject().Remove("editOnCat");File.WriteAllText(file,state.ToJsonString());}
 Console.WriteLine("[Service]\nExecStart=old-producer\nExecStartPost=old-prepare\nExecStartPost=old-publish");return 0;
case "is-enabled":Console.WriteLine(state["enabled"]!.GetValue<string>());return state["enabled"]!.GetValue<string>()=="enabled"?0:1;
case "is-active":var active=timer&&state["active"]!.GetValue<bool>();Console.WriteLine(active?"active":"inactive");return active?0:3;
case "stop":case "start":if(!timer)return 9;state["active"]=a[0]=="start";File.WriteAllText(file,state.ToJsonString());return 0;
case "daemon-reload":return 0;
default:return 99;
}
""");
var checks = 0;
void Check(bool ok, string label) { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) throw new IOException(label); checks++; }
async Task<(int Exit, string Text)> Run(string label, string[] arguments, string? stateFile = null)
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    if (stateFile is not null) { info.Environment["GM_UI_HOOK_STUB"] = stateFile; info.Environment["GM_UI_SERVED"] = Path.Combine(work, "served.txt"); info.Environment["PATH"] = Path.Combine(project, "bin", "Debug", "net10.0") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"); }
    using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
    var text = await output + "\n" + await error; File.WriteAllText(Path.Combine(work, label + ".log"), "exit=" + process.ExitCode + "\n" + text);
    Console.WriteLine("CLI " + label + " exit=" + process.ExitCode); return (process.ExitCode, text);
}
Check((await Run("stub-build", ["build", Path.Combine(project, "Stub.csproj"), "--nologo"])).Exit == 0, "managed BCL systemctl stub compiled");
var bin = Path.Combine(project, "bin", "Debug", "net10.0");
File.Copy(Path.Combine(bin, "Stub"), Path.Combine(bin, "systemctl"));
File.SetUnixFileMode(Path.Combine(bin, "systemctl"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
var ui = new string('a', 40); var hash = new string('b', 64); var review = new string('c', 40);
string Relative(string path) => Path.GetRelativePath(scripts, path);
string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
void Save(string path, JsonNode node) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, node.ToJsonString()); }
JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
(string Release, string Units, string State, string Old) Setup(string name, bool active, string enabled)
{
    var release = Path.Combine(work, name, "release"); var units = Path.Combine(work, name, "units");
    var old = Path.Combine(units, "gm-kernel-progress-refresh.service.d", "10-old.conf"); Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.WriteAllText(old, "[Service]\nExecStartPost=old-static-chain\n");
    Directory.CreateDirectory(Path.Combine(units, "default.target.wants"));
    File.CreateSymbolicLink(Path.Combine(units, "default.target.wants", "unrelated.service"), old);
    Save(Path.Combine(release, "prepared.json"), new JsonObject { ["uiCommit"] = ui, ["uiHash"] = hash, ["review"] = review });
    var state = Path.Combine(work, name, "stub.json");
    Save(state, new JsonObject { ["active"] = active, ["enabled"] = enabled, ["units"] = units, ["writerActive"] = "inactive", ["writerSub"] = "dead", ["pid"] = "0", ["job"] = "", ["tasks"] = "[not set]", ["group"] = "" });
    return (release, units, state, old);
}
Task<(int Exit, string Text)> Configure(string label, string mode, string release, string units, string state) => Run(label,
    ["run", "--file", "scripts/ConfigureReviewedWebProgressUiHook.cs", "--", "--mode", mode, "--execute", "--work", Relative(release), "--unit-dir", Relative(units), "--dist", "../fixture-ui", "--data", "../fixture-producer-data", "--baseline-data", "../fixture-baseline-data", "--portalctl", "../fixture-portalctl", "--previous-bundle", "../fixture-previous", "--ui-commit", ui, "--dist-hash", hash, "--review", review], state);
var transitionOnly = args.Contains("--transition-only");
if (!transitionOnly)
{
foreach (var (name, active, enabled) in new[] { ("inactive-enabled", false, "enabled"), ("inactive-disabled", false, "disabled"), ("active-enabled", true, "enabled") })
{
    var f = Setup(name, active, enabled); var original = Sha(f.Old);
    Check((await Configure(name + "-install", "install", f.Release, f.Units, f.State)).Exit == 0, name + " installs fixed hook");
    Check(!Read(f.State)["active"]!.GetValue<bool>() && Read(f.State)["enabled"]!.GetValue<string>() == enabled, "install leaves timer stopped and enabled preference unchanged");
    Check(Sha(Path.Combine(f.Release, "hook-backup", "dropins", "10-old.conf")) == original, "backup exact original drop-in bytes");
    Check((await Configure(name + "-premature-resume", "resume", f.Release, f.Units, f.State)).Exit != 0, "resume requires publication identity");
    Save(Path.Combine(f.Release, "published.json"), new JsonObject { ["uiCommit"] = ui, ["uiHash"] = hash, ["review"] = review });
    Check((await Configure(name + "-resume", "resume", f.Release, f.Units, f.State)).Exit == 0 && Read(f.State)["active"]!.GetValue<bool>() == active, "resume restores only original active bit");
    Check((await Configure(name + "-restore", "restore", f.Release, f.Units, f.State)).Exit == 0 && Sha(f.Old) == original && Directory.GetFiles(Path.GetDirectoryName(f.Old)!).Length == 1, "restore exact old chain and removes new hook");
    Check(Read(f.State)["active"]!.GetValue<bool>() == active && Read(f.State)["enabled"]!.GetValue<string>() == enabled, "restore preserves original active/enabled state");
    Check(!File.ReadAllText(f.State + ".commands").Split('\n').Any(l => l.StartsWith("enable ", StringComparison.Ordinal) || l.StartsWith("disable ", StringComparison.Ordinal)), "never enable or disable a timer");
}
foreach (var active in new[] { "activating", "deactivating", "reloading" })
{
    var f = Setup(active, false, "enabled"); var state = Read(f.State); state["writerActive"] = active; state["writerSub"] = "start-post"; state["tasks"] = "1"; state["group"] = "/stub"; state["job"] = "42"; Save(f.State, state);
    var result = await Configure(active + "-refused", "install", f.Release, f.Units, f.State);
    Check(result.Exit != 0 && result.Text.Contains("UI_WRITER_NOT_IDLE") && !File.Exists(Path.Combine(Path.GetDirectoryName(f.Old)!, "zz-reviewed-ui.conf")), active + " old post writer refused even with MainPID0");
}
{
    var f = Setup("queued", false, "disabled"); var state = Read(f.State); state["job"] = "123"; Save(f.State, state);
    var result = await Configure("queued-refused", "install", f.Release, f.Units, f.State);
    Check(result.Exit != 0 && result.Text.Contains("UI_WRITER_NOT_IDLE"), "pending service job refused while nominally inactive");
}
{
    var f = Setup("external-edit", false, "disabled"); Check((await Configure("external-install", "install", f.Release, f.Units, f.State)).Exit == 0, "external-edit fixture installed");
    File.AppendAllText(f.Old, "# USER_EDIT\n"); var edited = Sha(f.Old);
    var result = await Configure("external-refused", "restore", f.Release, f.Units, f.State);
    Check(result.Exit != 0 && result.Text.Contains("changed externally") && Sha(f.Old) == edited, "external drop-in edits retained on restore refusal");
}
Check(checks == 31, "all original 31 hook checks retained");
}

// Only temporary copies replace the fixed HTTPS origin with this finite BCL
// loopback server. Production tools have no endpoint override or fixture mode.
var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
using var listener = new HttpListener(); listener.Prefixes.Add("http://127.0.0.1:" + port + "/"); listener.Start();
using var cancellation = new CancellationTokenSource();
string? mutateHook = null;
var server = Task.Run(async () =>
{
    while (!cancellation.IsCancellationRequested)
    {
        HttpListenerContext context;
        try { context = await listener.GetContextAsync().WaitAsync(cancellation.Token); }
        catch (OperationCanceledException) { break; }
        var served = File.ReadAllText(Path.Combine(work, "served.txt"));
        var edit = Interlocked.Exchange(ref mutateHook, null);
        if (edit is not null) File.AppendAllText(edit, "# late external edit\n");
        var relative = context.Request.Url!.AbsolutePath.TrimStart('/');
        var path = Path.GetFullPath(relative, served);
        if (!WebProgressSafePath.IsWithin(served, path) || !File.Exists(path)) context.Response.StatusCode = 404;
        else { var bytes = File.ReadAllBytes(path); context.Response.ContentLength64 = bytes.Length; await context.Response.OutputStream.WriteAsync(bytes); }
        context.Response.Close();
    }
});
File.Copy(Path.Combine(bin, "Stub"), Path.Combine(bin, "portalctl"));
File.SetUnixFileMode(Path.Combine(bin, "portalctl"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
var sourceNames = new[] { "Directory.Build.props", "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs", "ReviewedWebProgressRelease.cs", "WebProgressDataValidation.cs", "WebProgressPublishing.cs", "WebProgressMetadata.cs" };
var legacySources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
foreach (var name in sourceNames.Where(n => n != "ReviewedWebProgressRelease.cs"))
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in new[] { "show", "35498ac5e72eda303883120d8b58cbb6c15fdb33:scripts/" + name }) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!; using var bytes = new MemoryStream();
    var output = process.StandardOutput.BaseStream.CopyToAsync(bytes); var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync(); await output;
    if (process.ExitCode != 0) throw new IOException(await error);
    legacySources.Add(name, bytes.ToArray());
}
void Put(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
void CopyTree(string from, string to)
{
    foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
    { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
}
void ReadOnly(string path)
{
    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).Append(path)) File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
}
string Freeze(string release, string commit, string acceptance, bool legacy)
{
    var files = new JsonObject();
    foreach (var dir in new[] { "tools", "ui", "previous-static" })
        foreach (var (path, sha) in ReviewedWebProgressRelease.Hashes(Path.Combine(release, dir))) files[dir + "/" + path] = sha;
    Save(Path.Combine(release, "release.json"), new JsonObject { ["schemaVersion"] = 1, ["toolsCommit"] = legacy ? "35498ac5e72eda303883120d8b58cbb6c15fdb33" : new string('d', 40), ["uiCommit"] = commit, ["review"] = acceptance, ["uiHash"] = ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.Combine(release, "ui"))), ["oldBundleHash"] = ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.Combine(release, "previous-static"))), ["files"] = files });
    foreach (var dir in new[] { "tools", "ui", "previous-static" }) ReadOnly(Path.Combine(release, dir));
    return Sha(Path.Combine(release, "release.json"));
}
void Bundle(string path, string label)
{
    Put(Path.Combine(path, "index.html"), "<html>" + label + "<script src=\"/apps/gm-kernel-progress/assets/fixture.js\"></script></html>");
    Put(Path.Combine(path, "assets", "fixture.js"), label);
    Put(Path.Combine(path, "data", "index.json"), "{\"schemaVersion\":1,\"generatedAt\":\"fixture\",\"latestRunId\":\"r1\",\"runs\":[{\"id\":\"r1\",\"commit\":\"old\",\"generatedAt\":\"fixture\",\"label\":\"fixture\",\"manifest\":\"runs/r1/manifest.json\"}]}");
    Put(Path.Combine(path, "data", "runs", "r1", "manifest.json"), "{\"schemaVersion\":1,\"run\":{\"id\":\"r1\",\"commit\":\"old\",\"generatedAt\":\"fixture\",\"pkVersion\":null,\"host\":{},\"configuration\":{},\"scope\":\"BCL fixture\",\"provenance\":{}},\"cases\":[]}");
}
(string Release, string Work, string Units, string State, string Prior, string OldHook, string[] Arguments) Upgrade(string name, bool active, string enabled)
{
    var f = Setup("upgrade-" + name, active, enabled);
    File.Delete(Path.Combine(f.Release, "prepared.json"));
    var prior = Path.Combine(work, "upgrade-" + name, "legacy-release");
    var oldScripts = Path.Combine(prior, "tools", "scripts");
    foreach (var (file, bytes) in legacySources) { var target = Path.Combine(oldScripts, file); Directory.CreateDirectory(oldScripts); File.WriteAllBytes(target, bytes); }
    Bundle(Path.Combine(prior, "previous-static"), "PRE424");
    var current = Path.Combine(work, "upgrade-" + name, "served-ui424"); Bundle(current, "UI424");
    CopyTree(current, Path.Combine(prior, "ui")); Directory.Delete(Path.Combine(prior, "ui", "data"), true);
    var oldPin = Freeze(prior, "424e85d6aac32c2afe7cad12cfd04bea4f0037ab", "f1fdc36553e9132c88d78a933e503160950460f2", true);
    var oldRecord = Read(Path.Combine(prior, "release.json"));
    var producer = Path.Combine(work, "upgrade-" + name, "producer-data"); CopyTree(Path.Combine(current, "data"), producer);
    var oldBackup = Path.Combine(prior, "work", "hook-backup");
    CopyTree(Path.GetDirectoryName(f.Old)!, Path.Combine(oldBackup, "dropins"));
    var original = ReviewedWebProgressRelease.Hashes(Path.Combine(oldBackup, "dropins"));
    string DropHash(Dictionary<string, string> hashes) => ReviewedWebProgressRelease.Sha(Encoding.UTF8.GetBytes(string.Join("\n", hashes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value))));
    string OldRelative(string path) => Path.GetRelativePath(oldScripts, path);
    var oldTool = Path.Combine(oldScripts, "PublishReviewedWebProgressUi.cs");
    var oldCommand = "dotnet run --file %h/" + Path.GetRelativePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), oldTool) + " -- --mode publish --execute --dist ../../ui --data " + OldRelative(producer) + " --baseline-data ../../previous-static/data --work ../../work --portalctl " + OldRelative(Path.Combine(bin, "portalctl")) + " --previous-bundle ../../previous-static --ui-commit " + oldRecord["uiCommit"]!.GetValue<string>() + " --dist-hash " + oldRecord["uiHash"]!.GetValue<string>() + " --review " + oldRecord["review"]!.GetValue<string>();
    var oldHook = Path.Combine(Path.GetDirectoryName(f.Old)!, "zz-reviewed-ui.conf");
    Put(oldHook, "[Service]\nExecStartPost=\nExecStartPost=" + oldCommand + "\n");
    Save(Path.Combine(oldBackup, "state.json"), new JsonObject { ["schemaVersion"] = 1, ["unitDir"] = OldRelative(f.Units), ["uiCommit"] = oldRecord["uiCommit"]!.DeepClone(), ["uiHash"] = oldRecord["uiHash"]!.DeepClone(), ["review"] = oldRecord["review"]!.DeepClone(), ["timerActive"] = true, ["timerEnabled"] = "enabled", ["dropinsExisted"] = true, ["originalDropinsHash"] = DropHash(original), ["hookHash"] = Sha(oldHook) });
    Save(Path.Combine(prior, "work", "published.json"), new JsonObject { ["uiCommit"] = oldRecord["uiCommit"]!.DeepClone(), ["uiHash"] = oldRecord["uiHash"]!.DeepClone(), ["review"] = oldRecord["review"]!.DeepClone() });
    foreach (var file in sourceNames)
    {
        var text = File.ReadAllText(Path.Combine(scripts, file));
        if (file == "ReviewedWebProgressRelease.cs") text = text.Replace("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/", "http://127.0.0.1:" + port + "/", StringComparison.Ordinal);
        Put(Path.Combine(f.Release, "tools", "scripts", file), text);
    }
    Bundle(Path.Combine(f.Release, "ui"), "NEXT_ACCEPTED_FIXTURE"); Directory.Delete(Path.Combine(f.Release, "ui", "data"), true);
    CopyTree(current, Path.Combine(f.Release, "previous-static"));
    var newPin = Freeze(f.Release, ui, review, false);
    var newScripts = Path.Combine(f.Release, "tools", "scripts");
    string Rel(string path) => Path.GetRelativePath(newScripts, path);
    var releaseWork = Path.Combine(f.Release, "work");
    File.WriteAllText(Path.Combine(work, "served.txt"), current);
    return (f.Release, releaseWork, f.Units, f.State, prior, oldHook,
        ["--work", Rel(releaseWork), "--unit-dir", Rel(f.Units), "--dist", "../../ui", "--data", Rel(producer), "--baseline-data", "../../previous-static/data", "--portalctl", Rel(Path.Combine(bin, "portalctl")), "--previous-bundle", "../../previous-static", "--ui-commit", ui, "--dist-hash", Read(Path.Combine(f.Release, "release.json"))["uiHash"]!.GetValue<string>(), "--review", review, "--release-sha256", newPin, "--previous-release", Rel(prior), "--previous-release-sha256", oldPin, "--previous-state-sha256", Sha(Path.Combine(oldBackup, "state.json"))]);
}
Task<(int Exit, string Text)> UpgradeRun(string label, string script, string mode, string release, string state, string[] arguments, bool verify = false) =>
    Run(label, ["run", "--file", Path.Combine(release, "tools", "scripts", script), "--", "--mode", mode, "--execute", .. arguments, .. verify ? new[] { "--verify-served" } : Array.Empty<string>()], state);
JsonObject Evidence(string releaseWork, string dropins, string state)
{
    JsonObject Files(string path) => new(ReviewedWebProgressRelease.Hashes(path).Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value))));
    return new JsonObject { ["work"] = Files(releaseWork), ["directories"] = new JsonArray(Directory.GetDirectories(releaseWork, "*", SearchOption.AllDirectories).Order().Select(p => (JsonNode?)JsonValue.Create(Path.GetRelativePath(releaseWork, p))).ToArray()), ["dropins"] = Files(dropins), ["timerState"] = Sha(state), ["systemctl"] = File.Exists(state + ".commands") ? Sha(state + ".commands") : null };
}
foreach (var failure in new[] { "missing-owned", "only-release", "only-release-pin", "only-state-pin", "missing-release", "missing-release-pin", "missing-state-pin", "empty-state-pin", "no-state-value", "inline-state-pin", "wrong-state-pin" })
{
    var f = Upgrade("intent-" + failure, true, "enabled");
    Save(Path.Combine(f.Work, "prepared.json"), new JsonObject { ["uiCommit"] = ui, ["uiHash"] = Read(Path.Combine(f.Release, "release.json"))["uiHash"]!.DeepClone(), ["review"] = review });
    var previousNames = new[] { "--previous-release", "--previous-release-sha256", "--previous-state-sha256" };
    var arguments = f.Arguments.ToList();
    void Remove(string name) { var at = arguments.IndexOf(name); arguments.RemoveRange(at, 2); }
    switch (failure)
    {
        case "missing-owned": File.Delete(f.OldHook); break;
        case "only-release": foreach (var name in previousNames.Skip(1)) Remove(name); break;
        case "only-release-pin": Remove(previousNames[0]); Remove(previousNames[2]); break;
        case "only-state-pin": Remove(previousNames[0]); Remove(previousNames[1]); break;
        case "missing-release": Remove(previousNames[0]); break;
        case "missing-release-pin": Remove(previousNames[1]); break;
        case "missing-state-pin": Remove(previousNames[2]); break;
        case "empty-state-pin": arguments[arguments.IndexOf(previousNames[2]) + 1] = ""; break;
        case "no-state-value": arguments.RemoveAt(arguments.IndexOf(previousNames[2]) + 1); break;
        case "inline-state-pin": foreach (var name in previousNames) Remove(name); arguments.Add("--previous-state-sha256=" + new string('0', 64)); break;
        case "wrong-state-pin": arguments[arguments.IndexOf(previousNames[2]) + 1] = new string('0', 64); break;
    }
    var before = Evidence(f.Work, Path.GetDirectoryName(f.OldHook)!, f.State);
    var result = await UpgradeRun("intent-" + failure, "ConfigureReviewedWebProgressUiHook.cs", "install", f.Release, f.State, arguments.ToArray());
    Check(result.Exit != 0 && (failure != "missing-owned" || result.Text.Contains("UI_OWNED_TRANSITION_HOOK_MISSING")), failure + " explicit transition cannot fall back to first install");
    Check(JsonNode.DeepEquals(before, Evidence(f.Work, Path.GetDirectoryName(f.OldHook)!, f.State)), failure + " no timer/systemctl/dropins/work/backup/publication writes");
}
{
    var f = Setup("intent-real-first-install", true, "enabled");
    var original = Sha(f.Old);
    Check((await Configure("intent-real-first-install", "install", f.Release, f.Units, f.State)).Exit == 0 && !Read(Path.Combine(f.Release, "hook-backup", "state.json"))["ownedUpgrade"]!.GetValue<bool>(), "true first install without previous options stays compatible");
    Check((await Configure("intent-real-first-restore", "restore", f.Release, f.Units, f.State)).Exit == 0 && Sha(f.Old) == original && Read(f.State)["active"]!.GetValue<bool>(), "first install restore retains original bytes and active preference");
}
{
    var f = Setup("intent-unknown-hook", true, "enabled");
    Put(Path.Combine(Path.GetDirectoryName(f.Old)!, "zz-reviewed-ui.conf"), "UNKNOWN_OWNED_NAME\n");
    var before = Evidence(f.Release, Path.GetDirectoryName(f.Old)!, f.State);
    Check((await Configure("intent-unknown-hook", "install", f.Release, f.Units, f.State)).Exit != 0 && JsonNode.DeepEquals(before, Evidence(f.Release, Path.GetDirectoryName(f.Old)!, f.State)), "no previous options cannot overwrite unknown existing hook or write evidence");
}
{
    var f = Upgrade("intent-full-positive", true, "enabled");
    var original = Sha(f.OldHook);
    Check((await UpgradeRun("intent-full-positive-prepare", "PublishReviewedWebProgressUi.cs", "prepare", f.Release, f.State, f.Arguments)).Exit == 0, "complete owned transition prepares");
    Check((await UpgradeRun("intent-full-positive-backup", "PublishReviewedWebProgressUi.cs", "backup", f.Release, f.State, f.Arguments, true)).Exit == 0, "complete owned transition has verified bundle");
    Check((await UpgradeRun("intent-full-positive-install", "ConfigureReviewedWebProgressUiHook.cs", "install", f.Release, f.State, f.Arguments)).Exit == 0 && Read(Path.Combine(f.Work, "hook-backup", "state.json"))["ownedUpgrade"]!.GetValue<bool>() && !Read(f.State)["active"]!.GetValue<bool>(), "complete valid owned transition still installs as owned with timer stopped");
    Check((await UpgradeRun("intent-full-positive-rollback", "PublishReviewedWebProgressUi.cs", "rollback", f.Release, f.State, f.Arguments)).Exit == 0, "complete owned transition retains exact bundle rollback");
    Check((await UpgradeRun("intent-full-positive-restore", "ConfigureReviewedWebProgressUiHook.cs", "restore", f.Release, f.State, f.Arguments)).Exit == 0 && Sha(f.OldHook) == original && Read(f.State)["active"]!.GetValue<bool>(), "complete owned restore recovers original hook and active preference");
}
if (!transitionOnly)
{
foreach (var (name, active, enabled) in new[] { ("active", true, "enabled"), ("disabled", false, "disabled"), ("inactive", false, "enabled") })
{
    var f = Upgrade(name, active, enabled); var oldHook = Sha(f.OldHook);
    var originalDropins = ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.GetDirectoryName(f.OldHook)!));
    var oldStateSha = Sha(Path.Combine(f.Prior, "work", "hook-backup", "state.json"));
    var oldReleaseSha = Sha(Path.Combine(f.Prior, "release.json"));
    Check(Sha(Path.Combine(f.Prior, "previous-static", "data", "index.json")) == Sha(Path.Combine(f.Release, "previous-static", "data", "index.json")) && Sha(Path.Combine(f.Prior, "previous-static", "index.html")) != Sha(Path.Combine(f.Release, "previous-static", "index.html")), "same index cannot identify pre424 versus UI424 bundle");
    Check((await UpgradeRun(name + "-prepare", "PublishReviewedWebProgressUi.cs", "prepare", f.Release, f.State, f.Arguments)).Exit == 0, "next UI prepares independently of same data index");
    Check((await UpgradeRun(name + "-backup", "PublishReviewedWebProgressUi.cs", "backup", f.Release, f.State, f.Arguments, true)).Exit == 0, "entire served UI424 bundle verified and committed");
    Put(Path.Combine(f.Work, "hook-backup.staging-abandoned", "state.json"), "INTERRUPTED_HOOK_BACKUP");
    Check((await UpgradeRun(name + "-upgrade", "ConfigureReviewedWebProgressUiHook.cs", "install", f.Release, f.State, f.Arguments)).Exit == 0 && Sha(f.OldHook) != oldHook, "owned UI424 hook atomically upgrades");
    Check(File.ReadAllText(Path.Combine(f.Work, "hook-backup.staging-abandoned", "state.json")) == "INTERRUPTED_HOOK_BACKUP", "partial hook backup staging remains preserved");
    Check(Sha(Path.Combine(f.Work, "hook-backup", "dropins", "zz-reviewed-ui.conf")) == oldHook && !Read(f.State)["active"]!.GetValue<bool>(), "complete old owned hook backed up before swap and timer stopped");
    Check((await UpgradeRun(name + "-early-resume", "ConfigureReviewedWebProgressUiHook.cs", "resume", f.Release, f.State, f.Arguments)).Exit != 0, "upgrade cannot resume before publication");
    var state = Read(f.State); state["portalExit"] = 23; Save(f.State, state);
    var failed = await UpgradeRun(name + "-publish-fail", "PublishReviewedWebProgressUi.cs", "publish", f.Release, f.State, f.Arguments);
    Check(failed.Exit != 0 && failed.Text.Contains("original exit=23") && !File.Exists(Path.Combine(f.Work, "published.json")), "original failed portal exit retained without publication claim");
    state["portalExit"] = 0; Save(f.State, state);
    Check((await UpgradeRun(name + "-publish", "PublishReviewedWebProgressUi.cs", "publish", f.Release, f.State, f.Arguments)).Exit == 0, "new UI publication and every served byte match");
    var metadata = Path.Combine(f.Work, "previous-snapshot", "metadata.json");
    var metadataBytes = File.ReadAllBytes(metadata); File.Delete(metadata);
    Check((await UpgradeRun(name + "-partial-skip", "PublishReviewedWebProgressUi.cs", "publish", f.Release, f.State, f.Arguments)).Exit != 0, "same fingerprint cannot skip with incomplete rollback backup");
    Check((await UpgradeRun(name + "-partial-rollback", "PublishReviewedWebProgressUi.cs", "rollback", f.Release, f.State, f.Arguments)).Exit != 0 && !File.Exists(Path.Combine(f.Work, "ROLLED_BACK")), "incomplete backup cannot rollback or claim restored state");
    Check((await UpgradeRun(name + "-partial-resume", "ConfigureReviewedWebProgressUiHook.cs", "resume", f.Release, f.State, f.Arguments)).Exit != 0, "incomplete backup cannot resume successful new UI");
    File.WriteAllBytes(metadata, metadataBytes);
    Check((await UpgradeRun(name + "-skip", "PublishReviewedWebProgressUi.cs", "publish", f.Release, f.State, f.Arguments)).Text.Contains("UI_SKIP_SAME includes UI identity"), "complete backup permits same UI+data fingerprint skip");
    var publication = Read(Path.Combine(f.Work, "published.json")); publication["review"] = new string('e', 40); Save(Path.Combine(f.Work, "published.json"), publication);
    Check((await UpgradeRun(name + "-wrong-publication", "ConfigureReviewedWebProgressUiHook.cs", "resume", f.Release, f.State, f.Arguments)).Exit != 0 && !Read(f.State)["active"]!.GetValue<bool>(), "mismatched publication cannot resume");
    publication["review"] = review; Save(Path.Combine(f.Work, "published.json"), publication);
    Check((await UpgradeRun(name + "-resume", "ConfigureReviewedWebProgressUiHook.cs", "resume", f.Release, f.State, f.Arguments)).Exit == 0 && Read(f.State)["active"]!.GetValue<bool>() == active, "matched publication resumes only original active preference");
    Check((await UpgradeRun(name + "-rollback", "PublishReviewedWebProgressUi.cs", "rollback", f.Release, f.State, f.Arguments)).Exit == 0 && File.Exists(Path.Combine(f.Work, "ROLLED_BACK")), "rollback publishes exact UI424 whole bundle");
    var sibling = Path.Combine(Path.GetDirectoryName(f.OldHook)!, "10-old.conf");
    var siblingBytes = File.ReadAllBytes(sibling); File.AppendAllText(sibling, "# external restore edit\n"); var editedSibling = Sha(sibling);
    Check((await UpgradeRun(name + "-edited-restore", "ConfigureReviewedWebProgressUiHook.cs", "restore", f.Release, f.State, f.Arguments)).Exit != 0 && Sha(sibling) == editedSibling, "owned restore refuses and retains external edits");
    File.WriteAllBytes(sibling, siblingBytes);
    var savedHook = Path.Combine(f.Work, "hook-backup", "dropins", "zz-reviewed-ui.conf");
    var savedHookBytes = File.ReadAllBytes(savedHook); File.AppendAllText(savedHook, "# corrupt backup\n");
    var installedHookSha = Sha(f.OldHook);
    Check((await UpgradeRun(name + "-corrupt-hook-backup", "ConfigureReviewedWebProgressUiHook.cs", "restore", f.Release, f.State, f.Arguments)).Exit != 0 && Sha(f.OldHook) == installedHookSha, "incomplete hook backup refuses restore without losing installed bytes");
    File.WriteAllBytes(savedHook, savedHookBytes);
    Check((await UpgradeRun(name + "-restore", "ConfigureReviewedWebProgressUiHook.cs", "restore", f.Release, f.State, f.Arguments)).Exit == 0 && Sha(f.OldHook) == oldHook && ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.GetDirectoryName(f.OldHook)!)) == originalDropins, "restore recovers exact UI424 oldhook and every drop-in");
    Check(Read(f.State)["active"]!.GetValue<bool>() == active && Read(f.State)["enabled"]!.GetValue<string>() == enabled && !File.ReadAllText(f.State + ".commands").Split('\n').Any(l => l.StartsWith("enable ", StringComparison.Ordinal) || l.StartsWith("disable ", StringComparison.Ordinal)), "actual disabled/inactive/enabled preference never changed");
    Check(Sha(Path.Combine(f.Prior, "work", "hook-backup", "state.json")) == oldStateSha && Sha(Path.Combine(f.Prior, "release.json")) == oldReleaseSha, "legacy records consumed without backfilling new fields");
    Check((await UpgradeRun(name + "-rolled-back-publish", "PublishReviewedWebProgressUi.cs", "publish", f.Release, f.State, f.Arguments)).Exit != 0, "rolled back new hook cannot overwrite old UI");
}
foreach (var failure in new[] { "hook-edit", "dropin-edit", "missing-record", "bad-pin", "tool-edit", "post-edit", "queued", "activating", "tasks", "tasks-empty-group", "partial-backup", "wrong-ui-same-index", "missing-asset", "symlink-source", "overlap", "edit-before-backup", "edit-during-verification" })
{
    var f = Upgrade(failure, false, "disabled");
    Check((await UpgradeRun(failure + "-prepare", "PublishReviewedWebProgressUi.cs", "prepare", f.Release, f.State, f.Arguments)).Exit == 0, failure + " prepares fixture");
    Check((await UpgradeRun(failure + "-backup", "PublishReviewedWebProgressUi.cs", "backup", f.Release, f.State, f.Arguments, true)).Exit == 0, failure + " initial full backup");
    var state = Read(f.State);
    switch (failure)
    {
        case "hook-edit": File.AppendAllText(f.OldHook, "# external edit\n"); break;
        case "dropin-edit": File.AppendAllText(Path.Combine(Path.GetDirectoryName(f.OldHook)!, "10-old.conf"), "# external edit\n"); break;
        case "missing-record": File.Delete(Path.Combine(f.Prior, "work", "hook-backup", "state.json")); break;
        case "bad-pin": f.Arguments[Array.IndexOf(f.Arguments, "--previous-release-sha256") + 1] = new string('0', 64); break;
        case "tool-edit": var tool = Path.Combine(f.Prior, "tools", "scripts", "PublishReviewedWebProgressUi.cs"); File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite); File.AppendAllText(tool, "// external edit"); break;
        case "post-edit": state["postOverride"] = "{ path=dotnet ; argv[]=unknown ; ignore_errors=no ; }"; break;
        case "queued": state["job"] = "123"; break;
        case "activating": state["writerActive"] = "activating"; state["writerSub"] = "start-post"; break;
        case "tasks": state["tasks"] = "1"; state["group"] = "/stub"; break;
        case "tasks-empty-group": state["tasks"] = "1"; break;
        case "partial-backup": File.Delete(Path.Combine(f.Work, "previous-snapshot", "metadata.json")); break;
        case "wrong-ui-same-index": File.WriteAllText(Path.Combine(f.Work, "previous-snapshot", "bundle", "index.html"), "PRE424 wrong UI same index"); break;
        case "missing-asset": File.Delete(Path.Combine(f.Work, "previous-snapshot", "bundle", "assets", "fixture.js")); break;
        case "symlink-source": var data = Path.GetFullPath(f.Arguments[Array.IndexOf(f.Arguments, "--data") + 1], Path.Combine(f.Release, "tools", "scripts")); var linked = Path.Combine(Path.GetDirectoryName(data)!, "linked-data"); Directory.CreateSymbolicLink(linked, data); f.Arguments[Array.IndexOf(f.Arguments, "--data") + 1] = Path.GetRelativePath(Path.Combine(f.Release, "tools", "scripts"), linked); break;
        case "overlap": f.Arguments[Array.IndexOf(f.Arguments, "--unit-dir") + 1] = "../../work/units"; break;
        case "edit-before-backup": state["editOnCat"] = true; break;
        case "edit-during-verification": mutateHook = f.OldHook; break;
    }
    Save(f.State, state);
    var original = ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.GetDirectoryName(f.OldHook)!));
    var hookBytes = File.ReadAllText(f.OldHook);
    var result = await UpgradeRun(failure + "-refused", "ConfigureReviewedWebProgressUiHook.cs", "install", f.Release, f.State, f.Arguments);
    Check(result.Exit != 0 && (failure.StartsWith("edit-", StringComparison.Ordinal) ? File.ReadAllText(f.OldHook) == hookBytes + "# late external edit\n" : ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.GetDirectoryName(f.OldHook)!)) == original), failure + " refusal preserves all actual original bytes");
    Check(!File.Exists(Path.Combine(f.Work, "published.json")) && !Read(f.State)["active"]!.GetValue<bool>(), failure + " never publishes or resumes");
}
// Real freeze CLI, real fixed Git objects, and a synthetic accepted UI repo.
// This never borrows the incomplete UI candidate or any live release files.
async Task<string> Git(string directory, params string[] arguments)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync(); if (process.ExitCode != 0) throw new IOException(await error); return (await output).Trim();
}
var uiRepo = Path.Combine(work, "freeze-ui-repo"); Bundle(Path.Combine(uiRepo, "web", "dist"), "FREEZE_ACCEPTED_FIXTURE");
Directory.Delete(Path.Combine(uiRepo, "web", "dist", "data"), true);
await Git(uiRepo, "init", "--quiet"); await Git(uiRepo, "config", "user.name", "Fixture"); await Git(uiRepo, "config", "user.email", "fixture@example.invalid");
await Git(uiRepo, "add", "web"); await Git(uiRepo, "commit", "--quiet", "-m", "Synthetic accepted fixture");
var uiSha = await Git(uiRepo, "rev-parse", "HEAD"); var toolsSha = await Git(root, "rev-parse", "HEAD");
var freezeOld = Path.Combine(work, "freeze-old"); Bundle(freezeOld, "UI424");
var frozen = Path.Combine(work, "frozen-cli");
var distHash = ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(Path.Combine(uiRepo, "web", "dist")));
string[] freezeArgs = ["run", "--file", "scripts/FreezeReviewedWebProgressUi.cs", "--", "--tools-commit", toolsSha, "--ui-commit", uiSha, "--review", review, "--dist-hash", distHash, "--previous-tree-hash", ReviewedWebProgressRelease.TreeHash(ReviewedWebProgressRelease.Hashes(freezeOld)), "--ui-tree", Relative(uiRepo), "--previous-bundle", Relative(freezeOld), "--release", Relative(frozen)];
Check((await Run("freeze-cli", freezeArgs)).Exit == 0, "real freeze CLI accepts explicit next UI identity and exact Git tools");
var frozenRecord = ReviewedWebProgressRelease.Frozen(frozen, Sha(Path.Combine(frozen, "release.json")));
Check(frozenRecord["toolsCommit"]!.GetValue<string>() == toolsSha && frozenRecord["uiCommit"]!.GetValue<string>() == uiSha, "frozen record pins both exact source SHAs");
Check((File.GetUnixFileMode(Path.Combine(frozen, "release.json")) & UnixFileMode.UserWrite) == 0 && Directory.GetFiles(Path.Combine(frozen, "tools", "scripts")).Length == 7, "seven whitelisted sources and release manifest frozen read-only");
var frozenScripts = Path.Combine(frozen, "tools", "scripts");
Check((await Run("frozen-cli-prepare", ["run", "--file", Path.Combine(frozenScripts, "PublishReviewedWebProgressUi.cs"), "--", "--mode", "prepare", "--work", "../../work", "--dist", "../../ui", "--data", Path.GetRelativePath(frozenScripts, Path.Combine(freezeOld, "data")), "--ui-commit", uiSha, "--dist-hash", distHash, "--review", review])).Exit == 0, "frozen real Git tools run prepare without editing checkout dependencies");
Check(!File.Exists(Path.Combine(frozen, "work", "published.json")), "freeze and prepare do not claim publication");
Check((await Run("freeze-existing-refused", freezeArgs)).Exit != 0, "freeze refuses to overwrite existing release");
Put(Path.Combine(uiRepo, "web", "dist", "index.html"), "UNREVIEWED_EDIT");
freezeArgs[^1] = Relative(Path.Combine(work, "frozen-drift"));
Check((await Run("freeze-drift-refused", freezeArgs)).Exit != 0 && !Directory.Exists(Path.Combine(work, "frozen-drift")), "accepted UI source drift rejected before creating release");
}
cancellation.Cancel(); await server;
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(root, work));
Console.WriteLine("ALL PASS " + checks + " UI hook stub checks; no real systemd/portal mode executed");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;
