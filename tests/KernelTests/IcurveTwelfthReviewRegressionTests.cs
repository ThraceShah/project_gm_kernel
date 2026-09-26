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

public class IcurveTwelfthReviewRegressionTests
{
    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [Fact]
    public void Terminator_ScaledTorus_MustNotJumpBranches()
    {
        // Torus R=1.5, r=1.0 scaled by lambda=0.05 (R=0.075, r=0.05)
        var lambda = 0.05;
        var major = 1.5 * lambda;
        var minor = 1.0 * lambda;
        var torusSurf = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), major, minor);

        var b0 = Vector(0, 1.5707372016677028, 0.9974949866040544);
        var e0 = Vector(0, 0.9251760534667308, -0.8182771110644103);
        var b = Vector(b0.X * lambda, b0.Y * lambda, b0.Z * lambda);
        var e = Vector(e0.X * lambda, e0.Y * lambda, e0.Z * lambda);

        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in torusSurf, in e, 1, out var endpointJet));
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in torusSurf, in b, 1, out var branchJet));
        var supportNormal = Unit(endpointJet.Gradient);

        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in e, in b, in supportNormal,
                out var planeNormal, out var chordUnit, out var lineDirection));

        var tB = 0.0;
        var tE = 1.0;
        var toBranch = Sub(b, e);
        var chordLength = Math.Sqrt(Dot(toBranch, toBranch));
        var chordRate = Scale(Sub(e, b), 1.0 / (tE - tB));

        var anchor = new TerminatorEvaluation.TerminatorAnchor(
            0, in e, in b, in planeNormal, in chordUnit, in lineDirection, in chordRate,
            tE, tB, chordLength);

        var budget = EvaluationBudget.Default;
        var targetT = 0.55;
        var status = TerminatorEvaluation.SolveIntervalPoint(in torusSurf, in anchor, targetT,
            ref budget, out var mu, out var point, out var residual, out var evaluations);

        Assert.Equal(AlgorithmStatus.Success, status);
        // Correct root is at Y > 0: approx (0, 0.02649419, 0.01213204)
        // Defective code jumped to negative Y: (0, -0.079457, 0.04980)
        Assert.True(point.Y > 0, $"Expected Y > 0 on correct branch, got point=({point.X}, {point.Y}, {point.Z})");
        Assert.InRange(point.Y, 0.020, 0.035);
        Assert.InRange(point.Z, 0.005, 0.020);
        Assert.InRange(residual, 0.0, 1e-10);

        // Verify target chord plane is satisfied
        var targetQ = TerminatorEvaluation.InterpolatedChordPoint(in anchor, targetT);
        var chordPlaneRes = Math.Abs(TerminatorEvaluation.ChordPlaneResidual(in anchor.ChordUnit, in targetQ, in point));
        Assert.InRange(chordPlaneRes, 0.0, 1e-12);

        // Also verify original scale lambda=1.0 still works
        var torusSurf1 = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.5, 1.0);
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in torusSurf1, in e0, 1, out var endpointJet1));
        var norm1 = Unit(endpointJet1.Gradient);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in e0, in b0, in norm1, out var pn1, out var cu1, out var ld1));
        var cr1 = Scale(Sub(e0, b0), 1.0 / (tE - tB));
        var anchor1 = new TerminatorEvaluation.TerminatorAnchor(
            0, in e0, in b0, in pn1, in cu1, in ld1, in cr1, tE, tB,
            Math.Sqrt(Dot(Sub(b0, e0), Sub(b0, e0))));

        var budget1 = EvaluationBudget.Default;
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.SolveIntervalPoint(in torusSurf1, in anchor1, targetT,
                ref budget1, out _, out var point1, out var res1, out _));
        Assert.True(point1.Y > 0);
        Assert.InRange(point1.Y, 0.50, 0.56);
        Assert.InRange(res1, 0.0, 1e-10);
    }

    [Fact]
    public void Terminator_ShortParameterInterval_MustNotAdsorbToBranchPoint()
    {
        // Unit cylinder x^2 + y^2 = 1, z = 0
        var cylSurf = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0);

        var b = Vector(1, 0, 0);
        var e = Vector(0, 1, 0);
        var tB = 0.0;
        var tE = 1e-15;
        var targetT = 5e-16;

        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in cylSurf, in e, 1, out var endpointJet));
        var supportNormal = Unit(endpointJet.Gradient);

        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in e, in b, in supportNormal,
                out var planeNormal, out var chordUnit, out var lineDirection));

        var toBranch = Sub(b, e);
        var chordLength = Math.Sqrt(Dot(toBranch, toBranch));
        var chordRate = Scale(Sub(e, b), 1.0 / (tE - tB));

        var anchor = new TerminatorEvaluation.TerminatorAnchor(
            0, in e, in b, in planeNormal, in chordUnit, in lineDirection, in chordRate,
            tE, tB, chordLength);

        // 1. Target t = 5e-16 (midpoint): correct root is (1/sqrt(2), 1/sqrt(2), 0)
        var budget = EvaluationBudget.Default;
        var status = TerminatorEvaluation.SolveIntervalPoint(in cylSurf, in anchor, targetT,
            ref budget, out var mu, out var point, out var residual, out var evaluations);

        Assert.Equal(AlgorithmStatus.Success, status);
        var expectedCoord = 1.0 / Math.Sqrt(2.0);
        Assert.InRange(Math.Abs(point.X - expectedCoord), 0.0, 1e-8);
        Assert.InRange(Math.Abs(point.Y - expectedCoord), 0.0, 1e-8);
        Assert.InRange(Math.Abs(point.Z), 0.0, 1e-8);

        // Verify target chord plane residual is small
        var targetQ = TerminatorEvaluation.InterpolatedChordPoint(in anchor, targetT);
        var chordPlaneRes = Math.Abs(TerminatorEvaluation.ChordPlaneResidual(in anchor.ChordUnit, in targetQ, in point));
        Assert.InRange(chordPlaneRes, 0.0, 1e-12);

        // 2. Exact endpoint t == tB must return exact B point
        var budgetB = EvaluationBudget.Default;
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.SolveIntervalPoint(in cylSurf, in anchor, tB,
                ref budgetB, out _, out var pointB, out _, out _));
        Assert.Equal(b.X, pointB.X);
        Assert.Equal(b.Y, pointB.Y);
        Assert.Equal(b.Z, pointB.Z);
    }

    [Fact]
    public void Svd_ExtremeDynamicRange_MustSucceedWithCorrectRank()
    {
        // A = diag(10^200, 10^-200)
        Span<double> a = stackalloc double[4] { 1e200, 0.0, 0.0, 1e-200 };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];

        var status = SmallLinearSolve.SvdFactorizeSquare(a, 2, sigma, u, v, 0.0, out var rank);
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.Equal(2, rank);
        Assert.InRange(Math.Abs(sigma[0] - 1e200) / 1e200, 0.0, 1e-14);
        Assert.InRange(Math.Abs(sigma[1] - 1e-200) / 1e-200, 0.0, 1e-14);

        // Solve A x = b where b = (10^200, 10^-200)^T => x = (1, 1)^T
        Span<double> b = stackalloc double[2] { 1e200, 1e-200 };
        Span<double> x = stackalloc double[2];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.SvdMinNormSolve(sigma, u, v, 2, rank, b, x));
        Assert.InRange(Math.Abs(x[0] - 1.0), 0.0, 1e-12);
        Assert.InRange(Math.Abs(x[1] - 1.0), 0.0, 1e-12);
    }

    [Fact]
    public void Svd_LossOfNonZeroData_MustReturnNumericalFailure()
    {
        // Matrix with 1e305 and 1e-200: dynamic range > 10^500 cannot be represented in double
        // Scaling by 1e305 causes 1e-200 to underflow to 0. Must reject with NumericalFailure, not false Success rank 1
        Span<double> a = stackalloc double[4] { 1e305, 0.0, 0.0, 1e-200 };
        Span<double> sigma = stackalloc double[2];
        Span<double> u = stackalloc double[4];
        Span<double> v = stackalloc double[4];

        var status = SmallLinearSolve.SvdFactorizeSquare(a, 2, sigma, u, v, 0.0, out var rank);
        Assert.Equal(AlgorithmStatus.NumericalFailure, status);
    }

    [Fact]
    public void BlendJoint_TryUnwrapSpineAngle_MultiCandidateWithoutWitness_MustReject()
    {
        var twoPi = 2.0 * Math.PI;
        var s0 = 0.25;
        // Bounds [0, 2*pi + 0.5] contains two candidates: 0.25 and 2*pi + 0.25
        var bounds = new BlendArcBounds(0, 0, 0, sMin: 0.0, sMax: twoPi + 0.5);

        // 1. Without witness (spineSeed is NaN): must reject due to ambiguity
        Assert.False(BlendJointLift.TryUnwrapSpineAngle(s0, in bounds, double.NaN, out _));

        // 2. With witness near 0.25: must pick 0.25
        Assert.True(BlendJointLift.TryUnwrapSpineAngle(s0, in bounds, 0.3, out var sNear));
        Assert.InRange(Math.Abs(sNear - 0.25), 0.0, 1e-12);

        // 3. With witness near second candidate: must pick 2*pi + 0.25
        Assert.True(BlendJointLift.TryUnwrapSpineAngle(s0, in bounds, twoPi + 0.3, out var sHigh));
        Assert.InRange(Math.Abs(sHigh - (twoPi + 0.25)), 0.0, 1e-12);

        // 4. With witness exactly midpoint (equidistant tie): must reject ambiguity
        var midSeed = 0.25 + Math.PI;
        Assert.False(BlendJointLift.TryUnwrapSpineAngle(s0, in bounds, midSeed, out _));

        // 5. Single candidate within bounds: [2*pi, 2*pi + 0.5] only has 2*pi + 0.25
        // Even without witness (NaN), must succeed
        var singleBounds = new BlendArcBounds(0, 0, 0, sMin: twoPi, sMax: twoPi + 0.5);
        Assert.True(BlendJointLift.TryUnwrapSpineAngle(s0, in singleBounds, double.NaN, out var sSingle));
        Assert.InRange(Math.Abs(sSingle - (twoPi + 0.25)), 0.0, 1e-12);
    }

    [Fact]
    public void CaseFValidator_MustRejectNaNAndNonFinite()
    {
        // 1. NaN in raw D2 must be rejected
        var nanSamples = new CaseFEvalSample[]
        {
            new CaseFEvalSample(0.0, 0.0, double.NaN, 0.0, 0.0)
        };
        Assert.False(CaseFValidator.Validate(nanSamples, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var nanFailure));
        Assert.Contains("non-finite", nanFailure);

        // 2. All NaNs must be rejected
        var allNan = new CaseFEvalSample[]
        {
            new CaseFEvalSample(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN)
        };
        Assert.False(CaseFValidator.Validate(allNan, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var allNanFailure));
        Assert.Contains("non-finite", allNanFailure);

        // 3. Negative metrics must be rejected
        var negMetric = new CaseFEvalSample[]
        {
            new CaseFEvalSample(0.0, -1e-5, 0.0, 0.0, 0.0)
        };
        Assert.False(CaseFValidator.Validate(negMetric, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out _));

        // 4. Negative or NaN tolerance must be rejected
        var validSample = new CaseFEvalSample[]
        {
            new CaseFEvalSample(1e-10, 1e-8, 1e-8, 1e-8, 1e-10)
        };
        Assert.False(CaseFValidator.Validate(validSample, 1e-8, -1.0, 1e-6, 1e-5, 1e-8, out var badTolFailure));
        Assert.Contains("Invalid tolerance", badTolFailure);

        // 5. Valid samples within tolerance must pass
        Assert.True(CaseFValidator.Validate(validSample, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var okReason));
        Assert.Empty(okReason);

        // 6. Valid sample exceeding tolerance must be rejected
        var excessiveSample = new CaseFEvalSample[]
        {
            new CaseFEvalSample(1e-5, 1e-8, 1e-8, 1e-8, 1e-10) // DiffD0 1e-5 > tol 1e-8
        };
        Assert.False(CaseFValidator.Validate(excessiveSample, 1e-8, 1e-6, 1e-6, 1e-5, 1e-8, out var excFailure));
        Assert.Contains("tolerance exceeded", excFailure);
    }

    [Fact]
    public void CaseFValidator_ValidateFiniteDifferenceD2_Coverage()
    {
        var validD1Plus = (0.0, 1.0 + 1e-5, 0.0);
        var validD1Minus = (0.0, 1.0 - 1e-5, 0.0);
        var validD2 = (0.0, 1.0, 0.0);

        // 1. Valid probe passes
        Assert.True(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, validD2, 1e-5, 1e-6, out var fdD2, out var err, out var reason));
        Assert.True(err <= 1e-6);
        Assert.Empty(reason);
        Assert.Equal(0.0, fdD2.x, 10);
        Assert.Equal(1.0, fdD2.y, 10);
        Assert.Equal(0.0, fdD2.z, 10);

        // 2. Only D1(t+h) injected with NaN -> rejects
        var nanPlus = (double.NaN, 1.0 + 1e-5, 0.0);
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(nanPlus, validD1Minus, validD2, 1e-5, 1e-6, out _, out var nanReason));
        Assert.Contains("Non-finite", nanReason);

        // 3. Only D1(t-h) injected with Infinity -> rejects
        var infMinus = (0.0, double.PositiveInfinity, 0.0);
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, infMinus, validD2, 1e-5, 1e-6, out _, out var infReason));
        Assert.Contains("Non-finite", infReason);

        // 4. Candidate D2 injected with NaN -> rejects
        var nanD2 = (0.0, double.NaN, 0.0);
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, nanD2, 1e-5, 1e-6, out _, out var d2Reason));
        Assert.Contains("Non-finite", d2Reason);

        // 5. Finite but error exceeds tolerance -> rejects
        var largePlus = (0.0, 1.0 + 1e-3, 0.0);
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(largePlus, validD1Minus, validD2, 1e-5, 1e-6, out var largeErr, out var tolReason));
        Assert.Contains("tolerance", tolReason);
        Assert.True(largeErr > 1e-6);

        // 6. Non-positive or non-finite step size h -> rejects
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, validD2, 0.0, 1e-6, out _, out var zeroHReason));
        Assert.Contains("step size", zeroHReason);
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, validD2, -1e-5, 1e-6, out _, out _));
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, validD2, double.NaN, 1e-6, out _, out _));

        // 7. Negative or non-finite tolerance -> rejects
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, validD2, 1e-5, -1e-6, out _, out var negTolReason));
        Assert.Contains("tolerance", negTolReason);
        Assert.False(CaseFValidator.ValidateFiniteDifferenceD2(validD1Plus, validD1Minus, validD2, 1e-5, double.NaN, out _, out _));
    }
}

