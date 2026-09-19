using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Query semantics of one icurve evaluation request (spec §6.1, §18.1).</summary>
internal enum ICurveQueryKind : byte
{
    RegularChartInterval = 0,
    ChartPoint = 1,
    OutsideSupportedDomain = 2,
    /// <summary>Interval between the start chart boundary and a start terminator (§6).</summary>
    StartTerminatorInterval = 3,
    /// <summary>Interval between the end chart boundary and an end terminator (§6).</summary>
    EndTerminatorInterval = 4,
    /// <summary>The terminator point itself, at the resolved native parameter (§6.5).</summary>
    ExactTerminator = 5,
}

/// <summary>Locatable sub-reason published inside <see cref="ICurveEvalReport"/> (§18.5).</summary>
internal enum ICurveEvalDetail : byte
{
    None = 0,
    /// <summary>The request needs a compatibility gate (GATE-T) that is still open;
    /// nothing was guessed and nothing was published.</summary>
    CompatibilityGateOpen = 1,
    /// <summary>Shared base-evaluation budget exhausted (§14.7 / §15).</summary>
    BudgetExceeded = 2,
    /// <summary>Multiple same-quality roots on different branches; no evidence to pick (§17.5).</summary>
    AmbiguousBranch = 3,
    /// <summary>Corrector stagnated (small step, large residual, or reverse oscillation) (§14.6).</summary>
    Stagnation = 4,
    /// <summary>Original chart map failed validation (non-finite / non-monotonic).</summary>
    InvalidChart = 5,
    /// <summary>Native parameter could not be recovered after an internal reparameterization.</summary>
    ParameterResolutionLost = 6,
    /// <summary>Nested / inner solve accuracy insufficient for the outer accept step (§15).</summary>
    InnerAccuracyInsufficient = 7,
    /// <summary>Requested blend construction is outside the supported regular-R tube.</summary>
    UnsupportedBlendConstruction = 8,
    /// <summary>Parameterization of a support became singular at the query.</summary>
    ParameterizationSingular = 9,
    /// <summary>Diagnosed plan switch after Singular/stagnation (§14.6); report.Plan is the final plan.</summary>
    PlanSwitched = 10,
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
    internal readonly double NonDefiningResidual; // |φ| of the unselected terminator support (§6.4 diagnostic)
    internal readonly ICurveEvalDetail Detail;    // locatable sub-reason (§18.5)

    internal ICurveEvalReport(ICurveQueryKind kind, AlgorithmStatus status,
        ICurveConstraintPlan plan, ChartSide side, BufferOffset segment,
        BufferOffset newtonIterations, double residual, CacheHitKind cacheHit = CacheHitKind.None,
        double nonDefiningResidual = 0, ICurveEvalDetail detail = ICurveEvalDetail.None)
    {
        Kind = kind;
        Status = status;
        Plan = plan;
        Side = side;
        Segment = segment;
        NewtonIterations = newtonIterations;
        Residual = residual;
        CacheHit = cacheHit;
        NonDefiningResidual = nonDefiningResidual;
        Detail = detail;
    }
}

/// <summary>
/// A terminator LIMIT (type T): hvec[0] is the terminator position, hvec[1]
/// the branch point that also appears in the chart (§6.1). Stored verbatim —
/// the terminator's native parameter is not transmitted and only becomes
/// available through a resolved GATE-T rule.
/// </summary>
internal readonly struct TerminatorLimit
{
    internal readonly LimitTermUse TermUse;
    internal readonly KernelVector3 Endpoint;     // terminator position (limit hvec[0])
    internal readonly KernelVector3 BranchPoint;  // branch point (limit hvec[1])

    internal TerminatorLimit(LimitTermUse termUse, in KernelVector3 endpoint, in KernelVector3 branchPoint)
    {
        TermUse = termUse;
        Endpoint = endpoint;
        BranchPoint = branchPoint;
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
    internal readonly bool HasStartTerminator;
    internal readonly bool HasEndTerminator;
    internal readonly TerminatorLimit StartTerminator;
    internal readonly TerminatorLimit EndTerminator;

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
        HasStartTerminator = false;
        HasEndTerminator = false;
        StartTerminator = default;
        EndTerminator = default;
    }

    internal ICurveView(in AnalyticSurface support0, KernelSense sense0,
        in AnalyticSurface support1, KernelSense sense1,
        ReadOnlySpan<KernelVector3> chartPositions, ReadOnlySpan<double> chartParameters,
        ReadOnlySpan<double> chartScales, ReadOnlySpan<KernelVector3> chartChordUnits,
        TerminatorLimit startTerminator, TerminatorLimit endTerminator)
    {
        Support0 = support0;
        Sense0 = sense0;
        Support1 = support1;
        Sense1 = sense1;
        ChartPositions = chartPositions;
        ChartParameters = chartParameters;
        ChartScales = chartScales;
        ChartChordUnits = chartChordUnits;
        // A terminator limit is present when its endpoint data is finite;
        // absent ends stay default and never classify as terminator queries.
        HasStartTerminator = IsPresent(in startTerminator);
        HasEndTerminator = IsPresent(in endTerminator);
        StartTerminator = startTerminator;
        EndTerminator = endTerminator;
    }

    private static bool IsPresent(in TerminatorLimit limit)
        => limit.TermUse != LimitTermUse.Unset
            && IsFinite(limit.Endpoint) && IsFinite(limit.BranchPoint);
}
