#!/usr/bin/env dotnet run
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using XtSchemaDefinition = ProjectGmKernel.Xt.XtSchemaDefinition;
using XtNodeDescriptor = ProjectGmKernel.Xt.XtNodeDescriptor;
using XtFieldDescriptor = ProjectGmKernel.Xt.XtFieldDescriptor;
using SchemaFieldOccurrence = System.Int32;
using SchemaModelerVersion = System.Int32;
using SchemaNumber = System.Int32;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repositoryRoot = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));
var jsonPath = Path.Combine(repositoryRoot, "tests", "ParasolidXtCorpus", "coverage", "xt-schema-differences.json");
var markdownPath = Path.Combine(repositoryRoot, "docs", "xt_schema_differences.md");
var check = args.Contains("--check", StringComparer.Ordinal);

var schemas = Enumerable.Range(0, XtSchemaRegistry.Count)
    .Select(XtSchemaRegistry.GetByIndex)
    .OrderBy(static schema => schema.ModelerVersion)
    .ThenBy(static schema => schema.SchemaNumber)
    .ThenBy(static schema => schema.Identity, StringComparer.Ordinal)
    .ToArray();
var differences = new List<SchemaDifference>(Math.Max(0, schemas.Length - 1));
for (var index = 1; index < schemas.Length; index++)
    differences.Add(Compare(schemas[index - 1], schemas[index]));

var json = SerializeJson(schemas.Length, differences);
var markdown = SerializeMarkdown(schemas.Length, differences);
if (check)
{
    if (!File.Exists(jsonPath) || !json.AsSpan().SequenceEqual(File.ReadAllBytes(jsonPath)) ||
        !File.Exists(markdownPath) || !StringComparer.Ordinal.Equals(markdown, File.ReadAllText(markdownPath)))
    {
        Console.Error.WriteLine("XT schema difference outputs are missing or out of date. Run dotnet run scripts/GenerateXtSchemaDifferences.cs.");
        return 1;
    }
    Console.WriteLine($"XT schema differences are up to date: schemas={schemas.Length} transitions={differences.Count}");
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
File.WriteAllBytes(jsonPath, json);
File.WriteAllText(markdownPath, markdown, new UTF8Encoding(false));
Console.WriteLine("Generated " + Path.GetRelativePath(repositoryRoot, jsonPath));
Console.WriteLine("Generated " + Path.GetRelativePath(repositoryRoot, markdownPath));
return 0;

static SchemaDifference Compare(XtSchemaDefinition from, XtSchemaDefinition to)
{
    var changes = new List<SchemaChange>();
    var fromNodes = from.Nodes.ToArray().ToDictionary(static node => node.Type);
    var toNodes = to.Nodes.ToArray().ToDictionary(static node => node.Type);
    foreach (var type in fromNodes.Keys.Concat(toNodes.Keys).Distinct().Order())
    {
        if (!fromNodes.TryGetValue(type, out var fromNode))
        {
            changes.Add(new SchemaChange("NodeAdded", $"{type}:{toNodes[type].Name}", "", NodeSignature(toNodes[type])));
            continue;
        }
        if (!toNodes.TryGetValue(type, out var toNode))
        {
            changes.Add(new SchemaChange("NodeRemoved", $"{type}:{fromNode.Name}", NodeSignature(fromNode), ""));
            continue;
        }
        if (NodeSignature(fromNode) != NodeSignature(toNode))
            changes.Add(new SchemaChange("NodeChanged", $"{type}:{fromNode.Name}->{toNode.Name}", NodeSignature(fromNode), NodeSignature(toNode)));

        var fromFields = IndexFields(from, fromNode);
        var toFields = IndexFields(to, toNode);
        foreach (var key in fromFields.Keys.Concat(toFields.Keys).Distinct().OrderBy(static key => key.Name, StringComparer.Ordinal).ThenBy(static key => key.Occurrence))
        {
            var path = $"{type}:{toNode.Name}.{key.Name}[{key.Occurrence}]";
            if (!fromFields.TryGetValue(key, out var fromField))
                changes.Add(new SchemaChange("FieldAdded", path, "", FieldSignature(toFields[key])));
            else if (!toFields.TryGetValue(key, out var toField))
                changes.Add(new SchemaChange("FieldRemoved", path, FieldSignature(fromField), ""));
            else if (FieldSignature(fromField) != FieldSignature(toField))
                changes.Add(new SchemaChange("FieldChanged", path, FieldSignature(fromField), FieldSignature(toField)));
        }
    }
    return new SchemaDifference(from.Identity, to.Identity, to.ModelerVersion, to.SchemaNumber, changes.ToArray());
}

static Dictionary<FieldKey, XtFieldDescriptor> IndexFields(XtSchemaDefinition schema, XtNodeDescriptor node)
{
    var result = new Dictionary<FieldKey, XtFieldDescriptor>();
    var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var field in schema.Fields.Slice(node.FieldOffset, node.ParsedFieldCount))
    {
        var occurrence = occurrences.TryGetValue(field.Name, out var seen) ? seen : 0;
        occurrences[field.Name] = occurrence + 1;
        result.Add(new FieldKey(field.Name, occurrence), field);
    }
    return result;
}

static string NodeSignature(XtNodeDescriptor node)
    => $"name={node.Name};transmit={(node.Transmit ? 1 : 0)};variable={(node.Variable ? 1 : 0)};fields={node.ParsedFieldCount}";

static string FieldSignature(XtFieldDescriptor field)
    => $"type={field.Type};transmit={(field.Transmit ? 1 : 0)};class={field.NodeClass};elements={field.ElementCount}";

static byte[] SerializeJson(int schemaCount, List<SchemaDifference> differences)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        writer.WriteStartObject();
        writer.WriteString("generator", "xt-schema-differences-v1");
        writer.WriteNumber("schemaCount", schemaCount);
        writer.WriteNumber("transitionCount", differences.Count);
        writer.WriteStartArray("transitions");
        foreach (var difference in differences)
        {
            writer.WriteStartObject();
            writer.WriteString("from", difference.From);
            writer.WriteString("to", difference.To);
            writer.WriteNumber("modelerVersion", difference.ModelerVersion);
            writer.WriteNumber("schemaNumber", difference.SchemaNumber);
            writer.WriteNumber("nodeAdded", difference.Count("NodeAdded"));
            writer.WriteNumber("nodeRemoved", difference.Count("NodeRemoved"));
            writer.WriteNumber("nodeChanged", difference.Count("NodeChanged"));
            writer.WriteNumber("fieldAdded", difference.Count("FieldAdded"));
            writer.WriteNumber("fieldRemoved", difference.Count("FieldRemoved"));
            writer.WriteNumber("fieldChanged", difference.Count("FieldChanged"));
            writer.WriteStartArray("changes");
            foreach (var change in difference.Changes)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", change.Kind);
                writer.WriteString("path", change.Path);
                writer.WriteString("from", change.From);
                writer.WriteString("to", change.To);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
    return stream.ToArray();
}

static string SerializeMarkdown(int schemaCount, List<SchemaDifference> differences)
{
    var text = new StringBuilder(32 * 1024)
        .AppendLine("# Parasolid x_t schema 相邻版本差异")
        .AppendLine()
        .AppendLine("本文件由 `scripts/GenerateXtSchemaDifferences.cs` 生成。完整逐字段明细位于")
        .AppendLine("`tests/ParasolidXtCorpus/coverage/xt-schema-differences.json`；禁止手工维护两份全集。")
        .AppendLine()
        .Append("当前共 ").Append(schemaCount).Append(" 个 schema、").Append(differences.Count).AppendLine(" 个按 modeller version 排序的相邻差异。")
        .AppendLine()
        .AppendLine("| From | To | +Node | -Node | ΔNode | +Field | -Field | ΔField |")
        .AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
    foreach (var difference in differences)
    {
        text.Append("| `").Append(difference.From).Append("` | `").Append(difference.To).Append("` | ")
            .Append(difference.Count("NodeAdded")).Append(" | ")
            .Append(difference.Count("NodeRemoved")).Append(" | ")
            .Append(difference.Count("NodeChanged")).Append(" | ")
            .Append(difference.Count("FieldAdded")).Append(" | ")
            .Append(difference.Count("FieldRemoved")).Append(" | ")
            .Append(difference.Count("FieldChanged")).AppendLine(" |");
    }
    return text.ToString();
}

readonly record struct FieldKey(string Name, SchemaFieldOccurrence Occurrence);
readonly record struct SchemaChange(string Kind, string Path, string From, string To);
sealed record SchemaDifference(string From, string To, SchemaModelerVersion ModelerVersion, SchemaNumber SchemaNumber, SchemaChange[] Changes)
{
    public int Count(string kind) => Changes.Count(change => change.Kind == kind);
}
