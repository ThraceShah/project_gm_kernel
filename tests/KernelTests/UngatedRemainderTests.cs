using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class UngatedRemainderTests
{
    [Fact]
    public void AffineImplicit_UniformScale_RescalesDistance()
    {
        Assert.True(AffineImplicitJets.IsUniformScale([2, 0, 0, 0, 2, 0, 0, 0, 2], out var scale));
        Assert.Equal(2, scale, 15);
        Assert.Equal(AlgorithmStatus.Success,
            AffineImplicitJets.TryUniformScaleDistance(2, 0.25, out var d));
        Assert.Equal(0.5, d, 15);
        Assert.False(AffineImplicitJets.IsUniformScale([2, 0, 0, 0, 3, 0, 0, 0, 2], out _));
    }

    [Fact]
    public void AffineImplicit_Transform_MatchesInverseTransposeGradient()
    {
        // A = 2 I → A^{-1} = 0.5 I; ∇̃ = 0.5 ∇.
        Span<double> aInv = [0.5, 0, 0, 0, 0.5, 0, 0, 0, 0.5];
        var g = Vector(1, 0, 0);
        Assert.Equal(AlgorithmStatus.Success, AffineImplicitJets.TransformImplicit(
            aInv, in g, 1, 0, 0, 1, 0, 1,
            out var tg, out var thxx, out _, out _, out _, out _, out _));
        Assert.Equal(0.5, tg.X, 12);
        Assert.Equal(0.25, thxx, 12);
    }

    [Fact]
    public void SweptElimination_OnSheet_IsZero()
    {
        var c = Vector(1, 0, 0);
        var cPrime = Vector(0, 1, 0);
        var d = Vector(0, 0, 1);
        var point = Add(c, Scale(d, 2.5)); // on the swept sheet
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SweptEliminationResidual(
            in point, in c, in cPrime, in d, out var residual, out _));
        Assert.InRange(Math.Abs(residual), 0, 1e-14);
    }

    [Fact]
    public void SpunMeridian_SharedPlane_IsZero()
    {
        var axisP = Vector(0, 0, 0);
        var axis = Vector(0, 0, 1);
        var profile = Vector(2, 0, 1);
        var point = Vector(0, 2, 1); // same cylindrical radius plane family via 90° spin
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunMeridianResidual(
            in point, in profile, in axisP, in axis, out var residual, out _));
        Assert.InRange(Math.Abs(residual), 0, 1e-14);
    }

    [Fact]
    public void LevenbergMarquardt_FullRank_AgreesWithNewtonDirection()
    {
        // Identity J, residual = (1, -2): Newton step = -r; LM with λ=0 matches.
        Span<double> j = [1, 0, 0, 1];
        Span<double> jCopy = new double[4];
        Span<double> r = [1, -2];
        Span<double> step = new double[2];
        Span<double> stacked = new double[8];
        Span<int> pivots = new int[2];
        Span<double> tau = new double[2];
        Assert.Equal(AlgorithmStatus.Success, LevenbergMarquardtStep.ComputeStep(
            j, jCopy, r, 2, lambda: 0, pivots, tau, step, stacked, out var predicted));
        Assert.True(predicted > 0);
        Assert.InRange(Math.Abs(step[0] + 1), 0, 1e-12);
        Assert.InRange(Math.Abs(step[1] - 2), 0, 1e-12);
    }

    [Fact]
    public void FailureReplayRing_RecordsAndCopiesOldestFirst()
    {
        var ring = new FailureReplayRing();
        ring.Record(ICurveConstraintPlan.I3, 0.5, 1, AlgorithmStatus.NotConverged, 1e-3,
            ICurveEvalDetail.Stagnation);
        ring.Record(ICurveConstraintPlan.P2, 0.6, 1, AlgorithmStatus.Singular, 1e-2,
            ICurveEvalDetail.ParameterizationSingular);
        Span<FailureReplayRing.Frame> frames = new FailureReplayRing.Frame[8];
        Assert.True(ring.TryCopy(frames, out var copied));
        Assert.Equal(2, copied);
        Assert.Equal(ICurveConstraintPlan.I3, frames[0].ConstraintPlan);
        Assert.Equal(0.5, frames[0].Parameter, 15);
        Assert.Equal(ICurveEvalDetail.Stagnation, frames[0].Detail);
    }

    [Fact]
    public void JointLift_RefusesTerminatorSpine()
    {
        var a = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 2);
        var d = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var outer = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0.1), Vector(0, 0, 1), Vector(1, 0, 0));
        Span<double> state = new double[6];
        Assert.Equal(AlgorithmStatus.Unsupported, BlendJointLift.TryLiftFromLocalSingular(
            in a, in d, in outer, Vector(2, 0, 0.1), Vector(0, 1, 0), 0.0625,
            Vector(2.1, 0, 0.1), 0.0, 2.0, isTerminatorSpine: true, state, out _, out _));
    }

    [Fact]
    public void Occurrence_DoesNotMergeDifferentParameters()
    {
        var a = new BlendJointLift.Occurrence(7, 0.25, 0);
        var b = new BlendJointLift.Occurrence(7, 0.2500000000000001, 0);
        Assert.False(a.Equals(in b));
        Assert.True(a.Equals(new BlendJointLift.Occurrence(7, 0.25, 0)));
    }

    [Fact]
    public void MultiSeed_AmbiguousBranch_IsNotBrokenByProximity()
    {
        Span<SeedCandidate> candidates =
        [
            new(0.5, SeedSource.LocalSearch, SeedQuality.SeedOnly, branchId: 0, sourceIndex: 0, 1e-6, 0),
            new(0.5, SeedSource.LocalSearch, SeedQuality.SeedOnly, branchId: 1, sourceIndex: 1, 1e-6, 0),
        ];
        Span<SeedCandidate> ordered = new SeedCandidate[2];
        Assert.Equal(AlgorithmStatus.NotConverged,
            ICurveSeedSelection.OrderSeeds(candidates, requestedBranch: -1, ordered, out _, out var failure));
        Assert.Equal(SeedOrderFailure.AmbiguousBranch, failure);
    }

    [Fact]
    public void PlanAlternates_ExcludeFailedAndPreferCheaper()
    {
        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var cyl = new AnalyticSurface(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        double[] angles = [0.0, 0.5, 1.0];
        var positions = new KernelVector3[3];
        var tangents = new KernelVector3[3];
        for (var i = 0; i < 3; i++)
        {
            positions[i] = Vector(Math.Cos(angles[i]), Math.Sin(angles[i]), 0);
            tangents[i] = Vector(-Math.Sin(angles[i]), Math.Cos(angles[i]), 0);
        }
        var parameters = new double[3];
        var scales = new double[2];
        var chords = new KernelVector3[2];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0, 1, parameters, scales, chords, out _, out _));
        var view = new ICurveView(in plane, 1, in cyl, 1, positions, parameters, scales, chords);
        Span<ICurveConstraintPlan> alts = stackalloc ICurveConstraintPlan[5];
        var n = ICurveConstraintPlanRules.Alternates(in view, ICurveConstraintPlan.I1, alts);
        Assert.True(n >= 1);
        for (var i = 0; i < n; i++)
            Assert.NotEqual(ICurveConstraintPlan.I1, alts[i]);
    }

    [Fact]
    public void ContactFromSpineWitness_OffsetsAlongSenseNormal()
    {
        var sphere = new AnalyticSurface(SurfaceClass.Sphere, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1);
        Assert.Equal(AlgorithmStatus.Success, BlendEvaluation.TryContactFromSpineWitness(
            in sphere, u: 0, v: 0, radius: 0.25, sense: 1, out var contact, out var n));
        Assert.InRange(Math.Abs(Dot(n, n) - 1), 0, 1e-12);
        // Sphere parametric (u,v)=(0,0) → point on equator along +X for this basis.
        Assert.True(Dot(Sub(contact, Vector(1, 0, 0)), n) > 0);
    }

    [Fact]
    public void CylinderDistanceJets_D3Term_IsNotDroppable()
    {
        var center = Vector(0, 0, 0);
        var axis = Vector(0, 0, 1);
        var point = Vector(2, 0.5, 1);
        var g = Vector(0.3, -0.2, 0.1);
        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axis, 1.0, in point, in g,
            out var value, out var grad, out var hxx, out _, out _, out _, out _, out _,
            out var txx, out _, out _, out _, out _, out _));
        Assert.True(Math.Abs(txx) > 0);
        Assert.True(Math.Abs(value) > 0);
        Assert.True(IsFinite(grad));
        Assert.True(double.IsFinite(hxx));
    }

    [Fact]
    public void EvaluationBudget_TightenInner_ReducesEta()
    {
        var budget = EvaluationBudget.Default;
        Assert.Equal(1.0, budget.InnerAccuracyFactor);
        budget.TightenInner(0.5);
        Assert.Equal(0.5, budget.InnerAccuracyFactor);
        budget.TightenInner(0.5);
        Assert.Equal(0.25, budget.InnerAccuracyFactor);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
