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

        // A downstream finite-domain solver must use this carrier's native
        // map through its final CHART endpoint, including a nonunit scale.
        var owns=KernelRuntime.EnsureCommandScratch();
        try
        {
            Assert.Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedIntersectionCurveExtent(curveTag,
                record.TMax-.1*(record.TMax-record.TMin),record.TMax,out var extent));
            Assert.Equal(0,KernelRuntime.CurveEval(curveTag,record.TMax,0,output));
            Assert.InRange(output[0].coord[0],extent.XLo,extent.XHi);
            Assert.InRange(output[0].coord[1],extent.YLo,extent.YHi);
            Assert.InRange(output[0].coord[2],extent.ZLo,extent.ZHi);
        }
        finally{if(owns)KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);}

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
            1e-9, out _));
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
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
        KernelRuntime.FreeICurveData(slot);
        Assert.True(KernelRuntime.IsValidTag(plane));
        Assert.True(KernelRuntime.IsValidTag(sphere));
    }

    [Fact]
    public void Bind_AcquiresSupportsBeforePublicEdgeAttachmentAndProtectsTheirLifetime()
    {
        var plane = CreatePlaneZ0(); var sphere = CreateUnitSphere();
        var curve = BindCircle(plane, sphere);
        Assert.Equal((byte)1, KernelRuntime.GetCurveByTag(curve).DependenciesAcquired);
        Assert.Equal(1, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
        Assert.Equal(1, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &plane));
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &sphere));
        // The wire helper calls the public EdgeAttachCurves entry point after
        // allocating its topology. Both defining surfaces must be recognized
        // as dependencies, not unrelated free roots of the attachment graph.
        Assert.Equal(0, KernelRuntime.CreateICurveWireBody(curve, out var body));
        int count; int* construction;
        Assert.Equal(0, KernelRuntime.PartAskGeoms(body, &count, &construction));
        Assert.Equal(0, count); Assert.True(construction == null);
        AssertCircleEvaluates(curve);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.True(KernelRuntime.IsValidTag(plane)); Assert.True(KernelRuntime.IsValidTag(sphere));
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &sphere));
        Assert.Equal(0, KernelRuntime.ICurveDataPool.AliveCount);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void Bind_SharedSupportsSurviveFirstDeletionAndCopyRemainsIndependent(bool importedOwner, bool deleteOwnerFirst)
    {
        var before = KernelRuntime.State.Session->Blocks.Statistics;
        var plane = CreatePlaneZ0(); var sphere = CreateUnitSphere();
        var first = BindCircle(plane, sphere);
        if (importedOwner)
        {
            Assert.Equal(0, KernelRuntime.CreateICurveWireBody(first, out var body));
            Assert.Equal(0, XtWriter.WriteText([body], 0, out var text));
            var document = XtText.DecodeDocument(text);
            var node = document.Nodes.Single(n => n.Type == 38); // XT INTERSECTION
            Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryMaterializeICurveFromXt(document, node.Index, out first, out _));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &sphere));
            var imported = KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(first).DataIndex];
            Assert.Equal((byte)1, imported.OwnsSupportSurfaces);
            plane = imported.Surface0Tag; sphere = imported.Surface1Tag;
        }
        var second = BindCircle(plane, sphere);
        Assert.Equal(2, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
        Assert.Equal(2, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
        int copy; Assert.Equal(0, KernelRuntime.EntityCopy(first, &copy));
        var copiedData = KernelRuntime.ICurveDataPool[KernelRuntime.GetCurveByTag(copy).DataIndex];
        Assert.Equal((byte)1, copiedData.OwnsSupportSurfaces);
        Assert.NotEqual(plane, copiedData.Surface0Tag); Assert.NotEqual(sphere, copiedData.Surface1Tag);
        var firstDeleted = deleteOwnerFirst ? first : second;
        var survivor = deleteOwnerFirst ? second : first;
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &firstDeleted));
        Assert.Equal(1, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
        Assert.Equal(1, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
        Assert.Equal(importedOwner && deleteOwnerFirst ? 1 : 0, KernelRuntime.GetSurfaceByTag(plane).GeometricDeletePending);
        AssertCircleEvaluates(survivor);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &survivor));
        if (importedOwner)
        {
            Assert.False(KernelRuntime.IsValidTag(plane)); Assert.False(KernelRuntime.IsValidTag(sphere));
        }
        else
        {
            Assert.True(KernelRuntime.IsValidTag(plane)); Assert.True(KernelRuntime.IsValidTag(sphere));
            Assert.Equal(0, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
            Assert.Equal(0, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
            Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &sphere));
        }
        AssertCircleEvaluates(copy);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &copy));
        Assert.False(KernelRuntime.IsValidTag(copiedData.Surface0Tag));
        Assert.False(KernelRuntime.IsValidTag(copiedData.Surface1Tag));
        Assert.Equal(0, KernelRuntime.Surfaces.AliveCount); Assert.Equal(0, KernelRuntime.Curves.AliveCount);
        Assert.Equal(0, KernelRuntime.ICurveDataPool.AliveCount);
        var after = KernelRuntime.State.Session->Blocks.Statistics;
        Assert.Equal(before.LiveBlocks, after.LiveBlocks); Assert.Equal(before.LiveBytes, after.LiveBytes);
    }

    [Fact]
    public void Bind_BorrowedFaceSupportOutlivesBodyAndRestoresOwnershipOnMarkGoto()
    {
        int body; Assert.Equal(0, KernelRuntime.BodyCreateSolidSphere(1, null, &body));
        int count; int* faces;
        Assert.Equal(0, KernelRuntime.BodyAskFaces(body, &count, &faces)); Assert.Equal(1, count);
        int sphere; Assert.Equal(0, KernelRuntime.FaceAskSurf(faces[0], &sphere));
        var plane = CreatePlaneZ0(); var curve = BindCircle(plane, sphere);
        int mark; Assert.Equal(0, KernelRuntime.MarkCreate(&mark));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(sphere).OwnerCount);
        Assert.Equal((byte)1, KernelRuntime.GetSurfaceByTag(sphere).GeometricDeletePending);
        AssertCircleEvaluates(curve);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &curve));
        Assert.False(KernelRuntime.IsValidTag(sphere)); Assert.True(KernelRuntime.IsValidTag(plane));
        Assert.Equal(0, KernelRuntime.MarkGoto(mark));
        Assert.Equal(1, KernelRuntime.GetSurfaceByTag(sphere).OwnerCount);
        Assert.Equal((byte)0, KernelRuntime.GetSurfaceByTag(sphere).GeometricDeletePending);
        AssertCircleEvaluates(curve);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &body));
        Assert.Equal((byte)1, KernelRuntime.GetSurfaceByTag(sphere).GeometricDeletePending);
        AssertCircleEvaluates(curve);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &curve));
        Assert.False(KernelRuntime.IsValidTag(sphere));
        Assert.True(KernelRuntime.IsValidTag(plane));
        Assert.Equal(0, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
        Assert.Equal((byte)0, KernelRuntime.GetSurfaceByTag(plane).GeometricDeletePending);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane));
        Assert.Equal(0, KernelRuntime.Surfaces.AliveCount); Assert.Equal(0, KernelRuntime.Curves.AliveCount);
    }

    [Fact]
    public void CopyAllocationFailure_PreservesBorrowedSourcesAndReleasesPartialOwnedGraph()
    {
        var plane = CreatePlaneZ0(); var sphere = CreateUnitSphere(); var curve = BindCircle(plane, sphere);
        var before = KernelRuntime.State.Session->Blocks.Statistics;
        var reachedSuccess = false;
        for (var fault = 0; fault < 64; fault++)
        {
            int copy; int error;
            KernelRuntime.InjectAllocationFailure(fault);
            try { error = KernelRuntime.EntityCopy(curve, &copy); }
            finally { KernelRuntime.InjectAllocationFailure(-1); }
            if (error == 0)
            {
                AssertCircleEvaluates(copy);
                Assert.Equal(0, KernelRuntime.EntityDelete(1, &copy));
                reachedSuccess = true;
            }
            else
            {
                Assert.Equal(ParasolidConstants.PK_ERROR_memory_full, error); Assert.Equal(0, copy);
            }
            Assert.Equal(1, KernelRuntime.Curves.AliveCount); Assert.Equal(2, KernelRuntime.Surfaces.AliveCount);
            Assert.Equal(1, KernelRuntime.ICurveDataPool.AliveCount);
            Assert.Equal(1, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
            Assert.Equal(1, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
            Assert.Equal((byte)0, KernelRuntime.GetSurfaceByTag(plane).GeometricDeletePending);
            Assert.Equal((byte)0, KernelRuntime.GetSurfaceByTag(sphere).GeometricDeletePending);
            var after = KernelRuntime.State.Session->Blocks.Statistics;
            Assert.Equal(before.LiveBlocks, after.LiveBlocks); Assert.Equal(before.LiveBytes, after.LiveBytes);
            if (reachedSuccess) break;
        }
        Assert.True(reachedSuccess); AssertCircleEvaluates(curve);
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &curve));
        Assert.True(KernelRuntime.IsValidTag(plane)); Assert.True(KernelRuntime.IsValidTag(sphere));
        Assert.Equal(0, KernelRuntime.EntityDelete(1, &plane)); Assert.Equal(0, KernelRuntime.EntityDelete(1, &sphere));
    }

    [Fact]
    public void Bind_SupportReferenceOverflowDoesNotConsumePayloadOrAcquireOtherSupport()
    {
        var plane = CreatePlaneZ0(); var sphere = CreateUnitSphere();
        var input = MinimalCircleInput(plane, sphere, [1,0,0, 0,1,0]);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var data, out _, out _));
        var planeSlot = KernelRuntime.GetSurfaceSlotByTag(plane);
        KernelRuntime.Surfaces[planeSlot].GeometricOwnerCount = int.MaxValue;
        try
        {
            Assert.Equal(AlgorithmStatus.WorkspaceTooSmall, KernelRuntime.TryBindICurveEntity(data, out var curve));
            Assert.Equal(0, curve); Assert.True(KernelRuntime.ICurveDataPool.IsAlive(data));
            Assert.Equal(int.MaxValue, KernelRuntime.GetSurfaceByTag(plane).GeometricOwnerCount);
            Assert.Equal(0, KernelRuntime.GetSurfaceByTag(sphere).GeometricOwnerCount);
            Assert.Equal(0, KernelRuntime.Curves.AliveCount);
        }
        finally { KernelRuntime.Surfaces[planeSlot].GeometricOwnerCount = 0; }
        KernelRuntime.FreeICurveData(data);
        Assert.True(KernelRuntime.IsValidTag(plane)); Assert.True(KernelRuntime.IsValidTag(sphere));
    }

    internal static int BindCircle(int plane, int sphere)
    {
        var input = MinimalCircleInput(plane, sphere, [1,0,0, 0,1,0]);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var data, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(data, out var curve));
        return curve;
    }

    private static void AssertCircleEvaluates(int curve)
    {
        var record = KernelRuntime.GetCurveByTag(curve);
        PK_VECTOR_s* values = stackalloc PK_VECTOR_s[3];
        Assert.Equal(0, KernelRuntime.CurveEval(curve, (record.TMin + record.TMax) / 2, 2, values));
        Assert.InRange(Math.Abs(values[0].coord[2]), 0, 1e-9);
        Assert.InRange(Math.Abs(values[0].coord[0] * values[0].coord[0] + values[0].coord[1] * values[0].coord[1] - 1), 0, 1e-9);
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

    [Fact]
    public void TryBindICurveEntity_PropagatesChartFailureDiagnostic()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        double[] onSurfaceChart = [1.0, 0, 0, 0, 1.0, 0];
        var input = MinimalCircleInput(plane, sphere, onSurfaceChart);
        input.BaseScale = -1.0;

        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        Assert.Equal(AlgorithmStatus.InvalidInput,
            KernelRuntime.TryBindICurveEntity(slot, out var tag, out var chartFailure));
        Assert.Equal(0, tag);
        Assert.Equal(ChartBuildFailure.BadBaseScale, chartFailure);
        KernelRuntime.FreeICurveData(slot);
    }

    [Fact]
    public void Evaluate_InteriorKnot_LeftAndRightSidesProduceDistinctSecondDerivatives()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        // 3 segments with 4 points: angles 0.0, 0.4, 0.9, 1.4 (different angular spans -> different scales)
        double[] angles = [0.0, 0.4, 0.9, 1.4];
        var chart = new double[angles.Length * 3];
        for (var i = 0; i < angles.Length; i++)
        {
            chart[i * 3] = Math.Cos(angles[i]);
            chart[i * 3 + 1] = Math.Sin(angles[i]);
            chart[i * 3 + 2] = 0;
        }

        var input = MinimalCircleInput(plane, sphere, chart);
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.DecodeIcurve(input, out var slot, out _, out _));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryBindICurveEntity(slot, out var curveTag));
        Assert.True(curveTag > 0);

        var ownsScratch = KernelRuntime.EnsureCommandScratch();
        try
        {
            var record = KernelRuntime.GetCurveByTag(curveTag);
            Assert.Equal(AlgorithmStatus.Success, KernelRuntime.TryPrepareICurveView(in record, out var view, out _));

            // Knot 1 is at index 1
            var knotT = view.ChartParameters[1];
            Span<KernelVector3> derivLeft = stackalloc KernelVector3[3];
            Span<KernelVector3> derivRight = stackalloc KernelVector3[3];

            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.Evaluate(in view, knotT, 2, derivLeft, out var repLeft, ChartSide.Left));
            Assert.Equal(AlgorithmStatus.Success,
                ICurveEvaluation.Evaluate(in view, knotT, 2, derivRight, out var repRight, ChartSide.Right));

            Assert.Equal(ChartSide.Left, repLeft.Side);
            Assert.Equal(ChartSide.Right, repRight.Side);

            // Position at knot must be identical (both equal the chart knot point)
            Assert.Equal(derivLeft[0].X, derivRight[0].X, 12);
            Assert.Equal(derivLeft[0].Y, derivRight[0].Y, 12);
            Assert.Equal(derivLeft[0].Z, derivRight[0].Z, 12);

            // First derivative directions align with tangent, but D2 second derivatives reflect
            // the left vs right polynomial segments (scales differ: 0.4 vs 0.5)
            var d2Diff = Math.Abs(derivLeft[2].X - derivRight[2].X) + Math.Abs(derivLeft[2].Y - derivRight[2].Y);
            Assert.True(d2Diff > 1e-4, $"Expected distinct D2 across knot, got diff {d2Diff}");
        }
        finally
        {
            if (ownsScratch) KernelRuntime.ReleaseCommandScratch(KernelRuntime.State.Session);
        }
    }

    [Theory]
    [InlineData(1,false,false)] [InlineData(1,false,true)] [InlineData(.125,true,false)] [InlineData(8,false,true)]
    public void InPlaceGeometryTransformCannotReuseTheOldWorldSpaceCurveSample(double scale,bool mirror,bool rotate)
    {
        var plane=CreatePlaneZ0();var sphere=CreateUnitSphere();
        double[] chart=[1,0,0,Math.Cos(.4),Math.Sin(.4),0,Math.Cos(.8),Math.Sin(.8),0];
        var input=MinimalCircleInput(plane,sphere,chart);input.BaseParameter=-.25;input.Start.Type=input.End.Type=LimitType.Artificial;
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.DecodeIcurve(input,out var data,out _,out _));
        Assert.Equal(AlgorithmStatus.Success,KernelRuntime.TryBindICurveEntity(data,out var curve));
        var transformForm=new PK_TRANSF_sf_s();var sign=mirror?-1:1;
        transformForm.matrix[0]=rotate?0:scale*sign;transformForm.matrix[1]=rotate?-scale:0;
        transformForm.matrix[4]=rotate?scale*sign:0;transformForm.matrix[5]=rotate?0:scale;
        transformForm.matrix[10]=scale;transformForm.matrix[15]=1;
        transformForm.matrix[3]=3;transformForm.matrix[7]=5;transformForm.matrix[11]=7;
        int transform;Assert.Equal(0,KernelRuntime.TransfCreate(&transformForm,&transform));
        var record=KernelRuntime.GetCurveByTag(curve);var parameter=record.TMin+(record.TMax-record.TMin)*.37;
        PK_VECTOR_s* before=stackalloc PK_VECTOR_s[3];PK_VECTOR_s* after=stackalloc PK_VECTOR_s[3];
        Assert.Equal(0,KernelRuntime.CurveEval(curve,parameter,2,before));
        var identity=new GeometryIdentity(record.Header.Tag,record.Header.Generation);
        Assert.True(GeometryEvaluationCache.TryGetExact(identity,parameter,ICurveQueryKind.RegularChartInterval,ChartSide.Right,2,1e-9,out _));
        var options=new PK_GEOM_transform_o_s{o_t_version=1,modify=ParasolidConstants.PK_GEOM_transform_modify_c,want_out_geoms=1,want_exact=1};
        int* inputs=stackalloc int[3]{curve,plane,sphere};int* outputs=stackalloc int[3];byte* exact=stackalloc byte[3];
        Assert.Equal(0,KernelRuntime.GeomTransform2(3,inputs,transform,&options,outputs,exact));
        Assert.Equal(0,KernelRuntime.CurveEval(curve,parameter*scale,2,after));
        for(var order=0;order<=2;order++)for(var row=0;row<3;row++)
        {
            var expected=order==0?transformForm.matrix[4*row+3]:0;
            for(var axis=0;axis<3;axis++)expected+=transformForm.matrix[4*row+axis]*before[order].coord[axis]/Math.Pow(scale,order);
            Assert.Equal(expected,after[order].coord[row],10);
        }
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

    [Fact]
    public void DiagnosticLifecycle_PreparationFailureClearsOldReport()
    {
        var plane = CreatePlaneZ0();
        var sphere = CreateUnitSphere();
        double[] angles = [0.0, 0.5, 1.0];
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
            BaseParameter = 0.0,
            BaseScale = 1.0,
            ChartCount = angles.Length,
            ChartHvecs = chart,
            Start = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = [chart[0], chart[1], chart[2]] },
            End = new IcurveLimitInput { Type = LimitType.Help, TermUse = LimitTermUse.Unset, Hvecs = [chart[^3], chart[^2], chart[^1]] },
            UvType = IntersectionUvType.None,
            ChordalError = 1e-4,
            AngularError = 1e-6,
        };

        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.DecodeIcurve(input, out var dataSlot, out _, out _));
        Assert.Equal(AlgorithmStatus.Success,
            KernelRuntime.TryBindICurveEntity(dataSlot, out var curveTag));

        var record = KernelRuntime.GetCurveByTag(curveTag);
        PK_VECTOR_s* output = stackalloc PK_VECTOR_s[1];

        // Request A: success
        var midT = 0.5 * (record.TMin + record.TMax);
        Assert.Equal(0, KernelRuntime.CurveEval(curveTag, midT, 0, output));
        Assert.Equal(AlgorithmStatus.Success, KernelRuntime.LastICurveEvalReport.Status);

        // A public delete correctly refuses an acquired support. Inject an
        // invalid preparation reference directly to exercise the diagnostic
        // failure path, then restore the owned reference for normal cleanup.
        Assert.Equal(ParasolidConstants.PK_ERROR_still_referenced, KernelRuntime.EntityDelete(1, &sphere));
        KernelRuntime.ICurveDataPool[dataSlot].Surface1Tag = 0;
        try
        {
            var err = KernelRuntime.CurveEval(curveTag, midT, 0, output);
            Assert.NotEqual(0, err);
            Assert.NotEqual(AlgorithmStatus.Success, KernelRuntime.LastICurveEvalReport.Status);
        }
        finally { KernelRuntime.ICurveDataPool[dataSlot].Surface1Tag = sphere; }
    }
}
