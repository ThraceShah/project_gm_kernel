using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Projection;

namespace KernelTests;

public class BezierPairDistanceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InteriorSurfaceMinimumIsGlobalIncludingAContinuousFamily(bool curved)
    {
        var a=new IntervalNumber[36];var b=new IntervalNumber[36];double[] z=[.25,-.25,.25];
        for(var u=0;u<3;u++)for(var v=0;v<3;v++)
        {
            var i=4*(3*u+v);a[i]=b[i]=u*.5;a[i+1]=b[i+1]=v*.5;
            a[i+2]=curved?z[u]+z[v]-.0625:0;b[i+2]=-1;a[i+3]=b[i+3]=1;
        }
        var best=new GeometryDistancePair{Upper=double.PositiveInfinity};
        Assert.Equal(AlgorithmStatus.Success,BezierPairDistance.Minimum(new(a,2,2,new(0,1,0,1)),new(b,2,2,new(0,1,0,1)),1e-9,ref best));
        Assert.InRange(Math.Abs(best.Upper-(curved?.9375:1)),0,1e-9);
        if(curved){Assert.InRange(Math.Abs(best.Parameters.U-.5),0,1e-6);Assert.InRange(Math.Abs(best.Parameters.V-.5),0,1e-6);}
    }

    [Fact]
    public void DisjointSurfaceDomainsHaveAnEndpointMinimum()
    {
        IntervalNumber[] a=[0,0,0,1, 0,1,0,1, 1,0,0,1, 1,1,0,1];
        IntervalNumber[] b=[2,2,1,1, 2,3,1,1, 3,2,1,1, 3,3,1,1];
        var best=new GeometryDistancePair{Upper=double.PositiveInfinity};
        Assert.Equal(AlgorithmStatus.Success,BezierPairDistance.Minimum(new(a,1,1,new(0,1,0,1)),new(b,1,1,new(0,1,0,1)),1e-9,ref best));
        Assert.InRange(Math.Abs(best.Upper-Math.Sqrt(3)),0,1e-9);
        Assert.InRange(Math.Abs(best.Parameters.U-1),0,1e-8);Assert.InRange(Math.Abs(best.Parameters.S),0,1e-8);
    }

    [Fact]
    public void SpaceCurvesHaveAZeroInteriorMinimum()
    {
        IntervalNumber[] a=[0,0,0,1, 1,0,0,1];IntervalNumber[] b=[.37,-1,0,1, .37,1,0,1];
        var best=new GeometryDistancePair{Upper=double.PositiveInfinity};
        Assert.Equal(AlgorithmStatus.Success,BezierPairDistance.Minimum(new(a,1,0,new(0,1,0,1)),new(b,1,0,new(0,1,0,1)),1e-9,ref best));
        Assert.InRange(best.Upper,0,1e-9);Assert.InRange(Math.Abs(best.Parameters.U-.37),0,1e-8);Assert.InRange(Math.Abs(best.Parameters.S-.5),0,1e-8);
    }

    [Fact]
    public void PointAndCurveDistanceDoesNotMistakeTheMiddleStationaryPointForTheMinimum()
    {
        double[] heights=[.03515625,-.05859375,.07682291666666667,-.05859375,.03515625];
        var a=new IntervalNumber[20];for(var i=0;i<5;i++){a[4*i+2]=heights[i];a[4*i+3]=1;}
        IntervalNumber[] b=[1,0,0,1];var best=new GeometryDistancePair{Upper=double.PositiveInfinity};
        Assert.Equal(AlgorithmStatus.Success,BezierPairDistance.Minimum(new(a,4,0,new(0,1,0,1)),new(b,0,0,new(0,1,0,1)),1e-9,ref best));
        Assert.InRange(Math.Abs(best.Upper-1),0,1e-9);
        Assert.True(Math.Min(Math.Abs(best.Parameters.U-.25),Math.Abs(best.Parameters.U-.75))<.001);
    }
}
