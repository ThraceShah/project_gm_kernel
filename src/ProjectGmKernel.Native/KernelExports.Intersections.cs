using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint="PK_CURVE_intersect_curve")]
    public static int PK_CURVE_intersect_curve(CurveTag first,PK_INTERVAL_s firstInterval,CurveTag second,PK_INTERVAL_s secondInterval,
        PK_CURVE_intersect_curve_o_s* options,BufferCount* count,PK_VECTOR_s** points,double** firstParameters,double** secondParameters,int** kinds)
        =>KernelRuntime.CurveIntersectCurve(first,firstInterval,second,secondInterval,options,count,points,firstParameters,secondParameters,kinds);

    [UnmanagedCallersOnly(EntryPoint="PK_SURF_intersect_curve")]
    public static int PK_SURF_intersect_curve(SurfTag surface,CurveTag curve,PK_INTERVAL_s interval,
        PK_SURF_intersect_curve_o_s* options,BufferCount* count,PK_VECTOR_s** points,PK_UV_s** parameters,double** curveParameters,int** kinds)
        =>KernelRuntime.SurfIntersectCurve(surface,curve,interval,options,count,points,parameters,curveParameters,kinds);

    [UnmanagedCallersOnly(EntryPoint="PK_SURF_intersect_surf")]
    public static int PK_SURF_intersect_surf(SurfTag first,SurfTag second,PK_SURF_intersect_surf_o_s* options,
        BufferCount* pointCount,PK_VECTOR_s** points,BufferCount* curveCount,CurveTag** curves,PK_INTERVAL_s** intervals,int** kinds)
        =>KernelRuntime.SurfIntersectSurf(first,second,options,pointCount,points,curveCount,curves,intervals,kinds);
}
