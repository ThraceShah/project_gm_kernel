using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int CreateProfileExtrudeTracking(BodyTag profile,BodyTag product,PK_TOPOL_track_r_s* result)
    {
        ref readonly var source=ref Bodies[TagRec(profile).Slot];
        ref readonly var output=ref Bodies[TagRec(product).Slot];
        var count=source.EdgeCountBody+2;
        var bytes=(nuint)(count*(sizeof(PK_TOPOL_track_record_r_s)+3*sizeof(int)));
        var records=(PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(bytes);
        if(records==null)return ParasolidConstants.PK_ERROR_memory_full;
        var originals=(int*)(records+count);var classes=originals+count;var products=classes+count;
        Span<EntityTag> faces=stackalloc EntityTag[count];
        var face=output.FirstFaceBody;
        for(var i=0;i<count;i++,face=Faces[face].NextInBody)faces[i]=Faces[face].Header.Tag;
        originals[0]=originals[1]=Faces[source.FirstFaceBody].Header.Tag;
        classes[0]=classes[1]=ParasolidConstants.PK_CLASS_face;
        products[0]=faces[count-2];products[1]=faces[count-1];
        var edge=source.FirstEdgeBody;
        for(var i=0;i<source.EdgeCountBody;i++,edge=Edges[edge].NextInBody)
        {
            // The SDK returns the two caps first, then the lateral records in
            // reverse source-edge order. Tags identify the association.
            var target=count-1-i;
            originals[target]=Edges[edge].Header.Tag;classes[target]=ParasolidConstants.PK_CLASS_edge;
            products[target]=faces[i];
        }
        for(var i=0;i<count;i++)
            records[i]=new PK_TOPOL_track_record_r_s
            {
                track=ParasolidConstants.PK_TOPOL_track_create_c,n_original_topols=1,
                original_topols=originals+i,original_classes=classes+i,n_product_topols=1,product_topols=products+i,
            };
        *result=new PK_TOPOL_track_r_s
        {
            n_track_records=count,track_records=records,internal_origs=originals,internal_classes=classes,internal_prods=products,
        };
        return 0;
    }

    public static int TopolTrackResultFree(PK_TOPOL_track_r_s* result)
    {
        if(result==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        // All records and their arrays are a single returned-memory block.
        var error=result->track_records==null?0:KernelRuntime.MemoryFree(result->track_records);
        if(error==0)*result=default;
        return error;
    }

    public static int TopolLocalResultFree(PK_TOPOL_local_r_s* result)
    {
        if(result==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        var error=result->error_entities==null?0:KernelRuntime.MemoryFree(result->error_entities);
        if(error==0)*result=default;
        return error;
    }
}
