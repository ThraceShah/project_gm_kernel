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

public class IcurveFourteenthReviewRegressionTests
{
    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [Fact]
    public void Terminator_NearTangentCylinder_ForwardErrorSatisfied()
    {
        // Counterexample from review 4:
        // Unit cylinder x^2 + y^2 = 1 with near-tangent search direction (slope ~ 1e-6).
        // B = (1, 0, 0), E = (-0.9999999999995, 9.999999999998333e-7, 0).
        // Parameter t = 1e-12.
        var cylSurf = new AnalyticSurface(SurfaceClass.Cylinder,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 1.0, 0.0);

        var b = Vector(1, 0, 0);
        var e = Vector(-0.9999999999995, 9.999999999998333e-7, 0);

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
        var targetT = 1e-12;
        var status = TerminatorEvaluation.SolveIntervalPoint(in cylSurf, in anchor, targetT,
            ref budget, out var mu, out var point, out var residual, out var evaluations);

        Assert.Equal(AlgorithmStatus.Success, status);

        // High-precision reference solution: Y ~ 1.561552812808438e-6, X ~ 0.9999999999987808
        // Old code prematurely terminated at Y ~ 3.999999999999667e-6 (error ~ 2.44e-6).
        var expectedY = 1.561552812808438e-6;
        var expectedX = 0.9999999999987808;
        var errorY = Math.Abs(point.Y - expectedY);
        var errorX = Math.Abs(point.X - expectedX);

        Assert.True(errorY < 1e-9, $"Y error {errorY} exceeds 1e-9 (expected {expectedY}, got {point.Y})");
        Assert.True(errorX < 1e-9, $"X error {errorX} exceeds 1e-9 (expected {expectedX}, got {point.X})");
        Assert.Equal(0.0, point.Z);

        // Geometric deviation must also be tightly satisfied
        Assert.Equal(AlgorithmStatus.Success,
            AnalyticImplicitEvaluation.GeometricDeviation(in cylSurf, in point, out var dev));
        Assert.True(dev < 1e-9, $"Geometric deviation {dev} exceeds 1e-9");
    }

    [Fact]
    public void Terminator_BudgetAccounting_ExplicitChainStepByStep()
    {
        // Linear plane z=0, chord along X from (0,0,0) to (1,0,0), target t=0.5.
        // Step-by-step budget chain:
        // 1. Branch jet at B (1)
        // 2. Newton probe at Q(0.5) (2)
        // 3. Midpoint deviation at xMid (3)
        // 4. Final publication deviation at xCurr (4)
        var plane = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 0, 0);

        var b = Vector(0, 0, 0);
        var e = Vector(1, 0, 0);
        var planeNormal = Vector(0, 1, 0);
        var chordUnit = Vector(1, 0, 0);
        var line = Vector(0, 0, 1);
        var chordRate = Scale(Sub(e, b), 1.0);
        var chordLen = 1.0;
        var anchor = new TerminatorEvaluation.TerminatorAnchor(0,
            in e, in b, in planeNormal, in chordUnit, in line, in chordRate,
            1.0, 0.0, chordLen);

        // Budget 0: halts before branch jet at B
        var b0 = new EvaluationBudget(0);
        var s0 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b0, out _, out _, out _, out var ev0);
        Assert.Equal(AlgorithmStatus.NotConverged, s0);
        Assert.Equal(0, ev0);
        Assert.Equal(0, b0.Used);

        // Budget 1: consumes branch jet at B, halts before Newton probe
        var b1 = new EvaluationBudget(1);
        var s1 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b1, out _, out _, out _, out var ev1);
        Assert.Equal(AlgorithmStatus.NotConverged, s1);
        Assert.Equal(1, ev1);
        Assert.Equal(1, b1.Used);

        // Budget 2: consumes branch jet and Newton probe, halts before midpoint deviation
        var b2 = new EvaluationBudget(2);
        var s2 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b2, out _, out _, out _, out var ev2);
        Assert.Equal(AlgorithmStatus.NotConverged, s2);
        Assert.Equal(2, ev2);
        Assert.Equal(2, b2.Used);

        // Budget 3: consumes branch jet, Newton probe, midpoint deviation, halts before final publication deviation
        var b3 = new EvaluationBudget(3);
        var s3 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b3, out _, out _, out _, out var ev3);
        Assert.Equal(AlgorithmStatus.NotConverged, s3);
        Assert.Equal(3, ev3);
        Assert.Equal(3, b3.Used);

        // Budget 4: consumes all 4 required geometric evaluations -> Success
        var b4 = new EvaluationBudget(4);
        var s4 = TerminatorEvaluation.SolveIntervalPoint(in plane, in anchor, 0.5,
            ref b4, out var mu4, out var pt4, out _, out var ev4);
        Assert.Equal(AlgorithmStatus.Success, s4);
        Assert.Equal(4, ev4);
        Assert.Equal(4, b4.Used);
        Assert.Equal(0.5, pt4.X, 10);
        Assert.Equal(0.0, pt4.Y, 10);
        Assert.Equal(0.0, pt4.Z, 10);
    }
}
