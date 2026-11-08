using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // The circle constructed at a fixed Blend station has its own native
    // angular trim. The great-circle section uses an affine V map over it.
    private struct FiniteSectionChart
    {
        internal CurveTag Curve;
        internal SurfTag Surface,CuttingPlane;
        internal EntityGeneration CurveGeneration,SurfaceGeneration,PlaneGeneration;
        internal double Station,First,Last,VFirst,VLast;
    }

    private static int AssociateFiniteSectionChart(ref SheetArrangement model,SurfTag surface,CurveTag curve,
        double station,double first,double last,double vFirst,double vLast)
    {
        if(!(last>first&&vLast>vFirst)||!double.IsFinite((vLast-vFirst)/(last-first)))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(model.SectionChartCount==model.SectionCharts.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        model.SectionCharts[model.SectionChartCount++]=MakeFiniteSectionChart(surface,curve,station,first,last,vFirst,vLast);
        return 0;
    }

    private static FiniteSectionChart MakeFiniteSectionChart(SurfTag surface,CurveTag curve,double station,double first,double last,
        double vFirst,double vLast,SurfTag cuttingPlane=0)
        =>new(){Curve=curve,Surface=surface,
            CurveGeneration=Curves[GetCurveSlotByTag(curve)].Header.Generation,
            SurfaceGeneration=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation,
            CuttingPlane=cuttingPlane,PlaneGeneration=cuttingPlane==0?0:Surfaces[GetSurfaceSlotByTag(cuttingPlane)].Header.Generation,
            Station=station,First=first,Last=last,VFirst=vFirst,VLast=vLast};

    private static bool TryFiniteSectionChart(ReadOnlySpan<FiniteSectionChart> charts,SurfTag surface,CurveTag curve,
        double first,double last,out FiniteSectionChart chart)
    {
        foreach(ref readonly var candidate in charts)
            if(candidate.Surface==surface&&candidate.Curve==curve&&first>=candidate.First&&last<=candidate.Last
                &&candidate.CurveGeneration==Curves[GetCurveSlotByTag(curve)].Header.Generation
                &&candidate.SurfaceGeneration==Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation)
            {chart=candidate;return true;}
        chart=default;return false;
    }

    private static int EvaluateFiniteSectionChart(in FiniteSectionChart chart,in CurveRecord carrier,double parameter,bool forward,
        out PK_UV_s uv,out PK_UV_s tangent,out KernelVector3 point)
    {
        uv=tangent=default;point=default;
        var slope=(chart.VLast-chart.VFirst)/(chart.Last-chart.First);
        uv.param[0]=chart.Station;
        uv.param[1]=parameter==chart.First?chart.VFirst:parameter==chart.Last?chart.VLast:Math.FusedMultiplyAdd(parameter-chart.First,slope,chart.VFirst);
        tangent.param[1]=forward==(LocalSenseSign(carrier.Sense)>0)?slope:-slope;
        Span<KernelVector3> evaluated=stackalloc KernelVector3[1];
        var status=EvaluateCurveCore(in carrier,parameter,0,false,evaluated,out _);
        if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        point=evaluated[0];return 0;
    }

    private static int WindFiniteSectionChart(in FiniteSectionChart chart,in ModelingEdge edge,byte forward,
        PK_UV_s query,double du,double dv,out BufferCount crossing)
    {
        var nativeForward=(forward!=0)==(LocalSenseSign(Curves[GetCurveSlotByTag(chart.Curve)].Sense)>0);
        var error=FiniteSectionRay(chart.Station,SectionChartV(chart,edge.Start),SectionChartV(chart,edge.End),nativeForward,
            query,du,dv,out crossing,out var v);
        LastSheetAlgorithmStatus=error==0?AlgorithmStatus.Success:AlgorithmStatus.NotConverged;
        if(error!=0){LastSheetPartitionStage=du==0?SheetPartitionStage.RayTangency:SheetPartitionStage.RayEndpoint;return error;}
        if(crossing!=0&&TraceFiniteBoundary!=null)
        {
            PK_UV_s hit=default;hit.param[0]=chart.Station;hit.param[1]=v;
            TraceFiniteBoundary(new(){Stage=BoundaryTraceStage.NativeSectionWinding,Curve=chart.Curve,Surface=chart.Surface,
                Generation=chart.CurveGeneration,SurfaceGeneration=chart.SurfaceGeneration,First=edge.Start,Last=edge.End,
                Source=-1,SourceEdge=-1,SourceFin=-1,Face=-1,Product=-1,Edge=-1,Forward=forward,UvStart=query,UvEnd=hit});
        }
        return 0;
    }

    internal static int FiniteSectionRay(double station,IntervalNumber vFirst,IntervalNumber vLast,bool nativeForward,
        PK_UV_s query,double du,double dv,out BufferCount crossing,out double hitV)
    {
        // A fixed-U section has at most one transverse ray hit. Require an
        // interior hit on both finite intervals so adjacent uses cannot both
        // count a vertex; a different ray handles unresolved endpoints.
        crossing=0;hitV=0;
        if(du==0)return query.param[0]!=station?0:ParasolidConstants.PK_ERROR_eval_failure;
        var along=(new IntervalNumber(station)-query.param[0])/du;
        if(along.Upper<0)return 0;
        var v=new IntervalNumber(query.param[1])+along*dv;
        if(v.Upper<vFirst.Lower||v.Lower>vLast.Upper)return 0;
        if(!v.Finite||along.Lower<=0||v.Lower<=vFirst.Upper||v.Upper>=vLast.Lower)return ParasolidConstants.PK_ERROR_eval_failure;
        crossing=nativeForward==(du>0)?1:-1;hitV=v.Midpoint;return 0;
    }

    private static IntervalNumber SectionChartV(in FiniteSectionChart chart,double t)
    {
        if(t==chart.First)return new(chart.VFirst);
        if(t==chart.Last)return new(chart.VLast);
        return chart.VFirst+(new IntervalNumber(t)-chart.First)*(new IntervalNumber(chart.VLast)-chart.VFirst)
            /(new IntervalNumber(chart.Last)-chart.First);
    }
}
