using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private const BufferCount CurvedCutLimit = 64;
    private const BufferCount CurvedNodeLimit = 4 + 2 * CurvedCutLimit;

    // Exact open branches of the existing intersection solver partition a
    // natural rectangular NURBS face. Boundary order supplies the rotation
    // system: incoming boundary -> inward cut -> outgoing boundary. Interior
    // junctions and tangent/coincident cuts require a different rotation system.
    private static int PartitionCurvedSheet(BodySlot body, SurfTag plane, double tolerance,
        out BodyTag partition, Span<FaceSlot> products, Span<sbyte> sides, out BufferCount faceCount,
        Span<EdgeSlot> cuts, out BufferCount cutCount)
    {
        partition = 0; faceCount = cutCount = 0;
        if (Xt.IsOpaque(body) || Bodies[body].BodyType != ParasolidConstants.PK_BODY_type_sheet_c
            || Bodies[body].FaceCountBody != 1) return ParasolidConstants.PK_ERROR_not_implemented;
        var source = Bodies[body].FirstFaceBody;
        var surface = Faces[source].SurfTag; var supportSlot = GetSurfaceSlotByTag(surface);
        if (supportSlot < 0) return ParasolidConstants.PK_ERROR_missing_geom;
        var support = Surfaces[supportSlot];
        if (support.Class != SurfaceClass.BSurface || support.Sense == ParasolidConstants.PK_TOPOL_sense_negative_c
            || Faces[source].Orientation != ParasolidConstants.PK_TOPOL_sense_positive_c
            || Faces[source].LoopCount != 1 || Loops[Faces[source].FirstLoop].FinCount != 4)
            return ParasolidConstants.PK_ERROR_not_implemented;
        var data = BSurfaceDataStore[support.DataIndex];
        if (data.IsUClosed != 0 || data.IsVClosed != 0) return ParasolidConstants.PK_ERROR_not_implemented;
        var uMults = data.UKnotMultsSpan(); var vMults = data.VKnotMultsSpan();
        // A C0 knot needs an additional face/edge partition even when the
        // cutting plane misses it. Do not publish a non-G1 accurate edge.
        for (var i = 1; i < uMults.Length - 1; i++) if (uMults[i] >= data.UDegree) return ParasolidConstants.PK_ERROR_not_implemented;
        for (var i = 1; i < vMults.Length - 1; i++) if (vMults[i] >= data.VDegree) return ParasolidConstants.PK_ERROR_not_implemented;
        var planeSlot = GetSurfaceSlotByTag(plane);
        if (!TryPrepareSurface(Surfaces[planeSlot], out var cutter)) return ParasolidConstants.PK_ERROR_not_implemented;
        Span<OperationIntersectionBranch> branches = stackalloc OperationIntersectionBranch[CurvedCutLimit];
        Span<KernelVector3> isolated = stackalloc KernelVector3[CurvedCutLimit];
        var status = FindSurfaceIntersections(supportSlot, planeSlot, branches, out var nb, isolated, out var ni);
        if (status != AlgorithmStatus.Success) return IntersectionError(status);
        if (ni != 0) return ParasolidConstants.PK_ERROR_not_implemented;
        var scratch = CommandScratch.Current; var memory = scratch.Take(2048);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var offset = 0;
            var nodes = ModelSpan<KernelVector3>(bytes, ref offset, CurvedNodeLimit);
            var perimeter = ModelSpan<double>(bytes, ref offset, CurvedNodeLimit);
            var endpointIds = ModelSpan<BufferOffset>(bytes, ref offset, CurvedNodeLimit);
            var edges = ModelSpan<ModelingEdge>(bytes, ref offset, CurvedNodeLimit + CurvedCutLimit);
            var coedges = ModelSpan<ModelingCoedge>(bytes, ref offset, CurvedNodeLimit + 2 * CurvedCutLimit);
            var loops = ModelSpan<ModelingLoop>(bytes, ref offset, CurvedCutLimit + 1);
            var faces = ModelSpan<ModelingFace>(bytes, ref offset, CurvedCutLimit + 1);
            var boundarySide = ModelSpan<sbyte>(bytes, ref offset, CurvedNodeLimit);
            var used = ModelSpan<byte>(bytes, ref offset, CurvedNodeLimit + 2 * CurvedCutLimit); used.Clear();
            Span<CurveTag> boundaries = stackalloc CurveTag[4];
            Span<KernelVector3> jet = stackalloc KernelVector3[2];
            Span<OperationIntersectionPoint> hits = stackalloc OperationIntersectionPoint[IntersectionPointCapacity];
            var fin = Loops[Faces[source].FirstLoop].FirstFin; var boundaryHits = 0;
            for (var i = 0; i < 4; i++, fin = Fins[fin].NextInLoop)
            {
                var fixedValue = i == 0 ? support.VMin : i == 1 ? support.UMax : i == 2 ? support.VMax : support.UMin;
                var constantU = (i & 1) != 0;
                var lo = constantU ? support.VMin : support.UMin; var hi = constantU ? support.VMax : support.UMax;
                var error = FreeformIsoparam(surface, constantU, fixedValue, lo, hi, out boundaries[i], out _, out _);
                if (error != 0) return error;
                var edge = Edges[Fins[fin].Edge]; var curve = GetCurveSlotByTag(edge.CurveTag);
                if (curve < 0 || Curves[curve].Class != CurveClass.BCurve
                    || !TryProfileEdgeRange(in edge, out var a, out var b) || a != lo || b != hi
                    || ((Curves[curve].Sense == ParasolidConstants.PK_TOPOL_sense_negative_c) == (Fins[fin].Sense == '+')) != (i >= 2))
                    return ParasolidConstants.PK_ERROR_not_implemented;
                var expected = GetBCurveView(BCurveDataStore[Curves[GetCurveSlotByTag(boundaries[i])].DataIndex]);
                var actual = GetBCurveView(BCurveDataStore[Curves[curve].DataIndex]);
                if (actual.Degree != expected.Degree || actual.Dimension != expected.Dimension || actual.Rational != expected.Rational
                    || actual.Periodic || !actual.Knots.SequenceEqual(expected.Knots) || !actual.Vertices.SequenceEqual(expected.Vertices))
                    return ParasolidConstants.PK_ERROR_not_implemented;
                Curves[GetCurveSlotByTag(boundaries[i])].Sense = Curves[curve].Sense;
                // A boundary touch/coincidence needs additional vertices or
                // coincident-edge handling; do not infer it from midpoint signs.
                status = BCurveAnalyticIntersection.Find(actual, cutter, lo, hi, hits, out var nh);
                if (status != AlgorithmStatus.Success) return IntersectionError(status);
                boundaryHits += nh;
                for (var h = 0; h < nh; h++) if (hits[h].Kind != IntersectionVectorKind.Simple)
                    return ParasolidConstants.PK_ERROR_not_implemented;
                perimeter[i] = i; endpointIds[i] = -1;
                error = CurvedBoundaryPoint(support, i, jet); if (error != 0) return error; nodes[i] = jet[0];
            }
            if (boundaryHits != 2 * nb) return ParasolidConstants.PK_ERROR_not_implemented;
            var nn = 4;
            for (var i = 0; i < nb; i++)
            {
                if (branches[i].Kind != IntersectionCurveKind.Simple) return ParasolidConstants.PK_ERROR_not_implemented;
                CurveTag curve = 0; var error = CreateIntersectionBranch(branches[i], &curve);
                if (error != 0) return error;
                edges[CurvedNodeLimit + i] = new() { Curve = curve, Start = branches[i].Start, End = branches[i].End };
                for (var end = 0; end < 2; end++)
                {
                    status = EvaluateCurveCore(in Curves[GetCurveSlotByTag(curve)], end == 0 ? branches[i].Start : branches[i].End, 0, false, jet, out _);
                    if (status != AlgorithmStatus.Success) return EvaluationError(status);
                    status = TryIntersectionPairedParameters(surface, plane, jet[0], out var uv, out _);
                    if (status != AlgorithmStatus.Success) return IntersectionError(status);
                    if (!CurvedPerimeter(support, uv, jet[0], tolerance, out var t)) return ParasolidConstants.PK_ERROR_not_implemented;
                    for (var j = 0; j < nn; j++) if (Math.Abs(perimeter[j] - t) < 1e-10 || ProfileDistance(nodes[j], jet[0]) <= tolerance)
                        return ParasolidConstants.PK_ERROR_not_implemented;
                    nodes[nn] = jet[0]; perimeter[nn] = t; endpointIds[nn++] = 2 * i + end;
                }
            }
            // Boundary ordering is topological; the intersection curves retain
            // their exact spatial geometry and parameters throughout.
            for (var i = 1; i < nn; i++)
            for (var j = i; j > 0 && perimeter[j] < perimeter[j - 1]; j--)
            {
                (perimeter[j], perimeter[j - 1]) = (perimeter[j - 1], perimeter[j]);
                (nodes[j], nodes[j - 1]) = (nodes[j - 1], nodes[j]);
                (endpointIds[j], endpointIds[j - 1]) = (endpointIds[j - 1], endpointIds[j]);
            }
            for (var i = 0; i < nn; i++)
            {
                var t0 = perimeter[i]; var t1 = i + 1 == nn ? 4 : perimeter[i + 1]; var side = (int)t0;
                var lo = (side & 1) == 0 ? support.UMin : support.VMin;
                var hi = (side & 1) == 0 ? support.UMax : support.VMax;
                var a = lo + (hi - lo) * (t0 - side); var b = lo + (hi - lo) * (t1 - side);
                if (side >= 2) { a = hi - (a - lo); b = hi - (b - lo); }
                edges[i] = new() { Curve = boundaries[side], StartVertex = i,
                    EndVertex = (i + 1) % nn, Start = Math.Min(a, b), End = Math.Max(a, b) };
                var error = CurvedBoundaryPoint(support, (t0 + t1) * .5, jet); if (error != 0) return error;
                var distance = Dot(Sub(jet[0], cutter.Origin), cutter.Axis);
                if (Math.Abs(distance) <= tolerance) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                boundarySide[i] = distance > 0 ? (sbyte)1 : (sbyte)-1;
                if (endpointIds[i] >= 0)
                {
                    var id = endpointIds[i]; ref var edge = ref edges[CurvedNodeLimit + id / 2];
                    if ((id & 1) == 0) edge.StartVertex = i; else edge.EndVertex = i;
                }
            }
            edges.Slice(CurvedNodeLimit, nb).CopyTo(edges[nn..]);
            var nc = 0; var nf = 0;
            for (var seed = 0; seed < nn + 2 * nb; seed++)
            {
                if (used[seed] != 0) continue;
                if (nf == products.Length || nf == sides.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                var current = seed; var first = nc; sbyte classification = 0;
                do
                {
                    if (used[current] != 0 || nc == coedges.Length) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                    used[current] = 1; int node;
                    if (current < nn)
                    {
                        if (classification != 0 && classification != boundarySide[current]) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                        classification = boundarySide[current];
                        coedges[nc++] = new() { Edge = current, Forward = 1 };
                        node = (current + 1) % nn; var id = endpointIds[node];
                        current = id < 0 ? node : nn + id;
                    }
                    else
                    {
                        var id = current - nn; var edge = nn + id / 2; var forward = (id & 1) == 0;
                        coedges[nc++] = new() { Edge = edge, Forward = forward ? (byte)1 : (byte)0 };
                        node = forward ? edges[edge].EndVertex : edges[edge].StartVertex; current = node;
                    }
                } while (current != seed);
                if (classification == 0) return ParasolidConstants.PK_ERROR_not_implemented;
                loops[nf] = new() { FirstCoedge = first, CoedgeCount = nc - first };
                sides[nf] = classification * LocalSenseSign(Surfaces[planeSlot].Sense) > 0 ? (sbyte)1 : (sbyte)-1;
                nf++;
            }
            if (nf != nb + 1) return ParasolidConstants.PK_ERROR_cant_do_intersect;
            var clone = TryCloneEntityCore(surface, out var clonedSurface); if (clone != 0) return clone;
            if (nb > 0)
            {
                clone = TryCloneEntityCore(plane, out var clonedPlane); if (clone != 0) return clone;
                for (var i = 0; i < nb; i++)
                {
                    ref var edge = ref edges[nn + i]; var old = edge.Curve;
                    var error = MakeCurvedCutICurve(old, edge.Start, edge.End, clonedSurface, clonedPlane, out var curve);
                    if (error != 0) return error;
                    edge.Curve = curve;
                    edge.Start = Curves[GetCurveSlotByTag(curve)].TMin; edge.End = Curves[GetCurveSlotByTag(curve)].TMax;
                    error = EntityDelete(1, &old); if (error != 0) return error;
                }
            }
            for (var f = 0; f < nf; f++) faces[f] = new() { Surface = clonedSurface,
                Sense = ParasolidConstants.PK_TOPOL_sense_positive_c, FirstLoop = f, LoopCount = 1 };
            var build = BuildModelingBrep(nodes[..nn], edges[..(nn + nb)], coedges[..nc], loops[..nf], faces[..nf], false,
                out partition, products[..nf]);
            if (build != 0) return build;
            var partitionSlot = TagRec(partition).Slot; var actualEdge = Bodies[partitionSlot].FirstEdgeBody;
            for (var i = 0; i < nn; i++) actualEdge = Edges[actualEdge].NextInBody;
            for (var i = 0; i < nb; i++, actualEdge = Edges[actualEdge].NextInBody) cuts[i] = actualEdge;
            faceCount = nf; cutCount = nb; return 0;
        }
        finally { scratch.Return(memory); }
    }

    // Chart points are witnesses on a previously discovered exact branch.
    // ICURVE evaluation solves the two exact supports, rather than interpolating
    // a polyline. The positive-weight Bezier hull supplies chart error bounds.
    private static int MakeCurvedCutICurve(CurveTag exact, double start, double end, SurfTag surface, SurfTag plane, out CurveTag curve)
    {
        curve = 0;
        var record = Curves[GetCurveSlotByTag(exact)];
        if (record.Class is not (CurveClass.BCurve or CurveClass.Line)) return ParasolidConstants.PK_ERROR_not_implemented;
        var count = record.Class == CurveClass.Line ? 2 : 33;
        Span<double> chartBuffer = stackalloc double[3 * 33]; Span<double> uvBuffer = stackalloc double[4 * 33];
        var chart = chartBuffer[..(3 * count)]; var uv = uvBuffer[..(4 * count)];
        Span<KernelVector3> jet = stackalloc KernelVector3[2];
        var status = EvaluateCurveCore(in record, start, 1, false, jet, out _);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        status = TryIntersectionPairedParameters(surface, plane, jet[0], out var firstUv, out _);
        if (status != AlgorithmStatus.Success) return IntersectionError(status);
        SurfaceDerivativeLayout.TryCreate(1, 1, out var layout); Span<KernelVector3> surfaceJet = stackalloc KernelVector3[4];
        status = EvaluateSurfaceCore(in Surfaces[GetSurfaceSlotByTag(surface)], firstUv.param[0], firstUv.param[1], in layout, surfaceJet, 0);
        if (status != AlgorithmStatus.Success) return EvaluationError(status);
        var normal = ProfileCross(surfaceJet[layout.GetIndex(1, 0)], surfaceJet[layout.GetIndex(0, 1)]);
        TryPrepareSurface(Surfaces[GetSurfaceSlotByTag(plane)], out var cutting);
        var forward = Dot(ProfileCross(normal, Scale(cutting.Axis, LocalSenseSign(Surfaces[GetSurfaceSlotByTag(plane)].Sense))), jet[1]) > 0;
        var first = forward ? surface : plane; var second = forward ? plane : surface;
        for (var i = 0; i < count; i++)
        {
            var parameter = start + (end - start) * i / (count - 1);
            status = EvaluateCurveCore(in record, parameter, 0, false, jet, out _);
            if (status != AlgorithmStatus.Success) return EvaluationError(status);
            chart[3 * i] = jet[0].X; chart[3 * i + 1] = jet[0].Y; chart[3 * i + 2] = jet[0].Z;
            status = TryIntersectionPairedParameters(first, second, jet[0], out var a, out var b);
            if (status != AlgorithmStatus.Success) return IntersectionError(status);
            uv[4 * i] = a.param[0]; uv[4 * i + 1] = a.param[1]; uv[4 * i + 2] = b.param[0]; uv[4 * i + 3] = b.param[1];
        }
        double chordalError = 1e-12, angularError = 1e-12;
        if (record.Class == CurveClass.BCurve)
        {
            var view = GetBCurveView(BCurveDataStore[record.DataIndex]);
            if (view.VertexCount != view.Degree + 1 || !view.Rational || view.Dimension != 4)
                return ParasolidConstants.PK_ERROR_not_implemented;
            var error = CurvedChartBounds(view, out chordalError, out angularError); if (error != 0) return error;
        }
        var input = new IcurveDecodeInput
        {
            Surface0Tag = first, Surface1Tag = second, BaseScale = 1, ChartCount = count, ChartHvecs = chart,
            UvType = IntersectionUvType.Both, UvValues = uv, ChordalError = chordalError, AngularError = angularError,
            Start = new() { Type = LimitType.Artificial, TermUse = LimitTermUse.Unset, Hvecs = chart[..3] },
            End = new() { Type = LimitType.Artificial, TermUse = LimitTermUse.Unset, Hvecs = chart[^3..] },
            Sense = ParasolidConstants.PK_TOPOL_sense_positive_c,
        };
        status = DecodeIcurve(input, out var data, out _, out _); if (status != AlgorithmStatus.Success) return IntersectionError(status);
        status = TryBindICurveEntity(data, out curve); if (status != AlgorithmStatus.Success) return IntersectionError(status);
        ICurveDataPool[data].OwnsSupportSurfaces = 1;
        return 0;
    }

    private static int CurvedChartBounds(in BCurveView view, out double chordal, out double angular)
    {
        chordal = angular = 0;
        Span<double> remainder = stackalloc double[68]; Span<double> work = stackalloc double[68];
        Span<double> segment = stackalloc double[68];
        view.Vertices.CopyTo(remainder); var degree = view.Degree;
        for (var interval = 0; interval < 32; interval++)
        {
            remainder.CopyTo(work); var t = 1.0 / (32 - interval);
            for (var level = 0; level <= degree; level++)
            {
                for (var d = 0; d < 4; d++)
                { segment[4 * level + d] = work[d]; remainder[4 * (degree - level) + d] = work[4 * (degree - level) + d]; }
                for (var j = 0; j < degree - level; j++) for (var d = 0; d < 4; d++)
                    work[4 * j + d] = (1 - t) * work[4 * j + d] + t * work[4 * (j + 1) + d];
            }
            var origin = Vector(segment[0] / segment[3], segment[1] / segment[3], segment[2] / segment[3]);
            var last = 4 * degree; var end = Vector(segment[last] / segment[last + 3], segment[last + 1] / segment[last + 3], segment[last + 2] / segment[last + 3]);
            var chord = Sub(end, origin); var length = OperationIntersection.Norm(chord);
            if (!(length > 0)) return ParasolidConstants.PK_ERROR_cant_do_intersect;
            chord = Scale(chord, 1 / length);
            for (var i = 0; i <= degree; i++)
            {
                var at = 4 * i; if (!(segment[at + 3] > 0)) return ParasolidConstants.PK_ERROR_cant_do_intersect;
                var p = Vector(segment[at] / segment[at + 3], segment[at + 1] / segment[at + 3], segment[at + 2] / segment[at + 3]);
                var delta = Sub(p, origin); var projection = Dot(delta, chord);
                chordal = Math.Max(chordal, OperationIntersection.Norm(Sub(delta, Scale(chord, Math.Clamp(projection, 0, length)))));
                for (var j = 0; j < i; j++)
                {
                    var a = 4 * j; var q = Vector(segment[a] / segment[a + 3], segment[a + 1] / segment[a + 3], segment[a + 2] / segment[a + 3]);
                    var direction = Sub(p, q); var norm = OperationIntersection.Norm(direction);
                    if (norm == 0) continue;
                    var cosine = Dot(direction, chord) / norm;
                    if (!(cosine > 1e-8)) return ParasolidConstants.PK_ERROR_not_implemented;
                    angular = Math.Max(angular, Math.Acos(Math.Min(1, cosine)));
                }
            }
        }
        chordal = Math.Max(chordal * 1.000001, 1e-12); angular = Math.Max(angular * 1.000001, 1e-12);
        return double.IsFinite(chordal) && double.IsFinite(angular) ? 0 : ParasolidConstants.PK_ERROR_cant_do_intersect;
    }

    private static int CurvedBoundaryPoint(in SurfaceRecord support, double t, Span<KernelVector3> point)
    {
        var side = Math.Min(3, (int)t); var s = t - side;
        var u = side == 0 ? support.UMin + s * (support.UMax - support.UMin)
            : side == 1 ? support.UMax : side == 2 ? support.UMax - s * (support.UMax - support.UMin) : support.UMin;
        var v = side == 0 ? support.VMin : side == 1 ? support.VMin + s * (support.VMax - support.VMin)
            : side == 2 ? support.VMax : support.VMax - s * (support.VMax - support.VMin);
        SurfaceDerivativeLayout.TryCreate(0, 0, out var layout);
        var status = EvaluateSurfaceCore(in support, u, v, in layout, point, 0);
        return status == AlgorithmStatus.Success ? 0 : EvaluationError(status);
    }

    private static bool CurvedPerimeter(in SurfaceRecord support, PK_UV_s uv, in KernelVector3 point, double tolerance, out double t)
    {
        var u = (uv.param[0] - support.UMin) / (support.UMax - support.UMin);
        var v = (uv.param[1] - support.VMin) / (support.VMax - support.VMin); t = 0;
        u = Math.Clamp(u, 0, 1); v = Math.Clamp(v, 0, 1);
        Span<KernelVector3> projected = stackalloc KernelVector3[1]; var found = false;
        for (var side = 0; side < 4; side++)
        {
            var candidate = side == 0 ? u : side == 1 ? 1 + v : side == 2 ? 3 - u : 4 - v;
            if (CurvedBoundaryPoint(support, candidate, projected) != 0 || ProfileDistance(projected[0], point) > tolerance) continue;
            if (found) return false;
            t = candidate; found = true;
        }
        return found && t > 0 && t < 4;
    }

    private static int ImprintCurvedSheet(BodySlot body, SurfTag plane, double tolerance, int* count, EntityTag** returned)
    {
        Span<FaceSlot> products = stackalloc FaceSlot[CurvedCutLimit + 1];
        Span<sbyte> sides = stackalloc sbyte[CurvedCutLimit + 1];
        Span<EdgeSlot> cuts = stackalloc EdgeSlot[CurvedCutLimit];
        var source = Bodies[body].FirstFaceBody;
        var error = PartitionCurvedSheet(body, plane, tolerance, out var partition, products, sides, out _, cuts, out var nc);
        if (error != 0) return error;
        if (nc == 0) return EntityDelete(1, &partition);
        var edges = AllocateReturnSlice(nc); if (edges == null) return ParasolidConstants.PK_ERROR_memory_full;
        for (var i = 0; i < nc; i++) edges[i] = Edges[cuts[i]].Header.Tag;
        Span<FaceSlot> originals = stackalloc FaceSlot[1] { source };
        error = PreserveModelingFaceHandles(body, TagRec(partition).Slot, originals, products[..1]);
        if (error == 0) error = ReplaceProfileBody(body, TagRec(partition).Slot);
        if (error != 0) return error;
        *count = nc; *returned = edges; return 0;
    }

    private static int SectionCurvedSheet(BodySlot body, SurfTag plane, PK_BODY_section_o_s* options, PK_section_r_s* result)
    {
        Span<FaceSlot> faces = stackalloc FaceSlot[CurvedCutLimit + 1];
        Span<sbyte> sides = stackalloc sbyte[CurvedCutLimit + 1];
        Span<EdgeSlot> cuts = stackalloc EdgeSlot[CurvedCutLimit];
        var original = Bodies[body].FirstFaceBody;
        var error = PartitionCurvedSheet(body, plane, options->default_tol, out var partition, faces, sides, out var nf, cuts, out var cutCount);
        if (error != 0) return error;
        if (cutCount == 0)
        {
            error = EntityDelete(1, &partition); if (error != 0) return error;
            var tag = Bodies[body].Header.Tag; var frontSide = sides[0] > 0;
            if (frontSide && options->fence == ParasolidConstants.PK_section_fence_back_c
                || !frontSide && options->fence == ParasolidConstants.PK_section_fence_front_c) return EntityDelete(1, &tag);
            var retained = AllocateReturnSlice(1); if (retained == null) return ParasolidConstants.PK_ERROR_memory_full;
            retained[0] = tag;
            if (frontSide) result->front_bodies = new() { length = 1, array = retained };
            else result->back_bodies = new() { length = 1, array = retained };
            return 0;
        }
        var frontCount = 0; var backCount = 0;
        for (var f = 0; f < nf; f++)
        {
            if (sides[f] > 0 && options->fence != ParasolidConstants.PK_section_fence_back_c) frontCount++;
            if (sides[f] < 0 && options->fence != ParasolidConstants.PK_section_fence_front_c) backCount++;
        }
        var front = frontCount == 0 ? null : AllocateReturnSlice(frontCount);
        var back = backCount == 0 ? null : AllocateReturnSlice(backCount);
        if (frontCount != 0 && front == null || backCount != 0 && back == null) return ParasolidConstants.PK_ERROR_memory_full;
        var first = true; var a = 0; var b = 0;
        Span<FaceSlot> selected = stackalloc FaceSlot[1];
        Span<FaceSlot> product = stackalloc FaceSlot[1];
        Span<FaceSlot> originals = stackalloc FaceSlot[1] { original };
        // Native section persistence favors the front result when both sides
        // survive. Sheet sections create no cap faces on the cutting surface.
        for (var side = 1; side >= -1; side -= 2)
        for (var f = 0; f < nf; f++)
        {
            if (sides[f] != side || side > 0 && options->fence == ParasolidConstants.PK_section_fence_back_c
                || side < 0 && options->fence == ParasolidConstants.PK_section_fence_front_c) continue;
            selected[0] = faces[f];
            error = CopyModelingFaces(selected, false, out var output, product); if (error != 0) return error;
            if (first)
            {
                error = PreserveModelingFaceHandles(body, TagRec(output).Slot, originals, product);
                if (error == 0) error = ReplaceProfileBody(body, TagRec(output).Slot);
                if (error != 0) return error;
                output = Bodies[body].Header.Tag; first = false;
            }
            if (side > 0) front[a++] = output; else back[b++] = output;
        }
        error = EntityDelete(1, &partition); if (error != 0) return error;
        if (first) { var tag = Bodies[body].Header.Tag; error = EntityDelete(1, &tag); if (error != 0) return error; }
        result->front_bodies = new() { length = frontCount, array = front };
        result->back_bodies = new() { length = backCount, array = back };
        return 0;
    }
}
