using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_create_spline")]
    public static int PK_BCURVE_create_spline(BufferCount count, PK_VECTOR_s* positions,
        PK_BCURVE_create_spline_o_s* options, CurveTag* curve, int* status, BufferCount* faultCount, int** faults)
        => KernelRuntime.BCurveCreateSpline(count, positions, options, curve, status, faultCount, faults);
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_create_spline_2")]
    public static int PK_BCURVE_create_spline_2(BufferCount count, PK_VECTOR_s* positions,
        PK_BCURVE_create_spline_2_o_s* options, PK_BCURVE_spline_r_s* result)
        => KernelRuntime.BCurveCreateSpline2(count, positions, options, result);
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_spline_r_f")]
    public static int PK_BCURVE_spline_r_f(PK_BCURVE_spline_r_s* result) => KernelRuntime.BCurveSplineResultFree(result);
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_make_matched")]
    public static int PK_BCURVE_make_matched(BufferCount count, CurveTag* curves,
        PK_BCURVE_make_matched_o_s* options, CurveTag* matched)
        => KernelRuntime.BCurveMakeMatched(count, curves, options, matched);
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_make_bsurf_lofted")]
    public static int PK_BCURVE_make_bsurf_lofted(BufferCount count, CurveTag* curves,
        PK_BCURVE_make_bsurf_lofted_o_s* options, SurfTag* surface)
        => KernelRuntime.BCurveMakeBSurfLofted(count, curves, options, surface);
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_sweep")]
    public static int PK_BCURVE_sweep(CurveTag curve, PK_VECTOR_s path, SurfTag* surface)
        => KernelRuntime.BCurveSweep(curve, path, surface);
    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_spin")]
    public static int PK_BCURVE_spin(CurveTag curve, PK_AXIS1_sf_s* axis, double angle, SurfTag* surface)
        => KernelRuntime.BCurveSpin(curve, axis, angle, surface);
    [UnmanagedCallersOnly(EntryPoint = "PK_BSURF_make_bcurve_u_isoparam")]
    public static int PK_BSURF_make_bcurve_u_isoparam(SurfTag surface, double parameter, CurveTag* curve)
        => KernelRuntime.BSurfMakeBCurveUIsoparam(surface, parameter, curve);
    [UnmanagedCallersOnly(EntryPoint = "PK_BSURF_make_bcurve_v_isoparam")]
    public static int PK_BSURF_make_bcurve_v_isoparam(SurfTag surface, double parameter, CurveTag* curve)
        => KernelRuntime.BSurfMakeBCurveVIsoparam(surface, parameter, curve);
    [UnmanagedCallersOnly(EntryPoint = "PK_BSURF_create_splinewise")]
    public static int PK_BSURF_create_splinewise(PK_BSURF_splinewise_sf_s* definition, SurfTag* surface)
        => KernelRuntime.BSurfCreateSplinewise(definition, surface);
    [UnmanagedCallersOnly(EntryPoint = "PK_SURF_make_sheet_body")]
    public static int PK_SURF_make_sheet_body(SurfTag surface, PK_UVBOX_s box, BodyTag* body)
        => KernelRuntime.SurfMakeSheetBody(surface, box, body);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_make_lofted_body")]
    public static int PK_BODY_make_lofted_body(BufferCount count, BodyTag* profiles, EntityTag* starts,
        PK_BODY_make_lofted_body_o_s* options, PK_BODY_tracked_loft_s* result)
        => KernelRuntime.BodyMakeLoftedBody(count, profiles, starts, options, result);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_tracked_loft_r_f")]
    public static int PK_BODY_tracked_loft_r_f(PK_BODY_tracked_loft_s* result) => KernelRuntime.BodyTrackedLoftResultFree(result);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_make_swept_body")]
    public static int PK_BODY_make_swept_body(BodyTag profile, BodyTag path, EntityTag start,
        PK_BODY_make_swept_body_o_s* options, PK_BODY_tracked_sweep_r_s* result)
        => KernelRuntime.BodyMakeSweptBody(profile, path, start, options, result);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_tracked_sweep_r_f")]
    public static int PK_BODY_tracked_sweep_r_f(PK_BODY_tracked_sweep_r_s* result) => KernelRuntime.BodyTrackedSweepResultFree(result);
    [UnmanagedCallersOnly(EntryPoint = "PK_BODY_sew_bodies")]
    public static int PK_BODY_sew_bodies(BufferCount count, BodyTag* bodies, double gap, PK_BODY_sew_bodies_o_s* options,
        BufferCount* sewnCount, BodyTag** sewn, BufferCount* unsewnCount, BodyTag** unsewn,
        BufferCount* problemCount, PK_BODY_problem_group_s** problems)
        => KernelRuntime.BodySewBodies(count, bodies, gap, options, sewnCount, sewn, unsewnCount, unsewn, problemCount, problems);
}
