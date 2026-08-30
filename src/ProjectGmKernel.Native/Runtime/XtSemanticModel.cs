namespace ProjectGmKernel.Native.Runtime;

internal enum XtSemanticKind : byte
{
    VersionExtension,
    PartBlock,
    Assembly,
    Instance,
    Body,
    Region,
    Shell,
    Face,
    Loop,
    Fin,
    Edge,
    Vertex,
    Point,
    Curve,
    Surface,
    Transform,
    Frame,
    AttributeDefinition,
    Attribute,
    Mesh,
    Lattice,
}

internal readonly record struct XtSemanticEntity(
    XtNodeIndex NodeIndex,
    XtNodeType NodeType,
    XtNodePosition NodePosition,
    XtSemanticKind Kind);

/// <summary>
/// Data-oriented semantic index over a lossless XT node graph.  The node graph
/// remains the version extension store; this projection gives common part,
/// topology, geometry, attribute and mesh entities stable typed categories
/// without copying their schema-defined field payloads.
/// </summary>
internal sealed class XtSemanticModel
{
    public required XtSemanticEntity[] Entities { get; init; }
    public required XtNodePosition[] PartRoots { get; init; }
    public required XtNodePosition[] Bodies { get; init; }
    public required XtNodePosition[] Topology { get; init; }
    public required XtNodePosition[] Geometry { get; init; }
    public required XtNodePosition[] AssembliesAndInstances { get; init; }
    public required XtNodePosition[] Attributes { get; init; }
    public required XtNodePosition[] MeshAndLattice { get; init; }
    public required XtNodePosition[] VersionExtensions { get; init; }
}

internal static class XtSemanticProjector
{
    public static XtSemanticModel Project(XtDocument document)
    {
        var entities = new XtSemanticEntity[document.Nodes.Length];
        var bodies = new List<XtNodePosition>();
        var topology = new List<XtNodePosition>();
        var geometry = new List<XtNodePosition>();
        var assembly = new List<XtNodePosition>();
        var attributes = new List<XtNodePosition>();
        var mesh = new List<XtNodePosition>();
        var extensions = new List<XtNodePosition>();
        var positions = new Dictionary<XtNodeIndex, XtNodePosition>(document.Nodes.Length);

        for (var position = 0; position < document.Nodes.Length; position++)
        {
            var node = document.Nodes[position];
            if (!positions.TryAdd(node.Index, position))
                throw new FormatException($"XT node index {node.Index} is duplicated.");
            var descriptor = document.Schema.GetNode(node.Type);
            if (descriptor.Type == 0)
                throw new FormatException($"XT node type {node.Type} is absent from schema {document.Schema.Identity}.");
            var kind = Classify(descriptor.Name);
            entities[position] = new XtSemanticEntity(node.Index, node.Type, position, kind);
            switch (kind)
            {
                case XtSemanticKind.Body:
                    bodies.Add(position);
                    break;
                case XtSemanticKind.Region:
                case XtSemanticKind.Shell:
                case XtSemanticKind.Face:
                case XtSemanticKind.Loop:
                case XtSemanticKind.Fin:
                case XtSemanticKind.Edge:
                case XtSemanticKind.Vertex:
                    topology.Add(position);
                    break;
                case XtSemanticKind.Point:
                case XtSemanticKind.Curve:
                case XtSemanticKind.Surface:
                case XtSemanticKind.Transform:
                case XtSemanticKind.Frame:
                    geometry.Add(position);
                    break;
                case XtSemanticKind.Assembly:
                case XtSemanticKind.Instance:
                    assembly.Add(position);
                    break;
                case XtSemanticKind.AttributeDefinition:
                case XtSemanticKind.Attribute:
                    attributes.Add(position);
                    break;
                case XtSemanticKind.Mesh:
                case XtSemanticKind.Lattice:
                    mesh.Add(position);
                    break;
                case XtSemanticKind.VersionExtension:
                    extensions.Add(position);
                    break;
            }
        }

        var roots = FindRoots(document, positions);
        return new XtSemanticModel
        {
            Entities = entities,
            PartRoots = roots,
            Bodies = bodies.ToArray(),
            Topology = topology.ToArray(),
            Geometry = geometry.ToArray(),
            AssembliesAndInstances = assembly.ToArray(),
            Attributes = attributes.ToArray(),
            MeshAndLattice = mesh.ToArray(),
            VersionExtensions = extensions.ToArray(),
        };
    }

    private static XtNodePosition[] FindRoots(XtDocument document, Dictionary<XtNodeIndex, XtNodePosition> positions)
    {
        var rootIndexes = XtPartGraph.GetRootIndexes(document);
        var roots = new XtNodePosition[rootIndexes.Length];
        for (var index = 0; index < roots.Length; index++)
        {
            if (!positions.TryGetValue(rootIndexes[index], out roots[index]))
                throw new FormatException($"XT part container references missing node {rootIndexes[index]}.");
        }
        return roots;
    }

    private static XtSemanticKind Classify(string name)
    {
        return name switch
        {
            "PART_XMT_BLOCK" => XtSemanticKind.PartBlock,
            "ASSEMBLY" => XtSemanticKind.Assembly,
            "INSTANCE" => XtSemanticKind.Instance,
            "BODY" => XtSemanticKind.Body,
            "REGION" => XtSemanticKind.Region,
            "SHELL" => XtSemanticKind.Shell,
            "FACE" => XtSemanticKind.Face,
            "LOOP" => XtSemanticKind.Loop,
            "HALFEDGE" or "FIN" => XtSemanticKind.Fin,
            "EDGE" => XtSemanticKind.Edge,
            "VERTEX" => XtSemanticKind.Vertex,
            "POINT" => XtSemanticKind.Point,
            "TRANSFORM" => XtSemanticKind.Transform,
            "FRAME" => XtSemanticKind.Frame,
            "ATTRIB_DEF" or "ATT_DEF_ID" or "FIELD_NAMES" => XtSemanticKind.AttributeDefinition,
            "ATTRIBUTE" or "FEATURE" or "MEMBER_OF_FEATURE" or
                "INT_VALUES" or "REAL_VALUES" or "CHAR_VALUES" or "POINT_VALUES" or
                "VECTOR_VALUES" or "POINTER_VALUES" or "UNICODE_VALUES" => XtSemanticKind.Attribute,
            "LATTICE" or "LTOPOL" or "LROD" or "LBALL" or "IJKBOX" => XtSemanticKind.Lattice,
            _ when name.StartsWith("LATTICE_", StringComparison.Ordinal) => XtSemanticKind.Lattice,
            _ when IsCurve(name) => XtSemanticKind.Curve,
            _ when IsSurface(name) => XtSemanticKind.Surface,
            _ when IsMesh(name) => XtSemanticKind.Mesh,
            _ => XtSemanticKind.VersionExtension,
        };
    }

    private static bool IsCurve(string name)
        => name is "LINE" or "CIRCLE" or "ELLIPSE" or "PARABOLA" or "HYPERBOLA" or
            "PARACURVE" or "INTERSECTION" or "SILHOUETTE" or "CPC" or "PCURVE" or
            "TRIMMED_CURVE" or "B_CURVE" or "SP_CURVE" or "FCURVE" or "CP_CURVE" or "TR_CURVE" or
            "PE_CURVE" or "OFFSET_CURVE" or "MESH_INTERSECTION" ||
            name.EndsWith("_CURVE", StringComparison.Ordinal);

    private static bool IsSurface(string name)
        => name is "PLANE" or "CYLINDER" or "CONE" or "SPHERE" or "TORUS" or "PIPE" or
            "PARASURF" or "SILH_SURF" or "OFFSET_SURF" or "SWEPT_SURF" or "SPUN_SURF" or
            "B_SURFACE" or "PE_SURF" or "CPS" or "BLENDSF" or "FSURF" ||
            name.EndsWith("_SURF", StringComparison.Ordinal) || name.EndsWith("_SURFACE", StringComparison.Ordinal);

    private static bool IsMesh(string name)
        => name is "MESH" or "PLINE" or "MTOPOL" or "MFACET" or "MFIN" or "MVERTEX" ||
            name.StartsWith("MESH_", StringComparison.Ordinal) || name.EndsWith("_MESH", StringComparison.Ordinal);
}
