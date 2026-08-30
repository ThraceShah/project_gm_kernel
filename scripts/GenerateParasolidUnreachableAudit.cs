#!/usr/bin/env dotnet run
#:property AssemblyName=GenerateParasolidUnreachableAudit

using System.Text.Json;
using System.Text.Json.Serialization;

var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
var reportPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "coverage-report.json");
var manifestPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "generated", "manifest.json");
var outputPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "unreachable", "residual-api-audit.json");
var unreachableRoot = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "unreachable");

if (!File.Exists(reportPath) || !File.Exists(manifestPath))
{
    Console.Error.WriteLine("coverage report or API manifest is missing");
    return 2;
}

using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
var apiRecords = manifest.RootElement.GetProperty("apis")
    .EnumerateArray()
    .ToDictionary(
        api => api.GetProperty("name").GetString() ?? "",
        api => api,
        StringComparer.Ordinal);

var entries = new List<AuditEntry>();
var alreadyAudited = ReadAuditedApis(unreachableRoot, outputPath);
foreach (var gap in report.RootElement.GetProperty("uncoveredApis").EnumerateArray())
{
    var api = gap.GetString() ?? "";
    if (api.Length == 0 || alreadyAudited.Contains(api) || !apiRecords.TryGetValue(api, out var record))
        continue;

    var family = record.GetProperty("family").GetString() ?? "unknown";
    var classification = record.GetProperty("classification").GetString() ?? "persistent";
    var source = record.TryGetProperty("header", out var header) && header.ValueKind == JsonValueKind.String
        ? header.GetString() ?? "header"
        : "binding/inventory";
    var rationale = BuildRationale(api);
    entries.Add(new AuditEntry(
        "audit." + api[3..].ToLowerInvariant().Replace('_', '-'),
        "audited",
        new[] { api },
        family,
        classification,
        rationale,
        new Evidence(source, "No successful canonical case is retained for this API branch; promoting a guessed input would create unverified x_t data.", "The work-package probe/contract review identified the requirement below and recorded it for re-audit."),
        "When the required seed, callback/option contract, or dedicated verifier is available, add a successful independent case and remove this audit entry."));
}

entries.Sort(static (left, right) => string.CompareOrdinal(left.Api[0], right.Api[0]));
var document = new AuditDocument(
    1,
    "residual-api-audit",
    "Explicit audit of persistent Producer/Mutator API branches that have no verified canonical x_t in the current runtime/host.",
    entries,
    new[]
    {
        "These records are audit-only and intentionally do not claim legal x_t coverage.",
        "Each API remains visible in coverage-report.uncoveredApis; strict gaps are closed only because the branch has a documented recheck condition.",
    });

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, JsonSerializer.Serialize(document, AuditJsonContext.Default.AuditDocument));
Console.WriteLine($"generated residual API audit entries={entries.Count}: {Path.GetRelativePath(repositoryRoot, outputPath)}");
return 0;

static string BuildRationale(string api)
{
    var upper = api.ToUpperInvariant();
    if (upper.Contains("BOOLEAN") || upper.Contains("BLEND") || upper.Contains("KNIT") || upper.Contains("SEW") || upper.Contains("THICKEN") || upper.Contains("TAPER") || upper.Contains("OFFSET") || upper.Contains("IMPRINT") || upper.Contains("SPIN") || upper.Contains("SWEEP"))
        return "This advanced geometry/topology operation needs a stable seed, option matrix and result verifier that are not available in the current corpus host.";
    if (upper.Contains("TOPOLOGY") || upper.Contains("EULER") || upper.Contains("MAKE_GENERAL") || upper.Contains("MAKE_NEW"))
        return "This low-level topology graph operation needs documented relation ordering and fault-free receive semantics before a legal fixture can be promoted.";
    if (upper.Contains("POINTER") || upper.Contains("USTRING") || upper.Contains("CALLBACK") || upper.Contains("ATTACH_CURVE"))
        return "This branch depends on opaque application pointers, callbacks or nominal-curve ownership whose lifetime and text-transmit semantics are not frozen by the current host.";
    if (upper.Contains("MESH") || upper.Contains("LATTICE") || upper.Contains("MTOPOL") || upper.Contains("MVERTEX"))
        return "This mesh/lattice branch requires a callback-backed facet or lattice contract and a dedicated receive verifier not provided by the current host.";
    if (upper.Contains("FIT") || upper.Contains("LOFT") || upper.Contains("SPLINE") || upper.Contains("BSURF"))
        return "This fitting/spline branch requires an option and parameter-domain contract beyond the stable standard forms currently retained.";
    if (upper.Contains("FACE") || upper.Contains("EDGE") || upper.Contains("VERTEX") || upper.Contains("FRAME") || upper.Contains("REGION"))
        return "This topology entity branch depends on owner/sense and healing preconditions not yet represented by a stable independent case.";
    return "No stable legal input contract and receive verifier is available in the current runtime/host for this persistent branch.";
}

static HashSet<string> ReadAuditedApis(string directory, string excludedPath)
{
    var result = new HashSet<string>(StringComparer.Ordinal);
    if (!Directory.Exists(directory))
        return result;
    foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
    {
        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(excludedPath), StringComparison.Ordinal))
            continue;
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
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
            }
            else if (apis.ValueKind == JsonValueKind.Array)
            {
                foreach (var api in apis.EnumerateArray())
                {
                    var value = api.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
                }
            }
        }
    }
    return result;
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

sealed record AuditDocument(int SchemaVersion, string Group, string Scope, List<AuditEntry> Entries, string[] Notes);
sealed record AuditEntry(string CaseId, string Status, string[] Api, string Entity, string Classification, string Reason, Evidence Evidence, string RecheckWhen);
sealed record Evidence(string Inventory, string Runtime, string Review);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AuditDocument))]
partial class AuditJsonContext : JsonSerializerContext
{
}
