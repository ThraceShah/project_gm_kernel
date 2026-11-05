using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static BufferCount GeometricOwnerCount(PoolKind pool, DataSlot slot)
        => pool == PoolKind.Curve ? Curves[slot].GeometricOwnerCount
            : pool == PoolKind.Surface ? Surfaces[slot].GeometricOwnerCount : 0;

    private static int CheckGeometryDependency(PoolKind pool, DataSlot slot)
    {
        if (PoolPartitionOf(pool, slot) != CurrentPartition)
            return ParasolidConstants.PK_ERROR_not_in_same_partition;
        return GeometricOwnerCount(pool, slot) == int.MaxValue ? ParasolidConstants.PK_ERROR_memory_full : 0;
    }

    // Called only after all fallible construction steps. Creation undo owns
    // the matching release, so acquiring the reference needs no allocation.
    private static void AcquireGeometryDependency(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Curve) Curves[slot].GeometricOwnerCount++;
        else Surfaces[slot].GeometricOwnerCount++;
    }

    /// <summary>Bind a pcurve while constructing a new fin in the current command.</summary>
    internal static int AcquireFinCurveReference(FinSlot finSlot, CurveTag curve)
    {
        var curveSlot = GetCurveSlotByTag(curve);
        if (!Fins.IsAlive(finSlot) || curveSlot < 0) return ParasolidConstants.PK_ERROR_unknown_class;
        ref var fin = ref Fins[finSlot];
        if (fin.CurveReferenceAcquired != 0) return ParasolidConstants.PK_ERROR_is_attached;
        var error = CheckGeometryDependency(PoolKind.Curve, curveSlot);
        if (error != 0) return error;
        if (fin.Header.Partition != Curves[curveSlot].Header.Partition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        AcquireGeometryDependency(PoolKind.Curve, curveSlot);
        fin.Curve = curve;
        fin.CurveReferenceAcquired = 1;
        return 0;
    }

    private static void ReleaseFinCurveReference(FinSlot slot, bool deleteOrphan)
    {
        ref var fin = ref Fins[slot];
        if (fin.CurveReferenceAcquired == 0) return;
        fin.CurveReferenceAcquired = 0;
        ReleaseGeometryDependency(fin.Curve, deleteOrphan);
    }

    private static void ReleaseGeometryDependency(GeomTag tag, bool deleteOrphan, bool ownsLifetime = true)
    {
        if (!State.Session->Tags.TryResolve(tag, CurrentSessionId, out var record)) return;
        var pool = (PoolKind)record.Pool;
        if (pool is not (PoolKind.Curve or PoolKind.Surface)) return;
        var slot = record.Slot;
        if (GeometricOwnerCount(pool, slot) <= 0) return;
        var releaseOrphan = deleteOrphan && ownsLifetime;
        if (pool == PoolKind.Curve) Curves[slot].GeometricOwnerCount--;
        else
        {
            ref var surface = ref Surfaces[slot];
            surface.GeometricOwnerCount--;
            // A decoded runtime ICurve borrows its supports. Imported/copy
            // curves own theirs, but may die before another borrower. Retain
            // that release obligation until the final live reference leaves.
            if (releaseOrphan) surface.GeometricDeletePending = 1;
            releaseOrphan = deleteOrphan && surface.GeometricDeletePending != 0;
            if (surface.GeometricOwnerCount == 0) surface.GeometricDeletePending = 0;
        }
        if (releaseOrphan && GeometricOwnerCount(pool, slot) == 0 && GeometryOwnerCount(pool, slot) == 0)
            FinalReleaseEntity(pool, slot, tag, knownAlive: true);
    }

    /// <summary>Direct defining geometry of the supported dependent classes.</summary>
    internal static BufferCount GetGeometryDependencyTags(PoolKind pool, DataSlot slot, Span<GeomTag> tags)
    {
        if (pool == PoolKind.Curve)
        {
            ref readonly var curve = ref Curves[slot];
            if (curve.Class == CurveClass.TRCurve && tags.Length >= 1)
            {
                tags[0] = TrCurveDataPool[curve.DataIndex].BasisCurveTag;
                return 1;
            }
            if (curve.Class == CurveClass.SPCurve && tags.Length >= 2)
            {
                ref readonly var data = ref SpCurveDataPool[curve.DataIndex];
                tags[0] = data.SurfTag; tags[1] = data.BCurveTag;
                return 2;
            }
            if (curve.Class == CurveClass.ICurve && tags.Length >= 2)
            {
                ref readonly var data = ref ICurveDataPool[curve.DataIndex];
                tags[0] = data.Surface0Tag; tags[1] = data.Surface1Tag;
                return 2;
            }
        }
        else if (pool == PoolKind.Surface && tags.Length >= 1)
        {
            ref readonly var surface = ref Surfaces[slot];
            switch (surface.Class)
            {
                case SurfaceClass.BlendSurface:
                    if(tags.Length<3)return 0;
                    ref readonly var blend=ref BlendedEdgeDataPool[surface.DataIndex];
                    tags[0]=blend.Surface0Tag;tags[1]=blend.Surface1Tag;tags[2]=blend.SpineTag;return 3;
                case SurfaceClass.Offset: tags[0] = OffsetDataPool[surface.DataIndex].BaseSurfTag; return 1;
                case SurfaceClass.Swept: tags[0] = SweptDataPool[surface.DataIndex].SectionCurveTag; return 1;
                case SurfaceClass.Spun: tags[0] = SpunDataPool[surface.DataIndex].ProfileCurveTag; return 1;
            }
        }
        return 0;
    }

    private static void ReleaseDefiningGeometry(PoolKind pool, DataSlot slot, bool deleteOrphans)
    {
        var acquired = pool == PoolKind.Curve ? Curves[slot].DependenciesAcquired : Surfaces[slot].DependenciesAcquired;
        if (acquired == 0) return;
        Span<GeomTag> tags = stackalloc GeomTag[3];
        var count = GetGeometryDependencyTags(pool, slot, tags);
        var ownsLifetime = pool != PoolKind.Curve || Curves[slot].Class != CurveClass.ICurve
            || ICurveDataPool[Curves[slot].DataIndex].OwnsSupportSurfaces != 0;
        // Clear before recursive release; the record may also appear in the
        // creation journal when a failed outer command unwinds.
        if (pool == PoolKind.Curve) Curves[slot].DependenciesAcquired = 0;
        else Surfaces[slot].DependenciesAcquired = 0;
        for (BufferOffset i = 0; i < count; i++) ReleaseGeometryDependency(tags[i], deleteOrphans, ownsLifetime);
    }
}
