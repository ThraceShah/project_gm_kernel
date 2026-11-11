#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
var state=JsonNode.Parse(File.ReadAllText(file))!;
var a=args.Where(s=>s!="--user").ToArray();
File.AppendAllText(file+".commands",string.Join(" ",a)+"\n");
var timer=a.Length>1&&a[1].EndsWith(".timer",StringComparison.Ordinal);
switch(a[0]){
case "show":
 if(a.Contains("--property=LoadState"))Console.WriteLine("loaded");
 else if(a.Contains("--property=ExecStartPost"))Console.WriteLine("{ path=dotnet ; argv[]=dotnet run --file PublishReviewedWebProgressUi.cs ; }");
 else{Console.WriteLine("ActiveState="+state["writerActive"]);Console.WriteLine("SubState="+state["writerSub"]);Console.WriteLine("MainPID="+state["pid"]);Console.WriteLine("Job="+state["job"]);Console.WriteLine("TasksCurrent="+state["tasks"]);Console.WriteLine("ControlGroup="+state["group"]);}return 0;
case "cat":Console.WriteLine("[Service]\nExecStart=old-producer\nExecStartPost=old-prepare\nExecStartPost=old-publish");return 0;
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
    if (stateFile is not null) { info.Environment["GM_UI_HOOK_STUB"] = stateFile; info.Environment["PATH"] = Path.Combine(project, "bin", "Debug", "net10.0") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"); }
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
    Save(Path.Combine(release, "prepared.json"), new JsonObject { ["uiCommit"] = ui, ["uiHash"] = hash });
    var state = Path.Combine(work, name, "stub.json");
    Save(state, new JsonObject { ["active"] = active, ["enabled"] = enabled, ["writerActive"] = "inactive", ["writerSub"] = "dead", ["pid"] = "0", ["job"] = "", ["tasks"] = "[not set]", ["group"] = "" });
    return (release, units, state, old);
}
Task<(int Exit, string Text)> Configure(string label, string mode, string release, string units, string state) => Run(label,
    ["run", "--file", "scripts/ConfigureReviewedWebProgressUiHook.cs", "--", "--mode", mode, "--execute", "--work", Relative(release), "--unit-dir", Relative(units), "--dist", "../fixture-ui", "--data", "../fixture-producer-data", "--baseline-data", "../fixture-baseline-data", "--portalctl", "../fixture-portalctl", "--previous-bundle", "../fixture-previous", "--ui-commit", ui, "--dist-hash", hash, "--review", review], state);
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
Console.WriteLine("EVIDENCE " + Path.GetRelativePath(root, work));
Console.WriteLine("ALL PASS " + checks + " UI hook stub checks; no real systemd/portal mode executed");
return 0;
static string ScriptPath([CallerFilePath] string path = "") => path;
