using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A tool hidden inside another tool cannot leave an imprint through a
    // surviving source face. Cancel common cell edges before building shells.
    private static int MergeRegularBooleanCells(scoped ref SheetArrangement model,Span<byte> selected,Span<byte> reversed,bool mergeCoincidentGeometry=false)
    {
        Span<BufferOffset> labels=stackalloc BufferOffset[model.NF],first=stackalloc BufferOffset[model.NE];labels.Fill(-1);first.Fill(-1);
        for(var f=0;f<model.NF;f++)if(selected[f]!=0)
        {
            labels[f]=f;var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                for(var k=0;k<loop.CoedgeCount;k++)
                {
                    var edge=model.Coedges[loop.FirstCoedge+k].Edge;var other=first[edge];
                    if(other<0){first[edge]=f;continue;}
                    if(model.Sources[f]!=model.Sources[other]||reversed[f]!=reversed[other]||!model.CutEdges.IsEmpty&&(model.CutEdges[edge]&2)!=0)continue;
                    var from=Math.Max(labels[f],labels[other]);var to=Math.Min(labels[f],labels[other]);
                    for(var j=0;j<=f;j++)if(labels[j]==from)labels[j]=to;
                }
            }
        }
        var scratch=CommandScratch.Current;var memory=scratch.Take(50000);if(memory.IsEmpty)return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes=MemoryMarshal.AsBytes(memory);var cursor=0;
            var coedges=ModelSpan<ModelingCoedge>(bytes,ref cursor,model.Coedges.Length);var loops=ModelSpan<ModelingLoop>(bytes,ref cursor,model.Loops.Length);
            var boundary=ModelSpan<ModelingCoedge>(bytes,ref cursor,model.Coedges.Length);var used=ModelSpan<byte>(bytes,ref cursor,model.Coedges.Length);
            if(cursor>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;
            Span<BufferOffset> at=stackalloc BufferOffset[model.NE];Span<ModelingCoedge> normalized=stackalloc ModelingCoedge[SheetEdgeLimit];
            var nf=0;var nc=0;var nl=0;
            for(var seed=0;seed<model.NF;seed++)if(labels[seed]==seed)
            {
                at.Fill(-1);var count=0;var face=model.Faces[seed];
                for(var f=seed;f<model.NF;f++)if(labels[f]==seed)
                {
                    var cell=model.Faces[f];
                    for(var l=0;l<cell.LoopCount;l++)
                    {
                        var loop=model.Loops[cell.FirstLoop+l];
                        for(var k=0;k<loop.CoedgeCount;k++)
                        {
                            var coedge=model.Coedges[loop.FirstCoedge+k];var previous=at[coedge.Edge];
                            if(previous<0){at[coedge.Edge]=count;boundary[count]=coedge;used[count++]=0;}
                            else{if(used[previous]!=0||boundary[previous].Forward==coedge.Forward)return ParasolidConstants.PK_ERROR_non_manifold;used[previous]=1;}
                        }
                    }
                }
                var firstLoop=nl;var outer=-1;
                for(var start=0;start<count;start++)if(used[start]==0)
                {
                    var begin=nc;var current=start;
                    do
                    {
                        if(used[current]!=0||nc==coedges.Length)return ParasolidConstants.PK_ERROR_bad_loop;
                        used[current]=1;var coedge=boundary[current];coedges[nc++]=coedge;var edge=model.Edges[coedge.Edge];
                        var vertex=coedge.Forward!=0?edge.EndVertex:edge.StartVertex;
                        if(vertex<0){current=start;continue;}
                        var next=-1;
                        for(var j=0;j<count;j++)if(used[j]==0||j==start)
                        {
                            var candidate=boundary[j];var record=model.Edges[candidate.Edge];
                            if((candidate.Forward!=0?record.StartVertex:record.EndVertex)!=vertex)continue;
                            if(next>=0)return ParasolidConstants.PK_ERROR_non_manifold;next=j;
                        }
                        if(next<0)return ParasolidConstants.PK_ERROR_bad_loop;current=next;
                    }while(current!=start);
                    var length=nc-begin;if(nl==loops.Length||length>normalized.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    var reverse=LocalSenseSign(face.Sense)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(face.Surface)].Sense)<0;
                    for(var i=0;i<length;i++){var value=coedges[begin+(reverse?length-1-i:i)];value.Forward^=reverse?(byte)1:(byte)0;normalized[i]=value;}
                    var error=SheetCycleWitness(face.Surface,model.Edges[..model.NE],normalized[..length],out _,out var positive);if(error!=0)return error;
                    if(positive!=0){if(outer>=0)return ParasolidConstants.PK_ERROR_bad_loop;outer=nl;}
                    loops[nl++]=new(){FirstCoedge=begin,CoedgeCount=length};
                }
                if(outer<0)return ParasolidConstants.PK_ERROR_bad_loop;
                (loops[firstLoop],loops[outer])=(loops[outer],loops[firstLoop]);
                face.FirstLoop=firstLoop;face.LoopCount=nl-firstLoop;model.Faces[nf]=face;model.Sources[nf]=model.Sources[seed];
                model.Witnesses[nf]=model.Witnesses[seed];reversed[nf]=reversed[seed];selected[nf++]=1;
            }
            coedges[..nc].CopyTo(model.Coedges);loops[..nl].CopyTo(model.Loops);model.NF=nf;model.NC=nc;model.NL=nl;
            return MergeRegularBooleanEdges(ref model,mergeCoincidentGeometry);
        }
        finally{scratch.Return(memory);}
    }

    private static int MergeRegularBooleanEdges(scoped ref SheetArrangement model,bool mergeCoincidentGeometry)
    {
        Span<BufferOffset> first=stackalloc BufferOffset[model.NP],second=stackalloc BufferOffset[model.NP];
        Span<byte> active=stackalloc byte[model.NE];
        Span<Geometry.Intersection.OperationIntersectionPoint> roots=stackalloc Geometry.Intersection.OperationIntersectionPoint[IntersectionPointCapacity];
        while(true)
        {
            first.Fill(-1);second.Fill(-1);active.Clear();
            for(var l=0;l<model.NL;l++)
            {
                var loop=model.Loops[l];for(var k=0;k<loop.CoedgeCount;k++)active[model.Coedges[loop.FirstCoedge+k].Edge]=1;
            }
            for(var e=0;e<model.NE;e++)if(active[e]!=0)
            {
                var edge=model.Edges[e];
                for(var s=0;s<2;s++)
                {
                    var vertex=s==0?edge.StartVertex:edge.EndVertex;if(vertex<0)continue;
                    if(first[vertex]<0)first[vertex]=e;else if(second[vertex]<0)second[vertex]=e;else second[vertex]=model.NE;
                }
            }
            var changed=false;
            for(var v=0;v<model.NP;v++)
            {
                var a=first[v];var b=second[v];if(a<0||b<0||b>=model.NE||a==b)continue;
                if(!model.CutEdges.IsEmpty&&((model.CutEdges[a]|model.CutEdges[b])&2)!=0)continue;
                var ea=model.Edges[a];var eb=model.Edges[b];
                if(ea.Curve!=eb.Curve)
                {
                    if(!mergeCoincidentGeometry)continue;
                    var error=ReparameterizeRegularEdge(ea,eb,roots,out var handled,out var mapped,out var flip);if(error!=0)return error;
                    if(handled){eb=model.Edges[b]=mapped;if(flip!=0)for(var c=0;c<model.NC;c++)if(model.Coedges[c].Edge==b)model.Coedges[c].Forward^=1;}
                    else
                    {
                        error=ReparameterizeRegularEdge(eb,ea,roots,out handled,out mapped,out flip);if(error!=0)return error;if(!handled)continue;
                        ea=model.Edges[a]=mapped;if(flip!=0)for(var c=0;c<model.NC;c++)if(model.Coedges[c].Edge==a)model.Coedges[c].Forward^=1;
                    }
                }
                if(ea.Start>eb.Start){(a,b)=(b,a);(ea,eb)=(eb,ea);}
                if(Math.Abs(ea.End-eb.Start)>1e-12)continue;
                var positive=LocalSenseSign(Curves[GetCurveSlotByTag(ea.Curve)].Sense)>0;
                if((positive?ea.EndVertex:ea.StartVertex)!=v||(positive?eb.StartVertex:eb.EndVertex)!=v)continue;
                var merged=ea;merged.End=eb.End;if(positive)merged.EndVertex=eb.EndVertex;else merged.StartVertex=eb.StartVertex;
                model.Edges[a]=merged;
                for(var l=0;l<model.NL;l++)
                {
                    var loop=model.Loops[l];var write=loop.FirstCoedge;var previous=-1;
                    for(var k=0;k<loop.CoedgeCount;k++)
                    {
                        var coedge=model.Coedges[loop.FirstCoedge+k];if(coedge.Edge==b)coedge.Edge=a;
                        if(coedge.Edge==previous)continue;model.Coedges[write++]=coedge;previous=coedge.Edge;
                    }
                    if(write-loop.FirstCoedge>1&&model.Coedges[write-1].Edge==model.Coedges[loop.FirstCoedge].Edge)write--;
                    loop.CoedgeCount=write-loop.FirstCoedge;model.Loops[l]=loop;
                }
                changed=true;break;
            }
            if(!changed)return 0;
        }
    }
}
