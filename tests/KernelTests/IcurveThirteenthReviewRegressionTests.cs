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

public class IcurveThirteenthReviewRegressionTests
{
    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [Fact]
    public void Terminator_RingTorus_MustNotJumpBranches()
    {
        // Counterexample from review 3:
        // Torus R=1.01, r=1.0 with x=0 cross-section containing two disjoint circles (y-1.01)^2+z^2=1 and (y+1.01)^2+z^2=1.
        // Curve starts and ends on positive Y circle.
        var torusSurf = new AnalyticSurface(SurfaceClass.Torus,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.01, 1.0);

        var b = Vector(0, 0.9808004776987111, 0.9995736030415051);
        var e = Vector(0, 0.9976113365371094, -0.9999232575641008);

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
        var targetT = 0.5;
        var status = TerminatorEvaluation.SolveIntervalPoint(in torusSurf, in anchor, targetT,
            ref budget, out var mu, out var point, out var residual, out var evaluations);

        Assert.Equal(AlgorithmStatus.Success, status);
        // Correct root is on the positive Y branch: approx (0, 0.010035, -0.008407)
        Assert.True(point.Y > 0.0, $"Expected positive Y on original branch, but got Y={point.Y}");
        Assert.True(Math.Abs(point.X) < 1e-12, $"Expected X=0, but got X={point.X}");
        Assert.InRange(point.Y, 0.005, 0.02);
        Assert.InRange(point.Z, -0.02, 0.0);

        // Verify geometric deviation is strictly satisfied
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.GeometricDeviation(in torusSurf, in point, out var dev));
        Assert.True(dev < 1e-9, $"Geometric deviation too large: {dev}");
    }

    [Fact]
    public void Terminator_Cylinder_LargeCoordinate_ResidualToleranceDecoupled()
    {
        // Counterexample from review 3:
        // Cylinder x^2 + y^2 = 1 with large axial coordinate L = 1e13.
        var L = 1e13;
        var cylSurf = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.0);

        var b = Vector(1, 0, L);
        var e = Vector(0, 1, L);

        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in cylSurf, in e, 1, out var endpointJet));
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.Evaluate(in cylSurf, in b, 1, out var branchJet));
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
        var targetT = 0.5;
        var status = TerminatorEvaluation.SolveIntervalPoint(in cylSurf, in anchor, targetT,
            ref budget, out var mu, out var point, out var residual, out var evaluations);

        Assert.Equal(AlgorithmStatus.Success, status);
        // Correct root is (1/sqrt(2), 1/sqrt(2), L)
        var expectedCoord = 1.0 / Math.Sqrt(2.0);
        Assert.True(Math.Abs(point.X - expectedCoord) < 1e-9, $"Expected X ~ {expectedCoord}, got {point.X}");
        Assert.True(Math.Abs(point.Y - expectedCoord) < 1e-9, $"Expected Y ~ {expectedCoord}, got {point.Y}");
        Assert.Equal(L, point.Z);

        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.GeometricDeviation(in cylSurf, in point, out var dev));
        Assert.True(dev < 1e-9, $"Geometric deviation too large: {dev}");
    }

    [Fact]
    public void Svd_SubnormalNonDiagonal_PreservesUOrthogonality()
    {
        // Counterexample from review 3:
        // A = 1e-320 * [1, 1; 1, -1] has orthogonal columns, cond=1.
        var a = new double[]
        {
            1e-320, 1e-320,
            1e-320, -1e-320
        };
        var s = new double[2];
        var u = new double[4];
        var v = new double[4];

        var status = SmallLinearSolve.SvdFactorizeSquare(a, 2, s, u, v, 1e-12, out var rank);
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.Equal(2, rank);

        // Check U^T * U ~ I to machine precision
        var utu00 = u[0] * u[0] + u[2] * u[2];
        var utu01 = u[0] * u[1] + u[2] * u[3];
        var utu10 = u[1] * u[0] + u[3] * u[2];
        var utu11 = u[1] * u[1] + u[3] * u[3];

        Assert.True(Math.Abs(utu00 - 1.0) < 1e-14, $"|U^T U[0,0] - 1| = {Math.Abs(utu00 - 1.0)}");
        Assert.True(Math.Abs(utu01) < 1e-14, $"|U^T U[0,1]| = {Math.Abs(utu01)}");
        Assert.True(Math.Abs(utu10) < 1e-14, $"|U^T U[1,0]| = {Math.Abs(utu10)}");
        Assert.True(Math.Abs(utu11 - 1.0) < 1e-14, $"|U^T U[1,1] - 1| = {Math.Abs(utu11 - 1.0)}");

        // Check V^T * V ~ I to machine precision
        var vtv00 = v[0] * v[0] + v[2] * v[2];
        var vtv01 = v[0] * v[1] + v[2] * v[3];
        var vtv10 = v[1] * v[0] + v[3] * v[2];
        var vtv11 = v[1] * v[1] + v[3] * v[3];

        Assert.True(Math.Abs(vtv00 - 1.0) < 1e-14, $"|V^T V[0,0] - 1| = {Math.Abs(vtv00 - 1.0)}");
        Assert.True(Math.Abs(vtv01) < 1e-14, $"|V^T V[0,1]| = {Math.Abs(vtv01)}");
        Assert.True(Math.Abs(vtv10) < 1e-14, $"|V^T V[1,0]| = {Math.Abs(vtv10)}");
        Assert.True(Math.Abs(vtv11 - 1.0) < 1e-14, $"|V^T V[1,1] - 1| = {Math.Abs(vtv11 - 1.0)}");
    }
}
