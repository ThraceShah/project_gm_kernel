using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

internal enum IntersectionVectorKind { Simple = 14611, Tangent = 14612, Start = 14613, End = 14614 }
internal enum IntersectionCurveKind { Simple = 14651, Tangent = 14652 }

internal struct OperationIntersectionPoint
{
    internal KernelVector3 Point;
    internal double First, Second, U, V;
    internal IntersectionVectorKind Kind;
}

internal unsafe struct OperationIntersectionBranch
{
    internal CurveClass Class;
    internal KernelVector3 Origin, Axis, Reference;
    internal double Radius, Minor, Start, End;
    internal IntersectionCurveKind Kind;
    internal SplineDegree Degree;
    // Exact rational polynomial branches have at most degree 16. These
    // homogeneous poles are copied into owned geometry before returning.
    internal fixed double Poles[68];
}

/// <summary>
/// Algebraic discovery for explicitly supported analytic configurations. Every
/// returned branch has a complete defining curve and a parameter interval.
/// Unsupported configurations never become a successful empty intersection.
/// </summary>
internal static class OperationIntersection
{
    internal const double DistanceTolerance = 1e-8;
    internal const double AngularTolerance = 1e-12;
    internal static double Norm(KernelVector3 v)
    {
        var scale=Math.Max(Math.Abs(v.X),Math.Max(Math.Abs(v.Y),Math.Abs(v.Z)));
        if(scale==0)return 0;if(!double.IsFinite(scale))return double.PositiveInfinity;
        var x=v.X/scale;var y=v.Y/scale;var z=v.Z/scale;return scale*Math.Sqrt(x*x+y*y+z*z);
    }
    internal static double ParameterTolerance(double a, double b) => 2e-12 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

    internal static bool AddPoint(Span<OperationIntersectionPoint> output, ref BufferCount count,
        KernelVector3 point, double first, double second, IntersectionVectorKind kind)
    {
        if (!IsFinite(point) || !double.IsFinite(first) || !double.IsFinite(second)) return false;
        for (var i = 0; i < count; i++)
            if (Math.Abs(output[i].First - first) <= ParameterTolerance(output[i].First, first)
                && Norm(Sub(output[i].Point, point)) <= DistanceTolerance)
            {
                if (kind is IntersectionVectorKind.Start or IntersectionVectorKind.End) output[i].Kind = kind;
                return true;
            }
        if (count == output.Length) return false;
        output[count++] = new() { Point = point, First = first, Second = second, Kind = kind };
        return true;
    }

    internal static void Sort(Span<OperationIntersectionPoint> points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            var value = points[i]; var j = i;
            while (j > 0 && points[j - 1].First > value.First) { points[j] = points[j - 1]; j--; }
            points[j] = value;
        }
    }

    internal static bool InInterval(double t, double lo, double hi)
        => t >= lo - ParameterTolerance(lo, hi) && t <= hi + ParameterTolerance(lo, hi);

    internal static bool PeriodicParameter(double angle, double lo, double hi, out double t)
    {
        t = angle + Math.Tau * Math.Ceiling((lo - angle - ParameterTolerance(lo, hi)) / Math.Tau);
        if (!InInterval(t, lo, hi)) return false;
        t = Math.Clamp(t, lo, hi);
        return true;
    }

    internal static AlgorithmStatus LineLine(KernelVector3 p, KernelVector3 d, double a, double b,
        KernelVector3 q, KernelVector3 e, double c, double f, Span<OperationIntersectionPoint> output, out BufferCount count)
    {
        count = 0;
        var r = Sub(q, p); var cross = Cross(d, e); var det = Dot(cross, cross);
        if(det==0&&Norm(cross)>0)return AlgorithmStatus.NumericalFailure;
        if(det>0&&det<=AngularTolerance*AngularTolerance)return AlgorithmStatus.NumericalFailure;
        if (det > AngularTolerance * AngularTolerance)
        {
            var t = Dot(Cross(r, e), cross) / det;
            var u = Dot(Cross(r, d), cross) / det;
            var x = Add(p, Scale(d, t)); var y = Add(q, Scale(e, u));
            if (!IsFinite(x) || !IsFinite(y)) return AlgorithmStatus.NumericalFailure;
            if (InInterval(t, a, b) && InInterval(u, c, f) && Norm(Sub(x, y)) <= DistanceTolerance)
                return AddPoint(output, ref count, x, Math.Clamp(t,a,b), Math.Clamp(u,c,f), IntersectionVectorKind.Simple)
                    ? AlgorithmStatus.Success : AlgorithmStatus.OutputTooSmall;
            return AlgorithmStatus.Success;
        }
        if (Norm(Cross(r, d)) > DistanceTolerance) return AlgorithmStatus.Success;
        var offset = Dot(r, d); var sense = Dot(d, e);
        var lo = Math.Max(a, Math.Min(offset + sense * c, offset + sense * f));
        var hi = Math.Min(b, Math.Max(offset + sense * c, offset + sense * f));
        if (hi < lo - ParameterTolerance(lo, hi)) return AlgorithmStatus.Success;
        if (hi - lo <= ParameterTolerance(lo, hi))
            return AddPoint(output, ref count, Add(p, Scale(d,lo)), lo, (lo-offset)/sense, IntersectionVectorKind.Tangent)
                ? AlgorithmStatus.Success : AlgorithmStatus.OutputTooSmall;
        return AddPoint(output, ref count, Add(p,Scale(d,lo)),lo,(lo-offset)/sense,IntersectionVectorKind.Start)
            && AddPoint(output,ref count,Add(p,Scale(d,hi)),hi,(hi-offset)/sense,IntersectionVectorKind.End)
            ? AlgorithmStatus.Success : AlgorithmStatus.OutputTooSmall;
    }

    internal static AlgorithmStatus LineConic(KernelVector3 p, KernelVector3 d, double a, double b,
        in OperationIntersectionBranch conic, double c, double f, Span<OperationIntersectionPoint> output, out BufferCount count)
    {
        count = 0;
        var r = Sub(p,conic.Origin); var y = Cross(conic.Axis,conic.Reference);
        var normalD = Dot(d,conic.Axis); var height = Dot(r,conic.Axis);
        Span<double> roots = stackalloc double[2]; var nr=0;
        if (Math.Abs(normalD)>AngularTolerance) roots[nr++]=-height/normalD;
        else
        {
            if(Math.Abs(height)>DistanceTolerance)return AlgorithmStatus.Success;
            var x0=Dot(r,conic.Reference)/conic.Radius;var y0=Dot(r,y)/conic.Minor;
            var xd=Dot(d,conic.Reference)/conic.Radius;var yd=Dot(d,y)/conic.Minor;
            if(!Quadratic(xd*xd+yd*yd,2*(x0*xd+y0*yd),x0*x0+y0*y0-1,roots,out nr))
                return AlgorithmStatus.NumericalFailure;
        }
        for(var i=0;i<nr;i++)
        {
            var t=roots[i];if(!InInterval(t,a,b))continue;
            var point=Add(p,Scale(d,t));var relative=Sub(point,conic.Origin);
            var x=Dot(relative,conic.Reference)/conic.Radius;var v=Dot(relative,y)/conic.Minor;
            if(Math.Abs(x*x+v*v-1)>DistanceTolerance/Math.Max(DistanceTolerance,Math.Min(conic.Radius,conic.Minor)))continue;
            if(!PeriodicParameter(Math.Atan2(v,x),c,f,out var angle))continue;
            var tangent=Add(Scale(conic.Reference,-conic.Radius*Math.Sin(angle)),Scale(y,conic.Minor*Math.Cos(angle)));
            var kind=Norm(Cross(Unit(tangent),d))<=1e-8?IntersectionVectorKind.Tangent:IntersectionVectorKind.Simple;
            if(!AddPoint(output,ref count,point,Math.Clamp(t,a,b),angle,kind))return AlgorithmStatus.OutputTooSmall;
        }
        return AlgorithmStatus.Success;
    }

    internal static bool Quadratic(double a,double b,double c,Span<double> roots,out BufferCount count)
    {
        count=0;var scale=Math.Max(Math.Abs(a),Math.Max(Math.Abs(b),Math.Abs(c)));
        if(!double.IsFinite(scale))return false;if(scale==0)return false;
        a/=scale;b/=scale;c/=scale;
        if(a==0){if(b!=0)roots[count++]=-c/b;return true;}
        var disc=Math.FusedMultiplyAdd(b,b,-4*a*c);
        // Do not turn a genuinely negative discriminant into a tangent root.
        if(disc<0)return true;
        if(disc==0){roots[count++]=-b/(2*a);return double.IsFinite(roots[0]);}
        var q=-.5*(b+Math.CopySign(Math.Sqrt(disc),b));
        roots[count++]=q/a;roots[count++]=c/q;
        if(roots[0]>roots[1])(roots[0],roots[1])=(roots[1],roots[0]);
        return double.IsFinite(roots[0])&&double.IsFinite(roots[1]);
    }

    internal static AlgorithmStatus PlaneConic(in AnalyticSurface plane, in OperationIntersectionBranch conic,
        double lo,double hi,Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var y=Cross(conic.Axis,conic.Reference);
        var a=conic.Radius*Dot(plane.Axis,conic.Reference);var b=conic.Minor*Dot(plane.Axis,y);
        var c=Dot(plane.Axis,Sub(conic.Origin,plane.Origin));var amplitude=Math.Sqrt(a*a+b*b);
        if(!double.IsFinite(amplitude)||!double.IsFinite(c))return AlgorithmStatus.NumericalFailure;
        if(amplitude<=AngularTolerance*Math.Max(conic.Radius,conic.Minor))
        {
            if(Math.Abs(c)>DistanceTolerance)return AlgorithmStatus.Success;
            return AddPoint(output,ref count,ConicPoint(conic,lo),lo,0,IntersectionVectorKind.Start)
                &&AddPoint(output,ref count,ConicPoint(conic,hi),hi,0,IntersectionVectorKind.End)
                ?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        }
        if(Math.Abs(c)>amplitude+DistanceTolerance)return AlgorithmStatus.Success;
        var alpha=Math.Atan2(b,a);var delta=Math.Acos(Math.Clamp(-c/amplitude,-1,1));
        for(var i=0;i<2;i++)
        {
            if(!PeriodicParameter(alpha+(i==0?delta:-delta),lo,hi,out var t))continue;
            var kind=delta<=1e-10||Math.PI-delta<=1e-10?IntersectionVectorKind.Tangent:IntersectionVectorKind.Simple;
            if(!AddPoint(output,ref count,ConicPoint(conic,t),t,0,kind))return AlgorithmStatus.OutputTooSmall;
        }
        Sort(output[..count]);return AlgorithmStatus.Success;
    }

    internal static KernelVector3 ConicPoint(in OperationIntersectionBranch conic,double t)
        => Add(conic.Origin,Add(Scale(conic.Reference,conic.Radius*Math.Cos(t)),Scale(Cross(conic.Axis,conic.Reference),conic.Minor*Math.Sin(t))));

    internal static AlgorithmStatus AnalyticLine(in AnalyticSurface surface,KernelVector3 p,KernelVector3 d,
        double lo,double hi,Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;var r=Sub(p,surface.Origin);var z=Dot(r,surface.Axis);var dz=Dot(d,surface.Axis);
        var radial=Sub(r,Scale(surface.Axis,z));var dr=Sub(d,Scale(surface.Axis,dz));
        Span<double> polynomial=stackalloc double[5];polynomial.Clear();var degree=2;
        switch(surface.Kind)
        {
            case SurfaceClass.Plane:polynomial[0]=z;polynomial[1]=dz;degree=1;break;
            case SurfaceClass.Sphere:polynomial[0]=Dot(r,r)-surface.Radius*surface.Radius;polynomial[1]=2*Dot(r,d);polynomial[2]=Dot(d,d);break;
            case SurfaceClass.Cylinder:polynomial[0]=Dot(radial,radial)-surface.Radius*surface.Radius;polynomial[1]=2*Dot(radial,dr);polynomial[2]=Dot(dr,dr);break;
            case SurfaceClass.Cone:
                var g=surface.Radius+surface.Secondary*z;var dg=surface.Secondary*dz;
                polynomial[0]=Dot(radial,radial)-g*g;polynomial[1]=2*(Dot(radial,dr)-g*dg);polynomial[2]=Dot(dr,dr)-dg*dg;break;
            case SurfaceClass.Torus:
                if(surface.Radius<=surface.Secondary)return AlgorithmStatus.Unsupported;
                var q0=Dot(r,r)+surface.Radius*surface.Radius-surface.Secondary*surface.Secondary;
                var q1=2*Dot(r,d);var q2=Dot(d,d);var factor=4*surface.Radius*surface.Radius;
                polynomial[0]=q0*q0-factor*Dot(radial,radial);polynomial[1]=2*q0*q1-2*factor*Dot(radial,dr);
                polynomial[2]=q1*q1+2*q0*q2-factor*Dot(dr,dr);polynomial[3]=2*q1*q2;polynomial[4]=q2*q2;degree=4;break;
            default:return AlgorithmStatus.Unsupported;
        }
        var max=0.0;for(var i=0;i<=degree;i++){if(!double.IsFinite(polynomial[i]))return AlgorithmStatus.NumericalFailure;max=Math.Max(max,Math.Abs(polynomial[i]));}
        if(max==0)
            return AddPoint(output,ref count,Add(p,Scale(d,lo)),lo,0,IntersectionVectorKind.Start)
                &&AddPoint(output,ref count,Add(p,Scale(d,hi)),hi,0,IntersectionVectorKind.End)?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
        Span<double> roots=stackalloc double[16];
        var status=BoundedPolynomialRoots.Find(polynomial[..(degree+1)],lo,hi,roots,out var n);
        if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<n;i++)
        {
            var t=roots[i];var point=Add(p,Scale(d,t));
            if(surface.Kind==SurfaceClass.Cone&&surface.Radius+surface.Secondary*Dot(Sub(point,surface.Origin),surface.Axis)<0)continue;
            if(AnalyticImplicitEvaluation.GeometricDeviation(surface,point,out var deviation)!=AlgorithmStatus.Success||deviation>DistanceTolerance)
                return AlgorithmStatus.NumericalFailure;
            if(AnalyticImplicitEvaluation.Evaluate(surface,point,1,out var jet)!=AlgorithmStatus.Success)return AlgorithmStatus.NumericalFailure;
            var kind=Math.Abs(Dot(Unit(jet.Gradient),d))<=1e-8?IntersectionVectorKind.Tangent:IntersectionVectorKind.Simple;
            if(!AddPoint(output,ref count,point,t,0,kind))return AlgorithmStatus.OutputTooSmall;
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Parameters(in AnalyticSurface surface,KernelVector3 point,out double u,out double v)
    {
        var r=Sub(point,surface.Origin);var x=Dot(r,surface.X);var y=Dot(r,surface.Y);var z=Dot(r,surface.Axis);
        u=surface.Kind==SurfaceClass.Plane?x:Math.Atan2(y,x);if(surface.Kind!=SurfaceClass.Plane&&u<0)u+=Math.Tau;
        v=surface.Kind switch {SurfaceClass.Plane=>y,SurfaceClass.Sphere=>Math.Atan2(z,Math.Sqrt(x*x+y*y)),
            SurfaceClass.Torus=>Math.Atan2(z,Math.Sqrt(x*x+y*y)-surface.Radius),_=>z};
        if(surface.Kind==SurfaceClass.Torus&&v>=Math.PI)v-=Math.Tau;
        return double.IsFinite(u)&&double.IsFinite(v)?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    internal static AlgorithmStatus AnalyticConic(in AnalyticSurface surface,in OperationIntersectionBranch conic,
        double lo,double hi,Span<OperationIntersectionPoint> output,out BufferCount count)
    {
        count=0;if(surface.Kind==SurfaceClass.Plane)return PlaneConic(surface,conic,lo,hi,output,out count);
        if(surface.Kind==SurfaceClass.Torus&&surface.Radius<=surface.Secondary)return AlgorithmStatus.Unsupported;
        Span<double> polynomial=stackalloc double[9];Span<double> roots=stackalloc double[16];
        for(var chart=0;chart<2;chart++)
        {
            var status=ConicPolynomial(surface,conic,chart!=0,polynomial,out var degree);
            if(status!=AlgorithmStatus.Success)return status;
            var zero=true;for(var k=0;k<=degree;k++)if(polynomial[k]!=0)zero=false;
            if(zero)
                return AddPoint(output,ref count,ConicPoint(conic,lo),lo,0,IntersectionVectorKind.Start)
                    &&AddPoint(output,ref count,ConicPoint(conic,hi),hi,0,IntersectionVectorKind.End)
                    ?AlgorithmStatus.Success:AlgorithmStatus.OutputTooSmall;
            status=BoundedPolynomialRoots.Find(polynomial[..(degree+1)],-1,1,roots,out var n);
            if(status!=AlgorithmStatus.Success)return status;
            for(var k=0;k<n;k++)
            {
                var angle=chart==0?2*Math.Atan(roots[k]):2*Math.Atan2(1,roots[k]);
                if(!PeriodicParameter(angle,lo,hi,out var t))continue;
                var point=ConicPoint(conic,t);
                if(surface.Kind==SurfaceClass.Cone&&surface.Radius+surface.Secondary*Dot(Sub(point,surface.Origin),surface.Axis)<0)continue;
                if(AnalyticImplicitEvaluation.GeometricDeviation(surface,point,out var deviation)!=AlgorithmStatus.Success||deviation>DistanceTolerance)
                    return AlgorithmStatus.NumericalFailure;
                if(AnalyticImplicitEvaluation.Evaluate(surface,point,1,out var jet)!=AlgorithmStatus.Success)return AlgorithmStatus.NumericalFailure;
                var tangent=Unit(Add(Scale(conic.Reference,-conic.Radius*Math.Sin(t)),Scale(Cross(conic.Axis,conic.Reference),conic.Minor*Math.Cos(t))));
                var kind=Math.Abs(Dot(Unit(jet.Gradient),tangent))<=1e-8?IntersectionVectorKind.Tangent:IntersectionVectorKind.Simple;
                if(!AddPoint(output,ref count,point,t,0,kind))return AlgorithmStatus.OutputTooSmall;
            }
        }
        Sort(output[..count]);return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus ConicPolynomial(in AnalyticSurface surface,in OperationIntersectionBranch conic,
        bool secondChart,Span<double> polynomial,out SplineDegree degree)
    {
        polynomial.Clear();degree=4;
        Span<KernelVector3> p=stackalloc KernelVector3[3];Span<KernelVector3> radial=stackalloc KernelVector3[3];
        var r=Sub(conic.Origin,surface.Origin);var x=Scale(conic.Reference,conic.Radius*(secondChart?-1:1));
        p[0]=Add(r,x);p[1]=Scale(Cross(conic.Axis,conic.Reference),2*conic.Minor);p[2]=Sub(r,x);
        Span<double> z=stackalloc double[3];Span<double> norm=stackalloc double[5];Span<double> radialNorm=stackalloc double[5];
        Span<double> w2=stackalloc double[5]{1,0,2,0,1};norm.Clear();radialNorm.Clear();
        for(var i=0;i<3;i++){z[i]=Dot(p[i],surface.Axis);radial[i]=Sub(p[i],Scale(surface.Axis,z[i]));}
        for(var i=0;i<3;i++)for(var j=0;j<3;j++){norm[i+j]+=Dot(p[i],p[j]);radialNorm[i+j]+=Dot(radial[i],radial[j]);}
        switch(surface.Kind)
        {
            case SurfaceClass.Sphere:
                for(var i=0;i<5;i++)polynomial[i]=norm[i]-surface.Radius*surface.Radius*w2[i];break;
            case SurfaceClass.Cylinder:
                for(var i=0;i<5;i++)polynomial[i]=radialNorm[i]-surface.Radius*surface.Radius*w2[i];break;
            case SurfaceClass.Cone:
                for(var i=0;i<3;i++)z[i]=surface.Secondary*z[i]+(i==1?0:surface.Radius);
                radialNorm.CopyTo(polynomial);
                for(var i=0;i<3;i++)for(var j=0;j<3;j++)polynomial[i+j]-=z[i]*z[j];break;
            case SurfaceClass.Torus:
                degree=8;var major2=surface.Radius*surface.Radius;
                for(var i=0;i<5;i++)norm[i]+=(major2-surface.Secondary*surface.Secondary)*w2[i];
                for(var i=0;i<5;i++)for(var j=0;j<5;j++)polynomial[i+j]+=norm[i]*norm[j]-4*major2*radialNorm[i]*w2[j];break;
            default:return AlgorithmStatus.Unsupported;
        }
        for(var i=0;i<=degree;i++)if(!double.IsFinite(polynomial[i]))return AlgorithmStatus.NumericalFailure;
        return AlgorithmStatus.Success;
    }

    internal static OperationIntersectionBranch Circle(KernelVector3 center,KernelVector3 normal,KernelVector3 reference,double radius,
        IntersectionCurveKind kind=IntersectionCurveKind.Simple)
        => new(){Class=CurveClass.Circle,Origin=center,Axis=Unit(normal),Reference=Unit(reference),Radius=radius,Minor=radius,Start=0,End=Math.Tau,Kind=kind};

    internal static OperationIntersectionBranch Line(KernelVector3 origin,KernelVector3 direction)
        =>new(){Class=CurveClass.Line,Origin=origin,Axis=Unit(direction),Start=-Math.BitIncrement(500.0),End=Math.BitIncrement(500.0),Kind=IntersectionCurveKind.Simple};

    internal static KernelVector3 Reference(KernelVector3 axis)
    {
        var seed=Math.Abs(axis.X)<.8?Vector(1,0,0):Vector(0,1,0);
        return Unit(Sub(seed,Scale(axis,Dot(seed,axis))));
    }

    internal static AlgorithmStatus Surfaces(in AnalyticSurface a,in AnalyticSurface b,
        Span<OperationIntersectionBranch> curves,out BufferCount nc,Span<KernelVector3> points,out BufferCount np)
    {
        nc=0;np=0;
        if(a.Kind==SurfaceClass.Plane)return PlaneSurface(a,b,curves,out nc,points,out np);
        if(b.Kind==SurfaceClass.Plane)return PlaneSurface(b,a,curves,out nc,points,out np);
        if(a.Kind==SurfaceClass.Sphere&&b.Kind==SurfaceClass.Sphere)
        {
            var delta=Sub(b.Origin,a.Origin);var distance=Norm(delta);
            if(!double.IsFinite(distance))return AlgorithmStatus.NumericalFailure;
            if(distance<=DistanceTolerance)
                return distance==0?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
            if(distance>a.Radius+b.Radius+DistanceTolerance||distance<Math.Abs(a.Radius-b.Radius)-DistanceTolerance)return AlgorithmStatus.Success;
            var normal=Scale(delta,1/distance);var h=(distance*distance+(a.Radius-b.Radius)*(a.Radius+b.Radius))/(2*distance);
            var center=Add(a.Origin,Scale(normal,h));var rr=(a.Radius-h)*(a.Radius+h);
            if(rr<=DistanceTolerance*DistanceTolerance){points[np++]=center;return AlgorithmStatus.Success;}
            curves[nc++]=Circle(center,normal,Reference(normal),Math.Sqrt(rr));return AlgorithmStatus.Success;
        }
        if(a.Kind==SurfaceClass.Cylinder&&b.Kind==SurfaceClass.Cylinder)
        {
            if(Norm(Cross(a.Axis,b.Axis))>0)return AlgorithmStatus.Unsupported;
            var delta=Sub(b.Origin,a.Origin);delta=Sub(delta,Scale(a.Axis,Dot(delta,a.Axis)));var distance=Norm(delta);
            if(distance<=DistanceTolerance)return distance==0?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
            if(distance>a.Radius+b.Radius+DistanceTolerance||distance<Math.Abs(a.Radius-b.Radius)-DistanceTolerance)return AlgorithmStatus.Success;
            var x=Scale(delta,1/distance);var y=Cross(a.Axis,x);var h=(distance*distance+(a.Radius-b.Radius)*(a.Radius+b.Radius))/(2*distance);
            var r2=(a.Radius-h)*(a.Radius+h);var center=Add(a.Origin,Scale(x,h));
            curves[nc++]=Line(Add(center,Scale(y,Math.Sqrt(Math.Max(0,r2)))),a.Axis);
            if(r2>DistanceTolerance*DistanceTolerance)curves[nc++]=Line(Sub(center,Scale(y,Math.Sqrt(r2))),a.Axis);
            else curves[0].Kind=IntersectionCurveKind.Tangent;
            return AlgorithmStatus.Success;
        }
        if(a.Kind==SurfaceClass.Sphere&&b.Kind==SurfaceClass.Cylinder)return SphereCylinder(a,b,curves,out nc);
        if(b.Kind==SurfaceClass.Sphere&&a.Kind==SurfaceClass.Cylinder)return SphereCylinder(b,a,curves,out nc);
        return AlgorithmStatus.Unsupported;
    }

    private static AlgorithmStatus SphereCylinder(in AnalyticSurface sphere,in AnalyticSurface cylinder,
        Span<OperationIntersectionBranch> curves,out BufferCount count)
    {
        count=0;var delta=Sub(sphere.Origin,cylinder.Origin);var z=Dot(delta,cylinder.Axis);
        if(Norm(Sub(delta,Scale(cylinder.Axis,z)))>DistanceTolerance)return AlgorithmStatus.Unsupported;
        var r2=(sphere.Radius-cylinder.Radius)*(sphere.Radius+cylinder.Radius);
        if(r2< -DistanceTolerance*DistanceTolerance)return AlgorithmStatus.Success;
        var h=Math.Sqrt(Math.Max(0,r2));
        curves[count++]=Circle(Add(sphere.Origin,Scale(cylinder.Axis,h)),cylinder.Axis,cylinder.X,cylinder.Radius,
            h<=DistanceTolerance?IntersectionCurveKind.Tangent:IntersectionCurveKind.Simple);
        if(h>DistanceTolerance)curves[count++]=Circle(Sub(sphere.Origin,Scale(cylinder.Axis,h)),cylinder.Axis,cylinder.X,cylinder.Radius);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus PlaneSurface(in AnalyticSurface plane,in AnalyticSurface surface,
        Span<OperationIntersectionBranch> curves,out BufferCount nc,Span<KernelVector3> points,out BufferCount np)
    {
        nc=0;np=0;var h=Dot(Sub(surface.Origin,plane.Origin),plane.Axis);var dot=Dot(plane.Axis,surface.Axis);
        if(surface.Kind==SurfaceClass.Plane)
        {
            var cross=Cross(plane.Axis,surface.Axis);var q=Dot(cross,cross);
            if(q==0&&Norm(cross)>0)return AlgorithmStatus.NumericalFailure;
            if(q==0)return AlgorithmStatus.Success;
            if(q<=AngularTolerance*AngularTolerance)return AlgorithmStatus.NumericalFailure;
            // Point nearest plane origin, expressed with local differences to avoid large-origin cancellation.
            var point=Add(plane.Origin,Scale(Cross(cross,plane.Axis),Dot(Sub(surface.Origin,plane.Origin),surface.Axis)/q));
            curves[nc++]=Line(point,cross);return AlgorithmStatus.Success;
        }
        if(surface.Kind==SurfaceClass.Sphere)
        {
            if(Math.Abs(h)>surface.Radius+DistanceTolerance)return AlgorithmStatus.Success;
            var center=Sub(surface.Origin,Scale(plane.Axis,h));var r2=(surface.Radius-h)*(surface.Radius+h);
            if(r2<=DistanceTolerance*DistanceTolerance){points[np++]=center;return AlgorithmStatus.Success;}
            curves[nc++]=Circle(center,plane.Axis,plane.X,Math.Sqrt(r2));return AlgorithmStatus.Success;
        }
        if(surface.Kind==SurfaceClass.Cylinder)
        {
            if(dot!=0&&Math.Abs(dot)<=AngularTolerance)return AlgorithmStatus.NumericalFailure;
            if(Math.Abs(dot)>AngularTolerance)
            {
                var center=Sub(surface.Origin,Scale(surface.Axis,h/dot));var projected=Sub(surface.Axis,Scale(plane.Axis,dot));
                var reference=Norm(projected)>AngularTolerance?Unit(projected):plane.X;
                var branch=Circle(center,plane.Axis,reference,surface.Radius);
                if(Math.Abs(dot)<1-1e-14){branch.Class=CurveClass.Ellipse;branch.Radius=surface.Radius/Math.Abs(dot);}
                curves[nc++]=branch;return AlgorithmStatus.Success;
            }
            if(Math.Abs(h)>surface.Radius+DistanceTolerance)return AlgorithmStatus.Success;
            var basePoint=Sub(surface.Origin,Scale(plane.Axis,h));var side=Unit(Cross(surface.Axis,plane.Axis));
            var r2=(surface.Radius-h)*(surface.Radius+h);var distance=Math.Sqrt(Math.Max(0,r2));
            curves[nc++]=Line(Add(basePoint,Scale(side,distance)),surface.Axis);
            if(distance>DistanceTolerance)curves[nc++]=Line(Sub(basePoint,Scale(side,distance)),surface.Axis);
            else curves[0].Kind=IntersectionCurveKind.Tangent;
            return AlgorithmStatus.Success;
        }
        if(surface.Kind==SurfaceClass.Cone&&Math.Abs(Math.Abs(dot)-1)<=AngularTolerance)
        {
            var z=-h/dot;var radius=surface.Radius+surface.Secondary*z;
            if(radius< -DistanceTolerance)return AlgorithmStatus.Success;
            var center=Add(surface.Origin,Scale(surface.Axis,z));
            if(radius<=DistanceTolerance){points[np++]=center;return AlgorithmStatus.Success;}
            curves[nc++]=Circle(center,plane.Axis,plane.X,radius);return AlgorithmStatus.Success;
        }
        if(surface.Kind==SurfaceClass.Torus&&surface.Radius>surface.Secondary)
        {
            if(Math.Abs(Math.Abs(dot)-1)<=AngularTolerance)
            {
                var z=-h/dot;if(Math.Abs(z)>surface.Secondary+DistanceTolerance)return AlgorithmStatus.Success;
                var center=Add(surface.Origin,Scale(surface.Axis,z));var radial=Math.Sqrt(Math.Max(0,(surface.Secondary-z)*(surface.Secondary+z)));
                curves[nc++]=Circle(center,plane.Axis,plane.X,surface.Radius+radial,radial<=DistanceTolerance?IntersectionCurveKind.Tangent:IntersectionCurveKind.Simple);
                if(radial>DistanceTolerance)curves[nc++]=Circle(center,plane.Axis,plane.X,surface.Radius-radial);
                return AlgorithmStatus.Success;
            }
            if(Math.Abs(dot)<=AngularTolerance&&Math.Abs(h)<=DistanceTolerance)
            {
                var radial=Unit(Cross(plane.Axis,surface.Axis));
                curves[nc++]=Circle(Add(surface.Origin,Scale(radial,surface.Radius)),plane.Axis,surface.Axis,surface.Secondary);
                curves[nc++]=Circle(Sub(surface.Origin,Scale(radial,surface.Radius)),plane.Axis,surface.Axis,surface.Secondary);
                return AlgorithmStatus.Success;
            }
        }
        return AlgorithmStatus.Unsupported;
    }
}
