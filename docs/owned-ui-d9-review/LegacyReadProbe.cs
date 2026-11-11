#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
var repo = Path.GetFullPath("../..", Path.GetDirectoryName(Source())!); var work = Path.Combine(repo, "temp_docs", "owned-ui-d9-review", "legacy-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); var release = Path.Combine(profile, ".local", "share", "gm-kernel-progress-ui", "releases", "ui-424-tools-35498ac"); var scripts = Path.Combine(release, "tools", "scripts");
string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Dictionary<string, string> Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), p => Sha(File.ReadAllBytes(p)), StringComparer.Ordinal);
string Tree(Dictionary<string, string> files) { using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); foreach (var p in files.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.AppendData(Encoding.UTF8.GetBytes(p.Key)); hash.AppendData(Encoding.UTF8.GetBytes(p.Value)); } return Convert.ToHexStringLower(hash.GetHashAndReset()); }
string DropHash(Dictionary<string, string> files) => Sha(Encoding.UTF8.GetBytes(string.Join("\n", files.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value))));
var checks = 0; void Require(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
var manifestPath = Path.Combine(release, "release.json"); var statePath = Path.Combine(release, "work", "hook-backup", "state.json"); var beforeManifest = File.ReadAllBytes(manifestPath); var beforeState = File.ReadAllBytes(statePath); var record = JsonNode.Parse(beforeManifest)!; var state = JsonNode.Parse(beforeState)!;
Require(Sha(beforeManifest) == "504cc8661339a52e9cb2a40a4ad1f88d573e2b4bc1e706105b77c199033bc660" && Sha(beforeState) == "b9e403bdc1767796766eea78dc6df5f586aeeef7066551253dd192f0425bbc2b", "actual old release and hook-state raw pins");
var actual = new Dictionary<string, string>(); foreach (var dir in new[] { "tools", "ui", "previous-static" }) foreach (var p in Files(Path.Combine(release, dir))) actual.Add(dir + "/" + p.Key, p.Value);
Require(actual.Count == record["files"]!.AsObject().Count && actual.All(p => record["files"]![p.Key]!.GetValue<string>() == p.Value), "all actual frozen manifest file hashes");
foreach (var dir in new[] { "tools", "ui", "previous-static" }) Require(Directory.GetFiles(Path.Combine(release, dir), "*", SearchOption.AllDirectories).Concat(Directory.GetDirectories(Path.Combine(release, dir), "*", SearchOption.AllDirectories)).Append(Path.Combine(release, dir)).All(p => (File.GetUnixFileMode(p) & (UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0), "actual " + dir + " read only");
foreach (var file in Directory.GetFiles(scripts))
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var arg in new[] { "show", "35498ac5e72eda303883120d8b58cbb6c15fdb33:scripts/" + Path.GetFileName(file) }) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!; using var output = new MemoryStream(); var copy = process.StandardOutput.BaseStream.CopyToAsync(output); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); await copy; Require(process.ExitCode == 0 && output.ToArray().SequenceEqual(File.ReadAllBytes(file)), "actual old tool equals fixed Git object " + Path.GetFileName(file));
}
var units = Path.Combine(profile, ".config", "systemd", "user"); var dropins = Path.Combine(units, "gm-kernel-progress-refresh.service.d"); var current = Files(dropins); var expected = Files(Path.Combine(release, "work", "hook-backup", "dropins"));
Require(DropHash(expected) == state["originalDropinsHash"]!.GetValue<string>(), "actual original full dropins match backup state"); expected["zz-reviewed-ui.conf"] = state["hookHash"]!.GetValue<string>(); Require(current.Count == expected.Count && current.All(p => expected.TryGetValue(p.Key, out var h) && h == p.Value), "actual current full dropins are exactly owned transition");
var hook = File.ReadAllText(Path.Combine(dropins, "zz-reviewed-ui.conf")); var command = hook.Split('\n').Single(x => x.StartsWith("ExecStartPost=dotnet "))["ExecStartPost=".Length..].Replace("%h/", profile + "/", StringComparison.Ordinal);
var savedPost = File.ReadAllText(Path.Combine(release, "work", "hook-backup", "effective-post-after.txt")); var argv = Regex.Matches(savedPost, @"argv\[\]=(.*?) ;", RegexOptions.Singleline);
Require(argv.Count == 1 && argv[0].Groups[1].Value == command && savedPost.Contains("ignore_errors=no"), "real retained effective argv equals exact current owned hook");
var bundle = JsonNode.Parse(File.ReadAllBytes(Path.Combine(release, "work", "prepared.json")))!["bundle"]!.GetValue<string>(); var currentBundle = Path.GetFullPath(bundle, Path.Combine(release, "work")); var bundleFiles = Files(currentBundle);
Require(bundleFiles.Count == 610 && Tree(bundleFiles) == "ffcf2b2269f8208622da84ded3346f72f63288d63ecfc9e91bb9b2b96676a1b7", "actual current prepared 610-file UI424 bundle identity");
Require(Tree(Files(Path.Combine(release, "ui"))) == "f443bb7ede213480603647f385707cfe09aed194eee2772d7fcad7d5e688ca88" && record["uiCommit"]!.GetValue<string>() == "424e85d6aac32c2afe7cad12cfd04bea4f0037ab" && record["review"]!.GetValue<string>() == "f1fdc36553e9132c88d78a933e503160950460f2", "actual fixed UI and independent review identity");
using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(20) }; var served = new JsonArray();
foreach (var path in new[] { "index.html", "data/index.json", "assets/index-DQUBkM3M.js" }) { using var response = await http.GetAsync("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/" + path); var bytes = await response.Content.ReadAsByteArrayAsync(); Require(response.IsSuccessStatusCode && Sha(bytes) == bundleFiles[path], "read-only actual HTTPS matches frozen current bundle " + path); served.Add((JsonNode)new JsonObject { ["path"] = path, ["status"] = (int)response.StatusCode, ["sha256"] = Sha(bytes), ["length"] = bytes.Length }); }
Require(beforeManifest.SequenceEqual(File.ReadAllBytes(manifestPath)) && beforeState.SequenceEqual(File.ReadAllBytes(statePath)) && state["ownedUpgrade"] is null && state["previousBundleHash"] is null, "legacy records unchanged with no backfilled metadata");
File.WriteAllText(Path.Combine(work, "receipt.json"), new JsonObject { ["utc"] = DateTimeOffset.UtcNow.ToString("O"), ["manifestSha256"] = Sha(beforeManifest), ["stateSha256"] = Sha(beforeState), ["fullCurrentDropins"] = new JsonObject(current.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, JsonValue.Create(p.Value)))), ["currentBundleHash"] = Tree(bundleFiles), ["historicalEffectiveArgvMatchesCurrentFile"] = true, ["newLiveManagerQuery"] = false, ["https"] = served }.ToJsonString());
Console.WriteLine("SUMMARY " + checks + "/" + checks + " read-only legacy checks; " + Path.GetRelativePath(repo, work));
static string Source([CallerFilePath] string path = "") => path;
