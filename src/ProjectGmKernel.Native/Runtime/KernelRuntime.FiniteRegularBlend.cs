using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // The regular provider supplies its two contact curves and two finite
    // termination curves. Incidence comes from their endpoint events and the
    // retained source boundary; no provider supplies an ordered result loop.
    private static int AssembleFiniteRegularBlend(BodySlot body,ReadOnlySpan<FaceSlot> sourceFaces,EdgeSlot selected,
        ReadOnlySpan<FaceSlot> supports,ReadOnlySpan<FaceSlot> caps,ReadOnlySpan<VertexSlot> ends,SurfTag blend,
        ReadOnlySpan<CurveTag> curves,ReadOnlySpan<double> lower,ReadOnlySpan<double> upper,ReadOnlySpan<KernelVector3> contacts,
        Span<FiniteSectionChart> sectionCharts,Span<FaceSlot> products,out FaceTag product)
    {
        product=0;
        var nv=Bodies[body].VertexCountBody;var ne=Bodies[body].EdgeCountBody;var nf=sourceFaces.Length;var loopCapacity=0;
        foreach(var face in sourceFaces)loopCapacity+=Faces[face].LoopCount;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[ne+curves.Length];
        Span<ModelingCoedge> inputCoedges=stackalloc ModelingCoedge[2*ne];
        Span<ModelingLoop> inputLoops=stackalloc ModelingLoop[loopCapacity];Span<ModelingFace> inputFaces=stackalloc ModelingFace[nf];
        Span<EdgeSlot> originals=stackalloc EdgeSlot[ne];Span<FaceSlot> sources=stackalloc FaceSlot[nf];
        Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv+contacts.Length],OriginalVertices=stackalloc VertexSlot[nv+contacts.Length],Edges=edges,
            Coedges=stackalloc ModelingCoedge[2*ne+2*curves.Length],Loops=stackalloc ModelingLoop[loopCapacity+1],Faces=stackalloc ModelingFace[nf+1],
            Sources=stackalloc FaceSlot[nf+1],Witnesses=stackalloc PK_UV_s[nf+1],SectionCharts=sectionCharts,SectionChartCount=sectionCharts.Length,Tolerance=1e-9};
        Span<BodyTag> bodies=stackalloc BodyTag[1]{Bodies[body].Header.Tag};
        var error=ReadRegularBoundaries(bodies,model.Points,model.OriginalVertices,edges,originals,inputCoedges,inputLoops,inputFaces,sources,owners,offsets,
            out model.NP,out model.NE,out _,out _,out var faceCount);if(error!=0)return error;
        if(faceCount!=nf)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<BoundaryEventId> events=stackalloc BoundaryEventId[contacts.Length];
        for(var i=0;i<contacts.Length;i++)
        {events[i]=model.NP;model.Points[model.NP]=contacts[i];model.OriginalVertices[model.NP++]=-1;}
        Span<BufferOffset> candidates=stackalloc BufferOffset[curves.Length];Span<FaceSlot> participants=stackalloc FaceSlot[curves.Length];
        for(var i=0;i<curves.Length;i++)
        {
            // These associations are the regular provider's contact and
            // termination equations, independent of source loop traversal.
            var start=i<2?events[2*i]:events[i-2];var end=i<2?events[2*i+1]:events[i];
            participants[i]=i<2?supports[i]:caps[i-2];
            var a=lower[i];var b=upper[i];if(b<a){(a,b)=(b,a);(start,end)=(end,start);}
            if(LocalSenseSign(Curves[GetCurveSlotByTag(curves[i])].Sense)<0)(start,end)=(end,start);
            if(!(b>a))return ParasolidConstants.PK_ERROR_bad_parameter;
            candidates[i]=model.NE;edges[model.NE++]=new(){Curve=curves[i],Start=a,End=b,StartVertex=start,EndVertex=end};
        }
        LastRegularBlendStage=RegularBlendStage.Trim;
        for(var e=0;e<ne;e++)
        {
            var old=Edges[originals[e]];if(originals[e]==selected)continue;
            for(var cap=0;cap<ends.Length;cap++)if(old.StartVertex==ends[cap]||old.EndVertex==ends[cap])
            {
                if(!LocalEdgeFaces(originals[e],out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                var side=supports.IndexOf(a);if(side<0)side=supports.IndexOf(b);if(side<0)return ParasolidConstants.PK_ERROR_not_implemented;
                var contact=2*side+cap;
                error=RegularBlendTrimParameter(old,old.StartVertex==ends[cap],contacts[contact],out var t,out var lowerEnd,out _);if(error!=0)return error;
                if(old.StartVertex==ends[cap])edges[e].StartVertex=events[contact];else edges[e].EndVertex=events[contact];
                if(lowerEnd)edges[e].Start=t;else edges[e].End=t;
            }
        }
        Span<ModelingCoedge> boundary=stackalloc ModelingCoedge[2*ne+curves.Length],newBoundary=stackalloc ModelingCoedge[curves.Length];var newCount=0;
        Span<FinSlot> sourceFins=stackalloc FinSlot[boundary.Length];
        Span<SheetHalfedge> halfedges=stackalloc SheetHalfedge[boundary.Length];
        Span<ModelingCoedge> cycleCoedges=stackalloc ModelingCoedge[boundary.Length];Span<SheetCycle> cycles=stackalloc SheetCycle[loopCapacity+1];
        LastRegularBlendStage=RegularBlendStage.Loops;
        for(var f=0;f<nf;f++)
        {
            var face=inputFaces[f];var count=0;var sourceLoop=Faces[sources[f]].FirstLoop;
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=inputLoops[face.FirstLoop+l];
                while(sourceLoop>=0&&RegularVertexLoop(sourceLoop,out _))sourceLoop=Loops[sourceLoop].NextInFace;
                if(sourceLoop<0||Loops[sourceLoop].FinCount!=loop.CoedgeCount)return ParasolidConstants.PK_ERROR_bad_loop;
                var fin=Loops[sourceLoop].FirstFin;sourceLoop=Loops[sourceLoop].NextInFace;
                foreach(var coedge in inputCoedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    if(fin<0||Fins[fin].Edge!=originals[coedge.Edge])return ParasolidConstants.PK_ERROR_bad_loop;
                    if(originals[coedge.Edge]!=selected){sourceFins[count]=fin;boundary[count++]=coedge;}
                    fin=Fins[fin].NextInLoop;
                }
            }
            for(var c=0;c<candidates.Length;c++)if(participants[c]==sources[f])
            {
                var e=candidates[c];var record=edges[e];
                var balance=0;
                foreach(var use in boundary[..count])
                {
                    var existing=edges[use.Edge];
                    if((use.Forward!=0?existing.EndVertex:existing.StartVertex)==record.StartVertex)balance++;
                    if((use.Forward!=0?existing.StartVertex:existing.EndVertex)==record.StartVertex)balance--;
                }
                if(Math.Abs(balance)!=1)return ParasolidConstants.PK_ERROR_bad_loop;
                var forward=balance>0?(byte)1:(byte)0;sourceFins[count]=-1;boundary[count++]=new(){Edge=e,Forward=forward};
                if(newCount==newBoundary.Length)return ParasolidConstants.PK_ERROR_bad_loop;
                newBoundary[newCount++]=new(){Edge=e,Forward=(byte)(forward^1)};
            }
            var before=model.NF;
            error=ArrangeFiniteReplacementFace(face.Surface,face.Sense,sources[f],boundary[..count],ref model,halfedges,cycleCoedges,cycles,sourceFins[..count]);if(error!=0)return error;
            // This provider preserves each finite support as one face. A split
            // requires an explicit product/provenance plan, never array guesses.
            if(model.NF!=before+1)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        if(newCount!=curves.Length)return ParasolidConstants.PK_ERROR_bad_loop;
        var nh=0;
        foreach(var use in newBoundary[..newCount])
        {error=AddSheetHalfedge(blend,ref model,use.Edge,use.Forward,halfedges,ref nh);if(error!=0)return error;}
        error=ExtractSheetCycles(blend,ref model,halfedges[..nh],cycleCoedges,cycles,out var cycleCount,out _);if(error!=0)return error;
        if(cycleCount!=1)return ParasolidConstants.PK_ERROR_not_implemented;
        var sense=cycles[0].Positive!=0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
        var blendCell=model.NF;
        error=ArrangeFiniteReplacementFace(blend,sense,-1,newBoundary[..newCount],ref model,halfedges,cycleCoedges,cycles);if(error!=0)return error;
        if(model.NF!=blendCell+1)return ParasolidConstants.PK_ERROR_bad_loop;
        Span<BufferOffset> indices=stackalloc BufferOffset[model.NF];for(var i=0;i<indices.Length;i++)indices[i]=i;
        Span<byte> reversed=stackalloc byte[model.NF],cavities=stackalloc byte[model.NF];reversed.Clear();cavities.Clear();
        LastRegularBlendStage=RegularBlendStage.Build;
        error=CloneRegularBoundaryBody(ref model,indices,reversed,cavities,out var output,products[..model.NF]);if(error!=0)return error;
        LastRegularBlendStage=RegularBlendStage.Publish;
        error=ReplaceLocalBody(body,output,sourceFaces,products);if(error!=0)return error;
        product=Faces[products[blendCell]].Header.Tag;return 0;
    }
}
