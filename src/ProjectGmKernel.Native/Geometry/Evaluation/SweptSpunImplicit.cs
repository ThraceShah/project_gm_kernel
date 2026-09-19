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
    /// Spun sheet about axis (P,A): the meridional plane through x has
    /// residual ((x−P)×A)·((C−P)×A) style reduced to the scalar
    /// ((x−P) − ((x−P)·A)A) · W_perp_sign — here the planar residual
    /// ((x−P)×A) · ((C−P)×A) = 0 forces x and C to share a meridional plane.
    /// </summary>
    internal static AlgorithmStatus SpunMeridianResidual(
        in KernelVector3 point, in KernelVector3 profilePoint,
        in KernelVector3 axisPoint, in KernelVector3 axis,
        out double residual, out KernelVector3 gradientWrtPoint)
    {
        residual = 0;
        gradientWrtPoint = default;
        if (!IsFinite(point) || !IsFinite(profilePoint) || !IsFinite(axisPoint) || !IsFinite(axis))
            return AlgorithmStatus.InvalidInput;
        var a2 = Dot(axis, axis);
        if (!(a2 > 1e-30)) return AlgorithmStatus.Singular;
        var unitA = Scale(axis, 1 / Math.Sqrt(a2));
        var wx = Cross(Sub(point, axisPoint), unitA);
        var wc = Cross(Sub(profilePoint, axisPoint), unitA);
        residual = Dot(wx, wc);
        // ∂/∂x of ( (x−P)×A ) · wc = wc × A  (since d((x−P)×A)= dx×A).
        gradientWrtPoint = Cross(wc, unitA);
        // Actually ∂/∂x [(x−P)×A] = -[A]_× so ∇_x (w·wc) = −A×wc = wc×A. OK.
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
