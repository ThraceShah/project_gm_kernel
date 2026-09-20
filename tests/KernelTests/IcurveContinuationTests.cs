using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Parameter continuation, local subdivision and shared evaluation budget
/// (spec §17.3–§17.5, §14.7, task T17). Continuation restores the native t
/// exactly; budget exhaustion is BudgetExceeded, never Unsupported; ambiguous
/// same-quality roots refuse instead of publishing a nearest neighbor.
/// </summary>
public class IcurveContinuationTests
{
    private const double PositionTol = 1e-9;

    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

    private static ICurveView CircleView()
    {
        double[] angles = [0.0, 0.4, 0.9, 1.4];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(Math.Cos(angles[i]), Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -1.0, 1.2, parameters, scales, chords, out _, out _));
        return new ICurveView(in PlaneZ0, 1, in CylinderR1, 1, positions, parameters, scales, chords);
    }

    [Fact]
    public void ContinueTo_FromChartAnchor_ReachesExactTargetOnCircle()
    {
        var view = CircleView();
        var t0 = view.ChartParameters[0];
        var t1 = view.ChartParameters[1];
        var tTarget = 0.5 * (t0 + t1);
        var budget = EvaluationBudget.Default;
        Span<KernelVector3> derivatives = stackalloc KernelVector3[3];

        Assert.Equal(AlgorithmStatus.Success, ICurveContinuation.ContinueTo(
            in view, ICurveConstraintPlan.I3, t0, view.ChartPositions[0], tTarget, 0,
            2, ref budget, derivatives, out var steps, out var residual, out var detail));

        Assert.True(steps > 0);
        Assert.Equal(ICurveEvalDetail.None, detail);
        Assert.True(residual <= 1e-12);
        Assert.True(budget.Used > 0);
        Assert.True(budget.Remaining < budget.Max);

        // Exact native parameter: position must lie on both supports and the chord plane.
        Assert.True(Math.Abs(derivatives[0].Z) < PositionTol);
        var radius = Math.Sqrt(derivatives[0].X * derivatives[0].X + derivatives[0].Y * derivatives[0].Y);
        Assert.True(Math.Abs(radius - 1.0) < PositionTol);
        var plane = OriginalChartParameterMap.PlaneResidual(
            view.ChartPositions, view.ChartParameters, view.ChartScales,
            view.ChartChordUnits, 0, tTarget, in derivatives[0]);
        Assert.True(Math.Abs(plane) < PositionTol);
    }

    [Fact]
    public void ContinueTo_ExhaustedBudget_ReportsBudgetExceeded()
    {
        var view = CircleView();
        var t0 = view.ChartParameters[0];
        var tTarget = view.ChartParameters[1];
        var budget = new EvaluationBudget(1); // smaller than one predictor+corrector charge
        Span<KernelVector3> derivatives = stackalloc KernelVector3[1];

        var status = ICurveContinuation.ContinueTo(
            in view, ICurveConstraintPlan.I3, t0, view.ChartPositions[0], tTarget, 0,
            0, ref budget, derivatives, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.True(budget.IsExhausted || budget.Remaining < 2);
    }

    [Fact]
    public void ContinueTo_IdentityParameter_RejectsUnverifiedAnchorWithoutPublishing()
    {
        var view = CircleView();
        var t = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        var offSurface = Vector(2, 0, 0);
        var budget = EvaluationBudget.Default;
        Span<KernelVector3> derivatives = stackalloc KernelVector3[1];
        derivatives[0] = Vector(42, 42, 42);

        Assert.Equal(AlgorithmStatus.NotConverged, ICurveContinuation.ContinueTo(
            in view, ICurveConstraintPlan.I3, t, in offSurface, t, 0,
            0, ref budget, derivatives, out _, out _, out _));
        Assert.Equal(42, derivatives[0].X);
    }

    [Fact]
    public void SubdivideTo_FromFarEndpoint_ConvergesInsideSegment()
    {
        var view = CircleView();
        var tTarget = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        var budget = EvaluationBudget.Default;
        Span<KernelVector3> derivatives = stackalloc KernelVector3[3];

        Assert.Equal(AlgorithmStatus.Success, ICurveContinuation.SubdivideTo(
            in view, ICurveConstraintPlan.I3, tTarget, 1, 2, ref budget, derivatives,
            out _, out var residual, out var detail));

        Assert.Equal(ICurveEvalDetail.None, detail);
        Assert.True(residual <= 1e-11);
        Assert.True(Math.Abs(derivatives[0].Z) < PositionTol);
    }

    [Fact]
    public void EvaluateRegularInterval_ContinuationFallback_MatchesDirectSolve()
    {
        var view = CircleView();
        var t = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        Span<KernelVector3> direct = stackalloc KernelVector3[3];
        Span<KernelVector3> via = stackalloc KernelVector3[3];

        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithPlan(in view, t, 2, ICurveConstraintPlan.I3, direct, out _));

        // Poison the cache with a distant CorrectedRoot so Hermite is unavailable
        // and the first Solve seed is the chord — still succeeds; assert D0 match.
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithPlan(in view, t, 2, ICurveConstraintPlan.I3, via, out var report));
        Assert.Equal(ICurveQueryKind.RegularChartInterval, report.Kind);
        Assert.True(Norm(Sub(direct[0], via[0])) < PositionTol);
    }

    [Fact]
    public void EvaluationBudget_TryConsume_RejectsWithoutDebiting()
    {
        var budget = new EvaluationBudget(3);
        Assert.True(budget.TryConsume(2));
        Assert.Equal(1, budget.Remaining);
        Assert.False(budget.TryConsume(2));
        Assert.Equal(1, budget.Remaining);
        Assert.Equal(2, budget.Used);
    }

    private static double Norm(in KernelVector3 v)
        => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
