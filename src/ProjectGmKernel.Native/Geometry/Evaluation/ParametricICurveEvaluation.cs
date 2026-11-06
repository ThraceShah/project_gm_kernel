using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Regular intersections with a B-surface support. The accepted state always
/// contains both support UV pairs; a mixed P2 step only changes the B-surface
/// pair, while P4 changes all four parameters. Original chart anchors select
/// the local branch, and bounded continuation advances the original t.
/// </summary>
internal static class ParametricICurveEvaluation
{
    private const BufferCount MaxCorrections = 24;
    private const BufferCount MaxLineSearch = 14;
    private const BufferCount MaxContinuationSteps = 64;
    private const double MinimumRelativeStep = 1e-12;

    internal static AlgorithmStatus EvaluateWithCache(scoped in ICurveView view,
        double t, DerivativeOrder order, ICurveConstraintPlan plan,
        TerminatorParameterRule rule, ChartSide side,
        scoped ref EvaluationSampleStore cache, scoped Span<KernelVector3> derivatives,
        out ICurveEvalReport report)
    {
        report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain,
            AlgorithmStatus.InvalidInput, plan, side, -1, 0, 0);
        if (order < 0 || order > ICurveEvaluation.MaxDerivativeOrder || !double.IsFinite(t)
            || side is not (ChartSide.Left or ChartSide.Right)) return AlgorithmStatus.InvalidInput;
        if (derivatives.Length <= order) return AlgorithmStatus.OutputTooSmall;
        var parameters = view.ChartParameters;
        if (!view.HasBSurface || parameters.Length < 2
            || view.ChartPositions.Length != parameters.Length
            || view.ChartScales.Length < parameters.Length - 1
            || view.ChartChordUnits.Length < parameters.Length - 1)
            return AlgorithmStatus.InvalidInput;

        if (t < parameters[0] || t > parameters[^1])
        {
            var terminator = t < parameters[0] ? view.HasStartTerminator : view.HasEndTerminator;
            if (!terminator) return AlgorithmStatus.InvalidInput;
            var kind = t < parameters[0] ? ICurveQueryKind.StartTerminatorInterval : ICurveQueryKind.EndTerminatorInterval;
            report = new ICurveEvalReport(kind, AlgorithmStatus.Unsupported, plan,
                side, -1, 0, 0, detail: ICurveEvalDetail.CompatibilityGateOpen);
            return AlgorithmStatus.Unsupported;
        }
        var locate = OriginalChartParameterMap.LocateSegment(parameters, t, side, out var segment);
        if (locate != AlgorithmStatus.Success) return locate;
        var node = -1;
        for (BufferOffset i = 0; i < parameters.Length; i++)
            if (parameters[i] == t) { node = i; break; }
        var query = node >= 0 ? ICurveQueryKind.ChartPoint : ICurveQueryKind.RegularChartInterval;
        var legal = ICurveConstraintPlanRules.ValidateEnumerator(plan);
        if (legal != AlgorithmStatus.Success)
            return FinishFailure(query, legal, plan, side, segment, 0, 0, ICurveEvalDetail.None, out report);

        // Defining D0 does not require a derivative or a constraint plan.
        if (node >= 0 && order == 0)
        {
            var point = view.ChartPositions[node];
            if (!IsFinite(point))
                return FinishFailure(query, AlgorithmStatus.NumericalFailure, plan, side,
                    segment, 0, 0, ICurveEvalDetail.InvalidChart, out report);
            var witness = node < view.ChartWitnesses.Length ? view.ChartWitnesses[node] : SampleWitness.None;
            _ = cache.TryInsert(new CurveSample(t, point, default, default, 0, query,
                side, segment, 0, 0, SampleSourceKind.ImportedChartAnchor, plan, in witness),
                separatePlans: true, evictWhenFull: true);
            derivatives[0] = point;
            report = new ICurveEvalReport(query, AlgorithmStatus.Success, plan, side,
                segment, 0, 0, CacheHitKind.Exact, qualityError: 0);
            return AlgorithmStatus.Success;
        }

        var selected = plan == ICurveConstraintPlan.Auto
            ? (view.Surface0.IsBSurface && view.Surface1.IsBSurface ? ICurveConstraintPlan.P4 : ICurveConstraintPlan.P2)
            : plan;
        if (selected != ICurveConstraintPlan.P4
            && (selected != ICurveConstraintPlan.P2 || view.Surface0.IsBSurface == view.Surface1.IsBSurface))
            return FinishFailure(query, AlgorithmStatus.Unsupported, selected, side,
                segment, 0, 0, ICurveEvalDetail.None, out report);
        var lengthScale = Math.Max(1.0, Math.Max(view.Surface0.LengthScale, view.Surface1.LengthScale));
        var tolerance = ICurveEvaluation.ResidualTolerance * lengthScale;
        if (!double.IsFinite(lengthScale) || !(view.ChartScales[segment] > 0)
            || !double.IsFinite(view.ChartScales[segment])
            || view.ChartWitnesses.Length != parameters.Length)
            return FinishFailure(query, AlgorithmStatus.InvalidInput, selected, side,
                segment, 0, 0, ICurveEvalDetail.InvalidChart, out report);

        if (cache.TryFindExact(t, query, side, order, ICurveEvaluation.CacheQualityFactor * lengthScale,
                out var hit, plan: plan)
            && (plan == ICurveConstraintPlan.Auto || hit.Plan == selected)
            && hit.Plan is ICurveConstraintPlan.P2 or ICurveConstraintPlan.P4
            && double.IsFinite(hit.ErrorEstimate) && hit.ErrorEstimate >= 0
            && IsFinite(hit.Position) && (order < 1 || IsFinite(hit.First)) && (order < 2 || IsFinite(hit.Second)))
        {
            derivatives[0] = node >= 0 ? view.ChartPositions[node] : hit.Position;
            if (order >= 1) derivatives[1] = hit.First;
            if (order >= 2) derivatives[2] = hit.Second;
            report = new ICurveEvalReport(query, AlgorithmStatus.Success, hit.Plan,
                side, segment, 0, hit.RawResidual, CacheHitKind.Exact, qualityError: hit.ErrorEstimate);
            return AlgorithmStatus.Success;
        }

        var budget = EvaluationBudget.Default;
        Span<KernelVector3> result = stackalloc KernelVector3[3];
        Span<double> state = stackalloc double[4];
        var anchor = node >= 0 ? node : segment;
        var status = Initialize(in view, anchor, state);
        var iterations = 0;
        var residual = 0.0;
        var detail = ICurveEvalDetail.None;
        var hitKind = CacheHitKind.None;
        if (status == AlgorithmStatus.Success)
            status = TrackBranch(in view, t, order, selected, segment, anchor, tolerance,
                ref budget, ref cache, side, state, result, out iterations, out residual, out detail, out hitKind);
        if (plan == ICurveConstraintPlan.Auto && selected == ICurveConstraintPlan.P2
            && status is AlgorithmStatus.NotConverged or AlgorithmStatus.Singular or AlgorithmStatus.NumericalFailure
            && detail != ICurveEvalDetail.BudgetExceeded)
        {
            var initialize = Initialize(in view, anchor, state);
            if (initialize == AlgorithmStatus.Success)
            {
                selected = ICurveConstraintPlan.P4;
                status = TrackBranch(in view, t, order, selected, segment, anchor, tolerance,
                    ref budget, ref cache, side, state, result, out var moreIterations,
                    out residual, out detail, out hitKind);
                iterations += moreIterations;
                if (status == AlgorithmStatus.Success) detail = ICurveEvalDetail.PlanSwitched;
            }
        }
        if (status != AlgorithmStatus.Success)
            return FinishFailure(query, status, selected, side, segment, iterations, residual, detail, out report);
        if (node >= 0) result[0] = view.ChartPositions[node];
        for (DerivativeOrder i = 0; i <= order; i++)
            if (!IsFinite(result[i]))
                return FinishFailure(query, AlgorithmStatus.NumericalFailure, selected,
                    side, segment, iterations, residual, detail, out report);

        var solvedWitness = new SampleWitness(state[0], state[1], state[2], state[3], 0,
            SampleWitness.HasUv0 | SampleWitness.HasUv1);
        var quality = node >= 0 ? 0 : tolerance;
        _ = cache.TryInsert(new CurveSample(t, result[0], order >= 1 ? result[1] : default,
            order >= 2 ? result[2] : default, order, query, side, segment, quality, residual,
            node >= 0 ? SampleSourceKind.ImportedChartAnchor : SampleSourceKind.CorrectedRoot,
            selected, in solvedWitness), separatePlans: true, evictWhenFull: true);
        result[..(order + 1)].CopyTo(derivatives);
        report = new ICurveEvalReport(query, AlgorithmStatus.Success, selected, side,
            segment, iterations, residual, hitKind, detail: detail, qualityError: quality);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus Initialize(in ICurveView view, BufferOffset anchor, Span<double> state)
    {
        var witness = view.ChartWitnesses[anchor];
        var point = view.ChartPositions[anchor];
        if (!ICurveSupportEvaluation.TryResolveUv(in view.Surface0, in point,
                (witness.Flags & SampleWitness.HasUv0) != 0, witness.U0, witness.V0, out state[0], out state[1])
            || !ICurveSupportEvaluation.TryResolveUv(in view.Surface1, in point,
                (witness.Flags & SampleWitness.HasUv1) != 0, witness.U1, witness.V1, out state[2], out state[3]))
            return AlgorithmStatus.Unsupported;
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus TrackBranch(in ICurveView view, double target, DerivativeOrder order,
        ICurveConstraintPlan plan, BufferOffset segment, BufferOffset anchor, double tolerance,
        ref EvaluationBudget budget, scoped ref EvaluationSampleStore cache, ChartSide side,
        scoped Span<double> state, scoped Span<KernelVector3> result, out BufferOffset iterations,
        out double residual, out ICurveEvalDetail detail, out CacheHitKind hitKind)
    {
        var status = FollowChart(in view, target, order, plan, segment, anchor, tolerance,
            ref budget, ref cache, side, state, result, out iterations, out residual, out detail, out hitKind);
        if (status != AlgorithmStatus.Success || target == view.ChartParameters[anchor]) return status;

        // A chart describes the connection between its two original anchors.
        // Tracking from only one end can silently accept a different nearby
        // branch. The other end must independently reach the same UV preimage;
        // it cannot borrow the first track's intermediate or cached state.
        Span<double> otherState = stackalloc double[4];
        Span<KernelVector3> otherResult = stackalloc KernelVector3[3];
        status = Initialize(in view, segment + 1, otherState);
        if (status != AlgorithmStatus.Success) return status;
        var empty = new EvaluationSampleStore(Span<CurveSample>.Empty);
        status = FollowChart(in view, target, order, plan, segment, segment + 1, tolerance,
            ref budget, ref empty, side, otherState, otherResult, out var moreIterations,
            out var otherResidual, out detail, out _);
        iterations += moreIterations;
        residual = Math.Max(residual, otherResidual);
        if (status != AlgorithmStatus.Success) return status;
        ICurveSupportEvaluation.UnwrapPeriodic(in view.Surface0, ref otherState[0], ref otherState[1], state[0], state[1]);
        ICurveSupportEvaluation.UnwrapPeriodic(in view.Surface1, ref otherState[2], ref otherState[3], state[2], state[3]);
        Span<KernelVector3> jet0 = stackalloc KernelVector3[4];
        Span<KernelVector3> jet1 = stackalloc KernelVector3[4];
        status = ReadSurfaceJets(in view, state, 1, ref budget, jet0, jet1, out detail);
        if (status != AlgorithmStatus.Success) return status;
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        var uvDifference = PairMotion(jet0, in layout, otherState[0] - state[0], otherState[1] - state[1])
            + PairMotion(jet1, in layout, otherState[2] - state[2], otherState[3] - state[3]);
        var same0 = SameSplinePreimage(in view.Surface0, state[0], state[1], otherState[0], otherState[1], out var unresolved0);
        var same1 = SameSplinePreimage(in view.Surface1, state[2], state[3], otherState[2], otherState[3], out var unresolved1);
        if (unresolved0 || unresolved1)
        {
            detail = ICurveEvalDetail.ParameterResolutionLost;
            return AlgorithmStatus.NotConverged;
        }
        var agree = Norm(Sub(result[0], otherResult[0])) <= 2 * tolerance
            && double.IsFinite(uvDifference) && uvDifference <= 4 * tolerance
            && same0 && same1;
        for (DerivativeOrder derivative = 1; derivative <= order && agree; derivative++)
        {
            var scale = Math.Max(Norm(result[derivative]), Norm(otherResult[derivative]));
            // A straight intersection can have a non-affine rational surface
            // parameter: its zero D2 then comes from cancellation. Independent
            // tracks need a roundoff floor with the same native-t units, not a
            // tolerance relative only to the nearly zero final derivative.
            var characteristic = derivative == 1
                ? Norm(Sub(view.ChartPositions[segment + 1], view.ChartPositions[segment]))
                : Math.Max(Norm(result[1]), Norm(otherResult[1]));
            var parameterSpan = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
            var rate = characteristic / parameterSpan;
            var roundoff = double.IsFinite(rate)
                ? (256 * SmallLinearSolve.MachineEpsilon) * rate
                : ((256 * SmallLinearSolve.MachineEpsilon) * characteristic) / parameterSpan;
            var agreementTolerance = Math.Max(1e-8 * scale, roundoff);
            if (!double.IsFinite(scale) || !double.IsFinite(roundoff) || !double.IsFinite(agreementTolerance))
                return AlgorithmStatus.NumericalFailure;
            agree = Norm(Sub(result[derivative], otherResult[derivative])) <= agreementTolerance;
        }
        if (agree) return AlgorithmStatus.Success;
        detail = ICurveEvalDetail.AmbiguousBranch;
        return AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus FollowChart(in ICurveView view, double target, DerivativeOrder order,
        ICurveConstraintPlan plan, BufferOffset segment, BufferOffset anchor, double tolerance,
        ref EvaluationBudget budget, scoped ref EvaluationSampleStore cache, ChartSide side,
        scoped Span<double> state, scoped Span<KernelVector3> result, out BufferOffset iterations,
        out double residual, out ICurveEvalDetail detail, out CacheHitKind hitKind)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        hitKind = CacheHitKind.None;
        var start = view.ChartParameters[anchor];
        var segmentSpan = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
        var chordLength = Norm(Sub(view.ChartPositions[segment + 1], view.ChartPositions[segment]));
        if (!(segmentSpan > 0) || !double.IsFinite(segmentSpan) || !(chordLength > 0) || !double.IsFinite(chordLength))
            return AlgorithmStatus.InvalidInput;
        Span<KernelVector3> jet0 = stackalloc KernelVector3[4];
        Span<KernelVector3> jet1 = stackalloc KernelVector3[4];
        var tangentStatus = ReadSurfaceJets(in view, state, 1, ref budget, jet0, jet1, out detail);
        if (tangentStatus != AlgorithmStatus.Success) return tangentStatus;
        if (!TryTangent(in view, state, jet0, jet1, 1, ref budget, out var tangent, out detail))
            return detail == ICurveEvalDetail.BudgetExceeded ? AlgorithmStatus.NotConverged : AlgorithmStatus.Singular;
        Span<KernelVector3> current = stackalloc KernelVector3[3];
        var radius = Math.Max(chordLength * 0.125, 32 * tolerance);
        var initial = CorrectAt(in view, start, start == target ? order : 0, plan, segment,
            state, in tangent, radius, tolerance, ref budget, current,
            out var rootTangent, out var initialIterations, out residual, out detail);
        iterations += initialIterations;
        if (initial != AlgorithmStatus.Success) return initial;
        tangent = rootTangent;
        if (start == target)
        {
            current[..(order + 1)].CopyTo(result);
            return AlgorithmStatus.Success;
        }

        // Only previously verified samples from this original segment are
        // eligible continuation anchors. Their UV state is rechecked before use.
        if (cache.TryFindNearest(target, ICurveQueryKind.RegularChartInterval, side, segment, out var neighbor)
            && neighbor.Plan == plan && neighbor.Parameter >= view.ChartParameters[segment]
            && neighbor.Parameter <= view.ChartParameters[segment + 1]
            && Math.Abs(target - neighbor.Parameter) < Math.Abs(target - start)
            && double.IsFinite(neighbor.ErrorEstimate) && neighbor.ErrorEstimate >= 0
            && neighbor.ErrorEstimate <= tolerance
            && (neighbor.Witness.Flags & (SampleWitness.HasUv0 | SampleWitness.HasUv1))
                == (SampleWitness.HasUv0 | SampleWitness.HasUv1))
        {
            Span<double> candidate = stackalloc double[4]
                { neighbor.Witness.U0, neighbor.Witness.V0, neighbor.Witness.U1, neighbor.Witness.V1 };
            Span<KernelVector3> cached = stackalloc KernelVector3[3];
            var cachedStatus = CorrectAt(in view, neighbor.Parameter, neighbor.Parameter == target ? order : 0, plan, segment,
                candidate, in tangent, Math.Max(32 * tolerance, chordLength * 1e-7), tolerance,
                ref budget, cached, out var cachedTangent, out var cachedIterations, out _, out var cachedDetail);
            iterations += cachedIterations;
            if (cachedDetail == ICurveEvalDetail.BudgetExceeded)
            {
                detail = cachedDetail;
                return AlgorithmStatus.NotConverged;
            }
            if (cachedStatus == AlgorithmStatus.Success)
            {
                candidate.CopyTo(state);
                cached.CopyTo(current);
                start = neighbor.Parameter;
                tangent = cachedTangent;
                hitKind = CacheHitKind.NeighborSeed;
            }
        }

        if (start == target)
        {
            current[..(order + 1)].CopyTo(result);
            return AlgorithmStatus.Success;
        }

        var direction = Math.Sign(target - start);
        var step = Math.Abs(target - start) * 0.25;
        var minimumStep = MinimumRelativeStep * segmentSpan;
        if (!(step > 0))
        {
            detail = ICurveEvalDetail.ParameterResolutionLost;
            return AlgorithmStatus.NotConverged;
        }
        if (step < minimumStep) step = Math.Min(Math.Abs(target - start), minimumStep);
        Span<double> trial = stackalloc double[4];
        Span<KernelVector3> next = stackalloc KernelVector3[3];
        for (BufferCount accepted = 0; accepted < MaxContinuationSteps;)
        {
            var remaining = Math.Abs(target - start);
            var nextParameter = step >= remaining ? target : start + direction * step;
            if (nextParameter == start || !double.IsFinite(nextParameter))
            {
                detail = ICurveEvalDetail.ParameterResolutionLost;
                return AlgorithmStatus.NotConverged;
            }
            if ((nextParameter - target) * direction > 0) nextParameter = target;
            var advance = Math.Abs(PlaneResidual(in view, segment, nextParameter, in current[0]));
            var cosine = Math.Abs(Dot(view.ChartChordUnits[segment], tangent));
            if (!(cosine > OriginalChartParameterMap.MinCosine) || !double.IsFinite(advance))
                return AlgorithmStatus.Singular;
            radius = Math.Max(4 * advance / cosine, 32 * tolerance);
            if (!double.IsFinite(radius)) return AlgorithmStatus.NumericalFailure;
            state.CopyTo(trial);
            var trialStatus = CorrectAt(in view, nextParameter, nextParameter == target ? order : 0,
                plan, segment, trial, in tangent, radius, tolerance, ref budget, next,
                out var nextTangent, out var used, out residual, out detail);
            iterations += used;
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            if (trialStatus == AlgorithmStatus.Success
                && Norm(Sub(next[0], current[0])) <= radius
                && Dot(tangent, nextTangent) > 0)
            {
                trial.CopyTo(state);
                next.CopyTo(current);
                tangent = nextTangent;
                start = nextParameter;
                accepted++;
                if (start == target)
                {
                    current[..(order + 1)].CopyTo(result);
                    return AlgorithmStatus.Success;
                }
                step = Math.Min(step * 1.5, Math.Abs(target - start));
                continue;
            }
            if (trialStatus is AlgorithmStatus.Unsupported or AlgorithmStatus.WorkspaceTooSmall
                or AlgorithmStatus.OutputTooSmall)
                return trialStatus;
            step *= 0.5;
            if (!(step >= minimumStep) || !(step > 0))
            {
                detail = trialStatus == AlgorithmStatus.Success ? ICurveEvalDetail.AmbiguousBranch : ICurveEvalDetail.Stagnation;
                return trialStatus == AlgorithmStatus.Singular ? trialStatus : AlgorithmStatus.NotConverged;
            }
        }
        detail = ICurveEvalDetail.Stagnation;
        return AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus CorrectAt(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, BufferOffset segment, Span<double> state,
        in KernelVector3 referenceTangent, double radius, double tolerance,
        ref EvaluationBudget budget, Span<KernelVector3> result, out KernelVector3 tangent,
        out BufferOffset iterations, out double residual, out ICurveEvalDetail detail)
    {
        tangent = default;
        iterations = 0;
        residual = double.PositiveInfinity;
        detail = ICurveEvalDetail.None;
        var n = plan == ICurveConstraintPlan.P4 ? 4 : 2;
        Span<double> start = stackalloc double[4];
        state.CopyTo(start);
        Span<double> f = stackalloc double[4];
        Span<double> jacobian = stackalloc double[16];
        Span<double> correction = stackalloc double[4];
        Span<double> trial = stackalloc double[4];
        Span<double> trialF = stackalloc double[4];
        Span<double> trialJ = stackalloc double[16];
        Span<KernelVector3> jet0 = stackalloc KernelVector3[4];
        Span<KernelVector3> jet1 = stackalloc KernelVector3[4];
        Span<KernelVector3> startJet0 = stackalloc KernelVector3[4];
        Span<KernelVector3> startJet1 = stackalloc KernelVector3[4];
        Span<KernelVector3> trialJet0 = stackalloc KernelVector3[4];
        Span<KernelVector3> trialJet1 = stackalloc KernelVector3[4];
        for (BufferCount iteration = 0; iteration < MaxCorrections; iteration++)
        {
            iterations++;
            var evaluate = EvaluateSystem(in view, t, plan, segment, state, 0, ref budget,
                f, jacobian, jet0, jet1, out var divisor, out detail);
            if (evaluate != AlgorithmStatus.Success) return evaluate;
            if (iteration == 0) { jet0.CopyTo(startJet0); jet1.CopyTo(startJet1); }
            residual = SmallLinearSolve.Norm(f[..n]);
            if (!double.IsFinite(residual)) return AlgorithmStatus.NumericalFailure;
            if (residual <= tolerance)
            {
                var publish = FinishRoot(in view, t, order, plan, segment, state,
                    in referenceTangent, tolerance, ref budget, result, out tangent, out detail);
                if (publish == AlgorithmStatus.Success) return publish;
                if (publish is not AlgorithmStatus.NotConverged) return publish;
                if (detail == ICurveEvalDetail.ParameterResolutionLost) return publish;
            }
            for (BufferOffset i = 0; i < n; i++) correction[i] = -f[i];
            var solve = Solve(jacobian, n, correction);
            if (solve != AlgorithmStatus.Success) return solve;
            var physicalStep = StepSize(in view, plan, correction, jet0, jet1);
            if (!(physicalStep > 0) || !double.IsFinite(physicalStep))
            {
                detail = ICurveEvalDetail.Stagnation;
                return AlgorithmStatus.NotConverged;
            }
            var alpha = Math.Min(1.0, radius / physicalStep);
            var accepted = false;
            for (BufferCount line = 0; line < MaxLineSearch; line++, alpha *= 0.5)
            {
                state.CopyTo(trial);
                if (plan == ICurveConstraintPlan.P4)
                {
                    for (BufferOffset i = 0; i < 4; i++) trial[i] = Math.FusedMultiplyAdd(alpha, correction[i], state[i]);
                }
                else
                {
                    var offset = view.Surface0.IsBSurface ? 0 : 2;
                    trial[offset] = Math.FusedMultiplyAdd(alpha, correction[0], state[offset]);
                    trial[offset + 1] = Math.FusedMultiplyAdd(alpha, correction[1], state[offset + 1]);
                }
                ClampDomainRoundoff(in view.Surface0, trial[..2]);
                ClampDomainRoundoff(in view.Surface1, trial[2..]);
                if (!WithinNeighborhood(in view, plan, start, trial, startJet0, startJet1, radius)) continue;
                var trialStatus = EvaluateSystem(in view, t, plan, segment, trial, divisor, ref budget,
                    trialF, trialJ, trialJet0, trialJet1, out _, out detail);
                if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
                if (trialStatus != AlgorithmStatus.Success) continue;
                var trialNorm = SmallLinearSolve.Norm(trialF[..n]);
                if (trialNorm < residual && trialNorm <= (1 - 1e-4 * alpha) * residual)
                {
                    trial.CopyTo(state);
                    accepted = true;
                    break;
                }
            }
            if (!accepted)
            {
                detail = ICurveEvalDetail.Stagnation;
                return AlgorithmStatus.NotConverged;
            }
        }
        detail = ICurveEvalDetail.Stagnation;
        return AlgorithmStatus.NotConverged;
    }

    private static AlgorithmStatus EvaluateSystem(in ICurveView view, double t, ICurveConstraintPlan plan,
        BufferOffset segment, ReadOnlySpan<double> state, double fixedDivisor, ref EvaluationBudget budget,
        Span<double> f, Span<double> jacobian, Span<KernelVector3> jet0, Span<KernelVector3> jet1,
        out double divisor, out ICurveEvalDetail detail)
    {
        detail = ICurveEvalDetail.None;
        divisor = 1;
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        if (plan == ICurveConstraintPlan.P4)
        {
            var status = ReadSurfaceJets(in view, state, 1, ref budget, jet0, jet1, out detail);
            if (status != AlgorithmStatus.Success) return status;
            AssembleP4(in view, t, segment, in layout, state, jet0, jet1, f, jacobian);
            return AlgorithmStatus.Success;
        }
        var parameterFirst = view.Surface0.IsBSurface;
        var support = parameterFirst ? view.Surface0 : view.Surface1;
        var implicitSurface = parameterFirst ? view.Surface1.Analytic : view.Surface0.Analytic;
        var offset = parameterFirst ? 0 : 2;
        var parametricJet = parameterFirst ? jet0 : jet1;
        if (!Charge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
        var evaluated = ICurveSupportEvaluation.Evaluate(in support, state[offset], state[offset + 1], in layout, parametricJet);
        if (evaluated != AlgorithmStatus.Success) return evaluated;
        if (!Charge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
        var implicitStatus = AnalyticImplicitEvaluation.Evaluate(in implicitSurface, in parametricJet[0], 1, out var implicitJet);
        if (implicitStatus != AlgorithmStatus.Success) return implicitStatus;
        divisor = fixedDivisor > 0 ? fixedDivisor : Norm(implicitJet.Gradient);
        if (!(divisor > 0) || !double.IsFinite(divisor)) return AlgorithmStatus.Singular;
        var gradient = Vector(implicitJet.Gradient.X / divisor, implicitJet.Gradient.Y / divisor, implicitJet.Gradient.Z / divisor);
        var su = parametricJet[layout.GetIndex(1, 0)];
        var sv = parametricJet[layout.GetIndex(0, 1)];
        var implicitValue=implicitJet.Value;
        if(implicitSurface.Kind==SurfaceClass.Plane&&SplinePlaneResidual.TryEvaluate(support.BSpline,implicitSurface,state[offset],state[offset+1],out var stableValue))implicitValue=stableValue;
        f[0] = implicitValue / divisor;
        f[1] = PlaneResidual(in view, segment, t, in parametricJet[0]);
        jacobian[0] = Dot(gradient, su); jacobian[1] = Dot(gradient, sv);
        jacobian[2] = Dot(view.ChartChordUnits[segment], su); jacobian[3] = Dot(view.ChartChordUnits[segment], sv);
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Independent full parametric-system gate, also used after P2. Its
    /// physical correction bound cannot be replaced by an implicit residual.
    /// Requested jets are recomputed at the accepted UV before differentiation.
    /// </summary>
    private static AlgorithmStatus FinishRoot(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, BufferOffset segment, Span<double> state,
        in KernelVector3 referenceTangent, double tolerance, ref EvaluationBudget budget,
        Span<KernelVector3> result, out KernelVector3 tangent, out ICurveEvalDetail detail)
    {
        tangent = default;
        detail = ICurveEvalDetail.None;
        var jetOrder = Math.Max(1, order);
        SurfaceDerivativeLayout.TryCreate(jetOrder, jetOrder, out var layout);
        Span<KernelVector3> jet0 = stackalloc KernelVector3[9];
        Span<KernelVector3> jet1 = stackalloc KernelVector3[9];
        if (plan == ICurveConstraintPlan.P2)
        {
            var parameterFirst = view.Surface0.IsBSurface;
            var support = parameterFirst ? view.Surface0 : view.Surface1;
            var other = parameterFirst ? view.Surface1 : view.Surface0;
            var offset = parameterFirst ? 0 : 2;
            SurfaceDerivativeLayout.TryCreate(0, 0, out var positionLayout);
            Span<KernelVector3> value = stackalloc KernelVector3[1];
            if (!Charge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
            var evaluated = ICurveSupportEvaluation.Evaluate(in support, state[offset], state[offset + 1], in positionLayout, value);
            if (evaluated != AlgorithmStatus.Success) return evaluated;
            var otherOffset = 2 - offset;
            if (!ICurveSupportEvaluation.TryResolveUv(in other, in value[0], true,
                    state[otherOffset], state[otherOffset + 1], out var u, out var v))
                return AlgorithmStatus.Singular;
            state[otherOffset] = u; state[otherOffset + 1] = v;
        }
        var status = ReadSurfaceJets(in view, state, jetOrder, ref budget, jet0, jet1, out detail);
        if (status != AlgorithmStatus.Success) return status;
        // The stable plane numerator must not hide a Cartesian D0 that the
        // ordinary support evaluator cannot represent within physical error.
        if(Norm(Sub(jet0[0],jet1[0]))>tolerance)return AlgorithmStatus.NotConverged;
        Span<double> f = stackalloc double[4];
        Span<double> jacobian = stackalloc double[16];
        AssembleP4(in view, t, segment, in layout, state, jet0, jet1, f, jacobian);
        if (SmallLinearSolve.Norm(f[..3]) > tolerance || Math.Abs(f[3]) > tolerance)
            return AlgorithmStatus.NotConverged;
        if (!TryTangent(in view, state, jet0, jet1, jetOrder, ref budget, out tangent, out detail))
            return detail == ICurveEvalDetail.BudgetExceeded ? AlgorithmStatus.NotConverged : AlgorithmStatus.Singular;
        var anchorProjection = Dot(referenceTangent, view.ChartChordUnits[segment]);
        var rootProjection = Dot(tangent, view.ChartChordUnits[segment]);
        if (!(Math.Abs(rootProjection) > OriginalChartParameterMap.MinCosine)
            || !(anchorProjection * rootProjection > 0) || !(Dot(referenceTangent, tangent) > 0))
        {
            detail = ICurveEvalDetail.AmbiguousBranch;
            return AlgorithmStatus.NotConverged;
        }
        Span<double> correction = stackalloc double[4] { -f[0], -f[1], -f[2], -f[3] };
        Span<double> factor = stackalloc double[16];
        Span<double> columnScales = stackalloc double[4];
        Span<int> pivots = stackalloc int[4];
        var solved = Factor(jacobian, 4, factor, columnScales, pivots);
        if (solved != AlgorithmStatus.Success) return solved;
        solved = SolveFactored(factor, 4, columnScales, pivots, correction);
        if (solved != AlgorithmStatus.Success) return solved;
        var physicalCorrection = PairMotion(jet0, in layout, correction[0], correction[1])
            + PairMotion(jet1, in layout, correction[2], correction[3]);
        if (!double.IsFinite(physicalCorrection) || physicalCorrection > tolerance)
            return AlgorithmStatus.NotConverged;
        var accurate0 = FinalUvAccuracy(in view.Surface0, state[0], state[1], correction[0], correction[1], out var unresolved0);
        var accurate1 = FinalUvAccuracy(in view.Surface1, state[2], state[3], correction[2], correction[3], out var unresolved1);
        if (unresolved0 || unresolved1)
        {
            detail = ICurveEvalDetail.ParameterResolutionLost;
            return AlgorithmStatus.NotConverged;
        }
        // A small physical residual is not enough to identify a UV preimage:
        // a thin support can have well separated parameters within that error.
        // Continue Newton until its UV correction reaches local roundoff too.
        if (!accurate0 || !accurate1) return AlgorithmStatus.NotConverged;
        result[0] = jet0[0];
        if (order == 0) return AlgorithmStatus.Success;

        Span<double> d1 = stackalloc double[4] { 0, 0, 0, 1 / view.ChartScales[segment] };
        solved = SolveFactored(factor, 4, columnScales, pivots, d1);
        if (solved != AlgorithmStatus.Success) return solved;
        result[1] = FirstChain(jet0, in layout, d1[0], d1[1]);
        if (!IsFinite(result[1])) return AlgorithmStatus.NumericalFailure;
        if (order == 1) return AlgorithmStatus.Success;

        var second0 = SecondChain(jet0, in layout, d1[0], d1[1]);
        var second1 = SecondChain(jet1, in layout, d1[2], d1[3]);
        Span<double> d2 = stackalloc double[4]
        {
            second1.X - second0.X, second1.Y - second0.Y, second1.Z - second0.Z,
            -Dot(view.ChartChordUnits[segment], second0),
        };
        solved = SolveFactored(factor, 4, columnScales, pivots, d2);
        if (solved != AlgorithmStatus.Success) return solved;
        result[2] = Add(second0, FirstChain(jet0, in layout, d2[0], d2[1]));
        return IsFinite(result[2]) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    private static AlgorithmStatus ReadSurfaceJets(in ICurveView view, ReadOnlySpan<double> state,
        DerivativeOrder order, ref EvaluationBudget budget, Span<KernelVector3> jet0,
        Span<KernelVector3> jet1, out ICurveEvalDetail detail)
    {
        SurfaceDerivativeLayout.TryCreate(order, order, out var layout);
        if (!Charge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
        var first = ICurveSupportEvaluation.Evaluate(in view.Surface0, state[0], state[1], in layout, jet0);
        if (first != AlgorithmStatus.Success) return first;
        if (!Charge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
        return ICurveSupportEvaluation.Evaluate(in view.Surface1, state[2], state[3], in layout, jet1);
    }

    private static void AssembleP4(in ICurveView view, double t, BufferOffset segment,
        in SurfaceDerivativeLayout layout, ReadOnlySpan<double> state, ReadOnlySpan<KernelVector3> jet0,
        ReadOnlySpan<KernelVector3> jet1, Span<double> f, Span<double> j)
    {
        var su0 = jet0[layout.GetIndex(1, 0)]; var sv0 = jet0[layout.GetIndex(0, 1)];
        var su1 = jet1[layout.GetIndex(1, 0)]; var sv1 = jet1[layout.GetIndex(0, 1)];
        f[0] = jet0[0].X - jet1[0].X; f[1] = jet0[0].Y - jet1[0].Y; f[2] = jet0[0].Z - jet1[0].Z;
        if(view.Surface0.IsBSurface!=view.Surface1.IsBSurface)
        {
            var splineFirst=view.Surface0.IsBSurface;var spline=splineFirst?view.Surface0:view.Surface1;var plane=splineFirst?view.Surface1.Analytic:view.Surface0.Analytic;var offset=splineFirst?0:2;
            if(plane.Kind==SurfaceClass.Plane&&SplinePlaneResidual.TryCorrectDifference(spline.BSpline,plane,state[offset],state[offset+1],state[2-offset],state[3-offset],splineFirst,Vector(f[0],f[1],f[2]),out var stable))
            {f[0]=stable.X;f[1]=stable.Y;f[2]=stable.Z;}
        }
        f[3] = PlaneResidual(in view, segment, t, in jet0[0]);
        j[0] = su0.X; j[1] = sv0.X; j[2] = -su1.X; j[3] = -sv1.X;
        j[4] = su0.Y; j[5] = sv0.Y; j[6] = -su1.Y; j[7] = -sv1.Y;
        j[8] = su0.Z; j[9] = sv0.Z; j[10] = -su1.Z; j[11] = -sv1.Z;
        j[12] = Dot(view.ChartChordUnits[segment], su0); j[13] = Dot(view.ChartChordUnits[segment], sv0);
        j[14] = j[15] = 0;
    }

    private static bool TryTangent(in ICurveView view, ReadOnlySpan<double> state,
        ReadOnlySpan<KernelVector3> jet0, ReadOnlySpan<KernelVector3> jet1, DerivativeOrder order,
        ref EvaluationBudget budget, out KernelVector3 tangent, out ICurveEvalDetail detail)
    {
        tangent = default;
        detail = ICurveEvalDetail.None;
        SurfaceDerivativeLayout.TryCreate(order, order, out var layout);
        if (!TryNormal(in view.Surface0, state[0], state[1], in layout, jet0, ref budget, out var n0, out detail)
            || !TryNormal(in view.Surface1, state[2], state[3], in layout, jet1, ref budget, out var n1, out detail))
            return false;
        if (view.Sense0 == ParasolidConstants.PK_TOPOL_sense_negative_c) n0 = Scale(n0, -1);
        if (view.Sense1 == ParasolidConstants.PK_TOPOL_sense_negative_c) n1 = Scale(n1, -1);
        var cross = Cross(n0, n1);
        if (!(Norm(cross) > OriginalChartParameterMap.MinCosine)) return false;
        tangent = Unit(cross);
        return IsFinite(tangent) && Dot(tangent, tangent) > 0.5;
    }

    private static bool TryNormal(in ICurveSupportView support, double u, double v,
        in SurfaceDerivativeLayout layout, ReadOnlySpan<KernelVector3> jet,
        ref EvaluationBudget budget, out KernelVector3 normal, out ICurveEvalDetail detail)
    {
        normal = default;
        detail = ICurveEvalDetail.None;
        if (!support.IsBSurface)
            return Charge(ref budget, out detail)
                && ICurveSupportEvaluation.TryNormal(in support, u, v, in jet[0], out normal);
        var cross = Cross(Unit(jet[layout.GetIndex(1, 0)]), Unit(jet[layout.GetIndex(0, 1)]));
        if (!(Norm(cross) > OriginalChartParameterMap.MinCosine)) return false;
        normal = Unit(cross);
        return IsFinite(normal) && Dot(normal, normal) > 0.5;
    }

    /// <summary>Column equilibration keeps a change of UV units out of the rank test.</summary>
    private static AlgorithmStatus Solve(ReadOnlySpan<double> jacobian, BufferCount n, Span<double> rhs)
    {
        Span<double> matrix = stackalloc double[16];
        Span<double> scales = stackalloc double[4];
        Span<int> pivots = stackalloc int[4];
        var status = Factor(jacobian, n, matrix, scales, pivots);
        return status == AlgorithmStatus.Success ? SolveFactored(matrix, n, scales, pivots, rhs) : status;
    }

    private static AlgorithmStatus Factor(ReadOnlySpan<double> jacobian, BufferCount n,
        Span<double> matrix, Span<double> scales, Span<int> pivots)
    {
        for (BufferOffset column = 0; column < n; column++)
        {
            var scale = 0.0;
            for (BufferOffset row = 0; row < n; row++) scale = Math.Max(scale, Math.Abs(jacobian[row * n + column]));
            if (!(scale > 0) || !double.IsFinite(scale)) return AlgorithmStatus.Singular;
            scales[column] = scale;
            for (BufferOffset row = 0; row < n; row++) matrix[row * n + column] = jacobian[row * n + column] / scale;
        }
        return SmallLinearSolve.LuFactorize(matrix, n, pivots);
    }

    private static AlgorithmStatus SolveFactored(ReadOnlySpan<double> matrix, BufferCount n,
        ReadOnlySpan<double> scales, ReadOnlySpan<int> pivots, Span<double> rhs)
    {
        var status = SmallLinearSolve.LuSolveInPlace(matrix, n, pivots, rhs);
        if (status != AlgorithmStatus.Success) return status;
        for (BufferOffset i = 0; i < n; i++)
        {
            rhs[i] /= scales[i];
            if (!double.IsFinite(rhs[i])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    private static bool WithinNeighborhood(in ICurveView view, ICurveConstraintPlan plan,
        ReadOnlySpan<double> start, ReadOnlySpan<double> trial,
        ReadOnlySpan<KernelVector3> jet0, ReadOnlySpan<KernelVector3> jet1, double radius)
    {
        if (!ICurveSupportEvaluation.IsInDomain(in view.Surface0, trial[0], trial[1])
            || !ICurveSupportEvaluation.IsInDomain(in view.Surface1, trial[2], trial[3])) return false;
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        if (plan == ICurveConstraintPlan.P4 || view.Surface0.IsBSurface)
        {
            if (!(PairMotion(jet0, in layout, trial[0] - start[0], trial[1] - start[1]) <= radius)
                || !BoundedSplineStep(in view.Surface0, start[0], start[1], trial[0], trial[1])) return false;
        }
        if (plan == ICurveConstraintPlan.P4 || view.Surface1.IsBSurface)
        {
            if (!(PairMotion(jet1, in layout, trial[2] - start[2], trial[3] - start[3]) <= radius)
                || !BoundedSplineStep(in view.Surface1, start[2], start[3], trial[2], trial[3])) return false;
        }
        return true;
    }

    private static bool BoundedSplineStep(in ICurveSupportView surface, double u0, double v0, double u1, double v1)
    {
        if (!surface.IsBSurface) return true;
        var spline = surface.BSpline;
        return Math.Abs(u1 - u0) <= 0.25 * TraversedKnotWidth(spline.UKnots, spline.UDegree,
                spline.UVertexCount, spline.UPeriodic, u0, u1)
            && Math.Abs(v1 - v0) <= 0.25 * TraversedKnotWidth(spline.VKnots, spline.VDegree,
                spline.VVertexCount, spline.VPeriodic, v0, v1);
    }

    private static bool SameSplinePreimage(in ICurveSupportView surface, double u0, double v0,
        double u1, double v1, out bool unresolved)
    {
        unresolved = false;
        if (!surface.IsBSurface) return true;
        var spline = surface.BSpline;
        var uWidth = TraversedKnotWidth(spline.UKnots, spline.UDegree, spline.UVertexCount, spline.UPeriodic, u0, u1);
        var vWidth = TraversedKnotWidth(spline.VKnots, spline.VDegree, spline.VVertexCount, spline.VPeriodic, v0, v1);
        if (!TryUvResolution(uWidth, u0, u1, out var uTolerance)
            || !TryUvResolution(vWidth, v0, v1, out var vTolerance))
        {
            unresolved = true;
            return false;
        }
        return Math.Abs(u1 - u0) <= uTolerance && Math.Abs(v1 - v0) <= vTolerance;
    }

    private static bool FinalUvAccuracy(in ICurveSupportView surface, double u, double v,
        double du, double dv, out bool unresolved)
    {
        unresolved = false;
        if (!surface.IsBSurface) return true;
        var spline = surface.BSpline;
        var uWidth = TraversedKnotWidth(spline.UKnots, spline.UDegree, spline.UVertexCount, spline.UPeriodic, u, u);
        var vWidth = TraversedKnotWidth(spline.VKnots, spline.VDegree, spline.VVertexCount, spline.VPeriodic, v, v);
        if (!TryUvResolution(uWidth, u, u, out var uTolerance)
            || !TryUvResolution(vWidth, v, v, out var vTolerance))
        {
            unresolved = true;
            return false;
        }
        return Math.Abs(du) <= uTolerance && Math.Abs(dv) <= vTolerance;
    }

    // A boundary root can produce an outward Newton component of a few ULPs.
    // Keep that component on its domain boundary; FinishRoot still checks the
    // full physical system and the unmodified UV correction before accepting.
    private static void ClampDomainRoundoff(in ICurveSupportView surface, Span<double> uv)
    {
        if (!surface.IsBSurface) return;
        var spline = surface.BSpline;
        if (!spline.UPeriodic) ClampParameter(spline.UKnots, spline.UDegree, spline.UVertexCount, ref uv[0]);
        if (!spline.VPeriodic) ClampParameter(spline.VKnots, spline.VDegree, spline.VVertexCount, ref uv[1]);
    }

    private static void ClampParameter(ReadOnlySpan<double> knots, SplineDegree degree, BufferCount count, ref double value)
    {
        var boundary = value < knots[degree] ? knots[degree] : value > knots[count] ? knots[count] : value;
        if (boundary == value) return;
        var width = TraversedKnotWidth(knots, degree, count, false, boundary, boundary);
        if (TryUvResolution(width, boundary, boundary, out var tolerance) && Math.Abs(value - boundary) <= tolerance)
            value = boundary;
    }

    private static bool TryUvResolution(double cellWidth, double a, double b, out double tolerance)
    {
        tolerance = 0;
        if (!(cellWidth > 0) || !double.IsFinite(cellWidth) || !double.IsFinite(a) || !double.IsFinite(b)) return false;
        var ulp = Math.Max(Ulp(a), Ulp(b));
        // Require at least 30 bits of resolution within the current knot cell.
        // A large UV origin must not turn a few ULPs into a substantial part of
        // the cell and thereby merge separate preimages.
        if (!(ulp <= cellWidth / 1_073_741_824.0)) return false;
        tolerance = Math.Max(64 * SmallLinearSolve.MachineEpsilon * cellWidth, 4 * ulp);
        return double.IsFinite(tolerance) && tolerance > 0;
    }

    private static double Ulp(double value)
    {
        var magnitude = Math.Abs(value);
        var next = Math.BitIncrement(magnitude);
        return double.IsFinite(next) ? next - magnitude : magnitude - Math.BitDecrement(magnitude);
    }

    /// <summary>Limit a continuation correction by the actual knot cells it traverses.</summary>
    private static double TraversedKnotWidth(ReadOnlySpan<double> knots, SplineDegree degree,
        BufferCount vertexCount, bool periodic, double from, double to)
    {
        var start = knots[degree];
        var period = knots[vertexCount] - start;
        if (!(period > 0) || !double.IsFinite(period) || !double.IsFinite(from) || !double.IsFinite(to)) return 0;
        if (periodic)
        {
            var delta = to - from;
            if (Math.Abs(delta) > period * 0.5) return 0;
            from = start + ((from - start) % period + period) % period;
            to = from + delta;
        }
        var lo = Math.Min(from, to);
        var hi = Math.Max(from, to);
        var minimum = period;
        for (BufferOffset i = degree; i < vertexCount; i++)
        {
            var a = knots[i];
            var b = knots[i + 1];
            if (!(b > a)) continue;
            if ((a <= hi && b >= lo)
                || (periodic && ((a - period <= hi && b - period >= lo)
                    || (a + period <= hi && b + period >= lo))))
                minimum = Math.Min(minimum, b - a);
        }
        return minimum;
    }

    private static double StepSize(in ICurveView view, ICurveConstraintPlan plan,
        ReadOnlySpan<double> step, ReadOnlySpan<KernelVector3> jet0, ReadOnlySpan<KernelVector3> jet1)
    {
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        if (plan == ICurveConstraintPlan.P4)
            return Math.Max(PairMotion(jet0, in layout, step[0], step[1]), PairMotion(jet1, in layout, step[2], step[3]));
        return PairMotion(view.Surface0.IsBSurface ? jet0 : jet1, in layout, step[0], step[1]);
    }

    private static double PairMotion(ReadOnlySpan<KernelVector3> jet,
        in SurfaceDerivativeLayout layout, double du, double dv)
        => Norm(jet[layout.GetIndex(1, 0)]) * Math.Abs(du) + Norm(jet[layout.GetIndex(0, 1)]) * Math.Abs(dv);

    private static KernelVector3 FirstChain(ReadOnlySpan<KernelVector3> jet,
        in SurfaceDerivativeLayout layout, double du, double dv)
        => Add(Scale(jet[layout.GetIndex(1, 0)], du), Scale(jet[layout.GetIndex(0, 1)], dv));

    private static KernelVector3 SecondChain(ReadOnlySpan<KernelVector3> jet,
        in SurfaceDerivativeLayout layout, double du, double dv)
        => Add(Add(Scale(Scale(jet[layout.GetIndex(2, 0)], du), du),
            Scale(Scale(Scale(jet[layout.GetIndex(1, 1)], du), dv), 2)),
            Scale(Scale(jet[layout.GetIndex(0, 2)], dv), dv));

    private static double PlaneResidual(in ICurveView view, BufferOffset segment, double t, in KernelVector3 point)
        => OriginalChartParameterMap.PlaneResidual(view.ChartPositions, view.ChartParameters,
            view.ChartScales, view.ChartChordUnits, segment, t, in point);

    private static bool Charge(ref EvaluationBudget budget, out ICurveEvalDetail detail)
    {
        detail = ICurveEvalDetail.None;
        if (budget.TryConsume(1)) return true;
        detail = ICurveEvalDetail.BudgetExceeded;
        return false;
    }

    private static AlgorithmStatus FinishFailure(ICurveQueryKind query, AlgorithmStatus status,
        ICurveConstraintPlan plan, ChartSide side, BufferOffset segment, BufferOffset iterations,
        double residual, ICurveEvalDetail detail, out ICurveEvalReport report)
    {
        report = new ICurveEvalReport(query, status, plan, side, segment, iterations, residual, detail: detail);
        return status;
    }

    private static double Norm(in KernelVector3 value)
        => SmallLinearSolve.Norm(stackalloc double[3] { value.X, value.Y, value.Z });

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
