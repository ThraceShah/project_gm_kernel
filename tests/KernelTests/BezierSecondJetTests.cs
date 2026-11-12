using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace KernelTests;

public class BezierSecondJetTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TotalSecondJetsMatchTheOriginalRectangularCalculationAcrossDegreesAndBoxes(bool rational)
    {
        var poles=new IntervalNumber[BezierSurfaceBounds.MaxScalars];Span<IntervalNumber> actual=stackalloc IntervalNumber[18],full=stackalloc IntervalNumber[27];
        foreach(var p in new[]{0,1,2,4,8,16})foreach(var q in new[]{0,1,2,4,8,16})
        {
            for(var u=0;u<=p;u++)for(var v=0;v<=q;v++)
            {
                var at=4*(u*(q+1)+v);var w=rational?1+.01*(u+v):1;
                poles[at]=(.03*u+.02*v)*w;poles[at+1]=(.04*v-.01*u)*w;poles[at+2]=(.002*u*u-.001*v*v+.003*u*v)*w;poles[at+3]=w;
            }
            var patch=new BezierSurfacePatch(poles.AsSpan(0,4*(p+1)*(q+1)),p,q,new(-1,2,.2,1.4));
            foreach(var box in new[]{new SurfaceParameterBox(-.2,.4,.4,.9),new SurfaceParameterBox(.2,.2,.7,.7),new SurfaceParameterBox(-1,-1,.2,1.4),new SurfaceParameterBox(-1.001,-.8,1.3,1.401)})
            {
                var expected=BezierSurfaceBounds.BoundRectangularSecond(patch,box,full);Assert.Equal(expected,BezierSurfaceBounds.BoundSecond(patch,box,actual));if(expected!=AlgorithmStatus.Success)continue;
                for(var k=0;k<6;k++)for(var axis=0;axis<3;axis++)
                {
                    var from=(k==0?0:k==1?9:k==2?3:k==3?18:k==4?12:6)+axis;
                    Assert.Equal(full[from].Lower,actual[3*k+axis].Lower);Assert.Equal(full[from].Upper,actual[3*k+axis].Upper);
                }
            }
        }
        var warmPatch=new BezierSurfacePatch(poles.AsSpan(0,4*17*17),16,16,new(-1,2,.2,1.4));var warmBox=new SurfaceParameterBox(.2,.21,.7,.71);
        Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.BoundSecond(warmPatch,warmBox,actual));var before=GC.GetAllocatedBytesForCurrentThread();
        var status=BezierSurfaceBounds.BoundSecond(warmPatch,warmBox,actual);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Fact]
    public void MissingOutputAndNonpositiveWeightsRemainFailures()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16],output=stackalloc IntervalNumber[18];poles.Clear();
        var patch=new BezierSurfacePatch(poles,1,1,new(0,1,0,1));Assert.Equal(AlgorithmStatus.WorkspaceTooSmall,BezierSurfaceBounds.BoundSecond(patch,new(0,1,0,1),output[..17]));
        Assert.Equal(AlgorithmStatus.NumericalFailure,BezierSurfaceBounds.BoundSecond(patch,new(0,1,0,1),output));
    }
}
