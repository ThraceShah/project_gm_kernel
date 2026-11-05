using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Computation.Numerics;

/// <summary>Outward interval extension of a scalar and one analytic derivative.</summary>
internal readonly struct IntervalDual(IntervalNumber value,IntervalNumber derivative)
{
    internal readonly IntervalNumber Value=value,Derivative=derivative;
    internal bool Finite=>Value.Finite&&Derivative.Finite;
    public static implicit operator IntervalDual(double x)=>new(x,0);
    public static implicit operator IntervalDual(IntervalNumber x)=>new(x,0);
    public static IntervalDual operator +(IntervalDual a,IntervalDual b)=>new(a.Value+b.Value,a.Derivative+b.Derivative);
    public static IntervalDual operator -(IntervalDual a)=>new(-a.Value,-a.Derivative);
    public static IntervalDual operator -(IntervalDual a,IntervalDual b)=>a+-b;
    public static IntervalDual operator *(IntervalDual a,IntervalDual b)=>new(a.Value*b.Value,a.Derivative*b.Value+a.Value*b.Derivative);
    public static IntervalDual operator /(IntervalDual a,IntervalDual b)=>new(a.Value/b.Value,(a.Derivative*b.Value-a.Value*b.Derivative)/IntervalNumber.Square(b.Value));
    internal static IntervalDual Square(IntervalDual x)=>new(IntervalNumber.Square(x.Value),2*x.Value*x.Derivative);
    internal static IntervalDual Sqrt(IntervalDual x)
    {
        var lower=Math.Sqrt(Math.Max(0,x.Value.Lower));var upper=Math.Sqrt(x.Value.Upper);
        var value=new IntervalNumber(lower==0?0:Math.BitDecrement(lower),Math.BitIncrement(upper));return new(value,x.Derivative/(2*value));
    }
    internal static IntervalDual Atan2(IntervalDual y,IntervalDual x)
    {
        if(!(y.Value.Lower>0))return new(new(double.NegativeInfinity,double.PositiveInfinity),new(double.NegativeInfinity,double.PositiveInfinity));
        var a=Math.Atan2(y.Value.Lower,x.Value.Lower);var b=Math.Atan2(y.Value.Lower,x.Value.Upper);var c=Math.Atan2(y.Value.Upper,x.Value.Lower);var d=Math.Atan2(y.Value.Upper,x.Value.Upper);
        return new(new(Math.BitDecrement(Math.BitDecrement(Math.Min(Math.Min(a,b),Math.Min(c,d)))),Math.BitIncrement(Math.BitIncrement(Math.Max(Math.Max(a,b),Math.Max(c,d))))),
            (x.Value*y.Derivative-y.Value*x.Derivative)/(IntervalNumber.Square(x.Value)+IntervalNumber.Square(y.Value)));
    }
    internal static void SinCos(IntervalDual x,out IntervalDual sin,out IntervalDual cos)
    {var s=Trig(x.Value,false);var c=Trig(x.Value,true);sin=new(s,c*x.Derivative);cos=new(c,-s*x.Derivative);}
    private static IntervalNumber Trig(IntervalNumber x,bool cosine)
    {
        if(!x.Finite||x.Magnitude>1e14||x.Upper-x.Lower>=2*Math.PI)return new(-1,1);
        var a=cosine?Math.Cos(x.Lower):Math.Sin(x.Lower);var b=cosine?Math.Cos(x.Upper):Math.Sin(x.Upper);var lo=Math.Min(a,b);var hi=Math.Max(a,b);
        var phase=cosine?0:Math.PI/2;var first=Math.Ceiling((x.Lower-phase)/Math.PI);var last=Math.Floor((x.Upper-phase)/Math.PI);
        for(var k=first;k<=last;k++){if(k%2==0)hi=1;else lo=-1;}
        return new(Math.Max(-1,Math.BitDecrement(Math.BitDecrement(lo))),Math.Min(1,Math.BitIncrement(Math.BitIncrement(hi))));
    }
}

internal readonly struct IntervalVector(IntervalDual x,IntervalDual y,IntervalDual z)
{
    internal readonly IntervalDual X=x,Y=y,Z=z;
    internal IntervalVector(in KernelVector3 point):this(point.X,point.Y,point.Z){}
    internal bool Finite=>X.Finite&&Y.Finite&&Z.Finite;
    public static IntervalVector operator +(IntervalVector a,IntervalVector b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static IntervalVector operator -(IntervalVector a,IntervalVector b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static IntervalVector operator *(IntervalVector a,IntervalDual b)=>new(a.X*b,a.Y*b,a.Z*b);
    public static IntervalVector operator /(IntervalVector a,IntervalDual b)=>new(a.X/b,a.Y/b,a.Z/b);
    internal static IntervalDual Dot(IntervalVector a,IntervalVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static IntervalVector Cross(IntervalVector a,IntervalVector b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    internal IntervalDual Norm()=>IntervalDual.Sqrt(IntervalDual.Square(X)+IntervalDual.Square(Y)+IntervalDual.Square(Z));
    internal IntervalVector Unit()=>this/Norm();
}
