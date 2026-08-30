#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Surface dependencies are deliberately represented as independent cases.  A
// SP curve is made against each supported surface family and is then attached
// to a sheet part.  The typed ask checks below make the dependency explicit;
// the corpus host additionally checks the transmitted SP_CURVE schema node.
var cases = new CorpusCaseSpec[]
{
    new(
        "spcurve.surface.plane-bcurve",
        "PK_PLANE_create + PK_CURVE_make_spcurves_2",
        "A B-curve p-curve carried by a planar surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/bcurve", "surface/plane", "support/one" },
        CreatePlaneSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"plane\",\"pcurve\":\"bcurve\",\"range\":[-1.5,1.5]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "B_CURVE", "PLANE" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->PLANE" },
        typeCoverage: new[] { "geometry.surface.plane", "geometry.spcurve.pcurve.bcurve", "schema.node.SP_CURVE" }),
    new(
        "spcurve.pcurve.plane-direct-bcurve",
        "PK_PLANE_create + PK_BCURVE_create + PK_SPCURVE_create",
        "A directly constructed SP_CURVE whose p-curve is a two-dimensional B-curve.",
        new[] { "surface-dependency", "spcurve", "pcurve/direct-bcurve", "surface/plane", "support/one" },
        CreateDirectPlaneSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"plane\",\"pcurve\":\"2d-bcurve\",\"vertexDim\":2,\"degree\":1}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "B_CURVE", "PLANE" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->PLANE" },
        typeCoverage: new[] { "geometry.surface.plane", "geometry.spcurve.pcurve.direct-bcurve" }),
    new(
        "spcurve.surface.cylinder-isoparam",
        "PK_CYL_create + PK_CURVE_make_spcurves_2",
        "A seam-line p-curve on a cylindrical surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/line", "surface/cylinder", "surface/periodic-u" },
        CreateCylinderSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"cylinder\",\"pcurve\":\"u-isoparametric\",\"uvBox\":[0,6.283185307179586,0,4]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "CYLINDER" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->CYLINDER" },
        typeCoverage: new[] { "geometry.surface.cylinder", "geometry.spcurve.pcurve.bcurve" }),
    new(
        "spcurve.surface.cone-isoparam",
        "PK_CONE_create + PK_CURVE_make_spcurves_2",
        "A generatrix-line p-curve on a conical surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/line", "surface/cone", "surface/periodic-u" },
        CreateConeSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"cone\",\"pcurve\":\"u-isoparametric\",\"uvBox\":[0,6.283185307179586,0.5,4]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "CONE" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->CONE" },
        typeCoverage: new[] { "geometry.surface.cone", "geometry.spcurve.pcurve.bcurve" }),
    new(
        "spcurve.surface.sphere-equator",
        "PK_SPHERE_create + PK_CIRCLE_create + PK_CURVE_make_spcurves_2",
        "A circular equator p-curve carried by a spherical surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/circle", "surface/sphere", "surface/closed-u" },
        CreateSphereSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"sphere\",\"pcurve\":\"circle\",\"radius\":2,\"range\":[0,6.283185307179586]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "SPHERE" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->SPHERE" },
        typeCoverage: new[] { "geometry.surface.sphere", "geometry.spcurve.pcurve.circle" }),
    new(
        "spcurve.surface.torus-equator",
        "PK_TORUS_create + PK_CIRCLE_create + PK_CURVE_make_spcurves_2",
        "A circular outer-equator p-curve carried by a toroidal surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/circle", "surface/torus", "surface/closed-u", "surface/closed-v" },
        CreateTorusSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"torus\",\"pcurve\":\"circle\",\"majorRadius\":4,\"minorRadius\":1,\"range\":[0,6.283185307179586]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "TORUS" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->TORUS" },
        typeCoverage: new[] { "geometry.surface.torus", "geometry.spcurve.pcurve.circle" }),
    new(
        "spcurve.surface.offset-line",
        "PK_PLANE_create + PK_SURF_offset + PK_CURVE_make_spcurves_2",
        "A line p-curve carried by a plane offset surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/line", "surface/offset", "surface/underlying" },
        CreateOffsetSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"offset-plane\",\"offsetDistance\":1.25,\"pcurve\":\"line\",\"range\":[-1.5,1.5]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE" },
        requiredSchemaDependencies: new[] { "SP_CURVE->PLANE" },
        typeCoverage: new[] { "geometry.surface.offset", "geometry.spcurve.pcurve.line" }),
    new(
        "spcurve.surface.swept-isoparam",
        "PK_SWEPT_create + PK_CURVE_make_spcurves_2",
        "A line p-curve carried by a swept surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/line", "surface/swept", "surface/derived" },
        CreateSweptSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"swept\",\"profile\":\"line\",\"direction\":[0,0,1],\"pcurve\":\"line-on-profile\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "SWEPT_SURF" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->SWEPT_SURF" },
        typeCoverage: new[] { "geometry.surface.swept", "geometry.spcurve.pcurve.bcurve" }),
    new(
        "spcurve.surface.spun-isoparam",
        "PK_SPUN_create + PK_CURVE_make_spcurves_2",
        "A circular p-curve carried by a spun surface.",
        new[] { "surface-dependency", "spcurve", "pcurve/circle", "surface/spun", "surface/derived" },
        CreateSpunSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"spun\",\"profile\":\"line\",\"axisDirection\":[0,0,1],\"pcurve\":\"circle\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "SPUN_SURF" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->SPUN_SURF" },
        typeCoverage: new[] { "geometry.surface.spun", "geometry.spcurve.pcurve.circle" }),
    new(
        "spcurve.surface.plane-ellipse",
        "PK_PLANE_create + PK_ELLIPSE_create + PK_CURVE_make_spcurves_2",
        "An elliptical p-curve carried by a planar surface.",
        new[] { "surface-dependency", "surface/plane", "spcurve", "pcurve/ellipse" },
        CreatePlaneEllipseSPCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"surface\":\"plane\",\"pcurve\":\"ellipse\",\"majorRadius\":2,\"minorRadius\":1,\"range\":[0,6.283185307179586]}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "PLANE" },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->PLANE" },
        typeCoverage: new[] { "geometry.surface.plane", "geometry.spcurve.pcurve.ellipse" }),
};

return ParasolidXtCorpusHost.RunGroup("surface-dependencies", cases, args);

static unsafe PK_BODY_t CreatePlaneSPCurve()
{
    var plane = CreatePlane(new PK_VECTOR_t(0.0, 0.0, 0.0));
    var line = CreateLine(new PK_VECTOR_t(-1.5, 0.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0));
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle plane");
    return AttachSPCurve(body, plane, line, new PK_INTERVAL_t(-1.5, 1.5));
}

static unsafe PK_BODY_t CreateDirectPlaneSPCurve()
{
    var plane = CreatePlane(new PK_VECTOR_t(0.0, 0.0, 0.0));
    var vertices = stackalloc double[] { -1.5, 0.0, 1.5, 0.0 };
    var multiplicities = stackalloc int[] { 2, 2 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    var form = new PK_BCURVE_sf_t(
        1, 2, 2, PK_LOGICAL_false, vertices,
        PK_BCURVE_form_arbitrary_c, 2, multiplicities, knots,
        PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false,
        PK_self_intersect_false_c);
    PK_BCURVE_t bcurve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&form, &bcurve), "PK_BCURVE_create direct SP curve");
    var spcurveForm = new PK_SPCURVE_sf_t(plane, bcurve);
    PK_SPCURVE_t spcurve;
    ParasolidXtCorpusHost.Check(PK_SPCURVE_create(&spcurveForm, &spcurve), "PK_SPCURVE_create direct");
    CheckSPCurve(spcurve, plane, bcurve);
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle direct SP curve");
    return AddConstructionGeometry(body, spcurve);
}

static unsafe PK_BODY_t CreatePlaneEllipseSPCurve()
{
    var plane = CreatePlane(new PK_VECTOR_t(0.0, 0.0, 0.0));
    var form = new PK_ELLIPSE_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 2.0, 1.0);
    PK_ELLIPSE_t ellipse;
    ParasolidXtCorpusHost.Check(PK_ELLIPSE_create(&form, &ellipse), "PK_ELLIPSE_create plane SP curve");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle plane ellipse");
    return AttachSPCurve(body, plane, ellipse, new PK_INTERVAL_t(0.0, 6.283185307179586));
}

static unsafe PK_BODY_t CreateCylinderSPCurve()
{
    var form = new PK_CYL_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 2.0);
    PK_CYL_t cylinder;
    ParasolidXtCorpusHost.Check(PK_CYL_create(&form, &cylinder), "PK_CYL_create spcurve");
    var line = CreateLine(new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0));
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle cylinder");
    return AttachSPCurve(body, cylinder, line, new PK_INTERVAL_t(0.0, 4.0));
}

static unsafe PK_BODY_t CreateConeSPCurve()
{
    var form = new PK_CONE_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 2.0, 0.35);
    PK_CONE_t cone;
    ParasolidXtCorpusHost.Check(PK_CONE_create(&form, &cone), "PK_CONE_create spcurve");
    var line = CreateLine(new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(Math.Sin(0.35), 0.0, Math.Cos(0.35)));
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle cone");
    return AttachSPCurve(body, cone, line, new PK_INTERVAL_t(0.0, 4.0));
}

static unsafe PK_BODY_t CreateSphereSPCurve()
{
    var form = new PK_SPHERE_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 2.0);
    PK_SPHERE_t sphere;
    ParasolidXtCorpusHost.Check(PK_SPHERE_create(&form, &sphere), "PK_SPHERE_create spcurve");
    var circleForm = new PK_CIRCLE_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 2.0);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&circleForm, &circle), "PK_CIRCLE_create sphere equator");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle sphere");
    return AttachSPCurve(body, sphere, circle, new PK_INTERVAL_t(0.0, 6.283185307179586));
}

static unsafe PK_BODY_t CreateTorusSPCurve()
{
    var form = new PK_TORUS_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 4.0, 1.0);
    PK_TORUS_t torus;
    ParasolidXtCorpusHost.Check(PK_TORUS_create(&form, &torus), "PK_TORUS_create spcurve");
    var circleForm = new PK_CIRCLE_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 5.0);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&circleForm, &circle), "PK_CIRCLE_create torus equator");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle torus");
    return AttachSPCurve(body, torus, circle, new PK_INTERVAL_t(0.0, 6.283185307179586));
}

static unsafe PK_BODY_t CreateOffsetSPCurve()
{
    var plane = CreatePlane(new PK_VECTOR_t(0.0, 0.0, 0.0));
    PK_SURF_t offset;
    ParasolidXtCorpusHost.Check(PK_SURF_offset(plane, 1.25, &offset), "PK_SURF_offset spcurve");
    var line = CreateLine(new PK_VECTOR_t(-1.5, 0.0, 1.25), new PK_VECTOR1_t(1.0, 0.0, 0.0));
    return AddSPCurveToPart(offset, line, new PK_INTERVAL_t(-1.5, 1.5), "offset");
}

static unsafe PK_BODY_t CreateSweptSPCurve()
{
    var line = CreateLine(new PK_VECTOR_t(-2.0, 0.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0));
    var form = new PK_SWEPT_sf_t(line, new PK_VECTOR1_t(0.0, 0.0, 1.0));
    PK_SWEPT_t swept;
    ParasolidXtCorpusHost.Check(PK_SWEPT_create(&form, &swept), "PK_SWEPT_create spcurve");
    return AddSPCurveToPart(swept, line, new PK_INTERVAL_t(-2.0, 2.0), "swept");
}

static unsafe PK_BODY_t CreateSpunSPCurve()
{
    var line = CreateLine(new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0));
    var form = new PK_SPUN_sf_t(line, new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_SPUN_t spun;
    ParasolidXtCorpusHost.Check(PK_SPUN_create(&form, &spun), "PK_SPUN_create spcurve");
    var circleForm = new PK_CIRCLE_sf_t(Axis2(new PK_VECTOR_t(0.0, 0.0, 0.0)), 2.0);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&circleForm, &circle), "PK_CIRCLE_create spun pcurve");
    return AddSPCurveToPart(spun, circle, new PK_INTERVAL_t(0.0, 6.283185307179586), "spun");
}

static unsafe PK_BODY_t AddSPCurveToPart(PK_SURF_t surface, PK_CURVE_t curve, PK_INTERVAL_t interval, string label)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle " + label);
    return AttachSPCurve(body, surface, curve, interval);
}

static unsafe PK_BODY_t AttachSPCurve(PK_BODY_t body, PK_SURF_t surface, PK_CURVE_t curve, PK_INTERVAL_t interval)
{
    int count;
    PK_SPCURVE_t* values;
    var options = new PK_CURVE_make_spcurves_o_t();
    ParasolidXtCorpusHost.Check(PK_CURVE_make_spcurves_2(curve, interval, surface, 1.0e-5, &options, &count, &values), "PK_CURVE_make_spcurves_2 isoparam");
    try
    {
        if (count < 1)
            throw new InvalidOperationException("isoparametric curve produced no SP curve");
        CheckSPCurve(values[0], surface, curve);
        return AddConstructionGeometry(body, values[0]);
    }
    finally
    {
        if (values is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(values), "PK_MEMORY_free isoparametric SP curves");
    }
}

static unsafe void CheckSPCurve(PK_SPCURVE_t spcurve, PK_SURF_t surface, PK_CURVE_t curve)
{
    PK_CLASS_t entityClass;
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(spcurve, &entityClass), "PK_ENTITY_ask_class SP curve");
    if (entityClass != PK_CLASS_spcurve)
        throw new InvalidOperationException("SP curve class mismatch: " + entityClass);
    PK_SPCURVE_sf_t standardForm;
    ParasolidXtCorpusHost.Check(PK_SPCURVE_ask(spcurve, &standardForm), "PK_SPCURVE_ask");
    if (standardForm.surf != surface)
        throw new InvalidOperationException($"SP curve surface dependency mismatch: {standardForm.surf}/{surface}");
    PK_CLASS_t pcurveClass;
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(standardForm.curve, &pcurveClass), "PK_ENTITY_ask_class SP curve p-curve");
    if (pcurveClass != PK_CLASS_bcurve)
        throw new InvalidOperationException("SP curve p-curve class mismatch: " + pcurveClass);
}

static unsafe PK_BODY_t AddConstructionGeometry(PK_BODY_t body, PK_GEOM_t geometry)
{
    var values = stackalloc PK_GEOM_t[1] { geometry };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, values), "PK_PART_add_geoms surface dependency");
    return body;
}

static unsafe PK_PLANE_t CreatePlane(PK_VECTOR_t location)
{
    var form = new PK_PLANE_sf_t(Axis2(location));
    PK_PLANE_t plane;
    ParasolidXtCorpusHost.Check(PK_PLANE_create(&form, &plane), "PK_PLANE_create surface dependency");
    return plane;
}

static unsafe PK_LINE_t CreateLine(PK_VECTOR_t location, PK_VECTOR1_t direction)
{
    var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(location, direction));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create surface dependency");
    return line;
}

static PK_AXIS2_sf_t Axis2(PK_VECTOR_t location) => new(
    location,
    new PK_VECTOR1_t(0.0, 0.0, 1.0),
    new PK_VECTOR1_t(1.0, 0.0, 0.0));
