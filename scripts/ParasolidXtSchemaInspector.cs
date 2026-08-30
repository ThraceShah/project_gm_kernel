using ProjectGmKernel.Native.Runtime;

public readonly record struct CorpusSchemaNode(string Name, int Count);

public readonly record struct CorpusSchemaDependency(string From, string To);

public sealed record CorpusXtInventory(
    CorpusSchemaNode[] Nodes,
    CorpusSchemaDependency[] Dependencies);

internal static class ParasolidXtSchemaInspector
{
    public static CorpusXtInventory Inspect(ReadOnlySpan<byte> bytes)
    {
        var inventory = XtCorpusInspection.Inspect(bytes);
        return new CorpusXtInventory(
            inventory.Nodes.Select(node => new CorpusSchemaNode(node.Name, node.Count)).ToArray(),
            inventory.Dependencies.Select(edge => new CorpusSchemaDependency(edge.From, edge.To)).ToArray());
    }
}
