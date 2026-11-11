using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    private static bool CartesianSplineRows(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,BufferCount variables)
    {
        if(nodes.Length!=1||variables!=5)return false;
        var node=nodes[0];var spine=program.Spines[node.Spine];if(SpineParameterMap.Explicit(spine))return false;
        var first=program.Surfaces[spine.First];var second=program.Surfaces[spine.Second];
        if(first.IsBSpline==second.IsBSpline)return false;
        var spline=first.IsBSpline?first:second;var plane=first.IsBSpline?second:first;
        return !plane.IsBlend&&plane.Analytic.Kind==SurfaceClass.Plane&&plane.NormalSense!=0&&spline.NormalSense!=0
            &&(first.IsBSpline?node.FirstUv:node.SecondUv)==3;
    }

    // The original three spline equations are M(UV)*(P-S-h*n), with rows
    // sense*n, unit(Su), unit(Sv). They have the same roots on a regular
    // spline frame. Retain all five unknowns and the native plane/CHART rows.
    private static AlgorithmStatus CartesianSplineResidual(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalDual> variables,IntervalDual parameter,Span<IntervalDual> residual,
        ReadOnlySpan<BufferOffset> charts,ReadOnlySpan<IntervalSplineContact> cache)
    {
        var node=nodes[0];var spine=program.Spines[node.Spine];var first=program.Surfaces[spine.First];
        var spline=program.Surfaces[first.IsBSpline?spine.First:spine.Second];var plane=program.Surfaces[first.IsBSpline?spine.Second:spine.First];
        var row=first.IsBSpline?0:1;var point=IntervalPoint(variables,0);
        var status=IntervalSpline(program,spline,variables[3],variables[4],out var foot,out var su,out var sv,cache,3);
        if(status!=AlgorithmStatus.Success)return status;
        var cross=IntervalVector.Cross(su,sv);var length=cross.Norm();
        if(!(length.Value.Lower>0))return AlgorithmStatus.NotConverged;
        var normal=cross/length;var delta=point-foot-normal*spline.Offset;
        residual[row]=delta.X;residual[3]=delta.Y;residual[4]=delta.Z;
        residual[1-row]=IntervalVector.Dot(point-new IntervalVector(plane.Analytic.Origin),new IntervalVector(plane.Analytic.Axis))*plane.NormalSense;
        status=SpineParameterMap.Interval(spine,Chart(program,node.Spine)[charts[0]],parameter,point,out residual[2]);
        if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<5;i++)if(!residual[i].Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    // R_G=R_F*M is an approximate inverse for the equivalent residual.
    // It only chooses the preconditioner; strict interval inclusion below
    // still proves existence and uniqueness with the actual new residual.
    private static AlgorithmStatus CartesianSplineInverse(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<double> state,Span<double> inverse)
    {
        var spine=program.Spines[nodes[0].Spine];var first=program.Surfaces[spine.First];var row=first.IsBSpline?0:1;
        var spline=program.Surfaces[first.IsBSpline?spine.First:spine.Second];
        var status=NestedSplineEvaluation.Contact(program,spline,state[3],state[4],out var contact);if(status!=AlgorithmStatus.Success)return status;
        var uLength=Math.Sqrt(Dot(contact.Du,contact.Du));var vLength=Math.Sqrt(Dot(contact.Dv,contact.Dv));
        if(!(uLength>0&&vLength>0))return AlgorithmStatus.NotConverged;
        var normal=Scale(contact.Normal,spline.NormalSense);var u=Scale(contact.Du,1/uLength);var v=Scale(contact.Dv,1/vLength);
        for(var i=0;i<5;i++)
        {
            var a=inverse[i*5+row];var b=inverse[i*5+3];var c=inverse[i*5+4];
            inverse[i*5+row]=a*normal.X+b*u.X+c*v.X;
            inverse[i*5+3]=a*normal.Y+b*u.Y+c*v.Y;
            inverse[i*5+4]=a*normal.Z+b*u.Z+c*v.Z;
        }
        foreach(var value in inverse)if(!double.IsFinite(value))return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestCartesianSplineBinding(in NestedBlendProgram program,ReadOnlySpan<IntervalNumber> boxes,
        Span<IntervalNumber> original,Span<IntervalNumber> rebound,Span<IntervalNumber> cartesian)
    {
        var root=program.Spines[0];var first=program.Surfaces[root.First];var source=program.Surfaces[first.IsBSpline?root.First:root.Second];
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=0,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=first.IsBSpline?3:-1,SecondUv=first.IsBSpline?-1:3};
        Span<BufferOffset> charts=stackalloc BufferOffset[1];charts[0]=0;
        Span<IntervalDual> variables=stackalloc IntervalDual[5],old=stackalloc IntervalDual[5],changed=stackalloc IntervalDual[5];
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[5];cache.Clear();
        for(var i=0;i<5;i++)variables[i]=new(boxes[i],0);
        var status=IntervalResidual(program,nodes,variables,.4,old,charts,cache);if(status!=AlgorithmStatus.Success)return status;
        status=CartesianSplineResidual(program,nodes,variables,.4,changed,charts,cache);if(status!=AlgorithmStatus.Success)return status;
        status=IntervalSpline(program,source,variables[3],variables[4],out _,out var su,out var sv);if(status!=AlgorithmStatus.Success)return status;
        var normal=IntervalVector.Cross(su,sv).Unit()*source.NormalSense;var row=first.IsBSpline?0:1;
        var delta=new IntervalVector(changed[row],changed[3],changed[4]);
        for(var i=0;i<5;i++){original[i]=old[i].Value;rebound[i]=changed[i].Value;cartesian[i]=changed[i].Value;}
        rebound[row]=IntervalVector.Dot(delta,normal).Value;rebound[3]=IntervalVector.Dot(delta,su.Unit()).Value;rebound[4]=IntervalVector.Dot(delta,sv.Unit()).Value;
        return AlgorithmStatus.Success;
    }
#endif
}
