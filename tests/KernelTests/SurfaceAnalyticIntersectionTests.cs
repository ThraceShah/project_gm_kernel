using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SurfaceAnalyticIntersectionTests
{
    [Fact]
    public void ShallowRootRetainsAnIsolationBoxInsteadOfStoppingAtSmallResidual()
    {
        const double slope=1.0/(1<<18);
        var first=1-.37*slope;var last=1+.63*slope;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];poles.Clear();
        poles[0]=first;poles[3]=1;poles[4]=last;poles[7]=1;
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,0,new(0,1,0,1)));
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];
        Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[4];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,cylinder,0,1,roots,ref count,1e-10,true,isolation));
        Assert.Equal(1,count);Assert.True(isolation[0].Certified);
        var exact=(1-first)/(last-first);
        Assert.InRange(exact,isolation[0].Root.Lower,isolation[0].Root.Upper);
        Assert.InRange(isolation[0].Root.Upper-isolation[0].Root.Lower,0,1e-8);
        Assert.InRange(Math.Abs(roots[0].First-exact),0,1e-8);
    }

    [Fact]
    public void APositiveNearZeroIsoEdgeDoesNotInventAContactRoot()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];poles.Clear();
        poles[0]=1+1e-12;poles[3]=1;poles[4]=1+2e-12;poles[7]=1;
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,0,new(0,1,0,1)));
        var cylinder=new AnalyticSurface(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1);
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];
        Span<ScalarRootIsolation> isolation=stackalloc ScalarRootIsolation[4];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,cylinder,0,1,roots,ref count,1e-10,false,isolation));
        Assert.Equal(0,count);
    }

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
