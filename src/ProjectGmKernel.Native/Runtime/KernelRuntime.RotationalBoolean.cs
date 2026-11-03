using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const int RotationalFaceLimit = 128;
    private const int RotationalCoordinateLimit = 257;
    private const double RotationalTolerance = 1e-9;

    private struct RotationalFrame
    {
        internal KernelVector3 Origin, Axis, Reference;
        internal double Scale;
    }

    // A full circular face is one segment of the meridian arrangement.
    // Cylinder: Coordinate=radius, [Low,High]=axial range. Plane: the reverse.
    private struct RotationalFace
    {
        internal FaceSlot Face;
        internal BodySlot Body;
        internal BufferOffset Input;
        internal SurfTag Surface;
        internal KernelClassCode Kind, Sign;
        internal double Coordinate, Low, High;
        internal EdgeSlot LowEdge, HighEdge;
    }

    private static int ReadRotationalModel(ReadOnlySpan<EntityTag> inputs, Span<RotationalFace> faces,
        out int count, out RotationalFrame frame)
    {
        count = 0; frame = default; var foundAxis = false;
        for (var b = 0; b < inputs.Length; b++)
        {
            if (!TryResolveBodySlot(inputs[b], out var body)) return ParasolidConstants.PK_ERROR_bad_class;
            ref readonly var record = ref Bodies[body];
            if (record.BodyType != ParasolidConstants.PK_BODY_type_solid_c || record.VertexCountBody != 0)
                return ParasolidConstants.PK_ERROR_not_implemented;
            var materialRegions = 0; var region = record.FirstRegion;
            for (var i = 0; i < record.RegionCount; i++, region = Regions[region].NextInBody)
                if (Regions[region].IsSolid != 0) materialRegions++;
            if (materialRegions != 1) return ParasolidConstants.PK_ERROR_not_implemented;
            var face = record.FirstFaceBody;
            for (var f = 0; f < record.FaceCountBody; f++, face = Faces[face].NextInBody)
            {
                var slot = GetSurfaceSlotByTag(Faces[face].SurfTag);
                if (slot < 0) return ParasolidConstants.PK_ERROR_missing_geom;
                if (Surfaces[slot].Class == SurfaceClass.Cylinder && !foundAxis)
                {
                    var c = CylinderDataPool[Surfaces[slot].DataIndex];
                    frame.Origin = Vector(c.LocationX, c.LocationY, c.LocationZ);
                    frame.Axis = Vector(c.AxisX, c.AxisY, c.AxisZ);
                    frame.Reference = Vector(c.RefDirX, c.RefDirY, c.RefDirZ);
                    foundAxis = true;
                }
                else if (Surfaces[slot].Class is not (SurfaceClass.Cylinder or SurfaceClass.Plane))
                    return ParasolidConstants.PK_ERROR_not_implemented;
                if (Surfaces[slot].Class == SurfaceClass.Cylinder)
                    frame.Scale = Math.Max(frame.Scale, CylinderDataPool[Surfaces[slot].DataIndex].Radius);
            }
        }
        if (!foundAxis) return ParasolidConstants.PK_ERROR_not_implemented;
        for (var b = 0; b < inputs.Length; b++)
        {
            var body = TagRec(inputs[b]).Slot; var face = Bodies[body].FirstFaceBody;
            for (var f = 0; f < Bodies[body].FaceCountBody; f++, face = Faces[face].NextInBody)
            {
                if (count == faces.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                ref readonly var actual = ref Faces[face];
                var surface = Surfaces[GetSurfaceSlotByTag(actual.SurfTag)];
                var d = new RotationalFace { Face = face, Body = body, Input = b, Surface = actual.SurfTag,
                    Sign = LocalSenseSign(actual.Orientation) * LocalSenseSign(surface.Sense), LowEdge = -1, HighEdge = -1 };
                if (surface.Class == SurfaceClass.Cylinder)
                {
                    var c = CylinderDataPool[surface.DataIndex];
                    var axis = Vector(c.AxisX, c.AxisY, c.AxisZ);
                    var delta = Sub(Vector(c.LocationX, c.LocationY, c.LocationZ), frame.Origin);
                    if (!RotationalDirectionsAgree(axis, frame.Axis, Math.Max(frame.Scale, OperationIntersection.Norm(delta)))
                        || OperationIntersection.Norm(Sub(delta, Scale(frame.Axis, Dot(delta, frame.Axis)))) > RotationalTolerance
                        || !(c.Radius > RotationalTolerance) || actual.LoopCount != 2)
                        return ParasolidConstants.PK_ERROR_not_implemented;
                    d.Kind = 1; d.Coordinate = c.Radius;
                }
                else
                {
                    var p = PlaneDataPool[surface.DataIndex];
                    var normal = Vector(p.NormalX, p.NormalY, p.NormalZ); var alignment = Dot(normal, frame.Axis);
                    if (!RotationalDirectionsAgree(normal, frame.Axis, frame.Scale) || actual.LoopCount is < 1 or > 2)
                        return ParasolidConstants.PK_ERROR_not_implemented;
                    d.Coordinate = Dot(Sub(Vector(p.LocationX, p.LocationY, p.LocationZ), frame.Origin), frame.Axis);
                    d.Sign *= alignment > 0 ? 1 : -1;
                }
                var loop = actual.FirstLoop;
                double first = 0, second = 0; var firstDirection = 0; var secondDirection = 0;
                for (var l = 0; l < actual.LoopCount; l++, loop = Loops[loop].NextInFace)
                {
                    if (loop < 0 || Loops[loop].FinCount != 1) return ParasolidConstants.PK_ERROR_not_implemented;
                    var fin = Loops[loop].FirstFin; var edge = Fins[fin].Edge;
                    var error = ReadRotationalRing(edge, in frame, out var radius, out var height, out var direction);
                    if (error != 0) return error;
                    direction *= Fins[fin].Sense == '+' ? 1 : -1;
                    if (d.Kind == 1 ? Math.Abs(radius - d.Coordinate) > RotationalTolerance : Math.Abs(height - d.Coordinate) > RotationalTolerance)
                        return ParasolidConstants.PK_ERROR_bad_loop;
                    if (l == 0) { first = d.Kind == 1 ? height : radius; firstDirection = direction; d.LowEdge = edge; }
                    else { second = d.Kind == 1 ? height : radius; secondDirection = direction; d.HighEdge = edge; }
                }
                if (actual.LoopCount == 1)
                {
                    d.Low = 0; d.High = first; d.HighEdge = d.LowEdge; d.LowEdge = -1;
                    if (firstDirection != d.Sign) return ParasolidConstants.PK_ERROR_bad_loop;
                }
                else
                {
                    if (first > second)
                    {
                        (first, second) = (second, first); (firstDirection, secondDirection) = (secondDirection, firstDirection);
                        (d.LowEdge, d.HighEdge) = (d.HighEdge, d.LowEdge);
                    }
                    d.Low = first; d.High = second;
                    if (second - first <= RotationalTolerance || firstDirection != (d.Kind == 1 ? d.Sign : -d.Sign)
                        || secondDirection != -firstDirection) return ParasolidConstants.PK_ERROR_bad_loop;
                }
                faces[count++] = d;
            }
        }
        return 0;
    }

    private static int ReadRotationalRing(EdgeSlot edge, in RotationalFrame frame,
        out double radius, out double height, out int direction)
    {
        radius = height = 0; direction = 0;
        if (edge < 0 || Edges[edge].StartVertex >= 0 || Edges[edge].EndVertex >= 0 || Edges[edge].FinCount != 2)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var curve = GetCurveSlotByTag(Edges[edge].CurveTag);
        if (curve < 0) return HasBoundaryEdgeGeometry(in Edges[edge]) ? ParasolidConstants.PK_ERROR_not_implemented : ParasolidConstants.PK_ERROR_missing_geom;
        if (Curves[curve].Class != CurveClass.Circle) return ParasolidConstants.PK_ERROR_not_implemented;
        var c = CircleDataPool[Curves[curve].DataIndex];
        var delta = Sub(Vector(c.CenterX, c.CenterY, c.CenterZ), frame.Origin);
        height = Dot(delta, frame.Axis); radius = c.Radius;
        var alignment = Dot(Vector(c.AxisX, c.AxisY, c.AxisZ), frame.Axis);
        if (!RotationalDirectionsAgree(Vector(c.AxisX, c.AxisY, c.AxisZ), frame.Axis, Math.Max(radius, Math.Abs(height)))
            || OperationIntersection.Norm(Sub(delta, Scale(frame.Axis, height))) > RotationalTolerance
            || !(radius > RotationalTolerance)) return ParasolidConstants.PK_ERROR_not_implemented;
        direction = (alignment > 0 ? 1 : -1) * LocalSenseSign(Curves[curve].Sense);
        return 0;
    }

    private static bool RotationalDirectionsAgree(in KernelVector3 a, in KernelVector3 b, double scale)
    {
        // Dot products lose first-order angular information near parallelism.
        // Admit only rounding-scale angular error, additionally bounded in space.
        var cross = OperationIntersection.Norm(Geometry.Evaluation.EvaluationMath.Cross(a, b));
        return cross <= 8e-15 && cross * Math.Max(1, scale) <= RotationalTolerance * .125;
    }

    private static int RotationalWinding(ReadOnlySpan<RotationalFace> faces, int input, double radius, double height,
        RegionSlot voidRegion = -1)
    {
        var winding = 0;
        foreach (ref readonly var f in faces)
        {
            if (f.Input != input || f.Kind != 1 || height <= f.Low || height >= f.High || radius >= f.Coordinate) continue;
            if (voidRegion >= 0)
            {
                var use = Faces[f.Face].FrontFaceUse;
                if (use < 0 || Shells[FaceUses[use].Shell].Region != voidRegion) continue;
            }
            winding += f.Sign;
        }
        return winding;
    }

    private static bool AddRotationalCoordinate(Span<double> values, ref int count, double value)
    {
        if (!double.IsFinite(value)) return false;
        for (var i = 0; i < count; i++) if (Math.Abs(values[i] - value) <= RotationalTolerance) return true;
        if (count == values.Length) return false; values[count++] = value; return true;
    }

    private static int RotationalCoordinates(ReadOnlySpan<RotationalFace> faces, Span<double> radii, Span<double> heights,
        out int radialCount, out int heightCount)
    {
        radialCount = 1; heightCount = 0; radii[0] = 0;
        foreach (ref readonly var f in faces)
        {
            if (f.Kind == 1)
            {
                if (!AddRotationalCoordinate(radii, ref radialCount, f.Coordinate)
                    || !AddRotationalCoordinate(heights, ref heightCount, f.Low)
                    || !AddRotationalCoordinate(heights, ref heightCount, f.High)) return ParasolidConstants.PK_ERROR_not_implemented;
            }
            else if (!AddRotationalCoordinate(heights, ref heightCount, f.Coordinate)
                || !AddRotationalCoordinate(radii, ref radialCount, f.Low)
                || !AddRotationalCoordinate(radii, ref radialCount, f.High)) return ParasolidConstants.PK_ERROR_not_implemented;
        }
        radii[..radialCount].Sort(); heights[..heightCount].Sort();
        return radialCount > 1 && heightCount > 1 ? 0 : ParasolidConstants.PK_ERROR_bad_loop;
    }

    private static int FillRotationalGrid(ReadOnlySpan<RotationalFace> faces, int inputCount,
        ReadOnlySpan<double> radii, ReadOnlySpan<double> heights, Span<uint> masks)
    {
        var width = radii.Length - 1;
        for (var z = 0; z + 1 < heights.Length; z++)
        for (var r = 0; r < width; r++)
        {
            var radius = radii[r] + (radii[r + 1] - radii[r]) * .5;
            var height = heights[z] + (heights[z + 1] - heights[z]) * .5; uint mask = 0;
            for (var b = 0; b < inputCount; b++)
            {
                var winding = RotationalWinding(faces, b, radius, height);
                if (winding is not (0 or 1)) return ParasolidConstants.PK_ERROR_bad_loop;
                if (winding == 1) mask |= 1u << b;
            }
            masks[z * width + r] = mask;
        }
        // Every oriented plane boundary must agree with the material jump;
        // this catches incomplete, inverted or geometrically inconsistent input.
        for (var z = 0; z < heights.Length; z++)
        for (var r = 0; r < width; r++)
        {
            var radius = radii[r] + (radii[r + 1] - radii[r]) * .5;
            var before = z == 0 ? 0u : masks[(z - 1) * width + r];
            var after = z == heights.Length - 1 ? 0u : masks[z * width + r];
            for (var b = 0; b < inputCount; b++)
            {
                var jump = 0;
                foreach (ref readonly var f in faces)
                    if (f.Input == b && f.Kind == 0 && Math.Abs(f.Coordinate - heights[z]) <= RotationalTolerance
                        && radius > f.Low && radius < f.High) jump += f.Sign;
                if (jump != (int)((before >> b) & 1) - (int)((after >> b) & 1)) return ParasolidConstants.PK_ERROR_bad_loop;
            }
        }
        return 0;
    }

    private static bool RotationalMaterial(uint mask, int function)
        => function == ParasolidConstants.PK_boolean_unite_c ? mask != 0
        : function == ParasolidConstants.PK_boolean_intersect_c ? (mask & 1) != 0 && (mask & ~1u) != 0
        : (mask & 1) != 0 && (mask & ~1u) == 0;

    private static int TryRotationalBoolean(ReadOnlySpan<EntityTag> inputs, PK_BODY_boolean_o_s* options,
        PK_TOPOL_track_r_s* tracking, PK_boolean_r_s* result, out bool handled)
    {
        handled = false;
        Span<RotationalFace> faceBuffer = stackalloc RotationalFace[RotationalFaceLimit];
        var error = ReadRotationalModel(inputs, faceBuffer, out var nf, out var frame);
        if (error == ParasolidConstants.PK_ERROR_not_implemented) return 0;
        handled = true; if (error != 0) return error; var faces = faceBuffer[..nf];
        Span<double> radiiBuffer = stackalloc double[RotationalCoordinateLimit];
        Span<double> heightsBuffer = stackalloc double[RotationalCoordinateLimit];
        error = RotationalCoordinates(faces, radiiBuffer, heightsBuffer, out var nr, out var nz); if (error != 0) return error;
        var radii = radiiBuffer[..nr]; var heights = heightsBuffer[..nz]; var cellCount = (nr - 1) * (nz - 1);
        var scratch = CommandScratch.Current; var memory = scratch.Take(cellCount * 2 + 2);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        int* returned = null; PK_TOPOL_track_r_s localTracking = default; var complete = false;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var masks = ModelSpan<uint>(bytes, ref cursor, cellCount);
            var components = ModelSpan<int>(bytes, ref cursor, cellCount);
            var voids = ModelSpan<int>(bytes, ref cursor, cellCount);
            var work = ModelSpan<int>(bytes, ref cursor, cellCount);
            error = FillRotationalGrid(faces, inputs.Length, radii, heights, masks); if (error != 0) return error;
            var clash = false; var anyClash = false; var targetLost = false;
            foreach (var mask in masks) if ((mask & 1) != 0 && (mask & ~1u) != 0) clash = true;
            foreach (var mask in masks) if ((mask & (mask - 1)) != 0) anyClash = true;
            foreach (var mask in masks) if ((mask & 1) != 0 && (mask & ~1u) == 0) targetLost = true;
            var touching = RotationalInputsTouch(faces);
            if (!clash) clash = touching;
            var function = options->function;
            if (function == ParasolidConstants.PK_boolean_unite_c) clash = anyClash || RotationalInputsTouch(faces, true);
            if (!clash)
            {
                var count = function == ParasolidConstants.PK_boolean_unite_c ? inputs.Length : function == ParasolidConstants.PK_boolean_subtract_c ? 1 : 0;
                returned = count > 0 ? AllocateReturnSlice(count) : null;
                if (count > 0 && returned == null) return ParasolidConstants.PK_ERROR_memory_full;
                if (count > 0) inputs[..count].CopyTo(new Span<int>(returned, count));
                if (function != ParasolidConstants.PK_boolean_unite_c)
                    for (var b = 1; b < inputs.Length; b++) { var tool = inputs[b]; error = EntityDelete(1, &tool); if (error != 0) return error; }
                if (count == 0) { var target = inputs[0]; error = EntityDelete(1, &target); if (error != 0) return error; }
                *result = new() { result = ParasolidConstants.PK_boolean_result_no_clash_c, n_bodies = count, bodies = returned };
                complete = true; return 0;
            }
            error = LabelRotationalGrid(masks, nr - 1, nz - 1, function, components, voids, work, out var componentCount);
            if (error != 0) return error;
            if (componentCount > RotationalFaceLimit) return ParasolidConstants.PK_ERROR_not_implemented;
            Span<uint> componentSources = stackalloc uint[componentCount]; componentSources.Clear();
            for (var i = 0; i < cellCount; i++) if (components[i] >= 0) componentSources[components[i]] |= masks[i];
            // Union keeps the earliest input body in each connected component.
            // Isolated tools retain their original body, faces and edges.
            Span<int> primary = stackalloc int[componentCount]; primary.Fill(-1);
            if (function == ParasolidConstants.PK_boolean_unite_c)
            {
                Span<int> order = stackalloc int[componentCount];
                for (var c = 0; c < componentCount; c++)
                {
                    var b = 0; while ((componentSources[c] & (1u << b)) == 0) b++;
                    primary[c] = b; order[c] = c;
                }
                for (var c = 0; c < componentCount; c++) for (var j = c + 1; j < componentCount; j++)
                    if (primary[order[j]] < primary[order[c]]) (order[c], order[j]) = (order[j], order[c]);
                Span<int> map = stackalloc int[componentCount]; Span<int> orderedPrimary = stackalloc int[componentCount];
                Span<uint> orderedSources = stackalloc uint[componentCount];
                for (var c = 0; c < componentCount; c++) { map[order[c]] = c; orderedPrimary[c] = primary[order[c]]; orderedSources[c] = componentSources[order[c]]; }
                for (var i = 0; i < cellCount; i++) if (components[i] >= 0) components[i] = map[components[i]];
                orderedPrimary.CopyTo(primary); orderedSources.CopyTo(componentSources);
                for (var c = 1; c < componentCount; c++) if (primary[c] == primary[c - 1]) return ParasolidConstants.PK_ERROR_not_implemented;
            }
            else if (componentCount > 0) primary[0] = 0;
            returned = componentCount > 0 ? AllocateReturnSlice(componentCount) : null;
            if (componentCount > 0 && returned == null) return ParasolidConstants.PK_ERROR_memory_full;
            Span<FaceSlot> sourceProducts = stackalloc FaceSlot[nf]; sourceProducts.Fill(-1);
            for (var c = 0; c < componentCount; c++)
            {
                if (function == ParasolidConstants.PK_boolean_unite_c && (componentSources[c] & (componentSources[c] - 1)) == 0)
                { returned[c] = inputs[primary[c]]; continue; }
                LabelRotationalVoids(components, c, nr - 1, nz - 1, voids, work);
                error = BuildRotationalGridBody(faces, frame, radii, heights, masks, components, voids, c,
                    options->merge_imprinted != 0, sourceProducts, out returned[c]); if (error != 0) return error;
            }
            // A source face keeps its handle on the first surviving product.
            Span<FaceSlot> old = stackalloc FaceSlot[1]; Span<FaceSlot> product = stackalloc FaceSlot[1];
            for (var f = 0; f < nf; f++) if (sourceProducts[f] >= 0)
            {
                old[0] = faces[f].Face; product[0] = sourceProducts[f];
                error = PreserveModelingFaceHandles(faces[f].Body, Shells[Faces[product[0]].FrontShell].Body, old, product);
                if (error != 0) return error;
            }
            Span<FaceSlot> moved = stackalloc FaceSlot[nf]; Span<FaceSlot> retained = stackalloc FaceSlot[nf];
            for (var b = 0; b < inputs.Length; b++) for (var c = 0; c < componentCount; c++)
            {
                var destination = TagRec(returned[c]).Slot; var sourceBody = TagRec(inputs[b]).Slot;
                if (destination == sourceBody) continue;
                var retainedCount = 0;
                for (var f = 0; f < nf; f++) if (faces[f].Input == b && sourceProducts[f] >= 0
                    && Shells[Faces[faces[f].Face].FrontShell].Body == destination)
                { moved[retainedCount] = sourceProducts[f]; retained[retainedCount++] = faces[f].Face; }
                if (retainedCount < 2) continue;
                error = RetainLocalSurvivingBoundary(sourceBody, destination, moved[..retainedCount], retained[..retainedCount]);
                if (error != 0) return error;
            }
            if (options->tracking != 0)
            {
                error = CreateRotationalTracking(faces, frame, inputs.Length, new ReadOnlySpan<int>(returned, componentCount), &localTracking);
                if (error != 0) return error;
            }
            Span<byte> preserved = stackalloc byte[inputs.Length]; preserved.Clear();
            for (var c = 0; c < componentCount; c++) if (primary[c] >= 0)
            {
                var original = inputs[primary[c]];
                if (returned[c] != original)
                {
                    error = ReplaceProfileBody(TagRec(original).Slot, TagRec(returned[c]).Slot); if (error != 0) return error;
                    returned[c] = original;
                }
                preserved[primary[c]] = 1;
            }
            for (var b = 0; b < inputs.Length; b++) if (preserved[b] == 0)
            { var consumed = inputs[b]; error = EntityDelete(1, &consumed); if (error != 0) return error; }
            *tracking = localTracking;
            *result = new() { result = clash && !(function == ParasolidConstants.PK_boolean_intersect_c && !targetLost && !touching)
                    ? ParasolidConstants.PK_boolean_result_success_c : ParasolidConstants.PK_boolean_result_no_clash_c,
                n_bodies = componentCount, bodies = returned };
            complete = true; return 0;
        }
        finally
        {
            if (!complete) { State.Session->Returns.TryFree(returned); if (localTracking.track_records != null) TopolTrackResultFree(&localTracking); }
            scratch.Return(memory);
        }
    }

    private static bool RotationalInputsTouch(ReadOnlySpan<RotationalFace> faces, bool allInputs = false)
    {
        foreach (ref readonly var a in faces) if (allInputs || a.Input == 0)
        foreach (ref readonly var b in faces) if (b.Input > a.Input)
        {
            if (a.Kind == b.Kind)
            {
                if (Math.Abs(a.Coordinate - b.Coordinate) <= RotationalTolerance
                    && Math.Max(a.Low, b.Low) <= Math.Min(a.High, b.High) + RotationalTolerance) return true;
            }
            else if (a.Coordinate >= b.Low - RotationalTolerance && a.Coordinate <= b.High + RotationalTolerance
                && b.Coordinate >= a.Low - RotationalTolerance && b.Coordinate <= a.High + RotationalTolerance) return true;
        }
        return false;
    }
}
