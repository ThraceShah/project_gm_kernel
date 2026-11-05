using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint="PK_BLENDSF_ask")]
    public static int PK_BLENDSF_ask(SurfTag surface,PK_BLENDSF_sf_s* result)
        =>KernelRuntime.BlendSfAsk(surface,result);
    [UnmanagedCallersOnly(EntryPoint="PK_SURF_create_blend")]
    public static int PK_SURF_create_blend(GeomTag first,double firstRange,GeomTag second,double secondRange,
        PK_VECTOR_s start,PK_VECTOR_s end,PK_SURF_create_blend_o_s* options,SurfTag* surface)
        =>KernelRuntime.SurfCreateBlend(first,firstRange,second,secondRange,start,end,options,surface);
}
