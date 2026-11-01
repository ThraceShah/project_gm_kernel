using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

public class ParametricICurveBranchTests
{
    private const double Separation = 1e-7;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DifferentUvBranchesWithinPhysicalTolerance_AreNotMerged(int planCode)
    {
        var fixture = new CloseBranches();
        var view = fixture.View(0, 2 * Separation);
        var sentinel = Vector(17, 23, 31);
        Span<KernelVector3> output = stackalloc KernelVector3[3] { sentinel, sentinel, sentinel };
        var cache = new EvaluationSampleStore(new CurveSample[4]);

        var status = ICurveEvaluation.EvaluateWithCache(in view, 0.5, 2,
            (ICurveConstraintPlan)planCode, ref cache, output, out var report);

        Assert.Equal(AlgorithmStatus.NotConverged, status);
        Assert.Equal(ICurveEvalDetail.AmbiguousBranch, report.Detail);
        Assert.Equal(0, cache.Count);
        foreach (var value in output)
        {
            Assert.Equal(sentinel.X, value.X);
            Assert.Equal(sentinel.Y, value.Y);
            Assert.Equal(sentinel.Z, value.Z);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void EachCloseBranchWithItsOwnCoherentChart_StillSucceeds(int planCode, bool upperBranch)
    {
        var fixture = new CloseBranches();
        var u = upperBranch ? 2 * Separation : 0;
        var view = fixture.View(u, u);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        var cache = new EvaluationSampleStore(new CurveSample[4]);

        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(in view, 0.5, 2,
            (ICurveConstraintPlan)planCode, ref cache, output, out _));
        Assert.InRange(Math.Abs(output[0].X - Separation * u), 0, 1e-24);
        Assert.InRange(Math.Abs(output[0].Y - 0.5), 0, 1e-14);
        Assert.InRange(Math.Abs(output[0].Z), 0, 1e-24);
        Assert.InRange(Math.Abs(output[1].X), 0, 1e-24);
        Assert.InRange(Math.Abs(output[1].Y - 1), 0, 1e-14);
        Assert.InRange(Math.Abs(output[1].Z), 0, 1e-24);
        Assert.InRange(Math.Abs(output[2].X) + Math.Abs(output[2].Y) + Math.Abs(output[2].Z), 0, 1e-24);
        Assert.True(cache.TryFindExact(0.5, ICurveQueryKind.RegularChartInterval,
            ChartSide.Right, 2, double.PositiveInfinity, out var sample, plan: (ICurveConstraintPlan)planCode));
        Assert.InRange(Math.Abs(sample.Witness.U0 - u), 0, 1e-17);
    }

    [Fact]
    public void LargeUvOriginCannotWidenPreimageToleranceAcrossItsKnotCell()
    {
        const double origin = 1e9;
        var end = origin + 1e-5;
        double[] vertices = [0, -1, 0, 0, 1, 0, 0.5, -1, 0, 0.5, 1, 0, 1, -1, 1, 1, 1, 1];
        var bs = new BSurfaceView(2, 1, 3, 2, 3, false, false, false, vertices,
            new double[] { origin, origin, origin, end, end, end }, new double[] { -1, -1, 1, 1 });
        var support = new ICurveSupportView(bs, new double[(int)BSurfaceEvaluation.WorkspaceSize(2, 1, 2, 2)]);
        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 1, 0), Vector(1, 0, 0));
        var other = new ICurveSupportView(in plane);
        var chord = Unit(Vector(1, 0, 1));
        var view = new ICurveView(support, ParasolidConstants.PK_TOPOL_sense_positive_c,
            other, ParasolidConstants.PK_TOPOL_sense_positive_c,
            new KernelVector3[] { Vector(0, 0, 0), Vector(1, 0, 1) },
            new double[] { 0, Math.Sqrt(2) }, new double[] { 1 }, new KernelVector3[] { chord },
            new SampleWitness[] { new(origin, 0, 0, 0, 0, 3), new(end, 0, 1, -1, 0, 3) });
        var sentinel = Vector(17, 23, 31);
        Span<KernelVector3> output = stackalloc KernelVector3[3] { sentinel, sentinel, sentinel };
        var cache = new EvaluationSampleStore(new CurveSample[4]);

        Assert.Equal(AlgorithmStatus.NotConverged, ICurveEvaluation.EvaluateWithCache(
            in view, 0.5 * Math.Sqrt(2), 2, ICurveConstraintPlan.P4, ref cache, output, out var report));
        Assert.Equal(ICurveEvalDetail.ParameterResolutionLost, report.Detail);
        Assert.Equal(0, cache.Count);
        foreach (var value in output) Assert.Equal(sentinel.X, value.X);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RationalStraightLine_ZeroCurvatureRoundoffDoesNotSplitTheBranch(int planCode)
    {
        const double slope = 1.3;
        // Degree elevation of the homogeneous linear control polygon gives
        // S(u,v)=(2u/(1+u), slope*2u/(1+u), v). Intersecting z=0 gives
        // a straight line; with original parameter x=t, D2 is exactly zero.
        double[] poles = [0, 0, -1, 1, 0, 0, 1, 1,
            1, slope, -1.5, 1.5, 1, slope, 1.5, 1.5,
            2, 2 * slope, -2, 2, 2, 2 * slope, 2, 2];
        var bs = new BSurfaceView(2, 1, 3, 2, 4, true, false, false, poles,
            new double[] { 0, 0, 0, 1, 1, 1 }, new double[] { -1, -1, 1, 1 });
        var support = new ICurveSupportView(bs, new double[(int)BSurfaceEvaluation.WorkspaceSize(2, 1, 2, 2)]);
        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var other = new ICurveSupportView(in plane);
        var view = new ICurveView(support, ParasolidConstants.PK_TOPOL_sense_positive_c,
            other, ParasolidConstants.PK_TOPOL_sense_positive_c,
            new KernelVector3[] { Vector(0, 0, 0), Vector(1, slope, 0) },
            new double[] { 0, 1 }, new double[] { 1 / Math.Sqrt(1 + slope * slope) },
            new KernelVector3[] { Unit(Vector(1, slope, 0)) },
            new SampleWitness[] { new(0, 0, 0, 0, 0, 3), new(1, 0, 1, slope, 0, 3) });
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        var cache = new EvaluationSampleStore(new CurveSample[4]);

        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(in view, 0.1, 2,
            (ICurveConstraintPlan)planCode, ref cache, output, out var report));
        Assert.Equal((ICurveConstraintPlan)planCode, report.Plan);
        Assert.InRange(Math.Abs(output[0].X - 0.1), 0, 1e-14);
        Assert.InRange(Math.Abs(output[0].Y - 0.1 * slope), 0, 1e-14);
        Assert.InRange(Math.Abs(output[0].Z), 0, 1e-14);
        Assert.InRange(Math.Abs(output[1].X - 1), 0, 1e-14);
        Assert.InRange(Math.Abs(output[1].Y - slope), 0, 1e-14);
        Assert.InRange(Math.Abs(output[1].Z), 0, 1e-14);
        Assert.InRange(Math.Abs(output[2].X) + Math.Abs(output[2].Y) + Math.Abs(output[2].Z), 0, 1e-14);
        Assert.Equal(1, cache.Count);
    }

    private sealed class CloseBranches
    {
        private readonly double[] vertices = new double[24];
        private readonly double[] workspace = new double[(int)BSurfaceEvaluation.WorkspaceSize(3, 1, 2, 2)];

        internal CloseBranches()
        {
            // Cubic Bezier form of S(u,v)=(e*u,v,u(u-e)(u-2e)). Its
            // binary64 coefficients preserve the exact factor u and two
            // distinct positive quadratic roots close to e and 2e.
            double[] z = [0, 2 * Separation * Separation / 3,
                -Separation + 4 * Separation * Separation / 3,
                1 - 3 * Separation + 2 * Separation * Separation];
            for (var i = 0; i < 4; i++)
            for (var j = 0; j < 2; j++)
            {
                var offset = (i * 2 + j) * 3;
                vertices[offset] = Separation * i / 3;
                vertices[offset + 1] = j;
                vertices[offset + 2] = z[i];
            }
            var a = z[3] - 3 * z[2] + 3 * z[1];
            var b = 3 * z[2] - 6 * z[1];
            var c = 3 * z[1];
            Assert.True(a > 0 && b < 0 && c > 0);
            Assert.True(b * b - 4 * a * c > 0);
        }

        internal ICurveView View(double startU, double endU)
        {
            var bs = new BSurfaceView(3, 1, 4, 2, 3, false, false, false, vertices,
                new double[] { 0, 0, 0, 0, 1, 1, 1, 1 }, new double[] { 0, 0, 1, 1 });
            var support = new ICurveSupportView(bs, workspace);
            var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
            var other = new ICurveSupportView(in plane);
            var start = Vector(Separation * startU, 0, 0);
            var end = Vector(Separation * endU, 1, 0);
            var chord = Unit(Vector(end.X - start.X, 1, 0));
            return new ICurveView(support, ParasolidConstants.PK_TOPOL_sense_positive_c,
                other, ParasolidConstants.PK_TOPOL_sense_positive_c,
                new KernelVector3[] { start, end }, new double[] { 0, 1 }, new double[] { 1 }, new KernelVector3[] { chord },
                new SampleWitness[] { new(startU, 0, start.X, 0, 0, 3), new(endU, 1, end.X, 1, 0, 3) });
        }
    }
}
