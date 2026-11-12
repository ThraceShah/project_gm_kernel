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
        if(plane.IsBlend||plane.Analytic.Kind!=SurfaceClass.Plane||plane.NormalSense==0||spline.NormalSense==0
            ||(first.IsBSpline?node.FirstUv:node.SecondUv)!=3)return false;
        // G must be continuous on each proof box. Restrict this optimization
        // to nonperiodic C1 sheets; native evaluation retains other sheets.
        var source=program.Spline(spline);
        return !source.UPeriodic&&!source.VPeriodic&&CartesianSplineSmoothKnots(source.UKnots,source.UDegree,source.UVertexCount)
            &&CartesianSplineSmoothKnots(source.VKnots,source.VDegree,source.VVertexCount);
    }

    private static bool CartesianSplineSmoothKnots(ReadOnlySpan<double> knots,SplineDegree degree,BufferCount count)
    {
        for(var i=degree+1;i<count;)
        {
            var start=i;var knot=knots[i];while(i<count&&knots[i]==knot)i++;
            if(knot>knots[degree]&&knot<knots[count]&&i-start>=degree)return false;
        }
        return degree>0;
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
        var cross=IntervalVector.Cross(su,sv);var length=cross.Norm();var uLength=su.Norm();var vLength=sv.Norm();
        if(!length.Finite||!uLength.Finite||!vLength.Finite||!(length.Value.Lower>0&&uLength.Value.Lower>0&&vLength.Value.Lower>0))
            return AlgorithmStatus.NotConverged;
        var normal=cross/length;if(!normal.Finite)return AlgorithmStatus.NotConverged;
        var delta=point-foot-normal*spline.Offset;
        residual[row]=delta.X;residual[3]=delta.Y;residual[4]=delta.Z;
        residual[1-row]=IntervalVector.Dot(point-new IntervalVector(plane.Analytic.Origin),new IntervalVector(plane.Analytic.Axis))*plane.NormalSense;
        status=SpineParameterMap.Interval(spine,Chart(program,node.Spine)[charts[0]],parameter,point,out residual[2]);
        if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<5;i++)if(!residual[i].Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    // G is affine in XYZ. Its two native UV directions share the spline
    // differential's value work, with the original scalar interval expressions.
    private static AlgorithmStatus CartesianSplineJacobian(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalDual> variables,IntervalNumber parameter,Span<IntervalDual> equations,
        ReadOnlySpan<BufferOffset> charts,ReadOnlySpan<IntervalSplineContact> cache,Span<IntervalNumber> matrix)
    {
        var spine=program.Spines[nodes[0].Spine];var splineRow=program.Surfaces[spine.First].IsBSpline?0:1;
        var spline=program.Surfaces[splineRow==0?spine.First:spine.Second];
        var plane=program.Surfaces[splineRow==0?spine.Second:spine.First];var axis=plane.Analytic.Axis;
        Span<IntervalNumber> local=stackalloc IntervalNumber[18];scoped ReadOnlySpan<IntervalNumber> hull;
        if(!cache.IsEmpty&&SplineContactMatches(cache[3],spline,variables[3].Value,variables[4].Value))hull=cache[3].Values;
        else
        {
            var status=IntervalSplineBounds(program,spline,variables[3].Value,variables[4].Value,local);if(status!=AlgorithmStatus.Success)return status;hull=local;
        }
        var foot=SplineUvVector(hull,0,3,6);var su=SplineUvVector(hull,3,9,12);var sv=SplineUvVector(hull,6,12,15);
        var cross=SplineDifferentialVector.Cross(su,sv);var length=cross.Norm();var uLength=su.Norm();var vLength=sv.Norm();
        if(!length.Finite||!uLength.Finite||!vLength.Finite||!(length.Value.Lower>0&&uLength.Value.Lower>0&&vLength.Value.Lower>0))return AlgorithmStatus.NotConverged;
        var normal=cross/length;if(!normal.Finite)return AlgorithmStatus.NotConverged;
        var point=new SplineDifferentialVector(new(variables[0].Value,0,0),new(variables[1].Value,0,0),new(variables[2].Value,0,0));
        var delta=point-foot-normal*spline.Offset;if(!delta.Finite)return AlgorithmStatus.NotConverged;
        var spatial=IntervalPoint(variables,0);
        equations[1-splineRow]=IntervalVector.Dot(spatial-new IntervalVector(plane.Analytic.Origin),new IntervalVector(axis))*plane.NormalSense;
        var chart=Chart(program,nodes[0].Spine)[charts[0]];
        var found=SpineParameterMap.Interval(spine,chart,parameter,spatial,out equations[2]);if(found!=AlgorithmStatus.Success)return found;
        if(!equations[1-splineRow].Finite||!equations[2].Finite)return AlgorithmStatus.NotConverged;
        for(var column=0;column<3;column++)
        {
            matrix[splineRow*5+column]=column==0?1:0;matrix[3*5+column]=column==1?1:0;matrix[4*5+column]=column==2?1:0;
            matrix[(1-splineRow)*5+column]=new IntervalNumber(column==0?axis.X:column==1?axis.Y:axis.Z)*plane.NormalSense;
            matrix[2*5+column]=column==0?chart.Chord.X:column==1?chart.Chord.Y:chart.Chord.Z;
        }
        matrix[splineRow*5+3]=delta.X.U;matrix[splineRow*5+4]=delta.X.V;
        matrix[3*5+3]=delta.Y.U;matrix[3*5+4]=delta.Y.V;matrix[4*5+3]=delta.Z.U;matrix[4*5+4]=delta.Z.V;
        matrix[(1-splineRow)*5+3]=matrix[(1-splineRow)*5+4]=equations[1-splineRow].Derivative;
        matrix[2*5+3]=matrix[2*5+4]=equations[2].Derivative;
        equations[splineRow]=new(delta.X.Value,delta.X.V);equations[3]=new(delta.Y.Value,delta.Y.V);equations[4]=new(delta.Z.Value,delta.Z.V);
        ModelingPerformance.SplinePointColumns();return AlgorithmStatus.Success;
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
        if(!(uLength>0&&vLength>0)||!double.IsFinite(uLength)||!double.IsFinite(vLength))return AlgorithmStatus.NotConverged;
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
        Span<IntervalDual> original,Span<IntervalDual> rebound,Span<IntervalDual> cartesian,BufferOffset direction=-1)
    {
        var root=program.Spines[0];var first=program.Surfaces[root.First];var source=program.Surfaces[first.IsBSpline?root.First:root.Second];
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=0,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=first.IsBSpline?3:-1,SecondUv=first.IsBSpline?-1:3};
        Span<BufferOffset> charts=stackalloc BufferOffset[1];charts[0]=0;
        Span<IntervalDual> variables=stackalloc IntervalDual[5];
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[5];cache.Clear();
        for(var i=0;i<5;i++)variables[i]=new(boxes[i],i==direction?1:0);
        var parameter=new IntervalDual(.4,direction==5?1:0);
        var status=IntervalResidual(program,nodes,variables,parameter,original,charts,cache);if(status!=AlgorithmStatus.Success)return status;
        status=CartesianSplineResidual(program,nodes,variables,parameter,cartesian,charts,cache);if(status!=AlgorithmStatus.Success)return status;
        status=IntervalSpline(program,source,variables[3],variables[4],out _,out var su,out var sv);if(status!=AlgorithmStatus.Success)return status;
        var normal=IntervalVector.Cross(su,sv).Unit()*source.NormalSense;var row=first.IsBSpline?0:1;
        var delta=new IntervalVector(cartesian[row],cartesian[3],cartesian[4]);
        cartesian.CopyTo(rebound);
        rebound[row]=IntervalVector.Dot(delta,normal);rebound[3]=IntervalVector.Dot(delta,su.Unit());rebound[4]=IntervalVector.Dot(delta,sv.Unit());
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus TestCartesianSplineMatrix(in NestedBlendProgram program,ReadOnlySpan<IntervalNumber> boxes,
        Span<IntervalNumber> matrix,Span<double> inverse)
    {
        var root=program.Spines[0];var first=program.Surfaces[root.First];
        Span<Occurrence> nodes=stackalloc Occurrence[1];nodes[0]=new(){Spine=0,Offset=0,Parent=-1,FirstChild=-1,SecondChild=-1,FirstUv=first.IsBSpline?3:-1,SecondUv=first.IsBSpline?-1:3};
        Span<IntervalDual> variables=stackalloc IntervalDual[5],equations=stackalloc IntervalDual[5];
        Span<IntervalSplineContact> cache=stackalloc IntervalSplineContact[5];cache.Clear();Span<BufferOffset> charts=stackalloc BufferOffset[1];charts[0]=0;
        if(!CartesianSplineRows(program,nodes,5))return AlgorithmStatus.Unsupported;
        var status=IntervalJacobian(program,nodes,boxes,.4,matrix,variables,equations,charts,cache,true);if(status!=AlgorithmStatus.Success||inverse.IsEmpty)return status;
        Span<double> state=stackalloc double[5],jacobian=stackalloc double[25],rhs=stackalloc double[5],residual=stackalloc double[5];Span<int> pivots=stackalloc int[5];
        for(var i=0;i<5;i++)state[i]=boxes[i].Midpoint;
        status=Assemble(program,nodes,state,.4,residual,jacobian,false,default);if(status!=AlgorithmStatus.Success)return status;
        status=SmallLinearSolve.LuFactorize(jacobian,5,pivots);if(status!=AlgorithmStatus.Success)return status;
        for(var column=0;column<5;column++)
        {
            rhs.Clear();rhs[column]=1;status=SmallLinearSolve.LuSolveInPlace(jacobian,5,pivots,rhs);if(status!=AlgorithmStatus.Success)return status;
            for(var row=0;row<5;row++)inverse[row*5+column]=rhs[row];
        }
        return CartesianSplineInverse(program,nodes,state,inverse);
    }
#endif
}
