#!/usr/bin/env dotnet run
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using XtFieldValue = ProjectGmKernel.Xt.XtFieldValue;
using XtFieldKind = ProjectGmKernel.Xt.XtFieldKind;

static string GetScriptPath([CallerFilePath] string path = "") => path;

if (args.Length is not 1 and not 2)
{
    Console.Error.WriteLine("usage: dotnet run scripts/DumpXtNodes.cs -- INPUT.x_t [SCHEMA_IDENTITY]");
    return 2;
}

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var inputPath = Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var bytes = File.ReadAllBytes(inputPath);
if (args.Length == 2)
    bytes = XtCorpusInspection.Transcode(bytes, args[1]);
var document = XtText.DecodeDocument(System.Text.Encoding.ASCII.GetString(bytes));

Console.WriteLine($"schema={document.Schema.Identity} header={document.HeaderSchemaIdentity} nodes={document.Nodes.Length}");
foreach (var node in document.Nodes)
{
    var descriptor = document.Schema.GetNode(node.Type);
    Console.WriteLine($"{descriptor.Name} type={node.Type} index={node.Index} variable={node.VariableLength}");
    var offset = 0;
    var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var field in document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
    {
        if (!field.Transmit)
            continue;
        var count = field.ElementCount > 1
            ? field.ElementCount
            : descriptor.Variable && field.ElementCount == 1 ? node.VariableLength : 1;
        var occurrence = occurrences.TryGetValue(field.Name, out var seen) ? seen : 0;
        occurrences[field.Name] = occurrence + 1;
        var values = new string[count];
        for (var index = 0; index < count; index++)
            values[index] = Format(node.Fields[offset + index]);
        offset += count;
        Console.WriteLine($"  {field.Name}[{occurrence}] {field.Type} = {string.Join(' ', values)}");
    }
}

return 0;

static string Format(XtFieldValue value) => value.Kind switch
{
    XtFieldKind.Empty => "?",
    XtFieldKind.Pointer => "p:" + value.Pointer.ToString(CultureInfo.InvariantCulture),
    XtFieldKind.Integer => "i:" + value.Integer.ToString(CultureInfo.InvariantCulture),
    XtFieldKind.Unsigned => "u:" + value.Integer.ToString(CultureInfo.InvariantCulture),
    XtFieldKind.Real => "r:" + value.Real.ToString("R", CultureInfo.InvariantCulture),
    XtFieldKind.Character => "c:" + value.Character,
    XtFieldKind.Logical => value.Integer == 0 ? "l:false" : "l:true",
    XtFieldKind.Vector => FormattableString.Invariant($"v:{value.Vector.X:R},{value.Vector.Y:R},{value.Vector.Z:R}"),
    XtFieldKind.Interval => FormattableString.Invariant($"i:{value.Vector.X:R},{value.Vector.Y:R}"),
    XtFieldKind.Box => FormattableString.Invariant($"b:{value.Vector.X:R},{value.Vector.Y:R},{value.Vector.Z:R},{value.Fourth:R},{value.Fifth:R},{value.Sixth:R}"),
    _ => value.Kind.ToString(),
};
