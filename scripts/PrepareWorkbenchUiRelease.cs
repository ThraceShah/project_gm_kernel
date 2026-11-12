#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Stage a new frozen owned UI release directory: ui/ is the freshly built dist,
// tools/ is the current workbench script set, previous-static/ is the exact
// served bundle used as the rollback target, and release.json pins the full
// content manifest. Everything is made read-only, matching
// ReviewedWebProgressRelease.Frozen. No old release bytes are reused.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var ui = Path.GetFullPath(Option("--ui", "../web/dist"), scripts);
var tools = Path.GetFullPath(Option("--tools", "."), scripts);
var served = Path.GetFullPath(Option("--served-bundle", ""), scripts);
var output = Path.GetFullPath(Option("--output", ""), scripts);
var uiCommit = Option("--ui-commit", "");
var toolsCommit = Option("--tools-commit", "");
var acceptance = Option("--acceptance", "");
if (!Directory.Exists(ui) || !Directory.Exists(tools) || !Directory.Exists(served)) throw new ArgumentException("--ui, --tools and --served-bundle must exist");
if (Directory.Exists(output)) throw new IOException("output release dir already exists");
if (uiCommit.Length != 40 || !uiCommit.All(char.IsAsciiHexDigit)) throw new ArgumentException("--ui-commit must be a full 40-hex commit");
if (toolsCommit.Length != 40 || !toolsCommit.All(char.IsAsciiHexDigit)) throw new ArgumentException("--tools-commit must be a full 40-hex commit");
if (acceptance.Length != 40 || !acceptance.All(char.IsAsciiHexDigit)) throw new ArgumentException("--acceptance must be a full 40-hex self-test commit");
string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

// The frozen UI is the built static bundle only (never node_modules/sources).
foreach (var entry in new[] { "index.html", "assets" })
    if (!File.Exists(Path.Combine(ui, entry)) && !Directory.Exists(Path.Combine(ui, entry))) throw new InvalidDataException("built dist is missing " + entry);
Copy(Path.Combine(ui, "index.html"), Path.Combine(output, "ui", "index.html"));
CopyTree(Path.Combine(ui, "assets"), Path.Combine(output, "ui", "assets"));
if (Directory.Exists(Path.Combine(ui, "display"))) CopyTree(Path.Combine(ui, "display"), Path.Combine(output, "ui", "display"));
CopyTree(tools, Path.Combine(output, "tools", "scripts"), new[] { ".cs", ".props" });
CopyTree(served, Path.Combine(output, "previous-static"));

var files = new JsonObject();
foreach (var dir in new[] { "tools", "ui", "previous-static" })
    foreach (var entry in Directory.GetFiles(Path.Combine(output, dir), "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        files[dir + "/" + Path.GetRelativePath(Path.Combine(output, dir), entry).Replace('\\', '/')] = Sha(File.ReadAllBytes(entry));

string TreeHash(string dir)
{
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (var entry in Directory.GetFiles(Path.Combine(output, dir), "*", SearchOption.AllDirectories)
                 .Select(f => (Relative: Path.GetRelativePath(Path.Combine(output, dir), f).Replace('\\', '/'), File: f))
                 .OrderBy(p => p.Relative, StringComparer.Ordinal))
    { hash.AppendData(Encoding.UTF8.GetBytes(entry.Relative)); hash.AppendData(Encoding.UTF8.GetBytes(Sha(File.ReadAllBytes(entry.File)))); }
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}

var uiHash = TreeHash("ui");
var oldBundleHash = TreeHash("previous-static");
var release = new JsonObject
{
    ["schemaVersion"] = 1,
    ["toolsCommit"] = toolsCommit,
    ["uiCommit"] = uiCommit,
    ["review"] = acceptance,
    ["acceptanceScope"] = "Owner self-test release record; the user cancelled independent board reviews. This field pins the self-test commit, it is not an independent review.",
    ["uiHash"] = uiHash,
    ["oldBundleHash"] = oldBundleHash,
    ["files"] = files,
};
var releasePath = Path.Combine(output, "release.json");
File.WriteAllText(releasePath, release.ToJsonString());
Directory.CreateDirectory(Path.Combine(output, "work"));
foreach (var root in new[] { "tools", "ui", "previous-static" })
{
    var path = Path.Combine(output, root);
    foreach (var entry in Directory.GetFiles(path, "*", SearchOption.AllDirectories).Prepend(path).Concat(Directory.GetDirectories(path, "*", SearchOption.AllDirectories)))
        File.SetUnixFileMode(entry, File.GetUnixFileMode(entry) & ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
}
File.SetUnixFileMode(releasePath, File.GetUnixFileMode(releasePath) & ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
Console.WriteLine("RELEASE " + output);
Console.WriteLine("uiHash " + uiHash);
Console.WriteLine("oldBundleHash " + oldBundleHash);
Console.WriteLine("files " + files.Count);
Console.WriteLine("releaseSha256 " + Sha(File.ReadAllBytes(releasePath)));
return 0;

void Copy(string source, string target)
{
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(source, target, false);
}
void CopyTree(string source, string target, string[]? extensions = null)
{
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        if (extensions != null && !extensions.Contains(Path.GetExtension(file), StringComparer.Ordinal)) continue;
        var relative = Path.GetRelativePath(source, file);
        var to = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(file, to, false);
    }
}
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
