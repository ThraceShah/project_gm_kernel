using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

namespace KernelTests;

/// <summary>
/// Runtime wiring of icurve evaluation (spec §19, task T19 subset): decode →
/// bind CurveTag → PK_CURVE_eval through <see cref="KernelRuntime"/> prepare
/// + <see cref="ICurveEvaluation"/>. Analytic plane∩sphere fixture only; real
/// Parasolid oracle remains NotRun (GATE-T/D open).
/// </summary>
[Collection("KernelTests")]
public unsafe class IcurveRuntimeEvalTests : IDisposable
{
    public IcurveRuntimeEvalTests()
    {
        KernelRuntime.SessionStop();
        var options = new PK_SESSION_start_o_s { o_t_version = 1 };
        Assert.Equal(0, KernelRuntime.SessionStart(&options));
    }

    public void Dispose() => KernelRuntime.SessionStop();

    [Fact]
    public void BindAndEval_PlaneSphereCircle_ChartNodeAndRegularInterval()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        double[] angles = [0.0, 0.35, 0.8, 1.25];
        var chart = new double[angles.Length * 3];
        for (var i = 0; i < angles.Length; i++)
        {
            chart[i * 3] = Math.Cos(angles[i]);
            chart[i * 3 + 1] = Math.Sin(angles[i]);
            chart[i * 3 + 2] = 0;
        }

        var input = new IcurveDecodeInput
        {
            Surface0Tag = plane,
            Surface1Tag = sphere,
            BaseParameter = -1.5,
            BaseScale = 1.2,
            ChartCount = angles.Length,
            ChartHvecs = chart,
            Start = new IcurveLimitInput
            {
                Type = LimitType.Help,
                TermUse = LimitTermUse.Unset,
                Hvecs = [chart[0], chart[1], chart[2]],
            },
            End = new IcurveLimitInput
            {
                Type = LimitType.Help,
                TermUse = LimitTermUse.Unset,
                Hvecs =
                [
                    chart[^3], chart[^2], chart[^1],
                ],
            },
            UvType = IntersectionUvType.None,
            ChordalError = 1e-4,
            AngularError = 1e-6,
        };

        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.DecodeIcurve(input, out var dataSlot, out var failure, out _));
        Assert.Equal(IcurveDecodeFailure.None, failure);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.TryBindICurveEntity(dataSlot, out var curveTag));
        Assert.True(curveTag > 0);

        var record = KernelRuntime.GetCurveByTag(curveTag);
        Assert.Equal(CurveClass.ICurve, record.Class);
        Assert.True(record.TMax > record.TMin);

        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[3];
        // Exact chart node: defining position, bit-exact with the imported hvec.
        Assert.Equal(0, KernelRuntime.CurveEval(curveTag, record.TMin, 0, output));
        Assert.Equal(chart[0], output[0].coord[0], 12);
        Assert.Equal(chart[1], output[0].coord[1], 12);
        Assert.Equal(chart[2], output[0].coord[2], 12);

        // Regular interval midpoint: on both supports.
        var tMid = 0.5 * (record.TMin + record.TMax);
        Assert.Equal(0, KernelRuntime.CurveEval(curveTag, tMid, 2, output));
        var x = output[0].coord[0];
        var y = output[0].coord[1];
        var z = output[0].coord[2];
        Assert.True(Math.Abs(z) < 1e-9);
        Assert.True(Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 1.0) < 1e-9);
        // D1 should be non-zero along the circle.
        var d1 = Math.Sqrt(output[1].coord[0] * output[1].coord[0]
            + output[1].coord[1] * output[1].coord[1]
            + output[1].coord[2] * output[1].coord[2]);
        Assert.True(d1 > 1e-6);

        PK_VECTOR_s tangent = default;
        Assert.Equal(0, KernelRuntime.CurveEvalWithTangent(curveTag, tMid, 0, output, &tangent));
        var tangentNorm = Math.Sqrt(tangent.coord[0] * tangent.coord[0]
            + tangent.coord[1] * tangent.coord[1] + tangent.coord[2] * tangent.coord[2]);
        Assert.Equal(1, tangentNorm, 12);

        var identity = new GeometryIdentity(record.Header.Tag, record.Header.Generation);
        Assert.True(GeometryEvaluationCache.TryGetExact(in identity, tMid,
            ICurveQueryKind.RegularChartInterval, ChartSide.Right, 1,
            ICurveEvaluation.CacheErrorBound, out _));
    }

    [Fact]
    public void Prepare_RejectsChartPointsOffSupportingSphere()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        double[] chart = [2, 0, 0, 0, 2, 0];
        var input = MinimalCircleInput(plane, sphere, chart);
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput,
            KernelRuntime.TryBindICurveEntity(slot, out _));
        KernelRuntime.FreeICurveData(slot);
    }

    [Fact]
    public void CurveEval_OrderAboveTwo_IsRejected()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        double[] chart = [1, 0, 0, 0, 1, 0];
        var input = MinimalCircleInput(plane, sphere, chart);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(slot, out var tag));

        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[4];
        var record = KernelRuntime.GetCurveByTag(tag);
        var t = 0.5 * (record.TMin + record.TMax);
        Assert.Equal(ParasolidConstants.PK_ERROR_too_many_derivatives,
            KernelRuntime.CurveEval(tag, t, 3, output));
    }

    [Fact]
    public void Prepare_NonAnalyticSupport_IsUnsupported()
    {
        // Offset surface is procedural — TryPrepareSurface fails → Unsupported.
        var plane = CreatePlaneZ0();
        int body;
        Assert.Equal(0, KernelRuntime.BodyCreateSolidBlock(2, 2, 2, null, &body));
        // Use the plane with itself would still be analytic; instead bind a
        // decoded icurve whose second tag is not a live analytic surface.
        double[] chart = [1, 0, 0, 0, 1, 0];
        var input = MinimalCircleInput(plane, plane + 99999, chart); // bogus second tag
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        Assert.Equal(AlgorithmStatus.Unsupported, KernelRuntime.TryBindICurveEntity(slot, out _));
        KernelRuntime.FreeICurveData(slot);
    }

    private static IcurveDecodeInput MinimalCircleInput(int s0, int s1, double[] chart)
        => new()
        {
            Surface0Tag = s0,
            Surface1Tag = s1,
            BaseParameter = 0,
            BaseScale = 1,
            ChartCount = chart.Length / 3,
            ChartHvecs = chart,
            Start = new IcurveLimitInput
            {
                Type = LimitType.Help,
                TermUse = LimitTermUse.Unset,
                Hvecs = chart.AsSpan(0, 3).ToArray(),
            },
            End = new IcurveLimitInput
            {
                Type = LimitType.Help,
                TermUse = LimitTermUse.Unset,
                Hvecs = chart.AsSpan(chart.Length - 3, 3).ToArray(),
            },
            UvType = IntersectionUvType.None,
            ChordalError = 1e-4,
            AngularError = 1e-6,
        };

    [Fact]
    public void PointOnSurfaceTolerance_DecoupledFromChordalError()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();

        // Off-surface chart points: radius 1.05 instead of 1.0 (0.05 deviation from unit sphere)
        double[] offSurfaceChart = [1.05, 0, 0, 0, 1.05, 0];

        // 1. With small ChordalError = 1e-6: rejected
        var inputSmall = MinimalCircleInput(plane, sphere, offSurfaceChart);
        inputSmall.ChordalError = 1e-6;
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(inputSmall, out var slotSmall, out _, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.TryBindICurveEntity(slotSmall, out _));
        KernelRuntime.FreeICurveData(slotSmall);

        // 2. With large ChordalError = 0.1: must ALSO be rejected (not relaxed by ChordalError)
        var inputLarge = MinimalCircleInput(plane, sphere, offSurfaceChart);
        inputLarge.ChordalError = 0.1;
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(inputLarge, out var slotLarge, out _, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.TryBindICurveEntity(slotLarge, out _));
        KernelRuntime.FreeICurveData(slotLarge);

        // 3. Valid on-surface chart points with large ChordalError = 0.1: must succeed
        double[] onSurfaceChart = [1.0, 0, 0, 0, 1.0, 0];
        var inputValid = MinimalCircleInput(plane, sphere, onSurfaceChart);
        inputValid.ChordalError = 0.1;
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(inputValid, out var slotValid, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(slotValid, out var tagValid));
        Assert.True(tagValid > 0);
    }

    [Fact]
    public void UvNullPair_AcceptedWhenCompletePair_RejectedWhenPartial()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        double[] chart = [1.0, 0, 0, 0, 1.0, 0];

        // Complete null pairs (NaN, NaN) for 2 chart points with UvType.First (stride 2 -> 4 doubles)
        double[] uvPairs = [double.NaN, double.NaN, double.NaN, double.NaN];
        var inputComplete = MinimalCircleInput(plane, sphere, chart);
        inputComplete.UvType = IntersectionUvType.First;
        inputComplete.UvValues = uvPairs;

        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(inputComplete, out var slotComplete, out var failComplete, out _));
        Assert.Equal(IcurveDecodeFailure.None, failComplete);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(slotComplete, out var tagComplete));
        Assert.True(tagComplete > 0);

        // Single null component (NaN, 0.0) -> rejected by decode
        double[] uvPartial = [double.NaN, 0.0, 0.0, 0.0];
        var inputPartial = MinimalCircleInput(plane, sphere, chart);
        inputPartial.UvType = IntersectionUvType.First;
        inputPartial.UvValues = uvPartial;

        Assert.Equal(AlgorithmStatus.InvalidInput, KernelRuntime.DecodeIcurve(inputPartial, out _, out var failPartial, out var indexPartial));
        Assert.Equal(IcurveDecodeFailure.NullUvValue, failPartial);
        Assert.Equal(0, indexPartial);
    }

    private static int CreatePlaneZ0()
    {
        var sf = new PK_PLANE_sf_s();
        sf.basis_set.axis.coord[2] = 1;
        sf.basis_set.ref_direction.coord[0] = 1;
        int tag = 0;
        Assert.Equal(0, KernelRuntime.PlaneCreate(&sf, &tag));
        return tag;
    }

    private static int CreateUnitSphere()
    {
        var sf = new PK_SPHERE_sf_s();
        sf.basis_set.axis.coord[2] = 1;
        sf.basis_set.ref_direction.coord[0] = 1;
        sf.radius = 1;
        int tag = 0;
        Assert.Equal(0, KernelRuntime.SphereCreate(&sf, &tag));
        return tag;
    }
}
