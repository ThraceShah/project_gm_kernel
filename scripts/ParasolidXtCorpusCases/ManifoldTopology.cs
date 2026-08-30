#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// These fixtures are deliberately small and classify the resulting topology
// with the typed ask APIs before transmit.  In particular, a vertex loop and
// a fin-ring loop are different legal states even when their parent face is
// otherwise identical.
var cases = new CorpusCaseSpec[]
{
    new(
        "topology.loop.outer.rectangle",
        "PK_BODY_create_sheet_rectangle + PK_LOOP_ask_type",
        "Planar sheet whose boundary loop is classified as an outer loop.",
        new[] { "topology", "topology/loop", "loop/outer", "body/sheet" },
        CreateOuterLoopSheet,
        new CorpusBodyCounts(1, 1, 1, 4, 4) { Loops = 1, Fins = 4 },
        typedAsk: AssertOuterLoopAndMultiFin,
        typeCoverage: new[] { "topology.loop.outer", "topology.loop.fin-count.multi", "topology.vertex.endpoint" }),
    new(
        "topology.edge.valence.zero.cylinder",
        "PK_BODY_create_solid_cyl + PK_EDGE_ask_vertices",
        "Solid cylinder with circular ring edges that have no endpoint vertices.",
        new[] { "topology", "topology/edge", "edge/valence-0", "edge/ring", "body/solid/cylinder" },
        CreateZeroValenceEdgeCylinder,
        new CorpusBodyCounts(2, 2, 3, 2, 0) { Loops = 4, Fins = 4 },
        typedAsk: AssertZeroValenceAndWinding,
        requiredSchemaNodes: new[] { "LOOP", "HALFEDGE" },
        requiredSchemaDependencies: new[] { "FACE->LOOP", "LOOP->HALFEDGE" },
        typeCoverage: new[] { "topology.edge.valence.0", "topology.loop.winding", "topology.loop.fin-count.one" }),
    new(
        "topology.face.inner-loop.block-cylinder-hole",
        "PK_BODY_boolean_2(PK_boolean_subtract)",
        "Solid block with a coaxial cylindrical through-hole, retaining inner face loops.",
        new[] { "topology", "topology/face", "topology/loop", "loop/inner", "face/hole", "body/solid" },
        CreateBooleanThroughHole,
        new CorpusBodyCounts(2, 2, 7, 14, 8) { Loops = 10, Fins = 28 },
        requiredSchemaNodes: new[] { "FACE", "LOOP", "HALFEDGE" },
        requiredSchemaDependencies: new[] { "FACE->LOOP", "LOOP->HALFEDGE" },
        typedAsk: AssertInnerAndWindingLoops,
        typeCoverage: new[] { "topology.face.inner-loop", "topology.loop.inner", "topology.loop.winding", "topology.region.multiple", "topology.shell.multiple" }),
    new(
        "topology.loop.wire.imprint-line",
        "PK_FACE_imprint_curve(PK_BCURVE)",
        "Planar sheet with an imprinted finite B-curve producing a two-fin wire loop.",
        new[] { "topology", "topology/face", "topology/loop", "loop/wire", "face/imprint", "body/sheet" },
        CreateImprintedWireLoop,
        new CorpusBodyCounts(1, 1, 1, 5, 6) { Loops = 2, Fins = 6 },
        parameters: "{\"surface\":\"sheet-rectangle(5,5)\",\"curve\":\"degree-1-polyline\",\"interval\":[0,1],\"endpointVertices\":2}",
        requiredSchemaNodes: new[] { "LOOP", "HALFEDGE", "B_CURVE" },
        requiredSchemaDependencies: new[] { "LOOP->HALFEDGE" },
        typedAsk: AssertWireLoop,
        typeCoverage: new[] { "topology.loop.wire", "topology.loop.fin-count.two" }),
    new(
        "topology.face.multiple-holes",
        "PK_BODY_boolean_2(PK_boolean_subtract) × 2",
        "Block with two disconnected cylindrical through-holes, retaining multiple inner loops.",
        new[] { "topology", "topology/face", "topology/loop", "loop/inner", "face/multiple-holes", "body/solid" },
        CreateBooleanMultipleHoles,
        typedAsk: AssertMultipleInnerLoops,
        typeCoverage: new[] { "topology.face.inner-loop", "topology.loop.inner", "topology.manifold.multiple-holes" }),
    new(
        "topology.loop.vertex.cone-apex",
        "PK_BODY_create_solid_cone + PK_LOOP_ask_type",
        "Zero-radius cone whose degenerate apex is represented by a vertex loop.",
        new[] { "topology", "topology/loop", "loop/vertex", "loop/degenerate", "body/solid/cone" },
        CreateVertexLoopCone,
        new CorpusBodyCounts(2, 2, 2, 1, 1) { Loops = 3, Fins = 2 },
        typedAsk: AssertVertexLoop,
        typeCoverage: new[] { "topology.loop.vertex", "topology.vertex.apex" }),
    new(
        "topology.edge.valence.two.rectangle",
        "PK_BODY_create_sheet_rectangle + PK_EDGE_ask_vertices",
        "Planar sheet boundary edge with two endpoint vertices.",
        new[] { "topology", "topology/edge", "edge/valence-2", "body/sheet" },
        CreateTwoValenceEdgeSheet,
        new CorpusBodyCounts(1, 1, 1, 4, 4) { Loops = 1, Fins = 4 },
        typedAsk: AssertTwoValenceEdge,
        typeCoverage: new[] { "topology.edge.valence.2" }),
    new(
        "topology.vertex.shared.rectangle",
        "PK_BODY_create_sheet_rectangle + PK_VERTEX_ask_oriented_edges",
        "Planar sheet vertex shared by two boundary edges.",
        new[] { "topology", "topology/vertex", "vertex/shared", "body/sheet" },
        CreateTwoValenceEdgeSheet,
        new CorpusBodyCounts(1, 1, 1, 4, 4) { Loops = 1, Fins = 4 },
        typedAsk: AssertSharedVertex,
        typeCoverage: new[] { "topology.vertex.shared" }),
    new(
        "topology.loop.ring-classification",
        "PK_BODY_create_sheet_rectangle + PK_FACE_euler_make_ring_loop + PK_LOOP_ask_type",
        "Diagnostic probe for loop classifications produced by a ring-loop Euler operation.",
        new[] { "topology", "topology/loop", "loop/ring", "body/sheet" },
        CreateRingLoopSheet,
        new CorpusBodyCounts(1, 1, 1, 5, 4) { Loops = 3, Fins = 6 },
        typedAsk: ReportRingTypes),
};

return ParasolidXtCorpusHost.RunGroup("manifold-topology", cases, args);

static unsafe PK_BODY_t CreateOuterLoopSheet()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle outer loop");
    return body;
}

static unsafe PK_BODY_t CreateZeroValenceEdgeCylinder()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cyl(2.0, 5.0, null, &body), "PK_BODY_create_solid_cyl zero valence edge");
    return body;
}

static unsafe PK_BODY_t CreateVertexLoopCone()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cone(0.0, 5.0, 0.25, null, &body), "PK_BODY_create_solid_cone one valence edge");
    return body;
}

static unsafe PK_BODY_t CreateTwoValenceEdgeSheet() => CreateOuterLoopSheet();

static unsafe PK_BODY_t CreateRingLoopSheet()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle ring classification");
    PK_LOOP_t ring;
    ParasolidXtCorpusHost.Check(PK_FACE_euler_make_ring_loop(FirstFace(body), &ring), "PK_FACE_euler_make_ring_loop classification");
    return body;
}


static unsafe void ReportRingTypes(PK_BODY_t body)
{
    int faceCount; PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces ring classification");
    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            int loopCount; PK_LOOP_t* loops;
            ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops ring classification");
            try { for (var j = 0; j < loopCount; j++) { PK_LOOP_type_t type; ParasolidXtCorpusHost.Check(PK_LOOP_ask_type(loops[j], &type), "PK_LOOP_ask_type ring classification"); Console.WriteLine($"ring-loop[{j}]={type}"); } }
            finally { if (loops is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free ring classification loops"); }
        }
    }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free ring classification faces"); }
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count; PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces manifold");
    try { if (count == 0) throw new InvalidOperationException("body has no face"); return faces[0]; }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free manifold faces"); }
}

static unsafe void AssertOuterLoopAndMultiFin(PK_BODY_t body)
{
    if (!ContainsLoopType(body, PK_LOOP_type_outer_c))
        throw new InvalidOperationException("sheet rectangle has no PK_LOOP_type_outer_c loop");
    if (!ContainsLoopWithFinCount(body, count => count >= 3))
        throw new InvalidOperationException("sheet rectangle has no multi-fin loop");
    if (!ContainsEdgeValence(body, 2))
        throw new InvalidOperationException("sheet rectangle has no endpoint vertex edge");
}

static unsafe void AssertZeroValenceEdge(PK_BODY_t body)
{
    if (!ContainsEdgeValence(body, 0))
        throw new InvalidOperationException("cylinder has no zero-valence edge");
}

static unsafe void AssertZeroValenceAndWinding(PK_BODY_t body)
{
    AssertZeroValenceEdge(body);
    if (!ContainsLoopType(body, PK_LOOP_type_winding_c))
        throw new InvalidOperationException("cylinder has no winding loop");
    if (!ContainsLoopWithFinCount(body, count => count == 1))
        throw new InvalidOperationException("cylinder has no one-fin winding loop");
}

static unsafe void AssertVertexLoop(PK_BODY_t body)
{
    if (!ContainsLoopType(body, PK_LOOP_type_vertex_c))
        throw new InvalidOperationException("cone apex has no vertex loop");
}

static unsafe void AssertTwoValenceEdge(PK_BODY_t body)
{
    if (!ContainsEdgeValence(body, 2))
        throw new InvalidOperationException("sheet rectangle has no two-valence edge");
}

static unsafe void AssertInnerAndWindingLoops(PK_BODY_t body)
{
    if (!ContainsLoopType(body, PK_LOOP_type_inner_c))
        throw new InvalidOperationException("boolean through-hole has no inner loop");
    if (!ContainsLoopType(body, PK_LOOP_type_winding_c))
        throw new InvalidOperationException("boolean through-hole has no winding loop");
    if (!ContainsRegionCount(body, 2) || !ContainsShellCount(body, 2))
        throw new InvalidOperationException("boolean through-hole has no multiple-region/multiple-shell topology");
}

static unsafe void AssertWireLoop(PK_BODY_t body)
{
    if (!ContainsLoopType(body, PK_LOOP_type_wire_c))
        throw new InvalidOperationException("imprinted line has no wire loop");
    if (!ContainsLoopWithFinCount(body, count => count == 2))
        throw new InvalidOperationException("imprinted line has no two-fin wire loop");
}


static unsafe void AssertSharedVertex(PK_BODY_t body)
{
    int vertexCount;
    PK_VERTEX_t* vertices;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_vertices(body, &vertexCount, &vertices), "PK_BODY_ask_vertices shared");
    try
    {
        for (var i = 0; i < vertexCount; i++)
        {
            int edgeCount;
            PK_EDGE_t* edges;
            PK_LOGICAL_t* orientations;
            ParasolidXtCorpusHost.Check(PK_VERTEX_ask_oriented_edges(vertices[i], &edgeCount, &edges, &orientations), "PK_VERTEX_ask_oriented_edges shared");
            try
            {
                if (edgeCount >= 2)
                    return;
            }
            finally
            {
                if (edges is not null)
                    ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free shared edges");
                if (orientations is not null)
                    ParasolidXtCorpusHost.Check(PK_MEMORY_free(orientations), "PK_MEMORY_free shared orientations");
            }
        }
    }
    finally
    {
        if (vertices is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(vertices), "PK_MEMORY_free shared vertices");
    }
    throw new InvalidOperationException("sheet rectangle has no vertex shared by two oriented edges");
}

static unsafe bool ContainsLoopType(PK_BODY_t body, PK_LOOP_type_t expected)
{
    int faceCount;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces loop type");
    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            int loopCount;
            PK_LOOP_t* loops;
            ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops loop type");
            try
            {
                for (var j = 0; j < loopCount; j++)
                {
                    PK_LOOP_type_t type;
                    ParasolidXtCorpusHost.Check(PK_LOOP_ask_type(loops[j], &type), "PK_LOOP_ask_type");
                    if (type == expected)
                        return true;
                }
            }
            finally
            {
                if (loops is not null)
                    ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free loop type loops");
            }
        }
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free loop type faces");
    }

    return false;
}

static unsafe bool ContainsEdgeValence(PK_BODY_t body, int expected)
{
    int edgeCount;
    PK_EDGE_t* edges;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_edges(body, &edgeCount, &edges), "PK_BODY_ask_edges valence");
    var vertices = stackalloc PK_VERTEX_t[2];
    try
    {
        for (var i = 0; i < edgeCount; i++)
        {
            vertices[0] = 0;
            vertices[1] = 0;
            ParasolidXtCorpusHost.Check(PK_EDGE_ask_vertices(edges[i], vertices), "PK_EDGE_ask_vertices valence");
            var count = (vertices[0] > 0 ? 1 : 0) + (vertices[1] > 0 ? 1 : 0);
            if (count == expected)
                return true;
        }
    }
    finally
    {
        if (edges is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free valence edges");
    }

    return false;
}

static unsafe bool ContainsLoopWithFinCount(PK_BODY_t body, Func<int, bool> predicate)
{
    int faceCount; PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces fin count");
    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            int loopCount; PK_LOOP_t* loops;
            ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops fin count");
            try
            {
                for (var j = 0; j < loopCount; j++)
                {
                    int finCount; PK_FIN_t* fins;
                    ParasolidXtCorpusHost.Check(PK_LOOP_ask_fins(loops[j], &finCount, &fins), "PK_LOOP_ask_fins fin count");
                    if (fins is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(fins), "PK_MEMORY_free fin count fins");
                    if (predicate(finCount)) return true;
                }
            }
            finally { if (loops is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free fin count loops"); }
        }
    }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free fin count faces"); }
    return false;
}

static unsafe bool ContainsRegionCount(PK_BODY_t body, int expected)
{
    int count; PK_REGION_t* regions;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_regions(body, &count, &regions), "PK_BODY_ask_regions topology cardinality");
    if (regions is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(regions), "PK_MEMORY_free topology regions");
    return count == expected;
}

static unsafe bool ContainsShellCount(PK_BODY_t body, int expected)
{
    int count; PK_SHELL_t* shells;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_shells(body, &count, &shells), "PK_BODY_ask_shells topology cardinality");
    if (shells is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(shells), "PK_MEMORY_free topology shells");
    return count == expected;
}

static unsafe PK_BODY_t CreateBooleanThroughHole()
{
    PK_BODY_t target;
    PK_BODY_t tool;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(10.0, 10.0, 10.0, null, &target), "PK_BODY_create_solid_block boolean hole");
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cyl(2.0, 10.0, null, &tool), "PK_BODY_create_solid_cyl boolean hole");
    var options = new PK_BODY_boolean_o_t { function = PK_boolean_subtract_c };
    var tracking = new PK_TOPOL_track_r_t();
    var results = new PK_boolean_r_t();
    ParasolidXtCorpusHost.Check(PK_BODY_boolean_2(target, 1, new[] { tool }, &options, &tracking, &results), "PK_BODY_boolean_2 subtract through-hole");
    try
    {
        if (results.n_bodies != 1 || results.bodies is null)
            throw new InvalidOperationException($"boolean through-hole returned {results.n_bodies} bodies");
        return results.bodies[0];
    }
    finally
    {
        ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f boolean hole");
        ParasolidXtCorpusHost.Check(PK_boolean_r_f(&results), "PK_boolean_r_f boolean hole");
    }
}

static unsafe PK_BODY_t CreateBooleanMultipleHoles()
{
    PK_BODY_t target;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(12.0, 10.0, 10.0, null, &target), "PK_BODY_create_solid_block multiple holes");
    for (var i = 0; i < 2; i++)
    {
        PK_BODY_t tool;
        ParasolidXtCorpusHost.Check(PK_BODY_create_solid_cyl(1.5, 10.0, null, &tool), "PK_BODY_create_solid_cyl multiple holes");
        PK_TRANSF_t transform;
        ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(i == 0 ? -3.0 : 3.0, 0.0, 0.0), &transform), "PK_TRANSF_create_translation multiple holes");
        int replaceCount; PK_GEOM_t* replaces; PK_LOGICAL_t* exact;
        ParasolidXtCorpusHost.Check(PK_BODY_transform(tool, transform, 1.0e-6, &replaceCount, &replaces, &exact), "PK_BODY_transform multiple holes");
        if (replaces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(replaces), "PK_MEMORY_free multiple hole replaces");
        if (exact is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(exact), "PK_MEMORY_free multiple hole exact");
        var options = new PK_BODY_boolean_o_t { function = PK_boolean_subtract_c };
        var tracking = new PK_TOPOL_track_r_t();
        var results = new PK_boolean_r_t();
        ParasolidXtCorpusHost.Check(PK_BODY_boolean_2(target, 1, new[] { tool }, &options, &tracking, &results), "PK_BODY_boolean_2 multiple holes");
        try
        {
            if (results.n_bodies != 1 || results.bodies is null)
                throw new InvalidOperationException("multiple-hole subtraction returned no body");
            target = results.bodies[0];
        }
        finally
        {
            ParasolidXtCorpusHost.Check(PK_TOPOL_track_r_f(&tracking), "PK_TOPOL_track_r_f multiple holes");
            ParasolidXtCorpusHost.Check(PK_boolean_r_f(&results), "PK_boolean_r_f multiple holes");
        }
    }
    return target;
}

static unsafe void AssertMultipleInnerLoops(PK_BODY_t body)
{
    int faceCount; PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces multiple holes");
    try
    {
        var innerLoops = 0;
        for (var i = 0; i < faceCount; i++)
        {
            int loopCount; PK_LOOP_t* loops;
            ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops multiple holes");
            try
            {
                for (var j = 0; j < loopCount; j++)
                {
                    PK_LOOP_type_t type;
                    ParasolidXtCorpusHost.Check(PK_LOOP_ask_type(loops[j], &type), "PK_LOOP_ask_type multiple holes");
                    if (type == PK_LOOP_type_inner_c) innerLoops++;
                }
            }
            finally { if (loops is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free multiple hole loops"); }
        }
        if (innerLoops < 2) throw new InvalidOperationException("multiple-hole body has fewer than two inner loops");
    }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free multiple hole faces"); }
}

static unsafe PK_BODY_t CreateImprintedWireLoop()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(5.0, 5.0, null, &body), "PK_BODY_create_sheet_rectangle imprinted wire");
    var face = FirstFace(body);
    var vertices = stackalloc double[] { -1.0, 0.0, 0.0, 1.0, 0.0, 0.0 };
    var knotMultiplicities = stackalloc int[] { 2, 2 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    var standardForm = new PK_BCURVE_sf_t(1, 2, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_polyline_c, 2, knotMultiplicities, knots, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c);
    PK_BCURVE_t curve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&standardForm, &curve), "PK_BCURVE_create imprinted wire");
    int edgeCount; PK_EDGE_t* edges = null; int faceCount; PK_FACE_t* faces = null;
    ParasolidXtCorpusHost.Check(PK_FACE_imprint_curve(face, curve, new PK_INTERVAL_t(0.0, 1.0), &edgeCount, &edges, &faceCount, &faces), "PK_FACE_imprint_curve wire loop");
    if (edges is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free imprinted wire edges");
    if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free imprinted wire faces");
    return body;
}
