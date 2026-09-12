using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using Xunit;

namespace KernelTests;

/// <summary>
/// The XT writer emits every geometry class the evaluators support. Each test
/// attaches dependent geometry to a primitive body, transmits text XT, and
/// checks the schema nodes, their dependency pointers, and the body geometry
/// chains (dependency geometry must stay reachable from the body).
/// </summary>
[Collection("KernelTests")]
public unsafe class XtGeometryWriterTests : IDisposable
{
    public XtGeometryWriterTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void EllipseAttachedToCylinderRim_Transmits()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidCyl(2, 3, null, &body));

        // A circular ellipse (r1 = r2) tracing the top rim circle exactly.
        int rimEdge = FindEdge(body, edge =>
        {
            var curve = KernelRuntime.GetCurveByTag(EdgeCurve(edge));
            if (curve.Class != CurveClass.Circle) return false;
            var data = KernelRuntime.GetCircleData(curve.DataIndex);
            return Math.Abs(data.CenterZ - 3) < 1e-9;
        });
        Assert.NotEqual(0, rimEdge);
        var rim = KernelRuntime.GetCircleData(KernelRuntime.GetCurveByTag(EdgeCurve(rimEdge)).DataIndex);

        var sf = new PK_ELLIPSE_sf_s { R1 = rim.Radius, R2 = rim.Radius };
        sf.basis_set.location.coord[0] = rim.CenterX;
        sf.basis_set.location.coord[1] = rim.CenterY;
        sf.basis_set.location.coord[2] = rim.CenterZ;
        sf.basis_set.axis.coord[2] = 1;
        sf.basis_set.ref_direction.coord[0] = 1;
        int ellipse;
        Assert.Equal(0, KernelRuntime.EllipseCreate(&sf, &ellipse));

        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(rimEdge));
        Assert.Equal(0, KernelRuntime.EdgeAttachCurves(1, &rimEdge, &ellipse));

        var document = Decode(Transmit(body));
        var node = SingleNode(document, XtNodeTypes.Ellipse);
        Assert.Equal(rim.CenterX, node.Fields[7].Vector.X, 14);
        Assert.Equal(rim.CenterY, node.Fields[7].Vector.Y, 14);
        Assert.Equal(rim.CenterZ, node.Fields[7].Vector.Z, 14);
        Assert.Equal(rim.Radius, node.Fields[10].Real, 14);
        Assert.Equal(rim.Radius, node.Fields[11].Real, 14);
        AssertInChain(document, CurveChainHead, node.Index);
    }

    [Fact]
    public void TrimmedCurveAttachedToBlockEdge_TransmitsWithBasisCurve()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body));
        KernelRuntime.TryResolveBodySlot(body, out var bodySlot);
        var edgeSlot = KernelRuntime.GetBodyRecord(bodySlot).FirstEdgeBody;
        int edge = KernelRuntime.TagOf(PoolKind.Edge, edgeSlot);
        int basisCurve = EdgeCurve(edge);
        var line = KernelRuntime.GetLineData(KernelRuntime.GetCurveByTag(basisCurve).DataIndex);
        var record = KernelRuntime.GetCurveByTag(basisCurve);
        var parm1 = record.TMin + 0.25 * (record.TMax - record.TMin);
        var parm2 = record.TMin + 0.75 * (record.TMax - record.TMin);

        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(edge));
        var sf = new PK_TRCURVE_sf_s { basis_curve = basisCurve };
        sf.t_int.value[0] = parm1;
        sf.t_int.value[1] = parm2;
        int trimmed;
        Assert.Equal(0, KernelRuntime.TrCurveCreate(&sf, &trimmed));
        Assert.Equal(0, KernelRuntime.EdgeAttachCurves(1, &edge, &trimmed));

        var document = Decode(Transmit(body));
        var trimmedNode = SingleNode(document, XtNodeTypes.TrimmedCurve);
        Assert.Equal(parm1, trimmedNode.Fields[10].Real, 14);
        Assert.Equal(parm2, trimmedNode.Fields[11].Real, 14);
        // point_1/point_2 lie exactly on the basis line at the trim parameters.
        Assert.Equal(line.LocationX + parm1 * line.AxisX, trimmedNode.Fields[8].Vector.X, 12);
        Assert.Equal(line.LocationY + parm1 * line.AxisY, trimmedNode.Fields[8].Vector.Y, 12);
        Assert.Equal(line.LocationZ + parm1 * line.AxisZ, trimmedNode.Fields[8].Vector.Z, 12);
        Assert.Equal(line.LocationX + parm2 * line.AxisX, trimmedNode.Fields[9].Vector.X, 12);
        Assert.Equal(line.LocationY + parm2 * line.AxisY, trimmedNode.Fields[9].Vector.Y, 12);
        Assert.Equal(line.LocationZ + parm2 * line.AxisZ, trimmedNode.Fields[9].Vector.Z, 12);

        // The basis line is transmitted with the body; both nodes sit in the
        // body curve chain and the trim references the line node.
        var basisNode = Deref(document, trimmedNode.Fields[7]);
        Assert.Equal(XtNodeTypes.Line, (XtNodeTypes)basisNode.Type);
        AssertInChain(document, CurveChainHead, trimmedNode.Index);
        AssertInChain(document, CurveChainHead, basisNode.Index);
    }

    [Fact]
    public void SpCurveAttachedToBlockTopEdge_TransmitsWithSupportAnd2DBCurve()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body));
        int topFace = FindPlaneFace(body, Axis.Z, location: 4);
        Assert.NotEqual(0, topFace);

        int edge = FindEdge(body, edge =>
        {
            var curve = KernelRuntime.GetCurveByTag(EdgeCurve(edge));
            if (curve.Class != CurveClass.Line) return false;
            var line = KernelRuntime.GetLineData(curve.DataIndex);
            return Math.Abs(line.LocationZ - 4) < 1e-12;
        });
        Assert.NotEqual(0, edge);
        var curve = KernelRuntime.GetCurveByTag(EdgeCurve(edge));
        var line = KernelRuntime.GetLineData(curve.DataIndex);
        int topSurface = FaceSurface(topFace);
        var plane = KernelRuntime.GetPlaneData(KernelRuntime.GetSurfaceByTag(topSurface).DataIndex);

        // Edge endpoints mapped into the plane's UV frame: the 2D B-curve
        // between them traces the 3D edge exactly through the surface.
        var (u0, v0) = PlaneUV(plane, line.LocationX + curve.TMin * line.AxisX,
            line.LocationY + curve.TMin * line.AxisY, line.LocationZ + curve.TMin * line.AxisZ);
        var (u1, v1) = PlaneUV(plane, line.LocationX + curve.TMax * line.AxisX,
            line.LocationY + curve.TMax * line.AxisY, line.LocationZ + curve.TMax * line.AxisZ);

        double[] poles = [u0, v0, u1, v1];
        double[] knots = [0, 1];
        int[] mults = [2, 2];
        fixed (double* p = poles)
        fixed (double* k = knots)
        fixed (int* m = mults)
        {
            var bSf = new PK_BCURVE_sf_s
            {
                degree = 1,
                n_vertices = 2,
                vertex_dim = 2,
                vertex = p,
                n_knots = 2,
                knot = k,
                knot_mult = m,
                form = ParasolidConstants.PK_BCURVE_form_unset_c,
                knot_type = ParasolidConstants.PK_knot_unset_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_unset_c,
            };
            int bcurve;
            Assert.Equal(0, KernelRuntime.BCurveCreate(&bSf, &bcurve));
            var spSf = new PK_SPCURVE_sf_s { surf = topSurface, curve = bcurve };
            int spcurve;
            Assert.Equal(0, KernelRuntime.SpCurveCreate(&spSf, &spcurve));
            Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(edge));
            Assert.Equal(0, KernelRuntime.EdgeAttachCurves(1, &edge, &spcurve));

            var document = Decode(Transmit(body));
            var spNode = SingleNode(document, XtNodeTypes.SpCurve);
            Assert.Equal(XtFieldKind.Pointer, spNode.Fields[7].Kind);
            Assert.Equal(XtFieldKind.Pointer, spNode.Fields[8].Kind);
            Assert.Equal(0, spNode.Fields[9].Pointer);                // original
            Assert.Equal(XtFieldKind.Empty, spNode.Fields[10].Kind);  // tolerance_to_original
            // The support surface is transmitted and chained; the 2D B-curve
            // is a floating node (owner=0, node_id=0, unchained), matching
            // Parasolid's canonical form verified against real Parasolid.
            var supportNode = Deref(document, spNode.Fields[7]);
            Assert.Equal(XtNodeTypes.Plane, (XtNodeTypes)supportNode.Type);
            AssertInChain(document, SurfaceChainHead, supportNode.Index);
            var pcurveNode = Deref(document, spNode.Fields[8]);
            Assert.Equal(XtNodeTypes.BCurve, (XtNodeTypes)pcurveNode.Type);
            Assert.Equal(0, pcurveNode.Fields[0].Integer);
            Assert.Equal(0, pcurveNode.Fields[2].Pointer);
            Assert.Equal(0, pcurveNode.Fields[3].Pointer);
            Assert.False(ChainNodes(document, CurveChainHead).Contains(pcurveNode.Index));
            AssertInChain(document, CurveChainHead, spNode.Index);
        }
    }

    [Fact]
    public void BSurfaceAttachedToBlockTopFace_TransmitsNurbsAndSurfaceData()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body));
        int topFace = FindPlaneFace(body, Axis.Z, location: 4);
        Assert.NotEqual(0, topFace);

        // Bilinear B-surface spanning the top face rectangle.
        double[] poles = new double[12];
        (double X, double Y)[] corners = { (-1, -1.5), (1, -1.5), (-1, 1.5), (1, 1.5) };
        for (var i = 0; i < 4; i++)
        {
            poles[3 * i] = corners[i].X;
            poles[3 * i + 1] = corners[i].Y;
            poles[3 * i + 2] = 4;
        }
        int bsurf = CreateBSurface(poles);
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(topFace));
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &topFace, &bsurf, &sense));

        var document = Decode(Transmit(body));
        var bsurfNode = SingleNode(document, XtNodeTypes.BSurface);
        var nurbs = Deref(document, bsurfNode.Fields[7]);
        Assert.Equal(XtNodeTypes.NurbsSurf, (XtNodeTypes)nurbs.Type);
        Assert.Equal(1, nurbs.Fields[2].Integer);      // u_degree
        Assert.Equal(1, nurbs.Fields[3].Integer);      // v_degree
        Assert.Equal(2, nurbs.Fields[4].Integer);      // n_u_vertices
        Assert.Equal(2, nurbs.Fields[5].Integer);      // n_v_vertices
        Assert.Equal(3, nurbs.Fields[14].Integer);     // vertex_dim
        var vertices = Deref(document, nurbs.Fields[15]);
        Assert.Equal(12, vertices.VariableLength);
        var surfaceData = Deref(document, bsurfNode.Fields[8]);
        Assert.Equal(XtNodeTypes.SurfaceData, (XtNodeTypes)surfaceData.Type);
        Assert.Equal(0, surfaceData.Fields[0].Vector.X);   // original_uint low
        Assert.Equal(1, surfaceData.Fields[0].Vector.Y);   // original_uint high
        Assert.Equal(1, surfaceData.Fields[4].Integer);    // self_int false encoding
        AssertInChain(document, SurfaceChainHead, bsurfNode.Index);
    }

    [Fact]
    public void SweptAttachedToBlockSideFace_TransmitsWithSection()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body));
        int sideFace = FindPlaneFace(body, Axis.X, location: 1);
        Assert.NotEqual(0, sideFace);
        int bottomEdge = FindEdge(body, edge =>
        {
            var curve = KernelRuntime.GetCurveByTag(EdgeCurve(edge));
            if (curve.Class != CurveClass.Line) return false;
            var line = KernelRuntime.GetLineData(curve.DataIndex);
            return Math.Abs(line.LocationZ) < 1e-12 && Math.Abs(line.LocationX - 1) < 1e-12
                && Math.Abs(line.AxisZ) < 1e-12;
        });
        Assert.NotEqual(0, bottomEdge);
        var line = KernelRuntime.GetLineData(KernelRuntime.GetCurveByTag(EdgeCurve(bottomEdge)).DataIndex);

        var lineSf = new PK_LINE_sf_s();
        lineSf.basis_set.location.coord[0] = line.LocationX;
        lineSf.basis_set.location.coord[1] = line.LocationY;
        lineSf.basis_set.location.coord[2] = line.LocationZ;
        lineSf.basis_set.axis.coord[0] = line.AxisX;
        lineSf.basis_set.axis.coord[1] = line.AxisY;
        lineSf.basis_set.axis.coord[2] = line.AxisZ;
        int section;
        Assert.Equal(0, KernelRuntime.LineCreate(&lineSf, &section));
        var sweptSf = new PK_SWEPT_sf_s { curve = section };
        sweptSf.direction.coord[2] = 1;
        int swept;
        Assert.Equal(0, KernelRuntime.SweptCreate(&sweptSf, &swept));
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(sideFace));
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &sideFace, &swept, &sense));

        var document = Decode(Transmit(body));
        var sweptNode = SingleNode(document, XtNodeTypes.SweptSurf);
        Assert.Equal(0, sweptNode.Fields[8].Vector.X);
        Assert.Equal(0, sweptNode.Fields[8].Vector.Y);
        Assert.Equal(1, sweptNode.Fields[8].Vector.Z);
        Assert.Equal(XtFieldKind.Empty, sweptNode.Fields[9].Kind); // scale unset
        AssertInChain(document, SurfaceChainHead, sweptNode.Index);
        var sectionNode = Deref(document, sweptNode.Fields[7]);
        Assert.Equal(XtNodeTypes.Line, (XtNodeTypes)sectionNode.Type);
        AssertInChain(document, CurveChainHead, sectionNode.Index);
    }

    [Fact]
    public void SpunAttachedToCylinderSideFace_TransmitsWithProfile()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidCyl(2, 3, null, &body));
        int sideFace = FindCylinderFace(body);
        Assert.NotEqual(0, sideFace);

        var lineSf = new PK_LINE_sf_s();
        lineSf.basis_set.location.coord[0] = 2;
        lineSf.basis_set.axis.coord[2] = 1;
        int profile;
        Assert.Equal(0, KernelRuntime.LineCreate(&lineSf, &profile));
        var spunSf = new PK_SPUN_sf_s { curve = profile };
        spunSf.axis.axis.coord[2] = 1;
        int spun;
        Assert.Equal(0, KernelRuntime.SpunCreate(&spunSf, &spun));
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(sideFace));
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &sideFace, &spun, &sense));

        var document = Decode(Transmit(body));
        var spunNode = SingleNode(document, XtNodeTypes.SpunSurf);
        Assert.Equal(0, spunNode.Fields[8].Vector.X);   // base
        Assert.Equal(1, spunNode.Fields[9].Vector.Z);   // axis
        Assert.Equal(XtFieldKind.Empty, spunNode.Fields[10].Kind); // start
        Assert.Equal(XtFieldKind.Empty, spunNode.Fields[11].Kind); // end
        Assert.Equal(XtFieldKind.Empty, spunNode.Fields[12].Kind); // start_param
        Assert.Equal(XtFieldKind.Empty, spunNode.Fields[13].Kind); // end_param
        Assert.Equal(XtFieldKind.Empty, spunNode.Fields[14].Kind); // x_axis unset
        Assert.Equal(XtFieldKind.Empty, spunNode.Fields[15].Kind); // scale
        AssertInChain(document, SurfaceChainHead, spunNode.Index);
        var profileNode = Deref(document, spunNode.Fields[7]);
        Assert.Equal(XtNodeTypes.Line, (XtNodeTypes)profileNode.Type);
        AssertInChain(document, CurveChainHead, profileNode.Index);
    }

    [Fact]
    public void OffsetAttachedToBlockTopFace_TransmitsWithBaseBSurface()
    {
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body));
        int topFace = FindPlaneFace(body, Axis.Z, location: 4);
        Assert.NotEqual(0, topFace);

        // A bilinear B-surface one unit below the top face; offsetting it by
        // +1 traces the top face plane exactly.
        double[] poles = new double[12];
        (double X, double Y)[] corners = { (-1, -1.5), (1, -1.5), (-1, 1.5), (1, 1.5) };
        for (var i = 0; i < 4; i++)
        {
            poles[3 * i] = corners[i].X;
            poles[3 * i + 1] = corners[i].Y;
            poles[3 * i + 2] = 3;
        }
        int baseBsurf = CreateBSurface(poles);
        var offsetSf = new PK_OFFSET_sf_s { underlying_surface = baseBsurf, offset_distance = 1 };
        int offset;
        Assert.Equal(0, KernelRuntime.OffsetCreate(&offsetSf, &offset));
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(topFace));
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &topFace, &offset, &sense));

        var document = Decode(Transmit(body));
        var offsetNode = SingleNode(document, XtNodeTypes.OffsetSurf);
        Assert.Equal('V', offsetNode.Fields[7].Character);
        Assert.Equal(0, offsetNode.Fields[8].Integer);               // true_offset false
        Assert.Equal(1, offsetNode.Fields[10].Real, 14);             // offset distance
        Assert.Equal(XtFieldKind.Empty, offsetNode.Fields[11].Kind); // scale unset
        AssertInChain(document, SurfaceChainHead, offsetNode.Index);
        var baseNode = Deref(document, offsetNode.Fields[9]);
        Assert.Equal(XtNodeTypes.BSurface, (XtNodeTypes)baseNode.Type);
        AssertInChain(document, SurfaceChainHead, baseNode.Index);
    }

    // ── Fixtures ─────────────────────────────────────────────────────

    private const int SurfaceChainHead = 21;
    private const int CurveChainHead = 22;

    private enum Axis { X, Y, Z }

    private static int EdgeCurve(int edge)
    {
        int curve;
        Assert.Equal(0, KernelRuntime.EdgeAskCurve(edge, &curve));
        return curve;
    }

    private static int FaceSurface(int face)
    {
        int surface;
        Assert.Equal(0, KernelRuntime.FaceAskSurf(face, &surface));
        return surface;
    }

    /// <summary>Finds the first edge whose curve satisfies the predicate.</summary>
    private static int FindEdge(int body, Func<int, bool> predicate)
    {
        int count;
        int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &edges));
        try
        {
            for (var i = 0; i < count; i++)
            {
                if (predicate(edges[i]))
                    return edges[i];
            }
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(edges)); }
        return 0;
    }

    /// <summary>Finds the axis-aligned plane face with the given outward axis
    /// and plane location.</summary>
    private static int FindPlaneFace(int body, Axis axis, double location)
    {
        int count;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        try
        {
            for (var i = 0; i < count; i++)
            {
                int surface;
                Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[i], &surface));
                var record = KernelRuntime.GetSurfaceByTag(surface);
                if (record.Class != SurfaceClass.Plane) continue;
                var plane = KernelRuntime.GetPlaneData(record.DataIndex);
                var normalComponent = axis switch
                {
                    Axis.X => plane.NormalX,
                    Axis.Y => plane.NormalY,
                    _ => plane.NormalZ,
                };
                var locationComponent = axis switch
                {
                    Axis.X => plane.LocationX,
                    Axis.Y => plane.LocationY,
                    _ => plane.LocationZ,
                };
                if (Math.Abs(Math.Abs(normalComponent) - 1) < 1e-12
                    && Math.Abs(locationComponent - location) < 1e-12)
                    return faces[i];
            }
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(faces)); }
        return 0;
    }

    private static int FindCylinderFace(int body)
    {
        int count;
        int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        try
        {
            for (var i = 0; i < count; i++)
            {
                int surface;
                Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[i], &surface));
                if (KernelRuntime.GetSurfaceByTag(surface).Class == SurfaceClass.Cylinder)
                    return faces[i];
            }
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(faces)); }
        return 0;
    }

    private static (double U, double V) PlaneUV(PlaneData plane, double px, double py, double pz)
    {
        var dx = px - plane.LocationX;
        var dy = py - plane.LocationY;
        var dz = pz - plane.LocationZ;
        // v direction is normal × ref_direction (matches the PK plane UV layout).
        var yx = plane.NormalY * plane.RefDirZ - plane.NormalZ * plane.RefDirY;
        var yy = plane.NormalZ * plane.RefDirX - plane.NormalX * plane.RefDirZ;
        var yz = plane.NormalX * plane.RefDirY - plane.NormalY * plane.RefDirX;
        return (dx * plane.RefDirX + dy * plane.RefDirY + dz * plane.RefDirZ,
                dx * yx + dy * yy + dz * yz);
    }

    private static int CreateBSurface(double[] poles)
    {
        double[] uKnots = [0, 1];
        double[] vKnots = [0, 1];
        int[] uMult = [2, 2];
        int[] vMult = [2, 2];
        fixed (double* p = poles)
        fixed (double* uk = uKnots)
        fixed (int* um = uMult)
        fixed (double* vk = vKnots)
        fixed (int* vm = vMult)
        {
            var sf = new PK_BSURF_sf_s
            {
                u_degree = 1,
                v_degree = 1,
                n_u_vertices = 2,
                n_v_vertices = 2,
                vertex_dim = 3,
                vertex = p,
                n_u_knots = uKnots.Length,
                n_v_knots = vKnots.Length,
                u_knot = uk,
                v_knot = vk,
                u_knot_mult = um,
                v_knot_mult = vm,
                form = ParasolidConstants.PK_BSURF_form_unset_c,
                u_knot_type = ParasolidConstants.PK_knot_unset_c,
                v_knot_type = ParasolidConstants.PK_knot_unset_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_unset_c,
                convexity = ParasolidConstants.PK_convexity_unset_c,
            };
            int surface;
            Assert.Equal(0, KernelRuntime.BSurfCreate(&sf, &surface));
            return surface;
        }
    }

    // ── Transmit + document helpers ──────────────────────────────────

    private static string Transmit(int body)
    {
        var parts = stackalloc int[1] { body };
        var options = new PK_PART_transmit_o_s
        {
            o_t_version = 4,
            transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            transmit_version = 371,
            transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
        };
        var block = new PK_MEMORY_block_s();
        Assert.Equal(0, KernelRuntime.PartTransmitB(1, parts, &options, &block));
        try
        {
            return System.Text.Encoding.ASCII.GetString(block.bytes, checked((int)block.n_bytes));
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryBlockFree(&block)); }
    }

    private static XtDocument Decode(string text) => XtText.DecodeDocument(text);

    private static XtNode SingleNode(XtDocument document, XtNodeTypes type)
    {
        var matches = document.Nodes.Where(n => n.Type == (int)type).ToList();
        Assert.Single(matches);
        return matches[0];
    }

    private static XtNode Deref(XtDocument document, XtFieldValue field)
    {
        Assert.Equal(XtFieldKind.Pointer, field.Kind);
        Assert.NotEqual(0, field.Pointer);
        return document.Nodes.Single(n => n.Index == field.Pointer);
    }

    /// <summary>Walks the body geometry chain whose head lives at the given
    /// body field index (21 = surfaces, 22 = curves) and returns node indexes.</summary>
    private static HashSet<int> ChainNodes(XtDocument document, int headFieldIndex)
    {
        var body = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.Body);
        var visited = new HashSet<int>();
        var current = body.Fields[headFieldIndex].Pointer;
        while (current != 0 && visited.Add(current))
            current = document.Nodes.Single(n => n.Index == current).Fields[3].Pointer;
        return visited;
    }

    private static void AssertInChain(XtDocument document, int headFieldIndex, int nodeIndex)
    {
        Assert.NotEqual(0, nodeIndex);
        Assert.Contains(nodeIndex, ChainNodes(document, headFieldIndex));
    }
}
