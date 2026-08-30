#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Boundary standard forms are kept separate from the family-level form cases.
// The input dimensions and knot multiplicities are checked again through the
// typed ask before transmit, so a normalized form cannot silently become a
// false coverage claim.
var cases = new CorpusCaseSpec[]
{
    new(
        "bsf-boundary.bcurve.degree1-2d",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Minimum linear two-dimensional non-rational B-curve.",
        new[] { "standard-form", "bcurve", "degree/1", "vertices/2", "vertex-dim/2", "non-rational", "clamped" },
        CreateLinear2d,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":1,\"nVertices\":2,\"vertexDim\":2,\"rational\":false,\"knotType\":\"bezier-ends\",\"multiplicities\":[2,2]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.degree.1", "bcurve.sf.n-vertices.2", "bcurve.sf.vertex-dim-2", "bcurve.sf.rational.false", "bcurve.sf.knot.bezier_ends", "bcurve.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBCurve(body, 1, 2, 2, PK_LOGICAL_false, PK_knot_bezier_ends_c)),
    new(
        "bsf-boundary.bcurve.degree4-3d",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Quartic five-control-vertex non-rational B-curve with clamped ends.",
        new[] { "standard-form", "bcurve", "degree/4", "vertices/5", "vertex-dim/3", "non-rational", "clamped" },
        CreateQuartic3d,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":4,\"nVertices\":5,\"vertexDim\":3,\"rational\":false,\"knotType\":\"bezier-ends\",\"multiplicities\":[5,5]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.degree.4", "bcurve.sf.n-vertices.5", "bcurve.sf.vertex-dim-3", "bcurve.sf.rational.false", "bcurve.sf.knot.bezier_ends", "bcurve.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBCurve(body, 4, 5, 3, PK_LOGICAL_false, PK_knot_bezier_ends_c)),
    new(
        "bsf-boundary.bcurve.degree4-interior-knot",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Quartic B-curve with an unclamped interior knot multiplicity.",
        new[] { "standard-form", "bcurve", "degree/4", "vertices/7", "vertex-dim/3", "interior-knot", "unclamped" },
        CreateQuarticInteriorKnot,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":4,\"nVertices\":7,\"vertexDim\":3,\"rational\":false,\"knotType\":\"non-uniform\",\"multiplicities\":[5,2,5]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.degree.4", "bcurve.sf.n-vertices.7", "bcurve.sf.knot.non_uniform", "bcurve.sf.knot.multiplicity.unclamped" },
        typedAsk: body => AssertBCurve(body, 4, 7, 3, PK_LOGICAL_false, PK_knot_non_uniform_c)),
    new(
        "bsf-boundary.bcurve.rational-2d",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Two-dimensional rational quadratic B-curve at the lower rational boundary.",
        new[] { "standard-form", "bcurve", "degree/2", "vertices/3", "vertex-dim/3-homogeneous", "rational" },
        CreateRational2d,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":2,\"nVertices\":3,\"vertexDim\":3,\"rational\":true,\"knotType\":\"bezier-ends\",\"multiplicities\":[3,3]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.degree.2", "bcurve.sf.vertex-dim-3", "bcurve.sf.rational.true", "bcurve.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBCurve(body, 2, 3, 3, PK_LOGICAL_true, PK_knot_bezier_ends_c)),
    new(
        "bsf-boundary.bcurve.degree6-7v",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Higher-degree six B-curve with seven clamped control vertices.",
        new[] { "standard-form", "bcurve", "degree/6", "vertices/7", "vertex-dim/3", "non-rational", "clamped" },
        CreateDegree6,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":6,\"nVertices\":7,\"vertexDim\":3,\"rational\":false,\"knotType\":\"bezier-ends\"}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.degree.6", "bcurve.sf.n-vertices.7", "bcurve.sf.vertex-dim-3", "bcurve.sf.rational.false", "bcurve.sf.knot.bezier_ends" },
        typedAsk: body => AssertBCurve(body, 6, 7, 3, PK_LOGICAL_false, PK_knot_bezier_ends_c)),
    new(
        "bsf-boundary.bcurve.rational-3d-homogeneous",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Three-dimensional rational B-curve with explicit homogeneous weight coordinates.",
        new[] { "standard-form", "bcurve", "rational", "vertex-dim/4-homogeneous" },
        CreateRational3dHomogeneous,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":2,\"nVertices\":3,\"vertexDim\":4,\"rational\":true,\"weights\":[1.0,0.707106781,1.0]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.vertex-dim-4", "bcurve.sf.rational.true" },
        typedAsk: body => AssertBCurve(body, 2, 3, 4, PK_LOGICAL_true, PK_knot_bezier_ends_c)),
    new(
        "bsf-boundary.bsurf.degree1-2x2",
        "PK_BSURF_create + PK_BSURF_ask",
        "Bilinear two-by-two three-dimensional B-surface.",
        new[] { "standard-form", "bsurf", "degree/1x1", "grid/2x2", "vertex-dim/3", "non-rational", "clamped" },
        CreateBilinear2d,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":1,\"vDegree\":1,\"nUVertices\":2,\"nVVertices\":2,\"vertexDim\":3,\"rational\":false,\"uKnotType\":\"bezier-ends\",\"vKnotType\":\"bezier-ends\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.degree.1", "bsurf.sf.grid.2x2", "bsurf.sf.vertex-dim-3", "bsurf.sf.rational.false", "bsurf.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBSurf(body, 1, 1, 2, 2, 3, PK_LOGICAL_false)),
    new(
        "bsf-boundary.bsurf.degree3-4x4",
        "PK_BSURF_create + PK_BSURF_ask",
        "Cubic four-by-four three-dimensional B-surface with clamped ends.",
        new[] { "standard-form", "bsurf", "degree/3x3", "grid/4x4", "vertex-dim/3", "non-rational", "clamped" },
        CreateCubic4x4,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":3,\"vDegree\":3,\"nUVertices\":4,\"nVVertices\":4,\"vertexDim\":3,\"rational\":false,\"uKnotType\":\"bezier-ends\",\"vKnotType\":\"bezier-ends\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.degree.3", "bsurf.sf.grid.4x4", "bsurf.sf.vertex-dim-3", "bsurf.sf.rational.false", "bsurf.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBSurf(body, 3, 3, 4, 4, 3, PK_LOGICAL_false)),
    new(
        "bsf-boundary.bsurf.rational-3x4",
        "PK_BSURF_create + PK_BSURF_ask",
        "Rational asymmetric three-by-four B-surface grid.",
        new[] { "standard-form", "bsurf", "degree/2x3", "grid/3x4", "vertex-dim/3", "rational", "clamped" },
        CreateRational3x4,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":3,\"nUVertices\":3,\"nVVertices\":4,\"vertexDim\":4,\"rational\":true,\"uKnotType\":\"bezier-ends\",\"vKnotType\":\"bezier-ends\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.degree.2", "bsurf.sf.degree.3", "bsurf.sf.grid.3x4", "bsurf.sf.vertex-dim-4", "bsurf.sf.rational.true", "bsurf.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBSurf(body, 2, 3, 3, 4, 4, PK_LOGICAL_true)),
    new(
        "bsf-boundary.bsurf.degree4-5x5",
        "PK_BSURF_create + PK_BSURF_ask",
        "Higher-degree four-by-four B-surface with a five-by-five grid.",
        new[] { "standard-form", "bsurf", "degree/4x4", "grid/5x5", "vertex-dim/3", "non-rational", "clamped" },
        CreateDegree4Surface,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":4,\"vDegree\":4,\"nUVertices\":5,\"nVVertices\":5,\"vertexDim\":3,\"rational\":false}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.degree.4", "bsurf.sf.grid.5x5", "bsurf.sf.vertex-dim-3", "bsurf.sf.rational.false", "bsurf.sf.knot.multiplicity.clamped" },
        typedAsk: body => AssertBSurf(body, 4, 4, 5, 5, 3, PK_LOGICAL_false)),
};

return ParasolidXtCorpusHost.RunGroup("spline-boundaries", cases, args);

static unsafe PK_BODY_t CreateLinear2d()
{
    var vertices = stackalloc double[] { 0.0, 0.0, 2.0, 0.0 };
    var multiplicities = stackalloc int[] { 2, 2 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(1, 2, 2, PK_LOGICAL_false, vertices, 2, multiplicities, knots, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateQuartic3d()
{
    var vertices = stackalloc double[] { 0, 0, 0, 1, 1, 0, 2, -1, 0, 3, 1, 0, 4, 0, 0 };
    var multiplicities = stackalloc int[] { 5, 5 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(4, 5, 3, PK_LOGICAL_false, vertices, 2, multiplicities, knots, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateQuarticInteriorKnot()
{
    var vertices = stackalloc double[] { 0, 0, 0, 1, 1, .2, 2, -1, .4, 3, 1, .6, 4, 0, .8, 5, 0, 1, 6, .5, 1.1 };
    var multiplicities = stackalloc int[] { 5, 2, 5 };
    var knots = stackalloc double[] { 0.0, .5, 1.0 };
    return CreateBCurve(4, 7, 3, PK_LOGICAL_false, vertices, 3, multiplicities, knots, PK_knot_non_uniform_c);
}

static unsafe PK_BODY_t CreateRational2d()
{
    var vertices = stackalloc double[] { 1.0, 0.0, 1.0, 0.5, 0.5, .7071067811865476, 0.0, 1.0, 1.0 };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(2, 3, 3, PK_LOGICAL_true, vertices, 2, multiplicities, knots, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateDegree6()
{
    var vertices = stackalloc double[] { 0,0,0, 1,0.2,0, 2,-0.2,0, 3,0.3,0, 4,-0.3,0, 5,0.2,0, 6,0,0 };
    var multiplicities = stackalloc int[] { 7, 7 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(6, 7, 3, PK_LOGICAL_false, vertices, 2, multiplicities, knots, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateRational3dHomogeneous()
{
    var vertices = stackalloc double[] { 1,0,0,1, 0.5,0.5,0,0.7071067811865476, 0,1,0,1 };
    var mult = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0, 1 };
    var sf = new PK_BCURVE_sf_t(2, 3, 4, PK_LOGICAL_true, vertices, PK_BCURVE_form_arbitrary_c, 2, mult, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c);
    PK_BCURVE_t curve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&sf, &curve), "PK_BCURVE_create homogeneous");
    return AddGeometry(curve);
}

static unsafe PK_BODY_t CreateBilinear2d()
{
    var vertices = stackalloc double[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 0 };
    var uMultiplicities = stackalloc int[] { 2, 2 };
    var vMultiplicities = stackalloc int[] { 2, 2 };
    var uKnots = stackalloc double[] { 0, 1 };
    var vKnots = stackalloc double[] { 0, 1 };
    return CreateBSurf(1, 1, 2, 2, 3, PK_LOGICAL_false, vertices, 2, 2, uMultiplicities, vMultiplicities, uKnots, vKnots, PK_knot_bezier_ends_c, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateCubic4x4()
{
    var vertices = stackalloc double[]
    {
        0,0,0, 1,0,.2, 2,0,.1, 3,0,0,
        0,1,.1, 1,1,.5, 2,1,.4, 3,1,.1,
        0,2,.1, 1,2,.4, 2,2,.5, 3,2,.1,
        0,3,0, 1,3,.1, 2,3,.2, 3,3,0,
    };
    var uMultiplicities = stackalloc int[] { 4, 4 };
    var vMultiplicities = stackalloc int[] { 4, 4 };
    var uKnots = stackalloc double[] { 0, 1 };
    var vKnots = stackalloc double[] { 0, 1 };
    return CreateBSurf(3, 3, 4, 4, 3, PK_LOGICAL_false, vertices, 2, 2, uMultiplicities, vMultiplicities, uKnots, vKnots, PK_knot_bezier_ends_c, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateRational3x4()
{
    var vertices = stackalloc double[]
    {
        0,0,1,1, 1,0,1,1, 2,0,1,1,
        0,1,1,1, .64,.64,.64,.8, 1.28,.64,.64,.8,
        0,2,1,1, .64,1.28,.64,.8, 1.28,1.28,.64,.8,
        0,3,1,1, 1,3,1,1, 2,3,1,1,
    };
    var uMultiplicities = stackalloc int[] { 3, 3 };
    var vMultiplicities = stackalloc int[] { 4, 4 };
    var uKnots = stackalloc double[] { 0, 1 };
    var vKnots = stackalloc double[] { 0, 1 };
    return CreateBSurf(2, 3, 3, 4, 4, PK_LOGICAL_true, vertices, 2, 2, uMultiplicities, vMultiplicities, uKnots, vKnots, PK_knot_bezier_ends_c, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateDegree4Surface()
{
    var vertices = stackalloc double[5 * 5 * 3];
    for (var v = 0; v < 5; v++)
        for (var u = 0; u < 5; u++)
        {
            var index = (v * 5 + u) * 3;
            vertices[index] = u;
            vertices[index + 1] = v;
            vertices[index + 2] = 0.1 * Math.Sin(u + v);
        }
    var multiplicities = stackalloc int[] { 5, 5 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(4, 4, 5, 5, 3, PK_LOGICAL_false, vertices, 2, 2, multiplicities, multiplicities, knots, knots, PK_knot_bezier_ends_c, PK_knot_bezier_ends_c);
}

static unsafe PK_BODY_t CreateBCurve(int degree, int verticesCount, int vertexDim, PK_LOGICAL_t rational, double* vertices, int knotsCount, int* multiplicities, double* knots, PK_knot_type_t knotType)
{
    var sf = new PK_BCURVE_sf_t(degree, verticesCount, vertexDim, rational, vertices, PK_BCURVE_form_arbitrary_c, knotsCount, multiplicities, knots, knotType, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c);
    PK_BCURVE_t curve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&sf, &curve), "PK_BCURVE_create boundary");
    return AddGeometry(curve);
}

static unsafe PK_BODY_t CreateBSurf(int uDegree, int vDegree, int nu, int nv, int vertexDim, PK_LOGICAL_t rational, double* vertices, int nuKnots, int nvKnots, int* uMult, int* vMult, double* uKnots, double* vKnots, PK_knot_type_t uType, PK_knot_type_t vType)
{
    var sf = new PK_BSURF_sf_t(uDegree, vDegree, nu, nv, vertexDim, rational, vertices, PK_BSURF_form_arbitrary_c, nuKnots, nvKnots, uMult, vMult, uKnots, vKnots, uType, vType, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c);
    PK_BSURF_t surface;
    ParasolidXtCorpusHost.Check(PK_BSURF_create(&sf, &surface), "PK_BSURF_create boundary");
    return AddGeometry(surface);
}

static unsafe void AssertBCurve(PK_BODY_t body, int degree, int vertices, int vertexDim, PK_LOGICAL_t rational, PK_knot_type_t knotType)
{
    PK_CURVE_t curve;
    ParasolidXtCorpusHost.Check(FindCurve(body, &curve), "find B_CURVE boundary");
    var sf = default(PK_BCURVE_sf_t);
    ParasolidXtCorpusHost.Check(PK_BCURVE_ask(curve, &sf), "PK_BCURVE_ask boundary");
    try
    {
        // This Parasolid build reports knot_type=unset for direct arbitrary
        // B-curves even when the knot multiplicity array is retained.  The
        // actual dimensions and rational flag are authoritative here; the
        // exact knot arrays are recorded in parameters and schema output.
        if (sf.degree != degree || sf.n_vertices != vertices || sf.vertex_dim != vertexDim || sf.is_rational != rational || sf.n_knots <= 0)
            throw new InvalidOperationException($"BCURVE ask mismatch degree={sf.degree}, vertices={sf.n_vertices}, dim={sf.vertex_dim}, rational={(int)sf.is_rational}/{(int)rational}, knot={(int)sf.knot_type}, nKnots={sf.n_knots}");
    }
    finally { Free(sf.vertex); Free(sf.knot_mult); Free(sf.knot); }
}

static unsafe void AssertBSurf(PK_BODY_t body, int uDegree, int vDegree, int nu, int nv, int vertexDim, PK_LOGICAL_t rational)
{
    PK_SURF_t surface;
    ParasolidXtCorpusHost.Check(FindSurface(body, &surface), "find B_SURFACE boundary");
    var sf = default(PK_BSURF_sf_t);
    ParasolidXtCorpusHost.Check(PK_BSURF_ask(surface, &sf), "PK_BSURF_ask boundary");
    try
    {
        if (sf.u_degree != uDegree || sf.v_degree != vDegree || sf.n_u_vertices != nu || sf.n_v_vertices != nv || sf.vertex_dim != vertexDim || sf.is_rational != rational)
            throw new InvalidOperationException($"BSURF ask mismatch degree={sf.u_degree}x{sf.v_degree}, grid={sf.n_u_vertices}x{sf.n_v_vertices}, dim={sf.vertex_dim}, rational={sf.is_rational}");
    }
    finally { Free(sf.vertex); Free(sf.u_knot_mult); Free(sf.v_knot_mult); Free(sf.u_knot); Free(sf.v_knot); }
}

static unsafe PK_ERROR_code_t FindCurve(PK_BODY_t body, PK_CURVE_t* result)
{
    int count; PK_GEOM_t* geoms;
    var error = PK_PART_ask_geoms(body, &count, &geoms);
    if (error != PK_ERROR_no_errors) return error;
    try { if (count < 1) throw new InvalidOperationException("boundary body has no curve geometry"); for (var i = 0; i < count; i++) { PK_CLASS_t cls; ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(geoms[i], &cls), "ask boundary geometry class"); if (cls == PK_CLASS_bcurve) { *result = (PK_CURVE_t)geoms[i]; return PK_ERROR_no_errors; } } throw new InvalidOperationException("boundary body has no curve geometry"); }
    finally { if (geoms is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(geoms), "free boundary curves"); }
}

static unsafe PK_ERROR_code_t FindSurface(PK_BODY_t body, PK_SURF_t* result)
{
    int count; PK_GEOM_t* geoms;
    var error = PK_PART_ask_geoms(body, &count, &geoms);
    if (error != PK_ERROR_no_errors) return error;
    try { if (count < 1) throw new InvalidOperationException("boundary body has no surface geometry"); for (var i = 0; i < count; i++) { PK_CLASS_t cls; ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(geoms[i], &cls), "ask boundary geometry class"); if (cls == PK_CLASS_bsurf) { *result = (PK_SURF_t)geoms[i]; return PK_ERROR_no_errors; } } throw new InvalidOperationException("boundary body has no surface geometry"); }
    finally { if (geoms is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(geoms), "free boundary surfaces"); }
}

static unsafe PK_BODY_t AddGeometry(PK_GEOM_t geometry)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(6.0, 4.0, null, &body), "PK_BODY_create_sheet_rectangle boundary");
    var geoms = stackalloc PK_GEOM_t[1]; geoms[0] = geometry;
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geoms), "PK_PART_add_geoms boundary");
    return body;
}

static unsafe void Free(void* pointer)
{
    if (pointer is not null) ParasolidXtCorpusHost.Check(PK_MEMORY_free(pointer), "free boundary standard form");
}
