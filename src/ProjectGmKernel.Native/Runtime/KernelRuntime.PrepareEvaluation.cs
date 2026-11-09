using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Prepare evaluation views from pooled geometry (spec §4, §19, task T19).
/// ICurve preparation rebuilds the immutable original chart map into
/// command-scratch spans and returns a borrowed <see cref="ICurveView"/> that
/// must not outlive the scratch checkpoint. Analytic and B-surface supports
/// share the prepared support interface. B-surface charts require finite UV
/// correspondence; only certified affine mixed-support termini are enabled.
/// </summary>
internal static unsafe partial class KernelRuntime
{
    /// <summary>
    /// Bind a decoded <see cref="ICurveData"/> slot to a public curve tag so
    /// <c>PK_CURVE_eval</c> can reach it. Computes TMin/TMax from the rebuilt
    /// chart parameter range. The data slot is consumed on success (freed on
    /// curve delete via <see cref="FreeCurveData"/>), and the bound curve holds
    /// liveness references to both support surfaces. Their lifetime ownership
    /// remains controlled by ICurveData.OwnsSupportSurfaces. On failure the data slot and
    /// support reference counts are left untouched for the caller to free.
    /// </summary>
    internal static AlgorithmStatus TryBindICurveEntity(DataSlot icurveDataIndex, out CurveTag tag)
        => TryBindICurveEntity(icurveDataIndex, out tag, out _);

    /// <summary>
    /// Bind a decoded <see cref="ICurveData"/> slot to a public curve tag and
    /// propagate any chart build failure diagnostics to the caller.
    /// </summary>
    internal static AlgorithmStatus TryBindICurveEntity(DataSlot icurveDataIndex, out CurveTag tag,
        out ChartBuildFailure chartFailure)
    {
        tag = 0;
        chartFailure = ChartBuildFailure.None;
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

            var prepareStatus = TryPrepareICurveView(in curve, out var view, out chartFailure);
            if (prepareStatus != AlgorithmStatus.Success)
            {
                Curves.Free(curveSlot);
                return prepareStatus;
            }

            var surface0 = GetSurfaceSlotByTag(ICurveDataPool[icurveDataIndex].Surface0Tag);
            var surface1 = GetSurfaceSlotByTag(ICurveDataPool[icurveDataIndex].Surface1Tag);
            var dependencyError = CheckGeometryDependency(PoolKind.Surface, surface0);
            if (dependencyError == 0) dependencyError = CheckGeometryDependency(PoolKind.Surface, surface1);
            if (dependencyError == 0 && surface0 == surface1
                && Surfaces[surface0].GeometricOwnerCount > int.MaxValue - 2)
                dependencyError = ParasolidConstants.PK_ERROR_memory_full;
            if (dependencyError != 0)
            {
                Curves.Free(curveSlot);
                return dependencyError == ParasolidConstants.PK_ERROR_memory_full
                    ? AlgorithmStatus.WorkspaceTooSmall : AlgorithmStatus.InvalidInput;
            }

            curve.TMin = view.ChartParameters[0];
            curve.TMax = view.ChartParameters[^1];
            if(view.StartTerminator.CertifiedAffine&&!AffineSplineTerminator.TryParameter(view.StartTerminator,curve.TMin,false,out curve.TMin)
                ||view.EndTerminator.CertifiedAffine&&!AffineSplineTerminator.TryParameter(view.EndTerminator,curve.TMax,true,out curve.TMax))
            {Curves.Free(curveSlot);return AlgorithmStatus.NotConverged;}
            var allocated = AllocateTag(EntityClass.Curve, PoolKind.Curve, curveSlot, curve.Header.Generation);
            if (allocated <= 0)
            {
                Curves.Free(curveSlot);
                return AlgorithmStatus.WorkspaceTooSmall;
            }
            // Publication is the last fallible step. The dependency release
            // path protects shared supports while preserving borrowed lifetime.
            AcquireGeometryDependency(PoolKind.Surface, surface0);
            AcquireGeometryDependency(PoolKind.Surface, surface1);
            curve.DependenciesAcquired = 1;
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
        var prepare0 = TryPrepareICurveSupport(in surf0, out var support0);
        if (prepare0 != AlgorithmStatus.Success) return prepare0;
        var prepare1 = TryPrepareICurveSupport(in surf1, out var support1);
        if (prepare1 != AlgorithmStatus.Success) return prepare1;
        var terminatesAtStart=data.StartLimit.Type==LimitType.Terminator;
        var terminatesAtEnd=data.EndLimit.Type==LimitType.Terminator;
        var splineTerminator=(support0.IsBSurface||support1.IsBSurface)&&(terminatesAtStart||terminatesAtEnd);
        if(splineTerminator&&(terminatesAtStart==terminatesAtEnd||support0.IsBSurface==support1.IsBSurface
            ||(support0.IsBSurface?support1.Analytic.Kind:support0.Analytic.Kind)!=SurfaceClass.Plane))
            return AlgorithmStatus.Unsupported;

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
        var witnessDoubleCount = ((long)chartCount * sizeof(SampleWitness) + sizeof(double) - 1) / sizeof(double);
        if (witnessDoubleCount > int.MaxValue) return AlgorithmStatus.WorkspaceTooSmall;
        var witnessDoubles = scratch.Take((BufferCount)witnessDoubleCount);
        if (positionDoubles.Length < chartCount * 3
            || parameterScratch.Length < chartCount
            || scaleScratch.Length < chartCount - 1
            || chordDoubles.Length < (chartCount - 1) * 3
            || tangentDoubles.Length < chartCount * 3
            || witnessDoubles.Length < witnessDoubleCount)
            return AlgorithmStatus.WorkspaceTooSmall;

        var positions = MemoryMarshal.Cast<double, KernelVector3>(positionDoubles);
        var tangents = MemoryMarshal.Cast<double, KernelVector3>(tangentDoubles);
        var chords = MemoryMarshal.Cast<double, KernelVector3>(chordDoubles);
        var witnesses = MemoryMarshal.Cast<double, SampleWitness>(witnessDoubles)[..chartCount];
        var localScale = Math.Max(support0.LengthScale, support1.LengthScale);
        var pointOnSurfaceTolerance = 1e-8 * localScale;

        for (BufferOffset i = 0; i < chartCount; i++)
        {
            var o = chartOffset + i * 3;
            positions[i] = Vector(hvecs[o], hvecs[o + 1], hvecs[o + 2]);
            if (!TryReadChartUv(in data, i, out var u0, out var v0, out var u1, out var v1))
                return AlgorithmStatus.InvalidInput;
            // Native terminal charts can omit the analytic plane's witness
            // as a zero pair. Its inverse is injective; keep the spline UV.
            if(splineTerminator&&!support0.IsBSurface&&u0==0&&v0==0)u0=v0=double.NaN;
            if(splineTerminator&&!support1.IsBSurface&&u1==0&&v1==0)u1=v1=double.NaN;
            var uv0Status = PrepareChartUv(in support0, in positions[i], pointOnSurfaceTolerance,
                ref u0, ref v0, out var hasUv0);
            if (uv0Status != AlgorithmStatus.Success) return uv0Status;
            var uv1Status = PrepareChartUv(in support1, in positions[i], pointOnSurfaceTolerance,
                ref u1, ref v1, out var hasUv1);
            if (uv1Status != AlgorithmStatus.Success) return uv1Status;
            witnesses[i] = new SampleWitness(u0, v0, u1, v1, 0,
                (byte)((hasUv0 ? SampleWitness.HasUv0 : 0) | (hasUv1 ? SampleWitness.HasUv1 : 0)));
            if (!TryChartTangent(in support0, surf0.Sense, u0, v0,
                    in support1, surf1.Sense, u1, v1, in positions[i], out tangents[i]))
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
        if(splineTerminator)
        {
            var end=terminatesAtEnd;var at=end?chartCount-1:0;var prior=end?at-1:1;
            var limit=end?endTerminator:startTerminator;
            if(!AffineSplineTerminator.Certify(support0,support1,limit,positions[at],witnesses[at],witnesses[prior]))
                return AlgorithmStatus.Unsupported;
            limit=new TerminatorLimit(limit.TermUse,limit.Endpoint,limit.BranchPoint,true);
            if(!AffineSplineTerminator.TryParameter(limit,parameterScratch[at],end,out _))return AlgorithmStatus.NotConverged;
            if(end)endTerminator=limit;else startTerminator=limit;
        }

        view = new ICurveView(support0, surf0.Sense, support1, surf1.Sense,
            positions, parameterScratch, scaleScratch, chords, witnesses,
            hasStart ? startTerminator : default,
            hasEnd ? endTerminator : default);
        return AlgorithmStatus.Success;
    }

    private static AlgorithmStatus TryPrepareICurveSupport(in SurfaceRecord surface,
        out ICurveSupportView support)
    {
        support = default;
        if (TryPrepareSurface(in surface, out var analytic))
        {
            support = new ICurveSupportView(in analytic);
            return AlgorithmStatus.Success;
        }
        if (surface.Class != SurfaceClass.BSurface || !BSurfaceDataStore.IsAlive(surface.DataIndex))
            return AlgorithmStatus.Unsupported;
        var bspline = GetBSurfaceView(in BSurfaceDataStore[surface.DataIndex]);
        var workspaceSize = BSurfaceEvaluation.WorkspaceSize(bspline.UDegree, bspline.VDegree, 2, 2);
        if (workspaceSize <= 0 || workspaceSize > int.MaxValue)
            return AlgorithmStatus.WorkspaceTooSmall;
        var workspace = CommandScratch.Current.Take((BufferCount)workspaceSize);
        if (workspace.Length < workspaceSize) return AlgorithmStatus.WorkspaceTooSmall;
        support = new ICurveSupportView(bspline, workspace);
        return AlgorithmStatus.Success;
    }

    private static ReadOnlySpan<double> HvecSpan(in ICurveData data)
    {
        var block = DereferenceBlock(data.HvecBlock);
        if (block == null || data.HvecCount <= 0) return ReadOnlySpan<double>.Empty;
        return new ReadOnlySpan<double>((double*)block, data.HvecCount * 3);
    }

    private static bool TryReadChartUv(in ICurveData data, BufferOffset chartIndex,
        out double u0, out double v0, out double u1, out double v1)
    {
        u0 = v0 = u1 = v1 = double.NaN;
        var stride = UvStrideOf(data.UvType);
        if (stride <= 0) return true;
        var block = DereferenceBlock(data.UvValueBlock);
        if (block == null || data.UvValueCount < (chartIndex + 1) * stride) return false;
        var values = new ReadOnlySpan<double>((double*)block, data.UvValueCount);
        var row = (data.StartLimit.Type == LimitType.Terminator ? 1 : 0) + chartIndex;
        var offset = row * stride;
        if (offset + stride > values.Length) return false;
        switch (data.UvType)
        {
            case IntersectionUvType.First:
                u0 = values[offset]; v0 = values[offset + 1];
                return true;
            case IntersectionUvType.Second:
                u1 = values[offset]; v1 = values[offset + 1];
                return true;
            case IntersectionUvType.Both:
                u0 = values[offset]; v0 = values[offset + 1];
                u1 = values[offset + 2]; v1 = values[offset + 3];
                return true;
            default:
                return false;
        }
    }

    private static AlgorithmStatus PrepareChartUv(in ICurveSupportView support,
        in KernelVector3 point, double tolerance, ref double u, ref double v, out bool hasUv)
    {
        hasUv = double.IsFinite(u) && double.IsFinite(v);
        if (!hasUv && !(double.IsNaN(u) && double.IsNaN(v)))
            return AlgorithmStatus.InvalidInput;
        if (support.IsBSurface && !hasUv)
            return AlgorithmStatus.Unsupported;
        if (!support.IsBSurface
            && (AnalyticImplicitEvaluation.GeometricDeviation(in support.Analytic, in point, out var deviation)
                != AlgorithmStatus.Success || deviation > tolerance))
            return AlgorithmStatus.InvalidInput;
        if (!hasUv)
        {
            hasUv = ICurveSupportEvaluation.TryResolveUv(in support, in point, false, 0, 0, out u, out v);
            return AlgorithmStatus.Success;
        }
        if (!SurfaceDerivativeLayout.TryCreate(0, 0, out var layout))
            return AlgorithmStatus.InvalidInput;
        Span<KernelVector3> value = stackalloc KernelVector3[1];
        if (ICurveSupportEvaluation.Evaluate(in support, u, v, in layout, value) != AlgorithmStatus.Success)
            return AlgorithmStatus.InvalidInput;
        var delta = Vector(value[0].X - point.X, value[0].Y - point.Y, value[0].Z - point.Z);
        return Math.Sqrt(Dot(delta, delta)) <= tolerance ? AlgorithmStatus.Success : AlgorithmStatus.InvalidInput;
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
    /// Unit chart tangent T = sense₀·n₀ × sense₁·n₁ (§5.1). B-surface normals
    /// come from the validated chart UV; surface sense flips each normal.
    /// </summary>
    private static bool TryChartTangent(in ICurveSupportView s0, KernelSense sense0, double u0, double v0,
        in ICurveSupportView s1, KernelSense sense1, double u1, double v1,
        in KernelVector3 point, out KernelVector3 tangent)
    {
        tangent = default;
        if (!ICurveSupportEvaluation.TryNormal(in s0, u0, v0, in point, out var normal0)
            || !ICurveSupportEvaluation.TryNormal(in s1, u1, v1, in point, out var normal1))
            return false;
        var n0 = Scale(normal0, SenseSign(sense0));
        var n1 = Scale(normal1, SenseSign(sense1));
        var cross = EvaluationMath.Cross(n0, n1);
        var normSq = Dot(cross, cross);
        if (!(normSq > 1e-30)) return false;
        tangent = Scale(cross, 1.0 / Math.Sqrt(normSq));
        return IsFinite(tangent);
    }

    private static double SenseSign(KernelSense sense)
        => sense == ParasolidConstants.PK_TOPOL_sense_negative_c ? -1.0 : 1.0;
}
