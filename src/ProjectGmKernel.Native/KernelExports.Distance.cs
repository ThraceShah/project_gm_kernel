using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint="PK_GEOM_range_vector")]
    public static int PK_GEOM_range_vector(GeomTag geometry,PK_VECTOR_s point,PK_GEOM_range_vector_o_s* options,PK_range_result_t* status,PK_range_1_r_s* result)
        =>KernelRuntime.GeomRangeVector(geometry,point,options,status,result);

    [UnmanagedCallersOnly(EntryPoint="PK_GEOM_range")]
    public static int PK_GEOM_range(GeomTag first,GeomTag second,PK_GEOM_range_o_s* options,PK_range_result_t* status,PK_range_2_r_s* result)
        =>KernelRuntime.GeomRange(first,second,options,status,result);
}
