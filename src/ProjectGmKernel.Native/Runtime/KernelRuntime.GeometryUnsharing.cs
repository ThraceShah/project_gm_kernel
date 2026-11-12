using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Keep the adopted defining closure's identities. Free geometry outside
    // that closure receives copies of any dependencies that now belong to it.
    private static int UnshareSheetDefiningGeometry(GeomTag surface)
    {
        var workspace = CommandScratchBorrow(Math.Min(8192, Math.Max(128, NextTagValue)));
        if (workspace.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var component = MemoryMarshal.Cast<double, GeomTag>(workspace);
            var error = CollectGeometryPart(surface, component, out var count, out _);
            if (error != 0) return error;
            var boundaryCount = PartitionBoundaryGeometry(component[..count]);
            if (boundaryCount == count) return 0;
            var copyWorkspace = CommandScratchBorrow(Math.Min(8192, Math.Max(128, NextTagValue)));
            if (copyWorkspace.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
            try
            {
                var copies = new EntityCopyContext { Pairs = MemoryMarshal.Cast<double, EntityCopyPair>(copyWorkspace) };
                Span<GeomTag> dependencies = stackalloc GeomTag[3], replacements = stackalloc GeomTag[3];
                for (var i = boundaryCount; i < count; i++)
                {
                    var tag = component[i]; var owner = TagRec(tag); var pool = (PoolKind)owner.Pool;
                    var n = GetGeometryDependencyTags(pool, owner.Slot, dependencies);
                    dependencies[..n].CopyTo(replacements);
                    var changed = false;
                    for (var d = 0; d < n; d++)
                    {
                        if (!component[..boundaryCount].Contains(dependencies[d])) continue;
                        error = CloneEntity(dependencies[d], ref copies, 0, out replacements[d]);
                        if (error != 0) return error;
                        changed = true;
                    }
                    if (!changed) continue;
                    if (GeometryOwnerCount(pool, owner.Slot) != 0 || GetConstructionBody(tag) >= 0)
                        return ParasolidConstants.PK_ERROR_bad_shared_entity;
                    if (!SnapshotGeometry(pool, owner.Slot)) return ParasolidConstants.PK_ERROR_memory_full;
                    for (var d = 0; d < n; d++)
                    {
                        if (dependencies[d] == replacements[d]) continue;
                        var old = TagRec(dependencies[d]); var copy = TagRec(replacements[d]);
                        if (!SnapshotGeometry((PoolKind)old.Pool, old.Slot) || !SnapshotGeometry((PoolKind)copy.Pool, copy.Slot))
                            return ParasolidConstants.PK_ERROR_memory_full;
                        error = CheckGeometryDependency((PoolKind)copy.Pool, copy.Slot); if (error != 0) return error;
                        if (!ReplaceDefiningGeometry(pool, owner.Slot, d, replacements[d])) return ParasolidConstants.PK_ERROR_memory_full;
                        AcquireGeometryDependency((PoolKind)copy.Pool, copy.Slot);
                        // Even a runtime borrower must release this new copy
                        // when its last reference leaves; no free input owns it.
                        if ((PoolKind)copy.Pool == PoolKind.Surface) Surfaces[copy.Slot].GeometricDeletePending = 1;
                        ReleaseGeometryDependency(dependencies[d], deleteOrphan: false);
                    }
                }
                return 0;
            }
            finally { CommandScratchReturn(copyWorkspace); }
        }
        finally { CommandScratchReturn(workspace); }
    }

    private static bool ReplaceDefiningGeometry(PoolKind pool, DataSlot slot, BufferOffset index, GeomTag replacement)
    {
        var session = State.Session;
        if (pool == PoolKind.Curve)
        {
            var curve = Curves[slot];
            switch (curve.Class)
            {
                case CurveClass.TRCurve:
                    ref var trim = ref TrCurveDataPool[curve.DataIndex]; if (!session->TrySnapshot(ref trim)) return false;
                    trim.BasisCurveTag = replacement; return true;
                case CurveClass.SPCurve:
                    ref var sp = ref SpCurveDataPool[curve.DataIndex]; if (!session->TrySnapshot(ref sp)) return false;
                    if (index == 0) sp.SurfTag = replacement; else sp.BCurveTag = replacement; return true;
                case CurveClass.ICurve:
                    ref var intersection = ref ICurveDataPool[curve.DataIndex]; if (!session->TrySnapshot(ref intersection)) return false;
                    if (index == 0) intersection.Surface0Tag = replacement; else intersection.Surface1Tag = replacement; return true;
            }
        }
        else if (pool == PoolKind.Surface)
        {
            var surface = Surfaces[slot];
            switch (surface.Class)
            {
                case SurfaceClass.BlendSurface:
                    ref var blend = ref BlendedEdgeDataPool[surface.DataIndex]; if (!session->TrySnapshot(ref blend)) return false;
                    if (index == 0) blend.Surface0Tag = replacement; else if (index == 1) blend.Surface1Tag = replacement; else blend.SpineTag = replacement; return true;
                case SurfaceClass.Offset:
                    ref var offset = ref OffsetDataPool[surface.DataIndex]; if (!session->TrySnapshot(ref offset)) return false;
                    offset.BaseSurfTag = replacement; return true;
                case SurfaceClass.Swept:
                    ref var swept = ref SweptDataPool[surface.DataIndex]; if (!session->TrySnapshot(ref swept)) return false;
                    swept.SectionCurveTag = replacement; return true;
                case SurfaceClass.Spun:
                    ref var spun = ref SpunDataPool[surface.DataIndex]; if (!session->TrySnapshot(ref spun)) return false;
                    spun.ProfileCurveTag = replacement; return true;
            }
        }
        return false;
    }
}
