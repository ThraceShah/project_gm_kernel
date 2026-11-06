using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

// Evaluate the homogeneous plane equation before Cartesian projection.
// Two-component arithmetic keeps a small plane residual from being rounded
// away when the two full world-space positions nearly coincide.
internal static class SplinePlaneResidual
{
    internal static bool TryEvaluate(in BSurfaceView surface,in AnalyticSurface plane,double u,double v,out double value)
    {
        var found=Evaluate(surface,plane,u,v,out var wide);value=wide.Value;return found&&double.IsFinite(value);
    }

    internal static bool TryCorrectDifference(in BSurfaceView surface,in AnalyticSurface plane,double u,double v,
        double planeU,double planeV,bool splineFirst,in KernelVector3 original,out KernelVector3 corrected)
    {
        corrected=original;if(!Evaluate(surface,plane,u,v,out var projected))return false;
        var normal=plane.Axis;
        projected-=Dot(normal,plane.X)*planeU+Dot(normal,plane.Y)*planeV;
        if(!splineFirst)projected=-projected;
        var correction=(projected-Dot(normal,original))/Dot(normal,normal);
        corrected=new(){X=(new Wide(original.X)+correction*normal.X).Value,Y=(new Wide(original.Y)+correction*normal.Y).Value,Z=(new Wide(original.Z)+correction*normal.Z).Value};
        return double.IsFinite(corrected.X)&&double.IsFinite(corrected.Y)&&double.IsFinite(corrected.Z);
    }

    private static bool Evaluate(in BSurfaceView surface,in AnalyticSurface plane,double u,double v,out Wide value)
    {
        value=default;
        if(plane.Kind!=SurfaceClass.Plane||surface.UDegree>16||surface.VDegree>16||!BSurfaceEvaluation.IsInDomain(surface,u,v))return false;
        u=BSurfaceEvaluation.WrapPeriodic(u,surface.UStart,surface.UEnd,surface.UPeriodic);
        v=BSurfaceEvaluation.WrapPeriodic(v,surface.VStart,surface.VEnd,surface.VPeriodic);
        var p=surface.UDegree;var q=surface.VDegree;
        var spanU=BSurfaceEvaluation.KnotSpan(surface.UKnots,p,surface.UVertexCount,u);
        var spanV=BSurfaceEvaluation.KnotSpan(surface.VKnots,q,surface.VVertexCount,v);
        var exponent=0;
        if(surface.Rational)
        {
            var maximum=0.0;
            for(var i=0;i<=p;i++)for(var j=0;j<=q;j++)
            {var at=((spanU-p+i)*surface.VVertexCount+spanV-q+j)*surface.Dimension;maximum=Math.Max(maximum,Math.Abs(surface.Vertices[at+3]));}
            if(!(maximum>0)||!double.IsFinite(maximum))return false;exponent=Math.ILogB(maximum);
        }
        Span<Wide> work=stackalloc Wide[34],rows=stackalloc Wide[34];
        for(var j=0;j<=q;j++)
        {
            for(var i=0;i<=p;i++)
            {
                var at=((spanU-p+i)*surface.VVertexCount+spanV-q+j)*surface.Dimension;
                var w=surface.Rational?Math.ScaleB(surface.Vertices[at+3],-exponent):1;
                var x=Math.ScaleB(surface.Vertices[at],-exponent);var y=Math.ScaleB(surface.Vertices[at+1],-exponent);var z=Math.ScaleB(surface.Vertices[at+2],-exponent);
                if(!double.IsFinite(w)||!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z)
                    ||surface.Rational&&w==0&&surface.Vertices[at+3]!=0||x==0&&surface.Vertices[at]!=0||y==0&&surface.Vertices[at+1]!=0||z==0&&surface.Vertices[at+2]!=0)return false;
                work[2*i]=(new Wide(x)-new Wide(plane.Origin.X)*w)*plane.Axis.X
                    +(new Wide(y)-new Wide(plane.Origin.Y)*w)*plane.Axis.Y+(new Wide(z)-new Wide(plane.Origin.Z)*w)*plane.Axis.Z;
                work[2*i+1]=new(w);
            }
            DeBoor(work,surface.UKnots,spanU,p,u);rows[2*j]=work[2*p];rows[2*j+1]=work[2*p+1];
        }
        DeBoor(rows,surface.VKnots,spanV,q,v);value=rows[2*q]/rows[2*q+1];return double.IsFinite(value.Value);
    }

    private static void DeBoor(Span<Wide> work,ReadOnlySpan<double> knots,KnotIndex span,SplineDegree degree,double t)
    {
        for(var r=1;r<=degree;r++)for(var j=degree;j>=r;j--)
        {
            var at=span-degree+j;var alpha=(new Wide(t)-new Wide(knots[at]))/(new Wide(knots[at+degree-r+1])-new Wide(knots[at]));
            for(var d=0;d<2;d++){var left=work[2*j+d-2];work[2*j+d]=left+alpha*(work[2*j+d]-left);}
        }
    }
    private static Wide Dot(in KernelVector3 a,in KernelVector3 b)
        =>new Wide(a.X)*b.X+new Wide(a.Y)*b.Y+new Wide(a.Z)*b.Z;

    private readonly struct Wide(double high,double low=0)
    {
        private readonly double hi=high,lo=low;
        internal double Value=>hi+lo;
        public static Wide operator +(Wide a,Wide b)
        {
            var s=a.hi+b.hi;var v=s-a.hi;var e=(a.hi-(s-v))+(b.hi-v)+a.lo+b.lo;var h=s+e;return new(h,e-(h-s));
        }
        public static Wide operator -(Wide a)=>new(-a.hi,-a.lo);
        public static Wide operator -(Wide a,Wide b)=>a+-b;
        public static Wide operator *(Wide a,double b)=>a*new Wide(b);
        public static Wide operator *(Wide a,Wide b)
        {
            var p=a.hi*b.hi;var e=Math.FusedMultiplyAdd(a.hi,b.hi,-p)+a.hi*b.lo+a.lo*b.hi+a.lo*b.lo;var h=p+e;return new(h,e-(h-p));
        }
        public static Wide operator /(Wide a,Wide b)
        {
            var q=a.hi/b.hi;var r=a-b*q;return new Wide(q)+new Wide((r.hi+r.lo)/b.hi);
        }
    }
}
