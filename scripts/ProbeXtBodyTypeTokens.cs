#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=TopologyDump
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using static parasolid;

static string GetScriptPath([CallerFilePath] string path = "") => path;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run scripts/ProbeXtBodyTypeTokens.cs -- INPUT.x_t");
    return 2;
}
var scriptDirectory = Path.GetDirectoryName(GetScriptPath()) ?? ".";
var inputPath = Path.GetFullPath(Path.Combine(scriptDirectory, args[0]));
var document = XtText.DecodeDocument(File.ReadAllText(inputPath));
var body = document.Nodes.First(node => document.Schema.GetNode(node.Type).Name == "BODY");
var descriptor = document.Schema.GetNode(body.Type);
var bodyTypeOffset = FindFieldOffset(document.Schema, descriptor, body, "body_type");
if (bodyTypeOffset < 0)
    throw new FormatException("BODY.body_type is absent from the target schema");

if (!ParasolidScriptHost.TryStartSession("XT body-type token probe", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}
unsafe
{
    using (session)
    {
        for (var token = 0; token <= 12; token++)
        {
            body.Fields[bodyTypeOffset] = XtFieldValue.Int(token);
            var bytes = Encoding.ASCII.GetBytes(XtText.Encode(document));
            fixed (byte* pointer = bytes)
            {
                var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
                var options = new PK_PART_receive_o_t
                {
                    o_t_version = 8,
                    transmit_format = PK_transmit_format_text_c,
                    receive_compound = PK_receive_compound_keep_c,
                };
                int count;
                PK_PART_t* parts;
                var error = PK_PART_receive_b(block, &options, &count, &parts);
                if (error != PK_ERROR_no_errors)
                {
                    Console.WriteLine($"token={token} receive_error={error}");
                    continue;
                }
                try
                {
                    PK_BODY_type_t bodyType = 0;
                    var askError = count == 1 ? PK_BODY_ask_type(parts[0], &bodyType) : PK_ERROR_bad_field_number;
                    Console.WriteLine($"token={token} parts={count} ask_error={askError} body_type={(askError == 0 ? bodyType : 0)}");
                }
                finally
                {
                    if (parts is not null)
                        ParasolidScriptHost.Check(PK_MEMORY_free(parts), "PK_MEMORY_free parts");
                }
            }
        }
    }
}
return 0;

static int FindFieldOffset(XtSchemaDefinition schema, XtNodeDescriptor descriptor, XtNode node, string name)
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
