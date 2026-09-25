using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>Located cause when INTERSECTION (node 38) decoding fails (spec §2.4, §5.2, task T02).</summary>
internal enum IcurveDecodeFailure : byte
{
    None = 0,
    BadChartCount,
    ChartHvecWrongLength,
    ChartHvecNotFinite,
    LimitHvecWrongLength,
    LimitHvecNotFinite,
    UnknownUvType,
    TruncatedUvValues,
    OversizedUvValues,
    NullUvValue,
    ExtendedChartUnsupported,
}

/// <summary>Decoded LIMIT payload: node 41 fields plus the limit's hull vectors.</summary>
internal ref struct IcurveLimitInput
{
    public LimitType Type;
    public LimitTermUse TermUse;
    public ReadOnlySpan<double> Hvecs; // 3 doubles per hull vector; 1 vector for H/L, 2 for T
}

/// <summary>
/// XT INTERSECTION decode input. Spans borrow the XT document field buffers;
/// nothing is copied until validation passes (spec §4.1: decode before store).
/// </summary>
internal ref struct IcurveDecodeInput
{
    public SurfTag Surface0Tag;
    public SurfTag Surface1Tag;
    public double BaseParameter;
    public double BaseScale;
    public double Scale;
    public KernelLogical ScaleProvided;
    public int ChartCount;
    public int ExtendedChartCount;                 // node 40 extended_chart_count, 0 when absent
    public ReadOnlySpan<double> ChartHvecs;        // ChartCount · 3 doubles
    public IcurveLimitInput Start;
    public IcurveLimitInput End;
    public IntersectionUvType UvType;              // node 204 uv_type
    public ReadOnlySpan<double> UvValues;          // node 204 values; null sentinel is NaN
    public double ChordalError;
    public double AngularError;
    public double ParameterError;
    public KernelLogical ParameterErrorProvided;
    public int SourceSchema;
    public KernelSense Sense;                      // source curve sense (XT node field 6)
}

/// <summary>
/// Decodes an XT INTERSECTION node (38) with its CHART (40), LIMIT (41) and
/// optional INTERSECTION_DATA (204) payloads into the icurve data pool.
/// Validation happens before any storage is written; a failure leaves the
/// pools untouched. UV layout follows the 2025 document (§2.4): total length
/// is (chart + terminator)·stride in the order start terminator, chart rows,
/// end terminator — the terminator branch point hvec is already part of the
/// limit's own pair and never counts again.
/// </summary>
internal static unsafe partial class KernelRuntime
{
    /// <summary>UV stride per hull vector for each node-204 uv_type; 0 means no UV data.</summary>
    internal static int UvStrideOf(IntersectionUvType uvType) => uvType switch
    {
        IntersectionUvType.None => 0,
        IntersectionUvType.First or IntersectionUvType.Second => 2,
        IntersectionUvType.Both => 4,
        _ => -1,
    };

    /// <summary>Expected node-204 values length: (chart + terminator) · stride (spec §2.4).</summary>
    internal static int ExpectedUvValueCount(IntersectionUvType uvType, int chartCount, int terminatorCount)
    {
        var stride = UvStrideOf(uvType);
        return stride < 0 ? -1 : (chartCount + terminatorCount) * stride;
    }

    /// <summary>Number of terminator limits among the branch ends (0, 1 or 2).</summary>
    internal static int TerminatorCount(IcurveLimitInput start, IcurveLimitInput end)
        => (start.Type == LimitType.Terminator ? 1 : 0) + (end.Type == LimitType.Terminator ? 1 : 0);

    /// <summary>Hull vectors carried by one limit: 2 for a terminator (position + branch), else 1.</summary>
    internal static int LimitHvecCount(IcurveLimitInput limit)
        => limit.Type == LimitType.Terminator ? 2 : 1;

    /// <summary>
    /// Validate and store one INTERSECTION node. On success the record and its
    /// payload blocks are alive in the pool; on failure nothing is left behind
    /// and <paramref name="failure"/> locates the cause.
    /// </summary>
    internal static AlgorithmStatus DecodeIcurve(IcurveDecodeInput input,
        out DataSlot dataIndex, out IcurveDecodeFailure failure, out BufferOffset failureIndex)
    {
        dataIndex = -1;
        failure = IcurveDecodeFailure.None;
        failureIndex = -1;

        if (input.ChartCount < 2) { failure = IcurveDecodeFailure.BadChartCount; return AlgorithmStatus.InvalidInput; }
        if (input.ExtendedChartCount != 0) { failure = IcurveDecodeFailure.ExtendedChartUnsupported; return AlgorithmStatus.Unsupported; }
        if (input.ChartHvecs.Length != input.ChartCount * 3)
        { failure = IcurveDecodeFailure.ChartHvecWrongLength; return AlgorithmStatus.InvalidInput; }
        for (BufferOffset i = 0; i < input.ChartHvecs.Length; i++)
            if (!double.IsFinite(input.ChartHvecs[i]))
            { failure = IcurveDecodeFailure.ChartHvecNotFinite; failureIndex = i; return AlgorithmStatus.InvalidInput; }

        if (!ValidateLimit(input.Start, out failure, out failureIndex)) return AlgorithmStatus.InvalidInput;
        if (!ValidateLimit(input.End, out failure, out failureIndex)) return AlgorithmStatus.InvalidInput;

        var terminatorCount = TerminatorCount(input.Start, input.End);
        var expectedUv = ExpectedUvValueCount(input.UvType, input.ChartCount, terminatorCount);
        if (expectedUv < 0) { failure = IcurveDecodeFailure.UnknownUvType; return AlgorithmStatus.InvalidInput; }
        if (input.UvValues.Length < expectedUv) { failure = IcurveDecodeFailure.TruncatedUvValues; return AlgorithmStatus.InvalidInput; }
        if (input.UvValues.Length > expectedUv) { failure = IcurveDecodeFailure.OversizedUvValues; return AlgorithmStatus.InvalidInput; }
        for (BufferOffset i = 0; i < input.UvValues.Length; i += 2)
        {
            var u = input.UvValues[i];
            var v = input.UvValues[i + 1];
            var uNull = double.IsNaN(u);
            var vNull = double.IsNaN(v);
            if (uNull != vNull)
            {
                failure = IcurveDecodeFailure.NullUvValue;
                failureIndex = uNull ? i : i + 1;
                return AlgorithmStatus.InvalidInput;
            }
            if (!uNull && (!double.IsFinite(u) || !double.IsFinite(v)))
            {
                failure = IcurveDecodeFailure.NullUvValue;
                failureIndex = !double.IsFinite(u) ? i : i + 1;
                return AlgorithmStatus.InvalidInput;
            }
        }

        // Storage: metadata slot, then hull-vector block (start + chart + end),
        // then the UV block. Every allocation undoes the previous one on failure.
        if (!ICurveDataPool.TryAllocate(out dataIndex))
            return AlgorithmStatus.WorkspaceTooSmall;
        var blocks = &State.Session->Blocks;
        var startVectors = LimitHvecCount(input.Start);
        var endVectors = LimitHvecCount(input.End);
        var hvecTotal = startVectors + input.ChartCount + endVectors;
        var hvecBlock = blocks->TryAllocate((nuint)(hvecTotal * 3 * sizeof(double)));
        var uvBlock = expectedUv > 0 ? blocks->TryAllocate((nuint)(expectedUv * sizeof(double))) : null;
        if (hvecBlock == null || (expectedUv > 0 && uvBlock == null))
        {
            if (hvecBlock != null) blocks->Free(hvecBlock);
            if (uvBlock != null) blocks->Free(uvBlock);
            ICurveDataPool.Free(dataIndex);
            dataIndex = -1;
            return AlgorithmStatus.WorkspaceTooSmall;
        }

        var header = ICurveDataPool[dataIndex].Header; // pool slot header survives the metadata write
        ref var data = ref ICurveDataPool[dataIndex];
        data = default;
        data.Header = header;
        data.Surface0Tag = input.Surface0Tag;
        data.Surface1Tag = input.Surface1Tag;
        data.BaseParameter = input.BaseParameter;
        data.BaseScale = input.BaseScale;
        data.ChartCount = input.ChartCount;
        data.HvecBlock = blocks->HandleOf(hvecBlock);
        data.HvecCount = hvecTotal;
        data.Scale = input.Scale;
        data.ScaleProvided = input.ScaleProvided;
        data.ChordalError = input.ChordalError;
        data.AngularError = input.AngularError;
        data.ParameterError = input.ParameterError;
        data.ParameterErrorProvided = input.ParameterErrorProvided;
        data.ExtendedChartCount = input.ExtendedChartCount;
        data.UvType = input.UvType;
        data.UvValueCount = expectedUv;
        data.UvValueBlock = expectedUv > 0 ? blocks->HandleOf(uvBlock) : -1;
        data.SourceSchema = input.SourceSchema;
        data.Sense = input.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c
            ? ParasolidConstants.PK_TOPOL_sense_negative_c
            : ParasolidConstants.PK_TOPOL_sense_positive_c;

        var stored = new Span<double>(hvecBlock, hvecTotal * 3);
        input.Start.Hvecs.CopyTo(stored[..(startVectors * 3)]);
        input.ChartHvecs.CopyTo(stored.Slice(startVectors * 3, input.ChartCount * 3));
        input.End.Hvecs.CopyTo(stored.Slice((startVectors + input.ChartCount) * 3, endVectors * 3));
        data.StartLimit = new LimitRecord
        {
            Type = input.Start.Type,
            TermUse = input.Start.TermUse,
            HvecIndex = 0,
            HvecCount = startVectors,
        };
        data.EndLimit = new LimitRecord
        {
            Type = input.End.Type,
            TermUse = input.End.TermUse,
            HvecIndex = startVectors + input.ChartCount,
            HvecCount = endVectors,
        };
        if (expectedUv > 0)
            input.UvValues.CopyTo(new Span<double>(uvBlock, expectedUv));
        return AlgorithmStatus.Success;
    }

    private static bool ValidateLimit(IcurveLimitInput limit,
        out IcurveDecodeFailure failure, out BufferOffset failureIndex)
    {
        failure = IcurveDecodeFailure.None;
        failureIndex = -1;
        var expected = LimitHvecCount(limit) * 3;
        if (limit.Hvecs.Length != expected)
        {
            failure = IcurveDecodeFailure.LimitHvecWrongLength;
            return false;
        }
        for (BufferOffset i = 0; i < limit.Hvecs.Length; i++)
            if (!double.IsFinite(limit.Hvecs[i]))
            {
                failure = IcurveDecodeFailure.LimitHvecNotFinite;
                failureIndex = i;
                return false;
            }
        return true;
    }
}
