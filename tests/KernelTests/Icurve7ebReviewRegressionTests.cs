// Regression proposal for commit 7eb2df2399468205ee3ea51efd2e4ed17dabbfbc.
// This file has NOT been compiled or run by the reviewer.
using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class Icurve7ebReviewRegressionTests
{
    private static readonly KernelVector3 Axis = Vector(3, 4, 0);
    private static readonly KernelVector3 Point = Vector(0, 0, 5);
    private static readonly KernelVector3 Profile = Vector(-4, 3, 0);
    private static readonly KernelVector3 ProfileDerivative = Vector(3, 4, 0);
    private const double Shift = 4503599627370496.0; // 2^52, exactly representable

    private static KernelVector3 AxisPoint(bool far)
        => far ? Vector(3 * Shift, 4 * Shift, 0) : Vector(0, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spun_TiltedSameAxis_MustPreserveResidualAndGradient(bool far)
    {
        // C(u)=(-4,3,0)+u(3,4,0) revolves into a regular radius-5 cylinder.
        // At u=0, Point is the positive pi/2 rotation of Profile about Axis.
        // The two axis reference points differ by Shift * Axis exactly.
        var axisPoint = AxisPoint(far);
        var status = SweptSpunImplicit.SpunConstraints(
            in Point, in Profile, in ProfileDerivative, in axisPoint, in Axis,
            out var axial, out var radial,
            out var axialGradient, out var radialGradient,
            out var axialDu, out var radialDu);

        // This test requests a stable successful evaluation of this exact
        // regular fixture; a temporary NumericalFailure policy is safer than
        // wrong data but does not fulfill that capability requirement.
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.InRange(Math.Abs(axial), 0, 1e-12);
        Assert.InRange(Math.Abs(radial), 0, 1e-10);
        Assert.InRange(Math.Abs(axialGradient.X - 0.6), 0, 1e-14);
        Assert.InRange(Math.Abs(axialGradient.Y - 0.8), 0, 1e-14);
        Assert.InRange(Math.Abs(axialGradient.Z), 0, 1e-14);
        Assert.InRange(Math.Abs(radialGradient.X), 0, 1e-10);
        Assert.InRange(Math.Abs(radialGradient.Y), 0, 1e-10);
        Assert.InRange(Math.Abs(radialGradient.Z - 10), 0, 1e-10);
        Assert.InRange(Math.Abs(axialDu + 5), 0, 1e-12);
        Assert.InRange(Math.Abs(radialDu), 0, 1e-10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spun_TiltedSameAxis_MustRecoverPositiveQuarterTurn(bool far)
    {
        var axisPoint = AxisPoint(far);
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in Point, in Profile, in axisPoint, in Axis, Math.PI / 2, out var angle));
        Assert.True(double.IsFinite(angle));
        Assert.InRange(Math.Abs(angle - Math.PI / 2), 0, 1e-10);
    }

    [Theory]
    [InlineData(10000000000000.0)]
    [InlineData(9007199254740992.0)]
    public void Spun_RepairedExactZAxis_MustRemainSuccessful(double axial)
    {
        var axis = Vector(0, 0, 1);
        var point = Vector(0, 1, axial);
        var profile = Vector(1, 0, axial);
        var du = Vector(0, 0, 1);
        var origin = Vector(0, 0, 0);
        var shifted = Vector(0, 0, axial);
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in point, in profile, in du, in origin, in axis,
            out var a0, out var r0, out _, out _, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in point, in profile, in du, in shifted, in axis,
            out var a1, out var r1, out _, out _, out _, out _));
        Assert.Equal(0.0, a0);
        Assert.Equal(0.0, r0);
        Assert.Equal(a0, a1);
        Assert.Equal(r0, r1);
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in point, in profile, in origin, in axis, Math.PI / 2, out var angle));
        Assert.InRange(Math.Abs(angle - Math.PI / 2), 0, 1e-12);
    }
}
