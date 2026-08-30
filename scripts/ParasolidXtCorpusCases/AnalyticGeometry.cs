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
        "analytic-curve-line-wire",
        "PK_LINE_create + PK_CURVE_make_wire_body",
        "Finite line segment promoted to a persistent wire body.",
        new[] { "curve", "line", "wire-body", "finite-range", "parameters/typical" },
        CreateLineWire,
        typedAsk: AssertWire,
        typeCoverage: new[] { "body.type.wire", "topology.edge.valence.2" }),
    new(
        "analytic-curve-circle-wire",
        "PK_CIRCLE_create + PK_CURVE_make_wire_body",
        "One full analytic circle promoted to a persistent wire body.",
        new[] { "curve", "circle", "wire-body", "periodic", "parameters/typical" },
        CreateCircleWire),
    new(
        "analytic-curve-ellipse-wire",
        "PK_ELLIPSE_create + PK_CURVE_make_wire_body",
        "One full analytic ellipse promoted to a persistent wire body.",
        new[] { "curve", "ellipse", "wire-body", "periodic", "parameters/typical" },
        CreateEllipseWire),
    new(
        "analytic-curve-wire-2",
        "PK_LINE_create + PK_CURVE_make_wire_body_2",
        "The option-bearing wire-body constructor with one finite line segment.",
        new[] { "curve", "line", "wire-body", "options", "finite-range" },
        CreateWireBody2),
    new(
        "analytic-curve-reversed-wire",
        "PK_CURVE_make_curve_reversed + PK_CURVE_make_wire_body",
        "A reversed finite line curve promoted to a persistent wire body.",
        new[] { "curve", "line", "reverse", "wire-body", "finite-range" },
        CreateReversedWire),
    new(
        "analytic-curve-line",
        "PK_LINE_create",
        "Construction line with an explicit axis-one standard form.",
        new[] { "curve", "line", "analytic", "construction-geometry", "axis1" },
        CreateLine,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"location\":[1.0,-2.0,0.5],\"axis\":[0.0,1.0,0.0]}"),
    new(
        "analytic-curve-line-rotated",
        "PK_LINE_create",
        "Construction line with a non-cardinal normalized axis direction.",
        new[] { "curve", "line", "analytic", "construction-geometry", "axis1", "axis/rotated" },
        CreateRotatedLine,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"location\":[-0.5,1.25,0.75],\"axis\":[0.6,0.8,0.0]}",
        typeCoverage: new[] { "analytic.axis.rotated" }),
    new(
        "analytic-curve-circle",
        "PK_CIRCLE_create",
        "Construction circle with an explicit axis-two basis and finite radius.",
        new[] { "curve", "circle", "analytic", "construction-geometry", "axis2" },
        CreateCircle,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"location\":[-1.0,2.0,0.0],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"radius\":2.5}"),
    new(
        "analytic-curve-circle-minimum-radius",
        "PK_CIRCLE_create",
        "Construction circle at a small positive radius boundary.",
        new[] { "curve", "circle", "analytic", "construction-geometry", "radius/minimum" },
        CreateMinimumCircle,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"radius\":0.000001}",
        typeCoverage: new[] { "analytic.radius.minimum" }),
    new(
        "analytic-curve-ellipse",
        "PK_ELLIPSE_create",
        "Construction ellipse with distinct major and minor radii.",
        new[] { "curve", "ellipse", "analytic", "construction-geometry", "axis2" },
        CreateEllipse,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"location\":[2.0,-1.0,0.25],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"majorRadius\":4.0,\"minorRadius\":1.5}"),
    new(
        "analytic-surface-plane",
        "PK_PLANE_create",
        "Planar sheet whose construction surface is created from an explicit plane standard form.",
        new[] { "surface", "plane", "analytic", "sheet", "axis2" },
        CreatePlane,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"location\":[0.0,0.0,1.25],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"sheetExtent\":[4.0,3.0]}"),
    new(
        "analytic-surface-plane-reversed",
        "PK_PLANE_create",
        "Planar sheet using a reversed normal and reference direction.",
        new[] { "surface", "plane", "analytic", "sheet", "axis2", "axis/reversed" },
        CreateReversedPlane,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"location\":[0.0,0.0,-1.25],\"axis\":[0.0,0.0,-1.0],\"refDirection\":[0.0,1.0,0.0]}",
        typeCoverage: new[] { "analytic.axis.reversed" }),
    new(
        "analytic-surface-cylinder",
        "PK_CYL_create + PK_CYL_make_solid_body",
        "Solid cylinder, exercising an analytic cylindrical surface and finite axial extent.",
        new[] { "surface", "cylinder", "analytic", "solid", "axis2" },
        CreateCylinder,
        new CorpusBodyCounts(2, 2, 3, 2, 0),
        null,
        "{\"location\":[0.0,0.0,-1.0],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"radius\":2.0,\"range\":[0.0,5.0]}"),
    new(
        "analytic-surface-cone",
        "PK_CONE_create + PK_CONE_make_solid_body",
        "Solid cone with a non-zero base radius and finite semi-angle.",
        new[] { "surface", "cone", "analytic", "solid", "axis2", "semi-angle" },
        CreateCone,
        new CorpusBodyCounts(2, 2, 3, 2, 0),
        null,
        "{\"location\":[0.0,0.0,0.0],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"radius\":2.0,\"semiAngle\":0.35,\"range\":[0.0,5.0]}"),
    new(
        "analytic-surface-sphere",
        "PK_SPHERE_create + PK_SPHERE_make_solid_body",
        "Solid sphere with an explicit spherical axis-two standard form.",
        new[] { "surface", "sphere", "analytic", "solid", "axis2", "periodic" },
        CreateSphere,
        new CorpusBodyCounts(2, 2, 1, 0, 0),
        null,
        "{\"location\":[-2.0,0.5,1.0],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"radius\":2.0}"),
    new(
        "analytic-surface-torus",
        "PK_TORUS_create + PK_TORUS_make_solid_body",
        "Solid torus with distinct major and minor radii.",
        new[] { "surface", "torus", "analytic", "solid", "axis2", "periodic" },
        CreateTorus,
        new CorpusBodyCounts(2, 2, 1, 0, 0),
        null,
        "{\"location\":[1.0,-1.0,0.0],\"axis\":[0.0,0.0,1.0],\"refDirection\":[1.0,0.0,0.0],\"majorRadius\":5.0,\"minorRadius\":1.25}"),
};

return ParasolidXtCorpusHost.RunGroup("analytic-geometry", cases, args);

static unsafe void AssertWire(PK_BODY_t body)
{
    PK_BODY_type_t type;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &type), "PK_BODY_ask_type wire");
    if (type != PK_BODY_type_wire_c)
        throw new InvalidOperationException("expected wire body, got " + type);
}

static unsafe PK_BODY_t CreateLine()
{
    var standardForm = new PK_LINE_sf_t(
        new PK_AXIS1_sf_t(
            new PK_VECTOR_t(1.0, -2.0, 0.5),
            new PK_VECTOR1_t(0.0, 1.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&standardForm, &line), "PK_LINE_create");
    return AddConstructionGeometry(line);
}

static unsafe PK_BODY_t CreateRotatedLine()
{
    var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(-0.5, 1.25, 0.75), new PK_VECTOR1_t(0.6, 0.8, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create rotated");
    return AddConstructionGeometry(line);
}

static unsafe PK_BODY_t CreateLineWire()
{
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(-2.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create wire");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(line, new PK_INTERVAL_t(-2.0, 2.0), &body), "PK_CURVE_make_wire_body line");
    return body;
}

static unsafe PK_BODY_t CreateCircleWire()
{
    var basis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    var circleForm = new PK_CIRCLE_sf_t(basis, 2.0);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&circleForm, &circle), "PK_CIRCLE_create wire");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(circle, new PK_INTERVAL_t(0.0, 6.283185307179586), &body), "PK_CURVE_make_wire_body circle");
    return body;
}

static unsafe PK_BODY_t CreateEllipseWire()
{
    var basis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    var ellipseForm = new PK_ELLIPSE_sf_t(basis, 3.0, 1.5);
    PK_ELLIPSE_t ellipse;
    ParasolidXtCorpusHost.Check(PK_ELLIPSE_create(&ellipseForm, &ellipse), "PK_ELLIPSE_create wire");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(ellipse, new PK_INTERVAL_t(0.0, 6.283185307179586), &body), "PK_CURVE_make_wire_body ellipse");
    return body;
}

static unsafe PK_BODY_t CreateWireBody2()
{
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(-1.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create wire 2");
    var curves = stackalloc PK_CURVE_t[1] { line };
    var bounds = stackalloc PK_INTERVAL_t[1] { new PK_INTERVAL_t(-1.0, 1.0) };
    var options = new PK_CURVE_make_wire_body_o_t
    {
        allow_disjoint = PK_LOGICAL_true,
        allow_general = PK_LOGICAL_false,
        check = PK_LOGICAL_true,
        want_edges = PK_LOGICAL_false,
        want_indices = PK_LOGICAL_false,
        sequential = PK_CURVE_sequential_no_c,
    };
    PK_BODY_t body;
    int edgeCount;
    PK_EDGE_t* edges;
    int* indices;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body_2(1, curves, bounds, &options, &body, &edgeCount, &edges, &indices), "PK_CURVE_make_wire_body_2");
    if (edges is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(edges), "PK_MEMORY_free wire 2 edges");
    if (indices is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(indices), "PK_MEMORY_free wire 2 indices");
    return body;
}

static unsafe PK_BODY_t CreateReversedWire()
{
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(-1.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create reversed");
    PK_CURVE_t reversed;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_curve_reversed(line, &reversed), "PK_CURVE_make_curve_reversed");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(reversed, new PK_INTERVAL_t(-1.0, 1.0), &body), "PK_CURVE_make_wire_body reversed");
    return body;
}

static unsafe PK_BODY_t CreateCircle()
{
    var standardForm = new PK_CIRCLE_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(-1.0, 2.0, 0.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        2.5);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&standardForm, &circle), "PK_CIRCLE_create");
    return AddConstructionGeometry(circle);
}

static unsafe PK_BODY_t CreateMinimumCircle()
{
    var form = new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)), 1.0e-6);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&form, &circle), "PK_CIRCLE_create minimum");
    return AddConstructionGeometry(circle);
}

static unsafe PK_BODY_t CreateEllipse()
{
    var standardForm = new PK_ELLIPSE_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(2.0, -1.0, 0.25),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        4.0,
        1.5);
    PK_ELLIPSE_t ellipse;
    ParasolidXtCorpusHost.Check(PK_ELLIPSE_create(&standardForm, &ellipse), "PK_ELLIPSE_create");
    return AddConstructionGeometry(ellipse);
}

static unsafe PK_BODY_t CreatePlane()
{
    var standardForm = new PK_PLANE_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, 1.25),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_PLANE_t plane;
    ParasolidXtCorpusHost.Check(PK_PLANE_create(&standardForm, &plane), "PK_PLANE_create");

    // A plane alone is construction geometry.  Add it to a small valid sheet
    // body so that it has a PK_PART owner and can be transmitted as a part.
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle");
    AddGeometryToPart(body, plane);
    return body;
}

static unsafe PK_BODY_t CreateReversedPlane()
{
    var form = new PK_PLANE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, -1.25), new PK_VECTOR1_t(0, 0, -1), new PK_VECTOR1_t(0, 1, 0)));
    PK_PLANE_t plane;
    ParasolidXtCorpusHost.Check(PK_PLANE_create(&form, &plane), "PK_PLANE_create reversed");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle reversed plane");
    AddGeometryToPart(body, plane);
    return body;
}

static unsafe PK_BODY_t CreateCylinder()
{
    var standardForm = new PK_CYL_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, -1.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        2.0);
    PK_CYL_t cylinder;
    ParasolidXtCorpusHost.Check(PK_CYL_create(&standardForm, &cylinder), "PK_CYL_create");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CYL_make_solid_body(cylinder, new PK_INTERVAL_t(0.0, 5.0), &body), "PK_CYL_make_solid_body");
    return body;
}

static unsafe PK_BODY_t CreateCone()
{
    var standardForm = new PK_CONE_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, 0.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        2.0,
        0.35);
    PK_CONE_t cone;
    ParasolidXtCorpusHost.Check(PK_CONE_create(&standardForm, &cone), "PK_CONE_create");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_CONE_make_solid_body(cone, new PK_INTERVAL_t(0.0, 5.0), &body), "PK_CONE_make_solid_body");
    return body;
}

static unsafe PK_BODY_t CreateSphere()
{
    var standardForm = new PK_SPHERE_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(-2.0, 0.5, 1.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        2.0);
    PK_SPHERE_t sphere;
    ParasolidXtCorpusHost.Check(PK_SPHERE_create(&standardForm, &sphere), "PK_SPHERE_create");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_SPHERE_make_solid_body(sphere, &body), "PK_SPHERE_make_solid_body");
    return body;
}

static unsafe PK_BODY_t CreateTorus()
{
    var standardForm = new PK_TORUS_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(1.0, -1.0, 0.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        5.0,
        1.25);
    PK_TORUS_t torus;
    ParasolidXtCorpusHost.Check(PK_TORUS_create(&standardForm, &torus), "PK_TORUS_create");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_TORUS_make_solid_body(torus, &body), "PK_TORUS_make_solid_body");
    return body;
}

static unsafe PK_BODY_t AddConstructionGeometry(PK_GEOM_t geometry)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle");
    AddGeometryToPart(body, geometry);
    return body;
}

static unsafe void AddGeometryToPart(PK_PART_t part, PK_GEOM_t geometry)
{
    var geometries = stackalloc PK_GEOM_t[1];
    geometries[0] = geometry;
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(part, 1, geometries), "PK_PART_add_geoms");
}
