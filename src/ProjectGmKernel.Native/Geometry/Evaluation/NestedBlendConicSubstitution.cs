using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    private static bool ConicConstraintSubstitution(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,BufferCount n)
    {
        if(n!=7||nodes.Length!=2||nodes[0].Offset!=0||nodes[1].Offset!=3||nodes[1].Parent!=0)return false;
        foreach(var node in nodes)if(node.FirstUv>=0||node.SecondUv>=0)return false;
        var root=program.Spines[nodes[0].Spine];if(SpineParameterMap.Explicit(root))return false;
        var first=program.Surfaces[root.First];var second=program.Surfaces[root.Second];if(first.IsBlend==second.IsBlend)return false;
        var blend=first.IsBlend?first:second;var other=first.IsBlend?second:first;var child=first.IsBlend?nodes[0].FirstChild:nodes[0].SecondChild;
        return child==1&&blend.Spine==nodes[1].Spine&&!other.IsBSpline&&(other.Analytic.Kind is SurfaceClass.Plane or SurfaceClass.Cylinder)
            &&(program.Spines[nodes[1].Spine].Curve.Kind is CurveClass.Circle or CurveClass.Ellipse);
    }

    // Keep Q-C(q)=0 and all seven native unknowns. On that constraint,
    // replacing Q by the original explicit C(q) in the two parent/foot
    // equations preserves their exact root set and native parameter q.
    private static AlgorithmStatus ConicSubstitutionResidual(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalDual> variables,IntervalDual parameter,Span<IntervalDual> residual,ReadOnlySpan<BufferOffset> charts,
        out IntervalVector blendNormal,out IntervalVector otherNormal,out IntervalVector tangent)
    {
        blendNormal=otherNormal=tangent=default;
        var root=program.Spines[nodes[0].Spine];var row=program.Surfaces[root.First].IsBlend?0:1;
        var blend=program.Surfaces[row==0?root.First:root.Second];var other=row==0?root.Second:root.First;
        var status=SpineParameterMap.Interval(program,program.Spines[nodes[1].Spine],variables[6],out var curve,out tangent);
        if(status!=AlgorithmStatus.Success)return status;
        var point=IntervalPoint(variables,0);var delta=point-curve;var length=delta.Norm();var speed=tangent.Norm();
        if(!length.Finite||!speed.Finite||!(length.Value.Lower>0&&speed.Value.Lower>0))return AlgorithmStatus.NotConverged;
        blendNormal=delta/length*blend.NormalSense;if(!blendNormal.Finite)return AlgorithmStatus.NotConverged;
        // Positive radius makes the squared radial equation equivalent to
        // length-radius, with the same native point and foot parameter.
        var radius=Math.Abs(blend.Range0);if(!(radius>0)||!double.IsFinite(radius))return AlgorithmStatus.NotConverged;
        residual[row]=(IntervalDual.Square(delta.X)+IntervalDual.Square(delta.Y)+IntervalDual.Square(delta.Z)-IntervalDual.Square((IntervalDual)radius))*blend.NormalSense;
        blendNormal=delta*(2*blend.NormalSense);
        status=IntervalNormal(program,other,nodes,0,variables,out residual[1-row],out otherNormal);if(status!=AlgorithmStatus.Success)return status;
        var otherSource=program.Surfaces[other];
        if(otherSource.Analytic.Kind==SurfaceClass.Cylinder)
        {
            var cylinder=otherSource.Analytic;if(!(cylinder.Radius>0)||!double.IsFinite(cylinder.Radius))return AlgorithmStatus.NotConverged;
            var relative=point-new IntervalVector(cylinder.Origin);var axis=new IntervalVector(cylinder.Axis);var radial=relative-axis*IntervalVector.Dot(relative,axis);
            residual[1-row]=(IntervalDual.Square(radial.X)+IntervalDual.Square(radial.Y)+IntervalDual.Square(radial.Z)-IntervalDual.Square((IntervalDual)cylinder.Radius))*otherSource.NormalSense;
            otherNormal=radial*(2*otherSource.NormalSense);
            // Subtract the cylinder equation before interval evaluation so
            // the common P-squared terms cancel algebraically. The stored
            // axis is not assumed to have an exactly unit squared norm.
            var centre=curve-new IntervalVector(cylinder.Origin);var z=IntervalVector.Dot(relative,axis);
            var factor=2-IntervalVector.Dot(axis,axis);
            residual[row]=(-2*IntervalVector.Dot(relative,centre)+IntervalDual.Square(centre.X)+IntervalDual.Square(centre.Y)+IntervalDual.Square(centre.Z)
                +factor*IntervalDual.Square(z)+IntervalDual.Square((IntervalDual)cylinder.Radius)-IntervalDual.Square((IntervalDual)radius))*blend.NormalSense;
            blendNormal=(axis*(2*factor*z)-centre*2)*blend.NormalSense;
        }
        status=SpineParameterMap.Interval(root,Chart(program,nodes[0].Spine)[charts[0]],parameter,point,out residual[2]);if(status!=AlgorithmStatus.Success)return status;
        var explicitResidual=IntervalPoint(variables,3)-curve;residual[3]=explicitResidual.X;residual[4]=explicitResidual.Y;residual[5]=explicitResidual.Z;
        residual[6]=IntervalVector.Dot(delta,tangent);
        for(var i=0;i<7;i++)if(!residual[i].Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus ConicSubstitutionJacobian(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,Span<IntervalNumber> matrix,Span<IntervalDual> variables,
        Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> charts)
    {
        ModelingPerformance.Jacobian(7);for(var i=0;i<7;i++)variables[i]=new(boxes[i],i==6?1:0);
        ModelingPerformance.ResidualDirection();
        var status=ConicSubstitutionResidual(program,nodes,variables[..7],parameter,equations[..7],charts,out var normal,out var other,out var tangent);
        if(status!=AlgorithmStatus.Success)return status;
        matrix.Clear();var root=program.Spines[nodes[0].Spine];var row=program.Surfaces[root.First].IsBlend?0:1;
        var otherSource=program.Surfaces[row==0?root.Second:root.First];
        if(otherSource.Analytic.Kind==SurfaceClass.Cylinder)
        {
            // Differentiate the actual projection arithmetic. Do not assume
            // the stored floating-point axis is algebraically unit length.
            var axis=new IntervalVector(otherSource.Analytic.Axis);other-=axis*IntervalVector.Dot(other,axis);
        }
        var chord=Chart(program,nodes[0].Spine)[charts[0]].Chord;
        for(var column=0;column<3;column++)
        {
            matrix[row*7+column]=column==0?normal.X.Value:column==1?normal.Y.Value:normal.Z.Value;
            matrix[(1-row)*7+column]=column==0?other.X.Value:column==1?other.Y.Value:other.Z.Value;
            matrix[2*7+column]=column==0?chord.X:column==1?chord.Y:chord.Z;
            matrix[6*7+column]=column==0?tangent.X.Value:column==1?tangent.Y.Value:tangent.Z.Value;
            matrix[(3+column)*7+3+column]=1;
        }
        for(var i=0;i<7;i++)matrix[i*7+6]=equations[i].Derivative;
        foreach(var value in matrix)if(!value.Finite)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    // At Q=C(q), J_G=(I-A)J_F, where A has the old Q gradients
    // of the Blend and foot rows in the three explicit-residual columns.
    // A^2=0, so R_G=R_F*(I+A), followed by inverse positive radial
    // row scales and the inverse cylinder-row subtraction. This is only a
    // point preconditioner; actual interval inclusion and nearest decide.
    private static AlgorithmStatus ConicSubstitutionInverse(in NestedBlendProgram program,ReadOnlySpan<Occurrence> nodes,
        ReadOnlySpan<double> state,Span<double> inverse)
    {
        var root=program.Spines[nodes[0].Spine];var row=program.Surfaces[root.First].IsBlend?0:1;
        var blend=program.Surfaces[row==0?root.First:root.Second];var delta=Sub(Point(state,0),Point(state,3));var length=Math.Sqrt(Dot(delta,delta));
        if(!(length>0)||!double.IsFinite(length))return AlgorithmStatus.NotConverged;
        var normal=Scale(delta,blend.NormalSense/length);
        var status=SpineParameterMap.Series(program,program.Spines[nodes[1].Spine],new TaylorJet(state[6],1),out _,out var tangent);
        if(status!=AlgorithmStatus.Success)return status;var t=tangent.Value;
        for(var i=0;i<7;i++)
        {
            var a=inverse[i*7+row];var b=inverse[i*7+6];
            inverse[i*7+3]-=a*normal.X+b*t.X;inverse[i*7+4]-=a*normal.Y+b*t.Y;inverse[i*7+5]-=a*normal.Z+b*t.Z;
            inverse[i*7+row]/=2*Math.Abs(blend.Range0);
            var other=program.Surfaces[row==0?root.Second:root.First];
            if(other.Analytic.Kind==SurfaceClass.Cylinder)
            {
                inverse[i*7+1-row]/=2*other.Analytic.Radius;
                inverse[i*7+1-row]+=inverse[i*7+row]*blend.NormalSense/other.NormalSense;
            }
        }
        foreach(var value in inverse)if(!double.IsFinite(value))return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestConicSubstitutionMatrix(in NestedBlendProgram program,ReadOnlySpan<IntervalNumber> boxes,
        Span<IntervalNumber> matrix,Span<IntervalNumber> directional)
    {
        var root=program.Spines[1];var first=program.Surfaces[root.First].IsBlend;
        Span<Occurrence> nodes=stackalloc Occurrence[2];
        nodes[0]=new(){Spine=1,Parent=-1,Offset=0,FirstChild=first?1:-1,SecondChild=first?-1:1,FirstUv=-1,SecondUv=-1};
        nodes[1]=new(){Spine=0,Parent=0,Offset=3,FirstChild=-1,SecondChild=-1,FirstUv=-1,SecondUv=-1};
        if(!ConicConstraintSubstitution(program,nodes,7))return AlgorithmStatus.Unsupported;
        Span<BufferOffset> charts=stackalloc BufferOffset[2];charts.Clear();
        Span<IntervalDual> variables=stackalloc IntervalDual[7],equations=stackalloc IntervalDual[7];
        var status=ConicSubstitutionJacobian(program,nodes,boxes,.01,matrix,variables,equations,charts);if(status!=AlgorithmStatus.Success)return status;
        for(var column=0;column<7;column++)
        {
            for(var i=0;i<7;i++)variables[i]=new(boxes[i],column==i?1:0);
            status=ConicSubstitutionResidual(program,nodes,variables,.01,equations,charts,out _,out _,out _);if(status!=AlgorithmStatus.Success)return status;
            for(var row=0;row<7;row++)directional[row*7+column]=equations[row].Derivative;
        }
        return AlgorithmStatus.Success;
    }
#endif
}
