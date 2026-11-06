using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Bounds;

// Conditional global-foot certificate. The caller must first certify a
// stationary foot at the rolling radius for every centre in its spine tube.
// Strict convexity on a convex parameter rectangle then makes that foot the
// unique global minimum; no other point of the rolling sphere hits support.
internal static class RollingBallReach
{
    private struct Cell
    {
        internal SurfaceParameterBox Domain;
        internal BufferCount Depth;
    }

    internal static AlgorithmStatus ValidateC1NaturalDomain(in BSurfaceView surface)
    {
        var p=surface.UDegree;var q=surface.VDegree;
        if(p<1||q<1||surface.UVertexCount<=p||surface.VVertexCount<=q
            ||surface.Dimension!=(surface.Rational?4:3)
            ||(long)surface.UVertexCount*surface.VVertexCount!=surface.Vertices.Length/surface.Dimension
            ||surface.Vertices.Length%surface.Dimension!=0
            ||surface.UKnots.Length!=(long)surface.UVertexCount+p+1
            ||surface.VKnots.Length!=(long)surface.VVertexCount+q+1)return AlgorithmStatus.InvalidInput;
        if(p>BezierSurfaceBounds.MaxDegree||q>BezierSurfaceBounds.MaxDegree||surface.UPeriodic||surface.VPeriodic)return AlgorithmStatus.Unsupported;
        foreach(var value in surface.Vertices)if(!double.IsFinite(value))return AlgorithmStatus.InvalidInput;
        if(surface.Rational)for(var at=3;at<surface.Vertices.Length;at+=4)if(!(surface.Vertices[at]>0))return AlgorithmStatus.Unsupported;
        var status=ValidateKnots(surface.UKnots,p,surface.UVertexCount);if(status!=AlgorithmStatus.Success)return status;
        return ValidateKnots(surface.VKnots,q,surface.VVertexCount);
    }

    private static AlgorithmStatus ValidateKnots(ReadOnlySpan<double> knots,SplineDegree degree,BufferCount vertices)
    {
        for(var i=0;i<knots.Length;i++)if(!double.IsFinite(knots[i])||i>0&&knots[i]<knots[i-1])return AlgorithmStatus.InvalidInput;
        var first=knots[degree];var last=knots[vertices];if(!(last>first))return AlgorithmStatus.InvalidInput;
        var multiplicity=0;
        for(var i=0;i<knots.Length;i++)
        {
            multiplicity=i>0&&knots[i]==knots[i-1]?multiplicity+1:1;
            // C1 in the common parameter coordinates is essential. Positive
            // per-span Hessians alone do not exclude two feet across a crease.
            if(knots[i]>first&&knots[i]<last&&multiplicity>degree-1)return AlgorithmStatus.Unsupported;
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus CertifySpline(in BSurfaceView surface,in IntervalBox3 centres,
        in SurfaceParameterBox certifiedFootUV,ref BufferCount visited,BufferCount limit,out double strength)
    {
        strength=0;var status=ValidateC1NaturalDomain(surface);if(status!=AlgorithmStatus.Success)return status;
        if(visited<0||limit<0||!certifiedFootUV.Valid||centres.IsEmpty
            ||!double.IsFinite(centres.XLo)||!double.IsFinite(centres.XHi)||!double.IsFinite(centres.YLo)
            ||!double.IsFinite(centres.YHi)||!double.IsFinite(centres.ZLo)||!double.IsFinite(centres.ZHi))return AlgorithmStatus.InvalidInput;
        // The procedural evaluator uses tensor linear continuation outside
        // natural knots. Do not substitute polynomial Bezier extrapolation or
        // clamp a joint foot tube into this deliberately bounded certificate.
        if(certifiedFootUV.U0<surface.UStart||certifiedFootUV.U1>surface.UEnd
            ||certifiedFootUV.V0<surface.VStart||certifiedFootUV.V1>surface.VEnd)return AlgorithmStatus.Unsupported;
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];var minimum=double.PositiveInfinity;
        for(var u=surface.UDegree;u<surface.UVertexCount;u++)for(var v=surface.VDegree;v<surface.VVertexCount;v++)
        {
            var domain=new SurfaceParameterBox(surface.UKnots[u],surface.UKnots[u+1],surface.VKnots[v],surface.VKnots[v+1]);
            if(!(domain.U1>domain.U0&&domain.V1>domain.V0))continue;
            status=BezierSurfaceBounds.Extract(surface,u,v,poles);if(status!=AlgorithmStatus.Success)return status;
            var patch=new BezierSurfacePatch(poles[..(4*(surface.UDegree+1)*(surface.VDegree+1))],surface.UDegree,surface.VDegree,domain);
            status=CertifyPatch(patch,centres,ref visited,limit,out var local);if(status!=AlgorithmStatus.Success)return status;minimum=Math.Min(minimum,local);
        }
        if(!double.IsFinite(minimum)||!(minimum>0))return AlgorithmStatus.NotConverged;
        strength=minimum;return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus CertifyPatch(in BezierSurfacePatch patch,in IntervalBox3 centres,
        ref BufferCount visited,BufferCount limit,out double strength)
    {
        strength=0;Span<Cell> pending=stackalloc Cell[64];pending[0]=new(){Domain=patch.Domain};var count=1;var minimum=double.PositiveInfinity;
        Span<IntervalNumber> jets=stackalloc IntervalNumber[18];
        while(count>0)
        {
            if(visited>=limit)return AlgorithmStatus.NotConverged;visited++;
            var cell=pending[--count];var box=cell.Domain;var status=BezierSurfaceBounds.BoundSecond(patch,box,jets);if(status!=AlgorithmStatus.Success)return status;
            IntervalNumber a=0,b=0,c=0;
            for(var d=0;d<3;d++)
            {
                var centre=d==0?new IntervalNumber(centres.XLo,centres.XHi):d==1?new(centres.YLo,centres.YHi):new(centres.ZLo,centres.ZHi);
                var delta=centre-jets[d];a+=IntervalNumber.Square(jets[3+d])-delta*jets[9+d];
                b+=jets[3+d]*jets[6+d]-delta*jets[12+d];c+=IntervalNumber.Square(jets[6+d])-delta*jets[15+d];
            }
            var determinant=a*c-IntervalNumber.Square(b);
            if(a.Finite&&b.Finite&&c.Finite&&a.Lower>0&&c.Lower>0&&determinant.Lower>0)
            {
                var lower=(determinant/(a+c)).Lower;if(!(lower>0)||!double.IsFinite(lower))return AlgorithmStatus.NotConverged;
                minimum=Math.Min(minimum,lower);continue;
            }
            if(a.Upper<=0||c.Upper<=0||determinant.Upper<=0||cell.Depth>=32||count+2>pending.Length)return AlgorithmStatus.NotConverged;
            var alongU=(box.U1-box.U0)/(patch.Domain.U1-patch.Domain.U0)>=(box.V1-box.V0)/(patch.Domain.V1-patch.Domain.V0);
            var lo=alongU?box.U0:box.V0;var hi=alongU?box.U1:box.V1;var middle=lo+(hi-lo)*.5;
            if(!(middle>lo&&middle<hi))return AlgorithmStatus.NotConverged;
            pending[count++]=new(){Domain=alongU?new(box.U0,middle,box.V0,box.V1):new(box.U0,box.U1,box.V0,middle),Depth=cell.Depth+1};
            pending[count++]=new(){Domain=alongU?new(middle,box.U1,box.V0,box.V1):new(box.U0,box.U1,middle,box.V1),Depth=cell.Depth+1};
        }
        strength=minimum;return AlgorithmStatus.Success;
    }

    // A nondegenerate affine plane has a constant positive Gram matrix on
    // R2, so the caller's certified stationary normal foot is globally unique.
    internal static AlgorithmStatus CertifyPlane(in AnalyticSurface plane,out double strength)
    {
        strength=0;if(plane.Kind!=SurfaceClass.Plane)return AlgorithmStatus.Unsupported;
        var x=plane.X;var n=plane.Axis;Span<IntervalNumber> u=stackalloc IntervalNumber[3]{x.X,x.Y,x.Z};Span<IntervalNumber> axis=stackalloc IntervalNumber[3]{n.X,n.Y,n.Z};
        for(var d=0;d<3;d++)if(!u[d].Finite||!axis[d].Finite)return AlgorithmStatus.InvalidInput;
        Span<IntervalNumber> v=stackalloc IntervalNumber[3]{axis[1]*u[2]-axis[2]*u[1],axis[2]*u[0]-axis[0]*u[2],axis[0]*u[1]-axis[1]*u[0]};
        IntervalNumber a=0,b=0,c=0;for(var d=0;d<3;d++){a+=IntervalNumber.Square(u[d]);b+=u[d]*v[d];c+=IntervalNumber.Square(v[d]);}
        var determinant=a*c-IntervalNumber.Square(b);if(!a.Finite||!b.Finite||!c.Finite||!determinant.Finite||a.Lower<=0||c.Lower<=0||determinant.Lower<=0)return AlgorithmStatus.NotConverged;
        var lower=(determinant/(a+c)).Lower;if(!(lower>0)||!double.IsFinite(lower))return AlgorithmStatus.NotConverged;
        strength=lower;return AlgorithmStatus.Success;
    }
}
