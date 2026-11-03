using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyImprintPlane(EntityTag body, SurfTag plane, double tolerance, int* count, EntityTag** edges)
    {
        if (Dispatcher.IsExecuting) return BodyImprintPlaneCore(body, plane, tolerance, count, edges);
        var command = new ImprintPlaneCommand { Body = body, Plane = plane, Tolerance = tolerance, Count = count, Edges = edges };
        var error = Dispatch(ApiId.BodyImprintPlane, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, body);
        if (error != 0)
        {
            if (count != null) *count = 0;
            if (edges != null) *edges = null;
        }
        return error;
    }

    public static int BodyImprintPlane2(EntityTag body, SurfTag plane, PK_BODY_imprint_plane_o_s* options, PK_TOPOL_track_r_s* tracking)
    {
        if (Dispatcher.IsExecuting) return BodyImprintPlaneOptionsCore(body, plane, options, tracking);
        var command = new ImprintPlaneCommand { Body = body, Plane = plane, Options = options, Tracking = tracking, Modern = 1 };
        var error = Dispatch(ApiId.BodyImprintPlane2, ConcurrencyKind.Local, AccessKind.GlobalWrite, ref command, body);
        if (error != 0 && tracking != null) *tracking = default;
        return error;
    }

    private struct ImprintPlaneCommand : IKernelCommand
    {
        internal EntityTag Body;
        internal SurfTag Plane;
        internal double Tolerance;
        internal BufferCount* Count;
        internal EntityTag** Edges;
        internal PK_BODY_imprint_plane_o_s* Options;
        internal PK_TOPOL_track_r_s* Tracking;
        internal byte Modern;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return Modern != 0 ? BodyImprintPlaneOptionsCore(Body, Plane, Options, Tracking)
                : BodyImprintPlaneCore(Body, Plane, Tolerance, Count, Edges); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int BodyImprintPlaneOptionsCore(EntityTag body, SurfTag plane, PK_BODY_imprint_plane_o_s* options, PK_TOPOL_track_r_s* tracking)
    {
        if (tracking != null) *tracking = default;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (options == null || tracking == null) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->o_t_version != 1) return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        if (options->have_tolerance > 1) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (options->imprint_dir != ParasolidConstants.PK_imprint_dir_no_check_c) return ParasolidConstants.PK_ERROR_not_implemented;
        int count = 0; EntityTag* edges = null;
        var error = BodyImprintPlaneCore(body, plane, options->have_tolerance != 0 ? options->tolerance : 1e-8, &count, &edges);
        if (error != 0) return error;
        if (count == 0) return ParasolidConstants.PK_ERROR_partial_no_intersect;
        var record = (PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(
            (nuint)(sizeof(PK_TOPOL_track_record_r_s) + count * sizeof(EntityTag)));
        if (record == null) return ParasolidConstants.PK_ERROR_memory_full;
        var products = (EntityTag*)(record + 1);
        new ReadOnlySpan<EntityTag>(edges, count).CopyTo(new Span<EntityTag>(products, count));
        *record = new PK_TOPOL_track_record_r_s { track = ParasolidConstants.PK_TOPOL_track_create_c,
            n_product_topols = count, product_topols = products };
        *tracking = new PK_TOPOL_track_r_s { n_track_records = 1, track_records = record, internal_prods = products };
        return MemoryFree(edges);
    }

    private static int BodyImprintPlaneCore(EntityTag body, SurfTag plane, double tolerance, int* count,
        EntityTag** edges)
    {
        if (count != null) *count = 0; if (edges != null) *edges = null;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        if (count == null || edges == null || !double.IsFinite(tolerance) || tolerance <= 0) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (!TryResolveBodySlot(body, out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
        if (!State.Session->Tags.TryResolve(plane, CurrentSessionId, out var sr) || sr.Pool != (byte)PoolKind.Surface
            || Surfaces[sr.Slot].Class != SurfaceClass.Plane) return ParasolidConstants.PK_ERROR_bad_class;
        if (Bodies[slot].Header.Partition != CurrentPartition || Surfaces[sr.Slot].Header.Partition != CurrentPartition)
            return ParasolidConstants.PK_ERROR_not_in_same_partition;
        var solid = Bodies[slot].BodyType == ParasolidConstants.PK_BODY_type_solid_c;
        if (!solid && Bodies[slot].BodyType != ParasolidConstants.PK_BODY_type_sheet_c) return ParasolidConstants.PK_ERROR_not_implemented;
        var scratch = CommandScratch.Current; var memory = scratch.Take(120000);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var faces = ModelSpan<PlanarModelFace>(bytes, ref cursor, PlanarFaceCapacity);
            var loops = ModelSpan<PlanarModelLoop>(bytes, ref cursor, PlanarPolygonCapacity);
            var points = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var cells = ModelSpan<PlanarModelPolygon>(bytes, ref cursor, PlanarPolygonCapacity);
            var cellPoints = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var cellWork = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            var output = ModelSpan<PlanarModelPolygon>(bytes, ref cursor, PlanarPolygonCapacity);
            var outputPoints = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            if (cursor > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            Span<EntityTag> input = stackalloc EntityTag[1]; input[0] = body;
            var error = ReadPlanarModel(input, faces, loops, points, out var nf, out var nl, out var np, out var epsilon);
            if (error != 0) return error;
            faces = faces[..nf]; loops = loops[..nl]; points = points[..np];
            ref readonly var data = ref PlaneDataPool[Surfaces[sr.Slot].DataIndex];
            var origin = Vector(data.LocationX, data.LocationY, data.LocationZ); var normal = Vector(data.NormalX, data.NormalY, data.NormalZ);
            Span<PlanarModelFace> cutter = stackalloc PlanarModelFace[1];
            cutter[0] = new PlanarModelFace { Source = -1, Origin = origin, Normal = normal };
            var nOut = 0; var nPoints = 0; var splits = false;
            for (var f = 0; f < nf; f++)
            {
                error = ArrangePlanarFace(in faces[f], cutter, loops, points, cells, cellPoints, cellWork, epsilon, out var nc, out _);
                if (error != 0) return error;
                var haveFront = false; var haveBack = false;
                for (var c = 0; c < nc; c++)
                {
                    var cell = cells[c]; var center = PlanarPolygonCenter(cellPoints.Slice(cell.First, cell.Count));
                    var d = ProfileDot(ProfileSubtract(center, origin), normal);
                    haveFront |= d > epsilon; haveBack |= d < -epsilon;
                    if (nOut == output.Length || nPoints > outputPoints.Length - cell.Count) return ParasolidConstants.PK_ERROR_not_implemented;
                    cellPoints.Slice(cell.First, cell.Count).CopyTo(outputPoints[nPoints..]);
                    output[nOut++] = new PlanarModelPolygon { First = nPoints, Count = cell.Count, Face = f, Classification = d >= 0 ? 1u : 2u };
                    nPoints += cell.Count;
                }
                splits |= haveFront && haveBack;
            }
            if (!splits) return ReturnPlanarImprintEdges(slot, origin, normal, epsilon, count, edges);
            Span<EntityTag> products = stackalloc EntityTag[PlanarFaceCapacity];
            Span<FaceTag> faceProducts = stackalloc FaceTag[nf]; faceProducts.Clear();
            error = ReconstructPlanarBodies(faces, output[..nOut], outputPoints[..nPoints], epsilon, solid, false, products, out var nb, faceProducts);
            if (error != 0) return error;
            if (nb != 1) return ParasolidConstants.PK_ERROR_not_implemented;
            var resultSlot = TagRec(products[0]).Slot;
            error = PreservePlanarFaceHandles(faces, faceProducts, input, products[..1]);
            if (error == 0) error = ReplaceProfileBody(slot, resultSlot);
            if (error != 0) return error;
            return ReturnPlanarImprintEdges(slot, origin, normal, epsilon, count, edges);
        }
        finally { scratch.Return(memory); }
    }

    // The legacy API returns every resulting edge lying in the cutting plane,
    // including pre-existing edges and a repeated imprint's original result.
    // Modern tracking reports the complete product list in one create record.
    private static int ReturnPlanarImprintEdges(BodySlot body, in KernelVector3 origin, in KernelVector3 normal,
        double tolerance, int* count, EntityTag** edges)
    {
        Span<EntityTag> selected = stackalloc EntityTag[Bodies[body].EdgeCountBody]; var n = 0;
        var edge = Bodies[body].FirstEdgeBody;
        for (var i = 0; i < Bodies[body].EdgeCountBody; i++, edge = Edges[edge].NextInBody)
        {
            ref readonly var e = ref Edges[edge];
            if (e.StartVertex < 0 || e.EndVertex < 0) continue;
            var a = Points[GetPointSlotByTag(Vertices[e.StartVertex].PointTag)].Position;
            var b = Points[GetPointSlotByTag(Vertices[e.EndVertex].PointTag)].Position;
            if (Math.Abs(ProfileDot(ProfileSubtract(a, origin), normal)) <= tolerance
                && Math.Abs(ProfileDot(ProfileSubtract(b, origin), normal)) <= tolerance) selected[n++] = e.Header.Tag;
        }
        var returned = n == 0 ? null : AllocateReturnSlice(n);
        if (n > 0 && returned == null) return ParasolidConstants.PK_ERROR_memory_full;
        if (n > 0) selected[..n].CopyTo(new Span<EntityTag>(returned, n));
        *count = n; *edges = returned; return 0;
    }

}
