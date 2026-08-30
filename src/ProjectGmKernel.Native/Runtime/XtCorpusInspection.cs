using System.Text;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>Stable schema-node summary used by the Parasolid XT corpus inspector.</summary>
public readonly record struct XtCorpusSchemaNode(string Name, int Count);

/// <summary>Stable pointer dependency between two XT schema nodes.</summary>
public readonly record struct XtCorpusSchemaDependency(string From, string To);

/// <summary>Schema inventory extracted from one text XT transmit.</summary>
public sealed record XtCorpusInventory(
    XtCorpusSchemaNode[] Nodes,
    XtCorpusSchemaDependency[] Dependencies);

public readonly record struct XtCorpusSupportedSchema(
    string Identity,
    string SourceFileName,
    string DescriptorSha256,
    int ModelerVersion,
    int SchemaNumber);

/// <summary>Read-only bridge to the production XT parser for corpus validation.</summary>
public static class XtCorpusInspection
{
    public static int GetUserFieldSize(ReadOnlySpan<byte> source)
        => XtText.DecodeDocument(Encoding.ASCII.GetString(source)).UserFieldSize;

    public static XtCorpusSupportedSchema[] GetSupportedSchemas()
    {
        var result = new XtCorpusSupportedSchema[XtSchemaRegistry.Count];
        for (var index = 0; index < result.Length; index++)
        {
            var schema = XtSchemaRegistry.GetByIndex(index);
            result[index] = new XtCorpusSupportedSchema(
                schema.Identity,
                XtSchemaRegistry.Registrations[index].ResourceFileName,
                ComputeSchemaDescriptorHash(schema),
                schema.ModelerVersion,
                schema.SchemaNumber);
        }
        return result;
    }

    public static int GetCompatibleTransmitVersion(int modelerVersion)
    {
        if (modelerVersion <= 0)
            return 0;
        var major = modelerVersion >= 100000 ? modelerVersion / 100000 : modelerVersion / 100;
        var minor = modelerVersion >= 100000 ? modelerVersion % 100000 / 1000 : modelerVersion % 100 / 10;
        var first = major * 10 + minor;
        var last = major * 10;
        for (var version = first; version >= last; version--)
        {
            if (XtSchemaRegistry.TryResolveTransmitVersion(version, out _))
                return version;
        }
        return 0;
    }

    private static string ComputeSchemaDescriptorHash(XtSchemaDefinition schema)
    {
        var canonical = new StringBuilder(schema.Fields.Length * 32);
        foreach (var node in schema.Nodes)
        {
            canonical.Append(node.Type).Append('|').Append(node.Name).Append('|')
                .Append(node.Transmit ? '1' : '0').Append('|').Append(node.Variable ? '1' : '0').Append('\n');
            foreach (var field in schema.Fields.Slice(node.FieldOffset, node.ParsedFieldCount))
            {
                canonical.Append(field.Name).Append('|').Append(field.Type).Append('|')
                    .Append(field.Transmit ? '1' : '0').Append('|').Append(field.NodeClass).Append('|')
                    .Append(field.ElementCount).Append('\n');
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    public static byte[] Transcode(ReadOnlySpan<byte> source, string targetSchemaIdentity)
    {
        var document = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
        var target = XtSchemaRegistry.Resolve(targetSchemaIdentity);
        return Encoding.ASCII.GetBytes(XtText.Encode(XtSchemaTranscoder.Transcode(document, target)));
    }

    public static bool CanTranscode(ReadOnlySpan<byte> source, string targetSchemaIdentity)
    {
        var document = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
        var target = XtSchemaRegistry.Resolve(targetSchemaIdentity);
        return XtSchemaTranscoder.CanTranscode(document, target);
    }

    public static string? GetTranscodeIncompatibility(ReadOnlySpan<byte> source, string targetSchemaIdentity)
    {
        var document = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
        var target = XtSchemaRegistry.Resolve(targetSchemaIdentity);
        return XtSchemaTranscoder.GetIncompatibility(document, target);
    }

    public static string ComputeStructuralHash(ReadOnlySpan<byte> source)
    {
        var document = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
        var canonical = new StringBuilder(document.Nodes.Length * 128)
            .Append("user-fields=").Append(document.UserFieldSize).Append('\n');
        foreach (var node in document.Nodes)
        {
            canonical.Append("node=").Append(node.Type).Append(',').Append(node.Index).Append(',').Append(node.VariableLength).Append('\n');
            foreach (var field in node.Fields)
            {
                canonical.Append((int)field.Kind).Append(':')
                    .Append(field.Integer).Append(':')
                    .Append(field.Pointer).Append(':')
                    .Append((int)field.Character).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Real)).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Vector.X)).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Vector.Y)).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Vector.Z)).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Fourth)).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Fifth)).Append(':')
                    .Append(BitConverter.DoubleToInt64Bits(field.Sixth)).Append('\n');
            }
            foreach (var userField in node.UserFields)
                canonical.Append("user=").Append(userField).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    public static byte[] EmbedWithBaseSchema(ReadOnlySpan<byte> source, string baseSchemaIdentity)
    {
        var document = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
        var baseSchema = XtSchemaRegistry.Resolve(baseSchemaIdentity);
        return Encoding.ASCII.GetBytes(XtText.EncodeWithBaseSchema(document, baseSchema));
    }

    public static XtCorpusInventory Inspect(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var document = XtText.DecodeDocument(text);
        var nodes = document.Nodes;
        var byIndex = new Dictionary<int, XtNode>(nodes.Length);
        foreach (var node in nodes)
            byIndex[node.Index] = node;

        var nodeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependencies = new HashSet<XtCorpusSchemaDependency>();
        foreach (var node in nodes)
        {
            var descriptor = document.Schema.GetNode(node.Type);
            var name = descriptor.Name.Length == 0 ? "UNKNOWN_" + node.Type : descriptor.Name;
            nodeCounts[name] = nodeCounts.TryGetValue(name, out var count) ? count + 1 : 1;
            foreach (var field in node.Fields)
            {
                if (field.Kind != XtFieldKind.Pointer || field.Pointer == 0 || !byIndex.TryGetValue(field.Pointer, out var target))
                    continue;
                var targetDescriptor = document.Schema.GetNode(target.Type);
                var targetName = targetDescriptor.Name.Length == 0 ? "UNKNOWN_" + target.Type : targetDescriptor.Name;
                dependencies.Add(new XtCorpusSchemaDependency(name + "#" + node.Index, targetName + "#" + target.Index));
            }
        }

        var nodeSummary = nodeCounts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new XtCorpusSchemaNode(pair.Key, pair.Value))
            .ToArray();
        var dependencySummary = dependencies
            .OrderBy(edge => edge.From, StringComparer.Ordinal)
            .ThenBy(edge => edge.To, StringComparer.Ordinal)
            .ToArray();
        return new XtCorpusInventory(nodeSummary, dependencySummary);
    }

    public static unsafe byte[] RoundTrip(
        ReadOnlySpan<byte> source,
        int transmitVersion,
        bool userFields = true,
        bool keepCompound = false)
    {
        var sourceDocument = XtText.DecodeDocument(Encoding.ASCII.GetString(source));
        var startOptions = new PK_SESSION_start_o_s
        {
            o_t_version = 1,
            user_field = userFields ? sourceDocument.UserFieldSize : 0,
        };
        Check(KernelRuntime.SessionStart(&startOptions), "PK_SESSION_start");
        try
        {
            fixed (byte* sourcePointer = source)
            {
                var input = new PK_MEMORY_block_s { bytes = sourcePointer, n_bytes = (nuint)source.Length };
                var receiveOptions = new PK_PART_receive_o_s
                {
                    o_t_version = 8,
                    transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                    receive_user_fields = userFields ? (byte)1 : (byte)0,
                    attdef_mismatch = ParasolidConstants.PK_ATTDEF_mismatch_fail_c,
                    receive_compound = keepCompound
                        ? ParasolidConstants.PK_receive_compound_keep_c
                        : ParasolidConstants.PK_receive_compound_split_c,
                    receive_using_seek = ParasolidConstants.PK_receive_using_seek_no_c,
                    receive_mixed = ParasolidConstants.PK_receive_mixed_fail_c,
                };
                int partCount;
                int* parts;
                Check(KernelRuntime.PartReceiveB(input, &receiveOptions, &partCount, &parts), "PK_PART_receive_b");
                try
                {
                    var transmitOptions = new PK_PART_transmit_o_s
                    {
                        o_t_version = 4,
                        transmit_format = ParasolidConstants.PK_transmit_format_text_c,
                        transmit_version = transmitVersion,
                        transmit_user_fields = userFields ? (byte)1 : (byte)0,
                        transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
                    };
                    var output = new PK_MEMORY_block_s();
                    Check(KernelRuntime.PartTransmitB(partCount, parts, &transmitOptions, &output), "PK_PART_transmit_b");
                    try
                    {
                        using var stream = new MemoryStream();
                        for (var current = &output; current is not null; current = current->next)
                        {
                            if (current->bytes is not null && current->n_bytes != 0)
                                stream.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
                        }
                        return stream.ToArray();
                    }
                    finally
                    {
                        Check(KernelRuntime.MemoryBlockFree(&output), "PK_MEMORY_block_f");
                    }
                }
                finally
                {
                    if (parts is not null)
                        Check(KernelRuntime.MemoryFree(parts), "PK_MEMORY_free");
                }
            }
        }
        finally
        {
            Check(KernelRuntime.SessionStop(), "PK_SESSION_stop");
        }

        static void Check(int code, string operation)
        {
            if (code != ParasolidConstants.PK_ERROR_no_errors)
                throw new InvalidOperationException(operation + " failed with error " + code);
        }
    }
}
