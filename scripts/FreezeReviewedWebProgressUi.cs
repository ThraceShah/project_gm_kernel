#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Only whitelisted tool sources from an exact Git object, the exact accepted
// UI bytes, and the exact previous public bundle enter the release. No keys,
// environment, .git, node_modules or private dependencies are copied.
if (args.Contains("--help")) { Console.WriteLine("FreezeReviewedWebProgressUi --tools-commit <40hex> --ui-commit <40hex> --review <40hex> --dist-hash <64hex> --previous-tree-hash <64hex> --ui-tree <relative> --previous-bundle <relative current served bundle> --release <new relative directory>"); return 0; }
var scripts = Path.GetDirectoryName(ScriptPath())!; var root = Path.GetFullPath("..", scripts);
string Fixed(string name, int length) { var value = Option(name, ""); if (value.Length != length || !value.All(char.IsAsciiHexDigit)) throw new ArgumentException(name + " requires fixed reviewed identity"); return value; }
var uiCommit = Fixed("--ui-commit", 40); var review = Fixed("--review", 40);
var uiHash = Fixed("--dist-hash", 64); var oldHash = Fixed("--previous-tree-hash", 64);
string Resolve(string name) { var value = Option(name, ""); if (value.Length == 0 || Path.IsPathRooted(value)) throw new ArgumentException(name + " requires script-relative path"); return Path.GetFullPath(value, scripts); }
void Safe(string path) { if (!WebProgressSafePath.IsSafeChain(path, out _)) throw new IOException("unsafe release path"); if (Directory.Exists(path)) foreach (var f in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)) if ((File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0) throw new IOException("unsafe release source"); }
var uiTree = Resolve("--ui-tree"); var previous = Resolve("--previous-bundle"); var release = Resolve("--release");
var toolsCommit = Option("--tools-commit", ""); if (toolsCommit.Length != 40 || !toolsCommit.All(char.IsAsciiHexDigit)) throw new ArgumentException("exact tools SHA required");
Safe(Path.Combine(uiTree, "web", "dist")); Safe(previous); Safe(release);
if (Directory.Exists(release)) throw new IOException("release already exists; preserve immutable artifacts");
foreach (var source in new[] { uiTree, previous }) if (WebProgressSafePath.IsWithin(source, release) || WebProgressSafePath.IsWithin(release, source)) throw new IOException("overlapping release paths");
async Task<byte[]> Git(params string[] arguments)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var p = Process.Start(info)!; using var output = new MemoryStream(); var read = p.StandardOutput.BaseStream.CopyToAsync(output); var error = p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync(); await read;
    if (p.ExitCode != 0) throw new IOException("git source check failed: " + await error); return output.ToArray();
}
if (Encoding.UTF8.GetString(await Git("-C", uiTree, "rev-parse", "HEAD")).Trim() != uiCommit || Encoding.UTF8.GetString(await Git("-C", uiTree, "status", "--porcelain", "--untracked-files=no", "--", "web")).Trim().Length != 0) throw new IOException("accepted UI source checkout drifted");
string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Dictionary<string, string> Hashes(string dir, bool uiOnly = false) => Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(f => (File: f, Relative: Path.GetRelativePath(dir, f).Replace('\\', '/'))).Where(p => !uiOnly || !p.Relative.StartsWith("data/", StringComparison.Ordinal)).ToDictionary(p => p.Relative, p => Sha(File.ReadAllBytes(p.File)), StringComparer.Ordinal);
string TreeHash(Dictionary<string, string> hashes) { using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); foreach (var (path, sha) in hashes.OrderBy(p => p.Key, StringComparer.Ordinal)) { h.AppendData(Encoding.UTF8.GetBytes(path)); h.AppendData(Encoding.UTF8.GetBytes(sha)); } return Convert.ToHexStringLower(h.GetHashAndReset()); }
var dist = Path.Combine(uiTree, "web", "dist"); var uiFiles = Hashes(dist, true); var oldFiles = Hashes(previous);
if (TreeHash(uiFiles) != uiHash || TreeHash(oldFiles) != oldHash) throw new IOException("accepted UI or exact old bundle hash mismatch");
var staging = release + ".staging-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(staging);
var files = new JsonObject();
void Put(string relative, byte[] bytes) { var target = Path.Combine(staging, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, bytes); files[relative] = Sha(bytes); }
foreach (var file in new[] { "Directory.Build.props", "PublishReviewedWebProgressUi.cs", "ConfigureReviewedWebProgressUiHook.cs", "ReviewedWebProgressRelease.cs", "WebProgressDataValidation.cs", "WebProgressPublishing.cs", "WebProgressMetadata.cs" }) Put("tools/scripts/" + file, await Git("show", toolsCommit + ":scripts/" + file));
foreach (var (path, sha) in uiFiles) { var bytes = File.ReadAllBytes(Path.Combine(dist, path)); if (Sha(bytes) != sha) throw new IOException("UI dist drift during freeze"); Put("ui/" + path, bytes); }
foreach (var (path, sha) in oldFiles) { var bytes = File.ReadAllBytes(Path.Combine(previous, path)); if (Sha(bytes) != sha) throw new IOException("previous bundle drift during freeze"); Put("previous-static/" + path, bytes); }
if (TreeHash(Hashes(dist, true)) != uiHash || TreeHash(Hashes(previous)) != oldHash) throw new IOException("source changed; staging evidence retained");
File.WriteAllText(Path.Combine(staging, "release.json"), new JsonObject { ["schemaVersion"] = 1, ["toolsCommit"] = toolsCommit, ["uiCommit"] = uiCommit, ["review"] = review, ["uiHash"] = uiHash, ["oldBundleHash"] = oldHash, ["files"] = files }.ToJsonString());
File.SetUnixFileMode(Path.Combine(staging, "release.json"), UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
foreach (var dir in new[] { "tools", "ui", "previous-static" })
{
    var frozen = Path.Combine(staging, dir);
    foreach (var file in Directory.GetFiles(frozen, "*", SearchOption.AllDirectories)) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    foreach (var path in Directory.GetDirectories(frozen, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).Prepend(frozen)) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
}
Directory.Move(staging, release);
Console.WriteLine("UI_RELEASE_FROZEN " + Path.GetRelativePath(root, release) + " tools=" + toolsCommit + " ui=" + uiCommit + " uiHash=" + uiHash + " oldBundle=" + oldHash);
return 0;
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : args[at + 1]; }
static string ScriptPath([CallerFilePath] string path = "") => path;
