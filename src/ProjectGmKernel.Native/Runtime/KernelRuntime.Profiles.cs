using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // A bound on one command's workspace, not a restriction on a body's shape.
    private const int ProfileEdgeLimit = 512;

    private struct ProfileSegment
    {
        internal CurveTag Curve;
        internal EdgeSlot Edge;
        internal BufferOffset Start, End, Component, Source;
        internal double T0, T1;
    }

    public static int CurveMakeWireBody(CurveTag curve, PK_INTERVAL_s bounds, EntityTag* body)
    {
        var options = new PK_CURVE_make_wire_body_o_s
        {
            o_t_version = 3, tolerance = 1e-6, allow_disjoint = 1,
            check = 1, sequential = ParasolidConstants.PK_CURVE_sequential_no_c,
        };
        int count = 0;
        return CurveMakeWireBody2(1, &curve, &bounds, &options, body, &count, null, null);
    }

    public static int CurveMakeWireBody2(int count, CurveTag* curves, PK_INTERVAL_s* bounds,
        PK_CURVE_make_wire_body_o_s* options, EntityTag* body, int* edgeCount,
        EntityTag** newEdges, int** indices)
    {
        if(body!=null)*body=0;if(edgeCount!=null)*edgeCount=0;
        if(newEdges!=null)*newEdges=null;if(indices!=null)*indices=null;
        if (Dispatcher.IsExecuting)
            return CurveMakeWireBodyCore(count, curves, bounds, options, body, edgeCount, newEdges, indices);
        var command = new ProfileWireCommand { Count = count, Curves = curves, Bounds = bounds,
            Options = options, Body = body, EdgeCount = edgeCount, Edges = newEdges, Indices = indices };
        return Dispatch(ApiId.CurveMakeWireBody2, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command);
    }

    private struct ProfileWireCommand : IKernelCommand
    {
        internal BufferCount Count;
        internal CurveTag* Curves;
        internal PK_INTERVAL_s* Bounds;
        internal PK_CURVE_make_wire_body_o_s* Options;
        internal EntityTag* Body;
        internal BufferCount* EdgeCount;
        internal EntityTag** Edges;
        internal BufferOffset** Indices;
        public int Execute()
        {
            var owns=EnsureCommandScratch();
            try
            {
                var error=CurveMakeWireBodyCore(Count, Curves, Bounds, Options, Body, EdgeCount, Edges, Indices);
                if(error!=0)
                {
                    if(Body!=null)*Body=0;if(EdgeCount!=null)*EdgeCount=0;
                    if(Edges!=null)*Edges=null;if(Indices!=null)*Indices=null;
                }
                return error;
            }
            finally{if(owns)ReleaseCommandScratch(State.Session);}
        }
    }

    private static int CurveMakeWireBodyCore(int count, CurveTag* curves, PK_INTERVAL_s* bounds,
        PK_CURVE_make_wire_body_o_s* options, EntityTag* bodyTag, int* edgeCount,
        EntityTag** newEdges, int** indices)
    {
        if (bodyTag != null) *bodyTag = 0;
        if (edgeCount != null) *edgeCount = 0;
        if (newEdges != null) *newEdges = null;
        if (indices != null) *indices = null;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (count <= 0 || count > ProfileEdgeLimit || curves == null || bounds == null
            || options == null || bodyTag == null || edgeCount == null)
            return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 3) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (!double.IsFinite(options->tolerance) || options->tolerance <= 0
            || options->allow_disjoint > 1 || options->allow_general > 1 || options->check > 1
            || options->want_edges > 1 || options->want_indices > 1
            || options->want_edges != 0 && newEdges == null || options->want_indices != 0 && indices == null
            || options->sequential != ParasolidConstants.PK_CURVE_sequential_no_c
                && options->sequential != ParasolidConstants.PK_CURVE_sequential_yes_c)
            return ParasolidConstants.PK_ERROR_bad_parameter;

        Span<ProfileSegment> segments = stackalloc ProfileSegment[ProfileEdgeLimit];
        Span<KernelVector3> points = stackalloc KernelVector3[2 * ProfileEdgeLimit];
        Span<int> degrees = stackalloc int[2 * ProfileEdgeLimit];
        Span<double> pointTolerances = stackalloc double[2 * ProfileEdgeLimit];
        Span<double> breaks=stackalloc double[ProfileEdgeLimit+1];
        degrees.Clear();
        pointTolerances.Clear();
        var pointCount = 0;var segmentCount=0;
        Span<KernelVector3> jet = stackalloc KernelVector3[2];
        for (var i = 0; i < count; i++)
        {
            var slot = GetCurveSlotByTag(curves[i]);
            if (slot < 0) return ParasolidConstants.PK_ERROR_bad_class;
            ref readonly var curve = ref Curves[slot];
            if (curve.Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
            if (curve.Class is not (CurveClass.Line or CurveClass.Circle or CurveClass.Ellipse or CurveClass.BCurve or CurveClass.ICurve))
                return ParasolidConstants.PK_ERROR_not_implemented;
            var a = bounds[i].value[0];
            var b = bounds[i].value[1];
            if (!double.IsFinite(a) || !double.IsFinite(b) || !(b > a)
                || curve.Class is CurveClass.Circle or CurveClass.Ellipse && b - a > Math.Tau + 1e-12
                || curve.Class is CurveClass.BCurve or CurveClass.ICurve && (a < curve.TMin || b > curve.TMax))
                return ParasolidConstants.PK_ERROR_bad_parameter;
            var splitError=ProfileCurveBreaks(in curve,a,b,breaks,out var breakCount);
            if(splitError!=0)return splitError;
            if(segmentCount>segments.Length-(breakCount-1))return ParasolidConstants.PK_ERROR_not_implemented;
            for(var part=0;part+1<breakCount;part++)
            {
                var first=breaks[part];var last=breaks[part+1];
                if (EvaluateCurveCore(in curve, first, 0, false, jet, out _) != AlgorithmStatus.Success)
                    return ParasolidConstants.PK_ERROR_eval_failure;
                var p0 = jet[0];
                if (EvaluateCurveCore(in curve, last, 0, false, jet, out _) != AlgorithmStatus.Success)
                    return ParasolidConstants.PK_ERROR_eval_failure;
                var p1 = jet[0];
                var v0 = MatchProfilePoint(points, ref pointCount, in p0, options->tolerance);
                var v1 = MatchProfilePoint(points, ref pointCount, in p1, options->tolerance);
                pointTolerances[v0]=Math.Max(pointTolerances[v0],ProfileDistance(in points[v0],in p0));
                pointTolerances[v1]=Math.Max(pointTolerances[v1],ProfileDistance(in points[v1],in p1));
                if(v0==v1&&curve.Class is CurveClass.Circle or CurveClass.Ellipse&&Math.Abs((last-first)-Math.Tau)>1e-12)
                    return ParasolidConstants.PK_ERROR_bad_parameter;
                if (++degrees[v0] > 2 || ++degrees[v1] > 2)
                    return ParasolidConstants.PK_ERROR_bad_wire;
                segments[segmentCount++] = new ProfileSegment { Curve = curves[i], T0 = first, T1 = last,
                    Start = v0, End = v1, Component = -1, Edge = -1,Source=i };
            }
        }
        var componentCount = 0;
        for (var seed = 0; seed < segmentCount; seed++)
        {
            if (segments[seed].Component >= 0) continue;
            segments[seed].Component = componentCount;
            bool changed;
            do
            {
                changed = false;
                for (var i = 0; i < segmentCount; i++)
                    if (segments[i].Component < 0)
                        for (var j = 0; j < segmentCount; j++)
                            if (segments[j].Component == componentCount && ProfileSegmentsTouch(in segments[i], in segments[j]))
                            { segments[i].Component = componentCount; changed = true; break; }
            } while (changed);
            componentCount++;
        }
        if (componentCount > 1 && options->allow_disjoint == 0) return ParasolidConstants.PK_ERROR_disjoint;

        var bodySlot = TryAllocateBodies();
        if (bodySlot < 0) return ParasolidConstants.PK_ERROR_memory_full;
        InitializeBody(ref Bodies[bodySlot]);
        AssignPartition(ref Bodies[bodySlot].Header, CurrentPartition);
        Bodies[bodySlot].BodyType = ParasolidConstants.PK_BODY_type_wire_c;
        var region = AddProfileVoidRegion(bodySlot);
        if (region < 0) return ParasolidConstants.PK_ERROR_memory_full;
        Span<int> shells = stackalloc int[componentCount];
        for (var i = 0; i < componentCount; i++)
        {
            shells[i] = AddProfileShell(bodySlot, region);
            if (shells[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;
        }
        Span<int> vertices = stackalloc int[pointCount];
        vertices.Fill(-1);
        for (var i = 0; i < segmentCount; i++)
        {
            ref var segment = ref segments[i];
            var periodicRing = segment.Start == segment.End
                && (Curves[GetCurveSlotByTag(segment.Curve)].Class is CurveClass.Circle or CurveClass.Ellipse
                    || ProfileClosedICurve(segment.Curve, segment.T0, segment.T1));
            if (!periodicRing)
                for (var end = 0; end < 2; end++)
                {
                    var index = end == 0 ? segment.Start : segment.End;
                    if (vertices[index] < 0 && !AddVertex(bodySlot, points[index].X, points[index].Y, points[index].Z, out vertices[index]))
                        return ParasolidConstants.PK_ERROR_memory_full;
                    Vertices[vertices[index]].Tolerance=pointTolerances[index]>1e-14?pointTolerances[index]:0;
                }
            // PK clips a B-spline support to the actual wire interval. Keeping
            // unrelated C0 knots outside that interval makes a valid edge fail
            // native G1 checks after XT import, despite correct end positions.
            var error = CloneProfileBoundedCurve(segment.Curve,segment.T0,segment.T1,out var clone);
            if (error != 0) return error;
            var start=segment.Start;var endVertex=segment.End;
            ref readonly var carrier=ref Curves[GetCurveSlotByTag(clone)];
            // The finite limits remain in native order; an oriented LINE
            // returned by SSI can traverse them in reverse.
            if(carrier.Class==CurveClass.Line&&LocalSenseSign(carrier.Sense)<0)(start,endVertex)=(endVertex,start);
            var edge = AddProfileEdge(bodySlot, clone, periodicRing ? -1 : vertices[start],
                periodicRing ? -1 : vertices[endVertex], segment.T0, segment.T1);
            if (edge < 0) return ParasolidConstants.PK_ERROR_memory_full;
            segment.Edge = edge;
            Edges[edge].WireShellTag = Shells[shells[segment.Component]].Header.Tag;
            if (AddProfileFin(-1, edge, true) < 0 || AddProfileFin(-1, edge, false) < 0)
                return ParasolidConstants.PK_ERROR_memory_full;
        }
        LinkProfileWireFins(bodySlot);
        var finish = FinishCreatedBody(bodySlot, bodyTag);
        if (finish != 0) return finish;
        if (!PublishProfileDummyFins(bodySlot)) return ParasolidConstants.PK_ERROR_memory_full;
        if (options->want_edges != 0)
        {
            var values = AllocateReturnSlice(segmentCount);
            if (values == null) return ParasolidConstants.PK_ERROR_memory_full;
            for (var i = 0; i < segmentCount; i++) values[i] = Edges[segments[i].Edge].Header.Tag;
            *newEdges = values;
        }
        if (options->want_indices != 0)
        {
            var values = AllocateReturnSlice(segmentCount);
            if (values == null) return ParasolidConstants.PK_ERROR_memory_full;
            for (var i = 0; i < segmentCount; i++) values[i] = segments[i].Source;
            *indices = values;
        }
        *edgeCount = segmentCount;
        return 0;
    }

    private static bool ProfileSegmentsTouch(in ProfileSegment a, in ProfileSegment b)
        => a.Start == b.Start || a.Start == b.End || a.End == b.Start || a.End == b.End;

    private static bool ProfileClosedICurve(CurveTag tag, double first, double last)
    {
        var record = Curves[GetCurveSlotByTag(tag)];
        if (record.Class != CurveClass.ICurve || first != record.TMin || last != record.TMax) return false;
        var data = ICurveDataPool[record.DataIndex];
        return data.StartLimit.Type == LimitType.Help && data.EndLimit.Type == LimitType.Help;
    }

    private static int MatchProfilePoint(Span<KernelVector3> points, ref int count, in KernelVector3 point, double tolerance)
    {
        for (var i = 0; i < count; i++)
            if (ProfileDistance(in points[i], in point) <= tolerance) return i;
        points[count] = point;
        return count++;
    }

    private static double ProfileDistance(in KernelVector3 a, in KernelVector3 b)
        => Math.Sqrt((a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y) + (a.Z-b.Z)*(a.Z-b.Z));

    private static RegionSlot AddProfileVoidRegion(BodySlot body)
    {
        var slot = TryAllocateRegions();
        if (slot < 0) return -1;
        ref var region = ref Regions[slot];
        region.IsSolid = 0; region.FirstShell = region.LastShell = region.Frame = -1;
        AppendRegionToBody(body, slot);
        return slot;
    }

    private static ShellSlot AddProfileShell(BodySlot body, RegionSlot region)
    {
        var slot = TryAllocateShells();
        if (slot < 0) return -1;
        InitializeShell(ref Shells[slot], body);
        AppendShellToBody(body, slot);
        AppendShellToRegion(region, slot);
        Shells[slot].Header.Tag = AllocateTag(EntityClass.Shell, PoolKind.Shell, slot, Shells[slot].Header.Generation);
        return Shells[slot].Header.Tag <= 0 ? -1 : slot;
    }

    private static EdgeSlot AddProfileEdge(BodySlot body, CurveTag curve, VertexSlot start, VertexSlot end, double t0, double t1)
    {
        var slot = TryAllocateEdges();
        if (slot < 0) return -1;
        ref var edge = ref Edges[slot];
        edge.Body = body; edge.StartVertex = start; edge.EndVertex = end;
        edge.CurveTag = curve; edge.FirstFinEdge = edge.LastFinEdge = -1;
        edge.ParameterStart = t0; edge.ParameterEnd = t1; edge.HasParameterRange = 1;
        AppendEdgeToBody(body, slot);
        return slot;
    }

    private static FinSlot AddProfileFin(LoopSlot loop, EdgeSlot edge, bool forward)
    {
        var slot = TryAllocateFins();
        if (slot < 0) return -1;
        ref var fin = ref Fins[slot];
        fin.Edge = edge; fin.Loop = loop; fin.Curve = -1; fin.Other = -1;
        fin.NextInLoop = fin.PrevInLoop = fin.NextAtVertex = fin.PrevAtVertex = -1;
        fin.NextOfEdge = fin.PrevOfEdge = -1;
        fin.Vertex = forward ? Edges[edge].EndVertex : Edges[edge].StartVertex;
        fin.Sense = forward ? '+' : '-';
        if (loop >= 0) AppendFinToLoop(loop, slot);
        AppendFinToEdge(edge, slot);
        if (fin.Vertex >= 0) AppendFinToVertex(fin.Vertex, slot);
        fin.Sense = forward ? '+' : '-';
        return slot;
    }

    private static bool PublishProfileDummyFins(BodySlot body)
    {
        var edge = Bodies[body].FirstEdgeBody;
        for (var i = 0; i < Bodies[body].EdgeCountBody; i++, edge = Edges[edge].NextInBody)
        {
            var fin = Edges[edge].FirstFinEdge;
            for (var j = 0; j < Edges[edge].FinCount; j++, fin = Fins[fin].NextOfEdge)
            {
                if (Fins[fin].Header.Tag == 0)
                    Fins[fin].Header.Tag = AllocateTag(EntityClass.Fin, PoolKind.Fin, fin, Fins[fin].Header.Generation);
                if (Fins[fin].Header.Tag <= 0) return false;
            }
        }
        return true;
    }

    private static void LinkProfileWireFins(BodySlot body)
    {
        var edge = Bodies[body].FirstEdgeBody;
        for (var i = 0; i < Bodies[body].EdgeCountBody; i++, edge = Edges[edge].NextInBody)
        {
            var fin = Edges[edge].FirstFinEdge;
            for (var j = 0; j < Edges[edge].FinCount; j++, fin = Fins[fin].NextOfEdge)
            {
                var vertex = Fins[fin].Vertex;
                if(vertex<0){Fins[fin].NextInLoop=Fins[fin].PrevInLoop=-1;continue;}
                var next = fin;
                if (vertex >= 0)
                {
                    var otherAtVertex = Fins[fin].NextAtVertex;
                    next = otherAtVertex >= 0 && otherAtVertex != fin
                        ? Fins[otherAtVertex].Other : Fins[fin].Other;
                }
                Fins[fin].NextInLoop = next;
                Fins[next].PrevInLoop = fin;
            }
        }
    }
}
