using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// Envelope Newton 4×4 / 3×3 solvers (spec §10.4, task T13 completion).
/// Manufactured roots on the RV-TUBE circular spine; not wired into Auto.
/// </summary>
public class BlendEnvelopeSolveTests
{
    private const double SpineRadius = 2.0;
    private const double TubeRadius = 0.25;

    private static KernelVector3 SpinePoint(double s)
        => Vector(SpineRadius * Math.Cos(s), SpineRadius * Math.Sin(s), 0);

    private static KernelVector3 SurfacePoint(double s, double theta)
    {
        var radial = Vector(Math.Cos(s), Math.Sin(s), 0);
        var yDir = Vector(0, 0, 1);
        return Add(SpinePoint(s), Scale(Add(Scale(radial, Math.Cos(theta)), Scale(yDir, Math.Sin(theta))), TubeRadius));
    }

    [Fact]
    public void FourByFour_RecoversManufacturedTubeRoot()
    {
        const double s = 0.4;
        const double theta = 0.43;
        var root = SurfacePoint(s, theta);
        // Outer support: horizontal plane through the root (φ = z − root.Z).
        var outer = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, root.Z), Vector(0, 0, 1), Vector(1, 0, 0));
        // Chord plane through the root with a tilted normal.
        var planeNormal = Vector(-0.4, 0.8, 0.2);
        var planeAnchor = root;

        Span<double> state =
        [
            root.X + 0.01,
            root.Y - 0.008,
            root.Z + 0.005,
            s - 0.05,
        ];
        Assert.Equal(AlgorithmStatus.Success, BlendEnvelopeSolve.SolveFourByFour(
            SpineRadius, TubeRadius, in outer, in planeAnchor, in planeNormal,
            state, out var iterations, out var residual));
        Assert.True(iterations >= 1);
        Assert.InRange(residual, 0, 1e-12);
        Assert.InRange(Math.Abs(state[0] - root.X), 0, 1e-10);
        Assert.InRange(Math.Abs(state[1] - root.Y), 0, 1e-10);
        Assert.InRange(Math.Abs(state[2] - root.Z), 0, 1e-10);
        Assert.InRange(Math.Abs(state[3] - s), 0, 1e-10);
    }

    [Fact]
    public void ThreeByThree_RecoversManufacturedInPlaneRoot()
    {
        const double s = 0.4;
        const double theta = 0.43;
        var root = SurfacePoint(s, theta);
        // Outer support is the horizontal plane through the root; the chord
        // plane is tilted so φ_A is not identically zero under the in-plane
        // unknowns (otherwise the 3×3 Jacobian row would vanish).
        var outer = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, root.Z), Vector(0, 0, 1), Vector(1, 0, 0));
        var planeNormal = Unit(Vector(-0.4, 0.8, 0.2));
        var planeAnchor = root;
        var basisU = Unit(Cross(planeNormal, Vector(0, 0, 1)));
        var basisV = Cross(planeNormal, basisU);

        Span<double> state = [0.012, -0.009, s - 0.04];
        Assert.Equal(AlgorithmStatus.Success, BlendEnvelopeSolve.SolveThreeByThree(
            SpineRadius, TubeRadius, in outer, in planeAnchor, in planeNormal,
            in basisU, in basisV, state, out _, out var residual));
        Assert.InRange(residual, 0, 1e-12);
        Assert.InRange(Math.Abs(state[0]), 0, 1e-10);
        Assert.InRange(Math.Abs(state[1]), 0, 1e-10);
        Assert.InRange(Math.Abs(state[2] - s), 0, 1e-10);
    }

    [Fact]
    public void FourByFour_RejectsNonPositiveRadii()
    {
        var outer = new AnalyticSurface(SurfaceClass.Plane,
            Vector(0, 0, 0), Vector(0, 0, 1), Vector(1, 0, 0));
        var anchor = Vector(0, 0, 0);
        var normal = Vector(0, 0, 1);
        Span<double> state = [1, 0, 0, 0];
        Assert.Equal(AlgorithmStatus.InvalidInput, BlendEnvelopeSolve.SolveFourByFour(
            0, TubeRadius, in outer, in anchor, in normal, state, out _, out _));
        Assert.Equal(AlgorithmStatus.WorkspaceTooSmall, BlendEnvelopeSolve.SolveFourByFour(
            SpineRadius, TubeRadius, in outer, in anchor, in normal, state[..3], out _, out _));
    }
}
