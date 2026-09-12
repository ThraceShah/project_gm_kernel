#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

// Reference probe: Parasolid's own XT for a block whose top edge carries an
// SP curve (support = standalone plane, p-curve = 2D B-curve).

using System.Runtime.CompilerServices;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("spcurve reference probe", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        PK_BODY_t body;
        Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
        int count;
        PK_EDGE_t* edges;
        Check(PK_BODY_ask_edges(body, &count, &edges), "edges");
        PK_EDGE_t edge = 0;
        for (var i = 0; i < count; i++)
        {
            PK_CURVE_t curve;
            if (PK_EDGE_ask_curve(edges[i], &curve) != 0) continue;
            PK_LINE_sf_t sf;
            if (PK_LINE_ask(curve, &sf) != 0) continue;
            if (Math.Abs(sf.basis_set.location.coord[2] - 4) < 1e-9) { edge = edges[i]; break; }
        }
        Check(PK_MEMORY_free(edges), "free edges");
        AssertTrue(edge != 0, "top edge");

        // Standalone support plane matching the top face geometry.
        PK_PLANE_sf_t planeSf = new(new PK_AXIS2_sf_t(
            new PK_VECTOR_t(0.0, 0.0, 4.0),
            new PK_VECTOR1_t(0.0, 0.0, 1.0), new PK_VECTOR1_t(1.0, 0.0, 0.0)));
        PK_PLANE_t plane;
        Check(PK_PLANE_create(&planeSf, &plane), "plane create");

        // 2D B-curve in the plane UV from (0,0) to (1,0) (straight p-curve).
        double* poles = stackalloc double[4] { 0, 0, 1, 0 };
        double* knots = stackalloc double[2] { 0, 1 };
        int* mults = stackalloc int[2] { 2, 2 };
        var bSf = new PK_BCURVE_sf_t
        {
            degree = 1, n_vertices = 2, vertex_dim = 2, vertex = poles,
            n_knots = 2, knot = knots, knot_mult = mults,
            form = PK_BCURVE_form_arbitrary_c,
            knot_type = PK_knot_non_uniform_c,
            self_intersecting = PK_self_intersect_false_c,
        };
        PK_BCURVE_t bcurve;
        Check(PK_BCURVE_create(&bSf, &bcurve), "2D bcurve create");
        PK_SPCURVE_sf_t spSf = new(plane, bcurve);
        PK_SPCURVE_t spcurve;
        Check(PK_SPCURVE_create(&spSf, &spcurve), "spcurve create");
        Check(PK_TOPOL_detach_geom(edge), "detach");
        Check(PK_EDGE_attach_curves(1, &edge, &spcurve), "attach");

        var options = new PK_PART_transmit_o_t
        {
            o_t_version = 1,
            transmit_format = PK_transmit_format_text_c,
            transmit_version = 371
        };
        PK_MEMORY_block_t block;
        Check(PK_PART_transmit_b(1, &body, &options, &block), "transmit");
        var text = System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(block.bytes, (int)block.n_bytes));
        Check(PK_MEMORY_block_f(&block), "block free");
        File.WriteAllText(OutputPath(), text);
        Console.WriteLine("saved");
    }
}

static string ScriptPath([CallerFilePath] string path = "") => path;
static string OutputPath() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "probe-writer-geometry", "spcurve-ref.x_t"));

static void AssertTrue(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
}

static void Check(int error, string label)
{
    if (error != 0) throw new InvalidOperationException($"{label}: error={error}");
}
