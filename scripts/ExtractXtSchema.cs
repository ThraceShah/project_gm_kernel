#!/usr/bin/env dotnet run
// Prints Parasolid XT schema node/field specs for the locked project schema.
//
// Usage:
//   dotnet run scripts/ExtractXtSchema.cs
//   dotnet run scripts/ExtractXtSchema.cs -- --focus BODY,SHELL,FACE

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SchemaElementCount = System.Int32;
using SchemaFieldCount = System.Int32;
using SchemaNodeClass = System.Int32;
using SchemaNodeType = System.Int32;

static string GetScriptPath([CallerFilePath] string path = "") => path;

var scriptDir = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var repoRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."));
var schemaPath = Path.Combine(repoRoot, "third_party", "parasolid", "schema", "sch_37102.sch_txt");

if (!File.Exists(schemaPath))
    throw new FileNotFoundException("Locked XT schema file is missing.", schemaPath);

var focus = ParseFocus(args);
var nodes = ParseSchema(schemaPath);
var selected = focus.Count == 0
    ? nodes
    : nodes.Where(node => focus.Contains(node.Name, StringComparer.OrdinalIgnoreCase)).ToArray();

Console.WriteLine("Parasolid XT schema extract");
Console.WriteLine("  schema: SCH_3701000_37102");
Console.WriteLine("  source: third_party/parasolid/schema/sch_37102.sch_txt");
Console.WriteLine("  nodes:  " + selected.Length);
Console.WriteLine();

foreach (var node in selected.OrderBy(node => node.TypeId))
{
    Console.WriteLine($"{node.TypeId} {node.Name}; {node.Description}; transmit={Flag(node.Transmit)} fields={node.FieldCount} variable={Flag(node.Variable)}");
    foreach (var field in node.Fields)
    {
        Console.WriteLine($"  {field.Name}; {field.Type}; transmit={Flag(field.Transmit)} class={field.NodeClass} elements={field.ElementCount}");
    }

    Console.WriteLine();
}

if (focus.Count != 0)
{
    var missing = focus.Where(name => nodes.All(node => !StringComparer.OrdinalIgnoreCase.Equals(node.Name, name))).ToArray();
    if (missing.Length != 0)
    {
        Console.Error.WriteLine("Missing focused schema nodes: " + string.Join(", ", missing));
        return 1;
    }
}

return 0;

static HashSet<string> ParseFocus(string[] args)
{
    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] != "--focus")
            continue;

        if (i + 1 >= args.Length)
            throw new ArgumentException("--focus requires a comma-separated node list.");

        foreach (var name in args[++i].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            result.Add(name);
    }

    return result;
}

static SchemaNode[] ParseSchema(string path)
{
    var nodeRegex = new Regex(@"^(?<id>\d+)\s+(?<name>[A-Z0-9_]+);\s*(?<desc>[^;]*);\s*(?<transmit>[01])\s+(?<fields>\d+)\s+(?<variable>[01])\s*$", RegexOptions.Compiled);
    var fieldRegex = new Regex(@"^(?<name>[A-Za-z0-9_]+);\s*(?<type>[A-Za-z]);\s*(?<transmit>[01])\s+(?<class>\d+)\s+(?<elements>\d+)\s*$", RegexOptions.Compiled);
    var nodes = new List<SchemaNode>();
    SchemaNodeBuilder? current = null;

    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("**", StringComparison.Ordinal) || line.StartsWith(':'))
            continue;

        var nodeMatch = nodeRegex.Match(line);
        if (nodeMatch.Success)
        {
            Flush();
            current = new SchemaNodeBuilder(
                int.Parse(nodeMatch.Groups["id"].Value),
                nodeMatch.Groups["name"].Value,
                nodeMatch.Groups["desc"].Value,
                ParseBit(nodeMatch.Groups["transmit"].Value),
                int.Parse(nodeMatch.Groups["fields"].Value),
                ParseBit(nodeMatch.Groups["variable"].Value));
            continue;
        }

        var fieldMatch = fieldRegex.Match(line);
        if (fieldMatch.Success && current is not null)
        {
            current.Fields.Add(new SchemaField(
                fieldMatch.Groups["name"].Value,
                fieldMatch.Groups["type"].Value[0],
                ParseBit(fieldMatch.Groups["transmit"].Value),
                int.Parse(fieldMatch.Groups["class"].Value),
                int.Parse(fieldMatch.Groups["elements"].Value)));
        }
    }

    Flush();
    return nodes.ToArray();

    void Flush()
    {
        if (current is null)
            return;

        if (current.Fields.Count != current.FieldCount)
            throw new FormatException($"Schema node {current.Name} declares {current.FieldCount} fields but parsed {current.Fields.Count}.");

        nodes.Add(new SchemaNode(
            current.TypeId,
            current.Name,
            current.Description,
            current.Transmit,
            current.FieldCount,
            current.Variable,
            current.Fields.ToArray()));
        current = null;
    }
}

static bool ParseBit(string value) => value switch
{
    "0" => false,
    "1" => true,
    _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Expected schema bit 0 or 1."),
};

static string Flag(bool value) => value ? "1" : "0";

sealed class SchemaNodeBuilder
{
    public SchemaNodeBuilder(SchemaNodeType typeId, string name, string description, bool transmit, SchemaFieldCount fieldCount, bool variable)
    {
        TypeId = typeId;
        Name = name;
        Description = description;
        Transmit = transmit;
        FieldCount = fieldCount;
        Variable = variable;
    }

    public SchemaNodeType TypeId { get; }
    public string Name { get; }
    public string Description { get; }
    public bool Transmit { get; }
    public SchemaFieldCount FieldCount { get; }
    public bool Variable { get; }
    public List<SchemaField> Fields { get; } = new();
}

readonly record struct SchemaNode(
    SchemaNodeType TypeId,
    string Name,
    string Description,
    bool Transmit,
    SchemaFieldCount FieldCount,
    bool Variable,
    SchemaField[] Fields);

readonly record struct SchemaField(
    string Name,
    char Type,
    bool Transmit,
    SchemaNodeClass NodeClass,
    SchemaElementCount ElementCount);
