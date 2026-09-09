using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Swept surface R(u, v) = C(u) + v·D with the u parameterisation inherited
/// from the section curve (XT 5.2.2.10). All v derivatives beyond the first
/// vanish; u derivatives are the section curve derivatives.
/// </summary>
internal static class SweptSurfaceEvaluation
{
    internal static AlgorithmStatus Evaluate(ReadOnlySpan<KernelVector3> section, KernelVector3 sweep, double v,
        in SurfaceDerivativeLayout layout, Span<KernelVector3> output)
    {
        if (section.Length <= layout.UOrder) return AlgorithmStatus.OutputTooSmall;
        for (DerivativeOrder j = 0; j <= layout.VOrder; j++)
        for (DerivativeOrder i = 0; i <= layout.UOrder; i++)
        {
            KernelVector3 value;
            if (j == 0) value = i == 0 ? Add(section[0], Scale(sweep, v)) : section[i];
            else if (j == 1) value = i == 0 ? sweep : default;
            else value = default;
            if (!IsFinite(value)) return AlgorithmStatus.NumericalFailure;
            output[layout.GetIndex(i, j)] = value;
        }
        return AlgorithmStatus.Success;
    }
}

/// <summary>
/// Spun surface R(u, v) = Z(u) + W(u)·cos v + (A×W(u))·sin v, where
/// W(u) = C(u) − Z(u) and Z(u) = P + ((C(u) − P)·A)A is the projection of the
/// profile onto the spin axis (XT 5.2.2.11). Z is affine in C, so with profile
/// derivatives C^(i): Z^(i) = (C^(i)·A)A (plus P at i = 0) and
/// W^(i) = C^(i) − Z^(i) + (P·A − P) folded into i = 0, giving every mixed
/// derivative in closed form through the trigonometric derivative cycle.
/// </summary>
internal static class SpunSurfaceEvaluation
{
    internal static AlgorithmStatus Evaluate(ReadOnlySpan<KernelVector3> profile, KernelVector3 basePoint,
        KernelVector3 axis, double v, in SurfaceDerivativeLayout layout, Span<KernelVector3> output)
    {
        if (profile.Length <= layout.UOrder) return AlgorithmStatus.OutputTooSmall;
        if (!double.IsFinite(v)) return AlgorithmStatus.InvalidInput;
        var (sin, cos) = Math.SinCos(v);
        for (DerivativeOrder i = 0; i <= layout.UOrder; i++)
        {
            KernelVector3 z, w;
            if (i == 0)
            {
                var d = Subtract(profile[0], basePoint);
                z = Add(basePoint, Scale(axis, Dot(d, axis)));
                w = Subtract(d, Scale(axis, Dot(d, axis)));
            }
            else
            {
                z = Scale(axis, Dot(profile[i], axis));
                w = Subtract(profile[i], z);
            }
            var wCross = Cross(axis, w);
            for (DerivativeOrder j = 0; j <= layout.VOrder; j++)
            {
                var (s, c) = Differentiate(sin, cos, j);
                // Z is independent of v, so only the j = 0 entries keep it.
                var value = j == 0 ? Add(z, Add(Scale(w, c), Scale(wCross, s)))
                    : Add(Scale(w, c), Scale(wCross, s));
                if (!IsFinite(value)) return AlgorithmStatus.NumericalFailure;
                output[layout.GetIndex(i, j)] = value;
            }
        }
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 Subtract(in KernelVector3 a, in KernelVector3 b) => Add(a, Scale(b, -1));
}
