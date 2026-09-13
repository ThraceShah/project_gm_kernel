using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Local implicit state of one blend branch after the §10.5 elimination. The
/// implicit-quality fields (zero-set statement) and the distance-quality
/// fields are separate and named; callers pick per capability and never read
/// a distance that the elimination could not support (§8.2, §10.5).
/// </summary>
internal readonly struct BlendLocalImplicitJet
{
    internal BlendLocalImplicitJet(double spineParameter, in KernelVector3 spinePoint, in KernelVector3 offset,
        double eliminationD, double eliminationRatio,
        double implicitValue, in KernelVector3 implicitGradient,
        double ihxx, double ihxy, double ihxz, double ihyy, double ihyz, double ihzz,
        double distanceValue, in KernelVector3 distanceGradient,
        double dhxx, double dhxy, double dhxz, double dhyy, double dhyz, double dhzz)
    {
        SpineParameter = spineParameter;
        SpinePoint = spinePoint;
        Offset = offset;
        EliminationD = eliminationD;
        EliminationRatio = eliminationRatio;
        ImplicitValue = implicitValue;
        ImplicitGradient = implicitGradient;
        ImplicitHxx = ihxx; ImplicitHxy = ihxy; ImplicitHxz = ihxz;
        ImplicitHyy = ihyy; ImplicitHyz = ihyz; ImplicitHzz = ihzz;
        DistanceValue = distanceValue;
        DistanceGradient = distanceGradient;
        DistanceHxx = dhxx; DistanceHxy = dhxy; DistanceHxz = dhxz;
        DistanceHyy = dhyy; DistanceHyz = dhyz; DistanceHzz = dhzz;
    }


    internal readonly double SpineParameter;       // solved s
    internal readonly KernelVector3 SpinePoint;    // C(s)
    internal readonly KernelVector3 Offset;        // q = x − C(s)
    internal readonly double EliminationD;         // D = ‖C'‖² − q·C''
    internal readonly double EliminationRatio;     // |D| / (‖C'‖² + ‖q‖‖C''‖)
    // Implicit quality (zero set only): φ_B = ‖q‖² − r².
    internal readonly double ImplicitValue;
    internal readonly KernelVector3 ImplicitGradient;
    internal readonly double ImplicitHxx, ImplicitHxy, ImplicitHxz, ImplicitHyy, ImplicitHyz, ImplicitHzz;
    // Distance quality: d_B = ρ − r on the same foot branch.
    internal readonly double DistanceValue;
    internal readonly KernelVector3 DistanceGradient;
    internal readonly double DistanceHxx, DistanceHxy, DistanceHxz, DistanceHyy, DistanceHyz, DistanceHzz;
}

/// <summary>
/// Envelope and local-implicit machinery for regular constant-radius blends
/// (spec §10.4–§10.6, task T13). The envelope assembly never zeroes (E₁)_s —
/// the target root satisfies E₂ = 0, but unconverged iterates do not, and a
/// permanently zeroed (E₁)_s would corrupt the step (§10.4). The local
/// elimination refuses through the scaled D ratio instead of dividing by an
/// epsilon-padded denominator (§10.6), and the solved branch is validated
/// against the blend's arc range — the envelope equations alone describe the
/// whole tube candidate (§10.6).
/// </summary>
internal static class BlendImplicitEvaluation
{
    /// <summary>Scaled D ratio below which the elimination is refused (§10.6).</summary>
    internal const double MinEliminationRatio = 1e-8;
    internal const int MaxSpineIterations = 12;

    /// <summary>
    /// §10.4 envelope residuals and first derivatives at an arbitrary state.
    /// (E₁)_s = −2q·C' is assembled unconditionally: only the converged root
    /// satisfies E₂ = 0, so no caller may pre-zero it.
    /// </summary>
    internal static void AssembleEnvelope(in KernelVector3 q, in KernelVector3 spineD1,
        in KernelVector3 spineD2, out double e1, out double e2, out KernelVector3 e1x,
        out double e1s, out KernelVector3 e2x, out double e2s)
    {
        e1 = Dot(q, q); // − r² added by the caller (radius is its business)
        e2 = Dot(q, spineD1);
        e1x = Scale(q, 2);
        e1s = -2 * Dot(q, spineD1);
        e2x = spineD1;
        e2s = Dot(q, spineD2) - Dot(spineD1, spineD1);
    }

    /// <summary>
    /// §10.5 local elimination near a cached spine branch: solve
    /// g(s) = q·C'(s) = 0 by 1D Newton from <paramref name="spineSeed"/>,
    /// then report φ_B and, at order 2, the Hessians — including the local
    /// distance d_B = ρ − r with its own Hessian. The spine kind switch is
    /// finite and static (analytic spines this slice; procedural spines enter
    /// through the prepared graph in later tasks).
    /// </summary>
    internal static AlgorithmStatus SolveLocalImplicit(in CircleData spine, double radius, KernelSense sense,
        DerivativeOrder order, in KernelVector3 point, double spineSeed, out BlendLocalImplicitJet jet)
        => SolveLocalImplicitCore(spine.CenterX, spine.CenterY, spine.CenterZ,
            spine.AxisX, spine.AxisY, spine.AxisZ, spine.RefDirX, spine.RefDirY, spine.RefDirZ,
            spine.Radius, radius, sense, order, in point, spineSeed, out jet);

    internal static AlgorithmStatus SolveLocalImplicit(double centerX, double centerY, double centerZ,
        double axisX, double axisY, double axisZ, double refX, double refY, double refZ,
        double spineRadius, double radius, KernelSense sense, DerivativeOrder order,
        in KernelVector3 point, double spineSeed, out BlendLocalImplicitJet jet)
        => SolveLocalImplicitCore(centerX, centerY, centerZ, axisX, axisY, axisZ,
            refX, refY, refZ, spineRadius, radius, sense, order, in point, spineSeed, out jet);

    private static AlgorithmStatus SolveLocalImplicitCore(double centerX, double centerY, double centerZ,
        double axisX, double axisY, double axisZ, double refX, double refY, double refZ,
        double spineRadius, double radius, KernelSense sense, DerivativeOrder order,
        in KernelVector3 point, double spineSeed, out BlendLocalImplicitJet jet)
    {
        jet = default;
        if (order < 0 || order > 2) return AlgorithmStatus.InvalidInput;
        if (sense != 1 && sense != -1) return AlgorithmStatus.InvalidInput;
        if (!(radius > 0) || !(spineRadius > 0) || !IsFinite(point)) return AlgorithmStatus.InvalidInput;

        var axis = Vector(axisX, axisY, axisZ);
        var refDir = Vector(refX, refY, refZ);
        var yAxis = Cross(axis, refDir);
        var center = Vector(centerX, centerY, centerZ);

        // 1D Newton on g(s) = q·C'(s), g' = q·C'' − ‖C'‖².
        var s = spineSeed;
        var converged = false;
        var spineD1 = default(KernelVector3);
        var spineD2 = default(KernelVector3);
        var q = default(KernelVector3);
        for (BufferOffset iteration = 0; iteration < MaxSpineIterations; iteration++)
        {
            var (sinS, cosS) = Math.SinCos(s);
            var radial = Add(Scale(refDir, cosS), Scale(yAxis, sinS));
            var spinePoint = Add(center, Scale(radial, spineRadius));
            spineD1 = Scale(Cross(axis, radial), spineRadius);
            spineD2 = Scale(radial, -spineRadius);
            q = Sub(point, spinePoint);
            var g = Dot(q, spineD1);
            if (!double.IsFinite(g)) return AlgorithmStatus.NumericalFailure;
            if (Math.Abs(g) <= ICurveEvaluation.ResidualTolerance * Math.Max(1.0, SpineNorm(spineRadius)))
            {
                converged = true;
                break;
            }
            var gPrime = Dot(q, spineD2) - Dot(spineD1, spineD1);
            if (!(Math.Abs(gPrime) > 1e-300)) return AlgorithmStatus.Singular;
            s -= g / gPrime;
            if (!double.IsFinite(s)) return AlgorithmStatus.NumericalFailure;
        }
        if (!converged) return AlgorithmStatus.NotConverged;

        // Scaled D guard (§10.6): no epsilon-padded division.
        var d = Dot(spineD1, spineD1) - Dot(q, spineD2);
        var scale = Dot(spineD1, spineD1) + Math.Sqrt(Dot(q, q)) * Math.Sqrt(Dot(spineD2, spineD2));
        var ratio = scale > 0 ? Math.Abs(d) / scale : 0;
        if (!(ratio >= MinEliminationRatio)) return AlgorithmStatus.Singular;

        var rho = Math.Sqrt(Dot(q, q));
        if (!(rho > 0)) return AlgorithmStatus.Singular;
        var normal = Scale(q, 1 / rho);
        var projectorXX = 1 - spineD1.X * spineD1.X / d;
        var projectorXY = -spineD1.X * spineD1.Y / d;
        var projectorXZ = -spineD1.X * spineD1.Z / d;
        var projectorYY = 1 - spineD1.Y * spineD1.Y / d;
        var projectorYZ = -spineD1.Y * spineD1.Z / d;
        var projectorZZ = 1 - spineD1.Z * spineD1.Z / d;

        var implicitGradient = Scale(q, 2);
        var distanceGradient = Scale(normal, sense);
        // ∇²φ_B = 2(I − C'C'ᵀ/D) (§10.5), sense-free (implicit zero set);
        // ∇²d_B = (I − C'C'ᵀ/D − nnᵀ)/ρ (§10.5), sense-applied.
        jet = new BlendLocalImplicitJet(s, Add(center, Scale(radialAt(s, refDir, yAxis), spineRadius)), q,
            d, ratio,
            Dot(q, q) - radius * radius, implicitGradient,
            2 * projectorXX, 2 * projectorXY, 2 * projectorXZ,
            2 * projectorYY, 2 * projectorYZ, 2 * projectorZZ,
            sense * (rho - radius), distanceGradient,
            sense * (projectorXX - normal.X * normal.X) / rho,
            sense * (projectorXY - normal.X * normal.Y) / rho,
            sense * (projectorXZ - normal.X * normal.Z) / rho,
            sense * (projectorYY - normal.Y * normal.Y) / rho,
            sense * (projectorYZ - normal.Y * normal.Z) / rho,
            sense * (projectorZZ - normal.Z * normal.Z) / rho);
        if (order == 0)
        {
            jet = StripDerivatives(in jet);
        }
        return double.IsFinite(jet.ImplicitValue) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// §10.6 arc-range validation: the envelope equations describe the whole
    /// tube candidate; only the solved state whose contact direction lands in
    /// the blend's arc interval is the stored branch. Periodic lifts of θ are
    /// considered before rejection (§13.5).
    /// </summary>
    internal static bool TryValidateArc(in KernelVector3 offset, in KernelVector3 x, in KernelVector3 y,
        double arc, double vMin, double vMax, out double v)
    {
        v = 0;
        if (!(arc != 0) || !double.IsFinite(arc)) return false;
        var theta = Math.Atan2(Dot(offset, y), Dot(offset, x));
        for (var lift = -1; lift <= 1; lift++)
        {
            var candidate = theta + lift * Math.Tau;
            var candidateV = candidate / arc;
            if (candidateV >= vMin && candidateV <= vMax)
            {
                v = candidateV;
                return true;
            }
        }
        return false;
    }

    private static KernelVector3 radialAt(double s, in KernelVector3 refDir, in KernelVector3 yAxis)
    {
        var (sinS, cosS) = Math.SinCos(s);
        return Add(Scale(refDir, cosS), Scale(yAxis, sinS));
    }

    private static double SpineNorm(double spineRadius) => spineRadius;

    private static BlendLocalImplicitJet StripDerivatives(in BlendLocalImplicitJet jet)
        => new(jet.SpineParameter, jet.SpinePoint, jet.Offset, jet.EliminationD, jet.EliminationRatio,
            jet.ImplicitValue, jet.ImplicitGradient, 0, 0, 0, 0, 0, 0,
            jet.DistanceValue, jet.DistanceGradient, 0, 0, 0, 0, 0, 0);

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
