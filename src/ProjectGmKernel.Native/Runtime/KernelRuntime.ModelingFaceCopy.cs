using System.Runtime.InteropServices;
using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    /// <summary>Copy selected faces and their exact supports/curves into one
    /// independently owned manifold sheet. Shared edges and holes are retained.</summary>
    private static int CopyModelingFaces(ReadOnlySpan<FaceSlot> input, bool solid, out EntityTag body, Span<FaceSlot> products)
    {
        body = 0;
        if (input.IsEmpty || input.Length > 2048) return ParasolidConstants.PK_ERROR_bad_parameter;
        var finCount = 0; var loopCount = 0;
        foreach (var f in input)
        {
            var l = Faces[f].FirstLoop;
            for (var i = 0; i < Faces[f].LoopCount; i++, l = Loops[l].NextInFace)
            { finCount += Loops[l].FinCount; loopCount++; }
        }
        if (finCount > 16384) return ParasolidConstants.PK_ERROR_not_implemented;
        var scratch = CommandScratch.Current;
        var memory = scratch.Take((finCount * 120 + loopCount * 8 + input.Length * 40 + 7) / 8);
        if (memory.IsEmpty) return ParasolidConstants.PK_ERROR_memory_full;
        try
        {
            var bytes = MemoryMarshal.AsBytes(memory); var offset = 0;
            var vertices = ModelSpan<KernelVector3>(bytes, ref offset, 2 * finCount);
            var oldVertices = ModelSpan<VertexSlot>(bytes, ref offset, 2 * finCount);
            var edges = ModelSpan<ModelingEdge>(bytes, ref offset, finCount);
            var oldEdges = ModelSpan<EdgeSlot>(bytes, ref offset, finCount);
            var coedges = ModelSpan<ModelingCoedge>(bytes, ref offset, finCount);
            var loops = ModelSpan<ModelingLoop>(bytes, ref offset, loopCount);
            var faces = ModelSpan<ModelingFace>(bytes, ref offset, input.Length);
            var finCurves = ModelSpan<CurveTag>(bytes, ref offset, finCount);
            if (offset > bytes.Length) return ParasolidConstants.PK_ERROR_memory_full;
            var nv = 0; var ne = 0; var nc = 0; var nl = 0;
            for (var f = 0; f < input.Length; f++)
            {
                ref readonly var source = ref Faces[input[f]];
                faces[f] = new ModelingFace { Surface = source.SurfTag,
                    Sense = source.Orientation == ParasolidConstants.PK_TOPOL_sense_negative_c ? ParasolidConstants.PK_TOPOL_sense_negative_c : ParasolidConstants.PK_TOPOL_sense_positive_c,
                    FirstLoop = nl, LoopCount = source.LoopCount };
                var loop = source.FirstLoop;
                for (var l = 0; l < source.LoopCount; l++, loop = Loops[loop].NextInFace)
                {
                    loops[nl++] = new ModelingLoop { FirstCoedge = nc, CoedgeCount = Loops[loop].FinCount };
                    var fin = Loops[loop].FirstFin;
                    for (var k = 0; k < Loops[loop].FinCount; k++, fin = Fins[fin].NextInLoop)
                    {
                        if (Fins[fin].Edge < 0) return ParasolidConstants.PK_ERROR_not_implemented;
                        var original = Fins[fin].Edge; var e = oldEdges[..ne].IndexOf(original);
                        if (e < 0)
                        {
                            ref readonly var edge = ref Edges[original];
                            if (edge.CurveTag <= 0 || !TryProfileEdgeRange(in edge, out var start, out var end))
                                return ParasolidConstants.PK_ERROR_not_implemented;
                            var a = -1; var b = -1;
                            for (var side = 0; side < 2; side++)
                            {
                                var old = side == 0 ? edge.StartVertex : edge.EndVertex; var index = -1;
                                if (old >= 0)
                                {
                                    index = oldVertices[..nv].IndexOf(old);
                                    if (index < 0)
                                    {
                                        var point = GetPointSlotByTag(Vertices[old].PointTag);
                                        if (point < 0) return ParasolidConstants.PK_ERROR_missing_geom;
                                        index = nv; oldVertices[nv] = old;
                                        vertices[nv++] = Points[point].Position;
                                    }
                                }
                                if (side == 0) a = index; else b = index;
                            }
                            e = ne; oldEdges[ne] = original;
                            edges[ne++] = new ModelingEdge { Curve = edge.CurveTag, StartVertex = a, EndVertex = b, Start = start, End = end };
                        }
                        finCurves[nc] = Fins[fin].Curve;
                        coedges[nc++] = new ModelingCoedge { Edge = e, Forward = Fins[fin].Sense == '+' ? (byte)1 : (byte)0 };
                    }
                }
            }
            var sourceGeometry = ModelSpan<GeomTag>(bytes,ref offset,input.Length+ne+nc);
            var clonedGeometry = ModelSpan<GeomTag>(bytes,ref offset,input.Length+ne+nc);
            if(offset>bytes.Length)return ParasolidConstants.PK_ERROR_memory_full;
            var geometryCount=0;
            for(var f=0;f<input.Length;f++)sourceGeometry[geometryCount++]=faces[f].Surface;
            for(var e=0;e<ne;e++)sourceGeometry[geometryCount++]=edges[e].Curve;
            for(var c=0;c<nc;c++)if(finCurves[c]>0)sourceGeometry[geometryCount++]=finCurves[c];
            // One copy context preserves exact surface/curve/pcurve sharing.
            // In particular each fin's SP-curve uses the new incident support.
            var error=TryCloneEntitiesCore(sourceGeometry[..geometryCount],clonedGeometry[..geometryCount]);if(error!=0)return error;
            geometryCount=0;
            for(var f=0;f<input.Length;f++)faces[f].Surface=clonedGeometry[geometryCount++];
            for(var e=0;e<ne;e++)edges[e].Curve=clonedGeometry[geometryCount++];
            for(var c=0;c<nc;c++)if(finCurves[c]>0)finCurves[c]=clonedGeometry[geometryCount++];
            Span<EdgeSlot> edgeProducts=stackalloc EdgeSlot[ne];
            error=BuildModelingBrep(vertices[..nv],edges[..ne],coedges[..nc],loops[..nl],faces,solid,out body,products,edgeProducts:edgeProducts);if(error!=0)return error;
            var target=TagRec(body).Slot;var vertex=Bodies[target].FirstVertexBody;
            for(var v=0;v<nv;v++,vertex=Vertices[vertex].NextInBody)Vertices[vertex].Tolerance=Vertices[oldVertices[v]].Tolerance;
            for(var e=0;e<ne;e++)Edges[edgeProducts[e]].Tolerance=Edges[oldEdges[e]].Tolerance;
            for(var f=0;f<input.Length;f++)
            {
                var targetLoop=Faces[products[f]].FirstLoop;
                for(var l=faces[f].FirstLoop;l<faces[f].FirstLoop+faces[f].LoopCount;l++,targetLoop=Loops[targetLoop].NextInFace)
                {
                    var fin=Loops[targetLoop].FirstFin;
                    for(var c=loops[l].FirstCoedge;c<loops[l].FirstCoedge+loops[l].CoedgeCount;c++,fin=Fins[fin].NextInLoop)
                    {
                        if(finCurves[c]<=0)continue;
                        error=AcquireFinCurveReference(fin,finCurves[c]);if(error!=0)return error;
                        error=TryAdoptDependentGeometry(finCurves[c]);if(error!=0)return error;
                    }
                }
            }
            RebuildBoundaryGeometryLinks(target,resetOwners:true);return 0;
        }
        finally { scratch.Return(memory); }
    }

    private static int PreservePlanarFaceHandles(ReadOnlySpan<PlanarModelFace> sources, Span<FaceTag> products,
        ReadOnlySpan<EntityTag> originalBodies, ReadOnlySpan<EntityTag> resultBodies)
    {
        Span<FaceSlot> originals = stackalloc FaceSlot[sources.Length];
        Span<FaceSlot> replacements = stackalloc FaceSlot[sources.Length];
        Span<int> indices = stackalloc int[sources.Length];
        for (var b = 0; b < originalBodies.Length; b++)
        {
            var sourceBody = TagRec(originalBodies[b]).Slot;
            foreach (var result in resultBodies)
            {
                var targetBody = TagRec(result).Slot; var count = 0;
                for (var f = 0; f < sources.Length; f++)
                {
                    if (sources[f].Body != b || sources[f].Source < 0 || products[f] == 0) continue;
                    var replacement = TagRec(products[f]).Slot;
                    if (Shells[Faces[replacement].BackShell].Body != targetBody) continue;
                    originals[count] = sources[f].Source; replacements[count] = replacement; indices[count++] = f;
                }
                if (count == 0) continue;
                var error = PreserveModelingFaceHandles(sourceBody, targetBody, originals[..count], replacements[..count]);
                if (error != 0) return error;
                for (var i = 0; i < count; i++) products[indices[i]] = Faces[replacements[i]].Header.Tag;
            }
        }
        return 0;
    }
}
