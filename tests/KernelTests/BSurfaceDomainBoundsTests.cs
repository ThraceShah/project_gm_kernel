using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace KernelTests;

public class BSurfaceDomainBoundsTests
{
    [Theory]
    [InlineData(0,1,0,1,2,2)]
    [InlineData(2,5,-3,-1,8,1)]
    public void ExplicitDomainUsesNativeTensorLinearExtensionRatherThanPolynomialExtrapolation(double u0,double u1,double v0,double v1,double u,double v)
    {
        var source=Product(u0,u1,v0,v1);
        Assert.Equal(AlgorithmStatus.Success,BSurfaceDomainBounds.Position(source,new(u,u,v,v),true,out var box));
        Contains(box,2,2,9);Assert.True(box.ZHi<16);
        Assert.Equal(AlgorithmStatus.InvalidInput,BSurfaceDomainBounds.Position(source,new(u,u,v,v),false,out _));
    }

    [Fact]
    public void DomainCrossingBothNaturalEdgesIncludesAllNativeRegions()
    {
        var source=Product(0,1,0,1);
        Assert.Equal(AlgorithmStatus.Success,BSurfaceDomainBounds.Position(source,new(-.25,1.25,-.5,1.5),true,out var box));
        Contains(box,-.25,-.5,0);Contains(box,1.25,1.5,3);
        Assert.True(box.XLo<=-.25&&box.YLo<=-.5&&box.XHi>=1.25&&box.YHi>=1.5&&box.ZLo<=0&&box.ZHi>=3);
    }

    [Fact]
    public void UnknownWeightsAndDomainsDoNotProduceAUsableBox()
    {
        double[] poles=[0,0,0,-1,0,-1,0,-1,-1,0,0,-1,-1,-1,0,-1];double[] knots=[0,0,1,1];
        var source=new BSurfaceView(1,1,2,2,4,true,false,false,poles,knots,knots);
        Assert.NotEqual(AlgorithmStatus.Success,BSurfaceDomainBounds.Position(source,new(0,1,0,1),false,out _));
        var product=Product(0,1,0,1);
        Assert.Equal(AlgorithmStatus.InvalidInput,BSurfaceDomainBounds.Position(product,new(double.NaN,1,0,1),true,out _));
    }

    [Fact]
    public void WarmDomainBoundsAllocateNoManagedMemory()
    {
        var source=Product(0,1,0,1);var domain=new SurfaceParameterBox(-.25,1.25,-.5,1.5);
        for(var i=0;i<4;i++)Assert.Equal(AlgorithmStatus.Success,BSurfaceDomainBounds.Position(source,domain,true,out _));
        var before=GC.GetAllocatedBytesForCurrentThread();var successful=true;
        for(var i=0;i<20;i++)successful&=BSurfaceDomainBounds.Position(source,domain,true,out _)==AlgorithmStatus.Success;
        var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Assert.True(successful);Assert.Equal(0,bytes);
    }

    private static BSurfaceView Product(double u0,double u1,double v0,double v1)
    {
        var poles=new double[27];for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {var at=3*(3*i+j);poles[at]=i*.5;poles[at+1]=j*.5;poles[at+2]=i==2&&j==2?1:0;}
        return new(2,2,3,3,3,false,false,false,poles,new double[]{u0,u0,u0,u1,u1,u1},new double[]{v0,v0,v0,v1,v1,v1});
    }

    private static void Contains(in IntervalBox3 box,double x,double y,double z)
    {Assert.InRange(x,box.XLo,box.XHi);Assert.InRange(y,box.YLo,box.YHi);Assert.InRange(z,box.ZLo,box.ZHi);}
}
