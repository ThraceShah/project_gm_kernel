using System.Globalization;
using System.Text;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static class XtText
{
    private const int CurrentTransmitModelerVersion = 3800150;

    public static string Encode(IReadOnlyList<XtNode> nodes)
        => EncodeCurrent(nodes, 371);

    public static string EncodeCurrent(IReadOnlyList<XtNode> nodes, int transmitVersion)
    {
        var schema = XtSchemaRegistry.Resolve(XtSchema.SchemaName);
        var nodeArray = nodes as XtNode[] ?? nodes.ToArray();
        if (transmitVersion == 0)
        {
            var baseSchema = XtSchemaRegistry.ResolveBySchemaNumber(13006);
            var definitions = new Dictionary<int, EmbeddedNodeDefinition>();
            foreach (var node in nodeArray)
            {
                if (definitions.ContainsKey(node.Type))
                    continue;
                var descriptor = schema.GetNode(node.Type);
                if (descriptor.Type == 0)
                    throw new InvalidOperationException($"Unknown XT node type {node.Type}.");
                definitions.Add(node.Type, CreateEmbeddedDefinition(
                    node.Type,
                    descriptor.Name,
                    descriptor.Description,
                    EffectiveFields(schema, descriptor)));
            }
            var identity = $"SCH_{CurrentTransmitModelerVersion}_{schema.SchemaNumber}_{baseSchema.SchemaNumber}";
            var effectiveSchema = BuildEmbeddedSchema(
                identity,
                CurrentTransmitModelerVersion,
                schema.SchemaNumber,
                definitions.Values);
            var maxNodeType = 1;
            foreach (var descriptor in schema.Nodes)
                maxNodeType = Math.Max(maxNodeType, descriptor.Type + 1);
            return Encode(new XtDocument
            {
                VersionText = $": TRANSMIT FILE created by modeller version {CurrentTransmitModelerVersion}",
                HeaderSchemaIdentity = identity,
                Schema = effectiveSchema,
                BaseSchema = baseSchema,
                EmbeddedMaxNodeType = maxNodeType,
                UserFieldSize = 0,
                Nodes = nodeArray,
            });
        }

        var headerIdentity = transmitVersion switch
        {
            371 => XtSchema.SchemaName,
            380 => $"SCH_{CurrentTransmitModelerVersion}_{schema.SchemaNumber}",
            _ => null,
        };
        if (headerIdentity is null)
        {
            if (!XtSchemaRegistry.TryResolveTransmitVersion(transmitVersion, out var targetSchema))
                throw new NotSupportedException($"Unsupported XT transmit version {transmitVersion}.");
            var sourceDocument = new XtDocument
            {
                VersionText = $": TRANSMIT FILE created by modeller version {CurrentTransmitModelerVersion}",
                HeaderSchemaIdentity = XtSchema.SchemaName,
                Schema = schema,
                UserFieldSize = 0,
                Nodes = nodeArray,
            };
            return Encode(XtSchemaTranscoder.Transcode(sourceDocument, targetSchema));
        }
        return Encode(new XtDocument
        {
            VersionText = $": TRANSMIT FILE created by modeller version {CurrentTransmitModelerVersion}",
            HeaderSchemaIdentity = headerIdentity,
            Schema = schema,
            UserFieldSize = 0,
            Nodes = nodeArray,
        });
    }

    public static bool TryEncodeForTransmitVersion(XtDocument document, int transmitVersion, out string text, bool includeUserFields = true)
    {
        text = "";
        document = SelectUserFields(document, includeUserFields);
        if (document.Schema.SchemaNumber != XtSchema.SchemaNumber)
        {
            if (transmitVersion == 0)
            {
                var currentSchema = XtSchemaRegistry.Resolve(XtSchema.SchemaName);
                if (!XtSchemaTranscoder.CanTranscode(document, currentSchema))
                    return false;
                var currentDocument = XtSchemaTranscoder.Transcode(document, currentSchema);
                text = EncodeWithBaseSchema(currentDocument, XtSchemaRegistry.ResolveBySchemaNumber(13006));
                return true;
            }
            if (!XtSchemaRegistry.TryResolveTransmitVersion(transmitVersion, out var historicTarget))
                return false;
            text = Encode(XtSchemaTranscoder.Transcode(document, historicTarget));
            return true;
        }

        if (transmitVersion == 0)
        {
            if (document.BaseSchema is not null)
            {
                text = Encode(document);
                return true;
            }
            text = EncodeWithBaseSchema(document, XtSchemaRegistry.ResolveBySchemaNumber(13006));
            return true;
        }

        var headerIdentity = transmitVersion switch
        {
            371 => XtSchema.SchemaName,
            380 => $"SCH_{CurrentTransmitModelerVersion}_{XtSchema.SchemaNumber}",
            _ => null,
        };
        if (headerIdentity is null)
        {
            if (!XtSchemaRegistry.TryResolveTransmitVersion(transmitVersion, out var targetSchema))
                return false;
            text = Encode(XtSchemaTranscoder.Transcode(document, targetSchema));
            return true;
        }
        text = Encode(new XtDocument
        {
            VersionText = $": TRANSMIT FILE created by modeller version {CurrentTransmitModelerVersion}",
            HeaderSchemaIdentity = headerIdentity,
            Schema = document.Schema,
            UserFieldSize = document.UserFieldSize,
            Nodes = document.Nodes,
        });
        return true;
    }

    public static XtDocument SelectUserFields(XtDocument document, bool include)
    {
        if (include || document.UserFieldSize == 0)
            return document;
        var nodes = new XtNode[document.Nodes.Length];
        for (var index = 0; index < nodes.Length; index++)
        {
            var source = document.Nodes[index];
            nodes[index] = new XtNode
            {
                Type = source.Type,
                Index = source.Index,
                VariableLength = source.VariableLength,
                Fields = source.Fields,
                UserFields = [],
            };
        }
        return new XtDocument
        {
            PhysicalHeader = document.PhysicalHeader,
            VersionText = document.VersionText,
            HeaderSchemaIdentity = document.HeaderSchemaIdentity,
            Schema = document.Schema,
            BaseSchema = document.BaseSchema,
            EmbeddedMaxNodeType = document.EmbeddedMaxNodeType,
            UserFieldSize = 0,
            Nodes = nodes,
        };
    }

    public static bool TrySelectPartRoots(XtDocument document, ReadOnlySpan<XtNodeIndex> rootIndexes, out XtDocument selected)
    {
        selected = document;
        if (document.Nodes.Length == 0 || rootIndexes.Length == 0)
            return false;

        var first = document.Nodes[0];
        var firstDescriptor = document.Schema.GetNode(first.Type);
        if (firstDescriptor.Name != "PART_XMT_BLOCK")
            return XtPartGraph.GetRootIndexes(document).AsSpan().SequenceEqual(rootIndexes);

        var descriptor = firstDescriptor;
        if (descriptor.Type == 0 || !descriptor.Variable)
            return false;
        var fields = document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
        var oldEntries = ReadPartBlockEntries(first, descriptor, fields);
        if (oldEntries is null)
            return false;
        foreach (var rootIndex in rootIndexes)
        {
            if (!oldEntries.Contains(rootIndex))
                return false;
        }
        if (oldEntries.AsSpan().SequenceEqual(rootIndexes))
            return true;

        var values = new List<XtFieldValue>(first.Fields.Length - oldEntries.Length + rootIndexes.Length);
        var valueOffset = 0;
        foreach (var field in fields)
        {
            if (!field.Transmit)
                continue;
            var oldCount = field.ElementCount > 1
                ? field.ElementCount
                : field.ElementCount == 1 ? first.VariableLength : 1;
            if (field.Name == "n_entries" && field.ElementCount == 0)
            {
                values.Add(XtFieldValue.Int(rootIndexes.Length));
            }
            else if (field.Name == "entries" && field.ElementCount == 1 && field.Type == 'p')
            {
                foreach (var rootIndex in rootIndexes)
                    values.Add(XtFieldValue.Ptr(rootIndex));
            }
            else
            {
                if (field.ElementCount == 1 && rootIndexes.Length != first.VariableLength)
                    return false;
                for (var index = 0; index < oldCount; index++)
                    values.Add(first.Fields[valueOffset + index]);
            }
            valueOffset += oldCount;
        }
        if (valueOffset != first.Fields.Length)
            return false;

        var nodes = document.Nodes.ToArray();
        nodes[0] = new XtNode
        {
            Type = first.Type,
            Index = first.Index,
            VariableLength = rootIndexes.Length,
            Fields = values.ToArray(),
            UserFields = first.UserFields,
        };
        selected = new XtDocument
        {
            PhysicalHeader = document.PhysicalHeader,
            VersionText = document.VersionText,
            HeaderSchemaIdentity = document.HeaderSchemaIdentity,
            Schema = document.Schema,
            BaseSchema = document.BaseSchema,
            EmbeddedMaxNodeType = document.EmbeddedMaxNodeType,
            UserFieldSize = document.UserFieldSize,
            Nodes = nodes,
        };
        return true;
    }

    private static XtNodeIndex[]? ReadPartBlockEntries(
        XtNode block,
        XtNodeDescriptor descriptor,
        ReadOnlySpan<XtFieldDescriptor> fields)
    {
        var entries = Array.Empty<XtNodeIndex>();
        var declaredCount = -1;
        var valueOffset = 0;
        foreach (var field in fields)
        {
            if (!field.Transmit)
                continue;
            var count = field.ElementCount > 1
                ? field.ElementCount
                : field.ElementCount == 1 ? block.VariableLength : 1;
            if (valueOffset + count > block.Fields.Length)
                return null;
            if (field.Name == "n_entries" && field.ElementCount == 0)
                declaredCount = checked((int)block.Fields[valueOffset].Integer);
            else if (field.Name == "entries" && field.ElementCount == 1 && field.Type == 'p')
            {
                entries = new XtNodeIndex[count];
                for (var index = 0; index < count; index++)
                    entries[index] = block.Fields[valueOffset + index].Pointer;
            }
            valueOffset += count;
        }
        return valueOffset == block.Fields.Length && declaredCount == entries.Length ? entries : null;
    }

    public static string EncodeWithBaseSchema(XtDocument document, XtSchemaDefinition baseSchema)
    {
        var definitions = new Dictionary<int, EmbeddedNodeDefinition>();
        foreach (var node in document.Nodes)
        {
            if (definitions.ContainsKey(node.Type))
                continue;
            var descriptor = document.Schema.GetNode(node.Type);
            if (descriptor.Type == 0)
                throw new InvalidOperationException($"Unknown XT node type {node.Type}.");
            definitions.Add(node.Type, CreateEmbeddedDefinition(
                node.Type,
                descriptor.Name,
                descriptor.Description,
                EffectiveFields(document.Schema, descriptor)));
        }
        var identity = $"SCH_{CurrentTransmitModelerVersion}_{document.Schema.SchemaNumber}_{baseSchema.SchemaNumber}";
        var effectiveSchema = BuildEmbeddedSchema(
            identity,
            CurrentTransmitModelerVersion,
            document.Schema.SchemaNumber,
            definitions.Values);
        var maxNodeType = 1;
        foreach (var descriptor in document.Schema.Nodes)
            maxNodeType = Math.Max(maxNodeType, descriptor.Type + 1);
        return Encode(new XtDocument
        {
            VersionText = $": TRANSMIT FILE created by modeller version {CurrentTransmitModelerVersion}",
            HeaderSchemaIdentity = identity,
            Schema = effectiveSchema,
            BaseSchema = baseSchema,
            EmbeddedMaxNodeType = maxNodeType,
            UserFieldSize = document.UserFieldSize,
            Nodes = document.Nodes,
        });
    }

    public static string Encode(XtDocument document)
    {
        var nodes = document.Nodes;
        var sb = new StringBuilder(nodes.Length * 256);
        var versionText = document.VersionText;
        sb.Append('T')
          .Append(versionText.Length)
          .Append(' ')
          .Append(versionText)
          .Append(document.HeaderSchemaIdentity.Length)
          .Append(' ')
          .Append(document.HeaderSchemaIdentity);
        if (document.BaseSchema is not null)
            sb.Append(document.EmbeddedMaxNodeType).Append(' ');
        sb.Append(document.UserFieldSize)
          .Append(' ');

        HashSet<int>? embeddedTypes = document.BaseSchema is null ? null : new HashSet<int>();
        foreach (var node in nodes)
        {
            var descriptor = document.Schema.GetNode(node.Type);
            if (descriptor.Type == 0)
                throw new InvalidOperationException("Unknown XT node type.");

            var variableLength = descriptor.Variable ? VariableLength(node, descriptor, document.Schema) : 0;
            sb.Append(node.Type).Append(' ');
            if (embeddedTypes is not null && embeddedTypes.Add(node.Type))
                WriteEmbeddedNodeDefinition(sb, descriptor, document.Schema, document.BaseSchema!);
            if (descriptor.Variable)
                sb.Append(variableLength).Append(' ');
            sb.Append(node.Index).Append(' ');
            var fields = document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
            var valueIndex = 0;
            for (var i = 0; i < fields.Length; i++)
            {
                if (!fields[i].Transmit)
                    continue;

                var count = fields[i].ElementCount > 1
                    ? fields[i].ElementCount
                    : descriptor.Variable && fields[i].ElementCount == 1 ? variableLength : 1;
                for (var j = 0; j < count; j++)
                    WriteField(sb, fields[i].Type, node.Fields[valueIndex++]);
            }

            if (valueIndex != node.Fields.Length)
                throw new InvalidOperationException($"XT node {node.Type}/{node.Index} contains {node.Fields.Length - valueIndex} extra field values.");
            if (document.UserFieldSize != 0 && HasUserFields(descriptor, fields))
            {
                if (node.UserFields.Length != document.UserFieldSize)
                    throw new InvalidOperationException($"XT node {node.Type}/{node.Index} has {node.UserFields.Length} user fields, expected {document.UserFieldSize}.");
                foreach (var userField in node.UserFields)
                    sb.Append(userField).Append(' ');
            }
        }

        sb.Append((int)XtNodeTypes.Terminator).Append(' ').Append('0').Append(' ');
        var payload = Wrap(sb.ToString());
        return document.PhysicalHeader is { Length: > 0 } physicalHeader
            ? physicalHeader + payload
            : payload;
    }

    public static XtNode[] Decode(string text)
        => DecodeDocument(text).Nodes;

    public static XtDocument DecodeDocument(string text)
    {
        var physicalHeader = RemovePhysicalHeader(ref text);
        text = RemoveLineBreaks(text);
        var tokenizer = new XtTokenizer(text);
        var flag = tokenizer.NextRequired();
        if (flag != "T")
            throw new FormatException("XT text flag is missing.");

        var versionLength = tokenizer.NextInt();
        var versionText = tokenizer.NextRaw(versionLength);
        var schemaLength = tokenizer.NextInt();
        var schema = tokenizer.NextRaw(schemaLength);
        var archiveIdentity = ParseArchiveIdentity(schema);
        var embeddedMaxNodeType = archiveIdentity.Embedded ? tokenizer.NextIntRequired() : 0;
        var userFieldSize = tokenizer.NextInt();
        if (userFieldSize < 0)
            throw new FormatException("XT user field size is negative.");
        ValidatePhysicalHeader(physicalHeader, schema, userFieldSize);
        var baseSchema = archiveIdentity.Embedded
            ? XtSchemaRegistry.ResolveBySchemaNumber(archiveIdentity.BaseSchemaNumber)
            : null;
        var schemaDefinition = baseSchema ?? XtSchemaRegistry.Resolve(schema);
        Dictionary<int, EmbeddedNodeDefinition>? embeddedDefinitions = archiveIdentity.Embedded ? new() : null;

        var nodes = new List<XtNode>();
        while (true)
        {
            var type = tokenizer.NextInt();
            // Optional schema fields may be emitted as compact null sentinels
            // by Parasolid. They are not valid node types; consume them until
            // a real node header (or the 999 terminator) is reached.
            while (type == 0)
                type = tokenizer.NextInt();
            if (type == (int)XtNodeTypes.Terminator)
            {
                var terminatorIndex = tokenizer.NextInt();
                if (terminatorIndex != 0)
                    throw new FormatException("Invalid XT terminator.");
                break;
            }

            XtNodeDescriptor descriptor;
            ReadOnlySpan<XtFieldDescriptor> fields;
            if (embeddedDefinitions is not null)
            {
                if (!embeddedDefinitions.TryGetValue(type, out var embeddedDefinition))
                {
                    embeddedDefinition = ReadEmbeddedNodeDefinition(ref tokenizer, type, baseSchema!);
                    embeddedDefinitions.Add(type, embeddedDefinition);
                }
                descriptor = embeddedDefinition.Descriptor;
                fields = embeddedDefinition.Fields;
            }
            else
            {
                descriptor = schemaDefinition.GetNode(type);
                if (descriptor.Type == 0)
                    throw new FormatException($"Unsupported XT node type {type} at token position {tokenizer.Position}.");
                fields = schemaDefinition.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
            }
            var variableLength = descriptor.Variable ? tokenizer.NextInt() : 0;

            var node = new XtNode { Type = type, Index = tokenizer.NextInt(), VariableLength = variableLength };
            var transmitted = CountTransmitted(descriptor, fields, variableLength);
            node.Fields = new XtFieldValue[transmitted];
            var valueIndex = 0;
            for (var i = 0; i < fields.Length; i++)
            {
                if (!fields[i].Transmit)
                    continue;

                var count = fields[i].ElementCount > 1
                    ? fields[i].ElementCount
                    : descriptor.Variable && fields[i].ElementCount == 1 ? variableLength : 1;
                if (fields[i].Type == 'c' && fields[i].ElementCount != 0)
                {
                    try
                    {
                        tokenizer.NextCharacterValues(node.Fields.AsSpan(valueIndex, count));
                        valueIndex += count;
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException)
                    {
                        throw new FormatException(
                            $"Invalid XT character array {descriptor.Name}.{fields[i].Name} on node {type}/{node.Index} at token position {tokenizer.Position}: {exception.Message}",
                            exception);
                    }
                    continue;
                }
                for (var j = 0; j < count; j++)
                {
                    try
                    {
                        node.Fields[valueIndex++] = ReadField(ref tokenizer, fields[i].Type);
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException)
                    {
                        throw new FormatException(
                            $"Invalid XT field {descriptor.Name}.{fields[i].Name}[{j}] on node {type}/{node.Index} at token position {tokenizer.Position}: {exception.Message}",
                            exception);
                    }
                }
            }
            if (userFieldSize != 0 && HasUserFields(descriptor, fields))
            {
                node.UserFields = new int[userFieldSize];
                for (var i = 0; i < node.UserFields.Length; i++)
                    node.UserFields[i] = tokenizer.NextIntRequired();
            }
            // Parasolid writes runs of '?' character sentinels without a
            // separator before an optional null pointer.  Once the declared
            // fields have consumed that run, a trailing zero can remain in
            // the tokenizer's compact-token buffer.  It is a field sentinel,
            // never a node type (the terminator is 999), so discard it before
            // reading the next node header.
            tokenizer.DiscardPendingZero();
            nodes.Add(node);
        }

        if (embeddedDefinitions is not null)
            schemaDefinition = BuildEmbeddedSchema(schema, archiveIdentity.ModelerVersion, archiveIdentity.CurrentSchemaNumber, embeddedDefinitions.Values);

        return new XtDocument
        {
            PhysicalHeader = physicalHeader,
            VersionText = versionText,
            HeaderSchemaIdentity = schema,
            Schema = schemaDefinition,
            BaseSchema = baseSchema,
            EmbeddedMaxNodeType = embeddedMaxNodeType,
            UserFieldSize = userFieldSize,
            Nodes = nodes.ToArray(),
        };
    }

    private static string? RemovePhysicalHeader(ref string text)
    {
        var first = 0;
        while (first < text.Length && char.IsWhiteSpace(text[first]))
            first++;
        if (first >= text.Length || text[first] == 'T')
        {
            if (first != 0)
                text = text[first..];
            return null;
        }
        if (first + 1 >= text.Length || text[first] != '*' || text[first + 1] != '*')
            throw new FormatException("XT text flag or physical file header is missing.");

        const string marker = "**END_OF_HEADER";
        var markerIndex = text.IndexOf(marker, first, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new FormatException("XT physical file header terminator is missing.");
        var payloadIndex = text.IndexOf('\n', markerIndex + marker.Length);
        if (payloadIndex < 0)
            throw new FormatException("XT physical file header has no payload.");
        payloadIndex++;
        var header = text[first..payloadIndex];
        text = text[payloadIndex..];
        return header;
    }

    private static void ValidatePhysicalHeader(string? physicalHeader, string payloadSchemaIdentity, int userFieldSize)
    {
        if (physicalHeader is null)
            return;
        var headerSchema = ReadHeaderValue(physicalHeader, "SCH");
        if (headerSchema is not null)
        {
            if (!IsSchemaIdentitySyntax(headerSchema))
                throw new FormatException($"XT physical header schema {headerSchema} is malformed.");
        }
        var headerUserFields = ReadHeaderValue(physicalHeader, "USFLD_SIZE");
        if (headerUserFields is not null &&
            (!int.TryParse(headerUserFields, NumberStyles.None, CultureInfo.InvariantCulture, out var declaredUserFields) ||
             declaredUserFields != userFieldSize))
            throw new FormatException($"XT physical header user-field size {headerUserFields} does not match payload size {userFieldSize}.");
    }

    private static bool IsSchemaIdentitySyntax(string identity)
    {
        if (!identity.StartsWith("SCH_", StringComparison.Ordinal))
            return false;
        var components = identity[4..].Split('_');
        if (components.Length is < 1 or > 3)
            return false;
        foreach (var component in components)
        {
            if (component.Length == 0 || !component.All(char.IsAsciiDigit))
                return false;
        }
        return true;
    }

    private static string? ReadHeaderValue(string header, string name)
    {
        var marker = name + "=";
        var start = header.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;
        start += marker.Length;
        var end = header.IndexOf(';', start);
        if (end < 0)
            throw new FormatException($"XT physical header field {name} is not terminated.");
        return header[start..end].Trim();
    }

    private static ArchiveSchemaIdentity ParseArchiveIdentity(string identity)
    {
        if (!identity.StartsWith("SCH_", StringComparison.Ordinal))
            throw new FormatException($"Invalid XT schema identity {identity}.");
        var components = identity[4..].Split('_');
        if (components.Length < 3)
            return new ArchiveSchemaIdentity(false, 0, 0, 0);
        if (!int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var modelerVersion) ||
            !int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var currentSchemaNumber) ||
            !int.TryParse(components[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var baseSchemaNumber))
            throw new FormatException($"Invalid embedded XT schema identity {identity}.");
        return new ArchiveSchemaIdentity(true, modelerVersion, currentSchemaNumber, baseSchemaNumber);
    }

    private static EmbeddedNodeDefinition ReadEmbeddedNodeDefinition(
        ref XtTokenizer tokenizer,
        XtNodeType type,
        XtSchemaDefinition baseSchema)
    {
        var baseDescriptor = baseSchema.GetNode(type);
        if (baseDescriptor.Type == 0)
        {
            var fieldCount = tokenizer.NextIntRequired();
            var name = tokenizer.NextShortString();
            var description = tokenizer.NextShortString();
            var fields = new XtFieldDescriptor[fieldCount];
            for (var index = 0; index < fields.Length; index++)
                fields[index] = ReadEmbeddedField(ref tokenizer, type);
            return CreateEmbeddedDefinition(type, name, description, fields);
        }

        var baseFields = EffectiveFields(baseSchema, baseDescriptor);
        var marker = tokenizer.NextIntRequired();
        if (marker == 255)
            return CreateEmbeddedDefinition(type, baseDescriptor.Name, baseDescriptor.Description, baseFields);
        if (marker < 0)
            throw new FormatException("Embedded XT schema field count is negative.");

        var currentFields = new List<XtFieldDescriptor>(marker);
        var baseIndex = 0;
        while (true)
        {
            var instruction = tokenizer.NextChar();
            switch (instruction)
            {
                case 'C':
                    if ((uint)baseIndex >= (uint)baseFields.Length)
                        throw new FormatException("Embedded XT schema copies beyond the base field list.");
                    currentFields.Add(baseFields[baseIndex++]);
                    break;
                case 'D':
                    if ((uint)baseIndex >= (uint)baseFields.Length)
                        throw new FormatException("Embedded XT schema deletes beyond the base field list.");
                    baseIndex++;
                    break;
                case 'I':
                case 'A':
                    currentFields.Add(ReadEmbeddedField(ref tokenizer, type));
                    break;
                case 'Z':
                    if (currentFields.Count != marker)
                        throw new FormatException($"Embedded XT schema declares {marker} fields but defines {currentFields.Count}.");
                    return CreateEmbeddedDefinition(type, baseDescriptor.Name, baseDescriptor.Description, currentFields.ToArray());
                default:
                    throw new FormatException($"Invalid embedded XT schema edit instruction '{instruction}'.");
            }
        }
    }

    private static XtFieldDescriptor ReadEmbeddedField(ref XtTokenizer tokenizer, XtNodeType ownerType)
    {
        var name = tokenizer.NextShortString();
        var nodeClass = tokenizer.NextIntRequired();
        var elementCount = tokenizer.NextIntRequired();
        var type = nodeClass != 0 ? 'p' : ReadEmbeddedType(ref tokenizer);
        var transmit = elementCount != 1 || tokenizer.NextSchemaLogical();
        return new XtFieldDescriptor(ownerType, name, type, transmit, nodeClass, elementCount);
    }

    private static char ReadEmbeddedType(ref XtTokenizer tokenizer)
    {
        var value = tokenizer.NextShortString();
        if (value.Length != 1 || !"bcdfhilnpqtuvw".Contains(value[0]))
            throw new FormatException($"Invalid embedded XT schema field type {value}.");
        return value[0];
    }

    private static EmbeddedNodeDefinition CreateEmbeddedDefinition(
        XtNodeType type,
        string name,
        string description,
        XtFieldDescriptor[] fields)
    {
        var variable = fields.Any(static field => field.ElementCount == 1);
        return new EmbeddedNodeDefinition(
            new XtNodeDescriptor(type, name, description, true, fields.Length, variable, 0, fields.Length),
            fields);
    }

    private static XtFieldDescriptor[] EffectiveFields(XtSchemaDefinition schema, XtNodeDescriptor descriptor)
    {
        var source = schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
        var count = 0;
        foreach (var field in source)
        {
            if (field.Transmit || field.ElementCount == 1)
                count++;
        }
        var result = new XtFieldDescriptor[count];
        var index = 0;
        foreach (var field in source)
        {
            if (field.Transmit || field.ElementCount == 1)
                result[index++] = field;
        }
        return result;
    }

    private static XtSchemaDefinition BuildEmbeddedSchema(
        string identity,
        int modelerVersion,
        int schemaNumber,
        IEnumerable<EmbeddedNodeDefinition> definitions)
    {
        var ordered = definitions.OrderBy(static definition => definition.Descriptor.Type).ToArray();
        var nodes = new XtNodeDescriptor[ordered.Length];
        var fields = new XtFieldDescriptor[ordered.Sum(static definition => definition.Fields.Length)];
        var fieldOffset = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            var definition = ordered[index];
            definition.Fields.CopyTo(fields, fieldOffset);
            nodes[index] = definition.Descriptor with { FieldOffset = fieldOffset };
            fieldOffset += definition.Fields.Length;
        }
        return new XtSchemaDefinition(identity, modelerVersion, schemaNumber, nodes, fields);
    }

    private static void WriteEmbeddedNodeDefinition(
        StringBuilder sb,
        XtNodeDescriptor descriptor,
        XtSchemaDefinition schema,
        XtSchemaDefinition baseSchema)
    {
        var fields = schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount).ToArray();
        var baseDescriptor = baseSchema.GetNode(descriptor.Type);
        if (baseDescriptor.Type == 0)
        {
            sb.Append(fields.Length).Append(' ');
            WriteShortString(sb, descriptor.Name);
            WriteShortString(sb, descriptor.Description);
            foreach (var field in fields)
                WriteEmbeddedField(sb, field);
            return;
        }

        var baseFields = EffectiveFields(baseSchema, baseDescriptor);
        if (fields.AsSpan().SequenceEqual(baseFields))
        {
            sb.Append(255).Append(' ');
            return;
        }

        sb.Append(fields.Length).Append(' ');
        for (var index = 0; index < baseFields.Length; index++)
            sb.Append('D');
        foreach (var field in fields)
        {
            sb.Append('A');
            WriteEmbeddedField(sb, field);
        }
        sb.Append('Z');
    }

    private static void WriteEmbeddedField(StringBuilder sb, XtFieldDescriptor field)
    {
        WriteShortString(sb, field.Name);
        sb.Append(field.NodeClass).Append(' ')
          .Append(field.ElementCount).Append(' ');
        if (field.NodeClass == 0)
            WriteShortString(sb, field.Type.ToString());
        if (field.ElementCount == 1)
            sb.Append(field.Transmit ? 'T' : 'F');
    }

    private static void WriteShortString(StringBuilder sb, string value)
    {
        sb.Append(value.Length).Append(' ').Append(value);
    }

    private static int CountTransmitted(XtNodeDescriptor descriptor, ReadOnlySpan<XtFieldDescriptor> fields, int variableLength)
    {
        var count = 0;
        foreach (var field in fields)
        {
            if (field.Transmit)
            count += field.ElementCount > 1
                ? field.ElementCount
                : descriptor.Variable && field.ElementCount == 1 ? variableLength : 1;
        }

        return count;
    }

    private static int VariableLength(XtNode node, XtNodeDescriptor descriptor, XtSchemaDefinition schema)
    {
        if (node.VariableLength >= 0)
            return node.VariableLength;

        var fields = schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount);
        var fixedValueCount = 0;
        var variableFieldCount = 0;
        foreach (var field in fields)
        {
            if (!field.Transmit)
                continue;
            if (field.ElementCount == 1)
                variableFieldCount++;
            else
                fixedValueCount += Math.Max(1, field.ElementCount);
        }
        if (variableFieldCount == 0 || node.Fields.Length < fixedValueCount || (node.Fields.Length - fixedValueCount) % variableFieldCount != 0)
            throw new InvalidOperationException($"Cannot infer variable length for XT node {node.Type}/{node.Index}.");
        return (node.Fields.Length - fixedValueCount) / variableFieldCount;
    }

    private static bool HasUserFields(XtNodeDescriptor descriptor, ReadOnlySpan<XtFieldDescriptor> fields)
    {
        foreach (var field in fields)
        {
            if (field.Name is "node_id" or "highest_node_id" or "attributes_features")
                return true;
        }
        return descriptor.Name is
            "ASSEMBLY" or "INSTANCE" or "BODY" or
            "SHELL" or "FACE" or "LOOP" or "EDGE" or "HALFEDGE" or "VERTEX" or "REGION" or
            "POINT" or "LINE" or "CIRCLE" or "ELLIPSE" or "PARABOLA" or "HYPERBOLA" or
            "PARACURVE" or "INTERSECTION" or "SILHOUETTE" or "OFFSET_CURVE" or "CPC" or
            "PLANE" or "CYLINDER" or "CONE" or "SPHERE" or "TORUS" or "PIPE" or
            "OFFSET_SURF" or "PARASURF" or "SILH_SURF" or "SWEPT_SURF" or "SPUN_SURF" or "CPS" or
            "ATTRIBUTE" or "FEATURE" or "MEMBER_OF_FEATURE" or "TRANSFORM" or
            "B_SURFACE" or "PE_SURF" or "PE_CURVE" or "PCURVE" or "TRIMMED_CURVE" or "B_CURVE" or "SP_CURVE" or
            "MESH" or "PLINE" or "LATTICE" or "FRAME";
    }

    private readonly record struct ArchiveSchemaIdentity(bool Embedded, int ModelerVersion, int CurrentSchemaNumber, int BaseSchemaNumber);
    private readonly record struct EmbeddedNodeDefinition(XtNodeDescriptor Descriptor, XtFieldDescriptor[] Fields);

    private static string RemoveLineBreaks(string text)
    {
        if (text.IndexOfAny(['\n', '\r']) < 0)
            return text;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is not '\n' and not '\r')
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static void WriteField(StringBuilder sb, char type, XtFieldValue value)
    {
        if (value.Kind == XtFieldKind.Empty)
        {
            sb.Append('?');
            return;
        }

        switch (type)
        {
            case 'p':
                sb.Append(value.Pointer).Append(' ');
                break;
            case 'd':
            case 'u':
            case 'n':
            case 'w':
            case 't':
            case 'q':
                sb.Append(value.Integer).Append(' ');
                break;
            case 'f':
                sb.Append(FormatReal(value.Real)).Append(' ');
                break;
            case 'c':
                sb.Append(value.Character);
                break;
            case 'l':
                sb.Append(value.Integer != 0 ? 'T' : 'F');
                break;
            case 'v':
            case 'h':
                sb.Append(FormatReal(value.Vector.X)).Append(' ')
                  .Append(FormatReal(value.Vector.Y)).Append(' ')
                  .Append(FormatReal(value.Vector.Z)).Append(' ');
                break;
            case 'i':
                sb.Append(FormatReal(value.Vector.X)).Append(' ')
                  .Append(FormatReal(value.Vector.Y)).Append(' ');
                break;
            case 'b':
                sb.Append(FormatReal(value.Vector.X)).Append(' ')
                  .Append(FormatReal(value.Vector.Y)).Append(' ')
                  .Append(FormatReal(value.Vector.Z)).Append(' ')
                  .Append(FormatReal(value.Fourth)).Append(' ')
                  .Append(FormatReal(value.Fifth)).Append(' ')
                  .Append(FormatReal(value.Sixth)).Append(' ');
                break;
            default:
                throw new NotSupportedException($"Unsupported XT field type '{type}'.");
        }
    }

    private static string FormatReal(double value)
    {
        if (value == 1000.0)
            return "1e3";

        var text = value.ToString("G17", CultureInfo.InvariantCulture).Replace('E', 'e');
        var exponent = text.IndexOf('e', StringComparison.Ordinal);
        if (exponent < 0)
            return text;

        var mantissa = text[..exponent];
        var sign = "";
        var digits = text[(exponent + 1)..];
        if (digits.StartsWith("+", StringComparison.Ordinal) || digits.StartsWith("-", StringComparison.Ordinal))
        {
            sign = digits[..1] == "+" ? "" : "-";
            digits = digits[1..];
        }

        digits = digits.TrimStart('0');
        if (digits.Length == 0)
            digits = "0";
        return mantissa + "e" + sign + digits;
    }

    private static XtFieldValue ReadField(ref XtTokenizer tokenizer, char type)
    {
        return type switch
        {
            'p' => tokenizer.NextIntegerValue(XtFieldKind.Pointer),
            'd' or 'n' or 'w' or 't' or 'q' => tokenizer.NextIntegerValue(XtFieldKind.Integer),
            'u' => tokenizer.NextIntegerValue(XtFieldKind.Unsigned),
            'f' => tokenizer.NextDoubleValue(),
            'c' => tokenizer.NextCharacterValue(),
            'l' => tokenizer.NextLogicalValue(),
            'i' => tokenizer.NextIntervalValue(),
            'b' => tokenizer.NextBoxValue(),
            'v' or 'h' => tokenizer.NextVectorValue(),
            _ => throw new NotSupportedException($"Unsupported XT field type '{type}'."),
        };
    }

    private static string Wrap(string text)
    {
        var sb = new StringBuilder(text.Length + text.Length / 80 + 1);
        var column = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (column == 79 && ch == ' ')
            {
                sb.Append('\n');
                column = 0;
            }

            sb.Append(ch);
            column++;
            if (column == 80)
            {
                sb.Append('\n');
                column = 0;
            }
        }

        if (column != 0)
            sb.Append('\n');
        return sb.ToString();
    }

    private ref struct XtTokenizer
    {
        private readonly string text;
        private int position;
        private string? pending;
        public readonly int Position => position;

        public XtTokenizer(string text)
        {
            this.text = text;
            position = 0;
            pending = null;
        }

        public string NextRequired()
        {
            if (pending is not null)
            {
                var value = pending;
                pending = null;
                return value;
            }

            SkipIgnored();
            if (position >= text.Length)
                throw new FormatException("Unexpected end of XT text.");

            if (text[position] == 'T')
            {
                position++;
                return "T";
            }

            var start = position;
            while (position < text.Length && !IsSeparator(text[position]))
                position++;
            return text[start..position];
        }

        public int NextInt()
        {
            var token = NextRequired();
            if (token == "?")
                return 0;
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return 0;
            }
            return int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public int NextIntRequired()
        {
            var token = NextRequired();
            if (token.Length == 0 || token[0] == '?')
                throw new FormatException("Required XT integer is null.");
            return int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public XtFieldValue NextIntegerValue(XtFieldKind kind)
        {
            var token = NextRequired();
            if (token == "?")
                return XtFieldValue.Null();
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return XtFieldValue.Null();
            }

            var value = long.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return kind switch
            {
                XtFieldKind.Pointer => XtFieldValue.Ptr(checked((int)value)),
                XtFieldKind.Unsigned => XtFieldValue.Unsigned(value),
                _ => XtFieldValue.Int(value),
            };
        }

        public double NextDouble()
        {
            var token = NextRequired();
            if (token == "?")
                return 0;
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return 0;
            }
            return ParseDoubleToken(token);
        }

        public XtFieldValue NextDoubleValue()
        {
            var token = NextRequired();
            if (token == "?")
                return XtFieldValue.Null();
            if (token.Length > 1 && token[0] == '?')
            {
                pending = token[1..];
                return XtFieldValue.Null();
            }

            return XtFieldValue.RealValue(ParseDoubleToken(token));
        }

        public XtFieldValue NextVectorValue()
        {
            var first = NextRequired();
            if (first == "?")
                return XtFieldValue.Null();
            if (first.Length > 1 && first[0] == '?')
            {
                pending = first[1..];
                return XtFieldValue.Null();
            }

            return XtFieldValue.Vec(
                ParseDoubleToken(first),
                NextDouble(),
                NextDouble());
        }

        public XtFieldValue NextIntervalValue()
        {
            var first = NextDoubleValue();
            if (first.Kind == XtFieldKind.Empty)
                return first;
            return XtFieldValue.IntervalValue(first.Real, NextDouble());
        }

        public XtFieldValue NextBoxValue()
        {
            var first = NextDoubleValue();
            if (first.Kind == XtFieldKind.Empty)
                return first;
            return XtFieldValue.BoxValue(first.Real, NextDouble(), NextDouble(), NextDouble(), NextDouble(), NextDouble());
        }

        public XtFieldValue NextCharacterValue()
        {
            var value = NextChar();
            return value == '?' ? XtFieldValue.Null() : XtFieldValue.Char(value);
        }

        public void NextCharacterValues(Span<XtFieldValue> values)
        {
            if (pending is not null)
                throw new FormatException("XT raw character array starts inside a pending compact token.");
            if (position < text.Length && IsSeparator(text[position]))
                position++;
            if (position + values.Length > text.Length)
                throw new FormatException("Unexpected end of XT raw character array.");
            for (var index = 0; index < values.Length; index++)
            {
                var value = text[position++];
                values[index] = value == '?' ? XtFieldValue.Null() : XtFieldValue.Char(value);
            }
        }

        public XtFieldValue NextLogicalValue()
        {
            var value = NextChar();
            return value switch
            {
                '?' => XtFieldValue.Null(),
                'T' => XtFieldValue.Logical(true),
                'F' => XtFieldValue.Logical(false),
                _ => throw new FormatException($"Invalid XT logical value '{value}'."),
            };
        }

        public char NextChar()
        {
            if (pending is not null)
            {
                var token = pending;
                var ch = token[0];
                pending = token.Length > 1 ? token[1..] : null;
                return ch;
            }

            SkipIgnored();
            if (position >= text.Length)
                throw new FormatException("Unexpected end of XT text.");

            var start = position;
            while (position < text.Length && !IsSeparator(text[position]))
                position++;
            var value = text[start..position];
            if (value.Length > 1)
                pending = value[1..];
            return value[0];
        }

        public void DiscardPendingZero()
        {
            if (pending == "0")
                pending = null;
        }

        public string NextRaw(int length)
        {
            SkipIgnored();
            if (position + length > text.Length)
                throw new FormatException("Unexpected end of XT raw string.");

            var value = text.Substring(position, length);
            position += length;
            return value;
        }

        public string NextShortString()
        {
            var length = NextIntRequired();
            if (length < 0)
                throw new FormatException("Negative XT short-string length.");
            return NextRaw(length);
        }

        public bool NextSchemaLogical()
        {
            var value = NextChar();
            return value switch
            {
                'T' => true,
                'F' => false,
                _ => throw new FormatException($"Invalid embedded schema logical '{value}'."),
            };
        }

        private void SkipIgnored()
        {
            while (position < text.Length && IsSeparator(text[position]))
                position++;
        }

        private static double ParseDoubleToken(string token) => token switch
        {
            "+" => 0.0,
            "-" => BitConverter.Int64BitsToDouble(long.MinValue),
            _ => double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture),
        };

        private static bool IsSeparator(char ch) => ch is ' ' or '\n' or '\r' or '\t';
    }
}
