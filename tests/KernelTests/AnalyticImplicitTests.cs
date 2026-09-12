using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Analytic implicit and oriented-distance jets (spec §8, task T03): closed
/// forms against manufactured solutions, central-difference cross-checks on a
/// single smooth patch, and rejection of zero-residual candidates on branches
/// the analytic surface does not contain (wrong cone nappe, torus mirror
/// sheet). ImplicitJet and OrientedDistanceJet are distinct types and never
/// interchangeable.
/// </summary>
public class AnalyticImplicitTests
{
    private static AnalyticSurface Sphere() => new(SurfaceClass.Sphere,
        Vector(1, 2, 3), Vector(0, 0, 1), Vector(1, 0, 0), 2.0);
    private static AnalyticSurface Cylinder() => new(SurfaceClass.Cylinder,
        Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
    private static AnalyticSurface Cone() => new(SurfaceClass.Cone,
        Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.5);
    private static AnalyticSurface RingTorus() => new(SurfaceClass.Torus,
        Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 3.0, 1.0);
    private static AnalyticSurface SpindleTorus() => new(SurfaceClass.Torus,
        Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 2.0);
    private static AnalyticSurface Plane() => new(SurfaceClass.Plane,
        Vector(0.5, -0.5, 2), Vector(0, 0, 1), Vector(1, 0, 0));

    private static void CheckImplicitGradient(Func<KernelVector3, ImplicitJet> eval, in KernelVector3 p,
        double tolerance)
    {
        const double h = 1e-5;
        var jet = eval(p);
        var px = eval(Vector(p.X + h, p.Y, p.Z));
        var mx = eval(Vector(p.X - h, p.Y, p.Z));
        var py = eval(Vector(p.X, p.Y + h, p.Z));
        var my = eval(Vector(p.X, p.Y - h, p.Z));
        var pz = eval(Vector(p.X, p.Y, p.Z + h));
        var mz = eval(Vector(p.X, p.Y, p.Z - h));
        Assert.InRange(Math.Abs(jet.Gradient.X - (px.Value - mx.Value) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Gradient.Y - (py.Value - my.Value) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Gradient.Z - (pz.Value - mz.Value) / (2 * h)), 0, tolerance);
    }

    private static void CheckImplicitHessian(Func<KernelVector3, ImplicitJet> eval, in KernelVector3 p,
        double tolerance)
    {
        const double h = 1e-5;
        var jet = eval(p);
        var px = eval(Vector(p.X + h, p.Y, p.Z));
        var mx = eval(Vector(p.X - h, p.Y, p.Z));
        var py = eval(Vector(p.X, p.Y + h, p.Z));
        var my = eval(Vector(p.X, p.Y - h, p.Z));
        var pz = eval(Vector(p.X, p.Y, p.Z + h));
        var mz = eval(Vector(p.X, p.Y, p.Z - h));
        Assert.InRange(Math.Abs(jet.Hxx - (px.Gradient.X - mx.Gradient.X) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hyy - (py.Gradient.Y - my.Gradient.Y) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hzz - (pz.Gradient.Z - mz.Gradient.Z) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hxy - (px.Gradient.Y - mx.Gradient.Y) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hxz - (px.Gradient.Z - mx.Gradient.Z) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hyz - (py.Gradient.Z - my.Gradient.Z) / (2 * h)), 0, tolerance);
    }

    private static void CheckDistanceJet(Func<KernelVector3, OrientedDistanceJet> eval, in KernelVector3 p,
        double tolerance)
    {
        const double h = 1e-5;
        var jet = eval(p);
        var px = eval(Vector(p.X + h, p.Y, p.Z));
        var mx = eval(Vector(p.X - h, p.Y, p.Z));
        var py = eval(Vector(p.X, p.Y + h, p.Z));
        var my = eval(Vector(p.X, p.Y - h, p.Z));
        var pz = eval(Vector(p.X, p.Y, p.Z + h));
        var mz = eval(Vector(p.X, p.Y, p.Z - h));
        Assert.InRange(Math.Abs(jet.Gradient.X - (px.Distance - mx.Distance) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Gradient.Y - (py.Distance - my.Distance) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Gradient.Z - (pz.Distance - mz.Distance) / (2 * h)), 0, tolerance);
        // Hessian as the difference of the analytic gradient.
        Assert.InRange(Math.Abs(jet.Hxx - (px.Gradient.X - mx.Gradient.X) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hyy - (py.Gradient.Y - my.Gradient.Y) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hzz - (pz.Gradient.Z - mz.Gradient.Z) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hxy - (px.Gradient.Y - mx.Gradient.Y) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hxz - (px.Gradient.Z - mx.Gradient.Z) / (2 * h)), 0, tolerance);
        Assert.InRange(Math.Abs(jet.Hyz - (py.Gradient.Z - my.Gradient.Z) / (2 * h)), 0, tolerance);
    }

    [Fact]
    public void Sphere_ImplicitAndDistance_MatchClosedForms()
    {
        var sphere = Sphere();
        var p = Vector(2.5, 3.5, 4.5); // r = (1.5, 1.5, 1.5), ‖r‖ = 2.598…
        Assert.Equal(AlgorithmStatus.Success, AnalyticImplicitEvaluation.Evaluate(in sphere, in p, 2, out var jet));
        Assert.Equal(6.75 - 4.0, jet.Value, 12);
        Assert.Equal(3.0, jet.Gradient.X, 12);
        Assert.Equal(2.0, jet.Hxx, 12);
        Assert.Equal(0.0, jet.Hxy, 12);
        CheckImplicitGradient(q => { AnalyticImplicitEvaluation.Evaluate(in sphere, in q, 1, out var j); return j; }, in p, 1e-6);
        CheckImplicitHessian(q => { AnalyticImplicitEvaluation.Evaluate(in sphere, in q, 2, out var j); return j; }, in p, 1e-6);

        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in sphere, in p, 1, 2, out var distance));
        Assert.Equal(Math.Sqrt(6.75) - 2.0, distance.Distance, 12);
        Assert.InRange(Math.Abs(Dot(distance.Gradient, distance.Gradient) - 1), 0, 1e-12);
        Assert.Equal(DistanceGrade.Exact, distance.Grade);
        // Foot witness back on the sphere.
        var footDelta = Math.Sqrt((distance.FootPoint.X - 1) * (distance.FootPoint.X - 1)
            + (distance.FootPoint.Y - 2) * (distance.FootPoint.Y - 2)
            + (distance.FootPoint.Z - 3) * (distance.FootPoint.Z - 3));
        Assert.Equal(2.0, footDelta, 12);
        CheckDistanceJet(q => { SurfaceDistanceEvaluation.Evaluate(in sphere, in q, 1, 2, out var d); return d; }, in p, 1e-6);

        // The sphere center has no unique normal: refused, not invented.
        var center = Vector(1, 2, 3);
        Assert.Equal(AlgorithmStatus.Singular, SurfaceDistanceEvaluation.Evaluate(in sphere, in center, 1, 2, out _));
    }

    [Fact]
    public void Cylinder_ImplicitAndDistance_AxisSingular()
    {
        var cylinder = Cylinder();
        var p = Vector(2, 0, 5);
        Assert.Equal(AlgorithmStatus.Success, AnalyticImplicitEvaluation.Evaluate(in cylinder, in p, 2, out var jet));
        Assert.Equal(3.0, jet.Value, 12);
        Assert.Equal(4.0, jet.Gradient.X, 12);
        CheckImplicitHessian(q => { AnalyticImplicitEvaluation.Evaluate(in cylinder, in q, 2, out var j); return j; }, in p, 1e-6);

        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in cylinder, in p, 1, 2, out var distance));
        Assert.Equal(1.0, distance.Distance, 12);
        Assert.Equal(1.0, distance.Gradient.X, 12);
        Assert.Equal(0.5, distance.Hyy, 12);
        Assert.Equal(0.0, distance.Hzz, 12);
        Assert.Equal(0.0, distance.Hxx, 12);
        CheckDistanceJet(q => { SurfaceDistanceEvaluation.Evaluate(in cylinder, in q, 1, 2, out var d); return d; }, in p, 1e-6);

        var axisPoint = Vector(0, 0, 3);
        Assert.Equal(AlgorithmStatus.Singular, SurfaceDistanceEvaluation.Evaluate(in cylinder, in axisPoint, 1, 2, out _));
        // The implicit jet still evaluates on the axis (gradient zero is honest).
        Assert.Equal(AlgorithmStatus.Success, AnalyticImplicitEvaluation.Evaluate(in cylinder, in axisPoint, 1, out var axisJet));
        Assert.Equal(-1.0, axisJet.Value, 12);
        Assert.Equal(0.0, axisJet.Gradient.X, 12);
    }

    [Fact]
    public void Cone_RejectsWrongNappe_ZeroResidualCandidate()
    {
        var cone = Cone();
        // On the valid sheet: generator R + k·z = 2 at z = 2, point ρ = 2.
        var onSheet = Vector(2, 0, 2);
        Assert.Equal(AlgorithmStatus.Success, AnalyticImplicitEvaluation.Evaluate(in cone, in onSheet, 2, out var jet));
        Assert.InRange(Math.Abs(jet.Value), 0, 1e-12);

        // Mirror sheet: same algebraic zero set at z = −4 (generator −1), ρ = 1.
        var wrongNappe = Vector(1, 0, -4);
        Assert.Equal(AlgorithmStatus.Unsupported, AnalyticImplicitEvaluation.Evaluate(in cone, in wrongNappe, 1, out _));

        // Distance on and off the valid sheet; grade drops off-surface honestly.
        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in cone, in onSheet, 1, 2, out var onDistance));
        Assert.InRange(Math.Abs(onDistance.Distance), 0, 1e-12);
        Assert.Equal(DistanceGrade.Exact, onDistance.Grade);

        var offSheet = Vector(3, 0, 2);
        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in cone, in offSheet, 1, 2, out var offDistance));
        Assert.Equal((3 - 2) / Math.Sqrt(1.25), offDistance.Distance, 12);
        Assert.Equal(1 / Math.Sqrt(1.25), offDistance.Gradient.X, 12);
        Assert.Equal(-0.5 / Math.Sqrt(1.25), offDistance.Gradient.Z, 12);
        // The normal foot x − d·∇d lands exactly on the cone for this
        // linear-in-(ρ, z) distance, so the witness validates as Exact.
        Assert.Equal(DistanceGrade.Exact, offDistance.Grade);
        Assert.Equal(2.2, offDistance.FootPoint.X, 12);
        Assert.Equal(2.4, offDistance.FootPoint.Z, 12);
        CheckDistanceJet(q => { SurfaceDistanceEvaluation.Evaluate(in cone, in q, 1, 2, out var d); return d; }, in offSheet, 1e-6);

        // Far nappe distance is not offered.
        Assert.Equal(AlgorithmStatus.Unsupported, SurfaceDistanceEvaluation.Evaluate(in cone, in wrongNappe, 1, 1, out _));
    }

    [Fact]
    public void Torus_RingImplicitMatches_ButMirrorSheetZeroResidualIsRejected()
    {
        var ring = RingTorus();
        // Point generated at (u, v) = (0.3, 0.7) on the ring torus a=3, b=1.
        var rho = 3 + Math.Cos(0.7);
        var onSurface = Vector(rho * Math.Cos(0.3), rho * Math.Sin(0.3), Math.Sin(0.7));
        Assert.Equal(AlgorithmStatus.Success, AnalyticImplicitEvaluation.Evaluate(in ring, in onSurface, 2, out var jet));
        Assert.InRange(Math.Abs(jet.Value), 0, 1e-9);
        var probe = Vector(5, 0, 0.5);
        CheckImplicitGradient(q => { AnalyticImplicitEvaluation.Evaluate(in ring, in q, 1, out var j); return j; }, in probe, 1e-4);
        CheckImplicitHessian(q => { AnalyticImplicitEvaluation.Evaluate(in ring, in q, 2, out var j); return j; }, in probe, 1e-3);

        // Spindle torus a=1, b=2: the point (−1, 0, 0) sits on the algebraic
        // mirror sheet ((ρ+a)²+z²=b²) with φ = 0 exactly, but it is not on
        // the XT lemon patch ((ρ−a)²+z²=b²). Zero residual must not accept it.
        var spindle = SpindleTorus();
        var mirror = Vector(-1, 0, 0);
        Assert.Equal(AlgorithmStatus.Unsupported, AnalyticImplicitEvaluation.Evaluate(in spindle, in mirror, 1, out _));

        // A genuine lemon-patch point still evaluates with zero residual.
        var lemonRho = 1 + 2 * Math.Cos(0.4);
        var lemonPoint = Vector(lemonRho * Math.Cos(0.9), lemonRho * Math.Sin(0.9), 2 * Math.Sin(0.4));
        Assert.Equal(AlgorithmStatus.Success, AnalyticImplicitEvaluation.Evaluate(in spindle, in lemonPoint, 1, out var lemonJet));
        Assert.InRange(Math.Abs(lemonJet.Value), 0, 1e-9);

        // Distance capability is not claimed for tori in this slice.
        Assert.Equal(AlgorithmStatus.Unsupported, SurfaceDistanceEvaluation.Evaluate(in ring, in onSurface, 1, 1, out _));
    }

    [Fact]
    public void Sense_FlipsDistanceConsistently()
    {
        var sphere = Sphere();
        var p = Vector(2.5, 3.5, 4.5);
        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in sphere, in p, 1, 2, out var positive));
        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in sphere, in p, -1, 2, out var negative));
        Assert.Equal(-positive.Distance, negative.Distance, 12);
        Assert.Equal(-positive.Gradient.X, negative.Gradient.X, 12);
        Assert.Equal(-positive.Hxx, negative.Hxx, 12);
        Assert.Equal(-positive.Hxy, negative.Hxy, 12);

        Assert.Equal(AlgorithmStatus.InvalidInput, SurfaceDistanceEvaluation.Evaluate(in sphere, in p, 0, 2, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput, SurfaceDistanceEvaluation.Evaluate(in sphere, in p, 2, 2, out _));
    }

    [Fact]
    public void Plane_DistanceIsExactEverywhere()
    {
        var plane = Plane();
        var p = Vector(3, -1, 5);
        Assert.Equal(AlgorithmStatus.Success, SurfaceDistanceEvaluation.Evaluate(in plane, in p, 1, 2, out var jet));
        Assert.Equal(3.0, jet.Distance, 12);
        Assert.Equal(1.0, jet.Gradient.Z, 12);
        Assert.Equal(0.0, jet.Hzz, 12);
        Assert.Equal(Vector(3, -1, 2), jet.FootPoint);
        Assert.Equal(DistanceGrade.Exact, jet.Grade);
    }
}
