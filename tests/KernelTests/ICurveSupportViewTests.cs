using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class ICurveSupportViewTests
{
    [Fact]
    public void StrictSupportEvaluation_RejectsLinearExtension()
    {
        var spline = PlanePatch();
        Span<double> work = stackalloc double[(int)BSurfaceEvaluation.WorkspaceSize(1, 1, 2, 2)];
        var support = new ICurveSupportView(spline, work);
        Span<KernelVector3> value = stackalloc KernelVector3[1];
        value[0] = Vector(11, 12, 13);
        SurfaceDerivativeLayout.TryCreate(0, 0, out var layout);
        Assert.Equal(AlgorithmStatus.InvalidInput, ICurveSupportEvaluation.Evaluate(in support, 1.1, 0.5, in layout, value));
        Assert.Equal(11, value[0].X);
        Assert.Equal(AlgorithmStatus.Success, BSurfaceEvaluation.Evaluate(in spline, 1.1, 0.5, in layout, work, value));
        Assert.Equal(1.1, value[0].X, 14);
    }

    [Fact]
    public void BSurfaceSeed_RequiresFiniteInDomainWitness()
    {
        var spline = PlanePatch();
        Span<double> work = stackalloc double[128];
        var support = new ICurveSupportView(spline, work);
        var point = Vector(0.2, 0.4, 0);
        Assert.False(ICurveSupportEvaluation.TryResolveUv(in support, in point, false, 0.2, 0.4, out _, out _));
        Assert.False(ICurveSupportEvaluation.TryResolveUv(in support, in point, true, double.NaN, 0.4, out _, out _));
        Assert.False(ICurveSupportEvaluation.TryResolveUv(in support, in point, true, 1.2, 0.4, out _, out _));
        Assert.True(ICurveSupportEvaluation.TryResolveUv(in support, in point, true, 0.2, 0.4, out var u, out var v));
        Assert.Equal(0.2, u);
        Assert.Equal(0.4, v);
    }

    [Fact]
    public void PeriodicWitnessLift_UsesKnotPeriod()
    {
        double[] vertices = [0,0,0, 0,1,0, 1,0,0, 1,1,0, 0,0,0, 0,1,0, 1,0,0, 1,1,0];
        double[] uKnots = [-4,-1,2,5,8,11,14];
        double[] vKnots = [0,0,1,1];
        var spline = new BSurfaceView(2, 1, 4, 2, 3, false, true, false, vertices, uKnots, vKnots);
        Span<double> work = stackalloc double[256];
        var support = new ICurveSupportView(spline, work);
        var u = 2.2; var v = 0.4;
        ICurveSupportEvaluation.UnwrapPeriodic(in support, ref u, ref v, 7.9, 4.0);
        Assert.Equal(8.2, u, 14);
        Assert.Equal(0.4, v);
        Assert.True(ICurveSupportEvaluation.IsInDomain(in support, u, v));
        Assert.False(ICurveSupportEvaluation.IsInDomain(in support, u, 1.1));
    }

    [Fact]
    public void RepeatedKnot_RefusesAmbiguousJetButKeepsPosition()
    {
        // Piecewise linear patch: the two u-tangents differ at u=0.5.
        double[] vertices = [0,0,0, 0,1,0, 0.5,0,0, 0.5,1,0, 1,0,1, 1,1,1];
        double[] uKnots = [0,0,0.5,1,1];
        double[] vKnots = [0,0,1,1];
        var spline = new BSurfaceView(1, 1, 3, 2, 3, false, false, false, vertices, uKnots, vKnots);
        Span<double> work = stackalloc double[128];
        var support = new ICurveSupportView(spline, work);
        Span<KernelVector3> output = stackalloc KernelVector3[9];
        SurfaceDerivativeLayout.TryCreate(0, 0, out var position);
        SurfaceDerivativeLayout.TryCreate(1, 1, out var first);
        SurfaceDerivativeLayout.TryCreate(2, 2, out var second);
        Assert.Equal(AlgorithmStatus.Success, ICurveSupportEvaluation.Evaluate(in support, 0.5, 0.4, in position, output));
        Assert.Equal(0.5, output[0].X, 14);
        Assert.Equal(AlgorithmStatus.Unsupported, ICurveSupportEvaluation.Evaluate(in support, 0.5, 0.4, in first, output));
        Assert.Equal(AlgorithmStatus.Unsupported, ICurveSupportEvaluation.Evaluate(in support, Math.BitDecrement(0.5), 0.4, in first, output));
        Assert.Equal(AlgorithmStatus.Unsupported, ICurveSupportEvaluation.Evaluate(in support, 0.5, 0.4, in second, output));
        Assert.Equal(AlgorithmStatus.Success, ICurveSupportEvaluation.Evaluate(in support, 0, 0.4, in second, output));
        Assert.Equal(AlgorithmStatus.Success, ICurveSupportEvaluation.Evaluate(in support, 1, 0.4, in second, output));
    }

    [Fact]
    public void NearbyKnots_CheckContinuityOfTheActuallySelectedRightSpan()
    {
        const double a = 0.5, b = 0.500000000005;
        double[] knots = [0,0,0,0,a,b,b,b,1,1,1,1];
        var vertices = new double[8 * 2 * 3];
        for (var i = 0; i < 8; i++)
        {
            var x = (knots[i + 1] + knots[i + 2] + knots[i + 3]) / 3;
            for (var j = 0; j < 2; j++)
            {
                var offset = (2 * i + j) * 3;
                vertices[offset] = x;
                vertices[offset + 1] = j;
                vertices[offset + 2] = Math.Max(0, x - b);
            }
        }
        var spline = new BSurfaceView(3, 1, 8, 2, 3, false, false, false,
            vertices, knots, new double[] { 0,0,1,1 });
        Span<double> work = stackalloc double[256];
        var support = new ICurveSupportView(spline, work);
        Span<KernelVector3> output = stackalloc KernelVector3[9];
        SurfaceDerivativeLayout.TryCreate(1, 1, out var first);
        SurfaceDerivativeLayout.TryCreate(2, 2, out var second);
        // The existing surface evaluator resolves a to the right of b.
        Assert.Equal(AlgorithmStatus.Success,
            BSurfaceEvaluation.EvaluateInDomain(in spline, a, 0.4, in first, work, output));
        Assert.InRange(output[first.GetIndex(1, 0)].Z, 0.999999999, 1.000000001);
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveSupportEvaluation.Evaluate(in support, a, 0.4, in first, output));
        Assert.Equal(AlgorithmStatus.Unsupported,
            ICurveSupportEvaluation.Evaluate(in support, a, 0.4, in second, output));
    }

    [Fact]
    public void DomainEndDoesNotHideAnInteriorKnotInsideTheResolutionWindow()
    {
        foreach (var nearStart in new[] { true, false })
        {
            var kink = nearStart ? 5e-12 : 1 - 5e-12;
            double[] knots = [0,0,0,0,kink,kink,kink,1,1,1,1];
            var vertices = new double[7 * 2 * 3];
            for (var i = 0; i < 7; i++)
            for (var j = 0; j < 2; j++)
            {
                var offset = (2 * i + j) * 3;
                vertices[offset] = (knots[i + 1] + knots[i + 2] + knots[i + 3]) / 3;
                vertices[offset + 1] = j;
                vertices[offset + 2] = Math.Max(0, vertices[offset] - kink);
            }
            var spline = new BSurfaceView(3, 1, 7, 2, 3, false, false, false,
                vertices, knots, new double[] { 0,0,1,1 });
            var support = new ICurveSupportView(spline, new double[256]);
            var output = new KernelVector3[4];
            SurfaceDerivativeLayout.TryCreate(1, 1, out var first);
            Assert.Equal(AlgorithmStatus.Unsupported, ICurveSupportEvaluation.Evaluate(
                in support, nearStart ? 0 : kink, 0.4, in first, output));
            Assert.Equal(AlgorithmStatus.Success,
                ICurveSupportEvaluation.Evaluate(in support, 1, 0.4, in first, output));
        }
    }

    [Fact]
    public void UnitNormal_PreservesParameterOrientation()
    {
        var spline = PlanePatch();
        Span<double> work = stackalloc double[128];
        var support = new ICurveSupportView(spline, work);
        var point = Vector(0.2, 0.4, 0);
        Assert.True(ICurveSupportEvaluation.TryNormal(in support, 0.2, 0.4, in point, out var normal));
        Assert.Equal(0, normal.X, 14);
        Assert.Equal(0, normal.Y, 14);
        Assert.Equal(1, normal.Z, 14);
    }

    [Fact]
    public void ControlNetScale_IsTranslationInvariantAndDehomogenized()
    {
        double[] vertices = [20,40,60,2, 30,63,90,3, 44,80,120,4, 55,105,150,5];
        double[] knots = [0,0,1,1];
        var rational = new BSurfaceView(1, 1, 2, 2, 4, true, false, false, vertices, knots, knots);
        Span<double> work = stackalloc double[128];
        var support = new ICurveSupportView(rational, work);
        Assert.Equal(Math.Sqrt(2), support.LengthScale, 14);
        var ordinary = PlanePatch();
        var ordinarySupport = new ICurveSupportView(ordinary, work);
        Assert.Equal(ordinarySupport.LengthScale, support.LengthScale, 14);
    }

    [Fact]
    public void ControlNetScale_DoesNotSquareHugeOrTinyDimensions()
    {
        double[] knots = [0,0,1,1];
        foreach (var size in new[] { 1e200, 1e-320 })
        {
            double[] vertices = [0,0,0, 0,size,0, size,0,0, size,size,0];
            var spline = new BSurfaceView(1, 1, 2, 2, 3, false, false, false, vertices, knots, knots);
            var support = new ICurveSupportView(spline, Span<double>.Empty);
            Assert.True(double.IsFinite(support.LengthScale));
            Assert.InRange(support.LengthScale / Math.Max(1, size * Math.Sqrt(2)), 0.99999999999999, 1.00000000000001);
        }
    }

    private static BSurfaceView PlanePatch()
    {
        double[] vertices = [0,0,0, 0,1,0, 1,0,0, 1,1,0];
        double[] knots = [0,0,1,1];
        return new BSurfaceView(1, 1, 2, 2, 3, false, false, false, vertices, knots, knots);
    }
}
