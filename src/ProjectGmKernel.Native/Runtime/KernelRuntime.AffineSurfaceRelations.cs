using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool TryAffineSphereCircle(SurfaceSlot slot,in AnalyticSurface sphere,
        Span<OperationIntersectionBranch> branches,out BufferCount count)
    {
        count=0;
        if(sphere.Kind!=SurfaceClass.Sphere||!TryAffinePlanarSurface(slot,out var plane,out _,out _))return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]);var poles=view.Vertices;
        var origin=new IntervalVector(plane.Origin);
        var u=new IntervalVector(ProfileEuclideanPole(poles,3,(view.UVertexCount-1)*view.VVertexCount))-origin;
        var v=new IntervalVector(ProfileEuclideanPole(poles,3,view.VVertexCount-1))-origin;
        var normal=new IntervalVector(plane.Axis);
        if((IntervalVector.Dot(normal,normal).Value-1).Magnitude!=0
            ||IntervalVector.Dot(u,normal).Value.Magnitude!=0||IntervalVector.Dot(v,normal).Value.Magnitude!=0)return false;
        var d=new IntervalVector(sphere.Origin)-origin;
        var height=IntervalVector.Dot(d,normal).Value;
        var radius2=IntervalNumber.Square(sphere.Radius)-IntervalNumber.Square(height);
        if(!radius2.Finite||radius2.Lower<=0)return false;
        var radius=IntervalDual.Sqrt(new(radius2,0)).Value;
        if(!radius.Finite)return false;

        // Bound the entire analytic section in the actual finite affine
        // chart. Coordinates here are normalized Bernstein coordinates,
        // so even a reparameterized patch must contain this box in [0,1]^2.
        var span=new IntervalNumber(-radius.Upper,radius.Upper);
        var footprint=d-normal*height+new IntervalVector(span,span,span);
        var uu=IntervalVector.Dot(u,u).Value;var uv=IntervalVector.Dot(u,v).Value;var vv=IntervalVector.Dot(v,v).Value;
        var determinant=uu*vv-uv*uv;if(!determinant.Finite||determinant.Lower<=0)return false;
        var a=IntervalVector.Dot(footprint,u).Value;var b=IntervalVector.Dot(footprint,v).Value;
        if(!Inside((a*vv-b*uv)/determinant)||!Inside((b*uu-a*uv)/determinant))return false;
        Span<KernelVector3> isolated=stackalloc KernelVector3[1];
        if(OperationIntersection.Surfaces(plane,sphere,branches,out var found,isolated,out var points)!=AlgorithmStatus.Success
            ||found!=1||points!=0||branches[0].Class!=CurveClass.Circle||branches[0].Kind!=IntersectionCurveKind.Simple)return false;
        count=found;return true;

        static bool Inside(IntervalNumber range)=>range.Finite&&range.Lower>0&&range.Upper<1;
    }

#if KERNEL_TEST_HOOKS
    internal static bool TestAffineSphereCircle(SurfTag patch,SurfTag sphere)
    {
        Span<OperationIntersectionBranch> branches=stackalloc OperationIntersectionBranch[1];
        return TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(sphere)],out var round)
            &&TryAffineSphereCircle(GetSurfaceSlotByTag(patch),round,branches,out _);
    }
#endif

    private static bool BranchesInsideAffineSurface(SurfaceSlot slot,ReadOnlySpan<OperationIntersectionBranch> branches)
    {
        if(!TryAffinePlanarSurface(slot,out var plane,out var du,out var dv))return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]);
        var uu=Dot(du,du);var uv=Dot(du,dv);var vv=Dot(dv,dv);var determinant=uu*vv-uv*uv;if(!(determinant>0))return false;
        foreach(ref readonly var branch in branches)
        {
            var count=branch.Class==CurveClass.Line?2:branch.Class==CurveClass.BCurve?branch.Degree+1:0;if(count==0)return false;
            for(var i=0;i<count;i++)
            {
                KernelVector3 point;
                if(branch.Class==CurveClass.Line)point=Add(branch.Origin,Scale(branch.Axis,i==0?branch.Start:branch.End));
                else{var at=4*i;var w=branch.Poles[at+3];if(!(w>0))return false;point=new(){X=branch.Poles[at]/w,Y=branch.Poles[at+1]/w,Z=branch.Poles[at+2]/w};}
                var offset=Sub(point,plane.Origin);var a=Dot(offset,du);var b=Dot(offset,dv);
                var u=view.UStart+(a*vv-b*uv)/determinant;var v=view.VStart+(b*uu-a*uv)/determinant;
                // Positive-weight Bezier control hulls preserve both affine
                // UV inequalities over the complete curve. Otherwise keep
                // the original finite two-patch SSI path.
                if(u<view.UStart-1e-12||u>view.UEnd+1e-12||v<view.VStart-1e-12||v>view.VEnd+1e-12)return false;
            }
        }
        return true;
    }

    // An exact affine parameter map proves this NURBS has an injective planar
    // support, rather than recognizing a plane from a few evaluated points.
    // Modeling SSI can use its scalar plane equation and still retain the
    // actual NURBS as an ICurve support and clip to both original finite loops.
    private static bool TryAffinePlanarSurface(SurfaceSlot slot,out AnalyticSurface plane,out KernelVector3 du,out KernelVector3 dv)
    {
        plane=default;du=dv=default;if(Surfaces[slot].Class!=SurfaceClass.BSurface)return false;
        var view=GetBSurfaceView(BSurfaceDataStore[Surfaces[slot].DataIndex]);if(view.Rational||view.Dimension!=3||view.UPeriodic||view.VPeriodic
            ||view.UVertexCount!=view.UDegree+1||view.VVertexCount!=view.VDegree+1)return false;
        for(var i=0;i<=view.UDegree;i++)if(view.UKnots[i]!=view.UStart||view.UKnots[view.UKnots.Length-1-i]!=view.UEnd)return false;
        for(var i=0;i<=view.VDegree;i++)if(view.VKnots[i]!=view.VStart||view.VKnots[view.VKnots.Length-1-i]!=view.VEnd)return false;
        var p=view.Vertices;var origin=ProfileEuclideanPole(p,3,0);var uEnd=ProfileEuclideanPole(p,3,(view.UVertexCount-1)*view.VVertexCount);var vEnd=ProfileEuclideanPole(p,3,view.VVertexCount-1);
        var uDelta=Sub(uEnd,origin);var vDelta=Sub(vEnd,origin);var normal=ProfileCross(uDelta,vDelta);if(!ProfileNormalize(ref normal))return false;
        var pq=view.UDegree*view.VDegree;
        for(var i=0;i<view.UVertexCount;i++)for(var j=0;j<view.VVertexCount;j++)
        {
            var at=ProfileEuclideanPole(p,3,i*view.VVertexCount+j);var u=i*view.VDegree;var v=j*view.UDegree;
            if(!ExactAffineCoordinate(at.X,origin.X,uEnd.X,vEnd.X,pq,u,v)||!ExactAffineCoordinate(at.Y,origin.Y,uEnd.Y,vEnd.Y,pq,u,v)
                ||!ExactAffineCoordinate(at.Z,origin.Z,uEnd.Z,vEnd.Z,pq,u,v))return false;
        }
        du=Scale(uDelta,1/(view.UEnd-view.UStart));dv=Scale(vDelta,1/(view.VEnd-view.VStart));var reference=uDelta;ProfileNormalize(ref reference);
        plane=new(SurfaceClass.Plane,origin,normal,reference);return true;
    }

    private static bool ExactAffineCoordinate(double value,double origin,double uEnd,double vEnd,double weight,double u,double v)
    {
        // Bernstein's first-moment identity, evaluated as an error-free
        // expansion. Subtracting an endpoint and adding it back can round
        // (for example 1.2 -> .2); that is not evidence of a nonlinear map.
        Span<double> values=stackalloc double[4]{value,origin,uEnd,vEnd};
        Span<double> weights=stackalloc double[4]{weight,u+v-weight,-u,-v};
        Span<double> expansion=stackalloc double[8];var count=0;
        for(var term=0;term<4;term++)
        {
            var high=values[term]*weights[term];if(!double.IsFinite(high))return false;
            var low=Math.FusedMultiplyAdd(values[term],weights[term],-high);
            for(var part=0;part<2;part++)
            {
                var q=part==0?low:high;var written=0;
                for(var i=0;i<count;i++)
                {
                    var sum=q+expansion[i];if(!double.IsFinite(sum))return false;
                    var virtualB=sum-q;var remainder=(q-(sum-virtualB))+(expansion[i]-virtualB);
                    if(remainder!=0)expansion[written++]=remainder;q=sum;
                }
                if(q!=0)expansion[written++]=q;count=written;
            }
        }
        return count==0;
    }
}
