using System.Runtime.CompilerServices;

namespace ProjectGmKernel.Native.Computation.Numerics;

/// <summary>Truncated univariate Taylor series; coefficient k is f^(k)/k!.
/// Used to differentiate nested distance/spine constraints analytically.</summary>
internal struct TaylorJet
{
    internal const DerivativeOrder MaxOrder=7;
    [InlineArray(MaxOrder+1)] private struct Coefficients{private double value;}
    private Coefficients coefficients;
    internal DerivativeOrder Order;
    internal double this[DerivativeOrder k]{readonly get=>coefficients[k];set=>coefficients[k]=value;}
    internal TaylorJet(double value,DerivativeOrder order){this=default;Order=order;coefficients[0]=value;}
    internal static TaylorJet Variable(double value,DerivativeOrder order)
    {var result=new TaylorJet(value,order);if(order>0)result[1]=1;return result;}
    internal readonly TaylorJet Truncate(DerivativeOrder order)
    {var result=this;result.Order=order;for(var k=order+1;k<=MaxOrder;k++)result[k]=0;return result;}
    internal readonly TaylorJet Differentiate()
    {var result=new TaylorJet(0,Math.Max(0,Order-1));for(var k=0;k<Order;k++)result[k]=(k+1)*this[k+1];return result;}
    public static TaylorJet operator +(TaylorJet a,TaylorJet b)
    {var result=new TaylorJet(0,Math.Max(a.Order,b.Order));for(var k=0;k<=result.Order;k++)result[k]=a[k]+b[k];return result;}
    public static TaylorJet operator -(TaylorJet a,TaylorJet b)=>a+-b;
    public static TaylorJet operator -(TaylorJet a)
    {for(var k=0;k<=a.Order;k++)a[k]=-a[k];return a;}
    public static TaylorJet operator +(TaylorJet a,double b){a[0]+=b;return a;}
    public static TaylorJet operator +(double a,TaylorJet b)=>b+a;
    public static TaylorJet operator -(TaylorJet a,double b)=>a+-b;
    public static TaylorJet operator -(double a,TaylorJet b)=>-b+a;
    public static TaylorJet operator *(TaylorJet a,double b)
    {for(var k=0;k<=a.Order;k++)a[k]*=b;return a;}
    public static TaylorJet operator *(double a,TaylorJet b)=>b*a;
    public static TaylorJet operator /(TaylorJet a,double b)=>a*(1/b);
    public static TaylorJet operator *(TaylorJet a,TaylorJet b)
    {
        var result=new TaylorJet(0,Math.Max(a.Order,b.Order));
        for(var k=0;k<=result.Order;k++)for(var i=0;i<=k;i++)result[k]+=a[i]*b[k-i];return result;
    }
    public static TaylorJet operator /(TaylorJet a,TaylorJet b)
    {
        var result=new TaylorJet(0,Math.Max(a.Order,b.Order));
        for(var k=0;k<=result.Order;k++){var value=a[k];for(var i=1;i<=k;i++)value-=b[i]*result[k-i];result[k]=value/b[0];}return result;
    }
    internal static TaylorJet Sqrt(TaylorJet a)
    {
        var result=new TaylorJet(Math.Sqrt(a[0]),a.Order);
        for(var k=1;k<=a.Order;k++){var value=a[k];for(var i=1;i<k;i++)value-=result[i]*result[k-i];result[k]=value/(2*result[0]);}return result;
    }
    internal static void SinCos(TaylorJet a,out TaylorJet sin,out TaylorJet cos)
    {
        var pair=Math.SinCos(a[0]);sin=new(pair.Sin,a.Order);cos=new(pair.Cos,a.Order);
        for(var k=1;k<=a.Order;k++)
        {
            for(var i=1;i<=k;i++){sin[k]+=i*a[i]*cos[k-i];cos[k]-=i*a[i]*sin[k-i];}
            sin[k]/=k;cos[k]/=k;
        }
    }
    internal static TaylorJet Atan2(TaylorJet y,TaylorJet x)
    {
        var result=new TaylorJet(Math.Atan2(y[0],x[0]),Math.Max(x.Order,y.Order));
        if(result.Order==0)return result;
        var derivative=((x*y.Differentiate()-y*x.Differentiate())/(x*x+y*y)).Truncate(result.Order-1);
        for(var k=1;k<=result.Order;k++)result[k]=derivative[k-1]/k;return result;
    }
    internal readonly TaylorJet Compose(TaylorJet delta)
    {
        var result=new TaylorJet(this[Order],delta.Order);
        for(var k=Order-1;k>=0;k--)result=result*delta+this[k];return result;
    }
    internal readonly bool Finite
    {get{for(var k=0;k<=Order;k++)if(!double.IsFinite(this[k]))return false;return true;}}
}
