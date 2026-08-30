#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=TopologyDump
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using System.Runtime.CompilerServices;
using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

static string GetScriptPath([CallerFilePath] string path = "") => path;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run scripts/ProbeXtLegacySheet.cs -- INPUT.x_t");
    return 2;
}
var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var inputPath = Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var source = File.ReadAllBytes(inputPath);

if (!ParasolidScriptHost.TryStartSession("XT legacy sheet probe", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

unsafe
{
    using (session)
    {
        for (var bodyType = 1; bodyType <= 5; bodyType++)
        for (var removeOutsideFins = 0; removeOutsideFins <= 1; removeOutsideFins++)
        for (var clearShellTopol = 0; clearShellTopol <= 1; clearShellTopol++)
        foreach (var faceSense in new[] { '+', '-', '?' })
        {
            var document = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
            document = Mutate(document, bodyType, removeOutsideFins != 0, clearShellTopol != 0, faceSense);
            var bytes = Encoding.ASCII.GetBytes(XtText.Encode(document));
            fixed (byte* pointer = bytes)
            {
                var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
                var options = new PK_PART_receive_o_t
                {
                    o_t_version = 8,
                    transmit_format = PK_transmit_format_text_c,
                    attdef_mismatch = PK_ATTDEF_mismatch_ignore_c,
                    receive_compound = PK_receive_compound_keep_c,
                    receive_using_seek = PK_receive_using_seek_no_c,
                    receive_mixed = PK_receive_mixed_fail_c,
                };
                int count;
                PK_PART_t* parts;
                var error = PK_PART_receive_b(block, &options, &count, &parts);
                if (error != PK_ERROR_no_errors)
                    continue;
                PK_BODY_type_t receivedType = 0;
                var askError = count == 1 ? PK_BODY_ask_type(parts[0], &receivedType) : PK_ERROR_bad_field_number;
                Console.WriteLine($"PASS token={bodyType} remove-outside={removeOutsideFins} clear-shell={clearShellTopol} face-sense={faceSense} count={count} ask={askError} type={(askError == 0 ? receivedType : 0)}");
                Check(PK_ENTITY_delete(count, parts), "PK_ENTITY_delete received parts");
                Check(PK_MEMORY_free(parts), "PK_MEMORY_free received parts");
            }
        }
    }
}
return 0;

static XtDocument Mutate(XtDocument document, int bodyType, bool removeOutsideFins, bool clearShellTopol, char faceSense)
{
    foreach (var node in document.Nodes)
    {
        var descriptor = document.Schema.GetNode(node.Type);
        if (descriptor.Name == "BODY")
            Set(document.Schema, descriptor, node, "body_type", XtFieldValue.Int(bodyType));
        else if (descriptor.Name == "FACE")
            Set(document.Schema, descriptor, node, "sense", XtFieldValue.Char(faceSense));
        else if (descriptor.Name == "SHELL" && clearShellTopol)
        {
            Set(document.Schema, descriptor, node, "edge", XtFieldValue.Ptr(0));
            Set(document.Schema, descriptor, node, "vertex", XtFieldValue.Ptr(0));
        }
    }
    if (!removeOutsideFins)
        return document;

    var removed = document.Nodes
        .Where(node => document.Schema.GetNode(node.Type).Name == "HALFEDGE" && GetPointer(document.Schema, node, "loop") == 0)
        .Select(static node => node.Index)
        .ToHashSet();
    foreach (var node in document.Nodes)
    {
        var descriptor = document.Schema.GetNode(node.Type);
        if (descriptor.Name == "HALFEDGE" && removed.Contains(GetPointer(document.Schema, node, "other")))
            Set(document.Schema, descriptor, node, "other", XtFieldValue.Ptr(0));
        if (descriptor.Name == "EDGE" && removed.Contains(GetPointer(document.Schema, node, "halfedge")))
        {
            var removedFin = document.Nodes.First(fin => fin.Index == GetPointer(document.Schema, node, "halfedge"));
            Set(document.Schema, descriptor, node, "halfedge", XtFieldValue.Ptr(GetPointer(document.Schema, removedFin, "other")));
        }
    }
    return new XtDocument
    {
        PhysicalHeader = document.PhysicalHeader,
        VersionText = document.VersionText,
        HeaderSchemaIdentity = document.HeaderSchemaIdentity,
        Schema = document.Schema,
        BaseSchema = document.BaseSchema,
        EmbeddedMaxNodeType = document.EmbeddedMaxNodeType,
        UserFieldSize = document.UserFieldSize,
        Nodes = document.Nodes.Where(node => !removed.Contains(node.Index)).ToArray(),
    };
}

static int GetPointer(XtSchemaDefinition schema, XtNode node, string name)
{
    var descriptor = schema.GetNode(node.Type);
    var offset = FindOffset(schema, descriptor, node, name);
    return offset < 0 ? 0 : node.Fields[offset].Pointer;
}

static void Set(XtSchemaDefinition schema, XtNodeDescriptor descriptor, XtNode node, string name, XtFieldValue value)
{
    var offset = FindOffset(schema, descriptor, node, name);
    if (offset >= 0)
        node.Fields[offset] = value;
}

static int FindOffset(XtSchemaDefinition schema, XtNodeDescriptor descriptor, XtNode node, string name)
{
    var valueOffset = 0;
    foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
    {
        if (!field.Transmit)
            continue;
        var count = field.ElementCount > 1
            ? field.ElementCount
            : descriptor.Variable && field.ElementCount == 1 ? Math.Max(0, node.VariableLength) : 1;
        if (field.Name == name)
            return count == 1 ? valueOffset : -1;
        valueOffset += count;
    }
    return -1;
}

static void Check(int error, string operation)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(operation + " failed with error " + error);
}
