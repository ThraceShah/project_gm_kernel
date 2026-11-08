using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class ConicTubeSeparationTests
{
    [Theory]
    [InlineData(1,0)]
    [InlineData(2,8)]
    public void WholeConicSeparationDistinguishesTheRepresentedToleranceSides(double scale,double offset)
    {
        var conic=new ParametricSpineCurve{Kind=CurveClass.Ellipse,Origin=new(){Y=offset},
            First=new(){Y=-2*scale},Second=new(){Z=scale}};
        var radius=.5*scale;var tolerance=1e-8*scale;
        var boundary=offset-(2*scale+radius+tolerance);
        var outside=new KernelVector3{Y=Math.BitDecrement(boundary)};
        var inside=new KernelVector3{Y=Math.BitIncrement(boundary)};
        Assert.True(ConicTubeSeparation.ProvesOutside(conic,outside,radius,tolerance,0));
        Assert.False(ConicTubeSeparation.ProvesOutside(conic,inside,radius,tolerance,0));
        Assert.False(ConicTubeSeparation.ProvesOutside(conic,inside,radius,tolerance,Math.PI));
    }
}
