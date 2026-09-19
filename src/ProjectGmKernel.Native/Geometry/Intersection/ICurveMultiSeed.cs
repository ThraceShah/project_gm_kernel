using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Same-branch multi-seed loop (spec §13.6 / T09): OrderSeeds then try each
/// candidate until a corrector succeeds. AmbiguousBranch is never broken by
/// spatial nearest-point.
/// </summary>
internal static class ICurveMultiSeed
{
    internal const int MaxSameBranchSeeds = 4;

    /// <summary>
    /// Build up to four same-segment seed candidates (chart anchors + optional
    /// Hermite prediction + chord) and order them. On AmbiguousBranch the
    /// caller must not pick by proximity.
    /// </summary>
    internal static AlgorithmStatus BuildOrderedSeeds(in ICurveView view, double t, BufferOffset segment,
        in KernelVector3 chordSeed, in KernelVector3 predictedSeed, bool hasPrediction,
        Span<SeedCandidate> ordered, out BufferCount count, out SeedOrderFailure failure)
    {
        Span<SeedCandidate> raw = stackalloc SeedCandidate[MaxSameBranchSeeds + 2];
        BufferCount n = 0;
        raw[n++] = new SeedCandidate(t, SeedSource.ChartAnchor, SeedQuality.SeedOnly,
            branchId: 0, sourceIndex: 0, errorEstimate: 1.0, continuityCell: segment);
        if (hasPrediction)
        {
            raw[n++] = new SeedCandidate(t, SeedSource.NeighborPrediction, SeedQuality.Predicted,
                branchId: 0, sourceIndex: 1,
                errorEstimate: Length(Sub(predictedSeed, chordSeed)), continuityCell: segment);
        }
        raw[n++] = new SeedCandidate(t, SeedSource.BracketInterpolation, SeedQuality.SeedOnly,
            branchId: 0, sourceIndex: 2, errorEstimate: 2.0, continuityCell: segment);
        // Chart endpoints of the segment as additional anchors.
        raw[n++] = new SeedCandidate(view.ChartParameters[segment], SeedSource.ChartAnchor,
            SeedQuality.SeedOnly, branchId: 0, sourceIndex: 3, errorEstimate: 1.5, continuityCell: segment);

        return ICurveSeedSelection.OrderSeeds(raw[..n], requestedBranch: 0, ordered, out count, out failure);
    }

    /// <summary>
    /// Resolve a seed position for a ranked candidate against the chart /
    /// prediction inputs supplied by the caller.
    /// </summary>
    internal static KernelVector3 ResolveSeedPosition(in SeedCandidate candidate,
        in KernelVector3 chordSeed, in KernelVector3 predictedSeed,
        in KernelVector3 segmentStart, in KernelVector3 segmentEnd)
        => candidate.Source switch
        {
            SeedSource.NeighborPrediction => predictedSeed,
            SeedSource.ChartAnchor when candidate.SourceIndex == 3 => segmentStart,
            SeedSource.ChartAnchor when candidate.SourceIndex == 4 => segmentEnd,
            _ => chordSeed,
        };

    private static double Length(in KernelVector3 v) => Math.Sqrt(Dot(v, v));
    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
