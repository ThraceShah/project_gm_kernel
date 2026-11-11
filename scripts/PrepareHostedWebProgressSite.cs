#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseHostedWebProgressSite=true
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Prepares an immutable UI release snapshot for the read-only hosted web
// progress site (see docs/web-progress-hosting.md). The built dist is never
// served directly: vite's emptyOutDir may wipe web/dist at any time during a
// build, so this script copies a finished, validated dist (without the bundled
// data/ tree; the host serves the validated data cache at /data) into
// site/ui-releases/<id>/ and records per-file checksums beside it. The release
// id is content derived, so repeated runs with an unchanged build are a no-op.
// Nothing here touches the currently active pointer: activation is a separate,
// atomic step (ActivateHostedWebProgressSite.cs).
//
// Usage (all paths resolve against this script's own directory):
//   dotnet run --file scripts/PrepareHostedWebProgressSite.cs -- \
//     [--dist ../web/dist] [--site ../temp_docs/web-progress-hosting/site] \
//     [--web ../web] [--builder pnpm] [--no-build]
var scripts = Path.GetDirectoryName(ScriptPath())!;
var dist = Path.GetFullPath(Option("--dist", "../web/dist"), scripts);
var site = Path.GetFullPath(Option("--site", "../temp_docs/web-progress-hosting/site"), scripts);
var web = Path.GetFullPath(Option("--web", "../web"), scripts);
if (!args.Contains("--no-build"))
{
    if (!File.Exists(Path.Combine(web, "package.json"))) throw new FileNotFoundException("Merged frontend package.json is required.");
    var builder = Option("--builder", "pnpm");
    if (builder is not ("pnpm" or "npm")) throw new ArgumentException("builder must be pnpm or npm");
    Run(builder, builder == "pnpm" ? ["install", "--frozen-lockfile"] : ["ci"], web);
    Run(builder, ["run", "build"], web);
}
var release = HostedWebProgressSite.PrepareRelease(site, dist);
var verification = HostedWebProgressSite.VerifyRelease(site, release["release"]!.GetValue<string>());
if (verification["errors"]!.AsArray().Count > 0) throw new InvalidDataException(verification.ToJsonString());
return 0;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
static void Run(string command, string[] arguments, string cwd)
{
    var info = new ProcessStartInfo(command) { WorkingDirectory = cwd, UseShellExecute = false };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
    using var process = Process.Start(info)!;
    process.WaitForExit();
    if (process.ExitCode != 0) throw new IOException(command + " exited " + process.ExitCode);
}
