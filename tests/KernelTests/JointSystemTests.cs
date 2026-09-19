using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Joint constraint tests (spec §12, task T14): the six-unknown lowering
/// against the RV-LIFTED-J reference (residual and full Jacobian off-root),
/// and the §12.3 Schur elimination against both the RV-SCHUR reference step
/// and the full-system LU solve with a non-converged inner block.
/// </summary>
public class JointSystemTests
{
    // RV-LIFTED-J fixture: spine = cylinder x²+y²=4 ∩ plane z=0, blend radius
    // 0.25, outer support plane z=0.10421770060730269, parameter plane through
    // the RV-TUBE blend point with the reference normal.
    private static readonly AnalyticSurface SupportA =
        new(SurfaceClass.Cylinder, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0), 2.0);
    private static readonly AnalyticSurface SupportD =
        new(SurfaceClass.Plane, Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly AnalyticSurface OuterPlane =
        new(SurfaceClass.Plane, Vector(0, 0, 0.10421770060730269), Vector(0, 0, 1), Vector(1, 0, 0));
    private static readonly KernelVector3 PlaneAnchor =
        Vector(2.051425212233302, 0.8673286684807344, 0.10421770060730269);
    private static readonly KernelVector3 PlaneNormal =
        Vector(-0.4364357804719847, 0.8728715609439694, 0.21821789023599236);
    private const double RadiusSq = 0.0625;

    private static readonly KernelVector3 StateX =
        Vector(2.061425212233302, 0.8593286684807344, 0.10621770060730269);
    private static readonly KernelVector3 StateC =
        Vector(1.84512198800577, 0.783836684617301, -0.001);

    private static readonly double[] ExpectedResidual =
    [
        0.0188750987742079,
        -0.001,
        0.0014817597623799778,
        0.060508965611886976,
        0.0020000000000000018,
        -0.010910894511799532,
    ];

    private static readonly double[] ExpectedJacobian =
    [
        0.0, 0.0, 0.0, 3.69024397601154, 1.567673369234602, 0.0,
        0.0, 0.0, 0.0, 0.0, 0.0, 1.0,
        0.4326064484550636, 0.15098396772686673, 0.2144354012146054,
            -0.4326064484550636, -0.15098396772686673, -0.2144354012146054,
        1.567673369234602, -3.69024397601154, 0.0, -1.7186573369614688, 4.122850424466604, 0.0,
        0.0, 0.0, 1.0, 0.0, 0.0, 0.0,
        -0.4364357804719847, 0.8728715609439694, 0.21821789023599236, 0.0, 0.0, 0.0,
    ];

    [Fact]
    public void SixUnknownResidual_MatchesReferenceVectors()
    {
        Span<double> residual = new double[6];
        JointBlendResidual.AssembleResidual(in SupportA, in SupportD, in OuterPlane,
            in PlaneAnchor, in PlaneNormal, RadiusSq, in StateX, in StateC, residual);
        for (var i = 0; i < 6; i++)
            Assert.InRange(Math.Abs(residual[i] - ExpectedResidual[i]), 0, 1e-12);
    }

    [Fact]
    public void SixUnknownJacobian_MatchesReferenceVectors()
    {
        Span<double> jacobian = new double[36];
        Assert.Equal(AlgorithmStatus.Success, JointBlendResidual.AssembleJacobian(
            in SupportA, in SupportD, in OuterPlane, in PlaneNormal, in StateX, in StateC, jacobian));
        for (var i = 0; i < 36; i++)
            Assert.InRange(Math.Abs(jacobian[i] - ExpectedJacobian[i]), 0, 1e-12);
    }

    [Fact]
    public void JointNewton_RecoversTheRootFromTheReferenceState()
    {
        // Starting from the off-root RV state, joint Newton (LU on the 6×6)
        // must drive every defining residual to zero without changing the
        // semantics of the compilation.
        Span<double> state = new double[6]
        {
            StateX.X, StateX.Y, StateX.Z, StateC.X, StateC.Y, StateC.Z,
        };
        Span<double> residual = new double[6];
        Span<double> jacobian = new double[36];
        Span<double> jacobianCopy = new double[36];
        Span<double> step = new double[6];
        Span<double> model = new double[6];
        Span<int> pivots = new int[6];
        for (var iteration = 0; iteration < 16; iteration++)
        {
            var x = Vector(state[0], state[1], state[2]);
            var c = Vector(state[3], state[4], state[5]);
            JointBlendResidual.AssembleResidual(in SupportA, in SupportD, in OuterPlane,
                in PlaneAnchor, in PlaneNormal, RadiusSq, in x, in c, residual);
            var norm = SmallLinearSolve.Norm(residual);
            if (norm < 1e-12) break;
            Assert.Equal(AlgorithmStatus.Success, JointBlendResidual.AssembleJacobian(
                in SupportA, in SupportD, in OuterPlane, in PlaneNormal, in x, in c, jacobian));
            Assert.Equal(AlgorithmStatus.Success, NewtonStep.ComputeStep(jacobian, jacobianCopy,
                residual, 6, pivots, model, step, out _));
            for (var i = 0; i < 6; i++) state[i] += step[i];
        }
        Span<double> finalResidual = new double[6];
        var fx = Vector(state[0], state[1], state[2]);
        var fc = Vector(state[3], state[4], state[5]);
        JointBlendResidual.AssembleResidual(in SupportA, in SupportD, in OuterPlane,
            in PlaneAnchor, in PlaneNormal, RadiusSq, in fx, in fc, finalResidual);
        Assert.InRange(SmallLinearSolve.Norm(finalResidual), 0, 1e-11);
        // The recovered spine point sits on the support cylinder of radius 2.
        Assert.InRange(Math.Abs(Math.Sqrt(state[3] * state[3] + state[4] * state[4]) - 2), 0, 1e-10);
    }

    [Fact]
    public void SchurElimination_MatchesReferenceStep_AndFullSystem()
    {
        // RV-SCHUR data (5 unknowns: 3 outer + 2 inner), non-zero inner
        // residual h — the F_z·b term must survive (§12.3).
        Span<double> fX =
        [
            3.0, 0.2, -0.1,
            0.1, 2.0, 0.3,
            0.2, -0.4, 2.5,
        ];
        Span<double> fZ =
        [
            0.2, 0.3,
            -0.1, 0.4,
            0.5, -0.2,
        ];
        Span<double> hX =
        [
            0.3, 0.1, 0.2,
            -0.2, 0.4, 0.1,
        ];
        Span<double> hZ =
        [
            2.0, 0.2,
            0.1, 1.5,
        ];
        Span<double> f = [0.3, -0.4, 0.2];
        Span<double> h = [0.1, -0.15];
        Span<double> expected =
        [
            -0.11495047374815487,
            0.20044519081558593,
            -0.027125267557157315,
            -0.04366119339076882,
            0.035940316679284844,
        ];

        Span<double> workspace = new double[32];
        Span<double> step = new double[5];
        Assert.Equal(AlgorithmStatus.Success, BlockSchurSolve.ComputeStep(
            fX, fZ, hX, hZ, f, h, 3, 3, 2, workspace, step, out var innerScale));
        for (var i = 0; i < 5; i++)
            Assert.InRange(Math.Abs(step[i] - expected[i]), 0, 1e-12);
        Assert.True(innerScale > 0, "F_z·b must contribute while the inner block is unconverged");

        // The same step from the full-system LU solve (§12.4 equivalence).
        Span<double> full = new double[25];
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++) full[i * 5 + j] = fX[i * 3 + j];
            for (var j = 0; j < 2; j++) full[i * 5 + 3 + j] = fZ[i * 2 + j];
        }
        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 3; j++) full[(3 + i) * 5 + j] = hX[i * 3 + j];
            for (var j = 0; j < 2; j++) full[(3 + i) * 5 + 3 + j] = hZ[i * 2 + j];
        }
        Span<double> rhs = [-(f[0]), -(f[1]), -(f[2]), -(h[0]), -(h[1])];
        rhs[0] = -f[0]; rhs[1] = -f[1]; rhs[2] = -f[2]; rhs[3] = -h[0]; rhs[4] = -h[1];
        Span<int> pivots = new int[5];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuFactorize(full, 5, pivots));
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuSolveInPlace(full, 5, pivots, rhs));
        for (var i = 0; i < 5; i++)
            Assert.InRange(Math.Abs(rhs[i] - expected[i]), 0, 1e-12);
    }

    [Fact]
    public void SchurElimination_SingularInnerBlock_KeepsTheFullSystem()
    {
        // H_z rank-deficient: the elimination refuses and the caller keeps the
        // joint system instead of padding the pivot (§12.3).
        Span<double> fX = [1, 0, 0, 0, 1, 0, 0, 0, 1];
        Span<double> fZ = [0, 0, 0, 0, 0, 0];
        Span<double> hX = [0, 0, 0, 0, 0, 0];
        Span<double> hZ = [1, 2, 2, 4]; // rank 1
        Span<double> f = [1, 1, 1];
        Span<double> h = [1, 1];
        Span<double> workspace = new double[32];
        Span<double> step = new double[5];
        Assert.Equal(AlgorithmStatus.Singular, BlockSchurSolve.ComputeStep(
            fX, fZ, hX, hZ, f, h, 3, 3, 2, workspace, step, out _));
        for (var i = 0; i < 5; i++) Assert.True(double.IsFinite(step[i]));
    }

    [Fact]
    public void JointLift_FromOffRootSeed_RecoversSixUnknownRoot()
    {
        // Opt-in lift entry (T14): same RV-LIFTED-J geometry, seeded from the
        // query point and circular-spine angle — not wired into Auto.
        var spineSeed = Math.Atan2(StateC.Y, StateC.X);
        Span<double> state = new double[6];
        Assert.Equal(AlgorithmStatus.Success, BlendJointLift.TryLiftFromLocalSingular(
            in SupportA, in SupportD, in OuterPlane, in PlaneAnchor, in PlaneNormal,
            RadiusSq, in StateX, spineSeed, spineRadius: 2.0, state,
            out var iterations, out var residual));
        Assert.True(iterations >= 1);
        Assert.InRange(residual, 0, 1e-11);
        Assert.InRange(Math.Abs(Math.Sqrt(state[3] * state[3] + state[4] * state[4]) - 2), 0, 1e-10);
        Assert.InRange(Math.Abs(state[5]), 0, 1e-10);
    }
}
