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
                out var localAxisPoint, out var localAxisCorrection, out var axisDriftBoundSq))
            return AlgorithmStatus.NumericalFailure;

        var rx = SubtractCompensated(in point, in localAxisPoint, in localAxisCorrection);
        var rc = SubtractCompensated(in profilePoint, in localAxisPoint, in localAxisCorrection);
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
                out var localAxisPoint, out var localAxisCorrection, out var axisDriftBoundSq))
            return false;

        var rx = SubtractCompensated(in point, in localAxisPoint, in localAxisCorrection);
        var rc = SubtractCompensated(in profilePoint, in localAxisPoint, in localAxisCorrection);
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
        out KernelVector3 localAxisPoint, out KernelVector3 localAxisCorrection,
        out double axisDriftBoundSq)
    {
        localAxisPoint = default;
        localAxisCorrection = default;
        axisDriftBoundSq = double.PositiveInfinity;

        var d = Sub(profilePoint, axisPoint);
        var t = Dot(d, axis) / a2;
        if (!double.IsFinite(t)) return false;

        // Error-free transformation of P_i + A_i * t:
        //   p_i + pi_i = A_i * t           (TwoProd via FMA)
        //   s_i + sigma_i = P_i + p_i      (Knuth TwoSum)
        //   local_i = FMA(A_i, t, P_i)
        // In exact real arithmetic, P + t * A lies strictly on the axis line for any finite t,
        // and local_i - (P_i + A_i * t) = (local_i - s_i) - (sigma_i + pi_i).
        // Subtracting local_i and adding back e_i = (local_i - s_i) - (sigma_i + pi_i)
        // recovers the exact axis point to double-double precision, with residual error
        // bounded by ulp(e_i) + ulp(sigma_i + pi_i) (and also bounded by 0.5 * ulp(local_i)).
        if (!TryFmaWithResidual(axis.X, t, axisPoint.X, out var lx, out var ex, out var errX)
            || !TryFmaWithResidual(axis.Y, t, axisPoint.Y, out var ly, out var ey, out var errY)
            || !TryFmaWithResidual(axis.Z, t, axisPoint.Z, out var lz, out var ez, out var errZ))
            return false;

        localAxisPoint = Vector(lx, ly, lz);
        localAxisCorrection = Vector(ex, ey, ez);

        var uax = Math.Abs(unitAxis.X);
        var uay = Math.Abs(unitAxis.Y);
        var uaz = Math.Abs(unitAxis.Z);

        var bx = errY * uaz + errZ * uay;
        var by = errZ * uax + errX * uaz;
        var bz = errX * uay + errY * uax;

        axisDriftBoundSq = bx * bx + by * by + bz * bz;
        return double.IsFinite(axisDriftBoundSq);
    }

    private static bool TryFmaWithResidual(
        double a, double t, double p,
        out double fmaResult, out double fmaRoundingError, out double residualBound)
    {
        fmaResult = Math.FusedMultiplyAdd(a, t, p);
        fmaRoundingError = 0;
        residualBound = double.PositiveInfinity;
        if (!double.IsFinite(fmaResult)) return false;

        var prod = a * t;
        if (!double.IsFinite(prod)) return false;
        var prodLow = Math.FusedMultiplyAdd(a, t, -prod);

        var sum = p + prod;
        if (!double.IsFinite(sum)) return false;
        var v = sum - p;
        var sumLow = (p - (sum - v)) + (prod - v);

        var delta = fmaResult - sum;
        var tau = sumLow + prodLow;
        fmaRoundingError = delta - tau;
        if (!double.IsFinite(fmaRoundingError) || !double.IsFinite(tau)) return false;

        var absFma = Math.Abs(fmaResult);
        var halfUlpFma = 0.5 * (Math.BitIncrement(absFma) - absFma);

        var absTau = Math.Abs(tau);
        var ulpTau = Math.BitIncrement(absTau) - absTau;
        var absErr = Math.Abs(fmaRoundingError);
        var ulpErr = Math.BitIncrement(absErr) - absErr;

        // After compensating by fmaRoundingError, the remaining error of (fmaResult - fmaRoundingError)
        // from the exact real (p + a * t) is bounded by 0.5 * (ulp(fmaRoundingError) + ulp(tau)),
        // and cannot exceed the uncompensated FMA rounding bound halfUlpFma.
        residualBound = Math.Min(halfUlpFma, 0.5 * (ulpErr + ulpTau));
        return double.IsFinite(residualBound);
    }

    private static KernelVector3 SubtractCompensated(
        in KernelVector3 q, in KernelVector3 localAxis, in KernelVector3 fmaRoundingError)
    {
        return Vector(
            SubtractOneCompensated(q.X, localAxis.X, fmaRoundingError.X),
            SubtractOneCompensated(q.Y, localAxis.Y, fmaRoundingError.Y),
            SubtractOneCompensated(q.Z, localAxis.Z, fmaRoundingError.Z));
    }

    private static double SubtractOneCompensated(double q, double localAxis, double fmaRoundingError)
    {
        var diff = q - localAxis;
        var v = diff - q;
        var diffLow = (q - (diff - v)) + (-localAxis - v);
        return diff + (diffLow + fmaRoundingError);
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
