using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Hot-path evaluation counters for T20 / §22 (caller-owned, zero-alloc).
/// Not a publish speed gate — reports jet/cache activity under the same
/// correctness threshold as KernelTests.
/// </summary>
internal struct EvaluationCounters
{
    internal int BaseJetEvaluations;
    internal int CacheExactHits;
    internal int CacheNeighborSeeds;
    internal int CorrectorTrials;
    internal int CorrectorAccepts;
    internal int PlanSwitches;
    internal int PredictedOnlyInserts;

    internal void Reset() => this = default;

    internal void RecordCacheHit(CacheHitKind kind)
    {
        if (kind == CacheHitKind.Exact) CacheExactHits++;
        else if (kind == CacheHitKind.NeighborSeed) CacheNeighborSeeds++;
    }
}
