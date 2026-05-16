using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe class XtWriter
{
    private const double DefaultResolutionSize = 1000.0;
    private const double DefaultLinearResolution = 1e-8;

    public static int WriteText(IReadOnlyList<EntityTag> parts, out string text)
    {
        text = "";
        var nodes = new List<XtNode>(128);

        for (var i = 0; i < parts.Count; i++)
        {
            if (!KernelRuntime.TryResolveBodySlot(parts[i], out var bodySlot))
                return ParasolidConstants.PK_ERROR_unsuitable_entity;

            if (!BuildBody(bodySlot, nodes))
                return ParasolidConstants.PK_ERROR_bad_field_conversion;
        }

        text = XtText.Encode(nodes);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static bool BuildBody(BodySlot bodySlot, List<XtNode> nodes)
    {
        var map = new NodeMap();
        var body = KernelRuntime.GetBodyRecord(bodySlot);
        map.Body = AddIndex(nodes, ref map);

        for (var regionSlot = body.FirstRegion; regionSlot >= 0; regionSlot = KernelRuntime.GetRegionRecord(regionSlot).NextInBody)
            map.RegionSlots.Add(regionSlot, AddIndex(nodes, ref map));
        for (var shellSlot = body.FirstShell; shellSlot >= 0; shellSlot = KernelRuntime.GetShellRecord(shellSlot).NextInBody)
            map.ShellSlots.Add(shellSlot, AddIndex(nodes, ref map));
        for (var faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
            map.FaceSlots.Add(faceSlot, AddIndex(nodes, ref map));
        for (var faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
        {
            for (var loopSlot = KernelRuntime.GetFaceRecord(faceSlot).FirstLoop; loopSlot >= 0; loopSlot = KernelRuntime.GetLoopRecord(loopSlot).NextInFace)
            {
                map.LoopSlots.Add(loopSlot, AddIndex(nodes, ref map));
                for (var finSlot = KernelRuntime.GetLoopRecord(loopSlot).FirstFin; finSlot >= 0; finSlot = KernelRuntime.GetFinRecord(finSlot).NextInLoop)
                    map.FinSlots.Add(finSlot, AddIndex(nodes, ref map));
            }
        }
        for (var edgeSlot = body.FirstEdgeBody; edgeSlot >= 0; edgeSlot = KernelRuntime.GetEdgeRecord(edgeSlot).NextInBody)
            map.EdgeSlots.Add(edgeSlot, AddIndex(nodes, ref map));
        for (var vertexSlot = body.FirstVertexBody; vertexSlot >= 0; vertexSlot = KernelRuntime.GetVertexRecord(vertexSlot).NextInBody)
            map.VertexSlots.Add(vertexSlot, AddIndex(nodes, ref map));

        AddGeometryIndexes(bodySlot, ref map, nodes);
        AssignPersistentNodeIds(ref map);

        var highest = map.PersistentNodeIdCount;
        SetNode(nodes, map, map.Body, BodyNode(map.Body, highest, body, map));

        for (var regionSlot = body.FirstRegion; regionSlot >= 0; regionSlot = KernelRuntime.GetRegionRecord(regionSlot).NextInBody)
            SetNode(nodes, map, map.RegionSlots[regionSlot], RegionNode(regionSlot, map));
        for (var shellSlot = body.FirstShell; shellSlot >= 0; shellSlot = KernelRuntime.GetShellRecord(shellSlot).NextInBody)
            SetNode(nodes, map, map.ShellSlots[shellSlot], ShellNode(shellSlot, map));
        for (var faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
            SetNode(nodes, map, map.FaceSlots[faceSlot], FaceNode(faceSlot, map));
        for (var faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
        {
            for (var loopSlot = KernelRuntime.GetFaceRecord(faceSlot).FirstLoop; loopSlot >= 0; loopSlot = KernelRuntime.GetLoopRecord(loopSlot).NextInFace)
            {
                SetNode(nodes, map, map.LoopSlots[loopSlot], LoopNode(loopSlot, map));
                for (var finSlot = KernelRuntime.GetLoopRecord(loopSlot).FirstFin; finSlot >= 0; finSlot = KernelRuntime.GetFinRecord(finSlot).NextInLoop)
                    SetNode(nodes, map, map.FinSlots[finSlot], HalfedgeNode(finSlot, map));
            }
        }
        for (var edgeSlot = body.FirstEdgeBody; edgeSlot >= 0; edgeSlot = KernelRuntime.GetEdgeRecord(edgeSlot).NextInBody)
            SetNode(nodes, map, map.EdgeSlots[edgeSlot], EdgeNode(edgeSlot, map));
        for (var vertexSlot = body.FirstVertexBody; vertexSlot >= 0; vertexSlot = KernelRuntime.GetVertexRecord(vertexSlot).NextInBody)
            SetNode(nodes, map, map.VertexSlots[vertexSlot], VertexNode(vertexSlot, map));

        WriteGeometryNodes(ref map, nodes);
        return true;
    }

    private static XtNodeIndex AddIndex(List<XtNode> nodes, ref NodeMap map)
    {
        var index = map.NextNodeIndex++;
        if (index == 2)
            index = map.NextNodeIndex++;
        map.NodePositions.Add(index, nodes.Count);
        nodes.Add(new XtNode { Index = index });
        return index;
    }

    private static void SetNode(List<XtNode> nodes, NodeMap map, XtNodeIndex index, XtNode node)
    {
        nodes[map.NodePositions[index]] = node;
    }

    private static void AddGeometryIndexes(BodySlot bodySlot, ref NodeMap map, List<XtNode> nodes)
    {
        var body = KernelRuntime.GetBodyRecord(bodySlot);
        for (var faceSlot = body.FirstFaceBody; faceSlot >= 0; faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
        {
            var surfTag = KernelRuntime.GetFaceRecord(faceSlot).SurfTag;
            if (surfTag > 0 && !map.SurfaceTags.ContainsKey(surfTag))
                map.SurfaceTags.Add(surfTag, AddIndex(nodes, ref map));
        }
        for (var edgeSlot = body.FirstEdgeBody; edgeSlot >= 0; edgeSlot = KernelRuntime.GetEdgeRecord(edgeSlot).NextInBody)
        {
            var curveTag = KernelRuntime.GetEdgeRecord(edgeSlot).CurveTag;
            if (curveTag > 0 && !map.CurveTags.ContainsKey(curveTag))
                map.CurveTags.Add(curveTag, AddIndex(nodes, ref map));
        }
        for (var vertexSlot = body.FirstVertexBody; vertexSlot >= 0; vertexSlot = KernelRuntime.GetVertexRecord(vertexSlot).NextInBody)
        {
            var pointTag = KernelRuntime.GetVertexRecord(vertexSlot).PointTag;
            if (pointTag > 0 && !map.PointTags.ContainsKey(pointTag))
                map.PointTags.Add(pointTag, AddIndex(nodes, ref map));
        }
    }

    private static void WriteGeometryNodes(ref NodeMap map, List<XtNode> nodes)
    {
        foreach (var pair in map.SurfaceTags)
        {
            var surface = KernelRuntime.GetSurfaceByTag(pair.Key);
            var node = surface.Class switch
            {
                SurfaceClass.Plane => PlaneNode(pair.Value, pair.Key, surface, map),
                SurfaceClass.Cylinder => CylinderNode(pair.Value, pair.Key, surface, map),
                _ => throw new NotSupportedException("Unsupported surface class for XT writer."),
            };
            SetNode(nodes, map, pair.Value, node);
        }

        foreach (var pair in map.CurveTags)
        {
            var curve = KernelRuntime.GetCurveByTag(pair.Key);
            var node = curve.Class switch
            {
                CurveClass.Line => LineNode(pair.Value, pair.Key, curve, map),
                CurveClass.Circle => CircleNode(pair.Value, pair.Key, curve, map),
                _ => throw new NotSupportedException("Unsupported curve class for XT writer."),
            };
            SetNode(nodes, map, pair.Value, node);
        }

        foreach (var pair in map.PointTags)
            SetNode(nodes, map, pair.Value, PointNode(pair.Value, pair.Key, map));
    }

    private static void AssignPersistentNodeIds(ref NodeMap map)
    {
        var next = 1;
        map.PersistentNodeIds[map.Body] = next++;
        AssignIds(map.ShellSlots, ref next, ref map);
        AssignIds(map.SurfaceTags, ref next, ref map);
        AssignIds(map.CurveTags, ref next, ref map);
        AssignIds(map.PointTags, ref next, ref map);
        AssignIds(map.RegionSlots, ref next, ref map);
        AssignIds(map.EdgeSlots, ref next, ref map);
        AssignIds(map.FinSlots, ref next, ref map);
        AssignIds(map.LoopSlots, ref next, ref map);
        AssignIds(map.FaceSlots, ref next, ref map);
        AssignIds(map.VertexSlots, ref next, ref map);
        map.PersistentNodeIdCount = next - 1;
    }

    private static void AssignIds(Dictionary<int, XtNodeIndex> indexes, ref int next, ref NodeMap map)
    {
        foreach (var pair in indexes)
            map.PersistentNodeIds[pair.Value] = next++;
    }

    private static int NodeId(XtNodeIndex index, NodeMap map)
    {
        return map.PersistentNodeIds.TryGetValue(index, out var value) ? value : index;
    }

    private static XtNode BodyNode(XtNodeIndex index, int highest, BodyRecord body, NodeMap map)
    {
        return new XtNode
        {
            Type = (int)XtNodeTypes.Body,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(highest),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.RealValue(DefaultResolutionSize),
                XtFieldValue.RealValue(DefaultLinearResolution),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Unsigned(1),
                XtFieldValue.Ptr(0),
                XtFieldValue.Unsigned(XtBodyType(body.BodyType)),
                XtFieldValue.Unsigned(1),
                XtFieldValue.Ptr(FirstSolidShell(map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(First(map.SurfaceTags)),
                XtFieldValue.Ptr(First(map.CurveTags)),
                XtFieldValue.Ptr(First(map.PointTags)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(Ptr(map.RegionSlots, body.FirstRegion)),
                XtFieldValue.Ptr(Ptr(map.EdgeSlots, body.FirstEdgeBody)),
                XtFieldValue.Ptr(Ptr(map.VertexSlots, body.FirstVertexBody)),
                XtFieldValue.Int(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Int(0),
                XtFieldValue.Ptr(0),
            ],
        };
    }

    private static XtNode RegionNode(RegionSlot slot, NodeMap map)
    {
        var region = KernelRuntime.GetRegionRecord(slot);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Region,
            Index = map.RegionSlots[slot],
            Fields =
            [
                XtFieldValue.Int(NodeId(map.RegionSlots[slot], map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(map.Body),
                XtFieldValue.Ptr(Ptr(map.RegionSlots, region.NextInBody)),
                XtFieldValue.Ptr(PreviousRegion(slot, map)),
                XtFieldValue.Ptr(Ptr(map.ShellSlots, region.FirstShell)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Char(region.IsSolid != 0 ? 'S' : 'V'),
                XtFieldValue.Ptr(0),
            ],
        };
    }

    private static XtNode ShellNode(ShellSlot slot, NodeMap map)
    {
        var shell = KernelRuntime.GetShellRecord(slot);
        var region = KernelRuntime.GetRegionRecord(shell.Region);
        var firstBack = 0;
        var firstFront = 0;
        for (var useSlot = shell.FirstFaceUseShell; useSlot >= 0; useSlot = KernelRuntime.GetFaceUseRecord(useSlot).NextInShell)
        {
            var use = KernelRuntime.GetFaceUseRecord(useSlot);
            if (use.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c && firstBack == 0)
                firstBack = Ptr(map.FaceSlots, use.Face);
            if (use.Sense != ParasolidConstants.PK_TOPOL_sense_negative_c && firstFront == 0)
                firstFront = Ptr(map.FaceSlots, use.Face);
        }

        return new XtNode
        {
            Type = (int)XtNodeTypes.Shell,
            Index = map.ShellSlots[slot],
            Fields =
            [
                XtFieldValue.Int(NodeId(map.ShellSlots[slot], map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(region.IsSolid != 0 ? map.Body : 0),
                XtFieldValue.Ptr(NextSolidShell(slot, map)),
                XtFieldValue.Ptr(firstBack),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(Ptr(map.RegionSlots, shell.Region)),
                XtFieldValue.Ptr(firstFront),
            ],
        };
    }

    private static XtNode FaceNode(FaceSlot slot, NodeMap map)
    {
        var face = KernelRuntime.GetFaceRecord(slot);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Face,
            Index = map.FaceSlots[slot],
            Fields =
            [
                XtFieldValue.Int(NodeId(map.FaceSlots[slot], map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Null(),
                XtFieldValue.Ptr(Ptr(map.FaceSlots, face.NextInBody)),
                XtFieldValue.Ptr(PreviousFace(slot, map)),
                XtFieldValue.Ptr(Ptr(map.LoopSlots, face.FirstLoop)),
                XtFieldValue.Ptr(Ptr(map.ShellSlots, face.BackShell)),
                XtFieldValue.Ptr(face.SurfTag > 0 ? map.SurfaceTags[face.SurfTag] : 0),
                XtFieldValue.Char(face.Orientation == ParasolidConstants.PK_TOPOL_sense_negative_c ? '-' : '+'),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(Ptr(map.FaceSlots, face.NextInBody)),
                XtFieldValue.Ptr(PreviousFace(slot, map)),
                XtFieldValue.Ptr(Ptr(map.ShellSlots, face.FrontShell)),
            ],
        };
    }

    private static XtNode LoopNode(LoopSlot slot, NodeMap map)
    {
        var loop = KernelRuntime.GetLoopRecord(slot);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Loop,
            Index = map.LoopSlots[slot],
            Fields =
            [
                XtFieldValue.Int(NodeId(map.LoopSlots[slot], map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(Ptr(map.FinSlots, loop.FirstFin)),
                XtFieldValue.Ptr(Ptr(map.FaceSlots, loop.Face)),
                XtFieldValue.Ptr(Ptr(map.LoopSlots, loop.NextInFace)),
            ],
        };
    }

    private static XtNode EdgeNode(EdgeSlot slot, NodeMap map)
    {
        var edge = KernelRuntime.GetEdgeRecord(slot);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Edge,
            Index = map.EdgeSlots[slot],
            Fields =
            [
                XtFieldValue.Int(NodeId(map.EdgeSlots[slot], map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Null(),
                XtFieldValue.Ptr(Ptr(map.FinSlots, edge.FirstFinEdge)),
                XtFieldValue.Ptr(PreviousEdge(slot, map)),
                XtFieldValue.Ptr(Ptr(map.EdgeSlots, edge.NextInBody)),
                XtFieldValue.Ptr(edge.CurveTag > 0 ? map.CurveTags[edge.CurveTag] : 0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(map.Body),
            ],
        };
    }

    private static XtNode HalfedgeNode(FinSlot slot, NodeMap map)
    {
        var fin = KernelRuntime.GetFinRecord(slot);
        var edge = KernelRuntime.GetEdgeRecord(fin.Edge);
        var other = OtherFinOnEdge(slot, edge);
        var vertex = EdgeFinVertex(slot, edge);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Halfedge,
            Index = map.FinSlots[slot],
            Fields =
            [
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(Ptr(map.LoopSlots, fin.Loop)),
                XtFieldValue.Ptr(Ptr(map.FinSlots, NextFinInClosedLoop(slot, fin))),
                XtFieldValue.Ptr(Ptr(map.FinSlots, PreviousFinInClosedLoop(slot, fin))),
                XtFieldValue.Ptr(Ptr(map.VertexSlots, vertex)),
                XtFieldValue.Ptr(Ptr(map.FinSlots, other)),
                XtFieldValue.Ptr(Ptr(map.EdgeSlots, fin.Edge)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(NextAtVertex(slot, vertex, map)),
                XtFieldValue.Char(FinSense(slot, edge)),
            ],
        };
    }

    private static XtNode VertexNode(VertexSlot slot, NodeMap map)
    {
        var vertex = KernelRuntime.GetVertexRecord(slot);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Vertex,
            Index = map.VertexSlots[slot],
            Fields =
            [
                XtFieldValue.Int(NodeId(map.VertexSlots[slot], map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(FirstFinAtVertex(slot, map)),
                XtFieldValue.Ptr(PreviousVertex(slot, map)),
                XtFieldValue.Ptr(Ptr(map.VertexSlots, vertex.NextInBody)),
                XtFieldValue.Ptr(vertex.PointTag > 0 ? map.PointTags[vertex.PointTag] : 0),
                XtFieldValue.Null(),
                XtFieldValue.Ptr(map.Body),
            ],
        };
    }

    private static XtNode PlaneNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        var data = KernelRuntime.GetPlaneData(surface.DataIndex);
        var owner = FindSurfaceOwner(tag, map);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Plane,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(owner),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.NormalX, data.NormalY, data.NormalZ),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
            ],
        };
    }

    private static XtNode CylinderNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        var data = KernelRuntime.GetCylinderData(surface.DataIndex);
        var owner = FindSurfaceOwner(tag, map);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Cylinder,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(owner),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.RealValue(data.Radius),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
            ],
        };
    }

    private static XtNode LineNode(XtNodeIndex index, CurveTag tag, CurveRecord curve, NodeMap map)
    {
        var data = KernelRuntime.GetLineData(curve.DataIndex);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Line,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(FindCurveOwner(tag, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
            ],
        };
    }

    private static XtNode CircleNode(XtNodeIndex index, CurveTag tag, CurveRecord curve, NodeMap map)
    {
        var data = KernelRuntime.GetCircleData(curve.DataIndex);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Circle,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(FindCurveOwner(tag, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.CenterX, data.CenterY, data.CenterZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
                XtFieldValue.RealValue(data.Radius),
            ],
        };
    }

    private static XtNode PointNode(XtNodeIndex index, PointTag tag, NodeMap map)
    {
        var data = KernelRuntime.GetPointByTag(tag);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Point,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(FindPointOwner(tag, map)),
                XtFieldValue.Ptr(NextPoint(tag, map)),
                XtFieldValue.Ptr(PreviousPoint(tag, map)),
                XtFieldValue.Vec(data.Position.X, data.Position.Y, data.Position.Z),
            ],
        };
    }

    private static XtNodeIndex Ptr(Dictionary<int, XtNodeIndex> map, int slotOrTag)
    {
        return slotOrTag >= 0 && map.TryGetValue(slotOrTag, out var value) ? value : 0;
    }

    private static XtNodeIndex FirstSolidShell(NodeMap map)
    {
        foreach (var pair in map.ShellSlots)
        {
            var shell = KernelRuntime.GetShellRecord(pair.Key);
            var region = KernelRuntime.GetRegionRecord(shell.Region);
            if (region.IsSolid != 0)
                return pair.Value;
        }

        return 0;
    }

    private static XtNodeIndex NextSolidShell(ShellSlot slot, NodeMap map)
    {
        var found = false;
        foreach (var pair in map.ShellSlots)
        {
            var shell = KernelRuntime.GetShellRecord(pair.Key);
            var region = KernelRuntime.GetRegionRecord(shell.Region);
            if (region.IsSolid == 0)
                continue;
            if (found)
                return pair.Value;
            if (pair.Key == slot)
                found = true;
        }

        return 0;
    }

    private static XtNodeIndex PreviousRegion(RegionSlot slot, NodeMap map) => PreviousDictionaryValue(map.RegionSlots, slot);
    private static XtNodeIndex PreviousFace(FaceSlot slot, NodeMap map) => PreviousDictionaryValue(map.FaceSlots, slot);
    private static XtNodeIndex PreviousEdge(EdgeSlot slot, NodeMap map) => PreviousDictionaryValue(map.EdgeSlots, slot);
    private static XtNodeIndex PreviousVertex(VertexSlot slot, NodeMap map) => PreviousDictionaryValue(map.VertexSlots, slot);

    private static FinSlot OtherFinOnEdge(FinSlot slot, EdgeRecord edge)
    {
        for (var finSlot = edge.FirstFinEdge; finSlot >= 0; finSlot = KernelRuntime.GetFinRecord(finSlot).NextOfEdge)
        {
            if (finSlot != slot)
                return finSlot;
        }

        return -1;
    }

    private static FinSlot NextFinInClosedLoop(FinSlot slot, FinRecord fin)
    {
        return fin.NextInLoop >= 0 ? fin.NextInLoop : KernelRuntime.GetLoopRecord(fin.Loop).FirstFin;
    }

    private static FinSlot PreviousFinInClosedLoop(FinSlot slot, FinRecord fin)
    {
        if (fin.PrevInLoop >= 0)
            return fin.PrevInLoop;

        var last = slot;
        for (var finSlot = KernelRuntime.GetLoopRecord(fin.Loop).FirstFin; finSlot >= 0; finSlot = KernelRuntime.GetFinRecord(finSlot).NextInLoop)
            last = finSlot;
        return last;
    }

    private static VertexSlot EdgeFinVertex(FinSlot slot, EdgeRecord edge)
    {
        if (edge.StartVertex < 0 || edge.EndVertex < 0)
            return -1;

        return FinSense(slot, edge) == '+' ? edge.StartVertex : edge.EndVertex;
    }

    private static char FinSense(FinSlot slot, EdgeRecord edge)
    {
        return slot == edge.FirstFinEdge ? '+' : '-';
    }

    private static XtNodeIndex FirstFinAtVertex(VertexSlot vertex, NodeMap map)
    {
        foreach (var pair in map.FinSlots)
        {
            var fin = KernelRuntime.GetFinRecord(pair.Key);
            var edge = KernelRuntime.GetEdgeRecord(fin.Edge);
            if (EdgeFinVertex(pair.Key, edge) == vertex)
                return pair.Value;
        }

        return 0;
    }

    private static XtNodeIndex NextAtVertex(FinSlot slot, VertexSlot vertex, NodeMap map)
    {
        if (vertex < 0)
            return 0;

        var found = false;
        foreach (var pair in map.FinSlots)
        {
            var fin = KernelRuntime.GetFinRecord(pair.Key);
            var edge = KernelRuntime.GetEdgeRecord(fin.Edge);
            if (EdgeFinVertex(pair.Key, edge) != vertex)
                continue;
            if (found)
                return pair.Value;
            if (pair.Key == slot)
                found = true;
        }

        return 0;
    }

    private static XtNodeIndex FindSurfaceOwner(SurfTag tag, NodeMap map)
    {
        foreach (var pair in map.FaceSlots)
        {
            if (KernelRuntime.GetFaceRecord(pair.Key).SurfTag == tag)
                return pair.Value;
        }

        return 0;
    }

    private static XtNodeIndex FindCurveOwner(CurveTag tag, NodeMap map)
    {
        foreach (var pair in map.EdgeSlots)
        {
            if (KernelRuntime.GetEdgeRecord(pair.Key).CurveTag == tag)
                return pair.Value;
        }

        return 0;
    }

    private static XtNodeIndex FindPointOwner(PointTag tag, NodeMap map)
    {
        foreach (var pair in map.VertexSlots)
        {
            if (KernelRuntime.GetVertexRecord(pair.Key).PointTag == tag)
                return pair.Value;
        }

        return 0;
    }

    private static XtNodeIndex NextSurface(SurfTag tag, NodeMap map) => NextDictionaryValue(map.SurfaceTags, tag);
    private static XtNodeIndex PreviousSurface(SurfTag tag, NodeMap map) => PreviousDictionaryValue(map.SurfaceTags, tag);
    private static XtNodeIndex NextCurve(CurveTag tag, NodeMap map) => NextDictionaryValue(map.CurveTags, tag);
    private static XtNodeIndex PreviousCurve(CurveTag tag, NodeMap map) => PreviousDictionaryValue(map.CurveTags, tag);
    private static XtNodeIndex NextPoint(PointTag tag, NodeMap map) => NextDictionaryValue(map.PointTags, tag);
    private static XtNodeIndex PreviousPoint(PointTag tag, NodeMap map) => PreviousDictionaryValue(map.PointTags, tag);

    private static XtNodeIndex NextDictionaryValue(Dictionary<int, XtNodeIndex> map, int key)
    {
        var found = false;
        foreach (var pair in map)
        {
            if (found)
                return pair.Value;
            if (pair.Key == key)
                found = true;
        }

        return 0;
    }

    private static XtNodeIndex PreviousDictionaryValue(Dictionary<int, XtNodeIndex> map, int key)
    {
        XtNodeIndex previous = 0;
        foreach (var pair in map)
        {
            if (pair.Key == key)
                return previous;
            previous = pair.Value;
        }

        return 0;
    }

    private static XtNodeIndex First(Dictionary<int, XtNodeIndex> map)
    {
        foreach (var pair in map)
            return pair.Value;
        return 0;
    }

    private static int XtBodyType(KernelBodyType bodyType)
    {
        return bodyType == ParasolidConstants.PK_BODY_type_solid_c ? 1 : bodyType;
    }

    private struct NodeMap
    {
        public XtNodeIndex Body;
        public XtNodeIndex NextNodeIndex;
        public int PersistentNodeIdCount;
        public Dictionary<XtNodeIndex, int> NodePositions;
        public Dictionary<XtNodeIndex, int> PersistentNodeIds;
        public Dictionary<int, XtNodeIndex> RegionSlots;
        public Dictionary<int, XtNodeIndex> ShellSlots;
        public Dictionary<int, XtNodeIndex> FaceSlots;
        public Dictionary<int, XtNodeIndex> LoopSlots;
        public Dictionary<int, XtNodeIndex> FinSlots;
        public Dictionary<int, XtNodeIndex> EdgeSlots;
        public Dictionary<int, XtNodeIndex> VertexSlots;
        public Dictionary<int, XtNodeIndex> SurfaceTags;
        public Dictionary<int, XtNodeIndex> CurveTags;
        public Dictionary<int, XtNodeIndex> PointTags;

        public NodeMap()
        {
            Body = 0;
            NextNodeIndex = 1;
            PersistentNodeIdCount = 0;
            NodePositions = new Dictionary<XtNodeIndex, int>();
            PersistentNodeIds = new Dictionary<XtNodeIndex, int>();
            RegionSlots = new Dictionary<int, XtNodeIndex>();
            ShellSlots = new Dictionary<int, XtNodeIndex>();
            FaceSlots = new Dictionary<int, XtNodeIndex>();
            LoopSlots = new Dictionary<int, XtNodeIndex>();
            FinSlots = new Dictionary<int, XtNodeIndex>();
            EdgeSlots = new Dictionary<int, XtNodeIndex>();
            VertexSlots = new Dictionary<int, XtNodeIndex>();
            SurfaceTags = new Dictionary<int, XtNodeIndex>();
            CurveTags = new Dictionary<int, XtNodeIndex>();
            PointTags = new Dictionary<int, XtNodeIndex>();
        }
    }
}
