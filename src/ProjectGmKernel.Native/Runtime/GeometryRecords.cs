using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

// ── Geometry Class Enums ──────────────────────────────────────────

/// <summary>
/// Discriminant for CurveRecord. Maps to PK_CLASS_t values for curves
/// (parasolid_tokens.h TYCU*): 3001-3009, no other curve classes exist.
/// </summary>
internal enum CurveClass : int
{
    None = 0,
    Line = 3001,     // PK_CLASS_line (TYCUST, straight)
    Circle = 3002,   // PK_CLASS_circle (TYCUCI)
    Ellipse = 3003,  // PK_CLASS_ellipse (TYCUEL)
    ICurve = 3004,   // PK_CLASS_icurve (TYCUIN, intersection curve; XT node 38 INTERSECTION)
    BCurve = 3005,   // PK_CLASS_bcurve (TYCUPA, parametric curve)
    SPCurve = 3006,  // PK_CLASS_spcurve (TYCUSP, surface parameter curve; XT node 137 SP_CURVE)
    FCurve = 3007,   // PK_CLASS_fcurve (TYCUFG, foreign curve)
    CpCurve = 3008,  // PK_CLASS_cpcurve (TYCUCP, constant-parameter curve)
    TRCurve = 3009,  // PK_CLASS_trcurve (TYCUTR, trimmed curve; XT node 133 TRIMMED_CURVE)
}

/// <summary>
/// Discriminant for SurfaceRecord. Maps to PK_CLASS_t values for surfaces
/// (parasolid_tokens.h TYSU*): 4001-4011, no other surface classes exist.
/// </summary>
internal enum SurfaceClass : int
{
    None = 0,
    Plane = 4001,        // PK_CLASS_plane (TYSUPL)
    Cylinder = 4002,     // PK_CLASS_cyl (TYSUCY)
    Cone = 4003,         // PK_CLASS_cone (TYSUCO)
    Sphere = 4004,       // PK_CLASS_sphere (TYSUSP)
    Torus = 4005,        // PK_CLASS_torus (TYSUTO)
    BSurface = 4006,     // PK_CLASS_bsurf (TYSUPA, parametric surface)
    BlendSurface = 4007, // PK_CLASS_blendsf (TYSUBL; XT nodes 56/57/58)
    Offset = 4008,       // PK_CLASS_offset (TYSUOF; XT node 60 OFFSET_SURF)
    Swept = 4009,        // PK_CLASS_swept (TYSUSE; XT node 67 SWEPT_SURF)
    Spun = 4010,         // PK_CLASS_spun (TYSUSU; XT node 68 SPUN_SURF)
    FSurface = 4011,     // PK_CLASS_fsurf (TYSUFG, foreign surface)
}

// ── Procedural Geometry Enums ────────────────────────────────────

/// <summary>
/// BLENDED_EDGE.blend_type / BLENDED_VERTEX.blend_type (sch_37102 nodes 56/57).
/// XT only defines these two values; chamfer blends are never stored as blend
/// surfaces (always plane/cylinder/cone/B-surface), and variable-radius or
/// conic blends that cannot be simplified are stored as B-surface.
/// </summary>
internal enum BlendType : byte
{
    RollingBall = (byte)'R',
    CliffEdge = (byte)'E',
}

/// <summary>
/// LIMIT.type (sch_37102 node 41). Help: any point on a closed curve;
/// Terminator: singular point of a surface/surface intersection;
/// Artificial: an artificial boundary; SpineBoundary: rolling-ball
/// degeneracy only.
/// </summary>
internal enum LimitType : byte
{
    Help = (byte)'H',
    Terminator = (byte)'T',
    Artificial = (byte)'L',
    SpineBoundary = (byte)'B',
}

/// <summary>
/// LIMIT.term_use (sch_37102 node 41): which intersection support surface
/// ('F' = surface[0], 'S' = surface[1]) a limit belongs to.
/// </summary>
internal enum LimitTermUse : byte
{
    First = (byte)'F',
    Second = (byte)'S',
    Unset = (byte)'?',
}

/// <summary>
/// INTERSECTION_DATA.uv_type (sch_37102 node 204): which support surfaces
/// carry UV parameter values for the chart hull vectors.
/// </summary>
internal enum IntersectionUvType : byte
{
    None = 1,
    First = 2,
    Second = 3,
    Both = 4,
}

/// <summary>
/// OFFSET_SURF.check (sch_37102 node 60): self-intersection check state
/// of the offset surface.
/// </summary>
internal enum OffsetCheckState : byte
{
    Valid = (byte)'V',
    Invalid = (byte)'I',
    Unchecked = (byte)'U',
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
    public SplineDegree Degree;
    public BufferCount NVertices; // total number of control points
    public BufferCount VertexDim; // dimension of each vertex (3 or 4 if rational)
    public KernelLogical IsRational;
    public KernelBCurveForm Form;
    public BufferCount NKnots;
    public KernelKnotType KnotType;
    public KernelLogical IsPeriodic;
    public KernelLogical IsClosed;
    public KernelSelfIntersect SelfIntersecting;
    // Indices into flat data arenas (CurveVertices, CurveKnots, CurveKnotMults)
    public DataSlot VertexOffset;   // offset into CurveVertices arena
    public DataSlot KnotOffset;     // offset into CurveKnots arena
    public DataSlot KnotMultOffset; // offset into CurveKnotMults arena
    public DataSlot ExpandedKnotOffset;
    public BufferCount ExpandedKnotCount;
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

// All records below mirror the XT schema node fields one-to-one
// (third_party/parasolid/schema/sch_37102.sch_txt); the common
// node_id/attributes_features/owner/next/previous/geometric_owner/sense
// header lives in CurveRecord/SurfaceRecord, not in the data records.

/// <summary>
/// LIMIT record (sch_37102 node 41): one end of an intersection or blend
/// branch. A Help/Artificial limit carries 1 hull vector; a Terminator
/// carries 2 (exact singular position + branch point).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LimitRecord
{
    public LimitType Type;
    public LimitTermUse TermUse;
    public DataSlot HvecOffset;  // offset into the hull-vector arena
    public int HvecCount;        // 1 for H/L, 2 for T
}

/// <summary>
/// Intersection curve data (sch_37102 node 38 INTERSECTION).
/// Exactly two support surfaces; the branch of the surface/surface
/// intersection is identified by the start/end limits and the chart.
/// Tangent direction is the cross product of the support surface normals
/// (honouring sense); UV parameters of the branch live per chart hull
/// vector in INTERSECTION_DATA, not as per-surface boxes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ICurveData
{
    public SurfTag Surface0Tag;      // surface[0], first support surface
    public SurfTag Surface1Tag;      // surface[1], second support surface
    // CHART summary (sch_37102 node 40): parameterisation of the branch
    public double BaseParameter;
    public double BaseScale;
    public int ChartCount;
    public DataSlot ChartHvecOffset; // chart hull vectors in the hull-vector arena
    // Branch identification
    public LimitRecord StartLimit;   // start LIMIT node
    public LimitRecord EndLimit;     // end LIMIT node
    public double Scale;             // optional; unset fields use 0
    // INTERSECTION_DATA (sch_37102 node 204), optional
    public IntersectionUvType UvType;
    public int UvValueCount;         // 0/2/4 doubles per hull vector
    public DataSlot UvValueOffset;
}

/// <summary>
/// Trimmed curve data (sch_37102 node 133 TRIMMED_CURVE).
/// Point1/Point2 lie exactly on the basis curve at Parm1/Parm2; with a
/// positive-sense basis curve Parm2 &gt; Parm1, with a negative sense
/// Parm2 &lt; Parm1.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TrimmedCurveData
{
    public CurveTag BasisCurveTag;
    public KernelVector3 Point1;
    public KernelVector3 Point2;
    public double Parm1;
    public double Parm2;
}

/// <summary>
/// Constant-parameter curve data (PK_CLASS_cpcurve). In XT the surviving
/// constant-parameter representation is OBSOLETE_CPC (sch_37102 node 36);
/// the current composite parametric curve is CPC (node 48, see
/// <see cref="CPCurveData"/>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CpCurveData
{
    public SurfTag SurfTag;         // supporting surface
    public byte ParameterDirection; // 'u' or 'v'
    public double Parameter;        // constant parameter value
}

/// <summary>
/// Composite parametric curve data (sch_37102 node 48 CPC): a curve made of
/// Bezier segments and a B-spline segment table (node 43 BSPLINE_CURVE).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CPCurveData
{
    public DataSlot BezierSegments; // Bezier segment table
    public DataSlot Bspline;        // BSPLINE_CURVE segment table
}

/// <summary>
/// Surface parameter curve data (sch_37102 node 137 SP_CURVE): a 3D curve
/// produced by embedding a 2D B-curve in the parameter space of a surface.
/// The 2D B-curve must be rational with vertex dimension 3 (u, v, weight)
/// or non-rational with vertex dimension 2.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SPCurveData
{
    public SurfTag SurfTag;         // surface whose UV space is used
    public CurveTag BCurveTag;      // 2D B-curve (vertex dim 3 rational / 2 plain)
    // original/tolerance_to_original are schema placeholders (not used by
    // Parasolid) and are intentionally not modelled.
}

/// <summary>
/// Offset curve data (sch_37102 node 46 OFFSET_CURVE): a curve offset from
/// a basis curve, measured along a supporting surface.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OffsetCurveData
{
    public SurfTag SurfTag;         // supporting surface
    public CurveTag CurveTag;       // basis curve
    public double Offset;           // signed offset distance
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

// ── Blend Geometry (sch_37102 nodes 56-59) ────────────────────────

/// <summary>
/// Blended edge surface data (sch_37102 node 56 BLENDED_EDGE): the exact
/// rolling-ball ('R') or cliff-edge ('E') blend along an edge. The spine is
/// the intersection of the two support surfaces offset by Range0/Range1
/// (signed; negated again for negative sense). For a cliff-edge blend one
/// support surface is itself a blended edge whose ranges are 0. The u
/// parameterisation is inherited from the spine; v runs from the
/// surface[0]-side blend boundary (v=0) to the surface[1]-side (v=1).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BlendedEdgeData
{
    public BlendType BlendType;
    public SurfTag Surface0Tag;         // surface[0], first support
    public SurfTag Surface1Tag;         // surface[1], second support (may be a blended edge)
    public CurveTag SpineTag;           // spine curve (often an INTERSECTION)
    public double Range0;               // offset applied to surface[0] (= radius)
    public double Range1;               // offset applied to surface[1]
    public double ThumbWeight0;         // schema constant 1
    public double ThumbWeight1;         // schema constant 1
    public BlendBoundTag Boundary0Tag;  // blend boundary on the surface[0] side
    public BlendBoundTag Boundary1Tag;  // blend boundary on the surface[1] side
    public LimitRecord StartLimit;      // only for periodic degenerate spines
    public LimitRecord EndLimit;
    public CurveTag ApproxSpineTag;     // optional approximate spine
    public double ApproxSpineCtol;
}

/// <summary>
/// Blended vertex surface data (sch_37102 node 57 BLENDED_VERTEX): the
/// blend surface smoothing a vertex where three blend surfaces meet. Three
/// support surfaces, no spine; the blend sphere centre is stored explicitly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BlendedVertexData
{
    public BlendType BlendType;
    public SurfTag Surface0Tag;
    public SurfTag Surface1Tag;
    public SurfTag Surface2Tag;
    public SurfTag SubSurface0Tag;      // sub-surfaces (the adjacent blend surfaces)
    public SurfTag SubSurface1Tag;
    public SurfTag SubSurface2Tag;
    public BlendBoundTag Boundary0Tag;
    public BlendBoundTag Boundary1Tag;
    public BlendBoundTag Boundary2Tag;
    public double Range0;
    public double Range1;
    public double Range2;
    public double ThumbWeight0;         // schema constant 1
    public double ThumbWeight1;
    public double ThumbWeight2;
    public KernelVector3 Centre;        // blend sphere centre
}

/// <summary>
/// Blend overlap surface data (sch_37102 node 58 BLEND_OVERLAP): the surface
/// patch smoothing the overlap region where two blend surfaces run into each
/// other. Two outer support surfaces plus four sub-surfaces; the two
/// overlap_type characters and blend types are stored per schema.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BlendOverlapData
{
    public SurfTag Surface0Tag;
    public SurfTag Surface1Tag;
    public SurfTag SubSurface0Tag;
    public SurfTag SubSurface1Tag;
    public SurfTag SubSurface2Tag;
    public SurfTag SubSurface3Tag;
    public double Range0;
    public double Range1;
    public double Range2;
    public double Range3;
    public double ThumbWeight0;         // schema constant 1
    public double ThumbWeight1;
    public double ThumbWeight2;
    public double ThumbWeight3;
    public BlendType BlendType0;        // blend_type[0]
    public BlendType BlendType1;        // blend_type[1]
    public byte OverlapType;            // schema char, values assigned per case
    public KernelLogical SwapUV;        // swap_u_v
}

/// <summary>
/// Blend boundary data (sch_37102 node 59 BLEND_BOUND): a constructive
/// surface that intersects the blend along the tangency boundary curve.
/// The support surface it belongs to is
/// blend.surface[1 - boundary] of the referenced blend surface.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BlendBoundData
{
    public short Boundary;          // index into the blend's surface array
    public SurfTag BlendTag;        // the blend surface (blended edge/vertex/overlap)
}

/// <summary>
/// Offset surface data (sch_37102 node 60 OFFSET_SURF). The offset distance
/// must not be zero within the linear resolution, and the offset surface
/// sense must match the base surface sense.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct OffsetData
{
    public SurfTag BaseSurfTag;
    public double Offset;               // signed offset distance
    public OffsetCheckState Check;      // self-intersection check state
    public double Scale;                // schema scale (internal)
}

/// <summary>
/// Swept surface data (sch_37102 node 67 SWEPT_SURF): a section curve swept
/// along a direction vector. The section must be an analytic or B-curve
/// (not an intersection, trimmed, SP or PE curve); the u parameterisation is
/// inherited from the section curve.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SweptData
{
    public CurveTag SectionCurveTag;
    public KernelVector3 Sweep;         // unit sweep direction
    public double Scale;                // schema scale (internal)
}

/// <summary>
/// Spun surface data (sch_37102 node 68 SPUN_SURF):
/// R(u,v) = Z(u) + (C(u)-Z(u))cos v + A×(C(u)-Z(u))sin v, where C is the
/// profile curve, Z the axis point and A the unit axis. Start/End are the
/// physical degeneracy points where the profile meets the axis (nullable in
/// the schema; StartParam/EndParam delimit the valid u range, 0 = infinite).
/// XAxis is set only when the profile plane contains the axis.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SpunData
{
    public CurveTag ProfileCurveTag;
    public KernelVector3 Base;          // point on the spin axis
    public KernelVector3 Axis;          // unit spin axis
    public KernelVector3 Start;         // degenerate point at low u (nullable)
    public KernelVector3 End;           // degenerate point at low v (nullable)
    public double StartParam;           // u range start (0 = unbounded)
    public double EndParam;             // u range end (0 = unbounded)
    public KernelVector3 XAxis;         // axis→profile perpendicular (nullable)
    public double Scale;                // schema scale (internal)
}
