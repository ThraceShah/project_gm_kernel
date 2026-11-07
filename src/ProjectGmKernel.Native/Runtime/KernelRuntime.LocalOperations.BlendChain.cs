using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Normalize a genuine subdivision chain, solve its rolling branch once,
    // then restore the prescribed partitions with shared exact sections. No
    // source topology is published until the complete replacement is ready.
    private static int BuildRegularBlendChain(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;if(selected.Length<2||faces.Length+selected.Length>products.Length||selected.Length>64)return ParasolidConstants.PK_ERROR_not_implemented;
        if(!CommonBlendChainFaces(selected))return BuildRegularSupportChain(body,faces,selected,descriptions,products,out made);
        Span<EdgeSlot> ordered=stackalloc EdgeSlot[selected.Length];selected.CopyTo(ordered);
        Span<double> lower=stackalloc double[selected.Length],upper=stackalloc double[selected.Length];
        var first=Edges[selected[0]];var curve=first.CurveTag;
        if(!LocalEdgeFaces(selected[0],out var fa,out var fb))return ParasolidConstants.PK_ERROR_not_implemented;
        var primaryFin=first.FirstFinEdge;if(Fins[primaryFin].Sense!='+')primaryFin=Fins[primaryFin].Other;
        var primaryFace=Loops[Fins[primaryFin].Loop].Face;
        var positive=LocalSenseSign(Curves[GetCurveSlotByTag(curve)].Sense)>0;
        for(var i=0;i<ordered.Length;i++)
        {
            var e=Edges[ordered[i]];
            if(e.CurveTag!=curve||e.PendingBlendKind!=1||e.PendingBlendFirst!=first.PendingBlendFirst||e.StartVertex<0||e.EndVertex<0
                ||!LocalEdgeFaces(ordered[i],out var a,out var b)||!(a==fa&&b==fb||a==fb&&b==fa)
                ||!TryProfileEdgeRange(e,out lower[i],out upper[i]))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        for(var i=1;i<ordered.Length;i++)for(var j=i;j>0&&lower[j]<lower[j-1];j--)
        {(ordered[j],ordered[j-1])=(ordered[j-1],ordered[j]);(lower[j],lower[j-1])=(lower[j-1],lower[j]);(upper[j],upper[j-1])=(upper[j-1],upper[j]);}
        Span<KernelVector3> junctions=stackalloc KernelVector3[ordered.Length-1],tangents=stackalloc KernelVector3[ordered.Length-1];
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        for(var i=0;i<ordered.Length-1;i++)
        {
            var end=positive?Edges[ordered[i]].EndVertex:Edges[ordered[i]].StartVertex;
            var start=positive?Edges[ordered[i+1]].StartVertex:Edges[ordered[i+1]].EndVertex;
            if(end!=start||Math.Abs(upper[i]-lower[i+1])>1e-10*Math.Max(1,upper[i]-lower[0]))return ParasolidConstants.PK_ERROR_not_implemented;
            var degree=0;var edge=Bodies[body].FirstEdgeBody;
            for(var k=0;k<Bodies[body].EdgeCountBody;k++,edge=Edges[edge].NextInBody)if(Edges[edge].StartVertex==end||Edges[edge].EndVertex==end)degree++;
            if(degree!=2)return ParasolidConstants.PK_ERROR_not_implemented;
            var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve)],upper[i],1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            junctions[i]=jet[0];tangents[i]=jet[1];if(!ProfileNormalize(ref tangents[i]))return ParasolidConstants.PK_ERROR_at_singularity;
        }
        for(var i=0;i<ordered.Length;i++)descriptions[i]=new(){Edge=Edges[ordered[i]].Header.Tag,First=Faces[fa].Header.Tag,Second=Faces[fb].Header.Tag};
        Span<FaceSlot> workingFaces=stackalloc FaceSlot[faces.Length];
        var error=CollapseBlendChain(body,ordered,fa,fb,workingFaces,out var working,out var merged);if(error!=0)return error;
        var workingSlot=TagRec(working).Slot;Edges[merged].PendingBlendKind=1;Edges[merged].PendingBlendFirst=Edges[merged].PendingBlendSecond=first.PendingBlendFirst;
        if(!LocalEdgeFaces(merged,out var workingFirst,out _))return ParasolidConstants.PK_ERROR_not_implemented;
        var primarySide=workingFirst==workingFaces[faces.IndexOf(primaryFace)]?0:1;
        Span<EdgeSlot> one=stackalloc EdgeSlot[1]{merged};Span<LocalBlendProduct> ignored=stackalloc LocalBlendProduct[1];Span<FaceSlot> wholeProducts=stackalloc FaceSlot[faces.Length+1];
        error=BuildRegularLocalBlend(workingSlot,workingFaces,one,ignored,wholeProducts,out var count);if(error!=0)return error;
        if(count!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var blendFace=TagRec(ignored[0].Result).Slot;var blend=Faces[blendFace].SurfTag;
        Span<SheetCutCurve> sections=stackalloc SheetCutCurve[ordered.Length-1];sections.Clear();
        for(var i=0;i<sections.Length;i++)
        {
            error=BlendChainSection(blend,primarySide,junctions[i],tangents[i],out sections[i]);if(error!=0)return error;
        }
        error=PartitionBlendChain(working,workingFaces,blendFace,sections,products,descriptions[..selected.Length],out var output);if(error!=0)return error;
        error=ReplaceLocalBody(body,output,faces,products[..(faces.Length+selected.Length)]);if(error!=0)return error;
        error=EntityDelete(1,&working);if(error!=0)return error;
        foreach(var section in sections)if(IsValidTag(section.Curve)){var tag=section.Curve;error=EntityDelete(1,&tag);if(error!=0)return error;}
        made=selected.Length;return 0;
    }

    private static int CollapseBlendChain(BodySlot body,ReadOnlySpan<EdgeSlot> selected,FaceSlot fa,FaceSlot fb,
        Span<FaceSlot> products,out BodyTag output,out EdgeSlot merged)
    {
        output=0;merged=-1;var ne=Bodies[body].EdgeCountBody;var nv=Bodies[body].VertexCountBody;var nf=Bodies[body].FaceCountBody;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[ne];Span<EdgeSlot> originals=stackalloc EdgeSlot[ne];
        Span<ModelingCoedge> source=stackalloc ModelingCoedge[2*ne];Span<ModelingLoop> loops=stackalloc ModelingLoop[nf];Span<ModelingFace> faces=stackalloc ModelingFace[nf];
        Span<FaceSlot> sources=stackalloc FaceSlot[nf];Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv],OriginalVertices=stackalloc VertexSlot[nv],Edges=edges,
            Coedges=stackalloc ModelingCoedge[2*ne],Loops=stackalloc ModelingLoop[nf],Faces=stackalloc ModelingFace[nf],Sources=sources};
        Span<BodyTag> input=stackalloc BodyTag[1]{Bodies[body].Header.Tag};
        var error=ReadRegularBoundaries(input,model.Points,model.OriginalVertices,edges,originals,source,loops,faces,sources,owners,offsets,out model.NP,out model.NE,out _,out var nl,out var faceCount);if(error!=0)return error;
        if(nl!=nf||faceCount!=nf)return ParasolidConstants.PK_ERROR_not_implemented;
        var first=originals.IndexOf(selected[0]);var last=originals.IndexOf(selected[^1]);if(first<0||last<0)return ParasolidConstants.PK_ERROR_not_implemented;
        var start=LocalSenseSign(Curves[GetCurveSlotByTag(edges[first].Curve)].Sense)>0?edges[first].StartVertex:edges[last].StartVertex;
        var end=LocalSenseSign(Curves[GetCurveSlotByTag(edges[first].Curve)].Sense)>0?edges[last].EndVertex:edges[first].EndVertex;
        var a=model.Points[start];var b=model.Points[end];edges[first]=new(){Curve=edges[first].Curve,Start=edges[first].Start,End=edges[last].End,StartVertex=start,EndVertex=end};
        for(var f=0;f<nf;f++)
        {
            var loop=loops[faces[f].FirstLoop];var at=model.NC;var replaced=0;
            for(var c=0;c<loop.CoedgeCount;c++)
            {
                var coedge=source[loop.FirstCoedge+c];
                if(!selected.Contains(originals[coedge.Edge])){model.Coedges[model.NC++]=coedge;continue;}
                // Only the first segment in this oriented traversal replaces
                // the chain, including when it wraps across loop.FirstFin.
                var old=Edges[originals[coedge.Edge]];var vertex=coedge.Forward!=0?old.StartVertex:old.EndVertex;
                var vertexIndex=model.OriginalVertices[..model.NP].IndexOf(vertex);
                if(vertexIndex!=start&&vertexIndex!=end)continue;
                model.Coedges[model.NC++]=new(){Edge=first,Forward=vertexIndex==start?(byte)1:(byte)0};replaced++;
            }
            if((sources[f]==fa||sources[f]==fb)&&replaced!=1)return ParasolidConstants.PK_ERROR_bad_loop;
            model.Loops[model.NL]=new(){FirstCoedge=at,CoedgeCount=model.NC-at};
            model.Faces[model.NF++]=new(){Surface=faces[f].Surface,Sense=faces[f].Sense,FirstLoop=model.NL++,LoopCount=1};
        }
        Span<BufferOffset> indices=stackalloc BufferOffset[nf];for(var i=0;i<nf;i++)indices[i]=i;Span<byte> reverse=stackalloc byte[nf],cavity=stackalloc byte[nf];reverse.Clear();cavity.Clear();
        error=CloneRegularBoundaryBody(ref model,indices,reverse,cavity,out output,products);if(error!=0)return error;
        var slot=TagRec(output).Slot;var edgeSlot=Bodies[slot].FirstEdgeBody;
        for(var i=0;i<Bodies[slot].EdgeCountBody;i++,edgeSlot=Edges[edgeSlot].NextInBody)
        {
            if(!LocalEdgeFaces(edgeSlot,out var x,out var y)||!(x==products[sources.IndexOf(fa)]&&y==products[sources.IndexOf(fb)]||y==products[sources.IndexOf(fa)]&&x==products[sources.IndexOf(fb)]))continue;
            error=SheetOriginalVertex(Edges[edgeSlot].StartVertex,out var p);if(error!=0)return error;error=SheetOriginalVertex(Edges[edgeSlot].EndVertex,out var q);if(error!=0)return error;
            if(ProfileDistance(a,p)>1e-8||ProfileDistance(b,q)>1e-8)continue;if(merged>=0)return ParasolidConstants.PK_ERROR_not_implemented;merged=edgeSlot;
        }
        return merged>=0?0:ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

    private static int BlendChainSection(SurfTag surface,BufferOffset primarySide,in KernelVector3 point,in KernelVector3 normal,out SheetCutCurve section)
    {
        section=default;var record=Surfaces[GetSurfaceSlotByTag(surface)];var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        var root=program.Surfaces[data.ProgramRoot];var definition=program.Spines[root.Spine];var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        var reference=ProfileCross(normal,Math.Abs(normal.X)<.7?Vector(1,0,0):Vector(0,1,0));ProfileNormalize(ref reference);
        var plane=new AnalyticSurface(SurfaceClass.Plane,point,normal,reference);Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];var count=0;
        for(var i=0;i<charts.Length-1;i++)
        {
            var lo=Math.Max(record.UMin,charts[i].Parameter);var hi=Math.Min(record.UMax,charts[i+1].Parameter);if(!(hi>lo))continue;
            // A subdivision is transported through the primary support's
            // contact, rather than by projecting its vertex onto the spine.
            // The primary fin/support order also changes under reflection.
            var basis=new SurfaceIntersectionPatch(program,data.ProgramRoot,new(lo,hi,0,1));
            var patch=new SurfaceIntersectionPatch(basis,false,root.ReverseSection?primarySide:1-primarySide,lo,hi);
            var status=SurfaceIsoPlaneRoots.Find(patch,plane,lo,hi,roots,ref count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        }
        if(count!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var error=NestedBlendIsoparam(surface,true,roots[0].First,0,1,out var circle,out var first,out var last);if(error!=0)return error;
        section=new(){Curve=circle,First=first,Last=last};return 0;
    }

    private static int PartitionBlendChain(BodyTag input,ReadOnlySpan<FaceSlot> originals,FaceSlot blendFace,ReadOnlySpan<SheetCutCurve> cuts,
        Span<FaceSlot> products,Span<LocalBlendProduct> descriptions,out BodyTag output)
    {
        output=0;var body=TagRec(input).Slot;var ne=Bodies[body].EdgeCountBody;var nv=Bodies[body].VertexCountBody;var nf=Bodies[body].FaceCountBody;var count=cuts.Length+1;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv+2*cuts.Length],OriginalVertices=stackalloc VertexSlot[nv+2*cuts.Length],
            Edges=stackalloc ModelingEdge[ne+3*cuts.Length],CutEdges=stackalloc byte[ne+3*cuts.Length],
            Coedges=stackalloc ModelingCoedge[2*ne+6*cuts.Length],Loops=stackalloc ModelingLoop[nf+cuts.Length],Faces=stackalloc ModelingFace[nf+cuts.Length],
            Sources=stackalloc FaceSlot[nf+cuts.Length],Sides=stackalloc sbyte[nf+cuts.Length],Witnesses=stackalloc PK_UV_s[nf+cuts.Length],Tolerance=1e-9};
        Span<ModelingEdge> edges=stackalloc ModelingEdge[ne];Span<EdgeSlot> sourceEdges=stackalloc EdgeSlot[ne];Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[2*ne];
        Span<ModelingLoop> loops=stackalloc ModelingLoop[nf];Span<ModelingFace> faces=stackalloc ModelingFace[nf];Span<FaceSlot> sources=stackalloc FaceSlot[nf];
        Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];Span<BodyTag> bodies=stackalloc BodyTag[1]{input};
        var error=ReadRegularBoundaries(bodies,model.Points,model.OriginalVertices,edges,sourceEdges,coedges,loops,faces,sources,owners,offsets,out model.NP,out var nEdges,out var nc,out var nl,out var nFaces);if(error!=0)return error;
        Span<KernelVector3> p=stackalloc KernelVector3[1];
        foreach(var cut in cuts)for(var end=0;end<2;end++)
        {
            var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut.Curve)],end==0?cut.First:cut.Last,0,false,p,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            error=model.AddPoint(p[0],-1,out _);if(error!=0)return error;
        }
        var view=new RegularBoundaryView(edges[..nEdges],coedges[..nc],loops[..nl],faces[..nFaces]);Span<SheetSourceEdge> fragments=stackalloc SheetSourceEdge[ne];
        Span<SheetCutCurve> owned=stackalloc SheetCutCurve[ne];var ownedCount=0;
        error=SplitRegularSourceEdges(view,sourceEdges,fragments,owned,ref ownedCount,ref model);if(error!=0)return error;
        if(ownedCount!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<SheetHalfedge> halfedges=stackalloc SheetHalfedge[2*(ne+3*cuts.Length)];Span<ModelingCoedge> cycles=stackalloc ModelingCoedge[2*(ne+3*cuts.Length)];Span<SheetCycle> cycleData=stackalloc SheetCycle[nf+cuts.Length];
        foreach(var face in sources)
        {
            error=ArrangeSheetFace(face,0,fragments,face==blendFace?cuts:default,ref model,halfedges,cycles,cycleData,true);if(error!=0)return error;
        }
        if(model.NF!=originals.Length+count)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        Span<BufferOffset> order=stackalloc BufferOffset[model.NF];var at=0;
        foreach(var original in originals)
        {
            var found=-1;for(var f=0;f<model.NF;f++)if(model.Sources[f]==original){if(found>=0)return ParasolidConstants.PK_ERROR_cant_do_intersect;found=f;}
            if(found<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;order[at++]=found;
        }
        Span<BufferOffset> resultCells=stackalloc BufferOffset[count];var resultCount=0;
        for(var f=0;f<model.NF;f++)if(model.Sources[f]==blendFace)
        {if(resultCount==resultCells.Length)return ParasolidConstants.PK_ERROR_bad_loop;resultCells[resultCount++]=at;order[at++]=f;}
        if(at!=model.NF||resultCount!=descriptions.Length)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        Span<byte> reversed=stackalloc byte[model.NF],cavity=stackalloc byte[model.NF];reversed.Clear();cavity.Clear();
        error=CloneRegularBoundaryBody(ref model,order,reversed,cavity,out output,products[..model.NF]);if(error!=0)return error;
        for(var i=0;i<resultCount;i++)descriptions[i].Result=Faces[products[resultCells[i]]].Header.Tag;
        return 0;
    }
}
