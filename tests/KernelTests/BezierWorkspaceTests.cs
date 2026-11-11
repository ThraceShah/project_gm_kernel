global using SplineDegree=System.Int32;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace KernelTests;

public class BezierWorkspaceTests
{
    [Theory]
    [InlineData(0,0)] [InlineData(0,1)] [InlineData(1,0)] [InlineData(1,1)]
    [InlineData(2,1)] [InlineData(1,2)] [InlineData(4,2)]
    [InlineData(16,0)] [InlineData(0,16)] [InlineData(16,16)]
    public void ActiveAndPaddedControlNetsKeepIdenticalOutwardRectangularJets(SplineDegree uDegree,SplineDegree vDegree)
    {
        var count=4*(uDegree+1)*(vDegree+1);var poles=new IntervalNumber[BezierSurfaceBounds.MaxScalars];
        Array.Fill(poles,new IntervalNumber(double.NegativeInfinity,double.PositiveInfinity));
        for(var i=0;i<=uDegree;i++)for(var j=0;j<=vDegree;j++)
        {
            var at=4*(i*(vDegree+1)+j);var weight=1+.125*i+.0625*j;
            poles[at]=Math.ScaleB(i,-4)*weight;poles[at+1]=Math.ScaleB(j,-4)*weight;poles[at+2]=Math.ScaleB(i*j,-8)*weight;poles[at+3]=weight;
        }
        var active=new BezierSurfacePatch(poles.AsSpan(0,count),uDegree,vDegree,new(-1,2,.2,1.4));
        var padded=new BezierSurfacePatch(poles,uDegree,vDegree,active.Domain);
        Span<IntervalNumber> expected=stackalloc IntervalNumber[27],actual=stackalloc IntervalNumber[27];
        foreach(var box in new[]{new SurfaceParameterBox(-.2,.4,.4,.9),new SurfaceParameterBox(.2,.2,.7,.7),new SurfaceParameterBox(-1.001,-.8,1.3,1.401)})
        {
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundRectangularSecond(active,box,expected));
            Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundRectangularSecond(padded,box,actual));
            for(var i=0;i<27;i++){Assert.Equal(expected[i].Lower,actual[i].Lower);Assert.Equal(expected[i].Upper,actual[i].Upper);}
        }
        var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
        var status=BezierSurfaceBounds.BoundRectangularSecond(padded,new(.2,.3,.6,.7),actual);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
        for(var i=count;i<poles.Length;i++){Assert.Equal(double.NegativeInfinity,poles[i].Lower);Assert.Equal(double.PositiveInfinity,poles[i].Upper);}
    }
}
