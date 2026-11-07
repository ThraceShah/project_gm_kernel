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
        // Carrier==0 denotes a certified surface participant. For a native
        // locator, Surface may name the scalar equation with a unique root
        // in this interval. That equation is used only within this command.
        internal SurfTag Surface;
        internal double Parameter,Lower,Upper;
    }
    private struct BoundaryCarrierSurface
    {
        internal CurveTag Carrier;
        internal SurfTag Surface;
        internal EntityGeneration CarrierGeneration,SurfaceGeneration;
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
        internal EntityGeneration Generation;
        internal SurfTag Surface;
        internal FaceSlot Source;
        internal BufferOffset Edge;
        internal BoundaryEventId StartEvent,EndEvent;
        internal double First,Last;
        internal byte Forward;

        internal static FiniteBoundaryUse Create(SurfTag surface,in ModelingEdge edge,BufferOffset index,byte forward,FaceSlot source=-1)
            =>new(){Carrier=edge.Curve,Generation=Curves[GetCurveSlotByTag(edge.Curve)].Header.Generation,
                Surface=surface,Source=source,Edge=index,First=edge.Start,Last=edge.End,Forward=forward,
                StartEvent=forward!=0?edge.StartVertex:edge.EndVertex,EndEvent=forward!=0?edge.EndVertex:edge.StartVertex};
    }

    [ThreadStatic] internal static bool ForbidLegacyRegularBlendAssembly;
    internal enum BoundaryTraceStage:byte{Input,Successor,Arrangement,Product,CarrierReuse,CertifiedRoot,UnresolvedOrder}
    internal struct BoundaryTrace
    {
        internal BoundaryTraceStage Stage;
        internal FaceSlot Source,Product;
        internal SurfTag Surface,OtherSurface;
        internal CurveTag Curve,ProductCurve;
        internal EntityGeneration Generation;
        internal BufferOffset Edge,Face,NextEdge;
        internal BoundaryEventId StartEvent,EndEvent;
        internal double First,Last,StartAngle,EndAngle,SpatialError;
        internal KernelVector3 StartPoint,EndPoint;
        internal byte Forward;
    }
    [ThreadStatic] internal static Action<BoundaryTrace>? TraceFiniteBoundary;

    private static void TraceBoundary(BoundaryTraceStage stage,in FiniteBoundaryUse use,BufferOffset face=-1,FaceSlot product=-1,CurveTag productCurve=0)
        =>TraceFiniteBoundary?.Invoke(new(){Stage=stage,Source=use.Source,Product=product,Surface=use.Surface,Curve=use.Carrier,
            ProductCurve=productCurve,Generation=use.Generation,Edge=use.Edge,Face=face,StartEvent=use.StartEvent,EndEvent=use.EndEvent,
            First=use.First,Last=use.Last,Forward=use.Forward});

    // Retained boundaries are supplied without a loop order. The shared
    // halfedge successor and hole extraction choose their final incidence.
    private static int ArrangeFiniteReplacementFace(SurfTag surface,KernelSense sense,FaceSlot source,
        ReadOnlySpan<ModelingCoedge> boundary,ref SheetArrangement model,Span<SheetHalfedge> halfedges,
        Span<ModelingCoedge> cycleCoedges,Span<SheetCycle> cycles)
    {
        var reverse=LocalSenseSign(sense)*LocalSenseSign(Surfaces[GetSurfaceSlotByTag(surface)].Sense)<0;
        var count=0;
        foreach(var coedge in boundary)
        {
            var forward=(byte)(coedge.Forward^(reverse?1:0));
            var use=FiniteBoundaryUse.Create(surface,model.Edges[coedge.Edge],coedge.Edge,forward,source);
            var error=AddFiniteBoundaryHalfedge(use,ref model,halfedges,ref count);if(error!=0)return error;
        }
        var result=ExtractSheetCycles(surface,ref model,halfedges[..count],cycleCoedges,cycles,out var nc,out var ne);
        if(result!=0)return result;
        return PublishFiniteSheetCycles(surface,sense,source,reverse,ref model,cycleCoedges[..ne],cycles[..nc]);
    }
}
