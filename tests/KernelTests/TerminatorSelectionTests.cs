using ProjectGmKernel.Native.Computation;
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
}
