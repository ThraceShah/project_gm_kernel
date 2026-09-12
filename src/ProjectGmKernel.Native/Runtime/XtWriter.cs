using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe class XtWriter
{
    private const double DefaultResolutionSize = 1000.0;
    private const double DefaultLinearResolution = 1e-8;

    public static int WriteText(IReadOnlyList<EntityTag> parts, int transmitVersion, out string text)
    {
        text = "";
        var nodes = new List<XtNode>(128);
        var graph = new TransmitGraph();
        var bodyIndexes = parts.Count > 1 ? new XtNodeIndex[parts.Count] : [];
        if (parts.Count > 1)
        {
            nodes.Add(PartTransmitBlockNode(1, bodyIndexes));
            graph.NextNodeIndex = 2;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (!KernelRuntime.TryResolveBodySlot(parts[i], out var bodySlot))
                return ParasolidConstants.PK_ERROR_unsuitable_entity;

            if (!BuildBody(bodySlot, nodes, ref graph, out var bodyIndex))
                return ParasolidConstants.PK_ERROR_bad_field_conversion;
            if (parts.Count > 1)
                bodyIndexes[i] = bodyIndex;
        }

        if (parts.Count > 1)
            nodes[0] = PartTransmitBlockNode(1, bodyIndexes);

        try
        {
            text = XtText.EncodeCurrent(nodes, transmitVersion);
        }
        catch (NotSupportedException)
        {
            return ParasolidConstants.PK_ERROR_wrong_version;
        }
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    private static bool BuildBody(BodySlot bodySlot, List<XtNode> nodes, ref TransmitGraph graph, out XtNodeIndex bodyIndex)
    {
        var map = new NodeMap();
        var body = KernelRuntime.GetBodyRecord(bodySlot);
        map.Body = AddIndex(nodes, ref graph, ref map);
        bodyIndex = map.Body;
        map.FirstRegionSlot = body.FirstRegion;
        map.FirstFaceSlot = body.FirstFaceBody;
        map.FirstEdgeSlot = body.FirstEdgeBody;
        map.FirstVertexSlot = body.FirstVertexBody;

        var regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = KernelRuntime.GetRegionRecord(regionSlot).NextInBody)
            map.RegionSlots.Add(regionSlot, AddIndex(nodes, ref graph, ref map));
        var shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = KernelRuntime.GetShellRecord(shellSlot).NextInBody)
            map.ShellSlots.Add(shellSlot, AddIndex(nodes, ref graph, ref map));
        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
            map.FaceSlots.Add(faceSlot, AddIndex(nodes, ref graph, ref map));
        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
        {
            var face = KernelRuntime.GetFaceRecord(faceSlot);
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = KernelRuntime.GetLoopRecord(loopSlot).NextInFace)
            {
                map.LoopSlots.Add(loopSlot, AddIndex(nodes, ref graph, ref map));
                var loop = KernelRuntime.GetLoopRecord(loopSlot);
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = KernelRuntime.GetFinRecord(finSlot).NextInLoop)
                    map.FinSlots.Add(finSlot, AddIndex(nodes, ref graph, ref map));
            }
        }
        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = KernelRuntime.GetEdgeRecord(edgeSlot).NextInBody)
            map.EdgeSlots.Add(edgeSlot, AddIndex(nodes, ref graph, ref map));
        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = KernelRuntime.GetVertexRecord(vertexSlot).NextInBody)
            map.VertexSlots.Add(vertexSlot, AddIndex(nodes, ref graph, ref map));

        AddGeometryIndexes(bodySlot, ref graph, ref map, nodes);
        CollectDependentGeometry(ref map, nodes, ref graph);
        BuildGeometryChains(bodySlot, ref map);
        AssignPersistentNodeIds(ref map);

        var highest = map.PersistentNodeIdCount;
        SetNode(nodes, map, map.Body, BodyNode(map.Body, highest, body, map));

        regionSlot = body.FirstRegion;
        for (var i = 0; i < body.RegionCount; i++, regionSlot = KernelRuntime.GetRegionRecord(regionSlot).NextInBody)
            SetNode(nodes, map, map.RegionSlots[regionSlot], RegionNode(regionSlot, map));
        shellSlot = body.FirstShell;
        for (var i = 0; i < body.ShellCount; i++, shellSlot = KernelRuntime.GetShellRecord(shellSlot).NextInBody)
            SetNode(nodes, map, map.ShellSlots[shellSlot], ShellNode(shellSlot, map));
        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
            SetNode(nodes, map, map.FaceSlots[faceSlot], FaceNode(faceSlot, map));
        faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
        {
            var face = KernelRuntime.GetFaceRecord(faceSlot);
            var loopSlot = face.FirstLoop;
            for (var j = 0; j < face.LoopCount; j++, loopSlot = KernelRuntime.GetLoopRecord(loopSlot).NextInFace)
            {
                SetNode(nodes, map, map.LoopSlots[loopSlot], LoopNode(loopSlot, map));
                var loop = KernelRuntime.GetLoopRecord(loopSlot);
                var finSlot = loop.FirstFin;
                for (var k = 0; k < loop.FinCount; k++, finSlot = KernelRuntime.GetFinRecord(finSlot).NextInLoop)
                    SetNode(nodes, map, map.FinSlots[finSlot], HalfedgeNode(finSlot, map));
            }
        }
        edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = KernelRuntime.GetEdgeRecord(edgeSlot).NextInBody)
            SetNode(nodes, map, map.EdgeSlots[edgeSlot], EdgeNode(edgeSlot, map));
        vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = KernelRuntime.GetVertexRecord(vertexSlot).NextInBody)
            SetNode(nodes, map, map.VertexSlots[vertexSlot], VertexNode(vertexSlot, map));

        WriteGeometryNodes(ref map, nodes, ref graph);
        WriteGeometricOwnerNodes(ref map, nodes);
        return true;
    }

    private static XtNode PartTransmitBlockNode(XtNodeIndex index, XtNodeIndex[] bodyIndexes)
    {
        var fields = new XtFieldValue[5 + bodyIndexes.Length];
        fields[0] = XtFieldValue.Int(bodyIndexes.Length);
        fields[1] = XtFieldValue.Int(0);
        fields[2] = XtFieldValue.Ptr(0);
        fields[3] = XtFieldValue.Ptr(0);
        fields[4] = XtFieldValue.Ptr(0);
        for (var i = 0; i < bodyIndexes.Length; i++)
            fields[5 + i] = XtFieldValue.Ptr(bodyIndexes[i]);

        return new XtNode
        {
            Type = (int)XtNodeTypes.PartTransmitBlock,
            Index = index,
            VariableLength = bodyIndexes.Length,
            Fields = fields,
        };
    }

    private static XtNodeIndex AddIndex(List<XtNode> nodes, ref TransmitGraph graph, ref NodeMap map)
    {
        var index = graph.NextNodeIndex++;
        if (index == 2)
            index = graph.NextNodeIndex++;
        map.NodePositions.Add(index, nodes.Count);
        nodes.Add(new XtNode { Index = index });
        return index;
    }

    private static void SetNode(List<XtNode> nodes, NodeMap map, XtNodeIndex index, XtNode node)
    {
        nodes[map.NodePositions[index]] = node;
    }

    private static void AddGeometryIndexes(BodySlot bodySlot, ref TransmitGraph graph, ref NodeMap map, List<XtNode> nodes)
    {
        var body = KernelRuntime.GetBodyRecord(bodySlot);
        var faceSlot = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, faceSlot = KernelRuntime.GetFaceRecord(faceSlot).NextInBody)
        {
            var surfTag = KernelRuntime.GetFaceRecord(faceSlot).SurfTag;
            if (surfTag > 0 && !map.SurfaceTags.ContainsKey(surfTag))
                map.SurfaceTags.Add(surfTag, AddIndex(nodes, ref graph, ref map));
        }
        var edgeSlot = body.FirstEdgeBody;
        for (var i = 0; i < body.EdgeCountBody; i++, edgeSlot = KernelRuntime.GetEdgeRecord(edgeSlot).NextInBody)
        {
            var curveTag = KernelRuntime.GetEdgeRecord(edgeSlot).CurveTag;
            if (curveTag > 0 && !map.CurveTags.ContainsKey(curveTag))
                map.CurveTags.Add(curveTag, AddIndex(nodes, ref graph, ref map));
        }
        var vertexSlot = body.FirstVertexBody;
        for (var i = 0; i < body.VertexCountBody; i++, vertexSlot = KernelRuntime.GetVertexRecord(vertexSlot).NextInBody)
        {
            var pointTag = KernelRuntime.GetVertexRecord(vertexSlot).PointTag;
            if (pointTag > 0 && !map.PointTags.ContainsKey(pointTag))
                map.PointTags.Add(pointTag, AddIndex(nodes, ref graph, ref map));
        }
    }

    private static void WriteGeometryNodes(ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        foreach (var pair in map.SurfaceTags)
        {
            var surface = KernelRuntime.GetSurfaceByTag(pair.Key);
            XtNode node;
            switch (surface.Class)
            {
                case SurfaceClass.Plane:
                    node = PlaneNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.Cylinder:
                    node = CylinderNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.Cone:
                    node = ConeNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.Sphere:
                    node = SphereNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.Torus:
                    node = TorusNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.BSurface:
                    node = BSurfaceNode(pair.Value, pair.Key, surface, ref map, nodes, ref graph);
                    break;
                case SurfaceClass.Swept:
                    node = SweptSurfaceNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.Spun:
                    node = SpunSurfaceNode(pair.Value, pair.Key, surface, map);
                    break;
                case SurfaceClass.Offset:
                    node = OffsetSurfaceNode(pair.Value, pair.Key, surface, map);
                    break;
                default:
                    throw new NotSupportedException("Unsupported surface class for XT writer.");
            }
            SetNode(nodes, map, pair.Value, node);
        }

        foreach (var pair in map.CurveTags)
        {
            var curve = KernelRuntime.GetCurveByTag(pair.Key);
            XtNode node;
            switch (curve.Class)
            {
                case CurveClass.Line:
                    node = LineNode(pair.Value, pair.Key, curve, map);
                    break;
                case CurveClass.Circle:
                    node = CircleNode(pair.Value, pair.Key, curve, map);
                    break;
                case CurveClass.Ellipse:
                    node = EllipseNode(pair.Value, pair.Key, curve, map);
                    break;
                case CurveClass.BCurve:
                    node = BCurveNode(pair.Value, pair.Key, curve, ref map, nodes, ref graph);
                    break;
                case CurveClass.TRCurve:
                    node = TrimmedCurveNode(pair.Value, pair.Key, curve, map);
                    break;
                case CurveClass.SPCurve:
                    node = SpCurveNode(pair.Value, pair.Key, curve, map);
                    break;
                default:
                    throw new NotSupportedException("Unsupported curve class for XT writer.");
            }
            SetNode(nodes, map, pair.Value, node);
        }

        foreach (var pair in map.PointTags)
            SetNode(nodes, map, pair.Value, PointNode(pair.Value, pair.Key, map));
    }

    /// <summary>
    /// Geometry that depends on other geometry (trimmed basis curves, SP curve
    /// supports and 2D B-curves, swept/spun sections, offset bases) must be
    /// transmitted with the body even though no topology owns it. Collect
    /// transitively until the tag maps stabilise.
    /// </summary>
    private static void CollectDependentGeometry(ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var tag in map.CurveTags.Keys.ToArray())
            {
                var curve = KernelRuntime.GetCurveByTag(tag);
                switch (curve.Class)
                {
                    case CurveClass.TRCurve:
                        changed |= AddCurveTag(KernelRuntime.TrCurveDataPool[curve.DataIndex].BasisCurveTag, tag, isCurve: true, ref map, nodes, ref graph);
                        break;
                    case CurveClass.SPCurve:
                    {
                        ref readonly var sp = ref KernelRuntime.SpCurveDataPool[curve.DataIndex];
                        changed |= AddSurfaceTag(sp.SurfTag, tag, isCurve: true, ref map, nodes, ref graph);
                        changed |= AddFloatingCurveTag(sp.BCurveTag, ref map, nodes, ref graph);
                        break;
                    }
                }
            }
            foreach (var tag in map.SurfaceTags.Keys.ToArray())
            {
                var surface = KernelRuntime.GetSurfaceByTag(tag);
                switch (surface.Class)
                {
                    case SurfaceClass.Swept:
                        changed |= AddCurveTag(KernelRuntime.SweptDataPool[surface.DataIndex].SectionCurveTag, tag, isCurve: false, ref map, nodes, ref graph);
                        break;
                    case SurfaceClass.Spun:
                        changed |= AddCurveTag(KernelRuntime.SpunDataPool[surface.DataIndex].ProfileCurveTag, tag, isCurve: false, ref map, nodes, ref graph);
                        break;
                    case SurfaceClass.Offset:
                        changed |= AddSurfaceTag(KernelRuntime.OffsetDataPool[surface.DataIndex].BaseSurfTag, tag, isCurve: false, ref map, nodes, ref graph);
                        break;
                }
            }
        }
    }

    private static bool AddCurveTag(CurveTag tag, int dependentTag, bool isCurve, ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        if (tag <= 0 || map.CurveTags.ContainsKey(tag))
            return false;
        var nodeIndex = AddIndex(nodes, ref graph, ref map);
        map.CurveTags.Add(tag, nodeIndex);
        RecordGeometricOwner(nodeIndex, dependentTag, isCurve, tag, SharedIsCurve: true, ref map, nodes, ref graph);
        return true;
    }

    /// <summary>
    /// An SP curve's 2D B-curve is transmitted as a floating node (no chain,
    /// owner=0, node_id=0), matching Parasolid's canonical form.
    /// </summary>
    private static bool AddFloatingCurveTag(CurveTag tag, ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        if (tag <= 0 || map.CurveTags.ContainsKey(tag))
            return false;
        map.CurveTags.Add(tag, AddIndex(nodes, ref graph, ref map));
        map.FloatingCurves.Add(tag);
        return true;
    }

    private static bool AddSurfaceTag(SurfTag tag, int dependentTag, bool isCurve, ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        if (tag <= 0 || map.SurfaceTags.ContainsKey(tag))
            return false;
        var nodeIndex = AddIndex(nodes, ref graph, ref map);
        map.SurfaceTags.Add(tag, nodeIndex);
        RecordGeometricOwner(nodeIndex, dependentTag, isCurve, tag, SharedIsCurve: false, ref map, nodes, ref graph);
        return true;
    }

    /// <summary>
    /// Ownerless dependency geometry carries a GEOMETRIC_OWNER node (XT node
    /// 141, {owner: dependent, shared_geometry: the dependency}) and points at
    /// it from its own geometric_owner field, matching Parasolid's transmit.
    /// </summary>
    private static void RecordGeometricOwner(XtNodeIndex sharedNode, int dependentTag, bool isCurve, int sharedTag, bool SharedIsCurve,
        ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        var ownerNode = AddIndex(nodes, ref graph, ref map);
        map.GeometricOwnerNodes.Add(sharedTag, ownerNode);
        map.PendingGeometricOwners.Add((ownerNode, dependentTag, isCurve, sharedTag, SharedIsCurve));
    }

    private static void WriteGeometricOwnerNodes(ref NodeMap map, List<XtNode> nodes)
    {
        foreach (var (node, dependentTag, isCurve, sharedTag, sharedIsCurve) in map.PendingGeometricOwners)
        {
            var dependent = isCurve ? map.CurveTags[dependentTag] : map.SurfaceTags[dependentTag];
            var shared = sharedIsCurve ? map.CurveTags[sharedTag] : map.SurfaceTags[sharedTag];
            SetNode(nodes, map, node, new XtNode
            {
                Type = (int)XtNodeTypes.GeometricOwner,
                Index = node,
                Fields =
                [
                    XtFieldValue.Ptr(dependent),
                    XtFieldValue.Ptr(node),
                    XtFieldValue.Ptr(node),
                    XtFieldValue.Ptr(shared),
                ],
            });
        }
    }

    private static XtFieldValue GeometricOwnerField(int tag, NodeMap map)
        => map.GeometricOwnerNodes.TryGetValue(tag, out var owner) ? XtFieldValue.Ptr(owner) : XtFieldValue.Ptr(0);

    /// <summary>
    /// The transmitted curve/surface chain is the kernel's topology-owned ring
    /// (broken at the first edge's curve / first face's surface) with dependent
    /// geometry appended, so dependency nodes stay reachable from the body.
    /// </summary>
    private static void BuildGeometryChains(BodySlot bodySlot, ref NodeMap map)
    {
        map.CurveChain = BuildChain(map.CurveTags, FirstCurveTag(bodySlot, map),
            tag => KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(tag)].NextInBody, map);
        map.SurfaceChain = BuildChain(map.SurfaceTags, FirstSurfaceTag(bodySlot, map),
            tag => KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(tag)].NextInBody, map);
    }

    private static List<int> BuildChain(Dictionary<int, XtNodeIndex> tags, int firstTag, Func<int, int> nextTagOf, NodeMap map)
    {
        var chain = new List<int>(tags.Count);
        if (firstTag > 0 && tags.ContainsKey(firstTag))
        {
            var current = firstTag;
            var guard = 0;
            do
            {
                chain.Add(current);
                current = nextTagOf(current);
                if (guard++ > tags.Count)
                    break;
            }
            while (current > 0 && current != firstTag && tags.ContainsKey(current));
        }
        foreach (var tag in tags.Keys)
        {
            if (!chain.Contains(tag) && !map.FloatingCurves.Contains(tag))
                chain.Add(tag);
        }
        return chain;
    }

    private static int FirstCurveTag(BodySlot bodySlot, NodeMap map)
    {
        var firstEdge = map.FirstEdgeSlot >= 0 ? KernelRuntime.GetEdgeRecord(map.FirstEdgeSlot).CurveTag : 0;
        if (firstEdge > 0)
            return firstEdge;
        foreach (var pair in map.CurveTags)
            return pair.Key;
        return 0;
    }

    private static int FirstSurfaceTag(BodySlot bodySlot, NodeMap map)
    {
        var firstFace = map.FirstFaceSlot >= 0 ? KernelRuntime.GetFaceRecord(map.FirstFaceSlot).SurfTag : 0;
        if (firstFace > 0)
            return firstFace;
        foreach (var pair in map.SurfaceTags)
            return pair.Key;
        return 0;
    }

    private static XtNodeIndex ChainHead(List<int> chain, Dictionary<int, XtNodeIndex> tags)
        => chain.Count > 0 && tags.TryGetValue(chain[0], out var head) ? head : 0;

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
                XtFieldValue.Ptr(ChainHead(map.SurfaceChain, map.SurfaceTags)),
                XtFieldValue.Ptr(ChainHead(map.CurveChain, map.CurveTags)),
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
                XtFieldValue.Ptr(NextRegion(slot, map)),
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
        var useSlot = shell.FirstFaceUseShell;
        for (var i = 0; i < shell.FaceUseCount; i++, useSlot = KernelRuntime.GetFaceUseRecord(useSlot).NextInShell)
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
                XtFieldValue.Ptr(NextShellInRegion(slot, map)),
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
                XtFieldValue.Ptr(FaceRingNext(face.BackFaceUse, back: true, map)),
                XtFieldValue.Ptr(FaceRingPrevious(face.BackFaceUse, back: true, map)),
                XtFieldValue.Ptr(Ptr(map.LoopSlots, face.FirstLoop)),
                XtFieldValue.Ptr(Ptr(map.ShellSlots, face.BackShell)),
                XtFieldValue.Ptr(face.SurfTag > 0 ? map.SurfaceTags[face.SurfTag] : 0),
                XtFieldValue.Char(face.Orientation == ParasolidConstants.PK_TOPOL_sense_negative_c ? '-' : '+'),
                XtFieldValue.Ptr(Ptr(map.FaceSlots, face.NextOnSurf)),
                XtFieldValue.Ptr(Ptr(map.FaceSlots, face.PrevOnSurf)),
                XtFieldValue.Ptr(FaceRingNext(face.FrontFaceUse, back: false, map)),
                XtFieldValue.Ptr(FaceRingPrevious(face.FrontFaceUse, back: false, map)),
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
                XtFieldValue.Ptr(NextLoop(slot, map)),
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
                edge.Tolerance != 0 ? XtFieldValue.RealValue(edge.Tolerance) : XtFieldValue.Null(),
                XtFieldValue.Ptr(Ptr(map.FinSlots, PrimaryFin(edge))),
                XtFieldValue.Ptr(PreviousEdge(slot, map)),
                XtFieldValue.Ptr(NextEdge(slot, map)),
                XtFieldValue.Ptr(edge.CurveTag > 0 ? map.CurveTags[edge.CurveTag] : 0),
                XtFieldValue.Ptr(EdgeOnCurve(slot, next: true, map)),
                XtFieldValue.Ptr(EdgeOnCurve(slot, next: false, map)),
                XtFieldValue.Ptr(map.Body),
            ],
        };
    }

    private static XtNodeIndex EdgeOnCurve(EdgeSlot slot, bool next, NodeMap map)
    {
        var curve = KernelRuntime.GetEdgeRecord(slot).CurveTag;
        if (curve <= 0) return 0;
        XtNodeIndex first = 0, previous = 0, before = 0, after = 0;
        var found = false;
        foreach (var pair in map.EdgeSlots)
        {
            if (KernelRuntime.GetEdgeRecord(pair.Key).CurveTag != curve) continue;
            if (first == 0) first = pair.Value;
            if (found && after == 0) after = pair.Value;
            if (pair.Key == slot)
            {
                before = previous;
                found = true;
            }
            previous = pair.Value;
        }
        if (first == previous) return 0;
        // Shared geometry uses a circular owner ring; singleton owners use null.
        return next ? (after != 0 ? after : first) : (before != 0 ? before : previous);
    }

    private static XtNode HalfedgeNode(FinSlot slot, NodeMap map)
    {
        var fin = KernelRuntime.GetFinRecord(slot);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Halfedge,
            Index = map.FinSlots[slot],
            Fields =
            [
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(Ptr(map.LoopSlots, fin.Loop)),
                XtFieldValue.Ptr(Ptr(map.FinSlots, fin.NextInLoop)),
                XtFieldValue.Ptr(Ptr(map.FinSlots, fin.PrevInLoop)),
                XtFieldValue.Ptr(Ptr(map.VertexSlots, fin.Vertex)),
                XtFieldValue.Ptr(Ptr(map.FinSlots, fin.Other)),
                XtFieldValue.Ptr(Ptr(map.EdgeSlots, fin.Edge)),
                XtFieldValue.Ptr(Ptr(map.CurveTags, fin.Curve)),
                XtFieldValue.Ptr(NextAtVertex(slot, fin, map)),
                XtFieldValue.Char(FinSense(slot, fin)),
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
                XtFieldValue.Ptr(Ptr(map.FinSlots, vertex.FirstFinVertex)),
                XtFieldValue.Ptr(PreviousVertex(slot, map)),
                XtFieldValue.Ptr(NextVertex(slot, map)),
                XtFieldValue.Ptr(vertex.PointTag > 0 ? map.PointTags[vertex.PointTag] : 0),
                vertex.Tolerance != 0 ? XtFieldValue.RealValue(vertex.Tolerance) : XtFieldValue.Null(),
                XtFieldValue.Ptr(map.Body),
            ],
        };
    }

    private static XtNode PlaneNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        var data = KernelRuntime.GetPlaneData(surface.DataIndex);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Plane,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
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
        return new XtNode
        {
            Type = (int)XtNodeTypes.Cylinder,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.RealValue(data.Radius),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
            ],
        };
    }

    private static XtNode ConeNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        var data = KernelRuntime.GetConeData(surface.DataIndex);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Cone,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.RealValue(data.Radius),
                XtFieldValue.RealValue(Math.Sin(data.SemiAngle)),
                XtFieldValue.RealValue(Math.Cos(data.SemiAngle)),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
            ],
        };
    }

    private static XtNode SphereNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        var data = KernelRuntime.GetSphereData(surface.DataIndex);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Sphere,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.CenterX, data.CenterY, data.CenterZ),
                XtFieldValue.RealValue(data.Radius),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
            ],
        };
    }

    private static XtNode TorusNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        var data = KernelRuntime.GetTorusData(surface.DataIndex);
        return new XtNode
        {
            Type = (int)XtNodeTypes.Torus,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.RealValue(data.MajorRadius),
                XtFieldValue.RealValue(data.MinorRadius),
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
                XtFieldValue.Ptr(CurveOwner(curve, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.LocationX, data.LocationY, data.LocationZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
            ],
        };
    }

    private static XtNode BCurveNode(XtNodeIndex index, CurveTag tag, CurveRecord curve,
        ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        var data = KernelRuntime.BCurveDataStore[curve.DataIndex];
        var nurbs = AddIndex(nodes, ref graph, ref map);
        var vertices = AddIndex(nodes, ref graph, ref map);
        var knots = AddIndex(nodes, ref graph, ref map);
        var mults = AddIndex(nodes, ref graph, ref map);
        var curveData = AddIndex(nodes, ref graph, ref map);
        var poles = data.PolesSpan();
        var knotValues = data.KnotsSpan();
        var multiplicities = data.KnotMultsSpan();
        var poleFields = new XtFieldValue[poles.Length];
        var knotFields = new XtFieldValue[data.NKnots];
        var multFields = new XtFieldValue[data.NKnots];
        for (BufferOffset i = 0; i < poles.Length; i++) poleFields[i] = XtFieldValue.RealValue(poles[i]);
        for (KnotIndex i = 0; i < data.NKnots; i++)
        {
            knotFields[i] = XtFieldValue.RealValue(knotValues[i]);
            multFields[i] = XtFieldValue.Int(multiplicities[i]);
        }
        SetNode(nodes, map, vertices, new XtNode { Type=(int)XtNodeTypes.BSplineVertices, Index=vertices, VariableLength=poles.Length, Fields=poleFields });
        SetNode(nodes, map, knots, new XtNode { Type=(int)XtNodeTypes.KnotSet, Index=knots, VariableLength=data.NKnots, Fields=knotFields });
        SetNode(nodes, map, mults, new XtNode { Type=(int)XtNodeTypes.KnotMultiplicities, Index=mults, VariableLength=data.NKnots, Fields=multFields });
        SetNode(nodes, map, curveData, new XtNode { Type=(int)XtNodeTypes.CurveData, Index=curveData, Fields=
            [XtFieldValue.Unsigned(data.SelfIntersecting - ParasolidConstants.PK_self_intersect_unset_c + 1), XtFieldValue.Ptr(0)] });
        SetNode(nodes, map, nurbs, new XtNode
        {
            Type=(int)XtNodeTypes.NurbsCurve, Index=nurbs, Fields=
            [
                XtFieldValue.Int(data.Degree), XtFieldValue.Int(data.NVertices), XtFieldValue.Int(data.VertexDim),
                XtFieldValue.Int(data.NKnots), XtFieldValue.Unsigned(data.KnotType - ParasolidConstants.PK_knot_unset_c),
                XtFieldValue.Logical(data.IsPeriodic != 0), XtFieldValue.Logical(data.IsClosed != 0),
                XtFieldValue.Logical(data.IsRational != 0), XtFieldValue.Unsigned(data.Form - ParasolidConstants.PK_BCURVE_form_unset_c),
                XtFieldValue.Ptr(vertices), XtFieldValue.Ptr(mults), XtFieldValue.Ptr(knots),
            ],
        });
        var floating = map.FloatingCurves.Contains(tag);
        return new XtNode
        {
            Type=(int)XtNodeTypes.BCurve, Index=index, Fields=
            [
                floating ? XtFieldValue.Int(0) : XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                floating ? XtFieldValue.Ptr(0) : XtFieldValue.Ptr(CurveOwner(curve, map)),
                floating ? XtFieldValue.Ptr(0) : XtFieldValue.Ptr(NextCurve(tag, map)),
                floating ? XtFieldValue.Ptr(0) : XtFieldValue.Ptr(PreviousCurve(tag, map)),
                floating ? XtFieldValue.Ptr(0) : GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'), XtFieldValue.Ptr(nurbs), XtFieldValue.Ptr(curveData),
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
                XtFieldValue.Ptr(CurveOwner(curve, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.CenterX, data.CenterY, data.CenterZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
                XtFieldValue.RealValue(data.Radius),
            ],
        };
    }

    private static XtNode EllipseNode(XtNodeIndex index, CurveTag tag, CurveRecord curve, NodeMap map)
    {
        var data = KernelRuntime.EllipseDataPool[curve.DataIndex];
        return new XtNode
        {
            Type = (int)XtNodeTypes.Ellipse,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(CurveOwner(curve, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Vec(data.CenterX, data.CenterY, data.CenterZ),
                XtFieldValue.Vec(data.AxisX, data.AxisY, data.AxisZ),
                XtFieldValue.Vec(data.RefDirX, data.RefDirY, data.RefDirZ),
                XtFieldValue.RealValue(data.R1),
                XtFieldValue.RealValue(data.R2),
            ],
        };
    }

    private static XtNode TrimmedCurveNode(XtNodeIndex index, CurveTag tag, CurveRecord curve, NodeMap map)
    {
        ref readonly var data = ref KernelRuntime.TrCurveDataPool[curve.DataIndex];
        return new XtNode
        {
            Type = (int)XtNodeTypes.TrimmedCurve,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(CurveOwner(curve, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Ptr(Ptr(map.CurveTags, data.BasisCurveTag)),
                XtFieldValue.Vec(data.Point1.X, data.Point1.Y, data.Point1.Z),
                XtFieldValue.Vec(data.Point2.X, data.Point2.Y, data.Point2.Z),
                XtFieldValue.RealValue(data.Parm1),
                XtFieldValue.RealValue(data.Parm2),
            ],
        };
    }

    private static XtNode SpCurveNode(XtNodeIndex index, CurveTag tag, CurveRecord curve, NodeMap map)
    {
        ref readonly var data = ref KernelRuntime.SpCurveDataPool[curve.DataIndex];
        return new XtNode
        {
            Type = (int)XtNodeTypes.SpCurve,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(CurveOwner(curve, map)),
                XtFieldValue.Ptr(NextCurve(tag, map)),
                XtFieldValue.Ptr(PreviousCurve(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Ptr(Ptr(map.SurfaceTags, data.SurfTag)),
                XtFieldValue.Ptr(Ptr(map.CurveTags, data.BCurveTag)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Null(),
            ],
        };
    }

    private static XtNode BSurfaceNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface,
        ref NodeMap map, List<XtNode> nodes, ref TransmitGraph graph)
    {
        ref readonly var data = ref KernelRuntime.BSurfaceDataStore[surface.DataIndex];
        var nurbs = AddIndex(nodes, ref graph, ref map);
        var vertices = AddIndex(nodes, ref graph, ref map);
        var uMults = AddIndex(nodes, ref graph, ref map);
        var vMults = AddIndex(nodes, ref graph, ref map);
        var uKnots = AddIndex(nodes, ref graph, ref map);
        var vKnots = AddIndex(nodes, ref graph, ref map);
        var surfaceData = AddIndex(nodes, ref graph, ref map);

        var poles = data.PolesSpan();
        var poleFields = new XtFieldValue[poles.Length];
        for (BufferOffset i = 0; i < poles.Length; i++) poleFields[i] = XtFieldValue.RealValue(poles[i]);
        SetNode(nodes, map, vertices, new XtNode { Type = (int)XtNodeTypes.BSplineVertices, Index = vertices, VariableLength = poles.Length, Fields = poleFields });

        var uKnotValues = data.UKnotsSpan();
        var vKnotValues = data.VKnotsSpan();
        var uMultiplicities = data.UKnotMultsSpan();
        var vMultiplicities = data.VKnotMultsSpan();
        var uKnotFields = new XtFieldValue[data.NUKnots];
        var vKnotFields = new XtFieldValue[data.NVKnots];
        var uMultFields = new XtFieldValue[data.NUKnots];
        var vMultFields = new XtFieldValue[data.NVKnots];
        for (KnotIndex i = 0; i < data.NUKnots; i++)
        {
            uKnotFields[i] = XtFieldValue.RealValue(uKnotValues[i]);
            uMultFields[i] = XtFieldValue.Int(uMultiplicities[i]);
        }
        for (KnotIndex i = 0; i < data.NVKnots; i++)
        {
            vKnotFields[i] = XtFieldValue.RealValue(vKnotValues[i]);
            vMultFields[i] = XtFieldValue.Int(vMultiplicities[i]);
        }
        SetNode(nodes, map, uKnots, new XtNode { Type = (int)XtNodeTypes.KnotSet, Index = uKnots, VariableLength = data.NUKnots, Fields = uKnotFields });
        SetNode(nodes, map, vKnots, new XtNode { Type = (int)XtNodeTypes.KnotSet, Index = vKnots, VariableLength = data.NVKnots, Fields = vKnotFields });
        SetNode(nodes, map, uMults, new XtNode { Type = (int)XtNodeTypes.KnotMultiplicities, Index = uMults, VariableLength = data.NUKnots, Fields = uMultFields });
        SetNode(nodes, map, vMults, new XtNode { Type = (int)XtNodeTypes.KnotMultiplicities, Index = vMults, VariableLength = data.NVKnots, Fields = vMultFields });

        // SURFACE_DATA persistent fields mirror what Parasolid transmits for a
        // created B-surface: floor/ceil integer bounds of the original UV box,
        // unset extended box, and 'B' original-range boundary markers.
        SetNode(nodes, map, surfaceData, new XtNode
        {
            Type = (int)XtNodeTypes.SurfaceData,
            Index = surfaceData,
            Fields =
            [
                XtFieldValue.IntervalValue(Math.Floor(surface.UMin), Math.Ceiling(surface.UMax)),
                XtFieldValue.IntervalValue(Math.Floor(surface.VMin), Math.Ceiling(surface.VMax)),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Unsigned(data.SelfIntersecting - ParasolidConstants.PK_self_intersect_unset_c + 1),
                XtFieldValue.Char('B'),
                XtFieldValue.Char('B'),
                XtFieldValue.Char('B'),
                XtFieldValue.Char('B'),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Null(),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(0),
            ],
        });

        SetNode(nodes, map, nurbs, new XtNode
        {
            Type = (int)XtNodeTypes.NurbsSurf,
            Index = nurbs,
            Fields =
            [
                XtFieldValue.Logical(data.IsUPeriodic != 0),
                XtFieldValue.Logical(data.IsVPeriodic != 0),
                XtFieldValue.Int(data.UDegree),
                XtFieldValue.Int(data.VDegree),
                XtFieldValue.Int(data.NUVertices),
                XtFieldValue.Int(data.NVVertices),
                XtFieldValue.Unsigned(data.UKnotType - ParasolidConstants.PK_knot_unset_c),
                XtFieldValue.Unsigned(data.VKnotType - ParasolidConstants.PK_knot_unset_c),
                XtFieldValue.Int(data.NUKnots),
                XtFieldValue.Int(data.NVKnots),
                XtFieldValue.Logical(data.IsRational != 0),
                XtFieldValue.Logical(data.IsUClosed != 0),
                XtFieldValue.Logical(data.IsVClosed != 0),
                XtFieldValue.Unsigned(data.Form - ParasolidConstants.PK_BSURF_form_unset_c),
                XtFieldValue.Int(data.VertexDim),
                XtFieldValue.Ptr(vertices),
                XtFieldValue.Ptr(uMults),
                XtFieldValue.Ptr(vMults),
                XtFieldValue.Ptr(uKnots),
                XtFieldValue.Ptr(vKnots),
            ],
        });

        return new XtNode
        {
            Type = (int)XtNodeTypes.BSurface,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Ptr(nurbs),
                XtFieldValue.Ptr(surfaceData),
            ],
        };
    }

    private static XtNode SweptSurfaceNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        ref readonly var data = ref KernelRuntime.SweptDataPool[surface.DataIndex];
        return new XtNode
        {
            Type = (int)XtNodeTypes.SweptSurf,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Ptr(Ptr(map.CurveTags, data.SectionCurveTag)),
                XtFieldValue.Vec(data.Sweep.X, data.Sweep.Y, data.Sweep.Z),
                XtFieldValue.Null(),
            ],
        };
    }

    private static XtNode SpunSurfaceNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        ref readonly var data = ref KernelRuntime.SpunDataPool[surface.DataIndex];
        return new XtNode
        {
            Type = (int)XtNodeTypes.SpunSurf,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Ptr(Ptr(map.CurveTags, data.ProfileCurveTag)),
                XtFieldValue.Vec(data.Base.X, data.Base.Y, data.Base.Z),
                XtFieldValue.Vec(data.Axis.X, data.Axis.Y, data.Axis.Z),
                SpunVector(data.Start),
                SpunVector(data.End),
                data.StartParam != 0 ? XtFieldValue.RealValue(data.StartParam) : XtFieldValue.Null(),
                data.EndParam != 0 ? XtFieldValue.RealValue(data.EndParam) : XtFieldValue.Null(),
                SpunVector(data.XAxis),
                XtFieldValue.Null(),
            ],
        };
    }

    // Schema-nullable spun fields carry a zero vector only when unset by the
    // creating API; a real degeneracy point never coincides with the origin
    // of the axis frame (the profile would be degenerate there).
    private static XtFieldValue SpunVector(KernelVector3 value)
        => value.X == 0 && value.Y == 0 && value.Z == 0
            ? XtFieldValue.Null()
            : XtFieldValue.Vec(value.X, value.Y, value.Z);

    private static XtNode OffsetSurfaceNode(XtNodeIndex index, SurfTag tag, SurfaceRecord surface, NodeMap map)
    {
        ref readonly var data = ref KernelRuntime.OffsetDataPool[surface.DataIndex];
        return new XtNode
        {
            Type = (int)XtNodeTypes.OffsetSurf,
            Index = index,
            Fields =
            [
                XtFieldValue.Int(NodeId(index, map)),
                XtFieldValue.Ptr(0),
                XtFieldValue.Ptr(SurfaceOwner(surface, map)),
                XtFieldValue.Ptr(NextSurface(tag, map)),
                XtFieldValue.Ptr(PreviousSurface(tag, map)),
                GeometricOwnerField(tag, map),
                XtFieldValue.Char('+'),
                XtFieldValue.Char((char)data.Check),
                XtFieldValue.Logical(false),
                XtFieldValue.Ptr(Ptr(map.SurfaceTags, data.BaseSurfTag)),
                XtFieldValue.RealValue(data.Offset),
                XtFieldValue.Null(),
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
                XtFieldValue.Ptr(Ptr(map.VertexSlots, data.OwnerVertex)),
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

    // Parasolid chains ownerless dependency geometry (a trimmed curve's basis,
    // an SP curve's 2D B-curve, a swept section) in the body's boundary chain
    // with the body itself as owner; owner=0 is rejected on receive.
    private static XtNodeIndex CurveOwner(CurveRecord curve, NodeMap map)
    {
        var edge = Ptr(map.EdgeSlots, curve.OwnerEdge);
        return edge != 0 ? edge : map.Body;
    }

    private static XtNodeIndex SurfaceOwner(SurfaceRecord surface, NodeMap map)
    {
        var face = Ptr(map.FaceSlots, surface.OwnerFace);
        return face != 0 ? face : map.Body;
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

    private static XtNodeIndex NextShellInRegion(ShellSlot slot, NodeMap map)
    {
        var shell = KernelRuntime.GetShellRecord(slot);
        var first = KernelRuntime.GetRegionRecord(shell.Region).FirstShell;
        return shell.NextInRegion != first ? Ptr(map.ShellSlots, shell.NextInRegion) : 0;
    }

    private static XtNodeIndex NextRegion(RegionSlot slot, NodeMap map)
    {
        var next = KernelRuntime.GetRegionRecord(slot).NextInBody;
        return next != map.FirstRegionSlot ? Ptr(map.RegionSlots, next) : 0;
    }

    private static XtNodeIndex PreviousRegion(RegionSlot slot, NodeMap map)
    {
        return slot != map.FirstRegionSlot ? Ptr(map.RegionSlots, KernelRuntime.GetRegionRecord(slot).PrevInBody) : 0;
    }

    private static XtNodeIndex NextLoop(LoopSlot slot, NodeMap map)
    {
        var loop = KernelRuntime.GetLoopRecord(slot);
        var first = KernelRuntime.GetFaceRecord(loop.Face).FirstLoop;
        return loop.NextInFace != first ? Ptr(map.LoopSlots, loop.NextInFace) : 0;
    }

    private static XtNodeIndex NextEdge(EdgeSlot slot, NodeMap map)
    {
        var next = KernelRuntime.GetEdgeRecord(slot).NextInBody;
        return next != map.FirstEdgeSlot ? Ptr(map.EdgeSlots, next) : 0;
    }

    private static XtNodeIndex PreviousEdge(EdgeSlot slot, NodeMap map)
    {
        return slot != map.FirstEdgeSlot ? Ptr(map.EdgeSlots, KernelRuntime.GetEdgeRecord(slot).PrevInBody) : 0;
    }

    private static XtNodeIndex NextVertex(VertexSlot slot, NodeMap map)
    {
        var next = KernelRuntime.GetVertexRecord(slot).NextInBody;
        return next != map.FirstVertexSlot ? Ptr(map.VertexSlots, next) : 0;
    }

    private static XtNodeIndex PreviousVertex(VertexSlot slot, NodeMap map)
    {
        return slot != map.FirstVertexSlot ? Ptr(map.VertexSlots, KernelRuntime.GetVertexRecord(slot).PrevInBody) : 0;
    }

    private static char FinSense(FinSlot slot, FinRecord fin)
    {
        if (fin.Edge < 0)
            return '?';
        if (fin.Vertex < 0)
            return slot == KernelRuntime.GetEdgeRecord(fin.Edge).FirstFinEdge ? '+' : '-';
        return fin.Sense;
    }

    // XT: edge.halfedge heads the edge's fin chain and must be the positive
    // (primary) fin (XT schema 5.3.9.1).
    private static FinSlot PrimaryFin(EdgeRecord edge)
    {
        var finSlot = edge.FirstFinEdge;
        for (var i = 0; i < edge.FinCount; i++, finSlot = KernelRuntime.GetFinRecord(finSlot).NextOfEdge)
        {
            if (KernelRuntime.GetFinRecord(finSlot).Sense == '+')
                return finSlot;
        }
        return edge.FirstFinEdge;
    }

    // Runtime face-use rings become null-terminated XT chains, separately for
    // back/front uses. Do not wrap around the shell's first use when exporting.
    private static XtNodeIndex FaceRingNext(FaceUseSlot useSlot, bool back, NodeMap map)
    {
        if (useSlot < 0)
            return 0;
        var start = KernelRuntime.GetShellRecord(KernelRuntime.GetFaceUseRecord(useSlot).Shell).FirstFaceUseShell;
        var cur = KernelRuntime.GetFaceUseRecord(useSlot).NextInShell;
        while (cur != start)
        {
            var use = KernelRuntime.GetFaceUseRecord(cur);
            if (IsBackFaceUse(use) == back)
                return Ptr(map.FaceSlots, use.Face);
            cur = use.NextInShell;
        }
        return 0;
    }

    private static XtNodeIndex FaceRingPrevious(FaceUseSlot useSlot, bool back, NodeMap map)
    {
        if (useSlot < 0)
            return 0;
        var start = KernelRuntime.GetShellRecord(KernelRuntime.GetFaceUseRecord(useSlot).Shell).FirstFaceUseShell;
        var cur = useSlot;
        while (cur != start)
        {
            cur = KernelRuntime.GetFaceUseRecord(cur).PrevInShell;
            var use = KernelRuntime.GetFaceUseRecord(cur);
            if (IsBackFaceUse(use) == back)
                return Ptr(map.FaceSlots, use.Face);
        }
        return 0;
    }

    private static bool IsBackFaceUse(FaceUseRecord use)
        => use.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c;

    private static XtNodeIndex NextAtVertex(FinSlot slot, FinRecord fin, NodeMap map)
    {
        if (fin.Vertex < 0)
            return 0;

        var vertex = KernelRuntime.GetVertexRecord(fin.Vertex);
        return fin.NextAtVertex != vertex.FirstFinVertex ? Ptr(map.FinSlots, fin.NextAtVertex) : 0;
    }

    private static XtNodeIndex NextSurface(SurfTag tag, NodeMap map)
        => ChainNeighbour(tag, map.SurfaceChain, map.SurfaceTags, offset: 1);

    private static XtNodeIndex PreviousSurface(SurfTag tag, NodeMap map)
        => ChainNeighbour(tag, map.SurfaceChain, map.SurfaceTags, offset: -1);

    private static XtNodeIndex NextCurve(CurveTag tag, NodeMap map)
        => ChainNeighbour(tag, map.CurveChain, map.CurveTags, offset: 1);

    private static XtNodeIndex PreviousCurve(CurveTag tag, NodeMap map)
        => ChainNeighbour(tag, map.CurveChain, map.CurveTags, offset: -1);

    private static XtNodeIndex ChainNeighbour(int tag, List<int> chain, Dictionary<int, XtNodeIndex> tags, int offset)
    {
        var index = chain.IndexOf(tag);
        if (index < 0)
            return 0;
        var neighbour = index + offset;
        if (neighbour < 0 || neighbour >= chain.Count)
            return 0;
        return tags.TryGetValue(chain[neighbour], out var node) ? node : 0;
    }

    private static XtNodeIndex NextPoint(PointTag tag, NodeMap map)
    {
        var slot = KernelRuntime.GetPointSlotByTag(tag);
        if (slot < 0)
            return 0;

        var point = KernelRuntime.Points[slot];
        var firstTag = map.FirstVertexSlot >= 0 ? KernelRuntime.GetVertexRecord(map.FirstVertexSlot).PointTag : 0;
        return point.NextInBody != firstTag ? Ptr(map.PointTags, point.NextInBody) : 0;
    }

    private static XtNodeIndex PreviousPoint(PointTag tag, NodeMap map)
    {
        var slot = KernelRuntime.GetPointSlotByTag(tag);
        if (slot < 0)
            return 0;

        var point = KernelRuntime.Points[slot];
        return point.OwnerVertex != map.FirstVertexSlot ? Ptr(map.PointTags, point.PrevInBody) : 0;
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
        public RegionSlot FirstRegionSlot;
        public FaceSlot FirstFaceSlot;
        public EdgeSlot FirstEdgeSlot;
        public VertexSlot FirstVertexSlot;
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
        public List<int> SurfaceChain;
        public List<int> CurveChain;
        public HashSet<int> FloatingCurves;
        public Dictionary<int, XtNodeIndex> GeometricOwnerNodes;
        public List<(XtNodeIndex Node, int DependentTag, bool DependentIsCurve, int SharedTag, bool SharedIsCurve)> PendingGeometricOwners;

        public NodeMap()
        {
            Body = 0;
            FirstRegionSlot = -1;
            FirstFaceSlot = -1;
            FirstEdgeSlot = -1;
            FirstVertexSlot = -1;
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
            SurfaceChain = [];
            CurveChain = [];
            FloatingCurves = new HashSet<int>();
            GeometricOwnerNodes = new Dictionary<int, XtNodeIndex>();
            PendingGeometricOwners = new List<(XtNodeIndex, int, bool, int, bool)>();
        }
    }

    private struct TransmitGraph
    {
        public XtNodeIndex NextNodeIndex;

        public TransmitGraph()
        {
            NextNodeIndex = 1;
        }
    }
}
