using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe class XtReader
{
    public static int ReadText(string text, out EntityTag[] parts)
        => ReadText(text, receiveUserFields: true, [], ParasolidConstants.PK_receive_compound_split_c, out parts);

    public static int ReadText(string text, bool receiveUserFields, out EntityTag[] parts)
        => ReadText(text, receiveUserFields, [], ParasolidConstants.PK_receive_compound_split_c, out parts);

    public static int ReadText(
        string text,
        bool receiveUserFields,
        ReadOnlySpan<int> partIndices,
        int receiveCompound,
        out EntityTag[] parts)
    {
        parts = [];
        XtDocument document;
        try
        {
            document = XtText.DecodeDocument(text);
            document = XtText.SelectUserFields(document, receiveUserFields);
            _ = document.SemanticModel;
        }
        catch (FormatException)
        {
            return ParasolidConstants.PK_ERROR_corrupt_file;
        }
        catch (NotSupportedException)
        {
            return ParasolidConstants.PK_ERROR_bad_file_format;
        }

        var result = new List<EntityTag>();
        try
        {
            var allRootIndexes = XtPartGraph.GetRootIndexes(document);
            if (partIndices.Length != 0 && partIndices[^1] >= allRootIndexes.Length)
                return ParasolidConstants.PK_ERROR_bad_index;
            XtNodeIndex[] rootIndexes;
            if (partIndices.Length == 0)
            {
                rootIndexes = allRootIndexes;
            }
            else
            {
                rootIndexes = new XtNodeIndex[partIndices.Length];
                for (var index = 0; index < rootIndexes.Length; index++)
                    rootIndexes[index] = allRootIndexes[partIndices[index]];
            }
            var compoundError = XtPartGraph.ApplyCompoundReceiveMode(
                document,
                rootIndexes,
                receiveCompound,
                out document,
                out rootIndexes);
            if (compoundError != ParasolidConstants.PK_ERROR_no_errors)
                return compoundError;
            var nodes = document.Nodes;
            foreach (var rootIndex in rootIndexes)
            {
                var root = FindPartNode(document, rootIndex)
                    ?? throw new FormatException("XT part container references a missing part.");
                EntityTag tag = 0;
                var error = root.Type == (int)XtNodeTypes.Body && document.Schema.SchemaNumber == XtSchema.SchemaNumber
                    ? MaterializeBody(nodes, root, out tag)
                    : ParasolidConstants.PK_ERROR_bad_file_format;
                if (error == ParasolidConstants.PK_ERROR_no_errors)
                    KernelRuntime.AttachReceivedXt(tag, document, root.Index, opaque: false);
                else
                    error = KernelRuntime.CreateOpaquePartCore(document, root, out tag);
                if (error != ParasolidConstants.PK_ERROR_no_errors)
                    return error;
                result.Add(tag);
            }
        }
        catch (FormatException)
        {
            return ParasolidConstants.PK_ERROR_corrupt_file;
        }

        parts = result.ToArray();
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static XtNode? FindPartNode(XtDocument document, XtNodeIndex index)
    {
        if (index == 0)
            return null;
        for (var i = 0; i < document.Nodes.Length; i++)
        {
            var node = document.Nodes[i];
            if (node.Index != index)
                continue;
            var name = document.Schema.GetNode(node.Type).Name;
            if (name is "ASSEMBLY" or "BODY")
                return node;
        }
        return null;
    }

    private static int MaterializeBody(XtNode[] nodes, XtNode body, out EntityTag bodyTag)
    {
        bodyTag = 0;
        var bodyFields = ReadBodyFields(body);
        var edgeCount = CountChain(nodes, bodyFields.Edge, (int)XtNodeTypes.Edge, ReadEdgeFields, fields => fields.Next);
        var vertexCount = CountChain(nodes, bodyFields.Vertex, (int)XtNodeTypes.Vertex, ReadVertexFields, fields => fields.Next);

        var cone = nodes.FirstOrDefault(node => node.Type == (int)XtNodeTypes.Cone);
        if (cone is not null && (edgeCount == 1 || edgeCount == 2))
            return MaterializeCone(nodes, cone, edgeCount, out bodyTag);

        if (edgeCount == 2 && vertexCount == 0)
            return MaterializeCylinder(nodes, bodyFields, out bodyTag);
        if (edgeCount == 12 && vertexCount == 8)
            return MaterializeBlock(out bodyTag);

        var sphere = nodes.FirstOrDefault(node => node.Type == (int)XtNodeTypes.Sphere);
        if (sphere is not null && edgeCount == 0 && vertexCount == 0)
            return MaterializeSphere(sphere, out bodyTag);

        var torus = nodes.FirstOrDefault(node => node.Type == (int)XtNodeTypes.Torus);
        if (torus is not null && edgeCount == 0 && vertexCount == 0)
            return MaterializeTorus(torus, out bodyTag);

        if (vertexCount >= 6 && edgeCount >= 9 && edgeCount % 3 == 0)
            return MaterializePrism(nodes, bodyFields, edgeCount / 3, out bodyTag);

        return ParasolidConstants.PK_ERROR_bad_file_format;
    }

    private static int MaterializeCone(XtNode[] nodes, XtNode cone, int edgeCount, out EntityTag bodyTag)
    {
        bodyTag = 0;
        var fields = ReadConeFields(cone);
        var axis = Axis(fields.Point, fields.Axis, fields.XAxis);
        var semiAngle = Math.Atan2(fields.SinHalfAngle, fields.CosHalfAngle);
        var height = 0.0;
        foreach (var plane in nodes.Where(node => node.Type == (int)XtNodeTypes.Plane))
        {
            var planePoint = plane.Fields[7].Vector;
            var projected = Math.Abs(Dot(Subtract(planePoint, fields.Point), fields.Axis));
            if (projected > height)
                height = projected;
        }

        if (height <= 0)
            return ParasolidConstants.PK_ERROR_corrupt_file;
        var localTag = 0;
        var error = KernelRuntime.BodyCreateSolidCone(fields.Radius, height, semiAngle, &axis, &localTag);
        bodyTag = localTag;
        return error;
    }

    private static int MaterializeSphere(XtNode sphere, out EntityTag bodyTag)
    {
        bodyTag = 0;
        var fields = ReadSphereFields(sphere);
        var axis = Axis(fields.Centre, fields.Axis, fields.XAxis);
        var localTag = 0;
        var error = KernelRuntime.BodyCreateSolidSphere(fields.Radius, &axis, &localTag);
        bodyTag = localTag;
        return error;
    }

    private static int MaterializeTorus(XtNode torus, out EntityTag bodyTag)
    {
        bodyTag = 0;
        var fields = ReadTorusFields(torus);
        var axis = Axis(fields.Centre, fields.Axis, fields.XAxis);
        var localTag = 0;
        var error = KernelRuntime.BodyCreateSolidTorus(fields.MajorRadius, fields.MinorRadius, &axis, &localTag);
        bodyTag = localTag;
        return error;
    }

    private static int MaterializePrism(XtNode[] nodes, BodyNodeFields bodyFields, int sideCount, out EntityTag bodyTag)
    {
        bodyTag = 0;
        if (sideCount < 3)
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var vertices = new List<XtVector>();
        var index = bodyFields.Vertex;
        var guard = 0;
        while (index != 0 && guard++ <= nodes.Length)
        {
            var vertex = FindNode(nodes, index, (int)XtNodeTypes.Vertex);
            if (vertex is null)
                return ParasolidConstants.PK_ERROR_corrupt_file;
            var pointIndex = vertex.Fields[5].Pointer;
            var point = FindNode(nodes, pointIndex, (int)XtNodeTypes.Point);
            if (point is not null)
                vertices.Add(point.Fields[5].Vector);
            index = ReadVertexFields(vertex).Next;
            if (index == bodyFields.Vertex)
                break;
        }

        if (vertices.Count == 0)
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var minZ = vertices.Min(static value => value.Z);
        var maxZ = vertices.Max(static value => value.Z);
        var height = maxZ - minZ;
        var radius = Math.Sqrt(vertices[0].X * vertices[0].X + vertices[0].Y * vertices[0].Y);
        if (height <= 0 || radius <= 0)
            return ParasolidConstants.PK_ERROR_corrupt_file;

        var localTag = 0;
        var error = KernelRuntime.BodyCreateSolidPrism(radius, height, sideCount, null, &localTag);
        bodyTag = localTag;
        return error;
    }

    private static PK_AXIS2_sf_s Axis(XtVector point, XtVector axisVector, XtVector xAxisVector)
    {
        var axis = new PK_AXIS2_sf_s();
        axis.location.coord[0] = point.X;
        axis.location.coord[1] = point.Y;
        axis.location.coord[2] = point.Z;
        axis.axis.coord[0] = axisVector.X;
        axis.axis.coord[1] = axisVector.Y;
        axis.axis.coord[2] = axisVector.Z;
        axis.ref_direction.coord[0] = xAxisVector.X;
        axis.ref_direction.coord[1] = xAxisVector.Y;
        axis.ref_direction.coord[2] = xAxisVector.Z;
        return axis;
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

    private static ConeNodeFields ReadConeFields(XtNode node) => new(
        Point: node.Fields[7].Vector,
        Axis: node.Fields[8].Vector,
        Radius: node.Fields[9].Real,
        SinHalfAngle: node.Fields[10].Real,
        CosHalfAngle: node.Fields[11].Real,
        XAxis: node.Fields[12].Vector);

    private static SphereNodeFields ReadSphereFields(XtNode node) => new(
        Centre: node.Fields[7].Vector,
        Radius: node.Fields[8].Real,
        Axis: node.Fields[9].Vector,
        XAxis: node.Fields[10].Vector);

    private static TorusNodeFields ReadTorusFields(XtNode node) => new(
        Centre: node.Fields[7].Vector,
        Axis: node.Fields[8].Vector,
        MajorRadius: node.Fields[9].Real,
        MinorRadius: node.Fields[10].Real,
        XAxis: node.Fields[11].Vector);

    private readonly record struct BodyNodeFields(XtNodeIndex Shell, XtNodeIndex Region, XtNodeIndex Edge, XtNodeIndex Vertex);
    private readonly record struct FaceNodeFields(XtNodeIndex Next, XtNodeIndex Loop, XtNodeIndex Shell, XtNodeIndex Surface, XtNodeIndex FrontShell);
    private readonly record struct EdgeNodeFields(XtNodeIndex Halfedge, XtNodeIndex Next, XtNodeIndex Curve);
    private readonly record struct VertexNodeFields(XtNodeIndex Next);
    private readonly record struct CylinderNodeFields(XtVector Point, XtVector Axis, double Radius, XtVector XAxis);
    private readonly record struct CircleNodeFields(XtVector Centre, XtVector Normal, double Radius, XtVector XAxis);
    private readonly record struct ConeNodeFields(XtVector Point, XtVector Axis, double Radius, double SinHalfAngle, double CosHalfAngle, XtVector XAxis);
    private readonly record struct SphereNodeFields(XtVector Centre, double Radius, XtVector Axis, XtVector XAxis);
    private readonly record struct TorusNodeFields(XtVector Centre, XtVector Axis, double MajorRadius, double MinorRadius, XtVector XAxis);
}
