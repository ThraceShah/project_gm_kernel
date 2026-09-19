using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>
/// Continuity-cell / difficulty typing for seed and step limits (spec §17.1–17.2).
/// Seam cells do not cross-average; near-tangent metrics report Singular rather
/// than AmbiguousBranch.
/// </summary>
internal enum ContinuityDifficulty : byte
{
    Regular = 0,
    MetricNearTangent = 1,
    AngleSensitive = 2,
    EliminationD = 3,
    InnerNoiseDominated = 4,
    SeamCell = 5,
}

internal static class ContinuityCellRules
{
    /// <summary>Original chart segment index is the continuity cell id.</summary>
    internal static int CellOfSegment(BufferOffset segment) => segment;

    /// <summary>
    /// Classify from support normals and elimination D. Near-parallel gradients
    /// (metric near-tangent) are Singular territory, not AmbiguousBranch.
    /// </summary>
    internal static ContinuityDifficulty Classify(in KernelVector3 gradient0, in KernelVector3 gradient1,
        double eliminationRatio, bool atSeam)
    {
        if (atSeam) return ContinuityDifficulty.SeamCell;
        var g0 = Math.Sqrt(Dot(gradient0, gradient0));
        var g1 = Math.Sqrt(Dot(gradient1, gradient1));
        if (g0 > 0 && g1 > 0)
        {
            var cos = Math.Abs(Dot(gradient0, gradient1) / (g0 * g1));
            if (cos > 1.0 - 1e-8) return ContinuityDifficulty.MetricNearTangent;
            if (cos > 0.985) return ContinuityDifficulty.AngleSensitive;
        }
        if (eliminationRatio >= 0 && eliminationRatio < BlendImplicitEvaluation.MinEliminationRatio)
            return ContinuityDifficulty.EliminationD;
        return ContinuityDifficulty.Regular;
    }

    internal static AlgorithmStatus StatusFor(ContinuityDifficulty difficulty)
        => difficulty is ContinuityDifficulty.MetricNearTangent or ContinuityDifficulty.EliminationD
            ? AlgorithmStatus.Singular
            : AlgorithmStatus.Success;

    private static double Dot(in KernelVector3 a, in KernelVector3 b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}
