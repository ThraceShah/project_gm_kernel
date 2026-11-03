using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int PartAskGeoms(EntityTag part, BufferCount* count, GeomTag** geometries)
    {
        if (!Dispatcher.IsExecuting)
        {
            var command = new PartAskGeomsCommand { Part = part, Count = count, Geometries = geometries };
            return Dispatch(ApiId.PartAskGeoms, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, part);
        }
        if (count == null || geometries == null) return ParasolidConstants.PK_ERROR_bad_field_number;
        *count = 0; *geometries = null;
        if (!TryResolveBodySlot(part, out var slot)) return ParasolidConstants.PK_ERROR_unknown_class;
        if (Xt.IsOpaque(slot) || Bodies[slot].ConstructionPointCount != 0) return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var body = ref Bodies[slot];
        var total = (long)body.ConstructionCurveCount + body.ConstructionSurfaceCount;
        if (total == 0) return 0;
        if (total > int.MaxValue) return ParasolidConstants.PK_ERROR_memory_full;
        var values = AllocateReturnSlice((int)total);
        if (values == null) return ParasolidConstants.PK_ERROR_memory_full;
        var index = 0;
        var current = body.FirstConstructionSurface >= 0 ? Surfaces[body.FirstConstructionSurface].Header.Tag : 0;
        for (var i = 0; i < body.ConstructionSurfaceCount; i++, current = GetConstructionNext(current)) values[index++] = current;
        current = body.FirstConstructionCurve >= 0 ? Curves[body.FirstConstructionCurve].Header.Tag : 0;
        for (var i = 0; i < body.ConstructionCurveCount; i++, current = GetConstructionNext(current)) values[index++] = current;
        *count = index; *geometries = values;
        return 0;
    }
}

internal unsafe struct PartAskGeomsCommand : IKernelCommand
{
    internal EntityTag Part;
    internal BufferCount* Count;
    internal GeomTag** Geometries;
    public int Execute() => KernelRuntime.PartAskGeoms(Part, Count, Geometries);
}
