using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Caching;

/// <summary>
/// Shared-spine L3 bookkeeping (spec §13.7, task T10): spine-keyed entries
/// carry parent range/sense so two blend parents can share the spine sample
/// without merging distinct arc/contact side state.
/// </summary>
internal struct SharedSpineCacheKey
{
    internal readonly int SpineNodeId;
    internal readonly long SpineParameterBits;
    internal readonly int ParentNodeId;
    internal readonly KernelSense ParentSense;
    internal readonly long ParentRangeBits;

    internal SharedSpineCacheKey(int spineNodeId, double spineParameter,
        int parentNodeId, KernelSense parentSense, double parentRange)
    {
        SpineNodeId = spineNodeId;
        SpineParameterBits = BitConverter.DoubleToInt64Bits(spineParameter);
        ParentNodeId = parentNodeId;
        ParentSense = parentSense;
        ParentRangeBits = BitConverter.DoubleToInt64Bits(parentRange);
    }

    /// <summary>True when the spine sample may be shared across parents.</summary>
    internal readonly bool SameSpine(in SharedSpineCacheKey other)
        => SpineNodeId == other.SpineNodeId && SpineParameterBits == other.SpineParameterBits;

    /// <summary>True when parent-local state must stay isolated.</summary>
    internal readonly bool SameParentContext(in SharedSpineCacheKey other)
        => ParentNodeId == other.ParentNodeId && ParentSense == other.ParentSense
            && ParentRangeBits == other.ParentRangeBits;
}

/// <summary>
/// Tiny caller-owned shared-spine memo (not the session L3 arena). Two parents
/// alternating eval can hit the same spine slot while keeping distinct
/// parent keys for arc/contact.
/// </summary>
internal struct SharedSpineMemo
{
    internal const int Capacity = 8;

    private int _count;
    private InlineKeys _keys;
    private InlinePoints _points;

    internal int Count => _count;

    internal bool TryFindSpine(in SharedSpineCacheKey key, out KernelVector3 spinePoint)
    {
        spinePoint = default;
        for (BufferOffset i = 0; i < _count; i++)
        {
            if (!_keys[i].SameSpine(in key)) continue;
            spinePoint = _points[i];
            return true;
        }
        return false;
    }

    internal bool TryInsert(in SharedSpineCacheKey key, in KernelVector3 spinePoint)
    {
        // Parent-local isolation: identical spine+parent overwrites; different
        // parent with same spine still stores a separate row keyed fully.
        for (BufferOffset i = 0; i < _count; i++)
        {
            if (_keys[i].SameSpine(in key) && _keys[i].SameParentContext(in key))
            {
                _points[i] = spinePoint;
                return true;
            }
        }
        if (_count >= Capacity) return false;
        _keys[_count] = key;
        _points[_count] = spinePoint;
        _count++;
        return true;
    }

    [System.Runtime.CompilerServices.InlineArray(Capacity)]
    private struct InlineKeys
    {
        private SharedSpineCacheKey _element0;
    }

    [System.Runtime.CompilerServices.InlineArray(Capacity)]
    private struct InlinePoints
    {
        private KernelVector3 _element0;
    }
}
