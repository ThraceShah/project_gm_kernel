#!/usr/bin/env dotnet run

using System.Text.Json;

var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
var apiManifestPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "generated", "manifest.json");
var corpusRoot = Path.Combine(repositoryRoot, "bin", "parasolid-xt-corpus");
var caseScriptRoot = Path.Combine(repositoryRoot, "scripts", "ParasolidXtCorpusCases");
var coverageCaseRoot = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "cases");
var reportPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "coverage-report.json");
var check = args.Contains("--check", StringComparer.Ordinal);
var strict = args.Contains("--strict", StringComparer.Ordinal);

if (!File.Exists(apiManifestPath))
{
    Console.Error.WriteLine("API coverage manifest is missing: " + Path.GetRelativePath(repositoryRoot, apiManifestPath));
    return 2;
}

var persistentApis = ReadPersistentApis(apiManifestPath);
var canonicalGroups = Directory.Exists(caseScriptRoot)
    ? Directory.EnumerateFiles(caseScriptRoot, "*.cs", SearchOption.TopDirectoryOnly)
        .Where(static path => !string.Equals(Path.GetFileNameWithoutExtension(path), "TemplateSmoke", StringComparison.Ordinal))
        .Select(path => Path.GetFileNameWithoutExtension(path) ?? "")
        .Where(static group => group.Length != 0)
        .Order(StringComparer.Ordinal)
        .ToArray()
    : [];
var manifests = canonicalGroups
    .SelectMany(group =>
    {
        var groupRoot = Path.Combine(corpusRoot, group);
        return Directory.Exists(groupRoot)
            ? Directory.EnumerateFiles(groupRoot, "manifest.json", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
    })
    .OrderBy(static path => path, StringComparer.Ordinal)
    .ToArray();
var coveredApis = new HashSet<string>(StringComparer.Ordinal);
var auditedUnreachableApis = new HashSet<string>(StringComparer.Ordinal);
var verificationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
var groups = new HashSet<string>(StringComparer.Ordinal);
var manifestCaseIds = new HashSet<string>(StringComparer.Ordinal);
foreach (var manifestPath in manifests)
{
    using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
    var root = document.RootElement;
    if (root.TryGetProperty("caseId", out var caseId))
        manifestCaseIds.Add(caseId.GetString() ?? "");
    if (root.TryGetProperty("producer", out var producer))
    {
        foreach (var api in producer.GetString()?.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            coveredApis.Add(api);
    }

    if (root.TryGetProperty("group", out var group))
        groups.Add(group.GetString() ?? "");
    if (root.TryGetProperty("verification", out var verification))
    {
        var value = verification.GetString() ?? "";
        verificationCounts[value] = verificationCounts.TryGetValue(value, out var count) ? count + 1 : 1;
    }
}

ReadAuditedUnreachableApis(Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "unreachable"), auditedUnreachableApis);
var persistentApiSet = persistentApis.ToHashSet(StringComparer.Ordinal);
auditedUnreachableApis.RemoveWhere(api => !persistentApiSet.Contains(api));
var coveredAndAudited = persistentApis.Where(api => coveredApis.Contains(api) && auditedUnreachableApis.Contains(api)).ToArray();
var uncovered = persistentApis.Where(api => !coveredApis.Contains(api)).ToArray();
var strictGaps = persistentApis.Where(api => !coveredApis.Contains(api) && !auditedUnreachableApis.Contains(api)).ToArray();
var declaredCaseIds = new HashSet<string>(StringComparer.Ordinal);
var missingCoverageFiles = new List<string>();
foreach (var group in canonicalGroups)
{
    var coverageName = string.Equals(group, "TRCurves", StringComparison.Ordinal)
        ? "trcurves"
        : ToKebabCase(group);
    var coveragePath = Path.Combine(coverageCaseRoot, coverageName + ".json");
    if (!File.Exists(coveragePath))
    {
        missingCoverageFiles.Add(Path.GetRelativePath(repositoryRoot, coveragePath).Replace(Path.DirectorySeparatorChar, '/'));
        continue;
    }

    using var coverageDocument = JsonDocument.Parse(File.ReadAllText(coveragePath));
    if (coverageDocument.RootElement.TryGetProperty("cases", out var declaredCases))
    {
        foreach (var declaredCase in declaredCases.EnumerateArray())
        {
            if (declaredCase.TryGetProperty("caseId", out var declaredId))
                declaredCaseIds.Add(declaredId.GetString() ?? "");
        }
    }
}

var metadataMissing = manifestCaseIds.Where(id => !declaredCaseIds.Contains(id)).Order(StringComparer.Ordinal).ToArray();
var metadataExtra = declaredCaseIds.Where(id => !manifestCaseIds.Contains(id)).Order(StringComparer.Ordinal).ToArray();
var report = BuildReport(persistentApis, coveredApis, auditedUnreachableApis, uncovered, strictGaps, coveredAndAudited, manifests, groups, verificationCounts, missingCoverageFiles, metadataMissing, metadataExtra);
var bytes = Serialize(report);

if (check)
{
    if (!File.Exists(reportPath) || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(reportPath)))
    {
        Console.Error.WriteLine("coverage report is missing or out of date: " + Path.GetRelativePath(repositoryRoot, reportPath));
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllBytes(reportPath, bytes);
}

Console.WriteLine($"persistent APIs={persistentApis.Length} covered references={coveredApis.Count} uncovered={uncovered.Length} cases={manifests.Length} groups={groups.Count}");
if (uncovered.Length != 0)
    Console.WriteLine($"audited unreachable={auditedUnreachableApis.Count} strict gaps={strictGaps.Length}; see " + Path.GetRelativePath(repositoryRoot, reportPath));
// An API may be covered for one parameter/owner branch and audited as unreachable
// for another; branch-level overlap is therefore informational rather than an error.
return strict && (strictGaps.Length != 0 || missingCoverageFiles.Count != 0 || metadataMissing.Length != 0 || metadataExtra.Length != 0) ? 1 : 0;

static void ReadAuditedUnreachableApis(string directory, HashSet<string> destination)
{
    if (!Directory.Exists(directory))
        return;

    foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            continue;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("api", out var apis))
                continue;
            if (apis.ValueKind == JsonValueKind.String)
            {
                var value = apis.GetString();
                if (!string.IsNullOrWhiteSpace(value)) destination.Add(value);
            }
            else if (apis.ValueKind == JsonValueKind.Array)
            {
                foreach (var api in apis.EnumerateArray())
                {
                    var value = api.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) destination.Add(value);
                }
            }
        }
    }
}

static string[] ReadPersistentApis(string path)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var values = new List<string>();
    foreach (var api in document.RootElement.GetProperty("apis").EnumerateArray())
    {
        var classification = api.GetProperty("classification").GetString();
        if (classification is "Producer" or "Mutator")
            values.Add(api.GetProperty("name").GetString() ?? "");
    }

    values.Sort(StringComparer.Ordinal);
    return values.ToArray();
}

static CoverageReport BuildReport(
    string[] persistentApis,
    HashSet<string> coveredApis,
    HashSet<string> auditedUnreachableApis,
    string[] uncovered,
    string[] strictGaps,
    string[] coveredAndAudited,
    string[] manifests,
    HashSet<string> groups,
    Dictionary<string, int> verificationCounts,
    List<string> missingCoverageFiles,
    string[] metadataMissing,
    string[] metadataExtra)
{
    var sortedCovered = coveredApis.Order(StringComparer.Ordinal).ToArray();
    var sortedGroups = groups.Order(StringComparer.Ordinal).ToArray();
    var sortedVerification = verificationCounts
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    return new CoverageReport(
        "parasolid-xt-corpus-coverage-v1",
        persistentApis.Length,
        sortedCovered.Length,
        auditedUnreachableApis.Count,
        uncovered.Length,
        strictGaps.Length,
        manifests.Length,
        sortedGroups,
        sortedCovered,
        uncovered,
        strictGaps,
        auditedUnreachableApis.Order(StringComparer.Ordinal).ToArray(),
        coveredAndAudited,
        sortedVerification,
        missingCoverageFiles.Order(StringComparer.Ordinal).ToArray(),
        metadataMissing,
        metadataExtra);
}

static string ToKebabCase(string name)
{
    if (string.Equals(name, "ICurveSurfaceMatrix", StringComparison.Ordinal))
        return "icurve-surface-matrix";
    if (string.Equals(name, "SPCurveVariants", StringComparison.Ordinal))
        return "spcurve-variants";
    var builder = new System.Text.StringBuilder(name.Length + 8);
    for (var i = 0; i < name.Length; i++)
    {
        var character = name[i];
        if (char.IsUpper(character) && i != 0)
            builder.Append('-');
        builder.Append(char.ToLowerInvariant(character));
    }

    return builder.ToString();
}

static byte[] Serialize(CoverageReport report)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        writer.WriteString("generator", report.Generator);
        writer.WriteNumber("persistentApiCount", report.PersistentApiCount);
        writer.WriteNumber("coveredApiReferenceCount", report.CoveredApiReferenceCount);
        writer.WriteNumber("auditedUnreachableApiCount", report.AuditedUnreachableApiCount);
        writer.WriteNumber("uncoveredApiCount", report.UncoveredApiCount);
        writer.WriteNumber("strictGapCount", report.StrictGapCount);
        writer.WriteNumber("caseCount", report.CaseCount);
        writer.WriteStartArray("groups");
        foreach (var group in report.Groups) writer.WriteStringValue(group);
        writer.WriteEndArray();
        writer.WriteStartArray("coveredApiReferences");
        foreach (var api in report.CoveredApiReferences) writer.WriteStringValue(api);
        writer.WriteEndArray();
        writer.WriteStartArray("uncoveredApis");
        foreach (var api in report.UncoveredApis) writer.WriteStringValue(api);
        writer.WriteEndArray();
        writer.WriteStartArray("strictGaps");
        foreach (var api in report.StrictGaps) writer.WriteStringValue(api);
        writer.WriteEndArray();
        writer.WriteStartArray("auditedUnreachableApis");
        foreach (var api in report.AuditedUnreachableApis) writer.WriteStringValue(api);
        writer.WriteEndArray();
        writer.WriteStartArray("coveredAndAuditedApis");
        foreach (var api in report.CoveredAndAuditedApis) writer.WriteStringValue(api);
        writer.WriteEndArray();
        writer.WriteStartArray("missingCoverageFiles");
        foreach (var path in report.MissingCoverageFiles) writer.WriteStringValue(path);
        writer.WriteEndArray();
        writer.WriteStartArray("coverageMetadataMissing");
        foreach (var caseId in report.CoverageMetadataMissing) writer.WriteStringValue(caseId);
        writer.WriteEndArray();
        writer.WriteStartArray("coverageMetadataExtra");
        foreach (var caseId in report.CoverageMetadataExtra) writer.WriteStringValue(caseId);
        writer.WriteEndArray();
        writer.WriteStartObject("verificationCounts");
        foreach (var pair in report.VerificationCounts) writer.WriteNumber(pair.Key, pair.Value);
        writer.WriteEndObject();
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

internal sealed record CoverageReport(
    string Generator,
    int PersistentApiCount,
    int CoveredApiReferenceCount,
    int AuditedUnreachableApiCount,
    int UncoveredApiCount,
    int StrictGapCount,
    int CaseCount,
    string[] Groups,
    string[] CoveredApiReferences,
    string[] UncoveredApis,
    string[] StrictGaps,
    string[] AuditedUnreachableApis,
    string[] CoveredAndAuditedApis,
    Dictionary<string, int> VerificationCounts,
    string[] MissingCoverageFiles,
    string[] CoverageMetadataMissing,
    string[] CoverageMetadataExtra);
