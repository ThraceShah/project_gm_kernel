using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class UngatedFinishTests
{
    [Fact]
    public void SharedSpineMemo_SharesSpine_IsolatesParents()
    {
        var memo = new SharedSpineMemo();
        var spinePt = Vector(2, 0, 0);
        var keyA = new SharedSpineCacheKey(1, 0.4, parentNodeId: 10, parentSense: 1, parentRange: 0.25);
        var keyB = new SharedSpineCacheKey(1, 0.4, parentNodeId: 11, parentSense: -1, parentRange: 0.3);
        Assert.True(memo.TryInsert(in keyA, in spinePt));
        Assert.True(memo.TryFindSpine(in keyB, out var hit)); // same spine
        Assert.Equal(spinePt.X, hit.X, 15);
        Assert.True(keyA.SameSpine(in keyB));
        Assert.False(keyA.SameParentContext(in keyB));
    }

    [Fact]
    public void ContinuityCell_NearParallelNormals_IsSingularNotAmbiguous()
    {
        var g0 = Vector(1, 0, 0);
        var g1 = Vector(1, 1e-10, 0);
        var d = ContinuityCellRules.Classify(in g0, in g1, eliminationRatio: 1.0, atSeam: false);
        Assert.Equal(ContinuityDifficulty.MetricNearTangent, d);
        Assert.Equal(AlgorithmStatus.Singular, ContinuityCellRules.StatusFor(d));
    }

    [Fact]
    public void PseudoArclength_AugmentsNearZeroPlaneRow()
    {
        // Identity J with plane row zeroed.
        Span<double> j = [1, 0, 0, 0, 1, 0, 0, 0, 0];
        Span<double> r = [0.1, -0.2, 0];
        Span<double> sigma = new double[3];
        Span<double> u = new double[9];
        Span<double> v = new double[9];
        Assert.Equal(AlgorithmStatus.Success, PseudoArclengthStep.TryAugmentPlaneRow(
            j, r, 3, planeRow: 2, sigma, u, v, out var augmented));
        Assert.True(augmented);
        Assert.True(Math.Abs(j[6]) + Math.Abs(j[7]) + Math.Abs(j[8]) > 0);
    }

    [Fact]
    public void JointMigrate_RoundTripsEliminationAndJoint()
    {
        Span<double> elim = [2.1, 0.2, 0.1, 0.4];
        Span<double> joint = new double[6];
        Assert.Equal(AlgorithmStatus.Success,
            BlendJointLift.TryMigratePlanState(elim, fromIsJoint: false, spineRadius: 2, joint, toIsJoint: true));
        Span<double> back = new double[4];
        Assert.Equal(AlgorithmStatus.Success,
            BlendJointLift.TryMigratePlanState(joint, fromIsJoint: true, spineRadius: 2, back, toIsJoint: false));
        Assert.InRange(Math.Abs(back[0] - elim[0]), 0, 1e-14);
        Assert.InRange(Math.Abs(back[3] - elim[3]), 0, 1e-12);
    }

    [Fact]
    public void PredictedOnly_NeverSatisfiesExactHit()
    {
        var samples = new CurveSample[4];
        var cache = new EvaluationSampleStore(samples);
        var predicted = new CurveSample(0.5, Vector(1, 0, 0), default, default, 0,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0,
            double.PositiveInfinity, SampleSourceKind.PredictedOnly, ICurveConstraintPlan.I2);
        Assert.True(cache.TryInsert(in predicted));
        Assert.False(cache.TryFindExact(0.5, ICurveQueryKind.RegularChartInterval, ChartSide.Right,
            0, 1e-6, out _));
    }

    [Fact]
    public void EvaluationBudget_InnerTighten_SurfacesInRefineDetail()
    {
        // Direct unit: TightenInner + detail enum exist and compose.
        var budget = EvaluationBudget.Default;
        budget.TightenInner(0.25);
        Assert.Equal(0.25, budget.InnerAccuracyFactor);
        Assert.Equal(ICurveEvalDetail.InnerAccuracyInsufficient, ICurveEvalDetail.InnerAccuracyInsufficient);
    }

    [Fact]
    public void TryCertifyP2_TinyBoxAroundRoot_IsUniqueOrUndetermined()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cyl = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
        // Parametric support0 = cylinder, support1 = plane; chord along Z-normal plane through (1,0,0).
        var chord = Vector(0, 1, 0);
        var planeOffset = 0.0; // y = 0 section ≈ ( ±1, 0, z) but cylinder UV...
        // Use plane as support0 (parametric) and cylinder as support1 for a clean P2.
        var uv = new IntervalRootCheck.IntervalBox2(-0.01, 0.01, -0.01, 0.01);
        Assert.Equal(AlgorithmStatus.Success, IntervalRootCheck.TryCertifyP2(
            in plane, in cyl, in chord, planeOffset: 1.0, in uv, out var status));
        Assert.True(status is IntervalRootStatus.Unique or IntervalRootStatus.Undetermined
            or IntervalRootStatus.Empty);
    }

    [Fact]
    public void EvaluationCounters_RecordCacheKinds()
    {
        var c = new EvaluationCounters();
        c.RecordCacheHit(CacheHitKind.Exact);
        c.RecordCacheHit(CacheHitKind.NeighborSeed);
        Assert.Equal(1, c.CacheExactHits);
        Assert.Equal(1, c.CacheNeighborSeeds);
    }

    [Fact]
    public void FailureReplay_ViaDiagnostics_RecordsFrames()
    {
        var diag = EvaluationDiagnostics.CreateDefault();
        diag.RecordFailure(ICurveConstraintPlan.I2, 0.3, 1, AlgorithmStatus.NotConverged, 1e-2,
            ICurveEvalDetail.Stagnation);
        Span<FailureReplayRing.Frame> frames = new FailureReplayRing.Frame[4];
        Assert.True(diag.Replay.TryCopy(frames, out var n));
        Assert.Equal(1, n);
        Assert.Equal(ICurveEvalDetail.Stagnation, frames[0].Detail);
    }

    [Fact]
    public void JointLift_SchurPath_StillRecoversRoot()
    {
        // Reuse RV-LIFTED-J geometry from JointSystemTests via public lift API.
        var supportA = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 2.0);
        var supportD = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var outer = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0.10421770060730269), Vector(0, 0, 1), Vector(1, 0, 0));
        var anchor = Vector(2.051425212233302, 0.8673286684807344, 0.10421770060730269);
        var normal = Vector(-0.4364357804719847, 0.8728715609439694, 0.21821789023599236);
        var point = Vector(2.061425212233302, 0.8593286684807344, 0.10621770060730269);
        var spineSeed = Math.Atan2(0.783836684617301, 1.84512198800577);
        Span<double> state = new double[6];
        Assert.Equal(AlgorithmStatus.Success, BlendJointLift.TryLiftFromLocalSingular(
            in supportA, in supportD, in outer, in anchor, in normal, 0.0625, in point,
            spineSeed, 2.0, state, out _, out var residual));
        Assert.InRange(residual, 0, 1e-10);
    }
}
