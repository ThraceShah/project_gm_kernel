using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal unsafe struct SessionStartCommand : IKernelCommand
{
    public PK_SESSION_start_o_s* Options;

    public int Execute() => KernelRuntime.SessionStart(Options);
}

internal unsafe struct SessionStopCommand : IKernelCommand
{
    public int Execute() => KernelRuntime.SessionStop();
}

internal unsafe struct PointCreateCommand : IKernelCommand
{
    public PK_POINT_sf_s* PointSf;
    public PointTag* Point;

    public int Execute() => KernelRuntime.PointCreate(PointSf, Point);
}

internal unsafe struct EntityAskClassCommand : IKernelCommand
{
    public EntityTag Entity;
    public int* Class;

    public int Execute() => KernelRuntime.EntityAskClass(Entity, Class);
}

internal unsafe struct EntityDeleteCommand : IKernelCommand
{
    public int EntityCount;
    public EntityTag* Entities;

    public int Execute() => KernelRuntime.EntityDelete(EntityCount, Entities);
}

internal unsafe struct EntityAskPartitionCommand : IKernelCommand
{
    public EntityTag Entity;
    public PartitionSlot* Partition;

    public int Execute() => KernelRuntime.EntityAskPartition(Entity, Partition);
}

internal unsafe struct SessionAskCurrentPartitionCommand : IKernelCommand
{
    public PartitionSlot* Partition;

    public int Execute() => KernelRuntime.SessionAskCurrentPartition(Partition);
}

internal unsafe struct BodyCreateTopology2Command : IKernelCommand
{
    public int TopologyCount;
    public PK_CLASS_t* Classes;
    public int RelationCount;
    public int* Parents;
    public int* Children;
    public int* Senses;
    public PK_BODY_create_topology_2_o_s* Options;
    public PK_BODY_create_topology_2_r_s* Results;

    public int Execute() => KernelRuntime.BodyCreateTopology2(TopologyCount, Classes, RelationCount, Parents, Children, Senses, Options, Results);
}

internal unsafe struct BodyAskShellsCommand : IKernelCommand
{
    public EntityTag Body;
    public int* ShellCount;
    public EntityTag** Shells;

    public int Execute() => KernelRuntime.BodyAskShells(Body, ShellCount, Shells);
}

internal unsafe struct BodyAskFacesCommand : IKernelCommand
{
    public EntityTag Body;
    public int* FaceCount;
    public EntityTag** Faces;

    public int Execute() => KernelRuntime.BodyAskFaces(Body, FaceCount, Faces);
}

internal unsafe struct BodyAskEdgesCommand : IKernelCommand
{
    public EntityTag Body;
    public int* EdgeCount;
    public EntityTag** Edges;

    public int Execute() => KernelRuntime.BodyAskEdges(Body, EdgeCount, Edges);
}

internal unsafe struct BodyAskVerticesCommand : IKernelCommand
{
    public EntityTag Body;
    public int* VertexCount;
    public EntityTag** Vertices;

    public int Execute() => KernelRuntime.BodyAskVertices(Body, VertexCount, Vertices);
}

internal unsafe struct BodyAskRegionsCommand : IKernelCommand
{
    public EntityTag Body;
    public int* RegionCount;
    public EntityTag** Regions;

    public int Execute() => KernelRuntime.BodyAskRegions(Body, RegionCount, Regions);
}

internal unsafe struct BodyAskTopologyCommand : IKernelCommand
{
    public EntityTag Body;
    public PK_BODY_ask_topology_o_s* Options;
    public int* TopologyCount;
    public nint* Topologies;
    public nint* Classes;
    public int* RelationCount;
    public nint* Parents;
    public nint* Children;
    public nint* Senses;

    public int Execute() => KernelRuntime.BodyAskTopology(Body, Options, TopologyCount, Topologies, Classes, RelationCount, Parents, Children, Senses);
}

internal unsafe struct FaceAskLoopsCommand : IKernelCommand
{
    public EntityTag Face;
    public int* LoopCount;
    public EntityTag** Loops;

    public int Execute() => KernelRuntime.FaceAskLoops(Face, LoopCount, Loops);
}

internal unsafe struct FaceAskSurfCommand : IKernelCommand
{
    public EntityTag Face;
    public SurfTag* Surf;

    public int Execute() => KernelRuntime.FaceAskSurf(Face, Surf);
}

internal unsafe struct FaceAskShellsCommand : IKernelCommand
{
    public EntityTag Face;
    public EntityTag* Shells;

    public int Execute() => KernelRuntime.FaceAskShells(Face, Shells);
}

internal unsafe struct RegionIsSolidCommand : IKernelCommand
{
    public EntityTag Region;
    public KernelLogical* IsSolid;

    public int Execute() => KernelRuntime.RegionIsSolid(Region, IsSolid);
}

internal unsafe struct LoopAskFaceCommand : IKernelCommand
{
    public EntityTag Loop;
    public EntityTag* Face;

    public int Execute() => KernelRuntime.LoopAskFace(Loop, Face);
}

internal unsafe struct LoopAskFinsCommand : IKernelCommand
{
    public EntityTag Loop;
    public int* FinCount;
    public EntityTag** Fins;

    public int Execute() => KernelRuntime.LoopAskFins(Loop, FinCount, Fins);
}

internal unsafe struct EdgeAskFinsCommand : IKernelCommand
{
    public EntityTag Edge;
    public int* FinCount;
    public EntityTag** Fins;

    public int Execute() => KernelRuntime.EdgeAskFins(Edge, FinCount, Fins);
}

internal unsafe struct EdgeAskCurveCommand : IKernelCommand
{
    public EntityTag Edge;
    public CurveTag* Curve;

    public int Execute() => KernelRuntime.EdgeAskCurve(Edge, Curve);
}

internal unsafe struct VertexAskPointCommand : IKernelCommand
{
    public EntityTag Vertex;
    public PointTag* Point;

    public int Execute() => KernelRuntime.VertexAskPoint(Vertex, Point);
}

internal unsafe struct FinAskEdgeCommand : IKernelCommand
{
    public EntityTag Fin;
    public EntityTag* Edge;

    public int Execute() => KernelRuntime.FinAskEdge(Fin, Edge);
}

internal unsafe struct FinAskLoopCommand : IKernelCommand
{
    public EntityTag Fin;
    public EntityTag* Loop;

    public int Execute() => KernelRuntime.FinAskLoop(Fin, Loop);
}

internal unsafe struct FinAskFaceCommand : IKernelCommand
{
    public EntityTag Fin;
    public EntityTag* Face;

    public int Execute() => KernelRuntime.FinAskFace(Fin, Face);
}

internal unsafe struct TransfCreateCommand : IKernelCommand
{
    public PK_TRANSF_sf_s* TransfSf;
    public TransfTag* Transf;

    public int Execute() => KernelRuntime.TransfCreate(TransfSf, Transf);
}

internal unsafe struct BodyCreateSolidBlockCommand : IKernelCommand
{
    public double X;
    public double Y;
    public double Z;
    public PK_AXIS2_sf_s* BasisSet;
    public EntityTag* Body;

    public int Execute() => KernelRuntime.BodyCreateSolidBlock(X, Y, Z, BasisSet, Body);
}

internal unsafe struct BodyCreateSolidCylCommand : IKernelCommand
{
    public double Radius;
    public double Height;
    public PK_AXIS2_sf_s* BasisSet;
    public EntityTag* Body;

    public int Execute() => KernelRuntime.BodyCreateSolidCyl(Radius, Height, BasisSet, Body);
}

internal unsafe struct BodyCreateSolidConeCommand : IKernelCommand
{
    public double Radius;
    public double Height;
    public double SemiAngle;
    public PK_AXIS2_sf_s* BasisSet;
    public EntityTag* Body;

    public int Execute() => KernelRuntime.BodyCreateSolidCone(Radius, Height, SemiAngle, BasisSet, Body);
}

internal unsafe struct BodyCreateSolidPrismCommand : IKernelCommand
{
    public double Radius;
    public double Height;
    public int SideCount;
    public PK_AXIS2_sf_s* BasisSet;
    public EntityTag* Body;

    public int Execute() => KernelRuntime.BodyCreateSolidPrism(Radius, Height, SideCount, BasisSet, Body);
}

internal unsafe struct BodyCreateSolidSphereCommand : IKernelCommand
{
    public double Radius;
    public PK_AXIS2_sf_s* BasisSet;
    public EntityTag* Body;

    public int Execute() => KernelRuntime.BodyCreateSolidSphere(Radius, BasisSet, Body);
}

internal unsafe struct BodyCreateSolidTorusCommand : IKernelCommand
{
    public double MajorRadius;
    public double MinorRadius;
    public PK_AXIS2_sf_s* BasisSet;
    public EntityTag* Body;

    public int Execute() => KernelRuntime.BodyCreateSolidTorus(MajorRadius, MinorRadius, BasisSet, Body);
}

internal unsafe struct CylCreateCommand : IKernelCommand
{
    public PK_CYL_sf_s* CylinderSf;
    public EntityTag* Cylinder;

    public int Execute() => KernelRuntime.CylCreate(CylinderSf, Cylinder);
}

internal unsafe struct CylAskCommand : IKernelCommand
{
    public EntityTag Cylinder;
    public PK_CYL_sf_s* CylinderSf;

    public int Execute() => KernelRuntime.CylAsk(Cylinder, CylinderSf);
}

internal unsafe struct PartTransmitBCommand : IKernelCommand
{
    public int PartCount;
    public EntityTag* Parts;
    public PK_PART_transmit_o_s* Options;
    public PK_MEMORY_block_t* Block;

    public int Execute() => KernelRuntime.PartTransmitB(PartCount, Parts, Options, Block);
}

internal unsafe struct PartReceiveBCommand : IKernelCommand
{
    public PK_MEMORY_block_t Block;
    public PK_PART_receive_o_s* Options;
    public int* PartCount;
    public EntityTag** Parts;

    public int Execute() => KernelRuntime.PartReceiveB(Block, Options, PartCount, Parts);
}

internal unsafe struct MemoryBlockFreeCommand : IKernelCommand
{
    public PK_MEMORY_block_t* Block;

    public int Execute() => KernelRuntime.MemoryBlockFree(Block);
}

internal unsafe struct MemoryFreeCommand : IKernelCommand
{
    public void* Pointer;

    public int Execute() => KernelRuntime.MemoryFree(Pointer);
}

internal unsafe struct MarkCreateCommand : IKernelCommand
{
    public int* Mark;

    public int Execute() => KernelRuntime.MarkCreate(Mark);
}

internal unsafe struct MarkGotoCommand : IKernelCommand
{
    public int Mark;

    public int Execute() => KernelRuntime.MarkGoto(Mark);
}

internal unsafe struct MarkDeleteCommand : IKernelCommand
{
    public int Mark;

    public int Execute() => KernelRuntime.MarkDelete(Mark);
}
