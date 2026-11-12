using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Geometry.Bounds;

/// <summary>Position bounds for an explicit native B-surface parameter domain.</summary>
internal static class BSurfaceDomainBounds
{
    internal static AlgorithmStatus Position(in BSurfaceView source,in SurfaceParameterBox domain,bool allowLinearExtension,out IntervalBox3 box)
    {
        box=default;
        if(!domain.Valid)return AlgorithmStatus.InvalidInput;
        if(!ValidSource(source))return AlgorithmStatus.Unsupported;
        var u=new IntervalNumber(domain.U0,domain.U1);var v=new IntervalNumber(domain.V0,domain.V1);
        if(source.UPeriodic&&(u.Lower<source.UStart||u.Upper>source.UEnd))u=new(source.UStart,source.UEnd);
        if(source.VPeriodic&&(v.Lower<source.VStart||v.Upper>source.VEnd))v=new(source.VStart,source.VEnd);
        var inside=u.Lower>=source.UStart&&u.Upper<=source.UEnd&&v.Lower>=source.VStart&&v.Upper<=source.VEnd;
        if(!inside&&!allowLinearExtension)return AlgorithmStatus.InvalidInput;
        if(inside&&TryControlHull(source,out box))return AlgorithmStatus.Success;
        return BoundPieces(source,u,v,allowLinearExtension,out box);
    }

    private static bool ValidSource(in BSurfaceView source)
    {
        if(source.UDegree<1||source.VDegree<1||source.UDegree>BezierSurfaceBounds.MaxDegree||source.VDegree>BezierSurfaceBounds.MaxDegree
            ||source.UVertexCount<=source.UDegree||source.VVertexCount<=source.VDegree||source.Dimension!=(source.Rational?4:3)
            ||source.Vertices.Length!=source.UVertexCount*source.VVertexCount*source.Dimension
            ||source.UKnots.Length!=source.UVertexCount+source.UDegree+1||source.VKnots.Length!=source.VVertexCount+source.VDegree+1)return false;
        foreach(var value in source.Vertices)if(!double.IsFinite(value))return false;
        for(var i=0;i<source.UKnots.Length;i++)if(!double.IsFinite(source.UKnots[i])||i>0&&source.UKnots[i]<source.UKnots[i-1])return false;
        for(var i=0;i<source.VKnots.Length;i++)if(!double.IsFinite(source.VKnots[i])||i>0&&source.VKnots[i]<source.VKnots[i-1])return false;
        return source.UEnd>source.UStart&&source.VEnd>source.VStart;
    }

    private static bool TryControlHull(in BSurfaceView source,out IntervalBox3 box)
    {
        box=default;var x=new IntervalNumber(double.PositiveInfinity,double.NegativeInfinity);var y=x;var z=x;
        for(var i=0;i<source.UVertexCount*source.VVertexCount;i++)
        {
            var at=i*source.Dimension;IntervalNumber weight=source.Rational?source.Vertices[at+3]:1;
            if(!(weight.Lower>0))return false;
            x=IntervalNumber.Hull(x,new IntervalNumber(source.Vertices[at])/weight);
            y=IntervalNumber.Hull(y,new IntervalNumber(source.Vertices[at+1])/weight);
            z=IntervalNumber.Hull(z,new IntervalNumber(source.Vertices[at+2])/weight);
        }
        if(!x.Finite||!y.Finite||!z.Finite)return false;
        box=new(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper);return true;
    }

    private static AlgorithmStatus BoundPieces(in BSurfaceView source,IntervalNumber u,IntervalNumber v,bool allowLinearExtension,out IntervalBox3 box)
    {
        box=default;var found=false;var x=new IntervalNumber(double.PositiveInfinity,double.NegativeInfinity);var y=x;var z=x;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars],jets=stackalloc IntervalNumber[18];
        var extendU=allowLinearExtension&&!source.UPeriodic;var extendV=allowLinearExtension&&!source.VPeriodic;
        for(var ue=extendU?-1:0;ue<=(extendU?1:0);ue++)for(var ve=extendV?-1:0;ve<=(extendV?1:0);ve++)
        {
            if(!Region(u,source.UStart,source.UEnd,ue,out var up,out var dx)||!Region(v,source.VStart,source.VEnd,ve,out var vp,out var dy))continue;
            for(var i=source.UDegree;i<source.UVertexCount;i++)for(var j=source.VDegree;j<source.VVertexCount;j++)
            {
                var a=source.UKnots[i];var b=source.UKnots[i+1];var c=source.VKnots[j];var d=source.VKnots[j+1];if(!(b>a&&d>c))continue;
                var lo=Math.Max(a,up.Lower);var hi=Math.Min(b,up.Upper);var low=Math.Max(c,vp.Lower);var high=Math.Min(d,vp.Upper);if(lo>hi||low>high)continue;
                var status=BezierSurfaceBounds.Extract(source,i,j,poles);if(status!=AlgorithmStatus.Success)return status;
                var patch=new BezierSurfacePatch(poles[..(4*(source.UDegree+1)*(source.VDegree+1))],source.UDegree,source.VDegree,new(a,b,c,d));
                status=BezierSurfaceBounds.BoundSecond(patch,new(lo,hi,low,high),jets);if(status!=AlgorithmStatus.Success)return status;
                // Match BSurfaceEvaluation's tensor linear extension. In
                // particular its cross term is dx*dy*Suv, not a polynomial
                // continuation of the Bezier span outside the knot domain.
                var px=jets[0]+dx*jets[3]+dy*(jets[6]+dx*jets[12]);
                var py=jets[1]+dx*jets[4]+dy*(jets[7]+dx*jets[13]);
                var pz=jets[2]+dx*jets[5]+dy*(jets[8]+dx*jets[14]);
                if(!px.Finite||!py.Finite||!pz.Finite)return AlgorithmStatus.NumericalFailure;
                x=IntervalNumber.Hull(x,px);y=IntervalNumber.Hull(y,py);z=IntervalNumber.Hull(z,pz);found=true;
            }
        }
        if(!found)return AlgorithmStatus.NotConverged;
        box=new(x.Lower,x.Upper,y.Lower,y.Upper,z.Lower,z.Upper);return AlgorithmStatus.Success;
    }

    private static bool Region(IntervalNumber input,double first,double last,BufferOffset region,out IntervalNumber clamped,out IntervalNumber delta)
    {
        delta=0;
        if(region<0)
        {
            clamped=new(first);if(input.Lower>first)return false;
            delta=new IntervalNumber(input.Lower,Math.Min(input.Upper,first))-first;return delta.Finite;
        }
        if(region>0)
        {
            clamped=new(last);if(input.Upper<last)return false;
            delta=new IntervalNumber(Math.Max(input.Lower,last),input.Upper)-last;return delta.Finite;
        }
        clamped=new(Math.Max(input.Lower,first),Math.Min(input.Upper,last));return clamped.Finite;
    }
}
