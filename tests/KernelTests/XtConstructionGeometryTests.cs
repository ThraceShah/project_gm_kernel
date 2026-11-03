using System.Text;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using Xunit;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class XtConstructionGeometryTests : IDisposable
{
    public XtConstructionGeometryTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ReflectedOffsetXt_ConvertsDistanceUsingSupportSense(bool negativeSupport, bool negativeOffset)
    {
        var input = XtText.DecodeDocument(ReflectedOffsetReference);
        var offsetNode = Assert.Single(input.Nodes, n => n.Type == (int)XtNodeTypes.OffsetSurf);
        var supportNode = Assert.Single(input.Nodes, n => n.Index == offsetNode.Fields[9].Pointer);
        supportNode.Fields[6] = XtFieldValue.Char(negativeSupport ? '-' : '+');
        offsetNode.Fields[6] = XtFieldValue.Char(negativeOffset ? '-' : '+');
        var body = Receive(XtText.Encode(input));
        Assert.True(KernelRuntime.TryResolveBodySlot(body, out var slot));
        Assert.False(KernelRuntime.Xt.IsOpaque(slot));
        var offset = FindOffset(body);
        var definition = new PK_OFFSET_sf_s();
        Assert.Equal(0, KernelRuntime.OffsetAsk(offset, &definition));
        var expectedDistance = negativeSupport ? -.1 : .1;
        Assert.Equal(expectedDistance, definition.offset_distance, 12);

        // Reflected quadratic section C(u)=(4-4u,6u(1-u),0), sweep +Z.
        // Its parameter normal is (6-12u,4,0)/sqrt((6-12u)^2+16).
        const double u = .31, v = .27;
        var normalX = 6 - 12 * u;
        var normalLength = Math.Sqrt(normalX * normalX + 16);
        var uv = new PK_UV_s(); uv.param[0] = u; uv.param[1] = v;
        var before = stackalloc PK_VECTOR_s[9];
        Assert.Equal(0, KernelRuntime.SurfEval(offset, uv, 2, 2, 0, before));
        Assert.Equal(4 - 4 * u + expectedDistance * normalX / normalLength, before[0].coord[0], 12);
        Assert.Equal(6 * u * (1 - u) + expectedDistance * 4 / normalLength, before[0].coord[1], 12);
        Assert.Equal(v, before[0].coord[2], 12);

        KernelRuntime.Xt.Drop(slot);
        var text = Transmit(body);
        var output = XtText.DecodeDocument(text);
        var writtenOffset = Assert.Single(output.Nodes, n => n.Type == (int)XtNodeTypes.OffsetSurf);
        Assert.Equal(.1, writtenOffset.Fields[10].Real, 12);
        Assert.Equal(negativeOffset ? '-' : '+', writtenOffset.Fields[6].Character);
        var received = Receive(text);
        var receivedOffset = FindOffset(received);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        var after = stackalloc PK_VECTOR_s[9];
        Assert.Equal(0, KernelRuntime.SurfEval(receivedOffset, uv, 2, 2, 0, after));
        for (var i = 0; i < 9; i++)
            for (var axis = 0; axis < 3; axis++) Assert.Equal(before[i].coord[axis], after[i].coord[axis], 11);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transmit_SeparatesConstructionChainsAndRetainsSharedDefiningGeometry(bool nested)
    {
        var body = CreateWire(out var section);
        var sweptDefinition = new PK_SWEPT_sf_s { curve = section };
        sweptDefinition.direction.coord[2] = 1;
        int swept;
        Assert.Equal(0, KernelRuntime.SweptCreate(&sweptDefinition, &swept));
        if (nested)
        {
            var offsetDefinition = new PK_OFFSET_sf_s { underlying_surface = swept, offset_distance = .1 };
            int offset;
            Assert.Equal(0, KernelRuntime.OffsetCreate(&offsetDefinition, &offset));
            var trimmedDefinition = new PK_TRCURVE_sf_s { basis_curve = section };
            trimmedDefinition.t_int.value[0] = .2;
            trimmedDefinition.t_int.value[1] = .8;
            int trimmed;
            Assert.Equal(0, KernelRuntime.TrCurveCreate(&trimmedDefinition, &trimmed));
        }

        var document = XtText.DecodeDocument(Transmit(body));
        var nodes = document.Nodes.ToDictionary(n => n.Index);
        var root = Assert.Single(document.Nodes, n => n.Type == (int)XtNodeTypes.Body);
        // SCH_3701000_371020 BODY uses fields 4/5 for construction and 21/22
        // for boundary geometry. Independent PK wire/SWEPT/OFFSET fixtures
        // retain SWEPT on the construction chain even when OFFSET references it.
        var constructionSurfaces = ReadChain(nodes, root.Fields[4].Pointer);
        var constructionCurves = ReadChain(nodes, root.Fields[5].Pointer);
        var boundarySurfaces = ReadChain(nodes, root.Fields[21].Pointer);
        var boundaryCurves = ReadChain(nodes, root.Fields[22].Pointer);
        Assert.Equal(nested ? 2 : 1, constructionSurfaces.Length);
        Assert.Equal(nested ? 1 : 0, constructionCurves.Length);
        Assert.Empty(boundarySurfaces);
        var boundary = Assert.Single(boundaryCurves);
        Assert.Equal((int)XtNodeTypes.BCurve, boundary.Type);
        Assert.Equal((int)XtNodeTypes.Edge, nodes[boundary.Fields[2].Pointer].Type);

        foreach (var node in constructionSurfaces.Concat(constructionCurves))
            Assert.Equal(root.Index, node.Fields[2].Pointer);
        Assert.Empty(constructionSurfaces.Select(n => n.Index).Intersect(boundarySurfaces.Select(n => n.Index)));
        Assert.Empty(constructionCurves.Select(n => n.Index).Intersect(boundaryCurves.Select(n => n.Index)));

        var sweep = Assert.Single(constructionSurfaces, n => n.Type == (int)XtNodeTypes.SweptSurf);
        Assert.Equal(boundary.Index, sweep.Fields[7].Pointer);
        if (nested)
        {
            var offset = Assert.Single(constructionSurfaces, n => n.Type == (int)XtNodeTypes.OffsetSurf);
            Assert.Equal(sweep.Index, offset.Fields[9].Pointer);
            AssertOwners(nodes, sweep, [offset.Index]);
            var trimmed = Assert.Single(constructionCurves);
            Assert.Equal((int)XtNodeTypes.TrimmedCurve, trimmed.Type);
            Assert.Equal(boundary.Index, trimmed.Fields[7].Pointer);
            AssertOwners(nodes, boundary, [sweep.Index, trimmed.Index]);
        }
        else
        {
            Assert.Equal(0, sweep.Fields[5].Pointer);
            AssertOwners(nodes, boundary, [sweep.Index]);
        }
    }

    private static XtNode[] ReadChain(Dictionary<int, XtNode> nodes, int head)
    {
        var chain = new List<XtNode>();
        var seen = new HashSet<int>();
        var previous = 0;
        while (head != 0)
        {
            Assert.True(seen.Add(head), "Geometry chains must terminate without repeating a member.");
            Assert.True(nodes.TryGetValue(head, out var node));
            Assert.Equal(previous, node!.Fields[4].Pointer);
            chain.Add(node);
            previous = head;
            head = node.Fields[3].Pointer;
        }
        return chain.ToArray();
    }

    private static void AssertOwners(Dictionary<int, XtNode> nodes, XtNode shared, int[] expectedOwners)
    {
        var head = shared.Fields[5].Pointer;
        Assert.NotEqual(0, head);
        var current = head;
        var owners = new List<int>();
        var seen = new HashSet<int>();
        do
        {
            Assert.True(seen.Add(current), "Only the owner-ring head may repeat.");
            var owner = nodes[current];
            Assert.Equal((int)XtNodeTypes.GeometricOwner, owner.Type);
            Assert.Equal(shared.Index, owner.Fields[3].Pointer);
            Assert.Equal(current, nodes[owner.Fields[1].Pointer].Fields[2].Pointer);
            Assert.Equal(current, nodes[owner.Fields[2].Pointer].Fields[1].Pointer);
            owners.Add(owner.Fields[0].Pointer);
            current = owner.Fields[1].Pointer;
        }
        while (current != head);
        Assert.Equal(expectedOwners.Order(), owners.Order());
    }

    private static int CreateWire(out int section)
    {
        var poles = stackalloc double[9] { -2, 0, 0, 0, 3, 0, 2, 0, 0 };
        var knots = stackalloc double[2] { 0, 1 };
        var multiplicities = stackalloc int[2] { 3, 3 };
        var definition = new PK_BCURVE_sf_s
        {
            degree = 2, n_vertices = 3, vertex_dim = 3, vertex = poles,
            n_knots = 2, knot = knots, knot_mult = multiplicities,
            form = ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            knot_type = ParasolidConstants.PK_knot_bezier_ends_c,
            self_intersecting = ParasolidConstants.PK_self_intersect_false_c,
        };
        int curve, body;
        Assert.Equal(0, KernelRuntime.BCurveCreate(&definition, &curve));
        var interval = new PK_INTERVAL_s(); interval.value[1] = 1;
        Assert.Equal(0, KernelRuntime.CurveMakeWireBody(curve, interval, &body));
        int count; int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &edges));
        Assert.Equal(1, count);
        int attached;
        try { Assert.Equal(0, KernelRuntime.EdgeAskCurve(edges[0], &attached)); }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(edges)); }
        section = attached;
        if (curve != section) Assert.Equal(0, KernelRuntime.EntityDelete(1, &curve));
        return body;
    }

    private static string Transmit(int body)
    {
        var options = new PK_PART_transmit_o_s
        {
            o_t_version = 4, transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            transmit_version = 371, transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
        };
        var block = new PK_MEMORY_block_s();
        Assert.Equal(0, KernelRuntime.PartTransmitB(1, &body, &options, &block));
        try
        {
            using var stream = new MemoryStream();
            for (var current = &block; current != null; current = current->next)
                stream.Write(new ReadOnlySpan<byte>(current->bytes, checked((int)current->n_bytes)));
            return Encoding.ASCII.GetString(stream.ToArray());
        }
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
            int count; int* parts;
            Assert.Equal(0, KernelRuntime.PartReceiveB(block, &options, &count, &parts));
            try { Assert.Equal(1, count); return parts[0]; }
            finally { Assert.Equal(0, KernelRuntime.MemoryFree(parts)); }
        }
    }

    private static int FindOffset(int body)
    {
        int count; int* geometry;
        Assert.Equal(0, KernelRuntime.PartAskGeoms(body, &count, &geometry));
        var offset = 0;
        try
        {
            for (var i = 0; i < count; i++)
            {
                int cls; Assert.Equal(0, KernelRuntime.EntityAskClass(geometry[i], &cls));
                if (cls != ParasolidConstants.PK_CLASS_offset) continue;
                Assert.Equal(0, offset); offset = geometry[i];
            }
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryFree(geometry)); }
        Assert.NotEqual(0, offset);
        return offset;
    }

    // Real PK 38.0.150 output from GeometryTransformOracle's independent
    // reflection-body-construction-offset-trim-wire producer. The four sense
    // combinations above were also checked with real PK receive/ask/eval:
    // only the support sense changes the public offset distance and position.
    private const string ReflectedOffsetReference = """
        T51 : TRANSMIT FILE created by modeller version 380015017 SCH_3701000_371020 12
         1 12 0 0 0 2 3 0 0 0 0 1e3 1e-8 0 4 0 1 0 2 1 5 0 0 6 7 0 0 8 0 9 0 0 0 0 0 0 0
         60 2 11 0 1 10 0 0 -VF10 .1 ?133 3 12 0 1 0 0 0 +6 3.6 .54 0 .8 .96 0 .1 .8 13
         5 3 0 1 0 0 11 0 8 0 134 6 9 0 11 0 0 12 +13 14 29 7 7 0 9 15 0 0 0 0 19 8 1 0
         1 0 0 5 0 V0 18 9 5 0 16 0 17 7 ?1 17 16 0 0 0 0 9 18 11 0 0 +18 17 2 0 18 9 0
         15 ?1 17 18 0 0 0 0 17 16 11 0 0 -29 15 4 0 17 0 7 4 0 0 16 11 6 0 ?16 0 0 6 0
         0 5 141 12 10 19 19 6 136 13 2 3 3 2 1 FFF2 20 21 22 135 14 2 0 45 9 20 4 0 0 2
         3 0 0 0 0 127 2 21 3 3 128 2 22 0 1 67 10 10 0 1 0 2 23 -6 0 0 1 ?141 19 3 12 1
        2 6 141 23 2 23 23 10 1 0
        """;
}
