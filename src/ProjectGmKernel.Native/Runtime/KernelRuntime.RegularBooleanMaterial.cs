namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // An open source-edge fragment not occupied by a cut connects two cells
    // of the same input body without crossing any other body's boundary.
    // Its two opposite uses supply connectivity; XYZ proximity does not.
    // A component therefore needs one material query per other input body.
    private static void ReadRegularMaterialComponents(scoped ref SheetArrangement model,
        ReadOnlySpan<FaceSlot> sources,ReadOnlySpan<BufferOffset> owners,
        ReadOnlySpan<SheetSourceEdge> sourceEdges,Span<BufferOffset> labels,bool enabled)
    {
        for(var f=0;f<model.NF;f++)labels[f]=f;
        if(!enabled)return;
        Span<BufferOffset> first=stackalloc BufferOffset[model.NE],second=stackalloc BufferOffset[model.NE],cellOwners=stackalloc BufferOffset[model.NF];
        Span<byte> forward=stackalloc byte[model.NE],blocked=stackalloc byte[model.NE];
        first.Fill(-1);second.Fill(-1);blocked.Clear();
        for(var e=0;e<model.NE;e++)blocked[model.CanonicalEdge(e)]|=model.CutEdges[e];
        for(var f=0;f<model.NF;f++)
        {
            var source=sources.IndexOf(model.Sources[f]);
            // The caller independently rejects a missing source. Do not
            // derive any connectivity from an unbound occurrence.
            cellOwners[f]=source<0?-1:owners[source];
            var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                for(var k=0;k<loop.CoedgeCount;k++)
                {
                    var use=model.Coedges[loop.FirstCoedge+k];var edge=model.CanonicalEdge(use.Edge);
                    if(first[edge]<0){first[edge]=f;forward[edge]=use.Forward;}
                    else if(second[edge]!=-1||first[edge]==f||forward[edge]==use.Forward)second[edge]=-2;
                    else second[edge]=f;
                }
            }
        }
        foreach(ref readonly var source in sourceEdges)
        for(var fragment=source.First;fragment<source.First+source.Count;fragment++)
        {
            var edge=model.CanonicalEdge(fragment);var a=first[edge];var b=second[edge];
            if(!(model.Edges[fragment].End>model.Edges[fragment].Start)
                ||blocked[edge]!=0||a<0||b<0||cellOwners[a]<0||cellOwners[a]!=cellOwners[b])continue;
            // Minimal representatives are visited before every member by
            // the classification loop. Compression keeps long chains cheap.
            while(labels[a]!=a){labels[a]=labels[labels[a]];a=labels[a];}
            while(labels[b]!=b){labels[b]=labels[labels[b]];b=labels[b];}
            labels[Math.Max(a,b)]=Math.Min(a,b);
        }
        for(var f=0;f<model.NF;f++){var root=f;while(labels[root]!=root)root=labels[root];labels[f]=root;}
    }

#if KERNEL_TEST_HOOKS
    internal static void TestRegularMaterialComponents(ReadOnlySpan<BufferOffset> cellOwners,
        ReadOnlySpan<ModelingCoedge> uses,ReadOnlySpan<ModelingLoop> loops,ReadOnlySpan<byte> cuts,
        BufferCount sourceEdgeCount,Span<BufferOffset> labels,bool enabled=true)
    {
        Span<ModelingFace> faces=stackalloc ModelingFace[cellOwners.Length];
        Span<FaceSlot> sources=stackalloc FaceSlot[cellOwners.Length];
        for(var f=0;f<faces.Length;f++){sources[f]=f;faces[f]=new(){FirstLoop=f,LoopCount=1};}
        var model=new SheetArrangement{Faces=faces,Sources=sources,Edges=stackalloc ModelingEdge[cuts.Length],
            CutEdges=stackalloc byte[cuts.Length],Coedges=stackalloc ModelingCoedge[uses.Length],
            Loops=stackalloc ModelingLoop[loops.Length],NF=faces.Length,NE=cuts.Length};
        cuts.CopyTo(model.CutEdges);uses.CopyTo(model.Coedges);loops.CopyTo(model.Loops);
        for(var e=0;e<model.NE;e++)model.Edges[e]=new(){Start=0,End=1};
        Span<SheetSourceEdge> edges=stackalloc SheetSourceEdge[1];edges[0]=new(){First=0,Count=sourceEdgeCount};
        ReadRegularMaterialComponents(ref model,sources,cellOwners,edges,labels,enabled);
    }
#endif
}
