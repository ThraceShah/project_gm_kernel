using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IcurveBSurfaceRuntimeTests : IDisposable
{
    public IcurveBSurfaceRuntimeTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose()
    {
        KernelRuntime.InjectAllocationFailure(-1);
        KernelRuntime.SessionStop();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void PublicCurveEval_BSurfaceSupports_ReturnOriginalParameterD0D1D2(
        bool swapped, bool twoBSurfaces, bool rational)
    {
        var curve = CreateCurve(swapped, twoBSurfaces, rational, out _, out _);
        AssertParabola(curve, 0.25);
        AssertParabola(curve, 0.65);
        // A repeated public request exercises the prepared-view/cache path.
        AssertParabola(curve, 0.25);
    }

    [Theory]
    [InlineData(0, (int)AlgorithmStatus.Unsupported)]
    [InlineData(1, (int)AlgorithmStatus.Unsupported)]
    [InlineData(2, (int)AlgorithmStatus.InvalidInput)]
    [InlineData(3, (int)AlgorithmStatus.InvalidInput)]
    public void Prepare_RequiresFiniteConsistentInDomainBSurfaceChartUv(int scenario, int expected)
    {
        var surface = CreatePatch(false, false);
        var plane = CreatePlane();
        double[] uv = scenario switch
        {
            0 => [],
            1 => [double.NaN, double.NaN, 1, 0.5],
            2 => [0.1, 0.5, 1, 0.5],
            _ => [0, 0.5, 1.1, 0.5],
        };
        var type = scenario == 0 ? IntersectionUvType.None : IntersectionUvType.First;
        var input = Definition(surface, plane, type, uv);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var data, out _, out _));
        Assert.Equal((AlgorithmStatus)expected, KernelRuntime.TryBindICurveEntity(data, out var curve));
        Assert.Equal(0, curve);
        KernelRuntime.FreeICurveData(data);
        Assert.Equal(0, KernelRuntime.Curves.AliveCount);
        Assert.Equal(0, KernelRuntime.ICurveDataPool.AliveCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeletingRuntimeCurve_PreservesBorrowedSupportSurfaces(bool marked)
    {
        var curve = CreateCurve(false, false, false, out var surface0, out var surface1);
        int mark=0;if(marked)Assert.Equal(0,KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &curve));
        Assert.False(KernelRuntime.IsValidTag(curve));
        Assert.True(KernelRuntime.GetSurfaceSlotByTag(surface0) >= 0);
        Assert.True(KernelRuntime.GetSurfaceSlotByTag(surface1) >= 0);
        Assert.Equal(2, KernelRuntime.Surfaces.AliveCount);
        if(marked){Assert.Equal(0,KernelRuntime.MarkGoto(mark));Assert.True(KernelRuntime.IsValidTag(curve));Assert.Equal(0,KernelRuntime.EntityDelete(1,&curve));}
        Assert.Equal(0, KernelRuntime.ICurveDataPool.AliveCount);
    }

    [Fact]
    public void L3_ExplicitP2AndP4KeepIndependentExactSamples()
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        var record = KernelRuntime.GetCurveByTag(curve);
        var identity = new GeometryIdentity(curve, record.Header.Generation);
        var ownsScratch = KernelRuntime.EnsureCommandScratch();
        try
        {
            Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryPrepareICurveView(in record, out var view, out _));
            var t = (0.25 + 0.25 * 0.25) / Math.Sqrt(2);
            Span<KernelVector3> output = stackalloc KernelVector3[3];
            Span<KernelVector3> expected = stackalloc KernelVector3[3];
            ICurveConstraintPlan[] plans = [ICurveConstraintPlan.P2, ICurveConstraintPlan.P4,
                ICurveConstraintPlan.P4, ICurveConstraintPlan.P2];
            for (var i = 0; i < plans.Length; i++)
            {
                Assert.Equal(AlgorithmStatus.Success, KernelRuntime.EvaluateICurveThroughL3(
                    in identity, in view, t, 2, plans[i], output, out var report));
                Assert.Equal(plans[i], report.Plan);
                if (i < 2) Assert.NotEqual(CacheHitKind.Exact, report.CacheHit);
                else Assert.Equal(CacheHitKind.Exact, report.CacheHit);
                if (i == 0) output.CopyTo(expected);
                else for (var order = 0; order < 3; order++)
                {
                    Assert.InRange(Math.Abs(output[order].X - expected[order].X), 0, 2e-8);
                    Assert.InRange(Math.Abs(output[order].Y - expected[order].Y), 0, 2e-8);
                    Assert.InRange(Math.Abs(output[order].Z - expected[order].Z), 0, 2e-8);
                }
            }
        }
        finally { if (ownsScratch) KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session); }
    }

    [Fact]
    public void PublicL3_AfterPrefillIsFull_LatestRootRetainsItsUvWitness()
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        for (var i = 1; i <= 20; i++) AssertParabola(curve, i / 22.0);
        const double x = 20.0 / 22.0;
        AssertParabola(curve, x);
        Assert.Equal(CacheHitKind.Exact, KernelRuntime.LastICurveEvalReport.CacheHit);
        var record = KernelRuntime.GetCurveByTag(curve);
        var identity = new GeometryIdentity(curve, record.Header.Generation);
        var t = (x + x * x) / Math.Sqrt(2);
        Assert.True(GeometryEvaluationCache.TryGetExact(in identity, t,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, 2, double.MaxValue, out var sample));
        Assert.Equal(SampleWitness.HasUv0 | SampleWitness.HasUv1,
            sample.Witness.Flags & (SampleWitness.HasUv0 | SampleWitness.HasUv1));
        Assert.True(double.IsFinite(sample.Witness.U0) && double.IsFinite(sample.Witness.V0)
            && double.IsFinite(sample.Witness.U1) && double.IsFinite(sample.Witness.V1));
    }

    [Fact]
    public void RealParasolidBSurfaceFixture_DecodesSchemaClassificationNumbers()
    {
        var document = RealBSurfaceFixture(out var node);
        var nurbs = Assert.Single(document.Nodes, value => value.Index == node.Fields[7].Pointer);
        // These are the original real-ParaSolid fixture values, not output
        // produced by this implementation: SCH_surface_form=arbitrary(2),
        // SCH_knot_type=piecewise_bezier(5) in both directions.
        Assert.Equal(2L, nurbs.Fields[13].Integer);
        Assert.Equal(5L, nurbs.Fields[6].Integer);
        Assert.Equal(5L, nurbs.Fields[7].Integer);
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.TryMaterializeICurveSupportFromXt(document, node.Index, out var surface));
        var data = KernelRuntime.BSurfaceDataStore[KernelRuntime.GetSurfaceByTag(surface).DataIndex];
        Assert.Equal(ParasolidConstants.PK_BSURF_form_arbitrary_c, data.Form);
        Assert.Equal(ParasolidConstants.PK_knot_piecewise_bezier_c, data.UKnotType);
        Assert.Equal(ParasolidConstants.PK_knot_piecewise_bezier_c, data.VKnotType);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &surface));
        Assert.Equal(before, PoolCounts());
    }

    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 7)]
    [InlineData(13, 0)]
    [InlineData(13, 12)]
    public void BSurfaceMaterializer_RejectsUnknownSchemaClassification(int field, int value)
    {
        var document = RealBSurfaceFixture(out var node);
        var nurbs = Assert.Single(document.Nodes, item => item.Index == node.Fields[7].Pointer);
        nurbs.Fields[field] = XtFieldValue.Unsigned(value);
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.InvalidInput,
            KernelRuntime.TryMaterializeICurveSupportFromXt(document, node.Index, out var surface));
        Assert.Equal(0, surface);
        Assert.Equal(before, PoolCounts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BSurfaceMaterializer_DoesNotMistakeChangedOrMalformedExtensionForOriginalBox(int scenario)
    {
        var document = RealBSurfaceFixture(out var node);
        var data = Assert.Single(document.Nodes, item => item.Index == node.Fields[8].Pointer);
        if (scenario == 0) data.Fields[2] = XtFieldValue.IntervalValue(-1, 1);
        else if (scenario == 1)
        {
            data.Fields[0] = XtFieldValue.RealValue(0);
            data.Fields[2] = XtFieldValue.RealValue(0);
        }
        else
        {
            data.Fields[0] = XtFieldValue.IntervalValue(0, double.PositiveInfinity);
            data.Fields[2] = XtFieldValue.IntervalValue(0, double.PositiveInfinity);
        }
        Assert.Equal(AlgorithmStatus.Unsupported,
            KernelRuntime.TryMaterializeICurveSupportFromXt(document, node.Index, out var surface));
        Assert.Equal(0, surface);
        Assert.Equal(0, KernelRuntime.Surfaces.AliveCount);
    }

    [Fact]
    public void BSurfaceMaterializer_OldVersionCheckMarkerIsUnsupported()
    {
        var document = RealBSurfaceFixture(out var node);
        var data = Assert.Single(document.Nodes, item => item.Index == node.Fields[8].Pointer);
        data.Fields[4] = XtFieldValue.Unsigned(4); // checked_ok_in_old_version
        Assert.Equal(AlgorithmStatus.Unsupported,
            KernelRuntime.TryMaterializeICurveSupportFromXt(document, node.Index, out var surface));
        Assert.Equal(0, surface);
    }

    [Fact]
    public void BSurfaceWriter_UsesSchemaEnumsAndLeavesUnrepresentableClassificationUnset()
    {
        var curve = CreateCurve(false, false, false, out var surface, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        var source = XtText.DecodeDocument(Transmit(body));
        var nurbs = Assert.Single(source.Nodes, node => node.Type == (int)XtNodeTypes.NurbsSurf);
        Assert.Equal(2L, nurbs.Fields[13].Integer); // arbitrary
        Assert.Equal(6L, nurbs.Fields[6].Integer);  // bezier_ends
        Assert.Equal(6L, nurbs.Fields[7].Integer);
        ref var data = ref KernelRuntime.BSurfaceDataStore[KernelRuntime.GetSurfaceByTag(surface).DataIndex];
        data.Form = ParasolidConstants.PK_BSURF_form_gen_cone_c;
        data.UKnotType = ParasolidConstants.PK_knot_smooth_seam_c;
        var rewritten = XtText.DecodeDocument(Transmit(body));
        var rewrittenNurbs = Assert.Single(rewritten.Nodes, node => node.Type == (int)XtNodeTypes.NurbsSurf);
        Assert.Equal(1L, rewrittenNurbs.Fields[13].Integer); // no gen_cone schema value
        Assert.Equal(1L, rewrittenNurbs.Fields[6].Integer);  // no smooth_seam schema value
        Assert.Equal(6L, rewrittenNurbs.Fields[7].Integer);
        var poles = Assert.Single(source.Nodes, node => node.Type == (int)XtNodeTypes.BSplineVertices);
        var rewrittenPoles = Assert.Single(rewritten.Nodes, node => node.Type == (int)XtNodeTypes.BSplineVertices);
        Assert.Equal(poles.Fields.Select(field => field.Real), rewrittenPoles.Fields.Select(field => field.Real));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PublicReceive_WireIsEvaluableAndReserializesBSurfacesAndNullUv(bool swapped, bool twoBSurfaces)
    {
        var curve = CreateCurve(swapped, twoBSurfaces, true, out var surface0, out var surface1);
        var sourceSurface = KernelRuntime.GetSurfaceSlotByTag(surface0);
        KernelRuntime.Surfaces[sourceSurface].Sense = ParasolidConstants.PK_TOPOL_sense_negative_c;
        var secondSurface = KernelRuntime.GetSurfaceSlotByTag(surface1);
        KernelRuntime.Surfaces[secondSurface].Sense = KernelRuntime.Surfaces[secondSurface].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? ParasolidConstants.PK_TOPOL_sense_positive_c : ParasolidConstants.PK_TOPOL_sense_negative_c;
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var sourceBody));
        var sourceText = Transmit(sourceBody);
        var sourceDocument = XtText.DecodeDocument(sourceText);
        var sourceNode = Assert.Single(sourceDocument.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        Assert.Equal(2L, sourceNode.Fields[17].Integer);
        Assert.Equal(2, sourceDocument.Nodes.Count(node => node.Type == (int)XtNodeTypes.Halfedge));
        var before = PoolCounts();

        var received = Receive(sourceText);
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var bodySlot));
        Assert.False(KernelRuntime.Xt.IsOpaque(bodySlot));
        var body = KernelRuntime.GetBodyRecord(bodySlot);
        Assert.Equal(ParasolidConstants.PK_BODY_type_wire_c, body.BodyType);
        Assert.Equal(1, body.EdgeCountBody);
        Assert.Equal(2, body.VertexCountBody);
        Assert.Equal(0, body.FaceCountBody);
        var receivedCurve = KernelRuntime.GetEdgeRecord(body.FirstEdgeBody).CurveTag;
        Assert.Equal(CurveClass.ICurve, KernelRuntime.GetCurveByTag(receivedCurve).Class);
        AssertParabola(receivedCurve, 0.35);
        var receivedData = KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(receivedCurve).DataIndex];
        Assert.Equal((byte)1, receivedData.OwnsSupportSurfaces);
        Assert.Equal(ParasolidConstants.PK_TOPOL_sense_negative_c,
            KernelRuntime.GetSurfaceByTag(receivedData.Surface0Tag).Sense);

        // Exercise the geometry writer independently of the preserved input
        // document, then exercise public retransmit as well.
        Assert.Equal(0, XtWriter.WriteText([received], 0, out var geometryText));
        var geometry = XtText.DecodeDocument(geometryText);
        Assert.Equal(twoBSurfaces ? 2 : 1, geometry.Nodes.Count(node => node.Type == (int)XtNodeTypes.BSurface));
        var sourceUv = Assert.Single(sourceDocument.Nodes, node => node.Type == (int)XtNodeTypes.IntersectionData);
        var receivedUv = Assert.Single(geometry.Nodes, node => node.Type == (int)XtNodeTypes.IntersectionData);
        Assert.Equal(sourceUv.Fields.Length, receivedUv.Fields.Length);
        for (var i = 0; i < sourceUv.Fields.Length; i++)
        {
            Assert.Equal(sourceUv.Fields[i].Kind, receivedUv.Fields[i].Kind);
            if (sourceUv.Fields[i].Kind == XtFieldKind.Real)
                Assert.Equal(sourceUv.Fields[i].Real, receivedUv.Fields[i].Real);
        }
        var chart = Assert.Single(geometry.Nodes, node => node.Type == (int)XtNodeTypes.Chart);
        Assert.Equal(XtFieldKind.Real, chart.Fields[5].Kind);
        Assert.Equal(0.125, chart.Fields[5].Real);
        Assert.Equal(XtFieldKind.Empty, chart.Fields[6].Kind);
        Assert.Contains(XtText.DecodeDocument(Transmit(received)).Nodes,
            node => node.Type == (int)XtNodeTypes.BSurface);

        Assert.Equal(0, KernelRuntime.EntityDelete(1, &received));
        Assert.Equal(before, PoolCounts());
    }

    [Fact]
    public void MaterializeInvalidChartUv_RollsBackCreatedSupports()
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        var document = XtText.DecodeDocument(Transmit(body));
        var uv = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.IntersectionData);
        // The first field is uv_type, followed by the chart values.
        uv.Fields[1] = XtFieldValue.RealValue(-0.1);
        var intersection = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.Intersection);
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.InvalidInput,
            KernelRuntime.TryMaterializeICurveFromXt(document, intersection.Index, out var imported, out _));
        Assert.Equal(0, imported);
        Assert.Equal(before, PoolCounts());
    }

    [Fact]
    public void PublicReceive_SecondInvalidWireRollsBackFirstMaterializedPart()
    {
        var firstCurve = CreateCurve(false, false, false, out _, out _);
        var secondCurve = CreateCurve(false, false, false, out _, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(firstCurve, out var firstBody));
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(secondCurve, out var secondBody));
        Assert.Equal(0, XtWriter.WriteText([firstBody, secondBody], 0, out var text));
        var document = XtText.DecodeDocument(text);
        var intersection = document.Nodes.Last(node => node.Type == (int)XtNodeTypes.Intersection);
        var uv = Assert.Single(document.Nodes, node => node.Index == intersection.Fields[^1].Pointer);
        uv.Fields[1] = XtFieldValue.RealValue(-0.1);
        var bytes = Encoding.ASCII.GetBytes(XtText.Encode(document));
        var before = PoolCounts();
        fixed (byte* pointer = bytes)
        {
            var block = new PK_MEMORY_block_s { bytes = pointer, n_bytes = (nuint)bytes.Length };
            var options = new PK_PART_receive_o_s
            {
                o_t_version = 1, transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            };
            int count = 0;
            int* parts = null;
            Assert.Equal(ParasolidConstants.PK_ERROR_corrupt_file,
                KernelRuntime.PartReceiveB(block, &options, &count, &parts));
            Assert.Equal(0, count);
            Assert.True(parts == null);
        }
        Assert.Equal(before, PoolCounts());
        AssertParabola(firstCurve, 0.25);
        AssertParabola(secondCurve, 0.65);
    }

    [Theory]
    [InlineData(0, (int)AlgorithmStatus.Unsupported)]
    [InlineData(1, (int)AlgorithmStatus.Unsupported)]
    [InlineData(2, (int)AlgorithmStatus.InvalidInput)]
    public void WireMaterializer_ClosedSubsetIsUnsupportedButDanglingVertexIsInvalid(int scenario, int expected)
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        var document = XtText.DecodeDocument(Transmit(body));
        var fins = document.Nodes.Where(node => node.Type == (int)XtNodeTypes.Halfedge).ToArray();
        Assert.Equal(2, fins.Length);
        if (scenario == 0)
            fins[0].Fields[4] = fins[1].Fields[4] = XtFieldValue.Ptr(0);
        else if (scenario == 1)
            fins[0].Fields[4] = fins[1].Fields[4];
        else
            fins[0].Fields[4] = XtFieldValue.Ptr(document.Nodes.Max(node => node.Index) + 1);
        var root = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        var before = PoolCounts();
        Assert.Equal((AlgorithmStatus)expected,
            KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out var imported));
        Assert.Equal(0, imported);
        Assert.Equal(before, PoolCounts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WireMaterializer_RejectsInconsistentTopologyBacklinks(int scenario)
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        var document = XtText.DecodeDocument(Transmit(body));
        var edge = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.Edge);
        var fins = document.Nodes.Where(node => node.Type == (int)XtNodeTypes.Halfedge).ToArray();
        var vertex = document.Nodes.First(node => node.Type == (int)XtNodeTypes.Vertex);
        if (scenario == 0) edge.Fields[9] = XtFieldValue.Ptr(0);
        else if (scenario == 1) vertex.Fields[7] = XtFieldValue.Ptr(0);
        else fins[0].Fields[8] = XtFieldValue.Ptr(fins[1].Index);
        var root = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.InvalidInput,
            KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out var imported));
        Assert.Equal(0, imported);
        Assert.Equal(before, PoolCounts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(300)]
    [InlineData(331)]
    public void PublicReceive_VertexlessClosedCircleRetainsOpaqueFallback(int transmitVersion)
    {
        var planeSf = new PK_PLANE_sf_s();
        planeSf.basis_set.axis.coord[2] = 1;
        planeSf.basis_set.ref_direction.coord[0] = 1;
        var cylinderSf = new PK_CYL_sf_s { radius = 1 };
        cylinderSf.basis_set = planeSf.basis_set;
        int plane = 0, cylinder = 0;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&planeSf, &plane));
        Assert.Equal(0, KernelRuntime.CylCreate(&cylinderSf, &cylinder));
        double[] chart = [1, 0, 0, 0, 1, 0, -1, 0, 0, 0, -1, 0, 1, 0, 0];
        var definition = new IcurveDecodeInput
        {
            Surface0Tag = plane, Surface1Tag = cylinder, BaseScale = 1, ChartCount = 5,
            ChartHvecs = chart, UvType = IntersectionUvType.None,
            Start = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = chart.AsSpan(0, 3) },
            End = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = chart.AsSpan(12, 3) },
            Sense = ParasolidConstants.PK_TOPOL_sense_positive_c,
        };
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(definition, out var data, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(data, out var curve));
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        var original = XtText.DecodeDocument(Transmit(body));
        var root = Assert.Single(original.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        root.Fields[18] = XtFieldValue.Unsigned(2); // nom_geom_state=on; this occupies BODY[17] in V30–32
        root.Fields[23] = XtFieldValue.Ptr(0);
        root.Fields[28] = XtFieldValue.Ptr(0);
        foreach (var fin in original.Nodes.Where(node => node.Type == (int)XtNodeTypes.Halfedge))
        {
            fin.Fields[4] = XtFieldValue.Ptr(0);
            fin.Fields[8] = XtFieldValue.Ptr(0);
        }
        var closed = WithNodes(original, original.Nodes.Where(node => node.Type != (int)XtNodeTypes.Vertex
            && node.Type != (int)XtNodeTypes.Point).ToArray());
        string text;
        if (transmitVersion == 0) text = XtText.Encode(closed);
        else Assert.True(XtText.TryEncodeForTransmitVersion(closed, transmitVersion, out text));
        var received = Receive(text);
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var bodySlot));
        Assert.True(KernelRuntime.Xt.IsOpaque(bodySlot));
        Assert.DoesNotContain(XtText.DecodeDocument(Transmit(received)).Nodes, node => node.Type == (int)XtNodeTypes.Vertex);
    }

    [Fact]
    public void PublicReceive_WireWithConstructionPointRemainsOpaque()
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        var original = XtText.DecodeDocument(Transmit(body));
        var root = Assert.Single(original.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        var template = original.Nodes.First(node => node.Type == (int)XtNodeTypes.Point);
        var index = original.Nodes.Max(node => node.Index) + 1;
        var fields = template.Fields.ToArray();
        fields[0] = XtFieldValue.Int(root.Fields[0].Integer + 1);
        fields[2] = XtFieldValue.Ptr(root.Index);
        fields[3] = fields[4] = XtFieldValue.Ptr(0);
        fields[5] = XtFieldValue.Vec(2, 3, 4);
        var point = new XtNode { Type = (int)XtNodeTypes.Point, Index = index, Fields = fields };
        root.Fields[0] = fields[0];
        root.Fields[6] = XtFieldValue.Ptr(index);
        var document = WithNodes(original, [.. original.Nodes, point]);
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.Unsupported,
            KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out var imported));
        Assert.Equal(0, imported);
        Assert.Equal(before, PoolCounts());
        var received = Receive(XtText.Encode(document));
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var slot));
        Assert.True(KernelRuntime.Xt.IsOpaque(slot));
        var retransmitted = XtText.DecodeDocument(Transmit(received));
        var transmittedRoot = Assert.Single(retransmitted.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        var retained = Assert.Single(retransmitted.Nodes, node => node.Index == transmittedRoot.Fields[6].Pointer);
        Assert.Equal((int)XtNodeTypes.Point, retained.Type);
        Assert.Equal(2, retained.Fields[5].Vector.X);
        Assert.Equal(3, retained.Fields[5].Vector.Y);
        Assert.Equal(4, retained.Fields[5].Vector.Z);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(17)]
    [InlineData(23)]
    public void WireAllocationFailure_PreservesCallerCurveAndLeavesNoDummyFins(int fault)
    {
        var curve = CreateCurve(false, false, false, out _, out _);
        var before = PoolCounts();
        KernelRuntime.InjectAllocationFailure(fault);
        var error = KernelRuntime.CreateICurveWireBody(curve, out var body);
        KernelRuntime.InjectAllocationFailure(-1);
        if (error != 0)
        {
            Assert.Equal(0, body);
            Assert.Equal(before, PoolCounts());
            AssertParabola(curve, 0.25);
        }
        else
        {
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            Assert.Equal(0, KernelRuntime.Fins.AliveCount);
            Assert.Equal(0, KernelRuntime.Bodies.AliveCount);
            Assert.Equal(0, KernelRuntime.Points.AliveCount);
            Assert.Equal(0, KernelRuntime.Curves.AliveCount);
            Assert.Equal(2, KernelRuntime.Surfaces.AliveCount);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(35)]
    public void WireMaterializeAllocationFailure_ReleasesImportedGeometryAndTopology(int fault)
    {
        var curve = CreateCurve(false, false, true, out _, out _);
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var source));
        var document = XtText.DecodeDocument(Transmit(source));
        var root = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.Body);
        var before = PoolCounts();
        KernelRuntime.InjectAllocationFailure(fault);
        var status = KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out var body);
        KernelRuntime.InjectAllocationFailure(-1);
        if (status == AlgorithmStatus.Success)
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        else
        {
            Assert.Equal(AlgorithmStatus.WorkspaceTooSmall, status);
            Assert.Equal(0, body);
        }
        Assert.Equal(before, PoolCounts());
    }

    private static int[] PoolCounts() =>
    [
        KernelRuntime.Bodies.AliveCount, KernelRuntime.Edges.AliveCount, KernelRuntime.Fins.AliveCount,
        KernelRuntime.Vertices.AliveCount, KernelRuntime.Points.AliveCount, KernelRuntime.Curves.AliveCount,
        KernelRuntime.ICurveDataPool.AliveCount, KernelRuntime.Surfaces.AliveCount,
        KernelRuntime.BSurfaceDataStore.AliveCount, KernelRuntime.PlaneDataPool.AliveCount,
        KernelRuntime.State.Session->Tags.LiveOrKeptCount,
    ];

    private static XtDocument RealBSurfaceFixture(out XtNode surface)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "blend.fxf.bsurf-plane.rolling-ball", "model.x_t");
        var document = XtText.DecodeDocument(File.ReadAllText(path));
        var nurbs = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.NurbsSurf && node.Index == 39);
        surface = Assert.Single(document.Nodes, node => node.Type == (int)XtNodeTypes.BSurface && node.Fields[7].Pointer == nurbs.Index);
        return document;
    }

    private static XtDocument WithNodes(XtDocument original, XtNode[] nodes) => new()
    {
        PhysicalHeader = original.PhysicalHeader,
        VersionText = original.VersionText, HeaderSchemaIdentity = original.HeaderSchemaIdentity,
        Schema = original.Schema, BaseSchema = original.BaseSchema,
        EmbeddedMaxNodeType = original.EmbeddedMaxNodeType, UserFieldSize = original.UserFieldSize,
        Nodes = nodes,
    };

    private static int CreateCurve(bool swapped, bool twoBSurfaces, bool rational, out int surface0, out int surface1)
    {
        var curved = CreatePatch(false, rational);
        var other = twoBSurfaces ? CreatePatch(true, rational) : CreatePlane();
        surface0 = swapped ? other : curved;
        surface1 = swapped ? curved : other;
        // The ordered normal cross-product must follow the increasing-u
        // chart. The analytic plane has +Y normal, the B-surface plane -Y.
        if (swapped == twoBSurfaces)
            KernelRuntime.Surfaces[KernelRuntime.GetSurfaceSlotByTag(surface1)].Sense
                = ParasolidConstants.PK_TOPOL_sense_negative_c;
        var a0 = twoBSurfaces ? 0.0 : double.NaN;
        var a1 = twoBSurfaces ? 1.0 : double.NaN;
        double[] uv = swapped
            ? [a0, a0, 0, 0.5, a1, a1, 1, 0.5]
            : [0, 0.5, a0, a0, 1, 0.5, a1, a1];
        var input = Definition(surface0, surface1, IntersectionUvType.Both, uv);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var data, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(data, out var curve));
        return curve;
    }

    private static IcurveDecodeInput Definition(int surface0, int surface1, IntersectionUvType type, ReadOnlySpan<double> uv)
        => new()
        {
            Surface0Tag = surface0, Surface1Tag = surface1,
            BaseParameter = 0, BaseScale = 1,
            ChartCount = 2, ChartHvecs = new double[] { 0, 0, 0, 1, 0, 1 },
            Start = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = new double[] { 0, 0, 0 } },
            End = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = new double[] { 1, 0, 1 } },
            UvType = type, UvValues = uv,
            ChordalError = 1e-5, AngularError = 1e-5,
            ParameterError = 0.125, ParameterErrorProvided = 1,
            Sense = ParasolidConstants.PK_TOPOL_sense_positive_c,
        };

    private static int CreatePlane()
    {
        var plane = new PK_PLANE_sf_s();
        plane.basis_set.axis.coord[1] = 1;
        plane.basis_set.ref_direction.coord[0] = 1;
        int tag = 0;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&plane, &tag));
        return tag;
    }

    private static int CreatePatch(bool plane, bool rational)
    {
        // Curved patch S(u,v)=(u,2v-1,u²); plane patch S(u,v)=(u,0,v).
        double[] xyz = plane
            ? [0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 1]
            : [0, -1, 0, 0, 1, 0, 0.5, -1, 0, 0.5, 1, 0, 1, -1, 1, 1, 1, 1];
        var dimension = rational ? 4 : 3;
        var vertices = new double[xyz.Length / 3 * dimension];
        for (var i = 0; i < xyz.Length / 3; i++)
        {
            var weight = rational ? 3.0 : 1.0;
            for (var j = 0; j < 3; j++) vertices[i * dimension + j] = xyz[i * 3 + j] * weight;
            if (rational) vertices[i * dimension + 3] = weight;
        }
        double[] knots = [0, 1];
        int[] uMults = plane ? [2, 2] : [3, 3];
        int[] vMults = [2, 2];
        fixed (double* poles = vertices, knotValues = knots)
        fixed (int* uMult = uMults, vMult = vMults)
        {
            var definition = new PK_BSURF_sf_s
            {
                u_degree = plane ? 1 : 2, v_degree = 1, n_u_vertices = plane ? 2 : 3, n_v_vertices = 2,
                vertex_dim = dimension, is_rational = rational ? (byte)1 : (byte)0, vertex = poles,
                n_u_knots = 2, n_v_knots = 2, u_knot = knotValues, v_knot = knotValues,
                u_knot_mult = uMult, v_knot_mult = vMult,
                u_knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
                v_knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
                form = ParasolidConstants.PK_BSURF_form_arbitrary_c,
                self_intersecting = ParasolidConstants.PK_self_intersect_false_c,
                convexity = ParasolidConstants.PK_convexity_unset_c,
            };
            int tag = 0;
            Assert.Equal(0, KernelRuntime.BSurfCreate(&definition, &tag));
            return tag;
        }
    }

    private static void AssertParabola(int curve, double x)
    {
        // Original chart plane is (x+z)/sqrt(2)=t, so t=(x+x²)/sqrt(2).
        var t = (x + x * x) / Math.Sqrt(2);
        var firstX = Math.Sqrt(2) / (1 + 2 * x);
        var secondX = -4 / Math.Pow(1 + 2 * x, 3);
        PK_VECTOR_s* jet = stackalloc PK_VECTOR_s[3];
        Assert.Equal(0, KernelRuntime.CurveEval(curve, t, 2, jet));
        AssertVector(jet[0], x, 0, x * x);
        AssertVector(jet[1], firstX, 0, 2 * x * firstX);
        AssertVector(jet[2], secondX, 0, 2 * firstX * firstX + 2 * x * secondX);
    }

    private static void AssertVector(PK_VECTOR_s actual, double x, double y, double z)
    {
        Assert.InRange(Math.Abs(actual.coord[0] - x), 0, 2e-8);
        Assert.InRange(Math.Abs(actual.coord[1] - y), 0, 2e-8);
        Assert.InRange(Math.Abs(actual.coord[2] - z), 0, 2e-8);
    }

    private static string Transmit(int body)
    {
        var options = new PK_PART_transmit_o_s { o_t_version = 1, transmit_format = ParasolidConstants.PK_transmit_format_text_c };
        var block = new PK_MEMORY_block_s();
        Assert.Equal(0, KernelRuntime.PartTransmitB(1, &body, &options, &block));
        try { return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(block.bytes, (int)block.n_bytes)); }
        finally { Assert.Equal(0, KernelRuntime.MemoryBlockFree(&block)); }
    }

    private static int Receive(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* data = bytes)
        {
            var block = new PK_MEMORY_block_s { bytes = data, n_bytes = (nuint)bytes.Length };
            var options = new PK_PART_receive_o_s
            {
                o_t_version = 1, receive_user_fields = 1,
                transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            };
            int count = 0;
            int* parts = null;
            Assert.Equal(0, KernelRuntime.PartReceiveB(block, &options, &count, &parts));
            try { Assert.Equal(1, count); return parts[0]; }
            finally { Assert.Equal(0, KernelRuntime.MemoryFree(parts)); }
        }
    }
}
