namespace ProjectGmKernel.Native.Computation.Numerics;

using ProjectGmKernel.Native.Runtime;

/// <summary>
/// Newton correction step with Armijo backtracking support (spec §14.3).
/// Pure span computation: residual and Jacobian evaluation stay with the
/// statically dispatched caller, so no residual callback crosses this module.
/// The step being small or the least-squares problem stationary is never
/// treated as root success here — that judgement belongs to validation.
/// </summary>
internal static class NewtonStep
{
    internal const double ArmijoCoefficient = 1e-4;
    internal const double MinStepFraction = 1.0 / 64.0;

    /// <summary>
    /// Solve the scaled Newton system J·p = −r into <paramref name="step"/>.
    /// <paramref name="jacobian"/> (n×n row-major) is consumed by the
    /// factorization; <paramref name="jacobianCopy"/> (length ≥ n²) receives
    /// the preserved original for the J·p model product, and
    /// <paramref name="modelWorkspace"/> (length ≥ n) receives that product.
    /// <paramref name="predictedReduction"/> is the decrease predicted for
    /// ½‖r‖² by the linear model; nonpositive values mean the direction is
    /// not a descent direction and the caller must fall back to a
    /// trust-region step.
    /// </summary>
    internal static AlgorithmStatus ComputeStep(Span<double> jacobian, Span<double> jacobianCopy,
        ReadOnlySpan<double> residual, BufferCount n, Span<int> pivotWorkspace, Span<double> modelWorkspace,
        Span<double> step, out double predictedReduction)
    {
        predictedReduction = 0;
        if (step.Length < n || pivotWorkspace.Length < n || modelWorkspace.Length < n
            || jacobianCopy.Length < n * n)
            return AlgorithmStatus.WorkspaceTooSmall;
        var residualNormSq = SmallLinearSolve.Dot(residual, residual);
        if (!double.IsFinite(residualNormSq)) return AlgorithmStatus.InvalidInput;
        if (residualNormSq == 0)
        {
            for (BufferOffset i = 0; i < n; i++) step[i] = 0;
            return AlgorithmStatus.Success;
        }

        jacobian[..(n * n)].CopyTo(jacobianCopy);
        for (BufferOffset i = 0; i < n; i++) step[i] = -residual[i];
        var status = SmallLinearSolve.LuFactorize(jacobian, n, pivotWorkspace);
        if (status != AlgorithmStatus.Success) return status;
        status = SmallLinearSolve.LuSolveInPlace(jacobian, n, pivotWorkspace, step);
        if (status != AlgorithmStatus.Success) return status;

        // With an exact solve ‖r + J p‖ = 0; evaluate it on the preserved
        // original so rounding on near-singular systems degrades the
        // prediction instead of hiding it.
        SmallLinearSolve.Multiply(jacobianCopy, n, n, step, modelWorkspace);
        var linearNormSq = 0.0;
        for (BufferOffset i = 0; i < n; i++)
        {
            var rPlusJp = residual[i] + modelWorkspace[i];
            linearNormSq += rPlusJp * rPlusJp;
        }
        predictedReduction = 0.5 * (residualNormSq - linearNormSq);
        return double.IsFinite(predictedReduction) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// Armijo condition for Ψ(y + α p) ≤ Ψ(y) + c₁ α gᵀp with the exact
    /// Newton directional derivative gᵀp = −‖r‖². The caller passes the
    /// already evaluated trial objective; no re-evaluation happens here.
    /// </summary>
    internal static bool ArmijoSatisfied(double psiBase, double psiTrial, double alpha, double residualNormSq)
        => psiTrial <= psiBase - ArmijoCoefficient * alpha * residualNormSq;

    /// <summary>Clamp a trial fraction of the full Newton step into the allowed window.</summary>
    internal static double BacktrackAlpha(double alpha)
        => alpha * 0.5 < MinStepFraction ? 0 : alpha * 0.5;
}
