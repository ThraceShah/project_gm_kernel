using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum BlendCornerStage:byte{None,Graph,OffsetRoot,Bands,Roll,Contacts,FarEnds,CornerSection,Ends,Clearance,Assemble,Publish,Release}
    internal enum BlendCornerEvent:byte{None,UnsupportedGraph,UnequalRadius,MixedConvexity,UnsupportedSupport,MultipleCenters,ShortEdge,OverlappingBands,UncertifiedClearance}
    [ThreadStatic] internal static BlendCornerStage LastBlendCornerStage;
    [ThreadStatic] internal static BlendCornerEvent LastBlendCornerEvent;
    [ThreadStatic] internal static BufferOffset LastBlendCornerPair;
    [ThreadStatic] internal static BufferCount LastBlendCornerRoots;
    [ThreadStatic] internal static AlgorithmStatus LastBlendCornerRootStatus;
    [ThreadStatic] internal static double LastCornerFirst,LastCornerLast,LastCornerStationFirst,LastCornerStationLast;
    [ThreadStatic] internal static AlgorithmStatus LastCornerFirstStatus,LastCornerLastStatus;

    private static int CornerReject(BlendCornerEvent kind,int error=ParasolidConstants.PK_ERROR_not_implemented)
    {LastBlendCornerEvent=kind;return error;}

    private static int BuildTrivalentLocalBlendCorner(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        var error=BuildTrivalentCornerCore(body,faces,selected,descriptions,products,out made);
        // A matched star owns its entire attempt. Never let a partial geometry
        // construction fall through to another local-operation algorithm.
        return error==ParasolidConstants.PK_ERROR_not_implemented&&LastBlendCornerStage>=BlendCornerStage.OffsetRoot?ParasolidConstants.PK_ERROR_cant_do_intersect:error;
    }

    private static int BuildTrivalentCornerCore(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<EdgeSlot> selected,
        Span<LocalBlendProduct> descriptions,Span<FaceSlot> products,out BufferCount made)
    {
        made=0;LastBlendCornerStage=BlendCornerStage.Graph;LastBlendCornerEvent=BlendCornerEvent.None;
        if(selected.Length!=3||Bodies[body].RegionCount!=2||Bodies[body].ShellCount!=2||products.Length<faces.Length+4||descriptions.Length<4)
            return CornerReject(BlendCornerEvent.UnsupportedGraph);
        Span<FaceSlot> supports=stackalloc FaceSlot[3],pairFaces=stackalloc FaceSlot[6],caps=stackalloc FaceSlot[3];
        Span<VertexSlot> far=stackalloc VertexSlot[3];Span<EdgeRecord> oldEdges=stackalloc EdgeRecord[3];
        supports.Fill(-1);var supportCount=0;var first=Edges[selected[0]];var radius=first.PendingBlendFirst;
        var vertex=first.StartVertex;
        for(var k=1;k<3;k++)if(Edges[selected[k]].StartVertex!=vertex&&Edges[selected[k]].EndVertex!=vertex){vertex=first.EndVertex;break;}
        if(vertex<0)return CornerReject(BlendCornerEvent.UnsupportedGraph);
        var incident=0;var scan=Bodies[body].FirstEdgeBody;
        for(var i=0;i<Bodies[body].EdgeCountBody;i++,scan=Edges[scan].NextInBody)if(Edges[scan].StartVertex==vertex||Edges[scan].EndVertex==vertex)incident++;
        if(incident!=3)return CornerReject(BlendCornerEvent.UnsupportedGraph);
        var error=SheetOriginalVertex(vertex,out var originalCorner);if(error!=0)return error;
        Span<KernelVector3> farPoints=stackalloc KernelVector3[3],contacts=stackalloc KernelVector3[3],normals=stackalloc KernelVector3[3];
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        Span<SurfTag> capPlanes=stackalloc SurfTag[3];Span<byte> ownCaps=stackalloc byte[3];ownCaps.Clear();
        for(var e=0;e<3;e++)
        {
            var edge=Edges[selected[e]];oldEdges[e]=edge;
            if(edge.PendingBlendKind!=1||edge.PendingBlendFirst!=radius||edge.PendingBlendSecond!=radius)return CornerReject(BlendCornerEvent.UnequalRadius);
            if(edge.StartVertex!=vertex&&edge.EndVertex!=vertex||!LocalEdgeFaces(selected[e],out var a,out var b))return CornerReject(BlendCornerEvent.UnsupportedGraph);
            far[e]=edge.StartVertex==vertex?edge.EndVertex:edge.StartVertex;if(far[e]<0)return CornerReject(BlendCornerEvent.UnsupportedGraph);
            error=SheetOriginalVertex(far[e],out farPoints[e]);if(error!=0)return error;
            pairFaces[2*e]=a;pairFaces[2*e+1]=b;
            for(var side=0;side<2;side++)
            {
                var face=pairFaces[2*e+side];var sf=GetSurfaceSlotByTag(Faces[face].SurfTag);
                if(sf<0||Surfaces[sf].Class is not (SurfaceClass.Plane or SurfaceClass.BSurface))return CornerReject(BlendCornerEvent.UnsupportedSupport);
                if(!supports[..supportCount].Contains(face)){if(supportCount==3)return CornerReject(BlendCornerEvent.UnsupportedGraph);supports[supportCount++]=face;}
            }
            if(!TryProfileEdgeRange(edge,out var lo,out var hi))return CornerReject(BlendCornerEvent.UnsupportedGraph);
            var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(edge.CurveTag)],(lo+hi)*.5,1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            error=RegularBlendNormal(a,jet[0],out var na);if(error!=0)return error;error=RegularBlendNormal(b,jet[0],out var nb);if(error!=0)return error;
            var fin=edge.FirstFinEdge;if(Loops[Fins[fin].Loop].Face!=a)fin=Fins[fin].Other;
            var turn=Dot(ProfileCross(na,nb),jet[1])*(Fins[fin].Sense=='+'?1:-1)*LocalSenseSign(Curves[GetCurveSlotByTag(edge.CurveTag)].Sense);
            if(!(turn>1e-10*OperationIntersection.Norm(jet[1])))return CornerReject(BlendCornerEvent.MixedConvexity);
            error=RegularBlendEndFace(body,far[e],a,b,out caps[e]);if(error!=0)return CornerReject(BlendCornerEvent.UnsupportedSupport,error);
            capPlanes[e]=Faces[caps[e]].SurfTag;
            if(Surfaces[GetSurfaceSlotByTag(capPlanes[e])].Class!=SurfaceClass.Plane)
            {
                if(!RegularPlanarSupport(GetSurfaceSlotByTag(capPlanes[e]),out _,out _))return CornerReject(BlendCornerEvent.UnsupportedSupport);
            }
        }
        if(supportCount!=3)return CornerReject(BlendCornerEvent.UnsupportedGraph);
        foreach(var cap in caps)if(supports.Contains(cap))return CornerReject(BlendCornerEvent.UnsupportedGraph);
        for(var f=0;f<3;f++){var uses=0;foreach(var face in pairFaces)if(face==supports[f])uses++;if(uses!=2)return CornerReject(BlendCornerEvent.UnsupportedGraph);}
        LastBlendCornerStage=BlendCornerStage.OffsetRoot;
        for(var e=0;e<3;e++)if(Surfaces[GetSurfaceSlotByTag(capPlanes[e])].Class!=SurfaceClass.Plane)
        {
            RegularPlanarSupport(GetSurfaceSlotByTag(capPlanes[e]),out var p,out _);
            capPlanes[e]=CreatePlaneSurfaceTag(p.Origin.X,p.Origin.Y,p.Origin.Z,p.Axis.X,p.Axis.Y,p.Axis.Z,p.X.X,p.X.Y,p.X.Z);
            if(capPlanes[e]<=0)return ParasolidConstants.PK_ERROR_memory_full;ownCaps[e]=1;
        }
        error=CornerOffsetCenter(supports,radius,out var center);if(error!=0)return error;
        for(var f=0;f<3;f++){error=CornerContact(supports[f],center,radius,out contacts[f],out normals[f]);if(error!=0)return error;}
        Span<SurfTag> bands=stackalloc SurfTag[3];Span<CurveTag> curves=stackalloc CurveTag[12];Span<double> lower=stackalloc double[12],upper=stackalloc double[12];
        Span<KernelVector3> distantContacts=stackalloc KernelVector3[6];bands.Clear();curves.Clear();
        Span<double> stations=stackalloc double[2],farStations=stackalloc double[2];Span<AnalyticSurface> termination=stackalloc AnalyticSurface[2];
        Span<SheetCutCurve> capCuts=stackalloc SheetCutCurve[GeneralIntersectionBranchLimit];
        for(var e=0;e<3;e++)
        {
            LastBlendCornerPair=e;
            LastBlendCornerStage=BlendCornerStage.Bands;
            var fa=pairFaces[2*e];var fb=pairFaces[2*e+1];var sa=Faces[fa].SurfTag;var sb=Faces[fb].SurfTag;
            var a=supports.IndexOf(fa);var b=supports.IndexOf(fb);var analytic=Surfaces[GetSurfaceSlotByTag(sa)].Class==SurfaceClass.Plane&&Surfaces[GetSurfaceSlotByTag(sb)].Class==SurfaceClass.Plane;
            if(analytic)
            {
                var axis=ProfileCross(normals[a],normals[b]);if(!ProfileNormalize(ref axis))return CornerReject(BlendCornerEvent.UnsupportedSupport);
                if(Dot(axis,Sub(farPoints[e],originalCorner))<0)axis=Scale(axis,-1);
                TryProfileEdgeRange(oldEdges[e],out var carrierFirst,out var carrierLast);
                var carrierStatus=EvaluateCurveCore(in Curves[GetCurveSlotByTag(oldEdges[e].CurveTag)],(carrierFirst+carrierLast)*.5,1,false,jet,out _);if(carrierStatus!=AlgorithmStatus.Success)return EvaluationError(carrierStatus);
                var carrierDirection=jet[1];if(!ProfileNormalize(ref carrierDirection))return CornerReject(BlendCornerEvent.UnsupportedSupport);
                bands[e]=CreateCylinderSurfaceTag(center.X,center.Y,center.Z,axis.X,axis.Y,axis.Z,normals[a].X,normals[a].Y,normals[a].Z,radius);
                if(bands[e]<=0)return ParasolidConstants.PK_ERROR_memory_full;
                for(var side=0;side<2;side++)
                {
                    var at=4*e+side;var contact=contacts[side==0?a:b];
                    if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(capPlanes[e])],out var cap))return CornerReject(BlendCornerEvent.UnsupportedSupport);
                    var denom=Dot(axis,cap.Axis);if(Math.Abs(denom)<1e-10)return CornerReject(BlendCornerEvent.UnsupportedSupport);
                    var distance=Dot(Sub(cap.Origin,contact),cap.Axis)/denom;if(!(distance>1e-9))return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
                    distantContacts[2*e+side]=Add(contact,Scale(axis,distance));
                    curves[at]=CreateLineCurveTag(contact.X,contact.Y,contact.Z,carrierDirection.X,carrierDirection.Y,carrierDirection.Z);if(curves[at]<=0)return ParasolidConstants.PK_ERROR_memory_full;
                    lower[at]=0;upper[at]=Dot(Sub(distantContacts[2*e+side],contact),carrierDirection);
                }
                error=CornerCircle(center,contacts[a],contacts[b],carrierDirection,radius,out curves[4*e+2],out lower[4*e+2],out upper[4*e+2],true);if(error!=0)return error;
                error=CornerCylinderCap(center,axis,radius,distantContacts[2*e],distantContacts[2*e+1],capPlanes[e],out curves[4*e+3],out lower[4*e+3],out upper[4*e+3]);if(error!=0)return error;
            }
            else
            {
                var opts=new PK_SURF_create_blend_o_s{o_t_version=1,have_help=1};WriteVector(&opts.help,Scale(Add(originalCorner,farPoints[e]),.5));
                LastBlendCornerStage=BlendCornerStage.Roll;
                var third=-1;for(var f=0;f<3;f++)if(supports[f]!=fa&&supports[f]!=fb)third=f;
                if(third<0||!RegularPlanarSupport(GetSurfaceSlotByTag(Faces[supports[third]].SurfTag),out var nearPlane,out _))return CornerReject(BlendCornerEvent.UnsupportedSupport);
                TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(capPlanes[e])],out var endPlane);
                nearPlane=new(nearPlane.Kind,nearPlane.Origin,Scale(nearPlane.Axis,LocalSenseSign(Faces[supports[third]].Orientation)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(Faces[supports[third]].SurfTag)].Sense)),nearPlane.X);
                endPlane=new(endPlane.Kind,endPlane.Origin,Scale(endPlane.Axis,LocalSenseSign(Faces[caps[e]].Orientation)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(Faces[caps[e]].SurfTag)].Sense)),endPlane.X);
                termination[0]=nearPlane;termination[1]=endPlane;
                SurfTag created=0;error=CreateFiniteRollingBlend(sa,-LocalSenseSign(Faces[fa].Orientation)*radius,sb,-LocalSenseSign(Faces[fb].Orientation)*radius,originalCorner,farPoints[e],&opts,&created,termination);if(error!=0)return error;bands[e]=created;
                for(var side=0;side<2;side++)
                {
                    LastBlendCornerStage=BlendCornerStage.Contacts;
                    var at=4*e+side;error=CreateBlendContactCurve(bands[e],side,out curves[at]);if(error!=0)return error;
                    var p=new PK_VECTOR_s();WriteVector(&p,contacts[side==0?a:b]);
                    double contactParameter=0;error=CurveParameteriseVectorCore(curves[at],p,&contactParameter);if(error!=0)
                    {
                        // Contact parameter side follows Blend's section,
                        // which can reverse the support ordering.
                        var other=contacts[side==0?b:a];WriteVector(&p,other);error=CurveParameteriseVectorCore(curves[at],p,&contactParameter);if(error!=0)return error;
                    }
                    lower[at]=contactParameter;
                    var cv=Curves[GetCurveSlotByTag(curves[at])];var status=EvaluateBlendContact(ICurveDataPool[cv.DataIndex],lower[at],0,jet,out stations[side]);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    LastBlendCornerStage=BlendCornerStage.FarEnds;error=CornerFarContact(curves[at],cv.TMin,cv.TMax,capPlanes[e],out upper[at],out distantContacts[2*e+side]);if(error!=0)return error;
                    // The geometric contact curves retain their actual support;
                    // re-order them to pairFaces only after matching the point.
                }
                var q=new PK_VECTOR_s();WriteVector(&q,contacts[a]);double t=0;
                if(CurveParameteriseVectorCore(curves[4*e],q,&t)!=0)
                {
                    (curves[4*e],curves[4*e+1])=(curves[4*e+1],curves[4*e]);(lower[4*e],lower[4*e+1])=(lower[4*e+1],lower[4*e]);(upper[4*e],upper[4*e+1])=(upper[4*e+1],upper[4*e]);
                    (distantContacts[2*e],distantContacts[2*e+1])=(distantContacts[2*e+1],distantContacts[2*e]);
                }
                if(Math.Abs(stations[0]-stations[1])>1e-8)return CornerReject(BlendCornerEvent.MultipleCenters);
                LastBlendCornerStage=BlendCornerStage.CornerSection;error=NestedBlendIsoparam(bands[e],true,(stations[0]+stations[1])*.5,0,1,out curves[4*e+2],out _,out _);if(error!=0)return error;
                error=CornerArcRange(curves[4*e+2],contacts[a],contacts[b],out lower[4*e+2],out upper[4*e+2]);if(error!=0)return error;
                for(var side=0;side<2;side++){var cv=Curves[GetCurveSlotByTag(curves[4*e+side])];var status=EvaluateBlendContact(ICurveDataPool[cv.DataIndex],upper[4*e+side],0,jet,out farStations[side]);if(status!=AlgorithmStatus.Success)return EvaluationError(status);}
                if(Math.Abs(farStations[0]-farStations[1])<1e-10)
                {
                    error=NestedBlendIsoparam(bands[e],true,(farStations[0]+farStations[1])*.5,0,1,out var section,out _,out _);if(error!=0)return error;
                    var circle=CircleDataPool[Curves[GetCurveSlotByTag(section)].DataIndex];TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(capPlanes[e])],out var plane);
                    if(OperationIntersection.Norm(ProfileCross(new(){X=circle.AxisX,Y=circle.AxisY,Z=circle.AxisZ},plane.Axis))<1e-10
                        &&Math.Abs(Dot(Sub(new(){X=circle.CenterX,Y=circle.CenterY,Z=circle.CenterZ},plane.Origin),plane.Axis))<1e-9)curves[4*e+3]=section;
                    else{error=EntityDelete(1,&section);if(error!=0)return error;}
                }
                if(curves[4*e+3]==0)
                {
                    error=PrepareSheetCutCurves(bands[e],capPlanes[e],capCuts,out var n);if(error!=0)return error;
                    if(n!=1||capCuts[0].Closed!=0)return CornerReject(BlendCornerEvent.UnsupportedSupport);curves[4*e+3]=capCuts[0].Curve;
                }
                error=CornerArcRange(curves[4*e+3],distantContacts[2*e],distantContacts[2*e+1],out lower[4*e+3],out upper[4*e+3]);if(error!=0)return error;
            }
            for(var side=0;side<2;side++)if(Math.Abs(upper[4*e+side]-lower[4*e+side])<1e-10)return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
        }
        LastBlendCornerStage=BlendCornerStage.Clearance;
        error=ValidateTrivalentCorner(body,faces,supports,pairFaces,caps,bands,curves,lower,upper,contacts,center,radius);if(error!=0)return error;
        var determinant=Dot(normals[0],ProfileCross(normals[1],normals[2]));if(Math.Abs(determinant)<1e-10)return CornerReject(BlendCornerEvent.UnsupportedSupport);
        var sum=Scale(Add(Add(ProfileCross(normals[1],normals[2]),ProfileCross(normals[2],normals[0])),ProfileCross(normals[0],normals[1])),1/determinant);if(!ProfileNormalize(ref sum))return CornerReject(BlendCornerEvent.UnsupportedSupport);
        var sphereAxis=ProfileCross(sum,normals[0]);if(!ProfileNormalize(ref sphereAxis))return CornerReject(BlendCornerEvent.UnsupportedSupport);
        var reference=Scale(sum,-1);var sphere=CreateSphereSurfaceTag(center.X,center.Y,center.Z,sphereAxis.X,sphereAxis.Y,sphereAxis.Z,reference.X,reference.Y,reference.Z,radius);
        if(sphere<=0)return ParasolidConstants.PK_ERROR_memory_full;
        LastBlendCornerStage=BlendCornerStage.Assemble;
        var vertexTag=Vertices[vertex].Header.Tag;
        for(var e=0;e<3;e++)descriptions[e]=new(){Edge=oldEdges[e].Header.Tag,First=Faces[pairFaces[2*e]].Header.Tag,Second=Faces[pairFaces[2*e+1]].Header.Tag};
        descriptions[3]=new(){Edge=oldEdges[0].Header.Tag,Vertex=vertexTag,First=Faces[supports[0]].Header.Tag,Second=Faces[supports[1]].Header.Tag,Third=Faces[supports[2]].Header.Tag};
        error=AssembleTrivalentBlendCorner(body,faces,selected,vertex,supports,pairFaces,caps,far,bands,sphere,curves,lower,upper,contacts,distantContacts,products);if(error!=0)return error;
        LastBlendCornerStage=BlendCornerStage.Release;
        foreach(var curve in curves)if(IsValidTag(curve)){var tag=curve;error=EntityDelete(1,&tag);if(error!=0)return error;}
        foreach(var band in bands)if(IsValidTag(band)&&GeometricOwnerCount(PoolKind.Surface,GetSurfaceSlotByTag(band))==0){var tag=band;error=EntityDelete(1,&tag);if(error!=0)return error;}
        if(IsValidTag(sphere)&&GeometricOwnerCount(PoolKind.Surface,GetSurfaceSlotByTag(sphere))==0){error=EntityDelete(1,&sphere);if(error!=0)return error;}
        for(var i=0;i<3;i++)if(ownCaps[i]!=0&&IsValidTag(capPlanes[i])){var tag=capPlanes[i];error=EntityDelete(1,&tag);if(error!=0)return error;}
        made=4;return 0;
    }

}
