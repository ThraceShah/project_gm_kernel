using System.Runtime.InteropServices;

namespace ProjectGmKernel.Xt;

internal enum XtNodeTypes : XtNodeType
{
    Terminator = 1,
    PartTransmitBlock = 176,
    Body = 12,
    Shell = 13,
    Face = 14,
    Loop = 15,
    Edge = 16,
    Halfedge = 17,
    Vertex = 18,
    Region = 19,
    Point = 29,
    Line = 30,
    Circle = 31,
    Plane = 50,
    Cylinder = 51,
    Cone = 52,
    Sphere = 53,
    Torus = 54,
}

public enum XtFieldKind : byte
{
    Empty = 0,
    Integer = 1,
    Real = 2,
    Pointer = 3,
    Character = 4,
    Unsigned = 5,
    Logical = 6,
    Vector = 7,
    Interval = 8,
    Box = 9,
}

public readonly struct XtVector
{
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public XtVector(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct XtFieldValue
{
    public XtFieldKind Kind;
    public long Integer;
    public double Real;
    public XtNodeIndex Pointer;
    public char Character;
    public XtVector Vector;
    public double Fourth;
    public double Fifth;
    public double Sixth;

    public static XtFieldValue Int(long value) => new() { Kind = XtFieldKind.Integer, Integer = value };
    public static XtFieldValue Null() => new() { Kind = XtFieldKind.Empty };
    public static XtFieldValue RealValue(double value) => new() { Kind = XtFieldKind.Real, Real = value };
    public static XtFieldValue Ptr(XtNodeIndex value) => new() { Kind = XtFieldKind.Pointer, Pointer = value };
    public static XtFieldValue Char(char value) => new() { Kind = XtFieldKind.Character, Character = value };
    public static XtFieldValue Unsigned(long value) => new() { Kind = XtFieldKind.Unsigned, Integer = value };
    public static XtFieldValue Logical(bool value) => new() { Kind = XtFieldKind.Logical, Integer = value ? 1 : 0 };
    public static XtFieldValue Vec(double x, double y, double z) => new() { Kind = XtFieldKind.Vector, Vector = new XtVector(x, y, z) };
    public static XtFieldValue IntervalValue(double low, double high) => new()
    {
        Kind = XtFieldKind.Interval,
        Vector = new XtVector(low, high, 0),
    };
    public static XtFieldValue BoxValue(double xLow, double xHigh, double yLow, double yHigh, double zLow, double zHigh) => new()
    {
        Kind = XtFieldKind.Box,
        Vector = new XtVector(xLow, xHigh, yLow),
        Fourth = yHigh,
        Fifth = zLow,
        Sixth = zHigh,
    };
}

public sealed class XtNode
{
    public XtNodeType Type;
    public XtNodeIndex Index;
    public int VariableLength = -1;
    public XtFieldValue[] Fields = [];
    public int[] UserFields = [];
}

public sealed class XtDocument
{
    private XtSemanticModel? semanticModel;

    public string? PhysicalHeader { get; init; }
    public required string VersionText { get; init; }
    public required string HeaderSchemaIdentity { get; init; }
    public required XtSchemaDefinition Schema { get; init; }
    public XtSchemaDefinition? BaseSchema { get; init; }
    public int EmbeddedMaxNodeType { get; init; }
    public int UserFieldSize { get; init; }
    public required XtNode[] Nodes { get; init; }
    public XtSemanticModel SemanticModel => semanticModel ??= XtSemanticProjector.Project(this);
}
