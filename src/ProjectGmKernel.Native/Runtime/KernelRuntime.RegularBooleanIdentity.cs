using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int PreserveRegularBooleanBoundary(scoped ReadOnlySpan<BodyTag> inputs,scoped ReadOnlySpan<BodyTag> products,
        scoped ref SheetArrangement model,scoped ReadOnlySpan<SheetSourceEdge> sources,scoped Span<EdgeSlot> edgeProducts,
        scoped Span<VertexSlot> vertexProducts,PK_TOPOL_track_r_s* tracking)
    {
        Span<byte> claimedEdges=stackalloc byte[model.NE],claimedVertices=stackalloc byte[model.NP];claimedEdges.Clear();claimedVertices.Clear();
        for(var e=0;e<model.NE;e++)foreach(var source in sources)if(edgeProducts[e]==source.Source)claimedEdges[e]=1;
        for(var v=0;v<model.NP;v++)if(vertexProducts[v]>=0&&vertexProducts[v]==model.OriginalVertices[v])claimedVertices[v]=1;
        var maxEdges=0;var maxVertices=0;
        foreach(var tag in inputs){var body=Bodies[TagRec(tag).Slot];maxEdges=Math.Max(maxEdges,body.EdgeCountBody);maxVertices=Math.Max(maxVertices,body.VertexCountBody);}
        foreach(var tag in products){var body=Bodies[TagRec(tag).Slot];maxEdges=Math.Max(maxEdges,body.EdgeCountBody);maxVertices=Math.Max(maxVertices,body.VertexCountBody);}
        Span<EdgeSlot> oldEdgeBuffer=stackalloc EdgeSlot[maxEdges],newEdgeBuffer=stackalloc EdgeSlot[maxEdges];
        Span<VertexSlot> oldVertexBuffer=stackalloc VertexSlot[maxVertices],newVertexBuffer=stackalloc VertexSlot[maxVertices];
        Span<EdgeSlot> from=stackalloc EdgeSlot[maxEdges],to=stackalloc EdgeSlot[maxEdges];Span<EntityTag> previousTags=stackalloc EntityTag[maxEdges];
        Span<VertexSlot> vertexFrom=stackalloc VertexSlot[maxVertices],vertexTo=stackalloc VertexSlot[maxVertices];
        // Target first: a shared result keeps one input identity. A split
        // keeps it on the first surviving native fragment, never by XYZ rank.
        foreach(var input in inputs)foreach(var product in products)
        {
            var source=TagRec(input).Slot;var target=TagRec(product).Slot;if(source==target)continue;
            var oldEdges=oldEdgeBuffer[..Bodies[source].EdgeCountBody];var newEdges=newEdgeBuffer[..Bodies[target].EdgeCountBody];
            var oldVertices=oldVertexBuffer[..Bodies[source].VertexCountBody];var newVertices=newVertexBuffer[..Bodies[target].VertexCountBody];
            CollectProfileSlots(source,oldEdges,oldVertices);CollectProfileSlots(target,newEdges,newVertices);
            var edgeCount=0;var vertexCount=0;
            foreach(var range in sources)
            {
                if(Edges[range.Source].Body!=source||from[..edgeCount].Contains(range.Source))continue;
                for(var k=range.First;k<range.First+range.Count;k++)
                {
                    var e=model.CanonicalEdge(k);var replacement=edgeProducts[e];
                    if(claimedEdges[e]!=0||replacement<0||Edges[replacement].Body!=target
                        ||!RegularIdentityFragmentContained(model.Edges[e],Edges[range.Source]))continue;
                    claimedEdges[e]=1;from[edgeCount]=range.Source;to[edgeCount]=replacement;
                    previousTags[edgeCount++]=Edges[replacement].Header.Tag;break;
                }
            }
            for(var v=0;v<model.NP;v++)
            {
                var original=model.OriginalVertices[v];var replacement=vertexProducts[v];
                if(claimedVertices[v]!=0||original<0||replacement<0||Vertices[original].Body!=source||Vertices[replacement].Body!=target
                    ||vertexFrom[..vertexCount].Contains(original))continue;
                claimedVertices[v]=1;vertexFrom[vertexCount]=original;vertexTo[vertexCount++]=replacement;
            }
            if(edgeCount==0&&vertexCount==0)continue;
            if(!SnapshotProfileTopology(source)||!SnapshotProfileTopology(target))return ParasolidConstants.PK_ERROR_memory_full;
            PreserveLocalBoundaryHandles(source,target,oldEdges,oldVertices,newEdges,newVertices,from[..edgeCount],to[..edgeCount],vertexFrom[..vertexCount],vertexTo[..vertexCount]);
            for(var e=0;e<model.NE;e++)
            {
                var index=to[..edgeCount].IndexOf(edgeProducts[e]);if(index>=0)edgeProducts[e]=from[index];
            }
            for(var v=0;v<model.NP;v++)
            {
                var index=vertexTo[..vertexCount].IndexOf(vertexProducts[v]);if(index>=0)vertexProducts[v]=vertexFrom[index];
            }
            // Tracking was captured before moving origins. Its products must
            // now name the retained tags, not the temporary donor records.
            for(var r=0;r<tracking->n_track_records;r++)
            {
                ref var record=ref tracking->track_records[r];
                for(var p=0;p<record.n_product_topols;p++)
                {
                    var index=previousTags[..edgeCount].IndexOf(record.product_topols[p]);
                    if(index>=0)record.product_topols[p]=Edges[from[index]].Header.Tag;
                }
            }
            if(TraceFiniteBoundary!=null)for(var i=0;i<edgeCount;i++)
            {
                var retained=Edges[from[i]];var fin=retained.FirstFinEdge;var face=Loops[Fins[fin].Loop].Face;
                TryProfileEdgeRange(retained,out var first,out var last);
                TraceFiniteBoundary(new(){Stage=BoundaryTraceStage.PreservedIdentity,SourceEdge=from[i],SourceFin=fin,
                    Surface=Faces[face].SurfTag,Curve=retained.CurveTag,ProductCurve=retained.CurveTag,
                    Edge=-1,Face=-1,Source=face,Product=face,First=first,Last=last});
            }
        }
        return 0;
    }

    private static bool RegularIdentityFragmentContained(in ModelingEdge fragment,in EdgeRecord source)
    {
        if(!TryProfileEdgeRange(source,out var first,out var last))return false;
        if(fragment.Curve==source.CurveTag)return fragment.Start>=first&&fragment.End<=last;
        return BoundCoincidentLineStations(fragment.Curve,source.CurveTag,fragment.Start,fragment.End,out var a,out var b,out _)
            &&Math.Min(a.Lower,b.Lower)>=first&&Math.Max(a.Upper,b.Upper)<=last;
    }
}
