#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

// Real Parasolid only: compare equivalent B-surface representations and genuine
// geometry changes. See docs/debug-body-compare-probe.md for the option matrix.
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("Body compare probe", out var session, out var message))
        throw new InvalidOperationException(message);
    using (session)
    {
        PK_SESSION_kernel_version_t version;
        Check(PK_SESSION_ask_kernel_version(&version), "kernel version");
        double precision;
        Check(PK_SESSION_ask_precision(&precision), "precision");
        Console.WriteLine($"Parasolid {version.major_revision}.{version.minor_revision} build={version.build_number} precision={precision:R}");

        var curved = CreateSurface(false);
        var curvedBody = MakeBody(curved);
        var planar = CreateSurface(true);
        var planarBody = MakeBody(planar);
        Describe("curved-reference", curved, curvedBody);
        Describe("planar-reference", planar, planarBody);

        foreach (var name in new[] { "identical", "raise-degree", "insert-knots", "reverse-both", "affine-uv", "move-interior-pole", "move-interior-pole-tiny", "translate" })
        {
            var candidate = CreateSurface(false, name == "move-interior-pole" ? 0.01 : name == "move-interior-pole-tiny" ? 1e-10 : 0,
                name == "translate" ? 0.01 : 0);
            if (name == "raise-degree")
            {
                var options = new PK_BSURF_raise_degree_o_t();
                Check(PK_BSURF_raise_degree(candidate, PK_PARAM_direction_u_c, 1, &options), "raise u degree");
                Check(PK_BSURF_raise_degree(candidate, PK_PARAM_direction_v_c, 1, &options), "raise v degree");
            }
            if (name == "insert-knots")
            {
                Check(PK_BSURF_add_u_knot(candidate, 0.37), "insert u knot");
                Check(PK_BSURF_add_v_knot(candidate, 0.61), "insert v knot");
            }
            if (name is "reverse-both" or "affine-uv")
            {
                var options = new PK_BSURF_reparameterise_o_t();
                if (name == "reverse-both")
                    options.reverse_u = options.reverse_v = PK_LOGICAL_true;
                else
                {
                    options.have_u_interval = options.have_v_interval = PK_LOGICAL_true;
                    options.u_interval = new(2, 5);
                    options.v_interval = new(-3, 4);
                }
                Check(PK_BSURF_reparameterise(candidate, &options), "reparameterise");
            }
            var body = MakeBody(candidate);
            Describe(name, candidate, body);
            Sample(curved, candidate, name);
            CompareModes(name, curvedBody, body);
        }

        var nonlinear = CreateSurface(true, planarMiddleX: 0.2);
        var nonlinearBody = MakeBody(nonlinear);
        Describe("planar-nonlinear-uv", nonlinear, nonlinearBody);
        Sample(planar, nonlinear, "planar-nonlinear-uv");
        Sample(planar, nonlinear, "planar-nonlinear-same-uv");
        CompareModes("planar-nonlinear-uv", planarBody, nonlinearBody);

        var rational = CreateSurface(false, homogeneousScale: 1);
        var scaled = CreateSurface(false, homogeneousScale: 2);
        var rationalBody = MakeBody(rational);
        var scaledBody = MakeBody(scaled);
        Describe("rational-reference", rational, rationalBody);
        Describe("rational-scaled-homogeneous-poles", scaled, scaledBody);
        Sample(rational, scaled, "rational-scaled-homogeneous-poles");
        CompareModes("rational-scaled-homogeneous-poles", rationalBody, scaledBody);

        PK_BODY_t blockA, blockB;
        Check(PK_BODY_create_solid_block(1, 1, 1, null, &blockA), "block a");
        Check(PK_BODY_create_solid_block(1, 1, 1.01, null, &blockB), "block b");
        CompareModes("different-block-height", blockA, blockB);
        CompareModes("sheet-versus-solid", curvedBody, blockA);
    }
}

static unsafe PK_BSURF_t CreateSurface(bool planar, double poleDelta = 0, double translation = 0, double planarMiddleX = 0.5, double homogeneousScale = 0)
{
    var dimension = homogeneousScale == 0 ? 3 : 4;
    var scale = homogeneousScale == 0 ? 1 : homogeneousScale;
    double* poles = stackalloc double[36];
    for (var u = 0; u < 3; u++)
        for (var v = 0; v < 3; v++)
        {
            var offset = (u * 3 + v) * dimension;
            poles[offset] = scale * (planar && u == 1 ? planarMiddleX : u * 0.5);
            poles[offset + 1] = scale * v * 0.5;
            poles[offset + 2] = scale * (translation + (u == 1 && v == 1 ? (planar ? 0 : 0.8) + poleDelta : 0));
            if (dimension == 4) poles[offset + 3] = scale;
        }
    double* knots = stackalloc double[] { 0, 1 };
    int* mult = stackalloc int[] { 3, 3 };
    var definition = new PK_BSURF_sf_t
    {
        u_degree = 2, v_degree = 2, n_u_vertices = 3, n_v_vertices = 3,
        vertex_dim = dimension, is_rational = dimension == 4 ? PK_LOGICAL_true : PK_LOGICAL_false,
        vertex = poles, form = PK_BSURF_form_arbitrary_c,
        n_u_knots = 2, n_v_knots = 2, u_knot = knots, v_knot = knots,
        u_knot_mult = mult, v_knot_mult = mult,
        u_knot_type = PK_knot_non_uniform_c, v_knot_type = PK_knot_non_uniform_c,
        self_intersecting = PK_self_intersect_false_c, convexity = PK_convexity_arbitrary_c,
    };
    PK_BSURF_t surface;
    Check(PK_BSURF_create(&definition, &surface), "create surface");
    return surface;
}

static unsafe PK_BODY_t MakeBody(PK_SURF_t surface)
{
    PK_BODY_t body;
    PK_UVBOX_t box;
    Check(PK_SURF_ask_uvbox(surface, &box), "surface domain");
    Check(PK_SURF_make_sheet_body(surface, box, &body), "make sheet");
    var options = new PK_BODY_check_o_t();
    int count;
    PK_check_fault_t* faults;
    Check(PK_BODY_check(body, &options, &count, &faults), "check body");
    if (faults != null) Check(PK_MEMORY_free(faults), "free faults");
    if (count != 0) throw new InvalidOperationException($"Body {body} has {count} faults");
    return body;
}

static unsafe void Describe(string name, PK_BSURF_t surface, PK_BODY_t body)
{
    PK_BSURF_sf_t sf;
    Check(PK_BSURF_ask(surface, &sf), "ask surface");
    try
    {
        int count;
        PK_FACE_t* faces;
        Check(PK_BODY_ask_faces(body, &count, &faces), "ask faces");
        try
        {
            PK_SURF_t attached;
            PK_CLASS_t kind;
            Check(PK_FACE_ask_surf(faces[0], &attached), "face surface");
            Check(PK_ENTITY_ask_class(attached, &kind), "surface class");
            Console.WriteLine($"SURFACE {name}: degree={sf.u_degree}x{sf.v_degree} poles={sf.n_u_vertices}x{sf.n_v_vertices} vertex_dim={sf.vertex_dim} knots={sf.n_u_knots}x{sf.n_v_knots} faces={count} attached_class={kind} bsurf_class={PK_CLASS_bsurf}");
        }
        finally { Check(PK_MEMORY_free(faces), "free faces"); }
        Console.Write("  poles=");
        for (var i = 0; i < sf.n_u_vertices * sf.n_v_vertices * sf.vertex_dim; i++)
            Console.Write($"{(i == 0 ? "" : ",")}{sf.vertex[i]:R}");
        Console.WriteLine();
    }
    finally
    {
        Check(PK_MEMORY_free(sf.vertex), "free poles");
        Check(PK_MEMORY_free(sf.u_knot), "free u knots");
        Check(PK_MEMORY_free(sf.v_knot), "free v knots");
        Check(PK_MEMORY_free(sf.u_knot_mult), "free u multiplicities");
        Check(PK_MEMORY_free(sf.v_knot_mult), "free v multiplicities");
    }
}

static unsafe void Sample(PK_SURF_t master, PK_SURF_t similar, string name)
{
    var max = 0.0;
    for (var i = 0; i <= 20; i++)
        for (var j = 0; j <= 20; j++)
        {
            var u = i / 20.0;
            var v = j / 20.0;
            var mapped = name switch
            {
                "reverse-both" => new PK_UV_t(1 - u, 1 - v),
                "affine-uv" => new PK_UV_t(2 + 3 * u, -3 + 7 * v),
                // x(t) = 0.4t + 0.6t^2, y(v) = v: identical planar image.
                "planar-nonlinear-uv" => new PK_UV_t(2 * u / (0.4 + Math.Sqrt(0.16 + 2.4 * u)), v),
                _ => new PK_UV_t(u, v),
            };
            PK_VECTOR_t a, b;
            Check(PK_SURF_eval(master, new(u, v), 0, 0, PK_LOGICAL_false, &a), "eval master");
            Check(PK_SURF_eval(similar, mapped, 0, 0, PK_LOGICAL_false, &b), "eval similar");
            var squared = 0.0;
            for (var k = 0; k < 3; k++) squared += (a.coord[k] - b.coord[k]) * (a.coord[k] - b.coord[k]);
            max = Math.Max(max, Math.Sqrt(squared));
        }
    Console.WriteLine($"SAMPLE {name}: mapped 21x21 max_distance={max:R}");
}

static unsafe void CompareModes(string name, PK_BODY_t master, PK_BODY_t similar)
{
    foreach (var mode in new[] { "default", "full", "accurate", "tolerant", "tolerant-only", "tolerant-all", "structure" })
    {
        var options = new PK_DEBUG_BODY_compare_o_t();
        if (mode != "default") options.max_diffs = 64;
        if (mode != "default" && mode != "full") options.all_tests = PK_LOGICAL_false;
        if (mode is "accurate" or "tolerant" or "tolerant-only")
        {
            options.num_tests = options.tol_tests = options.class_tests = options.sense_tests = options.missing_tests = PK_LOGICAL_false;
            if (mode != "accurate") options.tol_dev_tests = PK_LOGICAL_true;
            if (mode == "tolerant-only") options.acc_dev_tests = PK_LOGICAL_false;
        }
        if (mode == "tolerant-all")
        {
            options.acc_dev_tests = PK_LOGICAL_false;
            options.tol_dev_tests = PK_LOGICAL_true;
        }
        if (mode == "structure") options.acc_dev_tests = options.non_match_tests = PK_LOGICAL_false;
        var results = new PK_DEBUG_BODY_compare_r_t();
        Check(PK_DEBUG_BODY_compare(master, similar, &options, &results), "compare");
        try
        {
            Console.WriteLine($"COMPARE {name} {mode}: global={results.global_result} local={results.local_result} global_diffs={results.n_global_diffs} face_pairs={results.n_face_pairs}");
            for (var i = 0; i < results.n_global_diffs; i++)
            {
                var diff = results.global_diffs[i];
                if (diff.n_masters != diff.n_similars)
                    Console.WriteLine($"  GLOBAL {DiffName(diff.diff)} master={diff.n_masters} similar={diff.n_similars}");
            }
            for (var i = 0; i < results.n_face_pairs; i++)
            {
                var pair = results.face_pairs[i];
                for (var j = 0; j < pair.n_local_diffs; j++)
                {
                    var diff = pair.local_diffs[j];
                    Console.WriteLine($"  LOCAL faces={pair.master_face}/{pair.similar_face} {DiffName(diff.diff)} entities={diff.master_entity}/{diff.similar_entity} ints={diff.master_int}/{diff.similar_int} doubles={diff.master_double:R}/{diff.similar_double:R} classes={diff.master_class}/{diff.similar_class} logicals={diff.master_logical == PK_LOGICAL_true}/{diff.similar_logical == PK_LOGICAL_true}");
                }
            }
        }
        finally { Check(PK_DEBUG_BODY_compare_r_f(&results), "free compare"); }
    }
}

static string DiffName(PK_DEBUG_diff_t diff) => diff switch
{
    PK_DEBUG_diff_n_shells_c => "n_shells", PK_DEBUG_diff_n_faces_c => "n_faces",
    PK_DEBUG_diff_n_acc_edges_c => "n_acc_edges", PK_DEBUG_diff_n_tol_edges_c => "n_tol_edges",
    PK_DEBUG_diff_n_acc_vxs_c => "n_acc_vxs", PK_DEBUG_diff_n_tol_vxs_c => "n_tol_vxs",
    PK_DEBUG_diff_surf_dev_c => "surf_dev", PK_DEBUG_diff_curve_dev_c => "curve_dev",
    PK_DEBUG_diff_vx_dev_c => "vx_dev", PK_DEBUG_diff_surf_class_c => "surf_class",
    PK_DEBUG_diff_curve_class_c => "curve_class", PK_DEBUG_diff_face_sense_c => "face_sense",
    PK_DEBUG_diff_surf_sense_c => "surf_sense", PK_DEBUG_diff_curve_sense_c => "curve_sense",
    PK_DEBUG_diff_face_match_c => "face_match", PK_DEBUG_diff_fin_match_c => "fin_match",
    PK_DEBUG_diff_vx_match_c => "vx_match", _ => diff.ToString(),
};

static void Check(PK_ERROR_code_t error, string label) => ParasolidScriptHost.Check(error, label);
