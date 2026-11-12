using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace KernelTests;

public class BezierPositionBoundsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PositionHullAndRetainedRestrictionMatchTheFullJetCalculationBitwise(bool rational)
    {
        var poles=new IntervalNumber[BezierSurfaceBounds.MaxScalars];Span<IntervalNumber> reference=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[3],convenience=stackalloc IntervalNumber[3];
        var retained=new IntervalNumber[BezierSurfaceBounds.MaxScalars];var restricted=new IntervalNumber[BezierSurfaceBounds.MaxScalars];
        foreach(var p in new[]{0,1,2,4,8,16})foreach(var q in new[]{0,1,2,4,8,16})
        {
            var size=4*(p+1)*(q+1);
            for(var u=0;u<=p;u++)for(var v=0;v<=q;v++)
            {
                var at=4*(u*(q+1)+v);var w=rational?1+.01*(u+v):1;
                poles[at]=(.03*u+.02*v)*w;poles[at+1]=(.04*v-.01*u)*w;poles[at+2]=(.002*u*u-.001*v*v+.003*u*v)*w;poles[at+3]=w;
            }
            var patch=new BezierSurfacePatch(poles.AsSpan(0,size),p,q,new(-1,2,.2,1.4));
            foreach(var box in new[]{new SurfaceParameterBox(-.2,.4,.4,.9),new SurfaceParameterBox(.2,.2,.7,.7),new SurfaceParameterBox(-1,-1,.2,1.4),new SurfaceParameterBox(-1.001,-.8,1.3,1.401)})
            {
                var expected=BezierSurfaceBounds.Bound(patch,box,reference);Assert.Equal(expected,BezierSurfaceBounds.BoundPosition(patch,box,actual,retained));Assert.Equal(expected,BezierSurfaceBounds.BoundPosition(patch,box,convenience));
                if(expected!=AlgorithmStatus.Success)continue;
                for(var i=0;i<3;i++){Assert.Equal(reference[i].Lower,actual[i].Lower);Assert.Equal(reference[i].Upper,actual[i].Upper);Assert.Equal(reference[i].Lower,convenience[i].Lower);Assert.Equal(reference[i].Upper,convenience[i].Upper);}
                Assert.Equal(AlgorithmStatus.Success,BezierSurfaceBounds.Restrict(patch,box,restricted));
                for(var i=0;i<size;i++){Assert.Equal(restricted[i].Lower,retained[i].Lower);Assert.Equal(restricted[i].Upper,retained[i].Upper);}
            }
        }
        var warmPatch=new BezierSurfacePatch(poles,16,16,new(-1,2,.2,1.4));var warmBox=new SurfaceParameterBox(.2,.21,.7,.71);
        var before=GC.GetAllocatedBytesForCurrentThread();var status=BezierSurfaceBounds.BoundPosition(warmPatch,warmBox,actual,retained);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    [Fact]
    public void OutputWorkspaceAndNonpositiveWeightErrorsStayExplicit()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16],result=stackalloc IntervalNumber[3],workspace=stackalloc IntervalNumber[16];poles.Clear();var patch=new BezierSurfacePatch(poles,1,1,new(0,1,0,1));
        Assert.Equal(AlgorithmStatus.WorkspaceTooSmall,BezierSurfaceBounds.BoundPosition(patch,new(0,1,0,1),result[..2],workspace));
        Assert.Equal(AlgorithmStatus.WorkspaceTooSmall,BezierSurfaceBounds.BoundPosition(patch,new(0,1,0,1),result,workspace[..15]));
        Assert.Equal(AlgorithmStatus.NumericalFailure,BezierSurfaceBounds.BoundPosition(patch,new(0,1,0,1),result,workspace));
    }
}
