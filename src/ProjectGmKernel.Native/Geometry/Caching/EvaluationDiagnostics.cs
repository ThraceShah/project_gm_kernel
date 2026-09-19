using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Optional request-scoped diagnostics (FailureReplay + counters). Callers
/// attach before Evaluate and read after — never written from residual I/O.
/// </summary>
internal struct EvaluationDiagnostics
{
    internal FailureReplayRing Replay;
    internal EvaluationCounters Counters;
    internal EvaluationBudget Budget;

    internal static EvaluationDiagnostics CreateDefault()
        => new()
        {
            Replay = default,
            Counters = default,
            Budget = EvaluationBudget.Default,
        };

    internal void RecordFailure(ICurveConstraintPlan plan, double t, BufferOffset segment,
        AlgorithmStatus status, double residual, ICurveEvalDetail detail)
        => Replay.Record(plan, t, segment, status, residual, detail);
}
