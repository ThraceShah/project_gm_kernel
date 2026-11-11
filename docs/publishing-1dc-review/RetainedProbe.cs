#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// One additional interface check using only a successful InterfaceProbe's private fixture.
var here = Path.GetDirectoryName(Source())!; var repo = Path.GetFullPath("../..", here);
var work = Path.GetFullPath(args.Single(), here);
var allowed = Path.GetRelativePath(Path.Combine(repo, "temp_docs", "publishing-1dc-review"), work);
if (Path.IsPathRooted(allowed) || allowed.StartsWith("..", StringComparison.Ordinal)) throw new Exception("own fixture required");
var publish = Path.Combine(work, "publish"); var site = Path.Combine(work, "site"); var mirror = Path.Combine(work, "mirror.json");
var evidence = Path.Combine(work, "retained-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(evidence);
JsonObject Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
var aggregate = Read(Path.Combine(publish, "runs", "aggregate", "manifest.json"));
var id = "successor-" + Guid.NewGuid().ToString("N"); aggregate["run"]!["id"] = id;
Directory.CreateDirectory(Path.Combine(publish, "runs", id)); File.WriteAllText(Path.Combine(publish, "runs", id, "manifest.json"), aggregate.ToJsonString());
var index = Read(Path.Combine(publish, "index.json")); var descriptor = index["runs"]![0]!.DeepClone(); descriptor["id"] = id; descriptor["manifest"] = "runs/" + id + "/manifest.json";
index["runs"] = new JsonArray(descriptor); index["latestRunId"] = id; File.WriteAllText(Path.Combine(publish, "index.json"), index.ToJsonString());
var release = Directory.GetFiles(Path.Combine(site, "ui-releases"), "*.meta.json").Select(Read).OrderBy(x => x["createdAt"]!.GetValue<string>(), StringComparer.Ordinal).Last()["id"]!.GetValue<string>();
var info = new ProcessStartInfo("dotnet") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true };
var scripts = Path.Combine(repo, "scripts");
foreach (var arg in new[] { "run", "--no-cache", "--file", "scripts/ActivateHostedWebProgressSite.cs", "--", "--site", Path.GetRelativePath(scripts, site), "--data", Path.GetRelativePath(scripts, publish), "--state", Path.GetRelativePath(scripts, mirror), "--release", release }) info.ArgumentList.Add(arg);
info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
using (var cli = Process.Start(info)!) { var output = cli.StandardOutput.ReadToEndAsync(); var error = cli.StandardError.ReadToEndAsync(); await cli.WaitForExitAsync(); File.WriteAllText(Path.Combine(evidence, "activate.log"), "exit=" + cli.ExitCode + "\n" + await output + "\n" + await error); if (cli.ExitCode != 0) throw new Exception("activate refused"); }
var pointer = Read(Path.Combine(site, "site.json")); var generation = Read(Path.Combine(site, "generations", pointer["generation"]!.GetValue<string>() + ".meta.json"));
if (generation["runs"]!.AsObject().ContainsKey("aggregate")) throw new Exception("probe must exclude old aggregate from active mapping");
Console.WriteLine("PASS active generation excludes aggregate; CLI exit0");
using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
using var host = new Process { StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true } };
foreach (var arg in new[] { "web/host/bin/Release/net10.0/GmProgressHost.dll", "--site", site, "--listen", "127.0.0.1:" + port }) host.StartInfo.ArgumentList.Add(arg);
host.Start(); var hostOut = host.StandardOutput.ReadToEndAsync(); var hostErr = host.StandardError.ReadToEndAsync(); Console.WriteLine($"OWN_HOST pid={host.Id} port={port}");
using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(10) };
var records = new JsonArray();
try
{
    var ready = false; for (var i = 0; i < 100; i++) { try { using var response = await http.GetAsync("/healthz"); if (response.IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { } await Task.Delay(100); } if (!ready) throw new Exception("host not ready");
    foreach (var path in new[] { "index.json", "runs/aggregate/manifest.json", "runs/" + id + "/manifest.json", "runs/raw/e.log" })
    {
        using var response = await http.GetAsync("/data/" + path); var bytes = await response.Content.ReadAsByteArrayAsync();
        if (response.StatusCode != HttpStatusCode.OK || !bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(publish, path)))) throw new Exception("retained HTTP " + path);
        records.Add((JsonNode)new JsonObject { ["path"] = path, ["status"] = (int)response.StatusCode, ["length"] = bytes.Length, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
        Console.WriteLine("PASS exact HTTP200 " + path);
    }
}
finally { if (!host.HasExited) host.Kill(true); await host.WaitForExitAsync(); File.WriteAllText(Path.Combine(evidence, "host.log"), await hostOut + "\n" + await hostErr); File.WriteAllText(Path.Combine(evidence, "http.json"), records.ToJsonString()); Console.WriteLine($"OWN_HOST_CLEANED pid={host.Id} port={port} exit={host.ExitCode}"); }
Console.WriteLine("SUMMARY 5/5 retained checks; " + Path.GetRelativePath(repo, evidence));
static string Source([CallerFilePath] string path = "") => path;
