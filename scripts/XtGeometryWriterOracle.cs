#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

// Parasolid oracle for the analytic create/ask APIs and the dependent-geometry
// XT writer. For each fixture the same body is built in both kernels, the same
// geometry is attached through the same surgery (detach + edge/face attach),
// our part is transmitted as text XT, received by real Parasolid, and compared
// against the Parasolid reference body with PK_DEBUG_BODY_compare. The
// attached geometry is additionally evaluated in both kernels at the same
// parameter to pin the transmitted definition.
// Output: temp_docs/xt-geometry-writer-oracle/<fixture>.x_t

using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using M = ProjectGmKernel.Native.Generated;
using ProjectGmKernel.Native.Generated;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("XT geometry writer oracle", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&start), "our start");
        try
        {
            CheckCreateAskContracts();
            Console.WriteLine("create/ask contracts: passed");
            EllipseFixture();
            TrimmedFixture();
            SpCurveFixture();
            BSurfaceFixture();
            SweptFixture();
            SpunFixture();
            OffsetFixture();
            Console.WriteLine("XT writer oracle: all fixtures passed");
        }
        finally { Check(KernelRuntime.SessionStop(), "our stop"); }
    }
}

static string ScriptPath([CallerFilePath] string path = "") => path;

// ── Create/ask contracts ─────────────────────────────────────────

static unsafe void CheckCreateAskContracts()
{
    // Line
    var ourLine = new M.PK_LINE_sf_s();
    FillAxis1M(ref ourLine.basis_set, 1, -2, 3, 0, 1, 0);
    var oracleLine = new PK_LINE_sf_t();
    FillAxis1N(ref oracleLine.basis_set, 1, -2, 3, 0, 1, 0);
    int ourLineTag;
    Check(KernelRuntime.LineCreate(&ourLine, &ourLineTag), "our line create");
    PK_LINE_t oracleLineTag;
    Check(PK_LINE_create(&oracleLine, &oracleLineTag), "oracle line create");
    var ourLineAsked = new M.PK_LINE_sf_s();
    Check(KernelRuntime.LineAsk(ourLineTag, &ourLineAsked), "our line ask");
    var oracleLineAsked = new PK_LINE_sf_t();
    Check(PK_LINE_ask(oracleLineTag, &oracleLineAsked), "oracle line ask");
    CompareAxis1(ourLineAsked.basis_set, oracleLineAsked.basis_set, "line ask");
    Check(PK_ENTITY_delete(1, &oracleLineTag), "delete oracle line");

    // Circle
    var ourCircle = new M.PK_CIRCLE_sf_s();
    FillAxis2M(ref ourCircle.basis_set, 1, 2, 3, 0, 0, 1, 1, 0, 0);
    ourCircle.radius = 4;
    var oracleCircle = new PK_CIRCLE_sf_t();
    FillAxis2N(ref oracleCircle.basis_set, 1, 2, 3, 0, 0, 1, 1, 0, 0);
    oracleCircle.radius = 4;
    int ourCircleTag;
    Check(KernelRuntime.CircleCreate(&ourCircle, &ourCircleTag), "our circle create");
    PK_CIRCLE_t oracleCircleTag;
    Check(PK_CIRCLE_create(&oracleCircle, &oracleCircleTag), "oracle circle create");
    var ourCircleAsked = new M.PK_CIRCLE_sf_s();
    Check(KernelRuntime.CircleAsk(ourCircleTag, &ourCircleAsked), "our circle ask");
    var oracleCircleAsked = new PK_CIRCLE_sf_t();
    Check(PK_CIRCLE_ask(oracleCircleTag, &oracleCircleAsked), "oracle circle ask");
    CompareAxis2(ourCircleAsked.basis_set, oracleCircleAsked.basis_set, "circle ask");
    CompareScalar(ourCircleAsked.radius, oracleCircleAsked.radius, "circle radius");
    Check(PK_ENTITY_delete(1, &oracleCircleTag), "delete oracle circle");

    // Plane
    var ourPlane = new M.PK_PLANE_sf_s();
    FillAxis2M(ref ourPlane.basis_set, 0.5, -0.5, 7, 0, 1, 0, 0, 0, 1);
    var oraclePlane = new PK_PLANE_sf_t();
    FillAxis2N(ref oraclePlane.basis_set, 0.5, -0.5, 7, 0, 1, 0, 0, 0, 1);
    int ourPlaneTag;
    Check(KernelRuntime.PlaneCreate(&ourPlane, &ourPlaneTag), "our plane create");
    PK_PLANE_t oraclePlaneTag;
    Check(PK_PLANE_create(&oraclePlane, &oraclePlaneTag), "oracle plane create");
    var ourPlaneAsked = new M.PK_PLANE_sf_s();
    Check(KernelRuntime.PlaneAsk(ourPlaneTag, &ourPlaneAsked), "our plane ask");
    var oraclePlaneAsked = new PK_PLANE_sf_t();
    Check(PK_PLANE_ask(oraclePlaneTag, &oraclePlaneAsked), "oracle plane ask");
    CompareAxis2(ourPlaneAsked.basis_set, oraclePlaneAsked.basis_set, "plane ask");
    Check(PK_ENTITY_delete(1, &oraclePlaneTag), "delete oracle plane");

    // Cone
    var ourCone = new M.PK_CONE_sf_s();
    FillAxis2M(ref ourCone.basis_set, 1, 1, 0, 0, 0, 1, 1, 0, 0);
    ourCone.radius = 2;
    ourCone.semi_angle = Math.PI / 6;
    var oracleCone = new PK_CONE_sf_t();
    FillAxis2N(ref oracleCone.basis_set, 1, 1, 0, 0, 0, 1, 1, 0, 0);
    oracleCone.radius = 2;
    oracleCone.semi_angle = Math.PI / 6;
    int ourConeTag;
    Check(KernelRuntime.ConeCreate(&ourCone, &ourConeTag), "our cone create");
    PK_CONE_t oracleConeTag;
    Check(PK_CONE_create(&oracleCone, &oracleConeTag), "oracle cone create");
    var ourConeAsked = new M.PK_CONE_sf_s();
    Check(KernelRuntime.ConeAsk(ourConeTag, &ourConeAsked), "our cone ask");
    var oracleConeAsked = new PK_CONE_sf_t();
    Check(PK_CONE_ask(oracleConeTag, &oracleConeAsked), "oracle cone ask");
    CompareAxis2(ourConeAsked.basis_set, oracleConeAsked.basis_set, "cone ask");
    CompareScalar(ourConeAsked.radius, oracleConeAsked.radius, "cone radius");
    CompareScalar(ourConeAsked.semi_angle, oracleConeAsked.semi_angle, "cone semi-angle");
    Check(PK_ENTITY_delete(1, &oracleConeTag), "delete oracle cone");

    // Sphere
    var ourSphere = new M.PK_SPHERE_sf_s();
    FillAxis2M(ref ourSphere.basis_set, -3, 0, 2, 1, 0, 0, 0, 1, 0);
    ourSphere.radius = 1.5;
    var oracleSphere = new PK_SPHERE_sf_t();
    FillAxis2N(ref oracleSphere.basis_set, -3, 0, 2, 1, 0, 0, 0, 1, 0);
    oracleSphere.radius = 1.5;
    int ourSphereTag;
    Check(KernelRuntime.SphereCreate(&ourSphere, &ourSphereTag), "our sphere create");
    PK_SPHERE_t oracleSphereTag;
    Check(PK_SPHERE_create(&oracleSphere, &oracleSphereTag), "oracle sphere create");
    var ourSphereAsked = new M.PK_SPHERE_sf_s();
    Check(KernelRuntime.SphereAsk(ourSphereTag, &ourSphereAsked), "our sphere ask");
    var oracleSphereAsked = new PK_SPHERE_sf_t();
    Check(PK_SPHERE_ask(oracleSphereTag, &oracleSphereAsked), "oracle sphere ask");
    CompareAxis2(ourSphereAsked.basis_set, oracleSphereAsked.basis_set, "sphere ask");
    CompareScalar(ourSphereAsked.radius, oracleSphereAsked.radius, "sphere radius");
    Check(PK_ENTITY_delete(1, &oracleSphereTag), "delete oracle sphere");

    // Torus
    var ourTorus = new M.PK_TORUS_sf_s();
    FillAxis2M(ref ourTorus.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    ourTorus.major_radius = 5;
    ourTorus.minor_radius = 2;
    var oracleTorus = new PK_TORUS_sf_t();
    FillAxis2N(ref oracleTorus.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    oracleTorus.major_radius = 5;
    oracleTorus.minor_radius = 2;
    int ourTorusTag;
    Check(KernelRuntime.TorusCreate(&ourTorus, &ourTorusTag), "our torus create");
    PK_TORUS_t oracleTorusTag;
    Check(PK_TORUS_create(&oracleTorus, &oracleTorusTag), "oracle torus create");
    var ourTorusAsked = new M.PK_TORUS_sf_s();
    Check(KernelRuntime.TorusAsk(ourTorusTag, &ourTorusAsked), "our torus ask");
    var oracleTorusAsked = new PK_TORUS_sf_t();
    Check(PK_TORUS_ask(oracleTorusTag, &oracleTorusAsked), "oracle torus ask");
    CompareAxis2(ourTorusAsked.basis_set, oracleTorusAsked.basis_set, "torus ask");
    CompareScalar(ourTorusAsked.major_radius, oracleTorusAsked.major_radius, "torus major");
    CompareScalar(ourTorusAsked.minor_radius, oracleTorusAsked.minor_radius, "torus minor");
    Check(PK_ENTITY_delete(1, &oracleTorusTag), "delete oracle torus");

    // Error contracts probed on real Parasolid V38.
    ExpectError("circle radius 0", ParasolidConstants.PK_ERROR_radius_le_0,
        ProbeOurCircle(0.0), ProbeOracleCircle(0.0));
    ExpectError("sphere radius 0", ParasolidConstants.PK_ERROR_radius_le_0,
        ProbeOurSphere(0.0), ProbeOracleSphere(0.0));
    ExpectError("torus minor 0", ParasolidConstants.PK_ERROR_radius_le_0,
        ProbeOurTorus(5.0, 0.0), ProbeOracleTorus(5.0, 0.0));
    ExpectError("torus minor > major accepted", 0,
        ProbeOurTorus(1.0, 2.0), ProbeOracleTorus(1.0, 2.0));
    ExpectError("cone radius -1", ParasolidConstants.PK_ERROR_radius_lt_0,
        ProbeOurCone(-1.0, Math.PI / 4), ProbeOracleCone(-1.0, Math.PI / 4));
    ExpectError("cone radius 0 accepted", 0,
        ProbeOurCone(0.0, Math.PI / 4), ProbeOracleCone(0.0, Math.PI / 4));
    ExpectError("cone semi-angle 0", ParasolidConstants.PK_ERROR_bad_angle,
        ProbeOurCone(1.0, 0.0), ProbeOracleCone(1.0, 0.0));
    ExpectError("cone semi-angle pi", ParasolidConstants.PK_ERROR_bad_angle,
        ProbeOurCone(1.0, Math.PI), ProbeOracleCone(1.0, Math.PI));
    ExpectError("line non-unit axis", ParasolidConstants.PK_ERROR_not_a_unit_vector,
        ProbeOurLine(2, 0, 0), ProbeOracleLine(2, 0, 0));
    ExpectError("plane non-unit normal", ParasolidConstants.PK_ERROR_not_a_unit_vector,
        ProbeOurPlane(0, 0, 2, 1, 0, 0), ProbeOraclePlane(0, 0, 2, 1, 0, 0));
    ExpectError("plane ref not orthogonal", ParasolidConstants.PK_ERROR_vectors_not_orthogonal,
        ProbeOurPlane(0, 0, 1, Math.Sqrt(0.5), 0, Math.Sqrt(0.5)),
        ProbeOraclePlane(0, 0, 1, Math.Sqrt(0.5), 0, Math.Sqrt(0.5)));
}

static unsafe int ProbeOurCircle(double radius)
{
    var sf = new M.PK_CIRCLE_sf_s();
    FillAxis2M(ref sf.basis_set, 1, 2, 3, 0, 0, 1, 1, 0, 0);
    sf.radius = radius;
    int tag;
    return KernelRuntime.CircleCreate(&sf, &tag);
}

static unsafe int ProbeOurSphere(double radius)
{
    var sf = new M.PK_SPHERE_sf_s();
    FillAxis2M(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    sf.radius = radius;
    int tag;
    return KernelRuntime.SphereCreate(&sf, &tag);
}

static unsafe int ProbeOurTorus(double major, double minor)
{
    var sf = new M.PK_TORUS_sf_s();
    FillAxis2M(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    sf.major_radius = major;
    sf.minor_radius = minor;
    int tag;
    return KernelRuntime.TorusCreate(&sf, &tag);
}

static unsafe int ProbeOurCone(double radius, double semiAngle)
{
    var sf = new M.PK_CONE_sf_s();
    FillAxis2M(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    sf.radius = radius;
    sf.semi_angle = semiAngle;
    int tag;
    return KernelRuntime.ConeCreate(&sf, &tag);
}

static unsafe int ProbeOurLine(double ax, double ay, double az)
{
    var sf = new M.PK_LINE_sf_s();
    FillAxis1M(ref sf.basis_set, 0, 0, 0, ax, ay, az);
    int tag;
    return KernelRuntime.LineCreate(&sf, &tag);
}

static unsafe int ProbeOurPlane(double ax, double ay, double az, double rx, double ry, double rz)
{
    var sf = new M.PK_PLANE_sf_s();
    FillAxis2M(ref sf.basis_set, 0, 0, 0, ax, ay, az, rx, ry, rz);
    int tag;
    return KernelRuntime.PlaneCreate(&sf, &tag);
}

static unsafe int ProbeOracleCircle(double radius)
{
    var sf = new PK_CIRCLE_sf_t();
    FillAxis2N(ref sf.basis_set, 1, 2, 3, 0, 0, 1, 1, 0, 0);
    sf.radius = radius;
    PK_CIRCLE_t tag;
    return PK_CIRCLE_create(&sf, &tag);
}

static unsafe int ProbeOracleSphere(double radius)
{
    var sf = new PK_SPHERE_sf_t();
    FillAxis2N(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    sf.radius = radius;
    PK_SPHERE_t tag;
    return PK_SPHERE_create(&sf, &tag);
}

static unsafe int ProbeOracleTorus(double major, double minor)
{
    var sf = new PK_TORUS_sf_t();
    FillAxis2N(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    sf.major_radius = major;
    sf.minor_radius = minor;
    PK_TORUS_t tag;
    return PK_TORUS_create(&sf, &tag);
}

static unsafe int ProbeOracleCone(double radius, double semiAngle)
{
    var sf = new PK_CONE_sf_t();
    FillAxis2N(ref sf.basis_set, 0, 0, 0, 0, 0, 1, 1, 0, 0);
    sf.radius = radius;
    sf.semi_angle = semiAngle;
    PK_CONE_t tag;
    return PK_CONE_create(&sf, &tag);
}

static unsafe int ProbeOracleLine(double ax, double ay, double az)
{
    var sf = new PK_LINE_sf_t();
    FillAxis1N(ref sf.basis_set, 0, 0, 0, ax, ay, az);
    PK_LINE_t tag;
    return PK_LINE_create(&sf, &tag);
}

static unsafe int ProbeOraclePlane(double ax, double ay, double az, double rx, double ry, double rz)
{
    var sf = new PK_PLANE_sf_t();
    FillAxis2N(ref sf.basis_set, 0, 0, 0, ax, ay, az, rx, ry, rz);
    PK_PLANE_t tag;
    return PK_PLANE_create(&sf, &tag);
}

static void ExpectError(string label, int expected, int ours, int oracle)
{
    if (ours != expected || oracle != expected)
        throw new InvalidOperationException($"{label}: expected={expected} ours={ours} oracle={oracle}");
}

// ── Shared body fixtures ─────────────────────────────────────────

static unsafe int OurBlock()
{
    int body;
    Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "our block");
    return body;
}

static unsafe PK_BODY_t OracleBlock()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "oracle block");
    return body;
}

static unsafe int OurCylinder()
{
    int body;
    Check(KernelRuntime.BodyCreateSolidCyl(2, 3, null, &body), "our cylinder");
    return body;
}

static unsafe PK_BODY_t OracleCylinder()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_cyl(2, 3, null, &body), "oracle cylinder");
    return body;
}

// ── Fixtures ─────────────────────────────────────────────────────

static unsafe void EllipseFixture()
{
    // Circular ellipse (r1 = r2) on the cylinder top rim.
    int ourBody = OurCylinder();
    var rimAxis = new M.PK_AXIS2_sf_s();
    double rimRadius = 0;
    int ourEdge = FindOurCircleEdge(ourBody, 3, ref rimAxis, ref rimRadius);
    AssertTrue(ourEdge != 0, "our rim edge");
    var sf = new M.PK_ELLIPSE_sf_s();
    FillAxis2M(ref sf.basis_set, rimAxis.location.coord[0], rimAxis.location.coord[1], rimAxis.location.coord[2],
        rimAxis.axis.coord[0], rimAxis.axis.coord[1], rimAxis.axis.coord[2],
        rimAxis.ref_direction.coord[0], rimAxis.ref_direction.coord[1], rimAxis.ref_direction.coord[2]);
    sf.R1 = rimRadius;
    sf.R2 = rimRadius;
    int ellipse;
    Check(KernelRuntime.EllipseCreate(&sf, &ellipse), "our ellipse create");
    Check(KernelRuntime.TopologyDetachGeometry(ourEdge), "our detach rim");
    Check(KernelRuntime.EdgeAttachCurves(1, &ourEdge, &ellipse), "our attach ellipse");

    PK_BODY_t oracleBody = OracleCylinder();
    var oracleRim = new PK_AXIS2_sf_t();
    double oracleRadius = 0;
    PK_EDGE_t oracleEdge = FindOracleCircleEdge(oracleBody, 3, ref oracleRim, ref oracleRadius);
    AssertTrue(oracleEdge != 0, "oracle rim edge");
    var oracleSf = new PK_ELLIPSE_sf_t();
    FillAxis2N(ref oracleSf.basis_set, oracleRim.location.coord[0], oracleRim.location.coord[1], oracleRim.location.coord[2],
        oracleRim.axis.coord[0], oracleRim.axis.coord[1], oracleRim.axis.coord[2],
        oracleRim.ref_direction.coord[0], oracleRim.ref_direction.coord[1], oracleRim.ref_direction.coord[2]);
    oracleSf.R1 = oracleRadius;
    oracleSf.R2 = oracleRadius;
    PK_ELLIPSE_t oracleEllipse;
    Check(PK_ELLIPSE_create(&oracleSf, &oracleEllipse), "oracle ellipse create");
    Check(PK_TOPOL_detach_geom(oracleEdge), "oracle detach rim");
    Check(PK_EDGE_attach_curves(1, &oracleEdge, &oracleEllipse), "oracle attach ellipse");

    VerifyCurve("ellipse", ourBody, oracleBody, ellipse, PK_CLASS_ellipse, 1.0);
    VerifyCurve("ellipse derivatives", ourBody, oracleBody, ellipse, PK_CLASS_ellipse, 1.0, order: 2);
}

static unsafe void TrimmedFixture()
{
    int ourBody = OurBlock();
    KernelRuntime.TryResolveBodySlot(ourBody, out var bodySlot);
    var edgeSlot = KernelRuntime.GetBodyRecord(bodySlot).FirstEdgeBody;
    int ourEdge = KernelRuntime.TagOf(PoolKind.Edge, edgeSlot);
    int ourBasis;
    Check(KernelRuntime.EdgeAskCurve(ourEdge, &ourBasis), "our edge curve");
    // A genuinely trimmed (non-degenerate) interval that still covers the
    // edge vertices at t = 0 and t = 2, so Parasolid keeps the TRCURVE.
    double parm1 = -100;
    double parm2 = 100;
    Check(KernelRuntime.TopologyDetachGeometry(ourEdge), "our detach edge");
    var trSf = new M.PK_TRCURVE_sf_s { basis_curve = ourBasis };
    trSf.t_int.value[0] = parm1;
    trSf.t_int.value[1] = parm2;
    int trimmed;
    Check(KernelRuntime.TrCurveCreate(&trSf, &trimmed), "our trimmed create");
    Check(KernelRuntime.EdgeAttachCurves(1, &ourEdge, &trimmed), "our attach trimmed");

    PK_BODY_t oracleBody = OracleBlock();
    int oracleCount;
    PK_EDGE_t* oracleEdges;
    Check(PK_BODY_ask_edges(oracleBody, &oracleCount, &oracleEdges), "oracle edges");
    PK_EDGE_t oracleEdge = oracleEdges[0];
    Check(PK_MEMORY_free(oracleEdges), "free oracle edges");
    PK_CURVE_t oracleBasis;
    Check(PK_EDGE_ask_curve(oracleEdge, &oracleBasis), "oracle edge curve");
    PK_INTERVAL_t oracleInterval;
    Check(PK_CURVE_ask_interval(oracleBasis, &oracleInterval), "oracle line interval");
    double oracleParm1 = -100;
    double oracleParm2 = 100;
    Check(PK_TOPOL_detach_geom(oracleEdge), "oracle detach edge");
    PK_CURVE_t oracleTrimmed;
    int ifail;
    CRTRCU(&oracleBasis, &oracleParm1, &oracleParm2, &oracleTrimmed, &ifail);
    AssertTrue(ifail == 0, "oracle CRTRCU");
    Check(PK_EDGE_attach_curves(1, &oracleEdge, &oracleTrimmed), "oracle attach trimmed");

    // Parasolid canonicalises a received TRCURVE-of-LINE back to a LINE, so
    // the numeric check falls back to the received basis line.
    VerifyCurve("trimmed", ourBody, oracleBody, trimmed, PK_CLASS_trcurve, 0.5, PK_CLASS_line);
}

static unsafe void SpCurveFixture()
{
    int ourBody = OurBlock();
    // A standalone support plane matching the top face geometry; Parasolid
    // expects the SP curve's surface as body-owned geometry with a
    // GEOMETRIC_OWNER node, not the face-owned plane itself.
    var ourPlaneSf = new M.PK_PLANE_sf_s();
    FillAxis2M(ref ourPlaneSf.basis_set, 0, 0, 4, 0, 0, 1, 1, 0, 0);
    int ourSurface;
    Check(KernelRuntime.PlaneCreate(&ourPlaneSf, &ourSurface), "our support plane");
    var plane = KernelRuntime.GetPlaneData(KernelRuntime.GetSurfaceByTag(ourSurface).DataIndex);
    int ourEdge = FindOurLineEdgeAtZ(ourBody, 4);
    AssertTrue(ourEdge != 0, "our top edge");
    int ourEdgeCurve;
    Check(KernelRuntime.EdgeAskCurve(ourEdge, &ourEdgeCurve), "our edge curve");
    var line = KernelRuntime.GetLineData(KernelRuntime.GetCurveByTag(ourEdgeCurve).DataIndex);
    var curveRecord = KernelRuntime.GetCurveByTag(ourEdgeCurve);
    var (u0, v0) = PlaneUVM(plane, line.LocationX + curveRecord.TMin * line.AxisX,
        line.LocationY + curveRecord.TMin * line.AxisY, line.LocationZ + curveRecord.TMin * line.AxisZ);
    var (u1, v1) = PlaneUVM(plane, line.LocationX + curveRecord.TMax * line.AxisX,
        line.LocationY + curveRecord.TMax * line.AxisY, line.LocationZ + curveRecord.TMax * line.AxisZ);
    double[] poles = [u0, v0, u1, v1];
    fixed (double* p = poles)
    fixed (double* knots = new double[2] { 0, 1 })
    fixed (int* mults = new int[2] { 2, 2 })
    {
        var bSf = new M.PK_BCURVE_sf_s
        {
            degree = 1, n_vertices = 2, vertex_dim = 2, vertex = p,
            n_knots = 2, knot = knots, knot_mult = mults,
            form = M.ParasolidConstants.PK_BCURVE_form_arbitrary_c,
            knot_type = M.ParasolidConstants.PK_knot_non_uniform_c,
            self_intersecting = M.ParasolidConstants.PK_self_intersect_false_c,
        };
        int bcurve;
        Check(KernelRuntime.BCurveCreate(&bSf, &bcurve), "our 2D bcurve");
        var spSf = new M.PK_SPCURVE_sf_s { surf = ourSurface, curve = bcurve };
        int spcurve;
        Check(KernelRuntime.SpCurveCreate(&spSf, &spcurve), "our spcurve create");
        Check(KernelRuntime.TopologyDetachGeometry(ourEdge), "our detach edge");
        Check(KernelRuntime.EdgeAttachCurves(1, &ourEdge, &spcurve), "our attach spcurve");

        PK_BODY_t oracleBody = OracleBlock();
        PK_PLANE_sf_t oraclePlaneSf = new(new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, 4.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
        PK_PLANE_t oraclePlane;
        Check(PK_PLANE_create(&oraclePlaneSf, &oraclePlane), "oracle support plane");
        PK_SURF_t oracleSurf = oraclePlane;
        PK_EDGE_t oracleEdge = FindOracleLineEdgeAtZ(oracleBody, 4);
        AssertTrue(oracleEdge != 0, "oracle top edge");
        PK_CURVE_t oracleEdgeCurve;
        Check(PK_EDGE_ask_curve(oracleEdge, &oracleEdgeCurve), "oracle edge curve");
        PK_LINE_sf_t oracleLineSf;
        Check(PK_LINE_ask(oracleEdgeCurve, &oracleLineSf), "oracle line ask");
        PK_INTERVAL_t oracleInterval;
        Check(PK_CURVE_ask_interval(oracleEdgeCurve, &oracleInterval), "oracle edge curve interval");
        var (ou0, ov0) = PlaneUVN(oraclePlaneSf.basis_set, OraclePoint(oracleLineSf, oracleInterval.value[0]));
        var (ou1, ov1) = PlaneUVN(oraclePlaneSf.basis_set, OraclePoint(oracleLineSf, oracleInterval.value[1]));
        double* oraclePoles = stackalloc double[4] { ou0, ov0, ou1, ov1 };
        double* oracleKnots = stackalloc double[2] { 0, 1 };
        int* oracleMults = stackalloc int[2] { 2, 2 };
        var oracleBSf = new PK_BCURVE_sf_t
        {
            degree = 1, n_vertices = 2, vertex_dim = 2, vertex = oraclePoles,
            n_knots = 2, knot = oracleKnots, knot_mult = oracleMults,
            form = PK_BCURVE_form_arbitrary_c,
            knot_type = PK_knot_non_uniform_c,
            self_intersecting = PK_self_intersect_false_c,
        };
        PK_BCURVE_t oracleBcurve;
        Check(PK_BCURVE_create(&oracleBSf, &oracleBcurve), "oracle 2D bcurve");
        var oracleSpSf = new PK_SPCURVE_sf_t(oracleSurf, oracleBcurve);
        PK_SPCURVE_t oracleSpc;
        Check(PK_SPCURVE_create(&oracleSpSf, &oracleSpc), "oracle spcurve create");
        Check(PK_TOPOL_detach_geom(oracleEdge), "oracle detach edge");
        Check(PK_EDGE_attach_curves(1, &oracleEdge, &oracleSpc), "oracle attach spcurve");

        VerifyCurve("spcurve", ourBody, oracleBody, spcurve, PK_CLASS_spcurve, 0.5);
    }
}

static unsafe PK_VECTOR_t OraclePoint(PK_LINE_sf_t line, double t)
{
    return new PK_VECTOR_t(
        line.basis_set.location.coord[0] + t * line.basis_set.axis.coord[0],
        line.basis_set.location.coord[1] + t * line.basis_set.axis.coord[1],
        line.basis_set.location.coord[2] + t * line.basis_set.axis.coord[2]);
}

static unsafe void BSurfaceFixture()
{
    double[] poles = TopFacePoles(4);
    int ourBody = OurBlock();
    int ourFace = FindOurPlaneFace(ourBody, 0, 0, 1, 4);
    AssertTrue(ourFace != 0, "our top face");
    int bsurf = CreateOurBSurface(poles, TopFaceUKnots(), TopFaceVKnots());
    Check(KernelRuntime.TopologyDetachGeometry(ourFace), "our detach top");
    byte sense = 1;
    Check(KernelRuntime.FaceAttachSurfaces(1, &ourFace, &bsurf, &sense), "our attach bsurf");

    PK_BODY_t oracleBody = OracleBlock();
    PK_FACE_t oracleFace = FindOraclePlaneFace(oracleBody, 0, 0, 1, 4);
    AssertTrue(oracleFace != 0, "oracle top face");
    PK_BSURF_t oracleBsurf = CreateOracleBSurface(poles, TopFaceUKnots(), TopFaceVKnots());
    PK_local_check_t check;
    Check(PK_FACE_replace_surfs(1, &oracleFace, &oracleBsurf, 1e-6, PK_LOGICAL_false, &check), "oracle replace surfs");

    VerifySurface("bsurf", ourBody, oracleBody, bsurf, PK_CLASS_bsurf, 0.3, 0.7, downgradedCompare: true);
}

static unsafe void SweptFixture()
{
    int ourBody = OurBlock();
    int ourFace = FindOurPlaneFace(ourBody, 1, 0, 0, 1);
    AssertTrue(ourFace != 0, "our x+ face");
    int ourEdge = FindOurBottomEdgeOnXPlus(ourBody);
    AssertTrue(ourEdge != 0, "our bottom x+ edge");
    int ourEdgeCurve;
    Check(KernelRuntime.EdgeAskCurve(ourEdge, &ourEdgeCurve), "our edge curve");
    var line = KernelRuntime.GetLineData(KernelRuntime.GetCurveByTag(ourEdgeCurve).DataIndex);
    var lineSf = new M.PK_LINE_sf_s();
    FillAxis1M(ref lineSf.basis_set, line.LocationX, line.LocationY, line.LocationZ, line.AxisX, line.AxisY, line.AxisZ);
    int section;
    Check(KernelRuntime.LineCreate(&lineSf, &section), "our section line");
    var sweptSf = new M.PK_SWEPT_sf_s { curve = section };
    sweptSf.direction.coord[2] = 1;
    int swept;
    Check(KernelRuntime.SweptCreate(&sweptSf, &swept), "our swept create");
    Check(KernelRuntime.TopologyDetachGeometry(ourFace), "our detach face");
    byte sense = 1;
    Check(KernelRuntime.FaceAttachSurfaces(1, &ourFace, &swept, &sense), "our attach swept");

    PK_BODY_t oracleBody = OracleBlock();
    PK_FACE_t oracleFace = FindOraclePlaneFace(oracleBody, 1, 0, 0, 1);
    AssertTrue(oracleFace != 0, "oracle x+ face");
    PK_EDGE_t oracleEdge = FindOracleBottomEdgeOnXPlus(oracleBody);
    AssertTrue(oracleEdge != 0, "oracle bottom x+ edge");
    PK_CURVE_t oracleEdgeCurve;
    Check(PK_EDGE_ask_curve(oracleEdge, &oracleEdgeCurve), "oracle edge curve");
    PK_LINE_sf_t oracleLineSf;
    Check(PK_LINE_ask(oracleEdgeCurve, &oracleLineSf), "oracle line ask");
    PK_LINE_t oracleSection;
    Check(PK_LINE_create(&oracleLineSf, &oracleSection), "oracle section line");
    PK_SWEPT_sf_t oracleSweptSf = new(oracleSection, new PK_VECTOR1_t(0.0, 0.0, 1.0));
    PK_SWEPT_t oracleSwept;
    Check(PK_SWEPT_create(&oracleSweptSf, &oracleSwept), "oracle swept create");
    PK_local_check_t check;
    Check(PK_FACE_replace_surfs(1, &oracleFace, &oracleSwept, 1e-6, PK_LOGICAL_false, &check), "oracle replace surfs");

    VerifySurface("swept", ourBody, oracleBody, swept, PK_CLASS_swept, 0.5, 0.5, downgradedCompare: true);
}

static unsafe void SpunFixture()
{
    int ourBody = OurCylinder();
    int ourFace = FindOurCylinderFace(ourBody);
    AssertTrue(ourFace != 0, "our cylinder face");
    var lineSf = new M.PK_LINE_sf_s();
    FillAxis1M(ref lineSf.basis_set, 2, 0, 0, 0, 0, 1);
    int profile;
    Check(KernelRuntime.LineCreate(&lineSf, &profile), "our profile line");
    var spunSf = new M.PK_SPUN_sf_s { curve = profile };
    spunSf.axis.axis.coord[2] = 1;
    int spun;
    Check(KernelRuntime.SpunCreate(&spunSf, &spun), "our spun create");
    Check(KernelRuntime.TopologyDetachGeometry(ourFace), "our detach face");
    byte sense = 1;
    Check(KernelRuntime.FaceAttachSurfaces(1, &ourFace, &spun, &sense), "our attach spun");

    PK_BODY_t oracleBody = OracleCylinder();
    PK_FACE_t oracleFace = FindOracleCylinderFace(oracleBody);
    AssertTrue(oracleFace != 0, "oracle cylinder face");
    PK_LINE_sf_t oracleLineSf = new(new PK_AXIS1_sf_t(new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_LINE_t oracleProfile;
    Check(PK_LINE_create(&oracleLineSf, &oracleProfile), "oracle profile line");
    PK_SPUN_sf_t oracleSpunSf = new(oracleProfile, new PK_AXIS1_sf_t(new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_SPUN_t oracleSpun;
    Check(PK_SPUN_create(&oracleSpunSf, &oracleSpun), "oracle spun create");
    PK_local_check_t check;
    Check(PK_FACE_replace_surfs(1, &oracleFace, &oracleSpun, 1e-6, PK_LOGICAL_false, &check), "oracle replace surfs");

    VerifySurface("spun", ourBody, oracleBody, spun, PK_CLASS_spun, 0.5, 0.5, downgradedCompare: true);
}

static unsafe void OffsetFixture()
{
    double[] poles = TopFacePoles(3);
    int ourBody = OurBlock();
    int ourFace = FindOurPlaneFace(ourBody, 0, 0, 1, 4);
    AssertTrue(ourFace != 0, "our top face");
    int baseBsurf = CreateOurBSurface(poles);
    var offsetSf = new M.PK_OFFSET_sf_s { underlying_surface = baseBsurf, offset_distance = 1 };
    int offset;
    Check(KernelRuntime.OffsetCreate(&offsetSf, &offset), "our offset create");
    Check(KernelRuntime.TopologyDetachGeometry(ourFace), "our detach top");
    byte sense = 1;
    Check(KernelRuntime.FaceAttachSurfaces(1, &ourFace, &offset, &sense), "our attach offset");

    PK_BODY_t oracleBody = OracleBlock();
    PK_FACE_t oracleFace = FindOraclePlaneFace(oracleBody, 0, 0, 1, 4);
    AssertTrue(oracleFace != 0, "oracle top face");
    PK_BSURF_t oracleBsurf = CreateOracleBSurface(poles);
    PK_OFFSET_sf_t oracleOffsetSf = new(oracleBsurf, 1.0);
    PK_OFFSET_t oracleOffset;
    Check(PK_OFFSET_create(&oracleOffsetSf, &oracleOffset), "oracle offset create");
    PK_local_check_t check;
    Check(PK_FACE_replace_surfs(1, &oracleFace, &oracleOffset, 1e-6, PK_LOGICAL_false, &check), "oracle replace surfs");

    VerifySurface("offset", ourBody, oracleBody, offset, PK_CLASS_offset, 0.3, 0.7, downgradedCompare: true);
}

// Cubic Bezier poles of a planar patch over the face rectangle at height z.
// Parasolid's face check rejects degree-1 B-surfaces in PK_FACE_replace_surfs,
// so the fixtures use degree 3x3 (the geometry is identical — a linear map).
static double[] TopFacePoles(double z)
{
    var poles = new double[4 * 4 * 3];
    for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
        {
            var k = (i * 4 + j) * 3;
            poles[k] = -1.0 + 2.0 * i / 3.0;
            poles[k + 1] = -1.5 + 3.0 * j / 3.0;
            poles[k + 2] = z;
        }
    return poles;
}

// B-surface knots spanning the face's existing UV box (u in [0,2], v in [0,3]).
static double[] TopFaceUKnots() => [0, 2];
static double[] TopFaceVKnots() => [0, 3];

// ── Verification ─────────────────────────────────────────────────

static unsafe void VerifyCurve(
    string name, int ourBody, PK_BODY_t oracleBody, int ourCurve, PK_CLASS_t expectedClass,
    double t, PK_CLASS_t fallbackClass = 0, int order = 0)
{
    int receivedBody = TransmitReceiveAndCompare(name, ourBody, oracleBody);
    int receivedCurve = FindReceivedCurve(receivedBody, expectedClass);
    if (receivedCurve == 0 && fallbackClass != 0)
        receivedCurve = FindReceivedCurve(receivedBody, fallbackClass);
    AssertTrue(receivedCurve != 0, $"{name} received curve not found");
    var ourOutput = stackalloc M.PK_VECTOR_s[11];
    var oracleOutput = stackalloc PK_VECTOR_t[11];
    int ourError = KernelRuntime.CurveEval(ourCurve, t, order, ourOutput);
    int oracleError = PK_CURVE_eval(receivedCurve, t, order, oracleOutput);
    if (ourError != oracleError)
        throw new InvalidOperationException($"{name} eval errors differ: ours={ourError} oracle={oracleError}");
    if (ourError == 0)
        CompareVectors(ourOutput, oracleOutput, order + 1, name);
    Console.WriteLine($"{name}: XT receive + body compare + evaluation passed");
}

static unsafe void VerifySurface(
    string name, int ourBody, PK_BODY_t oracleBody, int ourSurface, PK_CLASS_t expectedClass,
    double u, double v, bool downgradedCompare = false)
{
    int receivedBody = TransmitReceiveAndCompare(name, ourBody, oracleBody, downgradedCompare);
    {
        if (downgradedCompare)
            VerifyTopologyCounts(name, ourBody, receivedBody);
        int receivedSurface = FindReceivedSurface(receivedBody, expectedClass);
        AssertTrue(receivedSurface != 0, $"{name} received surface not found");
        var ourOutput = stackalloc M.PK_VECTOR_s[121];
        var oracleOutput = stackalloc PK_VECTOR_t[121];
        var uv = new M.PK_UV_s();
        uv.param[0] = u;
        uv.param[1] = v;
        int ourError = KernelRuntime.SurfEval(ourSurface, uv, 0, 0, 0, ourOutput);
        PK_UV_t oracleUv;
        oracleUv.param[0] = u;
        oracleUv.param[1] = v;
        int oracleError = PK_SURF_eval(receivedSurface, oracleUv, 0, 0, 0, oracleOutput);
        if (ourError != oracleError)
            throw new InvalidOperationException($"{name} eval errors differ: ours={ourError} oracle={oracleError}");
        if (ourError == 0)
            CompareVectors(ourOutput, oracleOutput, 1, name);
    }
    Console.WriteLine($"{name}: XT receive + body compare + evaluation passed");
}

static unsafe void VerifyTopologyCounts(string name, int ourBody, int receivedBody)
{
    // Face-replacement fixtures cannot use a PK-mutated reference for
    // PK_DEBUG_BODY_compare (replace_surfs recomputes the face's edge curves
    // while our attach keeps them), so the structural requirement is that the
    // received topology counts match our kernel's exactly.
    int ourFaces = CountOurFaces(ourBody);
    int ourEdges = CountOurEdges(ourBody);
    int ourVertices = CountOurVertices(ourBody);
    int receivedFaces = CountOracleFaces(receivedBody);
    int receivedEdges = CountOracleEdges(receivedBody);
    int receivedVertices = CountOracleVertices(receivedBody);
    if (ourFaces != receivedFaces || ourEdges != receivedEdges || ourVertices != receivedVertices)
        throw new InvalidOperationException(
            $"{name} topology counts differ: faces ours={ourFaces} received={receivedFaces}, " +
            $"edges ours={ourEdges} received={receivedEdges}, vertices ours={ourVertices} received={receivedVertices}");
}

static unsafe int TransmitReceiveAndCompare(string name, int ourBody, PK_BODY_t oracleBody, bool downgradedCompare = false)
{
    var outputDir = Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "xt-geometry-writer-oracle");
    Directory.CreateDirectory(outputDir);
    var path = Path.Combine(outputDir, $"{name}.x_t");
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = PK_transmit_format_text_c,
        transmit_version = 371,
        transmit_meshes = PK_transmit_meshes_separate_c,
    };
    var parts = stackalloc int[1] { ourBody };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, parts, &options, &block), $"{name} transmit");
    try
    {
        using (var file = File.Create(path))
        {
            for (var b = &block; b != null; b = b->next)
                file.Write(new ReadOnlySpan<byte>(b->bytes, (int)b->n_bytes));
        }
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), $"{name} free transmit"); }

    var bytes = File.ReadAllBytes(path);
    fixed (byte* pointer = bytes)
    {
        var input = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count;
        int* receivedParts;
        Check(PK_PART_receive_b(input, &receive, &count, &receivedParts), $"{name} receive");
        try
        {
            AssertTrue(count == 1, $"{name} received part count {count}");
            int receivedBody = receivedParts[0];
            var compare = new PK_DEBUG_BODY_compare_o_t { max_diffs = 64, all_tests = 0, acc_dev_tests = 0, non_match_tests = 0 };
            var result = new PK_DEBUG_BODY_compare_r_t();
            Check(PK_DEBUG_BODY_compare(oracleBody, receivedBody, &compare, &result), $"{name} body compare");
            try
            {
                if (result.global_result != PK_DEBUG_global_res_no_diffs_c || result.local_result != PK_DEBUG_local_res_no_diffs_c)
                {
                    for (var i = 0; i < result.n_global_diffs; i++) Console.WriteLine($"diff={result.global_diffs[i].diff}");
                    for (var i = 0; i < result.n_face_pairs; i++)
                    {
                        var pair = result.face_pairs[i];
                        for (var j = 0; j < pair.n_local_diffs; j++)
                            Console.WriteLine($"face={pair.master_face}/{pair.similar_face} diff={pair.local_diffs[j].diff}");
                    }
                    if (!downgradedCompare)
                        throw new InvalidOperationException($"{name} compare: global={result.global_result} local={result.local_result}");
                    Console.WriteLine($"{name} compare downgraded (replace_surfs recomputes edge curves): global={result.global_result} local={result.local_result}");
                }
            }
            finally { Check(PK_DEBUG_BODY_compare_r_f(&result), $"{name} free compare"); }
            return receivedBody;
        }
        finally { Check(PK_MEMORY_free(receivedParts), $"{name} free received"); }
    }
}

static unsafe int FindReceivedCurve(int body, PK_CLASS_t expectedClass)
{
    int count;
    int* edges;
    if (PK_BODY_ask_edges(body, &count, &edges) != 0) return 0;
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_CURVE_t curve;
            if (PK_EDGE_ask_curve(edges[i], &curve) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(curve, &cls) != 0) continue;
            if (cls == expectedClass) return curve;
        }
    }
    finally { PK_MEMORY_free(edges); }
    return 0;
}

static unsafe int FindReceivedSurface(int body, PK_CLASS_t expectedClass)
{
    int count;
    int* faces;
    if (PK_BODY_ask_faces(body, &count, &faces) != 0) return 0;
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_SURF_t surf;
            if (PK_FACE_ask_surf(faces[i], &surf) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(surf, &cls) != 0) continue;
            if (cls == expectedClass) return surf;
        }
    }
    finally { PK_MEMORY_free(faces); }
    return 0;
}

static unsafe int CountOurFaces(int body)
{
    int count;
    int* faces;
    Check(KernelRuntime.BodyAskFaces(body, &count, &faces), "our faces");
    Check(KernelRuntime.MemoryFree(faces), "free our faces");
    return count;
}

static unsafe int CountOurEdges(int body)
{
    int count;
    int* edges;
    Check(KernelRuntime.BodyAskEdges(body, &count, &edges), "our edges");
    Check(KernelRuntime.MemoryFree(edges), "free our edges");
    return count;
}

static unsafe int CountOurVertices(int body)
{
    int count;
    int* vertices;
    Check(KernelRuntime.BodyAskVertices(body, &count, &vertices), "our vertices");
    if (vertices != null)
        Check(KernelRuntime.MemoryFree(vertices), "free our vertices");
    return count;
}

static unsafe int CountOracleFaces(int body)
{
    int count;
    int* faces;
    Check(PK_BODY_ask_faces(body, &count, &faces), "oracle faces");
    if (faces != null)
        Check(PK_MEMORY_free(faces), "free oracle faces");
    return count;
}

static unsafe int CountOracleEdges(int body)
{
    int count;
    int* edges;
    Check(PK_BODY_ask_edges(body, &count, &edges), "oracle edges");
    if (edges != null)
        Check(PK_MEMORY_free(edges), "free oracle edges");
    return count;
}

static unsafe int CountOracleVertices(int body)
{
    int count;
    int* vertices;
    Check(PK_BODY_ask_vertices(body, &count, &vertices), "oracle vertices");
    if (vertices != null)
        Check(PK_MEMORY_free(vertices), "free oracle vertices");
    return count;
}

// ── Search helpers (our side) ────────────────────────────────────

static unsafe int FindOurCircleEdge(int body, double centreZ, ref M.PK_AXIS2_sf_s axis, ref double radius)
{
    int count;
    int* edges;
    Check(KernelRuntime.BodyAskEdges(body, &count, &edges), "our edges");
    try
    {
        for (var i = 0; i < count; i++)
        {
            int curve;
            Check(KernelRuntime.EdgeAskCurve(edges[i], &curve), "our edge curve");
            var record = KernelRuntime.GetCurveByTag(curve);
            if (record.Class != CurveClass.Circle) continue;
            var data = KernelRuntime.GetCircleData(record.DataIndex);
            if (Math.Abs(data.CenterZ - centreZ) > 1e-9) continue;
            FillAxis2M(ref axis, data.CenterX, data.CenterY, data.CenterZ, data.AxisX, data.AxisY, data.AxisZ, data.RefDirX, data.RefDirY, data.RefDirZ);
            radius = data.Radius;
            return edges[i];
        }
    }
    finally { Check(KernelRuntime.MemoryFree(edges), "free our edges"); }
    return 0;
}

static unsafe int FindOurPlaneFace(int body, double nx, double ny, double nz, double locationComponent)
{
    int count;
    int* faces;
    Check(KernelRuntime.BodyAskFaces(body, &count, &faces), "our faces");
    try
    {
        for (var i = 0; i < count; i++)
        {
            int surf;
            Check(KernelRuntime.FaceAskSurf(faces[i], &surf), "our face surf");
            var record = KernelRuntime.GetSurfaceByTag(surf);
            if (record.Class != SurfaceClass.Plane) continue;
            var data = KernelRuntime.GetPlaneData(record.DataIndex);
            if (Math.Abs(data.NormalX - nx) > 1e-9 || Math.Abs(data.NormalY - ny) > 1e-9 || Math.Abs(data.NormalZ - nz) > 1e-9)
                continue;
            var projected = data.LocationX * nx + data.LocationY * ny + data.LocationZ * nz;
            if (Math.Abs(projected - locationComponent) > 1e-9) continue;
            return faces[i];
        }
    }
    finally { Check(KernelRuntime.MemoryFree(faces), "free our faces"); }
    return 0;
}

static unsafe int FindOurLineEdgeAtZ(int body, double z)
{
    int count;
    int* edges;
    Check(KernelRuntime.BodyAskEdges(body, &count, &edges), "our edges");
    try
    {
        for (var i = 0; i < count; i++)
        {
            int curve;
            Check(KernelRuntime.EdgeAskCurve(edges[i], &curve), "our edge curve");
            var record = KernelRuntime.GetCurveByTag(curve);
            if (record.Class != CurveClass.Line) continue;
            var data = KernelRuntime.GetLineData(record.DataIndex);
            if (Math.Abs(data.LocationZ - z) < 1e-9)
                return edges[i];
        }
    }
    finally { Check(KernelRuntime.MemoryFree(edges), "free our edges"); }
    return 0;
}

static unsafe int FindOurBottomEdgeOnXPlus(int body)
{
    int count;
    int* edges;
    Check(KernelRuntime.BodyAskEdges(body, &count, &edges), "our edges");
    try
    {
        for (var i = 0; i < count; i++)
        {
            int curve;
            Check(KernelRuntime.EdgeAskCurve(edges[i], &curve), "our edge curve");
            var record = KernelRuntime.GetCurveByTag(curve);
            if (record.Class != CurveClass.Line) continue;
            var data = KernelRuntime.GetLineData(record.DataIndex);
            if (Math.Abs(data.LocationX - 1) < 1e-9 && Math.Abs(data.LocationZ) < 1e-9 && Math.Abs(data.AxisZ) < 1e-9)
                return edges[i];
        }
    }
    finally { Check(KernelRuntime.MemoryFree(edges), "free our edges"); }
    return 0;
}

static unsafe int FindOurCylinderFace(int body)
{
    int count;
    int* faces;
    Check(KernelRuntime.BodyAskFaces(body, &count, &faces), "our faces");
    try
    {
        for (var i = 0; i < count; i++)
        {
            int surf;
            Check(KernelRuntime.FaceAskSurf(faces[i], &surf), "our face surf");
            if (KernelRuntime.GetSurfaceByTag(surf).Class == SurfaceClass.Cylinder)
                return faces[i];
        }
    }
    finally { Check(KernelRuntime.MemoryFree(faces), "free our faces"); }
    return 0;
}

// ── Search helpers (oracle side) ─────────────────────────────────

static unsafe PK_EDGE_t FindOracleCircleEdge(PK_BODY_t body, double centreZ, ref PK_AXIS2_sf_t axis, ref double radius)
{
    int count;
    PK_EDGE_t* edges;
    Check(PK_BODY_ask_edges(body, &count, &edges), "oracle edges");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_CURVE_t curve;
            if (PK_EDGE_ask_curve(edges[i], &curve) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(curve, &cls) != 0) continue;
            if (cls != PK_CLASS_circle) continue;
            PK_CIRCLE_sf_t sf;
            Check(PK_CIRCLE_ask(curve, &sf), "oracle circle ask");
            if (Math.Abs(sf.basis_set.location.coord[2] - centreZ) > 1e-9) continue;
            axis = sf.basis_set;
            radius = sf.radius;
            return edges[i];
        }
    }
    finally { Check(PK_MEMORY_free(edges), "free oracle edges"); }
    return 0;
}

static unsafe PK_FACE_t FindOraclePlaneFace(PK_BODY_t body, double nx, double ny, double nz, double locationComponent)
{
    int count;
    PK_FACE_t* faces;
    Check(PK_BODY_ask_faces(body, &count, &faces), "oracle faces");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_SURF_t surf;
            if (PK_FACE_ask_surf(faces[i], &surf) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(surf, &cls) != 0) continue;
            if (cls != PK_CLASS_plane) continue;
            PK_PLANE_sf_t sf;
            Check(PK_PLANE_ask(surf, &sf), "oracle plane ask");
            if (Math.Abs(sf.basis_set.axis.coord[0] - nx) > 1e-9 || Math.Abs(sf.basis_set.axis.coord[1] - ny) > 1e-9
                || Math.Abs(sf.basis_set.axis.coord[2] - nz) > 1e-9)
                continue;
            var projected = sf.basis_set.location.coord[0] * nx + sf.basis_set.location.coord[1] * ny + sf.basis_set.location.coord[2] * nz;
            if (Math.Abs(projected - locationComponent) > 1e-9) continue;
            return faces[i];
        }
    }
    finally { Check(PK_MEMORY_free(faces), "free oracle faces"); }
    return 0;
}

static unsafe PK_EDGE_t FindOracleLineEdgeAtZ(PK_BODY_t body, double z)
{
    int count;
    PK_EDGE_t* edges;
    Check(PK_BODY_ask_edges(body, &count, &edges), "oracle edges");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_CURVE_t curve;
            if (PK_EDGE_ask_curve(edges[i], &curve) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(curve, &cls) != 0) continue;
            if (cls != PK_CLASS_line) continue;
            PK_LINE_sf_t sf;
            Check(PK_LINE_ask(curve, &sf), "oracle line ask");
            if (Math.Abs(sf.basis_set.location.coord[2] - z) < 1e-9)
                return edges[i];
        }
    }
    finally { Check(PK_MEMORY_free(edges), "free oracle edges"); }
    return 0;
}

static unsafe PK_EDGE_t FindOracleBottomEdgeOnXPlus(PK_BODY_t body)
{
    int count;
    PK_EDGE_t* edges;
    Check(PK_BODY_ask_edges(body, &count, &edges), "oracle edges");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_CURVE_t curve;
            if (PK_EDGE_ask_curve(edges[i], &curve) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(curve, &cls) != 0) continue;
            if (cls != PK_CLASS_line) continue;
            PK_LINE_sf_t sf;
            Check(PK_LINE_ask(curve, &sf), "oracle line ask");
            if (Math.Abs(sf.basis_set.location.coord[0] - 1) < 1e-9 && Math.Abs(sf.basis_set.location.coord[2]) < 1e-9
                && Math.Abs(sf.basis_set.axis.coord[2]) < 1e-9)
                return edges[i];
        }
    }
    finally { Check(PK_MEMORY_free(edges), "free oracle edges"); }
    return 0;
}

static unsafe PK_FACE_t FindOracleCylinderFace(PK_BODY_t body)
{
    int count;
    PK_FACE_t* faces;
    Check(PK_BODY_ask_faces(body, &count, &faces), "oracle faces");
    try
    {
        for (var i = 0; i < count; i++)
        {
            PK_SURF_t surf;
            if (PK_FACE_ask_surf(faces[i], &surf) != 0) continue;
            PK_CLASS_t cls;
            if (PK_ENTITY_ask_class(surf, &cls) != 0) continue;
            if (cls == PK_CLASS_cyl) return faces[i];
        }
    }
    finally { Check(PK_MEMORY_free(faces), "free oracle faces"); }
    return 0;
}

// ── Shared helpers ───────────────────────────────────────────────

static unsafe void FillAxis1M(ref M.PK_AXIS1_sf_s basis, double lx, double ly, double lz, double ax, double ay, double az)
{
    basis.location.coord[0] = lx; basis.location.coord[1] = ly; basis.location.coord[2] = lz;
    basis.axis.coord[0] = ax; basis.axis.coord[1] = ay; basis.axis.coord[2] = az;
}

static unsafe void FillAxis1N(ref PK_AXIS1_sf_t basis, double lx, double ly, double lz, double ax, double ay, double az)
{
    basis.location.coord[0] = lx; basis.location.coord[1] = ly; basis.location.coord[2] = lz;
    basis.axis.coord[0] = ax; basis.axis.coord[1] = ay; basis.axis.coord[2] = az;
}

static unsafe void FillAxis2M(ref M.PK_AXIS2_sf_s basis, double lx, double ly, double lz, double ax, double ay, double az, double rx, double ry, double rz)
{
    basis.location.coord[0] = lx; basis.location.coord[1] = ly; basis.location.coord[2] = lz;
    basis.axis.coord[0] = ax; basis.axis.coord[1] = ay; basis.axis.coord[2] = az;
    basis.ref_direction.coord[0] = rx; basis.ref_direction.coord[1] = ry; basis.ref_direction.coord[2] = rz;
}

static unsafe void FillAxis2N(ref PK_AXIS2_sf_t basis, double lx, double ly, double lz, double ax, double ay, double az, double rx, double ry, double rz)
{
    basis.location.coord[0] = lx; basis.location.coord[1] = ly; basis.location.coord[2] = lz;
    basis.axis.coord[0] = ax; basis.axis.coord[1] = ay; basis.axis.coord[2] = az;
    basis.ref_direction.coord[0] = rx; basis.ref_direction.coord[1] = ry; basis.ref_direction.coord[2] = rz;
}

static unsafe void CompareAxis1(M.PK_AXIS1_sf_s ours, PK_AXIS1_sf_t oracle, string label)
{
    CompareScalar(ours.location.coord[0], oracle.location.coord[0], label + " location x");
    CompareScalar(ours.location.coord[1], oracle.location.coord[1], label + " location y");
    CompareScalar(ours.location.coord[2], oracle.location.coord[2], label + " location z");
    CompareScalar(ours.axis.coord[0], oracle.axis.coord[0], label + " axis x");
    CompareScalar(ours.axis.coord[1], oracle.axis.coord[1], label + " axis y");
    CompareScalar(ours.axis.coord[2], oracle.axis.coord[2], label + " axis z");
}

static unsafe void CompareAxis2(M.PK_AXIS2_sf_s ours, PK_AXIS2_sf_t oracle, string label)
{
    CompareScalar(ours.location.coord[0], oracle.location.coord[0], label + " location x");
    CompareScalar(ours.location.coord[1], oracle.location.coord[1], label + " location y");
    CompareScalar(ours.location.coord[2], oracle.location.coord[2], label + " location z");
    CompareScalar(ours.axis.coord[0], oracle.axis.coord[0], label + " axis x");
    CompareScalar(ours.axis.coord[1], oracle.axis.coord[1], label + " axis y");
    CompareScalar(ours.axis.coord[2], oracle.axis.coord[2], label + " axis z");
    CompareScalar(ours.ref_direction.coord[0], oracle.ref_direction.coord[0], label + " ref x");
    CompareScalar(ours.ref_direction.coord[1], oracle.ref_direction.coord[1], label + " ref y");
    CompareScalar(ours.ref_direction.coord[2], oracle.ref_direction.coord[2], label + " ref z");
}

static void CompareScalar(double ours, double oracle, string label)
{
    if (Math.Abs(ours - oracle) > 1e-12)
        throw new InvalidOperationException($"{label}: ours={ours:R} oracle={oracle:R}");
}

static unsafe (double U, double V) PlaneUVM(PlaneData plane, double px, double py, double pz)
{
    var dx = px - plane.LocationX;
    var dy = py - plane.LocationY;
    var dz = pz - plane.LocationZ;
    var yx = plane.NormalY * plane.RefDirZ - plane.NormalZ * plane.RefDirY;
    var yy = plane.NormalZ * plane.RefDirX - plane.NormalX * plane.RefDirZ;
    var yz = plane.NormalX * plane.RefDirY - plane.NormalY * plane.RefDirX;
    return (dx * plane.RefDirX + dy * plane.RefDirY + dz * plane.RefDirZ,
            dx * yx + dy * yy + dz * yz);
}

static unsafe (double U, double V) PlaneUVN(PK_AXIS2_sf_t basis, PK_VECTOR_t point)
{
    var dx = point.coord[0] - basis.location.coord[0];
    var dy = point.coord[1] - basis.location.coord[1];
    var dz = point.coord[2] - basis.location.coord[2];
    var nx = basis.axis.coord[0]; var ny = basis.axis.coord[1]; var nz = basis.axis.coord[2];
    var rx = basis.ref_direction.coord[0]; var ry = basis.ref_direction.coord[1]; var rz = basis.ref_direction.coord[2];
    var yx = ny * rz - nz * ry;
    var yy = nz * rx - nx * rz;
    var yz = nx * ry - ny * rx;
    return (dx * rx + dy * ry + dz * rz, dx * yx + dy * yy + dz * yz);
}

static int CreateOurBSurface(double[] poles, double[]? uKnots = null, double[]? vKnots = null)
{
    uKnots ??= [0, 1];
    vKnots ??= [0, 1];
    int[] uMult = [4, 4];
    int[] vMult = [4, 4];
    return CreateOurBSurfaceImpl(poles, uKnots, uMult, vKnots, vMult);
}

static unsafe int CreateOurBSurfaceImpl(double[] poles, double[] uKnots, int[] uMult, double[] vKnots, int[] vMult)
{
    fixed (double* p = poles)
    fixed (double* uk = uKnots)
    fixed (int* um = uMult)
    fixed (double* vk = vKnots)
    fixed (int* vm = vMult)
    {
        var sf = new M.PK_BSURF_sf_s
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = 0, vertex = p,
            n_u_knots = 2, n_v_knots = 2, u_knot = uk, v_knot = vk,
            u_knot_mult = um, v_knot_mult = vm,
            form = M.ParasolidConstants.PK_BSURF_form_unset_c,
            u_knot_type = M.ParasolidConstants.PK_knot_unset_c,
            v_knot_type = M.ParasolidConstants.PK_knot_unset_c,
            self_intersecting = M.ParasolidConstants.PK_self_intersect_unset_c,
            convexity = M.ParasolidConstants.PK_convexity_unset_c,
        };
        int surface;
        Check(KernelRuntime.BSurfCreate(&sf, &surface), "our bsurf create");
        return surface;
    }
}

static unsafe PK_BSURF_t CreateOracleBSurface(double[] poles, double[]? uKnots = null, double[]? vKnots = null)
{
    uKnots ??= [0, 1];
    vKnots ??= [0, 1];
    int[] uMult = [4, 4];
    int[] vMult = [4, 4];
    fixed (double* p = poles)
    fixed (double* uk = uKnots)
    fixed (int* um = uMult)
    fixed (double* vk = vKnots)
    fixed (int* vm = vMult)
    {
        var sf = new PK_BSURF_sf_t
        {
            u_degree = 3, v_degree = 3, n_u_vertices = 4, n_v_vertices = 4,
            vertex_dim = 3, is_rational = 0, vertex = p,
            n_u_knots = 2, n_v_knots = 2, u_knot = uk, v_knot = vk,
            u_knot_mult = um, v_knot_mult = vm,
            form = PK_BSURF_form_unset_c,
            u_knot_type = PK_knot_unset_c,
            v_knot_type = PK_knot_unset_c,
            self_intersecting = PK_self_intersect_unset_c,
            convexity = PK_convexity_unset_c,
        };
        PK_BSURF_t surface;
        Check(PK_BSURF_create(&sf, &surface), "oracle bsurf create");
        return surface;
    }
}

static unsafe void CompareVectors(M.PK_VECTOR_s* actual, PK_VECTOR_t* expected, int count, string name)
{
    for (var i = 0; i < count; i++)
        for (var j = 0; j < 3; j++)
        {
            var a = actual[i].coord[j];
            var e = expected[i].coord[j];
            if (!double.IsFinite(a) || !double.IsFinite(e) || Math.Abs(a - e) > 1e-11)
                throw new InvalidOperationException($"{name} coordinate {j}: ours={a:R} received={e:R}");
        }
}

static void AssertTrue(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
}

static void Check(int error, string label)
{
    if (error != 0) throw new InvalidOperationException($"{label}: error={error}");
}
