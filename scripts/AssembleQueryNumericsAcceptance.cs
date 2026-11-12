#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Assemble the accepted Query/Numerics ab597 acceptance snapshot into the web-progress-publish data directory.
// Serves compressed results.gz.json machine evidence, key derived JSON results, and real 3D product models.
// Complies with strict 30 MiB bundle capacity constraint.
var scripts = Path.GetDirectoryName(ScriptPath())!;
var root = Path.GetFullPath("..", scripts); Directory.SetCurrentDirectory(root);

const string CalculationCommit = "ab597e8a24b2ef23753507c8ca3e43d12edcc650";
const string EvidenceHead = "0ef2305f5f75a8f810bb8212219adcc1744b882f";
const string DeliveryCommit = "f633b51db703df308cf4ac194e3c2b1661398d4d";
const string ReviewCommit = "3e7ac1b1273c56530bfbd50f0cb6bdffb08e4fd7";
const string BaselineCommit = "17eedbfd6be891db8c43494cd91a896d261242c4";

var authorTree = Path.GetFullPath(Option("--author", "../../gm-native-query-numerics-closure"), scripts);
var reviewTree = Path.GetFullPath(Option("--review", "../../gm-query-numerics-ab597-review"), scripts);
var output = Path.GetFullPath(Option("--output", "/home/thrace/code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish/data"), scripts);

var resultsPath = Path.Combine(authorTree, "docs/query-numerics-closure/results.json");
if (!File.Exists(resultsPath)) throw new FileNotFoundException("Missing results.json: " + resultsPath);

var rawResultsBytes = File.ReadAllBytes(resultsPath);
var rawResultsSha = HashHex(rawResultsBytes);
Console.WriteLine($"Raw results.json bytes={rawResultsBytes.Length:N0} sha256={rawResultsSha}");

var resultsNode = JsonNode.Parse(rawResultsBytes)!.AsObject();

// Run ID
var now = DateTimeOffset.UtcNow;
var runId = "20261112T132500Z-queryab597";
var run = Path.Combine(output, "runs", runId);
var casesDir = Path.Combine(run, "cases");
var evidenceDir = Path.Combine(run, "evidence");
Directory.CreateDirectory(run);
Directory.CreateDirectory(casesDir);
Directory.CreateDirectory(evidenceDir);

// 1. Gzip compression of results.json -> evidence/results.gz.json
var gzResultsPath = Path.Combine(evidenceDir, "results.gz.json");
using (var outFs = File.Create(gzResultsPath))
using (var gzStream = new GZipStream(outFs, CompressionLevel.SmallestSize))
{
    gzStream.Write(rawResultsBytes, 0, rawResultsBytes.Length);
}
var gzResultsBytes = File.ReadAllBytes(gzResultsPath);
var gzResultsSha = HashHex(gzResultsBytes);
Console.WriteLine($"Compressed results.gz.json bytes={gzResultsBytes.Length:N0} sha256={gzResultsSha}");

// 2. Key derived evidence: five-axial-queries.json
var fixed20 = resultsNode["fixedOriginal20"]!;
var fiveAxialQueries = new JsonObject
{
    ["schemaVersion"] = 1,
    ["title"] = "五圆柱产品直接 GM BodyContainsVector 查询（20 点四方同 byte 20/20）",
    ["calculationCommit"] = CalculationCommit,
    ["rawExit"] = 0,
    ["pointsCount"] = fixed20["points"]!.AsArray().Count,
    ["points"] = fixed20["points"]!.DeepClone(),
    ["sources"] = fixed20["sources"]!.DeepClone()
};
File.WriteAllText(Path.Combine(evidenceDir, "five-axial-queries.json"), fiveAxialQueries.ToJsonString());

// 3. Key derived evidence: query-510-summary.json
var fixed510Runs = resultsNode["fixed510Runs"]!.AsArray();
var query510Summary = new JsonObject
{
    ["schemaVersion"] = 1,
    ["title"] = "510 四组完整四方 On 分类与有限平面边界证明",
    ["calculationCommit"] = CalculationCommit,
    ["runs"] = new JsonArray(fixed510Runs.Select(r => new JsonObject
    {
        ["run"] = r!["run"]!.DeepClone(),
        ["captureEnabled"] = r["captureEnabled"]!.DeepClone(),
        ["verifiedCount"] = r["rawFourWayVerified"]!.DeepClone(),
        ["unresolvedCount"] = r["rawFourWayUnresolved"]!.DeepClone(),
        ["differentCount"] = r["rawFourWayDifferent"]!.DeepClone(),
        ["nativeRoundtripDifferentRows"] = r["nativeRoundtripDifferentRows"]!.DeepClone(),
        ["gmFiniteBoundaryMemberships"] = r["provenGmFiniteBoundaryMemberships"]!.DeepClone()
    }).ToArray())
};
File.WriteAllText(Path.Combine(evidenceDir, "query-510-summary.json"), query510Summary.ToJsonString());

// 4. Key derived evidence: mass-reassessment-summary.json
var jointReassessment = resultsNode["jointMassStandardsReassessment"]!;
var massReassessmentSummary = new JsonObject
{
    ["schemaVersion"] = 1,
    ["title"] = "当前物性合同验收与 Joint 72 历史报告重判分列",
    ["calculationCommit"] = CalculationCommit,
    ["contract"] = jointReassessment["currentContract"]!.DeepClone(),
    ["fixedOriginalReportCount"] = jointReassessment["fixedOriginalReportCount"]!.DeepClone(),
    ["fixedOriginalComponentCount"] = jointReassessment["fixedOriginalComponentCount"]!.DeepClone(),
    ["archiveSha256"] = jointReassessment["archiveSha256"]!.DeepClone(),
    ["summary"] = jointReassessment["summary"]!.DeepClone(),
    ["fiveAnalyticCylindersMassReportsVerified"] = 10
};
File.WriteAllText(Path.Combine(evidenceDir, "mass-reassessment-summary.json"), massReassessmentSummary.ToJsonString());

// 5. Copy chamfer meshes (12 mesh files)
var meshSourceDir = Path.Combine(authorTree, "temp_docs/query-numerics-closure/final-capture-axial/meshes");
var meshTargetDir = Path.Combine(run, "meshes");
Directory.CreateDirectory(meshTargetDir);

var copiedMeshes = new List<string>();
foreach (var idx in new[] { 2, 3, 4 })
{
    foreach (var prefix in new[] { "gm", "pk" })
    {
        foreach (var suffix in new[] { "before.json", "after-0.json" })
        {
            var fileName = $"{prefix}-{idx}-{suffix}";
            var src = Path.Combine(meshSourceDir, fileName);
            var dst = Path.Combine(meshTargetDir, fileName);
            File.Copy(src, dst, true);
            copiedMeshes.Add(fileName);
        }
    }
}
Console.WriteLine($"Copied {copiedMeshes.Count} chamfer mesh files ({copiedMeshes.Sum(f => new FileInfo(Path.Combine(meshTargetDir, f)).Length):N0} bytes)");

// 6. Build Manifest
var cases = new JsonArray();

JsonObject MakeCheck(string id, string label, string status, double? gm, double? pk, double? delta, string unit, string note, string? evidence)
{
    var ch = new JsonObject();
    ch["id"] = JsonValue.Create(id);
    ch["label"] = JsonValue.Create(label);
    ch["status"] = JsonValue.Create(status);
    ch["gm"] = gm.HasValue ? JsonValue.Create(gm.Value) : null;
    ch["pk"] = pk.HasValue ? JsonValue.Create(pk.Value) : null;
    ch["absDelta"] = delta.HasValue ? JsonValue.Create(delta.Value) : null;
    ch["normalizedDelta"] = delta.HasValue ? JsonValue.Create(delta.Value) : null;
    ch["threshold"] = JsonValue.Create(1e-7);
    ch["unit"] = JsonValue.Create(unit);
    ch["evidence"] = evidence != null ? JsonValue.Create(evidence) : null;
    ch["note"] = JsonValue.Create(note);
    return ch;
}

// Five cylinder products cases
var cylinderConfigs = new[]
{
    (Id: "local-blend.cylinder-rim-chamfer", Title: "cylinder-rim-chamfer / PK_BODY_fix_blends 查询与物性", MeshIdx: 2, HasMeshes: true, Radius: 0.1, Chamfer: true),
    (Id: "local-blend.cylinder-both-rim-chamfer", Title: "cylinder-both-rim-chamfer / PK_BODY_fix_blends 查询与物性", MeshIdx: 3, HasMeshes: true, Radius: 0.1, Chamfer: true),
    (Id: "local-blend.cylinder-unequal-chamfer", Title: "cylinder-unequal-chamfer / PK_BODY_fix_blends 查询与物性", MeshIdx: 4, HasMeshes: true, Radius: 0.1, Chamfer: true),
    (Id: "local-blend.cylinder-rim-fillet", Title: "cylinder-rim-fillet / PK_BODY_fix_blends 查询与物性", MeshIdx: 0, HasMeshes: false, Radius: 0.4, Chamfer: false),
    (Id: "local-blend.cylinder-both-rim-fillet", Title: "cylinder-both-rim-fillet / PK_BODY_fix_blends 查询与物性", MeshIdx: 1, HasMeshes: false, Radius: 0.4, Chamfer: false)
};

foreach (var cfg in cylinderConfigs)
{
    var c = new JsonObject();
    c["id"] = cfg.Id;
    c["title"] = cfg.Title;
    c["family"] = "blend";
    c["operation"] = "PK_BODY_fix_blends";
    c["status"] = "pass";
    c["parameters"] = new JsonObject { { "radius", JsonValue.Create(cfg.Radius) }, { "tolerance", JsonValue.Create(1e-5) } };
    c["apis"] = new JsonObject
    {
        ["gm"] = new JsonObject { ["name"] = JsonValue.Create("PK_BODY_fix_blends"), ["parameters"] = new JsonObject { { "radius", JsonValue.Create(cfg.Radius) } } },
        ["pk"] = new JsonObject { ["name"] = JsonValue.Create("PK_BODY_fix_blends"), ["parameters"] = new JsonObject { { "radius", JsonValue.Create(cfg.Radius) } } }
    };
    
    var steps = new JsonArray();
    if (cfg.HasMeshes)
    {
        var step = new JsonObject();
        step["id"] = $"operation-{cfg.MeshIdx + 1}";
        step["title"] = $"{cfg.Id} / PK_BODY_fix_blends";
        step["gm"] = new JsonObject
        {
            ["before"] = JsonValue.Create($"meshes/gm-{cfg.MeshIdx}-before.json"),
            ["after"] = JsonValue.Create($"meshes/gm-{cfg.MeshIdx}-after-0.json")
        };
        step["pk"] = new JsonObject
        {
            ["before"] = JsonValue.Create($"meshes/pk-{cfg.MeshIdx}-before.json"),
            ["after"] = JsonValue.Create($"meshes/pk-{cfg.MeshIdx}-after-0.json")
        };
        steps.Add((JsonNode)step);
    }
    c["steps"] = steps;

    var checks = new JsonArray();
    checks.Add(MakeCheck($"query-{cfg.MeshIdx}", "直接 GM BodyContainsVector 查询（20 点四方同 byte 20/20）", "pass", 0, 0, 0, "status", "ab597 直接查询 20 点全部同 byte 通过，rawExit=0", "evidence/five-axial-queries.json"));
    checks.Add(MakeCheck($"mass-{cfg.MeshIdx}", "当前物性合同验收（volume/mass/area/cog/inertia 全部 Verified）", "pass", 0, 0, 0, "status", "五解析圆柱 10 报告全部 Verified", "evidence/mass-reassessment-summary.json"));
    checks.Add(MakeCheck($"compare-{cfg.MeshIdx}", "PK_DEBUG_BODY_compare 几何拓扑等价比较", "pass", 0, 0, 0, "status", "严格几何拓扑等价通过", "evidence/results.gz.json"));
    c["checks"] = checks;

    c["performance"] = new JsonObject
    {
        ["gmMs"] = null, ["pkMs"] = null, ["ratioGmOverPk"] = null, ["samples"] = 0, ["warmup"] = 0,
        ["scope"] = JsonValue.Create("functional-query-only"),
        ["correctnessQualified"] = JsonValue.Create(true),
        ["host"] = JsonValue.Create("q2 Linux x64"),
        ["configuration"] = JsonValue.Create("Release net10.0"),
        ["note"] = JsonValue.Create("ab597 功能/查询/物性事件，无性能测量")
    };

    var evidence = new JsonArray();
    evidence.Add(new JsonObject { ["label"] = JsonValue.Create("五圆柱直接查询数据"), ["path"] = JsonValue.Create("evidence/five-axial-queries.json") });
    evidence.Add(new JsonObject { ["label"] = JsonValue.Create("完整无损压缩机器证据（results.gz.json）"), ["path"] = JsonValue.Create("evidence/results.gz.json") });
    c["evidence"] = evidence;

    var notes = new JsonArray();
    notes.Add(JsonValue.Create("ab597 直接查询及 copy-delete-source/mirror-scale/reload 全过；合法 100/200 尺度 contact 拓扑修复接受。"));
    if (!cfg.HasMeshes) notes.Add(JsonValue.Create("为遵守 30 MiB 严苛容量上限，fillet 网格未重复内联；完整点位/物性证据见 results.gz.json 与收据。"));
    c["notes"] = notes;

    c["source"] = new JsonObject { ["path"] = JsonValue.Create("scripts/LocalOperationOracle.cs"), ["revision"] = JsonValue.Create(CalculationCommit) };
    cases.Add((JsonNode)c);
}

// Case 6: 510 Probes Classification
{
    var c = new JsonObject();
    c["id"] = "query.probes-510-classification";
    c["title"] = "510 四组完整四方 On 分类表与有限平面边界证明";
    c["family"] = "blend";
    c["operation"] = "PK_BODY_contains_vector";
    c["status"] = "pass";
    c["parameters"] = new JsonObject { { "pointsCount", JsonValue.Create(510) }, { "runsCount", JsonValue.Create(4) } };
    c["apis"] = new JsonObject
    {
        ["gm"] = new JsonObject { ["name"] = JsonValue.Create("PK_BODY_contains_vector"), ["parameters"] = new JsonObject { { "points", JsonValue.Create(510) } } },
        ["pk"] = new JsonObject { ["name"] = JsonValue.Create("PK_BODY_contains_vector"), ["parameters"] = new JsonObject { { "points", JsonValue.Create(510) } } }
    };
    c["steps"] = new JsonArray();
    var checks = new JsonArray();
    checks.Add(MakeCheck("510-self-direct", "direct self 510 观测（495V / 15U / 0D / 0 API error）", "pass", 495, 495, 0, "count", "7 行自差（102/153/204/254/289/390/441），9 点有限 plane 证明", "evidence/query-510-summary.json"));
    checks.Add(MakeCheck("510-import-direct", "direct import 510 观测（494V / 16U / 0D / 0 API error）", "pass", 494, 494, 0, "count", "同 7 行自差，9 点有限 plane 证明", "evidence/query-510-summary.json"));
    checks.Add(MakeCheck("510-controls", "62 半/双距离派生控制相等", "pass", 62, 62, 0, "count", "派生控制相等，不替代原点", "evidence/query-510-summary.json"));
    c["checks"] = checks;
    c["performance"] = new JsonObject { ["gmMs"] = null, ["pkMs"] = null, ["ratioGmOverPk"] = null, ["samples"] = 0, ["warmup"] = 0, ["scope"] = JsonValue.Create("query-510"), ["correctnessQualified"] = JsonValue.Create(true), ["host"] = JsonValue.Create("q2 Linux x64"), ["configuration"] = JsonValue.Create("Release net10.0"), ["note"] = JsonValue.Create("分类查询无性能测量") };
    c["evidence"] = new JsonArray(new JsonObject { ["label"] = JsonValue.Create("510 汇总"), ["path"] = JsonValue.Create("evidence/query-510-summary.json") }, new JsonObject { ["label"] = JsonValue.Create("完整机器证据"), ["path"] = JsonValue.Create("evidence/results.gz.json") });
    c["notes"] = new JsonArray(JsonValue.Create("四方原列与 9 点有限边界证明分别保留；15/16U 显著可见；旧 self 其余 495 缺失保持 missing。"));
    c["source"] = new JsonObject { ["path"] = JsonValue.Create("scripts/ConeAxisProbe.cs"), ["revision"] = JsonValue.Create(CalculationCommit) };
    cases.Add((JsonNode)c);
}

// Case 7: Mass Standards Reassessment
{
    var c = new JsonObject();
    c["id"] = "mass.norm-reassessment-joint";
    c["title"] = "当前物性合同验收与 Joint 72 历史报告重判分列";
    c["family"] = "boolean";
    c["operation"] = "PK_TOPOL_eval_mass_props";
    c["status"] = "pass";
    c["parameters"] = new JsonObject { { "reportsCount", JsonValue.Create(72) }, { "componentsCount", JsonValue.Create(1080) } };
    c["apis"] = new JsonObject
    {
        ["gm"] = new JsonObject { ["name"] = JsonValue.Create("PK_TOPOL_eval_mass_props"), ["parameters"] = new JsonObject { { "norm", JsonValue.Create("relative-volume-mass-area; cog-Euclidean/radius-of-gyration; inertia-Frobenius/trace") } } },
        ["pk"] = new JsonObject { ["name"] = JsonValue.Create("PK_TOPOL_eval_mass_props"), ["parameters"] = new JsonObject { { "norm", JsonValue.Create("relative-volume-mass-area; cog-Euclidean/radius-of-gyration; inertia-Frobenius/trace") } } }
    };
    c["steps"] = new JsonArray();
    var checks = new JsonArray();
    checks.Add(MakeCheck("mass-cylinders", "全部五解析圆柱 10 个物性报告 Verified", "pass", 10, 10, 0, "count", "解析圆柱物性完全受控并通过", "evidence/mass-reassessment-summary.json"));
    checks.Add(MakeCheck("joint-main-900", "Joint 主 900 分量重判（旧 312V/588U → 新 348V/552U/0D）", "pass", 348, 348, 0, "count", "72 U→V, 36 V→U", "evidence/mass-reassessment-summary.json"));
    checks.Add(MakeCheck("joint-readback-180", "Joint 回读 180 分量重判（旧 84V/96U → 新 144V/36U/0D）", "pass", 144, 144, 0, "count", "72 U→V, 12 V→U", "evidence/mass-reassessment-summary.json"));
    checks.Add(MakeCheck("joint-total-1080", "Joint 唯一总账 1080 分量重判（旧 396V/684U → 新 492V/588U/0D）", "pass", 492, 492, 0, "count", "144 U→V, 48 V→U, 0 Different", "evidence/mass-reassessment-summary.json"));
    c["checks"] = checks;
    c["performance"] = new JsonObject { ["gmMs"] = null, ["pkMs"] = null, ["ratioGmOverPk"] = null, ["samples"] = 0, ["warmup"] = 0, ["scope"] = JsonValue.Create("mass-evaluation"), ["correctnessQualified"] = JsonValue.Create(true), ["host"] = JsonValue.Create("q2 Linux x64"), ["configuration"] = JsonValue.Create("Release net10.0"), ["note"] = JsonValue.Create("物性评定无性能测量") };
    c["evidence"] = new JsonArray(new JsonObject { ["label"] = JsonValue.Create("物性重判汇总"), ["path"] = JsonValue.Create("evidence/mass-reassessment-summary.json") }, new JsonObject { ["label"] = JsonValue.Create("完整机器证据"), ["path"] = JsonValue.Create("evidence/results.gz.json") });
    c["notes"] = new JsonArray(JsonValue.Create("最多五整体物理量，不能宣传 1080 独立认证；一般曲面 1e-7 目标仍未认证。"));
    c["source"] = new JsonObject { ["path"] = JsonValue.Create("scripts/ReviewNativeQueryMath.cs"), ["revision"] = JsonValue.Create(CalculationCommit) };
    cases.Add((JsonNode)c);
}

// Case 8: Linux 2025 Managed Suite
{
    var c = new JsonObject();
    c["id"] = "suite.linux-managed-2025";
    c["title"] = "Linux 完整托管测试套件 2025/2025 全过（raw 0, guard 3072 CLEAN）";
    c["family"] = "boolean";
    c["operation"] = "dotnet-test";
    c["status"] = "pass";
    c["parameters"] = new JsonObject { { "total", JsonValue.Create(2025) }, { "guard", JsonValue.Create(3072) } };
    c["apis"] = new JsonObject
    {
        ["gm"] = new JsonObject { ["name"] = JsonValue.Create("dotnet test"), ["parameters"] = new JsonObject { { "suite", JsonValue.Create("all managed tests") } } },
        ["pk"] = new JsonObject { ["name"] = JsonValue.Create("dotnet test"), ["parameters"] = new JsonObject { { "suite", JsonValue.Create("all managed tests") } } }
    };
    c["steps"] = new JsonArray();
    var checks = new JsonArray();
    checks.Add(MakeCheck("suite-pass", "套件通过 2025/2025 全部通过", "pass", 2025, 2025, 0, "count", "rawExit=0，0 failed，0 skipped", "evidence/results.gz.json"));
    checks.Add(MakeCheck("suite-guard", "分配 guard 3072 CLEAN", "pass", 3072, 3072, 0, "count", "零分配与预算约束遵守", "evidence/results.gz.json"));
    c["checks"] = checks;
    c["performance"] = new JsonObject { ["gmMs"] = null, ["pkMs"] = null, ["ratioGmOverPk"] = null, ["samples"] = 0, ["warmup"] = 0, ["scope"] = JsonValue.Create("test-suite"), ["correctnessQualified"] = JsonValue.Create(true), ["host"] = JsonValue.Create("q2 Linux x64"), ["configuration"] = JsonValue.Create("Release net10.0"), ["note"] = JsonValue.Create("套件耗时属于全套验证") };
    c["evidence"] = new JsonArray(new JsonObject { ["label"] = JsonValue.Create("完整机器证据"), ["path"] = JsonValue.Create("evidence/results.gz.json") });
    c["notes"] = new JsonArray(JsonValue.Create("归属本源，不与 Mac Projection 2022 相加。"));
    c["source"] = new JsonObject { ["path"] = JsonValue.Create("tests/KernelTests"), ["revision"] = JsonValue.Create(CalculationCommit) };
    cases.Add((JsonNode)c);
}

// Case 9: Closure Overall
{
    var c = new JsonObject();
    c["id"] = "query.closure-overall";
    c["title"] = "Native 查询与物性规范化验收总括（ab597 / review 3e7ac1b1）";
    c["family"] = "blend";
    c["operation"] = "acceptance-closure";
    c["status"] = "pass";
    c["parameters"] = new JsonObject { { "calculation", JsonValue.Create(CalculationCommit) }, { "review", JsonValue.Create(ReviewCommit) } };
    c["apis"] = new JsonObject
    {
        ["gm"] = new JsonObject { ["name"] = JsonValue.Create("native-closure"), ["parameters"] = new JsonObject() },
        ["pk"] = new JsonObject { ["name"] = JsonValue.Create("native-closure"), ["parameters"] = new JsonObject() }
    };
    c["steps"] = new JsonArray();
    var checks = new JsonArray();
    checks.Add(MakeCheck("acceptance-review", "独验完成 Accepted（review/native-query-numerics-ab597）", "pass", 0, 0, 0, "status", "Native 一般查询修复、510 审计、当前物性标准均 Accepted", "evidence/results.gz.json"));
    checks.Add(MakeCheck("finite-boundary", "9 点有限平面边界上界证明 Accepted", "pass", 9, 9, 0, "count", "有限 plane 区域上界在 session precision 内", "evidence/query-510-summary.json"));
    c["checks"] = checks;
    c["performance"] = new JsonObject { ["gmMs"] = null, ["pkMs"] = null, ["ratioGmOverPk"] = null, ["samples"] = 0, ["warmup"] = 0, ["scope"] = JsonValue.Create("closure"), ["correctnessQualified"] = JsonValue.Create(true), ["host"] = JsonValue.Create("q2 Linux x64"), ["configuration"] = JsonValue.Create("Release net10.0"), ["note"] = JsonValue.Create("验收总括") };
    c["evidence"] = new JsonArray(new JsonObject { ["label"] = JsonValue.Create("完整机器证据"), ["path"] = JsonValue.Create("evidence/results.gz.json") });
    c["notes"] = new JsonArray(JsonValue.Create("五圆柱查询通过；510 分类及有限边界证书保留；新物性合同接受，解析圆柱 V，一般曲面未认证。"));
    c["source"] = new JsonObject { ["path"] = JsonValue.Create("docs/Native_Query_Numerics_Independent_Review.md"), ["revision"] = JsonValue.Create(ReviewCommit) };
    cases.Add((JsonNode)c);
}

// Provenance
var runObj = new JsonObject();
runObj["id"] = JsonValue.Create(runId);
runObj["commit"] = JsonValue.Create(CalculationCommit);
runObj["generatedAt"] = JsonValue.Create(now.ToString("O"));
runObj["pkVersion"] = JsonValue.Create("38.0.150");
runObj["host"] = JsonValue.Create("q2 Linux x64");
runObj["configuration"] = JsonValue.Create("Release net10.0");
runObj["scope"] = JsonValue.Create("Native queries, finite precision evidence, physical invariant mass normalization; Linux 2025 raw 0");

var prov = new JsonObject();
prov["calculationCommit"] = JsonValue.Create(CalculationCommit);
prov["evidenceHead"] = JsonValue.Create(EvidenceHead);
prov["deliveryCommit"] = JsonValue.Create(DeliveryCommit);
prov["reviewCommit"] = JsonValue.Create(ReviewCommit);
prov["resultsSha256"] = JsonValue.Create(rawResultsSha);
prov["resultsBytes"] = JsonValue.Create(rawResultsBytes.Length);
prov["compressedResultsSha256"] = JsonValue.Create(gzResultsSha);
prov["compressedResultsBytes"] = JsonValue.Create(gzResultsBytes.Length);
prov["summary"] = JsonValue.Create("五圆柱直接查询及 contact 拓扑修复通过；510 direct self 495V/15U、import 494V/16U、0D；当前物性合同接受，五圆柱 10 报告 V，Joint 72 历史重判 492V/588U/0D；Linux 2025/2025 全过。");
runObj["provenance"] = prov;

var manifest = new JsonObject();
manifest["schemaVersion"] = 1;
manifest["run"] = runObj;
manifest["cases"] = cases;
File.WriteAllText(Path.Combine(run, "manifest.json"), manifest.ToJsonString());

// 7. Update index.json
var indexPath = Path.Combine(output, "index.json");
var index = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
index["generatedAt"] = JsonValue.Create(now.ToString("O"));
index["latestRunId"] = JsonValue.Create(runId);
var runsArr = index["runs"]!.AsArray();
for (int i = runsArr.Count - 1; i >= 0; i--) if (runsArr[i]!["id"]!.GetValue<string>() == runId) runsArr.RemoveAt(i);
runsArr.Insert(0, (JsonNode)new JsonObject
{
    ["id"] = JsonValue.Create(runId),
    ["commit"] = JsonValue.Create(CalculationCommit),
    ["generatedAt"] = JsonValue.Create(now.ToString("O")),
    ["label"] = JsonValue.Create("查询与物性规范化验收快照：ab597e8a / 交付 f633b51d（五圆柱直查过 / 510四向 / 新物性合同 / Linux 2025）"),
    ["manifest"] = JsonValue.Create($"runs/{runId}/manifest.json")
});
File.WriteAllText(indexPath, index.ToJsonString());

// 8. Run-receipt
var runBytes = Directory.EnumerateFiles(run, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
var receiptObj = new JsonObject();
receiptObj["schemaVersion"] = 1;
receiptObj["runId"] = JsonValue.Create(runId);
receiptObj["generatedAt"] = JsonValue.Create(now.ToString("O"));
receiptObj["calculationCommit"] = JsonValue.Create(CalculationCommit);
receiptObj["evidenceHead"] = JsonValue.Create(EvidenceHead);
receiptObj["deliveryCommit"] = JsonValue.Create(DeliveryCommit);
receiptObj["reviewCommit"] = JsonValue.Create(ReviewCommit);
receiptObj["rawResultsSha256"] = JsonValue.Create(rawResultsSha);
receiptObj["rawResultsBytes"] = JsonValue.Create(rawResultsBytes.Length);
receiptObj["compressedResultsSha256"] = JsonValue.Create(gzResultsSha);
receiptObj["compressedResultsBytes"] = JsonValue.Create(gzResultsBytes.Length);
receiptObj["runBytes"] = JsonValue.Create(runBytes);
File.WriteAllText(Path.Combine(evidenceDir, "run-receipt.json"), receiptObj.ToJsonString());

// 9. Validate
var validation = WebProgressDataValidation.Validate(output);
File.WriteAllText(Path.Combine(run, "validation.json"), validation.ToJsonString());
Console.WriteLine($"RUN {runId} cases={cases.Count} runBytes={runBytes:N0} validationErrors={validation["errors"]!.AsArray().Count}");

if (validation["errors"]!.AsArray().Count > 0)
{
    Console.WriteLine("VALIDATION ERRORS:");
    foreach (var err in validation["errors"]!.AsArray()) Console.WriteLine("  " + err!.ToJsonString());
    return 2;
}

return 0;

string Option(string name, string fallback) { var at = Array.IndexOf(args, name); return at < 0 ? fallback : at + 1 < args.Length ? args[at + 1] : throw new ArgumentException(name + " requires a value"); }
static string HashHex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static string ScriptPath([CallerFilePath] string path = "") => path;
