#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidXtCorpusHost=true
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

using static parasolid;

// CRTRCU is a KI producer rather than a PK_* producer.  The shared binding
// in PskernelSharp/KernelInterface.cs is deliberately used here so that this
// case group does not carry a private ABI declaration.  Each trimmed curve is
// attached to a sheet body as construction geometry, making the TRCURVE
// persist in the x_t.
var cases = new CorpusCaseSpec[]
{
    new(
        "trcurve.line.finite",
        "CRTRCU + PK_LINE_create + PK_PART_add_geoms",
        "Finite trimmed line created by the CRTRCU kernel interface producer.",
        new[] { "curve", "trcurve", "line", "finite-range", "sheet-body", "construction-geometry" },
        CreateLineFinite,
        null,
        null,
        "{\"basis\":\"line\",\"range\":[-1.25,1.75],\"rangeKind\":\"finite\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->LINE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.line", "trcurve.range.finite" }),
    new(
        "trcurve.line.reversed",
        "CRTRCU + PK_CURVE_make_curve_reversed + PK_LINE_create + PK_PART_add_geoms",
        "Trimmed reversed-orientation line created from a reversed basis curve.",
        new[] { "curve", "trcurve", "line", "reversed-basis", "sheet-body", "construction-geometry" },
        CreateLineReversed,
        null,
        null,
        "{\"basis\":\"line\",\"basisOrientation\":\"reversed\",\"range\":[-1.25,1.75],\"rangeKind\":\"finite\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->LINE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.line", "trcurve.range.reversed" }),
    new(
        "trcurve.circle.seam-crossing",
        "CRTRCU + PK_CIRCLE_create + PK_PART_add_geoms",
        "Periodic circular trim crossing the parameter seam by one turn.",
        new[] { "curve", "trcurve", "circle", "periodic", "seam-crossing", "sheet-body", "construction-geometry" },
        CreateCircleSeamCrossing,
        null,
        null,
        "{\"basis\":\"circle\",\"range\":[5.5,7.0],\"rangeKind\":\"periodic-seam-crossing\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->CIRCLE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.circle", "trcurve.range.periodic-seam" }),
    new(
        "trcurve.circle.reversed",
        "CRTRCU + PK_CIRCLE_create + PK_PART_add_geoms",
        "Reversed periodic circular trim over a half-turn interval.",
        new[] { "curve", "trcurve", "circle", "periodic", "reversed-range", "sheet-body", "construction-geometry" },
        CreateCircleReversed,
        null,
        null,
        "{\"basis\":\"circle\",\"requestedRange\":[3.5,1.0],\"standardFormRange\":[3.5,7.283185307179586],\"rangeKind\":\"reversed-periodic-wrap\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->CIRCLE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.circle", "trcurve.range.periodic-seam", "trcurve.range.reversed" }),
    new(
        "trcurve.circle.finite",
        "CRTRCU + PK_CIRCLE_create + PK_PART_add_geoms",
        "Short finite interval on a periodic circle away from the seam.",
        new[] { "curve", "trcurve", "circle", "periodic", "finite-range" },
        CreateCircleFinite,
        null, null,
        "{\"basis\":\"circle\",\"range\":[1.0,2.0],\"rangeKind\":\"finite\"}",
        0, false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->CIRCLE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.circle", "trcurve.range.finite" }),
    new(
        "trcurve.circle.full-period",
        "CRTRCU + PK_CIRCLE_create + PK_PART_add_geoms",
        "Exactly one full period of a circular basis.",
        new[] { "curve", "trcurve", "circle", "periodic", "full-period" },
        CreateCircleFullPeriod,
        null, null,
        "{\"basis\":\"circle\",\"range\":[0.0,6.283185307179586],\"rangeKind\":\"full-period\"}",
        0, false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->CIRCLE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.circle", "trcurve.range.full-cycle" }),
    new(
        "trcurve.ellipse.full-period",
        "CRTRCU + PK_ELLIPSE_create + PK_PART_add_geoms",
        "One full period of an elliptic basis represented as a trimmed curve.",
        new[] { "curve", "trcurve", "ellipse", "periodic", "full-period", "sheet-body", "construction-geometry" },
        CreateEllipseFullPeriod,
        null,
        null,
        "{\"basis\":\"ellipse\",\"range\":[0.0,6.283185307179586],\"rangeKind\":\"full-period\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->ELLIPSE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.ellipse", "trcurve.range.full-cycle" }),
    new(
        "trcurve.ellipse.seam-crossing",
        "CRTRCU + PK_ELLIPSE_create + PK_PART_add_geoms",
        "Elliptic interval crossing the periodic seam.",
        new[] { "curve", "trcurve", "ellipse", "periodic", "seam-crossing" },
        CreateEllipseSeamCrossing,
        null, null,
        "{\"basis\":\"ellipse\",\"range\":[5.5,7.0],\"rangeKind\":\"periodic-seam-crossing\"}",
        0, false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->ELLIPSE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.ellipse", "trcurve.range.periodic-seam" }),
    new(
        "trcurve.ellipse.reversed",
        "CRTRCU + PK_ELLIPSE_create + PK_PART_add_geoms",
        "Reversed elliptic interval represented as a periodic wrap.",
        new[] { "curve", "trcurve", "ellipse", "periodic", "reversed-range" },
        CreateEllipseReversed,
        null, null,
        "{\"basis\":\"ellipse\",\"requestedRange\":[3.5,1.0],\"rangeKind\":\"reversed-periodic-wrap\"}",
        0, false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->ELLIPSE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.ellipse", "trcurve.range.reversed" }),
    new(
        "trcurve.bcurve.finite",
        "CRTRCU + PK_BCURVE_create + PK_PART_add_geoms",
        "Finite trimmed non-rational B-curve with an explicit knot domain.",
        new[] { "curve", "trcurve", "bcurve", "finite-range", "sheet-body", "construction-geometry" },
        CreateBCurveFinite,
        null,
        null,
        "{\"basis\":\"bcurve\",\"degree\":1,\"range\":[0.2,0.8],\"rangeKind\":\"finite\"}",
        0,
        false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->B_CURVE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.bcurve", "trcurve.range.finite" }),
    new(
        "trcurve.bcurve.reversed",
        "CRTRCU + PK_CURVE_make_curve_reversed + PK_BCURVE_create + PK_PART_add_geoms",
        "Trimmed reversed-orientation B-curve over a short finite interval.",
        new[] { "curve", "trcurve", "bcurve", "reversed-basis", "sheet-body", "construction-geometry" },
        CreateBCurveReversed,
        expectedCounts: null,
        managedTransmit: null,
        parameters: "{\"basis\":\"bcurve\",\"basisOrientation\":\"reversed\",\"degree\":1,\"range\":[0.2,0.8],\"rangeKind\":\"finite\"}",
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->B_CURVE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.bcurve", "trcurve.range.reversed" }),
    new(
        "trcurve.bcurve.full-period",
        "CRTRCU + PK_BCURVE_create + PK_PART_add_geoms",
        "Complete parameter interval of a finite B-curve basis.",
        new[] { "curve", "trcurve", "bcurve", "full-interval" },
        CreateBCurveFullPeriod,
        null, null,
        "{\"basis\":\"bcurve\",\"range\":[0.0,1.0],\"rangeKind\":\"full-interval\"}",
        0, false,
        requiredSchemaNodes: new[] { "TRIMMED_CURVE" },
        requiredSchemaDependencies: new[] { "TRIMMED_CURVE->B_CURVE" },
        typeCoverage: new[] { "trcurve", "trcurve.basis.bcurve", "trcurve.range.full-cycle" }),
};

return ParasolidXtCorpusHost.RunGroup("trcurves", cases, args);

static unsafe PK_BODY_t CreateLineFinite() => CreateTrimmedWire(CreateLine(), -1.25, 1.75);

static unsafe PK_BODY_t CreateLineReversed()
{
    var line = CreateLine();
    PK_CURVE_t reversed;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_curve_reversed(line, &reversed), "PK_CURVE_make_curve_reversed TRCURVE line");
    return CreateTrimmedWire(reversed, -1.25, 1.75);
}

static unsafe PK_BODY_t CreateCircleSeamCrossing() => CreateTrimmedWire(CreateCircle(), 5.5, 7.0);

static unsafe PK_BODY_t CreateCircleFinite() => CreateTrimmedWire(CreateCircle(), 1.0, 2.0);

static unsafe PK_BODY_t CreateCircleFullPeriod() => CreateTrimmedWire(CreateCircle(), 0.0, 2.0 * Math.PI);

static unsafe PK_BODY_t CreateCircleReversed() => CreateTrimmedWire(CreateCircle(), 3.5, 1.0, 3.5, 2.0 * Math.PI + 1.0);

static unsafe PK_BODY_t CreateEllipseFullPeriod() => CreateTrimmedWire(CreateEllipse(), 0.0, 2.0 * Math.PI);

static unsafe PK_BODY_t CreateEllipseSeamCrossing() => CreateTrimmedWire(CreateEllipse(), 5.5, 7.0);

static unsafe PK_BODY_t CreateEllipseReversed() => CreateTrimmedWire(CreateEllipse(), 3.5, 1.0, 3.5, 2.0 * Math.PI + 1.0);

static unsafe PK_BODY_t CreateBCurveFinite() => CreateTrimmedWire(CreateBCurve(), 0.2, 0.8);

static unsafe PK_BODY_t CreateBCurveReversed()
{
    var bcurve = CreateBCurve();
    PK_CURVE_t reversed;
    ParasolidXtCorpusHost.Check(PK_CURVE_make_curve_reversed(bcurve, &reversed), "PK_CURVE_make_curve_reversed TRCURVE bcurve");
    return CreateTrimmedWire(reversed, 0.2, 0.8);
}

static unsafe PK_BODY_t CreateBCurveFullPeriod() => CreateTrimmedWire(CreateBCurve(), 0.0, 1.0);


static unsafe PK_CURVE_t CreateLine()
{
    var standardForm = new PK_LINE_sf_t(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    ParasolidXtCorpusHost.Check(PK_LINE_create(&standardForm, &line), "PK_LINE_create TRCURVE line");
    return line;
}

static unsafe PK_CURVE_t CreateCircle()
{
    var basis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    var standardForm = new PK_CIRCLE_sf_t(basis, 2.0);
    PK_CIRCLE_t circle;
    ParasolidXtCorpusHost.Check(PK_CIRCLE_create(&standardForm, &circle), "PK_CIRCLE_create TRCURVE circle");
    return circle;
}

static unsafe PK_CURVE_t CreateEllipse()
{
    var basis = new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0),
        new PK_VECTOR1_t(1.0, 0.0, 0.0));
    var standardForm = new PK_ELLIPSE_sf_t(basis, 3.0, 1.5);
    PK_ELLIPSE_t ellipse;
    ParasolidXtCorpusHost.Check(PK_ELLIPSE_create(&standardForm, &ellipse), "PK_ELLIPSE_create TRCURVE ellipse");
    return ellipse;
}

static unsafe PK_CURVE_t CreateBCurve()
{
    var vertices = stackalloc double[]
    {
        -1.0, 0.0, 0.0,
        1.0, 1.0, 0.0,
    };
    var knotMultiplicities = stackalloc int[] { 2, 2 };
    var knots = stackalloc double[] { 0.0, 1.0 };
    var standardForm = new PK_BCURVE_sf_t(
        1,
        2,
        3,
        PK_LOGICAL_false,
        vertices,
        PK_BCURVE_form_arbitrary_c,
        2,
        knotMultiplicities,
        knots,
        PK_knot_non_uniform_c,
        PK_LOGICAL_false,
        PK_LOGICAL_false,
        PK_self_intersect_false_c);
    PK_BCURVE_t bcurve;
    ParasolidXtCorpusHost.Check(PK_BCURVE_create(&standardForm, &bcurve), "PK_BCURVE_create TRCURVE bcurve");
    return bcurve;
}

static unsafe PK_BODY_t CreateTrimmedWire(PK_CURVE_t basis, double start, double end, double expectedStart = double.NaN, double expectedEnd = double.NaN)
{
    PK_CURVE_t trimmed;
    var basisValue = basis;
    var startValue = start;
    var endValue = end;
    int ifail;
    CRTRCU(&basisValue, &startValue, &endValue, &trimmed, &ifail);
    if (ifail != 0)
        throw new InvalidOperationException("CRTRCU failed with ifail=" + ifail);
    if (trimmed <= 0)
        throw new InvalidOperationException("CRTRCU returned an invalid trimmed curve tag");

    PK_TRCURVE_sf_t standardForm;
    ParasolidXtCorpusHost.Check(PK_TRCURVE_ask(trimmed, &standardForm), "PK_TRCURVE_ask");
    if (standardForm.basis_curve != basis)
        throw new InvalidOperationException("TRCURVE standard form basis curve mismatch");
    var actualStart = standardForm.t_int.value[0];
    var actualEnd = standardForm.t_int.value[1];
    expectedStart = double.IsNaN(expectedStart) ? start : expectedStart;
    expectedEnd = double.IsNaN(expectedEnd) ? end : expectedEnd;
    if (Math.Abs(actualStart - expectedStart) > 1.0e-10 || Math.Abs(actualEnd - expectedEnd) > 1.0e-10)
        throw new InvalidOperationException("TRCURVE interval mismatch: expected [" + expectedStart + "," + expectedEnd + "] got [" + actualStart + "," + actualEnd + "]");
    PK_CLASS_t curveClass;
    ParasolidXtCorpusHost.Check(PK_ENTITY_ask_class(trimmed, &curveClass), "PK_ENTITY_ask_class TRCURVE");
    if (curveClass != PK_CLASS_trcurve)
        throw new InvalidOperationException("CRTRCU returned class " + curveClass + ", expected TRCURVE");

    PK_BODY_t body;
    ParasolidXtCorpusHost.Check(PK_BODY_create_sheet_rectangle(2.0, 2.0, null, &body), "PK_BODY_create_sheet_rectangle TRCURVE");
    var geometry = (PK_GEOM_t)trimmed;
    ParasolidXtCorpusHost.Check(PK_PART_add_geoms(body, 1, &geometry), "PK_PART_add_geoms TRCURVE");
    return body;
}
