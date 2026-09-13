using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Query semantics of one icurve evaluation request (spec §6.1, §18.1).</summary>
internal enum ICurveQueryKind : byte
{
    RegularChartInterval = 0,
    ChartPoint = 1,
    OutsideSupportedDomain = 2,
    /// <summary>Terminator intervals need the dedicated GATE-T path; never merged into this slice.</summary>
    TerminatorUnsupported = 3,
}

/// <summary>
/// Evaluation outcome: solver stop and semantic classification travel
/// together, but only <see cref="Status"/> decides publishability (§18.1).
/// Plan and derivative side enter the report per §19.2.
/// </summary>
internal readonly struct ICurveEvalReport
{
    internal readonly ICurveQueryKind Kind;
    internal readonly AlgorithmStatus Status;
    internal readonly ICurveConstraintPlan Plan;  // selected (or forced) plan; Auto for node/domain queries
    internal readonly ChartSide Side;             // published derivative side at chart nodes
    internal readonly BufferOffset Segment;       // original chart segment, -1 = none
    internal readonly BufferOffset NewtonIterations;
    internal readonly double Residual;            // final max |F| in scaled units
    internal readonly CacheHitKind CacheHit;      // how this request was served (§19.2)

    internal ICurveEvalReport(ICurveQueryKind kind, AlgorithmStatus status,
        ICurveConstraintPlan plan, ChartSide side, BufferOffset segment,
        BufferOffset newtonIterations, double residual, CacheHitKind cacheHit = CacheHitKind.None)
    {
        Kind = kind;
        Status = status;
        Plan = plan;
        Side = side;
        Segment = segment;
        NewtonIterations = newtonIterations;
        Residual = residual;
        CacheHit = cacheHit;
    }
}

/// <summary>
/// Prepared read-only view of one icurve for evaluation (spec §4.1, task T07
/// slice): two analytic supports plus the immutable original chart map. The
/// view borrows its spans; it owns nothing and outlives no data. Procedural
/// supports (blend/offset/...) attach to this shape in later tasks.
/// </summary>
internal readonly ref struct ICurveView
{
    internal readonly AnalyticSurface Support0;
    internal readonly KernelSense Sense0;
    internal readonly AnalyticSurface Support1;
    internal readonly KernelSense Sense1;
    internal readonly ReadOnlySpan<KernelVector3> ChartPositions;
    internal readonly ReadOnlySpan<double> ChartParameters;
    internal readonly ReadOnlySpan<double> ChartScales;
    internal readonly ReadOnlySpan<KernelVector3> ChartChordUnits;

    internal ICurveView(in AnalyticSurface support0, KernelSense sense0,
        in AnalyticSurface support1, KernelSense sense1,
        ReadOnlySpan<KernelVector3> chartPositions, ReadOnlySpan<double> chartParameters,
        ReadOnlySpan<double> chartScales, ReadOnlySpan<KernelVector3> chartChordUnits)
    {
        Support0 = support0;
        Sense0 = sense0;
        Support1 = support1;
        Sense1 = sense1;
        ChartPositions = chartPositions;
        ChartParameters = chartParameters;
        ChartScales = chartScales;
        ChartChordUnits = chartChordUnits;
    }
}
