#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj

using ProjectGmKernel.Native.Runtime;
using System.Runtime.CompilerServices;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var w = Math.Sqrt(0.5);
var cases = new (string Name, int Degree, bool Rational, bool Periodic, double[] Poles, double[] Knots, int[] Multiplicities)[]
{
    ("linear", 1, false, false, [0,0,0, 2,1,3], [0,4], [2,2]),
    ("quadratic", 2, false, false, [0,0,0, 1,2,0, 3,1,2], [0,4], [3,3]),
    ("cubic", 3, false, false, [0,0,0, 1,2,0, 2,-1,1, 4,1,2], [-2,4], [4,4]),
    ("nonuniform", 3, false, false, [0,0,0, 1,2,0, 2,-1,1, 3,0,2, 4,2,0, 5,1,1], [0,2,5,8], [4,1,1,4]),
    ("unclamped", 2, false, false, [0,0,0, 1,2,0, 2,-1,1, 4,1,2], [-2,-1,0,2,4,5,6], [1,1,1,1,1,1,1]),
    ("repeated-knot", 2, false, false, [0,0,0, 1,0,0, 2,0,0, 2,1,0, 2,2,0], [0,2,4], [3,2,3]),
    ("rational-arc", 2, true, false, [1,0,0,1, w,w,0,w, 0,1,0,1], [0,4], [3,3]),
    ("rational-cubic", 3, true, false, [0,0,0,1, 1.1,2.2,0,1.1, 1.8,-0.9,0.9,0.9, 4,1,2,1], [0,8], [4,4]),
    ("periodic", 3, false, true, [1,0,0, 0,1,0, -1,0,0, 0,-1,0, 1,0,0, 0,1,0, -1,0,0], [-3,-2,-1,0,1,2,3,4,5,6,7], [1,1,1,1,1,1,1,1,1,1,1]),
    ("degree-ten", 10, false, false, [0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 1,1,1], [0,16], [11,11]),
};
unsafe
{
    if (!ParasolidScriptHost.TryStartSession("B-curve oracle", out var host, out var message)) throw new InvalidOperationException(message);
    using (host)
    {
        var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
        Check(KernelRuntime.SessionStart(&start), "our start");
        try
        {
            var ours = stackalloc M.PK_VECTOR_s[11];
            var reference = stackalloc PK_VECTOR_t[11];
            foreach (var c in cases)
                fixed (double* poles = c.Poles)
                fixed (double* knots = c.Knots)
                fixed (int* mult = c.Multiplicities)
                {
                    var dimension = c.Rational ? 4 : 3;
                    var sf = new PK_BCURVE_sf_t
                    {
                        degree = c.Degree,
                        n_vertices = c.Poles.Length / dimension,
                        vertex_dim = dimension,
                        vertex = poles,
                        n_knots = c.Knots.Length,
                        knot = knots,
                        knot_mult = mult,
                        is_rational = (byte)(c.Rational ? 1 : 0),
                        is_periodic = (byte)(c.Periodic ? 1 : 0),
                        is_closed = (byte)(c.Periodic ? 1 : 0),
                        form = PK_BCURVE_form_unset_c,
                        knot_type = PK_knot_unset_c,
                        self_intersecting = PK_self_intersect_unset_c
                    };
                    var msf = new M.PK_BCURVE_sf_s
                    {
                        degree = sf.degree,
                        n_vertices = sf.n_vertices,
                        vertex_dim = sf.vertex_dim,
                        vertex = poles,
                        n_knots = sf.n_knots,
                        knot = knots,
                        knot_mult = mult,
                        is_rational = sf.is_rational,
                        is_periodic = sf.is_periodic,
                        is_closed = sf.is_closed,
                        form = sf.form,
                        knot_type = sf.knot_type,
                        self_intersecting = sf.self_intersecting
                    };
                    int curve, master;
                    Check(PK_BCURVE_create(&sf, &master), c.Name + " oracle create");
                    Check(KernelRuntime.BCurveCreate(&msf, &curve), c.Name + " our create");
                    var record = KernelRuntime.GetCurveByTag(curve);
                    double max = 0;
                    for (var sample = -32; sample <= 288; sample++)
                    {
                        var t = record.TMin + (record.TMax - record.TMin) * sample / 256;
                        for (var order = 0; order <= 10; order++)
                        {
                            var expected = PK_CURVE_eval(master, t, order, reference);
                            var actual = KernelRuntime.CurveEval(curve, t, order, ours);
                            if (actual != expected) throw new InvalidOperationException($"{c.Name} t={t:R} order={order}: expected error={expected}, actual={actual}");
                            if (actual != 0) continue;
                            Compare(ours, reference, order + 1, c.Name, t, order, ref max);
                            M.PK_VECTOR_s tangent;
                            PK_VECTOR_t expectedTangent;
                            expected = PK_CURVE_eval_with_tangent(master, t, order, reference, &expectedTangent);
                            actual = KernelRuntime.CurveEvalWithTangent(curve, t, order, ours, &tangent);
                            if (actual != expected) throw new InvalidOperationException($"{c.Name} t={t:R} tangent errors expected={expected} actual={actual}");
                            if (actual == 0)
                            {
                                Compare(ours, reference, order + 1, c.Name, t, order, ref max);
                                Compare(&tangent, &expectedTangent, 1, c.Name + " tangent", t, order, ref max);
                            }
                        }
                    }
                    Console.WriteLine($"{c.Name}: passed 321 parameters, orders 0..10, max absolute difference={max:R}");
                }
            CheckBodyRoundtrip(false);
            CheckBodyRoundtrip(true);
        }
        finally { Check(KernelRuntime.SessionStop(), "our stop"); }
    }
}

static string ScriptPath([CallerFilePath] string path = "") => path;

static unsafe void CheckBodyRoundtrip(bool rational)
{
    int body, referenceBody;
    Check(KernelRuntime.BodyCreateSolidBlock(2, 3, 4, null, &body), "our block");
    Check(PK_BODY_create_solid_block(2, 3, 4, null, &referenceBody), "reference block");
    KernelRuntime.TryResolveBodySlot(body, out var bodySlot);
    var edgeSlot = KernelRuntime.GetBodyRecord(bodySlot).FirstEdgeBody;
    var oldCurve = KernelRuntime.GetEdgeRecord(edgeSlot).CurveTag;
    var oldRecord = KernelRuntime.GetCurveByTag(oldCurve);
    var line = KernelRuntime.GetLineData(oldRecord.DataIndex);
    var length = oldRecord.TMax - oldRecord.TMin;
    var dimension = rational ? 4 : 3;
    double* vertices = stackalloc double[16];
    for (var i = 0; i < 4; i++)
    {
        var weight = rational ? (i == 1 ? 1.1 : i == 2 ? 0.9 : 1) : 1;
        vertices[dimension * i] = weight * (line.LocationX + length * i / 3 * line.AxisX);
        vertices[dimension * i + 1] = weight * (line.LocationY + length * i / 3 * line.AxisY);
        vertices[dimension * i + 2] = weight * (line.LocationZ + length * i / 3 * line.AxisZ);
        if (rational) vertices[dimension * i + 3] = weight;
    }
    double* knots = stackalloc double[2] { 0, length };
    int* mults = stackalloc int[2] { 4, 4 };
    var sf = new PK_BCURVE_sf_t
    {
        degree = 3,
        n_vertices = 4,
        vertex_dim = dimension,
        is_rational = (byte)(rational ? 1 : 0),
        vertex = vertices,
        n_knots = 2,
        knot = knots,
        knot_mult = mults,
        form = PK_BCURVE_form_unset_c,
        knot_type = PK_knot_unset_c,
        self_intersecting = PK_self_intersect_unset_c
    };
    var msf = new M.PK_BCURVE_sf_s
    {
        degree = sf.degree,
        n_vertices = sf.n_vertices,
        vertex_dim = sf.vertex_dim,
        is_rational = sf.is_rational,
        vertex = vertices,
        n_knots = 2,
        knot = knots,
        knot_mult = mults,
        form = sf.form,
        knot_type = sf.knot_type,
        self_intersecting = sf.self_intersecting
    };
    int curve, referenceCurve;
    Check(KernelRuntime.BCurveCreate(&msf, &curve), "attached B-curve create");
    Check(PK_BCURVE_create(&sf, &referenceCurve), "reference attached B-curve create");
    // Test setup: attach to the existing block edge without introducing another public modeling API.
    ref var record = ref KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(curve)];
    record.OwnerEdge = edgeSlot;
    record.PrevInBody = oldRecord.PrevInBody;
    record.NextInBody = oldRecord.NextInBody;
    KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.PrevInBody)].NextInBody = curve;
    KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(record.NextInBody)].PrevInBody = curve;
    KernelRuntime.Edges[edgeSlot].CurveTag = curve;
    ref var detached = ref KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(oldCurve)];
    detached.OwnerEdge = -1;
    detached.PrevInBody = detached.NextInBody = 0;

    int count;
    int* edges;
    Check(PK_BODY_ask_edges(referenceBody, &count, &edges), "reference edges");
    var found = false;
    try
    {
        for (var i = 0; i < count; i++)
        {
            int edgeCurve;
            Check(PK_EDGE_ask_curve(edges[i], &edgeCurve), "reference edge curve");
            var ls = new PK_LINE_sf_t();
            Check(PK_LINE_ask(edgeCurve, &ls), "reference line");
            var dx = ls.basis_set.location.coord[0] - line.LocationX;
            var dy = ls.basis_set.location.coord[1] - line.LocationY;
            var dz = ls.basis_set.location.coord[2] - line.LocationZ;
            var projection = dx * line.AxisX + dy * line.AxisY + dz * line.AxisZ;
            var dot = ls.basis_set.axis.coord[0] * line.AxisX + ls.basis_set.axis.coord[1] * line.AxisY + ls.basis_set.axis.coord[2] * line.AxisZ;
            if (Math.Abs(Math.Abs(dot) - 1) > 1e-12 || Math.Abs(dx - projection * line.AxisX) + Math.Abs(dy - projection * line.AxisY) + Math.Abs(dz - projection * line.AxisZ) > 1e-12) continue;
            var edge = edges[i];
            Check(PK_TOPOL_detach_geom(edge), "reference detach line");
            Check(PK_EDGE_attach_curves(1, &edge, &referenceCurve), "reference attach B-curve");
            found = true;
            break;
        }
    }
    finally { Check(PK_MEMORY_free(edges), "free reference edges"); }
    if (!found) throw new InvalidOperationException("Reference block edge not found.");

    var outputDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "bcurve-oracle"));
    Directory.CreateDirectory(outputDir);
    var path = Path.Combine(outputDir, rational ? "rational-bcurve-block.x_t" : "bcurve-block.x_t");
    var options = new M.PK_PART_transmit_o_s
    {
        o_t_version = 4,
        transmit_format = PK_transmit_format_text_c,
        transmit_meshes = PK_transmit_meshes_separate_c
    };
    var block = new M.PK_MEMORY_block_s();
    Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "B-curve transmit");
    try
    {
        using var file = File.Create(path);
        for (var b = &block; b != null; b = b->next) file.Write(new ReadOnlySpan<byte>(b->bytes, (int)b->n_bytes));
    }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free transmit"); }
    var bytes = File.ReadAllBytes(path);
    fixed (byte* pointer = bytes)
    {
        var input = new PK_MEMORY_block_t(null, (ulong)bytes.Length, pointer);
        var receive = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int* parts;
        Check(PK_PART_receive_b(input, &receive, &count, &parts), "B-curve receive");
        try
        {
            if (count != 1) throw new InvalidOperationException("B-curve roundtrip part count.");
            var compare = new PK_DEBUG_BODY_compare_o_t { max_diffs = 64, all_tests = 0, acc_dev_tests = 0, non_match_tests = 0 };
            var result = new PK_DEBUG_BODY_compare_r_t();
            Check(PK_DEBUG_BODY_compare(referenceBody, parts[0], &compare, &result), "B-curve body compare");
            try
            {
                if (result.global_result != PK_DEBUG_global_res_no_diffs_c || result.local_result != PK_DEBUG_local_res_no_diffs_c)
                {
                    for (var i = 0; i < result.n_global_diffs; i++) Console.WriteLine($"diff={result.global_diffs[i].diff}");
                    for (var i = 0; i < result.n_face_pairs; i++)
                    {
                        var pair = result.face_pairs[i];
                        for (var j = 0; j < pair.n_local_diffs; j++) Console.WriteLine($"face={pair.master_face}/{pair.similar_face} diff={pair.local_diffs[j].diff}");
                    }
                    throw new InvalidOperationException($"global={result.global_result} local={result.local_result}");
                }
            }
            finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free compare"); }
        }
        finally { Check(PK_MEMORY_free(parts), "free received parts"); }
    }
    Console.WriteLine($"B-curve attached body (rational={rational}): XT receive and PK_DEBUG_BODY_compare passed");
}

static void Check(int error, string label)
{
    if (error != 0) throw new InvalidOperationException($"{label}: error={error}");
}

static unsafe void Compare(M.PK_VECTOR_s* actual, PK_VECTOR_t* expected, int count, string name, double t, int order, ref double max)
{
    for (var i = 0; i < count; i++) for (var j = 0; j < 3; j++)
    {
        var a = actual[i].coord[j]; var e = expected[i].coord[j];
        var difference = Math.Abs(a - e); max = Math.Max(max, difference);
        if (!double.IsFinite(a) || !double.IsFinite(e) || difference > 1e-13)
            throw new InvalidOperationException($"{name} t={t:R} order={order} derivative={i} coordinate={j}: ours={a:R} reference={e:R} difference={difference:R}");
    }
}
