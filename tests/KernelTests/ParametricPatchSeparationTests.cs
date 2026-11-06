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
        Assert.True(visited>1);
    }

    [Theory]
    [InlineData(0,.001,256)] [InlineData(-.1,.2,256)] [InlineData(-.1,.001,0)]
    public void ContactExcessiveClearanceAndBudgetExhaustionNeverProveSeparation(double gap,double clearance,int budget)
    {
        Span<IntervalNumber> a=stackalloc IntervalNumber[24],b=stackalloc IntervalNumber[24];Parabola(a,0);Parabola(b,gap);
        var domain=new SurfaceParameterBox(0,1,0,1);var first=new SurfaceIntersectionPatch(new BezierSurfacePatch(a,2,1,domain));var second=new SurfaceIntersectionPatch(new BezierSurfacePatch(b,2,1,domain));
        var visited=0;Assert.Equal(AlgorithmStatus.NotConverged,ParametricPatchSeparation.CertifyDisjoint(first,second,domain,domain,clearance,ref visited,budget));
    }

    [Fact]
    public void ObliqueOverlappingBoxesSeparateInOneCertifiedProjectionCell()
    {
        Span<IntervalNumber> a=stackalloc IntervalNumber[16],b=stackalloc IntervalNumber[16];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {
            var at=4*(2*u+v);a[at]=u-v;a[at+1]=u+v;a[at+2]=v;a[at+3]=1;
            b[at]=a[at]-.1;b[at+1]=a[at+1]+.1;b[at+2]=a[at+2]-.2;b[at+3]=1;
        }
        var box=new SurfaceParameterBox(0,1,0,1);var first=new SurfaceIntersectionPatch(new BezierSurfacePatch(a,1,1,box));var second=new SurfaceIntersectionPatch(new BezierSurfacePatch(b,1,1,box));
        Span<IntervalNumber> x=stackalloc IntervalNumber[9],y=stackalloc IntervalNumber[9];Assert.Equal(AlgorithmStatus.Success,first.Bound(box,x));Assert.Equal(AlgorithmStatus.Success,second.Bound(box,y));
        for(var d=0;d<3;d++)Assert.True(x[d].Lower<=y[d].Upper&&y[d].Lower<=x[d].Upper);
        var visited=0;Assert.Equal(AlgorithmStatus.Success,ParametricPatchSeparation.CertifyDisjoint(first,second,box,box,.01,ref visited,1));Assert.Equal(1,visited);
    }

    [Fact]
    public void CertifiedParentEnvelopeSurvivesSubqueriesAndIsNotReusedAfterAnIsoTransform()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[24];Parabola(poles,0);var box=new SurfaceParameterBox(0,1,0,1);
        var source=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,1,box));
        Span<IntervalNumber> parent=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9],expected=stackalloc IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success,source.Bound(box,parent));Span<SurfaceBoundCacheEntry> envelope=stackalloc SurfaceBoundCacheEntry[1];envelope[0]=new(){Box=box,Status=AlgorithmStatus.Success};parent.CopyTo(envelope[0].Values);
        var pinned=new SurfaceIntersectionPatch(source,envelope,pinned:true);
        Assert.Equal(AlgorithmStatus.Success,pinned.Bound(new(.5,.5,.5,.5),actual));
        Assert.Equal(AlgorithmStatus.Success,pinned.Bound(box,actual));for(var d=0;d<9;d++){Assert.Equal(parent[d].Lower,actual[d].Lower);Assert.Equal(parent[d].Upper,actual[d].Upper);}
        var changed=new SurfaceIntersectionPatch(pinned,false,.25,0,1);var reference=new SurfaceIntersectionPatch(source,false,.25,0,1);
        Assert.Equal(AlgorithmStatus.Success,changed.Bound(changed.Domain,actual));Assert.Equal(AlgorithmStatus.Success,reference.Bound(reference.Domain,expected));
        for(var d=0;d<9;d++){Assert.Equal(expected[d].Lower,actual[d].Lower);Assert.Equal(expected[d].Upper,actual[d].Upper);}
        Assert.Equal(source.Bound(box,actual[..8]),pinned.Bound(box,actual[..8]));
    }

    private static void Parabola(Span<IntervalNumber> poles,double dz)
    {
        // Exact S(u,v)=(u,v,u*u+dz), not samples of the surface.
        for(var u=0;u<3;u++)for(var v=0;v<2;v++){var at=4*(2*u+v);poles[at]=u*.5;poles[at+1]=v;poles[at+2]=(u==2?1:0)+dz;poles[at+3]=1;}
    }
}
