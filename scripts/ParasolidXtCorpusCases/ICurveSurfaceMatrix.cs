#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Deterministic intersection seeds.  A warped B-surface is used where the
// analytic/analytic intersection would be represented by a line or circle;
// this forces Parasolid to retain the two supporting surfaces in an I_CURVE.
// The same seed is reused in both orders so left/right ownership is observable
// without relying on a random geometric search.
var cases = new CorpusCaseSpec[]
{
    Pair("bsurf", "cylinder", "icurve.bsurf-cylinder.depth1", "B-surface/cylinder intersection curve.", "geometry.surface-pair.bsurf-cylinder"),
    Pair("cylinder", "bsurf", "icurve.cylinder-bsurf.depth1-reversed", "Cylinder/B-surface intersection curve with reversed face order.", "geometry.surface-pair.cylinder-bsurf"),
    Pair("bsurf", "cone", "icurve.bsurf-cone.depth1", "B-surface/cone intersection curve.", "geometry.surface-pair.bsurf-cone"),
    Pair("bsurf", "sphere", "icurve.bsurf-sphere.depth1", "B-surface/sphere intersection curve.", "geometry.surface-pair.bsurf-sphere"),
    Pair("bsurf", "torus", "icurve.bsurf-torus.depth1", "B-surface/torus intersection curve across the periodic torus parameter seam.", "geometry.surface-pair.bsurf-torus", "periodic"),
    Pair("bsurf", "swept", "icurve.bsurf-swept.depth1", "B-surface/swept-surface intersection curve.", "geometry.surface-pair.bsurf-swept"),
    Pair("bsurf", "spun", "icurve.bsurf-spun.depth1", "B-surface/spun-surface intersection curve.", "geometry.surface-pair.bsurf-spun"),
    Pair("cone", "swept", "icurve.cone-swept.depth1", "Cone/swept-surface intersection curve.", "geometry.surface-pair.cone-swept"),
    Pair("sphere", "swept", "icurve.sphere-swept.depth1", "Sphere/swept-surface intersection curve.", "geometry.surface-pair.sphere-swept"),
    Pair("bsurf", "cylinder", "icurve.bsurf-cylinder.depth1-seed-vector", "B-surface/cylinder intersection branch selected by an explicit seed vector.", "geometry.surface-pair.bsurf-cylinder.seed-vector", "seed-vector"),
    Pair("bsurf", "cylinder", "icurve.bsurf-cylinder.depth1-box", "B-surface/cylinder intersection restricted to a finite spatial box.", "geometry.surface-pair.bsurf-cylinder.box", "box"),
    Pair("bsurf", "cylinder", "icurve.bsurf-cylinder.depth1-reverse-face", "B-surface/cylinder intersection with the right face orientation reversed.", "geometry.surface-pair.bsurf-cylinder.reverse-face", "reverse-face"),
};

return ParasolidXtCorpusHost.RunGroup("icurve-surface-matrix", cases, args);

static CorpusCaseSpec Pair(string leftKind, string rightKind, string id, string description, string pairLabel, string variant = "default")
{
    var leftNode = Node(leftKind);
    var rightNode = Node(rightKind);
    var typeCoverage = new List<string> { "geometry.icurve.depth.1", pairLabel, "geometry.surface." + leftKind, "geometry.surface." + rightKind, "schema.node.INTERSECTION" };
    if (variant == "periodic")
        typeCoverage.Add("geometry.icurve.periodic-support");
    return new CorpusCaseSpec(
        id,
        "PK_FACE_intersect_face",
        description,
        new[] { "icurve", "icurve/face-face", "surface-pair", "surface/" + leftKind, "surface/" + rightKind },
        () => CreateIntersection(leftKind, rightKind, variant),
        null,
        null,
        "{\"leftSurface\":\"" + leftKind + "\",\"rightSurface\":\"" + rightKind + "\",\"depth\":1,\"faceOrder\":\"" + leftKind + "->" + rightKind + "\",\"variant\":\"" + variant + "\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "INTERSECTION", leftNode, rightNode },
        requiredSchemaDependencies: new[] { "INTERSECTION->" + leftNode, "INTERSECTION->" + rightNode },
        typeCoverage: typeCoverage,
        typedAsk: AssertICurve);
}

static string Node(string kind) => kind switch
{
    "plane" => "PLANE",
    "cylinder" => "CYLINDER",
    "cone" => "CONE",
    "sphere" => "SPHERE",
    "torus" => "TORUS",
    "bsurf" => "B_SURFACE",
    "offset" => "OFFSET",
    "swept" => "SWEPT_SURF",
    "spun" => "SPUN_SURF",
    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
};

static unsafe PK_BODY_t CreateIntersection(string leftKind, string rightKind, string variant)
{
    var leftSurface = CreateSurface(leftKind);
    var rightSurface = CreateSurface(rightKind);
    var rightSupport = CreateSurface(rightKind);
    var leftBody = MakeSheetBody(leftSurface, leftKind);
    var rightBody = MakeSheetBody(rightSurface, rightKind);
    if (variant == "reverse-face")
        ParasolidXtCorpusHost.Check(PK_BODY_reverse_orientation(rightBody), "PK_BODY_reverse_orientation I_CURVE right body");
    var leftFace = FirstFace(leftBody);
    var rightFace = FirstFace(rightBody);

    var options = new PK_FACE_intersect_face_o_t();
    if (variant == "seed-vector")
    {
        options.have_vector = PK_LOGICAL_true;
        options.vector = new PK_VECTOR_t(2.0, 0.0, 0.3333333333333333);
    }
    else if (variant == "box")
    {
        options.have_box = PK_LOGICAL_true;
        options.box = new PK_BOX_t(-3.0, -3.0, -2.0, 3.0, 3.0, 2.0);
    }
    else if (variant == "uvboxes")
    {
        ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(leftSurface, &options.uvbox_1), "PK_SURF_ask_uvbox I_CURVE left option");
        ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(rightSurface, &options.uvbox_2), "PK_SURF_ask_uvbox I_CURVE right option");
        options.have_uvbox_1 = PK_LOGICAL_true;
        options.have_uvbox_2 = PK_LOGICAL_true;
    }
    else if (variant == "mixed-both")
    {
        options.mixed_curve_category = PK_mixed_intersection_both_c;
    }
    int nVectors;
    PK_VECTOR_t* vectors = null;
    int nCurves;
    PK_CURVE_t* curves = null;
    PK_INTERVAL_t* bounds = null;
    PK_intersect_curve_t* types = null;
    var returnedClasses = new List<PK_CLASS_t>();
    ParasolidXtCorpusHost.Check(PK_FACE_intersect_face(leftFace, rightFace, &options,
        &nVectors, &vectors, &nCurves, &curves, &bounds, &types),
        "PK_FACE_intersect_face " + leftKind + "/" + rightKind);
    try
    {
        for (var i = 0; i < nCurves; i++)
        {
            PK_CLASS_t curveClass;
            ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(curves[i], &curveClass), "PK_ENTITY_ask_class I_CURVE");
            returnedClasses.Add(curveClass);
            if (curveClass != PK_CLASS_icurve)
                continue;

            // The left face already owns leftSurface.  Add a distinct but
            // geometrically identical right support before adding the curve;
            // this makes both support references transmit in the result part.
            // A torus intersection may return several curves that share an
            // internal seam dependency.  In that case the right support is
            // already retained by the returned I_CURVE and adding a second
            // torus object is rejected as bad_shared_dep (917).
            if (rightKind != "torus")
            {
                var support = stackalloc PK_GEOM_t[1] { rightSupport };
                ParasolidXtCorpusHost.Check(PK_PART_add_geoms(leftBody, 1, support), "PK_PART_add_geoms I_CURVE support");
            }
            if (rightKind == "torus")
            {
                var allCurves = stackalloc PK_GEOM_t[nCurves];
                var nIntersectionCurves = 0;
                for (var j = 0; j < nCurves; j++)
                {
                    PK_CLASS_t candidateClass;
                    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(curves[j], &candidateClass), "PK_ENTITY_ask_class torus I_CURVE");
                    if (candidateClass == PK_CLASS_icurve)
                        allCurves[nIntersectionCurves++] = curves[j];
                }
                ParasolidXtCorpusHost.Check(PK_PART_add_geoms(leftBody, nIntersectionCurves, allCurves), "PK_PART_add_geoms I_CURVE");
            }
            else
            {
                var resultCurve = stackalloc PK_GEOM_t[1] { curves[i] };
                ParasolidXtCorpusHost.Check(PK_PART_add_geoms(leftBody, 1, resultCurve), "PK_PART_add_geoms I_CURVE");
            }
            return leftBody;
        }
    }
    finally
    {
        if (vectors is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(vectors), "PK_MEMORY_free I_CURVE vectors");
        if (curves is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free I_CURVE curves");
        if (bounds is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(bounds), "PK_MEMORY_free I_CURVE bounds");
        if (types is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(types), "PK_MEMORY_free I_CURVE types");
    }
    throw new InvalidOperationException("PK_FACE_intersect_face returned no I_CURVE for " + leftKind + "/" + rightKind + " (curves=" + nCurves + ", classes=" + string.Join(",", returnedClasses) + ")");
}

static unsafe PK_BODY_t MakeSheetBody(PK_SURF_t surface, string kind)
{
    PK_UVBOX_t box;
    ParasolidXtCorpusHost.Check(PK_SURF_ask_uvbox(surface, &box), "PK_SURF_ask_uvbox " + kind);
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_SURF_make_sheet_body(surface, box, &body), "PK_SURF_make_sheet_body " + kind);
    return body;
}

static unsafe PK_SURF_t CreateSurface(string kind)
{
    var origin = new PK_VECTOR_t(0, 0, 0);
    var z = new PK_VECTOR1_t(0, 0, 1);
    var x = new PK_VECTOR1_t(1, 0, 0);
    var basis = new PK_AXIS2_sf_t(origin, z, x);
    switch (kind)
    {
        case "plane":
        {
            var form = new PK_PLANE_sf_t(basis); PK_PLANE_t plane;
            ParasolidXtCorpusHost.Check(PK_PLANE_create(&form, &plane), "PK_PLANE_create I_CURVE plane"); return plane;
        }
        case "cylinder":
        {
            var form = new PK_CYL_sf_t(basis, 2); PK_CYL_t cylinder;
            ParasolidXtCorpusHost.Check(PK_CYL_create(&form, &cylinder), "PK_CYL_create I_CURVE cylinder"); return cylinder;
        }
        case "cone":
        {
            var form = new PK_CONE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, -1), z, x), 2, .4); PK_CONE_t cone;
            ParasolidXtCorpusHost.Check(PK_CONE_create(&form, &cone), "PK_CONE_create I_CURVE cone"); return cone;
        }
        case "sphere":
        {
            var form = new PK_SPHERE_sf_t(basis, 2); PK_SPHERE_t sphere;
            ParasolidXtCorpusHost.Check(PK_SPHERE_create(&form, &sphere), "PK_SPHERE_create I_CURVE sphere"); return sphere;
        }
        case "torus":
        {
            var form = new PK_TORUS_sf_t(basis, 4, 1); PK_TORUS_t torus;
            ParasolidXtCorpusHost.Check(PK_TORUS_create(&form, &torus), "PK_TORUS_create I_CURVE torus"); return torus;
        }
        case "bsurf":
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
            ParasolidXtCorpusHost.Check(PK_BSURF_create(&form, &bsurf), "PK_BSURF_create I_CURVE bsurf"); return bsurf;
        }
        case "offset":
        {
            var form = new PK_PLANE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), z, x)); PK_PLANE_t plane;
            ParasolidXtCorpusHost.Check(PK_PLANE_create(&form, &plane), "PK_PLANE_create I_CURVE offset base");
            PK_SURF_t offset; ParasolidXtCorpusHost.Check(PK_SURF_offset(plane, .5, &offset), "PK_SURF_offset I_CURVE"); return offset;
        }
        case "swept":
        {
            var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(-3, 0, 0), x)); PK_LINE_t line;
            ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create I_CURVE swept profile");
            var form = new PK_SWEPT_sf_t(line, z); PK_SWEPT_t swept;
            ParasolidXtCorpusHost.Check(PK_SWEPT_create(&form, &swept), "PK_SWEPT_create I_CURVE swept"); return swept;
        }
        case "spun":
        {
            var lineForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(new PK_VECTOR_t(2, 0, -3), z)); PK_LINE_t line;
            ParasolidXtCorpusHost.Check(PK_LINE_create(&lineForm, &line), "PK_LINE_create I_CURVE spun profile");
            var form = new PK_SPUN_sf_t(line, new PK_AXIS1_sf_t(origin, z)); PK_SPUN_t spun;
            ParasolidXtCorpusHost.Check(PK_SPUN_create(&form, &spun), "PK_SPUN_create I_CURVE spun"); return spun;
        }
        default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }
}

static unsafe PK_FACE_t FirstFace(PK_BODY_t body)
{
    int count; PK_FACE_t* faces = null;
    ParasolidXtCorpusHost.Check(PK_BODY_ask_faces(body, &count, &faces), "PK_BODY_ask_faces I_CURVE");
    try
    {
        if (count < 1 || faces is null) throw new InvalidOperationException("sheet body has no face");
        return faces[0];
    }
    finally { if (faces is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(faces), "PK_MEMORY_free I_CURVE faces"); }
}

static unsafe void AssertICurve(PK_BODY_t body)
{
    int count; PK_CURVE_t* curves = null;
    ParasolidXtCorpusHost.Check(PK_PART_ask_construction_curves(body, &count, &curves), "PK_PART_ask_construction_curves I_CURVE");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_CLASS_t cls; ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(curves[i], &cls), "PK_ENTITY_ask_class I_CURVE result");
            if (cls != PK_CLASS_icurve) continue;
            int commonCount; PK_SURF_t* common = null;
            var error = PK_CURVE_find_surfs_common(curves[i], &commonCount, &common);
            try
            {
                if (error != PK_ERROR_no_errors && error != PK_ERROR_geom_topol_mismatch)
                    ParasolidXtCorpusHost.Check(error, "PK_CURVE_find_surfs_common I_CURVE");
                if (error == PK_ERROR_no_errors && (commonCount < 2 || common is null))
                    throw new InvalidOperationException("I_CURVE has fewer than two support surfaces");
            }
            finally { if (common is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(common), "PK_MEMORY_free I_CURVE support list"); }
            return;
        }
        throw new InvalidOperationException("result has no PK_CLASS_icurve");
    }
    finally { if (curves is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(curves), "PK_MEMORY_free I_CURVE construction curves"); }
}
