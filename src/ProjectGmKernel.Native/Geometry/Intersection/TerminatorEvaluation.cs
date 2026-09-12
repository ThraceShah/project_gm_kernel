using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Terminator-specific semantics, documented parts only (spec §6, task T16):
/// the p.49 support-surface selection rule and the one-surface/two-planes
/// construction. The terminator parameter reconstruction and endpoint
/// derivative contract stay behind GATE-T and are deliberately absent.
/// </summary>
internal static class TerminatorEvaluation
{
    /// <summary>
    /// p.49 selection rule, evaluated as stated: surface[1] when surface[0]
    /// is a BLEND_BOUND, or when surface[0] is singular at the terminator and
    /// surface[1] is not, or when term_use is second. An explicit first
    /// term_use does not override the first two exceptions.
    /// </summary>
    internal static BufferOffset SelectSupportSurface(bool surface0Singular, bool surface1Singular,
        bool surface0IsBlendBound, LimitTermUse termUse)
    {
        if (surface0IsBlendBound) return 1;
        if (surface0Singular && !surface1Singular) return 1;
        if (termUse == LimitTermUse.Second) return 1;
        return 0;
    }

    /// <summary>
    /// Chord point Q between the terminator E (fraction 0) and the branch
    /// point B (fraction 1) along the increasing-parameter direction.
    /// </summary>
    internal static KernelVector3 ChordPoint(in KernelVector3 endpoint, in KernelVector3 branchPoint, double fraction)
        => Add(endpoint, Scale(Sub(branchPoint, endpoint), fraction));

    /// <summary>
    /// Planes of the one-surface construction: the first plane a·(x−E)=0
    /// contains the E–B chord and the chosen support normal w; the second
    /// plane is e·(x−Q(t))=0 against the chord interpolation. Their
    /// intersection line is x(μ,t) = Q + μ·v. Fails with
    /// <see cref="AlgorithmStatus.Singular"/> when e×w degenerates — the
    /// document does not authorize a fallback world axis.
    /// </summary>
    internal static AlgorithmStatus BuildPlanes(in KernelVector3 endpoint, in KernelVector3 branchPoint,
        in KernelVector3 supportNormal, out KernelVector3 planeNormal, out KernelVector3 chordUnit,
        out KernelVector3 lineDirection)
    {
        planeNormal = default;
        chordUnit = default;
        lineDirection = default;
        if (!IsFinite(endpoint) || !IsFinite(branchPoint) || !IsFinite(supportNormal))
            return AlgorithmStatus.InvalidInput;
        var e = Unit(Sub(branchPoint, endpoint));
        if (!IsFinite(e) || !(Dot(e, e) > 0)) return AlgorithmStatus.InvalidInput; // E == B
        var cross = Cross(e, supportNormal);
        // Unit() maps a degenerate cross product to the zero vector, which is
        // finite — test the magnitude directly so e ∥ w is diagnosed (no
        // world-axis fallback is authorized by the document).
        if (!(Dot(cross, cross) > 1e-24)) return AlgorithmStatus.Singular;
        var a = Unit(cross);
        var v = Unit(Cross(a, e));
        if (!IsFinite(v)) return AlgorithmStatus.NumericalFailure;
        planeNormal = a;
        chordUnit = e;
        lineDirection = v;
        return AlgorithmStatus.Success;
    }

    /// <summary>First-plane residual a·(x−E).</summary>
    internal static double PlaneResidual(in KernelVector3 planeNormal, in KernelVector3 endpoint, in KernelVector3 point)
        => Dot(planeNormal, Sub(point, endpoint));

    /// <summary>Second-plane residual e·(x−Q(t)); Q is the chord point at the same fraction.</summary>
    internal static double ChordPlaneResidual(in KernelVector3 chordUnit, in KernelVector3 chordPoint, in KernelVector3 point)
        => Dot(chordUnit, Sub(point, chordPoint));

    private static KernelVector3 Add(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
