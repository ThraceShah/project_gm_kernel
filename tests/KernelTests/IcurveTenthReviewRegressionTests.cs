using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

// Review target: 8b2fd1699d7da2b45e0c1b6db419f99752201466.
// Draft: not compiled or executed in the review environment.
// Costs below describe this commit's non-memoized exact-linear path.
// Replace fixed costs with independent boundary instrumentation if evaluation
// reuse or the call graph changes; an optimization need not retain these costs.
public class IcurveTenthReviewRegressionTests
{
    [Fact]
    public void ExactLinearSolve_I3D0_ChargesItsSixteenBaseCalls()
    {
        var view = LineView();
        var seed = Vector(-0.5, 0, 0);
        var budget = new EvaluationBudget(16);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.SolveDirect(in view, in seed, 0.5, 0, 0,
                ICurveConstraintPlan.I3, ref budget, output,
                out _, out _, out var detail));
        Assert.Equal(ICurveEvalDetail.None, detail);
        Assert.Equal(16, budget.Used);
        AssertPosition(output[0]);
    }

    [Fact]
    public void NonzeroContinuation_MustChargeNeighborhoodDeviations()
    {
        var view = LineView();
        var anchor = view.ChartPositions[0];
        var budget = new EvaluationBudget(76);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        Assert.Equal(AlgorithmStatus.Success,
            ICurveContinuation.ContinueTo(in view, ICurveConstraintPlan.I3,
                0, in anchor, 0.5, 0, 0, ref budget, output,
                out var steps, out _, out var detail));
        Assert.Equal(ICurveEvalDetail.None, detail);
        Assert.Equal(3, steps);
        AssertPosition(output[0]);

        // Three steps at t=1/8, 5/16, 1/2:
        //   predictor: 2 calls; SolveDirect(I3,D0): 16 calls;
        //   SameLocalBranch: 2 deviation calls.
        // Final target SolveDirect: 16 calls.
        // Actual total = 3*(2+16+2)+16 = 76, not 70.
        Assert.Equal(76, budget.Used);
        Assert.Equal(0, budget.Remaining);
    }

    [Fact]
    public void NonzeroContinuation_SeventyUnitsDoNotCoverSeventySixCalls()
    {
        var view = LineView();
        var anchor = view.ChartPositions[0];
        var budget = new EvaluationBudget(70);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveContinuation.ContinueTo(in view,
            ICurveConstraintPlan.I3, 0, in anchor, 0.5, 0, 0,
            ref budget, output, out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.InRange(budget.Used, 0, budget.Max);
        Assert.Equal(42.0, output[0].X);
        Assert.Equal(42.0, output[0].Y);
        Assert.Equal(42.0, output[0].Z);
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
        Assert.Equal(0.0, parameters[0]);
        Assert.Equal(1.0, parameters[1]);
        return new ICurveView(in planeZ, ParasolidConstants.PK_TOPOL_sense_positive_c,
            in planeY, ParasolidConstants.PK_TOPOL_sense_positive_c,
            points, parameters, scales, chords);
    }

    private static void AssertPosition(in KernelVector3 point)
    {
        Assert.Equal(-0.5, point.X);
        Assert.Equal(0.0, point.Y);
        Assert.Equal(0.0, point.Z);
    }
}
