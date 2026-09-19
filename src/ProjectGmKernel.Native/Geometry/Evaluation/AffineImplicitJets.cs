using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Affine chart / implicit jet transforms (spec §8.4, task T11). Uniform scale
/// preserves an oriented SDF after distance rescale; non-uniform A refuses the
/// distance claim and only transforms the raw implicit jet.
/// </summary>
internal static class AffineImplicitJets
{
    /// <summary>
    /// Given φ(x), ∇φ, H_φ at x, return φ̃(y)=φ(A^{-1}(y−t)), ∇̃=A^{-T}∇,
    /// H̃ = A^{-T} H A^{-1} for invertible 3×3 A (row-major).
    /// </summary>
    internal static AlgorithmStatus TransformImplicit(
        ReadOnlySpan<double> aInverseRowMajor, in KernelVector3 gradient,
        double hxx, double hxy, double hxz, double hyy, double hyz, double hzz,
        out KernelVector3 transformedGradient,
        out double thxx, out double thxy, out double thxz,
        out double thyy, out double thyz, out double thzz)
    {
        transformedGradient = default;
        thxx = thxy = thxz = thyy = thyz = thzz = 0;
        if (aInverseRowMajor.Length < 9) return AlgorithmStatus.WorkspaceTooSmall;

        // B = A^{-1}; ∇̃ = Bᵀ ∇; H̃ = Bᵀ H B.
        var b00 = aInverseRowMajor[0]; var b01 = aInverseRowMajor[1]; var b02 = aInverseRowMajor[2];
        var b10 = aInverseRowMajor[3]; var b11 = aInverseRowMajor[4]; var b12 = aInverseRowMajor[5];
        var b20 = aInverseRowMajor[6]; var b21 = aInverseRowMajor[7]; var b22 = aInverseRowMajor[8];

        transformedGradient = Vector(
            b00 * gradient.X + b10 * gradient.Y + b20 * gradient.Z,
            b01 * gradient.X + b11 * gradient.Y + b21 * gradient.Z,
            b02 * gradient.X + b12 * gradient.Y + b22 * gradient.Z);

        // HB first.
        var hb00 = hxx * b00 + hxy * b10 + hxz * b20;
        var hb01 = hxx * b01 + hxy * b11 + hxz * b21;
        var hb02 = hxx * b02 + hxy * b12 + hxz * b22;
        var hb10 = hxy * b00 + hyy * b10 + hyz * b20;
        var hb11 = hxy * b01 + hyy * b11 + hyz * b21;
        var hb12 = hxy * b02 + hyy * b12 + hyz * b22;
        var hb20 = hxz * b00 + hyz * b10 + hzz * b20;
        var hb21 = hxz * b01 + hyz * b11 + hzz * b21;
        var hb22 = hxz * b02 + hyz * b12 + hzz * b22;

        thxx = b00 * hb00 + b10 * hb10 + b20 * hb20;
        thxy = b00 * hb01 + b10 * hb11 + b20 * hb21;
        thxz = b00 * hb02 + b10 * hb12 + b20 * hb22;
        thyy = b01 * hb01 + b11 * hb11 + b21 * hb21;
        thyz = b01 * hb02 + b11 * hb12 + b21 * hb22;
        thzz = b02 * hb02 + b12 * hb12 + b22 * hb22;
        return IsFinite(transformedGradient) && double.IsFinite(thxx) && double.IsFinite(thzz)
            ? AlgorithmStatus.Success
            : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// Uniform scale λ>0: chart positions map by λ, oriented distance by λ,
    /// first derivatives unchanged in direction (scale position only).
    /// Non-uniform scale returns <see cref="AlgorithmStatus.Unsupported"/> for SDF.
    /// </summary>
    internal static AlgorithmStatus TryUniformScaleDistance(double scale, double distance,
        out double scaledDistance)
    {
        scaledDistance = 0;
        if (!(scale > 0) || !double.IsFinite(scale) || !double.IsFinite(distance))
            return AlgorithmStatus.InvalidInput;
        scaledDistance = distance * scale;
        return AlgorithmStatus.Success;
    }

    internal static bool IsUniformScale(ReadOnlySpan<double> aRowMajor, out double scale)
    {
        scale = 0;
        if (aRowMajor.Length < 9) return false;
        // Diagonal equal, off-diagonal ~0.
        var s0 = aRowMajor[0];
        var s1 = aRowMajor[4];
        var s2 = aRowMajor[8];
        if (!(s0 > 0) || Math.Abs(s0 - s1) > 1e-14 * Math.Max(1, Math.Abs(s0))
            || Math.Abs(s0 - s2) > 1e-14 * Math.Max(1, Math.Abs(s0)))
            return false;
        for (BufferOffset i = 0; i < 9; i++)
        {
            if (i is 0 or 4 or 8) continue;
            if (Math.Abs(aRowMajor[i]) > 1e-14) return false;
        }
        scale = s0;
        return true;
    }
}
