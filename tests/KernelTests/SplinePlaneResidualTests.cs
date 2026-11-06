using System.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class SplinePlaneResidualTests
{
    [Theory]
    [InlineData(false,false,0)] [InlineData(true,false,-900)] [InlineData(true,false,900)]
    [InlineData(false,true,0)] [InlineData(true,true,-900)] [InlineData(true,true,900)]
    public void SmallResidualAgreesWithExactBinaryPolynomialAcrossWeightScales(bool rational,bool multipleSpans,int weightExponent)
    {
        Span<double> poles=stackalloc double[48];Span<double> uKnots=stackalloc double[7],vKnots=stackalloc double[6];
        Span<double> workspace=stackalloc double[200];Span<KernelVector3> point=stackalloc KernelVector3[1];
        foreach(var origin in new[]{.5,1024.0})foreach(var h in new[]{Math.ScaleB(1,-16),Math.ScaleB(1,-20),Math.ScaleB(1,-24)})
        {
            var view=Surface(poles,uKnots,vKnots,rational,multipleSpans,weightExponent,origin);var plane=new AnalyticSurface(SurfaceClass.Plane,new(){Z=origin+h},new(){Z=1},new(){X=1});
            for(var sample=1;sample<13;sample++)
            {
                var angle=sample*.2718281828459045;var u=.5+Math.Sqrt(h)*Math.Cos(angle);var v=.5+Math.Sqrt(h)*Math.Sin(angle);
                Assert.True(SplinePlaneResidual.TryEvaluate(view,plane,u,v,out var residual));
                var exact=ExactResidual(u,v,origin,plane.Origin.Z);Assert.True(Math.Abs(residual-exact)<1e-28,$"{residual:R} vs exact {exact:R}");
                SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
                Assert.Equal(ProjectGmKernel.Native.Computation.AlgorithmStatus.Success,BSurfaceEvaluation.Evaluate(view,u,v,layout,workspace,point));
                var difference=new KernelVector3{Z=point[0].Z-plane.Origin.Z};
                Assert.True(SplinePlaneResidual.TryCorrectDifference(view,plane,u,v,point[0].X,point[0].Y,true,difference,out var corrected));
                Assert.True(Math.Abs(corrected.Z-exact)<1e-28);Assert.Equal(0,corrected.X);Assert.Equal(0,corrected.Y);
                Assert.True(SplinePlaneResidual.TryCorrectDifference(view,plane,u,v,point[0].X,point[0].Y,false,new(){Z=-difference.Z},out corrected));
                Assert.True(Math.Abs(corrected.Z+exact)<1e-28);
            }
        }
    }

    private static BSurfaceView Surface(Span<double> poles,Span<double> uk,Span<double> vk,bool rational,bool multiple,int exponent,double origin)
    {
        var nu=multiple?4:3;var dim=rational?4:3;var weight=Math.ScaleB(1,exponent);
        Span<double> x=stackalloc double[4]{-.5,-.25,.25,.5};Span<double> z=stackalloc double[4]{.25,0,0,.25};Span<double> zz=stackalloc double[3]{.25,-.25,.25};
        for(var i=0;i<nu;i++)for(var j=0;j<3;j++)
        {
            var at=dim*(i*3+j);poles[at]=(multiple?x[i]:i*.5-.5)*weight;poles[at+1]=(j*.5-.5)*weight;poles[at+2]=(origin+(multiple?z[i]:zz[i])+zz[j])*weight;if(rational)poles[at+3]=weight;
        }
        uk.Clear();vk.Clear();if(multiple){uk[3]=.5;uk[4]=uk[5]=uk[6]=1;}else uk[3]=uk[4]=uk[5]=1;
        vk[3]=vk[4]=vk[5]=1;
        return new(2,2,nu,3,dim,rational,false,false,poles[..(nu*3*dim)],uk[..(nu+3)],vk);
    }

    // Every input double is an exact dyadic. Form the independent polynomial
    // with BigInteger at a shared 2^-1074 scale before the final conversion.
    private static double ExactResidual(double u,double v,double origin,double plane)
    {
        var scale=BigInteger.One<<1074;var x=Scaled(u)-(scale>>1);var y=Scaled(v)-(scale>>1);
        var numerator=x*x+y*y-(Scaled(plane)-Scaled(origin))*scale;if(numerator.IsZero)return 0;
        var sign=numerator.Sign;numerator=BigInteger.Abs(numerator);var shift=(int)Math.Max(0,numerator.GetBitLength()-54);
        return sign*Math.ScaleB((double)(numerator>>shift),shift-2148);
    }
    private static BigInteger Scaled(double value)
    {
        var bits=BitConverter.DoubleToUInt64Bits(value);var exponent=(int)((bits>>52)&2047);var mantissa=bits&0x000fffffffffffffUL;
        var result=new BigInteger(exponent==0?mantissa:mantissa|0x0010000000000000UL);
        if(exponent!=0)result<<=exponent-1;return (bits>>63)==0?result:-result;
    }
}
