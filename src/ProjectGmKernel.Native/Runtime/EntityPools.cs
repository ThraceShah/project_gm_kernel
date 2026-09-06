using System.Runtime.InteropServices;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Entity class identifiers mapping to PK_CLASS_t values.
/// </summary>
internal enum EntityClass : int
{
    Session = 504,
    Point = 2501,
    Vector = 2502,
    Body = 1501,
    Shell = 1502,
    Face = 1503,
    Loop = 1504,
    Edge = 1505,
    Fin = 1506,
    Vertex = 1507,
    Region = 1508,
    Curve = 2002,
    Surface = 2003,
    Transform = 2500,
    Assembly = 5008,
}

/// <summary>
/// Pool kind identifiers for HandleRecord indirection.
/// </summary>
internal enum PoolKind : byte
{
    None = 0,
    FaceUse = 14,
    Point = 1,
    Vector = 2,
    Body = 3,
    Shell = 4,
    Face = 5,
    Loop = 6,
    Edge = 7,
    Fin = 8,
    Vertex = 9,
    Region = 10,
    Curve = 11,
    Surface = 12,
    Transform = 13,
    CircleData = 32,
    LineData = 33,
    CylinderData = 34,
    PlaneData = 35,
    ConeData = 36,
    SphereData = 37,
    TorusData = 38,
    BCurveData = 39,
}
