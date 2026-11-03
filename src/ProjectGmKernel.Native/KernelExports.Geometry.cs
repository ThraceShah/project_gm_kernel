using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native;

internal static unsafe partial class KernelExports
{
    [UnmanagedCallersOnly(EntryPoint = "PK_PART_ask_geoms")]
    public static int PK_PART_ask_geoms(EntityTag part, BufferCount* count, GeomTag** geometries)
    {
        var command = new PartAskGeomsCommand { Part = part, Count = count, Geometries = geometries };
        return KernelRuntime.Dispatch(ApiId.PartAskGeoms, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, part);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_POINT_ask")]
    public static int PK_POINT_ask(PointTag geometry, PK_POINT_sf_s* result)
    {
        var command = new PointAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.PointAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ELLIPSE_ask")]
    public static int PK_ELLIPSE_ask(CurveTag geometry, PK_ELLIPSE_sf_s* result)
    {
        var command = new EllipseAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.EllipseAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_TRCURVE_ask")]
    public static int PK_TRCURVE_ask(CurveTag geometry, PK_TRCURVE_sf_s* result)
    {
        var command = new TrCurveAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.TrCurveAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SPCURVE_ask")]
    public static int PK_SPCURVE_ask(CurveTag geometry, PK_SPCURVE_sf_s* result)
    {
        var command = new SpCurveAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.SpCurveAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BCURVE_ask")]
    public static int PK_BCURVE_ask(CurveTag geometry, PK_BCURVE_sf_s* result)
    {
        var command = new BCurveAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.BCurveAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BSURF_ask")]
    public static int PK_BSURF_ask(SurfTag geometry, PK_BSURF_sf_s* result)
    {
        var command = new BSurfAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.BSurfAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_OFFSET_ask")]
    public static int PK_OFFSET_ask(SurfTag geometry, PK_OFFSET_sf_s* result)
    {
        var command = new OffsetAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.OffsetAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SWEPT_ask")]
    public static int PK_SWEPT_ask(SurfTag geometry, PK_SWEPT_sf_s* result)
    {
        var command = new SweptAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.SweptAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SPUN_ask")]
    public static int PK_SPUN_ask(SurfTag geometry, PK_SPUN_sf_s* result)
    {
        var command = new SpunAskCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.SpunAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_CURVE_ask_interval")]
    public static int PK_CURVE_ask_interval(CurveTag geometry, PK_INTERVAL_s* result)
    {
        var command = new CurveAskIntervalCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.CurveAskInterval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SURF_ask_uvbox")]
    public static int PK_SURF_ask_uvbox(SurfTag geometry, PK_UVBOX_s* result)
    {
        var command = new SurfAskUvboxCommand { Geometry = geometry, Result = result };
        return KernelRuntime.Dispatch(ApiId.SurfAskUvbox, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, command.Geometry);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_ELLIPSE_create")]
    public static int PK_ELLIPSE_create(PK_ELLIPSE_sf_s* definition, CurveTag* geometry)
    {
        var command = new EllipseCreateCommand { EllipseSf = definition, Ellipse = geometry };
        return KernelRuntime.Dispatch(ApiId.EllipseCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SPCURVE_create")]
    public static int PK_SPCURVE_create(PK_SPCURVE_sf_s* definition, CurveTag* geometry)
    {
        var command = new SpCurveCreateCommand { SpCurveSf = definition, SpCurve = geometry };
        return KernelRuntime.Dispatch(ApiId.SpCurveCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_BSURF_create")]
    public static int PK_BSURF_create(PK_BSURF_sf_s* definition, SurfTag* geometry)
    {
        var command = new BSurfCreateCommand { BSurfSf = definition, BSurf = geometry };
        return KernelRuntime.Dispatch(ApiId.BSurfCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_OFFSET_create")]
    public static int PK_OFFSET_create(PK_OFFSET_sf_s* definition, SurfTag* geometry)
    {
        var command = new OffsetCreateCommand { OffsetSf = definition, Offset = geometry };
        return KernelRuntime.Dispatch(ApiId.OffsetCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SWEPT_create")]
    public static int PK_SWEPT_create(PK_SWEPT_sf_s* definition, SurfTag* geometry)
    {
        var command = new SweptCreateCommand { SweptSf = definition, Swept = geometry };
        return KernelRuntime.Dispatch(ApiId.SweptCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    [UnmanagedCallersOnly(EntryPoint = "PK_SPUN_create")]
    public static int PK_SPUN_create(PK_SPUN_sf_s* definition, SurfTag* geometry)
    {
        var command = new SpunCreateCommand { SpunSf = definition, Spun = geometry };
        return KernelRuntime.Dispatch(ApiId.SpunCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    // The SDK exposes trimmed-curve construction through this legacy ABI,
    // not through a PK_TRCURVE_create symbol.
    [UnmanagedCallersOnly(EntryPoint = "CRTRCU")]
    public static void CRTRCU(CurveTag* basis, double* start, double* end, CurveTag* trimmed, int* error)
    {
        if (error == null) return;
        if (trimmed != null) *trimmed = 0;
        if (basis == null || start == null || end == null || trimmed == null)
        {
            *error = ParasolidConstants.PK_ERROR_bad_field_number;
            return;
        }
        // CRTRCU rejects a descending interval; internal XT reconstruction
        // retains its separate negatively-sensed trimmed-curve contract.
        if (!double.IsFinite(*start) || !double.IsFinite(*end) || *end <= *start)
        {
            *error = ParasolidConstants.PK_ERROR_bad_parameter;
            return;
        }
        var definition = new PK_TRCURVE_sf_s { basis_curve = *basis };
        definition.t_int.value[0] = *start;
        definition.t_int.value[1] = *end;
        var command = new TrCurveCreateCommand { TrCurveSf = &definition, TrCurve = trimmed };
        *error = KernelRuntime.Dispatch(ApiId.TrCurveCreate, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }
}
