using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RegularBooleanOrigin
    {
        internal BufferOffset Edge;
        internal FaceSlot Face;
    }

    private static BufferCount ReadRegularBooleanOrigins(scoped ref SheetArrangement model,scoped Span<RegularBooleanOrigin> output)
    {
        if(output.IsEmpty)return 0;var count=0;
        // At most one row per coedge. Preserve occurrences before coincident
        // cells are discarded, then follow the actual edge unification/merge.
        for(var f=0;f<model.NF;f++)
        {
            var face=model.Faces[f];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                foreach(var use in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                    output[count++]=new(){Edge=use.Edge,Face=model.Sources[f]};
            }
        }
        return count;
    }

    // Basic tracking is the source intersection relation, including source
    // occurrences removed by same-domain material selection. Reconstructing
    // origins from surviving faces alone loses those participants.
    private static int CreateRegularBooleanTracking(scoped ref SheetArrangement model,
        scoped ReadOnlySpan<BufferOffset> first,scoped ReadOnlySpan<SheetSourceEdge> sources,
        scoped ReadOnlySpan<RegularBooleanOrigin> origins,scoped ReadOnlySpan<EdgeSlot> products,PK_TOPOL_track_r_s* result)
    {
        Span<EntityTag> tags=stackalloc EntityTag[RegularInputEdges+RegularInputFaces];
        Span<KernelClassCode> kinds=stackalloc KernelClassCode[tags.Length];Span<BodySlot> owners=stackalloc BodySlot[tags.Length];
        var count=0;var originalCount=0;PK_TOPOL_track_record_r_s* records=null;EntityTag* originals=null;KernelClassCode* classes=null;EntityTag* output=null;
        for(var pass=0;pass<2;pass++)
        {
            var index=0;var offset=0;
            for(var e=0;e<model.NE;e++)
            {
                if(first[e]<0)continue;
                var n=0;
                foreach(var range in sources)
                {
                    var participates=false;
                    for(var k=range.First;k<range.First+range.Count;k++)participates|=model.CanonicalEdge(k)==e;
                    if(!participates)continue;
                    var edge=Edges[range.Source];
                    if(tags[..n].Contains(edge.Header.Tag))continue;
                    tags[n]=edge.Header.Tag;kinds[n]=ParasolidConstants.PK_CLASS_edge;owners[n++]=edge.Body;
                }
                var edgeCount=n;
                foreach(var origin in origins)
                {
                    if(model.CanonicalEdge(origin.Edge)!=e)continue;
                    var body=TopologyBody(PoolKind.Face,origin.Face);
                    // An original finite edge is a more specific participant
                    // than either of its incident source faces in that body.
                    if(owners[..edgeCount].Contains(body))continue;
                    var tag=Faces[origin.Face].Header.Tag;if(tags[..n].Contains(tag))continue;
                    tags[n]=tag;kinds[n]=ParasolidConstants.PK_CLASS_face;owners[n++]=body;
                }
                var differentBodies=false;for(var i=1;i<n;i++)differentBodies|=owners[i]!=owners[0];
                if(!differentBodies)continue;
                if(pass!=0)
                {
                    if(products[e]<0)return ParasolidConstants.PK_ERROR_bad_loop;
                    tags[..n].CopyTo(new Span<EntityTag>(originals+offset,n));kinds[..n].CopyTo(new Span<KernelClassCode>(classes+offset,n));
                    output[index]=Edges[products[e]].Header.Tag;
                    records[index]=new(){track=ParasolidConstants.PK_TOPOL_track_derive_c,n_original_topols=n,
                        original_topols=originals+offset,original_classes=classes+offset,n_product_topols=1,product_topols=output+index};
                }
                index++;offset+=n;
            }
            if(pass!=0)break;count=index;originalCount=offset;if(count==0)return 0;
            records=(PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(
                (nuint)(count*sizeof(PK_TOPOL_track_record_r_s)+(2*originalCount+count)*sizeof(EntityTag)));
            if(records==null)return ParasolidConstants.PK_ERROR_memory_full;
            originals=(EntityTag*)(records+count);classes=originals+originalCount;output=classes+originalCount;
        }
        *result=new(){n_track_records=count,track_records=records,internal_origs=originals,internal_classes=classes,internal_prods=output};return 0;
    }
}
