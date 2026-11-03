using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_copy")]
    public static int PK_ENTITY_copy(EntityTag entity,EntityTag* copy)
        => KernelRuntime.EntityCopy(entity,copy);
    [UnmanagedCallersOnly(EntryPoint = "PK_ENTITY_copy_2")]
    public static int PK_ENTITY_copy_2(EntityTag entity,PK_ENTITY_copy_o_s* options,EntityTag* copy,PK_ENTITY_track_r_s* tracking)
        => KernelRuntime.EntityCopy2(entity,options,copy,tracking);
    [UnmanagedCallersOnly(EntryPoint = "PK_TRANSF_ask")]
    public static int PK_TRANSF_ask(TransfTag transform,PK_TRANSF_sf_s* form)
        => KernelRuntime.TransfAsk(transform,form);
    [UnmanagedCallersOnly(EntryPoint = "PK_GEOM_transform")]
    public static int PK_GEOM_transform(GeomTag geometry,TransfTag transform,double tolerance,GeomTag* output,KernelLogical* exact)
        => KernelRuntime.GeomTransform(geometry,transform,tolerance,output,exact);
    [UnmanagedCallersOnly(EntryPoint = "PK_GEOM_transform_2")]
    public static int PK_GEOM_transform_2(BufferCount count,GeomTag* geometries,TransfTag transform,PK_GEOM_transform_o_s* options,GeomTag* output,KernelLogical* exact)
        => KernelRuntime.GeomTransform2(count,geometries,transform,options,output,exact);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_transform")]
    public static int PK_BODY_transform(BodyTag body,TransfTag transform,double tolerance,BufferCount* count,GeomTag** replaced,KernelLogical** exact)
        => KernelRuntime.BodyTransform(body,transform,tolerance,count,replaced,exact);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_transform_2")]
    public static int PK_BODY_transform_2(BodyTag body,TransfTag transform,double tolerance,PK_BODY_transform_o_s* options,PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
        => KernelRuntime.BodyTransform2(body,transform,tolerance,options,tracking,results);
}
