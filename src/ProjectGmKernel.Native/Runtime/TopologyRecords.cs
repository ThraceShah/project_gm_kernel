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
    public KernelShellType ShellType;
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
    public KernelLoopType LoopType;
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
    public KernelEdgeType EdgeType;
    public BodySlot Body;
    public VertexSlot StartVertex; // -1 for ring/vertexless edges
    public VertexSlot EndVertex;   // -1 for ring/vertexless edges
    public FinSlot FirstFinEdge;
    public FinSlot LastFinEdge;
    public int FinCount;
    public CurveTag CurveTag;
    public KernelEdgeConvexity Convexity;
    public EdgeSlot PrevInBody;   // sibling ring
    public EdgeSlot NextInBody;   // sibling chain
}

/// <summary>
/// Fin (half-edge): a directed traversal of an edge within a loop.
/// Links edge, loop, face together with next/prev in loop and next/prev of edge.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FinRecord
{
    public RecordHeader Header;
    public KernelFinType FinType;
    public EdgeSlot Edge;
    public LoopSlot Loop;
    public FaceSlot Face;
    public FinSlot NextInLoop;
    public FinSlot PrevInLoop;
    public FinSlot NextOfEdge;
    public FinSlot PrevOfEdge;
    public VertexSlot Vertex;
    public FinSlot NextAtVertex;
    public FinSlot PrevAtVertex;
}

/// <summary>
/// Vertex: a point in the B-Rep. References a geometric point.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VertexRecord
{
    public RecordHeader Header;
    public KernelVertexType VertexType;
    public BodySlot Body;
    public PointTag PointTag;
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
    public RegionSlot PrevInBody; // sibling ring
    public RegionSlot NextInBody; // sibling chain
}
