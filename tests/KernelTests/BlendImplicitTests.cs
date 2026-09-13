using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Blend envelope and local implicit tests (spec §10.4–§10.6, task T13).
/// The circular-spine tube IS a torus, so the analytic torus machinery and
/// closed-form geometry provide independent paths (§21.2): spine elimination,
/// φ_B gradient/Hessian against differencing, d_B against the exact toral
/// distance, the scaled-D guard against the curvature-center degeneracy, and
/// arc-range validation against the whole-tube impostor.
/// </summary>
public class BlendImplicitTests
{
    private const double ValueTol = 1e-12;
    private const double DifferenceTol = 1e-5;

    // Spine circle R=2 (RV-TUBE geometry), tube radius 0.25.
    private const double SpineRadius = 2.0;
    private const double TubeRadius = 0.25;

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static KernelVector3 SpinePoint(double s)
        => Vector(SpineRadius * Math.Cos(s), SpineRadius * Math.Sin(s), 0);

    private static KernelVector3 SurfacePoint(double s, double theta)
    {
        var radial = Vector(Math.Cos(s), Math.Sin(s), 0);
        var yDir = Vector(0, 0, 1);
        return Add(SpinePoint(s), Scale(Add(Scale(radial, Math.Cos(theta)), Scale(yDir, Math.Sin(theta))), TubeRadius));
    }

    private static AlgorithmStatus Solve(in KernelVector3 point, double seed, int order,
        out BlendLocalImplicitJet jet)
        => BlendImplicitEvaluation.SolveLocalImplicit(0, 0, 0, 0, 0, 1, 1, 0, 0,
            SpineRadius, TubeRadius, 1, order, in point, seed, out jet);

    [Fact]
    public void AssembleEnvelope_NeverPreZeroesE1s_OffRoot()
    {
        // An unconverged state with q·C' ≠ 0: (E₁)_s = −2q·C' must keep its
        // formula value (§10.4 — only the root satisfies E₂ = 0).
        var q = Vector(0.3, -0.2, 0.5);
        var spineD1 = Vector(-2 * Math.Sin(0.4), 2 * Math.Cos(0.4), 0);
        var spineD2 = Vector(-2 * Math.Cos(0.4), -2 * Math.Sin(0.4), 0);
        BlendImplicitEvaluation.AssembleEnvelope(in q, in spineD1, in spineD2,
            out var e1, out var e2, out var e1x, out var e1s, out var e2x, out var e2s);
        Assert.Equal(Dot(q, q), e1, 12);
        Assert.Equal(Dot(q, spineD1), e2, 12);
        Assert.Equal(0.6, e1x.X, 12);
        Assert.Equal(-2 * Dot(q, spineD1), e1s, 12);
        Assert.True(Math.Abs(e1s) > 1e-3, "off-root (E₁)_s must not be pre-zeroed");
        Assert.Equal(spineD1.X, e2x.X, 12);
        Assert.Equal(Dot(q, spineD2) - Dot(spineD1, spineD1), e2s, 12);
    }

    [Fact]
    public void LocalImplicit_OnSurface_MatchesReferenceVectors()
    {
        // RV-TUBE point: s=0.4, θ=0.43; the elimination must recover s and the
        // §10.5 gradient/D from the reference vectors.
        var point = SurfacePoint(0.4, 0.43);
        Assert.Equal(AlgorithmStatus.Success, Solve(in point, 0.2, 2, out var jet));
        Assert.InRange(Math.Abs(jet.SpineParameter - 0.4), 0, 1e-12);
        Assert.InRange(Math.Abs(jet.ImplicitValue), 0, 1e-12);
        Assert.Equal(0.4186064484550638, jet.ImplicitGradient.X, 12);
        Assert.Equal(0.17698396772686675, jet.ImplicitGradient.Y, 12);
        Assert.Equal(0.20843540121460538, jet.ImplicitGradient.Z, 12);
        Assert.Equal(4.454482874837443, jet.EliminationD, 12);
        Assert.True(jet.EliminationRatio >= BlendImplicitEvaluation.MinEliminationRatio);
        // Distance on the surface is zero.
        Assert.InRange(Math.Abs(jet.DistanceValue), 0, 1e-12);
    }

    [Fact]
    public void LocalImplicit_Distance_MatchesExactToralDistance()
    {
        // Off-surface along the surface normal: the profile angle is preserved,
        // so the exact torus distance is the offset amount (§8.1 torus sheet).
        var point = SurfacePoint(0.4, 0.43);
        var normal = Scale(Sub(point, SpinePoint(0.4)), 1 / TubeRadius);
        var off = Add(point, Scale(normal, 0.01));
        Assert.Equal(AlgorithmStatus.Success, Solve(in off, 0.4, 2, out var jet));
        Assert.InRange(Math.Abs(jet.DistanceValue - 0.01), 0, 1e-10);
        Assert.InRange(Math.Abs(jet.ImplicitValue - ((TubeRadius + 0.01) * (TubeRadius + 0.01) - TubeRadius * TubeRadius)), 0, 1e-10);
        // Inward offset.
        var inner = Sub(point, Scale(normal, 0.005));
        Assert.Equal(AlgorithmStatus.Success, Solve(in inner, 0.4, 2, out var innerJet));
        Assert.InRange(Math.Abs(innerJet.DistanceValue + 0.005), 0, 1e-10);
    }

    [Fact]
    public void LocalImplicit_Hessians_MatchDifferencedGradients()
    {
        // ∇φ_B = 2q(s(x)) is an analytically defined field once s is solved;
        // its central difference (re-solving s at every offset point) must
        // match the §10.5 projector Hessian (§21.2 two-path rule).
        var point = SurfacePoint(0.4, 0.43);
        Assert.Equal(AlgorithmStatus.Success, Solve(in point, 0.4, 2, out var jet));
        const double h = 1e-5;
        Span<KernelVector3> gradient = new KernelVector3[3];
        for (var axis = 0; axis < 3; axis++)
        {
            var plus = point;
            var minus = point;
            SetComponent(ref plus, axis, GetComponent(in point, axis) + h);
            SetComponent(ref minus, axis, GetComponent(in point, axis) - h);
            Assert.Equal(AlgorithmStatus.Success, Solve(in plus, jet.SpineParameter, 1, out var jetPlus));
            Assert.Equal(AlgorithmStatus.Success, Solve(in minus, jet.SpineParameter, 1, out var jetMinus));
            gradient[axis] = Scale(Sub(jetPlus.ImplicitGradient, jetMinus.ImplicitGradient), 1 / (2 * h));
        }
        Assert.InRange(Math.Abs(gradient[0].X - jet.ImplicitHxx), 0, DifferenceTol);
        Assert.InRange(Math.Abs(gradient[0].Y - jet.ImplicitHxy), 0, DifferenceTol);
        Assert.InRange(Math.Abs(gradient[0].Z - jet.ImplicitHxz), 0, DifferenceTol);
        Assert.InRange(Math.Abs(gradient[1].Y - jet.ImplicitHyy), 0, DifferenceTol);
        Assert.InRange(Math.Abs(gradient[1].Z - jet.ImplicitHyz), 0, DifferenceTol);
        Assert.InRange(Math.Abs(gradient[2].Z - jet.ImplicitHzz), 0, DifferenceTol);
        // Symmetry of the differenced columns confirms the projector formula.
        Assert.InRange(Math.Abs(gradient[1].X - jet.ImplicitHxy), 0, DifferenceTol);
        Assert.InRange(Math.Abs(gradient[2].X - jet.ImplicitHxz), 0, DifferenceTol);
    }

    [Fact]
    public void LocalImplicit_CurvatureCenterDegeneracy_RefusesWithoutEpsilon()
    {
        // The spine circle's curvature center is the torus center: D → 0
        // there. The scaled-D guard must refuse (§10.6), never pad and divide.
        var degenerate = Vector(1e-10, 0, 0);
        Assert.Equal(AlgorithmStatus.Singular, Solve(in degenerate, 0.0, 2, out _));
        // A healthy point still passes with a respectable ratio.
        var healthy = SurfacePoint(0.4, 0.43);
        Assert.Equal(AlgorithmStatus.Success, Solve(in healthy, 0.4, 2, out var jet));
        Assert.True(jet.EliminationRatio > 0.1);
    }

    [Fact]
    public void ArcValidation_RejectsWholeTubeImpostorRoots()
    {
        // §10.6: the envelope equations cover the entire tube; the fillet's
        // quarter arc accepts only its own contact directions.
        var x = Vector(0, 0, -1); // fillet frame X
        var y = Vector(-1, 0, 0); // fillet frame Y
        var arc = Math.PI / 2;
        // Quarter-arc midpoint direction (θ = π/4): v = 0.5.
        var mid = Add(Scale(x, Math.Cos(Math.PI / 4)), Scale(y, Math.Sin(Math.PI / 4)));
        Assert.True(BlendImplicitEvaluation.TryValidateArc(in mid, in x, in y, arc, 0, 1, out var v));
        Assert.Equal(0.5, v, 12);
        // Antipodal direction (θ = π): a tube point beyond the blend patch.
        var antipode = Scale(x, -1);
        Assert.False(BlendImplicitEvaluation.TryValidateArc(in antipode, in x, in y, arc, 0, 1, out _));
        // Periodic lift: a direction just past ±π is the same physical sheet
        // as its lifted representative (§13.5).
        var lifted = Add(Scale(x, Math.Cos(Math.PI + 0.01)), Scale(y, Math.Sin(Math.PI + 0.01)));
        Assert.False(BlendImplicitEvaluation.TryValidateArc(in lifted, in x, in y, arc, 0, 1, out _));
        // Full-turn tube (a = 1, v = θ): a lifted angle still resolves.
        var tubeX = Vector(1, 0, 0);
        var tubeY = Vector(0, 0, 1);
        var turned = Add(Scale(tubeX, Math.Cos(0.43 + Math.Tau)), Scale(tubeY, Math.Sin(0.43 + Math.Tau)));
        Assert.True(BlendImplicitEvaluation.TryValidateArc(in turned, in tubeX, in tubeY, 1.0, 0, 1, out var vLifted));
        Assert.InRange(Math.Abs(vLifted - 0.43), 0, 1e-12);
    }

    private static double GetComponent(in KernelVector3 v, int axis) => axis switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };

    private static void SetComponent(ref KernelVector3 v, int axis, double value)
    {
        if (axis == 0) v.X = value;
        else if (axis == 1) v.Y = value;
        else v.Z = value;
    }
}
