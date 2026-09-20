using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveFifthReviewRegressionTests
{
    [Fact]
    public void TorusMeridian_AnchorPredictionMustNotCrossToOtherCircle()
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
        var view = BuildView(in plane, in torus, chart);
        var parameter = 0.3 * view.ChartParameters[1];
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.Evaluate(in view, parameter, 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return;
        }
        var expected = Vector(0, 0.5107510993614423, 0.14624162398375118);
        Assert.True(output[0].Y > 0);
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void ConeCircle_RoundedZeroResidualDoesNotProvePositionAccuracy()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cone = new AnalyticSurface(SurfaceClass.Cone,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1, 0.5);
        KernelVector3[] chart =
        [
            Vector(1, 0, 0),
            Vector(-0.9999999999995, 9.999999999998333e-7, 0),
        ];
        var view = BuildView(in plane, in cone, chart);
        var parameter = 23 * Math.ScaleB(1.0, -52);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, parameter, 0, ICurveConstraintPlan.I1, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return;
        }
        var expected = Vector(1, 1.0111803261355515e-8, 0);
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void TorusEquator_RoundedZeroResidualDoesNotProvePositionAccuracy()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var torus = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 4, 1);
        KernelVector3[] chart =
        [
            Vector(3, 4, 0),
            Vector(-3.0000039999985, -3.999996999998, 0),
        ];
        var view = BuildView(in plane, in torus, chart);
        var parameter = 60 * Math.ScaleB(1.0, -52);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, parameter, 0, ICurveConstraintPlan.I1, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return;
        }
        var expected = Vector(2.999999978796119, 4.000000015902911, 0);
        Assert.InRange(Distance(output[0], expected), 0,
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
        var supportRadius = fixture == 3 ? 4.0 : 1.0;
        var secondary = fixture == 3 ? 1.0 : fixture == 2 ? 0.5 : 0.0;
        var circleRadius = fixture == 3 ? 5.0 : 1.0;
        var support = new AnalyticSurface(kind,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0),
            supportRadius, secondary);
        KernelVector3[] chart =
        [
            Vector(circleRadius, 0, 0),
            Vector(0, circleRadius, 0),
        ];
        var view = BuildView(in plane, in support, chart);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(
            in view, 0.5 * view.ChartParameters[1], 2, output, out _));
        var c = 1 / Math.Sqrt(2);
        Assert.InRange(Distance(output[0], Vector(circleRadius * c, circleRadius * c, 0)), 0, 1e-11);
        Assert.InRange(Distance(output[1], Vector(-c, c, 0)), 0, 1e-11);
        Assert.InRange(Distance(output[2], Vector(-c / circleRadius, -c / circleRadius, 0)), 0, 1e-11);
        foreach (var value in output)
            Assert.True(IsFinite(value));
    }

    private static ICurveView BuildView(in AnalyticSurface support0,
        in AnalyticSurface support1, KernelVector3[] chart)
    {
        var tangents = new KernelVector3[chart.Length];
        for (var i = 0; i < chart.Length; i++)
        {
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in support0, in chart[i], 1, out var jet0));
            Assert.Equal(AlgorithmStatus.Success,
                AnalyticImplicitEvaluation.Evaluate(in support1, in chart[i], 1, out var jet1));
            tangents[i] = Unit(Cross(jet0.Gradient, jet1.Gradient));
        }
        var parameters = new double[chart.Length];
        var scales = new double[chart.Length - 1];
        var chords = new KernelVector3[chart.Length - 1];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            chart, tangents, 0, 1, parameters, scales, chords, out _, out _));
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
