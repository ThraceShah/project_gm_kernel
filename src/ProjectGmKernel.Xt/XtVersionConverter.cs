
namespace ProjectGmKernel.Xt;

public static class XtVersionConverter
{
    public static XtDocument Transcode(XtSchemaCatalog catalog,XtDocument document,string targetSchemaIdentity)
    {
        ArgumentNullException.ThrowIfNull(catalog);ArgumentNullException.ThrowIfNull(document);return Transcode(document,catalog.Resolve(targetSchemaIdentity));
    }

    public static bool TryTranscode(XtSchemaCatalog catalog,XtDocument document,string targetSchemaIdentity,out XtDocument? result,out XtDiagnostic diagnostic)
    {
        try{result=Transcode(catalog,document,targetSchemaIdentity);diagnostic=default;return true;}
        catch(Exception exception) when(exception is XtFormatException or FormatException or NotSupportedException or ArgumentException){result=null;diagnostic=new XtDiagnostic(exception is XtFormatException format?format.Code:XtErrorCode.NotRepresentable,exception.Message);return false;}
    }

    public static bool CanTranscode(XtDocument source, XtSchemaDefinition target)
        => GetIncompatibility(source, target) is null;

    public static string? GetIncompatibility(XtDocument source, XtSchemaDefinition target)
    {
        if (source.Nodes.Length == 0)
            return "document is empty";
        var nodesByIndex = new Dictionary<XtNodeIndex, XtNode>(source.Nodes.Length);
        foreach (var node in source.Nodes)
        {
            if (!nodesByIndex.TryAdd(node.Index, node))
                return $"node index {node.Index} is duplicated";
        }
        var pending = new Queue<XtNodeIndex>();
        var reachable = new HashSet<XtNodeIndex>();
        pending.Enqueue(source.Nodes[0].Index);
        while (pending.Count != 0)
        {
            var nodeIndex = pending.Dequeue();
            if (!reachable.Add(nodeIndex))
                continue;
            if (!nodesByIndex.TryGetValue(nodeIndex, out var node))
                continue;
            var sourceDescriptor = source.Schema.GetNode(node.Type);
            var targetDescriptor = target.GetNode(node.Type);
            if (sourceDescriptor.Type == 0 || targetDescriptor.Type == 0 || !targetDescriptor.Transmit)
                return $"node {node.Type}/{node.Index} is unavailable";
            var targetFields = new Dictionary<FieldKey, TargetField>();
            var targetNonTransmittedFields = new HashSet<string>(StringComparer.Ordinal);
            var targetOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var field in target.Fields.Slice(targetDescriptor.FieldOffset, targetDescriptor.ParsedFieldCount))
            {
                if (!field.Transmit)
                {
                    targetNonTransmittedFields.Add(field.Name);
                    continue;
                }
                var occurrence = targetOccurrences.TryGetValue(field.Name, out var seen) ? seen : 0;
                targetOccurrences[field.Name] = occurrence + 1;
                var count = FieldValueCount(targetDescriptor, field, Math.Max(0, node.VariableLength));
                targetFields.Add(new FieldKey(field.Name, occurrence), new TargetField(field.Type, count));
            }
            foreach (var pair in IndexFields(source.Schema, sourceDescriptor, node))
            {
                var hasNonDefaultValue = pair.Value.Values.Any(static value => !IsDefault(value)) &&
                    !IsExplicitlyDroppableDefault(sourceDescriptor, pair.Key, pair.Value.Values);
                if (!targetFields.TryGetValue(pair.Key, out var targetField))
                {
                    if (targetNonTransmittedFields.Contains(pair.Key.Name))
                        continue;
                    if (hasNonDefaultValue)
                        return $"node {node.Type}/{node.Index} field {pair.Key.Name}[{pair.Key.Occurrence}] is unavailable ({Describe(pair.Value.Values)})";
                    continue;
                }
                if (hasNonDefaultValue && !AreCompatible(pair.Value.Type, targetField.Type))
                    return $"node {node.Type}/{node.Index} field {pair.Key.Name}[{pair.Key.Occurrence}] changes from {pair.Value.Type}/{pair.Value.Values.Length} to {targetField.Type}/{targetField.ValueCount}";
                if (pair.Value.Values.Length > targetField.ValueCount &&
                    pair.Value.Values.AsSpan(targetField.ValueCount).ContainsNonDefault())
                    return $"node {node.Type}/{node.Index} field {pair.Key.Name}[{pair.Key.Occurrence}] would truncate non-default values from {pair.Value.Values.Length} to {targetField.ValueCount}";
                if (pair.Value.Type == 'p' && targetField.Type == 'p')
                {
                    var count = Math.Min(pair.Value.Values.Length, targetField.ValueCount);
                    for (var index = 0; index < count; index++)
                    {
                        var pointer = pair.Value.Values[index].Pointer;
                        if (pointer != 0)
                            pending.Enqueue(pointer);
                    }
                }
            }
        }
        return null;
    }

    public static XtDocument Transcode(XtDocument source, XtSchemaDefinition target)
    {
        if (source.Nodes.Length == 0)
            throw new FormatException("Cannot transcode an empty XT document.");
        if (GetIncompatibility(source, target) is { } incompatibility)
            throw new NotSupportedException($"Target schema {target.Identity} cannot represent the source XT document without loss: {incompatibility}.");

        var nodes = new List<XtNode>(source.Nodes.Length);
        foreach (var sourceNode in source.Nodes)
        {
            var sourceDescriptor = source.Schema.GetNode(sourceNode.Type);
            var targetDescriptor = target.GetNode(sourceNode.Type);
            if (sourceDescriptor.Type == 0 || targetDescriptor.Type == 0 || !targetDescriptor.Transmit || sourceNode.Type == 1)
                continue;

            var variableLength = targetDescriptor.Variable ? Math.Max(0, sourceNode.VariableLength) : 0;
            var sourceValues = IndexFields(source.Schema, sourceDescriptor, sourceNode);
            var targetValues = new List<XtFieldValue>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var targetField in target.Fields.Slice(targetDescriptor.FieldOffset, targetDescriptor.ParsedFieldCount))
            {
                if (!targetField.Transmit)
                    continue;
                var occurrence = occurrences.TryGetValue(targetField.Name, out var seen) ? seen : 0;
                occurrences[targetField.Name] = occurrence + 1;
                var count = FieldValueCount(targetDescriptor, targetField, variableLength);
                var fieldKey = new FieldKey(targetField.Name, occurrence);
                if ((TryGetLegacyOverride(source, sourceNode, target, targetDescriptor, targetField, out var sourceField) ||
                     sourceValues.TryGetValue(fieldKey, out sourceField)) &&
                    AreCompatible(sourceField.Type, targetField.Type))
                {
                    for (var index = 0; index < count; index++)
                        targetValues.Add(index < sourceField.Values.Length
                            ? ConvertValue(sourceField.Values[index], targetField.Type)
                            : DefaultValue(targetField.Type));
                }
                else
                {
                    for (var index = 0; index < count; index++)
                        targetValues.Add(DefaultValue(targetField.Type));
                }
            }

            nodes.Add(new XtNode
            {
                Type = sourceNode.Type,
                Index = sourceNode.Index,
                VariableLength = variableLength,
                Fields = targetValues.ToArray(),
                UserFields = sourceNode.UserFields.ToArray(),
            });
        }

        if (nodes.Count == 0 || nodes[0].Type != source.Nodes[0].Type)
            throw new NotSupportedException($"Target schema {target.Identity} cannot represent root node type {source.Nodes[0].Type}.");

        nodes = PruneUnreachable(target, nodes);
        ClearDanglingPointers(target, nodes);
        var result = new XtDocument
        {
            VersionText = source.VersionText,
            HeaderSchemaIdentity = target.Identity,
            Schema = target,
            UserFieldSize = source.UserFieldSize,
            Nodes = nodes.ToArray(),
        };
        if (!HasNode(source.Schema, "REGION") && HasNode(target, "REGION"))
            result = UpgradeLegacyRegions(result);
        var sourceRootName = source.Schema.GetNode(source.Nodes[0].Type).Name;
        if (sourceRootName == "POINTER_LIS_BLOCK" && HasNode(target, "PART_XMT_BLOCK"))
            result = XtPartGraph.WrapInCurrentPartBlock(result, XtPartGraph.GetRootIndexes(source));
        return result;
    }

    private static XtDocument UpgradeLegacyRegions(XtDocument document)
    {
        var regionDescriptor = FindNode(document.Schema, "REGION");
        var shellDescriptor = FindNode(document.Schema, "SHELL");
        var bodyDescriptor = FindNode(document.Schema, "BODY");
        var faceDescriptor = FindNode(document.Schema, "FACE");
        if (regionDescriptor.Type == 0 || shellDescriptor.Type == 0 || bodyDescriptor.Type == 0 || faceDescriptor.Type == 0)
            return document;

        var nodes = document.Nodes.ToList();
        var nextIndex = nodes.Max(static node => node.Index) + 1;
        var highestNodeId = 0L;
        foreach (var node in nodes)
        {
            var descriptor = document.Schema.GetNode(node.Type);
            if (TryGetSingle(document.Schema, descriptor, node, "node_id", out var nodeId))
                highestNodeId = Math.Max(highestNodeId, nodeId.Integer);
            if (descriptor.Name == "BODY" && TryGetSingle(document.Schema, descriptor, node, "highest_node_id", out var highest))
                highestNodeId = Math.Max(highestNodeId, highest.Integer);
        }

        foreach (var rootIndex in XtPartGraph.GetRootIndexes(document))
        {
            var body = nodes.FirstOrDefault(node => node.Index == rootIndex);
            if (body is null || document.Schema.GetNode(body.Type).Name != "BODY" ||
                !TryGetSingle(document.Schema, bodyDescriptor, body, "shell", out var shellValue) || shellValue.Pointer == 0)
                continue;
            var primaryShell = nodes.FirstOrDefault(node => node.Index == shellValue.Pointer);
            if (primaryShell is null || document.Schema.GetNode(primaryShell.Type).Name != "SHELL")
                throw new FormatException($"Legacy body {body.Index} references missing primary shell {shellValue.Pointer}.");

            var voidRegionIndex = nextIndex++;
            var solidRegionIndex = nextIndex++;
            var voidShellIndex = nextIndex++;
            var firstFaceIndex = TryGetSingle(document.Schema, shellDescriptor, primaryShell, "face", out var faceValue)
                ? faceValue.Pointer
                : 0;

            if (TryGetSingle(document.Schema, shellDescriptor, primaryShell, "edge", out var edgeValue))
            {
                SetSingle(document.Schema, bodyDescriptor, body, "edge", edgeValue);
                SetSingle(document.Schema, shellDescriptor, primaryShell, "edge", XtFieldValue.Ptr(0));
            }
            if (TryGetSingle(document.Schema, shellDescriptor, primaryShell, "vertex", out var vertexValue))
            {
                SetSingle(document.Schema, bodyDescriptor, body, "vertex", vertexValue);
                SetSingle(document.Schema, shellDescriptor, primaryShell, "vertex", XtFieldValue.Ptr(0));
            }
            SetSingle(document.Schema, bodyDescriptor, body, "nom_geom_state", XtFieldValue.Unsigned(1));
            SetSingle(document.Schema, bodyDescriptor, body, "region", XtFieldValue.Ptr(voidRegionIndex));
            SetSingle(document.Schema, shellDescriptor, primaryShell, "region", XtFieldValue.Ptr(solidRegionIndex));
            SetSingle(document.Schema, shellDescriptor, primaryShell, "front_face", XtFieldValue.Ptr(0));

            var voidShell = CreateDefaultNode(document.Schema, shellDescriptor, voidShellIndex, document.UserFieldSize);
            SetSingle(document.Schema, shellDescriptor, voidShell, "node_id", XtFieldValue.Int(++highestNodeId));
            SetSingle(document.Schema, shellDescriptor, voidShell, "body", XtFieldValue.Ptr(0));
            SetSingle(document.Schema, shellDescriptor, voidShell, "region", XtFieldValue.Ptr(voidRegionIndex));
            SetSingle(document.Schema, shellDescriptor, voidShell, "front_face", XtFieldValue.Ptr(firstFaceIndex));

            var voidRegion = CreateDefaultNode(document.Schema, regionDescriptor, voidRegionIndex, document.UserFieldSize);
            SetSingle(document.Schema, regionDescriptor, voidRegion, "node_id", XtFieldValue.Int(++highestNodeId));
            SetSingle(document.Schema, regionDescriptor, voidRegion, "body", XtFieldValue.Ptr(body.Index));
            SetSingle(document.Schema, regionDescriptor, voidRegion, "next", XtFieldValue.Ptr(solidRegionIndex));
            SetSingle(document.Schema, regionDescriptor, voidRegion, "previous", XtFieldValue.Ptr(0));
            SetSingle(document.Schema, regionDescriptor, voidRegion, "shell", XtFieldValue.Ptr(voidShellIndex));
            SetSingle(document.Schema, regionDescriptor, voidRegion, "type", XtFieldValue.Char('V'));

            var solidRegion = CreateDefaultNode(document.Schema, regionDescriptor, solidRegionIndex, document.UserFieldSize);
            SetSingle(document.Schema, regionDescriptor, solidRegion, "node_id", XtFieldValue.Int(++highestNodeId));
            SetSingle(document.Schema, regionDescriptor, solidRegion, "body", XtFieldValue.Ptr(body.Index));
            SetSingle(document.Schema, regionDescriptor, solidRegion, "next", XtFieldValue.Ptr(0));
            SetSingle(document.Schema, regionDescriptor, solidRegion, "previous", XtFieldValue.Ptr(voidRegionIndex));
            SetSingle(document.Schema, regionDescriptor, solidRegion, "shell", XtFieldValue.Ptr(primaryShell.Index));
            SetSingle(document.Schema, regionDescriptor, solidRegion, "type", XtFieldValue.Char('S'));

            foreach (var face in nodes)
            {
                if (document.Schema.GetNode(face.Type).Name != "FACE" ||
                    !TryGetSingle(document.Schema, faceDescriptor, face, "shell", out var ownerShell) ||
                    ownerShell.Pointer != primaryShell.Index)
                    continue;
                SetSingle(document.Schema, faceDescriptor, face, "front_shell", XtFieldValue.Ptr(voidShellIndex));
                if (TryGetSingle(document.Schema, faceDescriptor, face, "next", out var nextFace))
                    SetSingle(document.Schema, faceDescriptor, face, "next_front", nextFace);
                if (TryGetSingle(document.Schema, faceDescriptor, face, "previous", out var previousFace))
                    SetSingle(document.Schema, faceDescriptor, face, "previous_front", previousFace);
            }

            var edgeDescriptor = FindNode(document.Schema, "EDGE");
            var vertexDescriptor = FindNode(document.Schema, "VERTEX");
            var halfedgeDescriptor = FindNode(document.Schema, "HALFEDGE");
            foreach (var edge in nodes)
            {
                if (edgeDescriptor.Type == 0 || document.Schema.GetNode(edge.Type).Name != "EDGE")
                    continue;
                SetSingle(document.Schema, edgeDescriptor, edge, "owner", XtFieldValue.Ptr(body.Index));
                if (halfedgeDescriptor.Type == 0 ||
                    !TryGetSingle(document.Schema, edgeDescriptor, edge, "halfedge", out var halfedgeValue) ||
                    halfedgeValue.Pointer == 0)
                    continue;
                var firstHalfedge = nodes.FirstOrDefault(node => node.Index == halfedgeValue.Pointer);
                if (firstHalfedge is null)
                    continue;
                SetSingle(document.Schema, halfedgeDescriptor, firstHalfedge, "sense", XtFieldValue.Char('+'));
                if (!TryGetSingle(document.Schema, halfedgeDescriptor, firstHalfedge, "other", out var otherValue) ||
                    otherValue.Pointer == 0)
                    continue;
                var otherHalfedge = nodes.FirstOrDefault(node => node.Index == otherValue.Pointer);
                if (otherHalfedge is not null)
                    SetSingle(document.Schema, halfedgeDescriptor, otherHalfedge, "sense", XtFieldValue.Char('-'));
            }
            if (vertexDescriptor.Type != 0)
            {
                foreach (var vertex in nodes)
                {
                    if (document.Schema.GetNode(vertex.Type).Name == "VERTEX")
                        SetSingle(document.Schema, vertexDescriptor, vertex, "owner", XtFieldValue.Ptr(body.Index));
                }
            }

            SetSingle(document.Schema, bodyDescriptor, body, "highest_node_id", XtFieldValue.Int(highestNodeId));
            nodes.Add(voidShell);
            nodes.Add(voidRegion);
            nodes.Add(solidRegion);
        }

        return new XtDocument
        {
            VersionText = document.VersionText,
            HeaderSchemaIdentity = document.HeaderSchemaIdentity,
            Schema = document.Schema,
            BaseSchema = document.BaseSchema,
            EmbeddedMaxNodeType = document.EmbeddedMaxNodeType,
            UserFieldSize = document.UserFieldSize,
            Nodes = nodes.ToArray(),
        };
    }

    private static XtNode CreateDefaultNode(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNodeIndex index,
        int userFieldSize)
    {
        var values = new List<XtFieldValue>();
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, 0);
            for (var valueIndex = 0; valueIndex < count; valueIndex++)
                values.Add(DefaultValue(field.Type));
        }
        return new XtNode
        {
            Type = descriptor.Type,
            Index = index,
            VariableLength = 0,
            Fields = values.ToArray(),
            UserFields = userFieldSize == 0 ? [] : new int[userFieldSize],
        };
    }

    private static bool TryGetSingle(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node,
        string fieldName,
        out XtFieldValue value)
    {
        if (TryGetField(schema, descriptor, node, fieldName, out var field) && field.Values.Length == 1)
        {
            value = field.Values[0];
            return true;
        }
        value = default;
        return false;
    }

    private static void SetSingle(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node,
        string fieldName,
        XtFieldValue value)
    {
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, Math.Max(0, node.VariableLength));
            if (field.Name == fieldName)
            {
                if (count != 1)
                    throw new FormatException($"XT field {descriptor.Name}.{fieldName} is not scalar.");
                node.Fields[valueOffset] = ConvertValue(value, field.Type);
                return;
            }
            valueOffset += count;
        }
    }

    private static List<XtNode> PruneUnreachable(XtSchemaDefinition schema, List<XtNode> nodes)
    {
        var byIndex = nodes.ToDictionary(static node => node.Index);
        var reachable = new HashSet<int>();
        var pending = new Queue<int>();
        pending.Enqueue(nodes[0].Index);
        while (pending.Count != 0)
        {
            var index = pending.Dequeue();
            if (!reachable.Add(index) || !byIndex.TryGetValue(index, out var node))
                continue;
            var descriptor = schema.GetNode(node.Type);
            var valueOffset = 0;
            foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
            {
                if (!field.Transmit)
                    continue;
                var count = FieldValueCount(descriptor, field, Math.Max(0, node.VariableLength));
                if (field.Type == 'p')
                {
                    for (var fieldIndex = 0; fieldIndex < count; fieldIndex++)
                    {
                        var value = node.Fields[valueOffset + fieldIndex];
                        if (value.Kind == XtFieldKind.Pointer && value.Pointer != 0)
                            pending.Enqueue(value.Pointer);
                    }
                }
                valueOffset += count;
            }
        }
        return nodes.Where(node => reachable.Contains(node.Index)).ToList();
    }

    private static Dictionary<FieldKey, SourceField> IndexFields(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node)
    {
        var result = new Dictionary<FieldKey, SourceField>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, Math.Max(0, node.VariableLength));
            if (valueOffset + count > node.Fields.Length)
                throw new FormatException($"XT node {node.Type}/{node.Index} has fewer values than its schema.");
            var occurrence = occurrences.TryGetValue(field.Name, out var seen) ? seen : 0;
            occurrences[field.Name] = occurrence + 1;
            var values = new XtFieldValue[count];
            Array.Copy(node.Fields, valueOffset, values, 0, count);
            result.Add(new FieldKey(field.Name, occurrence), new SourceField(field.Type, values));
            valueOffset += count;
        }
        if (valueOffset != node.Fields.Length)
            throw new FormatException($"XT node {node.Type}/{node.Index} has extra values outside its schema.");
        return result;
    }

    private static void ClearDanglingPointers(XtSchemaDefinition schema, List<XtNode> nodes)
    {
        var indexes = nodes.Select(static node => node.Index).ToHashSet();
        foreach (var node in nodes)
        {
            var descriptor = schema.GetNode(node.Type);
            var valueOffset = 0;
            foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
            {
                if (!field.Transmit)
                    continue;
                var count = FieldValueCount(descriptor, field, Math.Max(0, node.VariableLength));
                if (field.Type == 'p')
                {
                    for (var index = 0; index < count; index++)
                    {
                        ref var value = ref node.Fields[valueOffset + index];
                        if (value.Kind == XtFieldKind.Pointer && value.Pointer != 0 && !indexes.Contains(value.Pointer))
                            value = XtFieldValue.Ptr(0);
                    }
                }
                valueOffset += count;
            }
        }
    }

    private static int FieldValueCount(XtNodeDescriptor descriptor, XtFieldDescriptor field, int variableLength)
        => field.ElementCount > 1
            ? field.ElementCount
            : descriptor.Variable && field.ElementCount == 1 ? variableLength : 1;

    private static bool HasNode(XtSchemaDefinition schema, string name)
    {
        foreach (var descriptor in schema.Nodes)
        {
            if (descriptor.Transmit && descriptor.Name == name)
                return true;
        }
        return false;
    }

    private static bool AreCompatible(char source, char target)
        => source == target || IsIntegral(source) && IsIntegral(target) || source is 'v' or 'h' && target is 'v' or 'h';

    private static bool IsIntegral(char type) => type is 'u' or 'd' or 'n' or 'w' or 't' or 'q';

    private static XtFieldValue ConvertValue(XtFieldValue value, char targetType)
    {
        if (value.Kind == XtFieldKind.Empty)
            return value;
        return targetType switch
        {
            'p' => XtFieldValue.Ptr(value.Pointer),
            'u' => XtFieldValue.Unsigned(value.Integer),
            'd' or 'n' or 'w' or 't' or 'q' => XtFieldValue.Int(value.Integer),
            _ => value,
        };
    }

    private static XtFieldValue DefaultValue(char type) => type switch
    {
        'p' => XtFieldValue.Ptr(0),
        'u' => XtFieldValue.Unsigned(0),
        'd' or 'n' or 'w' or 't' or 'q' => XtFieldValue.Int(0),
        'l' => XtFieldValue.Logical(false),
        _ => XtFieldValue.Null(),
    };

    private static bool IsDefault(XtFieldValue value) => value.Kind switch
    {
        XtFieldKind.Empty => true,
        XtFieldKind.Pointer => value.Pointer == 0,
        XtFieldKind.Integer or XtFieldKind.Unsigned or XtFieldKind.Logical => value.Integer == 0,
        XtFieldKind.Real => value.Real == 0,
        XtFieldKind.Character => value.Character is '\0' or '?',
        XtFieldKind.Vector or XtFieldKind.Interval => value.Vector.X == 0 && value.Vector.Y == 0 && value.Vector.Z == 0,
        XtFieldKind.Box => value.Vector.X == 0 && value.Vector.Y == 0 && value.Vector.Z == 0 && value.Fourth == 0 && value.Fifth == 0 && value.Sixth == 0,
        _ => false,
    };

    private static bool IsExplicitlyDroppableDefault(
        XtNodeDescriptor descriptor,
        FieldKey field,
        XtFieldValue[] values)
    {
        // Nominal geometry did not exist in earlier schemas.  The token value
        // one is the normal/classic geometry state and therefore carries no
        // additional semantics when the target schema has no such field.
        if (descriptor.Name == "BODY" && field.Name == "nom_geom_state" &&
            values.Length == 1 && values[0].Kind is XtFieldKind.Integer or XtFieldKind.Unsigned && values[0].Integer == 1)
            return true;

        // These are optional acceleration/recognition records.  The defining
        // surfaces, charts, ranges and NURBS data remain in the older schema;
        // Parasolid reconstructs the derived record on receive.
        if (descriptor.Name == "INTERSECTION" && field.Name == "intersection_data")
            return true;
        if (descriptor.Name == "CURVE_DATA" && field.Name == "analytic_form")
            return true;
        if (descriptor.Name == "LIMIT" && field.Name == "term_use" &&
            values.Length == 1 && values[0].Kind == XtFieldKind.Character && values[0].Character == 'F')
            return true;
        if (descriptor.Name == "CHART" && field.Name == "base_scale" &&
            values.Length == 1 && values[0].Kind == XtFieldKind.Real && values[0].Real == 1)
            return true;
        // Region ownership was introduced in Parasolid 6.  Earlier schemas
        // store the primary shell directly on BODY and derive the outside
        // region on receive.
        if (descriptor.Name == "BODY" && field.Name == "region")
            return true;
        if (descriptor.Name == "BODY" && field.Name is
            "edge" or "vertex" or "boundary_surface" or "boundary_curve" or "boundary_point")
            return true;
        if (descriptor.Name == "SHELL" && field.Name is "region" or "front_face")
            return true;
        if (descriptor.Name == "FACE" && field.Name is "next_front" or "previous_front" or "front_shell")
            return true;
        if (descriptor.Name == "EDGE" && field.Name is "owner" or "data")
            return true;
        if (descriptor.Name == "HALFEDGE" && field.Name is "next_at_vx" or "polyline" or "sense")
            return true;
        if (descriptor.Name == "VERTEX" && field.Name == "owner")
            return true;
        return false;
    }

    private static bool TryGetLegacyOverride(
        XtDocument source,
        XtNode sourceNode,
        XtSchemaDefinition target,
        XtNodeDescriptor targetDescriptor,
        XtFieldDescriptor targetField,
        out SourceField value)
    {
        value = default;
        var sourceDescriptor = source.Schema.GetNode(sourceNode.Type);
        if (sourceDescriptor.Name == "BODY" && targetDescriptor.Name == "BODY")
        {
            var boundaryName = targetField.Name switch
            {
                "surface" => "boundary_surface",
                "curve" => "boundary_curve",
                "point" => "boundary_point",
                _ => null,
            };
            if (boundaryName is not null && !HasField(target, targetDescriptor, boundaryName) &&
                TryGetField(source.Schema, sourceDescriptor, sourceNode, boundaryName, out value) &&
                value.Values.Any(static item => !IsDefault(item)))
                return true;
        }

        if (sourceDescriptor.Name == "SHELL" && targetDescriptor.Name == "SHELL" &&
            targetField.Name is "edge" or "vertex")
        {
            var targetBody = FindNode(target, "BODY");
            if (targetBody.Type != 0 && !HasField(target, targetBody, targetField.Name) &&
                TryGetField(source.Schema, sourceDescriptor, sourceNode, "body", out var bodyField) &&
                bodyField.Values.Length == 1 && bodyField.Values[0].Pointer != 0 &&
                FindNode(source.Nodes, bodyField.Values[0].Pointer) is { } bodyNode)
            {
                var bodyDescriptor = source.Schema.GetNode(bodyNode.Type);
                if (TryGetField(source.Schema, bodyDescriptor, bodyNode, targetField.Name, out value))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetField(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node,
        string name,
        out SourceField value)
        => IndexFields(schema, descriptor, node).TryGetValue(new FieldKey(name, 0), out value);

    private static bool HasField(XtSchemaDefinition schema, XtNodeDescriptor descriptor, string name)
    {
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (field.Transmit && field.Name == name)
                return true;
        }
        return false;
    }

    private static XtNodeDescriptor FindNode(XtSchemaDefinition schema, string name)
    {
        foreach (var descriptor in schema.Nodes)
        {
            if (descriptor.Transmit && descriptor.Name == name)
                return descriptor;
        }
        return default;
    }

    private static XtNode? FindNode(XtNode[] nodes, XtNodeIndex index)
    {
        foreach (var node in nodes)
        {
            if (node.Index == index)
                return node;
        }
        return null;
    }

    private static string Describe(XtFieldValue[] values)
        => string.Join(',', values.Select(static value => value.Kind switch
        {
            XtFieldKind.Pointer => "p:" + value.Pointer,
            XtFieldKind.Integer or XtFieldKind.Unsigned or XtFieldKind.Logical => "i:" + value.Integer,
            XtFieldKind.Real => "f:" + value.Real.ToString(System.Globalization.CultureInfo.InvariantCulture),
            XtFieldKind.Character => "c:" + value.Character + "/" + (int)value.Character,
            XtFieldKind.Empty => "?",
            _ => value.Kind.ToString(),
        }));

    private static bool ContainsNonDefault(this ReadOnlySpan<XtFieldValue> values)
    {
        foreach (var value in values)
        {
            if (!IsDefault(value))
                return true;
        }
        return false;
    }

    private readonly record struct FieldKey(string Name, int Occurrence);
    private readonly record struct SourceField(char Type, XtFieldValue[] Values);
    private readonly record struct TargetField(char Type, int ValueCount);
}
