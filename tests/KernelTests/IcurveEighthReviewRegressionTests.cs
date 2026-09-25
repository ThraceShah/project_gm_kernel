using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

// Review target: 7a4fb66b9cfe1c6c2cd757dac517c136416031dd.
// Proposed tests. Not compiled or executed in the review environment.
[Collection("KernelTests")]
public unsafe class IcurveEighthReviewRegressionTests : IDisposable
{
    public IcurveEighthReviewRegressionTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void L2_UnsupportedForcedPlanMustNotBecomeSupportedAfterCacheWarmup()
    {
        var view = TwoSphereView();
        var t = 0.5 * view.ChartParameters[1];
        var cache = new EvaluationSampleStore(new CurveSample[4]);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        // I1 requires exactly one plane. This pair contains no plane.
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveEvaluation.EvaluateWithCache(in view, t, 0,
                ICurveConstraintPlan.I1, ref cache, output, out _));
        AssertSentinel(output);

        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, t, 0,
                ICurveConstraintPlan.I2, ref cache, output, out _));

        output.Fill(Vector(42, 42, 42));
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveEvaluation.EvaluateWithCache(in view, t, 0,
                ICurveConstraintPlan.I1, ref cache, output, out _));
        AssertSentinel(output);
    }

    [Fact]
    public void L3_ExactHitMustNotBypassForcedPlanCapabilityValidation()
    {
        var view = TwoSphereView();
        var t = 0.5 * view.ChartParameters[1];
        var identity = new GeometryIdentity(876543, 1);
        Span<KernelVector3> output = stackalloc KernelVector3[1];

        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 0,
                ICurveConstraintPlan.I2, output, out _));

        output.Fill(Vector(42, 42, 42));
        Assert.Equal(AlgorithmStatus.Unsupported,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 0,
                ICurveConstraintPlan.I1, output, out _));
        AssertSentinel(output);
    }

    [Fact]
    public void ZeroBudget_IdentityContinuationMustNotComputeMissingD2()
    {
        var view = UnitCircleView();
        var budget = new EvaluationBudget(0);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        output.Fill(Vector(42, 42, 42));
        var t = view.ChartParameters[0];
        var anchor = view.ChartPositions[0];

        // A known position is not a cached D2 jet. Computing missing derivatives
        // still needs support evaluations and must consume the shared budget.
        var status = ICurveContinuation.ContinueTo(in view, ICurveConstraintPlan.I1,
            t, in anchor, t, 0, 2, ref budget, output,
            out _, out _, out var detail);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.BudgetExceeded, detail);
        Assert.Equal(0, budget.Used);
        AssertSentinel(output);
    }

    [Fact]
    public void PositiveBudget_IdentityContinuationStillComputesD2()
    {
        var view = UnitCircleView();
        var budget = EvaluationBudget.Default;
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        var t = view.ChartParameters[0];
        var anchor = view.ChartPositions[0];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveContinuation.ContinueTo(in view, ICurveConstraintPlan.I1,
                t, in anchor, t, 0, 2, ref budget, output,
                out _, out _, out _));
        AssertClose(output[0], Vector(1, 0, 0));
        AssertClose(output[1], Vector(0, Math.Sqrt(2), 0));
        AssertClose(output[2], Vector(-2, -2, 0));
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
