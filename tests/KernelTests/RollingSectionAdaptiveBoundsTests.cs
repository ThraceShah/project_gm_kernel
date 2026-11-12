using System.Reflection;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace KernelTests;

public class RollingSectionAdaptiveBoundsTests
{
    private delegate AlgorithmStatus Weight(IntervalDual q,IntervalDual v,out IntervalDual value);
    private static readonly Weight Evaluate=typeof(RollingSectionEvaluation).GetMethod("WeightRemainder",BindingFlags.NonPublic|BindingFlags.Static,null,[typeof(IntervalDual),typeof(IntervalDual),typeof(IntervalDual).MakeByRefType()],null)!.CreateDelegate<Weight>();

    [Theory]
    [InlineData(0,.5)] [InlineData(.00000095367431640625,.5)] [InlineData(.125,-.125)]
    [InlineData(.5,1.125)] [InlineData(.25,0)] [InlineData(.25,1)] [InlineData(.1,2)]
    public void ValueAndCoupledDerivativeEncloseIndependentDecimalCoefficientMoments(double q,double v)
    {
        Assert.Equal(AlgorithmStatus.Success,Evaluate(new(q,.25),new(v,.125),out var actual));
        var expected=Reference((decimal)q,(decimal)v,.25m,.125m);
        Assert.InRange((double)expected.Value,actual.Value.Lower,actual.Value.Upper);
        Assert.InRange((double)expected.Derivative,actual.Derivative.Lower,actual.Derivative.Upper);
        var before=GC.GetAllocatedBytesForCurrentThread();var status=Evaluate(new(q,.25),new(v,.125),out _);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Theory]
    [InlineData(1.5)] [InlineData(1.9)]
    public void SlowConvergenceBranchEnclosesTheIndependentHalfAngleClosedForm(double q)
    {
        Assert.Equal(AlgorithmStatus.Success,Evaluate(new(q,1),new(.5,0),out var actual));
        var denominator=4-2*q;var weight=1/Math.Sqrt(denominator);var expected=weight-.5;var derivative=weight/denominator;
        Assert.InRange(expected,actual.Value.Lower,actual.Value.Upper);
        Assert.InRange(derivative,actual.Derivative.Lower,actual.Derivative.Upper);
    }

    [Fact]
    public void WideParameterAndDerivativeBoxesKeepOutwardRemainders()
    {
        Assert.Equal(AlgorithmStatus.Success,Evaluate(new(new(0,.5),new(-.25,.5)),new(new(-.125,1.125),new(-.125,1)),out var actual));
        for(var i=0;i<=8;i++)for(var j=0;j<=8;j++)foreach(var dq in new[]{-.25m,.5m})foreach(var dv in new[]{-.125m,1m})
        {
            var expected=Reference(i/16m,-.125m+j*.15625m,dq,dv);
            Assert.InRange((double)expected.Value,actual.Value.Lower,actual.Value.Upper);
            Assert.InRange((double)expected.Derivative,actual.Derivative.Lower,actual.Derivative.Upper);
        }
    }

    [Fact]
    public void OriginalConvergenceDomainAndMaximumBudgetsStillRejectUnsupportedInput()
    {
        Assert.Equal(AlgorithmStatus.NotConverged,Evaluate(new(-.01,0),new(.5,0),out _));
        Assert.Equal(AlgorithmStatus.NotConverged,Evaluate(new(2,0),new(.5,0),out _));
        Assert.Equal(AlgorithmStatus.NotConverged,Evaluate(new(.1,0),new(65,0),out _));
        Assert.Equal(AlgorithmStatus.NotConverged,Evaluate(new(1.5,0),new(129,0),out _));
    }

    // Independent decimal coefficient and q-power moments, 96 terms. For the
    // tested |q|<=.5 the analytic remainder is below decimal precision; this
    // numerical check does not replace the production geometric-tail proof.
    private static (decimal Value,decimal Derivative) Reference(decimal q,decimal v,decimal dq,decimal dv)
    {
        var a=v;decimal av=1,value=0,qDerivative=0,vDerivative=0,power=1;
        for(var k=1;k<=96;k++)
        {
            var denominator=(decimal)k*(2*k+1);var factor=((decimal)k*k-v*v)/denominator;
            av=av*factor-a*(2*v/denominator);a*=factor;
            qDerivative+=k*a*power;power*=q;value+=a*power;vDerivative+=av*power;
        }
        return(value,qDerivative*dq+vDerivative*dv);
    }
}
