using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Local Moore–Krawczyk certification for analytic I3 cells (spec §18.3–§18.4,
/// task T18). Empty / Unique / Undetermined are distinct; Newton samples alone
/// are never labelled certified; unsupported process surfaces (torus / B-surface)
/// report BoundsUnavailable via Unsupported status. Cone is interval-capable.
/// </summary>
public class IntervalRootCheckTests
{
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
    private static readonly AnalyticSurface Cone =
        new(SurfaceClass.Cone, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.5);
    private static readonly AnalyticSurface Torus =
        new(SurfaceClass.Torus, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 3.0, 1.0);

    [Fact]
    public void TryCertifyI3_TightBoxAroundCirclePoint_Unique()
    {
        // Root of plane∩cylinder at (1,0,0) with e = (0,1,0), p = y = 0.
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.Unique, status);
    }

    [Fact]
    public void TryCertifyI3_BoxAwayFromCircle_Empty()
    {
        var chord = Vector(0, 1, 0);
        // Far from the unit circle in the z=0 plane.
        var box = new IntervalBox3(3.0, 3.2, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.Empty, status);
    }

    [Fact]
    public void TryCertifyI3_LargeBox_UndeterminedNotUnique()
    {
        var chord = Vector(0, 1, 0);
        // Large enough that contraction/inclusion cannot fire, but not empty.
        var box = new IntervalBox3(-2, 2, -2, 2, -0.5, 0.5);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.Undetermined, status);
    }

    [Fact]
    public void TryCertifyI3_PlaneCone_TightBox_Unique()
    {
        // Cone R=1, k=0.5: at z=0 the section is the unit circle; plane z=0 ∩
        // cone shares the same (1,0,0) root used by the cylinder fixture.
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in Cone, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.Unique, status);
    }

    [Fact]
    public void TryCertifyI3_UnsupportedTorus_BoundsUnavailable()
    {
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Unsupported, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in Torus, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.BoundsUnavailable, status);
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
