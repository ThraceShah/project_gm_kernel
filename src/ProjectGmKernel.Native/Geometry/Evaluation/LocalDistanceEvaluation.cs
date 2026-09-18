using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Result of the normal-foot elimination (spec §9.1): the local oriented
/// distance d = h with its gradient and Hessian, valid only inside the
/// evidenced tubular neighborhood of the converged foot-point branch.
/// </summary>
internal readonly struct FootPointJet
{
    internal readonly double Distance;
    internal readonly KernelVector3 Gradient;
    internal readonly double Hxx, Hxy, Hxz, Hyy, Hyz, Hzz;
    internal readonly double FootU;
    internal readonly double FootV;
    internal readonly KernelVector3 FootPoint;
    internal readonly double ConditionProxy;

    internal FootPointJet(double distance, in KernelVector3 gradient,
        double hxx, double hxy, double hxz, double hyy, double hyz, double hzz,
        double footU, double footV, in KernelVector3 footPoint, double conditionProxy)
    {
        Distance = distance;
        Gradient = gradient;
        Hxx = hxx; Hxy = hxy; Hxz = hxz;
        Hyy = hyy; Hyz = hyz; Hzz = hzz;
        FootU = footU;
        FootV = footV;
        FootPoint = footPoint;
        ConditionProxy = conditionProxy;
    }
}

/// <summary>
/// Normal-foot elimination for parametric surfaces (spec §9.1, task T11).
/// Solves H(u,v,h;x) = S(u,v) + h·n(u,v) − x = 0 with K = [Su+h·nu, Sv+h·nv, n],
/// then reports d = h, ∇d = n and the sensitivity Hessian
/// [n_u n_v]·[K⁻¹]₁:₂,: obtained from one transposed factorization — never an
/// explicit inverse. This is a local solve on one foot branch, not a global
/// closest-point algorithm (§9.1); validity is bounded by the K condition
/// proxy and the caller's patch evidence (§9.2).
/// </summary>
internal static class LocalDistanceEvaluation
{
    /// <summary>Condition proxy (smallest/largest LU pivot) below which the elimination is refused.</summary>
    internal const double MinConditionProxy = 1e-8;
    internal const int MaxNewtonIterations = 12;

    /// <summary>
    /// Solve the foot point of <paramref name="point"/> on the surface's
    /// natural-normal branch and report the local distance jet. The natural
    /// normal is Su×Sv normalized (PK convention); the source surface sense
    /// multiplies distance, gradient and Hessian together (§8.3).
    /// </summary>
    internal static AlgorithmStatus SolveFootPoint(in AnalyticSurface surface, in KernelVector3 point,
        KernelSense sense, DerivativeOrder order, out FootPointJet jet)
    {
        jet = default;
        if (order < 0 || order > 2) return AlgorithmStatus.InvalidInput;
        if (sense != 1 && sense != -1) return AlgorithmStatus.InvalidInput;
        if (!IsFinite(point)) return AlgorithmStatus.InvalidInput;

        // Initialize from the parameter witness of the near-surface point (§9.2).
        var witnessStatus = AnalyticParametricEvaluation.TryRecoverWitness(in surface, in point, out var u, out var v);
        if (witnessStatus != AlgorithmStatus.Success) return witnessStatus;

        Span<double> y = stackalloc double[3] { u, v, 0 };
        Span<double> hessianResidual = stackalloc double[3];
        Span<double> kMatrix = stackalloc double[9];
        Span<double> kCopy = stackalloc double[9];
        Span<double> kTranspose = stackalloc double[9];
        Span<double> step = stackalloc double[3];
        Span<double> model = stackalloc double[3];
        Span<int> pivots = stackalloc int[3];
        Span<KernelVector3> baseJet = stackalloc KernelVector3[9];
        if (!SurfaceDerivativeLayout.TryCreate(2, 2, out var layout))
            return AlgorithmStatus.InvalidInput;

        var conditionProxy = 0.0;
        var converged = false;
        for (BufferOffset iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            if (SurfaceEvaluation.Evaluate(in surface, y[0], y[1], in layout, baseJet) != AlgorithmStatus.Success)
                return AlgorithmStatus.NotConverged; // trial left the valid parameter domain
            var foot = baseJet[0];
            var normal = UnitNormal(baseJet, layout);
            if (!IsFinite(normal)) return AlgorithmStatus.Singular;

            hessianResidual[0] = foot.X + y[2] * normal.X - point.X;
            hessianResidual[1] = foot.Y + y[2] * normal.Y - point.Y;
            hessianResidual[2] = foot.Z + y[2] * normal.Z - point.Z;

            var nu = default(KernelVector3);
            var nv = default(KernelVector3);
            if (order >= 2 || iteration == 0)
            {
                // Unit-normal derivatives from the second-order surface jets.
                NormalDerivatives(baseJet, layout, out nu, out nv);
            }

            kMatrix[0] = baseJet[layout.GetIndex(1, 0)].X + y[2] * nu.X;
            kMatrix[1] = baseJet[layout.GetIndex(0, 1)].X + y[2] * nv.X;
            kMatrix[2] = normal.X;
            kMatrix[3] = baseJet[layout.GetIndex(1, 0)].Y + y[2] * nu.Y;
            kMatrix[4] = baseJet[layout.GetIndex(0, 1)].Y + y[2] * nv.Y;
            kMatrix[5] = normal.Y;
            kMatrix[6] = baseJet[layout.GetIndex(1, 0)].Z + y[2] * nu.Z;
            kMatrix[7] = baseJet[layout.GetIndex(0, 1)].Z + y[2] * nv.Z;
            kMatrix[8] = normal.Z;

            var residualNorm = SmallLinearSolve.Norm(hessianResidual);
            var lengthScale = Math.Max(1.0, SmallLinearSolve.Norm(stackalloc[] { point.X, point.Y, point.Z }));
            if (residualNorm <= ICurveEvaluation.ResidualTolerance * lengthScale)
            {
                converged = true;
                break;
            }

            kMatrix.CopyTo(kCopy);
            var stepStatus = NewtonStep.ComputeStep(kMatrix, kCopy, hessianResidual, 3, pivots, model, step, out var predicted);
            if (stepStatus != AlgorithmStatus.Success || !(predicted > 0))
                return stepStatus == AlgorithmStatus.Success ? AlgorithmStatus.Singular : stepStatus;
            // Bounded parameter step: an unbounded UV jump leaves the foot
            // branch (§9.2 step limiting).
            if (step[0] * step[0] + step[1] * step[1] > 25.0) return AlgorithmStatus.NotConverged;
            y[0] += step[0];
            y[1] += step[1];
            y[2] += step[2];
            if (!double.IsFinite(y[0]) || !double.IsFinite(y[1]) || !double.IsFinite(y[2]))
                return AlgorithmStatus.NumericalFailure;
        }
        if (!converged) return AlgorithmStatus.NotConverged;

        // Root jets and the condition proxy from the true root K.
        if (SurfaceEvaluation.Evaluate(in surface, y[0], y[1], in layout, baseJet) != AlgorithmStatus.Success)
            return AlgorithmStatus.NotConverged;
        var rootNormal = UnitNormal(baseJet, layout);
        if (!IsFinite(rootNormal)) return AlgorithmStatus.Singular;
        NormalDerivatives(baseJet, layout, out var rootNu, out var rootNv);

        kMatrix[0] = baseJet[layout.GetIndex(1, 0)].X + y[2] * rootNu.X;
        kMatrix[1] = baseJet[layout.GetIndex(0, 1)].X + y[2] * rootNv.X;
        kMatrix[2] = rootNormal.X;
        kMatrix[3] = baseJet[layout.GetIndex(1, 0)].Y + y[2] * rootNu.Y;
        kMatrix[4] = baseJet[layout.GetIndex(0, 1)].Y + y[2] * rootNv.Y;
        kMatrix[5] = rootNormal.Y;
        kMatrix[6] = baseJet[layout.GetIndex(1, 0)].Z + y[2] * rootNu.Z;
        kMatrix[7] = baseJet[layout.GetIndex(0, 1)].Z + y[2] * rootNv.Z;
        kMatrix[8] = rootNormal.Z;
        kMatrix.CopyTo(kCopy);
        if (SmallLinearSolve.LuFactorize(kCopy, 3, pivots) != AlgorithmStatus.Success)
            return AlgorithmStatus.Singular;
        var pivotMin = double.MaxValue;
        var pivotMax = 0.0;
        for (BufferOffset i = 0; i < 3; i++)
        {
            var magnitude = Math.Abs(kCopy[i * 3 + i]);
            if (magnitude < pivotMin) pivotMin = magnitude;
            if (magnitude > pivotMax) pivotMax = magnitude;
        }
        conditionProxy = pivotMax > 0 ? pivotMin / pivotMax : 0;
        if (!(conditionProxy >= MinConditionProxy))
            return AlgorithmStatus.Singular; // ill-conditioned elimination: refuse, no unique local SDF claimed (§9.2)

        var buildStatus = BuildJet(y, rootNormal, rootNu, rootNv, kMatrix, kTranspose, pivots,
            conditionProxy, sense, order, in point, out jet);
        return buildStatus;
    }

    private static AlgorithmStatus BuildJet(Span<double> y, in KernelVector3 normal,
        in KernelVector3 nu, in KernelVector3 nv, Span<double> kMatrix, Span<double> kTranspose,
        Span<int> pivots, double conditionProxy, KernelSense sense, DerivativeOrder order,
        in KernelVector3 point, out FootPointJet jet)
    {
        jet = default;
        // Foot point on the surface: x_foot = x − h·n, exact at convergence.
        var footPoint = Sub(point, Scale(normal, y[2]));
        var gradient = Scale(normal, sense);
        var hxx = 0.0; var hxy = 0.0; var hxz = 0.0;
        var hyy = 0.0; var hyz = 0.0; var hzz = 0.0;
        if (order >= 2)
        {
            // ∇²d = [n_u n_v]·[K⁻¹]₁:₂,: with the two rows of K⁻¹ obtained by
            // solving Kᵀ z_i = e_i (no explicit inverse, §9.1).
            for (BufferOffset i = 0; i < 3; i++)
                for (BufferOffset j = 0; j < 3; j++)
                    kTranspose[i * 3 + j] = kMatrix[j * 3 + i];
            if (SmallLinearSolve.LuFactorize(kTranspose, 3, pivots) != AlgorithmStatus.Success)
                return AlgorithmStatus.Singular;

            Span<double> z1 = stackalloc double[3];
            Span<double> z2 = stackalloc double[3];
            z1[0] = 1;
            z2[1] = 1;
            if (SmallLinearSolve.LuSolveInPlace(kTranspose, 3, pivots, z1) != AlgorithmStatus.Success
                || SmallLinearSolve.LuSolveInPlace(kTranspose, 3, pivots, z2) != AlgorithmStatus.Success)
                return AlgorithmStatus.NumericalFailure;

            // Row i of K⁻¹ is z_i; Hessian = n_u z₁ᵀ + n_v z₂ᵀ, sense-applied.
            hxx = sense * (nu.X * z1[0] + nv.X * z2[0]);
            hxy = sense * (nu.X * z1[1] + nv.X * z2[1]);
            hxz = sense * (nu.X * z1[2] + nv.X * z2[2]);
            hyy = sense * (nu.Y * z1[1] + nv.Y * z2[1]);
            hyz = sense * (nu.Y * z1[2] + nv.Y * z2[2]);
            hzz = sense * (nu.Z * z1[2] + nv.Z * z2[2]);
        }
        jet = new FootPointJet(sense * y[2], gradient, hxx, hxy, hxz, hyy, hyz, hzz,
            y[0], y[1], footPoint, conditionProxy);
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 UnitNormal(ReadOnlySpan<KernelVector3> jet, in SurfaceDerivativeLayout layout)
        => Unit(Cross(jet[layout.GetIndex(1, 0)], jet[layout.GetIndex(0, 1)]));

    /// <summary>Derivatives of the unit normal m = w/‖w‖, w = Su×Sv: m_u = (w_u − m(m·w_u))/‖w‖.</summary>
    private static void NormalDerivatives(ReadOnlySpan<KernelVector3> jet, in SurfaceDerivativeLayout layout,
        out KernelVector3 nu, out KernelVector3 nv)
    {
        var su = jet[layout.GetIndex(1, 0)];
        var sv = jet[layout.GetIndex(0, 1)];
        var suu = jet[layout.GetIndex(2, 0)];
        var suv = jet[layout.GetIndex(1, 1)];
        var svv = jet[layout.GetIndex(0, 2)];
        var w = Cross(su, sv);
        var norm = Math.Sqrt(Dot(w, w));
        if (!(norm > 0))
        {
            nu = nv = default;
            return;
        }
        var m = Scale(w, 1 / norm);
        // ∂(Su×Sv)/∂u = Suu×Sv + Su×Suv (product rule, both terms plus).
        var wu = Add(Cross(suu, sv), Cross(su, suv));
        var wv = Add(Cross(suv, sv), Cross(su, svv));
        nu = Scale(Sub(wu, Scale(m, Dot(m, wu))), 1 / norm);
        nv = Scale(Sub(wv, Scale(m, Dot(m, wv))), 1 / norm);
    }

    /// <summary>
    /// Offset zero-set via a true oriented distance (spec §9.3): when the base
    /// surface provides an exact local distance (plane/sphere/cylinder/ring-torus),
    /// the offset of signed distance a has the local distance d_base − a with the
    /// same foot branch. First-order or implicit-only bases are refused —
    /// φ_base − a is a different surface, and no averaging of any kind is
    /// performed here.
    /// </summary>
    internal static AlgorithmStatus TryOffsetDistance(in AnalyticSurface baseSurface,
        KernelSense baseSense, double offsetDistance, in KernelVector3 point,
        DerivativeOrder order, out FootPointJet jet)
    {
        jet = default;
        if (offsetDistance == 0) return AlgorithmStatus.InvalidInput; // XT: offset distance must not vanish
        var status = SurfaceDistanceEvaluation.Evaluate(in baseSurface, in point, baseSense, order, out var distance);
        if (status != AlgorithmStatus.Success) return status;
        if (distance.Grade != DistanceGrade.Exact)
            return AlgorithmStatus.Unsupported; // first-order estimates never eliminate an offset (§9.3)
        jet = new FootPointJet(distance.Distance - offsetDistance, distance.Gradient,
            distance.Hxx, distance.Hxy, distance.Hxz, distance.Hyy, distance.Hyz, distance.Hzz,
            0, 0, distance.FootPoint, 1.0);
        return AlgorithmStatus.Success;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
