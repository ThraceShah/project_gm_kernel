using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveFourthReviewRegressionTests
{
    [Fact]
    public void TorusMeridian_SameSegmentColdQuery_MustNotJumpToOtherCircle()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var torus = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.5, 1);
        KernelVector3[] chart =
        [
            Vector(0, 1.0838531634528576, -0.9092974268256817),
            Vector(0, 2.0403023058681398, 0.8414709848078965),
        ];
        var view = BuildView(in plane, in torus, chart);
        var parameter = 0.1 * view.ChartParameters[1];
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.Evaluate(in view, parameter, 0, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertUnchanged(output);
            return;
        }
        var expected = Vector(0, 1.6463066710347496, -0.9892392824846421);
        Assert.True(output[0].Y > 0);
        Assert.InRange(Distance(output[0], expected), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void CylinderNearTangentParameterPlane_RoundedZeroMustNotProveAccuracy()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] chart =
        [
            Vector(1, 0, 0),
            Vector(-0.9999999999995, 9.999999999998333e-7, 0),
        ];
        var view = BuildView(in plane, in cylinder, chart);
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
        var e = view.ChartChordUnits[0];
        var k = parameter / (view.ChartScales[0] * -e.X);
        var beta = e.Y / -e.X;
        var a = 1 + beta * beta;
        var b = 2 * beta * (1 - k);
        var c = -k * (2 - k);
        var expectedY = -2 * c / (b + Math.Sqrt(b * b - 4 * a * c));
        Assert.InRange(Math.Abs(output[0].Y - expectedY), 0,
            ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void NonAxisAlignedSphere_RoundedZeroMustNotProveAccuracy()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 5);
        KernelVector3[] chart =
        [
            Vector(3, 4, 0),
            Vector(-3.0000039999985, -3.999996999998, 0),
        ];
        var view = BuildView(in plane, in sphere, chart);
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

    [Fact]
    public void OrdinaryUnitCircle_AutoStillReturnsPositionAndDerivatives()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] chart = [Vector(1, 0, 0), Vector(0, 1, 0)];
        var view = BuildView(in plane, in sphere, chart);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(
            in view, 0.5 * view.ChartParameters[1], 2, output, out _));
        var expected = Vector(1 / Math.Sqrt(2), 1 / Math.Sqrt(2), 0);
        Assert.InRange(Distance(output[0], expected), 0, 1e-13);
        Assert.True(Dot(output[1], output[1]) > 0);
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
        var delta = Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        return Math.Sqrt(Dot(delta, delta));
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
