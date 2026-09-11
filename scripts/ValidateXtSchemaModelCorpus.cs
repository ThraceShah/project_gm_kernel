#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using ProjectGmKernel.Xt;
using System.Reflection;
using System.Runtime.CompilerServices;
using Schema37102 = ProjectGmKernel.Xt.Schema.SCH_3701097_37102;

#pragma warning disable IL2026, IL3050 // Corpus validation intentionally reflects generated model storage.

var scriptDirectory = GetScriptDirectory();
var schemaDirectory = Environment.GetEnvironmentVariable("PARASOLID_SCHEMA_DIR") ?? Environment.GetEnvironmentVariable("P_SCHEMA");
var catalog = string.IsNullOrWhiteSpace(schemaDirectory)
    ? XtSchemaCatalog.OpenBuiltIn()
    : XtSchemaCatalog.OpenDirectory(schemaDirectory);
catalog.LoadAll();
var fixtures = args.Length == 0
    ? Path.GetFullPath(Path.Combine(scriptDirectory, "..", "tests", "ParasolidXtCorpus", "Fixtures"))
    : Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var outputRoot = Path.GetFullPath(Path.Combine(scriptDirectory, "..", "bin", "xt-schema-model-rebuilt"));
var paths = Directory.EnumerateFiles(fixtures, "model.x_t", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
if (paths.Length == 0) throw new InvalidOperationException("No committed XT fixtures were found.");
var violations = 0;
var checkedValues = 0;
foreach (var path in paths)
{
    Console.Write($"{Path.GetRelativePath(fixtures, path)}: ");
    var document = XtCodec.Read(catalog, File.ReadAllBytes(path));
    var model = Schema37102.CODEC.Decode(document);
    var (fixtureViolations, fixtureChecked) = ValidateEnumMembership(model, Path.GetRelativePath(fixtures, path));
    violations += fixtureViolations;
    checkedValues += fixtureChecked;
    var rebuilt = Schema37102.CODEC.Encode(model);
    var rebuiltBytes = XtCodec.Write(catalog, rebuilt);
    _ = XtCodec.Read(catalog, rebuiltBytes);
    var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(fixtures, path))!;
    var outputDirectory = Path.Combine(outputRoot, relativeDirectory);
    Directory.CreateDirectory(outputDirectory);
    File.WriteAllBytes(Path.Combine(outputDirectory, "model.x_t"), rebuiltBytes);
    Console.WriteLine($"bodies={model.BODY.Length}, assemblies={model.ASSEMBLY.Length}, frames={model.FRAME.Length}, meshes={model.MESH.Length}, lattices={model.LATTICE.Length}, rebuilt={rebuiltBytes.Length}");
}
if (violations != 0)
    throw new InvalidOperationException($"{violations} generated enum value(s) disagree with corpus data; update the enum table in scripts/GenerateXtSchema.cs.");
Console.WriteLine($"Generated enum tables agree with all {checkedValues} corpus field values that have generated enums.");

static string GetScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

// Indexes the generated NODE__field enums of the 37102 binding namespace and the
// transmit flags of their fields, so decoded rows can be checked against them.
static Dictionary<(string Node, string Field), (Type EnumType, bool Transmit)> BuildSchemaEnumIndex()
{
    var assembly = typeof(XtCodec).Assembly;
    var schema = XtBuiltInSchemas.Resolve("SCH_3701097_37102");
    var index = new Dictionary<(string, string), (Type, bool)>();
    foreach (var type in assembly.GetTypes())
    {
        if (!type.IsEnum || type.Namespace != "ProjectGmKernel.Xt.Schema.SCH_3701097_37102")
            continue;
        var parts = type.Name.Split("__", 2);
        if (parts.Length != 2)
            continue;
        XtNodeDescriptor? descriptor = null;
        foreach (var candidate in schema.Nodes)
            if (candidate.Name == parts[0])
            {
                descriptor = candidate;
                break;
            }
        if (descriptor is null)
            continue;
        var transmit = false;
        foreach (var field in schema.GetFields(descriptor.Value))
            if (field.Name == parts[1])
                transmit = field.Transmit;
        index[(parts[0], parts[1])] = (type, transmit);
    }
    return index;
}

static (int Violations, int Checked) ValidateEnumMembership(Schema37102.MODEL model, string label)
{
    var storage = typeof(Schema37102.MODEL).GetField("Storage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(model)!;
    var storageType = storage.GetType();
    var violations = 0;
    var checkedValues = 0;
    foreach (var ((node, field), (enumType, transmit)) in BuildSchemaEnumIndex())
    {
        if (!transmit)
            continue;
        var rows = storageType.GetField(node, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(storage) as Array;
        if (rows is null)
            continue;
        var rowField = rows.GetType().GetElementType()!.GetField(field);
        if (rowField is null)
            continue;
        var allowed = Enum.GetValues(enumType).Cast<object>().Select(static value => Convert.ToUInt64(value)).ToHashSet();
        for (var row = 0; row < rows.Length; row++)
        {
            var raw = Convert.ToUInt64(rowField.GetValue(rows.GetValue(row))!);
            if (raw is ulong.MaxValue || raw == 0xFF)
                continue;
            checkedValues++;
            if (allowed.Contains(raw))
                continue;
            violations++;
            Console.WriteLine($"\n  enum violation: {label}: {node}.{field}[{row}] = {raw} is not in {enumType.Name} {{{string.Join(", ", allowed.OrderBy(static value => value))}}}");
        }
    }
    return (violations, checkedValues);
}
#pragma warning restore IL2026, IL3050
