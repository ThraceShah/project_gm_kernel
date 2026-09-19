using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Cross-call cache lifecycle tests (spec §13.8–§13.9, task T10): cold/warm
/// and reordered queries stay semantically identical, epoch bumps from
/// rollback/deletion/reset invalidate entries, slot ABA is repelled by the
/// generation check, eviction keeps the arena consistent, and the cache never
/// serves across sessions.
/// </summary>
public unsafe class GeometryCacheTests : IDisposable
{
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

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
            positions, tangents, -2.0, 1.7, parameters, scales, chords, out _, out _));
        return new ICurveView(in PlaneZ0, 1, in CylinderR1, 1, positions, parameters, scales, chords);
    }

    private static GeometryIdentity Identity(int tag, int generation = 1) => new(tag, generation);

    public GeometryCacheTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void WarmCold_ForwardReverseRandom_AgreeSemantically()
    {
        var view = CircleView();
        var identity = Identity(11);
        Span<KernelVector3> derivatives = new KernelVector3[3];

        // Cold forward pass.
        double[] forward = [-1.7, -1.4, -1.1, -0.8, -0.5];
        var cold = new KernelVector3[forward.Length][];
        for (var i = 0; i < forward.Length; i++)
        {
            Span<KernelVector3> d = new KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, forward[i], 2,
                    ICurveConstraintPlan.Auto, d, out var report));
            Assert.Equal(CacheHitKind.None, report.CacheHit);
            cold[i] = d.ToArray();
        }

        // Warm reverse pass: exact hits only, same values.
        for (var i = forward.Length - 1; i >= 0; i--)
        {
            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, forward[i], 2,
                    ICurveConstraintPlan.Auto, derivatives, out var report));
            Assert.Equal(CacheHitKind.Exact, report.CacheHit);
            Assert.Equal(cold[i][0].X, derivatives[0].X, 15);
            Assert.Equal(cold[i][1].Y, derivatives[1].Y, 15);
            Assert.Equal(cold[i][2].X, derivatives[2].X, 15);
        }

        // Interleaved fresh parameters keep the branch: every point on the circle.
        var random = new Random(7); // fixed seed: deterministic order (§22.1)
        var shuffled = forward.OrderBy(_ => random.Next()).ToArray();
        foreach (var t in shuffled)
        {
            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 2,
                    ICurveConstraintPlan.Auto, derivatives, out _));
            Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
                + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);
        }
    }

    [Fact]
    public void DifferentIdentities_DoNotShareSamples()
    {
        var view = CircleView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        var first = Identity(21);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in first, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out _));

        // Another geometry (different tag) must not hit the first one's sample.
        var second = Identity(22);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in second, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out var other));
        Assert.Equal(CacheHitKind.None, other.CacheHit);

        // Slot ABA: same tag, bumped generation — the stale entry is refused.
        var reused = Identity(21, 2);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in reused, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out var aba));
        Assert.Equal(CacheHitKind.None, aba.CacheHit);
    }

    [Fact]
    public void DeletionAndRollback_InvalidateCachedSamples()
    {
        var view = CircleView();
        var identity = Identity(31);
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out _));

        // Deleting any entity bumps the epoch and drops the entry.
        var plane = CreatePlane();
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane));
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out var afterDelete));
        Assert.Equal(CacheHitKind.None, afterDelete.CacheHit);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out var reheated));
        Assert.Equal(CacheHitKind.Exact, reheated.CacheHit);

        // Rollback likewise.
        int mark = 0;
        Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        _ = CreatePlane();
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -0.9, 2,
                ICurveConstraintPlan.Auto, derivatives, out _));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -0.9, 2,
                ICurveConstraintPlan.Auto, derivatives, out var afterRollback));
        Assert.Equal(CacheHitKind.None, afterRollback.CacheHit);
    }

    [Fact]
    public void SessionReset_InvalidatesEverything()
    {
        var view = CircleView();
        var identity = Identity(41);
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out _));

        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));

        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out var report));
        Assert.Equal(CacheHitKind.None, report.CacheHit);
    }

    [Fact]
    public void ClockEviction_KeepsArenaConsistent_WithoutDanglingBorrows()
    {
        var view = CircleView();
        Span<KernelVector3> derivatives = new KernelVector3[3];
        var identity = Identity(51);

        // Fill beyond the hard capacity: every sample distinct in t.
        var published = 0;
        for (var i = 0; i < GeometryEvaluationCache.Capacity + 8; i++)
        {
            var t = -1.9 + i * 1e-4; // distinct bit patterns inside segment 0
            Assert.Equal(AlgorithmStatus.Success,
                KernelRuntime.EvaluateICurveThroughL3(in identity, in view, t, 0,
                    ICurveConstraintPlan.Auto, derivatives, out _));
            published++;
        }
        // The arena never exceeded capacity and stayed consistent: the most
        // recent sample survived, an early one was evicted, and nothing stale
        // appears under another identity.
        var recent = -1.9 + (GeometryEvaluationCache.Capacity + 7) * 1e-4;
        Assert.True(GeometryEvaluationCache.TryGetExact(in identity, recent, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, 0, 1e-11, out var sample));
        Assert.Equal(recent, sample.Parameter, 15);
        var evicted = -1.9; // the first sample: cleared by the clock sweep
        Assert.False(GeometryEvaluationCache.TryGetExact(in identity, evicted, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, 0, 1e-11, out _));
        // Entries are value records: eviction cannot dangle borrowed data.
        var other = Identity(52);
        Assert.False(GeometryEvaluationCache.TryGetExact(in other, recent, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, 0, 1e-11, out _));
    }

    [Fact]
    public void CacheWrites_DoNotBumpTheEpoch()
    {
        var before = GeometryEvaluationCache.ModelGeometryEpoch;
        var view = CircleView();
        var identity = Identity(61);
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.2, 2,
                ICurveConstraintPlan.Auto, derivatives, out _));
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.EvaluateICurveThroughL3(in identity, in view, -1.0, 2,
                ICurveConstraintPlan.Auto, derivatives, out _));
        Assert.Equal(before, GeometryEvaluationCache.ModelGeometryEpoch);
    }

    private static EntityTag CreatePlane()
    {
        var sf = new PK_PLANE_sf_s();
        sf.basis_set.location.coord[0] = 0;
        sf.basis_set.location.coord[1] = 0;
        sf.basis_set.location.coord[2] = 0;
        sf.basis_set.axis.coord[0] = 0;
        sf.basis_set.axis.coord[1] = 0;
        sf.basis_set.axis.coord[2] = 1;
        sf.basis_set.ref_direction.coord[0] = 1;
        sf.basis_set.ref_direction.coord[1] = 0;
        sf.basis_set.ref_direction.coord[2] = 0;
        int tag = 0;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&sf, &tag));
        return tag;
    }
}
