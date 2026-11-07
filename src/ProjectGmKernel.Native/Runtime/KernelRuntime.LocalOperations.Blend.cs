using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.LocalOperations;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct LocalBlendProduct
    {
        internal EntityTag Edge,Vertex;
        internal FaceTag First,Second,Third,Result;
        internal BlendMarkSequence Sequence;
    }

    private static bool LocalEdgeFaces(EdgeSlot edge,out FaceSlot first,out FaceSlot second)
    {
        first=second=-1;ref var e=ref Edges[edge];
        if(e.FinCount!=2||e.FirstFinEdge<0)return false;
        var a=e.FirstFinEdge;var b=Fins[a].Other;
        if(b<0||Fins[a].Loop<0||Fins[b].Loop<0)return false;
        first=Loops[Fins[a].Loop].Face;second=Loops[Fins[b].Loop].Face;return first!=second;
    }

    private static bool DefaultLocalBlendProperties(in PK_blend_properties_s p)
        =>LocalChoice(p.propagate,ParasolidConstants.PK_blend_propagate_no_c)
        &&LocalChoice(p.vary,ParasolidConstants.PK_blend_vary_smooth_c)
        &&LocalChoice(p.render_rib,ParasolidConstants.PK_blend_render_rib_no_c)
        &&LocalChoice(p.ov_smooth,ParasolidConstants.PK_blend_ov_smooth_diff_c)
        &&LocalChoice(p.ov_cliff,ParasolidConstants.PK_blend_ov_cliff_diff_c)
        &&LocalChoice(p.ov_cliff_end,ParasolidConstants.PK_blend_ov_cliff_end_no_c)
        &&LocalChoice(p.ov_notch,ParasolidConstants.PK_blend_ov_notch_yes_c)
        &&double.IsFinite(p.tolerance)&&p.tolerance>=0&&double.IsFinite(p.ribspace)&&p.ribspace>=0;

    private static int SetLocalBlendCore(int count,EntityTag* edges,double first,double second,EntityTag* firstFaces,
        KernelBlendKind kind,void* options,int* outputCount,EntityTag** output)
    {
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(count<=0||count>LocalEdgeLimit||edges==null||options==null||outputCount==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(!double.IsFinite(first)||!double.IsFinite(second)||first<=0||second<=0)return kind==1?ParasolidConstants.PK_ERROR_radius_le_0:ParasolidConstants.PK_ERROR_distance_le_0;
        if(kind==1)
        {
            var o=(PK_EDGE_set_blend_constant_o_s*)options;if(o->o_t_version!=2)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
            if(o->cliff_edge!=0||!LocalChoice(o->xs_shape,ParasolidConstants.PK_blend_xs_shape_conic_c)||!DefaultLocalBlendProperties(in o->properties))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        else
        {
            var o=(PK_EDGE_set_blend_chamfer_o_s*)options;if(o->o_t_version!=1)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
            if(!DefaultLocalBlendProperties(in o->properties))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        Span<EdgeSlot> slots=stackalloc EdgeSlot[count];Span<FaceTag> designated=stackalloc FaceTag[count];var body=-1;
        for(var i=0;i<count;i++)
        {
            if(!State.Session->Tags.TryResolve(edges[i],CurrentSessionId,out var record)||record.Pool!=(byte)PoolKind.Edge)return ParasolidConstants.PK_ERROR_bad_class;
            var slot=record.Slot;slots[i]=slot;
            for(var j=0;j<i;j++)if(slots[j]==slot)return ParasolidConstants.PK_ERROR_duplicate_list_item;
            if(body<0)body=Edges[slot].Body;else if(body!=Edges[slot].Body)return ParasolidConstants.PK_ERROR_not_in_same_body;
            var error=ResolveLocalBody(Bodies[body].Header.Tag,out _);if(error!=0)return error;
            if(!LocalEdgeFaces(slot,out var a,out var b))return ParasolidConstants.PK_ERROR_bad_parameter;
            designated[i]=firstFaces==null?Faces[a].Header.Tag:firstFaces[i];
            if(designated[i]!=Faces[a].Header.Tag&&designated[i]!=Faces[b].Header.Tag)return ParasolidConstants.PK_ERROR_bad_parameter;
        }
        var returns=output==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(count*sizeof(int)));
        if(output!=null&&returns==null)return ParasolidConstants.PK_ERROR_memory_full;
        BlendMarkSequence sequence=0;var pending=Bodies[body].FirstEdgeBody;
        for(var i=0;i<Bodies[body].EdgeCountBody;i++,pending=Edges[pending].NextInBody)
            if(Edges[pending].PendingBlendKind!=0)sequence=Math.Max(sequence,Edges[pending].PendingBlendSequence);
        for(var i=0;i<count;i++)
        {
            ref var edge=ref Edges[slots[i]];if(!State.Session->TrySnapshot(ref edge))return ParasolidConstants.PK_ERROR_memory_full;
            edge.PendingBlendKind=kind;edge.PendingBlendFirst=first;edge.PendingBlendSecond=second;edge.PendingBlendFirstFace=designated[i];
            edge.PendingBlendSequence=++sequence;
            if(returns!=null)returns[i]=edges[i];
        }
        SessionMemoryOwner.DropBodyXt(body);
        *outputCount=count;if(output!=null)*output=returns;return 0;
    }

    private static int FixLocalBlendsCore(EntityTag body,PK_BODY_fix_blends_o_s* options,int* outputCount,
        EntityTag** output,PK_FACE_array_s** unders,EntityTag** topols,int* fault,EntityTag* faultEdge,EntityTag* faultTopol)
    {
        ModelingPerformance.Context profile=default;using var evidence=new ModelingPerformance.Scope(&profile);
        var error=ResolveLocalBody(body,out var slot);if(error!=0)return error;
        if(options==null||outputCount==null||fault==null||faultEdge==null||faultTopol==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=31)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if(!double.IsFinite(options->tolerance)||options->tolerance<0)return ParasolidConstants.PK_ERROR_bad_tolerance;
        if(!LocalChoice(options->transfer,ParasolidConstants.PK_blend_transfer_topol_no_c)
            ||!LocalChoice(options->output_sheet,ParasolidConstants.PK_blend_output_sheet_no_c)
            ||!LocalChoice(options->update,ParasolidConstants.PK_blend_edge_update_default_c)&&options->update!=ParasolidConstants.PK_blend_edge_update_v261_c
            ||!LocalChoice(options->repair_su_X,ParasolidConstants.PK_blend_repair_su_X_no_c)
            ||!LocalChoice(options->repair_fa_X,ParasolidConstants.PK_blend_repair_fa_X_no_c)
            ||!LocalChoice(options->inside_tight,ParasolidConstants.PK_blend_inside_tight_blends_c)
            ||!LocalChoice(options->ov_smooth,ParasolidConstants.PK_blend_ov_smooth_no_c)
            ||!LocalChoice(options->ov_notch,ParasolidConstants.PK_blend_ov_notch_yes_c)
            ||!LocalChoice(options->ov_order,ParasolidConstants.PK_blend_order_convex_concave_c)
            ||!LocalChoice(options->suggest_limit,ParasolidConstants.PK_blend_suggest_limit_no_c)
            ||!LocalChoice(options->report,ParasolidConstants.PK_blend_report_repaired_no_c)
            ||!LocalChoice(options->report_extended,ParasolidConstants.PK_blend_report_extended_no_c)
            ||!LocalChoice(options->extension_shape,ParasolidConstants.PK_extension_shape_linear_c)
            ||!LocalChoice(options->improve_tolerance,ParasolidConstants.PK_blend_tolerance_standard_c)
            ||!LocalChoice(options->laminar_trim,ParasolidConstants.PK_blend_laminar_trim_edges_c)
            ||options->setback_data.n_edges!=0||options->cap_data.n_caps!=0||options->n_explicit_cliff_edges!=0
            ||options->limit_topols.length!=0||options->limit_data.n_limits!=0||options->limit_data.n_vertices!=0
            ||options->vx_blend_data.n_vertices!=0||options->vx_blend_data.vertex_blend!=0
            ||options->y_blend_data.n_vertices!=0||options->y_blend_data.y_blend!=0
            ||options->vx_order_data.n_vertices!=0||options->setback_shape_data.n_vertices!=0
            ||!LocalChoice(options->tracking_type,ParasolidConstants.PK_blend_track_type_basic_c)
            ||options->propagate.function!=null||options->partition!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var n=Bodies[slot].EdgeCountBody;if(n>LocalEdgeLimit||Bodies[slot].FaceCountBody>PlaneLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<EdgeSlot> selected=stackalloc EdgeSlot[n];var count=0;var edge=Bodies[slot].FirstEdgeBody;
        for(var i=0;i<n;i++,edge=Edges[edge].NextInBody)if(Edges[edge].PendingBlendKind!=0)selected[count++]=edge;
        // The graph determines the physical construction; mark order retains
        // the API's carrier/traversal convention for the returned products.
        for(var i=1;i<count;i++)for(var j=i;j>0&&Edges[selected[j]].PendingBlendSequence<Edges[selected[j-1]].PendingBlendSequence;j--)
            (selected[j],selected[j-1])=(selected[j-1],selected[j]);
        if(count==0){*fault=ParasolidConstants.PK_blend_fault_no_blend_c;return 0;}
        var faceCount=Bodies[slot].FaceCountBody;Span<FaceSlot> faces=stackalloc FaceSlot[faceCount];var face=Bodies[slot].FirstFaceBody;
        for(var i=0;i<faceCount;i++,face=Faces[face].NextInBody)faces[i]=face;
        Span<LocalBlendProduct> descriptions=stackalloc LocalBlendProduct[LocalFaceLimit];Span<FaceSlot> products=stackalloc FaceSlot[LocalFaceLimit];
        BufferCount made;
        if(CanBuildAnalyticRollingNetwork(selected[..count]))
            error=BuildInteractingRollingBlends(slot,faces,selected[..count],descriptions,products,out made);
        else
        {
        error=BuildAxialLocalBlends(slot,faces,selected[..count],descriptions,products,out made);
        if(error==ParasolidConstants.PK_ERROR_not_implemented)
            error=BuildPlanarLocalBlends(slot,faces,selected[..count],descriptions,products,out made);
        if(error==ParasolidConstants.PK_ERROR_not_implemented)
            error=BuildTrivalentLocalBlendCorner(slot,faces,selected[..count],descriptions,products,out made);
        if(error==ParasolidConstants.PK_ERROR_not_implemented)
            error=BuildRegularLocalBlend(slot,faces,selected[..count],descriptions,products,out made);
        }
        if(error!=0)
        {
            *fault=error==ParasolidConstants.PK_ERROR_not_implemented?ParasolidConstants.PK_blend_fault_type_c:ParasolidConstants.PK_blend_fault_range_c;
            *faultEdge=Edges[selected[0]].Header.Tag;return error;
        }
        var result=output==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(made*sizeof(int)));
        var replaced=topols==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(made*sizeof(int)));
        var underlying=unders==null?null:(PK_FACE_array_s*)State.Session->Returns.TryAllocate((nuint)(made*sizeof(PK_FACE_array_s)));
        if(output!=null&&result==null||topols!=null&&replaced==null||unders!=null&&underlying==null)return ParasolidConstants.PK_ERROR_memory_full;
        for(var i=0;i<made;i++)
        {
            var desc=descriptions[i];if(result!=null)result[i]=desc.Result!=0?desc.Result:Faces[products[faceCount+i]].Header.Tag;
            if(replaced!=null)replaced[i]=options->vx_twin!=0&&desc.Vertex!=0?desc.Vertex:desc.Edge;
            if(underlying!=null)
            {
                var length=desc.Third==0?2:3;var array=(int*)State.Session->Returns.TryAllocate((nuint)(length*sizeof(int)));
                if(array==null)return ParasolidConstants.PK_ERROR_memory_full;
                array[0]=desc.First;array[1]=desc.Second;if(length==3)array[2]=desc.Third;
                underlying[i]=new(){array=array,length=length};
            }
        }
        *outputCount=made;if(output!=null)*output=result;if(topols!=null)*topols=replaced;if(unders!=null)*unders=underlying;
        *fault=ParasolidConstants.PK_blend_fault_no_fault_c;return 0;
    }

    private static int LocalBlendCentre(LocalPlane first,LocalPlane second,KernelVector3 point,double r1,double r2,out KernelVector3 center)
    {
        var cosine=Dot(first.Normal,second.Normal);var determinant=1-cosine*cosine;center=default;
        if(determinant<=1e-12||r1-r2*cosine<=0||r2-r1*cosine<=0)return ParasolidConstants.PK_ERROR_bad_parameter;
        var a=(-r1+cosine*r2)/determinant;var b=(-r2+cosine*r1)/determinant;
        center=Add(point,Add(Scale(first.Normal,a),Scale(second.Normal,b)));return Finite(center)?0:ParasolidConstants.PK_ERROR_bad_parameter;
    }

    private static int AddLocalArc(ref LocalBrep brep,int first,int second,KernelVector3 center,KernelVector3 normal,double radius)
    {
        var edge=brep.FindEdge(first,second);if(edge<0)edge=brep.AddLine(first,second);
        if(edge<0)return ParasolidConstants.PK_ERROR_memory_full;
        if(brep.Edges[edge].Curve>0)return 0;
        first=brep.Edges[edge].StartVertex;second=brep.Edges[edge].EndVertex;
        var x=Unit(Sub(brep.Points[first],center));var y=ConvexLocalModel.Cross(normal,x);var p=Sub(brep.Points[second],center);
        var angle=Math.Atan2(Dot(p,y),Dot(p,x));
        if(angle<0){normal=Scale(normal,-1);angle=-angle;}
        if(!(angle>1e-12)||angle>Math.PI+1e-9)return ParasolidConstants.PK_ERROR_bad_parameter;
        var curve=CreateCircleCurveTag(center.X,center.Y,center.Z,normal.X,normal.Y,normal.Z,x.X,x.Y,x.Z,radius);
        if(curve<=0)return ParasolidConstants.PK_ERROR_memory_full;
        brep.Edges[edge].Curve=curve;brep.Edges[edge].Start=0;brep.Edges[edge].End=angle;return 0;
    }
}
