using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint="PK_BCURVE_add_knot")]
    public static int PK_BCURVE_add_knot(CurveTag curve,double knot)=>KernelRuntime.BCurveAddKnot(curve,knot);
    [UnmanagedCallersOnly(EntryPoint="PK_BCURVE_raise_degree")]
    public static int PK_BCURVE_raise_degree(CurveTag curve,SplineDegree increment,PK_BCURVE_raise_degree_o_s* options)=>KernelRuntime.BCurveRaiseDegree(curve,increment,options);
    [UnmanagedCallersOnly(EntryPoint="PK_BCURVE_reparameterise")]
    public static int PK_BCURVE_reparameterise(CurveTag curve,PK_INTERVAL_s interval,PK_BCURVE_reparameterise_o_s* options)=>KernelRuntime.BCurveReparameterise(curve,interval,options);
    [UnmanagedCallersOnly(EntryPoint="PK_BSURF_add_u_knot")]
    public static int PK_BSURF_add_u_knot(SurfTag surface,double knot)=>KernelRuntime.BSurfAddUKnot(surface,knot);
    [UnmanagedCallersOnly(EntryPoint="PK_BSURF_add_v_knot")]
    public static int PK_BSURF_add_v_knot(SurfTag surface,double knot)=>KernelRuntime.BSurfAddVKnot(surface,knot);
    [UnmanagedCallersOnly(EntryPoint="PK_BSURF_raise_degree")]
    public static int PK_BSURF_raise_degree(SurfTag surface,PK_PARAM_direction_t direction,SplineDegree increment,PK_BSURF_raise_degree_o_s* options)=>KernelRuntime.BSurfRaiseDegree(surface,direction,increment,options);
    [UnmanagedCallersOnly(EntryPoint="PK_BSURF_reparameterise")]
    public static int PK_BSURF_reparameterise(SurfTag surface,PK_BSURF_reparameterise_o_s* options)=>KernelRuntime.BSurfReparameterise(surface,options);
}
