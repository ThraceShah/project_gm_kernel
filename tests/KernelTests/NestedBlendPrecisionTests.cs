using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class NestedBlendPrecisionTests
{
    [Theory]
    [InlineData(.001,0)]
    [InlineData(.23,.17)]
    [InlineData(.7,.83)]
    [InlineData(.999,1)]
    public void CurvedSpineD2MatchesAnIndependentDecimalClosedForm(double fraction,double v)
    {
        var axis=new KernelVector3{Z=1};var x=new KernelVector3{X=1};var length=Math.Sqrt(.4);
        BlendSurfaceInstruction[] surfaces=
        [
            new(){Analytic=new(SurfaceClass.Sphere,default,axis,x,1),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,default,axis,x),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Sphere,default,axis,x,.8),NormalSense=1},
            new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},axis,x),NormalSense=1},
            new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}
        ];
        BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2}];
        BlendChartInstruction[] charts=[new(){Position=x,Chord=new(){X=-.2/length,Y=-.6/length},Scale=1},new(){Position=new(){X=.8,Y=-.6},Parameter=length,Scale=1}];
        var program=new NestedBlendProgram(surfaces,spines,charts);SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> actual=stackalloc KernelVector3[9];
        var u=length*fraction;Assert.Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Evaluate(program,4,u,v,layout,actual));
        // Reference is a toroidal patch with chart coordinate q=e.P. Its
        // derivatives below use decimal closed forms, independent of Taylor
        // jets, the implicit Jacobian and all production distance evaluators.
        var root=SquareRoot(10m);var ex=-1m/root;var ey=-3m/root;var hx=3m/root;var hy=-1m/root;
        var q=ex+(decimal)u;var h=SquareRoot(1-q*q);var theta=(decimal)v*Pi/2;SinCos(theta,out var sin,out var cos);
        var f=1-.2m*sin;var fv=-.2m*cos*Pi/2;var fvv=.2m*sin*Pi*Pi/4;
        var cx=ex*q+hx*h;var cy=ey*q+hy*h;var dx=ex-hx*q/h;var dy=ey-hy*q/h;var ddx=-hx/(h*h*h);var ddy=-hy/(h*h*h);
        Check(actual[layout.GetIndex(0,0)],f*cx,f*cy,-.2m*cos);
        Check(actual[layout.GetIndex(1,0)],f*dx,f*dy,0);
        Check(actual[layout.GetIndex(2,0)],f*ddx,f*ddy,0);
        Check(actual[layout.GetIndex(0,1)],fv*cx,fv*cy,.2m*sin*Pi/2);
        Check(actual[layout.GetIndex(1,1)],fv*dx,fv*dy,0);
        Check(actual[layout.GetIndex(0,2)],fvv*cx,fvv*cy,.2m*cos*Pi*Pi/4);
    }

    private const decimal Pi=3.1415926535897932384626433833m;
    private static decimal SquareRoot(decimal value)
    {var x=value>1?value:1;for(var i=0;i<40;i++){var next=(x+value/x)/2;if(next==x)return x;x=next;}return x;}
    private static void SinCos(decimal x,out decimal sin,out decimal cos)
    {
        sin=x;cos=1;var s=x;var c=1m;
        for(var i=1;i<24;i++){s*=-x*x/((2*i)*(2*i+1));c*=-x*x/((2*i-1)*(2*i));sin+=s;cos+=c;}
    }
    private static void Check(in KernelVector3 actual,decimal x,decimal y,decimal z)
    {Near(actual.X,(double)x);Near(actual.Y,(double)y);Near(actual.Z,(double)z);}
    private static void Near(double actual,double expected)=>Assert.InRange(Math.Abs(actual-expected)/(1+Math.Abs(expected)),0,2e-12);
}
