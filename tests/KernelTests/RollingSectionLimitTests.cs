using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class RollingSectionLimitTests
{
    [Theory]
    [InlineData(0,1)] [InlineData(-.01,1.01)] [InlineData(-.3,1.3)]
    public void IntervalLimitContainsTheManufacturedSectionAndBothParameterDerivatives(double lower,double upper)
    {
        var u=new IntervalDual(new(-.01,.01),1);IntervalDual.SinCos(u,out var sin,out var cos);
        var first=new IntervalVector(cos,sin,0);var last=new IntervalVector(new KernelVector3{X=1});
        Assert.Equal(AlgorithmStatus.Success,RollingSectionEvaluation.Bound(last,first,new IntervalNumber(lower,upper),out var alongU));
        u=new(new(-.01,.01),0);IntervalDual.SinCos(u,out sin,out cos);first=new(cos,sin,0);
        Assert.Equal(AlgorithmStatus.Success,RollingSectionEvaluation.Bound(last,first,new(new(lower,upper),1),out var alongV));
        for(var i=0;i<=4;i++)for(var j=0;j<=4;j++)
        {
            var t=-.01+.005*i;var v=lower+(upper-lower)*j/4.0;var c=Math.Cos(v*t);var s=Math.Sin(v*t);
            Assert.InRange(c,alongU.X.Value.Lower,alongU.X.Value.Upper);Assert.InRange(s,alongU.Y.Value.Lower,alongU.Y.Value.Upper);
            Assert.InRange(-v*s,alongU.X.Derivative.Lower,alongU.X.Derivative.Upper);Assert.InRange(v*c,alongU.Y.Derivative.Lower,alongU.Y.Derivative.Upper);
            Assert.InRange(-t*s,alongV.X.Derivative.Lower,alongV.X.Derivative.Upper);Assert.InRange(t*c,alongV.Y.Derivative.Lower,alongV.Y.Derivative.Upper);
        }
    }

    [Theory]
    [InlineData(0.0)] [InlineData(1e-12)] [InlineData(-1e-12)] [InlineData(1e-7)] [InlineData(.01)] [InlineData(-.01)] [InlineData(.3)]
    public void CollapsingRollingSectionRetainsItsAnalyticLimitAndMixedDerivatives(double parameter)
    {
        const double radius=.5;
        var node=new BlendSurfaceInstruction{Range0=-radius,Range1=-radius,NormalSense=1};
        var u=TaylorJet.Variable(parameter,2);TaylorJet.SinCos(u,out var s,out var c);
        var center=new TaylorVector(new(0,2),new(0,2),u);
        var first=new TaylorVector(c,s,new(0,2));var second=TaylorVector.Constant(new KernelVector3{X=1},2);
        SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> actual=stackalloc KernelVector3[9];
        foreach(var v in new[]{0,.25,.5,.75,1})
        {
            Assert.Equal(AlgorithmStatus.Success,NestedBlendSeriesEvaluation.FromFrame(node,center,first,second,v,layout,actual));
            // This manufacturing law is P(u,v)=(r*cos(u*v),r*sin(u*v),u),
            // so its complete rectangular D2 jet has a regular limit at u=0.
            TaylorJet.SinCos(u*v,out var sin,out var cos);
            var radial=new TaylorVector(cos,sin,new(0,2));var tangent=new TaylorVector(-sin,cos,new(0,2));
            var p=center+radial*radius;var dv=tangent*(u*radius);var dvv=radial*(u*u*(-radius));
            for(var i=0;i<3;i++)for(var j=0;j<3;j++)
            {
                var expected=j==0?p:j==1?dv:dvv;var factor=i==2?2:1;var a=actual[layout.GetIndex(i,j)];
                Assert.InRange(Math.Abs(a.X-factor*expected.X[i]),0,2e-13);Assert.InRange(Math.Abs(a.Y-factor*expected.Y[i]),0,2e-13);Assert.InRange(Math.Abs(a.Z-factor*expected.Z[i]),0,2e-13);
            }
        }
    }
}
