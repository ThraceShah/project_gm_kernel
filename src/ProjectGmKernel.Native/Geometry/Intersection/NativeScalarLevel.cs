using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
namespace ProjectGmKernel.Native.Geometry.Intersection;

// An interval extension of the original length-scaled level equation in
// native v. Coefficients enclose constants; no sampled curve replaces S(u,v).
internal struct NativeScalarLevel
{
    internal bool Ready;
    private IntervalNumber c0,c1,c2,c3,c4,theta,domain;
    internal static NativeScalarLevel Create(in IntervalVector center,in IntervalVector first,in IntervalVector transverse,
        IntervalNumber angle,double radius,in AnalyticSurface target,IntervalNumber free)
    {
        if(!AnalyticLevelSet.Supported(target))return default;
        var d=new IntervalVector(center.X.Value-target.Origin.X,center.Y.Value-target.Origin.Y,center.Z.Value-target.Origin.Z);
        var a=new IntervalVector(first.X.Value,first.Y.Value,first.Z.Value);var b=new IntervalVector(transverse.X.Value,transverse.Y.Value,transverse.Z.Value);
        NativeScalarLevel result=new(){theta=angle,domain=free};IntervalNumber r=new(radius);
        if(target.Kind==SurfaceClass.Plane)
        {
            var axis=new IntervalVector(target.Axis);result.c0=Dot(d,axis);result.c1=r*Dot(a,axis);result.c2=r*Dot(b,axis);
        }
        else
        {
            if(target.Kind==SurfaceClass.Cylinder){d=Project(d,target.Axis);a=Project(a,target.Axis);b=Project(b,target.Axis);}
            IntervalNumber R=new(target.Radius);var rr=r*r;var aa=Square(a);var bb=Square(b);
            result.c0=(Square(d)-R*R+rr*(aa+bb)/2)/(2*R);result.c1=r*Dot(d,a)/R;result.c2=r*Dot(d,b)/R;
            result.c3=rr*(aa-bb)/(4*R);result.c4=rr*Dot(a,b)/(2*R);
        }
        result.Ready=result.c0.Finite&&result.c1.Finite&&result.c2.Finite&&result.c3.Finite&&result.c4.Finite&&angle.Finite&&free.Finite;
        return result;
    }
    internal readonly bool TryBound(IntervalNumber v,out IntervalNumber value,out IntervalNumber derivative)
    {
        value=derivative=default;if(!Ready||!v.Finite||v.Lower<domain.Lower||v.Upper>domain.Upper)return false;
        var t=theta*v;IntervalDual.SinCos(new(t,0),out var sin,out var cos);IntervalDual.SinCos(new(2*t,0),out var sin2,out var cos2);
        value=c0+c1*cos.Value+c2*sin.Value+c3*cos2.Value+c4*sin2.Value;
        derivative=theta*(-c1*sin.Value+c2*cos.Value-2*c3*sin2.Value+2*c4*cos2.Value);
        return value.Finite&&derivative.Finite;
    }
    private static IntervalNumber Dot(in IntervalVector a,in IntervalVector b)=>a.X.Value*b.X.Value+a.Y.Value*b.Y.Value+a.Z.Value*b.Z.Value;
    private static IntervalNumber Square(in IntervalVector a)=>IntervalNumber.Square(a.X.Value)+IntervalNumber.Square(a.Y.Value)+IntervalNumber.Square(a.Z.Value);
    private static IntervalVector Project(in IntervalVector vector,in KernelVector3 axis)
    {
        var x=vector.X.Value;var y=vector.Y.Value;var z=vector.Z.Value;AnalyticLevelSet.RemoveAxial(axis,ref x,ref y,ref z);return new(x,y,z);
    }
}
