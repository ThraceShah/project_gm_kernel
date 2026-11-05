using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_SURF_parameterise_vector")]
    public static int PK_SURF_parameterise_vector(SurfTag surface, PK_VECTOR_s point, PK_UV_s* uv)
        => KernelRuntime.SurfParameteriseVector(surface, point, uv);

    [UnmanagedCallersOnly(EntryPoint = "PK_CURVE_parameterise_vector")]
    public static int PK_CURVE_parameterise_vector(CurveTag curve, PK_VECTOR_s point, double* parameter)
        => KernelRuntime.CurveParameteriseVector(curve, point, parameter);
}
