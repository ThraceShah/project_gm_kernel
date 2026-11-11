using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

internal struct ScalarRootIsolation
{
    internal IntervalNumber Root,MonotoneDomain;
    internal bool Increasing,Certified;
    internal readonly bool SameRoot(in ScalarRootIsolation other)
        =>Certified&&other.Certified&&Increasing==other.Increasing
            &&Root.Lower<=other.Root.Upper&&other.Root.Lower<=Root.Upper
            &&MonotoneDomain.Lower<=other.MonotoneDomain.Upper&&other.MonotoneDomain.Lower<=MonotoneDomain.Upper;
}

// Intersect a bounded surface isoparametric curve with an analytic level set as a scalar
// equation. Monotone interval derivatives certify root uniqueness, including
// roots at a natural domain endpoint; unresolved cells never mean "no hit".
internal static class SurfaceIsoPlaneRoots
{
    internal enum IsolationFailure:byte{None,CurveBound,MeanValue,NewtonEmpty,NewtonDerivative,NewtonBudget,Subdivision,CellBudget,DomainCoverage}
    [ThreadStatic] internal static IsolationFailure LastFailure;
    [ThreadStatic] internal static IntervalNumber LastUnresolvedInterval,LastRequestedInterval,LastLeft,LastRight,LastDerivative;
    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch curve,in AnalyticSurface plane,double first,double last,
        Span<OperationIntersectionPoint> output,ref BufferCount count,double tolerance=1e-10,bool strictSeparation=false,
        Span<ScalarRootIsolation> isolation=default,RootIsolationUse use=RootIsolationUse.SurfaceBoundary)
    {
        using var profile=new ModelingPerformance.RootIsolationScope(use);
        if(!AnalyticLevelSet.Supported(plane))return AlgorithmStatus.Unsupported;
        LastFailure=IsolationFailure.None;LastRequestedInterval=new(first,last);
        Span<double> lower=stackalloc double[128],upper=stackalloc double[128];Span<BufferCount> depths=stackalloc BufferCount[128];
        lower[0]=first;upper[0]=last;depths[0]=0;var pending=1;var visited=0;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],at=stackalloc IntervalNumber[9];
        while(pending>0)
        {
            ModelingPerformance.Cell(true);
            if(++visited>32768)
            {
                LastFailure=IsolationFailure.CellBudget;LastRequestedInterval=new(first,last);var atIndex=pending-1;
                LastUnresolvedInterval=new(lower[atIndex],upper[atIndex]);
                var check=curve.Bound(new(lower[atIndex],lower[atIndex],0,0),at);if(check==AlgorithmStatus.Success)LastLeft=Distance(at,plane);
                check=curve.Bound(new(upper[atIndex],upper[atIndex],0,0),at);if(check==AlgorithmStatus.Success)LastRight=Distance(at,plane);
                check=curve.Bound(new(lower[atIndex],upper[atIndex],0,0),at);if(check==AlgorithmStatus.Success)LastDerivative=AnalyticLevelSet.Derivative(at,plane,3);
                ModelingPerformance.BudgetFailure();return AlgorithmStatus.NotConverged;
            }
            var index=--pending;var lo=lower[index];var hi=upper[index];var depth=depths[index];
            LastUnresolvedInterval=new(lo,hi);
            var status=curve.Bound(new(lo,hi,0,0),bounds);
            if(status==AlgorithmStatus.Success)
            {
                var distance=Distance(bounds,plane);
                var derivative=AnalyticLevelSet.Derivative(bounds,plane,3);
                LastDerivative=derivative;
                if(plane.Kind!=SurfaceClass.Plane)
                {
                    var middle=lo+(hi-lo)*.5;status=curve.Bound(new(middle,middle,0,0),at);if(status!=AlgorithmStatus.Success){LastFailure=IsolationFailure.CurveBound;return status;}
                    var centered=Distance(at,plane)+derivative*(new IntervalNumber(lo,hi)-middle);
                    distance=new(Math.Max(distance.Lower,centered.Lower),Math.Min(distance.Upper,centered.Upper));
                    if(!distance.Finite){LastFailure=IsolationFailure.MeanValue;return AlgorithmStatus.NotConverged;}
                }
                var exclusion=strictSeparation?0:tolerance;
                if(distance.Lower>exclusion||distance.Upper< -exclusion){ModelingPerformance.RootExcluded();continue;}
                if(derivative.ContainsZero)ModelingPerformance.RootNonmonotone();
                if(!derivative.ContainsZero)
                {
                    status=curve.Bound(new(lo,lo,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                    var left=Distance(at,plane);LastLeft=left;
                    status=curve.Bound(new(hi,hi,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                    var right=Distance(at,plane);LastRight=right;var parameter=0.0;var found=false;ScalarRootIsolation certificate=default;
                    if(!isolation.IsEmpty)
                    {
                        if(left.Upper<0&&right.Lower>0||left.Lower>0&&right.Upper<0)
                        {
                            status=IsolateBracket(curve,plane,lo,hi,derivative,out certificate);if(status!=AlgorithmStatus.Success)return status;
                            parameter=certificate.Root.Midpoint;found=true;
                        }
                        else if(left.Magnitude==0||right.Magnitude==0)
                        {
                            parameter=left.Magnitude==0?lo:hi;
                            certificate=new(){Root=new(parameter),MonotoneDomain=new(lo,hi),Increasing=derivative.Lower>0,Certified=true};found=true;
                        }
                        else if(left.Lower>0&&right.Lower>0||left.Upper<0&&right.Upper<0)continue;
                        else if(TryInternalBoundaryBracket(curve,plane,first,last,lo,hi,left,right,derivative,out certificate)==AlgorithmStatus.Success)
                        {parameter=certificate.Root.Midpoint;found=true;}
                    }
                    else if(left.Magnitude<=tolerance){parameter=lo;found=true;}
                    else if(right.Magnitude<=tolerance){parameter=hi;found=true;}
                    else if(left.Upper<0&&right.Lower>0||left.Lower>0&&right.Upper<0)
                    {
                        // The sign bracket and derivative already prove the
                        // root. Legacy CHART producers choose a representative
                        // within that bracket at their existing spatial error;
                        // a rounded midpoint sign must not discard the root.
                        var a=lo;var b=hi;var negativeLeft=left.Upper<0;
                        for(var iteration=0;iteration<48;iteration++)
                        {
                            parameter=a+(b-a)*.5;status=curve.Bound(new(parameter,parameter,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                            var value=Distance(at,plane);if(value.Magnitude<Math.Min(2e-12,tolerance)){found=true;break;}
                            if(value.ContainsZero)break;
                            if((value.Upper<0)==negativeLeft)a=parameter;else b=parameter;
                        }
                        if(!found)
                        {
                            status=IsolateBracket(curve,plane,a,b,derivative,out certificate);if(status!=AlgorithmStatus.Success)return status;
                            parameter=certificate.Root.Midpoint;found=true;
                        }
                    }
                    else if(left.Lower>0&&right.Lower>0||left.Upper<0&&right.Upper<0)continue;
                    if(found)
                    {
                        status=curve.Bound(new(parameter,parameter,0,0),at);if(status!=AlgorithmStatus.Success)return status;
                        var point=new KernelVector3{X=at[0].Midpoint,Y=at[1].Midpoint,Z=at[2].Midpoint};
                        if(isolation.IsEmpty)
                        {if(!OperationIntersection.AddPoint(output,ref count,point,parameter,0,IntersectionVectorKind.Simple))return AlgorithmStatus.OutputTooSmall;}
                        else
                        {
                            var duplicate=false;for(var root=0;root<count;root++)if(certificate.SameRoot(isolation[root])){duplicate=true;break;}
                            if(duplicate)continue;
                            if(count==output.Length||count==isolation.Length)return AlgorithmStatus.OutputTooSmall;
                            output[count]=new(){Point=point,First=parameter,Kind=IntersectionVectorKind.Simple};isolation[count++]=certificate;
                        }
                        ModelingPerformance.Root();
                        continue;
                    }
                }
            }
            else
            {
                ModelingPerformance.RootBoundFailed();
                if(status!=AlgorithmStatus.NotConverged)return status;
            }
            if(depth>=48||pending+2>lower.Length)
            {
                LastUnresolvedInterval=new(lo,hi);LastRequestedInterval=new(first,last);
                LastFailure=IsolationFailure.Subdivision;
                var check=curve.Bound(new(lo,lo,0,0),at);if(check==AlgorithmStatus.Success)LastLeft=Distance(at,plane);
                check=curve.Bound(new(hi,hi,0,0),at);if(check==AlgorithmStatus.Success)LastRight=Distance(at,plane);
                LastDerivative=AnalyticLevelSet.Derivative(bounds,plane,3);return AlgorithmStatus.NotConverged;
            }
            var mid=lo+(hi-lo)*.5;if(mid==lo||mid==hi)return AlgorithmStatus.NotConverged;
            lower[pending]=lo;upper[pending]=mid;depths[pending++]=depth+1;
            lower[pending]=mid;upper[pending]=hi;depths[pending++]=depth+1;
        }
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus TryInternalBoundaryBracket(in SurfaceIntersectionPatch curve,in AnalyticSurface support,
        double first,double last,double lower,double upper,IntervalNumber left,IntervalNumber right,IntervalNumber derivative,out ScalarRootIsolation certificate)
    {
        certificate=default;
        var slope=Math.Min(Math.Abs(derivative.Lower),Math.Abs(derivative.Upper));
        var width=Math.Max((upper-lower)*.05,4*Math.Max(left.ContainsZero?left.Magnitude:0,right.ContainsZero?right.Magnitude:0)/slope);
        var a=left.ContainsZero?Math.Max(first,lower-width):lower;
        var b=right.ContainsZero?Math.Min(last,upper+width):upper;
        if(a==lower&&b==upper)return AlgorithmStatus.NotConverged;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
        var status=curve.Bound(new(a,b,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;
        derivative=AnalyticLevelSet.Derivative(bounds,support,3);if(derivative.ContainsZero)return AlgorithmStatus.NotConverged;
        status=curve.Bound(new(a,a,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;left=Distance(bounds,support);
        status=curve.Bound(new(b,b,0,0),bounds);if(status!=AlgorithmStatus.Success)return status;right=Distance(bounds,support);
        return left.Upper<0&&right.Lower>0||left.Lower>0&&right.Upper<0
            ?IsolateBracket(curve,support,a,b,derivative,out certificate):AlgorithmStatus.NotConverged;
    }

    // Opposite strict endpoint signs prove existence. A nonzero interval
    // derivative proves uniqueness. Interval Newton narrows this established
    // root even when rounding prevents another sign decision; residual alone
    // is never converted to a parameter error or an existence claim.
    private static AlgorithmStatus IsolateBracket(in SurfaceIntersectionPatch curve,in AnalyticSurface support,
        double first,double last,IntervalNumber derivative,out ScalarRootIsolation certificate)
    {
        certificate=default;var root=new IntervalNumber(first,last);Span<IntervalNumber> values=stackalloc IntervalNumber[9];
        for(var iteration=0;iteration<64;iteration++)
        {
            var middle=root.Midpoint;var status=curve.Bound(new(middle,middle,0,0),values);if(status!=AlgorithmStatus.Success)return status;
            var image=new IntervalNumber(middle)-Distance(values,support)/derivative;
            var next=new IntervalNumber(Math.Max(root.Lower,image.Lower),Math.Min(root.Upper,image.Upper));if(!next.Finite){LastFailure=IsolationFailure.NewtonEmpty;return AlgorithmStatus.NotConverged;}
            if(next.Upper-next.Lower>=(root.Upper-root.Lower)*.875||next.Lower==next.Upper)
            {
                certificate=new(){Root=next,MonotoneDomain=new(first,last),Increasing=derivative.Lower>0,Certified=true};return AlgorithmStatus.Success;
            }
            root=next;status=curve.Bound(new(root.Lower,root.Upper,0,0),values);if(status!=AlgorithmStatus.Success)return status;
            derivative=AnalyticLevelSet.Derivative(values,support,3);if(derivative.ContainsZero){LastFailure=IsolationFailure.NewtonDerivative;return AlgorithmStatus.NotConverged;}
        }
        LastFailure=IsolationFailure.NewtonBudget;return AlgorithmStatus.NotConverged;
    }

    private static IntervalNumber Distance(ReadOnlySpan<IntervalNumber> bounds,in AnalyticSurface plane)
        =>AnalyticLevelSet.Value(bounds,plane);
}
