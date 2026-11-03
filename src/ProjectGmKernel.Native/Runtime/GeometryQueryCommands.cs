using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal unsafe struct PointAskCommand : IKernelCommand
{
    public PointTag Geometry;
    public PK_POINT_sf_s* Result;
    public int Execute() => KernelRuntime.PointAsk(Geometry, Result);
}

internal unsafe struct EllipseAskCommand : IKernelCommand
{
    public CurveTag Geometry;
    public PK_ELLIPSE_sf_s* Result;
    public int Execute() => KernelRuntime.EllipseAsk(Geometry, Result);
}

internal unsafe struct TrCurveAskCommand : IKernelCommand
{
    public CurveTag Geometry;
    public PK_TRCURVE_sf_s* Result;
    public int Execute() => KernelRuntime.TrCurveAsk(Geometry, Result);
}

internal unsafe struct SpCurveAskCommand : IKernelCommand
{
    public CurveTag Geometry;
    public PK_SPCURVE_sf_s* Result;
    public int Execute() => KernelRuntime.SpCurveAsk(Geometry, Result);
}

internal unsafe struct BCurveAskCommand : IKernelCommand
{
    public CurveTag Geometry;
    public PK_BCURVE_sf_s* Result;
    public int Execute() => KernelRuntime.BCurveAsk(Geometry, Result);
}

internal unsafe struct BSurfAskCommand : IKernelCommand
{
    public SurfTag Geometry;
    public PK_BSURF_sf_s* Result;
    public int Execute() => KernelRuntime.BSurfAsk(Geometry, Result);
}

internal unsafe struct OffsetAskCommand : IKernelCommand
{
    public SurfTag Geometry;
    public PK_OFFSET_sf_s* Result;
    public int Execute() => KernelRuntime.OffsetAsk(Geometry, Result);
}

internal unsafe struct SweptAskCommand : IKernelCommand
{
    public SurfTag Geometry;
    public PK_SWEPT_sf_s* Result;
    public int Execute() => KernelRuntime.SweptAsk(Geometry, Result);
}

internal unsafe struct SpunAskCommand : IKernelCommand
{
    public SurfTag Geometry;
    public PK_SPUN_sf_s* Result;
    public int Execute() => KernelRuntime.SpunAsk(Geometry, Result);
}

internal unsafe struct CurveAskIntervalCommand : IKernelCommand
{
    public CurveTag Geometry;
    public PK_INTERVAL_s* Result;
    public int Execute() => KernelRuntime.CurveAskInterval(Geometry, Result);
}

internal unsafe struct SurfAskUvboxCommand : IKernelCommand
{
    public SurfTag Geometry;
    public PK_UVBOX_s* Result;
    public int Execute() => KernelRuntime.SurfAskUvbox(Geometry, Result);
}
