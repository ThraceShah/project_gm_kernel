using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int PointAsk(PointTag geometry, PK_POINT_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return PointAskImplementation(geometry, result);
        var command = new PointAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.PointAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int EllipseAsk(CurveTag geometry, PK_ELLIPSE_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return EllipseAskImplementation(geometry, result);
        var command = new EllipseAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.EllipseAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int TrCurveAsk(CurveTag geometry, PK_TRCURVE_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return TrCurveAskImplementation(geometry, result);
        var command = new TrCurveAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.TrCurveAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int SpCurveAsk(CurveTag geometry, PK_SPCURVE_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return SpCurveAskImplementation(geometry, result);
        var command = new SpCurveAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.SpCurveAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int BCurveAsk(CurveTag geometry, PK_BCURVE_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return BCurveAskImplementation(geometry, result);
        var command = new BCurveAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.BCurveAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int BSurfAsk(SurfTag geometry, PK_BSURF_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return BSurfAskImplementation(geometry, result);
        var command = new BSurfAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.BSurfAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int OffsetAsk(SurfTag geometry, PK_OFFSET_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return OffsetAskImplementation(geometry, result);
        var command = new OffsetAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.OffsetAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int SweptAsk(SurfTag geometry, PK_SWEPT_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return SweptAskImplementation(geometry, result);
        var command = new SweptAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.SweptAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int SpunAsk(SurfTag geometry, PK_SPUN_sf_s* result)
    {
        if (Dispatcher.IsExecuting) return SpunAskImplementation(geometry, result);
        var command = new SpunAskCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.SpunAsk, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int CurveAskInterval(CurveTag geometry, PK_INTERVAL_s* result)
    {
        if (Dispatcher.IsExecuting) return CurveAskIntervalImplementation(geometry, result);
        var command = new CurveAskIntervalCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.CurveAskInterval, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

    public static int SurfAskUvbox(SurfTag geometry, PK_UVBOX_s* result)
    {
        if (Dispatcher.IsExecuting) return SurfAskUvboxImplementation(geometry, result);
        var command = new SurfAskUvboxCommand { Geometry = geometry, Result = result };
        return Dispatch(ApiId.SurfAskUvbox, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, geometry);
    }

}
