using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Construction;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyMakeLoftedBody(BufferCount count, BodyTag* profiles, EntityTag* starts,
        PK_BODY_make_lofted_body_o_s* options, PK_BODY_tracked_loft_s* result)
    {
        if (result != null) *result = default;
        if (Dispatcher.IsExecuting) return BodyMakeLoftedBodyCore(count, profiles, starts, options, result);
        var command = new FreeformBodyCommand { Operation = ApiId.BodyMakeLoftedBody, Count = count, Profiles = profiles,
            Starts = starts, Options = options, Result = result };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && profiles != null ? profiles[0] : 0);
    }
    public static int BodyTrackedLoftResultFree(PK_BODY_tracked_loft_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var error = KernelRuntime.TopolTrackResultFree(&result->tracking_info); if (error == 0) *result = default; return error;
    }
    public static int BodyMakeSweptBody(BodyTag profile, BodyTag path, EntityTag start,
        PK_BODY_make_swept_body_o_s* options, PK_BODY_tracked_sweep_r_s* result)
    {
        if (result != null) *result = default;
        if (Dispatcher.IsExecuting) return BodyMakeSweptBodyCore(profile, path, start, options, result);
        var command = new FreeformBodyCommand { Operation = ApiId.BodyMakeSweptBody, Profile = profile, Path = path,
            Start = start, Options = options, Result = result };
        return Dispatch(command.Operation, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, profile);
    }
    public static int BodyTrackedSweepResultFree(PK_BODY_tracked_sweep_r_s* result)
    {
        if (result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        var error = KernelRuntime.TopolTrackResultFree(&result->tracking_info); if (error == 0) *result = default; return error;
    }
    private struct FreeformBodyCommand : IKernelCommand
    {
        internal ApiId Operation;
        internal BufferCount Count;
        internal BodyTag* Profiles;
        internal EntityTag* Starts;
        internal BodyTag Profile, Path;
        internal EntityTag Start;
        internal void* Options;
        internal void* Result;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try
            {
                var error = Operation == ApiId.BodyMakeLoftedBody
                    ? BodyMakeLoftedBodyCore(Count, Profiles, Starts, (PK_BODY_make_lofted_body_o_s*)Options, (PK_BODY_tracked_loft_s*)Result)
                    : BodyMakeSweptBodyCore(Profile, Path, Start, (PK_BODY_make_swept_body_o_s*)Options, (PK_BODY_tracked_sweep_r_s*)Result);
                if (error != 0 && Result != null)
                {
                    if (Operation == ApiId.BodyMakeLoftedBody) ((PK_BODY_tracked_loft_s*)Result)->body = 0;
                    else ((PK_BODY_tracked_sweep_r_s*)Result)->body = 0;
                }
                return error;
            }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int FreeformReadProfile(BodyTag body, EntityTag start, Span<EdgeSlot> edges, Span<byte> directions,
        Span<int> offsets, out BufferCount edgeCount, out BufferCount loopCount, out KernelVector3 normal)
    {
        edgeCount = loopCount = 0; normal = default;
        if (!TryResolveBodySlot(body, out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
        ref readonly var input = ref Bodies[slot];
        if (input.Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
        if (!FreeformHasRequiredBoundaryGeometry(slot)) return ParasolidConstants.PK_ERROR_not_implemented;
        if (input.BodyType == ParasolidConstants.PK_BODY_type_wire_c)
            return FreeformReadWireProfile(slot, start, edges, directions, offsets, out edgeCount, out loopCount, out normal);
        if (input.BodyType != ParasolidConstants.PK_BODY_type_sheet_c || input.FaceCountBody != 1
            || input.EdgeCountBody > edges.Length || input.EdgeCountBody < 3) return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var face = ref Faces[input.FirstFaceBody]; var support = GetSurfaceSlotByTag(face.SurfTag);
        if (support < 0 || Surfaces[support].Class != SurfaceClass.Plane || face.LoopCount + 1 > offsets.Length)
            return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var plane = ref PlaneDataPool[Surfaces[support].DataIndex];
        normal = Vector(plane.NormalX, plane.NormalY, plane.NormalZ);
        if (face.Orientation == ParasolidConstants.PK_TOPOL_sense_negative_c) normal = ProfileScale(normal, -1);
        var loop = face.FirstLoop;
        for (var l = 0; l < face.LoopCount; l++, loop = Loops[loop].NextInFace)
        {
            offsets[l] = edgeCount; var firstFin = Loops[loop].FirstFin;
            if (l == 0 && start != 0)
            {
                var found = false; var search = firstFin;
                for (var k = 0; k < Loops[loop].FinCount; k++, search = Fins[search].NextInLoop)
                {
                    var e = Edges[Fins[search].Edge]; var v = Fins[search].Sense == '+' ? e.StartVertex : e.EndVertex;
                    if (v >= 0 && Vertices[v].Header.Tag == start) { firstFin = search; found = true; break; }
                }
                if (!found) return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            var fin = firstFin;
            for (var k = 0; k < Loops[loop].FinCount; k++, fin = Fins[fin].NextInLoop)
            {
                var edge = Fins[fin].Edge;
                if (Edges[edge].StartVertex < 0 || Edges[edge].EndVertex < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                edges[edgeCount] = edge; directions[edgeCount++] = (byte)(Fins[fin].Sense == '+' ? 1 : 0);
            }
        }
        loopCount = face.LoopCount; offsets[loopCount] = edgeCount;
        return edgeCount == input.EdgeCountBody ? 0 : ParasolidConstants.PK_ERROR_bad_loop;
    }

    private static int FreeformReadWireProfile(BodySlot body, EntityTag start, Span<EdgeSlot> edges, Span<byte> directions,
        Span<int> offsets, out BufferCount edgeCount, out BufferCount loopCount, out KernelVector3 normal)
    {
        edgeCount = loopCount = 0; normal = default; ref readonly var source = ref Bodies[body];
        if (source.EdgeCountBody < 3 || source.EdgeCountBody > edges.Length) return ParasolidConstants.PK_ERROR_not_implemented;
        Span<EdgeSlot> original = stackalloc EdgeSlot[source.EdgeCountBody]; Span<byte> used = stackalloc byte[source.EdgeCountBody]; used.Clear();
        var edge = source.FirstEdgeBody;
        for (var i = 0; i < original.Length; i++, edge = Edges[edge].NextInBody) original[i] = edge;
        Span<int> senses = stackalloc int[source.EdgeCountBody];
        while (edgeCount < original.Length)
        {
            if (loopCount + 1 >= offsets.Length) return ParasolidConstants.PK_ERROR_not_implemented;
            var seed = 0; while (used[seed] != 0) seed++;
            var forward = true;
            if (loopCount == 0 && start != 0)
            {
                var found = false;
                for (var i = 0; i < original.Length; i++)
                {
                    var record = Edges[original[i]];
                    if (record.StartVertex >= 0 && Vertices[record.StartVertex].Header.Tag == start) { seed = i; forward = true; found = true; break; }
                    if (record.EndVertex >= 0 && Vertices[record.EndVertex].Header.Tag == start) { seed = i; forward = false; found = true; break; }
                }
                if (!found) return ParasolidConstants.PK_ERROR_bad_parameter;
            }
            offsets[loopCount++] = edgeCount;
            var first = Edges[original[seed]].FirstFinEdge;
            if ((Fins[first].Sense == '+') != forward) first = Fins[first].Other;
            var fin = first;
            do
            {
                if (fin < 0 || Fins[fin].Loop >= 0 || edgeCount >= original.Length) return ParasolidConstants.PK_ERROR_bad_wire;
                var index = original.IndexOf(Fins[fin].Edge);
                if (index < 0 || used[index] != 0 || Edges[original[index]].StartVertex < 0 || Edges[original[index]].EndVertex < 0)
                    return ParasolidConstants.PK_ERROR_bad_wire;
                used[index] = 1; edges[edgeCount] = original[index]; senses[edgeCount++] = Fins[fin].Sense == '+' ? 1 : -1;
                fin = Fins[fin].NextInLoop;
            } while (fin != first);
        }
        offsets[loopCount] = edgeCount;
        var error = ValidateProfileLoops(edges[..edgeCount], senses, offsets[..(loopCount + 1)], out _, out normal, out _);
        if (error != 0) return error;
        for (var i = 0; i < edgeCount; i++) directions[i] = (byte)(senses[i] > 0 ? 1 : 0);
        return 0;
    }

    private static int BodyMakeSweptBodyCore(BodyTag profile, BodyTag path, EntityTag pathStart,
        PK_BODY_make_swept_body_o_s* options, PK_BODY_tracked_sweep_r_s* result)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (options == null || result == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        result->status.fault = ParasolidConstants.PK_BODY_sweep_failure_c;
        if (options->o_t_version != 8) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->alignment is not (ParasolidConstants.PK_BODY_sweep_align_parallel_c or ParasolidConstants.PK_BODY_sweep_align_normal_c)
            || options->twist.law_type != ParasolidConstants.PK_BODY_sweep_law_none_c || options->scale.law_type != ParasolidConstants.PK_BODY_sweep_law_none_c
            || options->simplify != ParasolidConstants.PK_BODY_simplify_no_c || options->topology_form != ParasolidConstants.PK_BODY_topology_minimal_c
            || options->fair != ParasolidConstants.PK_BODY_sweep_fair_default_c
            || options->n_ignorable_vertices != 0 || options->minimise_tolerance != 0
            || !double.IsFinite(options->tolerance) || options->tolerance <= 0) return ParasolidConstants.PK_ERROR_not_implemented;
        if (!TryResolveBodySlot(path, out var pathSlot) || Bodies[pathSlot].BodyType != ParasolidConstants.PK_BODY_type_wire_c)
            return ParasolidConstants.PK_ERROR_bad_class;
        if (!TryResolveBodySlot(profile, out var profileSlot)) return ParasolidConstants.PK_ERROR_bad_class;
        if (Bodies[profileSlot].BodyType != ParasolidConstants.PK_BODY_type_sheet_c || Bodies[profileSlot].FaceCountBody != 1)
            return ParasolidConstants.PK_ERROR_not_implemented;
        if (Bodies[pathSlot].EdgeCountBody != 1) return ParasolidConstants.PK_ERROR_not_implemented;
        if (!FreeformHasRequiredBoundaryGeometry(pathSlot)) return ParasolidConstants.PK_ERROR_not_implemented;
        ref readonly var pathEdge = ref Edges[Bodies[pathSlot].FirstEdgeBody];
        if (pathEdge.StartVertex < 0 || pathEdge.EndVertex < 0 || !TryProfileEdgeRange(in pathEdge, out var a, out var b))
            return ParasolidConstants.PK_ERROR_not_implemented;
        var reverse = pathStart != 0 && pathStart == Vertices[pathEdge.EndVertex].Header.Tag;
        if (pathStart != 0 && !reverse && pathStart != Vertices[pathEdge.StartVertex].Header.Tag) return ParasolidConstants.PK_ERROR_bad_parameter;
        var startVertex = reverse ? pathEdge.EndVertex : pathEdge.StartVertex;
        var endVertex = reverse ? pathEdge.StartVertex : pathEdge.EndVertex;
        if (options->alignment == ParasolidConstants.PK_BODY_sweep_align_normal_c && Curves[GetCurveSlotByTag(pathEdge.CurveTag)].Class != CurveClass.Line)
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<EdgeSlot> inputEdges = stackalloc EdgeSlot[128]; Span<byte> senses = stackalloc byte[128];
        Span<int> offsets = stackalloc int[65];
        var error = FreeformReadProfile(profile, 0, inputEdges, senses, offsets, out var count, out var loops, out var normal);
        if (error != 0) return error;
        reverse ^= Curves[GetCurveSlotByTag(pathEdge.CurveTag)].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c;
        error = FreeformBoundedCurve(pathEdge.CurveTag, a, b, reverse, out var pathCurve); if (error != 0) return error;
        ref readonly var pathData = ref BCurveDataStore[Curves[GetCurveSlotByTag(pathCurve)].DataIndex];
        var pp = pathData.PolesSpan(); var pd = pathData.VertexDim;
        var startWeight = pathData.IsRational != 0 ? pp[3] : 1;
        var origin = Vector(pp[0] / startWeight, pp[1] / startWeight, pp[2] / startWeight);
        var endIndex = (pathData.NVertices - 1) * pd; var endWeight = pathData.IsRational != 0 ? pp[endIndex + 3] : 1;
        var end = Vector(pp[endIndex] / endWeight, pp[endIndex + 1] / endWeight, pp[endIndex + 2] / endWeight);
        var translation = ProfileSubtract(end, origin); var signedHeight = ProfileDot(translation, normal);
        if (!ProfileFinite(translation) || !double.IsFinite(signedHeight)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var directionSign = Math.Sign(signedHeight);
        if (directionSign == 0) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->alignment == ParasolidConstants.PK_BODY_sweep_align_normal_c)
        {
            var scale = Math.Max(Math.Abs(translation.X), Math.Max(Math.Abs(translation.Y), Math.Abs(translation.Z)));
            var unitScale = Vector(translation.X / scale, translation.Y / scale, translation.Z / scale);
            var cross = ProfileCross(unitScale, normal);
            if (ProfileDot(cross, cross) > 1e-24 * ProfileDot(unitScale, unitScale)) return ParasolidConstants.PK_ERROR_not_implemented;
        }
        var previous = ProfileDot(origin, normal);
        if (!double.IsFinite(previous)) return ParasolidConstants.PK_ERROR_bad_parameter;
        for (var p = 1; p < pathData.NVertices; p++)
        {
            var w = pathData.IsRational != 0 ? pp[p * pd + 3] : 1;
            var current = (pp[p * pd] * normal.X + pp[p * pd + 1] * normal.Y + pp[p * pd + 2] * normal.Z) / w;
            // Positive rational basis and strictly ordered projected poles certify a one-to-one sweep height.
            if (!double.IsFinite(current) || (current - previous) * directionSign <= 1e-12) return ParasolidConstants.PK_ERROR_not_implemented;
            previous = current;
        }
        Span<SurfTag> sides = stackalloc SurfTag[count];
        for (var e = 0; e < count; e++)
        {
            ref readonly var edge = ref Edges[inputEdges[e]];
            if (!TryProfileEdgeRange(in edge, out a, out b)) return ParasolidConstants.PK_ERROR_not_implemented;
            error = FreeformBoundedCurve(edge.CurveTag, a, b, (senses[e] == 0) ^ (Curves[GetCurveSlotByTag(edge.CurveTag)].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c), out var section); if (error != 0) return error;
            error = FreeformTranslationSurface(section, pathCurve, origin, out sides[e]);
            var cleanup = EntityDeleteImplementation(1, &section); if (error == 0) error = cleanup; if (error != 0) return error;
            if (options->allow_rationals == 0 && BSurfaceDataStore[Surfaces[GetSurfaceSlotByTag(sides[e])].DataIndex].IsRational != 0)
                return ParasolidConstants.PK_ERROR_not_implemented;
        }
        error = EntityDeleteImplementation(1, &pathCurve); if (error != 0) return error;
        var transform = KernelAffineTransform.Translation(in translation);
        error = AssembleFreeformSolid(profile, profile, inputEdges[..count], senses[..count], inputEdges[..count], senses[..count],
            offsets[..(loops + 1)], sides, transform, directionSign, out var product);
        if (error != 0) return error;
        error = FreeformTolerantBoundaries(product, count, offsets[..(loops + 1)], options->tolerance); if (error != 0) return error;
        error = CreateFreeformSweepTracking(profile, inputEdges[..count], Bodies[pathSlot].FirstEdgeBody, startVertex, endVertex, product, &result->tracking_info);
        if (error != 0) return error;
        result->body = product; result->status.fault = ParasolidConstants.PK_BODY_sweep_ok_c; return 0;
    }

    private static int FreeformTranslationSurface(CurveTag section, CurveTag path, in KernelVector3 origin, out SurfTag surface)
    {
        surface = 0;
        ref readonly var c = ref BCurveDataStore[Curves[GetCurveSlotByTag(section)].DataIndex];
        ref readonly var p = ref BCurveDataStore[Curves[GetCurveSlotByTag(path)].DataIndex];
        var rational = c.IsRational != 0 || p.IsRational != 0; var dim = rational ? 4 : 3;
        if ((long)c.NVertices * p.NVertices > 8192) return ParasolidConstants.PK_ERROR_not_implemented;
        Span<double> poles = stackalloc double[c.NVertices * p.NVertices * dim];
        var cp = c.PolesSpan(); var pp = p.PolesSpan();
        for (var i = 0; i < c.NVertices; i++)
            for (var j = 0; j < p.NVertices; j++)
            {
                var cw = c.IsRational != 0 ? cp[i * c.VertexDim + 3] : 1;
                var pw = p.IsRational != 0 ? pp[j * p.VertexDim + 3] : 1;
                var offset = (i * p.NVertices + j) * dim;
                poles[offset] = cp[i * c.VertexDim] * pw + pp[j * p.VertexDim] * cw - origin.X * cw * pw;
                poles[offset + 1] = cp[i * c.VertexDim + 1] * pw + pp[j * p.VertexDim + 1] * cw - origin.Y * cw * pw;
                poles[offset + 2] = cp[i * c.VertexDim + 2] * pw + pp[j * p.VertexDim + 2] * cw - origin.Z * cw * pw;
                if (rational) poles[offset + 3] = cw * pw;
            }
        SurfTag output = 0;
        var error = CreateFreeformSurface(c.Degree, p.Degree, c.NVertices, p.NVertices, poles, dim, rational,
            c.KnotsSpan(), c.KnotMultsSpan(), p.KnotsSpan(), p.KnotMultsSpan(), c.IsClosed != 0, false, &output);
        surface = output; return error;
    }

    private static int BodyMakeLoftedBodyCore(BufferCount count, BodyTag* profiles, EntityTag* starts,
        PK_BODY_make_lofted_body_o_s* options, PK_BODY_tracked_loft_s* result)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (options == null || result == null || profiles == null || count < 2 || count > FreeformSectionLimit) return ParasolidConstants.PK_ERROR_bad_parameter;
        result->status.fault = ParasolidConstants.PK_BODY_loft_failure_c;
        if (options->o_t_version != 16) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->end_conditions.periodic != ParasolidConstants.PK_PARAM_periodic_no_c
            || options->end_conditions.start.type != ParasolidConstants.PK_BODY_loft_clamp_no_c || options->end_conditions.end.type != ParasolidConstants.PK_BODY_loft_clamp_no_c
            || count > 2 && (options->end_conditions.start.curvature_condition != ParasolidConstants.PK_BODY_loft_natural_c
                || options->end_conditions.end.curvature_condition != ParasolidConstants.PK_BODY_loft_natural_c)
            || options->simplify != ParasolidConstants.PK_BODY_simplify_no_c || options->topology_form != ParasolidConstants.PK_BODY_topology_minimal_c
            || options->matches.n_mappings != 0 || options->guide_vertex_matches.n_match_sets != 0
            || options->n_guide_wires != 0 || options->n_guide_derivs != 0 || options->n_intermediate_derivs != 0
            || options->want_edge_tracking != 0 || options->create_construction_topol != 0 || options->label_profiles != 0
            || options->minimise_tolerance != 0 || !double.IsFinite(options->tolerance) || options->tolerance <= 0)
            return ParasolidConstants.PK_ERROR_not_implemented;
        if (!TryResolveBodySlot(profiles[0], out var firstSlot) || !TryResolveBodySlot(profiles[count - 1], out var lastSlot)
            || Bodies[firstSlot].BodyType != ParasolidConstants.PK_BODY_type_sheet_c || Bodies[lastSlot].BodyType != ParasolidConstants.PK_BODY_type_sheet_c)
            return ParasolidConstants.PK_ERROR_not_implemented;
        for (var end = 0; end < 2; end++)
        {
            var index = end == 0 ? 0 : count - 1; var slot = end == 0 ? firstSlot : lastSlot;
            if (Bodies[slot].FaceCountBody != 1 || Faces[Bodies[slot].FirstFaceBody].LoopCount != 1)
            {
                result->status.fault = ParasolidConstants.PK_BODY_loft_bad_profile_type_c;
                result->status.fault_topol = profiles[index]; return 0;
            }
        }
        for (var p = 1; p + 1 < count; p++)
        {
            if (!TryResolveBodySlot(profiles[p], out var middle)) return ParasolidConstants.PK_ERROR_bad_class;
            if (Bodies[middle].BodyType != ParasolidConstants.PK_BODY_type_wire_c)
            {
                result->status.fault = ParasolidConstants.PK_BODY_loft_bad_profile_type_c;
                result->status.fault_topol = profiles[p];
                return 0;
            }
        }
        Span<EdgeSlot> allEdges = stackalloc EdgeSlot[count * 128]; Span<byte> allSenses = stackalloc byte[count * 128];
        Span<int> offsets = stackalloc int[65]; Span<int> otherOffsets = stackalloc int[65];
        var error = FreeformReadProfile(profiles[0], starts == null ? 0 : starts[0], allEdges[..128], allSenses[..128], offsets,
            out var edgeCount, out var loopCount, out var normal); if (error != 0) return error;
        for (var p = 1; p < count; p++)
        {
            error = FreeformReadProfile(profiles[p], starts == null ? 0 : starts[p], allEdges.Slice(p * 128, 128), allSenses.Slice(p * 128, 128),
                otherOffsets, out var ec, out var lc, out var n);
            if (error != 0) return error;
            if (ec != edgeCount || lc != loopCount || !otherOffsets[..(lc + 1)].SequenceEqual(offsets[..(loopCount + 1)])) return ParasolidConstants.PK_ERROR_not_implemented;
            if (ProfileDot(normal, n) < 1 - 1e-12) return ParasolidConstants.PK_ERROR_not_implemented;
        }
        Span<CurveTag> curves = stackalloc CurveTag[count]; Span<SurfTag> sides = stackalloc SurfTag[edgeCount];
        Span<double> scales = stackalloc double[count]; Span<KernelVector3> translations = stackalloc KernelVector3[count];
        Span<double> parameters = stackalloc double[count]; Span<double> controls = stackalloc double[3 * (count - 1) + 1];
        Span<double> diagonal = stackalloc double[count]; Span<double> seconds = stackalloc double[count];
        for (var p = 0; p < count; p++) parameters[p] = (double)p / (count - 1);
        Span<double> heights = stackalloc double[count];
        for (var p = 0; p < count; p++)
        {
            heights[p] = ProfileDot(FreeformEdgeStart(allEdges[p * 128], allSenses[p * 128]), normal);
            if (!double.IsFinite(heights[p])) return ParasolidConstants.PK_ERROR_bad_parameter;
        }
        var heightDelta = heights[^1] - heights[0]; if (!double.IsFinite(heightDelta)) return ParasolidConstants.PK_ERROR_bad_parameter;
        var direction = Math.Sign(heightDelta); if (direction == 0) return ParasolidConstants.PK_ERROR_not_implemented;
        if (!FreeformSplineMath.Interpolate(parameters, heights, 1, default, default, controls, diagonal, seconds)) return ParasolidConstants.PK_ERROR_bad_parameter;
        for (var p = 1; p < controls.Length; p++)
            if (!double.IsFinite(controls[p]) || (controls[p] - controls[p - 1]) * direction <= 1e-12) return ParasolidConstants.PK_ERROR_not_implemented;
        error = FreeformCertifyPolygonLoft(allEdges, allSenses, offsets[..(loopCount + 1)], parameters, normal, direction, out var polygonCertified);
        if (error != 0) return error;
        if (!polygonCertified)
        {
            var baseA = FreeformEdgeStart(allEdges[0], allSenses[0]); var baseB = FreeformEdgeStart(allEdges[1], allSenses[1]);
            var delta = ProfileSubtract(baseB, baseA); var lengthSquared = ProfileDot(delta, delta);
            if (lengthSquared < 1e-20) return ParasolidConstants.PK_ERROR_bad_parameter;
            for (var p = 0; p < count; p++)
            {
                var pa = FreeformEdgeStart(allEdges[p * 128], allSenses[p * 128]);
                var pb = FreeformEdgeStart(allEdges[p * 128 + 1], allSenses[p * 128 + 1]);
                scales[p] = ProfileDot(ProfileSubtract(pb, pa), delta) / lengthSquared;
                if (!(scales[p] > 1e-10)) return ParasolidConstants.PK_ERROR_not_implemented;
                translations[p] = ProfileSubtract(pa, ProfileScale(baseA, scales[p]));
            }
            if (!FreeformSplineMath.Interpolate(parameters, scales, 1, default, default, controls, diagonal, seconds)) return ParasolidConstants.PK_ERROR_bad_parameter;
            for (var p = 0; p < controls.Length; p++) if (!(controls[p] > 1e-10)) return ParasolidConstants.PK_ERROR_not_implemented;
        }
        var loft = FreeformNaturalLoftOptions();
        for (var e = 0; e < edgeCount; e++)
        {
            for (var p = 0; p < count; p++)
            {
                ref readonly var edge = ref Edges[allEdges[p * 128 + e]];
                if (!TryProfileEdgeRange(in edge, out var a, out var b)) return ParasolidConstants.PK_ERROR_not_implemented;
                error = polygonCertified
                    ? FreeformPolygonSectionCurve(allEdges[p * 128 + e], allSenses[p * 128 + e], out curves[p])
                    : FreeformBoundedCurve(edge.CurveTag, a, b, (allSenses[p * 128 + e] == 0) ^ (Curves[GetCurveSlotByTag(edge.CurveTag)].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c), out curves[p]);
                if (error != 0) return error;
            }
            if (!polygonCertified)
            {
                // Curved boundaries use a second sufficient certificate: homothetic
                // profiles, a positive interpolated scale and monotone plane height.
                error = FreeformCertifySectionFamily(curves, scales, translations); if (error != 0) return error;
            }
            SurfTag support = 0;
            fixed (CurveTag* ptr = curves) error = BCurveMakeBSurfLoftedCore(count, ptr, &loft, &support);
            if (error != 0) return error; sides[e] = support;
            fixed (CurveTag* ptr = curves) error = EntityDeleteImplementation(count, ptr);
            if (error != 0) return error;
        }
        error = AssembleFreeformSolid(profiles[0], profiles[count - 1], allEdges[..edgeCount], allSenses[..edgeCount],
            allEdges.Slice((count - 1) * 128, edgeCount), allSenses.Slice((count - 1) * 128, edgeCount), offsets[..(loopCount + 1)], sides,
            KernelAffineTransform.Identity, direction, out var product);
        if (error != 0) return error;
        error = FreeformTolerantBoundaries(product, edgeCount, offsets[..(loopCount + 1)], options->tolerance); if (error != 0) return error;
        error = CreateFreeformLoftTracking(new ReadOnlySpan<BodyTag>(profiles, count), allEdges, edgeCount, product, &result->tracking_info);
        if (error != 0) return error;
        result->body = product; result->status.fault = ParasolidConstants.PK_BODY_loft_ok_c; return 0;
    }

    private static PK_BCURVE_make_bsurf_lofted_o_s FreeformNaturalLoftOptions() => new()
    {
        o_t_version = 1, v_end = ParasolidConstants.PK_PARAM_end_natural_c, V_end = ParasolidConstants.PK_PARAM_end_natural_c,
        u_v = ParasolidConstants.PK_PARAM_twist_no_c, u_V = ParasolidConstants.PK_PARAM_twist_no_c,
        U_v = ParasolidConstants.PK_PARAM_twist_no_c, U_V = ParasolidConstants.PK_PARAM_twist_no_c,
        v_degen = ParasolidConstants.PK_PARAM_degen_no_c, V_degen = ParasolidConstants.PK_PARAM_degen_no_c,
        how_knot = ParasolidConstants.PK_PARAM_knot_auto_c, amalgamate = 1,
    };
    private static KernelVector3 FreeformEdgeStart(EdgeSlot edge, byte forward)
        => Points[GetPointSlotByTag(Vertices[forward != 0 ? Edges[edge].StartVertex : Edges[edge].EndVertex].PointTag)].Position;

    private static bool FreeformHasRequiredBoundaryGeometry(BodySlot body)
    {
        ref readonly var source = ref Bodies[body];
        var vertex = source.FirstVertexBody;
        for (var i = 0; i < source.VertexCountBody; i++, vertex = Vertices[vertex].NextInBody)
        {
            if (vertex < 0) return false;
            var point = GetPointSlotByTag(Vertices[vertex].PointTag);
            if (point < 0 || !ProfileFinite(Points[point].Position)) return false;
        }
        var edge = source.FirstEdgeBody;
        for (var i = 0; i < source.EdgeCountBody; i++, edge = Edges[edge].NextInBody)
        {
            // These construction/sewing routes operate on explicit spatial curves.
            // Tolerant fin-only boundaries remain valid topology, but require a
            // separate conversion route before they can be used as a profile.
            if (edge < 0 || GetCurveSlotByTag(Edges[edge].CurveTag) < 0) return false;
        }
        var face = source.FirstFaceBody;
        for (var i = 0; i < source.FaceCountBody; i++, face = Faces[face].NextInBody)
            if (face < 0 || GetSurfaceSlotByTag(Faces[face].SurfTag) < 0) return false;
        return true;
    }

    private static int FreeformCertifySectionFamily(ReadOnlySpan<CurveTag> curves, ReadOnlySpan<double> scales, ReadOnlySpan<KernelVector3> translations)
    {
        Span<double> breaks = stackalloc double[FreeformPoleLimit];
        int error, n, degree; bool rational;
        fixed (CurveTag* ptr = curves) error = FreeformMatchingLayout(curves.Length, ptr, breaks, out n, out degree, out rational);
        if (error != 0) return error;
        var count = (n - 1) * degree + 1;
        Span<double> reference = stackalloc double[count * 4]; Span<double> current = stackalloc double[count * 4];
        if (!FreeformMatchPoles(curves[0], breaks[..n], degree, true, reference)) return ParasolidConstants.PK_ERROR_bad_knots;
        for (var p = 1; p < curves.Length; p++)
        {
            if (!FreeformMatchPoles(curves[p], breaks[..n], degree, true, current)) return ParasolidConstants.PK_ERROR_bad_knots;
            for (var k = 0; k < count; k++)
            {
                var weight = reference[4 * k + 3];
                if (Math.Abs(current[4 * k + 3] - weight) > 2e-13 * weight) return ParasolidConstants.PK_ERROR_not_implemented;
                var expected = ProfileAdd(ProfileScale(Vector(reference[4 * k] / weight, reference[4 * k + 1] / weight, reference[4 * k + 2] / weight), scales[p]), translations[p]);
                var actual = Vector(current[4 * k] / weight, current[4 * k + 1] / weight, current[4 * k + 2] / weight);
                if (ProfileDistance(in expected, in actual) > 2e-9) return ParasolidConstants.PK_ERROR_not_implemented;
            }
        }
        return 0;
    }

    private static int AssembleFreeformSolid(BodyTag firstProfile, BodyTag lastProfile, ReadOnlySpan<EdgeSlot> firstEdges,
        ReadOnlySpan<byte> firstDirections, ReadOnlySpan<EdgeSlot> lastEdges, ReadOnlySpan<byte> lastDirections,
        ReadOnlySpan<int> offsets, ReadOnlySpan<SurfTag> sides, KernelAffineTransform lastTransform, KernelSense direction,
        out EntityTag result)
    {
        result = 0; var count = firstEdges.Length; var loopCount = offsets.Length - 1;
        Span<KernelVector3> points = stackalloc KernelVector3[2 * count];
        Span<ModelingEdge> edges = stackalloc ModelingEdge[3 * count];
        Span<ModelingCoedge> coedges = stackalloc ModelingCoedge[6 * count];
        Span<ModelingLoop> loops = stackalloc ModelingLoop[count + 2 * loopCount];
        Span<ModelingFace> faces = stackalloc ModelingFace[count + 2];
        Span<int> following = stackalloc int[count];
        for (var l = 0; l < loopCount; l++)
            for (var e = offsets[l]; e < offsets[l + 1]; e++) following[e] = e + 1 == offsets[l + 1] ? offsets[l] : e + 1;
        for (var e = 0; e < count; e++)
        {
            points[e] = FreeformEdgeStart(firstEdges[e], firstDirections[e]);
            var top = FreeformEdgeStart(lastEdges[e], lastDirections[e]); points[count + e] = lastTransform.TransformPoint(in top);
            for (var side = 0; side < 2; side++)
            {
                ref readonly var input = ref Edges[side == 0 ? firstEdges[e] : lastEdges[e]];
                var forward = (side == 0 ? firstDirections[e] : lastDirections[e]) != 0;
                if (!TryProfileEdgeRange(in input, out var a, out var b)) return ParasolidConstants.PK_ERROR_not_implemented;
                var error = TryCloneEntityCore(input.CurveTag, out var curve); if (error != 0) return error;
                if (side != 0) { error = TryTransformEntityCore(curve, in lastTransform); if (error != 0) return error; }
                edges[side * count + e] = new ModelingEdge { Curve = curve, Start = a, End = b,
                    StartVertex = side * count + (forward ? e : following[e]), EndVertex = side * count + (forward ? following[e] : e) };
            }
            CurveTag rail = 0; var iso = BSurfMakeBCurveIsoparamCore(sides[e], 0, true, &rail); if (iso != 0) return iso;
            edges[2 * count + e] = new ModelingEdge { Curve = rail, Start = 0, End = 1, StartVertex = e, EndVertex = count + e };
            faces[e] = new ModelingFace { Surface = sides[e], Sense = ParasolidConstants.PK_TOPOL_sense_positive_c, FirstLoop = e, LoopCount = 1 };
            loops[e] = new ModelingLoop { FirstCoedge = 4 * e, CoedgeCount = 4 };
            coedges[4 * e] = new ModelingCoedge { Edge = e, Forward = firstDirections[e] };
            coedges[4 * e + 1] = new ModelingCoedge { Edge = 2 * count + following[e], Forward = 1 };
            coedges[4 * e + 2] = new ModelingCoedge { Edge = count + e, Forward = (byte)(lastDirections[e] == 0 ? 1 : 0) };
            coedges[4 * e + 3] = new ModelingCoedge { Edge = 2 * count + e, Forward = 0 };
        }
        for (var e = 0; e < count; e++)
        {
            CurveTag endRail = 0; var error = BSurfMakeBCurveIsoparamCore(sides[e], 1, true, &endRail); if (error != 0) return error;
            var agree = FreeformSameCurve(edges[2 * count + following[e]].Curve, 0, 1, endRail, 0, 1, false, 2e-9);
            error = EntityDeleteImplementation(1, &endRail); if (error != 0) return error;
            if (!agree) return ParasolidConstants.PK_ERROR_inconsistent_geom;
        }
        var fin = 4 * count;
        for (var side = 0; side < 2; side++)
        {
            var source = Faces[Bodies[TagRec(side == 0 ? firstProfile : lastProfile).Slot].FirstFaceBody];
            var error = TryCloneEntityCore(source.SurfTag, out var support); if (error != 0) return error;
            if (side != 0) { error = TryTransformEntityCore(support, in lastTransform); if (error != 0) return error; }
            faces[count + side] = new ModelingFace { Surface = support, FirstLoop = count + side * loopCount, LoopCount = loopCount,
                Sense = side == 0 ? source.Orientation == ParasolidConstants.PK_TOPOL_sense_positive_c ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c : source.Orientation };
            for (var l = 0; l < loopCount; l++)
            {
                var size = offsets[l + 1] - offsets[l]; loops[count + side * loopCount + l] = new ModelingLoop { FirstCoedge = fin, CoedgeCount = size };
                for (var i = 0; i < size; i++)
                {
                    var e = side == 0 ? offsets[l + 1] - i - 1 : offsets[l] + i;
                    coedges[fin++] = new ModelingCoedge { Edge = side * count + e, Forward = side == 0 ? (byte)(firstDirections[e] == 0 ? 1 : 0) : lastDirections[e] };
                }
            }
        }
        if (direction < 0)
        {
            for (var f = 0; f < faces.Length; f++) faces[f].Sense = faces[f].Sense == ParasolidConstants.PK_TOPOL_sense_positive_c ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c;
            for (var l = 0; l < loops.Length; l++)
            {
                var list = coedges.Slice(loops[l].FirstCoedge, loops[l].CoedgeCount); list.Reverse();
                for (var k = 0; k < list.Length; k++) list[k].Forward = (byte)(list[k].Forward == 0 ? 1 : 0);
            }
        }
        return BuildModelingBrep(points, edges, coedges, loops, faces, true, out result);
    }
}
