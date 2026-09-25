using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Icurve evaluation over the original chart (spec §7, §16, tasks T07/T08):
/// the five regular-interval constraint plans P4/P2/I3/I2/I1 behind one query
/// classification, plus the ChartPoint and domain-exit contracts. Semantics
/// are fixed before any solving: an exact chart node returns the original
/// point (§5.4), regular intervals solve their plan's defining system on the
/// prepared chord plane, and everything outside [t₀, t_{m−1}] is refused
/// instead of clamped. The plan is an internal representation choice — it may
/// not change the requested point, branch or parameter (§7 preamble).
/// </summary>
internal static class ICurveEvaluation
{
    /// <summary>Fast Newton budget per seed (spec §14.7 starting configuration).</summary>
    internal const int MaxFastNewtonIterations = 8;
    /// <summary>Convergence target relative to the local length scale.</summary>
    internal const double ResidualTolerance = 1e-13;
    /// <summary>Highest derivative order this slice provides (§16.4 capability).</summary>
    internal const int MaxDerivativeOrder = 2;

    /// <summary>
    /// Evaluate position and, requested, the derivatives of the defined branch
    /// at native parameter t with automatic plan selection. Output is written
    /// only when every requested order validated — a failed D2 never publishes
    /// a partial D0 (§18.5). Terminator intervals need an explicit rule entry
    /// (GATE-T, §6.5); this production entry never guesses one.
    /// </summary>
    internal static AlgorithmStatus Evaluate(in ICurveView view, double t, DerivativeOrder order,
        scoped Span<KernelVector3> derivatives, out ICurveEvalReport report)
        => EvaluateWithPlan(in view, t, order, ICurveConstraintPlan.Auto, derivatives, out report);

    /// <summary>
    /// Evaluate with an explicit plan. <see cref="ICurveConstraintPlan.Auto"/>
    /// applies the §7.6 selection rules; an explicit plan is honored when the
    /// supports carry the required capabilities and refused otherwise (no
    /// silent fallback — a forced plan that cannot run must say so).
    /// </summary>
    internal static AlgorithmStatus EvaluateWithPlan(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, scoped Span<KernelVector3> derivatives, out ICurveEvalReport report)
        => EvaluateWithRule(in view, t, order, plan, TerminatorParameterRule.Unresolved, derivatives, out report);

    /// <summary>
    /// Evaluate with an explicit plan and terminator-parameter rule. The rule
    /// is an experiment selector (§6.5, GATE-T): named rules run the
    /// terminator paths deterministically; <see cref="TerminatorParameterRule.Unresolved"/>
    /// refuses them with a located gate diagnostic.
    /// </summary>
    internal static AlgorithmStatus EvaluateWithRule(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, TerminatorParameterRule rule, scoped Span<KernelVector3> derivatives,
        out ICurveEvalReport report)
    {
        var empty = new EvaluationSampleStore(Span<CurveSample>.Empty);
        return EvaluateWithCache(in view, t, order, plan, rule, ref empty, derivatives, out report);
    }

    /// <summary>
    /// Cached evaluation over the operation-level sample atlas (spec §13,
    /// task T09): an exact verified sample publishes directly; otherwise the
    /// request is corrected on the defined branch — seeded from a Hermite
    /// bracket prediction when the atlas offers one — and the validated result
    /// joins the atlas. Predictions never publish uncorrected, and failed
    /// requests store nothing.
    /// </summary>
    internal static AlgorithmStatus EvaluateWithCache(scoped in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, scoped ref EvaluationSampleStore cache, scoped Span<KernelVector3> derivatives,
        out ICurveEvalReport report)
        => EvaluateWithCache(in view, t, order, plan, TerminatorParameterRule.Unresolved, ref cache,
            derivatives, out report);

    /// <summary>Cached evaluation with an explicit terminator-parameter rule (§6.5).</summary>
    internal static AlgorithmStatus EvaluateWithCache(scoped in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, TerminatorParameterRule rule, scoped ref EvaluationSampleStore cache,
        scoped Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, AlgorithmStatus.NotRun,
            ICurveConstraintPlan.Auto, ChartSide.Right, -1, 0, 0);
        if (order < 0 || order > MaxDerivativeOrder) return AlgorithmStatus.InvalidInput;
        if (derivatives.Length <= order) return AlgorithmStatus.OutputTooSmall;
        if (!double.IsFinite(t)) return AlgorithmStatus.InvalidInput;

        Span<KernelVector3> result = stackalloc KernelVector3[MaxDerivativeOrder + 1];
        var status = EvaluateWithCacheCore(in view, t, order, plan, rule, ref cache, result, out report);
        if (status != AlgorithmStatus.Success) return status;
        for (DerivativeOrder i = 0; i <= order; i++)
            if (!IsFinite(result[i]))
            {
                report = new ICurveEvalReport(report.Kind, AlgorithmStatus.NumericalFailure,
                    report.Plan, report.Side, report.Segment, report.NewtonIterations,
                    report.Residual, report.CacheHit, report.NonDefiningResidual, report.Detail);
                return AlgorithmStatus.NumericalFailure;
            }
        result[..(order + 1)].CopyTo(derivatives);
        return status;
    }

    private static AlgorithmStatus EvaluateWithCacheCore(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, TerminatorParameterRule rule, scoped ref EvaluationSampleStore cache,
        scoped Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, AlgorithmStatus.NotRun,
            ICurveConstraintPlan.Auto, ChartSide.Right, -1, 0, 0);

        var parameters = view.ChartParameters;
        if (t < parameters[0] || t > parameters[^1])
        {
            // Outside the chart only a terminator limit can extend the domain;
            // classification precedes any solver choice (§6.1).
            if (t < parameters[0] && view.HasStartTerminator)
                return EvaluateTerminator(in view, t, order, rule, false, ref cache, derivatives, out report);
            if (t > parameters[^1] && view.HasEndTerminator)
                return EvaluateTerminator(in view, t, order, rule, true, ref cache, derivatives, out report);

            report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, AlgorithmStatus.InvalidInput,
                ICurveConstraintPlan.Auto, ChartSide.Right, -1, 0, 0);
            return AlgorithmStatus.InvalidInput;
        }

        // Exact chart node: the original point is the answer (§5.4); nearby
        // parameters are never snapped here.
        if (OriginalChartParameterMap.IsChartNode(parameters, t))
            return EvaluateChartPoint(in view, t, order, plan, ref cache, derivatives, out report);

        return EvaluateRegularInterval(in view, t, order, plan, ref cache, derivatives, out report);
    }

    /// <summary>Chart node contract: exact position, one-sided derivatives, no averaging.</summary>
    private static AlgorithmStatus EvaluateChartPoint(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, scoped ref EvaluationSampleStore cache, scoped Span<KernelVector3> derivatives,
        out ICurveEvalReport report)
    {
        // The right side is the published derivative side at an interior node.
        var locateStatus = OriginalChartParameterMap.LocateSegment(view.ChartParameters, t, ChartSide.Right, out var segment);
        if (locateStatus != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, locateStatus, plan, ChartSide.Right, -1, 0, 0);
            return locateStatus;
        }
        // The last node belongs to segment [m−2, m−1] but its anchor is the
        // final chart position itself — LocateSegment's segment is a
        // derivative-side locator, never a position index shortcut.
        var anchorIndex = t == view.ChartParameters[^1] ? view.ChartPositions.Length - 1 : segment;

        if (order == 0)
        {
            // The original anchor does not depend on a solver. An illegal plan
            // enumerator is still rejected, on this entry and on L3.
            var legal = ICurveConstraintPlanRules.ValidateEnumerator(plan);
            if (legal != AlgorithmStatus.Success)
            {
                report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, legal, plan, ChartSide.Right, segment, 0, 0);
                return legal;
            }
            derivatives[0] = view.ChartPositions[anchorIndex];
            report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, AlgorithmStatus.Success,
                plan, ChartSide.Right, segment, 0, 0, CacheHitKind.Exact);
            return AlgorithmStatus.Success;
        }

        // Derivatives reuse the selected plan's defining system, entered at the
        // node; the published D0 stays the original anchor regardless.
        var selected = plan == ICurveConstraintPlan.Auto ? ICurveConstraintPlanRules.Select(in view) : plan;
        var capability = ICurveConstraintPlanRules.ValidateRequest(in view, plan);
        if (capability != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, capability, plan, ChartSide.Right, segment, 0, 0);
            return capability;
        }
        if (cache.TryFindExact(t, ICurveQueryKind.ChartPoint, ChartSide.Right, order, CacheErrorBound, out var hit))
        {
            PublishHit(in hit, order, derivatives);
            report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, AlgorithmStatus.Success,
                hit.Plan, ChartSide.Right, segment, 0, hit.ErrorEstimate, CacheHitKind.Exact);
            return AlgorithmStatus.Success;
        }
        var seed = view.ChartPositions[anchorIndex];
        var solveStatus = SolveDirect(in view, in seed, t, segment, order, selected, derivatives,
            out var iterations, out var residual);
        report = new ICurveEvalReport(ICurveQueryKind.ChartPoint, solveStatus,
            selected, ChartSide.Right, segment, iterations, residual);
        if (solveStatus != AlgorithmStatus.Success) return solveStatus;
        derivatives[0] = seed;
        _ = cache.TryInsert(new CurveSample(t, in seed, derivatives[1],
            order >= 2 ? derivatives[2] : default, order, ICurveQueryKind.ChartPoint,
            ChartSide.Right, segment, residual, SampleSourceKind.CorrectedRoot, selected));
        return AlgorithmStatus.Success;
    }

    /// <summary>Regular interval: classify, select a plan, solve on the seed.</summary>
    private static AlgorithmStatus EvaluateRegularInterval(in ICurveView view, double t, DerivativeOrder order,
        ICurveConstraintPlan plan, scoped ref EvaluationSampleStore cache, scoped Span<KernelVector3> derivatives,
        out ICurveEvalReport report)
    {
        var locateStatus = OriginalChartParameterMap.LocateSegment(view.ChartParameters, t, ChartSide.Right, out var segment);
        if (locateStatus != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, locateStatus,
                plan, ChartSide.Right, -1, 0, 0);
            return locateStatus;
        }

        // Seed: the chord point Q(t) — inside the parameter plane by construction.
        var next = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
        var lambda = (t - view.ChartParameters[segment]) / next;
        var seed = Add(
            Scale(view.ChartPositions[segment], 1 - lambda),
            Scale(view.ChartPositions[segment + 1], lambda));

        var selected = plan == ICurveConstraintPlan.Auto ? ICurveConstraintPlanRules.Select(in view) : plan;
        var capability = ICurveConstraintPlanRules.ValidateRequest(in view, plan);
        if (capability != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, capability,
                plan, ChartSide.Right, segment, 0, 0);
            return capability;
        }
        if (cache.TryFindExact(t, ICurveQueryKind.RegularChartInterval, ChartSide.Right, order, CacheErrorBound, out var hit))
        {
            PublishHit(in hit, order, derivatives);
            report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, AlgorithmStatus.Success,
                hit.Plan, ChartSide.Right, segment, 0, hit.ErrorEstimate, CacheHitKind.Exact);
            return AlgorithmStatus.Success;
        }
        if (selected == ICurveConstraintPlan.I1
            && (view.Support0.Kind == SurfaceClass.Plane) != (view.Support1.Kind == SurfaceClass.Plane))
            return EvaluateI1ByContinuation(in view, t, segment, order, ref cache,
                derivatives, out report);

        // Neighbor prediction as the seed when the atlas brackets the request;
        // the predicted seed is corrected below before anything publishes and
        // falls back to the chord point when unavailable (§13.4).
        var hitKind = CacheHitKind.None;
        var solveSeed = seed;
        if (cache.TryFindBracket(t, ICurveQueryKind.RegularChartInterval, ChartSide.Right,
                segment, out var lower, out var upper))
        {
            Span<double> predicted = stackalloc double[3];
            if (ParameterCorrespondence.TryHermitePredict(lower.Parameter,
                    stackalloc[] { lower.Position.X, lower.Position.Y, lower.Position.Z },
                    stackalloc[] { lower.First.X, lower.First.Y, lower.First.Z },
                    upper.Parameter,
                    stackalloc[] { upper.Position.X, upper.Position.Y, upper.Position.Z },
                    stackalloc[] { upper.First.X, upper.First.Y, upper.First.Z },
                    t, predicted) == AlgorithmStatus.Success)
            {
                solveSeed = Vector(predicted[0], predicted[1], predicted[2]);
                hitKind = CacheHitKind.NeighborSeed;
                // PredictedOnly seed — never an exact hit (§13.4).
                _ = cache.TryInsert(new CurveSample(t, in solveSeed, default, default, 0,
                    ICurveQueryKind.RegularChartInterval, ChartSide.Right, segment,
                    double.PositiveInfinity, SampleSourceKind.PredictedOnly, selected));
            }
        }
        // One budget owns the preferred plan, every alternate, and any
        // continuation that follows. A convenience SolveDirect would mint a
        // fresh 4096 for each of those stages.
        var budget = EvaluationBudget.Default;
        var status = SolveDirect(in view, in solveSeed, t, segment, order, selected, ref budget,
            derivatives, out var iterations, out var residual, out var detail);
        if (detail == ICurveEvalDetail.BudgetExceeded)
        {
            report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, status,
                selected, ChartSide.Right, segment, iterations, residual, hitKind, 0, detail);
            return status;
        }
        // Diagnosed Auto switches only — forced plans stay on the requested plan (§7 / §14.6).
        if (plan == ICurveConstraintPlan.Auto
            && status is AlgorithmStatus.NotConverged or AlgorithmStatus.Singular
                or AlgorithmStatus.NumericalFailure)
        {
            Span<ICurveConstraintPlan> alternates = stackalloc ICurveConstraintPlan[5];
            var altCount = ICurveConstraintPlanRules.Alternates(in view, selected, alternates);
            for (BufferOffset ai = 0; ai < altCount; ai++)
            {
                var altStatus = SolveDirect(in view, in solveSeed, t, segment, order, alternates[ai],
                    ref budget, derivatives, out iterations, out residual, out detail);
                if (altStatus == AlgorithmStatus.Success)
                {
                    selected = alternates[ai];
                    status = altStatus;
                    detail = ICurveEvalDetail.PlanSwitched;
                    break;
                }
                if (detail == ICurveEvalDetail.BudgetExceeded)
                {
                    status = altStatus;
                    break;
                }
            }
        }
        if (status == AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, status,
                selected, ChartSide.Right, segment, iterations, residual, hitKind, 0, detail);
            _ = cache.TryInsert(new CurveSample(t, derivatives[0],
                order >= 1 ? derivatives[1] : default,
                order >= 2 ? derivatives[2] : default, order, ICurveQueryKind.RegularChartInterval,
                ChartSide.Right, segment, residual, SampleSourceKind.CorrectedRoot, selected));
            return status;
        }

        // Continuation only recovers numerical/seed failures. Capability refusals
        // and structural singularities stay as-is — subdivision must not rewrite
        // "plan cannot run" into Stagnation/BudgetExceeded (§7 preamble, §17).
        if (detail == ICurveEvalDetail.BudgetExceeded
            || status is not (AlgorithmStatus.NotConverged or AlgorithmStatus.NumericalFailure))
        {
            report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, status,
                selected, ChartSide.Right, segment, iterations, residual, hitKind, 0, detail);
            return status;
        }

        // Direct solve failed: parameter continuation / local subdivision from a
        // verified anchor inside the same original segment (§17.3–§17.5). The
        // same request budget covers predictor, corrector and midpoint probes.
        KernelVector3 anchorPosition;
        double anchorParameter;
        if (cache.TryFindNearest(t, ICurveQueryKind.RegularChartInterval, ChartSide.Right, segment,
                out var nearest))
        {
            anchorParameter = nearest.Parameter;
            anchorPosition = nearest.Position;
        }
        else
        {
            // Chart endpoints are defining D0 anchors (§5.4).
            var useHi = Math.Abs(view.ChartParameters[segment + 1] - t)
                < Math.Abs(t - view.ChartParameters[segment]);
            anchorParameter = useHi ? view.ChartParameters[segment + 1] : view.ChartParameters[segment];
            anchorPosition = useHi ? view.ChartPositions[segment + 1] : view.ChartPositions[segment];
        }

        var contStatus = ICurveContinuation.ContinueTo(in view, selected, anchorParameter,
            in anchorPosition, t, segment, order, ref budget, derivatives, out var contSteps,
            out residual, out detail);
        if (contStatus != AlgorithmStatus.Success)
        {
            contStatus = ICurveContinuation.SubdivideTo(in view, selected, t, segment, order, ref budget,
                derivatives, out contSteps, out residual, out detail);
        }

        iterations = contSteps;
        report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, contStatus,
            selected, ChartSide.Right, segment, iterations, residual, CacheHitKind.NeighborSeed,
            0, detail);
        if (contStatus != AlgorithmStatus.Success) return contStatus;
        _ = cache.TryInsert(new CurveSample(t, derivatives[0],
            order >= 1 ? derivatives[1] : default,
            order >= 2 ? derivatives[2] : default, order, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, segment, residual, SampleSourceKind.CorrectedRoot, selected));
        return contStatus;
    }

    private static AlgorithmStatus EvaluateI1ByContinuation(in ICurveView view, double t,
        BufferOffset segment, DerivativeOrder order, scoped ref EvaluationSampleStore cache,
        scoped Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        var useUpper = t - view.ChartParameters[segment]
            > view.ChartParameters[segment + 1] - t;
        var index = useUpper ? segment + 1 : segment;
        var anchor = view.ChartPositions[index];
        var budget = EvaluationBudget.Default;
        var status = ICurveContinuation.ContinueTo(in view, ICurveConstraintPlan.I1,
            view.ChartParameters[index], in anchor, t, segment, order, ref budget,
            derivatives, out var steps, out var residual, out var detail);
        if (status != AlgorithmStatus.Success)
            status = ICurveContinuation.SubdivideTo(in view, ICurveConstraintPlan.I1,
                t, segment, order, ref budget, derivatives, out steps, out residual, out detail);
        report = new ICurveEvalReport(ICurveQueryKind.RegularChartInterval, status,
            ICurveConstraintPlan.I1, ChartSide.Right, segment, steps, residual,
            CacheHitKind.None, 0, detail);
        if (status != AlgorithmStatus.Success) return status;
        _ = cache.TryInsert(new CurveSample(t, derivatives[0],
            order >= 1 ? derivatives[1] : default,
            order >= 2 ? derivatives[2] : default, order,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, segment,
            residual, SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I1));
        return AlgorithmStatus.Success;
    }

    /// <summary>Sample error bound for exact-hit acceptance (slice default).</summary>
    internal const double CacheErrorBound = 1e-11;

    /// <summary>
    /// Terminator queries beyond a chart boundary (§6): classification has
    /// already selected the end; this path resolves the GATE-T parameter rule,
    /// honors the exact-terminator D0 contract, and solves the one-surface /
    /// two-planes interval construction with its §16.3 scalar derivatives. The
    /// unselected support's deviation is published as a diagnostic only (§6.4)
    /// — never added as a fourth defining equation.
    /// </summary>
    private static AlgorithmStatus EvaluateTerminator(in ICurveView view, double t, DerivativeOrder order,
        TerminatorParameterRule rule, bool isEnd, scoped ref EvaluationSampleStore cache,
        scoped Span<KernelVector3> derivatives, out ICurveEvalReport report)
    {
        var kind = isEnd ? ICurveQueryKind.EndTerminatorInterval : ICurveQueryKind.StartTerminatorInterval;
        var side = isEnd ? ChartSide.Right : ChartSide.Left;
        var segment = isEnd ? view.ChartParameters.Length - 2 : 0;

        // GATE-T: without an explicitly selected reconstruction rule there is
        // no defined parameter beyond the chart boundary (§6.5).
        if (rule == TerminatorParameterRule.Unresolved)
        {
            report = new ICurveEvalReport(kind, AlgorithmStatus.Unsupported, ICurveConstraintPlan.Auto,
                side, segment, 0, 0, CacheHitKind.None, 0, ICurveEvalDetail.CompatibilityGateOpen);
            return AlgorithmStatus.Unsupported;
        }

        var limit = isEnd ? view.EndTerminator : view.StartTerminator;
        var resolveStatus = TerminatorEvaluation.TryResolveTerminatorParameter(in view, isEnd, rule,
            in limit.Endpoint, in limit.BranchPoint, out var terminatorParameter);
        if (resolveStatus != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(kind, resolveStatus, ICurveConstraintPlan.Auto, side, segment, 0, 0);
            return resolveStatus;
        }

        // The terminator itself: D0 is the defining position, bit-exact like a
        // chart anchor (§6.5). Higher orders stay behind the open endpoint
        // contract and never publish a partial result (§18.5).
        if (t == terminatorParameter)
        {
            if (order > 0)
            {
                report = new ICurveEvalReport(ICurveQueryKind.ExactTerminator, AlgorithmStatus.Unsupported,
                    ICurveConstraintPlan.Auto, side, segment, 0, 0, CacheHitKind.None, 0,
                    ICurveEvalDetail.CompatibilityGateOpen);
                return AlgorithmStatus.Unsupported;
            }
            derivatives[0] = limit.Endpoint;
            _ = cache.TryInsert(new CurveSample(t, in limit.Endpoint, default, default, 0,
                ICurveQueryKind.ExactTerminator, side, segment, 0, SampleSourceKind.ImportedChartAnchor,
                ICurveConstraintPlan.Auto));
            report = new ICurveEvalReport(ICurveQueryKind.ExactTerminator, AlgorithmStatus.Success,
                ICurveConstraintPlan.Auto, side, segment, 0, 0);
            return AlgorithmStatus.Success;
        }
        if (isEnd ? t > terminatorParameter : t < terminatorParameter)
        {
            // Beyond the resolved terminator: no defined geometry.
            report = new ICurveEvalReport(ICurveQueryKind.OutsideSupportedDomain, AlgorithmStatus.InvalidInput,
                ICurveConstraintPlan.Auto, side, segment, 0, 0);
            return AlgorithmStatus.InvalidInput;
        }

        var prepareStatus = TerminatorEvaluation.Prepare(in view, isEnd, in limit.Endpoint,
            in limit.BranchPoint, limit.TermUse, terminatorParameter, out var anchor);
        if (prepareStatus != AlgorithmStatus.Success)
        {
            report = new ICurveEvalReport(kind, prepareStatus, ICurveConstraintPlan.Auto, side, segment, 0, 0);
            return prepareStatus;
        }

        if (cache.TryFindExact(t, kind, side, order, CacheErrorBound, out var hit))
        {
            PublishHit(in hit, order, derivatives);
            report = new ICurveEvalReport(kind, AlgorithmStatus.Success, hit.Plan, side, segment,
                0, hit.ErrorEstimate, CacheHitKind.Exact);
            return AlgorithmStatus.Success;
        }

        var selectedSurface = anchor.SelectedSurface == 0 ? view.Support0 : view.Support1;
        var otherSurface = anchor.SelectedSurface == 0 ? view.Support1 : view.Support0;
        var budget = EvaluationBudget.Default;
        var solveStatus = TerminatorEvaluation.SolveIntervalPoint(in selectedSurface, in anchor, t,
            ref budget, out _, out var point, out var residual, out var evaluations);
        if (solveStatus != AlgorithmStatus.Success)
        {
            var detail = solveStatus == AlgorithmStatus.NotConverged && budget.Remaining == 0
                ? ICurveEvalDetail.BudgetExceeded
                : ICurveEvalDetail.None;
            report = new ICurveEvalReport(kind, solveStatus, ICurveConstraintPlan.Auto,
                side, segment, evaluations, residual, CacheHitKind.None, 0, detail);
            return solveStatus;
        }

        // Non-defining support deviation: diagnostics, not a constraint (§6.4).
        var nonDefining = 0.0;
        if (AnalyticImplicitEvaluation.Evaluate(in otherSurface, in point, 0, out var otherJet)
            == AlgorithmStatus.Success)
            nonDefining = Math.Abs(otherJet.Value);

        var first = default(KernelVector3);
        var second = default(KernelVector3);
        if (order >= 1)
        {
            var derivativeStatus = TerminatorEvaluation.IntervalDerivatives(in selectedSurface,
                in anchor, in point, order, out first, out second);
            if (derivativeStatus != AlgorithmStatus.Success)
            {
                report = new ICurveEvalReport(kind, derivativeStatus, ICurveConstraintPlan.Auto,
                    side, segment, evaluations, residual, CacheHitKind.None, nonDefining);
                return derivativeStatus;
            }
        }

        derivatives[0] = point;
        if (order >= 1) derivatives[1] = first;
        if (order >= 2) derivatives[2] = second;
        _ = cache.TryInsert(new CurveSample(t, in point, first, second, order, kind,
            side, segment, residual, SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.Auto));
        report = new ICurveEvalReport(kind, AlgorithmStatus.Success, ICurveConstraintPlan.Auto,
            side, segment, evaluations, residual, CacheHitKind.None, nonDefining);
        return AlgorithmStatus.Success;
    }

    private static void PublishHit(in CurveSample hit, DerivativeOrder order, scoped Span<KernelVector3> derivatives)
    {
        derivatives[0] = hit.Position;
        if (order >= 1) derivatives[1] = hit.First;
        if (order >= 2) derivatives[2] = hit.Second;
    }

    /// <summary>
    /// Direct plan solve without continuation/subdivision fallback. Used by the
    /// continuation module so a failed regular-interval request cannot recurse
    /// into itself through <see cref="EvaluateWithPlan"/> (spec §17.3).
    /// </summary>
    internal static AlgorithmStatus SolveDirect(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ICurveConstraintPlan plan,
        scoped Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual)
    {
        var budget = EvaluationBudget.Default;
        return SolveDirect(in view, in seed, t, segment, order, plan, ref budget,
            derivatives, out iterations, out residual, out _);
    }

    /// <summary>
    /// Same solve, drawing every implicit jet and geometric deviation from
    /// <paramref name="budget"/>. The caller span is written only after the
    /// final publication gate accepts the staged jet.
    /// </summary>
    internal static AlgorithmStatus SolveDirect(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ICurveConstraintPlan plan,
        ref EvaluationBudget budget, scoped Span<KernelVector3> derivatives,
        out BufferOffset iterations, out double residual, out ICurveEvalDetail detail)
    {
        Span<KernelVector3> staged = stackalloc KernelVector3[3];
        var status = Solve(in view, in seed, t, segment, order, plan, ref budget, staged,
            out iterations, out residual, out detail);
        if (status != AlgorithmStatus.Success) return status;
        derivatives[0] = staged[0];
        if (order >= 1) derivatives[1] = staged[1];
        if (order >= 2) derivatives[2] = staged[2];
        return AlgorithmStatus.Success;
    }

    /// <summary>Finite switch over the plans; no residual callbacks cross this boundary (§19.4).</summary>
    private static AlgorithmStatus Solve(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ICurveConstraintPlan plan,
        ref EvaluationBudget budget, scoped Span<KernelVector3> derivatives,
        out BufferOffset iterations, out double residual, out ICurveEvalDetail detail)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        var status = plan switch
        {
            ICurveConstraintPlan.I1 => SolveI1(in view, in seed, t, segment, order, ref budget, derivatives, out iterations, out residual, out detail),
            ICurveConstraintPlan.P2 => SolveP2(in view, in seed, t, segment, order, ref budget, derivatives, out iterations, out residual, out detail),
            ICurveConstraintPlan.I3 => SolveI3(in view, in seed, t, segment, order, ref budget, derivatives, out iterations, out residual, out detail),
            ICurveConstraintPlan.I2 => SolveI2(in view, in seed, t, segment, order, ref budget, derivatives, out iterations, out residual, out detail),
            ICurveConstraintPlan.P4 => SolveP4(in view, in seed, t, segment, order, ref budget, derivatives, out iterations, out residual, out detail),
            _ => AlgorithmStatus.InvalidInput,
        };
        if (status != AlgorithmStatus.Success) return status;
        if (IsPublishableRoot(in view, t, segment, in derivatives[0], ref budget, out detail))
            return AlgorithmStatus.Success;
        return AlgorithmStatus.NotConverged;
    }

    /// <summary>
    /// Independent publication gate (§18.1). All quantities have length units
    /// and the scale is local geometry, never the world-coordinate norm.
    /// </summary>
    internal static bool IsPublishableRoot(in ICurveView view, double t, BufferOffset segment,
        in KernelVector3 point)
    {
        var budget = EvaluationBudget.Default;
        return IsPublishableRoot(in view, t, segment, in point, ref budget, out _);
    }

    /// <summary>
    /// Publication gate that charges each support deviation and implicit jet
    /// against the caller's shared budget. A failed charge is
    /// <see cref="ICurveEvalDetail.BudgetExceeded"/>, not an ordinary rejection.
    /// </summary>
    internal static bool IsPublishableRoot(in ICurveView view, double t, BufferOffset segment,
        in KernelVector3 point, ref EvaluationBudget budget, out ICurveEvalDetail detail)
    {
        detail = ICurveEvalDetail.None;
        if (!TryGeometricDeviation(in view.Support0, in point, ref budget, out var d0, out detail)
            || !TryGeometricDeviation(in view.Support1, in point, ref budget, out var d1, out detail)
            || !TryImplicitJet(in view.Support0, in point, 1, ref budget, out var jet0, out detail)
            || !TryImplicitJet(in view.Support1, in point, 1, ref budget, out var jet1, out detail))
            return false;
        var tolerance = PublicationTolerance(in view);
        var planeResidual = OriginalChartParameterMap.PlaneResidual(
            view.ChartPositions, view.ChartParameters, view.ChartScales,
            view.ChartChordUnits, segment, t, in point);
        if (d0 > tolerance || d1 > tolerance || Math.Abs(planeResidual) > tolerance)
            return false;
        var oriented0 = view.Sense0 == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? Scale(jet0.Gradient, -1) : jet0.Gradient;
        var oriented1 = view.Sense1 == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? Scale(jet1.Gradient, -1) : jet1.Gradient;
        var tangent = Unit(Cross(oriented0, oriented1));
        if (!IsFinite(tangent)
            || !HasChartConsistentDirection(in view, segment, in tangent, ref budget, out detail))
            return false;
        if (!IsSameAnalyticBranch(in view.Support0, view.ChartPositions, segment, in point, tolerance)
            || !IsSameAnalyticBranch(in view.Support1, view.ChartPositions, segment, in point, tolerance))
            return false;

        // Residuals alone do not bound position error near tangency. Estimate
        // the Newton correction of the complete three-constraint system and
        // publish only when its forward-error proxy is also locally small.
        // The implicit values must already carry intermediate construction
        // error (cone generator low part, compensated squares). A correction
        // of the rounded residual is not the forward error of the definition.
        Span<double> jacobian = stackalloc double[9]
        {
            jet0.Gradient.X, jet0.Gradient.Y, jet0.Gradient.Z,
            jet1.Gradient.X, jet1.Gradient.Y, jet1.Gradient.Z,
            view.ChartChordUnits[segment].X,
            view.ChartChordUnits[segment].Y,
            view.ChartChordUnits[segment].Z,
        };
        Span<double> correction = stackalloc double[3]
        {
            -jet0.Value, -jet1.Value, -planeResidual,
        };
        Span<int> pivots = stackalloc int[3];
        if (SmallLinearSolve.LuFactorize(jacobian, 3, pivots) != AlgorithmStatus.Success
            || SmallLinearSolve.LuSolveInPlace(jacobian, 3, pivots, correction)
                != AlgorithmStatus.Success)
            return false;
        return SmallLinearSolve.Norm(correction) <= tolerance;
    }

    private static bool TryCharge(ref EvaluationBudget budget, out ICurveEvalDetail detail)
    {
        detail = ICurveEvalDetail.None;
        if (budget.TryConsume(1)) return true;
        detail = ICurveEvalDetail.BudgetExceeded;
        return false;
    }

    private static bool TryGeometricDeviation(in AnalyticSurface surface, in KernelVector3 point,
        ref EvaluationBudget budget, out double deviation, out ICurveEvalDetail detail)
    {
        deviation = 0;
        if (!TryCharge(ref budget, out detail)) return false;
        return AnalyticImplicitEvaluation.GeometricDeviation(in surface, in point, out deviation)
            == AlgorithmStatus.Success;
    }

    private static bool TryImplicitJet(in AnalyticSurface surface, in KernelVector3 point,
        DerivativeOrder order, ref EvaluationBudget budget, out ImplicitJet jet, out ICurveEvalDetail detail)
    {
        jet = default;
        if (!TryCharge(ref budget, out detail)) return false;
        return AnalyticImplicitEvaluation.Evaluate(in surface, in point, order, out jet)
            == AlgorithmStatus.Success;
    }

    private static bool HasChartConsistentDirection(in ICurveView view, BufferOffset segment,
        in KernelVector3 candidateTangent, ref EvaluationBudget budget, out ICurveEvalDetail detail)
    {
        detail = ICurveEvalDetail.None;
        var anchor = view.ChartPositions[segment];
        if (!TryImplicitJet(in view.Support0, in anchor, 1, ref budget, out var anchor0, out detail)
            || !TryImplicitJet(in view.Support1, in anchor, 1, ref budget, out var anchor1, out detail))
            return false;
        var oriented0 = view.Sense0 == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? Scale(anchor0.Gradient, -1) : anchor0.Gradient;
        var oriented1 = view.Sense1 == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? Scale(anchor1.Gradient, -1) : anchor1.Gradient;
        var anchorTangent = Unit(Cross(oriented0, oriented1));
        if (!IsFinite(anchorTangent)) return false;
        var chord = view.ChartChordUnits[segment];
        var anchorProjection = Dot(anchorTangent, chord);
        var candidateProjection = Dot(candidateTangent, chord);
        return anchorProjection * candidateProjection > 0;
    }

    private static bool IsSameAnalyticBranch(in AnalyticSurface surface,
        ReadOnlySpan<KernelVector3> chart, BufferOffset segment,
        in KernelVector3 candidate, double tolerance)
    {
        if (surface.Kind != SurfaceClass.Torus) return true;
        var lo = TorusProfileCoordinate(in surface, in chart[segment]);
        var hi = TorusProfileCoordinate(in surface, in chart[segment + 1]);
        if (Math.Abs(lo) <= tolerance || Math.Abs(hi) <= tolerance || Math.Sign(lo) != Math.Sign(hi))
            return true;
        var value = TorusProfileCoordinate(in surface, in candidate);
        return Math.Abs(value) <= tolerance || Math.Sign(value) == Math.Sign(lo);
    }

    private static double TorusProfileCoordinate(in AnalyticSurface surface,
        in KernelVector3 point)
    {
        var relative = Sub(point, surface.Origin);
        var axial = Dot(surface.Axis, relative);
        var radial = Sub(relative, Scale(surface.Axis, axial));
        return Math.Sqrt(Dot(radial, radial)) - surface.Radius;
    }

    internal static double PublicationTolerance(in ICurveView view)
    {
        var localScale = Math.Max(1.0, Math.Max(
            Math.Max(view.Support0.Radius, view.Support0.Secondary),
            Math.Max(view.Support1.Radius, view.Support1.Secondary)));
        return ResidualTolerance * localScale;
    }

    /// <summary>x′ᵀHx′ via the analytic Hessian, contracted without materializing the tensor (§16.2).</summary>
    private static double SecondDirectional(in AnalyticSurface surface, in KernelVector3 root,
        in KernelVector3 direction, ref EvaluationBudget budget, out ICurveEvalDetail detail)
    {
        if (!TryImplicitJet(in surface, in root, 2, ref budget, out var jet, out detail))
            return double.NaN;
        return jet.Hxx * direction.X * direction.X
            + 2 * jet.Hxy * direction.X * direction.Y
            + 2 * jet.Hxz * direction.X * direction.Z
            + jet.Hyy * direction.Y * direction.Y
            + 2 * jet.Hyz * direction.Y * direction.Z
            + jet.Hzz * direction.Z * direction.Z;
    }

    // ── I1: plane support + implicit other (§7.5) ────────────────

    private static AlgorithmStatus SolveI1(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ref EvaluationBudget budget,
        scoped Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual,
        out ICurveEvalDetail detail)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        var planeIsSupport0 = view.Support0.Kind == SurfaceClass.Plane;
        var planeIsSupport1 = view.Support1.Kind == SurfaceClass.Plane;
        if (planeIsSupport0 == planeIsSupport1) return AlgorithmStatus.Unsupported; // needs exactly one plane

        var n = planeIsSupport0 ? view.Support0.Axis : view.Support1.Axis;
        var c = planeIsSupport0 ? view.Support0.Origin : view.Support1.Origin;
        var other = planeIsSupport0 ? view.Support1 : view.Support0;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];

        // Intersection line of the support plane (fixed) and the parameter
        // plane through Q(t): direction b = unit(n × e); base point
        // x0(t) = Q + α·n′ with n′ = n − (n·e)e and α = n·(c−Q)/|n×e|²
        // (projecting along n instead would divide by the identically zero
        // n·(n×e)).
        var cross = Cross(n, chordUnit);
        var crossNormSq = Dot(cross, cross);
        if (!(crossNormSq > 1e-24)) return AlgorithmStatus.Singular; // near-parallel planes
        var b = Scale(cross, 1 / Math.Sqrt(crossNormSq));
        var nTilde = Sub(n, Scale(chordUnit, Dot(n, chordUnit)));

        var next = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
        var lambda = (t - view.ChartParameters[segment]) / next;
        var q = Add(
            Scale(view.ChartPositions[segment], 1 - lambda),
            Scale(view.ChartPositions[segment + 1], lambda));
        var alpha = Dot(n, Sub(c, q)) / crossNormSq;
        var x0 = Add(q, Scale(nTilde, alpha));

        // Preserve branch evidence from the caller's predictor by projecting it
        // onto the current support-plane/parameter-plane line.
        var mu = Dot(Sub(seed, x0), b);
        var gradient = default(KernelVector3);
        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxFastNewtonIterations; iteration++)
        {
            iterations = iteration + 1;
            var point = Add(x0, Scale(b, mu));
            if (!TryImplicitJet(in other, in point, 1, ref budget, out var jet, out detail))
                return detail == ICurveEvalDetail.BudgetExceeded
                    ? AlgorithmStatus.NotConverged
                    : AlgorithmStatus.Unsupported;
            gradient = jet.Gradient;
            residual = Math.Abs(jet.Value);
            if (IsPublishableRoot(in view, t, segment, in point, ref budget, out detail))
            {
                converged = true;
                break;
            }
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            var slope = Dot(gradient, b);
            if (!(Math.Abs(slope) > 1e-300)) return AlgorithmStatus.Singular;
            mu -= jet.Value / slope;
            if (!double.IsFinite(mu)) return AlgorithmStatus.NumericalFailure;
        }
        if (!converged)
        {
            Span<double> refined = stackalloc double[1];
            var refine = ICurveCorrection.Refine(in view, ICurveConstraintPlan.I1, t, segment,
                in seed, refined, ref budget, out var refineIterations, out residual, out detail);
            if (refine != AlgorithmStatus.Success) return refine;
            iterations = MaxFastNewtonIterations + refineIterations;
            mu = refined[0];
        }

        var root = Add(x0, Scale(b, mu));
        derivatives[0] = root;
        if (!converged && order >= 1)
        {
            // The fallback corrector moved μ; the derivative chain needs the
            // gradient at the refined root, not the abandoned fast-loop point.
            if (!TryImplicitJet(in other, in root, 1, ref budget, out var rootJet, out detail))
                return detail == ICurveEvalDetail.BudgetExceeded
                    ? AlgorithmStatus.NotConverged
                    : AlgorithmStatus.Unsupported;
            gradient = rootJet.Gradient;
        }

        // D1: x0′ = Q′ + α′n′ with Q′ = e/f and α′ = −(n·Q′)/|n×e|²; the
        // scalar chain μ′ = −(∇φ·x0′)/(∇φ·b).
        var qPrime = Scale(chordUnit, 1 / scale);
        var x0Prime = Add(qPrime, Scale(nTilde, -Dot(n, qPrime) / crossNormSq));
        if (order >= 1)
        {
            var slope = Dot(gradient, b);
            var muPrime = -Dot(gradient, x0Prime) / slope;
            derivatives[1] = Add(x0Prime, Scale(b, muPrime));
            if (!IsFinite(derivatives[1])) return AlgorithmStatus.NumericalFailure;
        }

        // D2: μ″ = −(x′ᵀHx′)/(∇φ·b); x0″ = 0.
        if (order >= 2)
        {
            var xPrime = derivatives[1];
            var curvature = SecondDirectional(in other, in root, in xPrime, ref budget, out detail);
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            var muDoublePrime = -curvature / Dot(gradient, b);
            derivatives[2] = Scale(b, muDoublePrime);
            if (!IsFinite(derivatives[2])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    // ── P2: parametric side + implicit other (§7.2) ──────────────

    private static AlgorithmStatus SolveP2(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ref EvaluationBudget budget,
        scoped Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual,
        out ICurveEvalDetail detail)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        // The parametric side is support0 in this slice (both analytic
        // supports qualify; a flipped-side variant arrives with non-analytic
        // supports in later tasks).
        if (AnalyticParametricEvaluation.TryRecoverWitness(in view.Support0, in seed, out var u0, out var v0)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];

        Span<double> q = stackalloc double[2] { u0, v0 };
        Span<double> jacobian = stackalloc double[4];
        Span<double> jacobianCopy = stackalloc double[4];
        Span<double> residualVector = stackalloc double[2];
        Span<double> step = stackalloc double[2];
        Span<double> model = stackalloc double[2];
        Span<int> pivots = stackalloc int[2];
        Span<KernelVector3> jet = stackalloc KernelVector3[4]; // layout(1,1): S, Su, Sv, Suv
        if (!SurfaceDerivativeLayout.TryCreate(1, 1, out var layout1))
            return AlgorithmStatus.InvalidInput;

        var gradient1 = default(KernelVector3);
        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxFastNewtonIterations; iteration++)
        {
            iterations = iteration + 1;
            if (!TryCharge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
            if (SurfaceEvaluation.Evaluate(in view.Support0, q[0], q[1], in layout1, jet) != AlgorithmStatus.Success)
                return AlgorithmStatus.NotConverged; // trial left the valid parameter domain
            if (!TryImplicitJet(in view.Support1, in jet[0], 1, ref budget, out var jet1, out detail))
                return detail == ICurveEvalDetail.BudgetExceeded
                    ? AlgorithmStatus.NotConverged
                    : AlgorithmStatus.Unsupported;
            gradient1 = jet1.Gradient;

            residualVector[0] = jet1.Value;
            residualVector[1] = OriginalChartParameterMap.PlaneResidual(
                view.ChartPositions, view.ChartParameters, view.ChartScales, view.ChartChordUnits, segment, t, in jet[0]);
            // Layout index: GetIndex(u,v) = u·(VOrder+1)+v, so Su is (1,0) and Sv is (0,1).
            var su1 = jet[layout1.GetIndex(1, 0)];
            var sv1 = jet[layout1.GetIndex(0, 1)];
            jacobian[0] = Dot(gradient1, su1); jacobian[1] = Dot(gradient1, sv1);
            jacobian[2] = Dot(chordUnit, su1); jacobian[3] = Dot(chordUnit, sv1);

            residual = SmallLinearSolve.Norm(residualVector);
            if (IsPublishableRoot(in view, t, segment, in jet[0], ref budget, out detail))
            {
                converged = true;
                break;
            }
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            var stepStatus = NewtonStep.ComputeStep(jacobian, jacobianCopy, residualVector, 2, pivots, model, step, out var predicted);
            if (stepStatus != AlgorithmStatus.Success || !(predicted > 0))
                return stepStatus == AlgorithmStatus.Success ? AlgorithmStatus.Singular : stepStatus;
            q[0] += step[0];
            q[1] += step[1];
            if (!double.IsFinite(q[0]) || !double.IsFinite(q[1])) return AlgorithmStatus.NumericalFailure;
        }
        if (!converged)
        {
            var refine = ICurveCorrection.Refine(in view, ICurveConstraintPlan.P2, t, segment,
                in seed, q, ref budget, out var refineIterations, out residual, out detail);
            if (refine != AlgorithmStatus.Success) return refine;
            iterations = MaxFastNewtonIterations + refineIterations;
        }

        // Root jets at second order; one factorization serves D1 and D2 (§16.1).
        if (!SurfaceDerivativeLayout.TryCreate(2, 2, out var layout2))
            return AlgorithmStatus.InvalidInput;
        Span<KernelVector3> rootJet = stackalloc KernelVector3[9];
        if (!TryCharge(ref budget, out detail)) return AlgorithmStatus.NotConverged;
        if (SurfaceEvaluation.Evaluate(in view.Support0, q[0], q[1], in layout2, rootJet) != AlgorithmStatus.Success)
            return AlgorithmStatus.NotConverged;
        if (!TryImplicitJet(in view.Support1, in rootJet[0], order >= 2 ? 2 : 1, ref budget, out var rootJet1, out detail))
            return detail == ICurveEvalDetail.BudgetExceeded
                ? AlgorithmStatus.NotConverged
                : AlgorithmStatus.Unsupported;

        var rootSu = rootJet[layout2.GetIndex(1, 0)];
        var rootSv = rootJet[layout2.GetIndex(0, 1)];
        jacobian[0] = Dot(rootJet1.Gradient, rootSu); jacobian[1] = Dot(rootJet1.Gradient, rootSv);
        jacobian[2] = Dot(chordUnit, rootSu); jacobian[3] = Dot(chordUnit, rootSv);
        if (SmallLinearSolve.LuFactorize(jacobian, 2, pivots) != AlgorithmStatus.Success)
            return AlgorithmStatus.Singular;

        derivatives[0] = rootJet[0];

        if (order >= 1)
        {
            // J (u′,v′) = [0, 1/f]; x′ = Su u′ + Sv v′.
            Span<double> d1 = stackalloc double[2];
            d1[0] = 0; d1[1] = 1 / scale;
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 2, pivots, d1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[1] = Add(Scale(rootSu, d1[0]), Scale(rootSv, d1[1]));
            if (!IsFinite(derivatives[1])) return AlgorithmStatus.NumericalFailure;
        }

        if (order >= 2)
        {
            // A = Suu u′² + 2Suv u′v′ + Svv v′²;
            // J (u″,v″) = −[x′ᵀH₁x′ + ∇φ₁·A; e·A]; x″ = A + Su u″ + Sv v″.
            Span<double> d1 = stackalloc double[2];
            d1[0] = 0; d1[1] = 1 / scale;
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 2, pivots, d1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            var xPrime = Add(Scale(rootSu, d1[0]), Scale(rootSv, d1[1]));
            var second = ParametricSecondChain(in layout2, rootJet, d1[0], d1[1]);
            Span<double> d2 = stackalloc double[2];
            var curvature = SecondDirectional(in view.Support1, in rootJet[0], in xPrime, ref budget, out detail);
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            d2[0] = -(curvature + Dot(rootJet1.Gradient, second));
            d2[1] = -Dot(chordUnit, second);
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 2, pivots, d2) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[2] = Add(second, Add(Scale(rootSu, d2[0]), Scale(rootSv, d2[1])));
            if (!IsFinite(derivatives[2])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    // ── I3: implicit/implicit, 3×3 (§7.3) ────────────────────────

    private static AlgorithmStatus SolveI3(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ref EvaluationBudget budget,
        scoped Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual,
        out ICurveEvalDetail detail)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];

        Span<double> x = stackalloc double[3] { seed.X, seed.Y, seed.Z };
        Span<double> jacobian = stackalloc double[9];
        Span<double> jacobianCopy = stackalloc double[9];
        Span<double> residualVector = stackalloc double[3];
        Span<double> step = stackalloc double[3];
        Span<double> model = stackalloc double[3];
        Span<int> pivots = stackalloc int[3];

        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxFastNewtonIterations; iteration++)
        {
            iterations = iteration + 1;
            var point = Vector(x[0], x[1], x[2]);
            if (!TryImplicitJet(in view.Support0, in point, 1, ref budget, out var jet0, out detail)
                || !TryImplicitJet(in view.Support1, in point, 1, ref budget, out var jet1, out detail))
                return detail == ICurveEvalDetail.BudgetExceeded
                    ? AlgorithmStatus.NotConverged
                    : AlgorithmStatus.Unsupported;
            residualVector[0] = jet0.Value;
            residualVector[1] = jet1.Value;
            residualVector[2] = OriginalChartParameterMap.PlaneResidual(
                view.ChartPositions, view.ChartParameters, view.ChartScales, view.ChartChordUnits, segment, t, in point);

            jacobian[0] = jet0.Gradient.X; jacobian[1] = jet0.Gradient.Y; jacobian[2] = jet0.Gradient.Z;
            jacobian[3] = jet1.Gradient.X; jacobian[4] = jet1.Gradient.Y; jacobian[5] = jet1.Gradient.Z;
            jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;

            residual = SmallLinearSolve.Norm(residualVector);
            if (IsPublishableRoot(in view, t, segment, in point, ref budget, out detail))
            {
                converged = true;
                break;
            }
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;

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
        {
            var refine = ICurveCorrection.Refine(in view, ICurveConstraintPlan.I3, t, segment,
                in seed, x, ref budget, out var refineIterations, out residual, out detail);
            if (refine != AlgorithmStatus.Success) return refine;
            iterations = MaxFastNewtonIterations + refineIterations;
        }

        var root = Vector(x[0], x[1], x[2]);

        // Rebuild the true root Jacobian once, then serve every derivative
        // right side from this decomposition (§14.2, §16.1).
        if (!TryImplicitJet(in view.Support0, in root, order >= 2 ? 2 : 1, ref budget, out var rootJet0, out detail)
            || !TryImplicitJet(in view.Support1, in root, order >= 2 ? 2 : 1, ref budget, out var rootJet1, out detail))
            return detail == ICurveEvalDetail.BudgetExceeded
                ? AlgorithmStatus.NotConverged
                : AlgorithmStatus.Unsupported;
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
        if (order >= 1)
        {
            derivatives[1] = Vector(d1[0], d1[1], d1[2]);
            if (!IsFinite(derivatives[1])) return AlgorithmStatus.NumericalFailure;
        }

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

    // ── I2: implicit/implicit with the plane eliminated, 2×2 (§7.4) ──

    private static AlgorithmStatus SolveI2(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ref EvaluationBudget budget,
        scoped Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual,
        out ICurveEvalDetail detail)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];

        // In-plane orthonormal basis from the least-parallel coordinate axis,
        // fixed for the whole original segment (§7.4). The eliminated plane
        // passes through Q(t) — never through the seed, which may sit off the
        // plane when it comes from a cache prediction.
        var axis = LeastParallelAxis(in chordUnit);
        var u = Unit(Sub(axis, Scale(chordUnit, Dot(axis, chordUnit))));
        var v = Cross(chordUnit, u);
        if (!IsFinite(u) || !IsFinite(v)) return AlgorithmStatus.Singular;
        var next = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
        var lambda = (t - view.ChartParameters[segment]) / next;
        var planeBase = Add(
            Scale(view.ChartPositions[segment], 1 - lambda),
            Scale(view.ChartPositions[segment + 1], lambda));

        Span<double> xi = stackalloc double[2]
        {
            Dot(Sub(seed, planeBase), u),
            Dot(Sub(seed, planeBase), v),
        };
        Span<double> jacobian = stackalloc double[4];
        Span<double> jacobianCopy = stackalloc double[4];
        Span<double> residualVector = stackalloc double[2];
        Span<double> step = stackalloc double[2];
        Span<double> model = stackalloc double[2];
        Span<int> pivots = stackalloc int[2];

        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxFastNewtonIterations; iteration++)
        {
            iterations = iteration + 1;
            var point = Add(planeBase, Add(Scale(u, xi[0]), Scale(v, xi[1])));
            if (!TryImplicitJet(in view.Support0, in point, 1, ref budget, out var jet0, out detail)
                || !TryImplicitJet(in view.Support1, in point, 1, ref budget, out var jet1, out detail))
                return detail == ICurveEvalDetail.BudgetExceeded
                    ? AlgorithmStatus.NotConverged
                    : AlgorithmStatus.Unsupported;
            residualVector[0] = jet0.Value;
            residualVector[1] = jet1.Value;
            jacobian[0] = Dot(jet0.Gradient, u); jacobian[1] = Dot(jet0.Gradient, v);
            jacobian[2] = Dot(jet1.Gradient, u); jacobian[3] = Dot(jet1.Gradient, v);

            residual = SmallLinearSolve.Norm(residualVector);
            if (IsPublishableRoot(in view, t, segment, in point, ref budget, out detail))
            {
                converged = true;
                break;
            }
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            var stepStatus = NewtonStep.ComputeStep(jacobian, jacobianCopy, residualVector, 2, pivots, model, step, out var predicted);
            if (stepStatus != AlgorithmStatus.Success || !(predicted > 0))
                return stepStatus == AlgorithmStatus.Success ? AlgorithmStatus.Singular : stepStatus;
            xi[0] += step[0];
            xi[1] += step[1];
            if (!double.IsFinite(xi[0]) || !double.IsFinite(xi[1])) return AlgorithmStatus.NumericalFailure;
        }
        if (!converged)
        {
            var refine = ICurveCorrection.Refine(in view, ICurveConstraintPlan.I2, t, segment,
                in seed, xi, ref budget, out var refineIterations, out residual, out detail);
            if (refine != AlgorithmStatus.Success) return refine;
            iterations = MaxFastNewtonIterations + refineIterations;
        }
        var root = Add(planeBase, Add(Scale(u, xi[0]), Scale(v, xi[1])));
        if (!TryImplicitJet(in view.Support0, in root, order >= 2 ? 2 : 1, ref budget, out var rootJet0, out detail)
            || !TryImplicitJet(in view.Support1, in root, order >= 2 ? 2 : 1, ref budget, out var rootJet1, out detail))
            return detail == ICurveEvalDetail.BudgetExceeded
                ? AlgorithmStatus.NotConverged
                : AlgorithmStatus.Unsupported;
        jacobian[0] = Dot(rootJet0.Gradient, u); jacobian[1] = Dot(rootJet0.Gradient, v);
        jacobian[2] = Dot(rootJet1.Gradient, u); jacobian[3] = Dot(rootJet1.Gradient, v);
        if (SmallLinearSolve.LuFactorize(jacobian, 2, pivots) != AlgorithmStatus.Success)
            return AlgorithmStatus.Singular;

        derivatives[0] = root;

        if (order >= 1)
        {
            // Q′ = e/f enters the right side — the I2 chain must not drop the
            // moving base point (§16.2, task T08 check).
            var qPrime = Scale(chordUnit, 1 / scale);
            Span<double> d1 = stackalloc double[2];
            d1[0] = -Dot(rootJet0.Gradient, qPrime);
            d1[1] = -Dot(rootJet1.Gradient, qPrime);
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 2, pivots, d1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[1] = Add(qPrime, Add(Scale(u, d1[0]), Scale(v, d1[1])));
            if (!IsFinite(derivatives[1])) return AlgorithmStatus.NumericalFailure;
        }

        if (order >= 2)
        {
            // J ξ″ = −[x′ᵀH₀x′, x′ᵀH₁x′]; x″ = U ξ″ (Q″ = 0).
            var qPrime = Scale(chordUnit, 1 / scale);
            Span<double> d1 = stackalloc double[2];
            d1[0] = -Dot(rootJet0.Gradient, qPrime);
            d1[1] = -Dot(rootJet1.Gradient, qPrime);
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 2, pivots, d1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            var xPrime = Add(qPrime, Add(Scale(u, d1[0]), Scale(v, d1[1])));
            Span<double> d2 = stackalloc double[2];
            d2[0] = -SecondDirectional(in view.Support0, in root, in xPrime, ref budget, out detail);
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            d2[1] = -SecondDirectional(in view.Support1, in root, in xPrime, ref budget, out detail);
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 2, pivots, d2) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[2] = Add(Scale(u, d2[0]), Scale(v, d2[1]));
            if (!IsFinite(derivatives[2])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 LeastParallelAxis(in KernelVector3 direction)
    {
        var ax = Math.Abs(direction.X);
        var ay = Math.Abs(direction.Y);
        var az = Math.Abs(direction.Z);
        if (ax <= ay && ax <= az) return Vector(1, 0, 0);
        return ay <= az ? Vector(0, 1, 0) : Vector(0, 0, 1);
    }

    // ── P4: parametric/parametric, 4×4 baseline (§7.1) ───────────

    private static AlgorithmStatus SolveP4(in ICurveView view, in KernelVector3 seed, double t,
        BufferOffset segment, DerivativeOrder order, ref EvaluationBudget budget,
        scoped Span<KernelVector3> derivatives, out BufferOffset iterations, out double residual,
        out ICurveEvalDetail detail)
    {
        iterations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        if (AnalyticParametricEvaluation.TryRecoverWitness(in view.Support0, in seed, out var u0, out var v0) != AlgorithmStatus.Success
            || AnalyticParametricEvaluation.TryRecoverWitness(in view.Support1, in seed, out var u1, out var v1) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        var scale = view.ChartScales[segment];
        var chordUnit = view.ChartChordUnits[segment];

        Span<double> q = stackalloc double[4] { u0, v0, u1, v1 };
        Span<double> jacobian = stackalloc double[16];
        Span<double> jacobianCopy = stackalloc double[16];
        Span<double> residualVector = stackalloc double[4];
        Span<double> step = stackalloc double[4];
        Span<double> model = stackalloc double[4];
        Span<int> pivots = stackalloc int[4];
        Span<KernelVector3> jet0 = stackalloc KernelVector3[4];
        Span<KernelVector3> jet1 = stackalloc KernelVector3[4];
        if (!SurfaceDerivativeLayout.TryCreate(1, 1, out var layout1))
            return AlgorithmStatus.InvalidInput;

        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxFastNewtonIterations; iteration++)
        {
            iterations = iteration + 1;
            if (!TryCharge(ref budget, out detail))
                return AlgorithmStatus.NotConverged;
            if (SurfaceEvaluation.Evaluate(in view.Support0, q[0], q[1], in layout1, jet0) != AlgorithmStatus.Success)
                return AlgorithmStatus.NotConverged; // trial left a valid parameter domain
            if (!TryCharge(ref budget, out detail))
                return AlgorithmStatus.NotConverged;
            if (SurfaceEvaluation.Evaluate(in view.Support1, q[2], q[3], in layout1, jet1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NotConverged;

            residualVector[0] = jet0[0].X - jet1[0].X;
            residualVector[1] = jet0[0].Y - jet1[0].Y;
            residualVector[2] = jet0[0].Z - jet1[0].Z;
            residualVector[3] = OriginalChartParameterMap.PlaneResidual(
                view.ChartPositions, view.ChartParameters, view.ChartScales, view.ChartChordUnits, segment, t, in jet0[0]);

            // Layout index: Su is (1,0), Sv is (0,1).
            var su0 = jet0[layout1.GetIndex(1, 0)];
            var sv0 = jet0[layout1.GetIndex(0, 1)];
            var su1 = jet1[layout1.GetIndex(1, 0)];
            var sv1 = jet1[layout1.GetIndex(0, 1)];
            jacobian[0] = su0.X; jacobian[1] = sv0.X; jacobian[2] = -su1.X; jacobian[3] = -sv1.X;
            jacobian[4] = su0.Y; jacobian[5] = sv0.Y; jacobian[6] = -su1.Y; jacobian[7] = -sv1.Y;
            jacobian[8] = su0.Z; jacobian[9] = sv0.Z; jacobian[10] = -su1.Z; jacobian[11] = -sv1.Z;
            jacobian[12] = Dot(chordUnit, su0); jacobian[13] = Dot(chordUnit, sv0); jacobian[14] = 0; jacobian[15] = 0;

            residual = SmallLinearSolve.Norm(residualVector);
            if (residual <= PublicationTolerance(in view)
                && IsPublishableRoot(in view, t, segment, in jet0[0], ref budget, out detail))
            {
                converged = true;
                break;
            }
            if (detail == ICurveEvalDetail.BudgetExceeded) return AlgorithmStatus.NotConverged;
            var stepStatus = NewtonStep.ComputeStep(jacobian, jacobianCopy, residualVector, 4, pivots, model, step, out var predicted);
            if (stepStatus != AlgorithmStatus.Success || !(predicted > 0))
                return stepStatus == AlgorithmStatus.Success ? AlgorithmStatus.Singular : stepStatus;
            for (var i = 0; i < 4; i++)
            {
                q[i] += step[i];
                if (!double.IsFinite(q[i])) return AlgorithmStatus.NumericalFailure;
            }
        }
        if (!converged)
        {
            var refine = ICurveCorrection.Refine(in view, ICurveConstraintPlan.P4, t, segment,
                in seed, q, ref budget, out var refineIterations, out residual, out detail);
            if (refine != AlgorithmStatus.Success) return refine;
            iterations = MaxFastNewtonIterations + refineIterations;
        }

        // Root jets at second order; the agreed output side is x0 (§7.1).
        if (!SurfaceDerivativeLayout.TryCreate(2, 2, out var layout2))
            return AlgorithmStatus.InvalidInput;
        Span<KernelVector3> rootJet0 = stackalloc KernelVector3[9];
        Span<KernelVector3> rootJet1 = stackalloc KernelVector3[9];
        if (!TryCharge(ref budget, out detail))
            return AlgorithmStatus.NotConverged;
        if (SurfaceEvaluation.Evaluate(in view.Support0, q[0], q[1], in layout2, rootJet0) != AlgorithmStatus.Success)
            return AlgorithmStatus.NotConverged;
        if (!TryCharge(ref budget, out detail))
            return AlgorithmStatus.NotConverged;
        if (SurfaceEvaluation.Evaluate(in view.Support1, q[2], q[3], in layout2, rootJet1) != AlgorithmStatus.Success)
            return AlgorithmStatus.NotConverged;

        var rootSu0 = rootJet0[layout2.GetIndex(1, 0)];
        var rootSv0 = rootJet0[layout2.GetIndex(0, 1)];
        var rootSu1 = rootJet1[layout2.GetIndex(1, 0)];
        var rootSv1 = rootJet1[layout2.GetIndex(0, 1)];
        jacobian[0] = rootSu0.X; jacobian[1] = rootSv0.X; jacobian[2] = -rootSu1.X; jacobian[3] = -rootSv1.X;
        jacobian[4] = rootSu0.Y; jacobian[5] = rootSv0.Y; jacobian[6] = -rootSu1.Y; jacobian[7] = -rootSv1.Y;
        jacobian[8] = rootSu0.Z; jacobian[9] = rootSv0.Z; jacobian[10] = -rootSu1.Z; jacobian[11] = -rootSv1.Z;
        jacobian[12] = Dot(chordUnit, rootSu0); jacobian[13] = Dot(chordUnit, rootSv0); jacobian[14] = 0; jacobian[15] = 0;
        if (SmallLinearSolve.LuFactorize(jacobian, 4, pivots) != AlgorithmStatus.Success)
            return AlgorithmStatus.Singular;

        derivatives[0] = rootJet0[0];

        if (order >= 1)
        {
            // J q′ = [0,0,0,1/f]; x′ = S0u u0′ + S0v v0′.
            Span<double> d1 = stackalloc double[4];
            d1[0] = 0; d1[1] = 0; d1[2] = 0; d1[3] = 1 / scale;
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 4, pivots, d1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[1] = Add(Scale(rootSu0, d1[0]), Scale(rootSv0, d1[1]));
            if (!IsFinite(derivatives[1])) return AlgorithmStatus.NumericalFailure;
        }

        if (order >= 2)
        {
            // A₀/A₁ are the second-order chain terms of each surface;
            // J q″ = −[A₀−A₁, e·A₀]; x″ = A₀ + S0u u0″ + S0v v0″.
            Span<double> d1 = stackalloc double[4];
            d1[0] = 0; d1[1] = 0; d1[2] = 0; d1[3] = 1 / scale;
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 4, pivots, d1) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            var a0 = ParametricSecondChain(in layout2, rootJet0, d1[0], d1[1]);
            var a1 = ParametricSecondChain(in layout2, rootJet1, d1[2], d1[3]);
            Span<double> d2 = stackalloc double[4];
            d2[0] = -(a0.X - a1.X);
            d2[1] = -(a0.Y - a1.Y);
            d2[2] = -(a0.Z - a1.Z);
            d2[3] = -Dot(chordUnit, a0);
            if (SmallLinearSolve.LuSolveInPlace(jacobian, 4, pivots, d2) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;
            derivatives[2] = Add(a0, Add(Scale(rootSu0, d2[0]), Scale(rootSv0, d2[1])));
            if (!IsFinite(derivatives[2])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 ParametricSecondChain(in SurfaceDerivativeLayout layout,
        ReadOnlySpan<KernelVector3> jet, double du, double dv)
        => Add(
            Add(Scale(jet[layout.GetIndex(2, 0)], du * du), Scale(jet[layout.GetIndex(1, 1)], 2 * du * dv)),
            Scale(jet[layout.GetIndex(0, 2)], dv * dv));

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
