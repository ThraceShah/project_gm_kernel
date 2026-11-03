using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodySewBodies(BufferCount count, BodyTag* bodies, double gap, PK_BODY_sew_bodies_o_s* options,
        BufferCount* sewnCount, BodyTag** sewn, BufferCount* unsewnCount, BodyTag** unsewn,
        BufferCount* problemCount, PK_BODY_problem_group_s** problems)
    {
        if (sewnCount != null) *sewnCount = 0; if (sewn != null) *sewn = null;
        if (unsewnCount != null) *unsewnCount = 0; if (unsewn != null) *unsewn = null;
        if (problemCount != null) *problemCount = 0; if (problems != null) *problems = null;
        if (Dispatcher.IsExecuting) return BodySewBodiesCore(count, bodies, gap, options, sewnCount, sewn, unsewnCount, unsewn, problemCount, problems);
        var command = new SewingCommand { Count = count, Bodies = bodies, Gap = gap, Options = options,
            SewnCount = sewnCount, Sewn = sewn, UnsewnCount = unsewnCount, Unsewn = unsewn, ProblemCount = problemCount, Problems = problems };
        return Dispatch(ApiId.BodySewBodies, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, count > 0 && bodies != null ? bodies[0] : 0);
    }
    private struct SewingCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal BodyTag* Bodies;
        internal double Gap;
        internal PK_BODY_sew_bodies_o_s* Options;
        internal BufferCount* SewnCount;
        internal BodyTag** Sewn;
        internal BufferCount* UnsewnCount;
        internal BodyTag** Unsewn;
        internal BufferCount* ProblemCount;
        internal PK_BODY_problem_group_s** Problems;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return BodySewBodiesCore(Count, Bodies, Gap, Options, SewnCount, Sewn, UnsewnCount, Unsewn, ProblemCount, Problems); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }
    private struct SewingSourceEdge
    {
        internal EdgeSlot Slot;
        internal BufferOffset Body, Start, End, Match;
        internal KernelSense BoundarySense;
        internal byte Reverse;
    }

    private static int BodySewBodiesCore(BufferCount count, BodyTag* bodies, double gap, PK_BODY_sew_bodies_o_s* options,
        BufferCount* sewnCount, BodyTag** sewn, BufferCount* unsewnCount, BodyTag** unsewn,
        BufferCount* problemCount, PK_BODY_problem_group_s** problems)
    {
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (count < 1 || count > 64 || bodies == null || options == null || sewnCount == null || sewn == null
            || unsewnCount == null || unsewn == null || problemCount == null || problems == null || !double.IsFinite(gap) || gap < 0)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 8) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->set_global_tolerance != 0 || options->treat_as_manifold == 0
            || options->duplicate_removal != ParasolidConstants.PK_BODY_sewing_remove_none_c
            || options->number_of_iterations != 1 || options->iteration_bounds != null
            || options->assembly_sewing != ParasolidConstants.PK_BODY_sewing_assy_no_c
            || options->general_sewing != ParasolidConstants.PK_BODY_sewing_gen_no_c
            || options->reduce_edge_tolerance != ParasolidConstants.PK_EDGE_reduce_tol_no_c
            || options->prefered_body_type is not (ParasolidConstants.PK_BODY_sewing_any_c or ParasolidConstants.PK_BODY_sewing_solid_c or ParasolidConstants.PK_BODY_sewing_sheet_c))
            return ParasolidConstants.PK_ERROR_not_implemented;
        Span<BodySlot> slots = stackalloc BodySlot[count];
        var edgeCount = 0; var vertexCount = 0; var faceCount = 0; var loopCount = 0; var finCount = 0;
        for (var i = 0; i < count; i++)
        {
            if (!TryResolveBodySlot(bodies[i], out slots[i])) return ParasolidConstants.PK_ERROR_bad_class;
            for (var j = 0; j < i; j++) if (slots[i] == slots[j]) return ParasolidConstants.PK_ERROR_bad_parameter;
            ref readonly var input = ref Bodies[slots[i]];
            if (input.Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
            if (input.BodyType != ParasolidConstants.PK_BODY_type_sheet_c) return ParasolidConstants.PK_ERROR_not_implemented;
            if (!FreeformHasRequiredBoundaryGeometry(slots[i])) return ParasolidConstants.PK_ERROR_not_implemented;
            edgeCount += input.EdgeCountBody; vertexCount += input.VertexCountBody; faceCount += input.FaceCountBody;
            var face = input.FirstFaceBody;
            for (var f = 0; f < input.FaceCountBody; f++, face = Faces[face].NextInBody)
            {
                loopCount += Faces[face].LoopCount; var loop = Faces[face].FirstLoop;
                for (var l = 0; l < Faces[face].LoopCount; l++, loop = Loops[loop].NextInFace) finCount += Loops[loop].FinCount;
            }
        }
        if (edgeCount > 2048 || vertexCount > 2048 || faceCount > 1024 || finCount > 8192) return ParasolidConstants.PK_ERROR_not_implemented;
        Span<SewingSourceEdge> sourceEdges = stackalloc SewingSourceEdge[edgeCount];
        Span<VertexSlot> sourceVertices = stackalloc VertexSlot[vertexCount];
        Span<KernelVector3> sourcePoints = stackalloc KernelVector3[vertexCount];
        Span<int> parents = stackalloc int[vertexCount];
        Span<int> bodySigns = stackalloc int[count]; bodySigns.Clear();
        Span<int> components = stackalloc int[count]; components.Fill(-1);
        var ev = 0; var vv = 0;
        for (var b = 0; b < count; b++)
        {
            ref readonly var input = ref Bodies[slots[b]];
            var vertex = input.FirstVertexBody;
            for (var v = 0; v < input.VertexCountBody; v++, vertex = Vertices[vertex].NextInBody)
            {
                sourceVertices[vv] = vertex; sourcePoints[vv] = Points[GetPointSlotByTag(Vertices[vertex].PointTag)].Position; parents[vv] = vv; vv++;
            }
            var edge = input.FirstEdgeBody;
            for (var e = 0; e < input.EdgeCountBody; e++, edge = Edges[edge].NextInBody)
            {
                ref readonly var record = ref Edges[edge]; var faceFins = 0; var sense = 0; var fin = record.FirstFinEdge;
                for (var k = 0; k < record.FinCount; k++, fin = Fins[fin].NextOfEdge)
                    if (Fins[fin].Loop >= 0) { faceFins++; sense = Fins[fin].Sense == '+' ? 1 : -1; }
                if (faceFins is < 1 or > 2) return ParasolidConstants.PK_ERROR_non_manifold;
                sourceEdges[ev] = new SewingSourceEdge { Slot = edge, Body = b,
                    Start = record.StartVertex < 0 ? -1 : sourceVertices[..vv].IndexOf(record.StartVertex),
                    End = record.EndVertex < 0 ? -1 : sourceVertices[..vv].IndexOf(record.EndVertex),
                    Match = -1, BoundarySense = faceFins == 1 ? sense : 0 };
                ev++;
            }
        }
        // Accurate sewing: coincident complete boundaries. Larger gaps require fin pcurves and are not silently collapsed.
        var matchTolerance = Math.Min(Math.Max(gap, 1e-10), 1e-9);
        for (var i = 0; i < edgeCount; i++)
        {
            if (sourceEdges[i].BoundarySense == 0) continue;
            ref readonly var a = ref Edges[sourceEdges[i].Slot];
            if (!TryProfileEdgeRange(in a, out var a0, out var a1)) return ParasolidConstants.PK_ERROR_not_implemented;
            for (var j = i + 1; j < edgeCount; j++)
            {
                if (sourceEdges[j].BoundarySense == 0 || sourceEdges[i].Body == sourceEdges[j].Body) continue;
                ref readonly var b = ref Edges[sourceEdges[j].Slot];
                if (!TryProfileEdgeRange(in b, out var b0, out var b1)) return ParasolidConstants.PK_ERROR_not_implemented;
                if (sourceEdges[i].Start < 0 || sourceEdges[j].Start < 0) continue;
                var reverse = ProfileDistance(in sourcePoints[sourceEdges[i].Start], in sourcePoints[sourceEdges[j].End]) <= matchTolerance
                    && ProfileDistance(in sourcePoints[sourceEdges[i].End], in sourcePoints[sourceEdges[j].Start]) <= matchTolerance;
                var forward = ProfileDistance(in sourcePoints[sourceEdges[i].Start], in sourcePoints[sourceEdges[j].Start]) <= matchTolerance
                    && ProfileDistance(in sourcePoints[sourceEdges[i].End], in sourcePoints[sourceEdges[j].End]) <= matchTolerance;
                if (!reverse && !forward) continue;
                var curveReverse = reverse ^ (Curves[GetCurveSlotByTag(a.CurveTag)].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c)
                    ^ (Curves[GetCurveSlotByTag(b.CurveTag)].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c);
                if (!FreeformSameCurve(a.CurveTag, a0, a1, b.CurveTag, b0, b1, curveReverse, matchTolerance)) continue;
                if (sourceEdges[i].Match >= 0 || sourceEdges[j].Match >= 0) return ParasolidConstants.PK_ERROR_non_manifold;
                sourceEdges[i].Match = j; sourceEdges[j].Match = i;
                sourceEdges[i].Reverse = sourceEdges[j].Reverse = (byte)(reverse ? 1 : 0);
                SewingUnion(parents, sourceEdges[i].Start, reverse ? sourceEdges[j].End : sourceEdges[j].Start);
                SewingUnion(parents, sourceEdges[i].End, reverse ? sourceEdges[j].Start : sourceEdges[j].End);
            }
        }
        var componentCount = 0;
        Span<int> retentionOrder = stackalloc int[count]; retentionOrder.Fill(int.MaxValue);
        Span<int> queue = stackalloc int[count];
        for (var root = 0; root < count; root++)
        {
            if (components[root] >= 0) continue;
            components[root] = componentCount; bodySigns[root] = 1; retentionOrder[root] = 0;
            var head = 0; var tail = 1; queue[0] = root;
            while (head < tail)
            {
                var current = queue[head++];
                for (var e = 0; e < edgeCount; e++)
                {
                    var other = sourceEdges[e].Match; if (other < 0) continue;
                    var from = sourceEdges[e].Body; var to = sourceEdges[other].Body;
                    if (from != current) continue;
                    var sign = -bodySigns[from] * sourceEdges[e].BoundarySense * sourceEdges[other].BoundarySense * (sourceEdges[e].Reverse != 0 ? -1 : 1);
                    if (components[to] < 0)
                    {
                        components[to] = componentCount; bodySigns[to] = sign;
                        // Grow from the retained target in its boundary order. Geometry already in
                        // the growing component owns a merged edge, including ties at equal depth.
                        retentionOrder[to] = tail; queue[tail++] = to;
                    }
                    else if (components[to] != componentCount || bodySigns[to] != sign) return ParasolidConstants.PK_ERROR_inconsistent_senses;
                }
            }
            componentCount++;
        }
        Span<EntityTag> products = stackalloc EntityTag[count]; var productCount = 0;
        Span<EntityTag> omitted = stackalloc EntityTag[count]; var omittedCount = 0;
        for (var component = 0; component < componentCount; component++)
        {
            if (component != components[0] && options->allow_disjoint_result == 0)
            {
                for (var b = 0; b < count; b++) if (components[b] == component) omitted[omittedCount++] = bodies[b];
                continue;
            }
            var members = 0; var target = -1;
            for (var b = 0; b < count; b++) if (components[b] == component) { members++; if (target < 0) target = b; }
            if (members == 1)
            {
                if (target == 0) products[productCount++] = bodies[target]; else omitted[omittedCount++] = bodies[target];
                continue;
            }
            var error = BuildSewnComponent(component, slots, components, bodySigns, retentionOrder, sourcePoints, parents, sourceEdges,
                faceCount, loopCount, finCount, options->prefered_body_type != ParasolidConstants.PK_BODY_sewing_sheet_c, out var product);
            if (error != 0) return error;
            error = ReplaceProfileBody(slots[target], TagRec(product).Slot); if (error != 0) return error;
            products[productCount++] = bodies[target];
            for (var b = 0; b < count; b++) if (components[b] == component && b != target)
            { var consumed = bodies[b]; error = EntityDeleteImplementation(1, &consumed); if (error != 0) return error; }
        }
        BodyTag* resultBodies = null; BodyTag* remaining = null;
        if (productCount > 0)
        {
            resultBodies = (BodyTag*)State.Session->Returns.TryAllocate((nuint)(productCount * sizeof(BodyTag)));
            if (resultBodies == null) return ParasolidConstants.PK_ERROR_memory_full;
            products[..productCount].CopyTo(new Span<EntityTag>(resultBodies, productCount));
        }
        if (omittedCount > 0)
        {
            remaining = (BodyTag*)State.Session->Returns.TryAllocate((nuint)(omittedCount * sizeof(BodyTag)));
            if (remaining == null) { if (resultBodies != null) MemoryFree(resultBodies); return ParasolidConstants.PK_ERROR_memory_full; }
            omitted[..omittedCount].CopyTo(new Span<EntityTag>(remaining, omittedCount));
        }
        *sewnCount = productCount; *sewn = resultBodies; *unsewnCount = omittedCount; *unsewn = remaining; return 0;
    }

    private static int BuildSewnComponent(BufferOffset component, ReadOnlySpan<BodySlot> slots, ReadOnlySpan<int> components,
        ReadOnlySpan<int> signs, ReadOnlySpan<int> retentionOrder, ReadOnlySpan<KernelVector3> sourcePoints, Span<int> parents,
        ReadOnlySpan<SewingSourceEdge> sourceEdges, BufferCount faceCapacity, BufferCount loopCapacity, BufferCount finCapacity,
        bool preferSolid, out EntityTag result)
    {
        result = 0;
        Span<KernelVector3> points = stackalloc KernelVector3[sourcePoints.Length];
        Span<int> vertexMap = stackalloc int[sourcePoints.Length]; vertexMap.Fill(-1);
        Span<ModelingEdge> edges = stackalloc ModelingEdge[sourceEdges.Length];
        Span<int> edgeMap = stackalloc int[sourceEdges.Length]; edgeMap.Fill(-1);
        Span<int> edgeOwners = stackalloc int[sourceEdges.Length];
        for (var i = 0; i < sourceEdges.Length; i++)
        {
            var other = sourceEdges[i].Match; var a = sourceEdges[i].Body;
            var b = other < 0 ? a : sourceEdges[other].Body;
            edgeOwners[i] = other < 0 || retentionOrder[a] < retentionOrder[b] ? i : other;
        }
        Span<ModelingFace> faces = stackalloc ModelingFace[faceCapacity];
        Span<ModelingLoop> loops = stackalloc ModelingLoop[loopCapacity];
        Span<ModelingCoedge> coedges = stackalloc ModelingCoedge[finCapacity];
        var np = 0; var ne = 0; var nf = 0; var nl = 0; var nc = 0; var closed = true;
        for (var i = 0; i < sourceEdges.Length; i++)
        {
            ref readonly var source = ref sourceEdges[i]; if (components[source.Body] != component) continue;
            if (edgeOwners[i] != i) continue;
            closed &= source.BoundarySense == 0 || source.Match >= 0;
            var from = -1; var to = -1;
            if (source.Start >= 0)
            {
                var a = SewingRoot(parents, source.Start); var b = SewingRoot(parents, source.End);
                if (vertexMap[a] < 0) { vertexMap[a] = np; points[np++] = sourcePoints[a]; }
                if (vertexMap[b] < 0) { vertexMap[b] = np; points[np++] = sourcePoints[b]; }
                from = vertexMap[a]; to = vertexMap[b];
            }
            ref readonly var input = ref Edges[source.Slot];
            if (!TryProfileEdgeRange(in input, out var first, out var last)) return ParasolidConstants.PK_ERROR_not_implemented;
            var error = TryCloneEntityCore(input.CurveTag, out var curve); if (error != 0) return error;
            edgeMap[i] = ne; if (source.Match >= 0) edgeMap[source.Match] = ne;
            edges[ne++] = new ModelingEdge { Curve = curve, StartVertex = from, EndVertex = to, Start = first, End = last };
        }
        for (var b = 0; b < slots.Length; b++)
        {
            if (components[b] != component) continue;
            var face = Bodies[slots[b]].FirstFaceBody;
            for (var f = 0; f < Bodies[slots[b]].FaceCountBody; f++, face = Faces[face].NextInBody)
            {
                ref readonly var input = ref Faces[face]; var error = TryCloneEntityCore(input.SurfTag, out var support); if (error != 0) return error;
                var positive = signs[b] > 0;
                faces[nf++] = new ModelingFace { Surface = support,
                    Sense = positive ? input.Orientation : input.Orientation == ParasolidConstants.PK_TOPOL_sense_positive_c ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c,
                    FirstLoop = nl, LoopCount = input.LoopCount };
                var loop = input.FirstLoop;
                for (var l = 0; l < input.LoopCount; l++, loop = Loops[loop].NextInFace)
                {
                    loops[nl++] = new ModelingLoop { FirstCoedge = nc, CoedgeCount = Loops[loop].FinCount };
                    var fin = positive ? Loops[loop].FirstFin : Loops[loop].LastFin;
                    for (var k = 0; k < Loops[loop].FinCount; k++, fin = positive ? Fins[fin].NextInLoop : Fins[fin].PrevInLoop)
                    {
                        var index = -1; for (var e = 0; e < sourceEdges.Length; e++) if (sourceEdges[e].Slot == Fins[fin].Edge) { index = e; break; }
                        if (index < 0 || edgeMap[index] < 0) return ParasolidConstants.PK_ERROR_bad_loop;
                        var forward = (Fins[fin].Sense == '+') == positive;
                        if (edgeOwners[index] != index && sourceEdges[index].Reverse != 0) forward = !forward;
                        coedges[nc++] = new ModelingCoedge { Edge = edgeMap[index], Forward = (byte)(forward ? 1 : 0) };
                    }
                }
            }
        }
        if (closed && preferSolid)
        {
            double volume = 0;
            for (var l = 0; l < nl; l++)
            {
                var loop = loops[l]; var first = coedges[loop.FirstCoedge];
                var aIndex = first.Forward != 0 ? edges[first.Edge].StartVertex : edges[first.Edge].EndVertex;
                if (aIndex < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                var a = ProfileSubtract(points[aIndex], points[0]);
                for (var i = 1; i + 1 < loop.CoedgeCount; i++)
                {
                    var b = coedges[loop.FirstCoedge + i]; var c = coedges[loop.FirstCoedge + i + 1];
                    var bi = b.Forward != 0 ? edges[b.Edge].StartVertex : edges[b.Edge].EndVertex;
                    var ci = c.Forward != 0 ? edges[c.Edge].StartVertex : edges[c.Edge].EndVertex;
                    volume += ProfileDot(a, ProfileCross(ProfileSubtract(points[bi], points[0]), ProfileSubtract(points[ci], points[0])));
                }
            }
            if (!double.IsFinite(volume) || Math.Abs(volume) < 1e-18) return ParasolidConstants.PK_ERROR_not_implemented;
            if (volume < 0)
            {
                for (var f = 0; f < nf; f++) faces[f].Sense = faces[f].Sense == ParasolidConstants.PK_TOPOL_sense_positive_c ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c;
                for (var l = 0; l < nl; l++)
                {
                    var list = coedges.Slice(loops[l].FirstCoedge, loops[l].CoedgeCount); list.Reverse();
                    for (var i = 0; i < list.Length; i++) list[i].Forward = (byte)(list[i].Forward == 0 ? 1 : 0);
                }
            }
        }
        return BuildModelingBrep(points[..np], edges[..ne], coedges[..nc], loops[..nl], faces[..nf], closed && preferSolid, out result);
    }
    private static int SewingRoot(Span<int> parents, BufferOffset index)
    {
        while (parents[index] != index) { parents[index] = parents[parents[index]]; index = parents[index]; }
        return index;
    }
    private static void SewingUnion(Span<int> parents, BufferOffset a, BufferOffset b)
    { a = SewingRoot(parents, a); b = SewingRoot(parents, b); if (a != b) parents[b] = a; }
}
