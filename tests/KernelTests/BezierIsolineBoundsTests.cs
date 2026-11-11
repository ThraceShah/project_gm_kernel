global using HomogeneousWeightExponent=System.Int32;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class BezierIsolineBoundsTests
{
    [Theory]
    [InlineData(true,-.125,-500)] [InlineData(true,.375,0)] [InlineData(true,1.125,500)]
    [InlineData(false,-.125,-500)] [InlineData(false,.375,0)] [InlineData(false,1.125,500)]
    public void PreparedRationalIsolineEnclosesAnalyticPositionsAndNativeDerivatives(bool constantU,double fixedParameter,HomogeneousWeightExponent exponent)
    {
        // H=(u,v,u²+v²,1+u+2v), native domains [2,5] x [-3,1].
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36],curvePoles=stackalloc IntervalNumber[12],bounds=stackalloc IntervalNumber[9];
        Fill(poles,exponent);var surface=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,2,new(2,5,-3,1)));
        var fixedNative=constantU?2+3*fixedParameter:-3+4*fixedParameter;
        var lower=constantU?-3.5:1.625;var upper=constantU?1.5:5.375;
        var iso=new SurfaceIntersectionPatch(surface,constantU,fixedNative,lower,upper);
        Assert.True(iso.CanPrepareBezierIsoline);Assert.Equal(AlgorithmStatus.Success,iso.PrepareBezierIsoline(curvePoles,out var prepared));
        Assert.Equal(iso.Domain.U0,prepared.Domain.U0);Assert.Equal(iso.Domain.U1,prepared.Domain.U1);
        foreach(var range in new[]{(-.125,1.125),(.25,.75),(.5,.5)})
        {
            var lo=constantU?-3+4*range.Item1:2+3*range.Item1;var hi=constantU?-3+4*range.Item2:2+3*range.Item2;
            Assert.Equal(AlgorithmStatus.Success,prepared.Bound(new(lo,hi,0,0),bounds));
            for(var i=0;i<=8;i++)
            {
                var t=range.Item1+(range.Item2-range.Item1)*i/8;var u=constantU?fixedParameter:t;var v=constantU?t:fixedParameter;
                var w=1+u+2*v;var z=u*u+v*v;double[] h=[u,v,z];double[] dh=constantU?[0,1,2*v]:[1,0,2*u];var dw=constantU?2:1;var scale=constantU?4:3;
                for(var d=0;d<3;d++)
                {
                    Assert.InRange(h[d]/w,bounds[d].Lower,bounds[d].Upper);
                    Assert.InRange((dh[d]*w-h[d]*dw)/(w*w*scale),bounds[3+d].Lower,bounds[3+d].Upper);
                    Assert.Equal(0,bounds[6+d].Magnitude);
                }
            }
        }
    }

    [Theory]
    [InlineData(true,0,16)] [InlineData(false,16,0)] [InlineData(true,16,16)] [InlineData(false,16,16)]
    public void ConstantAndMaximumDegreeAxesKeepExactRationalLinearMap(bool constantU,SplineDegree p,SplineDegree q)
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],curvePoles=stackalloc IntervalNumber[68],bounds=stackalloc IntervalNumber[9];
        for(var i=0;i<=p;i++)for(var j=0;j<=q;j++)
        {var u=p==0?0:(double)i/p;var v=q==0?0:(double)j/q;var at=4*(i*(q+1)+j);poles[at]=u;poles[at+1]=v;poles[at+2]=u+v;poles[at+3]=1+u+2*v;}
        var source=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles[..(4*(p+1)*(q+1))],p,q,new(0,1,0,1)));
        var iso=new SurfaceIntersectionPatch(source,constantU,.25,0,1);Assert.Equal(AlgorithmStatus.Success,iso.PrepareBezierIsoline(curvePoles,out var prepared));
        Assert.Equal(AlgorithmStatus.Success,prepared.Bound(new(.625,.625,0,0),bounds));
        var x=p==0?0:constantU?.25:.625;var y=q==0?0:constantU?.625:.25;var w=1+x+2*y;
        double[] h=[x,y,x+y];double[] dh=constantU?[0,1,1]:[1,0,1];var dw=constantU?2:1;
        for(var d=0;d<3;d++){Assert.InRange(h[d]/w,bounds[d].Lower,bounds[d].Upper);Assert.InRange((dh[d]*w-h[d]*dw)/(w*w),bounds[3+d].Lower,bounds[3+d].Upper);}
    }

    [Fact]
    public void PreparedRootRetainsNativeFreeParameterAndCertifiedExistence()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36];Fill(poles,0);
        var source=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,2,new(2,5,-3,1)));
        var iso=new SurfaceIntersectionPatch(source,true,2.75,-3,1);
        var plane=new AnalyticSurface(SurfaceClass.Plane,new KernelVector3{Y=.2},new KernelVector3{Y=1},new KernelVector3{X=1});
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[4];Span<ScalarRootIsolation> certificates=stackalloc ScalarRootIsolation[4];var count=0;
        Assert.Equal(AlgorithmStatus.Success,SurfaceIsoPlaneRoots.Find(iso,plane,-3,1,roots,ref count,isolation:certificates));
        Assert.Equal(1,count);Assert.Equal(-4.0/3,roots[0].First,9);Assert.True(certificates[0].Certified);
        Assert.InRange(-4.0/3,certificates[0].Root.Lower,certificates[0].Root.Upper);
    }

    [Fact]
    public void OffsetSweepAndParameterLineKeepOriginalTwoDimensionalBounds()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36];Fill(poles,0);var patch=new BezierSurfacePatch(poles,2,2,new(2,5,-3,1));
        var source=new SurfaceIntersectionPatch(patch);Assert.False(source.CanPrepareBezierIsoline);
        var offset=new SurfaceIntersectionPatch(patch,.25);var offsetIso=new SurfaceIntersectionPatch(offset,true,2.75,-3,1);Assert.False(offsetIso.CanPrepareBezierIsoline);
        var iso=new SurfaceIntersectionPatch(source,true,2.75,-3,1);var sweep=new SurfaceIntersectionPatch(iso,0,.5);Assert.False(sweep.CanPrepareBezierIsoline);
        var line=new SurfaceIntersectionPatch(source,2.5,-2,.25,.5,0,1);var lineIso=new SurfaceIntersectionPatch(line,true,2.75,0,1);Assert.False(lineIso.CanPrepareBezierIsoline);
    }

    [Fact]
    public void PreparationUsesCallerWorkspaceWithoutManagedAllocation()
    {
        Span<IntervalNumber> poles=stackalloc IntervalNumber[36],preparedPoles=stackalloc IntervalNumber[12];Fill(poles,0);
        var source=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,2,2,new(2,5,-3,1)));var iso=new SurfaceIntersectionPatch(source,false,-1.5,2,5);
        iso.PrepareBezierIsoline(preparedPoles,out _);var before=GC.GetAllocatedBytesForCurrentThread();
        var status=AlgorithmStatus.Success;for(var i=0;i<100;i++)status=iso.PrepareBezierIsoline(preparedPoles,out _);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocated);
    }

    private static void Fill(Span<IntervalNumber> poles,HomogeneousWeightExponent exponent)
    {
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {var at=4*(3*i+j);poles[at]=Math.ScaleB(i*.5,exponent);poles[at+1]=Math.ScaleB(j*.5,exponent);poles[at+2]=Math.ScaleB((i==2?1:0)+(j==2?1:0),exponent);poles[at+3]=Math.ScaleB(1+i*.5+j,exponent);}
    }
}
