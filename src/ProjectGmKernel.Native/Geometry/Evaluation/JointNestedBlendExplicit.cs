using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // Explicit occurrences constrain P=E(t). Re-solving the two offset
    // equations loses rank when their gradients coincide, although E(t)
    // can remain regular. The fourth row is the exact nearest-station
    // equation; its Schur complement certifies a local distance minimum.
    private static AlgorithmStatus AssembleExplicit(in ParametricSpineCurve curve,in Occurrence node,double parameter,
        ReadOnlySpan<double> state,Span<double> residual,Span<double> matrix,bool nearest,in KernelVector3 target,BufferOffset parentOffset)
    {
        var status=SpineParameterMap.Series(curve,TaylorJet.Variable(parameter,2),out var exact,out var derivative);if(status!=AlgorithmStatus.Success)return status;
        var point=Point(state,node.Offset);var delta=Sub(point,exact.Value);var tangent=derivative.Value;var n=state.Length;
        residual[node.Offset]=delta.X;residual[node.Offset+1]=delta.Y;residual[node.Offset+2]=delta.Z;
        if(!matrix.IsEmpty)
        {
            for(var i=0;i<3;i++)matrix[(node.Offset+i)*n+node.Offset+i]=1;
            if(nearest)
            {matrix[node.Offset*n+node.Offset+3]=-tangent.X;matrix[(node.Offset+1)*n+node.Offset+3]=-tangent.Y;matrix[(node.Offset+2)*n+node.Offset+3]=-tangent.Z;}
        }
        if(nearest)
        {
            var q=Sub(target,point);residual[node.Offset+3]=Dot(q,tangent);
            AddGradient(matrix,n,node.Offset+3,node.Offset,Scale(tangent,-1));
            if(parentOffset>=0)AddGradient(matrix,n,node.Offset+3,parentOffset,tangent);
            if(!matrix.IsEmpty)matrix[(node.Offset+3)*n+node.Offset+3]=Dot(q,new(){X=derivative.X[1],Y=derivative.Y[1],Z=derivative.Z[1]});
        }
        return AlgorithmStatus.Success;
    }
}
