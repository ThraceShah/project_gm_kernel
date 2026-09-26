using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Frozen row/column residual scaling for one accepted corrector state
/// (spec §14.1). Trial steps reuse the frozen scales; reject restores the
/// previous accepted freeze bit-exactly by not mutating it.
/// </summary>
internal struct FrozenResidualScale
{
    internal const int MaxDim = 6;

    private int _dimension;
    private InlineScales _row;
    private InlineScales _col;

    internal int Dimension => _dimension;

    internal void Capture(ReadOnlySpan<double> residual, ReadOnlySpan<double> jacobian, BufferCount n)
    {
        _dimension = n;
        for (BufferOffset i = 0; i < n; i++)
        {
            var rowMax = Math.Abs(residual[i]);
            for (BufferOffset j = 0; j < n; j++)
            {
                var a = Math.Abs(jacobian[i * n + j]);
                if (a > rowMax) rowMax = a;
            }
            _row[i] = rowMax > 0 ? 1.0 / rowMax : 1.0;
        }
        for (BufferOffset j = 0; j < n; j++)
        {
            var colMax = 0.0;
            for (BufferOffset i = 0; i < n; i++)
            {
                var a = Math.Abs(jacobian[i * n + j]) * _row[i];
                if (a > colMax) colMax = a;
            }
            _col[j] = colMax > 0 ? 1.0 / colMax : 1.0;
        }
    }

    internal void Apply(Span<double> residual, Span<double> jacobian)
    {
        var n = _dimension;
        for (BufferOffset i = 0; i < n; i++)
        {
            residual[i] *= _row[i];
            for (BufferOffset j = 0; j < n; j++)
                jacobian[i * n + j] *= _row[i] * _col[j];
        }
    }

    /// <summary>
    /// Map a scaled-space step p (the coordinates <c>DoglegStep</c> solves in,
    /// with <c>J_s = W_F·J·D_u</c>) to the physical state increment
    /// Δy = D_u·p (§14.1). The scaled step itself keeps feeding ‖p‖, the
    /// trust-region boundary test and the model predicted reduction — only
    /// the state update uses the mapped vector. Returns false when a mapped
    /// component is non-finite.
    /// </summary>
    internal readonly bool TryMapScaledStep(ReadOnlySpan<double> scaledStep, Span<double> physicalStep, BufferCount n)
    {
        if (scaledStep.Length < n || physicalStep.Length < n) return false;
        for (BufferOffset j = 0; j < n; j++)
        {
            var mapped = scaledStep[j] * _col[j];
            if (!double.IsFinite(mapped)) return false;
            physicalStep[j] = mapped;
        }
        return true;
    }

    /// <summary>
    /// Largest column scale: the physical extent of a scaled-space step is
    /// bounded by ‖p‖·max_j D_u[j]. Used to convert a physical length budget
    /// into the scaled metric at start-up.
    /// </summary>
    internal readonly double MaxColumnScale
    {
        get
        {
            var max = 0.0;
            for (BufferOffset j = 0; j < _dimension; j++)
            {
                if (_col[j] > max) max = _col[j];
            }
            return max;
        }
    }

    /// <summary>
    /// Conservative conversion factor for the scaled-space trust radius after
    /// a re-capture: the physical extent of axis j is radius·_col[j], so
    /// scaling the radius by min_j(previous._col[j]/_col[j]) never enlarges
    /// any physical axis when the column metric changes (§14.1: scales are
    /// rebuilt after accepted steps — the radius must move with them, not be
    /// silently reused across metrics).
    /// </summary>
    internal readonly double ConservativeRadiusFactor(in FrozenResidualScale previous)
    {
        var factor = 1.0;
        for (BufferOffset j = 0; j < _dimension; j++)
        {
            if (_col[j] > 0 && previous._col[j] > 0)
            {
                var ratio = previous._col[j] / _col[j];
                if (ratio < factor) factor = ratio;
            }
        }
        return factor > 0 && double.IsFinite(factor) ? factor : 1.0;
    }

    [System.Runtime.CompilerServices.InlineArray(MaxDim)]
    private struct InlineScales
    {
        private double _element0;
    }
}
