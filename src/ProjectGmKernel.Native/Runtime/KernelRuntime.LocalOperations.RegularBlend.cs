using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum RegularBlendStage:byte{None,Supports,Roll,Contacts,Ends,EndCircle,EndIntersection,EndParameters,Clearance,Reach,Trim,Loops,Build,Publish,Release}
    [ThreadStatic] internal static RegularBlendStage LastRegularBlendStage;
    // A single regular edge ending at two trivalent vertices. Contact curves
    // replace the selected edge; each end curve replaces its old cap corner.
    private static int BuildRegularLocalBlend(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;LastRegularBlendStage=RegularBlendStage.Supports;
        if(selected.Length>1)return BuildRegularBlendChain(body,faces,selected,descriptions,products,out made);
        if(selected.Length!=1||Bodies[body].RegionCount!=2||Bodies[body].ShellCount!=2)return ParasolidConstants.PK_ERROR_not_implemented;
        var edge=selected[0];var original=Edges[edge];
        if(original.PendingBlendKind!=1||original.StartVertex<0||original.EndVertex<0
            ||!LocalEdgeFaces(edge,out var fa,out var fb))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> supports=stackalloc FaceSlot[2]{fa,fb},caps=stackalloc FaceSlot[2];
        Span<SurfTag> capSurfaces=stackalloc SurfTag[2];
        Span<byte> ownedCaps=stackalloc byte[2];ownedCaps.Clear();
        Span<VertexSlot> ends=stackalloc VertexSlot[2]{original.StartVertex,original.EndVertex};
        for(var side=0;side<2;side++)
        {
            var support=Surfaces[GetSurfaceSlotByTag(Faces[supports[side]].SurfTag)];
            if(support.Class is not (SurfaceClass.Plane or SurfaceClass.BSurface or SurfaceClass.BlendSurface))return ParasolidConstants.PK_ERROR_not_implemented;
            var error=RegularBlendEndFace(body,ends[side],fa,fb,out caps[side]);if(error!=0)return error;
            capSurfaces[side]=Faces[caps[side]].SurfTag;
            if(Surfaces[GetSurfaceSlotByTag(capSurfaces[side])].Class!=SurfaceClass.Plane)
            {
                RegularPlanarSupport(GetSurfaceSlotByTag(capSurfaces[side]),out var plane,out _);
                capSurfaces[side]=CreatePlaneSurfaceTag(plane.Origin.X,plane.Origin.Y,plane.Origin.Z,plane.Axis.X,plane.Axis.Y,plane.Axis.Z,plane.X.X,plane.X.Y,plane.X.Z);
                if(capSurfaces[side]<=0)return ParasolidConstants.PK_ERROR_memory_full;
                ownedCaps[side]=1;
            }
        }
        if(caps[0]==caps[1])return ParasolidConstants.PK_ERROR_not_implemented;
        var sa=Faces[fa].SurfTag;var sb=Faces[fb].SurfTag;
        if(Surfaces[GetSurfaceSlotByTag(sa)].Class is not (SurfaceClass.BSurface or SurfaceClass.BlendSurface)
            &&Surfaces[GetSurfaceSlotByTag(sb)].Class is not (SurfaceClass.BSurface or SurfaceClass.BlendSurface))
            return ParasolidConstants.PK_ERROR_not_implemented;
        if(!TryProfileEdgeRange(original,out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(original.CurveTag)],(first+last)*.5,1,false,jet,out _);
        if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        var normalError=RegularBlendNormal(fa,jet[0],out var na);if(normalError!=0)return normalError;
        normalError=RegularBlendNormal(fb,jet[0],out var nb);if(normalError!=0)return normalError;
        var fin=original.FirstFinEdge;if(Loops[Fins[fin].Loop].Face!=fa)fin=Fins[fin].Other;
        var orientation=(Fins[fin].Sense=='+'?1:-1)*LocalSenseSign(Curves[GetCurveSlotByTag(original.CurveTag)].Sense);
        var turn=Dot(ProfileCross(na,nb),jet[1])*orientation;
        if(Math.Abs(turn)<1e-10*OperationIntersection.Norm(jet[1]))return ParasolidConstants.PK_ERROR_not_implemented;
        var direction=turn>0?-1:1;var radius=original.PendingBlendFirst;
        var error0=SheetOriginalVertex(ends[0],out var start);if(error0!=0)return error0;
        error0=SheetOriginalVertex(ends[1],out var end);if(error0!=0)return error0;
        var options=new PK_SURF_create_blend_o_s{o_t_version=1,have_help=1};WriteVector(&options.help,jet[0]);SurfTag blend=0;
        LastRegularBlendStage=RegularBlendStage.Roll;
        Span<AnalyticSurface> termination=stackalloc AnalyticSurface[2];
        for(var cap=0;cap<2;cap++)
        {
            if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(capSurfaces[cap])],out var plane))return ParasolidConstants.PK_ERROR_not_implemented;
            // A notch end face can face into the selected interval. The
            // tracing barrier is outward from this finite edge branch,
            // independently of the cap's material orientation.
            var height=Dot(Sub(jet[0],plane.Origin),plane.Axis);
            if(Math.Abs(height)<1e-12)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var outward=Scale(plane.Axis,height<0?1:-1);
            termination[cap]=new(SurfaceClass.Plane,plane.Origin,outward,plane.X);
        }
        error0=CreateFiniteRollingBlend(sa,direction*LocalSenseSign(Faces[fa].Orientation)*radius,
            sb,direction*LocalSenseSign(Faces[fb].Orientation)*radius,start,end,&options,&blend,termination);if(error0!=0)return error0;

        Span<CurveTag> curves=stackalloc CurveTag[4];Span<double> lo=stackalloc double[4],hi=stackalloc double[4];
        curves.Clear();
        Span<KernelVector3> contacts=stackalloc KernelVector3[4];Span<double> stations=stackalloc double[4];
        LastRegularBlendStage=RegularBlendStage.Contacts;
        for(var side=0;side<2;side++)
        {
            error0=CreateBlendContactCurve(blend,side,out curves[side]);if(error0!=0)return error0;
            var contactCurve=Curves[GetCurveSlotByTag(curves[side])];var lower=contactCurve.TMin;var upper=contactCurve.TMax;
            for(var cap=0;cap<2;cap++)
            {
                error0=RegularBlendContact(curves[side],lower,upper,capSurfaces[cap],
                    out var parameter,out contacts[2*side+cap]);if(error0!=0)return error0;
                if(cap==0)lo[side]=parameter;else hi[side]=parameter;
                var found=EvaluateBlendContact(ICurveDataPool[contactCurve.DataIndex],parameter,0,jet,out stations[2*side+cap]);if(found!=AlgorithmStatus.Success)return EvaluationError(found);
            }
        }
        LastRegularBlendStage=RegularBlendStage.Ends;
        Span<SheetCutCurve> cuts=stackalloc SheetCutCurve[GeneralIntersectionBranchLimit];
        for(var cap=0;cap<2;cap++)
        {
            var u0=stations[cap];var u1=stations[2+cap];
            // A planar constant-u section is an exact circular end curve,
            // including at an artificial spine boundary where SSI cannot
            // discover an interior branch. Certify its entire circle plane.
            if(Math.Abs(u0-u1)<1e-10)
            {
                LastRegularBlendStage=RegularBlendStage.EndCircle;
                error0=NestedBlendIsoparam(blend,true,(u0+u1)*.5,0,1,out var circle,out _,out _);if(error0!=0)return error0;
                TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(capSurfaces[cap])],out var plane);
                var data=CircleDataPool[Curves[GetCurveSlotByTag(circle)].DataIndex];
                var axis=Vector(data.AxisX,data.AxisY,data.AxisZ);var center=Vector(data.CenterX,data.CenterY,data.CenterZ);
                if(OperationIntersection.Norm(ProfileCross(axis,plane.Axis))<1e-12&&Math.Abs(Dot(Sub(center,plane.Origin),plane.Axis))<1e-10)
                    curves[2+cap]=circle;
                else {error0=EntityDelete(1,&circle);if(error0!=0)return error0;}
            }
            if(curves[2+cap]==0)
            {
                LastRegularBlendStage=RegularBlendStage.EndIntersection;
                error0=PrepareSheetCutCurves(blend,capSurfaces[cap],cuts,out var count);if(error0!=0)return error0;
                if(count!=1||cuts[0].Closed!=0)return ParasolidConstants.PK_ERROR_not_implemented;
                curves[2+cap]=cuts[0].Curve;
            }
            LastRegularBlendStage=RegularBlendStage.EndParameters;
            var p=new PK_VECTOR_s();WriteVector(&p,contacts[cap]);double a=0,b=0;
            error0=CurveParameteriseVectorCore(curves[2+cap],p,&a);if(error0!=0)return error0;
            WriteVector(&p,contacts[2+cap]);error0=CurveParameteriseVectorCore(curves[2+cap],p,&b);if(error0!=0)return error0;
            if(Curves[GetCurveSlotByTag(curves[2+cap])].Class==CurveClass.Circle)
            {
                if(a>Math.Tau-1e-9)a=0;if(b>Math.Tau-1e-9)b=0;
                if(Math.Abs(a-b)>Math.PI+1e-9)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                Curves[GetCurveSlotByTag(curves[2+cap])].Sense=direction>0?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
            }
            lo[2+cap]=a;hi[2+cap]=b;
        }
        // An oblique cap can attain a spine-coordinate extremum between its
        // two contacts. The certified two-barrier construction window contains
        // the entire end curve; contact endpoint extrema alone do not.
        var window=Surfaces[GetSurfaceSlotByTag(blend)];var firstStation=window.UMin;var lastStation=window.UMax;
        error0=ValidateRegularEndSeparation(curves[2..],lo[2..],hi[2..],termination);if(error0!=0)return error0;
        LastRegularBlendStage=RegularBlendStage.Clearance;
        error0=ValidateRegularBlendDomain(body,supports,caps,blend,firstStation,lastStation,direction<0);if(error0!=0)return error0;
        error0=ValidateRegularContactPlacement(edge,supports,curves[..2],lo[..2],hi[..2]);if(error0!=0)return error0;
        LastRegularBlendStage=RegularBlendStage.Reach;
        error0=ValidateRegularBlendEndExtensions(body,edge,supports,ends,contacts,blend,firstStation,lastStation);if(error0!=0)return error0;
        error0=AssembleRegularLocalBlend(body,faces,edge,supports,caps,ends,blend,curves,lo,hi,contacts,products);
        if(error0!=0)return error0;
        LastRegularBlendStage=RegularBlendStage.Release;
        descriptions[0]=new(){Edge=original.Header.Tag,First=Faces[fa].Header.Tag,Second=Faces[fb].Header.Tag};made=1;
        // The assembled body owns cloned geometry, including the complete
        // procedural support closure. Release the construction roots.
        // Replacing the source also releases construction geometry adopted
        // by that body; shared dependency roots may already be gone.
        foreach(var curve in curves)if(IsValidTag(curve)){var tag=curve;error0=EntityDelete(1,&tag);if(error0!=0)return error0;}
        for(var cap=0;cap<2;cap++)if(ownedCaps[cap]!=0&&IsValidTag(capSurfaces[cap])&&GeometricOwnerCount(PoolKind.Surface,GetSurfaceSlotByTag(capSurfaces[cap]))==0)
        {var tag=capSurfaces[cap];error0=EntityDelete(1,&tag);if(error0!=0)return error0;}
        // Curve deletion is deferred until command commit. Its dependent
        // surface is reclaimed then, when the final reference is released.
        return IsValidTag(blend)&&GeometricOwnerCount(PoolKind.Surface,GetSurfaceSlotByTag(blend))==0?EntityDelete(1,&blend):0;
    }

    private static int RegularBlendEndFace(BodySlot body,VertexSlot vertex,FaceSlot first,FaceSlot second,out FaceSlot cap)
    {
        cap=-1;var incident=0;var edge=Bodies[body].FirstEdgeBody;
        for(var i=0;i<Bodies[body].EdgeCountBody;i++,edge=Edges[edge].NextInBody)
        {
            if(Edges[edge].StartVertex!=vertex&&Edges[edge].EndVertex!=vertex)continue;incident++;
            if(!LocalEdgeFaces(edge,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
            if(a!=first&&a!=second){if(cap>=0&&cap!=a)return ParasolidConstants.PK_ERROR_not_implemented;cap=a;}
            if(b!=first&&b!=second){if(cap>=0&&cap!=b)return ParasolidConstants.PK_ERROR_not_implemented;cap=b;}
        }
        if(incident!=3||cap<0||RegularBlendAreaLoopCount(cap)!=1
            ||!RegularPlanarSupport(GetSurfaceSlotByTag(Faces[cap].SurfTag),out _,out _))return ParasolidConstants.PK_ERROR_not_implemented;
        return 0;
    }

    private static int RegularBlendNormal(FaceSlot face,in KernelVector3 point,out KernelVector3 normal)
    {
        normal=default;var record=Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)];
        var status=IntersectionSurfaceParameters(record,point,out var uv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];
        status=EvaluateSurfaceCore(in record,uv.param[0],uv.param[1],layout,jet,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        normal=Scale(ProfileCross(jet[layout.GetIndex(1,0)],jet[layout.GetIndex(0,1)]),LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(record.Sense));
        return ProfileNormalize(ref normal)?0:ParasolidConstants.PK_ERROR_at_singularity;
    }

    private static int RegularBlendContact(CurveTag curve,double first,double last,SurfTag cap,
        out double parameter,out KernelVector3 point)
    {
        parameter=0;point=default;Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        var status=IntersectSheetCurvePlane(curve,first,last,cap,roots,out var count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(count!=1||roots[0].Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_not_implemented;
        parameter=roots[0].First;point=roots[0].Point;return 0;
    }

    private static int AssembleRegularLocalBlend(BodySlot body,ReadOnlySpan<FaceSlot> sourceFaces,EdgeSlot selected,
        ReadOnlySpan<FaceSlot> supports,ReadOnlySpan<FaceSlot> caps,ReadOnlySpan<VertexSlot> ends,SurfTag blend,
        ReadOnlySpan<CurveTag> curves,ReadOnlySpan<double> lower,ReadOnlySpan<double> upper,ReadOnlySpan<KernelVector3> contacts,Span<FaceSlot> products)
    {
        var nv=Bodies[body].VertexCountBody;var ne=Bodies[body].EdgeCountBody;var nf=sourceFaces.Length;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[ne+4];Span<ModelingCoedge> inputCoedges=stackalloc ModelingCoedge[2*ne];
        Span<ModelingLoop> inputLoops=stackalloc ModelingLoop[nf];Span<ModelingFace> inputFaces=stackalloc ModelingFace[nf];
        Span<EdgeSlot> originals=stackalloc EdgeSlot[ne];Span<FaceSlot> sources=stackalloc FaceSlot[nf];
        Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];
        var model=new SheetArrangement{Points=stackalloc KernelVector3[nv+4],OriginalVertices=stackalloc VertexSlot[nv+4],Edges=edges,
            Coedges=stackalloc ModelingCoedge[2*ne+8],Loops=stackalloc ModelingLoop[nf+1],Faces=stackalloc ModelingFace[nf+1],
            Sources=stackalloc FaceSlot[nf+1],Witnesses=stackalloc PK_UV_s[nf+1],Tolerance=1e-9};
        Span<BodyTag> bodies=stackalloc BodyTag[1]{Bodies[body].Header.Tag};
        var error=ReadRegularBoundaries(bodies,model.Points,model.OriginalVertices,edges,originals,inputCoedges,inputLoops,inputFaces,sources,owners,offsets,
            out model.NP,out model.NE,out var nc,out var nl,out var faceCount);if(error!=0)return error;
        if(nl!=nf||faceCount!=nf)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<BufferOffset> vertices=stackalloc BufferOffset[4];
        for(var i=0;i<4;i++){vertices[i]=model.NP;model.Points[model.NP]=contacts[i];model.OriginalVertices[model.NP++]=-1;}
        Span<BufferOffset> newEdges=stackalloc BufferOffset[4];Span<ModelingCoedge> blendLoop=stackalloc ModelingCoedge[4];
        Span<byte> seen=stackalloc byte[4];seen.Clear();
        for(var i=0;i<4;i++)
        {
            var start=i<2?vertices[2*i]:vertices[i-2];var end=i<2?vertices[2*i+1]:vertices[i];
            var a=lower[i];var b=upper[i];if(b<a){(a,b)=(b,a);(start,end)=(end,start);}
            if(LocalSenseSign(Curves[GetCurveSlotByTag(curves[i])].Sense)<0)(start,end)=(end,start);
            if(b-a<1e-12)return ParasolidConstants.PK_ERROR_bad_parameter;
            newEdges[i]=model.NE;edges[model.NE++]=new(){Curve=curves[i],Start=a,End=b,StartVertex=start,EndVertex=end};
        }
        LastRegularBlendStage=RegularBlendStage.Trim;
        // Trim each of the four adjacent edges at its actual contact point.
        for(var e=0;e<ne;e++)
        {
            var old=Edges[originals[e]];if(originals[e]==selected)continue;
            for(var cap=0;cap<2;cap++)if(old.StartVertex==ends[cap]||old.EndVertex==ends[cap])
            {
                if(!LocalEdgeFaces(originals[e],out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                var side=supports.IndexOf(a);if(side<0)side=supports.IndexOf(b);if(side<0)return ParasolidConstants.PK_ERROR_not_implemented;
                var contact=2*side+cap;
                error=RegularBlendTrimParameter(old,old.StartVertex==ends[cap],contacts[contact],out var t,out var lowerEnd,out _);if(error!=0)return error;
                var atStart=old.StartVertex==ends[cap];if(atStart)edges[e].StartVertex=vertices[contact];else edges[e].EndVertex=vertices[contact];
                // Concave end corners extend one adjacent finite edge while
                // trimming the other. The exact base-curve inverse above is
                // still required; an artificial/full-domain copy is not used.
                if(lowerEnd)edges[e].Start=t;else edges[e].End=t;
            }
        }
        LastRegularBlendStage=RegularBlendStage.Loops;
        for(var f=0;f<nf;f++)
        {
            var source=sources[f];if(inputFaces[f].LoopCount!=1)return ParasolidConstants.PK_ERROR_not_implemented;
            var face=inputFaces[f];var loop=inputLoops[face.FirstLoop];var start=model.NC;var support=supports.IndexOf(source);var cap=caps.IndexOf(source);
            for(var k=0;k<loop.CoedgeCount;k++)
            {
                var coedge=inputCoedges[loop.FirstCoedge+k];var old=Edges[originals[coedge.Edge]];
                if(originals[coedge.Edge]==selected)
                {
                    if(support<0)return ParasolidConstants.PK_ERROR_bad_loop;
                    var oldStart=coedge.Forward!=0?old.StartVertex:old.EndVertex;var contact=2*support+(oldStart==ends[0]?0:1);
                    var replacement=new ModelingCoedge{Edge=newEdges[support],Forward=edges[newEdges[support]].StartVertex==vertices[contact]?(byte)1:(byte)0};
                    model.Coedges[model.NC++]=replacement;blendLoop[support]=new(){Edge=replacement.Edge,Forward=(byte)(replacement.Forward^1)};seen[support]++;
                    continue;
                }
                model.Coedges[model.NC++]=coedge;
                var oldEnd=coedge.Forward!=0?old.EndVertex:old.StartVertex;
                if(cap>=0&&oldEnd==ends[cap])
                {
                    var next=inputCoedges[loop.FirstCoedge+(k+1)%loop.CoedgeCount];
                    var from=coedge.Forward!=0?edges[coedge.Edge].EndVertex:edges[coedge.Edge].StartVertex;
                    var to=next.Forward!=0?edges[next.Edge].StartVertex:edges[next.Edge].EndVertex;var e=newEdges[2+cap];
                    var forward=edges[e].StartVertex==from&&edges[e].EndVertex==to;
                    if(!forward&&(edges[e].EndVertex!=from||edges[e].StartVertex!=to))return ParasolidConstants.PK_ERROR_bad_loop;
                    var replacement=new ModelingCoedge{Edge=e,Forward=forward?(byte)1:(byte)0};model.Coedges[model.NC++]=replacement;
                    blendLoop[2+cap]=new(){Edge=e,Forward=(byte)(replacement.Forward^1)};seen[2+cap]++;
                }
            }
            model.Loops[model.NL]=new(){FirstCoedge=start,CoedgeCount=model.NC-start};
            model.Faces[model.NF]=new(){Surface=face.Surface,Sense=face.Sense,FirstLoop=model.NL++,LoopCount=1};model.Sources[model.NF++]=source;
        }
        foreach(var count in seen)if(count!=1)return ParasolidConstants.PK_ERROR_bad_loop;
        // Order the four opposite coedges by shared vertices, preserving all
        // surrounding face orientations rather than guessing from coordinates.
        for(var i=1;i<4;i++)
        {
            var previous=blendLoop[i-1];var end=previous.Forward!=0?edges[previous.Edge].EndVertex:edges[previous.Edge].StartVertex;var found=-1;
            for(var j=i;j<4;j++)if((blendLoop[j].Forward!=0?edges[blendLoop[j].Edge].StartVertex:edges[blendLoop[j].Edge].EndVertex)==end){if(found>=0)return ParasolidConstants.PK_ERROR_bad_loop;found=j;}
            if(found<0)return ParasolidConstants.PK_ERROR_bad_loop;(blendLoop[i],blendLoop[found])=(blendLoop[found],blendLoop[i]);
        }
        error=SheetCycleWitness(blend,edges[..model.NE],blendLoop,out _,out var positive);if(error!=0)return error;
        model.Loops[model.NL]=new(){FirstCoedge=model.NC,CoedgeCount=4};blendLoop.CopyTo(model.Coedges[model.NC..]);model.NC+=4;
        model.Sources[model.NF]=-1;model.Faces[model.NF++]=new(){Surface=blend,Sense=positive!=0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c,FirstLoop=model.NL++,LoopCount=1};
        Span<BufferOffset> indices=stackalloc BufferOffset[model.NF];for(var i=0;i<indices.Length;i++)indices[i]=i;
        Span<byte> reversed=stackalloc byte[model.NF],cavities=stackalloc byte[model.NF];reversed.Clear();cavities.Clear();
        LastRegularBlendStage=RegularBlendStage.Build;
        error=CloneRegularBoundaryBody(ref model,indices,reversed,cavities,out var output,products[..model.NF]);if(error!=0)return error;
        LastRegularBlendStage=RegularBlendStage.Publish;
        return ReplaceLocalBody(body,output,sourceFaces,products);
    }

    private static int RegularBlendTrimParameter(in EdgeRecord edge,bool atStart,in KernelVector3 point,
        out double parameter,out bool lowerEnd,out bool extends)
    {
        parameter=0;extends=false;lowerEnd=atStart==(LocalSenseSign(Curves[GetCurveSlotByTag(edge.CurveTag)].Sense)>0);
        if(!TryProfileEdgeRange(edge,out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
        var p=new PK_VECTOR_s();WriteVector(&p,point);double t=0;
        var error=CurveParameteriseVectorCore(edge.CurveTag,p,&t);if(error!=0)return error;
        LiftPeriodicEdgeParameter(edge.CurveTag,first,last,ref t);
        var periodic=Curves[GetCurveSlotByTag(edge.CurveTag)].Class is CurveClass.Circle or CurveClass.Ellipse;
        if(periodic&&!OperationIntersection.InInterval(t,first,last))
        {
            // A principal inverse may be on the previous turn when the
            // replaced end extends. Keep the closest continuation of that
            // endpoint, without changing an already valid long-arc trim.
            var anchor=lowerEnd?first:last;t+=Math.Tau*Math.Round((anchor-t)/Math.Tau);
            if(Math.Abs(t-anchor)>=Math.PI)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        if(lowerEnd?t>=last-1e-10:t<=first+1e-10)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(periodic&&(lowerEnd?last-t:t-first)>=Math.Tau)return ParasolidConstants.PK_ERROR_not_implemented;
        parameter=t;extends=lowerEnd?t<first:t>last;return 0;
    }

    private static int ValidateRegularEndSeparation(ReadOnlySpan<CurveTag> curves,ReadOnlySpan<double> first,
        ReadOnlySpan<double> last,ReadOnlySpan<AnalyticSurface> termination)
    {
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];Span<KernelVector3> point=stackalloc KernelVector3[2];
        for(var cap=0;cap<2;cap++)
        {
            var lo=Math.Min(first[cap],last[cap]);var hi=Math.Max(first[cap],last[cap]);var opposite=termination[1-cap];
            var status=IntersectSheetCurvePlane(curves[cap],lo,hi,opposite,roots,out var count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            if(count!=0)return ParasolidConstants.PK_ERROR_radius_too_large;
            status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(curves[cap])],lo+(hi-lo)*.5,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var height=(new IntervalNumber(point[0].X)-opposite.Origin.X)*opposite.Axis.X
                +(new IntervalNumber(point[0].Y)-opposite.Origin.Y)*opposite.Axis.Y+(new IntervalNumber(point[0].Z)-opposite.Origin.Z)*opposite.Axis.Z;
            if(!height.Finite||height.Upper>=-1e-9)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        return 0;
    }

    private static int ValidateRegularBlendEndExtensions(BodySlot body,EdgeSlot selected,ReadOnlySpan<FaceSlot> supports,
        ReadOnlySpan<VertexSlot> ends,ReadOnlySpan<KernelVector3> contacts,SurfTag blend,double first,double last)
    {
        var extends=false;var edge=Bodies[body].FirstEdgeBody;
        for(var e=0;e<Bodies[body].EdgeCountBody;e++,edge=Edges[edge].NextInBody)
        {
            if(edge==selected)continue;var record=Edges[edge];
            for(var cap=0;cap<2;cap++)
            {
                if(record.StartVertex!=ends[cap]&&record.EndVertex!=ends[cap])continue;
                if(!LocalEdgeFaces(edge,out var a,out var b))return ParasolidConstants.PK_ERROR_not_implemented;
                var side=supports.IndexOf(a);if(side<0)side=supports.IndexOf(b);if(side<0)return ParasolidConstants.PK_ERROR_not_implemented;
                var error=RegularBlendTrimParameter(record,record.StartVertex==ends[cap],contacts[2*side+cap],out _,out _,out var extension);if(error!=0)return error;
                extends|=extension;
            }
        }
        if(!extends)return 0;
        // Growing a cap must not introduce another intersection with either
        // incident support. Certify global reach of every rolling ball over
        // the entire retained interval, including the finite end sections.
        var data=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(blend)].DataIndex];var program=GetNestedBlendProgram(data);
        return IntersectionError(JointNestedBlendEvaluation.CertifySplineReach(program,program.Surfaces[data.ProgramRoot].Spine,first,last));
    }
}
