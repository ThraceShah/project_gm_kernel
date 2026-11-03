using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>Publish exact fin pcurves with PK's tolerant loft/sweep ownership and tolerance conventions.</summary>
    private static int FreeformTolerantBoundaries(BodyTag body, BufferCount sideCount, ReadOnlySpan<int> offsets, double tolerance)
    {
        var bodySlot = TagRec(body).Slot; ref readonly var record = ref Bodies[bodySlot];
        Span<VertexSlot> vertices = stackalloc VertexSlot[record.VertexCountBody];
        Span<EdgeSlot> edges = stackalloc EdgeSlot[record.EdgeCountBody];
        Span<CurveTag> oldCurves = stackalloc CurveTag[record.EdgeCountBody];
        Span<double> uvPoles = stackalloc double[4];
        Span<double> uvKnots = stackalloc double[2] { 0, 1 };
        Span<int> uvMultiplicities = stackalloc int[2] { 2, 2 };
        var vertex = record.FirstVertexBody;
        var toleranceScale = Math.Max(tolerance, 2e-8);
        var relativeTolerance = tolerance / toleranceScale; var relativeResolution = 2e-8 / toleranceScale;
        var vertexTolerance = toleranceScale * Math.Sqrt(relativeTolerance * relativeTolerance + relativeResolution * relativeResolution);
        if (!double.IsFinite(vertexTolerance)) return ParasolidConstants.PK_ERROR_bad_parameter;
        for (var i = 0; i < vertices.Length; i++, vertex = Vertices[vertex].NextInBody)
        { vertices[i] = vertex; Vertices[vertex].Tolerance = vertexTolerance; }
        var edge = record.FirstEdgeBody;
        for (var i = 0; i < edges.Length; i++, edge = Edges[edge].NextInBody)
        { edges[i] = edge; oldCurves[i] = Edges[edge].CurveTag; Edges[edge].Tolerance = tolerance; }
        var face = record.FirstFaceBody;
        for (var f = 0; f < record.FaceCountBody; f++, face = Faces[face].NextInBody)
        {
            ref readonly var faceRecord = ref Faces[face]; var loop = faceRecord.FirstLoop;
            for (var l = 0; l < faceRecord.LoopCount; l++, loop = Loops[loop].NextInFace)
            {
                var fin = Loops[loop].FirstFin;
                for (var k = 0; k < Loops[loop].FinCount; k++, fin = Fins[fin].NextInLoop)
                {
                    CurveTag planar = 0; int error;
                    if (f < sideCount)
                    {
                        var following = f + 1;
                        for (var h = 0; h + 1 < offsets.Length; h++) if (f >= offsets[h] && f < offsets[h + 1])
                        { if (following == offsets[h + 1]) following = offsets[h]; break; }
                        var from = vertices.IndexOf(Fins[Fins[fin].PrevInLoop].Vertex);
                        var to = vertices.IndexOf(Fins[fin].Vertex);
                        var u0 = from == f || from == f + sideCount ? 0.0 : from == following || from == following + sideCount ? 1.0 : double.NaN;
                        var u1 = to == f || to == f + sideCount ? 0.0 : to == following || to == following + sideCount ? 1.0 : double.NaN;
                        var v0 = from >= sideCount ? 1.0 : 0; var v1 = to >= sideCount ? 1.0 : 0;
                        if (!double.IsFinite(u0) || !double.IsFinite(u1)) return ParasolidConstants.PK_ERROR_bad_loop;
                        uvPoles[0] = u0; uvPoles[1] = v0; uvPoles[2] = u1; uvPoles[3] = v1;
                        error = CreateFreeformCurve(1, uvPoles, 2, false, uvKnots, uvMultiplicities, false, &planar);
                    }
                    else error = FreeformPlanePcurve(Fins[fin].Edge, Fins[fin].Sense == '+', faceRecord.SurfTag, out planar);
                    if (error != 0) return error;
                    var definition = new PK_SPCURVE_sf_s { surf = faceRecord.SurfTag, curve = planar }; CurveTag spatial = 0;
                    error = SpCurveCreateImplementation(&definition, &spatial); if (error != 0) return error;
                    var trim = new PK_TRCURVE_sf_s { basis_curve = spatial }; trim.t_int.value[1] = 1; CurveTag trimmed = 0;
                    error = TrCurveCreateImplementation(&trim, &trimmed); if (error != 0) return error;
                    error = AcquireFinCurveReference(fin, trimmed); if (error != 0) return error;
                    error = TryAdoptDependentGeometry(trimmed); if (error != 0) return error;
                }
            }
        }
        // A tolerant edge is defined by its two fin curves. The exact construction
        // curves used while assembling the incidence graph are not nominal output.
        for (var i = 0; i < edges.Length; i++)
        {
            ref var curve = ref Curves[GetCurveSlotByTag(oldCurves[i])];
            curve.OwnerCount--; if (curve.OwnerCount == 0) { curve.OwnerEdge = -1; curve.PrevInBody = curve.NextInBody = 0; }
            Edges[edges[i]].CurveTag = 0; Edges[edges[i]].HasParameterRange = 0;
        }
        fixed (CurveTag* pointer = oldCurves)
        {
            var error = EntityDeleteImplementation(oldCurves.Length, pointer); if (error != 0) return error;
        }
        SessionMemoryOwner.DropBodyXt(bodySlot); return 0;
    }

    private static int FreeformPlanePcurve(EdgeSlot edge, bool forward, SurfTag planeTag, out CurveTag result)
    {
        result = 0;
        ref readonly var source = ref Edges[edge];
        if (!TryProfileEdgeRange(in source, out var a, out var b)) return ParasolidConstants.PK_ERROR_not_implemented;
        var reverse = !forward ^ (Curves[GetCurveSlotByTag(source.CurveTag)].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c);
        var error = FreeformBoundedCurve(source.CurveTag, a, b, reverse, out var spatial); if (error != 0) return error;
        try
        {
            ref readonly var data = ref BCurveDataStore[Curves[GetCurveSlotByTag(spatial)].DataIndex];
            ref readonly var plane = ref PlaneDataPool[Surfaces[GetSurfaceSlotByTag(planeTag)].DataIndex];
            var origin = Vector(plane.LocationX, plane.LocationY, plane.LocationZ); var x = Vector(plane.RefDirX, plane.RefDirY, plane.RefDirZ);
            var y = ProfileCross(Vector(plane.NormalX, plane.NormalY, plane.NormalZ), x);
            var dim = data.IsRational != 0 ? 3 : 2; Span<double> poles = stackalloc double[data.NVertices * dim];
            var input = data.PolesSpan();
            for (var i = 0; i < data.NVertices; i++)
            {
                var w = data.IsRational != 0 ? input[i * data.VertexDim + 3] : 1;
                var point = ProfileSubtract(Vector(input[i * data.VertexDim], input[i * data.VertexDim + 1], input[i * data.VertexDim + 2]), ProfileScale(origin, w));
                poles[i * dim] = ProfileDot(point, x); poles[i * dim + 1] = ProfileDot(point, y); if (dim == 3) poles[i * dim + 2] = w;
            }
            CurveTag product = 0; error = CreateFreeformCurve(data.Degree, poles, dim, data.IsRational != 0, data.KnotsSpan(), data.KnotMultsSpan(), false, &product);
            result = product; return error;
        }
        finally { EntityDeleteImplementation(1, &spatial); }
    }
}
