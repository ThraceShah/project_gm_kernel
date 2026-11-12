#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Globalization;
using System.Runtime.CompilerServices;

using System.Text.Json.Nodes;

// Appends the accepted Section 2c0e4ea performance event to the current
// overview event log. Every number is read from the independent review tree's
// original machine records (operation-summary / measurement receipt); nothing
// is typed in by hand and no other case/dimension is re-signed. Idempotent:
// re-running replaces exactly the events it owns (same utc/runId).
var scripts = Path.GetDirectoryName(ScriptPath())!;
var review = Path.GetFullPath(Option("--review", "../../gm-section-2c0e4ea-review"), scripts);
var eventsPath = Path.GetFullPath(Option("--events", "../web/data/overview-events.json"), scripts);
var runId = Option("--run-id", "");
if (runId.Length == 0) throw new ArgumentException("--run-id is required (the real assembly run id)");

const string Candidate = "2c0e4ea54bc2c4c9e22f24965ce71c41d84635b4";
const string Measurement = "a770a49d07b43dbe05d284f5e11c2d6adfc40fd6";
const string Native = "8fc38005545b10735c3612fdf6bd57460fae19af";
const string Src = "e8b7750c97e018ec6508a94fa8132817982158ff";
const string Baseline = "553b2c6c01d3862411f3b6a03735dc22ee20dc11";
const string Review = "24c54edc299fd8fdcc7ec3c53542fca4eca4ebcc";
const string WindowStart = "2026-11-12T06:56:45.5438090Z";
const string WindowEnd = "2026-11-12T06:58:43.1836750Z";
const string EventUtc = "2026-11-12T06:58:43Z";
const string Evidence = "docs/section-2c0e4ea-publish-receipt.md";

string G17(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

var summaryPath = Path.Combine(review, "docs/section-controlled/operation-summary.json");
var receiptPath = Path.Combine(review, "docs/section-controlled/measurement-receipt.json");
var summary = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
var receipt = JsonNode.Parse(File.ReadAllText(receiptPath))!.AsObject();

// The review receipt binds the original report; re-check the two machine files
// we derive from are the ones the review accepted.
if (receipt["runnerPerformanceQualified"]?.GetValue<bool>() != false) throw new InvalidDataException("review receipt must keep runnerPerformanceQualified=false");
if (summary["candidateMeasurementCommit"]?.GetValue<string>() != Measurement) throw new InvalidDataException("candidate measurement commit mismatch");
if (summary["baselineCommit"]?.GetValue<string>() != Baseline) throw new InvalidDataException("baseline commit mismatch");
if (summary["sources"]?["candidate"]?["commit"]?.GetValue<string>() != Measurement || summary["sources"]?["candidate"]?["nativeTree"]?.GetValue<string>() != Native || summary["sources"]?["candidate"]?["srcTree"]?.GetValue<string>() != Src) throw new InvalidDataException("candidate source layers mismatch");

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
var operations = summary["operations"]!.AsArray().OfType<JsonObject>().ToDictionary(o => o["case"]!.GetValue<string>(), StringComparer.Ordinal);
if (operations.Count != 5) throw new InvalidDataException("expected five measured Section operations");

var events = JsonNode.Parse(File.ReadAllText(eventsPath))!.AsArray();
// Drop the events this script owns (same utc + same runId delivery).
for (var i = events.Count - 1; i >= 0; i--)
{
    var e = events[i]!.AsObject();
    var kind = e["kind"]?.GetValue<string>();
    if (kind == "delivery" && e["runId"]?.GetValue<string>() == runId) events.RemoveAt(i);
    else if (kind == "case-update" && e["utc"]?.GetValue<string>() == EventUtc && e["sourceEvent"]?.GetValue<string>() == "section-2c0e4ea") events.RemoveAt(i);
}

events.Add((JsonNode)new JsonObject
{
    ["kind"] = "delivery",
    ["runId"] = runId,
    ["label"] = "Section 性能验收快照：2c0e4ea5/8fc38005 单窗口（rep2 模型/双 ratio/两回退照实，性能资格 false）",
    ["commit"] = Candidate,
    ["utc"] = EventUtc,
    ["note"] = "Section 2c0e4ea accepted performance window; baseline " + Baseline + "; review " + Review + ".",
});

foreach (var (shape, catalogId) in catalogIdOf)
{
    var target = catalog[catalogId];
    var op = operations[shape];
    var baseline = op["baseline"]!;
    var candidate = op["candidate"]!;
    var gmBefore = baseline["warmGmMedianMs"]!.GetValue<double>();
    var gmAfter = candidate["warmGmMedianMs"]!.GetValue<double>();
    var change = op["changePercent"]!.GetValue<double>();
    var paired = candidate["warmPairedGmPkMedian"]!.GetValue<double>();
    var ratioOfMedians = candidate["ratioOfWarmMedians"]!.GetValue<double>();
    var note = "Section 2c0e4ea 单窗口 " + WindowStart + " .. " + WindowEnd +
               "（Mac M4，20 proc/60 pairs/120 API，4 planar raw0/16 other raw2/runner2，runnerPerformanceQualified=false）：" +
               "暖中位 GM " + G17(gmBefore) + "→" + G17(gmAfter) + " ms（" + G17(change) + "%）；" +
               "候选配对 GM/PK ratioMedian " + G17(paired) + " / 中位数之比 " + G17(ratioOfMedians) + "（两口径分列不互换）；" +
               "全部冷暖/minmax/逐 rep 见原 operation-summary；两回退（curved +" + G17(operations["curved"]["changePercent"]!.GetValue<double>()) +
               "%、solidblend +" + G17(operations["solidblend"]["changePercent"]!.GetValue<double>()) +
               "%）保留；单窗口描述性观测，不签普遍/因果速度、不跨窗相乘。" +
               (shape == "shortedge" ? " 来源=PK seed→GM receive→GM Fix。" : "") +
               (shape == "curved" ? " curved roles/tracking 原缺失，中性保留。" : "");
    events.Add((JsonNode)new JsonObject
    {
        ["kind"] = "case-update",
        ["sourceEvent"] = "section-2c0e4ea",
        ["catalogId"] = catalogId,
        ["dimension"] = "performance",
        ["status"] = "observed",
        ["currentVersion"] = "reverified",
        ["lastVerified"] = new JsonObject
        {
            ["commit"] = Candidate,
            ["tree"] = "Native " + Native,
            ["host"] = "Mac M4 / macOS 27.2",
            ["exit"] = "runnerRawExit=2（measurement-receipt.json；4 planar raw0/16 other raw2）",
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
Console.WriteLine("SECTION_EVENTS appended=6 runId=" + runId + " operations=5 utc=" + EventUtc);
return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
