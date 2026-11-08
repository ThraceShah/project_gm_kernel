using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    // Command-local descriptions. Geometry is already owned by the new result;
    // callers clone borrowed geometry before handing it to this constructor.
    internal struct ModelingEdge
    {
        internal CurveTag Curve;
        internal BufferOffset StartVertex, EndVertex;
        internal double Start, End;
    }

    internal struct ModelingCoedge
    {
        internal BufferOffset Edge;
        internal byte Forward;
    }

    internal struct ModelingLoop
    {
        internal BufferOffset FirstCoedge;
        internal BufferCount CoedgeCount;
    }

    internal struct ModelingFace
    {
        internal SurfTag Surface;
        internal KernelSense Sense;
        internal BufferOffset FirstLoop;
        internal BufferCount LoopCount;
    }

    /// <summary>
    /// Build a manifold B-rep from explicit incidence, without tessellation or
    /// geometric inference. Faces and loops have outward material orientation.
    /// A solid has one exterior component and any number of cavity components;
    /// disconnected solids are returned as separate bodies by their operation.
    /// cavityFaces identifies inner boundaries when their chord volume cannot
    /// determine orientation (e.g. an untrimmed sphere with no vertices).
    /// The enclosing dispatched command provides atomic rollback on failure.
    /// </summary>
    internal static int BuildModelingBrep(ReadOnlySpan<KernelVector3> points,
        ReadOnlySpan<ModelingEdge> edges, ReadOnlySpan<ModelingCoedge> coedges,
        ReadOnlySpan<ModelingLoop> loops, ReadOnlySpan<ModelingFace> faces,
        bool solid, out EntityTag result, Span<FaceSlot> faceProducts = default,
        ReadOnlySpan<byte> cavityFaces = default,Span<EdgeSlot> edgeProducts=default,bool cloneBoundaryCurves=false)
    {
        result = 0;
        if (faces.IsEmpty || faces.Length > 4096 || points.Length > 8192 || edges.Length > 16384
            || (!faceProducts.IsEmpty && faceProducts.Length < faces.Length)
            || (!edgeProducts.IsEmpty && edgeProducts.Length < edges.Length)
            || (!cavityFaces.IsEmpty && cavityFaces.Length != faces.Length))
            return ParasolidConstants.PK_ERROR_bad_parameter;
        var scratch = CommandScratch.Current;
        var size = points.Length + 4 * edges.Length + 7 * faces.Length + loops.Length + coedges.Length;
        var memory = scratch.Take((size + 1) / 2);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var ints = MemoryMarshal.Cast<double, int>(memory);
            var cursor = 0;
            var vertexSlots = ints.Slice(cursor, points.Length); cursor += points.Length;
            var edgeSlots = ints.Slice(cursor, edges.Length); cursor += edges.Length;
            var firstFace = ints.Slice(cursor, edges.Length); cursor += edges.Length;
            var secondFace = ints.Slice(cursor, edges.Length); cursor += edges.Length;
            var firstSense = ints.Slice(cursor, edges.Length); cursor += edges.Length;
            var components = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var componentType = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var frontShell = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var backShell = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var products = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var faceDone = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var faceStack = ints.Slice(cursor, faces.Length); cursor += faces.Length;
            var loopOwners = ints.Slice(cursor, loops.Length); cursor += loops.Length;
            var coedgeOwners = ints.Slice(cursor, coedges.Length);
            firstFace.Fill(-1); secondFace.Fill(-1); firstSense.Clear();
            components.Fill(-1); loopOwners.Fill(-1); coedgeOwners.Fill(-1); faceDone.Clear();

            for (var i = 0; i < points.Length; i++)
                if (!ProfileFinite(in points[i])) return ParasolidConstants.PK_ERROR_bad_parameter;
            for (var i = 0; i < edges.Length; i++)
            {
                ref readonly var e = ref edges[i];
                if (e.StartVertex < -1 || e.EndVertex < -1 || e.StartVertex >= points.Length || e.EndVertex >= points.Length
                    || ((e.StartVertex < 0) != (e.EndVertex < 0))
                    || !double.IsFinite(e.Start) || !double.IsFinite(e.End) || !(e.End > e.Start))
                    return ParasolidConstants.PK_ERROR_bad_parameter;
                if (!State.Session->Tags.TryResolve(e.Curve, CurrentSessionId, out var curve)
                    || curve.Pool != (byte)PoolKind.Curve) return ParasolidConstants.PK_ERROR_bad_class;
                if (Curves[curve.Slot].Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
            }
            for (var f = 0; f < faces.Length; f++)
            {
                ref readonly var face = ref faces[f];
                if (face.FirstLoop < 0 || face.LoopCount < 0 || face.FirstLoop > loops.Length - face.LoopCount
                    || (face.Sense != ParasolidConstants.PK_TOPOL_sense_positive_c && face.Sense != ParasolidConstants.PK_TOPOL_sense_negative_c)
                    || (!cavityFaces.IsEmpty && cavityFaces[f] > 1)) return ParasolidConstants.PK_ERROR_bad_parameter;
                if (!State.Session->Tags.TryResolve(face.Surface, CurrentSessionId, out var surface)
                    || surface.Pool != (byte)PoolKind.Surface) return ParasolidConstants.PK_ERROR_bad_class;
                if (Surfaces[surface.Slot].Header.Partition != CurrentPartition) return ParasolidConstants.PK_ERROR_not_in_same_partition;
                for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
                {
                    ref readonly var loop = ref loops[l];
                    if (loopOwners[l] >= 0 || loop.CoedgeCount <= 0 || loop.FirstCoedge < 0
                        || loop.FirstCoedge > coedges.Length - loop.CoedgeCount) return ParasolidConstants.PK_ERROR_bad_loop;
                    loopOwners[l] = f;
                    for (var k = 0; k < loop.CoedgeCount; k++)
                    {
                        var ci = loop.FirstCoedge + k;
                        ref readonly var c = ref coedges[ci];
                        if (coedgeOwners[ci] >= 0 || (uint)c.Edge >= (uint)edges.Length || c.Forward > 1)
                            return ParasolidConstants.PK_ERROR_bad_loop;
                        coedgeOwners[ci] = f;
                        var next = coedges[loop.FirstCoedge + (k + 1) % loop.CoedgeCount];
                        if ((uint)next.Edge >= (uint)edges.Length) return ParasolidConstants.PK_ERROR_bad_loop;
                        ref readonly var e = ref edges[c.Edge];
                        var end = c.Forward != 0 ? e.EndVertex : e.StartVertex;
                        var start = next.Forward != 0 ? edges[next.Edge].StartVertex : edges[next.Edge].EndVertex;
                        if (end != start || (end < 0 && loop.CoedgeCount != 1)) return ParasolidConstants.PK_ERROR_bad_loop;
                        if (firstFace[c.Edge] < 0) { firstFace[c.Edge] = f; firstSense[c.Edge] = c.Forward; }
                        else
                        {
                            if (secondFace[c.Edge] >= 0 || firstSense[c.Edge] == c.Forward)
                                return ParasolidConstants.PK_ERROR_bad_loop;
                            secondFace[c.Edge] = f;
                        }
                    }
                }
            }
            if (loopOwners.Contains(-1) || coedgeOwners.Contains(-1)) return ParasolidConstants.PK_ERROR_bad_loop;
            for (var e = 0; e < edges.Length; e++)
                if (firstFace[e] < 0 || (solid && secondFace[e] < 0)) return ParasolidConstants.PK_ERROR_bad_loop;

            var componentCount = 0;
            for (var seed = 0; seed < faces.Length; seed++)
            {
                if (components[seed] >= 0) continue;
                var count = 1;
                faceStack[0] = seed; components[seed] = componentCount;
                while (count > 0)
                {
                    var f = faceStack[--count];
                    ref readonly var face = ref faces[f];
                    for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
                        for (var k = 0; k < loops[l].CoedgeCount; k++)
                        {
                            var e = coedges[loops[l].FirstCoedge + k].Edge;
                            var neighbor = firstFace[e] == f ? secondFace[e] : firstFace[e];
                            if (neighbor >= 0 && components[neighbor] < 0)
                            { components[neighbor] = componentCount; faceStack[count++] = neighbor; }
                        }
                }
                componentCount++;
            }

            var outerCount = 0;
            for (var c = 0; c < componentCount; c++)
            {
                var cavity = false;
                if (solid)
                {
                    if (!cavityFaces.IsEmpty)
                    {
                        var flag = -1;
                        for (var f = 0; f < faces.Length; f++) if (components[f] == c)
                        {
                            if (flag >= 0 && flag != cavityFaces[f]) return ParasolidConstants.PK_ERROR_bad_parameter;
                            flag = cavityFaces[f];
                        }
                        cavity = flag == 1;
                    }
                    else if (componentCount > 1)
                    {
                        // A chord integral determines the orientation exactly for
                        // planar polygons, but is not a certificate for a curved
                        // closed surface. Such callers provide cavityFaces.
                        double volume = 0;
                        KernelVector3 origin = default;
                        var gotOrigin = false;
                        for (var f = 0; f < faces.Length; f++) if (components[f] == c)
                        {
                            if (Surfaces[GetSurfaceSlotByTag(faces[f].Surface)].Class != SurfaceClass.Plane)
                                return ParasolidConstants.PK_ERROR_not_implemented;
                            ref readonly var face = ref faces[f];
                            for (var l = face.FirstLoop; l < face.FirstLoop + face.LoopCount; l++)
                            {
                                ref readonly var loop = ref loops[l];
                                var first = coedges[loop.FirstCoedge];
                                var index = first.Forward != 0 ? edges[first.Edge].StartVertex : edges[first.Edge].EndVertex;
                                if (index < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                                if (!gotOrigin) { origin = points[index]; gotOrigin = true; }
                                var a = ProfileSubtract(points[index], origin);
                                for (var k = 1; k + 1 < loop.CoedgeCount; k++)
                                {
                                    var bCoedge = coedges[loop.FirstCoedge + k];
                                    var cCoedge = coedges[loop.FirstCoedge + k + 1];
                                    var bi = bCoedge.Forward != 0 ? edges[bCoedge.Edge].StartVertex : edges[bCoedge.Edge].EndVertex;
                                    var ci = cCoedge.Forward != 0 ? edges[cCoedge.Edge].StartVertex : edges[cCoedge.Edge].EndVertex;
                                    if (bi < 0 || ci < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                                    volume += ProfileDot(a, ProfileCross(ProfileSubtract(points[bi], origin), ProfileSubtract(points[ci], origin)));
                                }
                            }
                        }
                        if (!double.IsFinite(volume) || volume == 0) return ParasolidConstants.PK_ERROR_bad_loop;
                        cavity = volume < 0;
                    }
                    if (!cavity) outerCount++;
                }
                componentType[c] = cavity ? 1 : 0;
            }
            if (solid && outerCount != 1) return ParasolidConstants.PK_ERROR_disjoint;

            var body = TryAllocateBodies();
            if (body < 0) return ParasolidConstants.PK_ERROR_memory_full;
            InitializeBody(ref Bodies[body]); AssignPartition(ref Bodies[body].Header, CurrentPartition);
            Bodies[body].BodyType = solid ? ParasolidConstants.PK_BODY_type_solid_c : ParasolidConstants.PK_BODY_type_sheet_c;
            var outside = AddProfileVoidRegion(body);
            if (outside < 0) return ParasolidConstants.PK_ERROR_memory_full;
            var material = -1;
            if (solid)
            {
                material = AddProfileVoidRegion(body);
                if (material < 0) return ParasolidConstants.PK_ERROR_memory_full;
                Regions[material].IsSolid = 1;
            }
            // XT's body shell chain starts with the unbounded void/exterior
            // boundary even when the caller lists original cavity faces first.
            for (var pass = 0; pass < (solid ? 2 : 1); pass++)
            for (var c = 0; c < componentCount; c++)
            {
                if (solid && componentType[c] != pass) continue;
                var voidRegion = outside;
                if (solid && componentType[c] != 0)
                {
                    voidRegion = AddProfileVoidRegion(body);
                    if (voidRegion < 0) return ParasolidConstants.PK_ERROR_memory_full;
                }
                frontShell[c] = AddProfileShell(body, voidRegion);
                backShell[c] = solid ? AddProfileShell(body, material) : frontShell[c];
                if (frontShell[c] < 0 || backShell[c] < 0) return ParasolidConstants.PK_ERROR_memory_full;
            }
            for (var i = 0; i < points.Length; i++)
                if (!AddVertex(body, points[i].X, points[i].Y, points[i].Z, out vertexSlots[i]))
                    return ParasolidConstants.PK_ERROR_memory_full;
            for (var i = 0; i < edges.Length; i++)
            {
                ref readonly var e = ref edges[i];
                var curve=e.Curve;
                if(cloneBoundaryCurves)
                {
                    var copied=CloneBoundaryCurve(e.Curve,faces[firstFace[i]].Surface,secondFace[i]>=0?faces[secondFace[i]].Surface:0,out curve);
                    if(copied!=0)return copied;
                }
                edgeSlots[i] = AddProfileEdge(body, curve, e.StartVertex < 0 ? -1 : vertexSlots[e.StartVertex],
                    e.EndVertex < 0 ? -1 : vertexSlots[e.EndVertex], e.Start, e.End);
                if (edgeSlots[i] < 0) return ParasolidConstants.PK_ERROR_memory_full;
            }
            for (var f = 0; f < faces.Length; f++)
            {
                ref readonly var source = ref faces[f];
                var face = AddFace(body, source.Surface);
                if (face < 0) return ParasolidConstants.PK_ERROR_memory_full;
                products[f] = face; Faces[face].Orientation = source.Sense;
                if (!AttachProfileSolidFace(face, frontShell[components[f]], backShell[components[f]]))
                    return ParasolidConstants.PK_ERROR_memory_full;
                for (var l = source.FirstLoop; l < source.FirstLoop + source.LoopCount; l++)
                {
                    var loop = AddLoop(face);
                    if (loop < 0) return ParasolidConstants.PK_ERROR_memory_full;
                    for (var k = 0; k < loops[l].CoedgeCount; k++)
                    {
                        var c = coedges[loops[l].FirstCoedge + k];
                        if (AddProfileFin(loop, edgeSlots[c.Edge], c.Forward != 0) < 0)
                            return ParasolidConstants.PK_ERROR_memory_full;
                    }
                }
            }
            if (!solid)
            {
                for (var e = 0; e < edges.Length; e++) if (secondFace[e] < 0)
                    if (AddProfileFin(-1, edgeSlots[e], firstSense[e] == 0) < 0)
                        return ParasolidConstants.PK_ERROR_memory_full;
                // Boundary dummy fins form the opposite traversal of each
                // boundary wire; they remain outside any face loop.
                for (var e = 0; e < edges.Length; e++) if (secondFace[e] < 0)
                {
                    var fin = Edges[edgeSlots[e]].LastFinEdge;
                    var end = Fins[fin].Vertex;
                    var nextFin = -1;
                    if (end < 0) nextFin = fin;
                    else for (var j = 0; j < edges.Length; j++) if (secondFace[j] < 0)
                    {
                        var candidate = Edges[edgeSlots[j]].LastFinEdge;
                        var start = Fins[candidate].Sense == '+' ? Edges[edgeSlots[j]].StartVertex : Edges[edgeSlots[j]].EndVertex;
                        if (start == end)
                        {
                            if (nextFin >= 0) return ParasolidConstants.PK_ERROR_bad_loop;
                            nextFin = candidate;
                        }
                    }
                    if (nextFin < 0) return ParasolidConstants.PK_ERROR_bad_loop;
                    Fins[fin].NextInLoop = nextFin; Fins[nextFin].PrevInLoop = fin;
                }
            }
            if (solid && !CheckProfileSolidTopology(body)) return ParasolidConstants.PK_ERROR_bad_loop;
            EntityTag completed = 0;
            var error = FinishCreatedBody(body, &completed);
            if (error != 0) return error;
            if (!solid && !PublishProfileDummyFins(body)) return ParasolidConstants.PK_ERROR_memory_full;
            if (!faceProducts.IsEmpty) products.CopyTo(faceProducts);
            if (!edgeProducts.IsEmpty) edgeSlots.CopyTo(edgeProducts);
            result = completed;
            return 0;
        }
        finally { scratch.Return(memory); }
    }
}
