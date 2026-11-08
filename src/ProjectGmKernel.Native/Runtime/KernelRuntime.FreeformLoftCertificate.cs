using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A sufficient, continuous certificate: each interpolated section is a strictly
    // convex polygon with strictly contained, disjoint convex holes; all section
    // planes are parallel and their height is strictly monotone. Every inequality
    // is proved on a whole polynomial interval using outward-rounded Bernstein
    // coefficients. A failed certificate is not treated as evidence of validity.
    private static int FreeformCertifyPolygonLoft(ReadOnlySpan<EdgeSlot> edges, ReadOnlySpan<byte> senses,
        ReadOnlySpan<int> offsets, ReadOnlySpan<double> parameters, in KernelVector3 normal, KernelSense direction,
        out bool certified)
    {
        certified = false; var sections = parameters.Length; var vertices = offsets[^1];
        for (var p = 0; p < sections; p++) for (var e = 0; e < vertices; e++)
        {
            var slot = GetCurveSlotByTag(Edges[edges[p * 128 + e]].CurveTag);
            if (slot < 0 || Curves[slot].Class != CurveClass.Line) return 0;
        }
        var degree = sections == 2 ? 1 : 3; var rows = degree * (sections - 1) + 1;
        var trajectories = CommandScratchBorrow(vertices * rows * 3);
        if (trajectories.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            Span<double> samples = stackalloc double[sections * 3]; Span<double> diagonal = stackalloc double[sections];
            Span<double> seconds = stackalloc double[sections * 3];
            for (var e = 0; e < vertices; e++)
            {
                for (var p = 0; p < sections; p++)
                {
                    var point = FreeformEdgeStart(edges[p * 128 + e], senses[p * 128 + e]);
                    samples[p * 3] = point.X; samples[p * 3 + 1] = point.Y; samples[p * 3 + 2] = point.Z;
                }
                if (degree == 1) samples.CopyTo(trajectories.Slice(e * rows * 3, rows * 3));
                else if (!FreeformSplineMath.Interpolate(parameters, samples, 3, default, default,
                    trajectories.Slice(e * rows * 3, rows * 3), diagonal, seconds)) return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            for (var k = 0; k < rows; k++)
            {
                var reference = FreeformTrajectoryPoint(trajectories, rows, 0, k);
                for (var e = 1; e < vertices; e++)
                {
                    var point = FreeformTrajectoryPoint(trajectories, rows, e, k);
                    if (!ExactProjectionEqual(point, reference, normal)) return 0;
                }
                if (k > 0)
                {
                    var previous = FreeformTrajectoryPoint(trajectories, rows, 0, k - 1);
                    var height = FreeformDotDifference(reference, previous, normal);
                    if (!double.IsFinite(height.Low) || !double.IsFinite(height.High)) return 0;
                    if (direction > 0 ? !(height.Low > 0) : !(height.High < 0)) return 0;
                }
            }
            for (var segment = 0; segment < sections - 1; segment++)
            {
                var start = segment * degree;
                for (var loop = 0; loop + 1 < offsets.Length; loop++)
                {
                    var begin = offsets[loop]; var end = offsets[loop + 1];
                    for (var e = begin; e < end; e++)
                    {
                        var next = e + 1 == end ? begin : e + 1;
                        var stop = loop == 0 ? vertices : end;
                        for (var point = begin; point < stop; point++)
                        {
                            if (point == e || point == next) continue;
                            if (!FreeformHalfPlanePositive(trajectories, rows, start, degree, e, next, point, normal, loop == 0 ? 1 : -1)) return 0;
                        }
                    }
                }
                for (var a = 1; a + 1 < offsets.Length; a++) for (var b = a + 1; b + 1 < offsets.Length; b++)
                    if (!FreeformHolesSeparated(trajectories, rows, start, degree, offsets[a], offsets[a + 1], offsets[b], offsets[b + 1], normal)
                        && !FreeformHolesSeparated(trajectories, rows, start, degree, offsets[b], offsets[b + 1], offsets[a], offsets[a + 1], normal)) return 0;
            }
            certified = true; return 0;
        }
        finally { CommandScratchReturn(trajectories); }
    }

    private static bool FreeformHolesSeparated(ReadOnlySpan<double> points, BufferCount rows, BufferOffset segment,
        SplineDegree degree, BufferOffset a0, BufferOffset a1, BufferOffset b0, BufferOffset b1, in KernelVector3 normal)
    {
        for (var e = a0; e < a1; e++)
        {
            var next = e + 1 == a1 ? a0 : e + 1; var separated = true;
            for (var p = b0; p < b1; p++)
                if (!FreeformHalfPlanePositive(points, rows, segment, degree, e, next, p, normal, 1)) { separated = false; break; }
            if (separated) return true;
        }
        return false;
    }

    private static KernelVector3 FreeformTrajectoryPoint(ReadOnlySpan<double> points, BufferCount rows, BufferOffset vertex, BufferOffset row)
    {
        var index = (vertex * rows + row) * 3; return Vector(points[index], points[index + 1], points[index + 2]);
    }

    private static bool FreeformHalfPlanePositive(ReadOnlySpan<double> points, BufferCount rows, BufferOffset segment,
        SplineDegree degree, BufferOffset a, BufferOffset b, BufferOffset c, in KernelVector3 normal, KernelSense sense)
    {
        Span<FreeformInterval> coefficients = stackalloc FreeformInterval[7]; coefficients.Clear();
        for (var i = 0; i <= degree; i++) for (var j = 0; j <= degree; j++)
        {
            var ai = FreeformTrajectoryPoint(points, rows, a, segment + i);
            var bi = FreeformTrajectoryPoint(points, rows, b, segment + i);
            var aj = FreeformTrajectoryPoint(points, rows, a, segment + j);
            var cj = FreeformTrajectoryPoint(points, rows, c, segment + j);
            var x = FreeformInterval.Difference(bi.X, ai.X); var y = FreeformInterval.Difference(bi.Y, ai.Y); var z = FreeformInterval.Difference(bi.Z, ai.Z);
            var u = FreeformInterval.Difference(cj.X, aj.X); var v = FreeformInterval.Difference(cj.Y, aj.Y); var w = FreeformInterval.Difference(cj.Z, aj.Z);
            var determinant = FreeformInterval.Add(FreeformInterval.Multiply(FreeformInterval.Subtract(FreeformInterval.Multiply(y, w), FreeformInterval.Multiply(z, v)), normal.X),
                FreeformInterval.Add(FreeformInterval.Multiply(FreeformInterval.Subtract(FreeformInterval.Multiply(z, u), FreeformInterval.Multiply(x, w)), normal.Y),
                    FreeformInterval.Multiply(FreeformInterval.Subtract(FreeformInterval.Multiply(x, v), FreeformInterval.Multiply(y, u)), normal.Z)));
            var weight = (double)(FreeformBinomial(degree, i) * FreeformBinomial(degree, j)) / FreeformBinomial(2 * degree, i + j);
            var enclosure = new FreeformInterval { Low = Math.BitDecrement(weight), High = Math.BitIncrement(weight) };
            coefficients[i + j] = FreeformInterval.Add(coefficients[i + j], FreeformInterval.Multiply(determinant, enclosure));
        }
        for (var k = 0; k <= 2 * degree; k++)
            if (!double.IsFinite(coefficients[k].Low) || !double.IsFinite(coefficients[k].High)
                || (sense > 0 ? !(coefficients[k].Low > 0) : !(coefficients[k].High < 0))) return false;
        return true;
    }

    private static int FreeformBinomial(SplineDegree degree, BufferOffset index)
    {
        var result = 1;
        for (var i = 1; i <= index; i++) result = result * (degree - i + 1) / i;
        return result;
    }

    private static FreeformInterval FreeformDotDifference(in KernelVector3 a, in KernelVector3 b, in KernelVector3 normal)
        => FreeformInterval.Add(FreeformInterval.Multiply(FreeformInterval.Difference(a.X, b.X), normal.X),
            FreeformInterval.Add(FreeformInterval.Multiply(FreeformInterval.Difference(a.Y, b.Y), normal.Y),
                FreeformInterval.Multiply(FreeformInterval.Difference(a.Z, b.Z), normal.Z)));

    private struct FreeformInterval
    {
        internal double Low, High;
        internal static FreeformInterval Difference(double a, double b)
            => a == b ? default : new() { Low = Math.BitDecrement(a - b), High = Math.BitIncrement(a - b) };
        internal static FreeformInterval Add(FreeformInterval a, FreeformInterval b)
            => new() { Low = Math.BitDecrement(a.Low + b.Low), High = Math.BitIncrement(a.High + b.High) };
        internal static FreeformInterval Subtract(FreeformInterval a, FreeformInterval b)
            => new() { Low = Math.BitDecrement(a.Low - b.High), High = Math.BitIncrement(a.High - b.Low) };
        internal static FreeformInterval Multiply(FreeformInterval a, double b)
            => b == 0 ? default : Multiply(a, new FreeformInterval { Low = b, High = b });
        internal static FreeformInterval Multiply(FreeformInterval a, FreeformInterval b)
        {
            var p = a.Low * b.Low; var q = a.Low * b.High; var r = a.High * b.Low; var s = a.High * b.High;
            return new() { Low = Math.BitDecrement(Math.Min(Math.Min(p, q), Math.Min(r, s))), High = Math.BitIncrement(Math.Max(Math.Max(p, q), Math.Max(r, s))) };
        }
    }

    private static int FreeformPolygonSectionCurve(EdgeSlot edge, byte forward, out CurveTag curve)
    {
        var a = FreeformEdgeStart(edge, forward); var b = FreeformEdgeStart(edge, (byte)(forward == 0 ? 1 : 0));
        Span<double> poles = stackalloc double[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
        Span<double> knots = stackalloc double[2] { 0, 1 }; Span<int> mults = stackalloc int[2] { 2, 2 };
        CurveTag output = 0; var error = CreateFreeformCurve(1, poles, 3, false, knots, mults, false, &output); curve = output; return error;
    }
}
