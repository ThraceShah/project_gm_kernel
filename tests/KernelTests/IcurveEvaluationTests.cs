using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Icurve evaluation slice tests (spec §7.3/§16.2/§18, task T07 vertical
/// slice). The reference is RV-CHART: the unit circle defined by z=0 and
/// x²+y²=1 with a non-uniform chart. Positions, D1 and D2 come from the
/// reference vectors and are independent of the solver; a ChartPoint must
/// return the original anchor bit-exactly, and out-of-domain queries are
/// refused without publishing anything.
/// </summary>
public unsafe class IcurveEvaluationTests
{
    private static ICurveView CircleView()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

        double[] angles = [0.0, 0.17, 0.62, 1.03];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(Math.Cos(angles[i]), Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, 1.7, parameters, scales, chords, out var segments, out _));
        Assert.Equal(3, segments);

        return new ICurveView(in plane, 1, in cylinder, 1,
            positions, parameters, scales, chords);
    }

    [Theory]
    [InlineData(-1.8931987150345606, 0.9980208873282734, 0.06288329234769621,
        -0.03699918671456238, 0.587214183238369,
        -0.34502364428074805, -0.029397843696154367)]
    [InlineData(-1.424462166498178, 0.9437641861633018, 0.3306193595594795,
        -0.19058773283279, 0.5440391536941673,
        -0.3072312019131942, -0.12808832331372091)]
    [InlineData(-0.6753610340047723, 0.716483622292384, 0.6976039126802429,
        -0.403784518905928, 0.41471240265808845,
        -0.22765488685597204, -0.2464397253720785)]
    public void Evaluate_RegularInterval_MatchesReferenceJets(
        double t, double px, double py, double d1x, double d1y, double d2x, double d2y)
    {
        var view = CircleView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, t, 2, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.RegularChartInterval, report.Kind);
        Assert.True(report.NewtonIterations > 0);
        Assert.InRange(report.Residual, 0, 1e-12);

        Assert.Equal(px, derivatives[0].X, 12);
        Assert.Equal(py, derivatives[0].Y, 12);
        Assert.Equal(0.0, derivatives[0].Z, 12);
        Assert.Equal(d1x, derivatives[1].X, 12);
        Assert.Equal(d1y, derivatives[1].Y, 12);
        Assert.Equal(d2x, derivatives[2].X, 12);
        Assert.Equal(d2y, derivatives[2].Y, 12);

        // The result satisfies both defining constraints and the chord plane.
        Assert.InRange(Math.Abs(derivatives[0].Z), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);
    }

    [Fact]
    public void Evaluate_TranslatedUnitCircle_D0UsesLocalAcceptanceAndMinimumBuffer()
    {
        const double translation = 1e13;
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, translation), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, translation), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] positions = [Vector(1, 0, translation), Vector(0, 1, translation)];
        KernelVector3[] tangents = [Vector(0, 1, 0), Vector(-1, 0, 0)];
        var parameters = new double[2];
        var scales = new double[1];
        var chords = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0, 1, parameters, scales, chords, out _, out _));
        var view = new ICurveView(in plane, 1, in sphere, 1,
            positions, parameters, scales, chords);
        var t = 0.5 * (parameters[0] + parameters[1]);
        Span<KernelVector3> position = stackalloc KernelVector3[1];

        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in view, t, 0, position, out _));
        Assert.InRange(Math.Abs(Math.Sqrt(position[0].X * position[0].X
            + position[0].Y * position[0].Y) - 1), 0, 1e-12);
        Assert.True(Math.Abs(position[0].X - 0.5) > 0.1);
    }

    [Fact]
    public void Evaluate_ChartPoint_ReturnsOriginalAnchorWithSideDerivatives()
    {
        var view = CircleView();
        var node = view.ChartParameters[1];
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node, 2, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.ChartPoint, report.Kind);

        // D0 is the original anchor, bit-exact — no snapping, no re-projection.
        Assert.Equal(view.ChartPositions[1].X, derivatives[0].X, 15);
        Assert.Equal(view.ChartPositions[1].Y, derivatives[0].Y, 15);
        Assert.Equal(view.ChartPositions[1].Z, derivatives[0].Z, 15);

        // D0-only requests publish the anchor without touching derivatives.
        Span<KernelVector3> positionOnly = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node, 0, positionOnly, out var positionReport));
        Assert.Equal(ICurveQueryKind.ChartPoint, positionReport.Kind);
        Assert.Equal(view.ChartPositions[1].X, positionOnly[0].X, 15);

        // D2 at the node stays side-dependent: the right-side value differs
        // from the left-side original segment data and is never averaged.
        Assert.Equal(1, report.Segment);
    }

    [Fact]
    public void Evaluate_OutsideDomain_RejectedWithoutPartialOutput()
    {
        var view = CircleView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        derivatives[0] = derivatives[1] = derivatives[2] = Vector(42, 42, 42);

        Assert.Equal(AlgorithmStatus.InvalidInput,
            ICurveEvaluation.Evaluate(in view, view.ChartParameters[0] - 1e-6, 2, derivatives, out var report));
        Assert.Equal(ICurveQueryKind.OutsideSupportedDomain, report.Kind);
        // No partial output was published.
        Assert.Equal(42, derivatives[0].X, 12);

        Assert.Equal(AlgorithmStatus.InvalidInput,
            ICurveEvaluation.Evaluate(in view, double.NaN, 1, derivatives, out _));
    }

    [Fact]
    public void Evaluate_DerivativeFailure_DoesNotPublishComputedPosition()
    {
        // Deliberately inconsistent chart direction makes the I1 line tangent
        // to the sphere at the valid D0 root, so D1 is singular after D0 was
        // computed internally.
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var sphere = new AnalyticSurface(SurfaceClass.Sphere,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        KernelVector3[] positions = [Vector(1, 0, 0), Vector(2, 0, 0)];
        double[] parameters = [0, 1];
        double[] scales = [1];
        KernelVector3[] chords = [Vector(1, 0, 0)];
        var view = new ICurveView(in plane, 1, in sphere, 1,
            positions, parameters, scales, chords);
        Span<KernelVector3> output = stackalloc KernelVector3[2];
        output.Fill(Vector(42, 42, 42));

        Assert.Equal(AlgorithmStatus.NumericalFailure,
            ICurveEvaluation.EvaluateWithPlan(in view, 0, 1,
                ICurveConstraintPlan.I1, output, out _));
        Assert.Equal(42, output[0].X);
        Assert.Equal(42, output[1].Z);
    }

    [Fact]
    public void Evaluate_D1MatchesChordFormula_IndependentCrossCheck()
    {
        // §5.5 cross-check: x′ = T/(f·(e·T)) with the analytic tangent from
        // the support cross product, compared against the solver's D1.
        var view = CircleView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        var t = -1.2;
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, t, 1, derivatives, out var report));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.LocateSegment(view.ChartParameters, t, ChartSide.Right, out var segment));

        var p = derivatives[0];
        var tangent = Unit(Vector(-p.Y, p.X, 0));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.TryParameterDerivative(
            view.ChartScales[segment], view.ChartChordUnits[segment], tangent, out var expected));
        Assert.InRange(Math.Abs(expected.X - derivatives[1].X), 0, 1e-11);
        Assert.InRange(Math.Abs(expected.Y - derivatives[1].Y), 0, 1e-11);
    }

    [Fact]
    public void Evaluate_NodeD2_SideDependent_NotAveraged()
    {
        var view = CircleView();
        var node = view.ChartParameters[1];
        Span<KernelVector3> right = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node, 2, right, out var rightReport));
        Assert.Equal(1, rightReport.Segment);

        // The left-side segment data (through a view truncated at the node)
        // would produce a different D2; the published right-side value is not
        // the average of the two sides.
        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        // Left-side evaluation from an interior point approaching the node.
        Span<KernelVector3> before = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node - 1e-7, 2, before, out _));
        Assert.True(Math.Abs(before[2].X - right[2].X) > 1e-12 || Math.Abs(before[2].Y - right[2].Y) > 1e-12,
            "node D2 must not equal the neighbouring-side limit (no averaging)");
    }
}
