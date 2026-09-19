using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Caller-owned failure-replay ring (spec §22.4 / T17). Captures compact
/// (plan, t, segment, status, residual) frames without I/O inside residuals.
/// Dump is explicit via <see cref="TryCopy"/>.
/// </summary>
internal struct FailureReplayRing
{
    internal const int Capacity = 16;

    private int _count;
    private int _write;
    private InlineFrames _frames;

    internal int Count => _count;

    internal void Clear()
    {
        _count = 0;
        _write = 0;
    }

    internal void Record(ICurveConstraintPlan plan, double t, BufferOffset segment,
        AlgorithmStatus status, double residual, ICurveEvalDetail detail)
    {
        _frames[_write] = new Frame((byte)plan, segment, status, detail,
            BitConverter.DoubleToInt64Bits(t), residual);
        _write = (_write + 1) % Capacity;
        if (_count < Capacity) _count++;
    }

    internal bool TryCopy(Span<Frame> destination, out BufferCount copied)
    {
        copied = 0;
        if (destination.Length < _count) return false;
        // Oldest-first: when not full, indices 0..count-1; when full, from write.
        if (_count < Capacity)
        {
            for (BufferOffset i = 0; i < _count; i++)
                destination[copied++] = _frames[i];
        }
        else
        {
            for (BufferOffset i = 0; i < Capacity; i++)
                destination[copied++] = _frames[(_write + i) % Capacity];
        }
        return true;
    }

    internal readonly struct Frame
    {
        internal readonly byte Plan;
        internal readonly BufferOffset Segment;
        internal readonly AlgorithmStatus Status;
        internal readonly ICurveEvalDetail Detail;
        internal readonly long ParameterBits;
        internal readonly double Residual;

        internal Frame(byte plan, BufferOffset segment, AlgorithmStatus status,
            ICurveEvalDetail detail, long parameterBits, double residual)
        {
            Plan = plan;
            Segment = segment;
            Status = status;
            Detail = detail;
            ParameterBits = parameterBits;
            Residual = residual;
        }

        internal double Parameter => BitConverter.Int64BitsToDouble(ParameterBits);
        internal ICurveConstraintPlan ConstraintPlan => (ICurveConstraintPlan)Plan;
    }

    [System.Runtime.CompilerServices.InlineArray(Capacity)]
    private struct InlineFrames
    {
        private Frame _element0;
    }
}
