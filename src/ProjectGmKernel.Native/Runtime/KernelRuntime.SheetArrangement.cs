using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
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
        internal BufferOffset InputEdge;
        internal BufferOffset First;
        internal BufferCount Count;
        internal byte InPlane;
    }
    private struct SheetHalfedge
    {
        internal BufferOffset Edge,Next;
        internal BoundaryEventId StartEvent,EndEvent;
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
        internal Span<double> VertexPrecisions;
        internal Span<byte> VertexMergeCandidates;
        internal Span<ModelingEdge> Edges;
        internal Span<byte> CutEdges;
        internal Span<ModelingCoedge> Coedges;
        internal Span<ModelingLoop> Loops;
        internal Span<ModelingFace> Faces;
        internal Span<FaceSlot> Sources;
        internal Span<sbyte> Sides;
        internal Span<PK_UV_s> Witnesses;
        internal Span<BoundaryParameter> Parameters;
        internal Span<BoundaryCarrierSurface> CarrierSurfaces;
        internal BufferCount CarrierSurfaceCount;
        internal bool RequireBoundaryAssociations;
        internal Span<BoundaryTangency> Tangencies;
        internal BufferCount TangencyCount;
        internal Span<BoundarySurfaceContact> SurfaceContacts;
        internal BufferCount SurfaceContactCount;
        internal BufferCount ParameterCount;
        internal BufferCount NP,NE,NC,NL,NF;
        internal double Tolerance;

        internal int AssociateParameter(BoundaryEventId endpoint,CurveTag curve,double parameter,double lower=double.NaN,double upper=double.NaN,
            SurfTag equation=0,BoundaryRootWitnessId witness=0)
        {
            if(Parameters.IsEmpty)return 0;
            if(double.IsNaN(lower)){lower=parameter;upper=parameter;}
            var generation=Curves[GetCurveSlotByTag(curve)].Header.Generation;
            var match=-1;var hasCertificate=false;
            for(var i=0;i<ParameterCount;i++)
            {
                ref readonly var known=ref Parameters[i];
                if(known.Event==endpoint&&known.Carrier==curve&&known.Generation==generation)
                {
                    if(upper<known.Lower||lower>known.Upper)
                    {
                        LastBoundaryParameterConflict=new(){Event=endpoint,Carrier=curve,Previous=known.Parameter,PreviousLower=known.Lower,PreviousUpper=known.Upper,Incoming=parameter,Lower=lower,Upper=upper};
                        LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetCurve=curve;LastSheetParameter=parameter;return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    }
                    hasCertificate|=known.Surface!=0;
                    if(known.Surface==equation&&known.Witness==witness||known.Surface==0)match=i;
                }
            }
            if(equation==0&&hasCertificate)return 0;
            if(match>=0)
            {
                ref var known=ref Parameters[match];
                if(known.Surface==equation&&known.Witness==witness)
                {
                    known.Lower=Math.Max(lower,known.Lower);known.Upper=Math.Min(upper,known.Upper);
                    known.Parameter=Math.Clamp(known.Parameter,known.Lower,known.Upper);
                }
                else {known.Surface=equation;known.Witness=witness;known.Lower=lower;known.Upper=upper;known.Parameter=parameter;}
                return 0;
            }
            // A joined event may have been proved by a different common
            // participant. Retain each native root equation so a later
            // pair can join through any one of them.
            if(ParameterCount==Parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            Parameters[ParameterCount++]=new(){Event=endpoint,Carrier=curve,Generation=generation,Surface=equation,Witness=witness,Parameter=parameter,Lower=lower,Upper=upper};return 0;
        }

        internal bool KnownParameter(BoundaryEventId endpoint,CurveTag curve,out double parameter)
        {
            var found=KnownBoundaryParameter(endpoint,curve,out var known);parameter=known.Parameter;return found;
        }

        internal bool KnownBoundaryParameter(BoundaryEventId endpoint,CurveTag curve,out BoundaryParameter parameter)
        {
            parameter=default;var generation=Curves[GetCurveSlotByTag(curve)].Header.Generation;
            for(var i=0;i<ParameterCount;i++)
            {
                var known=Parameters[i];if(known.Event!=endpoint||known.Carrier!=curve||known.Generation!=generation)continue;
                parameter=known;return true;
            }
            return false;
        }

        internal int AssociateTangency(BoundaryEventId endpoint,CurveTag first,CurveTag second)
        {
            if(Tangencies.IsEmpty||first==second)return 0;
            if(KnownTangency(endpoint,first,second))return 0;
            if(TangencyCount==Tangencies.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            Tangencies[TangencyCount++]=new(){Event=endpoint,First=first,Second=second};return 0;
        }

        internal int AssociateSurfaceContact(BoundaryEventId endpoint,SurfTag first,SurfTag second)
        {
            foreach(var contact in SurfaceContacts[..SurfaceContactCount])if(contact.Event==endpoint
                &&(contact.First==first&&contact.Second==second||contact.First==second&&contact.Second==first))return 0;
            if(SurfaceContactCount==SurfaceContacts.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            SurfaceContacts[SurfaceContactCount++]=new(){Event=endpoint,First=first,Second=second};return 0;
        }

        internal bool KnownTangency(BoundaryEventId endpoint,CurveTag first,CurveTag second,SurfTag surface=0)
        {
            foreach(var contact in Tangencies[..TangencyCount])
                if(contact.Event==endpoint&&(contact.First==first&&contact.Second==second||contact.First==second&&contact.Second==first))return true;
            if(surface!=0&&OtherBoundarySupport(surface,first,out var a)&&OtherBoundarySupport(surface,second,out var b))
                foreach(var contact in SurfaceContacts[..SurfaceContactCount])if(contact.Event==endpoint
                    &&(SameSheetSupport(a,contact.First)&&SameSheetSupport(b,contact.Second)||SameSheetSupport(a,contact.Second)&&SameSheetSupport(b,contact.First)))return true;
            return false;
        }

        internal int AddPoint(in KernelVector3 point,VertexSlot original,out BufferOffset index,scoped ReadOnlySpan<BoundaryParameter> anchors=default,bool requireProof=false)
            =>AddPoint(point,original,out index,out _,anchors,requireProof);

        internal int AddPoint(in KernelVector3 point,VertexSlot original,out BufferOffset index,out bool reusedByPosition,
            scoped ReadOnlySpan<BoundaryParameter> anchors=default,bool requireProof=false)
        {
            reusedByPosition=false;
            index=-1;BoundaryEventId proven=-1;var ambiguous=false;var unresolved=false;
            for(var i=0;i<NP;i++)
            {
                if(original>=0&&OriginalVertices[i]==original){index=i;return 0;}
                if(original>=0||!(ProfileDistance(Points[i],point)<=Tolerance)||ConflictingBoundaryAnchor(i,anchors,out var same,out var shared))continue;
                if(same)
                {
                    if(proven>=0)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    proven=i;continue;
                }
                if(requireProof){unresolved|=shared;continue;}
                ambiguous|=index>=0;index=i;
            }
            if(proven>=0){index=proven;return 0;}
            if(ambiguous||unresolved){LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;return ParasolidConstants.PK_ERROR_cant_do_intersect;}
            if(index>=0){reusedByPosition=true;return 0;}
            if(NP==Points.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            index=NP;OriginalVertices[NP]=original;if(!VertexPrecisions.IsEmpty)VertexPrecisions[NP]=original>=0?Vertices[original].Tolerance:0;if(!VertexMergeCandidates.IsEmpty)VertexMergeCandidates[NP]=0;Points[NP++]=point;return 0;
        }
        private bool ConflictingBoundaryAnchor(BoundaryEventId endpoint,scoped ReadOnlySpan<BoundaryParameter> anchors,out bool same,out bool shared)
        {
            same=shared=false;
            // A disjoint pair of certified native intervals on the same
            // carrier identifies distinct events even when their
            // positions lie inside the spatial candidate radius.
            foreach(ref readonly var anchor in anchors)
            foreach(ref readonly var known in Parameters[..ParameterCount])
            {
                if(known.Event!=endpoint||known.Carrier!=anchor.Carrier||known.Generation!=anchor.Generation)continue;
                if(known.Upper<anchor.Lower||known.Lower>anchor.Upper)return true;
                shared=true;
                var exact=anchor.Lower==anchor.Upper&&known.Lower==known.Upper&&known.Lower==anchor.Lower;
                if(anchor.Witness!=0||known.Witness!=0)
                {
                    if(anchor.Witness!=0&&known.Witness!=0)
                        same|=anchor.Surface==known.Surface?anchor.Witness==known.Witness
                            :exact&&(anchor.Witness-1)/IntersectionPointCapacity==(known.Witness-1)/IntersectionPointCapacity;
                    continue;
                }
                // Analytic source/plane equations can prove the same native
                // root even if that parameter has no binary64 representation.
                same|=exact||anchor.Surface!=0&&known.Surface!=0&&(known.Surface==anchor.Surface
                    ||SameCertifiedLinePlaneRoot(anchor.Carrier,known.Surface,anchor.Surface));
            }
            return false;
        }
        internal int AddEdge(CurveTag curve,double first,double last,BufferOffset start,BufferOffset end,bool cut,out BufferOffset index,bool exact=false)
        {
            index=-1;
            for(var i=0;i<NE;i++)if(Edges[i].Curve==curve&&Edges[i].StartVertex==start&&Edges[i].EndVertex==end
                &&(exact?Edges[i].Start==first&&Edges[i].End==last:Math.Abs(Edges[i].Start-first)<1e-12&&Math.Abs(Edges[i].End-last)<1e-12))
            {
                if(index>=0){LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;return ParasolidConstants.PK_ERROR_cant_do_intersect;}
                index=i;
            }
            if(index>=0)return 0;
            return AppendEdge(curve,first,last,start,end,cut,out index);
        }

        internal int AppendEdge(CurveTag curve,double first,double last,BufferOffset start,BufferOffset end,bool cut,out BufferOffset index)
        {
            index=-1;
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
        Span<SheetHalfedge> halfedges,ref BufferCount count,FaceSlot source=-1,EdgeSlot sourceEdge=-1,FinSlot sourceFin=-1,KernelSense faceSense=0)
    {
        var use=FiniteBoundaryUse.Create(surface,model.Edges[edge],edge,forward,source,sourceEdge,sourceFin,faceSense);
        return AddFiniteBoundaryHalfedge(use,ref model,halfedges,ref count);
    }

    private static int AddFiniteBoundaryHalfedge(in FiniteBoundaryUse use,scoped ref SheetArrangement model,
        scoped Span<SheetHalfedge> halfedges,ref BufferCount count)
    {
        if(count==halfedges.Length)return ParasolidConstants.PK_ERROR_not_implemented;
        var surface=use.Surface;var edge=use.Edge;var forward=use.Forward;
        ref readonly var record=ref model.Edges[edge];
        var slot=GetCurveSlotByTag(use.Carrier);var support=GetSurfaceSlotByTag(surface);
        if(slot<0||Curves[slot].Header.Generation!=use.Generation||record.Curve!=use.Carrier
            ||Curves[slot].Class!=use.Class||Curves[slot].Sense!=use.CurveSense||support<0
            ||Surfaces[support].Header.Generation!=use.SurfaceGeneration||Surfaces[support].Sense!=use.SurfaceSense
            ||record.Start!=use.First||record.End!=use.Last||use.Source>=0&&(!Faces.IsAlive(use.Source)
                ||Faces[use.Source].Header.Generation!=use.SourceFaceGeneration))return ParasolidConstants.PK_ERROR_bad_parameter;
        if(use.SourceFin>=0)
        {
            if(use.SourceEdge<0||use.Source<0||!Edges.IsAlive(use.SourceEdge)||!Fins.IsAlive(use.SourceFin)
                ||Edges[use.SourceEdge].Header.Generation!=use.SourceEdgeGeneration||Fins[use.SourceFin].Header.Generation!=use.SourceFinGeneration)
                return ParasolidConstants.PK_ERROR_bad_parameter;
            var fin=Fins[use.SourceFin];var reversed=LocalSenseSign(use.FaceSense)*LocalSenseSign(use.SurfaceSense)<0;
            if(fin.Edge!=use.SourceEdge||fin.Loop<0||Loops[fin.Loop].Face!=use.Source
                ||(fin.Sense=='+')!=((forward!=0)!=reversed))return ParasolidConstants.PK_ERROR_bad_loop;
        }
        TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.Input,Source=use.Source,Surface=use.Surface,Curve=use.Carrier,
            Generation=use.Generation,SurfaceGeneration=use.SurfaceGeneration,SourceEdge=use.SourceEdge,SourceFin=use.SourceFin,
            CurveSense=use.CurveSense,SurfaceSense=use.SurfaceSense,FaceSense=use.FaceSense,Edge=use.Edge,Face=-1,Product=-1,
            StartEvent=use.StartEvent,EndEvent=use.EndEvent,First=use.First,Last=use.Last,Forward=use.Forward,
            StartPoint=use.StartEvent>=0?model.Points[use.StartEvent]:default,EndPoint=use.EndEvent>=0?model.Points[use.EndEvent]:default});
        if(Surfaces[GetSurfaceSlotByTag(surface)].Class==SurfaceClass.Sphere)
        {
            var sphericalError=SphericalHalfedgeAngle(surface,record,forward,false,model.Points,out var startAngle);if(sphericalError!=0)return sphericalError;
            sphericalError=SphericalHalfedgeAngle(surface,record,forward,true,model.Points,out var endAngle);if(sphericalError!=0)return sphericalError;
            halfedges[count++]=new(){Edge=edge,Forward=forward,StartEvent=use.StartEvent,EndEvent=use.EndEvent,Next=-1,StartAngle=startAngle,EndAngle=endAngle};return 0;
        }
        var error=SheetCoedgePoint(surface,record,forward,0,out _,out var start,out _);if(error!=0)return error;
        error=SheetCoedgePoint(surface,record,forward,1,out _,out var end,out _);if(error!=0)return error;
        halfedges[count++]=new(){Edge=edge,Forward=forward,StartEvent=use.StartEvent,EndEvent=use.EndEvent,Next=-1,StartAngle=Math.Atan2(start.param[1],start.param[0]),EndAngle=Math.Atan2(end.param[1],end.param[0])};return 0;
    }

    private static int ArrangeSheetFace(FaceSlot face,SurfTag plane,ReadOnlySpan<SheetSourceEdge> sources,ReadOnlySpan<SheetCutCurve> cuts,
        ref SheetArrangement model,Span<SheetHalfedge> halfedges,Span<ModelingCoedge> cycleCoedges,Span<SheetCycle> cycles,bool borrowGeometry=false,bool checkAllBoundaries=false)
    {
        var surface=Faces[face].SurfTag;var support=Surfaces[GetSurfaceSlotByTag(surface)];
        var reverse=LocalSenseSign(Faces[face].Orientation)*LocalSenseSign(support.Sense)<0;
        var nh=0;var loop=Faces[face].FirstLoop;
        for(var l=0;l<Faces[face].LoopCount;l++,loop=Loops[loop].NextInFace)
        {
            if(RegularVertexLoop(loop,out _))continue;
            var fin=Loops[loop].FirstFin;
            for(var k=0;k<Loops[loop].FinCount;k++,fin=Fins[fin].NextInLoop)
            {
                var index=-1;for(var i=0;i<sources.Length;i++)if(sources[i].Source==Fins[fin].Edge){index=i;break;}
                if(index<0)return ParasolidConstants.PK_ERROR_bad_loop;
                ref readonly var source=ref sources[index];var forward=(Fins[fin].Sense=='+')!=reverse;
                for(var i=0;i<source.Count;i++)
                {
                    var error=AddSheetHalfedge(surface,ref model,source.First+i,forward?(byte)1:(byte)0,halfedges,ref nh,face,source.Source,fin);if(error!=0)return error;
                }
            }
        }
        return ArrangeFaceCutGraph(face,surface,NormalizeLocalSense(Faces[face].Orientation),plane,reverse,sources,cuts,ref model,halfedges,nh,cycleCoedges,cycles,borrowGeometry,checkAllBoundaries,default,-1);
    }

    private static int ArrangeFiniteInputFace(in RegularBoundaryView input,BufferOffset face,FaceSlot source,
        ReadOnlySpan<SheetSourceEdge> sources,ReadOnlySpan<SheetCutCurve> cuts,ref SheetArrangement model,
        Span<SheetHalfedge> halfedges,Span<ModelingCoedge> cycleCoedges,Span<SheetCycle> cycles)
    {
        var record=input.Faces[face];var reverse=LocalSenseSign(record.Sense)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(record.Surface)].Sense)<0;var count=0;
        var originalLoop=source>=0?Faces[source].FirstLoop:-1;
        for(var l=0;l<record.LoopCount;l++)
        {
            var loop=input.Loops[record.FirstLoop+l];var originalFin=-1;
            if(source>=0)
            {
                while(originalLoop>=0&&RegularVertexLoop(originalLoop,out _))originalLoop=Loops[originalLoop].NextInFace;
                if(originalLoop<0||Loops[originalLoop].FinCount!=loop.CoedgeCount)return ParasolidConstants.PK_ERROR_bad_loop;
                originalFin=Loops[originalLoop].FirstFin;originalLoop=Loops[originalLoop].NextInFace;
            }
            foreach(var coedge in input.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
            {
                var found=-1;for(var e=0;e<sources.Length;e++)if(sources[e].InputEdge==coedge.Edge){found=e;break;}
                if(found<0)return ParasolidConstants.PK_ERROR_bad_loop;var fragments=sources[found];
                if(source>=0&&(originalFin<0||Fins[originalFin].Edge!=fragments.Source))return ParasolidConstants.PK_ERROR_bad_loop;
                for(var i=0;i<fragments.Count;i++)
                {
                    var error=AddSheetHalfedge(record.Surface,ref model,fragments.First+i,(byte)(coedge.Forward^(reverse?1:0)),halfedges,ref count,
                        source,fragments.Source,originalFin,record.Sense);if(error!=0)return error;
                }
                if(originalFin>=0)originalFin=Fins[originalFin].NextInLoop;
            }
        }
        return ArrangeFaceCutGraph(source,record.Surface,record.Sense,0,reverse,sources,cuts,ref model,halfedges,count,cycleCoedges,cycles,true,false,input,face);
    }

    private static int ArrangeFaceCutGraph(FaceSlot face,SurfTag surface,KernelSense sense,SurfTag plane,bool reverse,
        ReadOnlySpan<SheetSourceEdge> sources,ReadOnlySpan<SheetCutCurve> cuts,ref SheetArrangement model,Span<SheetHalfedge> halfedges,
        BufferCount nh,Span<ModelingCoedge> cycleCoedges,Span<SheetCycle> cycles,bool borrowGeometry,bool checkAllBoundaries,
        in RegularBoundaryView domain,BufferOffset domainFace)
    {
        Span<double> parameters=stackalloc double[SheetPointLimit];Span<BufferOffset> endpoints=stackalloc BufferOffset[SheetPointLimit];
        Span<OperationIntersectionPoint> coincidenceRoots=stackalloc OperationIntersectionPoint[checkAllBoundaries?IntersectionPointCapacity:0];
        for(var c=0;c<cuts.Length;c++)
        {
            ref readonly var cut=ref cuts[c];var n=0;
            var certified=cut.NativeLocatorEnds!=0;var lowerEvent=cut.LowerEvent-1;var upperEvent=cut.UpperEvent-1;
            if(certified)
            {
                LastSheetPartitionStage=SheetPartitionStage.BoundaryAssociation;LastSheetCurve=cut.Curve;LastSheetParameter=cut.First;
                if(cut.Closed!=0||!HasCutEnd(ref model,cut.Curve,lowerEvent,cut.First)||!HasCutEnd(ref model,cut.Curve,upperEvent,cut.Last))
                    return ParasolidConstants.PK_ERROR_cant_do_intersect;
                parameters[n]=cut.First;endpoints[n++]=lowerEvent;parameters[n]=cut.Last;endpoints[n++]=upperEvent;
                TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.NativeCutEndpoints,Source=face,Surface=surface,Curve=cut.Curve,
                    Face=domainFace,Generation=Curves[GetCurveSlotByTag(cut.Curve)].Header.Generation,
                    SurfaceGeneration=Surfaces[GetSurfaceSlotByTag(surface)].Header.Generation,StartEvent=lowerEvent,EndEvent=upperEvent,First=cut.First,Last=cut.Last});
            }
            for(var i=0;i<model.NP;i++)
            {
                if(certified&&(i==lowerEvent||i==upperEvent))continue;
                var vector=new PK_VECTOR_s();WriteVector(&vector,model.Points[i]);double t=0;
                var known=model.KnownParameter(i,cut.Curve,out t);var error=0;
                if(!known&&model.RequireBoundaryAssociations)
                {error=AssociateCompatibleBoundaryEvent(ref model,i,cut.Curve,cut.First,cut.Last,out known);if(error!=0)return error;if(!known)continue;model.KnownParameter(i,cut.Curve,out t);}
                else if(!known)error=CurveParameteriseVectorCore(cut.Curve,vector,&t);
                if(error==ParasolidConstants.PK_ERROR_not_on_curve)continue;if(error!=0)return error;
                LiftPeriodicEdgeParameter(cut.Curve,cut.First,cut.Last,ref t);
                if(!OperationIntersection.InInterval(t,cut.First,cut.Last))continue;
                if(certified&&(t<cut.First||t>cut.Last))continue;
                var duplicate=false;
                for(var j=0;j<n;j++)
                {
                    if(certified&&known&&parameters[j]==t&&endpoints[j]!=i)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    if(model.RequireBoundaryAssociations||certified&&known?endpoints[j]==i:Math.Abs(parameters[j]-t)<1e-10)duplicate=true;
                }
                if(duplicate)continue;
                if(n==parameters.Length)return ParasolidConstants.PK_ERROR_not_implemented;parameters[n]=t;endpoints[n++]=i;
            }
            for(var i=1;i<n;i++)for(var j=i;j>0&&parameters[j]<parameters[j-1];j--)
            { (parameters[j],parameters[j-1])=(parameters[j-1],parameters[j]);(endpoints[j],endpoints[j-1])=(endpoints[j-1],endpoints[j]); }
            if(cut.Closed==0&&!certified)
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
                var span=wrap?cut.Last-first+last-cut.First:last-first;if(!(span>(model.RequireBoundaryAssociations||certified?0:1e-12)))continue;
                if(certified&&Math.BitIncrement(first)>=last)return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var inside=false;var located=false;
                for(var attempt=0;attempt<7;attempt++)
                {
                    var fraction=.3819660112501051+attempt*.037;var t=first+span*fraction;if(t>cut.Last)t=cut.First+t-cut.Last;
                    var error=SheetCurveUV(surface,cut.Curve,t,true,out var uv,out _,out var point);if(error!=0)return error;
                    var coincident=false;
                    if(face>=0){error=SheetPointOnCoincidentBoundary(face,sources,point,out coincident,checkAllBoundaries);if(error!=0)return error;}
                    if(coincident){inside=false;located=true;break;}
                    error=domainFace>=0?RegularFaceContains(domain,domainFace,uv,out inside):SheetSourceContains(face,uv,out inside);
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
                BufferOffset edge=-1;
                if(checkAllBoundaries)
                {
                    var candidate=new ModelingEdge{Curve=curve,Start=lower,End=upper,StartVertex=positive?a:b,EndVertex=positive?b:a};
                    for(var existing=0;existing<model.NE;existing++)
                    {
                        var equivalentError=EquivalentRegularEdges(candidate,model.Edges[existing],coincidenceRoots,out var equivalent,out _);if(equivalentError!=0)return equivalentError;
                        if(equivalent){edge=existing;break;}
                    }
                    if(edge>=0&&curve!=cut.Curve){var unused=curve;var deleted=EntityDelete(1,&unused);if(deleted!=0)return deleted;}
                }
                var add=0;if(edge<0){add=model.AddEdge(curve,lower,upper,positive?a:b,positive?b:a,true,out edge,exact:certified);if(add!=0)return add;}
                if(checkAllBoundaries)
                {
                    if(cut.KeepImprint!=0)model.CutEdges[edge]|=2;
                    var already=false;for(var h=0;h<nh;h++)already|=halfedges[h].Edge==edge;if(already)continue;
                    model.CutEdges[edge]|=1;
                }
                add=AddSheetHalfedge(surface,ref model,edge,1,halfedges,ref nh,face,faceSense:sense);if(add!=0)return add;
                add=AddSheetHalfedge(surface,ref model,edge,0,halfedges,ref nh,face,faceSense:sense);if(add!=0)return add;
            }
        }
        var arrangementError=ExtractSheetCycles(surface,ref model,halfedges[..nh],cycleCoedges,cycles,out var cycleCount,out var coedgeCount);
        if(arrangementError!=0)return arrangementError;
        return PublishFiniteSheetCycles(surface,sense,face,reverse,ref model,cycleCoedges[..coedgeCount],cycles[..cycleCount],plane,borrowGeometry);

        static bool HasCutEnd(ref SheetArrangement graph,CurveTag carrier,BoundaryEventId endpoint,double parameter)
            =>endpoint>=0&&endpoint<graph.NP&&graph.KnownBoundaryParameter(endpoint,carrier,out var known)&&parameter>=known.Lower&&parameter<=known.Upper;
    }

    private static int ExtractSheetCycles(SurfTag surface,scoped ref SheetArrangement model,scoped Span<SheetHalfedge> halfedges,
        scoped Span<ModelingCoedge> cycleCoedges,scoped Span<SheetCycle> cycles,out BufferCount cycleCount,out BufferCount coedgeCount)
    {
        cycleCount=coedgeCount=0;var nh=halfedges.Length;
        // Clockwise successor from the incoming reverse tangent walks the face
        // on the left, including intersections at existing vertices.
        for(var i=0;i<nh;i++)
        {
            LastSheetPartitionStage=SheetPartitionStage.CycleTrace;
            ref var h=ref halfedges[i];ref readonly var edge=ref model.Edges[h.Edge];
            LastSheetCurve=edge.Curve;LastSheetParameter=i;
            var end=h.EndEvent;
            if(end<0){h.Next=i;continue;}
            var best=double.PositiveInfinity;var next=-1;
            for(var j=0;j<nh;j++)
            {
                ref readonly var candidate=ref halfedges[j];ref readonly var ce=ref model.Edges[candidate.Edge];
                var start=candidate.StartEvent;if(start!=end)continue;
                var angle=h.EndAngle+Math.PI-candidate.StartAngle;angle-=Math.Tau*Math.Floor(angle/Math.Tau);if(angle<1e-12)angle=Math.Tau;
                var tangentPair=edge.Curve==ce.Curve||model.KnownTangency(end,edge.Curve,ce.Curve,surface);
                if(tangentPair)
                {
                    if(Math.Cos(h.EndAngle+Math.PI-candidate.StartAngle)<0)angle=Math.PI;
                    else if(h.Edge==candidate.Edge&&h.Forward!=candidate.Forward)angle=Math.Tau;
                    else
                    {
                        if(!TryBoundaryCurvature(surface,edge,h.Forward,true,out var incoming)
                            ||!TryBoundaryCurvature(surface,ce,candidate.Forward,false,out var outgoing))return ParasolidConstants.PK_ERROR_not_implemented;
                        var turn=-incoming-outgoing;
                        if(turn.Lower>0)angle=0;else if(turn.Upper<0)angle=Math.Tau;
                        else return TraceUnresolvedOrder(surface,end,edge.Curve,ce.Curve,h.EndAngle,candidate.StartAngle,turn);
                    }
                }
                var choose=angle<best;
                if(next>=0)
                {
                    ref readonly var previous=ref halfedges[next];var previousEdge=model.Edges[previous.Edge];
                    if(Math.Abs(angle-best)<Math.PI&&model.KnownTangency(end,ce.Curve,previousEdge.Curve,surface)&&Math.Cos(candidate.StartAngle-previous.StartAngle)>0)
                    {
                        if(!TryBoundaryCurvature(surface,ce,candidate.Forward,false,out var currentCurvature)
                            ||!TryBoundaryCurvature(surface,previousEdge,previous.Forward,false,out var previousCurvature))return ParasolidConstants.PK_ERROR_not_implemented;
                        var order=currentCurvature-previousCurvature;
                        if(order.Lower>0)choose=true;else if(order.Upper<0)choose=false;
                        else return TraceUnresolvedOrder(surface,end,ce.Curve,previousEdge.Curve,candidate.StartAngle,previous.StartAngle,order);
                    }
                }
                if(choose){best=angle;next=j;}
            }
            if(next<0)return ParasolidConstants.PK_ERROR_bad_loop;h.Next=next;
            TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.Successor,Surface=surface,Curve=edge.Curve,Edge=h.Edge,NextEdge=halfedges[next].Edge,
                StartEvent=h.StartEvent,EndEvent=h.EndEvent,Forward=h.Forward,StartAngle=h.StartAngle,EndAngle=h.EndAngle});
        }
        for(var seed=0;seed<nh;seed++)
        {
            if(halfedges[seed].Used!=0)continue;if(cycleCount==cycles.Length)return ParasolidConstants.PK_ERROR_not_implemented;
            var first=coedgeCount;var current=seed;
            do
            {
                LastSheetPartitionStage=SheetPartitionStage.CycleTrace;
                LastSheetCurve=model.Edges[halfedges[current].Edge].Curve;LastSheetParameter=current;
                ref var h=ref halfedges[current];if(h.Used!=0||coedgeCount==cycleCoedges.Length)return ParasolidConstants.PK_ERROR_bad_loop;
                h.Used=1;cycleCoedges[coedgeCount++]=new(){Edge=h.Edge,Forward=h.Forward};current=h.Next;
            }while(current!=seed);
            var cycle=new SheetCycle{First=first,Count=coedgeCount-first,Parent=-1};
            if(NullSheetBoundary(cycleCoedges.Slice(first,cycle.Count))){coedgeCount=first;continue;}
            LastSheetPartitionStage=SheetPartitionStage.CycleWitness;
            var error=SheetCycleWitness(surface,model.Edges[..model.NE],cycleCoedges.Slice(first,cycle.Count),out cycle.Left,out cycle.Positive);if(error!=0)return error;
            cycles[cycleCount++]=cycle;
        }
        return 0;
    }

    private static int TraceUnresolvedOrder(SurfTag surface,BoundaryEventId endpoint,CurveTag first,CurveTag second,double a,double b,IntervalNumber turn)
    {
        TraceFiniteBoundary?.Invoke(new(){Stage=BoundaryTraceStage.UnresolvedOrder,Surface=surface,StartEvent=endpoint,EndEvent=endpoint,
            Curve=first,ProductCurve=second,StartAngle=a,EndAngle=b,First=turn.Lower,Last=turn.Upper});
        return ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

    // A finite cut can end in the interior of the other occurrence's
    // mathematical domain. Traversing its exact edge uses in both directions
    // is a zero 1-chain, hence bounds no cell; spatial size is irrelevant.
    private static bool NullSheetBoundary(ReadOnlySpan<ModelingCoedge> boundary)
    {
        foreach(var edge in boundary)
        {
            var balance=0;
            foreach(var other in boundary)if(other.Edge==edge.Edge)balance+=other.Forward!=0?1:-1;
            if(balance!=0)return false;
        }
        return true;
    }

    // Certified tangency supplies the common first-order ray. The first
    // separated normal-curvature coefficient orders its two germs; a finite
    // offset sample cannot distinguish a true contact from a short crossing.
    private static bool TryBoundaryCurvature(SurfTag surface,in ModelingEdge edge,byte forward,bool atEnd,out IntervalNumber curvature)
    {
        curvature=default;var support=Surfaces[GetSurfaceSlotByTag(surface)];
        var record=Curves[GetCurveSlotByTag(edge.Curve)];
        var positive=(forward!=0)==(LocalSenseSign(Curves[GetCurveSlotByTag(edge.Curve)].Sense)>0);
        var parameter=positive!=atEnd?edge.Start:edge.End;
        if(!TryReadExplicitSpineCurve(record,out var curve))return TryImplicitBoundaryCurvature(surface,record,parameter,positive,out curvature);
        if(!TryPrepareSurface(support,out var analytic))return false;
        if(curve.Kind==CurveClass.Line)return true;
        var status=SpineParameterMap.Interval(curve,new IntervalDual(parameter,1),out var point,out var tangent);
        if(status!=AlgorithmStatus.Success)return false;
        var d1=new IntervalVector(tangent.X.Value,tangent.Y.Value,tangent.Z.Value);
        var d2=new IntervalVector(tangent.X.Derivative,tangent.Y.Derivative,tangent.Z.Derivative);
        var length=d1.Norm().Value;if(length.Lower<=0)return false;
        var normal=new IntervalVector(analytic.Axis);
        if(analytic.Kind==SurfaceClass.Cylinder)
        {
            var delta=new IntervalVector(point.X.Value,point.Y.Value,point.Z.Value)-new IntervalVector(analytic.Origin);
            normal=(delta-normal*IntervalVector.Dot(delta,normal)).Unit();
        }
        else if(analytic.Kind!=SurfaceClass.Plane)return false;
        curvature=IntervalVector.Dot(normal,IntervalVector.Cross(d1,d2)).Value/(length*length*length);
        if(!positive)curvature=-curvature;return curvature.Finite;
    }

    private static bool OtherBoundarySupport(SurfTag surface,CurveTag curve,out SurfTag other)
    {
        other=0;var record=Curves[GetCurveSlotByTag(curve)];if(record.Class!=CurveClass.ICurve)return false;
        var data=ICurveDataPool[record.DataIndex];
        if(SameSheetSupport(surface,data.Surface0Tag))other=data.Surface1Tag;
        else if(SameSheetSupport(surface,data.Surface1Tag))other=data.Surface0Tag;
        return other!=0;
    }

    private static bool TryImplicitBoundaryCurvature(SurfTag surface,in CurveRecord record,double parameter,bool positive,out IntervalNumber curvature)
    {
        curvature=default;if(record.Class!=CurveClass.ICurve)return false;
        var data=ICurveDataPool[record.DataIndex];var a=data.Surface0Tag;var b=data.Surface1Tag;
        if(data.ProgramBlock>0)
        {
            var side=SameSheetSupport(surface,a)?0:SameSheetSupport(surface,b)?1:-1;if(side<0)return false;
            var program=GetNestedBlendProgram(data.ProgramBlock,data.ProgramSurfaceCount,data.ProgramSpineCount,data.ProgramChartCount,data.ProgramScalarCount);
            var status=JointNestedBlendEvaluation.BoundSpineCurvature(program,data.ProgramSpine,parameter,side,out curvature);
            curvature*=LocalSenseSign(Surfaces[GetSurfaceSlotByTag(surface)].Sense);
            if(!positive)curvature=-curvature;return status==AlgorithmStatus.Success;
        }
        if(SameSheetSupport(surface,b))(a,b)=(b,a);else if(!SameSheetSupport(surface,a))return false;
        if(!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(a)],out var first)||!TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(b)],out var second))return false;
        var found=CertifiedIntersectionCurveExtent(record.Header.Tag,Math.BitDecrement(parameter),Math.BitIncrement(parameter),out var box);
        if(found!=AlgorithmStatus.Success)return false;
        var point=new IntervalVector(new IntervalNumber(box.XLo,box.XHi),new IntervalNumber(box.YLo,box.YHi),new IntervalNumber(box.ZLo,box.ZHi));
        if(!TryQuadricGradient(first,point,out var n)||!TryQuadricGradient(second,point,out var m))return false;
        var cross=IntervalVector.Cross(n,m);var length=cross.Norm().Value;if(length.Lower<=0)return false;var tangent=cross/length;
        Span<KernelVector3> jet=stackalloc KernelVector3[2];
        if(EvaluateCurveCore(in record,parameter,1,false,jet,out _)!=AlgorithmStatus.Success)return false;
        var alignment=IntervalVector.Dot(tangent,new IntervalVector(jet[1])).Value;
        if(alignment.ContainsZero)return false;if((alignment.Lower>0)!=positive)tangent=tangent*-1;
        var h=QuadricSecond(first,tangent);var k=QuadricSecond(second,tangent);
        var nn=IntervalVector.Dot(n,n).Value;var lateral=IntervalVector.Cross(n,tangent);var denominator=IntervalVector.Dot(m,lateral).Value;
        if(nn.Lower<=0||denominator.ContainsZero)return false;
        var alpha=-h/nn;
        curvature=(-k-alpha*IntervalVector.Dot(m,n).Value)/denominator*n.Norm().Value;return curvature.Finite;
    }

    private static bool TryQuadricGradient(in AnalyticSurface surface,in IntervalVector point,out IntervalVector normal)
    {
        normal=new(surface.Axis);if(surface.Kind==SurfaceClass.Plane)return true;
        var delta=point-new IntervalVector(surface.Origin);
        if(surface.Kind==SurfaceClass.Sphere){normal=delta;return true;}
        if(surface.Kind!=SurfaceClass.Cylinder)return false;
        normal=delta-normal*IntervalVector.Dot(delta,normal);return true;
    }

    private static IntervalNumber QuadricSecond(in AnalyticSurface surface,in IntervalVector tangent)
    {
        if(surface.Kind==SurfaceClass.Plane)return 0;
        var result=IntervalVector.Dot(tangent,tangent).Value;
        if(surface.Kind==SurfaceClass.Cylinder)
        {var axial=IntervalVector.Dot(new IntervalVector(surface.Axis),tangent).Value;result-=axial*axial;}
        return result;
    }
}
