using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int EdgeSplitAtParam(EntityTag edge,double parameter,EntityTag* vertex,EntityTag* addedEdge)
    {
        if(vertex!=null)*vertex=0;if(addedEdge!=null)*addedEdge=0;
        if(Dispatcher.IsExecuting)return SplitEdgeCore(edge,parameter,vertex,addedEdge);
        var command=new EdgeSubdivisionCommand{Edge=edge,Parameter=parameter,Vertex=vertex,AddedEdge=addedEdge};
        var error=Dispatch(ApiId.EdgeSplitAtParam,ConcurrencyKind.Local,AccessKind.GlobalWrite,ref command,edge);
        if(error!=0){if(vertex!=null)*vertex=0;if(addedEdge!=null)*addedEdge=0;}return error;
    }

    private struct EdgeSubdivisionCommand:IKernelCommand
    {
        internal EntityTag Edge;
        internal double Parameter;
        internal EntityTag* Vertex;
        internal EntityTag* AddedEdge;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try{return SplitEdgeCore(Edge,Parameter,Vertex,AddedEdge);}
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int SplitEdgeCore(EntityTag tag,double parameter,EntityTag* vertex,EntityTag* addedEdge)
    {
        if(!IsSessionStarted)return ParasolidConstants.PK_ERROR_not_in_PK;
        if(vertex==null||addedEdge==null||!double.IsFinite(parameter))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(!State.Session->Tags.TryResolve(tag,CurrentSessionId,out var record)||record.Pool!=(byte)PoolKind.Edge)return ParasolidConstants.PK_ERROR_bad_class;
        var slot=record.Slot;var original=Edges[slot];var body=original.Body;
        if(body<0||Xt.IsOpaque(body)||original.FinCount!=2||original.StartVertex<0||original.EndVertex<0||original.StartVertex==original.EndVertex
            ||original.PendingBlendKind!=0||original.Tolerance!=0)return ParasolidConstants.PK_ERROR_not_implemented;
        if(original.CurveTag<=0)return ParasolidConstants.PK_ERROR_missing_geom;
        if(!TryProfileEdgeRange(original,out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
        var epsilon=1e-10*Math.Max(1,last-first);
        if(parameter<=first+epsilon||parameter>=last-epsilon)return ParasolidConstants.PK_ERROR_bad_parameter;
        Span<FinSlot> uses=stackalloc FinSlot[2]{original.FirstFinEdge,original.LastFinEdge};
        foreach(var fin in uses)if(fin<0||Fins[fin].Loop<0||Fins[fin].Curve>=0)return ParasolidConstants.PK_ERROR_not_implemented;
        var curve=Curves[GetCurveSlotByTag(original.CurveTag)];Span<KernelVector3> point=stackalloc KernelVector3[1];
        var status=EvaluateCurveCore(in curve,parameter,0,false,point,out _);if(status!=AlgorithmStatus.Success)return EvaluationError(status);
        var error=SheetOriginalVertex(original.StartVertex,out var start);if(error!=0)return error;
        error=SheetOriginalVertex(original.EndVertex,out var end);if(error!=0)return error;
        if(ProfileDistance(start,point[0])<=1e-8||ProfileDistance(end,point[0])<=1e-8)return ParasolidConstants.PK_ERROR_bad_parameter;
        if(!SnapshotProfileTopology(body))return ParasolidConstants.PK_ERROR_memory_full;
        foreach(var fin in uses)if(!State.Session->TrySnapshot(ref Loops[Fins[fin].Loop]))return ParasolidConstants.PK_ERROR_memory_full;
        if(!AddVertex(body,point[0].X,point[0].Y,point[0].Z,out var middle))return ParasolidConstants.PK_ERROR_memory_full;
        Vertices[middle].Header.Tag=AllocateTag(EntityClass.Vertex,PoolKind.Vertex,middle,Vertices[middle].Header.Generation);
        if(Vertices[middle].Header.Tag<=0)return ParasolidConstants.PK_ERROR_memory_full;
        var positive=LocalSenseSign(curve.Sense)>0;
        var tail=AddProfileEdge(body,original.CurveTag,positive?middle:original.StartVertex,positive?original.EndVertex:middle,parameter,last);
        if(tail<0)return ParasolidConstants.PK_ERROR_memory_full;
        Edges[tail].Header.Tag=AllocateTag(EntityClass.Edge,PoolKind.Edge,tail,Edges[tail].Header.Generation);
        if(Edges[tail].Header.Tag<=0)return ParasolidConstants.PK_ERROR_memory_full;
        Edges[slot].StartVertex=positive?original.StartVertex:middle;Edges[slot].EndVertex=positive?middle:original.EndVertex;
        Edges[slot].ParameterStart=first;Edges[slot].ParameterEnd=parameter;Edges[slot].HasParameterRange=1;
        foreach(var fin in uses)
        {
            var forward=Fins[fin].Sense=='+';var loop=Fins[fin].Loop;
            UnlinkSubdivisionFinVertex(fin);AppendFinToVertex(forward?Edges[slot].EndVertex:Edges[slot].StartVertex,fin);
            var added=AddProfileFin(-1,tail,forward);if(added<0)return ParasolidConstants.PK_ERROR_memory_full;
            Fins[added].Header.Tag=AllocateTag(EntityClass.Fin,PoolKind.Fin,added,Fins[added].Header.Generation);
            if(Fins[added].Header.Tag<=0)return ParasolidConstants.PK_ERROR_memory_full;
            var before=forward==positive?fin:Fins[fin].PrevInLoop;var after=Fins[before].NextInLoop;
            Fins[added].Loop=loop;Fins[added].PrevInLoop=before;Fins[added].NextInLoop=after;
            Fins[before].NextInLoop=added;Fins[after].PrevInLoop=added;Loops[loop].FinCount++;
            if(forward==positive&&Loops[loop].LastFin==fin)Loops[loop].LastFin=added;
            if(forward!=positive&&Loops[loop].FirstFin==fin)Loops[loop].FirstFin=added;
        }
        RebuildBoundaryGeometryLinks(body,resetOwners:true);SessionMemoryOwner.DropBodyXt(body);
        *vertex=Vertices[middle].Header.Tag;*addedEdge=Edges[tail].Header.Tag;return 0;
    }

    private static void UnlinkSubdivisionFinVertex(FinSlot fin)
    {
        var vertex=Fins[fin].Vertex;if(vertex<0)return;
        var previous=Fins[fin].PrevAtVertex;var next=Fins[fin].NextAtVertex;
        if(previous==fin)Vertices[vertex].FirstFinVertex=Vertices[vertex].LastFinVertex=-1;
        else
        {
            Fins[previous].NextAtVertex=next;Fins[next].PrevAtVertex=previous;
            if(Vertices[vertex].FirstFinVertex==fin)Vertices[vertex].FirstFinVertex=next;
            if(Vertices[vertex].LastFinVertex==fin)Vertices[vertex].LastFinVertex=previous;
        }
        Fins[fin].Vertex=-1;Fins[fin].NextAtVertex=Fins[fin].PrevAtVertex=-1;
    }
}
