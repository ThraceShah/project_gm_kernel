using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class RollingBallReachTests
{
    [Theory]
    [InlineData(1,false)] [InlineData(2.5,false)] [InlineData(.3,true)]
    public void EntireMovingNormalSphereHasOneGlobalFootAfterRigidAndScaleTransforms(double scale,bool mirror)
    {
        const double bend=.03,radius=.1;Span<double> poles=stackalloc double[27];Span<double> knots=stackalloc double[6]{0,0,0,1,1,1};
        for(var i=0;i<3;i++)for(var j=0;j<3;j++)
        {
            var p=Transform(i*.5,j*.5,bend*((i==2?1:0)+(j==2?1:0)),scale,mirror);var at=3*(3*i+j);poles[at]=p.X;poles[at+1]=p.Y;poles[at+2]=p.Z;
        }
        var surface=new BSurfaceView(2,2,3,3,3,false,false,false,poles,knots,knots);
        // For every s in this interval, q=(s,.5) is an exact stationary
        // foot of C=S(q)-r*unit(-2*bend*s,-bend,1), at distance r.
        IntervalNumber s=new(.25,.45);var length=IntervalDual.Sqrt(new(1+IntervalNumber.Square(2*bend*s)+bend*bend,0)).Value;
        var centres=Transform(s+radius*2*bend*s/length,.5+radius*bend/length,bend*(IntervalNumber.Square(s)+.25)-radius/length,scale,mirror);
        var visited=0;Assert.Equal(AlgorithmStatus.Success,RollingBallReach.CertifySpline(surface,centres,new(.25,.45,.5,.5),ref visited,32768,out var strength));
        Assert.True(strength>0);Assert.InRange(visited,1,32768);
    }

    [Fact]
    public void C1MultiSpanAndPositiveRationalSupportsHaveGlobalCertificates()
    {
        Span<double> poles=stackalloc double[24];Span<double> uk=stackalloc double[7]{0,0,0,.5,1,1,1};Span<double> vk=stackalloc double[4]{0,0,1,1};
        Span<double> greville=stackalloc double[4]{0,.25,.75,1};for(var i=0;i<4;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=greville[i];poles[at+1]=j;poles[at+2]=0;}
        var multi=new BSurfaceView(2,1,4,2,3,false,false,false,poles,uk,vk);var visited=0;
        Assert.Equal(AlgorithmStatus.Success,RollingBallReach.CertifySpline(multi,new(.2,.8,.5,.5,.1,.2),new(.2,.8,.5,.5),ref visited,32768,out var strength));Assert.True(strength>.4);Assert.True(visited>=2);
        Span<double> rational=stackalloc double[36];Span<double> knots=stackalloc double[6]{0,0,0,1,1,1};
        for(var i=0;i<3;i++)for(var j=0;j<3;j++){var w=1+.01*i+.015*j;var at=4*(3*i+j);rational[at]=w*i*.5;rational[at+1]=w*j*.5;rational[at+2]=w*(i==1&&j==1?.03:0);rational[at+3]=w;}
        var source=new BSurfaceView(2,2,3,3,4,true,false,false,rational,knots,knots);visited=0;
        Assert.Equal(AlgorithmStatus.Success,RollingBallReach.CertifySpline(source,new(.25,.35,.25,.35,-.2,-.1),new(.15,.55,.15,.55),ref visited,32768,out strength));Assert.True(strength>0);
    }

    [Fact]
    public void C0CreaseWithTwoGlobalFeetIsRejectedDespitePositiveHessianOnEachSpan()
    {
        Span<double> poles=stackalloc double[18];Span<double> uk=stackalloc double[5]{0,0,.5,1,1};Span<double> vk=stackalloc double[4]{0,0,1,1};
        for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i*.5;poles[at+1]=j;poles[at+2]=i==1?0:1;}
        // S=(u,v,|2u-1|), C=(.5,.5,1): two equal-distance normal
        // feet u=.1 and .9, although each affine span has Hessian diag(5,1).
        var firstDistance=(.1-.5)*(.1-.5)+(.8-1)*(.8-1);var secondDistance=(.9-.5)*(.9-.5)+(.8-1)*(.8-1);Assert.Equal(firstDistance,secondDistance,12);
        var surface=new BSurfaceView(1,1,3,2,3,false,false,false,poles,uk,vk);var visited=0;
        Assert.Equal(AlgorithmStatus.Unsupported,RollingBallReach.ValidateC1NaturalDomain(surface));
        Assert.Equal(AlgorithmStatus.Unsupported,RollingBallReach.CertifySpline(surface,new(.5,.5,.5,.5,1,1),new(.09,.11,.5,.5),ref visited,32768,out var strength));Assert.Equal(0,strength);Assert.Equal(0,visited);
    }

    [Fact]
    public void SmoothSupportWithTwoNearestFeetCannotPassTheGlobalReachProof()
    {
        Span<double> poles=stackalloc double[18];Span<double> uk=stackalloc double[6]{0,0,0,1,1,1};Span<double> vk=stackalloc double[4]{0,0,1,1};
        for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i*.5;poles[at+1]=j;poles[at+2]=i==1?-1:1;}
        var surface=new BSurfaceView(2,1,3,2,3,false,false,false,poles,uk,vk);var visited=0;
        // z=(2u-1)^2, C=(.5,.5,1) has two feet .5 +/- sqrt(7/32).
        Assert.Equal(AlgorithmStatus.Success,RollingBallReach.ValidateC1NaturalDomain(surface));
        Assert.Equal(AlgorithmStatus.NotConverged,RollingBallReach.CertifySpline(surface,new(.5,.5,.5,.5,1,1),new(.03,.04,.5,.5),ref visited,32768,out var strength));Assert.Equal(0,strength);Assert.True(visited>0);
    }

    [Fact]
    public void OutsideNaturalFootPeriodicDataAndExhaustedSharedBudgetAreExplicitFailures()
    {
        Span<double> poles=stackalloc double[12]{0,0,0,0,1,0,1,0,0,1,1,0};Span<double> knots=stackalloc double[4]{0,0,1,1};
        var surface=new BSurfaceView(1,1,2,2,3,false,false,false,poles,knots,knots);var periodic=new BSurfaceView(1,1,2,2,3,false,true,false,poles,knots,knots);var visited=0;
        Assert.Equal(AlgorithmStatus.Unsupported,RollingBallReach.ValidateC1NaturalDomain(periodic));
        Assert.Equal(AlgorithmStatus.Unsupported,RollingBallReach.CertifySpline(surface,new(-.1,-.05,.5,.5,.1,.1),new(-.1,-.05,.5,.5),ref visited,32768,out var strength));Assert.Equal(0,strength);
        Assert.Equal(AlgorithmStatus.NotConverged,RollingBallReach.CertifySpline(surface,new(.2,.3,.4,.5,.1,.2),new(.2,.3,.4,.5),ref visited,0,out strength));Assert.Equal(0,strength);Assert.Equal(0,visited);
        Assert.Equal(AlgorithmStatus.Success,RollingBallReach.CertifySpline(surface,new(.2,.3,.4,.5,.1,.2),new(.2,.3,.4,.5),ref visited,1,out strength));Assert.Equal(1,visited);
        Assert.Equal(AlgorithmStatus.NotConverged,RollingBallReach.CertifySpline(surface,new(.2,.3,.4,.5,.1,.2),new(.2,.3,.4,.5),ref visited,1,out strength));Assert.Equal(0,strength);Assert.Equal(1,visited);
    }

    [Fact]
    public void AffinePlaneHasAnAnalyticGlobalFootCertificate()
    {
        var plane=new AnalyticSurface(SurfaceClass.Plane,new(){X=3,Y=-2,Z=7},new(){Z=1},new(){X=1});
        Assert.Equal(AlgorithmStatus.Success,RollingBallReach.CertifyPlane(plane,out var strength));Assert.True(strength>.4);
        var degenerate=new AnalyticSurface(SurfaceClass.Plane,default,new(){Z=1},new(){Z=1});Assert.Equal(AlgorithmStatus.NotConverged,RollingBallReach.CertifyPlane(degenerate,out strength));
    }

    private static KernelVector3 Transform(double x,double y,double z,double scale,bool mirror)
    {
        if(mirror)x=-x;var c=Math.Cos(.47);var s=Math.Sin(.47);var ca=Math.Cos(.31);var sa=Math.Sin(.31);var a=ca*y-sa*z;var b=sa*y+ca*z;
        return new(){X=3+scale*(c*x-s*a),Y=-2+scale*(s*x+c*a),Z=7+scale*b};
    }
    private static IntervalBox3 Transform(IntervalNumber x,IntervalNumber y,IntervalNumber z,double scale,bool mirror)
    {
        if(mirror)x=-x;var c=Math.Cos(.47);var s=Math.Sin(.47);var ca=Math.Cos(.31);var sa=Math.Sin(.31);var a=ca*y-sa*z;var b=sa*y+ca*z;
        var px=3+scale*(c*x-s*a);var py=-2+scale*(s*x+c*a);var pz=7+scale*b;return new(px.Lower,px.Upper,py.Lower,py.Upper,pz.Lower,pz.Upper);
    }
}
