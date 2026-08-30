
namespace ProjectGmKernel.Xt;

public enum XtCompoundReceiveMode
{
    Keep,
    Split,
    Fail,
}

public static class XtPartGraph
{
    public static bool ApplyCompoundReceiveMode(
        XtDocument document,
        ReadOnlySpan<XtNodeIndex> selectedRoots,
        XtCompoundReceiveMode receiveCompound,
        out XtDocument result,
        out XtNodeIndex[] roots)
    {
        result = document;
        roots = selectedRoots.ToArray();
        var byIndex = document.Nodes.ToDictionary(static node => node.Index);
        var expanded = new List<XtNodeIndex>(selectedRoots.Length);
        var compoundParents = new HashSet<XtNodeIndex>();
        foreach (var rootIndex in selectedRoots)
        {
            if (!byIndex.TryGetValue(rootIndex, out var root))
                throw new FormatException($"XT part root {rootIndex} is missing.");
            var descriptor = document.Schema.GetNode(root.Type);
            if (descriptor.Name != "BODY" || ReadPointer(document.Schema, root, descriptor, "child") == 0)
            {
                expanded.Add(rootIndex);
                continue;
            }
            if (receiveCompound == XtCompoundReceiveMode.Fail)
                return false;
            if (receiveCompound == XtCompoundReceiveMode.Keep)
            {
                expanded.Add(rootIndex);
                continue;
            }

            compoundParents.Add(rootIndex);
            var childIndex = ReadPointer(document.Schema, root, descriptor, "child");
            var visited = new HashSet<XtNodeIndex>();
            while (childIndex != 0)
            {
                if (!visited.Add(childIndex) || !byIndex.TryGetValue(childIndex, out var child))
                    throw new FormatException("XT compound child chain is cyclic or missing.");
                if (document.Schema.GetNode(child.Type).Name != "BODY")
                    throw new FormatException("XT compound child is not a BODY node.");
                expanded.Add(childIndex);
                childIndex = ReadPointer(document.Schema, child, document.Schema.GetNode(child.Type), "next");
            }
        }
        if (compoundParents.Count == 0 || receiveCompound == XtCompoundReceiveMode.Keep)
        {
            roots = expanded.ToArray();
            return true;
        }

        var containerIndexes = RootContainerIndexes(document);
        var clones = new List<XtNode>(document.Nodes.Length);
        foreach (var source in document.Nodes)
        {
            if (compoundParents.Contains(source.Index) || containerIndexes.Contains(source.Index))
                continue;
            clones.Add(CloneNode(source));
        }
        var cloneByIndex = clones.ToDictionary(static node => node.Index);
        var rootSet = expanded.ToHashSet();
        var ownership = new Dictionary<XtNodeIndex, XtNodeIndex>();
        foreach (var childRoot in expanded)
            MarkReachableOwner(document.Schema, cloneByIndex, rootSet, compoundParents, childRoot, ownership);

        foreach (var node in clones)
        {
            var descriptor = document.Schema.GetNode(node.Type);
            RewriteParentPointers(document.Schema, descriptor, node, compoundParents,
                ownership.TryGetValue(node.Index, out var owner) ? owner : 0);
            if (rootSet.Contains(node.Index) && (descriptor.Name is "BODY" or "ASSEMBLY"))
            {
                SetPointer(document.Schema, descriptor, node, "owner", 0);
                SetPointer(document.Schema, descriptor, node, "next", 0);
                SetPointer(document.Schema, descriptor, node, "previous", 0);
                SetPointer(document.Schema, descriptor, node, "child", 0);
            }
        }

        var split = new XtDocument
        {
            VersionText = document.VersionText,
            HeaderSchemaIdentity = document.HeaderSchemaIdentity,
            Schema = document.Schema,
            BaseSchema = document.BaseSchema,
            EmbeddedMaxNodeType = document.EmbeddedMaxNodeType,
            UserFieldSize = document.UserFieldSize,
            Nodes = clones.ToArray(),
        };
        result = WrapInCurrentPartBlock(split, expanded.ToArray());
        roots = expanded.ToArray();
        return true;
    }

    public static XtNodeIndex[] GetRootIndexes(XtDocument document)
    {
        if (document.Nodes.Length == 0)
            throw new FormatException("XT document contains no nodes.");
        var first = document.Nodes[0];
        var descriptor = document.Schema.GetNode(first.Type);
        return descriptor.Name switch
        {
            "BODY" or "ASSEMBLY" => [first.Index],
            "PART_XMT_BLOCK" => ReadEntries(document.Schema, first, descriptor),
            "POINTER_LIS_BLOCK" => ReadLegacyPointerList(document, first),
            _ => throw new FormatException($"XT root node {descriptor.Name}/{first.Index} is not a part container."),
        };
    }

    public static XtDocument WrapInCurrentPartBlock(XtDocument document, ReadOnlySpan<XtNodeIndex> roots)
    {
        var descriptor = FindNode(document.Schema, "PART_XMT_BLOCK");
        if (descriptor.Type == 0)
            throw new NotSupportedException($"Schema {document.Schema.Identity} has no PART_XMT_BLOCK node.");
        var blockIndex = NextNodeIndex(document.Nodes);
        var values = new List<XtFieldValue>();
        foreach (var field in document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = field.ElementCount > 1
                ? field.ElementCount
                : descriptor.Variable && field.ElementCount == 1 ? roots.Length : 1;
            if (field.Name == "n_entries")
            {
                values.Add(XtFieldValue.Int(roots.Length));
                continue;
            }
            if (field.Name == "entries" && field.Type == 'p' && field.ElementCount == 1)
            {
                foreach (var root in roots)
                    values.Add(XtFieldValue.Ptr(root));
                continue;
            }
            for (var index = 0; index < count; index++)
                values.Add(DefaultValue(field.Type));
        }

        var nodes = new XtNode[document.Nodes.Length + 1];
        nodes[0] = new XtNode
        {
            Type = descriptor.Type,
            Index = blockIndex,
            VariableLength = roots.Length,
            Fields = values.ToArray(),
        };
        document.Nodes.CopyTo(nodes, 1);
        return new XtDocument
        {
            VersionText = document.VersionText,
            HeaderSchemaIdentity = document.HeaderSchemaIdentity,
            Schema = document.Schema,
            BaseSchema = document.BaseSchema,
            EmbeddedMaxNodeType = document.EmbeddedMaxNodeType,
            UserFieldSize = document.UserFieldSize,
            Nodes = nodes,
        };
    }

    private static XtNodeIndex[] ReadLegacyPointerList(XtDocument document, XtNode first)
    {
        var byIndex = document.Nodes.ToDictionary(static node => node.Index);
        var roots = new List<XtNodeIndex>();
        var visited = new HashSet<XtNodeIndex>();
        var current = first;
        while (true)
        {
            if (!visited.Add(current.Index))
                throw new FormatException("XT legacy part-list block chain contains a cycle.");
            var descriptor = document.Schema.GetNode(current.Type);
            if (descriptor.Name != "POINTER_LIS_BLOCK")
                throw new FormatException("XT legacy part-list chain references a non-list node.");
            roots.AddRange(ReadEntries(document.Schema, current, descriptor));
            var next = ReadPointer(document.Schema, current, descriptor, "next_block");
            if (next == 0)
                break;
            if (!byIndex.TryGetValue(next, out current!))
                throw new FormatException($"XT legacy part-list references missing block {next}.");
        }
        if (roots.Count == 0)
            throw new FormatException("XT legacy part list is empty.");
        return roots.ToArray();
    }

    private static XtNodeIndex[] ReadEntries(
        XtSchemaDefinition schema,
        XtNode node,
        XtNodeDescriptor descriptor)
    {
        var entries = Array.Empty<XtNodeIndex>();
        var declaredCount = -1;
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, node.VariableLength);
            if (valueOffset + count > node.Fields.Length)
                throw new FormatException($"XT node {node.Type}/{node.Index} has fewer values than its schema.");
            if (field.Name == "n_entries")
                declaredCount = checked((int)node.Fields[valueOffset].Integer);
            else if (field.Name == "entries" && field.Type == 'p' && field.ElementCount == 1)
            {
                entries = new XtNodeIndex[count];
                for (var index = 0; index < count; index++)
                    entries[index] = node.Fields[valueOffset + index].Pointer;
            }
            valueOffset += count;
        }
        if (valueOffset != node.Fields.Length || declaredCount != entries.Length)
            throw new FormatException($"XT part-list node {node.Type}/{node.Index} has inconsistent entries.");
        return entries;
    }

    private static XtNodeIndex ReadPointer(
        XtSchemaDefinition schema,
        XtNode node,
        XtNodeDescriptor descriptor,
        string fieldName)
    {
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, node.VariableLength);
            if (field.Name == fieldName && field.Type == 'p' && count == 1)
                return node.Fields[valueOffset].Pointer;
            valueOffset += count;
        }
        return 0;
    }

    private static void SetPointer(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node,
        string fieldName,
        XtNodeIndex pointer)
    {
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, node.VariableLength);
            if (field.Name == fieldName && field.Type == 'p' && count == 1)
            {
                node.Fields[valueOffset] = XtFieldValue.Ptr(pointer);
                return;
            }
            valueOffset += count;
        }
    }

    private static HashSet<XtNodeIndex> RootContainerIndexes(XtDocument document)
    {
        var result = new HashSet<XtNodeIndex>();
        var first = document.Nodes[0];
        var descriptor = document.Schema.GetNode(first.Type);
        if (descriptor.Name == "PART_XMT_BLOCK")
            result.Add(first.Index);
        else if (descriptor.Name == "POINTER_LIS_BLOCK")
        {
            var byIndex = document.Nodes.ToDictionary(static node => node.Index);
            var current = first;
            while (result.Add(current.Index))
            {
                var next = ReadPointer(document.Schema, current, document.Schema.GetNode(current.Type), "next_block");
                if (next == 0)
                    break;
                if (!byIndex.TryGetValue(next, out current!))
                    throw new FormatException($"XT legacy part-list references missing block {next}.");
            }
        }
        return result;
    }

    private static XtNode CloneNode(XtNode source) => new()
    {
        Type = source.Type,
        Index = source.Index,
        VariableLength = source.VariableLength,
        Fields = source.Fields.ToArray(),
        UserFields = source.UserFields.ToArray(),
    };

    private static void MarkReachableOwner(
        XtSchemaDefinition schema,
        Dictionary<XtNodeIndex, XtNode> nodes,
        HashSet<XtNodeIndex> partRoots,
        HashSet<XtNodeIndex> compoundParents,
        XtNodeIndex childRoot,
        Dictionary<XtNodeIndex, XtNodeIndex> ownership)
    {
        var pending = new Queue<XtNodeIndex>();
        var visited = new HashSet<XtNodeIndex>();
        pending.Enqueue(childRoot);
        while (pending.Count != 0)
        {
            var index = pending.Dequeue();
            if (!visited.Add(index) || !nodes.TryGetValue(index, out var node))
                continue;
            if (!ownership.TryAdd(index, childRoot) && ownership[index] != childRoot)
                ownership[index] = 0;
            var descriptor = schema.GetNode(node.Type);
            var valueOffset = 0;
            foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
            {
                if (!field.Transmit)
                    continue;
                var count = FieldValueCount(descriptor, field, node.VariableLength);
                if (field.Type == 'p')
                {
                    for (var fieldIndex = 0; fieldIndex < count; fieldIndex++)
                    {
                        var pointer = node.Fields[valueOffset + fieldIndex].Pointer;
                        if (pointer == 0 || compoundParents.Contains(pointer) ||
                            partRoots.Contains(pointer) && pointer != childRoot)
                            continue;
                        if ((descriptor.Name is "BODY" or "ASSEMBLY") &&
                            (field.Name is "owner" or "next" or "previous" or "child"))
                            continue;
                        pending.Enqueue(pointer);
                    }
                }
                valueOffset += count;
            }
        }
    }

    private static void RewriteParentPointers(
        XtSchemaDefinition schema,
        XtNodeDescriptor descriptor,
        XtNode node,
        HashSet<XtNodeIndex> compoundParents,
        XtNodeIndex replacement)
    {
        var valueOffset = 0;
        foreach (var field in schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit)
                continue;
            var count = FieldValueCount(descriptor, field, node.VariableLength);
            if (field.Type == 'p')
            {
                for (var index = 0; index < count; index++)
                {
                    if (compoundParents.Contains(node.Fields[valueOffset + index].Pointer))
                        node.Fields[valueOffset + index] = XtFieldValue.Ptr(replacement);
                }
            }
            valueOffset += count;
        }
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

    private static XtNodeIndex NextNodeIndex(XtNode[] nodes)
    {
        var maximum = 0;
        foreach (var node in nodes)
            maximum = Math.Max(maximum, node.Index);
        return checked(maximum + 1);
    }

    private static int FieldValueCount(XtNodeDescriptor descriptor, XtFieldDescriptor field, int variableLength)
        => field.ElementCount > 1
            ? field.ElementCount
            : descriptor.Variable && field.ElementCount == 1 ? Math.Max(0, variableLength) : 1;

    private static XtFieldValue DefaultValue(char type) => type switch
    {
        'p' => XtFieldValue.Ptr(0),
        'u' => XtFieldValue.Unsigned(0),
        'd' or 'n' or 'w' or 't' or 'q' => XtFieldValue.Int(0),
        'l' => XtFieldValue.Logical(false),
        'c' => XtFieldValue.Char('?'),
        'f' => XtFieldValue.RealValue(0),
        'v' or 'h' => XtFieldValue.Vec(0, 0, 0),
        'i' => XtFieldValue.IntervalValue(0, 0),
        'b' => XtFieldValue.BoxValue(0, 0, 0, 0, 0, 0),
        _ => XtFieldValue.Null(),
    };
}
