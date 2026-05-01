using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_SESSION_start")]
    public static int PK_SESSION_start(PK_SESSION_start_o_s* options)
    {
        return KernelRuntime.Dispatch(
            ApiId.SessionStart,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            () => KernelRuntime.SessionStart(options));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SESSION_stop")]
    public static int PK_SESSION_stop()
    {
        return KernelRuntime.Dispatch(
            ApiId.SessionStop,
            ConcurrencyKind.Exclusive,
            AccessKind.SessionControl,
            KernelRuntime.SessionStop);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_POINT_create")]
    public static int PK_POINT_create(PK_POINT_sf_s* pointSf, int* point)
    {
        return KernelRuntime.Dispatch(
            ApiId.PointCreate,
            ConcurrencyKind.Exclusive,
            AccessKind.GlobalWrite,
            () => KernelRuntime.PointCreate(pointSf, point));
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_ask_class")]
    public static int PK_ENTITY_ask_class(int entity, int* @class)
    {
        return KernelRuntime.Dispatch(
            ApiId.EntityAskClass,
            ConcurrencyKind.Concurrent,
            AccessKind.ReadOnly,
            () => KernelRuntime.EntityAskClass(entity, @class));
    }
}
