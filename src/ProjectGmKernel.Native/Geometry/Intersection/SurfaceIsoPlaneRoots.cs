using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

// Intersect a bounded surface isoparametric curve with a plane as a scalar
// equation. Monotone interval derivatives certify root uniqueness, including
// roots at a natural domain endpoint; unresolved cells never mean "no hit".
internal static class SurfaceIsoPlaneRoots
{
    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch curve,in AnalyticSurface plane,double first,double last,
        Span<OperationIntersectionPoint> output,ref BufferCount count,double tolerance=1e-10,bool strictSeparation=false)
    {
        Span<double> lower=stackalloc double[128],upper=stackalloc double[128];Span<BufferCount> depths=stackalloc BufferCount[128];
        lower[0]=first;upper[0]=last;depths[0]=0;var pending=1;var visited=0;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],at=stackalloc IntervalNumber[9];
        while(pending>0)
        {
            ModelingPerformance.Cell(true);
            if(++visited>32768){ModelingPerformance.BudgetFailure();return AlgorithmStatus.NotConverged;}
            var index=--pending;var lo=lower[index];var hi=upper[index];var depth=depths[index];
            var status=curve.Bound(new(lo,hi,0,0),bounds);
            if(status==AlgorithmStatus.Success)
            {
                var distance=Distance(bounds,plane);
                var exclusion=strictSeparation?0:tolerance;
                if(distance.Lower>exclusion||distance.Upper< -exclusion)continue;
                var derivative=bounds[3]*plane.Axis.X+bounds[4]*plane.Axis.Y+bounds[5]*plane.Axis.Z;
                if(!derivative.ContainsZero)
                {
                    status=curve.Bound(new(lo,lo,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                    var left=Distance(at,plane);
                    status=curve.Bound(new(hi,hi,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                    var right=Distance(at,plane);var parameter=0.0;var found=false;
                    if(left.Magnitude<=tolerance){parameter=lo;found=true;}
                    else if(right.Magnitude<=tolerance){parameter=hi;found=true;}
                    else if(left.Upper<0&&right.Lower>0||left.Lower>0&&right.Upper<0)
                    {
                        var a=lo;var b=hi;var negativeLeft=left.Upper<0;
                        for(var iteration=0;iteration<48;iteration++)
                        {
                            parameter=a+(b-a)*.5;status=curve.Bound(new(parameter,parameter,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                            var value=Distance(at,plane);
                            if(value.Magnitude<Math.Min(2e-12,tolerance)){found=true;break;}
                            if(value.ContainsZero)return AlgorithmStatus.NotConverged;
                            if((value.Upper<0)==negativeLeft)a=parameter;else b=parameter;
                        }
                        if(!found)return AlgorithmStatus.NotConverged;
                    }
                    else if(left.Lower>0&&right.Lower>0||left.Upper<0&&right.Upper<0)continue;
                    if(found)
                    {
                        status=curve.Bound(new(parameter,parameter,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                        var point=new KernelVector3{X=at[0].Midpoint,Y=at[1].Midpoint,Z=at[2].Midpoint};
                        if(!OperationIntersection.AddPoint(output,ref count,point,parameter,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;
                        ModelingPerformance.Root();
                        continue;
                    }
                }
            }
            else if(status!=AlgorithmStatus.NotConverged)return status;
            if(depth>=48||pending+2>lower.Length)return AlgorithmStatus.NotConverged;
            var mid=lo+(hi-lo)*.5;if(mid==lo||mid==hi)return AlgorithmStatus.NotConverged;
            lower[pending]=lo;upper[pending]=mid;depths[pending++]=depth+1;
            lower[pending]=mid;upper[pending]=hi;depths[pending++]=depth+1;
        }
        return AlgorithmStatus.Success;
    }

    private static IntervalNumber Distance(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface plane)
        =>(bounds[0]-plane.Origin.X)*plane.Axis.X+(bounds[1]-plane.Origin.Y)*plane.Axis.Y+(bounds[2]-plane.Origin.Z)*plane.Axis.Z;
}
