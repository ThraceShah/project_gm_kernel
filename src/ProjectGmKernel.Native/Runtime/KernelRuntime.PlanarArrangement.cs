using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    internal struct PlanarModelFace
    {
        internal FaceSlot Source;
        internal SurfTag Surface;
        internal KernelSense Sense;
        internal BufferOffset Body, FirstLoop;
        internal BufferCount LoopCount;
        internal KernelVector3 Origin, Normal;
    }

    internal struct PlanarModelLoop
    {
        internal BufferOffset First;
        internal BufferCount Count;
    }

    internal struct PlanarModelPolygon
    {
        internal BufferOffset First, Face;
        internal BufferCount Count;
        internal uint Classification;
        internal byte Reversed;
    }

    private const int PlanarFaceCapacity = 512;
    private const int PlanarPointCapacity = 8192;
    private const int PlanarPolygonCapacity = 2048;

    /// <summary>Read exact straight-edge planar B-reps, including non-convex
    /// faces and inner loops. A planar spline support is retained as a spline;
    /// neither it nor a polygon boundary is approximated by a display mesh.</summary>
    private static int ReadPlanarModel(ReadOnlySpan<EntityTag> bodies, Span<PlanarModelFace> faces,
        Span<PlanarModelLoop> loops, Span<KernelVector3> points, out int faceCount,
        out int loopCount, out int pointCount, out double tolerance)
    {
        faceCount = loopCount = pointCount = 0; tolerance = 1e-10;
        for (var b = 0; b < bodies.Length; b++)
        {
            if (!TryResolveBodySlot(bodies[b], out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
            if (Bodies[slot].Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
            if (Xt.IsOpaque(slot)) return ParasolidConstants.PK_ERROR_not_implemented;
            ref readonly var body = ref Bodies[slot];
            var f = body.FirstFaceBody;
            for (var fi = 0; fi < body.FaceCountBody; fi++, f = Faces[f].NextInBody)
            {
                if (faceCount == faces.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                ref readonly var face = ref Faces[f];
                if (GetSurfaceSlotByTag(face.SurfTag) < 0) return ParasolidConstants.PK_ERROR_missing_geom;
                if (face.LoopCount <= 0) return ParasolidConstants.PK_ERROR_not_implemented;
                var descriptor = new PlanarModelFace { Source = f, Surface = face.SurfTag,
                    Sense = face.Orientation == ParasolidConstants.PK_TOPOL_sense_negative_c ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c,
                    Body = b, FirstLoop = loopCount, LoopCount = face.LoopCount };
                var loop = face.FirstLoop;
                for (var li = 0; li < face.LoopCount; li++, loop = Loops[loop].NextInFace)
                {
                    if (loopCount == loops.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                    ref readonly var l = ref Loops[loop];
                    if (l.FinCount < 3 || l.FinCount > points.Length - pointCount) return ParasolidConstants.PK_ERROR_not_implemented;
                    var first = pointCount;
                    var fin = l.FirstFin;
                    for (var k = 0; k < l.FinCount; k++, fin = Fins[fin].NextInLoop)
                    {
                        var edge = Fins[fin].Edge;
                        if (edge < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                        ref readonly var e = ref Edges[edge];
                        var vertex = Fins[fin].Sense == '+' ? e.StartVertex : e.EndVertex;
                        if (vertex < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                        var pointSlot = GetPointSlotByTag(Vertices[vertex].PointTag);
                        if (pointSlot < 0 || !HasBoundaryEdgeGeometry(in e)) return ParasolidConstants.PK_ERROR_missing_geom;
                        if (!PlanarStraightEdge(in e)) return ParasolidConstants.PK_ERROR_not_implemented;
                        var p = Points[pointSlot].Position;
                        if (!ProfileFinite(in p)) return ParasolidConstants.PK_ERROR_bad_parameter;
                        points[pointCount++] = p;
                        tolerance = Math.Max(tolerance, 128 * 2.220446049250313e-16 * Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))));
                    }
                    loops[loopCount++] = new PlanarModelLoop { First = first, Count = pointCount - first };
                }
                var outer = loops[descriptor.FirstLoop];
                descriptor.Origin = points[outer.First];
                var n = Vector(0, 0, 0);
                for (var k = 1; k + 1 < outer.Count; k++)
                    n = ProfileAdd(n, ProfileCross(ProfileSubtract(points[outer.First + k], descriptor.Origin),
                        ProfileSubtract(points[outer.First + k + 1], descriptor.Origin)));
                if (!ProfileNormalize(ref n)) return ParasolidConstants.PK_ERROR_bad_loop;
                descriptor.Normal = n;
                // The outer-loop traversal establishes outward face orientation;
                // require every boundary and every spline pole to lie on it.
                for (var k = loops[descriptor.FirstLoop].First; k < pointCount; k++)
                    if (Math.Abs(ProfileDot(ProfileSubtract(points[k], descriptor.Origin), n)) > tolerance * 8)
                        return ParasolidConstants.PK_ERROR_not_implemented;
                if (!PlanarSupport(face.SurfTag, descriptor.Origin, n, tolerance)) return ParasolidConstants.PK_ERROR_not_implemented;
                faces[faceCount++] = descriptor;
            }
        }
        return 0;
    }

    private static bool PlanarStraightEdge(in EdgeRecord edge)
    {
        var slot = GetCurveSlotByTag(edge.CurveTag);
        if (slot < 0) return false;
        ref readonly var c = ref Curves[slot];
        if (c.Class == CurveClass.Line) return true;
        if (c.Class != CurveClass.BCurve || edge.StartVertex < 0 || edge.EndVertex < 0) return false;
        var firstPoint = GetPointSlotByTag(Vertices[edge.StartVertex].PointTag);
        var secondPoint = GetPointSlotByTag(Vertices[edge.EndVertex].PointTag);
        if (firstPoint < 0 || secondPoint < 0) return false;
        var a = Points[firstPoint].Position;
        var b = Points[secondPoint].Position;
        var direction = ProfileSubtract(b, a);
        if (!ProfileNormalize(ref direction)) return false;
        ref readonly var spline = ref BCurveDataStore[c.DataIndex];
        var poles = spline.PolesSpan();
        for (var i = 0; i < spline.NVertices; i++)
        {
            var offset = i * spline.VertexDim;
            var w = spline.IsRational != 0 ? poles[offset + 3] : 1;
            if (!(w > 0) || !double.IsFinite(w)) return false;
            var p = Vector(poles[offset] / w, poles[offset + 1] / w, poles[offset + 2] / w);
            if (!ProfileFinite(in p)) return false;
            if (ProfileDot(ProfileCross(ProfileSubtract(p, a), direction), ProfileCross(ProfileSubtract(p, a), direction)) > 1e-20)
                return false;
        }
        return true;
    }

    private static bool PlanarSupport(SurfTag tag, in KernelVector3 origin, in KernelVector3 normal, double tolerance)
    {
        var slot = GetSurfaceSlotByTag(tag);
        if (slot < 0) return false;
        ref readonly var s = ref Surfaces[slot];
        if (s.Class == SurfaceClass.Plane) return true;
        if (s.Class != SurfaceClass.BSurface) return false;
        ref readonly var data = ref BSurfaceDataStore[s.DataIndex];
        var poles = data.PolesSpan();
        for (var i = 0; i < poles.Length; i += data.VertexDim)
        {
            var w = data.IsRational != 0 ? poles[i + 3] : 1;
            if (!(w > 0) || !double.IsFinite(w)) return false;
            var p = Vector(poles[i] / w, poles[i + 1] / w, poles[i + 2] / w);
            if (!ProfileFinite(in p)) return false;
            if (Math.Abs(ProfileDot(ProfileSubtract(p, origin), normal)) > tolerance * 8) return false;
        }
        return true;
    }

    private static bool PlanarFaceContains(in PlanarModelFace face, ReadOnlySpan<PlanarModelLoop> loops,
        ReadOnlySpan<KernelVector3> points, in KernelVector3 point)
    {
        var n = face.Normal;
        var axis = Math.Abs(n.X) > Math.Abs(n.Y) ? (Math.Abs(n.X) > Math.Abs(n.Z) ? 0 : 2) : (Math.Abs(n.Y) > Math.Abs(n.Z) ? 1 : 2);
        var inside = false;
        for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
        {
            var loop = loops[l];
            for (var k = 0; k < loop.Count; k++)
            {
                var a = ProfileSubtract(points[loop.First + k], point);
                var b = ProfileSubtract(points[loop.First + (k + 1) % loop.Count], point);
                var ax = axis == 0 ? a.Y : a.X; var ay = axis == 2 ? a.Y : a.Z;
                var bx = axis == 0 ? b.Y : b.X; var by = axis == 2 ? b.Y : b.Z;
                if ((ay > 0) != (by > 0) && ax + (bx - ax) * (-ay) / (by - ay) > 0) inside = !inside;
            }
        }
        return inside;
    }

    // Oriented solid angle gives winding for convex, concave and cavity shells.
    // A fan is an algebraic integration of each polygon, not a mesh model:
    // overlapping fan triangles cancel and holes carry reversed orientation.
    private static int PlanarClassifyPoint(ReadOnlySpan<PlanarModelFace> faces,
        ReadOnlySpan<PlanarModelLoop> loops, ReadOnlySpan<KernelVector3> points,
        int body, in KernelVector3 point, double tolerance, out int boundaryFace, RegionSlot voidRegion = -1)
    {
        boundaryFace = -1;
        double angle = 0;
        for (var f = 0; f < faces.Length; f++)
        {
            ref readonly var face = ref faces[f];
            if (face.Body != body) continue;
            if (voidRegion >= 0)
            {
                ref readonly var topology = ref Faces[face.Source];
                if ((topology.BackShell < 0 || Shells[topology.BackShell].Region != voidRegion)
                    && (topology.FrontShell < 0 || Shells[topology.FrontShell].Region != voidRegion)) continue;
            }
            var distance = ProfileDot(ProfileSubtract(point, face.Origin), face.Normal);
            if (Math.Abs(distance) <= tolerance)
            {
                if (PlanarFaceContains(in face, loops, points, in point)) { boundaryFace = f; return 0; }
                continue;
            }
            for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
            {
                var loop = loops[l];
                var a = ProfileSubtract(points[loop.First], point); var la = Math.Sqrt(ProfileDot(a, a));
                for (var k = 1; k + 1 < loop.Count; k++)
                {
                    var b = ProfileSubtract(points[loop.First + k], point); var lb = Math.Sqrt(ProfileDot(b, b));
                    var c = ProfileSubtract(points[loop.First + k + 1], point); var lc = Math.Sqrt(ProfileDot(c, c));
                    angle += 2 * Math.Atan2(ProfileDot(a, ProfileCross(b, c)), la * lb * lc
                        + ProfileDot(a, b) * lc + ProfileDot(b, c) * la + ProfileDot(c, a) * lb);
                }
            }
        }
        if (!double.IsFinite(angle)) return 2;
        if (Math.Abs(angle) < 1e-6) return -1;
        if (Math.Abs(Math.Abs(angle) - 2 * Math.Tau) < 1e-6)
            // Material-facing loops wind negatively around a bounded void.
            // The exterior shell winds positively around all material/cavities.
            return voidRegion >= 0 && angle > 0 ? -1 : 1;
        return 2; // unresolved, never silently treated as empty
    }

    private static int SplitPlanarCells(Span<PlanarModelPolygon> cells, ref int cellCount,
        Span<KernelVector3> vertices, ref int vertexCount, Span<KernelVector3> work,
        in KernelVector3 origin, in KernelVector3 normal, double tolerance)
    {
        var count = cellCount;
        var outputCount = 0;
        for (var c = 0; c < count; c++)
        {
            var cell = cells[c];
            var lo = double.PositiveInfinity; var hi = double.NegativeInfinity;
            for (var k = 0; k < cell.Count; k++)
            {
                var d = ProfileDot(ProfileSubtract(vertices[cell.First + k], origin), normal);
                lo = Math.Min(lo, d); hi = Math.Max(hi, d);
            }
            if (lo >= -tolerance || hi <= tolerance)
            {
                if (outputCount > work.Length - cell.Count) return ParasolidConstants.PK_ERROR_not_implemented;
                vertices.Slice(cell.First, cell.Count).CopyTo(work[outputCount..]);
                cells[c].First = outputCount; outputCount += cell.Count;
                continue;
            }
            if (cellCount >= cells.Length || outputCount > work.Length - 2 * (cell.Count + 2))
                return ParasolidConstants.PK_ERROR_not_implemented;
            var frontFirst = outputCount;
            var frontCount = ClipPlanarConvex(vertices.Slice(cell.First, cell.Count), work[outputCount..], origin, normal, true, tolerance);
            outputCount += frontCount;
            var backFirst = outputCount;
            var backCount = ClipPlanarConvex(vertices.Slice(cell.First, cell.Count), work[outputCount..], origin, normal, false, tolerance);
            outputCount += backCount;
            if (frontCount < 3 || backCount < 3) return ParasolidConstants.PK_ERROR_bad_loop;
            cells[c].First = frontFirst; cells[c].Count = frontCount;
            cells[cellCount++] = new PlanarModelPolygon { First = backFirst, Count = backCount };
        }
        // New cells already refer to the work buffer; all old cells were copied
        // before it replaces the source, so no moving slice aliases a later read.
        work[..outputCount].CopyTo(vertices); vertexCount = outputCount;
        return 0;
    }

    private static int ClipPlanarConvex(ReadOnlySpan<KernelVector3> input, Span<KernelVector3> output,
        in KernelVector3 origin, in KernelVector3 normal, bool positive, double tolerance)
    {
        var count = 0;
        for (var k = 0; k < input.Length; k++)
        {
            var a = input[k]; var b = input[(k + 1) % input.Length];
            var da = ProfileDot(ProfileSubtract(a, origin), normal); var db = ProfileDot(ProfileSubtract(b, origin), normal);
            if (Math.Abs(da) <= tolerance) da = 0;
            if (Math.Abs(db) <= tolerance) db = 0;
            if (positive ? da >= 0 : da <= 0) output[count++] = a;
            if (da * db < 0)
            {
                var t = da / (da - db);
                output[count++] = Vector(Math.FusedMultiplyAdd(t, b.X - a.X, a.X),
                    Math.FusedMultiplyAdd(t, b.Y - a.Y, a.Y), Math.FusedMultiplyAdd(t, b.Z - a.Z, a.Z));
            }
        }
        return count;
    }

    private static int ArrangePlanarFace(in PlanarModelFace face, ReadOnlySpan<PlanarModelFace> cutters,
        ReadOnlySpan<PlanarModelLoop> loops, ReadOnlySpan<KernelVector3> sourcePoints,
        Span<PlanarModelPolygon> cells, Span<KernelVector3> vertices, Span<KernelVector3> work,
        double tolerance, out int count, out int vertexCount)
    {
        count = vertexCount = 0;
        var outer = loops[face.FirstLoop]; var origin = face.Origin;
        var x = ProfileSubtract(sourcePoints[outer.First + 1], origin);
        if (!ProfileNormalize(ref x)) return ParasolidConstants.PK_ERROR_bad_loop;
        var y = ProfileCross(face.Normal, x);
        var minX = double.PositiveInfinity; var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity; var maxY = double.NegativeInfinity;
        for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
            for (var k = 0; k < loops[l].Count; k++)
            {
                var p = ProfileSubtract(sourcePoints[loops[l].First + k], origin);
                var px = ProfileDot(p, x); var py = ProfileDot(p, y);
                minX = Math.Min(minX, px); maxX = Math.Max(maxX, px); minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
            }
        if (maxX - minX <= tolerance * 4 || maxY - minY <= tolerance * 4) return ParasolidConstants.PK_ERROR_bad_loop;
        vertices[0] = ProfileAdd(origin, ProfileAdd(ProfileScale(x, minX), ProfileScale(y, minY)));
        vertices[1] = ProfileAdd(origin, ProfileAdd(ProfileScale(x, maxX), ProfileScale(y, minY)));
        vertices[2] = ProfileAdd(origin, ProfileAdd(ProfileScale(x, maxX), ProfileScale(y, maxY)));
        vertices[3] = ProfileAdd(origin, ProfileAdd(ProfileScale(x, minX), ProfileScale(y, maxY)));
        count = 1; vertexCount = 4; cells[0] = new PlanarModelPolygon { First = 0, Count = 4 };
        // Lines supporting every outer and inner edge partition the face plane.
        // Membership of each open cell is then constant, including concave and
        // holed profiles; no ear triangulation or guessed hole bridge is needed.
        for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
            for (var k = 0; k < loops[l].Count; k++)
            {
                var a = sourcePoints[loops[l].First + k];
                var b = sourcePoints[loops[l].First + (k + 1) % loops[l].Count];
                var normal = ProfileCross(ProfileSubtract(b, a), face.Normal);
                if (!ProfileNormalize(ref normal)) return ParasolidConstants.PK_ERROR_bad_loop;
                var error = SplitPlanarCells(cells, ref count, vertices, ref vertexCount, work, a, normal, tolerance);
                if (error != 0) return error;
            }
        for (var c = count - 1; c >= 0; c--)
        {
            var center = PlanarPolygonCenter(vertices.Slice(cells[c].First, cells[c].Count));
            if (!PlanarFaceContains(in face, loops, sourcePoints, center)) { cells[c] = cells[--count]; }
        }
        foreach (ref readonly var cutter in cutters)
        {
            // Coplanar faces also need their boundary lines: their intersection
            // partitions material on both sides, even though the planes coincide.
            var cross = ProfileCross(face.Normal, cutter.Normal);
            if (ProfileDot(cross, cross) < 1e-20)
            {
                if (Math.Abs(ProfileDot(ProfileSubtract(cutter.Origin, origin), face.Normal)) > tolerance) continue;
                if (face.Source == cutter.Source) continue;
                for (var l = cutter.FirstLoop; l < cutter.FirstLoop + cutter.LoopCount; l++)
                    for (var k = 0; k < loops[l].Count; k++)
                    {
                        var a = sourcePoints[loops[l].First + k];
                        var b = sourcePoints[loops[l].First + (k + 1) % loops[l].Count];
                        var normal = ProfileCross(ProfileSubtract(b, a), face.Normal);
                        if (!ProfileNormalize(ref normal)) return ParasolidConstants.PK_ERROR_bad_loop;
                        var error = SplitPlanarCells(cells, ref count, vertices, ref vertexCount, work, a, normal, tolerance);
                        if (error != 0) return error;
                    }
            }
            else
            {
                var error = SplitPlanarCells(cells, ref count, vertices, ref vertexCount, work, cutter.Origin, cutter.Normal, tolerance);
                if (error != 0) return error;
            }
        }
        return 0;
    }

    private static KernelVector3 PlanarPolygonCenter(ReadOnlySpan<KernelVector3> vertices)
    {
        var sum = Vector(0, 0, 0); var origin = vertices[0];
        for (var i = 1; i < vertices.Length; i++) sum = ProfileAdd(sum, ProfileSubtract(vertices[i], origin));
        return ProfileAdd(origin, ProfileScale(sum, 1.0 / vertices.Length));
    }
}
