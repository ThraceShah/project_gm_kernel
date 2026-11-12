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
var packed = new Dictionary<string, string>(StringComparer.Ordinal);
var bytesRaw = 0L;
var bytesGz = 0L;

void PackReference(JsonObject side, string key)
{
    var reference = side[key];
    if (reference is not JsonValue value || !value.TryGetValue<string>(out var relative) || relative.Length == 0) return;
    if (relative.EndsWith(".gz", StringComparison.Ordinal)) return;
    // The same file can be referenced twice (after + afterBodies); pack it once
    // and rewrite the later reference from the binding map.
    if (packed.TryGetValue(relative, out var alreadyPacked)) { side[key] = alreadyPacked; return; }
    var source = Path.GetFullPath(relative, runDir);
    if (!source.StartsWith(Path.GetFullPath(runDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(source))
        throw new IOException("mesh reference is missing or escapes the run: " + relative);
    var target = source + ".gz";
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
    packed[relative] = relative + ".gz";
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

// Evidence/log transport packing: raw capture, compare and oracle logs are kept
// complete but stored as standard gzip members when they exceed the threshold.
// Every reference in the manifest is rewritten, so no evidence silently vanishes
// and the uncompressed SHA is still recorded.
var evidenceThreshold = long.Parse(Option("--evidence-threshold", "0"), System.Globalization.CultureInfo.InvariantCulture);
var evidencePacked = new Dictionary<string, string>(StringComparer.Ordinal);
if (evidenceThreshold > 0)
{
    foreach (var testCase in manifest["cases"]!.AsArray().OfType<JsonObject>())
    {
        var references = new List<JsonObject>();
        foreach (var evidence in testCase["evidence"]?.AsArray() ?? new JsonArray()) if (evidence is JsonObject o) references.Add(o);
        foreach (var check in testCase["checks"]?.AsArray() ?? new JsonArray()) if (check is JsonObject o && o["evidence"] != null) references.Add(o);
        foreach (var holder in references)
        {
            var key = holder.ContainsKey("path") ? "path" : "evidence";
            if (holder[key] is not JsonValue value || !value.TryGetValue<string>(out var relative) || relative.EndsWith(".gz", StringComparison.Ordinal)) continue;
            // A file shared by several cases/checks is packed once; later references
            // are rewritten from the binding map because the original is already gone.
            if (evidencePacked.TryGetValue(relative, out var alreadyPacked)) { holder[key] = alreadyPacked; continue; }
            var source = Path.GetFullPath(relative, runDir);
            if (!source.StartsWith(Path.GetFullPath(runDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(source)) continue;
            var info = new FileInfo(source);
            if (info.Length < evidenceThreshold) continue;
            if (info.Extension is not (".json" or ".log")) continue;
            if (!evidencePacked.TryGetValue(relative, out var packedPath))
            {
                var raw = File.ReadAllBytes(source);
                using (var stream = new FileStream(source + ".gz", FileMode.Create, FileAccess.Write, FileShare.None))
                using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: false))
                    gzip.Write(raw);
                using (var check = new GZipStream(File.OpenRead(source + ".gz"), CompressionMode.Decompress))
                using (var buffer = new MemoryStream())
                {
                    check.CopyTo(buffer);
                    if (!buffer.ToArray().AsSpan().SequenceEqual(raw)) throw new IOException("gzip round trip mismatch: " + relative);
                }
                records.Add((JsonNode)new JsonObject
                {
                    ["path"] = relative + ".gz",
                    ["uncompressedPath"] = relative,
                    ["encoding"] = "gzip",
                    ["kind"] = "evidence",
                    ["uncompressedBytes"] = raw.Length,
                    ["uncompressedSha256"] = Convert.ToHexStringLower(SHA256.HashData(raw)),
                    ["compressedBytes"] = new FileInfo(source + ".gz").Length,
                    ["compressedSha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source + ".gz"))),
                });
                bytesRaw += raw.Length;
                bytesGz += new FileInfo(source + ".gz").Length;
                File.Delete(source);
                packedPath = relative + ".gz";
                evidencePacked[relative] = packedPath;
            }
            holder[key] = packedPath;
        }
    }
}

// The rewritten manifest must be on disk before it can be packed.
File.WriteAllText(manifestPath + ".tmp", manifest.ToJsonString());
File.Move(manifestPath + ".tmp", manifestPath, true);

// Optional manifest transport packing: the manifest is the largest single
// artifact and is fetched by every client, so it is stored as a gzip member with
// the index descriptor pointing at it explicitly.
if (args.Contains("--pack-manifest"))
{
    var raw = File.ReadAllBytes(manifestPath);
    using (var stream = new FileStream(manifestPath + ".gz", FileMode.Create, FileAccess.Write, FileShare.None))
    using (var gzip = new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: false))
        gzip.Write(raw);
    using (var check = new GZipStream(File.OpenRead(manifestPath + ".gz"), CompressionMode.Decompress))
    using (var buffer = new MemoryStream())
    {
        check.CopyTo(buffer);
        if (!buffer.ToArray().AsSpan().SequenceEqual(raw)) throw new IOException("gzip round trip mismatch: manifest");
    }
    records.Add((JsonNode)new JsonObject
    {
        ["path"] = "manifest.json.gz",
        ["uncompressedPath"] = "manifest.json",
        ["encoding"] = "gzip",
        ["kind"] = "manifest",
        ["uncompressedBytes"] = raw.Length,
        ["uncompressedSha256"] = Convert.ToHexStringLower(SHA256.HashData(raw)),
        ["compressedBytes"] = new FileInfo(manifestPath + ".gz").Length,
        ["compressedSha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(manifestPath + ".gz"))),
    });
    bytesRaw += raw.Length;
    bytesGz += new FileInfo(manifestPath + ".gz").Length;
    File.Delete(manifestPath);
    var dataRoot = Path.GetDirectoryName(Path.GetDirectoryName(runDir))!;
    var indexPath = Path.Combine(dataRoot, "index.json");
    var index = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
    var runId = manifest["run"]!["id"]!.GetValue<string>();
    foreach (var descriptor in index["runs"]!.AsArray().OfType<JsonObject>())
        if (descriptor["id"]?.GetValue<string>() == runId) descriptor["manifest"] = $"runs/{Path.GetFileName(runDir)}/manifest.json.gz";
    File.WriteAllText(indexPath + ".tmp", index.ToJsonString());
    File.Move(indexPath + ".tmp", indexPath, true);
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
// Incremental runs keep the earlier bindings instead of dropping them; totals
// are recomputed from the merged member list.
if (File.Exists(reportPath))
{
    var existingMembers = JsonNode.Parse(File.ReadAllText(reportPath))!["members"]!.AsArray();
    var known = existingMembers.OfType<JsonObject>().Select(m => m["path"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
    foreach (var member in records.OfType<JsonObject>())
    {
        if (!known.Add(member["path"]!.GetValue<string>())) continue;
        existingMembers.Add(member.DeepClone());
    }
    report["members"] = existingMembers.DeepClone();
}
var membersFinal = report["members"]!.AsArray().OfType<JsonObject>().ToArray();
report["files"] = membersFinal.Length;
report["uncompressedBytes"] = membersFinal.Sum(m => m["uncompressedBytes"]!.GetValue<long>());
report["compressedBytes"] = membersFinal.Sum(m => m["compressedBytes"]!.GetValue<long>());
File.WriteAllText(reportPath + ".tmp", report.ToJsonString());
File.Move(reportPath + ".tmp", reportPath, true);

Console.WriteLine($"PACKED files={records.Count} raw={bytesRaw} gz={bytesGz} ratio={(bytesGz == 0 ? 0 : (double)bytesRaw / bytesGz):F2}");
return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
