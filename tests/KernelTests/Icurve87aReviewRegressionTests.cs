// Review regression tests for 87a82ef (GPT Review 5 / Round 15 remediation).
using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class Icurve87aReviewRegressionTests
{
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

    [Fact]
    public void Spun_AxisReferenceShiftAlongSameAxis_MustNotChangeRegularity()
    {
        var axis = Vector(0, 0, 1);
        var point = Vector(0, 1, 1e13);
        var profile = Vector(1, 0, 1e13);
        var profileDerivative = Vector(0, 0, 1);
        var origin = Vector(0, 0, 0);
        var sameAxisOtherOrigin = Vector(0, 0, 1e13);

        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in point, in profile, in profileDerivative, in sameAxisOtherOrigin, in axis,
            out var localHeight, out var localRadius, out _, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in point, in profile, in profileDerivative, in origin, in axis,
            out var originalHeight, out var originalRadius, out _, out _, out _, out _));
        Assert.Equal(0.0, localHeight);
        Assert.Equal(0.0, localRadius);
        Assert.Equal(localHeight, originalHeight);
        Assert.Equal(localRadius, originalRadius);
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in point, in profile, in origin, in axis, Math.PI / 2, out var angle));
        Assert.InRange(Math.Abs(angle - Math.PI / 2), 0, 1e-12);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.551115123125783e-17)] // 2^-54
    [InlineData(18014398509481984.0)] // 2^54
    public void Joint_GeometricallySimilarRegularSystems_ShouldUseRowScaling(double scale)
    {
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), scale);
        var r = scale / 4;
        var seed = Vector(scale + 2 * r, 0, 0);
        var planeAnchor = Vector(0, 0, 0);
        var planeNormal = Vector(0, 1, 0);
        Span<double> state = stackalloc double[6];
        var status = BlendJointLift.TryLiftFromLocalSingular(
            in PlaneZ0, in sphere, in PlaneZ0, in planeAnchor, in planeNormal,
            r * r, in seed, 0.0, scale, state, out var iterations, out _);
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.True(iterations >= 2);
        Assert.InRange(Math.Abs(state[0] / scale - 1.25), 0, 1e-10);
        Assert.InRange(Math.Abs(state[3] / scale - 1.0), 0, 1e-10);
    }

    private static KernelVector3 Circle(double angle)
        => Vector(Math.Cos(angle), Math.Sin(angle), 0);

    private static ICurveView TerminatorView()
    {
        double[] angles = { 0.0, 0.17, 0.62, 1.03 };
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Circle(angles[i]);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, 1.7, parameters, scales, chords, out _, out _));
        var end = new TerminatorLimit(LimitTermUse.First, Circle(1.28), Circle(1.03));
        return new ICurveView(in PlaneZ0, 1, in CylinderR1, 1,
            positions, parameters, scales, chords, default, end);
    }

    [Fact]
    public void Terminator_IdenticalExplicitRuleRequest_MustReuseAdequateSample()
    {
        var view = TerminatorView();
        var endpoint = Circle(1.28);
        var branch = Circle(1.03);
        Assert.Equal(AlgorithmStatus.Success, TerminatorEvaluation.TryResolveTerminatorParameter(
            in view, true, TerminatorParameterRule.ExtensionRatio,
            in endpoint, in branch, out var tT));
        var t = 0.5 * (view.ChartParameters[^1] + tT);
        Span<CurveSample> storage = stackalloc CurveSample[16];
        var cache = new EvaluationSampleStore(storage);
        Span<KernelVector3> first = stackalloc KernelVector3[1];
        Span<KernelVector3> second = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, TerminatorParameterRule.ExtensionRatio,
            ref cache, first, out var cold));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.Auto, TerminatorParameterRule.ExtensionRatio,
            ref cache, second, out var hot));
        Assert.Equal(CacheHitKind.Exact, hot.CacheHit);
        Assert.Equal(first[0].X, second[0].X);
        Assert.Equal(first[0].Y, second[0].Y);
        Assert.Equal(cold.Residual, hot.Residual);
        Assert.Equal(cold.QualityError, hot.QualityError);
    }

    [Fact]
    public void Refine_ColumnSkewedLargeCylinderSupport0_ConvergesToTrueRoot()
    {
        // Cylinder R=100 as Support0 ∩ Plane z=0 as Support1:
        // P2 unknowns (u, v) are the cylinder's parametric (theta, z),
        // with Jacobian column magnitudes ~100 (for theta) vs ~1 (for z).
        // This directly verifies the D_u scaling on the cylinder's angular UV.
        var radius = 100.0;
        double[] angles = [0.0, 0.17, 0.62, 1.03];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(radius * Math.Cos(angles[i]), radius * Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, 1.7 / radius, parameters, scales, chords, out _, out _));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), radius);
        var view = new ICurveView(in cylinder, 1, in PlaneZ0, 1, positions, parameters, scales, chords);

        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        var lambda = (t - view.ChartParameters[1]) / (view.ChartParameters[2] - view.ChartParameters[1]);
        var chord = Add(
            Scale(view.ChartPositions[1], 1 - lambda),
            Scale(view.ChartPositions[2], lambda));
        var seed = Add(chord, Vector(10, -10, 5));

        Span<double> refined = stackalloc double[4];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveCorrection.Refine(in view, ICurveConstraintPlan.P2, t, 1, in seed, refined,
                out _, out var residual));
        Assert.InRange(residual, 0, 1e-8);

        var theta = refined[0];
        var z = refined[1];
        var x = radius * Math.Cos(theta);
        var y = radius * Math.Sin(theta);
        var distFromAxis = Math.Sqrt(x * x + y * y);
        Assert.InRange(Math.Abs(distFromAxis - radius), 0, 1e-8);
        Assert.InRange(Math.Abs(z), 0, 1e-8);
    }

    [Fact]
    public unsafe void L3Cache_ExactHit_PreservesResidualAndQualityError()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
        try
        {
            var view = CircleView(1.0, 1.7);
            var identity = new GeometryIdentity(101, 1);
            var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
            Span<KernelVector3> first = stackalloc KernelVector3[3];
            Span<KernelVector3> second = stackalloc KernelVector3[3];

            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 1,
                    ICurveConstraintPlan.Auto, first, out var cold));
            Assert.Equal(CacheHitKind.None, cold.CacheHit);

            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 1,
                    ICurveConstraintPlan.Auto, second, out var hot));
            Assert.Equal(CacheHitKind.Exact, hot.CacheHit);
            Assert.Equal(cold.Residual, hot.Residual);
            Assert.Equal(cold.QualityError, hot.QualityError);
        }
        finally
        {
            KernelRuntime.SessionStop();
        }
    }

    [Fact]
    public unsafe void XtCone_RealParasolidFixture_MatchesInternalAnalyticSurface()
    {
        var repoRoot = FindRepoRoot();
        var fixturePath = Path.Combine(repoRoot, "docs", "reviews", "cone_probe_evidence", "probe_cone_v371.x_t");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist at {fixturePath}");
        var text = File.ReadAllText(fixturePath);
        var document = XtText.DecodeDocument(text);
        var cone = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.Cone);

        // Raw node fields in real PK fixture
        Assert.Equal(0.30151134457776363, cone.Fields[8].Vector.X, 14);
        Assert.Equal(-0.502518907629606, cone.Fields[8].Vector.Y, 14);
        Assert.Equal(0.810287391340663, cone.Fields[8].Vector.Z, 14);

        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
        try
        {
            Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryMaterializeAnalyticSurfaceFromXt(
                document, cone.Index, out var surfTag));
            var asked = new PK_CONE_sf_s();
            Assert.Equal(0, KernelRuntime.ConeAsk(surfTag, &asked));
            Assert.Equal(0.30151134457776363, asked.basis_set.axis.coord[0], 14);
            Assert.Equal(-0.502518907629606, asked.basis_set.axis.coord[1], 14);
            Assert.Equal(0.810287391340663, asked.basis_set.axis.coord[2], 14);
            Assert.Equal(-0.9372184261837676, asked.basis_set.ref_direction.coord[0], 14);
        }
        finally
        {
            KernelRuntime.SessionStop();
        }
    }

    private static ICurveView CircleView(double radius, double baseScale)
    {
        double[] angles = [0.0, 0.17, 0.62, 1.03];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(radius * Math.Cos(angles[i]), radius * Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, baseScale, parameters, scales, chords, out _, out _));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), radius);
        return new ICurveView(in PlaneZ0, 1, in cylinder, 1, positions, parameters, scales, chords);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new DirectoryNotFoundException("Repo root not found");
    }
}
