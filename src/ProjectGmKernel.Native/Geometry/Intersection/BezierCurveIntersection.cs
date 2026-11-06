using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;

namespace ProjectGmKernel.Native.Geometry.Intersection;

/// <summary>Transverse space-curve roots. A certified nonzero component of
/// A' x B' selects an exact auxiliary ruled surface; its offset coordinate
/// distinguishes projected crossings from actual three-dimensional roots.</summary>
internal static class BezierCurveIntersection
{
    internal static AlgorithmStatus Find(in BezierSurfacePatch first, in BezierSurfacePatch second,
        double firstLo, double firstHi, double secondLo, double secondHi,
        Span<OperationIntersectionPoint> output, ref BufferCount count)
        =>Find(new SurfaceIntersectionPatch(first),new SurfaceIntersectionPatch(second),firstLo,firstHi,secondLo,secondHi,output,ref count);

    internal static AlgorithmStatus Find(in SurfaceIntersectionPatch first,in SurfaceIntersectionPatch second,
        double firstLo,double firstHi,double secondLo,double secondHi,
        Span<OperationIntersectionPoint> output,ref BufferCount count)
    {
        Span<ParameterCell> pending = stackalloc ParameterCell[128];
        pending[0] = new() { Lower = new() { U = firstLo, S = secondLo }, Upper = new() { U = firstHi, S = secondHi } };
        var n = 1; var visited = 0;
        Span<IntervalNumber> a = stackalloc IntervalNumber[9]; Span<IntervalNumber> b = stackalloc IntervalNumber[9];
        Span<SurfaceIntersectionNode> roots = stackalloc SurfaceIntersectionNode[64];
        while (n > 0)
        {
            if (++visited > 100000) return AlgorithmStatus.NotConverged;
            var cell = pending[--n];
            var status = first.Bound(cell.First, a); if(status==AlgorithmStatus.NotConverged)goto Subdivide;if (status != AlgorithmStatus.Success) return status;
            status = second.Bound(cell.Second, b); if(status==AlgorithmStatus.NotConverged)goto Subdivide;if (status != AlgorithmStatus.Success) return status;
            var empty = false; for (var d = 0; d < 3; d++) if (!(a[d] - b[d]).ContainsZero) empty = true;
            if (empty) continue;
            var axis = -1; var best = 0.0;
            for (var d = 0; d < 3; d++)
            {
                var e = (d + 1) % 3; var f = (d + 2) % 3; var cross = a[3 + e] * b[3 + f] - a[3 + f] * b[3 + e];
                var lower = cross.ContainsZero ? 0 : Math.Min(Math.Abs(cross.Lower), Math.Abs(cross.Upper));
                if (lower > best) { best = lower; axis = d; }
            }
            if (axis >= 0)
            {
                var height = 1.0;
                for (var d = 0; d < 3; d++) height = Math.Max(height, Math.Max(a[d].Upper, b[d].Upper) - Math.Min(a[d].Lower, b[d].Lower));
                var surface = new SurfaceIntersectionPatch(first,axis,height);
                cell.Lower.V = -height; cell.Upper.V = height;
                status = ParametricSurfaceIntersection.Isolate(surface, second, cell, 3, roots, out var nr);
                if (status != AlgorithmStatus.Success) return status;
                for (var i = 0; i < nr; i++)
                {
                    if (Math.Abs(roots[i].V) > OperationIntersection.DistanceTolerance) continue;
                    if (!OperationIntersection.AddPoint(output, ref count, roots[i].Point, roots[i].U, roots[i].S, IntersectionVectorKind.Simple))
                        return AlgorithmStatus.OutputTooSmall;
                }
                continue;
            }
            Subdivide:
            if (cell.Depth >= 48 || n + 2 > pending.Length) return AlgorithmStatus.NotConverged;
            var alongFirst = cell.Width(0) / (firstHi - firstLo) >= cell.Width(2) / (secondHi - secondLo);
            var split = alongFirst ? 0 : 2; var middle = cell.Lower.Parameter(split) + cell.Width(split) * .4931471805599453;
            var right = cell; cell.Upper.SetParameter(split, middle); right.Lower.SetParameter(split, middle); cell.Depth++; right.Depth = cell.Depth;
            pending[n++] = right; pending[n++] = cell;
        }
        return AlgorithmStatus.Success;
    }
}
