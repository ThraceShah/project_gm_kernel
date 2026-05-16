using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

// ── Geometry Class Enums ──────────────────────────────────────────

/// <summary>
/// Discriminant for CurveRecord. Maps to PK_CLASS_t values for curves.
/// </summary>
internal enum CurveClass : int
{
    None = 0,
    Line = 3001,
    Circle = 3002,
    Ellipse = 3003,
    BCurve = 3005,
    ICurve = 3006,
    FCurve = 3007,
    SPCurve = 3008,
    TRCurve = 3009,
    CPCurve = 3010,
}

/// <summary>
/// Discriminant for SurfaceRecord. Maps to PK_CLASS_t values for surfaces.
/// </summary>
internal enum SurfaceClass : int
{
    None = 0,
    Plane = 4001,
    Cylinder = 4002,
    Cone = 4003,
    Sphere = 4004,
    Torus = 4005,
    BSurface = 4006,
    Offset = 4007,
    FSurface = 4008,
    Swept = 4009,
    Spun = 4010,
    BlendSurface = 4011,
}

// ── B-Spline Enums ────────────────────────────────────────────────

internal enum BCurveForm : int
{
    Unknown = 0,
    Linear = 1,
    Circular = 2,
    Elliptic = 3,
    Parabolic = 4,
    Hyperbolic = 5,
    Polynomial = 6,
}

internal enum BSurfaceForm : int
{
    Unknown = 0,
    Planar = 1,
    Spherical = 2,
    Cylindrical = 3,
    Conical = 4,
    Toroidal = 5,
    Polynomial = 6,
}

internal enum KnotType : int
{
    Unknown = 0,
    Uniform = 1,
    QuasiUniform = 2,
    PiecewiseBezier = 3,
    NonUniform = 4,
}

internal enum SelfIntersect : int
{
    No = 0,
    Yes = 1,
    Maybe = 2,
}

// ── Kernel-Internal Geometry Structs ─────────────────────────────

/// <summary>
/// 3D vector / point. Kernel-internal equivalent of PK_VECTOR_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KernelVector3
{
    public double X;
    public double Y;
    public double Z;
}

// ── Simple Geometry Records ───────────────────────────────────────

/// <summary>
/// Geometric vector: a direction/magnitude in 3D space.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VectorRecord
{
    public RecordHeader Header;
    public KernelVector3 Value;
}

/// <summary>
/// Geometric point: a position in 3D space.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PointRecord
{
    public RecordHeader Header;
    public KernelVector3 Position;
    public VertexSlot OwnerVertex;
    public PointTag PrevInBody;
    public PointTag NextInBody;
}

/// <summary>
/// Homogeneous transformation matrix (4x4).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct TransformRecord
{
    public RecordHeader Header;
    public fixed double Matrix[16];
}

// ── Curve / Surface Records ───────────────────────────────────────

/// <summary>
/// Curve handle record. Discriminant tells which pool holds the specific data.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CurveRecord
{
    public RecordHeader Header;
    public CurveClass Class;
    public DataSlot DataIndex;    // index into the type-specific data pool
    public double TMin;           // parameter interval start
    public double TMax;           // parameter interval end
    public KernelSense Sense;
    public EdgeSlot OwnerEdge;
    public CurveTag PrevInBody;
    public CurveTag NextInBody;
}

/// <summary>
/// Surface handle record. Discriminant tells which pool holds the specific data.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SurfaceRecord
{
    public RecordHeader Header;
    public SurfaceClass Class;
    public DataSlot DataIndex;    // index into the type-specific data pool
    public double UMin;           // u parameter interval start
    public double UMax;           // u parameter interval end
    public double VMin;           // v parameter interval start
    public double VMax;           // v parameter interval end
    public FaceSlot OwnerFace;
    public SurfTag PrevInBody;
    public SurfTag NextInBody;
}

// ── Analytic Curve Data ───────────────────────────────────────────

/// <summary>
/// Line data: a point and a direction.
/// Maps from PK_LINE_sf_s (PK_AXIS1_sf_t basis_set).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LineData
{
    public RecordHeader Header;
    public double LocationX, LocationY, LocationZ;
    public double AxisX, AxisY, AxisZ;
}

/// <summary>
/// Circle data: center, axis, ref direction, radius.
/// Maps from PK_CIRCLE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CircleData
{
    public RecordHeader Header;
    public double CenterX, CenterY, CenterZ;
    public double AxisX, AxisY, AxisZ;
    public double RefDirX, RefDirY, RefDirZ;
    public double Radius;
}

/// <summary>
/// Ellipse data: center, axis, ref direction, two radii.
/// Maps from PK_ELLIPSE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct EllipseData
{
    public RecordHeader Header;
    public double CenterX, CenterY, CenterZ;
    public double AxisX, AxisY, AxisZ;
    public double RefDirX, RefDirY, RefDirZ;
    public double R1;
    public double R2;
}

// ── Analytic Surface Data ─────────────────────────────────────────

/// <summary>
/// Plane data: point, normal, ref direction.
/// Maps from PK_PLANE_sf_s (PK_AXIS2_sf_t basis_set).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PlaneData
{
    public RecordHeader Header;
    public double LocationX, LocationY, LocationZ;
    public double NormalX, NormalY, NormalZ;
    public double RefDirX, RefDirY, RefDirZ;
}

/// <summary>
/// Cylinder data: axis frame + radius.
/// Maps from PK_CYL_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CylinderData
{
    public RecordHeader Header;
    public double LocationX, LocationY, LocationZ;
    public double AxisX, AxisY, AxisZ;
    public double RefDirX, RefDirY, RefDirZ;
    public double Radius;
}

/// <summary>
/// Cone data: axis frame + radius + semi-angle.
/// Maps from PK_CONE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ConeData
{
    public RecordHeader Header;
    public double LocationX, LocationY, LocationZ;
    public double AxisX, AxisY, AxisZ;
    public double RefDirX, RefDirY, RefDirZ;
    public double Radius;
    public double SemiAngle;
}

/// <summary>
/// Sphere data: center + radius.
/// Maps from PK_SPHERE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SphereData
{
    public RecordHeader Header;
    public double CenterX, CenterY, CenterZ;
    public double AxisX, AxisY, AxisZ;
    public double RefDirX, RefDirY, RefDirZ;
    public double Radius;
}

/// <summary>
/// Torus data: axis frame + major/minor radii.
/// Maps from PK_TORUS_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TorusData
{
    public RecordHeader Header;
    public double LocationX, LocationY, LocationZ;
    public double AxisX, AxisY, AxisZ;
    public double RefDirX, RefDirY, RefDirZ;
    public double MajorRadius;
    public double MinorRadius;
}

// ── B-Spline Data ─────────────────────────────────────────────────

/// <summary>
/// B-spline curve metadata. Actual pole/knot data stored in flat arenas.
/// Maps from PK_BCURVE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BCurveData
{
    public int Degree;
    public int NVertices;         // total number of control points
    public int VertexDim;         // dimension of each vertex (3 or 4 if rational)
    public KernelLogical IsRational;
    public KernelBCurveForm Form;
    public int NKnots;
    public KernelKnotType KnotType;
    public KernelLogical IsPeriodic;
    public KernelLogical IsClosed;
    public KernelSelfIntersect SelfIntersecting;
    // Indices into flat data arenas (CurveVertices, CurveKnots, CurveKnotMults)
    public DataSlot VertexOffset;   // offset into CurveVertices arena
    public DataSlot KnotOffset;     // offset into CurveKnots arena
    public DataSlot KnotMultOffset; // offset into CurveKnotMults arena
}

/// <summary>
/// B-spline surface metadata. Actual pole/knot data stored in flat arenas.
/// Maps from PK_BSURF_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BSurfaceData
{
    public int UDegree;
    public int VDegree;
    public int NUVertices;
    public int NVVertices;
    public int VertexDim;
    public KernelLogical IsRational;
    public KernelBSurfaceForm Form;
    public int NUKnots;
    public int NVKnots;
    public KernelKnotType UKnotType;
    public KernelKnotType VKnotType;
    public KernelLogical IsUPeriodic;
    public KernelLogical IsVPeriodic;
    public KernelLogical IsUClosed;
    public KernelLogical IsVClosed;
    public KernelSelfIntersect SelfIntersecting;
    public KernelConvexity Convexity;
    // Indices into flat data arenas
    public DataSlot VertexOffset;     // offset into SurfaceVertices arena
    public DataSlot UKnotOffset;      // offset into SurfaceUKnots arena
    public DataSlot VKnotOffset;      // offset into SurfaceVKnots arena
    public DataSlot UKnotMultOffset;  // offset into SurfaceUKnotMults arena
    public DataSlot VKnotMultOffset;  // offset into SurfaceVKnotMults arena
}

// ── Other Curve/Surface Data ──────────────────────────────────────

/// <summary>
/// Intersection curve data. Stores the two intersecting surfaces and their ranges.
/// Maps from PK_ICURVE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ICurveData
{
    public SurfTag Surf1Tag;
    public SurfTag Surf2Tag;
    public double U1Min, U1Max, V1Min, V1Max;
    public double U2Min, U2Max, V2Min, V2Max;
}

/// <summary>
/// Trimmed curve data. References a base curve with trim intervals.
/// Maps from PK_TRCURVE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TrimmedCurveData
{
    public CurveTag BaseCurveTag;
    public double TMin;
    public double TMax;
}

/// <summary>
/// Curve on parametric surface (p-curve) data.
/// Maps from PK_CPCURVE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CPCurveData
{
    public SurfTag SurfTag;
    // The p-curve is itself a 2D B-spline in UV space
    public int Degree;
    public int NVertices;
    public int VertexDim;
    public KernelLogical IsRational;
    public int NKnots;
    public DataSlot VertexOffset;
    public DataSlot KnotOffset;
    public DataSlot KnotMultOffset;
}

/// <summary>
/// Spun curve data.
/// Maps from PK_SPCURVE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SPCurveData
{
    public CurveTag ProfileCurveTag;
    public double LocationX, LocationY, LocationZ;
    public double AxisX, AxisY, AxisZ;
}

/// <summary>
/// Foreign (custom) curve data. Stores the foreign type identifier.
/// Maps from PK_FCURVE_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FCurveData
{
    public int ForeignType;
    public nint ForeignData;      // opaque pointer to foreign data
}

/// <summary>
/// Foreign (custom) surface data.
/// Maps from PK_FSURF_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FSurfaceData
{
    public int ForeignType;
    public nint ForeignData;
}

/// <summary>
/// Blend surface data.
/// Maps from PK_BLENDSF_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BlendSurfaceData
{
    public GeomTag Geom1Tag;
    public GeomTag Geom2Tag;
    public double Range1Min, Range1Max;
    public double Range2Min, Range2Max;
}

/// <summary>
/// Offset surface data.
/// Maps from PK_OFFSET_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OffsetData
{
    public SurfTag BaseSurfTag;
    public double Offset;
}

/// <summary>
/// Swept surface data.
/// Maps from PK_SWEPT_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SweptData
{
    public CurveTag ProfileCurveTag;
    public CurveTag SpineCurveTag;
}

/// <summary>
/// Spun surface data.
/// Maps from PK_SPUN_sf_s.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SpunData
{
    public CurveTag ProfileCurveTag;
    public double LocationX, LocationY, LocationZ;
    public double AxisX, AxisY, AxisZ;
}
