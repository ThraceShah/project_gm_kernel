using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace KernelTests;

public class ConicBlockLinearSystemTests
{
    [Theory]
    [InlineData(-3,0)] [InlineData(-3,1e-10)] [InlineData(.75,1e-10)]
    public void ExactBlockInverseRemainderAndIntervalSolveKeepAllSevenUnknowns(double footDerivative,double uncertainty)
    {
        Span<IntervalNumber> matrix=stackalloc IntervalNumber[49],rhs=stackalloc IntervalNumber[7],solution=stackalloc IntervalNumber[7],remainder=stackalloc IntervalNumber[49],reference=stackalloc IntervalNumber[49];Span<double> inverse=stackalloc double[49];
        Fill(matrix,footDerivative,uncertainty);double[] expected=[.25,-.5,.125,.75,-1,.25,.5];
        for(var row=0;row<7;row++){IntervalNumber value=0;for(var k=0;k<7;k++)value+=matrix[7*row+k]*expected[k];rhs[row]=value;}
        var status=JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inverse,remainder,reference,solution,out var nearest,out var referenceNearest);
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(referenceNearest,nearest);
        Assert.Equal(footDerivative<0?AlgorithmStatus.Success:AlgorithmStatus.NotConverged,nearest);
        for(var row=0;row<7;row++)
        {
            Assert.InRange(expected[row],solution[row].Lower,solution[row].Upper);
            for(var column=0;column<7;column++)
            {
                var i=7*row+column;Assert.Equal(BitConverter.DoubleToInt64Bits(reference[i].Lower),BitConverter.DoubleToInt64Bits(remainder[i].Lower));Assert.Equal(BitConverter.DoubleToInt64Bits(reference[i].Upper),BitConverter.DoubleToInt64Bits(remainder[i].Upper));
                var product=0.0;for(var k=0;k<7;k++)product+=inverse[7*row+k]*matrix[7*k+column].Midpoint;
                Assert.InRange(product-(row==column?1:0),-2e-14,2e-14);
                if(column is >=3 and <6)Assert.Equal(0,remainder[i].Magnitude);
            }
        }
        var before=GC.GetAllocatedBytesForCurrentThread();
        status=JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inverse,remainder,reference,solution,out _,out _);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Fact]
    public void FootIntervalContainingZeroCannotPassNearestEvenWithInvertiblePrincipalPointMatrix()
    {
        Span<IntervalNumber> matrix=stackalloc IntervalNumber[49],rhs=stackalloc IntervalNumber[7],solution=stackalloc IntervalNumber[7],remainder=stackalloc IntervalNumber[49],reference=stackalloc IntervalNumber[49];Span<double> inverse=stackalloc double[49];
        Fill(matrix,-3,0);matrix[48]=new(-1,1);rhs.Clear();
        JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inverse,remainder,reference,solution,out var nearest,out var referenceNearest);
        Assert.Equal(AlgorithmStatus.NotConverged,nearest);Assert.Equal(referenceNearest,nearest);
    }

    [Fact]
    public void SingularPrincipalBlockCannotPublishAPreconditionerSuccess()
    {
        Span<IntervalNumber> matrix=stackalloc IntervalNumber[49],rhs=stackalloc IntervalNumber[7],solution=stackalloc IntervalNumber[7],remainder=stackalloc IntervalNumber[49],reference=stackalloc IntervalNumber[49];Span<double> inverse=stackalloc double[49];
        Fill(matrix,-3,0);matrix[..7].Clear();rhs.Clear();
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.TestConicLinearSystem(matrix,rhs,inverse,remainder,reference,solution,out _,out _));
    }

    private static void Fill(Span<IntervalNumber> matrix,double footDerivative,double uncertainty)
    {
        matrix.Clear();double[] a=[4,1,0,.5,1,3,.5,0,0,.5,2,.25,.5,0,.25,footDerivative];
        for(var i=0;i<4;i++)for(var j=0;j<4;j++)
        {var value=a[4*i+j];matrix[7*(i==3?6:i)+(j==3?6:j)]=value==0?new(0):new(value-Math.Abs(value)*uncertainty,value+Math.Abs(value)*uncertainty);}
        for(var row=3;row<6;row++)matrix[7*row+row]=1;
        matrix[27]=new(-.5-uncertainty,-.5+uncertainty);matrix[34]=new(.25-uncertainty,.25+uncertainty);matrix[41]=new(-2-uncertainty,-2+uncertainty);
    }
}
