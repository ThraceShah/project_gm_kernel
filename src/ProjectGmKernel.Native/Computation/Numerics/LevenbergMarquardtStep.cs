using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Computation.Numerics;

/// <summary>
/// Levenberg–Marquardt step for scaled square systems (spec §14.5). Augments
/// the normal equations with λ·diag(JᵀJ) via QR on the stacked [J; √λ I]
/// system. A small step with large residual is never treated as success —
/// that judgement stays with the caller.
/// </summary>
internal static class LevenbergMarquardtStep
{
    internal const int MaxSmallSystem = TrustRegionStep.MaxSmallSystem;

    /// <summary>
    /// Solve min ‖[J; √λ I] p + [r; 0]‖ for p. <paramref name="jacobian"/> is
    /// preserved via <paramref name="jacobianCopy"/>. Workspaces: tau/columnPivot
    /// length ≥ n; step length ≥ n; stacked length ≥ (n+n)*n for the augmented
    /// QR storage when using the explicit path below.
    /// </summary>
    internal static AlgorithmStatus ComputeStep(ReadOnlySpan<double> jacobian,
        Span<double> jacobianCopy, ReadOnlySpan<double> residual, BufferCount n,
        double lambda, Span<int> columnPivotWorkspace, Span<double> tauWorkspace,
        Span<double> step, Span<double> stackedWorkspace, out double predictedReduction)
    {
        predictedReduction = 0;
        if (n > MaxSmallSystem || n <= 0) return AlgorithmStatus.WorkspaceTooSmall;
        if (!(lambda >= 0) || !double.IsFinite(lambda)) return AlgorithmStatus.InvalidInput;
        if (jacobian.Length < n * n || jacobianCopy.Length < n * n || residual.Length < n
            || step.Length < n || columnPivotWorkspace.Length < n || tauWorkspace.Length < n
            || stackedWorkspace.Length < (n + n) * n)
            return AlgorithmStatus.WorkspaceTooSmall;

        var residualNormSq = SmallLinearSolve.Dot(residual, residual);
        if (!double.IsFinite(residualNormSq)) return AlgorithmStatus.InvalidInput;
        if (residualNormSq == 0)
        {
            for (BufferOffset i = 0; i < n; i++) step[i] = 0;
            return AlgorithmStatus.Success;
        }

        jacobian[..(n * n)].CopyTo(jacobianCopy);
        // Stack [J; √λ I] into (2n)×n row-major.
        var m = n + n;
        var sqrtLambda = Math.Sqrt(lambda);
        for (BufferOffset i = 0; i < n; i++)
        {
            for (BufferOffset j = 0; j < n; j++)
                stackedWorkspace[i * n + j] = jacobianCopy[i * n + j];
        }
        for (BufferOffset i = 0; i < n; i++)
        {
            for (BufferOffset j = 0; j < n; j++)
                stackedWorkspace[(n + i) * n + j] = i == j ? sqrtLambda : 0;
        }

        Span<double> rhs = stackalloc double[MaxSmallSystem * 2];
        for (BufferOffset i = 0; i < n; i++) rhs[i] = -residual[i];
        for (BufferOffset i = 0; i < n; i++) rhs[n + i] = 0;

        var status = SmallLinearSolve.QrFactorize(stackedWorkspace, m, n, tauWorkspace,
            columnPivotWorkspace, SmallLinearSolve.MachineEpsilon * n, out var rank);
        if (status != AlgorithmStatus.Success) return status;
        if (rank < n) return AlgorithmStatus.Singular;

        status = SmallLinearSolve.QrLeastSquares(stackedWorkspace, m, n, tauWorkspace, rank,
            columnPivotWorkspace, rhs, step);
        if (status != AlgorithmStatus.Success) return status;

        Span<double> model = stackalloc double[MaxSmallSystem];
        SmallLinearSolve.Multiply(jacobianCopy, n, n, step, model);
        var linearNormSq = 0.0;
        for (BufferOffset i = 0; i < n; i++)
        {
            var rPlusJp = residual[i] + model[i];
            linearNormSq += rPlusJp * rPlusJp;
        }
        predictedReduction = 0.5 * (residualNormSq - linearNormSq);
        return AlgorithmStatus.Success;
    }
}
