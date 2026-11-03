using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct LocalOperationCommand:IKernelCommand
    {
        internal ApiId Operation;
        internal EntityTag Body;
        internal BufferCount Count;
        internal EntityTag* Entities;
        internal EntityTag* FirstFaces;
        internal double* Offsets;
        internal double Value,Second,Tolerance;
        internal void* Options;
        internal BufferCount* OutputCount;
        internal EntityTag** Output;
        internal EntityTag** SecondaryOutput;
        internal PK_FACE_array_s** Unders;
        internal KernelClassCode* Fault;
        internal EntityTag* FaultEdge;
        internal EntityTag* FaultTopol;
        internal PK_TOPOL_track_r_s* Tracking;
        internal PK_TOPOL_local_r_s* Results;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try
            {
                return Operation switch
                {
                    ApiId.EdgeSetBlendConstant=>SetLocalBlendCore(Count,Entities,Value,Value,null,1,Options,OutputCount,Output),
                    ApiId.EdgeSetBlendChamfer=>SetLocalBlendCore(Count,Entities,Value,Second,FirstFaces,2,Options,OutputCount,Output),
                    ApiId.BodyFixBlends=>FixLocalBlendsCore(Body,(PK_BODY_fix_blends_o_s*)Options,OutputCount,Output,Unders,SecondaryOutput,Fault,FaultEdge,FaultTopol),
                    ApiId.BodyOffset or ApiId.BodyOffset2=>OffsetLocalBodyCore(Body,Value,Tolerance,(PK_BODY_offset_o_s*)Options,Tracking,Results),
                    ApiId.FaceOffset or ApiId.FaceOffset2=>OffsetLocalFacesCore(Count,Entities,Offsets,Tolerance,(PK_FACE_offset_o_s*)Options,Tracking,Results),
                    ApiId.BodyHollow=>HollowLocalBodyCore(Body,Value,Tolerance,(PK_BODY_hollow_o_s*)Options,null,null,OutputCount,Output,SecondaryOutput),
                    ApiId.BodyHollow2=>HollowLocalBodyCore(Body,Value,Tolerance,(PK_BODY_hollow_o_s*)Options,Tracking,Results,null,null,null),
                    _=>ParasolidConstants.PK_ERROR_not_implemented,
                };
            }
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int RunLocalOperation(ref LocalOperationCommand command)
    {
        if(command.OutputCount!=null)*command.OutputCount=0;
        if(command.Output!=null)*command.Output=null;
        if(command.SecondaryOutput!=null)*command.SecondaryOutput=null;
        if(command.Unders!=null)*command.Unders=null;
        if(command.Fault!=null)*command.Fault=ParasolidConstants.PK_blend_fault_no_fault_c;
        if(command.FaultEdge!=null)*command.FaultEdge=0;
        if(command.FaultTopol!=null)*command.FaultTopol=0;
        if(command.Tracking!=null)*command.Tracking=default;
        if(command.Results!=null)*command.Results=default;
        var error=Dispatcher.IsExecuting?command.Execute():
            Dispatch(command.Operation,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,
                command.Body!=0?command.Body:command.Count>0&&command.Entities!=null?command.Entities[0]:0);
        if(error!=0)
        {
            if(command.OutputCount!=null)*command.OutputCount=0;
            if(command.Output!=null)*command.Output=null;
            if(command.SecondaryOutput!=null)*command.SecondaryOutput=null;
            if(command.Unders!=null)*command.Unders=null;
            if(command.Tracking!=null)*command.Tracking=default;
            if(command.Results!=null)*command.Results=default;
        }
        return error;
    }

    public static int EdgeSetBlendConstant(int count,EntityTag* edges,double radius,
        PK_EDGE_set_blend_constant_o_s* options,int* blendCount,EntityTag** blendEdges)
    {
        var command=new LocalOperationCommand{Operation=ApiId.EdgeSetBlendConstant,Count=count,Entities=edges,
            Value=radius,Options=options,OutputCount=blendCount,Output=blendEdges};
        return RunLocalOperation(ref command);
    }

    public static int EdgeSetBlendChamfer(int count,EntityTag* edges,double range2,double range1,
        EntityTag* firstFaces,PK_EDGE_set_blend_chamfer_o_s* options,int* blendCount,EntityTag** blendEdges)
    {
        var command=new LocalOperationCommand{Operation=ApiId.EdgeSetBlendChamfer,Count=count,Entities=edges,
            Value=range1,Second=range2,FirstFaces=firstFaces,Options=options,OutputCount=blendCount,Output=blendEdges};
        return RunLocalOperation(ref command);
    }

    public static int BodyFixBlends(EntityTag body,PK_BODY_fix_blends_o_s* options,int* blendCount,
        EntityTag** blends,PK_FACE_array_s** unders,int** topols,int* fault,EntityTag* faultEdge,EntityTag* faultTopol)
    {
        var command=new LocalOperationCommand{Operation=ApiId.BodyFixBlends,Body=body,Options=options,
            OutputCount=blendCount,Output=blends,Unders=unders,SecondaryOutput=topols,Fault=fault,FaultEdge=faultEdge,FaultTopol=faultTopol};
        return RunLocalOperation(ref command);
    }

    public static int BodyOffset(EntityTag body,double offset,double tolerance,byte check)
    {
        if(check>1)return ParasolidConstants.PK_ERROR_bad_parameter;
        var options=DefaultLocalOffsetOptions();var tracking=new PK_TOPOL_track_r_s();var results=new PK_TOPOL_local_r_s();
        var command=new LocalOperationCommand{Operation=ApiId.BodyOffset,Body=body,Value=offset,Tolerance=tolerance,
            Options=&options,Tracking=&tracking,Results=&results};
        var error=KernelRuntime.RunLocalOperation(ref command);
        if(error==0){TopolTrackResultFree(&tracking);TopolLocalResultFree(&results);}return error;
    }

    public static int BodyOffset2(EntityTag body,double offset,double tolerance,PK_BODY_offset_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        var command=new LocalOperationCommand{Operation=ApiId.BodyOffset2,Body=body,Value=offset,Tolerance=tolerance,
            Options=options,Tracking=tracking,Results=results};return RunLocalOperation(ref command);
    }

    public static int FaceOffset(int count,EntityTag* faces,double* offsets,double tolerance,byte check)
    {
        if(check>1)return ParasolidConstants.PK_ERROR_bad_parameter;
        var options=new PK_FACE_offset_o_s{o_t_version=11};var tracking=new PK_TOPOL_track_r_s();var results=new PK_TOPOL_local_r_s();
        var command=new LocalOperationCommand{Operation=ApiId.FaceOffset,Count=count,Entities=faces,Offsets=offsets,
            Tolerance=tolerance,Options=&options,Tracking=&tracking,Results=&results};
        var error=KernelRuntime.RunLocalOperation(ref command);
        if(error==0){TopolTrackResultFree(&tracking);TopolLocalResultFree(&results);}return error;
    }

    public static int FaceOffset2(int count,EntityTag* faces,double* offsets,double tolerance,PK_FACE_offset_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        var command=new LocalOperationCommand{Operation=ApiId.FaceOffset2,Count=count,Entities=faces,Offsets=offsets,
            Tolerance=tolerance,Options=options,Tracking=tracking,Results=results};return RunLocalOperation(ref command);
    }

    public static int BodyHollow(EntityTag body,double offset,double tolerance,byte check,
        int* count,EntityTag** originalFaces,EntityTag** newFaces)
    {
        if(check>1)return ParasolidConstants.PK_ERROR_bad_parameter;
        var options=new PK_BODY_hollow_o_s{o_t_version=12};
        var command=new LocalOperationCommand{Operation=ApiId.BodyHollow,Body=body,Value=offset,Tolerance=tolerance,
            Options=&options,OutputCount=count,Output=originalFaces,SecondaryOutput=newFaces};return RunLocalOperation(ref command);
    }

    public static int BodyHollow2(EntityTag body,double offset,double tolerance,PK_BODY_hollow_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        var command=new LocalOperationCommand{Operation=ApiId.BodyHollow2,Body=body,Value=offset,Tolerance=tolerance,
            Options=options,Tracking=tracking,Results=results};return RunLocalOperation(ref command);
    }

    private static PK_BODY_offset_o_s DefaultLocalOffsetOptions()=>new(){o_t_version=12};

    private static int ResolveLocalBody(EntityTag body,out BodySlot slot)
    {
        slot=-1;
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(!State.Session->Tags.TryResolve(body,CurrentSessionId,out var record)||record.Pool!=(byte)PoolKind.Body)
            return ParasolidConstants.PK_ERROR_bad_class;
        slot=record.Slot;
        if(Bodies[slot].Header.Partition!=CurrentPartition)return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if(Bodies[slot].BodyType!=ParasolidConstants.PK_BODY_type_solid_c)return ParasolidConstants.PK_ERROR_not_implemented;
        // TOPOL_detach_geom keeps valid topology while clearing its geometric
        // support. All local readers require these supports before indexing
        // the geometry pools or reconstructing an analytic boundary.
        var face=Bodies[slot].FirstFaceBody;
        for(var i=0;i<Bodies[slot].FaceCountBody;i++,face=Faces[face].NextInBody)
            if(GetSurfaceSlotByTag(Faces[face].SurfTag)<0)return ParasolidConstants.PK_ERROR_missing_geom;
        var edge=Bodies[slot].FirstEdgeBody;var finGeometryOnly=false;
        for(var i=0;i<Bodies[slot].EdgeCountBody;i++,edge=Edges[edge].NextInBody)
        {
            if(!HasBoundaryEdgeGeometry(in Edges[edge]))return ParasolidConstants.PK_ERROR_missing_geom;
            finGeometryOnly|=GetCurveSlotByTag(Edges[edge].CurveTag)<0;
        }
        var vertex=Bodies[slot].FirstVertexBody;
        for(var i=0;i<Bodies[slot].VertexCountBody;i++,vertex=Vertices[vertex].NextInBody)
            if(GetPointSlotByTag(Vertices[vertex].PointTag)<0)return ParasolidConstants.PK_ERROR_missing_geom;
        // Tolerant fins may provide complete geometry without a nominal curve,
        // but the current exact local reconstruction does not consume pcurves.
        return finGeometryOnly?ParasolidConstants.PK_ERROR_not_implemented:0;
    }

    private static int LocalFaceOffsets(BodySlot body,double distance,int count,EntityTag* faces,double* distances,
        Span<FaceSlot> source,Span<double> offsets)
    {
        if(!double.IsFinite(distance)||count<0||count>source.Length||count>0&&(faces==null||distances==null))return ParasolidConstants.PK_ERROR_bad_parameter;
        var face=Bodies[body].FirstFaceBody;
        for(var i=0;i<source.Length;i++,face=Faces[face].NextInBody){source[i]=face;offsets[i]=distance;}
        for(var i=0;i<count;i++)
        {
            if(!double.IsFinite(distances[i]))return ParasolidConstants.PK_ERROR_bad_parameter;
            if(!State.Session->Tags.TryResolve(faces[i],CurrentSessionId,out var record)||record.Pool!=(byte)PoolKind.Face)
                return ParasolidConstants.PK_ERROR_bad_class;
            var index=source.IndexOf(record.Slot);if(index<0)return ParasolidConstants.PK_ERROR_not_in_same_body;
            for(var j=0;j<i;j++)if(faces[j]==faces[i])return ParasolidConstants.PK_ERROR_duplicate_list_item;
            offsets[index]=distances[i];
        }
        return 0;
    }
}
