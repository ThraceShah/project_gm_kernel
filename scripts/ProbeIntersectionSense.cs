#!/usr/bin/env dotnet
#:project ../src/ProjectGmKernel.Xt/ProjectGmKernel.Xt.csproj

using ProjectGmKernel.Xt;
using System.Runtime.CompilerServices;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath())!;
var roots = new[]
{
    Path.GetFullPath(Path.Combine(scriptDirectory, "..", "tests", "ParasolidXtCorpus", "Fixtures")),
    Path.GetFullPath(Path.Combine(scriptDirectory, "..", "bin", "parasolid-xt-corpus")),
    Path.GetFullPath(Path.Combine(scriptDirectory, "..", "bin", "parasolid-xt-corpus-complex-aggregator")),
};
var catalog = XtSchemaCatalog.OpenDirectory(Path.Combine(scriptDirectory, "..", "third_party", "parasolid", "schema"));
catalog.LoadAll();
var schema = XtBuiltInSchemas.Resolve("SCH_3701097_37102");
XtNodeDescriptor intersection = default;
foreach (var candidate in schema.Nodes)
    if (candidate.Name == "INTERSECTION")
    {
        intersection = candidate;
        break;
    }
if (intersection.Type == 0)
    throw new InvalidOperationException("INTERSECTION node is missing from the built-in schema.");

var files = roots.SelectMany(static root => Directory.EnumerateFiles(root, "*.x_t", SearchOption.AllDirectories)).Order(StringComparer.Ordinal).ToArray();
var intersectionSense = new SortedDictionary<char, int>();
var surfaceSense = new SortedDictionary<string, SortedDictionary<char, int>>();
var intersectionMinus = new List<string>();
var intersectionMinusFiles = new SortedDictionary<string, int>();
var surfaceMinus = new List<string>();
var models = 0;

foreach (var path in files)
{
    XtDocument document;
    try
    {
        document = XtCodec.Read(catalog, File.ReadAllBytes(path));
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"skip {Path.GetRelativePath(scriptDirectory, path)}: {exception.Message}");
        continue;
    }
    models++;
    var byIndex = new Dictionary<int, XtNode>();
    foreach (var node in document.Nodes)
        byIndex[node.Index] = node;
    foreach (var node in document.Nodes)
    {
        if (node.Type != intersection.Type)
            continue;
        RecordSense(intersectionSense, CharacterAt(schema, intersection, node, "sense"), path, node, intersectionMinus, prefix: "INTERSECTION");
        if (CharacterAt(schema, intersection, node, "sense") == '-')
        {
            var key = Path.GetRelativePath(scriptDirectory, path);
            intersectionMinusFiles[key] = intersectionMinusFiles.GetValueOrDefault(key) + 1;
        }
        var fields = schema.GetFields(intersection).ToArray();
        var transmitted = fields.Where(static field => field.Transmit).ToArray();
        for (var slot = 0; slot < 2; slot++)
        {
            var surfaceField = Array.Find(fields, static field => field.Name == "surface");
            var position = CountTransmittedBefore(fields, surfaceField) + slot;
            if (position >= node.Fields.Length)
                continue;
            var pointer = node.Fields[position].Pointer;
            if (!byIndex.TryGetValue(pointer, out var target))
                continue;
            if (!schema.TryGetNode(target.Type, out var targetDescriptor) || targetDescriptor.Type == 0)
                continue;
            RecordSense(
                surfaceSense.TryGetValue(targetDescriptor.Name, out var bucket) ? bucket : surfaceSense[targetDescriptor.Name] = new SortedDictionary<char, int>(),
                CharacterAt(schema, targetDescriptor, target, "sense"),
                path,
                target,
                surfaceMinus,
                prefix: $"INTERSECTION[{slot}] (index {node.Index}) -> {targetDescriptor.Name}");
        }
    }
}

Console.WriteLine($"models={models}, files={files.Length}");
Console.WriteLine("INTERSECTION own sense: " + Describe(intersectionSense));
foreach (var (typeName, bucket) in surfaceSense)
    Console.WriteLine($"INTERSECTION surface target {typeName}: {Describe(bucket)}");
Console.WriteLine();
Console.WriteLine($"INTERSECTION sense='-': {intersectionMinus.Count} nodes across {intersectionMinusFiles.Count} files");
foreach (var (file, count) in intersectionMinusFiles)
    Console.WriteLine($"  {count}x {file}");
Console.WriteLine("surface target sense='-' examples:");
foreach (var example in surfaceMinus.Take(5))
    Console.WriteLine("  " + example);
if (surfaceMinus.Count == 0)
    Console.WriteLine("  (none)");
return 0;

static int CountTransmittedBefore(XtFieldDescriptor[] fields, XtFieldDescriptor field)
{
    var count = 0;
    foreach (var candidate in fields)
    {
        if (candidate.Name == field.Name)
            break;
        if (candidate.Transmit)
            count++;
    }
    return count;
}

static char? CharacterAt(XtSchemaDefinition schema, XtNodeDescriptor node, XtNode row, string fieldName)
{
    var fields = schema.GetFields(node).ToArray();
    var position = 0;
    foreach (var candidate in fields)
    {
        if (!candidate.Transmit)
            continue;
        if (candidate.Name == fieldName)
            return row.Fields[position] is { Kind: XtFieldKind.Character } value ? value.Character : null;
        position++;
    }
    return null;
}

static void RecordSense(SortedDictionary<char, int> bucket, char? sense, string path, XtNode row, List<string> examples, string prefix)
{
    var key = sense ?? '?';
    bucket[key] = bucket.GetValueOrDefault(key) + 1;
    if (sense == '-' && examples.Count < 64)
        examples.Add($"{prefix}: {path} node index {row.Index}");
}

static string Describe(SortedDictionary<char, int> bucket)
    => string.Join(", ", bucket.Select(static pair => $"'{pair.Key}'={pair.Value}"));

static string GetScriptPath([CallerFilePath] string path = "") => path;
