using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Runtime;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Geometry.Intersection;

internal struct SurfaceIntersectionNode
{
    internal KernelVector3 Point;
    internal double U, V, S, T;
    internal readonly double Parameter(BufferOffset axis) => axis == 0 ? U : axis == 1 ? V : axis == 2 ? S : T;
    internal void SetParameter(BufferOffset axis, double value)
    { if (axis == 0) U = value; else if (axis == 1) V = value; else if (axis == 2) S = value; else T = value; }
}

internal struct SurfaceIntersectionSegment
{
    internal BufferOffset Start, End;
    internal double ChordalError;
}

internal struct ParameterCell
{
    internal SurfaceIntersectionNode Lower, Upper;
    internal BufferCount Depth;
    internal readonly double Width(BufferOffset axis) => Upper.Parameter(axis) - Lower.Parameter(axis);
    internal readonly SurfaceParameterBox First => new(Lower.U, Upper.U, Lower.V, Upper.V);
    internal readonly SurfaceParameterBox Second => new(Lower.S, Upper.S, Lower.T, Upper.T);
    internal readonly SurfaceIntersectionNode Center => new() { U = Lower.U + Width(0) * .5, V = Lower.V + Width(1) * .5,
        S = Lower.S + Width(2) * .5, T = Lower.T + Width(3) * .5 };
}

/// <summary>
/// Exhaustive regular-locus discovery in the four-dimensional parameter domain.
/// Convex-hull interval bounds reject empty cells. A contracting 3x3 Jacobian
/// minor proves a cell is a graph over its remaining coordinate: it cannot hide
/// a closed component. All boundary roots are isolated before graph arcs are
/// admitted. Unresolved singular/coincident cells fail the entire operation.
/// </summary>
internal static class ParametricSurfaceIntersection
{
    internal const BufferCount MaxCells = 200000;
    private const double ParameterTolerance = 2e-10;

    internal static AlgorithmStatus Find(in BezierSurfacePatch first, in BezierSurfacePatch second,
        Span<SurfaceIntersectionNode> nodes, ref BufferCount nodeCount,
        Span<SurfaceIntersectionSegment> segments, ref BufferCount segmentCount)
        =>FindProcedural(new SurfaceIntersectionPatch(first),new SurfaceIntersectionPatch(second),nodes,ref nodeCount,segments,ref segmentCount);

    internal static AlgorithmStatus FindProcedural(scoped in SurfaceIntersectionPatch firstSource,scoped in SurfaceIntersectionPatch secondSource,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount)
    {
        BufferCount remaining=MaxCells;
        return FindProcedural(firstSource,secondSource,nodes,ref nodeCount,segments,ref segmentCount,ref remaining);
    }

    internal static AlgorithmStatus FindProcedural(scoped in SurfaceIntersectionPatch firstSource,scoped in SurfaceIntersectionPatch secondSource,
        Span<SurfaceIntersectionNode> nodes,ref BufferCount nodeCount,Span<SurfaceIntersectionSegment> segments,ref BufferCount segmentCount,ref BufferCount remaining)
    {
        Span<SurfaceBoundCacheEntry> firstCache=stackalloc SurfaceBoundCacheEntry[256],secondCache=stackalloc SurfaceBoundCacheEntry[256];firstCache.Clear();secondCache.Clear();
        var first=new SurfaceIntersectionPatch(firstSource,firstCache);var second=new SurfaceIntersectionPatch(secondSource,secondCache);
        Span<ParameterCell> pending = stackalloc ParameterCell[256];
        var initial = new ParameterCell { Lower = new() { U = first.Domain.U0, V = first.Domain.V0, S = second.Domain.U0, T = second.Domain.V0 },
            Upper = new() { U = first.Domain.U1, V = first.Domain.V1, S = second.Domain.U1, T = second.Domain.V1 } };
        pending[0] = initial; var count = 1; var visited = 0;
        Span<IntervalNumber> f = stackalloc IntervalNumber[3]; Span<IntervalNumber> jac = stackalloc IntervalNumber[12];
        Span<SurfaceIntersectionNode> roots = stackalloc SurfaceIntersectionNode[32];
        Span<SurfaceIntersectionNode> faceRoots = stackalloc SurfaceIntersectionNode[16];
        Span<double> inverse = stackalloc double[9];Span<IntervalNumber> check=stackalloc IntervalNumber[9];
        while (count > 0)
        {
            if (++visited > MaxCells||remaining--<=0) return AlgorithmStatus.NotConverged;
            var cell = pending[--count]; var savedNodes = nodeCount; var savedSegments = segmentCount;
            var status = Bounds(first, second, cell, f, jac);if(status!=AlgorithmStatus.Success&&status!=AlgorithmStatus.NotConverged)return status;
            var bounded=status==AlgorithmStatus.Success;
            if (bounded&&(!f[0].ContainsZero || !f[1].ContainsZero || !f[2].ContainsZero)) continue;
            var free = bounded?SelectGraphAxis(jac, cell, inverse):-1; var size = 0.0;
            for (var d = 0; d < 4; d++) size = Math.Max(size, cell.Width(d) / initial.Width(d));
            // Procedural patches already follow individual spine charts. The
            // contracting graph proof and complete boundary isolation decide
            // admission; subdividing unrelated affine support coordinates to
            // a fixed fraction adds no completeness guarantee.
            var accepted = free >= 0 && (first.IsProcedural||second.IsProcedural||size <= .14);
            var nr = 0;
            if (accepted)
            {
                for (var axis = 0; axis < 4 && accepted; axis++) for (var end = 0; end < 2 && accepted; end++)
                {
                    var face = cell; var at = end == 0 ? cell.Lower.Parameter(axis) : cell.Upper.Parameter(axis);
                    face.Lower.SetParameter(axis, at); face.Upper.SetParameter(axis, at);
                    status = Bounds(first, second, face, f, jac);if(status==AlgorithmStatus.NotConverged){accepted=false;break;}if(status!=AlgorithmStatus.Success)return status;
                    var containedBoundary = false;
                    for (var row = 0; row < 3; row++)
                    {
                        if (f[row].Magnitude != 0) continue;
                        var redundant = true; for (var column = 0; column < 4; column++) if (column != axis && jac[4 * row + column].Magnitude != 0) redundant = false;
                        containedBoundary |= redundant;
                    }
                    // With a nonsingular graph minor, a constraint that
                    // vanishes identically on a face describes a contained
                    // boundary arc. Its ends occur on the other faces.
                    if (containedBoundary) continue;
                    status = Isolate(first, second, face, axis, faceRoots, out var n);
                    if (status != AlgorithmStatus.Success) { accepted = false; break; }
                    for (var i = 0; i < n; i++)
                    {
                        var duplicate = false; for (var j = 0; j < nr; j++) if (SameParameters(faceRoots[i], roots[j], initial)) duplicate = true;
                        if (duplicate) continue;
                        if (nr == roots.Length) { accepted = false; break; } roots[nr++] = faceRoots[i];
                    }
                }
                if (accepted)
                {
                    for (var i = 1; i < nr; i++) for (var j = i; j > 0 && roots[j].Parameter(free) < roots[j - 1].Parameter(free); j--)
                        (roots[j], roots[j - 1]) = (roots[j - 1], roots[j]);
                    for (var i = 0; i + 1 < nr; i++)
                    {
                        var lo = roots[i].Parameter(free); var hi = roots[i + 1].Parameter(free);
                        // A regular graph coordinate may move by very little
                        // while its spatial arc is long (near an isocurve).
                        // Never discard that arc using a global UV threshold.
                        var sectionParameter = lo + (hi - lo) * .5;
                        if (!(sectionParameter > lo && sectionParameter < hi))
                        {
                            if (SameParameters(roots[i], roots[i + 1], initial)) continue;
                            accepted = false; break;
                        }
                        var section = cell; section.Lower.SetParameter(free, sectionParameter); section.Upper.SetParameter(free, sectionParameter);
                        status = Isolate(first, second, section, free, faceRoots, out var n);
                        if (status != AlgorithmStatus.Success || n > 1) { accepted = false; break; }
                        if (n == 0) continue;
                        status = AddSegment(first,cell,roots[i], roots[i + 1], initial, nodes, ref nodeCount, segments, ref segmentCount);
                        if (status != AlgorithmStatus.Success) return status;
                    }
                }
            }
            if (accepted) continue;
            // No partially verified arcs from this cell may survive a retry.
            // Admission above can only fail while isolating a later gap; use
            // a checkpoint for both result buffers.
            nodeCount = savedNodes; segmentCount = savedSegments;
            if (cell.Depth >= 48 || count + 2 > pending.Length) return AlgorithmStatus.NotConverged;
            var split = 0; var widest = 0.0;
            for (var d = 0; d < 4; d++) if (cell.Width(d) / initial.Width(d) > widest) { split = d; widest = cell.Width(d) / initial.Width(d); }
            if(!bounded){if(first.IsProcedural&&first.Bound(cell.First,check)==AlgorithmStatus.NotConverged)split=0;else if(second.IsProcedural&&second.Bound(cell.Second,check)==AlgorithmStatus.NotConverged)split=2;}
            var middle = cell.Lower.Parameter(split) + cell.Width(split) * .4931471805599453;
            if (!(middle > cell.Lower.Parameter(split) && middle < cell.Upper.Parameter(split))) return AlgorithmStatus.NumericalFailure;
            var right = cell; cell.Upper.SetParameter(split, middle); right.Lower.SetParameter(split, middle); cell.Depth++; right.Depth = cell.Depth;
            pending[count++] = right; pending[count++] = cell;
        }
        return AlgorithmStatus.Success;
    }

    internal static AlgorithmStatus Bounds(in BezierSurfacePatch first, in BezierSurfacePatch second,
        in ParameterCell cell, Span<IntervalNumber> residual, Span<IntervalNumber> jacobian)
        =>Bounds(new SurfaceIntersectionPatch(first),new SurfaceIntersectionPatch(second),cell,residual,jacobian);

    internal static AlgorithmStatus Bounds(in SurfaceIntersectionPatch first,in SurfaceIntersectionPatch second,
        in ParameterCell cell,Span<IntervalNumber> residual,Span<IntervalNumber> jacobian)
    {
        Span<IntervalNumber> a = stackalloc IntervalNumber[9]; Span<IntervalNumber> b = stackalloc IntervalNumber[9];
        var status = first.Bound(cell.First, a); if (status != AlgorithmStatus.Success) return status;
        status = second.Bound(cell.Second, b); if (status != AlgorithmStatus.Success) return status;
        for (var d = 0; d < 3; d++)
        {
            residual[d] = a[d] - b[d]; jacobian[4 * d] = a[3 + d]; jacobian[4 * d + 1] = a[6 + d];
            jacobian[4 * d + 2] = -b[3 + d]; jacobian[4 * d + 3] = -b[6 + d];
        }
        return AlgorithmStatus.Success;
    }

    private static BufferOffset SelectGraphAxis(ReadOnlySpan<IntervalNumber> jac, in ParameterCell cell, Span<double> inverse)
    {
        Span<BufferOffset> axes = stackalloc BufferOffset[3];
        for (var free = 0; free < 4; free++)
        {
            Axes(free, axes);
            if (!Invert(jac, axes, inverse)) continue;
            if (Contraction(jac, axes, inverse, cell) < .7) return free;
        }
        return -1;
    }

    internal static AlgorithmStatus Isolate(in BezierSurfacePatch first, in BezierSurfacePatch second,
        in ParameterCell domain, BufferOffset fixedAxis, Span<SurfaceIntersectionNode> roots, out BufferCount rootCount)
        =>Isolate(new SurfaceIntersectionPatch(first),new SurfaceIntersectionPatch(second),domain,fixedAxis,roots,out rootCount);

    internal static AlgorithmStatus Isolate(in SurfaceIntersectionPatch first,in SurfaceIntersectionPatch second,
        in ParameterCell domain,BufferOffset fixedAxis,Span<SurfaceIntersectionNode> roots,out BufferCount rootCount)
    {
        rootCount = 0;
        Span<ParameterCell> pending = stackalloc ParameterCell[160]; pending[0] = domain; var count = 1; var visited = 0;
        Span<IntervalNumber> f = stackalloc IntervalNumber[3]; Span<IntervalNumber> jac = stackalloc IntervalNumber[12];
        Span<IntervalNumber> centerF = stackalloc IntervalNumber[3]; Span<IntervalNumber> centerJac = stackalloc IntervalNumber[12];
        Span<BufferOffset> axes = stackalloc BufferOffset[3]; Axes(fixedAxis, axes);
        Span<double> inverse = stackalloc double[9];
        Span<IntervalNumber> position = stackalloc IntervalNumber[9];
        while (count > 0)
        {
            if (++visited > 12000) return AlgorithmStatus.NotConverged;
            var cell = pending[--count];var bounded=false;var status = Bounds(first, second, cell, f, jac);if(status==AlgorithmStatus.NotConverged)goto Subdivide;bounded=true;if(status!=AlgorithmStatus.Success)return status;
            if (!f[0].ContainsZero || !f[1].ContainsZero || !f[2].ContainsZero) continue;
            var point = cell.Center;
            if (Invert(jac, axes, inverse) && Contraction(jac, axes, inverse, cell) < .8)
            {
                for (var iteration = 0; iteration < 12; iteration++)
                {
                    var singleton = new ParameterCell { Lower = point, Upper = point };
                    status = Bounds(first, second, singleton, centerF, centerJac);if(status==AlgorithmStatus.NotConverged)goto Subdivide;if(status!=AlgorithmStatus.Success)return status;
                    if (!Invert(centerJac, axes, inverse)) break;
                    var step = 0.0;
                    for (var r = 0; r < 3; r++)
                    {
                        var delta = 0.0; for (var c = 0; c < 3; c++) delta += inverse[3 * r + c] * centerF[c].Midpoint;
                        var axis = axes[r]; var next = Math.Clamp(point.Parameter(axis) - delta, cell.Lower.Parameter(axis), cell.Upper.Parameter(axis));
                        step = Math.Max(step, Math.Abs(next - point.Parameter(axis))); point.SetParameter(axis, next);
                    }
                    if (step < 2e-14) break;
                }
                var anchor = new ParameterCell { Lower = point, Upper = point };
                status = Bounds(first, second, anchor, centerF, centerJac);if(status==AlgorithmStatus.NotConverged)goto Subdivide;if(status!=AlgorithmStatus.Success)return status;
                var empty = false; var unique = true;
                // Certify on a small extension of the unknown domain, allowing
                // a root on a shared cell boundary to be isolated by both sides.
                var expanded = cell;
                for (var r = 0; r < 3; r++)
                {
                    var axis = axes[r]; var margin = Math.Max(cell.Width(axis) * .05, 1e-11);
                    expanded.Lower.SetParameter(axis, cell.Lower.Parameter(axis) - margin); expanded.Upper.SetParameter(axis, cell.Upper.Parameter(axis) + margin);
                }
                status = Bounds(first, second, expanded, f, jac);if(status==AlgorithmStatus.NotConverged)goto Subdivide;if(status!=AlgorithmStatus.Success)return status;
                if (!Invert(centerJac, axes, inverse)) unique = false;
                for (var r = 0; r < 3 && unique; r++)
                {
                    IntervalNumber image = point.Parameter(axes[r]);
                    for (var k = 0; k < 3; k++) image -= inverse[3 * r + k] * centerF[k];
                    for (var c = 0; c < 3; c++)
                    {
                        IntervalNumber value = r == c ? 1 : 0;
                        for (var k = 0; k < 3; k++) value -= inverse[3 * r + k] * jac[4 * k + axes[c]];
                        image += value * (new IntervalNumber(expanded.Lower.Parameter(axes[c]), expanded.Upper.Parameter(axes[c])) - point.Parameter(axes[c]));
                    }
                    var axis = axes[r];
                    if (image.Upper < cell.Lower.Parameter(axis) || image.Lower > cell.Upper.Parameter(axis)) empty = true;
                    unique &= image.Lower > expanded.Lower.Parameter(axis) && image.Upper < expanded.Upper.Parameter(axis);
                }
                if (empty) continue;
                if (unique && centerF[0].Magnitude < 1e-9 && centerF[1].Magnitude < 1e-9 && centerF[2].Magnitude < 1e-9)
                {
                    status = first.Bound(anchor.First, position); if (status != AlgorithmStatus.Success) return status;
                    point.Point = Vector(position[0].Midpoint, position[1].Midpoint, position[2].Midpoint);
                    var duplicate = false;
                    for (var i = 0; i < rootCount; i++) if (SameParameters(roots[i], point, domain)) duplicate = true;
                    if (!duplicate) { if (rootCount == roots.Length) return AlgorithmStatus.OutputTooSmall; roots[rootCount++] = point; }
                    continue;
                }
            }
            Subdivide:
            if (cell.Depth >= 44 || count + 2 > pending.Length) return AlgorithmStatus.NotConverged;
            var split = axes[0]; var width = 0.0;
            for (var i = 0; i < 3; i++)
            {
                var axis = axes[i]; var relative = cell.Width(axis) / Math.Max(domain.Width(axis), 1e-30);
                if (relative > width) { split = axis; width = relative; }
            }
            if(!bounded){if(fixedAxis!=0&&first.IsProcedural&&first.Bound(cell.First,position)==AlgorithmStatus.NotConverged)split=0;else if(fixedAxis!=2&&second.IsProcedural&&second.Bound(cell.Second,position)==AlgorithmStatus.NotConverged)split=2;}
            var middle = cell.Lower.Parameter(split) + cell.Width(split) * .4931471805599453;
            if (!(middle > cell.Lower.Parameter(split) && middle < cell.Upper.Parameter(split))) return AlgorithmStatus.NumericalFailure;
            var right = cell; cell.Upper.SetParameter(split, middle); right.Lower.SetParameter(split, middle); cell.Depth++; right.Depth = cell.Depth;
            pending[count++] = right; pending[count++] = cell;
        }
        return AlgorithmStatus.Success;
    }

    private static void Axes(BufferOffset excluded, Span<BufferOffset> axes)
    { var n = 0; for (var d = 0; d < 4; d++) if (d != excluded) axes[n++] = d; }

    private static bool Invert(ReadOnlySpan<IntervalNumber> jac, ReadOnlySpan<BufferOffset> axes, Span<double> inverse)
    {
        Span<double> lu = stackalloc double[9]; Span<BufferOffset> pivots = stackalloc BufferOffset[3]; Span<double> rhs = stackalloc double[3];
        for (var r = 0; r < 3; r++) for (var c = 0; c < 3; c++) lu[3 * r + c] = jac[4 * r + axes[c]].Midpoint;
        if (SmallLinearSolve.LuFactorize(lu, 3, pivots) != AlgorithmStatus.Success) return false;
        for (var c = 0; c < 3; c++)
        {
            rhs.Clear(); rhs[c] = 1; if (SmallLinearSolve.LuSolveInPlace(lu, 3, pivots, rhs) != AlgorithmStatus.Success) return false;
            for (var r = 0; r < 3; r++) inverse[3 * r + c] = rhs[r];
        }
        return true;
    }

    private static double Contraction(ReadOnlySpan<IntervalNumber> jac, ReadOnlySpan<BufferOffset> axes,
        ReadOnlySpan<double> inverse, in ParameterCell cell)
    {
        var norm = 0.0;
        for (var r = 0; r < 3; r++)
        {
            var row = 0.0;
            for (var c = 0; c < 3; c++)
            {
                IntervalNumber value = r == c ? 1 : 0;
                for (var k = 0; k < 3; k++) value -= inverse[3 * r + k] * jac[4 * k + axes[c]];
                row = Math.BitIncrement(row + (new IntervalNumber(value.Magnitude)
                    * (new IntervalNumber(cell.Upper.Parameter(axes[c])) - cell.Lower.Parameter(axes[c]))
                    / Math.Max(cell.Width(axes[r]), 1e-30)).Upper);
            }
            norm = Math.Max(norm, row);
        }
        return norm;
    }

    private static bool SameParameters(in SurfaceIntersectionNode a, in SurfaceIntersectionNode b, in ParameterCell domain)
    {
        for (var d = 0; d < 4; d++) if (Math.Abs(a.Parameter(d) - b.Parameter(d)) > ParameterTolerance * Math.Max(domain.Width(d), 1)) return false;
        return true;
    }

    private static AlgorithmStatus AddSegment(in SurfaceIntersectionPatch patch,in ParameterCell cell,in SurfaceIntersectionNode a, in SurfaceIntersectionNode b, in ParameterCell domain,
        Span<SurfaceIntersectionNode> nodes, ref BufferCount nodeCount, Span<SurfaceIntersectionSegment> segments, ref BufferCount segmentCount)
    {
        var start = -1; var end = -1;
        for (var i = 0; i < nodeCount; i++) { if (SameParameters(nodes[i], a, domain)) start = i; if (SameParameters(nodes[i], b, domain)) end = i; }
        if (start < 0) { if (nodeCount == nodes.Length) return AlgorithmStatus.OutputTooSmall; start = nodeCount; nodes[nodeCount++] = a; }
        if (end < 0) { if (nodeCount == nodes.Length) return AlgorithmStatus.OutputTooSmall; end = nodeCount; nodes[nodeCount++] = b; }
        if (start == end) return AlgorithmStatus.NumericalFailure;
        for (var i = 0; i < segmentCount; i++) if (segments[i].Start == start && segments[i].End == end || segments[i].Start == end && segments[i].End == start) return AlgorithmStatus.Success;
        if (segmentCount == segments.Length) return AlgorithmStatus.OutputTooSmall;
        Span<IntervalNumber> bounds=stackalloc IntervalNumber[9];var status=patch.Bound(cell.First,bounds);if(status!=AlgorithmStatus.Success)return status;
        var chord=Sub(b.Point,a.Point);var square=Dot(chord,chord);var error=0.0;
        for(var corner=0;corner<8;corner++)
        {
            var point=Vector((corner&1)==0?bounds[0].Lower:bounds[0].Upper,(corner&2)==0?bounds[1].Lower:bounds[1].Upper,(corner&4)==0?bounds[2].Lower:bounds[2].Upper);
            var t=square>0?Math.Clamp(Dot(Sub(point,a.Point),chord)/square,0,1):0;
            error=Math.Max(error,OperationIntersection.Norm(Sub(point,Add(a.Point,Scale(chord,t)))));
        }
        error=error*(1+1e-12)+2e-12*Math.Max(1,OperationIntersection.Norm(a.Point));
        segments[segmentCount++] = new() { Start = start, End = end,ChordalError=error }; return AlgorithmStatus.Success;
    }
}
