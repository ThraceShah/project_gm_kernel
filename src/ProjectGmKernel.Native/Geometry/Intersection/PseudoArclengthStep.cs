using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Internal pseudo-arclength augmentation for ill-conditioned fixed-t Newton
/// (spec §17.4). Never redefines the public <c>icurve(t)</c> — callers must
/// restore the native parameter via the chord-plane residual after the step.
/// </summary>
internal static class PseudoArclengthStep
{
    /// <summary>
    /// Given J (n×n) and residual r for the defining system including the
    /// parameter-plane row, replace a near-zero plane row with the null-space
    /// direction of the remaining rows (approximate via last right singular
    /// vector) so the step can leave the plane briefly. The caller then
    /// re-projects onto the plane / restores t.
    /// </summary>
    internal static AlgorithmStatus TryAugmentPlaneRow(Span<double> jacobian, Span<double> residual,
        BufferCount n, BufferOffset planeRow, Span<double> singularValues,
        Span<double> u, Span<double> v, out bool augmented)
    {
        augmented = false;
        if (n <= 1 || planeRow < 0 || planeRow >= n) return AlgorithmStatus.InvalidInput;
        if (jacobian.Length < n * n || residual.Length < n
            || singularValues.Length < n || u.Length < n * n || v.Length < n * n)
            return AlgorithmStatus.WorkspaceTooSmall;

        // Detect ill-conditioned plane contribution: row nearly zero.
        var rowNorm = 0.0;
        for (BufferOffset j = 0; j < n; j++)
            rowNorm += jacobian[planeRow * n + j] * jacobian[planeRow * n + j];
        if (rowNorm > 1e-20) return AlgorithmStatus.Success; // well-conditioned; no augment

        Span<double> copy = stackalloc double[TrustRegionStep.MaxSmallSystem * TrustRegionStep.MaxSmallSystem];
        jacobian[..(n * n)].CopyTo(copy);
        // Zero the plane row for the SVD of the geometric block.
        for (BufferOffset j = 0; j < n; j++) copy[planeRow * n + j] = 0;
        var status = SmallLinearSolve.SvdFactorizeSquare(copy, n, singularValues, u, v,
            SmallLinearSolve.MachineEpsilon * n, out var rank);
        if (status != AlgorithmStatus.Success) return status;
        if (rank >= n) return AlgorithmStatus.Success;

        // Last column of V ≈ null direction; install as the plane row and
        // set residual so the step advances along that direction.
        for (BufferOffset j = 0; j < n; j++)
            jacobian[planeRow * n + j] = v[j * n + (n - 1)];
        residual[planeRow] = 0;
        augmented = true;
        return AlgorithmStatus.Success;
    }
}
