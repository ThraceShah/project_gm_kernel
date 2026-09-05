namespace ProjectGmKernel.Native.Geometry.Evaluation;

/// <summary>Borrowed, validated B-spline data. Rational poles are homogeneous (wx,wy,wz,w).</summary>
internal readonly ref struct BCurveView(
    SplineDegree degree, BufferCount dimension, bool rational, bool periodic,
    ReadOnlySpan<double> vertices, ReadOnlySpan<double> knots)
{
    internal SplineDegree Degree { get; } = degree;
    internal BufferCount Dimension { get; } = dimension;
    internal bool Rational { get; } = rational;
    internal bool Periodic { get; } = periodic;
    internal ReadOnlySpan<double> Vertices { get; } = vertices;
    internal ReadOnlySpan<double> Knots { get; } = knots;
    internal BufferCount VertexCount => Vertices.Length / Dimension;
    internal double Start => Knots[Degree];
    internal double End => Knots[VertexCount];
}
