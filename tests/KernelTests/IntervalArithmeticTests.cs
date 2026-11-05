using System.Numerics;
using ProjectGmKernel.Native.Computation.Numerics;

namespace KernelTests;

public class IntervalArithmeticTests
{
    [Fact]
    public void ExactDyadicOperationsRetainTheirExactSingletons()
    {
        IntervalNumber a=.5,b=.25;var sum=a+b;var product=a*b;var quotient=b/a;
        Assert.Equal(.75,sum.Lower);Assert.Equal(sum.Lower,sum.Upper);Assert.Equal(.125,product.Lower);Assert.Equal(product.Lower,product.Upper);
        Assert.Equal(.5,quotient.Lower);Assert.Equal(quotient.Lower,quotient.Upper);
    }

    [Fact]
    public void OutwardBoundsEncloseExactBigIntegerFractionsIncludingSubnormalResults()
    {
        var random=new Random(93274);
        for(var i=0;i<500;i++)
        {
            var a=Math.ScaleB(random.NextDouble()*2-1,random.Next(-1000,1001));var b=Math.ScaleB(random.NextDouble()*2-1,random.Next(-1000,1001));
            if(b==0)b=double.Epsilon;
            var fa=Fraction(a);var fb=Fraction(b);
            Check(new IntervalNumber(a)+b,fa.N*fb.D+fb.N*fa.D,fa.D*fb.D);
            Check(new IntervalNumber(a)*b,fa.N*fb.N,fa.D*fb.D);
            Check(new IntervalNumber(a)/b,fa.N*fb.D,fa.D*fb.N);
        }
        Check(new IntervalNumber(double.Epsilon)*.25,BigInteger.One,BigInteger.One<<1076);
    }

    private static (BigInteger N,BigInteger D) Fraction(double value)
    {
        var bits=BitConverter.DoubleToUInt64Bits(value);var exponent=(bits>>52)&2047;var mantissa=bits&0x000fffffffffffffUL;
        if(exponent!=0)mantissa|=1UL<<52;
        var n=new BigInteger(mantissa);if((bits>>63)!=0)n=-n;var power=exponent==0?-1074:(int)exponent-1075;
        return power>=0?(n<<power,BigInteger.One):(n,BigInteger.One<<-power);
    }

    private static void Check(IntervalNumber interval,BigInteger n,BigInteger d)
    {
        if(d.Sign<0){n=-n;d=-d;}
        Assert.False(double.IsNaN(interval.Lower)||double.IsNaN(interval.Upper));
        if(!double.IsNegativeInfinity(interval.Lower)){var lower=Fraction(interval.Lower);Assert.True(lower.N*d<=n*lower.D,$"lower={interval.Lower:R}");}
        if(!double.IsPositiveInfinity(interval.Upper)){var upper=Fraction(interval.Upper);Assert.True(upper.N*d>=n*upper.D,$"upper={interval.Upper:R}");}
    }
}
