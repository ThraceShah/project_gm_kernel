using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // The circle constructed at a fixed Blend station has its own native
    // angular trim. The great-circle section uses an affine V map over it.
    private struct FiniteSectionChart
    {
        internal CurveTag Curve;
        internal SurfTag Surface;
        internal EntityGeneration CurveGeneration,SurfaceGeneration;
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

    private static FiniteSectionChart MakeFiniteSectionChart(SurfTag surface,CurveTag curve,double station,double first,double last,double vFirst,double vLast)
        =>new(){Curve=curve,Surface=surface,
            CurveGeneration=Curves[GetCurveSlotByTag(curve)].Header.Generation,
            SurfaceGeneration=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation,
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
        var status=EvaluateCurveCore(carrier,parameter,0,false,evaluated,out _);
        if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        point=evaluated[0];return 0;
    }
}
