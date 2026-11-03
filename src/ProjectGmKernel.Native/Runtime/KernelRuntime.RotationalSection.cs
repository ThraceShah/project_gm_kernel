using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int TrySectionRotationalBody(EntityTag body, SurfTag surface, PK_BODY_section_o_s* options,
        PK_section_r_s* result, out bool handled)
    {
        handled = false; Span<EntityTag> input = stackalloc EntityTag[1] { body };
        Span<RotationalFace> storage = stackalloc RotationalFace[RotationalFaceLimit];
        var error = ReadRotationalModel(input, storage, out var count, out var frame);
        if (error == ParasolidConstants.PK_ERROR_not_implemented) return 0;
        if (error != 0) { handled = true; return error; }
        var planeRecord = Surfaces[GetSurfaceSlotByTag(surface)];
        var plane = PlaneDataPool[planeRecord.DataIndex];
        var normal = Vector(plane.NormalX, plane.NormalY, plane.NormalZ);
        if (!RotationalDirectionsAgree(normal, frame.Axis, frame.Scale)) return 0;
        handled = true; normal = Scale(normal, LocalSenseSign(planeRecord.Sense));
        var origin = Vector(plane.LocationX, plane.LocationY, plane.LocationZ);
        var height = Dot(Sub(origin, frame.Origin), frame.Axis);
        var sign = Dot(normal, frame.Axis) > 0 ? 1 : -1;
        var low = double.PositiveInfinity; var high = double.NegativeInfinity; var radius = 0d;
        var sources = storage[..count];
        foreach (ref readonly var f in sources) if (f.Kind == 1)
        { low = Math.Min(low, f.Low); high = Math.Max(high, f.High); radius = Math.Max(radius, f.Coordinate); }
        var preserveTool = planeRecord.OwnerCount > 0
            && GeometryBody(PoolKind.Surface, GetSurfaceSlotByTag(surface)) == TagRec(body).Slot;
        if (height <= low + RotationalTolerance || height >= high - RotationalTolerance)
        {
            var front = (height <= low + RotationalTolerance) == (sign > 0);
            if (front && options->fence == ParasolidConstants.PK_section_fence_back_c
                || !front && options->fence == ParasolidConstants.PK_section_fence_front_c)
                return EntityDelete(1, &body);
            var kept = AllocateReturnSlice(1); if (kept == null) return ParasolidConstants.PK_ERROR_memory_full; kept[0] = body;
            if (front) result->front_bodies = new() { length = 1, array = kept }; else result->back_bodies = new() { length = 1, array = kept };
            return 0;
        }
        if (preserveTool)
        {
            error = DetachRotationalCuttingTool(body, surface); if (error != 0) return error;
        }
        var back = body;
        if (options->fence == ParasolidConstants.PK_section_fence_both_c)
        { error = TryCloneEntityCore(body, out back); if (error != 0) return error; }
        var margin = Math.Max(1, Math.Max(radius, high - low));
        var center = Add(frame.Origin, Scale(frame.Axis, height));
        var boolean = DefaultModelingBooleanOptions(ParasolidConstants.PK_boolean_intersect_c);
        boolean.default_tol = options->default_tol; boolean.max_tol = options->max_tol;
        PK_section_r_s output = default; var completed = false;
        Span<EntityTag> originalFaces = stackalloc EntityTag[RotationalFaceLimit];
        Span<EntityTag> caps = stackalloc EntityTag[1024];
        try
        {
            for (var side = 0; side < 2; side++)
            {
                var front = side == 0;
                if (front && options->fence == ParasolidConstants.PK_section_fence_back_c
                    || !front && options->fence == ParasolidConstants.PK_section_fence_front_c) continue;
                var axis = front ? normal : Scale(normal, -1);
                var basis = new PK_AXIS2_sf_s(); basis.location.coord[0] = center.X; basis.location.coord[1] = center.Y; basis.location.coord[2] = center.Z;
                basis.axis.coord[0] = axis.X; basis.axis.coord[1] = axis.Y; basis.axis.coord[2] = axis.Z;
                basis.ref_direction.coord[0] = frame.Reference.X; basis.ref_direction.coord[1] = frame.Reference.Y; basis.ref_direction.coord[2] = frame.Reference.Z;
                EntityTag halfspace = 0; var upper = (front ? sign : -sign) > 0;
                error = BodyCreateSolidCyl(radius + margin, (upper ? high - height : height - low) + margin, &basis, &halfspace);
                if (error != 0) return error;
                var target = front ? body : back; var slot = TagRec(target).Slot;
                var oldCount = Bodies[slot].FaceCountBody; var oldFace = Bodies[slot].FirstFaceBody;
                for (var i = 0; i < oldCount; i++, oldFace = Faces[oldFace].NextInBody) originalFaces[i] = Faces[oldFace].Header.Tag;
                PK_TOPOL_track_r_s tracking = default; PK_boolean_r_s split = default;
                error = BodyBooleanCore(target, 1, &halfspace, &boolean, &tracking, &split); if (error != 0) return error;
                if (front) output.front_bodies = new() { length = split.n_bodies, array = split.bodies };
                else output.back_bodies = new() { length = split.n_bodies, array = split.bodies };
                var capCount = 0;
                for (var b = 0; b < split.n_bodies; b++)
                {
                    var product = TagRec(split.bodies[b]).Slot; var face = Bodies[product].FirstFaceBody;
                    for (var i = 0; i < Bodies[product].FaceCountBody; i++, face = Faces[face].NextInBody)
                    {
                        if (originalFaces[..oldCount].Contains(Faces[face].Header.Tag)) continue;
                        var record = Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)];
                        if (record.Class != SurfaceClass.Plane) continue;
                        var p = PlaneDataPool[record.DataIndex];
                        if (Math.Abs(Dot(Sub(Vector(p.LocationX, p.LocationY, p.LocationZ), origin), normal)) > RotationalTolerance) continue;
                        if (capCount == caps.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                        caps[capCount++] = Faces[face].Header.Tag;
                        var oldSurface = Faces[face].SurfTag;
                        // The cutting support may belong to the consumed input.
                        // Its captured definition remains independent of that lifetime.
                        var capSurface = CreatePlaneSurfaceTag(plane.LocationX, plane.LocationY, plane.LocationZ,
                            plane.NormalX, plane.NormalY, plane.NormalZ, plane.RefDirX, plane.RefDirY, plane.RefDirZ);
                        if (capSurface <= 0) return ParasolidConstants.PK_ERROR_memory_full;
                        Surfaces[GetSurfaceSlotByTag(capSurface)].Sense = planeRecord.Sense;
                        var faceTag = Faces[face].Header.Tag; error = TopologyDetachGeometry(faceTag); if (error != 0) return error;
                        var sense = front ? (byte)0 : (byte)1;
                        error = FaceAttachSurfaces(1, &faceTag, &capSurface, &sense); if (error != 0) return error;
                        if (Surfaces[GetSurfaceSlotByTag(oldSurface)].OwnerCount == 0)
                        { error = EntityDelete(1, &oldSurface); if (error != 0) return error; }
                        var loop = Faces[face].FirstLoop;
                        for (var l = 0; l < Faces[face].LoopCount; l++, loop = Loops[loop].NextInFace)
                        {
                            var edge = Fins[Loops[loop].FirstFin].Edge;
                            error = NormalizeRotationalSectionRing(edge, sources, frame, in plane); if (error != 0) return error;
                        }
                    }
                }
                var returned = capCount > 0 ? AllocateReturnSlice(capCount) : null;
                if (capCount > 0 && returned == null) return ParasolidConstants.PK_ERROR_memory_full;
                if (capCount > 0) caps[..capCount].CopyTo(new Span<int>(returned, capCount));
                if (front) output.front_faces = new() { length = capCount, array = returned };
                else output.back_faces = new() { length = capCount, array = returned };
            }
            if (preserveTool)
            { error = RestoreRotationalSectionTool(surface, sources, frame, in output); if (error != 0) return error; }
            *result = output; completed = true; return 0;
        }
        finally
        {
            if (!completed)
            {
                State.Session->Returns.TryFree(output.front_bodies.array); State.Session->Returns.TryFree(output.back_bodies.array);
                State.Session->Returns.TryFree(output.front_faces.array); State.Session->Returns.TryFree(output.back_faces.array);
            }
        }
    }

    private static int DetachRotationalCuttingTool(EntityTag body, SurfTag surface)
    {
        if (Surfaces[GetSurfaceSlotByTag(surface)].GeometricOwnerCount != 0) return ParasolidConstants.PK_ERROR_not_implemented;
        var error = TryCloneEntityCore(surface, out var replacement); if (error != 0) return error;
        var sourceBody = TagRec(body).Slot; var face = Bodies[sourceBody].FirstFaceBody;
        for (var i = 0; i < Bodies[sourceBody].FaceCountBody; i++, face = Faces[face].NextInBody)
        {
            if (Faces[face].SurfTag != surface) continue;
            var tag = Faces[face].Header.Tag; var sense = Faces[face].Orientation == ParasolidConstants.PK_TOPOL_sense_negative_c ? (byte)0 : (byte)1;
            error = TopologyDetachGeometry(tag); if (error != 0) return error;
            error = FaceAttachSurfaces(1, &tag, &replacement, &sense); if (error != 0) return error;
        }
        return 0;
    }

    private static int RestoreRotationalSectionTool(SurfTag surface, ReadOnlySpan<RotationalFace> originals,
        in RotationalFrame frame, in PK_section_r_s result)
    {
        var owner = -1;
        for (var side = 0; side < 2; side++)
        {
            var bodies = side == 0 ? result.front_bodies : result.back_bodies;
            for (var b = 0; b < bodies.length; b++)
            {
                var body = TagRec(bodies.array[b]).Slot; if (owner >= 0 && owner != body) continue;
                var face = Bodies[body].FirstFaceBody;
                for (var f = 0; f < Bodies[body].FaceCountBody; f++, face = Faces[face].NextInBody)
                {
                    var oldSurface = Faces[face].SurfTag; var record = Surfaces[GetSurfaceSlotByTag(oldSurface)];
                    if (record.Class != SurfaceClass.Plane) continue;
                    var plane = PlaneDataPool[record.DataIndex];
                    var height = Dot(Sub(Vector(plane.LocationX, plane.LocationY, plane.LocationZ), frame.Origin), frame.Axis);
                    var sign = (Dot(Vector(plane.NormalX, plane.NormalY, plane.NormalZ), frame.Axis) > 0 ? 1 : -1)
                        * LocalSenseSign(record.Sense) * LocalSenseSign(Faces[face].Orientation);
                    var low = Faces[face].LoopCount == 1 ? 0 : double.PositiveInfinity; var high = 0d; var loop = Faces[face].FirstLoop;
                    for (var l = 0; l < Faces[face].LoopCount; l++, loop = Loops[loop].NextInFace)
                    {
                        var error = ReadRotationalRing(Fins[Loops[loop].FirstFin].Edge, frame, out var r, out _, out _); if (error != 0) return error;
                        low = Math.Min(low, r); high = Math.Max(high, r);
                    }
                    var retained = false;
                    foreach (ref readonly var original in originals)
                        if (original.Kind == 0 && original.Surface == surface && original.Sign == sign
                            && Math.Abs(original.Coordinate - height) <= RotationalTolerance
                            && Math.Min(original.High, high) - Math.Max(original.Low, low) > RotationalTolerance) retained = true;
                    if (!retained) continue;
                    var originalRecord = Surfaces[GetSurfaceSlotByTag(surface)]; var originalPlane = PlaneDataPool[originalRecord.DataIndex];
                    var originalNormalSign = (Dot(Vector(originalPlane.NormalX, originalPlane.NormalY, originalPlane.NormalZ), frame.Axis) > 0 ? 1 : -1)
                        * LocalSenseSign(originalRecord.Sense);
                    var orientation = sign == originalNormalSign ? (byte)1 : (byte)0; var faceTag = Faces[face].Header.Tag;
                    var detached = TopologyDetachGeometry(faceTag); if (detached != 0) return detached;
                    var attached = FaceAttachSurfaces(1, &faceTag, &surface, &orientation); if (attached != 0) return attached;
                    owner = body;
                    var old = GetSurfaceSlotByTag(oldSurface);
                    if (Surfaces[old].OwnerCount == 0 && Surfaces[old].GeometricOwnerCount == 0)
                    { var deleted = EntityDelete(1, &oldSurface); if (deleted != 0) return deleted; }
                }
            }
        }
        return owner >= 0 ? 0 : EntityDelete(1, &surface);
    }

    private static int NormalizeRotationalSectionRing(EdgeSlot edge, ReadOnlySpan<RotationalFace> original,
        in RotationalFrame frame, in PlaneData plane)
    {
        var error = ReadRotationalRing(edge, frame, out var radius, out var height, out _); if (error != 0) return error;
        var existing = false;
        foreach (ref readonly var f in original) if (f.Kind == 1 && Math.Abs(f.Coordinate - radius) <= RotationalTolerance
            && (Math.Abs(f.Low - height) <= RotationalTolerance || Math.Abs(f.High - height) <= RotationalTolerance)) existing = true;
        if (existing)
        {
            var first = Edges[edge].FirstFinEdge; var last = Edges[edge].LastFinEdge;
            var a = Loops[Fins[first].Loop].Face; var b = Loops[Fins[last].Loop].Face;
            if (Surfaces[GetSurfaceSlotByTag(Faces[a].SurfTag)].Class == SurfaceClass.Cylinder
                || Surfaces[GetSurfaceSlotByTag(Faces[b].SurfTag)].Class == SurfaceClass.Cylinder) return 0;
        }
        var curve = GetCurveSlotByTag(Edges[edge].CurveTag); ref var record = ref Curves[curve]; ref var circle = ref CircleDataPool[record.DataIndex];
        var axis = Vector(plane.NormalX, plane.NormalY, plane.NormalZ);
        var oldDirection = Dot(Vector(circle.AxisX, circle.AxisY, circle.AxisZ), axis) * LocalSenseSign(record.Sense);
        if (oldDirection > 0)
        {
            var first = Edges[edge].FirstFinEdge; var last = Edges[edge].LastFinEdge;
            Fins[first].Sense = Fins[first].Sense == '+' ? '-' : '+'; Fins[last].Sense = Fins[last].Sense == '+' ? '-' : '+';
        }
        record.Sense = ParasolidConstants.PK_TOPOL_sense_negative_c;
        circle.AxisX = axis.X; circle.AxisY = axis.Y; circle.AxisZ = axis.Z;
        circle.RefDirX = plane.RefDirX; circle.RefDirY = plane.RefDirY; circle.RefDirZ = plane.RefDirZ;
        return 0;
    }
}
