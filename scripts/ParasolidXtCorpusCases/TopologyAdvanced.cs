#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// These cases exercise public Euler/topology mutators on a stable analytic
// sheet seed.  Every mutator is isolated in its own case, so each output x_t
// has one independently auditable topology change.
var cases = new CorpusCaseSpec[]
{
    new(
        "topology.advanced.face-ring-loop",
        "PK_BODY_create_sheet_rectangle + PK_FACE_euler_make_ring_loop",
        "Sheet face with a second boundary loop made by the face Euler ring-loop operation.",
        new[] { "topology", "topology/face", "topology/loop", "topology/multi-loop", "loop/ring", "body/sheet", "face/sheet" },
        CreateFaceRingLoop,
        new CorpusBodyCounts(1, 1, 1, 5, 4) { Loops = 3, Fins = 6 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_FACE_euler_make_ring_loop\",\"newLoop\":\"ring\"}"),
    new(
        "topology.advanced.face-ring-face",
        "PK_BODY_create_sheet_rectangle + PK_FACE_euler_make_ring_face",
        "Sheet body with a second face made by the face Euler ring-face operation.",
        new[] { "topology", "topology/face", "topology/loop", "topology/multi-face", "face/ring", "body/sheet", "face/sheet" },
        CreateFaceRingFace,
        new CorpusBodyCounts(1, 1, 2, 5, 4) { Loops = 3, Fins = 6 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_FACE_euler_make_ring_face\",\"newFace\":\"ring\"}"),
    new(
        "topology.advanced.edge-split",
        "PK_BODY_create_sheet_rectangle + PK_EDGE_euler_split",
        "Sheet boundary edge split at its forward vertex, introducing a shared intermediate vertex.",
        new[] { "topology", "topology/edge", "topology/vertex", "topology/shared", "edge/split", "vertex/edge-owned", "body/sheet" },
        CreateEdgeSplit,
        new CorpusBodyCounts(1, 1, 1, 5, 5) { Loops = 1, Fins = 5 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_EDGE_euler_split\",\"forward\":true}"),
    new(
        "topology.advanced.edge-make-curve",
        "PK_BODY_create_sheet_rectangle + PK_EDGE_make_curve",
        "Sheet boundary edge chain processed by the edge make-curve topology/geometry mutator.",
        new[] { "topology", "topology/edge", "topology/curve", "edge/curve-replacement", "curve/continuous", "body/sheet" },
        CreateEdgeMakeCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4) { Loops = 1, Fins = 4 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_EDGE_make_curve\",\"edgeCount\":1,\"tolerance\":0.00001,\"curveDirection\":\"none\"}"),
    new(
        "topology.advanced.loop-make-edge",
        "PK_BODY_create_sheet_rectangle + PK_LOOP_euler_make_edge",
        "Sheet boundary loop extended with an Euler edge and its new vertex.",
        new[] { "topology", "topology/loop", "topology/edge", "topology/vertex", "loop/make-edge", "edge/new", "vertex/new", "body/sheet" },
        CreateLoopMakeEdge,
        new CorpusBodyCounts(1, 1, 1, 5, 5) { Loops = 1, Fins = 6 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_LOOP_euler_make_edge\",\"finIndex\":0}"),
    new(
        "topology.advanced.loop-make-edge-face",
        "PK_BODY_create_sheet_rectangle + PK_LOOP_euler_make_edge_face",
        "Sheet loop joined across two non-adjacent fins to create a second face and shared edge.",
        new[] { "topology", "topology/loop", "topology/edge", "topology/face", "topology/shared", "loop/make-edge-face", "edge/shared", "face/new", "body/sheet" },
        CreateLoopMakeEdgeFace,
        new CorpusBodyCounts(1, 1, 2, 5, 4) { Loops = 2, Fins = 6 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_LOOP_euler_make_edge_face\",\"finIndices\":[0,2]}"),
    new(
        "topology.advanced.loop-make-edge-loop",
        "PK_BODY_create_sheet_rectangle + PK_LOOP_euler_make_edge_loop",
        "Sheet loop joined across two non-adjacent fins to create a second loop and shared edge.",
        new[] { "topology", "topology/loop", "topology/edge", "topology/shared", "loop/make-edge-loop", "edge/shared", "body/sheet" },
        CreateLoopMakeEdgeLoop,
        new CorpusBodyCounts(1, 1, 1, 5, 4) { Loops = 2, Fins = 6 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_LOOP_euler_make_edge_loop\",\"finIndices\":[0,2]}",
        typedAsk: ReportAllLoopTypes),
    new(
        "topology.advanced.edge-set-precision",
        "PK_BODY_create_sheet_rectangle + PK_EDGE_set_precision_2",
        "Sheet boundary edge precision updated with the default precision method.",
        new[] { "topology", "topology/edge", "edge/precision", "edge/tolerance", "body/sheet" },
        CreateEdgeSetPrecision,
        new CorpusBodyCounts(1, 1, 1, 4, 4) { Loops = 1, Fins = 4 },
        null,
        "{\"source\":\"PK_BODY_create_sheet_rectangle(2,3)\",\"operation\":\"PK_EDGE_set_precision_2\",\"precision\":0.0001,\"method\":\"default\",\"reportShortEdges\":false}"),
};

return ParasolidXtCorpusHost.RunGroup("topology-advanced", cases, args);

static unsafe PK_BODY_t CreateSheet()
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(
        PK_BODY_create_sheet_rectangle(2.0, 3.0, null, &body),
        "PK_BODY_create_sheet_rectangle(topology advanced)");
    return body;
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces(topology advanced)");
    try
    {
        if (count < 1)
            throw new InvalidOperationException("sheet body has no face");
        return faces[0];
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free(faces)");
    }
}

static unsafe PK_EDGE_t FirstEdge(PK_BODY_t body)
{
    int count;
    PK_EDGE_t* edges;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_edges(body, &count, &edges), "PK_BODY_ask_edges(topology advanced)");
    try
    {
        if (count < 1)
            throw new InvalidOperationException("sheet body has no edge");
        return edges[0];
    }
    finally
    {
        if (edges is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free(edges)");
    }
}

static unsafe PK_LOOP_t FirstLoop(PK_BODY_t body)
{
    var face = FirstFace(body);
    int count;
    PK_LOOP_t* loops;
    ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(face, &count, &loops), "PK_FACE_ask_loops(topology advanced)");
    try
    {
        if (count < 1)
            throw new InvalidOperationException("sheet face has no loop");
        return loops[0];
    }
    finally
    {
        if (loops is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free(loops)");
    }
}

static unsafe void ReportAllLoopTypes(PK_BODY_t body)
{
    int faceCount; PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces loop type probe");
    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            int loopCount; PK_LOOP_t* loops;
            ParasolidXtCorpusHost.Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "PK_FACE_ask_loops loop type probe");
            try { for (var j = 0; j < loopCount; j++) { PK_LOOP_type_t type; ParasolidXtCorpusHost.Check(PK_LOOP_ask_type(loops[j], &type), "PK_LOOP_ask_type loop type probe"); Console.WriteLine($"edge-loop operation face={i} loop={j} type={type}"); } }
            finally { if (loops is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(loops), "PK_MEMORY_free loop type probe loops"); }
        }
    }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free loop type probe faces"); }
}

static unsafe PK_FIN_t LoopFin(PK_LOOP_t loop, int index)
{
    int count;
    PK_FIN_t* fins;
    ParasolidXtCorpusHost.Check(PK_LOOP_ask_fins(loop, &count, &fins), "PK_LOOP_ask_fins(topology advanced)");
    try
    {
        if ((uint)index >= (uint)count)
            throw new InvalidOperationException("loop has too few fins");
        return fins[index];
    }
    finally
    {
        if (fins is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(fins), "PK_MEMORY_free(fins)");
    }
}

static unsafe PK_BODY_t CreateFaceRingLoop()
{
    var body = CreateSheet();
    PK_LOOP_t loop;
    ParasolidXtCorpusHost.Check(PK_FACE_euler_make_ring_loop(FirstFace(body), &loop), "PK_FACE_euler_make_ring_loop");
    if (loop <= 0)
        throw new InvalidOperationException("PK_FACE_euler_make_ring_loop returned an invalid loop");
    return body;
}

static unsafe PK_BODY_t CreateFaceRingFace()
{
    var body = CreateSheet();
    PK_FACE_t face;
    ParasolidXtCorpusHost.Check(PK_FACE_euler_make_ring_face(FirstFace(body), &face), "PK_FACE_euler_make_ring_face");
    if (face <= 0)
        throw new InvalidOperationException("PK_FACE_euler_make_ring_face returned an invalid face");
    return body;
}

static unsafe PK_BODY_t CreateEdgeSplit()
{
    var body = CreateSheet();
    PK_VERTEX_t vertex;
    PK_EDGE_t edge;
    ParasolidXtCorpusHost.Check(
        PK_EDGE_euler_split(FirstEdge(body), PK_LOGICAL_true, &vertex, &edge),
        "PK_EDGE_euler_split");
    if (vertex <= 0 || edge <= 0)
        throw new InvalidOperationException("PK_EDGE_euler_split returned an invalid topology tag");
    return body;
}

static unsafe PK_BODY_t CreateEdgeMakeCurve()
{
    var body = CreateSheet();
    var options = new PK_EDGE_make_curve_o_t
    {
        o_t_version = 2,
    };
    var edges = stackalloc PK_EDGE_t[1] { FirstEdge(body) };
    var tracking = new PK_ENTITY_track_r_t();
    ParasolidXtCorpusHost.Check(
        PK_EDGE_make_curve(1, edges, 1.0e-5, &options, &tracking),
        "PK_EDGE_make_curve");
    ParasolidXtCorpusHost.Check(PK_ENTITY_track_r_f(&tracking), "PK_ENTITY_track_r_f(edge make curve)");
    return body;
}

static unsafe PK_BODY_t CreateLoopMakeEdge()
{
    var body = CreateSheet();
    var loop = FirstLoop(body);
    PK_VERTEX_t vertex;
    PK_EDGE_t edge;
    ParasolidXtCorpusHost.Check(
        PK_LOOP_euler_make_edge(loop, LoopFin(loop, 0), &vertex, &edge),
        "PK_LOOP_euler_make_edge");
    if (vertex <= 0 || edge <= 0)
        throw new InvalidOperationException("PK_LOOP_euler_make_edge returned an invalid topology tag");
    return body;
}

static unsafe PK_BODY_t CreateLoopMakeEdgeFace()
{
    var body = CreateSheet();
    var loop = FirstLoop(body);
    PK_FACE_t face;
    PK_EDGE_t edge;
    ParasolidXtCorpusHost.Check(
        PK_LOOP_euler_make_edge_face(loop, LoopFin(loop, 0), LoopFin(loop, 2), &face, &edge),
        "PK_LOOP_euler_make_edge_face");
    if (face <= 0 || edge <= 0)
        throw new InvalidOperationException("PK_LOOP_euler_make_edge_face returned an invalid topology tag");
    return body;
}

static unsafe PK_BODY_t CreateLoopMakeEdgeLoop()
{
    var body = CreateSheet();
    var loop = FirstLoop(body);
    PK_LOOP_t newLoop;
    ParasolidXtCorpusHost.Check(
        PK_LOOP_euler_make_edge_loop(loop, LoopFin(loop, 0), LoopFin(loop, 2), &newLoop),
        "PK_LOOP_euler_make_edge_loop");
    if (newLoop <= 0)
        throw new InvalidOperationException("PK_LOOP_euler_make_edge_loop returned an invalid loop");
    return body;
}

static unsafe PK_BODY_t CreateEdgeSetPrecision()
{
    var body = CreateSheet();
    var options = new PK_EDGE_set_precision_o_t
    {
        o_t_version = 2,
        sp_method = PK_set_precision_method_default_c,
        report_short_edges = PK_set_precision_report_no_c,
    };
    int newEdgeCount;
    PK_EDGE_t* newEdges;
    ParasolidXtCorpusHost.Check(
        PK_EDGE_set_precision_2(FirstEdge(body), 1.0e-4, &options, &newEdgeCount, &newEdges),
        "PK_EDGE_set_precision_2");
    try
    {
        if (newEdgeCount < 0)
            throw new InvalidOperationException("PK_EDGE_set_precision_2 returned a negative edge count");
        return body;
    }
    finally
    {
        if (newEdges is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(newEdges), "PK_MEMORY_free(new edges)");
    }
}
