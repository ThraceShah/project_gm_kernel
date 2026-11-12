using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;
namespace KernelTests;
[Collection("KernelTests")]
public class ProjectionSeedTests
{
    [Fact]
    public void SubdivisionUsesFewerEstimatesButKeepsTheGlobalDistanceCertificate()
    {
        double[] poles=new double[27],z=[.25,-.25,.25],knots=[0,0,0,1,1,1];
        for(var u=0;u<3;u++)for(var v=0;v<3;v++){var at=3*(u*3+v);poles[at]=u*.5;poles[at+1]=v*.5;poles[at+2]=z[u]+z[v]-.0625;}
        var surface=new BSurfaceView(2,2,3,3,3,false,false,false,poles,knots,knots);var target=new KernelVector3{X=.5,Y=.5,Z=-1};
        try
        {
            BSplineProjection.AllSubdivisionSeeds=true;BSplineProjection.TestSeedSearches=0;var old=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Assert.Equal(AlgorithmStatus.Success,KernelRuntime.ClosestBSurface(surface,target,1e-9,ref old));var beforeCount=BSplineProjection.TestSeedSearches;
            BSplineProjection.AllSubdivisionSeeds=false;BSplineProjection.TestSeedSearches=0;var actual=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Assert.Equal(AlgorithmStatus.Success,KernelRuntime.ClosestBSurface(surface,target,1e-9,ref actual));
            Assert.True(BSplineProjection.TestSeedSearches<beforeCount);Assert.InRange(actual.DistanceUpper,.9375-1e-12,.9375+1e-9);Assert.InRange(Math.Abs(actual.U-.5),0,1e-7);Assert.InRange(Math.Abs(actual.V-.5),0,1e-7);
            var warm=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};KernelRuntime.ClosestBSurface(surface,target,1e-9,ref warm);var next=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};var start=GC.GetAllocatedBytesForCurrentThread();var status=KernelRuntime.ClosestBSurface(surface,target,1e-9,ref next);var allocation=GC.GetAllocatedBytesForCurrentThread()-start;Assert.Equal(AlgorithmStatus.Success,status);Assert.Equal(0,allocation);
        }
        finally{BSplineProjection.AllSubdivisionSeeds=false;}
    }
    [Theory] [InlineData(.13,.31,false)] [InlineData(.87,.73,true)] [InlineData(.5,.2,false)]
    public void MultipleValleysAndRationalOriginalParametersStillCloseTheWholeDomain(double u,double v,bool rational)
    {
        var dimension=rational?4:3;double[] poles=new double[5*2*dimension],z=[.25,-.75,19.0/12,-.75,.25],uk=[2,2,2,2,2,5,5,5,5,5],vk=[-3,-3,-1,-1];
        for(var i=0;i<5;i++)for(var j=0;j<2;j++){var w=rational?1+.1*i+.07*j:1;var at=dimension*(2*i+j);poles[at]=i*.25*w;poles[at+1]=j*w;poles[at+2]=z[i]*w;if(rational)poles[at+3]=w;}
        var surface=new BSurfaceView(4,1,5,2,dimension,rational,false,false,poles,uk,vk);SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> target=stackalloc KernelVector3[1];Span<double> scratch=stackalloc double[4096];var nativeU=2+3*u;var nativeV=-3+2*v;
        Assert.Equal(AlgorithmStatus.Success,BSurfaceEvaluation.Evaluate(surface,nativeU,nativeV,layout,scratch,target));
        var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Assert.Equal(AlgorithmStatus.Success,KernelRuntime.ClosestBSurface(surface,target[0],1e-9,ref best));Assert.InRange(best.DistanceUpper,0,1e-9);Assert.InRange(Math.Abs(best.U-nativeU),0,1e-7);Assert.InRange(Math.Abs(best.V-nativeV),0,1e-7);
    }
}
