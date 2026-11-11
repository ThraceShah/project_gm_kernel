using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SplineCartesianRowTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void ReboundRowsEncloseTheOriginalNativeEquationsAwayFromARoot(bool rational,bool reversed)
    {
        var program=PlaneSplinePointJacobianTests.Program(rational,reversed);
        IntervalNumber[] box=[new(.18),new(.36),new(.19),new(.23),new(.43)];
        Span<IntervalNumber> original=stackalloc IntervalNumber[5],rebound=stackalloc IntervalNumber[5],cartesian=stackalloc IntervalNumber[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        for(var i=0;i<5;i++)
        {
            Assert.True((original[i]-rebound[i]).ContainsZero);Assert.True((original[i]-rebound[i]).Magnitude<1e-10);
        }
        Assert.True(original[reversed?0:1].Magnitude>.01);
    }

    [Theory]
    [InlineData(-.17,false)] [InlineData(.17,false)] [InlineData(.17,true)]
    public void OriginalOffsetAndChartMapRemainBoundAndWrongPositionOrUvFails(double offset,bool reversed)
    {
        var program=Planar(offset,reversed);var u=.4/2.7;
        IntervalNumber[] box=[new(u),new(.3),new(offset),new(u),new(.3)];
        Span<IntervalNumber> original=stackalloc IntervalNumber[5],rebound=stackalloc IntervalNumber[5],cartesian=stackalloc IntervalNumber[5];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        for(var i=0;i<5;i++){Assert.True(original[i].ContainsZero);Assert.True(cartesian[i].ContainsZero);}
        box[2]=new(offset+.02);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        Assert.False(original[reversed?0:1].ContainsZero);Assert.False(cartesian[4].ContainsZero);
        box[2]=new(offset);box[3]=new(u+.1);
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
        Assert.False(original[3].ContainsZero);Assert.False(cartesian[reversed?0:1].ContainsZero);
        Span<IntervalNumber> bound=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(program,0,new(.2,.6,0,0),bound));
        Assert.InRange(.2/2.7,bound[0].Lower,bound[0].Upper);Assert.InRange(.6/2.7,bound[0].Lower,bound[0].Upper);
        Assert.InRange(.3,bound[1].Lower,bound[1].Upper);Assert.InRange(offset,bound[2].Lower,bound[2].Upper);
        Assert.InRange(1/2.7,bound[3].Lower,bound[3].Upper);Assert.True(bound[4].ContainsZero);Assert.True(bound[5].ContainsZero);
    }

    [Fact]
    public void DegenerateSplineFramesRemainFailures()
    {
        var program=PlaneSplinePointJacobianTests.Program(false,false,degenerate:true);
        IntervalNumber[] box=[new(.18),new(.36),new(.19),new(.23),new(.43)];
        Span<IntervalNumber> original=stackalloc IntervalNumber[5],rebound=stackalloc IntervalNumber[5],cartesian=stackalloc IntervalNumber[5];
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.TestCartesianSplineBinding(program,box,original,rebound,cartesian));
    }

    private static NestedBlendProgram Planar(double offset,bool reversed)
    {
        var spline=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=-1,Offset=offset,UDegree=1,VDegree=1,UCount=2,VCount=2,Dimension=3};
        var plane=new BlendSurfaceInstruction{Analytic=new(SurfaceClass.Plane,new(){Y=.3},new(){Y=1},new(){X=1}),NormalSense=-1};
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=new(){Y=.3,Z=offset},Chord=new(){X=1},Scale=2.7,FirstV=.3,SecondV=.3},new(){Position=new(){X=1,Y=.3,Z=offset},Parameter=2.7,Scale=1,FirstU=1,FirstV=.3,SecondU=1,SecondV=.3}];
        double[] data=[0,0,0,0,1,0,1,0,0,1,1,0,0,0,1,1,0,0,1,1];
        return new(reversed?new[]{spline,plane}:new[]{plane,spline},spines,charts,data,new double[1024]);
    }
}
