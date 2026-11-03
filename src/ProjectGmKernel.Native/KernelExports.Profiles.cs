using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_CURVE_make_wire_body")]
    public static int PK_CURVE_make_wire_body(CurveTag curve, PK_INTERVAL_s bounds, EntityTag* body)
        => KernelRuntime.CurveMakeWireBody(curve, bounds, body);

    [UnmanagedCallersOnly(EntryPoint = "PK_CURVE_make_wire_body_2")]
    public static int PK_CURVE_make_wire_body_2(int count, CurveTag* curves, PK_INTERVAL_s* bounds,
        PK_CURVE_make_wire_body_o_s* options, EntityTag* body, int* edgeCount, EntityTag** edges, int** indices)
        => KernelRuntime.CurveMakeWireBody2(count, curves, bounds, options, body, edgeCount, edges, indices);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_create_sheet_planar")]
    public static int PK_BODY_create_sheet_planar(int count, PK_VECTOR_s* vectors,
        PK_BODY_create_sheet_planar_o_s* options, EntityTag* body)
        => KernelRuntime.BodyCreateSheetPlanar(count, vectors, options, body);

    [UnmanagedCallersOnly(EntryPoint = "PK_EDGE_make_faces_from_wire")]
    public static int PK_EDGE_make_faces_from_wire(int count, EntityTag* edges, byte* senses, int* sharedLoop, EntityTag* faces)
        => KernelRuntime.EdgeMakeFacesFromWire(count, edges, senses, sharedLoop, faces);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_extrude")]
    public static int PK_BODY_extrude(EntityTag profile, PK_VECTOR_s path, PK_BODY_extrude_o_s* options,
        EntityTag* body, PK_TOPOL_track_r_s* tracking, PK_TOPOL_local_r_s* results)
        => KernelRuntime.BodyExtrude(profile, path, options, body, tracking, results);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_spin")]
    public static int PK_BODY_spin(EntityTag body, PK_AXIS1_sf_s* axis, double angle, byte localCheck,
        int* count, EntityTag** laterals, EntityTag** bases, int* checkResult)
        => KernelRuntime.BodySpin(body, axis, angle, localCheck, count, laterals, bases, checkResult);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_check")]
    public static int PK_BODY_check(EntityTag body,PK_BODY_check_o_s* options,int* count,PK_check_fault_s** faults)
        => KernelRuntime.BodyCheck(body,options,count,faults);

    [UnmanagedCallersOnly(EntryPoint = "PK_TOPOL_track_r_f")]
    public static int PK_TOPOL_track_r_f(PK_TOPOL_track_r_s* result)
        => KernelRuntime.TopolTrackResultFree(result);

    [UnmanagedCallersOnly(EntryPoint = "PK_TOPOL_local_r_f")]
    public static int PK_TOPOL_local_r_f(PK_TOPOL_local_r_s* result)
        => KernelRuntime.TopolLocalResultFree(result);

}
