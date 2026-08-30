using System.Runtime.InteropServices;

namespace ProjectGmKernel.Xt;

public enum XtPartKind : byte { Body, Assembly }
public enum XtBodyKind : byte { Unspecified, Solid, Sheet, Minimum, Wire, General, Acorn, Empty, Compound }
public enum XtGeometryKind : byte
{
    Line, Circle, Ellipse, ICurve, BCurve, SPCurve, FCurve, CPCurve, TRCurve,
    Plane, Cylinder, Cone, Sphere, Torus, BSurf, BlendSF, BlendBound, Offset, Swept, Spun, FSurf,
}
public enum XtOwnerKind : byte
{
    None, Part, Body, Region, Shell, Face, Loop, Fin, Edge, Vertex, Point, Geometry,
    Assembly, Instance, Transform, Frame, Attribute, Mesh, Lattice,
}
public enum XtAttributeValueKind : byte
{
    Empty, Integer, Real, Utf8String, UnicodeString, Vector, Coordinate, Direction,
    Axis, Entity, Pointer, Bytes,
}
public enum XtMeshKind : byte { Mesh,Pline,MTopol,MFacet,MFin,MVertex,PolylineData,PsmMesh,IntegerComb,IntegerTooth,VectorComb,VectorTooth,PointValues }

[StructLayout(LayoutKind.Sequential)]
public struct XtRange { public XtTableOffset Offset; public XtTableCount Count; }

[StructLayout(LayoutKind.Sequential)]
public struct XtVector3 { public double X; public double Y; public double Z; }

[StructLayout(LayoutKind.Sequential)]
public struct XtPartRow { public XtPartKind Kind; public XtNodeIndex TransmitIndex; public byte IsRoot; public byte HasExternalNext; public byte HasExternalPrevious; public XtNodeIndex ExternalNext; public XtNodeIndex ExternalPrevious; public XtBodyIndex Body; public XtAssemblyIndex Assembly; public XtStringIndex Name; public XtPartIndex Next; public XtPartIndex Previous; }
[StructLayout(LayoutKind.Sequential)]
public struct XtBodyRow { public XtBodyKind Kind; public XtTableCount HighestNodeId; public XtTableIndex LowestNodeId; public XtInstanceIndex ReferenceInstance; public XtBodyIndex Owner; public XtBodyIndex Child; public XtGeometryIndex Surface; public XtGeometryIndex Curve; public XtPointIndex Point; public XtGeometryIndex BoundarySurface; public XtGeometryIndex BoundaryCurve; public XtPointIndex BoundaryPoint; public XtMeshIndex BoundaryMesh; public XtMeshIndex BoundaryPolyline; public XtRange Regions; public XtRange Shells; public XtRange Edges; public XtRange Vertices; public XtRange Attributes; }
[StructLayout(LayoutKind.Sequential)]
public struct XtRegionRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtBodyIndex Body; public XtBodyIndex OwnerBody; public XtShellIndex FirstShell; public byte IsSolid; public XtRegionIndex Next; public XtRegionIndex Previous; }
[StructLayout(LayoutKind.Sequential)]
public struct XtShellRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtBodyIndex Body; public XtRegionIndex Region; public XtFaceIndex FirstFace; public XtFaceIndex FrontFace; public XtEdgeIndex FirstEdge; public XtVertexIndex FirstVertex; public XtShellIndex Next; }
[StructLayout(LayoutKind.Sequential)]
public struct XtFaceRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtShellIndex Shell; public XtLoopIndex FirstLoop; public XtGeometryIndex Surface; public XtOwnerKind SurfaceKind; public XtTableIndex SurfaceEntity; public XtFaceIndex Next; public XtFaceIndex Previous; public XtFaceIndex NextFront; public XtFaceIndex PreviousFront; public XtFaceIndex NextOnSurface; public XtFaceIndex PreviousOnSurface; public XtShellIndex FrontShell; public sbyte Sense; }
[StructLayout(LayoutKind.Sequential)]
public struct XtLoopRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtFaceIndex Face; public XtFinIndex FirstFin; public XtLoopIndex Next; public byte IsOuter; }
[StructLayout(LayoutKind.Sequential)]
public struct XtFinRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtLoopIndex Loop; public XtEdgeIndex Edge; public XtVertexIndex Vertex; public XtGeometryIndex Curve; public XtOwnerKind CurveKind; public XtTableIndex CurveEntity; public XtFinIndex Forward; public XtFinIndex Backward; public XtFinIndex Other; public XtFinIndex NextAtVertex; public sbyte Sense; public byte SensePresent; }
[StructLayout(LayoutKind.Sequential)]
public struct XtEdgeRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtVertexIndex Start; public XtVertexIndex End; public XtGeometryIndex Curve; public XtOwnerKind CurveKind; public XtTableIndex CurveEntity; public XtFinIndex FirstFin; public XtEdgeIndex Next; public XtEdgeIndex Previous; public XtEdgeIndex NextOnCurve; public XtEdgeIndex PreviousOnCurve; public XtBodyIndex Body; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public double Tolerance; public byte TolerancePresent; }
[StructLayout(LayoutKind.Sequential)]
public struct XtVertexRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtPointIndex Point; public XtFinIndex FirstFin; public XtEdgeIndex FirstEdge; public XtVertexIndex Next; public XtVertexIndex Previous; public XtBodyIndex Body; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public double Tolerance; public byte TolerancePresent; }
[StructLayout(LayoutKind.Sequential)]
public struct XtPointRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtVector3 Position; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public XtPointIndex Next; public XtPointIndex Previous; }

// Fixed data is stored inline; spline and procedural data uses flat ranges.
[StructLayout(LayoutKind.Sequential)]
public struct XtGeometryRow
{
    public XtGeometryKind Kind;
    public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex;
    public XtVector3 Origin;
    public XtVector3 Axis;
    public XtVector3 Reference;
    public double A;
    public double B;
    public double C;
    public double D;
    public double E;
    public double Scale;
    public XtRange ControlPoints;
    public XtRange Weights;
    public XtRange Knots;
    public XtRange Multiplicities;
    public XtRange KnotsU;
    public XtRange KnotsV;
    public XtRange MultiplicitiesU;
    public XtRange MultiplicitiesV;
    public XtGeometryIndex Basis;
    public XtGeometryIndex Profile;
    public XtGeometryIndex Spine;
    public XtChartIndex Chart;
    public XtLimitIndex StartLimit;
    public XtLimitIndex EndLimit;
    public XtIntersectionDataIndex IntersectionData;
    public XtGeometricOwnerIndex GeometricOwner;
    public XtOwnerKind OwnerKind;
    public XtTableIndex Owner;
    public XtGeometryIndex Next;
    public XtGeometryIndex Previous;
    public char Subtype;
    public sbyte Sense;
    public byte Flag;
    public short DegreeU;
    public short DegreeV;
    public XtTableCount ControlCountU;
    public XtTableCount ControlCountV;
    public short VertexDimension;
    public uint KnotTypeU;
    public uint KnotTypeV;
    public byte Rational;
    public byte PeriodicU;
    public byte PeriodicV;
    public byte ClosedU;
    public byte ClosedV;
    public byte Form;
    public double OriginalULow,OriginalUHigh,OriginalVLow,OriginalVHigh,ExtendedULow,ExtendedUHigh,ExtendedVLow,ExtendedVHigh;
    public byte SelfIntersection;
    public byte ScalePresent;
    public char OriginalUStart,OriginalUEnd,OriginalVStart,OriginalVEnd,ExtendedUStart,ExtendedUEnd,ExtendedVStart,ExtendedVEnd;
}

[StructLayout(LayoutKind.Sequential)]
public struct XtTransformRow { public XtNodeIndex TransmitIndex; public double M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23; public XtInstanceIndex Owner; public double Scale; public int Flag; }
[StructLayout(LayoutKind.Sequential)]
public struct XtFrameRow { public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtGeometryIndex Geometry; public XtOwnerKind GeometryKind; public XtTableIndex GeometryEntity; public XtFrameIndex Next; public XtFrameIndex Previous; public XtFrameIndex NextOnGeometry; public XtFrameIndex PreviousOnGeometry; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public sbyte Sense; }
[StructLayout(LayoutKind.Sequential)]
public struct XtAssemblyRow { public XtTableCount HighestNodeId; public XtRange Instances; public XtRange Attributes; public XtStringIndex Name; public XtInstanceIndex ReferenceInstance; public XtAssemblyIndex Next; public XtAssemblyIndex Previous; }
[StructLayout(LayoutKind.Sequential)]
public struct XtInstanceRow { public XtNodeIndex TransmitIndex; public XtAssemblyIndex Owner; public XtPartIndex Part; public byte HasExternalPart; public byte ExternalLinks; public XtTransformIndex Transform; public XtInstanceIndex NextInPart; public XtInstanceIndex PreviousInPart; public XtInstanceIndex NextOfPart; public XtInstanceIndex PreviousOfPart; public XtRange Attributes; }
[StructLayout(LayoutKind.Sequential)]
public struct XtAttributeDefinitionRow { public XtNodeIndex TransmitIndex; public XtStringIndex Name; public XtRange FieldDefinitions; public ulong LegalOwners; public ulong Actions; public int TypeId; public byte BuiltIn; }
[StructLayout(LayoutKind.Sequential)]
public struct XtAttributeFieldDefinitionRow { public XtStringIndex Name; public XtAttributeValueKind Kind; public XtTableCount Cardinality; }
[StructLayout(LayoutKind.Sequential)]
public struct XtAttributeRow { public XtNodeIndex TransmitIndex; public XtAttributeDefinitionIndex Definition; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public XtRange Values; }
[StructLayout(LayoutKind.Sequential)]
public struct XtAttributeValueRow { public XtAttributeValueKind Kind; public long Integer; public double Real; public XtVector3 Vector; public XtRange Data; public XtTableIndex Entity; public byte Present; }
[StructLayout(LayoutKind.Sequential)]
public struct XtUserFieldRow { public XtOwnerKind OwnerKind; public XtTableIndex Owner; public XtRange Payload; }
[StructLayout(LayoutKind.Sequential)]
public struct XtMeshRow { public XtMeshKind Kind; public XtTableIndex PersistentId; public XtNodeIndex TransmitIndex; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public XtTableIndex Next; public XtTableIndex Previous; public XtTableIndex Data; public XtTableIndex PsmMesh; public XtTableIndex PositionPool; public XtTableIndex NormalPool; public XtTableIndex PositionIndices; public XtTableIndex NormalIndices; public XtRange References; public XtRange Values; public XtRange Vertices; public XtRange Topology; public XtRange Facets; public XtRange Fins; public char Sense; public uint Token; public uint Token2; public int I0; public int I1; public int I2; public int I3; public int I4; public double D0; public double D1; public byte Logical; }
[StructLayout(LayoutKind.Sequential)]
public struct XtLatticeRow { public XtOwnerKind OwnerKind; public XtTableIndex Owner; public XtRange Vertices; public XtRange Rods; public XtRange Balls; public XtRange IjkBoxes; }
[StructLayout(LayoutKind.Sequential)]
public struct XtConnectionRow { public XtTableIndex A; public XtTableIndex B; public XtTableIndex C; public XtTableIndex D; public XtTableIndex Next; public uint Flags; }
[StructLayout(LayoutKind.Sequential)]
public struct XtHVectorRow { public double X; public double Y; public double Z; public double W; }
[StructLayout(LayoutKind.Sequential)]
public struct XtChartRow { public XtNodeIndex TransmitIndex; public double BaseParameter; public double BaseScale; public XtTableCount ChartCount; public double ChordalError; public double AngularError; public double ParameterErrorLow; public double ParameterErrorHigh; public byte ParameterErrorLowPresent; public byte ParameterErrorHighPresent; public XtRange HVectors; }
[StructLayout(LayoutKind.Sequential)]
public struct XtLimitRow { public XtNodeIndex TransmitIndex; public char Type; public char TermUse; public byte TypePresent; public byte TermUsePresent; public XtRange HVectors; }
[StructLayout(LayoutKind.Sequential)]
public struct XtIntersectionDataRow { public XtNodeIndex TransmitIndex; public uint UvType; public XtRange Values; }
[StructLayout(LayoutKind.Sequential)]
public struct XtGeometricOwnerRow { public XtNodeIndex TransmitIndex; public XtOwnerKind OwnerKind; public XtTableIndex Owner; public XtGeometricOwnerIndex Next; public XtGeometricOwnerIndex Previous; public XtOwnerKind SharedGeometryKind; public XtTableIndex SharedGeometry; }
[StructLayout(LayoutKind.Sequential)]
public struct XtStringRow { public XtBlobOffset Offset; public XtTableCount ByteCount; }
[StructLayout(LayoutKind.Sequential)]
public struct XtScalarRow { public double Value; public byte Present; }
[StructLayout(LayoutKind.Sequential)]
public struct XtTransmitOrderRow { public XtNodeIndex NodeIndex; public XtTableIndex Order; }

[StructLayout(LayoutKind.Sequential)]
public readonly record struct XtBrepCounts(
    XtTableCount Parts = 0, XtTableCount Bodies = 0, XtTableCount Regions = 0,
    XtTableCount Shells = 0, XtTableCount Faces = 0, XtTableCount Loops = 0,
    XtTableCount Fins = 0, XtTableCount Edges = 0, XtTableCount Vertices = 0,
    XtTableCount Points = 0, XtTableCount Geometries = 0, XtTableCount Transforms = 0,
    XtTableCount Frames = 0, XtTableCount Assemblies = 0, XtTableCount Instances = 0,
    XtTableCount AttributeDefinitions = 0, XtTableCount AttributeFieldDefinitions = 0,
    XtTableCount Attributes = 0, XtTableCount AttributeValues = 0,
    XtTableCount UserFields = 0, XtTableCount Meshes = 0, XtTableCount Lattices = 0,
    XtTableCount Connections = 0, XtTableCount ControlPoints = 0,
    XtTableCount Scalars = 0, XtTableCount Charts = 0, XtTableCount Limits = 0,
    XtTableCount IntersectionData = 0, XtTableCount HVectors = 0, XtTableCount GeometricOwners = 0, XtTableCount TransmitOrder = 0,
    XtTableCount Strings = 0, XtTableCount PayloadBytes = 0);

public sealed class XtBrepModel
{
    internal XtBrepModel(XtBrepStorage storage) => Storage = storage;
    internal XtBrepStorage Storage { get; }
    public string? VersionText => Storage.VersionText;
    public XtBrepCounts Counts => Storage.Counts;
    public ReadOnlySpan<XtPartRow> Parts => Storage.Parts;
    public ReadOnlySpan<XtBodyRow> Bodies => Storage.Bodies;
    public ReadOnlySpan<XtRegionRow> Regions => Storage.Regions;
    public ReadOnlySpan<XtShellRow> Shells => Storage.Shells;
    public ReadOnlySpan<XtFaceRow> Faces => Storage.Faces;
    public ReadOnlySpan<XtLoopRow> Loops => Storage.Loops;
    public ReadOnlySpan<XtFinRow> Fins => Storage.Fins;
    public ReadOnlySpan<XtEdgeRow> Edges => Storage.Edges;
    public ReadOnlySpan<XtVertexRow> Vertices => Storage.Vertices;
    public ReadOnlySpan<XtPointRow> Points => Storage.Points;
    public ReadOnlySpan<XtGeometryRow> Geometries => Storage.Geometries;
    public ReadOnlySpan<XtTransformRow> Transforms => Storage.Transforms;
    public ReadOnlySpan<XtFrameRow> Frames => Storage.Frames;
    public ReadOnlySpan<XtAssemblyRow> Assemblies => Storage.Assemblies;
    public ReadOnlySpan<XtInstanceRow> Instances => Storage.Instances;
    public ReadOnlySpan<XtAttributeDefinitionRow> AttributeDefinitions => Storage.AttributeDefinitions;
    public ReadOnlySpan<XtAttributeFieldDefinitionRow> AttributeFieldDefinitions => Storage.AttributeFieldDefinitions;
    public ReadOnlySpan<XtAttributeRow> Attributes => Storage.Attributes;
    public ReadOnlySpan<XtAttributeValueRow> AttributeValues => Storage.AttributeValues;
    public ReadOnlySpan<XtUserFieldRow> UserFields => Storage.UserFields;
    public ReadOnlySpan<XtMeshRow> Meshes => Storage.Meshes;
    public ReadOnlySpan<XtLatticeRow> Lattices => Storage.Lattices;
    public ReadOnlySpan<XtConnectionRow> Connections => Storage.Connections;
    public ReadOnlySpan<XtVector3> ControlPoints => Storage.ControlPoints;
    public ReadOnlySpan<XtScalarRow> Scalars => Storage.Scalars;
    public ReadOnlySpan<XtChartRow> Charts => Storage.Charts;
    public ReadOnlySpan<XtLimitRow> Limits => Storage.Limits;
    public ReadOnlySpan<XtIntersectionDataRow> IntersectionData => Storage.IntersectionData;
    public ReadOnlySpan<XtHVectorRow> HVectors => Storage.HVectors;
    public ReadOnlySpan<XtGeometricOwnerRow> GeometricOwners => Storage.GeometricOwners;
    public ReadOnlySpan<XtTransmitOrderRow> TransmitOrder => Storage.TransmitOrder;
    public ReadOnlySpan<XtStringRow> Strings => Storage.Strings;
    public ReadOnlySpan<byte> Payload => Storage.Payload;
}

public sealed class XtBrepBuilder
{
    private bool _finalized;
    private readonly XtBrepStorage _storage;
    public XtBrepBuilder(XtBrepCounts counts) => _storage = new XtBrepStorage(counts);
    public string? VersionText
    {
        get => _storage.VersionText;
        set
        {
            if (_finalized) throw new InvalidOperationException("XT B-rep builder is finalized.");
            _storage.VersionText = value;
        }
    }
    public Span<XtPartRow> Parts => Writable(_storage.Parts);
    public Span<XtBodyRow> Bodies => Writable(_storage.Bodies);
    public Span<XtRegionRow> Regions => Writable(_storage.Regions);
    public Span<XtShellRow> Shells => Writable(_storage.Shells);
    public Span<XtFaceRow> Faces => Writable(_storage.Faces);
    public Span<XtLoopRow> Loops => Writable(_storage.Loops);
    public Span<XtFinRow> Fins => Writable(_storage.Fins);
    public Span<XtEdgeRow> Edges => Writable(_storage.Edges);
    public Span<XtVertexRow> Vertices => Writable(_storage.Vertices);
    public Span<XtPointRow> Points => Writable(_storage.Points);
    public Span<XtGeometryRow> Geometries => Writable(_storage.Geometries);
    public Span<XtTransformRow> Transforms => Writable(_storage.Transforms);
    public Span<XtFrameRow> Frames => Writable(_storage.Frames);
    public Span<XtAssemblyRow> Assemblies => Writable(_storage.Assemblies);
    public Span<XtInstanceRow> Instances => Writable(_storage.Instances);
    public Span<XtAttributeDefinitionRow> AttributeDefinitions => Writable(_storage.AttributeDefinitions);
    public Span<XtAttributeFieldDefinitionRow> AttributeFieldDefinitions => Writable(_storage.AttributeFieldDefinitions);
    public Span<XtAttributeRow> Attributes => Writable(_storage.Attributes);
    public Span<XtAttributeValueRow> AttributeValues => Writable(_storage.AttributeValues);
    public Span<XtUserFieldRow> UserFields => Writable(_storage.UserFields);
    public Span<XtMeshRow> Meshes => Writable(_storage.Meshes);
    public Span<XtLatticeRow> Lattices => Writable(_storage.Lattices);
    public Span<XtConnectionRow> Connections => Writable(_storage.Connections);
    public Span<XtVector3> ControlPoints => Writable(_storage.ControlPoints);
    public Span<XtScalarRow> Scalars => Writable(_storage.Scalars);
    public Span<XtChartRow> Charts => Writable(_storage.Charts);
    public Span<XtLimitRow> Limits => Writable(_storage.Limits);
    public Span<XtIntersectionDataRow> IntersectionData => Writable(_storage.IntersectionData);
    public Span<XtHVectorRow> HVectors => Writable(_storage.HVectors);
    public Span<XtGeometricOwnerRow> GeometricOwners => Writable(_storage.GeometricOwners);
    public Span<XtTransmitOrderRow> TransmitOrder => Writable(_storage.TransmitOrder);
    public Span<XtStringRow> Strings => Writable(_storage.Strings);
    public Span<byte> Payload => Writable(_storage.Payload);

    public XtBrepModel FinalizeModel()
    {
        if (_finalized) throw new InvalidOperationException("XT B-rep builder is already finalized.");
        XtBrepValidator.Validate(_storage);
        _finalized = true;
        return new XtBrepModel(_storage);
    }

    private Span<T> Writable<T>(T[] table)
    {
        if (_finalized) throw new InvalidOperationException("XT B-rep builder is finalized.");
        return table;
    }
}

internal sealed class XtBrepStorage
{
    internal XtBrepStorage(XtBrepCounts c)
    {
        static T[] A<T>(XtTableCount n) => n < 0 ? throw new ArgumentOutOfRangeException(nameof(n)) : GC.AllocateUninitializedArray<T>(n, pinned: true);
        Parts=A<XtPartRow>(c.Parts); Bodies=A<XtBodyRow>(c.Bodies); Regions=A<XtRegionRow>(c.Regions); Shells=A<XtShellRow>(c.Shells);
        Faces=A<XtFaceRow>(c.Faces); Loops=A<XtLoopRow>(c.Loops); Fins=A<XtFinRow>(c.Fins); Edges=A<XtEdgeRow>(c.Edges); Vertices=A<XtVertexRow>(c.Vertices);
        Points=A<XtPointRow>(c.Points); Geometries=A<XtGeometryRow>(c.Geometries); Transforms=A<XtTransformRow>(c.Transforms); Frames=A<XtFrameRow>(c.Frames);
        Assemblies=A<XtAssemblyRow>(c.Assemblies); Instances=A<XtInstanceRow>(c.Instances); AttributeDefinitions=A<XtAttributeDefinitionRow>(c.AttributeDefinitions);
        AttributeFieldDefinitions=A<XtAttributeFieldDefinitionRow>(c.AttributeFieldDefinitions); Attributes=A<XtAttributeRow>(c.Attributes); AttributeValues=A<XtAttributeValueRow>(c.AttributeValues);
        UserFields=A<XtUserFieldRow>(c.UserFields); Meshes=A<XtMeshRow>(c.Meshes); Lattices=A<XtLatticeRow>(c.Lattices); Connections=A<XtConnectionRow>(c.Connections);
        ControlPoints=A<XtVector3>(c.ControlPoints); Scalars=A<XtScalarRow>(c.Scalars);Charts=A<XtChartRow>(c.Charts);Limits=A<XtLimitRow>(c.Limits);IntersectionData=A<XtIntersectionDataRow>(c.IntersectionData);HVectors=A<XtHVectorRow>(c.HVectors);GeometricOwners=A<XtGeometricOwnerRow>(c.GeometricOwners);TransmitOrder=A<XtTransmitOrderRow>(c.TransmitOrder); Strings=A<XtStringRow>(c.Strings); Payload=A<byte>(c.PayloadBytes);
    }
    internal readonly XtPartRow[] Parts; internal readonly XtBodyRow[] Bodies; internal readonly XtRegionRow[] Regions; internal readonly XtShellRow[] Shells;
    internal readonly XtFaceRow[] Faces; internal readonly XtLoopRow[] Loops; internal readonly XtFinRow[] Fins; internal readonly XtEdgeRow[] Edges; internal readonly XtVertexRow[] Vertices;
    internal readonly XtPointRow[] Points; internal readonly XtGeometryRow[] Geometries; internal readonly XtTransformRow[] Transforms; internal readonly XtFrameRow[] Frames;
    internal readonly XtAssemblyRow[] Assemblies; internal readonly XtInstanceRow[] Instances; internal readonly XtAttributeDefinitionRow[] AttributeDefinitions;
    internal readonly XtAttributeFieldDefinitionRow[] AttributeFieldDefinitions; internal readonly XtAttributeRow[] Attributes; internal readonly XtAttributeValueRow[] AttributeValues;
    internal readonly XtUserFieldRow[] UserFields; internal readonly XtMeshRow[] Meshes; internal readonly XtLatticeRow[] Lattices; internal readonly XtConnectionRow[] Connections;
    internal readonly XtVector3[] ControlPoints; internal readonly XtScalarRow[] Scalars;internal readonly XtChartRow[] Charts;internal readonly XtLimitRow[] Limits;internal readonly XtIntersectionDataRow[] IntersectionData;internal readonly XtHVectorRow[] HVectors;internal readonly XtGeometricOwnerRow[] GeometricOwners;internal readonly XtTransmitOrderRow[] TransmitOrder; internal readonly XtStringRow[] Strings; internal readonly byte[] Payload;
    internal string? VersionText;
    internal XtBrepCounts Counts=>new(Parts.Length,Bodies.Length,Regions.Length,Shells.Length,Faces.Length,Loops.Length,Fins.Length,Edges.Length,Vertices.Length,Points.Length,Geometries.Length,Transforms.Length,Frames.Length,Assemblies.Length,Instances.Length,AttributeDefinitions.Length,AttributeFieldDefinitions.Length,Attributes.Length,AttributeValues.Length,UserFields.Length,Meshes.Length,Lattices.Length,Connections.Length,ControlPoints.Length,Scalars.Length,Charts.Length,Limits.Length,IntersectionData.Length,HVectors.Length,GeometricOwners.Length,TransmitOrder.Length,Strings.Length,Payload.Length);
}
