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
        "topology.face.make-sheet-body",
        "PK_FACE_make_sheet_body + PK_BODY_create_solid_block",
        "Extract one planar solid face into an independent sheet body.",
        new[] { "topology", "face", "face/make-sheet-body", "body/sheet", "producer" },
        CreateSheetFromBlockFace,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"result\":\"sheet-body\"}"),
    new(
        "topology.face.imprint-point",
        "PK_FACE_imprint_point + PK_FACE_find_interior_vec + PK_POINT_create",
        "Imprint an interior point on a planar block face.",
        new[] { "topology", "face", "face/imprint-point", "point", "mutator" },
        CreateImprintedBlockFace,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"point\":\"face-interior\"}"),
    new(
        "topology.face.make-valid",
        "PK_FACE_make_valid_faces + PK_BODY_create_solid_block",
        "Run face validity repair on a valid block face; no replacement is needed when it is already valid.",
        new[] { "topology", "face", "face/make-valid", "validation", "producer" },
        CreateValidatedBlockFace,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"inputValid\":true}"),
    new(
        "topology.face.approximation-cycle",
        "PK_FACE_set_approx + PK_FACE_unset_approx + PK_BODY_create_solid_block",
        "Install and then clear an approximate representation on one block face.",
        new[] { "topology", "face", "face/approximation", "mutator", "lifecycle" },
        CreateFaceApproximationCycle,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"operation\":\"set-then-unset-approximation\"}"),
    new(
        "topology.face.identity-transform",
        "PK_FACE_transform + PK_TRANSF_create + PK_BODY_create_solid_block",
        "Apply an identity transform to one planar block face.",
        new[] { "topology", "face", "face/transform", "transform/identity", "mutator" },
        CreateIdentityTransformedFace,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"transform\":\"identity\",\"tolerance\":0.000001}"),
    new(
        "topology.face.identity-transform-2",
        "PK_FACE_transform_2 + PK_TRANSF_create + PK_BODY_create_solid_block",
        "Apply the option-bearing identity transform to one planar block face.",
        new[] { "topology", "face", "face/transform", "transform/identity", "mutator", "options" },
        CreateIdentityTransformedFace2,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"transform\":\"identity\",\"options\":\"default\",\"tolerance\":0.000001}"),
    new(
        "topology.face.offset-sheet",
        "PK_FACE_offset + PK_BODY_create_sheet_rectangle",
        "Offset the sole planar face of a sheet rectangle by a positive distance.",
        new[] { "topology", "face", "face/offset", "body/sheet", "mutator" },
        CreateOffsetSheetFace,
        null,
        null,
        "{\"source\":\"body.sheet.rectangle.typical\",\"faceIndex\":0,\"offset\":0.25,\"tolerance\":0.000001}"),
    new(
        "topology.face.offset-sheet-2",
        "PK_FACE_offset_2 + PK_BODY_create_sheet_rectangle",
        "Offset a planar sheet face through the option-bearing face offset API.",
        new[] { "topology", "face", "face/offset", "body/sheet", "mutator", "options" },
        CreateOffsetSheetFace2,
        null,
        null,
        "{\"source\":\"body.sheet.rectangle.typical\",\"faceIndex\":0,\"offset\":0.25,\"tolerance\":0.000001,\"options\":\"default\"}"),
    new(
        "topology.face.make-sheet-bodies",
        "PK_FACE_make_sheet_bodies + PK_BODY_create_solid_block",
        "Option-bearing conversion of one solid face into a sheet-body array.",
        new[] { "topology", "face", "face/make-sheet-bodies", "body/sheet", "producer" },
        CreateSheetBodiesFromFace,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"faceIndex\":0,\"allowDisjoint\":false,\"makeFrom\":\"copy\"}"),
    new(
        "topology.face.imprint-curve",
        "PK_FACE_imprint_curve + PK_LINE_create + PK_BODY_create_sheet_rectangle",
        "Imprint a finite analytic line across the sole planar sheet face.",
        new[] { "topology", "face", "face/imprint-curve", "curve/line", "body/sheet", "mutator" },
        CreateImprintedSheetCurve,
        null,
        null,
        "{\"source\":\"body.sheet.rectangle.typical\",\"line\":{\"axis\":[1.0,0.0,0.0],\"location\":[0.0,0.0,0.0]},\"interval\":[-0.75,0.75]}"),
    new(
        "topology.face.imprint-curves-2",
        "PK_FACE_imprint_curves_2 + PK_LINE_create + PK_BODY_create_sheet_rectangle",
        "Imprint one finite analytic line through the option-bearing face imprint API.",
        new[] { "topology", "face", "face/imprint-curve", "curve/line", "body/sheet", "mutator", "options" },
        CreateImprintedSheetCurve2,
        null,
        null,
        "{\"source\":\"body.sheet.rectangle.typical\",\"line\":{\"axis\":[1.0,0.0,0.0],\"location\":[0.0,0.0,0.0]},\"interval\":[-0.75,0.75],\"options\":\"default\"}"),
    new(
        "topology.edge.split-at-param",
        "PK_EDGE_split_at_param + PK_EDGE_find_interval + PK_BODY_create_solid_block",
        "Split one finite block edge at its parameter midpoint.",
        new[] { "topology", "edge", "edge/split", "vertex", "mutator" },
        CreateSplitBlockEdge,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"edgeIndex\":0,\"parameter\":\"interval-midpoint\"}"),
    new(
        "topology.edge.imprint-point",
        "PK_EDGE_imprint_point + PK_EDGE_ask_geometry + PK_POINT_create",
        "Imprint a midpoint point on one finite block edge.",
        new[] { "topology", "edge", "edge/imprint-point", "point", "mutator" },
        CreateImprintedBlockEdge,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"edgeIndex\":0,\"point\":\"edge-endpoint-midpoint\"}"),
    new(
        "topology.vertex.set-precision",
        "PK_VERTEX_set_precision + PK_BODY_create_solid_block",
        "Set an explicit precision on one vertex of a solid block.",
        new[] { "topology", "vertex", "vertex/precision", "mutator", "tolerance" },
        CreatePreciseBlockVertex,
        new CorpusBodyCounts(2, 2, 6, 12, 8),
        null,
        "{\"source\":\"body.solid.block.typical\",\"vertexIndex\":0,\"precision\":0.000001}"),
    new(
        "topology.loop.offset-planar",
        "PK_LOOP_offset_planar + PK_BODY_create_sheet_rectangle",
        "Offset the outer loop of a planar rectangular sheet by a finite distance.",
        new[] { "topology", "loop", "loop/offset-planar", "body/sheet", "producer" },
        CreateOffsetSheetLoop,
        null,
        null,
        "{\"source\":\"body.sheet.rectangle.typical\",\"offset\":0.25,\"result\":\"offset-loop\"}"),
};

return ParasolidXtCorpusHost.RunGroup("topology-operations", cases, args);

static unsafe PK_BODY_t CreateBlock()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &body),
        "PK_BODY_create_solid_block topology");
    return body;
}

static unsafe PK_BODY_t CreateSheet()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body),
        "PK_BODY_create_sheet_rectangle topology");
    return body;
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces topology");
    try
    {
        if (count == 0)
            throw new InvalidOperationException("body has no faces");
        return faces[0];
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free topology faces");
    }
}

static unsafe PK_EDGE_t FirstEdge(PK_BODY_t body)
{
    int count;
    PK_EDGE_t* edges;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_edges(body, &count, &edges), "PK_BODY_ask_edges topology");
    try
    {
        if (count == 0)
            throw new InvalidOperationException("body has no edges");
        return edges[0];
    }
    finally
    {
        if (edges is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free topology edges");
    }
}

static unsafe PK_LOOP_t FirstLoop(PK_BODY_t body)
{
    var face = FirstFace(body);
    int count;
    PK_LOOP_t* loops;
    ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(face, &count, &loops), "PK_FACE_ask_loops topology");
    try
    {
        if (count == 0)
            throw new InvalidOperationException("face has no loops");
        return loops[0];
    }
    finally
    {
        if (loops is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free topology loops");
    }
}

static unsafe PK_VERTEX_t FirstVertex(PK_BODY_t body)
{
    int count;
    PK_VERTEX_t* vertices;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_vertices(body, &count, &vertices), "PK_BODY_ask_vertices topology");
    try
    {
        if (count == 0)
            throw new InvalidOperationException("body has no vertices");
        return vertices[0];
    }
    finally
    {
        if (vertices is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(vertices), "PK_MEMORY_free topology vertices");
    }
}

static unsafe PK_BODY_t CreateSheetFromBlockFace()
{
    var source = CreateBlock();
    var face = FirstFace(source);
    var faces = stackalloc PK_FACE_t[1] { face };
    PK_BODY_t sheet;
    ParasolidXtCorpusHost.Check(PK_FACE_make_sheet_body(1, faces, &sheet), "PK_FACE_make_sheet_body");
    return sheet;
}

static unsafe PK_VECTOR_t FindFaceInterior(PK_FACE_t face)
{
    var options = new PK_FACE_find_interior_vec_o_t();
    PK_VECTOR_t position;
    PK_UV_t uv;
    ParasolidXtCorpusHost.Check(PK_FACE_find_interior_vec(face, &options, &position, &uv), "PK_FACE_find_interior_vec");
    return position;
}

static unsafe PK_BODY_t CreateImprintedBlockFace()
{
    var body = CreateBlock();
    var face = FirstFace(body);
    var position = FindFaceInterior(face);
    var pointForm = new PK_POINT_sf_t(position);
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &point), "PK_POINT_create face imprint");
    PK_VERTEX_t newVertex;
    ParasolidXtCorpusHost.Check(PK_FACE_imprint_point(face, point, &newVertex), "PK_FACE_imprint_point");
    return body;
}

static unsafe PK_BODY_t CreateValidatedBlockFace()
{
    var body = CreateBlock();
    var face = FirstFace(body);
    int nFaces;
    PK_FACE_t* newFaces;
    PK_LOGICAL_t succeeded;
    ParasolidXtCorpusHost.Check(
        PK_FACE_make_valid_faces(face, &nFaces, &newFaces, &succeeded),
        "PK_FACE_make_valid_faces");
    try
    {
        _ = nFaces;
        _ = succeeded;
        return body;
    }
    finally
    {
        if (newFaces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(newFaces), "PK_MEMORY_free valid faces");
    }
}

static unsafe PK_BODY_t CreateFaceApproximationCycle()
{
    var body = CreateBlock();
    var face = FirstFace(body);
    var faces = stackalloc PK_FACE_t[1] { face };
    ParasolidXtCorpusHost.Check(PK_FACE_set_approx(1, faces), "PK_FACE_set_approx");
    ParasolidXtCorpusHost.Check(PK_FACE_unset_approx(1, faces), "PK_FACE_unset_approx");
    return body;
}

static unsafe PK_BODY_t CreateIdentityTransformedFace()
{
    var body = CreateBlock();
    var face = FirstFace(body);
    var identity = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &transform), "PK_TRANSF_create face identity");
    var faces = stackalloc PK_FACE_t[1] { face };
    var transforms = stackalloc PK_TRANSF_t[1] { transform };
    int nReplaces;
    PK_GEOM_t* replaces;
    PK_LOGICAL_t* exact;
    var local = new PK_local_check_t();
    ParasolidXtCorpusHost.Check(
        PK_FACE_transform(1, faces, transforms, 0.000001, PK_LOGICAL_true, &nReplaces, &replaces, &exact, &local),
        "PK_FACE_transform");
    if (replaces is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(replaces), "PK_MEMORY_free face transform replaces");
    if (exact is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(exact), "PK_MEMORY_free face transform exact");
    return body;
}

static unsafe PK_BODY_t CreateIdentityTransformedFace2()
{
    var body = CreateBlock();
    var face = FirstFace(body);
    var identity = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &transform), "PK_TRANSF_create face identity 2");
    var faces = stackalloc PK_FACE_t[1] { face };
    var transforms = stackalloc PK_TRANSF_t[1] { transform };
    var options = new PK_FACE_transform_o_t();
    var tracking = new PK_TOPOL_track_r_t();
    var local = new PK_TOPOL_local_r_t(PK_local_status_ok_c, 0, null);
    ParasolidXtCorpusHost.Check(
        PK_FACE_transform_2(1, faces, transforms, 0.000001, &options, &tracking, &local),
        "PK_FACE_transform_2");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f face transform 2");
    ParasolidXtCorpusHost.Check(PK_TOPOL_local_r_f(&local), "PK_TOPOL_local_r_f face transform 2");
    return body;
}

static unsafe PK_BODY_t CreateOffsetSheetFace()
{
    var body = CreateSheet();
    var face = FirstFace(body);
    var faces = stackalloc PK_FACE_t[1] { face };
    var offsets = stackalloc double[1] { 0.25 };
    ParasolidXtCorpusHost.Check(PK_FACE_offset(1, faces, offsets, 0.000001, PK_LOGICAL_true), "PK_FACE_offset");
    return body;
}

static unsafe PK_BODY_t CreateOffsetSheetFace2()
{
    var body = CreateSheet();
    var face = FirstFace(body);
    var faces = stackalloc PK_FACE_t[1] { face };
    var offsets = stackalloc double[1] { 0.25 };
    var options = new PK_FACE_offset_o_t();
    var tracking = new PK_TOPOL_track_r_t();
    var local = new PK_TOPOL_local_r_t(PK_local_status_ok_c, 0, null);
    ParasolidXtCorpusHost.Check(
        PK_FACE_offset_2(1, faces, offsets, 0.000001, &options, &tracking, &local),
        "PK_FACE_offset_2");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f face offset 2");
    ParasolidXtCorpusHost.Check(PK_TOPOL_local_r_f(&local), "PK_TOPOL_local_r_f face offset 2");
    return body;
}

static unsafe PK_BODY_t CreateSheetBodiesFromFace()
{
    var source = CreateBlock();
    var face = FirstFace(source);
    var faces = stackalloc PK_FACE_t[1] { face };
    var options = new PK_FACE_make_sheet_bodies_o_t();
    int bodyCount;
    PK_BODY_t* bodies;
    var tracking = new PK_TOPOL_track_r_t();
    ParasolidXtCorpusHost.Check(PK_FACE_make_sheet_bodies(1, faces, &options, &bodyCount, &bodies, &tracking), "PK_FACE_make_sheet_bodies");
    ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f make sheet bodies");
    try
    {
        if (bodyCount <= 0)
            throw new InvalidOperationException("PK_FACE_make_sheet_bodies returned no bodies");
        return bodies[0];
    }
    finally
    {
        if (bodies is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(bodies), "PK_MEMORY_free sheet bodies");
    }
}

static unsafe PK_BODY_t CreateImprintedSheetCurve()
{
    var body = CreateSheet();
    var face = FirstFace(body);
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create face imprint");
    int nEdges;
    PK_EDGE_t* newEdges;
    int nFaces;
    PK_FACE_t* newFaces;
    ParasolidXtCorpusHost.Check(
        PK_FACE_imprint_curve(face, line, new PK_INTERVAL_t(-0.75, 0.75), &nEdges, &newEdges, &nFaces, &newFaces),
        "PK_FACE_imprint_curve");
    if (newEdges is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(newEdges), "PK_MEMORY_free face imprint edges");
    if (newFaces is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(newFaces), "PK_MEMORY_free face imprint faces");
    return body;
}

static unsafe PK_BODY_t CreateImprintedSheetCurve2()
{
    var body = CreateSheet();
    var face = FirstFace(body);
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create face imprint 2");
    var curves = stackalloc PK_CURVE_t[1] { line };
    var intervals = stackalloc PK_INTERVAL_t[1] { new PK_INTERVAL_t(-0.75, 0.75) };
    var options = new PK_FACE_imprint_curves_o_t();
    var tracking = new PK_ENTITY_track_r_t();
    ParasolidXtCorpusHost.Check(
        PK_FACE_imprint_curves_2(face, 1, curves, intervals, &options, &tracking),
        "PK_FACE_imprint_curves_2");
    ParasolidXtCorpusHost.Check(PK_ENTITY_track_r_f(&tracking), "PK_ENTITY_track_r_f face imprint curves");
    return body;
}


static unsafe PK_BODY_t CreateSplitBlockEdge()
{
    var body = CreateBlock();
    var edge = FirstEdge(body);
    PK_INTERVAL_t interval;
    ParasolidXtCorpusHost.Check(PK_EDGE_find_interval(edge, &interval), "PK_EDGE_find_interval");
    var parameter = (interval.value[0] + interval.value[1]) * 0.5;
    PK_VERTEX_t newVertex;
    PK_EDGE_t newEdge;
    ParasolidXtCorpusHost.Check(
        PK_EDGE_split_at_param(edge, parameter, &newVertex, &newEdge),
        "PK_EDGE_split_at_param");
    return body;
}

static unsafe void AskEdgeGeometry(PK_EDGE_t edge, out PK_VECTOR_t start, out PK_VECTOR_t end)
{
    PK_CURVE_t curve;
    PK_CLASS_t @class;
    var ends = stackalloc PK_VECTOR_t[2];
    PK_INTERVAL_t interval;
    PK_LOGICAL_t sense;
    ParasolidXtCorpusHost.Check(
        PK_EDGE_ask_geometry(edge, PK_LOGICAL_true, &curve, &@class, ends, &interval, &sense),
        "PK_EDGE_ask_geometry");
    start = ends[0];
    end = ends[1];
}

static unsafe PK_BODY_t CreateImprintedBlockEdge()
{
    var body = CreateBlock();
    var edge = FirstEdge(body);
    AskEdgeGeometry(edge, out var start, out var end);
    var point = new PK_VECTOR_t(
        (start.coord[0] + end.coord[0]) * 0.5,
        (start.coord[1] + end.coord[1]) * 0.5,
        (start.coord[2] + end.coord[2]) * 0.5);
    var pointForm = new PK_POINT_sf_t(point);
    PK_POINT_t pointEntity;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &pointEntity), "PK_POINT_create edge imprint");
    PK_VERTEX_t newVertex;
    PK_EDGE_t newEdge;
    ParasolidXtCorpusHost.Check(PK_EDGE_imprint_point(edge, pointEntity, &newVertex, &newEdge), "PK_EDGE_imprint_point");
    return body;
}

static unsafe PK_BODY_t CreatePreciseBlockVertex()
{
    var body = CreateBlock();
    var vertex = FirstVertex(body);
    ParasolidXtCorpusHost.Check(PK_VERTEX_set_precision(vertex, 0.000001), "PK_VERTEX_set_precision");
    return body;
}

static unsafe PK_BODY_t CreateOffsetSheetLoop()
{
    var body = CreateSheet();
    var loop = FirstLoop(body);
    var options = new PK_LOOP_offset_planar_o_t
    {
        gap_fill = PK_LOOP_opl_gap_fill_round_c,
        local_check = PK_LOGICAL_true,
        tolerance = 0.000001,
    };
    int nLoops;
    PK_LOOP_t* newLoops;
    var tracking = new PK_TOPOL_track_r_t();
    ParasolidXtCorpusHost.Check(
        PK_LOOP_offset_planar(loop, 0.25, &options, &nLoops, &newLoops, &tracking),
        "PK_LOOP_offset_planar");
    try
    {
        if (nLoops == 0)
            throw new InvalidOperationException("PK_LOOP_offset_planar returned no loops");
        return body;
    }
    finally
    {
        ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f loop offset");
        if (newLoops is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(newLoops), "PK_MEMORY_free offset loops");
    }
}
