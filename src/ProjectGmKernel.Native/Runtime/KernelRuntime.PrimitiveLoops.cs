namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool AddConnectedPolygonLoop(FaceSlot faceSlot, ReadOnlySpan<EdgeSlot> edges)
    {
        var loop = AddLoop(faceSlot);
        if (loop < 0) return false;
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = Edges[edges[i]];
            var next = Edges[edges[(i + 1) % edges.Length]];
            // The fin points to the shared vertex with the next loop edge.
            // Its sense follows that endpoint, independently of edge-use order.
            var vertex = edge.EndVertex == next.StartVertex || edge.EndVertex == next.EndVertex
                ? edge.EndVertex : edge.StartVertex;
            var slot = TryAllocateFins();
            if (slot < 0) return false;
            ref var fin = ref Fins[slot];
            fin.Loop = loop; fin.Edge = edges[i]; fin.Curve = -1; fin.Vertex = -1;
            fin.Other = fin.NextOfEdge = fin.PrevOfEdge = -1;
            fin.NextInLoop = fin.PrevInLoop = fin.NextAtVertex = fin.PrevAtVertex = -1;
            AppendFinToLoop(loop, slot);
            AppendFinToEdge(edges[i], slot);
            AppendFinToVertex(vertex, slot);
        }
        return true;
    }
}
