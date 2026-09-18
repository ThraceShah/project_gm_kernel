using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// How the terminator's native parameter t_T is reconstructed (spec §6.5,
/// GATE-T). The XT file does not transmit t_T, so every rule here is an
/// <em>experiment candidate</em>, not a documented fact:
/// <see cref="Unresolved"/> is the production default and never guesses;
/// the named rules are deterministic reconstructions from import data,
/// available only through the explicit-rule evaluation entry until real-PK
/// evidence closes the gate.
/// </summary>
internal enum TerminatorParameterRule : byte
{
    /// <summary>No rule selected: terminator-interval queries are refused with
    /// <see cref="ICurveEvalDetail.CompatibilityGateOpen"/> (GATE-T open).</summary>
    Unresolved = 0,
    /// <summary>Extend the chart's normalized-cosine scale recursion across the
    /// terminator chord: f_T = f·(T·e_boundary)/(T·e_chord), t_T = t_B ± f_T·C.</summary>
    ExtensionRatio = 1,
    /// <summary>Match the chart's parameter speed dt/ds = f·(T·e) at the branch
    /// point across the chord arc: t_T = t_B ± C·f·(T·e_boundary).</summary>
    TangentMatching = 2,
}


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

    /// <summary>
    /// Prepared one-end terminator state: the selected support, both planes
    /// reduced to the scalar line x(μ,t) = Q(t) + μ·v, and the resolved
    /// parameter mapping. Built only from a resolved rule — the preparation
    /// itself never guesses t_T (§6.5).
    /// </summary>
    internal readonly struct TerminatorAnchor
    {
        internal readonly BufferOffset SelectedSurface;   // 0 or 1 (p.49 rule)
        internal readonly KernelVector3 Endpoint;         // E, defining data
        internal readonly KernelVector3 BranchPoint;      // B, defining data
        internal readonly KernelVector3 PlaneNormal;      // a
        internal readonly KernelVector3 ChordUnit;        // e as built (E→B orientation)
        internal readonly KernelVector3 LineDirection;    // v = unit(a×e)
        internal readonly KernelVector3 ChordRate;        // Q'(t) = (E−B)/(t_T−t_B)
        internal readonly double TerminatorParameter;     // resolved t_T
        internal readonly double BranchParameter;         // chart boundary parameter t_B
        internal readonly double ChordLength;             // |E−B|

        internal TerminatorAnchor(BufferOffset selectedSurface, in KernelVector3 endpoint,
            in KernelVector3 branchPoint, in KernelVector3 planeNormal, in KernelVector3 chordUnit,
            in KernelVector3 lineDirection, in KernelVector3 chordRate,
            double terminatorParameter, double branchParameter, double chordLength)
        {
            SelectedSurface = selectedSurface;
            Endpoint = endpoint;
            BranchPoint = branchPoint;
            PlaneNormal = planeNormal;
            ChordUnit = chordUnit;
            LineDirection = lineDirection;
            ChordRate = chordRate;
            TerminatorParameter = terminatorParameter;
            BranchParameter = branchParameter;
            ChordLength = chordLength;
        }
    }

    /// <summary>
    /// Reconstruct the terminator's native parameter from import data with an
    /// explicit candidate rule (§6.5). Both rules extend from the chart
    /// boundary parameter t_B across the terminator chord, using the branch
    /// tangent T = unit(∇φ₀×∇φ₁) at B; they differ in whether the boundary
    /// scale is divided or multiplied by the chord alignment (T·e_chord).
    /// Fails with a located status when the chord or the tangent data cannot
    /// support a resolvable extension — never falls back to another rule.
    /// </summary>
    internal static AlgorithmStatus TryResolveTerminatorParameter(in ICurveView view, bool isEnd,
        TerminatorParameterRule rule, in KernelVector3 endpoint, in KernelVector3 branchPoint,
        out double terminatorParameter)
    {
        terminatorParameter = 0;
        if (rule == TerminatorParameterRule.Unresolved) return AlgorithmStatus.InvalidInput;
        if (!IsFinite(endpoint) || !IsFinite(branchPoint)) return AlgorithmStatus.InvalidInput;

        var toBranch = Sub(branchPoint, endpoint);
        var chordLength = Math.Sqrt(Dot(toBranch, toBranch));
        if (!(chordLength > 0) || !double.IsFinite(chordLength))
            return AlgorithmStatus.InvalidInput; // E == B: no interval exists

        // e_chord points in the increasing-parameter direction across the
        // interval: toward E at an end terminator, away from it at a start one.
        var chordDirection = isEnd ? Scale(toBranch, -1 / chordLength) : Scale(toBranch, 1 / chordLength);

        var branchParameter = isEnd ? view.ChartParameters[^1] : view.ChartParameters[0];
        var boundaryScale = isEnd ? view.ChartScales[^1] : view.ChartScales[0];
        var boundaryChord = isEnd ? view.ChartChordUnits[^1] : view.ChartChordUnits[0];
        if (!double.IsFinite(branchParameter) || !(boundaryScale > 0) || !IsFinite(boundaryChord))
            return AlgorithmStatus.InvalidInput;

        // Branch tangent from the defining supports (§5.1 construction).
        if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in branchPoint, 1, out var jet0) != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in branchPoint, 1, out var jet1) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        var tangent = Unit(Cross(jet0.Gradient, jet1.Gradient));
        if (!IsFinite(tangent)) return AlgorithmStatus.Singular;

        var forwardCosine = Dot(tangent, boundaryChord);
        if (!(forwardCosine > OriginalChartParameterMap.MinCosine))
            return AlgorithmStatus.Singular; // branch tangent misaligned with the chart side
        var chordCosine = Dot(tangent, chordDirection);
        if (!(chordCosine > OriginalChartParameterMap.MinCosine))
            return AlgorithmStatus.Singular; // the curve turns away from the chord: extension undefined

        // The two candidates of §6.5; both deterministic in the imported data.
        var span = rule switch
        {
            TerminatorParameterRule.ExtensionRatio => boundaryScale * (forwardCosine / chordCosine) * chordLength,
            TerminatorParameterRule.TangentMatching => boundaryScale * forwardCosine * chordLength,
            _ => double.NaN,
        };
        if (!double.IsFinite(span) || !(span > 0))
            return AlgorithmStatus.NumericalFailure;

        terminatorParameter = isEnd ? branchParameter + span : branchParameter - span;
        if (!double.IsFinite(terminatorParameter) || terminatorParameter == branchParameter)
            return AlgorithmStatus.NumericalFailure; // not resolvable in binary64
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Prepare one end's construction (§6.2): validate the branch point
    /// against the chart boundary, select the support surface, derive w (with
    /// the branch-tangent fallback) and build both planes. The resolved
    /// terminator parameter travels with the anchor.
    /// </summary>
    internal static AlgorithmStatus Prepare(in ICurveView view, bool isEnd,
        in KernelVector3 endpoint, in KernelVector3 branchPoint, LimitTermUse termUse,
        double terminatorParameter, out TerminatorAnchor anchor)
    {
        anchor = default;
        if (!IsFinite(endpoint) || !IsFinite(branchPoint) || !double.IsFinite(terminatorParameter))
            return AlgorithmStatus.InvalidInput;

        // The limit's branch point and the chart boundary must describe the
        // same imported point; a mismatch is an input problem, never snapped (§5.2).
        var boundary = isEnd ? view.ChartPositions[^1] : view.ChartPositions[0];
        var branchScale = Math.Max(1.0, Math.Sqrt(Dot(branchPoint, branchPoint)));
        if (Dot(Sub(branchPoint, boundary), Sub(branchPoint, boundary)) > (1e-9 * branchScale) * (1e-9 * branchScale))
            return AlgorithmStatus.InvalidInput;

        // Support selection at E: a zero gradient is the singularity the p.49
        // rule reacts to (surface0 singular and surface1 not → surface[1]).
        if (AnalyticImplicitEvaluation.Evaluate(in view.Support0, in endpoint, 1, out var jet0) != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in view.Support1, in endpoint, 1, out var jet1) != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        var endpointScale = 1.0 + Math.Sqrt(Dot(endpoint, endpoint));
        var surface0Singular = GradientNormSq(jet0.Gradient) <= (1e-12 * endpointScale) * (1e-12 * endpointScale);
        var surface1Singular = GradientNormSq(jet1.Gradient) <= (1e-12 * endpointScale) * (1e-12 * endpointScale);
        var selected = SelectSupportSurface(surface0Singular, surface1Singular, false, termUse);
        var selectedJet = selected == 0 ? jet0 : jet1;

        // w = selected support normal at E; when it cannot be defined there,
        // the branch-point curve tangent is the document's only fallback (§6.2).
        var supportNormal = Unit(selectedJet.Gradient);
        if (!IsFinite(supportNormal))
        {
            supportNormal = Unit(Cross(jet0.Gradient, jet1.Gradient));
            if (!IsFinite(supportNormal)) return AlgorithmStatus.Singular;
        }

        if (BuildPlanes(in endpoint, in branchPoint, in supportNormal,
                out var planeNormal, out var chordUnit, out var lineDirection) != AlgorithmStatus.Success)
            return AlgorithmStatus.Singular; // degenerate e×w: no world-axis fallback is authorized

        var branchParameter = isEnd ? view.ChartParameters[^1] : view.ChartParameters[0];
        if (terminatorParameter == branchParameter
            || (isEnd ? terminatorParameter < branchParameter : terminatorParameter > branchParameter))
            return AlgorithmStatus.InvalidInput; // resolved parameter must sit strictly beyond the chart
        var step = terminatorParameter - branchParameter;
        if (!double.IsFinite(1.0 / step)) return AlgorithmStatus.NumericalFailure;

        var toBranch = Sub(branchPoint, endpoint);
        var chordLength = Math.Sqrt(Dot(toBranch, toBranch));
        // Q'(t) = (E−B)/(t_T−t_B), constant on the interval (Q is affine in t).
        var chordRate = Scale(Sub(endpoint, branchPoint), 1 / step);

        anchor = new TerminatorAnchor(selected, in endpoint, in branchPoint,
            in planeNormal, in chordUnit, in lineDirection, in chordRate,
            terminatorParameter, branchParameter, chordLength);
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Chord point Q(t) = B + λ(E−B) with λ = (t−t_B)/(t_T−t_B); λ∈(0,1) on
    /// the interval, 0 at the branch point and 1 at the terminator itself.
    /// </summary>
    internal static KernelVector3 InterpolatedChordPoint(in TerminatorAnchor anchor, double t)
    {
        var lambda = (t - anchor.BranchParameter) / (anchor.TerminatorParameter - anchor.BranchParameter);
        return Add(anchor.BranchPoint, Scale(Sub(anchor.Endpoint, anchor.BranchPoint), lambda));
    }

    /// <summary>
    /// Solve the one-surface system on the interval (§6.3): φ_A(Q + μv) = 0 by
    /// safeguarded Newton from the branch-connected seed μ=0, with an
    /// expanding sign-change bracket and bisection as the fallback. The
    /// accepted root is the one connected to the branch witness μ(t_B) = 0 —
    /// not merely the smallest |μ| among all roots.
    /// </summary>
    internal static AlgorithmStatus SolveIntervalPoint(in AnalyticSurface selectedSurface,
        in TerminatorAnchor anchor, double t,
        out double mu, out KernelVector3 point, out double residual, out BufferOffset evaluations)
    {
        mu = 0;
        point = default;
        residual = 0;
        evaluations = 0;
        if (!double.IsFinite(t)) return AlgorithmStatus.InvalidInput;
        var q = InterpolatedChordPoint(in anchor, t);

        var bound = 4.0 * anchor.ChordLength; // roots beyond a multiple of the chord are not this branch
        double value;
        var gradient = default(KernelVector3);
        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            evaluations++;
            point = Add(q, Scale(anchor.LineDirection, mu));
            if (AnalyticImplicitEvaluation.Evaluate(in selectedSurface, in point, 1, out var jet)
                != AlgorithmStatus.Success)
                return AlgorithmStatus.Unsupported;
            value = jet.Value;
            gradient = jet.Gradient;
            residual = Math.Abs(value);
            if (residual <= ICurveEvaluation.ResidualTolerance * Math.Max(1.0, Math.Abs(point.X)
                    + Math.Abs(point.Y) + Math.Abs(point.Z)))
            {
                converged = true;
                break;
            }
            var slope = Dot(gradient, anchor.LineDirection);
            if (!(Math.Abs(slope) > 1e-300)) break; // leave to the bracketed fallback
            var next = mu - value / slope;
            if (!double.IsFinite(next) || Math.Abs(next) > bound) break;
            mu = next;
        }
        if (!converged && !BracketedSolve(in selectedSurface, in anchor, in q, bound,
                out mu, out point, out residual, ref evaluations))
            return AlgorithmStatus.NotConverged;
        return AlgorithmStatus.Success;
    }

    private const int MaxNewtonIterations = 8;
    private const int MaxBisectionIterations = 60;
    private const int BracketSamples = 24;

    /// <summary>Sign-change bracket nearest the branch witness, bisected, then Newton-polished.</summary>
    private static bool BracketedSolve(in AnalyticSurface surface, in TerminatorAnchor anchor,
        in KernelVector3 q, double bound, out double mu, out KernelVector3 point,
        out double residual, ref BufferOffset evaluations)
    {
        mu = 0;
        point = default;
        residual = 0;
        double lowValue = 0, highValue = 0, lowMu = 0, highMu = 0;
        var bracketed = false;
        for (BufferOffset i = 1; i <= BracketSamples && !bracketed; i++)
        {
            var step = bound * i / BracketSamples;
            evaluations++;
            if (TryValue(in surface, in anchor, in q, step, out highValue)
                && TryValue(in surface, in anchor, in q, -step, out lowValue))
            {
                if ((highValue <= 0 && 0 <= lowValue) || (lowValue <= 0 && 0 <= highValue))
                {
                    lowMu = -step;
                    highMu = step;
                    bracketed = true;
                }
            }
        }
        if (!bracketed) return false;

        for (BufferOffset i = 0; i < MaxBisectionIterations; i++)
        {
            var mid = 0.5 * (lowMu + highMu);
            evaluations++;
            if (!TryValue(in surface, in anchor, in q, mid, out var midValue)) return false;
            if ((midValue <= 0 && lowValue > 0) || (midValue > 0 && lowValue <= 0))
            {
                highMu = mid;
                highValue = midValue;
            }
            else
            {
                lowMu = mid;
                lowValue = midValue;
            }
        }
        mu = 0.5 * (lowMu + highMu);
        point = Add(q, Scale(anchor.LineDirection, mu));
        evaluations++;
        if (!TryValue(in surface, in anchor, in q, mu, out var value)) return false;
        residual = Math.Abs(value);
        return residual <= ICurveEvaluation.ResidualTolerance * (1.0 + Math.Abs(point.X)
            + Math.Abs(point.Y) + Math.Abs(point.Z));
    }

    private static bool TryValue(in AnalyticSurface surface, in TerminatorAnchor anchor,
        in KernelVector3 q, double mu, out double value)
    {
        value = 0;
        var point = Add(q, Scale(anchor.LineDirection, mu));
        if (AnalyticImplicitEvaluation.Evaluate(in surface, in point, 0, out var jet)
            != AlgorithmStatus.Success)
            return false;
        value = jet.Value;
        return double.IsFinite(value);
    }

    /// <summary>
    /// Interval derivatives from the fixed-plane construction (§16.3): the
    /// scalar reduction μ(t) of φ_A(Q(t)+μv), with Q'' = 0. Requires the
    /// regular scalar reduction (∇φ_A·v ≠ 0); the resulting jets describe the
    /// document's one-face construction, not a two-face intersection.
    /// </summary>
    internal static AlgorithmStatus IntervalDerivatives(in AnalyticSurface selectedSurface,
        in TerminatorAnchor anchor, double t, DerivativeOrder order,
        out KernelVector3 first, out KernelVector3 second)
    {
        first = default;
        second = default;
        if (order < 1) return AlgorithmStatus.InvalidInput;
        var point = InterpolatedChordPoint(in anchor, t);
        if (AnalyticImplicitEvaluation.Evaluate(in selectedSurface, in point, 1, out var jet)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        var slope = Dot(jet.Gradient, anchor.LineDirection);
        if (!(Math.Abs(slope) > 1e-300)) return AlgorithmStatus.Singular;

        var muPrime = -Dot(jet.Gradient, anchor.ChordRate) / slope;
        first = Add(anchor.ChordRate, Scale(anchor.LineDirection, muPrime));
        if (!IsFinite(first)) return AlgorithmStatus.NumericalFailure;
        if (order == 1) return AlgorithmStatus.Success;

        // μ'' = −(x'ᵀHx')/(∇φ·v); x'' = μ''v (§16.3).
        if (AnalyticImplicitEvaluation.Evaluate(in selectedSurface, in point, 2, out var jet2)
            != AlgorithmStatus.Success)
            return AlgorithmStatus.Unsupported;
        var curvature = jet2.Hxx * first.X * first.X
            + 2 * jet2.Hxy * first.X * first.Y
            + 2 * jet2.Hxz * first.X * first.Z
            + jet2.Hyy * first.Y * first.Y
            + 2 * jet2.Hyz * first.Y * first.Z
            + jet2.Hzz * first.Z * first.Z;
        var muDoublePrime = -curvature / slope;
        second = Scale(anchor.LineDirection, muDoublePrime);
        return IsFinite(second) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    private static double GradientNormSq(in KernelVector3 gradient)
        => gradient.X * gradient.X + gradient.Y * gradient.Y + gradient.Z * gradient.Z;

    private static KernelVector3 Add(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
