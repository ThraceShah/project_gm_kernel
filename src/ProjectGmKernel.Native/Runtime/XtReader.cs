using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe class XtReader
{
    public static int ReadText(string text, out EntityTag[] parts)
    {
        parts = [];
        XtNode[] nodes;
        try
        {
            nodes = XtText.Decode(text);
        }
        catch (FormatException)
        {
            return ParasolidConstants.PK_ERROR_corrupt_file;
        }
        catch (NotSupportedException)
        {
            return ParasolidConstants.PK_ERROR_bad_file_format;
        }

        if (nodes.Length == 0 || (nodes[0].Type != (int)XtNodeTypes.Body && nodes[0].Type != (int)XtNodeTypes.PartTransmitBlock))
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var result = new List<EntityTag>();
        foreach (var body in GetBodyNodes(nodes))
        {
            var error = MaterializeBody(nodes, body, out var tag);
            if (error != ParasolidConstants.PK_ERROR_no_errors)
                return error;
            result.Add(tag);
        }

        parts = result.ToArray();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static IEnumerable<XtNode> GetBodyNodes(XtNode[] nodes)
    {
        if (nodes[0].Type == (int)XtNodeTypes.PartTransmitBlock)
        {
            var fields = nodes[0].Fields;
            if (fields.Length < 5)
                throw new FormatException("Invalid XT part transmit block.");

            var count = checked((int)fields[0].Integer);
            if (count < 0 || fields.Length != 5 + count)
                throw new FormatException("Invalid XT part transmit block entries.");

            for (var i = 0; i < count; i++)
            {
                var body = FindNode(nodes, fields[5 + i].Pointer, (int)XtNodeTypes.Body);
                if (body is null)
                    throw new FormatException("XT part transmit block references a missing body.");
                yield return body;
            }
            yield break;
        }

        foreach (var body in nodes.Where(node => node.Type == (int)XtNodeTypes.Body))
            yield return body;
    }

    private static int MaterializeBody(XtNode[] nodes, XtNode body, out EntityTag bodyTag)
    {
        bodyTag = 0;
        var bodyFields = ReadBodyFields(body);
        var edgeCount = CountChain(nodes, bodyFields.Edge, (int)XtNodeTypes.Edge, ReadEdgeFields, fields => fields.Next);
        var vertexCount = CountChain(nodes, bodyFields.Vertex, (int)XtNodeTypes.Vertex, ReadVertexFields, fields => fields.Next);

        if (edgeCount == 2 && vertexCount == 0)
            return MaterializeCylinder(nodes, bodyFields, out bodyTag);
        if (edgeCount == 12 && vertexCount == 8)
            return MaterializeBlock(out bodyTag);

        return ParasolidConstants.PK_ERROR_bad_file_format;
    }

    private static int MaterializeCylinder(XtNode[] nodes, BodyNodeFields fields, out EntityTag bodyTag)
    {
        bodyTag = 0;
        XtNode? sideFace = null;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Type == (int)XtNodeTypes.Face)
            {
                var candidateFields = ReadFaceFields(nodes[i]);
                if (FindNode(nodes, candidateFields.Surface, (int)XtNodeTypes.Cylinder) is not null)
                {
                    sideFace = nodes[i];
                    break;
                }
            }
        }
        if (sideFace is null)
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var faceFields = ReadFaceFields(sideFace);
        var cylinder = FindNode(nodes, faceFields.Surface, (int)XtNodeTypes.Cylinder);
        if (cylinder is null)
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var cylinderFields = ReadCylinderFields(cylinder);
        if (!TryFindCylinderHeight(nodes, fields.Edge, cylinderFields.Point, cylinderFields.Axis, out var height))
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var axis = new PK_AXIS2_sf_s();
        axis.location.coord[0] = cylinderFields.Point.X;
        axis.location.coord[1] = cylinderFields.Point.Y;
        axis.location.coord[2] = cylinderFields.Point.Z;
        axis.axis.coord[0] = cylinderFields.Axis.X;
        axis.axis.coord[1] = cylinderFields.Axis.Y;
        axis.axis.coord[2] = cylinderFields.Axis.Z;
        axis.ref_direction.coord[0] = cylinderFields.XAxis.X;
        axis.ref_direction.coord[1] = cylinderFields.XAxis.Y;
        axis.ref_direction.coord[2] = cylinderFields.XAxis.Z;

        var localTag = 0;
        var error = KernelRuntime.CreateSolidCylinderCore(cylinderFields.Radius, height, &axis, &localTag);
        bodyTag = localTag;
        return error;
    }

    private static bool TryFindCylinderHeight(XtNode[] nodes, XtNodeIndex firstEdge, XtVector origin, XtVector axis, out double height)
    {
        height = 0;
        var edgeIndex = firstEdge;
        var first = firstEdge;
        var guard = 0;
        while (edgeIndex != 0 && guard++ < nodes.Length)
        {
            var edge = FindNode(nodes, edgeIndex, (int)XtNodeTypes.Edge);
            if (edge is null)
                return false;

            var edgeFields = ReadEdgeFields(edge);
            var circle = FindNode(nodes, edgeFields.Curve, (int)XtNodeTypes.Circle);
            if (circle is null)
                return false;

            var circleFields = ReadCircleFields(circle);
            var projected = Math.Abs(Dot(Subtract(circleFields.Centre, origin), axis));
            if (projected > height)
                height = projected;
            edgeIndex = edgeFields.Next;
            if (edgeIndex == first)
                break;
        }

        return height > 0;
    }

    private static int MaterializeBlock(out EntityTag bodyTag)
    {
        bodyTag = 0;
        var localTag = 0;
        var error = KernelRuntime.CreateSolidBlockCore(1, 1, 1, null, &localTag);
        bodyTag = localTag;
        return error;
    }

    private static int CountChain<TFields>(XtNode[] nodes, XtNodeIndex first, XtNodeType expectedType, Func<XtNode, TFields> read, Func<TFields, XtNodeIndex> next)
    {
        var count = 0;
        var index = first;
        var firstIndex = first;
        while (index != 0)
        {
            var node = FindNode(nodes, index, expectedType);
            if (node is null)
                return -1;
            count++;
            index = next(read(node));
            if (index == firstIndex)
                break;
            if (count > nodes.Length)
                return -1;
        }

        return count;
    }

    private static XtNode? FindNode(XtNode[] nodes, XtNodeIndex index, XtNodeType expectedType)
    {
        if (index == 0)
            return null;
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Index == index && nodes[i].Type == expectedType)
                return nodes[i];
        }
        return null;
    }

    private static XtVector Subtract(XtVector a, XtVector b)
    {
        return new XtVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    private static double Dot(XtVector a, XtVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static BodyNodeFields ReadBodyFields(XtNode node) => new(
        Shell: node.Fields[19].Pointer,
        Region: node.Fields[26].Pointer,
        Edge: node.Fields[27].Pointer,
        Vertex: node.Fields[28].Pointer);

    private static FaceNodeFields ReadFaceFields(XtNode node) => new(
        Next: node.Fields[3].Pointer,
        Loop: node.Fields[5].Pointer,
        Shell: node.Fields[6].Pointer,
        Surface: node.Fields[7].Pointer,
        FrontShell: node.Fields[13].Pointer);

    private static EdgeNodeFields ReadEdgeFields(XtNode node) => new(
        Halfedge: node.Fields[3].Pointer,
        Next: node.Fields[5].Pointer,
        Curve: node.Fields[6].Pointer);

    private static VertexNodeFields ReadVertexFields(XtNode node) => new(Next: node.Fields[4].Pointer);

    private static CylinderNodeFields ReadCylinderFields(XtNode node) => new(
        Point: node.Fields[7].Vector,
        Axis: node.Fields[8].Vector,
        Radius: node.Fields[9].Real,
        XAxis: node.Fields[10].Vector);

    private static CircleNodeFields ReadCircleFields(XtNode node) => new(
        Centre: node.Fields[7].Vector,
        Normal: node.Fields[8].Vector,
        Radius: node.Fields[10].Real,
        XAxis: node.Fields[9].Vector);

    private readonly record struct BodyNodeFields(XtNodeIndex Shell, XtNodeIndex Region, XtNodeIndex Edge, XtNodeIndex Vertex);
    private readonly record struct FaceNodeFields(XtNodeIndex Next, XtNodeIndex Loop, XtNodeIndex Shell, XtNodeIndex Surface, XtNodeIndex FrontShell);
    private readonly record struct EdgeNodeFields(XtNodeIndex Halfedge, XtNodeIndex Next, XtNodeIndex Curve);
    private readonly record struct VertexNodeFields(XtNodeIndex Next);
    private readonly record struct CylinderNodeFields(XtVector Point, XtVector Axis, double Radius, XtVector XAxis);
    private readonly record struct CircleNodeFields(XtVector Centre, XtVector Normal, double Radius, XtVector XAxis);
}
