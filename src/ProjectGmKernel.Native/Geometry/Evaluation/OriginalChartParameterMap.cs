using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Which original segment an exact chart node belongs to for derivative purposes.</summary>
internal enum ChartSide : byte
{
    Left = 0,
    Right = 1,
}

/// <summary>
/// Located cause when chart preparation fails. Published inside the eventual
/// <c>ICurveEvalReport</c> (spec §18.5); never folded into a generic failure.
/// </summary>
internal enum ChartBuildFailure : byte
{
    None = 0,
    NonFiniteInput,
    BadBaseScale,
    MisalignedTangent,
    ZeroLengthChord,
    ScaleOverflow,
    ParameterResolutionLost,
}

/// <summary>
/// Deterministic rebuild of the original chart parameterization (spec §5):
/// normalized-cosine scale recursion, chord-projection parameter plane and
/// the native-parameter inverse ψᵢ. The chart is immutable once built —
/// adaptive subdivision must reuse, never rebuild, these anchors (§5.6).
/// Unit tangent Tᵢ comes from the sense-corrected cross product of the two
/// support normals (§5.1); this module never constructs it from |dot|.
/// </summary>
internal static class OriginalChartParameterMap
{
    /// <summary>Cosines below this are treated as misaligned, not clipped.</summary>
    internal const double MinCosine = 1e-12;

    /// <summary>
    /// Compute tᵢ, fᵢ (one per segment) and unit chords eᵢ from the original
    /// chart points and their unit tangents. f₀ = base_scale,
    /// fᵢ = fᵢ₋₁·(Tᵢ·eᵢ₋₁)/(Tᵢ·eᵢ), tᵢ₊₁ = tᵢ + Cᵢ·fᵢ. The last point gets no
    /// scale — the next chord does not exist and is never read (§5.1).
    /// </summary>
    internal static AlgorithmStatus Build(ReadOnlySpan<KernelVector3> positions,
        ReadOnlySpan<KernelVector3> tangents, double baseParameter, double baseScale,
        Span<double> parameters, Span<double> scales, Span<KernelVector3> chordUnits,
        out BufferCount segmentCount, out ChartBuildFailure failure)
    {
        segmentCount = 0;
        failure = ChartBuildFailure.None;
        var pointCount = positions.Length;
        if (pointCount < 2 || tangents.Length != pointCount
            || parameters.Length < pointCount || scales.Length < pointCount - 1
            || chordUnits.Length < pointCount - 1)
            return AlgorithmStatus.WorkspaceTooSmall;
        if (!double.IsFinite(baseParameter) || !double.IsFinite(baseScale) || !(baseScale > 0))
        {
            failure = ChartBuildFailure.BadBaseScale;
            return AlgorithmStatus.InvalidInput;
        }
        for (BufferOffset i = 0; i < pointCount; i++)
            if (!IsFinite(positions[i]) || !IsFinite(tangents[i])
                || Math.Abs(Dot(tangents[i], tangents[i]) - 1) > 1e-9)
            {
                failure = ChartBuildFailure.NonFiniteInput;
                return AlgorithmStatus.InvalidInput;
            }

        parameters[0] = baseParameter;
        var scale = baseScale;
        for (BufferOffset i = 0; i < pointCount - 1; i++)
        {
            var chord = Sub(positions[i + 1], positions[i]);
            var chordLength = Math.Sqrt(Dot(chord, chord));
            if (!(chordLength > 0) || !double.IsFinite(chordLength))
            {
                failure = ChartBuildFailure.ZeroLengthChord;
                return AlgorithmStatus.InvalidInput;
            }
            var e = Scale(chord, 1 / chordLength);
            chordUnits[i] = e;

            // The recursion cosine Tᵢ·eᵢ must be positive; a misaligned or
            // degenerate tangent is a diagnosable input problem, not
            // something to hide behind Abs(dot) (§5.2).
            var forwardCosine = Dot(tangents[i], e);
            if (!(forwardCosine > MinCosine))
            {
                failure = ChartBuildFailure.MisalignedTangent;
                return AlgorithmStatus.InvalidInput;
            }
            if (i > 0)
            {
                var backwardCosine = Dot(tangents[i], chordUnits[i - 1]);
                if (!(backwardCosine > MinCosine))
                {
                    failure = ChartBuildFailure.MisalignedTangent;
                    return AlgorithmStatus.InvalidInput;
                }
                scale = scale * backwardCosine / forwardCosine;
                if (!double.IsFinite(scale) || !(scale > 0))
                {
                    failure = ChartBuildFailure.ScaleOverflow;
                    return AlgorithmStatus.NumericalFailure;
                }
            }
            scales[i] = scale;

            var nextParameter = parameters[i] + chordLength * scale;
            if (!double.IsFinite(nextParameter) || !(nextParameter > parameters[i]))
            {
                failure = ChartBuildFailure.ParameterResolutionLost;
                return AlgorithmStatus.NumericalFailure;
            }
            parameters[i + 1] = nextParameter;
        }
        segmentCount = pointCount - 1;
        return AlgorithmStatus.Success;
    }

    /// <summary>
    /// Segment containing t. At an exact interior node <paramref name="side"/>
    /// selects the left or right original segment; derivatives on the two
    /// sides legitimately differ (§5.5) and are never averaged.
    /// </summary>
    internal static AlgorithmStatus LocateSegment(ReadOnlySpan<double> parameters, double t,
        ChartSide side, out BufferOffset segment)
    {
        segment = -1;
        if (!double.IsFinite(t)) return AlgorithmStatus.InvalidInput;
        if (t < parameters[0] || t > parameters[^1]) return AlgorithmStatus.InvalidInput;
        // Exact node: bit-comparable query against the rebuilt parameters.
        for (BufferOffset i = 0; i < parameters.Length; i++)
        {
            if (t != parameters[i]) continue;
            if (i == parameters.Length - 1) segment = parameters.Length - 2;
            else segment = side == ChartSide.Left ? Math.Max(0, i - 1) : i;
            return AlgorithmStatus.Success;
        }
        for (BufferOffset i = 0; i < parameters.Length - 1; i++)
            if (parameters[i] < t && t < parameters[i + 1])
            {
                segment = i;
                return AlgorithmStatus.Success;
            }
        return AlgorithmStatus.InvalidInput;
    }

    /// <summary>True when t is bit-identical to a rebuilt chart parameter (ChartPoint rule, §5.4).</summary>
    internal static bool IsChartNode(ReadOnlySpan<double> parameters, double t)
    {
        for (BufferOffset i = 0; i < parameters.Length; i++)
            if (t == parameters[i]) return true;
        return false;
    }

    /// <summary>Chord-plane residual p(x,t) = e·(x − Pᵢ) − (t − tᵢ)/fᵢ with anchored difference (§5.3).</summary>
    internal static double PlaneResidual(ReadOnlySpan<KernelVector3> positions,
        ReadOnlySpan<double> parameters, ReadOnlySpan<double> scales,
        ReadOnlySpan<KernelVector3> chordUnits, BufferOffset segment, double t, in KernelVector3 point)
    {
        var anchored = t - parameters[segment];
        var chord = chordUnits[segment];
        var anchor = positions[segment];
        var projection = Math.FusedMultiplyAdd(chord.X, point.X - anchor.X,
            Math.FusedMultiplyAdd(chord.Y, point.Y - anchor.Y,
                chord.Z * (point.Z - anchor.Z)));
        return projection - anchored / scales[segment];
    }

    /// <summary>Native-parameter inverse ψᵢ(x) = tᵢ + fᵢ·e·(x − Pᵢ) (§5.3).</summary>
    internal static double InverseParameter(ReadOnlySpan<KernelVector3> positions,
        ReadOnlySpan<double> parameters, ReadOnlySpan<double> scales,
        ReadOnlySpan<KernelVector3> chordUnits, BufferOffset segment, in KernelVector3 point)
        => parameters[segment] + scales[segment] * Dot(chordUnits[segment], Sub(point, positions[segment]));

    /// <summary>D1 = T/(f·(e·T)) from the curve tangent at the query point (§5.5).</summary>
    internal static AlgorithmStatus TryParameterDerivative(double scale, in KernelVector3 chordUnit,
        in KernelVector3 tangentDirection, out KernelVector3 derivative)
    {
        derivative = default;
        var cosine = Dot(chordUnit, tangentDirection);
        if (!(cosine > MinCosine)) return AlgorithmStatus.Singular;
        derivative = Scale(tangentDirection, 1 / (scale * cosine));
        return IsFinite(derivative) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    /// <summary>
    /// D2 = [T′·(e·T) − T·(e·T′)]/(f·(e·T)²) given dT/dt along the same
    /// regular branch. Node sides differ through their own e, f; callers pass
    /// the side-consistent data and never average (§5.5).
    /// </summary>
    internal static AlgorithmStatus TryParameterSecondDerivative(double scale, in KernelVector3 chordUnit,
        in KernelVector3 tangentDirection, in KernelVector3 tangentDerivative, out KernelVector3 secondDerivative)
    {
        secondDerivative = default;
        var cosine = Dot(chordUnit, tangentDirection);
        if (!(cosine > MinCosine)) return AlgorithmStatus.Singular;
        var numerator = Sub(Scale(tangentDerivative, cosine),
            Scale(tangentDirection, Dot(chordUnit, tangentDerivative)));
        secondDerivative = Scale(numerator, 1 / (scale * cosine * cosine));
        return IsFinite(secondDerivative) ? AlgorithmStatus.Success : AlgorithmStatus.NumericalFailure;
    }

    private static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
