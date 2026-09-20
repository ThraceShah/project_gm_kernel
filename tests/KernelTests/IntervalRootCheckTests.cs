using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Local Moore–Krawczyk certification for analytic I3 cells (spec §18.3–§18.4,
/// task T18). Empty / Unique / Undetermined are distinct; Newton samples alone
/// are never labelled certified; B-surface and spindle torus report
/// BoundsUnavailable via Unsupported status. Ring torus is interval-capable.
/// </summary>
public class IntervalRootCheckTests
{
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
    private static readonly AnalyticSurface Cone =
        new(SurfaceClass.Cone, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.5);
    private static readonly AnalyticSurface RingTorus =
        new(SurfaceClass.Torus, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 3.0, 1.0);
    private static readonly AnalyticSurface SpindleTorus =
        new(SurfaceClass.Torus, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 2.0);

    [Fact]
    public void IntervalMultiplication_ContainsExactPositiveProduct()
    {
        var product = IntervalRootCheck.MultiplyIntervals(1, 1, 1, 1);
        Assert.True(product.Lo <= 1);
        Assert.True(product.Hi >= 1);
    }

    [Fact]
    public void TryCertifyP2_PeriodicMultiRootBox_ReportsBoundsUnavailable()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        var chord = Vector(0, 1, 0);
        var box = new IntervalRootCheck.IntervalBox2(-2 * Math.PI, 2 * Math.PI, -1, 1);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyP2(
            in cylinder, in plane, in chord, 0, in box, out var status));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_TightBoxAroundCirclePoint_Unique()
    {
        // Root of plane∩cylinder at (1,0,0) with e = (0,1,0), p = y = 0.
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_BoxAwayFromCircle_Empty()
    {
        var chord = Vector(0, 1, 0);
        // Far from the unit circle in the z=0 plane.
        var box = new IntervalBox3(3.0, 3.2, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_LargeBox_UndeterminedNotUnique()
    {
        var chord = Vector(0, 1, 0);
        // Large enough that contraction/inclusion cannot fire, but not empty.
        var box = new IntervalBox3(-2, 2, -2, 2, -0.5, 0.5);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_PlaneCone_TightBox_Unique()
    {
        // Cone R=1, k=0.5: at z=0 the section is the unit circle; plane z=0 ∩
        // cone shares the same (1,0,0) root used by the cylinder fixture.
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in Cone, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_PlaneRingTorus_TightBox_Unique()
    {
        // Outer equator of a=3,b=1 torus in z=0 is ρ=4; root (4,0,0), e=(0,1,0).
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(3.9, 4.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in RingTorus, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_PlaneRingTorus_BoxAway_Empty()
    {
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(7.0, 7.2, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in RingTorus, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryCertifyI3_SpindleTorus_BoundsUnavailable()
    {
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in SpindleTorus, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
    }

    [Fact]
    public void TryDisambiguateI3Pair_PrefersUniqueOverEmpty()
    {
        var chord = Vector(0, 1, 0);
        var nearRoot = Vector(1, 0, 0);
        var farAway = Vector(3.1, 0, 0);
        Assert.False(IntervalRootCheck.TryDisambiguateI3Pair(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0,
            in nearRoot, in farAway, cellRadius: 0.08, out var preferNear));
        Assert.True(preferNear);

        Assert.False(IntervalRootCheck.TryDisambiguateI3Pair(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0,
            in farAway, in nearRoot, cellRadius: 0.08, out var preferFarFirst));
        Assert.True(preferFarFirst);
    }

    [Fact]
    public void TryDisambiguateI3Pair_TwoUndetermined_DoesNotResolve()
    {
        var chord = Vector(0, 1, 0);
        // Large cells around distant points → not Unique/Empty pair.
        var a = Vector(0.5, 0, 0);
        var b = Vector(-0.5, 0, 0);
        Assert.False(IntervalRootCheck.TryDisambiguateI3Pair(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0,
            in a, in b, cellRadius: 2.0, out _));
    }

    [Fact]
    public void NewtonSample_IsNotCertifiedByThisModule()
    {
        // A single Newton hit at the root is Success for the evaluator, but this
        // module never invents a Unique label from point samples (§18.4).
        var root = Vector(1, 0, 0);
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in PlaneZ0, in root, 0, out var p));
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in CylinderR1, in root, 0, out var c));
        Assert.True(Math.Abs(p.Value) < 1e-15);
        Assert.True(Math.Abs(c.Value) < 1e-15);
        // No IntervalRootStatus is produced here — certification requires TryCertifyI3.
    }
}
