using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SurfaceAnalyticIntersectionTests
{
    [Theory]
    [InlineData((int)SurfaceClass.Cylinder)]
    [InlineData((int)SurfaceClass.Sphere)]
    public void CompleteImplicitCircleHasOneClosedComponent(int representation)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];PlanePatch(poles,0);
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(-2,2,-2,2)));
        var support=new AnalyticSurface((SurfaceClass)representation,default,new(){Z=1},new(){X=1},1);
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[1024];Span<SurfaceIntersectionSegment> segments=stackalloc SurfaceIntersectionSegment[1024];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,SurfacePlaneIntersection.Find(patch,support,nodes,ref nn,segments,ref ne));Assert.True(nn>4);Assert.Equal(nn,ne);
        Span<int> degree=stackalloc int[nn];degree.Clear();
        foreach(var segment in segments[..ne]){degree[segment.Start]++;degree[segment.End]++;}
        foreach(var count in degree)Assert.Equal(2,count);
        foreach(var node in nodes[..nn])Assert.InRange(Math.Abs(node.Point.X*node.Point.X+node.Point.Y*node.Point.Y-1),0,2e-9);
    }

    [Fact]
    public void TwoCylinderGeneratorsAreBothRetained()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];PlanePatch(poles,0);
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(-2,2,-2,2)));
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,default,new(){Y=1},new(){X=1},1);
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[1024];Span<SurfaceIntersectionSegment> segments=stackalloc SurfaceIntersectionSegment[1024];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.Success,SurfacePlaneIntersection.Find(patch,cylinder,nodes,ref nn,segments,ref ne));
        var positive=false;var negative=false;
        foreach(var node in nodes[..nn]){Assert.InRange(Math.Abs(Math.Abs(node.Point.X)-1),0,2e-9);positive|=node.Point.X>0;negative|=node.Point.X<0;}
        Assert.True(positive&&negative);
        foreach(var segment in segments[..ne])Assert.Equal(Math.Sign(nodes[segment.Start].Point.X),Math.Sign(nodes[segment.End].Point.X));
    }

    [Fact]
    public void UnresolvedIsolatedSphereTangencyCannotBecomeAnEmptySuccess()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[16];PlanePatch(poles,1);
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,1,new(-2,2,-2,2)));
        var sphere=new AnalyticSurface(SurfaceClass.Sphere,default,new(){Z=1},new(){X=1},1);
        Span<SurfaceIntersectionNode> nodes=stackalloc SurfaceIntersectionNode[1024];Span<SurfaceIntersectionSegment> segments=stackalloc SurfaceIntersectionSegment[1024];var nn=0;var ne=0;
        Assert.Equal(AlgorithmStatus.NotConverged,SurfacePlaneIntersection.Find(patch,sphere,nodes,ref nn,segments,ref ne));
    }

    private static void PlanePatch(Span<IntervalNumber> poles,double height)
    {
        poles.Clear();
        for(var i=0;i<2;i++)for(var j=0;j<2;j++)
        {var at=4*(2*i+j);poles[at]=i==0?-2:2;poles[at+1]=j==0?-2:2;poles[at+2]=height;poles[at+3]=1;}
    }
}
