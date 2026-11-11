#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Offline systemd parser only: no service manager, daemon-reload, unit install or portal.
var repo = Path.GetFullPath("../..", Path.GetDirectoryName(Source())!);
var work = Path.Combine(repo, "temp_docs", "publishing-1dc-review", "unit-" + Guid.NewGuid().ToString("N"));
var units = Path.Combine(work, "root", "etc", "systemd", "system"); var dropins = Path.Combine(units, "gm-kernel-progress-refresh.service.d"); Directory.CreateDirectory(dropins);
var current = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user", "gm-kernel-progress-refresh.service.d", "zz-reviewed-ui.conf");
var bytes = File.ReadAllBytes(current); File.WriteAllBytes(Path.Combine(dropins, "zz-reviewed-ui.conf"), bytes);
File.WriteAllText(Path.Combine(units, "gm-kernel-progress-refresh.service"), "[Unit]\nDefaultDependencies=no\n[Service]\nType=oneshot\nExecStart=true\n");
File.WriteAllText(Path.Combine(dropins, "zz-hosted-activation.conf"), "[Service]\nExecStartPost=\nExecStartPost=dotnet run --file scripts/PublishHostedWebProgressSite.cs -- --site %h/fixture-site --state %h/fixture-mirror.json\n");
// Supply executable files for offline verification; none are executed by verify.
var bin = Path.Combine(work, "root", "usr", "bin"); Directory.CreateDirectory(bin);
foreach (var name in new[] { "dotnet", "true" })
{
    var executable = Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator).Select(p => Path.Combine(p, name)).First(File.Exists);
    File.Copy(executable, Path.Combine(bin, name)); File.SetUnixFileMode(Path.Combine(bin, name), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
var info = new ProcessStartInfo("systemd-analyze") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (var arg in new[] { "--root=" + Path.Combine(work, "root"), "--man=no", "verify", "gm-kernel-progress-refresh.service" }) info.ArgumentList.Add(arg);
info.Environment["SYSTEMD_LOG_LEVEL"] = "debug"; info.Environment["SYSTEMD_PAGER"] = "cat";
using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
var text = await output + "\n" + await error; File.WriteAllText(Path.Combine(work, "offline-parser.log"), "exit=" + process.ExitCode + "\n" + text);
var lines = text.Split('\n'); var marker = Array.FindIndex(lines, line => line.Contains("ExecStartPost:", StringComparison.Ordinal));
var effective = marker >= 0 && marker + 1 < lines.Length ? lines[marker] + "\n" + lines[marker + 1] : "";
File.WriteAllText(Path.Combine(work, "effective-post.txt"), effective);
if (process.ExitCode != 0 || !effective.Contains("PublishReviewedWebProgressUi.cs") || effective.Contains("PublishHostedWebProgressSite.cs")) throw new Exception("inspect offline-parser.log for unexpected parser result");
if (!bytes.SequenceEqual(File.ReadAllBytes(current))) throw new Exception("live source changed during read-only probe");
File.WriteAllText(Path.Combine(work, "receipt.json"), new JsonObject { ["reviewedDropinSha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)), ["parserExit"] = process.ExitCode, ["fixtureOnly"] = true, ["effectivePost"] = effective }.ToJsonString());
Console.WriteLine("PASS offline effective ExecStartPost remains reviewed static UI hook; hosted hook reset; live bytes unchanged"); Console.WriteLine("EVIDENCE " + Path.GetRelativePath(repo, work));
static string Source([CallerFilePath] string path = "") => path;
