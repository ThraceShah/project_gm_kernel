using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static partial class JointNestedBlendEvaluation
{
    // Permute the existing seven rows/unknowns as (P.xyz,q | Q.xyz).
    // The substituted Jacobian is [A,0; B,I], B[:,0..2]=0.
    // These are linear algebra workspaces, not a reduced geometric model.
    private static BufferOffset ConicPrincipalIndex(BufferOffset index)=>index==3?6:index;

    private static AlgorithmStatus ConicBlockInverse(ReadOnlySpan<IntervalNumber> matrix,Span<double> inverse)
    {
        Span<double> block=stackalloc double[16],rhs=stackalloc double[4];Span<BufferOffset> pivots=stackalloc BufferOffset[4];
        for(var i=0;i<4;i++)for(var j=0;j<4;j++)block[4*i+j]=matrix[7*ConicPrincipalIndex(i)+ConicPrincipalIndex(j)].Midpoint;
        var status=SmallLinearSolve.LuFactorize(block,4,pivots);if(status!=AlgorithmStatus.Success)return AlgorithmStatus.NotConverged;
        inverse.Clear();
        for(var column=0;column<4;column++)
        {
            rhs.Clear();rhs[column]=1;status=SmallLinearSolve.LuSolveInPlace(block,4,pivots,rhs);if(status!=AlgorithmStatus.Success)return status;
            var c=ConicPrincipalIndex(column);
            for(var row=0;row<4;row++)inverse[7*ConicPrincipalIndex(row)+c]=rhs[row];
            for(var row=3;row<6;row++)inverse[7*row+c]=-matrix[7*row+6].Midpoint*rhs[3];
        }
        for(var row=3;row<6;row++)inverse[7*row+row]=1;
        foreach(var value in inverse)if(!double.IsFinite(value))return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    // Evaluate exactly the same I-RJ nonzero terms in the original k order.
    // The three Q columns vanish identically for this block preconditioner.
    private static void ConicBlockPrecondition(ReadOnlySpan<double> inverse,ReadOnlySpan<IntervalNumber> matrix,Span<IntervalNumber> result)
    {
        result.Clear();
        for(var row=0;row<7;row++)for(var column=0;column<4;column++)
        {
            var c=ConicPrincipalIndex(column);IntervalNumber value=row==c?1:0;
            for(var k=0;k<3;k++)value-=inverse[7*row+k]*matrix[7*k+c];
            if(row is >=3 and <6)value-=matrix[7*row+c];
            value-=inverse[7*row+6]*matrix[42+c];result[7*row+c]=value;
        }
    }

    private static AlgorithmStatus ConicBlockSolve(ReadOnlySpan<IntervalNumber> matrix,Span<IntervalNumber> rhs)
    {
        Span<IntervalNumber> block=stackalloc IntervalNumber[16],principal=stackalloc IntervalNumber[4];
        for(var i=0;i<4;i++)
        {
            principal[i]=rhs[ConicPrincipalIndex(i)];
            for(var j=0;j<4;j++)block[4*i+j]=matrix[7*ConicPrincipalIndex(i)+ConicPrincipalIndex(j)];
        }
        var status=SolveIntervals(block,principal,4);if(status!=AlgorithmStatus.Success)return status;
        for(var row=3;row<6;row++){rhs[row]-=matrix[7*row+6]*principal[3];if(!rhs[row].Finite)return AlgorithmStatus.NotConverged;}
        for(var row=0;row<4;row++)rhs[ConicPrincipalIndex(row)]=principal[row];
        return AlgorithmStatus.Success;
    }

    // The original explicit-child nearest test solves [I,-C';0,phi_q]
    // against (0,0,0,1). Its last component is exactly 1/phi_q.
    private static AlgorithmStatus ConicBlockNearest(ReadOnlySpan<IntervalNumber> matrix)
        =>matrix[48].Finite&&matrix[48].Upper<0?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;

#if KERNEL_TEST_HOOKS
    internal static AlgorithmStatus TestConicLinearSystem(ReadOnlySpan<IntervalNumber> matrix,ReadOnlySpan<IntervalNumber> rhs,
        Span<double> inverse,Span<IntervalNumber> remainder,Span<IntervalNumber> referenceRemainder,Span<IntervalNumber> solution,
        out AlgorithmStatus nearest,out AlgorithmStatus referenceNearest)
    {
        nearest=ConicBlockNearest(matrix);
        Span<IntervalNumber> child=stackalloc IntervalNumber[16],childRhs=stackalloc IntervalNumber[4];childRhs.Clear();childRhs[3]=1;
        for(var i=0;i<4;i++)for(var j=0;j<4;j++)child[4*i+j]=matrix[7*(3+i)+3+j];
        referenceNearest=SolveIntervals(child,childRhs,4);
        if(referenceNearest==AlgorithmStatus.Success&&childRhs[3].Upper>=0)referenceNearest=AlgorithmStatus.NotConverged;
        var status=ConicBlockInverse(matrix,inverse);if(status!=AlgorithmStatus.Success)return status;
        ConicBlockPrecondition(inverse,matrix,remainder);Precondition(inverse,matrix,7,referenceRemainder);
        rhs.CopyTo(solution);return ConicBlockSolve(matrix,solution);
    }
#endif
}
