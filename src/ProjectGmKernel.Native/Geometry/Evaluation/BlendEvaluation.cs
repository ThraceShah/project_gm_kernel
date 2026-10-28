using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
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
        var outputCount = order switch { 0 => 1, 1 => 3, _ => 6 };
        if (output.Length < outputCount) return AlgorithmStatus.OutputTooSmall;
        if (!double.IsFinite(u) || !double.IsFinite(v)) return AlgorithmStatus.InvalidInput;
        if (!(frame.Radius > 0) || !double.IsFinite(frame.Arc) || !IsFinite(frame.Spine)) return AlgorithmStatus.InvalidInput;

        Span<KernelVector3> result = stackalloc KernelVector3[6];
        var theta = v * frame.Arc;
        var (sin, cos) = Math.SinCos(theta);
        var e = Add(Scale(frame.X, cos), Scale(frame.Y, sin));
        result[0] = Add(frame.Spine, Scale(e, frame.Radius));
        if (!IsFinite(result[0])) return AlgorithmStatus.NumericalFailure;
        if (order == 0)
        {
            result[..outputCount].CopyTo(output);
            return AlgorithmStatus.Success;
        }

        // B_u = C' + r(E₁ + v·a'·V); B_v = r·a·V (§10.3).
        var e1 = Add(Scale(frame.XD1, cos), Scale(frame.YD1, sin));
        var vVector = Sub(Scale(frame.Y, cos), Scale(frame.X, sin));
        result[1] = Add(frame.SpineD1, Scale(Add(e1, Scale(vVector, v * frame.ArcD1)), frame.Radius));
        result[2] = Scale(vVector, frame.Radius * frame.Arc);
        if (!IsFinite(result[1]) || !IsFinite(result[2])) return AlgorithmStatus.NumericalFailure;
        if (order == 1)
        {
            result[..outputCount].CopyTo(output);
            return AlgorithmStatus.Success;
        }

        // B_uu = C'' + r{X''c + Y''s + 2v·a'·V₁ + v·a''·V − (v·a')²·E};
        // B_uv = r{a·V₁ + a'·V − a·v·a'·E}; B_vv = −r·a²·E (§10.3), where
        // V₁ = −X's + Y'c holds θ fixed and the moving-θ contributions appear
        // as the explicit −E terms.
        var e2 = Add(Scale(frame.XD2, cos), Scale(frame.YD2, sin));
        var v1 = Sub(Scale(frame.YD1, cos), Scale(frame.XD1, sin));
        var vu = v * frame.ArcD1;
        result[3] = Add(frame.SpineD2, Scale(
            Add(Add(Add(e2, Scale(v1, 2 * vu)), Scale(vVector, v * frame.ArcD2)), Scale(e, -vu * vu)),
            frame.Radius));
        result[4] = Scale(Add(Add(Scale(v1, frame.Arc), Scale(vVector, frame.ArcD1)), Scale(e, -frame.Arc * vu)),
            frame.Radius);
        result[5] = Scale(e, -frame.Radius * frame.Arc * frame.Arc);
        if (!IsFinite(result[3]) || !IsFinite(result[4]) || !IsFinite(result[5]))
            return AlgorithmStatus.NumericalFailure;
        result[..outputCount].CopyTo(output);
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
    /// bare acos. Near-coincident contacts are refused; near-antipodal contacts
    /// are refused unless a finite <paramref name="previousArc"/> witness is
    /// supplied to disambiguate the ±π branch (GATE-A owns the unwitnessed
    /// extreme-degeneracy contract).
    /// </summary>
    internal static AlgorithmStatus TryBuildFrame(in KernelVector3 spinePoint, in KernelVector3 spineTangent,
        in KernelVector3 contactStart, in KernelVector3 contactEnd, double radius, out KernelVector3 x,
        out KernelVector3 y, out double arc, double previousArc = double.NaN)
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
        var tUnit = Unit(spineTangent);
        if (!IsFinite(tUnit) || !(Dot(tUnit, tUnit) > 0.5))
            return AlgorithmStatus.Singular;
        var cross = Cross(tUnit, startX);
        if (!(Dot(cross, cross) > 1e-18))
            return AlgorithmStatus.Singular; // T ∥ X: no valid cross-section
        var yCandidate = Unit(cross);
        if (!IsFinite(yCandidate) || !(Dot(yCandidate, yCandidate) > 0.5))
            return AlgorithmStatus.Singular;
        var cosA = Dot(endX, startX);
        var sinA = Dot(endX, yCandidate);
        if (Math.Abs(cosA * cosA + sinA * sinA - 1.0) > 1e-6)
            return AlgorithmStatus.InvalidInput; // endX does not lie in span(X, Y)
        arc = Math.Atan2(sinA, cosA);

        // Degenerate arcs: near-coincident contacts (a ≈ 0) are always refused;
        // near-antipodal ones (a ≈ ±π, ambiguous Y direction) are refused when
        // no continuity witness is available (§10.2, GATE-A).
        if (Math.Abs(sinA) < 1e-9 && (cosA >= 0 || !double.IsFinite(previousArc)))
            return AlgorithmStatus.Singular;

        if (double.IsFinite(previousArc))
            arc += 2 * Math.PI * Math.Round((previousArc - arc) / (2 * Math.PI));

        x = startX;
        y = yCandidate;
        return double.IsFinite(arc) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// Analytically propagate the spine D0..D2 and contact D0..D2 jets into a
    /// complete <see cref="BlendFrameInput"/> (X, XD1, XD2, Y, YD1, YD2, Arc,
    /// ArcD1, ArcD2) by the chain rule (§10.2–§10.3) without <c>spineD3</c>,
    /// using the cross-section identity <c>P(u) = X_end(u) − (X_end(u)·X(u))X(u) = sin(a(u))·Y(u)</c>.
    /// Refuses antipodal/collinear sections (<c>|sin a| ≤ 1e-7</c>) where <c>spineD3</c>
    /// is required to disambiguate <c>Y''</c>.
    /// </summary>
    internal static AlgorithmStatus TryBuildFrameJet(
        in KernelVector3 spine, in KernelVector3 spineD1, in KernelVector3 spineD2,
        in KernelVector3 contactStart, in KernelVector3 contactStartD1, in KernelVector3 contactStartD2,
        in KernelVector3 contactEnd, in KernelVector3 contactEndD1, in KernelVector3 contactEndD2,
        double radius, out BlendFrameInput frame, double previousArc = double.NaN)
    {
        frame = default;
        var status = TryBuildFrame(
            in spine, in spineD1, in contactStart, in contactEnd, radius,
            out var x, out var y, out var arc, previousArc);
        if (status != AlgorithmStatus.Success)
            return status;
        if (!IsFinite(spineD2)
            || !IsFinite(contactStartD1) || !IsFinite(contactStartD2)
            || !IsFinite(contactEndD1) || !IsFinite(contactEndD2))
            return AlgorithmStatus.InvalidInput;

        var invR = 1 / radius;
        var xD1 = Scale(Sub(contactStartD1, spineD1), invR);
        var xD2 = Scale(Sub(contactStartD2, spineD2), invR);

        var endX = Scale(Sub(contactEnd, spine), invR);
        var endXD1 = Scale(Sub(contactEndD1, spineD1), invR);
        var endXD2 = Scale(Sub(contactEndD2, spineD2), invR);

        var c = Dot(endX, x);
        var cD1 = Dot(endXD1, x) + Dot(endX, xD1);
        var cD2 = Dot(endXD2, x) + 2 * Dot(endXD1, xD1) + Dot(endX, xD2);

        // P(u) = X_end(u) − c(u)·X(u) = s(u)·Y(u) with s(u) = sin a(u).
        var s = Dot(endX, y);
        if (!(Math.Abs(s) > 1e-7) || !double.IsFinite(s))
            return AlgorithmStatus.Singular; // collinear contacts require explicit spineD3

        var pD1 = Sub(Sub(endXD1, Scale(x, cD1)), Scale(xD1, c));
        var pD2 = Sub(Sub(Sub(endXD2, Scale(x, cD2)), Scale(xD1, 2 * cD1)), Scale(xD2, c));

        var invS = 1 / s;
        var sD1 = Dot(y, pD1);
        var yD1 = Scale(Sub(pD1, Scale(y, sD1)), invS);
        var sD2 = Dot(y, pD2) + s * Dot(yD1, yD1);
        var yD2 = Scale(Sub(Sub(pD2, Scale(yD1, 2 * sD1)), Scale(y, sD2)), invS);

        var rho2 = c * c + s * s;
        if (!(rho2 > 1e-18) || !double.IsFinite(rho2))
            return AlgorithmStatus.Singular;
        var arcD1 = (c * sD1 - s * cD1) / rho2;
        var arcD2 = ((c * sD2 - s * cD2) - 2 * (c * cD1 + s * sD1) * arcD1) / rho2;
        if (!IsFinite(xD1) || !IsFinite(xD2) || !IsFinite(yD1) || !IsFinite(yD2)
            || !double.IsFinite(arcD1) || !double.IsFinite(arcD2))
            return AlgorithmStatus.NumericalFailure;

        frame = new BlendFrameInput(
            in spine, in spineD1, in spineD2,
            in x, in xD1, in xD2,
            in y, in yD1, in yD2,
            arc, arcD1, arcD2, radius);
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Analytically propagate the spine D0..D3 (including <paramref name="spineD3"/>
    /// for <c>Y''</c> when the spine torsion/curvature varies) and contact D0..D2
    /// jets into a complete <see cref="BlendFrameInput"/> (§10.2–§10.3).
    /// </summary>
    internal static AlgorithmStatus TryBuildFrameJet(
        in KernelVector3 spine, in KernelVector3 spineD1, in KernelVector3 spineD2, in KernelVector3 spineD3,
        in KernelVector3 contactStart, in KernelVector3 contactStartD1, in KernelVector3 contactStartD2,
        in KernelVector3 contactEnd, in KernelVector3 contactEndD1, in KernelVector3 contactEndD2,
        double radius, out BlendFrameInput frame, double previousArc = double.NaN)
    {
        frame = default;
        var status = TryBuildFrame(
            in spine, in spineD1, in contactStart, in contactEnd, radius,
            out var x, out var y, out var arc, previousArc);
        if (status != AlgorithmStatus.Success)
            return status;
        if (!IsFinite(spineD2) || !IsFinite(spineD3)
            || !IsFinite(contactStartD1) || !IsFinite(contactStartD2)
            || !IsFinite(contactEndD1) || !IsFinite(contactEndD2))
            return AlgorithmStatus.InvalidInput;

        var invR = 1 / radius;
        var xD1 = Scale(Sub(contactStartD1, spineD1), invR);
        var xD2 = Scale(Sub(contactStartD2, spineD2), invR);

        var endX = Scale(Sub(contactEnd, spine), invR);
        var endXD1 = Scale(Sub(contactEndD1, spineD1), invR);
        var endXD2 = Scale(Sub(contactEndD2, spineD2), invR);

        // W = C' × X, W' = C'' × X + C' × X', W'' = C''' × X + 2(C'' × X') + C' × X''.
        var wVec = Cross(spineD1, x);
        var wNorm = Math.Sqrt(Dot(wVec, wVec));
        if (!(wNorm > 1e-15) || !double.IsFinite(wNorm))
            return AlgorithmStatus.Singular;
        var invW = 1 / wNorm;
        var wD1 = Add(Cross(spineD2, x), Cross(spineD1, xD1));
        var wD2 = Add(Add(Cross(spineD3, x), Scale(Cross(spineD2, xD1), 2)), Cross(spineD1, xD2));

        var yDotWD1 = Dot(y, wD1);
        var yD1 = Scale(Sub(wD1, Scale(y, yDotWD1)), invW);
        var yDotWD2 = Dot(y, wD2);
        var yD1DotWD1 = Dot(yD1, wD1);
        var yD2 = Scale(
            Sub(Sub(wD2, Scale(y, yDotWD2 + yD1DotWD1)), Scale(yD1, 2 * yDotWD1)),
            invW);

        // c = X_end · X, s = X_end · Y, and their first/second u-derivatives.
        var c = Dot(endX, x);
        var cD1 = Dot(endXD1, x) + Dot(endX, xD1);
        var cD2 = Dot(endXD2, x) + 2 * Dot(endXD1, xD1) + Dot(endX, xD2);

        var s = Dot(endX, y);
        var sD1 = Dot(endXD1, y) + Dot(endX, yD1);
        var sD2 = Dot(endXD2, y) + 2 * Dot(endXD1, yD1) + Dot(endX, yD2);

        var rho2 = c * c + s * s;
        if (!(rho2 > 1e-18) || !double.IsFinite(rho2))
            return AlgorithmStatus.Singular;
        var arcD1 = (c * sD1 - s * cD1) / rho2;
        var arcD2 = ((c * sD2 - s * cD2) - 2 * (c * cD1 + s * sD1) * arcD1) / rho2;
        if (!IsFinite(xD1) || !IsFinite(xD2) || !IsFinite(yD1) || !IsFinite(yD2)
            || !double.IsFinite(arcD1) || !double.IsFinite(arcD2))
            return AlgorithmStatus.NumericalFailure;

        frame = new BlendFrameInput(
            in spine, in spineD1, in spineD2,
            in x, in xD1, in xD2,
            in y, in yD1, in yD2,
            arc, arcD1, arcD2, radius);
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Contact point from a spine witness and support UV without re-projection
    /// (§10.2): Q_j = S_j(u_j, v_j). The normal is sense-resolved for the
    /// separate spine-center consistency check; it is not applied to Q_j.
    /// </summary>
    internal static AlgorithmStatus TryContactFromSpineWitness(in AnalyticSurface support,
        double u, double v, double radius, KernelSense sense,
        out KernelVector3 contact, out KernelVector3 unitNormal)
    {
        contact = unitNormal = default;
        if (!(radius > 0)) return AlgorithmStatus.InvalidInput;
        if (!SurfaceDerivativeLayout.TryCreate(1, 1, out var layout))
            return AlgorithmStatus.InvalidInput;
        Span<KernelVector3> jet = stackalloc KernelVector3[4];
        if (SurfaceEvaluation.Evaluate(in support, u, v, in layout, jet) != AlgorithmStatus.Success)
            return AlgorithmStatus.NotConverged;
        var su = jet[layout.GetIndex(1, 0)];
        var sv = jet[layout.GetIndex(0, 1)];
        var n = Cross(su, sv);
        var n2 = Dot(n, n);
        if (!(n2 > 1e-30)) return AlgorithmStatus.Singular;
        unitNormal = Scale(n, 1 / Math.Sqrt(n2));
        if (sense == ParasolidConstants.PK_TOPOL_sense_negative_c)
            unitNormal = Scale(unitNormal, -1);
        contact = jet[0];
        return IsFinite(contact) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }
}
