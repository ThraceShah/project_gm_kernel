using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
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
            if(at.X*pq!=origin.X*pq+uDelta.X*u+vDelta.X*v||at.Y*pq!=origin.Y*pq+uDelta.Y*u+vDelta.Y*v||at.Z*pq!=origin.Z*pq+uDelta.Z*u+vDelta.Z*v)return false;
        }
        du=Scale(uDelta,1/(view.UEnd-view.UStart));dv=Scale(vDelta,1/(view.VEnd-view.VStart));var reference=uDelta;ProfileNormalize(ref reference);
        plane=new(SurfaceClass.Plane,origin,normal,reference);return true;
    }
}
