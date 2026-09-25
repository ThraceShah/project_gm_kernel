using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Assembled joint system for the spec §12.2 lowering: on a regular spine
/// patch defined as the intersection of two implicit supports A, D, find the
/// blend point x and spine point c satisfying
///   A(c) = 0, D(c) = 0, ‖x−c‖² − r² = 0, (x−c)·w(c) = 0, φ_S(x) = 0, p(x,t) = 0
/// with w(c) = ∇A×∇D left UNNORMALIZED — the direction derivative is exact
/// without normalization derivatives, and the numerical scale is the linear
/// solver's business (frozen row scaling, §12.2). State layout:
/// (x.X, x.Y, x.Z, c.X, c.Y, c.Z). This compilation of the semantics applies
/// only to regular two-support spine patches; ChartPoint and terminator
/// intervals compile their own rules (§12.2).
/// </summary>
internal static class JointBlendResidual
{
    /// <summary>Assemble the 6 residuals at (x, c). r² is passed directly.</summary>
    internal static AlgorithmStatus AssembleResidual(in AnalyticSurface supportA, in AnalyticSurface supportD,
        in AnalyticSurface outerSurface, in KernelVector3 planeAnchor, in KernelVector3 planeNormal,
        double radiusSq, in KernelVector3 x, in KernelVector3 c, Span<double> residual)
    {
        if (residual.Length < 6) return AlgorithmStatus.WorkspaceTooSmall;
        var statusA = AnalyticImplicitEvaluation.Evaluate(in supportA, in c, 1, out var jetA);
        if (statusA != AlgorithmStatus.Success) return statusA;
        var statusD = AnalyticImplicitEvaluation.Evaluate(in supportD, in c, 1, out var jetD);
        if (statusD != AlgorithmStatus.Success) return statusD;
        var statusS = AnalyticImplicitEvaluation.Evaluate(in outerSurface, in x, 1, out var jetS);
        if (statusS != AlgorithmStatus.Success) return statusS;

        var q = Sub(x, c);
        var w = Cross(jetA.Gradient, jetD.Gradient);

        residual[0] = jetA.Value;
        residual[1] = jetD.Value;
        residual[2] = Dot(q, q) - radiusSq;
        residual[3] = Dot(q, w);
        residual[4] = jetS.Value;
        residual[5] = Dot(planeNormal, Sub(x, planeAnchor));
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Assemble the 6×6 Jacobian with the §12.2 direction derivatives:
    /// δw = (H_A δc)×∇D + ∇A×(H_D δc), and
    /// δ{(x−c)·w} = (δx−δc)·w + (x−c)·δw. The w-row c-block is
    /// Mᵀ(x−c) − w with M δc = δw.
    /// </summary>
    internal static AlgorithmStatus AssembleJacobian(in AnalyticSurface supportA, in AnalyticSurface supportD,
        in AnalyticSurface outerSurface, in KernelVector3 planeNormal,
        in KernelVector3 x, in KernelVector3 c, Span<double> jacobian)
    {
        if (jacobian.Length < 36) return AlgorithmStatus.WorkspaceTooSmall;
        var statusA = AnalyticImplicitEvaluation.Evaluate(in supportA, in c, 2, out var jetA);
        var statusD = AnalyticImplicitEvaluation.Evaluate(in supportD, in c, 2, out var jetD);
        if (statusA != AlgorithmStatus.Success || statusD != AlgorithmStatus.Success) return statusA != AlgorithmStatus.Success ? statusA : statusD;
        var statusS = AnalyticImplicitEvaluation.Evaluate(in outerSurface, in x, 1, out var jetS);
        if (statusS != AlgorithmStatus.Success) return statusS;

        var q = Sub(x, c);
        var gradientA = jetA.Gradient;
        var gradientD = jetD.Gradient;
        var w = Cross(gradientA, gradientD);

        // Rows 1–2: supports depend on c only.
        for (var i = 0; i < 3; i++)
        {
            jacobian[0 * 6 + i] = 0;
            jacobian[0 * 6 + 3 + i] = GetComponent(gradientA, i);
            jacobian[1 * 6 + i] = 0;
            jacobian[1 * 6 + 3 + i] = GetComponent(gradientD, i);
        }

        // Row 3: ‖x−c‖² − r².
        for (var i = 0; i < 3; i++)
        {
            jacobian[2 * 6 + i] = 2 * GetComponent(q, i);
            jacobian[2 * 6 + 3 + i] = -2 * GetComponent(q, i);
        }

        // Row 4: (x−c)·w(c). x-block = w; c-block = Mᵀq − w where
        // M δc = (H_A δc)×∇D + ∇A×(H_D δc). Column j of M:
        // (H_A e_j)×∇D + ∇A×(H_D e_j).
        jacobian[3 * 6 + 0] = w.X;
        jacobian[3 * 6 + 1] = w.Y;
        jacobian[3 * 6 + 2] = w.Z;
        for (var j = 0; j < 3; j++)
        {
            var columnA = Scale(gradientA, 0);
            var hAe = HessianColumn(in jetA, j);
            var hDe = HessianColumn(in jetD, j);
            var mw = Add(Cross(hAe, gradientD), Cross(gradientA, hDe));
            // c-block entry (row 4, column 3+j) = q · M e_j = q·mw, then −w_j.
            jacobian[3 * 6 + 3 + j] = Dot(q, mw) - GetComponent(w, j);
        }

        // Row 5: φ_S(x).
        for (var i = 0; i < 3; i++)
        {
            jacobian[4 * 6 + i] = GetComponent(jetS.Gradient, i);
            jacobian[4 * 6 + 3 + i] = 0;
        }

        // Row 6: parameter plane (x-block only).
        for (var i = 0; i < 3; i++)
        {
            jacobian[5 * 6 + i] = GetComponent(planeNormal, i);
            jacobian[5 * 6 + 3 + i] = 0;
        }
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 HessianColumn(in ImplicitJet jet, int column) => column switch
    {
        0 => Vector(jet.Hxx, jet.Hxy, jet.Hxz),
        1 => Vector(jet.Hxy, jet.Hyy, jet.Hyz),
        _ => Vector(jet.Hxz, jet.Hyz, jet.Hzz),
    };

    private static double GetComponent(in KernelVector3 v, int i) => i switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
