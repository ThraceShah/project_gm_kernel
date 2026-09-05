using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class EvaluationMath
{
    internal static KernelVector3 Vector(double x, double y, double z) => new() { X = x, Y = y, Z = z };
    internal static KernelVector3 Add(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    internal static KernelVector3 Scale(in KernelVector3 a, double scale)
        => Vector(a.X * scale, a.Y * scale, a.Z * scale);
    internal static KernelVector3 Cross(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    internal static bool IsFinite(in KernelVector3 value)
        => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    internal static KernelVector3 Unit(in KernelVector3 value)
    {
        var scale = Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        if (!(scale > 0) || !double.IsFinite(scale))
            return default;
        var scaled = Vector(value.X / scale, value.Y / scale, value.Z / scale);
        return Scale(scaled, 1 / Math.Sqrt(scaled.X * scaled.X + scaled.Y * scaled.Y + scaled.Z * scaled.Z));
    }

    internal static (double Sin, double Cos) Differentiate(double sin, double cos, DerivativeOrder order)
        => (order & 3) switch { 0 => (sin, cos), 1 => (cos, -sin), 2 => (-sin, -cos), _ => (-cos, sin) };
}
