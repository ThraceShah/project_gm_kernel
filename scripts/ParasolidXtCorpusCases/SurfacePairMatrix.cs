#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// This group is intentionally a Cartesian matrix.  A p-curve is always a
// two-dimensional B-curve in Parasolid; the four source families are kept as
// separate cases even where the resulting standard form is equivalent.  The
// supporting surface is likewise constructed independently for every case so
// that each manifest/x_t is a self-contained dependency fixture.
var cases = new List<CorpusCaseSpec>(36);
var surfaces = new[] { "plane", "cylinder", "cone", "sphere", "torus", "bsurf", "offset", "swept", "spun" };
var pcurves = new[] { "direct-bcurve", "line-bcurve", "circle-bcurve", "ellipse-bcurve" };
foreach (var surface in surfaces)
foreach (var pcurve in pcurves)
{
    var supportNode = surface switch
    {
        "plane" => "PLANE",
        "cylinder" => "CYLINDER",
        "cone" => "CONE",
        "sphere" => "SPHERE",
        "torus" => "TORUS",
        "bsurf" => "B_SURFACE",
        "offset" => "PLANE",
        "swept" => "SWEPT_SURF",
        "spun" => "SPUN_SURF",
        _ => throw new InvalidOperationException(surface),
    };
    var pcurveLabel = "geometry.spcurve.pcurve." + pcurve;
    var surfaceLabel = "geometry.surface." + surface;
    cases.Add(new CorpusCaseSpec(
        "spcurve." + surface + "." + pcurve,
        "PK_SPCURVE_create + PK_BCURVE_create",
        "Two-dimensional " + pcurve + " p-curve on a " + surface + " supporting surface.",
        new[] { "spcurve", "spcurve/pcurve-family", "surface-pair", "surface/" + surface, "pcurve/" + pcurve },
        () => CreateSPCurveCase(surface, pcurve),
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"supportingSurface\":\"" + surface + "\",\"pcurveFamily\":\"" + pcurve + "\",\"vertexDimension\":2}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "B_CURVE", supportNode },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->" + supportNode },
        typeCoverage: new[] { surfaceLabel, pcurveLabel, "geometry.spcurve.pair." + surface + "." + pcurve, "schema.node.SP_CURVE", "schema.node.B_CURVE" },
        typedAsk: body => AssertSPCurve(body, surface, pcurve)));
}

// Analytic z=0 source curves are converted by the kernel's SP_CURVE producer
// into two-dimensional BCurve p-curves.  Keep these as separate fixtures from
// the direct 2D-BSpline cases above so source provenance is observable.
foreach (var surface in surfaces)
foreach (var sourceCurve in new[] { "line", "circle", "ellipse" })
{
    // A straight line has no non-empty exact parameter curve on a sphere or
    // torus.  B-surface line/ellipse projection currently emits an XT spline
    // payload that the shared inspector cannot decode; those four legal
    // source combinations are recorded in the group's unreachable audit.
    if (((surface is "sphere" or "torus") && sourceCurve == "line") ||
        (surface == "bsurf" && (sourceCurve is "line" or "ellipse")))
        continue;
    var supportNode = surface switch
    {
        "plane" => "PLANE",
        "cylinder" => "CYLINDER",
        "cone" => "CONE",
        "sphere" => "SPHERE",
        "torus" => "TORUS",
        "bsurf" => "B_SURFACE",
        "offset" => "PLANE",
        "swept" => "SWEPT_SURF",
        "spun" => "SPUN_SURF",
        _ => throw new InvalidOperationException(surface),
    };
    cases.Add(new CorpusCaseSpec(
        "spcurve." + surface + ".analytic-" + sourceCurve,
        "PK_CURVE_make_spcurves_2 + PK_" + sourceCurve.ToUpperInvariant() + "_create",
        "A z=0 analytic " + sourceCurve + " converted to a BCurve p-curve on a " + surface + " supporting surface.",
        new[] { "spcurve", "spcurve/pcurve-family", "spcurve/analytic-source", "surface-pair", "surface/" + surface, "pcurve/analytic-" + sourceCurve },
        () => CreateAnalyticSPCurveCase(surface, sourceCurve),
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"supportingSurface\":\"" + surface + "\",\"sourceCurve\":\"z=0 " + sourceCurve + "\",\"producer\":\"PK_CURVE_make_spcurves_2\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "SP_CURVE", "B_CURVE", supportNode },
        requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE", "SP_CURVE->" + supportNode },
        typeCoverage: new[] { "geometry.surface." + surface, "geometry.spcurve.pcurve.analytic-" + sourceCurve, "geometry.spcurve.pair." + surface + ".analytic-" + sourceCurve, "schema.node.SP_CURVE", "schema.node.B_CURVE" },
        typedAsk: body => AssertSPCurve(body, surface, "analytic-" + sourceCurve)));
}

return ParasolidXtCorpusHost.RunGroup("surface-pair-matrix", cases, args);

static unsafe PK_BODY_t CreateSPCurveCase(string surfaceKind, string pcurveKind)
{
    var surface = CreateSurface(surfaceKind);
    var pcurve = CreatePcurve(pcurveKind);
    var form = new PK_SPCURVE_sf_t(surface, pcurve);
    PK_SPCURVE_t spcurve;
    ParasolidXtCorpusHost.Check(PK_SPCURVE_create(&form, &spcurve), "PK_SPCURVE_create " + surfaceKind + "/" + pcurveKind);

    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(6.0, 4.0, null, &body), "PK_BODY_create_sheet_rectangle surface pair");
    var geometry = stackalloc PK_GEOM_t[1] { spcurve };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geometry), "PK_PART_add_geoms surface pair SP_CURVE");
    return body;
}

static unsafe PK_BODY_t CreateAnalyticSPCurveCase(string surfaceKind, string sourceCurveKind)
{
    var surface = CreateSurface(surfaceKind);
    var source = CreateAnalyticCurve(sourceCurveKind, surfaceKind);
    var interval = sourceCurveKind == "line"
        ? (surfaceKind is "cylinder" or "cone" or "spun" ? new PK_INTERVAL_t(0.0, 4.0) : new PK_INTERVAL_t(-2.0, 2.0))
        : new PK_INTERVAL_t(0.0, 6.283185307179586);
    int count;
    PK_SPCURVE_t* curves;
    var options = new PK_CURVE_make_spcurves_o_t();
    ParasolidXtCorpusHost.Check(PK_CURVE_make_spcurves_2(source, interval, surface, 1.0e-5, &options, &count, &curves), "PK_CURVE_make_spcurves_2 analytic source");
    try
    {
        if (count < 1 || curves is null)
            throw new InvalidOperationException("analytic source generated no SP_CURVE: " + surfaceKind + "/" + sourceCurveKind);
        PK_BODY_t body;
        ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(6.0, 4.0, null, &body), "PK_BODY_create_sheet_rectangle analytic source");
        var geometry = stackalloc PK_GEOM_t[1] { curves[0] };
        ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geometry), "PK_PART_add_geoms analytic source SP_CURVE");
        return body;
    }
    finally
    {
        if (curves is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free analytic source SP_CURVEs");
    }
}

static unsafe PK_CURVE_t CreateAnalyticCurve(string kind, string surfaceKind)
{
    var z = surfaceKind == "offset" ? 1.0 : 0.0;
    if (kind == "line")
    {
        var location = surfaceKind switch
        {
            "cylinder" => new PK_VECTOR_t(2.0, 0.0, -2.0),
            "cone" => new PK_VECTOR_t(2.0, 0.0, 0.0),
            "spun" => new PK_VECTOR_t(2.0, 0.0, -2.0),
            _ => new PK_VECTOR_t(-2.0, 0.0, z),
        };
        var direction = surfaceKind switch
        {
            "cylinder" => new PK_VECTOR1_t(0.0, 0.0, 1.0),
            "cone" => new PK_VECTOR1_t(Math.Sin(0.35), 0.0, Math.Cos(0.35)),
            "spun" => new PK_VECTOR1_t(0.0, 0.0, 1.0),
            _ => new PK_VECTOR1_t(1.0, 0.0, 0.0),
        };
        var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(location, direction));
        PK_LINE_t line;
        ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create analytic source");
        return line;
    }
    if (kind == "circle")
    {
        var normal = surfaceKind is "swept" ? new PK_VECTOR1_t(0.0, 1.0, 0.0) : new PK_VECTOR1_t(0.0, 0.0, 1.0);
        var form = new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0.0, 0.0, z), normal, new PK_VECTOR1_t(1.0, 0.0, 0.0)), surfaceKind == "torus" ? 5.0 : 1.0);
        PK_CIRCLE_t circle;
        ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&form, &circle), "PK_CIRCLE_create analytic source");
        return circle;
    }
    var ellipseNormal = surfaceKind is "swept" ? new PK_VECTOR1_t(0.0, 1.0, 0.0) : new PK_VECTOR1_t(0.0, 0.0, 1.0);
    var ellipseForm = new PK_ELLIPSE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0.0, 0.0, z), ellipseNormal, new PK_VECTOR1_t(1.0, 0.0, 0.0)), 2.0, 1.0);
    PK_ELLIPSE_t ellipse;
    ParasolidXtCorpusHost.Check(PK_ELLIPSE_create(&ellipseForm, &ellipse), "PK_ELLIPSE_create analytic source");
    return ellipse;
}

static unsafe PK_CURVE_t CreatePcurve(string kind)
{
    // The line/direct families use a genuine 2D non-rational B-curve.  For
    // circle/ellipse source families use a quadratic 2D Bezier arc: unlike a
    // 3D rational B-curve, this is accepted by PK_SPCURVE_create as a true
    // p-curve (the source family remains explicit in the case metadata).
    if (kind is "circle-bcurve" or "ellipse-bcurve")
    {
        var scale = 1.0;
        var vertices = stackalloc double[]
        {
            scale, 0.0,
            scale, scale,
            0.0, scale,
        };
        var multiplicities = stackalloc int[] { 3, 3 };
        var knots = stackalloc double[] { 0.0, 1.0 };
        var form = new PK_BCURVE_sf_t(
            2, 3, 2, PK_LOGICAL_false, vertices,
            PK_BCURVE_form_arbitrary_c,
            2, multiplicities, knots, PK_knot_bezier_ends_c,
            PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c);
        PK_BCURVE_t bcurve;
        ParasolidXtCorpusHost.Check(PK_BCURVE_create(&form, &bcurve), "PK_BCURVE_create " + kind);
        return bcurve;
    }

    var lineVertices = stackalloc double[] { -2.0, 0.0, 2.0, 0.0 };
    var lineMultiplicities = stackalloc int[] { 2, 2 };
    var lineKnots = stackalloc double[] { 0.0, 1.0 };
    var lineForm = new PK_BCURVE_sf_t(
        1, 2, 2, PK_LOGICAL_false, lineVertices,
        PK_BCURVE_form_arbitrary_c, 2, lineMultiplicities, lineKnots,
        PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false,
        PK_self_intersect_false_c);
    PK_BCURVE_t line;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&lineForm, &line), "PK_BCURVE_create " + kind);
    return line;
}

static unsafe PK_SURF_t CreateSurface(string kind)
{
    var origin = new PK_VECTOR_t(0.0, 0.0, 0.0);
    var basis = new PK_AXIS2_sf_t(origin,
        new PK_VECTOR1_t(0.0, 0.0, 1.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    switch (kind)
    {
        case "plane":
        {
            var form = new PK_PLANE_sf_t(basis);
            PK_PLANE_t plane;
            ParasolidXtCorpusHost.Check(PK_PLANE_create(&form, &plane), "PK_PLANE_create surface pair");
            return plane;
        }
        case "cylinder":
        {
            var form = new PK_CYL_sf_t(basis, 2.0);
            PK_CYL_t cylinder;
            ParasolidXtCorpusHost.Check(PK_CYL_create(&form, &cylinder), "PK_CYL_create surface pair");
            return cylinder;
        }
        case "cone":
        {
            var form = new PK_CONE_sf_t(basis, 2.0, 0.35);
            PK_CONE_t cone;
            ParasolidXtCorpusHost.Check(PK_CONE_create(&form, &cone), "PK_CONE_create surface pair");
            return cone;
        }
        case "sphere":
        {
            var form = new PK_SPHERE_sf_t(basis, 2.0);
            PK_SPHERE_t sphere;
            ParasolidXtCorpusHost.Check(PK_SPHERE_create(&form, &sphere), "PK_SPHERE_create surface pair");
            return sphere;
        }
        case "torus":
        {
            var form = new PK_TORUS_sf_t(basis, 4.0, 1.0);
            PK_TORUS_t torus;
            ParasolidXtCorpusHost.Check(PK_TORUS_create(&form, &torus), "PK_TORUS_create surface pair");
            return torus;
        }
        case "bsurf":
        {
            var vertices = stackalloc double[]
            {
                -2.0, -2.0, 0.25, 0.0, -2.0, 0.0, 2.0, -2.0, 0.25,
                -2.0, 0.0, 0.0, 0.0, 0.0, 0.5, 2.0, 0.0, 0.0,
                -2.0, 2.0, 0.25, 0.0, 2.0, 0.0, 2.0, 2.0, 0.25,
            };
            var multiplicities = stackalloc int[] { 3, 3 };
            var knots = stackalloc double[] { 0.0, 1.0 };
            var form = new PK_BSURF_sf_t(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices,
                PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots,
                PK_knot_bezier_ends_c, PK_knot_bezier_ends_c,
                PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false,
                PK_self_intersect_false_c, PK_convexity_arbitrary_c);
            PK_BSURF_t bsurf;
            ParasolidXtCorpusHost.Check(PK_BSURF_create(&form, &bsurf), "PK_BSURF_create surface pair");
            return bsurf;
        }
        case "offset":
        {
            var planeForm = new PK_PLANE_sf_t(basis);
            PK_PLANE_t plane;
            ParasolidXtCorpusHost.Check(PK_PLANE_create(&planeForm, &plane), "PK_PLANE_create offset surface pair");
            PK_SURF_t offset;
            ParasolidXtCorpusHost.Check(PK_SURF_offset(plane, 1.0, &offset), "PK_SURF_offset surface pair");
            return offset;
        }
        case "swept":
        {
            var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(-2.0, 0.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
            PK_LINE_t line;
            ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create swept surface pair");
            var form = new PK_SWEPT_sf_t(line, new PK_VECTOR1_t(0.0, 0.0, 1.0));
            PK_SWEPT_t swept;
            ParasolidXtCorpusHost.Check(PK_SWEPT_create(&form, &swept), "PK_SWEPT_create surface pair");
            return swept;
        }
        case "spun":
        {
            var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
            PK_LINE_t line;
            ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create spun surface pair");
            var spunForm = new PK_SPUN_sf_t(line, new PK_AXIS1_sf_t(origin, new PK_VECTOR1_t(0.0, 0.0, 1.0)));
            PK_SPUN_t spun;
            ParasolidXtCorpusHost.Check(PK_SPUN_create(&spunForm, &spun), "PK_SPUN_create surface pair");
            return spun;
        }
        default:
            throw new InvalidOperationException("unknown supporting surface: " + kind);
    }
}

static unsafe void AssertSPCurve(PK_BODY_t body, string surfaceKind, string pcurveKind)
{
    int count;
    PK_CURVE_t* curves;
    ParasolidXtCorpusHost.Check(PK_PART_ask_construction_curves(body, &count, &curves), "PK_PART_ask_construction_curves surface pair");
    try
    {
        if (curves is null || count < 1)
            throw new InvalidOperationException("SP_CURVE fixture has no construction curve");
        for (var i = 0; i < count; i++)
        {
            PK_CLASS_t curveClass;
            ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(curves[i], &curveClass), "PK_ENTITY_ask_class surface pair curve");
            if (curveClass != PK_CLASS_spcurve)
                continue;
            PK_SPCURVE_sf_t asked;
            ParasolidXtCorpusHost.Check(PK_SPCURVE_ask(curves[i], &asked), "PK_SPCURVE_ask surface pair");
            if (asked.surf <= 0 || asked.curve <= 0)
                throw new InvalidOperationException("SP_CURVE has null support or p-curve");
            PK_CLASS_t pcurveClass;
            ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(asked.curve, &pcurveClass), "PK_ENTITY_ask_class pcurve surface pair");
            if (pcurveClass != PK_CLASS_bcurve)
                throw new InvalidOperationException("SP_CURVE p-curve is not a B_CURVE");
            var sf = default(PK_BCURVE_sf_t);
            ParasolidXtCorpusHost.Check(PK_BCURVE_ask(asked.curve, &sf), "PK_BCURVE_ask pcurve surface pair");
            try
            {
                if (sf.vertex_dim < 2 || sf.n_vertices < 2 || sf.n_knots < 2)
                    throw new InvalidOperationException("SP_CURVE p-curve has incomplete standard form");
            }
            finally
            {
                Free(sf.vertex);
                Free(sf.knot_mult);
                Free(sf.knot);
            }
            return;
        }
        throw new InvalidOperationException("SP_CURVE fixture has no PK_CLASS_spcurve: " + surfaceKind + "/" + pcurveKind);
    }
    finally
    {
        if (curves is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free surface pair curves");
    }
}

static unsafe void Free(void* pointer)
{
    if (pointer is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(pointer), "PK_MEMORY_free surface pair standard form");
}
