using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Frame and arc inputs of one regular rolling-ball blend cross-section,
/// already recovered from the spine witness (§10.2). All derivative slots are
/// with respect to the spine parameter u; the caller's preparation graph
/// decides the required orders (§16.4) — this struct never derives them by
/// differencing the full evaluation.
/// </summary>
internal readonly struct BlendFrameInput
{
    internal readonly KernelVector3 Spine;        // C(u)
    internal readonly KernelVector3 SpineD1;      // C'(u)
    internal readonly KernelVector3 SpineD2;      // C''(u)
    internal readonly KernelVector3 X;            // (Q_start − c)/r
    internal readonly KernelVector3 XD1;
    internal readonly KernelVector3 XD2;
    internal readonly KernelVector3 Y;            // directed arc completion
    internal readonly KernelVector3 YD1;
    internal readonly KernelVector3 YD2;
    internal readonly double Arc;                 // a(u)
    internal readonly double ArcD1;               // a'(u)
    internal readonly double ArcD2;               // a''(u)
    internal readonly double Radius;              // r > 0

    internal BlendFrameInput(in KernelVector3 spine, in KernelVector3 spineD1, in KernelVector3 spineD2,
        in KernelVector3 x, in KernelVector3 xd1, in KernelVector3 xd2,
        in KernelVector3 y, in KernelVector3 yd1, in KernelVector3 yd2,
        double arc, double arcD1, double arcD2, double radius)
    {
        Spine = spine;
        SpineD1 = spineD1;
        SpineD2 = spineD2;
        X = x;
        XD1 = xd1;
        XD2 = xd2;
        Y = y;
        YD1 = yd1;
        YD2 = yd2;
        Arc = arc;
        ArcD1 = arcD1;
        ArcD2 = arcD2;
        Radius = radius;
    }
}

/// <summary>
/// Regular rolling-ball blend parameter evaluation (spec §10.1–§10.3, task
/// T12): B(u,v) = C(u) + r·{X cos θ + Y sin θ}, θ = v·a(u), with the D1/D2
/// formulas of §10.3 assembled from the frame jets. The v range endpoints map
/// to the sense-resolved contact points chosen by the caller; arc expansion
/// policy and public extension ranges stay behind GATE-A.
/// </summary>
internal static class BlendEvaluation
{
    internal const int MaxParameterOrder = 2;

    /// <summary>
    /// Acceptance gate for the ordinary r&gt;0 construction (§10.1): the radius
    /// must be positive and the signed support ranges consistent with it.
    /// Inconsistent |range0|/|range1| are refused — never averaged away.
    /// </summary>
    internal static bool IsRegularConstruction(double radius, double range0, double range1)
    {
        if (!(radius > 0) || !double.IsFinite(radius)) return false;
        const double tolerance = 1e-9;
        return Math.Abs(Math.Abs(range0) - radius) <= tolerance * radius
            && Math.Abs(Math.Abs(range1) - radius) <= tolerance * radius;
    }

    /// <summary>
    /// Evaluate B and, requested, the parameter derivatives. Output layout:
    /// position, B_u, B_v, B_uu, B_uv, B_vv (as requested by
    /// <paramref name="order"/>: 0 → position, 1 → +first derivatives,
    /// 2 → +second derivatives).
    /// </summary>
    internal static AlgorithmStatus Evaluate(in BlendFrameInput frame, double u, double v,
        DerivativeOrder order, Span<KernelVector3> output)
    {
        if (order < 0 || order > MaxParameterOrder) return AlgorithmStatus.InvalidInput;
        if (output.Length < 1 + 2 * order) return AlgorithmStatus.OutputTooSmall;
        if (!double.IsFinite(u) || !double.IsFinite(v)) return AlgorithmStatus.InvalidInput;
        if (!(frame.Radius > 0) || !double.IsFinite(frame.Arc) || !IsFinite(frame.Spine)) return AlgorithmStatus.InvalidInput;

        var theta = v * frame.Arc;
        var (sin, cos) = Math.SinCos(theta);
        var e = Add(Scale(frame.X, cos), Scale(frame.Y, sin));
        output[0] = Add(frame.Spine, Scale(e, frame.Radius));
        if (!IsFinite(output[0])) return AlgorithmStatus.NumericalFailure;
        if (order == 0) return AlgorithmStatus.Success;

        // B_u = C' + r(E₁ + v·a'·V); B_v = r·a·V (§10.3).
        var e1 = Add(Scale(frame.XD1, cos), Scale(frame.YD1, sin));
        var vVector = Sub(Scale(frame.Y, cos), Scale(frame.X, sin));
        output[1] = Add(frame.SpineD1, Scale(Add(e1, Scale(vVector, v * frame.ArcD1)), frame.Radius));
        output[2] = Scale(vVector, frame.Radius * frame.Arc);
        if (!IsFinite(output[1]) || !IsFinite(output[2])) return AlgorithmStatus.NumericalFailure;
        if (order == 1) return AlgorithmStatus.Success;

        // B_uu = C'' + r{X''c + Y''s + 2v·a'·V₁ + v·a''·V − (v·a')²·E};
        // B_uv = r{a·V₁ + a'·V − a·v·a'·E}; B_vv = −r·a²·E (§10.3), where
        // V₁ = −X's + Y'c holds θ fixed and the moving-θ contributions appear
        // as the explicit −E terms.
        var e2 = Add(Scale(frame.XD2, cos), Scale(frame.YD2, sin));
        var v1 = Sub(Scale(frame.YD1, cos), Scale(frame.XD1, sin));
        var vu = v * frame.ArcD1;
        output[3] = Add(frame.SpineD2, Scale(
            Add(Add(Add(e2, Scale(v1, 2 * vu)), Scale(vVector, v * frame.ArcD2)), Scale(e, -vu * vu)),
            frame.Radius));
        output[4] = Scale(Add(Add(Scale(v1, frame.Arc), Scale(vVector, frame.ArcD1)), Scale(e, -frame.Arc * vu)),
            frame.Radius);
        output[5] = Scale(e, -frame.Radius * frame.Arc * frame.Arc);
        if (!IsFinite(output[3]) || !IsFinite(output[4]) || !IsFinite(output[5]))
            return AlgorithmStatus.NumericalFailure;
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Local implicit value of the constant-radius tube around the spine
    /// branch (§10.5): φ_B(x) = ‖x − C(s(x))‖² − r² near a cached spine
    /// parameter, with D = ‖C'‖² − q·C'' deciding the elimination. This
    /// overload takes the caller's spine state; s-refinement is the caller's
    /// corrector (T13 wires the full plan).
    /// </summary>
    internal static AlgorithmStatus TryLocalImplicit(in KernelVector3 spinePoint, in KernelVector3 spineD1,
        in KernelVector3 spineD2, double radius, in KernelVector3 point, out double value,
        out KernelVector3 gradient, out double eliminationD)
    {
        value = 0;
        gradient = default;
        eliminationD = 0;
        if (!(radius > 0) || !IsFinite(point)) return AlgorithmStatus.InvalidInput;
        var q = Sub(point, spinePoint);
        value = Dot(q, q) - radius * radius;
        gradient = Scale(q, 2);
        eliminationD = Dot(spineD1, spineD1) - Dot(q, spineD2);
        return double.IsFinite(value) && double.IsFinite(eliminationD) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>
    /// Recover the cross-section frame from the spine witness and the two
    /// sense-resolved contact points (§10.2): X = (Q_start − c)/r, Y from the
    /// spine tangent with the directed-arc convention, a via atan2 — never a
    /// bare acos. Near-coincident and near-antipodal contacts are refused
    /// (GATE-A owns the extreme-degeneracy contract).
    /// </summary>
    internal static AlgorithmStatus TryBuildFrame(in KernelVector3 spinePoint, in KernelVector3 spineTangent,
        in KernelVector3 contactStart, in KernelVector3 contactEnd, double radius, out KernelVector3 x,
        out KernelVector3 y, out double arc)
    {
        x = y = default;
        arc = 0;
        if (!(radius > 0) || !IsFinite(spinePoint) || !IsFinite(spineTangent)
            || !IsFinite(contactStart) || !IsFinite(contactEnd))
            return AlgorithmStatus.InvalidInput;
        if (Dot(spineTangent, spineTangent) <= 0)
            return AlgorithmStatus.Singular; // zero spine tangent

        var startX = Scale(Sub(contactStart, spinePoint), 1 / radius);
        var endX = Scale(Sub(contactEnd, spinePoint), 1 / radius);
        var startNorm = Dot(startX, startX);
        var endNorm = Dot(endX, endX);
        if (Math.Abs(startNorm - 1) > 1e-9 || Math.Abs(endNorm - 1) > 1e-9
            || !double.IsFinite(startNorm) || !double.IsFinite(endNorm))
            return AlgorithmStatus.InvalidInput; // contacts are not at radius distance

        // Directed arc via atan2: cos a from X, sin a from Y = unit(T×X).
        var cross = Cross(spineTangent, startX);
        var yCandidate = Unit(cross);
        if (!IsFinite(yCandidate)) return AlgorithmStatus.Singular; // T ∥ X: no valid cross-section
        var cosA = Dot(endX, startX);
        var sinA = Dot(endX, yCandidate);
        arc = Math.Atan2(sinA, cosA);

        // Degenerate arcs are gate territory: near-coincident contacts (a ≈ 0)
        // and near-antipodal ones (a ≈ ±π, ambiguous Y direction) are refused
        // here instead of silently picking a branch (§10.2, GATE-A).
        if (Math.Abs(sinA) < 1e-9)
            return AlgorithmStatus.Singular;
        x = startX;
        y = yCandidate;
        return double.IsFinite(arc) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }
}
