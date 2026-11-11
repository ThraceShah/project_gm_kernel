#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=ParasolidBodyCompareOracle
global using EdgeSlot = int;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using M = ProjectGmKernel.Native.Generated;
using static parasolid;

var script = Path.GetDirectoryName(ScriptPath())!;
var root = Path.Combine(script, "..");
var author = Path.Combine(root, "..", "gm-blend-functional-delivery");
var name = args.Single(x => x.StartsWith("--output-name=", StringComparison.Ordinal))[14..];
Require(name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "new output name");
var output = Path.Combine(root, "temp_docs", "blend-60be-review", name);
Require(!Directory.Exists(output), "immutable review execution");
Directory.CreateDirectory(output);
foreach (var receipt in new[] {
    "docs/reviews/advanced-modeling/blend-functional-evidence.sha256",
    "temp_docs/blend-functional/final-api-evidence.sha256",
    "temp_docs/blend-functional/extra-query-evidence.sha256",
    "temp_docs/blend-functional/final-integrated-evidence.sha256",
    "temp_docs/blend-functional/shortedge-products.sha256" })
{
    var path = Path.Combine(author, receipt); var count = 0;
    foreach (var line in File.ReadLines(path))
    {
        if (line.Length == 0) continue;
        var relative = line[66..];
        Require(Hash(Path.Combine(author, relative)) == line[..64], "author receipt mismatch: " + relative);
        if (relative.StartsWith("src/", StringComparison.Ordinal) || relative.StartsWith("scripts/", StringComparison.Ordinal)
            || relative.StartsWith("tests/", StringComparison.Ordinal))
            Require(Hash(Path.Combine(root, relative)) == line[..64], "candidate source mismatch: " + relative);
        count++;
    }
    Console.WriteLine($"RECEIPT {receipt} entries={count} sha256={Hash(path)}");
}
var complete = File.ReadAllLines(Path.Combine(author, "temp_docs/blend-functional/final-integrated/complete-catalog.log"));
Require(complete.Count(x => x.StartsWith("GEOMETRY_TOPOLOGY_PASS ", StringComparison.Ordinal)) == 34, "17 construction + 17 lifecycle records");
Require(complete.Last() == "Local operation geometry: 17 passed, 0 failed, mass_unresolved=2.", "complete catalog status");
var failures = File.ReadAllLines(Path.Combine(author, "temp_docs/blend-functional/final-integrated/axial-material-query.log"))
    .Where(x => x.StartsWith("MANAGED_MATERIAL_QUERY ", StringComparison.Ordinal)).ToArray();
Require(failures.Length == 20 && failures.All(x => x.Contains("GM_error=5000 ", StringComparison.Ordinal)
    && x.Contains("PK_error=0 ", StringComparison.Ordinal)), "20 preserved unsupported GM queries");
foreach (var execution in new[] { "final-integrated/complete-catalog", "final-integrated/axial-material-query", "direction-controls-v5",
    "shortedge-single-v1", "regular-contract-v1", "chain-contract-v1", "native-gauge-control", "native-gauge-certified-v1" })
{
    var path = Path.Combine(author, "temp_docs/blend-functional", execution);
    Console.WriteLine($"HISTORICAL_EXECUTION {execution} original_exit={File.ReadAllText(path + ".exit").Trim()} log_sha256={Hash(path + ".log")}");
}
Console.WriteLine("GM_ASSEMBLY sha256=" + Hash(typeof(KernelRuntime).Assembly.Location));
Require(ParasolidScriptHost.TryStartSession("Independent Blend 60be review", out var host, out var reason), reason);
using (host) unsafe
{
    var start = new M.PK_SESSION_start_o_s { o_t_version = 1 };
    Check(KernelRuntime.SessionStart(&start), "GM start");
    try
    {
        foreach (var product in new[] { "block-all-fillet", "prism-all-chamfer", "oblique-single-fillet", "cylinder-unequal-chamfer" })
        foreach (var phase in new[] { "", "-lifecycle" })
        {
            var prefix = Path.Combine(author, "temp_docs/local-operation-oracle/blend-final-complete", product + phase);
            var a = Receive(File.ReadAllBytes(prefix + "-reference.x_t"));
            var b = Receive(File.ReadAllBytes(prefix + "-managed.x_t"));
            try
            {
                Full(a, product + phase + " reference"); Full(b, product + phase + " actual GM product");
                ParasolidBodyCompare.AssertEquivalent(a, b, "independent saved product " + product + phase);
                Console.WriteLine($"SAVED_PRODUCT_PASS {product}{phase} GM_sha256={Hash(prefix + "-managed.x_t")} PK_sha256={Hash(prefix + "-reference.x_t")}");
            }
            finally { Delete(a); Delete(b); }
        }
        var ellipseBytes = File.ReadAllBytes(Path.Combine(author, "temp_docs/local-operation-oracle/blend-final-complete/oblique-single-fillet-managed.x_t"));
        foreach (var mode in new[] { "ellipse-compensated", "ellipse-reverse-map", "ellipse-pi-reverse-map", "ellipse-wrong-endpoint" })
        {
            var own = ReceiveManaged(ellipseBytes);
            var record = KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];
            EdgeSlot found = -1; var e = record.FirstEdgeBody;
            for (var i = 0; i < record.EdgeCountBody; i++, e = KernelRuntime.Edges[e].NextInBody)
                if (KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(KernelRuntime.Edges[e].CurveTag)].Class == CurveClass.Ellipse) { found = e; break; }
            Require(found >= 0, "actual oblique product has finite ellipse");
            ref var edge = ref KernelRuntime.Edges[found];
            ref var curve = ref KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
            curve.Sense = curve.Sense == M.ParasolidConstants.PK_TOPOL_sense_positive_c
                ? M.ParasolidConstants.PK_TOPOL_sense_negative_c : M.ParasolidConstants.PK_TOPOL_sense_positive_c;
            if (mode.Contains("reverse-map", StringComparison.Ordinal))
            {
                ref var conic = ref KernelRuntime.EllipseDataPool[curve.DataIndex];
                conic.AxisX = -conic.AxisX; conic.AxisY = -conic.AxisY; conic.AxisZ = -conic.AxisZ;
                var shift = mode.Contains("pi", StringComparison.Ordinal) ? Math.PI : 0;
                if (shift != 0) { conic.RefDirX = -conic.RefDirX; conic.RefDirY = -conic.RefDirY; conic.RefDirZ = -conic.RefDirZ; }
                (edge.ParameterStart, edge.ParameterEnd) = (shift - edge.ParameterEnd, shift - edge.ParameterStart);
            }
            else
            {
                (edge.StartVertex, edge.EndVertex) = (edge.EndVertex, edge.StartVertex);
                var fin = edge.FirstFinEdge;
                for (var side = 0; side < 2; side++, fin = KernelRuntime.Fins[fin].Other)
                    KernelRuntime.Fins[fin].Sense = KernelRuntime.Fins[fin].Sense == '+' ? '-' : '+';
                if (mode == "ellipse-wrong-endpoint")
                {
                    ref var point = ref KernelRuntime.Points[KernelRuntime.GetPointSlotByTag(KernelRuntime.Vertices[edge.StartVertex].PointTag)];
                    point.Position.X += 5e-9;
                }
            }
            SessionMemoryOwner.DropBodyXt(KernelRuntime.ResolveTagRecord(own).Slot);
            var bytes = Transmit(own); Require(!bytes.AsSpan().SequenceEqual(ellipseBytes), "fixture mutation is actually transmitted");
            File.WriteAllBytes(Path.Combine(output, mode + ".x_t"), bytes);
            var a = Receive(ellipseBytes); var b = Receive(bytes);
            try { Adjudicate(a, b, mode, mode != "ellipse-wrong-endpoint"); }
            finally { Delete(a); Delete(b); Check(KernelRuntime.EntityDelete(1, &own), "delete ellipse fixture"); }
        }
        foreach (var delta in new[] { 1e-11, 5e-9 })
        {
            PK_BODY_t own; Check(KernelRuntime.BodyCreateSolidCyl(1, 2, null, &own), "circle fixture");
            var original = Transmit(own); var record = KernelRuntime.Bodies[KernelRuntime.ResolveTagRecord(own).Slot];
            ref var edge = ref KernelRuntime.Edges[record.FirstEdgeBody];
            ref var curve = ref KernelRuntime.Curves[KernelRuntime.GetCurveSlotByTag(edge.CurveTag)];
            curve.Sense = M.ParasolidConstants.PK_TOPOL_sense_negative_c;
            var fin = edge.FirstFinEdge;
            for (var side = 0; side < 2; side++, fin = KernelRuntime.Fins[fin].Other)
                KernelRuntime.Fins[fin].Sense = KernelRuntime.Fins[fin].Sense == '+' ? '-' : '+';
            KernelRuntime.CircleDataPool[curve.DataIndex].Radius += delta;
            var bytes = Transmit(own); var label = delta < 1e-9 ? "circle-within-cap" : "circle-outside-cap-below-native-precision";
            File.WriteAllBytes(Path.Combine(output, label + ".x_t"), bytes);
            File.WriteAllBytes(Path.Combine(output, label + "-reference.x_t"), original);
            var a = Receive(original); var b = Receive(bytes);
            try { Adjudicate(a, b, label, delta < 1e-9); }
            finally { Delete(a); Delete(b); Check(KernelRuntime.EntityDelete(1, &own), "delete circle fixture"); }
        }
        Require(KernelRuntime.Bodies.AliveCount == 0 && KernelRuntime.Curves.AliveCount == 0 && KernelRuntime.Surfaces.AliveCount == 0
            && KernelRuntime.Points.AliveCount == 0 && KernelRuntime.ReturnOutstandingBlocks == 0, "independent fixture cleanup");
        Console.WriteLine("PASS independent saved products=8 ellipse controls=4 circle precision controls=2; no Blend construction executed by this runner");
    }
    finally { Check(KernelRuntime.SessionStop(), "GM stop"); }
}

static unsafe void Adjudicate(PK_BODY_t a, PK_BODY_t b, string label, bool expected)
{
    var options = new PK_DEBUG_BODY_compare_o_t { max_diffs = 1024, all_tests = PK_LOGICAL_false,
        acc_dev_tests = PK_LOGICAL_false, tol_dev_tests = PK_LOGICAL_true, non_match_tests = PK_LOGICAL_true };
    PK_DEBUG_BODY_compare_r_t result; Check(PK_DEBUG_BODY_compare(a, b, &options, &result), "independent raw comparison");
    try
    {
        var proof = typeof(ParasolidBodyCompare).GetMethod("ProveFiniteAnalyticDirection", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<DirectionProof>();
        var senses = 0; var proved = 0; var other = 0;
        for (var f = 0; f < result.n_face_pairs; f++) for (var i = 0; i < result.face_pairs[f].n_local_diffs; i++)
        {
            var d = result.face_pairs[f].local_diffs[i];
            Console.WriteLine($"RAW_CONTROL {label} diff={d.diff} entities={d.master_entity}/{d.similar_entity}");
            if (d.diff == PK_DEBUG_diff_curve_sense_c) { senses++; if (proof(a, b, d.master_entity, d.similar_entity, in result)) proved++; }
            else if (d.diff != PK_DEBUG_diff_n_cht_pts_c) other++;
        }
        Console.WriteLine($"PROOF_CONTROL {label} expected={expected} raw_senses={senses} proved={proved} other={other} global={result.global_result} local={result.local_result}");
        Require(senses > 0, "control actually exercises raw sense proof: " + label);
        Require(expected ? proved == senses && other == 0 : proved < senses, "finite proof control result: " + label);
    }
    finally { Check(PK_DEBUG_BODY_compare_r_f(&result), "free raw control"); }
    var accepted = true;
    try { ParasolidBodyCompare.AssertEquivalent(a, b, label); }
    catch (InvalidOperationException ex) when (ex.Message.Contains("body geometry/structure/sense/tolerance/matching differs", StringComparison.Ordinal)) { accepted = false; }
    Require(accepted == expected, "full comparator result: " + label);
    if (expected) { Full(a, label + " reference"); Full(b, label + " received"); }
}
static unsafe void Full(PK_BODY_t body, string label)
{
    var options = new PK_BODY_check_o_t { max_faults = 100 }; int n; PK_check_fault_t* faults;
    Check(PK_BODY_check(body, &options, &n, &faults), "full native body check");
    try { Console.WriteLine($"FULL_CHECK {label} faults={n}"); Require(n == 0, label + " native body faults"); }
    finally { if (faults != null) Check(PK_MEMORY_free(faults), "free faults"); }
}
static unsafe PK_BODY_t Receive(byte[] bytes)
{
    fixed (byte* p = bytes)
    {
        var block = new PK_MEMORY_block_t(null, (ulong)bytes.Length, p); var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int n; PK_PART_t* parts; Check(PK_PART_receive_b(block, &options, &n, &parts), "real PK receive");
        try { Require(n == 1, "one PK part"); return parts[0]; } finally { Check(PK_MEMORY_free(parts), "free PK parts"); }
    }
}
static unsafe PK_BODY_t ReceiveManaged(byte[] bytes)
{
    fixed (byte* p = bytes)
    {
        var block = new M.PK_MEMORY_block_s { n_bytes = (nuint)bytes.Length, bytes = p }; var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
        int n; PK_PART_t* parts; Check(KernelRuntime.PartReceiveB(block, (M.PK_PART_receive_o_s*)&options, &n, &parts), "GM receive");
        try { Require(n == 1, "one GM part"); return parts[0]; } finally { Check(KernelRuntime.MemoryFree(parts), "free GM parts"); }
    }
}
static unsafe byte[] Transmit(PK_BODY_t body)
{
    var options = new M.PK_PART_transmit_o_s { o_t_version = 4, transmit_format = PK_transmit_format_text_c, transmit_version = 371, transmit_meshes = PK_transmit_meshes_separate_c };
    M.PK_MEMORY_block_s block; Check(KernelRuntime.PartTransmitB(1, &body, &options, &block), "GM fixture transmit");
    try { using var stream = new MemoryStream(); for (var p = &block; p != null; p = p->next) stream.Write(new ReadOnlySpan<byte>(p->bytes, checked((int)p->n_bytes))); return stream.ToArray(); }
    finally { Check(KernelRuntime.MemoryBlockFree(&block), "free GM XT"); }
}
static unsafe void Delete(PK_BODY_t body) => Check(PK_ENTITY_delete(1, &body), "delete PK fixture");
static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string ScriptPath([CallerFilePath] string path = "") => path;
static void Check(int error, string label) => ParasolidScriptHost.Check(error, label);
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
unsafe delegate bool DirectionProof(PK_BODY_t a, PK_BODY_t b, PK_CURVE_t ac, PK_CURVE_t bc, in PK_DEBUG_BODY_compare_r_t comparison);
