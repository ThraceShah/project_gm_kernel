using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A defining-geometry component belongs to one part. Moving an unattached
    // component into a body must include all of its free roots; otherwise PK
    // reports bad_shared_entity rather than silently adopting an outside owner.
    private static int ValidateGeometryAttachmentParts(PoolKind topologyPool, BufferCount count,
        EntityTag* topology, GeomTag* geometry)
    {
        if (topologyPool == PoolKind.Vertex) return 0;
        Span<GeomTag> dependencies = stackalloc GeomTag[3];
        var needsGraph = false;
        for (var i = 0; i < count; i++)
        {
            var record = TagRec(geometry[i]); var pool = (PoolKind)record.Pool;
            needsGraph |= GeometricOwnerCount(pool, record.Slot) != 0 || GetConstructionBody(geometry[i]) >= 0
                || GetGeometryDependencyTags(pool, record.Slot, dependencies) != 0;
        }
        if (!needsGraph) return 0;
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
            var selected = new ReadOnlySpan<GeomTag>(geometry, count);
            for (var i = 0; i < count; i++)
            {
                var error = CollectGeometryPart(geometry[i], component, out var used, out var part);
                if (error != 0) return error;
                var body = TopologyBody(topologyPool, TagRec(topology[i]).Slot);
                if (part >= 0 && part != body) return ParasolidConstants.PK_ERROR_has_parent;
                for (var j = 0; j < count; j++)
                    if (component[..used].Contains(geometry[j]) && TopologyBody(topologyPool, TagRec(topology[j]).Slot) != body)
                        return part >= 0 ? ParasolidConstants.PK_ERROR_has_parent : ParasolidConstants.PK_ERROR_bad_shared_entity;
                if (part >= 0) continue;
                for (var j = 0; j < used; j++)
                {
                    var record = TagRec(component[j]); var pool = (PoolKind)record.Pool;
                    if (GeometryOwnerCount(pool, record.Slot) == 0 && GeometricOwnerCount(pool, record.Slot) == 0
                        && !selected.Contains(component[j])) return ParasolidConstants.PK_ERROR_bad_shared_entity;
                }
            }
            return 0;
        }
        finally
        {
            CommandScratchReturn(workspace);
            if (ownsScratch) ReleaseCommandScratch(State.Session);
        }
    }

    private static int CollectGeometryPart(GeomTag geometry, Span<GeomTag> component,
        out BufferCount count, out BodySlot part)
    {
        count = 0; part = -1;
        if (!AddGeometryPartTag(geometry, component, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
        Span<GeomTag> dependencies = stackalloc GeomTag[3];
        for (var head = 0; head < count; head++)
        {
            var tag = component[head];
            if (!State.Session->Tags.TryResolve(tag, CurrentSessionId, out var record)) return ParasolidConstants.PK_ERROR_unknown_class;
            var pool = (PoolKind)record.Pool;
            if (pool is not (PoolKind.Curve or PoolKind.Surface)) return ParasolidConstants.PK_ERROR_unknown_class;
            var partition = PoolPartitionOf(pool, record.Slot);
            var body = GeometryOwnerCount(pool, record.Slot) != 0 ? GeometryBody(pool, record.Slot) : GetConstructionBody(tag);
            if (body >= 0)
            {
                if (part >= 0 && part != body) return ParasolidConstants.PK_ERROR_has_parent;
                part = body;
            }
            var n = GetGeometryDependencyTags(pool, record.Slot, dependencies);
            for (var j = 0; j < n; j++)
                if (!AddGeometryPartTag(dependencies[j], component, ref count)) return ParasolidConstants.PK_ERROR_memory_full;
            // Reverse owners are not a separate allocated index. The stable
            // pool scan is restricted to this partition and keeps core paths
            // allocation-free even when several owners share one dependency.
            for (var slot = 0; slot < Curves.AllocatedCount; slot++)
            {
                if (!Curves.IsAlive(slot) || Curves[slot].Header.Partition != partition) continue;
                n = GetGeometryDependencyTags(PoolKind.Curve, slot, dependencies);
                if (dependencies[..n].Contains(tag) && !AddGeometryPartTag(Curves[slot].Header.Tag, component, ref count))
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
            for (var slot = 0; slot < Surfaces.AllocatedCount; slot++)
            {
                if (!Surfaces.IsAlive(slot) || Surfaces[slot].Header.Partition != partition) continue;
                n = GetGeometryDependencyTags(PoolKind.Surface, slot, dependencies);
                if (dependencies[..n].Contains(tag) && !AddGeometryPartTag(Surfaces[slot].Header.Tag, component, ref count))
                    return ParasolidConstants.PK_ERROR_memory_full;
            }
            if (pool != PoolKind.Curve) continue;
            for (var slot = 0; slot < Fins.AllocatedCount; slot++)
            {
                if (!Fins.IsAlive(slot) || Fins[slot].Header.Partition != partition) continue;
                ref readonly var fin = ref Fins[slot];
                if (fin.CurveReferenceAcquired == 0 || fin.Curve != tag || fin.Edge < 0) continue;
                body = Edges[fin.Edge].Body;
                if (part >= 0 && part != body) return ParasolidConstants.PK_ERROR_has_parent;
                part = body;
            }
        }
        return 0;
    }

    private static bool AddGeometryPartTag(GeomTag tag, Span<GeomTag> tags, ref BufferCount count)
    {
        if (tag <= 0 || tags[..count].Contains(tag)) return true;
        if (count == tags.Length) return false;
        tags[count++] = tag;
        return true;
    }
}
