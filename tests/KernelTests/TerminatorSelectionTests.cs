using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Terminator documented parts (spec §6, task T16 subset): the p.49 OR
/// selection rule against all 24 reference_vectors.json RV-TERM-SELECT cases,
/// and the RV-TERMINATOR one-surface/two-planes construction for
/// z=0 ∩ z=y²−x³. The GATE-T items (terminator parameter reconstruction,
/// endpoint derivative contract) are intentionally absent here.
/// </summary>
public class TerminatorSelectionTests
{
    [Fact]
    public void SelectSupportSurface_MatchesAllReferenceCases()
    {
        // (surface0Singular, surface1Singular, surface0IsBlendBound, termUse) → expected
        (bool, bool, bool, LimitTermUse, int)[] cases =
        [
            (false, false, false, LimitTermUse.Unset, 0),
            (false, false, false, LimitTermUse.First, 0),
            (false, false, false, LimitTermUse.Second, 1),
            (false, false, true, LimitTermUse.Unset, 1),
            (false, false, true, LimitTermUse.First, 1),
            (false, false, true, LimitTermUse.Second, 1),
            (false, true, false, LimitTermUse.Unset, 0),
            (false, true, false, LimitTermUse.First, 0),
            (false, true, false, LimitTermUse.Second, 1),
            (false, true, true, LimitTermUse.Unset, 1),
            (false, true, true, LimitTermUse.First, 1),
            (false, true, true, LimitTermUse.Second, 1),
            (true, false, false, LimitTermUse.Unset, 1),
            (true, false, false, LimitTermUse.First, 1),
            (true, false, false, LimitTermUse.Second, 1),
            (true, false, true, LimitTermUse.Unset, 1),
            (true, false, true, LimitTermUse.First, 1),
            (true, false, true, LimitTermUse.Second, 1),
            (true, true, false, LimitTermUse.Unset, 0),
            (true, true, false, LimitTermUse.First, 0),
            (true, true, false, LimitTermUse.Second, 1),
            (true, true, true, LimitTermUse.Unset, 1),
            (true, true, true, LimitTermUse.First, 1),
            (true, true, true, LimitTermUse.Second, 1),
        ];
        foreach (var (s0Singular, s1Singular, s0BlendBound, termUse, expected) in cases)
            Assert.Equal(expected,
                TerminatorEvaluation.SelectSupportSurface(s0Singular, s1Singular, s0BlendBound, termUse));
    }

    [Fact]
    public void TerminatorPlanes_MatchReferenceConstruction()
    {
        // RV-TERMINATOR: E = origin, B = (1e-4, 1e-6, 0), surface[0] = z=0 chosen.
        var endpoint = Vector(0, 0, 0);
        var branch = Vector(0.0001, 0.000001, 0);
        var supportNormal = Vector(0, 0, 1);
        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.BuildPlanes(in endpoint, in branch, in supportNormal, out var planeNormal, out var chordUnit, out var lineDirection));

        // Chord midpoint at fraction 0.5 lies on both planes and on z=0.
        var mid = TerminatorEvaluation.ChordPoint(in endpoint, in branch, 0.5);
        Assert.Equal(5e-05, mid.X, 15);
        Assert.Equal(5.000000000000001e-07, mid.Y, 15);
        Assert.Equal(0.0, mid.Z, 15);
        Assert.InRange(Math.Abs(TerminatorEvaluation.PlaneResidual(in planeNormal, in endpoint, in mid)), 0, 1e-18);
        Assert.InRange(Math.Abs(TerminatorEvaluation.ChordPlaneResidual(in chordUnit, in mid, in mid)), 0, 1e-18);

        // The intersection line direction is ±z: points along v satisfy both
        // planes; the non-defining surface keeps its deviation as a diagnostic
        // value (h⁶/8 scale), never as a fourth equation.
        Assert.InRange(Math.Abs(lineDirection.Z), 1 - 1e-12, 1);
        var h = 0.01;
        var alongLine = Vector(mid.X, mid.Y, h);
        Assert.InRange(Math.Abs(TerminatorEvaluation.PlaneResidual(in planeNormal, in endpoint, in alongLine)), 0, 1e-18);
        Assert.InRange(Math.Abs(TerminatorEvaluation.ChordPlaneResidual(in chordUnit, in mid, in alongLine)), 0, 1e-18);
        // Reference: at the midpoint the non-defining residual is 1.25e-13.
        var nonDefining = mid.Y * mid.Y - mid.X * mid.X * mid.X;
        Assert.InRange(Math.Abs(nonDefining - 1.2500000000000007e-13), 0, 1e-28);
    }

    [Fact]
    public void TerminatorPlanes_DegenerateGeometryIsDiagnosed()
    {
        // E == B: no chord direction exists.
        var endpoint = Vector(0, 0, 0);
        Assert.Equal(AlgorithmStatus.InvalidInput,
            TerminatorEvaluation.BuildPlanes(in endpoint, in endpoint, Vector(0, 0, 1), out _, out _, out _));

        // Chord parallel to the support normal: e×w degenerates and the
        // document authorizes no world-axis fallback.
        var branch = Vector(0, 0, 1);
        Assert.Equal(AlgorithmStatus.Singular,
            TerminatorEvaluation.BuildPlanes(in endpoint, in branch, Vector(0, 0, 1), out _, out _, out _));

        Assert.Equal(AlgorithmStatus.InvalidInput,
            TerminatorEvaluation.BuildPlanes(in endpoint, Vector(1, 0, double.NaN), Vector(0, 0, 1), out _, out _, out _));
    }

    [Fact]
    public void Prepare_WhenSupport0RefIsBlendBound_OverridesFirstTermUseAndSelectsSupport1()
    {
        var plane0 = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var plane1 = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 1, 0), Vector(1, 0, 0));
        var support0 = new SurfaceSupportRef(in plane0, ParasolidConstants.PK_TOPOL_sense_positive_c, isBlendBound: true, blendBoundSlot: 0);
        var support1 = new SurfaceSupportRef(in plane1, ParasolidConstants.PK_TOPOL_sense_positive_c, isBlendBound: false);

        KernelVector3[] positions = [Vector(0, 0, 0), Vector(1, 0, 0)];
        double[] parameters = [0.0, 1.0];
        double[] scales = [1.0];
        KernelVector3[] chords = [Vector(1, 0, 0)];
        var startTerm = new TerminatorLimit(LimitTermUse.First, Vector(-0.1, 0, 0), Vector(0, 0, 0));
        var view = new ICurveView(in support0, in support1, positions, parameters, scales, chords, startTerm, default);

        Assert.Equal(AlgorithmStatus.Success,
            TerminatorEvaluation.Prepare(
                in view, isEnd: false, startTerm.Endpoint, startTerm.BranchPoint,
                LimitTermUse.First, -0.1, out var anchor));
        Assert.Equal(1, anchor.SelectedSurface);
    }

    [Fact]
    public void EvaluateWithRule_LargeScaleTorusTerminator_DoesNotOverflowSingularityCheckOrSwitchSurface()
    {
        // Regression for review Item 3: 4:1 ring torus at R = 1 vs R = 2^190 with TermUse.First.
        // Previously, squaring jet0.Gradient and 1e-12 * gradScale0 overflowed both sides to +Infinity,
        // evaluating Infinity <= Infinity as true, marking the regular torus as singular, switching
        // SelectedSurface from Torus 0 to Plane 1, and returning an unprojected chord midpoint.
        static void RunAtScale(double r, out int selectedSurface, out KernelVector3 normalizedPoint)
        {
            var rOut = 1.25 * r;
            var torus = new AnalyticSurface(SurfaceClass.Torus, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), r, 0.25 * r);
            var plane = new AnalyticSurface(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
            var support0 = new SurfaceSupportRef(in torus, ParasolidConstants.PK_TOPOL_sense_positive_c, isBlendBound: false);
            var support1 = new SurfaceSupportRef(in plane, ParasolidConstants.PK_TOPOL_sense_negative_c, isBlendBound: false);

            var e = Vector(rOut, 0, 0);
            var b = Vector(0, rOut, 0);
            var p1 = Vector(-rOut, 0, 0);
            KernelVector3[] positions = [b, p1];
            double[] parameters = [0.0, rOut * Math.Sqrt(2.0)];
            double[] scales = [1.0];
            KernelVector3[] chords = [Vector(-1.0 / Math.Sqrt(2.0), -1.0 / Math.Sqrt(2.0), 0)];
            var startTerm = new TerminatorLimit(LimitTermUse.First, e, b);
            var view = new ICurveView(in support0, in support1, positions, parameters, scales, chords, startTerm, default);

            Assert.Equal(AlgorithmStatus.Success,
                TerminatorEvaluation.TryResolveTerminatorParameter(
                    in view, isEnd: false, TerminatorParameterRule.ExtensionRatio, in e, in b, out var tE));
            Assert.Equal(AlgorithmStatus.Success,
                TerminatorEvaluation.Prepare(
                    in view, isEnd: false, e, b, LimitTermUse.First, tE, out var anchor));
            selectedSurface = anchor.SelectedSurface;

            var tMid = 0.5 * (tE + parameters[0]);
            Span<KernelVector3> derivatives = stackalloc KernelVector3[3];
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.EvaluateWithRule(
                    in view, tMid, order: 2, ICurveConstraintPlan.Auto,
                    TerminatorParameterRule.ExtensionRatio, derivatives, out _));
            normalizedPoint = Scale(derivatives[0], 1.0 / r);
        }

        RunAtScale(1.0, out var selUnit, out var ptUnit);
        RunAtScale(Math.ScaleB(1.0, 190), out var selLarge, out var ptLarge);

        Assert.Equal(0, selUnit);
        Assert.Equal(0, selLarge);
        Assert.InRange(Math.Abs(ptUnit.X - 1.25 / Math.Sqrt(2.0)), 0, 1e-10);
        Assert.InRange(Math.Abs(ptUnit.Y - 1.25 / Math.Sqrt(2.0)), 0, 1e-10);
        Assert.Equal(ptUnit.X, ptLarge.X, 14);
        Assert.Equal(ptUnit.Y, ptLarge.Y, 14);
        Assert.Equal(0.0, ptLarge.Z, 14);
    }
}
