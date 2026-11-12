#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Appends the accepted cumulative integration 3aff4e2b performance event to the
// current overview event log. Numbers come only from the independent review
// tree's original measurement receipt; no other case or dimension is re-signed.
// Idempotent: replaces exactly the events it owns (same utc + sourceEvent).
var scripts = Path.GetDirectoryName(ScriptPath())!;
var review = Path.GetFullPath(Option("--review", "../../gm-accepted-integration-3aff4e2b-review"), scripts);
var eventsPath = Path.GetFullPath(Option("--events", "../web/data/overview-events.json"), scripts);
var runId = Option("--run-id", "");
if (runId.Length == 0) throw new ArgumentException("--run-id is required");

const string Calculation = "3aff4e2b0247bd4f21e74e71d437a33dff1a3e24";
const string Native = "854d01fec8201646b256acbb8297d3388dd74cdd";
const string Src = "545c317e7c6737b397708b80598e45c73aeb3bf5";
const string Baseline = "271af03cfefedef12eea19337f1b2b3bca9fe96b";
const string Delivery = "4026e59c827814e52559ec72d41ff282468e3894";
const string WindowSource = "a105eb4854e9f138e409a12bcb041e877f254452";
const string QualitySource = "b99c218e3592551e9bc73d5c509712871607e8bd";
const string Review = "611b56c88a34685f6c50aabebe506cf20d0bc734";
const string WindowStart = "2026-11-12T13:15:25.2643960Z";
const string WindowEnd = "2026-11-12T13:17:20.4729820Z";
const string EventUtc = "2026-11-12T13:17:20Z";
const string Evidence = "docs/accepted-integration-3aff4e2b-publish-receipt.md";

string G17(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

var receipt = JsonNode.Parse(File.ReadAllText(Path.Combine(review, "docs/accepted-integration-independent/receipt.json")))!.AsObject();
if (receipt["calculationCommit"]?.GetValue<string>() != Calculation) throw new InvalidDataException("calculation commit mismatch");
if (receipt["baseline"]?.GetValue<string>() != Baseline) throw new InvalidDataException("baseline mismatch");
if (receipt["nativeTree"]?.GetValue<string>() != Native) throw new InvalidDataException("native tree mismatch");
if (receipt["windowSource"]?.GetValue<string>() != WindowSource) throw new InvalidDataException("window source mismatch");
var measurement = receipt["measurement"]!.AsObject();
if (measurement["runnerExit"]?.GetValue<int>() != 2) throw new InvalidDataException("runner exit must stay 2");
if (measurement["mainComponents"]?["Unresolved"]?.GetValue<int>() != 588) throw new InvalidDataException("mass unresolved accounting mismatch");

var overviewPath = Path.Combine(Path.GetDirectoryName(eventsPath)!, "project-overview.json");
var catalog = JsonNode.Parse(File.ReadAllText(overviewPath))!["catalog"]!.AsArray().OfType<JsonObject>()
    .ToDictionary(c => c["id"]!.GetValue<string>(), StringComparer.Ordinal);
var catalogIdOf = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["sphere"] = "analytic-boolean.equal-overlap-subtract",
    ["planar"] = "planar-boolean.overlap-two-axes-subtract",
    ["curved"] = "regular-curved.block-subtract",
    ["solidblend"] = "solid-blend.plane-nurbs",
    ["shortedge"] = "short-edge.a1-r0.5-t1e-07-reversefalse-default",
};
var operations = measurement["operations"]!.AsArray().OfType<JsonObject>().ToDictionary(o => o["case"]!.GetValue<string>(), StringComparer.Ordinal);
if (operations.Count != 5) throw new InvalidDataException("expected five measured operations");

var events = JsonNode.Parse(File.ReadAllText(eventsPath))!.AsArray();
for (var i = events.Count - 1; i >= 0; i--)
{
    var e = events[i]!.AsObject();
    if (e["kind"]?.GetValue<string>() == "delivery" && e["runId"]?.GetValue<string>() == runId) events.RemoveAt(i);
    else if (e["kind"]?.GetValue<string>() == "case-update" && e["sourceEvent"]?.GetValue<string>() == "accepted-integration-3aff4e2b") events.RemoveAt(i);
}

events.Add((JsonNode)new JsonObject
{
    ["kind"] = "delivery",
    ["runId"] = runId,
    ["label"] = "累计优化集成验收快照：3aff4e2b/854d01fe 单窗口（组合基线 271af03c；全冷暖/两倍率照实，性能资格 false）",
    ["commit"] = Calculation,
    ["utc"] = EventUtc,
    ["note"] = "Accepted cumulative integration window; baseline " + Baseline + "; review " + Review + "; delivery " + Delivery + ".",
});

foreach (var (shape, catalogId) in catalogIdOf)
{
    var target = catalog[catalogId];
    var op = operations[shape];
    var baseline = op["baseline"]!;
    var candidate = op["candidate"]!;
    var gmBefore = baseline["gmMedianMs"]!.GetValue<double>();
    var gmAfter = candidate["gmMedianMs"]!.GetValue<double>();
    var change = op["changePercent"]!.GetValue<double>();
    var paired = candidate["pairedRatioMedian"]!.GetValue<double>();
    var ratioOfMedians = candidate["ratioOfMedians"]!.GetValue<double>();
    var note = "累计优化集成 3aff4e2b 单窗口 " + WindowStart + " .. " + WindowEnd +
               "（Mac M4，20 proc/60 pairs/120 API，16 raw2/4 planar raw0/runner2，runnerPerformanceQualified=false）：" +
               "暖中位 GM " + G17(gmBefore) + "→" + G17(gmAfter) + " ms（" + G17(change) + "%）；" +
               "候选配对 GM/PK ratioMedian " + G17(paired) + " / 中位数之比 " + G17(ratioOfMedians) + "（两口径分列不互换）；" +
               "基线=组合基线 " + Baseline + "（非 Section 基线）；全部冷暖/散布见原 receipt；" +
               "物性仍为旧合同 1080=396V/684U（不借 ab597 新 norm 洗绿）；单窗口描述性观测，不签普遍/因果速度、不跨窗相乘。" +
               (shape == "shortedge" ? " shortedge 为组合窗口 +" + G17(change) + "%。" : "") +
               (shape == "solidblend" ? " solidblend 为本窗口最大回退改善 −" + G17(-change) + "%。" : "");
    events.Add((JsonNode)new JsonObject
    {
        ["kind"] = "case-update",
        ["sourceEvent"] = "accepted-integration-3aff4e2b",
        ["catalogId"] = catalogId,
        ["dimension"] = "performance",
        ["status"] = "observed",
        ["currentVersion"] = "reverified",
        ["lastVerified"] = new JsonObject
        {
            ["commit"] = Calculation,
            ["tree"] = "Native " + Native,
            ["host"] = "Mac M4 / macOS 27.2",
            ["exit"] = "runnerRawExit=2（16 raw2/4 planar raw0；measurement receipt）",
            ["evidence"] = Evidence,
        },
        ["note"] = note,
        ["utc"] = EventUtc,
        ["identity"] = new JsonObject
        {
            ["parameters"] = target["parametersDefinition"]!.DeepClone(),
            ["source"] = target["source"]!.DeepClone(),
        },
    });
}

var tmp = eventsPath + ".tmp";
File.WriteAllText(tmp, events.ToJsonString());
File.Move(tmp, eventsPath, true);
Console.WriteLine("ACCEPTED_INTEGRATION_EVENTS appended=6 runId=" + runId + " window=" + WindowStart + ".." + WindowEnd);
return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
