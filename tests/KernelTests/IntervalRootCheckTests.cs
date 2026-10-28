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
    public void TryCertifyI3_TightBoxAroundCirclePoint_CertifiesUnique()
    {
        // Root of plane∩cylinder at (1,0,0) with e = (0,1,0), p = y = 0.
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(0.9, 1.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0, in box,
            out var status, out var image));
        Assert.Equal(IntervalRootStatus.Unique, status);
        Assert.True(image.XLo <= 1.0 && image.XHi >= 1.0);
        Assert.True(image.YLo <= 0.0 && image.YHi >= 0.0);
        Assert.True(image.ZLo <= 0.0 && image.ZHi >= 0.0);
    }

    [Fact]
    public void TryCertifyI3_BoxAwayFromCircle_CertifiesEmpty()
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
    public void TryCertifyI3_LargeBox_ReportsUndetermined()
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
    public void TryCertifyI3_PlaneCone_TightBox_CertifiesUnique()
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
    public void TryCertifyI3_PlaneRingTorus_TightBox_CertifiesUnique()
    {
        // Outer equator of a=3,b=1 torus in z=0 is ρ=4; root (4,0,0), e=(0,1,0).
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(3.9, 4.1, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in RingTorus, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.Unique, status);
    }

    [Fact]
    public void TryCertifyI3_PlaneRingTorus_BoxAway_CertifiesEmpty()
    {
        var chord = Vector(0, 1, 0);
        var box = new IntervalBox3(7.0, 7.2, -0.05, 0.05, -0.05, 0.05);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in PlaneZ0, in RingTorus, in chord, planeOffset: 0, in box,
            out var status, out _));
        Assert.Equal(IntervalRootStatus.Empty, status);
    }

    [Fact]
    public void TryCertifyI3_SpindleTorus_ReportsUnsupported()
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
        Assert.True(IntervalRootCheck.TryDisambiguateI3Pair(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0,
            in nearRoot, in farAway, cellRadius: 0.08, out var preferNear));
        Assert.True(preferNear);

        Assert.True(IntervalRootCheck.TryDisambiguateI3Pair(
            in PlaneZ0, in CylinderR1, in chord, planeOffset: 0,
            in farAway, in nearRoot, cellRadius: 0.08, out var preferFarFirst));
        Assert.False(preferFarFirst);
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

    [Fact]
    public void TryCertifyI3_UnnormalizedCylinderAxisAtLargeZ_CertifiesEmptyOnRootlessBox()
    {
        // Regression for review Item 1: support0 is x=0, support1 is cylinder with R=1 and
        // unnormalized axis (0,0,1.0000000001), chord plane y=0. At x=y=0, the residual is
        // ((1 - a²)z)² - 1 = 0, whose positive root is z_* ≈ 4999999586.04818 (413.95 away from
        // 5e9). The box [5e9 - 0.001, 5e9 + 0.001] has no root and must be certified Empty,
        // while a box around z_* is certified Unique.
        var planeX0 = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var a = 1.0000000001;
        var cylinderUnnorm = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, a), Vector(1, 0, 0), 1.0);
        var chordY = Vector(0, 1, 0);

        var rootlessBox = new IntervalBox3(-0.001, 0.001, -0.001, 0.001, 5e9 - 0.001, 5e9 + 0.001);
        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in planeX0, in cylinderUnnorm, in chordY, planeOffset: 0, in rootlessBox,
            out var rootlessStatus, out _));
        Assert.Equal(IntervalRootStatus.Empty, rootlessStatus);

        var oneMinusA2 = Math.Abs(Math.FusedMultiplyAdd(-a, a, 1.0));
        var zStar = 1.0 / oneMinusA2;
        var rootBox = new IntervalBox3(-1e-12, 1e-12, -1e-12, 1e-12, zStar - 0.001, zStar + 0.001);
        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in planeX0, in cylinderUnnorm, in chordY, planeOffset: 0, in rootBox,
            out var rootStatus, out _));
        Assert.Equal(IntervalRootStatus.Unique, rootStatus);
    }

    [Fact]
    public void TryCertifyI3_LargeBaseParameterChordPlane_PreservesOffsetAndSelectsTrueCandidate()
    {
        // Regression for review Item 2: L = 2^53, P0 = (0,0,-L), P1 = (0,0,2), t0 = -L, f0 = 1, t = 1.
        // Plain double (1 - (-2^53)) rounds to 2^53, losing the +1 offset; EFT ChordPlaneOffsetInterval
        // preserves [1, 1] so z = 1 is certified Unique and z = 0 is certified Empty.
        var l = 9007199254740992.0; // 2^53
        var p0 = Vector(0, 0, -l);
        var p1 = Vector(0, 0, 2);
        var dir = Vector(0, 0, 1);
        KernelVector3[] positions = [p0, p1];
        KernelVector3[] tangents = [dir, dir];
        double[] parameters = new double[2];
        double[] scales = new double[1];
        KernelVector3[] chordUnits = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success,
            OriginalChartParameterMap.Build(positions, tangents, -l, 1.0, parameters, scales, chordUnits,
                out var segmentCount, out var buildFailure));
        Assert.Equal(1, segmentCount);
        Assert.Equal(ChartBuildFailure.None, buildFailure);

        var planeX0 = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(1, 0, 0), Vector(0, 1, 0));
        var planeY0 = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 1, 0), Vector(0, 0, 1));
        var chordZ = chordUnits[0];

        IntervalRootCheck.ChordPlaneOffsetInterval(in chordZ, in p0, parameters[0], scales[0], t: 1.0,
            out var offsetLo, out var offsetHi);
        Assert.True(offsetLo <= 1.0 && offsetHi >= 1.0);
        Assert.Equal(1.0, IntervalRootCheck.ChordPlaneOffset(in chordZ, in p0, parameters[0], scales[0], t: 1.0), 15);

        var boxAtZero = new IntervalBox3(-0.001, 0.001, -0.001, 0.001, -0.001, 0.001);
        var boxAtOne = new IntervalBox3(-0.001, 0.001, -0.001, 0.001, 1.0 - 0.001, 1.0 + 0.001);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in planeX0, in planeY0, in chordZ, offsetLo, offsetHi, in boxAtZero,
            out var certZero, out _));
        Assert.Equal(IntervalRootStatus.Empty, certZero);

        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyI3(
            in planeX0, in planeY0, in chordZ, offsetLo, offsetHi, in boxAtOne,
            out var certOne, out _));
        Assert.Equal(IntervalRootStatus.Unique, certOne);

        var candA = Vector(0, 0, 1);
        var candB = Vector(0, 0, 0);
        Assert.True(IntervalRootCheck.TryDisambiguateI3Pair(
            in planeX0, in planeY0, in chordZ, offsetLo, offsetHi,
            in candA, in candB, cellRadius: 0.001, out var preferA));
        Assert.True(preferA);

        // Also verify PlaneResidual does not lose the +1 offset at L = 2^53.
        Assert.Equal(0.0, OriginalChartParameterMap.PlaneResidual(positions, parameters, scales, chordUnits, 0, t: 1.0, in candA), 15);
        Assert.Equal(-1.0, OriginalChartParameterMap.PlaneResidual(positions, parameters, scales, chordUnits, 0, t: 1.0, in candB), 15);
    }
}
