using ProjectGmKernel.Native.Runtime;

namespace ProjectGmKernel.Native.Geometry.Evaluation;

internal static class EvaluationMath
{
    internal static KernelVector3 Vector(double x, double y, double z) => new() { X = x, Y = y, Z = z };
    internal static KernelVector3 Add(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    internal static KernelVector3 Sub(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    internal static KernelVector3 Scale(in KernelVector3 a, double scale)
        => Vector(a.X * scale, a.Y * scale, a.Z * scale);
    internal static KernelVector3 Cross(in KernelVector3 a, in KernelVector3 b)
        => Vector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    internal static double Dot(in KernelVector3 a, in KernelVector3 b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    internal static bool IsFinite(in KernelVector3 value)
        => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    internal static bool ExactProjectionEqual(in KernelVector3 a, in KernelVector3 b, in KernelVector3 normal)
    {
        Span<double> expansion = stackalloc double[24]; var length = 0;
        Span<double> positions = stackalloc double[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
        Span<double> directions = stackalloc double[3] { normal.X, normal.Y, normal.Z };
        for (var i = 0; i < positions.Length; i++)
        {
            var coefficient = directions[i % 3] * (i < 3 ? 1 : -1);
            if (coefficient == 0 || positions[i] == 0) continue;
            var product = positions[i] * coefficient;
            // Underflow would destroy the error-free product decomposition.
            if (!double.IsFinite(product) || Math.Abs(product) < Math.ScaleB(1,-969)) return false;
            var error = Math.FusedMultiplyAdd(positions[i], coefficient, -product);
            if (!TryGrowExpansion(expansion, ref length, product) || !TryGrowExpansion(expansion, ref length, error)) return false;
        }
        for (var i = 0; i < length; i++) if (expansion[i] != 0) return false;
        return true;
    }

    private static bool TryGrowExpansion(Span<double> expansion, ref BufferCount length, double value)
    {
        var count = 0; var carry = value;
        for (var i = 0; i < length; i++)
        {
            var term = expansion[i]; var sum = carry + term; if (!double.IsFinite(sum)) return false;
            var part = sum - carry; var residual = (carry - (sum - part)) + (term - part);
            if (residual != 0) expansion[count++] = residual; carry = sum;
        }
        if (carry != 0 || count == 0) expansion[count++] = carry;
        length = count; return true;
    }

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
