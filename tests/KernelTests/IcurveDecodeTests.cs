using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// XT INTERSECTION / INTERSECTION_DATA decode tests (spec §2.4, task T02).
/// Size expectations come from reference_vectors.json RV-UV; the error cases
/// cover null pairs (XT null-real sentinel NaN), truncation, oversize and
/// unknown enums. Layout is verified against the document order: start
/// terminator hvec, chart rows, end terminator hvec.
/// </summary>
public unsafe class IcurveDecodeTests
{
    public IcurveDecodeTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Theory]
    [InlineData(1, 4, 0, 0)]   // none: 0 values regardless of terminators
    [InlineData(1, 4, 1, 0)]
    [InlineData(1, 4, 2, 0)]
    [InlineData(2, 4, 0, 8)]   // first: 2 per hull vector
    [InlineData(2, 4, 1, 10)]
    [InlineData(2, 4, 2, 12)]
    [InlineData(3, 4, 0, 8)]   // second
    [InlineData(3, 4, 1, 10)]
    [InlineData(3, 4, 2, 12)]
    [InlineData(4, 4, 0, 16)]  // both: 4 per hull vector
    [InlineData(4, 4, 1, 20)]
    [InlineData(4, 4, 2, 24)]
    public void ExpectedUvValueCount_MatchesReferenceSizeCases(int uvType, int chartCount, int terminators, int expected)
    {
        Assert.Equal(expected, KernelRuntime.ExpectedUvValueCount((IntersectionUvType)uvType, chartCount, terminators));
    }

    private static IcurveDecodeInput ValidInput(IntersectionUvType uvType, double[] uvValues)
    {
        // 4 chart points on the unit circle (RV-CHART geometry), help limits.
        var chart = new[]
        {
            1.0, 0.0, 0.0,
            0.9855847669095608, 0.16918234906699603, 0.0,
            0.8138784566625339, 0.5810351605373051, 0.0,
            0.5148188449699553, 0.8572989891886034, 0.0,
        };
        return new IcurveDecodeInput
        {
            Surface0Tag = 101,
            Surface1Tag = 202,
            BaseParameter = -2.0,
            BaseScale = 1.7,
            ScaleProvided = 0,
            ChartCount = 4,
            ChartHvecs = chart,
            Start = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = [0.9, 0.4, 0.0] },
            End = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = [0.5, 0.85, 0.0] },
            UvType = uvType,
            UvValues = uvValues,
            ChordalError = 1e-3,
            AngularError = 1e-4,
            ParameterErrorProvided = 0,
            SourceSchema = 37102,
        };
    }

    [Fact]
    public void Decode_NoneUvType_StoresAnchorsWithoutUvBlock()
    {
        var input = ValidInput(IntersectionUvType.None, []);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var slot, out var failure, out _));
        Assert.Equal(IcurveDecodeFailure.None, failure);

        ref readonly var data = ref KernelRuntime.ICurveDataPool[slot];
        Assert.Equal(101, data.Surface0Tag);
        Assert.Equal(202, data.Surface1Tag);
        Assert.Equal(4, data.ChartCount);
        Assert.Equal(-2.0, data.BaseParameter, 12);
        Assert.Equal(1.7, data.BaseScale, 12);
        Assert.Equal(6, data.HvecCount); // start + 4 chart + end
        Assert.Equal(-1, data.UvValueBlock);
        Assert.Equal(0, data.UvValueCount);
        Assert.Equal(37102, data.SourceSchema);

        // Block layout: start limit, chart points, end limit.
        var stored = new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(data.HvecBlock), data.HvecCount * 3);
        Assert.Equal(0.9, stored[0], 12);
        Assert.Equal(1.0, stored[3], 12);
        Assert.Equal(0.5148188449699553, stored[12], 12);
        Assert.Equal(0.5, stored[15], 12);

        // Limits point into the shared block, never at a separate arena.
        Assert.Equal(0, data.StartLimit.HvecIndex);
        Assert.Equal(1, data.StartLimit.HvecCount);
        Assert.Equal(5, data.EndLimit.HvecIndex);
        Assert.Equal(1, data.EndLimit.HvecCount);

        KernelRuntime.FreeICurveData(slot);
    }

    [Fact]
    public void Decode_BothUvTypeWithTwoTerminators_FullLayoutOrder()
    {
        // RV-UV both/4-chart/2-terminator case: 24 values in the order
        // start.hvec[0] (u0,v0,u1,v1), 4 chart rows, end.hvec[0].
        var uv = new double[24];
        for (var i = 0; i < 24; i++) uv[i] = i;
        var input = ValidInput(IntersectionUvType.Both, uv);
        input.Start.Type = LimitType.Terminator;
        input.Start.Hvecs = [0.0, 0.0, 0.0, 0.0001, 0.000001, 0.0]; // position + branch point
        input.End.Type = LimitType.Terminator;
        input.End.Hvecs = [0.2, 0.9, 0.0, 0.21, 0.89, 0.0];

        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        ref readonly var data = ref KernelRuntime.ICurveDataPool[slot];
        // Terminators count for their hvec[0] UV only; the branch hvec is not
        // counted again (spec §2.4).
        Assert.Equal(24, data.UvValueCount);
        Assert.Equal(8, data.HvecCount); // 2 + 4 + 2

        var stored = new ReadOnlySpan<double>(KernelRuntime.DereferenceBlock(data.UvValueBlock), 24);
        for (var i = 0; i < 24; i++) Assert.Equal(i, stored[i], 12);
        Assert.Equal(0, data.StartLimit.HvecIndex);
        Assert.Equal(2, data.StartLimit.HvecCount);
        Assert.Equal(6, data.EndLimit.HvecIndex);

        KernelRuntime.FreeICurveData(slot);
    }

    [Fact]
    public void Decode_RejectsCorruptInputsWithLocatedCauses()
    {
        // Null UV pair: NaN sentinel in the first pair.
        var uv = new double[8];
        Array.Fill(uv, 1.0);
        uv[0] = double.NaN;
        var input = ValidInput(IntersectionUvType.First, uv);
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out var failure, out var index));
        Assert.Equal(IcurveDecodeFailure.NullUvValue, failure);
        Assert.Equal(0, index);

        // Single null component inside a later pair.
        uv[0] = 1.0;
        uv[5] = double.NaN;
        input = ValidInput(IntersectionUvType.First, uv);
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out index));
        Assert.Equal(IcurveDecodeFailure.NullUvValue, failure);
        Assert.Equal(5, index);

        // Truncated values.
        input = ValidInput(IntersectionUvType.Both, new double[15]);
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.TruncatedUvValues, failure);

        // Oversized values.
        input = ValidInput(IntersectionUvType.None, new double[3]);
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.OversizedUvValues, failure);

        // Unknown enum value.
        input = ValidInput((IntersectionUvType)9, []);
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.UnknownUvType, failure);

        // Wrong chart length.
        input = ValidInput(IntersectionUvType.None, []);
        input.ChartHvecs = new double[9];
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.ChartHvecWrongLength, failure);

        // Non-finite chart coordinate (a "?" hull vector must not become zero).
        input = ValidInput(IntersectionUvType.None, []);
        var badChart = new double[12];
        Array.Fill(badChart, 1.0);
        badChart[7] = double.NaN;
        input.ChartHvecs = badChart;
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out index));
        Assert.Equal(IcurveDecodeFailure.ChartHvecNotFinite, failure);
        Assert.Equal(7, index);

        // Terminator limit must carry exactly two hull vectors.
        input = ValidInput(IntersectionUvType.None, []);
        input.Start.Type = LimitType.Terminator;
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.LimitHvecWrongLength, failure);

        // Extended charts are declared unsupported at decode time.
        input = ValidInput(IntersectionUvType.None, []);
        input.ExtendedChartCount = 3;
        Assert.Equal(AlgorithmStatus.Unsupported, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.ExtendedChartUnsupported, failure);

        // Degenerate chart.
        input = ValidInput(IntersectionUvType.None, []);
        input.ChartCount = 1;
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out _, out failure, out _));
        Assert.Equal(IcurveDecodeFailure.BadChartCount, failure);
    }

    [Fact]
    public void Decode_FailureLeavesNoPartialStorage()
    {
        var before = KernelRuntime.ICurveDataPool.AllocatedCount;
        var uv = new double[8];
        Array.Fill(uv, 1.0);
        uv[3] = double.NaN;
        var input = ValidInput(IntersectionUvType.First, uv);
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        Assert.Equal(-1, slot);
        Assert.Equal(before, KernelRuntime.ICurveDataPool.AllocatedCount);
    }
}
