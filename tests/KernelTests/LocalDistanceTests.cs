using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Local distance and offset tests (spec §9, task T11): the general
/// normal-foot elimination (K system + sensitivity Hessian) is validated
/// against the independent closed-form analytic distances (§21.2 two-path
/// rule), guards refuse singular/multi-foot configurations instead of
/// claiming a unique SDF, and the offset capability accepts only the true
/// distance form — the implicit φ−a impostor is shown to disagree.
/// </summary>
public class LocalDistanceTests
{
    private const double ValueTol = 1e-10;
    private const double GradientTol = 1e-9;
    private const double HessianTol = 1e-7;

    private static void CheckAgainstClosedForm(in AnalyticSurface surface, in KernelVector3 point)
    {
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.SolveFootPoint(in surface, in point, 1, 2, out var solved));
        Assert.Equal(AlgorithmStatus.Success,
            SurfaceDistanceEvaluation.Evaluate(in surface, in point, 1, 2, out var closed));

        Assert.InRange(Math.Abs(solved.Distance - closed.Distance), 0, ValueTol);
        Assert.InRange(Length(Delta(solved.Gradient, closed.Gradient)), 0, GradientTol);
        Assert.InRange(Math.Abs(solved.Hxx - closed.Hxx), 0, HessianTol);
        Assert.InRange(Math.Abs(solved.Hxy - closed.Hxy), 0, HessianTol);
        Assert.InRange(Math.Abs(solved.Hxz - closed.Hxz), 0, HessianTol);
        Assert.InRange(Math.Abs(solved.Hyy - closed.Hyy), 0, HessianTol);
        Assert.InRange(Math.Abs(solved.Hyz - closed.Hyz), 0, HessianTol);
        Assert.InRange(Math.Abs(solved.Hzz - closed.Hzz), 0, HessianTol);

        // Foot point sits on the surface and reproduces the closed-form foot.
        Assert.InRange(Length(Delta(solved.FootPoint, closed.FootPoint)), 0, ValueTol);

        // Consistency metrics (§9.1): the Hessian annihilates the normal
        // (distance is linear along its own gradient direction).
        var hn = Vector(
            solved.Hxx * solved.Gradient.X + solved.Hxy * solved.Gradient.Y + solved.Hxz * solved.Gradient.Z,
            solved.Hxy * solved.Gradient.X + solved.Hyy * solved.Gradient.Y + solved.Hyz * solved.Gradient.Z,
            solved.Hxz * solved.Gradient.X + solved.Hyz * solved.Gradient.Y + solved.Hzz * solved.Gradient.Z);
        Assert.InRange(Length(hn), 0, 1e-9);
    }

    private static KernelVector3 Delta(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static double Length(in KernelVector3 v)
        => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    [Fact]
    public void FootElimination_MatchesClosedForm_OnSphereCylinderPlane()
    {
        var sphere = new AnalyticSurface(SurfaceClass.Sphere, Vector(1, 2, 3), Vector(0, 0, 1), Vector(1, 0, 0), 2.0);
        CheckAgainstClosedForm(in sphere, Vector(2.5, 3.5, 4.5));
        CheckAgainstClosedForm(in sphere, Vector(-1.0, 0.8, 1.5));

        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        CheckAgainstClosedForm(in cylinder, Vector(2, 0, 5));
        CheckAgainstClosedForm(in cylinder, Vector(-0.5, -1.2, -3));

        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0.5, -0.5, 2), Vector(0, 0, 1), Vector(1, 0, 0));
        CheckAgainstClosedForm(in plane, Vector(3, -1, 5));
        CheckAgainstClosedForm(in plane, Vector(-2, 4, 0.5));
    }

    [Fact]
    public void FootElimination_MatchesClosedForm_OnCone()
    {
        var cone = new AnalyticSurface(SurfaceClass.Cone, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.5);
        var point = Vector(3, 0, 2);
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.SolveFootPoint(in cone, in point, 1, 2, out var solved));
        Assert.Equal(AlgorithmStatus.Success,
            SurfaceDistanceEvaluation.Evaluate(in cone, in point, 1, 2, out var closed));
        Assert.InRange(Math.Abs(solved.Distance - closed.Distance), 0, ValueTol);
        Assert.InRange(Length(Delta(solved.Gradient, closed.Gradient)), 0, GradientTol);
        // The cone's closed-form distance is exact on the surface and in its
        // regular region (linear in (ρ, z)); the foot solve agrees off-surface
        // to the first order as well on this meridian.
        Assert.InRange(Math.Abs(solved.FootPoint.Z - closed.FootPoint.Z), 0, 1e-6);
    }

    [Fact]
    public void FootElimination_SenseFlipsAllComponentsTogether()
    {
        var sphere = new AnalyticSurface(SurfaceClass.Sphere, Vector(1, 2, 3), Vector(0, 0, 1), Vector(1, 0, 0), 2.0);
        var point = Vector(2.5, 3.5, 4.5);
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.SolveFootPoint(in sphere, in point, 1, 2, out var positive));
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.SolveFootPoint(in sphere, in point, -1, 2, out var negative));
        Assert.Equal(-positive.Distance, negative.Distance, 12);
        Assert.Equal(-positive.Gradient.X, negative.Gradient.X, 12);
        Assert.Equal(-positive.Hxx, negative.Hxx, 12);
    }

    [Fact]
    public void FootElimination_RefusesSingularConfiguration()
    {
        // Cylinder axis: no witness angle, no unique normal.
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        var axisPoint = Vector(0, 0, 3);
        Assert.Equal(AlgorithmStatus.Singular,
            LocalDistanceEvaluation.SolveFootPoint(in cylinder, in axisPoint, 1, 2, out _));

        // Sphere center: witness refuses.
        var sphere = new AnalyticSurface(SurfaceClass.Sphere, Vector(1, 2, 3), Vector(0, 0, 1), Vector(1, 0, 0), 2.0);
        var center = Vector(1, 2, 3);
        Assert.Equal(AlgorithmStatus.Singular,
            LocalDistanceEvaluation.SolveFootPoint(in sphere, in center, 1, 2, out _));

        var nanPoint = Vector(0, 0, double.NaN);
        Assert.Equal(AlgorithmStatus.InvalidInput,
            LocalDistanceEvaluation.SolveFootPoint(in sphere, in nanPoint, 1, 2, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput,
            LocalDistanceEvaluation.SolveFootPoint(in sphere, in nanPoint, 0, 2, out _));
    }

    [Fact]
    public void FootElimination_ReportsConditionProxy_AboveRefusalThreshold()
    {
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        var point = Vector(2, 0, 5);
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.SolveFootPoint(in cylinder, in point, 1, 2, out var jet));
        Assert.True(jet.ConditionProxy >= LocalDistanceEvaluation.MinConditionProxy);
        Assert.True(jet.ConditionProxy <= 1.0);
        // Condition proxy in a healthy region stays respectable (order 1).
        Assert.True(jet.ConditionProxy > 1e-3);
    }

    [Fact]
    public void OffsetDistance_TrueDistanceForm_AgreesWithDefinitionalSurface()
    {
        // Base cylinder R=1, offset a=0.5: the offset surface is the cylinder
        // of radius 1.5. Points generated definitionally S_a(u,v) must satisfy
        // d_base(x) − a = 0; the impostor φ_base − a must NOT vanish there.
        var baseCylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        const double a = 0.5;
        for (var i = 0; i < 8; i++)
        {
            var u = Math.Tau * i / 8 + 0.13;
            var v = -1.4 + i * 0.31;
            var offsetPoint = Vector(
                1.5 * Math.Cos(u),
                1.5 * Math.Sin(u),
                v);
            Assert.Equal(AlgorithmStatus.Success,
                LocalDistanceEvaluation.TryOffsetDistance(in baseCylinder, 1, a, in offsetPoint, 2, out var jet));
            Assert.InRange(Math.Abs(jet.Distance), 0, ValueTol);
            // The offset's own gradient is the base normal (same foot branch).
            Assert.InRange(Math.Abs(Length(jet.Gradient) - 1), 0, 1e-12);

            // The impostor form disagrees on the same point.
            var rho = Math.Sqrt(offsetPoint.X * offsetPoint.X + offsetPoint.Y * offsetPoint.Y);
            var impostor = rho * rho - 1.0 - a;
            Assert.True(Math.Abs(impostor) > 0.1,
                "φ_base − a must not vanish on the offset surface (different geometry)");
        }

        // Negative offset with the signed distance: inner cylinder radius 0.5.
        var innerPoint = Vector(0.5, 0, 2);
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.TryOffsetDistance(in baseCylinder, 1, -0.5, in innerPoint, 1, out var inner));
        Assert.InRange(Math.Abs(inner.Distance), 0, ValueTol);
    }

    [Fact]
    public void OffsetDistance_RefusesFirstOrderBaseAndZeroOffset()
    {
        var cone = new AnalyticSurface(SurfaceClass.Cone, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.5);
        var point = Vector(3, 0, 2);
        // The cone's linear distance carries exact feet on the valid sheet
        // (§8.1), so the offset elimination is allowed there.
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.TryOffsetDistance(in cone, 1, 0.5, in point, 1, out var coneOffset));
        Assert.InRange(Math.Abs(coneOffset.Distance - ((3 - 2) / Math.Sqrt(1.25) - 0.5)), 0, ValueTol);
        // Far nappe: no distance capability, therefore no offset elimination.
        var farNappe = Vector(1, 0, -4);
        Assert.Equal(AlgorithmStatus.Unsupported,
            LocalDistanceEvaluation.TryOffsetDistance(in cone, 1, 0.5, in farNappe, 1, out _));
        // Torus has no distance capability in this slice at all.
        var torus = new AnalyticSurface(SurfaceClass.Torus, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 3.0, 1.0);
        var torusPoint = Vector(5, 0, 0.5);
        Assert.Equal(AlgorithmStatus.Unsupported,
            LocalDistanceEvaluation.TryOffsetDistance(in torus, 1, 0.5, in torusPoint, 1, out _));

        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        Assert.Equal(AlgorithmStatus.InvalidInput,
            LocalDistanceEvaluation.TryOffsetDistance(in cylinder, 1, 0, in point, 1, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput,
            LocalDistanceEvaluation.TryOffsetDistance(in cylinder, 1, 0.5, in point, -1, out _));
    }

    [Fact]
    public void OffsetDistance_InheritsSenseAndHessian()
    {
        var baseCylinder = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        var point = Vector(2, 0, 5);
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.TryOffsetDistance(in baseCylinder, 1, 0.5, in point, 2, out var positive));
        Assert.Equal(AlgorithmStatus.Success,
            LocalDistanceEvaluation.TryOffsetDistance(in baseCylinder, -1, 0.5, in point, 2, out var negative));
        // a stays along the oriented normal: d = s(ρ−R) − a = −1.5 here.
        Assert.Equal(-1.5, negative.Distance, 12);
        Assert.Equal(-positive.Gradient.X, negative.Gradient.X, 12);
        Assert.Equal(-positive.Hyy, negative.Hyy, 12);
    }
}
