using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Bounded trust-region corrector over the regular constraint plans
/// (spec §14.3–§14.6, §13.3 layer L1, tasks T09 completion + T14 support).
/// The fast full-Newton paths stay the first attempt; this module is the
/// accept/reject driven fallback: every step is a dogleg trial evaluated in
/// the trial buffer of a <see cref="SolveStateBuffer"/>, committed only when
/// the reduction ratio accepts it, and rolled back bit-exactly otherwise.
/// A trial that leaves a valid parameter domain is a rejected step, never a
/// failed solve (§14.3). Residual/Jacobian evaluation is a finite switch over
/// the plans — no residual callbacks cross this boundary (§19.4).
/// </summary>
internal static class ICurveCorrection
{
    /// <summary>Accepted-iteration budget of the trust-region phase (§14.7).</summary>
    internal const int MaxAcceptedIterations = 20;
    /// <summary>Separate trial budget; rejections must not multiply accepted cost unchecked.</summary>
    internal const int MaxTrials = 48;

    /// <summary>
    /// Refine one plan's root state from <paramref name="seed"/>. On success
    /// the refined state (plan dimension, see <paramref name="refinedState"/>)
    /// satisfies the defining residuals at <see cref="ICurveEvaluation.ResidualTolerance"/>
    /// relative to the local length scale. State layouts: I1 → μ; I2 → (ξ₁,ξ₂)
    /// in the fixed in-plane basis through Q(t); I3 → (x,y,z); P2 → (u,v) of
    /// support0; P4 → (u₀,v₀,u₁,v₁).
    /// </summary>
    internal static AlgorithmStatus Refine(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, in KernelVector3 seed,
        Span<double> refinedState, out BufferOffset acceptedIterations, out double residual)
        => Refine(in view, plan, t, segment, in seed, refinedState, out acceptedIterations,
            out residual, out _);

    /// <summary>
    /// Refine with locatable detail: Stagnation on reverse-oscillation / radius
    /// floor; PlanSwitched is reported by the caller after <see cref="RefineWithPlanSwitch"/>.
    /// </summary>
    internal static AlgorithmStatus Refine(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, in KernelVector3 seed,
        Span<double> refinedState, out BufferOffset acceptedIterations, out double residual,
        out ICurveEvalDetail detail)
    {
        var budget = EvaluationBudget.Default;
        return Refine(in view, plan, t, segment, in seed, refinedState, ref budget,
            out acceptedIterations, out residual, out detail);
    }

    /// <summary>
    /// Refine with shared budget: when predicted descent is dominated by the
    /// nested accuracy floor η_k, tighten inner and retry before shrinking the
    /// trust radius (§15.2–15.3).
    /// </summary>
    internal static AlgorithmStatus Refine(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, in KernelVector3 seed,
        Span<double> refinedState, ref EvaluationBudget budget,
        out BufferOffset acceptedIterations, out double residual, out ICurveEvalDetail detail)
    {
        acceptedIterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        var n = StateDimension(plan);
        if (n <= 0 || refinedState.Length < n) return AlgorithmStatus.InvalidInput;
        if (!double.IsFinite(t)) return AlgorithmStatus.InvalidInput;

        Span<double> state = stackalloc double[MaxSmallSystem];
        var initStatus = InitState(in view, plan, t, segment, in seed, state);
        if (initStatus != AlgorithmStatus.Success) return initStatus;

        Span<double> jacobianMaster = stackalloc double[MaxSmallSystem * MaxSmallSystem];
        Span<double> residualVector = stackalloc double[MaxSmallSystem];
        var memo = new ResidualMemo();
        memo.BeginTrial();
        if (!budget.TryConsume(1))
        {
            detail = ICurveEvalDetail.BudgetExceeded;
            return AlgorithmStatus.NotConverged;
        }
        var evalStatus = EvaluateSystem(in view, plan, t, segment, state,
            residualVector, jacobianMaster, out var acceptedPoint);
        if (evalStatus != AlgorithmStatus.Success) return evalStatus;
        _ = memo.TryInsert(plan, t, segment, 0, residualVector[..n]);
        residual = SmallLinearSolve.Norm(residualVector);
        if (ICurveEvaluation.IsPublishableRoot(in view, t, segment, in acceptedPoint))
            return Success(state, n, refinedState);

        var freeze = new FrozenResidualScale();
        freeze.Capture(residualVector[..n], jacobianMaster[..(n * n)], n);
        freeze.Apply(residualVector[..n], jacobianMaster[..(n * n)]);

        var scale0 = Math.Max(1.0, SmallLinearSolve.Norm(state));
        var buffer = new SolveStateBuffer(
            stackalloc double[MaxSmallSystem], stackalloc double[MaxSmallSystem],
            state, SmallLinearSolve.Norm(state) + 1);
        var minRadius = 1e-12 * scale0;

        Span<double> jacobianScratch = stackalloc double[MaxSmallSystem * MaxSmallSystem];
        Span<double> jacobianCopy = stackalloc double[MaxSmallSystem * MaxSmallSystem];
        Span<int> pivotWorkspace = stackalloc int[MaxSmallSystem];
        Span<int> columnPivotWorkspace = stackalloc int[MaxSmallSystem];
        Span<double> tauWorkspace = stackalloc double[MaxSmallSystem];
        Span<double> gradient = stackalloc double[MaxSmallSystem];
        Span<double> newtonStep = stackalloc double[MaxSmallSystem];
        Span<double> step = stackalloc double[MaxSmallSystem];

        var psiBase = 0.5 * SmallLinearSolve.Dot(residualVector[..n], residualVector[..n]);
        var trials = 0;
        var consecutiveRejects = 0;
        var innerTightenUsed = false;
        Span<double> trialResidual = stackalloc double[MaxSmallSystem];
        Span<double> trialJacobian = stackalloc double[MaxSmallSystem * MaxSmallSystem];
        while (acceptedIterations < MaxAcceptedIterations && trials < MaxTrials)
        {
            jacobianMaster[..(n * n)].CopyTo(jacobianScratch);
            var doglegStatus = TrustRegionStep.DoglegStep(jacobianScratch, jacobianCopy,
                residualVector, n, buffer.AcceptedRadius, pivotWorkspace, tauWorkspace,
                columnPivotWorkspace, gradient, newtonStep, step, out var predicted);
            if (doglegStatus != AlgorithmStatus.Success)
            {
                detail = doglegStatus == AlgorithmStatus.Singular
                    ? ICurveEvalDetail.ParameterizationSingular
                    : ICurveEvalDetail.None;
                return doglegStatus;
            }
            if (!(predicted > 0))
            {
                detail = ICurveEvalDetail.Stagnation;
                return AlgorithmStatus.Singular;
            }

            // §15: if predicted descent is below the nested accuracy floor, tighten
            // η_k once before treating the model as stagnant / shrinking radius.
            var innerFloor = psiBase * budget.InnerAccuracyFactor * 1e-4;
            if (!innerTightenUsed && predicted <= innerFloor && psiBase > 0)
            {
                budget.TightenInner(0.5);
                detail = ICurveEvalDetail.InnerAccuracyInsufficient;
                innerTightenUsed = true;
                // Re-evaluate the accepted residual at the tighter nested demand
                // without consuming a reject / radius shrink.
                continue;
            }

            trials++;
            buffer.BeginTrial();
            memo.BeginTrial();
            var trial = buffer.TrialState;
            for (BufferOffset i = 0; i < n; i++) trial[i] += step[i];

            if (!budget.TryConsume(1))
            {
                detail = ICurveEvalDetail.BudgetExceeded;
                return AlgorithmStatus.NotConverged;
            }
            var trialStatus = EvaluateSystem(in view, plan, t, segment, buffer.TrialState,
                trialResidual, trialJacobian, out _);
            var psiTrial = double.PositiveInfinity;
            if (trialStatus == AlgorithmStatus.Success)
            {
                _ = memo.TryInsert(plan, t, segment, 0, trialResidual[..n]);
                freeze.Apply(trialResidual[..n], trialJacobian[..(n * n)]);
                psiTrial = 0.5 * SmallLinearSolve.Dot(trialResidual[..n], trialResidual[..n]);
            }
            var ratio = TrustRegionStep.ReductionRatio(psiBase, psiTrial, predicted);
            var stepNorm = SmallLinearSolve.Norm(step);
            var atBoundary = stepNorm >= buffer.AcceptedRadius * (1 - 1e-12);
            if (ratio >= TrustRegionStep.MinAcceptRatio)
            {
                consecutiveRejects = 0;
                innerTightenUsed = false;
                var nextRadius = TrustRegionStep.UpdateRadius(buffer.AcceptedRadius, ratio,
                    atBoundary, minRadius, double.MaxValue);
                buffer.CommitTrial(nextRadius);
                acceptedIterations++;

                if (!budget.TryConsume(1))
                {
                    detail = ICurveEvalDetail.BudgetExceeded;
                    return AlgorithmStatus.NotConverged;
                }
                evalStatus = EvaluateSystem(in view, plan, t, segment, buffer.AcceptedState,
                    residualVector, jacobianMaster, out acceptedPoint);
                if (evalStatus != AlgorithmStatus.Success) return evalStatus;
                residual = SmallLinearSolve.Norm(residualVector);
                if (ICurveEvaluation.IsPublishableRoot(in view, t, segment, in acceptedPoint))
                    return Success(buffer.AcceptedState, n, refinedState);
                freeze.Capture(residualVector[..n], jacobianMaster[..(n * n)], n);
                freeze.Apply(residualVector[..n], jacobianMaster[..(n * n)]);
                psiBase = 0.5 * SmallLinearSolve.Dot(residualVector[..n], residualVector[..n]);
            }
            else
            {
                consecutiveRejects++;
                var shrunk = TrustRegionStep.UpdateRadius(buffer.AcceptedRadius, ratio,
                    atBoundary, minRadius, double.MaxValue);
                if (shrunk >= buffer.AcceptedRadius && buffer.AcceptedRadius <= minRadius)
                {
                    detail = ICurveEvalDetail.Stagnation;
                    return AlgorithmStatus.NotConverged;
                }
                buffer.RejectTrial(shrunk);
                if (consecutiveRejects >= 4 && shrunk <= minRadius)
                {
                    detail = ICurveEvalDetail.Stagnation;
                    return AlgorithmStatus.NotConverged;
                }
            }
        }
        detail = ICurveEvalDetail.Stagnation;
        return AlgorithmStatus.NotConverged;
    }

    /// <summary>
    /// Try the primary plan, then §7.6 alternates on Singular/Stagnation (§14.6).
    /// </summary>
    internal static AlgorithmStatus RefineWithPlanSwitch(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, in KernelVector3 seed,
        Span<double> refinedState, out ICurveConstraintPlan usedPlan,
        out BufferOffset acceptedIterations, out double residual, out ICurveEvalDetail detail)
    {
        usedPlan = plan;
        var status = Refine(in view, plan, t, segment, in seed, refinedState,
            out acceptedIterations, out residual, out detail);
        if (status == AlgorithmStatus.Success) return status;
        if (status is not (AlgorithmStatus.Singular or AlgorithmStatus.NotConverged))
            return status;

        Span<ICurveConstraintPlan> alternates = stackalloc ICurveConstraintPlan[5];
        var count = ICurveConstraintPlanRules.Alternates(in view, plan, alternates);
        for (BufferOffset i = 0; i < count; i++)
        {
            var alt = alternates[i];
            var altStatus = Refine(in view, alt, t, segment, in seed, refinedState,
                out acceptedIterations, out residual, out detail);
            if (altStatus == AlgorithmStatus.Success)
            {
                usedPlan = alt;
                detail = ICurveEvalDetail.PlanSwitched;
                return AlgorithmStatus.Success;
            }
        }
        return status;
    }

    private static AlgorithmStatus Success(ReadOnlySpan<double> state, int n, Span<double> refinedState)
    {
        state[..n].CopyTo(refinedState);
        return AlgorithmStatus.Success;
    }

    private const int MaxSmallSystem = TrustRegionStep.MaxSmallSystem;

    private static int StateDimension(ICurveConstraintPlan plan) => plan switch
    {
        ICurveConstraintPlan.I1 => 1,
        ICurveConstraintPlan.I2 or ICurveConstraintPlan.P2 => 2,
        ICurveConstraintPlan.I3 => 3,
        ICurveConstraintPlan.P4 => 4,
        _ => 0,
    };

    /// <summary>Deterministic initial state per plan, from the same seed the fast paths use.</summary>
    private static AlgorithmStatus InitState(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, in KernelVector3 seed, Span<double> state)
    {
        switch (plan)
        {
            case ICurveConstraintPlan.I1:
                state[0] = 0; // the projected chord point on the plane∩plane line (§7.5)
                return AlgorithmStatus.Success;
            case ICurveConstraintPlan.I2:
            {
                InPlaneBasis(in view, segment, t, out var u, out var v, out var q);
                var offset = Sub(in seed, in q);
                state[0] = Dot(offset, u);
                state[1] = Dot(offset, v);
                return AlgorithmStatus.Success;
            }
            case ICurveConstraintPlan.I3:
                state[0] = seed.X;
                state[1] = seed.Y;
                state[2] = seed.Z;
                return AlgorithmStatus.Success;
            case ICurveConstraintPlan.P2:
                if (AnalyticParametricEvaluation.TryRecoverWitness(in view.Support0, in seed, out var u0, out var v0)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.Unsupported;
                state[0] = u0;
                state[1] = v0;
                return AlgorithmStatus.Success;
            case ICurveConstraintPlan.P4:
                if (AnalyticParametricEvaluation.TryRecoverWitness(in view.Support0, in seed, out var w0, out var w1)
                        != AlgorithmStatus.Success
                    || AnalyticParametricEvaluation.TryRecoverWitness(in view.Support1, in seed, out var w2, out var w3)
                        != AlgorithmStatus.Success)
                    return AlgorithmStatus.Unsupported;
                state[0] = w0;
                state[1] = w1;
                state[2] = w2;
                state[3] = w3;
                return AlgorithmStatus.Success;
            default:
                return AlgorithmStatus.InvalidInput;
        }
    }

    /// <summary>
    /// Residual and Jacobian of one plan's defining system at a state
    /// (§7.1–§7.5). Returns <see cref="AlgorithmStatus.NotConverged"/> when a
    /// parametric trial left its valid domain — the corrector reads that as a
    /// rejected step — and <see cref="AlgorithmStatus.Unsupported"/> when the
    /// supports lack the plan's capabilities.
    /// </summary>
    private static AlgorithmStatus EvaluateSystem(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, ReadOnlySpan<double> state,
        Span<double> residual, Span<double> jacobian, out KernelVector3 point)
    {
        point = default;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];
        switch (plan)
        {
            case ICurveConstraintPlan.I1:
            {
                var planeIsSupport0 = view.Support0.Kind == SurfaceClass.Plane;
                var planeIsSupport1 = view.Support1.Kind == SurfaceClass.Plane;
                if (planeIsSupport0 == planeIsSupport1) return AlgorithmStatus.Unsupported;
                var n = planeIsSupport0 ? view.Support0.Axis : view.Support1.Axis;
                var c = planeIsSupport0 ? view.Support0.Origin : view.Support1.Origin;
                var other = planeIsSupport0 ? view.Support1 : view.Support0;

                var cross = Cross(n, chordUnit);
                var crossNormSq = Dot(cross, cross);
                if (!(crossNormSq > 1e-24)) return AlgorithmStatus.Singular;
                var b = Scale(cross, 1 / Math.Sqrt(crossNormSq));
                var nTilde = Sub(n, Scale(chordUnit, Dot(n, chordUnit)));

                ChordPoint(in view, segment, t, out var q);
                var alpha = Dot(n, Sub(c, q)) / crossNormSq;
                point = Add(Add(q, Scale(nTilde, alpha)), Scale(b, state[0]));
                if (AnalyticImplicitEvaluation.Evaluate(in other, in point, 1, out var jet)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.Unsupported;
                residual[0] = jet.Value;
                jacobian[0] = Dot(jet.Gradient, b);
                return AlgorithmStatus.Success;
            }
            case ICurveConstraintPlan.I2:
            {
                InPlaneBasis(in view, segment, t, out var u, out var v, out var q);
                point = Add(q, Add(Scale(u, state[0]), Scale(v, state[1])));
                if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in point, 1, out var jet0)
                    != AlgorithmStatus.Success
                    || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in point, 1, out var jet1)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.Unsupported;
                residual[0] = jet0.Value;
                residual[1] = jet1.Value;
                jacobian[0] = Dot(jet0.Gradient, u);
                jacobian[1] = Dot(jet0.Gradient, v);
                jacobian[2] = Dot(jet1.Gradient, u);
                jacobian[3] = Dot(jet1.Gradient, v);
                return AlgorithmStatus.Success;
            }
            case ICurveConstraintPlan.I3:
            {
                point = Vector(state[0], state[1], state[2]);
                if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in point, 1, out var jet0)
                    != AlgorithmStatus.Success
                    || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in point, 1, out var jet1)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.Unsupported;
                residual[0] = jet0.Value;
                residual[1] = jet1.Value;
                residual[2] = OriginalChartParameterMap.PlaneResidual(
                    view.ChartPositions, view.ChartParameters, view.ChartScales,
                    view.ChartChordUnits, segment, t, in point);
                jacobian[0] = jet0.Gradient.X; jacobian[1] = jet0.Gradient.Y; jacobian[2] = jet0.Gradient.Z;
                jacobian[3] = jet1.Gradient.X; jacobian[4] = jet1.Gradient.Y; jacobian[5] = jet1.Gradient.Z;
                jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;
                return AlgorithmStatus.Success;
            }
            case ICurveConstraintPlan.P2:
            {
                Span<KernelVector3> jet = stackalloc KernelVector3[4]; // layout(1,1): S, Su, Sv, Suv
                if (!SurfaceDerivativeLayout.TryCreate(1, 1, out var layout))
                    return AlgorithmStatus.InvalidInput;
                if (SurfaceEvaluation.Evaluate(in view.Support0, state[0], state[1], in layout, jet)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.NotConverged; // trial left the valid parameter domain
                point = jet[0];
                if (AnalyticImplicitEvaluation.Evaluate(in view.Support1, in jet[0], 1, out var jet1)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.Unsupported;
                residual[0] = jet1.Value;
                residual[1] = OriginalChartParameterMap.PlaneResidual(
                    view.ChartPositions, view.ChartParameters, view.ChartScales,
                    view.ChartChordUnits, segment, t, in jet[0]);
                var su = jet[layout.GetIndex(1, 0)];
                var sv = jet[layout.GetIndex(0, 1)];
                jacobian[0] = Dot(jet1.Gradient, su);
                jacobian[1] = Dot(jet1.Gradient, sv);
                jacobian[2] = Dot(chordUnit, su);
                jacobian[3] = Dot(chordUnit, sv);
                return AlgorithmStatus.Success;
            }
            case ICurveConstraintPlan.P4:
            {
                Span<KernelVector3> jet0 = stackalloc KernelVector3[4];
                Span<KernelVector3> jet1 = stackalloc KernelVector3[4];
                if (!SurfaceDerivativeLayout.TryCreate(1, 1, out var layout))
                    return AlgorithmStatus.InvalidInput;
                if (SurfaceEvaluation.Evaluate(in view.Support0, state[0], state[1], in layout, jet0)
                    != AlgorithmStatus.Success
                    || SurfaceEvaluation.Evaluate(in view.Support1, state[2], state[3], in layout, jet1)
                    != AlgorithmStatus.Success)
                    return AlgorithmStatus.NotConverged; // trial left a valid parameter domain
                point = jet0[0];
                residual[0] = jet0[0].X - jet1[0].X;
                residual[1] = jet0[0].Y - jet1[0].Y;
                residual[2] = jet0[0].Z - jet1[0].Z;
                residual[3] = OriginalChartParameterMap.PlaneResidual(
                    view.ChartPositions, view.ChartParameters, view.ChartScales,
                    view.ChartChordUnits, segment, t, in jet0[0]);
                var su0 = jet0[layout.GetIndex(1, 0)];
                var sv0 = jet0[layout.GetIndex(0, 1)];
                var su1 = jet1[layout.GetIndex(1, 0)];
                var sv1 = jet1[layout.GetIndex(0, 1)];
                jacobian[0] = su0.X; jacobian[1] = sv0.X; jacobian[2] = -su1.X; jacobian[3] = -sv1.X;
                jacobian[4] = su0.Y; jacobian[5] = sv0.Y; jacobian[6] = -su1.Y; jacobian[7] = -sv1.Y;
                jacobian[8] = su0.Z; jacobian[9] = sv0.Z; jacobian[10] = -su1.Z; jacobian[11] = -sv1.Z;
                jacobian[12] = Dot(chordUnit, su0); jacobian[13] = Dot(chordUnit, sv0);
                jacobian[14] = 0; jacobian[15] = 0;
                return AlgorithmStatus.Success;
            }
            default:
                return AlgorithmStatus.InvalidInput;
        }
    }

    private static void ChordPoint(in ICurveView view, BufferOffset segment, double t, out KernelVector3 q)
    {
        var next = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
        var lambda = (t - view.ChartParameters[segment]) / next;
        q = Add(
            Scale(view.ChartPositions[segment], 1 - lambda),
            Scale(view.ChartPositions[segment + 1], lambda));
    }

    /// <summary>Fixed in-plane orthonormal basis through Q(t) (§7.4); identical construction in fast path and here.</summary>
    private static void InPlaneBasis(in ICurveView view, BufferOffset segment, double t,
        out KernelVector3 u, out KernelVector3 v, out KernelVector3 q)
    {
        var chordUnit = view.ChartChordUnits[segment];
        var axis = LeastParallelAxis(in chordUnit);
        u = Unit(Sub(axis, Scale(chordUnit, Dot(axis, chordUnit))));
        v = Cross(chordUnit, u);
        ChordPoint(in view, segment, t, out q);
    }

    private static KernelVector3 LeastParallelAxis(in KernelVector3 direction)
    {
        var ax = Math.Abs(direction.X);
        var ay = Math.Abs(direction.Y);
        var az = Math.Abs(direction.Z);
        if (ax <= ay && ax <= az) return Vector(1, 0, 0);
        return ay <= az ? Vector(0, 1, 0) : Vector(0, 0, 1);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
