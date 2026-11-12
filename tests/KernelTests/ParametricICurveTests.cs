using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using Xunit;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Manufactured intersections with independent closed-form roots and jets.
/// The reference differentiates the original chord-plane equation directly;
/// it never calls the surface evaluator or the curve derivative implementation.
/// </summary>
public class ParametricICurveTests
{
    private static readonly AnalyticSurface PlaneY0 = new(
        SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 1, 0), Vector(1, 0, 0));

    [Theory]
    [InlineData(false,false,.125)] [InlineData(false,true,.125)]
    [InlineData(true,false,.125)] [InlineData(true,true,.125)]
    [InlineData(false,false,1.7)] [InlineData(false,true,1.7)]
    [InlineData(true,false,1.7)] [InlineData(true,true,1.7)]
    [InlineData(false,false,8)] [InlineData(false,true,8)]
    [InlineData(true,false,8)] [InlineData(true,true,8)]
    public void HomogeneousChartEquationRetainsBothTracksAndD2AfterReflectedRotation(bool rational,bool reversed,double scale)
    {
        var geometry=rational?RationalCircle(false):Parabola();var dimension=rational?4:3;var poles=(double[])geometry.Vertices.Clone();
        for(var i=0;i<poles.Length;i+=dimension)
        {
            var w=rational?poles[i+3]:1;var x=poles[i];var y=poles[i+1];var z=poles[i+2];
            poles[i]=scale*(-.6*x-.8*y)+3*w;poles[i+1]=scale*(-.8*x+.6*y)+5*w;poles[i+2]=scale*z+7*w;
        }
        var positions=geometry.Positions.Select(p=>Frame(p,scale,true)).ToArray();var chords=geometry.Chords.Select(p=>Frame(p,1,false)).ToArray();
        var scales=geometry.Scales.Select(s=>s/scale).ToArray();var witnesses=new SampleWitness[positions.Length];
        var plane=new AnalyticSurface(SurfaceClass.Plane,Vector(3,5,7),Vector(.8,-.6,0),Vector(-.6,-.8,0));
        for(var i=0;i<positions.Length;i++)
        {
            Assert.Equal(AlgorithmStatus.Success,OperationIntersection.Parameters(plane,positions[i],out var u,out var v));
            witnesses[i]=reversed?new(u,v,geometry.Uv[i],0,0,SampleWitness.HasUv0|SampleWitness.HasUv1):new(geometry.Uv[i],0,u,v,0,SampleWitness.HasUv0|SampleWitness.HasUv1);
        }
        var surface=new BSurfaceView(geometry.Degree,1,poles.Length/(2*dimension),2,dimension,rational,false,false,poles,geometry.UKnots,geometry.VKnots);
        var support=new ICurveSupportView(surface,new double[BSurfaceEvaluation.WorkspaceSize(geometry.Degree,1,2,2)]);var other=new ICurveSupportView(plane);
        var sense=ParasolidConstants.PK_TOPOL_sense_positive_c;
        var view=reversed?new ICurveView(other,sense,support,sense,positions,geometry.Parameters,scales,chords,witnesses):new ICurveView(support,sense,other,sense,positions,geometry.Parameters,scales,chords,witnesses);
        Span<KernelVector3> actual=stackalloc KernelVector3[3];
        foreach(var plan in new[]{ICurveConstraintPlan.P2,ICurveConstraintPlan.P4})for(var segment=0;segment<scales.Length;segment++)
        {
            var t=geometry.Parameters[segment]+.47*(geometry.Parameters[segment+1]-geometry.Parameters[segment]);
            Assert.Equal(AlgorithmStatus.Success,ICurveEvaluation.EvaluateWithPlan(view,t,2,plan,actual,out _));
            var reference=geometry.Reference(t,segment);for(var order=0;order<3;order++)reference[order]=Frame(reference[order],scale,order==0);
            AssertJet(reference,actual);
        }
        static KernelVector3 Frame(KernelVector3 p,double size,bool point)=>Vector(size*(-.6*p.X-.8*p.Y)+(point?3:0),size*(-.8*p.X+.6*p.Y)+(point?5:0),size*p.Z+(point?7:0));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public void MixedParabola_BothSupportOrdersAndPlans_MatchClosedForm(bool reversed, int planCode)
    {
        var geometry = Parabola();
        var view = geometry.View(reversed: reversed);
        var plan = (ICurveConstraintPlan)planCode;
        foreach (var fraction in new[] { 0.13, 0.47, 0.81 })
        for (var segment = 0; segment < geometry.Scales.Length; segment++)
        {
            var t = geometry.Parameters[segment]
                + fraction * (geometry.Parameters[segment + 1] - geometry.Parameters[segment]);
            Span<KernelVector3> actual = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, t, 2, plan, actual, out var report));
            Assert.Equal(plan, report.Plan);
            Assert.Equal(ICurveQueryKind.RegularChartInterval, report.Kind);
            AssertJet(geometry.Reference(t, segment), actual);
            Assert.True(Math.Abs(actual[2].Z) > 0.01, "this case must exercise a nonzero second derivative");
        }
    }

    [Fact]
    public void TwoBSurfaces_P4AndAuto_MatchTheSameParabola()
    {
        var geometry = Parabola();
        var view = geometry.View(twoBSurfaces: true);
        Assert.True(view.HasBSurface);
        foreach (var plan in new[] { ICurveConstraintPlan.P4, ICurveConstraintPlan.Auto })
        for (var segment = 0; segment < geometry.Scales.Length; segment++)
        {
            var t = (geometry.Parameters[segment] + geometry.Parameters[segment + 1]) * 0.5;
            Span<KernelVector3> actual = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, t, 2, plan, actual, out var report));
            Assert.Equal(ICurveConstraintPlan.P4, report.Plan);
            AssertJet(geometry.Reference(t, segment), actual);
        }
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public void RationalQuarterCylinder_NonconstantWeights_MatchCircleJets(bool reversed, int planCode)
    {
        var geometry = RationalCircle(periodic: false);
        var view = geometry.View(reversed: reversed);
        foreach (var fraction in new[] { 0.19, 0.51, 0.83 })
        for (var segment = 0; segment < geometry.Scales.Length; segment++)
        {
            var t = geometry.Parameters[segment]
                + fraction * (geometry.Parameters[segment + 1] - geometry.Parameters[segment]);
            Span<KernelVector3> actual = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithPlan(
                in view, t, 2, (ICurveConstraintPlan)planCode, actual, out _));
            AssertJet(geometry.Reference(t, segment), actual);
            Assert.InRange(Math.Abs(actual[0].X * actual[0].X + actual[0].Z * actual[0].Z - 1), 0, 2e-11);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(-1, -1)]
    public void SupportSense_DoesNotChangeTheImmutableNativeChart(int sense0, int sense1)
    {
        var geometry = Parabola();
        var view = geometry.View(
            sense0: sense0 < 0 ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c,
            sense1: sense1 < 0 ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c);
        var t = (geometry.Parameters[1] + geometry.Parameters[2]) * 0.5;
        foreach (var plan in new[] { ICurveConstraintPlan.P2, ICurveConstraintPlan.P4 })
        {
            Span<KernelVector3> actual = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, t, 2, plan, actual, out _));
            AssertJet(geometry.Reference(t, 1), actual);
        }
    }

    [Fact]
    public void OriginalParameterOffsetAndScale_ArePreservedThroughD2()
    {
        var original = Parabola(baseParameter: -3.25, baseScale: 1.7);
        var rescaled = Parabola(baseParameter: 7.25, baseScale: 3.4);
        var view0 = original.View();
        var view1 = rescaled.View();
        const double fraction = 0.41;
        var t0 = original.Parameters[1] + fraction * (original.Parameters[2] - original.Parameters[1]);
        var t1 = rescaled.Parameters[1] + fraction * (rescaled.Parameters[2] - rescaled.Parameters[1]);
        Span<KernelVector3> jet0 = stackalloc KernelVector3[3];
        Span<KernelVector3> jet1 = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view0, t0, 2, jet0, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view1, t1, 2, jet1, out _));
        AssertJet(original.Reference(t0, 1), jet0);
        AssertJet(rescaled.Reference(t1, 1), jet1);
        AssertVector(jet0[0], jet1[0]);
        AssertVector(Scale(jet0[1], 0.5), jet1[1]);
        AssertVector(Scale(jet0[2], 0.25), jet1[2]);
    }

    [Theory]
    [InlineData(-80)]
    [InlineData(80)]
    public void SupportUvUnits_CanDifferWithoutChangingTheCurve(int exponent)
    {
        var geometry = Parabola();
        var uScale = Math.ScaleB(1.0, exponent);
        var vScale = Math.ScaleB(1.0, -exponent);
        for (var i = 0; i < geometry.UKnots.Length; i++) geometry.UKnots[i] *= uScale;
        for (var i = 0; i < geometry.VKnots.Length; i++) geometry.VKnots[i] *= vScale;
        for (var i = 0; i < geometry.Uv.Length; i++) geometry.Uv[i] *= uScale;
        var t = (geometry.Parameters[1] + geometry.Parameters[2]) * 0.5;
        foreach (var reversed in new[] { false, true })
        foreach (var plan in new[] { ICurveConstraintPlan.P2, ICurveConstraintPlan.P4 })
        {
            var view = geometry.View(reversed: reversed);
            Span<KernelVector3> actual = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithPlan(in view, t, 2, plan, actual, out _));
            AssertJet(geometry.Reference(t, 1), actual);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    public void NearTangentSupports_SmallEquationResidualStillRequiresAnAccurateRoot(int planCode)
    {
        // S(u,v)=(u,v,u²), z=10^-6 gives two branches u=±10^-3.
        // The positive branch's UV seed is only 10^-10 away, so its plane
        // residual is about 2*10^-13. That residual alone does not certify
        // position accuracy: the published root must correct the UV error.
        const double x = 1e-3;
        var surface = new BSurfaceView(2, 1, 3, 2, 3, false, false, false,
            Extrude([-1, 0, 1], [1, -1, 1]), new double[] { -1, -1, -1, 1, 1, 1 },
            new double[] { -1, -1, 1, 1 });
        var bspline = new ICurveSupportView(surface, new double[BSurfaceEvaluation.WorkspaceSize(2, 1, 2, 2)]);
        var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, x * x),
            Vector(0, 0, 1), Vector(1, 0, 0));
        var analytic = new ICurveSupportView(in plane);
        KernelVector3[] points = [Vector(x, 0, x * x), Vector(x, 0.4, x * x), Vector(x, 0.9, x * x)];
        SampleWitness[] witnesses =
        [
            new(x - 1e-10, 0, x, 0, 0, SampleWitness.HasUv0 | SampleWitness.HasUv1),
            new(x - 1e-10, 0.4, x, 0.4, 0, SampleWitness.HasUv0 | SampleWitness.HasUv1),
            new(x - 1e-10, 0.9, x, 0.9, 0, SampleWitness.HasUv0 | SampleWitness.HasUv1),
        ];
        var view = new ICurveView(bspline, ParasolidConstants.PK_TOPOL_sense_positive_c,
            analytic, ParasolidConstants.PK_TOPOL_sense_positive_c, points, new double[] { 0, 0.4, 0.9 },
            new double[] { 1, 1 }, new KernelVector3[] { Vector(0, 1, 0), Vector(0, 1, 0) }, witnesses);
        Span<KernelVector3> output = stackalloc KernelVector3[1];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithPlan(
            in view, 0.23, 0, (ICurveConstraintPlan)planCode, output, out _));
        Assert.InRange(Math.Abs(output[0].X - x), 0, 5e-12);
        Assert.InRange(Math.Abs(output[0].Y - 0.23), 0, 1e-12);
        Assert.InRange(Math.Abs(output[0].Z - x * x), 0, 1e-14);
    }

    [Fact]
    public void ChartNode_D0IsDefiningData_AndD2UsesTheRequestedSide()
    {
        var geometry = Parabola();
        var view = geometry.View();
        var node = geometry.Parameters[1];
        Span<KernelVector3> left = stackalloc KernelVector3[3];
        Span<KernelVector3> right = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node, 2, left, out var leftReport, ChartSide.Left));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node, 2, right, out var rightReport, ChartSide.Right));
        Assert.Equal(ICurveQueryKind.ChartPoint, leftReport.Kind);
        Assert.Equal(ChartSide.Left, leftReport.Side);
        Assert.Equal(ChartSide.Right, rightReport.Side);
        Assert.Equal(geometry.Positions[1].X, left[0].X);
        Assert.Equal(geometry.Positions[1].Z, left[0].Z);
        Assert.Equal(geometry.Positions[1].X, right[0].X);
        Assert.Equal(geometry.Positions[1].Z, right[0].Z);
        AssertJet(geometry.Reference(node, 0), left);
        AssertJet(geometry.Reference(node, 1), right);
        AssertVector(left[1], right[1]);
        Assert.True(Math.Abs(left[2].X - right[2].X) > 1e-3);

        // D0 at an imported chart point is its exact defining value, including
        // rounding in the source chart, rather than a newly corrected root.
        geometry.Positions[1] = Vector(geometry.Positions[1].X, 0, Math.BitIncrement(geometry.Positions[1].Z));
        view = geometry.View();
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, node, 0, right, out _));
        Assert.Equal(geometry.Positions[1].Z, right[0].Z);
    }

    [Fact]
    public void Cache_ColdWarmAndDifferentQueryOrders_HaveTheSameJets()
    {
        var geometry = Parabola();
        var view = geometry.View();
        double[] queries =
        [
            geometry.Parameters[0] * 0.3 + geometry.Parameters[1] * 0.7,
            geometry.Parameters[1] * 0.8 + geometry.Parameters[2] * 0.2,
            geometry.Parameters[1] * 0.2 + geometry.Parameters[2] * 0.8,
        ];
        var cache = new EvaluationSampleStore(new CurveSample[16]);
        var cold = new KernelVector3[queries.Length * 3];
        for (var i = 0; i < queries.Length; i++)
        {
            var output = cold.AsSpan(i * 3, 3);
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
                in view, queries[i], 2, ICurveConstraintPlan.P2, ref cache, output, out var report));
            Assert.NotEqual(CacheHitKind.Exact, report.CacheHit);
            AssertJet(geometry.Reference(queries[i], i == 0 ? 0 : 1), output);
        }
        Assert.Equal(queries.Length, cache.Count);
        foreach (var i in new[] { 2, 0, 1 })
        {
            Span<KernelVector3> output = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
                in view, queries[i], 2, ICurveConstraintPlan.P2, ref cache, output, out var report));
            Assert.Equal(CacheHitKind.Exact, report.CacheHit);
            AssertJet(cold.AsSpan(i * 3, 3), output, 0);
        }

        var reordered = new EvaluationSampleStore(new CurveSample[16]);
        foreach (var i in new[] { 2, 0, 1 })
        {
            Span<KernelVector3> output = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
                in view, queries[i], 2, ICurveConstraintPlan.P2, ref reordered, output, out _));
            AssertJet(cold.AsSpan(i * 3, 3), output);
        }
    }

    [Fact]
    public void Cache_DerivativeOrderPlanAndNodeSideRemainPartOfTheContract()
    {
        var geometry = Parabola();
        var view = geometry.View();
        var cache = new EvaluationSampleStore(new CurveSample[16]);
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        var t = (geometry.Parameters[0] + geometry.Parameters[1]) * 0.5;
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 0, ICurveConstraintPlan.P2, ref cache, output, out _));
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 2, ICurveConstraintPlan.P2, ref cache, output, out var upgraded));
        Assert.NotEqual(CacheHitKind.Exact, upgraded.CacheHit);
        AssertJet(geometry.Reference(t, 0), output);
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
            in view, t, 2, ICurveConstraintPlan.P4, ref cache, output, out var forced));
        Assert.Equal(ICurveConstraintPlan.P4, forced.Plan);
        AssertJet(geometry.Reference(t, 0), output);
        foreach (var plan in new[] { ICurveConstraintPlan.P4, ICurveConstraintPlan.P2 })
        {
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
                in view, t, 2, plan, ref cache, output, out var repeated));
            Assert.Equal(plan, repeated.Plan);
            Assert.Equal(CacheHitKind.Exact, repeated.CacheHit);
            AssertJet(geometry.Reference(t, 0), output);
        }
        foreach (var side in new[] { ChartSide.Left, ChartSide.Right })
        {
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.EvaluateWithCache(
                in view, geometry.Parameters[1], 2, ICurveConstraintPlan.P2, ref cache, output, out _, side));
            AssertJet(geometry.Reference(geometry.Parameters[1], side == ChartSide.Left ? 0 : 1), output);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void MissingMalformedAndOutOfDomainWitnesses_DoNotPublish(int defect)
    {
        var geometry = Parabola();
        var witnesses = geometry.Witnesses();
        witnesses = defect switch
        {
            0 => [],
            1 => witnesses[..^1],
            2 => [new SampleWitness(double.NaN, 0, 0, 0, 0, SampleWitness.HasUv0), witnesses[1], witnesses[2]],
            3 => [new SampleWitness(0, double.PositiveInfinity, 0, 0, 0, SampleWitness.HasUv0), witnesses[1], witnesses[2]],
            _ => [new SampleWitness(-0.01, 0, 0, 0, 0, SampleWitness.HasUv0), witnesses[1], witnesses[2]],
        };
        var view = geometry.View(witnesses: witnesses);
        AssertFailureDoesNotPublish(in view, (geometry.Parameters[0] + geometry.Parameters[1]) * 0.5);
    }

    [Fact]
    public void MissingUvFlag_IsNotInterpretedAsTheZeroCoordinate()
    {
        var geometry = Parabola();
        var witnesses = geometry.Witnesses();
        witnesses[0] = SampleWitness.None; // u=0 is numerically valid, but the witness is absent.
        var view = geometry.View(witnesses: witnesses);
        AssertFailureDoesNotPublish(in view, (geometry.Parameters[0] + geometry.Parameters[1]) * 0.5);
    }

    [Fact]
    public void StrictDomain_DoesNotUseTheBSurfaceLinearExtension()
    {
        var geometry = Parabola();
        // Move the support's upper domain boundary below the last two chart UVs.
        geometry.UKnots = [0, 0, 0, 0.2, 0.2, 0.2];
        var view = geometry.View();
        AssertFailureDoesNotPublish(in view, (geometry.Parameters[1] + geometry.Parameters[2]) * 0.5);
        view = Parabola().View();
        AssertFailureDoesNotPublish(in view, view.ChartParameters[0] - 0.01);
        AssertFailureDoesNotPublish(in view, view.ChartParameters[^1] + 0.01);
    }

    [Fact]
    public void RankDeficientSupport_DoesNotPublishOrCachePartialJets()
    {
        var geometry = Parabola();
        // Collapse the v direction: S(u,v)=(u,0,u²) has a zero surface normal.
        for (var i = 1; i < geometry.Vertices.Length; i += 3) geometry.Vertices[i] = 0;
        var view = geometry.View();
        AssertFailureDoesNotPublish(in view, (geometry.Parameters[0] + geometry.Parameters[1]) * 0.5,
            AlgorithmStatus.Singular);
    }

    [Fact]
    public void UnsupportedImplicitPlan_DoesNotSilentlyChangeToAParametricPlan()
    {
        var geometry = Parabola();
        var view = geometry.View();
        foreach (var plan in new[] { ICurveConstraintPlan.I1, ICurveConstraintPlan.I2, ICurveConstraintPlan.I3 })
        {
            Span<KernelVector3> output = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Unsupported, ICurveEvaluation.EvaluateWithPlan(
                in view, (geometry.Parameters[0] + geometry.Parameters[1]) * 0.5, 2, plan, output, out _));
        }
    }

    [Fact]
    public void C1InteriorKnot_AllowsD1ButRejectsUnresolvedD2()
    {
        // S=(u,v,u²+0.5*max(u-0.5,0)²): the first derivative is continuous,
        // while the left/right second derivatives at u=0.5 are 2 and 3.
        var geometry = new GeometryCase(2, false, false,
            Extrude([0, 0.25, 0.75, 1], [0, 0, 0.5, 1.125]),
            [0, 0, 0, 0.5, 1, 1, 1], [0.1, 0.5, 0.9],
            [Vector(0.1, 0, 0.01), Vector(0.5, 0, 0.25), Vector(0.9, 0, 0.89)],
            [Vector(1, 0, 0.2), Vector(1, 0, 1), Vector(1, 0, 2.2)]);
        var view = geometry.View();
        Span<KernelVector3> output = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in view, geometry.Parameters[1], 1, output, out _));
        AssertVector(geometry.Positions[1], output[0], 0);
        AssertFailureDoesNotPublish(in view, geometry.Parameters[1]);
    }

    [Fact]
    public void PeriodicSupport_LiftedUvWitnessesStayOnTheSameCircleBranch()
    {
        var canonical = RationalCircle(periodic: true);
        var lifted = RationalCircle(periodic: true);
        var canonicalView = canonical.View();
        var t = (canonical.Parameters[1] + canonical.Parameters[2]) * 0.5;
        Span<KernelVector3> expected = stackalloc KernelVector3[3];
        Span<KernelVector3> actual = stackalloc KernelVector3[3];
        Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in canonicalView, t, 2, expected, out _));
        AssertJet(canonical.Reference(t, 1), expected);
        foreach (var lift in new[] { -4.0, 4.0 })
        {
            var liftedWitnesses = lifted.Witnesses();
            for (var i = 0; i < liftedWitnesses.Length; i++)
            {
                var w = liftedWitnesses[i];
                liftedWitnesses[i] = new SampleWitness(w.U0 + lift, w.V0, w.U1, w.V1, w.AuxParameter, w.Flags);
            }
            var liftedView = lifted.View(witnesses: liftedWitnesses);
            Assert.Equal(AlgorithmStatus.Success, ICurveEvaluation.Evaluate(in liftedView, t, 2, actual, out _));
            AssertJet(expected, actual);
        }
    }

    private static void AssertFailureDoesNotPublish(in ICurveView view, double t, AlgorithmStatus? expected = null)
    {
        var sentinel = Vector(123, -456, 789);
        Span<KernelVector3> output = stackalloc KernelVector3[3] { sentinel, sentinel, sentinel };
        var cache = new EvaluationSampleStore(new CurveSample[8]);
        var status = ICurveEvaluation.EvaluateWithCache(in view, t, 2, ICurveConstraintPlan.Auto,
            ref cache, output, out _);
        Assert.NotEqual(AlgorithmStatus.Success, status);
        if (expected.HasValue) Assert.Equal(expected.Value, status);
        Assert.Equal(0, cache.Count);
        foreach (var value in output) AssertVector(sentinel, value, 0);
    }

    private static void AssertJet(ReadOnlySpan<KernelVector3> expected, ReadOnlySpan<KernelVector3> actual,
        double tolerance = 3e-9)
    {
        for (var i = 0; i < 3; i++) AssertVector(expected[i], actual[i], tolerance);
    }

    private static void AssertVector(in KernelVector3 expected, in KernelVector3 actual, double tolerance = 3e-9)
    {
        Assert.True(double.IsFinite(actual.X) && double.IsFinite(actual.Y) && double.IsFinite(actual.Z));
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, tolerance * Math.Max(1, Math.Abs(expected.X)));
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, tolerance * Math.Max(1, Math.Abs(expected.Y)));
        Assert.InRange(Math.Abs(expected.Z - actual.Z), 0, tolerance * Math.Max(1, Math.Abs(expected.Z)));
    }

    private static KernelVector3 Subtract(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static GeometryCase Parabola(double baseParameter = -3.25, double baseScale = 1.7)
        => new(2, false, false, Extrude([0, 0.5, 1], [0, 0, 1]),
            [0, 0, 0, 1, 1, 1], [0, 0.35, 1],
            [Vector(0, 0, 0), Vector(0.35, 0, 0.1225), Vector(1, 0, 1)],
            [Vector(1, 0, 0), Vector(1, 0, 0.7), Vector(1, 0, 2)], baseParameter, baseScale);

    private static double[] Extrude(double[] x, double[] z)
    {
        var vertices = new double[x.Length * 6];
        for (var i = 0; i < x.Length; i++)
        for (var j = 0; j < 2; j++)
        {
            vertices[i * 6 + j * 3] = x[i];
            vertices[i * 6 + j * 3 + 1] = j == 0 ? -1 : 1;
            vertices[i * 6 + j * 3 + 2] = z[i];
        }
        return vertices;
    }

    private static GeometryCase RationalCircle(bool periodic)
    {
        var w = Math.Sqrt(0.5);
        // The periodic degree-two control net repeats its first two poles,
        // including weights. The extended knots repeat every four units.
        double[] x = periodic ? [1, 1, 1, 0, -1, -1, -1, 0, 1, 1] : [1, 1, 0];
        double[] z = periodic ? [-1, 0, 1, 1, 1, 0, -1, -1, -1, 0] : [0, 1, 1];
        var vertices = new double[x.Length * 8];
        for (var i = 0; i < x.Length; i++)
        for (var j = 0; j < 2; j++)
        {
            var weight = (i % 2 == 0) != periodic ? 1 : w;
            vertices[i * 8 + j * 4] = x[i] * weight;
            vertices[i * 8 + j * 4 + 1] = (j == 0 ? -1 : 1) * weight;
            vertices[i * 8 + j * 4 + 2] = z[i] * weight;
            vertices[i * 8 + j * 4 + 3] = weight;
        }
        double[] uv = periodic ? [0.12, 0.38, 0.76] : [0, 0.35, 1];
        var points = new KernelVector3[uv.Length];
        var tangents = new KernelVector3[uv.Length];
        for (var i = 0; i < uv.Length; i++)
        {
            var u = uv[i];
            var denominator = (1 - u) * (1 - u) + 2 * w * u * (1 - u) + u * u;
            points[i] = Vector(((1 - u) * (1 - u) + 2 * w * u * (1 - u)) / denominator,
                0, (2 * w * u * (1 - u) + u * u) / denominator);
            tangents[i] = Vector(-points[i].Z, 0, points[i].X);
        }
        return new GeometryCase(2, true, periodic, vertices,
            periodic ? [-1, -1, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5] : [0, 0, 0, 1, 1, 1],
            uv, points, tangents, circle: true);
    }

    private sealed class GeometryCase
    {
        internal readonly int Degree;
        internal readonly bool Rational;
        internal readonly bool Periodic;
        internal readonly double[] Vertices;
        internal double[] UKnots;
        internal readonly double[] VKnots = [-1, -1, 1, 1];
        internal readonly double[] Uv;
        internal readonly KernelVector3[] Positions;
        internal readonly double[] Parameters;
        internal readonly double[] Scales;
        internal readonly KernelVector3[] Chords;
        private readonly bool circle;
        private readonly double[] workspace;
        private readonly double[] planeWorkspace;

        internal GeometryCase(int degree, bool rational, bool periodic, double[] vertices, double[] uKnots,
            double[] uv, KernelVector3[] positions, KernelVector3[] tangents,
            double baseParameter = -3.25, double baseScale = 1.7, bool circle = false)
        {
            Degree = degree;
            Rational = rational;
            Periodic = periodic;
            Vertices = vertices;
            UKnots = uKnots;
            Uv = uv;
            Positions = positions;
            this.circle = circle;
            workspace = new double[BSurfaceEvaluation.WorkspaceSize(degree, 1, 2, 2)];
            planeWorkspace = new double[BSurfaceEvaluation.WorkspaceSize(1, 1, 2, 2)];
            Parameters = new double[positions.Length];
            Scales = new double[positions.Length - 1];
            Chords = new KernelVector3[positions.Length - 1];
            Parameters[0] = baseParameter;
            // Definition of the original chart, kept independent of Build().
            for (var i = 0; i < Scales.Length; i++)
            {
                var delta = Subtract(positions[i + 1], positions[i]);
                var length = Math.Sqrt(Dot(delta, delta));
                Chords[i] = Scale(delta, 1 / length);
                Scales[i] = i == 0 ? baseScale
                    : Scales[i - 1] * Dot(Chords[i - 1], tangents[i]) / Dot(Chords[i], tangents[i]);
                Parameters[i + 1] = Parameters[i] + Scales[i] * length;
            }
        }

        internal SampleWitness[] Witnesses(bool reversed = false, bool twoBSurfaces = false)
        {
            var result = new SampleWitness[Positions.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var planeV = twoBSurfaces ? Positions[i].Z : -Positions[i].Z;
                result[i] = reversed
                    ? new SampleWitness(Positions[i].X, planeV, Uv[i], 0, 0, SampleWitness.HasUv0 | SampleWitness.HasUv1)
                    : new SampleWitness(Uv[i], 0, Positions[i].X, planeV, 0, SampleWitness.HasUv0 | SampleWitness.HasUv1);
            }
            return result;
        }

        internal ICurveView View(bool reversed = false, bool twoBSurfaces = false,
            int sense0 = ParasolidConstants.PK_TOPOL_sense_positive_c,
            int sense1 = ParasolidConstants.PK_TOPOL_sense_positive_c, SampleWitness[]? witnesses = null)
        {
            var surface = new BSurfaceView(Degree, 1, Vertices.Length / ((Rational ? 4 : 3) * 2), 2,
                Rational ? 4 : 3, Rational, Periodic, false, Vertices, UKnots, VKnots);
            var support = new ICurveSupportView(surface, workspace);
            var plane = new BSurfaceView(1, 1, 2, 2, 3, false, false, false,
                new double[] { 0, 0, -1, 0, 0, 2, 1, 0, -1, 1, 0, 2 },
                new double[] { 0, 0, 1, 1 }, new double[] { -1, -1, 2, 2 });
            var other = twoBSurfaces
                ? new ICurveSupportView(plane, planeWorkspace)
                : new ICurveSupportView(in PlaneY0);
            return reversed
                ? new ICurveView(other, sense0, support, sense1, Positions, Parameters, Scales, Chords,
                    witnesses ?? Witnesses(true, twoBSurfaces))
                : new ICurveView(support, sense0, other, sense1, Positions, Parameters, Scales, Chords,
                    witnesses ?? Witnesses(false, twoBSurfaces));
        }

        internal KernelVector3[] Reference(double t, int segment)
        {
            var e = Chords[segment];
            var f = Scales[segment];
            var coordinate = Dot(e, Positions[segment]) + (t - Parameters[segment]) / f;
            if (circle)
            {
                // Intersect the chord-normal line e·X=c with x²+z²=1.
                var normal = Vector(e.Z, 0, -e.X);
                var height = Math.Sqrt(1 - coordinate * coordinate);
                return
                [
                    Add(Scale(e, coordinate), Scale(normal, height)),
                    Scale(Subtract(e, Scale(normal, coordinate / height)), 1 / f),
                    Scale(normal, -1 / (f * f * height * height * height)),
                ];
            }

            // e_z*u² + e_x*u = c, C=(u,0,u²).
            var discriminant = Math.Sqrt(e.X * e.X + 4 * e.Z * coordinate);
            var u = 2 * coordinate / (e.X + discriminant);
            var slope = e.X + 2 * e.Z * u;
            var first = 1 / (f * slope);
            var second = -2 * e.Z * first * first / slope;
            return [Vector(u, 0, u * u), Vector(first, 0, 2 * u * first),
                Vector(second, 0, 2 * first * first + 2 * u * second)];
        }
    }
}
