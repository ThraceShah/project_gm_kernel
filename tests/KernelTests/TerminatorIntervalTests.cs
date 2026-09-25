using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Terminator evaluation (spec §6, task T16 completion): classification
/// precedes solver choice, the GATE-T parameter rule is never guessed in the
/// production entry, the one-surface/two-planes interval construction holds
/// its own defining constraints with the unselected face's deviation kept as
/// a diagnostic (§6.4), and the exact terminator publishes its defining D0
/// bit-exactly while higher orders stay behind the open endpoint contract
/// (§6.5). Tolerances per §21.7: position 1e-10, D1 1e-9, D2 1e-7.
/// </summary>
public class TerminatorIntervalTests
{
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

    private const double ChartEndAngle = 1.03;

    private static KernelVector3 CirclePoint(double angle, double z = 0)
        => Vector(Math.Cos(angle), Math.Sin(angle), z);

    private static ICurveView CircleViewWithEndTerminator(double endAngle, LimitTermUse termUse, double endZ = 0)
    {
        double[] angles = [0.0, 0.17, 0.62, ChartEndAngle];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = CirclePoint(angles[i]);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, 1.7, parameters, scales, chords, out var segments, out _));
        Assert.Equal(3, segments);

        var end = new TerminatorLimit(termUse, CirclePoint(endAngle, endZ), CirclePoint(ChartEndAngle));
        var absent = default(TerminatorLimit);
        return new ICurveView(in PlaneZ0, 1, in CylinderR1, 1,
            positions, parameters, scales, chords, absent, end);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    // ── GATE-T: classification and gating ────────────────────────

    [Fact]
    public void BeyondChart_WithoutRule_IsGatedWithoutOutput()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var t = view.ChartParameters[^1] + 1e-3;
        Span<KernelVector3> derivatives = new KernelVector3[3];
        derivatives[0] = derivatives[1] = derivatives[2] = Vector(42, 42, 42);

        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveEvaluation.EvaluateWithPlan(in view, t, 2, ICurveConstraintPlan.Auto, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.EndTerminatorInterval, report.Kind);
        Assert.Equal(ICurveEvalDetail.CompatibilityGateOpen, report.Detail);
        // Nothing was published (§18.5: no partial output).
        Assert.Equal(42, derivatives[0].X, 12);
    }

    [Fact]
    public void ChartIntervalQueries_AreUnaffectedByTerminatorPresence()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in view, t, 0, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.RegularChartInterval, report.Kind);
        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);
    }

    // ── GATE-T: parameter reconstruction rules (§6.5, §23.1) ─────

    [Theory]
    [InlineData((int)TerminatorParameterRule.ExtensionRatio)]
    [InlineData((int)TerminatorParameterRule.TangentMatching)]
    public void Resolve_EndTerminator_ProducesOrderedFiniteParameter(int ruleId)
    {
        var rule = (TerminatorParameterRule)ruleId;
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true, rule,
                in endpoint, in branch, out var terminatorParameter));
        Assert.True(double.IsFinite(terminatorParameter));
        Assert.True(terminatorParameter > view.ChartParameters[^1]);
    }

    [Fact]
    public void Resolve_StartTerminator_ProducesParameterBelowChartStart()
    {
        double[] angles = [0.0, 0.17, 0.62, 1.03];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = CirclePoint(angles[i]);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, 1.7, parameters, scales, chords, out _, out _));

        var start = new TerminatorLimit(LimitTermUse.First, CirclePoint(-0.25), CirclePoint(0.0));
        var absent = default(TerminatorLimit);
        var view = new ICurveView(in PlaneZ0, 1, in CylinderR1, 1,
            positions, parameters, scales, chords, start, absent);

        var endpoint = CirclePoint(-0.25);
        var branch = CirclePoint(0.0);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, false,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out var tStart));
        Assert.True(tStart < view.ChartParameters[0]);
    }

    [Fact]
    public void Resolve_DegenerateChord_IsRefused()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var same = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.InvalidInput,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in same, in same, out _));
    }

    // ── Interval construction: defining constraints + diagnostics ──

    [Fact]
    public void TerminatorInterval_PlaneSelected_HoldsConstructionAndReportsDeviation()
    {
        // term_use First selects surface[0] = plane z=0. On the interval the
        // one-face construction returns the chord point Q(t) itself (the
        // scalar solve of the linear face is exact at μ=0); the cylinder is
        // NOT a defining constraint and its deviation is diagnostic only —
        // the §6.4 contract in its purest form.
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out var tT));

        var t = 0.5 * (view.ChartParameters[^1] + tT);
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t, 2, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, derivatives, out var report));

        Assert.Equal(ICurveQueryKind.EndTerminatorInterval, report.Kind);
        Assert.Equal(2, report.Segment); // adjacent original chart segment
        Assert.InRange(report.Residual, 0, 1e-12);

        // Defining: selected plane and both planes of the construction.
        Assert.InRange(Math.Abs(derivatives[0].Z), 0, 1e-12);
        var expectedQ = Add(branch, Scale(Sub(endpoint, branch),
            (t - view.ChartParameters[^1]) / (tT - view.ChartParameters[^1])));
        Assert.Equal(expectedQ.X, derivatives[0].X, 12);
        Assert.Equal(expectedQ.Y, derivatives[0].Y, 12);

        // Diagnostic: the unselected cylinder deviation (the chord sags inside
        // the circle) is reported, not treated as a failure.
        Assert.True(report.NonDefiningResidual > 1e-6,
            $"expected visible non-defining deviation, got {report.NonDefiningResidual}");

        // §16.3 with Q'' = 0 and a linear selected face: x' = Q', x'' = 0.
        var chordRate = Scale(Sub(endpoint, branch), 1 / (tT - view.ChartParameters[^1]));
        Assert.Equal(chordRate.X, derivatives[1].X, 12);
        Assert.Equal(chordRate.Y, derivatives[1].Y, 12);
        Assert.Equal(0.0, derivatives[2].X, 9);
        Assert.Equal(0.0, derivatives[2].Y, 9);
    }

    [Fact]
    public void TerminatorInterval_CylinderSelected_NonlinearJetsMatchCentralDifferences()
    {
        // term_use Second selects surface[1] = cylinder (p.49 rule), with the
        // terminator on the circle itself so the construction tracks the true
        // branch: D1/D2 must match central differences of the published jets
        // on the same construction (§21.2).
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.Second);
        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out var tT));

        var t = 0.5 * (view.ChartParameters[^1] + tT);
        Span<KernelVector3> center = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t, 2, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, center, out var report));

        // On the construction the point stays on the selected cylinder.
        Assert.Equal(ICurveQueryKind.EndTerminatorInterval, report.Kind);
        Assert.InRange(Math.Abs(center[0].X * center[0].X + center[0].Y * center[0].Y - 1), 0, 1e-12);
        Assert.InRange(report.Residual, 0, 1e-12);

        // D1 vs the central difference of published D0 (§21.2).
        var delta = 1e-5;
        Span<KernelVector3> minus = new KernelVector3[1];
        Span<KernelVector3> plus = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t - delta, 0, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, minus, out _));
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t + delta, 0, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, plus, out _));
        Assert.Equal((plus[0].X - minus[0].X) / (2 * delta), center[1].X, 6);
        Assert.Equal((plus[0].Y - minus[0].Y) / (2 * delta), center[1].Y, 6);

        // D2 vs the central difference of published D1 (two-step check).
        Span<KernelVector3> minusD1 = new KernelVector3[2];
        Span<KernelVector3> plusD1 = new KernelVector3[2];
        var wideDelta = 1e-4;
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t - wideDelta, 1, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, minusD1, out _));
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t + wideDelta, 1, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, plusD1, out _));
        Assert.Equal((plusD1[1].X - minusD1[1].X) / (2 * wideDelta), center[2].X, 6);
        Assert.Equal((plusD1[1].Y - minusD1[1].Y) / (2 * wideDelta), center[2].Y, 6);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.25)]
    public void TerminatorInterval_ManufacturedCylinder_MatchesClosedForm(double lambda)
    {
        // Manufactured case: Unit cylinder x² + y² = 1, plane z = 0.
        // Branch point B = (1, 0, 0) at tB = 0, Terminator endpoint E = (0, 1, 0) at tE = 1.
        // TerminatorAnchor with lineDirection = (1/√2, 1/√2, 0).
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.0);
        var b = Vector(1, 0, 0);
        var e = Vector(0, 1, 0);
        var tB = 0.0;
        var tE = 1.0;
        var t = tB + lambda * (tE - tB);

        var lineDir = Vector(1 / Math.Sqrt(2), 1 / Math.Sqrt(2), 0);
        var chordRate = Vector(-1, 1, 0);
        var chordDir = Vector(-1 / Math.Sqrt(2), 1 / Math.Sqrt(2), 0);
        var planeNorm = Vector(0, 0, -1);
        var anchor = new TerminatorEvaluation.TerminatorAnchor(0, in e, in b, in planeNorm, in chordDir,
            in lineDir, in chordRate, tE, tB, Math.Sqrt(2));

        var budget = EvaluationBudget.Default;
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.SolveIntervalPoint(in cylinder, in anchor, t,
                ref budget, out _, out var point, out _, out _));

        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.IntervalDerivatives(in cylinder, in anchor, in point, 2,
                out var d1, out var d2));

        if (Math.Abs(lambda - 0.5) < 1e-12)
        {
            // Exact closed form at t = 0.5:
            // x = (1/√2, 1/√2, 0), D1 = (-1, 1, 0), D2 = (-√2, -√2, 0)
            Assert.Equal(1 / Math.Sqrt(2), point.X, 12);
            Assert.Equal(1 / Math.Sqrt(2), point.Y, 12);
            Assert.Equal(0.0, point.Z, 12);

            Assert.Equal(-1.0, d1.X, 12);
            Assert.Equal(1.0, d1.Y, 12);
            Assert.Equal(0.0, d1.Z, 12);

            Assert.Equal(-Math.Sqrt(2), d2.X, 10);
            Assert.Equal(-Math.Sqrt(2), d2.Y, 10);
            Assert.Equal(0.0, d2.Z, 10);
        }
        else if (Math.Abs(lambda - 0.25) < 1e-12)
        {
            // Exact closed form at t = 0.25:
            // x = ((1+√7)/4, (-1+√7)/4, 0)
            // D1 = (-1 + 1/√7, 1 + 1/√7, 0)
            // D2 = (-32/(7√7), -32/(7√7), 0)
            var expectedX = (1 + Math.Sqrt(7)) / 4;
            var expectedY = (-1 + Math.Sqrt(7)) / 4;
            Assert.Equal(expectedX, point.X, 12);
            Assert.Equal(expectedY, point.Y, 12);
            Assert.Equal(0.0, point.Z, 12);

            var expectedD1X = -1 + 1 / Math.Sqrt(7);
            var expectedD1Y = 1 + 1 / Math.Sqrt(7);
            Assert.Equal(expectedD1X, d1.X, 10);
            Assert.Equal(expectedD1Y, d1.Y, 10);
            Assert.Equal(0.0, d1.Z, 10);

            var expectedD2X = -32 / (7 * Math.Sqrt(7));
            var expectedD2Y = -32 / (7 * Math.Sqrt(7));
            Assert.Equal(expectedD2X, d2.X, 10);
            Assert.Equal(expectedD2Y, d2.Y, 10);
            Assert.Equal(0.0, d2.Z, 10);
        }
    }

    [Fact]
    public void TerminatorInterval_CylinderSelectedOffCircle_ReportsNonDefiningDeviation()
    {
        // Terminator off the plane (but on the cylinder): the construction
        // holds the cylinder (selected) while the plane deviation becomes a
        // visible diagnostic — never a fourth equation (§6.4).
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.Second, endZ: 0.2);
        var endpoint = CirclePoint(1.28, 0.2);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out var tT));

        var t = 0.5 * (view.ChartParameters[^1] + tT);
        Span<KernelVector3> derivatives = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, t, 0, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, derivatives, out var report));

        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);
        Assert.True(report.NonDefiningResidual > 1e-6,
            $"expected visible plane deviation, got {report.NonDefiningResidual}");
    }

    // ── Exact terminator and boundary contracts (§6.5, §5.4) ─────

    [Fact]
    public void ExactTerminator_D0IsDefiningPosition_HigherOrdersStayGated()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out var tT));

        Span<KernelVector3> position = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithRule(in view, tT, 0, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, position, out var report));
        Assert.Equal(ICurveQueryKind.ExactTerminator, report.Kind);
        // D0 is the imported terminator position, bit-exact.
        Assert.Equal(endpoint.X, position[0].X, 15);
        Assert.Equal(endpoint.Y, position[0].Y, 15);
        Assert.Equal(endpoint.Z, position[0].Z, 15);

        // A derivative request does not publish a partial D0 (§18.5).
        Span<KernelVector3> derivatives = new KernelVector3[3];
        derivatives[0] = derivatives[1] = derivatives[2] = Vector(42, 42, 42);
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveEvaluation.EvaluateWithRule(in view, tT, 2, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, derivatives, out var gated));
        Assert.Equal(ICurveQueryKind.ExactTerminator, gated.Kind);
        Assert.Equal(ICurveEvalDetail.CompatibilityGateOpen, gated.Detail);
        Assert.Equal(42, derivatives[0].X, 12);
    }

    [Fact]
    public void ChartBoundaryNode_KeepsChartPointContract_WithTerminatorPresent()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var boundary = view.ChartParameters[^1];
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in view, boundary, 0, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.ChartPoint, report.Kind);
        // The original anchor, bit-exact — the terminator never redefines it.
        Assert.Equal(view.ChartPositions[^1].X, derivatives[0].X, 15);
        Assert.Equal(view.ChartPositions[^1].Y, derivatives[0].Y, 15);
    }

    [Fact]
    public void BeyondResolvedTerminator_IsOutsideSupportedDomain()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out var tT));

        Span<KernelVector3> derivatives = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.InvalidInput,
            ICurveEvaluation.EvaluateWithRule(in view, tT + 1.0, 0, ICurveConstraintPlan.Auto,
                TerminatorParameterRule.ExtensionRatio, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.OutsideSupportedDomain, report.Kind);
    }

    [Fact]
    public void InconsistentBranchPoint_IsRefusedNotSnapped()
    {
        // The limit's branch point disagrees with the chart boundary: an
        // input problem, diagnosed — never silently snapped (§5.2).
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var endpoint = CirclePoint(1.28);
        var wrongBranch = CirclePoint(ChartEndAngle + 0.05);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in wrongBranch, out var tT));
        Assert.Equal(AlgorithmStatus.InvalidInput,
            TerminatorEvaluation.Prepare(in view, true, in endpoint, in wrongBranch,
                LimitTermUse.First, tT, out _));
    }

    [Fact]
    public void SingularSelectedSurface_DegenerateExtensionIsDiagnosed()
    {
        // A cone apex at the terminator is singular for surface[0]; the chord
        // to the branch point is perpendicular to the branch tangent there
        // (∇φ_cone ∥ chord), so a resolvable parameter extension does not
        // exist and the diagnosis is Singular — no guessed rule, no fallback.
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            CirclePoint(1.28), Vector(0, 0, 1), Vector(1, 0, 0), 0.0, 1.0);

        double[] angles = [0.77, 0.92, ChartEndAngle];
        var positions = new KernelVector3[3];
        var tangents = new KernelVector3[3];
        for (var i = 0; i < 3; i++)
        {
            positions[i] = CirclePoint(angles[i]);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[3];
        var scales = new double[2];
        var chords = new KernelVector3[2];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0.0, 1.7, parameters, scales, chords, out _, out _));

        var end = new TerminatorLimit(LimitTermUse.First, CirclePoint(1.28), CirclePoint(ChartEndAngle));
        var absent = default(TerminatorLimit);
        var view = new ICurveView(in cone, 1, in PlaneZ0, 1,
            positions, parameters, scales, chords, absent, end);

        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        Assert.Equal(AlgorithmStatus.Singular,
            TerminatorEvaluation.TryResolveTerminatorParameter(in view, true,
                TerminatorParameterRule.ExtensionRatio, in endpoint, in branch, out _));
    }

    [Fact]
    public void SolveIntervalPoint_ExhaustedBudget_ReportsNotConverged()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.First);
        var end = view.EndTerminator;
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.TryResolveTerminatorParameter(
            in view, true, TerminatorParameterRule.ExtensionRatio,
            in end.Endpoint, in end.BranchPoint, out var tT));
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.Prepare(
            in view, true, in end.Endpoint, in end.BranchPoint, end.TermUse, tT, out var anchor));

        var t = 0.5 * (view.ChartParameters[^1] + tT);
        var budget = new EvaluationBudget(0); // no base evals allowed
        Assert.Equal(AlgorithmStatus.NotConverged, TerminatorEvaluation.SolveIntervalPoint(
            in PlaneZ0, in anchor, t, ref budget, out _, out _, out _, out _));
        Assert.Equal(0, budget.Remaining);
        Assert.Equal(0, budget.Used);
    }

    [Fact]
    public void SolveParametricTwoByTwo_DrivesPlaneAndChordResidualsToZero()
    {
        var view = CircleViewWithEndTerminator(1.28, LimitTermUse.Second);
        var end = view.EndTerminator;
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.TryResolveTerminatorParameter(
            in view, true, TerminatorParameterRule.ExtensionRatio,
            in end.Endpoint, in end.BranchPoint, out var tT));
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.Prepare(
            in view, true, in end.Endpoint, in end.BranchPoint, LimitTermUse.Second, tT, out var anchor));

        var t = 0.5 * (view.ChartParameters[^1] + tT);
        var budget = new EvaluationBudget(20);
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.SolveParametricTwoByTwo(
            in CylinderR1, in anchor, t, ref budget, out var u, out var v, out var point, out var residual, out var evals));
        Assert.True(evals > 0);
        Assert.InRange(residual, 0, 1e-11);

        // Point is on the cylinder (x^2 + y^2 = 1, z arbitrary)
        Assert.InRange(Math.Abs(point.X * point.X + point.Y * point.Y - 1.0), 0, 1e-10);
        // And satisfies (point - Q) · PlaneNormal == 0 and (point - Q) · ChordUnit == 0
        var q = TerminatorEvaluation.InterpolatedChordPoint(in anchor, t);
        var dx = Vector(point.X - q.X, point.Y - q.Y, point.Z - q.Z);
        Assert.InRange(Math.Abs(Dot(dx, anchor.PlaneNormal)), 0, 1e-11);
        Assert.InRange(Math.Abs(Dot(dx, anchor.ChordUnit)), 0, 1e-11);
    }

    [Fact]
    public void Prepare_SingularEndpoint_FallsBackToBranchTangent()
    {
        // Cone apex at endpoint makes selected surface singular at E.
        var endpoint = CirclePoint(1.28);
        var branch = CirclePoint(ChartEndAngle);
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            endpoint, Vector(0, 0, 1), Vector(1, 0, 0), 0.0, 1.0);

        double[] angles = [0.77, 0.92, ChartEndAngle];
        var positions = new KernelVector3[3];
        var tangents = new KernelVector3[3];
        for (var i = 0; i < 3; i++)
        {
            positions[i] = CirclePoint(angles[i]);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[3];
        var scales = new double[2];
        var chords = new KernelVector3[2];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0.0, 1.7, parameters, scales, chords, out _, out _));

        var end = new TerminatorLimit(LimitTermUse.First, endpoint, branch);
        var absent = default(TerminatorLimit);
        var view = new ICurveView(in cone, 1, in PlaneZ0, 1,
            positions, parameters, scales, chords, absent, end);

        // Prepare directly with a known tT. Even though cone is singular at endpoint,
        // it falls back to the branch tangent T_B = Unit(n0 x n1) at branchPoint.
        var tT = parameters[^1] + 0.3;
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.Prepare(
            in view, true, in endpoint, in branch, LimitTermUse.First, tT, out var anchor));
        Assert.True(IsFinite(anchor.PlaneNormal));
        Assert.True(IsFinite(anchor.LineDirection));
        Assert.InRange(Math.Abs(Dot(anchor.PlaneNormal, anchor.LineDirection)), 0, 1e-12);
    }
}
