#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=TopologyDump
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Runtime;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("usage: dotnet run scripts/XtDebug.cs -- PATH.x_t [--dump]");
    return 2;
}

var text = File.ReadAllText(args[0]);
try
{
    var nodes = XtText.Decode(text);
    Console.WriteLine($"decode ok: nodes={nodes.Length}");
    foreach (var group in nodes.GroupBy(node => node.Type).OrderBy(group => group.Key))
        Console.WriteLine($"  type {group.Key}: {group.Count()}");

    if (args.Length > 1 && args[1] == "--dump")
        DumpNodes(nodes);
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

static void DumpNodes(XtNode[] nodes)
{
    foreach (var node in nodes)
    {
        var descriptor = ProjectGmKernel.Native.Generated.XtSchema.GetNode(node.Type);
        Console.WriteLine($"{node.Type} {node.Index} {descriptor.Name}");
        var fields = ProjectGmKernel.Native.Generated.XtSchema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
        var valueIndex = 0;
        for (var i = 0; i < fields.Length; i++)
        {
            if (!fields[i].Transmit)
                continue;

            var value = node.Fields[valueIndex++];
            Console.WriteLine($"  {fields[i].Name}: {Format(value)}");
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
        _ => value.Integer.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
