using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct PlanarBoundarySegment
    {
        internal BufferOffset Start, End, Group;
        internal byte Used;
    }

    private struct PlanarBoundaryLoop
    {
        internal BufferOffset First, Group, Outer;
        internal BufferCount Count;
        internal double Area;
    }

    /// <summary>Cancel internal cell boundaries, assemble oriented outer/hole
    /// loops, normalize T junctions, and build connected result bodies. Polygon
    /// cells are an exact plane arrangement, never a faceted surface substitute.</summary>
    private static int ReconstructPlanarBodies(ReadOnlySpan<PlanarModelFace> sources,
        ReadOnlySpan<PlanarModelPolygon> polygons, ReadOnlySpan<KernelVector3> polygonPoints,
        double tolerance, bool solid, bool mergeCoplanar, Span<EntityTag> results,
        out int resultCount, Span<FaceTag> sourceProducts = default,scoped Span<BooleanTopologyOrigin> lineage=default)
    {
        resultCount = 0;
        if (polygons.IsEmpty) return 0;
        var scratch = CommandScratch.Current;
        var memory = scratch.Take(160000);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var vertices = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var polygonIndices = ModelSpan<int>(bytes, ref cursor, PlanarPointCapacity);
            var groups = ModelSpan<int>(bytes, ref cursor, polygons.Length);
            var groupSource = ModelSpan<int>(bytes, ref cursor, polygons.Length);
            var segments = ModelSpan<PlanarBoundarySegment>(bytes, ref cursor, PlanarPointCapacity * 2);
            var loopVertices = ModelSpan<int>(bytes, ref cursor, PlanarPointCapacity * 2);
            var boundaryLoops = ModelSpan<PlanarBoundaryLoop>(bytes, ref cursor, PlanarPolygonCapacity);
            var vertexMap = ModelSpan<int>(bytes, ref cursor, PlanarPointCapacity);
            var modelEdges = ModelSpan<ModelingEdge>(bytes, ref cursor, PlanarPointCapacity);
            var modelCoedges = ModelSpan<ModelingCoedge>(bytes, ref cursor, PlanarPointCapacity * 2);
            var modelLoops = ModelSpan<ModelingLoop>(bytes, ref cursor, PlanarPolygonCapacity);
            var modelFaces = ModelSpan<ModelingFace>(bytes, ref cursor, PlanarPolygonCapacity);
            var modelSource = ModelSpan<int>(bytes, ref cursor, PlanarPolygonCapacity);
            if (cursor > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            var vertexCount = 0; var groupCount = 0;
            for (var p = 0; p < polygons.Length; p++)
            {
                ref readonly var polygon = ref polygons[p];
                if (polygon.First < 0 || polygon.Count < 3 || polygon.First > polygonPoints.Length - polygon.Count)
                    return ParasolidConstants.PK_ERROR_bad_loop;
                var group = -1;
                for (var g = 0; g < groupCount; g++)
                {
                    ref readonly var prior = ref polygons[groupSource[g]];
                    if (polygon.Reversed != prior.Reversed) continue;
                    if (!mergeCoplanar)
                    {
                        if (polygon.Face == prior.Face && polygon.Classification == prior.Classification) { group = g; break; }
                    }
                    else
                    {
                        ref readonly var a = ref sources[polygon.Face]; ref readonly var b = ref sources[prior.Face];
                        if (ProfileDot(a.Normal, b.Normal) > 1 - 1e-12
                            && Math.Abs(ProfileDot(ProfileSubtract(a.Origin, b.Origin), a.Normal)) <= tolerance
                            && (a.Surface == b.Surface || a.Surface == 0 || b.Surface == 0
                                || Surfaces[GetSurfaceSlotByTag(a.Surface)].Class == SurfaceClass.Plane
                                && Surfaces[GetSurfaceSlotByTag(b.Surface)].Class == SurfaceClass.Plane)) { group = g; break; }
                    }
                }
                if (group < 0) { group = groupCount; groupSource[groupCount++] = p; }
                groups[p] = group;
                for (var k = 0; k < polygon.Count; k++)
                {
                    var index = polygon.First + k;
                    var point = polygonPoints[index]; var found = -1;
                    for (var v = 0; v < vertexCount; v++) if (ProfileDistance(in vertices[v], in point) <= tolerance) { found = v; break; }
                    if (found < 0)
                    {
                        if (vertexCount == vertices.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                        found = vertexCount; vertices[vertexCount++] = point;
                    }
                    polygonIndices[index] = found;
                }
            }

            var segmentCount = 0;
            Span<int> splitVertices = stackalloc int[PlanarPointCapacity];
            Span<double> splitParameters = stackalloc double[PlanarPointCapacity];
            for (var p = 0; p < polygons.Length; p++)
            {
                ref readonly var polygon = ref polygons[p];
                for (var k = 0; k < polygon.Count; k++)
                {
                    var ai = polygonIndices[polygon.First + k]; var bi = polygonIndices[polygon.First + (k + 1) % polygon.Count];
                    if (ai == bi) continue;
                    var a = vertices[ai]; var d = ProfileSubtract(vertices[bi], a); var length2 = ProfileDot(d, d);
                    var splitCount = 2; splitVertices[0] = ai; splitVertices[1] = bi; splitParameters[0] = 0; splitParameters[1] = 1;
                    // Split all faces at the same geometric vertices before
                    // cancelling edges, so no hanging T junction survives.
                    for (var v = 0; v < vertexCount; v++)
                    {
                        if (v == ai || v == bi) continue;
                        var delta = ProfileSubtract(vertices[v], a); var t = ProfileDot(delta, d) / length2;
                        if (!(t > 0 && t < 1)) continue;
                        var residual = ProfileSubtract(delta, ProfileScale(d, t));
                        if (ProfileDot(residual, residual) <= tolerance * tolerance)
                        {
                            var at = splitCount++;
                            while (at > 0 && splitParameters[at - 1] > t)
                            { splitParameters[at] = splitParameters[at - 1]; splitVertices[at] = splitVertices[at - 1]; at--; }
                            splitParameters[at] = t; splitVertices[at] = v;
                        }
                    }
                    for (var s = 0; s + 1 < splitCount; s++)
                    {
                        var start = splitVertices[s]; var end = splitVertices[s + 1];
                        if (polygon.Reversed != 0) (start, end) = (end, start);
                        var cancel = -1;
                        for (var j = 0; j < segmentCount; j++)
                            if (segments[j].Used == 0 && segments[j].Group == groups[p]
                                && segments[j].Start == end && segments[j].End == start) { cancel = j; break; }
                        if (cancel >= 0) segments[cancel].Used = 1;
                        else
                        {
                            if (segmentCount == segments.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                            segments[segmentCount++] = new PlanarBoundarySegment { Start = start, End = end, Group = groups[p] };
                        }
                    }
                }
            }

            var loopCount = 0; var loopVertexCount = 0;
            for (var s = 0; s < segmentCount; s++)
            {
                if (segments[s].Used != 0) continue;
                if (loopCount == boundaryLoops.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                var first = loopVertexCount; var current = s; var start = segments[s].Start; var group = segments[s].Group;
                for (var limit = 0; ; limit++)
                {
                    if (limit >= segmentCount || segments[current].Used != 0) return ParasolidConstants.PK_ERROR_bad_loop;
                    if (loopVertexCount == loopVertices.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                    loopVertices[loopVertexCount++] = segments[current].Start; segments[current].Used = 1;
                    var end = segments[current].End;
                    if (end == start) break;
                    var next = -1;
                    for (var j = 0; j < segmentCount; j++)
                        if (segments[j].Used == 0 && segments[j].Group == group && segments[j].Start == end)
                        {
                            if (next >= 0) return ParasolidConstants.PK_ERROR_not_implemented; // non-manifold contact
                            next = j;
                        }
                    if (next < 0) return ParasolidConstants.PK_ERROR_bad_loop;
                    current = next;
                }
                var count = loopVertexCount - first;
                if (count < 3) return ParasolidConstants.PK_ERROR_bad_loop;
                ref readonly var poly = ref polygons[groupSource[group]];
                var normal = sources[poly.Face].Normal;
                if (poly.Reversed != 0) normal = ProfileScale(normal, -1);
                var origin = vertices[loopVertices[first]]; double area = 0;
                for (var k = 1; k + 1 < count; k++)
                    area += ProfileDot(ProfileCross(ProfileSubtract(vertices[loopVertices[first + k]], origin),
                        ProfileSubtract(vertices[loopVertices[first + k + 1]], origin)), normal);
                if (Math.Abs(area) <= tolerance * tolerance) return ParasolidConstants.PK_ERROR_bad_loop;
                boundaryLoops[loopCount++] = new PlanarBoundaryLoop { First = first, Count = count, Group = group, Outer = -1, Area = area };
            }
            // Remove degree-two collinear vertices consistently in every loop;
            // vertices at an imprint junction have more than two neighbours.
            vertexMap[..vertexCount].Fill(-1);
            for (var v = 0; v < vertexCount; v++)
            {
                var a = -1; var b = -1; var branched = false;
                for (var l = 0; l < loopCount; l++)
                    for (var k = 0; k < boundaryLoops[l].Count; k++)
                    {
                        var loop = boundaryLoops[l];
                        if (loopVertices[loop.First + k] != v) continue;
                        var previous = loopVertices[loop.First + (k + loop.Count - 1) % loop.Count];
                        var next = loopVertices[loop.First + (k + 1) % loop.Count];
                        if (a < 0) { a = previous; b = next; }
                        else if (!((a == previous && b == next) || (a == next && b == previous))) branched = true;
                    }
                if (a < 0 || branched) continue;
                var ab = ProfileSubtract(vertices[b], vertices[a]); var av = ProfileSubtract(vertices[v], vertices[a]);
                var t = ProfileDot(av, ab) / ProfileDot(ab, ab); var off = ProfileSubtract(av, ProfileScale(ab, t));
                if (t > 0 && t < 1 && ProfileDot(off, off) <= tolerance * tolerance) vertexMap[v] = 1;
            }
            for (var l = 0; l < loopCount; l++)
            {
                var count = 0; var loop = boundaryLoops[l];
                for (var k = 0; k < loop.Count; k++)
                    if (vertexMap[loopVertices[loop.First + k]] != 1) loopVertices[loop.First + count++] = loopVertices[loop.First + k];
                if (count < 3) return ParasolidConstants.PK_ERROR_bad_loop;
                boundaryLoops[l].Count = count;
            }
            for (var l = 0; l < loopCount; l++)
            {
                if (boundaryLoops[l].Area > 0) { boundaryLoops[l].Outer = l; continue; }
                var candidate = -1; var candidateArea = double.PositiveInfinity;
                var point = vertices[loopVertices[boundaryLoops[l].First]];
                for (var o = 0; o < loopCount; o++)
                {
                    if (boundaryLoops[o].Group != boundaryLoops[l].Group || boundaryLoops[o].Area <= 0) continue;
                    var normal = sources[polygons[groupSource[boundaryLoops[o].Group]].Face].Normal;
                    if (PlanarIndexedLoopContains(vertices, loopVertices.Slice(boundaryLoops[o].First, boundaryLoops[o].Count), point, normal)
                        && boundaryLoops[o].Area < candidateArea) { candidate = o; candidateArea = boundaryLoops[o].Area; }
                }
                if (candidate < 0) return ParasolidConstants.PK_ERROR_bad_loop;
                boundaryLoops[l].Outer = candidate;
            }

            // Drop vertices that disappeared when internal cell edges cancelled.
            vertexMap[..vertexCount].Fill(-1); var usedCount = 0;
            for (var l = 0; l < loopCount; l++) for (var k = 0; k < boundaryLoops[l].Count; k++)
            {
                var index = boundaryLoops[l].First + k; var v = loopVertices[index];
                if (vertexMap[v] < 0) vertexMap[v] = usedCount++;
                loopVertices[index] = vertexMap[v];
            }
            // Store reordered vertices in the now-unused polygon index workspace
            // is too small for vectors; reuse source-independent segment memory.
            var compact = MemoryMarshal.Cast<PlanarBoundarySegment, KernelVector3>(segments);
            for (var v = 0; v < vertexCount; v++) if (vertexMap[v] >= 0) compact[vertexMap[v]] = vertices[v];
            compact[..usedCount].CopyTo(vertices); vertexCount = usedCount;

            var nEdges = 0; var nCoedges = 0; var nLoops = 0; var nFaces = 0;
            for (var outer = 0; outer < loopCount; outer++)
            {
                if (boundaryLoops[outer].Outer != outer) continue;
                ref readonly var polygon = ref polygons[groupSource[boundaryLoops[outer].Group]];
                ref readonly var source = ref sources[polygon.Face];
                SurfTag surface = 0;
                for (var f = 0; f < nFaces; f++)
                    if (source.Surface != 0 && sources[modelSource[f]].Surface == source.Surface) { surface = modelFaces[f].Surface; break; }
                if (surface == 0)
                {
                    if (source.Surface != 0)
                    {
                        var error = TryCloneEntityCore(source.Surface, out surface);
                        if (error != 0) return error;
                    }
                    else
                    {
                        var x = ProfileSubtract(vertices[loopVertices[boundaryLoops[outer].First + 1]], vertices[loopVertices[boundaryLoops[outer].First]]);
                        if (!ProfileNormalize(ref x)) return ParasolidConstants.PK_ERROR_bad_loop;
                        surface = CreatePlaneSurfaceTag(source.Origin.X, source.Origin.Y, source.Origin.Z,
                            source.Normal.X, source.Normal.Y, source.Normal.Z, x.X, x.Y, x.Z);
                        if (surface <= 0) return ParasolidConstants.PK_ERROR_memory_full;
                    }
                }
                var sense = source.Surface != 0 ? source.Sense : ParasolidConstants.PK_TOPOL_sense_positive_c;
                if (polygon.Reversed != 0) sense = sense == ParasolidConstants.PK_TOPOL_sense_negative_c
                    ? ParasolidConstants.PK_TOPOL_sense_positive_c : ParasolidConstants.PK_TOPOL_sense_negative_c;
                var modelFace = new ModelingFace { Surface = surface, Sense = sense, FirstLoop = nLoops };
                // Outer first, then holes, independently of arrangement traversal.
                for (var pass = 0; pass < 2; pass++) for (var l = 0; l < loopCount; l++)
                {
                    if (boundaryLoops[l].Outer != outer || (pass == 0 ? l != outer : l == outer)) continue;
                    var boundary = boundaryLoops[l];
                    modelLoops[nLoops++] = new ModelingLoop { FirstCoedge = nCoedges, CoedgeCount = boundary.Count };
                    modelFace.LoopCount++;
                    for (var k = 0; k < boundary.Count; k++)
                    {
                        var a = loopVertices[boundary.First + k]; var b = loopVertices[boundary.First + (k + 1) % boundary.Count];
                        var e = -1; var forward = true;
                        for (var j = 0; j < nEdges; j++)
                            if (modelEdges[j].StartVertex == a && modelEdges[j].EndVertex == b) { e = j; break; }
                            else if (modelEdges[j].StartVertex == b && modelEdges[j].EndVertex == a) { e = j; forward = false; break; }
                        if (e < 0)
                        {
                            if (nEdges == modelEdges.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                            var p = vertices[a]; var q = vertices[b];
                            var curve = CreateLineCurveTag(p.X, p.Y, p.Z, q.X - p.X, q.Y - p.Y, q.Z - p.Z);
                            if (curve <= 0) return ParasolidConstants.PK_ERROR_memory_full;
                            e = nEdges; modelEdges[nEdges++] = new ModelingEdge { Curve = curve, StartVertex = a, EndVertex = b, Start = 0, End = ProfileDistance(p, q) };
                        }
                        if (nCoedges == modelCoedges.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                        modelCoedges[nCoedges++] = new ModelingCoedge { Edge = e, Forward = forward ? (byte)1 : (byte)0 };
                    }
                }
                modelSource[nFaces] = polygon.Face; modelFaces[nFaces++] = modelFace;
            }
            return BuildPlanarComponents(vertices[..vertexCount], modelEdges[..nEdges], modelCoedges[..nCoedges],
                modelLoops[..nLoops], modelFaces[..nFaces], sources, modelSource[..nFaces], solid, tolerance,
                results, out resultCount, sourceProducts,lineage);
        }
        finally { scratch.Return(memory); }
    }

    private static Span<T> ModelSpan<T>(Span<byte> memory, ref int cursor, int count) where T : unmanaged
    {
        cursor = (cursor + 7) & ~7;
        var bytes = count * sizeof(T);
        if (cursor > memory.Length - bytes) { cursor = memory.Length + 1; return Span<T>.Empty; }
        var span = MemoryMarshal.Cast<byte, T>(memory.Slice(cursor, bytes)); cursor += bytes; return span;
    }

    private static bool PlanarIndexedLoopContains(ReadOnlySpan<KernelVector3> points, ReadOnlySpan<int> indices,
        in KernelVector3 point, in KernelVector3 normal)
    {
        var axis = Math.Abs(normal.X) > Math.Abs(normal.Y) ? (Math.Abs(normal.X) > Math.Abs(normal.Z) ? 0 : 2) : (Math.Abs(normal.Y) > Math.Abs(normal.Z) ? 1 : 2);
        var inside = false;
        for (var k = 0; k < indices.Length; k++)
        {
            var a = ProfileSubtract(points[indices[k]], point); var b = ProfileSubtract(points[indices[(k + 1) % indices.Length]], point);
            var ax = axis == 0 ? a.Y : a.X; var ay = axis == 2 ? a.Y : a.Z;
            var bx = axis == 0 ? b.Y : b.X; var by = axis == 2 ? b.Y : b.Z;
            if ((ay > 0) != (by > 0) && ax + (bx - ax) * (-ay) / (by - ay) > 0) inside = !inside;
        }
        return inside;
    }
}
