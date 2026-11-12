using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;
namespace ProjectGmKernel.Native.Geometry.Evaluation;
// Temporary diagnostic callbacks; the oracle owns serialization and I/O.
internal static class JointWorkProbe
{
    internal delegate void BeginObserver(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,in SurfaceParameterBox domain,BufferCount outputLength,
        ReadOnlySpan<BufferCount> reachBudget,BufferCount curvature,BufferOffset curvatureSide,BufferCount support,BufferOffset parameterSide,
        BufferCount capture,ReadOnlySpan<IntervalNumber> require,BufferOffset rootChart,BufferCount anchors,BufferCount acceleration,
        in KernelVector3 normal,BufferCount captureCharts,ReadOnlySpan<BufferOffset> requireCharts);
    internal delegate void PreparedObserver(ReadOnlySpan<JointNestedBlendEvaluation.Occurrence> nodes,ReadOnlySpan<BufferOffset> charts,ReadOnlySpan<TaylorJet> snapshot,BufferCount n);
    internal delegate void TubeObserver(ReadOnlySpan<IntervalNumber> boxes,ReadOnlySpan<IntervalNumber> derivative);
    internal delegate void EndObserver(AlgorithmStatus status);
    internal static BeginObserver? OnBegin;
    internal static PreparedObserver? OnPrepared;
    internal static TubeObserver? OnTube;
    internal static EndObserver? OnEnd;
    internal static bool Enabled=>OnBegin!=null;
    internal static void Begin(in NestedBlendProgram program,BlendSurfaceIndex surface,BlendSpineIndex spine,in SurfaceParameterBox domain,BufferCount outputLength,
        ReadOnlySpan<BufferCount> reachBudget,BufferCount curvature,BufferOffset curvatureSide,BufferCount support,BufferOffset parameterSide,
        BufferCount capture,ReadOnlySpan<IntervalNumber> require,BufferOffset rootChart,BufferCount anchors,BufferCount acceleration,
        in KernelVector3 normal,BufferCount captureCharts,ReadOnlySpan<BufferOffset> requireCharts)
        =>OnBegin?.Invoke(program,surface,spine,domain,outputLength,reachBudget,curvature,curvatureSide,support,parameterSide,capture,require,rootChart,anchors,acceleration,normal,captureCharts,requireCharts);
    internal static void Prepared(ReadOnlySpan<JointNestedBlendEvaluation.Occurrence> nodes,ReadOnlySpan<BufferOffset> charts,ReadOnlySpan<TaylorJet> snapshot,BufferCount n)=>OnPrepared?.Invoke(nodes,charts,snapshot,n);
    internal static void Tube(ReadOnlySpan<IntervalNumber> boxes,ReadOnlySpan<IntervalNumber> derivative)=>OnTube?.Invoke(boxes,derivative);
    internal static void End(AlgorithmStatus status)=>OnEnd?.Invoke(status);
}
