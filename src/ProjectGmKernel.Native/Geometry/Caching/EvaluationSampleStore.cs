using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>Where a stored sample came from; drives what may reuse it (spec §13.4).</summary>
internal enum SampleSourceKind : byte
{
    /// <summary>Original chart anchor: D0 is defining data and is never overwritten.</summary>
    ImportedChartAnchor = 0,
    /// <summary>Corrected root that satisfied its defining residuals on publish.</summary>
    CorrectedRoot = 1,
    /// <summary>Prediction only — never publishable as an exact result.</summary>
    PredictedOnly = 2,
}

/// <summary>Cache outcome of one cached evaluation request (§19.2 report field).</summary>
internal enum CacheHitKind : byte
{
    None = 0,
    /// <summary>An exact sample satisfied the request directly.</summary>
    Exact = 1,
    /// <summary>A neighbor prediction seeded the corrector that produced the result.</summary>
    NeighborSeed = 2,
}

/// <summary>One reusable evaluation sample with its parameter correspondence (§13.2, slice layout).</summary>
internal readonly struct CurveSample
{
    internal readonly double Parameter;           // native t, bit-exact key
    internal readonly KernelVector3 Position;
    internal readonly KernelVector3 First;        // D1 (valid when MaxOrder ≥ 1)
    internal readonly KernelVector3 Second;       // D2 (valid when MaxOrder ≥ 2)
    internal readonly DerivativeOrder MaxOrder;
    internal readonly ICurveQueryKind Kind;
    internal readonly ChartSide Side;
    internal readonly BufferOffset Segment;
    internal readonly double ErrorEstimate;       // sample's own residual-scaled bound
    internal readonly SampleSourceKind Source;
    internal readonly ICurveConstraintPlan Plan;

    internal CurveSample(double parameter, in KernelVector3 position, in KernelVector3 first,
        in KernelVector3 second, DerivativeOrder maxOrder, ICurveQueryKind kind, ChartSide side,
        BufferOffset segment, double errorEstimate, SampleSourceKind source, ICurveConstraintPlan plan)
    {
        Parameter = parameter;
        Position = position;
        First = first;
        Second = second;
        MaxOrder = maxOrder;
        Kind = kind;
        Side = side;
        Segment = segment;
        ErrorEstimate = errorEstimate;
        Source = source;
        Plan = plan;
    }
}

/// <summary>
/// Operation-level sample atlas (spec §13.3 layer L2, task T09): caller-owned
/// fixed storage, insertion-ordered by native parameter, exact keys — no
/// bucketing that could map neighboring parameters to one point (§13.4).
/// Only verified sources satisfy exact hits; predictions remain seeds.
/// </summary>
internal ref struct EvaluationSampleStore
{
    private Span<CurveSample> slots;
    private BufferCount count;

    internal EvaluationSampleStore(Span<CurveSample> storage)
    {
        slots = storage;
        count = 0;
    }

    internal readonly BufferCount Count => count;

    /// <summary>
    /// Exact hit: bit-identical parameter plus matching query semantics, side,
    /// sufficient derivative order and error bound. Predicted-only samples
    /// never qualify (§13.4).
    /// </summary>
    internal readonly bool TryFindExact(double parameter, ICurveQueryKind kind, ChartSide side,
        DerivativeOrder minOrder, double maxError, out CurveSample sample)
    {
        sample = default;
        if (!double.IsFinite(parameter) || maxError < 0) return false;
        for (BufferOffset i = 0; i < count; i++)
        {
            ref readonly var candidate = ref slots[i];
            if (candidate.Parameter != parameter || candidate.Kind != kind || candidate.Side != side)
                continue;
            if (candidate.Source == SampleSourceKind.PredictedOnly) continue;
            if (candidate.MaxOrder < minOrder || candidate.ErrorEstimate > maxError) continue;
            sample = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Nearest verified samples below and above the parameter on the same
    /// query semantics — the bracket for a Hermite seed prediction. Bracketing
    /// is not a hit: the result is a seed only (§13.4).
    /// </summary>
    internal readonly bool TryFindBracket(double parameter, ICurveQueryKind kind, ChartSide side,
        out CurveSample lower, out CurveSample upper)
    {
        lower = upper = default;
        var hasLower = false;
        var hasUpper = false;
        for (BufferOffset i = 0; i < count; i++)
        {
            ref readonly var candidate = ref slots[i];
            if (candidate.Kind != kind || candidate.Side != side) continue;
            if (candidate.Source == SampleSourceKind.PredictedOnly) continue;
            if (candidate.Parameter < parameter && (!hasLower || candidate.Parameter > lower.Parameter))
            {
                lower = candidate;
                hasLower = true;
            }
            if (candidate.Parameter > parameter && (!hasUpper || candidate.Parameter < upper.Parameter))
            {
                upper = candidate;
                hasUpper = true;
            }
        }
        return hasLower && hasUpper;
    }

    /// <summary>
    /// Insert or improve: the anchor keeps precedence over corrected roots at
    /// the same key (§13.4 node rule), corrected roots replace worse-rooted or
    /// predicted entries. Failure (storage full) leaves the store unchanged.
    /// </summary>
    internal bool TryInsert(in CurveSample sample)
    {
        for (BufferOffset i = 0; i < count; i++)
        {
            ref var candidate = ref slots[i];
            if (candidate.Parameter != sample.Parameter || candidate.Kind != sample.Kind
                || candidate.Side != sample.Side)
                continue;
            if (RankOf(sample.Source) <= RankOf(candidate.Source) && sample.ErrorEstimate >= candidate.ErrorEstimate)
                return true; // nothing to improve
            candidate = sample;
            return true;
        }
        if (count >= slots.Length) return false;
        slots[count++] = sample;
        return true;
    }

    private static int RankOf(SampleSourceKind source) => source switch
    {
        SampleSourceKind.ImportedChartAnchor => 2,
        SampleSourceKind.CorrectedRoot => 1,
        _ => 0,
    };
}
