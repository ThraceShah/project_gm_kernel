using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Metric sample from an ICurve evaluation comparison against reference Oracle (spec §21.6 / Case F).
/// </summary>
internal readonly struct CaseFEvalSample
{
    internal readonly double DiffD0;
    internal readonly double DiffD1;
    internal readonly double DiffRawD2;
    internal readonly double GeomD2;
    internal readonly double CircleDelta;

    internal CaseFEvalSample(double diffD0, double diffD1, double diffRawD2, double geomD2, double circleDelta)
    {
        DiffD0 = diffD0;
        DiffD1 = diffD1;
        DiffRawD2 = diffRawD2;
        GeomD2 = geomD2;
        CircleDelta = circleDelta;
    }
}

/// <summary>
/// Shared validator for Case F evaluation metrics (spec §21.6). Rejects non-finite (NaN, Inf),
/// negative, and out-of-tolerance metric values.
/// </summary>
internal static class CaseFValidator
{
    internal static bool Validate(ReadOnlySpan<CaseFEvalSample> samples,
        double maxPosTol, double maxD1Tol, double maxRawD2Tol, double maxGeomD2Tol, double maxCircleTol,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!double.IsFinite(maxPosTol) || maxPosTol < 0 ||
            !double.IsFinite(maxD1Tol) || maxD1Tol < 0 ||
            !double.IsFinite(maxRawD2Tol) || maxRawD2Tol < 0 ||
            !double.IsFinite(maxGeomD2Tol) || maxGeomD2Tol < 0 ||
            !double.IsFinite(maxCircleTol) || maxCircleTol < 0)
        {
            failureReason = "Invalid tolerance parameter in CaseFValidator: non-finite or negative.";
            return false;
        }

        if (samples.Length == 0)
        {
            failureReason = "Empty sample list in CaseFValidator.";
            return false;
        }

        for (BufferOffset i = 0; i < samples.Length; i++)
        {
            ref readonly var s = ref samples[i];
            if (!double.IsFinite(s.DiffD0) || s.DiffD0 < 0 || s.DiffD0 > maxPosTol)
            {
                failureReason = $"D0 position tolerance exceeded or non-finite at sample {i}: diff={s.DiffD0:E3} > tol={maxPosTol:E3}";
                return false;
            }
            if (!double.IsFinite(s.DiffD1) || s.DiffD1 < 0 || s.DiffD1 > maxD1Tol)
            {
                failureReason = $"D1 tangent tolerance exceeded or non-finite at sample {i}: diff={s.DiffD1:E3} > tol={maxD1Tol:E3}";
                return false;
            }
            if (!double.IsFinite(s.DiffRawD2) || s.DiffRawD2 < 0 || s.DiffRawD2 > maxRawD2Tol)
            {
                failureReason = $"Raw D2 vector tolerance exceeded or non-finite at sample {i}: diff={s.DiffRawD2:E3} > tol={maxRawD2Tol:E3}";
                return false;
            }
            if (!double.IsFinite(s.GeomD2) || s.GeomD2 < 0 || s.GeomD2 > maxGeomD2Tol)
            {
                failureReason = $"Geometric D2 (curvature/normal) tolerance exceeded or non-finite at sample {i}: diff={s.GeomD2:E3} > tol={maxGeomD2Tol:E3}";
                return false;
            }
            if (!double.IsFinite(s.CircleDelta) || s.CircleDelta < 0 || s.CircleDelta > maxCircleTol)
            {
                failureReason = $"Circle support deviation exceeded or non-finite at sample {i}: diff={s.CircleDelta:E3} > tol={maxCircleTol:E3}";
                return false;
            }
        }
        return true;
    }
}
