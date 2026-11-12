using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Bounds;

// Exact homogeneous substitution S(U(t)/W(t),V(t)/W(t)). These are temporary
// interval coefficients in the original native t; no spatial curve is fitted
// or installed in the model. A whole finite UV span must lie in one cell.
internal static class SpCurveBezier
{
    internal static bool HasC1Joins(ReadOnlySpan<double> knots,SplineDegree degree,BufferCount vertices)
    {
        for(var i=degree+1;i<vertices;)
        {
            var j=i+1;while(j<vertices&&knots[j]==knots[i])j++;
            if(j-i>=degree)return false;i=j;
        }
        return true;
    }

    internal static AlgorithmStatus FromSpline(in BSurfaceView surface,in BCurveView curve,KnotIndex knot,double first,double last,
        Span<IntervalNumber> output,out SplineDegree degree)
    {
        degree=0;Span<IntervalNumber> parameter=stackalloc IntervalNumber[4*17],restricted=stackalloc IntervalNumber[4*17];
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        var status=Extract(curve,knot,parameter);if(status!=AlgorithmStatus.Success)return status;
        var native=new SurfaceParameterBox(curve.Knots[knot],curve.Knots[knot+1],0,1);var finite=new SurfaceParameterBox(first,last,0,1);
        status=BezierSurfaceBounds.Restrict(new(parameter[..(4*(curve.Degree+1))],curve.Degree,0,native),finite,restricted);if(status!=AlgorithmStatus.Success)return status;
        for(var u=surface.UDegree;u<surface.UVertexCount;u++)for(var v=surface.VDegree;v<surface.VVertexCount;v++)
        {
            var domain=new SurfaceParameterBox(surface.UKnots[u],surface.UKnots[u+1],surface.VKnots[v],surface.VKnots[v+1]);
            if(!(domain.U1>domain.U0&&domain.V1>domain.V0))continue;
            status=BezierSurfaceBounds.Extract(surface,u,v,poles);if(status!=AlgorithmStatus.Success)return status;
            status=Compose(new(poles[..(4*(surface.UDegree+1)*(surface.VDegree+1))],surface.UDegree,surface.VDegree,domain),restricted,curve.Degree,output,out degree);
            if(status!=AlgorithmStatus.Unsupported)return status;
        }
        return AlgorithmStatus.Unsupported;
    }

    internal static AlgorithmStatus Extract(in BCurveView curve,KnotIndex knot,Span<IntervalNumber> output)
    {
        if(curve.Degree is <1 or >16||curve.Dimension!=(curve.Rational?3:2))return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[4*17];
        for(var i=0;i<=curve.Degree;i++)
        {
            var at=(knot-curve.Degree+i)*curve.Dimension;
            poles[4*i]=curve.Vertices[at];poles[4*i+1]=curve.Vertices[at+1];poles[4*i+2]=0;
            poles[4*i+3]=curve.Rational?curve.Vertices[at+2]:1;
            if(poles[4*i+3].Lower<=0)return AlgorithmStatus.Unsupported;
        }
        return BezierSurfaceBounds.ExtractCurve(poles,curve.Degree,curve.Knots,knot,output);
    }

    internal static AlgorithmStatus Compose(in BezierSurfacePatch surface,ReadOnlySpan<IntervalNumber> uv,SplineDegree n,
        Span<IntervalNumber> output,out SplineDegree degree)
    {
        var p=surface.UDegree;var q=surface.VDegree;degree=n*(p+q);
        if(n<1||degree is <1 or >16)return AlgorithmStatus.Unsupported;
        if(uv.Length<4*(n+1)||output.Length<4*(degree+1))return AlgorithmStatus.WorkspaceTooSmall;
        var du=new IntervalNumber(surface.Domain.U1)-surface.Domain.U0;var dv=new IntervalNumber(surface.Domain.V1)-surface.Domain.V0;
        if(du.Lower<=0||dv.Lower<=0)return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> factors=stackalloc IntervalNumber[4*17],current=stackalloc IntervalNumber[17],next=stackalloc IntervalNumber[17];
        for(var i=0;i<=n;i++)
        {
            var u=uv[4*i];var v=uv[4*i+1];var w=uv[4*i+3];if(w.Lower<=0)return AlgorithmStatus.Unsupported;
            factors[i]=(u-surface.Domain.U0*w)/du;factors[17+i]=(surface.Domain.U1*w-u)/du;
            factors[34+i]=(v-surface.Domain.V0*w)/dv;factors[51+i]=(surface.Domain.V1*w-v)/dv;
            // Nonnegative homogeneous Bernstein coordinates prove complete
            // UV coverage by this native surface cell, including its ends.
            if(factors[i].Lower<0||factors[17+i].Lower<0||factors[34+i].Lower<0||factors[51+i].Lower<0)return AlgorithmStatus.Unsupported;
        }
        output[..(4*(degree+1))].Clear();
        for(var i=0;i<=p;i++)for(var j=0;j<=q;j++)
        {
            current.Clear();current[0]=1;var used=0;
            for(var factor=0;factor<p+q;factor++)
            {
                var kind=factor<p?(factor<i?0:1):(factor-p<j?2:3);var coefficients=factors.Slice(17*kind,n+1);
                for(var k=0;k<=used+n;k++)
                {
                    IntervalNumber value=0;
                    for(var r=Math.Max(0,k-n);r<=Math.Min(used,k);r++)
                        value+=current[r]*coefficients[k-r]*(new IntervalNumber(Choose(used,r))*Choose(n,k-r)/Choose(used+n,k));
                    next[k]=value;
                }
                used+=n;next[..(used+1)].CopyTo(current);
            }
            var multiplier=Choose(p,i)*Choose(q,j);
            for(var k=0;k<=degree;k++)for(var d=0;d<4;d++)
                output[4*k+d]+=surface.Poles[4*(i*(q+1)+j)+d]*current[k]*multiplier;
        }
        for(var k=0;k<=degree;k++)if(!output[4*k+3].Finite||output[4*k+3].Lower<=0)return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    private static double Choose(SplineDegree degree,BufferOffset index)
    {
        var value=1.0;for(var i=1;i<=Math.Min(index,degree-index);i++)value=value*(degree-i+1)/i;return value;
    }
}
