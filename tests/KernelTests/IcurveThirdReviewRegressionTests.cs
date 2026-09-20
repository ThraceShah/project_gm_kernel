using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveThirdReviewRegressionTests
{
    [Fact]
    public void CrossSegmentWarmCache_MustPreserveTheColdSphereSphereBranch()
    {
        var first = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, -3), Vector(0, 0, 1), Vector(1, 0, 0), 5);
        var second = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 3), Vector(0, 0, 1), Vector(1, 0, 0), 5);
        KernelVector3[] chart =
        [
            Vector(4, 0, 0), Vector(0, 4, 0), Vector(-4, 0, 0),
            Vector(0, -4, 0), Vector(4, 0, 0),
        ];
        var view = BuildView(in first, in second, chart);
        var ta = view.ChartParameters[0]
            + 0.02 * (view.ChartParameters[1] - view.ChartParameters[0]);
        var tb = view.ChartParameters[3]
            + 0.98 * (view.ChartParameters[4] - view.ChartParameters[3]);
        var target = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        Span<KernelVector3> cold = new KernelVector3[2];
        Span<KernelVector3> warm = new KernelVector3[2];
        var store = new EvaluationSampleStore(new CurveSample[8]);

        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in view, target, 1, cold, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, ta, 1, ICurveConstraintPlan.Auto, ref store, warm, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, tb, 1, ICurveConstraintPlan.Auto, ref store, warm, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, target, 1, ICurveConstraintPlan.Auto, ref store, warm, out _));

        var expected = Vector(-2 * Math.Sqrt(2), 2 * Math.Sqrt(2), 0);
        Assert.InRange(Distance(cold[0], expected), 0, 1e-11);
        Assert.InRange(Distance(warm[0], expected), 0, 1e-11);
        Assert.InRange(Distance(cold[0], warm[0]), 0, 1e-11);
    }

    [Fact]
    public void NearlyTangentParameterPlane_MustNotAcceptRoundedZeroAsForwardAccuracy()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        const double delta = 1e-6;
        KernelVector3[] chart =
        [
            Vector(1, 0, 0),
            Vector(-Math.Cos(delta), Math.Sin(delta), 0),
        ];
        var view = BuildView(in plane, in sphere, chart);
        var t = 23 * Math.ScaleB(1.0, -52);
        Span<KernelVector3> output = new KernelVector3[1];
        output.Fill(Vector(42, 42, 42));

        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, t, 0, ICurveConstraintPlan.I1, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            Assert.Equal(42, output[0].X);
            Assert.Equal(42, output[0].Y);
            Assert.Equal(42, output[0].Z);
            return;
        }

        var e = view.ChartChordUnits[0];
        var k = (t - view.ChartParameters[0]) / (view.ChartScales[0] * -e.X);
        var beta = e.Y / -e.X;
        var a = 1 + beta * beta;
        var b = 2 * beta * (1 - k);
        var c = -k * (2 - k);
        var expectedY = -2 * c / (b + Math.Sqrt(b * b - 4 * a * c));
        Assert.InRange(Math.Abs(output[0].Y - expectedY),
            0, ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void ExactLinearIntersection_MustNotBeRejectedByAnAngleOnlyErrorFloor()
    {
        var sine = Math.ScaleB(1.0, -10);
        var cosine = Math.Sqrt(1 - sine * sine);
        var plane0 = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var plane1 = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, sine, cosine), Vector(1, 0, 0));
        KernelVector3[] chart = [Vector(0, 0, 0), Vector(-1, 0, 0)];
        var view = BuildView(in plane0, in plane1, chart);
        Span<KernelVector3> output = new KernelVector3[1];

        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithPlan(
            in view, 0.5, 0, ICurveConstraintPlan.I3, output, out _));
        Assert.Equal(-0.5, output[0].X);
        Assert.Equal(0.0, output[0].Y);
        Assert.Equal(0.0, output[0].Z);
    }

    [Fact]
    public void Auto_PlanePlaneIntersection_MustChooseAnApplicablePlan()
    {
        var plane0 = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var plane1 = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 1, 0), Vector(1, 0, 0));
        KernelVector3[] chart = [Vector(0, 0, 0), Vector(-1, 0, 0)];
        var view = BuildView(in plane0, in plane1, chart);
        Span<KernelVector3> baseline = new KernelVector3[1];
        Span<KernelVector3> automatic = new KernelVector3[1];

        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithPlan(
            in view, 0.5, 0, ICurveConstraintPlan.I3, baseline, out _));
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in view, 0.5, 0, automatic, out _));
        Assert.InRange(Distance(automatic[0], baseline[0]), 0, 1e-13);
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
}
