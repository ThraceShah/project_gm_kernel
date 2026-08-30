#!/usr/bin/env dotnet run
#:property AssemblyName=CheckParasolidTypeCoverage

using System.Text.Json;

var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
var matrixPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "type-matrix.json");
var corpusRoot = Path.Combine(repositoryRoot, "bin", "parasolid-xt-corpus");
var reportPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "type-coverage-report.json");
var check = args.Contains("--check", StringComparer.Ordinal);
var strict = args.Contains("--strict", StringComparer.Ordinal);

if (!File.Exists(matrixPath))
{
    Console.Error.WriteLine("type coverage matrix is missing: " + Path.GetRelativePath(repositoryRoot, matrixPath));
    return 2;
}

using var matrix = JsonDocument.Parse(File.ReadAllText(matrixPath));
var required = ReadIds(matrix.RootElement, "required");
var excluded = ReadIds(matrix.RootElement, "excluded");
var rejected = ReadIds(matrix.RootElement, "rejected");
var normalized = ReadIds(matrix.RootElement, "normalized");
var needsRecipe = ReadIds(matrix.RootElement, "needsRecipe");
var deferred = ReadIds(matrix.RootElement, "deferred");
var auditedRejections = ReadAuditedTypeRejections(Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "unreachable"));
var unverifiedRejections = rejected.Where(id => !auditedRejections.Contains(id)).Order(StringComparer.Ordinal).ToArray();
var covered = new HashSet<string>(StringComparer.Ordinal);
var manifests = 0;
var groups = new HashSet<string>(StringComparer.Ordinal);
var caseScriptRoot = Path.Combine(repositoryRoot, "scripts", "ParasolidXtCorpusCases");
var canonicalGroups = Directory.Exists(caseScriptRoot)
    ? Directory.EnumerateFiles(caseScriptRoot, "*.cs", SearchOption.TopDirectoryOnly)
        .Where(path => !string.Equals(Path.GetFileNameWithoutExtension(path), "TemplateSmoke", StringComparison.Ordinal))
        .Select(path => Path.GetFileNameWithoutExtension(path) ?? "")
        .Where(group => group.Length != 0)
        .Order(StringComparer.Ordinal)
        .ToArray()
    : [];
foreach (var groupRoot in canonicalGroups.Select(group => Path.Combine(corpusRoot, group)).Where(Directory.Exists))
{
    foreach (var manifestPath in Directory.EnumerateFiles(groupRoot, "manifest.json", SearchOption.AllDirectories))
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        manifests++;
        if (manifest.RootElement.TryGetProperty("group", out var group))
            groups.Add(group.GetString() ?? "");
        if (manifest.RootElement.TryGetProperty("typeCoverage", out var labels) && labels.ValueKind == JsonValueKind.Array)
        {
            foreach (var label in labels.EnumerateArray())
            {
                var value = label.GetString();
                if (!string.IsNullOrWhiteSpace(value)) covered.Add(value);
            }
        }
    }
}

var missing = required.Where(id => !covered.Contains(id) && !rejected.Contains(id) && !normalized.Contains(id)).Order(StringComparer.Ordinal).ToArray();
var coveredRequired = required.Count(id => covered.Contains(id));
var duplicateAssignments = required.Where(id => covered.Contains(id)).ToArray();
var planGapCount = missing.Length + deferred.Length;
var report = new TypeCoverageReport(
    "parasolid-type-coverage-v1",
    required.Length,
    coveredRequired,
    missing.Length,
    excluded.Length,
    rejected.Length,
    normalized.Length,
    deferred.Length,
    planGapCount,
    manifests,
    groups.Order(StringComparer.Ordinal).ToArray(),
    covered.Order(StringComparer.Ordinal).ToArray(),
    missing,
    duplicateAssignments,
    rejected.Order(StringComparer.Ordinal).ToArray(),
    unverifiedRejections,
    normalized.Order(StringComparer.Ordinal).ToArray(),
    needsRecipe.Order(StringComparer.Ordinal).ToArray(),
    deferred.Order(StringComparer.Ordinal).ToArray());
var bytes = Serialize(report);

if (check)
{
    if (!File.Exists(reportPath) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(reportPath)))
    {
        Console.Error.WriteLine("type coverage report is missing or out of date: " + Path.GetRelativePath(repositoryRoot, reportPath));
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllBytes(reportPath, bytes);
}

Console.WriteLine($"type coverage required={required.Length} covered={coveredRequired} rejected={rejected.Length} normalized={normalized.Length} needsRecipe={needsRecipe.Length} deferred={deferred.Length} planGaps={planGapCount} missing={missing.Length} unverifiedRejections={unverifiedRejections.Length} excluded={excluded.Length} cases={manifests} groups={groups.Count}");
return strict && (missing.Length != 0 || unverifiedRejections.Length != 0) ? 1 : 0;

static string[] ReadIds(JsonElement root, string property)
{
    if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
        return [];
    var result = new List<string>();
    foreach (var value in values.EnumerateArray())
    {
        var id = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
        if (!string.IsNullOrWhiteSpace(id)) result.Add(id!);
    }
    result.Sort(StringComparer.Ordinal);
    return result.Distinct(StringComparer.Ordinal).ToArray();
}

static HashSet<string> ReadAuditedTypeRejections(string directory)
{
    var result = new HashSet<string>(StringComparer.Ordinal);
    if (!Directory.Exists(directory)) return result;
    foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array) continue;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("typeId", out var typeId) && typeId.ValueKind == JsonValueKind.String)
            {
                var value = typeId.GetString();
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value!);
            }
        }
    }
    return result;
}

static byte[] Serialize(TypeCoverageReport report)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        writer.WriteString("generator", report.Generator);
        writer.WriteNumber("requiredCount", report.RequiredCount);
        writer.WriteNumber("coveredRequiredCount", report.CoveredRequiredCount);
        writer.WriteNumber("missingCount", report.MissingCount);
        writer.WriteNumber("typeCoverageStrictGapCount", report.MissingCount);
        writer.WriteNumber("excludedCount", report.ExcludedCount);
        writer.WriteNumber("rejectedCount", report.RejectedCount);
        writer.WriteNumber("normalizedCount", report.NormalizedCount);
        writer.WriteNumber("deferredCount", report.DeferredCount);
        writer.WriteNumber("planGapCount", report.PlanGapCount);
        writer.WriteNumber("caseCount", report.CaseCount);
        writer.WriteStartArray("groups");
        foreach (var group in report.Groups) writer.WriteStringValue(group);
        writer.WriteEndArray();
        writer.WriteStartArray("normalizedLabels");
        foreach (var label in report.NormalizedLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("needsRecipeLabels");
        foreach (var label in report.NeedsRecipeLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("deferredLabels");
        foreach (var label in report.DeferredLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("rejectedLabels");
        foreach (var label in report.RejectedLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("unverifiedRejections");
        foreach (var label in report.UnverifiedRejections) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("coveredLabels");
        foreach (var label in report.CoveredLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("missingLabels");
        foreach (var label in report.MissingLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteStartArray("coveredRequiredLabels");
        foreach (var label in report.CoveredRequiredLabels) writer.WriteStringValue(label);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
    return stream.ToArray();
}

static string FindRepositoryRoot(string startDirectory)
{
    var current = Path.GetFullPath(startDirectory);
    while (true)
    {
        if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, "AGENTS.md")))
            return current;
        var parent = Directory.GetParent(current)?.FullName;
        if (parent is null || string.Equals(parent, current, StringComparison.Ordinal))
            return current;
        current = parent;
    }
}

sealed record TypeCoverageReport(
    string Generator,
    int RequiredCount,
    int CoveredRequiredCount,
    int MissingCount,
    int ExcludedCount,
    int RejectedCount,
    int NormalizedCount,
    int DeferredCount,
    int PlanGapCount,
    int CaseCount,
    string[] Groups,
    string[] CoveredLabels,
    string[] MissingLabels,
    string[] CoveredRequiredLabels,
    string[] RejectedLabels,
    string[] UnverifiedRejections,
    string[] NormalizedLabels,
    string[] NeedsRecipeLabels,
    string[] DeferredLabels);
