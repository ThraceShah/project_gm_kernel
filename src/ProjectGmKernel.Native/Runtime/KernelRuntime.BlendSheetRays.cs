using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static AlgorithmStatus IntersectParametricBoundaryRay(SurfTag surface,PK_UV_s query,double du,double dv,in ModelingEdge edge,
        Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var surfaceSlot=GetSurfaceSlotByTag(surface);var curveSlot=GetCurveSlotByTag(edge.Curve);var record=Curves[curveSlot];
        if(TryBlendIsoparam(curveSlot,out var isoSurface,out var isoV)&&SameSheetSupport(surface,Surfaces[isoSurface].Header.Tag))
        {
            if(dv==0)return Math.Abs(query.param[1]-isoV)<1e-12?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
            var along=(isoV-query.param[1])/dv;var parameter=query.param[0]+du*along;
            if(along< -1e-10||!OperationIntersection.InInterval(parameter,edge.Start,edge.End))return AlgorithmStatus.Success;
            Span<KernelVector3> point=stackalloc KernelVector3[1];var evaluated=EvaluateCurveCore(in record,parameter,0,false,point,out _);if(evaluated!=AlgorithmStatus.Success)return evaluated;
            return OperationIntersection.AddPoint(output,ref count,point[0],along,parameter,IntersectionVectorKind.Simple)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        }
        var status=RegularSurfaceExtent(surfaceSlot,out var extent);if(status!=AlgorithmStatus.Success)return status;
        SurfaceSlot other=-1;IntersectionCurveSpan span=default;var pieces=0;
        if(record.Class==CurveClass.ICurve)
        {
            var data=ICurveDataPool[record.DataIndex];
            var tag=SameSheetSupport(surface,data.Surface0Tag)?data.Surface1Tag:SameSheetSupport(surface,data.Surface1Tag)?data.Surface0Tag:0;
            other=GetSurfaceSlotByTag(tag);if(other<0)return AlgorithmStatus.Unsupported;pieces=RegularSurfacePatchCount(other);
        }
        else
        {
            var range=new PK_INTERVAL_s();range.value[0]=edge.Start;range.value[1]=edge.End;
            if(ResolveIntersectionCurve(edge.Curve,range,out span)!=0)return AlgorithmStatus.Unsupported;
            pieces=GeneralCurvePieceCount(span);
        }
        if(pieces<=0)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> ap=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],bp=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[64];
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        for(var i=0;i<RegularSurfacePatchCount(surfaceSlot);i++)
        {
            status=RegularSurfacePatch(surfaceSlot,i,extent,ap,out var source);if(status!=AlgorithmStatus.Success)return status;
            if(source.Domain.U1<=source.Domain.U0||source.Domain.V1<=source.Domain.V0)continue;
            var lo=0.0;var hi=double.PositiveInfinity;
            if(!ClipParameterRay(query.param[0],du,source.Domain.U0,source.Domain.U1,ref lo,ref hi)
                ||!ClipParameterRay(query.param[1],dv,source.Domain.V0,source.Domain.V1,ref lo,ref hi)||!(hi>lo)||!double.IsFinite(hi))continue;
            var ray=new SurfaceIntersectionPatch(source,query.param[0],query.param[1],du,dv,lo,hi);
            for(var j=0;j<pieces;j++)
            {
                var nr=0;
                if(other>=0)
                {
                    status=RegularSurfacePatch(other,j,extent,bp,out var support);if(status!=AlgorithmStatus.Success)return status;
                    var box=support.Domain;if(box.U1<=box.U0||box.V1<=box.V0)continue;
                    var cell=new ParameterCell{Lower=new(){U=box.U0,V=box.V0,S=ray.Domain.U0},Upper=new(){U=box.U1,V=box.V1,S=ray.Domain.U1}};
                    status=ParametricSurfaceIntersection.Isolate(support,ray,cell,3,nodes,out var n);if(status!=AlgorithmStatus.Success)return status;
                    for(var k=0;k<n;k++)
                    {
                        var vector=new PK_VECTOR_s();WriteVector(&vector,nodes[k].Point);double parameter=0;
                        var error=CurveParameteriseVectorCore(edge.Curve,vector,&parameter);
                        if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return AlgorithmStatus.NotConverged;
                        if(OperationIntersection.InInterval(parameter,edge.Start,edge.End)
                            &&!OperationIntersection.AddPoint(roots,ref nr,nodes[k].Point,nodes[k].S,parameter,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                    }
                }
                else
                {
                    status=GeneralCurvePiece(span,j,bp,out var degree,out var domain,out var lower,out var upper);if(status!=AlgorithmStatus.Success)return status;
                    if(!(upper>lower))continue;var boundary=new SurfaceIntersectionPatch(new BezierSurfacePatch(bp[..(4*(degree+1))],degree,0,domain));
                    status=BezierCurveIntersection.Find(ray,boundary,ray.Domain.U0,ray.Domain.U1,lower,upper,roots,ref nr);if(status!=AlgorithmStatus.Success)return status;
                    for(var k=0;k<nr;k++)
                    {
                        if(!GeneralCurveNativeParameter(span,roots[k].Point,roots[k].Second,out var parameter))return AlgorithmStatus.NumericalFailure;
                        roots[k].Second=parameter;
                    }
                }
                for(var k=0;k<nr;k++)if(!OperationIntersection.AddPoint(output,ref count,roots[k].Point,roots[k].First,roots[k].Second,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
            }
        }
        OperationIntersection.Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static bool ClipParameterRay(double origin,double direction,double lower,double upper,ref double start,ref double end)
    {
        if(direction==0)return origin>=lower&&origin<=upper;
        var a=(new IntervalNumber(lower)-origin)/direction;var b=(new IntervalNumber(upper)-origin)/direction;if(direction<0)(a,b)=(b,a);
        start=Math.Max(start,a.Lower);end=Math.Min(end,b.Upper);return end>=start;
    }
}
