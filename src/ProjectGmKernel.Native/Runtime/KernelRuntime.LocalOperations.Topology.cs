using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.LocalOperations;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>
    /// Hollow a convex planar solid using the oriented boundary difference of
    /// its original and offset half-space intersections. In a pierce plane the
    /// two polygons belong to the same source group: shared edge intervals
    /// cancel, so touching holes become open notches, split faces or disappear.
    /// </summary>
    private static int TryHollowPlanarTopology(BodySlot body,ReadOnlySpan<FaceSlot> faces,
        ReadOnlySpan<double> offsets,ReadOnlySpan<byte> pierced,double tolerance,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* local,int* legacyCount,
        EntityTag** legacyOld,EntityTag** legacyNew,out bool handled)
    {
        handled=false;
        Span<LocalPlane> planes=stackalloc LocalPlane[faces.Length];
        var error=ReadLocalPlanes(body,faces,planes);
        if(error==ParasolidConstants.PK_ERROR_not_implemented)return 0;
        handled=true;if(error!=0)return error;
        Span<LocalPlane> shifted=stackalloc LocalPlane[faces.Length];var positive=false;
        for(var i=0;i<faces.Length;i++)
        {
            shifted[i]=new(){Normal=planes[i].Normal,Distance=planes[i].Distance+offsets[i]};
            positive|=offsets[i]>0;
        }
        Span<KernelVector3> original=stackalloc KernelVector3[VertexLimit];
        Span<KernelVector3> moved=stackalloc KernelVector3[VertexLimit];
        Span<ulong> masks=stackalloc ulong[VertexLimit];
        Span<BufferOffset> oldIndices=stackalloc BufferOffset[IndexLimit];
        Span<BufferOffset> newIndices=stackalloc BufferOffset[IndexLimit];
        Span<LocalPolygon> oldPolygons=stackalloc LocalPolygon[faces.Length];
        Span<LocalPolygon> newPolygons=stackalloc LocalPolygon[faces.Length];
        if(!Build(planes,original,masks,oldIndices,oldPolygons,out _,out _,tolerance))
            return ParasolidConstants.PK_ERROR_not_implemented;
        if(!Build(shifted,moved,masks,newIndices,newPolygons,out _,out _,tolerance))
            return ParasolidConstants.PK_ERROR_bad_parameter;

        Span<PlanarModelFace> sources=stackalloc PlanarModelFace[2*faces.Length];sources.Clear();
        Span<PlanarModelPolygon> polygons=stackalloc PlanarModelPolygon[2*faces.Length];
        Span<KernelVector3> points=stackalloc KernelVector3[2*IndexLimit];
        Span<EntityTag> originalTags=stackalloc EntityTag[faces.Length];
        Span<SurfTag> temporary=stackalloc SurfTag[faces.Length];temporary.Clear();
        var polygonCount=0;var pointCount=0;var epsilon=1e-10;
        for(var i=0;i<faces.Length;i++)
        {
            originalTags[i]=Faces[faces[i]].Header.Tag;
            epsilon=Math.Max(epsilon,2e-13*Math.Max(Math.Abs(planes[i].Distance),Math.Abs(shifted[i].Distance)));
            var reverse=positive&&pierced[i]==0;
            // Native hollowing retains source identities on the outer shell
            // for either sign; create records always describe new inner walls.
            var originalIndex=reverse?faces.Length+i:i;
            sources[originalIndex]=new(){Source=faces[i],Surface=Faces[faces[i]].SurfTag,
                Sense=reverse?FlipLocalSense(Faces[faces[i]].Orientation):NormalizeLocalSense(Faces[faces[i]].Orientation),
                Origin=Scale(planes[i].Normal,planes[i].Distance),Normal=Scale(planes[i].Normal,reverse?-1:1)};
            if(pierced[i]!=0)
            {
                if(!AppendLocalBoundaryPolygon(positive?moved:original,positive?newIndices:oldIndices,
                    positive?newPolygons[i]:oldPolygons[i],i,false,polygons,points,ref polygonCount,ref pointCount)
                    ||!AppendLocalBoundaryPolygon(positive?original:moved,positive?oldIndices:newIndices,
                        positive?oldPolygons[i]:newPolygons[i],i,true,polygons,points,ref polygonCount,ref pointCount))
                    return ParasolidConstants.PK_ERROR_memory_full;
                continue;
            }
            if(!AppendLocalBoundaryPolygon(original,oldIndices,oldPolygons[i],originalIndex,reverse,
                polygons,points,ref polygonCount,ref pointCount))return ParasolidConstants.PK_ERROR_memory_full;
            if(newPolygons[i].Count==0)continue;
            error=CloneLocalPlane(faces[i],offsets[i],out var shiftedSurface);if(error!=0)return error;
            temporary[i]=shiftedSurface;reverse=!positive;
            var shiftedIndex=positive?i:faces.Length+i;
            sources[shiftedIndex]=new(){Source=faces[i],Surface=shiftedSurface,
                Sense=reverse?FlipLocalSense(Faces[faces[i]].Orientation):NormalizeLocalSense(Faces[faces[i]].Orientation),
                Origin=Scale(shifted[i].Normal,shifted[i].Distance),Normal=Scale(shifted[i].Normal,reverse?-1:1)};
            if(!AppendLocalBoundaryPolygon(moved,newIndices,newPolygons[i],shiftedIndex,reverse,
                polygons,points,ref polygonCount,ref pointCount))return ParasolidConstants.PK_ERROR_memory_full;
        }
        Span<EntityTag> bodies=stackalloc EntityTag[LocalFaceLimit];
        Span<FaceTag> sourceProducts=stackalloc FaceTag[2*faces.Length];sourceProducts.Clear();
        error=ReconstructPlanarBodies(sources,polygons[..polygonCount],points[..pointCount],epsilon,
            true,false,bodies,out var bodyCount,sourceProducts);
        if(error!=0)return error;
        // PK_BODY_hollow_2 edits one body and rejects a disconnected material
        // result (for example two separated walls) with PK_ERROR_not_unique.
        if(bodyCount!=1)return ParasolidConstants.PK_ERROR_not_unique;
        for(var i=0;i<temporary.Length;i++)if(temporary[i]!=0)
        {
            var tag=temporary[i];error=EntityDelete(1,&tag);if(error!=0)return error;
        }
        var productBody=TagRec(bodies[0]).Slot;
        Span<FaceSlot> retained=stackalloc FaceSlot[faces.Length];
        Span<FaceSlot> products=stackalloc FaceSlot[faces.Length];var retainedCount=0;
        Span<FaceSlot> productSources=stackalloc FaceSlot[LocalFaceLimit];
        Span<FaceSlot> sourcePieces=stackalloc FaceSlot[LocalFaceLimit];var pieceCount=0;
        Span<EntityTag> derived=stackalloc EntityTag[LocalFaceLimit];
        Span<BufferOffset> origins=stackalloc BufferOffset[LocalFaceLimit];var derivedCount=0;
        for(var i=0;i<faces.Length;i++)
        {
            if(sourceProducts[i]==0)continue;
            var first=TagRec(sourceProducts[i]).Slot;
            retained[retainedCount]=faces[i];products[retainedCount++]=first;
            sourcePieces[pieceCount]=first;productSources[pieceCount++]=faces[i];
            var f=Bodies[productBody].FirstFaceBody;
            for(var j=0;j<Bodies[productBody].FaceCountBody;j++,f=Faces[f].NextInBody)
                if(f!=first&&Faces[f].SurfTag==Faces[first].SurfTag)
                {
                    if(derivedCount==derived.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    derived[derivedCount]=Faces[f].Header.Tag;origins[derivedCount++]=i;
                    if(pieceCount==sourcePieces.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    sourcePieces[pieceCount]=f;productSources[pieceCount++]=faces[i];
                }
        }
        error=ReplaceLocalBody(body,bodies[0],retained[..retainedCount],products[..retainedCount],false,
            sourcePieces[..pieceCount],productSources[..pieceCount]);
        if(error!=0)return error;
        if(tracking!=null)
        {
            error=LocalTopologyHollowTracking(originalTags,sourceProducts[faces.Length..],derived[..derivedCount],
                origins[..derivedCount],tracking);
            if(error==0)local->status=ParasolidConstants.PK_local_status_ok_c;
            return error;
        }
        var created=0;for(var i=faces.Length;i<sourceProducts.Length;i++)if(sourceProducts[i]!=0)created++;
        var oldOutput=legacyOld==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(created*sizeof(EntityTag)));
        var newOutput=legacyNew==null?null:(EntityTag*)State.Session->Returns.TryAllocate((nuint)(created*sizeof(EntityTag)));
        if(legacyOld!=null&&oldOutput==null||legacyNew!=null&&newOutput==null)return ParasolidConstants.PK_ERROR_memory_full;
        var next=0;
        for(var i=0;i<faces.Length;i++)if(sourceProducts[faces.Length+i]!=0)
        {
            if(oldOutput!=null)oldOutput[next]=originalTags[i];
            if(newOutput!=null)newOutput[next]=sourceProducts[faces.Length+i];next++;
        }
        *legacyCount=created;if(legacyOld!=null)*legacyOld=oldOutput;if(legacyNew!=null)*legacyNew=newOutput;
        return 0;
    }

    private static int RetainLocalHollowBoundary(BodySlot source,BodySlot target,ReadOnlySpan<FaceSlot> moved,
        ReadOnlySpan<FaceSlot> retained,ReadOnlySpan<FaceSlot> pieces,ReadOnlySpan<FaceSlot> origins)
    {
        Span<EdgeSlot> oldEdges=stackalloc EdgeSlot[Bodies[source].EdgeCountBody];
        Span<VertexSlot> oldVertices=stackalloc VertexSlot[Bodies[source].VertexCountBody];
        Span<EdgeSlot> newEdges=stackalloc EdgeSlot[Bodies[target].EdgeCountBody];
        Span<VertexSlot> newVertices=stackalloc VertexSlot[Bodies[target].VertexCountBody];
        CollectProfileSlots(source,oldEdges,oldVertices);CollectProfileSlots(target,newEdges,newVertices);
        Span<FaceSlot> pieceFaces=stackalloc FaceSlot[pieces.Length];
        Span<FaceSlot> originalFaces=stackalloc FaceSlot[pieces.Length];
        Span<FaceSlot> allSources=stackalloc FaceSlot[Bodies[source].FaceCountBody];
        var sourceFace=Bodies[source].FirstFaceBody;
        for(var i=0;i<allSources.Length;i++,sourceFace=Faces[sourceFace].NextInBody)allSources[i]=sourceFace;
        for(var i=0;i<pieces.Length;i++)
        {
            pieceFaces[i]=ProfileMappedSlot(pieces[i],moved,retained);
            originalFaces[i]=ProfileMappedSlot(origins[i],retained,moved);
        }
        Span<EdgeSlot> edgeFrom=stackalloc EdgeSlot[oldEdges.Length];Span<EdgeSlot> edgeTo=stackalloc EdgeSlot[oldEdges.Length];var edgeCount=0;
        Span<VertexSlot> vertexFrom=stackalloc VertexSlot[oldVertices.Length];Span<VertexSlot> vertexTo=stackalloc VertexSlot[oldVertices.Length];var vertexCount=0;
        for(var i=0;i<oldEdges.Length;i++)
        {
            var slot=oldEdges[i];if(!LocalEdgeFaces(slot,out var a,out var b))continue;
            var start=Points[GetPointSlotByTag(Vertices[Edges[slot].StartVertex].PointTag)].Position;
            var found=-1;var best=double.PositiveInfinity;
            for(var j=0;j<newEdges.Length;j++)
            {
                if(edgeTo[..edgeCount].Contains(newEdges[j])||!LocalEdgeFaces(newEdges[j],out var c,out var d))continue;
                var ci=pieceFaces.IndexOf(c);var di=pieceFaces.IndexOf(d);if(ci<0||di<0)continue;
                c=originalFaces[ci];d=originalFaces[di];if(!(a==c&&b==d||a==d&&b==c))continue;
                // When a pierce edge becomes two disjoint intervals, one
                // surviving interval retains the original edge identity.
                var x=Points[GetPointSlotByTag(Vertices[Edges[newEdges[j]].StartVertex].PointTag)].Position;
                var y=Points[GetPointSlotByTag(Vertices[Edges[newEdges[j]].EndVertex].PointTag)].Position;
                var distance=Math.Min(Norm(Sub(start,x)),Norm(Sub(start,y)));
                if(distance<best){best=distance;found=j;}
            }
            if(found>=0){edgeFrom[edgeCount]=slot;edgeTo[edgeCount++]=newEdges[found];}
        }
        for(var i=0;i<oldVertices.Length;i++)
        {
            var mask=LocalVertexFaceMask(oldVertices[i],allSources);var found=-1;
            for(var j=0;j<newVertices.Length;j++)
            {
                if(vertexTo[..vertexCount].Contains(newVertices[j]))continue;
                if(LocalHollowVertexMask(newVertices[j],pieceFaces,originalFaces,allSources,out var next)&&next==mask)
                {if(found>=0)return ParasolidConstants.PK_ERROR_not_implemented;found=j;}
            }
            if(found>=0){vertexFrom[vertexCount]=oldVertices[i];vertexTo[vertexCount++]=newVertices[found];}
        }
        PreserveLocalBoundaryHandles(source,target,oldEdges,oldVertices,newEdges,newVertices,
            edgeFrom[..edgeCount],edgeTo[..edgeCount],vertexFrom[..vertexCount],vertexTo[..vertexCount]);
        return 0;
    }

    private static bool LocalHollowVertexMask(VertexSlot vertex,ReadOnlySpan<FaceSlot> pieces,
        ReadOnlySpan<FaceSlot> origins,ReadOnlySpan<FaceSlot> originalFaces,out ulong mask)
    {
        mask=0;var first=Vertices[vertex].FirstFinVertex;if(first<0)return false;var fin=first;var guard=0;
        do
        {
            if(Fins[fin].Loop<0)return false;
            var piece=pieces.IndexOf(Loops[Fins[fin].Loop].Face);if(piece<0)return false;
            var source=originalFaces.IndexOf(origins[piece]);if(source<0)return false;mask|=1UL<<source;
            fin=Fins[fin].NextAtVertex;
        }while(fin>=0&&fin!=first&&++guard<LocalCoedgeLimit);
        return fin==first&&mask!=0;
    }

    private static bool AppendLocalBoundaryPolygon(ReadOnlySpan<KernelVector3> vertices,ReadOnlySpan<BufferOffset> indices,
        LocalPolygon polygon,BufferOffset source,bool reverse,Span<PlanarModelPolygon> output,Span<KernelVector3> points,
        ref BufferCount polygonCount,ref BufferCount pointCount)
    {
        if(polygon.Count==0)return true;
        if(polygonCount==output.Length||pointCount>points.Length-polygon.Count)return false;
        output[polygonCount++]=new(){Face=source,First=pointCount,Count=polygon.Count};
        for(var i=0;i<polygon.Count;i++)
            points[pointCount++]=vertices[indices[polygon.First+(reverse?polygon.Count-1-i:i)]];
        return true;
    }

    private static int LocalTopologyHollowTracking(ReadOnlySpan<EntityTag> originals,ReadOnlySpan<FaceTag> walls,
        ReadOnlySpan<EntityTag> derived,ReadOnlySpan<BufferOffset> origins,PK_TOPOL_track_r_s* tracking)
    {
        var creates=0;var splits=0;
        for(var i=0;i<originals.Length;i++){if(walls[i]!=0)creates++;if(origins.Contains(i))splits++;}
        var count=creates+splits;var productCount=creates+derived.Length;
        if(count==0){*tracking=default;return 0;}
        var bytes=(nuint)(count*(sizeof(PK_TOPOL_track_record_r_s)+2*sizeof(int))+productCount*sizeof(int));
        var records=(PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(bytes);
        if(records==null)return ParasolidConstants.PK_ERROR_memory_full;
        var source=(EntityTag*)(records+count);var classes=source+count;var products=classes+count;
        var next=0;var product=0;
        for(var i=0;i<originals.Length;i++)if(walls[i]!=0)
        {
            source[next]=originals[i];classes[next]=ParasolidConstants.PK_CLASS_face;products[product]=walls[i];
            records[next]=new(){track=ParasolidConstants.PK_TOPOL_track_create_c,n_original_topols=1,
                original_topols=source+next,original_classes=classes+next,n_product_topols=1,product_topols=products+product};
            next++;product++;
        }
        for(var i=0;i<originals.Length;i++)if(origins.Contains(i))
        {
            var first=product;
            for(var j=0;j<derived.Length;j++)if(origins[j]==i)products[product++]=derived[j];
            source[next]=originals[i];classes[next]=ParasolidConstants.PK_CLASS_face;
            records[next]=new(){track=ParasolidConstants.PK_TOPOL_track_derive_c,n_original_topols=1,
                original_topols=source+next,original_classes=classes+next,n_product_topols=product-first,product_topols=products+first};
            next++;
        }
        *tracking=new(){n_track_records=count,track_records=records,internal_origs=source,internal_classes=classes,internal_prods=products};
        return 0;
    }
}
