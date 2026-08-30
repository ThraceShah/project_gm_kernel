using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

public readonly record struct CorpusBodyCounts(
    int Regions,
    int Shells,
    int Faces,
    int Edges,
    int Vertices)
{
    public int Loops { get; init; }
    public int Fins { get; init; }
}

public sealed class CorpusCaseSpec
{
    public CorpusCaseSpec(
        string caseId,
        string producer,
        string description,
        IReadOnlyList<string> coverage,
        Func<PK_BODY_t> create,
        CorpusBodyCounts? expectedCounts = null,
        Func<byte[]>? managedTransmit = null,
        string? parameters = null,
        int sessionUserFieldLength = 0,
        bool transmitUserFields = false,
        IReadOnlyList<string>? requiredSchemaNodes = null,
        IReadOnlyList<string>? requiredSchemaDependencies = null,
        IReadOnlyList<string>? typeCoverage = null,
        Action<PK_BODY_t>? typedAsk = null,
        bool inspectSchema = true,
        bool compound = false)
    {
        CaseId = caseId;
        Producer = producer;
        Description = description;
        Coverage = coverage;
        Create = create;
        ExpectedCounts = expectedCounts;
        ManagedTransmit = managedTransmit;
        Parameters = parameters ?? description;
        SessionUserFieldLength = sessionUserFieldLength;
        TransmitUserFields = transmitUserFields;
        RequiredSchemaNodes = requiredSchemaNodes ?? Array.Empty<string>();
        RequiredSchemaDependencies = requiredSchemaDependencies ?? Array.Empty<string>();
        TypeCoverage = typeCoverage ?? Array.Empty<string>();
        TypedAsk = typedAsk;
        InspectSchema = inspectSchema;
        IsCompound = compound;
    }

    public string CaseId { get; }
    public string Producer { get; }
    public string Description { get; }
    public IReadOnlyList<string> Coverage { get; }
    public Func<PK_BODY_t> Create { get; }
    public CorpusBodyCounts? ExpectedCounts { get; }
    public Func<byte[]>? ManagedTransmit { get; }
    public string Parameters { get; }
    public int SessionUserFieldLength { get; }
    public bool TransmitUserFields { get; }
    public IReadOnlyList<string> RequiredSchemaNodes { get; }
    public IReadOnlyList<string> RequiredSchemaDependencies { get; }
    public IReadOnlyList<string> TypeCoverage { get; }
    /// <summary>Optional post-create typed ask/contract assertion supplied by the case owner.</summary>
    public Action<PK_BODY_t>? TypedAsk { get; }
    /// <summary>Disable schema inspection only for explicitly out-of-scope payloads (e.g. mesh).</summary>
    public bool InspectSchema { get; }
    /// <summary>The transmitted root expands to multiple child BODY parts and uses the compound verifier.</summary>
    public bool IsCompound { get; }
}

public readonly record struct CorpusAssemblyCounts(int Instances, int Parts);

public sealed class CorpusAssemblyCaseSpec
{
    public CorpusAssemblyCaseSpec(
        string caseId,
        string producer,
        string description,
        IReadOnlyList<string> coverage,
        Func<PK_ASSEMBLY_t> create,
        CorpusAssemblyCounts expectedCounts,
        string? parameters = null,
        IReadOnlyList<string>? typeCoverage = null)
    {
        CaseId = caseId;
        Producer = producer;
        Description = description;
        Coverage = coverage;
        Create = create;
        ExpectedCounts = expectedCounts;
        Parameters = parameters ?? description;
        TypeCoverage = typeCoverage ?? Array.Empty<string>();
    }

    public string CaseId { get; }
    public string Producer { get; }
    public string Description { get; }
    public IReadOnlyList<string> Coverage { get; }
    public Func<PK_ASSEMBLY_t> Create { get; }
    public CorpusAssemblyCounts ExpectedCounts { get; }
    public string Parameters { get; }
    public IReadOnlyList<string> TypeCoverage { get; }
}

internal sealed record CorpusCaseListEntry(string CaseId, string Producer, string Description, string[] Coverage, string[] TypeCoverage);

internal sealed record CorpusManifest(
    string CaseId,
    string Group,
    string Producer,
    string Description,
    string Parameters,
    string[] Coverage,
    int SchemaVersion,
    int TransmitVersion,
    CorpusBodyCounts EntityCounts,
    string SemanticHash,
    string GeneratorVersion,
    string Verification,
    string ManagedVerification,
    CorpusSchemaNode[] SchemaNodes,
    CorpusSchemaDependency[] SchemaDependencies,
    string[] TypeCoverage);

internal sealed record CorpusAssemblyManifest(
    string CaseId,
    string Group,
    string Producer,
    string Description,
    string Parameters,
    string[] Coverage,
    int SchemaVersion,
    int TransmitVersion,
    CorpusAssemblyCounts EntityCounts,
    string SemanticHash,
    string GeneratorVersion,
    string Verification,
    string[] TypeCoverage);

internal sealed record CorpusDiagnostics(string CaseId, string Status, string Summary, string? Exception);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CorpusCaseListEntry[]))]
[JsonSerializable(typeof(CorpusManifest))]
[JsonSerializable(typeof(CorpusXtInventory))]
[JsonSerializable(typeof(CorpusAssemblyManifest))]
[JsonSerializable(typeof(CorpusDiagnostics))]
internal partial class CorpusJsonContext : JsonSerializerContext
{
}

public static unsafe class ParasolidXtCorpusHost
{
    private const int TransmitVersion = 371;
    private const string GeneratorVersion = "corpus-host-v1";

    public static int RunGroup(
        string group,
        IReadOnlyList<CorpusCaseSpec> cases,
        string[] args,
        [System.Runtime.CompilerServices.CallerFilePath] string scriptPath = "")
    {
        if (cases.Count == 0)
        {
            Console.Error.WriteLine("case-group has no cases: " + group);
            return 2;
        }

        var options = ParseOptions(group, args, scriptPath);
        if (!options.IsValid)
        {
            Console.Error.WriteLine("usage: dotnet run <case-group.cs> -- [--list-json] [--check] [--case CASE_ID] [--group GROUP] [--output PATH]");
            return 2;
        }
        if (options.ListJson)
        {
            WriteCaseList(cases);
            return 0;
        }

        var selected = SelectCases(group, cases, options.CaseId);
        if (selected.Count == 0)
            return 2;

        var sessionUserFieldLength = cases.Max(static spec => spec.SessionUserFieldLength);
        if (!ParasolidScriptHost.TryStartSession("Parasolid XT corpus " + group, out var session, out var skipMessage, userFieldLength: sessionUserFieldLength))
        {
            Console.WriteLine(skipMessage);
            return 0;
        }

        var failures = 0;
        using (session)
        {
            var schema = AskSchemaVersion();
            foreach (var spec in selected)
            {
                try
                {
                    RunCase(group, spec, schema, options.OutputRoot, options.Check);
                    Console.WriteLine("PASS " + spec.CaseId);
                }
                catch (Exception ex)
                {
                    failures++;
                    WriteFailure(options.OutputRoot, spec, schema, ex);
                    Console.Error.WriteLine("FAIL " + spec.CaseId + ": " + ex.Message);
                }
            }
        }

        return failures == 0 ? 0 : 1;
    }

    public static int RunAssemblyGroup(
        string group,
        IReadOnlyList<CorpusAssemblyCaseSpec> cases,
        string[] args,
        [System.Runtime.CompilerServices.CallerFilePath] string scriptPath = "")
    {
        if (cases.Count == 0)
        {
            Console.Error.WriteLine("case-group has no cases: " + group);
            return 2;
        }

        var options = ParseOptions(group, args, scriptPath);
        if (!options.IsValid)
        {
            Console.Error.WriteLine("usage: dotnet run <case-group.cs> -- [--list-json] [--check] [--case CASE_ID] [--group GROUP] [--output PATH]");
            return 2;
        }
        if (options.ListJson)
        {
            var entries = cases
                .Select(spec => new CorpusCaseListEntry(spec.CaseId, spec.Producer, spec.Description, spec.Coverage.Order(StringComparer.Ordinal).ToArray(), spec.TypeCoverage.Order(StringComparer.Ordinal).ToArray()))
                .OrderBy(entry => entry.CaseId, StringComparer.Ordinal)
                .ToArray();
            Console.WriteLine(JsonSerializer.Serialize(entries, CorpusJsonContext.Default.CorpusCaseListEntryArray));
            return 0;
        }

        var selected = new List<CorpusAssemblyCaseSpec>(cases.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in cases)
        {
            if (!ids.Add(spec.CaseId))
                throw new InvalidOperationException("duplicate case ID in " + group + ": " + spec.CaseId);
            if (options.CaseId is null || string.Equals(options.CaseId, spec.CaseId, StringComparison.Ordinal))
                selected.Add(spec);
        }
        if (selected.Count == 0)
            return 2;

        if (!ParasolidScriptHost.TryStartSession("Parasolid XT corpus " + group, out var session, out var skipMessage))
        {
            Console.WriteLine(skipMessage);
            return 0;
        }

        var failures = 0;
        using (session)
        {
            var schema = AskSchemaVersion();
            foreach (var spec in selected)
            {
                try
                {
                    RunAssemblyCase(group, spec, schema, options.OutputRoot, options.Check);
                    Console.WriteLine("PASS " + spec.CaseId);
                }
                catch (Exception ex)
                {
                    failures++;
                    WriteAssemblyFailure(options.OutputRoot, spec, ex);
                    Console.Error.WriteLine("FAIL " + spec.CaseId + ": " + ex.Message);
                }
            }
        }

        return failures == 0 ? 0 : 1;
    }

    public static void Check(PK_ERROR_code_t error, string operation)
    {
        if (error != 0)
            throw new InvalidOperationException(operation + " failed with error " + error);
    }

    private static CorpusRunOptions ParseOptions(string group, string[] args, string scriptPath)
    {
        var caseId = (string?)null;
        var listJson = false;
        var check = false;
        var output = DefaultOutputRoot(group, scriptPath);
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--list-json":
                    listJson = true;
                    break;
                case "--check":
                    check = true;
                    break;
                case "--case" when i + 1 < args.Length:
                    caseId = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = ResolvePath(args[++i], scriptPath);
                    break;
                case "--group" when i + 1 < args.Length:
                    if (!string.Equals(group, args[++i], StringComparison.Ordinal))
                        return new CorpusRunOptions(caseId, listJson, check, output, Invalid: true);
                    break;
                default:
                    return new CorpusRunOptions(caseId, listJson, check, output, Invalid: true);
            }
        }

        return new CorpusRunOptions(caseId, listJson, check, output, Invalid: false);
    }

    private static string ResolvePath(string path, string scriptPath)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        var scriptDirectory = Path.GetDirectoryName(scriptPath) ?? ".";
        return Path.GetFullPath(Path.Combine(scriptDirectory, path));
    }

    private static string DefaultOutputRoot(string group, string scriptPath)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptPath);
        var repositoryRoot = FindRepositoryRoot(scriptDirectory ?? ".");
        return Path.Combine(repositoryRoot, "bin", "parasolid-xt-corpus", group);
    }

    private static string FindRepositoryRoot(string startDirectory)
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

    private static List<CorpusCaseSpec> SelectCases(
        string group,
        IReadOnlyList<CorpusCaseSpec> cases,
        string? caseId)
    {
        var result = new List<CorpusCaseSpec>(cases.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in cases)
        {
            if (!ids.Add(spec.CaseId))
                throw new InvalidOperationException("duplicate case ID in " + group + ": " + spec.CaseId);
            if (caseId is null || string.Equals(caseId, spec.CaseId, StringComparison.Ordinal))
                result.Add(spec);
        }

        if (caseId is not null && result.Count == 0)
        {
            Console.Error.WriteLine("unknown case ID in " + group + ": " + caseId);
            return [];
        }

        return result;
    }

    private static void WriteCaseList(IReadOnlyList<CorpusCaseSpec> cases)
    {
        var values = new List<CorpusCaseListEntry>(cases.Count);
        foreach (var spec in cases)
        {
            var coverage = spec.Coverage.Order(StringComparer.Ordinal).ToArray();
            values.Add(new CorpusCaseListEntry(spec.CaseId, spec.Producer, spec.Description, coverage, spec.TypeCoverage.Order(StringComparer.Ordinal).ToArray()));
        }

        values.Sort(static (left, right) => string.CompareOrdinal(left.CaseId, right.CaseId));
        Console.WriteLine(JsonSerializer.Serialize(values.ToArray(), CorpusJsonContext.Default.CorpusCaseListEntryArray));
    }

    private static int AskSchemaVersion()
    {
        PK_SESSION_schema_version_t schema;
        Check(PK_SESSION_ask_schema_version(&schema), "PK_SESSION_ask_schema_version");
        return schema.schema_version;
    }

    private static void RequireStructuralRoundTrip(byte[] expected, byte[] actual, string caseId)
    {
        var expectedHash = XtCorpusInspection.ComputeStructuralHash(expected);
        var actualHash = XtCorpusInspection.ComputeStructuralHash(actual);
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            throw new InvalidOperationException($"{caseId} structural XT hash differs: {expectedHash} != {actualHash}");
    }

    private static void RunCase(
        string group,
        CorpusCaseSpec spec,
        int schemaVersion,
        string outputRoot,
        bool check)
    {
        var body = spec.Create();
        if (body <= 0)
            throw new InvalidOperationException("producer returned an invalid body tag");

        var counts = spec.IsCompound ? AskCompoundCounts(body) : AskCounts(body);
        if (spec.ExpectedCounts is { } expected &&
            (counts.Regions != expected.Regions || counts.Shells != expected.Shells ||
             counts.Faces != expected.Faces || counts.Edges != expected.Edges ||
             counts.Vertices != expected.Vertices ||
             (expected.Loops != 0 && counts.Loops != expected.Loops) ||
             (expected.Fins != 0 && counts.Fins != expected.Fins)))
            throw new InvalidOperationException($"counts mismatch: expected {expected}, got {counts}");

        // Typed asks are deliberately owned by the case group because a body may
        // contain several geometric/topological entities and only the case knows
        // which one is the target.  The callback must throw on a mismatch.
        spec.TypedAsk?.Invoke(body);

        var caseDirectory = Path.Combine(outputRoot, spec.CaseId);
        Directory.CreateDirectory(caseDirectory);
        var xtPath = Path.Combine(caseDirectory, "model.x_t");
        var manifestPath = Path.Combine(caseDirectory, "manifest.json");
        var diagnosticsPath = Path.Combine(caseDirectory, "diagnostics.json");

        var xt = Transmit(body, spec.TransmitUserFields);
        File.WriteAllBytes(xtPath, xt);
        if (spec.IsCompound)
            ReceiveCompoundAndCompare(xt, body, counts, spec.CaseId);
        else
            ReceiveAndCompare(xt, body, counts, spec.CaseId, spec.TransmitUserFields);

        var managedRoundTrip = CorpusManagedKernel.RoundTrip(xt, TransmitVersion, spec.TransmitUserFields, spec.IsCompound);
        RequireStructuralRoundTrip(xt, managedRoundTrip, spec.CaseId + " managed round-trip");
        File.WriteAllBytes(Path.Combine(caseDirectory, "managed-roundtrip.x_t"), managedRoundTrip);
        if (spec.IsCompound)
            ReceiveCompoundAndCompare(managedRoundTrip, body, counts, spec.CaseId + " managed round-trip");
        else
            ReceiveAndCompare(managedRoundTrip, body, counts, spec.CaseId + " managed round-trip", spec.TransmitUserFields);
        var managedEmbedded = CorpusManagedKernel.RoundTrip(xt, 0, spec.TransmitUserFields, spec.IsCompound);
        RequireStructuralRoundTrip(xt, managedEmbedded, spec.CaseId + " managed embedded");
        File.WriteAllBytes(Path.Combine(caseDirectory, "managed-embedded.x_t"), managedEmbedded);
        if (spec.IsCompound)
            ReceiveCompoundAndCompare(managedEmbedded, body, counts, spec.CaseId + " managed embedded");
        else
            ReceiveAndCompare(managedEmbedded, body, counts, spec.CaseId + " managed embedded", spec.TransmitUserFields);
        var managedVerification = "passed";
        if (spec.ManagedTransmit is not null)
        {
            var managedXt = spec.ManagedTransmit();
            if (managedXt.Length == 0)
                throw new InvalidOperationException("managed kernel produced an empty x_t");
            File.WriteAllBytes(Path.Combine(caseDirectory, "managed-model.x_t"), managedXt);
            if (spec.IsCompound)
                ReceiveCompoundAndCompare(managedXt, body, counts, spec.CaseId + " managed");
            else
                ReceiveAndCompare(managedXt, body, counts, spec.CaseId + " managed", false);
        }

        var schemaInventory = !spec.InspectSchema || spec.TransmitUserFields
            ? new CorpusXtInventory(Array.Empty<CorpusSchemaNode>(), Array.Empty<CorpusSchemaDependency>())
            : ParasolidXtSchemaInspector.Inspect(xt);
        ValidateSchemaExpectations(spec, schemaInventory);
        var semanticHash = ComputeSemanticHash(spec, schemaVersion, counts, schemaInventory);
        var manifest = new CorpusManifest(
            spec.CaseId,
            group,
            spec.Producer,
            spec.Description,
            spec.Parameters,
            spec.Coverage.Order(StringComparer.Ordinal).ToArray(),
            schemaVersion,
            TransmitVersion,
            counts,
            semanticHash,
            GeneratorVersion,
            "ParasolidVerified",
            managedVerification,
            schemaInventory.Nodes,
            schemaInventory.Dependencies,
            spec.TypeCoverage.Order(StringComparer.Ordinal).ToArray());

        if (check)
        {
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("manifest is missing; run without --check to generate it");
            var previous = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), CorpusJsonContext.Default.CorpusManifest);
            if (previous is null || !string.Equals(previous.SemanticHash, semanticHash, StringComparison.Ordinal))
                throw new InvalidOperationException("semantic hash changed from previous manifest");
        }

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, CorpusJsonContext.Default.CorpusManifest));
        File.WriteAllText(diagnosticsPath, JsonSerializer.Serialize(
            new CorpusDiagnostics(spec.CaseId, "pass", "self-receive and body compare passed; managed verification=" + managedVerification, null),
            CorpusJsonContext.Default.CorpusDiagnostics));
    }

    private static void RunAssemblyCase(
        string group,
        CorpusAssemblyCaseSpec spec,
        int schemaVersion,
        string outputRoot,
        bool check)
    {
        var assembly = spec.Create();
        if (assembly <= 0)
            throw new InvalidOperationException("producer returned an invalid assembly tag");

        var caseDirectory = Path.Combine(outputRoot, spec.CaseId);
        Directory.CreateDirectory(caseDirectory);
        var manifestPath = Path.Combine(caseDirectory, "manifest.json");
        var bytes = Transmit(assembly);
        File.WriteAllBytes(Path.Combine(caseDirectory, "model.x_t"), bytes);
        var counts = ReceiveAssemblyAndCheck(bytes, assembly, spec.ExpectedCounts, spec.CaseId);
        var managedRoundTrip = CorpusManagedKernel.RoundTrip(bytes, TransmitVersion);
        RequireStructuralRoundTrip(bytes, managedRoundTrip, spec.CaseId + " managed round-trip");
        File.WriteAllBytes(Path.Combine(caseDirectory, "managed-roundtrip.x_t"), managedRoundTrip);
        _ = ReceiveAssemblyAndCheck(managedRoundTrip, assembly, spec.ExpectedCounts, spec.CaseId + " managed round-trip");
        var managedEmbedded = CorpusManagedKernel.RoundTrip(bytes, 0);
        RequireStructuralRoundTrip(bytes, managedEmbedded, spec.CaseId + " managed embedded");
        File.WriteAllBytes(Path.Combine(caseDirectory, "managed-embedded.x_t"), managedEmbedded);
        _ = ReceiveAssemblyAndCheck(managedEmbedded, assembly, spec.ExpectedCounts, spec.CaseId + " managed embedded");
        var semanticHash = ComputeAssemblySemanticHash(spec, schemaVersion, counts);
        var manifest = new CorpusAssemblyManifest(
            spec.CaseId,
            group,
            spec.Producer,
            spec.Description,
            spec.Parameters,
            spec.Coverage.Order(StringComparer.Ordinal).ToArray(),
            schemaVersion,
            TransmitVersion,
            counts,
            semanticHash,
            GeneratorVersion,
            "ParasolidVerified",
            spec.TypeCoverage.Order(StringComparer.Ordinal).ToArray());

        if (check)
        {
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("manifest is missing; run without --check to generate it");
            var previous = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), CorpusJsonContext.Default.CorpusAssemblyManifest);
            if (previous is null || !string.Equals(previous.SemanticHash, semanticHash, StringComparison.Ordinal))
                throw new InvalidOperationException("semantic hash changed from previous manifest");
        }

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, CorpusJsonContext.Default.CorpusAssemblyManifest));
        File.WriteAllText(
            Path.Combine(caseDirectory, "diagnostics.json"),
            JsonSerializer.Serialize(new CorpusDiagnostics(spec.CaseId, "pass", "assembly self-receive and structural checks passed", null), CorpusJsonContext.Default.CorpusDiagnostics));
    }

    private static CorpusAssemblyCounts ReceiveAssemblyAndCheck(
        byte[] bytes,
        PK_ASSEMBLY_t expected,
        CorpusAssemblyCounts expectedCounts,
        string caseId)
    {
        fixed (byte* bytesPointer = bytes)
        {
            var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPointer);
            var options = new PK_PART_receive_o_t { o_t_version = 1, transmit_format = PK_transmit_format_text_c };
            int partCount;
            PK_PART_t* parts;
            Check(PK_PART_receive_b(block, &options, &partCount, &parts), "PK_PART_receive_b " + caseId);
            try
            {
                if (partCount != 1)
                    throw new InvalidOperationException("received part count is " + partCount);
                var received = parts[0];
                int instanceCount;
                PK_INSTANCE_t* instances;
                Check(PK_ASSEMBLY_ask_instances(received, &instanceCount, &instances), "PK_ASSEMBLY_ask_instances " + caseId);
                try
                {
                    int partCountInAssembly;
                    PK_PART_t* assemblyParts;
                    Check(PK_ASSEMBLY_ask_parts(received, &partCountInAssembly, &assemblyParts), "PK_ASSEMBLY_ask_parts " + caseId);
                    try
                    {
                        var actual = new CorpusAssemblyCounts(instanceCount, partCountInAssembly);
                        if (actual != expectedCounts)
                            throw new InvalidOperationException($"assembly counts mismatch: expected {expectedCounts}, got {actual}");
                        _ = expected;
                        return actual;
                    }
                    finally
                    {
                        if (assemblyParts is not null)
                            Check(PK_MEMORY_free(assemblyParts), "PK_MEMORY_free assembly parts");
                    }
                }
                finally
                {
                    if (instances is not null)
                        Check(PK_MEMORY_free(instances), "PK_MEMORY_free assembly instances");
                }
            }
            finally
            {
                if (parts is not null)
                    Check(PK_MEMORY_free(parts), "PK_MEMORY_free received parts");
            }
        }
    }

    private static string ComputeAssemblySemanticHash(CorpusAssemblyCaseSpec spec, int schemaVersion, CorpusAssemblyCounts counts)
    {
        var canonical = new StringBuilder()
            .Append("case=").Append(spec.CaseId).Append('\n')
            .Append("producer=").Append(spec.Producer).Append('\n')
            .Append("parameters=").Append(spec.Parameters).Append('\n')
            .Append("schema=").Append(schemaVersion).Append('\n')
            .Append("instances=").Append(counts.Instances).Append('\n')
            .Append("parts=").Append(counts.Parts).Append('\n');
        foreach (var label in spec.Coverage.Order(StringComparer.Ordinal))
            canonical.Append("coverage=").Append(label).Append('\n');
        foreach (var label in spec.TypeCoverage.Order(StringComparer.Ordinal))
            canonical.Append("type-coverage=").Append(label).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void WriteAssemblyFailure(string outputRoot, CorpusAssemblyCaseSpec spec, Exception exception)
    {
        var caseDirectory = Path.Combine(outputRoot, spec.CaseId);
        Directory.CreateDirectory(caseDirectory);
        File.WriteAllText(
            Path.Combine(caseDirectory, "diagnostics.json"),
            JsonSerializer.Serialize(new CorpusDiagnostics(spec.CaseId, "fail", exception.Message, exception.ToString()), CorpusJsonContext.Default.CorpusDiagnostics));
    }


    private static byte[] Transmit(PK_BODY_t body, bool transmitUserFields = false)
    {
        var options = new PK_PART_transmit_o_t
        {
            o_t_version = 1,
            transmit_format = PK_transmit_format_text_c,
            transmit_version = TransmitVersion,
            transmit_user_fields = transmitUserFields ? PK_LOGICAL_true : PK_LOGICAL_false,
        };
        var block = new PK_MEMORY_block_t();
        Check(PK_PART_transmit_b(1, &body, &options, &block), "PK_PART_transmit_b");
        try
        {
            using var stream = new MemoryStream();
            for (var current = &block; current is not null; current = current->next)
            {
                if (current->bytes is not null && current->n_bytes != 0)
                    stream.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
            }

            return stream.ToArray();
        }
        finally
        {
            Check(PK_MEMORY_block_f(&block), "PK_MEMORY_block_f");
        }
    }

    private static void ReceiveAndCompare(byte[] bytes, PK_BODY_t expected, CorpusBodyCounts expectedCounts, string caseId, bool receiveUserFields = false)
    {
        fixed (byte* bytesPointer = bytes)
        {
            var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPointer);
            var options = new PK_PART_receive_o_t
            {
                o_t_version = 1,
                transmit_format = PK_transmit_format_text_c,
                receive_user_fields = receiveUserFields ? PK_LOGICAL_true : PK_LOGICAL_false,
            };
            int partCount;
            PK_PART_t* parts;
            Check(PK_PART_receive_b(block, &options, &partCount, &parts), "PK_PART_receive_b " + caseId);
            try
            {
                if (partCount != 1)
                    throw new InvalidOperationException("received part count is " + partCount);
                var received = parts[0];
                var receivedCounts = AskCounts(received);
                if (receivedCounts.Regions != expectedCounts.Regions || receivedCounts.Shells != expectedCounts.Shells ||
                    receivedCounts.Faces != expectedCounts.Faces || receivedCounts.Edges != expectedCounts.Edges ||
                    receivedCounts.Vertices != expectedCounts.Vertices ||
                    (expectedCounts.Loops != 0 && receivedCounts.Loops != expectedCounts.Loops) ||
                    (expectedCounts.Fins != 0 && receivedCounts.Fins != expectedCounts.Fins))
                    throw new InvalidOperationException($"self-receive counts mismatch: expected {expectedCounts}, got {receivedCounts}");
                CompareBodies(expected, received, caseId);
                if (receiveUserFields)
                    CompareUserFields(expected, received, caseId);
            }
            finally
            {
                if (parts is not null)
                    Check(PK_MEMORY_free(parts), "PK_MEMORY_free received parts");
            }
        }
    }

    private static CorpusBodyCounts AskCompoundCounts(PK_BODY_t compound)
    {
        var children = AskCompoundChildren(compound, out var childCount);
        try
        {
            var total = new CorpusBodyCounts(0, 0, 0, 0, 0);
            for (var i = 0; i < childCount; i++)
            {
                var counts = AskCounts(children[i]);
                total = total with
                {
                    Regions = total.Regions + counts.Regions,
                    Shells = total.Shells + counts.Shells,
                    Faces = total.Faces + counts.Faces,
                    Edges = total.Edges + counts.Edges,
                    Vertices = total.Vertices + counts.Vertices,
                    Loops = total.Loops + counts.Loops,
                    Fins = total.Fins + counts.Fins,
                };
            }

            return total;
        }
        finally
        {
            Free(children);
        }
    }

    private static PK_BODY_t* AskCompoundChildren(PK_BODY_t compound, out int childCount)
    {
        var options = new PK_BODY_ask_children_o_t();
        var count = 0;
        PK_BODY_t* children;
        Check(PK_BODY_ask_children(compound, &options, &count, &children), "PK_BODY_ask_children");
        childCount = count;
        if (count < 1 || children is null)
            throw new InvalidOperationException("compound body has no children");
        return children;
    }

    private static void ReceiveCompoundAndCompare(byte[] bytes, PK_BODY_t expectedCompound, CorpusBodyCounts expectedCounts, string caseId)
    {
        var expectedChildren = AskCompoundChildren(expectedCompound, out var expectedChildCount);
        try
        {
            fixed (byte* bytesPointer = bytes)
            {
                var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, bytesPointer);
                var options = new PK_PART_receive_o_t
                {
                    o_t_version = 1,
                    transmit_format = PK_transmit_format_text_c,
                };
                int partCount;
                PK_PART_t* parts;
                Check(PK_PART_receive_b(block, &options, &partCount, &parts), "PK_PART_receive_b " + caseId);
                try
                {
                    if (partCount != expectedChildCount)
                        throw new InvalidOperationException($"compound receive part count mismatch: expected {expectedChildCount}, got {partCount}");

                    var receivedTotal = new CorpusBodyCounts(0, 0, 0, 0, 0);
                    for (var i = 0; i < partCount; i++)
                    {
                        var received = (PK_BODY_t)parts[i];
                        var expected = expectedChildren[i];
                        var expectedChildCounts = AskCounts(expected);
                        var receivedCounts = AskCounts(received);
                        if (receivedCounts != expectedChildCounts)
                            throw new InvalidOperationException($"compound child {i} counts mismatch: expected {expectedChildCounts}, got {receivedCounts}");
                        PK_BODY_type_t expectedType;
                        PK_BODY_type_t receivedType;
                        Check(PK_BODY_ask_type(expected, &expectedType), "PK_BODY_ask_type expected compound child");
                        Check(PK_BODY_ask_type(received, &receivedType), "PK_BODY_ask_type received compound child");
                        if (expectedType != receivedType)
                            throw new InvalidOperationException($"compound child {i} body type mismatch: expected {expectedType}, got {receivedType}");
                        receivedTotal = receivedTotal with
                        {
                            Regions = receivedTotal.Regions + receivedCounts.Regions,
                            Shells = receivedTotal.Shells + receivedCounts.Shells,
                            Faces = receivedTotal.Faces + receivedCounts.Faces,
                            Edges = receivedTotal.Edges + receivedCounts.Edges,
                            Vertices = receivedTotal.Vertices + receivedCounts.Vertices,
                            Loops = receivedTotal.Loops + receivedCounts.Loops,
                            Fins = receivedTotal.Fins + receivedCounts.Fins,
                        };
                        // PK_DEBUG_BODY_compare deliberately rejects bodies that are
                        // still owned by a compound.  Counts and concrete child
                        // types are the stable receive-side contract here; the
                        // parent/child cardinality is checked above.
                    }

                    if (receivedTotal != expectedCounts)
                        throw new InvalidOperationException($"compound aggregate counts mismatch: expected {expectedCounts}, got {receivedTotal}");
                }
                finally
                {
                    if (parts is not null)
                        Check(PK_MEMORY_free(parts), "PK_MEMORY_free compound received parts");
                }
            }
        }
        finally
        {
            Free(expectedChildren);
        }
    }

    private static void CompareUserFields(PK_BODY_t expected, PK_BODY_t received, string caseId)
    {
        int length;
        Check(PK_SESSION_ask_user_field_len(&length), "PK_SESSION_ask_user_field_len " + caseId);
        if (length <= 0)
            throw new InvalidOperationException(caseId + " requested user-field comparison in a zero-length session");
        var expectedBody = stackalloc int[length];
        var receivedBody = stackalloc int[length];
        Check(PK_ENTITY_ask_user_field(expected, expectedBody), "PK_ENTITY_ask_user_field expected body " + caseId);
        Check(PK_ENTITY_ask_user_field(received, receivedBody), "PK_ENTITY_ask_user_field received body " + caseId);
        for (var index = 0; index < length; index++)
        {
            if (expectedBody[index] != receivedBody[index])
                throw new InvalidOperationException($"{caseId} body user field differs at {index}: {expectedBody[index]} != {receivedBody[index]}");
        }

        var expectedFaces = UserFieldSignatures(expected, length);
        var receivedFaces = UserFieldSignatures(received, length);
        if (!expectedFaces.SequenceEqual(receivedFaces, StringComparer.Ordinal))
            throw new InvalidOperationException(caseId + " face user-field payloads differ");
    }

    private static string[] UserFieldSignatures(PK_BODY_t body, int length)
    {
        int faceCount;
        PK_FACE_t* faces;
        Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces user fields");
        try
        {
            var signatures = new string[faceCount];
            var values = stackalloc int[length];
            for (var faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                Check(PK_ENTITY_ask_user_field(faces[faceIndex], values), "PK_ENTITY_ask_user_field face");
                signatures[faceIndex] = string.Join(',', new ReadOnlySpan<int>(values, length).ToArray());
            }
            Array.Sort(signatures, StringComparer.Ordinal);
            return signatures;
        }
        finally
        {
            if (faces is not null)
                Check(PK_MEMORY_free(faces), "PK_MEMORY_free user-field faces");
        }
    }

    private static void CompareBodies(PK_BODY_t expected, PK_BODY_t received, string caseId)
    {
        var options = new PK_DEBUG_BODY_compare_o_t
        {
            max_diffs = 64,
            all_tests = PK_LOGICAL_false,
            acc_dev_tests = PK_LOGICAL_false,
            non_match_tests = PK_LOGICAL_false,
        };
        var results = new PK_DEBUG_BODY_compare_r_t();
        Check(PK_DEBUG_BODY_compare(expected, received, &options, &results), "PK_DEBUG_BODY_compare " + caseId);
        try
        {
            if (results.global_result != PK_DEBUG_global_res_no_diffs_c)
                throw new InvalidOperationException($"body compare mismatch: global={results.global_result}, local={results.local_result}, global_diffs={results.n_global_diffs}, face_pairs={results.n_face_pairs}");
        }
        finally
        {
            Check(PK_DEBUG_BODY_compare_r_f(&results), "PK_DEBUG_BODY_compare_r_f " + caseId);
        }
    }

    private static CorpusBodyCounts AskCounts(PK_BODY_t body)
    {
        var counts = new CorpusBodyCounts(
            AskRegions(body),
            AskShells(body),
            AskFaces(body),
            AskEdges(body),
            AskVertices(body));
        counts = counts with { Loops = AskLoops(body), Fins = AskFins(body) };
        return counts;
    }

    private static int AskRegions(PK_BODY_t body)
    {
        int count;
        PK_REGION_t* values;
        Check(PK_BODY_ask_regions(body, &count, &values), "PK_BODY_ask_regions");
        Free(values);
        return count;
    }

    private static int AskShells(PK_BODY_t body)
    {
        int count;
        PK_SHELL_t* values;
        Check(PK_BODY_ask_shells(body, &count, &values), "PK_BODY_ask_shells");
        Free(values);
        return count;
    }

    private static int AskFaces(PK_BODY_t body)
    {
        int count;
        PK_FACE_t* values;
        Check(PK_BODY_ask_faces(body, &count, &values), "PK_BODY_ask_faces");
        Free(values);
        return count;
    }

    private static int AskEdges(PK_BODY_t body)
    {
        int count;
        PK_EDGE_t* values;
        Check(PK_BODY_ask_edges(body, &count, &values), "PK_BODY_ask_edges");
        Free(values);
        return count;
    }

    private static int AskVertices(PK_BODY_t body)
    {
        int count;
        PK_VERTEX_t* values;
        Check(PK_BODY_ask_vertices(body, &count, &values), "PK_BODY_ask_vertices");
        Free(values);
        return count;
    }

    private static int AskLoops(PK_BODY_t body)
    {
        int faceCount;
        PK_FACE_t* faces;
        Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces for loops");
        var total = 0;
        try
        {
            for (var i = 0; i < faceCount; i++)
            {
                int loopCount;
                PK_LOOP_t* loops;
                Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops");
                total += loopCount;
                Free(loops);
            }
        }
        finally
        {
            Free(faces);
        }

        return total;
    }

    private static int AskFins(PK_BODY_t body)
    {
        int faceCount;
        PK_FACE_t* faces;
        Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces for fins");
        var total = 0;
        try
        {
            for (var i = 0; i < faceCount; i++)
            {
                int loopCount;
                PK_LOOP_t* loops;
                Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops for fins");
                try
                {
                    for (var j = 0; j < loopCount; j++)
                    {
                        int finCount;
                        PK_FIN_t* fins;
                        Check(PK_LOOP_ask_fins(loops[j], &finCount, &fins), "PK_LOOP_ask_fins");
                        total += finCount;
                        Free(fins);
                    }
                }
                finally
                {
                    Free(loops);
                }
            }
        }
        finally
        {
            Free(faces);
        }

        return total;
    }

    private static void Free<T>(T* values) where T : unmanaged
    {
        if (values is not null)
            Check(PK_MEMORY_free(values), "PK_MEMORY_free");
    }

    private static void ValidateSchemaExpectations(CorpusCaseSpec spec, CorpusXtInventory inventory)
    {
        if (spec.RequiredSchemaNodes.Count != 0)
        {
            var available = inventory.Nodes.Select(node => node.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var expected in spec.RequiredSchemaNodes)
            {
                if (!available.Contains(expected))
                    throw new InvalidOperationException("required XT schema node is missing: " + expected);
            }
        }

        if (spec.RequiredSchemaDependencies.Count != 0)
        {
            foreach (var expected in spec.RequiredSchemaDependencies)
            {
                var separator = expected.IndexOf("->", StringComparison.Ordinal);
                if (separator <= 0 || separator == expected.Length - 2)
                    throw new InvalidOperationException("invalid schema dependency expectation: " + expected);
                var from = expected[..separator];
                var to = expected[(separator + 2)..];
                if (!inventory.Dependencies.Any(edge => edge.From.StartsWith(from + "#", StringComparison.Ordinal) && edge.To.StartsWith(to + "#", StringComparison.Ordinal)))
                    throw new InvalidOperationException("required XT schema dependency is missing: " + expected);
            }
        }
    }

    private static string ComputeSemanticHash(CorpusCaseSpec spec, int schemaVersion, CorpusBodyCounts counts, CorpusXtInventory schemaInventory)
    {
        var canonical = new StringBuilder()
            .Append("case=").Append(spec.CaseId).Append('\n')
            .Append("producer=").Append(spec.Producer).Append('\n')
            .Append("parameters=").Append(spec.Parameters).Append('\n')
            .Append("schema=").Append(schemaVersion).Append('\n')
            .Append("regions=").Append(counts.Regions).Append('\n')
            .Append("shells=").Append(counts.Shells).Append('\n')
            .Append("faces=").Append(counts.Faces).Append('\n')
            .Append("edges=").Append(counts.Edges).Append('\n')
            .Append("vertices=").Append(counts.Vertices).Append('\n')
            .Append("loops=").Append(counts.Loops).Append('\n')
            .Append("fins=").Append(counts.Fins).Append('\n')
            .Append("typed-ask=").Append(spec.TypedAsk is null ? "none" : "passed").Append('\n');
        foreach (var label in spec.Coverage.Order(StringComparer.Ordinal))
            canonical.Append("coverage=").Append(label).Append('\n');
        foreach (var node in schemaInventory.Nodes)
            canonical.Append("schema-node=").Append(node.Name).Append(':').Append(node.Count).Append('\n');
        foreach (var edge in schemaInventory.Dependencies)
            canonical.Append("schema-edge=").Append(edge.From).Append("->").Append(edge.To).Append('\n');
        foreach (var coverage in spec.TypeCoverage.Order(StringComparer.Ordinal))
            canonical.Append("type-coverage=").Append(coverage).Append('\n');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void WriteFailure(string outputRoot, CorpusCaseSpec spec, int schemaVersion, Exception exception)
    {
        var caseDirectory = Path.Combine(outputRoot, spec.CaseId);
        Directory.CreateDirectory(caseDirectory);
        File.WriteAllText(
            Path.Combine(caseDirectory, "diagnostics.json"),
            JsonSerializer.Serialize(new CorpusDiagnostics(spec.CaseId, "fail", exception.Message, exception.ToString()),
                CorpusJsonContext.Default.CorpusDiagnostics));
    }

    private readonly record struct CorpusRunOptions(
        string? CaseId,
        bool ListJson,
        bool Check,
        string OutputRoot,
        bool Invalid)
    {
        public bool IsValid => !Invalid;
    }

}
