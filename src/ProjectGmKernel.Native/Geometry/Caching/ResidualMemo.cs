using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Trial-scoped L0 residual memo (spec §13.3, task T09). Keys are plan +
/// parameter bits + segment + order + trial generation — never a publishable
/// <see cref="CurveSample"/>. Invalidated when the trial generation bumps
/// (reject / plan switch / <see cref="BeginTrial"/>).
/// </summary>
internal struct ResidualMemo
{
    internal const int Capacity = 8;
    internal const int ResidualWidth = 6;

    private int _generation;
    private int _count;
    private int _write;
    private InlineKeys _keys;
    private InlineResiduals _residuals;

    internal int Generation => _generation;
    internal int Count => _count;

    internal void BeginTrial()
    {
        _generation++;
        _count = 0;
        _write = 0;
    }

    internal void Invalidate()
    {
        _generation++;
        _count = 0;
        _write = 0;
    }

    internal bool TryFind(ICurveConstraintPlan plan, double t, BufferOffset segment,
        DerivativeOrder order, Span<double> residualOut)
    {
        var key = MakeKey(plan, t, segment, order, _generation);
        var limit = _count < Capacity ? _count : Capacity;
        for (BufferOffset i = 0; i < limit; i++)
        {
            if (!KeysEqual(_keys[i], key)) continue;
            var stored = ResidualsAt(i);
            if (residualOut.Length < 1) return false;
            var n = Math.Min(residualOut.Length, ResidualWidth);
            stored[..n].CopyTo(residualOut[..n]);
            return true;
        }
        return false;
    }

    internal bool TryInsert(ICurveConstraintPlan plan, double t, BufferOffset segment,
        DerivativeOrder order, ReadOnlySpan<double> residual)
    {
        if (residual.Length is < 1 or > ResidualWidth) return false;
        var index = _write;
        _keys[index] = MakeKey(plan, t, segment, order, _generation);
        WriteResidual(index, residual);
        _write = (_write + 1) % Capacity;
        if (_count < Capacity) _count++;
        return true;
    }

    private static MemoKey MakeKey(ICurveConstraintPlan plan, double t, BufferOffset segment,
        DerivativeOrder order, int generation)
        => new((byte)plan, segment, order, generation, BitConverter.DoubleToInt64Bits(t));

    private static bool KeysEqual(in MemoKey a, in MemoKey b)
        => a.Plan == b.Plan && a.Segment == b.Segment && a.Order == b.Order
            && a.Generation == b.Generation && a.ParameterBits == b.ParameterBits;

    private ReadOnlySpan<double> ResidualsAt(BufferOffset index) => _residuals.Row(index);

    private void WriteResidual(BufferOffset index, ReadOnlySpan<double> residual)
    {
        var row = _residuals.RowMutable(index);
        row.Clear();
        residual.CopyTo(row);
    }

    private readonly struct MemoKey
    {
        internal readonly byte Plan;
        internal readonly BufferOffset Segment;
        internal readonly DerivativeOrder Order;
        internal readonly int Generation;
        internal readonly long ParameterBits;

        internal MemoKey(byte plan, BufferOffset segment, DerivativeOrder order, int generation, long parameterBits)
        {
            Plan = plan;
            Segment = segment;
            Order = order;
            Generation = generation;
            ParameterBits = parameterBits;
        }
    }

    [System.Runtime.CompilerServices.InlineArray(Capacity)]
    private struct InlineKeys
    {
        private MemoKey _element0;
    }

    [System.Runtime.CompilerServices.InlineArray(Capacity * ResidualWidth)]
    private struct InlineResiduals
    {
        private double _element0;

        internal Span<double> RowMutable(BufferOffset index)
            => System.Runtime.InteropServices.MemoryMarshal.CreateSpan(
                ref System.Runtime.CompilerServices.Unsafe.Add(
                    ref _element0, index * ResidualWidth), ResidualWidth);

        internal ReadOnlySpan<double> Row(BufferOffset index) => RowMutable(index);
    }
}
