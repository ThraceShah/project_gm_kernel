using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Local elimination constraints for swept / spun sheets (spec §9.4, task T11
/// math). Not wired into icurve prepare — analytic-only prepare stays.
/// </summary>
internal static class SweptSpunImplicit
{
    /// <summary>
    /// Swept sheet R(u,v)=C(u)+v D: foot constraint Eᵀ(x−C(u))=0 with
    /// E = unit direction orthogonal to D in the (C',D) plane when well-defined.
    /// Here the residual is simply (x−C)·(D×C') — vanishing iff x−C lies in span{D,C'}.
    /// </summary>
    internal static AlgorithmStatus SweptEliminationResidual(
        in KernelVector3 point, in KernelVector3 sectionPoint, in KernelVector3 sectionTangent,
        in KernelVector3 sweepDirection, out double residual, out KernelVector3 gradientWrtPoint)
    {
        residual = 0;
        gradientWrtPoint = default;
        if (!IsFinite(point) || !IsFinite(sectionPoint) || !IsFinite(sectionTangent)
            || !IsFinite(sweepDirection))
            return AlgorithmStatus.InvalidInput;
        var normal = Cross(sweepDirection, sectionTangent);
        var n2 = Dot(normal, normal);
        if (!(n2 > 1e-30)) return AlgorithmStatus.Singular; // D ∥ C'
        residual = Dot(Sub(point, sectionPoint), normal);
        gradientWrtPoint = normal;
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Spun-sheet elimination constraints (spec §9.4): eliminate the rotation
    /// angle with TWO scalar equations — axial height agreement and squared
    /// radial length agreement about the axis (P, Â):
    ///   h₁(x,u) = Â·(x−P) − Â·(C(u)−P)                (axial height, length)
    ///   h₂(x,u) = ‖P⊥(x−P)‖² − ‖P⊥(C(u)−P)‖²           (radial length²; P⊥ = I − ÂÂᵀ)
    /// The rotation angle itself is recovered separately, with its periodic
    /// lift, by <see cref="TryRecoverSpunAngle"/> — the single scalar
    /// ((x−P)×Â)·((C−P)×Â) used previously vanishes on ORTHOGONAL radial
    /// vectors, not on a shared meridian plane, and its gradient was negated;
    /// both are retired (consolidated review N9).
    /// Derivatives are analytic in x and u (§16.1: no differencing of nested
    /// evaluation): ∇ₓh₁ = Â, ∇ₓh₂ = 2·P⊥(x−P), ∂ᵤh₁ = −Â·C′(u),
    /// ∂ᵤh₂ = −2·(P⊥(C(u)−P))·C′(u).
    /// </summary>
    internal static AlgorithmStatus SpunConstraints(
        in KernelVector3 point, in KernelVector3 profilePoint, in KernelVector3 profileDerivative,
        in KernelVector3 axisPoint, in KernelVector3 axis,
        out double axialResidual, out double radialResidual,
        out KernelVector3 axialGradientX, out KernelVector3 radialGradientX,
        out double axialDerivativeU, out double radialDerivativeU)
    {
        axialResidual = 0;
        radialResidual = 0;
        axialGradientX = default;
        radialGradientX = default;
        axialDerivativeU = 0;
        radialDerivativeU = 0;
        if (!IsFinite(point) || !IsFinite(profilePoint) || !IsFinite(profileDerivative)
            || !IsFinite(axisPoint) || !IsFinite(axis))
            return AlgorithmStatus.InvalidInput;
        var a2 = Dot(axis, axis);
        if (!(a2 > 1e-30)) return AlgorithmStatus.Singular;
        var unitAxis = Scale(axis, 1 / Math.Sqrt(a2));

        if (!TryPrepareLocalAxis(in axisPoint, in axis, a2, in unitAxis, in profilePoint,
                out var localAxisPoint, out var axisDriftBoundSq))
            return AlgorithmStatus.NumericalFailure;

        var rx = Sub(point, localAxisPoint);
        var rc = Sub(profilePoint, localAxisPoint);
        var zx = Dot(unitAxis, rx);
        var zc = Dot(unitAxis, rc);

        // Axis points (§9.4 "轴上点…单独处理"): the radial row degenerates
        // (∇ₓh₂ = 0) — refuse instead of publishing an unconstrained equation.
        // The degeneracy test is translation-invariant along the axis via the local
        // axis point at C's height: tolerance scales with the local radial profile size (‖radialC‖),
        // with a tight term bounding local projection cancellation error.
        var radialX = Sub(rx, Scale(unitAxis, zx));
        var radialC = Sub(rc, Scale(unitAxis, zc));
        var radNormC = Math.Sqrt(Dot(radialC, radialC));
        if (axisDriftBoundSq > 1e-12 * Math.Max(1.0, Dot(radialC, radialC)))
            return AlgorithmStatus.NumericalFailure;

        var tolC = 1e-12 * Math.Max(1.0, radNormC) + 2e-16 * Math.Abs(zc);
        if (radNormC <= tolC) return AlgorithmStatus.Singular;

        var radNormX = Math.Sqrt(Dot(radialX, radialX));
        var tolX = 1e-12 * Math.Max(1.0, radNormC) + 2e-16 * Math.Abs(zx);
        if (radNormX <= tolX) return AlgorithmStatus.Singular;

        axialResidual = Dot(unitAxis, Sub(point, profilePoint));
        radialResidual = Dot(radialX, radialX) - Dot(radialC, radialC);
        axialGradientX = unitAxis;
        radialGradientX = Scale(radialX, 2);
        axialDerivativeU = -Dot(unitAxis, profileDerivative);
        radialDerivativeU = -2 * Dot(radialC, profileDerivative);
        if (!IsFinite(axialGradientX) || !IsFinite(radialGradientX)
            || !double.IsFinite(axialResidual) || !double.IsFinite(radialResidual)
            || !double.IsFinite(axialDerivativeU) || !double.IsFinite(radialDerivativeU))
            return AlgorithmStatus.NumericalFailure;
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Recover the spun rotation angle θ of x in the section frame at the
    /// profile point C(u) and lift it onto the branch of
    /// <paramref name="previousAngle"/> (periodic lift, §9.4). The frame is
    /// E₁ = unit(P⊥(C−P)), E₂ = Â×E₁; θ = atan2(P⊥(x−P)·E₂, P⊥(x−P)·E₁).
    /// Returns false when the point sits on the axis or the frame degenerates.
    /// </summary>
    internal static bool TryRecoverSpunAngle(
        in KernelVector3 point, in KernelVector3 profilePoint,
        in KernelVector3 axisPoint, in KernelVector3 axis,
        double previousAngle, out double angle)
    {
        angle = 0;
        if (!IsFinite(point) || !IsFinite(profilePoint) || !IsFinite(axisPoint) || !IsFinite(axis)
            || !double.IsFinite(previousAngle))
            return false;
        var a2 = Dot(axis, axis);
        if (!(a2 > 1e-30)) return false;
        var unitAxis = Scale(axis, 1 / Math.Sqrt(a2));

        if (!TryPrepareLocalAxis(in axisPoint, in axis, a2, in unitAxis, in profilePoint,
                out var localAxisPoint, out var axisDriftBoundSq))
            return false;

        var rx = Sub(point, localAxisPoint);
        var rc = Sub(profilePoint, localAxisPoint);
        var zx = Dot(unitAxis, rx);
        var zc = Dot(unitAxis, rc);
        var radialX = Sub(rx, Scale(unitAxis, zx));
        var radialC = Sub(rc, Scale(unitAxis, zc));
        var radNormC = Math.Sqrt(Dot(radialC, radialC));
        if (axisDriftBoundSq > 1e-12 * Math.Max(1.0, Dot(radialC, radialC)))
            return false;

        var tolC = 1e-12 * Math.Max(1.0, radNormC) + 2e-16 * Math.Abs(zc);
        if (radNormC <= tolC) return false;
        var e1 = Scale(radialC, 1 / radNormC);
        var e2 = Cross(unitAxis, e1);
        var radNormX = Math.Sqrt(Dot(radialX, radialX));
        var tolX = 1e-12 * Math.Max(1.0, radNormC) + 2e-16 * Math.Abs(zx);
        if (radNormX <= tolX) return false; // point on the axis: angle undefined
        var cosTheta = Dot(radialX, e1) / radNormX;
        var sinTheta = Dot(radialX, e2) / radNormX;
        var theta = Math.Atan2(sinTheta, cosTheta);
        if (!double.IsFinite(theta)) return false;
        // Periodic lift onto the previous branch (witness), mirroring the
        // spine-angle unwrap discipline: nearest 2π translate of the witness.
        var twoPi = 2.0 * Math.PI;
        var k = Math.Round((previousAngle - theta) / twoPi);
        angle = theta + k * twoPi;
        return true;
    }

    private static bool TryPrepareLocalAxis(
        in KernelVector3 axisPoint, in KernelVector3 axis, double a2,
        in KernelVector3 unitAxis, in KernelVector3 profilePoint,
        out KernelVector3 localAxisPoint, out double axisDriftBoundSq)
    {
        localAxisPoint = default;
        axisDriftBoundSq = double.PositiveInfinity;

        var d = Sub(profilePoint, axisPoint);
        var t = Dot(d, axis) / a2;
        if (!double.IsFinite(t)) return false;

        localAxisPoint = Vector(
            Math.FusedMultiplyAdd(axis.X, t, axisPoint.X),
            Math.FusedMultiplyAdd(axis.Y, t, axisPoint.Y),
            Math.FusedMultiplyAdd(axis.Z, t, axisPoint.Z));

        if (!IsFinite(localAxisPoint)) return false;

        // FMA computes localAxisPoint = axisPoint + t * axis + e with a single final rounding.
        // In exact arithmetic, axisPoint + t * axis lies strictly on the axis line for any finite t.
        // The perpendicular deviation of localAxisPoint from the line {axisPoint + s * axis}
        // is the transverse component of the FMA rounding error:
        //   delta_axis = ||(I - unitAxis * unitAxis^T) e|| = ||e x unitAxis|| <= ||bound_e x unitAxis||.
        // For each component, IEEE 754 round-to-nearest error satisfies |e_i| <= 0.5 * ulp(localAxisPoint_i).
        // Using BitIncrement provides a conservative, platform-independent bound on ulp.
        var absX = Math.Abs(localAxisPoint.X);
        var absY = Math.Abs(localAxisPoint.Y);
        var absZ = Math.Abs(localAxisPoint.Z);

        var ulpX = Math.BitIncrement(absX) - absX;
        var ulpY = Math.BitIncrement(absY) - absY;
        var ulpZ = Math.BitIncrement(absZ) - absZ;

        var uax = Math.Abs(unitAxis.X);
        var uay = Math.Abs(unitAxis.Y);
        var uaz = Math.Abs(unitAxis.Z);

        var bx = ulpY * uaz + ulpZ * uay;
        var by = ulpZ * uax + ulpX * uaz;
        var bz = ulpX * uay + ulpY * uax;

        axisDriftBoundSq = bx * bx + by * by + bz * bz;
        return double.IsFinite(axisDriftBoundSq);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
