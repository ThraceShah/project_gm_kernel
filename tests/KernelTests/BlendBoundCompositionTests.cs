using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace KernelTests;

/// <summary>
/// BLEND_BOUND distance-composition math tests (spec §11.1, task T15): the
/// document-literal F_B jet against the RV-BBOUND-COMPOSITION reference (two
/// sphere distances, including the required D₃ contraction term), and a
/// central-difference cross-check of the assembled gradient. This module is
/// math-only — the GATE-B role mapping stays open, so no production path
/// consumes it and no BlendBound parameterization is invented (§11.4).
/// </summary>
public class BlendBoundCompositionTests
{
    // RV-BBOUND-COMPOSITION fixture.
    private static readonly KernelVector3 Point = Vector(2.0, 0.7, 0.5);
    private static readonly KernelVector3 Center0 = Vector(0.3, -0.4, 0.2);
    private const double Sphere0Radius = 0.8;
    private static readonly KernelVector3 Center1 = Vector(-0.2, 0.1, 0.3);
    private const double Sphere1Radius = 1.1;
    private const double R0 = 0.25;
    private const double R1 = 0.3;

    [Fact]
    public void Composition_MatchesReferenceVectors_IncludingD3Contraction()
    {
        var input = BuildInput(in Point);
        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.Evaluate(
            in input, R0, R1, out var value, out var gradient,
            out var hxx, out var hxy, out var hxz, out var hyy, out var hyz, out var hzz));

        Assert.Equal(1.284312752784525, value, 12);
        Assert.Equal(0.8420948490411034, gradient.X, 12);
        Assert.Equal(0.5380025167628464, gradient.Y, 12);
        Assert.Equal(0.14703185666125354, gradient.Z, 12);
        Assert.Equal(0.13766572029615562, hxx, 12);
        Assert.Equal(-0.22311258096154538, hxy, 12);
        Assert.Equal(-0.06114648125013602, hxz, 12);
        Assert.Equal(0.35688713914050846, hyy, 12);
        Assert.Equal(-0.03746145139159731, hyz, 12);
        Assert.Equal(0.4825822565761844, hzz, 12);
    }

    [Fact]
    public void D3ContractionTerm_IsNotDroppable()
    {
        // The reference isolates the contraction contribution: assembling the
        // Hessian WITHOUT the r₁·T term must disagree with the reference —
        // proving the test catches the §11.1 violation.
        var input = BuildInput(in Point);
        BlendBoundComposition.Evaluate(in input, R0, R1, out _, out _, out var hxx, out _, out _, out _, out _, out _);

        // Recompute with T = 0: the difference is exactly r₁·T_xx.
        var without = new BlendBoundComposition.CompositionInput(
            input.D0Value, input.D0Gradient,
            input.D0Hxx, input.D0Hxy, input.D0Hxz, input.D0Hyy, input.D0Hyz, input.D0Hzz,
            input.D1Gradient,
            input.D1Hxx, input.D1Hxy, input.D1Hxz, input.D1Hyy, input.D1Hyz, input.D1Hzz,
            0, 0, 0, 0, 0, 0, 0, 0, 0);
        BlendBoundComposition.Evaluate(in without, R0, R1, out _, out _, out var hxxZero, out _, out _, out _, out _, out _);
        Assert.NotEqual(0, hxx - hxxZero, 10);
    }

    [Fact]
    public void Gradient_MatchesCentralDifference_OfTheComposedFunction()
    {
        // Independent path: evaluate F_B(x) = d₀(x + r₁∇d₁(x)) − r₀ by closed
        // forms at x±h and difference (§21.2).
        const double h = 1e-6;
        for (var axis = 0; axis < 3; axis++)
        {
            var plus = Point;
            var minus = Point;
            SetComponent(ref plus, axis, GetComponent(in Point, axis) + h);
            SetComponent(ref minus, axis, GetComponent(in Point, axis) - h);
            var fp = ComposeValue(in plus);
            var fm = ComposeValue(in minus);
            var input = BuildInput(in Point);
            BlendBoundComposition.Evaluate(in input, R0, R1, out _, out var gradient,
                out _, out _, out _, out _, out _, out _);
            var numerical = GetComponent(gradient, axis);
            var expected = (fp - fm) / (2 * h);
            Assert.InRange(Math.Abs(numerical - expected), 0, 1e-8);
        }
    }

    [Fact]
    public void CylinderDistanceJets_AxialInvariance_ContractionIsIdenticallyZero()
    {
        // Axial invariance: moving along cylinder axis does not change the radial distance Hessian.
        // For g parallel to cylinder axis, T(g) must be identically zero (§11.1).
        var center = Vector(0, 0, 0);
        var axisZ = Vector(0, 0, 1);
        var point = Vector(2.0, 0.5, 1.0);
        var gAxial = Vector(0, 0, 3.5);

        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axisZ, 1.0, in point, in gAxial,
            out _, out _, out _, out _, out _, out _, out _, out _,
            out var txx, out var txy, out var txz, out var tyy, out var tyz, out var tzz));

        Assert.Equal(0.0, txx, 14);
        Assert.Equal(0.0, txy, 14);
        Assert.Equal(0.0, txz, 14);
        Assert.Equal(0.0, tyy, 14);
        Assert.Equal(0.0, tyz, 14);
        Assert.Equal(0.0, tzz, 14);

        // Tilted cylinder with arbitrary axis.
        var axisTilted = Vector(1, 2, 3);
        var unitAxis = Scale(axisTilted, 1 / Math.Sqrt(14));
        var gTilted = Scale(unitAxis, -2.7);
        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axisTilted, 1.5, in point, in gTilted,
            out _, out _, out _, out _, out _, out _, out _, out _,
            out txx, out txy, out txz, out tyy, out tyz, out tzz));

        Assert.Equal(0.0, txx, 13);
        Assert.Equal(0.0, txy, 13);
        Assert.Equal(0.0, txz, 13);
        Assert.Equal(0.0, tyy, 13);
        Assert.Equal(0.0, tyz, 13);
        Assert.Equal(0.0, tzz, 13);
    }

    [Fact]
    public void CylinderDistanceJets_MatchesCentralDifference()
    {
        // Numerical cross-check: directional derivative of Hessian along g
        // matches the analytical third-derivative tensor contraction T(g).
        var center = Vector(0.2, -0.3, 0.5);
        var axis = Vector(0.5, -0.2, 0.8);
        var point = Vector(1.8, 2.1, -0.4);
        var g = Vector(0.7, -0.4, 0.3);
        var delta = 1e-5;

        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axis, 0.9, in point, in g,
            out _, out _, out _, out _, out _, out _, out _, out _,
            out var txx, out var txy, out var txz, out var tyy, out var tyz, out var tzz));

        var pPlus = Add(point, Scale(g, delta));
        var pMinus = Sub(point, Scale(g, delta));

        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axis, 0.9, in pPlus, default,
            out _, out _, out var hxxP, out var hxyP, out var hxzP, out var hyyP, out var hyzP, out var hzzP,
            out _, out _, out _, out _, out _, out _));

        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axis, 0.9, in pMinus, default,
            out _, out _, out var hxxM, out var hxyM, out var hxzM, out var hyyM, out var hyzM, out var hzzM,
            out _, out _, out _, out _, out _, out _));

        Assert.Equal((hxxP - hxxM) / (2 * delta), txx, 8);
        Assert.Equal((hxyP - hxyM) / (2 * delta), txy, 8);
        Assert.Equal((hxzP - hxzM) / (2 * delta), txz, 8);
        Assert.Equal((hyyP - hyyM) / (2 * delta), tyy, 8);
        Assert.Equal((hyzP - hyzM) / (2 * delta), tyz, 8);
        Assert.Equal((hzzP - hzzM) / (2 * delta), tzz, 8);
    }

    [Fact]
    public void CylinderDistanceJets_LinearComposition_HessianIsIdenticallyZero()
    {
        // When d0 is linear along the cylinder axis (d0(y) = y_z, H0 = 0),
        // F_B(x) = d0(x + r1 ∇d1(x)) - r0 = x_z - r0 is linear.
        // Its composition Hessian must be zero everywhere (§11.1).
        var center = Vector(0, 0, 0);
        var axis = Vector(0, 0, 1);
        var point = Vector(2.0, 0.7, 0.5);
        var r0 = 0.25;
        var r1 = 0.3;
        var gradD0 = Vector(0, 0, 1);

        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.CylinderDistanceJets(
            in center, in axis, 1.0, in point, in gradD0,
            out _, out var gradD1, out var hxx1, out var hxy1, out var hxz1, out var hyy1, out var hyz1, out var hzz1,
            out var txx, out var txy, out var txz, out var tyy, out var tyz, out var tzz));

        var input = new BlendBoundComposition.CompositionInput(
            point.Z - r0, gradD0,
            0, 0, 0, 0, 0, 0,
            gradD1, hxx1, hxy1, hxz1, hyy1, hyz1, hzz1,
            txx, txy, txz, txy, tyy, tyz, txz, tyz, tzz);

        Assert.Equal(AlgorithmStatus.Success, BlendBoundComposition.Evaluate(
            in input, r0, r1, out _, out _,
            out var hxx, out var hxy, out var hxz, out var hyy, out var hyz, out var hzz));

        Assert.Equal(0.0, hxx, 14);
        Assert.Equal(0.0, hxy, 14);
        Assert.Equal(0.0, hxz, 14);
        Assert.Equal(0.0, hyy, 14);
        Assert.Equal(0.0, hyz, 14);
        Assert.Equal(0.0, hzz, 14);
    }

    private static double ComposeValue(in KernelVector3 x)
    {
        BlendBoundComposition.SphereDistanceJets(in Center1, Sphere1Radius, in x, default,
            out _, out var d1Gradient, out _, out _, out _, out _, out _, out _,
            out _, out _, out _, out _, out _, out _);
        var y = Add(x, Scale(d1Gradient, R1));
        BlendBoundComposition.SphereDistanceJets(in Center0, Sphere0Radius, in y, default,
            out var d0Value, out _, out _, out _, out _, out _, out _, out _,
            out _, out _, out _, out _, out _, out _);
        return d0Value - R0;
    }

    private static BlendBoundComposition.CompositionInput BuildInput(in KernelVector3 x)
    {
        // d₁ jets at x; y = x + r₁∇d₁(x); d₀ jets at y; contraction T from the
        // d₀ gradient at y against d₁'s third derivatives at x.
        BlendBoundComposition.SphereDistanceJets(in Center1, Sphere1Radius, in x, default,
            out _, out var d1Gradient, out var d1Hxx, out var d1Hxy, out var d1Hxz,
            out var d1Hyy, out var d1Hyz, out var d1Hzz,
            out _, out _, out _, out _, out _, out _);
        var y = Add(x, Scale(d1Gradient, R1));
        BlendBoundComposition.SphereDistanceJets(in Center0, Sphere0Radius, in y, default,
            out var d0Value, out var d0Gradient, out var d0Hxx, out var d0Hxy, out var d0Hxz,
            out var d0Hyy, out var d0Hyz, out var d0Hzz,
            out _, out _, out _, out _, out _, out _);
        // T = Σₖ g₀ₖ ∂²(∂ₖd₁)/∂x_i∂x_j with g₀ = ∇d₀(y) (document formula).
        BlendBoundComposition.SphereDistanceJets(in Center1, Sphere1Radius, in x, d0Gradient,
            out _, out _, out _, out _, out _, out _, out _, out _,
            out var txx, out var txy, out var txz, out var tyy, out var tyz, out var tzz);
        return new BlendBoundComposition.CompositionInput(
            d0Value, d0Gradient, d0Hxx, d0Hxy, d0Hxz, d0Hyy, d0Hyz, d0Hzz,
            d1Gradient, d1Hxx, d1Hxy, d1Hxz, d1Hyy, d1Hyz, d1Hzz,
            txx, txy, txz, txy, tyy, tyz, txz, tyz, tzz);
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

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
