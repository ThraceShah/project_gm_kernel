#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Static UI-only release, independent of scheduler/hosting migration. Never
// builds UI from the data producer's checkout. Every refresh uses the exact
// immutable, independently reviewed UI content hash. Default is prepare only.
if (args.Contains("--help"))
{
    Console.WriteLine("PublishReviewedWebProgressUi --mode prepare|publish|rollback --dist <relative immutable dist> --ui-commit <40hex> --dist-hash <64hex> --data <relative> --work <relative> [--review <40hex> --portalctl <relative> --previous-bundle <relative> --execute]");
    return 0;
}
var scripts = Path.GetDirectoryName(ScriptPath())!;
string Resolve(string name)
{
    var value = Option(name, "");
    if (value.Length == 0 || Path.IsPathRooted(value)) throw new ArgumentException(name + " must be script-relative");
    return Path.GetFullPath(value, scripts);
}
var mode = Option("--mode", "prepare");
if (mode is not ("prepare" or "publish" or "rollback")) throw new ArgumentException("unknown mode");
var work = Resolve("--work");
SafeTree(work);
if (mode != "prepare" && !args.Contains("--execute")) throw new ArgumentException("publishing requires explicit --execute after independent review");
var portal = mode != "prepare" ? Resolve("--portalctl") : "";
var previous = mode == "publish" ? Resolve("--previous-bundle") : "";
if (portal.Length > 0) SafeTree(portal);
var backup = Path.Combine(work, "previous-bundle");
Dictionary<string, string> Hashes(string dir, bool uiOnly = false) => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
    .Select(f => (File: f, Relative: Path.GetRelativePath(dir, f).Replace('\\', '/')))
    .Where(p => !uiOnly || !p.Relative.StartsWith("data/", StringComparison.Ordinal))
    .ToDictionary(p => p.Relative, p => HashHex(File.ReadAllBytes(p.File)), StringComparer.Ordinal);
string HashHex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
void SafeTree(string path)
{
    if (!WebProgressSafePath.IsSafeChain(path, out var bad)) throw new IOException("UI_PATH_UNSAFE " + bad);
    if (Directory.Exists(path)) foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
        if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("UI_PATH_UNSAFE " + entry);
}
void Separate(string a, string b)
{
    if (WebProgressSafePath.IsWithin(a, b) || WebProgressSafePath.IsWithin(b, a)) throw new IOException("UI_PATH_OVERLAP");
}
JsonObject? ReadObject(string path) => File.Exists(path) ? JsonNode.Parse(File.ReadAllBytes(path)) as JsonObject : null;
void WriteAtomic(string path, JsonObject value)
{
    if (!WebProgressSafePath.IsSafeChain(path, out _) || !WebProgressSafePath.IsSafeChain(path + ".tmp", out _)) throw new IOException("UI_PATH_UNSAFE");
    File.WriteAllText(path + ".tmp", value.ToJsonString()); File.Move(path + ".tmp", path, true);
}
string TreeHash(Dictionary<string, string> hashes)
{
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (var (path, sha) in hashes.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.AppendData(Encoding.UTF8.GetBytes(path)); hash.AppendData(Encoding.UTF8.GetBytes(sha)); }
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}
void Copy(string source, string target, Dictionary<string, string> files)
{
    foreach (var (relative, sha) in files)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p.Length == 0 || p is "." or ".." || p.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))) throw new InvalidDataException("unsafe public file " + relative);
        var bytes = File.ReadAllBytes(Path.Combine(source, relative));
        if (HashHex(bytes) != sha) throw new IOException("source changed during snapshot");
        var to = Path.Combine(target, relative); Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.WriteAllBytes(to, bytes);
    }
}
async Task Publish(string bundle)
{
    var info = new ProcessStartInfo(portal) { UseShellExecute = false };
    foreach (var arg in new[] { "static", "--slug", "gm-kernel-progress", "--name", "GM Kernel vs Parasolid 对比工作台", "--dir", bundle }) info.ArgumentList.Add(arg);
    using var process = Process.Start(info)!; await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new IOException("portalctl original exit=" + process.ExitCode);
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    const string origin = "https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/";
    foreach (var relative in new[] { "index.html", "data/index.json" })
        if (!Enumerable.SequenceEqual(File.ReadAllBytes(Path.Combine(bundle, relative)), await http.GetByteArrayAsync(origin + relative))) throw new IOException("served bytes differ: " + relative);
}
Directory.CreateDirectory(work);
using var lease = new FileStream(Path.Combine(work, "publish.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
if (mode == "rollback")
{
    SafeTree(backup);
    var metadata = ReadObject(Path.Combine(work, "previous.json")) ?? throw new InvalidDataException("missing rollback record");
    if (TreeHash(Hashes(backup)) != metadata["treeHash"]!.GetValue<string>()) throw new InvalidDataException("rollback bundle integrity mismatch");
    await Publish(backup);
    // A subsequent timer must not silently re-publish the new UI after rollback.
    File.WriteAllText(Path.Combine(work, "ROLLED_BACK"), "Stop or restore the previous static hook before resuming the timer.\n");
    return 0;
}
if (File.Exists(Path.Combine(work, "ROLLED_BACK"))) throw new InvalidDataException("UI_ROLLED_BACK timer hook must be restored before a new release");
var dist = Resolve("--dist"); var data = Resolve("--data");
foreach (var path in new[] { dist, data }) { SafeTree(path); Separate(work, path); }
Separate(dist, data);
var uiCommit = Option("--ui-commit", ""); var review = Option("--review", "");
if (uiCommit.Length != 40 || !uiCommit.All(char.IsAsciiHexDigit) || mode == "publish" && (review.Length != 40 || !review.All(char.IsAsciiHexDigit))) throw new ArgumentException("fixed UI and independent acceptance review full SHAs required");
var uiFiles = Hashes(dist, true); var uiHash = TreeHash(uiFiles);
var expectedHash = Option("--dist-hash", "");
if (expectedHash.Length > 0 && expectedHash != uiHash || mode == "publish" && expectedHash.Length != 64) throw new InvalidDataException("UI_HASH_MISMATCH or missing accepted dist hash");
var validation = WebProgressDataValidation.Validate(data);
if (validation["errors"]!.AsArray().Count > 0) throw new InvalidDataException(validation.ToJsonString());
var dataFiles = Hashes(data);
if (dataFiles.Keys.Any(p => Path.GetExtension(p) is not (".json" or ".log"))) throw new InvalidDataException("data allowlist");
var fingerprint = uiCommit + ":" + uiHash + ":" + TreeHash(dataFiles);
var candidate = Path.Combine(work, "bundle-" + HashHex(Encoding.UTF8.GetBytes(fingerprint))[..16]);
if (!Directory.Exists(candidate)) { Copy(dist, candidate, uiFiles); Copy(data, Path.Combine(candidate, "data"), dataFiles); }
SafeTree(candidate);
var expectedFiles = new Dictionary<string, string>(uiFiles, StringComparer.Ordinal);
foreach (var pair in dataFiles) expectedFiles.Add("data/" + pair.Key, pair.Value);
if (TreeHash(Hashes(candidate)) != TreeHash(expectedFiles)) throw new IOException("candidate incomplete/changed; preserve evidence and choose a new --work");
if (TreeHash(Hashes(data)) != TreeHash(dataFiles) || TreeHash(Hashes(dist, true)) != uiHash) throw new IOException("UI_SNAPSHOT_CHANGED reprepare from a completed producer snapshot");
var snapshotValidation = WebProgressDataValidation.Validate(Path.Combine(candidate, "data"));
if (snapshotValidation["errors"]!.AsArray().Count > 0) throw new InvalidDataException("UI_SNAPSHOT_CLOSURE_INVALID " + snapshotValidation.ToJsonString());
var files = Directory.GetFiles(candidate, "*", SearchOption.AllDirectories);
var bytes = files.Sum(f => new FileInfo(f).Length);
if (files.Length > 5000 || bytes > 30L * 1024 * 1024) throw new InvalidDataException("UI_BUNDLE_CAPACITY files=" + files.Length + " bytes=" + bytes + "; preserve current bundle, no evidence pruning");
var metadataPath = Path.Combine(work, "prepared.json");
WriteAtomic(metadataPath, new JsonObject { ["schemaVersion"] = 1, ["uiCommit"] = uiCommit, ["uiHash"] = uiHash, ["review"] = review, ["fingerprint"] = fingerprint, ["bundle"] = Path.GetRelativePath(work, candidate), ["files"] = files.Length, ["bytes"] = bytes, ["dataIndexSha256"] = dataFiles["index.json"] });
Console.WriteLine("UI_PREPARED ui=" + uiCommit + " uiHash=" + uiHash + " files=" + files.Length + " bytes=" + bytes + " dataIndexSha=" + dataFiles["index.json"]);
if (mode == "prepare") return 0;
var published = ReadObject(Path.Combine(work, "published.json"));
if (published?["fingerprint"]?.GetValue<string>() == fingerprint) { Console.WriteLine("UI_SKIP_SAME includes UI identity"); return 0; }
if (!Directory.Exists(backup))
{
    SafeTree(previous); Separate(work, previous);
    using var http = new HttpClient();
    foreach (var path in new[] { "index.html", "data/index.json" })
        if (!Enumerable.SequenceEqual(File.ReadAllBytes(Path.Combine(previous, path)), await http.GetByteArrayAsync("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/" + path))) throw new IOException("previous bundle does not match served slug");
    var previousFiles = Hashes(previous); Copy(previous, backup, previousFiles);
    if (TreeHash(Hashes(previous)) != TreeHash(previousFiles) || TreeHash(Hashes(backup)) != TreeHash(previousFiles)) throw new IOException("previous bundle changed; reprepare backup");
    WriteAtomic(Path.Combine(work, "previous.json"), new JsonObject { ["schemaVersion"] = 1, ["treeHash"] = TreeHash(previousFiles) });
}
if (TreeHash(Hashes(data)) != TreeHash(dataFiles)) throw new IOException("UI_SNAPSHOT_CHANGED reprepare before publication");
await Publish(candidate);
WriteAtomic(Path.Combine(work, "published.json"), new JsonObject { ["schemaVersion"] = 1, ["fingerprint"] = fingerprint, ["uiCommit"] = uiCommit, ["uiHash"] = uiHash, ["review"] = review });
return 0;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
