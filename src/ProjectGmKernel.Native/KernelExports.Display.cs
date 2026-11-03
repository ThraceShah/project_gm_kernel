using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PGM_BODY_export_display")]
    public static int PGM_BODY_export_display(EntityTag body,PgmDisplaySnapshot* snapshot)
        => XtWriter.CaptureNativeDisplay(body,snapshot);

    [UnmanagedCallersOnly(EntryPoint = "PGM_DISPLAY_snapshot_free")]
    public static int PGM_DISPLAY_snapshot_free(PgmDisplaySnapshot* snapshot)
    {
        if(snapshot==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        var error=snapshot->Xt.bytes==null?0:KernelRuntime.MemoryFree(snapshot->Xt.bytes);
        var second=snapshot->Bindings==null?0:KernelRuntime.MemoryFree(snapshot->Bindings);
        *snapshot=default;return error!=0?error:second;
    }

    [UnmanagedCallersOnly(EntryPoint = "PGM_DISPLAY_validate_pick")]
    public static int PGM_DISPLAY_validate_pick(KernelSessionId sessionGeneration,EntityTag body,EntityGeneration bodyGeneration,
        EntityTag entity,EntityGeneration entityGeneration,KernelClassCode entityClass,KernelLogical* valid)
    {
        if(valid==null)return ParasolidConstants.PK_ERROR_bad_field_number;
        var binding=new XtWriter.DisplayBinding(body,entity,entityClass,0,entityGeneration);
        *valid=XtWriter.IsCurrentDisplayBinding(binding,sessionGeneration,bodyGeneration)?(byte)1:(byte)0;
        return 0;
    }
}
