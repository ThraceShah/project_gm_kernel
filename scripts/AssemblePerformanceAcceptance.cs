#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed performance acceptance snapshot run (candidate 6761600 +
// accepted review 80bde347) from the immutable Mac raw packages. All displayed
// values are derived from the original report.json samples and cross-checked
// against the accepted review table; Linux is never used to re-measure Mac
// samples. No modeling runs here.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Candidate="6761600fef20602342cd9dc81bd4f7e4fb85d939";
const string CandidateSrc="01e03aa9ed10a8145556872e98fa1677e17a4cb7";
const string CandidateNative="03931f6";
const string Baseline="ecfff3aae1dbbe9dd48adbbdf2e8e635da735938";
const string BaselineSrc="e314cc2307c5e77ea6f2b7685ba17836ae4ef5c8";
const string Review="80bde347f293314e2627120e376828e0ed5b6162";
const string RawReportSha="0b867277df0f35e5a9be369f7cf17fcd36308a2cbe9db5bea12d0dc080ece418";
const string PerfTarSha="85be8a3e3d349cd2a755ecd7a67722746ec5cc9a391aaf389b79a6d294ce0333";
const string QualityTarSha="f51e1cde728f63bfd6af64520b5e5f2267813dc828be0cba394c472b3113887c";
const string BaselineTarSha="0c93eb4ebedc840f0bb5975dffb66903ae912950e3a11d2f01132cc2f7cd57fd";
var incoming=Path.GetFullPath(Option("--incoming","../temp_docs/incoming/mac-performance"),scripts);
var rawReport=Path.Combine(incoming,"extracted/temp_docs/pk-native-work-reduction/controlled-final/report.json");
var qualityDir=Path.GetFullPath(Option("--quality","../temp_docs/incoming/mac-performance/extracted-quality"),scripts);
var baselineDir=Path.GetFullPath(Option("--baseline","../temp_docs/incoming/mac-performance/extracted-baseline"),scripts);
var reviewTree=Path.GetFullPath(Option("--review","../../gm-pk-performance-final-review"),scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-performance/data"),scripts);
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-perf6761600");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains('\\'))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Hash(File.ReadAllBytes(rawReport))==RawReportSha,"raw report hash");
foreach(var (path,sha) in new[]{(Path.Combine(incoming,"mac-modeling-performance-6761600-raw.tar.gz"),PerfTarSha),(Path.Combine(incoming,"mac-modeling-quality-6761600.tar.gz"),QualityTarSha),(Path.Combine(incoming,"mac-modeling-baseline-failures.tar.gz"),BaselineTarSha)})
    Require(Hash(File.ReadAllBytes(path))==sha,Path.GetFileName(path)+" hash");
Require(Command("git",["-C",reviewTree,"rev-parse","HEAD"])==Review,"review tree HEAD");

// ---- raw report parsing (all values derive from the original samples)
var report=JsonNode.Parse(File.ReadAllText(rawReport))!.AsObject();
Require(report["candidateSha"]!.GetValue<string>()==Candidate&&report["baselineSha"]!.GetValue<string>()==Baseline,"report source identities");
var samples=report["samples"]!.AsArray().OfType<JsonObject>().ToArray();
Require(samples.Length==20&&samples.All(s=>s["processExit"]!.GetValue<int>()==0),"20 processes, all original exit 0");
string CaseOf(JsonObject s)=>s["case"]!.GetValue<string>();
double Median(IEnumerable<double> v){var a=v.Order().ToArray();return a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2;}
var ops=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
foreach(var op in new[]{"sphere","planar","curved","solidblend","shortedge"})
{
    JsonObject[] pairs(string source,string temperature)=>samples.Where(s=>CaseOf(s)==op&&s["source"]!.GetValue<string>()==source).SelectMany(s=>s["pairs"]!.AsArray().OfType<JsonObject>().Where(p=>p["temperature"]!.GetValue<string>()==temperature)).ToArray();
    var warmB=pairs("baseline","warm");var warmC=pairs("candidate","warm");var coldB=pairs("baseline","cold");var coldC=pairs("candidate","cold");
    Require(warmB.Length==4&&warmC.Length==4&&coldB.Length==2&&coldC.Length==2,op+" pair counts");
    double Gm(JsonObject p)=>p["gmMs"]!.GetValue<double>();double Pk(JsonObject p)=>p["pkMs"]!.GetValue<double>();
    var gmB=Median(warmB.Select(Gm));var gmC=Median(warmC.Select(Gm));var pkC=Median(warmC.Select(Pk));
    var change=(gmC/gmB-1)*100;var speedup=gmB/gmC;
    var paired=Median(warmC.Select(p=>p["ratioGmOverPk"]!.GetValue<double>()));
    ops[op]=new JsonObject
    {
        ["gmMedianBaselineWarm"]=gmB,["gmMedianCandidateWarm"]=gmC,["pkMedianCandidateWarm"]=pkC,
        ["gmElapsedChangePercent"]=change,["warmSpeedup"]=speedup,["pairedRatioMedian"]=paired,
        ["ratioOfMedians"]=gmC/pkC,
        ["gmWarmRangeCandidate"]=new JsonArray(warmC.Min(Gm),warmC.Max(Gm)),
        ["gmWarmRangeBaseline"]=new JsonArray(warmB.Min(Gm),warmB.Max(Gm)),
        ["gmColdMedianBaseline"]=Median(coldB.Select(Gm)),["gmColdMedianCandidate"]=Median(coldC.Select(Gm)),
        ["warmPairsCandidate"]=new JsonArray(warmC.Select(p=>(JsonNode?)JsonValue.Create(Pk(p))).ToArray()),
        ["allPairs"]=new JsonArray(samples.Where(s=>CaseOf(s)==op).SelectMany(s=>s["pairs"]!.AsArray().Select(p=>(JsonNode?)new JsonObject{["source"]=s["source"]!.GetValue<string>(),["process"]=s["directory"]!.GetValue<string>(),["rep"]=p!["rep"]!.DeepClone(),["temperature"]=p["temperature"]!.DeepClone(),["gmMs"]=p["gmMs"]!.DeepClone(),["pkMs"]=p["pkMs"]!.DeepClone(),["ratioGmOverPk"]=p["ratioGmOverPk"]!.DeepClone()})).ToArray())
    };
}
// cross-check the accepted review table (warm medians / change / paired median)
void Pin(string op,double gmB,double gmC,double pkC,double changePercent,double paired)
{var o=ops[op];Require(Math.Abs(o["gmMedianBaselineWarm"]!.GetValue<double>()-gmB)<1e-4*Math.Max(1,gmB)&&Math.Abs(o["gmMedianCandidateWarm"]!.GetValue<double>()-gmC)<1e-4*Math.Max(1,gmC)&&Math.Abs(o["pkMedianCandidateWarm"]!.GetValue<double>()-pkC)<1e-4*Math.Max(1,pkC)&&Math.Abs(o["gmElapsedChangePercent"]!.GetValue<double>()-changePercent)<0.005&&Math.Abs(o["pairedRatioMedian"]!.GetValue<double>()-paired)<1e-4*Math.Max(1,paired),op+" differs from accepted review table");}
Pin("sphere",0.07575,0.07545,0.26760,-0.40,0.28479);
Pin("planar",0.59570,0.77640,1.10575,30.334,0.80082);
Pin("curved",917.27970,909.81715,1.85730,-0.81,488.26694);
Pin("solidblend",1540.33615,1524.97405,1.43810,-1.00,1064.27179);
Pin("shortedge",22593.31015,14179.10690,1.49920,-37.2420,9465.56563);
var workLines=new JsonArray(samples.Where(s=>CaseOf(s)=="shortedge").SelectMany(s=>s["work"]!.AsArray().Select(w=>(JsonNode?)JsonValue.Create(s["source"]!.GetValue<string>()+" "+s["directory"]!.GetValue<string>()+" "+w!.GetValue<string>()))).ToArray());

// ---- quality settlement parsing
var suiteReceipt=JsonNode.Parse(File.ReadAllText(Path.Combine(qualityDir,"docs/native-modeling-work-reduction/suite-receipt.json")))!.AsObject();
Require(suiteReceipt["rawExit"]!.GetValue<int>()==1,"suite original exit 1");
var counters=suiteReceipt["counters"]!.AsObject();
var failures=suiteReceipt["failures"]!.AsArray();
Require(counters["failed"]!.GetValue<int>()==2&&failures.Count==2,"suite 1875/1877 with 2 failures");
var comparison=JsonNode.Parse(File.ReadAllText(Path.Combine(baselineDir,"docs/native-modeling-work-reduction/baseline-failure-comparison.json")))!.AsObject();
Require(comparison["sameObservedFailures"]!.GetValue<bool>()==true,"same-Mac baseline reproduces both failures");

// ---- run directory and shipped evidence
var run=Path.Combine(output,"runs",runId);
if(Directory.Exists(run))throw new IOException("Run already exists: immutable "+runId);
var evidenceDir=Path.Combine(run,"evidence");Directory.CreateDirectory(evidenceDir);
void Ship(string source,string shipped,string note)
{
    var bytes=File.ReadAllBytes(source);File.WriteAllBytes(Path.Combine(evidenceDir,shipped),bytes);
}
Ship(rawReport,"mac-report.json","verbatim original");
Ship(Path.Combine(qualityDir,"docs/native-modeling-work-reduction/suite-receipt.json"),"suite-receipt.json","verbatim original");
Ship(Path.Combine(qualityDir,"temp_docs/pk-native-work-reduction/complete-suite.log"),"complete-suite.log","verbatim original");
Ship(Path.Combine(baselineDir,"docs/native-modeling-work-reduction/baseline-failure-comparison.json"),"baseline-failure-comparison.json","verbatim original");
Ship(Path.Combine(baselineDir,"temp_docs/pk-native-work-reduction/baseline-failure-comparison.log"),"baseline-failure-comparison.log","verbatim original");
var trxBytes=File.ReadAllBytes(Path.Combine(qualityDir,"temp_docs/pk-native-work-reduction/test-results/complete-suite.trx"));

// ---- host/performance metadata (measured on the Mac host; never re-measured on Linux)
var host=new JsonObject{["os"]="macOS 27.2.0",["architecture"]="Arm64",["cpu"]="Apple M4",["sdk"]="10.0.400",["runtime"]=".NET 10.0.11",["pk"]="Parasolid 38.0.150, precision 1e-8",["pkToy"]="d820719b6d8510d623972ab9d8efc65edbc312f9"};
var configuration=new JsonObject{["build"]="Release (JIT managed, default tiering)",["nativeAot"]=false,["protocol"]=report["protocol"]!.DeepClone(),["profiling"]="bench-profile enabled in both trees; profile overhead present in qualified timings",["timing"]="capture API interval only",["order"]="fixed per-case GM/PK order; ABBA sources per case; one process at a time"};
JsonObject Perf(string op)
{
    var o=ops[op];
    return new JsonObject{["gmMs"]=JsonValue.Create(o["gmMedianCandidateWarm"]!.GetValue<double>()),["pkMs"]=JsonValue.Create(o["pkMedianCandidateWarm"]!.GetValue<double>()),["ratioGmOverPk"]=JsonValue.Create(o["ratioOfMedians"]!.GetValue<double>()),["samples"]=6,["warmup"]=0,["scope"]="Same-host Mac M4 ABBA, 2 processes x 3 fresh-input reps per source; rep0 cold, reps1/2 warm. gmMs/pkMs = candidate warm medians; ratioGmOverPk = ratio of those medians. The accepted paired-median GM/PK is reported per case in checks (semantics differ, both are real). No cross-host ratio; Linux not used to re-measure.",["correctnessQualified"]=true,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="限定固定操作样本；默认 assembly 路径；不签 f9c59201 event-arrangement 或整体 PK 目标；小样本（每源 2 冷 + 4 暖）。",["measurementKind"]="fresh-process-cold-median",["stableBenchmark"]=false,["qualifiedScope"]="Five fixed successful operations on one Mac host; not general-model performance certification."};
}
JsonNode NullPerf(string note)=>new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]=note,["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]=note,["measurementKind"]="not-run",["stableBenchmark"]=false,["qualifiedScope"]="Correctness/quality scope only; no performance certification."};
JsonObject Check(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,string? evidence=null)
    =>new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=gm,["pk"]=pk,["absDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value)):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=evidence,["note"]=note};
var cases=new JsonArray();
void AddCase(string id,string title,string family,string operation,string status,JsonArray checks,JsonArray evidence,JsonArray notes,JsonNode perf,string sourcePath,int[] exits,int? exitCode)
{
    cases.Add((JsonNode)new JsonObject
    {
        ["id"]=id,["title"]=title,["family"]=family,["operation"]=operation,["status"]=status,
        ["parameters"]=new JsonObject{{"candidate",Candidate},{"candidateSrc",CandidateSrc},{"baseline",Baseline},{"acceptanceReview","../gm-pk-performance-final-review/docs/Pk_Performance_6761600_Independent_Review.md @ "+Review}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]=operation,["parameters"]=new JsonObject{{"sourceTree","gm-pk-performance-delivery @ "+Candidate},{"nativeTree",CandidateNative}}},["pk"]=new JsonObject{["name"]=operation,["parameters"]=new JsonObject{{"oracle","real Parasolid 38.0.150 (Mac), identical numeric parameters"}}}},
        ["steps"]=new JsonArray(),["checks"]=checks,["performance"]=perf,["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]=sourcePath,["revision"]=Candidate},
        ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=exitCode,["exitCodes"]=new JsonArray(exits.Select(c=>(JsonNode?)JsonValue.Create(c)).ToArray()),["timedOut"]=false}
    });
}
var ev=new JsonArray((JsonNode)new JsonObject{["label"]="Mac 原 report.json（20 进程/60 pairs 全部原值与 work 行，原字节）",["path"]="evidence/mac-report.json"},(JsonNode)new JsonObject{["label"]="原件哈希锚定",["path"]="evidence/run-receipt.json"});
string F(double v)=>v.ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
foreach(var (op,caseId,title,family) in new[]{("sphere","perf.sphere-subtract","perf / sphere subtract（冷/暖全值）","boolean"),("planar","perf.planar-subtract","perf / planar subtract（+30.334% 回归未归因，保留）","boolean"),("curved","perf.curved-block-subtract","perf / curved block subtract（小变化不计实质收益）","boolean"),("solidblend","perf.plane-nurbs-solidblend","perf / plane-NURBS fillet solidblend（小变化不计实质收益）","blend"),("shortedge","perf.shortedge-fillet","perf / shortedge fillet（-37.2420% / 1.59342x，仍 9465.56563x PK）","blend")})
{
    var o=ops[op];
    var checks=new JsonArray(
        Check("quality-"+op,"12 pairs strict 质量门（0 API error、full BODY_check 0 faults、结构计数、15 物性 1e-7）","pass",12,12,0,"pairs","审计复算最坏 normalized15=5.239703452434893e-9；严格 mask 全 60 raw 文件","evidence/mac-report.json"),
        Check("warm-gm-"+op,"候选 GM 暖中位数（ms）","pass",o["gmMedianCandidateWarm"]!.GetValue<double>(),o["gmMedianBaselineWarm"]!.GetValue<double>(),null,"ms","右列为基线 GM 暖中位数；变化 "+F(o["gmElapsedChangePercent"]!.GetValue<double>())+"%","evidence/mac-report.json"),
        Check("paired-"+op,"候选 GM/PK 配对中位数（接受口径）","diagnostic",o["pairedRatioMedian"]!.GetValue<double>(),1,null,"x","与 performance.ratioGmOverPk（中位数之比 "+F(o["ratioOfMedians"]!.GetValue<double>())+"）语义不同，两者均为真实原值","evidence/mac-report.json"),
        Check("range-"+op,"候选 GM 暖区间 ["+F(o["gmWarmRangeCandidate"]![0]!.GetValue<double>())+", "+F(o["gmWarmRangeCandidate"]![1]!.GetValue<double>())+"]；冷中位 "+F(o["gmColdMedianCandidate"]!.GetValue<double>())+"ms","diagnostic",null,null,null,"ms","样本范围如实保留；每源 2 冷 + 4 暖","evidence/mac-report.json"),
        Check("exit-"+op,"原进程退出码（2 进程 × 3 reps，全部 0）","pass",0,0,0,"exit code","",null));
    var notes=new JsonArray((JsonNode?)JsonValue.Create("同 host（Mac M4）ABBA、同 runner、同参数合格样本；Linux 不重测、不替代 Mac 时间。profiling 开启于两树；口径为 capture API interval。"),
        (JsonNode?)JsonValue.Create("默认 assembly 路径的固定成功操作；不签 f9c59201 event-arrangement、不签整体 PK 目标、不推及一般模型或下游组合。"),
        (JsonNode?)JsonValue.Create("rep0 为进程冷（非整机冷/同 JIT 状态）；XT 仅最终 rep2 保留原字节，不把 rep2 XT 签给其它 reps。"));
    if(op=="planar")notes.Add((JsonNode?)JsonValue.Create("回归 +30.334% 为真实观察：候选暖区间与基线区间重叠、zero Jacobians、原因未归因；不做重复 ABBA 选窗，保留观察不隐藏。"));
    if(op=="shortedge")notes.Add((JsonNode?)JsonValue.Create("工作量收益（residual-directions 约 1.01e7→2.78e4 等）见 perf.work-reduction-counters；不得表述为 99.7% 算术/CPU 删除。"));
    AddCase(caseId,title,family,"PK_BODY_boolean (fixed successful operation)","pass",checks,(JsonArray)ev.DeepClone(),notes,Perf(op),"scripts/NativeModelingWorkComparison.cs",new[]{0,0,0,0},0);
}

// work reduction counters
AddCase("perf.work-reduction-counters","perf / 工作量收益计数（建图/节点/rebind 与 residual 调用对照）","blend","interval-evaluation work reduction counters","pass",
    new JsonArray(
        Check("work-audit","计数与原始 log/report 一致（独立审计 exit 0）","pass",null,null,null,"","审计脚本按同 repetition 对齐核对；warm/cold 缓存历史会改变不同 rep 计数","evidence/mac-report.json"),
        Check("work-residual","shortedge warm rep1 residual-directions：基线 10,113,178 → 候选 27,774","diagnostic",27774,10113178,null,"calls","接受报告 rep-1 对照值；各进程各行原值见 evidence/mac-report.json work 行","evidence/mac-report.json"),
        Check("work-nodes","候选仍执行：record-builds 223,506 / record-nodes 33,683,402 / record-rebinds 1,217,266 / jacobians 1,450,110 / directions 10,113,418","diagnostic",null,null,null,"counts","操作计数，不是算术指令或排他 CPU 成本；不得表述为 99.7% 算术/CPU 删除",null),
        Check("work-retained","两源保留：SSI cells 4,956 / root cells 220,895 / roots 3,518 / winding 196 / bound requests 275,525 / bound evaluations 223,331 / hits 30,727 / budget failures 0","diagnostic",null,null,null,"counts","",null)),
    new JsonArray((JsonNode)new JsonObject{["label"]="Mac 原 report.json work 行（原字节）",["path"]="evidence/mac-report.json"}),
    new JsonArray((JsonNode?)JsonValue.Create("工作量收益独立于耗时结论显示；load average（约 3.51–11.09）不证明排他 API CPU。"),
        (JsonNode?)JsonValue.Create("显示建图/节点/rebind 等工作量变化，不是 99.7% CPU 或算术删除结论。")),
    NullPerf("计数类证据，无耗时测量；不得由此推导倍率。"),"scripts/NativeModelingWorkComparison.cs",new int[0],null);

// quality settlement
AddCase("quality.full-suite-1877","quality / Mac 全量 suite 1875/1877（2 既有失败，原 exit 1）","blend","tests/KernelTests complete suite","fail",
    new JsonArray(
        Check("suite-total","TRX 执行数","pass",1877,1877,0,"tests","unique executions","evidence/suite-receipt.json"),
        Check("suite-passed","通过数","pass",1875,1875,0,"tests","",null),
        Check("suite-failed","失败数（Expected Success / Actual NotConverged）","fail",2,0,0,"tests","BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative；NestedBlendCacheEmptyReadSetTests.SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled（调用前者）","evidence/complete-suite.log"),
        Check("suite-exit","原外部退出码","fail",1,1,0,"exit code","自然结算 15m49s；不用审计 exit 0 代签",null),
        Check("suite-baseline","同 Mac 基线对照：ecfff3a 与 candidate 定向复跑同两测项同样失败（TRX 语义一致）→ 既有失败归类","pass",2,2,0,"failures","baseline TRX 2/2 fail、candidate TRX 2/2 fail；comparison runner exit 0=失败匹配，不是测试通过","evidence/baseline-failure-comparison.json"),
        Check("suite-dll-binding","启动时编译测试 DLL 哈希缺口","missing",null,null,null,"","历史缺口保留；后续稳定 DLL 指纹不回填该次启动绑定",null),
        Check("suite-record-tests","12 记录算术/支撑测试在 suite 内通过；最大容量测试为独立一次通过（原 exit 0），未并入 1877","pass",12,12,0,"tests","",null)),
    new JsonArray((JsonNode)new JsonObject{["label"]="原 complete-suite.log（原字节）",["path"]="evidence/complete-suite.log"},(JsonNode)new JsonObject{["label"]="原 suite 结算收据（原字节）",["path"]="evidence/suite-receipt.json"},(JsonNode)new JsonObject{["label"]="同 Mac 基线失败对照（原字节）",["path"]="evidence/baseline-failure-comparison.json"},(JsonNode)new JsonObject{["label"]="TRX 原件（未内联，2.75MB）：temp_docs/incoming/mac-performance/extracted-quality/temp_docs/pk-native-work-reduction/test-results/complete-suite.trx sha256=fce633b02e4d9cdd6654a29a2eb3904150a65cb95ac45650cc4788f099a9769d",["path"]="evidence/run-receipt.json"}),
    new JsonArray((JsonNode?)JsonValue.Create("不写全 suite 过；两项失败为同 Mac 基线既有失败（同一 supporting-blend 检查），不是本次优化新增回归，但保持红不消失。"),
        (JsonNode?)JsonValue.Create("Linux 1885 与本 Mac 1877 不是同一套/来源，互不代签。")),
    NullPerf("质量结算，无耗时测量。"),"tests/KernelTests/KernelTests.csproj",new[]{1},1);

// overall PK target / cross-cutting
AddCase("perf.overall-pk-target","overall / PK 性能目标未达成；planar 回归未归因；范围限定","boolean","overall performance target","fail",
    new JsonArray(
        Check("target-pk","PK 性能目标（GM/PK ≤ 1）","fail",9465.56563,1,1,"x","未达：curved 488.26694x、solidblend 1064.27179x、shortedge 9465.56563x（配对中位数）","evidence/mac-report.json"),
        Check("target-planar","planar GM 耗时回归 +30.334%","fail",30.334,0,0,"%","区间重叠、zero Jacobians、原因未归因；保留观察",null),
        Check("target-fast-side","sphere 0.28479x、planar 0.80082x：此五固定操作中 GM 快于 PK","diagnostic",null,null,null,"","仅限固定操作样本，不推及一般模型",null),
        Check("target-quality","全量 suite 非全绿（1875/1877）","fail",1875,1877,0,"tests","2 既有失败见 quality.full-suite-1877",null),
        Check("target-work","工作量收益证据已独立显示（非 CPU 结论）","pass",null,null,null,"","见 perf.work-reduction-counters",null)),
    new JsonArray((JsonNode)new JsonObject{["label"]="全部原值与样本范围",["path"]="evidence/mac-report.json"},(JsonNode)new JsonObject{["label"]="来源/哈希/容量回执",["path"]="evidence/run-receipt.json"}),
    new JsonArray((JsonNode?)JsonValue.Create("不签一般模型加速、下游组合成功、f9c59201 计算接受、PK 达标、全绿 suite、合 main 或产品部署；与计算 run 20261111T130936Z-f9c59201 分源（不同源码），不构成同一 kernel 集成。"),
        (JsonNode?)JsonValue.Create("family 为看板既有粗分类（blend|boolean）；准确类别以标题/operation/notes 为准。")),
    NullPerf("整体结论，无新耗时测量。"),"docs/Pk_Performance_6761600_Independent_Review.md",new int[0],null);

// ---- manifest
var manifest=new JsonObject
{
    ["schemaVersion"]=1,
    ["run"]=new JsonObject
    {
        ["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["pkVersion"]="38.0.150",
        ["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),
        ["scope"]=new JsonObject{["kind"]="performance-acceptance-snapshot",["candidate"]=Candidate,["acceptanceReview"]=Review,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="固定性能候选 6761600 + 接受报告 80bde347 的限定范围快照；与计算 run 分源，不构成集成。"},
        ["provenance"]=new JsonObject
        {
            ["performanceSource"]=new JsonObject{["tree"]="gm-pk-performance-delivery",["candidate"]=Candidate,["candidateSrcTree"]=CandidateSrc,["candidateNative"]=CandidateNative,["baseline"]=Baseline,["baselineSrcTree"]=BaselineSrc},
            ["acceptanceReview"]=new JsonObject{["tree"]="gm-pk-performance-final-review",["commit"]=Review,["path"]="docs/Pk_Performance_6761600_Independent_Review.md"},
            ["collectorSource"]=new JsonObject{["tree"]="gm-delivery-board",["commit"]=collectorSha,["script"]="scripts/AssemblePerformanceAcceptance.cs",["note"]="仅装配/展示；测量全部来自 Mac 原包，不在 Linux 重测"},
            ["packages"]=new JsonObject{["mac-modeling-performance-6761600-raw.tar.gz"]=PerfTarSha,["mac-modeling-quality-6761600.tar.gz"]=QualityTarSha,["mac-modeling-baseline-failures.tar.gz"]=BaselineTarSha,["rawReportSha256"]=RawReportSha},
            ["rawReport"]="evidence/mac-report.json",
            ["xtScope"]="Oracle 输出文件名每 rep 覆盖；保留原字节的 XT 仅最终 rep2，未签给其它 reps；XT 正文与原归档不公开。",
            ["suiteExit"]="complete-suite 原外部 exit 1（1875/1877）；审计/对比 exit 不代签。",
            ["integrationNote"]="与计算 run 20261111T130936Z-f9c59201 为不同源码、不同 run；不合并标签冒称同一 kernel 集成；不签 f9c59201 event-arrangement 或整体 PK 目标。",
            ["performanceScope"]="同 host 合格样本的限定观察；配对中位数与中位数之比分别标注；planar 回归未归因保留；非全绿 suite 保留。",
            ["familyTaxonomyNote"]="family 为看板既有粗分类（blend|boolean）。",
            ["caseStatusDefinitions"]=new JsonObject{["pass"]="该 case 现行必需检查通过，仅限所列范围。",["fail"]="存在必需检查失败；原 exit 另行独立记录。",["missing"]="未取得/缺口；不推断。",["diagnostic"]="诊断/观察项；不是合格验收通过。"}
        }
    },
    ["cases"]=cases
};
File.WriteAllText(Path.Combine(run,"manifest.json"),manifest.ToJsonString());
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="性能交付验收快照：6761600 工作量优化（限定范围，与 f9c59201 分源未集成）",["manifest"]=$"runs/{runId}/manifest.json"})};
File.WriteAllText(Path.Combine(output,"index.json"),index.ToJsonString());
var referenceBytes=trxBytes.Length;
File.WriteAllText(Path.Combine(evidenceDir,"run-receipt.json"),new JsonObject
{
    ["schemaVersion"]=1,["runId"]=runId,["generatedAt"]=now.ToString("O"),["collectorCommit"]=collectorSha,
    ["performanceCommit"]=Candidate,["acceptanceReview"]=Review,
    ["packageHashes"]=new JsonObject{["perf"]=PerfTarSha,["quality"]=QualityTarSha,["baseline"]=BaselineTarSha},
    ["originalExits"]=new JsonObject{["complete-suite"]=1,["baseline-comparison-runner"]=0,["twenty-oracle-processes"]=0},
    ["referenceOnly"]=(JsonNode?)JsonValue.Create("complete-suite.trx sha256=fce633b02e4d9cdd6654a29a2eb3904150a65cb95ac45650cc4788f099a9769d bytes="+referenceBytes+"（未内联，原件在 incoming 私有目录）"),
    ["validation"]="see validation.json"
}.ToJsonString());
var validation=WebProgressDataValidation.Validate(output);
File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());
var runBytes=Directory.EnumerateFiles(run,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
Console.WriteLine("RUN "+runId+" cases="+cases.Count+" runBytes="+runBytes+" validationErrors="+validation["errors"]!.AsArray().Count);
return validation["errors"]!.AsArray().Count==0?0:2;
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Command(string command,string[] arguments)
{
    var info=new ProcessStartInfo(command){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
    foreach(var arg in arguments)info.ArgumentList.Add(arg);
    using var p=System.Diagnostics.Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();
    if(p.ExitCode!=0)throw new IOException(command+" failed: "+p.StandardError.ReadToEnd());
    return output.Trim();
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
