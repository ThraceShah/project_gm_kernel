#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;

static string GetScriptPath([CallerFilePath] string path = "") => path;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("usage: dotnet run scripts/XtDebug.cs -- PATH.x_t [--dump]");
    return 2;
}

var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var inputPath = Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var text = File.ReadAllText(inputPath);
try
{
    var document = XtText.DecodeDocument(text);
    var nodes = document.Nodes;
    Console.WriteLine($"decode ok: schema={document.HeaderSchemaIdentity} nodes={nodes.Length} root={(nodes.Length == 0 ? "none" : nodes[0].Type + "/" + nodes[0].Index)}");
    try
    {
        var semantic = document.SemanticModel;
        Console.WriteLine($"semantic ok: roots={semantic.PartRoots.Length} extensions={semantic.VersionExtensions.Length}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"semantic failed: {exception.GetType().Name}: {exception.Message}");
    }
    foreach (var group in nodes.GroupBy(node => node.Type).OrderBy(group => group.Key))
        Console.WriteLine($"  type {group.Key}: {group.Count()}");

    if (args.Length > 1 && args[1] == "--dump")
        DumpNodes(document);
}
catch (Exception ex)
{
    Console.WriteLine($"decode failed: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

unsafe
{
    KernelRuntime.SessionStop();
    var options = new ProjectGmKernel.Native.Generated.PK_SESSION_start_o_s { o_t_version = 1 };
    var start = KernelRuntime.SessionStart(&options);
    if (start != 0)
    {
        Console.WriteLine($"session start failed: {start}");
        return start;
    }

    var error = XtReader.ReadText(text, out var parts);
    Console.WriteLine($"reader result: error={error} parts={parts.Length}");
    KernelRuntime.SessionStop();
    return error == 0 ? 0 : 1;
}

static void DumpNodes(XtDocument document)
{
    foreach (var node in document.Nodes)
    {
        var descriptor = document.Schema.GetNode(node.Type);
        Console.WriteLine($"{node.Type} {node.Index} {descriptor.Name}");
        var fields = document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
        var valueIndex = 0;
        for (var i = 0; i < fields.Length; i++)
        {
            if (!fields[i].Transmit)
                continue;

            var count = fields[i].ElementCount > 1
                ? fields[i].ElementCount
                : descriptor.Variable && fields[i].ElementCount == 1 ? node.VariableLength : 1;
            for (var index = 0; index < count; index++)
            {
                var value = node.Fields[valueIndex++];
                Console.WriteLine($"  {fields[i].Name}[{index}]: {Format(value)}");
            }
        }
    }
}

static string Format(XtFieldValue value)
{
    return value.Kind switch
    {
        XtFieldKind.Pointer => "ptr " + value.Pointer,
        XtFieldKind.Real => value.Real.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
        XtFieldKind.Character => "'" + value.Character + "'",
        XtFieldKind.Logical => value.Integer != 0 ? "T" : "F",
        XtFieldKind.Vector => $"({value.Vector.X:G17}, {value.Vector.Y:G17}, {value.Vector.Z:G17})",
        XtFieldKind.Interval => $"[{value.Vector.X:G17}, {value.Vector.Y:G17}]",
        XtFieldKind.Box => $"[{value.Vector.X:G17}, {value.Vector.Y:G17}]x[{value.Vector.Z:G17}, {value.Fourth:G17}]x[{value.Fifth:G17}, {value.Sixth:G17}]",
        _ => value.Integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
