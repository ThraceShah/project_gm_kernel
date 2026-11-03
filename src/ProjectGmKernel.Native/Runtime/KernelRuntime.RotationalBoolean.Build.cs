using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private struct RotationalBoundary
    {
        internal KernelClassCode Kind, Sign;
        internal BufferOffset Coordinate, First, Last, Source;
        internal ulong SourcesLow, SourcesHigh;
        internal byte Cavity;
    }

    private static int LabelRotationalGrid(ReadOnlySpan<uint> masks, int width, int height, int function,
        Span<int> components, Span<int> voids, Span<int> work, out int count)
    {
        count = 0; components.Fill(-1); voids.Fill(-1);
        // A checkerboard meridian vertex rotates into a nonmanifold circle.
        for (var z = 1; z < height; z++) for (var r = 1; r < width; r++)
        {
            var a = RotationalMaterial(masks[(z - 1) * width + r - 1], function);
            var b = RotationalMaterial(masks[(z - 1) * width + r], function);
            var c = RotationalMaterial(masks[z * width + r - 1], function);
            var d = RotationalMaterial(masks[z * width + r], function);
            if (a == d && b == c && a != b) return ParasolidConstants.PK_ERROR_not_implemented;
        }
        for (var seed = 0; seed < masks.Length; seed++)
        {
            if (components[seed] >= 0 || !RotationalMaterial(masks[seed], function)) continue;
            var read = 0; var write = 1; work[0] = seed; components[seed] = count;
            while (read < write)
            {
                var cell = work[read++]; var x = cell % width; var y = cell / width;
                for (var direction = 0; direction < 4; direction++)
                {
                    var next = direction == 0 ? (x > 0 ? cell - 1 : -1) : direction == 1 ? (x + 1 < width ? cell + 1 : -1)
                        : direction == 2 ? (y > 0 ? cell - width : -1) : y + 1 < height ? cell + width : -1;
                    if (next < 0 || components[next] >= 0 || !RotationalMaterial(masks[next], function)) continue;
                    components[next] = count; work[write++] = next;
                }
            }
            count++;
        }
        return 0;
    }

    private static void LabelRotationalVoids(ReadOnlySpan<int> components, int component, int width, int height,
        Span<int> voids, Span<int> work)
    {
        voids.Fill(-1); var label = 1;
        for (var seed = 0; seed < components.Length; seed++)
        {
            if (voids[seed] >= 0 || components[seed] == component) continue;
            var read = 0; var write = 1; work[0] = seed; voids[seed] = label; var exterior = false;
            while (read < write)
            {
                var cell = work[read++]; var x = cell % width; var y = cell / width;
                exterior |= x == width - 1 || y == 0 || y == height - 1;
                for (var direction = 0; direction < 4; direction++)
                {
                    var next = direction == 0 ? (x > 0 ? cell - 1 : -1) : direction == 1 ? (x + 1 < width ? cell + 1 : -1)
                        : direction == 2 ? (y > 0 ? cell - width : -1) : y + 1 < height ? cell + width : -1;
                    if (next < 0 || voids[next] >= 0 || components[next] == component) continue;
                    voids[next] = label; work[write++] = next;
                }
            }
            if (exterior) for (var i = 0; i < write; i++) voids[work[i]] = 0;
            label++;
        }
    }

    private static int BuildRotationalGridBody(ReadOnlySpan<RotationalFace> sources, in RotationalFrame frame,
        ReadOnlySpan<double> radii, ReadOnlySpan<double> heights, ReadOnlySpan<uint> masks,
        ReadOnlySpan<int> components, ReadOnlySpan<int> voids, int component, bool merge,
        Span<FaceSlot> sourceProducts, out EntityTag body)
    {
        body = 0; var width = radii.Length - 1; var height = heights.Length - 1;
        var scratch = CommandScratch.Current; var memory = scratch.Take(30000);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var boundaries = ModelSpan<RotationalBoundary>(bytes, ref cursor, 1024);
            var edges = ModelSpan<ModelingEdge>(bytes, ref cursor, 1024);
            var edgeRadii = ModelSpan<double>(bytes, ref cursor, 1024);
            var edgeHeights = ModelSpan<double>(bytes, ref cursor, 1024);
            var edgeDirections = ModelSpan<int>(bytes, ref cursor, 1024);
            var coedges = ModelSpan<ModelingCoedge>(bytes, ref cursor, 2048);
            var loops = ModelSpan<ModelingLoop>(bytes, ref cursor, 2048);
            var faces = ModelSpan<ModelingFace>(bytes, ref cursor, 1024);
            var cavity = ModelSpan<byte>(bytes, ref cursor, 1024);
            var products = ModelSpan<FaceSlot>(bytes, ref cursor, 1024);
            if (cursor > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            var n = 0;
            for (var kind = 0; kind < 2; kind++)
            {
                var fixedCount = kind == 1 ? radii.Length : heights.Length;
                var movingCount = kind == 1 ? height : width;
                for (var fixedIndex = kind == 1 ? 1 : 0; fixedIndex < fixedCount; fixedIndex++)
                {
                    var previous = -1;
                    for (var moving = 0; moving < movingCount; moving++)
                    {
                        var before = kind == 1 ? moving * width + fixedIndex - 1 : fixedIndex > 0 ? (fixedIndex - 1) * width + moving : -1;
                        var after = kind == 1 ? fixedIndex < width ? moving * width + fixedIndex : -1 : fixedIndex < height ? fixedIndex * width + moving : -1;
                        var a = before >= 0 && components[before] == component;
                        var b = after >= 0 && components[after] == component;
                        if (a == b) { previous = -1; continue; }
                        var coordinate = kind == 1 ? radii[fixedIndex] : heights[fixedIndex];
                        var movingValues = kind == 1 ? heights : radii;
                        var middle = movingValues[moving] + (movingValues[moving + 1] - movingValues[moving]) * .5;
                        var source = -1; ulong low = 0, high = 0;
                        for (var f = 0; f < sources.Length; f++)
                        {
                            ref readonly var s = ref sources[f];
                            if (s.Kind != kind || Math.Abs(s.Coordinate - coordinate) > RotationalTolerance || middle <= s.Low || middle >= s.High) continue;
                            if (source < 0) source = f;
                            if (f < 64) low |= 1UL << f; else high |= 1UL << (f - 64);
                        }
                        if (source < 0) return ParasolidConstants.PK_ERROR_bad_loop;
                        var empty = a ? after : before; var isCavity = empty >= 0 && voids[empty] > 0 ? (byte)1 : (byte)0;
                        var sign = a ? 1 : -1;
                        if (previous >= 0 && boundaries[previous].Sign == sign && boundaries[previous].Cavity == isCavity
                            && (merge || boundaries[previous].SourcesLow == low && boundaries[previous].SourcesHigh == high))
                        { boundaries[previous].Last = moving + 1; continue; }
                        if (n == boundaries.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                        boundaries[n] = new() { Kind = kind, Sign = sign, Coordinate = fixedIndex, First = moving, Last = moving + 1,
                            Source = source, SourcesLow = low, SourcesHigh = high, Cavity = isCavity };
                        previous = n++;
                    }
                }
            }
            var edgeCount = 0; var loopCount = 0;
            for (var f = 0; f < n; f++)
            {
                ref readonly var boundary = ref boundaries[f]; ref readonly var source = ref sources[boundary.Source];
                var error = TryCloneEntityCore(source.Surface, out var surface); if (error != 0) return error;
                var sourceSense = Faces[source.Face].Orientation;
                var sense = source.Sign == boundary.Sign ? sourceSense : FlipLocalSense(sourceSense);
                faces[f] = new() { Surface = surface, Sense = NormalizeLocalSense(sense), FirstLoop = loopCount };
                cavity[f] = boundary.Cavity;
                // Planes use outer ring first; cylinders use lower then upper.
                for (var end = 0; end < 2; end++)
                {
                    var index = boundary.Kind == 1 ? (end == 0 ? boundary.First : boundary.Last) : (end == 0 ? boundary.Last : boundary.First);
                    var radius = boundary.Kind == 1 ? radii[boundary.Coordinate] : radii[index];
                    var z = boundary.Kind == 1 ? heights[index] : heights[boundary.Coordinate];
                    if (radius == 0) continue;
                    var edge = -1;
                    for (var e = 0; e < edgeCount; e++) if (edgeRadii[e] == radius && edgeHeights[e] == z) { edge = e; break; }
                    if (edge < 0)
                    {
                        if (edgeCount == edges.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                        var cylinderSeam = true;
                        for (var k = 0; k < n; k++) if (boundaries[k].Kind == 0 && heights[boundaries[k].Coordinate] == z
                            && (radii[boundaries[k].First] == radius || radii[boundaries[k].Last] == radius)) { cylinderSeam = false; break; }
                        error = CreateRotationalRing(sources, frame, radius, z, cylinderSeam, out var curve, out var direction); if (error != 0) return error;
                        edge = edgeCount++; edges[edge] = new() { Curve = curve, StartVertex = -1, EndVertex = -1, Start = 0, End = Math.Tau };
                        edgeRadii[edge] = radius; edgeHeights[edge] = z; edgeDirections[edge] = direction;
                    }
                    var traversal = (end == 0 ? 1 : -1) * boundary.Sign;
                    coedges[loopCount] = new() { Edge = edge, Forward = traversal == edgeDirections[edge] ? (byte)1 : (byte)0 };
                    loops[loopCount] = new() { FirstCoedge = loopCount, CoedgeCount = 1 };
                    loopCount++; faces[f].LoopCount++;
                }
            }
            var build = BuildModelingBrep(default, edges[..edgeCount], coedges[..loopCount], loops[..loopCount], faces[..n], true,
                out body, products[..n], cavity[..n]);
            if (build != 0) return build;
            for (var f = 0; f < n; f++) if (sourceProducts[boundaries[f].Source] < 0) sourceProducts[boundaries[f].Source] = products[f];
            return 0;
        }
        finally { scratch.Return(memory); }
    }

    private static int CreateRotationalRing(ReadOnlySpan<RotationalFace> sources, in RotationalFrame frame,
        double radius, double height, bool cylinderSeam, out CurveTag curve, out int direction)
    {
        curve = 0; direction = 1;
        var cuttingPlane = -1; var cylinderSign = 1;
        for (var f = 0; f < sources.Length; f++)
        {
            ref readonly var source = ref sources[f];
            if (source.Kind == 0 && cuttingPlane < 0 && Math.Abs(source.Coordinate - height) <= RotationalTolerance
                && radius >= source.Low - RotationalTolerance && radius <= source.High + RotationalTolerance) cuttingPlane = f;
            if (source.Kind == 1 && Math.Abs(source.Coordinate - radius) <= RotationalTolerance
                && height > source.Low + RotationalTolerance && height < source.High - RotationalTolerance) cylinderSign = source.Sign;
        }
        if (!cylinderSeam || cuttingPlane < 0)
        foreach (ref readonly var source in sources)
        for (var end = 0; end < 2; end++)
        {
            var edge = end == 0 ? source.LowEdge : source.HighEdge; if (edge < 0) continue;
            var error = ReadRotationalRing(edge, frame, out var r, out var z, out var sense); if (error != 0) return error;
            if (Math.Abs(r - radius) > RotationalTolerance || Math.Abs(z - height) > RotationalTolerance) continue;
            direction = sense; return TryCloneEntityCore(Edges[edge].CurveTag, out curve);
        }
        // New plane/cylinder intersections use the source plane's normal chart.
        var axis = frame.Axis;
        if (cuttingPlane >= 0)
        {
            ref readonly var source = ref sources[cuttingPlane];
            var p = PlaneDataPool[Surfaces[GetSurfaceSlotByTag(source.Surface)].DataIndex];
            axis = Vector(p.NormalX, p.NormalY, p.NormalZ);
        }
        var center = Add(frame.Origin, Scale(frame.Axis, height));
        curve = CreateCircleCurveTag(center.X, center.Y, center.Z, axis.X, axis.Y, axis.Z,
            frame.Reference.X, frame.Reference.Y, frame.Reference.Z, radius);
        direction = Dot(axis, frame.Axis) > 0 ? 1 : -1;
        if (curve > 0 && (cylinderSeam ? direction < 0 : cylinderSign < 0))
        {
            Curves[GetCurveSlotByTag(curve)].Sense = ParasolidConstants.PK_TOPOL_sense_negative_c;
            direction = -direction;
        }
        return curve > 0 ? 0 : ParasolidConstants.PK_ERROR_memory_full;
    }

    private static EntityTag LocateRotationalBoundary(ReadOnlySpan<RotationalFace> faces, int input, double radius, double height,
        out int kind)
    {
        kind = 0; EntityTag found = 0;
        foreach (ref readonly var f in faces)
        {
            if (f.Input != input) continue;
            var coordinate = f.Kind == 1 ? radius : height; var t = f.Kind == 1 ? height : radius;
            if (Math.Abs(coordinate - f.Coordinate) > RotationalTolerance || t < f.Low - RotationalTolerance || t > f.High + RotationalTolerance) continue;
            if (Math.Abs(t - f.Low) <= RotationalTolerance && f.LowEdge >= 0) { kind = ParasolidConstants.PK_CLASS_edge; return Edges[f.LowEdge].Header.Tag; }
            if (Math.Abs(t - f.High) <= RotationalTolerance && f.HighEdge >= 0) { kind = ParasolidConstants.PK_CLASS_edge; return Edges[f.HighEdge].Header.Tag; }
            found = Faces[f.Face].Header.Tag;
        }
        if (found != 0) kind = ParasolidConstants.PK_CLASS_face; return found;
    }

    private static int CreateRotationalTracking(ReadOnlySpan<RotationalFace> faces, in RotationalFrame frame, int inputCount,
        ReadOnlySpan<EntityTag> bodies, PK_TOPOL_track_r_s* result)
    {
        var count = 0; PK_TOPOL_track_record_r_s* records = null; EntityTag* original = null; int* classes = null; EntityTag* product = null;
        Span<EntityTag> tags = stackalloc EntityTag[inputCount]; Span<int> kinds = stackalloc int[inputCount];
        for (var pass = 0; pass < 2; pass++)
        {
            var index = 0; var offset = 0;
            foreach (var body in bodies)
            {
                var slot = TagRec(body).Slot; var edge = Bodies[slot].FirstEdgeBody;
                for (var e = 0; e < Bodies[slot].EdgeCountBody; e++, edge = Edges[edge].NextInBody)
                {
                    var error = ReadRotationalRing(edge, frame, out var r, out var z, out _); if (error != 0) return error;
                    var n = 0;
                    for (var b = 0; b < inputCount; b++)
                    {
                        var tag = LocateRotationalBoundary(faces, b, r, z, out var kind);
                        if (tag == 0) continue; tags[n] = tag; kinds[n++] = kind;
                    }
                    if (n < 2) continue;
                    if (pass != 0)
                    {
                        tags[..n].CopyTo(new Span<int>(original + offset, n)); kinds[..n].CopyTo(new Span<int>(classes + offset, n));
                        product[index] = Edges[edge].Header.Tag;
                        records[index] = new() { track = ParasolidConstants.PK_TOPOL_track_derive_c, n_original_topols = n,
                            original_topols = original + offset, original_classes = classes + offset, n_product_topols = 1, product_topols = product + index };
                        offset += n;
                    }
                    index++;
                }
            }
            if (pass == 0)
            {
                count = index; if (count == 0) return 0;
                records = (PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate((nuint)(count * (sizeof(PK_TOPOL_track_record_r_s) + (2 * inputCount + 1) * sizeof(int))));
                if (records == null) return ParasolidConstants.PK_ERROR_memory_full;
                original = (int*)(records + count); classes = original + count * inputCount; product = classes + count * inputCount;
            }
        }
        *result = new() { n_track_records = count, track_records = records, internal_origs = original, internal_classes = classes, internal_prods = product };
        return 0;
    }
}
