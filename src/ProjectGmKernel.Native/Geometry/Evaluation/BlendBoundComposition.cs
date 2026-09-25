using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Document-literal BLEND_BOUND distance composition (spec §11.1, task T15):
/// with y = x + r₁∇d₁(x), A = I + r₁H₁(x),
///   F_B(x) = d₀(y) − r₀,
///   ∇F_B   = Aᵀ∇d₀(y),
///   ∇²F_B  = AᵀH₀(y)A + r₁ Σₖ [∇d₀(y)]ₖ ∇²(∂ₖd₁)(x).
/// The last term is the d₁ third-derivative contraction — dropping it while
/// claiming an exact Hessian is a spec violation, so the contraction is a
/// required input here, not an optimization. This is the PURE MATH MODULE
/// only: GATE-B (the role mapping of d₀/d₁ to the XT boundary/support
/// indices) is open, so nothing in this file feeds a production evaluation
/// path (§11.3).
/// </summary>
internal static class BlendBoundComposition
{
    /// <summary>
    /// Input jets at the evaluation point: d₀ evaluated at y (caller composes
    /// y = x + r₁∇d₁(x)), d₁'s gradient/Hessian at x, and the contraction
    /// T_ij = Σₖ [∇d₀(y)]ₖ ∂²(∂ₖd₁)/∂x_i∂x_j evaluated at x.
    /// </summary>
    internal readonly struct CompositionInput
    {
        internal readonly double D0Value;
        internal readonly KernelVector3 D0Gradient;
        internal readonly double D0Hxx, D0Hxy, D0Hxz, D0Hyy, D0Hyz, D0Hzz;
        internal readonly KernelVector3 D1Gradient;
        internal readonly double D1Hxx, D1Hxy, D1Hxz, D1Hyy, D1Hyz, D1Hzz;
        internal readonly double Txx, Txy, Txz, Tyx, Tyy, Tyz, Tzx, Tzy, Tzz;

        internal CompositionInput(double d0Value, in KernelVector3 d0Gradient,
            double d0Hxx, double d0Hxy, double d0Hxz, double d0Hyy, double d0Hyz, double d0Hzz,
            in KernelVector3 d1Gradient,
            double d1Hxx, double d1Hxy, double d1Hxz, double d1Hyy, double d1Hyz, double d1Hzz,
            double txx, double txy, double txz, double tyx, double tyy, double tyz,
            double tzx, double tzy, double tzz)
        {
            D0Value = d0Value;
            D0Gradient = d0Gradient;
            D0Hxx = d0Hxx; D0Hxy = d0Hxy; D0Hxz = d0Hxz;
            D0Hyy = d0Hyy; D0Hyz = d0Hyz; D0Hzz = d0Hzz;
            D1Gradient = d1Gradient;
            D1Hxx = d1Hxx; D1Hxy = d1Hxy; D1Hxz = d1Hxz;
            D1Hyy = d1Hyy; D1Hyz = d1Hyz; D1Hzz = d1Hzz;
            Txx = txx; Txy = txy; Txz = txz;
            Tyx = tyx; Tyy = tyy; Tyz = tyz;
            Tzx = tzx; Tzy = tzy; Tzz = tzz;
        }
    }

    /// <summary>Assemble the document-literal F_B jet from the input jets.</summary>
    internal static AlgorithmStatus Evaluate(in CompositionInput input, double r0, double r1,
        out double value, out KernelVector3 gradient, out double hxx, out double hxy,
        out double hxz, out double hyy, out double hyz, out double hzz)
    {
        value = 0;
        gradient = default;
        hxx = hxy = hxz = hyy = hyz = hzz = 0;
        if (!IsFinite(input.D1Gradient) || !IsFinite(input.D0Gradient))
            return AlgorithmStatus.InvalidInput;

        // A = I + r₁H₁(x) (row-major 3×3).
        var a00 = 1 + r1 * input.D1Hxx;
        var a01 = r1 * input.D1Hxy;
        var a02 = r1 * input.D1Hxz;
        var a10 = r1 * input.D1Hxy;
        var a11 = 1 + r1 * input.D1Hyy;
        var a12 = r1 * input.D1Hyz;
        var a20 = r1 * input.D1Hxz;
        var a21 = r1 * input.D1Hyz;
        var a22 = 1 + r1 * input.D1Hzz;

        value = input.D0Value - r0;

        // ∇F_B = Aᵀ∇d₀(y).
        var g = input.D0Gradient;
        gradient = Vector(
            a00 * g.X + a10 * g.Y + a20 * g.Z,
            a01 * g.X + a11 * g.Y + a21 * g.Z,
            a02 * g.X + a12 * g.Y + a22 * g.Z);

        // ∇²F_B = AᵀH₀A + r₁·T with T the D₃ contraction (§11.1).
        var h00 = input.D0Hxx; var h01 = input.D0Hxy; var h02 = input.D0Hxz;
        var h10 = input.D0Hxy; var h11 = input.D0Hyy; var h12 = input.D0Hyz;
        var h20 = input.D0Hxz; var h21 = input.D0Hyz; var h22 = input.D0Hzz;
        // (AᵀH₀A)_ij = Σ_{k,l} A_ki H_kl A_lj — assembled via H₀A first.
        var hA00 = h00 * a00 + h01 * a10 + h02 * a20;
        var hA01 = h00 * a01 + h01 * a11 + h02 * a21;
        var hA02 = h00 * a02 + h01 * a12 + h02 * a22;
        var hA10 = h10 * a00 + h11 * a10 + h12 * a20;
        var hA11 = h10 * a01 + h11 * a11 + h12 * a21;
        var hA12 = h10 * a02 + h11 * a12 + h12 * a22;
        var hA20 = h20 * a00 + h21 * a10 + h22 * a20;
        var hA21 = h20 * a01 + h21 * a11 + h22 * a21;
        var hA22 = h20 * a02 + h21 * a12 + h22 * a22;
        hxx = a00 * hA00 + a10 * hA10 + a20 * hA20 + r1 * input.Txx;
        hxy = a00 * hA01 + a10 * hA11 + a20 * hA21 + r1 * input.Txy;
        hxz = a00 * hA02 + a10 * hA12 + a20 * hA22 + r1 * input.Txz;
        hyy = a01 * hA01 + a11 * hA11 + a21 * hA21 + r1 * input.Tyy;
        hyz = a01 * hA02 + a11 * hA12 + a21 * hA22 + r1 * input.Tyz;
        hzz = a02 * hA02 + a12 * hA12 + a22 * hA22 + r1 * input.Tzz;
        return IsFinite(gradient) && double.IsFinite(hxx) && double.IsFinite(hzz)
            ? AlgorithmStatus.Success
            : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// Sphere-distance helper for tests and fixtures: the jets of
    /// d(x) = ‖x−c‖ − R at a point, including the third-derivative contraction
    /// T_ij = Σₖ gₖ ∂H_ij/∂x_k against a supplied gradient g (the d₀ gradient
    /// at y, per the document formula).
    /// </summary>
    internal static void SphereDistanceJets(in KernelVector3 center, double sphereRadius,
        in KernelVector3 point, in KernelVector3 contractionGradient, out double value,
        out KernelVector3 gradient, out double hxx, out double hxy, out double hxz,
        out double hyy, out double hyz, out double hzz,
        out double txx, out double txy, out double txz, out double tyy, out double tyz, out double tzz)
    {
        var r = Sub(point, center);
        var rho = Math.Sqrt(Dot(r, r));
        var normal = Scale(r, 1 / rho);
        value = rho - sphereRadius;
        gradient = normal;
        // H = (I − nnᵀ)/ρ.
        hxx = (1 - normal.X * normal.X) / rho;
        hxy = -normal.X * normal.Y / rho;
        hxz = -normal.X * normal.Z / rho;
        hyy = (1 - normal.Y * normal.Y) / rho;
        hyz = -normal.Y * normal.Z / rho;
        hzz = (1 - normal.Z * normal.Z) / rho;
        // ∂H_ij/∂x_k = [−(dn_i/dx_k n_j + n_i dn_j/dx_k)ρ − (δij − n_i n_j)]/ρ²
        // with dn_i/dx_k = (δik − n_i n_k)/ρ; contracted against g.
        // T_ij = Σₖ gₖ ∂H_ij/∂x_k is symmetric in (i, j) since H is a Hessian.
        txx = SphereContraction(normal, rho, contractionGradient, 0, 0);
        txy = SphereContraction(normal, rho, contractionGradient, 0, 1);
        txz = SphereContraction(normal, rho, contractionGradient, 0, 2);
        tyy = SphereContraction(normal, rho, contractionGradient, 1, 1);
        tyz = SphereContraction(normal, rho, contractionGradient, 1, 2);
        tzz = SphereContraction(normal, rho, contractionGradient, 2, 2);
    }

    /// <summary>
    /// Cylinder-distance jets for D₃ contraction tests (math-only, GATE-B open).
    /// d(x)=ρ−R with ρ=‖(x−c)−((x−c)·a)a‖ on the finite sheet.
    /// </summary>
    internal static AlgorithmStatus CylinderDistanceJets(in KernelVector3 center, in KernelVector3 axis,
        double cylinderRadius, in KernelVector3 point, in KernelVector3 contractionGradient,
        out double value, out KernelVector3 gradient,
        out double hxx, out double hxy, out double hxz, out double hyy, out double hyz, out double hzz,
        out double txx, out double txy, out double txz, out double tyy, out double tyz, out double tzz)
    {
        value = 0;
        gradient = default;
        hxx = hxy = hxz = hyy = hyz = hzz = 0;
        txx = txy = txz = tyy = tyz = tzz = 0;
        var a2 = Dot(axis, axis);
        if (!(a2 > 0) || !(cylinderRadius > 0)) return AlgorithmStatus.InvalidInput;
        var unitA = Scale(axis, 1 / Math.Sqrt(a2));
        var d = Sub(point, center);
        var axial = Scale(unitA, Dot(d, unitA));
        var radial = Sub(d, axial);
        var rho = Math.Sqrt(Dot(radial, radial));
        if (!(rho > 1e-14)) return AlgorithmStatus.Singular;
        var normal = Scale(radial, 1 / rho);
        value = rho - cylinderRadius;
        gradient = normal;
        // H = (I − nnᵀ − aaᵀ)/ρ in the plane orthogonal to a (axis direction has zero curvature).
        hxx = (1 - normal.X * normal.X - unitA.X * unitA.X) / rho;
        hxy = (-normal.X * normal.Y - unitA.X * unitA.Y) / rho;
        hxz = (-normal.X * normal.Z - unitA.X * unitA.Z) / rho;
        hyy = (1 - normal.Y * normal.Y - unitA.Y * unitA.Y) / rho;
        hyz = (-normal.Y * normal.Z - unitA.Y * unitA.Z) / rho;
        hzz = (1 - normal.Z * normal.Z - unitA.Z * unitA.Z) / rho;
        // T(g) = [3α nnᵀ − α P − g_⊥ nᵀ − n g_⊥ᵀ] / ρ², where P = I − aaᵀ, g_⊥ = Pg, α = n·g (§11.1).
        var gDotA = Dot(contractionGradient, unitA);
        var gPerp = Sub(contractionGradient, Scale(unitA, gDotA));
        var alpha = Dot(normal, contractionGradient);
        var invRhoSq = 1.0 / (rho * rho);

        var pXX = 1.0 - unitA.X * unitA.X;
        var pXY = -unitA.X * unitA.Y;
        var pXZ = -unitA.X * unitA.Z;
        var pYY = 1.0 - unitA.Y * unitA.Y;
        var pYZ = -unitA.Y * unitA.Z;
        var pZZ = 1.0 - unitA.Z * unitA.Z;

        txx = (3 * alpha * normal.X * normal.X - alpha * pXX - 2 * gPerp.X * normal.X) * invRhoSq;
        txy = (3 * alpha * normal.X * normal.Y - alpha * pXY - gPerp.X * normal.Y - normal.X * gPerp.Y) * invRhoSq;
        txz = (3 * alpha * normal.X * normal.Z - alpha * pXZ - gPerp.X * normal.Z - normal.X * gPerp.Z) * invRhoSq;
        tyy = (3 * alpha * normal.Y * normal.Y - alpha * pYY - 2 * gPerp.Y * normal.Y) * invRhoSq;
        tyz = (3 * alpha * normal.Y * normal.Z - alpha * pYZ - gPerp.Y * normal.Z - normal.Y * gPerp.Z) * invRhoSq;
        tzz = (3 * alpha * normal.Z * normal.Z - alpha * pZZ - 2 * gPerp.Z * normal.Z) * invRhoSq;
        return AlgorithmStatus.Success;
    }

    private static double SphereContraction(in KernelVector3 n, double rho, in KernelVector3 g,
        int i, int j)
    {
        var sum = 0.0;
        for (var k = 0; k < 3; k++)
        {
            var dnI = DNormal(n, rho, i, k);
            var dnJ = DNormal(n, rho, j, k);
            var deltaIJ = i == j ? 1.0 : 0.0;
            // ∂H_ij/∂x_k = [−(dn_i·n_j + n_i·dn_j)ρ − (δij − n_i n_j)·n_k] / ρ²,
            // with ∂ρ/∂x_k = n_k on the last term.
            var derivative = (-(dnI * GetComponent(n, j) + GetComponent(n, i) * dnJ) * rho
                - (deltaIJ - GetComponent(n, i) * GetComponent(n, j)) * GetComponent(n, k)) / (rho * rho);
            sum += GetComponent(g, k) * derivative;
        }
        return sum;
    }

    private static double DNormal(in KernelVector3 n, double rho, int i, int k)
    {
        var deltaIK = i == k ? 1.0 : 0.0;
        return (deltaIK - GetComponent(n, i) * GetComponent(n, k)) / rho;
    }

    private static double GetComponent(in KernelVector3 v, int i) => i switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
