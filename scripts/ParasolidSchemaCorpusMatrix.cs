#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System.Runtime.CompilerServices;
using System.Text.Json;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repositoryRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var corpusRoot = Path.Combine(repositoryRoot, "bin", "parasolid-xt-corpus");
var reportPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "xt-schema-corpus-matrix-report.json");
var supportReportPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "xt-schema-support-report.json");
var applicabilityPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "xt-schema-applicability.json");
var check = args.Contains("--check", StringComparer.Ordinal);
var includeSmokeVerified = args.Contains("--include-smoke-verified", StringComparer.Ordinal);
var caseFilter = ReadOption(args, "--case");
var schemaFilter = ReadOption(args, "--schema");
var applicability = ReadApplicability(applicabilityPath);
var groupNames = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "scripts", "ParasolidXtCorpusCases"), "*.cs", SearchOption.TopDirectoryOnly)
    .Select(static path => Path.GetFileNameWithoutExtension(path) ?? "")
    .Where(static name => name.Length != 0 && name != "TemplateSmoke")
    .Order(StringComparer.Ordinal)
    .ToArray();
var caseFiles = groupNames
    .SelectMany(group => Directory.Exists(Path.Combine(corpusRoot, group))
        ? Directory.EnumerateFiles(Path.Combine(corpusRoot, group), "model.x_t", SearchOption.AllDirectories)
        : [])
    .Where(path => caseFilter is null || string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), caseFilter, StringComparison.Ordinal))
    .Order(StringComparer.Ordinal)
    .ToArray();
if (caseFiles.Length == 0)
{
    Console.Error.WriteLine("generated corpus is missing; run scripts/GenerateParasolidXtCorpus.cs first");
    return 2;
}

var verifiedSchemas = ReadVerifiedSchemas(supportReportPath);
var schemas = XtCorpusInspection.GetSupportedSchemas()
    .Where(schema => schemaFilter is not null
        ? string.Equals(schema.Identity, schemaFilter, StringComparison.Ordinal)
        : includeSmokeVerified ? verifiedSchemas.Contains(schema.Identity) : schema.ModelerVersion >= 600000)
    .ToArray();
if (schemaFilter is not null && schemas.Length == 0)
    schemas = [XtCorpusInspection.ResolveSupportedSchema(schemaFilter)];
if (!ParasolidScriptHost.TryStartSession("Parasolid schema corpus matrix", out var session, out var skipMessage, userFieldLength: 8))
{
    Console.WriteLine(skipMessage);
    return 0;
}

var results = new List<MatrixResult>();
unsafe
{
    using (session)
    {
        Check(PK_SESSION_set_facet_geometry(PK_facet_geometry_all_c), "PK_SESSION_set_facet_geometry(all)");
        foreach (var casePath in caseFiles)
        {
            var caseId = Path.GetFileName(Path.GetDirectoryName(casePath)) ?? casePath;
            var source = File.ReadAllBytes(casePath);
            ReceivedParts expected;
            try
            {
                expected = Receive(source, caseId + " source");
            }
            catch (Exception exception)
            {
                foreach (var schema in schemas)
                    results.Add(new MatrixResult(caseId, schema.Identity, "SourceUnavailable", exception.Message));
                Console.WriteLine("SKIP " + caseId + ": " + exception.Message);
                continue;
            }
            try
            {
                foreach (var schema in schemas)
                {
                    if (applicability.TryGetValue(caseId, out var rule) && schema.ModelerVersion < rule.MinimumModelerVersion)
                    {
                        results.Add(new MatrixResult(caseId, schema.Identity, "NotApplicable", rule.Reason));
                        if (caseFilter is not null || schemaFilter is not null)
                            Console.WriteLine("SKIP " + caseId + " " + schema.Identity + ": " + rule.Reason);
                        continue;
                    }
                    var incompatibility = XtCorpusInspection.GetTranscodeIncompatibility(source, schema.Identity);
                    if (incompatibility is not null)
                    {
                        results.Add(new MatrixResult(caseId, schema.Identity, "NotApplicable", incompatibility));
                        if (caseFilter is not null || schemaFilter is not null)
                            Console.WriteLine("SKIP " + caseId + " " + schema.Identity + ": " + incompatibility);
                        continue;
                    }
                    try
                    {
                        var target = XtCorpusInspection.Transcode(source, schema.Identity);
                        var direct = Receive(target, caseId + " " + schema.Identity);
                        try { CompareParts(expected, direct, caseId + " " + schema.Identity); }
                        finally { FreeParts(direct); }

                        var transmitVersion = XtCorpusInspection.GetCompatibleTransmitVersion(schema.ModelerVersion);
                        var managed = XtCorpusInspection.RoundTrip(target, transmitVersion, userFields: true, keepCompound: true);
                        var managedAgain = XtCorpusInspection.RoundTrip(managed, transmitVersion, userFields: true, keepCompound: true);
                        if (XtCorpusInspection.ComputeStructuralHash(managed) != XtCorpusInspection.ComputeStructuralHash(managedAgain))
                            throw new InvalidOperationException("managed canonical structural hash is unstable");
                        var managedParts = Receive(managed, caseId + " " + schema.Identity + " managed");
                        try { CompareParts(expected, managedParts, caseId + " " + schema.Identity + " managed"); }
                        finally { FreeParts(managedParts); }
                        results.Add(new MatrixResult(caseId, schema.Identity, "Passed", null));
                    }
                    catch (Exception exception)
                    {
                        results.Add(new MatrixResult(caseId, schema.Identity, "Failed", exception.Message));
                        Console.WriteLine("FAIL " + caseId + " " + schema.Identity + ": " + exception.Message);
                    }
                }
            }
            finally
            {
                FreeParts(expected);
            }
        }
    }
}

var report = Serialize(results, caseFiles.Length, schemas.Length);
if (caseFilter is not null || schemaFilter is not null || includeSmokeVerified)
{
    // Focused diagnostics never replace the canonical full-matrix report.
}
else if (check)
{
    if (!File.Exists(reportPath) || !report.AsSpan().SequenceEqual(File.ReadAllBytes(reportPath)))
    {
        Console.Error.WriteLine("schema corpus matrix report is missing or out of date: " + Path.GetRelativePath(repositoryRoot, reportPath));
        return 1;
    }
}
else
{
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllBytes(reportPath, report);
}

var failed = results.Count(static result => result.Status == "Failed");
Console.WriteLine($"schema corpus matrix schemas={schemas.Length} cases={caseFiles.Length} applicable={results.Count(static result => result.Status != "NotApplicable")} passed={results.Count(static result => result.Status == "Passed")} failed={failed}");
return failed == 0 ? 0 : 1;

static unsafe ReceivedParts Receive(byte[] bytes, string label)
{
    fixed (byte* pointer = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var options = new PK_PART_receive_o_t
        {
            o_t_version = 8,
            transmit_format = PK_transmit_format_text_c,
            attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
            receive_user_fields = PK_LOGICAL_true,
            receive_compound = PK_receive_compound_keep_c,
        };
        int count;
        PK_PART_t* parts;
        Check(PK_PART_receive_b(block, &options, &count, &parts), "PK_PART_receive_b " + label);
        return new ReceivedParts(count, parts);
    }
}

static unsafe void CompareParts(ReceivedParts expected, ReceivedParts actual, string label)
{
    if (expected.Count != actual.Count)
        throw new InvalidOperationException($"{label} part count differs: {expected.Count} != {actual.Count}");
    for (var index = 0; index < expected.Count; index++)
    {
        PK_CLASS_t expectedClass;
        PK_CLASS_t actualClass;
        Check(PK_ENTITY_ask_class(expected.Parts[index], &expectedClass), "PK_ENTITY_ask_class expected " + label);
        Check(PK_ENTITY_ask_class(actual.Parts[index], &actualClass), "PK_ENTITY_ask_class actual " + label);
        if (expectedClass != actualClass)
            throw new InvalidOperationException($"{label} class differs: {expectedClass} != {actualClass}");
        if (expectedClass == PK_CLASS_body)
            CompareBodies(expected.Parts[index], actual.Parts[index], label);
        else if (expectedClass == PK_CLASS_assembly)
            CompareAssemblies(expected.Parts[index], actual.Parts[index], label);
    }
}

static unsafe void CompareBodies(PK_BODY_t expected, PK_BODY_t actual, string label)
{
    PK_BODY_type_t expectedType;
    PK_BODY_type_t actualType;
    Check(PK_BODY_ask_type(expected, &expectedType), "PK_BODY_ask_type expected " + label);
    Check(PK_BODY_ask_type(actual, &actualType), "PK_BODY_ask_type actual " + label);
    if (expectedType != actualType)
        throw new InvalidOperationException($"body type differs: {expectedType} != {actualType}");
    if (expectedType == PK_BODY_type_compound_c)
    {
        var childOptions = new PK_BODY_ask_children_o_t();
        int expectedCount;
        int actualCount;
        PK_BODY_t* expectedChildren;
        PK_BODY_t* actualChildren;
        Check(PK_BODY_ask_children(expected, &childOptions, &expectedCount, &expectedChildren), "PK_BODY_ask_children expected " + label);
        Check(PK_BODY_ask_children(actual, &childOptions, &actualCount, &actualChildren), "PK_BODY_ask_children actual " + label);
        try
        {
            if (expectedCount != actualCount)
                throw new InvalidOperationException($"compound child count differs: {expectedCount} != {actualCount}");
            // PK_DEBUG_BODY_compare rejects a child while it is still owned by a
            // compound (PK_ERROR_compound_body).  The received parts are
            // disposable oracle values, so detach both child sets before doing
            // the normal body comparison.
            var removeOptions = new PK_BODY_remove_from_parents_o_t();
            Check(PK_BODY_remove_from_parents(expectedCount, expectedChildren, &removeOptions), "PK_BODY_remove_from_parents expected " + label);
            Check(PK_BODY_remove_from_parents(actualCount, actualChildren, &removeOptions), "PK_BODY_remove_from_parents actual " + label);
            try
            {
                for (var index = 0; index < expectedCount; index++)
                    CompareBodies(expectedChildren[index], actualChildren[index], label + " child " + index);
            }
            finally
            {
                var addOptions = new PK_BODY_add_to_compound_o_t();
                Check(PK_BODY_add_to_compound(expectedCount, expectedChildren, expected, &addOptions), "PK_BODY_add_to_compound expected " + label);
                Check(PK_BODY_add_to_compound(actualCount, actualChildren, actual, &addOptions), "PK_BODY_add_to_compound actual " + label);
            }
            return;
        }
        finally
        {
            if (expectedChildren is not null) Check(PK_MEMORY_free(expectedChildren), "PK_MEMORY_free expected children");
            if (actualChildren is not null) Check(PK_MEMORY_free(actualChildren), "PK_MEMORY_free actual children");
        }
    }

    var options = new PK_DEBUG_BODY_compare_o_t
    {
        max_diffs = 64,
        all_tests = PK_LOGICAL_false,
        acc_dev_tests = PK_LOGICAL_false,
        non_match_tests = PK_LOGICAL_false,
    };
    var results = new PK_DEBUG_BODY_compare_r_t();
    Check(PK_DEBUG_BODY_compare(expected, actual, &options, &results), "PK_DEBUG_BODY_compare " + label);
    try
    {
        if (results.global_result != PK_DEBUG_global_res_no_diffs_c)
        {
            var details = new List<string>(results.n_global_diffs);
            for (var index = 0; index < results.n_global_diffs; index++)
            {
                var difference = results.global_diffs[index];
                details.Add($"{difference.diff}:{difference.n_masters}/{difference.n_similars}");
            }
            throw new InvalidOperationException($"body differs: global={results.global_result} local={results.local_result} diffs=[{string.Join(',', details)}]");
        }
    }
    finally
    {
        Check(PK_DEBUG_BODY_compare_r_f(&results), "PK_DEBUG_BODY_compare_r_f " + label);
    }
}

static unsafe void CompareAssemblies(PK_ASSEMBLY_t expected, PK_ASSEMBLY_t actual, string label)
{
    int expectedInstances;
    int actualInstances;
    PK_INSTANCE_t* expectedValues;
    PK_INSTANCE_t* actualValues;
    Check(PK_ASSEMBLY_ask_instances(expected, &expectedInstances, &expectedValues), "PK_ASSEMBLY_ask_instances expected " + label);
    Check(PK_ASSEMBLY_ask_instances(actual, &actualInstances, &actualValues), "PK_ASSEMBLY_ask_instances actual " + label);
    try
    {
        if (expectedInstances != actualInstances)
            throw new InvalidOperationException($"assembly instance count differs: {expectedInstances} != {actualInstances}");
    }
    finally
    {
        if (expectedValues is not null) Check(PK_MEMORY_free(expectedValues), "PK_MEMORY_free expected instances");
        if (actualValues is not null) Check(PK_MEMORY_free(actualValues), "PK_MEMORY_free actual instances");
    }
}

static unsafe void FreeParts(ReceivedParts value)
{
    if (value.Parts is not null)
        Check(PK_MEMORY_free(value.Parts), "PK_MEMORY_free parts");
}

static byte[] Serialize(List<MatrixResult> results, int caseCount, int schemaCount)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        writer.WriteString("generator", "parasolid-schema-corpus-matrix-v2");
        writer.WriteNumber("schemaCount", schemaCount);
        writer.WriteNumber("caseCount", caseCount);
        writer.WriteNumber("applicableCount", results.Count(static result => result.Status != "NotApplicable"));
        writer.WriteNumber("passedCount", results.Count(static result => result.Status == "Passed"));
        writer.WriteNumber("failedCount", results.Count(static result => result.Status == "Failed"));
        writer.WriteNumber("notApplicableCount", results.Count(static result => result.Status == "NotApplicable"));
        writer.WriteNumber("sourceUnavailableCount", results.Count(static result => result.Status == "SourceUnavailable"));
        writer.WriteStartArray("schemas");
        foreach (var schema in results.GroupBy(static result => result.SchemaIdentity, StringComparer.Ordinal).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var schemaFailures = schema.Count(static result => result.Status == "Failed");
            var sourceUnavailable = schema.Count(static result => result.Status == "SourceUnavailable");
            writer.WriteStartObject();
            writer.WriteString("identity", schema.Key);
            writer.WriteString("status", schemaFailures == 0 && sourceUnavailable == 0 ? "CorpusVerified" : "Failed");
            writer.WriteNumber("applicableCount", schema.Count(static result => result.Status is "Passed" or "Failed"));
            writer.WriteNumber("passedCount", schema.Count(static result => result.Status == "Passed"));
            writer.WriteNumber("failedCount", schemaFailures);
            writer.WriteNumber("notApplicableCount", schema.Count(static result => result.Status == "NotApplicable"));
            writer.WriteNumber("sourceUnavailableCount", sourceUnavailable);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("notApplicableReasons");
        foreach (var reason in results.Where(static result => result.Status == "NotApplicable")
                     .GroupBy(static result => result.Error ?? "unspecified", StringComparer.Ordinal)
                     .Select(static group => new { Reason = group.Key, Count = group.Count() })
                     .OrderByDescending(static value => value.Count)
                     .ThenBy(static value => value.Reason, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("reason", reason.Reason);
            writer.WriteNumber("count", reason.Count);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("failures");
        foreach (var result in results.Where(static result => result.Status == "Failed").OrderBy(static result => result.CaseId).ThenBy(static result => result.SchemaIdentity))
        {
            writer.WriteStartObject();
            writer.WriteString("caseId", result.CaseId);
            writer.WriteString("schemaIdentity", result.SchemaIdentity);
            writer.WriteString("error", result.Error);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
    return stream.ToArray();
}

static Dictionary<string, ApplicabilityRule> ReadApplicability(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException("schema applicability audit is missing", path);
    using var document = JsonDocument.Parse(File.ReadAllBytes(path));
    var result = new Dictionary<string, ApplicabilityRule>(StringComparer.Ordinal);
    foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
    {
        var caseId = item.GetProperty("caseId").GetString() ?? throw new FormatException("schema applicability caseId is null");
        var rule = new ApplicabilityRule(
            item.GetProperty("minimumModelerVersion").GetInt32(),
            item.GetProperty("reason").GetString() ?? throw new FormatException("schema applicability reason is null"));
        if (!result.TryAdd(caseId, rule))
            throw new FormatException("duplicate schema applicability case: " + caseId);
    }
    return result;
}

static HashSet<string> ReadVerifiedSchemas(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException("schema support report is missing", path);
    using var document = JsonDocument.Parse(File.ReadAllBytes(path));
    var result = new HashSet<string>(StringComparer.Ordinal);
    foreach (var schema in document.RootElement.GetProperty("schemas").EnumerateArray())
    {
        if (schema.GetProperty("status").GetString() == "ParasolidVerified")
            result.Add(schema.GetProperty("identity").GetString() ?? throw new FormatException("schema identity is null"));
    }
    return result;
}

static string? ReadOption(string[] arguments, string name)
{
    for (var index = 0; index < arguments.Length; index++)
    {
        if (!string.Equals(arguments[index], name, StringComparison.Ordinal))
            continue;
        if (index + 1 >= arguments.Length)
            throw new ArgumentException(name + " requires a value");
        return arguments[index + 1];
    }
    return null;
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}

readonly unsafe struct ReceivedParts
{
    public ReceivedParts(int count, PK_PART_t* parts)
    {
        Count = count;
        Parts = parts;
    }

    public int Count { get; }
    public PK_PART_t* Parts { get; }
}
readonly record struct MatrixResult(string CaseId, string SchemaIdentity, string Status, string? Error);
readonly record struct ApplicabilityRule(int MinimumModelerVersion, string Reason);
