using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>Where a seed candidate came from; drives the deterministic order (§13.6).</summary>
internal enum SeedSource : byte
{
    ExactSample = 0,
    NeighborPrediction = 1,
    BracketInterpolation = 2,
    ChartAnchor = 3,
    ImportedUv = 4,
    LocalSearch = 5,
}

/// <summary>Quality class of a candidate; only Verified samples may ever be published as exact.</summary>
internal enum SeedQuality : byte
{
    Verified = 0,
    Predicted = 1,
    SeedOnly = 2,
}

/// <summary>One seed candidate on a branch of the icurve (parameters already unwrapped).</summary>
internal readonly struct SeedCandidate
{
    internal readonly double Parameter;
    internal readonly SeedSource Source;
    internal readonly SeedQuality Quality;
    internal readonly int BranchId;       // -1 = no branch evidence
    internal readonly int SourceIndex;    // stable tie-break key, never random
    internal readonly double ErrorEstimate;
    internal readonly int ContinuityCell; // -1 = unknown

    internal SeedCandidate(double parameter, SeedSource source, SeedQuality quality,
        int branchId, int sourceIndex, double errorEstimate, int continuityCell)
    {
        Parameter = parameter;
        Source = source;
        Quality = quality;
        BranchId = branchId;
        SourceIndex = sourceIndex;
        ErrorEstimate = errorEstimate;
        ContinuityCell = continuityCell;
    }
}

/// <summary>Located cause when seed ordering cannot produce a usable list.</summary>
internal enum SeedOrderFailure : byte
{
    None = 0,
    Empty,
    AmbiguousBranch,
}

/// <summary>
/// Deterministic seed ordering for one icurve request (spec §13.6, task T06).
/// Candidates are filtered by branch semantics, then ordered by source
/// priority and error — never by spatial proximity, which cannot distinguish
/// branches. Symmetric undistinguishable candidates across different branches
/// are reported as ambiguous instead of being broken randomly.
/// </summary>
internal static class ICurveSeedSelection
{
    /// <summary>Relative error gap below which two candidates count as indistinguishable.</summary>
    internal const double AmbiguityRelativeGap = 1e-9;

    /// <summary>
    /// Order candidates for a request limited to <paramref name="requestedBranch"/>
    /// (pass -1 when the request has no branch evidence yet). Output is a
    /// deterministic permutation of the accepted candidates. With no requested
    /// branch, candidates on different branches that tie in source, quality and
    /// error are ambiguous: the caller must resolve semantics before solving.
    /// </summary>
    internal static AlgorithmStatus OrderSeeds(ReadOnlySpan<SeedCandidate> candidates, int requestedBranch,
        Span<SeedCandidate> ordered, out BufferCount count, out SeedOrderFailure failure)
    {
        count = 0;
        failure = SeedOrderFailure.None;
        if (ordered.Length < candidates.Length) return AlgorithmStatus.WorkspaceTooSmall;

        // Semantic filter: branch first, then cell-less or matching-cell candidates stay.
        for (BufferOffset i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (requestedBranch >= 0 && candidate.BranchId >= 0 && candidate.BranchId != requestedBranch)
                continue;
            ordered[count++] = candidate;
        }
        if (count == 0)
        {
            failure = SeedOrderFailure.Empty;
            return AlgorithmStatus.NotConverged;
        }

        if (requestedBranch < 0 && HasBranchAmbiguity(ordered[..count], out failure))
            return AlgorithmStatus.NotConverged;

        InsertionSort(ordered[..count]);
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Two candidates on different branches with the same source, quality and
    /// indistinguishable error are symmetric: ordering would be an arbitrary
    /// choice, so the request reports ambiguity rather than guessing.
    /// </summary>
    private static bool HasBranchAmbiguity(ReadOnlySpan<SeedCandidate> candidates, out SeedOrderFailure failure)
    {
        failure = SeedOrderFailure.None;
        for (BufferOffset i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].BranchId < 0) continue;
            for (BufferOffset j = (BufferOffset)(i + 1); j < candidates.Length; j++)
            {
                if (candidates[j].BranchId < 0 || candidates[j].BranchId == candidates[i].BranchId) continue;
                if (candidates[i].Source != candidates[j].Source
                    || candidates[i].Quality != candidates[j].Quality) continue;
                var scale = Math.Max(1.0, Math.Max(Math.Abs(candidates[i].ErrorEstimate), Math.Abs(candidates[j].ErrorEstimate)));
                if (Math.Abs(candidates[i].ErrorEstimate - candidates[j].ErrorEstimate)
                    <= AmbiguityRelativeGap * scale)
                {
                    failure = SeedOrderFailure.AmbiguousBranch;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Insertion sort by (source priority, quality, error, source index) —
    /// stable and deterministic; candidate counts are small (spec §14.7 caps
    /// same-branch seeds at four plus anchors).
    /// </summary>
    private static void InsertionSort(Span<SeedCandidate> candidates)
    {
        for (BufferOffset i = 1; i < candidates.Length; i++)
        {
            var key = candidates[i];
            var j = i - 1;
            while (j >= 0 && RanksBefore(key, candidates[j]))
            {
                candidates[j + 1] = candidates[j];
                j--;
            }
            candidates[j + 1] = key;
        }
    }

    private static bool RanksBefore(in SeedCandidate a, in SeedCandidate b)
    {
        if (a.Source != b.Source) return a.Source < b.Source;
        if (a.Quality != b.Quality) return a.Quality < b.Quality;
        if (a.ErrorEstimate != b.ErrorEstimate) return a.ErrorEstimate < b.ErrorEstimate;
        return a.SourceIndex < b.SourceIndex;
    }
}
