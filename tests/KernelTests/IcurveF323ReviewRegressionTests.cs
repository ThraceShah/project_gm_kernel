// Review regression draft for f323ec0. Not compiled or run in this review.
using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveF323ReviewRegressionTests
{
    private static readonly AnalyticSurface Plane =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface Cylinder =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

    private static ICurveView RuleView()
    {
        var positions = new[] { Vector(1, 0, 0), Vector(0, 1, 0) };
        var tangents = new[] { Vector(0, 1, 0), Vector(-1, 0, 0) };
        var parameters = new double[2];
        var scales = new double[1];
        var chords = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0, 1, parameters, scales, chords, out _, out _));
        var end = new TerminatorLimit(LimitTermUse.First, Vector(-1, 0, 0), positions[1]);
        return new ICurveView(in Plane, 1, in Cylinder, 1,
            positions, parameters, scales, chords, default, end);
    }

    private static double Distance(in KernelVector3 a, in KernelVector3 b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X)
            + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminator_RuleChange_MustNotReuseAnotherParameterization(bool reverse)
    {
        var view = RuleView();
        var t = view.ChartParameters[^1] + 0.5;
        var warmRule = reverse ? TerminatorParameterRule.TangentMatching
            : TerminatorParameterRule.ExtensionRatio;
        var targetRule = reverse ? TerminatorParameterRule.ExtensionRatio
            : TerminatorParameterRule.TangentMatching;
        Span<KernelVector3> expected = stackalloc KernelVector3[1];
        Span<KernelVector3> warm = stackalloc KernelVector3[1];
        Span<KernelVector3> actual = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithRule(
            in view, t, 0, ICurveConstraintPlan.Auto, targetRule, expected, out _));
        Span<CurveSample> storage = stackalloc CurveSample[8];
        var cache = new EvaluationSampleStore(storage);
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, warmRule, ref cache, warm, out _));
        Assert.True(Distance(in warm[0], in expected[0]) > 0.2,
            "The two rule contexts must have distinguishable defining points.");
        // This test specifies automatic cache-context separation/invalidation.
        // An alternative API may explicitly reject an incompatible store, but
        // must preserve output and must never return another rule's point as Success.
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, targetRule, ref cache, actual, out _));
        Assert.InRange(Distance(in expected[0], in actual[0]), 0, 1e-12);
    }

    [Fact]
    public void Terminator_SameRule_RemainsSuccessfulAndExactlyReusable()
    {
        var view = RuleView();
        var t = view.ChartParameters[^1] + 0.5;
        Span<CurveSample> storage = stackalloc CurveSample[8];
        var cache = new EvaluationSampleStore(storage);
        Span<KernelVector3> cold = stackalloc KernelVector3[1];
        Span<KernelVector3> hot = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, TerminatorParameterRule.ExtensionRatio,
            ref cache, cold, out var coldReport));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, TerminatorParameterRule.ExtensionRatio,
            ref cache, hot, out var hotReport));
        Assert.Equal(CacheHitKind.Exact, hotReport.CacheHit);
        Assert.Equal(cold[0].X, hot[0].X);
        Assert.Equal(cold[0].Y, hot[0].Y);
        Assert.Equal(coldReport.Residual, hotReport.Residual);
        Assert.Equal(coldReport.QualityError, hotReport.QualityError);
    }

    [Fact]
    public void Terminator_Hit_MustNotTurnMeasuredNonDefiningDeviationIntoZero()
    {
        var view = RuleView();
        var t = view.ChartParameters[^1] + 0.5;
        Span<CurveSample> storage = stackalloc CurveSample[8];
        var cache = new EvaluationSampleStore(storage);
        Span<KernelVector3> result = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, TerminatorParameterRule.ExtensionRatio,
            ref cache, result, out var cold));
        Assert.True(cold.NonDefiningResidual > 0.2);
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, TerminatorParameterRule.ExtensionRatio,
            ref cache, result, out var hot));
        Assert.Equal(CacheHitKind.Exact, hot.CacheHit);
        // A revised report may instead have an explicit diagnostic-unavailable
        // flag. A bare numeric zero must not mean a measurement not performed.
        Assert.Equal(cold.NonDefiningResidual, hot.NonDefiningResidual);
    }

    [Theory]
    [InlineData(10000000000000.0)]
    [InlineData(9007199254740992.0)] // 2^53, exactly representable
    public void Spun_ExactAxisProjection_MustNotBecomeSingularAfterReferenceShift(double axial)
    {
        var axis = Vector(0, 0, 1);
        var point = Vector(0, 1, axial);
        var profile = Vector(1, 0, axial);
        var profileDu = Vector(0, 0, 1);
        var origin = Vector(0, 0, 0);
        var shifted = Vector(0, 0, axial);
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in point, in profile, in profileDu, in shifted, in axis,
            out var h1, out var h2, out _, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in point, in profile, in profileDu, in origin, in axis,
            out var g1, out var g2, out _, out _, out _, out _));
        Assert.Equal(0.0, h1); Assert.Equal(0.0, h2);
        Assert.Equal(h1, g1); Assert.Equal(h2, g2);
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in point, in profile, in origin, in axis, Math.PI / 2, out var angle));
        Assert.InRange(Math.Abs(angle - Math.PI / 2), 0, 1e-12);
    }

    [Fact]
    public void ChartPoint_ExactHit_MustUseStoredRawResidual_NotQuality()
    {
        var view = RuleView();
        var t = view.ChartParameters[^1];
        // Exercise report plumbing independently of nonlinear convergence.
        // Tiny raw residual is valid diagnostic data; position-quality is zero
        // because the chart point itself is defining data.
        const double rawResidual = 3e-17;
        Span<CurveSample> storage = stackalloc CurveSample[4];
        var cache = new EvaluationSampleStore(storage);
        Assert.True(cache.TryInsert(new CurveSample(t, view.ChartPositions[^1],
            Vector(-Math.Sqrt(2), 0, 0), default, 1, ICurveQueryKind.ChartPoint,
            ChartSide.Right, 0, 0, rawResidual, SampleSourceKind.CorrectedRoot,
            ICurveConstraintPlan.I1)));
        Span<KernelVector3> result = stackalloc KernelVector3[2];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 1, ICurveConstraintPlan.Auto, ref cache, result, out var report));
        Assert.Equal(CacheHitKind.Exact, report.CacheHit);
        Assert.Equal(rawResidual, report.Residual);
        Assert.Equal(0.0, report.QualityError);
    }
}
