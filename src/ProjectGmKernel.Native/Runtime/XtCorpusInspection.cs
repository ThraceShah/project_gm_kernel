using System.Text;
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

/// <summary>Read-only bridge to the production XT parser for corpus validation.</summary>
public static class XtCorpusInspection
{
    public static XtCorpusInventory Inspect(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var nodes = XtText.Decode(text);
        var byIndex = new Dictionary<int, XtNode>(nodes.Length);
        foreach (var node in nodes)
            byIndex[node.Index] = node;

        var nodeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var dependencies = new HashSet<XtCorpusSchemaDependency>();
        foreach (var node in nodes)
        {
            var descriptor = XtSchema.GetNode(node.Type);
            var name = descriptor.Name.Length == 0 ? "UNKNOWN_" + node.Type : descriptor.Name;
            nodeCounts[name] = nodeCounts.TryGetValue(name, out var count) ? count + 1 : 1;
            foreach (var field in node.Fields)
            {
                if (field.Kind != XtFieldKind.Pointer || field.Pointer == 0 || !byIndex.TryGetValue(field.Pointer, out var target))
                    continue;
                var targetDescriptor = XtSchema.GetNode(target.Type);
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
}
