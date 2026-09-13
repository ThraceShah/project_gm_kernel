using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Constraint-plan tests (spec §7/§16, tasks T07 completion + T08): the five
/// plans agree on position/D1/D2 for the same semantic parameter and branch
/// (§21.1 常规计划 row), the branch survives multi-branch fixtures, witnesses
/// round-trip, and derivative chains match independent difference checks.
/// Tolerances used (recorded per §21.7): position 1e-10, D1 1e-9, D2 1e-7.
/// </summary>
public class IcurvePlansTests
{
    private const double PositionTol = 1e-10;
    private const double D1Tol = 1e-9;
    private const double D2Tol = 1e-7;

    internal static readonly ICurveConstraintPlan[] AllPlans =
    [
        ICurveConstraintPlan.I1,
        ICurveConstraintPlan.P2,
        ICurveConstraintPlan.I3,
        ICurveConstraintPlan.I2,
        ICurveConstraintPlan.P4,
    ];

    // Fixture surfaces live at type scope: ICurveView borrows them by `in`.
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
    private static readonly AnalyticSurface SphereSqrt2 =
        new(SurfaceClass.Sphere, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), Math.Sqrt(2));

    // Fixture A: plane z=0 ∩ cylinder R=1 (the RV-CHART circle).
    private static ICurveView CircleView()
    {
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
        return new ICurveView(in PlaneZ0, 1, in CylinderR1, 1, positions, parameters, scales, chords);
    }

    // Fixture B: cylinder R=1 ∩ sphere R=√2 — two branches z=±1; the chart
    // lives on z=+1. Neither support is a plane, so the auto plan is P2.
    private static ICurveView CylinderSphereView()
    {
        double[] angles = [0.0, 0.5, 1.1, 1.7];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(Math.Cos(angles[i]), Math.Sin(angles[i]), 1);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0.5, 1.3, parameters, scales, chords, out var segments, out _));
        Assert.Equal(3, segments);
        return new ICurveView(in CylinderR1, 1, in SphereSqrt2, 1, positions, parameters, scales, chords);
    }

    [Fact]
    public void PlanSelection_FollowsCapabilityRules()
    {
        // Plane support + implicit other → I1 (analytic preference, §7.6).
        Assert.Equal(ICurveConstraintPlan.I1, ICurveConstraintPlanRules.Select(CircleView()));
        // Two non-plane analytic supports → P2 (parametric side + implicit other).
        Assert.Equal(ICurveConstraintPlan.P2, ICurveConstraintPlanRules.Select(CylinderSphereView()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AllPlans_AgreeOnTheCircleBranch(int planId)
    {
        var view = CircleView();
        double[] queries = [-1.7, -1.2, -0.8];
        var plan = (ICurveConstraintPlan)planId;
        foreach (var t in queries)
        {
            var reference = Evaluate(in view, t, ICurveConstraintPlan.I3);
            var other = Evaluate(in view, t, plan);
            Assert.InRange(Length(Delta(other.Position, reference.Position)), 0, PositionTol);
            Assert.InRange(Length(Delta(other.D1, reference.D1)), 0, D1Tol);
            Assert.InRange(Length(Delta(other.D2, reference.D2)), 0, D2Tol);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AllPlans_AgreeAndStayOnUpperBranch(int planId)
    {
        var view = CylinderSphereView();
        double[] queries = [0.8, 1.3, 1.9];
        var plan = (ICurveConstraintPlan)planId;
        foreach (var t in queries)
        {
            var reference = Evaluate(in view, t, ICurveConstraintPlan.I3);
            // True branch: x²+y²=1 and z=+1 (not the z=−1 mirror).
            Assert.InRange(Math.Abs(reference.Position.X * reference.Position.X
                + reference.Position.Y * reference.Position.Y - 1), 0, 1e-10);
            Assert.InRange(Math.Abs(reference.Position.Z - 1), 0, 1e-10);
            var other = Evaluate(in view, t, plan);
            Assert.InRange(Length(Delta(other.Position, reference.Position)), 0, PositionTol);
            Assert.InRange(Math.Abs(other.Position.Z - 1), 0, 1e-10);
            Assert.InRange(Length(Delta(other.D1, reference.D1)), 0, D1Tol);
            Assert.InRange(Length(Delta(other.D2, reference.D2)), 0, D2Tol);
        }
    }

    [Fact]
    public void ForcedI1WithoutPlaneSupport_IsRefusedWithoutOutput()
    {
        var view = CylinderSphereView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        derivatives[0] = derivatives[1] = derivatives[2] = Vector(42, 42, 42);
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveEvaluation.EvaluateWithPlan(in view, 1.2, 2, ICurveConstraintPlan.I1, derivatives, out var report));
        Assert.Equal(42, derivatives[0].X, 12); // no partial output (§18.5)
    }

    [Fact]
    public void AutoPlan_IsRecordedInTheReport()
    {
        var circle = CircleView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in circle, -1.2, 2, derivatives, out var report));
        Assert.Equal(ICurveConstraintPlan.I1, report.Plan); // plane support → I1
        Assert.Equal(ChartSide.Right, report.Side);

        var mixed = CylinderSphereView();
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.Evaluate(in mixed, 1.2, 2, derivatives, out report));
        Assert.Equal(ICurveConstraintPlan.P2, report.Plan);
    }

    [Fact]
    public void WitnessRecovery_RoundTripsOnAllAnalyticClasses()
    {
        var surfaces = new AnalyticSurface[]
        {
            new(SurfaceClass.Plane, Vector(1, -2, 0.5), Vector(0, 0, 1), Vector(1, 0, 0)),
            new(SurfaceClass.Cylinder, Vector(0.5, 0.5, 0), Vector(0, 0, 1), Vector(1, 0, 0), 2.0),
            new(SurfaceClass.Cone, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.5),
            new(SurfaceClass.Sphere, Vector(1, 2, 3), Vector(0, 0, 1), Vector(1, 0, 0), 2.0),
            new(SurfaceClass.Torus, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 3.0, 1.0),
        };
        foreach (var surface in surfaces)
        {
            if (!SurfaceDerivativeLayout.TryCreate(0, 0, out var layout)) throw new Xunit.Sdk.XunitException("layout");
            Span<KernelVector3> value = new KernelVector3[1];
            (double u, double v)[] samples = surface.Kind switch
            {
                SurfaceClass.Sphere => [(0.3, 0.4), (2.0, -0.2)],
                SurfaceClass.Torus => [(0.3, 0.7), (2.5, -1.1)],
                SurfaceClass.Cone => [(0.3, 0.4), (2.0, 1.2)],
                _ => [(0.3, 0.4), (2.0, -1.2), (5.0, 0.9)],
            };
            foreach (var (u, v) in samples)
            {
                Assert.Equal(AlgorithmStatus.Success, SurfaceEvaluation.Evaluate(in surface, u, v, in layout, value));
                Assert.Equal(AlgorithmStatus.Success,
                    AnalyticParametricEvaluation.TryRecoverWitness(in surface, in value[0], out var ru, out var rv));
                // Angular witnesses live in the principal branch; compare on
                // the sample's own period sheet (§13.5).
                var expectedU = ParameterCorrespondence.Unwrap(ru, u, Math.Tau);
                Assert.InRange(Math.Abs(expectedU - u), 0, 1e-12);
                Assert.InRange(Math.Abs(rv - v), 0, 1e-12);
            }
        }

        // Singular points are refused, not guessed: cylinder axis has no angle.
        var cylinder = surfaces[1];
        var axisPoint = Vector(0.5, 0.5, 3);
        Assert.Equal(AlgorithmStatus.Singular,
            AnalyticParametricEvaluation.TryRecoverWitness(in cylinder, in axisPoint, out _, out _));
    }

    [Fact]
    public void D2_MatchesCentralDifferenceOfD1_WithinOneSegment()
    {
        // §21.2: multiple difference steps on a single smooth patch; the
        // solver D2 must match (D1(t+h) − D1(t−h))/2h.
        var view = CylinderSphereView();
        var t = 1.2;
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, t, 2, derivatives, out _));
        foreach (var h in new[] { 1e-4, 1e-5 })
        {
            Span<KernelVector3> plus = new KernelVector3[2];
            Span<KernelVector3> minus = new KernelVector3[2];
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, t + h, 1, plus, out _));
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, t - h, 1, minus, out _));
            var numerical = Scale(Delta(plus[1], minus[1]), 1 / (2 * h));
            Assert.InRange(Length(Delta(numerical, derivatives[2])), 0, D2Tol);
        }
    }

    [Fact]
    public void D1_MatchesTangentFormula_ForNonPlanePlans()
    {
        // §5.5 cross-check on the mixed fixture where the auto plan is P2.
        var view = CylinderSphereView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        var t = 1.3;
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, t, 1, derivatives, out var report));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.LocateSegment(
            view.ChartParameters, t, ChartSide.Right, out var segment));
        var p = derivatives[0];
        var tangent = Unit(Vector(-p.Y, p.X, 0));
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.TryParameterDerivative(
            view.ChartScales[segment], view.ChartChordUnits[segment], tangent, out var expected));
        Assert.InRange(Length(Delta(expected, derivatives[1])), 0, D1Tol);
    }

    [Fact]
    public void ChartNode_ContractHoldsForEveryPlan()
    {
        var view = CylinderSphereView();
        var node = view.ChartParameters[1];
        // I1 needs a plane support and is correctly refused on this fixture
        // (covered by ForcedI1WithoutPlaneSupport_IsRefusedWithoutOutput).
        foreach (var plan in new[] { ICurveConstraintPlan.P2, ICurveConstraintPlan.I3,
                     ICurveConstraintPlan.I2, ICurveConstraintPlan.P4 })
        {
            Span<KernelVector3> derivatives = new KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, node, 2, plan, derivatives, out var report));
            Assert.Equal(ICurveQueryKind.ChartPoint, report.Kind);
            // D0 is the original anchor bit-exact regardless of plan.
            Assert.Equal(view.ChartPositions[1].X, derivatives[0].X, 15);
            Assert.Equal(view.ChartPositions[1].Y, derivatives[0].Y, 15);
            Assert.Equal(view.ChartPositions[1].Z, derivatives[0].Z, 15);
        }
    }

    private static KernelVector3 Delta(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static double Length(in KernelVector3 v)
        => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private static (KernelVector3 Position, KernelVector3 D1, KernelVector3 D2) Evaluate(
        in ICurveView view, double t, ICurveConstraintPlan plan)
    {
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithPlan(in view, t, 2, plan, derivatives, out var report));
        Assert.Equal(AlgorithmStatus.Success, report.Status);
        Assert.True(report.NewtonIterations > 0);
        return (derivatives[0], derivatives[1], derivatives[2]);
    }
}
