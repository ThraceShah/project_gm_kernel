#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

// One-off probe: transmit Parasolid bodies carrying each dependent geometry
// class as construction geometry and dump the resulting XT text so the typed
// writer can mirror the exact persistent-field values Parasolid emits.
// Output: temp_docs/probe-writer-geometry/<case>.x_t

using System.Runtime.CompilerServices;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("XT geometry node probe", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        Directory.CreateDirectory(OutputDir());
        EllipseCase();
        TrimmedCase();
        SpCurveCase();
        BSurfaceCase();
        BSurfaceProbeCase();
        SweptCase();
        SpunCase();
        OffsetCase();
        IntervalCase();
        ErrorCase();
        Console.WriteLine("done");
    }
}

static string ScriptPath([CallerFilePath] string path = "") => path;

static string OutputDir() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "probe-writer-geometry"));

static unsafe void Dump(string name, PK_BODY_t body)
{
    var options = new PK_PART_transmit_o_t
    {
        o_t_version = 1,
        transmit_format = PK_transmit_format_text_c,
        transmit_version = 371
    };
    PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1, &body, &options, &block), name + " transmit");
    var text = System.Text.Encoding.UTF8.GetString(
        new ReadOnlySpan<byte>(block.bytes, (int)block.n_bytes));
    Check(PK_MEMORY_block_f(&block), name + " block free");
    File.WriteAllText(Path.Combine(OutputDir(), name + ".x_t"), text);
    Console.WriteLine($"=== {name} ===");
    foreach (var line in text.Split('\n'))
    {
        var trimmed = line.TrimEnd('\r');
        if (trimmed.Length > 0)
            Console.WriteLine(trimmed);
    }
}

static unsafe void AddAndDump(string name, PK_BODY_t body, PK_GEOM_t* geometry, int count)
{
    Check(PK_PART_add_geoms(body, count, geometry), name + " add geoms");
    Dump(name, body);
}

static unsafe PK_BODY_t Block()
{
    PK_BODY_t body;
    Check(PK_BODY_create_solid_block(1.0, 1.0, 1.0, null, &body), "block");
    return body;
}

static unsafe void EllipseCase()
{
    PK_ELLIPSE_sf_t sf = new(
        new PK_AXIS2_sf_t(new PK_VECTOR_t(1.0, 2.0, 3.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)),
        3.0, 2.0);
    PK_ELLIPSE_t ellipse;
    Check(PK_ELLIPSE_create(&sf, &ellipse), "ellipse create");
    var body = Block();
    var geometry = stackalloc PK_GEOM_t[1] { ellipse };
    AddAndDump("ellipse", body, geometry, 1);
}

static unsafe void TrimmedCase()
{
    PK_LINE_sf_t lineSf = new(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    Check(PK_LINE_create(&lineSf, &line), "line create");
    PK_CURVE_t basis = line;
    double start = -1.25, end = 1.75;
    PK_CURVE_t trimmed;
    int ifail;
    CRTRCU(&basis, &start, &end, &trimmed, &ifail);
    if (ifail != 0) throw new InvalidOperationException("CRTRCU ifail=" + ifail);
    PK_TRCURVE_sf_t trSf;
    Check(PK_TRCURVE_ask(trimmed, &trSf), "trcurve ask");
    Console.WriteLine($"trimmed interval: [{trSf.t_int.value[0]:R},{trSf.t_int.value[1]:R}]");
    var body = Block();
    var geometry = stackalloc PK_GEOM_t[2] { line, trimmed };
    AddAndDump("trimmed", body, geometry, 2);
}

static unsafe void SpCurveCase()
{
    Span<double> vertices = [0.0, 0.0, 1.0, 2.5];
    Span<double> knots = [0.0, 1.0];
    Span<int> mults = [2, 2];
    fixed (double* vertex = vertices)
    fixed (double* knot = knots)
    fixed (int* mult = mults)
    {
        var bSf = new PK_BCURVE_sf_t
        {
            degree = 1,
            n_vertices = 2,
            vertex_dim = 2,
            vertex = vertex,
            n_knots = 2,
            knot = knot,
            knot_mult = mult,
            is_rational = 0,
            is_periodic = 0,
            is_closed = 0,
            form = PK_BCURVE_form_arbitrary_c,
            knot_type = PK_knot_non_uniform_c,
            self_intersecting = PK_self_intersect_false_c
        };
        PK_BCURVE_t bcurve;
        Check(PK_BCURVE_create(&bSf, &bcurve), "2d bcurve create");
        PK_PLANE_sf_t planeSf = new(new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, 5.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
        PK_PLANE_t plane;
        Check(PK_PLANE_create(&planeSf, &plane), "plane create");
        PK_SPCURVE_sf_t spSf = new(plane, bcurve);
        PK_SPCURVE_t spcurve;
        Check(PK_SPCURVE_create(&spSf, &spcurve), "spcurve create");
        var body = Block();
        var geometry = stackalloc PK_GEOM_t[1] { spcurve };
        AddAndDump("spcurve", body, geometry, 1);
    }
}

static unsafe void BSurfaceCase()
{
    Span<double> vertices =
    [
        0.0, 0.0, 0.0,
        1.0, 0.0, 0.3,
        0.0, 1.0, 0.3,
        1.0, 1.0, 0.0,
    ];
    Span<double> uKnots = [0.0, 1.0];
    Span<double> vKnots = [0.0, 1.0];
    Span<int> uMults = [2, 2];
    Span<int> vMults = [2, 2];
    fixed (double* vertex = vertices)
    fixed (double* uKnot = uKnots)
    fixed (double* vKnot = vKnots)
    fixed (int* uMult = uMults)
    fixed (int* vMult = vMults)
    {
        var sf = new PK_BSURF_sf_t
        {
            u_degree = 1,
            v_degree = 1,
            n_u_vertices = 2,
            n_v_vertices = 2,
            vertex = vertex,
            vertex_dim = 3,
            is_rational = 0,
            n_u_knots = 2,
            n_v_knots = 2,
            u_knot = uKnot,
            v_knot = vKnot,
            u_knot_mult = uMult,
            v_knot_mult = vMult,
            u_knot_type = PK_knot_unset_c,
            v_knot_type = PK_knot_unset_c,
            is_u_periodic = 0,
            is_v_periodic = 0,
            is_u_closed = 0,
            is_v_closed = 0,
            self_intersecting = PK_self_intersect_unset_c,
            form = PK_BSURF_form_unset_c,
            convexity = PK_convexity_unset_c
        };
        PK_BSURF_t bsurf;
        Check(PK_BSURF_create(&sf, &bsurf), "bsurf create");
        var body = Block();
        var geometry = stackalloc PK_GEOM_t[1] { bsurf };
        AddAndDump("bsurf", body, geometry, 1);
    }
}

static unsafe void BSurfaceProbeCase()
{
    // u range [0,2], v range [-1,1], declared self-intersecting: pins the
    // original_uint/vint rounding and the XT self_int encoding.
    Span<double> vertices =
    [
        0.0, 0.0, 0.0,  1.0, 0.0, 0.3,  0.0, 1.0, 0.3,  1.0, 1.0, 0.0,
    ];
    Span<double> uKnots = [0.0, 2.0];
    Span<double> vKnots = [-1.0, 1.0];
    Span<int> mults = [2, 2];
    fixed (double* vertex = vertices)
    fixed (double* uKnot = uKnots)
    fixed (double* vKnot = vKnots)
    fixed (int* mult = mults)
    {
        var sf = new PK_BSURF_sf_t
        {
            u_degree = 1,
            v_degree = 1,
            n_u_vertices = 2,
            n_v_vertices = 2,
            vertex = vertex,
            vertex_dim = 3,
            is_rational = 0,
            n_u_knots = 2,
            n_v_knots = 2,
            u_knot = uKnot,
            v_knot = vKnot,
            u_knot_mult = mult,
            v_knot_mult = mult,
            u_knot_type = PK_knot_unset_c,
            v_knot_type = PK_knot_unset_c,
            is_u_periodic = 0,
            is_v_periodic = 0,
            is_u_closed = 0,
            is_v_closed = 0,
            self_intersecting = PK_self_intersect_true_c,
            form = PK_BSURF_form_unset_c,
            convexity = PK_convexity_unset_c
        };
        PK_BSURF_t bsurf;
        Check(PK_BSURF_create(&sf, &bsurf), "probe bsurf create");
        var body = Block();
        var geometry = stackalloc PK_GEOM_t[1] { bsurf };
        AddAndDump("bsurf-probe", body, geometry, 1);
    }
}

static unsafe void SweptCase()
{
    PK_LINE_sf_t lineSf = new(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(1.0, 2.0, 3.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    Check(PK_LINE_create(&lineSf, &line), "section line create");
    PK_SWEPT_sf_t sweptSf = new(line, new PK_VECTOR1_t(0.0, 0.0, 1.0));
    PK_SWEPT_t swept;
    Check(PK_SWEPT_create(&sweptSf, &swept), "swept create");
    var body = Block();
    var geometry = stackalloc PK_GEOM_t[2] { line, swept };
    AddAndDump("swept", body, geometry, 2);
}

static unsafe void SpunCase()
{
    PK_LINE_sf_t lineSf = new(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(2.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_LINE_t line;
    Check(PK_LINE_create(&lineSf, &line), "profile line create");
    PK_SPUN_sf_t spunSf = new(line, new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(0.0, 0.0, 1.0)));
    PK_SPUN_t spun;
    Check(PK_SPUN_create(&spunSf, &spun), "spun create");
    var body = Block();
    var geometry = stackalloc PK_GEOM_t[2] { line, spun };
    AddAndDump("spun", body, geometry, 2);
}

static unsafe void OffsetCase()
{
    var bsurf = BilinearBSurface();
    PK_OFFSET_sf_t offsetSf = new(bsurf, 0.5);
    PK_OFFSET_t offset;
    Check(PK_OFFSET_create(&offsetSf, &offset), "offset create");
    var body = Block();
    var geometry = stackalloc PK_GEOM_t[2] { bsurf, offset };
    AddAndDump("offset", body, geometry, 2);
}

static unsafe PK_BSURF_t BilinearBSurface()
{
    Span<double> vertices =
    [
        0.0, 0.0, 0.0,
        1.0, 0.0, 0.3,
        0.0, 1.0, 0.3,
        1.0, 1.0, 0.0,
    ];
    Span<double> knots = [0.0, 1.0];
    Span<int> mults = [2, 2];
    fixed (double* vertex = vertices)
    fixed (double* knot = knots)
    fixed (int* mult = mults)
    {
        var sf = new PK_BSURF_sf_t
        {
            u_degree = 1,
            v_degree = 1,
            n_u_vertices = 2,
            n_v_vertices = 2,
            vertex = vertex,
            vertex_dim = 3,
            is_rational = 0,
            n_u_knots = 2,
            n_v_knots = 2,
            u_knot = knot,
            v_knot = knot,
            u_knot_mult = mult,
            v_knot_mult = mult,
            u_knot_type = PK_knot_unset_c,
            v_knot_type = PK_knot_unset_c,
            is_u_periodic = 0,
            is_v_periodic = 0,
            is_u_closed = 0,
            is_v_closed = 0,
            self_intersecting = PK_self_intersect_unset_c,
            form = PK_BSURF_form_unset_c,
            convexity = PK_convexity_unset_c
        };
        PK_BSURF_t bsurf;
        Check(PK_BSURF_create(&sf, &bsurf), "offset base bsurf create");
        return bsurf;
    }
}

static unsafe void IntervalCase()
{
    PK_LINE_sf_t lineSf = new(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(1.0, 2.0, 3.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_LINE_t line;
    Check(PK_LINE_create(&lineSf, &line), "interval line create");
    PK_INTERVAL_t interval;
    Check(PK_CURVE_ask_interval(line, &interval), "line interval");
    Console.WriteLine($"line interval: [{interval.value[0]:R},{interval.value[1]:R}]");

    PK_CIRCLE_sf_t circleSf = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(1.0, 2.0, 3.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 2.0);
    PK_CIRCLE_t circle;
    Check(PK_CIRCLE_create(&circleSf, &circle), "interval circle create");
    Check(PK_CURVE_ask_interval(circle, &interval), "circle interval");
    Console.WriteLine($"circle interval: [{interval.value[0]:R},{interval.value[1]:R}]");

    PK_PLANE_sf_t planeSf = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(1.0, 2.0, 3.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_PLANE_t plane;
    Check(PK_PLANE_create(&planeSf, &plane), "interval plane create");
    PK_UVBOX_t uvbox;
    Check(PK_SURF_ask_uvbox(plane, &uvbox), "plane uvbox");
    Console.WriteLine($"plane uvbox: [{uvbox.param[0]:R},{uvbox.param[1]:R}] x [{uvbox.param[2]:R},{uvbox.param[3]:R}]");

    PK_TORUS_sf_t torusSf = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(1.0, 2.0, 3.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 5.0, 2.0);
    PK_TORUS_t torus;
    Check(PK_TORUS_create(&torusSf, &torus), "interval torus create");
    Check(PK_SURF_ask_uvbox(torus, &uvbox), "torus uvbox");
    Console.WriteLine($"torus uvbox: [{uvbox.param[0]:R},{uvbox.param[1]:R}] x [{uvbox.param[2]:R},{uvbox.param[3]:R}]");

    Check(PK_ENTITY_delete(1, &line), "delete line");
    Check(PK_ENTITY_delete(1, &circle), "delete circle");
    Check(PK_ENTITY_delete(1, &plane), "delete plane");
    Check(PK_ENTITY_delete(1, &torus), "delete torus");
}

static unsafe void ErrorCase()
{
    // Non-unit axis.
    PK_LINE_sf_t badLine = new(new PK_AXIS1_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0), new PK_VECTOR1_t(2.0, 0.0, 0.0)));
    PK_LINE_t tag;
    Console.WriteLine($"line non-unit axis: {PK_LINE_create(&badLine, &tag)}");

    PK_CIRCLE_sf_t badCircle = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 0.0);
    Console.WriteLine($"circle radius 0: {PK_CIRCLE_create(&badCircle, &tag)}");
    badCircle.radius = -1.0;
    Console.WriteLine($"circle radius -1: {PK_CIRCLE_create(&badCircle, &tag)}");

    PK_PLANE_sf_t badPlane = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 2.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
    PK_PLANE_t planeTag;
    Console.WriteLine($"plane non-unit normal: {PK_PLANE_create(&badPlane, &planeTag)}");
    badPlane.basis_set.axis.coord[2] = 1.0;
    badPlane.basis_set.ref_direction.coord[0] = 0.0;
    badPlane.basis_set.ref_direction.coord[2] = 1.0;
    Console.WriteLine($"plane parallel ref dir: {PK_PLANE_create(&badPlane, &planeTag)}");
    badPlane.basis_set.ref_direction.coord[2] = 0.0;
    badPlane.basis_set.ref_direction.coord[0] = 2.0;
    Console.WriteLine($"plane non-unit ref dir: {PK_PLANE_create(&badPlane, &planeTag)}");

    PK_CIRCLE_sf_t unitAxisCircle = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 3.0, 0.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 1.0);
    Console.WriteLine($"circle non-unit axis: {PK_CIRCLE_create(&unitAxisCircle, &tag)}");

    PK_CONE_sf_t badCone = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 1.0, 0.0);
    PK_CONE_t coneTag;
    Console.WriteLine($"cone semi-angle 0: {PK_CONE_create(&badCone, &coneTag)}");
    badCone.semi_angle = Math.PI;
    Console.WriteLine($"cone semi-angle pi: {PK_CONE_create(&badCone, &coneTag)}");
    badCone.semi_angle = Math.PI / 4;
    badCone.radius = 0.0;
    Console.WriteLine($"cone radius 0: {PK_CONE_create(&badCone, &coneTag)}");
    badCone.radius = -1.0;
    Console.WriteLine($"cone radius -1: {PK_CONE_create(&badCone, &coneTag)}");

    PK_SPHERE_sf_t badSphere = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 0.0);
    PK_SPHERE_t sphereTag;
    Console.WriteLine($"sphere radius 0: {PK_SPHERE_create(&badSphere, &sphereTag)}");

    PK_TORUS_sf_t badTorus = new(new PK_AXIS2_sf_t(
        new PK_VECTOR_t(0.0, 0.0, 0.0),
        new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)), 1.0, 2.0);
    PK_TORUS_t torusTag;
    Console.WriteLine($"torus minor>major: {PK_TORUS_create(&badTorus, &torusTag)}");
    badTorus.minor_radius = 0.0;
    Console.WriteLine($"torus minor 0: {PK_TORUS_create(&badTorus, &torusTag)}");
    badTorus.minor_radius = -1.0;
    Console.WriteLine($"torus minor -1: {PK_TORUS_create(&badTorus, &torusTag)}");
}

static void Check(int error, string label)
{
    if (error != 0) throw new InvalidOperationException($"{label}: error={error}");
}
