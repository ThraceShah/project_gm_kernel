using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private readonly struct WrappedChartStation
    {
        private readonly double first,last,scale,mappedFirst,mappedScale;

        internal WrappedChartStation(in BlendChartInstruction source,double last,in BlendChartInstruction target)
        {first=source.Parameter;this.last=last;scale=source.Scale;mappedFirst=target.Parameter;mappedScale=target.Scale;}

        internal bool Available=>scale>0&&mappedScale>0;

        internal bool TryTransfer(in IntervalNumber station,out IntervalNumber lifted)
        {
            lifted=UncertifiedStation;
            if(!station.Finite||station.Lower<first||station.Upper>last)return false;
            // The rebuilt and original chart use the same chord equation.
            // Their normalized station (t - origin) / scale is unchanged.
            lifted=new IntervalNumber(mappedFirst)+(station-first)/scale*mappedScale;
            return lifted.Finite;
        }
    }

    private struct WrappedCutCharts
    {
        internal WrappedChartStation First,Last;
    }

    private static int CloneWrappedNestedIntersection(CurveTag original,in CurveRecord record,in ICurveData data,
        double first,double last,out CurveTag curve,out double lower,out double upper,out WrappedCutCharts mapping)
    {
        curve=0;lower=first;upper=last;mapping=default;
        if(!ProfileClosedICurve(original,record.TMin,record.TMax)||!(first>last)||first>record.TMax||last<record.TMin
            ||data.UvType!=IntersectionUvType.Both||data.ExtendedChartCount!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        if(last==record.TMin){upper=record.TMax;return TryCloneEntityCore(original,out curve);}
        if(first==record.TMax){lower=record.TMin;return TryCloneEntityCore(original,out curve);}
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            var root=program.Spines[data.ProgramSpine];var charts=program.Charts.Slice(root.ChartOffset,root.ChartCount);
            if(SpineParameterMap.Explicit(root)||!ExactVector(charts[0].Position,charts[^1].Position)
                ||!SameClosedChartWitness(data.Surface0Tag,charts[0].FirstU,charts[0].FirstV,charts[^1].FirstU,charts[^1].FirstV)
                ||!SameClosedChartWitness(data.Surface1Tag,charts[0].SecondU,charts[0].SecondV,charts[^1].SecondU,charts[^1].SecondV))
                return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var start=0;while(start<charts.Length-2&&charts[start+1].Parameter<=first)start++;
            var end=1;while(end<charts.Length-1&&charts[end].Parameter<last)end++;
            var seam=charts.Length-1-start;var count=seam+end+1;
            if(count>charts.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            var positions=NestedWorkspace<KernelVector3>(count);var uv=NestedWorkspace<double>(4*count);
            if(positions.Length!=count||uv.Length!=4*count)return ParasolidConstants.PK_ERROR_memory_full;
            var previousA=new PK_UV_s();var previousB=new PK_UV_s();
            for(var i=0;i<count;i++)
            {
                // The duplicate closing station is one physical point.
                // At it, take the outgoing gauge and the exact paired UV
                // from the original first chart.
                var source=charts[i<seam?start+i:i-seam];positions[i]=source.Position;
                var a=new PK_UV_s();a.param[0]=source.FirstU;a.param[1]=source.FirstV;
                var b=new PK_UV_s();b.param[0]=source.SecondU;b.param[1]=source.SecondV;
                if(i>0){UnwrapIntersectionParameters(data.Surface0Tag,previousA,ref a);UnwrapIntersectionParameters(data.Surface1Tag,previousB,ref b);}
                uv[4*i]=a.param[0];uv[4*i+1]=a.param[1];uv[4*i+2]=b.param[0];uv[4*i+3]=b.param[1];previousA=a;previousB=b;
            }
            var error=TryCloneEntityCore(data.Surface0Tag,out var firstSupport);if(error!=0)return error;
            error=TryCloneEntityCore(data.Surface1Tag,out var secondSupport);if(error!=0)return error;
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
            var status=compiler.Surface(firstSupport,out var firstIndex);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            status=compiler.Surface(secondSupport,out var secondIndex);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            status=compiler.ChartSpine(firstIndex,secondIndex,positions,charts[start].Parameter,charts[start].Scale,
                root.ChordalError,root.AngularError,LimitType.Artificial,LimitType.Artificial,out var spine,record.Sense,uv);
            if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var rebuilt=compiler.View;var definition=rebuilt.Spines[spine];var window=rebuilt.Charts.Slice(definition.ChartOffset,definition.ChartCount);
            for(var i=0;i<window.Length-1;i++)
                if(!ExactVector(window[i].Chord,charts[i<seam?start+i:i-seam].Chord))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            // Rebuilding the XT chart can continue its scale across the
            // seam with a different positive factor. Match chord distance
            // on the original segment, rather than adding a fake period.
            lower=window[0].Parameter+(first-charts[start].Parameter)*(window[0].Scale/charts[start].Scale);
            upper=last==charts[end].Parameter?window[^1].Parameter:
                window[^2].Parameter+(last-charts[end-1].Parameter)*(window[^2].Scale/charts[end-1].Scale);
            if(lower<window[0].Parameter||upper>window[^1].Parameter||!(upper>lower))return ParasolidConstants.PK_ERROR_cant_do_intersect;
            CurveTag result=0;error=BindNestedBlendSpine(firstSupport,secondSupport,rebuilt,spine,&result,uv);
            if(error!=0)return error;
            mapping=new(){First=new(charts[start],charts[start+1].Parameter,window[0]),Last=new(charts[end-1],charts[end].Parameter,window[^2])};
            curve=result;return 0;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static bool SameClosedChartWitness(SurfTag support,double u0,double v0,double u1,double v1)
    {
        if(!double.IsFinite(u0)||!double.IsFinite(v0)||!double.IsFinite(u1)||!double.IsFinite(v1))return false;
        var start=new PK_UV_s();start.param[0]=u0;start.param[1]=v0;
        var end=new PK_UV_s();end.param[0]=u1;end.param[1]=v1;
        UnwrapIntersectionParameters(support,start,ref end);
        // Compare in native coordinates. Only allow rounding at the scale
        // of the two input parameters and the period that was added.
        static bool Match(double first,double original,double lifted)=>Math.Abs(first-lifted)
            <=8*SmallLinearSolve.MachineEpsilon*Math.Max(Math.Abs(first),Math.Abs(original));
        return Match(u0,u1,end.param[0])&&Match(v0,v1,end.param[1]);
    }
}
