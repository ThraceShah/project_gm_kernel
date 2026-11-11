#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj

global using ReviewEntity = System.Int32;
global using ReviewIdentifier = System.Int32;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using static parasolid;

// Independent, read-only four-XT consumer audit. No model construction or exporter execution.
var scripts = Path.GetDirectoryName(ScriptPath())!;
try
{
    if (args.Length != 2 || args[0] != "--source-run" || Path.IsPathRooted(args[1]))
        throw new ArgumentException("--source-run <path relative to scripts/>");
    var source = Path.GetFullPath(args[1], scripts);
    var package = Path.GetFullPath("../web/data/display/real-uv", scripts);
    var sample = Path.Combine(source, "cases/solid-blend.plane-nurbs/sample-0");
    var display = Path.Combine(sample, "display");
    var manifest = Read(Path.Combine(source, "manifest.json"));
    var executionPath = Path.Combine(sample, "execution.json");
    var execution = Read(executionPath);
    var producer = "eecab6d51cae79a687fa459af512adc3e03895f5";
    Require(S(manifest["run"]!, "commit") == producer && manifest["run"]!["provenance"]!["dirtyAtStart"]!.GetValue<bool>() == false, "clean producer");
    Require(execution["exitCode"]!.GetValue<int>() == 0 && execution["timedOut"]!.GetValue<bool>() == false, "original execution");
    foreach (var (file, expected) in new[] {
        ("scripts/WebProgressCapture.cs", "da417ff2733163be641b97416f48014ebaca73e28f628e50203b3fe69735eaa8"),
        ("scripts/WebProgressDisplayReceipts.cs", "76e44ab354d31a9f80fcc28ac2afb2f5fa4f54eee044b8f64b0a94bd939e4204") })
    {
        Require(S(execution["sourceHashes"]!, file) == expected && S(manifest["run"]!["provenance"]!["sourceHashes"]!, file) == expected, "execution/manifest source hash");
        var start = new ProcessStartInfo("git") { WorkingDirectory = scripts, RedirectStandardOutput = true };
        start.ArgumentList.Add("show"); start.ArgumentList.Add(producer + ":" + file);
        using var child = Process.Start(start)!; using var bytes = new MemoryStream();
        child.StandardOutput.BaseStream.CopyTo(bytes); child.WaitForExit();
        Require(child.ExitCode == 0 && Hash(bytes.ToArray()) == expected, "immutable producer blob");
    }
    if (!ParasolidScriptHost.TryStartSession("independent f3 UV read-only review", out var session, out var reason))
        throw new InvalidOperationException("ENV_NOT_READY: " + reason);
    using (session) unsafe
    {
        var seen = new HashSet<string>();
        foreach (var item in Read(Path.Combine(package, "display-manifest.json"))["items"]!.AsArray())
        {
            var snapshot = S(item!, "sourceSnapshot");
            var receiptPath = Path.Combine(display, snapshot + ".receipt.json");
            var receipt = Read(receiptPath);
            var kernel = S(receipt, "kernel").ToUpperInvariant(); var stage = S(receipt, "stage");
            Require(seen.Add(kernel + "/" + stage), "unique receipt kernel/stage");
            foreach (var key in new[] { "sourceRun", "sourceSnapshot", "caseId", "sampleId", "stepId", "stage" })
                Require(S(item!, key) == S(receipt, key), "item/receipt " + key);
            Require(S(item!, "caseId") == "solid-blend.plane-nurbs" && S(item!, "sampleId") == "sample-0" && S(item!, "stepId") == "operation-1", "scoped identity");
            Require(S(item!, "kernel") == kernel && Hash(File.ReadAllBytes(receiptPath)) == S(item!, "snapshotReceiptSha256"), "receipt bytes/kernel");
            var meshPath = Path.GetFullPath(S(receipt, "meshPath"), display);
            var xtBytes = File.ReadAllBytes(Path.GetFullPath(S(receipt, "xtPath"), display));
            var meshBytes = File.ReadAllBytes(meshPath); var mesh = JsonNode.Parse(meshBytes)!;
            Require(Hash(meshBytes) == S(receipt, "meshSha256") && Hash(meshBytes) == S(item!, "sourceMeshSha256") && meshBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(package, S(item!, "meshPath")))), "complete original/public mesh bytes");
            Require(Hash(xtBytes) == S(receipt, "xtSha256") && Hash(xtBytes) == S(item!, "inputXtSha256"), "complete XT bytes");
            Require(Hash(File.ReadAllBytes(executionPath)) == S(item!, "executionSha256"), "complete execution bytes");
            Require(Path.GetFullPath(S(mesh, "displayReceipt"), Path.GetDirectoryName(meshPath)!) == receiptPath && S(mesh["source"]!, "kernel") == kernel && S(mesh["source"]!, "commit") == producer, "original mesh receipt/source");
            var isoBytes = File.ReadAllBytes(Path.Combine(package, S(item!, "path"))); var iso = JsonNode.Parse(isoBytes)!;
            Require(Hash(isoBytes) == S(item!, "sha256") && iso["parameterHash"] == null, "ISO bytes/unknown parameter SHA");
            foreach (var key in new[] { "sourceRun", "sourceSnapshot", "caseId", "sampleId", "stepId", "stage", "kernel", "sourceMeshSha256", "inputXtSha256", "snapshotReceiptSha256", "executionSha256", "producerCommit", "captureCodeSha256", "receiptCodeSha256" })
                Require(S(iso, key) == S(item!, key), "ISO/item " + key);
            ReviewEntity body = 0; var nParts = 0; ReviewEntity* parts = null;
            fixed (byte* data = xtBytes)
            {
                var block = new PK_MEMORY_block_t(null, (ulong)xtBytes.Length, data);
                var options = new PK_PART_receive_o_t { transmit_format = PK_transmit_format_text_c };
                Check(PK_PART_receive_b(block, &options, &nParts, &parts), "receive");
                Require(nParts == 1, "one actual body"); body = parts[0]; PK_MEMORY_free(parts);
            }
            var nFaces = 0; ReviewEntity* faceTags = null; var xyzCount = 0; var maxError = 0.0;
            try
            {
                Check(PK_BODY_ask_faces(body, &nFaces, &faceTags), "faces");
                var byId = new Dictionary<ReviewIdentifier, ReviewEntity>();
                for (var i = 0; i < nFaces; i++) { ReviewIdentifier id = 0; Check(PK_ENTITY_ask_identifier(faceTags[i], &id), "identifier"); Require(byId.TryAdd(id, faceTags[i]), "unique received identifier"); }
                Require(nFaces == mesh["faces"]!.AsArray().Count && nFaces == receipt["faceBindings"]!.AsArray().Count, "all face counts");
                var bound = new HashSet<ReviewIdentifier>(); var meshIds = new HashSet<string>();
                foreach (var face in mesh["faces"]!.AsArray())
                {
                    var id = face!["identifier"]!.GetValue<ReviewIdentifier>();
                    Require(bound.Add(id) && byId.ContainsKey(id) && meshIds.Add(S(face, "id")), "mesh unique identity");
                    Require(receipt["faceBindings"]!.AsArray().Count(b => S(b!, "meshFaceId") == S(face, "id") && b!["xtFaceIdentifier"]!.GetValue<ReviewIdentifier>() == id && b["entityTag"]!.GetValue<ReviewEntity>() == face["entityTag"]!.GetValue<ReviewEntity>()) == 1, "receipt mapping");
                }
                foreach (var face in iso["faces"]!.AsArray())
                {
                    var id = face!["identifier"]!.GetValue<ReviewIdentifier>(); var tag = byId[id];
                    var matched = mesh["faces"]!.AsArray().Single(f => S(f!, "id") == S(face, "faceId") && f!["identifier"]!.GetValue<ReviewIdentifier>() == id);
                    Require(matched!["entityTag"]!.GetValue<ReviewEntity>() == face["meshEntityTag"]!.GetValue<ReviewEntity>(), "ISO mesh binding");
                    ReviewEntity surface = 0; Check(PK_FACE_ask_surf(tag, &surface), "surface");
                    PK_LOGICAL_t rectangular = PK_LOGICAL_false; PK_UVBOX_t box = default;
                    Check(PK_FACE_is_uvbox(tag, &rectangular, &box), "recttrim"); Require(rectangular == PK_LOGICAL_true, "actual rectangular trim");
                    for (var j = 0; j < 4; j++) Require(Math.Abs(box.param[j] - face["trimUvBox"]![j]!.GetValue<double>()) < 1e-12, "actual UV domain");
                    var curveIndex = 0;
                    foreach (var direction in new[] { "u", "v" })
                    foreach (var line in face[direction + "Lines"]!.AsArray())
                    {
                        var curve = face["curves"]![curveIndex++]!; var uv = curve["uvSamples"]!.AsArray(); var xyz = line!.AsArray();
                        Require(S(curve, "direction") == direction && uv.Count / 2 == xyz.Count / 3, "curve ordering");
                        var constant = curve["constantParameter"]!.GetValue<double>();
                        for (var j = 0; j < uv.Count / 2; j++)
                        {
                            var u = uv[2*j]!.GetValue<double>(); var v = uv[2*j+1]!.GetValue<double>();
                            Require((direction == "u" ? u : v) == constant, "true constant U/V");
                            var actual = Evaluate(surface, tag, u, v); var error2 = 0.0;
                            for (var k = 0; k < 3; k++) { var delta = actual.coord[k] - xyz[j*3+k]!.GetValue<double>(); error2 += delta*delta; }
                            maxError = Math.Max(maxError, Math.Sqrt(error2)); xyzCount++;
                        }
                        Require(curve["sampledChordError"]!.GetValue<double>() <= iso["chordTolerance"]!.GetValue<double>(), "reported sampled chord error");
                    }
                    Console.WriteLine($"FACE {kernel}/{stage} {S(face, "faceId")} identifier={id} U={face["uLines"]!.AsArray().Count} V={face["vLines"]!.AsArray().Count}");
                }
                Require(maxError < 1e-12, "actual surface XYZ");
                Console.WriteLine($"PASS {kernel}/{stage} {snapshot} allBindings={nFaces} evaluatedPoints={xyzCount} maxXyzError={maxError:R} meshSha={Hash(meshBytes)} xtSha={Hash(xtBytes)}");
            }
            finally { PK_MEMORY_free(faceTags); Check(PK_ENTITY_delete(1, &body), "delete received body"); }
        }
        Require(seen.SetEquals(new[] { "GM/before", "GM/after", "PK/before", "PK/after" }), "four exact stages");
    }
    Console.WriteLine("PASS independent frozen four-XT source/face/recttrim/surface audit; no modeling acceptance or analytic chord bound claimed");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 1; }

static string ScriptPath([CallerFilePath] string path = "") => path;
static JsonNode Read(string path) => JsonNode.Parse(File.ReadAllBytes(path))!;
static string S(JsonNode obj, string key) => obj[key]!.GetValue<string>();
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Require(bool ok, string label) { if (!ok) throw new InvalidDataException(label); }
static void Check(PK_ERROR_code_t error, string label) { if (error != 0) throw new InvalidOperationException(label + ": PK error " + error); }
static unsafe PK_VECTOR_t Evaluate(ReviewEntity surface, ReviewEntity face, double u, double v)
{
    var uv = new PK_UV_t(u, v); var options = new PK_FACE_contains_vectors_o_t { n_uvs = 1, uvs = &uv }; ReviewEntity topol = 0;
    Check(PK_FACE_contains_vectors(face, &options, &topol), "trim containment"); Require(topol != 0, "point inside actual trim");
    PK_VECTOR_t p = default; Check(PK_SURF_eval(surface, uv, 0, 0, PK_LOGICAL_false, &p), "actual surface evaluation"); return p;
}
