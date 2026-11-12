#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Lossless transport packing for the static workbench bundle.
//
// Every display mesh referenced by a run manifest is stored as a standard gzip
// member (`<path>.gz`) that decompresses to the exact original bytes; the step
// side is marked with `"encoding":"gzip"` so the current consumer contract is
// explicit and a missing/mismatched member fails instead of showing fake data.
// This is transport encoding only: geometry, roles, IDs, tolerances and source
// identity are untouched, and the uncompressed SHA-256 is recorded so the
// binding to the producer bytes is auditable.
//
// Usage (paths resolve against this script's directory):
//   dotnet run --file scripts/PackWebProgressMeshes.cs -- --run-dir ../temp_docs/web-progress/runs/<id>
var scripts = Path.GetDirectoryName(ScriptPath())!;
var runDir = Path.GetFullPath(Option("--run-dir", ""), scripts);
if (!File.Exists(Path.Combine(runDir, "manifest.json"))) throw new ArgumentException("--run-dir must contain manifest.json");
var manifestPath = Path.Combine(runDir, "manifest.json");
var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
var records = new JsonArray();
var packed = new HashSet<string>(StringComparer.Ordinal);
var bytesRaw = 0L;
var bytesGz = 0L;

void PackReference(JsonObject side, string key)
{
    var reference = side[key];
    if (reference is not JsonValue value || !value.TryGetValue<string>(out var relative) || relative.Length == 0) return;
    if (relative.EndsWith(".gz", StringComparison.Ordinal)) return;
    var source = Path.GetFullPath(relative, runDir);
    if (!source.StartsWith(Path.GetFullPath(runDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(source))
        throw new IOException("mesh reference is missing or escapes the run: " + relative);
    var target = source + ".gz";
    if (!packed.Add(relative)) return;
    var raw = File.ReadAllBytes(source);
    using (var stream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
    using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: false))
        gzip.Write(raw);
    // Verify the round trip before the uncompressed member is dropped.
    using (var check = new GZipStream(File.OpenRead(target), CompressionMode.Decompress))
    using (var buffer = new MemoryStream())
    {
        check.CopyTo(buffer);
        if (!buffer.ToArray().AsSpan().SequenceEqual(raw)) throw new IOException("gzip round trip mismatch: " + relative);
    }
    var gzBytes = new FileInfo(target).Length;
    records.Add((JsonNode)new JsonObject
    {
        ["path"] = relative + ".gz",
        ["uncompressedPath"] = relative,
        ["encoding"] = "gzip",
        ["uncompressedBytes"] = raw.Length,
        ["uncompressedSha256"] = Convert.ToHexStringLower(SHA256.HashData(raw)),
        ["compressedBytes"] = gzBytes,
        ["compressedSha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(target))),
    });
    bytesRaw += raw.Length;
    bytesGz += gzBytes;
    File.Delete(source);
    side[key] = relative + ".gz";
}

foreach (var testCase in manifest["cases"]!.AsArray().OfType<JsonObject>())
{
    foreach (var step in testCase["steps"]!.AsArray().OfType<JsonObject>())
    {
        foreach (var name in new[] { "gm", "pk" })
        {
            var side = step[name]!.AsObject();
            var before = false;
            foreach (var key in new[] { "before", "after" }) { PackReference(side, key); before |= side[key] != null; }
            foreach (var key in new[] { "tools", "afterBodies" })
            {
                if (side[key] is not JsonArray paths) continue;
                for (var i = 0; i < paths.Count; i++)
                {
                    var holder = new JsonObject { ["value"] = paths[i]!.GetValue<string>() };
                    PackReference(holder, "value");
                    paths[i] = holder["value"]!.GetValue<string>();
                }
            }
            if (before) side["encoding"] = "gzip";
        }
    }
}

var report = new JsonObject
{
    ["schemaVersion"] = 1,
    ["type"] = "lossless-gzip-transport-binding",
    ["run"] = manifest["run"]?["id"]?.DeepClone(),
    ["files"] = records.Count,
    ["uncompressedBytes"] = bytesRaw,
    ["compressedBytes"] = bytesGz,
    ["members"] = records,
};
var reportPath = Path.Combine(runDir, "mesh-transport.json");
File.WriteAllText(reportPath + ".tmp", report.ToJsonString());
File.Move(reportPath + ".tmp", reportPath, true);
File.WriteAllText(manifestPath + ".tmp", manifest.ToJsonString());
File.Move(manifestPath + ".tmp", manifestPath, true);
Console.WriteLine($"PACKED files={records.Count} raw={bytesRaw} gz={bytesGz} ratio={(bytesGz == 0 ? 0 : (double)bytesRaw / bytesGz):F2}");
return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
