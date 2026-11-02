using System.Text;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using Xunit;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IcurveWireShellTests : IDisposable
{
    public IcurveWireShellTests()
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

    [Fact]
    public void PublicReceive_RealPkWireEvaluatesAndFreshTransmitPreservesShellOwnership()
    {
        var before = PoolCounts();
        var body = Receive(RealPkWire);
        var curve = AssertShellWire(body);
        var record = KernelRuntime.GetCurveByTag(curve);
        var parameter = record.TMin + 0.41 * (record.TMax - record.TMin);
        Span<PK_VECTOR_s> expected = stackalloc PK_VECTOR_s[3];
        Evaluate(curve, parameter, expected);
        AssertSurfaceJets(expected);

        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        Assert.False(KernelRuntime.Xt.IsOpaque(bodySlot));
        KernelRuntime.Xt.Drop(bodySlot);
        Assert.False(KernelRuntime.TryGetReceivedXt(body, out _, out _));

        var text = Transmit(body);
        var document = XtText.DecodeDocument(text);
        var root = Node(document, XtNodeTypes.Body);
        var shell = Node(document, XtNodeTypes.Shell);
        var region = Node(document, XtNodeTypes.Region);
        var edge = Node(document, XtNodeTypes.Edge);
        Assert.Equal(shell.Index, root.Fields[19].Pointer);
        Assert.Equal(region.Index, root.Fields[26].Pointer);
        Assert.Equal(0, root.Fields[27].Pointer);
        Assert.Equal(root.Index, shell.Fields[2].Pointer);
        Assert.Equal(0, shell.Fields[4].Pointer);
        Assert.Equal(edge.Index, shell.Fields[5].Pointer);
        Assert.Equal(region.Index, shell.Fields[7].Pointer);
        Assert.Equal(0, shell.Fields[8].Pointer);
        Assert.Equal(root.Index, region.Fields[2].Pointer);
        Assert.Equal(shell.Index, region.Fields[5].Pointer);
        Assert.Equal('V', region.Fields[7].Character);
        Assert.Equal(shell.Index, edge.Fields[9].Pointer);

        var received = Receive(text);
        var receivedCurve = AssertShellWire(received);
        Span<PK_VECTOR_s> actual = stackalloc PK_VECTOR_s[3];
        Evaluate(receivedCurve, parameter, actual);
        for (var order = 0; order < 3; order++)
        {
            var actualValue = actual[order];
            var expectedValue = expected[order];
            for (var axis = 0; axis < 3; axis++)
                Assert.InRange(Math.Abs(actualValue.coord[axis] - expectedValue.coord[axis]),
                    0, 1e-9 * Math.Max(1, Math.Abs(expectedValue.coord[axis])));
        }
        AssertSurfaceJets(actual);

        int* bodies = stackalloc int[2] { body, received };
        Assert.Equal(0, KernelRuntime.EntityDelete(2, bodies));
        Assert.Equal(before, PoolCounts());
    }

    [Fact]
    public void PublicReceive_DirectBodyWireSubsetKeepsItsOriginalOwnership()
    {
        var document = XtText.DecodeDocument(RealPkWire);
        var root = Node(document, XtNodeTypes.Body);
        var edge = Node(document, XtNodeTypes.Edge);
        root.Fields[19] = XtFieldValue.Ptr(0);
        root.Fields[26] = XtFieldValue.Ptr(0);
        root.Fields[27] = XtFieldValue.Ptr(edge.Index);
        edge.Fields[9] = XtFieldValue.Ptr(root.Index);
        document = WithNodes(document, document.Nodes.Where(node =>
            node.Type != (int)XtNodeTypes.Shell && node.Type != (int)XtNodeTypes.Region).ToArray());
        var body = Receive(XtText.Encode(document));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var bodySlot));
        var record = KernelRuntime.GetBodyRecord(bodySlot);
        Assert.Equal(0, record.RegionCount);
        Assert.Equal(0, record.ShellCount);
        Assert.Equal(1, record.EdgeCountBody);
        Assert.False(KernelRuntime.Xt.IsOpaque(bodySlot));
        KernelRuntime.Xt.Drop(bodySlot);
        var fresh = XtText.DecodeDocument(Transmit(body));
        var freshBody = Node(fresh, XtNodeTypes.Body);
        var freshEdge = Node(fresh, XtNodeTypes.Edge);
        Assert.DoesNotContain(fresh.Nodes, node => node.Type == (int)XtNodeTypes.Shell);
        Assert.DoesNotContain(fresh.Nodes, node => node.Type == (int)XtNodeTypes.Region);
        Assert.Equal(freshEdge.Index, freshBody.Fields[27].Pointer);
        Assert.Equal(freshBody.Index, freshEdge.Fields[9].Pointer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PublicReceive_RejectsMalformedShellOwnershipWithoutPublishingEntities(int scenario)
    {
        var document = XtText.DecodeDocument(RealPkWire);
        var root = Node(document, XtNodeTypes.Body);
        var shell = Node(document, XtNodeTypes.Shell);
        var region = Node(document, XtNodeTypes.Region);
        var edge = Node(document, XtNodeTypes.Edge);
        switch (scenario)
        {
            case 0: edge.Fields[9] = XtFieldValue.Ptr(root.Index); break;
            case 1: shell.Fields[2] = XtFieldValue.Ptr(0); break;
            case 2: shell.Fields[7] = XtFieldValue.Ptr(0); break;
            case 3: region.Fields[2] = XtFieldValue.Ptr(0); break;
            default: region.Fields[5] = XtFieldValue.Ptr(0); break;
        }
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.InvalidInput,
            KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out var body));
        Assert.Equal(0, body);
        Assert.Equal(ParasolidConstants.PK_ERROR_corrupt_file,
            TryReceive(XtText.Encode(document), out body));
        Assert.Equal(0, body);
        Assert.Equal(before, PoolCounts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Materializer_RejectsAdditionalOrNonVoidShellTopology(int scenario)
    {
        var document = XtText.DecodeDocument(RealPkWire);
        var root = Node(document, XtNodeTypes.Body);
        var shell = Node(document, XtNodeTypes.Shell);
        var region = Node(document, XtNodeTypes.Region);
        if (scenario == 0)
            region.Fields[7] = XtFieldValue.Char('S');
        else if (scenario == 1 || scenario == 2)
        {
            var original = scenario == 1 ? shell : region;
            var extra = new XtNode
            {
                Type = original.Type, Index = document.Nodes.Max(node => node.Index) + 1,
                Fields = original.Fields.ToArray(),
            };
            original.Fields[3] = XtFieldValue.Ptr(extra.Index);
            extra.Fields[3] = XtFieldValue.Ptr(0);
            extra.Fields[5] = XtFieldValue.Ptr(0);
            document = WithNodes(document, [.. document.Nodes, extra]);
        }
        else
        {
            var surface = Node(document, XtNodeTypes.BSurface);
            var face = new XtNode
            {
                Type = (int)XtNodeTypes.Face, Index = document.Nodes.Max(node => node.Index) + 1,
                Fields =
                [
                    XtFieldValue.Int(12), XtFieldValue.Ptr(0), XtFieldValue.Null(),
                    XtFieldValue.Ptr(0), XtFieldValue.Ptr(0), XtFieldValue.Ptr(0),
                    XtFieldValue.Ptr(shell.Index), XtFieldValue.Ptr(surface.Index), XtFieldValue.Char('+'),
                    XtFieldValue.Ptr(0), XtFieldValue.Ptr(0), XtFieldValue.Ptr(0),
                    XtFieldValue.Ptr(0), XtFieldValue.Ptr(0),
                ],
            };
            shell.Fields[4] = XtFieldValue.Ptr(face.Index);
            document = WithNodes(document, [.. document.Nodes, face]);
        }
        var before = PoolCounts();
        Assert.Equal(AlgorithmStatus.Unsupported,
            KernelRuntime.TryMaterializeICurveWireBodyFromXt(document, root, out var body));
        Assert.Equal(0, body);
        Assert.Equal(before, PoolCounts());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(40)]
    public void PublicReceive_AllocationFailureReleasesWireRegionShellAndGeometry(int fault)
    {
        var before = PoolCounts();
        int body;
        int error;
        KernelRuntime.InjectAllocationFailure(fault);
        try { error = TryReceive(RealPkWire, out body); }
        finally { KernelRuntime.InjectAllocationFailure(-1); }
        if (error == 0)
        {
            AssertShellWire(body);
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        }
        else
        {
            Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, error);
            Assert.Equal(0, body);
        }
        Assert.Equal(before, PoolCounts());
    }

    private static int AssertShellWire(int body)
    {
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        var record = KernelRuntime.GetBodyRecord(slot);
        Assert.Equal(ParasolidConstants.PK_BODY_type_wire_c, record.BodyType);
        Assert.Equal(1, record.RegionCount);
        Assert.Equal(1, record.ShellCount);
        Assert.Equal(0, record.FaceCountBody);
        Assert.Equal(1, record.EdgeCountBody);
        Assert.Equal(2, record.VertexCountBody);
        Assert.Equal((byte)0, KernelRuntime.GetRegionRecord(record.FirstRegion).IsSolid);
        Assert.Equal(record.FirstRegion, KernelRuntime.GetShellRecord(record.FirstShell).Region);
        Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var curve = KernelRuntime.GetEdgeRecord(record.FirstEdgeBody).CurveTag;
        Assert.Equal(CurveClass.ICurve, KernelRuntime.GetCurveByTag(curve).Class);
        return curve;
    }

    private static void Evaluate(int curve, double parameter, Span<PK_VECTOR_s> values)
    {
        fixed (PK_VECTOR_s* output = values)
            Assert.Equal(0, KernelRuntime.CurveEval(curve, parameter, 2, output));
    }

    private static void AssertSurfaceJets(ReadOnlySpan<PK_VECTOR_s> values)
    {
        var p = values[0]; var d1 = values[1]; var d2 = values[2];
        var x = p.coord[0]; var y = p.coord[1]; var z = p.coord[2];
        var dx = d1.coord[0]; var dy = d1.coord[1]; var dz = d1.coord[2];
        var ddx = d2.coord[0]; var ddy = d2.coord[1]; var ddz = d2.coord[2];
        Assert.True(double.IsFinite(x + y + z + dx + dy + dz + ddx + ddy + ddz));
        Assert.True(dx * dx + dy * dy + dz * dz > 1e-8);
        Assert.InRange(Math.Abs(x * x + y * y - 4), 0, 1e-7);
        Assert.InRange(Math.Abs(z + x * x * y * y / 81), 0, 1e-7);
        Assert.InRange(Math.Abs(x * dx + y * dy), 0, 1e-7);
        Assert.InRange(Math.Abs(dz + (2 * x * dx * y * y + 2 * y * dy * x * x) / 81), 0, 1e-7);
        Assert.InRange(Math.Abs(dx * dx + dy * dy + x * ddx + y * ddy), 0, 1e-7);
        Assert.InRange(Math.Abs(ddz + (2 * (dx * dx + x * ddx) * y * y
            + 8 * x * dx * y * dy + 2 * x * x * (dy * dy + y * ddy)) / 81), 0, 1e-7);
    }

    private static XtNode Node(XtDocument document, XtNodeTypes type)
        => Assert.Single(document.Nodes, node => node.Type == (int)type);

    private static XtDocument WithNodes(XtDocument source, XtNode[] nodes) => new()
    {
        PhysicalHeader = source.PhysicalHeader, VersionText = source.VersionText,
        HeaderSchemaIdentity = source.HeaderSchemaIdentity, Schema = source.Schema,
        BaseSchema = source.BaseSchema, EmbeddedMaxNodeType = source.EmbeddedMaxNodeType,
        UserFieldSize = source.UserFieldSize, Nodes = nodes,
    };

    private static int[] PoolCounts() =>
    [
        KernelRuntime.Bodies.AliveCount, KernelRuntime.Regions.AliveCount, KernelRuntime.Shells.AliveCount,
        KernelRuntime.Edges.AliveCount, KernelRuntime.Fins.AliveCount, KernelRuntime.Vertices.AliveCount,
        KernelRuntime.Points.AliveCount, KernelRuntime.Curves.AliveCount, KernelRuntime.ICurveDataPool.AliveCount,
        KernelRuntime.Surfaces.AliveCount, KernelRuntime.BSurfaceDataStore.AliveCount,
        KernelRuntime.CylinderDataPool.AliveCount, KernelRuntime.State.Session->Tags.LiveOrKeptCount,
    ];

    private static string Transmit(int body)
    {
        var options = new PK_PART_transmit_o_s
        {
            o_t_version = 4, transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            transmit_version = 371, transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
        };
        var block = new PK_MEMORY_block_s();
        Assert.Equal(0, KernelRuntime.PartTransmitB(1, &body, &options, &block));
        try { return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(block.bytes, (int)block.n_bytes)); }
        finally { Assert.Equal(0, KernelRuntime.MemoryBlockFree(&block)); }
    }

    private static int Receive(string text)
    {
        Assert.Equal(0, TryReceive(text, out var body));
        return body;
    }

    private static int TryReceive(string text, out int body)
    {
        body = 0;
        var bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* data = bytes)
        {
            var block = new PK_MEMORY_block_s { bytes = data, n_bytes = (nuint)bytes.Length };
            var options = new PK_PART_receive_o_s
            {
                o_t_version = 1, transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            };
            int count = 0;
            int* parts = null;
            var error = KernelRuntime.PartReceiveB(block, &options, &count, &parts);
            try
            {
                if (error == 0)
                {
                    Assert.Equal(1, count);
                    Assert.True(parts != null);
                    body = parts[0];
                }
                else
                {
                    Assert.Equal(0, count);
                    Assert.True(parts == null);
                }
                return error;
            }
            finally { if (parts != null) Assert.Equal(0, KernelRuntime.MemoryFree(parts)); }
        }
    }

    // Generated by real Parasolid 38.0.150, transmitted as 37.1. The warped
    // B-surface is z=-x²*y²/81; its cylinder support is x²+y²=4. The wire covers
    // a proper subinterval of the closed icurve, with two distinct vertices.
    private const string RealPkWire = """
T51 : TRANSMIT FILE created by modeller version 380015017 SCH_3701000_371020 12
 1 11 0 0 0 0 0 0 0 0 0 1e3 1e-8 0 0 0 1 0 2 1 2 0 3 4 5 0 0 6 0 7 0 0 0 0 0 0 0
 13 2 3 0 1 0 0 8 0 6 0 51 3 11 0 1 9 0 10 +0 0 0 0 0 1 2 1 0 0 38 4 9 0 8 0 0 0
 +9 3 11 12 12 13 29 5 7 0 7 14 0 -1.370572989500076 1.456547177558224 -.0492003
769971487 19 6 1 0 1 0 0 2 0 V0 18 7 5 0 15 0 16 5 ?1 17 15 0 0 0 0 7 17 8 0 0 +
18 16 2 0 17 7 0 14 ?1 17 17 0 0 0 0 16 15 8 0 0 -29 14 4 0 16 0 5 .963360512426
637 -1.752694075729445 -.0351969393882781 16 8 6 0 ?15 0 0 4 0 0 2 124 9 10 0 1
 0 3 18 +19 20 40 20 11 0 1 20 .1150206995273626 .3430618124044215 ??2 0 0 1.888
16255462779 -.659425634398198 -.0191392710955855 1.565534616875 -1.2446290063171
66 -.0468727212281686 1.06628089292791 -1.692053503107055 -.0401870769004614 .45
7231118867623 -1.94703356518039 -.00978438243030455 -.213971458509536 -1.9885211
1252139 -.00223504920065032 -.850258317957071 -1.81026539290371 -.02924834236585
18 -1.400012320765106 -1.428273608838972 -.0493629970206592 -1.791168733736075 -
.889783438419883 -.0313585964579246 -1.983733650843505 -.254560017502928 -.00314
8198346151635 -1.956310521088725 .4157513019553405 -.00816691053526171 -1.714441
94545274 1.029897478233744 -.03849005554316985 -1.276241288075828 1.539872778709
51 -.047681536927189 -.704439800278641 1.871834546049245 -.02146533487258315 -.0
479063290573762 1.999426163587005 -.0001132691154926284 .611515933083102 1.90421
8544071425 -.01674033388259965 1.200547619276686 1.599589139075745 -.04552926676
97813 1.66221566292069 1.112222590105562 -.0421961808607671 1.93368949917927 .51
0729792320584 -.01204122975761756 2 0 0 41 1 12 H?2 0 0 204 80 13 4 .5 .83333333
3333333 0 0 .3900957276003005 .814693759104631 ??.2925618322804725 .760922436145
833 ??.2179910828154905 .677713482154651 ??.1754944058032685 .576205186477937 ??
.1685798145797685 .464338090248411 ??.1982891011827155 .358290280340488 ??.26195
4398526838 .2666646132058155 ??.351702760263353 .2014718777106545 ??.45757333041
6179 .1693777248594155 ??.569291883659224 .1739482464852125 ??.671649579705624 .
214259675757877 ??.756645463118252 .287293118654029 ??.811972424341541 .38259336
66202265 ??.833237693931167 .492015611823771 ??.817369757345238 .601919322180517
 ??.766598189845957 .700091269879448 ??.685370431684261 .777035943820115 ??.5851
21632053431 .822281583196545 ??.5 .833333333333333 ??141 18 4 18 18 9 126 19 FF2
 2 3 3 5 5 2 2 FFF2 3 21 22 23 24 25 125 20 0 1 0 1 0 1 0 1 2 BBBBBBBB????0 0 0
 0 45 27 21 -3 -3 -1 0 -3 1 3 -3 -1 -3 0 1 0 0 -1 3 0 1 -3 3 -1 0 3 1 3 3 -1 127
 2 22 3 3 127 2 23 3 3 128 2 24 0 1 128 2 25 0 1 141 10 4 10 10 3 1 0
""";
}
