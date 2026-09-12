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
        blocks->Free(DereferenceBlock(data.HvecBlock));
        blocks->Free(DereferenceBlock(data.UvValueBlock));
        ICurveDataPool.Free(dataIndex);
    }

    internal static void FreeBlendEdgeData(DataSlot dataIndex)
    {
        var blocks = &State.Session->Blocks;
        ref var data = ref BlendedEdgeDataPool[dataIndex];
        blocks->Free(DereferenceBlock(data.HvecBlock));
        BlendedEdgeDataPool.Free(dataIndex);
    }
}
