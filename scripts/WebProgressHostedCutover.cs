#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseHostedWebProgressSite=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Reviewable operational script. Default/help only prints usage. No mode is
// executed by the integration gate; deploy/rollback require --execute and a
// later independently accepted main SHA. See docs/web-progress-publishing-integration.md.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var mode = Option("--mode", "plan");
if (args.Contains("--help") || mode == "plan")
{
    Console.WriteLine("WebProgressHostedCutover --mode check|deploy|rollback --publisher <relative> --backup <relative> --unit-dir <relative> --portalctl <relative> --static-bundle <relative> --site <relative> --state <relative> --expected-main <full SHA> --tailnet-ip <literal IPv4> [--port 8790] [--execute]");
    Console.WriteLine("All filesystem options resolve against this script. Check is read-only. Deploy/rollback need independent review; neither has been executed by this task. Back up the exact current static bundle before replacing the same gm-kernel-progress slug.");
    return 0;
}
if (mode is not ("check" or "deploy" or "rollback")) throw new ArgumentException("Unknown mode");
if (mode != "check" && !args.Contains("--execute")) throw new ArgumentException("Operational mutation requires explicit --execute after review");
string Resolve(string name)
{
    var value = Option(name, "");
    if (value.Length == 0 || Path.IsPathRooted(value)) throw new ArgumentException(name + " must be script-relative");
    return Path.GetFullPath(value, scripts);
}
var publisher = Resolve("--publisher");
var backup = Resolve("--backup");
var units = Resolve("--unit-dir");
var portal = Resolve("--portalctl");
var staticBundle = Resolve("--static-bundle");
var site = Resolve("--site");
var mirror = Resolve("--state");
var expected = Option("--expected-main", "");
var ip = Option("--tailnet-ip", "");
var port = int.Parse(Option("--port", "8790"));
if (expected.Length != 40 || !expected.All(char.IsAsciiHexDigit) || !System.Net.IPAddress.TryParse(ip, out _) || port is < 1 or > 65535) throw new ArgumentException("exact SHA, Tailnet IP and valid port required");
foreach (var path in new[] { publisher, backup, units, portal, staticBundle, site, mirror }) HostedWebProgressSite.RequireSafeTree(path);
HostedWebProgressSite.RequireSeparate(site, publisher);
HostedWebProgressSite.RequireSeparate(site, mirror);
foreach (var path in new[] { publisher, units, staticBundle, site, mirror }) HostedWebProgressSite.RequireSeparate(backup, path);
var dropins = Path.Combine(units, "gm-kernel-progress-refresh.service.d");
var hostUnit = Path.Combine(units, "gm-kernel-progress-host.service");
const string slug = "gm-kernel-progress";
const string refresh = "gm-kernel-progress-refresh.service";
const string timer = "gm-kernel-progress-refresh.timer";
const string host = "gm-kernel-progress-host.service";
var publicOrigin = "https://qingyu1-linux.tail77e06.ts.net/apps/" + slug + "/";
async Task<(int Exit, string Text)> Run(string command, params string[] arguments)
{
    var info = new ProcessStartInfo(command) { WorkingDirectory = publisher, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var text = (await output).Trim();
    Console.WriteLine(Path.GetFileName(command) + " " + string.Join(" ", arguments) + " exit=" + process.ExitCode);
    Console.WriteLine(text); Console.Error.WriteLine((await error).Trim());
    return (process.ExitCode, text);
}
async Task Require(string command, params string[] arguments) { var result = await Run(command, arguments); if (result.Exit != 0) throw new IOException(command + " failed exit=" + result.Exit); }
async Task<bool> SystemState(string action, string unit) => (await Run("systemctl", "--user", action, unit)).Exit == 0;
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
async Task<byte[]> Get(string url) { using var response = await http.GetAsync(url); response.EnsureSuccessStatusCode(); return await response.Content.ReadAsByteArrayAsync(); }
void CopyTree(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
    { var target = Path.Combine(to, Path.GetRelativePath(from, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
}
Dictionary<string, string> Hashes(string dir) => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), f => HostedWebProgressSite.HashHex(File.ReadAllBytes(f)));
string HomePath(string path)
{
    var relative = Path.GetRelativePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path);
    if (relative.StartsWith("..", StringComparison.Ordinal)) throw new InvalidDataException("unit path must be under current user's home");
    return "%h/" + relative.Replace('\\', '/').Replace("%", "%%");
}
var recoveryPath = Path.Combine(backup, "recovery.json");
if (mode == "rollback")
{
    var recovery = HostedWebProgressSite.ReadJsonObject(recoveryPath) ?? throw new InvalidDataException("missing recovery record");
    var saved = Path.Combine(backup, "static-bundle");
    var hashes = Hashes(saved);
    var expectedHashes = recovery["staticHashes"]!.AsObject();
    if (hashes.Count != expectedHashes.Count || hashes.Any(p => p.Value != expectedHashes[p.Key]?.GetValue<string>())) throw new InvalidDataException("rollback bundle integrity mismatch");
    if (await SystemState("is-active", refresh)) throw new InvalidDataException("refresh running; wait for it before rollback");
    await Require("systemctl", "--user", "stop", timer);
    await Require(portal, "static", "--slug", slug, "--name", "GM Kernel vs Parasolid 对比工作台", "--dir", saved);
    if (!Enumerable.SequenceEqual(File.ReadAllBytes(Path.Combine(saved, "data", "index.json")), await Get(publicOrigin + "data/index.json"))) throw new IOException("static rollback HTTP differs from backed-up bundle");
    if (Directory.Exists(dropins)) Directory.Delete(dropins, true);
    if (Directory.Exists(Path.Combine(backup, "refresh-dropins"))) CopyTree(Path.Combine(backup, "refresh-dropins"), dropins);
    await Require("systemctl", "--user", "disable", "--now", host);
    if (File.Exists(hostUnit)) File.Delete(hostUnit);
    if (File.Exists(Path.Combine(backup, "host.service"))) File.Copy(Path.Combine(backup, "host.service"), hostUnit);
    await Require("systemctl", "--user", "daemon-reload");
    if (recovery["hostEnabled"]!.GetValue<bool>()) await Require("systemctl", "--user", "enable", host);
    if (recovery["hostActive"]!.GetValue<bool>()) await Require("systemctl", "--user", "start", host);
    if ((await Run("systemctl", "--user", "is-enabled", timer)).Text != recovery["timerEnabled"]!.GetValue<string>()) throw new IOException("timer preference changed externally; left stopped");
    if (recovery["timerActive"]!.GetValue<bool>()) await Require("systemctl", "--user", "start", timer);
    Console.WriteLine("ROLLBACK_RESTORED static bundle and previous service/drop-in state; immutable host generations retained");
    return 0;
}
// Read-only execution checks: do not fetch/pull/rewrite the live checkout.
if ((await Run("git", "rev-parse", "HEAD")).Text != expected || (await Run("git", "ls-remote", "origin", "refs/heads/main")).Text.Split('\t')[0] != expected)
    throw new InvalidDataException("ENV_NOT_READY publisher and actual origin/main must match the independently accepted expected SHA");
if ((await Run("git", "status", "--porcelain", "--untracked-files=no")).Text.Length > 0) throw new InvalidDataException("ENV_NOT_READY dirty publisher");
if (!(await Run("dotnet", "--version")).Text.StartsWith("10.", StringComparison.Ordinal)) throw new InvalidDataException("ENV_NOT_READY .NET10 required");
if (!(await Run("tailscale", "ip", "-4")).Text.Split('\n').Contains(ip, StringComparer.Ordinal)) throw new InvalidDataException("ENV_NOT_READY Tailnet address mismatch");
if (await SystemState("is-active", refresh)) throw new InvalidDataException("refresh running; wait for completion before cutover");
await Require("systemctl", "--user", "show", timer, "--property=ActiveState", "--property=UnitFileState");
await Require("systemctl", "--user", "cat", refresh);
await Require(portal, "list");
var oldFiles = Directory.GetFiles(staticBundle, "*", SearchOption.AllDirectories);
if (oldFiles.Length > 5000 || oldFiles.Sum(f => new FileInfo(f).Length) > 30L * 1024 * 1024) throw new InvalidDataException("old static rollback bundle exceeds safe capacity");
if (System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port)) throw new IOException("host port already in use; do not stop another owner");
if (!Enumerable.SequenceEqual(File.ReadAllBytes(Path.Combine(staticBundle, "data", "index.json")), await Get(publicOrigin + "data/index.json"))) throw new InvalidDataException("backup input is not the currently served static bundle");
var source = Path.Combine(publisher, "temp_docs", "web-progress-publish", "data");
var closure = HostedWebProgressSite.CheckDataRoot(source);
if (closure["errors"]!.AsArray().Count > 0) throw new InvalidDataException("ENV_NOT_READY " + closure.ToJsonString());
if (mode == "check") { Console.WriteLine("CHECK_ONLY no deployment executed; independent review, frontend/browser and private native-env checks remain required"); return 0; }
if (Directory.Exists(backup)) throw new InvalidDataException("backup must be new; never replace earlier evidence");
Directory.CreateDirectory(backup);
CopyTree(staticBundle, Path.Combine(backup, "static-bundle"));
if (Directory.Exists(dropins)) CopyTree(dropins, Path.Combine(backup, "refresh-dropins"));
if (File.Exists(hostUnit)) File.Copy(hostUnit, Path.Combine(backup, "host.service"));
HostedWebProgressSite.WriteJsonAtomic(recoveryPath, new JsonObject
{
    ["schemaVersion"] = 1, ["expectedMain"] = expected,
    ["timerActive"] = await SystemState("is-active", timer), ["timerEnabled"] = (await Run("systemctl", "--user", "is-enabled", timer)).Text,
    ["hostActive"] = await SystemState("is-active", host), ["hostEnabled"] = await SystemState("is-enabled", host),
    ["staticHashes"] = new JsonObject(Hashes(Path.Combine(backup, "static-bundle")).Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value)))),
});
await Require("systemctl", "--user", "stop", timer);
await Require("dotnet", "build", "web/host/GmProgressHost.csproj", "--configuration", "Release", "--nologo");
await Require("dotnet", "run", "--file", "scripts/PublishHostedWebProgressSite.cs", "--", "--site", Path.GetRelativePath(Path.Combine(publisher, "scripts"), site), "--state", Path.GetRelativePath(Path.Combine(publisher, "scripts"), mirror));
Directory.CreateDirectory(units);
File.WriteAllText(hostUnit, $"""
[Unit]
Description=Read-only GM progress host
After=network-online.target
[Service]
WorkingDirectory={HomePath(publisher)}
Environment=MSBUILDDISABLENODEREUSE=1
ExecStart=dotnet web/host/bin/Release/net10.0/GmProgressHost.dll --site {HomePath(site)} --listen 127.0.0.1:{port} --listen {ip}:{port}
Restart=on-failure
CPUQuota=100%
MemoryMax=512M
TasksMax=128
[Install]
WantedBy=default.target
""");
Directory.CreateDirectory(dropins);
File.WriteAllText(Path.Combine(dropins, "zz-hosted-activation.conf"), $"""
[Service]
ExecStartPost=
ExecStartPost=dotnet run --file scripts/PublishHostedWebProgressSite.cs -- --site {HomePath(site)} --state {HomePath(mirror)}
""");
await Require("systemd-analyze", "--user", "verify", hostUnit);
await Require("systemctl", "--user", "daemon-reload");
await Require("systemctl", "--user", "enable", "--now", host);
await Get("http://127.0.0.1:" + port + "/healthz");
await Get("http://" + ip + ":" + port + "/healthz");
await Require(portal, "proxy", "--slug", slug, "--name", "GM Kernel vs Parasolid 对比工作台", "--target", "http://" + ip + ":" + port);
var localIndex = await Get("http://127.0.0.1:" + port + "/data/index.json");
if (!Enumerable.SequenceEqual(localIndex, await Get(publicOrigin + "data/index.json"))) throw new IOException("proxy HTTP index differs; execute recorded rollback before resuming timer");
await Get(publicOrigin); await Get(publicOrigin + "healthz");
if (HostedWebProgressSite.ReadJsonObject(recoveryPath)!["timerActive"]!.GetValue<bool>()) await Require("systemctl", "--user", "start", timer);
Console.WriteLine("CUTOVER_APPLIED verify desktop/mobile actual models and units/timer recovery; on failure use --mode rollback with the same arguments");
return 0;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
