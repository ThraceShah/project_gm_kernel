using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_boolean")]
    public static int PK_BODY_boolean(EntityTag target, int count, EntityTag* tools, PK_BODY_boolean_o_s* options, int* resultCount, EntityTag** bodies)
        => KernelRuntime.BodyBoolean(target, count, tools, options, resultCount, bodies);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_boolean_2")]
    public static int PK_BODY_boolean_2(EntityTag target, int count, EntityTag* tools, PK_BODY_boolean_o_s* options, PK_TOPOL_track_r_s* tracking, PK_boolean_r_s* result)
        => KernelRuntime.BodyBoolean2(target, count, tools, options, tracking, result);

    [UnmanagedCallersOnly(EntryPoint = "PK_boolean_r_f")]
    public static int PK_boolean_r_f(PK_boolean_r_s* result)
        => KernelRuntime.BooleanResultFree(result);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_imprint_plane")]
    public static int PK_BODY_imprint_plane(EntityTag body, SurfTag plane, double tolerance, int* count, EntityTag** edges)
        => KernelRuntime.BodyImprintPlane(body, plane, tolerance, count, edges);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_imprint_plane_2")]
    public static int PK_BODY_imprint_plane_2(EntityTag body, SurfTag plane, PK_BODY_imprint_plane_o_s* options, PK_TOPOL_track_r_s* tracking)
        => KernelRuntime.BodyImprintPlane2(body, plane, options, tracking);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_trim")]
    public static int PK_BODY_trim(EntityTag body, int edgeCount, EntityTag* edges, int faceCount, EntityTag* faces, byte keep)
        => KernelRuntime.BodyTrim(body, edgeCount, edges, faceCount, faces, keep);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_contains_vector")]
    public static int PK_BODY_contains_vector(EntityTag body, PK_VECTOR_s point, int* enclosure, EntityTag* topology)
        => KernelRuntime.BodyContainsVector(body, point, enclosure, topology);

    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_section_with_surf")]
    public static int PK_BODY_section_with_surf(EntityTag body, SurfTag surface, PK_BODY_section_o_s* options, PK_section_r_s* result)
        => KernelRuntime.BodySectionWithSurf(body, surface, options, result);

    [UnmanagedCallersOnly(EntryPoint = "PK_section_r_f")]
    public static int PK_section_r_f(PK_section_r_s* result)
        => KernelRuntime.SectionResultFree(result);
}
