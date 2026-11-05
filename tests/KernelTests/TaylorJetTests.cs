using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class TaylorJetTests
{
    [Fact]
    public void ElementarySeriesPreserveCoefficientsThroughSeventhOrder()
    {
        var x=TaylorJet.Variable(.37,7);TaylorJet.SinCos(x,out var sin,out var cos);
        double factorial=1;
        for(var k=0;k<=7;k++)
        {
            if(k>0)factorial*=k;
            Near(Math.Sin(.37+k*Math.PI/2),sin[k]*factorial);Near(Math.Cos(.37+k*Math.PI/2),cos[k]*factorial);
        }
        var angle=TaylorJet.Atan2(sin,cos);Near(.37,angle[0]);Near(1,angle[1]);for(var k=2;k<=7;k++)Near(0,angle[k]);
        var y=TaylorJet.Sqrt((x+2)*(x+2));for(var k=0;k<=7;k++)Near((x+2)[k],y[k]);
        var ratio=(x*x+2*x+1)/(x+1);for(var k=0;k<=7;k++)Near((x+1)[k],ratio[k]);
    }

    [Fact]
    public void PolynomialCompositionAndDifferentiationRespectTaylorFactorials()
    {
        var polynomial=new TaylorJet(2,3);polynomial[1]=3;polynomial[2]=5;polynomial[3]=7;
        var delta=TaylorJet.Variable(0,4);delta[2]=.25;var value=polynomial.Compose(delta);
        Near(2,value[0]);Near(3,value[1]);Near(5.75,value[2]);Near(9.5,value[3]);Near(5.5625,value[4]);
        var derivative=value.Differentiate();for(var k=0;k<4;k++)Near((k+1)*value[k+1],derivative[k]);
    }

    [Theory]
    [InlineData(SurfaceClass.Sphere)] [InlineData(SurfaceClass.Cylinder)] [InlineData(SurfaceClass.Torus)]
    internal void RadialManufacturedPathsHaveExactDistanceAndNormalSeries(SurfaceClass kind)
    {
        const int order=5;var t=TaylorJet.Variable(.23,order);TaylorJet.SinCos(t,out var sin,out var cos);
        var radius=2+t*.2+t*t*.1;var zero=new TaylorJet(0,order);
        var axis=new KernelVector3{Z=1};var reference=new KernelVector3{X=1};
        var surface=new AnalyticSurface(kind,default,axis,reference,kind==SurfaceClass.Torus?3:1,kind==SurfaceClass.Torus?1:0);
        var point=kind==SurfaceClass.Torus?new TaylorVector(3+radius,zero,zero):new TaylorVector(radius*cos,radius*sin,zero);
        Assert.Equal(AlgorithmStatus.Success,AnalyticDistanceSeries.Evaluate(surface,point,out var distance,out var normal));
        var expected=radius-1;for(var k=0;k<=order;k++)
        {
            Near(expected[k],distance[k]);Near(kind==SurfaceClass.Torus?(k==0?1:0):cos[k],normal.X[k]);Near(kind==SurfaceClass.Torus?0:sin[k],normal.Y[k]);Near(0,normal.Z[k]);
        }
    }

    private static void Near(double expected,double actual)=>Assert.InRange(Math.Abs(expected-actual),0,2e-12);
}
