using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const BufferCount NestedSurfaceLimit=128,NestedSpineLimit=128,NestedChartLimit=4096;

    private static Span<T> NestedWorkspace<T>(BufferCount count) where T:unmanaged
    {
        var storage=CommandScratch.Current.Take((sizeof(T)*count+7)/8);
        return storage.Length*8<sizeof(T)*count?default:MemoryMarshal.Cast<double,T>(storage)[..count];
    }

    private ref struct NestedProgramCompiler
    {
        private Span<BlendSurfaceInstruction> surfaces;
        private Span<BlendSpineInstruction> spines;
        private Span<BlendChartInstruction> charts;
        private Span<SurfTag> surfaceTags;
        private Span<CurveTag> spineTags;
        private Span<double> scalars;
        private BufferCount ns,nc,nh,nd;
        internal readonly NestedBlendProgram View=>new(surfaces[..ns],spines[..nc],charts[..nh],scalars[..nd],CommandScratch.Current.AvailableSpanOfDoubles());
        public NestedProgramCompiler()
        {
            ns=nc=nh=nd=0;surfaces=NestedWorkspace<BlendSurfaceInstruction>(NestedSurfaceLimit);spines=NestedWorkspace<BlendSpineInstruction>(NestedSpineLimit);
            charts=NestedWorkspace<BlendChartInstruction>(NestedChartLimit);surfaceTags=NestedWorkspace<SurfTag>(NestedSurfaceLimit);spineTags=NestedWorkspace<CurveTag>(NestedSpineLimit);
            scalars=NestedWorkspace<double>(65536);
        }
        internal readonly bool Ready=>surfaces.Length==NestedSurfaceLimit&&spines.Length==NestedSpineLimit&&charts.Length==NestedChartLimit&&surfaceTags.Length==NestedSurfaceLimit&&spineTags.Length==NestedSpineLimit&&scalars.Length==65536;

        internal AlgorithmStatus Surface(SurfTag tag,out BlendSurfaceIndex index)
        {
            index=surfaceTags[..ns].IndexOf(tag);if(index>=0)return surfaces[index].NormalSense==0?AlgorithmStatus.InvalidInput:AlgorithmStatus.Success;
            if(ns==surfaces.Length)return AlgorithmStatus.OutputTooSmall;var slot=GetSurfaceSlotByTag(tag);if(slot<0)return AlgorithmStatus.InvalidInput;
            index=ns++;surfaceTags[index]=tag;surfaces[index]=default;var source=Surfaces[slot];
            var instruction=new BlendSurfaceInstruction{NormalSense=SenseSign(source.Sense)};
            if(TryPrepareSurface(source,out var analytic))instruction.Analytic=analytic;
            else if(source.Class==SurfaceClass.BSurface)
            {
                var spline=GetBSurfaceView(BSurfaceDataStore[source.DataIndex]);
                var required=spline.Vertices.Length+spline.UKnots.Length+spline.VKnots.Length;if(nd+required>scalars.Length)return AlgorithmStatus.WorkspaceTooSmall;
                instruction.IsBSpline=true;instruction.SplineBasis=index;instruction.ScalarOffset=nd;instruction.UDegree=spline.UDegree;instruction.VDegree=spline.VDegree;
                instruction.UCount=spline.UVertexCount;instruction.VCount=spline.VVertexCount;instruction.Dimension=spline.Dimension;instruction.Rational=spline.Rational;instruction.UPeriodic=spline.UPeriodic;instruction.VPeriodic=spline.VPeriodic;
                spline.Vertices.CopyTo(scalars[nd..]);nd+=spline.Vertices.Length;spline.UKnots.CopyTo(scalars[nd..]);nd+=spline.UKnots.Length;spline.VKnots.CopyTo(scalars[nd..]);nd+=spline.VKnots.Length;
                for(var previous=0;previous<index;previous++)
                {
                    var candidate=surfaces[previous];if(!candidate.IsBSpline||candidate.Offset!=0)continue;
                    var other=View.Spline(candidate);
                    if(candidate.UDegree==instruction.UDegree&&candidate.VDegree==instruction.VDegree&&candidate.UCount==instruction.UCount&&candidate.VCount==instruction.VCount&&candidate.Dimension==instruction.Dimension&&candidate.Rational==instruction.Rational&&candidate.UPeriodic==instruction.UPeriodic&&candidate.VPeriodic==instruction.VPeriodic
                        &&spline.Vertices.SequenceEqual(other.Vertices)&&spline.UKnots.SequenceEqual(other.UKnots)&&spline.VKnots.SequenceEqual(other.VKnots))
                    {instruction.SplineBasis=candidate.SplineBasis;break;}
                }
            }
            else if(source.Class==SurfaceClass.Offset)
            {
                var offset=OffsetDataPool[source.DataIndex];var status=Surface(offset.BaseSurfTag,out var basis);if(status!=AlgorithmStatus.Success)return status;
                instruction=surfaces[basis];if(!instruction.IsBSpline)return AlgorithmStatus.Unsupported;
                instruction.Offset+=offset.Offset;instruction.NormalSense=SenseSign(source.Sense);
            }
            else if(source.Class==SurfaceClass.BlendSurface)
            {
                var data=BlendedEdgeDataPool[source.DataIndex];
                var status=Surface(data.Surface0Tag,out instruction.First);if(status!=AlgorithmStatus.Success)return status;
                status=Surface(data.Surface1Tag,out instruction.Second);if(status!=AlgorithmStatus.Success)return status;
                status=BlendSpine(data.SpineTag,instruction.First,instruction.Second,data.Range0,data.Range1,source.UMin,source.UMax,out instruction.Spine,
                    data.StartLimit.HvecCount==0&&data.EndLimit.HvecCount==0);if(status!=AlgorithmStatus.Success)return status;
                instruction.IsBlend=true;instruction.Range0=data.Range0;instruction.Range1=data.Range1;
                instruction.ReverseSection=spines[instruction.Spine].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c;
                instruction.NormalSense=GetNestedBlendProgram(data).Surfaces[data.ProgramRoot].NormalSense*SenseSign(source.Sense);
            }
            else return AlgorithmStatus.Unsupported;
            surfaces[index]=instruction;return AlgorithmStatus.Success;
        }

        internal AlgorithmStatus Spine(CurveTag tag,out BlendSpineIndex index)
        {
            index=spineTags[..nc].IndexOf(tag);if(index>=0)return AlgorithmStatus.Success;
            var slot=GetCurveSlotByTag(tag);if(slot<0||Curves[slot].Class!=CurveClass.ICurve)return AlgorithmStatus.Unsupported;
            var data=ICurveDataPool[Curves[slot].DataIndex];
            var status=Surface(data.Surface0Tag,out var a);if(status!=AlgorithmStatus.Success)return status;
            status=Surface(data.Surface1Tag,out var b);if(status!=AlgorithmStatus.Success)return status;
            if(data.ProgramBlock>0)
            {
                var stored=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
                var definition=stored.Spines[data.ProgramSpine];var original=stored.Charts.Slice(definition.ChartOffset,definition.ChartCount);
                if(nc==spines.Length||nh+original.Length>charts.Length)return AlgorithmStatus.OutputTooSmall;
                index=nc++;spineTags[index]=tag;definition.First=a;definition.Second=b;definition.ChartOffset=nh;spines[index]=definition;
                original.CopyTo(charts[nh..]);nh+=original.Length;return AlgorithmStatus.Success;
            }
            var hulls=HvecSpan(data);var positions=MemoryMarshal.Cast<double,KernelVector3>(hulls.Slice(data.StartLimit.HvecCount*3,data.ChartCount*3));
            status=ChartSpine(a,b,positions,data.BaseParameter,data.BaseScale,data.ChordalError,data.AngularError,data.StartLimit.Type,data.EndLimit.Type,out index,data.Sense);
            if(status==AlgorithmStatus.Success)spineTags[index]=tag;return status;
        }

        internal AlgorithmStatus ChartSpine(BlendSurfaceIndex a,BlendSurfaceIndex b,scoped ReadOnlySpan<KernelVector3> positions,
            double baseParameter,double baseScale,double chordalError,double angularError,LimitType start,LimitType end,out BlendSpineIndex index,KernelSense sense=ParasolidConstants.PK_TOPOL_sense_positive_c)
        {
            index=-1;var count=positions.Length;if(count<2||nc==spines.Length||nh+count>charts.Length)return AlgorithmStatus.OutputTooSmall;
            if(start is not (LimitType.Artificial or LimitType.Help)||end is not (LimitType.Artificial or LimitType.Help))return AlgorithmStatus.Unsupported;
            var tangents=NestedWorkspace<KernelVector3>(count);var parameters=NestedWorkspace<double>(count);var scales=NestedWorkspace<double>(count-1);var chords=NestedWorkspace<KernelVector3>(count-1);
            if(tangents.IsEmpty||parameters.IsEmpty||scales.IsEmpty||chords.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
            var program=View;double u0=0,v0=0,u1=0,v1=0;
            for(var i=0;i<count;i++)
            {
                var point=TaylorVector.Constant(positions[i],0);
                TaylorJet d0,d1;TaylorVector n0,n1;
                var status=ChartSupportDistance(program,a,point,i!=0,ref u0,ref v0,out d0,out n0);if(status!=AlgorithmStatus.Success)return status;
                status=ChartSupportDistance(program,b,point,i!=0,ref u1,ref v1,out d1,out n1);if(status!=AlgorithmStatus.Success)return status;
                if(Math.Abs(d0[0])>1e-8||Math.Abs(d1[0])>1e-8)return AlgorithmStatus.InvalidInput;
                tangents[i]=Unit(EvaluationMath.Cross(n0.Value,n1.Value));
                charts[nh+i]=new(){FirstU=u0,FirstV=v0,SecondU=u1,SecondV=v1};
            }
            var built=OriginalChartParameterMap.Build(positions,tangents,baseParameter,baseScale,parameters,scales,chords,out _,out _);if(built!=AlgorithmStatus.Success)return built;
            index=nc++;spineTags[index]=0;spines[index]=new(){First=a,Second=b,ChartOffset=nh,ChartCount=count,StartType=start,EndType=end,ChordalError=chordalError,AngularError=angularError,Sense=sense};
            for(var i=0;i<count;i++)
            {
                var witness=charts[nh];witness.Position=positions[i];witness.Parameter=parameters[i];witness.Scale=i<count-1?scales[i]:1;witness.Chord=i<count-1?chords[i]:default;charts[nh++]=witness;
            }
            return AlgorithmStatus.Success;
        }

        private static AlgorithmStatus ChartSupportDistance(in NestedBlendProgram program,BlendSurfaceIndex surface,in TaylorVector point,bool seeded,
            ref double u,ref double v,out TaylorJet distance,out TaylorVector normal)
        {
            var node=program.Surfaces[surface];if(!node.IsBSpline)return NestedBlendSeriesEvaluation.Distance(program,surface,point,0,out distance,out normal);
            if(seeded)
            {
                var status=NestedSplineEvaluation.DistanceFromSeed(program,node,point,ref u,ref v,out distance,out normal);
                if(status==AlgorithmStatus.Success)return status;
            }
            return NestedSplineEvaluation.Distance(program,node,point,out distance,out normal,out u,out v);
        }

        private AlgorithmStatus BlendSpine(CurveTag tag,BlendSurfaceIndex a,BlendSurfaceIndex b,double r0,double r1,double first,double last,out BlendSpineIndex index,bool natural=false)
        {
            index=-1;var slot=GetCurveSlotByTag(tag);if(slot<0)return AlgorithmStatus.InvalidInput;
            if(Curves[slot].Class==CurveClass.ICurve)return Spine(tag,out index);
            var source=Curves[slot];if(!TryReadExplicitSpineCurve(source,out var curve))return AlgorithmStatus.Unsupported;
            if(double.IsNaN(first))first=source.TMin;if(double.IsNaN(last))last=source.TMax;
            if(!double.IsFinite(first)||!double.IsFinite(last)||!(last>first))return AlgorithmStatus.InvalidInput;
            if(ns+2>surfaces.Length)return AlgorithmStatus.OutputTooSmall;
            var left=surfaces[a];var right=surfaces[b];
            if(left.IsBlend||left.IsBSpline||right.IsBlend||right.IsBSpline)return AlgorithmStatus.Unsupported;
            var status=OffsetAnalyticBlendSupport(left.Analytic,r0*left.NormalSense,out var offset0);if(status!=AlgorithmStatus.Success)return status;
            status=OffsetAnalyticBlendSupport(right.Analytic,r1*right.NormalSense,out var offset1);if(status!=AlgorithmStatus.Success)return status;
            left.Analytic=offset0;right.Analytic=offset1;var ca=ns++;var cb=ns++;surfaces[ca]=left;surfaces[cb]=right;surfaceTags[ca]=surfaceTags[cb]=0;
            status=SpineParameterMap.Certify(curve,offset0,first,last);if(status!=AlgorithmStatus.Success)return status;
            status=SpineParameterMap.Certify(curve,offset1,first,last);if(status!=AlgorithmStatus.Success)return status;
            // Witnesses only seed inverse searches. The exact parameter map
            // and complete coefficient/interval certificates define geometry.
            var count=curve.Kind==CurveClass.Line?2:(BufferCount)Math.Ceiling((last-first)/(Math.PI/4))+1;
            if(count<2||nc==spines.Length||nh+count>charts.Length)return AlgorithmStatus.OutputTooSmall;
            var closed=natural&&curve.Kind is CurveClass.Circle or CurveClass.Ellipse&&last-first==Math.Tau;
            index=nc++;spineTags[index]=tag;spines[index]=new(){Curve=curve,First=ca,Second=cb,ChartOffset=nh,ChartCount=count,StartType=closed?LimitType.Help:LimitType.Terminator,EndType=closed?LimitType.Help:LimitType.Terminator,Sense=source.Sense};
            var startChart=nh;
            for(var i=0;i<count;i++)
            {
                var parameter=first+(last-first)*i/(count-1);status=SpineParameterMap.Series(curve,new(parameter,0),out var position,out var tangent);if(status!=AlgorithmStatus.Success)return status;
                charts[nh++]=new(){Position=position.Value,Parameter=parameter,Chord=Unit(tangent.Value),Scale=1};
            }
            if(closed)charts[nh-1].Position=charts[startChart].Position;
            return AlgorithmStatus.Success;
        }

        internal AlgorithmStatus Blend(SurfTag first,SurfTag second,CurveTag spine,double r0,double r1,out BlendSurfaceIndex index,double lower=double.NaN,double upper=double.NaN)
        {
            index=-1;var status=Surface(first,out var a);if(status!=AlgorithmStatus.Success)return status;
            status=Surface(second,out var b);if(status!=AlgorithmStatus.Success)return status;
            status=BlendSpine(spine,a,b,r0,r1,lower,upper,out var curve,double.IsNaN(lower)&&double.IsNaN(upper));if(status!=AlgorithmStatus.Success)return status;
            if(ns==surfaces.Length)return AlgorithmStatus.OutputTooSmall;
            index=ns++;surfaceTags[index]=0;surfaces[index]=new(){IsBlend=true,First=a,Second=b,Spine=curve,Range0=r0,Range1=r1,NormalSense=1};
            surfaces[index].ReverseSection=spines[curve].Sense==ParasolidConstants.PK_TOPOL_sense_negative_c;
            var definition=spines[curve];var domain=charts.Slice(definition.ChartOffset,definition.ChartCount);var u=(domain[0].Parameter+domain[^1].Parameter)*.5;
            SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[4];var view=View;
            status=JointNestedBlendEvaluation.Evaluate(view,index,u,.5,layout,jet);if(status!=AlgorithmStatus.Success)return status;
            Span<KernelVector3> center=stackalloc KernelVector3[1];status=JointNestedBlendEvaluation.EvaluateSpine(view,curve,u,0,center);if(status!=AlgorithmStatus.Success)return status;
            var orientation=Dot(EvaluationMath.Cross(jet[layout.GetIndex(1,0)],jet[layout.GetIndex(0,1)]),Sub(jet[0],center[0]));
            if(!double.IsFinite(orientation)||orientation==0)return AlgorithmStatus.Singular;
            surfaces[index].NormalSense=Math.Sign(orientation);return AlgorithmStatus.Success;
        }
    }

    private static int CompileAndBindNestedBlend(SurfTag first,SurfTag second,CurveTag spine,double r0,double r1,SurfTag* result,double lower=double.NaN,double upper=double.NaN)
    {
        var owns=EnsureCommandScratch();var checkpoint=CommandScratch.ThreadArenaOffset;
        try
        {
            var compiler=new NestedProgramCompiler();if(!compiler.Ready)return ParasolidConstants.PK_ERROR_memory_full;
            var status=compiler.Blend(first,second,spine,r0,r1,out var root,lower,upper);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
            return BindNestedBlend(first,second,spine,compiler.View,root,result);
        }
        finally{CommandScratch.ThreadArenaOffset=checkpoint;if(owns)ReleaseCommandScratch(State.Session);}
    }
}
