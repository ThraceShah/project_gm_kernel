#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed 6616 controlled-measurement acceptance snapshot from the
// immutable Mac original package and the accepted review appendix. Values come
// from the 03e delivery sources (operation-summary/measurement-receipt/
// material-binding original bytes); display meshes are the material-bound rep-2
// originals. No modeling runs here.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Baseline="270e2b6814afe5a48cd0532e89531f6fb80011be";
const string BaselineSrc="c45cafbbd581642157e963ea55f2c5355fc65211";
const string Candidate="6616cd60a55e50a5d8a5ada7274895c44609676b";
const string CandidateSrc="41c8b597c54753ab394bc99a8b2e59bb3d83a756";
const string CandidateNative="5b4c6104c600b0194bb543e2dc11efa89e2f0f1f";
const string BaselineNativeSubtree="afb8cec5cde9769cb68c470ea8da9591a1804e64";
const string Wrapper="a73bc08dbd39e984dff59dac7bba31a3baf80b4e";
const string Delivery="03e5a8e8ec9a8a297346e42d1b854f6293c8a083";
const string CodeMath="5a4ad5319b77c0944baf598e07dbbce79782a483";
const string Review="1806fd58d8aab59942912eabadcd4324e30fdfb0";
const string ZipSha="97a3164698a16f1c543793289f1b863b1a67e20be7127ed5b0da017e0016120a";
const string ReportSha="fef5e9a8fc33aa9da3d1ab8c932b680f33aa9b0f24214e73e3987c5dfa01e868";
const string MaterialSha="df9a43cf55e2cf978d5c009c3920c2315417899d2a0d99f733ad2267b8173d7f";
var pkgRoot=Path.GetFullPath(Option("--package","../temp_docs/incoming/mac-performance/extracted-6616"),scripts);
var reviewTree=Path.GetFullPath(Option("--review","../../gm-performance-6616-review"),scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-measurement/data"),scripts);
var docsDir=Path.Combine(pkgRoot,"docs/native-modeling-algorithm");
var measureRoot=Path.Combine(pkgRoot,"temp_docs/pk-native-algorithm-work/measurement-6616/temp_docs/native-270-6616-controlled");
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-alg6616");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains('\\'))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Command("git",["-C",reviewTree,"rev-parse","HEAD"])==Review,"review tree HEAD");
Require(Hash(File.ReadAllBytes(Path.Combine(measureRoot,"report.json")))==ReportSha,"raw report hash");
Require(Hash(File.ReadAllBytes(Path.Combine(docsDir,"material-binding.json")))==MaterialSha,"material binding hash");
Require(Hash(File.ReadAllBytes(Path.Combine(pkgRoot,"docs/Pk_Native_Algorithm_Controlled_Measurement.md")))=="3d2d66955d45f3a10586803f170bea510d64bb64a0904b6bdcb2416fe7489d8a","controlled measurement doc hash");
var summary=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"operation-summary.json")))!.AsObject();
var receipt=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"measurement-receipt.json")))!.AsObject();
var binding=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"material-binding.json")))!.AsObject();
Require(summary["rawReportSha256"]!.GetValue<string>()==ReportSha&&binding["rawReportSha256"]!.GetValue<string>()==ReportSha,"sources bind the raw report");
Require(binding["meshFiles"]!.GetValue<int>()==312&&binding["meshBytes"]!.GetValue<long>()==34964660L&&binding["finalRepetitionXtFiles"]!.GetValue<int>()==44,"material binding counts");
Require(receipt["originalHarnessExit"]!.GetValue<int>()==0&&receipt["processes"]!.GetValue<int>()==20&&receipt["operationPairs"]!.GetValue<int>()==60,"harness facts");
var report=JsonNode.Parse(File.ReadAllText(Path.Combine(measureRoot,"report.json")))!.AsObject();
Require(report["baselineSha"]!.GetValue<string>()==Baseline&&report["candidateSha"]!.GetValue<string>()==Wrapper,"report source identities (candidateSha field = measurement wrapper tree)");
Require(report["baselineNativeTree"]!.GetValue<string>()==BaselineSrc&&report["candidateNativeTree"]!.GetValue<string>()==CandidateSrc,"report native-tree fields are whole src trees");
Require(receipt["candidateNativeSubtree"]!.GetValue<string>()==CandidateNative&&summary["acceptedCandidateNativeSubtree"]!.GetValue<string>()==CandidateNative&&receipt["baselineNativeSubtree"]!.GetValue<string>()==BaselineNativeSubtree,"native subtree fields");
var ops=summary["operations"]!.AsArray().OfType<JsonObject>().ToDictionary(o=>o["id"]!.GetValue<string>(),StringComparer.Ordinal);
Require(ops.Count==5,"five measured operations");

// ---- run directory and verbatim 03e sources
var run=Path.Combine(output,"runs",runId);
if(Directory.Exists(run))throw new IOException("Run already exists: immutable "+runId);
var evidenceDir=Path.Combine(run,"evidence");Directory.CreateDirectory(evidenceDir);
var evidenceManifest=new JsonArray();
void ShipVerbatim(string file,string source,string shipped)
{
    var bytes=File.ReadAllBytes(file);File.WriteAllBytes(Path.Combine(evidenceDir,shipped),bytes);
    evidenceManifest.Add((JsonNode)new JsonObject{["original"]=source,["shipped"]="evidence/"+shipped,["bytes"]=bytes.Length,["sha256"]=Hash(bytes),["kind"]="verbatim-original"});
}
string Sanitize(string text)=>text.Replace(pkgRoot,".").Replace(reviewTree,".").Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"~");
void ShipSanitized(string file,string source,string shipped)
{
    var bytes=File.ReadAllBytes(file);var derivative=Encoding.UTF8.GetBytes(Sanitize(Encoding.UTF8.GetString(bytes)));
    File.WriteAllBytes(Path.Combine(evidenceDir,shipped),derivative);
    evidenceManifest.Add((JsonNode)new JsonObject{["original"]=source,["shipped"]="evidence/"+shipped,["originalBytes"]=bytes.Length,["originalSha256"]=Hash(bytes),["shippedSha256"]=Hash(derivative),["kind"]="sanitized-display-derivative"});
}
ShipVerbatim(Path.Combine(docsDir,"operation-summary.json"),"docs/native-modeling-algorithm/operation-summary.json (03e original bytes)","operation-summary.json");
ShipVerbatim(Path.Combine(docsDir,"measurement-receipt.json"),"docs/native-modeling-algorithm/measurement-receipt.json (03e original bytes)","measurement-receipt.json");
ShipVerbatim(Path.Combine(docsDir,"material-binding.json"),"docs/native-modeling-algorithm/material-binding.json (03e original bytes)","material-binding.json");
ShipVerbatim(Path.Combine(docsDir,"quality-receipt.json"),"docs/native-modeling-algorithm/quality-receipt.json (03e original bytes)","quality-receipt.json");
ShipVerbatim(Path.Combine(measureRoot,"report.json"),"native-270-6616-controlled/report.json (raw report)","report.json");

// ---- display import: material-bound rep-2 meshes of the first candidate process
var displayBinding=new JsonArray();
JsonArray ImportRep2(string caseId,string opCase)
{
    var sample=binding["samples"]!.AsArray().OfType<JsonObject>().First(s=>s["case"]!.GetValue<string>()==opCase&&s["source"]!.GetValue<string>()=="candidate");
    var rep2=sample["meshes"]!.AsArray().OfType<JsonObject>().Where(m=>m["step"]!.GetValue<string>()=="operation-3").ToArray();
    var prefix=$"cases/{caseId}/sample-0/measurement-6616";
    var shipped=new JsonArray();
    var step=new JsonObject{["id"]="rep2-display",["title"]="",["gm"]=new JsonObject{["tools"]=new JsonArray()},["pk"]=new JsonObject{["tools"]=new JsonArray()}};
    foreach(var m in rep2)
    {
        var source=Path.Combine(pkgRoot,m["path"]!.GetValue<string>());
        var bytes=File.ReadAllBytes(source);
        Require(Hash(bytes)==m["sha256"]!.GetValue<string>()&&bytes.Length==m["bytes"]!.GetValue<long>(),"material binding mesh identity "+m["path"]!.GetValue<string>());
        var name=Path.GetFileName(source);var target=Path.Combine(run,prefix,"meshes",name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);
        var stage=name.Contains("-before")?"before":name.Contains("-after")?"after":"tool";
        shipped.Add((JsonNode)new JsonObject{["file"]=prefix+"/meshes/"+name,["stage"]=stage,["kernel"]=m["kernel"]!.GetValue<string>(),["sha256"]=Hash(bytes),["bytes"]=bytes.Length});
        var side=m["kernel"]!.GetValue<string>();
        if(stage=="before")step[side]!["before"]=JsonValue.Create(prefix+"/meshes/"+name);
        else if(stage=="after")step[side]!["after"]=JsonValue.Create(prefix+"/meshes/"+name);
        else ((JsonArray)step[side]!["tools"]!).Add((JsonNode?)JsonValue.Create(prefix+"/meshes/"+name));
    }
    var processDir=Path.GetDirectoryName(Path.GetDirectoryName(rep2[0]["path"]!.GetValue<string>()!))!;
    var captureBytes=File.ReadAllBytes(Path.Combine(pkgRoot,processDir,"capture.json"));
    Require(Hash(captureBytes)==sample["captureSha256"]!.GetValue<string>(),"capture identity "+processDir);
    step["title"]=(opCase=="shortedge"?"shortedge 输入=未改 PK seed XT，GM 执行倒圆/导出；":"")+"显示样本："+Path.GetFileName(processDir)+" rep 2（material-binding 原字节；单 rep2 显示≠60 pairs 统计）";
    displayBinding.Add((JsonNode)new JsonObject
    {
        ["case"]=caseId,["operation"]=opCase,["process"]=Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(rep2[0]["path"]!.GetValue<string>()!))),
        ["rep"]=2,["captureSha256"]=sample["captureSha256"]?.DeepClone(),["rawExit"]=sample["rawExit"]?.DeepClone(),
        ["meshes"]=shipped,
        ["finalRepetitionXt"]=sample["finalRepetitionXt"]?.DeepClone(),
        ["xtNote"]="XT 仅各进程最后 rep2 保留，未必 GM/PK 双份；无保留侧显示缺失，不猜测、不补签 rep0/1；XT 正文不公开。",
        ["provenanceNote"]=opCase=="shortedge"?"shortedge 输入实际来自未改 PK seed XT，GM 执行倒圆并导出结果；原 mesh geometryAuthor 默认字符串不是独立构造证明。":"按其实际 runner 来源：GM/PK 以相同数值参数独立构造输入。",
        ["note"]="单 rep2 显示样本 ≠ 60 pairs 统计；mesh 为 material-binding 原字节（未改写）。"
    });
    return new JsonArray((JsonNode)step);
}

// ---- builders
var host=new JsonObject{["os"]="macOS 27.2.0",["architecture"]="Arm64",["cpu"]="Apple M4",["sdk"]="10.0.400",["runtime"]=".NET 10.0.11",["pk"]="Parasolid 38.0.150",["pkToy"]="d820719b6d8510d623972ab9d8efc65edbc312f9"};
var configuration=new JsonObject{["build"]="Release",["nativeAot"]=false,["protocol"]=report["protocol"]!.DeepClone(),["windowUtc"]="2026-11-11T17:42:47.315986Z .. 2026-11-11T17:50:06.660156Z",["systemLoadObserved"]="3.38 .. 15.37 (one-minute load; not exclusive CPU)",["timing"]="capture API interval only"};
JsonNode Perf(JsonObject op)
{
    var warm=op["candidate"]!["warm"]!;
    return new JsonObject{["gmMs"]=JsonValue.Create(warm["gmMedianMs"]!.GetValue<double>()),["pkMs"]=JsonValue.Create(warm["pkMedianMs"]!.GetValue<double>()),["ratioGmOverPk"]=JsonValue.Create(warm["ratioOfMedians"]!.GetValue<double>()),["samples"]=6,["warmup"]=0,["scope"]="同窗口 Mac ABBA，每源 2 冷 + 4 暖；gmMs/pkMs=候选暖中位数；ratioGmOverPk=中位数之比（与 gm/pk 一致）。配对 ratio 中位数为另一语义，见 checks，两者不能互换。",["correctnessQualified"]=true,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="固定 270/6616 窗口单轮观测；不签排他 CPU、热稳定或 PK 总目标；物性资格仅 270 点值门（见 meas.mass-qualification）。",["measurementKind"]="fresh-process-cold-median",["stableBenchmark"]=false,["qualifiedScope"]="Five fixed successful operations, one window; not general-model or steady-state certification."};
}
JsonNode NullPerf()=>new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]="报表/计数/边界类：无耗时测量。",["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="不由此推导倍率。",["measurementKind"]="not-run",["stableBenchmark"]=false,["qualifiedScope"]="Evidence/report scope only."};
JsonObject Check(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,string? evidence=null)
    =>new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=gm,["pk"]=pk,["absDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value)):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=evidence,["note"]=note};
var cases=new JsonArray();
void AddCase(string id,string title,string family,string status,JsonArray steps,JsonArray checks,JsonArray evidence,JsonArray notes,JsonNode perf,string sourcePath)
{
    cases.Add((JsonNode)new JsonObject
    {
        ["id"]=id,["title"]=title,["family"]=family,["operation"]="controlled measurement (270 vs 6616)",["status"]=status,
        ["parameters"]=new JsonObject{{"baseline",Baseline},{"candidate",Candidate},{"measurementWrapper",Wrapper},{"delivery",Delivery},{"acceptanceReview","../gm-performance-6616-review/docs/native-modeling-algorithm/independent-6616-review.md @ "+Review}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="oracle APIs per operation-summary",["parameters"]=new JsonObject{{"candidateSrcTree",CandidateSrc},{"candidateNativeSubtree",CandidateNative}}},["pk"]=new JsonObject{["name"]="real PK 38.0.150 (Mac)",["parameters"]=new JsonObject{{"oracle","identical numeric parameters"}}}},
        ["steps"]=steps,["checks"]=checks,["performance"]=perf,["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]=sourcePath,["revision"]=Candidate},
        ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=0,["exitCodes"]=new JsonArray(0),["timedOut"]=false}
    });
}
JsonNode NoModelNote()=>JsonValue.Create("报表/诊断类：无实际模型显示，不造模型。");
JsonNode WindowNote()=>JsonValue.Create("单窗口 UTC 2026-11-11T17:42:47.315986Z..17:50:06.660156Z（Apple M4）；load 3.38–15.37；不签排他 CPU/热稳定/PK 总目标；不从一轮实测推广普遍倍率。");
JsonNode ScopeNote()=>JsonValue.Create("未含 4794/60be/G241b，不签功能已集成；旧 676 快照与 f9/Blend 字节原样保留；本窗口原 270 公共 Capture 物性仅点值门，无模量三态资格。");
string F(double v)=>v.ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
var evBasic=new JsonArray((JsonNode)new JsonObject{["label"]="03e 原 operation-summary（原字节）",["path"]="evidence/operation-summary.json"},(JsonNode)new JsonObject{["label"]="03e 原 measurement-receipt（原字节）",["path"]="evidence/measurement-receipt.json"},(JsonNode)new JsonObject{["label"]="03e 原 material-binding（原字节）",["path"]="evidence/material-binding.json"},(JsonNode)new JsonObject{["label"]="原 report（原字节，SHA fef5e9a8…）",["path"]="evidence/report.json"},(JsonNode)new JsonObject{["label"]="显示绑定（mesh 哈希/rep2 XT/来源纠正）",["path"]="evidence/display-binding.json"},(JsonNode)new JsonObject{["label"]="来源/收据回执",["path"]="evidence/run-receipt.json"});
foreach(var (opCase,title,family) in new[]{("sphere","meas / sphere subtract（冷/暖全值）","boolean"),("planar","meas / planar subtract（暖回退 +6.23% 保留）","boolean"),("curved","meas / curved block subtract（暖 −5.86%、冷回退保留）","boolean"),("solidblend","meas / plane-NURBS fillet（暖 +0.32% 保留）","blend"),("shortedge","meas / shortedge FixBlends（暖 −64.44% / 2.81198x，仍 3423.49713x PK）","blend")})
{
    var op=ops[opCase];
    var steps=ImportRep2("meas."+opCase,opCase);
    string Row(string src,string temp)
    {
        var s=op[src]![temp]!;
        return $"n={s["n"]!.GetValue<int>()} GM中位 {F(s["gmMedianMs"]!.GetValue<double>())} [{F(s["gmMinMs"]!.GetValue<double>())},{F(s["gmMaxMs"]!.GetValue<double>())}] PK中位 {F(s["pkMedianMs"]!.GetValue<double>())} 配对ratio中位 {F(s["ratioMedian"]!.GetValue<double>())} 中位数之比 {F(s["ratioOfMedians"]!.GetValue<double>())}";
    }
    var checks=new JsonArray(
        Check("quality-"+opCase,"每 rep 质量门（API/step/error/status/body/count/点值物性、fresh XT 真实 PK 接收、BODY_compare/BODY_check、有限曲线）通过","pass",6,6,0,"pairs","60 对全过、2028 检查 pass；60 compare 保持 max_diffs=1024 等原 mask；点值物性门见 meas.mass-qualification","evidence/operation-summary.json"),
        Check("warm-"+opCase,"暖（每源 4）：基线 "+Row("baseline","warm")+"；候选 "+Row("candidate","warm"),"pass",op["candidate"]!["warm"]!["gmMedianMs"]!.GetValue<double>(),op["baseline"]!["warm"]!["gmMedianMs"]!.GetValue<double>(),null,"ms","右列=基线暖 GM 中位数；配对 ratio 中位数与中位数之比分列不互换","evidence/operation-summary.json"),
        Check("cold-"+opCase,"冷（每源 2）：基线 "+Row("baseline","cold")+"；候选 "+Row("candidate","cold"),"diagnostic",null,null,null,"ms","冷数据未删（含离群值）；冷配对比值中位数与时间中位数之比差异大，不能互换","evidence/operation-summary.json"),
        Check("change-"+opCase,"GM 暖变化 "+F(op["gmWarmChangePercent"]!.GetValue<double>())+"%；暖加速 "+F(op["gmWarmSpeedup"]!.GetValue<double>())+"x","diagnostic",op["gmWarmChangePercent"]!.GetValue<double>(),null,null,"%","回退/微差均保留不归因；这是该固定窗口观测","evidence/operation-summary.json"),
        Check("exit-"+opCase,"该操作 2 进程原 exit 均 0（report/log 记录）","pass",0,0,0,"exit code","子进程退出来自 report/log，并非另一组独立 exit 文件",null));
    var notes=new JsonArray(WindowNote(),ScopeNote(),
        (JsonNode?)JsonValue.Create("显示绑定见 evidence/display-binding.json；单 rep2 显示 ≠ 60 pairs 统计；每 rep 的 mesh/质量/计时各自存在。"));
    if(opCase=="shortedge")notes.Add((JsonNode?)JsonValue.Create("输入来源纠正：shortedge 的 GM 输入实际来自未改 PK seed XT，GM 真实执行倒圆并导出；原 mesh geometryAuthor 默认字符串不是独立构造证明（不改原 mesh 字节）。"));
    if(opCase=="curved")notes.Add((JsonNode?)JsonValue.Create("curved tracking=false、semanticCoverage=missing：拓扑修改角色未知，不展示为全部 modified 已证。"));
    AddCase("meas."+opCase,title,family,"pass",steps,checks,(JsonArray)evBasic.DeepClone(),notes,Perf(op),"scripts/NativeModelingWorkComparison.cs");
}
AddCase("meas.work-counters","meas / shortedge 工作量计数（Bound/Jacobian/residual 对照）","blend","diagnostic",new JsonArray(),
    new JsonArray(
        Check("work-bounds","暖 rep1 求界 223,331→67,137；rep2 候选求界 67,166（分列，不用 rep1 覆盖全样本）","diagnostic",67137,223331,null,"requests","操作计数，不是排他 CPU 或可删除耗时","evidence/operation-summary.json"),
        Check("work-jacobian","暖 rep1 Jacobian 1,597,561→525,315；residual 27,774→7,023","diagnostic",525315,1597561,null,"counts","","evidence/operation-summary.json"),
        Check("work-retained","不变项：根单元 220,895、根 3,518、SSI 4,956、winding 196、budget 失败 0、bound requests 275,525","diagnostic",null,null,null,"counts","","evidence/operation-summary.json"),
        Check("work-unknown","negative-hit/XYZ-column 计数本窗口未打印，不补造","missing",null,null,null,"","如实缺失",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),WindowNote(),ScopeNote()),NullPerf(),"shortedge workload counters");
AddCase("meas.quality-suite","meas / 质量与套件结算（1912/1914 非全绿、原 exit 1 保留）","blend","fail",new JsonArray(),
    new JsonArray(
        Check("quality-checks","60 对质量检查 2028 项 pass、60 compare 原 mask、96 条 chart 诊断、BODY_check/计数/API/blend-fault 重核","pass",2028,2028,0,"checks","face_pairs 不作成功匹配总数","evidence/quality-receipt.json"),
        Check("suite-counts","套件 1914 总 / 1912 通过 / 2 失败 / 0 跳过","fail",1912,1914,0,"tests","两项为基线既有失败","evidence/quality-receipt.json"),
        Check("suite-exit","原 exit 1 保留；270 focused 两失败原 exit 1 保留","fail",1,1,0,"exit code","不因 60 对通过改全绿",null),
        Check("harness","正式 harness 原 exit 0；20 子进程原 exit 全 0（report/log 记录）","pass",20,20,0,"processes","与 setup 失败分开","evidence/report.json"),
        Check("setup-attempts","setup 首 CLI 参数错误 exit 134；首次候选预构建外部 exit 缺失 + 60s 工具超时（计时前，原日志保留）","diagnostic",134,null,null,"exit code","checker 0 不补签这些失败","evidence/measurement-receipt.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),ScopeNote()),NullPerf(),"quality and suite settlement");
AddCase("meas.mass-qualification","meas / 物性资格边界（仅 270 点值门，无模量三态）","blend","missing",new JsonArray(),
    new JsonArray(
        Check("mass-point-gate","本窗口物性=真实 PK 对 GM fresh XT 与 PK reference 的 15 项点值比较（accuracy=.99999999，abs(GM−PK)/(1+abs(PK))<1e-7）","pass",15,15,0,"components","900 项点值比较由独审重算阈值","evidence/material-binding.json"),
        Check("mass-modulus","原 capture 无 modulus/uncertainty 字段；不是 GM 原生物性 API 模量证明；不能称 60be/4794 模量三态全 Verified","missing",null,null,null,"","不改写原物性统计、不加历史 adapter","evidence/measurement-receipt.json"),
        Check("mass-bounds","shortedge 失败时模量诊断分支未触发（日志无 MASS BOUNDS）","missing",null,null,null,"","如实缺失",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),ScopeNote()),NullPerf(),"mass qualification boundary");
AddCase("meas.overall-target","meas / 总体：单窗口观测接受，PK 总目标未完成","boolean","fail",new JsonArray(),
    new JsonArray(
        Check("target-pk","PK 总目标未完成：shortedge 候选配对 GM/PK 中位 3423.49713x（中位数之比 3424.36754x 分列）","fail",3423.49713,null,1,"x","两语义不互换","evidence/operation-summary.json"),
        Check("target-regressions","三项暖回退（sphere +2.02%、planar +6.23%、NURBS +0.32%）与 curved 冷回退、冷离群值全部保留","fail",3,0,0,"regressions","微差不归因",null),
        Check("target-nonclaim","不签：排他 CPU、热稳定、普遍倍率、功能集成（4794/60be/G241b 未含）、套件全绿","fail",null,null,null,"","排除范围照实","evidence/measurement-receipt.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),WindowNote(),ScopeNote()),NullPerf(),"overall measurement status");

// ---- binding + manifest + receipt
File.WriteAllText(Path.Combine(evidenceDir,"display-binding.json"),new JsonObject{["schemaVersion"]=1,["note"]="Material-bound rep-2 display samples (first candidate process per operation) from the immutable 6616 package; single rep-2 display != 60-pair statistics; original mesh bytes unchanged.",["samples"]=displayBinding}.ToJsonString());
var manifest=new JsonObject
{
    ["schemaVersion"]=1,
    ["run"]=new JsonObject
    {
        ["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["pkVersion"]="38.0.150",
        ["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),
        ["scope"]=new JsonObject{["kind"]="controlled-measurement-acceptance-snapshot",["candidate"]=Candidate,["baseline"]=Baseline,["acceptanceReview"]=Review,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="270→6616 固定窗口正式测量快照；每源 2 冷 + 4 暖；双 ratio 语义分列；回退/未决/边界照实。"},
        ["provenance"]=new JsonObject
        {
            ["baselineSource"]=new JsonObject{["commit"]=Baseline,["srcTree"]=BaselineSrc,["nativeSubtree"]=BaselineNativeSubtree,["note"]="report 的 baselineNativeTree/candidateNativeTree 字段是整个 src 子树（c45cafbb/41c8b597）；Native 项目子树另记；report.candidateSha 字段是测量包装 a73bc08 树"},
            ["candidateSource"]=new JsonObject{["commit"]=Candidate,["srcTree"]=CandidateSrc,["nativeSubtree"]=CandidateNative},
            ["measurementWrapper"]=new JsonObject{["commit"]=Wrapper,["note"]="只还原五 oracle 到 270；report 从该树产出"},["measurementDelivery"]=Delivery,["codeMathAcceptance"]=CodeMath,
            ["acceptanceReview"]=new JsonObject{["tree"]="gm-performance-6616-review",["branch"]="review/pk-algorithm-6616",["commit"]=Review,["path"]="docs/native-modeling-algorithm/independent-6616-review.md"},
            ["collectorSource"]=new JsonObject{["tree"]="gm-delivery-board",["commit"]=collectorSha,["script"]="scripts/AssembleMeasurementAcceptance.cs",["note"]="仅装配/展示；无新 Capture、不重跑 Mac 测量"},
            ["package"]=new JsonObject{["zip"]="mac-controlled-270-6616-original.zip",["sha256"]=ZipSha,["rawReportSha256"]=ReportSha,["materialBindingSha256"]=MaterialSha,["members"]=490,["expandedBytes"]=36430726},
            ["windowUtc"]="2026-11-11T17:42:47.315986Z .. 2026-11-11T17:50:06.660156Z",
            ["notIncluded"]="Experimental 241b rows; unaccepted functional 4794/60be; no AOT/Windows; no main integration or functionality claims.",
            ["ratioSemantics"]="配对 ratio 中位数（ratioMedian）与中位数之比（ratioOfMedians）分列显示，不互换；performance.ratioGmOverPk=后者。",
            ["massQualification"]="仅 270 公共 Capture 的 15 项点值门（accuracy=.99999999）；无 modulus 字段；不称模量三态全 Verified。",
            ["provenanceCorrections"]="原 mesh geometryAuthor 默认字符串不是输入独立构造证明；shortedge GM 输入来自未改 PK seed XT（GM 执行倒圆/导出）；curved tracking/角色未知。",
            ["familyTaxonomyNote"]="family 为看板既有粗分类。",
            ["caseStatusDefinitions"]=new JsonObject{["pass"]="该 case 现行必需检查通过，仅限所列范围。",["fail"]="存在必需检查失败；原 exit 独立记录。",["missing"]="缺失/未取得资格；不推断。",["diagnostic"]="诊断/观察项；不是验收通过。"}
        }
    },
    ["cases"]=cases
};
File.WriteAllText(Path.Combine(run,"manifest.json"),manifest.ToJsonString());
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="6616 受控测量验收快照：270e2b6→6616cd6（单窗口，回退/边界照实，未签功能集成）",["manifest"]=$"runs/{runId}/manifest.json"})};
File.WriteAllText(Path.Combine(output,"index.json"),index.ToJsonString());
var runBytes=Directory.EnumerateFiles(run,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
File.WriteAllText(Path.Combine(evidenceDir,"run-receipt.json"),new JsonObject
{
    ["schemaVersion"]=1,["runId"]=runId,["generatedAt"]=now.ToString("O"),["collectorCommit"]=collectorSha,
    ["baseline"]=Baseline,["candidate"]=Candidate,["acceptanceReview"]=Review,
    ["originalExits"]=new JsonObject{["harness"]=0,["processes20"]=0,["setup-first-cli"]=134,["first-prebuild"]="missing (60s tool timeout noted separately)",["suite"]=1,["baseline-focused2"]=1},
    ["runBytesBeforeReceiptAndValidation"]=runBytes,["evidenceFiles"]=evidenceManifest,
    ["validation"]="see validation.json"
}.ToJsonString());
var validation=WebProgressDataValidation.Validate(output);
File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());
Console.WriteLine("RUN "+runId+" cases="+cases.Count+" runBytes="+(runBytes+new FileInfo(Path.Combine(evidenceDir,"run-receipt.json")).Length+new FileInfo(Path.Combine(run,"validation.json")).Length)+" validationErrors="+validation["errors"]!.AsArray().Count);
return validation["errors"]!.AsArray().Count==0?0:2;
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Command(string command,string[] arguments)
{
    var info=new ProcessStartInfo(command){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
    foreach(var arg in arguments)info.ArgumentList.Add(arg);
    using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();
    if(p.ExitCode!=0)throw new IOException(command+" failed: "+p.StandardError.ReadToEnd());
    return output.Trim();
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
