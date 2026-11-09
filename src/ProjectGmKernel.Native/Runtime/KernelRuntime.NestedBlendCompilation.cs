using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
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
            Span<double> knownUv=default;
            if(data.UvType!=IntersectionUvType.None)
            {
                knownUv=NestedWorkspace<double>(4*data.ChartCount);if(knownUv.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
                for(var chart=0;chart<data.ChartCount;chart++)
                {
                    if(!TryReadChartUv(data,chart,out knownUv[4*chart],out knownUv[4*chart+1],out knownUv[4*chart+2],out knownUv[4*chart+3]))return AlgorithmStatus.InvalidInput;
                }
            }
            status=ChartSpine(a,b,positions,data.BaseParameter,data.BaseScale,data.ChordalError,data.AngularError,data.StartLimit.Type,data.EndLimit.Type,out index,data.Sense,knownUv);
            if(status!=AlgorithmStatus.Success)return status;
            // The native evaluator owns this carrier's parameter map. A
            // second construction from rounded support normals can change
            // its CHART limits even though the spatial curve is identical.
            // Keep the native gauges when adapting it to the joint solver.
            status=TryPrepareICurveView(in Curves[slot],out var native,out _);if(status!=AlgorithmStatus.Success)return status;
            var offset=spines[index].ChartOffset;
            for(var chart=0;chart<native.ChartParameters.Length;chart++)
            {
                ref var witness=ref charts[offset+chart];witness.Parameter=native.ChartParameters[chart];
                if(chart<native.ChartScales.Length){witness.Scale=native.ChartScales[chart];witness.Chord=native.ChartChordUnits[chart];}
            }
            spineTags[index]=tag;return AlgorithmStatus.Success;
        }

        internal AlgorithmStatus ChartSpine(BlendSurfaceIndex a,BlendSurfaceIndex b,scoped ReadOnlySpan<KernelVector3> positions,
            double baseParameter,double baseScale,double chordalError,double angularError,LimitType start,LimitType end,out BlendSpineIndex index,KernelSense sense=ParasolidConstants.PK_TOPOL_sense_positive_c,
            scoped ReadOnlySpan<double> knownUv=default)
        {
            index=-1;var count=positions.Length;if(count<2||nc==spines.Length||nh+count>charts.Length)return AlgorithmStatus.OutputTooSmall;
            if(!knownUv.IsEmpty&&knownUv.Length!=4*count)return AlgorithmStatus.InvalidInput;
            if(start is not (LimitType.Artificial or LimitType.Help)||end is not (LimitType.Artificial or LimitType.Help))return AlgorithmStatus.Unsupported;
            var tangents=NestedWorkspace<KernelVector3>(count);var parameters=NestedWorkspace<double>(count);var scales=NestedWorkspace<double>(count-1);var chords=NestedWorkspace<KernelVector3>(count-1);
            if(tangents.IsEmpty||parameters.IsEmpty||scales.IsEmpty||chords.IsEmpty)return AlgorithmStatus.WorkspaceTooSmall;
            var program=View;double u0=0,v0=0,u1=0,v1=0;
            for(var i=0;i<count;i++)
            {
                var point=TaylorVector.Constant(positions[i],0);
                TaylorJet d0,d1;TaylorVector n0,n1;
                var firstKnown=!knownUv.IsEmpty&&double.IsFinite(knownUv[4*i])&&double.IsFinite(knownUv[4*i+1]);
                var secondKnown=!knownUv.IsEmpty&&double.IsFinite(knownUv[4*i+2])&&double.IsFinite(knownUv[4*i+3]);
                if(firstKnown){u0=knownUv[4*i];v0=knownUv[4*i+1];}if(secondKnown){u1=knownUv[4*i+2];v1=knownUv[4*i+3];}
                var status=!firstKnown?ChartSupportDistance(program,a,point,i!=0,ref u0,ref v0,out d0,out n0)
                    :ChartSupportWitness(program,a,surfaceTags[a],positions[i],u0,v0,out d0,out n0);if(status!=AlgorithmStatus.Success)
                {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"ICurve anchor={i}/{count} first distance={status}");return status;}
                status=!secondKnown?ChartSupportDistance(program,b,point,i!=0,ref u1,ref v1,out d1,out n1)
                    :ChartSupportWitness(program,b,surfaceTags[b],positions[i],u1,v1,out d1,out n1);if(status!=AlgorithmStatus.Success)
                {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"ICurve anchor={i}/{count} second distance={status}");return status;}
                if(Math.Abs(d0[0])>1e-8||Math.Abs(d1[0])>1e-8)
                {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"ICurve CHART anchor={i}/{count} supports={a}/{b} residual={d0[0]:R}/{d1[0]:R} point=({positions[i].X:R},{positions[i].Y:R},{positions[i].Z:R})");return AlgorithmStatus.InvalidInput;}
                tangents[i]=Unit(EvaluationMath.Cross(n0.Value,n1.Value));
                charts[nh+i]=new(){FirstU=u0,FirstV=v0,SecondU=u1,SecondV=v1};
            }
            var built=OriginalChartParameterMap.Build(positions,tangents,baseParameter,baseScale,parameters,scales,chords,out _,out var failure);if(built!=AlgorithmStatus.Success)
            {if(Environment.GetEnvironmentVariable("PGM_XT_DIAGNOSTICS")=="1")Console.Error.WriteLine($"ICurve CHART map={failure} sense={sense} support_senses={surfaces[a].NormalSense}/{surfaces[b].NormalSense} first_direction={Dot(tangents[0],Sub(positions[1],positions[0])):R}");return built;}
            index=nc++;spineTags[index]=0;spines[index]=new(){First=a,Second=b,ChartOffset=nh,ChartCount=count,StartType=start,EndType=end,ChordalError=chordalError,AngularError=angularError,Sense=sense};
            for(var i=0;i<count;i++)
            {
                var witness=charts[nh];witness.Position=positions[i];witness.Parameter=parameters[i];witness.Scale=i<count-1?scales[i]:1;witness.Chord=i<count-1?chords[i]:default;charts[nh++]=witness;
            }
            return AlgorithmStatus.Success;
        }

        internal AlgorithmStatus RefinePlaneRollingChart(ref BlendSpineIndex index,scoped Span<KernelVector3> positions,scoped Span<double> uv)
        {
            var definition=spines[index];var a=surfaces[definition.First];var b=surfaces[definition.Second];
            if(!a.IsBlend)(a,b)=(b,a);
            if(!a.IsBlend||b.IsBlend||b.IsBSpline||b.Analytic.Kind!=SurfaceClass.Plane
                ||Math.Abs(a.Range0)!=Math.Abs(a.Range1)
                ||spines[a.Spine].Curve.Kind is not (CurveClass.Circle or CurveClass.Ellipse))return AlgorithmStatus.Success;
            if(index!=nc-1||definition.ChartOffset+definition.ChartCount!=nh)return AlgorithmStatus.InvalidInput;

            // A small plane residual can conceal a large tangential station
            // error on a shallow cut. XT consumers may return a stored row
            // directly. Certify the joint root in its original chart gauge
            // before replacing a seed; terminal events remain fixed.
            var program=View;var chart=program.Charts.Slice(definition.ChartOffset,definition.ChartCount);
            Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];
            Span<KernelVector3> point=stackalloc KernelVector3[1];Span<double> contacts=stackalloc double[4];
            var motion=0.0;
            for(var i=1;i+1<chart.Length;i++)
            {
                var t=chart[i].Parameter;
                var status=JointNestedBlendEvaluation.BoundSpine(program,index,new(t,t,0,0),bounds);
                if(status!=AlgorithmStatus.Success)return status;
                var root=new IntervalVector(bounds[0],bounds[1],bounds[2]);
                var previous=(root-new IntervalVector(positions[i])).Norm().Value;
                if(!previous.Finite)return AlgorithmStatus.NotConverged;
                var accuracy=JointNestedBlendEvaluation.ResidualAccuracy(program,index,positions[i]);
                // A certified roundoff-sized displacement needs no repair.
                // Use the upper bound so an uncertain root cannot qualify.
                if(previous.Upper<=8*accuracy)continue;
                status=JointNestedBlendEvaluation.EvaluateSpine(program,index,t,0,point,contacts);
                if(status!=AlgorithmStatus.Success)return status;
                var corrected=(root-new IntervalVector(point[0])).Norm().Value;
                if(!corrected.Finite||corrected.Upper>=previous.Lower)
                {
                    // In a shallow intersection a small equation residual
                    // does not guarantee a better point along the surfaces.
                    status=JointNestedBlendEvaluation.BoundSpineAnchor(program,index,t,bounds,contacts);
                    if(status!=AlgorithmStatus.Success)return status;
                    root=new(bounds[0],bounds[1],bounds[2]);
                    previous=(root-new IntervalVector(positions[i])).Norm().Value;
                    point[0]=new(){X=bounds[0].Midpoint,Y=bounds[1].Midpoint,Z=bounds[2].Midpoint};
                    corrected=(root-new IntervalVector(point[0])).Norm().Value;
                    if(!previous.Finite||!corrected.Finite||corrected.Upper>=previous.Lower)return AlgorithmStatus.NotConverged;
                }
                var seed=new IntervalVector(chart[i].Position);
                var left=(seed-new IntervalVector(chart[i-1].Position)).Norm().Value;
                var right=(seed-new IntervalVector(chart[i+1].Position)).Norm().Value;
                // Keep the entire certified root and its representative in
                // the seed's own neighborhood. Half the shorter adjacent
                // chord makes neighboring repair neighborhoods disjoint.
                var local=(.5*new IntervalNumber(Math.Min(left.Lower,right.Lower))).Lower;
                var moved=(new IntervalVector(point[0])-seed).Norm().Value;
                if(!left.Finite||!right.Finite||!moved.Finite||Math.Max(previous.Upper,moved.Upper)>=local)return AlgorithmStatus.NotConverged;
                positions[i]=point[0];contacts.CopyTo(uv.Slice(4*i,4));motion=Math.Max(motion,moved.Upper);
            }
            if(motion==0)return AlgorithmStatus.Success;
            for(var i=0;i+1<chart.Length;i++)
            {
                var chord=new IntervalVector(chart[i].Chord);
                var separation=IntervalVector.Dot(new IntervalVector(positions[i+1])-new IntervalVector(positions[i]),chord).Value;
                if(!separation.Finite||separation.Lower<=0)return AlgorithmStatus.NotConverged;
            }
            nc=index;nh=definition.ChartOffset;
            var error=(new IntervalNumber(definition.ChordalError)+motion).Upper;
            return ChartSpine(definition.First,definition.Second,positions,0,1,error,definition.AngularError,
                definition.StartType,definition.EndType,out index,knownUv:uv);
        }

        private static AlgorithmStatus ChartSupportWitness(in NestedBlendProgram program,BlendSurfaceIndex surface,SurfTag tag,
            in KernelVector3 point,double u,double v,out TaylorJet distance,out TaylorVector normal)
        {
            distance=default;normal=default;var slot=GetSurfaceSlotByTag(tag);if(slot<0)return AlgorithmStatus.InvalidInput;
            SurfaceDerivativeLayout.TryCreate(1,1,out var layout);Span<KernelVector3> jets=stackalloc KernelVector3[4];
            var status=EvaluateSurfaceCore(in Surfaces[slot],u,v,layout,jets,0);if(status!=AlgorithmStatus.Success)return status;
            var error=OperationIntersection.Norm(Sub(point,jets[0]));if(error>OperationIntersection.DistanceTolerance)return AlgorithmStatus.InvalidInput;
            var node=program.Surfaces[surface];KernelVector3 direction;
            if(node.IsBlend)
            {
                status=NestedBlendSeriesEvaluation.Spine(program,node.Spine,u,0,0,out var center);if(status!=AlgorithmStatus.Success)return status;
                direction=Scale(Unit(Sub(jets[0],center.Value)),node.NormalSense);
            }
            else if(node.IsBSpline)direction=Scale(Unit(EvaluationMath.Cross(jets[layout.GetIndex(1,0)],jets[layout.GetIndex(0,1)])),node.NormalSense);
            else
            {
                status=AnalyticDistanceSeries.Evaluate(node.Analytic,TaylorVector.Constant(jets[0],0),out _,out var analyticNormal);if(status!=AlgorithmStatus.Success)return status;
                direction=Scale(analyticNormal.Value,node.NormalSense);
            }
            distance=new(error,0);normal=TaylorVector.Constant(direction,0);return AlgorithmStatus.Success;
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
            var source=Curves[slot];var splineSpine=source.Class==CurveClass.BCurve;
            if(!TryReadExplicitSpineCurve(source,out var curve)&&!splineSpine)return AlgorithmStatus.Unsupported;
            if(double.IsNaN(first))first=source.TMin;if(double.IsNaN(last))last=source.TMax;
            if(!double.IsFinite(first)||!double.IsFinite(last)||!(last>first))return AlgorithmStatus.InvalidInput;
            if(ns+2>surfaces.Length)return AlgorithmStatus.OutputTooSmall;
            var left=surfaces[a];var right=surfaces[b];
            if(left.IsBlend||left.IsBSpline||right.IsBlend||right.IsBSpline)return AlgorithmStatus.Unsupported;
            var status=OffsetAnalyticBlendSupport(left.Analytic,r0*left.NormalSense,out var offset0);if(status!=AlgorithmStatus.Success)return status;
            status=OffsetAnalyticBlendSupport(right.Analytic,r1*right.NormalSense,out var offset1);if(status!=AlgorithmStatus.Success)return status;
            left.Analytic=offset0;right.Analytic=offset1;var ca=ns++;var cb=ns++;surfaces[ca]=left;surfaces[cb]=right;surfaceTags[ca]=surfaceTags[cb]=0;
            if(splineSpine)return CompileRationalSpine(tag,source,first,last,ca,cb,offset0,offset1,out index);
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

        private AlgorithmStatus CompileRationalSpine(CurveTag tag,in CurveRecord source,double first,double last,
            BlendSurfaceIndex a,BlendSurfaceIndex b,in AnalyticSurface left,in AnalyticSurface right,out BlendSpineIndex index)
        {
            index=-1;var spline=GetBCurveView(BCurveDataStore[source.DataIndex]);var count=RationalSpine.SpanCount(spline);
            if(count==0||first!=spline.Start||last!=spline.End)return AlgorithmStatus.Unsupported;
            if(nc==spines.Length||nh+count+1>charts.Length)return AlgorithmStatus.OutputTooSmall;
            if(nd+spline.Vertices.Length+spline.Knots.Length>scalars.Length)return AlgorithmStatus.WorkspaceTooSmall;
            var status=RationalSpine.Certify(spline,left);if(status!=AlgorithmStatus.Success)return status;
            status=RationalSpine.Certify(spline,right);if(status!=AlgorithmStatus.Success)return status;
            status=RationalSpine.CertifyRegular(spline,count);if(status!=AlgorithmStatus.Success)return status;
            var offset=nd;spline.Vertices.CopyTo(scalars[nd..]);nd+=spline.Vertices.Length;
            spline.Knots.CopyTo(scalars[nd..]);nd+=spline.Knots.Length;
            index=nc++;spineTags[index]=tag;spines[index]=new(){Curve=new(){Kind=CurveClass.BCurve},First=a,Second=b,
                ChartOffset=nh,ChartCount=count+1,StartType=LimitType.Terminator,EndType=LimitType.Terminator,Sense=source.Sense,
                SpineScalarOffset=offset,SpineDegree=spline.Degree};
            for(var i=0;i<=count;i++)
            {
                var parameter=RationalSpine.Boundary(spline,i);
                status=RationalSpine.Series(spline,new(parameter,0),out var position,out var tangent);if(status!=AlgorithmStatus.Success)return status;
                charts[nh++]=new(){Position=position.Value,Parameter=parameter,Chord=Unit(tangent.Value),Scale=1};
            }
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
