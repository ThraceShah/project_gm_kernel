using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RegularPointContact{internal FaceSlot Face;internal KernelVector3 Point;}

    private static bool RegularVertexLoop(LoopSlot loop,out VertexSlot vertex)
    {
        vertex=-1;if(Loops[loop].FinCount!=1)return false;var slot=Loops[loop].FirstFin;if(slot<0)return false;var fin=Fins[slot];
        if(fin.Edge>=0||fin.Curve>0||fin.Vertex<0||fin.NextInLoop!=slot||fin.PrevInLoop!=slot||fin.Other>=0)return false;
        vertex=fin.Vertex;return true;
    }

    private static int AppendRegularPointLoop(FaceSlot face,in KernelVector3 point,double precision=0)
    {
        var loop=Faces[face].FirstLoop;
        for(var i=0;i<Faces[face].LoopCount;i++,loop=Loops[loop].NextInFace)
        {
            if(!RegularVertexLoop(loop,out var existing))continue;var error=SheetOriginalVertex(existing,out var position);if(error!=0)return error;
            if(ProfileDistance(position,point)<=1e-8)
            {
                if(precision>Vertices[existing].Tolerance){if(!State.Session->TrySnapshot(ref Vertices[existing]))return ParasolidConstants.PK_ERROR_memory_full;Vertices[existing].Tolerance=precision;}
                return 0;
            }
        }
        var body=TopologyBody(PoolKind.Face,face);
        if(!SnapshotProfileTopology(body)||!State.Session->TrySnapshot(ref Loops[Faces[face].FirstLoop])||!State.Session->TrySnapshot(ref Loops[Faces[face].LastLoop]))return ParasolidConstants.PK_ERROR_memory_full;
        if(!AddVertex(body,point.X,point.Y,point.Z,out var vertex))return ParasolidConstants.PK_ERROR_memory_full;
        Vertices[vertex].Tolerance=precision;loop=AddLoop(face);
        if(loop<0||AddDegenerateFinToLoopAndVertex(loop,face,vertex)<0)return ParasolidConstants.PK_ERROR_memory_full;
        RebuildBoundaryGeometryLinks(body,resetOwners:true);
        if(!PublishBodyTopologyTags(body))return ParasolidConstants.PK_ERROR_memory_full;
        SessionMemoryOwner.DropBodyXt(body);return 0;
    }

    private static int ReadRegularOriginalVertex(VertexSlot vertex,Span<KernelVector3> points,Span<VertexSlot> vertices,
        Span<double> precisions,ref BufferCount count,out BufferOffset index)
    {
        index=vertices[..count].IndexOf(vertex);if(index>=0)return 0;
        var error=SheetOriginalVertex(vertex,out var p);if(error!=0)return error;
        if(index<0){if(count==points.Length)return ParasolidConstants.PK_ERROR_not_implemented;index=count;vertices[count]=vertex;if(!precisions.IsEmpty)precisions[count]=0;points[count++]=p;}
        if(!precisions.IsEmpty)precisions[index]=Math.Max(precisions[index],Vertices[vertex].Tolerance);return 0;
    }

    private static int TransferRegularPointLoops(scoped ref SheetArrangement model,scoped ReadOnlySpan<BufferOffset> indices,BodyTag body)
    {
        var view=new RegularBoundaryView(model.Edges[..model.NE],model.Coedges[..model.NC],model.Loops[..model.NL],model.Faces[..model.NF]);
        var face=Bodies[TagRec(body).Slot].FirstFaceBody;
        for(var i=0;i<indices.Length;i++,face=Faces[face].NextInBody)
        {
            var index=indices[i];var source=model.Sources[index];
            if(source<0||!Faces.IsAlive(source)||!RegularCoincidentSupports(model.Faces[index].Surface,Faces[source].SurfTag))continue;
            var loop=Faces[source].FirstLoop;
            for(var l=0;l<Faces[source].LoopCount;l++,loop=Loops[loop].NextInFace)
            {
                if(!RegularVertexLoop(loop,out var vertex))continue;var error=SheetOriginalVertex(vertex,out var point);if(error!=0)return error;
                var current=Bodies[TagRec(body).Slot].FirstVertexBody;var already=false;
                for(var v=0;v<Bodies[TagRec(body).Slot].VertexCountBody;v++,current=Vertices[current].NextInBody)
                {error=SheetOriginalVertex(current,out var position);if(error!=0)return error;if(ProfileDistance(position,point)<=model.Tolerance){already=true;break;}}
                if(already)continue;
                var status=IntersectionSurfaceParameters(Surfaces[GetSurfaceSlotByTag(model.Faces[index].Surface)],point,out var uv);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
                error=RegularFaceContains(view,index,uv,out var inside);if(error!=0)return error;if(!inside)continue;
                error=AppendRegularPointLoop(face,point,Vertices[vertex].Tolerance);if(error!=0)return error;
            }
        }
        return 0;
    }
}
