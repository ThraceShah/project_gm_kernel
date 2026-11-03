using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.LocalOperations;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static bool LocalChoice(int value,int supported)=>value==0||value==supported;
    private static bool SupportedLocalControls(int method,int fix,int step,int blend,int grow,int update)
        =>LocalChoice(method,ParasolidConstants.PK_offset_method_sx_trim_c)
        &&LocalChoice(fix,ParasolidConstants.PK_fix_degens_no_c)
        &&LocalChoice(step,ParasolidConstants.PK_offset_step_no_c)
        &&LocalChoice(blend,ParasolidConstants.PK_EDGE_offset_blend_no_c)
        &&LocalChoice(grow,ParasolidConstants.PK_FACE_grow_update_c)
        &&LocalChoice(update,ParasolidConstants.PK_local_ops_update_default_c);

    private static int OffsetLocalBodyCore(EntityTag body,double offset,double tolerance,PK_BODY_offset_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        var error=ResolveLocalBody(body,out var slot);if(error!=0)return error;
        if(options==null||tracking==null||results==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=12)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if(!double.IsFinite(tolerance)||tolerance<=0)return ParasolidConstants.PK_ERROR_bad_tolerance;
        if(!SupportedLocalControls(options->offset_method,options->fix_degens,options->offset_step,options->blend_edges,options->grow,options->update)
            ||!LocalChoice(options->vertex_limit,ParasolidConstants.PK_VERTEX_limit_no_c)
            ||!LocalChoice(options->edge_limit,ParasolidConstants.PK_EDGE_limit_no_c))return ParasolidConstants.PK_ERROR_not_implemented;
        var count=Bodies[slot].FaceCountBody;if(count<1||count>PlaneLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> faces=stackalloc FaceSlot[count];Span<double> offsets=stackalloc double[count];
        error=LocalFaceOffsets(slot,offset,options->n_offset_faces,options->offset_faces,options->offset_values,faces,offsets);
        if(error==0)error=BuildLocalOffset(slot,faces,offsets,default,false,tolerance,out var product,Span<FaceSlot>.Empty);
        if(error==0)results->status=ParasolidConstants.PK_local_status_ok_c;
        return error;
    }

    private static int OffsetLocalFacesCore(int count,EntityTag* input,double* distances,double tolerance,PK_FACE_offset_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
    {
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(count<=0||input==null||distances==null||options==null||tracking==null||results==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=11)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if(!double.IsFinite(tolerance)||tolerance<=0)return ParasolidConstants.PK_ERROR_bad_tolerance;
        if(!SupportedLocalControls(options->offset_method,options->fix_degens,options->offset_step,options->blend_edges,options->grow,options->update)
            ||!LocalChoice(options->vertex_limit,ParasolidConstants.PK_VERTEX_limit_no_c)
            ||!LocalChoice(options->edge_limit,ParasolidConstants.PK_EDGE_limit_no_c))return ParasolidConstants.PK_ERROR_not_implemented;
        if(!State.Session->Tags.TryResolve(input[0],CurrentSessionId,out var face)||face.Pool!=(byte)PoolKind.Face)return ParasolidConstants.PK_ERROR_bad_class;
        var slot=Shells[Faces[face.Slot].BackShell].Body;
        var error=ResolveLocalBody(Bodies[slot].Header.Tag,out slot);if(error!=0)return error;
        var n=Bodies[slot].FaceCountBody;if(n<1||n>PlaneLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> faces=stackalloc FaceSlot[n];Span<double> offsets=stackalloc double[n];
        error=LocalFaceOffsets(slot,0,count,input,distances,faces,offsets);
        if(error==0)error=BuildLocalOffset(slot,faces,offsets,default,false,tolerance,out _,Span<FaceSlot>.Empty);
        if(error==0)results->status=ParasolidConstants.PK_local_status_ok_c;
        return error;
    }

    private static int HollowLocalBodyCore(EntityTag body,double offset,double tolerance,PK_BODY_hollow_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results,int* legacyCount,EntityTag** oldOutput,EntityTag** newOutput)
    {
        var error=ResolveLocalBody(body,out var slot);if(error!=0)return error;
        if(options==null||(legacyCount==null&&(tracking==null||results==null)))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(options->o_t_version!=12)return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if(!double.IsFinite(tolerance)||tolerance<=0)return ParasolidConstants.PK_ERROR_bad_tolerance;
        if(!double.IsFinite(offset)||offset==0)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(!SupportedLocalControls(options->offset_method,options->fix_degens,options->offset_step,options->blend_edges,options->grow,options->update)
            ||!LocalChoice(options->hollow_local,ParasolidConstants.PK_hollow_local_none_c)||options->n_local_faces!=0
            ||!LocalChoice(options->hollow_region,ParasolidConstants.PK_REGION_hollow_void_c)
            ||options->grow_cb!=null||options->pierce_tangent!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var count=Bodies[slot].FaceCountBody;if(count<1||count>PlaneLimit)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<FaceSlot> faces=stackalloc FaceSlot[count];Span<double> offsets=stackalloc double[count];Span<byte> pierced=stackalloc byte[count];pierced.Clear();
        error=LocalFaceOffsets(slot,offset,options->n_offset_faces,options->offset_faces,options->offset_values,faces,offsets);if(error!=0)return error;
        if(options->n_pierce_faces<0||options->n_pierce_faces>count||options->n_pierce_faces>0&&options->pierce_faces==null)return ParasolidConstants.PK_ERROR_bad_parameter;
        for(var i=0;i<options->n_pierce_faces;i++)
        {
            if(!State.Session->Tags.TryResolve(options->pierce_faces[i],CurrentSessionId,out var face)||face.Pool!=(byte)PoolKind.Face)return ParasolidConstants.PK_ERROR_bad_class;
            var index=faces.IndexOf(face.Slot);if(index<0)return ParasolidConstants.PK_ERROR_not_in_same_body;
            if(pierced[index]!=0)return ParasolidConstants.PK_ERROR_duplicate_list_item;
            pierced[index]=1;offsets[index]=0;
        }
        var created=0;
        for(var i=0;i<count;i++)
        {
            if(offsets[i]==0)pierced[i]=1;
            else if(Math.Sign(offsets[i])!=Math.Sign(offset))return ParasolidConstants.PK_ERROR_not_implemented;
            else created++;
        }
        if(created==0)return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<FaceSlot> products=stackalloc FaceSlot[2*count];
        error=BuildLocalOffset(slot,faces,offsets,pierced,true,tolerance,out _,products);if(error!=0)return error;
        if(tracking!=null)
        {
            error=LocalHollowTracking(faces,products,pierced,tracking);if(error!=0)return error;
            results->status=ParasolidConstants.PK_local_status_ok_c;
        }
        else
        {
            var oldFaces=oldOutput==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(created*sizeof(int)));
            var newFaces=newOutput==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(created*sizeof(int)));
            if(oldOutput!=null&&oldFaces==null||newOutput!=null&&newFaces==null)return ParasolidConstants.PK_ERROR_memory_full;
            var next=0;
            for(var i=0;i<count;i++)if(pierced[i]==0)
            {
                if(oldFaces!=null)oldFaces[next]=Faces[faces[i]].Header.Tag;
                if(newFaces!=null)newFaces[next]=Faces[products[count+next]].Header.Tag;
                next++;
            }
            *legacyCount=created;if(oldOutput!=null)*oldOutput=oldFaces;if(newOutput!=null)*newOutput=newFaces;
        }
        return 0;
    }

    private static int BuildLocalOffset(BodySlot source,ReadOnlySpan<FaceSlot> faces,ReadOnlySpan<double> offsets,
        ReadOnlySpan<byte> pierce,bool hollow,double tolerance,out EntityTag result,Span<FaceSlot> returnedProducts)
    {
        result=0;
        var any=false;for(var i=0;i<offsets.Length;i++)any|=offsets[i]!=0;
        if(!any&&!hollow){result=Bodies[source].Header.Tag;return 0;}
        var analytic=BuildAnalyticLocalOffset(source,faces,offsets,pierce,hollow,tolerance,out result,returnedProducts);
        if(analytic!=ParasolidConstants.PK_ERROR_not_implemented)return analytic;
        Span<LocalPlane> planes=stackalloc LocalPlane[faces.Length];
        var error=ReadLocalPlanes(source,faces,planes);if(error!=0)return error;
        Span<LocalPlane> moved=stackalloc LocalPlane[faces.Length];
        for(var i=0;i<planes.Length;i++)moved[i]=new(){Normal=planes[i].Normal,Distance=planes[i].Distance+offsets[i]};
        Span<KernelVector3> vertices=stackalloc KernelVector3[VertexLimit];Span<ulong> mask=stackalloc ulong[VertexLimit];
        Span<int> indices=stackalloc int[IndexLimit];Span<LocalPolygon> polygons=stackalloc LocalPolygon[faces.Length];
        if(!Build(moved,vertices,mask,indices,polygons,out var nVertices,out var nIndices,tolerance))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(nVertices!=Bodies[source].VertexCountBody)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<polygons.Length;i++)if(polygons[i].Count==0)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> original=stackalloc KernelVector3[VertexLimit];Span<ulong> oldMask=stackalloc ulong[VertexLimit];
        Span<int> oldIndices=stackalloc int[IndexLimit];Span<LocalPolygon> oldPolygons=stackalloc LocalPolygon[faces.Length];
        var oldVertexCount=0;
        if(hollow&&!Build(planes,original,oldMask,oldIndices,oldPolygons,out oldVertexCount,out _,tolerance))return ParasolidConstants.PK_ERROR_not_implemented;
        // Shared pierced edges make the two annular loops touch. They require
        // explicit coplanar boolean splitting, so reject before changing input.
        if(hollow)
            for(var a=0;a<faces.Length;a++)if(pierce[a]!=0)
                for(var b=a+1;b<faces.Length;b++)if(pierce[b]!=0)
                    for(var v=0;v<oldVertexCount;v++)if((oldMask[v]&((1UL<<a)|(1UL<<b)))==((1UL<<a)|(1UL<<b)))return ParasolidConstants.PK_ERROR_not_implemented;
        Span<KernelVector3> points=stackalloc KernelVector3[LocalPointLimit];Span<ModelingEdge> edges=stackalloc ModelingEdge[LocalEdgeLimit];
        Span<ModelingCoedge> coedges=stackalloc ModelingCoedge[LocalCoedgeLimit];Span<ModelingLoop> loops=stackalloc ModelingLoop[2*LocalFaceLimit];
        Span<ModelingFace> modelFaces=stackalloc ModelingFace[LocalFaceLimit];Span<byte> cavities=stackalloc byte[LocalFaceLimit];
        var brep=new LocalBrep{Points=points,Edges=edges,Coedges=coedges,Loops=loops,Faces=modelFaces,Cavities=cavities};
        var positive=false;var closed=hollow;
        for(var i=0;i<offsets.Length;i++){if(offsets[i]>0)positive=true;if(hollow&&pierce[i]!=0)closed=false;}
        if(hollow)
        {
            for(var i=0;i<faces.Length;i++)
            {
                error=CloneLocalPlane(faces[i],0,out var surf);if(error!=0)return error;
                var sense=Faces[faces[i]].Orientation;
                if(pierce[i]!=0)
                {
                    if(!AddLocalPolyFace(ref brep,positive?vertices:original,positive?indices:oldIndices,positive?polygons[i]:oldPolygons[i],surf,sense)
                        ||!AddLocalPolyHole(ref brep,positive?original:vertices,positive?oldIndices:indices,positive?oldPolygons[i]:polygons[i]))return ParasolidConstants.PK_ERROR_memory_full;
                }
                else if(!AddLocalPolyFace(ref brep,original,oldIndices,oldPolygons[i],surf,positive?FlipLocalSense(sense):sense,positive,(byte)(positive&&closed?1:0)))return ParasolidConstants.PK_ERROR_memory_full;
            }
        }
        for(var i=0;i<faces.Length;i++)
        {
            if(hollow&&pierce[i]!=0)continue;
            error=CloneLocalPlane(faces[i],offsets[i],out var surf);if(error!=0)return error;
            var reverse=hollow&&!positive;var sense=Faces[faces[i]].Orientation;
            if(!AddLocalPolyFace(ref brep,vertices,indices,polygons[i],surf,reverse?FlipLocalSense(sense):sense,reverse,(byte)(reverse&&closed?1:0)))return ParasolidConstants.PK_ERROR_memory_full;
        }
        Span<FaceSlot> products=stackalloc FaceSlot[brep.FaceCount];
        error=brep.Complete(out var product,products);if(error!=0)return error;
        error=ReplaceLocalBody(source,product,faces,products,!hollow);if(error!=0)return error;
        if(!returnedProducts.IsEmpty)products.CopyTo(returnedProducts);
        result=Bodies[source].Header.Tag;return 0;
    }

    private static int LocalHollowTracking(ReadOnlySpan<FaceSlot> originals,ReadOnlySpan<FaceSlot> products,
        ReadOnlySpan<byte> pierce,PK_TOPOL_track_r_s* tracking)
    {
        var count=0;for(var i=0;i<pierce.Length;i++)if(pierce[i]==0)count++;
        var bytes=(nuint)(count*(sizeof(PK_TOPOL_track_record_r_s)+3*sizeof(int)));
        var records=(PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(bytes);if(records==null)return ParasolidConstants.PK_ERROR_memory_full;
        var source=(int*)(records+count);var classes=source+count;var output=classes+count;var next=0;
        for(var i=0;i<originals.Length;i++)if(pierce[i]==0)
        {
            source[next]=Faces[originals[i]].Header.Tag;classes[next]=ParasolidConstants.PK_CLASS_face;output[next]=Faces[products[originals.Length+next]].Header.Tag;
            records[next]=new(){track=ParasolidConstants.PK_TOPOL_track_create_c,n_original_topols=1,original_topols=source+next,
                original_classes=classes+next,n_product_topols=1,product_topols=output+next};next++;
        }
        *tracking=new(){n_track_records=count,track_records=records,internal_origs=source,internal_classes=classes,internal_prods=output};return 0;
    }
}
