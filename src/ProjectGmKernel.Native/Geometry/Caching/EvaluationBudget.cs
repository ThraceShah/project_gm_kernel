using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Shared base-geometry evaluation budget for one top-level request
/// (spec §14.7, §15, task T17). Nested correctors, continuation steps and
/// local subdivision draw from the same counter — each layer must not own a
/// private 4096. Exhaustion is <see cref="AlgorithmStatus.NotConverged"/>
/// with a BudgetExceeded detail on the evaluation report, never Unsupported.
/// </summary>
internal struct EvaluationBudget
{
    /// <summary>Starting shared budget for one outer evaluation (§14.7).</summary>
    internal const int DefaultMaxBaseEvaluations = 4096;

    internal int Remaining;
    internal int Used;
    internal int Max;

    internal static EvaluationBudget Default => new(DefaultMaxBaseEvaluations);

    /// <summary>
    /// Nested accuracy factor η_k ∈ (0,1] (§15). Starts at 1; callers tighten
    /// toward the outer residual scale when InnerAccuracyInsufficient fires.
    /// </summary>
    internal double InnerAccuracyFactor;

    internal EvaluationBudget(int maxBaseEvaluations)
    {
        Max = maxBaseEvaluations < 0 ? 0 : maxBaseEvaluations;
        Remaining = Max;
        Used = 0;
        InnerAccuracyFactor = 1.0;
    }

    /// <summary>Tighten nested tolerance (never below 1e-3 of current).</summary>
    internal void TightenInner(double factor)
    {
        if (!(factor > 0) || !(factor < 1)) return;
        InnerAccuracyFactor = Math.Max(1e-3, InnerAccuracyFactor * factor);
    }

    /// <summary>
    /// Consume <paramref name="count"/> base evaluations. Returns false when
    /// the remaining budget cannot cover the request; nothing is deducted then.
    /// </summary>
    internal bool TryConsume(int count)
    {
        if (count < 0) return false;
        if (count > Remaining) return false;
        Remaining -= count;
        Used += count;
        return true;
    }

    /// <summary>True when no further base evaluation may run.</summary>
    internal readonly bool IsExhausted => Remaining <= 0;
}
