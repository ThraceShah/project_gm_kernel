using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RationalContactChart
    {
        internal BufferCount Count;
        internal SplineDegree Degree;
        internal double Section;
        internal IntervalNumber Lift;
    }

    private static int BindRationalContactChart(SurfTag surface,in CurveRecord boundary,double first,double last,
        Span<RationalConicArc> arcs,Span<IntervalNumber> x,Span<IntervalNumber> y,out RationalContactChart chart)
    {
        chart=default;var slot=GetSurfaceSlotByTag(surface);
        if(slot<0||Surfaces[slot].Class!=SurfaceClass.BlendSurface||boundary.Class is not(CurveClass.Circle or CurveClass.Ellipse)
            ||!TryReadExplicitSpineCurve(boundary,out var contact))return 0;
        var data=BlendedEdgeDataPool[Surfaces[slot].DataIndex];var program=GetNestedBlendProgram(data);
        var root=program.Surfaces[data.ProgramRoot];var spine=program.Spines[root.Spine];
        if(spine.Curve.Kind!=CurveClass.BCurve)return 0;
        var spline=program.SplineSpine(spine);
        Span<RationalConicArc> candidate=stackalloc RationalConicArc[RationalConicGauge.MaxSpans];
        Span<IntervalNumber> cx=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride],
            cy=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride];
        for(var side=0;side<2;side++)
        {
            var support=program.Surfaces[side==0?root.First:root.Second];var range=side==0?root.Range0:root.Range1;
            var a=support.Analytic;
            if(support.IsBlend||support.IsBSpline||a.Kind is not(SurfaceClass.Plane or SurfaceClass.Cylinder or SurfaceClass.Sphere))continue;
            if(a.Kind!=SurfaceClass.Plane)
                support.Analytic=new(a.Kind,a.Origin,a.Axis,a.X,a.Radius+range*support.NormalSense,a.Secondary);
            if(AnalyticSpineContact.Curve(contact,support,-range,out var center)!=AlgorithmStatus.Success
                ||!RationalConicGauge.Certify(spline,center,1e-10,candidate,cx,cy,out var count)
                ||!RationalConicGauge.NativeAngleLift(candidate[..count],first,last,out var lift))continue;
            if(chart.Count!=0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
            candidate[..count].CopyTo(arcs);var used=count*RationalConicGauge.Stride;cx[..used].CopyTo(x);cy[..used].CopyTo(y);
            var section=side==0?1.0:0.0;
            chart=new(){Count=count,Degree=spline.Degree,Section=root.ReverseSection?1-section:section,Lift=lift};
        }
        return 0;
    }

    private static int TryRationalContactUV(SurfTag surface,in CurveRecord boundary,double first,double last,double parameter,bool forward,
        out PK_UV_s uv,out PK_UV_s tangent,out KernelVector3 point,out bool handled)
    {
        uv=tangent=default;point=default;handled=false;
        Span<RationalConicArc> arcs=stackalloc RationalConicArc[RationalConicGauge.MaxSpans];
        Span<IntervalNumber> x=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride],
            y=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride];
        var error=BindRationalContactChart(surface,boundary,first,last,arcs,x,y,out var chart);
        if(error!=0||chart.Count==0)return error;
        handled=true;
        if(!double.IsFinite(parameter)||parameter<first||parameter>last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(!RationalConicGauge.LocateStation(chart.Degree,arcs[..chart.Count],x,y,new IntervalNumber(parameter)+chart.Lift,
            out var station,out var inverseSlope,out var interior))return ParasolidConstants.PK_ERROR_cant_do_intersect;
        if(!interior&&parameter!=first&&parameter!=last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        Span<KernelVector3> evaluated=stackalloc KernelVector3[1];
        var status=EvaluateCurveCore(boundary,parameter,0,false,evaluated,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        point=evaluated[0];
        if(!interior)
        {
            // Endpoint equality may be representational. The finite angle
            // map has already isolated the only adjacent native station;
            // check its full position box against the existing contact budget.
            var data=BlendedEdgeDataPool[Surfaces[GetSurfaceSlotByTag(surface)].DataIndex];var program=GetNestedBlendProgram(data);
            Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
            status=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,new(station.Lower,station.Upper,chart.Section,chart.Section),bounds);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var delta=new IntervalVector(bounds[0]-point.X,bounds[1]-point.Y,bounds[2]-point.Z).Norm().Value;
            if(!delta.Finite||delta.Upper>1e-10)return ParasolidConstants.PK_ERROR_cant_do_intersect;
        }
        uv.param[0]=station.Midpoint;uv.param[1]=chart.Section;
        tangent.param[0]=inverseSlope*(forward?1:-1)*LocalSenseSign(boundary.Sense);
        return 0;
    }

    private static AlgorithmStatus IntersectRationalContactRay(SurfTag surface,in CurveRecord boundary,PK_UV_s query,
        double du,double dv,in ModelingEdge edge,Span<OperationIntersectionPoint> output,out BufferCount count,out bool handled)
    {
        count=0;handled=false;
        Span<RationalConicArc> arcs=stackalloc RationalConicArc[RationalConicGauge.MaxSpans];
        Span<IntervalNumber> x=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride],
            y=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride];
        var error=BindRationalContactChart(surface,boundary,edge.Start,edge.End,arcs,x,y,out var chart);
        if(error!=0)return AlgorithmStatus.NotConverged;
        if(chart.Count==0)return AlgorithmStatus.Success;
        handled=true;
        if(dv==0)return query.param[1]==chart.Section?AlgorithmStatus.NotConverged:AlgorithmStatus.Success;
        var along=(new IntervalNumber(chart.Section)-query.param[1])/dv;
        if(along.Upper<0)return AlgorithmStatus.Success;
        if(along.Lower<=0)return AlgorithmStatus.NotConverged;
        var station=new IntervalNumber(query.param[0])+along*du;
        if(station.Upper<arcs[0].First||station.Lower>arcs[chart.Count-1].Last)return AlgorithmStatus.Success;
        if(!RationalConicGauge.LocateAngle(chart.Degree,arcs[..chart.Count],x,y,station,out var angle))return AlgorithmStatus.NotConverged;
        var parameter=angle-chart.Lift;
        if(parameter.Upper<edge.Start||parameter.Lower>edge.End)return AlgorithmStatus.Success;
        if(parameter.Lower<=edge.Start||parameter.Upper>=edge.End)return AlgorithmStatus.NotConverged;
        if(output.IsEmpty)return AlgorithmStatus.OutputTooSmall;
        Span<KernelVector3> point=stackalloc KernelVector3[1];var at=parameter.Midpoint;
        var status=EvaluateCurveCore(boundary,at,0,false,point,out _);if(status!=AlgorithmStatus.Success)return status;
        output[count++]=new(){Point=point[0],First=along.Midpoint,Second=at,Kind=IntersectionVectorKind.Simple};
        return AlgorithmStatus.Success;
    }

    private static int IsRationalContact(SurfTag surface,in CurveRecord boundary,double first,double last,out bool found)
    {
        found=false;
        var slot=GetSurfaceSlotByTag(surface);
        if(slot<0||Surfaces[slot].Class!=SurfaceClass.BlendSurface||boundary.Class is not(CurveClass.Ellipse or CurveClass.Circle))return 0;
        Span<RationalConicArc> arcs=stackalloc RationalConicArc[RationalConicGauge.MaxSpans];
        Span<IntervalNumber> x=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride],
            y=stackalloc IntervalNumber[RationalConicGauge.MaxSpans*RationalConicGauge.Stride];
        var error=BindRationalContactChart(surface,boundary,first,last,arcs,x,y,out var chart);
        found=error==0&&chart.Count>0;return error;
    }

}
