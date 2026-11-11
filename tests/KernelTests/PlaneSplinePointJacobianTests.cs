using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class PlaneSplinePointJacobianTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void NativeNormalizedRowsMatchEveryOriginalDirectionalColumn(bool rational,bool reversed)
    {
        var program=Program(rational,reversed);Span<IntervalNumber> reference=stackalloc IntervalNumber[25],actual=stackalloc IntervalNumber[25];
        foreach(var boundary in new[]{false,true})
        {
            IntervalNumber[] box=[new(.18,.24),new(.33,.38),new(.16,.21),boundary?new(-.03,.04):new(.2,.3),boundary?new(.97,1.03):new(.3,.4)];
            JointNestedBlendEvaluation.TestPlaneSplinePointJacobian(program,0,box,reference,actual,out var eligible,out var expected,out var result);
            Assert.True(eligible);Assert.Equal(AlgorithmStatus.Success,expected);Assert.Equal(expected,result);
            for(var i=0;i<25;i++){Assert.Equal(reference[i].Lower,actual[i].Lower);Assert.Equal(reference[i].Upper,actual[i].Upper);}
        }
    }

    [Fact]
    public void DegenerateSplineNormalsCannotPublishAValidMatrixAndNonplaneSystemsKeepTheirEvaluator()
    {
        var program=Program(false,false,degenerate:true);Span<IntervalNumber> reference=stackalloc IntervalNumber[25],actual=stackalloc IntervalNumber[25];
        IntervalNumber[] box=[new(.18,.24),new(.33,.38),new(.16,.21),new(.2,.3),new(.3,.4)];
        JointNestedBlendEvaluation.TestPlaneSplinePointJacobian(program,0,box,reference,actual,out _,out var expected,out var result);
        Assert.Equal(AlgorithmStatus.NotConverged,expected);Assert.Equal(expected,result);
        program=Program(false,false,nonplane:true);
        JointNestedBlendEvaluation.TestPlaneSplinePointJacobian(program,0,box,reference,actual,out var eligible,out _,out _);Assert.False(eligible);
    }

    [Fact]
    public void MatrixEvaluationAllocatesNoManagedMemory()
    {
        var program=Program(true,true);Span<IntervalNumber> reference=stackalloc IntervalNumber[25],actual=stackalloc IntervalNumber[25];
        IntervalNumber[] box=[new(.18,.24),new(.33,.38),new(.16,.21),new(.2,.3),new(.3,.4)];
        JointNestedBlendEvaluation.TestPlaneSplinePointJacobian(program,0,box,reference,actual,out _,out _,out _);
        var before=GC.GetAllocatedBytesForCurrentThread();
        JointNestedBlendEvaluation.TestPlaneSplinePointJacobian(program,0,box,reference,actual,out _,out _,out var result);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,result);Assert.Equal(0,allocated);
    }

    internal static NestedBlendProgram Program(bool rational,bool reversed,bool degenerate=false,bool nonplane=false)
    {
        var dimension=rational?4:3;var data=new double[9*dimension+12];
        for(var u=0;u<3;u++)for(var v=0;v<3;v++)
        {
            var weight=rational?1+.15*u+.2*v:1;var at=dimension*(3*u+v);
            data[at]=(degenerate?1:u*.5)*weight;data[at+1]=(degenerate?1:v*.5)*weight;
            data[at+2]=(degenerate?1:.3*u*u-.1*v*v+.07*u*v)*weight;if(rational)data[at+3]=weight;
        }
        double[] knots=[0,0,0,1,1,1];knots.CopyTo(data,9*dimension);knots.CopyTo(data,9*dimension+6);
        var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=-1,Offset=.17,UDegree=2,VDegree=2,UCount=3,VCount=3,Dimension=dimension,Rational=rational};
        var plane=new BlendSurfaceInstruction{Analytic=new(nonplane?SurfaceClass.Sphere:SurfaceClass.Plane,new(){Z=.1},new(){Z=1},new(){X=1},2),NormalSense=-1};
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=new(){X=.2,Y=.3,Z=.4},Chord=new(){X=.3,Y=-.7,Z=.2},Scale=2.7},new(){Parameter=1,Scale=1}];
        return new(reversed?new[]{spline,plane}:new[]{plane,spline},spines,charts,data,new double[2048]);
    }
}
