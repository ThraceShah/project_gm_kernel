using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    public static int BodyContainsVector(EntityTag body, PK_VECTOR_s vector, int* enclosure, EntityTag* topology)
    {
        if (Dispatcher.IsExecuting) return BodyContainsVectorCore(body, vector, enclosure, topology);
        var command = new ContainsVectorCommand { Body = body, Point = vector, Enclosure = enclosure, Topology = topology };
        var error = Dispatch(ApiId.BodyContainsVector, ConcurrencyKind.Concurrent, AccessKind.ReadOnly, ref command, body);
        if (error != 0)
        {
            if (enclosure != null) *enclosure = ParasolidConstants.PK_enclosure_unset_c;
            if (topology != null) *topology = 0;
        }
        return error;
    }

    private struct ContainsVectorCommand : IKernelCommand
    {
        internal EntityTag Body;
        internal PK_VECTOR_s Point;
        internal KernelClassCode* Enclosure;
        internal EntityTag* Topology;
        public int Execute()
        {
            var owns = EnsureCommandScratch();
            try { return BodyContainsVectorCore(Body, Point, Enclosure, Topology); }
            finally { if (owns) ReleaseCommandScratch(State.Session); }
        }
    }

    private static int BodyContainsVectorCore(EntityTag body, PK_VECTOR_s vector, int* enclosure, EntityTag* topology)
    {
        if (enclosure != null) *enclosure = ParasolidConstants.PK_enclosure_unset_c;
        if (topology != null) *topology = 0;
        if (!IsSessionStarted) return ParasolidConstants.PK_ERROR_not_in_PK;
        var point = Vector(vector.coord[0], vector.coord[1], vector.coord[2]);
        if (enclosure == null || topology == null || !ProfileFinite(in point)) return ParasolidConstants.PK_ERROR_bad_parameter;
        if (!TryResolveBodySlot(body, out var slot)) return ParasolidConstants.PK_ERROR_bad_class;
        var scratch = CommandScratch.Current; var memory = scratch.Take(40000);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var faces = ModelSpan<PlanarModelFace>(bytes, ref cursor, PlanarFaceCapacity);
            var loops = ModelSpan<PlanarModelLoop>(bytes, ref cursor, PlanarPolygonCapacity);
            var points = ModelSpan<KernelVector3>(bytes, ref cursor, PlanarPointCapacity);
            if (cursor > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            Span<EntityTag> input = stackalloc EntityTag[1]; input[0] = body;
            var error = ReadPlanarModel(input, faces, loops, points, out var nf, out var nl, out var np, out var epsilon);
            if (error != 0)
            {
                if (error == ParasolidConstants.PK_ERROR_not_implemented)
                {
                    var curved = TryClassifySphereBoolean(slot, in point, enclosure, topology, out var handled);
                    if (handled) return curved;
                }
                return error;
            }
            faces = faces[..nf]; loops = loops[..nl]; points = points[..np];
            var boundary = LocatePlanarBoundary(faces, loops, points, 0, point, epsilon, out _);
            if (boundary != 0) { *enclosure = ParasolidConstants.PK_enclosure_on_c; *topology = boundary; return 0; }
            var classification = Bodies[slot].BodyType == ParasolidConstants.PK_BODY_type_sheet_c ? -1
                : PlanarClassifyPoint(faces, loops, points, 0, point, epsilon, out _);
            if (classification is 0 or 2) return ParasolidConstants.PK_ERROR_eval_failure;
            *enclosure = classification > 0 ? ParasolidConstants.PK_enclosure_inside_c : ParasolidConstants.PK_enclosure_outside_c;
            var region = Bodies[slot].FirstRegion;
            for (var i = 0; i < Bodies[slot].RegionCount; i++, region = Regions[region].NextInBody)
            {
                if ((Regions[region].IsSolid != 0) != (classification > 0)) continue;
                if (*topology == 0) *topology = Regions[region].Header.Tag;
                if (classification > 0) break;
                if (PlanarClassifyPoint(faces, loops, points, 0, point, epsilon, out _, region) == 1)
                { *topology = Regions[region].Header.Tag; break; }
            }
            return 0;
        }
        finally { scratch.Return(memory); }
    }

    private static EntityTag LocatePlanarBoundary(ReadOnlySpan<PlanarModelFace> faces, ReadOnlySpan<PlanarModelLoop> loops,
        ReadOnlySpan<KernelVector3> points, int body, in KernelVector3 point, double tolerance, out KernelClassCode kind)
    {
        kind = 0; EntityTag faceTag = 0; EntityTag edgeTag = 0;
        for (var f = 0; f < faces.Length; f++)
        {
            ref readonly var face = ref faces[f];
            if (face.Body != body || face.Source < 0
                || Math.Abs(ProfileDot(ProfileSubtract(point, face.Origin), face.Normal)) > tolerance) continue;
            var actualLoop = Faces[face.Source].FirstLoop;
            for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++, actualLoop = Loops[actualLoop].NextInFace)
            {
                var loop = loops[l]; var fin = Loops[actualLoop].FirstFin;
                for (var k = 0; k < loop.Count; k++, fin = Fins[fin].NextInLoop)
                {
                    var a = points[loop.First + k]; var b = points[loop.First + (k + 1) % loop.Count];
                    var edge = Fins[fin].Edge;
                    if (ProfileDistance(a, point) <= tolerance)
                    {
                        kind = ParasolidConstants.PK_CLASS_vertex;
                        var vertex = Fins[fin].Sense == '+' ? Edges[edge].StartVertex : Edges[edge].EndVertex;
                        return Vertices[vertex].Header.Tag;
                    }
                    var d = ProfileSubtract(b, a); var delta = ProfileSubtract(point, a); var dd = ProfileDot(d, d);
                    var t = ProfileDot(delta, d) / dd;
                    if (t >= 0 && t <= 1)
                    {
                        var offset = ProfileSubtract(delta, ProfileScale(d, t));
                        if (ProfileDot(offset, offset) <= tolerance * tolerance) edgeTag = Edges[edge].Header.Tag;
                    }
                }
            }
            if (PlanarFaceContains(in face, loops, points, point)) faceTag = Faces[face.Source].Header.Tag;
        }
        if (edgeTag != 0) { kind = ParasolidConstants.PK_CLASS_edge; return edgeTag; }
        if (faceTag != 0) kind = ParasolidConstants.PK_CLASS_face;
        return faceTag;
    }
}
