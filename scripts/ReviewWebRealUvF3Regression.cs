#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Original assertions, ephemeral port: never interrupts another worktree's port 8089 server.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var web = Path.GetFullPath("../web", scripts);
var filename = Path.Combine(web, "tests/smoke.mjs");
var source = File.ReadAllText(filename)
    .Replace("const __filename = fileURLToPath(import.meta.url);", "const __filename = " + JsonValue.Create(filename)!.ToJsonString() + ";", StringComparison.Ordinal)
    .Replace("const port = 8089;", "const port = 0;", StringComparison.Ordinal)
    .Replace("const baseUrl = `http://127.0.0.1:${port}`;", "const baseUrl = `http://127.0.0.1:${server.address().port}`;", StringComparison.Ordinal);
var start = new ProcessStartInfo("node") { WorkingDirectory = web, RedirectStandardInput = true };
foreach (var argument in new[] { "--input-type=module", "-", "--inspection-only" }) start.ArgumentList.Add(argument);
using var child = Process.Start(start)!;
child.StandardInput.Write(source); child.StandardInput.Close(); child.WaitForExit();
return child.ExitCode;

static string ScriptPath([CallerFilePath] string path = "") => path;
