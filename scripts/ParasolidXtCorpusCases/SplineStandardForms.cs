#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// Direct standard-form cases.  Each case intentionally asks the created
// spline before putting it in a sheet body so the transmitted fixture is tied
// to the values that Parasolid accepted, rather than only to input metadata.
var cases = new CorpusCaseSpec[]
{
    new(
        "standard-form.bcurve.rational",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Rational quadratic B-curve with homogeneous four-dimensional control vertices (XYZW, W is weight).",
        new[] { "standard-form", "bcurve", "rational", "vertex-dim-4-homogeneous", "non-periodic", "open" },
        CreateRationalBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":2,\"nVertices\":3,\"vertexDim\":4,\"rational\":true,\"knotType\":\"bezier-ends\",\"periodic\":false,\"closed\":false}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.form.arbitrary", "bcurve.sf.rational.true", "bcurve.sf.vertex-dim-4", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.knot.bezier_ends", "bcurve.sf.self-intersecting.false" }
    ),
    new(
        "standard-form.bcurve.uniform",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Non-rational quadratic B-curve with a uniform knot set.",
        new[] { "standard-form", "bcurve", "non-rational", "uniform-knots", "non-periodic", "open" },
        CreateUniformBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":2,\"nVertices\":5,\"vertexDim\":3,\"rational\":false,\"knotType\":\"uniform\",\"knots\":[0.0,1.0,2.0,3.0]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.knot.uniform" }
    ),
    new(
        "standard-form.bcurve.quasi-uniform",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Non-rational cubic B-curve with a quasi-uniform knot set.",
        new[] { "standard-form", "bcurve", "non-rational", "quasi-uniform-knots", "non-periodic", "open" },
        CreateQuasiUniformBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":3,\"nVertices\":6,\"vertexDim\":3,\"rational\":false,\"knotType\":\"quasi-uniform\",\"knots\":[0.0,1.0,2.0,3.0]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.knot.quasi_uniform" }
    ),
    new(
        "standard-form.bcurve.piecewise-bezier-knots",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Non-rational quadratic B-curve whose interior knot has Bezier-piece multiplicity.",
        new[] { "standard-form", "bcurve", "non-rational", "piecewise-bezier-knots", "non-periodic", "open" },
        CreatePiecewiseKnotBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":2,\"nVertices\":5,\"vertexDim\":3,\"rational\":false,\"knotType\":\"piecewise-bezier\",\"multiplicities\":[3,2,3]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.knot.piecewise_bezier" }
    ),
    new(
        "standard-form.bcurve.bezier-ends",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Cubic B-curve with the Bezier-ends knot classification.",
        new[] { "standard-form", "bcurve", "non-rational", "bezier-ends-knots", "non-periodic", "open" },
        CreateBezierEndsBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":3,\"nVertices\":4,\"vertexDim\":3,\"rational\":false,\"knotType\":\"bezier-ends\",\"multiplicities\":[4,4]}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.knot.bezier_ends" }
    ),
    new(
        "standard-form.bcurve.polyline",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Polyline-form linear B-curve with four control vertices.",
        new[] { "standard-form", "bcurve", "polyline-form", "non-rational", "non-periodic", "open" },
        CreatePolylineBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":1,\"nVertices\":4,\"vertexDim\":3,\"vertexForm\":\"polyline\",\"knotType\":\"uniform\"}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.form.polyline", "bcurve.sf.knot.uniform" }
    ),
    new(
        "standard-form.bcurve.closed",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Closed non-periodic cubic B-curve with coincident end control vertices.",
        new[] { "standard-form", "bcurve", "closed", "non-periodic", "non-rational" },
        CreateClosedBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":3,\"nVertices\":5,\"vertexDim\":3,\"isPeriodic\":false,\"isClosed\":true}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.true", "bcurve.sf.knot.non_uniform" }
    ),
    new(
        "standard-form.bcurve.periodic",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Periodic cubic B-curve with repeated leading control vertices and a uniform knot cycle.",
        new[] { "standard-form", "bcurve", "periodic", "closed", "non-rational", "uniform-knots" },
        CreatePeriodicBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":3,\"nVertices\":9,\"vertexDim\":3,\"isPeriodic\":true,\"isClosed\":true,\"knotType\":\"uniform\"}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.true", "bcurve.sf.closed.true", "bcurve.sf.knot.uniform" }
    ),
    new(
        "standard-form.bcurve.smooth-seam",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Periodic cubic B-curve using the smooth-seam knot classification.",
        new[] { "standard-form", "bcurve", "periodic", "closed", "smooth-seam", "non-rational" },
        CreateSmoothSeamBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":3,\"nVertices\":9,\"vertexDim\":3,\"isPeriodic\":true,\"isClosed\":true,\"knotType\":\"smooth-seam\"}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.true", "bcurve.sf.closed.true", "bcurve.sf.knot.smooth_seam" }
    ),
    new(
        "standard-form.bcurve.self-intersecting",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Cubic B-curve explicitly classified as self-intersecting by its standard form.",
        new[] { "standard-form", "bcurve", "self-intersecting", "non-rational", "non-periodic", "open" },
        CreateSelfIntersectingBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"degree\":3,\"nVertices\":4,\"vertexDim\":3,\"isRational\":false,\"selfIntersecting\":true}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.rational.false", "bcurve.sf.periodic.false", "bcurve.sf.closed.false", "bcurve.sf.self-intersecting.true" }
    ),
    new(
        "standard-form.bcurve.circular",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Rational quadratic B-curve using the circular standard-form classification.",
        new[] { "standard-form", "bcurve", "form/circular", "rational" },
        CreateCircularBCurve,
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.form.circular", "bcurve.sf.rational.true", "bcurve.sf.vertex-dim-4" }
    ),
    new(
        "standard-form.bcurve.elliptic",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Rational quadratic B-curve using the elliptic standard-form classification.",
        new[] { "standard-form", "bcurve", "form/elliptic", "rational" },
        CreateEllipticBCurve,
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.form.elliptic", "bcurve.sf.rational.true", "bcurve.sf.vertex-dim-4" }
    ),
    new(
        "standard-form.bcurve.parabolic",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Quadratic B-curve using the parabolic standard-form classification.",
        new[] { "standard-form", "bcurve", "form/parabolic", "non-rational" },
        CreateParabolicBCurve,
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.form.parabolic", "bcurve.sf.rational.false" }
    ),
    new(
        "standard-form.bcurve.hyperbolic",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "Rational quadratic B-curve using the hyperbolic standard-form classification.",
        new[] { "standard-form", "bcurve", "form/hyperbolic", "rational" },
        CreateHyperbolicBCurve,
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.form.hyperbolic", "bcurve.sf.rational.true", "bcurve.sf.vertex-dim-4" }
    ),
    new(
        "standard-form.bcurve.unset-sentinels",
        "PK_BCURVE_create + PK_BCURVE_ask",
        "B-curve retaining unset form, knot and self-intersection sentinels.",
        new[] { "standard-form", "bcurve", "unset-sentinels" },
        CreateUnsetBCurve,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"form\":\"unset\",\"knotType\":\"unset\",\"selfIntersecting\":\"unset\"}",
        requiredSchemaNodes: new[] { "B_CURVE", "NURBS_CURVE" },
        typeCoverage: new[] { "bcurve.sf.form.unset", "bcurve.sf.knot.unset", "bcurve.sf.self-intersecting.unset" }
    ),
    new(
        "standard-form.bsurf.rational",
        "PK_BSURF_create + PK_BSURF_ask",
        "Rational B-surface with homogeneous four-dimensional control vertices (XYZW, W is weight).",
        new[] { "standard-form", "bsurf", "rational", "vertex-dim-4-homogeneous", "non-periodic", "open" },
        CreateRationalBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"nUVertices\":3,\"nVVertices\":3,\"vertexDim\":4,\"rational\":true,\"uKnotType\":\"non-uniform\",\"vKnotType\":\"non-uniform\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.form.arbitrary", "bsurf.sf.rational.true", "bsurf.sf.vertex-dim-4", "bsurf.sf.periodic-u.false", "bsurf.sf.periodic-v.false", "bsurf.sf.closed-u.false", "bsurf.sf.closed-v.false", "bsurf.sf.knot.bezier_ends", "bsurf.sf.convexity.arbitrary", "bsurf.sf.self-intersecting.false" }
    ),
    new(
        "standard-form.bsurf.periodic-u",
        "PK_BSURF_create + PK_BSURF_ask",
        "B-surface periodic in U and closed in U, with an open V direction.",
        new[] { "standard-form", "bsurf", "periodic-u", "closed-u", "non-rational", "uniform-knots" },
        CreatePeriodicUBSurfSplinewise,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"nUVertices\":5,\"nVVertices\":3,\"isUPeriodic\":true,\"isUClosed\":true,\"isVPeriodic\":false,\"isVClosed\":false}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.periodic-u.true", "bsurf.sf.periodic-v.false", "bsurf.sf.closed-u.true", "bsurf.sf.closed-v.false", "bsurf.sf.knot.uniform" }
    ),
    new(
        "standard-form.bsurf.periodic-v",
        "PK_BSURF_create + PK_BSURF_ask",
        "B-surface periodic in V and closed in V, with an open U direction.",
        new[] { "standard-form", "bsurf", "periodic-v", "closed-v", "non-rational", "uniform-knots" },
        CreatePeriodicVBSurfSplinewise,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"nUVertices\":3,\"nVVertices\":5,\"isUPeriodic\":false,\"isVPeriodic\":true,\"isVClosed\":true}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.periodic-u.false", "bsurf.sf.periodic-v.true", "bsurf.sf.closed-u.false", "bsurf.sf.closed-v.true", "bsurf.sf.knot.smooth_seam" }
    ),
    new(
        "standard-form.bsurf.uniform-mixed",
        "PK_BSURF_create + PK_BSURF_ask",
        "Non-rational B-surface with uniform U knots and quasi-uniform V knots.",
        new[] { "standard-form", "bsurf", "non-rational", "uniform-knots", "quasi-uniform-knots", "mixed-knot-types" },
        CreateUniformMixedBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"nUVertices\":3,\"nVVertices\":3,\"uKnotType\":\"uniform\",\"vKnotType\":\"quasi-uniform\",\"periodic\":false}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.periodic-u.false", "bsurf.sf.periodic-v.false", "bsurf.sf.closed-u.false", "bsurf.sf.closed-v.false", "bsurf.sf.knot.uniform", "bsurf.sf.knot.quasi_uniform" }
    ),
    new(
        "standard-form.bsurf.piecewise-bezier",
        "PK_BSURF_create + PK_BSURF_ask",
        "Non-rational B-surface with Bezier-piece multiplicity in both directions.",
        new[] { "standard-form", "bsurf", "non-rational", "piecewise-bezier-knots", "mixed-control-grid" },
        CreatePiecewiseBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"nUVertices\":3,\"nVVertices\":3,\"uKnotType\":\"piecewise-bezier\",\"vKnotType\":\"piecewise-bezier\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.periodic-u.false", "bsurf.sf.periodic-v.false", "bsurf.sf.closed-u.false", "bsurf.sf.closed-v.false", "bsurf.sf.knot.piecewise_bezier" }
    ),
    new(
        "standard-form.bsurf.convex",
        "PK_BSURF_create + PK_BSURF_ask",
        "Convexity-classified non-rational B-surface.",
        new[] { "standard-form", "bsurf", "non-rational", "convexity-convex", "non-periodic", "open" },
        CreateConvexBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"convexity\":\"convex\",\"selfIntersecting\":false}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.periodic-u.false", "bsurf.sf.periodic-v.false", "bsurf.sf.closed-u.false", "bsurf.sf.closed-v.false", "bsurf.sf.convexity.convex", "bsurf.sf.knot.non_uniform" }
    ),
    new(
        "standard-form.bsurf.concave",
        "PK_BSURF_create + PK_BSURF_ask",
        "Concavity-classified non-rational B-surface.",
        new[] { "standard-form", "bsurf", "non-rational", "convexity-concave" },
        CreateConcaveBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"convexity\":\"concave\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.convexity.concave" }
    ),
    new(
        "standard-form.bsurf.self-intersecting",
        "PK_BSURF_create + PK_BSURF_ask",
        "B-surface explicitly classified as self-intersecting by its standard form.",
        new[] { "standard-form", "bsurf", "self-intersecting", "non-rational" },
        CreateSelfIntersectingBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"uDegree\":2,\"vDegree\":2,\"selfIntersecting\":true}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.rational.false", "bsurf.sf.self-intersecting.true" }
    ),
    new(
        "standard-form.bsurf.unset-sentinels",
        "PK_BSURF_create + PK_BSURF_ask",
        "B-surface retaining unset form, knot, self-intersection and convexity sentinels.",
        new[] { "standard-form", "bsurf", "unset-sentinels" },
        CreateUnsetBSurf,
        new CorpusBodyCounts(1, 1, 1, 4, 4),
        null,
        "{\"form\":\"unset\",\"uKnotType\":\"unset\",\"vKnotType\":\"unset\",\"selfIntersecting\":\"unset\",\"convexity\":\"unset\"}",
        requiredSchemaNodes: new[] { "B_SURFACE", "NURBS_SURF" },
        typeCoverage: new[] { "bsurf.sf.form.unset", "bsurf.sf.knot.unset", "bsurf.sf.self-intersecting.unset", "bsurf.sf.convexity.unset" }
    ),
};

return ParasolidXtCorpusHost.RunGroup("spline-standard-forms", cases, args);

static unsafe PK_BODY_t CreateRationalBCurve()
{
    var vertices = stackalloc double[]
    {
        1.0, 0.0, 0.0, 1.0,
        0.5, 0.5, 0.0, 0.7071067811865476,
        0.0, 1.0, 0.0, 1.0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(2, 3, 4, PK_LOGICAL_true, vertices, PK_BCURVE_form_arbitrary_c, 2, multiplicities, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 4);
}

static unsafe PK_BODY_t CreateUniformBCurve()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 1.0, 0.0, 2.0, 1.0, 0.0,
        3.0, -0.5, 0.0, 4.0, 0.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 1, 1, 3 };
    var knots = stackalloc double[] { 0.0, 1.0, 2.0, 3.0 };
    return CreateBCurve(2, 5, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 4, multiplicities, knots, PK_knot_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreateQuasiUniformBCurve()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 1.5, 0.5, 2.0, -1.0, 1.0,
        3.0, 1.0, 0.0, 4.0, -0.5, 0.5, 5.0, 0.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 4, 1, 1, 4 };
    var knots = stackalloc double[] { 0.0, 1.0, 2.0, 3.0 };
    return CreateBCurve(3, 6, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 4, multiplicities, knots, PK_knot_quasi_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreatePiecewiseKnotBCurve()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 2.0, 0.5, 2.0, -1.0, 1.0,
        3.0, 1.0, 0.0, 4.0, 0.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 2, 3 };
    var knots = stackalloc double[] { 0.0, 1.0, 2.0 };
    return CreateBCurve(2, 5, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 3, multiplicities, knots, PK_knot_piecewise_bezier_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreateBezierEndsBCurve()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 2.0, 0.5, 2.0, -1.0, 1.0, 4.0, 0.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 4, 4 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(3, 4, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 2, multiplicities, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreatePolylineBCurve()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 1.0, 0.0, 2.0, -0.5, 0.0, 3.0, 0.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 2, 1, 1, 2 };
    var knots = stackalloc double[] { 0.0, 1.0, 2.0, 3.0 };
    return CreateBCurve(1, 4, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_polyline_c, 4, multiplicities, knots, PK_knot_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreateClosedBCurve()
{
    var vertices = stackalloc double[]
    {
        1.0, 0.0, 0.0, 0.0, 1.0, 0.0, -1.0, 0.0, 0.0,
        0.0, -1.0, 0.0, 1.0, 0.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 4, 1, 4 };
    var knots = stackalloc double[] { 0.0, 1.0, 2.0 };
    return CreateBCurve(3, 5, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 3, multiplicities, knots, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_true, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreatePeriodicBCurve()
{
    var vertices = stackalloc double[]
    {
        1.0, 0.0, 0.0, 0.5, 0.8660254037844386, 0.0, -0.5, 0.8660254037844386, 0.0,
        -1.0, 0.0, 0.0, -0.5, -0.8660254037844386, 0.0, 0.5, -0.8660254037844386, 0.0,
        1.0, 0.0, 0.0, 0.5, 0.8660254037844386, 0.0, -0.5, 0.8660254037844386, 0.0,
    };
    var multiplicities = stackalloc int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
    var knots = stackalloc double[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0 };
    return CreateBCurve(3, 9, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 13, multiplicities, knots, PK_knot_uniform_c, PK_LOGICAL_true, PK_LOGICAL_true, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreateSmoothSeamBCurve()
{
    var positions = stackalloc PK_VECTOR_t[6];
    for (var i = 0; i < 6; i++)
    {
        var a = 2.0 * Math.PI * i / 6.0;
        positions[i] = new PK_VECTOR_t(Math.Cos(a), Math.Sin(a), 0.0);
    }
    var form = new PK_BCURVE_splinewise_sf_t(
        3, 6, positions,
        PK_PARAM_end_periodic_c, PK_PARAM_end_periodic_c,
        default, default,
        PK_PARAM_knot_auto_c, null);
    PK_BCURVE_t bcurve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create_splinewise(&form, &bcurve), "PK_BCURVE_create_splinewise smooth-seam");
    AskBCurve(bcurve, 3, PK_LOGICAL_false);
    return AddConstructionGeometry(bcurve);
}

static unsafe PK_BODY_t CreateSelfIntersectingBCurve()
{
    var vertices = stackalloc double[]
    {
        -1.5, -1.0, 0.0, 1.5, 1.0, 0.0, -1.5, 1.0, 0.0, 1.5, -1.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 4, 4 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(3, 4, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_arbitrary_c, 2, multiplicities, knots, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_true_c, 3);
}

static unsafe PK_BODY_t CreateCircularBCurve()
{
    var vertices = stackalloc double[] { 1.0, 0.0, 0.0, 1.0, 0.5, 0.5, 0.0, 0.7071067811865476, 0.0, 1.0, 0.0, 1.0 };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(2, 3, 4, PK_LOGICAL_true, vertices, PK_BCURVE_form_circular_c, 2, multiplicities, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 4);
}

static unsafe PK_BODY_t CreateEllipticBCurve()
{
    var vertices = stackalloc double[] { 3.0, 0.0, 0.0, 1.0, 1.5, 1.5, 0.0, 0.7071067811865476, 0.0, 1.0, 0.0, 1.0 };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(2, 3, 4, PK_LOGICAL_true, vertices, PK_BCURVE_form_elliptic_c, 2, multiplicities, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 4);
}

static unsafe PK_BODY_t CreateParabolicBCurve()
{
    var vertices = stackalloc double[] { 0.0, 0.0, 0.0, 1.0, 1.0, 0.0, 2.0, 0.0, 0.0 };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(2, 3, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_parabolic_c, 2, multiplicities, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 3);
}

static unsafe PK_BODY_t CreateHyperbolicBCurve()
{
    var vertices = stackalloc double[] { 1.0, 0.0, 0.0, 1.0, 0.5, 0.5, 0.0, 0.5, 0.0, 1.0, 0.0, 1.0 };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBCurve(2, 3, 4, PK_LOGICAL_true, vertices, PK_BCURVE_form_hyperbolic_c, 2, multiplicities, knots, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, 4);
}

static unsafe PK_BODY_t CreateUnsetBCurve()
{
    var vertices = stackalloc double[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0 };
    var multiplicities = stackalloc int[] { 2, 2 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    PK_BCURVE_t bcurve;
    var form = new PK_BCURVE_sf_t(1, 2, 3, PK_LOGICAL_false, vertices, PK_BCURVE_form_unset_c, 2, multiplicities, knots, PK_knot_unset_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_unset_c);
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&form, &bcurve), "PK_BCURVE_create unset sentinels");
    AssertUnsetBCurve(bcurve);
    return AddConstructionGeometry(bcurve);
}

static unsafe PK_BODY_t CreateRationalBSurf()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 1.0, 1.0, 1.0, 0.0, 1.0, 1.0, 2.0, 0.0, 1.0, 1.0,
        0.0, 1.0, 1.0, 1.0, 0.64, 0.64, 0.64, 0.8, 1.28, 0.64, 0.64, 0.8,
        0.0, 2.0, 1.0, 1.0, 1.0, 2.0, 1.0, 1.0, 2.0, 2.0, 1.0, 1.0,
    };
    var uMultiplicities = stackalloc int[] { 3, 3 };
    var vMultiplicities = stackalloc int[] { 3, 3 };
    var uKnots = stackalloc double[] { 0.0, 1.0 };
    var vKnots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(2, 2, 3, 3, 4, PK_LOGICAL_true, vertices, PK_BSURF_form_arbitrary_c, 2, 2, uMultiplicities, vMultiplicities, uKnots, vKnots, PK_knot_bezier_ends_c, PK_knot_bezier_ends_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c, 4);
}

static unsafe PK_BODY_t CreatePeriodicUBSurfSplinewise()
{
    var positions = stackalloc PK_VECTOR_t[6 * 3];
    for (var v = 0; v < 3; v++)
        for (var u = 0; u < 6; u++)
        {
            var a = 2.0 * Math.PI * u / 6.0;
            positions[v * 6 + u] = new PK_VECTOR_t(Math.Cos(a), Math.Sin(a), v);
        }
    var form = new PK_BSURF_splinewise_sf_t(3, 2, 6, 3, positions,
        PK_PARAM_end_periodic_c, PK_PARAM_end_natural_c,
        PK_PARAM_end_periodic_c, PK_PARAM_end_natural_c,
        null, null, null, null,
        PK_PARAM_knot_auto_c, PK_PARAM_knot_auto_c, null, null,
        PK_PARAM_twist_no_c, PK_PARAM_twist_no_c, PK_PARAM_twist_no_c, PK_PARAM_twist_no_c,
        default, default, default, default);
    PK_BSURF_t surface;
    ParasolidXtCorpusHost.Check(PK_BSURF_create_splinewise(&form, &surface), "PK_BSURF_create_splinewise periodic-u");
    return AddConstructionGeometry(surface);
}

static unsafe PK_BODY_t CreatePeriodicVBSurfSplinewise()
{
    var positions = stackalloc PK_VECTOR_t[3 * 6];
    for (var v = 0; v < 6; v++)
        for (var u = 0; u < 3; u++)
        {
            var a = 2.0 * Math.PI * v / 6.0;
            positions[v * 3 + u] = new PK_VECTOR_t(u, Math.Cos(a), Math.Sin(a));
        }
    var form = new PK_BSURF_splinewise_sf_t(2, 3, 3, 6, positions,
        PK_PARAM_end_natural_c, PK_PARAM_end_periodic_c,
        PK_PARAM_end_natural_c, PK_PARAM_end_periodic_c,
        null, null, null, null,
        PK_PARAM_knot_auto_c, PK_PARAM_knot_auto_c, null, null,
        PK_PARAM_twist_no_c, PK_PARAM_twist_no_c, PK_PARAM_twist_no_c, PK_PARAM_twist_no_c,
        default, default, default, default);
    PK_BSURF_t surface;
    ParasolidXtCorpusHost.Check(PK_BSURF_create_splinewise(&form, &surface), "PK_BSURF_create_splinewise periodic-v");
    return AddConstructionGeometry(surface);
}

static unsafe PK_BODY_t CreateUniformMixedBSurf()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 0.0, 0.2, 2.0, 0.0, 0.0,
        0.0, 1.0, 0.3, 1.0, 1.0, 0.8, 2.0, 1.0, 0.2,
        0.0, 2.0, 0.0, 1.0, 2.0, 0.4, 2.0, 2.0, 0.0,
    };
    var uMultiplicities = stackalloc int[] { 3, 3 };
    var vMultiplicities = stackalloc int[] { 3, 3 };
    var uKnots = stackalloc double[] { 0.0, 1.0 };
    var vKnots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c, 2, 2, uMultiplicities, vMultiplicities, uKnots, vKnots, PK_knot_uniform_c, PK_knot_quasi_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c, 3);
}

static unsafe PK_BODY_t CreatePiecewiseBSurf()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 0.0, 0.2, 2.0, 0.0, 0.0,
        0.0, 1.0, 0.3, 1.0, 1.0, 0.8, 2.0, 1.0, 0.2,
        0.0, 2.0, 0.0, 1.0, 2.0, 0.4, 2.0, 2.0, 0.0,
    };
    var uMultiplicities = stackalloc int[] { 3, 3 };
    var vMultiplicities = stackalloc int[] { 3, 3 };
    var uKnots = stackalloc double[] { 0.0, 1.0 };
    var vKnots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c, 2, 2, uMultiplicities, vMultiplicities, uKnots, vKnots, PK_knot_piecewise_bezier_c, PK_knot_piecewise_bezier_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_arbitrary_c, 3);
}

static unsafe PK_BODY_t CreateConvexBSurf()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 0.0, 0.2, 2.0, 0.0, 0.0,
        0.0, 1.0, 0.2, 1.0, 1.0, 0.5, 2.0, 1.0, 0.2,
        0.0, 2.0, 0.0, 1.0, 2.0, 0.2, 2.0, 2.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots, PK_knot_non_uniform_c, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_convex_c, 3);
}

static unsafe PK_BODY_t CreateConcaveBSurf()
{
    var vertices = stackalloc double[]
    {
        0.0, 0.0, 0.0, 1.0, 0.0, -0.2, 2.0, 0.0, 0.0,
        0.0, 1.0, -0.2, 1.0, 1.0, -0.5, 2.0, 1.0, -0.2,
        0.0, 2.0, 0.0, 1.0, 2.0, -0.2, 2.0, 2.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots, PK_knot_non_uniform_c, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_false_c, PK_convexity_concave_c, 3);
}

static unsafe PK_BODY_t CreateSelfIntersectingBSurf()
{
    var vertices = stackalloc double[]
    {
        -1.5, -1.0, 0.0, 1.5, 1.0, 0.0, -1.5, 1.0, 0.0,
        1.5, -1.0, 0.0, -1.5, -1.0, 0.0, 1.5, 1.0, 0.0,
        -1.5, 1.0, 0.0, 1.5, -1.0, 0.0, -1.5, -1.0, 0.0,
    };
    var multiplicities = stackalloc int[] { 3, 3 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    return CreateBSurf(2, 2, 3, 3, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_arbitrary_c, 2, 2, multiplicities, multiplicities, knots, knots, PK_knot_non_uniform_c, PK_knot_non_uniform_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_true_c, PK_convexity_arbitrary_c, 3);
}

static unsafe PK_BODY_t CreateUnsetBSurf()
{
    var vertices = stackalloc double[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 0.0 };
    var multiplicities = stackalloc int[] { 2, 2 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    PK_BSURF_t bsurf;
    var form = new PK_BSURF_sf_t(1, 1, 2, 2, 3, PK_LOGICAL_false, vertices, PK_BSURF_form_unset_c, 2, 2, multiplicities, multiplicities, knots, knots, PK_knot_unset_c, PK_knot_unset_c, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_LOGICAL_false, PK_self_intersect_unset_c, PK_convexity_unset_c);
    ParasolidXtCorpusHost.Check(PK_BSURF_create(&form, &bsurf), "PK_BSURF_create unset sentinels");
    AssertUnsetBSurf(bsurf);
    return AddConstructionGeometry(bsurf);
}

static unsafe void AssertUnsetBCurve(PK_BCURVE_t bcurve)
{
    var asked = default(PK_BCURVE_sf_t);
    ParasolidXtCorpusHost.Check(PK_BCURVE_ask(bcurve, &asked), "PK_BCURVE_ask unset sentinels");
    try
    {
        if (asked.form != PK_BCURVE_form_unset_c || asked.knot_type != PK_knot_unset_c || asked.self_intersecting != PK_self_intersect_unset_c)
            throw new InvalidOperationException($"unset BCurve sentinels normalized: form={asked.form}, knot={asked.knot_type}, self={asked.self_intersecting}");
    }
    finally
    {
        Free(asked.vertex);
        Free(asked.knot_mult);
        Free(asked.knot);
    }
}

static unsafe void AssertUnsetBSurf(PK_BSURF_t bsurf)
{
    var asked = default(PK_BSURF_sf_t);
    ParasolidXtCorpusHost.Check(PK_BSURF_ask(bsurf, &asked), "PK_BSURF_ask unset sentinels");
    try
    {
        if (asked.form != PK_BSURF_form_unset_c || asked.u_knot_type != PK_knot_unset_c || asked.v_knot_type != PK_knot_unset_c || asked.self_intersecting != PK_self_intersect_unset_c || asked.convexity != PK_convexity_unset_c)
            throw new InvalidOperationException($"unset BSurf sentinels normalized: form={asked.form}, uKnot={asked.u_knot_type}, vKnot={asked.v_knot_type}, self={asked.self_intersecting}, convexity={asked.convexity}");
    }
    finally
    {
        Free(asked.vertex);
        Free(asked.u_knot_mult);
        Free(asked.v_knot_mult);
        Free(asked.u_knot);
        Free(asked.v_knot);
    }
}

static unsafe PK_BODY_t CreateBCurve(int degree, int vertexCount, int vertexDim, PK_LOGICAL_t rational, double* vertices, PK_BCURVE_form_t form, int knotCount, int* multiplicities, double* knots, PK_knot_type_t knotType, PK_LOGICAL_t periodic, PK_LOGICAL_t closed, PK_self_intersect_t selfIntersecting, int expectedVertexDim)
{
    var standardForm = new PK_BCURVE_sf_t(degree, vertexCount, vertexDim, rational, vertices, form, knotCount, multiplicities, knots, knotType, periodic, closed, selfIntersecting);
    PK_BCURVE_t bcurve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&standardForm, &bcurve), "PK_BCURVE_create");
    AskBCurve(bcurve, expectedVertexDim, rational);
    return AddConstructionGeometry(bcurve);
}

static unsafe PK_BODY_t CreateBSurf(int uDegree, int vDegree, int nUVertices, int nVVertices, int vertexDim, PK_LOGICAL_t rational, double* vertices, PK_BSURF_form_t form, int nUKnots, int nVKnots, int* uMultiplicities, int* vMultiplicities, double* uKnots, double* vKnots, PK_knot_type_t uKnotType, PK_knot_type_t vKnotType, PK_LOGICAL_t uPeriodic, PK_LOGICAL_t vPeriodic, PK_LOGICAL_t uClosed, PK_LOGICAL_t vClosed, PK_self_intersect_t selfIntersecting, PK_convexity_t convexity, int expectedVertexDim)
{
    var standardForm = new PK_BSURF_sf_t(uDegree, vDegree, nUVertices, nVVertices, vertexDim, rational, vertices, form, nUKnots, nVKnots, uMultiplicities, vMultiplicities, uKnots, vKnots, uKnotType, vKnotType, uPeriodic, vPeriodic, uClosed, vClosed, selfIntersecting, convexity);
    PK_BSURF_t bsurf;
    ParasolidXtCorpusHost.Check(PK_BSURF_create(&standardForm, &bsurf), "PK_BSURF_create");
    AskBSurf(bsurf, expectedVertexDim, rational);
    return AddConstructionGeometry(bsurf);
}

static unsafe void AskBCurve(PK_BCURVE_t bcurve, int expectedVertexDim, PK_LOGICAL_t expectedRational)
{
    var standardForm = default(PK_BCURVE_sf_t);
    ParasolidXtCorpusHost.Check(PK_BCURVE_ask(bcurve, &standardForm), "PK_BCURVE_ask");
    try
    {
        if (standardForm.vertex_dim != expectedVertexDim || standardForm.is_rational != expectedRational)
            throw new InvalidOperationException("PK_BCURVE_ask returned unexpected vertex representation");
        if (standardForm.n_vertices <= 0 || standardForm.n_knots <= 0)
            throw new InvalidOperationException("PK_BCURVE_ask returned an empty standard form");
    }
    finally
    {
        Free(standardForm.vertex);
        Free(standardForm.knot_mult);
        Free(standardForm.knot);
    }
}

static unsafe void AskBSurf(PK_BSURF_t bsurf, int expectedVertexDim, PK_LOGICAL_t expectedRational)
{
    var standardForm = default(PK_BSURF_sf_t);
    ParasolidXtCorpusHost.Check(PK_BSURF_ask(bsurf, &standardForm), "PK_BSURF_ask");
    try
    {
        if (standardForm.vertex_dim != expectedVertexDim || standardForm.is_rational != expectedRational)
            throw new InvalidOperationException("PK_BSURF_ask returned unexpected vertex representation");
        if (standardForm.n_u_vertices <= 0 || standardForm.n_v_vertices <= 0 || standardForm.n_u_knots <= 0 || standardForm.n_v_knots <= 0)
            throw new InvalidOperationException("PK_BSURF_ask returned an empty standard form");
    }
    finally
    {
        Free(standardForm.vertex);
        Free(standardForm.u_knot_mult);
        Free(standardForm.v_knot_mult);
        Free(standardForm.u_knot);
        Free(standardForm.v_knot);
    }
}

static unsafe void Free(void* pointer)
{
    if (pointer is not null)
        ParasolidXtCorpusHost.Check(PK_MEMORY_free(pointer), "PK_MEMORY_free standard form");
}

static unsafe PK_BODY_t AddConstructionGeometry(PK_GEOM_t geometry)
{
    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(6.0, 4.0, null, &body), "PK_BODY_create_sheet_rectangle");
    var geometries = stackalloc PK_GEOM_t[1];
    geometries[0] = geometry;
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, geometries), "PK_PART_add_geoms");
    return body;
}
