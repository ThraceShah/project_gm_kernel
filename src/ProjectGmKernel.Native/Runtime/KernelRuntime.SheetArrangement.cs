using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const BufferCount SheetFaceLimit=512,SheetEdgeLimit=4096,SheetPointLimit=4096,SheetLoopLimit=2048,SheetCoedgeLimit=16384;
    private struct SheetSourceEdge
    {
        internal EdgeSlot Source;
        internal BufferOffset First;
        internal BufferCount Count;
        internal byte InPlane;
    }
    private struct SheetHalfedge
    {
        internal BufferOffset Edge,Next;
        internal byte Forward,Used;
        internal double StartAngle,EndAngle;
    }
    private struct SheetCycle
    {
        internal BufferOffset First,Parent;
        internal BufferCount Count,Depth;
        internal byte Positive;
        internal PK_UV_s Left;
    }
    private ref struct SheetArrangement
    {
        internal Span<KernelVector3> Points;
        internal Span<VertexSlot> OriginalVertices;
        internal Span<ModelingEdge> Edges;
        internal Span<byte> CutEdges;
        internal Span<ModelingCoedge> Coedges;
        internal Span<ModelingLoop> Loops;
        internal Span<ModelingFace> Faces;
        internal Span<FaceSlot> Sources;
        internal Span<sbyte> Sides;
        internal Span<PK_UV_s> Witnesses;
        internal BufferCount NP,NE,NC,NL,NF;
        internal double Tolerance;

        internal int AddPoint(in KernelVector3 point,VertexSlot original,out BufferOffset index)
        {
            index=-1;
            for(var i=0;i<NP;i++)
            {
                if(original>=0&&OriginalVertices[i]==original){index=i;return 0;}
                if(original<0&&ProfileDistance(Points[i],point)<=Tolerance)
                { if(index>=0)return ParasolidConstants.PK_ERROR_not_implemented;index=i; }
            }
            if(index>=0)return 0;
            if(NP==Points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            index=NP;OriginalVertices[NP]=original;Points[NP++]=point;return 0;
        }
        internal int AddEdge(CurveTag curve,double first,double last,BufferOffset start,BufferOffset end,bool cut,out BufferOffset index)
        {
            index=-1;
            for(var i=0;i<NE;i++)if(Edges[i].Curve==curve&&Math.Abs(Edges[i].Start-first)<1e-12&&Math.Abs(Edges[i].End-last)<1e-12
                &&Edges[i].StartVertex==start&&Edges[i].EndVertex==end){index=i;return 0;}
            if(NE==Edges.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            index=NE;CutEdges[NE]=cut?(byte)1:(byte)0;
            Edges[NE++]=new(){Curve=curve,Start=first,End=last,StartVertex=start,EndVertex=end};return 0;
        }
    }

    private static int ReadSheetBoundaryEdges(BodySlot body,SurfTag plane,ref SheetArrangement model,Span<SheetSourceEdge> sources,out BufferCount count)
    {
        count=Bodies[body].EdgeCountBody;if(count>sources.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
        Span<double> parameters=stackalloc double[IntersectionPointCapacity+2];Span<BufferOffset> vertices=stackalloc BufferOffset[IntersectionPointCapacity+2];
        Span<KernelVector3> evaluated=stackalloc KernelVector3[2];
        var source=Bodies[body].FirstEdgeBody;
        for(var e=0;e<count;e++,source=Edges[source].NextInBody)
        {
            ref readonly var edge=ref Edges[source];var slot=GetCurveSlotByTag(edge.CurveTag);
            if(slot<0||!TryProfileEdgeRange(edge,out var first,out var last))return ParasolidConstants.PK_ERROR_not_implemented;
            var status=IntersectSheetCurvePlane(edge.CurveTag,first,last,plane,roots,out var nr);if(status!=AlgorithmStatus.Success)return IntersectionError(status);
            var coincident=nr==2&&roots[0].Kind==IntersectionVectorKind.Start&&roots[1].Kind==IntersectionVectorKind.End;
            var ring=edge.StartVertex<0&&edge.EndVertex<0;var n=0;
            var rawStart=LocalSenseSign(Curves[slot].Sense)>0?edge.StartVertex:edge.EndVertex;
            var rawEnd=LocalSenseSign(Curves[slot].Sense)>0?edge.EndVertex:edge.StartVertex;
            if(!ring)
            {
                parameters[n]=first;var error=SheetOriginalVertex(rawStart,out var point);if(error!=0)return error;
                error=model.AddPoint(point,rawStart,out vertices[n++]);if(error!=0)return error;
            }
            if(!coincident)for(var i=0;i<nr;i++)
            {
                if(roots[i].Kind!=IntersectionVectorKind.Simple)return ParasolidConstants.PK_ERROR_not_implemented;
                var t=roots[i].First;var epsilon=1e-10*Math.Max(1,last-first);
                if(!ring&&(Math.Abs(t-first)<epsilon||Math.Abs(t-last)<epsilon))continue;
                if(ring&&i>0&&ProfileDistance(roots[i].Point,roots[0].Point)<=model.Tolerance)continue;
                parameters[n]=t;var error=model.AddPoint(roots[i].Point,-1,out vertices[n++]);if(error!=0)return error;
            }
            if(!ring)
            {
                parameters[n]=last;var error=SheetOriginalVertex(rawEnd,out var point);if(error!=0)return error;
                error=model.AddPoint(point,rawEnd,out vertices[n++]);if(error!=0)return error;
            }
            var start=model.NE;
            if(ring&&n==0)
            {
                var error=TryCloneEntityCore(edge.CurveTag,out var clone);if(error!=0)return error;
                error=model.AddEdge(clone,first,last,-1,-1,coincident,out _);if(error!=0)return error;
            }
            else for(var i=0;i<(ring?n:n-1);i++)
            {
                var j=(i+1)%n;var wrap=ring&&j==0;
                var error=CloneSheetCurveArc(edge.CurveTag,parameters[i],parameters[j],wrap,out var curve,out var lo,out var hi);if(error!=0)return error;
                var forward=LocalSenseSign(Curves[GetCurveSlotByTag(curve)].Sense)>0;
                error=model.AddEdge(curve,lo,hi,forward?vertices[i]:vertices[j],forward?vertices[j]:vertices[i],coincident,out _);if(error!=0)return error;
            }
            sources[e]=new(){Source=source,First=start,Count=model.NE-start,InPlane=coincident?(byte)1:(byte)0};
        }
        return 0;
    }

    private static int SheetOriginalVertex(VertexSlot vertex,out KernelVector3 point)
    {
        point=default;if(vertex<0)return ParasolidConstants.PK_ERROR_not_implemented;
        var slot=GetPointSlotByTag(Vertices[vertex].PointTag);if(slot<0)return ParasolidConstants.PK_ERROR_missing_geom;
        point=Points[slot].Position;return 0;
    }

    private static int AddSheetHalfedge(SurfTag surface,ref SheetArrangement model,BufferOffset edge,byte forward,
        Span<SheetHalfedge> halfedges,ref BufferCount count)
    {
        if(count==halfedges.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var record=ref model.Edges[edge];
        var error=SheetCoedgePoint(surface,record,forward,0,out _,out var start,out _);if(error!=0)return error;
        error=SheetCoedgePoint(surface,record,forward,1,out _,out var end,out _);if(error!=0)return error;
        halfedges[count++]=new(){Edge=edge,Forward=forward,Next=-1,StartAngle=Math.Atan2(start.param[1],start.param[0]),EndAngle=Math.Atan2(end.param[1],end.param[0])};return 0;
    }

    private static int ArrangeSheetFace(FaceSlot face,SurfTag plane,ReadOnlySpan<SheetSourceEdge> sources,ReadOnlySpan<SheetCutCurve> cuts,
        ref SheetArrangement model,Span<SheetHalfedge> halfedges,Span<ModelingCoedge> cycleCoedges,Span<SheetCycle> cycles,bool borrowGeometry=false)
    {
        var surface=Faces[face].SurfTag;var support=Surfaces[GetSurfaceSlotByTag(surface)];
        var reverse=LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(support.Sense)<0;
        var nh=0;var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
            {
                var index=-1;for(var i=0;i<sources.Length;i++)if(sources[i].Source==Fins[fin].Edge){index=i;break;}
                if(index<0)return ParasolidConstants.PK_ERROR_bad_loop;
                ref readonly var source=ref sources[index];var forward=(Fins[fin].Sense=='+')!=reverse;
                for(var i=0;i<source.Count;i++)
                {
                    var error=AddSheetHalfedge(surface,ref model,source.First+i,forward?(byte)1:(byte)0,halfedges,ref nh);if(error!=0)return error;
                }
            }
        }
        Span<double> parameters=stackalloc double[SheetPointLimit];Span<BufferOffset> endpoints=stackalloc BufferOffset[SheetPointLimit];
        for(var c=0;c<cuts.Length;c++)
        {
            ref readonly var cut=ref cuts[c];var n=0;
            for(var i=0;i<model.NP;i++)
            {
                var vector=new PK_VECTOR_s();WriteVector(&vector,model.Points[i]);double t=0;
                var error=CurveParameteriseVectorCore(cut.Curve,vector,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                LiftPeriodicEdgeParameter(cut.Curve,cut.First,cut.Last,ref t);
                if(!OperationIntersection.InInterval(t,cut.First,cut.Last))continue;
                var duplicate=false;for(var j=0;j<n;j++)if(Math.Abs(parameters[j]-t)<1e-10)duplicate=true;if(duplicate)continue;
                if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;parameters[n]=t;endpoints[n++]=i;
            }
            for(var i=1;i<n;i++)for(var j=i;j>0&&parameters[j]<parameters[j-1];j--)
            { (parameters[j],parameters[j-1])=(parameters[j-1],parameters[j]);(endpoints[j],endpoints[j-1])=(endpoints[j-1],endpoints[j]); }
            if(cut.Closed==0)
            {
                if(n==0||Math.Abs(parameters[0]-cut.First)>1e-10)
                { parameters[..n].CopyTo(parameters[1..]);endpoints[..n].CopyTo(endpoints[1..]);parameters[0]=cut.First;endpoints[0]=-1;n++; }
                if(Math.Abs(parameters[n-1]-cut.Last)>1e-10){parameters[n]=cut.Last;endpoints[n++]=-1;}
            }
            var intervals=cut.Closed!=0?(n==0?1:n):n-1;
            for(var i=0;i<intervals;i++)
            {
                var closed=cut.Closed!=0&&n==0;var j=n==0?0:(i+1)%n;var wrap=cut.Closed!=0&&n>0&&j==0;
                var first=closed?cut.First:parameters[i];var last=closed?cut.Last:parameters[j];
                var span=wrap?cut.Last-first+last-cut.First:last-first;if(!(span>1e-12))continue;
                var inside=false;var located=false;
                for(var attempt=0;attempt<7;attempt++)
                {
                    var fraction=.3819660112501051+attempt*.037;var t=first+span*fraction;if(t>cut.Last)t=cut.First+t-cut.Last;
                    var error=SheetCurveUV(surface,cut.Curve,t,true,out var uv,out _,out var point);if(error!=0)return error;
                    error=SheetPointOnCoincidentBoundary(face,sources,point,out var coincident);if(error!=0)return error;
                    if(coincident){inside=false;located=true;break;}
                    error=SheetSourceContains(face,uv,out inside);
                    if(error==ParasolidConstants.PK_ERROR_eval_failure)continue;if(error!=0)return error;located=true;break;
                }
                if(!located)return ParasolidConstants.PK_ERROR_eval_failure;if(!inside)continue;
                BufferOffset a=-1,b=-1;
                if(!closed)
                {
                    a=endpoints[i];b=endpoints[j];
                    if(a<0||b<0)
                    {
                        var error=SheetCurveUV(surface,cut.Curve,first,true,out _,out _,out var pa);if(error!=0)return error;
                        error=SheetCurveUV(surface,cut.Curve,last,true,out _,out _,out var pb);if(error!=0)return error;
                        if(a<0){error=model.AddPoint(pa,-1,out a);if(error!=0)return error;}
                        if(b<0){error=model.AddPoint(pb,-1,out b);if(error!=0)return error;}
                    }
                }
                CurveTag curve=cut.Curve;var lower=first;var upper=last;
                if(wrap){var error=CloneSheetCurveArc(cut.Curve,first,last,true,out curve,out lower,out upper);if(error!=0)return error;}
                var positive=LocalSenseSign(Curves[GetCurveSlotByTag(curve)].Sense)>0;
                var add=model.AddEdge(curve,lower,upper,positive?a:b,positive?b:a,true,out var edge);if(add!=0)return add;
                add=AddSheetHalfedge(surface,ref model,edge,1,halfedges,ref nh);if(add!=0)return add;
                add=AddSheetHalfedge(surface,ref model,edge,0,halfedges,ref nh);if(add!=0)return add;
            }
        }
        // Clockwise successor from the incoming reverse tangent walks the face
        // on the left, including intersections at existing vertices.
        for(var i=0;i<nh;i++)
        {
            ref var h=ref halfedges[i];ref readonly var edge=ref model.Edges[h.Edge];
            var end=h.Forward!=0?edge.EndVertex:edge.StartVertex;
            if(end<0){h.Next=i;continue;}
            var best=double.PositiveInfinity;var next=-1;
            for(var j=0;j<nh;j++)
            {
                ref readonly var candidate=ref halfedges[j];ref readonly var ce=ref model.Edges[candidate.Edge];
                var start=candidate.Forward!=0?ce.StartVertex:ce.EndVertex;if(start!=end)continue;
                var angle=h.EndAngle+Math.PI-candidate.StartAngle;angle-=Math.Tau*Math.Floor(angle/Math.Tau);if(angle<1e-12)angle=Math.Tau;
                if(angle<best){best=angle;next=j;}
            }
            if(next<0)return ParasolidConstants.PK_ERROR_bad_loop;h.Next=next;
        }
        var cycleCount=0;var coedgeCount=0;
        for(var seed=0;seed<nh;seed++)
        {
            if(halfedges[seed].Used!=0)continue;if(cycleCount==cycles.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            var first=coedgeCount;var current=seed;
            do
            {
                ref var h=ref halfedges[current];if(h.Used!=0||coedgeCount==cycleCoedges.Length)return ParasolidConstants.PK_ERROR_bad_loop;
                h.Used=1;cycleCoedges[coedgeCount++]=new(){Edge=h.Edge,Forward=h.Forward};current=h.Next;
            }while(current!=seed);
            var cycle=new SheetCycle{First=first,Count=coedgeCount-first,Parent=-1};
            var error=SheetCycleWitness(surface,model.Edges[..model.NE],cycleCoedges.Slice(first,cycle.Count),out cycle.Left,out cycle.Positive);if(error!=0)return error;
            cycles[cycleCount++]=cycle;
        }
        return PublishSheetCycles(face,plane,reverse,ref model,cycleCoedges[..coedgeCount],cycles[..cycleCount],borrowGeometry);
    }
}
