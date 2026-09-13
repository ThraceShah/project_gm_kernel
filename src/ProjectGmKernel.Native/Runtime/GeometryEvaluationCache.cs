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
    internal SampleSourceKind Source;
    internal ICurveConstraintPlan Plan;
    internal byte ClockReferenced;
    internal byte Occupied;
}

/// <summary>
/// Cross-call geometry evaluation cache (spec §13.3 layer L3, §13.8–§13.9,
/// task T10): session-owned native arena outside the scratch and return
/// allocators, hard capacity with CLOCK eviction, and a monotonic model
/// geometry epoch — creation, deletion, rollback and session reset invalidate
/// every entry; cache writes never move the epoch.
/// </summary>
internal static unsafe class GeometryEvaluationCache
{
    /// <summary>Hard sample budget of the slice arena (§13.9 bounded arena).</summary>
    internal const int Capacity = 256;

    private static CachedCurveSample* arena;
    private static MemoryPageIndex clockHand;

    /// <summary>
    /// Monotonic model geometry epoch (§13.8): bumped by geometry creation,
    /// deletion, rollback and session lifecycle; never by cache writes.
    /// </summary>
    internal static long ModelGeometryEpoch { get; private set; }

    internal static void BumpModelGeometryEpoch() => ModelGeometryEpoch++;

    // ── Session lifetime ─────────────────────────────────────────
    // The arena lives in one long-lived SessionMemory allocation — outside
    // CommandScratch, the return arena and the variable-length block
    // allocator (§13.9) — and is released when the session stops.

    internal static void Attach(SessionMemory* memory)
    {
        Detach(memory);
        if (memory == null) return;
        var block = memory->TryAllocate((nuint)(Capacity * sizeof(CachedCurveSample)));
        if (block == null) return; // cache disabled for this session, evaluation still works
        arena = (CachedCurveSample*)block;
        Clear();
    }

    internal static void Detach(SessionMemory* memory)
    {
        if (arena != null && memory != null)
            memory->Free(arena);
        arena = null;
        clockHand = 0;
    }

    internal static void Clear()
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
    /// error bound must all match.
    /// </summary>
    internal static bool TryGetExact(in GeometryIdentity identity, double parameter, ICurveQueryKind kind,
        ChartSide side, DerivativeOrder minOrder, double maxError, out CurveSample sample)
    {
        sample = default;
        var epoch = ModelGeometryEpoch;
        if (arena == null) return false;
        for (MemoryPageIndex i = 0; i < Capacity; i++)
        {
            ref var entry = ref arena[i];
            if (entry.Occupied == 0) continue;
            if (entry.Epoch != epoch) continue; // stale: dropped lazily, never served
            if (entry.Owner.Tag != identity.Tag || entry.Owner.Generation != identity.Generation) continue;
            if (entry.Parameter != parameter) continue;
            if (entry.Kind != kind || entry.Side != side) continue;
            if (entry.Source == SampleSourceKind.PredictedOnly) continue;
            if (entry.MaxOrder < minOrder || entry.ErrorEstimate > maxError) continue;
            entry.ClockReferenced = 1;
            sample = ToL2Sample(in entry);
            return true;
        }
        return false;
    }

    /// <summary>Copy every live entry of one identity into the operation store (L3 → L2 prefill).</summary>
    internal static void Prefill(in GeometryIdentity identity, ref EvaluationSampleStore store)
    {
        var epoch = ModelGeometryEpoch;
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

    /// <summary>Publish one validated operation result into the cross-call cache.</summary>
    internal static void Publish(in GeometryIdentity identity, in CurveSample sample)
    {
        if (arena == null) return;
        // Duplicate key: keep the better of the two by the same rule as L2.
        var epoch = ModelGeometryEpoch;
        for (MemoryPageIndex i = 0; i < Capacity; i++)
        {
            ref var entry = ref arena[i];
            if (entry.Occupied == 0) continue;
            if (entry.Owner.Tag != identity.Tag || entry.Owner.Generation != identity.Generation) continue;
            if (entry.Parameter != sample.Parameter || entry.Kind != sample.Kind || entry.Side != sample.Side)
                continue;
            if (RankOf(sample.Source) <= RankOf(entry.Source) && sample.ErrorEstimate >= entry.ErrorEstimate)
                return;
            Overwrite(ref entry, identity, epoch, in sample);
            return;
        }

        var slot = ClaimSlot();
        Overwrite(ref arena[slot], identity, epoch, in sample);
    }

    // ── CLOCK eviction (§13.9) ───────────────────────────────────

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
        entry.Source = sample.Source;
        entry.Plan = sample.Plan;
        entry.ClockReferenced = 1;
        entry.Occupied = 1;
    }

    private static CurveSample ToL2Sample(in CachedCurveSample entry)
        => new(entry.Parameter, entry.Position, entry.First, entry.Second, entry.MaxOrder,
            entry.Kind, entry.Side, entry.Segment, entry.ErrorEstimate, entry.Source, entry.Plan);

    private static int RankOf(SampleSourceKind source) => source switch
    {
        SampleSourceKind.ImportedChartAnchor => 2,
        SampleSourceKind.CorrectedRoot => 1,
        _ => 0,
    };
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
        Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        Span<CurveSample> operationStorage = stackalloc CurveSample[IcurveL2SampleBudget];
        var operationStore = new EvaluationSampleStore(operationStorage);
        GeometryEvaluationCache.Prefill(in identity, ref operationStore);
        var status = ICurveEvaluation.EvaluateWithCache(in view, t, order, plan, ref operationStore,
            derivatives, out report);
        if (status == AlgorithmStatus.Success)
            GeometryEvaluationCache.Publish(in identity, new CurveSample(t, derivatives[0],
                derivatives[1], order >= 2 ? derivatives[2] : default, order, report.Kind,
                report.Side, report.Segment, report.Residual, SampleSourceKind.CorrectedRoot, report.Plan));
        return status;
    }
}
