using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
namespace KernelTests;
public class SupportingSectionAngleTests
{
    [Theory]
    [InlineData(1,0)] [InlineData(1,.1)] [InlineData(0,1)] [InlineData(-.1,1)] [InlineData(-1,1)] [InlineData(1,-.1)]
    public void NativeHalfPlanesEncloseAtan2AndBothAnalyticDerivatives(double x,double y)
    {
        for(var direction=0;direction<2;direction++)
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.SupportingSectionAngle(new(x,direction==0?1:0),new(y,direction==1?1:0),out var angle));
            Assert.InRange(Math.Atan2(y,x),angle.Value.Lower,angle.Value.Upper);var derivative=direction==0?-y/(x*x+y*y):x/(x*x+y*y);Assert.InRange(derivative,angle.Derivative.Lower,angle.Derivative.Upper);
        }
    }
    [Fact]
    public void AWholePositiveLateralBoxCrossesTheRightAngleWithoutABranchCut()
    {
        Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.SupportingSectionAngle(new(new(-.1,.1),1),new(new(.9,1.1),.2),out var result));
        foreach(var x in new[]{-.1,0,.1})foreach(var y in new[]{.9,1.0,1.1})
        {
            Assert.InRange(Math.Atan2(y,x),result.Value.Lower,result.Value.Upper);var derivative=(x*.2-y)/(x*x+y*y);Assert.InRange(derivative,result.Derivative.Lower,result.Derivative.Upper);
        }
    }
    [Fact]
    public void UnresolvedOriginAndNegativeAxisBranchesRemainFailures()
    {
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.SupportingSectionAngle(new(new(-1,1),0),new(new(-1,1),0),out _));
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.SupportingSectionAngle(new(-1,0),new(new(-.1,.1),0),out _));
        Assert.Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.SupportingSectionAngle(new(0,0),new(-1,0),out _));
    }
}
