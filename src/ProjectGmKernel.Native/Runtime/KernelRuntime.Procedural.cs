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
        if(data.ProgramBlock>0)blocks->Free(DereferenceBlock(data.ProgramBlock));
        if(data.ContactChartBlock>0)blocks->Free(DereferenceBlock(data.ContactChartBlock));
        ICurveDataPool.Free(dataIndex);
        // Bound definitions release their references through the common
        // geometry graph. Unbound materializers clean up their own inputs;
        // freeing payload storage must not introduce another owner policy.
    }

    internal static void FreeBlendEdgeData(DataSlot dataIndex)
    {
        var blocks = &State.Session->Blocks;
        ref var data = ref BlendedEdgeDataPool[dataIndex];
        blocks->Free(DereferenceBlock(data.HvecBlock));
        BlendedEdgeDataPool.Free(dataIndex);
    }
}
