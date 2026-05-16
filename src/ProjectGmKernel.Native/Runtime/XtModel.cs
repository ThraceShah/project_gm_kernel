using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

internal enum XtNodeTypes : XtNodeType
{
    Terminator = 1,
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
}

internal enum XtFieldKind : byte
{
    Empty = 0,
    Integer = 1,
    Real = 2,
    Pointer = 3,
    Character = 4,
    Unsigned = 5,
    Logical = 6,
    Vector = 7,
}

internal readonly struct XtVector
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
internal struct XtFieldValue
{
    public XtFieldKind Kind;
    public long Integer;
    public double Real;
    public XtNodeIndex Pointer;
    public char Character;
    public XtVector Vector;

    public static XtFieldValue Int(long value) => new() { Kind = XtFieldKind.Integer, Integer = value };
    public static XtFieldValue Null() => new() { Kind = XtFieldKind.Empty };
    public static XtFieldValue RealValue(double value) => new() { Kind = XtFieldKind.Real, Real = value };
    public static XtFieldValue Ptr(XtNodeIndex value) => new() { Kind = XtFieldKind.Pointer, Pointer = value };
    public static XtFieldValue Char(char value) => new() { Kind = XtFieldKind.Character, Character = value };
    public static XtFieldValue Unsigned(long value) => new() { Kind = XtFieldKind.Unsigned, Integer = value };
    public static XtFieldValue Logical(bool value) => new() { Kind = XtFieldKind.Logical, Integer = value ? 1 : 0 };
    public static XtFieldValue Vec(double x, double y, double z) => new() { Kind = XtFieldKind.Vector, Vector = new XtVector(x, y, z) };
}

internal sealed class XtNode
{
    public XtNodeType Type;
    public XtNodeIndex Index;
    public XtFieldValue[] Fields = [];
}
