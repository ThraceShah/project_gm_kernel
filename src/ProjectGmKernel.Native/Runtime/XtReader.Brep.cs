using ProjectGmKernel.Native.Computation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class XtReader
{
    // Parsing and indexing belong to the existing managed XT adapter. The
    // runtime receives the validated graph and allocates only session records.
    private static AlgorithmStatus TryMaterializeBrep(XtDocument document, XtNode root, out EntityTag body)
    {
        body = 0;
        try
        {
            var graph = new BrepGraph(document, root);
            graph.Validate();
            var topology = graph.Topology.ToArray();
            var geometry = graph.Geometry.ToArray();
            return KernelRuntime.MaterializeXtBrep(document, root, topology, new int[topology.Length],
                geometry, new int[geometry.Length], graph.Construction.ToArray(), out body);
        }
        catch (FormatException) { return AlgorithmStatus.InvalidInput; }
        catch (NotSupportedException) { return AlgorithmStatus.Unsupported; }
    }

    private sealed class BrepGraph
    {
        private readonly XtDocument document;
        private readonly XtNode root;
        private readonly Dictionary<XtNodeIndex, XtNode> byIndex;
        private readonly HashSet<XtNodeIndex> topologyIndexes = [];
        private readonly HashSet<XtNodeIndex> geometryIndexes = [];
        private readonly HashSet<XtNodeIndex> visitingGeometry = [];
        private readonly HashSet<XtNodeIndex> expanded = [];
        internal readonly List<XtNode> Topology = [];
        internal readonly List<XtNode> Geometry = [];
        internal readonly List<XtNode> Construction = [];

        internal BrepGraph(XtDocument document, XtNode root)
        {
            this.document = document;
            this.root = root;
            byIndex = new Dictionary<int, XtNode>(document.Nodes.Length);
            foreach (var node in document.Nodes)
                if (!byIndex.TryAdd(node.Index, node)) throw new FormatException("Duplicate XT node index.");
        }

        internal void Validate()
        {
            if (!KernelRuntime.HasCanonicalICurveWireBodyLayout(document, root)) throw new NotSupportedException();
            if (root.Fields[17].Integer is not (1 or 2 or 3 or 6)) throw new NotSupportedException();
            foreach (var field in new[] { 3, 6, 7, 8, 20, 24, 25, 33 })
                if (Ptr(root, field) != 0) throw new NotSupportedException("Additional body geometry or compound topology.");
            Walk(Ptr(root, 26), XtNodeTypes.Region, 3, region =>
            {
                AddTopology(region);
                Require(Ptr(region, 2) == root.Index);
                if (Ptr(region, 6) != 0 || Ptr(region, 8) != 0) throw new NotSupportedException();
                if (region.Fields[7].Character is not ('S' or 'V')) throw new FormatException("Invalid region type.");
                Walk(Ptr(region, 5), XtNodeTypes.Shell, 3, shell =>
                {
                    Require(Ptr(shell, 7) == region.Index);
                    AddShell(shell);
                });
            });
            var firstShell = Ptr(root, 19);
            if (firstShell != 0 && !topologyIndexes.Contains(firstShell))
                throw new NotSupportedException("A shell without an explicit region is not materialized.");
            if (firstShell != 0) RequireTopology(firstShell, XtNodeTypes.Shell);
            Walk(Ptr(root, 27), XtNodeTypes.Edge, 5, edge => AddEdge(edge, root.Index));
            Walk(Ptr(root, 28), XtNodeTypes.Vertex, 4, vertex =>
            {
                Require(Ptr(vertex, 7) == root.Index);
                AddTopology(vertex);
                Tolerance(vertex, 6);
                AddGeometry(Ptr(vertex, 5), XtNodeTypes.Point);
            });
            if(root.Fields[17].Integer==2&&Ptr(root,28)==0&&Geometry.Any(node=>node.Type==(int)XtNodeTypes.Intersection))
                throw new NotSupportedException("Vertexless intersection wire topology remains opaque.");
            foreach (var node in Topology)
            {
                if (node.Type == (int)XtNodeTypes.Halfedge)
                {
                    if (Ptr(node, 6) != 0)
                    {
                        RequireTopology(Ptr(node, 6), XtNodeTypes.Edge);
                        RequireInChain(Ptr(Node(Ptr(node, 6)), 3), XtNodeTypes.Halfedge, 5, node.Index);
                        RequireTopology(Ptr(node, 5), XtNodeTypes.Halfedge);
                    }
                    if (Ptr(node, 1) != 0)
                    {
                        RequireTopology(Ptr(node, 1), XtNodeTypes.Loop);
                        RequireInChain(Ptr(Node(Ptr(node, 1)), 2), XtNodeTypes.Halfedge, 2, node.Index);
                    }
                    if (Ptr(node, 4) != 0) RequireTopology(Ptr(node, 4), XtNodeTypes.Vertex);
                    if (Ptr(node, 2) != 0) RequireTopology(Ptr(node, 2), XtNodeTypes.Halfedge);
                    if (Ptr(node, 3) != 0) RequireTopology(Ptr(node, 3), XtNodeTypes.Halfedge);
                }
                else if (node.Type == (int)XtNodeTypes.Face)
                {
                    if (Ptr(node, 6) != 0)
                    {
                        RequireTopology(Ptr(node, 6), XtNodeTypes.Shell);
                        RequireInChain(Ptr(Node(Ptr(node, 6)), 4), XtNodeTypes.Face, 3, node.Index);
                    }
                    if (Ptr(node, 13) != 0)
                    {
                        RequireTopology(Ptr(node, 13), XtNodeTypes.Shell);
                        RequireInChain(Ptr(Node(Ptr(node, 13)), 8), XtNodeTypes.Face, 11, node.Index);
                    }
                }
                else if (node.Type == (int)XtNodeTypes.Vertex)
                {
                    var fins = new HashSet<int>();
                    Walk(Ptr(node, 2), XtNodeTypes.Halfedge, 8, fin =>
                    {
                        Require(topologyIndexes.Contains(fin.Index) && Ptr(fin, 4) == node.Index);
                        fins.Add(fin.Index);
                    });
                    foreach (var fin in Topology)
                        if (fin.Type == (int)XtNodeTypes.Halfedge && Ptr(fin, 4) == node.Index)
                            Require(fins.Contains(fin.Index));
                }
                else if (node.Type == (int)XtNodeTypes.Edge)
                {
                    int? start = null, end = null;
                    Walk(Ptr(node, 3), XtNodeTypes.Halfedge, 5, fin =>
                    {
                        var forward = Ptr(fin, 4);
                        var positive = fin.Fields[9].Character == '+';
                        if (positive)
                        {
                            if (end.HasValue) Require(end.Value == forward);
                            end = forward;
                        }
                        else
                        {
                            if (start.HasValue) Require(start.Value == forward);
                            start = forward;
                        }
                        if (Ptr(fin, 1) != 0)
                        {
                            var backward = Ptr(Node(Ptr(fin, 3), XtNodeTypes.Halfedge), 4);
                            if (positive)
                            {
                                if (start.HasValue) Require(start.Value == backward);
                                start = backward;
                            }
                            else
                            {
                                if (end.HasValue) Require(end.Value == backward);
                                end = backward;
                            }
                        }
                    });
                    Require(start.HasValue && end.HasValue && ((start == 0) == (end == 0)));
                }
            }
            // Part-owned geometry uses separate chains from geometry attached
            // to topology. A construction member may also define another
            // construction member; retain its explicit BODY ownership too.
            WalkConstructionGeometry(Ptr(root, 4), surface: true);
            WalkConstructionGeometry(Ptr(root, 5), surface: false);
            // Boundary chains include both attached geometry and dependencies.
            foreach (var field in new[] { 21, 22, 23 })
                WalkAnyGeometry(Ptr(root, field));
        }

        private void AddShell(XtNode shell)
        {
            Require(Ptr(shell, 2) == 0 || Ptr(shell, 2) == root.Index);
            AddTopology(shell);
            if (Ptr(shell, 6) != 0) throw new NotSupportedException("Acorn shells are outside this importer.");
            Walk(Ptr(shell, 4), XtNodeTypes.Face, 3, face => { Require(Ptr(face, 6) == shell.Index); AddFace(face); });
            Walk(Ptr(shell, 8), XtNodeTypes.Face, 11, face => { Require(Ptr(face, 13) == shell.Index); AddFace(face); });
            Walk(Ptr(shell, 5), XtNodeTypes.Edge, 5, edge => AddEdge(edge, shell.Index));
        }

        private void AddFace(XtNode face)
        {
            AddTopology(face);
            if (!expanded.Add(face.Index)) return;
            Tolerance(face, 2);
            Require(face.Fields[8].Character is '+' or '-');
            AddGeometry(Ptr(face, 7));
            Require(IsSurface(Node(Ptr(face, 7))));
            Walk(Ptr(face, 5), XtNodeTypes.Loop, 4, loop =>
            {
                AddTopology(loop);
                Require(Ptr(loop, 3) == face.Index && Ptr(loop, 2) != 0);
                Walk(Ptr(loop, 2), XtNodeTypes.Halfedge, 2, fin =>
                {
                    Require(Ptr(fin, 1) == loop.Index);
                    var next = Node(Ptr(fin, 2), XtNodeTypes.Halfedge);
                    var previous = Node(Ptr(fin, 3), XtNodeTypes.Halfedge);
                    Require(Ptr(next, 3) == fin.Index && Ptr(previous, 2) == fin.Index);
                    AddFin(fin);
                });
            });
        }

        private void AddEdge(XtNode edge, XtNodeIndex owner)
        {
            Require(Ptr(edge, 9) == owner);
            AddTopology(edge);
            if (!expanded.Add(edge.Index)) return;
            Tolerance(edge, 2);
            var nominal = Ptr(edge, 6);
            if (nominal != 0)
            {
                AddGeometry(nominal);
                Require(IsCurve(Node(nominal)));
            }
            else Require(edge.Fields[2].Kind == XtFieldKind.Real && edge.Fields[2].Real > 0);
            Require(Ptr(edge, 3) != 0);
            var parameterCurves = 0;
            Walk(Ptr(edge, 3), XtNodeTypes.Halfedge, 5, fin =>
            {
                Require(Ptr(fin, 6) == edge.Index);
                if (nominal == 0 && Ptr(fin, 1) != 0)
                {
                    Require(Ptr(fin, 7) != 0);
                    parameterCurves++;
                }
                AddFin(fin);
            });
            if (nominal == 0) Require(parameterCurves > 0);
        }

        private void AddFin(XtNode fin)
        {
            AddTopology(fin);
            if (Ptr(fin, 6) == 0)
            {
                Require(Ptr(fin, 1) != 0 && Ptr(fin, 4) != 0 && Ptr(fin, 5) == 0);
                if (Ptr(fin, 2) != fin.Index || Ptr(fin, 3) != fin.Index)
                    throw new NotSupportedException("Only single-vertex pole loops are materialized.");
                Require(fin.Fields[9].Kind == XtFieldKind.Empty || fin.Fields[9].Character == '?');
            }
            else Require(fin.Fields[9].Character is '+' or '-');
            if (Ptr(fin, 7) != 0)
            {
                AddGeometry(Ptr(fin, 7));
                var curve = Node(Ptr(fin, 7));
                Require(IsCurve(curve) && Ptr(curve, 2) == fin.Index);
            }
        }

        private void AddTopology(XtNode node)
        {
            var expected = (XtNodeTypes)node.Type switch
            {
                XtNodeTypes.Region or XtNodeTypes.Shell => 9,
                XtNodeTypes.Face => 14, XtNodeTypes.Loop => 5,
                XtNodeTypes.Halfedge or XtNodeTypes.Edge => 10,
                XtNodeTypes.Vertex => 8, _ => throw new FormatException(),
            };
            Require(node.Fields.Length == expected);
            if (topologyIndexes.Add(node.Index)) Topology.Add(node);
        }

        private void AddGeometry(XtNodeIndex index, XtNodeTypes? required = null)
        {
            if (index == 0) throw new NotSupportedException("Topology without attached geometry remains opaque.");
            var node = Node(index, required);
            if (geometryIndexes.Contains(index)) return;
            if (!visitingGeometry.Add(index)) throw new FormatException("Cyclic geometry dependency.");
            ValidateGeometryFields(node);
            switch ((XtNodeTypes)node.Type)
            {
                case XtNodeTypes.Point:
                case XtNodeTypes.Line:
                case XtNodeTypes.Circle:
                case XtNodeTypes.Ellipse:
                case XtNodeTypes.Plane:
                case XtNodeTypes.Cylinder:
                case XtNodeTypes.Cone:
                case XtNodeTypes.Sphere:
                case XtNodeTypes.Torus:
                case XtNodeTypes.BCurve:
                case XtNodeTypes.BSurface:
                    break;
                case XtNodeTypes.TrimmedCurve:
                case XtNodeTypes.SweptSurf:
                case XtNodeTypes.SpunSurf:
                    AddGeometry(Ptr(node, 7)); Require(IsCurve(Node(Ptr(node, 7)))); break;
                case XtNodeTypes.SpCurve:
                    AddGeometry(Ptr(node, 7)); AddGeometry(Ptr(node, 8), XtNodeTypes.BCurve);
                    Require(IsSurface(Node(Ptr(node, 7)))); break;
                case XtNodeTypes.OffsetSurf:
                    AddGeometry(Ptr(node, 9)); Require(IsSurface(Node(Ptr(node, 9)))); break;
                case XtNodeTypes.Intersection:
                    AddGeometry(Ptr(node,7));AddGeometry(Ptr(node,8));
                    Require(IsSurface(Node(Ptr(node,7)))&&IsSurface(Node(Ptr(node,8))));break;
                case XtNodeTypes.BlendedEdge:
                    AddGeometry(Ptr(node,8));AddGeometry(Ptr(node,9));AddGeometry(Ptr(node,10),XtNodeTypes.Intersection);
                    Require(IsSurface(Node(Ptr(node,8)))&&IsSurface(Node(Ptr(node,9))));break;
                default: throw new NotSupportedException("Unsupported boundary geometry.");
            }
            visitingGeometry.Remove(index);
            geometryIndexes.Add(index);
            Geometry.Add(node);
        }

        private static bool IsCurve(XtNode node) => (XtNodeTypes)node.Type is XtNodeTypes.Line or XtNodeTypes.Circle
            or XtNodeTypes.Ellipse or XtNodeTypes.BCurve or XtNodeTypes.TrimmedCurve or XtNodeTypes.SpCurve or XtNodeTypes.Intersection;

        private static bool IsSurface(XtNode node) => (XtNodeTypes)node.Type is XtNodeTypes.Plane or XtNodeTypes.Cylinder
            or XtNodeTypes.Cone or XtNodeTypes.Sphere or XtNodeTypes.Torus or XtNodeTypes.BSurface
            or XtNodeTypes.SweptSurf or XtNodeTypes.SpunSurf or XtNodeTypes.OffsetSurf or XtNodeTypes.BlendedEdge;

        private static void ValidateGeometryFields(XtNode node)
        {
            var expected = (XtNodeTypes)node.Type switch
            {
                XtNodeTypes.Point => 6, XtNodeTypes.Line or XtNodeTypes.BCurve or XtNodeTypes.BSurface => 9,
                XtNodeTypes.Plane or XtNodeTypes.SweptSurf => 10,
                XtNodeTypes.Circle or XtNodeTypes.Cylinder or XtNodeTypes.Sphere or XtNodeTypes.SpCurve => 11,
                XtNodeTypes.Ellipse or XtNodeTypes.Torus or XtNodeTypes.TrimmedCurve or XtNodeTypes.OffsetSurf => 12,
                XtNodeTypes.Cone => 13, XtNodeTypes.SpunSurf => 16,
                XtNodeTypes.Intersection=>node.Fields.Length==14?14:13,XtNodeTypes.BlendedEdge=>19,
                _ => throw new NotSupportedException("Unsupported boundary geometry."),
            };
            Require(node.Fields.Length == expected);
            if (node.Type != (int)XtNodeTypes.Point)
                Require(node.Fields[6].Kind == XtFieldKind.Character && node.Fields[6].Character is '+' or '-');
            foreach (var field in node.Fields)
            {
                if (field.Kind == XtFieldKind.Real) Require(double.IsFinite(field.Real));
                else if (field.Kind == XtFieldKind.Vector)
                    Require(double.IsFinite(field.Vector.X) && double.IsFinite(field.Vector.Y) && double.IsFinite(field.Vector.Z));
            }
        }

        private void WalkConstructionGeometry(XtNodeIndex first, bool surface)
        {
            var visited = new HashSet<int>();
            for (var index = first; index != 0;)
            {
                if (!visited.Add(index)) { Require(index == first); break; }
                var node = Node(index);
                Require(surface ? IsSurface(node) : IsCurve(node));
                Require(Ptr(node, 2) == root.Index);
                AddGeometry(index);
                Construction.Add(node);
                index = Ptr(node, 3);
            }
        }

        private void WalkAnyGeometry(XtNodeIndex first)
        {
            var visited = new HashSet<int>();
            for (var index = first; index != 0;)
            {
                if (!visited.Add(index)) { Require(index == first); break; }
                var node = Node(index);
                if (!geometryIndexes.Contains(index))
                    throw new NotSupportedException("Unreferenced construction geometry is not materialized.");
                index = Ptr(node, 3);
            }
        }

        private void Walk(XtNodeIndex first, XtNodeTypes type, BufferOffset next, Action<XtNode> visit)
        {
            var visited = new HashSet<int>();
            for (var index = first; index != 0;)
            {
                if (!visited.Add(index)) { Require(index == first); break; }
                var node = Node(index, type);
                visit(node);
                index = Ptr(node, next);
            }
        }

        private void RequireInChain(XtNodeIndex first, XtNodeTypes type, BufferOffset next, XtNodeIndex expected)
        {
            var found = false;
            Walk(first, type, next, node => { if (node.Index == expected) found = true; });
            Require(found);
        }

        private XtNode Node(XtNodeIndex index, XtNodeTypes? type = null)
        {
            if (index == 0 || !byIndex.TryGetValue(index, out var node)
                || (type.HasValue && node.Type != (int)type.Value)) throw new FormatException("Missing or inconsistent XT reference.");
            return node;
        }

        private void RequireTopology(XtNodeIndex index, XtNodeTypes type)
        {
            _ = Node(index, type);
            Require(topologyIndexes.Contains(index));
        }

        private static XtNodeIndex Ptr(XtNode node, BufferOffset field)
        {
            if ((uint)field >= node.Fields.Length || node.Fields[field].Kind != XtFieldKind.Pointer)
                throw new FormatException("Unexpected XT field kind.");
            return node.Fields[field].Pointer;
        }

        private static void Tolerance(XtNode node, BufferOffset field)
        {
            var value = node.Fields[field];
            if (value.Kind != XtFieldKind.Empty)
                Require(value.Kind == XtFieldKind.Real && double.IsFinite(value.Real) && value.Real >= 0);
        }

        private static void Require(bool condition)
        {
            if (!condition) throw new FormatException("Inconsistent XT B-rep topology.");
        }
    }
}
