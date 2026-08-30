#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

var cases = new CorpusCaseSpec[]
{
    new(
        "mutator.body.rotation",
        "PK_TRANSF_create_rotation + PK_BODY_transform",
        "Solid block rotated around a finite axis through the origin.",
        new[] { "mutator", "body", "transform", "rotation", "persistent-state" },
        CreateRotatedBlock),
    new(
        "mutator.body.equal-scale",
        "PK_TRANSF_create_equal_scale + PK_BODY_transform",
        "Solid block uniformly scaled about a fixed centre.",
        new[] { "mutator", "body", "transform", "equal-scale", "persistent-state" },
        CreateScaledBlock),
    new(
        "mutator.body.composed-transform",
        "PK_TRANSF_transform + PK_BODY_transform",
        "Solid block transformed by a composed translation and rotation transform.",
        new[] { "mutator", "body", "transform", "composition", "persistent-state" },
        CreateComposedTransformBlock),
    new(
        "mutator.body.copy-topology",
        "PK_BODY_copy_topology",
        "Independent topology copy of a solid block with default copy options.",
        new[] { "mutator", "body", "copy", "topology", "persistent-state" },
        CreateCopiedBlock),
    new(
        "mutator.body.make-manifold",
        "PK_BODY_make_manifold_bodies",
        "Manifold decomposition of a single solid block body.",
        new[] { "mutator", "body", "manifold", "decomposition", "persistent-state" },
        CreateManifoldBlock),
    new(
        "mutator.body.entity-copy",
        "PK_ENTITY_copy",
        "Generic entity copy of a solid block, retained as an independent body.",
        new[] { "mutator", "entity", "copy", "body", "persistent-state" },
        CreateEntityCopiedBlock),
    new(
        "mutator.body.entity-copy-2",
        "PK_ENTITY_copy_2",
        "Option-bearing generic entity copy of a solid block.",
        new[] { "mutator", "entity", "copy", "options", "body" },
        CreateEntityCopiedBlock2),
    new(
        "mutator.body.transform-options",
        "PK_BODY_transform_2 + PK_TRANSF_create_translation",
        "Option-bearing body transform with explicit face-merge and scaled-check policy.",
        new[] { "mutator", "body", "transform", "options", "persistent-state" },
        CreateOptionTransformedBlock),
    new(
        "mutator.body.transform-options-2",
        "PK_TRANSF_transform_2 + PK_BODY_transform",
        "Option-bearing transform composition applied to a solid block.",
        new[] { "mutator", "body", "transform", "options", "composition", "persistent-state" },
        CreateOptionComposedTransformBlock),
    new(
        "mutator.sheet.reverse-orientation",
        "PK_BODY_reverse_orientation + PK_BODY_create_sheet_rectangle",
        "Orientation reversal of a planar sheet body.",
        new[] { "mutator", "sheet-body", "reverse-orientation", "persistent-state" },
        CreateReversedSheet),
    new(
        "mutator.sheet.face-transform",
        "PK_FACE_transform + PK_TRANSF_create_translation",
        "Translate the single face of a planar sheet with the face transform API.",
        new[] { "mutator", "face", "sheet-body", "transform", "translation" },
        CreateTransformedSheetFace),
    new(
        "mutator.wire.edge-reverse",
        "PK_EDGE_reverse + PK_CURVE_make_wire_body",
        "Reverse the orientation of the sole edge in a finite wire body.",
        new[] { "mutator", "edge", "wire-body", "reverse" },
        CreateReversedWireEdge),
    new(
        "mutator.wire.edge-reverse-2",
        "PK_EDGE_reverse_2 + PK_CURVE_make_wire_body",
        "Option-bearing edge reversal on a finite wire body.",
        new[] { "mutator", "edge", "wire-body", "reverse", "options" },
        CreateReversedWireEdge2),
    new(
        "mutator.wire.edge-make-wire-body",
        "PK_EDGE_make_wire_body + PK_CURVE_make_wire_body",
        "Repackage a finite wire edge as a new wire body with default edge options.",
        new[] { "mutator", "edge", "wire-body", "repackage" },
        CreateWireBodyFromEdge),
    new(
        "mutator.wire.edge-delete-wireframe",
        "PK_EDGE_delete_wireframe + PK_CURVE_make_wire_body",
        "Delete the edge wireframe from a finite wire body while retaining the body container.",
        new[] { "mutator", "edge", "wire-body", "delete", "wireframe" },
        CreateWireBodyAfterEdgeDelete),
    new(
        "mutator.wire.edge-delete",
        "PK_EDGE_delete + PK_CURVE_make_wire_body",
        "Topology-aware deletion of the sole edge in a finite wire body.",
        new[] { "mutator", "edge", "wire-body", "delete", "topology" },
        CreateWireBodyAfterTopologyDelete),
    new(
        "mutator.body.translation",
        "PK_BODY_transform + PK_TRANSF_create_translation",
        "Solid block translated by a finite vector transform.",
        new[] { "mutator", "body", "transform", "translation", "persistent-state" },
        CreateTranslatedBlock,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"translation\":[3.0,-2.0,1.0],\"tolerance\":0.000001}"),
};

return ParasolidXtCorpusHost.RunGroup("mutators", cases, args);

static unsafe PK_BODY_t CreateTranslatedBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(transform)");
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(
        PK_TRANSF_create_translation(new PK_VECTOR_t(3.0, -2.0, 1.0), &transform),
        "PK_TRANSF_create_translation");
    int replaceCount;
    PK_GEOM_t* replaces;
    PK_LOGICAL_t* exact;
    ParasolidXtCorpusHost.Check(
        PK_BODY_transform(body, transform, 0.000001, &replaceCount, &replaces, &exact),
        "PK_BODY_transform");
    if (replaces is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(replaces), "PK_MEMORY_free transform replaces");
    if (exact is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(exact), "PK_MEMORY_free transform exact");
    return body;
}

static unsafe PK_BODY_t CreateRotatedBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(rotation)");
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(
        PK_TRANSF_create_rotation(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), 0.5, &transform),
        "PK_TRANSF_create_rotation");
    ApplyTransform(body, transform, "rotation");
    return body;
}

static unsafe PK_BODY_t CreateScaledBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(scale)");
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(
        PK_TRANSF_create_equal_scale(1.5, new PK_VECTOR_t(0.5, 0.5, 0.5), &transform),
        "PK_TRANSF_create_equal_scale");
    ApplyTransform(body, transform, "equal scale");
    return body;
}

static unsafe PK_BODY_t CreateComposedTransformBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(composed)");
    PK_TRANSF_t translation;
    PK_TRANSF_t rotation;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(1.0, -1.0, 0.5), &translation), "PK_TRANSF_create_translation composed");
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_rotation(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), 0.25, &rotation), "PK_TRANSF_create_rotation composed");
    PK_TRANSF_t composed;
    ParasolidXtCorpusHost.Check(PK_TRANSF_transform(translation, rotation, &composed), "PK_TRANSF_transform");
    ApplyTransform(body, composed, "composed");
    return body;
}

static unsafe PK_BODY_t CreateCopiedBlock()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block(copy)");
    var options = new PK_BODY_copy_topology_o_t();
    PK_BODY_t copy;
    var tracking = new PK_TOPOL_track_r_t();
    ParasolidXtCorpusHost.Check(PK_BODY_copy_topology(source, &options, &copy, &tracking), "PK_BODY_copy_topology");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f copy");
    return copy;
}

static unsafe PK_BODY_t CreateManifoldBlock()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block(manifold)");
    int count;
    PK_BODY_t* components;
    ParasolidXtCorpusHost.Check(PK_BODY_make_manifold_bodies(source, &count, &components), "PK_BODY_make_manifold_bodies");
    try
    {
        if (count != 1)
            throw new InvalidOperationException("expected one manifold component, got " + count);
        return components[0];
    }
    finally
    {
        if (components is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(components), "PK_MEMORY_free manifold components");
    }
}

static unsafe PK_BODY_t CreateEntityCopiedBlock()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block(entity copy)");
    PK_ENTITY_t entityCopy;
    ParasolidXtCorpusHost.Check(PK_ENTITY_copy(source, &entityCopy), "PK_ENTITY_copy");
    return entityCopy;
}

static unsafe PK_BODY_t CreateEntityCopiedBlock2()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block(entity copy 2)");
    var options = new PK_ENTITY_copy_o_t();
    PK_ENTITY_t entityCopy;
    var tracking = new PK_ENTITY_track_r_t();
    ParasolidXtCorpusHost.Check(PK_ENTITY_copy_2(source, &options, &entityCopy, &tracking), "PK_ENTITY_copy_2");
    ParasolidXtCorpusHost.Check(PK_ENTITY_track_r_f(&tracking), "PK_ENTITY_track_r_f entity copy 2");
    return entityCopy;
}

static unsafe PK_BODY_t CreateOptionTransformedBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(transform options)");
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(0.5, 0.25, -0.75), &transform), "PK_TRANSF_create_translation options");
    var options = new PK_BODY_transform_o_t
    {
        merge_face = PK_LOGICAL_true,
        check_fa_fa = PK_check_fa_fa_yes_c,
        update = PK_local_ops_update_default_c,
        check_scaled = PK_check_scaled_none_c,
    };
    var tracking = new PK_TOPOL_track_r_t();
    var local = new PK_TOPOL_local_r_t(PK_local_status_ok_c, 0, null);
    ParasolidXtCorpusHost.Check(PK_BODY_transform_2(body, transform, 0.000001, &options, &tracking, &local), "PK_BODY_transform_2");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f transform options");
    ParasolidXtCorpusHost.Check(PK_TOPOL_local_r_f(&local), "PK_TOPOL_local_r_f transform options");
    return body;
}

static unsafe PK_BODY_t CreateReversedSheet()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle reverse");
    ParasolidXtCorpusHost.Check(PK_BODY_reverse_orientation(body), "PK_BODY_reverse_orientation");
    return body;
}

static unsafe PK_BODY_t CreateReversedWireEdge()
{
    var body = CreateLineWireBody(out var edge);
    ParasolidXtCorpusHost.Check(PK_EDGE_reverse(edge), "PK_EDGE_reverse");
    return body;
}

static unsafe PK_BODY_t CreateTransformedSheetFace()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle face transform");
    int faceCount;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces face transform");
    try
    {
        var transforms = stackalloc PK_TRANSF_t[1];
        ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(0.5, 0.25, 0.0), transforms), "PK_TRANSF_create_translation face transform");
        int replaceCount;
        PK_GEOM_t* replaces;
        PK_LOGICAL_t* exact;
        PK_local_check_t checkResult;
        ParasolidXtCorpusHost.Check(PK_FACE_transform(1, faces, transforms, 0.000001, PK_LOGICAL_true, &replaceCount, &replaces, &exact, &checkResult), "PK_FACE_transform");
        if (replaces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(replaces), "PK_MEMORY_free face transform replaces");
        if (exact is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(exact), "PK_MEMORY_free face transform exact");
        return body;
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free face transform");
    }
}



static unsafe PK_BODY_t CreateReversedWireEdge2()
{
    var body = CreateLineWireBody(out var edge);
    var edges = stackalloc PK_EDGE_t[1] { edge };
    var options = new PK_EDGE_reverse_2_o_t();
    ParasolidXtCorpusHost.Check(PK_EDGE_reverse_2(1, edges, &options), "PK_EDGE_reverse_2");
    return body;
}

static unsafe PK_BODY_t CreateLineWireBody(out PK_EDGE_t edge)
{
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(-1.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create edge reverse");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(line, new PK_INTERVAL_t(-1.0, 1.0), &body), "PK_CURVE_make_wire_body edge reverse");
    int edgeCount;
    PK_EDGE_t* edges;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_edges(body, &edgeCount, &edges), "PK_BODY_ask_edges edge reverse");
    try
    {
        if (edgeCount != 1)
            throw new InvalidOperationException("expected one wire edge, got " + edgeCount);
        edge = edges[0];
        return body;
    }
    finally
    {
        if (edges is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free edge reverse");
    }
}

static unsafe PK_BODY_t CreateWireBodyFromEdge()
{
    _ = CreateLineWireBody(out var edge);
    var edges = stackalloc PK_EDGE_t[1] { edge };
    var options = new PK_EDGE_make_wire_body_o_t();
    PK_BODY_t body;
    var tracking = new PK_TOPOL_track_r_t();
    ParasolidXtCorpusHost.Check(PK_EDGE_make_wire_body(1, edges, &options, &body, &tracking), "PK_EDGE_make_wire_body");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f edge wire");
    return body;
}

static unsafe PK_BODY_t CreateWireBodyAfterEdgeDelete()
{
    var body = CreateLineWireBody(out var edge);
    var edges = stackalloc PK_EDGE_t[1] { edge };
    ParasolidXtCorpusHost.Check(PK_EDGE_delete_wireframe(1, edges), "PK_EDGE_delete_wireframe");
    return body;
}

static unsafe PK_BODY_t CreateWireBodyAfterTopologyDelete()
{
    var body = CreateLineWireBody(out var edge);
    var edges = stackalloc PK_EDGE_t[1] { edge };
    var options = new PK_EDGE_delete_o_t();
    var tracking = new PK_TOPOL_track_r_t();
    var local = new PK_TOPOL_local_r_t(PK_local_status_ok_c, 0, null);
    ParasolidXtCorpusHost.Check(PK_EDGE_delete(1, edges, &options, &tracking, &local), "PK_EDGE_delete");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f edge delete");
    ParasolidXtCorpusHost.Check(PK_TOPOL_local_r_f(&local), "PK_TOPOL_local_r_f edge delete");
    return body;
}


static unsafe PK_BODY_t CreateOptionComposedTransformBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body), "PK_BODY_create_solid_block(transform 2)");
    PK_TRANSF_t translation;
    PK_TRANSF_t rotation;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(0.5, 0.0, 0.25), &translation), "PK_TRANSF_create_translation transform 2");
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_rotation(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), 0.125, &rotation), "PK_TRANSF_create_rotation transform 2");
    var options = new PK_TRANSF_transform_o_t();
    var results = new PK_TRANSF_transform_r_t();
    ParasolidXtCorpusHost.Check(PK_TRANSF_transform_2(translation, rotation, &options, &results), "PK_TRANSF_transform_2");
    try
    {
        ApplyTransform(body, results.transf_out, "transform 2");
        return body;
    }
    finally
    {
        ParasolidXtCorpusHost.Check(PK_TRANSF_transform_r_f(&results), "PK_TRANSF_transform_r_f");
    }
}


static unsafe void ApplyTransform(PK_BODY_t body, PK_TRANSF_t transform, string label)
{
    int replaceCount;
    PK_GEOM_t* replaces;
    PK_LOGICAL_t* exact;
    ParasolidXtCorpusHost.Check(
        PK_BODY_transform(body, transform, 0.000001, &replaceCount, &replaces, &exact),
        "PK_BODY_transform " + label);
    if (replaces is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(replaces), "PK_MEMORY_free " + label + " replaces");
    if (exact is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(exact), "PK_MEMORY_free " + label + " exact");
}
