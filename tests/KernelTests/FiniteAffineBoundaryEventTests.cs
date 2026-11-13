using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class FiniteAffineBoundaryEventTests
{
    [Theory]
    [InlineData(1,1)]
    [InlineData(-1,.125)]
    [InlineData(1,8)]
    public void TheWholeDeclaredEventBoxMustMissTheNativeFiniteInterval(double orientation,double scale)
    {
        // Native U[-2,3], nonunit direction, translated and rotated away
        // from any coordinate axis. A single point inverse is insufficient
        // when its precision enclosure reaches the finite endpoint.
        var line=new ParametricSpineCurve{Kind=CurveClass.Line,Origin=Vector(7,-11,4),First=Vector(2*orientation*scale,3*scale,-scale)};
        var beyond=Add(line.Origin,Scale(line.First,4));
        var gap=new IntervalVector(new IntervalNumber(beyond.X)+new IntervalNumber(-.01*scale,.01*scale),
            new IntervalNumber(beyond.Y)+new IntervalNumber(-.01*scale,.01*scale),new IntervalNumber(beyond.Z)+new IntervalNumber(-.01*scale,.01*scale));
        Assert.True(KernelRuntime.FiniteAffineBoundaryEventOutside(line,gap,-2,3));
        var boundary=Add(line.Origin,Scale(line.First,3));
        var touching=new IntervalVector(new IntervalNumber(boundary.X)+new IntervalNumber(-scale,scale),
            new IntervalNumber(boundary.Y)+new IntervalNumber(-scale,scale),new IntervalNumber(boundary.Z)+new IntervalNumber(-scale,scale));
        Assert.False(KernelRuntime.FiniteAffineBoundaryEventOutside(line,touching,-2,3));
        Assert.False(KernelRuntime.FiniteAffineBoundaryEventOutside(line,new IntervalVector(line.Origin),-2,3));
    }
}
