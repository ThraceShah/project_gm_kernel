namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Procedural geometry (icurve, blend family) metadata lives in paged
    // pools; hull-vector and UV payloads live in independently released
    // variable-length blocks owned by the record. Free functions mirror the
    // B-curve pattern: release every owned block, then the metadata slot.
    // Records without blocks (blend vertex/overlap/bound) need no payload
    // release beyond the pool slot.

    internal static void FreeICurveData(DataSlot dataIndex)
    {
        var blocks = &State.Session->Blocks;
        ref var data = ref ICurveDataPool[dataIndex];
        var owned0 = data.OwnsSupportSurfaces != 0 ? data.Surface0Tag : 0;
        var owned1 = data.OwnsSupportSurfaces != 0 ? data.Surface1Tag : 0;
        blocks->Free(DereferenceBlock(data.HvecBlock));
        blocks->Free(DereferenceBlock(data.UvValueBlock));
        ICurveDataPool.Free(dataIndex);
        ReleaseImportedICurveSupport(owned0);
        if (owned1 != owned0) ReleaseImportedICurveSupport(owned1);
    }

    private static void ReleaseImportedICurveSupport(SurfTag tag)
    {
        var slot = GetSurfaceSlotByTag(tag);
        // A caller may subsequently attach an imported support to a face;
        // the topology then owns its lifetime. Ordinary decoded supports are
        // borrowed and never reach this path.
        if (slot >= 0 && Surfaces.IsAlive(slot) && Surfaces[slot].OwnerCount == 0)
        {
            // This callback runs when the curve payload is physically freed,
            // including undo replay and deferred-delete commit. Do not append
            // a fresh EntityDelete transaction while replaying that journal.
            FinalReleaseEntity(PoolKind.Surface, slot, tag, knownAlive: true);
        }
    }

    internal static void FreeBlendEdgeData(DataSlot dataIndex)
    {
        var blocks = &State.Session->Blocks;
        ref var data = ref BlendedEdgeDataPool[dataIndex];
        blocks->Free(DereferenceBlock(data.HvecBlock));
        BlendedEdgeDataPool.Free(dataIndex);
    }
}
