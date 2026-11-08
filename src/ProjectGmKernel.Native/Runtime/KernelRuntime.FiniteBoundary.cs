using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal struct BoundaryParameterConflict
    {
        internal BoundaryEventId Event;
        internal CurveTag Carrier;
        internal double Previous,PreviousLower,PreviousUpper,Incoming,Lower,Upper;
    }
    [ThreadStatic] internal static BoundaryParameterConflict LastBoundaryParameterConflict;
    private struct BoundaryParameter
    {
        internal BoundaryEventId Event;
        internal CurveTag Carrier;
        internal EntityGeneration Generation;
        // Surface may name the scalar equation with a unique root in this
        // native interval. That equation is used only within this command.
        internal SurfTag Surface;
        // One-based (finite input edge, isolated root ordinal) within the
        // named equation's complete result, shared by its coedge discoveries.
        // Accepted cut uses occupy a separate occurrence range after input edges.
        internal BoundaryRootWitnessId Witness;
        // A transferred line station names its finite occurrence without
        // using the original curve/surface root ordinal.
        internal BufferOffset LineOccurrence;
        internal double Parameter,Lower,Upper;

        internal readonly BufferOffset SourceOccurrence=>Witness==0?LineOccurrence-1:(Witness-1)/IntersectionPointCapacity;
    }
    private struct BoundaryCarrierSurface
    {
        internal CurveTag Carrier;
        internal SurfTag Surface;
        internal EntityGeneration CarrierGeneration,SurfaceGeneration;
        internal BufferOffset InputEdge;
        internal double First,Last;

        internal readonly bool Covers(double first,double last)=>First<=first&&last<=Last;
    }
    private struct BoundaryTangency
    {
        internal BoundaryEventId Event;
        internal CurveTag First,Second;
    }
    private struct BoundarySurfaceContact
    {
        internal BoundaryEventId Event;
        internal SurfTag First,Second;
    }
    // Endpoint identities belong to one command's explicit incidence graph.
    // A positive-length interval is retained regardless of its spatial length.
    private struct FiniteBoundaryUse
    {
        internal CurveTag Carrier;
        internal EntityGeneration Generation,SurfaceGeneration,SourceFaceGeneration,SourceEdgeGeneration,SourceFinGeneration;
        internal CurveClass Class;
        internal SurfTag Surface;
        internal FaceSlot Source;
        internal EdgeSlot SourceEdge;
        internal FinSlot SourceFin;
        internal BufferOffset Edge;
        internal BoundaryEventId StartEvent,EndEvent;
        internal double First,Last;
        internal KernelSense CurveSense,SurfaceSense,FaceSense;
        internal byte Forward;

        internal static FiniteBoundaryUse Create(SurfTag surface,in ModelingEdge edge,BufferOffset index,byte forward,FaceSlot source=-1,
            EdgeSlot sourceEdge=-1,FinSlot sourceFin=-1,KernelSense faceSense=0)
        {
            var carrier=Curves[GetCurveSlotByTag(edge.Curve)];var support=Surfaces[GetSurfaceSlotByTag(surface)];
            return new(){Carrier=edge.Curve,Generation=carrier.Header.Generation,Class=carrier.Class,CurveSense=carrier.Sense,
                Surface=surface,SurfaceGeneration=support.Header.Generation,SurfaceSense=support.Sense,
                FaceSense=NormalizeLocalSense(faceSense==0&&source>=0?Faces[source].Orientation:faceSense),
                Source=source,SourceEdge=sourceEdge,SourceFin=sourceFin,SourceFaceGeneration=source>=0?Faces[source].Header.Generation:0,
                SourceEdgeGeneration=sourceEdge>=0?Edges[sourceEdge].Header.Generation:0,
                SourceFinGeneration=sourceFin>=0?Fins[sourceFin].Header.Generation:0,
                Edge=index,First=edge.Start,Last=edge.End,Forward=forward,
                StartEvent=forward!=0?edge.StartVertex:edge.EndVertex,EndEvent=forward!=0?edge.EndVertex:edge.StartVertex};
        }
    }

    [ThreadStatic] internal static bool ForbidLegacyRegularBlendAssembly;
    [ThreadStatic] internal static bool ForbidSpatialBoundaryReuse;
    internal enum BoundaryTraceStage:byte{Input,Successor,Arrangement,Product,CarrierReuse,CertifiedRoot,UnresolvedOrder,NativeCutLocator,NativeCutEndpoints,AnalyticSectionEnds,SpatialEndpointReuse,NativeSectionChart,NativeSectionWinding,ThirdFaceStation,ThirdFaceOutsideTrim,CoincidentLineStation}
    internal struct BoundaryTrace
    {
        internal PK_UV_s UvStart,UvEnd;
        internal BoundaryTraceStage Stage;
        internal FaceSlot Source,Product;
        internal EdgeSlot SourceEdge;
        internal FinSlot SourceFin;
        internal SurfTag Surface,OtherSurface;
        internal CurveTag Curve,ProductCurve;
        internal EntityGeneration Generation,SurfaceGeneration;
        internal BufferOffset Edge,Face,NextEdge,InputCoedge;
        internal BoundaryEventId StartEvent,EndEvent;
        internal BoundaryRootWitnessId Witness;
        internal double First,Last,StartAngle,EndAngle,SpatialError;
        internal KernelVector3 StartPoint,EndPoint;
        internal KernelSense CurveSense,SurfaceSense,FaceSense;
        internal byte Forward;
    }
    [ThreadStatic] internal static Action<BoundaryTrace>? TraceFiniteBoundary;

    private static void TraceBoundary(BoundaryTraceStage stage,in FiniteBoundaryUse use,BufferOffset face=-1,FaceSlot product=-1,CurveTag productCurve=0)
        =>TraceFiniteBoundary?.Invoke(new(){Stage=stage,Source=use.Source,Product=product,Surface=use.Surface,Curve=use.Carrier,
            ProductCurve=productCurve,Generation=use.Generation,SurfaceGeneration=use.SurfaceGeneration,SourceEdge=use.SourceEdge,SourceFin=use.SourceFin,
            CurveSense=use.CurveSense,SurfaceSense=use.SurfaceSense,FaceSense=use.FaceSense,Edge=use.Edge,Face=face,StartEvent=use.StartEvent,EndEvent=use.EndEvent,
            First=use.First,Last=use.Last,Forward=use.Forward});

    // Retained boundaries are supplied without a loop order. The shared
    // halfedge successor and hole extraction choose their final incidence.
    private static int ArrangeFiniteReplacementFace(SurfTag surface,KernelSense sense,FaceSlot source,
        scoped ReadOnlySpan<ModelingCoedge> boundary,scoped ref SheetArrangement model,scoped Span<SheetHalfedge> halfedges,
        scoped Span<ModelingCoedge> cycleCoedges,scoped Span<SheetCycle> cycles,scoped ReadOnlySpan<FinSlot> sourceFins=default)
    {
        var reverse=LocalSenseSign(sense)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(surface)].Sense)<0;
        var count=0;
        if(!sourceFins.IsEmpty&&sourceFins.Length!=boundary.Length)return ParasolidConstants.PK_ERROR_bad_parameter;
        for(var i=0;i<boundary.Length;i++)
        {
            var coedge=boundary[i];var fin=sourceFins.IsEmpty?-1:sourceFins[i];
            var forward=(byte)(coedge.Forward^(reverse?1:0));
            var use=FiniteBoundaryUse.Create(surface,model.Edges[coedge.Edge],coedge.Edge,forward,source,fin>=0?Fins[fin].Edge:-1,fin,sense);
            var error=AddFiniteBoundaryHalfedge(use,ref model,halfedges,ref count);if(error!=0)return error;
        }
        var result=ExtractSheetCycles(surface,ref model,halfedges[..count],cycleCoedges,cycles,out var nc,out var ne);
        if(result!=0)return result;
        return PublishFiniteSheetCycles(surface,sense,source,reverse,ref model,cycleCoedges[..ne],cycles[..nc]);
    }

    private static int BuildArrangedLocalBody(scoped ref SheetArrangement model,out BodyTag body,Span<FaceSlot> products)
    {
        var error=BuildModelingBrep(model.Points[..model.NP],model.Edges[..model.NE],model.Coedges[..model.NC],model.Loops[..model.NL],
            model.Faces[..model.NF],true,out body,products);if(error!=0)return error;
        if(TraceFiniteBoundary!=null)for(var i=0;i<model.NF;i++)
        {
            var face=model.Faces[i];
            for(var l=0;l<face.LoopCount;l++)
            {
                var loop=model.Loops[face.FirstLoop+l];
                foreach(var coedge in model.Coedges.Slice(loop.FirstCoedge,loop.CoedgeCount))
                {
                    var use=FiniteBoundaryUse.Create(face.Surface,model.Edges[coedge.Edge],coedge.Edge,coedge.Forward,model.Sources[i],faceSense:face.Sense);
                    TraceBoundary(BoundaryTraceStage.Product,use,i,products[i],use.Carrier);
                }
            }
        }
        return 0;
    }
}
