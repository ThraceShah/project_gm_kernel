using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class AffineSplineTerminator
{
    // A nonrational, single linear span at a fixed transverse UV is affine
    // throughout its natural interval. Its inverse is unique on that line.
    internal static bool Certify(in ICurveSupportView first,in ICurveSupportView second,
        in TerminatorLimit limit,in KernelVector3 chartPoint,in SampleWitness at,in SampleWitness previous)
    {
        if(first.IsBSurface==second.IsBSurface||limit.TermUse is not (LimitTermUse.First or LimitTermUse.Second)
            ||OperationIntersection.Norm(Sub(chartPoint,limit.BranchPoint))!=0)return false;
        ref readonly var spline=ref (first.IsBSurface?ref first:ref second);
        ref readonly var plane=ref (first.IsBSurface?ref second:ref first);
        ref readonly var bs=ref spline.BSpline;
        if(plane.Analytic.Kind!=SurfaceClass.Plane||bs.Rational||bs.UPeriodic||bs.VPeriodic
            ||bs.UVertexCount!=bs.UDegree+1||bs.VVertexCount!=bs.VDegree+1)return false;
        var flag=first.IsBSurface?SampleWitness.HasUv0:SampleWitness.HasUv1;
        if((at.Flags&previous.Flags&flag)==0)return false;
        var u=first.IsBSurface?at.U0:at.U1;var v=first.IsBSurface?at.V0:at.V1;
        var priorU=first.IsBSurface?previous.U0:previous.U1;var priorV=first.IsBSurface?previous.V0:previous.V1;
        var variesU=u!=priorU&&v==priorV&&bs.UDegree==1&&bs.UVertexCount==2;
        var variesV=v!=priorV&&u==priorU&&bs.VDegree==1&&bs.VVertexCount==2;
        if(variesU==variesV)return false;
        var lo=variesU?bs.UStart:bs.VStart;var hi=variesU?bs.UEnd:bs.VEnd;
        var station=variesU?u:v;var prior=variesU?priorU:priorV;
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);
        Span<KernelVector3> sample=stackalloc KernelVector3[1];
        if(ICurveSupportEvaluation.Evaluate(spline,variesU?lo:u,variesU?v:lo,layout,sample)!=AlgorithmStatus.Success)return false;
        var origin=sample[0];
        if(ICurveSupportEvaluation.Evaluate(spline,variesU?hi:u,variesU?v:hi,layout,sample)!=AlgorithmStatus.Success)return false;
        var axis=Sub(sample[0],origin);var squared=Dot(axis,axis);
        if(!(squared>0)||!double.IsFinite(squared))return false;
        var endFraction=Dot(Sub(limit.Endpoint,origin),axis)/squared;
        var terminal=lo+(hi-lo)*endFraction;
        if(!(terminal>=lo&&terminal<=hi&&(terminal-station)*(station-prior)>0))return false;
        var tolerance=Math.Min(OperationIntersection.DistanceTolerance,ICurveEvaluation.ResidualTolerance*spline.LengthScale);
        Span<IntervalNumber> poles=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        if(BezierSurfaceBounds.Extract(bs,bs.UDegree,bs.VDegree,poles)!=AlgorithmStatus.Success)return false;
        var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles[..(4*bs.UVertexCount*bs.VVertexCount)],
            bs.UDegree,bs.VDegree,new(bs.UStart,bs.UEnd,bs.VStart,bs.VEnd)));
        // Both represented chords are affine. Endpoint enclosures bound
        // the discrepancy throughout the entire terminal span by convexity.
        return BoundEndpoint(patch,plane.Analytic,variesU?terminal:u,variesU?v:terminal,limit.Endpoint,tolerance)
            &&BoundEndpoint(patch,plane.Analytic,u,v,limit.BranchPoint,tolerance);
    }

    internal static bool BoundEndpoint(in SurfaceIntersectionPatch patch,in AnalyticSurface plane,
        double u,double v,in KernelVector3 point,double tolerance)
    {
        Span<IntervalNumber> jet=stackalloc IntervalNumber[9];
        if(patch.Bound(new(u,u,v,v),jet)!=AlgorithmStatus.Success)return false;
        var dx=jet[0]-point.X;var dy=jet[1]-point.Y;var dz=jet[2]-point.Z;
        var squared=IntervalNumber.Square(dx)+IntervalNumber.Square(dy)+IntervalNumber.Square(dz);
        var height=IntervalVector.Dot(new IntervalVector(point)-new IntervalVector(plane.Origin),new IntervalVector(plane.Axis)).Value;
        return squared.Upper<=IntervalNumber.Square(new(tolerance)).Lower&&height.Magnitude<=tolerance;
    }

}
