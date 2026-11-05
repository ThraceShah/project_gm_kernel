using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Projection;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int ClosestProceduralVector(GeomTag geometry,in KernelVector3 target,double tolerance,out KernelVector3 foot,out double u,out double v)
    {
        foot=default;u=v=0;var error=PrepareDistanceOperand(geometry,out var operand);if(error!=0)return error;
        Span<IntervalNumber> point=stackalloc IntervalNumber[4]{target.X,target.Y,target.Z,1};var other=new SurfaceIntersectionPatch(new BezierSurfacePatch(point,0,0,new(0,1,0,1)));
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];var best=new GeometryDistancePair{Upper=double.PositiveInfinity};
        for(var i=0;i<operand.Pieces;i++)
        {
            var status=DistancePatch(operand,operand.Extent,i,poles,out var patch,out var empty);if(status!=AlgorithmStatus.Success)return IntersectionError(status);if(empty)continue;
            status=BezierPairDistance.MinimumProcedural(patch,other,tolerance,ref best);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
        }
        if(!double.IsFinite(best.Upper))return ParasolidConstants.PK_ERROR_eval_failure;
        foot=best.First;u=best.Parameters.U;v=best.Parameters.V;return 0;
    }
}
