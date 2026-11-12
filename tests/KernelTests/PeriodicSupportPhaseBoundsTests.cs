using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class PeriodicSupportPhaseBoundsTests
{
    [Theory]
    [InlineData(false,0,1)] [InlineData(true,0,1)]
    [InlineData(false,.7,1)] [InlineData(true,.7,1)]
    [InlineData(false,1,0)] [InlineData(true,1,0)]
    public void ConstrainedConicPhaseEnclosesIndependentAnglesAndDerivativesInBothSupportOrders(bool reverse,double du,double dv)
    {
        // Mathematical programme fixture only; real-PK topology acceptance
        // is independent. The parent CHART is t=Y on the radius-1.2 cylinder.
        var basis=ConicConstraintSubstitutionTests.Program(false,reverse,false,boundCylinder:true);
        var surfaces=basis.Surfaces.ToArray();var spines=basis.Spines.ToArray();var charts=basis.Charts.ToArray();
        surfaces[0].Range0=surfaces[0].Range1=-.5;
        spines[0].StartType=spines[0].EndType=LimitType.Help;
        var program=new NestedBlendProgram(surfaces,spines,charts);var side=reverse?1:0;
        var v=Math.Atan2(.2,Math.Sqrt(.25-.2*.2))/(Math.PI*.5);
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[4];
        foreach(var reference in new[]{.4,.4+Math.Tau,.4+Math.PI})
        {
            Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpineSupportPhase(program,1,.01,.011,side,reference,.1,du,dv,bounds));
            foreach(var t in new[]{.01,.0105,.011})
            {
                if(dv==0)
                {
                    Contains(bounds[0],v-.1);Contains(bounds[1],0);Contains(bounds[2],1);Contains(bounds[3],0);continue;
                }
                var phase=Math.Asin(t/1.2)-reference-du/dv*(v-.1);var speed=1/Math.Sqrt(1.2*1.2-t*t);
                Contains(bounds[0],Math.Sin(phase));Contains(bounds[1],Math.Cos(phase)*speed);
                Contains(bounds[2],Math.Cos(phase));Contains(bounds[3],-Math.Sin(phase)*speed);
            }
        }
        var before=GC.GetAllocatedBytesForCurrentThread();
        var status=JointNestedBlendEvaluation.BoundSpineSupportPhase(program,1,.01,.011,side,.4,.1,du,dv,bounds);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
        Assert.Equal(AlgorithmStatus.Unsupported,JointNestedBlendEvaluation.BoundSpineSupportPhase(program,1,.01,.011,1-side,.4,.1,du,dv,bounds));
        Assert.Equal(AlgorithmStatus.InvalidInput,JointNestedBlendEvaluation.BoundSpineSupportPhase(program,1,.01,.011,side,double.NaN,.1,du,dv,bounds));
    }

    private static void Contains(IntervalNumber bound,double value)=>Assert.InRange(value,bound.Lower,bound.Upper);
}
