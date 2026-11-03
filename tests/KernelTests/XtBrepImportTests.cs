using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class XtBrepImportTests : IDisposable
{
    public XtBrepImportTests()
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
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Receive_PreservesActualPosedGeometryAndFreshRuntimeRoundtrip(int primitive)
    {
        var before = Counts();
        var source = CreatePosed(primitive);
        if (primitive == 1) AssertPlaneBasesAreUnit(source);
        var expected = Samples(source);
        var sourceTopology = TopologyCounts(source);
        Assert.Equal(0, Receive(Transmit(source), out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var slot));
        Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        Assert.Equal(sourceTopology, TopologyCounts(received));
        AssertSamples(expected, Samples(received));
        KernelRuntime.Xt.Drop(slot);
        Assert.Equal(0, Receive(Transmit(received), out var fresh));
        AssertSamples(expected, Samples(fresh));
        Assert.Equal(sourceTopology, TopologyCounts(fresh));
        int* bodies = stackalloc int[3] { source, received, fresh };
        Assert.Equal(0, KernelRuntime.EntityDelete(3, bodies));
        Assert.Equal(before, Counts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Receive_RejectsInconsistentBrepWithoutPublishingPartialRecords(int malformed)
    {
        var source = CreatePosed(0);
        var document = XtText.DecodeDocument(Transmit(source));
        var face = document.Nodes.First(n => n.Type == (int)XtNodeTypes.Face);
        var fin = document.Nodes.First(n => n.Type == (int)XtNodeTypes.Halfedge);
        var vertex = document.Nodes.First(n => n.Type == (int)XtNodeTypes.Vertex);
        var edge = document.Nodes.First(n => n.Type == (int)XtNodeTypes.Edge);
        switch (malformed)
        {
            case 0: face.Fields[7] = edge.Fields[6]; break;
            case 1: fin.Fields[5] = XtFieldValue.Ptr(0); break;
            case 2: vertex.Fields[7] = XtFieldValue.Ptr(face.Index); break;
            default: fin.Fields[3] = XtFieldValue.Ptr(fin.Index); break;
        }
        var before = Counts();
        Assert.Equal(ParasolidConstants.PK_ERROR_corrupt_file, Receive(XtText.Encode(document), out var result));
        Assert.Equal(0, result);
        Assert.Equal(before, Counts());
    }

    [Fact]
    public void Receive_FinCurveWithWrongTopologicalOwnerIsRejected()
    {
        var source = CreatePosed(0);
        var document = XtText.DecodeDocument(Transmit(source));
        var fin = document.Nodes.First(n => n.Type == (int)XtNodeTypes.Halfedge);
        var edge = document.Nodes.Single(n => n.Index == fin.Fields[6].Pointer);
        // A curve owned by an EDGE cannot simultaneously be owned by a fin.
        fin.Fields[7] = edge.Fields[6];
        var text = XtText.Encode(document);
        var before = Counts();
        Assert.Equal(ParasolidConstants.PK_ERROR_corrupt_file, Receive(text, out var received));
        Assert.Equal(0, received);
        Assert.Equal(before, Counts());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Receive_ConstructionGeometryPreservesDependenciesThroughFreshXtCopyAndDeletion(bool sharedChain)
    {
        var before = Counts();
        var blocks = KernelRuntime.State.Session->Blocks.Statistics;
        var source = CreateConstructionWire(sharedChain);
        var expected = ConstructionSamples(source);
        Assert.Equal(0, Receive(Transmit(source), out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var slot));
        Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var record = KernelRuntime.GetBodyRecord(slot);
        Assert.Equal(sharedChain ? 3 : 1, record.ConstructionSurfaceCount);
        Assert.Equal(sharedChain ? 1 : 0, record.ConstructionCurveCount);
        AssertSamples(expected, ConstructionSamples(received));
        KernelRuntime.Xt.Drop(slot);
        Assert.Equal(0, Receive(Transmit(received), out var fresh));
        AssertSamples(expected, ConstructionSamples(fresh));
        int copy; Assert.Equal(0, KernelRuntime.EntityCopy(received, &copy));
        int* originals = stackalloc int[2] { source, received };
        Assert.Equal(0, KernelRuntime.EntityDelete(2, originals));
        AssertSamples(expected, ConstructionSamples(copy));
        Assert.Equal(0, Receive(Transmit(copy), out var copiedRoundtrip));
        AssertSamples(expected, ConstructionSamples(copiedRoundtrip));
        int* remaining = stackalloc int[3] { fresh, copy, copiedRoundtrip };
        Assert.Equal(0, KernelRuntime.EntityDelete(3, remaining));
        Assert.Equal(before, Counts());
        var afterBlocks = KernelRuntime.State.Session->Blocks.Statistics;
        Assert.Equal(blocks.LiveBlocks, afterBlocks.LiveBlocks);
        Assert.Equal(blocks.LiveBytes, afterBlocks.LiveBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(25)]
    public void Receive_ConstructionGeometryAllocationFailureRestoresAllPayloads(int allocation)
    {
        var source = CreateConstructionWire(sharedChain: true);
        var text = Transmit(source);
        var before = Counts();
        var blocks = KernelRuntime.State.Session->Blocks.Statistics;
        KernelRuntime.InjectAllocationFailure(allocation);
        int error, body;
        try { error = Receive(text, out body); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0) Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        else Assert.Equal(0, body);
        Assert.Equal(before, Counts());
        var afterBlocks = KernelRuntime.State.Session->Blocks.Statistics;
        Assert.Equal(blocks.LiveBlocks, afterBlocks.LiveBlocks);
        Assert.Equal(blocks.LiveBytes, afterBlocks.LiveBytes);
    }

    [Fact]
    public void Receive_RejectsConstructionMemberOwnedByAnotherTopology()
    {
        var source = CreateConstructionWire(sharedChain: false);
        var document = XtText.DecodeDocument(Transmit(source));
        var root = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.Body);
        var construction = document.Nodes.Single(n => n.Index == root.Fields[4].Pointer);
        construction.Fields[2] = XtFieldValue.Ptr(document.Nodes.First(n => n.Type == (int)XtNodeTypes.Edge).Index);
        var before = Counts();
        Assert.Equal(ParasolidConstants.PK_ERROR_corrupt_file, Receive(XtText.Encode(document), out var body));
        Assert.Equal(0, body); Assert.Equal(before, Counts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(65)]
    [InlineData(110)]
    public void Receive_AllocationFailureRollsBackGeometryAndTopology(int allocation)
    {
        var source = CreatePosed(0);
        var text = Transmit(source);
        var before = Counts();
        KernelRuntime.InjectAllocationFailure(allocation);
        int error; int received;
        try { error = Receive(text, out received); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0) Assert.Equal(0, KernelRuntime.EntityDelete(1, &received));
        else Assert.Equal(0, received);
        Assert.Equal(before, Counts());
    }

    [Fact]
    public void Receive_PreservesPlanarHoleLoopsAndNeutralShellUses()
    {
        var points = stackalloc PK_VECTOR_s[8];
        double[] xy = [0,0, 8,0, 8,7, 0,7, 2,2, 2,4, 5,4, 5,2];
        for (var i = 0; i < 8; i++) { points[i].coord[0] = xy[2*i]; points[i].coord[1] = xy[2*i+1]; points[i].coord[2] = 2; }
        var loopIndices = stackalloc int[8] {0,0,0,0,1,1,1,1};
        var options = new PK_BODY_create_sheet_planar_o_s { o_t_version = 1, loops = loopIndices };
        options.plane.location.coord[2] = 2; options.plane.axis.coord[2] = 1;
        options.basis_set.location.coord[2] = 2; options.basis_set.axis.coord[2] = 1; options.basis_set.ref_direction.coord[0] = 1;
        int source;
        Assert.Equal(0, KernelRuntime.BodyCreateSheetPlanar(8, points, &options, &source));
        var before = Counts();
        Assert.Equal(0, Receive(Transmit(source), out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var slot));
        Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var body = KernelRuntime.GetBodyRecord(slot);
        Assert.Equal(1, body.RegionCount); Assert.Equal(1, body.ShellCount); Assert.Equal(1, body.FaceCountBody);
        Assert.Equal(2, KernelRuntime.GetFaceRecord(body.FirstFaceBody).LoopCount);
        Assert.Equal(2, KernelRuntime.GetShellRecord(body.FirstShell).FaceUseCount);
        AssertSamples(Samples(source), Samples(received));
        KernelRuntime.Xt.Drop(slot);
        Assert.Equal(0, Receive(Transmit(received), out var fresh));
        AssertSamples(Samples(source), Samples(fresh));
        int* copies = stackalloc int[2] {received,fresh};
        Assert.Equal(0, KernelRuntime.EntityDelete(2, copies));
        Assert.Equal(before, Counts());
    }

    [Fact]
    public void Receive_FullCircleWirePreservesVertexlessShellAndPublicTopology()
    {
        var sf = new PK_CIRCLE_sf_s { radius = 2.5 };
        sf.basis_set.location.coord[0] = 7; sf.basis_set.axis.coord[2] = 1; sf.basis_set.ref_direction.coord[0] = 1;
        int curve; Assert.Equal(0, KernelRuntime.CircleCreate(&sf, &curve));
        var bounds = new PK_INTERVAL_s(); bounds.value[1] = Math.Tau;
        int source; Assert.Equal(0, KernelRuntime.CurveMakeWireBody(curve, bounds, &source));
        Assert.Equal(0, Receive(Transmit(source), out var body));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var record = KernelRuntime.GetBodyRecord(slot);
        Assert.Equal(0, record.VertexCountBody); Assert.Equal(1, record.ShellCount);
        Assert.Equal(KernelRuntime.TagOf(PoolKind.Shell, record.FirstShell), KernelRuntime.GetEdgeRecord(record.FirstEdgeBody).WireShellTag);
        int publicFinCount; int* publicFins;
        Assert.Equal(0, KernelRuntime.EdgeAskFins(KernelRuntime.TagOf(PoolKind.Edge, record.FirstEdgeBody), &publicFinCount, &publicFins));
        Assert.Equal(0, publicFinCount); Assert.True(publicFins == null);
        int n, nr; nint topols, classes, parents, children, senses;
        var options = new PK_BODY_ask_topology_o_s {o_t_version=2,want_fins=1};
        Assert.Equal(0, KernelRuntime.BodyAskTopology(body,&options,&n,&topols,&classes,&nr,&parents,&children,&senses));
        Assert.Equal(4,n); Assert.Equal(3,nr);
        KernelRuntime.Xt.Drop(slot);
        Assert.Equal(0, Receive(Transmit(body), out var fresh));
        AssertSamples(Samples(source), Samples(fresh));
    }

    [Fact]
    public void EditingReceivedGeometry_InvalidatesCachedSourceAndMarkGotoUsesRestoredRuntime()
    {
        var source = CreatePosed(0);
        Assert.Equal(0, Receive(Transmit(source), out var body));
        Assert.True(KernelRuntime.TryGetReceivedXt(body, out _, out _));
        int count; int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        var face = faces[0];
        int original;
        Assert.Equal(0, KernelRuntime.FaceAskSurf(face, &original));
        PK_PLANE_sf_s definition;
        Assert.Equal(0, KernelRuntime.PlaneAsk(original, &definition));
        definition.basis_set.location.coord[0] += 23;
        var changedX = definition.basis_set.location.coord[0];
        int replacement;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&definition, &replacement));
        int mark;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face));
        byte sense = 1;
        Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &face, &replacement, &sense));
        Assert.False(KernelRuntime.TryGetReceivedXt(body, out _, out _));
        var changed = XtText.DecodeDocument(Transmit(body));
        Assert.Contains(changed.Nodes, node => node.Type == (int)XtNodeTypes.Plane
            && Math.Abs(node.Fields[7].Vector.X - changedX) < 1e-12);
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        AssertSamples(Samples(source), Samples(body));
        Assert.Equal(0, Receive(Transmit(body), out var restored));
        AssertSamples(Samples(source), Samples(restored));
    }

    [Fact]
    public void PublicTopologyQueries_MatchMeasuredPkBlockContract()
    {
        var body = CreatePosed(0);
        int type, config;
        Assert.Equal(0, KernelRuntime.BodyAskType(body, &type));
        Assert.Equal(ParasolidConstants.PK_BODY_type_solid_c, type);
        Assert.Equal(0, KernelRuntime.BodyAskConfig(body, &config));
        Assert.Equal(ParasolidConstants.PK_BODY_config_standard_c, config);
        foreach (var wantFins in new[] { false, true })
        {
            var options = new PK_BODY_ask_topology_o_s { o_t_version = 2, want_fins = (byte)(wantFins ? 1 : 0) };
            int count, relations; nint topols, classes, parents, children, senses;
            Assert.Equal(0, KernelRuntime.BodyAskTopology(body, &options, &count, &topols, &classes,
                &relations, &parents, &children, &senses));
            Assert.Equal(wantFins ? 61 : 37, count);
            Assert.Equal(wantFins ? 94 : 70, relations);
            var ct = (int*)classes; var pa = (int*)parents; var ch = (int*)children;
            Assert.Equal(24, Enumerable.Range(0, relations).Count(i => ct[pa[i]] == ParasolidConstants.PK_CLASS_edge && ct[ch[i]] == ParasolidConstants.PK_CLASS_vertex));
            Assert.Equal(24, Enumerable.Range(0, relations).Count(i => ct[pa[i]] == (wantFins ? ParasolidConstants.PK_CLASS_fin : ParasolidConstants.PK_CLASS_loop) && ct[ch[i]] == ParasolidConstants.PK_CLASS_edge));
        }
        int n; int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &n, &faces));
        var face = faces[0];
        int countEdges; int* edges;
        Assert.Equal(0, KernelRuntime.FaceAskEdges(face, &countEdges, &edges));
        Assert.Equal(4, countEdges);
        var edge = edges[0];
        int* vertices = stackalloc int[2];
        Assert.Equal(0, KernelRuntime.EdgeAskVertices(edge, vertices));
        Assert.NotEqual(0, vertices[0]); Assert.NotEqual(0, vertices[1]); Assert.NotEqual(vertices[0], vertices[1]);
        int surf, expectedSurf; byte sense;
        Assert.Equal(0, KernelRuntime.FaceAskOrientedSurf(face, &surf, &sense));
        Assert.Equal(0, KernelRuntime.FaceAskSurf(face, &expectedSurf));
        Assert.Equal(expectedSurf, surf);
        int* regions; Assert.Equal(0, KernelRuntime.BodyAskRegions(body, &n, &regions));
        var region = regions[0];
        int shellCount; int* shells;
        Assert.Equal(0, KernelRuntime.RegionAskShells(region, &shellCount, &shells));
        Assert.Equal(1, shellCount); Assert.NotEqual(0, shells[0]);
    }

    [Fact]
    public void OpaqueBodyClassification_ComesFromSourceMetadata()
    {
        var source = CreatePosed(0);
        var document = XtText.DecodeDocument(Transmit(source));
        var root = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.Body);
        root.Fields[17] = XtFieldValue.Unsigned(3);
        Assert.Equal(0, KernelRuntime.CreateOpaquePartCore(document, null, root, out var opaque));
        Assert.True(KernelRuntime.TryResolveBodySlot(opaque, out var slot));
        Assert.True(KernelRuntime.Xt.IsOpaque(slot));
        int type, config;
        Assert.Equal(0, KernelRuntime.BodyAskType(opaque, &type));
        Assert.Equal(ParasolidConstants.PK_BODY_type_sheet_c, type);
        Assert.Equal(0, KernelRuntime.BodyAskConfig(opaque, &config));
        Assert.Equal(ParasolidConstants.PK_BODY_config_standard_c, config);
    }

    [Fact]
    public void PublicTopologyQueries_ClosedSeamEdgesHaveOneVertexRelationAndCopyPreservesBothEnds()
    {
        var points = stackalloc PK_VECTOR_s[4];
        points[0].coord[0] = 2;
        points[1].coord[0] = 4;
        points[2].coord[0] = 4; points[2].coord[2] = 2;
        points[3].coord[0] = 2; points[3].coord[2] = 2;
        var planar = new PK_BODY_create_sheet_planar_o_s { o_t_version = 1 };
        planar.plane.axis.coord[1] = -1;
        planar.basis_set.axis.coord[2] = 1; planar.basis_set.ref_direction.coord[0] = 1;
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSheetPlanar(4, points, &planar, &body));
        var axis = new PK_AXIS1_sf_s(); axis.axis.coord[2] = 1;
        int lateralCount, result;
        Assert.Equal(0, KernelRuntime.BodySpin(body, &axis, Math.Tau, 1, &lateralCount, null, null, &result));
        int copy;
        Assert.Equal(0, KernelRuntime.EntityCopy(body, &copy));
        foreach (var target in new[] { body, copy })
        foreach (var wantFins in new[] { false, true })
        {
            var options = new PK_BODY_ask_topology_o_s { o_t_version = 2, want_fins = (byte)(wantFins ? 1 : 0) };
            int count, relations; nint topols, classes, parents, children, senses;
            Assert.Equal(0, KernelRuntime.BodyAskTopology(target, &options, &count, &topols, &classes,
                &relations, &parents, &children, &senses));
            try
            {
                Assert.Equal(wantFins ? 41 : 25, count);
                Assert.Equal(wantFins ? 60 : 44, relations);
                var tags = (int*)topols; var types = (int*)classes;
                var parent = (int*)parents; var child = (int*)children;
                var closedEdges = 0;
                int* vertices = stackalloc int[2];
                for (var i = 0; i < count; i++)
                {
                    if (types[i] != ParasolidConstants.PK_CLASS_edge) continue;
                    Assert.Equal(0, KernelRuntime.EdgeAskVertices(tags[i], vertices));
                    Assert.NotEqual(0, vertices[0]); Assert.NotEqual(0, vertices[1]);
                    var vertexRelations = 0;
                    for (var j = 0; j < relations; j++)
                        if (parent[j] == i && types[child[j]] == ParasolidConstants.PK_CLASS_vertex) vertexRelations++;
                    Assert.Equal(vertices[0] == vertices[1] ? 1 : 2, vertexRelations);
                    if (vertices[0] == vertices[1]) closedEdges++;
                }
                Assert.Equal(4, closedEdges);
            }
            finally
            {
                KernelRuntime.MemoryFree((void*)topols); KernelRuntime.MemoryFree((void*)classes);
                KernelRuntime.MemoryFree((void*)parents); KernelRuntime.MemoryFree((void*)children);
                KernelRuntime.MemoryFree((void*)senses);
            }
        }
    }

    [Fact]
    public void Receive_ApexConePreservesSingleVertexPoleLoopThroughCopyAndDelete()
    {
        var before = Counts();
        var source = CreatePosed(6);
        Assert.Equal(0, Receive(Transmit(source), out var received));
        Assert.True(KernelRuntime.TryResolveBodySlot(received, out var receivedSlot));
        Assert.False(KernelRuntime.Xt.IsOpaque(receivedSlot));
        int copy;
        Assert.Equal(0, KernelRuntime.EntityCopy(received, &copy));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &received));
        Assert.True(KernelRuntime.TryResolveBodySlot(copy, out var copySlot));
        var body = KernelRuntime.GetBodyRecord(copySlot);
        Assert.Equal(1, body.VertexCountBody); Assert.Equal(1, body.EdgeCountBody);
        var poles = 0; var face = body.FirstFaceBody;
        for (var i = 0; i < body.FaceCountBody; i++, face = KernelRuntime.GetFaceRecord(face).NextInBody)
        {
            var record = KernelRuntime.GetFaceRecord(face);
            var loop = record.FirstLoop;
            for (var j = 0; j < record.LoopCount; j++, loop = KernelRuntime.GetLoopRecord(loop).NextInFace)
            {
                var boundary = KernelRuntime.GetLoopRecord(loop);
                if (KernelRuntime.GetFinRecord(boundary.FirstFin).Edge >= 0) continue;
                poles++;
                Assert.Equal(SurfaceClass.Cone, KernelRuntime.GetSurfaceByTag(record.SurfTag).Class);
                Assert.Equal(1, boundary.FinCount);
                Assert.Equal(body.FirstVertexBody, KernelRuntime.GetFinRecord(boundary.FirstFin).Vertex);
                int count; int* fins;
                Assert.Equal(0, KernelRuntime.LoopAskFins(KernelRuntime.TagOf(PoolKind.Loop, loop), &count, &fins));
                Assert.Equal(0, count); Assert.True(fins == null);
            }
        }
        Assert.Equal(1, poles);
        foreach (var wantFins in new[] { false, true })
        {
            var options = new PK_BODY_ask_topology_o_s { o_t_version = 2, want_fins = (byte)(wantFins ? 1 : 0) };
            int count, relations; nint topols, classes, parents, children, senses;
            Assert.Equal(0, KernelRuntime.BodyAskTopology(copy, &options, &count, &topols, &classes,
                &relations, &parents, &children, &senses));
            Assert.Equal(wantFins ? 14 : 12, count); Assert.Equal(wantFins ? 16 : 14, relations);
            KernelRuntime.MemoryFree((void*)topols); KernelRuntime.MemoryFree((void*)classes);
            KernelRuntime.MemoryFree((void*)parents); KernelRuntime.MemoryFree((void*)children);
            KernelRuntime.MemoryFree((void*)senses);
        }
        Assert.Equal(0, Receive(Transmit(copy), out var fresh));
        AssertSamples(Samples(source), Samples(fresh));
        int* remaining = stackalloc int[3] { source, copy, fresh };
        Assert.Equal(0, KernelRuntime.EntityDelete(3, remaining));
        Assert.Equal(before, Counts());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transmit_TopologyOwnedBCurveAlsoRecordsProceduralSurfaceOwner(bool spun)
    {
        var body = CreatePosed(0);
        var section = AttachTestBCurveToFirstEdge(body);
        var surface = CreateTestProceduralSurface(section, spun);
        ReplaceFaceSurface(body, 0, surface);
        var document = XtText.DecodeDocument(Transmit(body));
        var curve = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.BCurve);
        var owner = Assert.Single(document.Nodes.Where(n => n.Type == (int)XtNodeTypes.GeometricOwner
            && n.Fields[3].Pointer == curve.Index));
        var dependent = document.Nodes.Single(n => n.Type == (int)(spun ? XtNodeTypes.SpunSurf : XtNodeTypes.SweptSurf));
        Assert.Equal(dependent.Index, owner.Fields[0].Pointer);
        Assert.Equal(owner.Index, curve.Fields[5].Pointer);
        Assert.Equal(owner.Index, owner.Fields[1].Pointer);
        Assert.Equal(owner.Index, owner.Fields[2].Pointer);
        Assert.Equal(curve.Index, dependent.Fields[7].Pointer);
        Assert.Contains(document.Nodes, n => n.Type == (int)XtNodeTypes.Edge && n.Index == curve.Fields[2].Pointer);
    }

    [Fact]
    public void Transmit_SharedProceduralSectionWritesDeduplicatedGeometricOwnerRing()
    {
        var body = CreatePosed(0);
        var section = AttachTestBCurveToFirstEdge(body);
        var swept = CreateTestProceduralSurface(section, spun: false);
        var spun = CreateTestProceduralSurface(section, spun: true);
        var offsetDefinition = new PK_OFFSET_sf_s { underlying_surface = swept, offset_distance = 0.25 };
        int offset;
        Assert.Equal(0, KernelRuntime.OffsetCreate(&offsetDefinition, &offset));
        // The nested surface forces more than one dependency-collection pass.
        ReplaceFaceSurface(body, 0, offset);
        ReplaceFaceSurface(body, 1, spun);
        var document = XtText.DecodeDocument(Transmit(body));
        var curve = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.BCurve);
        var owners = document.Nodes.Where(n => n.Type == (int)XtNodeTypes.GeometricOwner
            && n.Fields[3].Pointer == curve.Index).ToArray();
        Assert.Equal(2, owners.Length);
        var expected = document.Nodes.Where(n => n.Type is (int)XtNodeTypes.SweptSurf or (int)XtNodeTypes.SpunSurf)
            .Select(n => n.Index).Order().ToArray();
        Assert.Equal(expected, owners.Select(n => n.Fields[0].Pointer).Order().ToArray());
        Assert.Contains(owners, n => n.Index == curve.Fields[5].Pointer);
        foreach (var owner in owners)
        {
            var other = owners.Single(n => n.Index != owner.Index);
            Assert.Equal(other.Index, owner.Fields[1].Pointer);
            Assert.Equal(other.Index, owner.Fields[2].Pointer);
        }
    }

    private static int AttachTestBCurveToFirstEdge(int body)
    {
        double* poles = stackalloc double[6] { 2,0,0, 3,0,1 };
        double* knots = stackalloc double[2] { 0,1 };
        int* multiplicities = stackalloc int[2] { 2,2 };
        var definition = new PK_BCURVE_sf_s
        {
            degree = 1, n_vertices = 2, vertex_dim = 3, vertex = poles,
            n_knots = 2, knot = knots, knot_mult = multiplicities,
            form = ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c,
        };
        int curve;
        Assert.Equal(0, KernelRuntime.BCurveCreate(&definition, &curve));
        int count; int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &edges));
        try
        {
            var edge = edges[0];
            Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(edge));
            Assert.Equal(0, KernelRuntime.EdgeAttachCurves(1, &edge, &curve));
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(edges)); }
        return curve;
    }

    private static int CreateConstructionWire(bool sharedChain)
    {
        var poles = stackalloc double[9] { 2,0,0, 3,1,1, 2,0,2 };
        var knots = stackalloc double[2] { 0,1 };
        var multiplicities = stackalloc int[2] { 3,3 };
        var definition = new PK_BCURVE_sf_s
        {
            degree = 2, n_vertices = 3, vertex_dim = 3, vertex = poles,
            n_knots = 2, knot = knots, knot_mult = multiplicities,
            form = ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c,
        };
        int curve; Assert.Equal(0, KernelRuntime.BCurveCreate(&definition, &curve));
        var inputCurve = curve;
        var interval = new PK_INTERVAL_s(); interval.value[1] = 1;
        int body; Assert.Equal(0, KernelRuntime.CurveMakeWireBody(curve, interval, &body));
        int n; int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &n, &edges));
        Assert.Equal(1, n); Assert.Equal(0, KernelRuntime.EdgeAskCurve(edges[0], &curve));
        Assert.Equal(0, KernelRuntime.MemoryFree(edges));
        if (inputCurve != curve) Assert.Equal(0, KernelRuntime.EntityDelete(1, &inputCurve));
        var sweep = new PK_SWEPT_sf_s { curve = curve }; sweep.direction.coord[1] = 1;
        int swept; Assert.Equal(0, KernelRuntime.SweptCreate(&sweep, &swept));
        if (sharedChain)
        {
            var offset = new PK_OFFSET_sf_s { underlying_surface = swept, offset_distance = .1 };
            int root; Assert.Equal(0, KernelRuntime.OffsetCreate(&offset, &root));
            var spun = new PK_SPUN_sf_s { curve = curve }; spun.axis.axis.coord[2] = 1;
            Assert.Equal(0, KernelRuntime.SpunCreate(&spun, &root));
            var trim = new PK_TRCURVE_sf_s { basis_curve = curve };
            trim.t_int.value[0] = .2; trim.t_int.value[1] = .8;
            Assert.Equal(0, KernelRuntime.TrCurveCreate(&trim, &root));
        }
        return body;
    }

    private static double[] ConstructionSamples(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        var record = KernelRuntime.GetBodyRecord(slot);
        var rows = new List<double[]>();
        var values = stackalloc PK_VECTOR_s[4];
        var uv = new PK_UV_s(); uv.param[0] = .31; uv.param[1] = .27;
        foreach (var surfaces in new[] { false, true })
        {
            var count = surfaces ? record.ConstructionSurfaceCount : record.ConstructionCurveCount;
            var first = surfaces ? record.FirstConstructionSurface : record.FirstConstructionCurve;
            var geometry = first < 0 ? 0 : KernelRuntime.TagOf(surfaces ? PoolKind.Surface : PoolKind.Curve, first);
            for (var i = 0; i < count; i++, geometry = KernelRuntime.GetConstructionNext(geometry))
            {
                Assert.True(geometry > 0);
                Assert.Equal(slot, KernelRuntime.GetConstructionBody(geometry));
                int cls; Assert.Equal(0, KernelRuntime.EntityAskClass(geometry, &cls));
                if (surfaces) Assert.Equal(0, KernelRuntime.SurfEval(geometry, uv, 1, 1, 0, values));
                else Assert.Equal(0, KernelRuntime.CurveEval(geometry, .37, 2, values));
                var length = surfaces ? 4 : 3;
                var row = new double[2+length*3]; row[0] = cls;
                row[1] = surfaces ? KernelRuntime.GetSurfaceByTag(geometry).GeometricOwnerCount
                    : KernelRuntime.GetCurveByTag(geometry).GeometricOwnerCount;
                for (var k = 0; k < length; k++) for (var j = 0; j < 3; j++) row[2+3*k+j] = values[k].coord[j];
                rows.Add(row);
            }
            Assert.Equal(0, geometry);
        }
        return rows.OrderBy(row => row[0]).ThenBy(row => row[2]).SelectMany(row => row).ToArray();
    }

    private static int CreateTestProceduralSurface(int section, bool spun)
    {
        int surface;
        if (spun)
        {
            var definition = new PK_SPUN_sf_s { curve = section };
            definition.axis.axis.coord[2] = 1;
            Assert.Equal(0, KernelRuntime.SpunCreate(&definition, &surface));
        }
        else
        {
            var definition = new PK_SWEPT_sf_s { curve = section };
            definition.direction.coord[1] = 1;
            Assert.Equal(0, KernelRuntime.SweptCreate(&definition, &surface));
        }
        return surface;
    }

    private static void ReplaceFaceSurface(int body, int index, int surface)
    {
        int count; int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        try
        {
            var face = faces[index];
            Assert.Equal(0, KernelRuntime.TopologyDetachGeometry(face));
            byte sense = 1;
            Assert.Equal(0, KernelRuntime.FaceAttachSurfaces(1, &face, &surface, &sense));
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(faces)); }
    }

    private static int CreatePosed(int primitive)
    {
        var axis = new PK_AXIS2_sf_s();
        axis.location.coord[0] = 11; axis.location.coord[1] = -7; axis.location.coord[2] = 5;
        axis.axis.coord[0] = 0.6; axis.axis.coord[2] = 0.8;
        axis.ref_direction.coord[1] = 1;
        int body = 0;
        var error = primitive switch
        {
            0 => KernelRuntime.BodyCreateSolidBlock(2.25, 4.5, 7.75, &axis, &body),
            1 => KernelRuntime.BodyCreateSolidPrism(2.75, 5.25, 5, &axis, &body),
            2 => KernelRuntime.BodyCreateSolidCyl(2.75, 5.25, &axis, &body),
            3 => KernelRuntime.BodyCreateSolidCone(2.75, 4.25, 0.3, &axis, &body),
            4 => KernelRuntime.BodyCreateSolidSphere(3.25, &axis, &body),
            5 => KernelRuntime.BodyCreateSolidTorus(4.25, 1.15, &axis, &body),
            _ => KernelRuntime.BodyCreateSolidCone(0, 5, 0.25, &axis, &body),
        };
        Assert.Equal(0, error);
        return body;
    }

    private static double[] Samples(int body)
    {
        var samples = new List<double[]>();
        int count; int* entities;
        Assert.Equal(0, KernelRuntime.BodyAskVertices(body, &count, &entities));
        var vertices = new ReadOnlySpan<int>(entities, count).ToArray();
        foreach (var vertex in vertices)
        {
            int point; PK_POINT_sf_s definition;
            Assert.Equal(0, KernelRuntime.VertexAskPoint(vertex, &point));
            Assert.Equal(0, KernelRuntime.PointAsk(point, &definition));
            samples.Add([0, definition.position.coord[0], definition.position.coord[1], definition.position.coord[2]]);
        }
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &entities));
        var edges = new ReadOnlySpan<int>(entities, count).ToArray();
        foreach (var edge in edges)
        {
            int curve; PK_VECTOR_s value;
            Assert.Equal(0, KernelRuntime.EdgeAskCurve(edge, &curve));
            Assert.Equal(0, KernelRuntime.CurveEval(curve, 0.37, 0, &value));
            samples.Add([1, value.coord[0], value.coord[1], value.coord[2]]);
        }
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &entities));
        var faces = new ReadOnlySpan<int>(entities, count).ToArray();
        foreach (var face in faces)
        {
            int surface; PK_VECTOR_s value;
            var uv = new PK_UV_s(); uv.param[0] = 0.31; uv.param[1] = 0.27;
            Assert.Equal(0, KernelRuntime.FaceAskSurf(face, &surface));
            Assert.Equal(0, KernelRuntime.SurfEval(surface, uv, 0, 0, 0, &value));
            samples.Add([2, value.coord[0], value.coord[1], value.coord[2]]);
        }
        return samples.OrderBy(a => a[0]).ThenBy(a => a[1]).ThenBy(a => a[2]).ThenBy(a => a[3]).SelectMany(a => a).ToArray();
    }

    private static void AssertPlaneBasesAreUnit(int body)
    {
        int count; int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces));
        try
        {
            for (var i = 0; i < count; i++)
            {
                int surface; PK_PLANE_sf_s definition;
                Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[i], &surface));
                Assert.Equal(0, KernelRuntime.PlaneAsk(surface, &definition));
                var normalLength = 0.0; var referenceLength = 0.0; var dot = 0.0;
                for (var j = 0; j < 3; j++)
                {
                    var normal = definition.basis_set.axis.coord[j];
                    var reference = definition.basis_set.ref_direction.coord[j];
                    normalLength += normal * normal;
                    referenceLength += reference * reference;
                    dot += normal * reference;
                }
                Assert.InRange(Math.Abs(normalLength - 1), 0, 2e-14);
                Assert.InRange(Math.Abs(referenceLength - 1), 0, 2e-14);
                Assert.InRange(Math.Abs(dot), 0, 2e-14);
            }
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(faces)); }
    }

    private static void AssertSamples(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++) Assert.InRange(Math.Abs(expected[i] - actual[i]), 0, 2e-12);
    }

    private static int[] TopologyCounts(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        var record = KernelRuntime.GetBodyRecord(slot);
        return [record.BodyType, record.RegionCount, record.ShellCount, record.FaceCountBody, record.EdgeCountBody, record.VertexCountBody];
    }

    private static int[] Counts() =>
    [KernelRuntime.Bodies.AliveCount, KernelRuntime.Regions.AliveCount, KernelRuntime.Shells.AliveCount,
        KernelRuntime.Faces.AliveCount, KernelRuntime.FaceUses.AliveCount, KernelRuntime.Loops.AliveCount,
        KernelRuntime.Edges.AliveCount, KernelRuntime.Fins.AliveCount, KernelRuntime.Vertices.AliveCount,
        KernelRuntime.Points.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.Surfaces.AliveCount,
        KernelRuntime.LineDataPool.AliveCount, KernelRuntime.PlaneDataPool.AliveCount,
        KernelRuntime.BCurveDataStore.AliveCount, KernelRuntime.SweptDataPool.AliveCount, KernelRuntime.SpunDataPool.AliveCount,
        KernelRuntime.OffsetDataPool.AliveCount, KernelRuntime.TrCurveDataPool.AliveCount,
        KernelRuntime.State.Session->Tags.LiveOrKeptCount];

    internal static string Transmit(int body)
    {
        var options = new PK_PART_transmit_o_s { o_t_version = 4, transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            transmit_version = 371, transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c };
        PK_MEMORY_block_s block;
        Assert.Equal(0, KernelRuntime.PartTransmitB(1, &body, &options, &block));
        try { return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(block.bytes, (int)block.n_bytes)); }
        finally { Assert.Equal(0, KernelRuntime.MemoryBlockFree(&block)); }
    }

    internal static int Receive(string text, out int body)
    {
        body = 0;
        var bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* buffer = bytes)
        {
            var block = new PK_MEMORY_block_s { bytes = buffer, n_bytes = (nuint)bytes.Length };
            var options = new PK_PART_receive_o_s { o_t_version = 1, transmit_format = ParasolidConstants.PK_transmit_format_text_c };
            int count = 0; int* parts = null;
            var error = KernelRuntime.PartReceiveB(block, &options, &count, &parts);
            if (error == 0) { Assert.Equal(1, count); body = parts[0]; }
            if (parts != null) Assert.Equal(0, KernelRuntime.MemoryFree(parts));
            return error;
        }
    }
}
