using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace KernelTests;

public class ParametricPatchSeparationTests
{
    [Fact]
    public void CurvedSupportsWithOverlappingGlobalBoxesHaveCertifiedClearance()
    {
        Span<IntervalNumber> a=stackalloc IntervalNumber[24],b=stackalloc IntervalNumber[24];Parabola(a,0);Parabola(b,-.1);
        var domain=new SurfaceParameterBox(0,1,0,1);var first=new SurfaceIntersectionPatch(new BezierSurfacePatch(a,2,1,domain));var second=new SurfaceIntersectionPatch(new BezierSurfacePatch(b,2,1,domain));
        var visited=0;Assert.Equal(AlgorithmStatus.Success,ParametricPatchSeparation.CertifyDisjoint(first,second,domain,domain,.001,ref visited,32768));
        Assert.True(visited>1,"The initial full-support boxes overlap and require both-patch refinement.");
    }

    [Fact]
    public void NearSurfaceSingletonNeedsSeparationRatherThanAnAccurateClosestPoint()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[24];Parabola(poles,0);
        Span<IntervalNumber> point=stackalloc IntervalNumber[4]{.5,.5,.2499,1};var domain=new SurfaceParameterBox(0,1,0,1);
        var first=new SurfaceIntersectionPatch(new BezierSurfacePatch(point,0,0,domain));var second=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,1,domain));
        var visited=0;Assert.Equal(AlgorithmStatus.Success,ParametricPatchSeparation.CertifyDisjoint(first,second,domain,domain,2e-8,ref visited,2048));
        Assert.True(visited>8);
    }

    [Theory]
    [InlineData(0,.001,256)] [InlineData(-.1,.2,256)] [InlineData(-.1,.001,0)]
    public void ContactExcessiveClearanceAndBudgetExhaustionNeverProveSeparation(double gap,double clearance,int budget)
    {
        Span<IntervalNumber> a=stackalloc IntervalNumber[24],b=stackalloc IntervalNumber[24];Parabola(a,0);Parabola(b,gap);
        var domain=new SurfaceParameterBox(0,1,0,1);var first=new SurfaceIntersectionPatch(new BezierSurfacePatch(a,2,1,domain));var second=new SurfaceIntersectionPatch(new BezierSurfacePatch(b,2,1,domain));
        var visited=0;Assert.Equal(AlgorithmStatus.NotConverged,ParametricPatchSeparation.CertifyDisjoint(first,second,domain,domain,clearance,ref visited,budget));
    }

    private static void Parabola(Span<IntervalNumber> poles,double dz)
    {
        // Exact S(u,v)=(u,v,u*u+dz), not samples of the surface.
        for(var u=0;u<3;u++)for(var v=0;v<2;v++){var at=4*(2*u+v);poles[at]=u*.5;poles[at+1]=v;poles[at+2]=(u==2?1:0)+dz;poles[at+3]=1;}
    }
}
