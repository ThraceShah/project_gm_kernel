using ProjectGmKernel.Native.Computation;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Enumerates roots on a bounded interval by derivative isolation: between two
/// consecutive derivative roots a polynomial is monotone. Tangencies are tested
/// at every critical point, so no branch depends on a sampling grid or seed.
/// Degrees above 16 and unresolvable floating-point ranges fail explicitly.
/// </summary>
internal static class BoundedPolynomialRoots
{
    internal static AlgorithmStatus Find(ReadOnlySpan<double> coefficients,double lo,double hi,
        Span<double> roots,out BufferCount count)
    {
        count=0;if(coefficients.Length<1||coefficients.Length>17||!double.IsFinite(lo)||!double.IsFinite(hi)||lo>hi)return AlgorithmStatus.InvalidInput;
        // Normalize the interval before recursive differentiation. FMA Horner
        // translation avoids evaluating unscaled high powers of the original t.
        var center=lo/2+hi/2;var half=(hi-lo)/2;
        if(!double.IsFinite(half)||!(half>0))return AlgorithmStatus.NumericalFailure;
        Span<double> transformed=stackalloc double[17];transformed.Clear();
        var degree=coefficients.Length-1;
        while(degree>0&&coefficients[degree]==0)degree--;
        transformed[0]=coefficients[degree];var current=0;
        for(var k=degree-1;k>=0;k--)
        {
            transformed[current+1]=transformed[current]*half;
            for(var j=current;j>0;j--)transformed[j]=Math.FusedMultiplyAdd(transformed[j],center,transformed[j-1]*half);
            transformed[0]=Math.FusedMultiplyAdd(transformed[0],center,coefficients[k]);current++;
        }
        var scale=0.0;for(var i=0;i<=degree;i++){if(!double.IsFinite(transformed[i]))return AlgorithmStatus.NumericalFailure;scale=Math.Max(scale,Math.Abs(transformed[i]));}
        if(scale==0)return AlgorithmStatus.Singular;
        for(var i=0;i<=degree;i++)transformed[i]/=scale;
        var status=Isolate(transformed[..(degree+1)],roots,out count);
        if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<count;i++)roots[i]=Math.Clamp(Math.FusedMultiplyAdd(roots[i],half,center),lo,hi);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Isolate(ReadOnlySpan<double> p,Span<double> roots,out BufferCount count)
    {
        count=0;var degree=p.Length-1;while(degree>0&&p[degree]==0)degree--;
        if(degree==0)return p[0]==0?AlgorithmStatus.Singular:AlgorithmStatus.Success;
        if(degree==1)
        {
            var root=-p[0]/p[1];if(root>=-1&&root<=1){if(roots.IsEmpty)return AlgorithmStatus.OutputTooSmall;roots[count++]=root;}
            return AlgorithmStatus.Success;
        }
        Span<double> derivative=stackalloc double[16];Span<double> critical=stackalloc double[16];
        for(var i=1;i<=degree;i++)derivative[i-1]=p[i]*i;
        var status=Isolate(derivative[..degree],critical,out var n);if(status!=AlgorithmStatus.Success)return status;
        var previous=-1.0;var previousValue=Value(p,previous);var previousZero=NearZero(p,previous,previousValue);
        if(previousZero&&!Append(roots,ref count,previous))return AlgorithmStatus.OutputTooSmall;
        for(var i=0;i<=n;i++)
        {
            var end=i==n?1:critical[i];var value=Value(p,end);var zero=NearZero(p,end,value);
            // A rounded critical value cannot distinguish an exact tangent
            // from a close pair of roots. Report uncertainty rather than
            // silently merging that pair or certifying an empty interval.
            if(i<n&&zero&&value!=0)return AlgorithmStatus.NumericalFailure;
            if(!previousZero&&!zero&&Math.Sign(previousValue)!=Math.Sign(value))
            {
                var a=previous;var b=end;var fa=previousValue;
                for(var j=0;j<80;j++)
                {
                    var mid=a/2+b/2;if(mid==a||mid==b)break;var fm=Value(p,mid);
                    if(fm==0){a=b=mid;break;}
                    if(Math.Sign(fm)==Math.Sign(fa)){a=mid;fa=fm;}else b=mid;
                }
                if(!Append(roots,ref count,a/2+b/2))return AlgorithmStatus.OutputTooSmall;
            }
            if(zero&&!Append(roots,ref count,end))return AlgorithmStatus.OutputTooSmall;
            previous=end;previousValue=value;previousZero=zero;
        }
        return AlgorithmStatus.Success;
    }
    private static double Value(ReadOnlySpan<double> p,double t)
    {var value=p[^1];for(var i=p.Length-2;i>=0;i--)value=Math.FusedMultiplyAdd(value,t,p[i]);return value;}
    private static bool NearZero(ReadOnlySpan<double> p,double t,double value)
    {var bound=Math.Abs(p[^1]);for(var i=p.Length-2;i>=0;i--)bound=bound*Math.Abs(t)+Math.Abs(p[i]);return Math.Abs(value)<=32*2.2204460492503131e-16*bound;}
    private static bool Append(Span<double> roots,ref BufferCount count,double root)
    {if(count>0&&Math.Abs(roots[count-1]-root)<=8*2.2204460492503131e-16)return true;if(count==roots.Length)return false;roots[count++]=root;return true;}
}
