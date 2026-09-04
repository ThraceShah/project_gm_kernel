#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property AssemblyName=ProbeXtGapRecipes
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Diagnostic probe for the five remaining typed corpus gaps:
//   1. topology.loop.hole / topology.loop.peripheral (PK_LOOP_type 5401/5402)
//   2. geometry.blend.depth.2 (nested PK_FACE_make_blend)
//   3. geometry.icurve.depth.2 (I_CURVE with dependent supporting geometry)
//   4. geometry.blend.pair.sphere-spun
// Prints the observed tokens/faults; changes nothing.

if (!ParasolidScriptHost.TryStartSession("XT gap recipe probe", out var session, out var skipMessage))
{
    Console.WriteLine(skipMessage);
    return 0;
}

unsafe
{
    using (session)
    {
        ProbeLoopTypes();
        ProbeNestedBlend();
        ProbeDependentICurve();
        ProbeSphereSpunBlend();
    }
}
return 0;

static void Check(PK_ERROR_code_t error, string name)
{
    if (error != PK_ERROR_no_errors)
        throw new InvalidOperationException(name + " failed: " + error);
}

// ---------- 1. loop tokens ----------

static unsafe void ProbeLoopTypes()
{
    Console.WriteLine("== probe loop types ==");
    // solid block with through cylindrical hole (subtract)
    var basis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0));
    PK_BODY_t block;
    Check(PK_BODY_create_solid_block(4, 4, 2, &basis, &block), "create block");
    var cylBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, -1), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0));
    PK_BODY_t cylinder;
    Check(PK_BODY_create_solid_cyl(0.5, 4, &cylBasis, &cylinder), "create cyl");
    var booleanOptions = new PK_BODY_boolean_o_t();
    int nBodies;
    PK_BODY_t* bodies = null;
    Check(PK_BODY_boolean(block, 1, &cylinder, &booleanOptions, &nBodies, &bodies), "boolean subtract");
    try
    {
        DumpBodyLoops(nBodies > 0 ? bodies[0] : block, "solid-through-hole");
    }
    finally { if (bodies != null) Check(PK_MEMORY_free(bodies), "free bodies"); }

    // planar sheet with imprinted circle (inner loop on planar face)
    PK_BODY_t sheet;
    Check(PK_BODY_create_sheet_rectangle(4, 4, &basis, &sheet), "create sheet");
    var circleForm = new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)), 1.0);
    PK_CIRCLE_t circle;
    Check(PK_CIRCLE_create(&circleForm, &circle), "create circle");
    int faceCount;
    PK_FACE_t* faces = null;
    Check(PK_BODY_ask_faces(sheet, &faceCount, &faces), "ask faces");
    PK_FACE_t face = faces[0];
    Check(PK_MEMORY_free(faces), "free faces");
    int nNewEdges; PK_EDGE_t* newEdges = null; int nNewFaces; PK_FACE_t* newFaces = null;
    var bounds = new PK_INTERVAL_t(0, Math.PI * 2);
    var imprintError = PK_FACE_imprint_curve(face, circle, bounds, &nNewEdges, &newEdges, &nNewFaces, &newFaces);
    Console.WriteLine("imprint circle: " + imprintError + " edges=" + nNewEdges + " faces=" + nNewFaces);
    if (newEdges != null) Check(PK_MEMORY_free(newEdges), "free imprint edges");
    if (newFaces != null) Check(PK_MEMORY_free(newFaces), "free imprint faces");
    DumpBodyLoops(sheet, "sheet-imprint-circle");

    // full cylinder sheet body
    var cylForm = new PK_CYL_sf_t(basis, 1.0);
    PK_CYL_t cylSurf;
    Check(PK_CYL_create(&cylForm, &cylSurf), "create cyl surf");
    PK_UVBOX_t box;
    Check(PK_SURF_ask_uvbox(cylSurf, &box), "ask uvbox");
    PK_BODY_t cylSheet;
    Check(PK_SURF_make_sheet_body(cylSurf, box, &cylSheet), "make cyl sheet");
    DumpBodyLoops(cylSheet, "cylinder-sheet");
}

static unsafe void DumpBodyLoops(PK_BODY_t body, string label)
{
    int faceCount;
    PK_FACE_t* faces = null;
    Check(PK_BODY_ask_faces(body, &faceCount, &faces), "ask faces " + label);
    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            int loopCount;
            PK_LOOP_t* loops = null;
            Check(PK_FACE_ask_loops(faces[i], &loopCount, &loops), "ask loops " + label);
            try
            {
                for (var j = 0; j < loopCount; j++)
                {
                    PK_LOOP_type_t type;
                    Check(PK_LOOP_ask_type(loops[j], &type), "ask loop type " + label);
                    Console.WriteLine($"{label}: face {i} loop {j} type={(int)type} ({type})");
                }
            }
            finally { if (loops != null) Check(PK_MEMORY_free(loops), "free loops"); }
        }
    }
    finally { if (faces != null) Check(PK_MEMORY_free(faces), "free faces"); }
}

// ---------- 2. nested blend ----------

static unsafe void ProbeNestedBlend()
{
    Console.WriteLine("== probe nested blend (depth 2) ==");
    // First blend: two perpendicular planes -> cylinder blend face.
    var leftBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(1, 0, 0), new PK_VECTOR1_t(0, 1, 0));
    var rightBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 1, 0), new PK_VECTOR1_t(1, 0, 0));
    PK_BODY_t leftBody, rightBody;
    Check(PK_BODY_create_sheet_rectangle(4, 4, &leftBasis, &leftBody), "left rect");
    Check(PK_BODY_create_sheet_rectangle(4, 4, &rightBasis, &rightBody), "right rect");
    var leftFace = FirstFace(leftBody);
    var rightFace = FirstFace(rightBody);

    var firstOptions = new PK_FACE_make_blend_o_t();
    firstOptions.shape.xsection = PK_blend_xs_rolling_ball_c;
    firstOptions.shape.radius = 0.25;
    firstOptions.shape.ratio = 1.0;
    firstOptions.walls = PK_blend_walls_trim_both_c;
    var spineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(0.25, 0.25, -1.5), new PK_VECTOR1_t(0, 0, 1)));
    PK_LINE_t spine;
    Check(PK_LINE_create(&spineForm, &spine), "spine");
    firstOptions.shape.parameter = spine;

    if (!TryBlend(leftFace, rightFace, &firstOptions, out var blendBody, out var fault))
    {
        Console.WriteLine("first blend failed: fault=" + fault);
        return;
    }
    Console.WriteLine("first blend ok");
    // Inspect faces of the blend result.
    int faceCount;
    PK_FACE_t* faces = null;
    Check(PK_BODY_ask_faces(blendBody, &faceCount, &faces), "ask blend faces");
    try
    {
        for (var i = 0; i < faceCount; i++)
        {
            PK_SURF_t surface;
            Check(PK_FACE_ask_surf(faces[i], &surface), "ask surf");
            PK_CLASS_t cls;
            Check(PK_ENTITY_ask_class(surface, &cls), "ask class");
            Console.WriteLine("first blend face " + i + " class=" + cls);
        }

        // Second blend: blend face 0 of the blend result against a new perpendicular plane.
        var thirdBasis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0));
        PK_BODY_t thirdBody;
        Check(PK_BODY_create_sheet_rectangle(4, 4, &thirdBasis, &thirdBody), "third rect");
        var thirdFace = FirstFace(thirdBody);
        for (var i = 0; i < faceCount; i++)
        {
            for (var radius = 0.05; radius <= 0.4; radius += 0.05)
            {
                var secondOptions = new PK_FACE_make_blend_o_t();
                secondOptions.shape.xsection = PK_blend_xs_rolling_ball_c;
                secondOptions.shape.radius = radius;
                secondOptions.shape.ratio = 1.0;
                secondOptions.walls = PK_blend_walls_trim_both_c;
                for (var d = 0.0; d <= 1.5; d += 0.25)
                {
                    var secondSpineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(0.25 + d, 0.25, -1.5), new PK_VECTOR1_t(0, 0, 1)));
                    PK_LINE_t secondSpine;
                    Check(PK_LINE_create(&secondSpineForm, &secondSpine), "second spine");
                    secondOptions.shape.parameter = secondSpine;
                    foreach (var senses in new[] { (true, true), (true, false), (false, true) })
                    {
                        if (TryBlendSense(faces[i], thirdFace, senses.Item1, senses.Item2, &secondOptions, out var nested, out var nestedFault))
                        {
                            PK_BODY_type_t type;
                            Check(PK_BODY_ask_type(nested, &type), "ask nested type");
                            Console.WriteLine($"NESTED BLEND OK: face={i} radius={radius} offset={d} senses={senses} bodyType={type}");
                            return;
                        }
                        else if ((int)nestedFault != 0 && (int)nestedFault != 17461 && (int)nestedFault != 17462)
                        {
                            Console.WriteLine($"nested try face={i} r={radius} d={d} senses={senses}: fault={nestedFault}");
                        }
                    }
                }
            }
        }
    }
    finally { if (faces != null) Check(PK_MEMORY_free(faces), "free blend faces"); }
}

static unsafe bool TryBlend(PK_FACE_t left, PK_FACE_t right, PK_FACE_make_blend_o_t* options, out PK_BODY_t result, out PK_fxf_fault_t fault)
    => TryBlendSense(left, right, true, true, options, out result, out fault);

static unsafe bool TryBlendSense(PK_FACE_t left, PK_FACE_t right, bool leftSense, bool rightSense, PK_FACE_make_blend_o_t* options, out PK_BODY_t result, out PK_fxf_fault_t fault)
{
    result = default;
    fault = PK_fxf_fault_no_fault_c;
    var leftFaces = new[] { left };
    var rightFaces = new[] { right };
    int nSheetBodies; PK_BODY_t* sheetBodies = null;
    int nBlendTopols; PK_TOPOL_t* blendTopols = null;
    PK_TOPOL_array_t* unders = null;
    var ribs = new PK_blend_rib_r_t();
    var faultInfo = new PK_fxf_error_t();
    var error = PK_FACE_make_blend(1, leftFaces, 1, rightFaces,
        leftSense ? PK_LOGICAL_true : PK_LOGICAL_false,
        rightSense ? PK_LOGICAL_true : PK_LOGICAL_false,
        options, &nSheetBodies, &sheetBodies, &nBlendTopols, &blendTopols, &unders, &ribs, &faultInfo);
    fault = faultInfo.fault;
    if (error != PK_ERROR_no_errors || nSheetBodies < 1 || sheetBodies == null)
    {
        if (sheetBodies != null) Check(PK_MEMORY_free(sheetBodies), "free bodies");
        if (blendTopols != null) Check(PK_MEMORY_free(blendTopols), "free topols");
        if (unders != null) Check(PK_MEMORY_free(unders), "free unders");
        if (ribs.n_ribs != 0) Check(PK_blend_rib_r_f(&ribs), "free ribs");
        return false;
    }
    result = sheetBodies[0];
    Check(PK_MEMORY_free(sheetBodies), "free bodies");
    if (blendTopols != null) Check(PK_MEMORY_free(blendTopols), "free topols");
    if (unders != null) Check(PK_MEMORY_free(unders), "free unders");
    if (ribs.n_ribs != 0) Check(PK_blend_rib_r_f(&ribs), "free ribs");
    return true;
}

// ---------- 3. dependent-support I_CURVE ----------

static unsafe void ProbeDependentICurve()
{
    Console.WriteLine("== probe dependent I_CURVE (depth 2) ==");
    // Spun surface whose profile is a B-curve (dependent profile), intersected with a B-surface.
    var profile = CreateWarpedBCurve();
    var spunForm = new PK_SPUN_sf_t(profile, new PK_AXIS1_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1)));
    PK_SPUN_t spun;
    Check(PK_SPUN_create(&spunForm, &spun), "create spun");
    TryIntersect("spun(bcurve)-bsurf", spun, "bsurf");
    // Swept surface whose path/profile is a B-curve.
    var sweptForm = new PK_SWEPT_sf_t(profile, new PK_VECTOR1_t(0, 0, 1));
    PK_SWEPT_t swept;
    Check(PK_SWEPT_create(&sweptForm, &swept), "create swept");
    TryIntersect("swept(bcurve)-bsurf", swept, "bsurf");
    TryIntersect("swept(bcurve)-cylinder", swept, "cylinder");
    // Offset of a warped B-surface.
    var bsurf = CreateWarpedBSurf();
    PK_SURF_t offset;
    var offsetError = PK_SURF_offset(bsurf, 0.3, &offset);
    Console.WriteLine("offset bsurf: " + offsetError);
    if (offsetError == PK_ERROR_no_errors)
        TryIntersect("offset(bsurf)-bsurf", offset, "bsurf2");
}

static unsafe PK_CURVE_t CreateWarpedBCurve()
{
    var poles = stackalloc double[]
    {
        1.2, 0, -1,
        1.8, 0, 1,
        2.6, 0, -1,
        3.4, 0, 1,
    };
    var multiplicities = stackalloc int[] { 4, 4 };
    var knots = stackalloc double[] { 0, 1 };
    var form = new PK_BCURVE_sf_t(3, 4, 3, PK_LOGICAL_false, poles,
        PK_BCURVE_form_arbitrary_c, 2, multiplicities, knots,
        PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c);
    PK_BCURVE_t curve;
    Check(PK_BCURVE_create(&form, &curve), "create bcurve");
    return curve;
}

static unsafe PK_SURF_t CreateWarpedBSurf()
{
    var vertices = stackalloc double[]
    {
        -3, -3, -1, 0, -3, 1, 3, -3, -1,
        -3, 0, 1, 0, 0, -1, 3, 0, 1,
        -3, 3, -1, 0, 3, 1, 3, 3, -1,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0, 1 };
    var form = new PK_BSURF_sf_t(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices,
        PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots,
        PK_knot_bezier_ends_c, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false,
        PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c);
    PK_BSURF_t bsurf;
    Check(PK_BSURF_create(&form, &bsurf), "create bsurf");
    return bsurf;
}

static unsafe void TryIntersect(string label, PK_SURF_t leftSurface, string rightKind)
{
    PK_UVBOX_t leftBox;
    Check(PK_SURF_ask_uvbox(leftSurface, &leftBox), "ask left uvbox " + label);
    PK_BODY_t leftBody;
    Check(PK_SURF_make_sheet_body(leftSurface, leftBox, &leftBody), "make left sheet " + label);
    PK_SURF_t rightSurface = rightKind == "cylinder"
        ? CreateCylinder()
        : CreateWarpedBSurf();
    PK_BODY_t rightBody;
    PK_UVBOX_t rightBox;
    Check(PK_SURF_ask_uvbox(rightSurface, &rightBox), "ask right uvbox " + label);
    Check(PK_SURF_make_sheet_body(rightSurface, rightBox, &rightBody), "make right sheet " + label);
    var leftFace = FirstFace(leftBody);
    var rightFace = FirstFace(rightBody);
    var options = new PK_FACE_intersect_face_o_t();
    int nVectors; PK_VECTOR_t* vectors = null;
    int nCurves; PK_CURVE_t* curves = null;
    PK_INTERVAL_t* bounds = null;
    PK_intersect_curve_t* types = null;
    var error = PK_FACE_intersect_face(leftFace, rightFace, &options, &nVectors, &vectors, &nCurves, &curves, &bounds, &types);
    Console.WriteLine(label + ": " + error);
    if (error == PK_ERROR_no_errors)
    {
        for (var i = 0; i < nCurves; i++)
        {
            PK_CLASS_t cls;
            Check(PK_ENTITY_ask_class(curves[i], &cls), "ask class " + label);
            Console.WriteLine($"  {label}: curve {i} class={cls} type={types[i]}");
        }
    }
    if (vectors != null) Check(PK_MEMORY_free(vectors), "free vectors");
    if (curves != null) Check(PK_MEMORY_free(curves), "free curves");
    if (bounds != null) Check(PK_MEMORY_free(bounds), "free bounds");
    if (types != null) Check(PK_MEMORY_free(types), "free types");
}

static unsafe PK_SURF_t CreateCylinder()
{
    var form = new PK_CYL_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)), 2);
    PK_CYL_t cylinder;
    Check(PK_CYL_create(&form, &cylinder), "create cylinder");
    return cylinder;
}

// ---------- 4. sphere/spun blend ----------

static unsafe void ProbeSphereSpunBlend()
{
    Console.WriteLine("== probe sphere/spun blend ==");
    for (var sphereRadius = 2.0; sphereRadius <= 4.0; sphereRadius += 1.0)
    for (var lineOffset = 2.2; lineOffset <= 4.0; lineOffset += 0.6)
    for (var blendRadius = 0.05; blendRadius <= 0.4; blendRadius += 0.1)
    {
        var sphereForm = new PK_SPHERE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0)), sphereRadius);
        PK_SPHERE_t sphere;
        Check(PK_SPHERE_create(&sphereForm, &sphere), "create sphere");
        PK_UVBOX_t sphereBox;
        Check(PK_SURF_ask_uvbox(sphere, &sphereBox), "ask sphere uvbox");
        PK_BODY_t sphereBody;
        Check(PK_SURF_make_sheet_body(sphere, sphereBox, &sphereBody), "make sphere sheet");
        var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(lineOffset, 0, -3), new PK_VECTOR1_t(0, 0, 1)));
        PK_LINE_t line;
        Check(PK_LINE_create(&lineForm, &line), "create spun profile");
        var spunForm = new PK_SPUN_sf_t(line, new PK_AXIS1_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1)));
        PK_SPUN_t spun;
        Check(PK_SPUN_create(&spunForm, &spun), "create spun");
        PK_UVBOX_t spunBox;
        Check(PK_SURF_ask_uvbox(spun, &spunBox), "ask spun uvbox");
        PK_BODY_t spunBody;
        Check(PK_SURF_make_sheet_body(spun, spunBox, &spunBody), "make spun sheet");

        var options = new PK_FACE_make_blend_o_t();
        options.shape.xsection = PK_blend_xs_rolling_ball_c;
        options.shape.radius = blendRadius;
        options.shape.ratio = 1.0;
        options.walls = PK_blend_walls_trim_both_c;
        var sphereFace = FirstFace(sphereBody);
        var spunFace = FirstFace(spunBody);
        var found = false;
        for (var spineOffset = 0.2; spineOffset <= 2.0; spineOffset += 0.3)
        for (var up = 0.2; up <= 2.0; up += 0.6)
        {
            var spine2 = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(lineOffset - spineOffset, up, -2), new PK_VECTOR1_t(0, 0, 1)));
            PK_LINE_t spine;
            Check(PK_LINE_create(&spine2, &spine), "create spine");
            options.shape.parameter = spine;
            if (TryBlend(sphereFace, spunFace, &options, out _, out var fault))
            {
                Console.WriteLine($"SPHERE-SPUN BLEND OK: sphereR={sphereRadius} lineOffset={lineOffset} blendR={blendRadius} spineOffset={spineOffset} up={up}");
                found = true;
                break;
            }
        }
        if (found) return;
    }
    Console.WriteLine("no sphere/spun seed succeeded");
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces = null;
    Check(PK_BODY_ask_faces(body, &count, &faces), "ask faces");
    try
    {
        if (count < 1 || faces == null) throw new InvalidOperationException("body has no face");
        return faces[0];
    }
    finally { if (faces != null) Check(PK_MEMORY_free(faces), "free faces"); }
}
