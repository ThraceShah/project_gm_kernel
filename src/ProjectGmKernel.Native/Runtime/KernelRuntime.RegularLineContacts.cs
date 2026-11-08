using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RegularAxialContact
    {
        internal FaceSlot Face,ToolFace;
        internal BoundaryParameter FirstSource,LastSource;
        internal KernelVector3 Origin,Axis,Start,End;
        internal double First,Last;
        internal bool FirstUseForward;
    }

    private static bool TryRegularAxialContact(in RegularBoundaryView input,ReadOnlySpan<FaceSlot> sources,
        ReadOnlySpan<BufferOffset> owners,BufferOffset first,BufferOffset second,out RegularAxialContact contact)
    {
        contact=default;
        if(!TryAxialCylinderPlaneContact(sources[first],sources[second],out var cylinderFace,out var planeFace,
            out var cylinder,out var plane,out var anchor))return false;
        var curved=cylinderFace==sources[first]?first:second;var planar=curved==first?second:first;
        if(owners[curved]!=0||owners[planar]==0||input.Faces[planar].LoopCount!=1||!ExactPoint(anchor,out var origin)
            ||!TryWholeAxialContactDomain(cylinderFace,cylinder,out var lower,out var upper))return false;
        var axis=new IntervalVector(cylinder.Axis);
        if((IntervalVector.Dot(axis,axis).Value-1).Magnitude!=0)return false;
        var outward=anchor-new IntervalVector(cylinder.Origin);
        var normal=IntervalVector.Dot(outward,new IntervalVector(plane.Axis)).Value;
        var sense=LocalSenseSign(input.Faces[curved].Sense)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(input.Faces[curved].Surface)].Sense)
            *LocalSenseSign(input.Faces[planar].Sense)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(input.Faces[planar].Surface)].Sense);
        if((normal*sense).Upper>=0)return false;
        // The two certified transverse stations partition the whole straight
        // line on this planar occurrence. The cylinder rings independently
        // certify that the complete contact lies away from its axial ends.
        var line=new ParametricSpineCurve{Kind=CurveClass.Line,Origin=origin,First=cylinder.Axis};
        var loop=input.Loops[input.Faces[planar].FirstLoop];
        Span<OperationIntersectionPoint> hit=stackalloc OperationIntersectionPoint[2],ends=stackalloc OperationIntersectionPoint[2];
        Span<BoundaryParameter> stations=stackalloc BoundaryParameter[2];
        var count=0;
        for(var k=0;k<loop.CoedgeCount;k++)
        {
            var occurrence=input.Coedges[loop.FirstCoedge+k].Edge;var use=input.Edges[occurrence];var edge=Curves[GetCurveSlotByTag(use.Curve)];
            if(edge.Class!=CurveClass.Line||!TryReadExplicitSpineCurve(edge,out var boundary)
                ||!TryExactFiniteLineRoots(line,lower,upper,boundary,use.Start,use.End,hit,out var n))return false;
            if(n==0)continue;
            if(n!=1||count==2||hit[0].Kind!=IntersectionVectorKind.Simple
                ||hit[0].First<=lower||hit[0].First>=upper||hit[0].Second<=use.Start||hit[0].Second>=use.End)return false;
            var t=hit[0].Second;
            stations[count]=new(){Carrier=use.Curve,Generation=edge.Header.Generation,Surface=input.Faces[planar].Surface,
                LineOccurrence=occurrence+1,Parameter=t,Lower=t,Upper=t};
            ends[count++]=hit[0];
        }
        if(count!=2)return false;
        var a=ends[0];var b=ends[1];var firstUseForward=a.First>b.First;
        if(firstUseForward){(a,b)=(b,a);(stations[0],stations[1])=(stations[1],stations[0]);}
        var middle=a.First+(b.First-a.First)*.5;
        if(!(middle>a.First&&middle<b.First))return false;
        var witness=Add(origin,Scale(cylinder.Axis,middle));var surface=Surfaces[GetSurfaceSlotByTag(input.Faces[planar].Surface)];
        if(IntersectionSurfaceParameters(surface,witness,out var uv)!=AlgorithmStatus.Success
            ||RegularFaceContains(input,planar,uv,out var inside)!=0||!inside)return false;
        // The finite cylinder domain starts and ends outside the planar trim.
        if(!Outside(input,planar,surface,origin,cylinder.Axis,lower)||!Outside(input,planar,surface,origin,cylinder.Axis,upper))return false;
        contact=new(){Face=cylinderFace,ToolFace=planeFace,Origin=origin,Axis=cylinder.Axis,First=a.First,Last=b.First,
            Start=a.Point,End=b.Point,FirstUseForward=firstUseForward,FirstSource=stations[0],LastSource=stations[1]};
        return true;

        static bool Outside(in RegularBoundaryView input,BufferOffset planar,in SurfaceRecord surface,
            in KernelVector3 origin,in KernelVector3 axis,double station)
        {
            var p=Add(origin,Scale(axis,station));
            return IntersectionSurfaceParameters(surface,p,out var uv)==AlgorithmStatus.Success
                &&RegularFaceContains(input,planar,uv,out var covered)==0&&!covered;
        }
    }

    private static bool TryWholeAxialContactDomain(FaceSlot face,in AnalyticSurface cylinder,out double lower,out double upper)
    {
        lower=upper=0;var record=Faces[face];if(record.LoopCount!=2)return false;
        var axis=new IntervalVector(cylinder.Axis);var axis2=IntervalVector.Dot(axis,axis).Value;
        var loop=record.FirstLoop;
        for(var i=0;i<2;i++,loop=Loops[loop].NextInFace)
        {
            if(Loops[loop].FinCount!=1)return false;var index=Fins[Loops[loop].FirstFin].Edge;if(index<0)return false;
            var edge=Edges[index];if(edge.StartVertex>=0||edge.EndVertex>=0||!TryProfileEdgeRange(edge,out var a,out var b)||b-a!=Math.Tau)return false;
            var curve=Curves[GetCurveSlotByTag(edge.CurveTag)];if(curve.Class!=CurveClass.Circle)return false;
            var ring=CircleDataPool[curve.DataIndex];var normal=Vector(ring.AxisX,ring.AxisY,ring.AxisZ);
            if(ring.Radius!=cylinder.Radius||!ExactVector(normal,cylinder.Axis)&&!ExactVector(normal,Scale(cylinder.Axis,-1)))return false;
            var reference=new IntervalVector(Vector(ring.RefDirX,ring.RefDirY,ring.RefDirZ));
            if((IntervalVector.Dot(reference,reference).Value-1).Magnitude!=0||IntervalVector.Dot(axis,reference).Value.Magnitude!=0)return false;
            var delta=new IntervalVector(Vector(ring.CenterX,ring.CenterY,ring.CenterZ))-new IntervalVector(cylinder.Origin);
            var radial=IntervalVector.Cross(delta,axis);
            if(radial.X.Value.Magnitude!=0||radial.Y.Value.Magnitude!=0||radial.Z.Value.Magnitude!=0)return false;
            var station=IntervalVector.Dot(delta,axis).Value/axis2;
            if(!station.Finite||station.Lower!=station.Upper)return false;
            if(i==0)lower=station.Lower;else upper=station.Lower;
        }
        if(lower>upper)(lower,upper)=(upper,lower);return upper>lower;
    }

    private static bool ExactPoint(in IntervalVector box,out KernelVector3 point)
    {
        point=Vector(box.X.Value.Lower,box.Y.Value.Lower,box.Z.Value.Lower);
        return box.Finite&&box.X.Value.Upper==point.X&&box.Y.Value.Upper==point.Y&&box.Z.Value.Upper==point.Z;
    }

    private static int AppendRegularAxialContact(in RegularAxialContact contact,ReadOnlySpan<EdgeSlot> sources)
    {
        if(!LiveLineContactSource(contact.FirstSource,sources)||!LiveLineContactSource(contact.LastSource,sources))
            return ParasolidConstants.PK_ERROR_cant_do_intersect;
        var face=contact.Face;var body=TopologyBody(PoolKind.Face,face);
        if(!SnapshotProfileTopology(body)||!State.Session->TrySnapshot(ref Loops[Faces[face].FirstLoop])
            ||!State.Session->TrySnapshot(ref Loops[Faces[face].LastLoop]))return ParasolidConstants.PK_ERROR_memory_full;
        var origin=contact.Origin;var axis=contact.Axis;
        var curve=CreateLineCurveTag(origin.X,origin.Y,origin.Z,axis.X,axis.Y,axis.Z);
        var from=contact.FirstUseForward?contact.End:contact.Start;var to=contact.FirstUseForward?contact.Start:contact.End;
        if(curve<=0||!AddVertex(body,from.X,from.Y,from.Z,out var fromSlot)
            ||!AddVertex(body,to.X,to.Y,to.Z,out var toSlot))return ParasolidConstants.PK_ERROR_memory_full;
        // Keep the negative edge sense and increasing native limits.
        // The two-use loop starts at the first tool-trim incidence.
        ref var carrier=ref Curves[GetCurveSlotByTag(curve)];carrier.Sense=ToggleModelingSense(carrier.Sense);
        var edge=AddProfileEdge(body,curve,contact.FirstUseForward?fromSlot:toSlot,contact.FirstUseForward?toSlot:fromSlot,
            contact.First,contact.Last);
        var loop=AddLoop(face);if(edge<0||loop<0)return ParasolidConstants.PK_ERROR_memory_full;
        var positive=AddProfileFin(-1,edge,true);var negative=AddProfileFin(-1,edge,false);
        if(positive<0||negative<0)return ParasolidConstants.PK_ERROR_memory_full;
        var first=contact.FirstUseForward?positive:negative;var last=contact.FirstUseForward?negative:positive;
        AppendFinToLoop(loop,first);AppendFinToLoop(loop,last);
        if(Fins[first].Other!=last||Fins[last].Other!=first||Fins[first].NextInLoop!=last||Fins[last].NextInLoop!=first)
            return ParasolidConstants.PK_ERROR_bad_loop;
        RebuildBoundaryGeometryLinks(body,resetOwners:true);
        if(!PublishBodyTopologyTags(body))return ParasolidConstants.PK_ERROR_memory_full;
        SessionMemoryOwner.DropBodyXt(body);
        if(TraceFiniteBoundary!=null)
        {
            TraceContactEndpoint(contact,contact.FirstSource,sources,edge,curve,contact.Start);
            TraceContactEndpoint(contact,contact.LastSource,sources,edge,curve,contact.End);
        }
        return 0;
    }

    private static bool LiveLineContactSource(in BoundaryParameter station,ReadOnlySpan<EdgeSlot> sources)
    {
        var occurrence=station.SourceOccurrence;
        if((uint)occurrence>=(uint)sources.Length||!Edges.IsAlive(sources[occurrence]))return false;
        var edge=Edges[sources[occurrence]];var slot=GetCurveSlotByTag(station.Carrier);
        return edge.CurveTag==station.Carrier&&slot>=0&&Curves[slot].Header.Generation==station.Generation
            &&TryProfileEdgeRange(edge,out var first,out var last)&&first<station.Lower&&station.Upper<last;
    }

    private static void TraceContactEndpoint(in RegularAxialContact contact,in BoundaryParameter station,ReadOnlySpan<EdgeSlot> sources,
        EdgeSlot edge,CurveTag carrier,in KernelVector3 point)
        =>TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.AxialContactImprint,Source=contact.ToolFace,Product=contact.Face,
            SourceEdge=sources[station.SourceOccurrence],Surface=station.Surface,OtherSurface=Faces[contact.Face].SurfTag,
            Curve=station.Carrier,Generation=station.Generation,ProductCurve=carrier,Edge=edge,StartPoint=point,
            First=station.Lower,Last=station.Upper,StartEvent=-1,EndEvent=-1});

    private static bool RegularHasContactLine(in RegularBoundaryView input)
    {
        foreach(var loop in input.Loops)
            if(loop.CoedgeCount==2&&RegularLineContactUses(input.Edges,input.Coedges.Slice(loop.FirstCoedge,2)))return true;
        return false;
    }

    private static bool RegularLineContactUses(ReadOnlySpan<ModelingEdge> edges,ReadOnlySpan<ModelingCoedge> loop)
        =>loop.Length==2&&loop[0].Edge==loop[1].Edge&&loop[0].Forward!=loop[1].Forward
            &&Curves[GetCurveSlotByTag(edges[loop[0].Edge].Curve)].Class==CurveClass.Line;
}
