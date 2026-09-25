using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;

namespace KernelTests;

/// <summary>
/// Small linear solves and correction steps for the icurve evaluator (spec
/// §14, task T05). Reference values come from the delivery package
/// reference_vectors.json (RV-SCHUR); the remaining cases use independently
/// constructible matrices with known rank, solution or trust-region geometry.
/// </summary>
public class IcurveNumericsTests
{
    private static double[] SchurSystemMatrix()
    {
        // [[Fx Fz] / [Hx Hz]] from RV-SCHUR, row-major 5×5.
        return
        [
            3.0, 0.2, -0.1, 0.2, 0.3,
            0.1, 2.0, 0.3, -0.1, 0.4,
            0.2, -0.4, 2.5, 0.5, -0.2,
            0.3, 0.1, 0.2, 2.0, 0.2,
            -0.2, 0.4, 0.1, 0.1, 1.5,
        ];
    }

    private static double[] SchurSystemRhs() => [-0.3, 0.4, -0.2, -0.1, 0.15];

    private static readonly double[] SchurExpectedStep =
    [
        -0.11495047374815487,
        0.20044519081558593,
        -0.027125267557157315,
        -0.04366119339076882,
        0.035940316679284844,
    ];

    [Fact]
    public void LuSolve_MatchesReferenceSchurNewtonStep()
    {
        var matrix = SchurSystemMatrix();
        var rhs = SchurSystemRhs();
        var pivots = new int[5];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuFactorize(matrix, 5, pivots));
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuSolveInPlace(matrix, 5, pivots, rhs));
        for (var i = 0; i < 5; i++) Assert.Equal(SchurExpectedStep[i], rhs[i], 12);
    }

    [Fact]
    public void LuFactorization_ServesMultipleRightHandSides()
    {
        var matrix = SchurSystemMatrix();
        var pivots = new int[5];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuFactorize(matrix, 5, pivots));
        Span<double> first = [-0.3, 0.4, -0.2, -0.1, 0.15];
        Span<double> second = [-0.6, 0.8, -0.4, -0.2, 0.3];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuSolveInPlace(matrix, 5, pivots, first));
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuSolveInPlace(matrix, 5, pivots, second));
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(SchurExpectedStep[i], first[i], 12);
            Assert.Equal(2 * SchurExpectedStep[i], second[i], 12);
        }
    }

    [Fact]
    public void LuFactorize_RankDeficientAndZero_ReportSingularWithoutNaN()
    {
        // Row 2 = 2·row 0, row 2 dependent: rank 1 system.
        Span<double> singular = [1, 2, 3, 2, 4, 6, 1, 1, 1];
        var pivots = new int[3];
        Assert.Equal(AlgorithmStatus.Singular, SmallLinearSolve.LuFactorize(singular, 3, pivots));
        for (var i = 0; i < 9; i++) Assert.True(double.IsFinite(singular[i]), "factorization wrote NaN/Inf");

        Span<double> zero = new double[9];
        Assert.Equal(AlgorithmStatus.Singular, SmallLinearSolve.LuFactorize(zero, 3, pivots));

        Span<double> nanInput = [double.NaN, 0, 0, 0, 1, 0, 0, 0, 1];
        Assert.Equal(AlgorithmStatus.InvalidInput, SmallLinearSolve.LuFactorize(nanInput, 3, pivots));
    }

    [Fact]
    public void LuSolve_HilbertSystem_StaysFiniteWithBoundedError()
    {
        const int n = 4;
        var hilbert = new double[n * n];
        for (var i = 0; i < n; i++)
            for (var j = 0; j < n; j++)
                hilbert[i * n + j] = 1.0 / (i + j + 1);
        Span<double> solution = [1, 2, 3, 4];
        Span<double> rhs = new double[n];
        SmallLinearSolve.Multiply(hilbert, n, n, solution, rhs);
        var pivots = new int[n];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuFactorize(hilbert, n, pivots));
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.LuSolveInPlace(hilbert, n, pivots, rhs));
        for (var i = 0; i < n; i++)
        {
            Assert.True(double.IsFinite(rhs[i]));
            Assert.InRange(Math.Abs(rhs[i] - (i + 1)), 0, 1e-6);
        }
    }

    [Fact]
    public void QrLeastSquares_OverdeterminedRecoversExactSolution()
    {
        // 4×2 system with exact solution x* = (2, 3); b = A x*.
        Span<double> a = [1, 0, 0, 1, 1, 1, 1, -1];
        Span<double> b = [2, 3, 5, -1];
        var tau = new double[2];
        var columnPivots = new int[2];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.QrFactorize(a, 4, 2, tau, columnPivots, 0, out var rank));
        Assert.Equal(2, rank);
        var x = new double[2];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.QrLeastSquares(a, 4, 2, tau, rank, columnPivots, b, x));
        Assert.Equal(2, x[0], 12);
        Assert.Equal(3, x[1], 12);
    }

    [Fact]
    public void QrFactorize_RankDeficient_ReportsRankAndConsistentSolution()
    {
        // 4×3, third column = first + second, last row zero: rank 2.
        Span<double> a = [1, 0, 1, 0, 1, 1, 2, 1, 3, 0, 0, 0];
        Span<double> b = [1, 1, 3, 0]; // = A·(1,1,0), consistent on the rank-2 column space
        var tau = new double[3];
        var columnPivots = new int[3];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.QrFactorize(a, 4, 3, tau, columnPivots, 1e-14, out var rank));
        Assert.Equal(2, rank);
        var x = new double[3];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.QrLeastSquares(a, 4, 3, tau, rank, columnPivots, b, x));
        for (var i = 0; i < 3; i++) Assert.True(double.IsFinite(x[i]));
        // The residual of the consistent system must vanish on the column space.
        Span<double> ax = new double[4];
        SmallLinearSolve.Multiply([1, 0, 1, 0, 1, 1, 2, 1, 3, 0, 0, 0], 4, 3, x, ax);
        Assert.InRange(Math.Abs(ax[0] - 1), 0, 1e-12);
        Assert.InRange(Math.Abs(ax[1] - 1), 0, 1e-12);
        Assert.InRange(Math.Abs(ax[2] - 3), 0, 1e-12);
        Assert.InRange(Math.Abs(ax[3]), 0, 1e-12);
    }

    [Fact]
    public void SvdMinNorm_RankDeficientSquare_RecoversKnownSolution()
    {
        // A = [[1,0,0],[0,1,0],[0,0,0]], b = [2,3,0] → min-norm x = [2,3,0].
        Span<double> a = [1, 0, 0, 0, 1, 0, 0, 0, 0];
        Span<double> sigma = new double[3];
        Span<double> u = new double[9];
        Span<double> v = new double[9];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.SvdFactorizeSquare(
            a, 3, sigma, u, v, 1e-14, out var rank));
        Assert.Equal(2, rank);
        Assert.True(sigma[0] >= sigma[1] && sigma[1] > sigma[2]);
        Span<double> b = [2, 3, 0];
        Span<double> x = new double[3];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.SvdMinNormSolve(sigma, u, v, 3, rank, b, x));
        Assert.Equal(2, x[0], 10);
        Assert.Equal(3, x[1], 10);
        Assert.InRange(Math.Abs(x[2]), 0, 1e-12);
    }

    [Fact]
    public void SvdFactorize_FullRank_MatchesIdentitySingularValues()
    {
        Span<double> a = [2, 0, 0, 0, 3, 0, 0, 0, 4];
        Span<double> sigma = new double[3];
        Span<double> u = new double[9];
        Span<double> v = new double[9];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.SvdFactorizeSquare(
            a, 3, sigma, u, v, 0, out var rank));
        Assert.Equal(3, rank);
        Assert.Equal(4, sigma[0], 10);
        Assert.Equal(3, sigma[1], 10);
        Assert.Equal(2, sigma[2], 10);
    }

    [Fact]
    public void SvdFactorize_IllConditioned_RecoversSmallSingularValueWithoutGramSquaring()
    {
        // A = [[1, 1], [0, 1e-10]]. Forming AᵀA squares the condition number,
        // rounding 1 + 1e-20 to 1 and obliterating σ_min. One-sided Jacobi
        // preserves σ_min ≈ 7.07e-11 and identifies rank 2 under tol 1e-12.
        Span<double> a = [1.0, 1.0, 0.0, 1e-10];
        Span<double> sigma = new double[2];
        Span<double> u = new double[4];
        Span<double> v = new double[4];
        Assert.Equal(AlgorithmStatus.Success, SmallLinearSolve.SvdFactorizeSquare(
            a, 2, sigma, u, v, 1e-12, out var rank));
        Assert.Equal(2, rank);
        Assert.InRange(sigma[0], Math.Sqrt(2.0) - 1e-8, Math.Sqrt(2.0) + 1e-8);
        Assert.InRange(sigma[1], 7.0e-11, 7.1e-11);

        // Verify A = U Σ Vᵀ reconstruction
        for (var r = 0; r < 2; r++)
        for (var c = 0; c < 2; c++)
        {
            var recon = 0.0;
            for (var k = 0; k < 2; k++)
                recon += u[r * 2 + k] * sigma[k] * v[c * 2 + k];
            Assert.InRange(Math.Abs(recon - a[r * 2 + c]), 0, 1e-14);
        }
    }

    [Fact]
    public void NewtonStep_QuadraticWithArmijo_ConvergesToRoot()
    {
        // SPD system: F(y) = A (y − y*), root y* = (1, −2, 0.5).
        Span<double> a = [4, 1, 0, 1, 3, -1, 0, -1, 2];
        Span<double> root = [1, -2, 0.5];
        var y = new double[] { 8, 6, -4 };
        var residual = new double[3];
        var jacobian = new double[9];
        var jacobianCopy = new double[9];
        var model = new double[3];
        var step = new double[3];
        var pivots = new int[3];
        for (var iteration = 0; iteration < 20; iteration++)
        {
            for (var i = 0; i < 3; i++)
            {
                var sum = 0.0;
                for (var j = 0; j < 3; j++) sum += a[i * 3 + j] * (y[j] - root[j]);
                residual[i] = sum;
            }
            if (SmallLinearSolve.Norm(residual) < 1e-10) break;
            a.CopyTo(jacobian);
            Assert.Equal(AlgorithmStatus.Success, NewtonStep.ComputeStep(jacobian, jacobianCopy, residual, 3, pivots, model, step, out var predicted));
            // Exact linear solve: prediction equals ½‖r‖² up to rounding.
            Assert.InRange(predicted, 0.5 * SmallLinearSolve.Dot(residual, residual) - 1e-8, 0.5 * SmallLinearSolve.Dot(residual, residual) + 1e-8);
            for (var i = 0; i < 3; i++) y[i] += step[i];
        }
        Assert.InRange(SmallLinearSolve.Norm(residual), 0, 1e-10);
    }

    [Fact]
    public void DoglegStep_LargeRadius_TakesTheNewtonStep()
    {
        Span<double> a = [4, 1, 0, 1, 3, -1, 0, -1, 2];
        Span<double> residual = [-2, 1, 0.5];
        var jacobian = new double[9];
        var copy = new double[9];
        a.CopyTo(jacobian);
        jacobian.CopyTo(copy);
        var pivots = new int[3];
        var tau = new double[3];
        var columnPivots = new int[3];
        var gradient = new double[3];
        var newton = new double[3];
        var step = new double[3];
        var model = new double[3];
        var pivotsWorkspace = new int[3];
        var jacobianCopy = new double[9];
        // Independent Newton direction from the LU module.
        Span<double> reference = [-2, 1, 0.5];
        Assert.Equal(AlgorithmStatus.Success, NewtonStep.ComputeStep(copy, jacobianCopy, reference, 3, pivotsWorkspace, model, step, out _));
        for (var i = 0; i < 3; i++) reference[i] = step[i];

        var status = TrustRegionStep.DoglegStep(jacobian, copy, [-2, 1, 0.5], 3, 1e6,
            pivots, tau, columnPivots, gradient, newton, step, out var predicted);
        Assert.Equal(AlgorithmStatus.Success, status);
        for (var i = 0; i < 3; i++) Assert.Equal(reference[i], step[i], 10);
        Assert.InRange(predicted, 0.5 * SmallLinearSolve.Dot([-2, 1, 0.5], [-2, 1, 0.5]) - 1e-8,
            0.5 * SmallLinearSolve.Dot([-2, 1, 0.5], [-2, 1, 0.5]) + 1e-8);
    }

    [Fact]
    public void DoglegStep_SmallRadius_TrustBoundaryAndPositivePrediction()
    {
        Span<double> a = [4, 1, 0, 1, 3, -1, 0, -1, 2];
        Span<double> residual = [-2, 1, 0.5];
        var jacobian = new double[9];
        var copy = new double[9];
        a.CopyTo(jacobian);
        jacobian.CopyTo(copy);
        var pivots = new int[3];
        var tau = new double[3];
        var columnPivots = new int[3];
        var gradient = new double[3];
        var newton = new double[3];
        var step = new double[3];

        const double radius = 0.05;
        var status = TrustRegionStep.DoglegStep(jacobian, copy, residual, 3, radius,
            pivots, tau, columnPivots, gradient, newton, step, out var predicted);
        Assert.Equal(AlgorithmStatus.Success, status);
        Assert.InRange(SmallLinearSolve.Norm(step), radius - 1e-12, radius + 1e-12);
        Assert.True(predicted > 0, "boundary step must predict a decrease");
    }

    [Fact]
    public void ReductionRatio_AndRadiusUpdate_FollowStartingPolicy()
    {
        Assert.Equal(-1, TrustRegionStep.ReductionRatio(2, 3, 0));
        Assert.Equal(0.5, TrustRegionStep.ReductionRatio(2, 1, 2), 12);

        var radius = 1.0;
        radius = TrustRegionStep.UpdateRadius(radius, 0.1, stepAtBoundary: true, 1e-12, 64);
        Assert.Equal(0.25, radius, 12);
        radius = TrustRegionStep.UpdateRadius(radius, 0.8, stepAtBoundary: true, 1e-12, 64);
        Assert.Equal(0.5, radius, 12);
        radius = TrustRegionStep.UpdateRadius(radius, 0.8, stepAtBoundary: false, 1e-12, 64);
        Assert.Equal(0.5, radius, 12);
        radius = TrustRegionStep.UpdateRadius(radius, -1, stepAtBoundary: true, 1e-12, 64);
        Assert.Equal(0.125, radius, 12);
    }

    [Fact]
    public void DoglegStep_ZeroResidualAndZeroGradient_NoNaN()
    {
        Span<double> a = [4, 1, 0, 1, 3, -1, 0, -1, 2];
        var jacobian = new double[9];
        var copy = new double[9];
        a.CopyTo(jacobian);
        jacobian.CopyTo(copy);
        var pivots = new int[3];
        var tau = new double[3];
        var columnPivots = new int[3];
        var gradient = new double[3];
        var newton = new double[3];
        var step = new double[3];

        Span<double> zero = new double[3];
        Assert.Equal(AlgorithmStatus.Success, TrustRegionStep.DoglegStep(jacobian, copy, zero, 3, 1.0,
            pivots, tau, columnPivots, gradient, newton, step, out var predicted));
        Assert.Equal(0, predicted, 12);
        for (var i = 0; i < 3; i++) Assert.Equal(0, step[i], 12);

        Span<double> singular = new double[9];
        singular.CopyTo(jacobian);
        Assert.Equal(AlgorithmStatus.Singular, TrustRegionStep.DoglegStep(jacobian, copy, [-1, 1, 0.5], 3, 1.0,
            pivots, tau, columnPivots, gradient, newton, step, out _));
        for (var i = 0; i < 3; i++) Assert.True(double.IsFinite(step[i]));
    }
}
