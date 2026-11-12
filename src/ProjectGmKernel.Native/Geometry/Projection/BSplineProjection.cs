using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Projection;

internal struct ProjectionPoint
{
    internal KernelVector3 Point;
    internal double U, V, DistanceUpper;
}

/// <summary>Global distance branch-and-bound on positive rational Bezier hulls.
/// Newton steps improve an upper bound; only conservative hull-distance bounds
/// discard domains. Failure to close the global distance gap is explicit.</summary>
internal static class BSplineProjection
{
    internal static AlgorithmStatus Closest(in BezierSurfacePatch patch, in KernelVector3 target,
        double tolerance, ref ProjectionPoint best)
    {
        if (!(tolerance > 0) || !double.IsFinite(tolerance) || !IsFinite(target)) return AlgorithmStatus.InvalidInput;
        Span<SurfaceParameterBox> pending = stackalloc SurfaceParameterBox[128];
        pending[0] = patch.Domain; var count = 1; var visited = 0;
        Span<IntervalNumber> bounds = stackalloc IntervalNumber[9]; Span<IntervalNumber> poles = stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
        while (count > 0)
        {
            if (++visited > 200000) return AlgorithmStatus.NotConverged;
            var box = pending[--count];
            var status = BezierSurfaceBounds.BoundPosition(patch, box, bounds[..3], poles); if (status != AlgorithmStatus.Success) return status;
            var lower = BoxDistance(bounds, target);
            if (lower >= best.DistanceUpper - tolerance) continue;
            Improve(patch, box, target, ref best);
            if (best.DistanceUpper <= tolerance) continue;
            // The position bound prepared this exact box's restricted net;
            // Improve only writes best and uses its own point workspace.
            var direction = Sub(target, best.Point); var length = OperationNorm(direction);
            if (length > 0 && double.IsFinite(length))
            {
                direction = Scale(direction, 1 / length);
                // The support-plane distance is a lower bound only for a
                // direction of norm <= 1, including normalization rounding.
                var normUpper = Math.BitIncrement(Math.Sqrt((IntervalNumber.Square(new(direction.X))
                    + IntervalNumber.Square(new(direction.Y)) + IntervalNumber.Square(new(direction.Z))).Upper));
                direction = Scale(direction, Math.BitDecrement(1 / Math.Max(1, normUpper)));
                IntervalNumber projection = new(double.NegativeInfinity, double.NegativeInfinity);
                for (var i = 0; i < (patch.UDegree + 1) * (patch.VDegree + 1); i++)
                {
                    var at = 4 * i;
                    var value = (poles[at] / poles[at + 3] - target.X) * direction.X
                        + (poles[at + 1] / poles[at + 3] - target.Y) * direction.Y + (poles[at + 2] / poles[at + 3] - target.Z) * direction.Z;
                    projection = IntervalNumber.Hull(projection, value);
                }
                lower = Math.Max(lower, -projection.Upper);
            }
            if (lower >= best.DistanceUpper - tolerance) continue;
            var uWidth = patch.UDegree == 0 ? 0 : (box.U1 - box.U0) / (patch.Domain.U1 - patch.Domain.U0);
            var vWidth = patch.VDegree == 0 ? 0 : (box.V1 - box.V0) / (patch.Domain.V1 - patch.Domain.V0);
            if (Math.Max(uWidth, vWidth) < 1e-13 || count + 2 > pending.Length) return AlgorithmStatus.NumericalFailure;
            if (uWidth >= vWidth)
            {
                var middle = box.U0 + (box.U1 - box.U0) * .5;
                pending[count++] = new(middle, box.U1, box.V0, box.V1); pending[count++] = new(box.U0, middle, box.V0, box.V1);
            }
            else
            {
                var middle = box.V0 + (box.V1 - box.V0) * .5;
                pending[count++] = new(box.U0, box.U1, middle, box.V1); pending[count++] = new(box.U0, box.U1, box.V0, middle);
            }
        }
        return AlgorithmStatus.Success;
    }

    private static void Improve(in BezierSurfacePatch patch, in SurfaceParameterBox box, in KernelVector3 target, ref ProjectionPoint best)
    {
        Span<IntervalNumber> values = stackalloc IntervalNumber[9];
        for (var seed = 0; seed < 5; seed++)
        {
            var u = seed == 0 ? (box.U0 + box.U1) * .5 : seed <= 2 ? box.U0 : box.U1;
            var v = seed == 0 ? (box.V0 + box.V1) * .5 : (seed & 1) != 0 ? box.V0 : box.V1;
            for (var iteration = 0; iteration < 18; iteration++)
            {
                if (BezierSurfaceBounds.Bound(patch, new(u, u, v, v), values) != AlgorithmStatus.Success) break;
                var point = Vector(values[0].Midpoint, values[1].Midpoint, values[2].Midpoint);
                var distance = UpperDistance(values, target);
                if (distance < best.DistanceUpper) best = new() { U = u, V = v, Point = point, DistanceUpper = distance };
                var residual = Sub(point, target);
                var du = Vector(values[3].Midpoint, values[4].Midpoint, values[5].Midpoint);
                var dv = Vector(values[6].Midpoint, values[7].Midpoint, values[8].Midpoint);
                var aa = Dot(du, du); var ab = Dot(du, dv); var bb = Dot(dv, dv); var a = Dot(residual, du); var b = Dot(residual, dv);
                double su, sv;
                if (patch.VDegree == 0) { if (!(aa > 0)) break; su = -a / aa; sv = 0; }
                else if (patch.UDegree == 0) { if (!(bb > 0)) break; su = 0; sv = -b / bb; }
                else
                {
                    var determinant = aa * bb - ab * ab; if (!(determinant > 1e-24 * aa * bb)) break;
                    su = (-a * bb + b * ab) / determinant; sv = (-b * aa + a * ab) / determinant;
                }
                var improved = false;
                for (var trial = 0; trial < 12; trial++)
                {
                    var nextU = Math.Clamp(u + su, box.U0, box.U1); var nextV = Math.Clamp(v + sv, box.V0, box.V1);
                    if (nextU == u && nextV == v) break;
                    if (BezierSurfaceBounds.BoundPosition(patch, new(nextU, nextU, nextV, nextV), values[..3]) != AlgorithmStatus.Success) break;
                    if (UpperDistance(values, target) < distance) { u = nextU; v = nextV; improved = true; break; }
                    su *= .5; sv *= .5;
                }
                if (!improved) break;
            }
        }
    }

    private static double BoxDistance(ReadOnlySpan<IntervalNumber> bounds, in KernelVector3 target)
    {
        IntervalNumber square = 0;
        for (var d = 0; d < 3; d++)
        {
            var coordinate = d == 0 ? target.X : d == 1 ? target.Y : target.Z;
            var delta = coordinate < bounds[d].Lower ? new IntervalNumber(bounds[d].Lower) - coordinate
                : coordinate > bounds[d].Upper ? new IntervalNumber(coordinate) - bounds[d].Upper : new IntervalNumber(0);
            square += IntervalNumber.Square(delta);
        }
        return Math.Max(0, Math.BitDecrement(Math.Sqrt(Math.Max(0, square.Lower))));
    }
    private static double UpperDistance(ReadOnlySpan<IntervalNumber> bounds, in KernelVector3 target)
    {
        var square = IntervalNumber.Square(bounds[0] - target.X) + IntervalNumber.Square(bounds[1] - target.Y) + IntervalNumber.Square(bounds[2] - target.Z);
        return Math.BitIncrement(Math.Sqrt(Math.Max(0, square.Upper)));
    }
    private static double OperationNorm(in KernelVector3 value)
    {
        var scale = Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        return scale == 0 ? 0 : scale * Math.Sqrt(Dot(Scale(value, 1 / scale), Scale(value, 1 / scale)));
    }
}
