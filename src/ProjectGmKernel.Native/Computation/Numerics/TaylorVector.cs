using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Computation.Numerics;

internal struct TaylorVector(TaylorJet x,TaylorJet y,TaylorJet z)
{
    internal TaylorJet X=x,Y=y,Z=z;
    internal readonly KernelVector3 Value=>new(){X=X[0],Y=Y[0],Z=Z[0]};
    internal readonly bool Finite=>X.Finite&&Y.Finite&&Z.Finite;
    internal static TaylorVector Constant(in KernelVector3 value,DerivativeOrder order)
        =>new(new(value.X,order),new(value.Y,order),new(value.Z,order));
    internal readonly TaylorVector Truncate(DerivativeOrder order)=>new(X.Truncate(order),Y.Truncate(order),Z.Truncate(order));
    internal readonly TaylorVector Differentiate()=>new(X.Differentiate(),Y.Differentiate(),Z.Differentiate());
    internal readonly TaylorVector Compose(TaylorJet delta)=>new(X.Compose(delta),Y.Compose(delta),Z.Compose(delta));
    public static TaylorVector operator +(TaylorVector a,TaylorVector b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static TaylorVector operator -(TaylorVector a,TaylorVector b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static TaylorVector operator -(TaylorVector a)=>new(-a.X,-a.Y,-a.Z);
    public static TaylorVector operator *(TaylorVector a,TaylorJet b)=>new(a.X*b,a.Y*b,a.Z*b);
    public static TaylorVector operator *(TaylorVector a,double b)=>new(a.X*b,a.Y*b,a.Z*b);
    public static TaylorVector operator /(TaylorVector a,TaylorJet b)=>new(a.X/b,a.Y/b,a.Z/b);
    internal static TaylorJet Dot(TaylorVector a,TaylorVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    internal static TaylorVector Cross(TaylorVector a,TaylorVector b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
    internal readonly TaylorJet Norm()=>TaylorJet.Sqrt(Dot(this,this));
    internal readonly TaylorVector Unit()=>this/Norm();
}
