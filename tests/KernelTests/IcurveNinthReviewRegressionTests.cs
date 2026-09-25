using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

// Review target: 2623cafdbb710768125a46447dda07b73a95b634.
// Draft regression tests. These tests have not been compiled or executed here.
// The small-positive-budget test targets the CURRENT non-memoized call graph.
// When changing that graph, instrument actual base-evaluation boundaries and
// test that actual evaluations <= Max and Used agrees with the actual count.
[Collection("KernelTests")]
public unsafe class IcurveNinthReviewRegressionTests : IDisposable
{
    public IcurveNinthReviewRegressionTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnknownPlan_ChartD0_IsRejectedByEveryEntry(bool throughL3)
    {
        var view = TwoSphereView();
        var identity = new GeometryIdentity(987631, 1);
        var t = view.ChartParameters[0];
        var unknown = (ICurveConstraintPlan)byte.MaxValue;
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = throughL3
            ? KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 0,
                unknown, output, out _)
            : ICurveEvaluation.EvaluateWithPlan(in view, t, 0,
                unknown, output, out _);

        Assert.Equal(AlgorithmStatus.InvalidInput, status);
        AssertSentinel(output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChartD0_ForcedPlanSemantics_DoNotDependOnCacheLayer(bool warmL3)
    {
        var view = TwoSphereView();
        var identity = new GeometryIdentity(987632, 1);
        var t = view.ChartParameters[0];
        var store = new EvaluationSampleStore(new CurveSample[4]);
        Span<KernelVector3> direct = stackalloc KernelVector3[1];
        Span<KernelVector3> cached = stackalloc KernelVector3[1];

        if (warmL3)
            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 0,
                    ICurveConstraintPlan.Auto, cached, out _));

        direct.Fill(Vector(42, 42, 42));
        cached.Fill(Vector(42, 42, 42));
        var directStatus = ICurveEvaluation.EvaluateWithCache(in view, t, 0,
            ICurveConstraintPlan.I1, ref store, direct, out _);
        var cachedStatus = KernelRuntime.EvaluateICurveThroughL3(in identity,
            in view, t, 0, ICurveConstraintPlan.I1, cached, out _);

        // This test does not prescribe whether a legal but inapplicable plan
        // should be ignored for a defining D0 anchor. Both entries must agree.
        Assert.Equal(directStatus, cachedStatus);
        if (directStatus == AlgorithmStatus.Success)
        {
            AssertClose(direct[0], view.ChartPositions[0]);
            AssertClose(cached[0], view.ChartPositions[0]);
        }
        else
        {
            AssertSentinel(direct);
            AssertSentinel(cached);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsufficientBudget_IdentityD2_DoesNotRunUnchargedWork(bool smallPositive)
    {
        var view = UnitCircleView();
        var t = view.ChartParameters[0];
        var anchor = view.ChartPositions[0];
        var budget = new EvaluationBudget(smallPositive ? 2 : 0);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));

        // Current source path, without memo reuse:
        //   ContinueTo.IsPublishableRoot:   4 implicit calls + 2 deviations
        //   SolveI1 fast loop:              1 implicit call
        //   SolveI1.IsPublishableRoot:      4 implicit calls + 2 deviations
        //   D2 SecondDirectional:          1 implicit call
        //   Solve.IsPublishableRoot:        4 implicit calls + 2 deviations
        // Two base-evaluation units do not cover these calls. The current
        // implementation deducts one unit per wrapper and nevertheless succeeds.
        // If real jet/memo reuse is introduced, replace this fixture-specific
        // assertion with a counter at the actual base-evaluation boundary.
        var status = ICurveContinuation.ContinueTo(in view, ICurveConstraintPlan.I1,
            t, in anchor, t, 0, 2, ref budget, output,
            out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.InRange(budget.Used, 0, budget.Max);
        AssertSentinel(output);
    }

    [Fact]
    public void SufficientBudget_IdentityContinuation_StillComputesD2()
    {
        var view = UnitCircleView();
        var t = view.ChartParameters[0];
        var anchor = view.ChartPositions[0];
        var budget = EvaluationBudget.Default;
        Span<KernelVector3> output = stackalloc KernelVector3[3];

        Assert.Equal(AlgorithmStatus.Success,
            ICurveContinuation.ContinueTo(in view, ICurveConstraintPlan.I1,
                t, in anchor, t, 0, 2, ref budget, output,
                out _, out _, out _));
        AssertClose(output[0], Vector(1, 0, 0));
        AssertClose(output[1], Vector(0, Math.Sqrt(2), 0));
        AssertClose(output[2], Vector(-2, -2, 0));
        Assert.True(budget.Used > 0);
    }

    [Fact]
    public void ValidAuto_ChartD0_StillReturnsTheOriginalAnchor()
    {
        var view = TwoSphereView();
        var identity = new GeometryIdentity(987633, 1);
        var t = view.ChartParameters[0];
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in view, t, 0, output, out _));
        AssertClose(output[0], view.ChartPositions[0]);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 0,
                ICurveConstraintPlan.Auto, output, out _));
        AssertClose(output[0], view.ChartPositions[0]);
    }

    private static ICurveView TwoSphereView()
    {
        var lower = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, -3), Vector(0, 0, 1), Vector(1, 0, 0), 5);
        var upper = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 3), Vector(0, 0, 1), Vector(1, 0, 0), 5);
        KernelVector3[] chart = [Vector(4, 0, 0), Vector(0, 4, 0)];
        return BuildView(in lower, in upper, chart);
    }

    private static ICurveView UnitCircleView()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] chart = [Vector(1, 0, 0), Vector(0, 1, 0)];
        return BuildView(in plane, in sphere, chart);
    }

    private static ICurveView BuildView(in AnalyticSurface support0,
        in AnalyticSurface support1, KernelVector3[] chart)
    {
        var tangents = new KernelVector3[chart.Length];
        for (var i = 0; i < chart.Length; i++)
        {
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in support0, in chart[i], 1, out var a));
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in support1, in chart[i], 1, out var b));
            tangents[i] = Unit(Cross(a.Gradient, b.Gradient));
        }
        var parameters = new double[chart.Length];
        var scales = new double[chart.Length - 1];
        var chords = new KernelVector3[chart.Length - 1];
        Assert.Equal(AlgorithmStatus.Success,
            OriginalChartParameterMap.Build(chart, tangents, 0, 1,
                parameters, scales, chords, out _, out _));
        return new ICurveView(in support0, ParasolidConstants.PK_TOPOL_sense_positive_c,
            in support1, ParasolidConstants.PK_TOPOL_sense_positive_c,
            chart, parameters, scales, chords);
    }

    private static void AssertClose(in KernelVector3 value, in KernelVector3 expected)
    {
        Assert.InRange(Math.Abs(value.X - expected.X), 0, 1e-12);
        Assert.InRange(Math.Abs(value.Y - expected.Y), 0, 1e-12);
        Assert.InRange(Math.Abs(value.Z - expected.Z), 0, 1e-12);
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
