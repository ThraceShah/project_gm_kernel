using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

// Review target: 21a0f1b7c0e8d0f394add13f563c012bdeebc274.
// Proposed regression tests: this file has not been compiled or executed.
public class IcurveSixthReviewRegressionTests
{
    [Fact]
    public void SmallNativeParameterScale_MustNotSkipTrackingAndPublishOtherBranch()
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
        var baseline = BuildView(in plane, in torus, chart, 1);
        Span<KernelVector3> original = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(
            in baseline, 0.3 * baseline.ChartParameters[1], 0, original, out _));
        var expected = Vector(0, 0.5107510993614423, 0.14624162398375118);
        Assert.InRange(Distance(original[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in baseline));

        // Only the parameter scale changes. The curve and fractional query do not.
        var scaled = BuildView(in plane, in torus, chart, 1e-12);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.Evaluate(
            in scaled, 0.3 * scaled.ChartParameters[1], 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return; // A diagnosed refusal is preferable to a wrong-branch Success.
        }
        Assert.True(output[0].Y > 0, "The defining chart is on the positive-Y meridian circle.");
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in scaled));
    }

    [Fact]
    public void ConeGenerator_LowPartLostBeforeCompensation_MustNotProveAccuracy()
    {
        var z = Math.ScaleB(1.0, -52);
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, z), Vector(0, 0, 1), Vector(1, 0, 0));
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1, 0.5);
        KernelVector3[] chart =
        [
            Vector(1, 0, z),
            Vector(-0.9999999999995, 9.999999999998333e-7, z),
        ];
        var view = BuildView(in plane, in cone, chart, 1);
        var t = 23 * Math.ScaleB(1.0, -52);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.Evaluate(in view, t, 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return;
        }

        // At z=2^-52 the real section radius is 1+2^-53. Do not round that
        // sum to double before forming r^2-1: carry delta explicitly instead.
        var delta = 0.5 * z;
        var chord = view.ChartChordUnits[0];
        var progress = (t - view.ChartParameters[0])
            / (view.ChartScales[0] * -chord.X);
        var beta = chord.Y / -chord.X;
        var a = 1 + beta * beta;
        var b = 2 * beta * (1 - progress);
        var positiveRhs = progress * (2 - progress) + delta * (2 + delta);
        var expectedY = 2 * positiveRhs
            / (b + Math.Sqrt(b * b + 4 * a * positiveRhs));
        Assert.InRange(Math.Abs(output[0].Y - expectedY), 0,
            ICurveEvaluation.PublicationTolerance(in view));
        Assert.InRange(Math.Abs(output[0].Z - z), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OrdinaryQuarterCircle_AllAnalyticSupportsStillReturnD0D1D2(int fixture)
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
        var view = BuildView(in plane, in support, chart, 1);
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
        in AnalyticSurface support1, KernelVector3[] chart, double baseScale)
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
            chart, tangents, 0, baseScale, parameters, scales, chords, out _, out _));
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
