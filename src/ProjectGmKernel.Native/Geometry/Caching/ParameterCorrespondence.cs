using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Parameter correspondence math for cached samples (spec §13.5, task T06):
/// periodic unwrapping that keeps an explicit integer lift, and Hermite
/// prediction between two same-cell samples. Predictions are seeds — they are
/// never published as exact results, and never averaged across a seam, branch
/// or low-continuity boundary.
/// </summary>
internal static class ParameterCorrespondence
{
    /// <summary>
    /// Unwrap <paramref name="value"/> onto the representative nearest to
    /// <paramref name="reference"/> on the period lattice, reporting the
    /// applied lift so callers can preserve the sheet identity. The lift is
    /// the integer number of periods added to the raw value; averaging across
    /// a 2π−ε/ε seam without this would fold to the wrong side.
    /// </summary>
    internal static double UnwrapWithLift(double value, double reference, double period, out long lift)
    {
        lift = 0;
        if (!double.IsFinite(value) || !double.IsFinite(reference) || !(period > 0))
            return value;
        var offset = (reference - value) / period;
        if (!double.IsFinite(offset))
            return value;
        lift = (long)Math.Round(offset);
        return value + lift * period;
    }

    /// <summary>Unwrap without reporting the lift.</summary>
    internal static double Unwrap(double value, double reference, double period)
        => UnwrapWithLift(value, reference, period, out _);

    /// <summary>
    /// Cubic Hermite prediction at <paramref name="t"/> from two same-cell
    /// samples (tₐ, yₐ, y′ₐ) and (t_b, y_b, y′_b): h₀₀yₐ + h₁₀Δt·y′ₐ +
    /// h₀₁y_b + h₁₁Δt·y′_b with s = (t−tₐ)/(t_b−tₐ). The arrays hold the
    /// state vectors (positions, auxiliary unknowns); every component uses the
    /// same scalar basis. Parameters must already be unwrapped onto one sheet.
    /// </summary>
    internal static AlgorithmStatus TryHermitePredict(double ta, ReadOnlySpan<double> ya,
        ReadOnlySpan<double> da, double tb, ReadOnlySpan<double> yb, ReadOnlySpan<double> db,
        double t, Span<double> predicted)
    {
        if (ya.Length != yb.Length || ya.Length != da.Length || ya.Length != db.Length
            || predicted.Length < ya.Length)
            return AlgorithmStatus.WorkspaceTooSmall;
        var delta = tb - ta;
        if (!(delta != 0) || !double.IsFinite(delta)) return AlgorithmStatus.InvalidInput;
        var s = (t - ta) / delta;
        if (!double.IsFinite(s)) return AlgorithmStatus.InvalidInput;
        var s2 = s * s;
        var s3 = s2 * s;
        var h00 = 2 * s3 - 3 * s2 + 1;
        var h10 = s3 - 2 * s2 + s;
        var h01 = -2 * s3 + 3 * s2;
        var h11 = s3 - s2;
        for (BufferOffset i = 0; i < ya.Length; i++)
        {
            predicted[i] = h00 * ya[i] + h10 * delta * da[i] + h01 * yb[i] + h11 * delta * db[i];
            if (!double.IsFinite(predicted[i])) return AlgorithmStatus.NumericalFailure;
        }
        return AlgorithmStatus.Success;
    }
}
