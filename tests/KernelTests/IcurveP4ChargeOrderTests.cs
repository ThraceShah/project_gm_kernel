using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

// P4 must charge a surface evaluation only when that call is about to run.
// Counts come from SurfaceEvaluation.Evaluate, not from the budget counter.
public class IcurveP4ChargeOrderTests : IDisposable
{
    public IcurveP4ChargeOrderTests()
    {
        var view = LineView();
        Assert.True(SurfaceEvaluationCalls.Arm(in view.Support0, in view.Support1));
    }

    public void Dispose() => SurfaceEvaluationCalls.Disarm();

    [Fact]
    public void FastPair_BudgetZero_DoesNotCallEitherSurface()
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(0);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 2,
            ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(0, budget.Used);
        Assert.Equal(0, SurfaceEvaluationCalls.Support0);
        Assert.Equal(0, SurfaceEvaluationCalls.Support1);
        AssertSentinel(output);
    }

    [Fact]
    public void FastPair_BudgetOne_CallsOnlyTheFirstSurface()
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(1);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 2,
            ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(1, budget.Used);
        Assert.Equal(1, SurfaceEvaluationCalls.Support0);
        Assert.Equal(0, SurfaceEvaluationCalls.Support1);
        AssertSentinel(output);
    }

    [Fact]
    public void FastPair_FirstSurfaceFailure_DoesNotChargeTheSecond()
    {
        SurfaceEvaluationCalls.Disarm();
        var view = LineView();
        Assert.True(SurfaceEvaluationCalls.Arm(in view.Support0, in view.Support1, failSupport: 1));
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(8);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 0,
            ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.NotEqual(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(1, budget.Used);
        Assert.Equal(1, SurfaceEvaluationCalls.Support0);
        Assert.Equal(0, SurfaceEvaluationCalls.Support1);
        AssertSentinel(output);
    }

    [Fact]
    public void FastPair_SecondSurfaceFailure_ChargesBothCalls()
    {
        SurfaceEvaluationCalls.Disarm();
        var view = LineView();
        Assert.True(SurfaceEvaluationCalls.Arm(in view.Support0, in view.Support1, failSupport: 2));
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(8);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 0,
            ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.NotEqual(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(2, budget.Used);
        Assert.Equal(1, SurfaceEvaluationCalls.Support0);
        Assert.Equal(1, SurfaceEvaluationCalls.Support1);
        AssertSentinel(output);
    }

    [Fact]
    public void FastPair_BothSurfacesThenLaterBudget_KeepsTheCallerSentinel()
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(2);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 2,
            ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(2, budget.Used);
        Assert.Equal(1, SurfaceEvaluationCalls.Support0);
        Assert.Equal(1, SurfaceEvaluationCalls.Support1);
        AssertSentinel(output);
    }

    [Fact]
    public void CorrectorPair_BudgetOne_CallsOnlyTheFirstSurface()
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(1);
        Span<double> state = stackalloc double[4];

        var status = ICurveCorrection.Refine(in view, ICurveConstraintPlan.P4, 0.5, 0,
            in seed, state, ref budget, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(1, budget.Used);
        Assert.Equal(1, SurfaceEvaluationCalls.Support0);
        Assert.Equal(0, SurfaceEvaluationCalls.Support1);
    }

    [Fact]
    public void SufficientBudget_P4_StillPublishesTheLinePoint()
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = EvaluationBudget.Default;
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 2,
                ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail));
        Assert.Equal(ICurveEvalDetail.None, detail);
        Assert.Equal(-0.5, output[0].X);
        Assert.Equal(0.0, output[0].Y);
        Assert.Equal(0.0, output[0].Z);
        Assert.Equal(-1.0, output[1].X);
        Assert.Equal(0.0, output[1].Y);
        Assert.Equal(0.0, output[1].Z);
        Assert.Equal(0.0, output[2].X);
        Assert.Equal(0.0, output[2].Y);
        Assert.Equal(0.0, output[2].Z);
        Assert.True(budget.Used > 0);
        Assert.True(SurfaceEvaluationCalls.Support0 > 0);
        Assert.Equal(SurfaceEvaluationCalls.Support0, SurfaceEvaluationCalls.Support1);
    }

    [Theory]
    [InlineData(8, 1, 1)]
    [InlineData(9, 2, 1)]
    public void RootRebuild_StopsOnTheUnchargedSurface(int max, int support0, int support1)
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(max);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 2,
            ICurveConstraintPlan.P4, ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(max, budget.Used);
        Assert.Equal(support0, SurfaceEvaluationCalls.Support0);
        Assert.Equal(support1, SurfaceEvaluationCalls.Support1);
        AssertSentinel(output);
    }

    [Fact]
    public void Probe_DistinguishesPlanesThatDifferOnlyByReference()
    {
        SurfaceEvaluationCalls.Disarm();
        var first = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var second = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(0, 1, 0));
        Assert.True(SurfaceEvaluationCalls.Arm(in first, in second, failSupport: 2));
        Assert.True(SurfaceDerivativeLayout.TryCreate(0, 0, out var layout));
        Span<KernelVector3> jet = stackalloc KernelVector3[1];

        Assert.Equal(AlgorithmStatus.NotConverged,
            SurfaceEvaluation.Evaluate(in second, 0, 0, in layout, jet));
        Assert.Equal(0, SurfaceEvaluationCalls.Support0);
        Assert.Equal(1, SurfaceEvaluationCalls.Support1);
    }

    [Fact]
    public void Probe_RejectsTagsThatCompareEqual()
    {
        SurfaceEvaluationCalls.Disarm();
        var first = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var same = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        Assert.False(SurfaceEvaluationCalls.Arm(in first, in same, failSupport: 2));
        Assert.True(SurfaceDerivativeLayout.TryCreate(0, 0, out var layout));
        Span<KernelVector3> jet = stackalloc KernelVector3[1];

        Assert.Equal(AlgorithmStatus.Success,
            SurfaceEvaluation.Evaluate(in same, 1, 0, in layout, jet));
        Assert.Equal(0, SurfaceEvaluationCalls.Support0);
        Assert.Equal(0, SurfaceEvaluationCalls.Support1);
    }

    private static ICurveView LineView()
    {
        var planeZ = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var planeY = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 1, 0), Vector(1, 0, 0));
        KernelVector3[] points = [Vector(0, 0, 0), Vector(-1, 0, 0)];
        KernelVector3[] tangents = [Vector(-1, 0, 0), Vector(-1, 0, 0)];
        var parameters = new double[2];
        var scales = new double[1];
        var chords = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            OriginalChartParameterMap.Build(points, tangents, 0, 1,
                parameters, scales, chords, out _, out _));
        return new ICurveView(in planeZ, ParasolidConstants.PK_TOPOL_sense_positive_c,
            in planeY, ParasolidConstants.PK_TOPOL_sense_positive_c,
            points, parameters, scales, chords);
    }

    private static void AssertSentinel(ReadOnlySpan<KernelVector3> output)
    {
        foreach (var value in output)
        {
            Assert.Equal(42.0, value.X);
            Assert.Equal(42.0, value.Y);
            Assert.Equal(42.0, value.Z);
        }
    }
}
