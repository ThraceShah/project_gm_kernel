using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A mark retains payloads for undo, not the public liveness of defining
    // geometry whose last live owner was deleted. Prepare this finite closure
    // and all deletion capacity before changing topology.
    private static int CollectDeletionGeometry(ReadOnlySpan<EntityTag> entities, Span<GeomTag> geometry, out BufferCount count)
    {
        count = 0;
        foreach (var entity in entities)
        {
            var record = TagRec(entity); var pool = (PoolKind)record.Pool;
            if (pool is PoolKind.Curve or PoolKind.Surface)
            {
                if (!AddGeometryPartTag(entity, geometry, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
            }
            else if (pool == PoolKind.Body)
            {
                var body = Bodies[record.Slot]; var face = body.FirstFaceBody; var edge = body.FirstEdgeBody;
                for (var f = 0; f < body.FaceCountBody; f++, face = Faces[face].NextInBody)
                    if (!AddGeometryPartTag(Faces[face].SurfTag, geometry, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
                for (var e = 0; e < body.EdgeCountBody; e++, edge = Edges[edge].NextInBody)
                {
                    if (!AddGeometryPartTag(Edges[edge].CurveTag, geometry, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
                    var fin = Edges[edge].FirstFinEdge;
                    for (var f = 0; f < Edges[edge].FinCount; f++, fin = Fins[fin].NextOfEdge)
                        if (Fins[fin].CurveReferenceAcquired != 0 && !AddGeometryPartTag(Fins[fin].Curve, geometry, ref count))
                            return ParasolidConstants.PK_ERROR_memory_full;
                }
                var tag = body.FirstConstructionSurface >= 0 ? Surfaces[body.FirstConstructionSurface].Header.Tag : 0;
                for (var s = 0; s < body.ConstructionSurfaceCount; s++, tag = GetConstructionNext(tag))
                    if (!AddGeometryPartTag(tag, geometry, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
                tag = body.FirstConstructionCurve >= 0 ? Curves[body.FirstConstructionCurve].Header.Tag : 0;
                for (var c = 0; c < body.ConstructionCurveCount; c++, tag = GetConstructionNext(tag))
                    if (!AddGeometryPartTag(tag, geometry, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
            }
        }
        Span<GeomTag> dependencies = stackalloc GeomTag[3];
        for (var head = 0; head < count; head++)
        {
            var record = TagRec(geometry[head]); var pool = (PoolKind)record.Pool;
            var borrowed = pool == PoolKind.Curve && Curves[record.Slot].Class == CurveClass.ICurve
                && ICurveDataPool[Curves[record.Slot].DataIndex].OwnsSupportSurfaces == 0;
            var n = GetGeometryDependencyTags(pool, record.Slot, dependencies);
            for (var d = 0; d < n; d++)
            {
                // Runtime query curves borrow independent supports. A former
                // boundary owner can separately leave a deletion obligation.
                if (borrowed && Surfaces[GetSurfaceSlotByTag(dependencies[d])].GeometricDeletePending == 0) continue;
                if (!AddGeometryPartTag(dependencies[d], geometry, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
            }
        }
        return 0;
    }

    private static void RetireUnownedDefiningGeometry(ReadOnlySpan<GeomTag> geometry)
    {
        // Every pass retires at least one record. Revisit a shared descendant
        // after all of its parent branches have been retired.
        bool changed;
        do
        {
            changed = false;
            foreach (var tag in geometry)
            {
                if (!State.Session->Tags.TryResolve(tag, CurrentSessionId, out var record)) continue;
                var pool = (PoolKind)record.Pool;
                if (GeometryOwnerCount(pool, record.Slot) != 0 || HasLiveDefiningOwner(tag)) continue;
                DeleteEntitySingle(State.Session, pool, record.Slot, tag); changed = true;
            }
        } while (changed);
    }

    private static bool HasLiveDefiningOwner(GeomTag tag)
    {
        var record = TagRec(tag); var pool = (PoolKind)record.Pool;
        if (GeometricOwnerCount(pool, record.Slot) == 0) return false;
        Span<GeomTag> dependencies = stackalloc GeomTag[3]; var partition = PoolPartitionOf(pool, record.Slot);
        for (var slot = 0; slot < Curves.AllocatedCount; slot++)
        {
            if (!Curves.IsAlive(slot) || Curves[slot].Header.Partition != partition) continue;
            var count = GetGeometryDependencyTags(PoolKind.Curve, slot, dependencies); if (dependencies[..count].Contains(tag)) return true;
        }
        for (var slot = 0; slot < Surfaces.AllocatedCount; slot++)
        {
            if (!Surfaces.IsAlive(slot) || Surfaces[slot].Header.Partition != partition) continue;
            var count = GetGeometryDependencyTags(PoolKind.Surface, slot, dependencies); if (dependencies[..count].Contains(tag)) return true;
        }
        for (var slot = 0; slot < Fins.AllocatedCount; slot++)
            if (Fins.IsAlive(slot) && Fins[slot].CurveReferenceAcquired != 0 && Fins[slot].Curve == tag) return true;
        return false;
    }
}
