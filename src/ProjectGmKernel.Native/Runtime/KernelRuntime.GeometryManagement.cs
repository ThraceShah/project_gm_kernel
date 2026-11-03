using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int EntityCopy(EntityTag entity,EntityTag* copy)
    {
        if(copy!=null)*copy=0;
        if(Dispatcher.IsExecuting)return EntityCopyCore(entity,copy);
        var command=new GeometryManagementCommand { Operation=ApiId.EntityCopy,Entity=entity,Output=copy };
        return Dispatch(ApiId.EntityCopy,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,entity);
    }
    public static int EntityCopy2(EntityTag entity,PK_ENTITY_copy_o_s* options,EntityTag* copy,PK_ENTITY_track_r_s* tracking)
    {
        if(copy!=null)*copy=0;if(tracking!=null)*tracking=default;
        if(Dispatcher.IsExecuting)return EntityCopy2Core(entity,options,copy,tracking);
        var command=new GeometryManagementCommand { Operation=ApiId.EntityCopy2,Entity=entity,CopyOptions=options,Output=copy,EntityTracking=tracking };
        return Dispatch(ApiId.EntityCopy2,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,entity);
    }
    public static int TransfAsk(TransfTag transform,PK_TRANSF_sf_s* form)
    {
        if(Dispatcher.IsExecuting)return TransfAskCore(transform,form);
        var command=new GeometryManagementCommand { Operation=ApiId.TransfAsk,Transform=transform,TransformForm=form };
        return Dispatch(ApiId.TransfAsk,ConcurrencyKind.Concurrent,AccessKind.ReadOnly,ref command,transform);
    }
    public static int GeomTransform(GeomTag geometry,TransfTag transform,double tolerance,GeomTag* output,KernelLogical* exact)
    {
        if(output!=null)*output=0;if(exact!=null)*exact=0;
        if(Dispatcher.IsExecuting)return GeomTransformCore(geometry,transform,tolerance,output,exact);
        var command=new GeometryManagementCommand { Operation=ApiId.GeomTransform,Entity=geometry,Transform=transform,Tolerance=tolerance,Output=output,Exact=exact };
        return Dispatch(ApiId.GeomTransform,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,geometry);
    }
    public static int GeomTransform2(BufferCount count,GeomTag* geometries,TransfTag transform,PK_GEOM_transform_o_s* options,GeomTag* output,KernelLogical* exact)
    {
        if(Dispatcher.IsExecuting)return GeomTransform2Core(count,geometries,transform,options,output,exact);
        var command=new GeometryManagementCommand { Operation=ApiId.GeomTransform2,Count=count,Entities=geometries,Transform=transform,GeomOptions=options,Output=output,Exact=exact };
        return Dispatch(ApiId.GeomTransform2,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,count>0&&geometries!=null?geometries[0]:0);
    }
    public static int BodyTransform(BodyTag body,TransfTag transform,double tolerance,BufferCount* count,GeomTag** replaced,KernelLogical** exact)
    {
        if(count!=null)*count=0;if(replaced!=null)*replaced=null;if(exact!=null)*exact=null;
        if(count==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        if(Dispatcher.IsExecuting)return BodyTransformCore(body,transform,tolerance);
        var command=new GeometryManagementCommand { Operation=ApiId.BodyTransform,Entity=body,Transform=transform,Tolerance=tolerance };
        return Dispatch(ApiId.BodyTransform,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,body);
    }
    public static int BodyTransform2(BodyTag body,TransfTag transform,double tolerance,PK_BODY_transform_o_s* options,PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        if(tracking!=null)*tracking=default;if(results!=null)*results=default;
        if(Dispatcher.IsExecuting)return BodyTransform2Core(body,transform,tolerance,options,tracking,results);
        var command=new GeometryManagementCommand { Operation=ApiId.BodyTransform2,Entity=body,Transform=transform,Tolerance=tolerance,BodyOptions=options,TopologyTracking=tracking,LocalResults=results };
        return Dispatch(ApiId.BodyTransform2,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,body);
    }

    private static int EntityCopyCore(EntityTag entity,EntityTag* copy)
    {
        if(copy==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        var error=TryCloneEntityCore(entity,out var result);if(error==0)*copy=result;return error;
    }
    private static int EntityCopy2Core(EntityTag entity,PK_ENTITY_copy_o_s* options,EntityTag* copy,PK_ENTITY_track_r_s* tracking)
    {
        if(copy==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        if(options!=null)
        {
            if(options->o_t_version is <1 or >2)return ParasolidConstants.PK_ERROR_bad_value;
            if(options->destination!=0 || options->want_user_fields!=0 || options->want_attribs!=0 || options->want_groups!=0
                || options->want_tracking!=0 || options->track_classes.length!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        }
        return EntityCopyCore(entity,copy);
    }
    private static int TransfAskCore(TransfTag tag,PK_TRANSF_sf_s* form)
    {
        if(form==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(!IsValidTag(tag)||TagRec(tag).Pool!=(byte)PoolKind.Transform)return ParasolidConstants.PK_ERROR_unknown_class;
        ref var record=ref Transforms[TagRec(tag).Slot];for(var i=0;i<16;i++)form->matrix[i]=record.Matrix[i];return 0;
    }
    private static int GeomTransformCore(GeomTag geometry,TransfTag transform,double tolerance,GeomTag* output,KernelLogical* exact)
    {
        if(output==null||exact==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(!IsValidTag(geometry)||TagRec(geometry).Pool is not ((byte)PoolKind.Point or (byte)PoolKind.Curve or (byte)PoolKind.Surface))return ParasolidConstants.PK_ERROR_unsuitable_entity;
        var error=ReadAffine(transform,out var matrix);if(error!=0)return error;
        error=TryCloneEntityCore(geometry,out var copy);if(error!=0)return error;
        error=TryTransformEntityCore(copy,matrix,tolerance);if(error!=0)return error;
        *output=copy;*exact=1;return 0;
    }
    private static int GeomTransform2Core(BufferCount count,GeomTag* geometries,TransfTag transform,PK_GEOM_transform_o_s* options,GeomTag* output,KernelLogical* exact)
    {
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(count<=0||geometries==null||options==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        if(options->o_t_version!=1 || options->modify is not (ParasolidConstants.PK_GEOM_transform_modify_c or ParasolidConstants.PK_GEOM_transform_new_c))return ParasolidConstants.PK_ERROR_bad_value;
        if(options->want_out_geoms!=0&&output==null || options->want_exact!=0&&exact==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        var error=ReadAffine(transform,out var matrix);if(error!=0)return error;
        if(count==int.MaxValue)return ParasolidConstants.PK_ERROR_memory_full;
        if(options->modify==ParasolidConstants.PK_GEOM_transform_modify_c)
        { error=ValidateExplicitTransformClosure(new ReadOnlySpan<GeomTag>(geometries,count));if(error!=0)return error; }
        var ownsScratch=EnsureCommandScratch();
        var workspace=CommandScratchBorrow((count+1)/2);
        if(workspace.IsEmpty) { if(ownsScratch)ReleaseCommandScratch(State.Session);return ParasolidConstants.PK_ERROR_memory_full; }
        var copies=System.Runtime.InteropServices.MemoryMarshal.Cast<double,GeomTag>(workspace);
        try
        {
            for(var i=0;i<count;i++)
            {
                if(!IsValidTag(geometries[i]))return ParasolidConstants.PK_ERROR_unknown_class;
                var record=TagRec(geometries[i]);var pool=(PoolKind)record.Pool;
                if(pool is not (PoolKind.Point or PoolKind.Curve or PoolKind.Surface))return ParasolidConstants.PK_ERROR_unsuitable_entity;
                if(options->modify==ParasolidConstants.PK_GEOM_transform_modify_c)
                {
                    copies[i]=geometries[i];
                }
            }
            if(options->modify==ParasolidConstants.PK_GEOM_transform_new_c)
            { error=TryCloneEntitiesCore(new ReadOnlySpan<GeomTag>(geometries,count),copies[..count]);if(error!=0)return error; }
            error=TryTransformEntitiesCore(copies[..count],matrix,options->tolerance);if(error!=0)return error;
            for(var i=0;i<count;i++) { if(options->want_out_geoms!=0)output[i]=copies[i];if(options->want_exact!=0)exact[i]=1; }
            return 0;
        }
        finally { CommandScratchReturn(workspace);if(ownsScratch)ReleaseCommandScratch(State.Session); }
    }
    private static int BodyTransformCore(BodyTag body,TransfTag transform,double tolerance)
    {
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(!IsValidTag(body)||TagRec(body).Pool!=(byte)PoolKind.Body)return ParasolidConstants.PK_ERROR_unknown_class;
        var error=ReadAffine(transform,out var matrix);return error!=0?error:TryTransformEntityCore(body,matrix,tolerance);
    }
    private static int BodyTransform2Core(BodyTag body,TransfTag transform,double tolerance,PK_BODY_transform_o_s* options,PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        if(options==null||tracking==null||results==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        if(options->o_t_version is <1 or >3)return ParasolidConstants.PK_ERROR_bad_value;
        var error=BodyTransformCore(body,transform,tolerance);
        if(error==0)results->status=ParasolidConstants.PK_local_status_ok_c;
        return error;
    }

    private struct GeometryManagementCommand:IKernelCommand
    {
        internal ApiId Operation;internal EntityTag Entity;internal TransfTag Transform;internal BufferCount Count;
        internal EntityTag* Entities;internal EntityTag* Output;internal KernelLogical* Exact;internal double Tolerance;
        internal PK_ENTITY_copy_o_s* CopyOptions;internal PK_GEOM_transform_o_s* GeomOptions;internal PK_BODY_transform_o_s* BodyOptions;
        internal PK_TRANSF_sf_s* TransformForm;internal PK_ENTITY_track_r_s* EntityTracking;internal PK_TOPOL_track_r_s* TopologyTracking;internal PK_TOPOL_local_r_s* LocalResults;
        public int Execute()=>Operation switch
        {
            ApiId.EntityCopy=>EntityCopyCore(Entity,Output),ApiId.EntityCopy2=>EntityCopy2Core(Entity,CopyOptions,Output,EntityTracking),
            ApiId.TransfAsk=>TransfAskCore(Transform,TransformForm),ApiId.GeomTransform=>GeomTransformCore(Entity,Transform,Tolerance,Output,Exact),
            ApiId.GeomTransform2=>GeomTransform2Core(Count,Entities,Transform,GeomOptions,Output,Exact),
            ApiId.BodyTransform=>BodyTransformCore(Entity,Transform,Tolerance),ApiId.BodyTransform2=>BodyTransform2Core(Entity,Transform,Tolerance,BodyOptions,TopologyTracking,LocalResults),
            _=>ParasolidConstants.PK_ERROR_not_implemented,
        };
    }
}
