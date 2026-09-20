using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Operation-level cache tests (spec §13.3–§13.4, task T09): cold and warm
/// passes agree, exact keys never collide across parameters, precision gates
/// keep low-quality samples from satisfying tighter requests, predicted-only
/// samples never publish, and failed requests store nothing.
/// </summary>
public class IcurveCacheTests
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

    [Fact]
    public void WarmPass_MatchesColdPass_AndReportsHits()
    {
        var view = CircleView();
        double[] queries = [-1.7, -1.2, -0.8, -0.5];
        var cold = new (KernelVector3 p, KernelVector3 d1, KernelVector3 d2, ICurveEvalReport report)[queries.Length];
        var samples = new CurveSample[32];
        var cache = new EvaluationSampleStore(samples);

        for (var i = 0; i < queries.Length; i++)
        {
            Span<KernelVector3> derivatives = new KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithCache(in view, queries[i], 2, ICurveConstraintPlan.Auto,
                    ref cache, derivatives, out var report));
            cold[i] = (derivatives[0], derivatives[1], derivatives[2], report);
            Assert.Equal(CacheHitKind.None, report.CacheHit);
        }
        Assert.Equal(queries.Length, cache.Count);

        for (var i = 0; i < queries.Length; i++)
        {
            Span<KernelVector3> derivatives = new KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithCache(in view, queries[i], 2, ICurveConstraintPlan.Auto,
                    ref cache, derivatives, out var report));
            Assert.Equal(CacheHitKind.Exact, report.CacheHit);
            Assert.Equal(cold[i].p.X, derivatives[0].X, 15);
            Assert.Equal(cold[i].p.Y, derivatives[0].Y, 15);
            Assert.Equal(cold[i].d1.X, derivatives[1].X, 15);
            Assert.Equal(cold[i].d2.X, derivatives[2].X, 15);
        }
    }

    [Fact]
    public void ExactKeys_NeverCollideAcrossParameters()
    {
        var view = CircleView();
        var samples = new CurveSample[8];
        var cache = new EvaluationSampleStore(samples);
        Span<KernelVector3> derivatives = new KernelVector3[3];

        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(in view, -1.2, 2,
            ICurveConstraintPlan.Auto, ref cache, derivatives, out _));
        // A different parameter must not hit the stored sample: with a second
        // verified sample above it, −1.1 brackets into a Hermite seed.
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(in view, -1.0, 2,
            ICurveConstraintPlan.Auto, ref cache, derivatives, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(in view, -1.1, 2,
            ICurveConstraintPlan.Auto, ref cache, derivatives, out var second));
        Assert.Equal(CacheHitKind.NeighborSeed, second.CacheHit);
        Assert.Equal(3, cache.Count);

        // The near-miss sample's position must not leak into the second query.
        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);
    }

    [Fact]
    public void PrecisionGate_LowQualitySampleDoesNotSatisfyTighterRequest()
    {
        var view = CircleView();
        var samples = new CurveSample[8];
        var cache = new EvaluationSampleStore(samples);
        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.LocateSegment(
            view.ChartParameters, -1.15, ChartSide.Right, out var segment));

        // Loose legacy samples around the query parameter: they bracket the
        // request (so the corrector runs from a seed) but are too coarse to
        // satisfy an exact hit.
        var loose = new CurveSample(-1.2, Vector(0.9, 0.4, 0), default, default, 2,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, segment, 1e-6,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        var looseUpper = new CurveSample(-1.1, Vector(0.94, 0.34, 0), default, default, 2,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, segment, 1e-6,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        Assert.True(cache.TryInsert(in loose));
        Assert.True(cache.TryInsert(in looseUpper));

        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(in view, -1.15, 2,
            ICurveConstraintPlan.Auto, ref cache, derivatives, out var report));
        Assert.Equal(CacheHitKind.NeighborSeed, report.CacheHit);
        // The published result satisfies the definition, not the loose sample.
        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);

        // A request within the loose sample's own bound hits it directly.
        var wide = new EvaluationSampleStore(new CurveSample[8]);
        Assert.True(wide.TryInsert(in loose));
        Assert.True(wide.TryFindExact(-1.2, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 2, 1e-5, out _));
        Assert.False(wide.TryFindExact(-1.2, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 2, 1e-9, out _));
    }

    [Fact]
    public void PredictedOnlySamples_NeverSatisfyExactHits()
    {
        var prediction = new CurveSample(-1.2, Vector(0.9, 0.4, 0), default, default, 2,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, -1, 1e-15,
            SampleSourceKind.PredictedOnly, ICurveConstraintPlan.I3);
        var cache = new EvaluationSampleStore(new CurveSample[8]);
        Assert.True(cache.TryInsert(in prediction));

        Assert.False(cache.TryFindExact(-1.2, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 2, 1, out _));
        // It neither helps nor blocks bracketing between verified samples.
        var lower = new CurveSample(-1.3, Vector(0.86, 0.51, 0), default, default, 2,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, -1, 1e-15,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        var upper = new CurveSample(-1.1, Vector(0.94, 0.34, 0), default, default, 2,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, -1, 1e-15,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        Assert.True(cache.TryInsert(in lower));
        Assert.True(cache.TryInsert(in upper));
        Assert.True(cache.TryFindBracket(-1.15, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, -1, out var foundLower, out var foundUpper));
        Assert.Equal(-1.3, foundLower.Parameter, 12);
        Assert.Equal(-1.1, foundUpper.Parameter, 12);
    }

    [Fact]
    public void FailedRequest_StoresNothing()
    {
        var view = CircleView();
        var samples = new CurveSample[8];
        var cache = new EvaluationSampleStore(samples);
        Span<KernelVector3> derivatives = new KernelVector3[3];

        Assert.Equal(AlgorithmStatus.InvalidInput, ICurveEvaluation.EvaluateWithCache(in view, -5.0, 2,
            ICurveConstraintPlan.Auto, ref cache, derivatives, out _));
        Assert.Equal(0, cache.Count);
        Assert.Equal(AlgorithmStatus.InvalidInput, ICurveEvaluation.EvaluateWithCache(in view, double.NaN, 2,
            ICurveConstraintPlan.Auto, ref cache, derivatives, out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void AnchorPrecedes_OverLaterCorrections_AtTheSameKey()
    {
        // §13.4: the original chart node's D0 rule — a corrected sample must
        // not overwrite the anchor entry for the same key.
        var anchor = new CurveSample(-2.0, Vector(1, 0, 0), default, default, 0,
            ICurveQueryKind.ChartPoint, ChartSide.Right, 0, 0,
            SampleSourceKind.ImportedChartAnchor, ICurveConstraintPlan.Auto);
        var cache = new EvaluationSampleStore(new CurveSample[8]);
        Assert.True(cache.TryInsert(in anchor));

        var corrected = new CurveSample(-2.0, Vector(0.999, 0.001, 0), default, default, 2,
            ICurveQueryKind.ChartPoint, ChartSide.Right, 0, 1e-15,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        Assert.True(cache.TryInsert(in corrected));
        Assert.Equal(1, cache.Count);

        cache.TryFindExact(-2.0, ICurveQueryKind.ChartPoint, ChartSide.Right, 0, 1e-15, out var hit);
        Assert.Equal(SampleSourceKind.ImportedChartAnchor, hit.Source);
        Assert.Equal(1, hit.Position.X, 15);
    }

    [Fact]
    public void StorageExhaustion_LeavesStoreUnchanged()
    {
        var cache = new EvaluationSampleStore(new CurveSample[1]);
        var first = new CurveSample(-2.0, Vector(1, 0, 0), default, default, 0,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, -1, 1e-14,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        var second = new CurveSample(-1.0, Vector(0.5, 0.86, 0), default, default, 0,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, -1, 1e-14,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3);
        Assert.True(cache.TryInsert(in first));
        Assert.False(cache.TryInsert(in second));
        Assert.Equal(1, cache.Count);
    }
}
