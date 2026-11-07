using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum SupportChainStage:byte{None,Supports,Order,Seams,Normalize,Roll,Sections,Restore,Publish}
    [ThreadStatic] internal static SupportChainStage LastSupportChainStage;
    private struct SupportChainSeam
    {
        internal EdgeSlot Edge;
        internal FaceSlot Support;
        internal BufferOffset Side;
        internal AnalyticSurface Plane;
    }

    private static bool CommonBlendChainFaces(ReadOnlySpan<EdgeSlot> selected)
    {
        if(!LocalEdgeFaces(selected[0],out var first,out var second))return false;
        foreach(var edge in selected)
            if(!LocalEdgeFaces(edge,out var a,out var b)||!(a==first&&b==second||a==second&&b==first))return false;
        return true;
    }

    // A face seam changes finite domains, not the rolling equations, when
    // both sides reference the identical oriented support. Normalize those
    // domains once, then transport every original seam through its contact.
    private static int BuildRegularSupportChain(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;LastSupportChainStage=SupportChainStage.Supports;if(selected.Length<2||selected.Length>64||Bodies[body].RegionCount!=2||Bodies[body].ShellCount!=2)
            return ParasolidConstants.PK_ERROR_not_implemented;
        if(!LocalEdgeFaces(selected[0],out var fa,out var fb)||SameSupportChainFace(fa,fb))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> supports=stackalloc FaceSlot[2]{fa,fb};
        Span<EdgeSlot> ordered=stackalloc EdgeSlot[selected.Length];selected.CopyTo(ordered);
        Span<double> lower=stackalloc double[selected.Length],upper=stackalloc double[selected.Length];
        var first=Edges[selected[0]];var curve=first.CurveTag;var positive=LocalSenseSign(Curves[GetCurveSlotByTag(curve)].Sense)>0;
        for(var i=0;i<ordered.Length;i++)
        {
            var edge=Edges[ordered[i]];
            if(edge.CurveTag!=curve||edge.PendingBlendKind!=1||edge.PendingBlendFirst!=first.PendingBlendFirst
                ||edge.StartVertex<0||edge.EndVertex<0||!LocalEdgeFaces(ordered[i],out var a,out var b)
                ||!(SameSupportChainFace(a,fa)&&SameSupportChainFace(b,fb)||SameSupportChainFace(a,fb)&&SameSupportChainFace(b,fa))
                ||!TryProfileEdgeRange(edge,out lower[i],out upper[i]))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        LastSupportChainStage=SupportChainStage.Order;
        for(var i=1;i<ordered.Length;i++)for(var j=i;j>0&&lower[j]<lower[j-1];j--)
        {(ordered[j],ordered[j-1])=(ordered[j-1],ordered[j]);(lower[j],lower[j-1])=(lower[j-1],lower[j]);(upper[j],upper[j-1])=(upper[j-1],upper[j]);}
        Span<SupportChainSeam> seams=stackalloc SupportChainSeam[Bodies[body].EdgeCountBody];var seamCount=0;
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        LastSupportChainStage=SupportChainStage.Seams;
        for(var i=0;i<ordered.Length-1;i++)
        {
            var end=positive?Edges[ordered[i]].EndVertex:Edges[ordered[i]].StartVertex;
            var start=positive?Edges[ordered[i+1]].StartVertex:Edges[ordered[i+1]].EndVertex;
            if(end!=start||Math.Abs(upper[i]-lower[i+1])>1e-10*Math.Max(1,upper[i]-lower[0]))return ParasolidConstants.PK_ERROR_not_implemented;
            var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve)],upper[i],1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var incident=Bodies[body].FirstEdgeBody;var degree=0;var seamSides=0;
            for(var k=0;k<Bodies[body].EdgeCountBody;k++,incident=Edges[incident].NextInBody)
            {
                var record=Edges[incident];if(record.StartVertex!=end&&record.EndVertex!=end)continue;degree++;
                if(selected.Contains(incident))continue;
                if(!LocalEdgeFaces(incident,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                var side=SameSupportChainFace(a,fa)&&SameSupportChainFace(b,fa)?0:SameSupportChainFace(a,fb)&&SameSupportChainFace(b,fb)?1:-1;
                if(side<0||(seamSides&(1<<side))!=0||seamCount==seams.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                var error=SupportChainSeamPlane(incident,jet[1],out var plane);if(error!=0)return error;
                seams[seamCount++]=new(){Edge=incident,Support=supports[side],Side=side,Plane=plane};seamSides|=1<<side;
            }
            if(degree!=2+((seamSides&1)!=0?1:0)+((seamSides&2)!=0?1:0))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        if(seamCount==0||faces.Length+seamCount+1>products.Length||seamCount+1>descriptions.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> aliases=stackalloc FaceSlot[faces.Length];
        for(var f=0;f<faces.Length;f++)
        {
            aliases[f]=SameSupportChainFace(faces[f],fa)?fa:SameSupportChainFace(faces[f],fb)?fb:faces[f];
            if(aliases[f]!=faces[f])continue;
            for(var j=0;j<f;j++)if(SameSupportChainFace(faces[f],faces[j])){aliases[f]=aliases[j];break;}
        }
        var sourceEdge=Bodies[body].FirstEdgeBody;
        for(var e=0;e<Bodies[body].EdgeCountBody;e++,sourceEdge=Edges[sourceEdge].NextInBody)
        {
            if(!LocalEdgeFaces(sourceEdge,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
            var group=aliases[faces.IndexOf(a)];if(group!=aliases[faces.IndexOf(b)])continue;
            var known=false;for(var s=0;s<seamCount;s++)known|=seams[s].Edge==sourceEdge;if(known)continue;
            if(group==fa||group==fb)return ParasolidConstants.PK_ERROR_not_implemented;
            seams[seamCount++]=new(){Edge=sourceEdge,Support=group,Side=-1};
        }
        LastSupportChainStage=SupportChainStage.Normalize;
        Span<FaceSlot> workingSources=stackalloc FaceSlot[faces.Length];
        var error0=NormalizeSupportChain(body,faces,aliases,ordered,seams[..seamCount],out var working,out var merged,
            products,workingSources,out var workingCount);if(error0!=0)return error0;
        Span<FaceSlot> workingFaces=stackalloc FaceSlot[workingCount];products[..workingCount].CopyTo(workingFaces);
        Span<FaceSlot> workingSupports=stackalloc FaceSlot[2];
        for(var side=0;side<2;side++)
        {
            var found=-1;for(var f=0;f<workingCount;f++)if(workingSources[f]==supports[side])
            {if(found>=0)return ParasolidConstants.PK_ERROR_not_implemented;found=f;}
            if(found<0)return ParasolidConstants.PK_ERROR_not_implemented;workingSupports[side]=workingFaces[found];
        }
        Edges[merged].PendingBlendKind=1;Edges[merged].PendingBlendFirst=Edges[merged].PendingBlendSecond=first.PendingBlendFirst;
        LocalEdgeFaces(merged,out var primary,out _);
        Span<EdgeSlot> one=stackalloc EdgeSlot[1]{merged};Span<LocalBlendProduct> ignored=stackalloc LocalBlendProduct[1];
        Span<FaceSlot> wholeProducts=stackalloc FaceSlot[workingCount+1];
        LastSupportChainStage=SupportChainStage.Roll;
        error0=BuildRegularLocalBlend(TagRec(working).Slot,workingFaces,one,ignored,wholeProducts,out var count);if(error0!=0)return error0;
        if(count!=1)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var blendFace=wholeProducts[workingCount];var blend=Faces[blendFace].SurfTag;
        Span<OperationIntersectionPoint> passiveRoots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        foreach(var seam in seams[..seamCount])if(seam.Side<0)
        {
            var edge=Edges[seam.Edge];if(!TryProfileEdgeRange(edge,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
            var status=IntersectRegularSurfaceCurve(blend,edge.CurveTag,a,b,passiveRoots,out var n);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            if(n!=0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        Span<SheetCutCurve> sections=stackalloc SheetCutCurve[seamCount];Span<double> stations=stackalloc double[seamCount];var sectionCount=0;
        LastSupportChainStage=SupportChainStage.Sections;
        for(var i=0;i<seamCount;i++)
        {
            var seam=seams[i];if(seam.Side<0)continue;var side=workingSupports[seam.Side]==primary?0:1;
            error0=SupportChainContactSection(blend,side,seam,out var section,out var station);if(error0!=0)return error0;
            var duplicate=false;
            for(var j=0;j<sectionCount;j++)
            {
                if(station==stations[j])duplicate=true;
                else if(Math.Abs(station-stations[j])<1e-10)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            }
            if(duplicate){var tag=section.Curve;error0=EntityDelete(1,&tag);if(error0!=0)return error0;}
            else{sections[sectionCount]=section;stations[sectionCount++]=station;}
        }
        LastSupportChainStage=SupportChainStage.Restore;
        error0=RestoreSupportChain(working,faces,aliases,workingFaces,workingSources[..workingCount],workingSupports,blendFace,seams[..seamCount],sections[..sectionCount],
            descriptions,products,out var output);if(error0!=0)return error0;
        LastSupportChainStage=SupportChainStage.Publish;
        error0=ReplaceLocalBody(body,output,faces,products[..(faces.Length+sectionCount+1)]);if(error0!=0)return error0;
        error0=EntityDelete(1,&working);if(error0!=0)return error0;
        foreach(var section in sections[..sectionCount])if(IsValidTag(section.Curve)){var tag=section.Curve;error0=EntityDelete(1,&tag);if(error0!=0)return error0;}
        for(var i=0;i<sectionCount+1;i++)descriptions[i].Edge=first.Header.Tag;
        made=sectionCount+1;return 0;
    }

    private static bool SameSupportChainFace(FaceSlot a,FaceSlot b)
        =>TryCertifyIdentitySupport(Faces[a].SurfTag,Faces[b].SurfTag,out var certificate)
            &&LocalSenseSign(Faces[a].Orientation)*LocalSenseSign(Faces[b].Orientation)*certificate.NormalRelation>0;

    private static int SupportChainSeamPlane(EdgeSlot edge,in KernelVector3 chainTangent,out AnalyticSurface plane)
    {
        plane=default;var record=Curves[GetCurveSlotByTag(Edges[edge].CurveTag)];KernelVector3 origin,axis,normal=default;
        if(record.Class==CurveClass.Line)
        {
            var line=LineDataPool[record.DataIndex];origin=Vector(line.LocationX,line.LocationY,line.LocationZ);axis=Vector(line.AxisX,line.AxisY,line.AxisZ);
        }
        else if(record.Class==CurveClass.ICurve)
        {
            var data=ICurveDataPool[record.DataIndex];var support0=GetSurfaceSlotByTag(data.Surface0Tag);var support1=GetSurfaceSlotByTag(data.Surface1Tag);
            if(support0>=0&&TryPrepareSurface(Surfaces[support0],out plane)&&plane.Kind==SurfaceClass.Plane)return 0;
            if(support1>=0&&TryPrepareSurface(Surfaces[support1],out plane)&&plane.Kind==SurfaceClass.Plane)return 0;
            return ParasolidConstants.PK_ERROR_not_implemented;
        }
        else if(record.Class==CurveClass.BCurve)
        {
            var data=BCurveDataStore[record.DataIndex];var poles=data.PolesSpan();var dim=data.VertexDim;var rational=data.IsRational!=0;
            if(data.NVertices<2)return ParasolidConstants.PK_ERROR_not_implemented;
            var w=rational?poles[3]:1;if(!(w>0))return ParasolidConstants.PK_ERROR_not_implemented;
            origin=Vector(poles[0]/w,poles[1]/w,poles[2]/w);axis=default;
            for(var i=1;i<data.NVertices;i++)
            {
                var at=i*dim;w=rational?poles[at+3]:1;if(!(w>0))return ParasolidConstants.PK_ERROR_not_implemented;
                var delta=Sub(Vector(poles[at]/w,poles[at+1]/w,poles[at+2]/w),origin);
                if(OperationIntersection.Norm(axis)==0){axis=delta;continue;}
                normal=ProfileCross(axis,delta);if(OperationIntersection.Norm(normal)>1e-12*OperationIntersection.Norm(axis)*OperationIntersection.Norm(delta))break;
                normal=default;
            }
            if(OperationIntersection.Norm(normal)==0)normal=Sub(chainTangent,Scale(axis,Dot(chainTangent,axis)/Dot(axis,axis)));
            if(!ProfileNormalize(ref normal))return ParasolidConstants.PK_ERROR_not_implemented;
            // Positive rational basis functions preserve the homogeneous
            // control-hull plane enclosure over every knot span.
            for(var i=0;i<data.NVertices;i++)
            {
                var at=i*dim;IntervalNumber weight=rational?poles[at+3]:1;
                if(weight.Lower<=0)return ParasolidConstants.PK_ERROR_not_implemented;
                var residual=(new IntervalNumber(poles[at])/weight-origin.X)*normal.X
                    +(new IntervalNumber(poles[at+1])/weight-origin.Y)*normal.Y+(new IntervalNumber(poles[at+2])/weight-origin.Z)*normal.Z;
                if(residual.Magnitude>1e-10)return ParasolidConstants.PK_ERROR_not_implemented;
            }
            if(!ProfileNormalize(ref axis))return ParasolidConstants.PK_ERROR_not_implemented;
            plane=new(SurfaceClass.Plane,origin,normal,axis);return 0;
        }
        else return ParasolidConstants.PK_ERROR_not_implemented;
        normal=Sub(chainTangent,Scale(axis,Dot(chainTangent,axis)/Dot(axis,axis)));
        if(!ProfileNormalize(ref normal)||!ProfileNormalize(ref axis))return ParasolidConstants.PK_ERROR_not_implemented;
        if(!TryProfileEdgeRange(Edges[edge],out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
        var lineResidual=new IntervalNumber(first,last)*(new IntervalNumber(axis.X)*normal.X+new IntervalNumber(axis.Y)*normal.Y+new IntervalNumber(axis.Z)*normal.Z);
        if(lineResidual.Magnitude>1e-10)return ParasolidConstants.PK_ERROR_not_implemented;
        plane=new(SurfaceClass.Plane,origin,normal,axis);return 0;
    }

    private static int NormalizeSupportChain(BodySlot body,ReadOnlySpan<FaceSlot> originals,ReadOnlySpan<FaceSlot> aliases,
        ReadOnlySpan<EdgeSlot> selected,ReadOnlySpan<SupportChainSeam> seams,out BodyTag output,out EdgeSlot merged,
        Span<FaceSlot> products,Span<FaceSlot> workingSources,out BufferCount count)
    {
        output=0;merged=-1;count=0;var ne=Bodies[body].EdgeCountBody;var nv=Bodies[body].VertexCountBody;var nf=originals.Length;
        var loopCapacity=0;foreach(var face in originals)loopCapacity+=Faces[face].LoopCount;
        if(loopCapacity>SheetLoopLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv],OriginalVertices=stackalloc VertexSlot[nv],Edges=stackalloc ModelingEdge[ne],
            Coedges=stackalloc ModelingCoedge[2*ne],Loops=stackalloc ModelingLoop[loopCapacity],Faces=stackalloc ModelingFace[nf],Sources=stackalloc FaceSlot[nf],Witnesses=stackalloc PK_UV_s[nf]};
        Span<EdgeSlot> edges=stackalloc EdgeSlot[ne];Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];Span<BodyTag> input=stackalloc BodyTag[1]{Bodies[body].Header.Tag};
        var error=ReadRegularBoundaries(input,model.Points,model.OriginalVertices,model.Edges,edges,model.Coedges,model.Loops,model.Faces,model.Sources,owners,offsets,
            out model.NP,out model.NE,out model.NC,out model.NL,out model.NF);if(error!=0)return error;
        for(var e=0;e<ne;e++)
        {
            if(!LocalEdgeFaces(edges[e],out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
            if(aliases[originals.IndexOf(a)]!=aliases[originals.IndexOf(b)])continue;
            var found=false;foreach(var seam in seams)found|=seam.Edge==edges[e];if(!found)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        for(var f=0;f<nf;f++)model.Sources[f]=aliases[originals.IndexOf(model.Sources[f])];
        Span<byte> keep=stackalloc byte[nf],reverse=stackalloc byte[nf],cavity=stackalloc byte[nf];keep.Fill(1);reverse.Clear();cavity.Clear();
        error=MergeRegularBooleanCells(ref model,keep,reverse);if(error!=0)return error;
        Span<BufferOffset> order=stackalloc BufferOffset[model.NF];for(var f=0;f<model.NF;f++)order[f]=f;
        Span<EdgeSlot> edgeProducts=stackalloc EdgeSlot[model.NE];
        error=CloneRegularBoundaryBody(ref model,order,reverse,cavity,out output,products[..model.NF],edgeProducts);if(error!=0)return error;count=model.NF;
        model.Sources[..model.NF].CopyTo(workingSources);
        var first=Edges[selected[0]];var last=Edges[selected[^1]];
        var positive=LocalSenseSign(Curves[GetCurveSlotByTag(first.CurveTag)].Sense)>0;
        var start=positive?first.StartVertex:last.StartVertex;var end=positive?last.EndVertex:first.EndVertex;
        if(!TryProfileEdgeRange(first,out var lower,out _)||!TryProfileEdgeRange(last,out _,out var upper))
            return ParasolidConstants.PK_ERROR_cant_do_intersect;
        for(var e=0;e<edgeProducts.Length;e++)
        {
            var edge=model.Edges[e];
            if(edgeProducts[e]<0||edge.Curve!=first.CurveTag||edge.Start!=lower||edge.End!=upper||edge.StartVertex<0||edge.EndVertex<0
                ||model.OriginalVertices[edge.StartVertex]!=start||model.OriginalVertices[edge.EndVertex]!=end)continue;
            if(merged>=0)return ParasolidConstants.PK_ERROR_cant_do_intersect;merged=edgeProducts[e];
        }
        return merged>=0?0:ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

    private static int SupportChainContactSection(SurfTag surface,BufferOffset side,in SupportChainSeam seam,out SheetCutCurve section,out double station)
    {
        section=default;station=0;var record=Surfaces[GetSurfaceSlotByTag(surface)];var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        var root=program.Surfaces[data.ProgramRoot];var definition=program.Spines[root.Spine];var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];var count=0;
        for(var i=0;i<charts.Length-1;i++)
        {
            var lo=Math.Max(record.UMin,charts[i].Parameter);var hi=Math.Min(record.UMax,charts[i+1].Parameter);if(!(hi>lo))continue;
            var basis=new SurfaceIntersectionPatch(program,data.ProgramRoot,new(lo,hi,0,1));
            var patch=new SurfaceIntersectionPatch(basis,false,root.ReverseSection?side:1-side,lo,hi);
            var status=SurfaceIsoPlaneRoots.Find(patch,seam.Plane,lo,hi,roots,ref count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        }
        if(count!=1||!TryProfileEdgeRange(Edges[seam.Edge],out var first,out var last))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var point=new PK_VECTOR_s();WriteVector(&point,roots[0].Point);double t=0;
        var error=CurveParameteriseVectorCore(Edges[seam.Edge].CurveTag,point,&t);if(error!=0)return error;
        if(!OperationIntersection.InInterval(t,first,last))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        station=roots[0].First;
        error=NestedBlendIsoparam(surface,true,station,0,1,out var circle,out first,out last);if(error!=0)return error;
        section=new(){Curve=circle,First=first,Last=last};return 0;
    }

    private static int RestoreSupportChain(BodyTag input,ReadOnlySpan<FaceSlot> originals,ReadOnlySpan<FaceSlot> aliases,
        ReadOnlySpan<FaceSlot> workingFaces,ReadOnlySpan<FaceSlot> workingSources,ReadOnlySpan<FaceSlot> supports,FaceSlot blendFace,ReadOnlySpan<SupportChainSeam> seams,ReadOnlySpan<SheetCutCurve> sections,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BodyTag output)
    {
        output=0;var body=TagRec(input).Slot;var ne=Bodies[body].EdgeCountBody;var nv=Bodies[body].VertexCountBody;var nf=Bodies[body].FaceCountBody;
        var capacity=ne+4*(seams.Length+sections.Length);var faceCapacity=originals.Length+sections.Length+1;
        var sourceLoopCapacity=Faces[blendFace].LoopCount;foreach(var face in workingFaces)sourceLoopCapacity+=Faces[face].LoopCount;
        var loopCapacity=sourceLoopCapacity+seams.Length+sections.Length;
        if(loopCapacity>SheetLoopLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv+4*(seams.Length+sections.Length)],OriginalVertices=stackalloc VertexSlot[nv+4*(seams.Length+sections.Length)],
            Edges=stackalloc ModelingEdge[capacity],CutEdges=stackalloc byte[capacity],Coedges=stackalloc ModelingCoedge[2*capacity],Loops=stackalloc ModelingLoop[loopCapacity],
            Faces=stackalloc ModelingFace[faceCapacity],Sources=stackalloc FaceSlot[faceCapacity],Sides=stackalloc sbyte[faceCapacity],Witnesses=stackalloc PK_UV_s[faceCapacity],Tolerance=1e-9};
        Span<ModelingEdge> edges=stackalloc ModelingEdge[ne];Span<EdgeSlot> sourceEdges=stackalloc EdgeSlot[ne];Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[2*ne];
        Span<ModelingLoop> loops=stackalloc ModelingLoop[sourceLoopCapacity];Span<ModelingFace> faces=stackalloc ModelingFace[nf];Span<FaceSlot> sources=stackalloc FaceSlot[nf];
        Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];Span<BodyTag> bodies=stackalloc BodyTag[1]{input};
        var error=ReadRegularBoundaries(bodies,model.Points,model.OriginalVertices,edges,sourceEdges,coedges,loops,faces,sources,owners,offsets,out model.NP,out var nEdges,out var nc,out var nl,out var nFaces);if(error!=0)return error;
        Span<KernelVector3> p=stackalloc KernelVector3[1];
        foreach(var cut in sections)for(var end=0;end<2;end++)
        {
            var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(cut.Curve)],end==0?cut.First:cut.Last,0,false,p,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            error=model.AddPoint(p[0],-1,out _);if(error!=0)return error;
        }
        foreach(var seam in seams)for(var end=0;end<2;end++)
        {
            var edge=Edges[seam.Edge];error=SheetOriginalVertex(end==0?edge.StartVertex:edge.EndVertex,out var point);if(error!=0)return error;
            error=model.AddPoint(point,-1,out _);if(error!=0)return error;
        }
        var view=new RegularBoundaryView(edges[..nEdges],coedges[..nc],loops[..nl],faces[..nFaces]);Span<SheetSourceEdge> fragments=stackalloc SheetSourceEdge[ne];
        Span<SheetCutCurve> owned=stackalloc SheetCutCurve[ne];var ownedCount=0;
        error=SplitRegularSourceEdges(view,sourceEdges,fragments,owned,ref ownedCount,ref model);if(error!=0)return error;if(ownedCount!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<SheetHalfedge> halfedges=stackalloc SheetHalfedge[2*capacity];Span<ModelingCoedge> cycles=stackalloc ModelingCoedge[2*capacity];Span<SheetCycle> cycleData=stackalloc SheetCycle[loopCapacity];
        Span<SheetCutCurve> cuts=stackalloc SheetCutCurve[seams.Length];
        foreach(var face in sources)
        {
            var n=0;var group=face==blendFace?-1:workingSources[workingFaces.IndexOf(face)];for(var i=0;i<seams.Length;i++)if(group==seams[i].Support)
            {var edge=Edges[seams[i].Edge];if(!TryProfileEdgeRange(edge,out var lo,out var hi))return ParasolidConstants.PK_ERROR_not_implemented;cuts[n++]=new(){Curve=edge.CurveTag,First=lo,Last=hi};}
            error=ArrangeSheetFace(face,0,fragments,face==blendFace?sections:cuts[..n],ref model,halfedges,cycles,cycleData,true);if(error!=0)return error;
        }
        if(model.NF!=faceCapacity)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        Span<BufferOffset> order=stackalloc BufferOffset[faceCapacity];order.Fill(-1);var at=originals.Length;
        for(var f=0;f<model.NF;f++)
        {
            if(model.Sources[f]==blendFace){order[at++]=f;continue;}
            var source=workingSources[workingFaces.IndexOf(model.Sources[f])];var found=-1;
            for(var i=0;i<originals.Length;i++)
            {
                if(source!=aliases[i])continue;
                error=SheetSourceContains(originals[i],model.Witnesses[f],out var inside);if(error!=0)return error;if(!inside)continue;
                if(found>=0)return ParasolidConstants.PK_ERROR_bad_loop;found=i;
            }
            if(found<0||order[found]>=0)return ParasolidConstants.PK_ERROR_bad_loop;order[found]=f;
        }
        foreach(var index in order)if(index<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        for(var b=originals.Length;b<order.Length;b++)
        {
            var blend=model.Faces[order[b]];FaceSlot a=-1,c=-1;
            for(var l=0;l<blend.LoopCount;l++)
            {
                var loop=model.Loops[blend.FirstLoop+l];
                for(var k=0;k<loop.CoedgeCount;k++)
                {
                    var edge=model.Coedges[loop.FirstCoedge+k].Edge;
                    for(var f=0;f<originals.Length;f++)
                    {
                        var cell=model.Faces[order[f]];var adjacent=false;
                        for(var j=0;j<cell.LoopCount;j++){var boundary=model.Loops[cell.FirstLoop+j];for(var q=0;q<boundary.CoedgeCount;q++)adjacent|=model.Coedges[boundary.FirstCoedge+q].Edge==edge;}
                        if(!adjacent)continue;
                        var side=aliases[f]==workingSources[workingFaces.IndexOf(supports[0])]?0:aliases[f]==workingSources[workingFaces.IndexOf(supports[1])]?1:-1;
                        if(side==0){if(a>=0&&a!=originals[f])return ParasolidConstants.PK_ERROR_bad_loop;a=originals[f];}
                        if(side==1){if(c>=0&&c!=originals[f])return ParasolidConstants.PK_ERROR_bad_loop;c=originals[f];}
                    }
                }
            }
            if(a<0||c<0)return ParasolidConstants.PK_ERROR_bad_loop;descriptions[b-originals.Length]=new(){First=Faces[a].Header.Tag,Second=Faces[c].Header.Tag};
        }
        Span<byte> reversed=stackalloc byte[faceCapacity],cavity=stackalloc byte[faceCapacity];reversed.Clear();cavity.Clear();
        return CloneRegularBoundaryBody(ref model,order,reversed,cavity,out output,products[..faceCapacity]);
    }
}
