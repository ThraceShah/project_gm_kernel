#!/usr/bin/env dotnet run

using System.Text.Json;

var root = FindRoot(Directory.GetCurrentDirectory());
var corpusRoot = Path.Combine(root, "bin", "parasolid-xt-corpus");
var outputPath = Path.Combine(root, "tests", "ParasolidXtCorpus", "coverage", "schema-dependency-report.json");
var failures = new List<string>();
var checkedCases = 0;
var scriptRoot = Path.Combine(root, "scripts", "ParasolidXtCorpusCases");
var canonicalGroups = Directory.Exists(scriptRoot)
    ? Directory.EnumerateFiles(scriptRoot, "*.cs").Where(path => !string.Equals(Path.GetFileNameWithoutExtension(path), "TemplateSmoke", StringComparison.Ordinal)).Select(path => Path.GetFileNameWithoutExtension(path)!).Order(StringComparer.Ordinal).ToArray()
    : [];
foreach (var manifestPath in canonicalGroups.SelectMany(group =>
    Directory.Exists(Path.Combine(corpusRoot, group))
        ? Directory.EnumerateFiles(Path.Combine(corpusRoot, group), "manifest.json", SearchOption.AllDirectories)
        : Enumerable.Empty<string>()).Order(StringComparer.Ordinal))
{
    using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
    var rootElement = document.RootElement;
    checkedCases++;
    var caseId = rootElement.TryGetProperty("caseId", out var id) ? id.GetString() ?? manifestPath : manifestPath;
    var nodes = rootElement.TryGetProperty("schemaNodes", out var nodeValues) && nodeValues.ValueKind == JsonValueKind.Array
        ? nodeValues.EnumerateArray().Select(x => x.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "").ToHashSet(StringComparer.Ordinal)
        : [];
    var edges = rootElement.TryGetProperty("schemaDependencies", out var edgeValues) && edgeValues.ValueKind == JsonValueKind.Array
        ? edgeValues.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String
            ? x.GetString() ?? ""
            : x.TryGetProperty("from", out var from) && x.TryGetProperty("to", out var to)
                ? (from.GetString() ?? "") + "->" + (to.GetString() ?? "")
                : "").ToHashSet(StringComparer.Ordinal)
        : [];
    if (rootElement.TryGetProperty("requiredSchemaNodes", out var requiredNodes) && requiredNodes.ValueKind == JsonValueKind.Array)
        foreach (var required in requiredNodes.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length != 0))
            if (!nodes.Contains(required)) failures.Add(caseId + ":node:" + required);
    if (rootElement.TryGetProperty("requiredSchemaDependencies", out var requiredEdges) && requiredEdges.ValueKind == JsonValueKind.Array)
        foreach (var required in requiredEdges.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length != 0))
        {
            var separator = required.IndexOf("->", StringComparison.Ordinal);
            var from = separator > 0 ? required[..separator] : required;
            var to = separator > 0 ? required[(separator + 2)..] : "";
            var found = separator > 0
                ? edges.Any(edge => edge.StartsWith(from + "#", StringComparison.Ordinal) && edge.Contains("->" + to + "#", StringComparison.Ordinal))
                : edges.Contains(required);
            if (!found) failures.Add(caseId + ":edge:" + required);
        }
}

using var output = new MemoryStream();
using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
{
    writer.WriteStartObject();
    writer.WriteString("generator", "parasolid-schema-dependency-v1");
    writer.WriteNumber("caseCount", checkedCases);
    writer.WriteNumber("failureCount", failures.Count);
    writer.WriteStartArray("failures");
    foreach (var failure in failures.Order(StringComparer.Ordinal)) writer.WriteStringValue(failure);
    writer.WriteEndArray();
    writer.WriteEndObject();
}
var bytes = output.ToArray();
if (args.Contains("--check", StringComparer.Ordinal))
{
    if (!File.Exists(outputPath) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(outputPath)))
    {
        Console.Error.WriteLine("schema dependency report is missing or out of date");
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllBytes(outputPath, bytes);
}
Console.WriteLine($"schema dependencies cases={checkedCases} failures={failures.Count}");
return failures.Count == 0 ? 0 : 1;

static string FindRoot(string start)
{
    var current = Path.GetFullPath(start);
    while (!File.Exists(Path.Combine(current, "AGENTS.md")) && Directory.GetParent(current) is { } parent)
        current = parent.FullName;
    return current;
}
