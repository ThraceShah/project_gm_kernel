#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// A fixed, deliberately small face-face blend matrix.  The two seed families
// exercise the canonical analytic result (plane/plane -> cylinder) and the
// dependent result (B-surface/plane -> BLENDSF).  The same shared constructor
// is used for each case so that option changes do not hide seed changes.
var cases = new CorpusCaseSpec[]
{
    new(
        "blend.fxf.plane-plane.rolling-ball.positive",
        "PK_FACE_make_blend",
        "Plane/plane rolling-ball blend with positive wall senses.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/plane-plane", "option/walls-trim-both", "sense/positive" },
        () => CreateBlend(new BlendRecipe(0, PK_blend_xs_rolling_ball_c, PK_blend_xs_shape_unset_c, PK_LOGICAL_false, PK_LOGICAL_false, default, PK_LOGICAL_true, PK_LOGICAL_true, PK_blend_walls_trim_both_c)),
        null,
        null,
        "{\"pair\":\"plane-plane\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05,\"leftSense\":true,\"rightSense\":true}",
        requiredSchemaNodes: new[] { "CYLINDER" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.plane-plane", "geometry.surface.cylinder", "blend.option.walls.trim-both" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_cyl)),
    new(
        "blend.fxf.plane-plane.rolling-ball.reversed",
        "PK_FACE_make_blend",
        "Plane/plane rolling-ball blend with the right wall sense reversed.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/plane-plane", "option/walls-trim-both", "sense/reversed" },
        () => CreateBlend(new BlendRecipe(0, PK_blend_xs_rolling_ball_c, PK_blend_xs_shape_unset_c, PK_LOGICAL_false, PK_LOGICAL_false, default, PK_LOGICAL_true, PK_LOGICAL_false, PK_blend_walls_trim_both_c)),
        null,
        null,
        "{\"pair\":\"plane-plane\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05,\"leftSense\":true,\"rightSense\":false}",
        requiredSchemaNodes: new[] { "CYLINDER" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.plane-plane", "geometry.surface.cylinder", "blend.option.walls.trim-both", "blend.option.sense.reversed" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_cyl)),
    new(
        "blend.fxf.bsurf-plane.rolling-ball",
        "PK_FACE_make_blend",
        "B-surface/plane rolling-ball blend with dependent supporting geometry.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/bsurf-plane", "option/walls-trim-both" },
        () => CreateBlend(new BlendRecipe(1, PK_blend_xs_rolling_ball_c, PK_blend_xs_shape_unset_c, PK_LOGICAL_false, PK_LOGICAL_false, default, PK_LOGICAL_true, PK_LOGICAL_true, PK_blend_walls_trim_both_c)),
        null,
        null,
        "{\"pair\":\"bsurf-plane\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        requiredSchemaDependencies: new[] { "BLENDED_EDGE->B_SURFACE", "BLENDED_EDGE->PLANE", "BLENDED_EDGE->INTERSECTION", "BLEND_BOUND->BLENDED_EDGE", "BLEND_BOUND->PLANE" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.bsurf-plane", "geometry.surface.bsurf", "geometry.surface.plane", "schema.node.BLENDED_EDGE", "schema.node.BLEND_BOUND", "blend.option.walls.trim-both" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_blendsf)),
    new(
        "blend.fxf.bsurf-plane.rolling-ball.help-point",
        "PK_FACE_make_blend",
        "B-surface/plane blend with an explicit help point.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/bsurf-plane", "option/help-point" },
        () => CreateBlend(new BlendRecipe(1, PK_blend_xs_rolling_ball_c, PK_blend_xs_shape_unset_c, PK_LOGICAL_false, PK_LOGICAL_true, new PK_VECTOR_t(1.0, 1.0, 0.45), PK_LOGICAL_true, PK_LOGICAL_true, PK_blend_walls_trim_both_c)),
        null,
        null,
        "{\"pair\":\"bsurf-plane\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05,\"helpPoint\":[1,1,0.45]}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.bsurf-plane", "schema.node.BLENDED_EDGE", "schema.node.BLEND_BOUND", "blend.option.help-point" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_blendsf)),
    new(
        "blend.fxf.cylinder-cone.rolling-ball",
        "PK_CYL_create + PK_CONE_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded cylindrical and conical supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/cylinder-cone", "option/walls-trim-both" },
        () => CreateAnalyticBlend("cylinder", "cone"),
        null,
        null,
        "{\"pair\":\"cylinder-cone\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "TORUS" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.cylinder-cone", "geometry.surface.cylinder", "geometry.surface.cone", "geometry.surface.torus", "blend.option.walls.trim-both" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_torus)),
    new(
        "blend.fxf.cylinder-bsurf.rolling-ball",
        "PK_CYL_create + PK_BSURF_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded cylindrical and B-surface supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/cylinder-bsurf", "option/walls-trim-both" },
        () => CreateAnalyticBlend("cylinder", "bsurf"),
        null,
        null,
        "{\"pair\":\"cylinder-bsurf\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "B_SURFACE" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.cylinder-bsurf", "geometry.surface.cylinder", "geometry.surface.bsurf", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.cylinder-swept.rolling-ball",
        "PK_CYL_create + PK_SWEPT_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded cylindrical and swept supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/cylinder-swept", "option/walls-trim-both" },
        () => CreateAnalyticBlend("cylinder", "swept"),
        null,
        null,
        "{\"pair\":\"cylinder-swept\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "CYLINDER", "SP_CURVE" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.cylinder-swept", "geometry.surface.cylinder", "geometry.surface.swept", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.cone-sphere.rolling-ball",
        "PK_CONE_create + PK_SPHERE_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded conical and spherical supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/cone-sphere", "option/walls-trim-both" },
        () => CreateAnalyticBlend("cone", "sphere"),
        null,
        null,
        "{\"pair\":\"cone-sphere\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "TORUS" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.cone-sphere", "geometry.surface.cone", "geometry.surface.sphere", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.cone-bsurf.rolling-ball",
        "PK_CONE_create + PK_BSURF_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded conical and B-surface supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/cone-bsurf", "option/walls-trim-both" },
        () => CreateAnalyticBlend("cone", "bsurf"),
        null,
        null,
        "{\"pair\":\"cone-bsurf\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.cone-bsurf", "geometry.surface.cone", "geometry.surface.bsurf", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.cone-spun.rolling-ball",
        "PK_CONE_create + PK_SPUN_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded conical and spun supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/cone-spun", "option/walls-trim-both" },
        () => CreateAnalyticBlend("cone", "spun"),
        null,
        null,
        "{\"pair\":\"cone-spun\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "TORUS" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.cone-spun", "geometry.surface.cone", "geometry.surface.spun", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.sphere-bsurf.rolling-ball",
        "PK_SPHERE_create + PK_BSURF_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded spherical and B-surface supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/sphere-bsurf", "option/walls-trim-both" },
        () => CreateAnalyticBlend("sphere", "bsurf"),
        null,
        null,
        "{\"pair\":\"sphere-bsurf\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.sphere-bsurf", "geometry.surface.sphere", "geometry.surface.bsurf", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.sphere-swept.rolling-ball",
        "PK_SPHERE_create + PK_SWEPT_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded spherical and swept supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/sphere-swept", "option/walls-trim-both" },
        () => CreateAnalyticBlend("sphere", "swept"),
        null,
        null,
        "{\"pair\":\"sphere-swept\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.sphere-swept", "geometry.surface.sphere", "geometry.surface.swept", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.bsurf-spun.rolling-ball",
        "PK_BSURF_create + PK_SPUN_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded B-surface and spun supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/bsurf-spun", "option/walls-trim-both" },
        () => CreateAnalyticBlend("bsurf", "spun"),
        null,
        null,
        "{\"pair\":\"bsurf-spun\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.bsurf-spun", "geometry.surface.bsurf", "geometry.surface.spun", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.swept-spun.rolling-ball",
        "PK_SWEPT_create + PK_SPUN_create + PK_FACE_make_blend",
        "Rolling-ball blend between bounded swept and spun supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/swept-spun", "option/walls-trim-both" },
        () => CreateAnalyticBlend("swept", "spun"),
        null,
        null,
        "{\"pair\":\"swept-spun\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "CYLINDER", "SP_CURVE" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.swept-spun", "geometry.surface.swept", "geometry.surface.spun", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.spun-spun.rolling-ball",
        "PK_SPUN_create + PK_FACE_make_blend",
        "Rolling-ball blend between two bounded spun supporting faces.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface-pair/spun-spun", "option/walls-trim-both" },
        () => CreateAnalyticBlend("spun", "spun"),
        null,
        null,
        "{\"pair\":\"spun-spun\",\"xsection\":\"rolling-ball\",\"walls\":\"trim-both\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "TORUS", "SPUN_SURF", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.blend.pair.spun-spun", "geometry.surface.spun", "blend.option.walls.trim-both" },
        typedAsk: AssertBlendAny),
    new(
        "blend.fxf.bsurf-plane.shape-conic",
        "PK_BSURF_create + PK_FACE_make_blend",
        "B-surface/plane blend using the conic cross-section shape.",
        new[] { "blend", "blend/face-face", "surface-pair/bsurf-plane", "option/shape-conic" },
        () => CreateBlendVariant("shape-conic"),
        null,
        null,
        "{\"pair\":\"bsurf-plane\",\"xsection\":\"rolling-ball\",\"shape\":\"conic\",\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.bsurf", "geometry.surface.plane", "blend.option.shape.conic" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_blendsf)),
    new(
        "blend.fxf.bsurf-plane.multiple",
        "PK_BSURF_create + PK_FACE_make_blend",
        "B-surface/plane blend with multiple-result selection enabled.",
        new[] { "blend", "blend/face-face", "surface-pair/bsurf-plane", "option/multiple" },
        () => CreateBlendVariant("multiple"),
        null,
        null,
        "{\"pair\":\"bsurf-plane\",\"xsection\":\"rolling-ball\",\"multiple\":true,\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.bsurf", "geometry.surface.plane", "blend.option.multiple" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_blendsf)),
    new(
        "blend.fxf.bsurf-plane.propagate",
        "PK_BSURF_create + PK_FACE_make_blend",
        "B-surface/plane blend with wall propagation enabled.",
        new[] { "blend", "blend/face-face", "surface-pair/bsurf-plane", "option/propagate" },
        () => CreateBlendVariant("propagate"),
        null,
        null,
        "{\"pair\":\"bsurf-plane\",\"xsection\":\"rolling-ball\",\"propagate\":true,\"radius\":0.05}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.bsurf", "geometry.surface.plane", "blend.option.propagate" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_blendsf)),
    new(
        "blend.fxf.bsurf-plane.rho-relative",
        "PK_BSURF_create + PK_FACE_make_blend",
        "B-surface/plane blend with relative rho shape parameterization.",
        new[] { "blend", "blend/face-face", "surface-pair/bsurf-plane", "option/rho-relative" },
        () => CreateBlendVariant("rho-relative"),
        null,
        null,
        "{\"pair\":\"bsurf-plane\",\"xsection\":\"rolling-ball\",\"rhoType\":\"relative\",\"rho\":0.5}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.bsurf", "geometry.surface.plane", "blend.option.rho.relative" },
        typedAsk: body => AssertBlendResult(body, PK_CLASS_blendsf)),
};

return ParasolidXtCorpusHost.RunGroup("blend-matrix", cases, args);

static unsafe PK_BODY_t CreateBlend(BlendRecipe recipe)
{
    PK_FACE_t left;
    PK_FACE_t right;
    PK_BODY_t leftBody;
    PK_CURVE_t spine;
    if (recipe.SeedKind == 0)
        (leftBody, left, right, spine) = CreatePlanePair();
    else
        (leftBody, left, right, spine) = CreateBSurfPlanePair();

    var options = new PK_FACE_make_blend_o_t();
    options.walls = recipe.Walls;
    options.shape.xsection = recipe.Xsection;
    options.shape.radius = 0.05;
    options.shape.xs_shape = recipe.CrossSectionShape;
    options.shape.parameter = spine;
    options.multiple = recipe.Multiple;
    options.have_help_point = recipe.HaveHelpPoint;
    options.help_point = recipe.HelpPoint;

    var leftFaces = new[] { left };
    var rightFaces = new[] { right };
    int nSheetBodies;
    PK_BODY_t* sheetBodies = null;
    int nBlendTopols;
    PK_TOPOL_t* blendTopols = null;
    PK_TOPOL_array_t* unders = null;
    var ribs = new PK_blend_rib_r_t();
    var fault = new PK_fxf_error_t();
    var status = PK_FACE_make_blend(1, leftFaces, 1, rightFaces, recipe.LeftSense, recipe.RightSense, &options,
        &nSheetBodies, &sheetBodies, &nBlendTopols, &blendTopols, &unders, &ribs, &fault);
    if (status != PK_ERROR_no_errors || nSheetBodies < 1 || sheetBodies is null || nBlendTopols < 1 || blendTopols is null)
    {
        if (sheetBodies is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free blend matrix bodies");
        if (blendTopols is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free blend matrix topols");
        if (unders is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free blend matrix unders");
        if (ribs.n_ribs != 0) ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f blend matrix");
        throw new InvalidOperationException("PK_FACE_make_blend failed status=" + status + " fault=" + fault.fault + " nSheet=" + nSheetBodies + " nBlend=" + nBlendTopols);
    }

    var result = sheetBodies[0];
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free blend matrix bodies");
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free blend matrix topols");
    if (unders is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free blend matrix unders");
    if (ribs.n_ribs != 0) ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f blend matrix");
    return result;
}

static unsafe PK_BODY_t CreateAnalyticBlend(string leftKind, string rightKind)
{
    var leftBody = CreateAnalyticSheet(leftKind);
    var rightBody = CreateAnalyticSheet(rightKind);
    var left = FirstFace(leftBody);
    var right = FirstFace(rightBody);
    var spineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(0.25, 0.25, -2.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_LINE_t spine;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&spineForm, &spine), "PK_LINE_create analytic blend spine");
    var options = new PK_FACE_make_blend_o_t();
    options.shape.xsection = PK_blend_xs_rolling_ball_c;
    options.shape.radius = 0.05;
    options.shape.parameter = spine;
    options.walls = PK_blend_walls_trim_both_c;
    var leftFaces = new[] { left };
    var rightFaces = new[] { right };
    if (TryBlendRaw(leftFaces, rightFaces, &options, out var result, out var fault))
        return result;
    throw new InvalidOperationException($"analytic {leftKind}/{rightKind} blend has no stable seed (fault={fault})");
}

static unsafe PK_BODY_t CreateBlendVariant(string variant)
{
    var (_, left, right, spine) = CreateBSurfPlanePair();
    var options = new PK_FACE_make_blend_o_t();
    options.shape.xsection = PK_blend_xs_rolling_ball_c;
    options.shape.radius = 0.05;
    options.shape.parameter = spine;
    options.walls = PK_blend_walls_trim_both_c;
    switch (variant)
    {
        case "shape-conic": options.shape.xs_shape = PK_blend_xs_shape_conic_c; break;
        case "shape-g2": options.shape.xs_shape = PK_blend_xs_shape_g2_c; break;
        case "shape-chamfer": options.shape.xs_shape = PK_blend_xs_shape_chamfer_c; break;
        case "width": options.shape.xs_shape = PK_blend_xs_shape_conic_c; options.shape.width = 0.1; break;
        case "ratio": options.shape.xs_shape = PK_blend_xs_shape_conic_c; options.shape.ratio = 0.75; break;
        case "rho-relative": options.shape.xs_shape = PK_blend_xs_shape_conic_c; options.shape.rho_type = PK_blend_rho_relative_c; options.shape.rho_const = 0.5; break;
        case "multiple": options.multiple = PK_LOGICAL_true; break;
        case "propagate": options.propagate = PK_blend_propagate_yes_c; break;
        case "notch": options.notch = PK_LOGICAL_true; break;
        default: throw new InvalidOperationException("unknown blend variant " + variant);
    }
    if (TryBlendRaw(new[] { left }, new[] { right }, &options, out var result, out var fault))
        return result;
    throw new InvalidOperationException("B-surface/plane blend variant failed: " + variant + " fault=" + fault);
}

static unsafe bool TryBlendRaw(PK_FACE_t[] left, PK_FACE_t[] right, PK_FACE_make_blend_o_t* options, out PK_BODY_t result, out PK_fxf_fault_t faultCode)
{
    result = default;
    faultCode = PK_fxf_fault_no_fault_c;
    int nSheetBodies;
    PK_BODY_t* sheetBodies = null;
    int nBlendTopols;
    PK_TOPOL_t* blendTopols = null;
    PK_TOPOL_array_t* unders = null;
    var ribs = new PK_blend_rib_r_t();
    var fault = new PK_fxf_error_t();
    var status = PK_FACE_make_blend(1, left, 1, right, PK_LOGICAL_true, PK_LOGICAL_true, options,
        &nSheetBodies, &sheetBodies, &nBlendTopols, &blendTopols, &unders, &ribs, &fault);
    faultCode = fault.fault;
    if (status != PK_ERROR_no_errors || nSheetBodies < 1 || sheetBodies is null || nBlendTopols < 1 || blendTopols is null)
    {
        if (sheetBodies is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free analytic blend bodies");
        if (blendTopols is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free analytic blend topols");
        if (unders is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free analytic blend unders");
        if (ribs.n_ribs != 0) ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f analytic blend");
        return false;
    }
    result = sheetBodies[0];
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free analytic blend bodies");
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free analytic blend topols");
    if (unders is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free analytic blend unders");
    if (ribs.n_ribs != 0) ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f analytic blend");
    return true;
}

static unsafe PK_BODY_t CreateAnalyticSheet(string kind)
{
    PK_SURF_t surface;
    switch (kind)
    {
        case "cylinder":
        {
            var form = new PK_CYL_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 2.0);
            PK_CYL_t value;
            ParasolidXtCorpusHost.Check(PK_CYL_create(&form, &value), "PK_CYL_create analytic blend");
            surface = value;
            break;
        }
        case "cone":
        {
            var form = new PK_CONE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 2.0, 0.35);
            PK_CONE_t value;
            ParasolidXtCorpusHost.Check(PK_CONE_create(&form, &value), "PK_CONE_create analytic blend");
            surface = value;
            break;
        }
        case "sphere":
        {
            var form = new PK_SPHERE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 2.0);
            PK_SPHERE_t value;
            ParasolidXtCorpusHost.Check(PK_SPHERE_create(&form, &value), "PK_SPHERE_create analytic blend");
            surface = value;
            break;
        }
        case "bsurf":
        {
            var vertices = stackalloc double[]
            {
                0.0, 0.0, 0.0, 1.0, 0.0, 0.2, 2.0, 0.0, 0.0,
                0.0, 1.0, 0.3, 1.0, 1.0, 0.8, 2.0, 1.0, 0.2,
                0.0, 2.0, 0.0, 1.0, 2.0, 0.4, 2.0, 2.0, 0.0,
            };
            var multiplicities = stackalloc int[] { 3, 3 };
            var knots = stackalloc double[] { 0.0, 1.0 };
            var form = new PK_BSURF_sf_t(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c,
                2, 2, multiplicities, multiplicities, knots, knots, PK_knot_uniform_c, PK_knot_quasi_uniform_c,
                PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false,
                PK_self_intersect_false_c, PK_convexity_arbitrary_c);
            PK_BSURF_t value;
            ParasolidXtCorpusHost.Check(PK_BSURF_create(&form, &value), "PK_BSURF_create analytic blend");
            surface = value;
            break;
        }
        case "swept":
        {
            var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(-2.0, 0.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
            PK_LINE_t line;
            ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create swept analytic blend");
            var sweptForm = new PK_SWEPT_sf_t(line, new PK_VECTOR1_t(0.0, 0.0, 1.0));
            PK_SWEPT_t value;
            ParasolidXtCorpusHost.Check(PK_SWEPT_create(&sweptForm, &value), "PK_SWEPT_create analytic blend");
            surface = value;
            break;
        }
        case "spun":
        {
            var form = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
            PK_LINE_t line;
            ParasolidXtCorpusHost.Check(PK_LINE_create(&form, &line), "PK_LINE_create spun analytic blend");
            var spunForm = new PK_SPUN_sf_t(line, new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
            PK_SPUN_t value;
            ParasolidXtCorpusHost.Check(PK_SPUN_create(&spunForm, &value), "PK_SPUN_create analytic blend");
            surface = value;
            break;
        }
        default:
            throw new InvalidOperationException("unsupported analytic blend surface " + kind);
    }
    PK_UVBOX_t uvBox;
    ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(surface, &uvBox), "PK_SURF_ask_uvbox analytic blend");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_SURF_make_sheet_body(surface, uvBox, &body), "PK_SURF_make_sheet_body analytic blend");
    return body;
}

static unsafe (PK_BODY_t Body, PK_FACE_t Left, PK_FACE_t Right, PK_CURVE_t Spine) CreatePlanePair()
{
    var leftBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(1, 0, 0), new PK_VECTOR1_t(0, 1, 0));
    var rightBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 1, 0), new PK_VECTOR1_t(1, 0, 0));
    PK_BODY_t leftBody;
    PK_BODY_t rightBody;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4, 4, &leftBasis, &leftBody), "PK_BODY_create_sheet_rectangle matrix left");
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4, 4, &rightBasis, &rightBody), "PK_BODY_create_sheet_rectangle matrix right");
    var left = FirstFace(leftBody);
    var right = FirstFace(rightBody);
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(0.25, 0.25, -1.5), new PK_VECTOR1_t(0, 0, 1)));
    PK_LINE_t spine;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &spine), "PK_LINE_create matrix spine");
    return (leftBody, left, right, spine);
}

static unsafe (PK_BODY_t Body, PK_FACE_t Left, PK_FACE_t Right, PK_CURVE_t Spine) CreateBSurfPlanePair()
{
    var vertices = stackalloc double[]
    {
        0, 0, 0, 1, 0, 0.2, 2, 0, 0,
        0, 1, 0.3, 1, 1, 0.8, 2, 1, 0.2,
        0, 2, 0, 1, 2, 0.4, 2, 2, 0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0, 1 };
    var form = new PK_BSURF_sf_t(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c,
        2, 2, multiplicities, multiplicities, knots, knots, PK_knot_uniform_c, PK_knot_quasi_uniform_c,
        PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c);
    PK_BSURF_t bsurf;
    ParasolidXtCorpusHost.Check(PK_BSURF_create(&form, &bsurf), "PK_BSURF_create matrix");
    PK_UVBOX_t uvBox;
    ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(bsurf, &uvBox), "PK_SURF_ask_uvbox matrix");
    PK_BODY_t bsurfBody;
    ParasolidXtCorpusHost.Check(PK_SURF_make_sheet_body(bsurf, uvBox, &bsurfBody), "PK_SURF_make_sheet_body matrix");
    var planeBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(1, 1, 0.2), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0));
    PK_BODY_t planeBody;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4, 4, &planeBasis, &planeBody), "PK_BODY_create_sheet_rectangle matrix plane");
    var left = FirstFace(bsurfBody);
    var right = FirstFace(planeBody);
    var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(0, 1, 0.45), new PK_VECTOR1_t(1, 0, 0)));
    PK_LINE_t spine;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &spine), "PK_LINE_create matrix dependent spine");
    return (bsurfBody, left, right, spine);
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces blend matrix");
    try
    {
        if (count == 0) throw new InvalidOperationException("blend matrix body has no face");
        return faces[0];
    }
    finally
    {
        if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free blend matrix faces");
    }
}

static unsafe void AssertBlendResult(PK_BODY_t body, PK_CLASS_t expectedSurfaceClass)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type blend matrix result");
    if (bodyType != PK_BODY_type_sheet_c)
        throw new InvalidOperationException("blend matrix result is not a sheet body: " + bodyType);
    var face = FirstFace(body);
    PK_SURF_t surface;
    PK_CLASS_t actualSurfaceClass;
    ParasolidXtCorpusHost.Check(PK_FACE_ask_surf(face, &surface), "PK_FACE_ask_surf blend matrix result");
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(surface, &actualSurfaceClass), "PK_ENTITY_ask_class blend matrix surface");
    if (actualSurfaceClass != expectedSurfaceClass)
        throw new InvalidOperationException("blend matrix surface class=" + actualSurfaceClass + ", expected=" + expectedSurfaceClass);
    if (expectedSurfaceClass == PK_CLASS_blendsf)
    {
        PK_BLENDSF_sf_t standardForm;
        ParasolidXtCorpusHost.Check(PK_BLENDSF_ask(surface, &standardForm), "PK_BLENDSF_ask blend matrix");
        if (standardForm.geom_1 <= 0 || standardForm.geom_2 <= 0 || standardForm.spine <= 0)
            throw new InvalidOperationException("blend matrix BLENDSF has missing supports or spine");
        if (Math.Abs(standardForm.radii[0]) <= 1.0e-10 || Math.Abs(standardForm.radii[1]) <= 1.0e-10)
            throw new InvalidOperationException("blend matrix BLENDSF has zero radii");
    }
}

static unsafe void AssertBlendAny(PK_BODY_t body)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type analytic blend result");
    if (bodyType != PK_BODY_type_sheet_c)
        throw new InvalidOperationException("analytic blend result is not a sheet body: " + bodyType);
    var face = FirstFace(body);
    PK_SURF_t surface;
    PK_CLASS_t surfaceClass;
    ParasolidXtCorpusHost.Check(PK_FACE_ask_surf(face, &surface), "PK_FACE_ask_surf analytic blend result");
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(surface, &surfaceClass), "PK_ENTITY_ask_class analytic blend result");
    if (surfaceClass is not (PK_CLASS_blendsf or PK_CLASS_cyl or PK_CLASS_cone or PK_CLASS_sphere or PK_CLASS_torus or PK_CLASS_bsurf))
        throw new InvalidOperationException("unexpected analytic blend surface class: " + surfaceClass);
}

readonly record struct BlendRecipe(
    int SeedKind,
    PK_blend_xs_plane_t Xsection,
    PK_blend_xs_shape_t CrossSectionShape,
    PK_LOGICAL_t Multiple,
    PK_LOGICAL_t HaveHelpPoint,
    PK_VECTOR_t HelpPoint,
    PK_LOGICAL_t LeftSense,
    PK_LOGICAL_t RightSense,
    PK_blend_walls_t Walls);
