// Review proposal for 5c7e8bf26bc2d1d9388629f7fc62dd2597ab4e0c.
// The reviewer has not compiled or run this C# file.
using System;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class Icurve5c7ReviewRegressionTests
{
    private const double Shift = 4503599627370496.0; // 2^52
    private static readonly KernelVector3 Axis = Vector(3, 4, 0);

    private static void Fixture(bool translated, bool far,
        out KernelVector3 point, out KernelVector3 profile, out KernelVector3 axisPoint)
    {
        var offset = translated ? 4.0 : 0.0;
        // Translate the entire previous exact quarter-turn fixture by (4,4,0).
        // Both near/far axis points are exactly representable; their difference
        // is Shift * Axis, also exact. No rounding of input geometry is assumed away.
        point = Vector(offset, offset, 5);
        profile = Vector(offset - 4, offset + 3, 0);
        axisPoint = far
            ? Vector(3 * Shift + offset, 4 * Shift + offset, 0)
            : Vector(offset, offset, 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Spun_ExactTranslatedAxis_MustPreserveConstraints(bool translated, bool far)
    {
        Fixture(translated, far, out var point, out var profile, out var axisPoint);
        var status = SweptSpunImplicit.SpunConstraints(
            in point, in profile, in Axis, in axisPoint, in Axis,
            out var axial, out var radial, out var axialGradient,
            out var radialGradient, out var axialDu, out var radialDu);

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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Spun_ExactTranslatedAxis_MustRecoverQuarterTurn(bool translated, bool far)
    {
        Fixture(translated, far, out var point, out var profile, out var axisPoint);
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in point, in profile, in axisPoint, in Axis, Math.PI / 2, out var angle));
        Assert.True(double.IsFinite(angle));
        Assert.InRange(Math.Abs(angle - Math.PI / 2), 0, 1e-10);
    }

    [Theory]
    [InlineData(10000000000000.0)]
    [InlineData(9007199254740992.0)]
    public void Spun_ExactZAxisRepair_RemainsSuccessful(double z)
    {
        var a = Vector(0, 0, 1);
        var x = Vector(0, 1, z);
        var c = Vector(1, 0, z);
        var origin = Vector(0, 0, 0);
        Assert.Equal(AlgorithmStatus.Success, SweptSpunImplicit.SpunConstraints(
            in x, in c, in a, in origin, in a,
            out var h1, out var h2, out _, out _, out _, out _));
        Assert.Equal(0.0, h1);
        Assert.Equal(0.0, h2);
        Assert.True(SweptSpunImplicit.TryRecoverSpunAngle(
            in x, in c, in origin, in a, Math.PI / 2, out var angle));
        Assert.InRange(Math.Abs(angle - Math.PI / 2), 0, 1e-12);
    }
}
