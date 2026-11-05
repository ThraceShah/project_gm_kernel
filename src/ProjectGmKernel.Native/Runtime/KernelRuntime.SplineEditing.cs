using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private enum SplineEditKind:byte{AddKnot,RaiseDegree,Reparameterise}

    public static int BCurveAddKnot(CurveTag curve,double knot)
        => EditSpline(ApiId.BCurveAddKnot,curve,SplineEditKind.AddKnot,0,knot,default,null,false);
    public static int BCurveRaiseDegree(CurveTag curve,SplineDegree increment,PK_BCURVE_raise_degree_o_s* options)
        => EditSpline(ApiId.BCurveRaiseDegree,curve,SplineEditKind.RaiseDegree,increment,0,default,options,false);
    public static int BCurveReparameterise(CurveTag curve,PK_INTERVAL_s interval,PK_BCURVE_reparameterise_o_s* options)
        => EditSpline(ApiId.BCurveReparameterise,curve,SplineEditKind.Reparameterise,0,0,interval,options,false);
    public static int BSurfAddUKnot(SurfTag surface,double knot)
        => EditSpline(ApiId.BSurfAddUKnot,surface,SplineEditKind.AddKnot,0,knot,default,null,true);
    public static int BSurfAddVKnot(SurfTag surface,double knot)
        => EditSpline(ApiId.BSurfAddVKnot,surface,SplineEditKind.AddKnot,0,knot,default,null,true,true);
    public static int BSurfRaiseDegree(SurfTag surface,PK_PARAM_direction_t direction,SplineDegree increment,PK_BSURF_raise_degree_o_s* options)
    {
        if(direction is not (ParasolidConstants.PK_PARAM_direction_u_c or ParasolidConstants.PK_PARAM_direction_v_c))return ParasolidConstants.PK_ERROR_bad_parameter;
        return EditSpline(ApiId.BSurfRaiseDegree,surface,SplineEditKind.RaiseDegree,increment,0,default,options,true,direction==ParasolidConstants.PK_PARAM_direction_v_c);
    }
    public static int BSurfReparameterise(SurfTag surface,PK_BSURF_reparameterise_o_s* options)
        => EditSpline(ApiId.BSurfReparameterise,surface,SplineEditKind.Reparameterise,0,0,default,options,true);

    private static int EditSpline(ApiId api,GeomTag geometry,SplineEditKind kind,SplineDegree increment,double knot,PK_INTERVAL_s interval,void* options,bool surface,bool alongV=false)
    {
        var command=new SplineEditCommand{Geometry=geometry,Kind=kind,Increment=increment,Knot=knot,Interval=interval,Options=options,Surface=surface,AlongV=alongV};
        if(Dispatcher.IsExecuting)return command.Execute();
        return Dispatch(api,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,geometry);
    }

    private struct SplineEditCommand:IKernelCommand
    {
        internal GeomTag Geometry;
        internal SplineEditKind Kind;
        internal SplineDegree Increment;
        internal double Knot;
        internal PK_INTERVAL_s Interval;
        internal void* Options;
        internal bool Surface,AlongV;
        public int Execute()
        {
            if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
            if(Kind!=SplineEditKind.AddKnot)
            {
                if(Options==null)return ParasolidConstants.PK_ERROR_bad_parameter;
                if(*(int*)Options!=(Surface&&Kind==SplineEditKind.Reparameterise?2:1))return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
            }
            var owns=EnsureCommandScratch();
            try{return Surface?EditBSurface(Geometry,Kind,Increment,Knot,AlongV,(PK_BSURF_reparameterise_o_s*)Options):EditBCurve(Geometry,Kind,Increment,Knot,Interval);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int EditableSpline(GeomTag tag,bool surface,out DataSlot slot)
    {
        slot=surface?GetSurfaceSlotByTag(tag):GetCurveSlotByTag(tag);
        if(slot<0||surface&&Surfaces[slot].Class!=SurfaceClass.BSurface||!surface&&Curves[slot].Class!=CurveClass.BCurve)return ParasolidConstants.PK_ERROR_bad_class;
        var pool=surface?PoolKind.Surface:PoolKind.Curve;
        if(PoolPartitionOf(pool,slot)!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if(GeometryOwnerCount(pool,slot)!=0||GeometricOwnerCount(pool,slot)!=0||GetConstructionBody(tag)>=0)return ParasolidConstants.PK_ERROR_is_attached;
        return 0;
    }

    private static int EditBCurve(CurveTag curve,SplineEditKind kind,SplineDegree increment,double knot,PK_INTERVAL_s interval)
    {
        var error=EditableSpline(curve,false,out var slot);if(error!=0)return error;
        var data=BCurveDataStore[Curves[slot].DataIndex];var degree=data.Degree;var view=GetBCurveView(in data);
        if(data.IsPeriodic!=0||degree>16||data.NVertices>FreeformPoleLimit||view.Knots[0]!=view.Start||view.Knots[^1]!=view.End)return ParasolidConstants.PK_ERROR_not_implemented;
        if(kind==SplineEditKind.RaiseDegree&&(increment<1||increment>16-degree))return ParasolidConstants.PK_ERROR_bad_parameter;
        var target=kind==SplineEditKind.RaiseDegree?degree+increment:degree;
        var capacity=kind==SplineEditKind.RaiseDegree?(data.NKnots-1)*target+1:data.NVertices+(kind==SplineEditKind.AddKnot?1:0);
        if(capacity>FreeformPoleLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        var dimension=data.VertexDim+(data.IsRational!=0?0:1);if(dimension is not (3 or 4))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<double> input=stackalloc double[data.NVertices*dimension],poles=stackalloc double[capacity*dimension],knots=stackalloc double[capacity+target+1];
        LiftEditingPoles(data.PolesSpan(),data.VertexDim,data.IsRational!=0,input);var count=data.NVertices;
        if(kind==SplineEditKind.RaiseDegree)
        {if(!SplineEditingMath.Raise(input,view.Knots,dimension,degree,increment,poles,knots,out count))return ParasolidConstants.PK_ERROR_cant_make_bspline;}
        else
        {
            input.CopyTo(poles);view.Knots.CopyTo(knots);
            if(kind==SplineEditKind.AddKnot)
            {
                error=InsertEditingKnot(poles,knots,dimension,degree,ref count,knot);if(error!=0)return error;
            }
            else if(!RemapEditingKnots(knots[..(count+degree+1)],degree,count,interval))return ParasolidConstants.PK_ERROR_bad_parameter;
        }
        Span<double> compact=stackalloc double[count+target+1];Span<int> mults=stackalloc int[compact.Length];
        var nk=CompactEditingKnots(knots[..(count+target+1)],compact,mults);
        Span<double> output=stackalloc double[count*data.VertexDim];if(!LowerEditingPoles(poles[..(count*dimension)],data.VertexDim,data.IsRational!=0,output))return ParasolidConstants.PK_ERROR_bad_vertex;
        CurveTag edited=0;error=CreateFreeformCurve(target,output,data.VertexDim,data.IsRational!=0,compact[..nk],mults[..nk],data.IsClosed!=0,&edited);
        return error!=0?error:ReplaceEditedSpline(curve,edited,false);
    }

    private static void LiftEditingPoles(ReadOnlySpan<double> source,BufferCount dimension,bool rational,Span<double> result)
    {
        if(rational){source.CopyTo(result);return;}
        for(var i=0;i<source.Length/dimension;i++){source.Slice(i*dimension,dimension).CopyTo(result.Slice(i*(dimension+1),dimension));result[i*(dimension+1)+dimension]=1;}
    }

    private static bool LowerEditingPoles(ReadOnlySpan<double> source,BufferCount dimension,bool rational,Span<double> result)
    {
        if(rational){source.CopyTo(result);return true;}
        for(var i=0;i<result.Length/dimension;i++)
        {
            var weight=source[i*(dimension+1)+dimension];if(!(weight>0))return false;
            for(var d=0;d<dimension;d++){var value=source[i*(dimension+1)+d]/weight;if(!double.IsFinite(value))return false;result[i*dimension+d]=value;}
        }
        return true;
    }

    private static int InsertEditingKnot(Span<double> poles,Span<double> knots,BufferCount dimension,SplineDegree degree,ref BufferCount count,double knot)
    {
        if(!double.IsFinite(knot)||knot<=knots[degree]||knot>=knots[count])return ParasolidConstants.PK_ERROR_bad_parameter;
        var mult=0;for(var i=0;i<count+degree+1;i++)if(knots[i]==knot)mult++;
        if(mult>=degree)return ParasolidConstants.PK_ERROR_repeated_knots;
        return FreeformSplineMath.Insert(poles,knots,dimension,degree,ref count,knot)?0:ParasolidConstants.PK_ERROR_bad_knots;
    }

    private static bool RemapEditingKnots(Span<double> knots,SplineDegree degree,BufferCount count,PK_INTERVAL_s interval)
    {
        var first=interval.value[0];var last=interval.value[1];if(!double.IsFinite(first)||!double.IsFinite(last)||!(last>first))return false;
        var a=knots[degree];var b=knots[count];var scale=(last-first)/(b-a);if(!(scale>0)||!double.IsFinite(scale))return false;
        var previous=knots[0];var mapped=first;
        for(var i=0;i<knots.Length;i++)
        {
            var value=knots[i];var next=value==a?first:value==b?last:Math.FusedMultiplyAdd(value-a,scale,first);
            if(!double.IsFinite(next)||value>previous&&!(next>mapped))return false;
            knots[i]=next;previous=value;mapped=next;
        }
        return true;
    }

    private static BufferCount CompactEditingKnots(ReadOnlySpan<double> expanded,Span<double> knots,Span<int> mults)
    {
        var count=0;
        foreach(var value in expanded){if(count>0&&knots[count-1]==value)mults[count-1]++;else{knots[count]=value;mults[count++]=1;}}
        return count;
    }

    private static int ReplaceEditedSpline(GeomTag original,GeomTag edited,bool surface)
    {
        if(surface)
        {
            ref var a=ref Surfaces[GetSurfaceSlotByTag(original)];ref var b=ref Surfaces[GetSurfaceSlotByTag(edited)];
            if(!State.Session->TrySnapshot(ref a)||!State.Session->TrySnapshot(ref b))return ParasolidConstants.PK_ERROR_memory_full;
            (a.DataIndex,b.DataIndex)=(b.DataIndex,a.DataIndex);a.UMin=b.UMin;a.UMax=b.UMax;a.VMin=b.VMin;a.VMax=b.VMax;
        }
        else
        {
            ref var a=ref Curves[GetCurveSlotByTag(original)];ref var b=ref Curves[GetCurveSlotByTag(edited)];
            if(!State.Session->TrySnapshot(ref a)||!State.Session->TrySnapshot(ref b))return ParasolidConstants.PK_ERROR_memory_full;
            (a.DataIndex,b.DataIndex)=(b.DataIndex,a.DataIndex);a.TMin=b.TMin;a.TMax=b.TMax;
        }
        GeometryEvaluationCache.BumpModelGeometryEpoch();return EntityDelete(1,&edited);
    }
}
