using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Bounds;

internal readonly struct SurfaceParameterBox(double u0, double u1, double v0, double v1)
{
    internal readonly double U0 = u0, U1 = u1, V0 = v0, V1 = v1;
    internal bool Valid => double.IsFinite(U0) && double.IsFinite(U1) && double.IsFinite(V0) && double.IsFinite(V1) && U1 >= U0 && V1 >= V0;
}

/// <summary>One homogeneous Bezier span. Interval poles enclose extraction rounding.</summary>
internal readonly ref struct BezierSurfacePatch(ReadOnlySpan<IntervalNumber> poles, SplineDegree uDegree,
    SplineDegree vDegree, SurfaceParameterBox domain)
{
    internal ReadOnlySpan<IntervalNumber> Poles { get; } = poles;
    internal SplineDegree UDegree { get; } = uDegree;
    internal SplineDegree VDegree { get; } = vDegree;
    internal SurfaceParameterBox Domain { get; } = domain;
}

internal static class BezierSurfaceBounds
{
    internal const SplineDegree MaxDegree = 16;
    internal const BufferCount MaxScalars = 4 * 17 * 17;

    // Exact clamped Bezier nets or symbolic de Boor with Bernstein-polynomial
    // coefficients. No interpolation or sampled bounds.
    internal static AlgorithmStatus Extract(in BSurfaceView surface, KnotIndex uSpan, KnotIndex vSpan,
        Span<IntervalNumber> poles)
    {
        var p = surface.UDegree; var q = surface.VDegree;
        if (p < 1 || q < 1 || p > MaxDegree || q > MaxDegree || surface.Dimension != (surface.Rational ? 4 : 3)
            || uSpan < p || uSpan >= surface.UVertexCount || vSpan < q || vSpan >= surface.VVertexCount)
            return AlgorithmStatus.Unsupported;
        if (poles.Length < 4 * (p + 1) * (q + 1)) return AlgorithmStatus.WorkspaceTooSmall;
        Span<IntervalNumber> layers = stackalloc IntervalNumber[MaxScalars];
        Span<IntervalNumber> input = stackalloc IntervalNumber[4 * 17];
        Span<IntervalNumber> result = stackalloc IntervalNumber[4 * 17];
        var maxWeight = 1.0;
        if (surface.Rational)
        {
            maxWeight = 0;
            for (var u = uSpan - p; u <= uSpan; u++) for (var v = vSpan - q; v <= vSpan; v++)
                maxWeight = Math.Max(maxWeight, surface.Vertices[(u * surface.VVertexCount + v) * 4 + 3]);
        }
        if (!(maxWeight > 0) || !double.IsFinite(maxWeight)) return AlgorithmStatus.InvalidInput;
        var exponent = Math.ILogB(maxWeight);
        // A complete end-clamped single span already is its Bezier control
        // net. Check the complete knot vectors before bypassing extraction.
        if(surface.UVertexCount==p+1&&surface.VVertexCount==q+1
            &&ClampedBezierKnots(surface.UKnots,p)&&ClampedBezierKnots(surface.VKnots,q))
        {
            for(var u=0;u<=p;u++)for(var v=0;v<=q;v++)for(var d=0;d<4;d++)
            {
                var value=d==3&&!surface.Rational?1:surface.Vertices[(u*(q+1)+v)*surface.Dimension+d];
                if(!ScaleHomogeneous(value,exponent,out poles[4*(u*(q+1)+v)+d]))return AlgorithmStatus.NumericalFailure;
            }
            return AlgorithmStatus.Success;
        }
        for (var v = 0; v <= q; v++)
        {
            for (var u = 0; u <= p; u++)
            {
                var at = ((uSpan - p + u) * surface.VVertexCount + vSpan - q + v) * surface.Dimension;
                for (var d = 0; d < 4; d++)
                {
                    var value = d == 3 && !surface.Rational ? 1 : surface.Vertices[at + d];
                    if(!ScaleHomogeneous(value,exponent,out input[4*u+d]))return AlgorithmStatus.NumericalFailure;
                }
            }
            var status = ExtractCurve(input, p, surface.UKnots, uSpan, result);
            if (status != AlgorithmStatus.Success) return status;
            for (var u = 0; u <= p; u++) for (var d = 0; d < 4; d++) layers[4 * (u * (q + 1) + v) + d] = result[4 * u + d];
        }
        for (var u = 0; u <= p; u++)
        {
            layers.Slice(4 * u * (q + 1), 4 * (q + 1)).CopyTo(input);
            var status = ExtractCurve(input, q, surface.VKnots, vSpan, result);
            if (status != AlgorithmStatus.Success) return status;
            result[..(4 * (q + 1))].CopyTo(poles[(4 * u * (q + 1))..]);
        }
        return AlgorithmStatus.Success;
    }

    private static bool ClampedBezierKnots(ReadOnlySpan<double> knots,SplineDegree degree)
    {
        if(knots.Length!=2*(degree+1))return false;
        var first=knots[0];var last=knots[^1];if(!double.IsFinite(first)||!double.IsFinite(last)||!(last>first))return false;
        for(var i=0;i<=degree;i++)if(knots[i]!=first||knots[degree+1+i]!=last)return false;
        return true;
    }

    private static bool ScaleHomogeneous(double value,BufferOffset exponent,out IntervalNumber scaled)
    {
        var result=Math.ScaleB(value,-exponent);scaled=result;
        if(!double.IsFinite(result)||value!=0&&result==0)return false;
        // Power-of-two scaling is exact except when nonzero subnormal
        // rounding loses bits. Preserve an outward enclosure there too.
        if(double.IsSubnormal(result)&&Math.ScaleB(result,exponent)!=value)scaled=new(Math.BitDecrement(result),Math.BitIncrement(result));
        return true;
    }

    internal static AlgorithmStatus ExtractCurve(ReadOnlySpan<IntervalNumber> input, SplineDegree degree,
        ReadOnlySpan<double> knots, KnotIndex span, Span<IntervalNumber> output)
    {
        var a = knots[span]; var b = knots[span + 1];
        if (!(b > a)) return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> work = stackalloc IntervalNumber[4 * 17 * 17]; work.Clear();
        var stride = 4 * (degree + 1);
        for (var j = 0; j <= degree; j++) for (var d = 0; d < 4; d++) work[j * stride + d] = input[4 * j + d];
        for (var r = 1; r <= degree; r++) for (var j = degree; j >= r; j--)
        {
            var index = span - degree + j;
            var denominator = new IntervalNumber(knots[index + degree - r + 1]) - knots[index];
            if (denominator.Lower <= 0) return AlgorithmStatus.NumericalFailure;
            var alpha0 = (new IntervalNumber(a) - knots[index]) / denominator;
            var alpha1 = (new IntervalNumber(b) - knots[index]) / denominator;
            for (var k = r; k >= 0; k--) for (var d = 0; d < 4; d++)
            {
                IntervalNumber value = 0;
                var fraction = new IntervalNumber(k) / r;
                if (k < r) value += (1 - fraction) * ((1 - alpha0) * work[(j - 1) * stride + 4 * k + d] + alpha0 * work[j * stride + 4 * k + d]);
                if (k > 0) value += fraction * ((1 - alpha1) * work[(j - 1) * stride + 4 * (k - 1) + d] + alpha1 * work[j * stride + 4 * (k - 1) + d]);
                work[j * stride + 4 * k + d] = value;
            }
        }
        work.Slice(degree * stride, stride).CopyTo(output); return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Restrict(in BezierSurfacePatch patch, in SurfaceParameterBox box, Span<IntervalNumber> poles)
    {
        if (!box.Valid || !(patch.Domain.U1 > patch.Domain.U0) || !(patch.Domain.V1 > patch.Domain.V0)) return AlgorithmStatus.InvalidInput;
        var p = patch.UDegree; var q = patch.VDegree;
        var size = 4 * (p + 1) * (q + 1); if (poles.Length < size) return AlgorithmStatus.WorkspaceTooSmall;
        var u0 = Normalize(box.U0, patch.Domain.U0, patch.Domain.U1);
        var u1 = Normalize(box.U1, patch.Domain.U0, patch.Domain.U1);
        var v0 = Normalize(box.V0, patch.Domain.V0, patch.Domain.V1);
        var v1 = Normalize(box.V1, patch.Domain.V0, patch.Domain.V1);
        Span<IntervalNumber> layer = stackalloc IntervalNumber[Math.Min(MaxScalars,size)];
        var width=4*(Math.Max(p,q)+1);
        Span<IntervalNumber> input = stackalloc IntervalNumber[Math.Min(4*17,width)]; Span<IntervalNumber> result = stackalloc IntervalNumber[Math.Min(4*17,width)];
        for (var v = 0; v <= q; v++)
        {
            for (var u = 0; u <= p; u++) for (var d = 0; d < 4; d++) input[4 * u + d] = patch.Poles[4 * (u * (q + 1) + v) + d];
            RestrictCurve(input, p, u0, u1, result);
            for (var u = 0; u <= p; u++) for (var d = 0; d < 4; d++) layer[4 * (u * (q + 1) + v) + d] = result[4 * u + d];
        }
        for (var u = 0; u <= p; u++)
        {
            layer.Slice(4 * u * (q + 1), 4 * (q + 1)).CopyTo(input);
            RestrictCurve(input, q, v0, v1, result); result[..(4 * (q + 1))].CopyTo(poles[(4 * u * (q + 1))..]);
        }
        return AlgorithmStatus.Success;
    }

    private static IntervalNumber Normalize(double parameter, double first, double last)
        => parameter == first ? 0 : parameter == last ? 1 : (new IntervalNumber(parameter) - first) / (new IntervalNumber(last) - first);

    private static void RestrictCurve(ReadOnlySpan<IntervalNumber> input, SplineDegree degree, IntervalNumber a,
        IntervalNumber b, Span<IntervalNumber> output)
    {
        Span<IntervalNumber> work = stackalloc IntervalNumber[Math.Min(4*17,4*(degree+1))];
        // Blossom evaluation: kth restricted pole has degree-k copies of a
        // and k copies of b. This also supports tiny certification extensions.
        for (var k = 0; k <= degree; k++)
        {
            input[..(4 * (degree + 1))].CopyTo(work);
            for (var r = 0; r < degree; r++)
            {
                var t = r < degree - k ? a : b;
                for (var j = 0; j < degree - r; j++) for (var d = 0; d < 4; d++) work[4 * j + d] = (1 - t) * work[4 * j + d] + t * work[4 * (j + 1) + d];
            }
            work[..4].CopyTo(output[(4 * k)..]);
        }
    }

    /// <summary>XYZ, d/du and d/dv intervals; output layout is component-major.</summary>
    internal static AlgorithmStatus Bound(in BezierSurfacePatch patch, in SurfaceParameterBox box, Span<IntervalNumber> output)
        =>Bound(patch,box,output,default);

    private static AlgorithmStatus Bound(in BezierSurfacePatch patch,in SurfaceParameterBox box,Span<IntervalNumber> output,Span<IntervalNumber> homogeneous)
    {
        if (output.Length < 9) return AlgorithmStatus.WorkspaceTooSmall;
        Span<IntervalNumber> poles = stackalloc IntervalNumber[Math.Min(MaxScalars,4*(patch.UDegree+1)*(patch.VDegree+1))];
        var status = Restrict(patch, box, poles); if (status != AlgorithmStatus.Success) return status;
        var p = patch.UDegree; var q = patch.VDegree;
        Span<IntervalNumber> h = stackalloc IntervalNumber[4]; Span<IntervalNumber> hu = stackalloc IntervalNumber[4]; Span<IntervalNumber> hv = stackalloc IntervalNumber[4];
        for (var d = 0; d < 4; d++)
        {
            h[d] = poles[d];
            for (var i = 1; i < (p + 1) * (q + 1); i++) h[d] = IntervalNumber.Hull(h[d], poles[4 * i + d]);
        }
        HomogeneousDerivativeHull(patch, box, true, hu);
        HomogeneousDerivativeHull(patch, box, false, hv);
        if(!homogeneous.IsEmpty){h.CopyTo(homogeneous);hv.CopyTo(homogeneous[4..]);hu.CopyTo(homogeneous[12..]);}
        if (!(h[3].Lower > 0)) return AlgorithmStatus.NumericalFailure;
        for (var d = 0; d < 3; d++)
        {
            // A rational surface lies in the convex hull of its Cartesian
            // poles. This avoids independent numerator/weight overestimation.
            var value = poles[d] / poles[3];
            for (var i = 1; i < (p + 1) * (q + 1); i++) value = IntervalNumber.Hull(value, poles[4 * i + d] / poles[4 * i + 3]);
            output[d] = value;
            output[3 + d] = (hu[d] - value * hu[3]) / h[3];
            output[6 + d] = (hv[d] - value * hv[3]) / h[3];
            if (!value.Finite) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    private static void HomogeneousDerivativeHull(in BezierSurfacePatch patch, in SurfaceParameterBox box,
        bool alongU, Span<IntervalNumber> hull)
    {
        if ((alongU ? patch.UDegree : patch.VDegree) == 0) { hull.Clear(); return; }
        var p = patch.UDegree; var q = patch.VDegree; var dp = alongU ? p - 1 : p; var dq = alongU ? q : q - 1;
        var size=Math.Min(MaxScalars,4*(dp+1)*(dq+1));
        Span<IntervalNumber> derivatives = stackalloc IntervalNumber[size];
        Span<IntervalNumber> restricted = stackalloc IntervalNumber[size];
        var scale = alongU ? p / (new IntervalNumber(patch.Domain.U1) - patch.Domain.U0) : q / (new IntervalNumber(patch.Domain.V1) - patch.Domain.V0);
        for (var u = 0; u <= dp; u++) for (var v = 0; v <= dq; v++) for (var d = 0; d < 4; d++)
        {
            var at = 4 * (u * (q + 1) + v) + d;
            derivatives[4 * (u * (dq + 1) + v) + d] = scale * (patch.Poles[at + (alongU ? 4 * (q + 1) : 4)] - patch.Poles[at]);
        }
        var derivative = new BezierSurfacePatch(derivatives[..(4 * (dp + 1) * (dq + 1))], dp, dq, patch.Domain);
        Restrict(derivative, box, restricted);
        for (var d = 0; d < 4; d++)
        {
            hull[d] = restricted[d];
            for (var i = 1; i < (dp + 1) * (dq + 1); i++) hull[d] = IntervalNumber.Hull(hull[d], restricted[4 * i + d]);
        }
    }

    /// <summary>XYZ, Su, Sv, Suu, Suv, Svv, each three consecutive components.</summary>
    internal static AlgorithmStatus BoundSecond(in BezierSurfacePatch patch,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        if(output.Length<18)return AlgorithmStatus.WorkspaceTooSmall;
        Span<IntervalNumber> jets=stackalloc IntervalNumber[27];var status=BoundRectangularSecond(patch,box,jets);if(status!=AlgorithmStatus.Success)return status;
        jets[..3].CopyTo(output);jets.Slice(9,3).CopyTo(output[3..]);jets.Slice(3,3).CopyTo(output[6..]);jets.Slice(18,3).CopyTo(output[9..]);jets.Slice(12,3).CopyTo(output[12..]);jets.Slice(6,3).CopyTo(output[15..]);
        return AlgorithmStatus.Success;
    }

    // Rectangular jets (i,j), 0<=i,j<=2, u-major. Mixed order three
    // is needed to bound the exact linear extension of a rational surface.
    internal static AlgorithmStatus BoundRectangularSecond(in BezierSurfacePatch patch,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        if(output.Length<27)return AlgorithmStatus.WorkspaceTooSmall;
        Span<IntervalNumber> first=stackalloc IntervalNumber[9],homogeneous=stackalloc IntervalNumber[36];var status=Bound(patch,box,first,homogeneous);if(status!=AlgorithmStatus.Success)return status;
        for(var i=0;i<=2;i++)for(var j=0;j<=2;j++)
        {if(i+j<=1)continue;HomogeneousJetHull(patch,box,i,j,homogeneous.Slice(4*(3*i+j),4));}
        var weight=homogeneous[3];if(weight.Lower<=0)return AlgorithmStatus.NumericalFailure;
        for(var i=0;i<=2;i++)for(var j=0;j<=2;j++)for(var d=0;d<3;d++)
        {
            var at=3*(3*i+j)+d;
            if(i==0&&j==0){output[at]=first[d];continue;}
            if(i==1&&j==0){output[at]=first[3+d];continue;}
            if(i==0&&j==1){output[at]=first[6+d];continue;}
            var value=homogeneous[4*(3*i+j)+d];
            for(var a=0;a<=i;a++)for(var b=0;b<=j;b++)
            {if(a==i&&b==j)continue;var coefficient=(i==2&&a==1?2:1)*(j==2&&b==1?2:1);value-=coefficient*output[3*(3*a+b)+d]*homogeneous[4*(3*(i-a)+(j-b))+3];}
            output[at]=value/weight;if(!output[at].Finite)return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    // The source B-surface certifies polynomial XYZ control vertices. Its
    // homogeneous weight is identically one; extraction rounding in the
    // unused fourth channel is not a varying rational denominator.
    internal static AlgorithmStatus BoundPolynomialRectangularSecond(in BezierSurfacePatch patch,in SurfaceParameterBox box,Span<IntervalNumber> output)
    {
        if(output.Length<27)return AlgorithmStatus.WorkspaceTooSmall;
        var size=Math.Min(MaxScalars,4*(patch.UDegree+1)*(patch.VDegree+1));
        Span<IntervalNumber> restricted=stackalloc IntervalNumber[size],hull=stackalloc IntervalNumber[4];
        var status=Restrict(patch,box,restricted);if(status!=AlgorithmStatus.Success)return status;
        for(var axis=0;axis<3;axis++)
        {
            var value=restricted[axis];for(var i=1;i<(patch.UDegree+1)*(patch.VDegree+1);i++)value=IntervalNumber.Hull(value,restricted[4*i+axis]);output[axis]=value;
        }
        for(var u=0;u<=2;u++)for(var v=0;v<=2;v++)
        {
            if(u+v==0)continue;HomogeneousJetHull(patch,box,u,v,hull);
            for(var axis=0;axis<3;axis++){var value=hull[axis];if(!value.Finite)return AlgorithmStatus.NumericalFailure;output[3*(3*u+v)+axis]=value;}
        }
        return output[0].Finite&&output[1].Finite&&output[2].Finite?AlgorithmStatus.Success:AlgorithmStatus.NumericalFailure;
    }

    private static void HomogeneousJetHull(in BezierSurfacePatch patch,in SurfaceParameterBox box,DerivativeOrder uOrder,DerivativeOrder vOrder,Span<IntervalNumber> hull)
    {
        if(uOrder>patch.UDegree||vOrder>patch.VDegree){hull.Clear();return;}
        var p=patch.UDegree;var q=patch.VDegree;var size=Math.Min(MaxScalars,4*(p+1)*(q+1));
        Span<IntervalNumber> a=stackalloc IntervalNumber[size],b=stackalloc IntervalNumber[size];patch.Poles[..(4*(p+1)*(q+1))].CopyTo(a);
        for(var axis=0;axis<2;axis++)for(var k=0;k<(axis==0?uOrder:vOrder);k++)
        {
            var np=p-(axis==0?1:0);var nq=q-(axis==1?1:0);var scale=axis==0?p/(new IntervalNumber(patch.Domain.U1)-patch.Domain.U0):q/(new IntervalNumber(patch.Domain.V1)-patch.Domain.V0);
            for(var u=0;u<=np;u++)for(var v=0;v<=nq;v++)for(var d=0;d<4;d++)
            {var at=4*(u*(q+1)+v)+d;b[4*(u*(nq+1)+v)+d]=scale*(a[at+(axis==0?4*(q+1):4)]-a[at]);}
            b[..(4*(np+1)*(nq+1))].CopyTo(a);p=np;q=nq;
        }
        Restrict(new(a[..(4*(p+1)*(q+1))],p,q,patch.Domain),box,b);
        for(var d=0;d<4;d++){hull[d]=b[d];for(var i=1;i<(p+1)*(q+1);i++)hull[d]=IntervalNumber.Hull(hull[d],b[4*i+d]);}
    }
}
