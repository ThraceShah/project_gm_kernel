#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Text inventory of a fixed git snapshot. Signals are candidates, never test outcomes.
var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!, ".."));
const string snapshot = "610e3ce844bf99c85af6069905f9f3deaab08133";
var destination = Path.Combine(root, "docs/reviews/advanced-modeling/operation-acceptance-audit-610e3ce.json");
var paths = Git("ls-tree", "-r", "--name-only", snapshot, "--", "scripts", "tests/KernelTests", "docs/reviews").Split('\n', StringSplitOptions.RemoveEmptyEntries);
var scripts = paths.Where(p => p.StartsWith("scripts/", StringComparison.Ordinal) && p.EndsWith(".cs", StringComparison.Ordinal)).ToArray();
var tests = paths.Where(p => p.StartsWith("tests/KernelTests/", StringComparison.Ordinal) && p.EndsWith(".cs", StringComparison.Ordinal)).ToArray();
var documents = paths.Where(p => p.StartsWith("docs/reviews/", StringComparison.Ordinal) && p.EndsWith(".md", StringComparison.Ordinal)).ToArray();
var sources = scripts.Concat(tests).Concat(documents).ToDictionary(p => p, p => Git("show", snapshot + ":" + p), StringComparer.Ordinal);
var registry = JsonNode.Parse(Git("show", snapshot + ":scripts/web-progress-registry.json"))!;
var recipes = registry["cases"]!.AsArray();
var entries = new JsonArray();
foreach (var path in scripts)
    entries.Add((JsonNode)Entry(path, Path.GetFileNameWithoutExtension(path), sources[path], 1, "script-file"));
var testCount = 0;
var unindexedTests = new JsonArray();
foreach (var path in tests)
{
    var text = sources[path]; var masked = Mask(text);
    var declarations = Regex.Matches(MaskComments(text), @"\[(?:Fact|Theory)(?:\([^\]]*\))?\](?:\s|\[[^\]]*\])*(?:public|internal)\s+(?:async\s+|unsafe\s+)*(?:void|Task|ValueTask)\s+(\w+)\s*\(");
    foreach (Match attribute in Regex.Matches(text, @"\[(?:Fact|Theory)(?:\(|\])"))
        if (!declarations.Any(d => d.Index == attribute.Index))
            unindexedTests.Add((JsonNode)new JsonObject { ["path"] = path, ["line"] = Line(text, attribute.Index), ["status"] = "unindexed signature; manual follow-up required" });
    foreach (Match match in declarations)
    {
        var open = masked.IndexOf('{', match.Index + match.Length);
        if (open < 0) continue;
        var depth = 1; var end = open + 1;
        while (end < masked.Length && depth > 0) { if (masked[end] == '{') depth++; if (masked[end] == '}') depth--; end++; }
        entries.Add((JsonNode)Entry(path, match.Groups[1].Value, text[match.Index..end], Line(text, match.Index), "attributed-test-method")); testCount++;
    }
}
var docEntries = new JsonArray();
foreach (var path in documents)
{
    var text = sources[path]; var lines = text.Split('\n');
    var markers = new JsonArray();
    for (var i = 0; i < lines.Length; i++)
        if (Regex.IsMatch(lines[i], @"(?i)complet|accept|passed|完成|通过|验收")) markers.Add((JsonNode)JsonValue.Create(i + 1)!);
    docEntries.Add((JsonNode)new JsonObject { ["path"] = path, ["sha256"] = Hash(text), ["claimMarkerLines"] = markers,
        ["status"] = "text-indexed; claims require fixed-log adjudication, not inferred true/false" });
}
var catalogText = Git("show", snapshot + ":src/ProjectGmKernel.Native/Runtime/FunctionCatalog.generated.cs");
var catalog = new JsonArray();
foreach (Match match in Regex.Matches(catalogText, "(\\d+) => \"([^\"]+)\"u8"))
{
    var name = match.Groups[2].Value;
    var normalized = name.StartsWith("PK_", StringComparison.Ordinal) ? name[3..].Replace("_", "", StringComparison.Ordinal) : name;
    var references = new JsonArray();
    foreach (var entry in entries)
        foreach (var call in entry!["operationOccurrences"]!.AsArray())
        {
            var called = call!["call"]!.GetValue<string>();
            if (called != name && !(called.StartsWith("KernelRuntime.", StringComparison.Ordinal) && string.Equals(called[14..], normalized, StringComparison.OrdinalIgnoreCase))) continue;
            references.Add((JsonNode)new JsonObject { ["entryId"] = entry["id"]!.DeepClone(), ["line"] = call["line"]!.DeepClone(), ["call"] = called,
                ["mapping"] = called == name ? "exact native symbol" : "normalized-name candidate; forwarding/helper semantics not proved" });
        }
    catalog.Add((JsonNode)new JsonObject { ["id"] = int.Parse(match.Groups[1].Value), ["api"] = name, ["category"] = Category(name),
        ["evidence"] = "src/ProjectGmKernel.Native/Runtime/FunctionCatalog.generated.cs:" + Line(catalogText, match.Index),
        ["staticReferences"] = references,
        ["independentAcceptance"] = "unknown; dispatch registration is not acceptance" });
}
var caseEntries = new JsonArray();
foreach (var recipe in recipes)
{
    var source = recipe!["source"]!.GetValue<string>();
    caseEntries.Add((JsonNode)new JsonObject { ["id"] = recipe["id"]!.DeepClone(), ["family"] = recipe["family"]!.DeepClone(),
        ["api"] = recipe["api"]?.DeepClone(), ["operation"] = recipe["operation"]!.DeepClone(), ["source"] = source,
        ["parameters"] = recipe["parameters"]!.DeepClone(), ["arguments"] = recipe["arguments"]?.DeepClone(),
        ["adapter"] = recipe["meshAdapter"]!.GetValue<bool>() ? "implemented" : "not-implemented",
        ["entryId"] = source + "#" + Path.GetFileNameWithoutExtension(source),
        ["statusOwnership"] = "case-level; current collector does not persist independent per-operation primary status",
        ["dynamicPath"] = "unknown unless recorded in historicalRuns" });
}
var runs = new JsonArray();
var runPaths = Git("ls-tree", "-r", "--name-only", snapshot, "--", "web/data/runs").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
foreach (var path in runPaths.Where(p => p.EndsWith("/manifest.json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
{
    var manifest = JsonNode.Parse(Git("show", snapshot + ":" + path))!; var cases = new JsonArray();
    foreach (var c in manifest["cases"]!.AsArray().Where(c => c!["execution"]?["attempted"]?.GetValue<bool>() == true))
    {
        var capturePath = Path.GetDirectoryName(path)!.Replace('\\', '/') + "/cases/" + c!["id"]!.GetValue<string>() + "/sample-0/capture.json";
        var timings = runPaths.Contains(capturePath) ? JsonNode.Parse(Git("show", snapshot + ":" + capturePath))!["timings"]!.DeepClone() : null;
        cases.Add((JsonNode)new JsonObject { ["id"] = c!["id"]!.DeepClone(), ["status"] = c["status"]!.DeepClone(),
            ["operation"] = c["operation"]?.DeepClone(), ["stepCount"] = c["steps"]!.AsArray().Count,
            ["stepTitles"] = new JsonArray(c["steps"]!.AsArray().Select(s => s!["title"]!.DeepClone()).ToArray()),
            ["unownedCheckCount"] = c["checks"]!.AsArray().Count(ch => ch?["operationId"] == null),
            ["ratio"] = c["performance"]?["ratioGmOverPk"]?.DeepClone(), ["timingNote"] = c["performance"]?["note"]?.DeepClone(),
            ["captureEvidence"] = runPaths.Contains(capturePath) ? capturePath : null, ["captureTimings"] = timings });
    }
    runs.Add((JsonNode)new JsonObject { ["manifest"] = path, ["implementation"] = manifest["run"]!["commit"]!.DeepClone(), ["cases"] = cases });
}
var rules = new JsonArray
{
    Rule("A1", "confirmed", "case checks/status have no operation owner; valid combination result can be misread as every API's status", ["scripts/RefreshWebProgress.cs", "scripts/WebProgressCapture.cs"], "scripts/RefreshWebProgress.cs:102-129; scripts/WebProgressCapture.cs:208-209", "all adapter-enabled recipes are schema exposure; actual multiple-step cases are counted separately"),
    Rule("A2", "confirmed", "case performance sums captured API steps; cannot be a single operation's latency", ["scripts/RefreshWebProgress.cs"], "scripts/RefreshWebProgress.cs:102,125-129", "all adapter-enabled recipes are schema exposure; no construction/export/check I/O inflation alleged"),
    Rule("A3", "confirmed", "fillet, classification, copy/transform/receive and subsequent Boolean share parent exit", ["scripts/ShortEdgeBlendOracle.cs"], "scripts/ShortEdgeBlendOracle.cs:256-298,341-403", "source-level affected recipe envelope; optional flags are not claimed executed"),
    Rule("A4", "confirmed", "two freeform construction cases include import/copy/delete/transform lifecycle before parent PASS", ["scripts/FreeformModelingOracle.cs"], "scripts/FreeformModelingOracle.cs:49-65,470-496", "only parallel-nurbs-path-solid and three-profile-natural-loft execute this conditional lifecycle"),
    Rule("A5", "confirmed", "imported local edit cases include lifecycle inside parent result", ["scripts/LocalTopologyChangeOracle.cs"], "scripts/LocalTopologyChangeOracle.cs:42-81,270-288", "only Imported cases; source recipe envelope is an upper bound"),
    Rule("A6", "confirmed", "body transform case also tests copy/source deletion/dependent geometry/XT before parent PASS", ["scripts/GeometryTransformOracle.cs"], "scripts/GeometryTransformOracle.cs:254-303", "body branch only; geometry branches must not be labelled affected by this rule"),
    Rule("A7", "historical-confirmed", "evaluation primary positives and later dependency deletion share terminal failure", ["scripts/ParasolidEvaluationOracle.cs"], "docs/ParasolidBodyCompareValidation.md:31; scripts/ParasolidEvaluationOracle.cs:119,676-685", "historical report distinguishes ten passed primary fixtures from offset deletion5002"),
    Rule("A8", "candidate", "different input lineage after independent upstream operations; isolate same-input before attributing a downstream difference", ["scripts/BlendChainOracle.cs", "scripts/SupportBlendChainOracle.cs", "scripts/TrivalentBlendCornerOracle.cs", "scripts/MixedCurvedBooleanOracle.cs", "scripts/RotationalModelingOracle.cs", "scripts/RegularSolidBlendOracle.cs"], "source chains indexed in entries; not all branches dynamically adjudicated", "legitimate integration tests retained; no universal same-input defect asserted"),
    Rule("A9", "candidate", "local normalization and bespoke compare masks require applicability review per operation", ["scripts/LocalOperationOracle.cs", "scripts/IntersectionOperationOracle.cs"], "scripts/LocalOperationOracle.cs:84-90,303; scripts/IntersectionOperationOracle.cs:347-363", "wire auxiliary exists; non_match=false alone is not proved invalid for face-free wires"),
};
var result = new JsonObject { ["snapshot"] = snapshot, ["method"] = "fixed git objects; file lexical API occurrences, attributed test methods, no interprocedural or branch proof",
    ["limitations"] = "API lists retain lexical order and duplicates; construction/helper calls are not inferred operation chains. InlineData is not expanded, MemberData/dynamic dispatch and unregistered CLI products are unknown. Candidate signals never establish a bug or API pass.",
    ["counts"] = new JsonObject { ["catalog"] = catalog.Count, ["scriptFiles"] = scripts.Length, ["testFiles"] = tests.Length, ["attributedTestMethods"] = testCount, ["reviewMarkdownFiles"] = documents.Length, ["recipes"] = recipes.Count,
        ["scriptEntrypoints"] = scripts.Count(p => sources[p].StartsWith("#!/usr/bin/env dotnet run", StringComparison.Ordinal)),
        ["testAttributes"] = tests.Sum(p => Regex.Matches(sources[p], @"\[(?:Fact|Theory)(?:\(|\])").Count),
        ["adapterEnabled"] = recipes.Count(c => c!["meshAdapter"]!.GetValue<bool>()), ["adapterMissing"] = recipes.Count(c => !c!["meshAdapter"]!.GetValue<bool>()) },
    ["catalog"] = catalog, ["recipes"] = caseEntries, ["entries"] = entries, ["unindexedTestAttributes"] = unindexedTests, ["reviewClaims"] = docEntries, ["historicalRuns"] = runs, ["rules"] = rules };
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
File.WriteAllText(destination, result.ToJsonString(new() { WriteIndented = true }));
Console.WriteLine(result["counts"]!.ToJsonString());
Console.WriteLine("families=" + string.Join(", ", recipes.GroupBy(c => c!["family"]!.GetValue<string>()).Select(g => g.Key + ":" + g.Count())));
Console.WriteLine("categories=" + string.Join(", ", catalog.GroupBy(c => c!["category"]!.GetValue<string>()).Select(g => g.Key + ":" + g.Count())));
Console.WriteLine("unclassified=" + string.Join(", ", catalog.Where(c => c!["category"]!.GetValue<string>() == "unclassified-review-required").Select(c => c!["api"]!.GetValue<string>())));
foreach (var run in runs)
{
    Console.WriteLine(run!["manifest"] + " attempted=" + run["cases"]!.AsArray().Count + " multistep=" + run["cases"]!.AsArray().Count(c => c!["stepCount"]!.GetValue<int>() > 1));
    foreach (var c in run["cases"]!.AsArray().Where(c => c!["stepCount"]!.GetValue<int>() > 1)) Console.WriteLine(c!.ToJsonString());
}
foreach (var rule in rules) Console.WriteLine(rule!["id"] + " recipe-envelope=" + rule["affectedRecipeEnvelope"]!.AsArray().Count);

JsonObject Entry(string path, string name, string text, int firstLine, string kind)
{
    var apis = new JsonArray();
    foreach (Match m in Regex.Matches(MaskComments(text), @"\b(KernelRuntime\.\w+|PK_\w+)\s*\("))
        apis.Add((JsonNode)new JsonObject { ["call"] = m.Groups[1].Value, ["line"] = firstLine + Line(text, m.Index) - 1 });
    var markers = new JsonArray();
    foreach (Match m in Regex.Matches(text, @"AssertEquivalent|AssertWireGeometry|PK_DEBUG_BODY_compare\(|PK_BODY_check\(|CheckBody\(|CompareMass\(|Mass\(|BodyContainsVector\(|InjectAllocationFailure\(|Assert\.|Stopwatch\.|WebProgressCapture\.|Environment.ExitCode|Require\(failures|Require\(failed|PASS|FAIL "))
        markers.Add((JsonNode)new JsonObject { ["marker"] = m.Value, ["line"] = firstLine + Line(text, m.Index) - 1 });
    var categories = apis.Select(a => a!["call"]!.GetValue<string>()).Select(Category).Distinct().ToArray();
    return new JsonObject { ["id"] = path + "#" + name, ["entryPath"] = path, ["entry"] = name, ["line"] = firstLine, ["kind"] = kind,
        ["sha256"] = Hash(text), ["operationOccurrences"] = apis, ["categories"] = new JsonArray(categories.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()),
        ["checkpoints"] = markers, ["statusOwnership"] = kind == "script-file" ? "process/case/helper; lexical markers only" : "test method aggregate assertion outcome",
        ["timingBoundary"] = text.Contains("Stopwatch", StringComparison.Ordinal) ? "timing present; see indexed source markers, scope requires manual review" : "no direct Stopwatch marker; helper timing unknown",
        ["evidenceState"] = "static-indexed; dynamic API outcome unknown", ["multiCategoryCandidate"] = categories.Length > 1,
        ["affectedRecipes"] = new JsonArray(recipes.Where(c => c!["source"]!.GetValue<string>() == path).Select(c => c!["id"]!.DeepClone()).ToArray()) };
}
JsonNode Rule(string id, string state, string issue, string[] owners, string evidence, string boundary) => new JsonObject
{
    ["id"] = id, ["state"] = state, ["issue"] = issue, ["owners"] = new JsonArray(owners.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()), ["evidence"] = evidence, ["boundary"] = boundary,
    ["affectedRecipeEnvelope"] = new JsonArray(recipes.Where(c => id is "A1" or "A2" ? c!["meshAdapter"]!.GetValue<bool>() : owners.Contains(c!["source"]!.GetValue<string>(), StringComparer.Ordinal)).Select(c => c!["id"]!.DeepClone()).ToArray())
};
static string Category(string name)
{
    if (Regex.IsMatch(name, "(?i)contains_vector|ContainsVector|Enclosure")) return "classification";
    if (Regex.IsMatch(name, "(?i)eval|inverse|parameterise|find_vector|find_min|find_length|GEOM_range")) return "evaluation-projection-distance";
    if (Regex.IsMatch(name, "(?i)transmit|receive|PartTransmit|PartReceive")) return "import-export";
    if (Regex.IsMatch(name, "(?i)copy|transform")) return "copy-transform";
    if (Regex.IsMatch(name, "(?i)_ask|Ask|check|debug|mass|is_|_f$|free|memory|session|thread|delete|set_user|find_|FUNCTION_find|MARK_goto|PARTITION_set")) return "inquiry-quality-resource-session";
    if (Regex.IsMatch(name, "(?i)boolean")) return "boolean";
    if (Regex.IsMatch(name, "(?i)intersect|section|imprint")) return "intersection-section-imprint";
    if (Regex.IsMatch(name, "(?i)blend|chamfer")) return "blend-chamfer";
    if (Regex.IsMatch(name, "(?i)hollow|offset|delete_faces|Local")) return "local-edit-offset-hollow";
    if (Regex.IsMatch(name, "(?i)extrude|spin|sweep|loft|sew|trim|split_at_param|create|knot|degree|reparameter|attach|detach|make_|Make|Create|SetGeom|CRTRCU")) return "construction-geometry-edit";
    if (Regex.IsMatch(name, "(?i)ask|check|debug|mass|is_|_f$|free|memory|session|thread|delete|find_")) return "inquiry-quality-resource-session";
    return "unclassified-review-required";
}
string Git(params string[] arguments)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException(error); return output;
}
static int Line(string text, int index) => 1 + text.AsSpan(0, index).Count('\n');
static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
static string MaskComments(string text) => Regex.Replace(text, @"//[^\n]*|/\*[\s\S]*?\*/", m => new string(' ', m.Length));
static string Mask(string text) => Regex.Replace(text, "@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|//[^\\n]*|/\\*[\\s\\S]*?\\*/", m => new string(' ', m.Length));
static string ScriptPath([CallerFilePath] string path = "") => path;
