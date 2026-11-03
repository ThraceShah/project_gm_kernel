using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal static unsafe partial class KernelRuntime
{
    private static int CreateFreeformSweepTracking(BodyTag profile, ReadOnlySpan<EdgeSlot> sourceEdges, EdgeSlot path,
        VertexSlot start, VertexSlot end, BodyTag product, PK_TOPOL_track_r_s* result)
    {
        var count = sourceEdges.Length + 2;
        var bytes = (nuint)(count * (sizeof(PK_TOPOL_track_record_r_s) + 5 * sizeof(EntityTag)));
        var records = (PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(bytes);
        if (records == null) return ParasolidConstants.PK_ERROR_memory_full;
        var originals = (EntityTag*)(records + count); var classes = originals + 2 * count; var products = classes + 2 * count;
        var face = Bodies[TagRec(product).Slot].FirstFaceBody;
        for (var i = 0; i < count; i++, face = Faces[face].NextInBody)
        {
            if (i < sourceEdges.Length)
            {
                originals[2 * i] = Edges[sourceEdges[i]].Header.Tag; originals[2 * i + 1] = Edges[path].Header.Tag;
                classes[2 * i] = classes[2 * i + 1] = ParasolidConstants.PK_CLASS_edge;
            }
            else
            {
                originals[2 * i] = Faces[Bodies[TagRec(profile).Slot].FirstFaceBody].Header.Tag;
                originals[2 * i + 1] = Vertices[i == sourceEdges.Length ? start : end].Header.Tag;
                classes[2 * i] = ParasolidConstants.PK_CLASS_face; classes[2 * i + 1] = ParasolidConstants.PK_CLASS_vertex;
            }
            products[i] = Faces[face].Header.Tag;
            records[i] = new PK_TOPOL_track_record_r_s { track = ParasolidConstants.PK_TOPOL_track_create_c,
                n_original_topols = 2, original_topols = originals + 2 * i, original_classes = classes + 2 * i,
                n_product_topols = 1, product_topols = products + i };
        }
        *result = new PK_TOPOL_track_r_s { n_track_records = count, track_records = records,
            internal_origs = originals, internal_classes = classes, internal_prods = products };
        return 0;
    }

    private static int CreateFreeformLoftTracking(ReadOnlySpan<BodyTag> profiles, ReadOnlySpan<EdgeSlot> sourceEdges,
        BufferCount edgeCount, BodyTag product, PK_TOPOL_track_r_s* result)
    {
        var recordsCount = edgeCount + 2; var originalsCount = profiles.Length * edgeCount + 2;
        var bytes = (nuint)(recordsCount * (sizeof(PK_TOPOL_track_record_r_s) + sizeof(EntityTag)) + originalsCount * 2 * sizeof(EntityTag));
        var records = (PK_TOPOL_track_record_r_s*)State.Session->Returns.TryAllocate(bytes);
        if (records == null) return ParasolidConstants.PK_ERROR_memory_full;
        var originals = (EntityTag*)(records + recordsCount); var classes = originals + originalsCount; var products = classes + originalsCount;
        var face = Bodies[TagRec(product).Slot].FirstFaceBody;
        for (var i = 0; i < edgeCount; i++, face = Faces[face].NextInBody)
        {
            var offset = i * profiles.Length; products[i] = Faces[face].Header.Tag;
            for (var p = 0; p < profiles.Length; p++)
            {
                originals[offset + p] = Edges[sourceEdges[p * 128 + i]].Header.Tag;
                classes[offset + p] = ParasolidConstants.PK_CLASS_edge;
            }
            records[i] = new PK_TOPOL_track_record_r_s { track = ParasolidConstants.PK_TOPOL_track_create_c,
                n_original_topols = profiles.Length, original_topols = originals + offset, original_classes = classes + offset,
                n_product_topols = 1, product_topols = products + i };
        }
        for (var cap = 0; cap < 2; cap++, face = Faces[face].NextInBody)
        {
            var index = edgeCount + cap; var offset = edgeCount * profiles.Length + cap;
            var source = Bodies[TagRec(profiles[cap == 0 ? 0 : profiles.Length - 1]).Slot].FirstFaceBody;
            originals[offset] = Faces[source].Header.Tag; classes[offset] = ParasolidConstants.PK_CLASS_face;
            products[index] = Faces[face].Header.Tag;
            records[index] = new PK_TOPOL_track_record_r_s { track = ParasolidConstants.PK_TOPOL_track_create_c,
                n_original_topols = 1, original_topols = originals + offset, original_classes = classes + offset,
                n_product_topols = 1, product_topols = products + index };
        }
        *result = new PK_TOPOL_track_r_s { n_track_records = recordsCount, track_records = records,
            internal_origs = originals, internal_classes = classes, internal_prods = products };
        return 0;
    }
}
