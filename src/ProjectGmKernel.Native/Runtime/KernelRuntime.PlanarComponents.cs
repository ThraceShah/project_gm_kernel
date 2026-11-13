using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;
using static ProjectGmKernel.Native.Geometry.Evaluation.EvaluationMath;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int BuildPlanarComponents(ReadOnlySpan<KernelVector3> points, ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> coedges, ReadOnlySpan<ModelingLoop> loops, ReadOnlySpan<ModelingFace> faces,
        ReadOnlySpan<PlanarModelFace> sources, ReadOnlySpan<int> sourceIndices, bool solid, double tolerance,
        Span<EntityTag> results, out int resultCount, Span<FaceTag> sourceProducts,scoped Span<BooleanTopologyOrigin> lineage=default)
    {
        resultCount = 0;
        var scratch = CommandScratch.Current;
        var memory = scratch.Take(120000);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var cursor = 0;
            var component = ModelSpan<int>(bytes, ref cursor, faces.Length);
            var firstFace = ModelSpan<int>(bytes, ref cursor, edges.Length);
            var componentParent = ModelSpan<int>(bytes, ref cursor, faces.Length);
            var volume = ModelSpan<double>(bytes, ref cursor, faces.Length);
            var vertexMap = ModelSpan<int>(bytes, ref cursor, points.Length);
            var edgeMap = ModelSpan<int>(bytes, ref cursor, edges.Length);
            var productFaces = ModelSpan<int>(bytes, ref cursor, faces.Length);
            var sourceFaces = ModelSpan<int>(bytes, ref cursor, faces.Length);
            var newPoints = ModelSpan<KernelVector3>(bytes, ref cursor, points.Length);
            var newEdges = ModelSpan<ModelingEdge>(bytes, ref cursor, edges.Length);
            var newCoedges = ModelSpan<ModelingCoedge>(bytes, ref cursor, coedges.Length);
            var newLoops = ModelSpan<ModelingLoop>(bytes, ref cursor, loops.Length);
            var newFaces = ModelSpan<ModelingFace>(bytes, ref cursor, faces.Length);
            var cavities = ModelSpan<byte>(bytes, ref cursor, faces.Length);
            if (cursor > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            firstFace.Fill(-1); volume.Clear();
            for (var f = 0; f < faces.Length; f++) component[f] = f;
            for (var f = 0; f < faces.Length; f++)
            {
                for (var l = faces[f].FirstLoop; l < faces[f].FirstLoop + faces[f].LoopCount; l++)
                    for (var k = 0; k < loops[l].CoedgeCount; k++)
                    {
                        var e = coedges[loops[l].FirstCoedge + k].Edge;
                        if (firstFace[e] < 0) firstFace[e] = f;
                        else
                        {
                            var a = ComponentRoot(component, f); var b = ComponentRoot(component, firstFace[e]);
                            if (a != b) component[Math.Max(a, b)] = Math.Min(a, b);
                        }
                    }
            }
            for (var f = 0; f < faces.Length; f++) component[f] = ComponentRoot(component, f);
            var origin = points.IsEmpty ? default : points[0];
            for (var f = 0; f < faces.Length; f++)
                for (var l = faces[f].FirstLoop; l < faces[f].FirstLoop + faces[f].LoopCount; l++)
                {
                    var loop = loops[l];
                    var a = ProfileSubtract(ModelingLoopPoint(points, edges, coedges, loop, 0), origin);
                    for (var k = 1; k + 1 < loop.CoedgeCount; k++)
                    {
                        var b = ProfileSubtract(ModelingLoopPoint(points, edges, coedges, loop, k), origin);
                        var c = ProfileSubtract(ModelingLoopPoint(points, edges, coedges, loop, k + 1), origin);
                        volume[component[f]] += ProfileDot(a, ProfileCross(b, c));
                    }
                }
            componentParent.Fill(-1);
            for (var f = 0; f < faces.Length; f++) if (component[f] == f)
            {
                if (!solid || volume[f] > tolerance * tolerance * tolerance) { componentParent[f] = f; continue; }
                if (Math.Abs(volume[f]) <= tolerance * tolerance * tolerance) return ParasolidConstants.PK_ERROR_bad_loop;
                var point = ModelingLoopPoint(points, edges, coedges, loops[faces[f].FirstLoop], 0);
                var parent = -1; var best = double.PositiveInfinity;
                for (var other = 0; other < faces.Length; other++)
                    if (component[other] == other && volume[other] > 0 && volume[other] < best
                        && ModelingComponentContains(points, edges, coedges, loops, faces, component, other, point))
                    { parent = other; best = volume[other]; }
                if (parent < 0) return ParasolidConstants.PK_ERROR_bad_loop;
                componentParent[f] = parent;
            }
            for (var root = 0; root < faces.Length; root++)
            {
                if (component[root] != root || componentParent[root] != root) continue;
                if (resultCount == results.Length) return ParasolidConstants.PK_ERROR_not_implemented;
                vertexMap.Fill(-1); edgeMap.Fill(-1);
                var nPoints = 0; var nEdges = 0; var nCoedges = 0; var nLoops = 0; var nFaces = 0;
                for (var f = 0; f < faces.Length; f++)
                {
                    if (componentParent[component[f]] != root) continue;
                    var descriptor = faces[f];
                    // A geometry entity cannot belong to two independent bodies.
                    // Split shared supports only when an earlier component has
                    // already consumed the original clone.
                    var surface = descriptor.Surface; var shared = false;
                    for (var previous = 0; previous < f; previous++)
                        if (faces[previous].Surface == surface && componentParent[component[previous]] != root) { shared = true; break; }
                    if (shared)
                    {
                        var existing = -1;
                        for (var j = 0; j < nFaces; j++) if (faces[sourceFaces[j]].Surface == surface) { existing = j; break; }
                        if (existing >= 0) descriptor.Surface = newFaces[existing].Surface;
                        else
                        {
                            var error = TryCloneEntityCore(surface, out var clone);
                            if (error != 0) return error;
                            descriptor.Surface = clone;
                        }
                    }
                    descriptor.FirstLoop = nLoops;
                    for (var l = faces[f].FirstLoop; l < faces[f].FirstLoop + faces[f].LoopCount; l++)
                    {
                        var loop = loops[l]; newLoops[nLoops++] = new ModelingLoop { FirstCoedge = nCoedges, CoedgeCount = loop.CoedgeCount };
                        for (var k = 0; k < loop.CoedgeCount; k++)
                        {
                            var coedge = coedges[loop.FirstCoedge + k]; var e = coedge.Edge;
                            if (edgeMap[e] < 0)
                            {
                                var edge = edges[e];
                                for (var end = 0; end < 2; end++)
                                {
                                    var v = end == 0 ? edge.StartVertex : edge.EndVertex;
                                    if (v >= 0 && vertexMap[v] < 0) { vertexMap[v] = nPoints; newPoints[nPoints++] = points[v]; }
                                }
                                edge.StartVertex = edge.StartVertex < 0 ? -1 : vertexMap[edge.StartVertex];
                                edge.EndVertex = edge.EndVertex < 0 ? -1 : vertexMap[edge.EndVertex];
                                edgeMap[e] = nEdges; newEdges[nEdges++] = edge;
                            }
                            coedge.Edge = edgeMap[e]; newCoedges[nCoedges++] = coedge;
                        }
                    }
                    sourceFaces[nFaces] = f; cavities[nFaces] = solid && volume[component[f]] < 0 ? (byte)1 : (byte)0;
                    newFaces[nFaces++] = descriptor;
                }
                var build = BuildModelingBrep(newPoints[..nPoints], newEdges[..nEdges], newCoedges[..nCoedges],
                    newLoops[..nLoops], newFaces[..nFaces], solid, out var body, productFaces[..nFaces], cavities[..nFaces]);
                if (build != 0) return build;
                results[resultCount++] = body;
                if(!lineage.IsEmpty)for(var f=0;f<nFaces;f++)
                {
                    var index=sourceFaces[f];if(index>=lineage.Length)return ParasolidConstants.PK_ERROR_not_implemented;
                    var original=sources[sourceIndices[index]].Source;
                    lineage[index]=new(){Product=Faces[productFaces[f]].Header.Tag,Original=Faces[original].Header.Tag,
                        Class=ParasolidConstants.PK_CLASS_face,Owner=Bodies[TopologyBody(PoolKind.Face,original)].Header.Tag};
                }
                if (!sourceProducts.IsEmpty)
                    for (var f = 0; f < nFaces; f++)
                    {
                        var source = sourceIndices[sourceFaces[f]];
                        if (source < sourceProducts.Length && sourceProducts[source] == 0)
                            sourceProducts[source] = Faces[productFaces[f]].Header.Tag;
                    }
            }
            return 0;
        }
        finally { scratch.Return(memory); }
    }

    private static int ComponentRoot(ReadOnlySpan<int> parents, int index)
    {
        while (parents[index] != index) index = parents[index];
        return index;
    }

    private static KernelVector3 ModelingLoopPoint(ReadOnlySpan<KernelVector3> points, ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> coedges, in ModelingLoop loop, int position)
    {
        var coedge = coedges[loop.FirstCoedge + position]; var edge = edges[coedge.Edge];
        return points[coedge.Forward != 0 ? edge.StartVertex : edge.EndVertex];
    }

    private static bool ModelingComponentContains(ReadOnlySpan<KernelVector3> points, ReadOnlySpan<ModelingEdge> edges,
        ReadOnlySpan<ModelingCoedge> coedges, ReadOnlySpan<ModelingLoop> loops, ReadOnlySpan<ModelingFace> faces,
        ReadOnlySpan<int> components, int component, in KernelVector3 point)
    {
        double angle = 0;
        for (var f = 0; f < faces.Length; f++) if (components[f] == component)
            for (var l = faces[f].FirstLoop; l < faces[f].FirstLoop + faces[f].LoopCount; l++)
            {
                var loop = loops[l]; var a = ProfileSubtract(ModelingLoopPoint(points, edges, coedges, loop, 0), point);
                var la = Math.Sqrt(ProfileDot(a, a));
                for (var k = 1; k + 1 < loop.CoedgeCount; k++)
                {
                    var b = ProfileSubtract(ModelingLoopPoint(points, edges, coedges, loop, k), point);
                    var c = ProfileSubtract(ModelingLoopPoint(points, edges, coedges, loop, k + 1), point);
                    var lb = Math.Sqrt(ProfileDot(b, b)); var lc = Math.Sqrt(ProfileDot(c, c));
                    angle += 2 * Math.Atan2(ProfileDot(a, ProfileCross(b, c)), la * lb * lc
                        + ProfileDot(a, b) * lc + ProfileDot(b, c) * la + ProfileDot(c, a) * lb);
                }
            }
        return Math.Abs(angle) > Math.Tau;
    }
}
