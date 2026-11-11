#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
#:property UseHostedWebProgressSite=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Local replacement for the static portal hook. No portalctl, geometry, queue
// edits or publisher-state edits. Failed activation retries this exact cache
// on the next invocation; site.json alone commits the serving pair.
if (args.Contains("--help"))
{
    Console.WriteLine("PublishHostedWebProgressSite --site <relative> --state <relative> [--data ../temp_docs/web-progress-publish/data] [--web ../web] [--no-build]");
    return 0;
}
var scripts = Path.GetDirectoryName(ScriptPath())!;
string Resolve(string name, string fallback) => Path.GetFullPath(Option(name, fallback), scripts);
var site = Resolve("--site", "../temp_docs/web-progress-hosting/site");
var state = Resolve("--state", "../temp_docs/web-progress-hosting/activation-state.json");
var data = Resolve("--data", "../temp_docs/web-progress-publish/data");
var web = Resolve("--web", "../web");
foreach (var path in new[] { site, state, state + ".host.lock", data, web }) HostedWebProgressSite.RequireSafeTree(path);
foreach (var path in new[] { state, data, web }) HostedWebProgressSite.RequireSeparate(site, path);
HostedWebProgressSite.RequireSeparate(state, data);
HostedWebProgressSite.RequireSeparate(state, web);
HostedWebProgressSite.RequireSeparate(data, web);
Directory.CreateDirectory(Path.GetDirectoryName(state)!);
using var lease = new FileStream(state + ".host.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var validation = WebProgressDataValidation.Validate(data);
if (validation["errors"]!.AsArray().Count > 0) { Console.Error.WriteLine("HOST_PUBLISH_DATA_INVALID " + validation.ToJsonString()); return 3; }
// The producer must have completed and released its scheduler lock before
// this hook runs (one serialized systemd oneshot). Pin the observed index so
// an unexpected concurrent producer never silently advances this activation.
var indexBytes = File.ReadAllBytes(Path.Combine(data, "index.json"));
var latest = JsonNode.Parse(indexBytes)!["latestRunId"]!.GetValue<string>();
if (!args.Contains("--no-build"))
{
    var builder = Option("--builder", "pnpm");
    if (builder is not ("pnpm" or "npm")) throw new ArgumentException("builder must be pnpm or npm");
    foreach (var command in new[] { builder == "pnpm" ? new[] { "install", "--frozen-lockfile" } : new[] { "ci" }, new[] { "run", "build" } })
    {
        var info = new ProcessStartInfo(builder) { WorkingDirectory = web, UseShellExecute = false };
        foreach (var argument in command) info.ArgumentList.Add(argument);
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(info)!;
        process.WaitForExit();
        if (process.ExitCode != 0) return process.ExitCode;
    }
}
var release = HostedWebProgressSite.PrepareRelease(site, Path.Combine(web, "dist"));
if (!indexBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(data, "index.json"))))
{ Console.Error.WriteLine("HOST_PUBLISH_SOURCE_CHANGED retry after producer completes"); return 3; }
var result = HostedWebProgressSite.Activate(site, release["release"]!.GetValue<string>(), data, state, indexBytes);
Console.WriteLine("HOST_PUBLISH run=" + latest + " status=" + result["status"]);
return result["status"]!.GetValue<string>() is "activated" or "skip-same" ? 0 : 3;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
