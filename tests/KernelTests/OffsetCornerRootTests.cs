using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class OffsetCornerRootTests
{
    [Theory]
    [InlineData(1,false)] [InlineData(2.5,false)] [InlineData(.3,true)]
    public void CompleteThreeOffsetRootIsCovariantAndNormalEnclosed(double scale,bool mirror)
    {
        Span<IntervalNumber> roof=stackalloc IntervalNumber[16],sideA=stackalloc IntervalNumber[16],sideB=stackalloc IntervalNumber[16];
        for(var u=0;u<2;u++)for(var v=0;v<2;v++)
        {
            Store(roof,4*(2*u+v),u,v,1+.2*u*v,scale,mirror);
            Store(sideA,4*(2*u+v),0,u,v,scale,mirror);
            Store(sideB,4*(2*u+v),v,0,u,scale,mirror);
        }
        var domain=new SurfaceParameterBox(0,1,0,1);var radius=.1*scale*(mirror?-1:1);
        var a=new SurfaceIntersectionPatch(new BezierSurfacePatch(roof,1,1,domain),-radius);
        var b=new SurfaceIntersectionPatch(new BezierSurfacePatch(sideA,1,1,domain),radius);
        var c=new SurfaceIntersectionPatch(new BezierSurfacePatch(sideB,1,1,domain),radius);
        Span<SurfaceIntersectionNode> roots=stackalloc SurfaceIntersectionNode[8];
        Assert.Equal(AlgorithmStatus.Success,SurfaceTripleRoots.Find(a,b,c,roots,out var count));Assert.Equal(1,count);
        var transformed=Transform(.1,.1,0,scale,mirror);
        var point=roots[0].Point;
        // Both planar signed distances are exact; roof tangency is certified
        // by the complete three-surface root, independently of seed guesses.
        Assert.True(Math.Abs(point.X-transformed.X)<1e-8*scale);Assert.True(Math.Abs(point.Y-transformed.Y)<1e-8*scale);
        Assert.True(double.IsFinite(point.Z));Assert.InRange(roots[0].U,0,1);Assert.InRange(roots[0].V,0,1);
        Span<IntervalNumber> enclosure=stackalloc IntervalNumber[9];Assert.Equal(AlgorithmStatus.Success,a.Bound(new(roots[0].U,roots[0].U,roots[0].V,roots[0].V),enclosure));
        Assert.InRange(point.X,enclosure[0].Lower-1e-9*scale,enclosure[0].Upper+1e-9*scale);
        Assert.InRange(point.Y,enclosure[1].Lower-1e-9*scale,enclosure[1].Upper+1e-9*scale);
        Assert.InRange(point.Z,enclosure[2].Lower-1e-9*scale,enclosure[2].Upper+1e-9*scale);
    }

    private static void Store(Span<IntervalNumber> poles,int at,double x,double y,double z,double scale,bool mirror)
    {
        var p=Transform(x,y,z,scale,mirror);poles[at]=p.X;poles[at+1]=p.Y;poles[at+2]=p.Z;poles[at+3]=1;
    }
    private static KernelVector3 Transform(double x,double y,double z,double scale,bool mirror)
    {
        if(mirror)x=-x;var c=1/Math.Sqrt(2);
        return new(){X=3+scale*(x-y)*c,Y=-2+scale*(x+y)*c,Z=7+scale*z};
    }
}
