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
/// Body: top-level owning entity. Contains shells, and direct references
/// to all faces/edges/vertices for flat iteration.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BodyRecord
{
    public RecordHeader Header;
    public KernelBodyType BodyType;
    public KernelBodyConfig BodyConfig;
    public ShellSlot FirstShell;
    public int ShellCount;
    public RegionSlot FirstRegion;
    public int RegionCount;
    // Flat iteration: body directly references all its faces/edges/vertices
    public FaceSlot FirstFaceBody;
    public int FaceCountBody;
    public EdgeSlot FirstEdgeBody;
    public int EdgeCountBody;
    public VertexSlot FirstVertexBody;
    public int VertexCountBody;
}

/// <summary>
/// Shell: a connected set of faces. Contains faces and optionally an acorn vertex.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShellRecord
{
    public RecordHeader Header;
    public KernelShellType ShellType;
    public BodySlot Body;
    public FaceSlot FirstFaceShell;
    public int FaceCount;
    public VertexSlot AcornVertex;       // -1 if none
    public ShellSlot NextInBody;         // sibling chain
}

/// <summary>
/// Face: a bounded region on a surface. Contains loops.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FaceRecord
{
    public RecordHeader Header;
    public ShellSlot Shell;
    public LoopSlot FirstLoop;
    public int LoopCount;
    public SurfTag SurfTag;
    public KernelSense Orientation;
    public FaceSlot NextInShell;  // sibling chain in shell
    public FaceSlot NextInBody;   // sibling chain in body
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
    public int FinCount;
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
    public FinSlot FirstFinEdge;
    public int FinCount;
    public CurveTag CurveTag;
    public KernelEdgeConvexity Convexity;
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
    public VertexSlot NextInBody; // sibling chain
}

/// <summary>
/// Region: a void space inside a body. Contains one shell.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RegionRecord
{
    public RecordHeader Header;
    public BodySlot Body;
    public ShellSlot Shell;       // -1 if none
    public RegionSlot NextInBody; // sibling chain
}
