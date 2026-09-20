using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Parameter continuation and local subdivision for regular chart intervals
/// (spec §17.3–§17.5, task T17). Tracking advances the native parameter t only;
/// a pseudo-arclength step may never redefine <c>icurve(t)</c>. Steps never
/// cross an unhandled original chart node. Shared base-evaluation budget is
/// drawn from <see cref="EvaluationBudget"/> — exhaustion is BudgetExceeded,
/// not Unsupported. Multiple same-quality roots report AmbiguousBranch instead
/// of publishing a nearest-neighbor guess.
/// </summary>
internal static class ICurveContinuation
{
    /// <summary>Accepted continuation steps per request (§14.7).</summary>
    internal const int MaxAcceptedSteps = 64;
    /// <summary>Local midpoint subdivisions before giving up (§17.5).</summary>
    internal const int MaxSubdivisionDepth = 8;
    /// <summary>Minimum |Δt| relative to the segment length before stagnation.</summary>
    internal const double MinRelativeStep = 1e-12;

    /// <summary>
    /// Track from a verified anchor <paramref name="tStart"/>/<paramref name="yStart"/>
    /// to the exact requested <paramref name="tTarget"/> inside one original
    /// chart segment. The final accepted parameter is always tTarget — a nearby
    /// accepted point is never substituted (§17.3).
    /// </summary>
    internal static AlgorithmStatus ContinueTo(in ICurveView view, ICurveConstraintPlan plan,
        double tStart, in KernelVector3 yStart, double tTarget, BufferOffset segment,
        DerivativeOrder order, ref EvaluationBudget budget, Span<KernelVector3> derivatives,
        out BufferOffset steps, out double residual, out ICurveEvalDetail detail)
    {
        steps = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        if (order < 0 || order > ICurveEvaluation.MaxDerivativeOrder)
            return AlgorithmStatus.InvalidInput;
        if (derivatives.Length <= order) return AlgorithmStatus.OutputTooSmall;
        if (!double.IsFinite(tStart) || !double.IsFinite(tTarget)) return AlgorithmStatus.InvalidInput;
        if (segment < 0 || segment + 1 >= view.ChartParameters.Length) return AlgorithmStatus.InvalidInput;

        var tLo = view.ChartParameters[segment];
        var tHi = view.ChartParameters[segment + 1];
        // Both ends must lie in the open-or-closed original segment; crossing a
        // chart node is forbidden without an explicit cell transition (§17.3).
        if (tStart < tLo || tStart > tHi || tTarget < tLo || tTarget > tHi)
            return AlgorithmStatus.InvalidInput;
        if (tStart == tTarget)
        {
            if (!ICurveEvaluation.IsPublishableRoot(in view, tTarget, segment, in yStart))
                return AlgorithmStatus.NotConverged;
            return CorrectAt(in view, plan, in yStart, tTarget, segment, order,
                derivatives, out steps, out residual);
        }

        var position = yStart;
        var t = tStart;
        var direction = Math.Sign(tTarget - tStart);
        var segmentLength = tHi - tLo;
        var step = 0.25 * Math.Abs(tTarget - tStart);
        if (!(step > 0)) step = 0.25 * segmentLength;
        var minStep = MinRelativeStep * Math.Max(segmentLength, 1.0);
        Span<KernelVector3> local = stackalloc KernelVector3[3];

        while (steps < MaxAcceptedSteps)
        {
            var remaining = Math.Abs(tTarget - t);
            if (remaining <= 0)
                return CorrectAt(in view, plan, in position, tTarget, segment, order,
                    derivatives, out _, out residual);

            // Last step lands exactly on tTarget — never a nearby accepted point.
            var delta = Math.Min(step, remaining) * direction;
            var tNext = t + delta;
            if (Math.Abs(tTarget - tNext) <= minStep * 0.5)
                tNext = tTarget;

            if (!budget.TryConsume(2)) // predictor Jacobian + corrector seed evals (lower bound)
            {
                detail = ICurveEvalDetail.BudgetExceeded;
                return AlgorithmStatus.NotConverged;
            }

            var predictStatus = PredictI3(in view, segment, t, in position, tNext, out var predicted);
            if (predictStatus != AlgorithmStatus.Success)
            {
                // Without a reliable tangent, fall back to holding the last root
                // as the seed and let the corrector absorb the parameter jump.
                predicted = position;
            }

            var correctStatus = CorrectAt(in view, plan, in predicted, tNext, segment, 0, local,
                out var acceptedIterations, out residual);
            // Each Refine/Solve attempt may itself burn many base evals; charge a
            // conservative lower bound so shared budget still shrinks.
            if (!budget.TryConsume(Math.Max(1, acceptedIterations)))
            {
                detail = ICurveEvalDetail.BudgetExceeded;
                return AlgorithmStatus.NotConverged;
            }

            if (correctStatus == AlgorithmStatus.Success)
            {
                // Branch guard: the accepted point must stay on the same local
                // sheet as the start (dot of displacement with start tangent ≥ 0
                // is insufficient alone; residual-plane continuity is required).
                if (!SameLocalBranch(in view, in yStart, in local[0], segment, tStart, tNext))
                {
                    detail = ICurveEvalDetail.AmbiguousBranch;
                    return AlgorithmStatus.NotConverged;
                }

                position = local[0];
                t = tNext;
                steps++;
                // Successful steps may grow; failed ones shrink below.
                step = Math.Min(step * 1.5, Math.Max(remaining, segmentLength));
                if (t == tTarget)
                {
                    // Re-evaluate exactly the requested jet order. Intermediate
                    // continuation points carry D0 only and cannot upgrade a
                    // higher-order request merely by reaching the same t.
                    return CorrectAt(in view, plan, in position, tTarget, segment, order,
                        derivatives, out _, out residual);
                }
                continue;
            }

            // Failure: restore (position,t) unchanged and halve the step (§17.3).
            step *= 0.5;
            if (step < minStep)
            {
                detail = ICurveEvalDetail.Stagnation;
                return AlgorithmStatus.NotConverged;
            }
        }

        detail = ICurveEvalDetail.Stagnation;
        return AlgorithmStatus.NotConverged;
    }

    /// <summary>
    /// Local midpoint subdivision inside one original segment (§17.5): from the
    /// chart endpoints try to build a verified chain toward <paramref name="tTarget"/>.
    /// Two equally residualled roots that disagree spatially beyond tolerance
    /// are AmbiguousBranch — denser sampling alone is not uniqueness evidence.
    /// </summary>
    internal static AlgorithmStatus SubdivideTo(in ICurveView view, ICurveConstraintPlan plan,
        double tTarget, BufferOffset segment, DerivativeOrder order, ref EvaluationBudget budget,
        Span<KernelVector3> derivatives, out BufferOffset evaluations,
        out double residual, out ICurveEvalDetail detail)
    {
        evaluations = 0;
        residual = 0;
        detail = ICurveEvalDetail.None;
        if (order < 0 || order > ICurveEvaluation.MaxDerivativeOrder)
            return AlgorithmStatus.InvalidInput;
        if (derivatives.Length <= order) return AlgorithmStatus.OutputTooSmall;
        if (segment < 0 || segment + 1 >= view.ChartParameters.Length) return AlgorithmStatus.InvalidInput;

        var tLo = view.ChartParameters[segment];
        var tHi = view.ChartParameters[segment + 1];
        if (tTarget < tLo || tTarget > tHi) return AlgorithmStatus.InvalidInput;

        // Seed from the nearer chart endpoint (defining D0, §5.4).
        var useHi = Math.Abs(tHi - tTarget) < Math.Abs(tTarget - tLo);
        var tAnchor = useHi ? tHi : tLo;
        var yAnchor = useHi ? view.ChartPositions[segment + 1] : view.ChartPositions[segment];

        // First try direct continuation from the chart anchor.
        var status = ContinueTo(in view, plan, tAnchor, in yAnchor, tTarget, segment,
            order, ref budget, derivatives, out var steps, out residual, out detail);
        evaluations = steps;
        if (status == AlgorithmStatus.Success) return status;

        // Midpoint ladder: evaluate midpoints between anchor and target, each
        // corrected from the chord seed; then continue from the closest success.
        var tLeft = tAnchor;
        var yLeft = yAnchor;
        Span<KernelVector3> midDeriv = stackalloc KernelVector3[3];
        Span<KernelVector3> otherDeriv = stackalloc KernelVector3[3];
        for (BufferOffset depth = 0; depth < MaxSubdivisionDepth; depth++)
        {
            if (budget.IsExhausted)
            {
                detail = ICurveEvalDetail.BudgetExceeded;
                return AlgorithmStatus.NotConverged;
            }

            var tMid = 0.5 * (tLeft + tTarget);
            if (tMid == tLeft || tMid == tTarget) break;

            ChordSeed(in view, segment, tMid, out var chord);
            if (!budget.TryConsume(4))
            {
                detail = ICurveEvalDetail.BudgetExceeded;
                return AlgorithmStatus.NotConverged;
            }
            var midStatus = CorrectAt(in view, plan, in chord, tMid, segment, 0, midDeriv,
                out var midIters, out var midResidual);
            evaluations += midIters;
            if (midStatus != AlgorithmStatus.Success) continue;

            // Ambiguity probe: correct also from the opposite chart end; if both
            // converge to distant roots with comparable residual, refuse (§17.5).
            var yOther = useHi ? view.ChartPositions[segment] : view.ChartPositions[segment + 1];
            ChordSeed(in view, segment, tMid, out var chordFromOther);
            // Perturb the chord seed slightly toward the other end so the second
            // attempt is not bit-identical to the first.
            var towardOther = Scale(Sub(yOther, chordFromOther), 0.05);
            var otherSeed = Add(chordFromOther, towardOther);
            if (budget.TryConsume(4))
            {
                var otherStatus = CorrectAt(in view, plan, in otherSeed, tMid, segment, 0, otherDeriv,
                    out var otherIters, out var otherResidual);
                evaluations += otherIters;
                if (otherStatus == AlgorithmStatus.Success)
                {
                    var separation = Norm(Sub(midDeriv[0], otherDeriv[0]));
                    var scale = Math.Max(1.0, Norm(midDeriv[0]));
                    var residualGap = Math.Abs(midResidual - otherResidual);
                    if (separation > 1e-6 * scale
                        && residualGap <= 1e-6 * Math.Max(1.0, Math.Max(midResidual, otherResidual)))
                    {
                        // Interval cert may resolve Unique vs Empty (§18.3); otherwise refuse.
                        if (plan == ICurveConstraintPlan.I3
                            && TryPreferCertifiedI3Root(in view, tMid, segment,
                                midDeriv[0], otherDeriv[0], out var preferMid))
                        {
                            if (!preferMid)
                                midDeriv[0] = otherDeriv[0];
                        }
                        else
                        {
                            detail = ICurveEvalDetail.AmbiguousBranch;
                            return AlgorithmStatus.NotConverged;
                        }
                    }
                }
            }

            // Continue from the verified midpoint to the exact target.
            status = ContinueTo(in view, plan, tMid, in midDeriv[0], tTarget, segment,
                order, ref budget, derivatives, out var contSteps, out residual, out detail);
            evaluations += contSteps;
            if (status == AlgorithmStatus.Success) return status;

            tLeft = tMid;
            yLeft = midDeriv[0];
        }

        if (detail == ICurveEvalDetail.None) detail = ICurveEvalDetail.Stagnation;
        return AlgorithmStatus.NotConverged;
    }

    /// <summary>
    /// Prefer a Unique-certified I3 cell over an Empty sibling when two Newton
    /// hits disagree (§17.5 + §18.3). Returns false when certification cannot
    /// break the tie.
    /// </summary>
    private static bool TryPreferCertifiedI3Root(in ICurveView view, double t, BufferOffset segment,
        in KernelVector3 candidateA, in KernelVector3 candidateB, out bool preferA)
    {
        preferA = true;
        var chordUnit = view.ChartChordUnits[segment];
        var planeOffset = IntervalRootCheck.ChordPlaneOffset(in chordUnit, view.ChartPositions[segment],
            view.ChartParameters[segment], view.ChartScales[segment], t);
        var cellRadius = Math.Max(1e-4, 1e-3 * Math.Max(1.0, Norm(candidateA)));
        return IntervalRootCheck.TryDisambiguateI3Pair(
            in view.Support0, in view.Support1, in chordUnit, planeOffset,
            in candidateA, in candidateB, cellRadius, out preferA);
    }

    /// <summary>
    /// I3 predictor: solve J y_t = −F_t with F_t = (0,0,−1/f) and advance
    /// y ← y + y_t Δt. Other plans reuse the last position as a hold predictor;
    /// the corrector absorbs the jump (§17.3).
    /// </summary>
    private static AlgorithmStatus PredictI3(in ICurveView view, BufferOffset segment,
        double t, in KernelVector3 y, double tNext, out KernelVector3 predicted)
    {
        predicted = y;
        var scale = view.ChartScales[segment];
        if (!(Math.Abs(scale) > 0)) return AlgorithmStatus.Singular;
        var chordUnit = view.ChartChordUnits[segment];

        if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in y, 1, out var jet0)
                != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in y, 1, out var jet1)
                != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;

        // J rows: ∇φ0, ∇φ1, eᵀ. Right-hand side −F_t = (0,0,1/f).
        Span<double> jacobian = stackalloc double[9];
        jacobian[0] = jet0.Gradient.X; jacobian[1] = jet0.Gradient.Y; jacobian[2] = jet0.Gradient.Z;
        jacobian[3] = jet1.Gradient.X; jacobian[4] = jet1.Gradient.Y; jacobian[5] = jet1.Gradient.Z;
        jacobian[6] = chordUnit.X; jacobian[7] = chordUnit.Y; jacobian[8] = chordUnit.Z;
        Span<double> rhs = stackalloc double[3];
        rhs[0] = 0;
        rhs[1] = 0;
        rhs[2] = 1.0 / scale;
        Span<int> pivots = stackalloc int[3];
        var factorStatus = SmallLinearSolve.LuFactorize(jacobian, 3, pivots);
        if (factorStatus != AlgorithmStatus.Success) return factorStatus;
        var solveStatus = SmallLinearSolve.LuSolveInPlace(jacobian, 3, pivots, rhs);
        if (solveStatus != AlgorithmStatus.Success) return solveStatus;

        var dt = tNext - t;
        predicted = Vector(y.X + rhs[0] * dt, y.Y + rhs[1] * dt, y.Z + rhs[2] * dt);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus CorrectAt(in ICurveView view, ICurveConstraintPlan plan,
        in KernelVector3 seed, double t, BufferOffset segment, DerivativeOrder order,
        Span<KernelVector3> derivatives,
        out BufferOffset iterations, out double residual)
    {
        // Direct solve only — never re-enter EvaluateWithPlan, which would recurse
        // through the continuation fallback (§17.3).
        var fast = ICurveEvaluation.SolveDirect(in view, in seed, t, segment, order, plan,
            derivatives, out iterations, out residual);
        if (fast == AlgorithmStatus.Success) return fast;

        Span<double> state = stackalloc double[4];
        var refineStatus = ICurveCorrection.Refine(in view, plan, t, segment, in seed, state,
            out iterations, out residual);
        if (refineStatus != AlgorithmStatus.Success) return refineStatus;

        if (!TryPositionFromState(in view, plan, t, segment, state, out var position))
            return AlgorithmStatus.NumericalFailure;
        return ICurveEvaluation.SolveDirect(in view, in position, t, segment, order, plan,
            derivatives, out iterations, out residual);
    }

    private static bool TryPositionFromState(in ICurveView view, ICurveConstraintPlan plan,
        double t, BufferOffset segment, ReadOnlySpan<double> state, out KernelVector3 position)
    {
        position = default;
        switch (plan)
        {
            case ICurveConstraintPlan.I3:
                position = Vector(state[0], state[1], state[2]);
                return true;
            case ICurveConstraintPlan.I2:
            {
                var chordUnit = view.ChartChordUnits[segment];
                var axis = LeastParallelAxis(in chordUnit);
                var u = Unit(Sub(axis, Scale(chordUnit, Dot(axis, chordUnit))));
                var v = Cross(chordUnit, u);
                ChordSeed(in view, segment, t, out var q);
                position = Add(q, Add(Scale(u, state[0]), Scale(v, state[1])));
                return true;
            }
            case ICurveConstraintPlan.I1:
            {
                ChordSeed(in view, segment, t, out var q);
                var planeIsSupport0 = view.Support0.Kind == SurfaceClass.Plane;
                var n = planeIsSupport0 ? view.Support0.Axis : view.Support1.Axis;
                var c = planeIsSupport0 ? view.Support0.Origin : view.Support1.Origin;
                var chordUnit = view.ChartChordUnits[segment];
                var cross = Cross(n, chordUnit);
                var crossNormSq = Dot(cross, cross);
                if (!(crossNormSq > 1e-24)) return false;
                var b = Scale(cross, 1 / Math.Sqrt(crossNormSq));
                var nTilde = Sub(n, Scale(chordUnit, Dot(n, chordUnit)));
                var alpha = Dot(n, Sub(c, q)) / crossNormSq;
                position = Add(Add(q, Scale(nTilde, alpha)), Scale(b, state[0]));
                return true;
            }
            case ICurveConstraintPlan.P2:
            {
                Span<KernelVector3> jet = stackalloc KernelVector3[1];
                if (!SurfaceDerivativeLayout.TryCreate(0, 0, out var layout)) return false;
                if (SurfaceEvaluation.Evaluate(in view.Support0, state[0], state[1], in layout, jet)
                    != AlgorithmStatus.Success)
                    return false;
                position = jet[0];
                return true;
            }
            case ICurveConstraintPlan.P4:
            {
                Span<KernelVector3> jet = stackalloc KernelVector3[1];
                if (!SurfaceDerivativeLayout.TryCreate(0, 0, out var layout)) return false;
                if (SurfaceEvaluation.Evaluate(in view.Support0, state[0], state[1], in layout, jet)
                    != AlgorithmStatus.Success)
                    return false;
                position = jet[0];
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Local branch continuity: the tracked point's support residuals stay small
    /// and its displacement from the start does not jump across the origin of a
    /// closed section (simple length guard relative to chord scale).
    /// </summary>
    private static bool SameLocalBranch(in ICurveView view, in KernelVector3 start,
        in KernelVector3 candidate, BufferOffset segment, double tStart, double tCandidate)
    {
        if (AnalyticImplicitEvaluation.GeometricDeviation(in view.Support0, in candidate, out var d0)
                != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.GeometricDeviation(in view.Support1, in candidate, out var d1)
                != AlgorithmStatus.Success)
            return false;
        var localScale = ICurveEvaluation.PublicationTolerance(in view)
            / ICurveEvaluation.ResidualTolerance;
        if (d0 > 1e-6 * localScale || d1 > 1e-6 * localScale)
            return false;

        // Parameter-plane consistency at the candidate.
        var plane = OriginalChartParameterMap.PlaneResidual(
            view.ChartPositions, view.ChartParameters, view.ChartScales,
            view.ChartChordUnits, segment, tCandidate, in candidate);
        if (Math.Abs(plane) > 1e-6 * localScale) return false;

        // Reject a jump larger than a generous multiple of the chord advance —
        // that is the typical signature of landing on the opposite sheet.
        var chordAdvance = Math.Abs(tCandidate - tStart) / Math.Max(Math.Abs(view.ChartScales[segment]), 1e-300);
        var jump = Norm(Sub(candidate, start));
        return !(jump > 8.0 * Math.Max(chordAdvance, 1e-6));
    }

    private static void ChordSeed(in ICurveView view, BufferOffset segment, double t, out KernelVector3 q)
    {
        var next = view.ChartParameters[segment + 1] - view.ChartParameters[segment];
        var lambda = (t - view.ChartParameters[segment]) / next;
        q = Add(
            Scale(view.ChartPositions[segment], 1 - lambda),
            Scale(view.ChartPositions[segment + 1], lambda));
    }

    private static KernelVector3 LeastParallelAxis(in KernelVector3 direction)
    {
        var ax = Math.Abs(direction.X);
        var ay = Math.Abs(direction.Y);
        var az = Math.Abs(direction.Z);
        if (ax <= ay && ax <= az) return Vector(1, 0, 0);
        return ay <= az ? Vector(0, 1, 0) : Vector(0, 0, 1);
    }

    private static double Norm(in KernelVector3 v)
        => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
