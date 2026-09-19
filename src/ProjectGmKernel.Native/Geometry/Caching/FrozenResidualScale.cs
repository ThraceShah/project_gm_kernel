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

    [System.Runtime.CompilerServices.InlineArray(MaxDim)]
    private struct InlineScales
    {
        private double _element0;
    }
}
