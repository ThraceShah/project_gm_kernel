using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

[Collection("KernelTests")]
public unsafe class IcurveReviewRegressionTests : IDisposable
{
    public IcurveReviewRegressionTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void CachedD0_DoesNotBypassFailedD2ThroughZeroDistanceContinuation()
    {
        var view = CircleView(baseScale: 1e-200);
        var t = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        var store = new EvaluationSampleStore(new CurveSample[8]);
        Span<KernelVector3> output = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.I1, ref store, output, out _));

        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.EvaluateWithCache(
            in view, t, 2, ICurveConstraintPlan.I1, ref store, output, out _);
        Assert.NotEqual(AlgorithmStatus.Success, status);
        AssertSentinel(output);
    }

    [Fact]
    public void OuterTorusChart_DoesNotPublishInnerEquatorRoot()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var torus = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 2, 1);
        KernelVector3[] chart =
        [
            Vector(3, 0, 0),
            Vector(-1.5, 1.5 * Math.Sqrt(3), 0),
        ];
        var view = BuildView(in plane, in torus, chart, 1);
        var t = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        Span<KernelVector3> output = new KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, t, 0, ICurveConstraintPlan.I1, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertSentinel(output);
            return;
        }
        var expected = Vector(1.5, 1.5 * Math.Sqrt(3), 0);
        Assert.InRange(Distance(output[0], expected), 0, 1e-11);
    }

    [Fact]
    public void I3_OneUlpBoxContainingExactLinearRoot_IsNeverCertifiedEmpty()
    {
        const double a = 0.5773502691896258;
        var support0 = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(a, a, a), Vector(1, -1, 0));
        var support1 = new AnalyticSurface(SurfaceClass.Plane,
            Vector(3, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var chord = Vector(0, 0, 1);
        var root = Vector(3, 0.125, -3.125);
        var box = new IntervalBox3(
            Math.BitDecrement(root.X), Math.BitIncrement(root.X),
            Math.BitDecrement(root.Y), Math.BitIncrement(root.Y),
            Math.BitDecrement(root.Z), Math.BitIncrement(root.Z));
        var status = IntervalRootCheck.TryCertifyI3(
            in support0, in support1, in chord, -3.125, in box,
            out var certificate, out _);
        if (status == AlgorithmStatus.Unsupported)
        {
            Assert.Equal(IntervalRootStatus.BoundsUnavailable, certificate);
            return;
        }
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.NotEqual(IntervalRootStatus.Empty, certificate);
    }

    [Fact]
    public void NearlyTangentSpherePlane_DoesNotPublishChordAsAccurateRoot()
    {
        var z = 1.0 - Math.ScaleB(1.0, -48);
        var radius = Math.Sqrt((1 - z) * (1 + z));
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, z), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] chart = [Vector(radius, 0, z), Vector(0, radius, z)];
        var view = BuildView(in plane, in sphere, chart, 1);
        var t = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        Span<KernelVector3> output = new KernelVector3[1];
        output.Fill(Vector(42, 42, 42));
        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, t, 0, ICurveConstraintPlan.I1, output, out _);
        if (status != AlgorithmStatus.Success)
        {
            AssertSentinel(output);
            return;
        }
        var expected = Vector(radius / Math.Sqrt(2), radius / Math.Sqrt(2), z);
        Assert.InRange(Distance(output[0], expected),
            0, 4 * ICurveEvaluation.PublicationTolerance(in view));
    }

    [Fact]
    public void D0ThenD2_UpgradesTheCachedJetForTheNextD2Request()
    {
        var view = CircleView(1);
        var t = 0.5 * (view.ChartParameters[0] + view.ChartParameters[1]);
        var store = new EvaluationSampleStore(new CurveSample[8]);
        Span<KernelVector3> output = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.I1, ref store, output, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 2, ICurveConstraintPlan.I1, ref store, output, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 2, ICurveConstraintPlan.I1, ref store, output, out var report));
        Assert.Equal(CacheHitKind.Exact, report.CacheHit);
    }

    [Fact]
    public void L3_ExactHitBeyondL2PrefillCapacity_IsUsedByEvaluationEntry()
    {
        var view = CircleView(1);
        var identity = new GeometryIdentity(987654, 1);
        Span<KernelVector3> output = new KernelVector3[3];
        double last = 0;
        for (var i = 0; i < 17; i++)
        {
            last = view.ChartParameters[0]
                + (view.ChartParameters[1] - view.ChartParameters[0]) * (i + 1) / 18.0;
            Assert.Equal(AlgorithmStatus.Success, KernelRuntime.EvaluateICurveThroughL3(
                in identity, in view, last, 1, ICurveConstraintPlan.I1, output, out _));
        }
        Assert.True(GeometryEvaluationCache.TryGetExact(
            in identity, last, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, 1, ICurveEvaluation.CacheErrorBound, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.EvaluateICurveThroughL3(
            in identity, in view, last, 1, ICurveConstraintPlan.I1, output, out var report));
        Assert.Equal(CacheHitKind.Exact, report.CacheHit);
    }

    private static ICurveView CircleView(double baseScale)
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] chart = [Vector(1, 0, 0), Vector(0, 1, 0)];
        return BuildView(in plane, in sphere, chart, baseScale);
    }

    private static ICurveView BuildView(in AnalyticSurface support0,
        in AnalyticSurface support1, KernelVector3[] chart, double baseScale)
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

    private static void AssertSentinel(ReadOnlySpan<KernelVector3> output)
    {
        for (var i = 0; i < output.Length; i++)
        {
            Assert.Equal(42.0, output[i].X);
            Assert.Equal(42.0, output[i].Y);
            Assert.Equal(42.0, output[i].Z);
        }
    }
}
