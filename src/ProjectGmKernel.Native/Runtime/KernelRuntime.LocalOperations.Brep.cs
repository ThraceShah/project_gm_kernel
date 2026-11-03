using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.LocalOperations;
using static ProjectGmKernel.Native.Geometry.LocalOperations.ConvexLocalModel;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const int LocalFaceLimit=512;
    private const int LocalPointLimit=1024;
    private const int LocalEdgeLimit=1536;
    private const int LocalCoedgeLimit=4096;

    private ref struct LocalBrep
    {
        internal Span<KernelVector3> Points;
        internal Span<ModelingEdge> Edges;
        internal Span<ModelingCoedge> Coedges;
        internal Span<ModelingLoop> Loops;
        internal Span<ModelingFace> Faces;
        internal Span<byte> Cavities;
        internal BufferCount PointCount,EdgeCount,CoedgeCount,LoopCount,FaceCount;
        internal int AddPoint(KernelVector3 point)
        {
            var epsilon=2e-12*Math.Max(1,Norm(point));
            for(var i=0;i<PointCount;i++)if(Norm(Sub(Points[i],point))<=epsilon)return i;
            if(PointCount==Points.Length)return -1;
            Points[PointCount]=point;return PointCount++;
        }
        internal int FindEdge(int a,int b)
        {
            for(var i=0;i<EdgeCount;i++)if(Edges[i].StartVertex==a&&Edges[i].EndVertex==b||Edges[i].StartVertex==b&&Edges[i].EndVertex==a)return i;
            return -1;
        }
        internal int AddLine(int a,int b)
        {
            var edge=FindEdge(a,b);if(edge>=0)return edge;
            if(a==b||a<0||b<0||EdgeCount==Edges.Length)return -1;
            Edges[EdgeCount]=new(){StartVertex=a,EndVertex=b,Start=0,End=Norm(Sub(Points[b],Points[a]))};
            return EdgeCount++;
        }
        internal bool AddLoop(scoped ReadOnlySpan<int> points,bool reverse=false)
        {
            if(points.Length<3||LoopCount==Loops.Length||CoedgeCount+points.Length>Coedges.Length)return false;
            var first=CoedgeCount;
            for(var i=0;i<points.Length;i++)
            {
                var a=points[reverse?points.Length-1-i:i];var b=points[reverse?(points.Length-2-i+points.Length)%points.Length:(i+1)%points.Length];
                var edge=AddLine(a,b);if(edge<0)return false;
                Coedges[CoedgeCount++]=new(){Edge=edge,Forward=(byte)(Edges[edge].StartVertex==a?1:0)};
            }
            Loops[LoopCount++]=new(){FirstCoedge=first,CoedgeCount=points.Length};return true;
        }
        internal bool AddFace(SurfTag surface,KernelSense sense,scoped ReadOnlySpan<int> points,bool reverse=false,byte cavity=0)
        {
            if(surface<=0||FaceCount==Faces.Length)return false;
            var first=LoopCount;
            if(!points.IsEmpty&&!AddLoop(points,reverse))return false;
            Faces[FaceCount]=new(){Surface=surface,Sense=NormalizeLocalSense(sense),FirstLoop=first,LoopCount=LoopCount-first};
            Cavities[FaceCount++]=cavity;return true;
        }
        internal int AddRing(KernelVector3 center,KernelVector3 axis,KernelVector3 reference,double radius)
        {
            if(EdgeCount==Edges.Length)return -1;
            var curve=CreateCircleCurveTag(center.X,center.Y,center.Z,axis.X,axis.Y,axis.Z,reference.X,reference.Y,reference.Z,radius);
            if(curve<=0)return -1;
            Edges[EdgeCount]=new(){Curve=curve,StartVertex=-1,EndVertex=-1,Start=0,End=Math.Tau};return EdgeCount++;
        }
        internal bool AddRingFace(SurfTag surface,KernelSense sense,int first,bool firstForward,int second=-1,bool secondForward=false,byte cavity=0)
        {
            if(FaceCount==Faces.Length||LoopCount+2>Loops.Length||CoedgeCount+2>Coedges.Length||surface<=0||first<0)return false;
            var start=LoopCount;
            Coedges[CoedgeCount]=new(){Edge=first,Forward=(byte)(firstForward?1:0)};
            Loops[LoopCount++]=new(){FirstCoedge=CoedgeCount++,CoedgeCount=1};
            if(second>=0){Coedges[CoedgeCount]=new(){Edge=second,Forward=(byte)(secondForward?1:0)};Loops[LoopCount++]=new(){FirstCoedge=CoedgeCount++,CoedgeCount=1};}
            Faces[FaceCount]=new(){Surface=surface,Sense=NormalizeLocalSense(sense),FirstLoop=start,LoopCount=LoopCount-start};Cavities[FaceCount++]=cavity;return true;
        }
        internal void NegativeCurveUse(int edge)
        {
            // Reversing the topological edge and both of its coedges leaves
            // every oriented face boundary unchanged. The curve's natural
            // parameterization remains unchanged; its edge use becomes '-'.
            ref var e=ref Edges[edge];Curves[GetCurveSlotByTag(e.Curve)].Sense=ParasolidConstants.PK_TOPOL_sense_negative_c;
            (e.StartVertex,e.EndVertex)=(e.EndVertex,e.StartVertex);
            for(var i=0;i<CoedgeCount;i++)if(Coedges[i].Edge==edge)Coedges[i].Forward=(byte)(1-Coedges[i].Forward);
        }
        internal int Complete(out EntityTag body,Span<FaceSlot> products)
        {
            body=0;
            for(var i=0;i<EdgeCount;i++)
            {
                if(Edges[i].Curve>0)continue;
                var p=Points[Edges[i].StartVertex];var d=Sub(Points[Edges[i].EndVertex],p);
                var curve=CreateLineCurveTag(p.X,p.Y,p.Z,d.X,d.Y,d.Z);
                if(curve<=0)return ParasolidConstants.PK_ERROR_memory_full;
                Edges[i].Curve=curve;
            }
            return BuildModelingBrep(Points[..PointCount],Edges[..EdgeCount],Coedges[..CoedgeCount],Loops[..LoopCount],
                Faces[..FaceCount],true,out body,products,Cavities[..FaceCount]);
        }
    }

    private static int ReadLocalPlanes(BodySlot body,ReadOnlySpan<FaceSlot> faces,Span<LocalPlane> planes)
    {
        if(faces.Length>PlaneLimit||Bodies[body].RegionCount!=2||Bodies[body].ShellCount!=2)return ParasolidConstants.PK_ERROR_not_implemented;
        for(var i=0;i<faces.Length;i++)
        {
            ref var face=ref Faces[faces[i]];
            if(face.LoopCount!=1)return ParasolidConstants.PK_ERROR_not_implemented;
            var surface=GetSurfaceSlotByTag(face.SurfTag);if(surface<0||Surfaces[surface].Class!=SurfaceClass.Plane)return ParasolidConstants.PK_ERROR_not_implemented;
            var data=PlaneDataPool[Surfaces[surface].DataIndex];
            var sign=LocalSenseSign(face.Orientation)*LocalSenseSign(Surfaces[surface].Sense);
            var normal=Scale(new(){X=data.NormalX,Y=data.NormalY,Z=data.NormalZ},sign);
            normal=Unit(normal);var p=new KernelVector3{X=data.LocationX,Y=data.LocationY,Z=data.LocationZ};
            planes[i]=new(){Normal=normal,Distance=Dot(normal,p)};
            var loop=Loops[face.FirstLoop];var fin=loop.FirstFin;
            for(var j=0;j<loop.FinCount;j++,fin=Fins[fin].NextInLoop)
            {
                var edge=Fins[fin].Edge;
                if(edge<0||Edges[edge].FinCount!=2||Edges[edge].StartVertex<0||Edges[edge].EndVertex<0
                    ||Curves[GetCurveSlotByTag(Edges[edge].CurveTag)].Class!=CurveClass.Line)return ParasolidConstants.PK_ERROR_not_implemented;
            }
        }
        // Checking every input vertex against every plane prevents accidentally
        // replacing a nonconvex solid by its convex intersection of halfspaces.
        var vertex=Bodies[body].FirstVertexBody;
        for(var v=0;v<Bodies[body].VertexCountBody;v++,vertex=Vertices[vertex].NextInBody)
        {
            var p=Points[GetPointSlotByTag(Vertices[vertex].PointTag)].Position;
            for(var f=0;f<faces.Length;f++)
                if(Dot(planes[f].Normal,p)-planes[f].Distance>1e-9*Math.Max(1,Norm(p)))return ParasolidConstants.PK_ERROR_not_implemented;
        }
        return 0;
    }

    private static int LocalSenseSign(KernelSense sense)=>sense==ParasolidConstants.PK_TOPOL_sense_negative_c?-1:1;
    private static KernelSense NormalizeLocalSense(KernelSense sense)=>LocalSenseSign(sense)<0?ParasolidConstants.PK_TOPOL_sense_negative_c:ParasolidConstants.PK_TOPOL_sense_positive_c;
    private static KernelSense FlipLocalSense(KernelSense sense)=>LocalSenseSign(sense)<0?ParasolidConstants.PK_TOPOL_sense_positive_c:ParasolidConstants.PK_TOPOL_sense_negative_c;

    private static int CloneLocalPlane(FaceSlot face,double offset,out SurfTag surface)
    {
        var error=TryCloneEntityCore(Faces[face].SurfTag,out surface);if(error!=0)return error;
        var slot=GetSurfaceSlotByTag(surface);ref var data=ref PlaneDataPool[Surfaces[slot].DataIndex];
        var distance=offset*LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(Surfaces[slot].Sense);
        data.LocationX+=distance*data.NormalX;data.LocationY+=distance*data.NormalY;data.LocationZ+=distance*data.NormalZ;
        return 0;
    }

    private static int LocalPlaneTag(KernelVector3 normal,KernelVector3 point,out SurfTag surface)
    {
        var reference=ConvexLocalModel.Cross(Math.Abs(normal.X)<.7?new KernelVector3{X=1}:new KernelVector3{Y=1},normal);
        reference=Unit(reference);
        surface=CreatePlaneSurfaceTag(point.X,point.Y,point.Z,normal.X,normal.Y,normal.Z,reference.X,reference.Y,reference.Z);
        return surface>0?0:ParasolidConstants.PK_ERROR_memory_full;
    }

    private static bool AddLocalPolyFace(ref LocalBrep brep,ReadOnlySpan<KernelVector3> vertices,
        ReadOnlySpan<int> indices,LocalPolygon polygon,SurfTag surface,KernelSense sense,bool reverse=false,byte cavity=0)
    {
        Span<int> mapped=stackalloc int[VertexLimit];
        if(polygon.Count<3)return false;
        for(var i=0;i<polygon.Count;i++)if((mapped[i]=brep.AddPoint(vertices[indices[polygon.First+i]]))<0)return false;
        return brep.AddFace(surface,sense,mapped[..polygon.Count],reverse,cavity);
    }

    /// <summary>Retain the public identities of surviving faces before replacing the body.</summary>
    private static int ReplaceLocalBody(BodySlot source,EntityTag output,ReadOnlySpan<FaceSlot> oldFaces,Span<FaceSlot> products,
        bool retainAllEdges=false,ReadOnlySpan<FaceSlot> hollowPieces=default,ReadOnlySpan<FaceSlot> hollowOrigins=default)
    {
        var target=TagRec(output).Slot;
        Span<int> from=stackalloc int[oldFaces.Length];products[..oldFaces.Length].CopyTo(from);
        var preserve=PreserveModelingFaceHandles(source,target,oldFaces,products);if(preserve!=0)return preserve;
        if(retainAllEdges)
        {
            // Preserve a vertex by its incident source-face set. Coordinate
            // matching would fail precisely when the offset moved that vertex.
            var error=RetainLocalOffsetBoundary(source,target,from,oldFaces);
            if(error!=0)return error;
        }
        else if(!hollowPieces.IsEmpty)
        {
            var error=RetainLocalHollowBoundary(source,target,from,oldFaces,hollowPieces,hollowOrigins);if(error!=0)return error;
        }
        else
        {
            var error=RetainLocalSurvivingBoundary(source,target,from,oldFaces);if(error!=0)return error;
        }
        return ReplaceProfileBody(source,target);
    }

    internal static int PreserveModelingFaceHandles(BodySlot source,BodySlot target,ReadOnlySpan<FaceSlot> oldFaces,Span<FaceSlot> products)
    {
        if(oldFaces.Length>products.Length||!SnapshotProfileTopology(source)||!SnapshotProfileTopology(target))return ParasolidConstants.PK_ERROR_memory_full;
        if(!SnapshotLocalFaceLinks(source)||!SnapshotLocalFaceLinks(target))return ParasolidConstants.PK_ERROR_memory_full;
        Span<int> sourceFaces=stackalloc int[Bodies[source].FaceCountBody];var sourceFace=Bodies[source].FirstFaceBody;
        for(var i=0;i<sourceFaces.Length;i++,sourceFace=Faces[sourceFace].NextInBody)sourceFaces[i]=sourceFace;
        Span<int> targetFaces=stackalloc int[Bodies[target].FaceCountBody];var face=Bodies[target].FirstFaceBody;
        for(var i=0;i<targetFaces.Length;i++,face=Faces[face].NextInBody)targetFaces[i]=face;
        Span<int> from=stackalloc int[oldFaces.Length];products[..oldFaces.Length].CopyTo(from);
        for(var i=0;i<oldFaces.Length;i++)
        {
            var a=Faces[oldFaces[i]];var b=Faces[from[i]];
            Faces[oldFaces[i]]=b;Faces[oldFaces[i]].Header=a.Header;
            Faces[from[i]]=a;Faces[from[i]].Header=b.Header;
        }
        RemapLocalFaces(source,sourceFaces,oldFaces,from);
        RemapLocalFaces(target,targetFaces,from,oldFaces);
        RebuildBoundaryGeometryLinks(source,resetOwners:true);RebuildBoundaryGeometryLinks(target,resetOwners:true);
        oldFaces.CopyTo(products);
        return 0;
    }

    private static bool SnapshotLocalFaceLinks(BodySlot body)
    {
        var face=Bodies[body].FirstFaceBody;
        for(var i=0;i<Bodies[body].FaceCountBody;i++,face=Faces[face].NextInBody)
        {
            ref var f=ref Faces[face];
            if(f.BackFaceUse>=0&&!State.Session->TrySnapshot(ref FaceUses[f.BackFaceUse]))return false;
            if(f.FrontFaceUse>=0&&!State.Session->TrySnapshot(ref FaceUses[f.FrontFaceUse]))return false;
            var loop=f.FirstLoop;
            for(var j=0;j<f.LoopCount;j++,loop=Loops[loop].NextInFace)if(!State.Session->TrySnapshot(ref Loops[loop]))return false;
        }
        return true;
    }

    private static void RemapLocalFaces(BodySlot body,ReadOnlySpan<int> faceList,ReadOnlySpan<int> from,ReadOnlySpan<int> to)
    {
        ref var b=ref Bodies[body];b.FirstFaceBody=ProfileMappedSlot(b.FirstFaceBody,from,to);b.LastFaceBody=ProfileMappedSlot(b.LastFaceBody,from,to);
        for(var i=0;i<faceList.Length;i++)
        {
            var face=ProfileMappedSlot(faceList[i],from,to);ref var f=ref Faces[face];
            f.NextInBody=ProfileMappedSlot(f.NextInBody,from,to);f.PrevInBody=ProfileMappedSlot(f.PrevInBody,from,to);
            if(f.BackFaceUse>=0)FaceUses[f.BackFaceUse].Face=face;
            if(f.FrontFaceUse>=0)FaceUses[f.FrontFaceUse].Face=face;
            var loop=f.FirstLoop;
            for(var j=0;j<f.LoopCount;j++,loop=Loops[loop].NextInFace)Loops[loop].Face=face;
        }
    }

    private static ulong LocalVertexFaceMask(VertexSlot vertex,ReadOnlySpan<int> faces)
    {
        ulong result=0;var fin=Vertices[vertex].FirstFinVertex;
        if(fin<0)return 0;
        var first=fin;var guard=0;
        do
        {
            if(Fins[fin].Loop>=0){var index=faces.IndexOf(Loops[Fins[fin].Loop].Face);if(index>=0&&index<64)result|=1UL<<index;}
            fin=Fins[fin].NextAtVertex;
        }while(fin>=0&&fin!=first&&++guard<LocalCoedgeLimit);
        return result;
    }

    private static int RetainLocalOffsetBoundary(BodySlot source,BodySlot target,ReadOnlySpan<int> oldMovedFaces,ReadOnlySpan<int> retainedFaces)
    {
        var edgeCount=Bodies[source].EdgeCountBody;var vertexCount=Bodies[source].VertexCountBody;
        if(edgeCount!=Bodies[target].EdgeCountBody||vertexCount!=Bodies[target].VertexCountBody)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<int> oldEdges=stackalloc int[edgeCount];Span<int> oldVertices=stackalloc int[vertexCount];
        Span<int> newEdges=stackalloc int[edgeCount];Span<int> newVertices=stackalloc int[vertexCount];
        CollectProfileSlots(source,oldEdges,oldVertices);CollectProfileSlots(target,newEdges,newVertices);
        Span<int> mappedVertices=stackalloc int[vertexCount];Span<int> mappedEdges=stackalloc int[edgeCount];
        for(var i=0;i<vertexCount;i++)
        {
            var mask=LocalVertexFaceMask(oldVertices[i],oldMovedFaces);var found=-1;
            for(var j=0;j<vertexCount;j++)if(LocalVertexFaceMask(newVertices[j],retainedFaces)==mask){if(found>=0)return ParasolidConstants.PK_ERROR_not_implemented;found=j;}
            if(found<0)return ParasolidConstants.PK_ERROR_not_implemented;mappedVertices[i]=newVertices[found];
        }
        for(var i=0;i<edgeCount;i++)
        {
            var old=Edges[oldEdges[i]];var fa=old.FirstFinEdge;var fb=Fins[fa].Other;
            var a=oldMovedFaces.IndexOf(Loops[Fins[fa].Loop].Face);var b=oldMovedFaces.IndexOf(Loops[Fins[fb].Loop].Face);var found=-1;
            for(var j=0;j<edgeCount;j++)
            {
                var next=Edges[newEdges[j]];var na=next.FirstFinEdge;var nb=Fins[na].Other;
                var c=retainedFaces.IndexOf(Loops[Fins[na].Loop].Face);var d=retainedFaces.IndexOf(Loops[Fins[nb].Loop].Face);
                if(a==c&&b==d||a==d&&b==c){if(found>=0)return ParasolidConstants.PK_ERROR_not_implemented;found=j;}
            }
            if(found<0)return ParasolidConstants.PK_ERROR_not_implemented;mappedEdges[i]=newEdges[found];
        }
        return PreserveProfileBaseHandles(source,target,mappedEdges,mappedVertices);
    }

    private static int RetainLocalSurvivingBoundary(BodySlot source,BodySlot target,ReadOnlySpan<int> movedFaces,ReadOnlySpan<int> retainedFaces)
    {
        Span<int> oldEdges=stackalloc int[Bodies[source].EdgeCountBody];Span<int> oldVertices=stackalloc int[Bodies[source].VertexCountBody];
        Span<int> newEdges=stackalloc int[Bodies[target].EdgeCountBody];Span<int> newVertices=stackalloc int[Bodies[target].VertexCountBody];
        CollectProfileSlots(source,oldEdges,oldVertices);CollectProfileSlots(target,newEdges,newVertices);
        Span<int> edgeFrom=stackalloc int[oldEdges.Length];Span<int> edgeTo=stackalloc int[oldEdges.Length];var edgeCount=0;
        Span<int> vertexFrom=stackalloc int[oldVertices.Length];Span<int> vertexTo=stackalloc int[oldVertices.Length];var vertexCount=0;
        for(var i=0;i<oldEdges.Length;i++)
        {
            var slot=oldEdges[i];if(Edges[slot].PendingBlendKind!=0||!LocalEdgeFaces(slot,out var a,out var b))continue;
            a=movedFaces.IndexOf(a);b=movedFaces.IndexOf(b);if(a<0||b<0)continue;var found=-1;
            for(var j=0;j<newEdges.Length;j++)
            {
                if(!LocalEdgeFaces(newEdges[j],out var c,out var d))continue;
                c=retainedFaces.IndexOf(c);d=retainedFaces.IndexOf(d);
                if(a==c&&b==d||a==d&&b==c){if(found>=0){found=-2;break;}found=j;}
            }
            if(found>=0){edgeFrom[edgeCount]=slot;edgeTo[edgeCount++]=newEdges[found];}
        }
        for(var i=0;i<oldVertices.Length;i++)
        {
            var mask=LocalVertexFaceMask(oldVertices[i],movedFaces);if(mask==0)continue;var found=-1;
            for(var j=0;j<newVertices.Length;j++)if(LocalVertexFaceMask(newVertices[j],retainedFaces)==mask)
            {if(found>=0){found=-2;break;}found=j;}
            if(found>=0){vertexFrom[vertexCount]=oldVertices[i];vertexTo[vertexCount++]=newVertices[found];}
        }
        PreserveLocalBoundaryHandles(source,target,oldEdges,oldVertices,newEdges,newVertices,
            edgeFrom[..edgeCount],edgeTo[..edgeCount],vertexFrom[..vertexCount],vertexTo[..vertexCount]);
        return 0;
    }

    private static void PreserveLocalBoundaryHandles(BodySlot source,BodySlot target,ReadOnlySpan<EdgeSlot> oldEdges,
        ReadOnlySpan<VertexSlot> oldVertices,ReadOnlySpan<EdgeSlot> newEdges,ReadOnlySpan<VertexSlot> newVertices,
        ReadOnlySpan<EdgeSlot> edgeFrom,ReadOnlySpan<EdgeSlot> edgeTo,ReadOnlySpan<VertexSlot> vertexFrom,ReadOnlySpan<VertexSlot> vertexTo)
    {
        for(var i=0;i<edgeFrom.Length;i++)
        {
            var old=Edges[edgeFrom[i]];var next=Edges[edgeTo[i]];
            Edges[edgeFrom[i]]=next;Edges[edgeFrom[i]].Header=old.Header;
            Edges[edgeTo[i]]=old;Edges[edgeTo[i]].Header=next.Header;
        }
        for(var i=0;i<vertexFrom.Length;i++)
        {
            var old=Vertices[vertexFrom[i]];var next=Vertices[vertexTo[i]];
            Vertices[vertexFrom[i]]=next;Vertices[vertexFrom[i]].Header=old.Header;
            Vertices[vertexTo[i]]=old;Vertices[vertexTo[i]].Header=next.Header;
        }
        RemapProfileBaseTopology(source,oldEdges,oldVertices,edgeFrom,edgeTo,vertexFrom,vertexTo);
        RemapProfileBaseTopology(target,newEdges,newVertices,edgeTo,edgeFrom,vertexTo,vertexFrom);
        RebuildBoundaryGeometryLinks(source,resetOwners:true);RebuildBoundaryGeometryLinks(target,resetOwners:true);
    }
}
