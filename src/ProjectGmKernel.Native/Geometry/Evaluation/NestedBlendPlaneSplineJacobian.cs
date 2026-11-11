using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // In the five-variable plane/spline system, XYZ enters every equation
    // affinely while UV is fixed. Reuse the native spline hull and covectors
    // for those three columns; UV retains the full directional evaluator.
    private static bool PlaneSplinePointColumns(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,ReadOnlySpan<BufferOffset> charts,
        ReadOnlySpan<IntervalSplineContact> cache,Span<IntervalNumber> matrix,out AlgorithmStatus status)
    {
        status=AlgorithmStatus.Unsupported;
        if(nodes.Length!=1||boxes.Length!=5)return false;
        var node=nodes[0];var spine=program.Spines[node.Spine];if(SpineParameterMap.Explicit(spine))return false;
        var first=program.Surfaces[spine.First];var second=program.Surfaces[spine.Second];
        if(first.IsBSpline==second.IsBSpline)return false;
        var spline=first.IsBSpline?first:second;var plane=first.IsBSpline?second:first;
        var uv=first.IsBSpline?node.FirstUv:node.SecondUv;
        if(plane.IsBlend||plane.Analytic.Kind!=SurfaceClass.Plane||uv!=3)return false;
        status=IntervalSpline(program,spline,new(boxes[3],0),new(boxes[4],0),out var foot,out var su,out var sv,cache,3);
        if(status!=AlgorithmStatus.Success)return true;
        var normal=IntervalVector.Cross(su,sv).Unit()*spline.NormalSense;var tangentU=su.Unit();var tangentV=sv.Unit();
        if(!normal.Finite||!tangentU.Finite||!tangentV.Finite){status=AlgorithmStatus.NotConverged;return true;}
        var axis=new IntervalVector(plane.Analytic.Axis);var origin=new IntervalVector(plane.Analytic.Origin);
        var chart=Chart(program,node.Spine)[charts[0]];var splineRow=first.IsBSpline?0:1;var planeRow=1-splineRow;
        for(var column=0;column<3;column++)
        {
            var point=new IntervalVector(new(boxes[0],column==0?1:0),new(boxes[1],column==1?1:0),new(boxes[2],column==2?1:0));
            var delta=point-foot;
            var a=IntervalVector.Dot(point-origin,axis)*plane.NormalSense;
            var b=IntervalVector.Dot(delta,normal)-spline.Offset*spline.NormalSense;
            status=SpineParameterMap.Interval(spine,chart,parameter,point,out var gauge);if(status!=AlgorithmStatus.Success)return true;
            var u=IntervalVector.Dot(delta,tangentU);var v=IntervalVector.Dot(delta,tangentV);
            if(!a.Finite||!b.Finite||!gauge.Finite||!u.Finite||!v.Finite){status=AlgorithmStatus.NotConverged;return true;}
            matrix[planeRow*5+column]=a.Derivative;matrix[splineRow*5+column]=b.Derivative;
            matrix[2*5+column]=gauge.Derivative;matrix[3*5+column]=u.Derivative;matrix[4*5+column]=v.Derivative;
        }
        ModelingPerformance.SplinePointColumns();status=AlgorithmStatus.Success;return true;
    }

#if KERNEL_TEST_HOOKS
    internal static void TestPlaneSplinePointJacobian(in NestedBlendProgram program,BlendSpineIndex spine,ReadOnlySpan<IntervalNumber> boxes,
        Span<IntervalNumber> reference,Span<IntervalNumber> actual,out bool eligible,out AlgorithmStatus referenceStatus,out AlgorithmStatus actualStatus)
    {
        eligible=false;referenceStatus=actualStatus=AlgorithmStatus.NotRun;
        var root=program.Spines[spine];
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=spine,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,
            FirstUv=program.Surfaces[root.First].IsBSpline?3:-1,SecondUv=program.Surfaces[root.Second].IsBSpline?3:-1};
        Span<BufferOffset> charts=stackalloc BufferOffset[1];charts[0]=0;
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[5];cache.Clear();
        var source=program.Surfaces[program.Surfaces[root.First].IsBSpline?root.First:root.Second];
        var status=CacheSplineContact(program,source,boxes[3],boxes[4],ref cache[3]);
        if(status!=AlgorithmStatus.Success){referenceStatus=actualStatus=status;return;}
        Span<IntervalDual> variables=stackalloc IntervalDual[5],equations=stackalloc IntervalDual[5];
        for(var i=0;i<5;i++)variables[i]=new(boxes[i],0);
        referenceStatus=AlgorithmStatus.Success;
        for(var column=0;column<5;column++)
        {
            variables[column]=new(boxes[column],1);status=IntervalResidual(program,nodes,variables,.4,equations,charts,cache);
            variables[column]=new(boxes[column],0);
            if(status!=AlgorithmStatus.Success){referenceStatus=status;break;}
            for(var row=0;row<5;row++)reference[row*5+column]=equations[row].Derivative;
        }
        eligible=PlaneSplinePointColumns(program,nodes,boxes,.4,charts,cache,actual,out actualStatus);
        if(!eligible||actualStatus!=AlgorithmStatus.Success)return;
        for(var column=3;column<5;column++)
        {
            variables[column]=new(boxes[column],1);status=IntervalResidual(program,nodes,variables,.4,equations,charts,cache);
            variables[column]=new(boxes[column],0);
            if(status!=AlgorithmStatus.Success){actualStatus=status;return;}
            for(var row=0;row<5;row++)actual[row*5+column]=equations[row].Derivative;
        }
    }
#endif
}
