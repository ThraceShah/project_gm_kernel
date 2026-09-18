namespace ProjectGmKernel.Native.Computation.Numerics;

using ProjectGmKernel.Native.Runtime;

/// <summary>
/// Dogleg trust-region step for scaled square systems (spec §14.4). Pure span
/// computation; the caller owns residual/Jacobian evaluation, radius updates
/// on accepted steps and all branch/domain guards. Degenerate gradients or
/// rank-deficient Jacobians return status instead of producing NaN steps.
/// Small fixed stack buffers cover systems up to <see cref="MaxSmallSystem"/>
/// unknowns (spec §4.4); larger joint systems route through the explicit
/// workspace path in a later task.
/// </summary>
internal static class TrustRegionStep
{
    /// <summary>ρ below this rejects the step.</summary>
    internal const double MinAcceptRatio = 0.1;
    /// <summary>ρ below this shrinks the radius.</summary>
    internal const double ShrinkRatio = 0.25;
    /// <summary>ρ above this with a boundary step grows the radius.</summary>
    internal const double GrowRatio = 0.75;
    internal const int MaxSmallSystem = 16;

    /// <summary>
    /// Compute the dogleg step p for J·p = −r within radius
    /// <paramref name="radius"/>. <paramref name="jacobian"/> (n×n row-major)
    /// is consumed: <paramref name="jacobianCopy"/> (length ≥ n²) receives the
    /// preserved original for J·v products while the input storage is
    /// destroyed by the QR factorization. Remaining workspaces must each hold
    /// n doubles (tau/columnPivot hold n; gradient/newton/step hold n).
    /// <paramref name="predictedReduction"/> reports ½‖r‖² − ½‖r + J p‖².
    /// </summary>
    internal static AlgorithmStatus DoglegStep(Span<double> jacobian, Span<double> jacobianCopy,
        ReadOnlySpan<double> residual, BufferCount n, double radius, Span<int> pivotWorkspace,
        Span<double> tauWorkspace, Span<int> columnPivotWorkspace, Span<double> gradient,
        Span<double> newtonStep, Span<double> step, out double predictedReduction)
    {
        predictedReduction = 0;
        if (!(radius > 0) || !double.IsFinite(radius)) return AlgorithmStatus.InvalidInput;
        if (n > MaxSmallSystem) return AlgorithmStatus.WorkspaceTooSmall;
        if (jacobianCopy.Length < n * n || pivotWorkspace.Length < n || tauWorkspace.Length < n
            || columnPivotWorkspace.Length < n || gradient.Length < n || newtonStep.Length < n
            || step.Length < n)
            return AlgorithmStatus.WorkspaceTooSmall;

        var residualNorm = SmallLinearSolve.Norm(residual);
        if (!double.IsFinite(residualNorm)) return AlgorithmStatus.InvalidInput;
        if (residualNorm == 0)
        {
            for (BufferOffset i = 0; i < n; i++) step[i] = 0;
            return AlgorithmStatus.Success;
        }

        // Preserve J: g = Jᵀr and every model product below need the original.
        Span<double> product = stackalloc double[MaxSmallSystem];
        jacobian[..(n * n)].CopyTo(jacobianCopy);
        SmallLinearSolve.MultiplyTransposed(jacobianCopy, n, n, residual, gradient);
        var gradientNorm = SmallLinearSolve.Norm(gradient);
        if (!double.IsFinite(gradientNorm)) return AlgorithmStatus.NumericalFailure;
        if (gradientNorm == 0) return AlgorithmStatus.Singular;

        var status = SmallLinearSolve.QrFactorize(jacobian, n, n, tauWorkspace, columnPivotWorkspace,
            SmallLinearSolve.MachineEpsilon * n, out var rank);
        if (status != AlgorithmStatus.Success) return status;

        Span<double> rhs = stackalloc double[MaxSmallSystem];
        for (BufferOffset i = 0; i < n; i++) rhs[i] = -residual[i];
        // Full-rank: pivoted-QR basic solution. Rank-deficient: true min-norm via SVD (§14.2).
        if (rank < n)
        {
            Span<double> sigma = stackalloc double[MaxSmallSystem];
            Span<double> u = stackalloc double[MaxSmallSystem * MaxSmallSystem];
            Span<double> v = stackalloc double[MaxSmallSystem * MaxSmallSystem];
            status = SmallLinearSolve.SvdFactorizeSquare(jacobianCopy, n, sigma, u, v,
                SmallLinearSolve.MachineEpsilon * n, out var svdRank);
            if (status != AlgorithmStatus.Success) return status;
            status = SmallLinearSolve.SvdMinNormSolve(sigma, u, v, n, svdRank, rhs, newtonStep);
        }
        else
            status = SmallLinearSolve.QrLeastSquares(jacobian, n, n, tauWorkspace, rank, columnPivotWorkspace, rhs, newtonStep);
        if (status != AlgorithmStatus.Success) return status;

        // Cauchy point pC = −(gᵀg / ‖Jg‖²) g against the preserved J.
        SmallLinearSolve.Multiply(jacobianCopy, n, n, gradient, product);
        var jgNormSq = SmallLinearSolve.Dot(product, product);
        if (!double.IsFinite(jgNormSq)) return AlgorithmStatus.NumericalFailure;
        if (jgNormSq <= 0) return AlgorithmStatus.Singular;
        var gradientNormSq = SmallLinearSolve.Dot(gradient, gradient);
        var cauchyScale = -gradientNormSq / jgNormSq;
        var cauchyNorm = -cauchyScale * gradientNorm;
        var newtonNorm = SmallLinearSolve.Norm(newtonStep);

        if (newtonNorm <= radius)
        {
            for (BufferOffset i = 0; i < n; i++) step[i] = newtonStep[i];
        }
        else if (cauchyNorm >= radius)
        {
            var scale = -radius / gradientNorm;
            for (BufferOffset i = 0; i < n; i++) step[i] = scale * gradient[i];
        }
        else
        {
            // p = pC + β (pN − pC) with ‖p‖ = radius, β ∈ (0, 1].
            var dNormSq = 0.0;
            var cauchyDotD = 0.0;
            var cauchyNormSq = cauchyNorm * cauchyNorm;
            for (BufferOffset i = 0; i < n; i++)
            {
                var cauchy = cauchyScale * gradient[i];
                var delta = newtonStep[i] - cauchy;
                dNormSq += delta * delta;
                cauchyDotD += cauchy * delta;
            }
            var discriminant = cauchyDotD * cauchyDotD - dNormSq * (cauchyNormSq - radius * radius);
            if (!(dNormSq > 0) || discriminant < 0) return AlgorithmStatus.Singular;
            var beta = (-cauchyDotD + Math.Sqrt(discriminant)) / dNormSq;
            if (beta > 1) beta = 1;
            for (BufferOffset i = 0; i < n; i++)
            {
                var cauchy = cauchyScale * gradient[i];
                step[i] = cauchy + beta * (newtonStep[i] - cauchy);
            }
        }

        SmallLinearSolve.Multiply(jacobianCopy, n, n, step, product);
        var linearNormSq = 0.0;
        for (BufferOffset i = 0; i < n; i++)
        {
            var rPlusJp = residual[i] + product[i];
            linearNormSq += rPlusJp * rPlusJp;
        }
        predictedReduction = 0.5 * (residualNorm * residualNorm - linearNormSq);
        return double.IsFinite(predictedReduction) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// Actual / predicted reduction ratio for Ψ = ½‖r‖². Nonpositive predicted
    /// decrease yields −1 so the caller rebuilds the model or shrinks the step
    /// (never NaN).
    /// </summary>
    internal static double ReductionRatio(double psiBase, double psiTrial, double predictedReduction)
    {
        if (!(predictedReduction > 0)) return -1;
        return (psiBase - psiTrial) / predictedReduction;
    }

    /// <summary>Radius update per the spec's starting policy (§14.4), clamped to [min, max].</summary>
    internal static double UpdateRadius(double radius, double ratio, bool stepAtBoundary, double minRadius, double maxRadius)
    {
        if (ratio < ShrinkRatio) radius *= 0.25;
        else if (ratio > GrowRatio && stepAtBoundary) radius = radius * 2 <= maxRadius ? radius * 2 : maxRadius;
        return radius < minRadius ? minRadius : radius;
    }
}
