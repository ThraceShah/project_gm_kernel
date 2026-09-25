using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Prepare evaluation views from pooled geometry (spec §4, §19, task T19).
/// ICurve preparation rebuilds the immutable original chart map into
/// command-scratch spans and returns a borrowed <see cref="ICurveView"/> that
/// must not outlive the scratch checkpoint. Analytic supports only — procedural
/// supports stay <see cref="AlgorithmStatus.Unsupported"/> until later tasks.
/// </summary>
internal static unsafe partial class KernelRuntime
{
    /// <summary>
    /// Bind a decoded <see cref="ICurveData"/> slot to a public curve tag so
    /// <c>PK_CURVE_eval</c> can reach it. Computes TMin/TMax from the rebuilt
    /// chart parameter range. The data slot is consumed on success (freed on
    /// curve delete via <see cref="FreeCurveData"/>); on failure the data slot
    /// is left untouched for the caller to free.
    /// </summary>
    internal static AlgorithmStatus TryBindICurveEntity(DataSlot icurveDataIndex, out CurveTag tag)
    {
        tag = 0;
        if (!IsSessionStarted) return AlgorithmStatus.InvalidInput;
        if (!ICurveDataPool.IsAlive(icurveDataIndex)) return AlgorithmStatus.InvalidInput;

        var ownsScratch = EnsureCommandScratch();
        try
        {
            // Provisional curve record so TryPrepareICurveView can resolve the slot.
            if (!Curves.TryAllocate(out var curveSlot))
                return AlgorithmStatus.WorkspaceTooSmall;
            GeometryEvaluationCache.BumpModelGeometryEpoch();
            ref var curve = ref Curves[curveSlot];
            AssignPartition(ref curve.Header, CurrentPartition);
            curve.Class = CurveClass.ICurve;
            curve.DataIndex = icurveDataIndex;
            curve.TMin = 0;
            curve.TMax = 1;
            curve.Sense = ICurveDataPool[icurveDataIndex].Sense;
            curve.OwnerEdge = -1;
            curve.OwnerCount = 0;
            curve.PrevInBody = curve.NextInBody = 0;

            var prepareStatus = TryPrepareICurveView(in curve, out var view, out _);
            if (prepareStatus != AlgorithmStatus.Success)
            {
                Curves.Free(curveSlot);
                return prepareStatus;
            }

            curve.TMin = view.ChartParameters[0];
            curve.TMax = view.ChartParameters[^1];
            var allocated = AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
            if (allocated <= 0)
            {
                Curves.Free(curveSlot);
                return AlgorithmStatus.WorkspaceTooSmall;
            }
            tag = allocated;
            return AlgorithmStatus.Success;
        }
        finally
        {
            if (ownsScratch) ReleaseCommandScratch(State.Session);
        }
    }

    /// <summary>
    /// Build an <see cref="ICurveView"/> for one pooled icurve into the current
    /// command scratch. Callers must hold the command scratch for the lifetime
    /// of the returned view (ref struct borrows scratch spans).
    /// </summary>
    internal static AlgorithmStatus TryPrepareICurveView(in CurveRecord record,
        out ICurveView view, out ChartBuildFailure chartFailure)
    {
        view = default;
        chartFailure = ChartBuildFailure.None;
        if (record.Class != CurveClass.ICurve) return AlgorithmStatus.InvalidInput;
        if (!ICurveDataPool.IsAlive(record.DataIndex)) return AlgorithmStatus.InvalidInput;

        ref readonly var data = ref ICurveDataPool[record.DataIndex];
        var slot0 = GetSurfaceSlotByTag(data.Surface0Tag);
        var slot1 = GetSurfaceSlotByTag(data.Surface1Tag);
        if (slot0 < 0 || !Surfaces.IsAlive(slot0) || slot1 < 0 || !Surfaces.IsAlive(slot1))
            return AlgorithmStatus.Unsupported;
        ref readonly var surf0 = ref Surfaces[slot0];
        ref readonly var surf1 = ref Surfaces[slot1];
        if (!TryPrepareSurface(in surf0, out var analytic0)
            || !TryPrepareSurface(in surf1, out var analytic1))
            return AlgorithmStatus.Unsupported; // non-analytic / BlendBound supports: later tasks

        var chartCount = data.ChartCount;
        if (chartCount < 2) return AlgorithmStatus.InvalidInput;
        var hvecs = HvecSpan(in data);
        if (hvecs.Length < (data.StartLimit.HvecCount + chartCount + data.EndLimit.HvecCount) * 3)
            return AlgorithmStatus.InvalidInput;

        var chartOffset = data.StartLimit.HvecCount * 3;
        var scratch = CommandScratch.Current;
        var positionDoubles = scratch.Take(chartCount * 3);
        var parameterScratch = scratch.Take(chartCount);
        var scaleScratch = scratch.Take(chartCount - 1);
        var chordDoubles = scratch.Take((chartCount - 1) * 3);
        var tangentDoubles = scratch.Take(chartCount * 3);
        if (positionDoubles.Length < chartCount * 3
            || parameterScratch.Length < chartCount
            || scaleScratch.Length < chartCount - 1
            || chordDoubles.Length < (chartCount - 1) * 3
            || tangentDoubles.Length < chartCount * 3)
            return AlgorithmStatus.WorkspaceTooSmall;

        var positions = MemoryMarshal.Cast<double, KernelVector3>(positionDoubles);
        var tangents = MemoryMarshal.Cast<double, KernelVector3>(tangentDoubles);
        var chords = MemoryMarshal.Cast<double, KernelVector3>(chordDoubles);
        var localScale = Math.Max(1.0, Math.Max(
            Math.Max(analytic0.Radius, analytic0.Secondary),
            Math.Max(analytic1.Radius, analytic1.Secondary)));
        var pointOnSurfaceTolerance = 1e-8 * localScale;

        for (BufferOffset i = 0; i < chartCount; i++)
        {
            var o = chartOffset + i * 3;
            positions[i] = Vector(hvecs[o], hvecs[o + 1], hvecs[o + 2]);
            if (!IsChartPointConsistent(in analytic0, in analytic1, in positions[i], pointOnSurfaceTolerance)
                || !IsChartUvConsistent(in data, i, in analytic0, in analytic1,
                    in positions[i], pointOnSurfaceTolerance))
                return AlgorithmStatus.InvalidInput;
            if (!TryChartTangent(in analytic0, surf0.Sense, in analytic1, surf1.Sense,
                    in positions[i], out tangents[i]))
                return AlgorithmStatus.Singular;
        }

        var buildStatus = OriginalChartParameterMap.Build(
            positions, tangents, data.BaseParameter, data.BaseScale,
            parameterScratch, scaleScratch, chords, out _, out chartFailure);
        if (buildStatus != AlgorithmStatus.Success) return buildStatus;

        var startTerminator = default(TerminatorLimit);
        var endTerminator = default(TerminatorLimit);
        var hasStart = false;
        var hasEnd = false;
        if (data.StartLimit.Type == LimitType.Terminator && data.StartLimit.HvecCount >= 2)
        {
            startTerminator = ReadTerminator(hvecs, data.StartLimit.HvecIndex, data.StartLimit.TermUse);
            hasStart = true;
        }
        if (data.EndLimit.Type == LimitType.Terminator && data.EndLimit.HvecCount >= 2)
        {
            endTerminator = ReadTerminator(hvecs, data.EndLimit.HvecIndex, data.EndLimit.TermUse);
            hasEnd = true;
        }

        if (hasStart || hasEnd)
        {
            view = new ICurveView(in analytic0, surf0.Sense, in analytic1, surf1.Sense,
                positions, parameterScratch, scaleScratch, chords,
                hasStart ? startTerminator : default,
                hasEnd ? endTerminator : default);
        }
        else
        {
            view = new ICurveView(in analytic0, surf0.Sense, in analytic1, surf1.Sense,
                positions, parameterScratch, scaleScratch, chords);
        }
        return AlgorithmStatus.Success;
    }

    private static ReadOnlySpan<double> HvecSpan(in ICurveData data)
    {
        var block = DereferenceBlock(data.HvecBlock);
        if (block == null || data.HvecCount <= 0) return ReadOnlySpan<double>.Empty;
        return new ReadOnlySpan<double>((double*)block, data.HvecCount * 3);
    }

    private static bool IsChartPointConsistent(in AnalyticSurface support0,
        in AnalyticSurface support1, in KernelVector3 point, double tolerance)
        => AnalyticImplicitEvaluation.GeometricDeviation(in support0, in point, out var d0)
                == AlgorithmStatus.Success
            && AnalyticImplicitEvaluation.GeometricDeviation(in support1, in point, out var d1)
                == AlgorithmStatus.Success
            && d0 <= tolerance && d1 <= tolerance;

    private static bool IsChartUvConsistent(in ICurveData data, BufferOffset chartIndex,
        in AnalyticSurface support0, in AnalyticSurface support1, in KernelVector3 point,
        double tolerance)
    {
        var stride = UvStrideOf(data.UvType);
        if (stride <= 0) return true;
        var block = DereferenceBlock(data.UvValueBlock);
        if (block == null || data.UvValueCount < (chartIndex + 1) * stride) return false;
        var values = new ReadOnlySpan<double>((double*)block, data.UvValueCount);
        var row = (data.StartLimit.Type == LimitType.Terminator ? 1 : 0) + chartIndex;
        var offset = row * stride;
        if (offset + stride > values.Length) return false;
        return data.UvType switch
        {
            IntersectionUvType.First => IsUvPointConsistent(in support0,
                values[offset], values[offset + 1], in point, tolerance),
            IntersectionUvType.Second => IsUvPointConsistent(in support1,
                values[offset], values[offset + 1], in point, tolerance),
            IntersectionUvType.Both => IsUvPointConsistent(in support0,
                    values[offset], values[offset + 1], in point, tolerance)
                && IsUvPointConsistent(in support1,
                    values[offset + 2], values[offset + 3], in point, tolerance),
            _ => false,
        };
    }

    private static bool IsUvPointConsistent(in AnalyticSurface support, double u, double v,
        in KernelVector3 point, double tolerance)
    {
        // Both components NaN represent an omitted UV pair in Parasolid XT.
        if (double.IsNaN(u) && double.IsNaN(v)) return true;
        if (!double.IsFinite(u) || !double.IsFinite(v)) return false;
        if (!SurfaceDerivativeLayout.TryCreate(0, 0, out var layout)) return false;
        Span<KernelVector3> value = stackalloc KernelVector3[1];
        if (SurfaceEvaluation.Evaluate(in support, u, v, in layout, value) != AlgorithmStatus.Success)
            return false;
        var delta = Vector(value[0].X - point.X, value[0].Y - point.Y, value[0].Z - point.Z);
        return Math.Sqrt(Dot(delta, delta)) <= tolerance;
    }

    private static TerminatorLimit ReadTerminator(ReadOnlySpan<double> hvecs, BufferOffset hvecIndex,
        LimitTermUse termUse)
    {
        var o = hvecIndex * 3;
        var endpoint = Vector(hvecs[o], hvecs[o + 1], hvecs[o + 2]);
        var branch = Vector(hvecs[o + 3], hvecs[o + 4], hvecs[o + 5]);
        return new TerminatorLimit(termUse, in endpoint, in branch);
    }

    /// <summary>
    /// Unit chart tangent T = sense₀·n₀ × sense₁·n₁ (§5.1). Gradients come from
    /// the analytic zero-set jet; surface sense flips the corresponding normal.
    /// </summary>
    private static bool TryChartTangent(in AnalyticSurface s0, KernelSense sense0,
        in AnalyticSurface s1, KernelSense sense1, in KernelVector3 point, out KernelVector3 tangent)
    {
        tangent = default;
        if (AnalyticImplicitEvaluation.Evaluate(in s0, in point, 1, out var j0) != AlgorithmStatus.Success
            || AnalyticImplicitEvaluation.Evaluate(in s1, in point, 1, out var j1) != AlgorithmStatus.Success)
            return false;
        var n0 = Scale(j0.Gradient, SenseSign(sense0));
        var n1 = Scale(j1.Gradient, SenseSign(sense1));
        var cross = EvaluationMath.Cross(n0, n1);
        var normSq = Dot(cross, cross);
        if (!(normSq > 1e-30)) return false;
        tangent = Scale(cross, 1.0 / Math.Sqrt(normSq));
        return IsFinite(tangent);
    }

    private static double SenseSign(KernelSense sense)
        => sense == ParasolidConstants.PK_TOPOL_sense_negative_c ? -1.0 : 1.0;
}
