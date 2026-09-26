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

    /// <summary>
    /// Computes the central finite difference second derivative from D1 probes at t+h and t-h,
    /// and calculates the Euclidean error norm against the candidate D2 vector.
    /// Rejects non-finite inputs or non-positive step sizes.
    /// </summary>
    internal static bool TryComputeFiniteDifferenceD2(
        (double x, double y, double z) d1Plus,
        (double x, double y, double z) d1Minus,
        (double x, double y, double z) d2,
        double h,
        out (double x, double y, double z) fdD2,
        out double error,
        out string failureReason)
    {
        fdD2 = default;
        error = double.NaN;
        failureReason = string.Empty;

        if (!double.IsFinite(h) || h <= 0)
        {
            failureReason = "Invalid step size h in finite difference: non-finite or non-positive.";
            return false;
        }

        if (!double.IsFinite(d1Plus.x) || !double.IsFinite(d1Plus.y) || !double.IsFinite(d1Plus.z) ||
            !double.IsFinite(d1Minus.x) || !double.IsFinite(d1Minus.y) || !double.IsFinite(d1Minus.z) ||
            !double.IsFinite(d2.x) || !double.IsFinite(d2.y) || !double.IsFinite(d2.z))
        {
            failureReason = "Non-finite coordinate encountered in finite difference probe vectors.";
            return false;
        }

        var fdX = (d1Plus.x - d1Minus.x) / (2.0 * h);
        var fdY = (d1Plus.y - d1Minus.y) / (2.0 * h);
        var fdZ = (d1Plus.z - d1Minus.z) / (2.0 * h);
        fdD2 = (fdX, fdY, fdZ);

        var errX = fdX - d2.x;
        var errY = fdY - d2.y;
        var errZ = fdZ - d2.z;

        error = Math.Sqrt(errX * errX + errY * errY + errZ * errZ);
        if (!double.IsFinite(error))
        {
            failureReason = "Finite-difference error norm computed as non-finite.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that the candidate D2 vector matches central finite difference of D1 within tolerance.
    /// Rejects non-finite inputs, invalid tolerance, or error exceeding tolerance.
    /// </summary>
    internal static bool ValidateFiniteDifferenceD2(
        (double x, double y, double z) d1Plus,
        (double x, double y, double z) d1Minus,
        (double x, double y, double z) d2,
        double h,
        double tolerance,
        out (double x, double y, double z) fdD2,
        out double error,
        out string failureReason)
    {
        if (!TryComputeFiniteDifferenceD2(d1Plus, d1Minus, d2, h, out fdD2, out error, out failureReason))
        {
            return false;
        }

        if (!double.IsFinite(tolerance) || tolerance < 0)
        {
            failureReason = "Invalid tolerance parameter in ValidateFiniteDifferenceD2: non-finite or negative.";
            return false;
        }

        if (error > tolerance)
        {
            failureReason = $"Finite-difference D2 error exceeded tolerance: error={error:E3} > tol={tolerance:E3}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Overload of ValidateFiniteDifferenceD2 that does not output the computed fdD2 vector.
    /// </summary>
    internal static bool ValidateFiniteDifferenceD2(
        (double x, double y, double z) d1Plus,
        (double x, double y, double z) d1Minus,
        (double x, double y, double z) d2,
        double h,
        double tolerance,
        out double error,
        out string failureReason)
        => ValidateFiniteDifferenceD2(d1Plus, d1Minus, d2, h, tolerance, out _, out error, out failureReason);
}

