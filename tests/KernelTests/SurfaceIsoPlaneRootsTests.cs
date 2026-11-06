using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SurfaceIsoPlaneRootsTests
{
    [Theory]
    [InlineData(.1875,-.3125,.1875,.25,.75)]
    [InlineData(0,.5,0,0,1)]
    public void IsolatesEverySimpleRootIncludingBothDomainEndpoints(double z0,double z1,double z2,double first,double last)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[12];poles.Clear();
        for(var i=0;i<3;i++){poles[4*i]=i*.5;poles[4*i+3]=1;}
        poles[2]=z0;poles[6]=z1;poles[10]=z2;
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,0,new(0,1,0,1)));
        var plane=new AnalyticSurface(SurfaceClass.Plane,default,new KernelVector3{Z=1},new KernelVector3{X=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(curve,plane,0,1,roots,ref count));
        Assert.Equal(2,count);OperationIntersection.Sort(roots[..count]);Assert.Equal(first,roots[0].First,8);Assert.Equal(last,roots[1].First,8);
    }

    [Fact]
    public void UncertainEndpointSignsCannotBeReportedAsNoIntersection()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[8];poles.Clear();
        poles[0]=0;poles[3]=poles[7]=1;poles[4]=1;
        poles[2]=new(-1e-8,3e-8);poles[6]=new(1-1e-8,1+3e-8);
        var curve=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,1,0,new(0,1,0,1)));
        var plane=new AnalyticSurface(SurfaceClass.Plane,default,new KernelVector3{Z=1},new KernelVector3{X=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[8];var count=0;
        Assert.Equal(AlgorithmStatus.NotConverged,SurfaceIsoPlaneRoots.Find(curve,plane,0,1,roots,ref count));
    }
}
