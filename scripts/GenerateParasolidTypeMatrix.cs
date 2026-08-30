#!/usr/bin/env dotnet run

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root = FindRoot(Directory.GetCurrentDirectory());
var matrixPath = Path.Combine(root, "tests", "ParasolidXtCorpus", "coverage", "type-matrix.json");
var apiManifestPath = Path.Combine(root, "tests", "ParasolidXtCorpus", "coverage", "generated", "manifest.json");
var schemaPath = Path.Combine(root, "third_party", "parasolid", "schema", "sch_37102.sch_txt");
var outputPath = Path.Combine(root, "tests", "ParasolidXtCorpus", "coverage", "generated", "type-matrix-inputs.json");
if (!File.Exists(matrixPath) || !File.Exists(apiManifestPath) || !File.Exists(schemaPath))
{
    Console.Error.WriteLine("type matrix inputs are missing");
    return 2;
}

using var matrix = JsonDocument.Parse(File.ReadAllText(matrixPath));
using var apiManifest = JsonDocument.Parse(File.ReadAllText(apiManifestPath));
var apiNames = apiManifest.RootElement.GetProperty("apis").EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "").ToHashSet(StringComparer.Ordinal);
var tokenNames = apiManifest.RootElement.GetProperty("tokens").EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "").ToHashSet(StringComparer.Ordinal);
var typeNames = apiManifest.RootElement.GetProperty("types").EnumerateArray().Select(x => x.GetProperty("name").GetString() ?? "").ToHashSet(StringComparer.Ordinal);
var required = ReadIds(matrix.RootElement, "required");
var excluded = ReadIds(matrix.RootElement, "excluded");
var rejected = ReadIds(matrix.RootElement, "rejected");
var normalized = ReadIds(matrix.RootElement, "normalized");
var deferred = ReadIds(matrix.RootElement, "deferred");
var schemaText = File.ReadAllText(schemaPath);
var sources = matrix.RootElement.GetProperty("required").EnumerateArray()
    .Concat(matrix.RootElement.GetProperty("excluded").EnumerateArray())
    .Concat(matrix.RootElement.GetProperty("rejected").EnumerateArray())
    .Concat(matrix.RootElement.GetProperty("normalized").EnumerateArray())
    .Where(x => x.TryGetProperty("source", out _))
    .Select(x => x.GetProperty("source").GetString() ?? "")
    .Where(static x => x.Length != 0)
    .Distinct(StringComparer.Ordinal)
    .Order(StringComparer.Ordinal)
    .ToArray();
var knownSourceNames = apiNames.Concat(tokenNames).Concat(typeNames).ToHashSet(StringComparer.Ordinal);
var unresolvedSources = sources.Where(source =>
{
    if (!source.StartsWith("PK_", StringComparison.Ordinal)) return false;
    var baseName = source.Split(new[] { '.', '(', ' ' }, 2)[0];
    return !knownSourceNames.Contains(baseName) && !schemaText.Contains(baseName, StringComparison.Ordinal);
}).Order(StringComparer.Ordinal).ToArray();

using var output = new MemoryStream();
using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
{
    writer.WriteStartObject();
    writer.WriteString("generator", "parasolid-type-matrix-inputs-v1");
    writer.WriteString("apiManifestSha256", Hash(apiManifestPath));
    writer.WriteString("schemaSha256", Hash(schemaPath));
    writer.WriteString("matrixSha256", Hash(matrixPath));
    writer.WriteNumber("requiredCount", required.Length);
    writer.WriteNumber("excludedCount", excluded.Length);
    writer.WriteNumber("rejectedCount", rejected.Length);
    writer.WriteNumber("normalizedCount", normalized.Length);
    writer.WriteNumber("deferredCount", deferred.Length);
    writer.WriteNumber("sourceCount", sources.Length);
    writer.WriteStartArray("unresolvedSources");
    foreach (var source in unresolvedSources) writer.WriteStringValue(source);
    writer.WriteEndArray();
    writer.WriteEndObject();
}
var bytes = output.ToArray();
if (args.Contains("--check", StringComparer.Ordinal))
{
    if (!File.Exists(outputPath) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(outputPath)))
    {
        Console.Error.WriteLine("generated type matrix input report is missing or out of date");
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllBytes(outputPath, bytes);
}

Console.WriteLine($"type matrix inputs required={required.Length} sources={sources.Length} unresolved={unresolvedSources.Length}");
return unresolvedSources.Length == 0 ? 0 : 1;

static string[] ReadIds(JsonElement root, string property) => root.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
    ? values.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : value.TryGetProperty("id", out var id) ? id.GetString() : null).Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
    : [];

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
