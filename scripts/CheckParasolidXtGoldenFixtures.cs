#!/usr/bin/env dotnet run

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

var root = FindRoot(Directory.GetCurrentDirectory());
var fixtureRoot = Path.Combine(root, "tests", "ParasolidXtCorpus", "Fixtures");
var reportPath = Path.Combine(root, "tests", "ParasolidXtCorpus", "coverage", "golden-fixture-report.json");
var required = new[]
{
    "body.solid.block.typical",
    "body.solid.sphere.typical",
    "analytic-surface-plane",
    "topology.face.inner-loop.block-cylinder-hole",
    "blend.fxf.bsurf-plane.rolling-ball",
    "assembly.single-block-identity-instance",
    "attribute.face.named-integer",
    "user-field.body.single-slot",
};
var fixtures = new List<FixtureRecord>(required.Length);
foreach (var id in required)
{
    var directory = Path.Combine(fixtureRoot, id);
    var model = Path.Combine(directory, "model.x_t");
    var manifest = Path.Combine(directory, "manifest.json");
    var diagnostics = Path.Combine(directory, "diagnostics.json");
    if (!File.Exists(model) || !File.Exists(manifest) || !File.Exists(diagnostics))
    {
        Console.Error.WriteLine("golden fixture is incomplete: " + id);
        return 1;
    }
    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
    var caseId = document.RootElement.TryGetProperty("caseId", out var value) ? value.GetString() : null;
    if (!string.Equals(caseId, id, StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"golden manifest caseId mismatch: {id} -> {caseId}");
        return 1;
    }
    fixtures.Add(new FixtureRecord(id, Hash(model), Hash(manifest), Hash(diagnostics), new FileInfo(model).Length));
}

fixtures.Sort((a, b) => string.CompareOrdinal(a.CaseId, b.CaseId));
var report = new GoldenReport("parasolid-xt-golden-v1", fixtures.ToArray());
var bytes = JsonSerializer.SerializeToUtf8Bytes(report, GoldenJsonContext.Default.GoldenReport);
if (args.Contains("--check", StringComparer.Ordinal))
{
    if (!File.Exists(reportPath) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(reportPath)))
    {
        Console.Error.WriteLine("golden fixture report is missing or out of date: " + Path.GetRelativePath(root, reportPath));
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllBytes(reportPath, bytes);
}

Console.WriteLine($"golden fixtures={fixtures.Count}");
return 0;

static string Hash(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string FindRoot(string start)
{
    var current = Path.GetFullPath(start);
    while (!File.Exists(Path.Combine(current, "AGENTS.md")) && Directory.GetParent(current) is { } parent)
        current = parent.FullName;
    return current;
}

sealed record GoldenReport(string Generator, FixtureRecord[] Fixtures);
sealed record FixtureRecord(string CaseId, string ModelSha256, string ManifestSha256, string DiagnosticsSha256, long ModelBytes);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GoldenReport))]
internal partial class GoldenJsonContext : JsonSerializerContext
{
}
