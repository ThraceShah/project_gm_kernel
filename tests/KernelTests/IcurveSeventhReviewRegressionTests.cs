using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

// Review target: 627f78a0082b1bd79560daa18b2c236e607d53d2.
// Proposed regression tests; not compiled or executed in the review environment.
// Numeric reference evidence is in the accompanying numeric_review_evidence.json.
public class IcurveSeventhReviewRegressionTests
{
    [Fact]
    public void LargeParameterBase_MustNotInflateTrackingStepAndSwitchBranch()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var torus = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.1, 1);
        KernelVector3[] chart =
        [
            Vector(0, 1.170737201667703, 0.9974949866040544),
            Vector(0, 0.44635637913638815, -0.7568024953079282),
        ];
        const double baseScale = 1.0537578582454332;
        var expected = Vector(0, 0.17569762136753667, 0.38166099205233195);

        var ordinary = BuildView(in plane, in torus, chart, 0, baseScale);
        Span<KernelVector3> baseline = stackalloc KernelVector3[1];
        Assert.Equal(2.0, ordinary.ChartParameters[1]);
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in ordinary, 1, 0, baseline, out _));
        Assert.InRange(Distance(baseline[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in ordinary));

        var baseParameter = Math.ScaleB(1.0, 52);
        var shifted = BuildView(in plane, in torus, chart, baseParameter, baseScale);
        var query = baseParameter + 1;
        Assert.Equal(baseParameter + 2, shifted.ChartParameters[1]);
        Assert.True(query > shifted.ChartParameters[0]);
        Assert.True(query < shifted.ChartParameters[1]);

        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.Evaluate(in shifted, query, 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return; // A located precision/branch refusal is safer than another branch.
        }
        Assert.True(output[0].Y > 0, "The original chart lies on the positive-Y meridian circle.");
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in shifted));
    }

    [Fact]
    public void LongAxialCone_GeometricDeviationMustNotEraseRadialDistance()
    {
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1, 1e-16);
        var point = Vector(1.9, 0, 1e16);
        var status = AnalyticImplicitEvaluation.GeometricDeviation(in cone, in point, out var distance);
        if (status != AlgorithmStatus.Success)
            return; // Unsupported accuracy must not be relabelled a zero distance.
        Assert.InRange(Math.Abs(distance - 0.1), 0, 1e-13);
    }

    [Fact]
    public void LongAxialCone_QuarterCircleMustNotPublishWrongRadius()
    {
        const double z = 1e16;
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, z), Vector(0, 0, 1), Vector(1, 0, 0));
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1, 1e-16);
        KernelVector3[] chart = [Vector(2, 0, z), Vector(0, 2, z)];
        var view = BuildView(in plane, in cone, chart, 0, 1);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.Evaluate(
            in view, 0.5 * view.ChartParameters[1], 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return;
        }

        // The exact radius from the stored k and z differs from 2 by < 3e-17.
        // This rounded reference is therefore sufficient for a 1e-13 test.
        var expected = Vector(Math.Sqrt(2), Math.Sqrt(2), z);
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void PreviousShortNativeScaleFixture_StillTracksItsOriginalBranch()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var torus = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.5, 1);
        KernelVector3[] chart =
        [
            Vector(0, 1.0838531634528576, 0.9092974268256817),
            Vector(0, 1.5874989834394464, -0.9961646088358407),
        ];
        var view = BuildView(in plane, in torus, chart, 0, 1e-12);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(
            in view, 0.3 * view.ChartParameters[1], 0, output, out _));
        var expected = Vector(0, 0.5107510993614423, 0.14624162398375118);
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void PreviousConeGeneratorLowPart_MustRemainPreserved()
    {
        var z = Math.ScaleB(1.0, -52);
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1, 0.5);
        var point = Vector(1, 0, z);
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in cone, in point, 0, out var jet));
        // phi = 1 - (1 + 2^-53)^2 = -2^-52 - 2^-106.
        Assert.InRange(Math.Abs(jet.Value + Math.ScaleB(1.0, -52)), 0, 1e-30);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OrdinaryQuarterCircle_AllAnalyticSupportsReturnExpectedJets(int fixture)
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var kind = fixture switch
        {
            0 => SurfaceClass.Sphere,
            1 => SurfaceClass.Cylinder,
            2 => SurfaceClass.Cone,
            _ => SurfaceClass.Torus,
        };
        var radius = fixture == 3 ? 5.0 : 1.0;
        var support = new AnalyticSurface(kind,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0),
            fixture == 3 ? 4.0 : 1.0,
            fixture == 3 ? 1.0 : fixture == 2 ? 0.5 : 0.0);
        KernelVector3[] chart = [Vector(radius, 0, 0), Vector(0, radius, 0)];
        var view = BuildView(in plane, in support, chart, 0, 1);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(
            in view, 0.5 * view.ChartParameters[1], 2, output, out _));
        var c = 1 / Math.Sqrt(2);
        Assert.InRange(Distance(output[0], Vector(radius * c, radius * c, 0)), 0, 1e-11);
        Assert.InRange(Distance(output[1], Vector(-c, c, 0)), 0, 1e-11);
        Assert.InRange(Distance(output[2], Vector(-c / radius, -c / radius, 0)), 0, 1e-11);
        foreach (var value in output)
            Assert.True(IsFinite(value));
    }

    private static ICurveView BuildView(in AnalyticSurface support0,
        in AnalyticSurface support1, KernelVector3[] chart, double baseParameter, double baseScale)
    {
        var tangents = new KernelVector3[chart.Length];
        for (var i = 0; i < chart.Length; i++)
        {
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in support0, in chart[i], 1, out var j0));
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in support1, in chart[i], 1, out var j1));
            tangents[i] = Unit(Cross(j0.Gradient, j1.Gradient));
        }
        var parameters = new double[chart.Length];
        var scales = new double[chart.Length - 1];
        var chords = new KernelVector3[chart.Length - 1];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            chart, tangents, baseParameter, baseScale, parameters, scales, chords, out _, out _));
        return new ICurveView(in support0, ParasolidConstants.PK_TOPOL_sense_positive_c,
            in support1, ParasolidConstants.PK_TOPOL_sense_positive_c,
            chart, parameters, scales, chords);
    }

    private static double Distance(in KernelVector3 a, in KernelVector3 b)
    {
        var d = Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        return Math.Sqrt(Dot(d, d));
    }

    private static void AssertUnchanged(ReadOnlySpan<KernelVector3> output)
    {
        foreach (var value in output)
        {
            Assert.Equal(42.0, value.X);
            Assert.Equal(42.0, value.Y);
            Assert.Equal(42.0, value.Z);
        }
    }
}
