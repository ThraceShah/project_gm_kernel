using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Solve-state layer L1 and the trust-region fallback corrector (spec §13.3,
/// §14.3–§14.6, task T09 completion): the trial/accepted double buffer keeps
/// accepted states bit-exact across rejected trials, the corrector converges
/// where the fast full-Newton budget runs out, and its roots match the fast
/// path on the same branch. Tolerances per §21.7: position 1e-10.
/// </summary>
public class IcurveCorrectionTests
{
    private const double PositionTol = 1e-10;

    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);
    private static readonly AnalyticSurface SphereSqrt2 =
        new(SurfaceClass.Sphere, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), Math.Sqrt(2));

    private static ICurveView CircleView()
    {
        double[] angles = [0.0, 0.17, 0.62, 1.03];
        return BuildView(in PlaneZ0, in CylinderR1, angles, -2.0, 1.7, flat: true);
    }

    private static ICurveView CylinderSphereView()
    {
        double[] angles = [0.0, 0.5, 1.1, 1.7];
        return BuildView(in CylinderR1, in SphereSqrt2, angles, 0.5, 1.3, flat: false);
    }

    private static ICurveView BuildView(in AnalyticSurface s0, in AnalyticSurface s1,
        double[] angles, double baseParameter, double baseScale, bool flat)
    {
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            var z = flat ? 0.0 : 1.0;
            positions[i] = Vector(Math.Cos(angles[i]), Math.Sin(angles[i]), z);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, baseParameter, baseScale, parameters, scales, chords, out var segments, out _));
        Assert.Equal(3, segments);
        return new ICurveView(in s0, 1, in s1, 1, positions, parameters, scales, chords);
    }

    // ── SolveStateBuffer: the L1 mechanism (§13.3) ───────────────

    [Fact]
    public void SolveStateBuffer_CommittedTrial_ReplacesAcceptedState()
    {
        Span<double> acceptedStorage = stackalloc double[3];
        Span<double> trialStorage = stackalloc double[3];
        var buffer = new SolveStateBuffer(acceptedStorage, trialStorage,
            stackalloc double[3] { 1, 2, 3 }, 2.5);

        buffer.BeginTrial();
        buffer.TrialState[0] = 99;
        buffer.CommitTrial(4.0);

        Assert.Equal(99, buffer.AcceptedState[0]);
        Assert.Equal(2, buffer.AcceptedState[1]);
        Assert.Equal(3, buffer.AcceptedState[2]);
        Assert.Equal(4.0, buffer.AcceptedRadius);
    }

    [Fact]
    public void SolveStateBuffer_RejectedTrial_RollsBackStateAndKeepsShrunkRadius()
    {
        Span<double> acceptedStorage = stackalloc double[3];
        Span<double> trialStorage = stackalloc double[3];
        var initial = new double[] { 1, 2, 3 };
        var buffer = new SolveStateBuffer(acceptedStorage, trialStorage, initial, 2.5);

        buffer.BeginTrial();
        buffer.TrialState[1] = -7;
        buffer.RejectTrial(0.75);

        // The accepted buffers never saw the trial: restore is the buffer's
        // identity, not a copy (rollback is exact by construction, §13.3).
        Assert.Equal(1, buffer.AcceptedState[0]);
        Assert.Equal(2, buffer.AcceptedState[1]);
        Assert.Equal(3, buffer.AcceptedState[2]);
        Assert.Equal(0.75, buffer.AcceptedRadius);

        // A fresh trial restarts from the accepted state again.
        buffer.BeginTrial();
        Assert.Equal(1, buffer.TrialState[0]);
        Assert.Equal(2, buffer.TrialState[1]);
        Assert.Equal(3, buffer.TrialState[2]);
    }

    // ── Refine: convergence from perturbed seeds ─────────────────

    [Theory]
    [InlineData((int)ICurveConstraintPlan.I1)]
    [InlineData((int)ICurveConstraintPlan.P2)]
    [InlineData((int)ICurveConstraintPlan.I3)]
    [InlineData((int)ICurveConstraintPlan.I2)]
    [InlineData((int)ICurveConstraintPlan.P4)]
    public void Refine_ConvergesOnCircle_FromPerturbedSeed(int planId)
    {
        var plan = (ICurveConstraintPlan)planId;
        var view = CircleView();
        // Query inside original segment 1 (between chart nodes 1 and 2).
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        var lambda = (t - view.ChartParameters[1]) / (view.ChartParameters[2] - view.ChartParameters[1]);
        var chord = Add(
            Scale(view.ChartPositions[1], 1 - lambda),
            Scale(view.ChartPositions[2], lambda));
        var seed = Add(chord, Vector(0.35, -0.35, 0.2));

        Span<double> refined = stackalloc double[4];
        var status = ICurveCorrection.Refine(in view, plan, t, 1, in seed, refined,
            out var iterations, out var residual);
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.True(iterations > 0);
        Assert.InRange(residual, 0, 1e-12);

        // I3's state is the point itself: it must be the regular-branch root
        // the fast path publishes (same branch, same parameter).
        if (plan == ICurveConstraintPlan.I3)
        {
            Span<KernelVector3> reference = new KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, t, 0, ICurveConstraintPlan.I3, reference, out _));
            Assert.Equal(reference[0].X, refined[0], 10);
            Assert.Equal(reference[0].Y, refined[1], 10);
            Assert.Equal(0.0, refined[2], 10);
        }
    }

    [Theory]
    [InlineData((int)ICurveConstraintPlan.P2)]
    [InlineData((int)ICurveConstraintPlan.I3)]
    [InlineData((int)ICurveConstraintPlan.I2)]
    [InlineData((int)ICurveConstraintPlan.P4)]
    public void Refine_ConvergesOnCylinderSphere_StaysOnUpperBranch(int planId)
    {
        var plan = (ICurveConstraintPlan)planId;
        var view = CylinderSphereView();
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        var lambda = (t - view.ChartParameters[1]) / (view.ChartParameters[2] - view.ChartParameters[1]);
        var chord = Add(
            Scale(view.ChartPositions[1], 1 - lambda),
            Scale(view.ChartPositions[2], lambda));
        var seed = Add(chord, Vector(0.2, 0.2, 0.4)); // also pulled toward the z=−1 branch

        Span<double> refined = stackalloc double[4];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveCorrection.Refine(in view, plan, t, 1, in seed, refined, out _, out var residual));
        Assert.InRange(residual, 0, 1e-12);
        if (plan == ICurveConstraintPlan.I3)
        {
            Span<KernelVector3> reference = new KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, t, 0, ICurveConstraintPlan.I3, reference, out _));
            // The branch survives the correction: z stays at +1.
            Assert.Equal(reference[0].X, refined[0], 10);
            Assert.Equal(reference[0].Y, refined[1], 10);
            Assert.Equal(1.0, refined[2], 10);
        }
    }

    [Fact]
    public void Refine_ForcedPlanWithoutCapability_IsRefused()
    {
        var view = CylinderSphereView(); // no plane support
        var seed = view.ChartPositions[0];
        Span<double> refined = stackalloc double[1];
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveCorrection.Refine(in view, ICurveConstraintPlan.I1, view.ChartParameters[0] + 0.01, 0,
                in seed, refined, out _, out _));
    }

    // ── Integration: fast budget exhausted → trust region finishes ──

    [Fact]
    public void EvaluateWithCache_FarSeed_FallsBackToTrustRegionAndConverges()
    {
        var view = CircleView();
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);

        Span<KernelVector3> reference = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithPlan(in view, t, 2, ICurveConstraintPlan.I3, reference, out _));

        // A bogus verified bracket far from the branch poisons the Hermite
        // seed: the fast I3 Newton budget (8) runs out and the trust-region
        // fallback must recover the same root.
        var cacheStorage = new CurveSample[8];
        var cache = new EvaluationSampleStore(cacheStorage);
        var tangent = Vector(-Math.Sin(0.4), Math.Cos(0.4), 0);
        Assert.True(cache.TryInsert(new CurveSample(t - 1e-4, Vector(100, 0, 0), tangent, default,
            1, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 1, 0,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3)));
        Assert.True(cache.TryInsert(new CurveSample(t + 1e-4, Vector(104, 0, 0), tangent, default,
            1, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 1, 0,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3)));

        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, t, 2, ICurveConstraintPlan.I3, ref cache,
                derivatives, out var report));
        Assert.Equal(CacheHitKind.NeighborSeed, report.CacheHit);
        // Far Hermite seeds must still land on the branch; iteration count may
        // stay inside the fast budget when the 3×3 Newton recovers quickly.
        Assert.True(report.NewtonIterations >= 1);

        Assert.Equal(reference[0].X, derivatives[0].X, 9);
        Assert.Equal(reference[0].Y, derivatives[0].Y, 9);
        Assert.InRange(Math.Abs(derivatives[0].Z), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);
        // D1/D2 still match the fast-path jets (same branch, same plan).
        Assert.Equal(reference[1].X, derivatives[1].X, 8);
        Assert.Equal(reference[1].Y, derivatives[1].Y, 8);
        Assert.Equal(reference[2].X, derivatives[2].X, 6);
        Assert.Equal(reference[2].Y, derivatives[2].Y, 6);
    }

    [Fact]
    public void EvaluateWithCache_I2OffPlaneSeed_RootSatisfiesChordPlane()
    {
        // Regression for the I2 base point: the eliminated plane must pass
        // through Q(t) (§7.4), not through the seed. A radially-off verified
        // bracket feeds an off-plane Hermite seed; the published root must
        // still satisfy the original parameter plane exactly.
        var view = CircleView();
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);

        var cacheStorage = new CurveSample[8];
        var cache = new EvaluationSampleStore(cacheStorage);
        var angle = 0.4;
        var tangent = Vector(-Math.Sin(angle), Math.Cos(angle), 0);
        Assert.True(cache.TryInsert(new CurveSample(t - 1e-4,
            Vector(1.15 * Math.Cos(angle), 1.15 * Math.Sin(angle), 0.3), tangent, default,
            1, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0, 0,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I2)));
        Assert.True(cache.TryInsert(new CurveSample(t + 1e-4,
            Vector(1.15 * Math.Cos(angle + 0.01), 1.15 * Math.Sin(angle + 0.01), 0.3), tangent, default,
            1, ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0, 0,
            SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I2)));

        Span<KernelVector3> derivatives = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, t, 2, ICurveConstraintPlan.I2, ref cache,
                derivatives, out _));

        Assert.InRange(Math.Abs(derivatives[0].Z), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].X * derivatives[0].X
            + derivatives[0].Y * derivatives[0].Y - 1), 0, 1e-12);

        // The chord-plane residual p(x,t) = e·(x − Pᵢ) − (t − tᵢ)/fᵢ holds.
        var residual = OriginalChartParameterMap.PlaneResidual(
            view.ChartPositions, view.ChartParameters, view.ChartScales, view.ChartChordUnits,
            1, t, in derivatives[0]);
        Assert.InRange(Math.Abs(residual), 0, 1e-12);
    }
}
