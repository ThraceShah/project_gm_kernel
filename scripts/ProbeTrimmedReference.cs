#!/usr/bin/env dotnet run
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

// Reference probe: Parasolid's own XT for a block whose first edge carries a
// trimmed curve (basis = the detached edge line), to see where PK chains the
// basis curve and what owner/next/previous it writes.

using System.Runtime.CompilerServices;
using static parasolid;

unsafe
{
    if (!ParasolidScriptHost.TryStartSession("trimmed reference probe", out var host, out var message))
        throw new InvalidOperationException(message);
    using (host)
    {
        PK_BODY_t body;
        Check(PK_BODY_create_solid_block(2, 3, 4, null, &body), "block");
        int count;
        PK_EDGE_t* edges;
        Check(PK_BODY_ask_edges(body, &count, &edges), "edges");
        PK_EDGE_t edge = edges[0];
        Check(PK_MEMORY_free(edges), "free edges");
        PK_CURVE_t basis;
        Check(PK_EDGE_ask_curve(edge, &basis), "edge curve");
        PK_INTERVAL_t interval;
        Check(PK_CURVE_ask_interval(basis, &interval), "interval");
        Console.WriteLine($"basis interval [{interval.value[0]:R},{interval.value[1]:R}]");
        Check(PK_TOPOL_detach_geom(edge), "detach");
        double p1 = interval.value[0] + 0.25 * (interval.value[1] - interval.value[0]);
        double p2 = interval.value[0] + 0.75 * (interval.value[1] - interval.value[0]);
        PK_CURVE_t trimmed;
        int ifail;
        CRTRCU(&basis, &p1, &p2, &trimmed, &ifail);
        AssertTrue(ifail == 0, "CRTRCU");
        Check(PK_EDGE_attach_curves(1, &edge, &trimmed), "attach");

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
        File.WriteAllText(Path.Combine(OutputDir(), "trimmed-ref.x_t"), text);
        Console.WriteLine("saved");
    }
}

static string ScriptPath([CallerFilePath] string path = "") => path;
static string OutputDir() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, "..", "temp_docs", "probe-writer-geometry"));
static string OutputPath() => Path.Combine(OutputDir(), "trimmed-ref.x_t");

static void AssertTrue(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
}

static void Check(int error, string label)
{
    if (error != 0) throw new InvalidOperationException($"{label}: error={error}");
}
