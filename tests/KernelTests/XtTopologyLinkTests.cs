using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public unsafe class XtTopologyLinkTests : IDisposable
{
    public XtTopologyLinkTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Theory]
    [InlineData("block")]
    [InlineData("cylinder")]
    [InlineData("cone")]
    public void ExportedFaceChainsTerminate_AndFinOtherLinksClose(string kind)
    {
        int body;
        var error = kind switch
        {
            "block" => KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body),
            "cylinder" => KernelRuntime.BodyCreateSolidCyl(3, 5, null, &body),
            _ => KernelRuntime.BodyCreateSolidCone(2, 5, 0.25, null, &body),
        };
        Assert.Equal(0, error);
        Assert.Equal(0, XtWriter.WriteText([body], 0, out var text));
        var document = XtText.DecodeDocument(text);
        var nodes = document.Nodes.ToDictionary(node => node.Index);
        var faceUses = 0;
        foreach (var shell in document.Nodes.Where(node => node.Type == 13))
        foreach (var front in new[] { false, true })
        {
            var index = Field(document, shell, front ? "front_face" : "face").Pointer;
            var previous = 0;
            var seen = new HashSet<int>();
            while (index != 0)
            {
                Assert.True(seen.Add(index), "XT face chains must not wrap back to their head.");
                var face = nodes[index];
                Assert.Equal(shell.Index, Field(document, face, front ? "front_shell" : "shell").Pointer);
                Assert.Equal(previous, Field(document, face, front ? "previous_front" : "previous").Pointer);
                previous = index;
                index = Field(document, face, front ? "next_front" : "next").Pointer;
                faceUses++;
            }
        }
        Assert.Equal(2 * document.Nodes.Count(node => node.Type == 14), faceUses);

        foreach (var edge in document.Nodes.Where(node => node.Type == 16))
        {
            var primary = nodes[Field(document, edge, "halfedge").Pointer];
            var secondary = nodes[Field(document, primary, "other").Pointer];
            Assert.NotEqual(primary.Index, secondary.Index);
            Assert.Equal(primary.Index, Field(document, secondary, "other").Pointer);
            Assert.Equal('+', Field(document, primary, "sense").Character);
            Assert.Equal('-', Field(document, secondary, "sense").Character);
            Assert.Equal(edge.Index, Field(document, primary, "edge").Pointer);
            Assert.Equal(edge.Index, Field(document, secondary, "edge").Pointer);
        }

        int count;
        int* edges;
        Assert.Equal(0, KernelRuntime.BodyAskEdges(body, &count, &edges));
        for (var i = 0; i < count; i++)
        {
            int finCount;
            int* fins;
            Assert.Equal(0, KernelRuntime.EdgeAskFins(edges[i], &finCount, &fins));
            Assert.Equal(2, finCount);
        }
    }

    private static XtFieldValue Field(XtDocument document, XtNode node, string name)
    {
        var descriptor = document.Schema.GetNode(node.Type);
        var offset = 0;
        foreach (var field in document.Schema.Fields.Slice(descriptor.FieldOffset, descriptor.ParsedFieldCount))
        {
            if (!field.Transmit) continue;
            if (field.Name == name) return node.Fields[offset];
            offset += Math.Max(1, field.ElementCount);
        }
        throw new InvalidOperationException("Missing topology field: " + name);
    }
}
