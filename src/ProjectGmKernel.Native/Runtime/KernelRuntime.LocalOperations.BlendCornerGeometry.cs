using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.LocalOperations;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int CornerCylinderCap(in KernelVector3 center,in KernelVector3 axis,double radius,in KernelVector3 first,in KernelVector3 second,SurfTag cap,
        out CurveTag curve,out double lower,out double upper)
    {
        curve=0;lower=upper=0;if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(cap)],out var plane)||plane.Kind!=SurfaceClass.Plane)return CornerReject(BlendCornerEvent.UnsupportedSupport);
        Span<KernelVector3> points=stackalloc KernelVector3[2]{first,second};Span<ModelingEdge> edges=stackalloc ModelingEdge[1];edges[0]=new(){StartVertex=0,EndVertex=1};
        var model=new LocalBrep{Points=points,Edges=edges,PointCount=2,EdgeCount=1};
        var error=AddLocalCylinderSection(ref model,0,1,center,axis,radius,new LocalPlane{Normal=plane.Axis,Distance=Dot(plane.Axis,plane.Origin)});if(error!=0)return error;
        curve=edges[0].Curve;lower=edges[0].Start;upper=edges[0].End;return 0;
    }
    private static int CornerFarContact(CurveTag curve,double first,double last,SurfTag cap,out double parameter,out KernelVector3 point)
    {
        parameter=0;point=default;Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        var status=IntersectSheetCurvePlane(curve,first,last,cap,roots,out var count);LastBlendCornerRoots=count;LastBlendCornerRootStatus=status;
        if(status==AlgorithmStatus.InvalidInput)
        {
            LastCornerFirst=first;LastCornerLast=last;var cv=Curves[GetCurveSlotByTag(curve)];Span<KernelVector3> jet=stackalloc KernelVector3[1];
            LastCornerFirstStatus=EvaluateBlendContact(ICurveDataPool[cv.DataIndex],first,0,jet,out LastCornerStationFirst);LastCornerLastStatus=EvaluateBlendContact(ICurveDataPool[cv.DataIndex],last,0,jet,out LastCornerStationLast);
        }
        if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        if(count!=1||roots[0].Kind!=IntersectionVectorKind.Simple)return CornerReject(BlendCornerEvent.UnsupportedSupport);
        parameter=roots[0].First;point=roots[0].Point;return 0;
    }
    private static int CornerOffsetCenter(ReadOnlySpan<FaceSlot> faces,double radius,out KernelVector3 center)
    {
        center=default;Span<IntervalBox3> extents=stackalloc IntervalBox3[3];Span<SurfaceSlot> slots=stackalloc SurfaceSlot[3];Span<double> offsets=stackalloc double[3];
        for(var i=0;i<3;i++)
        {
            var status=RegularFiniteFaceExtent(faces[i],out extents[i]);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            slots[i]=GetSurfaceSlotByTag(Faces[faces[i]].SurfTag);offsets[i]=-radius*LocalSenseSign(Faces[faces[i]].Orientation)*LocalSenseSign(Surfaces[slots[i]].Sense);
        }
        var na=RegularSurfacePatchCount(slots[0]);var nb=RegularSurfacePatchCount(slots[1]);var nc=RegularSurfacePatchCount(slots[2]);
        if(na<=0||nb<=0||nc<=0||(long)na*nb*nc>64)return CornerReject(BlendCornerEvent.UnsupportedSupport);
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],cp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[8];var count=0;var original=new SurfaceIntersectionNode();
        for(var a=0;a<na;a++)
        {
            var status=RegularSurfacePatch(slots[0],a,extents[0],ap,out var abase);if(status!=AlgorithmStatus.Success)return IntersectionError(status);if(abase.Domain.U1<=abase.Domain.U0||abase.Domain.V1<=abase.Domain.V0)continue;
            var first=new SurfaceIntersectionPatch(abase,offsets[0]);
            for(var b=0;b<nb;b++)
            {
                status=RegularSurfacePatch(slots[1],b,extents[1],bp,out var bbase);if(status!=AlgorithmStatus.Success)return IntersectionError(status);if(bbase.Domain.U1<=bbase.Domain.U0||bbase.Domain.V1<=bbase.Domain.V0)continue;
                var second=new SurfaceIntersectionPatch(bbase,offsets[1]);
                for(var c=0;c<nc;c++)
                {
                    status=RegularSurfacePatch(slots[2],c,extents[2],cp,out var cbase);if(status!=AlgorithmStatus.Success)return IntersectionError(status);if(cbase.Domain.U1<=cbase.Domain.U0||cbase.Domain.V1<=cbase.Domain.V0)continue;
                    var third=new SurfaceIntersectionPatch(cbase,offsets[2]);status=SurfaceTripleRoots.Find(first,second,third,roots,out var n);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    for(var r=0;r<n;r++)
                    {
                        var root=roots[r];
                        if(count!=0&&OperationIntersection.Norm(Sub(center,root.Point))<1e-12*Math.Max(1,OperationIntersection.Norm(center))
                            &&Math.Abs(root.U-original.U)+Math.Abs(root.V-original.V)+Math.Abs(root.S-original.S)+Math.Abs(root.T-original.T)<1e-10)continue;
                        if(++count>1)return CornerReject(BlendCornerEvent.MultipleCenters);center=root.Point;original=root;
                    }
                }
            }
        }
        return count==1?0:CornerReject(BlendCornerEvent.MultipleCenters,ParasolidConstants.PK_ERROR_radius_too_large);
    }

    private static int CornerContact(FaceSlot face,in KernelVector3 center,double radius,out KernelVector3 point,out KernelVector3 outward)
    {
        point=outward=default;var record=Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)];
        if(TryPrepareSurface(record,out var plane)&&plane.Kind==SurfaceClass.Plane)
        {
            outward=Scale(plane.Axis,LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(record.Sense));
            point=Sub(center,Scale(plane.Axis,Dot(Sub(center,plane.Origin),plane.Axis)));
        }
        else
        {
            var checkpoint=CommandScratch.ThreadArenaOffset;
            try
            {
                var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
                var status=compiler.Surface(record.Header.Tag,out var index);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                var program=compiler.View;var node=program.Surfaces[index];
                status=NestedSplineEvaluation.Closest(program,node,center,out var u,out var v);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                status=NestedSplineEvaluation.Contact(program,node,u,v,out var contact);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                point=contact.Point;outward=Scale(contact.Normal,LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(record.Sense));
            }
            finally{CommandScratch.ThreadArenaOffset=checkpoint;}
        }
        return OperationIntersection.Norm(Sub(Sub(point,center),Scale(outward,radius)))<1e-8*Math.Max(1,radius)?0:CornerReject(BlendCornerEvent.MultipleCenters);
    }

    private static int CornerCircle(in KernelVector3 center,in KernelVector3 first,in KernelVector3 second,in KernelVector3 axis,double radius,out CurveTag curve,out double lower,out double upper,bool preserveAxis=false)
    {
        var reference=Sub(first,center);if(!ProfileNormalize(ref reference)){curve=0;lower=upper=0;return ParasolidConstants.PK_ERROR_at_singularity;}
        var direction=axis;if(!preserveAxis&&Dot(ProfileCross(reference,Sub(second,center)),direction)<0)direction=Scale(direction,-1);
        curve=CreateCircleCurveTag(center.X,center.Y,center.Z,direction.X,direction.Y,direction.Z,reference.X,reference.Y,reference.Z,radius);
        lower=upper=0;if(curve<=0)return ParasolidConstants.PK_ERROR_memory_full;return CornerArcRange(curve,first,second,out lower,out upper);
    }
    private static int CornerArcRange(CurveTag curve,in KernelVector3 first,in KernelVector3 second,out double lower,out double upper)
    {
        lower=upper=0;var p=new PK_VECTOR_s();WriteVector(&p,first);double a=0,b=0;var error=CurveParameteriseVectorCore(curve,p,&a);if(error!=0)return error;
        WriteVector(&p,second);error=CurveParameteriseVectorCore(curve,p,&b);if(error!=0)return error;
        if(Curves[GetCurveSlotByTag(curve)].Class==CurveClass.Circle)
        {
            if(a>Math.Tau-1e-9)a=0;if(b>Math.Tau-1e-9)b=0;
            if(Math.Abs(a-b)>Math.PI+1e-9){if(a<b)a+=Math.Tau;else b+=Math.Tau;}
        }
        lower=a;upper=b;return Math.Abs(a-b)>1e-11?0:CornerReject(BlendCornerEvent.ShortEdge,ParasolidConstants.PK_ERROR_radius_too_large);
    }
}
