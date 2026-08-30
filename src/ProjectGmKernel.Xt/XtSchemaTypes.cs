namespace ProjectGmKernel.Xt;

public readonly record struct XtNodeDescriptor(
    XtNodeType Type,
    string Name,
    string Description,
    bool Transmit,
    XtFieldCount DeclaredFieldCount,
    bool Variable,
    XtFieldIndex FieldOffset,
    XtFieldCount ParsedFieldCount);

public readonly record struct XtFieldDescriptor(
    XtNodeType OwnerType,
    string Name,
    char Type,
    bool Transmit,
    XtNodeClass NodeClass,
    XtElementCount ElementCount);

public readonly record struct XtSchemaInfo(
    string Identity,
    string FileName,
    XtModelerVersion ModelerVersion,
    XtSchemaNumber SchemaNumber,
    XtNodeCount NodeCount,
    XtFieldCount FieldCount);

public sealed class XtSchemaDefinition
{
    private readonly XtNodeDescriptor[] _nodes;
    private readonly XtFieldDescriptor[] _fields;

    internal XtSchemaDefinition(
        XtSchemaInfo info,
        XtNodeDescriptor[] nodes,
        XtFieldDescriptor[] fields)
    {
        Info = info;
        _nodes = nodes;
        _fields = fields;
    }

    public XtSchemaInfo Info { get; }
    public string Identity => Info.Identity;
    public XtModelerVersion ModelerVersion => Info.ModelerVersion;
    public XtSchemaNumber SchemaNumber => Info.SchemaNumber;
    public ReadOnlySpan<XtNodeDescriptor> Nodes => _nodes;
    public ReadOnlySpan<XtFieldDescriptor> Fields => _fields;

    public bool TryGetNode(XtNodeType type, out XtNodeDescriptor descriptor)
    {
        foreach (var node in _nodes)
        {
            if (node.Type != type)
                continue;
            descriptor = node;
            return true;
        }
        descriptor = default;
        return false;
    }

    public XtNodeDescriptor GetNode(XtNodeType type)
        => TryGetNode(type, out var descriptor) ? descriptor : default;

    public ReadOnlySpan<XtFieldDescriptor> GetFields(XtNodeDescriptor node)
        => _fields.AsSpan(node.FieldOffset, node.ParsedFieldCount);
}
