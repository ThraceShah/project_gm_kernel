using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Construction chains contain the part's geometry outside the defining
    // closure of its topology. They have independent links from boundary data.
    internal static GeomTag GetConstructionNext(GeomTag geometry)
    {
        if (!State.Session->Tags.TryResolve(geometry, CurrentSessionId, out var record)) return 0;
        return (PoolKind)record.Pool switch
        {
            PoolKind.Curve => Curves[record.Slot].NextConstruction,
            PoolKind.Surface => Surfaces[record.Slot].NextConstruction,
            _ => 0,
        };
    }

    internal static BodySlot GetConstructionBody(GeomTag geometry)
    {
        if (!State.Session->Tags.TryResolve(geometry, CurrentSessionId, out var record)) return -1;
        var pool = (PoolKind)record.Pool;
        if (pool is not (PoolKind.Curve or PoolKind.Surface)) return -1;
        var body = ConstructionBodyTag(pool, record.Slot);
        return body > 0 && TryResolveBodySlot(body, out var slot) ? slot : -1;
    }

    internal static int TryRegisterConstructionGeometry(BodySlot body, GeomTag geometry)
    {
        if (!Bodies.IsAlive(body) || !State.Session->Tags.TryResolve(geometry, CurrentSessionId, out var record))
            return ParasolidConstants.PK_ERROR_unknown_class;
        var pool = (PoolKind)record.Pool;
        if (pool is not (PoolKind.Curve or PoolKind.Surface)) return ParasolidConstants.PK_ERROR_not_implemented;
        if (PoolPartitionOf(pool, record.Slot) != Bodies[body].Header.Partition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if (GeometryOwnerCount(pool, record.Slot) != 0)
            return GeometryBody(pool, record.Slot) == body ? 0 : ParasolidConstants.PK_ERROR_has_parent;
        // SP-curve parameter curves are floating 2D data, never BODY members.
        if (pool == PoolKind.Curve && Curves[record.Slot].Class == CurveClass.BCurve)
        {
            ref readonly var data = ref BCurveDataStore[Curves[record.Slot].DataIndex];
            if (data.VertexDim - (data.IsRational != 0 ? 1 : 0) == 2) return 0;
        }
        var previousBody = ConstructionBodyTag(pool, record.Slot);
        if (previousBody != 0) return previousBody == Bodies[body].Header.Tag ? 0 : ParasolidConstants.PK_ERROR_has_parent;
        if (ConstructionCount(body, pool) == int.MaxValue) return ParasolidConstants.PK_ERROR_memory_full;
        var last = ConstructionTail(body, pool);
        if (!State.Session->TrySnapshot(ref Bodies[body]) || !SnapshotGeometry(pool, record.Slot)
            || (last >= 0 && !SnapshotGeometry(pool, last))) return ParasolidConstants.PK_ERROR_memory_full;
        ConstructionBodyTag(pool, record.Slot) = Bodies[body].Header.Tag;
        ConstructionPrevious(pool, record.Slot) = last >= 0 ? TagOf(pool, last) : 0;
        ConstructionNext(pool, record.Slot) = 0;
        if (last >= 0) ConstructionNext(pool, last) = geometry;
        else ConstructionHead(body, pool) = record.Slot;
        ConstructionTail(body, pool) = record.Slot;
        ConstructionCount(body, pool)++;
        SessionMemoryOwner.DropBodyXt(body);
        return 0;
    }

    private static int TryAdoptDependentGeometry(GeomTag geometry)
    {
        var record = TagRec(geometry);
        Span<GeomTag> dependencies = stackalloc GeomTag[2];
        if (GeometricOwnerCount((PoolKind)record.Pool, record.Slot) == 0 && GetConstructionBody(geometry) < 0
            && GetGeometryDependencyTags((PoolKind)record.Pool, record.Slot, dependencies) == 0) return 0;
        var ownsScratch = EnsureCommandScratch();
        var workspace = CommandScratchBorrow(Math.Min(8192, Math.Max(128, NextTagValue)));
        if (workspace.IsEmpty)
        {
            if (ownsScratch) ReleaseCommandScratch(State.Session);
            return ParasolidConstants.PK_ERROR_memory_full;
        }
        try
        {
            var component = MemoryMarshal.Cast<double, GeomTag>(workspace);
            var error = CollectGeometryPart(geometry, component, out var count, out var body);
            if (error != 0 || body < 0) return error;
            // Geometry reached from attached curves/surfaces is boundary
            // defining data, even when it has no direct edge/face owner.
            // A reverse owner, such as a new SWEPT of an edge-owned curve,
            // remains construction geometry until that owner is attached.
            var boundaryCount = PartitionBoundaryGeometry(component[..count]);
            for (var i = 0; i < boundaryCount; i++)
            {
                var boundary = TagRec(component[i]);
                if (!TryUnregisterConstructionGeometry((PoolKind)boundary.Pool, boundary.Slot))
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
            for (var i = boundaryCount; i < count; i++)
            {
                error = TryRegisterConstructionGeometry(body, component[i]);
                if (error != 0) return error;
            }
            return 0;
        }
        finally
        {
            CommandScratchReturn(workspace);
            if (ownsScratch) ReleaseCommandScratch(State.Session);
        }
    }

    // Reorder the existing scratch span into boundary closure / construction
    // candidates. The growing prefix is its own breadth-first queue.
    private static BufferCount PartitionBoundaryGeometry(Span<GeomTag> component)
    {
        BufferCount boundaryCount = 0;
        for (var i = 0; i < component.Length; i++)
        {
            var tag = component[i]; var record = TagRec(tag);
            if (GeometryOwnerCount((PoolKind)record.Pool, record.Slot) != 0)
                IncludeBoundaryGeometry(tag, component, ref boundaryCount);
        }
        // Fin pcurves own a reference separately from edge/face owner counts.
        for (var slot = 0; slot < Fins.AllocatedCount; slot++)
        {
            if (!Fins.IsAlive(slot)) continue;
            ref readonly var fin = ref Fins[slot];
            if (fin.CurveReferenceAcquired != 0 && fin.Edge >= 0)
                IncludeBoundaryGeometry(fin.Curve, component, ref boundaryCount);
        }
        Span<GeomTag> dependencies = stackalloc GeomTag[2];
        for (var head = 0; head < boundaryCount; head++)
        {
            var record = TagRec(component[head]);
            var count = GetGeometryDependencyTags((PoolKind)record.Pool, record.Slot, dependencies);
            for (var i = 0; i < count; i++)
                IncludeBoundaryGeometry(dependencies[i], component, ref boundaryCount);
        }
        return boundaryCount;
    }

    private static void IncludeBoundaryGeometry(GeomTag tag, Span<GeomTag> component, ref BufferCount boundaryCount)
    {
        var index = component[boundaryCount..].IndexOf(tag);
        if (index < 0) return;
        index += boundaryCount;
        component[index] = component[boundaryCount];
        component[boundaryCount++] = tag;
    }

    private static bool TryUnregisterConstructionGeometry(PoolKind pool, DataSlot slot)
    {
        if (!SnapshotConstructionUnlink(pool, slot)) return false;
        UnlinkConstructionGeometry(pool, slot);
        return true;
    }

    private static bool SnapshotConstructionUnlink(PoolKind pool, DataSlot slot)
    {
        if (pool is not (PoolKind.Curve or PoolKind.Surface)) return true;
        var bodyTag = ConstructionBodyTag(pool, slot);
        if (bodyTag == 0) return true;
        if (!TryResolveBodySlot(bodyTag, out var body)) return false;
        var previous = ConstructionPrevious(pool, slot); var next = ConstructionNext(pool, slot);
        return State.Session->TrySnapshot(ref Bodies[body]) && SnapshotGeometry(pool, slot)
            && (previous == 0 || SnapshotGeometry(pool, TagRec(previous).Slot))
            && (next == 0 || SnapshotGeometry(pool, TagRec(next).Slot));
    }

    private static void UnlinkConstructionGeometry(PoolKind pool, DataSlot slot)
    {
        if (pool is not (PoolKind.Curve or PoolKind.Surface)) return;
        var bodyTag = ConstructionBodyTag(pool, slot);
        if (bodyTag == 0 || !TryResolveBodySlot(bodyTag, out var body)) return;
        var previous = ConstructionPrevious(pool, slot); var next = ConstructionNext(pool, slot);
        if (previous != 0) ConstructionNext(pool, TagRec(previous).Slot) = next;
        else ConstructionHead(body, pool) = next == 0 ? -1 : TagRec(next).Slot;
        if (next != 0) ConstructionPrevious(pool, TagRec(next).Slot) = previous;
        else ConstructionTail(body, pool) = previous == 0 ? -1 : TagRec(previous).Slot;
        ConstructionCount(body, pool)--;
        ConstructionBodyTag(pool, slot) = 0;
        ConstructionPrevious(pool, slot) = ConstructionNext(pool, slot) = 0;
        SessionMemoryOwner.DropBodyXt(body);
    }

    // Deletion preflights every construction-chain before-image before any
    // entity is destroyed, including the direct (unmarked) delete path.
    private static bool SnapshotConstructionDeletion(PoolKind pool, DataSlot slot)
    {
        if (pool != PoolKind.Body)
        {
            if (pool is not (PoolKind.Curve or PoolKind.Surface)) return true;
            var bodyTag = ConstructionBodyTag(pool, slot);
            return bodyTag == 0 || (TryResolveBodySlot(bodyTag, out var body) && SnapshotConstructionDeletion(PoolKind.Body, body));
        }
        if (Bodies[slot].ConstructionCurveCount == 0 && Bodies[slot].ConstructionSurfaceCount == 0) return true;
        if (!State.Session->TrySnapshot(ref Bodies[slot])) return false;
        var surface = Bodies[slot].FirstConstructionSurface;
        for (var i = 0; i < Bodies[slot].ConstructionSurfaceCount; i++)
        {
            if (!SnapshotGeometry(PoolKind.Surface, surface)) return false;
            var next = Surfaces[surface].NextConstruction;
            surface = next == 0 ? -1 : TagRec(next).Slot;
        }
        var curve = Bodies[slot].FirstConstructionCurve;
        for (var i = 0; i < Bodies[slot].ConstructionCurveCount; i++)
        {
            if (!SnapshotGeometry(PoolKind.Curve, curve)) return false;
            var next = Curves[curve].NextConstruction;
            curve = next == 0 ? -1 : TagRec(next).Slot;
        }
        return true;
    }

    private static ref EntityTag ConstructionBodyTag(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Curve) return ref Curves[slot].ConstructionBodyTag;
        return ref Surfaces[slot].ConstructionBodyTag;
    }
    private static ref GeomTag ConstructionPrevious(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Curve) return ref Curves[slot].PrevConstruction;
        return ref Surfaces[slot].PrevConstruction;
    }
    private static ref GeomTag ConstructionNext(PoolKind pool, DataSlot slot)
    {
        if (pool == PoolKind.Curve) return ref Curves[slot].NextConstruction;
        return ref Surfaces[slot].NextConstruction;
    }
    private static ref DataSlot ConstructionHead(BodySlot body, PoolKind pool)
    {
        if (pool == PoolKind.Curve) return ref Bodies[body].FirstConstructionCurve;
        return ref Bodies[body].FirstConstructionSurface;
    }
    private static ref DataSlot ConstructionTail(BodySlot body, PoolKind pool)
    {
        if (pool == PoolKind.Curve) return ref Bodies[body].LastConstructionCurve;
        return ref Bodies[body].LastConstructionSurface;
    }
    private static ref BufferCount ConstructionCount(BodySlot body, PoolKind pool)
    {
        if (pool == PoolKind.Curve) return ref Bodies[body].ConstructionCurveCount;
        return ref Bodies[body].ConstructionSurfaceCount;
    }
}
