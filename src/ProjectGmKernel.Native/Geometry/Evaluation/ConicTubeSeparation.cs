using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class ConicTubeSeparation
{
    // C = O + A*x, |x| = 1. Completing the square with M=A^T*A+lambda*I
    // gives a lower bound for the entire conic, even if the proposed
    // multiplier comes from an inaccurate nearest-station estimate.
    internal static bool ProvesOutside(in ParametricSpineCurve conic,in KernelVector3 point,double radius,double tolerance,double station)
    {
        if(conic.Kind is not (CurveClass.Circle or CurveClass.Ellipse)||!(radius>0)||!(tolerance>=0)||!double.IsFinite(station))return false;
        var scale=Math.Max(Math.Max(Magnitude(point),Magnitude(conic.Origin)),
            Math.Max(Math.Max(Magnitude(conic.First),Magnitude(conic.Second)),Math.Max(radius,tolerance)));
        if(!double.IsFinite(scale))return false;
        var exponent=Math.ILogB(scale);
        var a=Scaled(conic.First,exponent);var b=Scaled(conic.Second,exponent);
        var d=Sub(Scaled(point,exponent),Scaled(conic.Origin,exponent));
        var (s,c)=Math.SinCos(station);
        var residual=Sub(d,Add(Scale(a,c),Scale(b,s)));
        var multiplier=Dot(Add(Scale(a,c),Scale(b,s)),residual);
        if(!double.IsFinite(multiplier)||Math.Abs(multiplier)>64)return false;

        var first=WideVector.Input(conic.First,exponent);var second=WideVector.Input(conic.Second,exponent);
        var delta=WideVector.Input(point,exponent)-WideVector.Input(conic.Origin,exponent);
        var lambda=Wide.Input(multiplier,0);
        var m0=first.Dot(first)+lambda;var m1=second.Dot(second)+lambda;var cross=first.Dot(second);
        var determinant=m0*m1-cross*cross;
        if(m0.Lower<=0||determinant.Lower<=0)return false;
        var g0=first.Dot(delta);var g1=second.Dot(delta);
        var limit=Wide.Input(radius,exponent)+Wide.Input(tolerance,exponent);
        var numerator=m1*g0*g0+m0*g1*g1-(cross*g0*g1)*new Wide(2,2);
        // Avoid division by the determinant or subtracting nearly equal
        // rounded double distances. The sign is enclosed in binary-input
        // decimal interval arithmetic.
        return ((delta.Dot(delta)-lambda-limit*limit)*determinant-numerator).Lower>0;
    }

    private static KernelVector3 Scaled(in KernelVector3 value,int exponent)
        =>new(){X=Math.ScaleB(value.X,-exponent),Y=Math.ScaleB(value.Y,-exponent),Z=Math.ScaleB(value.Z,-exponent)};

    private static double Magnitude(in KernelVector3 value)=>Math.Max(Math.Abs(value.X),Math.Max(Math.Abs(value.Y),Math.Abs(value.Z)));

    private readonly struct WideVector(Wide x,Wide y,Wide z)
    {
        private readonly Wide X=x,Y=y,Z=z;
        internal static WideVector Input(in KernelVector3 value,int exponent)
            =>new(Wide.Input(value.X,exponent),Wide.Input(value.Y,exponent),Wide.Input(value.Z,exponent));
        internal Wide Dot(in WideVector value)=>X*value.X+Y*value.Y+Z*value.Z;
        public static WideVector operator -(WideVector a,WideVector b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    }

    // Scope: operands are normalized by one exact power of two, and the
    // finite polynomial above keeps decimal intermediates below 10^10.
    // Decimal has a 96-bit coefficient and up to 28 decimal places. This
    // outward pad exceeds the rounding of the operation and of the pad
    // itself, also for sub-unit values. It is arithmetic error, not a
    // geometric tolerance.
    private readonly struct Wide(decimal lower,decimal upper)
    {
        internal readonly decimal Lower=lower;
        private readonly decimal Upper=upper;

        internal static Wide Input(double x,int normalizingExponent)
        {
            if(x==0)return new(0,0);
            var bits=(ulong)BitConverter.DoubleToInt64Bits(x);
            var raw=(int)((bits>>52)&0x7ff);
            var significand=(bits&0x000fffffffffffffUL)|(raw==0?0UL:0x0010000000000000UL);
            var exponent=(raw==0?-1074:raw-1075)-normalizingExponent;
            if(exponent< -160)return Enclose(0,0);
            // All normalized inputs (including the multiplier) have
            // magnitude <=64 and therefore a negative binary exponent
            // when represented by this significand.
            var value=(decimal)significand;
            while(exponent< -60){value/=1152921504606846976m;exponent+=60;}
            value/=(decimal)(1UL<< -exponent);
            if((bits>>63)!=0)value=-value;
            return Enclose(value,value);
        }

        private static decimal Pad(decimal x)=>1e-26m*(1+Math.Abs(x));
        private static Wide Enclose(decimal a,decimal b)=>new(a-Pad(a),b+Pad(b));
        public static Wide operator +(Wide a,Wide b)=>Enclose(a.Lower+b.Lower,a.Upper+b.Upper);
        public static Wide operator -(Wide a,Wide b)=>Enclose(a.Lower-b.Upper,a.Upper-b.Lower);
        public static Wide operator *(Wide a,Wide b)
        {
            var p=a.Lower*b.Lower;var q=a.Lower*b.Upper;var r=a.Upper*b.Lower;var s=a.Upper*b.Upper;
            return Enclose(Math.Min(Math.Min(p,q),Math.Min(r,s)),Math.Max(Math.Max(p,q),Math.Max(r,s)));
        }
    }
}
