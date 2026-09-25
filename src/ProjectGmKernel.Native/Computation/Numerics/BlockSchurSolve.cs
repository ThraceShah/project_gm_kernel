namespace ProjectGmKernel.Native.Computation.Numerics;

using ProjectGmKernel.Native.Runtime;

/// <summary>
/// Block Schur elimination for the nested Newton system (spec §12.3, task
/// T14): outer F(x,z) = 0 (m×n_z outer unknowns x), inner H(x,z) = 0. With
/// H_z stably solvable: H_z W = H_x, H_z b = H, then
/// (F_x − F_z W)Δx = −F + F_z b and Δz = −b − WΔx. The F_z·b term is kept
/// even while the inner block is not fully converged (§12.3), and no explicit
/// inverse is formed. An ill-conditioned H_z returns Singular so the caller
/// can keep the full system (§12.3) instead of padding the pivot.
/// </summary>
internal static class BlockSchurSolve
{
    /// <summary>
    /// Compute the joint Newton step for F ∈ R^m (x-block n_x), H ∈ R^k
    /// (z-block n_z), n_x + n_z ≤ 8 in this small-system slice.
    /// Layout: fX (m·n_x), fZ (m·n_z), hX (k·n_x), hZ (k·n_z) row-major,
    /// f (m), h (k). The step is (Δx, Δz) concatenated.
    /// </summary>
    internal static AlgorithmStatus ComputeStep(ReadOnlySpan<double> fX, ReadOnlySpan<double> fZ,
        ReadOnlySpan<double> hX, ReadOnlySpan<double> hZ, ReadOnlySpan<double> f, ReadOnlySpan<double> h,
        BufferCount outerCount, BufferCount outerUnknowns, BufferCount innerUnknowns,
        Span<double> schurWorkspace, Span<double> step, out double innerResidualScale)
    {
        innerResidualScale = 0;
        var m = outerCount;
        var nx = outerUnknowns;
        var nz = innerUnknowns;
        if (nx + nz > 8 || nz <= 0 || nx <= 0 || m != nx) return AlgorithmStatus.InvalidInput;
        if (fX.Length < m * nx || fZ.Length < m * nz || hX.Length < nz * nx || hZ.Length < nz * nz
            || f.Length < m || h.Length < nz || step.Length < nx + nz)
            return AlgorithmStatus.InvalidInput;
        if (schurWorkspace.Length < nx * nx + nx + nx * nz + nz) return AlgorithmStatus.WorkspaceTooSmall;

        for (BufferOffset i = 0; i < m * nx; i++) if (!double.IsFinite(fX[i])) return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < m * nz; i++) if (!double.IsFinite(fZ[i])) return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < nz * nx; i++) if (!double.IsFinite(hX[i])) return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < nz * nz; i++) if (!double.IsFinite(hZ[i])) return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < m; i++) if (!double.IsFinite(f[i])) return AlgorithmStatus.InvalidInput;
        for (BufferOffset i = 0; i < nz; i++) if (!double.IsFinite(h[i])) return AlgorithmStatus.InvalidInput;

        // Workspace layout: schur (n_x²), rhs (n_x), w (n_x·n_z), b (n_z).
        var schur = schurWorkspace[..(nx * nx)];
        var rhs = schurWorkspace.Slice(nx * nx, nx);
        var w = schurWorkspace.Slice(nx * nx + nx, nx * nz);
        var b = schurWorkspace.Slice(nx * nx + nx + nx * nz, nz);

        // b = H_z⁻¹ H, W = H_z⁻¹ H_x via one LU factorization of H_z.
        Span<double> hzCopy = stackalloc double[64];
        Span<int> pivots = stackalloc int[8];
        for (BufferOffset i = 0; i < nz * nz; i++)
            hzCopy[i] = hZ[i];
        var factorStatus = SmallLinearSolve.LuFactorize(hzCopy, nz, pivots);
        if (factorStatus != AlgorithmStatus.Success) return factorStatus; // singular H_z: keep the full system

        Span<double> solveScratch = stackalloc double[8];
        for (BufferOffset j = 0; j < nz; j++) b[j] = h[j];
        var solveStatus = SmallLinearSolve.LuSolveInPlace(hzCopy, nz, pivots, b);
        if (solveStatus != AlgorithmStatus.Success) return solveStatus;
        for (BufferOffset col = 0; col < nx; col++)
        {
            for (BufferOffset i = 0; i < nz; i++) solveScratch[i] = hX[i * nx + col];
            solveStatus = SmallLinearSolve.LuSolveInPlace(hzCopy, nz, pivots, solveScratch);
            if (solveStatus != AlgorithmStatus.Success) return solveStatus;
            for (BufferOffset i = 0; i < nz; i++) w[i * nx + col] = solveScratch[i];
        }

        // Schur block S = F_x − F_z W and right side −F + F_z b.
        for (BufferOffset i = 0; i < m; i++)
            for (BufferOffset j = 0; j < nx; j++)
            {
                var value = fX[i * nx + j];
                for (BufferOffset l = 0; l < nz; l++)
                    value -= fZ[i * nz + l] * w[l * nx + j];
                schur[i * nx + j] = value;
            }
        for (BufferOffset i = 0; i < m; i++)
        {
            var value = -f[i];
            for (BufferOffset l = 0; l < nz; l++)
                value += fZ[i * nz + l] * b[l];
            rhs[i] = value;
        }

        // Solve S Δx = rhs with a fresh LU of the Schur block.
        Span<double> schurCopy = stackalloc double[64];
        schur[..(nx * nx)].CopyTo(schurCopy);
        var schurStatus = SmallLinearSolve.LuFactorize(schurCopy, nx, pivots);
        if (schurStatus != AlgorithmStatus.Success) return schurStatus;
        Span<double> deltaX = stackalloc double[8];
        for (BufferOffset i = 0; i < nx; i++) deltaX[i] = rhs[i];
        schurStatus = SmallLinearSolve.LuSolveInPlace(schurCopy, nx, pivots, deltaX);
        if (schurStatus != AlgorithmStatus.Success) return schurStatus;

        Span<double> tempStep = stackalloc double[8];
        for (BufferOffset i = 0; i < nx; i++)
        {
            if (!double.IsFinite(deltaX[i])) return AlgorithmStatus.NumericalFailure;
            tempStep[i] = deltaX[i];
        }

        // Δz = −b − W Δx with the *unconverged* b (never dropped, §12.3).
        for (BufferOffset l = 0; l < nz; l++)
        {
            var value = -b[l];
            for (BufferOffset j = 0; j < nx; j++)
                value -= w[l * nx + j] * deltaX[j];
            if (!double.IsFinite(value)) return AlgorithmStatus.NumericalFailure;
            tempStep[nx + l] = value;
        }

        // Sensitivity of the outer residual to the inner residual (§15.1):
        // ‖F_z H_z⁻¹ H‖ in the same scaled units the caller compares against.
        var innerContribution = 0.0;
        for (BufferOffset i = 0; i < m; i++)
        {
            var value = 0.0;
            for (BufferOffset l = 0; l < nz; l++)
                value += fZ[i * nz + l] * b[l];
            innerContribution = Math.Max(innerContribution, Math.Abs(value));
        }
        if (!double.IsFinite(innerContribution)) return AlgorithmStatus.NumericalFailure;

        for (BufferOffset i = 0; i < nx + nz; i++)
            step[i] = tempStep[i];
        innerResidualScale = innerContribution;
        return AlgorithmStatus.Success;
    }
}
