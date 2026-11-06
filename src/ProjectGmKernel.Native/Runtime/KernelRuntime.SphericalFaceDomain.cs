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
#if KERNEL_TEST_HOOKS
    internal static int TestSphericalCycle(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        PK_UV_s uv,out BufferCount winding,out byte positive)
    {
        var owns=EnsureCommandScratch();positive=0;
        try{var error=SphericalLoopWinding(surface,edges,loop,uv,out winding);return error!=0?error:SphericalCycleWitness(surface,edges,loop,out _,out positive);}
        finally{if(owns)ReleaseCommandScratch(State.Session);}
    }
#endif
    private static int ValidateSphericalFaceDomain(FaceSlot face)
    {
        var record=Faces[face];if(record.LoopCount==0)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<ModelingEdge> edges=stackalloc ModelingEdge[2048];Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[2048];
        var reverse=LocalSenseSign(record.Orientation)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(record.SurfTag)].Sense)<0;var loop=record.FirstLoop;
        for(var l=0;l<record.LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var count=Loops[loop].FinCount;if(count>edges.Length)return ParasolidConstants.PK_ERROR_not_implemented;var fin=Loops[loop].FirstFin;
            for(var i=0;i<count;i++,fin=Fins[fin].NextInLoop)
            {
                var edge=Edges[Fins[fin].Edge];if(!TryProfileEdgeRange(edge,out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
                edges[i]=new(){Curve=edge.CurveTag,Start=first,End=last,StartVertex=edge.StartVertex,EndVertex=edge.EndVertex};
                coedges[reverse?count-1-i:i]=new(){Edge=i,Forward=(Fins[fin].Sense=='+')!=reverse?(byte)1:(byte)0};
            }
            var error=SphericalCycleWitness(record.SurfTag,edges[..count],coedges[..count],out _,out var positive);if(error!=0)return error;
            // Complements of these loops need a global spherical domain.
            if(positive!=(l==0?1:0))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        return 0;
    }

    private static int SphericalLoopFrame(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        out AnalyticSurface sphere,out KernelVector3 normal,out KernelVector3 x,out KernelVector3 y)
    {
        normal=x=y=default;if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out sphere))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> point=stackalloc KernelVector3[2];
        // These evaluations choose a chart only. Every point of every edge
        // must subsequently pass the positive-hemisphere interval proof.
        foreach(ref readonly var coedge in loop)
        {
            var edge=edges[coedge.Edge];var status=EvaluateCurveCore(in Curves[GetCurveSlotByTag(edge.Curve)],(edge.Start+edge.End)*.5,0,false,point,out _);
            if(status!=AlgorithmStatus.Success)return EvaluationError(status);normal=Add(normal,Sub(point[0],sphere.Origin));
        }
        if(!ProfileNormalize(ref normal))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[68],restricted=stackalloc IntervalNumber[68];
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[32];Span<BufferCount> depths=stackalloc BufferCount[32];var budget=4096;
        foreach(ref readonly var coedge in loop)
        {
            var edge=edges[coedge.Edge];var interval=new PK_INTERVAL_s();interval.value[0]=edge.Start;interval.value[1]=edge.End;
            var error=ResolveIntersectionCurve(edge.Curve,interval,out var span);if(error!=0)return error;
            var pieces=GeneralCurvePieceCount(span);if(pieces<=0)return ParasolidConstants.PK_ERROR_not_implemented;
            for(var piece=0;piece<pieces;piece++)
            {
                var status=GeneralCurvePiece(span,piece,poles,out var degree,out var domain,out var lo,out var hi);if(status!=AlgorithmStatus.Success)return IntersectionError(status);if(!(hi>lo))continue;
                var curve=new BezierSurfacePatch(poles[..(4*(degree+1))],degree,0,domain);pending[0]=new(lo,hi,0,1);depths[0]=0;var n=1;
                while(n>0)
                {
                    if(--budget<0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    var box=pending[--n];var depth=depths[n];status=BezierSurfaceBounds.Restrict(curve,box,restricted);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                    var positive=true;
                    for(var i=0;i<=degree;i++)
                    {
                        var at=4*i;var w=restricted[at+3];if(w.Lower<=0)return ParasolidConstants.PK_ERROR_not_implemented;
                        var projection=(restricted[at]-sphere.Origin.X*w)*normal.X+(restricted[at+1]-sphere.Origin.Y*w)*normal.Y+(restricted[at+2]-sphere.Origin.Z*w)*normal.Z;
                        positive&=projection.Lower>1e-12*sphere.Radius*w.Upper;
                    }
                    if(positive)continue;
                    if(depth==16||n+2>pending.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    var mid=box.U0+(box.U1-box.U0)*.5;pending[n]=new(box.U0,mid,0,1);depths[n++]=depth+1;pending[n]=new(mid,box.U1,0,1);depths[n++]=depth+1;
                }
            }
        }
        x=OperationIntersection.Reference(normal);y=ProfileCross(normal,x);return 0;
    }

    internal static int SphericalLoopWinding(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        PK_UV_s uv,out BufferCount winding)
    {
        winding=0;var error=SphericalLoopFrame(surface,edges,loop,out var sphere,out var normal,out var x,out var y);if(error!=0)return error;
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> point=stackalloc KernelVector3[1];
        var status=EvaluateSurfaceCore(in Surfaces[GetSurfaceSlotByTag(surface)],uv.param[0],uv.param[1],layout,point,0);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        return SphericalWindingPoint(sphere,normal,x,y,edges,loop,point[0],out winding);
    }

    private static int SphericalWindingPoint(in AnalyticSurface sphere,in KernelVector3 normal,in KernelVector3 x,in KernelVector3 y,
        ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,in KernelVector3 point,out BufferCount winding)
    {
        winding=0;var relative=Sub(point,sphere.Origin);var w=Dot(relative,normal);if(w<=0)return 0;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];Span<KernelVector3> jet=stackalloc KernelVector3[2];
        for(var attempt=0;attempt<6;attempt++)
        {
            winding=0;var angle=attempt*.6180339887498949;var xx=Add(Scale(x,Math.Cos(angle)),Scale(y,Math.Sin(angle)));var yy=ProfileCross(normal,xx);
            var qx=Dot(relative,xx)/w;var qy=Dot(relative,yy)/w;var planeNormal=Sub(yy,Scale(normal,qy));ProfileNormalize(ref planeNormal);
            var plane=new AnalyticSurface(SurfaceClass.Plane,sphere.Origin,planeNormal,xx);var retry=false;
            foreach(ref readonly var coedge in loop)
            {
                var edge=edges[coedge.Edge];var status=IntersectSheetCurvePlane(edge.Curve,edge.Start,edge.End,plane,roots,out var count);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                for(var i=0;i<count;i++)
                {
                    var root=roots[i];if(root.Kind!=IntersectionVectorKind.Simple){retry=true;break;}
                    var r=Sub(root.Point,sphere.Origin);var denominator=Dot(r,normal);if(denominator<=0)return ParasolidConstants.PK_ERROR_eval_failure;
                    var along=Dot(r,xx)/denominator-qx;if(along< -1e-10)continue;if(along<=1e-10){retry=true;break;}
                    var duplicate=false;
                    for(var j=0;j<i;j++)if(ProfileDistance(root.Point,roots[j].Point)<1e-9)
                    {
                        var periodic=Curves[GetCurveSlotByTag(edge.Curve)].Class is CurveClass.Circle or CurveClass.Ellipse;
                        if(!periodic||Math.Abs(Math.Abs(root.First-roots[j].First)-Math.Tau)>1e-10){retry=true;break;}duplicate=true;
                    }
                    if(retry)break;if(duplicate)continue;
                    var epsilon=1e-10*Math.Max(1,edge.End-edge.Start);
                    if(edge.StartVertex>=0&&(Math.Abs(root.First-edge.Start)<epsilon||Math.Abs(root.First-edge.End)<epsilon)){retry=true;break;}
                    var curve=Curves[GetCurveSlotByTag(edge.Curve)];status=EvaluateCurveCore(in curve,root.First,1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
                    var tangent=Scale(jet[1],(coedge.Forward!=0?1:-1)*LocalSenseSign(curve.Sense));
                    var crossing=Dot(tangent,yy)-qy*Dot(tangent,normal);
                    if(Math.Abs(crossing)<=1e-12*OperationIntersection.Norm(tangent)){retry=true;break;}
                    winding+=crossing>0?1:-1;
                }
                if(retry)break;
            }
            if(!retry)return 0;
        }
        return ParasolidConstants.PK_ERROR_eval_failure;
    }

    internal static int SphericalCycleWitness(SurfTag surface,ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop,
        out PK_UV_s left,out byte positive)
    {
        left=default;positive=0;var error=SphericalLoopFrame(surface,edges,loop,out var sphere,out var normal,out var x,out var y);if(error!=0)return error;
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        for(var attempt=0;attempt<24;attempt++)
        {
            var coedge=loop[attempt%loop.Length];var edge=edges[coedge.Edge];var curve=Curves[GetCurveSlotByTag(edge.Curve)];
            var fraction=.2931471805599453+(attempt%5)*.071;var t=edge.Start+(edge.End-edge.Start)*fraction;
            var status=EvaluateCurveCore(in curve,t,1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            var relative=Sub(jet[0],sphere.Origin);var w=Dot(relative,normal);if(w<=0)return ParasolidConstants.PK_ERROR_eval_failure;
            var px=Dot(relative,x)/w;var py=Dot(relative,y)/w;var sign=(coedge.Forward!=0?1:-1)*LocalSenseSign(curve.Sense);
            var dx=(Dot(jet[1],x)-px*Dot(jet[1],normal))*sign/w;var dy=(Dot(jet[1],y)-py*Dot(jet[1],normal))*sign/w;var length=Math.Sqrt(dx*dx+dy*dy);if(!(length>0))continue;
            var epsilon=1e-5*Math.Pow(.2,attempt/6);var a=Add(normal,Add(Scale(x,px-epsilon*dy/length),Scale(y,py+epsilon*dx/length)));
            var b=Add(normal,Add(Scale(x,px+epsilon*dy/length),Scale(y,py-epsilon*dx/length)));ProfileNormalize(ref a);ProfileNormalize(ref b);
            a=Add(sphere.Origin,Scale(a,sphere.Radius));b=Add(sphere.Origin,Scale(b,sphere.Radius));
            error=SphericalWindingPoint(sphere,normal,x,y,edges,loop,a,out var wa);if(error!=0)continue;
            error=SphericalWindingPoint(sphere,normal,x,y,edges,loop,b,out var wb);if(error!=0)continue;
            if(wa==1&&wb==0||wa==0&&wb==-1)
            {
                status=OperationIntersection.Parameters(sphere,a,out var u,out var v);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                left.param[0]=u;left.param[1]=v;positive=wa==1?(byte)1:(byte)0;return 0;
            }
        }
        return ParasolidConstants.PK_ERROR_eval_failure;
    }

    private static int SphericalHalfedgeAngle(SurfTag surface,in ModelingEdge edge,byte forward,bool end,
        ReadOnlySpan<KernelVector3> points,out double angle)
    {
        angle=0;var curve=Curves[GetCurveSlotByTag(edge.Curve)];var increasing=(forward!=0)==(LocalSenseSign(curve.Sense)>0);
        var t=end==increasing?edge.End:edge.Start;Span<KernelVector3> jet=stackalloc KernelVector3[2];
        var status=EvaluateCurveCore(in curve,t,1,false,jet,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(surface)],out var sphere);var vertex=end?(forward!=0?edge.EndVertex:edge.StartVertex):(forward!=0?edge.StartVertex:edge.EndVertex);
        var normal=Sub(vertex>=0?points[vertex]:jet[0],sphere.Origin);if(!ProfileNormalize(ref normal))return ParasolidConstants.PK_ERROR_at_singularity;
        var x=OperationIntersection.Reference(normal);var y=ProfileCross(normal,x);var tangent=Scale(jet[1],increasing?1:-1);
        angle=Math.Atan2(Dot(tangent,y),Dot(tangent,x));return 0;
    }
}
