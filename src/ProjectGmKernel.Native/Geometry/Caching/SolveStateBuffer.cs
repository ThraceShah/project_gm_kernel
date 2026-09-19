using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Solve-state layer L1 (spec §13.3, task T09 completion): trial/accepted
/// double buffering for one corrector state — the accepted unknown vector and
/// trust radius survive rejected trials bit-exactly, so a rejected step can
/// roll back without re-deriving anything (§14.3 "拒绝步恢复全部状态").
/// Both buffers are caller-owned spans; nothing is allocated and no state
/// escapes the owning solve. The accepted buffers are only ever written by
/// <see cref="CommitTrial"/> — a trial that was never committed cannot
/// pollute them by construction.
/// </summary>
internal ref struct SolveStateBuffer
{
    private Span<double> accepted;
    private Span<double> trial;
    private double acceptedRadius;
    private double trialRadius;

    /// <summary>
    /// Adopt the two storage spans and the initial accepted state. Each span
    /// must hold at least <paramref name="initialState"/>.Length doubles —
    /// callers pass fixed-size stackalloc workspaces (§4.4).
    /// </summary>
    internal SolveStateBuffer(Span<double> acceptedStorage, Span<double> trialStorage,
        ReadOnlySpan<double> initialState, double initialRadius)
    {
        if (acceptedStorage.Length < initialState.Length || trialStorage.Length < initialState.Length)
        {
            accepted = trial = default;
            Dimension = 0;
            acceptedRadius = trialRadius = 0;
            return;
        }
        accepted = acceptedStorage;
        trial = trialStorage;
        Dimension = initialState.Length;
        initialState.CopyTo(accepted);
        acceptedRadius = initialRadius;
        trialRadius = initialRadius;
    }

    internal readonly int Dimension;
    internal readonly bool IsValid => Dimension > 0;

    /// <summary>The accepted unknown vector — read-only by contract (§13.3).</summary>
    internal readonly Span<double> AcceptedState => accepted;

    /// <summary>The trial workspace, valid between <see cref="BeginTrial"/> and the next commit/rollback.</summary>
    internal readonly Span<double> TrialState => trial;

    internal readonly double AcceptedRadius => acceptedRadius;

    /// <summary>Start a trial: copy the accepted state into the trial buffer.</summary>
    internal void BeginTrial()
    {
        accepted[..Dimension].CopyTo(trial);
        trialRadius = acceptedRadius;
    }

    /// <summary>Promote the trial state to accepted; the trial radius travels with it.</summary>
    internal void CommitTrial(double newRadius)
    {
        trial[..Dimension].CopyTo(accepted);
        acceptedRadius = newRadius;
    }

    /// <summary>
    /// Reject the trial state while retaining the updated control radius for
    /// the next step. Geometry state rolls back bit-exactly; trust-region
    /// control state intentionally does not.
    /// </summary>
    internal void RejectTrial(double newRadius)
    {
        acceptedRadius = newRadius;
        trialRadius = newRadius;
    }
}
