using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Standalone envelope Newton solvers for constant-radius circular-spine
/// blends (spec §10.4, task T13). Exposes 4×4 (x,s) and 3×3 (ξ₁,ξ₂,s) systems
/// without wiring into <see cref="ICurveConstraintPlan"/> / Auto — production
/// plan selection stays analytic-only until GATE-A evidence lands.
/// </summary>
internal static class BlendEnvelopeSolve
{
    internal const int MaxNewtonIterations = 16;
    internal const double ResidualTolerance = 1e-12;

    /// <summary>
    /// Solve (E₁−r², E₂, φ_A, p) = 0 in R⁴ with state (x.X, x.Y, x.Z, s).
    /// Circular spine in the XY plane: C(s)=(R cos s, R sin s, 0).
    /// </summary>
    internal static AlgorithmStatus SolveFourByFour(
        double spineRadius, double tubeRadius,
        in AnalyticSurface outerSurface,
        in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        Span<double> state, out BufferOffset iterations, out double residual)
    {
        iterations = 0;
        residual = 0;
        if (state.Length < 4) return AlgorithmStatus.WorkspaceTooSmall;
        if (!(spineRadius > 0) || !(tubeRadius > 0)) return AlgorithmStatus.InvalidInput;
        if (!IsFinite(planeAnchor) || !IsFinite(planeNormal)) return AlgorithmStatus.InvalidInput;

        Span<double> f = stackalloc double[4];
        Span<double> j = stackalloc double[16];
        Span<double> jCopy = stackalloc double[16];
        Span<double> step = stackalloc double[4];
        Span<double> model = stackalloc double[4];
        Span<int> pivots = stackalloc int[4];

        for (BufferOffset iter = 0; iter < MaxNewtonIterations; iter++)
        {
            iterations = iter + 1;
            var assemble = AssembleFourByFour(spineRadius, tubeRadius, in outerSurface,
                in planeAnchor, in planeNormal, state, f, j);
            if (assemble != AlgorithmStatus.Success) return assemble;
            residual = MaxAbs(f);
            if (residual <= ResidualTolerance) return AlgorithmStatus.Success;

            var status = NewtonStep.ComputeStep(j, jCopy, f, 4, pivots, model, step, out var predicted);
            if (status != AlgorithmStatus.Success || !(predicted > 0))
                return status == AlgorithmStatus.Success ? AlgorithmStatus.NotConverged : status;
            for (BufferOffset i = 0; i < 4; i++)
            {
                state[i] += step[i];
                if (!double.IsFinite(state[i])) return AlgorithmStatus.NumericalFailure;
            }
        }
        residual = MaxAbs(f);
        return AlgorithmStatus.NotConverged;
    }

    /// <summary>
    /// Solve the chord-plane reduced 3×3 (ξ₁,ξ₂,s) with x = Q + ξ₁ u + ξ₂ v,
    /// where (u,v) span the plane orthogonal to <paramref name="planeNormal"/>.
    /// </summary>
    internal static AlgorithmStatus SolveThreeByThree(
        double spineRadius, double tubeRadius,
        in AnalyticSurface outerSurface,
        in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        in KernelVector3 basisU, in KernelVector3 basisV,
        Span<double> state, out BufferOffset iterations, out double residual)
    {
        // state = (ξ1, ξ2, s); p residual is identically zero by construction.
        iterations = 0;
        residual = 0;
        if (state.Length < 3) return AlgorithmStatus.WorkspaceTooSmall;
        if (!(spineRadius > 0) || !(tubeRadius > 0)) return AlgorithmStatus.InvalidInput;

        Span<double> f = stackalloc double[3];
        Span<double> j = stackalloc double[9];
        Span<double> jCopy = stackalloc double[9];
        Span<double> step = stackalloc double[3];
        Span<double> model = stackalloc double[3];
        Span<int> pivots = stackalloc int[3];

        for (BufferOffset iter = 0; iter < MaxNewtonIterations; iter++)
        {
            iterations = iter + 1;
            if (AssembleThreeByThree(spineRadius, tubeRadius, in outerSurface,
                    in planeAnchor, in basisU, in basisV, state, f, j) != AlgorithmStatus.Success)
                return AlgorithmStatus.Unsupported;
            residual = MaxAbs(f);
            if (residual <= ResidualTolerance) return AlgorithmStatus.Success;

            var status = NewtonStep.ComputeStep(j, jCopy, f, 3, pivots, model, step, out var predicted);
            if (status != AlgorithmStatus.Success || !(predicted > 0))
                return status == AlgorithmStatus.Success ? AlgorithmStatus.NotConverged : status;
            for (BufferOffset i = 0; i < 3; i++)
            {
                state[i] += step[i];
                if (!double.IsFinite(state[i])) return AlgorithmStatus.NumericalFailure;
            }
        }
        residual = MaxAbs(f);
        return AlgorithmStatus.NotConverged;
    }

    internal static AlgorithmStatus AssembleFourByFour(
        double spineRadius, double tubeRadius,
        in AnalyticSurface outerSurface,
        in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        ReadOnlySpan<double> state, Span<double> residual, Span<double> jacobian)
    {
        if (state.Length < 4 || residual.Length < 4 || jacobian.Length < 16)
            return AlgorithmStatus.WorkspaceTooSmall;
        var x = Vector(state[0], state[1], state[2]);
        var s = state[3];
        var (sinS, cosS) = Math.SinCos(s);
        var spine = Vector(spineRadius * cosS, spineRadius * sinS, 0);
        var spineD1 = Vector(-spineRadius * sinS, spineRadius * cosS, 0);
        var spineD2 = Vector(-spineRadius * cosS, -spineRadius * sinS, 0);
        var q = Sub(x, spine);

        BlendImplicitEvaluation.AssembleEnvelope(in q, in spineD1, in spineD2,
            out var e1, out var e2, out var e1x, out var e1s, out var e2x, out var e2s);
        if (AnalyticImplicitEvaluation.Evaluate(in outerSurface, in x, 1, out var jetA)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        residual[0] = e1 - tubeRadius * tubeRadius;
        residual[1] = e2;
        residual[2] = jetA.Value;
        residual[3] = Dot(planeNormal, Sub(x, planeAnchor));

        // Row 0: ∇_x E1 = e1x, ∂_s E1 = e1s
        jacobian[0] = e1x.X; jacobian[1] = e1x.Y; jacobian[2] = e1x.Z; jacobian[3] = e1s;
        jacobian[4] = e2x.X; jacobian[5] = e2x.Y; jacobian[6] = e2x.Z; jacobian[7] = e2s;
        jacobian[8] = jetA.Gradient.X; jacobian[9] = jetA.Gradient.Y;
        jacobian[10] = jetA.Gradient.Z; jacobian[11] = 0;
        jacobian[12] = planeNormal.X; jacobian[13] = planeNormal.Y;
        jacobian[14] = planeNormal.Z; jacobian[15] = 0;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus AssembleThreeByThree(
        double spineRadius, double tubeRadius,
        in AnalyticSurface outerSurface,
        in KernelVector3 planeAnchor,
        in KernelVector3 basisU, in KernelVector3 basisV,
        ReadOnlySpan<double> state, Span<double> residual, Span<double> jacobian)
    {
        var x = Add(planeAnchor, Add(Scale(basisU, state[0]), Scale(basisV, state[1])));
        var s = state[2];
        var (sinS, cosS) = Math.SinCos(s);
        var spine = Vector(spineRadius * cosS, spineRadius * sinS, 0);
        var spineD1 = Vector(-spineRadius * sinS, spineRadius * cosS, 0);
        var spineD2 = Vector(-spineRadius * cosS, -spineRadius * sinS, 0);
        var q = Sub(x, spine);

        BlendImplicitEvaluation.AssembleEnvelope(in q, in spineD1, in spineD2,
            out var e1, out var e2, out var e1x, out var e1s, out var e2x, out var e2s);
        if (AnalyticImplicitEvaluation.Evaluate(in outerSurface, in x, 1, out var jetA)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        residual[0] = e1 - tubeRadius * tubeRadius;
        residual[1] = e2;
        residual[2] = jetA.Value;

        // Chain rule: ∂/∂ξ_i = ∇_x · basis_i
        jacobian[0] = Dot(e1x, basisU); jacobian[1] = Dot(e1x, basisV); jacobian[2] = e1s;
        jacobian[3] = Dot(e2x, basisU); jacobian[4] = Dot(e2x, basisV); jacobian[5] = e2s;
        jacobian[6] = Dot(jetA.Gradient, basisU);
        jacobian[7] = Dot(jetA.Gradient, basisV);
        jacobian[8] = 0;
        return AlgorithmStatus.Success;
    }

    private static double MaxAbs(ReadOnlySpan<double> values)
    {
        var max = 0.0;
        for (BufferOffset i = 0; i < values.Length; i++)
        {
            var v = values[i];
            if (!double.IsFinite(v)) return double.PositiveInfinity;
            var a = Math.Abs(v);
            if (a > max) max = a;
        }
        return max;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
