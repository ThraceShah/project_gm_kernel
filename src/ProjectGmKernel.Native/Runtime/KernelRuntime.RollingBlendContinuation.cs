using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool RollingTraceBeyondPlane(IntervalNumber x,IntervalNumber y,IntervalNumber z,
        ReadOnlySpan<AnalyticSurface> planes,double radius)
        =>RollingTraceBeyondPlane(x,y,z,planes,radius,out _);

    private static bool RollingTraceBeyondPlane(IntervalNumber x,IntervalNumber y,IntervalNumber z,
        ReadOnlySpan<AnalyticSurface> planes,double radius,out BufferOffset planeIndex)
    {
        planeIndex=-1;for(var i=0;i<planes.Length;i++)
        {
            var plane=planes[i];
            var squared=IntervalNumber.Square(new(plane.Axis.X))+IntervalNumber.Square(new(plane.Axis.Y))+IntervalNumber.Square(new(plane.Axis.Z));
            var projectedRadius=radius*Math.BitIncrement(Math.Sqrt(squared.Upper));
            var distance=(x-plane.Origin.X)*plane.Axis.X+(y-plane.Origin.Y)*plane.Axis.Y+(z-plane.Origin.Z)*plane.Axis.Z;
            if(distance.Lower>projectedRadius+1e-8*Math.Max(1,radius)){planeIndex=i;return true;}
        }
        return false;
    }

    // The planes are the actual finite consumer's outward termination faces.
    // A rolling ball beyond one plane by its full projected radius cannot
    // contribute to that finite domain. Artificial spline bounds are not
    // termination evidence. Every retained chart must have a unique regular
    // implicit tube over its complete interval, including any linear extension.
    private static AlgorithmStatus CertifyFiniteRollingTrace(in NestedBlendProgram program,BlendSpineIndex spine,
        ReadOnlySpan<AnalyticSurface> planes,double radius,bool closed)
    {
        var definition=program.Spines[spine];var charts=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var budget=8192;
        Span<BufferOffset> endPlanes=stackalloc BufferOffset[2];
        if(!closed)for(var end=0;end<2;end++)
        {
            var t=end==0?charts[0].Parameter:charts[^1].Parameter;
            var status=JointNestedBlendEvaluation.BoundSpine(program,spine,new(t,t,0,1),bounds);if(status!=AlgorithmStatus.Success)return status;
            if(!RollingTraceBeyondPlane(bounds[0],bounds[1],bounds[2],planes,radius,out endPlanes[end]))return AlgorithmStatus.NotConverged;
        }
        Span<SurfaceParameterBox> pending=stackalloc SurfaceParameterBox[48];Span<BufferCount> depths=stackalloc BufferCount[48];
        for(var chart=0;chart<charts.Length-1;chart++)
        {
            pending[0]=new(charts[chart].Parameter,charts[chart+1].Parameter,0,1);depths[0]=0;var count=1;
            while(count>0)
            {
                if(--budget<0)return AlgorithmStatus.NotConverged;
                var box=pending[--count];var depth=depths[count];var status=JointNestedBlendEvaluation.BoundSpine(program,spine,box,bounds);
                if(status==AlgorithmStatus.Success)
                {
                    if(closed)continue;
                    var first=planes[endPlanes[0]].Axis;var last=planes[endPlanes[1]].Axis;
                    var firstSlope=bounds[3]*first.X+bounds[4]*first.Y+bounds[5]*first.Z;
                    var lastSlope=bounds[3]*last.X+bounds[4]*last.Y+bounds[5]*last.Z;
                    if(firstSlope.Upper<0&&lastSlope.Lower>0)continue;
                    status=AlgorithmStatus.NotConverged;
                }
                if(status!=AlgorithmStatus.NotConverged)return status;
                if(depth==32||count+2>pending.Length)return AlgorithmStatus.NotConverged;
                var mid=box.U0+(box.U1-box.U0)*.5;if(!(mid>box.U0&&mid<box.U1))return AlgorithmStatus.NotConverged;
                pending[count]=new(box.U0,mid,0,1);depths[count++]=depth+1;pending[count]=new(mid,box.U1,0,1);depths[count++]=depth+1;
            }
        }
        return AlgorithmStatus.Success;
    }
}
