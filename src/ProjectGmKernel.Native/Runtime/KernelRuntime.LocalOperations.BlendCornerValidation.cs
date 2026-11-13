using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Projection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal enum CornerCertificateStage:byte{None,FiniteContact,CenterMaterial,BallDistance,UniqueFoot,Domains,OwnSeparation,OtherFace,NeighborArcs,NeighborMonotonicity}
    [ThreadStatic] internal static CornerCertificateStage LastCornerCertificate;
    [ThreadStatic] internal static FaceSlot LastCornerCertificateFace;
    [ThreadStatic] internal static BufferOffset LastCornerCertificateBand;
    // A sufficient, geometry-invariant acceptance certificate: a material
    // rolling ball; whole band clearance from non-support faces; and strict
    // monotone separating projections between every pair of neighboring bands.
    private static int ValidateTrivalentCorner(BodySlot body,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<FaceSlot> supports,
        ReadOnlySpan<FaceSlot> pairs,ReadOnlySpan<FaceSlot> caps,ReadOnlySpan<SurfTag> bands,ReadOnlySpan<CurveTag> curves,
        ReadOnlySpan<double> lower,ReadOnlySpan<double> upper,ReadOnlySpan<KernelVector3> contacts,in KernelVector3 center,double radius,ReadOnlySpan<byte> vanished)
    {
        var tolerance=1e-9*Math.Max(1,radius);var point=new PK_VECTOR_s();var material=center;
        if(vanished.Contains((byte)1)){var radial=Sub(Add(Add(contacts[0],contacts[1]),contacts[2]),Scale(center,3));if(!ProfileNormalize(ref radial))return CornerReject(BlendCornerEvent.UncertifiedClearance);material=Add(center,Scale(radial,radius*.5));}
        WriteVector(&point,material);int enclosure;EntityTag topology;var visited=0;
        LastCornerCertificate=CornerCertificateStage.FiniteContact;var finiteError=CornerContactsInFaces(body,faces,supports,contacts);if(finiteError!=0)return finiteError;
        LastCornerCertificate=CornerCertificateStage.CenterMaterial;
        var error=BodyContainsVectorCore(Bodies[body].Header.Tag,point,&enclosure,&topology);if(error!=0)return error;
        if(enclosure!=ParasolidConstants.PK_enclosure_inside_c)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_radius_too_large);
        // The complete positive-rational hull distance search supplies a
        // lower distance bound, including portions outside a guessed local UV.
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],sampleJet=stackalloc IntervalNumber[9];
        foreach(var face in faces)
        {
            LastCornerCertificate=CornerCertificateStage.BallDistance;LastCornerCertificateFace=face;
            var support=supports.Contains(face);var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);var sf=Surfaces[slot];
            if(TryPrepareSurface(sf,out var plane)&&plane.Kind==SurfaceClass.Plane)
            {
                var sign=LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(sf.Sense);
                var distance=-sign*((new IntervalNumber(center.X)-plane.Origin.X)*plane.Axis.X+(new IntervalNumber(center.Y)-plane.Origin.Y)*plane.Axis.Y+(new IntervalNumber(center.Z)-plane.Origin.Z)*plane.Axis.Z);
                var zeroCap=false;for(var e=0;e<3;e++)if(vanished[e]!=0&&caps[e]==face)zeroCap=true;
                if(zeroCap)
                {
                    // A minor spherical triangle is the normalized positive
                    // cone of its three contact radii. Certifying all three
                    // generators behind a center plane certifies the patch.
                    if(Math.Abs(distance.Lower)>tolerance||Math.Abs(distance.Upper)>tolerance)return CornerReject(BlendCornerEvent.UncertifiedClearance);
                    foreach(var contact in contacts)if(sign*Dot(Sub(contact,plane.Origin),plane.Axis)>tolerance)return CornerReject(BlendCornerEvent.UncertifiedClearance);
                    continue;
                }
                if(distance.Lower<radius-tolerance||!support&&distance.Lower<=radius+tolerance)
                {
                    var cone=distance.Lower>=-tolerance;
                    foreach(var contact in contacts)if(sign*Dot(Sub(contact,center),plane.Axis)>tolerance)cone=false;
                    if(!cone)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
                }
            }
            else if(sf.Class==SurfaceClass.BSurface)
            {
                var view=GetBSurfaceView(BSurfaceDataStore[sf.DataIndex]);var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
                var regularity=RollingBallReach.ValidateC1NaturalDomain(view);if(regularity!=AlgorithmStatus.Success)return IntersectionError(regularity);
                var uv=new PK_UV_s();var strength=double.PositiveInfinity;var distanceSquared=new IntervalNumber(double.NegativeInfinity,double.PositiveInfinity);var gradientSquared=distanceSquared;
                if(support){var inverse=IntersectionSurfaceParameters(sf,contacts[supports.IndexOf(face)],out uv);if(inverse!=AlgorithmStatus.Success)return IntersectionError(inverse);}
                var count=(view.UVertexCount-view.UDegree)*(view.VVertexCount-view.VDegree);if(count>64)return CornerReject(BlendCornerEvent.UncertifiedClearance);
                for(var u=view.UDegree;u<view.UVertexCount;u++)for(var v=view.VDegree;v<view.VVertexCount;v++)
                {
                    var domain=new SurfaceParameterBox(view.UKnots[u],view.UKnots[u+1],view.VKnots[v],view.VKnots[v+1]);if(!(domain.U1>domain.U0&&domain.V1>domain.V0))continue;
                    var status=BezierSurfaceBounds.Extract(view,u,v,poles);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    var patch=new BezierSurfacePatch(poles[..(4*(view.UDegree+1)*(view.VDegree+1))],view.UDegree,view.VDegree,domain);
                    if(support)
                    {
                        LastCornerCertificate=CornerCertificateStage.UniqueFoot;var convex=CornerSquaredDistanceConvex(patch,center,ref visited,out var localStrength);if(convex!=0)return convex;strength=Math.Min(strength,localStrength);
                        if(uv.param[0]>=domain.U0&&uv.param[0]<=domain.U1&&uv.param[1]>=domain.V0&&uv.param[1]<=domain.V1)
                        {
                            status=BezierSurfaceBounds.Bound(patch,new(uv.param[0],uv.param[0],uv.param[1],uv.param[1]),sampleJet);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                            IntervalNumber square=0,gu=0,gv=0;for(var d=0;d<3;d++){var delta=sampleJet[d]-(d==0?center.X:d==1?center.Y:center.Z);square+=IntervalNumber.Square(delta);gu+=delta*sampleJet[3+d];gv+=delta*sampleJet[6+d];}
                            distanceSquared=square;gradientSquared=IntervalNumber.Square(gu)+IntervalNumber.Square(gv);
                        }
                    }
                    else
                    {
                        LastCornerCertificate=CornerCertificateStage.BallDistance;status=BSplineProjection.Closest(patch,center,.25*tolerance,ref best);if(status!=AlgorithmStatus.Success)return CornerReject(BlendCornerEvent.UncertifiedClearance,IntersectionError(status));
                    }
                }
                if(support)
                {
                    // Strong convexity: d_min^2 >= d(w)^2 - |grad g(w)|^2/mu,
                    // g=|S-C|^2/2. This closes the global distance gap at a
                    // certified stationary witness without a near-tangent BnB.
                    var lowerBound=distanceSquared-gradientSquared/strength;
                    if(!lowerBound.Finite||lowerBound.Lower<IntervalNumber.Square(new(radius-tolerance)).Upper)return CornerReject(BlendCornerEvent.UncertifiedClearance);
                }
                else if(best.DistanceUpper<=radius+2*tolerance)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_radius_too_large);
            }
            else
            {
                if(support)return CornerReject(BlendCornerEvent.UnsupportedSupport);
                var status=RegularFiniteFaceExtent(face,out var extent);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                if(!(center.X+radius<extent.XLo-tolerance||center.X-radius>extent.XHi+tolerance||center.Y+radius<extent.YLo-tolerance||center.Y-radius>extent.YHi+tolerance||center.Z+radius<extent.ZLo-tolerance||center.Z-radius>extent.ZHi+tolerance))return CornerReject(BlendCornerEvent.UncertifiedClearance);
            }
        }
        Span<SurfaceParameterBox> domains=stackalloc SurfaceParameterBox[3];Span<double> directions=stackalloc double[3];Span<KernelVector3> tangents=stackalloc KernelVector3[3];
        for(var e=0;e<3;e++)
        {
            if(vanished[e]!=0)continue;
            LastCornerCertificate=CornerCertificateStage.Domains;LastCornerCertificateBand=e;
            error=CornerBandDomain(bands[e],curves[4*e],curves[4*e+2],lower[4*e],upper[4*e],lower[4*e+2],upper[4*e+2],Faces[caps[e]].SurfTag,
                out domains[e],out directions[e],out tangents[e]);if(error!=0)return error;
        }
        for(var e=0;e<3;e++)
        {
            if(vanished[e]!=0)continue;
            LastCornerCertificate=CornerCertificateStage.OwnSeparation;LastCornerCertificateBand=e;
            // A band advances strictly away from its common spherical
            // section, so it cannot overlap the spherical triangle behind it.
            error=CornerProjectionMonotone(bands[e],curves[4*e+2],domains[e],directions[e],tangents[e],ref visited);if(error!=0)return error;
            var third=-1;for(var f=0;f<3;f++)if(supports[f]!=pairs[2*e]&&supports[f]!=pairs[2*e+1])third=f;
            if(third<0||Dot(tangents[e],Sub(contacts[third],center))>=-tolerance)return CornerReject(BlendCornerEvent.OverlappingBands);
            foreach(var face in faces)
            {
                if(face==pairs[2*e]||face==pairs[2*e+1]||face==caps[e])continue;
                LastCornerCertificate=CornerCertificateStage.OtherFace;LastCornerCertificateFace=face;
                var eventBoundary=false;for(var collapsed=0;collapsed<3;collapsed++)if(vanished[collapsed]!=0&&caps[collapsed]==face)eventBoundary=true;
                error=CornerBandFaceClearance(bands[e],curves[4*e+2],domains[e],face,tolerance,ref visited,eventBoundary);if(error!=0)return error;
            }
            for(var other=0;other<e;other++)
            {
                if(vanished[other]!=0)continue;
                var common=-1;for(var f=0;f<3;f++)if((supports[f]==pairs[2*e]||supports[f]==pairs[2*e+1])&&(supports[f]==pairs[2*other]||supports[f]==pairs[2*other+1]))common=f;
                if(common<0)return CornerReject(BlendCornerEvent.UnsupportedGraph);
                var separator=Sub(tangents[e],tangents[other]);if(!ProfileNormalize(ref separator))return CornerReject(BlendCornerEvent.OverlappingBands);
                LastCornerCertificate=CornerCertificateStage.NeighborArcs;error=CornerSectionSide(curves[4*e+2],lower[4*e+2],upper[4*e+2],contacts[common],separator,tolerance);if(error!=0)return error;
                error=CornerSectionSide(curves[4*other+2],lower[4*other+2],upper[4*other+2],contacts[common],Scale(separator,-1),tolerance);if(error!=0)return error;
                LastCornerCertificate=CornerCertificateStage.NeighborMonotonicity;error=CornerProjectionMonotone(bands[e],curves[4*e+2],domains[e],directions[e],separator,ref visited);if(error!=0)return error;
                error=CornerProjectionMonotone(bands[other],curves[4*other+2],domains[other],directions[other],Scale(separator,-1),ref visited);if(error!=0)return error;
            }
        }
        return 0;
    }

    private static int CornerBandDomain(SurfTag band,CurveTag contact,CurveTag section,double near,double far,double sectionA,double sectionB,SurfTag cap,
        out SurfaceParameterBox domain,out double direction,out KernelVector3 tangent)
    {
        domain=default;direction=1;tangent=default;var sf=Surfaces[GetSurfaceSlotByTag(band)];
        if(sf.Class==SurfaceClass.Cylinder)
        {
            var data=CylinderDataPool[sf.DataIndex];tangent=new(){X=data.AxisX,Y=data.AxisY,Z=data.AxisZ};
            TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cap)],out var plane);
            var projection=-CornerCylinderPlaneBound(band,section,new(0,0,Math.Min(sectionA,sectionB),Math.Max(sectionA,sectionB)),plane.Axis,plane.Origin);
            var denom=Dot(tangent,plane.Axis);if(Math.Abs(denom)<1e-10)return CornerReject(BlendCornerEvent.UnsupportedSupport);
            var length=projection/denom;
            if(!(length.Lower>1e-10||length.Upper< -1e-10))return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
            direction=length.Lower>0?1:-1;tangent=Scale(tangent,direction);
            domain=new(Math.Min(0,length.Lower),Math.Max(0,length.Upper),Math.Min(sectionA,sectionB),Math.Max(sectionA,sectionB));return 0;
        }
        var cv=Curves[GetCurveSlotByTag(contact)];var contactData=ICurveDataPool[cv.DataIndex];Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var status=EvaluateBlendContact(contactData,near,0,jet,out var u);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        status=EvaluateBlendContact(contactData,far,0,jet,out var end);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        direction=Math.Sign(end-u);if(direction==0)return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
        domain=direction>0?new(u,sf.UMax,0,1):new(sf.UMin,u,0,1);
        var dataBlend=BlendedEdgeDataPool[sf.DataIndex];var program=GetNestedBlendProgram(dataBlend);var spine=program.Surfaces[dataBlend.ProgramRoot].Spine;
        status=JointNestedBlendEvaluation.EvaluateSpine(program,spine,u,1,jet);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        tangent=Scale(jet[1],direction);return ProfileNormalize(ref tangent)?0:CornerReject(BlendCornerEvent.OverlappingBands);
    }

    private static int CornerContactsInFaces(BodySlot body,ReadOnlySpan<FaceSlot> sourceFaces,ReadOnlySpan<FaceSlot> supports,ReadOnlySpan<KernelVector3> contacts)
    {
        var nv=Bodies[body].VertexCountBody;var ne=Bodies[body].EdgeCountBody;var nf=Bodies[body].FaceCountBody;
        Span<KernelVector3> points=stackalloc KernelVector3[nv];Span<VertexSlot> vertices=stackalloc VertexSlot[nv];Span<ModelingEdge> edges=stackalloc ModelingEdge[ne];Span<EdgeSlot> originals=stackalloc EdgeSlot[ne];
        BufferCount loopCapacity=0;foreach(var face in sourceFaces)loopCapacity+=Faces[face].LoopCount;
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[2*ne];Span<ModelingLoop> loops=stackalloc ModelingLoop[loopCapacity];Span<ModelingFace> faces=stackalloc ModelingFace[nf];Span<FaceSlot> sources=stackalloc FaceSlot[nf];Span<BufferOffset> owners=stackalloc BufferOffset[nf],offsets=stackalloc BufferOffset[2];
        Span<BodyTag> inputs=stackalloc BodyTag[1]{Bodies[body].Header.Tag};var error=ReadRegularBoundaries(inputs,points,vertices,edges,originals,coedges,loops,faces,sources,owners,offsets,out _,out var ec,out var cc,out var lc,out var fc);if(error!=0)return error;
        var view=new RegularBoundaryView(edges[..ec],coedges[..cc],loops[..lc],faces[..fc]);
        for(var f=0;f<3;f++)
        {
            var status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(Faces[supports[f]].SurfTag)],contacts[f],out var uv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var index=sources[..fc].IndexOf(supports[f]);if(index<0)return CornerReject(BlendCornerEvent.UnsupportedGraph);
            if(CornerContactOnFiniteBoundary(supports[f],contacts[f]))continue;
            error=RegularFaceContains(view,index,uv,out var inside);if(error!=0)return error;if(!inside)return CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
        }
        return 0;
    }

    private static bool CornerContactOnFiniteBoundary(FaceSlot face,in KernelVector3 point)
    {
        var record=Faces[face];var loop=record.FirstLoop;var p=new PK_VECTOR_s();WriteVector(&p,point);Span<KernelVector3> jet=stackalloc KernelVector3[1];
        for(var l=0;l<record.LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;for(var f=0;f<Loops[loop].FinCount;f++,fin=Fins[fin].NextInLoop)
            {
                var edge=Edges[Fins[fin].Edge];var curve=Curves[GetCurveSlotByTag(edge.CurveTag)];if(curve.Class is not (CurveClass.Line or CurveClass.Circle))continue;
                if(!TryProfileEdgeRange(edge,out var first,out var last))continue;double parameter=0;if(CurveParameteriseVectorCore(edge.CurveTag,p,&parameter)!=0||parameter<first-1e-10||parameter>last+1e-10)continue;
                if(EvaluateCurveCore(in curve,parameter,0,false,jet,out _)==AlgorithmStatus.Success&&OperationIntersection.Norm(Sub(jet[0],point))<1e-10)return true;
            }
        }
        return false;
    }

    // Strict convexity of squared distance on the full natural UV rectangle
    // excludes a second contact/preimage of the same rolling center. It is a
    // sufficient certificate, not an assumption about input control points.
    private static int CornerSquaredDistanceConvex(in BezierSurfacePatch patch,in KernelVector3 center,ref BufferCount visited,out double strength)
    {
        strength=double.PositiveInfinity;
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[128];Span<BufferCount> depths=stackalloc BufferCount[128];pending[0]=patch.Domain;depths[0]=0;var count=1;Span<IntervalNumber> jets=stackalloc IntervalNumber[18];
        while(count>0)
        {
            if(++visited>131072)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            var at=--count;var box=pending[at];var depth=depths[at];var status=BezierSurfaceBounds.BoundSecond(patch,box,jets);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            IntervalNumber a=0,b=0,c=0;
            for(var d=0;d<3;d++)
            {
                var q=(d==0?center.X:d==1?center.Y:center.Z)-jets[d];
                a+=IntervalNumber.Square(jets[3+d])-q*jets[9+d];b+=jets[3+d]*jets[6+d]-q*jets[12+d];c+=IntervalNumber.Square(jets[6+d])-q*jets[15+d];
            }
            var determinant=a*c-IntervalNumber.Square(b);
            if(a.Lower>0&&c.Lower>0&&determinant.Lower>0){strength=Math.Min(strength,(determinant/(a+c)).Lower);continue;}
            if(depth>=28||count+2>pending.Length)return CornerReject(BlendCornerEvent.UncertifiedClearance);
            CornerSplit(box,patch.Domain,false,out var first,out var second);pending[count]=first;depths[count++]=depth+1;pending[count]=second;depths[count++]=depth+1;
        }
        return 0;
    }

    private static int CornerProjectionMonotone(SurfTag band,CurveTag section,in SurfaceParameterBox domain,double direction,in KernelVector3 normal,ref BufferCount visited)
    {
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[128];Span<BufferCount> depths=stackalloc BufferCount[128];pending[0]=domain;depths[0]=0;var count=1;Span<IntervalNumber> values=stackalloc IntervalNumber[9];
        while(count>0)
        {
            if(++visited>131072)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            var index=--count;var box=pending[index];var depth=depths[index];var status=CornerBandBounds(band,section,box,values);
            if(status==AlgorithmStatus.Success)
            {
                var derivative=direction*(values[3]*normal.X+values[4]*normal.Y+values[5]*normal.Z);if(derivative.Lower>1e-12)continue;
                if(derivative.Upper<=0)return CornerReject(BlendCornerEvent.OverlappingBands,ParasolidConstants.PK_ERROR_blends_overlap);
            }
            else if(status!=AlgorithmStatus.NotConverged)return IntersectionError(status);
            if(depth>=32||count+2>pending.Length)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            CornerSplit(box,domain,status==AlgorithmStatus.NotConverged,out var left,out var right);pending[count]=left;depths[count++]=depth+1;pending[count]=right;depths[count++]=depth+1;
        }
        return 0;
    }
    private static int CornerBandFaceClearance(SurfTag band,CurveTag section,in SurfaceParameterBox domain,FaceSlot face,double tolerance,ref BufferCount visited,bool eventBoundary=false)
    {
        var slot=GetSurfaceSlotByTag(Faces[face].SurfTag);var planar=RegularPlanarSupport(slot,out var plane,out var deviation);var normal=Scale(plane.Axis,LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(Surfaces[slot].Sense));
        var status=RegularFiniteFaceExtent(face,out var obstacle);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[128];Span<BufferCount> depths=stackalloc BufferCount[128];pending[0]=domain;depths[0]=0;var count=1;Span<IntervalNumber> values=stackalloc IntervalNumber[9];
        while(count>0)
        {
            if(++visited>131072)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            var index=--count;var box=pending[index];var depth=depths[index];
            if(planar&&Surfaces[GetSurfaceSlotByTag(band)].Class==SurfaceClass.Cylinder)
            {
                var direct=CornerCylinderPlaneBound(band,section,box,normal,plane.Origin)+new IntervalNumber(-deviation,deviation);
                if(direct.Upper< -tolerance||eventBoundary&&direct.Upper<=tolerance)continue;
            }
            status=CornerBandBounds(band,section,box,values);
            if(status==AlgorithmStatus.Success)
            {
                if(values[0].Upper<obstacle.XLo-tolerance||values[0].Lower>obstacle.XHi+tolerance||values[1].Upper<obstacle.YLo-tolerance||values[1].Lower>obstacle.YHi+tolerance||values[2].Upper<obstacle.ZLo-tolerance||values[2].Lower>obstacle.ZHi+tolerance)continue;
                if(planar){var distance=(values[0]-plane.Origin.X)*normal.X+(values[1]-plane.Origin.Y)*normal.Y+(values[2]-plane.Origin.Z)*normal.Z+new IntervalNumber(-deviation,deviation);if(distance.Upper< -tolerance||eventBoundary&&distance.Upper<=tolerance)continue;}
            }
            else if(status!=AlgorithmStatus.NotConverged)return IntersectionError(status);
            if(depth>=32||count+2>pending.Length)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            CornerSplit(box,domain,status==AlgorithmStatus.NotConverged,out var left,out var right);pending[count]=left;depths[count++]=depth+1;pending[count]=right;depths[count++]=depth+1;
        }
        return 0;
    }
    private static void CornerSplit(in SurfaceParameterBox box,in SurfaceParameterBox domain,bool forceU,out SurfaceParameterBox first,out SurfaceParameterBox second)
    {
        var u=forceU||(box.U1-box.U0)/(domain.U1-domain.U0)>=(box.V1-box.V0)/(domain.V1-domain.V0);var middle=u?(box.U0+box.U1)*.5:(box.V0+box.V1)*.5;
        first=u?new(box.U0,middle,box.V0,box.V1):new(box.U0,box.U1,box.V0,middle);second=u?new(middle,box.U1,box.V0,box.V1):new(box.U0,box.U1,middle,box.V1);
    }
    private static AlgorithmStatus CornerBandBounds(SurfTag band,CurveTag section,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        var record=Surfaces[GetSurfaceSlotByTag(band)];if(record.Class==SurfaceClass.Cylinder)return CornerCylinderBounds(band,section,box,output);
        var data=BlendedEdgeDataPool[record.DataIndex];return JointNestedBlendEvaluation.Bound(GetNestedBlendProgram(data,record),data.ProgramRoot,box,output);
    }
    private static IntervalNumber CornerCylinderPlaneBound(SurfTag band,CurveTag section,in SurfaceParameterBox box,in KernelVector3 normal,in KernelVector3 origin)
    {
        var circle=CircleDataPool[Curves[GetCurveSlotByTag(section)].DataIndex];var cylinder=CylinderDataPool[Surfaces[GetSurfaceSlotByTag(band)].DataIndex];
        var x=new KernelVector3{X=circle.RefDirX,Y=circle.RefDirY,Z=circle.RefDirZ};var y=ProfileCross(new(){X=circle.AxisX,Y=circle.AxisY,Z=circle.AxisZ},x);
        IntervalNumber constant=0,axial=0,cosine=0,sine=0;for(var d=0;d<3;d++)
        {
            var n=d==0?normal.X:d==1?normal.Y:normal.Z;
            constant+=(new IntervalNumber(d==0?circle.CenterX:d==1?circle.CenterY:circle.CenterZ)-(d==0?origin.X:d==1?origin.Y:origin.Z))*n;
            axial+=new IntervalNumber(d==0?cylinder.AxisX:d==1?cylinder.AxisY:cylinder.AxisZ)*n;cosine+=new IntervalNumber(d==0?x.X:d==1?x.Y:x.Z)*n;sine+=new IntervalNumber(d==0?y.X:d==1?y.Y:y.Z)*n;
        }
        IntervalDual.SinCos(new IntervalDual(new(box.V0,box.V1),0),out var s,out var c);return constant+axial*new IntervalNumber(box.U0,box.U1)+circle.Radius*(cosine*c.Value+sine*s.Value);
    }
    private static AlgorithmStatus CornerCylinderBounds(SurfTag band,CurveTag section,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        var circle=CircleDataPool[Curves[GetCurveSlotByTag(section)].DataIndex];var cylinder=CylinderDataPool[Surfaces[GetSurfaceSlotByTag(band)].DataIndex];
        var x=new KernelVector3{X=circle.RefDirX,Y=circle.RefDirY,Z=circle.RefDirZ};var axis=new KernelVector3{X=circle.AxisX,Y=circle.AxisY,Z=circle.AxisZ};var y=ProfileCross(axis,x);
        IntervalDual.SinCos(new IntervalDual(new(box.V0,box.V1),1),out var sine,out var cosine);IntervalNumber t=new(box.U0,box.U1);var r=circle.Radius;
        output[0]=circle.CenterX+cylinder.AxisX*t+r*(x.X*cosine.Value+y.X*sine.Value);output[1]=circle.CenterY+cylinder.AxisY*t+r*(x.Y*cosine.Value+y.Y*sine.Value);output[2]=circle.CenterZ+cylinder.AxisZ*t+r*(x.Z*cosine.Value+y.Z*sine.Value);
        output[3]=cylinder.AxisX;output[4]=cylinder.AxisY;output[5]=cylinder.AxisZ;
        output[6]=r*(x.X*cosine.Derivative+y.X*sine.Derivative);output[7]=r*(x.Y*cosine.Derivative+y.Y*sine.Derivative);output[8]=r*(x.Z*cosine.Derivative+y.Z*sine.Derivative);return AlgorithmStatus.Success;
    }
    private static int CornerSectionSide(CurveTag curve,double lower,double upper,in KernelVector3 anchor,in KernelVector3 normal,double tolerance)
    {
        var circle=CircleDataPool[Curves[GetCurveSlotByTag(curve)].DataIndex];var x=new KernelVector3{X=circle.RefDirX,Y=circle.RefDirY,Z=circle.RefDirZ};var axis=new KernelVector3{X=circle.AxisX,Y=circle.AxisY,Z=circle.AxisZ};var y=ProfileCross(axis,x);
        var constant=(new IntervalNumber(circle.CenterX)-anchor.X)*normal.X+(new IntervalNumber(circle.CenterY)-anchor.Y)*normal.Y+(new IntervalNumber(circle.CenterZ)-anchor.Z)*normal.Z;
        var a=circle.Radius*(new IntervalNumber(x.X)*normal.X+new IntervalNumber(x.Y)*normal.Y+new IntervalNumber(x.Z)*normal.Z);var b=circle.Radius*(new IntervalNumber(y.X)*normal.X+new IntervalNumber(y.Y)*normal.Y+new IntervalNumber(y.Z)*normal.Z);
        Span<double> lo=stackalloc double[64],hi=stackalloc double[64];Span<BufferCount> depths=stackalloc BufferCount[64];lo[0]=Math.Min(lower,upper);hi[0]=Math.Max(lower,upper);depths[0]=0;var count=1;var visited=0;
        while(count>0)
        {
            if(++visited>8192)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            var at=--count;IntervalDual.SinCos(new IntervalDual(new(lo[at],hi[at]),0),out var sine,out var cosine);var value=constant+a*cosine.Value+b*sine.Value;if(value.Lower>=-tolerance)continue;
            if(value.Upper< -tolerance)return CornerReject(BlendCornerEvent.OverlappingBands,ParasolidConstants.PK_ERROR_blends_overlap);
            var depth=depths[at];if(depth>=40||count+2>lo.Length)return CornerReject(BlendCornerEvent.UncertifiedClearance,ParasolidConstants.PK_ERROR_cant_do_intersect);
            var middle=(lo[at]+hi[at])*.5;var left=lo[at];var right=hi[at];lo[count]=left;hi[count]=middle;depths[count++]=depth+1;lo[count]=middle;hi[count]=right;depths[count++]=depth+1;
        }
        return 0;
    }
}
