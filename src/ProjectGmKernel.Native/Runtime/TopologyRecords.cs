using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Shared metadata header embedded at the start of every entity record.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RecordHeader
{
    public int Generation;
    public byte Alive;
    public short Partition;
    public int RollbackStamp;
    public int Tag;                             // published tag for this slot (0 = none yet)
}

// ── Topology Records ──────────────────────────────────────────────

/// <summary>
/// Body: top-level owning entity. Contains regions/shells, and direct references
/// to all faces/edges/vertices for flat iteration.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BodyRecord
{
    public RecordHeader Header;
    public KernelBodyType BodyType;
    public KernelBodyConfig BodyConfig;
    public ShellSlot FirstShell;
    public ShellSlot LastShell;
    public int ShellCount;
    public RegionSlot FirstRegion;
    public RegionSlot LastRegion;
    public int RegionCount;
    // Flat iteration: body directly references all its faces/edges/vertices
    public FaceSlot FirstFaceBody;
    public FaceSlot LastFaceBody;
    public int FaceCountBody;
    public EdgeSlot FirstEdgeBody;
    public EdgeSlot LastEdgeBody;
    public int EdgeCountBody;
    public VertexSlot FirstVertexBody;
    public VertexSlot LastVertexBody;
    public int VertexCountBody;
    // Construction geometry chains (XT BODY surface/curve/point heads).
    // Lattice/mesh/polyline chains have no kernel entity yet.
    public SurfaceSlot FirstConstructionSurface;
    public SurfaceSlot LastConstructionSurface;
    public int ConstructionSurfaceCount;
    public CurveSlot FirstConstructionCurve;
    public CurveSlot LastConstructionCurve;
    public int ConstructionCurveCount;
    public PointSlot FirstConstructionPoint;
    public PointSlot LastConstructionPoint;
    public int ConstructionPointCount;
    public BodySlot PrevInPartition;
    public BodySlot NextInPartition;
}

/// <summary>
/// Shell: a connected region boundary. Contains directed face uses.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShellRecord
{
    public RecordHeader Header;
    public BodySlot Body;
    public RegionSlot Region;
    public FaceUseSlot FirstFaceUseShell;
    public FaceUseSlot LastFaceUseShell;
    public int FaceUseCount;
    public VertexSlot AcornVertex;       // -1 if none
    public ShellSlot PrevInBody;         // sibling ring
    public ShellSlot NextInBody;         // sibling chain
    public ShellSlot PrevInRegion;       // sibling ring
    public ShellSlot NextInRegion;       // sibling chain
}

/// <summary>
/// FaceUse: a directed use of a shared face by one shell.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FaceUseRecord
{
    public RecordHeader Header;
    public ShellSlot Shell;
    public FaceSlot Face;
    public KernelSense Sense;
    public FaceUseSlot PrevInShell;
    public FaceUseSlot NextInShell;
}

/// <summary>
/// Face: a bounded region on a surface. Contains loops and back/front shell use links.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FaceRecord
{
    public RecordHeader Header;
    public ShellSlot BackShell;
    public ShellSlot FrontShell;
    public FaceUseSlot BackFaceUse;
    public FaceUseSlot FrontFaceUse;
    public LoopSlot FirstLoop;
    public int LoopCount;
    public SurfTag SurfTag;
    public KernelSense Orientation;
    public double Tolerance;     // XT face tolerance is not used; keep as null-double (0)
    public FaceSlot PrevOnSurf;  // previous face sharing the surface (XT previous_on_surface)
    public FaceSlot NextOnSurf;  // next face sharing the surface (XT next_on_surface)
    public FaceSlot PrevInBody;   // sibling ring in body
    public FaceSlot NextInBody;   // sibling chain in body
    public LoopSlot LastLoop;
}

/// <summary>
/// Loop: a boundary loop of a face. Contains fins (half-edges).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LoopRecord
{
    public RecordHeader Header;
    public FaceSlot Face;
    public FinSlot FirstFin;
    public FinSlot LastFin;
    public int FinCount;
    public LoopSlot PrevInFace;   // sibling ring
    public LoopSlot NextInFace;   // sibling chain
}

/// <summary>
/// Edge: a topological edge bounded by two vertices. Contains fins.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct EdgeRecord
{
    public RecordHeader Header;
    public BodySlot Body;
    public VertexSlot StartVertex; // -1 for ring/vertexless edges
    public VertexSlot EndVertex;   // -1 for ring/vertexless edges
    public FinSlot FirstFinEdge;
    public FinSlot LastFinEdge;
    public int FinCount;
    public CurveTag CurveTag;
    public double Tolerance;     // 0 = accurate edge (XT null-double)
    public EdgeSlot PrevInBody;   // sibling ring
    public EdgeSlot NextInBody;   // sibling chain
}

/// <summary>
/// Fin (XT "halfedge"): the oriented use of an edge by a loop (XT schema 5.3.9).
/// Vertex is the forward vertex of the fin. Sense is '+' when the fin direction
/// agrees with its edge, '-' when opposed — this is the fin's only stored sense.
/// There is deliberately no sense between the fin and its own pcurve (Curve):
/// Parasolid stores no such field; the direction is derived at query time
/// (PK_FIN_ask_oriented_curve returns a flag) from the constraint that the
/// pcurve ends correspond to the fin's vertices, while the pcurve node itself
/// carries its own sense against its basis curve. See
/// docs/xt_topology_sense_semantics.md.
/// Face is reached via the owning loop. Other is the next fin around the edge
/// in XT's ordered fin ring, starting at the positive (primary) fin.
/// Curve holds the trimmed SP-curve of a tolerant edge's fin
/// (-1 otherwise).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FinRecord
{
    public RecordHeader Header;
    public LoopSlot Loop;          // -1 for dummy fins
    public FinSlot NextInLoop;     // forward: next fin around loop
    public FinSlot PrevInLoop;     // backward: previous fin around loop
    public VertexSlot Vertex;      // forward vertex of the fin
    public FinSlot Other;          // next fin around edge in XT ring; -1 when unattached
    public EdgeSlot Edge;          // -1 for isolated/dummy fins
    public CurveTag Curve;         // trimmed SP-curve for tolerant edges; -1 otherwise
    public FinSlot NextAtVertex;   // next fin in chain at vertex
    public FinSlot PrevAtVertex;
    public FinSlot NextOfEdge;     // edge's ordered fin ring (kernel-internal)
    public FinSlot PrevOfEdge;
    public char Sense;             // '+' same direction as edge, '-' opposed
}

/// <summary>
/// Vertex: a point in the B-Rep. References a geometric point.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VertexRecord
{
    public RecordHeader Header;
    public BodySlot Body;
    public PointTag PointTag;
    public double Tolerance;     // 0 = accurate vertex (XT null-double)
    public FinSlot FirstFinVertex;
    public FinSlot LastFinVertex;
    public VertexSlot PrevInBody; // sibling ring
    public VertexSlot NextInBody; // sibling chain
}

/// <summary>
/// Region: a solid or void space inside a body. Contains shells.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RegionRecord
{
    public RecordHeader Header;
    public BodySlot Body;
    public KernelLogical IsSolid;
    public ShellSlot FirstShell;  // -1 if none
    public ShellSlot LastShell;   // -1 if none
    public int ShellCount;
    public FrameSlot Frame;      // XT frame attached to region; -1 = none (no frame entity yet)
    public RegionSlot PrevInBody; // sibling ring
    public RegionSlot NextInBody; // sibling chain
}
