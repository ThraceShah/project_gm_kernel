// Review target: b85cf130dd66284fd8b2d97db8e4a6e2e9933ebc.
// Suggested destination: tests/KernelTests/IcurveB85ReviewRegressionTests.cs.
// Draft only: not compiled or executed in the review environment.
using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveB85ReviewRegressionTests
{
    [Fact]
    public void MultiSeed_MustNotTurnParameterResolutionFailureIntoAnotherMeridian()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var torus = MeridianTorus();
        var points = MeridianChart();
        var length = Distance(points[0], points[1]);
        var baseParameter = 4503599627370496.0; // 2^52
        var view = BuildView(in plane, in torus, points, baseParameter, 10.0 / length);
        var target = baseParameter + 1.0;
        Assert.Equal(baseParameter + 10.0, view.ChartParameters[1]);

        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.Evaluate(in view, target, 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            // A safe, diagnosed refusal is allowed when internal tracking cannot
            // resolve a safe step in the native parameter representation.
            AssertSentinel(output);
            return;
        }
        AssertPositiveMeridianRoot(in output[0], ICurveEvaluation.PublicationTolerance(in view));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1e-200)]
    [InlineData(1e200)]
    public void Svd_UniformScaling_MustKeepFullRankAndFiniteSingularValues(double scale)
    {
        Span<double> matrix = stackalloc double[4] { scale, 0, 0, scale };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];
        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdFactorizeSquare(matrix, 2, sigma, u, v, 1e-12, out var rank));
        Assert.Equal(2, rank);
        foreach (var singularValue in sigma)
        {
            Assert.True(double.IsFinite(singularValue));
            Assert.InRange(Math.Abs(singularValue / scale - 1.0), 0, 1e-12);
        }
        Span<double> rhs = stackalloc double[2] { scale, scale };
        Span<double> solution = stackalloc double[2];
        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdMinNormSolve(sigma, u, v, 2, rank, rhs, solution));
        Assert.InRange(Math.Abs(solution[0] - 1.0), 0, 1e-12);
        Assert.InRange(Math.Abs(solution[1] - 1.0), 0, 1e-12);
    }

    [Fact]
    public void Svd_PreviousSmallSingularValueRepair_RemainsEffective()
    {
        Span<double> matrix = stackalloc double[4] { 1, 1, 0, 1e-10 };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];
        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdFactorizeSquare(matrix, 2, sigma, u, v, 1e-12, out var rank));
        Assert.Equal(2, rank);
        var expectedSmall = 1e-10 / Math.Sqrt(2.0);
        Assert.InRange(Math.Abs(sigma[1] / expectedSmall - 1.0), 0, 1e-12);
    }

    [Fact]
    public void TerminatorScalarSolve_MustNotLeaveTheBranchConnectedMeridian()
    {
        // This tests the explicitly defined internal one-face/two-plane
        // construction, not the unresolved PK terminator parameter policy.
        var torus = MeridianTorus();
        var points = MeridianChart();
        var branch = points[0];
        var endpoint = points[1];
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in torus, in endpoint, 1, out var endpointJet));
        var normal = Unit(endpointJet.Gradient);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in endpoint, in branch, in normal,
                out var planeNormal, out var chordUnit, out var line));
        var chordRate = Difference(in endpoint, in branch);
        var anchor = new TerminatorEvaluation.TerminatorAnchor(0,
            in endpoint, in branch, in planeNormal, in chordUnit, in line, in chordRate,
            1.0, 0.0, Distance(endpoint, branch));
        var budget = EvaluationBudget.Default;
        var status = TerminatorEvaluation.SolveIntervalPoint(in torus, in anchor, 0.1,
            ref budget, out _, out var point, out _, out _);
        if (status == AlgorithmStatus.Success)
            AssertPositiveMeridianRoot(in point, 1.5e-13);
    }

    [Fact]
    public void I1RegularQuery_LeftSide_MustKeepReportAndCacheKeyConsistent()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        KernelVector3[] points = [Vector(1, 0, 0), Vector(0, 1, 0)];
        var view = BuildView(in plane, in sphere, points, 0.0, 1.0);
        var target = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        Span<CurveSample> storage = stackalloc CurveSample[8];
        var cache = new EvaluationSampleStore(storage);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, target, 0, ICurveConstraintPlan.Auto,
                ref cache, output, out var coldReport, ChartSide.Left));
        Assert.Equal(ChartSide.Left, coldReport.Side);
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, target, 0, ICurveConstraintPlan.Auto,
                ref cache, output, out var warmReport, ChartSide.Left));
        Assert.Equal(ChartSide.Left, warmReport.Side);
        Assert.Equal(CacheHitKind.Exact, warmReport.CacheHit);
    }

    private static AnalyticSurface MeridianTorus() => new(SurfaceClass.Torus,
        Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.5, 1.0);

    private static KernelVector3[] MeridianChart() =>
    [
        Vector(0, 1.0838531634528576, -0.9092974268256817),
        Vector(0, 2.0403023058681398, 0.8414709848078965),
    ];

    private static void AssertPositiveMeridianRoot(in KernelVector3 point, double tolerance)
    {
        var expected = Vector(0, 1.6463066710347496, -0.9892392824846421);
        Assert.True(point.Y > 0, "The negative-Y meridian is a different connected branch.");
        Assert.InRange(Distance(point, expected), 0, tolerance);
    }

    private static ICurveView BuildView(in AnalyticSurface s0, in AnalyticSurface s1,
        KernelVector3[] points, double baseParameter, double baseScale)
    {
        var tangents = new KernelVector3[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in s0, in points[i], 1, out var j0));
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in s1, in points[i], 1, out var j1));
            tangents[i] = Unit(Cross(j0.Gradient, j1.Gradient));
        }
        var parameters = new double[points.Length];
        var scales = new double[points.Length - 1];
        var chords = new KernelVector3[points.Length - 1];
        Assert.Equal(AlgorithmStatus.Success,
            OriginalChartParameterMap.Build(points, tangents, baseParameter, baseScale,
                parameters, scales, chords, out _, out _));
        return new ICurveView(in s0, ParasolidConstants.PK_TOPOL_sense_positive_c,
            in s1, ParasolidConstants.PK_TOPOL_sense_positive_c,
            points, parameters, scales, chords);
    }

    private static KernelVector3 Difference(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static double Distance(in KernelVector3 a, in KernelVector3 b)
    {
        var d = Difference(in a, in b);
        return Math.Sqrt(Dot(d, d));
    }

    private static void AssertSentinel(ReadOnlySpan<KernelVector3> values)
    {
        foreach (var value in values)
        {
            Assert.Equal(42.0, value.X);
            Assert.Equal(42.0, value.Y);
            Assert.Equal(42.0, value.Z);
        }
    }
}
