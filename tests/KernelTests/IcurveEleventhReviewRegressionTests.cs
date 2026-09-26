// Regression tests for issues identified in gpt_review_1.md.
using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveEleventhReviewRegressionTests
{
    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    [Fact]
    public void Terminator_BranchTracking_MustNotJumpToWrongTorusCircle()
    {
        // Counterexample from review 1:
        // Torus R=1.5, r=1.0 cut by x=0 -> two circles (y - 1.5)^2 + z^2 = 1 and (y + 1.5)^2 + z^2 = 1.
        // B = C(1.5), E = C(4.1) both on +Y circle.
        // Target t = 0.55 must track +Y circle near (0, 0.52988, 0.24264) and never jump to -Y circle (0, -1.589, 0.996).
        var torus = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.5, 1.0);

        var b = Vector(0, 1.5707372016677028, 0.9974949866040544);
        var e = Vector(0, 0.9251760534667308, -0.8182771110644103);

        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in torus, in e, 1, out var endpointJet));
        var normal = Unit(endpointJet.Gradient);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in e, in b, in normal,
                out var planeNormal, out var chordUnit, out var line));

        var chordRate = Scale(Sub(e, b), 1.0); // tE - tB = 1.0
        var chordLen = Math.Sqrt(Dot(Sub(e, b), Sub(e, b)));
        var anchor = new TerminatorEvaluation.TerminatorAnchor(0,
            in e, in b, in planeNormal, in chordUnit, in line, in chordRate,
            1.0, 0.0, chordLen);

        var budget = EvaluationBudget.Default;
        var status = TerminatorEvaluation.SolveIntervalPoint(in torus, in anchor, 0.55,
            ref budget, out _, out var point, out var residual, out _);

        if (status == AlgorithmStatus.Success)
        {
            Assert.True(point.Y > 0, $"Terminator jumped to wrong negative-Y branch: y={point.Y}");
            var expectedY = 0.5298838;
            var expectedZ = 0.2426408;
            Assert.InRange(point.Y, expectedY - 0.05, expectedY + 0.05);
            Assert.InRange(point.Z, expectedZ - 0.05, expectedZ + 0.05);
            Assert.InRange(residual, 0.0, 1e-10);
        }
    }

    [Fact]
    public void Svd_SubnormalInput_MustSucceedWithRankTwo()
    {
        // 1e-320 I_2 has subnormal diagonal entries whose reciprocal would overflow to infinity.
        var scale = 1e-320;
        Span<double> matrix = stackalloc double[4] { scale, 0, 0, scale };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];

        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdFactorizeSquare(matrix, 2, sigma, u, v, 0.0, out var rank));
        Assert.Equal(2, rank);
        Assert.True(double.IsFinite(sigma[0]) && sigma[0] > 0.0);
        Assert.True(double.IsFinite(sigma[1]) && sigma[1] > 0.0);
        Assert.InRange(Math.Abs(sigma[0] / scale - 1.0), 0.0, 1e-12);
        Assert.InRange(Math.Abs(sigma[1] / scale - 1.0), 0.0, 1e-12);
    }

    [Fact]
    public void Svd_MixedScales_MustRetainBothSingularValuesAndSolveAccurately()
    {
        // Matrix diag(1e200, 1) has well-separated scales where squaring 1e-200 underflows.
        // Robust column scaling must preserve sigma_1 = 1.0 and rank 2 when rankTolerance is 0.
        Span<double> matrix = stackalloc double[4] { 1e200, 0, 0, 1.0 };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];

        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdFactorizeSquare(matrix, 2, sigma, u, v, 0.0, out var rank));
        Assert.Equal(2, rank);
        Assert.InRange(Math.Abs(sigma[0] / 1e200 - 1.0), 0.0, 1e-12);
        Assert.InRange(Math.Abs(sigma[1] - 1.0), 0.0, 1e-12);

        Span<double> rhs = stackalloc double[2] { 1e200, 1.0 };
        Span<double> x = stackalloc double[2];
        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdMinNormSolve(sigma, u, v, 2, rank, rhs, x));
        Assert.InRange(Math.Abs(x[0] - 1.0), 0.0, 1e-12);
        Assert.InRange(Math.Abs(x[1] - 1.0), 0.0, 1e-12);
    }

    [Fact]
    public void Svd_MixedScales_ReversedOrder_MustSucceed()
    {
        Span<double> matrix = stackalloc double[4] { 1.0, 0, 0, 1e-200 };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];

        Assert.Equal(AlgorithmStatus.Success,
            SmallLinearSolve.SvdFactorizeSquare(matrix, 2, sigma, u, v, 0.0, out var rank));
        Assert.Equal(2, rank);
        Assert.InRange(Math.Abs(sigma[0] - 1.0), 0.0, 1e-12);
        Assert.InRange(Math.Abs(sigma[1] / 1e-200 - 1.0), 0.0, 1e-12);
    }

    [Fact]
    public void Terminator_BudgetAccounting_MustMatchActualImplicitEvaluations()
    {
        // Linear plane z=0, B=(0,0,0), E=(1,0,0), tB=0, tE=1, t=0.5
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var b = Vector(0, 0, 0);
        var e = Vector(1, 0, 0);
        var normal = Vector(0, 0, 1);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in e, in b, in normal,
                out var planeNormal, out var chordUnit, out var line));

        var chordRate = Scale(Sub(e, b), 1.0);
        var chordLen = 1.0;
        var anchor = new TerminatorEvaluation.TerminatorAnchor(0,
            in e, in b, in planeNormal, in chordUnit, in line, in chordRate,
            1.0, 0.0, chordLen);

        // Budget 0: must return NotConverged with 0 used and 0 reported evals
        var b0 = new EvaluationBudget(0);
        var s0 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b0, out _, out _, out _, out var ev0);
        Assert.Equal(AlgorithmStatus.NotConverged, s0);
        Assert.Equal(0, ev0);
        Assert.Equal(0, b0.Used);

        // Budget 1: consumes branch jet at B, fails on Newton probe -> NotConverged with 1 used and 1 eval
        var b1 = new EvaluationBudget(1);
        var s1 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b1, out _, out _, out _, out var ev1);
        Assert.Equal(AlgorithmStatus.NotConverged, s1);
        Assert.Equal(1, ev1);
        Assert.Equal(1, b1.Used);

        // Budget 2: consumes branch jet at B, solves root on z=0 in 1 iteration -> Success with 2 used and 2 evals
        var b2 = new EvaluationBudget(2);
        var s2 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b2, out var mu2, out var pt2, out _, out var ev2);
        Assert.Equal(AlgorithmStatus.Success, s2);
        Assert.Equal(2, ev2);
        Assert.Equal(2, b2.Used);
        Assert.Equal(0.5, pt2.X, 10);
        Assert.Equal(0.0, pt2.Y, 10);
        Assert.Equal(0.0, pt2.Z, 10);
    }

    [Fact]
    public void BlendJoint_SpineAnglePeriodRecovery_MustRespectBoundsAndWitness()
    {
        // Target s* = 2*pi + 0.25, bounds [2*pi, 2*pi + 0.5]
        var targetS = 2.0 * Math.PI + 0.25;
        var bounds = new BlendArcBounds(0, 0, 0, sMin: 2.0 * Math.PI, sMax: 2.0 * Math.PI + 0.5);

        // Raw s0 from atan2 is ~0.25
        var s0 = Math.Atan2(Math.Sin(targetS), Math.Cos(targetS));
        Assert.InRange(Math.Abs(s0 - 0.25), 0.0, 1e-12);

        // Unwrapping into bounds [2*pi, 2*pi + 0.5] must recover 2*pi + 0.25
        Assert.True(BlendJointLift.TryUnwrapSpineAngle(s0, in bounds, double.NaN, out var unwrappedS));
        Assert.InRange(Math.Abs(unwrappedS - targetS), 0.0, 1e-12);

        // Outside bounds: bounds [0, 0.5] should reject candidate if target is 2*pi + 0.25
        var narrowBounds = new BlendArcBounds(0, 0, 0, sMin: 4.0 * Math.PI, sMax: 4.0 * Math.PI + 0.5);
        Assert.True(BlendJointLift.TryUnwrapSpineAngle(s0, in narrowBounds, double.NaN, out var unwrappedHigh));
        Assert.InRange(Math.Abs(unwrappedHigh - (4.0 * Math.PI + 0.25)), 0.0, 1e-12);
    }

    [Fact]
    public void BlendJoint_PlanStateMigration_MustPreservePeriodWithReferenceS()
    {
        var targetS = 2.0 * Math.PI + 0.75;
        Span<double> jointState = stackalloc double[6]
        {
            1.0, 2.0, 3.0,
            2.0 * Math.Cos(targetS), 2.0 * Math.Sin(targetS), 0.0
        };
        Span<double> elimState = stackalloc double[4];

        Assert.Equal(AlgorithmStatus.Success,
            BlendJointLift.TryMigratePlanState(jointState, fromIsJoint: true, spineRadius: 2.0,
                elimState, toIsJoint: false, referenceS: targetS));

        Assert.InRange(Math.Abs(elimState[3] - targetS), 0.0, 1e-12);
    }
}
