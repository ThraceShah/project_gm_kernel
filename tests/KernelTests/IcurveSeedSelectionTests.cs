using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Intersection;

namespace KernelTests;

/// <summary>
/// Seed selection and parameter correspondence tests (spec §13.5–§13.6, task
/// T06): periodic unwrapping keeps an explicit lift, Hermite prediction
/// reproduces cubics exactly, ordering is deterministic and never driven by
/// spatial proximity, and symmetric cross-branch candidates report ambiguity
/// instead of being broken randomly.
/// </summary>
public class IcurveSeedSelectionTests
{
    private const double Tau = Math.Tau;

    [Fact]
    public void Unwrap_ChoosesNearestSheet_AndReportsLift()
    {
        // 0.1 near reference 6.2 lives on the next sheet: 0.1 + 2π.
        var unwrapped = ParameterCorrespondence.UnwrapWithLift(0.1, 6.2, Tau, out var lift);
        Assert.Equal(1, lift);
        Assert.Equal(0.1 + Tau, unwrapped, 12);

        // 6.2 near reference 0.1 lives on the previous sheet: 6.2 − 2π.
        unwrapped = ParameterCorrespondence.UnwrapWithLift(6.2, 0.1, Tau, out lift);
        Assert.Equal(-1, lift);
        Assert.Equal(6.2 - Tau, unwrapped, 12);

        // Already on the nearest sheet: lift 0, value unchanged.
        unwrapped = ParameterCorrespondence.UnwrapWithLift(-0.3, 0.2, Tau, out lift);
        Assert.Equal(0, lift);
        Assert.Equal(-0.3, unwrapped, 12);
    }

    [Fact]
    public void HermitePrediction_ReproducesCubicExactly()
    {
        // y(t) = t³ on [1, 2]: samples (1, 1, 3) and (2, 8, 12).
        Span<double> predicted = new double[1];
        Assert.Equal(AlgorithmStatus.Success, ParameterCorrespondence.TryHermitePredict(
            1.0, stackalloc double[] { 1.0 }, stackalloc double[] { 3.0 },
            2.0, stackalloc double[] { 8.0 }, stackalloc double[] { 12.0 },
            1.5, predicted));
        Assert.Equal(3.375, predicted[0], 12);

        Assert.Equal(AlgorithmStatus.Success, ParameterCorrespondence.TryHermitePredict(
            1.0, stackalloc double[] { 1.0 }, stackalloc double[] { 3.0 },
            2.0, stackalloc double[] { 8.0 }, stackalloc double[] { 12.0 },
            1.73, predicted));
        Assert.Equal(Math.Pow(1.73, 3), predicted[0], 10);
    }

    [Fact]
    public void HermitePrediction_PreservesUnwrappedSheet()
    {
        // Samples on the 2π-1 seam: values unwrapped to the sheet around 2π
        // before interpolation; the prediction stays on that sheet, it must
        // not fold back across the seam.
        var ya = ParameterCorrespondence.Unwrap(Tau - 0.01, Tau - 0.01, Tau); // identity on-sheet
        var yb = ParameterCorrespondence.Unwrap(0.01 + Tau, ya, Tau);         // next-sheet value lifted
        Span<double> predicted = new double[1];
        Assert.Equal(AlgorithmStatus.Success, ParameterCorrespondence.TryHermitePredict(
            Tau - 0.01, new[] { ya }, new[] { 1.0 },
            0.01 + Tau, new[] { yb }, new[] { 1.0 },
            (Tau - 0.01 + 0.01 + Tau) / 2, predicted));
        // The midpoint lands exactly on the seam value τ; on the lifted sheet
        // it must read τ, never fold back to ≈0.
        Assert.InRange(Math.Abs(predicted[0] - Tau), 0, 1e-9);
    }

    [Fact]
    public void OrderSeeds_SourcePriority_BeforeAnySpatialNotion()
    {
        // The spatially closer candidate (smaller |t − target|) sits on a
        // different branch and carries worse quality: ordering must keep the
        // verified exact sample first — proximity never wins (§13.6).
        SeedCandidate[] candidates =
        [
            new(0.52, SeedSource.ExactSample, SeedQuality.Verified, branchId: 0, sourceIndex: 7, errorEstimate: 1e-13, continuityCell: 2),
            new(0.51, SeedSource.LocalSearch, SeedQuality.SeedOnly, branchId: 1, sourceIndex: 3, errorEstimate: 1e-2, continuityCell: 2),
        ];
        Span<SeedCandidate> ordered = new SeedCandidate[8];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveSeedSelection.OrderSeeds(candidates, 0, ordered, out var count, out var failure));
        Assert.Equal(SeedOrderFailure.None, failure);
        Assert.Equal(1, count);
        Assert.Equal(SeedSource.ExactSample, ordered[0].Source);
    }

    [Fact]
    public void OrderSeeds_DeterministicBySourceQualityErrorThenStableIndex()
    {
        SeedCandidate[] candidates =
        [
            new(0.30, SeedSource.NeighborPrediction, SeedQuality.Predicted, -1, sourceIndex: 9, errorEstimate: 1e-4, -1),
            new(0.10, SeedSource.ChartAnchor, SeedQuality.SeedOnly, -1, sourceIndex: 1, errorEstimate: 1e-9, -1),
            new(0.20, SeedSource.NeighborPrediction, SeedQuality.Predicted, -1, sourceIndex: 4, errorEstimate: 1e-5, -1),
            new(0.15, SeedSource.NeighborPrediction, SeedQuality.Predicted, -1, sourceIndex: 6, errorEstimate: 1e-5, -1),
        ];
        Span<SeedCandidate> ordered = new SeedCandidate[8];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveSeedSelection.OrderSeeds(candidates, -1, ordered, out var count, out _));
        Assert.Equal(4, count);
        Assert.Equal(0.20, ordered[0].Parameter, 12);   // same source/quality, smallest error
        Assert.Equal(0.15, ordered[1].Parameter, 12);   // tied error, stable source index
        Assert.Equal(0.30, ordered[2].Parameter, 12);   // larger error
        Assert.Equal(0.10, ordered[3].Parameter, 12);   // chart anchor after predictions

        // Re-running with a permuted input yields the identical order.
        (candidates[0], candidates[3]) = (candidates[3], candidates[0]);
        Span<SeedCandidate> reordered = new SeedCandidate[8];
        Assert.Equal(AlgorithmStatus.Success,
            ICurveSeedSelection.OrderSeeds(candidates, -1, reordered, out _, out _));
        for (var i = 0; i < 4; i++) Assert.Equal(ordered[i].Parameter, reordered[i].Parameter, 12);
    }

    [Fact]
    public void OrderSeeds_SymmetricCrossBranchTie_ReportsAmbiguity()
    {
        // Two verified samples on different branches with indistinguishable
        // error and no requested branch: the choice is semantic, not numeric.
        SeedCandidate[] candidates =
        [
            new(0.40, SeedSource.ExactSample, SeedQuality.Verified, branchId: 0, sourceIndex: 1, errorEstimate: 1e-13, -1),
            new(0.40 + 1e-15, SeedSource.ExactSample, SeedQuality.Verified, branchId: 1, sourceIndex: 2, errorEstimate: 1e-13, -1),
        ];
        Span<SeedCandidate> ordered = new SeedCandidate[8];
        Assert.Equal(AlgorithmStatus.NotConverged,
            ICurveSeedSelection.OrderSeeds(candidates, -1, ordered, out _, out var failure));
        Assert.Equal(SeedOrderFailure.AmbiguousBranch, failure);

        // A requested branch resolves the tie semantically.
        Assert.Equal(AlgorithmStatus.Success,
            ICurveSeedSelection.OrderSeeds(candidates, 1, ordered, out var count, out _));
        Assert.Equal(1, count);
        Assert.Equal(1, ordered[0].BranchId);
    }

    [Fact]
    public void OrderSeeds_EmptyAfterFiltering_IsNotSuccess()
    {
        SeedCandidate[] candidates =
        [
            new(0.4, SeedSource.ChartAnchor, SeedQuality.SeedOnly, branchId: 0, sourceIndex: 1, errorEstimate: 1e-3, -1),
        ];
        Span<SeedCandidate> ordered = new SeedCandidate[8];
        Assert.Equal(AlgorithmStatus.NotConverged,
            ICurveSeedSelection.OrderSeeds(candidates, 1, ordered, out _, out var failure));
        Assert.Equal(SeedOrderFailure.Empty, failure);
    }

    [Fact]
    public void OrderSeeds_CrossBranchWithSmallerResidual_IsNeverPreferredOverRequestedBranch()
    {
        // Candidate 0: on wrong branch (branchId 1), but with tiny errorEstimate = 1e-15
        // Candidate 1: on requested branch (branchId 0), with larger errorEstimate = 1e-4
        SeedCandidate[] candidates =
        [
            new(0.4, SeedSource.NeighborPrediction, SeedQuality.Predicted, branchId: 1, sourceIndex: 1, errorEstimate: 1e-15, continuityCell: 0),
            new(0.4, SeedSource.ChartAnchor, SeedQuality.SeedOnly, branchId: 0, sourceIndex: 2, errorEstimate: 1e-4, continuityCell: 0),
        ];
        Span<SeedCandidate> ordered = new SeedCandidate[4];

        // Ordering with requestedBranch = 0 MUST filter out the branch 1 candidate despite its smaller residual
        Assert.Equal(AlgorithmStatus.Success,
            ICurveSeedSelection.OrderSeeds(candidates, 0, ordered, out var count, out var failure));
        Assert.Equal(SeedOrderFailure.None, failure);
        Assert.Equal(1, count);
        Assert.Equal(0, ordered[0].BranchId);
        Assert.Equal(SeedSource.ChartAnchor, ordered[0].Source);
    }
}
