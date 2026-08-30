#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

var cases = new CorpusCaseSpec[]
{
    Case("spcurve.variant.plane.rational", "plane", "rational", "Rational two-dimensional B-curve p-curve.", "{\"rational\":true}"),
    Case("spcurve.variant.plane.closed", "plane", "closed", "Closed non-periodic B-curve p-curve.", "{\"closed\":true}"),
    Case("spcurve.variant.plane.periodic", "plane", "periodic", "Periodic smooth-seam B-curve p-curve.", "{\"periodic\":true,\"closed\":true}"),
    Case("spcurve.variant.cylinder.seam-crossing", "cylinder", "periodic", "Periodic B-curve p-curve crossing a cylindrical seam.", "{\"surface\":\"cylinder\",\"periodic\":true,\"seamCrossing\":true}"),
    Case("spcurve.variant.torus.rational", "torus", "rational", "Rational p-curve on a periodic torus support.", "{\"surface\":\"torus\",\"rational\":true}"),
    Case("spcurve.variant.torus.closed", "torus", "closed", "Closed p-curve on a periodic torus support.", "{\"surface\":\"torus\",\"closed\":true}"),
};

return ParasolidXtCorpusHost.RunGroup("spcurve-variants", cases, args);

static CorpusCaseSpec Case(string id, string surface, string variant, string description, string parameters) => new(
    id,
    "PK_BCURVE_create + PK_SPCURVE_create",
    description,
    new[] { "spcurve", "variant", "surface/" + surface, "pcurve/" + variant },
    () => Create(surface, variant),
    new CorpusBodyCounts(1, 1, 1, 4, 4),
    null,
    parameters,
    0,
    false,
    requiredSchemaNodes: new[] { "SP_CURVE", "B_CURVE" },
    requiredSchemaDependencies: new[] { "SP_CURVE->B_CURVE" },
    typeCoverage: new[] { "geometry.spcurve.variant." + variant, "geometry.spcurve.variant.surface." + surface });

static unsafe PK_BODY_t Create(string surfaceKind, string variant)
{
    var surface = CreateSurface(surfaceKind);
    var pcurve = CreatePcurve(variant);
    PK_SPCURVE_t spcurve;
    var form = new PK_SPCURVE_sf_t(surface, pcurve);
    ParasolidXtCorpusHost.Check(PK_SPCURVE_create(&form, &spcurve), "PK_SPCURVE_create variant");
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(6, 4, null, &body), "PK_BODY_create_sheet_rectangle variant");
    var values = stackalloc PK_GEOM_t[1] { spcurve };
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, values), "PK_PART_add_geoms variant");
    return body;
}

static unsafe PK_CURVE_t CreatePcurve(string variant)
{
    if (variant == "rational")
    {
        var vertices = stackalloc double[] { 1.0, 0.0, 1.0, 0.5, 0.5, 0.7071067811865476, 0.0, 1.0, 1.0 };
        var mult = stackalloc int[] { 3, 3 }; var knots = stackalloc double[] { 0, 1 };
        return CreateBCurve(2, 3, 3, PK_LOGICAL_true, vertices, 2, mult, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false);
    }
    if (variant == "closed")
    {
        var vertices = stackalloc double[] { 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0 };
        var mult = stackalloc int[] { 3, 1, 3 }; var knots = stackalloc double[] { 0, 1, 2 };
        return CreateBCurve(2, 4, 2, PK_LOGICAL_false, vertices, 3, mult, knots, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_true);
    }
    var periodicVertices = stackalloc double[]
    {
        1.0, 0.0, 0.5, 0.8660254037844386, -0.5, 0.8660254037844386,
        -1.0, 0.0, -0.5, -0.8660254037844386, 0.5, -0.8660254037844386,
        1.0, 0.0, 0.5, 0.8660254037844386, -0.5, 0.8660254037844386,
    };
    var periodicMult = stackalloc int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
    var periodicKnots = stackalloc double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
    return CreateBCurve(3, 9, 2, PK_LOGICAL_false, periodicVertices, 13, periodicMult, periodicKnots, PK_knot_uniform_c, PK_LOGICAL_true, PK_LOGICAL_true);
}

static unsafe PK_CURVE_t CreateBCurve(int degree, int nVertices, int dim, PK_LOGICAL_t rational, double* vertices, int nKnots, int* mult, double* knots, PK_knot_type_t knotType, PK_LOGICAL_t periodic, PK_LOGICAL_t closed)
{
    var sf = new PK_BCURVE_sf_t(degree, nVertices, dim, rational, vertices, PK_BCURVE_form_arbitrary_c, nKnots, mult, knots, knotType, periodic, closed, PK_self_intersect_false_c);
    PK_BCURVE_t curve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&sf, &curve), "PK_BCURVE_create variant");
    return curve;
}

static unsafe PK_SURF_t CreateSurface(string kind)
{
    var basis = new PK_AXIS2_sf_t(new PK_VECTOR_t(0, 0, 0), new PK_VECTOR1_t(0, 0, 1), new PK_VECTOR1_t(1, 0, 0));
    if (kind == "plane") { var sf = new PK_PLANE_sf_t(basis); PK_PLANE_t p; ParasolidXtCorpusHost.Check(PK_PLANE_create(&sf, &p), "PK_PLANE_create variant"); return p; }
    if (kind == "cylinder") { var sf = new PK_CYL_sf_t(basis, 2); PK_CYL_t c; ParasolidXtCorpusHost.Check(PK_CYL_create(&sf, &c), "PK_CYL_create variant"); return c; }
    var torus = new PK_TORUS_sf_t(basis, 4, 1); PK_TORUS_t t; ParasolidXtCorpusHost.Check(PK_TORUS_create(&torus, &t), "PK_TORUS_create variant"); return t;
}
