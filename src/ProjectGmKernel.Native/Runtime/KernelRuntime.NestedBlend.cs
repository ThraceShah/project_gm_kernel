using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BlendSfAsk(SurfTag surface,PK_BLENDSF_sf_s* result)
    {
        var command=new BlendAskCommand{Surface=surface,Result=result};
        if(Dispatcher.IsExecuting)return command.Execute();
        return Dispatch(ApiId.BlendSfAsk,ConcurrencyKind.Local,AccessKind.ReadOnly,ref command,surface);
    }
    private struct BlendAskCommand:IKernelCommand
    {
        internal SurfTag Surface;
        internal PK_BLENDSF_sf_s* Result;
        public int Execute()
        {
            if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
            if(Result==null)return ParasolidConstants.PK_ERROR_bad_parameter;
            var slot=GetSurfaceSlotByTag(Surface);if(slot<0||Surfaces[slot].Class!=SurfaceClass.BlendSurface)return ParasolidConstants.PK_ERROR_bad_class;
            ref readonly var data=ref BlendedEdgeDataPool[Surfaces[slot].DataIndex];var value=new PK_BLENDSF_sf_s{geom_1=data.Surface0Tag,geom_2=data.Surface1Tag,spine=data.SpineTag};
            value.radii[0]=data.Range0;value.radii[1]=data.Range1;value.spine_extent.value[0]=Surfaces[slot].UMin;value.spine_extent.value[1]=Surfaces[slot].UMax;*Result=value;return 0;
        }
    }

    private static nuint NestedBlendProgramBytes(BufferCount surfaces,BufferCount spines,BufferCount charts,BufferCount scalars=0)
        =>(nuint)(surfaces*sizeof(BlendSurfaceInstruction)+spines*sizeof(BlendSpineInstruction)+charts*sizeof(BlendChartInstruction)+scalars*8);
    internal static int BindNestedBlend(SurfTag first,SurfTag second,CurveTag spine,
        in NestedBlendProgram program,BlendSurfaceIndex root,SurfTag* result)
    {
        if(result!=null)*result=0;
        fixed(BlendSurfaceInstruction* surfaces=program.Surfaces)
        fixed(BlendSpineInstruction* spines=program.Spines)
        fixed(BlendChartInstruction* charts=program.Charts)
        fixed(double* scalars=program.Scalars)
        {
            var command=new BindNestedBlendCommand{First=first,Second=second,Spine=spine,Surfaces=surfaces,Spines=spines,Charts=charts,
                SurfaceCount=program.Surfaces.Length,SpineCount=program.Spines.Length,ChartCount=program.Charts.Length,ScalarCount=program.Scalars.Length,Scalars=scalars,Root=root,Result=result};
            if(Dispatcher.IsExecuting)return command.Execute();
            return Dispatch(ApiId.SurfCreateBlend,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,first);
        }
    }

    internal static int BindNestedBlendSpine(SurfTag first,SurfTag second,in NestedBlendProgram program,BlendSpineIndex root,CurveTag* result,ReadOnlySpan<double> uv=default)
    {
        if(result!=null)*result=0;
        fixed(BlendSurfaceInstruction* surfaces=program.Surfaces)
        fixed(BlendSpineInstruction* spines=program.Spines)
        fixed(BlendChartInstruction* charts=program.Charts)
        fixed(double* scalars=program.Scalars)
        fixed(double* witnesses=uv)
        {
            var command=new BindNestedBlendCommand{First=first,Second=second,Surfaces=surfaces,Spines=spines,Charts=charts,
                SurfaceCount=program.Surfaces.Length,SpineCount=program.Spines.Length,ChartCount=program.Charts.Length,ScalarCount=program.Scalars.Length,Scalars=scalars,CurveRoot=root,IsCurve=true,Result=result,Uv=witnesses,UvCount=uv.Length};
            if(Dispatcher.IsExecuting)return command.Execute();
            return Dispatch(ApiId.SurfCreateBlend,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,first);
        }
    }

    private struct BindNestedBlendCommand:IKernelCommand
    {
        internal SurfTag First,Second;
        internal CurveTag Spine;
        internal BlendSurfaceInstruction* Surfaces;
        internal BlendSpineInstruction* Spines;
        internal BlendChartInstruction* Charts;
        internal double* Scalars;
        internal double* Uv;
        internal BufferCount UvCount;
        internal BufferCount SurfaceCount,SpineCount,ChartCount,ScalarCount;
        internal BlendSurfaceIndex Root;
        internal BlendSpineIndex CurveRoot;
        internal bool IsCurve;
        internal GeomTag* Result;
        public int Execute()
        {
            if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
            if(Result==null||(!IsCurve&&(Root<0||Root>=SurfaceCount))||IsCurve&&(CurveRoot<0||CurveRoot>=SpineCount)||SurfaceCount is <1 or >128||SpineCount is <1 or >128||ChartCount is <2 or >4096||ScalarCount is <0 or >65536)return ParasolidConstants.PK_ERROR_bad_parameter;
            var program=new NestedBlendProgram(new(Surfaces,SurfaceCount),new(Spines,SpineCount),new(Charts,ChartCount),new(Scalars,ScalarCount),CommandScratch.Current.AvailableSpanOfDoubles());
            if(IsCurve)return BindNestedBlendSpineCore(First,Second,program,CurveRoot,Result,new(Uv,UvCount));
            var root=program.Surfaces[Root];if(!root.IsBlend||root.Spine<0||root.Spine>=SpineCount)return ParasolidConstants.PK_ERROR_bad_parameter;
            var firstSlot=GetSurfaceSlotByTag(First);var secondSlot=GetSurfaceSlotByTag(Second);var spineSlot=GetCurveSlotByTag(Spine);
            if(firstSlot<0||secondSlot<0||spineSlot<0)return ParasolidConstants.PK_ERROR_bad_class;
            var error=CheckGeometryDependency(PoolKind.Surface,firstSlot);if(error!=0)return error;
            error=CheckGeometryDependency(PoolKind.Surface,secondSlot);if(error!=0)return error;
            error=CheckGeometryDependency(PoolKind.Curve,spineSlot);if(error!=0)return error;
            var bytes=checked((int)NestedBlendProgramBytes(SurfaceCount,SpineCount,ChartCount,ScalarCount));
            if(!BlendedEdgeDataPool.TryAllocate(out var dataSlot))return ParasolidConstants.PK_ERROR_memory_full;
            ref var data=ref BlendedEdgeDataPool[dataSlot];data.ProgramBlock=-1;data.HvecBlock=-1;
            var payload=State.Session->Blocks.TryAllocate((nuint)bytes);if(payload==null){BlendedEdgeDataPool.Free(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
            data.ProgramBlock=State.Session->Blocks.HandleOf(payload);
            if(data.ProgramBlock<=0){State.Session->Blocks.Free(payload);BlendedEdgeDataPool.Free(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
            data.ProgramSurfaceCount=SurfaceCount;data.ProgramSpineCount=SpineCount;data.ProgramChartCount=ChartCount;data.ProgramRoot=Root;data.ProgramScalarCount=ScalarCount;
            var target=(byte*)payload;
            program.Surfaces.CopyTo(new Span<BlendSurfaceInstruction>(target,SurfaceCount));target+=SurfaceCount*sizeof(BlendSurfaceInstruction);
            program.Spines.CopyTo(new Span<BlendSpineInstruction>(target,SpineCount));target+=SpineCount*sizeof(BlendSpineInstruction);
            program.Charts.CopyTo(new Span<BlendChartInstruction>(target,ChartCount));
            target+=ChartCount*sizeof(BlendChartInstruction);program.Scalars.CopyTo(new Span<double>(target,ScalarCount));
            data.BlendType=BlendType.RollingBall;data.Surface0Tag=First;data.Surface1Tag=Second;data.SpineTag=Spine;
            data.Range0=root.Range0;data.Range1=root.Range1;data.ThumbWeight0=data.ThumbWeight1=1;
            if(!KernelRuntime.Surfaces.TryAllocate(out var slot)){FreeNestedBlendData(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
            ref var surface=ref KernelRuntime.Surfaces[slot];AssignPartition(ref surface.Header,CurrentPartition);
            surface.Class=SurfaceClass.BlendSurface;surface.DataIndex=dataSlot;surface.OwnerFace=-1;
            surface.Sense=ParasolidConstants.PK_TOPOL_sense_positive_c;
            var source=program.Spines[root.Spine];var domain=program.Charts.Slice(source.ChartOffset,source.ChartCount);
            // A clamped rational spine already has finite native knot
            // limits. Do not turn its natural ends into explicit XT T limits.
            if(SpineParameterMap.Explicit(source)&&source.Curve.Kind!=CurveClass.BCurve
                &&source.StartType==LimitType.Terminator&&source.EndType==LimitType.Terminator)
            {
                var limits=(KernelVector3*)State.Session->Blocks.TryAllocate(2*(nuint)sizeof(KernelVector3));
                if(limits==null){FreeNestedBlendData(dataSlot);KernelRuntime.Surfaces.Free(slot);return ParasolidConstants.PK_ERROR_memory_full;}
                data.HvecBlock=State.Session->Blocks.HandleOf(limits);data.HvecCount=2;
                var reverse=source.Sense==ParasolidConstants.PK_TOPOL_sense_negative_c;
                limits[0]=domain[reverse?^1:0].Position;limits[1]=domain[reverse?0:^1].Position;
                data.StartLimit=new(){Type=LimitType.Terminator,TermUse=LimitTermUse.Unset,HvecCount=1,HvecIndex=0};
                data.EndLimit=new(){Type=LimitType.Terminator,TermUse=LimitTermUse.Unset,HvecCount=1,HvecIndex=1};
            }
            surface.UMin=domain[0].Parameter;surface.UMax=domain[^1].Parameter;surface.VMin=0;surface.VMax=1;
            var tag=AllocateTag(EntityClass.Surface,PoolKind.Surface,slot,surface.Header.Generation);
            if(tag<=0){FreeNestedBlendData(dataSlot);KernelRuntime.Surfaces.Free(slot);return ParasolidConstants.PK_ERROR_memory_full;}
            AcquireGeometryDependency(PoolKind.Surface,firstSlot);AcquireGeometryDependency(PoolKind.Surface,secondSlot);AcquireGeometryDependency(PoolKind.Curve,spineSlot);surface.DependenciesAcquired=1;
            error=TryAdoptDependentGeometry(tag);if(error!=0)return error;*Result=tag;return 0;
        }
    }

    internal static NestedBlendProgram GetNestedBlendProgram(scoped in BlendedEdgeData data)
        =>GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);

    internal static NestedBlendProgram GetNestedBlendProgram(DataSlot block,BufferCount surfaceCount,BufferCount spineCount,BufferCount chartCount,BufferCount scalarCount=0)
    {
        var pointer=(byte*)DereferenceBlock(block);
        var surfaces=new ReadOnlySpan<BlendSurfaceInstruction>(pointer,surfaceCount);pointer+=surfaceCount*sizeof(BlendSurfaceInstruction);
        var spines=new ReadOnlySpan<BlendSpineInstruction>(pointer,spineCount);pointer+=spineCount*sizeof(BlendSpineInstruction);
        var charts=new ReadOnlySpan<BlendChartInstruction>(pointer,chartCount);pointer+=chartCount*sizeof(BlendChartInstruction);
        return new(surfaces,spines,charts,new ReadOnlySpan<double>(pointer,scalarCount),CommandScratch.Current.AvailableSpanOfDoubles(),block);
    }

    private static int BindNestedBlendSpineCore(SurfTag first,SurfTag second,in NestedBlendProgram program,BlendSpineIndex root,CurveTag* result,ReadOnlySpan<double> uv)
    {
        var a=GetSurfaceSlotByTag(first);var b=GetSurfaceSlotByTag(second);if(a<0||b<0)return ParasolidConstants.PK_ERROR_bad_class;
        var error=CheckGeometryDependency(PoolKind.Surface,a);if(error!=0)return error;
        error=CheckGeometryDependency(PoolKind.Surface,b);if(error!=0)return error;
        var source=program.Spines[root];var charts=program.Charts.Slice(source.ChartOffset,source.ChartCount);
        if(charts.Length<2||!uv.IsEmpty&&uv.Length!=4*charts.Length)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(!ICurveDataPool.TryAllocate(out var dataSlot))return ParasolidConstants.PK_ERROR_memory_full;
        ref var data=ref ICurveDataPool[dataSlot];data.HvecBlock=data.UvValueBlock=data.ProgramBlock=-1;
        data.Surface0Tag=first;data.Surface1Tag=second;data.BaseParameter=charts[0].Parameter;data.BaseScale=charts[0].Scale;
        data.ChartCount=charts.Length;data.HvecCount=charts.Length+2;data.UvType=IntersectionUvType.None;data.ExtendedChartCount=0;
        data.Sense=source.Sense==ParasolidConstants.PK_TOPOL_sense_negative_c?source.Sense:ParasolidConstants.PK_TOPOL_sense_positive_c;data.ChordalError=source.ChordalError;data.AngularError=source.AngularError;
        var hull=(KernelVector3*)State.Session->Blocks.TryAllocate((nuint)(data.HvecCount*sizeof(KernelVector3)));
        if(hull==null){FreeICurveData(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
        data.HvecBlock=State.Session->Blocks.HandleOf(hull);
        for(var i=0;i<charts.Length;i++)hull[i+1]=charts[i].Position;hull[0]=hull[1];hull[data.HvecCount-1]=hull[data.HvecCount-2];
        if(!uv.IsEmpty)
        {
            var witnesses=State.Session->Blocks.TryAllocate((nuint)(uv.Length*sizeof(double)));if(witnesses==null){FreeICurveData(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
            data.UvValueBlock=State.Session->Blocks.HandleOf(witnesses);data.UvType=IntersectionUvType.Both;data.UvValueCount=uv.Length;
            uv.CopyTo(new Span<double>(witnesses,uv.Length));
        }
        data.StartLimit=new(){Type=source.StartType==LimitType.Help?LimitType.Help:LimitType.Artificial,TermUse=LimitTermUse.Unset,HvecCount=1,HvecIndex=0};
        data.EndLimit=new(){Type=source.EndType==LimitType.Help?LimitType.Help:LimitType.Artificial,TermUse=LimitTermUse.Unset,HvecCount=1,HvecIndex=data.HvecCount-1};
        var bytes=(int)NestedBlendProgramBytes(program.Surfaces.Length,program.Spines.Length,program.Charts.Length,program.Scalars.Length);
        var payload=State.Session->Blocks.TryAllocate((nuint)bytes);if(payload==null){FreeICurveData(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
        data.ProgramBlock=State.Session->Blocks.HandleOf(payload);data.ProgramSurfaceCount=program.Surfaces.Length;data.ProgramSpineCount=program.Spines.Length;data.ProgramChartCount=program.Charts.Length;data.ProgramSpine=root;data.ProgramScalarCount=program.Scalars.Length;
        var pointer=(byte*)payload;program.Surfaces.CopyTo(new Span<BlendSurfaceInstruction>(pointer,program.Surfaces.Length));pointer+=program.Surfaces.Length*sizeof(BlendSurfaceInstruction);
        program.Spines.CopyTo(new Span<BlendSpineInstruction>(pointer,program.Spines.Length));pointer+=program.Spines.Length*sizeof(BlendSpineInstruction);
        program.Charts.CopyTo(new Span<BlendChartInstruction>(pointer,program.Charts.Length));
        pointer+=program.Charts.Length*sizeof(BlendChartInstruction);program.Scalars.CopyTo(new Span<double>(pointer,program.Scalars.Length));
        if(!Curves.TryAllocate(out var slot)){FreeICurveData(dataSlot);return ParasolidConstants.PK_ERROR_memory_full;}
        ref var curve=ref Curves[slot];AssignPartition(ref curve.Header,CurrentPartition);curve.Class=CurveClass.ICurve;curve.DataIndex=dataSlot;curve.Sense=data.Sense;
        curve.OwnerEdge=-1;curve.TMin=charts[0].Parameter;curve.TMax=charts[^1].Parameter;
        var tag=AllocateTag(EntityClass.Curve,PoolKind.Curve,slot,curve.Header.Generation);
        if(tag<=0){FreeICurveData(dataSlot);Curves.Free(slot);return ParasolidConstants.PK_ERROR_memory_full;}
        AcquireGeometryDependency(PoolKind.Surface,a);AcquireGeometryDependency(PoolKind.Surface,b);curve.DependenciesAcquired=1;data.OwnsSupportSurfaces=1;
        error=TryAdoptDependentGeometry(tag);if(error!=0)return error;*result=tag;return 0;
    }

    private static void FreeNestedBlendData(DataSlot slot)
    {
        var data=BlendedEdgeDataPool[slot];if(data.ProgramBlock>0)State.Session->Blocks.Free(DereferenceBlock(data.ProgramBlock));
        if(data.HvecBlock>0)State.Session->Blocks.Free(DereferenceBlock(data.HvecBlock));
        BlendedEdgeDataPool.Free(slot);
    }

    private static int NestedBlendIsoparam(SurfTag surface,bool constantU,double fixedValue,double start,double end,
        out CurveTag curve,out double lower,out double upper)
    {
        curve=0;lower=start;upper=end;
        if(!constantU)
        {
            Span<double> poles=stackalloc double[4]{start,fixedValue,end,fixedValue};Span<double> knots=stackalloc double[2]{start,end};Span<int> mults=stackalloc int[2]{2,2};
            CurveTag parameterCurve=0;var error=CreateFreeformCurve(1,poles,2,false,knots,mults,false,&parameterCurve);if(error!=0)return error;
            var definition=new PK_SPCURVE_sf_s{surf=surface,curve=parameterCurve};CurveTag result=0;error=SpCurveCreateImplementation(&definition,&result);curve=result;return error;
        }
        var record=Surfaces[GetSurfaceSlotByTag(surface)];var data=BlendedEdgeDataPool[record.DataIndex];var program=GetNestedBlendProgram(data);
        Span<KernelVector3> center=stackalloc KernelVector3[1];var found=JointNestedBlendEvaluation.EvaluateSpine(program,program.Surfaces[data.ProgramRoot].Spine,fixedValue,0,center);
        if(found!=Computation.AlgorithmStatus.Success)return EvaluationError(found);
        SurfaceDerivativeLayout.TryCreate(0,1,out var layout);Span<KernelVector3> jet=stackalloc KernelVector3[2];
        found=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,fixedValue,start,layout,jet);if(found!=Computation.AlgorithmStatus.Success)return EvaluationError(found);
        var radius=Math.Abs(data.Range0);var radial=ProfileScale(EvaluationMath.Sub(jet[0],center[0]),1/radius);var tangent=jet[1];var speed=Math.Sqrt(ProfileDot(tangent,tangent));
        if(!(speed>0))return ParasolidConstants.PK_ERROR_at_singularity;
        tangent=ProfileScale(tangent,1/speed);var axis=ProfileCross(radial,tangent);
        curve=CreateCircleCurveTag(center[0].X,center[0].Y,center[0].Z,axis.X,axis.Y,axis.Z,radial.X,radial.Y,radial.Z,radius);
        lower=0;upper=(end-start)*speed/radius;return curve>0?0:ParasolidConstants.PK_ERROR_memory_full;
    }

    private static int ParameteriseNestedSpine(in ICurveData data,in KernelVector3 point,double* parameter)
    {
        var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
        var source=program.Spines[data.ProgramSpine];var charts=program.Charts.Slice(source.ChartOffset,source.ChartCount);
        Span<KernelVector3> evaluated=stackalloc KernelVector3[1];
        for(var i=0;i<charts.Length-1;i++)
        {
            var candidate=charts[i].Parameter+charts[i].Scale*EvaluationMath.Dot(charts[i].Chord,EvaluationMath.Sub(point,charts[i].Position));
            if(candidate<charts[i].Parameter-1e-10||candidate>charts[i+1].Parameter+1e-10)continue;
            candidate=Math.Clamp(candidate,charts[i].Parameter,charts[i+1].Parameter);
            var status=JointNestedBlendEvaluation.EvaluateSpine(program,data.ProgramSpine,candidate,0,evaluated);
            if(status==Computation.AlgorithmStatus.Success&&Geometry.Intersection.OperationIntersection.Norm(EvaluationMath.Sub(evaluated[0],point))<=1e-8)
            {*parameter=candidate;return 0;}
        }
        return ParasolidConstants.PK_ERROR_not_on_curve;
    }

    private static Computation.AlgorithmStatus ParameteriseNestedSurface(in SurfaceRecord surface,in KernelVector3 point,out PK_UV_s uv)
    {
        uv=default;var data=BlendedEdgeDataPool[surface.DataIndex];var program=GetNestedBlendProgram(data);var root=program.Surfaces[data.ProgramRoot];
        var path=Computation.Numerics.TaylorVector.Constant(point,0);
        var status=JointNestedBlendEvaluation.Distance(program,data.ProgramRoot,path,out var distance,out var normal);if(status!=Computation.AlgorithmStatus.Success)return status;
        if(Math.Abs(distance[0])>Geometry.Intersection.OperationIntersection.DistanceTolerance)return Computation.AlgorithmStatus.InvalidInput;
        var center=EvaluationMath.Sub(point,EvaluationMath.Scale(normal.Value,distance[0]+Math.Abs(root.Range0)*root.NormalSense));var vector=new PK_VECTOR_s();WriteVector(&vector,center);double parameter;
        var error=CurveParameteriseVectorCore(data.SpineTag,vector,&parameter);if(error!=0)return Computation.AlgorithmStatus.NotConverged;
        // The conic inverse returns its natural chart. The normal-plane
        // solve above used the finite blend chart, which can cross that seam.
        LiftFiniteConicBlendParameter(surface,data.SpineTag,ref parameter);
        SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> start=stackalloc KernelVector3[1],end=stackalloc KernelVector3[1];
        status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,parameter,0,layout,start);if(status!=Computation.AlgorithmStatus.Success)return status;
        status=JointNestedBlendEvaluation.Evaluate(program,data.ProgramRoot,parameter,1,layout,end);if(status!=Computation.AlgorithmStatus.Success)return status;
        var x=EvaluationMath.Unit(EvaluationMath.Sub(start[0],center));var last=EvaluationMath.Unit(EvaluationMath.Sub(end[0],center));var cosine=EvaluationMath.Dot(x,last);
        var transverse=EvaluationMath.Sub(last,EvaluationMath.Scale(x,cosine));var sine=Math.Sqrt(EvaluationMath.Dot(transverse,transverse));if(!(sine>1e-7))return Computation.AlgorithmStatus.Singular;
        var y=EvaluationMath.Scale(transverse,1/sine);var offset=EvaluationMath.Sub(point,center);var v=Math.Atan2(EvaluationMath.Dot(offset,y),EvaluationMath.Dot(offset,x))/Math.Atan2(sine,cosine);
        if(!double.IsFinite(v)||v< -1e-8||v>1+1e-8)return Computation.AlgorithmStatus.InvalidInput;
        uv.param[0]=parameter;uv.param[1]=Math.Clamp(v,0,1);return Computation.AlgorithmStatus.Success;
    }

    private static void LiftFiniteConicBlendParameter(in SurfaceRecord surface,CurveTag spine,ref double u)
    {
        if(!double.IsFinite(u)||u>=surface.UMin&&u<=surface.UMax||!(surface.UMax-surface.UMin<Math.Tau))return;
        var slot=GetCurveSlotByTag(spine);if(slot<0||Curves[slot].Class is not (CurveClass.Circle or CurveClass.Ellipse))return;
        var middle=surface.UMin+(surface.UMax-surface.UMin)*.5;var shifted=u+Math.Tau*Math.Round((middle-u)/Math.Tau);
        if(shifted>=surface.UMin&&shifted<=surface.UMax)u=shifted;
    }
}
