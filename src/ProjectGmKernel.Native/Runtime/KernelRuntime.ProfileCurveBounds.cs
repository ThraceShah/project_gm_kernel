using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>Split a wire's B-spline at every internal non-G1 C0 junction.</summary>
    private static int ProfileCurveBreaks(in CurveRecord curve,double first,double last,Span<double> breaks,out BufferCount count)
    {
        count=1;breaks[0]=first;
        if(curve.Class==CurveClass.BCurve)
        {
            ref readonly var data=ref BCurveDataStore[curve.DataIndex];
            if(data.VertexDim!=(data.IsRational!=0?4:3))return ParasolidConstants.PK_ERROR_bad_dimension;
            var knots=data.KnotsSpan();var multiplicities=data.KnotMultsSpan();var expanded=0;
            var poles=data.PolesSpan();var dimension=data.VertexDim;
            for(var k=0;k<knots.Length;k++)
            {
                var previous=expanded;expanded+=multiplicities[k];
                if(knots[k]<=first||knots[k]>=last||multiplicities[k]<data.Degree)continue;
                if(multiplicities[k]!=data.Degree)return ParasolidConstants.PK_ERROR_not_implemented;
                // With multiplicity p, the join is control point k-1 in the
                // expanded knot layout. Its neighbours define both endpoint
                // tangent directions, also for positive rational weights.
                var pole=previous-1;
                if(pole<=0||pole+1>=data.NVertices)return ParasolidConstants.PK_ERROR_bad_knots;
                var center=ProfileEuclideanPole(poles,dimension,pole);
                var left=Sub(center,ProfileEuclideanPole(poles,dimension,pole-1));
                var right=Sub(ProfileEuclideanPole(poles,dimension,pole+1),center);
                var ln=OperationIntersection.Norm(left);var rn=OperationIntersection.Norm(right);
                if(!(ln>0)||!(rn>0)||!double.IsFinite(ln)||!double.IsFinite(rn))return ParasolidConstants.PK_ERROR_eval_failure;
                left=Scale(left,1/ln);right=Scale(right,1/rn);
                if(Dot(left,right)>0&&OperationIntersection.Norm(ProfileCross(left,right))<=1e-11)continue;
                if(count==breaks.Length-1)return ParasolidConstants.PK_ERROR_not_implemented;
                breaks[count++]=knots[k];
            }
        }
        breaks[count++]=last;return 0;
    }

    private static int CloneProfileBoundedCurve(CurveTag source,double first,double last,out CurveTag clone)
    {
        ref readonly var curve=ref Curves[GetCurveSlotByTag(source)];
        if(curve.Class!=CurveClass.BCurve||first==curve.TMin&&last==curve.TMax)return TryCloneEntityCore(source,out clone);
        var error=FreeformBoundedCurve(source,first,last,false,out clone,preserveParameters:true);
        if(error==ParasolidConstants.PK_ERROR_not_implemented)
        {
            // Preserve the existing periodic/unclamped/high-degree wire path
            // only when its whole support has no outside non-G1 junction to
            // leak into the bounded edge. Clipping such layouts is separate.
            Span<double> breaks=stackalloc double[ProfileEdgeLimit+1];
            var status=ProfileCurveBreaks(in curve,curve.TMin,curve.TMax,breaks,out var count);
            if(status!=0||count!=2)return error;
            error=TryCloneEntityCore(source,out clone);
        }
        if(error==0)Curves[GetCurveSlotByTag(clone)].Sense=curve.Sense;
        return error;
    }

    private static KernelVector3 ProfileEuclideanPole(ReadOnlySpan<double> poles,BufferCount dimension,ControlPointIndex index)
    {
        var at=index*dimension;var weight=dimension==4?poles[at+3]:1;
        return Vector(poles[at]/weight,poles[at+1]/weight,poles[at+2]/weight);
    }
}
