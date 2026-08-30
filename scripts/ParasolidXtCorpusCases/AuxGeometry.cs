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
        "geometry.point.construction",
        "PK_POINT_create + PK_PART_add_geoms",
        "A construction point attached to a legal sheet body.",
        new[] { "geometry", "point", "construction-geometry", "owner/part" },
        CreateConstructionPoint,
        null,
        null,
        "{\"position\":[1.25,-0.5,2.0],\"owner\":\"part.construction-geometry\"}"),
    new(
        "geometry.part-remove-construction",
        "PK_PART_add_geoms + PK_PART_remove_geoms",
        "A construction circle is attached to and then removed from a sheet part.",
        new[] { "geometry", "circle", "part", "remove", "mutator" },
        CreateRemovedConstructionGeometry,
        null,
        null,
        "{\"geometry\":\"circle\",\"operation\":\"add-then-remove\"}"),
    new(
        "geometry.geom-copy-construction",
        "PK_GEOM_copy + PK_PART_add_geoms",
        "A copied point geometry attached as construction data on a sheet part.",
        new[] { "geometry", "point", "copy", "construction-geometry", "owner/part" },
        CreateCopiedConstructionPoint,
        null,
        null,
        "{\"source\":\"point\",\"copyDependents\":\"always\",\"owner\":\"part.construction-geometry\"}"),
    new(
        "geometry.entity-delete-temporary",
        "PK_ENTITY_delete",
        "A temporary analytic point is deleted before the persistent sheet part is transmitted.",
        new[] { "geometry", "point", "entity", "delete", "lifecycle" },
        CreateAfterTemporaryDelete,
        null,
        null,
        "{\"temporary\":\"point\",\"operation\":\"delete-before-transmit\"}"),
    new(
        "geometry.geom-delete-single",
        "PK_GEOM_delete_single",
        "Delete a standalone analytic line through the single-geometry lifecycle API.",
        new[] { "geometry", "line", "delete", "lifecycle", "mutator" },
        CreateAfterSingleGeometryDelete,
        null,
        null,
        "{\"temporary\":\"line\",\"operation\":\"delete-single-before-transmit\"}"),
    new(
        "geometry.geom-transform",
        "PK_GEOM_transform + PK_TRANSF_create_translation",
        "A construction point transformed into a new persistent geometry.",
        new[] { "geometry", "point", "transform", "translation", "construction-geometry" },
        CreateTransformedConstructionPoint,
        null,
        null,
        "{\"source\":\"point\",\"translation\":[2.0,-1.0,0.5],\"mode\":\"new\"}"),
    new(
        "geometry.body-outline",
        "PK_BODY_make_curves_outline + PK_TRANSF_create",
        "Visible outline curves extracted from a solid block and retained on a sheet part.",
        new[] { "geometry", "outline", "curve", "body", "construction-geometry" },
        CreateBodyOutline,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"viewDirection\":[0.0,0.0,1.0],\"operation\":\"curves-outline\"}"),
    new(
        "geometry.body-perspective-outline",
        "PK_BODY_make_persp_outline + PK_CURVE_make_wire_body",
        "Perspective outline curve extracted from a solid block and promoted to a wire body.",
        new[] { "geometry", "outline", "perspective", "curve", "wire-body" },
        CreatePerspectiveOutline,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"eyePosition\":[10.0,10.0,10.0],\"operation\":\"perspective-outline\"}"),
    new(
        "geometry.body-spun-outline",
        "PK_BODY_make_spun_outline + PK_CURVE_make_wire_body",
        "Spun outline curve extracted from a solid block around a fixed axis.",
        new[] { "geometry", "outline", "spun", "curve", "wire-body" },
        CreateSpunOutline,
        null,
        null,
        "{\"source\":\"body.solid.block.typical\",\"spinAxis\":{\"location\":[0.0,0.0,0.0],\"direction\":[0.0,0.0,1.0]},\"operation\":\"spun-outline\"}"),
    new(
        "geometry.lsq-plane",
        "PK_VECTOR_make_lsq_plane + PK_PART_add_geoms",
        "A least-squares plane fitted through three non-collinear positions and retained as construction geometry.",
        new[] { "geometry", "plane", "least-squares", "construction-geometry", "owner/part" },
        CreateLeastSquaresPlane,
        null,
        null,
        "{\"positions\":[[0.0,0.0,0.0],[1.0,0.0,0.1],[0.0,1.0,-0.1]],\"owner\":\"part.construction-geometry\"}"),
};

return ParasolidXtCorpusHost.RunGroup("aux-geometry", cases, args);

static unsafe PK_BODY_t CreateConstructionPoint()
{
    var standardForm = new PK_POINT_sf_t(new PK_VECTOR_t(1.25, -0.5, 2.0));
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&standardForm, &point), "PK_POINT_create");

    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle(point owner)");
    var geometries = stackalloc PK_GEOM_t[1] { point };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geometries), "PK_PART_add_geoms(point)");
    return body;
}

static unsafe PK_BODY_t CreateRemovedConstructionGeometry()
{
    var circleForm = new PK_CIRCLE_sf_t(
        new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, 0.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0),
            new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        1.0);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&circleForm, &circle), "PK_CIRCLE_create remove");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle remove");
    var geometries = stackalloc PK_GEOM_t[1] { circle };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geometries), "PK_PART_add_geoms remove");
    int removed;
    ParasolidXtCorpusHost.Check(PK_PART_remove_geoms(body, 1, geometries, &removed), "PK_PART_remove_geoms");
    if (removed != 1)
        throw new InvalidOperationException("expected one construction geometry removed, got " + removed);
    return body;
}

static unsafe PK_BODY_t CreateCopiedConstructionPoint()
{
    var sourceForm = new PK_POINT_sf_t(new PK_VECTOR_t(1.0, 1.0, 1.0));
    PK_POINT_t source;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&sourceForm, &source), "PK_POINT_create copy source");
    var sourceGeom = stackalloc PK_GEOM_t[1] { source };
    var options = new PK_GEOM_copy_o_t();
    var copies = new PK_GEOM_copy_r_t();
    ParasolidXtCorpusHost.Check(PK_GEOM_copy(1, sourceGeom, &options, &copies), "PK_GEOM_copy");
    try
    {
        if (copies.n_copied_geoms != 1)
            throw new InvalidOperationException("expected one copied geometry, got " + copies.n_copied_geoms);
        PK_BODY_t body;
        ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle copied point");
        var copied = stackalloc PK_GEOM_t[1] { copies.copied_geoms[0] };
        ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, copied), "PK_PART_add_geoms copied point");
        return body;
    }
    finally
    {
        ParasolidXtCorpusHost.Check(PK_GEOM_copy_r_f(&copies), "PK_GEOM_copy_r_f");
    }
}

static unsafe PK_BODY_t CreateAfterTemporaryDelete()
{
    var pointForm = new PK_POINT_sf_t(new PK_VECTOR_t(-1.0, 0.5, 0.0));
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &point), "PK_POINT_create temporary");
    var entities = stackalloc PK_ENTITY_t[1] { point };
    ParasolidXtCorpusHost.Check(PK_ENTITY_delete(1, entities), "PK_ENTITY_delete temporary");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle after delete");
    return body;
}

static unsafe PK_BODY_t CreateAfterSingleGeometryDelete()
{
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create geom delete single");
    ParasolidXtCorpusHost.Check(PK_GEOM_delete_single(line), "PK_GEOM_delete_single");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle after geom delete single");
    return body;
}

static unsafe PK_BODY_t CreateLeastSquaresPlane()
{
    var positions = stackalloc PK_VECTOR_t[]
    {
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR_t(1.0, 0.0, 0.1),
        new PK_VECTOR_t(0.0, 1.0, -0.1),
    };
    var options = new PK_VECTOR_make_lsq_plane_o_t();
    PK_PLANE_t plane;
    ParasolidXtCorpusHost.Check(PK_VECTOR_make_lsq_plane(3, positions, &options, &plane), "PK_VECTOR_make_lsq_plane");
    return AddGeometryToSheet(plane, "least-squares plane");
}

static unsafe PK_BODY_t CreateTransformedConstructionPoint()
{
    var pointForm = new PK_POINT_sf_t(new PK_VECTOR_t(0.5, 0.5, 0.5));
    PK_POINT_t point;
    ParasolidXtCorpusHost.Check(PK_POINT_create(&pointForm, &point), "PK_POINT_create transform");
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create_translation(new PK_VECTOR_t(2.0, -1.0, 0.5), &transform), "PK_TRANSF_create_translation geom");
    PK_GEOM_t transformed;
    PK_LOGICAL_t exact;
    ParasolidXtCorpusHost.Check(PK_GEOM_transform(point, transform, 0.000001, &transformed, &exact), "PK_GEOM_transform");
    return AddGeometryToSheet(transformed, "transformed point");
}

static unsafe PK_BODY_t AddGeometryToSheet(PK_GEOM_t geometry, string label)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 3.0, null, &body), "PK_BODY_create_sheet_rectangle " + label);
    var geometries = stackalloc PK_GEOM_t[1] { geometry };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geometries), "PK_PART_add_geoms " + label);
    return body;
}

static unsafe PK_BODY_t CreateBodyOutline()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block outline");
    var identity = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &transform), "PK_TRANSF_create outline");
    var options = new PK_BODY_make_curves_outline_o_t();
    int curveCount;
    PK_CURVE_t* curves;
    PK_INTERVAL_t* intervals;
    PK_TOPOL_t* topols;
    int* outlines;
    double* tolerances;
    double maxSeparation;
    ParasolidXtCorpusHost.Check(
        PK_BODY_make_curves_outline(1, &source, &transform, new PK_VECTOR1_t(0.0, 0.0, 1.0), &options, &curveCount, &curves, &intervals, &topols, &outlines, &tolerances, &maxSeparation),
        "PK_BODY_make_curves_outline");
    try
    {
        if (curveCount <= 0)
            throw new InvalidOperationException("outline returned no curves");
        PK_BODY_t body;
        ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(curves[0], intervals[0], &body), "PK_CURVE_make_wire_body outline");
        return body;
    }
    finally
    {
        if (curves is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free outline curves");
        if (intervals is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(intervals), "PK_MEMORY_free outline intervals");
        if (topols is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(topols), "PK_MEMORY_free outline topols");
        if (outlines is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(outlines), "PK_MEMORY_free outline flags");
        if (tolerances is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(tolerances), "PK_MEMORY_free outline tolerances");
    }
}

static unsafe PK_BODY_t CreatePerspectiveOutline()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block perspective outline");
    var identity = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &transform), "PK_TRANSF_create perspective outline");
    var bodies = stackalloc PK_BODY_t[1] { source };
    var transforms = stackalloc PK_TRANSF_t[1] { transform };
    var options = new PK_BODY_make_persp_outline_o_t();
    var result = new PK_BODY_make_persp_outline_r_t();
    var tracking = new PK_ENTITY_track_r_t();
    ParasolidXtCorpusHost.Check(PK_BODY_make_persp_outline(1, bodies, transforms, new PK_VECTOR_t(10.0, 10.0, 10.0), &options, &result, &tracking), "PK_BODY_make_persp_outline");
    try
    {
        if (result.n_outlines <= 0 || result.outlines is null || result.outlines[0].n_curves <= 0)
            throw new InvalidOperationException("perspective outline returned no curves");
        var outline = result.outlines[0];
        var curve = outline.curves[0];
        var interval = outline.intervals[0];
        PK_BODY_t body;
        ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(curve, interval, &body), "PK_CURVE_make_wire_body perspective outline");
        return body;
    }
    finally
    {
        ParasolidXtCorpusHost.Check(PK_BODY_make_persp_outline_r_f(&result), "PK_BODY_make_persp_outline_r_f");
        ParasolidXtCorpusHost.Check(PK_ENTITY_track_r_f(&tracking), "PK_ENTITY_track_r_f perspective outline");
    }
}

static unsafe PK_BODY_t CreateSpunOutline()
{
    PK_BODY_t source;
    ParasolidXtCorpusHost.Check(PK_BODY_create_solid_block(1.0, 2.0, 3.0, null, &source), "PK_BODY_create_solid_block spun outline");
    var identity = new PK_TRANSF_sf_t(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1);
    PK_TRANSF_t transform;
    ParasolidXtCorpusHost.Check(PK_TRANSF_create(&identity, &transform), "PK_TRANSF_create spun outline");
    var bodies = stackalloc PK_BODY_t[1] { source };
    var transforms = stackalloc PK_TRANSF_t[1] { transform };
    var axis = new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0));
    var options = new PK_BODY_make_spun_outline_o_t();
    int curveCount;
    PK_CURVE_t* curves;
    PK_INTERVAL_t* intervals;
    PK_TOPOL_t* topols;
    int* outlines;
    double* tolerances;
    double maxSeparation;
    ParasolidXtCorpusHost.Check(PK_BODY_make_spun_outline(1, bodies, transforms, &axis, &options, &curveCount, &curves, &intervals, &topols, &outlines, &tolerances, &maxSeparation), "PK_BODY_make_spun_outline");
    try
    {
        if (curveCount <= 0)
            throw new InvalidOperationException("spun outline returned no curves");
        PK_BODY_t body;
        ParasolidXtCorpusHost.Check(PK_CURVE_make_wire_body(curves[0], intervals[0], &body), "PK_CURVE_make_wire_body spun outline");
        return body;
    }
    finally
    {
        if (curves is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free spun outline curves");
        if (intervals is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(intervals), "PK_MEMORY_free spun outline intervals");
        if (topols is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(topols), "PK_MEMORY_free spun outline topols");
        if (outlines is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(outlines), "PK_MEMORY_free spun outline flags");
        if (tolerances is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(tolerances), "PK_MEMORY_free spun outline tolerances");
    }
}
