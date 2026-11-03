using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint="PK_EDGE_set_blend_constant")]
    public static int PK_EDGE_set_blend_constant(int count,EntityTag* edges,double radius,
        PK_EDGE_set_blend_constant_o_s* options,int* blendCount,EntityTag** blendEdges)
        =>KernelRuntime.EdgeSetBlendConstant(count,edges,radius,options,blendCount,blendEdges);

    [UnmanagedCallersOnly(EntryPoint="PK_EDGE_set_blend_chamfer")]
    public static int PK_EDGE_set_blend_chamfer(int count,EntityTag* edges,double range2,double range1,
        EntityTag* firstFaces,PK_EDGE_set_blend_chamfer_o_s* options,int* blendCount,EntityTag** blendEdges)
        =>KernelRuntime.EdgeSetBlendChamfer(count,edges,range2,range1,firstFaces,options,blendCount,blendEdges);

    [UnmanagedCallersOnly(EntryPoint="PK_BODY_fix_blends")]
    public static int PK_BODY_fix_blends(EntityTag body,PK_BODY_fix_blends_o_s* options,int* blendCount,
        EntityTag** blends,PK_FACE_array_s** unders,int** topols,int* fault,EntityTag* faultEdge,EntityTag* faultTopol)
        =>KernelRuntime.BodyFixBlends(body,options,blendCount,blends,unders,topols,fault,faultEdge,faultTopol);

    [UnmanagedCallersOnly(EntryPoint="PK_BODY_offset")]
    public static int PK_BODY_offset(EntityTag body,double offset,double tolerance,byte check)
        =>KernelRuntime.BodyOffset(body,offset,tolerance,check);

    [UnmanagedCallersOnly(EntryPoint="PK_BODY_offset_2")]
    public static int PK_BODY_offset_2(EntityTag body,double offset,double tolerance,PK_BODY_offset_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
        =>KernelRuntime.BodyOffset2(body,offset,tolerance,options,tracking,results);

    [UnmanagedCallersOnly(EntryPoint="PK_FACE_offset")]
    public static int PK_FACE_offset(int count,EntityTag* faces,double* offsets,double tolerance,byte check)
        =>KernelRuntime.FaceOffset(count,faces,offsets,tolerance,check);

    [UnmanagedCallersOnly(EntryPoint="PK_FACE_offset_2")]
    public static int PK_FACE_offset_2(int count,EntityTag* faces,double* offsets,double tolerance,
        PK_FACE_offset_o_s* options,PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
        =>KernelRuntime.FaceOffset2(count,faces,offsets,tolerance,options,tracking,results);

    [UnmanagedCallersOnly(EntryPoint="PK_BODY_hollow")]
    public static int PK_BODY_hollow(EntityTag body,double offset,double tolerance,byte check,
        int* count,EntityTag** originalFaces,EntityTag** newFaces)
        =>KernelRuntime.BodyHollow(body,offset,tolerance,check,count,originalFaces,newFaces);

    [UnmanagedCallersOnly(EntryPoint="PK_BODY_hollow_2")]
    public static int PK_BODY_hollow_2(EntityTag body,double offset,double tolerance,PK_BODY_hollow_o_s* options,
        PK_TOPOL_track_r_s* tracking,PK_TOPOL_local_r_s* results)
        =>KernelRuntime.BodyHollow2(body,offset,tolerance,options,tracking,results);
}
