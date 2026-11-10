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

    private readonly ref struct NestedBranchStationMap
    {
        private readonly ReadOnlySpan<double> _stations;
        private readonly ReadOnlySpan<double> _parameters;

        internal NestedBranchStationMap(ReadOnlySpan<double> stations, ReadOnlySpan<double> parameters)
        {
            _stations = stations;
            _parameters = parameters;
        }

        internal int Count => _stations.Length;

        internal bool TryMapOldToNew(double tOld, out double tNew)
        {
            tNew = 0;
            if (!double.IsFinite(tOld) || _stations.Length < 2 || _parameters.Length < _stations.Length) return false;
            if (tOld < _stations[0] || tOld > _stations[^1]) return false;
            if (tOld == _stations[0]) { tNew = _parameters[0]; return true; }
            if (tOld == _stations[^1]) { tNew = _parameters[_stations.Length - 1]; return true; }
            for (var i = 0; i < _stations.Length - 1; i++)
            {
                var s0 = _stations[i];
                var s1 = _stations[i + 1];
                if (tOld >= s0 && tOld <= s1 && s1 > s0)
                {
                    var u = (tOld - s0) / (s1 - s0);
                    tNew = _parameters[i] + u * (_parameters[i + 1] - _parameters[i]);
                    return true;
                }
            }
            return false;
        }

        internal bool TryMapNewToOld(double tNew, out double tOld)
        {
            tOld = 0;
            if (!double.IsFinite(tNew) || _stations.Length < 2 || _parameters.Length < _stations.Length) return false;
            if (tNew < _parameters[0] || tNew > _parameters[_stations.Length - 1]) return false;
            if (tNew == _parameters[0]) { tOld = _stations[0]; return true; }
            if (tNew == _parameters[_stations.Length - 1]) { tOld = _stations[^1]; return true; }
            for (var i = 0; i < _stations.Length - 1; i++)
            {
                var p0 = _parameters[i];
                var p1 = _parameters[i + 1];
                if (tNew >= p0 && tNew <= p1 && p1 > p0)
                {
                    var u = (tNew - p0) / (p1 - p0);
                    tOld = _stations[i] + u * (_stations[i + 1] - _stations[i]);
                    return true;
                }
            }
            return false;
        }

        internal bool CertifyBranchCoverage(
            in NestedBlendProgram originalProgram, BufferOffset originalSpine,
            in NestedBlendProgram compiledProgram, BufferOffset newSpine,
            ReadOnlySpan<BlendChartInstruction> originalCharts,
            double lower, double upper, double tolerance = 1e-7)
        {
            if (_stations.Length < 2 || _parameters.Length < _stations.Length) return false;
            if (_stations[0] != lower || _stations[^1] != upper) return false;
            for (var i = 0; i < _stations.Length - 1; i++)
            {
                if (!(_stations[i + 1] > _stations[i]) || !(_parameters[i + 1] > _parameters[i]))
                    return false;
            }
            var prevP = _parameters[0];
            Span<KernelVector3> origJet = stackalloc KernelVector3[2];
            Span<KernelVector3> newJet = stackalloc KernelVector3[2];
            Span<double> origUv = stackalloc double[4];
            Span<double> newUv = stackalloc double[4];

            for (var k = 0; k < originalCharts.Length; k++)
            {
                var tOld = originalCharts[k].Parameter;
                if (tOld <= lower || tOld >= upper) continue;
                if (!TryMapOldToNew(tOld, out var pNew) || !(pNew > prevP)) return false;
                if (!TryMapNewToOld(pNew, out var tRecovered) || Math.Abs(tRecovered - tOld) > 1e-10) return false;

                if (JointNestedBlendEvaluation.EvaluateSpine(originalProgram, originalSpine, tOld, 1, origJet, origUv) != AlgorithmStatus.Success)
                    return false;
                if (JointNestedBlendEvaluation.EvaluateSpine(compiledProgram, newSpine, pNew, 1, newJet, newUv) != AlgorithmStatus.Success)
                    return false;

                var diff = EvaluationMath.Sub(newJet[0], origJet[0]);
                var tLenSq = EvaluationMath.Dot(newJet[1], newJet[1]);
                if (tLenSq > 0)
                {
                    var pProj = Math.Clamp(pNew - EvaluationMath.Dot(diff, newJet[1]) / tLenSq, _parameters[0], _parameters[^1]);
                    if (JointNestedBlendEvaluation.EvaluateSpine(compiledProgram, newSpine, pProj, 1, newJet, newUv) == AlgorithmStatus.Success)
                        diff = EvaluationMath.Sub(newJet[0], origJet[0]);
                }
                var distSq = EvaluationMath.Dot(diff, diff);
                if (distSq > tolerance * tolerance) return false;

                var dotT = EvaluationMath.Dot(EvaluationMath.Unit(origJet[1]), EvaluationMath.Unit(newJet[1]));
                if (!(dotT > 0.99)) return false;

                prevP = pNew;
            }
            return true;
        }
    }

    private static bool TryCompactFiniteNestedIntersection(in CurveRecord record,in ICurveData data,
        SurfTag firstSupport,SurfTag secondSupport,double lower,double upper,out CurveTag curve)
    {
        curve=0;
        if(data.IsBlendContact!=0||data.ProgramBlock<=0||data.UvType!=IntersectionUvType.Both||data.ExtendedChartCount!=0
            ||!(lower>=record.TMin&&upper<=record.TMax&&upper>lower))return false;
        var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            var root=program.Spines[data.ProgramSpine];
            if(SpineParameterMap.Explicit(root)||program.Surfaces[root.First].IsBSpline||program.Surfaces[root.Second].IsBSpline)return false;
            var charts=program.Charts.Slice(root.ChartOffset,root.ChartCount);
            if(charts.Length<2)return false;
            var trimmed=lower>record.TMin||upper<record.TMax;
            if(!trimmed&&charts.Length<=12&&!HasChartUvPeriodJump(firstSupport,secondSupport,charts))return false;
            var mid=0.5*(lower+upper);
            Span<KernelVector3> jetLo=stackalloc KernelVector3[2],jetMid=stackalloc KernelVector3[2],jetHi=stackalloc KernelVector3[2];
            Span<double> uvLo=stackalloc double[4],uvMid=stackalloc double[4],uvHi=stackalloc double[4];
            if(JointNestedBlendEvaluation.EvaluateSpine(program,data.ProgramSpine,lower,1,jetLo,uvLo)!=AlgorithmStatus.Success
                ||JointNestedBlendEvaluation.EvaluateSpine(program,data.ProgramSpine,mid,1,jetMid,uvMid)!=AlgorithmStatus.Success
                ||JointNestedBlendEvaluation.EvaluateSpine(program,data.ProgramSpine,upper,1,jetHi,uvHi)!=AlgorithmStatus.Success)return false;
            if(lower==charts[0].Parameter)jetLo[0]=charts[0].Position;
            if(upper==charts[^1].Parameter)jetHi[0]=charts[^1].Position;

            var tLo=EvaluationMath.Unit(jetLo[1]);var tMid=EvaluationMath.Unit(jetMid[1]);var tHi=EvaluationMath.Unit(jetHi[1]);
            var chord=EvaluationMath.Sub(jetHi[0],jetLo[0]);
            var chordLength=Math.Sqrt(EvaluationMath.Dot(chord,chord));
            if(!(chordLength>0)||!double.IsFinite(chordLength))return false;
            var totalTurn=Math.Acos(Math.Clamp(EvaluationMath.Dot(tLo,tMid),-1,1))+Math.Acos(Math.Clamp(EvaluationMath.Dot(tMid,tHi),-1,1));
            Span<double> stations=stackalloc double[16];var count=0;
            if(chordLength<=1e-3||totalTurn<=0.40)
            {
                stations[count++]=lower;stations[count++]=upper;
            }
            else
            {
                var span=upper-lower;
                var blendSide=program.Surfaces[root.First].IsBlend?1:program.Surfaces[root.Second].IsBlend?3:-1;
                var blendVSpan=blendSide>=0?Math.Abs(uvHi[blendSide]-uvLo[blendSide]):0;
                var effectiveTurn=Math.Max(totalTurn,blendVSpan*(0.5*Math.PI));
                var rampStart=(lower-record.TMin)<=1e-3*(record.TMax-record.TMin)
                    &&(Surfaces[GetSurfaceSlotByTag(firstSupport)].Class==SurfaceClass.Cylinder
                        ||Surfaces[GetSurfaceSlotByTag(secondSupport)].Class==SurfaceClass.Cylinder);
                var maxStep=span*Math.Clamp(0.33743/Math.Max(effectiveTurn,0.33743),0.1,0.5);
                var step=rampStart?span*0.0005484304319:maxStep;
                stations[count++]=lower;var cur=lower;
                while(count<15)
                {
                    var next=cur+Math.Min(step,maxStep);
                    if(next>=upper-0.25*maxStep)break;
                    stations[count++]=next;cur=next;step*=4.0;
                }
                stations[count++]=upper;
            }
            var positions=NestedWorkspace<KernelVector3>(count);var uv=NestedWorkspace<double>(4*count);
            if(positions.Length!=count||uv.Length!=4*count)return false;
            Span<KernelVector3> point=stackalloc KernelVector3[1];Span<double> contacts=stackalloc double[4];
            var midIndex=0;var bestMidDist=double.PositiveInfinity;
            for(var i=0;i<count;i++)
            {
                var d=Math.Abs(stations[i]-mid);if(d<bestMidDist){bestMidDist=d;midIndex=i;}
                if(i==0){positions[0]=jetLo[0];uvLo.CopyTo(uv.Slice(0,4));}
                else if(i==count-1){positions[count-1]=jetHi[0];uvHi.CopyTo(uv.Slice(4*(count-1),4));}
                else
                {
                    if(JointNestedBlendEvaluation.EvaluateSpine(program,data.ProgramSpine,stations[i],0,point,contacts)!=AlgorithmStatus.Success)return false;
                    positions[i]=point[0];contacts.CopyTo(uv.Slice(4*i,4));
                }
            }
            var seedA=new PK_UV_s();seedA.param[0]=uvMid[0];seedA.param[1]=uvMid[1];
            var seedB=new PK_UV_s();seedB.param[0]=uvMid[2];seedB.param[1]=uvMid[3];
            UnwrapStationUv(firstSupport,secondSupport,seedA,seedB,uv,midIndex);
            for(var i=midIndex-1;i>=0;i--)
            {
                var prevA=new PK_UV_s();prevA.param[0]=uv[4*(i+1)];prevA.param[1]=uv[4*(i+1)+1];
                var prevB=new PK_UV_s();prevB.param[0]=uv[4*(i+1)+2];prevB.param[1]=uv[4*(i+1)+3];
                UnwrapStationUv(firstSupport,secondSupport,prevA,prevB,uv,i);
            }
            for(var i=midIndex+1;i<count;i++)
            {
                var prevA=new PK_UV_s();prevA.param[0]=uv[4*(i-1)];prevA.param[1]=uv[4*(i-1)+1];
                var prevB=new PK_UV_s();prevB.param[0]=uv[4*(i-1)+2];prevB.param[1]=uv[4*(i-1)+3];
                UnwrapStationUv(firstSupport,secondSupport,prevA,prevB,uv,i);
            }
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return false;
            if(compiler.Surface(firstSupport,out var firstIndex)!=AlgorithmStatus.Success)return false;
            if(compiler.Surface(secondSupport,out var secondIndex)!=AlgorithmStatus.Success)return false;
            var compactStatus=compiler.CompactChartSpine(firstIndex,secondIndex,positions,lower,upper,
                out var spine,record.Sense,uv);
            if(compactStatus!=AlgorithmStatus.Success)
            {
                if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")
                    Console.Error.WriteLine($"COMPACT_FAIL supports={firstSupport}/{secondSupport} count={count} status={compactStatus} range=[{lower:R},{upper:R}]");
                return false;
            }
            var compiled=compiler.View;
            var compiledSpine=compiled.Spines[spine];
            var compiledCharts=compiled.Charts.Slice(compiledSpine.ChartOffset,count);
            Span<double> newParameters=stackalloc double[count];
            for(var i=0;i<count;i++)newParameters[i]=compiledCharts[i].Parameter;
            var branchMap=new NestedBranchStationMap(stations.Slice(0,count),newParameters);
            if(!branchMap.CertifyBranchCoverage(program,data.ProgramSpine,compiled,spine,charts,lower,upper))return false;
            CurveTag result=0;
            if(BindNestedBlendSpine(firstSupport,secondSupport,compiled,spine,&result,uv)!=0)return false;
            curve=result;return true;
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;}
    }

    private static void UnwrapStationUv(SurfTag first,SurfTag second,in PK_UV_s prevA,in PK_UV_s prevB,Span<double> uv,BufferOffset index)
    {
        var a=new PK_UV_s();a.param[0]=uv[4*index];a.param[1]=uv[4*index+1];
        var b=new PK_UV_s();b.param[0]=uv[4*index+2];b.param[1]=uv[4*index+3];
        UnwrapIntersectionParameters(first,prevA,ref a);
        UnwrapIntersectionParameters(second,prevB,ref b);
        uv[4*index]=a.param[0];uv[4*index+1]=a.param[1];uv[4*index+2]=b.param[0];uv[4*index+3]=b.param[1];
    }

    private static bool HasChartUvPeriodJump(SurfTag first,SurfTag second,ReadOnlySpan<BlendChartInstruction> charts)
    {
        var prevA=new PK_UV_s();prevA.param[0]=charts[0].FirstU;prevA.param[1]=charts[0].FirstV;
        var prevB=new PK_UV_s();prevB.param[0]=charts[0].SecondU;prevB.param[1]=charts[0].SecondV;
        for(var i=1;i<charts.Length;i++)
        {
            var a=new PK_UV_s();a.param[0]=charts[i].FirstU;a.param[1]=charts[i].FirstV;
            var b=new PK_UV_s();b.param[0]=charts[i].SecondU;b.param[1]=charts[i].SecondV;
            var ua=a;var ub=b;
            UnwrapIntersectionParameters(first,prevA,ref ua);
            UnwrapIntersectionParameters(second,prevB,ref ub);
            if(ua.param[0]!=a.param[0]||ua.param[1]!=a.param[1]||ub.param[0]!=b.param[0]||ub.param[1]!=b.param[1])return true;
            prevA=a;prevB=b;
        }
        return false;
    }
}
