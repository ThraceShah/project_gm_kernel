namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>
    /// Spin keeps the input boundary as its base (or full-turn seam). Move the
    /// produced records into the original slots while retaining each slot's
    /// published header. This preserves real edge/vertex identities without
    /// retargeting the tag map or exposing the temporary replacement body.
    /// </summary>
    private static int PreserveProfileBaseHandles(BodySlot source,BodySlot product,
        Span<int> productBases,Span<int> productBaseVertices)
    {
        if(!SnapshotProfileTopology(source)||!SnapshotProfileTopology(product))
            return Generated.ParasolidConstants.PK_ERROR_memory_full;
        Span<int> sourceEdges=stackalloc int[Bodies[source].EdgeCountBody];
        Span<int> sourceVertices=stackalloc int[Bodies[source].VertexCountBody];
        Span<int> outputEdges=stackalloc int[Bodies[product].EdgeCountBody];
        Span<int> outputVertices=stackalloc int[Bodies[product].VertexCountBody];
        CollectProfileSlots(source,sourceEdges,sourceVertices);
        CollectProfileSlots(product,outputEdges,outputVertices);
        if(sourceEdges.Length!=productBases.Length||sourceVertices.Length!=productBaseVertices.Length)
            return Generated.ParasolidConstants.PK_ERROR_bad_parameter;
        for(var i=0;i<sourceEdges.Length;i++)
        {
            var original=Edges[sourceEdges[i]];var replacement=Edges[productBases[i]];
            Edges[sourceEdges[i]]=replacement;Edges[sourceEdges[i]].Header=original.Header;
            Edges[productBases[i]]=original;Edges[productBases[i]].Header=replacement.Header;
        }
        for(var i=0;i<sourceVertices.Length;i++)
        {
            var original=Vertices[sourceVertices[i]];var replacement=Vertices[productBaseVertices[i]];
            Vertices[sourceVertices[i]]=replacement;Vertices[sourceVertices[i]].Header=original.Header;
            Vertices[productBaseVertices[i]]=original;Vertices[productBaseVertices[i]].Header=replacement.Header;
        }
        RemapProfileBaseTopology(source,sourceEdges,sourceVertices,sourceEdges,productBases,sourceVertices,productBaseVertices);
        RemapProfileBaseTopology(product,outputEdges,outputVertices,productBases,sourceEdges,productBaseVertices,sourceVertices);
        RebuildBoundaryGeometryLinks(source,resetOwners:true);
        RebuildBoundaryGeometryLinks(product,resetOwners:true);
        sourceEdges.CopyTo(productBases);
        sourceVertices.CopyTo(productBaseVertices);
        return 0;
    }

    private static void CollectProfileSlots(BodySlot body,Span<int> edges,Span<int> vertices)
    {
        var edge=Bodies[body].FirstEdgeBody;
        for(var i=0;i<edges.Length;i++,edge=Edges[edge].NextInBody)edges[i]=edge;
        var vertex=Bodies[body].FirstVertexBody;
        for(var i=0;i<vertices.Length;i++,vertex=Vertices[vertex].NextInBody)vertices[i]=vertex;
    }

    private static int ProfileMappedSlot(int slot,ReadOnlySpan<int> from,ReadOnlySpan<int> to)
    {
        if(slot<0)return slot;
        var index=from.IndexOf(slot);
        return index<0?slot:to[index];
    }

    private static void RemapProfileBaseTopology(BodySlot body,ReadOnlySpan<int> oldEdges,ReadOnlySpan<int> oldVertices,
        ReadOnlySpan<int> edgeFrom,ReadOnlySpan<int> edgeTo,ReadOnlySpan<int> vertexFrom,ReadOnlySpan<int> vertexTo)
    {
        ref var record=ref Bodies[body];
        record.FirstEdgeBody=ProfileMappedSlot(record.FirstEdgeBody,edgeFrom,edgeTo);
        record.LastEdgeBody=ProfileMappedSlot(record.LastEdgeBody,edgeFrom,edgeTo);
        record.FirstVertexBody=ProfileMappedSlot(record.FirstVertexBody,vertexFrom,vertexTo);
        record.LastVertexBody=ProfileMappedSlot(record.LastVertexBody,vertexFrom,vertexTo);
        for(var i=0;i<oldEdges.Length;i++)
        {
            var slot=ProfileMappedSlot(oldEdges[i],edgeFrom,edgeTo);
            ref var edge=ref Edges[slot];
            edge.Body=body;
            edge.PrevInBody=ProfileMappedSlot(edge.PrevInBody,edgeFrom,edgeTo);
            edge.NextInBody=ProfileMappedSlot(edge.NextInBody,edgeFrom,edgeTo);
            edge.StartVertex=ProfileMappedSlot(edge.StartVertex,vertexFrom,vertexTo);
            edge.EndVertex=ProfileMappedSlot(edge.EndVertex,vertexFrom,vertexTo);
            var fin=edge.FirstFinEdge;
            for(var j=0;j<edge.FinCount;j++,fin=Fins[fin].NextOfEdge)
            {
                Fins[fin].Edge=ProfileMappedSlot(Fins[fin].Edge,edgeFrom,edgeTo);
                Fins[fin].Vertex=ProfileMappedSlot(Fins[fin].Vertex,vertexFrom,vertexTo);
            }
        }
        for(var i=0;i<oldVertices.Length;i++)
        {
            var slot=ProfileMappedSlot(oldVertices[i],vertexFrom,vertexTo);
            ref var vertex=ref Vertices[slot];
            vertex.Body=body;
            vertex.PrevInBody=ProfileMappedSlot(vertex.PrevInBody,vertexFrom,vertexTo);
            vertex.NextInBody=ProfileMappedSlot(vertex.NextInBody,vertexFrom,vertexTo);
        }
    }
}
