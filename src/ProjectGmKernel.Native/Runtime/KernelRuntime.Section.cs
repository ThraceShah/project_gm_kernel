using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodySectionWithSurf(EntityTag body, SurfTag surface, PK_BODY_section_o_s* options, PK_section_r_s* result)
    {
        if (Dispatcher.IsExecuting) return BodySectionWithSurfCore(body, surface, options, result);
        var command = new SectionCommand { Body = body, Surface = surface, Options = options, Result = result };
        var error = Dispatch(ApiId.BodySectionWithSurf, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, body);
        if (error != 0 && result != null) *result = default;
        return error;
    }

    private struct SectionCommand : IKernelCommand
    {
        internal EntityTag Body;
        internal SurfTag Surface;
        internal PK_BODY_section_o_s* Options;
        internal PK_section_r_s* Result;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return BodySectionWithSurfCore(Body, Surface, Options, Result); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int BodySectionWithSurfCore(EntityTag body, SurfTag surface, PK_BODY_section_o_s* options, PK_section_r_s* result)
    {
        if (result != null) *result = default;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (options == null || result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 5) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->fence is not (ParasolidConstants.PK_section_fence_both_c or ParasolidConstants.PK_section_fence_front_c or ParasolidConstants.PK_section_fence_back_c)
            || !double.IsFinite(options->default_tol) || options->default_tol <= 0 || !double.IsFinite(options->max_tol) || options->max_tol < 0
            || options->merge_imprinted > 1 || options->merge_new_faces > 1)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        // Section merge rules protect edges adjacent to new caps unless
        // merge_new_faces is set. A Boolean's generic coplanar merge flag does
        // not implement those rules and must not silently replace them.
        if (options->matched_region != null || options->selective_merge != 0 || options->keep_target_edges != 0
            || options->merge_imprinted != 0 || options->merge_new_faces != 0)
            return ParasolidConstants.PK_ERROR_not_implemented;
        if (!TryResolveBodySlot(body, out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
        if (Bodies[slot].BodyType != ParasolidConstants.PK_BODY_type_solid_c) return ParasolidConstants.PK_ERROR_not_implemented;
        if (!State.Session->Tags.TryResolve(surface, CurrentSessionId, out var sr) || sr.Pool != (byte)PoolKind.Surface)
            return ParasolidConstants.PK_ERROR_bad_class;
        if (Surfaces[sr.Slot].Class != SurfaceClass.Plane) return ParasolidConstants.PK_ERROR_not_implemented;
        if (Bodies[slot].Header.Partition != CurrentPartition || Surfaces[sr.Slot].Header.Partition != CurrentPartition)
            return ParasolidConstants.PK_ERROR_not_in_same_partition;
        var sphereError = TrySectionSphere(body, surface, options, result, out var sphereHandled);
        if (sphereHandled) return sphereError;
        var rotationalError = TrySectionRotationalBody(body, surface, options, result, out var rotationalHandled);
        if (rotationalHandled) return rotationalError;
        var scratch = CommandScratch.Current; var workspace = scratch.Take(40000);
        if (workspace.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        KernelVector3 origin, normal, reference, transverse; double minX, maxX, minY, maxY, minZ, maxZ;
        try
        {
            var bytes = MemoryMarshal.AsBytes(workspace); var cursor = 0;
            var faces = ModelSpan<PlanarModelFace>(bytes, ref cursor, PlanarFaceCapacity);
            var loops = ModelSpan<PlanarModelLoop>(bytes, ref cursor, PlanarPolygonCapacity);
            var points = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            Span<EntityTag> input = stackalloc EntityTag[1]; input[0] = body;
            var error = ReadPlanarModel(input, faces, loops, points, out _, out _, out var np, out var tolerance);
            if (error != 0) return error;
            ref readonly var plane = ref PlaneDataPool[Surfaces[sr.Slot].DataIndex];
            origin = Vector(plane.LocationX, plane.LocationY, plane.LocationZ);
            var sign = Surfaces[sr.Slot].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c ? -1 : 1;
            normal = Vector(plane.NormalX * sign, plane.NormalY * sign, plane.NormalZ * sign);
            reference = Vector(plane.RefDirX, plane.RefDirY, plane.RefDirZ); transverse = ProfileCross(normal, reference);
            minX = minY = minZ = double.PositiveInfinity; maxX = maxY = maxZ = double.NegativeInfinity;
            for (var i = 0; i < np; i++)
            {
                var d = ProfileSubtract(points[i], origin); var x = ProfileDot(d, reference); var y = ProfileDot(d, transverse); var z = ProfileDot(d, normal);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
            }
            if (minZ >= -tolerance || maxZ <= tolerance)
            {
                var front = minZ >= -tolerance;
                if (front && options->fence == ParasolidConstants.PK_section_fence_back_c
                    || !front && options->fence == ParasolidConstants.PK_section_fence_front_c) return EntityDelete(1, &body);
                var returned = AllocateReturnSlice(1); if (returned == null) return ParasolidConstants.PK_ERROR_memory_full; returned[0] = body;
                if (front) result->front_bodies = new PK_BODY_array_s { length = 1, array = returned };
                else result->back_bodies = new PK_BODY_array_s { length = 1, array = returned };
                return 0;
            }
        }
        finally { scratch.Return(workspace); }

        // A finite half-space tool is exact over the bounded target: every
        // straight-edge planar face lies inside its vertex projection bounds.
        // The other five box planes are strictly outside those bounds and can
        // introduce no trimming edges. Both sides use the same exact CSG path.
        var both = options->fence == ParasolidConstants.PK_section_fence_both_c;
        var backTarget = body;
        if (both)
        {
            var copyError = TryCloneEntityCore(body, out backTarget);
            if (copyError != 0) return copyError;
        }
        var margin = Math.Max(1, Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)));
        var center = ProfileAdd(origin, ProfileAdd(ProfileScale(reference, minX + (maxX - minX) * 0.5), ProfileScale(transverse, minY + (maxY - minY) * 0.5)));
        var boolean = DefaultModelingBooleanOptions(ParasolidConstants.PK_boolean_intersect_c);
        boolean.default_tol = options->default_tol; boolean.max_tol = options->max_tol; boolean.merge_imprinted = options->merge_imprinted;
        Span<EntityTag> capFaces = stackalloc EntityTag[PlanarFaceCapacity];
        Span<EntityTag> originalFaces = stackalloc EntityTag[PlanarFaceCapacity];
        for (var side = 0; side < 2; side++)
        {
            var front = side == 0;
            if (front && options->fence == ParasolidConstants.PK_section_fence_back_c || !front && options->fence == ParasolidConstants.PK_section_fence_front_c) continue;
            var axis = new PK_AXIS2_sf_s(); axis.location.coord[0] = center.X; axis.location.coord[1] = center.Y; axis.location.coord[2] = center.Z;
            var n = front ? normal : ProfileScale(normal, -1);
            axis.axis.coord[0] = n.X; axis.axis.coord[1] = n.Y; axis.axis.coord[2] = n.Z;
            axis.ref_direction.coord[0] = reference.X; axis.ref_direction.coord[1] = reference.Y; axis.ref_direction.coord[2] = reference.Z;
            EntityTag halfspace = 0;
            var error = BodyCreateSolidBlock(maxX - minX + 2 * margin, maxY - minY + 2 * margin,
                (front ? maxZ : -minZ) + margin, &axis, &halfspace);
            if (error != 0) return error;
            PK_TOPOL_track_r_s tracking = default; PK_boolean_r_s output = default;
            var target = front ? body : backTarget;
            var targetSlot = TagRec(target).Slot; var originalCount = Bodies[targetSlot].FaceCountBody;
            var originalFace = Bodies[targetSlot].FirstFaceBody;
            for (var f = 0; f < originalCount; f++, originalFace = Faces[originalFace].NextInBody)
                originalFaces[f] = Faces[originalFace].Header.Tag;
            error = BodyBooleanCore(target, 1, &halfspace, &boolean, &tracking, &output);
            if (error != 0) return error;
            var bodies = new PK_BODY_array_s { length = output.n_bodies, array = output.bodies };
            var count = 0;
            for (var b = 0; b < output.n_bodies; b++)
            {
                var bs = TagRec(output.bodies[b]).Slot; var face = Bodies[bs].FirstFaceBody;
                for (var f = 0; f < Bodies[bs].FaceCountBody; f++, face = Faces[face].NextInBody)
                {
                    if (originalFaces[..originalCount].Contains(Faces[face].Header.Tag)) continue;
                    ref readonly var s = ref Surfaces[GetSurfaceSlotByTag(Faces[face].SurfTag)];
                    if (s.Class != SurfaceClass.Plane) continue;
                    ref readonly var p = ref PlaneDataPool[s.DataIndex];
                    var faceNormal = Vector(p.NormalX, p.NormalY, p.NormalZ);
                    if (Math.Abs(ProfileDot(faceNormal, normal)) > 1 - 1e-10
                        && Math.Abs(ProfileDot(ProfileSubtract(Vector(p.LocationX, p.LocationY, p.LocationZ), origin), normal)) <= 1e-8)
                    {
                        if (count == capFaces.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                        capFaces[count++] = Faces[face].Header.Tag;
                        // PK sections retain the cutting plane's orientation:
                        // front caps use it negatively, back caps positively.
                        var oldSurface = Faces[face].SurfTag;
                        error = TryCloneEntityCore(surface, out var capSurface);
                        if (error != 0) return error;
                        var faceTag = Faces[face].Header.Tag;
                        error = TopologyDetachGeometry(faceTag);
                        if (error != 0) return error;
                        var sense = front ? (byte)0 : (byte)1;
                        error = FaceAttachSurfaces(1, &faceTag, &capSurface, &sense);
                        if (error != 0) return error;
                        if (Surfaces[GetSurfaceSlotByTag(oldSurface)].OwnerCount == 0)
                        {
                            error = EntityDelete(1, &oldSurface);
                            if (error != 0) return error;
                        }
                    }
                }
            }
            var returnedFaces = count == 0 ? null : AllocateReturnSlice(count);
            if (count > 0 && returnedFaces == null) return ParasolidConstants.PK_ERROR_memory_full;
            if (count > 0) capFaces[..count].CopyTo(new Span<EntityTag>(returnedFaces, count));
            if (front) { result->front_bodies = bodies; result->front_faces = new PK_FACE_array_s { length = count, array = returnedFaces }; }
            else { result->back_bodies = bodies; result->back_faces = new PK_FACE_array_s { length = count, array = returnedFaces }; }
        }
        return 0;
    }

    internal static PK_BODY_boolean_o_s DefaultModelingBooleanOptions(int operation) => new()
    {
        o_t_version = 19, function = operation, default_tol = 1e-5,
        fence = ParasolidConstants.PK_boolean_fence_none_c, check_fa = ParasolidConstants.PK_boolean_check_fa_yes_c,
        imprint_complete_targ = ParasolidConstants.PK_imprint_complete_no_c, imprint_complete_tool = ParasolidConstants.PK_imprint_complete_no_c,
        target_material_side = ParasolidConstants.PK_boolean_material_default_c, tool_material_side = ParasolidConstants.PK_boolean_material_default_c,
        resulting_body_type = ParasolidConstants.PK_boolean_prefer_original_c, flag_no_effect = ParasolidConstants.PK_boolean_no_effect_basic_c,
        nm_edge_repair = ParasolidConstants.PK_nm_edge_repair_no_c, blend_radius = 1e-5,
        tracking_type = ParasolidConstants.PK_boolean_track_type_basic_c, track_regions = ParasolidConstants.PK_track_regions_no_c,
        update = ParasolidConstants.PK_boolean_update_default_c, merge_in_solid_dimension = ParasolidConstants.PK_TOPOL_dimension_any_c,
        merge_in_face_dimension = ParasolidConstants.PK_TOPOL_dimension_any_c, impose_bodies = ParasolidConstants.PK_boolean_impose_no_c,
        merge_tools = ParasolidConstants.PK_merge_tools_no_c,
    };

    public static int SectionResultFree(PK_section_r_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var error = result->front_bodies.array == null ? 0 : KernelRuntime.MemoryFree(result->front_bodies.array);
        if (error == 0 && result->back_bodies.array != null) error = KernelRuntime.MemoryFree(result->back_bodies.array);
        if (error == 0 && result->front_faces.array != null) error = KernelRuntime.MemoryFree(result->front_faces.array);
        if (error == 0 && result->back_faces.array != null) error = KernelRuntime.MemoryFree(result->back_faces.array);
        if (error == 0) *result = default;
        return error;
    }
}
