#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Face-face blend fixtures.  Each uses two deterministic perpendicular sheet
// faces and an offset line spine.  PK_FACE_make_blend returns a sheet body
// containing the fixed blend face; the shared host inspects its transmitted
// dependency graph.
var cases = new CorpusCaseSpec[]
{
    new(
        "icurve.bsurf-plane.depth1",
        "PK_FACE_intersect_face",
        "Intersection curve returned for a bounded B-surface patch and a planar face.",
        new[] { "icurve", "icurve/face-face", "surface/bsurf", "surface/plane", "surface-pair/bsurf-plane", "topology/manifold" },
        CreateBSurfPlaneICurve,
        null,
        null,
        "{\"seed\":\"bsurf(0..2,0..2) + plane(z=0.2)\",\"depth\":1}",
        requiredSchemaNodes: new[] { "INTERSECTION" },
        requiredSchemaDependencies: new[] { "INTERSECTION->B_SURFACE", "INTERSECTION->PLANE" },
        typeCoverage: new[] { "geometry.icurve.depth.1", "geometry.surface-pair.bsurf-plane", "geometry.surface.bsurf", "geometry.surface.plane", "schema.node.SP_CURVE" },
        typedAsk: AssertICurve),
    new(
        "blend.fxf.plane-plane.rolling-ball.1",
        "PK_FACE_make_blend",
        "Constant rolling-ball face-face blend between two perpendicular planar walls.",
        new[] { "blend", "blend/face-face", "blend/constant", "blend/rolling-ball", "surface/plane", "topology/manifold" },
        () => CreateBlend(0.05, PK_LOGICAL_true, PK_LOGICAL_true),
        null,
        null,
        "{\"seed\":\"perpendicular-sheet-planes\",\"spine\":\"line(x=0.25,y=0.25)\",\"radius\":0.05,\"leftSense\":true,\"rightSense\":true,\"walls\":\"trim-both\",\"depth\":1}",
        requiredSchemaNodes: new[] { "CYLINDER" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.cylinder" },
        typedAsk: AssertBlendSheet),
    new(
        "blend.fxf.plane-plane.rolling-ball.1-reversed",
        "PK_FACE_make_blend",
        "Constant face-face blend between perpendicular planar walls with the right wall sense reversed.",
        new[] { "blend", "blend/face-face", "blend/constant", "blend/sense", "sense/reversed", "surface/plane" },
        () => CreateBlend(0.05, PK_LOGICAL_true, PK_LOGICAL_false),
        null,
        null,
        "{\"seed\":\"perpendicular-sheet-planes\",\"spine\":\"line(x=0.25,y=0.25)\",\"radius\":0.05,\"leftSense\":true,\"rightSense\":false,\"walls\":\"trim-both\",\"depth\":1}",
        requiredSchemaNodes: new[] { "CYLINDER" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.cylinder", "geometry.blend.sense.reversed" },
        typedAsk: AssertBlendSheet),
    new(
        "blend.fxf.bsurf-plane.rolling-ball.1",
        "PK_FACE_make_blend",
        "Face-face rolling-ball blend between a bounded B-surface and a plane.",
        new[] { "blend", "blend/face-face", "blend/rolling-ball", "surface/bsurf", "surface/plane" },
        CreateBSurfPlaneBlend,
        null,
        null,
        "{\"leftWall\":\"bounded B-surface patch\",\"rightWall\":\"plane(z=0.2)\",\"radius\":0.05,\"depth\":1}",
        requiredSchemaNodes: new[] { "BLENDED_EDGE", "BLEND_BOUND", "INTERSECTION" },
        requiredSchemaDependencies: new[] { "BLENDED_EDGE->B_SURFACE", "BLENDED_EDGE->PLANE", "BLENDED_EDGE->INTERSECTION", "BLEND_BOUND->BLENDED_EDGE", "BLEND_BOUND->PLANE" },
        typeCoverage: new[] { "geometry.blend.depth.1", "geometry.surface.bsurf", "geometry.surface.plane", "schema.node.BLENDED_EDGE", "schema.node.BLEND_BOUND" },
        typedAsk: AssertGenericBlendSheet),
    new(
        "blend.fxf.plane-plane-plane.nested.2",
        "PK_FACE_make_blend",
        "Nested face-face blend: the first blend's cylindrical face is blended against a third perpendicular plane.",
        new[] { "blend", "blend/face-face", "blend/nested", "blend/rolling-ball", "surface/plane", "topology/manifold" },
        CreateNestedBlend,
        null,
        null,
        "{\"seed\":\"perpendicular-sheet-planes\",\"firstRadius\":0.25,\"secondRadius\":0.05,\"spine\":\"line(x=0.25,y=0.25)+z\",\"walls\":\"trim-both\",\"depth\":2}",
        requiredSchemaNodes: new[] { "TORUS", "SP_CURVE", "B_CURVE" },
        requiredSchemaDependencies: new[] { "FACE->TORUS", "SP_CURVE->B_CURVE" },
        typeCoverage: new[] { "geometry.blend.depth.2", "geometry.blend.depth.1", "geometry.surface.torus" },
        typedAsk: AssertNestedBlendSheet),
};

return ParasolidXtCorpusHost.RunGroup("face-blends", cases, args);

static unsafe PK_BODY_t CreateNestedBlend()
{
    // Depth 2: blend the cylindrical face produced by a first plane/plane
    // blend against a third perpendicular plane.  The nested result depends
    // on blend-produced geometry rather than on a primitive wall.
    var seed = CreatePerpendicularPlaneSeed();
    var firstOptions = new PK_FACE_make_blend_o_t();
    firstOptions.shape.xsection = PK_blend_xs_rolling_ball_c;
    firstOptions.shape.radius = 0.25;
    firstOptions.shape.ratio = 1.0;
    firstOptions.walls = PK_blend_walls_trim_both_c;
    firstOptions.shape.parameter = seed.Spine;
    if (!TryBlend(seed.Left, seed.Right, PK_LOGICAL_true, PK_LOGICAL_true, &firstOptions, out var firstBody, out var firstFault))
        throw new InvalidOperationException("nested blend first stage failed (fault=" + firstFault + ")");

    int faceCount;
    PK_FACE_t* faces = null;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(firstBody, &faceCount, &faces), "PK_BODY_ask_faces nested blend first stage");
    PK_FACE_t blendFace;
    try
    {
        if (faceCount < 1)
            throw new InvalidOperationException("nested blend first stage has no face");
        blendFace = faces[0];
    }
    finally
    {
        if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free nested blend first faces");
    }

    var thirdBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0));
    PK_BODY_t thirdBody;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 4.0, &thirdBasis, &thirdBody), "PK_BODY_create_sheet_rectangle nested third plane");
    var secondOptions = new PK_FACE_make_blend_o_t();
    secondOptions.shape.xsection = PK_blend_xs_rolling_ball_c;
    secondOptions.shape.radius = 0.05;
    secondOptions.shape.ratio = 1.0;
    secondOptions.walls = PK_blend_walls_trim_both_c;
    secondOptions.shape.parameter = seed.Spine;
    if (TryBlend(blendFace, FirstFace(thirdBody), PK_LOGICAL_true, PK_LOGICAL_true, &secondOptions, out var result, out var fault))
        return result;
    throw new InvalidOperationException("nested blend second stage failed (fault=" + fault + ")");
}

static unsafe void AssertNestedBlendSheet(PK_BODY_t body)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type nested blend");
    if (bodyType != PK_BODY_type_sheet_c)
        throw new InvalidOperationException("nested blend result has body type " + bodyType + ", expected sheet");
    var face = FirstFace(body);
    PK_SURF_t surface;
    PK_CLASS_t surfaceClass;
    ParasolidXtCorpusHost.Check(PK_FACE_ask_surf(face, &surface), "PK_FACE_ask_surf nested blend");
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(surface, &surfaceClass), "PK_ENTITY_ask_class nested blend");
    if (surfaceClass is not (PK_CLASS_blendsf or PK_CLASS_torus))
        throw new InvalidOperationException("nested blend surface class is " + surfaceClass);
}

static unsafe PK_BODY_t CreateBlend(double radius, PK_LOGICAL_t leftSense, PK_LOGICAL_t rightSense)
{
    var options = new PK_FACE_make_blend_o_t();
    options.shape.xsection = PK_blend_xs_rolling_ball_c;
    options.shape.radius = radius;
    options.shape.ratio = 1.0;
    options.walls = PK_blend_walls_trim_both_c;

    // The parameter spine is offset from two perpendicular planes, rather
    // than lying on their common intersection.
    var seed = CreatePerpendicularPlaneSeed();
    options.shape.parameter = seed.Spine;
    if (TryBlend(seed.Left, seed.Right, leftSense, rightSense, &options, out var result, out var fault))
        return result;
    throw new InvalidOperationException("PK_FACE_make_blend found no stable perpendicular-sheet seed (fault=" + fault + ")");
}

static unsafe (PK_FACE_t Left, PK_FACE_t Right, PK_CURVE_t Spine) CreatePerpendicularPlaneSeed()
{
    var leftBasis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 1.0, 0.0));
    var rightBasis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 1.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    PK_BODY_t leftBody;
    PK_BODY_t rightBody;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 4.0, &leftBasis, &leftBody), "PK_BODY_create_sheet_rectangle blend left");
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 4.0, &rightBasis, &rightBody), "PK_BODY_create_sheet_rectangle blend right");
    var left = FirstFace(leftBody);
    var right = FirstFace(rightBody);
    var spineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.25, 0.25, -1.5),
        new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_LINE_t spine;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&spineForm, &spine), "PK_LINE_create blend spine");
    return (left, right, spine);
}

static unsafe PK_BODY_t CreateBSurfPlaneBlend()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 0.0, 0.2, 2.0, 0.0, 0.0,
        0.0, 1.0, 0.3, 1.0, 1.0, 0.8, 2.0, 1.0, 0.2,
        0.0, 2.0, 0.0, 1.0, 2.0, 0.4, 2.0, 2.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    var form = new PK_BSURF_sf_t(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices,
        PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots,
        PK_knot_uniform_c, PK_knot_quasi_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false,
        PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c);
    PK_BSURF_t bsurf;
    ParasolidXtCorpusHost.Check(PK_BSURF_create(&form, &bsurf), "PK_BSURF_create blend seed");
    PK_UVBOX_t uvBox;
    ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(bsurf, &uvBox), "PK_SURF_ask_uvbox blend seed");
    PK_BODY_t bsurfBody;
    ParasolidXtCorpusHost.Check(PK_SURF_make_sheet_body(bsurf, uvBox, &bsurfBody), "PK_SURF_make_sheet_body blend seed");
    var planeBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(1.0, 1.0, 0.2),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0));
    PK_BODY_t planeBody;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 4.0, &planeBasis, &planeBody), "PK_BODY_create_sheet_rectangle blend plane");
    var left = FirstFace(bsurfBody);
    var right = FirstFace(planeBody);
    var leftFaces = new[] { left };
    var rightFaces = new[] { right };
    var options = new PK_FACE_make_blend_o_t();
    options.shape.xsection = PK_blend_xs_rolling_ball_c;
    options.shape.radius = 0.05;
    options.walls = PK_blend_walls_trim_both_c;
    var spines = new[]
    {
        new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 1.0, 0.45), new PK_VECTOR1_t(1.0, 0.0, 0.0))),
        new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(1.0, 0.0, 0.45), new PK_VECTOR1_t(0.0, 1.0, 0.0))),
        new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(1.0, 1.0, 0.45), new PK_VECTOR1_t(0.0, 0.0, 1.0))),
    };
    PK_fxf_fault_t lastFault = PK_fxf_fault_no_fault_c;
    foreach (var spineForm in spines)
    {
        PK_LINE_t spine;
        ParasolidXtCorpusHost.Check(PK_LINE_create(&spineForm, &spine), "PK_LINE_create dependent blend spine");
        options.shape.parameter = spine;
        var error = TryBlendRaw(leftFaces, rightFaces, &options, out var result, out var fault);
        if (error)
            return result;
        lastFault = fault;
    }
    throw new InvalidOperationException("dependent face blend has no stable seed (last fault=" + lastFault + ")");
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
        if (sheetBodies is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free dependent blend bodies");
        if (blendTopols is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free dependent blend topols");
        if (unders is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free dependent blend unders");
        if (ribs.n_ribs != 0) ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f dependent blend");
        return false;
    }
    result = sheetBodies[0];
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free dependent blend bodies");
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free dependent blend topols");
    if (unders is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free dependent blend unders");
    if (ribs.n_ribs != 0) ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f dependent blend");
    return true;
}

static unsafe PK_BODY_t CreateBSurfPlaneICurve()
{
    var bsurfVertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 0.0, 0.2, 2.0, 0.0, 0.0,
        0.0, 1.0, 0.3, 1.0, 1.0, 0.8, 2.0, 1.0, 0.2,
        0.0, 2.0, 0.0, 1.0, 2.0, 0.4, 2.0, 2.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    var bsurfForm = new PK_BSURF_sf_t(
        2, 2, 3, 3, 3, PK_LOGICAL_false, bsurfVertices,
        PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities,
        knots, knots, PK_knot_uniform_c, PK_knot_quasi_uniform_c,
        PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false,
        PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c);
    PK_BSURF_t bsurf;
    ParasolidXtCorpusHost.Check(PK_BSURF_create(&bsurfForm, &bsurf), "PK_BSURF_create icurve seed");
    PK_UVBOX_t bsurfUvBox;
    ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(bsurf, &bsurfUvBox), "PK_SURF_ask_uvbox icurve bsurf");
    PK_BODY_t bsurfBody;
    ParasolidXtCorpusHost.Check(PK_SURF_make_sheet_body(bsurf, bsurfUvBox, &bsurfBody), "PK_SURF_make_sheet_body icurve bsurf");
    PK_BODY_t planeBody;
    var planeBasis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(1.0, 1.0, 0.2),
        new PK_VECTOR1_t(0.0, 0.0, 1.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(4.0, 4.0, &planeBasis, &planeBody), "PK_BODY_create_sheet_rectangle icurve plane");
    var bsurfFace = FirstFace(bsurfBody);
    var planeFace = FirstFace(planeBody);
    var options = new PK_FACE_intersect_face_o_t();
    int nVectors;
    PK_VECTOR_t* vectors = null;
    int nCurves;
    PK_CURVE_t* curves = null;
    PK_INTERVAL_t* bounds = null;
    PK_intersect_curve_t* types = null;
    ParasolidXtCorpusHost.Check(PK_FACE_intersect_face(bsurfFace, planeFace, &options, &nVectors, &vectors, &nCurves, &curves, &bounds, &types), "PK_FACE_intersect_face bsurf-plane");
    try
    {
        for (var i = 0; i < nCurves; i++)
        {
            PK_CLASS_t curveClass;
            ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(curves[i], &curveClass), "PK_ENTITY_ask_class icurve");
            if (curveClass != PK_CLASS_icurve)
                continue;
            // Keep the intersection curve as construction geometry on the
            // B-surface part; making a wire body converts it to a B-curve.
            var planeForm = new PK_PLANE_sf_t(new PK_AXIS2_sf_t(
                new PK_VECTOR_t(1.0, 1.0, 0.2),
                new PK_VECTOR1_t(0.0, 0.0, 1.0),
                new PK_VECTOR1_t(1.0, 0.0, 0.0)));
            PK_PLANE_t planeSurface;
            ParasolidXtCorpusHost.Check(PK_PLANE_create(&planeForm, &planeSurface), "PK_PLANE_create icurve support");
            var constructionSurface = stackalloc PK_GEOM_t[1] { planeSurface };
            ParasolidXtCorpusHost.Check(PK_PART_add_geoms(bsurfBody, 1, constructionSurface), "PK_PART_add_geoms icurve plane support");
            var constructionCurve = stackalloc PK_GEOM_t[1] { curves[i] };
            ParasolidXtCorpusHost.Check(PK_PART_add_geoms(bsurfBody, 1, constructionCurve), "PK_PART_add_geoms icurve");
            int commonSurfaceCount;
            PK_SURF_t* commonSurfaces = null;
            var commonError = PK_CURVE_find_surfs_common(curves[i], &commonSurfaceCount, &commonSurfaces);
            try
            {
                // On this kernel build the public common-surface inquiry does
                // not accept an ICURVE returned by FACE_intersect_face and
                // reports the precise geometry/topology mismatch.  The
                // intersection node still carries both supports, which is
                // asserted by the required XT dependency edges below.
                if (commonError != PK_ERROR_geom_topol_mismatch && commonError != PK_ERROR_no_errors)
                    ParasolidXtCorpusHost.Check(commonError, "PK_CURVE_find_surfs_common icurve");
                if (commonError == PK_ERROR_no_errors && (commonSurfaceCount < 2 || commonSurfaces is null))
                    throw new InvalidOperationException("ICURVE has fewer than two common supporting surfaces");
            }
            finally
            {
                if (commonSurfaces is not null)
                    ParasolidXtCorpusHost.Check(PK_MEMORY_free(commonSurfaces), "PK_MEMORY_free icurve common surfaces");
            }
            return bsurfBody;
        }
    }
    finally
    {
        if (vectors is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(vectors), "PK_MEMORY_free icurve vectors");
        if (curves is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free icurve curves");
        if (bounds is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(bounds), "PK_MEMORY_free icurve bounds");
        if (types is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(types), "PK_MEMORY_free icurve types");
    }
    throw new InvalidOperationException("PK_FACE_intersect_face returned no ICURVE (curves=" + nCurves + ", vectors=" + nVectors + ")");
}

static unsafe void AssertICurve(PK_BODY_t body)
{
    int curveCount;
    PK_CURVE_t* curves;
    ParasolidXtCorpusHost.Check(PK_PART_ask_construction_curves(body, &curveCount, &curves), "PK_PART_ask_construction_curves icurve result");
    try
    {
        if (curveCount < 1 || curves is null)
            throw new InvalidOperationException("ICURVE result has no construction curve");
        for (var i = 0; i < curveCount; i++)
        {
            PK_CLASS_t actual;
            ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(curves[i], &actual), "PK_ENTITY_ask_class icurve result");
            if (actual == PK_CLASS_icurve)
                return;
        }
        throw new InvalidOperationException("ICURVE result has no PK_CLASS_icurve construction curve");
    }
    finally
    {
        if (curves is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free icurve result curves");
    }
}

static unsafe bool TryBlend(PK_FACE_t leftFace, PK_FACE_t rightFace, PK_LOGICAL_t leftSense, PK_LOGICAL_t rightSense, PK_FACE_make_blend_o_t* options, out PK_BODY_t result, out PK_fxf_fault_t faultCode)
{
    result = default;
    faultCode = PK_fxf_fault_no_fault_c;
    var left = new[] { leftFace };
    var right = new[] { rightFace };
    int nSheetBodies;
    PK_BODY_t* sheetBodies = null;
    int nBlendTopols;
    PK_TOPOL_t* blendTopols = null;
    PK_TOPOL_array_t* unders = null;
    var ribs = new PK_blend_rib_r_t();
    var fault = new PK_fxf_error_t();
    var error = PK_FACE_make_blend(
        1, left, 1, right, leftSense, rightSense, options,
        &nSheetBodies, &sheetBodies, &nBlendTopols, &blendTopols,
        &unders, &ribs, &fault);
    if (error != PK_ERROR_no_errors)
    {
        faultCode = fault.fault;
        return false;
    }
    faultCode = fault.fault;
    if (nSheetBodies < 1 || sheetBodies is null || nBlendTopols < 1 || blendTopols is null)
    {
        if (sheetBodies is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free failed blend sheet bodies");
        if (blendTopols is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free failed blend topologies");
        if (unders is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free failed blend unders");
        if (ribs.n_ribs != 0)
            ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f failed blend ribs");
        return false;
    }

    result = sheetBodies[0];
    ParasolidXtCorpusHost.Check(PK_MEMORY_free(sheetBodies), "PK_MEMORY_free blend sheet bodies");
    if (blendTopols is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(blendTopols), "PK_MEMORY_free blend topologies");
    if (unders is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(unders), "PK_MEMORY_free blend unders");
    if (ribs.n_ribs != 0)
        ParasolidXtCorpusHost.Check(PK_blend_rib_r_f(&ribs), "PK_blend_rib_r_f blend ribs");
    return true;
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces first face");
    try
    {
        if (count == 0)
            throw new InvalidOperationException("body has no face");
        return faces[0];
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free first face list");
    }
}

static unsafe void AssertBlendSheet(PK_BODY_t body)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type blend result");
    if (bodyType != PK_BODY_type_sheet_c)
        throw new InvalidOperationException("blend result has body type " + bodyType + ", expected sheet");

    int faceCount;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces blend result");
    try
    {
        if (faceCount < 1)
            throw new InvalidOperationException("blend result has no faces");
        PK_SURF_t surface;
        ParasolidXtCorpusHost.Check(PK_FACE_ask_surf(faces[0], &surface), "PK_FACE_ask_surf blend result");
        PK_CLASS_t surfaceClass;
        ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(surface, &surfaceClass), "PK_ENTITY_ask_class blend surface");
        if (surfaceClass != PK_CLASS_cyl)
            throw new InvalidOperationException("expected canonical planar blend cylinder, got " + surfaceClass);
        PK_CYL_sf_t cylinder;
        ParasolidXtCorpusHost.Check(PK_CYL_ask(surface, &cylinder), "PK_CYL_ask blend surface");
        if (Math.Abs(cylinder.radius - 0.05) > 1.0e-8)
            throw new InvalidOperationException("blend cylinder radius mismatch: " + cylinder.radius);
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free blend result faces");
    }
}

static unsafe void AssertGenericBlendSheet(PK_BODY_t body)
{
    PK_BODY_type_t bodyType;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_type(body, &bodyType), "PK_BODY_ask_type dependent blend");
    if (bodyType != PK_BODY_type_sheet_c)
        throw new InvalidOperationException("dependent blend result has body type " + bodyType);
    int faceCount;
    PK_FACE_t* faces;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &faceCount, &faces), "PK_BODY_ask_faces dependent blend");
    try
    {
        if (faceCount < 1)
            throw new InvalidOperationException("dependent blend result has no face");
        PK_SURF_t surface;
        PK_CLASS_t surfaceClass;
        ParasolidXtCorpusHost.Check(PK_FACE_ask_surf(faces[0], &surface), "PK_FACE_ask_surf dependent blend");
        ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(surface, &surfaceClass), "PK_ENTITY_ask_class dependent blend");
        if (surfaceClass != PK_CLASS_blendsf)
            throw new InvalidOperationException("dependent blend surface class is " + surfaceClass + ", expected BLENDSF");
        PK_BLENDSF_sf_t blendSurface;
        ParasolidXtCorpusHost.Check(PK_BLENDSF_ask(surface, &blendSurface), "PK_BLENDSF_ask dependent blend");
        if (blendSurface.geom_1 <= 0 || blendSurface.geom_2 <= 0 || blendSurface.spine <= 0)
            throw new InvalidOperationException("dependent BLENDSF has missing support or spine");
        if (Math.Abs(blendSurface.radii[0]) <= 1.0e-10 || Math.Abs(blendSurface.radii[1]) <= 1.0e-10)
            throw new InvalidOperationException("dependent BLENDSF has zero radii");
        if (blendSurface.spine_extent.value[1] <= blendSurface.spine_extent.value[0])
            throw new InvalidOperationException("dependent BLENDSF has empty spine extent");
    }
    finally
    {
        if (faces is not null)
            ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free dependent blend faces");
    }
}
