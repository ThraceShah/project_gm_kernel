using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class BezierPlaneResidualTests
{
    [Theory]
    [InlineData(false,.125)] [InlineData(true,.125)]
    [InlineData(false,8)] [InlineData(true,8)]
    public void HomogeneousPlaneBoundRetainsSmallResidualUnderTranslation(bool rational,double scale)
    {
        foreach(var origin in new[]{3.0,1024.0})
        {
            Span<IntervalNumber> poles=stackalloc IntervalNumber[24];
            for(var i=0;i<3;i++)for(var j=0;j<2;j++)
            {
                var at=4*(2*i+j);var w=rational?i+1:1;
                poles[at]=(origin+scale*i*.5)*w;poles[at+1]=j*w;poles[at+2]=0;poles[at+3]=w;
            }
            var patch=new BezierSurfacePatch(poles,2,1,new(0,1,0,1));
            var plane=new AnalyticSurface(SurfaceClass.Plane,new(){X=origin+scale*.5},new(){X=1},new(){Y=1});
            Assert.True(BezierPlaneResidual.TryBound(patch,plane,new(.25,.5,0,1),out var box));
            Assert.True(box.ContainsZero);
            for(var k=0;k<=16;k++)
            {
                var u=k/16.0;
                Assert.True(BezierPlaneResidual.TryBound(patch,plane,new(u,u,.375,.375),out var bound));
                // These dyadic input poles are exact. Decimal independently
                // evaluates their known rational polynomial before division.
                var t=(decimal)u;var expected=(double)((decimal)scale*(rational?(t*t+t-.5m)/(1+2*t):t-.5m));
                Assert.InRange(expected,bound.Lower,bound.Upper);
                Assert.True(bound.Upper-bound.Lower<1e-12*scale);
                if(k is 0 or 16)Assert.False(bound.ContainsZero);
                if(k>=4&&k<=8)Assert.InRange(expected,box.Lower,box.Upper);
            }
        }
    }
}
