#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed G+Conic controlled-measurement acceptance snapshot
// (217fc6f8 + accepted review 6a7dfa15) from the immutable Mac original
// package. Values derive from the four 217fc JSONs and raw report; display
// meshes are the material-bound rep-2 originals. No modeling runs here.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Baseline="6616cd60a55e50a5d8a5ada7274895c44609676b";
const string BaselineWrapper="5392a06ea07267f093461876d84fc575849f5ea5";
const string BaselineNative="5b4c6104c600b0194bb543e2dc11efa89e2f0f1f";
const string BaselineSrc="41c8b597c54753ab394bc99a8b2e59bb3d83a756";
const string Candidate="74c29e0fc14cc40bc134a012b0eca3fecedc2947";
const string CandidateWrapper="34ffe46b7010b44c221fae24b2371597e46ede36";
const string CandidateNative="58c1667b0155ec46b53be42b9750c8dbd78a75dd";
const string CandidateSrc="397526ef55c3b0376f3e2fc353375ba7ca18395b";
const string Delivery="217fc6f811e06cbeb936753f77bd65ae4156d8e8";
const string ConicCode="35e61e5e02e319d8f6b9eaba38f880e62f1bbf0c";
const string GCode="3fdc109babe6eda489d985171e3808f956481be1";
const string Review="6a7dfa159c762e90d8180e2f1ed77191b2d52a04";
const string ZipSha="acf7b872cc941708eda0d6b87e45da97d00336adf5167dd2c3fed1e7dee53e0e";
const string ReportSha="c5b2cbba1c05f10e109689aca25c283d33c33f6192815d576ae3086cd3657ec1";
const string MaterialSha="dd6738a8491540680e25545e8a12edd6c4eeae602bff5af34019745611e2665b";
const string LoadedSha="820418ac1b4a1f7ec0e556b7054ba3da3e3d4e9148683ce6d5be5bdf5542e6c6";
const string ReviewEvidenceSha="1ac9334ba2bb176c4895ddad61b97b95b76daee56382a319d724ca758b172ebd";
var pkgRoot=Path.GetFullPath(Option("--package","../temp_docs/incoming/mac-performance/extracted-gconic"),scripts);
var reviewTree=Path.GetFullPath(Option("--review","../../gm-conic-substitution-review"),scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-gconic/data"),scripts);
var docsDir=Path.Combine(pkgRoot,"docs/native-modeling-gconic");
var measureRoot=Path.Combine(pkgRoot,"temp_docs/pk-native-algorithm-work/measurement-6616-gconic-candidate/temp_docs/native-6616-gconic-controlled");
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-gconic217fc");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains("\\"))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Command("git",["-C",reviewTree,"rev-parse","HEAD"])==Review,"review tree HEAD");
Require(Hash(File.ReadAllBytes(Path.Combine(measureRoot,"report.json")))==ReportSha,"raw report hash");
Require(Hash(File.ReadAllBytes(Path.Combine(docsDir,"material-binding.json")))==MaterialSha,"material binding hash");
Require(Hash(File.ReadAllBytes(Path.Combine(docsDir,"loaded-identity.json")))==LoadedSha,"loaded identity hash");
var summary=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"operation-summary.json")))!.AsObject();
var receipt=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"measurement-receipt.json")))!.AsObject();
var binding=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"material-binding.json")))!.AsObject();
var loaded=JsonNode.Parse(File.ReadAllText(Path.Combine(docsDir,"loaded-identity.json")))!.AsObject();
Require(summary["rawReportSha256"]!.GetValue<string>()==ReportSha&&binding["rawReportSha256"]!.GetValue<string>()==ReportSha,"sources bind the raw report");
Require(binding["meshFiles"]!.GetValue<int>()==312&&binding["meshBytes"]!.GetValue<long>()==34973060L&&binding["finalRepetitionXtFiles"]!.GetValue<int>()==44,"material binding counts");
Require(receipt["originalHarnessExit"]!.GetValue<int>()==0&&receipt["processes"]!.GetValue<int>()==20&&receipt["pairs"]!.GetValue<int>()==60,"harness facts");
Require(receipt["headlineWarmSamplesPerSourceOperation"]!.GetValue<int>()==4&&receipt["totalSamplesPerSourceOperation"]!.GetValue<int>()==6&&receipt["coldSamplesPerSourceOperation"]!.GetValue<int>()==2,"warm/cold sample counts");
var report=JsonNode.Parse(File.ReadAllText(Path.Combine(measureRoot,"report.json")))!.AsObject();
Require(report["baselineSha"]!.GetValue<string>()==BaselineWrapper&&report["candidateSha"]!.GetValue<string>()==CandidateWrapper,"report identity fields (both = wrapper trees)");
Require(report["baselineNativeTree"]!.GetValue<string>()==BaselineSrc&&report["candidateNativeTree"]!.GetValue<string>()==CandidateSrc,"report NativeTree fields are whole src trees");
Require(summary["acceptedCandidateSource"]!.GetValue<string>()==Candidate&&summary["acceptedCandidateNativeSubtree"]!.GetValue<string>()==CandidateNative,"summary layering");
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
ShipVerbatim(Path.Combine(docsDir,"operation-summary.json"),"docs/native-modeling-gconic/operation-summary.json (217fc original bytes)","operation-summary.json");
ShipVerbatim(Path.Combine(docsDir,"measurement-receipt.json"),"docs/native-modeling-gconic/measurement-receipt.json (217fc original bytes)","measurement-receipt.json");
ShipVerbatim(Path.Combine(docsDir,"material-binding.json"),"docs/native-modeling-gconic/material-binding.json (217fc original bytes)","material-binding.json");
ShipVerbatim(Path.Combine(docsDir,"loaded-identity.json"),"docs/native-modeling-gconic/loaded-identity.json (217fc original bytes)","loaded-identity.json");
ShipVerbatim(Path.Combine(reviewTree,"docs/native-modeling-algorithm/conic-measurement-independent-evidence.json"),"gm-conic-substitution-review independent evidence (verbatim)","conic-measurement-independent-evidence.json");
ShipVerbatim(Path.Combine(measureRoot,"report.json"),"native-6616-gconic-controlled/report.json (raw report; all 120 timings)","report.json");
// ---- display import: material-bound rep-2 meshes of the first candidate process
var displayBinding=new JsonArray();
JsonArray ImportRep2(string caseId,string opCase)
{
    var sample=binding["samples"]!.AsArray().OfType<JsonObject>().First(s=>s["case"]!.GetValue<string>()==opCase&&s["source"]!.GetValue<string>()=="candidate");
    var rep2=sample["meshes"]!.AsArray().OfType<JsonObject>().Where(m=>m["step"]!.GetValue<string>()=="operation-3").ToArray();
    var prefix=$"cases/{caseId}/sample-0/gconic-217fc";
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
        ["provenanceNote"]=opCase=="shortedge"?"shortedge 实际为 PK seed→GM receive→GM FixBlends→GM XT；默认 mesh.geometryAuthor 字符串不是独立输入构造证明。":opCase=="curved"?"curved tracking/roles missing 保留，角色未知。":"按其实际 runner 来源：GM/PK 以相同数值参数独立构造输入。",
        ["note"]="单 rep2 显示样本 ≠ 60 pairs 统计（每源 2 冷+4 暖）；mesh 为 material-binding 原字节（未改写）；XT 仅各进程末 rep2 且未必双侧，缺则标缺。"
    });
    return new JsonArray((JsonNode)step);
}

// ---- builders
var host=new JsonObject{["os"]="macOS 27.2.0",["architecture"]="Arm64",["cpu"]="Apple M4",["sdk"]="10.0.400",["runtime"]=".NET 10.0.11",["pk"]="Parasolid 38.0.150",["pkToy"]="d820719b6d8510d623972ab9d8efc65edbc312f9"};
var configuration=new JsonObject{["build"]="Release",["nativeAot"]=false,["protocol"]=report["protocol"]!.DeepClone(),["windowUtc"]="2026-11-11T20:14:18.520534Z .. 2026-11-11T20:18:00.112944Z",["systemLoadObserved"]="4.00 .. 5.43 (one-minute load; no exclusive CPU/thermal claims)",["timing"]="capture API interval only"};
JsonNode Perf(JsonObject op)
{
    var warm=op["candidate"]!["warm"]!;
    return new JsonObject{["gmMs"]=JsonValue.Create(warm["gmMedianMs"]!.GetValue<double>()),["pkMs"]=JsonValue.Create(warm["pkMedianMs"]!.GetValue<double>()),["ratioGmOverPk"]=JsonValue.Create(warm["ratioOfMedians"]!.GetValue<double>()),["samples"]=4,["warmup"]=0,["scope"]="headline 取候选 4 个暖样本（每源总 6=2 冷+4 暖全保留，冷 rep0 未丢弃）；gmMs/pkMs=候选暖中位数；ratioGmOverPk=中位数之比（与 gm/pk 一致）。配对 ratio 中位数为另一语义（如 shortedge 340.1703x vs 334.0140x），见 checks，不互换。",["correctnessQualified"]=true,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="G+Conic 合计相对 6616 的同窗口结果；不拆 G/Conic 孤立贡献、不乘旧窗口、不签普遍加速或 PK 目标；物性仅本窗口点值门。",["measurementKind"]="controlled-abba-warm-median",["stableBenchmark"]=false,["qualifiedScope"]="Five fixed independent operations, one controlled window; not general-model or steady-state certification."};
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
        ["parameters"]=new JsonObject{{"baseline",Baseline},{"candidate",Candidate},{"measurementWrapper",CandidateWrapper},{"delivery",Delivery},{"acceptanceReview","../gm-performance-6616-review/docs/native-modeling-algorithm/independent-6616-review.md @ "+Review}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="oracle APIs per operation-summary",["parameters"]=new JsonObject{{"candidateSrcTree",CandidateSrc},{"candidateNativeSubtree",CandidateNative}}},["pk"]=new JsonObject{["name"]="real PK 38.0.150 (Mac)",["parameters"]=new JsonObject{{"oracle","identical numeric parameters"}}}},
        ["steps"]=steps,["checks"]=checks,["performance"]=perf,["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]=sourcePath,["revision"]=Candidate},
        ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=0,["exitCodes"]=new JsonArray(0),["timedOut"]=false}
    });
}
JsonNode NoModelNote()=>JsonValue.Create("报表/诊断类：无实际模型显示，不造模型。");
JsonNode WindowNote()=>JsonValue.Create("单窗口 UTC 2026-11-11T20:14:18.520534Z..20:18:00.112944Z（Apple M4）；load 4.00–5.43；不签排他 CPU/热稳定/PK 总目标；G+Conic 合计收益不拆孤立贡献、不乘旧窗口。");
JsonNode ScopeNote()=>JsonValue.Create("不含 1887/Bezier16aca，不签功能性能合体；旧快照字节原样保留；物性仅本窗口 900 项 PK 点值门（n_cht_pts 唯一诊断），无 modulus 不签 1887 三态/GM 原生 mass；本窗口无下游组合，各步接受范围单列。");
string F(double v)=>v.ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
var evBasic=new JsonArray((JsonNode)new JsonObject{["label"]="03e 原 operation-summary（原字节）",["path"]="evidence/operation-summary.json"},(JsonNode)new JsonObject{["label"]="03e 原 measurement-receipt（原字节）",["path"]="evidence/measurement-receipt.json"},(JsonNode)new JsonObject{["label"]="03e 原 material-binding（原字节）",["path"]="evidence/material-binding.json"},(JsonNode)new JsonObject{["label"]="原 report（原字节，SHA fef5e9a8…）",["path"]="evidence/report.json"},(JsonNode)new JsonObject{["label"]="显示绑定（mesh 哈希/rep2 XT/来源纠正）",["path"]="evidence/display-binding.json"},(JsonNode)new JsonObject{["label"]="来源/收据回执",["path"]="evidence/run-receipt.json"});
foreach(var (opCase,title,family) in new[]{("sphere","gconic / sphere subtract（暖 −11.26% / 1.1269x，配对 PK 0.2805x）","boolean"),("planar","gconic / planar subtract（暖 +65.66% 回退、冷 +18.66% 回退，保留）","boolean"),("curved","gconic / curved block subtract（暖 −84.38% / 6.4015x，配对 75.6049x）","boolean"),("solidblend","gconic / plane-NURBS fillet（暖 −56.56% / 2.3019x，配对 438.1989x）","blend"),("shortedge","gconic / shortedge FixBlends（暖 −87.03% / 7.7080x，配对 340.1703x / 中位比 334.0140x）","blend")})
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
    if(opCase=="shortedge")notes.Add((JsonNode?)JsonValue.Create("来源纠正：shortedge 实际为 PK seed→GM receive→GM FixBlends→GM XT；默认 mesh.geometryAuthor 字符串不是独立构造证明（不改原 mesh 字节）。配对 ratio 中位 340.1703x 与中位数之比 334.0140x 分列，不互换反算。"));
    if(opCase=="curved")notes.Add((JsonNode?)JsonValue.Create("curved tracking/roles missing 保留：拓扑修改角色未知，不补签。"));
    if(opCase=="planar")notes.Add((JsonNode?)JsonValue.Create("planar 暖回退 +65.66% 与冷回退 50.08305→59.41610ms 均保留；亚毫秒波动/离群值（冷 PK 12.033/17.4161ms、shortedge 暖 601.4206–879.774ms）照实，不归因。"));
    AddCase("gconic."+opCase,title,family,"pass",steps,checks,(JsonArray)evBasic.DeepClone(),notes,Perf(op),"scripts/NativeModelingWorkComparison.cs");
}
// work counters (per-rep, no models)
AddCase("gconic.work-counters","gconic / shortedge 工作量逐 rep 计数（Bound/Jacobian/rootcells）","blend","diagnostic",new JsonArray(),
    new JsonArray(
        Check("work-baseline-jac","baseline Jacobian 逐 rep：冷 rep0 526,133 / 暖 rep1 525,315 / 暖 rep2 525,505（分列，不用 rep1 覆盖）","diagnostic",525315,526133,null,"counts","逐 rep 原样保存","evidence/operation-summary.json"),
        Check("work-candidate-jac","candidate 每 rep Jacobian 16,870；bound 每 rep 47,246；rootcells 均 22,371（对侧均 220,895）","diagnostic",16870,525315,null,"counts","工作量计数非排他 CPU；算术不同，比值不等于 CPU 节省","evidence/operation-summary.json"),
        Check("work-bounds","baseline bound 暖 rep2 67,166 与其余 67,137 分列；candidate bound 47,246","diagnostic",47246,67137,null,"requests","","evidence/operation-summary.json"),
        Check("work-retained","不变项：roots 3,518、SSI 4,956、winding 196、budget 失败 0","diagnostic",null,null,null,"counts","","evidence/operation-summary.json"),
        Check("work-unknown","未打印计数不补造","missing",null,null,null,"","如实缺失",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),WindowNote(),ScopeNote()),NullPerf(),"shortedge workload counters");
AddCase("gconic.quality-layering","gconic / 质量分层（suite 1942/1944 非全绿、mirror8 原红保留）","blend","fail",new JsonArray(),
    new JsonArray(
        Check("quality-pairs","60 对每 pair fresh PK receive/full BODY_check/strict/finite + 900 项 PK 点值 mass 全过；60 compare global 21921、local 仅 21931 或带 n_cht_pts 的 21930、96 条 chart 诊断","pass",60,60,0,"pairs","face_pairs 非匹配总数","evidence/report.json"),
        Check("suite-counts","candidate fullsuite 1944 / 1942 通过 / 2 known 失败 / 0 跳过","fail",1942,1944,0,"tests","原 raw 1 保留","evidence/measurement-receipt.json"),
        Check("suite-exit","原 raw exit 1 保留","fail",1,1,0,"exit code","不因本窗口通过改全绿",null),
        Check("mirror-red","mirror+scale 8 case：94f/b47 各 raw 134、PK 0、157/fault 16059/NetworkClip 原红保留，不外推 1887/main","fail",8,0,0,"cases","","evidence/measurement-receipt.json"),
        Check("env-attempt","初始路径拼写 127=计时前 env 错误（非建模失败）；审计 attempt 3x134/旧脚本 143=工具 attempt 不混原建模；checker 0 不补签","diagnostic",127,null,null,"exit code","",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),ScopeNote()),NullPerf(),"quality layering");
AddCase("gconic.identity-loaded","gconic / 加载身份与源码校验（MVID/DLL/PDB/CodeView）","boolean","pass",new JsonArray(),
    new JsonArray(
        Check("identity-loaded","20 进程 start/end 观察器 MVID/DLL/PDB SHA、CodeView GUID/portable-PDB ID 与封存 PE/PDB 逐份一致","pass",20,20,0,"processes","实际 loaded 身份，非文件 hash 代替","evidence/loaded-identity.json"),
        Check("identity-sources","PDB 逐文档 checksum 对固定 clean wrapper Git 源字节核验：baseline 328 / candidate 330 tracked Native 全匹配；各 3 份生成文档单列未核","pass",328,330,0,"files","不签独立可重现编译","evidence/loaded-identity.json"),
        Check("identity-runners","九公共 oracle/Host/BodyCompare/Capture/runner 两侧 hash 逐字相同；五 oracle 去 identity-only 入口后与固定 a73 逐行相同","pass",9,9,0,"files","","evidence/loaded-identity.json"),
        Check("identity-prebuild","10 identity-only 预构建 + 资格正例 + 6 负例全 raw 0；harness/20 子进程原 exit 全 0","pass",0,0,0,"exit code","",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),ScopeNote()),NullPerf(),"loaded identity and source verification");
AddCase("gconic.overall-target","gconic / 总体：单窗口 G+Conic 合计观测，回退与边界照实","boolean","fail",new JsonArray(),
    new JsonArray(
        Check("target-pk","PK 总目标未完成：shortedge 候选配对 GM/PK 中位 340.1703x（中位数之比 334.0140x 分列）","fail",340.1703,null,1,"x","不互换反算","evidence/operation-summary.json"),
        Check("target-regressions","planar 暖 +65.66% 回退与冷 50.08305→59.41610 回退、全部离群值/波动保留","fail",2,0,0,"regressions","不归因、不择优","evidence/operation-summary.json"),
        Check("target-nonclaim","不签：普遍加速、G/Conic 孤立贡献拆分、跨窗口倍率相乘、1887 功能/质量资格、下游组合、全家族、PK 目标","fail",null,null,null,"","排除范围照实","evidence/measurement-receipt.json")),
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
        ["scope"]=new JsonObject{["kind"]="controlled-measurement-acceptance-snapshot",["candidate"]=Candidate,["baseline"]=Baseline,["acceptanceReview"]=Review,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="6616→74c29e0 G+Conic 合计同窗口测量快照；每源 2 冷+4 暖；双 ratio 分列；回退/离群/边界照实。"},
        ["provenance"]=new JsonObject
        {
            ["baselineSource"]=new JsonObject{{"calculation",Baseline},{"wrapper",BaselineWrapper},{"nativeSubtree",BaselineNative},{"srcTree",BaselineSrc},{"note","report/summary 的 NativeTree 字段实际装整个 src 树；四层分列"}},
            ["candidateSource"]=new JsonObject{{"calculation",Candidate},{"wrapper",CandidateWrapper},{"nativeSubtree",CandidateNative},{"srcTree",CandidateSrc}},
            ["measurementDelivery"]=Delivery,["conicCodeMath"]=ConicCode,["gCodeMath"]=GCode,
            ["acceptanceReview"]=new JsonObject{{"tree","gm-conic-substitution-review"},{"branch","review/native-conic-b47"},{"commit",Review},{"path","docs/Native_Conic_Independent_Review.md"},{"independentEvidenceSha256",ReviewEvidenceSha}},
            ["collectorSource"]=new JsonObject{{"tree","gm-delivery-board"},{"commit",collectorSha},{"script","scripts/AssembleGConicAcceptance.cs"},{"note","仅装配/展示；无新 Capture、不重跑 ABBA/建模"}},
            ["package"]=new JsonObject{{"zip","mac-6616-gconic-controlled-original.zip"},{"sha256",ZipSha},{"members",665},{"expandedBytes",131859212},{"rawReportSha256",ReportSha},{"materialBindingSha256",MaterialSha},{"loadedIdentitySha256",LoadedSha},{"note","大原包/DLL/PDB/XT/私有路径不公开；本地 hash 锚定"}},
            ["windowUtc"]="2026-11-11T20:14:18.520534Z .. 2026-11-11T20:18:00.112944Z",
            ["notIncluded"]="1887 functional integration; Bezier 16aca; downstream combinations; full family; main merge; universal speedup claims.",
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
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="6616 受控测量验收快照（元数据修正版）：270e2b6→6616cd6（单窗口，回退/边界照实，未签功能集成）",["manifest"]=$"runs/{runId}/manifest.json"})};
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
