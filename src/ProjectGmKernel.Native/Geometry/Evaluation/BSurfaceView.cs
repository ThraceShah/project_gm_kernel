namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>
/// Borrowed, validated B-spline surface data. Poles are u-major
/// (pole (i, j) at Vertices[(i * VVertexCount + j) * Dimension]).
/// Rational poles are homogeneous (wx, wy, wz, w).
/// </summary>
internal readonly ref struct BSurfaceView(
    SplineDegree uDegree, SplineDegree vDegree,
    BufferCount uVertexCount, BufferCount vVertexCount,
    BufferCount dimension, bool rational, bool uPeriodic, bool vPeriodic,
    ReadOnlySpan<double> vertices, ReadOnlySpan<double> uKnots, ReadOnlySpan<double> vKnots)
{
    internal SplineDegree UDegree { get; } = uDegree;
    internal SplineDegree VDegree { get; } = vDegree;
    internal BufferCount UVertexCount { get; } = uVertexCount;
    internal BufferCount VVertexCount { get; } = vVertexCount;
    internal BufferCount Dimension { get; } = dimension;
    internal bool Rational { get; } = rational;
    internal bool UPeriodic { get; } = uPeriodic;
    internal bool VPeriodic { get; } = vPeriodic;
    internal ReadOnlySpan<double> Vertices { get; } = vertices;
    internal ReadOnlySpan<double> UKnots { get; } = uKnots;
    internal ReadOnlySpan<double> VKnots { get; } = vKnots;
    internal double UStart => UKnots[UDegree];
    internal double UEnd => UKnots[UVertexCount];
    internal double VStart => VKnots[VDegree];
    internal double VEnd => VKnots[VVertexCount];
}
