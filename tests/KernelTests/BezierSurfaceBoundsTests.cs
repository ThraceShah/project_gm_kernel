using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

public class BezierSurfaceBoundsTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(true, -600)] [InlineData(true, 600)]
    public void RationalMultiSpanBoundsEncloseIndependentSplinePositionsAndJets(bool rational, int weightExponent)
    {
        var dim = rational ? 4 : 3; var poles = new double[4 * 3 * dim];
        for (var u = 0; u < 4; u++) for (var v = 0; v < 3; v++)
        {
            var at = (u * 3 + v) * dim; var w = rational ? Math.ScaleB(1 + .2 * u + .3 * v, weightExponent) : 1;
            poles[at] = (u * .4 + .1 * v) * w; poles[at + 1] = (v * .5 - .05 * u) * w;
            poles[at + 2] = (.3 * u * u - .2 * v * v + .15 * u * v) * w; if (rational) poles[at + 3] = w;
        }
        double[] uk = [0, 0, 0, .4, 1, 1, 1], vk = [0, 0, 0, 1, 1, 1];
        var view = new BSurfaceView(2, 2, 4, 3, dim, rational, false, false, poles, uk, vk);
        var extracted = new IntervalNumber[4 * 9]; var bound = new IntervalNumber[9];
        var workspace = new double[2048]; var jet = new KernelVector3[4]; SurfaceDerivativeLayout.TryCreate(1, 1, out var layout);
        for (var span = 2; span < 4; span++)
        {
            Assert.Equal(AlgorithmStatus.Success, BezierSurfaceBounds.Extract(view, span, 2, extracted));
            var domain = new SurfaceParameterBox(uk[span], uk[span + 1], 0, 1);
            var patch = new BezierSurfacePatch(extracted, 2, 2, domain);
            var box = new SurfaceParameterBox(domain.U0 + .13 * (domain.U1 - domain.U0), domain.U0 + .73 * (domain.U1 - domain.U0), .17, .81);
            Assert.Equal(AlgorithmStatus.Success, BezierSurfaceBounds.Bound(patch, box, bound));
            for (var i = 0; i <= 10; i++) for (var j = 0; j <= 10; j++)
            {
                var u = box.U0 + (box.U1 - box.U0) * i / 10; var v = box.V0 + (box.V1 - box.V0) * j / 10;
                Assert.Equal(AlgorithmStatus.Success, BSurfaceEvaluation.EvaluateInDomain(view, u, v, layout, workspace, jet));
                Check(bound.AsSpan(0, 3), jet[0]); Check(bound.AsSpan(3, 3), jet[layout.GetIndex(1, 0)]); Check(bound.AsSpan(6, 3), jet[layout.GetIndex(0, 1)]);
            }
        }
    }

    [Fact]
    public void PointAndFaceBoxesRetainFiniteDerivativeBounds()
    {
        double[] poles = [0, 0, 0, 0, 1, 0, 1, 0, 0, 1, 1, 1], knots = [0, 0, 1, 1];
        var view = new BSurfaceView(1, 1, 2, 2, 3, false, false, false, poles, knots, knots);
        var extracted = new IntervalNumber[16]; var bounds = new IntervalNumber[9];
        Assert.Equal(AlgorithmStatus.Success, BezierSurfaceBounds.Extract(view, 1, 1, extracted));
        var patch = new BezierSurfacePatch(extracted, 1, 1, new(0, 1, 0, 1));
        Assert.Equal(AlgorithmStatus.Success, BezierSurfaceBounds.Bound(patch, new(.3, .3, .7, .7), bounds));
        Check(bounds.AsSpan(0, 3), new() { X = .3, Y = .7, Z = .21 });
        Check(bounds.AsSpan(3, 3), new() { X = 1, Y = 0, Z = .7 });
        Check(bounds.AsSpan(6, 3), new() { X = 0, Y = 1, Z = .3 });
        Assert.All(bounds, value => Assert.True(value.Finite));
    }

    private static void Check(ReadOnlySpan<IntervalNumber> bounds, KernelVector3 vector)
    {
        Assert.InRange(vector.X, bounds[0].Lower, bounds[0].Upper);
        Assert.InRange(vector.Y, bounds[1].Lower, bounds[1].Upper);
        Assert.InRange(vector.Z, bounds[2].Lower, bounds[2].Upper);
    }
}
