#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using System.Runtime.CompilerServices;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("Body comparison regression", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&start), "managed start");
        try
        {
            PK_BODY_t reference, managed;
            Check(PK_BODY_create_solid_block(1, 1, 1, null, &reference), "reference block");
            Check(KernelRuntime.BodyCreateSolidBlock(1, 1, 1, null, &managed), "managed block");
            ParasolidBodyCompare.AssertEquivalent(reference, Receive(managed, "equal-block"), "equal block");
            Check(KernelRuntime.BodyCreateSolidBlock(1, 1, 1.01, null, &managed), "wrong-height block");
            Reject(reference, Receive(managed, "wrong-height"), "wrong-height block", "body geometry");

            var line = new PK_LINE_sf_t(new(new(0, 0, 0), new(1, 0, 0)));
            PK_CURVE_t curve;
            Check(PK_LINE_create(&line, &curve), "reference line");
            Check(PK_CURVE_make_wire_body(curve, new(0, 1), &reference), "reference wire");
            ParasolidBodyCompare.AssertEquivalent(reference, Receive(ManagedWire(line, 0, 1), "equal-wire"), "equal wire");
            line.basis_set.location.coord[1] = 1;
            Reject(reference, Receive(ManagedWire(line, 0, 1), "translated-wire"), "translated wire", "unmatched wire edge");
            line.basis_set.location.coord[1] = 0;
            Reject(reference, Receive(ManagedWire(line, 0, 2), "wrong-trim-wire"), "wrong trim wire", "unmatched wire edge");
            reference = BCurveWire(0.1, false);
            Reject(reference, Receive(BCurveWire(0.2, true), "wrong-interior-wire"), "wrong interior wire", "unmatched wire edge");

            var circle = new PK_CIRCLE_sf_t
            {
                radius = 1, basis_set = new(new(0, 0, 0), new(0, 0, 1), new(1, 0, 0)),
            };
            Check(PK_CIRCLE_create(&circle, &curve), "reference circle");
            Check(PK_CURVE_make_wire_body(curve, new(0, Math.Tau), &reference), "closed reference wire");
            circle.basis_set.ref_direction = new(0, 1, 0);
            Check(KernelRuntime.CircleCreate((M.PK_CIRCLE_sf_s*)&circle, &curve), "managed phase-shifted circle");
            var fullRange = new M.PK_INTERVAL_s(); fullRange.value[1] = Math.Tau;
            Check(KernelRuntime.CurveMakeWireBody(curve, fullRange, &managed), "closed managed wire");
            ParasolidBodyCompare.AssertEquivalent(reference, Receive(managed, "closed-wire-phase"), "closed wire phase");

            var sheet = Sheet(0, false, false);
            var received = Receive(Sheet(0, true, false), "equal-sheet");
            ParasolidBodyCompare.AssertEquivalent(sheet, received, "equal sheet");
            ParasolidBodyCompare.AssertEquivalent(Sheet(0, false, true), received, "degree-elevated sheet");
            ParasolidBodyCompare.AssertEquivalent(Sheet(0, false, false, true), received, "knot-inserted sheet");
            Reject(sheet, Receive(Sheet(0.01, true, false), "wrong-pole-sheet"), "wrong pole sheet", "body geometry");
            Console.WriteLine("PASS body comparison regression: 6 positive and 5 negative XT controls");
        }
        finally { Check(KernelRuntime.SessionStop(), "managed stop"); }
    }
}

static unsafe PK_BODY_t ManagedWire(PK_LINE_sf_t line, double low, double high)
{
    PK_CURVE_t curve;
    PK_BODY_t body;
    Check(KernelRuntime.LineCreate((M.PK_LINE_sf_s*)&line, &curve), "managed line");
    var range = new M.PK_INTERVAL_s(); range.value[0] = low; range.value[1] = high;
    Check(KernelRuntime.CurveMakeWireBody(curve, range, &body), "managed wire");
    return body;
}

static unsafe PK_BODY_t BCurveWire(double bulge, bool managed)
{
    double* poles = stackalloc double[] { 0, 0, 0, 0.5, bulge, 0, 1, 0, 0 };
    double* knots = stackalloc double[] { 0, 1 };
    int* mult = stackalloc int[] { 3, 3 };
    var sf = new PK_BCURVE_sf_t
    {
        degree = 2, n_vertices = 3, vertex_dim = 3, vertex = poles,
        form = PK_BCURVE_form_arbitrary_c, n_knots = 2,
        knot = knots, knot_mult = mult, knot_type = PK_knot_non_uniform_c,
        self_intersecting = PK_self_intersect_false_c,
    };
    PK_CURVE_t curve;
    PK_BODY_t body;
    Check(managed ? KernelRuntime.BCurveCreate((M.PK_BCURVE_sf_s*)&sf, &curve) : PK_BCURVE_create(&sf, &curve), "B-curve");
    var range = new PK_INTERVAL_t(0, 1);
    Check(managed ? KernelRuntime.CurveMakeWireBody(curve, *(M.PK_INTERVAL_s*)&range, &body) : PK_CURVE_make_wire_body(curve, range, &body), "B-curve wire");
    return body;
}

static unsafe PK_BODY_t Sheet(double poleDelta, bool managed, bool raiseDegree, bool insertKnots = false)
{
    double* poles = stackalloc double[27];
    for (var u = 0; u < 3; u++) for (var v = 0; v < 3; v++)
    {
        var at = (u * 3 + v) * 3;
        poles[at] = u * 0.5; poles[at + 1] = v * 0.5;
        poles[at + 2] = u == 1 && v == 1 ? 0.8 + poleDelta : 0;
    }
    double* knots = stackalloc double[] { 0, 1 };
    int* mult = stackalloc int[] { 3, 3 };
    var sf = new PK_BSURF_sf_t
    {
        u_degree = 2, v_degree = 2, n_u_vertices = 3, n_v_vertices = 3,
        vertex_dim = 3, vertex = poles, form = PK_BSURF_form_arbitrary_c,
        n_u_knots = 2, n_v_knots = 2, u_knot = knots, v_knot = knots,
        u_knot_mult = mult, v_knot_mult = mult,
        u_knot_type = PK_knot_non_uniform_c, v_knot_type = PK_knot_non_uniform_c,
        self_intersecting = PK_self_intersect_false_c, convexity = PK_convexity_arbitrary_c,
    };
    PK_SURF_t surface;
    Check(managed ? KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf, &surface) : PK_BSURF_create(&sf, &surface), "B-surface");
    if (raiseDegree)
    {
        var options = new PK_BSURF_raise_degree_o_t();
        Check(PK_BSURF_raise_degree(surface, PK_PARAM_direction_u_c, 1, &options), "elevate surface");
    }
    if (insertKnots)
    {
        Check(PK_BSURF_add_u_knot(surface, 0.37), "insert u knot");
        Check(PK_BSURF_add_v_knot(surface, 0.61), "insert v knot");
    }
    PK_BODY_t body;
    var box = new PK_UVBOX_t(0, 0, 1, 1);
    Check(managed ? KernelRuntime.SurfMakeSheetBody(surface, *(M.PK_UVBOX_s*)&box, &body) : PK_SURF_make_sheet_body(surface, box, &body), "sheet");
    return body;
}

static unsafe PK_BODY_t Receive(PK_BODY_t managed, string name)
{
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4, transmit_format = PK_transmit_format_text_c,
        transmit_version = 371, transmit_meshes = PK_transmit_meshes_separate_c,
    };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &managed, &options, &block), "managed transmit");
    var directory = Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "body-compare-regression");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, name + ".x_t");
    try
    {
        using var file = File.Create(path);
        for (var b = &block; b != null; b = b->next) file.Write(new ReadOnlySpan<byte>(b->bytes, (int)b->n_bytes));
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free transmit"); }
    var bytes = File.ReadAllBytes(path);
    fixed (byte* p = bytes)
    {
        var input = new PK_MEMORY_block_t(null, (ulong)bytes.Length, p);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int count;
        PK_PART_t* parts;
        Check(PK_PART_receive_b(input, &receive, &count, &parts), "real PK receive");
        try
        {
            if (count != 1) throw new InvalidOperationException("Expected one received part");
            return parts[0];
        }
        finally { Check(PK_MEMORY_free(parts), "free parts"); }
    }
}

static void Reject(PK_BODY_t reference, PK_BODY_t actual, string label, string expectedReason)
{
    try { ParasolidBodyCompare.AssertEquivalent(reference, actual, label); }
    catch (InvalidOperationException exception) when (exception.Message.Contains(expectedReason, StringComparison.Ordinal))
    { Console.WriteLine("PASS rejected " + label); return; }
    throw new InvalidOperationException(label + " incorrectly passed");
}

static string ScriptPath([CallerFilePath] string path = "") => path;
static void Check(PK_ERROR_code_t error, string label) => ParasolidScriptHost.Check(error, label);
