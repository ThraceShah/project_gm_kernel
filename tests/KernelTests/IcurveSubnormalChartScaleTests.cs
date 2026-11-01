using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class IcurveSubnormalChartScaleTests
{
    [Theory]
    [InlineData(-1074, 1, 1.0 / 6.0)]
    [InlineData(-1074, 2, -1.0 / 6.0)]
    [InlineData(-1073, 1, 1.0 / 6.0)]
    [InlineData(-1022, 1, 1.0 / 6.0)]
    [InlineData(0, 1, 1.0 / 6.0)]
    [InlineData(-1074, 0, 0.0)]
    public void PlaneResidual_PreservesDivisionRemainderAcrossParameterScales(
        int unitExponent, int numeratorOffset, double expectedResidualAtOrigin)
    {
        var view = LineView(unitExponent, numeratorOffset, out var parameter);
        var origin = Vector(0, 0, 0);
        var root = Vector(0, 0, -expectedResidualAtOrigin);

        var residualAtOrigin = OriginalChartParameterMap.PlaneResidual(
            view.ChartPositions, view.ChartParameters, view.ChartScales,
            view.ChartChordUnits, 0, parameter, in origin);
        var residualAtRoot = OriginalChartParameterMap.PlaneResidual(
            view.ChartPositions, view.ChartParameters, view.ChartScales,
            view.ChartChordUnits, 0, parameter, in root);

        Assert.InRange(Math.Abs(residualAtOrigin - expectedResidualAtOrigin), 0, 1e-15);
        Assert.InRange(Math.Abs(residualAtRoot), 0, 1e-15);
    }

    [Fact]
    public void PlaneResidual_SubnormalScale_PreservesLowPartOfParameterDifference()
    {
        ReadOnlySpan<KernelVector3> positions = stackalloc KernelVector3[1] { Vector(0, 0, 0) };
        ReadOnlySpan<double> parameters = stackalloc double[1] { -double.Epsilon };
        ReadOnlySpan<double> scales = stackalloc double[1] { 3 * double.Epsilon };
        ReadOnlySpan<KernelVector3> chordUnits = stackalloc KernelVector3[1] { Vector(0, 0, 1) };
        var parameter = Math.ScaleB(3.0, -100);
        var point = Vector(0, 0, Math.ScaleB(1.0, 974));

        // (t - t0)/f = 2^974 + 1/3 exactly. The +1/3 comes entirely
        // from the low part of t - t0, despite the enormous high part.
        var residual = OriginalChartParameterMap.PlaneResidual(
            positions, parameters, scales, chordUnits, 0, parameter, in point);

        Assert.InRange(Math.Abs(residual + 1.0 / 3.0), 0, 1e-15);
    }

    [Fact]
    public void PlaneResidual_SubnormalScale_HandlesFiniteQuotientNearDoubleMaxValue()
    {
        ReadOnlySpan<KernelVector3> positions = stackalloc KernelVector3[1] { Vector(0, 0, 0) };
        ReadOnlySpan<double> parameters = stackalloc double[1] { 0 };
        ReadOnlySpan<double> scales = stackalloc double[1] { 3 * double.Epsilon };
        ReadOnlySpan<KernelVector3> chordUnits = stackalloc KernelVector3[1] { Vector(0, 0, 1) };
        var parameter = Math.BitDecrement(Math.ScaleB(3.0, -50));
        var point = Vector(0, 0, double.MaxValue);

        // t/f = 2^1024 - 2^973/3 and MaxValue = 2^1024 - 2^971,
        // hence the finite residual is 2^971/3. Neither the quotient
        // correction nor its intermediate normalization may overflow.
        var expectedResidual = Math.ScaleB(1.0, 971) / 3;
        var residual = OriginalChartParameterMap.PlaneResidual(
            positions, parameters, scales, chordUnits, 0, parameter, in point);

        Assert.InRange(Math.Abs((residual - expectedResidual) / expectedResidual), 0, 1e-14);
    }

    [Theory]
    [InlineData(1, -1.0 / 6.0)]
    [InlineData(2, 1.0 / 6.0)]
    public void IsPublishableRoot_RejectsOriginWhenSubnormalDivisionLosesRemainder(
        int numeratorOffset, double expectedRootZ)
    {
        var view = LineView(-1074, numeratorOffset, out var parameter);
        var origin = Vector(0, 0, 0);
        var root = Vector(0, 0, expectedRootZ);

        // Both points are on both support planes. Only the original chart
        // equation distinguishes the true root from the rounded quotient.
        Assert.False(ICurveEvaluation.IsPublishableRoot(in view, parameter, 0, in origin));
        Assert.True(ICurveEvaluation.IsPublishableRoot(in view, parameter, 0, in root));
    }

    [Theory]
    [InlineData(-1074, 1, -1.0 / 6.0)]
    [InlineData(-1074, 2, 1.0 / 6.0)]
    [InlineData(-1073, 1, -1.0 / 6.0)]
    [InlineData(-1022, 1, -1.0 / 6.0)]
    [InlineData(0, 1, -1.0 / 6.0)]
    [InlineData(-1074, 0, 0.0)]
    public void EvaluateWithPlan_I2D0_ReturnsAnalyticLineRoot(
        int unitExponent, int numeratorOffset, double expectedRootZ)
    {
        var view = LineView(unitExponent, numeratorOffset, out var parameter);
        Span<KernelVector3> derivatives = stackalloc KernelVector3[1];

        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, parameter, 0, ICurveConstraintPlan.I2, derivatives, out var report);

        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.Equal(ICurveConstraintPlan.I2, report.Plan);
        Assert.InRange(Math.Abs(derivatives[0].X), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].Y), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].Z - expectedRootZ), 0, 1e-12);
    }

    [Theory]
    [InlineData(1, -1.0 / 6.0)]
    [InlineData(2, 1.0 / 6.0)]
    public void SolveDirect_I1D0_ReturnsAnalyticCylinderGeneratorRoot(
        int numeratorOffset, double expectedRootZ)
    {
        var view = LineView(-1074, numeratorOffset, out var parameter, cylinderSupport: true);
        var seed = Vector(0, 1, 0);
        Span<KernelVector3> derivatives = stackalloc KernelVector3[1];

        // The public I1 entry tracks from the nearest chart endpoint. Here
        // that span is one or two double.Epsilon, so its quarter-step rounds
        // to zero. Test the D0 solver without that existing continuation limit.
        var status = ICurveEvaluation.SolveDirect(in view, in seed, parameter, 0, 0,
            ICurveConstraintPlan.I1, derivatives, out _, out var residual);

        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.InRange(residual, 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].X), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].Y - 1.0), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].Z - expectedRootZ), 0, 1e-12);
    }

    [Theory]
    [InlineData(1, -1.0 / 6.0)]
    [InlineData(2, 1.0 / 6.0)]
    public void EvaluateWithPlan_I1D0_OrdinaryScaleReturnsAnalyticCylinderGeneratorRoot(
        int numeratorOffset, double expectedRootZ)
    {
        var view = LineView(0, numeratorOffset, out var parameter, cylinderSupport: true);
        Span<KernelVector3> derivatives = stackalloc KernelVector3[1];

        var status = ICurveEvaluation.EvaluateWithPlan(
            in view, parameter, 0, ICurveConstraintPlan.I1, derivatives, out var report);

        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.Equal(ICurveConstraintPlan.I1, report.Plan);
        Assert.InRange(Math.Abs(derivatives[0].X), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].Y - 1.0), 0, 1e-12);
        Assert.InRange(Math.Abs(derivatives[0].Z - expectedRootZ), 0, 1e-12);
    }

    [Theory]
    [InlineData((int)ICurveConstraintPlan.I1, 1)]
    [InlineData((int)ICurveConstraintPlan.I1, 2)]
    [InlineData((int)ICurveConstraintPlan.I2, 1)]
    [InlineData((int)ICurveConstraintPlan.I2, 2)]
    public void Refine_EliminatedPlan_UsesOriginalParameterPlane(int planValue, int numeratorOffset)
    {
        var plan = (ICurveConstraintPlan)planValue;
        var cylinderSupport = plan == ICurveConstraintPlan.I1;
        var view = LineView(-1074, numeratorOffset, out var parameter, cylinderSupport);
        var seed = Vector(0, cylinderSupport ? 1.0 : 0.0, 0);
        Span<double> refinedState = stackalloc double[4];

        // The seed satisfies both supports but misses the parameter plane
        // by 1/6. The fallback must correct that plane before eliminating it;
        // its remaining in-plane unknowns cannot repair the z coordinate.
        var status = ICurveCorrection.Refine(in view, plan, parameter, 0, in seed,
            refinedState, out _, out var residual);

        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.InRange(residual, 0, 1e-12);
    }

    private static ICurveView LineView(int unitExponent, int numeratorOffset, out double parameter,
        bool cylinderSupport = false)
    {
        // Let N=2^51 and u=2^unitExponent. For f=3u and t=(3N+k)u,
        // the exact quotient is N+k/3. With k=1 or 2 it rounds to N+1/2.
        // P0.z=-round(t/f), so p(z,t)=z+1/2-k/3: the exact roots are
        // -1/6 and +1/6, independent of u. At u=double.Epsilon the
        // division remainder is +/-u/2 and cannot be stored as a double.
        // k=0 is the exactly divisible control, whose root is zero.
        var unit = Math.ScaleB(1.0, unitExponent);
        var n = Math.ScaleB(1.0, 51);
        var scale = 3 * unit;
        parameter = (3 * n + numeratorOffset) * unit;
        var roundedQuotient = parameter / scale;
        Assert.Equal(n + (numeratorOffset == 0 ? 0.0 : 0.5), roundedQuotient);

        var y = cylinderSupport ? 1.0 : 0.0;
        KernelVector3[] positions = [Vector(0, y, -roundedQuotient), Vector(0, y, 0.75)];
        KernelVector3[] tangents = [Vector(0, 0, 1), Vector(0, 0, 1)];
        double[] parameters = new double[2];
        double[] scales = new double[1];
        KernelVector3[] chordUnits = new KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, OriginalChartParameterMap.Build(
            positions, tangents, 0, scale, parameters, scales, chordUnits, out _, out _));
        Assert.Equal(1.0, chordUnits[0].Z);
        Assert.True(parameters[0] < parameter && parameter < parameters[1]);

        var origin = Vector(0, 0, 0);
        var planeX = new AnalyticSurface(SurfaceClass.Plane, origin, Vector(1, 0, 0), Vector(0, 1, 0));
        var support1 = cylinderSupport
            ? new AnalyticSurface(SurfaceClass.Cylinder, origin, Vector(0, 0, 1), Vector(1, 0, 0), 1.0)
            : new AnalyticSurface(SurfaceClass.Plane, origin, Vector(0, 1, 0), Vector(0, 0, 1));
        return new ICurveView(in planeX, 1, in support1, 1,
            positions, parameters, scales, chordUnits);
    }
}
