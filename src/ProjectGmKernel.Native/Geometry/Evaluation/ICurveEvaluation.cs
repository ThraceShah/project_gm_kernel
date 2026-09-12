using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Icurve evaluation, regular-interval vertical slice (spec §7.3, §16.1–16.2,
/// task T07): the implicit/implicit plan I3 over the original chart, plus the
/// ChartPoint and domain-exit contracts. The two-parameter plans (P4/P2/I2)
/// and the terminator path attach to the same query classification in later
/// tasks; GATE-T keeps terminator intervals out of this slice.
///
/// Semantics are fixed before any solving: an exact chart node returns the
/// original point (§5.4), regular intervals solve
///   F(x,t) = [φ₀(x), φ₁(x), p(x,t)] = 0
/// with the chord-plane residual from the prepared map, and everything outside
/// [t₀, t_{m−1}] is refused instead of clamped (§5.4, §9.5).
/// </summary>
internal static class ICurveEvaluation
{
    /// <summary>Fast Newton budget per seed (spec §14.7 starting configuration).</summary>
    internal const int MaxFastNewtonIterations = 8;
    /// <summary>Convergence target relative to the local length scale.</summary>
    internal const double ResidualTolerance = 1e-13;

    /// <summary>
    /// Evaluate position and, requested, the derivatives of the defined branch
    /// at native parameter t. Output is written only when every requested
    /// order validated — a failed D2 never publishes a partial D0 (§18.5).
    /// </summary>
    internal static AlgorithmStatus Evaluate(in ICurveView view, double t, DerivativeOrder order,
        Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, AlgorithmStatus.NotRun, -1, 0, 0);
        if (order < 0 || order > 2) return AlgorithmStatus.InvalidInput;
        if (derivatives.Length <= order || derivatives.Length < 1) return AlgorithmStatus.OutputTooSmall;
        if (!double.IsFinite(t)) return AlgorithmStatus.InvalidInput;

        var parameters = view.ChartParameters;
        if (t < parameters[0] || t > parameters[^1])
        {
            report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, AlgorithmStatus.InvalidInput, -1, 0, 0);
            return AlgorithmStatus.InvalidInput;
        }

        // Exact chart node: the original point is the answer (§5.4); nearby
        // parameters are never snapped here.
        if (OriginalChartParameterMap.IsChartNode(parameters, t))
            return EvaluateChartPoint(in view, t, order, derivatives, out report);

        return EvaluateRegularInterval(in view, t, order, derivatives, out report);
    }

    /// <summary>Chart node contract: exact position, one-sided derivatives, no averaging.</summary>
    private static AlgorithmStatus EvaluateChartPoint(in ICurveView view, double t, DerivativeOrder order,
        Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        // At an interior node the right side is the published derivative side;
        // LocateSegment's side argument keeps the two sides distinguishable.
        var locateStatus = OriginalChartParameterMap.LocateSegment(view.ChartParameters, t, ChartSide.Right, out var segment);
        if (locateStatus != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, locateStatus, -1, 0, 0);
            return locateStatus;
        }

        // The node position is the defining data; solve nothing.
        var position = view.ChartPositions[segment];
        if (order == 0)
        {
            derivatives[0] = position;
            report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, AlgorithmStatus.Success, segment, 0, 0);
            return AlgorithmStatus.Success;
        }

        // Derivatives reuse the regular-interval linearization, evaluated at
        // the node with the chosen side's chord data. Start the Newton from
        // the exact node position; it converges without moving the point.
        var solveStatus = SolveWithDerivatives(in view, in position, t, segment, order, derivatives,
            out var iterations, out var residual);
        report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, solveStatus, segment, iterations, residual);
        if (solveStatus != AlgorithmStatus.Success) return solveStatus;
        derivatives[0] = position; // the published D0 stays the original anchor regardless of the linearization
        return AlgorithmStatus.Success;
    }

    /// <summary>Regular interval: I3 Newton from the chord interpolation, then chained derivatives.</summary>
    private static AlgorithmStatus EvaluateRegularInterval(in ICurveView view, double t, DerivativeOrder order,
        Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        var locateStatus = OriginalChartParameterMap.LocateSegment(view.ChartParameters, t, ChartSide.Right, out var segment);
        if (locateStatus != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, locateStatus, -1, 0, 0);
            return locateStatus;
        }

        // Seed: the chord point Q(t) — inside the parameter plane by construction.
        var lambda = (t - view.ChartParameters[segment]) / (view.ChartParameters[segment + 1] - view.ChartParameters[segment]);
        var seed = Add(
            Scale(view.ChartPositions[segment], 1 - lambda),
            Scale(view.ChartPositions[segment + 1], lambda));

        var status = SolveWithDerivatives(in view, in seed, t, segment, order, derivatives,
            out var iterations, out var residual);
        report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, status, segment, iterations, residual);
        return status;
    }

    /// <summary>
    /// I3 Newton for [φ₀, φ₁, p] and, on success, D1/D2 through the reused
    /// root factorization (§16.1: one decomposition, several right sides).
    /// Implicit jets come from the analytic module with its sheet guards.
    /// </summary>
    private static AlgorithmStatus SolveWithDerivatives(in ICurveView view, in KernelVector3 seed,
        double t, BufferOffset segment, DerivativeOrder order,
        Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual)
    {
        iterations = 0;
        residual = 0;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];
        var anchor = view.ChartPositions[segment];

        Span<double> x = stackalloc double[3] { seed.X, seed.Y, seed.Z };
        Span<double> jacobian = stackalloc double[9];
        Span<double> jacobianCopy = stackalloc double[9];
        Span<double> residualVector = stackalloc double[3];
        Span<double> step = stackalloc double[3];
        Span<double> model = stackalloc double[3];
        Span<int> pivots = stackalloc int[3];

        var gradient0 = default(KernelVector3);
        var gradient1 = default(KernelVector3);
        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxFastNewtonIterations; iteration++)
        {
            iterations = iteration + 1;
            var point = Vector(x[0], x[1], x[2]);
            if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in point, 1, out var jet0) != AlgorithmStatus.Success
                || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in point, 1, out var jet1) != AlgorithmStatus.Success)
                return AlgorithmStatus.Unsupported;
            gradient0 = jet0.Gradient;
            gradient1 = jet1.Gradient;
            residualVector[0] = jet0.Value;
            residualVector[1] = jet1.Value;
            residualVector[2] = OriginalChartParameterMap.PlaneResidual(
                view.ChartPositions, view.ChartParameters, view.ChartScales, view.ChartChordUnits, segment, t, in point);

            jacobian[0] = gradient0.X; jacobian[1] = gradient0.Y; jacobian[2] = gradient0.Z;
            jacobian[3] = gradient1.X; jacobian[4] = gradient1.Y; jacobian[5] = gradient1.Z;
            jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;

            residual = SmallLinearSolve.Norm(residualVector);
            var lengthScale = Math.Max(1.0, SmallLinearSolve.Norm(x));
            if (residual <= ResidualTolerance * lengthScale)
            {
                converged = true;
                break;
            }

            var status = NewtonStep.ComputeStep(jacobian, jacobianCopy, residualVector, 3, pivots, model, step, out var predicted);
            if (status != AlgorithmStatus.Success || !(predicted > 0))
                return status == AlgorithmStatus.Success ? AlgorithmStatus.Singular : status;
            x[0] += step[0];
            x[1] += step[1];
            x[2] += step[2];
            if (!double.IsFinite(x[0]) || !double.IsFinite(x[1]) || !double.IsFinite(x[2]))
                return AlgorithmStatus.NumericalFailure;
        }
        if (!converged)
            return AlgorithmStatus.NotConverged;

        var root = Vector(x[0], x[1], x[2]);

        // Rebuild the true root Jacobian once, then serve every derivative
        // right side from this decomposition (§14.2).
        if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in root, order >= 2 ? 2 : 1, out var rootJet0) != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in root, order >= 2 ? 2 : 1, out var rootJet1) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        jacobian[0] = rootJet0.Gradient.X; jacobian[1] = rootJet0.Gradient.Y; jacobian[2] = rootJet0.Gradient.Z;
        jacobian[3] = rootJet1.Gradient.X; jacobian[4] = rootJet1.Gradient.Y; jacobian[5] = rootJet1.Gradient.Z;
        jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;
        if (SmallLinearSolve.LuFactorize(jacobian, 3, pivots) != AlgorithmStatus.Success)
            return AlgorithmStatus.Singular;

        // D1: J x′ = [0, 0, 1/f] (§16.2, I3 right side).
        Span<double> d1 = stackalloc double[3];
        d1[0] = 0; d1[1] = 0; d1[2] = 1 / scale;
        if (SmallLinearSolve.LuSolveInPlace(jacobian, 3, pivots, d1) != AlgorithmStatus.Success)
            return AlgorithmStatus.NumericalFailure;
        derivatives[0] = root;
        derivatives[1] = Vector(d1[0], d1[1], d1[2]);
        if (!IsFinite(derivatives[1])) return AlgorithmStatus.NumericalFailure;

        if (order >= 2)
        {
            // D2: J x″ = −[x′ᵀH₀x′, x′ᵀH₁x′, 0] (§16.2).
            Span<double> d2 = stackalloc double[3];
            d2[0] = -(rootJet0.Hxx * d1[0] * d1[0] + 2 * rootJet0.Hxy * d1[0] * d1[1]
                + 2 * rootJet0.Hxz * d1[0] * d1[2] + rootJet0.Hyy * d1[1] * d1[1]
                + 2 * rootJet0.Hyz * d1[1] * d1[2] + rootJet0.Hzz * d1[2] * d1[2]);
            d2[1] = -(rootJet1.Hxx * d1[0] * d1[0] + 2 * rootJet1.Hxy * d1[0] * d1[1]
                + 2 * rootJet1.Hxz * d1[0] * d1[2] + rootJet1.Hyy * d1[1] * d1[1]
                + 2 * rootJet1.Hyz * d1[1] * d1[2] + rootJet1.Hzz * d1[2] * d1[2]);
            d2[2] = 0;
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 3, pivots, d2) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[2] = Vector(d2[0], d2[1], d2[2]);
            if (!IsFinite(derivatives[2])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }
}
