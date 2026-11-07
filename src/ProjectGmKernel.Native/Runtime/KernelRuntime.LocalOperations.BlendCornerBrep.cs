using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int AssembleTrivalentBlendCorner(BodySlot body,ReadOnlySpan<FaceSlot> sourceFaces,ReadOnlySpan<EdgeSlot> selected,
        VertexSlot corner,ReadOnlySpan<FaceSlot> supports,ReadOnlySpan<FaceSlot> pairs,ReadOnlySpan<FaceSlot> caps,ReadOnlySpan<VertexSlot> far,
        ReadOnlySpan<SurfTag> bands,SurfTag sphere,ReadOnlySpan<CurveTag> curves,ReadOnlySpan<double> lower,ReadOnlySpan<double> upper,
        ReadOnlySpan<KernelVector3> contacts,ReadOnlySpan<KernelVector3> distant,ReadOnlySpan<byte> vanished,Span<LocalBlendProduct> descriptions)
    {
        var collapsed=vanished.Contains((byte)1);
        var nv=Bodies[body].VertexCountBody;var ne=Bodies[body].EdgeCountBody;var nf=sourceFaces.Length;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[ne+12];Span<ModelingCoedge> inputCoedges=stackalloc ModelingCoedge[2*ne];
        Span<ModelingLoop> inputLoops=stackalloc ModelingLoop[nf];Span<ModelingFace> inputFaces=stackalloc ModelingFace[nf];
        Span<EdgeSlot> originals=stackalloc EdgeSlot[ne];Span<FaceSlot> sources=stackalloc FaceSlot[nf];Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv+9],OriginalVertices=stackalloc VertexSlot[nv+9],Edges=edges,
            Coedges=stackalloc ModelingCoedge[2*ne+24],Loops=stackalloc ModelingLoop[nf+4],Faces=stackalloc ModelingFace[nf+4],Sources=stackalloc FaceSlot[nf+4],Witnesses=stackalloc PK_UV_s[nf+4],Tolerance=1e-9};
        Span<BodyTag> bodies=stackalloc BodyTag[1]{Bodies[body].Header.Tag};
        var error=ReadRegularBoundaries(bodies,model.Points,model.OriginalVertices,edges,originals,inputCoedges,inputLoops,inputFaces,sources,owners,offsets,
            out model.NP,out model.NE,out _,out var nl,out var count);if(error!=0)return error;
        if(nl!=nf||count!=nf)return CornerReject(BlendCornerEvent.UnsupportedGraph);
        Span<BufferOffset> nearVertices=stackalloc BufferOffset[3],farVertices=stackalloc BufferOffset[6],newEdges=stackalloc BufferOffset[12];
        for(var i=0;i<9;i++)
        {
            var point=i<3?contacts[i]:distant[i-3];var index=-1;
            for(var v=0;v<model.NP;v++)if(CornerPointsWithin(model.Points[v],point,1e-9)){index=v;break;}
            if(index<0){index=model.NP;model.Points[model.NP]=point;model.OriginalVertices[model.NP++]=-1;}
            if(i<3)nearVertices[i]=index;else farVertices[i-3]=index;
        }
        for(var e=0;e<3;e++)for(var side=0;side<4;side++)
        {
            var at=4*e+side;var a=supports.IndexOf(pairs[2*e]);var b=supports.IndexOf(pairs[2*e+1]);
            var start=side<2?nearVertices[side==0?a:b]:side==2?nearVertices[a]:farVertices[2*e];
            var end=side<2?farVertices[2*e+side]:side==2?nearVertices[b]:farVertices[2*e+1];
            var lo=lower[at];var hi=upper[at];if(hi<lo){(lo,hi)=(hi,lo);(start,end)=(end,start);}
            if(LocalSenseSign(Curves[GetCurveSlotByTag(curves[at])].Sense)<0)(start,end)=(end,start);
            newEdges[at]=model.NE;edges[model.NE++]=new(){Curve=curves[at],Start=lo,End=hi,StartVertex=start,EndVertex=end};
        }
        for(var e=0;e<ne;e++)
        {
            var old=Edges[originals[e]];if(selected.Contains(originals[e]))continue;
            for(var k=0;k<3;k++)
            {
                if(old.StartVertex!=far[k]&&old.EndVertex!=far[k])continue;
                if(!LocalEdgeFaces(originals[e],out var a,out var b))return CornerReject(BlendCornerEvent.UnsupportedGraph);
                var side=pairs[2*k]==a||pairs[2*k]==b?0:pairs[2*k+1]==a||pairs[2*k+1]==b?1:-1;
                if(side<0)return CornerReject(BlendCornerEvent.UnsupportedGraph);
                var p=new PK_VECTOR_s();WriteVector(&p,distant[2*k+side]);double t=0;error=CurveParameteriseVectorCore(old.CurveTag,p,&t);if(error!=0)return error;
                if(t<edges[e].Start-1e-10||t>edges[e].End+1e-10)return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
                t=Math.Clamp(t,edges[e].Start,edges[e].End);
                var atStart=old.StartVertex==far[k];if(atStart)edges[e].StartVertex=farVertices[2*k+side];else edges[e].EndVertex=farVertices[2*k+side];
                if(atStart==(LocalSenseSign(Curves[GetCurveSlotByTag(old.CurveTag)].Sense)>0))edges[e].Start=t;else edges[e].End=t;
            }
        }
        Span<ModelingCoedge> bandLoops=stackalloc ModelingCoedge[12],sphereLoop=stackalloc ModelingCoedge[3],boundary=stackalloc ModelingCoedge[2*ne+24];
        Span<SheetHalfedge> halfedges=stackalloc SheetHalfedge[boundary.Length];Span<ModelingCoedge> arranged=stackalloc ModelingCoedge[boundary.Length];
        Span<SheetCycle> cycles=stackalloc SheetCycle[nf+4];Span<byte> seen=stackalloc byte[12];seen.Clear();
        for(var f=0;f<nf;f++)
        {
            var source=sources[f];if(Faces[source].LoopCount!=1)return CornerReject(BlendCornerEvent.UnsupportedGraph);
            var face=inputFaces[f];var loop=inputLoops[face.FirstLoop];var n=0;
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var coedge=inputCoedges[loop.FirstCoedge+k];var old=Edges[originals[coedge.Edge]];var selectedIndex=selected.IndexOf(originals[coedge.Edge]);
                if(selectedIndex>=0)
                {
                    var e=selectedIndex;var side=pairs[2*e]==source?0:pairs[2*e+1]==source?1:-1;if(side<0)return ParasolidConstants.PK_ERROR_bad_loop;
                    var oldStart=coedge.Forward!=0?old.StartVertex:old.EndVertex;
                    var vertex=oldStart==corner?nearVertices[supports.IndexOf(source)]:farVertices[2*e+side];var edge=newEdges[4*e+side];
                    var replacement=new ModelingCoedge{Edge=edge,Forward=edges[edge].StartVertex==vertex?(byte)1:(byte)0};
                    if(Curves[GetCurveSlotByTag(edges[edge].Curve)].Class==CurveClass.Line&&replacement.Forward==0)
                    {
                        // Analytic contact carriers inherit the source edge's
                        // natural tangent; their positive use is the original
                        // supporting face boundary, independently of slots.
                        Curves[GetCurveSlotByTag(edges[edge].Curve)].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
                        (edges[edge].StartVertex,edges[edge].EndVertex)=(edges[edge].EndVertex,edges[edge].StartVertex);replacement.Forward=1;
                    }
                    boundary[n++]=replacement;bandLoops[4*e+side]=new(){Edge=edge,Forward=(byte)(replacement.Forward^1)};seen[4*e+side]++;continue;
                }
                boundary[n++]=coedge;var oldEnd=coedge.Forward!=0?old.EndVertex:old.StartVertex;
                for(var e=0;e<3;e++)if(source==caps[e]&&oldEnd==far[e])
                {
                    var next=inputCoedges[loop.FirstCoedge+(k+1)%loop.CoedgeCount];var from=coedge.Forward!=0?edges[coedge.Edge].EndVertex:edges[coedge.Edge].StartVertex;
                    var to=next.Forward!=0?edges[next.Edge].StartVertex:edges[next.Edge].EndVertex;var edge=newEdges[4*e+3];
                    var forward=edges[edge].StartVertex==from&&edges[edge].EndVertex==to;
                    if(!forward&&(edges[edge].EndVertex!=from||edges[edge].StartVertex!=to))return ParasolidConstants.PK_ERROR_bad_loop;
                    var replacement=new ModelingCoedge{Edge=edge,Forward=forward?(byte)1:(byte)0};boundary[n++]=replacement;
                    bandLoops[4*e+3]=new(){Edge=edge,Forward=(byte)(replacement.Forward^1)};seen[4*e+3]++;
                }
            }
            error=PublishCornerBoundary(face.Surface,face.Sense,source,boundary[..n],collapsed,ref model,halfedges,arranged,cycles);if(error!=0)return error;
        }
        for(var e=0;e<3;e++)
        {
            for(var side=0;side<4;side++)if(side!=2&&seen[4*e+side]!=1)return ParasolidConstants.PK_ERROR_bad_loop;
            var first=bandLoops[4*e];var vertex=nearVertices[supports.IndexOf(pairs[2*e])];
            var atEnd=(first.Forward!=0?edges[first.Edge].EndVertex:edges[first.Edge].StartVertex)==vertex;
            var nearEdge=newEdges[4*e+2];var forward=atEnd?edges[nearEdge].StartVertex==vertex:edges[nearEdge].EndVertex==vertex;
            if(Curves[GetCurveSlotByTag(edges[nearEdge].Curve)].Class==CurveClass.Circle)
            {
                // The first two selected bands establish the spherical
                // patch. Its third section closes that construction
                // in the opposite carrier sense; selection order, not world
                // coordinates, determines the representation.
                var sense=vanished[e]!=0
                    ?(forward?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c)
                    :e==2?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
                if(Curves[GetCurveSlotByTag(edges[nearEdge].Curve)].Sense!=sense)
                {
                    Curves[GetCurveSlotByTag(edges[nearEdge].Curve)].Sense=sense;
                    (edges[nearEdge].StartVertex,edges[nearEdge].EndVertex)=(edges[nearEdge].EndVertex,edges[nearEdge].StartVertex);forward=!forward;
                }
            }
            bandLoops[4*e+2]=new(){Edge=nearEdge,Forward=forward?(byte)1:(byte)0};sphereLoop[e]=new(){Edge=nearEdge,Forward=forward?(byte)0:(byte)1};
            if(vanished[e]!=0){sphereLoop[e]=bandLoops[4*e+3];continue;}
            var band=bandLoops.Slice(4*e,4);
            error=SheetCycleWitness(bands[e],edges[..model.NE],band,out _,out var positive);if(error!=0)return error;
            var orientation=positive!=0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;
            error=PublishCornerBoundary(bands[e],orientation,-1,band,collapsed,ref model,halfedges,arranged,cycles);if(error!=0)return error;
        }
        error=PublishCornerBoundary(sphere,ParasolidConstants.PK_TOPOL_sense_positive_c,-1,sphereLoop,collapsed,ref model,halfedges,arranged,cycles);if(error!=0)return error;
        Span<FaceSlot> retained=stackalloc FaceSlot[nf];var retainedCount=nf;
        if(collapsed)
        {
            error=ReduceCornerEvent(ref model,nf,sourceFaces,retained,out retainedCount);if(error!=0)return error;
            Span<ModelingFace> reducedFaces=stackalloc ModelingFace[model.NF];model.Faces[..model.NF].CopyTo(reducedFaces);
            Span<ModelingLoop> reducedLoops=stackalloc ModelingLoop[model.NL];model.Loops[..model.NL].CopyTo(reducedLoops);
            Span<FaceSlot> reducedSources=stackalloc FaceSlot[model.NF];model.Sources[..model.NF].CopyTo(reducedSources);
            model.Coedges[..model.NC].CopyTo(boundary);model.NC=model.NL=model.NF=0;
            for(var f=0;f<reducedFaces.Length;f++)
            {
                var face=reducedFaces[f];var loop=reducedLoops[face.FirstLoop];
                error=PublishCornerBoundary(face.Surface,face.Sense,reducedSources[f],boundary.Slice(loop.FirstCoedge,loop.CoedgeCount),false,
                    ref model,halfedges,arranged,cycles);if(error!=0)return error;
            }
        }
        else sourceFaces.CopyTo(retained);
        Span<SurfTag> productSurfaces=stackalloc SurfTag[descriptions.Length];var productCount=0;
        for(var e=0;e<bands.Length;e++)if(vanished[e]==0)productSurfaces[productCount++]=bands[e];
        productSurfaces[productCount++]=sphere;
        Span<BufferOffset> resultCells=stackalloc BufferOffset[productCount];
        for(var i=0;i<productCount;i++)
        {
            var cell=-1;
            for(var f=0;f<model.NF;f++)if(model.Sources[f]<0&&model.Faces[f].Surface==productSurfaces[i])
            {if(cell>=0)return ParasolidConstants.PK_ERROR_bad_loop;cell=f;}
            if(cell<0)return ParasolidConstants.PK_ERROR_bad_loop;resultCells[i]=cell;
        }
        Span<BufferOffset> indices=stackalloc BufferOffset[model.NF];for(var i=0;i<indices.Length;i++)indices[i]=i;
        Span<byte> reversed=stackalloc byte[model.NF],cavities=stackalloc byte[model.NF];reversed.Clear();cavities.Clear();
        Span<FaceSlot> built=stackalloc FaceSlot[model.NF];error=CloneRegularBoundaryBody(ref model,indices,reversed,cavities,out var output,built);if(error!=0)return error;
        LastBlendCornerStage=BlendCornerStage.Publish;error=ReplaceLocalBody(body,output,retained[..retainedCount],built);if(error!=0)return error;
        for(var i=0;i<productCount;i++)descriptions[i].Result=Faces[built[resultCells[i]]].Header.Tag;
        return 0;
    }

    private static int PublishCornerBoundary(SurfTag surface,KernelSense sense,FaceSlot source,scoped ReadOnlySpan<ModelingCoedge> boundary,
        bool collapsed,scoped ref SheetArrangement model,scoped Span<SheetHalfedge> halfedges,scoped Span<ModelingCoedge> arranged,scoped Span<SheetCycle> cycles)
    {
        if(!collapsed)
        {
            var count=model.NF;
            var error=ArrangeFiniteReplacementFace(surface,sense,source,boundary,ref model,halfedges,arranged,cycles);if(error!=0)return error;
            return model.NF==count+1?0:ParasolidConstants.PK_ERROR_bad_loop;
        }
        // Extinction first collects the oriented boundary chain so its
        // proved coincident uses can be cancelled before arrangement.
        model.Loops[model.NL]=new(){FirstCoedge=model.NC,CoedgeCount=boundary.Length};boundary.CopyTo(model.Coedges[model.NC..]);model.NC+=boundary.Length;
        model.Faces[model.NF]=new(){Surface=surface,Sense=sense,FirstLoop=model.NL++,LoopCount=1};model.Sources[model.NF++]=source;return 0;
    }

}
