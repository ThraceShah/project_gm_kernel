using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace KernelTests;

public class SurfaceTripleRootTests
{
    [Fact]
    public void ThreeTransverseSupportsHaveOneCertifiedInteriorRoot()
    {
        var a=Parabola(0);var b=Plane(true);var c=Plane(false);var roots=new SurfaceIntersectionNode[64];
        var status=SurfaceTripleRoots.Find(new(new BezierSurfacePatch(a,2,2,new(-1,1,-1,1))),new(new BezierSurfacePatch(b,1,1,new(-1,1,-1,1))),new(new BezierSurfacePatch(c,1,1,new(-1,1,-1,1))),roots,out var n);
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(1,n);Assert.InRange(Math.Abs(roots[0].Point.X)+Math.Abs(roots[0].Point.Y)+Math.Abs(roots[0].Point.Z),0,1e-8);
    }

    [Fact]
    public void BothDisconnectedRootsAreFoundWithoutBoundarySeeds()
    {
        var a=Parabola(-.25);var b=Plane(true);IntervalNumber[] c=[-1,-1,0,1,-1,1,0,1,1,-1,0,1,1,1,0,1];var roots=new SurfaceIntersectionNode[64];
        var status=SurfaceTripleRoots.Find(new(new BezierSurfacePatch(a,2,2,new(-1,1,-1,1))),new(new BezierSurfacePatch(b,1,1,new(-1,1,-1,1))),new(new BezierSurfacePatch(c,1,1,new(-1,1,-1,1))),roots,out var n);
        Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(2,n);Assert.Contains(roots[..n],r=>Math.Abs(r.Point.X-.5)<1e-8);Assert.Contains(roots[..n],r=>Math.Abs(r.Point.X+.5)<1e-8);
        for(var i=0;i<n;i++)Assert.InRange(Math.Abs(roots[i].Point.Y)+Math.Abs(roots[i].Point.Z),0,1e-8);
    }

    private static IntervalNumber[] Parabola(double offset)
    {
        var poles=new IntervalNumber[36];
        for(var i=0;i<3;i++)for(var j=0;j<3;j++){var at=4*(3*i+j);poles[at]=i-1;poles[at+1]=j-1;poles[at+2]=(i==1?-1:1)+offset;poles[at+3]=1;}
        return poles;
    }
    private static IntervalNumber[] Plane(bool yZero)
    {
        var poles=new IntervalNumber[16];
        for(var i=0;i<2;i++)for(var j=0;j<2;j++){var at=4*(2*i+j);poles[at]=yZero?2*i-1:0;poles[at+1]=yZero?0:2*i-1;poles[at+2]=2*j-1;poles[at+3]=1;}
        return poles;
    }
}
