#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the accepted Projection cc47871 acceptance snapshot into the web-progress-publish data directory.
// Uses real candidate rep2 meshes (5 operations: 01-sphere, 05-planar, 09-curved, 13-solidblend, 17-shortedge),
// preserves raw exits and measurement timings, maintains all cold/warm timings and both ratio definitions.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts); Directory.SetCurrentDirectory(root);

const string Candidate = "cc47871133fb7d536ffa7fd050066c51f4905708";
const string CandidateNative = "fe3fa0378ef6d9c0dd971de4c993a289cdb8a34b";
const string CandidateSrc = "31be8f6c46766bb3b60b0e402bfc043426ed58fb";
const string Baseline = "17eedbfd6be891db8c43494cd91a896d261242c4";
const string BaselineNative = "116";
const string WindowReportSha = "6568f1d9d19bbf0df0f9eeb07dc2afac1f5990599535b2f5caa0dbaf40d9e90e";
const string MaterialBindingCurrentSha = "3678da6d62f44e00e8e6419af0709b25d73c43f3cfdd50a0a8c7c75e916d0306";
const string Delivery = "553b2c6c01d3862411f3b6a03735dc22ee20dc11";
const string Review = "7b20d7f1046ad5b6f54f8601edd739fa5d54a901";
const string ZipQualitySha = "c1f1b90ff6f8eed87d23cb960b16e3502eab77211f0d1d13bdce7f14812702b8";
const string ZipControlledSha = "9bfad64ff79211c4fbc6620e3a2678e3f966e8150d5b3dad778e5814c8128919";

var reviewTree = Path.GetFullPath(Option("--review", "../../gm-projection-cc47871-review"), scripts);
var pkgControlled = Path.Combine(reviewTree, "docs/projection-controlled");
var pkgQuality = Path.Combine(reviewTree, "docs/projection-independent");
var pkgRaw = Path.Combine(reviewTree, "temp_docs/independent-projection-cc47871/measurement/original");
var output = Path.GetFullPath(Option("--output", "/home/thrace/code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/data"), scripts);

var now = DateTimeOffset.UtcNow;
var runId = Option("--run-id", "20261112T045837Z-projcc47871");
var collectorSha = Command("git", ["rev-parse", "HEAD"]);

void Require(bool condition, string message) { if (!condition) throw new InvalidDataException("IDENTITY " + message); }
string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(pkgControlled, "operation-summary.json")))!.AsObject();
var receipt = JsonNode.Parse(File.ReadAllText(Path.Combine(pkgControlled, "measurement-receipt.json")))!.AsObject();
var bindingCurrent = JsonNode.Parse(File.ReadAllText(Path.Combine(pkgRaw, "temp_docs/projection-controlled/material-binding-current.json")))!.AsObject();
var rawReportPath = Path.Combine(pkgRaw, "temp_docs/projection-controlled/window/report.json");

Require(receipt["originalReportSha256"]!.GetValue<string>() == Hash(File.ReadAllBytes(rawReportPath)), "receipt binds raw report");
Require(receipt["originalReportSha256"]!.GetValue<string>() == WindowReportSha, "window report sha matches constant");
Require(receipt["runnerPerformanceQualified"]!.GetValue<bool>() == false, "performanceQualified stays false");
Require(bindingCurrent["meshFiles"]!.GetValue<int>() == 312 && bindingCurrent["meshBytes"]!.GetValue<long>() == 35389380L, "material binding counts");

var ops = summary["operations"]!.AsArray().OfType<JsonObject>().ToDictionary(o => o["case"]!.GetValue<string>(), StringComparer.Ordinal);
Require(ops.Count == 5, "five measured operations");

var run = Path.Combine(output, "runs", runId);
if (Directory.Exists(run)) Directory.Delete(run, true);
var evidenceDir = Path.Combine(run, "evidence"); Directory.CreateDirectory(evidenceDir);
var evidenceManifest = new JsonArray();

void Ship(string sourcePath, string shipped)
{
    var bytes = File.ReadAllBytes(sourcePath);
    File.WriteAllBytes(Path.Combine(evidenceDir, shipped), bytes);
    evidenceManifest.Add((JsonNode)new JsonObject
    {
        ["original"] = Path.GetFileName(sourcePath),
        ["shipped"] = "evidence/" + shipped,
        ["bytes"] = bytes.Length,
        ["sha256"] = Hash(bytes)
    });
}

Ship(Path.Combine(pkgControlled, "operation-summary.json"), "operation-summary.json");
Ship(Path.Combine(pkgControlled, "measurement-receipt.json"), "measurement-receipt.json");
Ship(Path.Combine(pkgControlled, "source-binary-binding.json"), "source-binary-binding.json");
Ship(Path.Combine(pkgRaw, "temp_docs/projection-controlled/material-binding-current.json"), "material-binding-current.json");
Ship(rawReportPath, "report.json");
Ship(Path.Combine(pkgQuality, "receipt.json"), "quality-receipt.json");
Ship(Path.Combine(pkgQuality, "material-bindings.json"), "quality-material-bindings.json");

// ---- rep-2 display import per candidate processes: 01, 05, 09, 13, 17
var candDirs = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["sphere"] = "01-sphere-candidate",
    ["planar"] = "05-planar-candidate",
    ["curved"] = "09-curved-candidate",
    ["solidblend"] = "13-solidblend-candidate",
    ["shortedge"] = "17-shortedge-candidate"
};

var displayBinding = new JsonArray();
var groupSteps = new Dictionary<string, JsonArray>(StringComparer.Ordinal);
string CaseIdOf(string c) => "projection." + c;

foreach (var c in new[] { "sphere", "planar", "curved", "solidblend", "shortedge" })
{
    var caseId = CaseIdOf(c);
    var dirName = candDirs[c];
    var sample = bindingCurrent["samples"]!.AsArray().OfType<JsonObject>().First(s => s["directory"]!.GetValue<string>() == dirName);
    var rep2 = sample["meshes"]!.AsArray().OfType<JsonObject>().Where(m => m["step"]!.GetValue<string>() == "operation-3").ToArray();
    var prefix = $"cases/{caseId}/sample-0/{dirName}";
    var shipped = new JsonArray();
    var step = new JsonObject();
    step["id"] = "rep2-display";
    step["title"] = dirName + " rep 2 显示样本（material-binding-current 原字节；单 rep2 显示≠60 pairs 统计）" +
                    (c == "shortedge" ? "；来源=PK seed→GM receive→GM Fix（不称 GM 独立构造）" : "") +
                    (c == "curved" ? "；curved roles/tracking missing 如实中性" : "");
    step["gm"] = new JsonObject { ["tools"] = new JsonArray() };
    step["pk"] = new JsonObject { ["tools"] = new JsonArray() };

    foreach (var m in rep2)
    {
        var relSrc = m["path"]!.GetValue<string>();
        var src = Path.Combine(pkgRaw, relSrc);
        var bytes = File.ReadAllBytes(src);
        Require(Hash(bytes) == m["sha256"]!.GetValue<string>() && bytes.Length == m["bytes"]!.GetValue<long>(), "mesh identity " + relSrc);
        var name = Path.GetFileName(src);
        var target = Path.Combine(run, prefix, "meshes", name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(src, target);
        var stage = name.Contains("-before") ? "before" : name.Contains("-after") ? "after" : "tool";
        var side = m["kernel"]!.GetValue<string>();
        shipped.Add((JsonNode)new JsonObject
        {
            ["file"] = prefix + "/meshes/" + name,
            ["stage"] = JsonValue.Create(stage),
            ["kernel"] = JsonValue.Create(side),
            ["sha256"] = JsonValue.Create(Hash(bytes)),
            ["bytes"] = JsonValue.Create(bytes.Length)
        });
        if (stage == "before") step[side]!["before"] = JsonValue.Create(prefix + "/meshes/" + name);
        else if (stage == "after") step[side]!["after"] = JsonValue.Create(prefix + "/meshes/" + name);
        else ((JsonArray)step[side]!["tools"]!).Add((JsonNode?)JsonValue.Create(prefix + "/meshes/" + name));
    }
    groupSteps[c] = new JsonArray((JsonNode)step);
    displayBinding.Add((JsonNode)new JsonObject
    {
        ["case"] = JsonValue.Create(caseId),
        ["process"] = JsonValue.Create(dirName),
        ["rep"] = JsonValue.Create(2),
        ["qualityStatus"] = sample["qualityStatus"]?.DeepClone(),
        ["rawExit"] = sample["rawExit"]?.DeepClone(),
        ["captureSha256"] = sample["captureSha256"]?.DeepClone(),
        ["meshes"] = shipped,
        ["finalRepetitionArtifacts"] = sample["finalRepetitionArtifacts"]?.DeepClone(),
        ["note"] = "同实例真实 DisplayBody 原字节；单 rep2 显示≠60 pairs 统计；64 最终文件集角色见 material-binding-current；质量开发 NURBS 成品是 rep5，不与正式 rep2 串；24 份 native-via-managed 额外 GM body 未采主 display，不能绑主 GM mesh。"
    });
}
File.WriteAllText(Path.Combine(evidenceDir, "display-binding.json"), new JsonObject
{
    ["schemaVersion"] = 1,
    ["note"] = "Projection rep-2 display binding per material-binding-current roles; exact candidate processes 01, 05, 09, 13, 17.",
    ["groups"] = displayBinding
}.ToJsonString());

// ---- host & config
var host = new JsonObject();
host["os"] = "macOS 27.2.0"; host["architecture"] = "Arm64"; host["cpu"] = "Apple M4"; host["sdk"] = "10.0.400"; host["runtime"] = ".NET 10.0.11"; host["pk"] = "Parasolid 38.0.150";
var configuration = new JsonObject();
configuration["build"] = "Release"; configuration["nativeAot"] = false;
configuration["protocol"] = JsonValue.Create("ABBA sources per case; 3 fresh-input pairs per process; rep0 cold, reps1/2 warm; single window; 20 processes 60 pairs 120 API measurements");
configuration["windowUtc"] = "2026-11-12T04:56:39.7493270Z .. 2026-11-12T04:58:37.0945730Z";
configuration["systemLoadObserved"] = "3.72 .. 4.30 (one-minute load; powermetrics raw/no numeric temperature; pmset warning state; no exclusive CPU/thermal claims)";

JsonObject Check(string id, string label, string status, double? gm, double? pk, double? threshold, string unit, string note, string? evidence = null)
{
    var o = new JsonObject(); o["id"] = id; o["label"] = label; o["status"] = status; o["gm"] = gm; o["pk"] = pk;
    o["absDelta"] = gm.HasValue && pk.HasValue ? Math.Abs(gm.Value - pk.Value) : null;
    o["normalizedDelta"] = gm.HasValue && pk.HasValue ? Math.Abs(gm.Value - pk.Value) / (1 + Math.Abs(pk.Value)) : null;
    o["threshold"] = threshold; o["unit"] = unit; o["evidence"] = evidence; o["note"] = note; return o;
}

JsonNode Perf(double gmMs, double pkMs)
{
    var o = new JsonObject(); o["gmMs"] = JsonValue.Create(gmMs); o["pkMs"] = JsonValue.Create(pkMs); o["ratioGmOverPk"] = null;
    o["samples"] = JsonValue.Create(4); o["warmup"] = JsonValue.Create(0);
    o["scope"] = JsonValue.Create("headline 取候选 4 个暖样本（每源总 6=2 冷+4 暖全保留）；两 ratio 口径（配对 GM/PK 中位数与中位数之比）在 checks 分别显示不得互换，故 performance.ratioGmOverPk=null；performanceQualified=false 原样，不签普遍/因果速度。");
    o["correctnessQualified"] = false; o["host"] = host.DeepClone(); o["configuration"] = configuration.DeepClone();
    o["note"] = JsonValue.Create("单窗口描述性观测；不跨窗相乘；planar 回退（+78.105%）必须保留。");
    o["measurementKind"] = "controlled-abba-warm-median"; o["stableBenchmark"] = false;
    o["qualifiedScope"] = JsonValue.Create("Five fixed operations, one controlled window; descriptive observation only.");
    return o;
}

JsonNode NullPerf()
{
    var o = new JsonObject(); o["gmMs"] = null; o["pkMs"] = null; o["ratioGmOverPk"] = null; o["samples"] = JsonValue.Create(0); o["warmup"] = JsonValue.Create(0);
    o["scope"] = JsonValue.Create("报告/质量类：无耗时测量。"); o["correctnessQualified"] = false; o["host"] = host.DeepClone(); o["configuration"] = configuration.DeepClone();
    o["note"] = JsonValue.Create("不推导倍率。"); o["measurementKind"] = "not-run"; o["stableBenchmark"] = false; o["qualifiedScope"] = JsonValue.Create("Evidence report only.");
    return o;
}

JsonNode NoModelNote() => JsonValue.Create("报表/诊断类：无实际模型显示，不造模型；不用旧源模型补。");
JsonNode WindowNote() => JsonValue.Create("单窗口 UTC 2026-11-12T04:56:39.7493270Z..04:58:37.0945730Z（Mac M4）；load 3.72–4.30；powermetrics 原/无数值温度、pmset 警告状态；不签独占 CPU/热稳定/普遍或因果速度/PK 目标。");
JsonNode LimitsNote() => JsonValue.Create("限定保留：五圆柱查询 5000 缺口、FaceAttach 未测、curved roles/tracking 缺失、物性未决（684U）、性能目标未完成（shortedge 331.97x/solidblend 253.85x）；2022 套件通过不抹这些。");
JsonNode RatioNote() => JsonValue.Create("两口径分列：配对 GM/PK ratioMedian 与 ratioOfMedians 不得互换；实值按机器文件 G17；全部冷/暖 minmax 与 120 原时间在 report.json/operation-summary。");

var evCore = new JsonArray();
evCore.Add((JsonNode)new JsonObject { ["label"] = JsonValue.Create("原 operation-summary/measurement-receipt/source-binary-binding（原字节）"), ["path"] = JsonValue.Create("evidence/operation-summary.json") });
evCore.Add((JsonNode)new JsonObject { ["label"] = JsonValue.Create("material-binding-current（正确角色，原字节）"), ["path"] = JsonValue.Create("evidence/material-binding-current.json") });
evCore.Add((JsonNode)new JsonObject { ["label"] = JsonValue.Create("原 report（全部 120 G17 时间，原字节）"), ["path"] = JsonValue.Create("evidence/report.json") });
evCore.Add((JsonNode)new JsonObject { ["label"] = JsonValue.Create("显示绑定（rep2 角色/哈希/64 文件集说明）"), ["path"] = JsonValue.Create("evidence/display-binding.json") });
evCore.Add((JsonNode)new JsonObject { ["label"] = JsonValue.Create("质量收据（独审原字节）"), ["path"] = JsonValue.Create("evidence/quality-receipt.json") });
evCore.Add((JsonNode)new JsonObject { ["label"] = JsonValue.Create("来源/归档/回执"), ["path"] = JsonValue.Create("evidence/run-receipt.json") });

var cases = new JsonArray();
void AddCase(string id, string title, string status, JsonArray steps, JsonArray checks, JsonArray evidence, JsonArray notes, JsonNode perf, string sourcePath, int rawExit)
{
    var c = new JsonObject();
    c["id"] = id; c["title"] = title; c["family"] = "boolean"; c["operation"] = "projection performance (cc47871 vs 17eed baseline)"; c["status"] = status;
    var parameters = new JsonObject();
    parameters["candidate"] = JsonValue.Create(Candidate); parameters["candidateNative"] = JsonValue.Create(CandidateNative);
    parameters["candidateSrc"] = JsonValue.Create(CandidateSrc); parameters["baseline"] = JsonValue.Create(Baseline);
    parameters["acceptanceReview"] = JsonValue.Create("../gm-projection-cc47871-review/docs/Native_Projection_Independent_Review.md @ " + Review);
    c["parameters"] = parameters;
    var apis = new JsonObject();
    apis["gm"] = new JsonObject { ["name"] = JsonValue.Create("projection ops per capture"), ["parameters"] = new JsonObject { { "srcTree", JsonValue.Create(CandidateSrc) }, { "nativeTree", JsonValue.Create(CandidateNative) } } };
    apis["pk"] = new JsonObject { ["name"] = JsonValue.Create("real PK 38.0.150 (Mac)"), ["parameters"] = new JsonObject { { "oracle", JsonValue.Create("identical numeric parameters") } } };
    c["apis"] = apis; c["steps"] = steps; c["checks"] = checks; c["performance"] = perf; c["evidence"] = evidence; c["notes"] = notes;
    c["source"] = new JsonObject { ["path"] = JsonValue.Create(sourcePath), ["revision"] = JsonValue.Create(Candidate) };
    var exits = new JsonArray(); exits.Add((JsonNode?)JsonValue.Create(rawExit));
    c["execution"] = new JsonObject { ["selected"] = true, ["attempted"] = true, ["exitCode"] = JsonValue.Create(rawExit), ["exitCodes"] = exits, ["timedOut"] = false };
    cases.Add((JsonNode)c);
}

foreach (var c in new[] { "sphere", "planar", "curved", "solidblend", "shortedge" })
{
    var o = ops[c]; var b = o["baseline"]!; var ca = o["candidate"]!;
    var checks = new JsonArray();
    string Row(JsonNode src)
    {
        var warm = src["warmGmSamplesMs"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        var wpk = src["warmPkSamplesMs"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        return "n=4 GM[" + string.Join(",", warm.Select(v => v.ToString("G17", System.Globalization.CultureInfo.InvariantCulture))) + "] 中位 " + src["warmGmMedianMs"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + " [" + src["warmGmMinMs"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "," + src["warmGmMaxMs"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "] PK中位 " + src["warmPkMedianMs"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
    }
    checks.Add(Check("warm-" + c, "暖样本（每源 4）：基线 " + Row(b) + "；候选 " + Row(ca), "pass", ca["warmGmMedianMs"]!.GetValue<double>(), b["warmGmMedianMs"]!.GetValue<double>(), null, "ms", "全部原值/离群照实（sphere -5.626%、planar 回退 +" + (o["changePercent"]!.GetValue<double>()).ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "% 必须保留）；不归因", "evidence/operation-summary.json"));
    checks.Add(Check("cold-" + c, "冷样本（每源 2）：基线 GM[" + string.Join(",", b["coldGmSamplesMs"]!.AsArray().Select(x => x!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture))) + "] PK[" + string.Join(",", b["coldPkSamplesMs"]!.AsArray().Select(x => x!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture))) + "]；候选 GM[" + string.Join(",", ca["coldGmSamplesMs"]!.AsArray().Select(x => x!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture))) + "] PK[" + string.Join(",", ca["coldPkSamplesMs"]!.AsArray().Select(x => x!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture))) + "]", "diagnostic", null, null, null, "ms", "冷 rep0 保留不丢弃", "evidence/operation-summary.json"));
    var ratioOfMedians = ca["warmGmMedianMs"]!.GetValue<double>() / ca["warmPkMedianMs"]!.GetValue<double>();
    checks.Add(Check("ratios-" + c, "候选配对 GM/PK ratioMedian " + ca["warmPairedGmPkMedian"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "；中位数之比 " + ratioOfMedians.ToString("G17", System.Globalization.CultureInfo.InvariantCulture), "diagnostic", ca["warmPairedGmPkMedian"]!.GetValue<double>(), ratioOfMedians, null, "x", "两口径分列不得互换；performance.ratioGmOverPk=null 因 performanceQualified=false", "evidence/operation-summary.json"));
    checks.Add(Check("change-" + c, "GM 暖变化 " + o["changePercent"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "%；观测加速 " + o["observedSpeedup"]!.GetValue<double>().ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + "x", "diagnostic", o["changePercent"]!.GetValue<double>(), null, null, "%", "描述性观测，不说普遍/因果速度", null));
    var qsc = ca["qualityStatusCounts"]?.ToJsonString();
    checks.Add(Check("quality-" + c, "该操作候选质量状态计数（原样）：" + (qsc ?? "null"), "diagnostic", null, null, null, "", "主 60 compare 900 分量 312 Verified/588 Unresolved/0 Different；详见 projection.mass-quality", "evidence/material-binding-current.json"));
    checks.Add(Check("exit-" + c, "候选进程原 exit " + ca["rawExit"]?.ToJsonString() + "（2 进程×3 对 API 全 0）", "diagnostic", null, null, null, "exit code", "planar 4 进程 raw0/Verified；其余 raw2/Unresolved；主 runner 2", null));

    var notes = new JsonArray();
    notes.Add(WindowNote()); notes.Add(RatioNote()); notes.Add(LimitsNote());
    if (c == "shortedge") notes.Add((JsonNode?)JsonValue.Create("来源纠正：PK seed→GM receive→GM Fix→GM XT；不称 GM 独立构造。"));
    if (c == "curved") notes.Add((JsonNode?)JsonValue.Create("curved roles/tracking missing 中性保留，不补造。"));
    notes.Add((JsonNode?)JsonValue.Create("显示绑定见 evidence/display-binding.json；单 rep2 显示≠60 pairs；64 最终文件集角色见 material-binding-current；质量开发 NURBS 成品是 rep5，不与正式 rep2 串；24 份 native-via-managed 额外 GM body 未采主 display，不能绑主 GM mesh。"));
    AddCase(CaseIdOf(c), "projection / " + c + "（冷暖全值/双 ratio 分列；rep2 真实模型）", c == "planar" ? "fail" : "pass", (JsonArray)groupSteps[c].DeepClone(), checks, (JsonArray)evCore.DeepClone(), notes, Perf(ca["warmGmMedianMs"]!.GetValue<double>(), ca["warmPkMedianMs"]!.GetValue<double>()), "scripts/NativeModelingWorkComparison.cs", ca["rawExit"]?.GetValue<int>() ?? 2);
}

// quality accounting case
var qChecks = new JsonArray();
qChecks.Add(Check("mass-main", "主 60 compare 的 900 mass 分量：312 Verified / 588 Unresolved / 0 Different", "missing", 588, 900, 0, "components", "missing 不绿；不写 900 全 Verified", "evidence/quality-receipt.json"));
qChecks.Add(Check("mass-extra", "额外 shortedge 12 个 native-via-managed 回读控制 180 分量（独立列：84V/96U）", "diagnostic", 180, 180, 0, "components", "side/API 失败与数值未决分开", "evidence/quality-receipt.json"));
qChecks.Add(Check("mass-total", "唯一总账：72 报告 1080 分量=396 Verified / 684 Unresolved / 0 Different（不重复计数、不以 15 summary 再引用计数）", "missing", 684, 1080, 0, "components", "原合同 72 唯一 1080 分量 396V/684U/0D；不代签新物性标准", "evidence/quality-receipt.json"));
qChecks.Add(Check("quality-contract", "quality JSON 保留 estimate/modulus/bounds/status/mask/rawdiff/conditionalfiniteproof；ComparedRejected 等不改 diagnostic 豁免", "pass", null, null, null, "", "performanceQualified=false 原字节不改；独审仅接受描述性观测", "evidence/measurement-receipt.json"));
AddCase("projection.mass-quality", "质量分层：396 Verified/684 Unresolved（不重复计数，missing 不绿）", "missing", new JsonArray(), qChecks, (JsonArray)evCore.DeepClone(), new JsonArray(NoModelNote(), LimitsNote()), NullPerf(), "docs/projection-controlled/measurement-receipt.json", 2);

// suite case
var sChecks = new JsonArray();
sChecks.Add(Check("suite-2022", "Mac Projection cc47871 套件 2022 total / 2022 pass / 0 fail / 0 skip，原 raw 0（4m44s）", "pass", 2022, 2022, 0, "tests", "Mac 2022/2022 raw build0/test0 通过；只属 cc47871", "evidence/quality-receipt.json"));
sChecks.Add(Check("suite-history", "历史套件分列：Joint185 2019/2019、1654 1998/2000 原 1 保留，不回写覆盖", "diagnostic", null, null, null, "", "历史保持原字节", "evidence/quality-receipt.json"));
AddCase("projection.mac-suite", "Mac 套件 2022/2022 全过（原 raw 0，耗时 4m44s）", "pass", new JsonArray(), sChecks, (JsonArray)evCore.DeepClone(), new JsonArray(NoModelNote(), LimitsNote()), NullPerf(), "docs/projection-independent/receipt.json", 0);

// eight-groups report case
AddCase("projection.eight-groups", "八组 Projection cc47871 当前实体/lifecycle（报告类，无模型）", "diagnostic", new JsonArray(),
    new JsonArray(
        Check("groups-eight", "八组几何拓扑限定接受：rigid 原 0 且 1 missing；另 7 组原 2、missing 按独审表", "diagnostic", 8, 8, 0, "groups", "报告/展示范围说明，不用旧源模型补", "evidence/run-receipt.json"),
        Check("groups-missing", "7raw2/rigid0 有 missing 照实记录，不代签全绿", "diagnostic", null, null, null, "", "照实", null)),
    (JsonArray)evCore.DeepClone(), new JsonArray(NoModelNote(), LimitsNote()), NullPerf(), "docs/Native_Projection_Independent_Review.md", 2);

// overall
AddCase("projection.overall", "总体：单窗口描述性观测接受，回退/未决/边界照实", "fail", new JsonArray(),
    new JsonArray(
        Check("overall-accepted", "接受=cc478711 窗口代码/质量与同窗描述性观测（7b20d7f1 独审限定范围）；不签全绿/数值全 Verified/普遍因果速度/PK 目标", "pass", null, null, null, "", "", "evidence/run-receipt.json"),
        Check("overall-regressions", "planar 暖 +78.105% 回退必须保留；shortedge 331.97x；不归因", "fail", 1, 0, 0, "regressions", "", "evidence/operation-summary.json"),
        Check("overall-limits", "五圆柱查询 5000/FaceAttach/curved roles/物性未决（684U）/性能目标未达；performanceQualified=false", "missing", null, null, null, "", "2022 套件通过不抹这些", null)),
    (JsonArray)evCore.DeepClone(), new JsonArray(NoModelNote(), WindowNote(), LimitsNote()), NullPerf(), "docs/projection-controlled/operation-summary.json", 2);

// ---- manifest + receipt
var runObj = new JsonObject();
runObj["id"] = runId; runObj["commit"] = JsonValue.Create(Candidate); runObj["generatedAt"] = JsonValue.Create(now.ToString("O")); runObj["pkVersion"] = JsonValue.Create("38.0.150");
runObj["host"] = host.DeepClone(); runObj["configuration"] = configuration.DeepClone();
var scope = new JsonObject();
scope["kind"] = "projection-acceptance-snapshot"; scope["candidate"] = JsonValue.Create(Candidate); scope["baseline"] = JsonValue.Create(Baseline);
scope["acceptanceReview"] = JsonValue.Create(Review); scope["registered"] = JsonValue.Create(cases.Count); scope["executed"] = JsonValue.Create(cases.Count);
scope["note"] = JsonValue.Create("cc478711/fe3fa037 vs 17eedbfd 单窗口（metadata 6568f1d9、交付 553b2c6c）；5 操作 rep2 模型+全部冷暖原值；双 ratio 分列；396V/684U；Mac 2022 套件 0 fail 全过；planar 回退（+78.105%）照实。");
runObj["scope"] = scope;

var prov = new JsonObject();
prov["projectionSource"] = new JsonObject { { "candidate", JsonValue.Create(Candidate) }, { "candidateNative", JsonValue.Create(CandidateNative) }, { "candidateSrc", JsonValue.Create(CandidateSrc) }, { "baseline", JsonValue.Create(Baseline) }, { "baselineNative", JsonValue.Create(BaselineNative) }, { "note", JsonValue.Create("计算含 Projection bound work；非旧分离性能。") } };
prov["windowMetadata"] = JsonValue.Create(WindowReportSha);
prov["measurementDelivery"] = JsonValue.Create(Delivery);
prov["acceptanceReview"] = new JsonObject { { "tree", JsonValue.Create("gm-projection-cc47871-review") }, { "commit", JsonValue.Create(Review) }, { "path", JsonValue.Create("docs/Native_Projection_Independent_Review.md") } };
prov["collectorSource"] = new JsonObject { { "tree", JsonValue.Create("gm-delivery-board") }, { "commit", JsonValue.Create(collectorSha) }, { "script", JsonValue.Create("scripts/AssembleProjectionAcceptance.cs") }, { "note", JsonValue.Create("仅装配/展示；无新 Capture/PK/积分/fullsuite/ABBA") } };
prov["packages"] = new JsonObject { { "qualityZip", JsonValue.Create(ZipQualitySha) }, { "controlledZip", JsonValue.Create(ZipControlledSha) }, { "note", JsonValue.Create("zip/DLL/PDB/XT/vendor 不公开；大 source/日志私有 hash 锚定，关键原 exit/日志摘要可读") } };
prov["materialBinding"] = JsonValue.Create("material-binding-current.json 正确角色；未改 raw/XT/计时。显示单 rep2≠60 pairs 统计；质量开发 NURBS 成品是 rep5，不与正式 rep2 串；24 份 native-via-managed 额外 GM body 未采主 display，不能绑主 GM mesh。");
prov["qualityAccounting"] = "原合同 72 唯一 1080 分量 396V/684U/0D（主 900 分量 312V/588U + 额外 180 分量 84V/96U）；不重复计数；missing 不绿。";
prov["suite"] = "Mac 2022/2022 全过 raw 0（4m44s）；历史 Joint185 2019/2019、1654 1998/2000 原 1 保留，不回写覆盖。";
prov["performanceScope"] = "performanceQualified=false 原字节；独审仅有限描述性观测；两 ratio 口径分列；不跨窗相乘；不签普遍/因果速度/PK 目标。";
prov["limitsNote"] = "五圆柱查询 5000/FaceAttach/curved roles/物性未决（684U）/性能目标未达；performanceQualified=false。";
var csd = new JsonObject();
csd["pass"] = "该 case 现行必需检查通过，仅限所列范围。"; csd["fail"] = "存在必需检查失败/回退/未完成目标。"; csd["missing"] = "缺失/未决不转绿。"; csd["diagnostic"] = "诊断/观察/含未决；不是无条件验收通过。";
prov["caseStatusDefinitions"] = csd;
runObj["provenance"] = prov;

var manifest = new JsonObject();
manifest["schemaVersion"] = 1; manifest["run"] = runObj; manifest["cases"] = cases;
File.WriteAllText(Path.Combine(run, "manifest.json"), manifest.ToJsonString());

// ---- update index.json
var indexPath = Path.Combine(output, "index.json");
var index = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
index["generatedAt"] = JsonValue.Create(now.ToString("O"));
index["latestRunId"] = JsonValue.Create(runId);
var runsArr = index["runs"]!.AsArray();
// Remove if already present (for idempotent rerun)
for (int i = runsArr.Count - 1; i >= 0; i--) if (runsArr[i]!["id"]!.GetValue<string>() == runId) runsArr.RemoveAt(i);
runsArr.Insert(0, (JsonNode)new JsonObject
{
    ["id"] = JsonValue.Create(runId),
    ["commit"] = JsonValue.Create(Candidate),
    ["generatedAt"] = JsonValue.Create(now.ToString("O")),
    ["label"] = JsonValue.Create("Projection 验收快照：cc478711/fe3fa037 单窗口（rep2 模型/双 ratio/回退照实，性能资格 false）"),
    ["manifest"] = JsonValue.Create($"runs/{runId}/manifest.json")
});
File.WriteAllText(indexPath, index.ToJsonString());

var runBytes = Directory.EnumerateFiles(run, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
var receiptObj = new JsonObject();
receiptObj["schemaVersion"] = 1; receiptObj["runId"] = JsonValue.Create(runId); receiptObj["generatedAt"] = JsonValue.Create(now.ToString("O")); receiptObj["collectorCommit"] = JsonValue.Create(collectorSha);
receiptObj["layers"] = new JsonObject { { "candidate", JsonValue.Create(Candidate) }, { "native", JsonValue.Create(CandidateNative) }, { "delivery", JsonValue.Create(Delivery) }, { "review", JsonValue.Create(Review) }, { "qualityZip", JsonValue.Create(ZipQualitySha) }, { "controlledZip", JsonValue.Create(ZipControlledSha) } };
receiptObj["originalExits"] = new JsonObject { { "mainRunner", 2 }, { "planar4Processes", 0 }, { "other16Processes", 2 }, { "suite", 0 }, { "recordingDriver", 0 } };
receiptObj["runBytesBeforeReceiptAndValidation"] = JsonValue.Create((double)runBytes);
receiptObj["evidenceFiles"] = evidenceManifest;
receiptObj["validation"] = JsonValue.Create("see validation.json");
File.WriteAllText(Path.Combine(evidenceDir, "run-receipt.json"), receiptObj.ToJsonString());

var validation = WebProgressDataValidation.Validate(output);
File.WriteAllText(Path.Combine(run, "validation.json"), validation.ToJsonString());
Console.WriteLine("RUN " + runId + " cases=" + cases.Count + " runBytes=" + (runBytes + new FileInfo(Path.Combine(evidenceDir, "run-receipt.json")).Length + new FileInfo(Path.Combine(run, "validation.json")).Length) + " validationErrors=" + validation["errors"]!.AsArray().Count);
return validation["errors"]!.AsArray().Count == 0 ? 0 : 2;

static string Command(string command, string[] arguments)
{
    var info = new ProcessStartInfo(command); info.RedirectStandardOutput = true; info.RedirectStandardError = true; info.UseShellExecute = false;
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    using var p = Process.Start(info)!; var output = p.StandardOutput.ReadToEnd(); p.WaitForExit();
    if (p.ExitCode != 0) throw new IOException(command + " failed: " + p.StandardError.ReadToEnd());
    return output.Trim();
}
string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string ScriptPath([CallerFilePath] string path = "") => path;
