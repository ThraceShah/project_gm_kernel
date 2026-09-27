using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace ProjectGmKernel.Native.Runtime;
/// <summary>Slot-ABA-safe geometry identity: a tag alone is never a cache key (§13.8).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GeometryIdentity
{
    internal EntityTag Tag;
    internal EntityGeneration Generation;

    internal GeometryIdentity(EntityTag tag, EntityGeneration generation)
    {
        Tag = tag;
        Generation = generation;
    }
}

/// <summary>
/// One L3 sample record. Stored by value in the cache arena — nothing in the
/// entry borrows session blocks or scratch, so eviction can never dangle
/// borrowed data (§13.9).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CachedCurveSample
{
    internal GeometryIdentity Owner;
    internal long Epoch;
    internal double Parameter;
    internal KernelVector3 Position;
    internal KernelVector3 First;
    internal KernelVector3 Second;
    internal DerivativeOrder MaxOrder;
    internal ICurveQueryKind Kind;
    internal ChartSide Side;
    internal BufferOffset Segment;
    internal double ErrorEstimate;
    internal double RawResidual;
    internal double NonDefiningResidual;
    internal TerminatorParameterRule TerminatorRule;
    internal SampleSourceKind Source;
    internal ICurveConstraintPlan Plan;
    internal SampleWitness Witness;
    internal byte ClockReferenced;
    internal byte Occupied;
}

/// <summary>
/// Cross-call geometry evaluation cache (spec §13.3 layer L3, §13.8–§13.9,
/// task T10): session-owned native arena outside the scratch and return
/// allocators, hard capacity with CLOCK eviction, and a monotonic model
/// geometry epoch — creation, deletion, rollback and session reset invalidate
/// every entry; cache writes never move the epoch.
/// Synchronization (§13.9, consolidated-review B1): every entry mutation —
/// lookup-and-copy, CLOCK reference bits, slot claim/overwrite/upgrade and
/// Attach/Detach/Clear — runs under one short SpinLock. No child evaluator is
/// ever called under the lock: hits are copied out by value, solves run
/// lock-free between a miss and the publish. This is required because
/// PK_CURVE_eval is dispatched as Concurrent/ReadOnly — multiple readers may
/// execute in parallel — so the "read" path's cache writes must be serialized.
/// </summary>
internal static unsafe class GeometryEvaluationCache
{
    /// <summary>Hard sample budget of the slice arena (§13.9 bounded arena).</summary>
    internal const int Capacity = 256;

    private static CachedCurveSample* arena;
    private static MemoryPageIndex clockHand;
    private static long modelGeometryEpoch;
    private static System.Threading.SpinLock gate = new();

    /// <summary>
    /// Monotonic model geometry epoch (§13.8): bumped by geometry creation,
    /// deletion, rollback and session lifecycle; never by cache writes.
    /// </summary>
    internal static long ModelGeometryEpoch => System.Threading.Volatile.Read(ref modelGeometryEpoch);

    internal static void BumpModelGeometryEpoch() => System.Threading.Interlocked.Increment(ref modelGeometryEpoch);

    // ── Session lifetime ─────────────────────────────────────────
    // The arena lives in one long-lived SessionMemory allocation — outside
    // CommandScratch, the return arena and the variable-length block
    // allocator (§13.9) — and is released when the session stops.

    internal static void Attach(SessionMemory* memory)
    {
        bool taken = false;
        gate.Enter(ref taken);
        try
        {
            DetachLocked(memory);
            if (memory == null) return;
            var block = memory->TryAllocate((nuint)(Capacity * sizeof(CachedCurveSample)));
            if (block == null) return; // cache disabled for this session, evaluation still works
            arena = (CachedCurveSample*)block;
            ClearLocked();
        }
        finally
        {
            if (taken) gate.Exit();
        }
    }

    internal static void Detach(SessionMemory* memory)
    {
        bool taken = false;
        gate.Enter(ref taken);
        try
        {
            DetachLocked(memory);
        }
        finally
        {
            if (taken) gate.Exit();
        }
    }

    private static void DetachLocked(SessionMemory* memory)
    {
        if (arena != null && memory != null)
            memory->Free(arena);
        arena = null;
        clockHand = 0;
    }

    internal static void Clear()
    {
        bool taken = false;
        gate.Enter(ref taken);
        try
        {
            ClearLocked();
        }
        finally
        {
            if (taken) gate.Exit();
        }
    }

    private static void ClearLocked()
    {
        if (arena == null) return;
        for (MemoryPageIndex i = 0; i < Capacity; i++)
            arena[i].Occupied = 0;
        clockHand = 0;
    }

    // ── Lookup and publish ───────────────────────────────────────

    /// <summary>
    /// Exact verified hit: identity (tag + generation), bit-identical
    /// parameter, current epoch, query semantics, side, derivative order and
    /// error bound must all match. The sample is copied out under the gate;
    /// nothing that runs here re-enters the cache.
    /// </summary>
    internal static bool TryGetExact(in GeometryIdentity identity, double parameter, ICurveQueryKind kind,
        ChartSide side, DerivativeOrder minOrder, double maxError, out CurveSample sample,
        TerminatorParameterRule rule = TerminatorParameterRule.Unresolved)
    {
        sample = default;
        bool taken = false;
        gate.Enter(ref taken);
        try
        {
            var epoch = Volatile.Read(ref modelGeometryEpoch);
            if (arena == null) return false;
            for (MemoryPageIndex i = 0; i < Capacity; i++)
            {
                ref var entry = ref arena[i];
                if (entry.Occupied == 0) continue;
                if (entry.Epoch != epoch) continue; // stale: dropped lazily, never served
                if (entry.Owner.Tag != identity.Tag || entry.Owner.Generation != identity.Generation) continue;
                if (entry.Parameter != parameter) continue;
                if (entry.Kind != kind || entry.Side != side || entry.TerminatorRule != rule) continue;
                if (entry.Source == SampleSourceKind.PredictedOnly) continue;
                if (entry.MaxOrder < minOrder || entry.ErrorEstimate > maxError) continue;
                entry.ClockReferenced = 1;
                sample = ToL2Sample(in entry);
                return true;
            }
            return false;
        }
        finally
        {
            if (taken) gate.Exit();
        }
    }

    /// <summary>Copy every live entry of one identity into the operation store (L3 → L2 prefill).</summary>
    internal static void Prefill(in GeometryIdentity identity, ref EvaluationSampleStore store)
    {
        bool taken = false;
        gate.Enter(ref taken);
        try
        {
            var epoch = Volatile.Read(ref modelGeometryEpoch);
            if (arena == null) return;
            for (MemoryPageIndex i = 0; i < Capacity && store.Count < store.Capacity; i++)
            {
                ref var entry = ref arena[i];
                if (entry.Occupied == 0 || entry.Epoch != epoch) continue;
                if (entry.Owner.Tag != identity.Tag || entry.Owner.Generation != identity.Generation) continue;
                if (entry.Source == SampleSourceKind.PredictedOnly) continue;
                entry.ClockReferenced = 1;
                _ = store.TryInsert(ToL2Sample(in entry));
            }
        }
        finally
        {
            if (taken) gate.Exit();
        }
    }

    /// <summary>Publish one validated operation result into the cross-call cache.</summary>
    internal static void Publish(in GeometryIdentity identity, in CurveSample sample)
    {
        bool taken = false;
        gate.Enter(ref taken);
        try
        {
            if (arena == null) return;
            // Duplicate key: keep the better of the two by the same rule as L2.
            var epoch = Volatile.Read(ref modelGeometryEpoch);
            for (MemoryPageIndex i = 0; i < Capacity; i++)
            {
                ref var entry = ref arena[i];
                if (entry.Occupied == 0) continue;
                if (entry.Owner.Tag != identity.Tag || entry.Owner.Generation != identity.Generation) continue;
                if (entry.Parameter != sample.Parameter || entry.Kind != sample.Kind || entry.Side != sample.Side
                    || entry.TerminatorRule != sample.TerminatorRule)
                    continue;
                if (entry.Epoch != epoch)
                {
                    Overwrite(ref entry, identity, epoch, in sample);
                    return;
                }
                var existing = ToL2Sample(in entry);
                if (!CurveSampleQuality.TryImprove(in existing, in sample, out var improved))
                    return;
                Overwrite(ref entry, identity, epoch, in improved);
                return;
            }

            var slot = ClaimSlot();
            Overwrite(ref arena[slot], identity, epoch, in sample);
        }
        finally
        {
            if (taken) gate.Exit();
        }
    }

    // ── CLOCK eviction (§13.9) — caller holds the gate ───────────

    private static MemoryPageIndex ClaimSlot()
    {
        for (MemoryPageIndex sweep = 0; sweep < 2 * Capacity; sweep++)
        {
            ref var entry = ref arena[clockHand];
            var slot = clockHand;
            clockHand = (clockHand + 1) % Capacity;
            if (entry.Occupied == 0) return slot;
            if (entry.ClockReferenced != 0)
            {
                entry.ClockReferenced = 0;
                continue;
            }
            return slot; // evict: entries own no borrowed memory, nothing dangles
        }
        return clockHand; // unreachable with the double sweep; deterministic fallback
    }

    // ── Plumbing ─────────────────────────────────────────────────

    private static void Overwrite(ref CachedCurveSample entry, in GeometryIdentity identity,
        long epoch, in CurveSample sample)
    {
        entry.Owner = identity;
        entry.Epoch = epoch;
        entry.Parameter = sample.Parameter;
        entry.Position = sample.Position;
        entry.First = sample.First;
        entry.Second = sample.Second;
        entry.MaxOrder = sample.MaxOrder;
        entry.Kind = sample.Kind;
        entry.Side = sample.Side;
        entry.Segment = sample.Segment;
        entry.ErrorEstimate = sample.ErrorEstimate;
        entry.RawResidual = sample.RawResidual;
        entry.NonDefiningResidual = sample.NonDefiningResidual;
        entry.TerminatorRule = sample.TerminatorRule;
        entry.Source = sample.Source;
        entry.Plan = sample.Plan;
        entry.Witness = sample.Witness;
        entry.ClockReferenced = 1;
        entry.Occupied = 1;
    }

    private static CurveSample ToL2Sample(in CachedCurveSample entry)
        => new(entry.Parameter, entry.Position, entry.First, entry.Second, entry.MaxOrder,
            entry.Kind, entry.Side, entry.Segment, entry.ErrorEstimate, entry.RawResidual,
            entry.NonDefiningResidual, entry.TerminatorRule,
            entry.Source, entry.Plan, entry.Witness);

}

/// <summary>
/// Cross-call cached icurve evaluation: L3 prefill → operation-level (L2)
/// evaluation with correction → publish of the validated sample (§13.3).
/// </summary>
internal static unsafe partial class KernelRuntime
{
    internal const int IcurveL2SampleBudget = 16;

    internal static AlgorithmStatus EvaluateICurveThroughL3(in GeometryIdentity identity, in ICurveView view,
        double t, DerivativeOrder order, ICurveConstraintPlan plan,
        Span<KernelVector3> derivatives, out ICurveEvalReport report, ChartSide side = ChartSide.Right)
    {
        if (order >= 0 && order <= ICurveEvaluation.MaxDerivativeOrder
            && derivatives.Length > order && double.IsFinite(t)
            && t >= view.ChartParameters[0] && t <= view.ChartParameters[^1])
        {
            var kind = OriginalChartParameterMap.IsChartNode(view.ChartParameters, t)
                ? ICurveQueryKind.ChartPoint
                : ICurveQueryKind.RegularChartInterval;
            var legal = ICurveConstraintPlanRules.ValidateEnumerator(plan);
            if (legal != AlgorithmStatus.Success)
            {
                report = new ICurveEvalReport(kind, legal, plan, side, -1, 0, 0);
                return legal;
            }
            // Defining D0 publishes the original anchor and does not consult
            // plan capability. Every other query still does, before a cache hit.
            var definingPosition = kind == ICurveQueryKind.ChartPoint && order == 0;
            if (!definingPosition)
            {
                var capability = ICurveConstraintPlanRules.ValidateRequest(in view, plan);
                if (capability != AlgorithmStatus.Success)
                {
                    report = new ICurveEvalReport(kind, capability, plan, side, -1, 0, 0);
                    return capability;
                }
            }
            if (GeometryEvaluationCache.TryGetExact(in identity, t, kind, side,
                    order, ICurveEvaluation.CacheQualityBound(in view), out var exact))
            {
                derivatives[0] = exact.Position;
                if (order >= 1) derivatives[1] = exact.First;
                if (order >= 2) derivatives[2] = exact.Second;
                report = new ICurveEvalReport(kind, AlgorithmStatus.Success, exact.Plan,
                    side, exact.Segment, 0, exact.RawResidual, CacheHitKind.Exact,
                    nonDefiningResidual: exact.NonDefiningResidual,
                    qualityError: exact.ErrorEstimate);
                return AlgorithmStatus.Success;
            }
        }

        Span<CurveSample> operationStorage = stackalloc CurveSample[IcurveL2SampleBudget];
        var operationStore = new EvaluationSampleStore(operationStorage);
        GeometryEvaluationCache.Prefill(in identity, ref operationStore);
        var status = ICurveEvaluation.EvaluateWithCache(in view, t, order, plan, ref operationStore,
            derivatives, out report, side);
        if (status == AlgorithmStatus.Success)
            GeometryEvaluationCache.Publish(in identity, new CurveSample(t, derivatives[0],
                order >= 1 ? derivatives[1] : default,
                order >= 2 ? derivatives[2] : default, order, report.Kind,
                report.Side, report.Segment, report.QualityError, report.Residual,
                report.NonDefiningResidual, TerminatorParameterRule.Unresolved,
                SampleSourceKind.CorrectedRoot, report.Plan));
        return status;
    }
}
