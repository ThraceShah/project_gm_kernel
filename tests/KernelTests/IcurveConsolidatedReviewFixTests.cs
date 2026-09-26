using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;
using ProjectGmKernel.Xt;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Regression tests for the consolidated-review fixes (2026-09-26):
/// B1 — L3 cache short-lock synchronization under concurrent readers.
/// N1 — scaled-space dogleg step mapped back through D_u for state updates.
/// N5 — XT cone axis/ref byte parity with the live Parasolid probe
///       (temp_docs/icurve-evaluation/probe/cone-axis-probe-output.txt).
/// N9 — §9.4 spun two-equation elimination replaces the broken meridian dot.
/// N2 — cache sample quality in certified length units, scale-aware bound.
/// N13 — dimensionless joint acceptance rejects the small-radius false success.
/// </summary>
public unsafe class IcurveConsolidatedReviewFixTests : IDisposable
{
    private static readonly AnalyticSurface PlaneZ0 =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface CylinderR1 =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0);

    public IcurveConsolidatedReviewFixTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    private static GeometryIdentity Identity(int tag, int generation = 1) => new(tag, generation);

    private static ICurveView CircleView(double radius, double baseScale)
    {
        double[] angles = [0.0, 0.17, 0.62, 1.03];
        var positions = new KernelVector3[4];
        var tangents = new KernelVector3[4];
        for (var i = 0; i < 4; i++)
        {
            positions[i] = Vector(radius * Math.Cos(angles[i]), radius * Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[4];
        var scales = new double[3];
        var chords = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, -2.0, baseScale, parameters, scales, chords, out _, out _));
        var cylinder = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), radius);
        return new ICurveView(in PlaneZ0, 1, in cylinder, 1, positions, parameters, scales, chords);
    }

    // ── B1: L3 cache short-lock synchronization ─────────────────

    [Fact]
    public void Cache_ConcurrentPublishLookupEviction_NeverTearsPayload()
    {
        var identity = Identity(4242);
        // Stable key set (below capacity) with an epoch bump before every
        // publish: each publish takes the in-place overwrite branch (stale
        // epoch, same key), swapping the payload generation g under the
        // readers' feet — exactly the B1 tear scenario. Payload fields encode
        // the generation in two places (Position.Z and First.Z); a torn copy
        // mixes generations and betrays itself.
        const int writerThreads = 3;
        const int readerThreads = 3;
        const int iterations = 6000;
        const int keySpace = 64;
        var generation = 0L;
        var violations = 0L;
        var hits = 0L;

        var writers = Enumerable.Range(0, writerThreads).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                var p = (i % keySpace) * 0.25;
                var g = (double)System.Threading.Interlocked.Increment(ref generation);
                // Periodic epoch bumps turn the steady-state duplicate-key
                // publishes into in-place overwrites (stale-epoch branch),
                // while the gaps between bumps keep entries current so
                // readers actually hit them.
                if (g % 200 == 0) GeometryEvaluationCache.BumpModelGeometryEpoch();
                GeometryEvaluationCache.Publish(in identity, new CurveSample(p,
                    Vector(p, 2 * p, g), Vector(3 * p, 0, g), default, 0,
                    ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0,
                    0, SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3));
            }
        })).ToArray();
        var readers = Enumerable.Range(0, readerThreads).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                var p = (i % keySpace) * 0.25;
                if (!GeometryEvaluationCache.TryGetExact(in identity, p,
                        ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0,
                        0, out var sample)) continue;
                System.Threading.Interlocked.Increment(ref hits);
                if (sample.Parameter != p
                    || sample.Position.X != p || sample.Position.Y != 2 * p
                    || sample.First.X != 3 * p
                    || sample.Position.Z != sample.First.Z) // torn generation mix
                    System.Threading.Interlocked.Increment(ref violations);
            }
        })).ToArray();
        Task.WaitAll(writers.Concat(readers).ToArray());

        Assert.True(hits > 0, "the fixture must produce at least one hit for the test to mean anything");
        Assert.Equal(0, violations);
    }

    [Fact]
    public void Cache_ConcurrentChurnWithClear_EndsConsistent()
    {
        var identity = Identity(4243);
        const int churn = 4000;
        var churners = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < churn; i++)
            {
                var p = i * 0.125;
                GeometryEvaluationCache.Publish(in identity, new CurveSample(p,
                    Vector(p, 0, 0), default, default, 0,
                    ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0,
                    0, SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3));
                if (i % 997 == 0) GeometryEvaluationCache.Clear();
            }
        })).ToArray();
        Task.WaitAll(churners);

        GeometryEvaluationCache.Clear();
        GeometryEvaluationCache.Publish(in identity, new CurveSample(7.5,
            Vector(7.5, 0, 0), default, default, 0,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0,
            0, SampleSourceKind.CorrectedRoot, ICurveConstraintPlan.I3));
        Assert.True(GeometryEvaluationCache.TryGetExact(in identity, 7.5,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, 0, 0, out var sample));
        Assert.Equal(7.5, sample.Position.X);
    }

    // ── N1: scaled step → physical step mapping (§14.1) ─────────

    [Fact]
    public void FrozenScale_MapsScaledStepThroughColumns_LinearSystemSolvesInOneStep()
    {
        // Consolidated-report N1 counterexample: F(y) = [y1 + 100 y2 − 1,
        // y1 − 100 y2 − 1], y0 = 0. Capture yields W_F = 0.01 I and
        // D_u = diag(100, 1); the scaled Newton step is p = (0.01, 0) and the
        // physical update Δy = D_u p = (1, 0) reaches the root in one step
        // with predicted == actual reduction (ρ = 1). Adding p directly (the
        // bug) lands at (0.01, 0) and computes ρ ≈ 0.02 — a false rejection.
        Span<double> rawResidual = stackalloc double[2] { -1, -1 };
        Span<double> rawJacobian = stackalloc double[4] { 1, 100, 1, -100 };
        Span<double> residual = stackalloc double[2];
        Span<double> jacobian = stackalloc double[4];
        rawResidual.CopyTo(residual);
        rawJacobian.CopyTo(jacobian);

        var freeze = new FrozenResidualScale();
        freeze.Capture(residual, jacobian, 2);
        freeze.Apply(residual, jacobian);
        Assert.Equal(-0.01, residual[0], 15);
        Assert.Equal(1.0, jacobian[0], 15);
        Assert.Equal(1.0, jacobian[1], 15);

        Span<double> jacobianCopy = stackalloc double[4];
        Span<int> pivots = stackalloc int[2];
        Span<int> columnPivots = stackalloc int[2];
        Span<double> tau = stackalloc double[2];
        Span<double> gradient = stackalloc double[2];
        Span<double> newtonStep = stackalloc double[2];
        Span<double> step = stackalloc double[2];
        Span<double> physicalStep = stackalloc double[2];
        Assert.Equal(AlgorithmStatus.Success, TrustRegionStep.DoglegStep(
            jacobian, jacobianCopy, residual, 2, 1e3, pivots, tau, columnPivots,
            gradient, newtonStep, step, out var predicted));
        Assert.True(predicted > 0);

        Assert.True(freeze.TryMapScaledStep(step, physicalStep, 2));
        // The mapped step is the true Newton step: one application solves F.
        var y0 = 0.0;
        var y1X = y0 + physicalStep[0];
        var y1Y = y0 + physicalStep[1];
        Assert.Equal(1.0, y1X, 12);
        Assert.Equal(0.0, y1Y, 12);
        var f1x = rawJacobian[0] * y1X + rawJacobian[1] * y1Y - 1;
        var f1y = rawJacobian[2] * y1X + rawJacobian[3] * y1Y - 1;
        Assert.InRange(Math.Abs(f1x), 0, 1e-12);
        Assert.InRange(Math.Abs(f1y), 0, 1e-12);
        // ρ = (psiBase − psiTrial)/predicted ≈ 1: the trial point sits exactly
        // where the linear model predicted it would.
        Span<double> trialF = stackalloc double[2] { f1x, f1y };
        freeze.Apply(trialF, stackalloc double[4]);
        var psiBase = 0.5 * (residual[0] * residual[0] + residual[1] * residual[1]);
        var psiTrial = 0.5 * (trialF[0] * trialF[0] + trialF[1] * trialF[1]);
        Assert.InRange((psiBase - psiTrial) / predicted, 1.0 - 1e-9, 1.0 + 1e-9);
    }

    [Fact]
    public void FrozenScale_ConservativeRadiusFactor_NeverGrowsPhysicalEllipsoid()
    {
        Span<double> residual = stackalloc double[2] { 1, 1 };
        // Column scales are inverse-max AFTER row equilibration: a matrix with
        // imbalanced columns ([[1,10],[1,10]] → D_u = (10,1)) versus a more
        // extreme one ([[1,100],[1,100]] → D_u = (100,1)) versus balanced (I).
        var mild = new FrozenResidualScale();
        mild.Capture(residual, stackalloc double[4] { 1, 10, 1, 10 }, 2);
        var extreme = new FrozenResidualScale();
        extreme.Capture(residual, stackalloc double[4] { 1, 100, 1, 100 }, 2);
        var balanced = new FrozenResidualScale();
        balanced.Capture(residual, stackalloc double[4] { 1, 0, 0, 1 }, 2);

        // Rebuilding onto larger columns must shrink the radius so no physical
        // axis grows; a rebuild that would grow the ellipsoid is clamped to 1.
        Assert.InRange(extreme.ConservativeRadiusFactor(in mild), 0, 0.1 + 1e-12);
        Assert.InRange(mild.ConservativeRadiusFactor(in extreme), 1.0 - 1e-15, 1.0);
        Assert.InRange(balanced.ConservativeRadiusFactor(in balanced), 1.0 - 1e-15, 1.0);

        Span<double> accepted = stackalloc double[2];
        Span<double> trial = stackalloc double[2];
        var buffer = new SolveStateBuffer(accepted, trial, stackalloc double[2] { 1, 2 }, 5.0);
        buffer.RescaleAcceptedRadius(extreme.ConservativeRadiusFactor(in mild));
        Assert.True(buffer.AcceptedRadius <= 5.0 * 0.1 + 1e-12);
    }

    [Fact]
    public void Refine_ColumnSkewedLargeCylinder_ConvergesToTrueRoot()
    {
        // Plane z=0 ∩ cylinder R=100: the P2 unknowns (u, v) have column
        // magnitudes ~100 vs ~1 — precisely where a missing D_u mapping sends
        // the state in the wrong units. The corrector must still converge.
        var view = CircleView(100.0, 1.7 / 100.0);
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        var lambda = (t - view.ChartParameters[1]) / (view.ChartParameters[2] - view.ChartParameters[1]);
        var chord = Add(
            Scale(view.ChartPositions[1], 1 - lambda),
            Scale(view.ChartPositions[2], lambda));
        var seed = Add(chord, Vector(30, -30, 20));

        Span<double> refined = stackalloc double[4];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveCorrection.Refine(in view, ICurveConstraintPlan.P2, t, 1, in seed, refined,
                out _, out var residual));
        // Raw equation units: cylinder φ = ρ² − R² ≈ 2R·δ, so the raw residual
        // bound scales with R (δ ≈ residual / 200 here — well inside the gate).
        Assert.InRange(residual, 0, 1e-8);

        Assert.Equal(AlgorithmStatus.Success,
            ICurveCorrection.Refine(in view, ICurveConstraintPlan.I3, t, 1, in seed, refined,
                out _, out _));
        var radius = Math.Sqrt(refined[0] * refined[0] + refined[1] * refined[1]);
        Assert.InRange(Math.Abs(radius - 100.0), 0, 1e-8);
        Assert.InRange(Math.Abs(refined[2]), 0, 1e-8);
    }

    // ── N5: XT cone axis byte parity with the live Parasolid probe ──

    // Reference values transmitted by real Parasolid v380 for a solid cone
    // with basis location (1,2,3), axis (0.30151134457776363, −0.502518907629606,
    // 0.810287391340663), ref (−0.9372184261837676, 0, 0.3487429162314578),
    // radius 0.5, semi_angle 0.25 (scripts/ConeAxisProbe.cs). PK stores the
    // PK-interface axis and ref VERBATIM; our writer must match bit-level
    // (tolerance 1e-15 covers text round-trip, not a sign flip).
    [Fact]
    public void XtCone_AxisAndRef_MatchRealParasolidProbeBytes()
    {
        var basis = new PK_AXIS2_sf_s();
        basis.location.coord[0] = 1;
        basis.location.coord[1] = 2;
        basis.location.coord[2] = 3;
        basis.axis.coord[0] = 0.30151134457776363;
        basis.axis.coord[1] = -0.502518907629606;
        basis.axis.coord[2] = 0.810287391340663;
        basis.ref_direction.coord[0] = -0.9372184261837676;
        basis.ref_direction.coord[1] = 0;
        basis.ref_direction.coord[2] = 0.3487429162314578;
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidCone(0.5, 2.0, 0.25, &basis, &body));

        var document = Decode(Transmit(body));
        var cone = document.Nodes.Single(n => n.Type == (int)XtNodeTypes.Cone);

        // Field layout per sch_7016: [.., 7]=pvec, [8]=axis, [9]=radius,
        // [10]=sin_half_angle, [11]=cos_half_angle, [12]=x_axis.
        Assert.Equal(1.0, cone.Fields[7].Vector.X, 15);
        Assert.Equal(2.0, cone.Fields[7].Vector.Y, 15);
        Assert.Equal(3.0, cone.Fields[7].Vector.Z, 15);
        Assert.Equal(0.30151134457776363, cone.Fields[8].Vector.X, 15);
        Assert.Equal(-0.502518907629606, cone.Fields[8].Vector.Y, 15);
        Assert.Equal(0.810287391340663, cone.Fields[8].Vector.Z, 15);
        Assert.Equal(0.5, cone.Fields[9].Real, 15);
        Assert.Equal(0.247403959254523, cone.Fields[10].Real, 15);
        Assert.Equal(0.968912421710645, cone.Fields[11].Real, 15);
        Assert.Equal(-0.9372184261837676, cone.Fields[12].Vector.X, 15);
        Assert.Equal(0.0, cone.Fields[12].Vector.Y, 15);
        Assert.Equal(0.348742916231458, cone.Fields[12].Vector.Z, 15);

        // Full byte round trip through our own reader: the tilted axis must
        // survive materialization with no convention change anywhere.
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryMaterializeAnalyticSurfaceFromXt(
            document, cone.Index, out var surfTag));
        var asked = new PK_CONE_sf_s();
        Assert.Equal(0, KernelRuntime.ConeAsk(surfTag, &asked));
        Assert.Equal(0.30151134457776363, asked.basis_set.axis.coord[0], 15);
        Assert.Equal(-0.502518907629606, asked.basis_set.axis.coord[1], 15);
        Assert.Equal(0.810287391340663, asked.basis_set.axis.coord[2], 15);
        Assert.Equal(-0.9372184261837676, asked.basis_set.ref_direction.coord[0], 15);
    }

    // ── N9: §9.4 spun two-equation elimination ───────────────────

    private static readonly KernelVector3 SpunAxisRaw = Vector(1, 1, 1);
    private static readonly KernelVector3 SpunAxis = Scale(SpunAxisRaw, 1 / Math.Sqrt(3));
    private static readonly KernelVector3 SpunAxisPoint = Vector(2, -1, 0.5);
    private static readonly KernelVector3 SpunProfile = Vector(1.5, 0.7, -0.3);
    private static readonly KernelVector3 SpunProfileDerivative = Vector(0.2, -0.4, 0.6);

    private static KernelVector3 RotateAbout(in KernelVector3 v, in KernelVector3 unitAxis, double theta)
    {
        var c = Math.Cos(theta);
        var s = Math.Sin(theta);
        return Add(Add(Scale(v, c), Scale(Cross(in unitAxis, in v), s)),
            Scale(unitAxis, Dot(unitAxis, v) * (1 - c)));
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [Fact]
    public void SpunConstraints_PointRotatedFromProfile_IsZeroOnBothRows()
    {
        var theta = 0.83;
        var x = Add(in SpunAxisPoint, RotateAbout(Sub(in SpunProfile, in SpunAxisPoint), in SpunAxis, theta));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in x, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axial, out var radial, out _, out _, out _, out _));
        Assert.InRange(Math.Abs(axial), 0, 1e-14);
        Assert.InRange(Math.Abs(radial), 0, 1e-13);
    }

    [Fact]
    public void SpunConstraints_GradientsAndUDerivatives_MatchFiniteDifferences()
    {
        var theta = -1.3;
        var x = Add(in SpunAxisPoint, RotateAbout(Sub(in SpunProfile, in SpunAxisPoint), in SpunAxis, theta));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in x, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out _, out _, out var axialGrad, out var radialGrad, out var axialDu, out var radialDu));

        var direction = Unit(Vector(0.3, -0.7, 0.4));
        const double h = 1e-6;
        var plus = Add(in x, Scale(in direction, h));
        var minus = Add(in x, Scale(in direction, -h));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in plus, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axialP, out var radialP, out _, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in minus, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axialM, out var radialM, out _, out _, out _, out _));
        Assert.Equal((axialP - axialM) / (2 * h), Dot(axialGrad, direction), 6);
        Assert.Equal((radialP - radialM) / (2 * h), Dot(radialGrad, direction), 5);

        var profilePlus = Add(in SpunProfile, Scale(in SpunProfileDerivative, h));
        var profileMinus = Add(in SpunProfile, Scale(in SpunProfileDerivative, -h));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in x, in profilePlus, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axialUp, out var radialUp, out _, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in x, in profileMinus, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axialUm, out var radialUm, out _, out _, out _, out _));
        Assert.Equal((axialUp - axialUm) / (2 * h), axialDu, 6);
        Assert.Equal((radialUp - radialUm) / (2 * h), radialDu, 5);
    }

    [Fact]
    public void SpunConstraints_AxisPoint_IsSingular()
    {
        var x = Add(in SpunAxisPoint, Scale(in SpunAxis, 0.75)); // exactly on the axis
        Assert.Equal(AlgorithmStatus.Singular, SweptSpunImplicit.SpunConstraints(
            in x, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out _, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void SpunConstraints_WrongHeightOrRadius_IsNonZeroOnItsOwnRow()
    {
        var theta = 0.4;
        var onSheet = Add(in SpunAxisPoint, RotateAbout(Sub(in SpunProfile, in SpunAxisPoint), in SpunAxis, theta));
        var lifted = Add(in onSheet, Scale(in SpunAxis, 0.05));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in lifted, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axial, out var radial, out _, out _, out _, out _));
        Assert.Equal(0.05, Math.Abs(axial), 12);
        Assert.InRange(Math.Abs(radial), 0, 1e-13);

        // Displace purely in the section plane: along the radial projection
        // of (onSheet − P), which leaves the axial row untouched.
        var offset = Sub(in onSheet, in SpunAxisPoint);
        var radialOffset = Sub(in offset, Scale(in SpunAxis, Dot(in SpunAxis, in offset)));
        var reRadial = Add(in onSheet, Scale(Unit(in radialOffset), 0.05));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in reRadial, in SpunProfile, in SpunProfileDerivative, in SpunAxisPoint, in SpunAxisRaw,
            out var axial2, out var radial2, out _, out _, out _, out _));
        Assert.InRange(Math.Abs(axial2), 0, 1e-12);
        Assert.True(radial2 > 1e-3); // radius moved by 0.05 on a ~1.9 lever
    }

    [Fact]
    public void SpunAngleRecovery_DistinguishesBranchesAndAppliesPeriodicLift()
    {
        var theta = 0.83;
        var x = Add(in SpunAxisPoint, RotateAbout(Sub(in SpunProfile, in SpunAxisPoint), in SpunAxis, theta));
        var mirrored = Add(in SpunAxisPoint, RotateAbout(Sub(in SpunProfile, in SpunAxisPoint), in SpunAxis, -theta));

        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in x, in SpunProfile, in SpunAxisPoint, in SpunAxisRaw, 0.0, out var angle));
        Assert.Equal(theta, angle, 12);
        // Periodic lift follows the witness branch.
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in x, in SpunProfile, in SpunAxisPoint, in SpunAxisRaw, theta - 2 * Math.PI, out var lifted));
        Assert.Equal(theta - 2 * Math.PI, lifted, 12);
        // The mirrored point is a different branch: the recovered angle has
        // the opposite sign (the old dot-product residual could not tell).
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in mirrored, in SpunProfile, in SpunAxisPoint, in SpunAxisRaw, 0.0, out var mirrorAngle));
        Assert.Equal(-theta, mirrorAngle, 12);
        // On the axis the angle is undefined.
        var onAxis = Add(in SpunAxisPoint, Scale(in SpunAxis, 1.0));
        Assert.False(SweptSpunImplicit.TryRecoverSpunAngle(
            in onAxis, in SpunProfile, in SpunAxisPoint, in SpunAxisRaw, 0.0, out _));
    }

    // ── N2: cache quality in certified length units ─────────────

    [Fact]
    public void CacheQuality_Bound_ScalesWithLocalModelSize()
    {
        var unit = CircleView(1.0, 1.7);
        var scaled = CircleView(500.0, 1.7 / 500.0);
        Assert.Equal(1e-11, ICurveEvaluation.CacheQualityBound(in unit), 3);
        Assert.Equal(5e-9, ICurveEvaluation.CacheQualityBound(in scaled), 3);
        // Published roots carry the certified gate bound, comfortably below
        // the hit bound at every scale (no raw-residual coupling).
        Assert.True(ICurveEvaluation.PublicationTolerance(in scaled)
            <= ICurveEvaluation.CacheQualityBound(in scaled) / 10);
    }

    [Fact]
    public void CacheQuality_LargeRadiusModel_ExactHitStillFires()
    {
        // With raw-residual quality this fixture missed: φ ≈ 2R·δ ≈ 5e-8 for
        // a δ ≈ 5e-11 root, far above an absolute 1e-11 bound. Certified
        // length-unit quality keeps the L2 exact hit scale-consistent.
        var view = CircleView(500.0, 1.7 / 500.0);
        var t = 0.5 * (view.ChartParameters[1] + view.ChartParameters[2]);
        var storage = new CurveSample[16];
        var cache = new EvaluationSampleStore(storage);
        Span<KernelVector3> first = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, t, 2, ICurveConstraintPlan.Auto, ref cache,
                first, out var report1));
        Assert.True(double.IsFinite(report1.QualityError));
        Assert.True(report1.QualityError <= ICurveEvaluation.CacheQualityBound(in view));

        Span<KernelVector3> second = new KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveEvaluation.EvaluateWithCache(in view, t, 2, ICurveConstraintPlan.Auto, ref cache,
                second, out var report2));
        Assert.Equal(CacheHitKind.Exact, report2.CacheHit);
        Assert.Equal(first[0].X, second[0].X, 10);
        Assert.Equal(first[1].Y, second[1].Y, 8);
    }

    // ── N13: dimensionless joint acceptance ──────────────────────

    private static void JointFixture(double sphereRadius, double tubeRadius, double pointOffsetFactor,
        out AnalyticSurface a, out AnalyticSurface d, out AnalyticSurface s, out KernelVector3 point)
    {
        // Spine: A(c): c_z = 0  ∩  D(c): ‖c‖ = sphereRadius → c₀ on the axis-plane
        // circle; S(x): x_z = 0; parameter plane y = 0.
        a = PlaneZ0;
        d = new AnalyticSurface(SurfaceClass.Sphere, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), sphereRadius);
        s = PlaneZ0;
        point = Vector(sphereRadius + pointOffsetFactor * tubeRadius, 0, 0);
    }

    [Fact]
    public void JointLift_SmallRadiusFalseSuccess_IsRejectedAndConvergesToTrueRoot()
    {
        // The consolidated counterexample: r = 2⁻²⁴ with the point at 2r from
        // c. The raw radius residual 3r² ≈ 1.07e−14 passed the absolute 1e−12
        // test and returned Success with a 100% radial error; the
        // dimensionless row test (|F| / (2r·L) ≈ 0.5) must reject it and the
        // Newton loop must converge to the true tube point.
        const double r = 1.0 / 16777216.0; // 2^-24
        JointFixture(1.0, r, pointOffsetFactor: 2.0, out var a, out var d, out var s, out var point);
        Span<double> state = new double[6];
        var status = BlendJointLift.TryLiftFromLocalSingular(
            in a, in d, in s, Vector(0, 0, 0), Vector(0, 1, 0), r * r, in point,
            spineSeed: 0.0, spineRadius: 1.0, state, out var iterations, out _);

        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.True(iterations >= 2, "the initial state must be rejected, forcing at least one Newton step");
        var dx = state[0] - state[3];
        var dy = state[1] - state[4];
        var dz = state[2] - state[5];
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        Assert.InRange(Math.Abs(distance - r), 0, 1e-8 * Math.Max(1.0, r));
        Assert.Equal(1.0, state[3], 10);
        Assert.Equal(0.0, state[4], 10);
        Assert.Equal(0.0, state[5], 10);
    }

    [Fact]
    public void JointLift_LargeScaleFixture_Converges()
    {
        const double radius = 1000.0;
        const double tube = 1.0;
        JointFixture(radius, tube, pointOffsetFactor: 2.0, out var a, out var d, out var s, out var point);
        Span<double> state = new double[6];
        Assert.Equal(AlgorithmStatus.Success, BlendJointLift.TryLiftFromLocalSingular(
            in a, in d, in s, Vector(0, 0, 0), Vector(0, 1, 0), tube * tube, in point,
            spineSeed: 0.0, spineRadius: radius, state, out _, out _));
        var dx = state[0] - state[3];
        var dy = state[1] - state[4];
        var dz = state[2] - state[5];
        Assert.InRange(Math.Abs(Math.Sqrt(dx * dx + dy * dy + dz * dz) - tube), 0, 1e-8);
    }

    [Fact]
    public void JointResidual_Denominators_ArePositiveAndDimensionless()
    {
        const double r = 1.0 / 16777216.0;
        JointFixture(1.0, r, pointOffsetFactor: 2.0, out var a, out var d, out var s, out var point);
        Span<double> residual = new double[6];
        Span<double> denominators = new double[6];
        Assert.Equal(AlgorithmStatus.Success, JointBlendResidual.AssembleResidual(
            in a, in d, in s, Vector(0, 0, 0), Vector(0, 1, 0), r * r,
            in point, Vector(1, 0, 0), residual, denominators));
        for (var i = 0; i < 6; i++)
        {
            Assert.True(denominators[i] > 0 && double.IsFinite(denominators[i]));
        }
        // Radius row: 3r² / (2r·3r) = 0.5 — the exact false-success figure of
        // the counterexample, now above any sane dimensionless tolerance.
        Assert.Equal(0.5, Math.Abs(residual[2]) / denominators[2], 12);
    }

    // ── transmit helpers (same pattern as XtGeometryWriterTests) ──

    private static string Transmit(int body)
    {
        var parts = stackalloc int[1] { body };
        var options = new PK_PART_transmit_o_s
        {
            o_t_version = 4,
            transmit_format = ParasolidConstants.PK_transmit_format_text_c,
            transmit_version = 371,
            transmit_meshes = ParasolidConstants.PK_transmit_meshes_separate_c,
        };
        var block = new PK_MEMORY_block_s();
        Assert.Equal(0, KernelRuntime.PartTransmitB(1, parts, &options, &block));
        try
        {
            return System.Text.Encoding.ASCII.GetString(block.bytes, checked((int)block.n_bytes));
        }
        finally { Assert.Equal(0, KernelRuntime.MemoryBlockFree(&block)); }
    }

    private static XtDocument Decode(string text) => XtText.DecodeDocument(text);
}
