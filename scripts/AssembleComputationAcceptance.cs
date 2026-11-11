#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed computation acceptance snapshot run (candidate f9c59201 +
// accepted review 4c7646e) from original execution evidence and the
// same-instance display capture of the accepted source. Original evidence
// bytes stay untouched in their source trees; logs shipped in the run are
// sanitized display derivatives, each anchored by original path/bytes/SHA256
// in evidence-manifest.json. No modeling or math runs here.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Candidate="f9c59201c0b9c5d103dee45124e3f43ca59a0921";
const string Review="4c7646e1085e6fa786b2f335cabc7c086c008e13";
const string EvidenceIndexSha="94752804be4ac709fbb4c1104b110b754787121c79a695b6f39723a5a12898c5";
const string ReportSha="829027ad0c125036dbb1b40c8c7ca29f29bfa2859109d9acc519b853107b2685";
var evidenceRoot=Path.GetFullPath(Option("--evidence","../../gm-pk-native-delivery/temp_docs/pk-native-delivery"),scripts);
var evidenceTree=Path.GetFullPath("../../gm-pk-native-delivery",scripts);
var reviewTree=Path.GetFullPath(Option("--review","../../gm-pk-native-final-review"),scripts);
var captureRoot=Path.GetFullPath(Option("--capture","../../gm-board-capture-f9c5920/temp_docs/board-capture"),scripts);
var captureTree=Path.GetFullPath("../../gm-board-capture-f9c5920",scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-acceptance/data"),scripts);
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-f9c59201");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains('\\'))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);

// ---- fixed identity checks (assembly refuses to run on mismatch)
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Hash(File.ReadAllBytes(Path.Combine(evidenceRoot,"evidence-final.sha256")))==EvidenceIndexSha,"evidence-final.sha256 index hash");
Require(Hash(File.ReadAllBytes(Path.Combine(reviewTree,"docs/reviews/advanced-modeling/pk-native-final-review.md")))==ReportSha,"review report hash");
Require(Command("git",["-C",reviewTree,"rev-parse","HEAD"])==Review,"review tree HEAD");
Require(File.ReadAllText(Path.Combine(captureRoot,"capture-source.txt")).Split(' ')[0]==Candidate,"capture source HEAD");
var exits=new Dictionary<string,int>(StringComparer.Ordinal);
foreach(var file in Directory.EnumerateFiles(evidenceRoot,"*.exit"))exits[Path.GetFileNameWithoutExtension(file)]=int.Parse(File.ReadAllText(file).Trim());
Require(exits.GetValueOrDefault("full")==0&&exits.GetValueOrDefault("transverse")==134&&exits.GetValueOrDefault("probes-self")==134&&exits.GetValueOrDefault("probes-import")==134,"original red/exit facts");

// ---- derived extracts of the original logs (analysis derivatives)
string Original(string name)=>File.ReadAllText(Path.Combine(evidenceRoot,name));
string Sanitize(string text)=>text.Replace(evidenceTree,".").Replace(captureTree,".").Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"~");
string[] Lines(string name,params string[] prefixes)=>Original(name).Split('\n').Where(l=>prefixes.Any(p=>l.StartsWith(p,StringComparison.Ordinal))).Select(l=>l.Trim()).ToArray();
var transverseMass=Lines("transverse.log","MASS ","FAIL ");_ = transverseMass;
var standardLines=Lines("standard.log","PASS ","MASS ","FAIL ");
var opSummaries=new Dictionary<string,JsonArray>(StringComparer.Ordinal);
void Summarize(string name,params string[] prefixes)=>opSummaries[name]=new JsonArray(Lines(name,prefixes).Select(l=>(JsonNode?)JsonValue.Create(l)).ToArray());
Summarize("regular-bore.log","PASS ","FAIL ");
Summarize("support-chain.log","PASS ","FAIL ");
Summarize("separated-network.log","PASS ","FAIL ");
Summarize("input-rigid.log","PASS ","FAIL ");
Summarize("input-mirror.log","PASS ","FAIL ");
Summarize("corner.log","PASS ","FAIL ");
Summarize("analytic-local.log","PASS ","FAIL ","NotRun:");
Summarize("comparator-controls.log","PASS ","FAIL ");
var matrixNames=Original("matrix-96-cases.txt").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
var shardSummary=new JsonArray();
for(var i=0;i<8;i++)
{
    var log=Original($"matrix-shard-{i}.log");
    shardSummary.Add((JsonNode)new JsonObject
    {
        ["shard"]=$"matrix-shard-{i}",["originalExit"]=exits[$"matrix-shard-{i}"],
        ["passLine"]=log.Split('\n').Select(l=>l.Trim()).FirstOrDefault(l=>l.StartsWith("PASS cases=",StringComparison.Ordinal)),
        ["caseLines"]=log.Split('\n').Count(l=>l.StartsWith("CASE ",StringComparison.Ordinal)),
        ["selfGeometryLines"]=log.Split('\n').Count(l=>l.StartsWith("SELF GEOMETRY",StringComparison.Ordinal)),
        ["bytes"]=new FileInfo(Path.Combine(evidenceRoot,$"matrix-shard-{i}.log")).Length,
        ["sha256"]=Hash(File.ReadAllBytes(Path.Combine(evidenceRoot,$"matrix-shard-{i}.log")))
    });
}

// ---- run directory skeleton and shipped evidence
var run=Path.Combine(output,"runs",runId);
if(Directory.Exists(run))throw new IOException("Run already exists: immutable "+runId);
var evidenceDir=Path.Combine(run,"evidence");Directory.CreateDirectory(evidenceDir);
var manifestFiles=new JsonArray();
void Ship(string originalName,string shippedName)
{
    var source=Path.Combine(evidenceRoot,originalName);var bytes=File.ReadAllBytes(source);
    var derivative=Sanitize(Encoding.UTF8.GetString(bytes));
    var derivativeBytes=Encoding.UTF8.GetBytes(derivative);
    File.WriteAllText(Path.Combine(evidenceDir,shippedName),derivative);
    manifestFiles.Add((JsonNode)new JsonObject{["original"]="../gm-pk-native-delivery/temp_docs/pk-native-delivery/"+originalName,["shipped"]="evidence/"+shippedName,["originalBytes"]=bytes.Length,["originalSha256"]=Hash(bytes),["shippedBytes"]=derivativeBytes.Length,["shippedSha256"]=Hash(derivativeBytes),["kind"]="sanitized-display-derivative"});
}
var inlineBig=new[]{"regular-bore.log","corner.log"};
var referenceOnly=new[]{"standard.log","input-rigid.log","input-mirror.log"};
foreach(var name in new[]{"full.log","transverse.log","probes-self.log","probes-import.log","intersections.log","support-chain.log","separated-network.log","analytic-local.log","comparator-controls.log","consumer-forward.log","consumer-reverse.log","consumer-inject.log"}.Concat(inlineBig))Ship(name,name.EndsWith(".txt")?name+".log":name);
Ship("matrix-96-cases.txt","matrix-96-cases.log");
Ship("evidence-final.sha256","evidence-final-index.log");
foreach(var name in referenceOnly)
{
    var bytes=File.ReadAllBytes(Path.Combine(evidenceRoot,name));
    manifestFiles.Add((JsonNode)new JsonObject{["original"]="../gm-pk-native-delivery/temp_docs/pk-native-delivery/"+name,["shipped"]=null,["originalBytes"]=bytes.Length,["originalSha256"]=Hash(bytes),["kind"]="reference-only-30MiB-package-budget; extracted result lines shipped in operations-summaries.json; originals kept byte-identical in the computation evidence tree"});
}

// ---- same-instance display capture (accepted source, report standard command)
var captureDir=Path.Combine(run,"cases","lifecycle.standard-self","sample-0");
Directory.CreateDirectory(captureDir);
foreach(var file in Directory.EnumerateFiles(Path.Combine(captureRoot,"capture"),"*",SearchOption.AllDirectories))
{
    var relative=Path.GetRelativePath(Path.Combine(captureRoot,"capture"),file);
    var target=Path.Combine(captureDir,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
}
File.Copy(Path.Combine(captureRoot,"oracle.log"),Path.Combine(captureDir,"oracle.log"));
var captureLogBytes=File.ReadAllBytes(Path.Combine(captureDir,"oracle.log"));
File.WriteAllText(Path.Combine(captureDir,"oracle.log"),Sanitize(Encoding.UTF8.GetString(captureLogBytes)));
var capture=JsonNode.Parse(File.ReadAllText(Path.Combine(captureDir,"capture.json")))!.AsObject();
foreach(var step in capture["steps"]!.AsArray())foreach(var side in new[]{"gm","pk"})
{
    foreach(var key in new[]{"before","after"})if(step![side]![key]!=null)step[side]![key]="cases/lifecycle.standard-self/sample-0/"+step[side]![key]!.GetValue<string>();
    foreach(var key in new[]{"tools","afterBodies"})if(step![side]![key] is JsonArray paths)for(var p=0;p<paths.Count;p++)paths[p]="cases/lifecycle.standard-self/sample-0/"+paths[p]!.GetValue<string>();
}
var execution=new JsonObject
{
    ["exitCode"]=0,["timedOut"]=false,
    ["startedUtc"]=File.ReadAllText(Path.Combine(captureRoot,"start.utc")).Trim(),
    ["finishedUtc"]=File.ReadAllText(Path.Combine(captureRoot,"end.utc")).Trim(),
    ["arguments"]=new JsonArray(File.ReadAllText(Path.Combine(captureRoot,"argv.txt")).Split('\n').Where(l=>l.Length>0).Select(l=>(JsonNode?)JsonValue.Create(l)).ToArray()),
    ["worktree"]="gm-board-capture-f9c5920 (detached, clean)",["worktreeHead"]=Candidate,
    ["sourceHashes"]=new JsonObject{["scripts/ShortEdgeBlendOracle.cs"]="af77db29008f9751c2a159e69d26ddc08b7ca6350baffc53d6c5a9cf20e99828",["scripts/RollingBooleanConsumerOracle.cs"]="e65a838b1544fae9843316c9b8674b4db1e38804148842ffe91c1a798965a981",["scripts/ParasolidBodyCompare.cs"]="38fdcac891ba99221bb778af8848761daf1b847d74db0bf6de12d714c290635e"},
    ["logSha256"]=Hash(captureLogBytes),["logOriginalSha256Note"]="oracle.log shipped sanitized; original byte-identical log kept in the capture worktree"
};
File.WriteAllText(Path.Combine(captureDir,"execution.json"),execution.ToJsonString());

// ---- check/case builders
var host=new JsonObject{["os"]="Linux x86_64 (qingyu2)",["assemblyHost"]="same host as author, review and capture runs",["sdk"]="10.0.302",["runtime"]=".NET 10.0.10",["pk"]="Parasolid 38.0.150 via pinned PskernelSharp d820719b6d8510d623972ab9d8efc65edbc312f9"};
var configuration=new JsonObject{["build"]="Release",["nativeAot"]=false,["captureCommand"]="ShortEdgeBlendOracle --output-name=board-f9c5920-standard --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership",["timing"]="no formal performance measurement in this run",["parallelism"]="author suites serial"};
JsonNode Perf()=>new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]="No formal performance acceptance this round: Linux diagnostic wall times are not performance evidence; unqualified ratios stay null.",["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="性能线（Mac 同 host GM/PK）另行交付，未完成前不签任何倍率或加速。",["measurementKind"]="not-run",["stableBenchmark"]=false,["qualifiedScope"]="Correctness/acceptance scope only; no steady-state performance certification."};
JsonObject Check(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,string? evidence=null)
    =>new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=gm,["pk"]=pk,["absDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value)):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=evidence,["note"]=note};
var cases=new JsonArray();
void AddCase(string id,string title,string family,string operation,string status,JsonArray checks,JsonArray evidence,JsonArray notes,string sourcePath,JsonObject executionInfo)
{
    cases.Add((JsonNode)new JsonObject
    {
        ["id"]=id,["title"]=title,["family"]=family,["operation"]=operation,["status"]=status,
        ["parameters"]=new JsonObject{{"candidate",Candidate},{"acceptanceReport","../gm-pk-native-final-review/docs/reviews/advanced-modeling/pk-native-final-review.md @ "+Review}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]=operation,["parameters"]=new JsonObject{{"sourceTree","gm-pk-native-delivery @ "+Candidate}}},["pk"]=new JsonObject{["name"]=operation,["parameters"]=new JsonObject{{"oracle","real Parasolid 38.0.150, same parameters"}}}},
        ["steps"]=new JsonArray(),["checks"]=checks,["performance"]=Perf(),["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]=sourcePath,["revision"]=Candidate},
        ["execution"]=executionInfo
    });
}
JsonArray Ev(params (string Label,string Path)[] items)=>new JsonArray(items.Select(i=>(JsonNode)new JsonObject{["label"]=i.Label,["path"]=i.Path}).ToArray());
JsonObject Exec(params int[] codes)=>new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=codes[^1],["exitCodes"]=new JsonArray(codes.Select(c=>(JsonNode?)JsonValue.Create(c)).ToArray()),["timedOut"]=false};

// 1. 96 self-Fix parameter matrix
AddCase("computation.matrix-96-self-fix","computation / 96 self Fix 参数矩阵（角4×半径3×容差2×顺序2×update2）","blend","PK_EDGE_set_blend_constant + fix blends","pass",
    new JsonArray(
        Check("matrix-cases","96 唯一参数 case 集合（与预期矩阵完全相同）","pass",matrixNames.Length,96,0,"cases","names locked in evidence/matrix-96-cases.log","evidence/matrix-96-cases.log"),
        Check("matrix-shards","8 个 shard 原外部 exit 全 0","pass",8,8,0,"exit","原 matrix.exit=143 为初次串行被杀，未计入 96 完成数","evidence/matrix-shards-summary.json"),
        Check("matrix-self-fix","每 case self Fix API error=0","pass",96,96,0,"error","由 shard PASS cases=12×8 与审计核对","evidence/matrix-shards-summary.json"),
        Check("matrix-managed-check","managed full BODY_check 0 faults","pass",0,0,0,"faults","接受报告 4c7646e 审计逐 case 核对",null),
        Check("matrix-finite","有限 ICurve/双侧 UV、mapped geometry、原 mass 记录齐全，无 FAIL","pass",null,null,null,"","审计 EvidenceAudit 外部 exit 0；4c7646e 报告收据 67/67",null),
        Check("matrix-serial-143","初次串行 matrix 运行外部 143（被杀）","diagnostic",null,null,null,"exit code","保留原 exit，不计入通过数","evidence/matrix-shards-summary.json")),
    Ev(("逐 shard/逐 case 汇总（派生）","evidence/matrix-shards-summary.json"),("96 case 名单（原字节副本）","evidence/matrix-96-cases.log"),("原证据索引 726 条（原字节副本）","evidence/evidence-final-index.log"),("全部原件哈希与未内联清单","evidence/evidence-manifest.json")),
    new JsonArray((JsonNode?)JsonValue.Create("范围：96 个 self 构造/几何 case 完整通过；不等于 96 个均跑生命周期，也不等于每 case 启用另行可选 native fin box/face domain 检查。"),
        (JsonNode?)JsonValue.Create("8 个 shard 原 log 共 7,567,422 bytes 未内联（固定 30MiB 静态包容量门）；逐文件 SHA256/大小/原路径见 evidence-manifest.json，原件保持在计算证据树 temp_docs/pk-native-delivery/，未删除。")),
    "scripts/ShortEdgeBlendOracle.cs",Exec(0,0,0,0,0,0,0,0));

// 2. full regression 1885
AddCase("computation.regression-1885","computation / 全量回归 1885（作者实际完成记录）","blend","tests/KernelTests full suite","pass",
    new JsonArray(
        Check("regression-total","回归总量","pass",1885,1885,0,"tests","原 full.log：Total tests: 1885","evidence/full.log"),
        Check("regression-passed","通过数","pass",1885,1885,0,"tests","原 full.log：Passed: 1885","evidence/full.log"),
        Check("regression-exit","原外部退出码","pass",0,0,0,"exit code","full.exit=0",null),
        Check("regression-time","原墙钟 19.5104 分钟（含构建/排队，inclusive）","diagnostic",null,null,null,"minutes","inclusive 墙钟，不是排他 CPU 或性能证据","evidence/full.log")),
    Ev(("原 full.log（脱敏副本）","evidence/full.log")),
    new JsonArray((JsonNode?)JsonValue.Create("来源/源码一致的作者实际完成记录（full.head=2922ff526908bb9290371d33967e27c1849049b2，git diff 2922ff5 f9c5920 -- src tests 为空）；本发布未重跑 1885。"),
        (JsonNode?)JsonValue.Create("不能由本条替代 transverse/probes 红项，也不能被解读为性能测量。")),
    "tests/KernelTests/KernelTests.csproj",Exec(0));

// 3. intersections 116
AddCase("computation.intersections-116","computation / 独立求交合同 116（参数/类型/分支 + self-XT→real-PK wire 比较）","boolean","independent intersection contracts","pass",
    new JsonArray(
        Check("intersections-total","116 独立求交合同通过","pass",116,116,0,"contracts","原 intersections.log PASS 行","evidence/intersections.log"),
        Check("intersections-exit","原外部退出码","pass",0,0,0,"exit code","intersections.exit=0",null),
        Check("intersections-mask","116 runner 实际 mask：max_diffs=64、non_match_tests=false；数量/类型/sense/容差/missing/tol deviation 开，accuracy deviation 关；另 AssertWireGeometry","diagnostic",null,null,null,"","wire 合同证据，不等于 solid 同 mask 或任意曲线严格全等证明","evidence/intersections.log"),
        Check("intersections-corrupt-wire","native corrupt-wire 边界（tensor-rational-extreme-large-scale 首 wire 保留 PK_BODY_state_corrupt_c=13802）","diagnostic",null,null,null,"","原生限制单独保留，未作为兼容通过；三个 self-XT wire 全检 0 faults","evidence/intersections.log")),
    Ev(("原 intersections.log（脱敏副本）","evidence/intersections.log")),
    new JsonArray((JsonNode?)JsonValue.Create("采样比较不能宣称为任意曲线的完整等价证明；范围以合同参数/类型/分支为准。")),
    "scripts/BCurveAnalyticIntersectionOracle.cs",Exec(0));

// 4. standard lifecycle with same-instance display
var captureChecks=new JsonArray();
foreach(var check in capture["checks"]!.AsArray()){var copy=check!.DeepClone();copy["evidence"]="cases/lifecycle.standard-self/sample-0/capture.json";captureChecks.Add(copy);}
captureChecks.Add(Check("capture-exit","原采集进程退出码（board-f9c5920-standard）","pass",0,0,0,"exit code","含构建的墙钟见 execution.json；非性能测量","cases/lifecycle.standard-self/sample-0/execution.json"));
captureChecks.Add(Check("capture-review-standard","独立接受方同命令（review-f9c5920-standard）外部 0、PASS cases=1","pass",null,null,null,"","其 self Fix/反射副本/首切/再切 normalized15=3.965655017106088E-09/4.544547029326304E-08/2.3886265011558517E-08/2.2404188966449608E-08 属该次运行；本 case 数值来自本次同实例采集，不混签",null));
var captureSteps=capture["steps"]!.DeepClone().AsArray();
var captureCase=new JsonObject
{
    ["id"]="lifecycle.standard-self",["title"]="lifecycle / 标准 self 生命周期（Fix→copy 删源→反射/一致缩放→首切→可编辑重载→再切）",
    ["family"]="blend",["operation"]="fix blends + boolean cuts lifecycle",["status"]="pass",
    ["parameters"]=new JsonObject{{"candidate",Candidate},{"flags",new JsonArray("--event-arrangement","--geometry","--lifecycle","--proof-only-lifecycle","--material-ownership")},{"outputName","board-f9c5920-standard"},{"note","报告标准命令原样执行；未加 --require-native-fin-uvbox/--require-native-face-domains"}},
    ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="KernelRuntime.BodyFixBlends + PK_BODY_boolean (managed)",["parameters"]=new JsonObject{{"sourceTree","gm-board-capture-f9c5920 @ "+Candidate},{"collector","WebProgressCapture.cs @ "+Candidate}}},["pk"]=new JsonObject{["name"]="PK_EDGE_set_blend_constant/PK_BODY_fix_blends + PK_BODY_boolean (real PK)",["parameters"]=new JsonObject{{"oracle","real Parasolid 38.0.150, identical numeric parameters"}}}},
    ["steps"]=captureSteps,["checks"]=captureChecks,
    ["performance"]=Perf(),
    ["evidence"]=Ev(("同实例 capture（steps/checks/timings）","cases/lifecycle.standard-self/sample-0/capture.json"),("原采集日志（脱敏副本）","cases/lifecycle.standard-self/sample-0/oracle.log"),("执行来源/argv/UTC/exit","cases/lifecycle.standard-self/sample-0/execution.json"),("BODY_compare mask 与差异","cases/lifecycle.standard-self/sample-0/evidence/compare-0.json"),("BODY_compare mask 与差异","cases/lifecycle.standard-self/sample-0/evidence/compare-1.json"),("BODY_compare mask 与差异","cases/lifecycle.standard-self/sample-0/evidence/compare-2.json"),("BODY_compare mask 与差异","cases/lifecycle.standard-self/sample-0/evidence/compare-3.json"),("BODY_compare mask 与差异","cases/lifecycle.standard-self/sample-0/evidence/compare-4.json")),
    ["notes"]=new JsonArray((JsonNode?)JsonValue.Create("真实 GM/PK before/after/tool mesh 与 compare/mass/BODY_check 均来自同一次 board-f9c5920-standard 采集（5 步×双侧）；fresh XT receive + 完整 BODY_check + 有效 mask（max_diffs=1024、tol_dev_tests=true、acc_dev_tests=false、仅 n_cht_pts 诊断）+ 15 项物性门槛随 capture 记录。"),
        (JsonNode?)JsonValue.Create("imported-product 步骤（operation-2/3）的圆角由 PK 创建后导入 GM，后续切削由 GM 执行，不计作 GM 自建圆角；self-constructed-product（operation-4/5）为独立 GM Fix 后切削。"),
        (JsonNode?)JsonValue.Create("显示派生与同实例 receipt 绑定本采集；不从 PK 重建冒充 GM，也不与独立接受方运行混签。")),
    ["source"]=new JsonObject{["path"]="scripts/ShortEdgeBlendOracle.cs",["revision"]=Candidate},
    ["sessionPrecision"]=capture["sessionPrecision"]!.DeepClone(),
    ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=0,["exitCodes"]=new JsonArray(0),["timedOut"]=false}
};
cases.Add((JsonNode)captureCase);

// 5. same-byte two-tool Boolean consumer
AddCase("boolean.same-byte-two-tool-consumer","boolean / 同字节双刀 Boolean（target+tool 原字节直送两侧）","boolean","PK_BODY_boolean (same input bytes, two tools)","pass",
    new JsonArray(
        Check("consumer-inputs","输入字节绑定（target/tool-1/tool-2 sha256）","pass",null,null,null,"","target=37ac232e…b0ba tool-1=ad885c46…aec2 tool-2=8056a1bf…40792（全值见证据）","evidence/consumer-forward.log"),
        Check("consumer-gm","GM error=0 / result success / 单 body / 投影替换 reached","pass",0,0,0,"error","",null),
        Check("consumer-pk","PK 同字节 error=0 / result success / 单 body","pass",0,0,0,"error","",null),
        Check("consumer-receive","fresh receive + 完整 BODY_check 0 faults + 严格比较通过","pass",0,0,0,"faults","",null),
        Check("consumer-mass","mass normalized15=7.701450779942389E-11 < 1E-7","pass",7.701450779942389e-11,null,1e-7,"normalized15","accuracy=0.99999999；forward/reverse 两次一致","evidence/consumer-forward.log"),
        Check("consumer-exit","forward/reverse 原外部退出码","pass",0,0,0,"exit code","consumer-forward.exit=0, consumer-reverse.exit=0",null)),
    Ev(("原 consumer-forward.log（脱敏副本）","evidence/consumer-forward.log"),("原 consumer-reverse.log（脱敏副本）","evidence/consumer-reverse.log")),
    new JsonArray((JsonNode?)JsonValue.Create("同字节双刀 Boolean 与横切（transverse）输入身份不同（transverse 自独立构造 Fix 后不同目标继续），不能互签。")),
    "scripts/RollingBooleanConsumerOracle.cs",Exec(0,0));

// 6. injected failure rollback (diagnostic)
AddCase("boolean.inject-rollback","boolean / 注入发布分配失败回滚（预期失败试验）","boolean","PK_BODY_boolean with injected allocation failure","diagnostic",
    new JsonArray(
        Check("inject-gm","注入后 GM error=901 / result failed / 零 body，三输入原字节回滚","pass",901,null,901,"error","预期内部失败路径验证，不是建模验收","evidence/consumer-inject.log"),
        Check("inject-pk","PK 同字节 success / 单 body / full check 0","pass",0,0,0,"faults","",null),
        Check("inject-cert","生产压缩证书诊断：2/2/10 站点、4/5/8828 接受盒、最大空间 link 1.7188375197174616e-13","diagnostic",1.7188375197174616e-13,null,null,"distance","诊断确认证书实际成功","evidence/consumer-inject.log"),
        Check("inject-exit","原外部退出码","pass",0,0,0,"exit code","consumer-inject.exit=0",null)),
    Ev(("原 consumer-inject.log（脱敏副本）","evidence/consumer-inject.log")),
    new JsonArray((JsonNode?)JsonValue.Create("预期失败注入只证明该分配失败路径回滚；不推断其它失败路径，也不计作操作通过。")),
    "scripts/RollingBooleanConsumerOracle.cs",Exec(0));

// 7-13. per-operation successes
void OpCase(string id,string title,string family,string operation,string status,JsonArray checks,string raw,string? shipped,string[] summaryNames)
{
    var evidence=new JsonArray();
    if(shipped!=null)evidence.Add((JsonNode)new JsonObject{["label"]="原 log（脱敏副本）",["path"]="evidence/"+shipped});
    foreach(var name in summaryNames)evidence.Add((JsonNode)new JsonObject{["label"]="原始汇总行摘录（派生，原件见 evidence/evidence-manifest.json）",["path"]="evidence/operations-summaries.json"});
    evidence.Add((JsonNode)new JsonObject{["label"]="原件哈希/大小/路径",["path"]="evidence/evidence-manifest.json"});
    AddCase(id,title,family,operation,status,checks,evidence,new JsonArray((JsonNode?)JsonValue.Create("按实际执行范围展示；不扩大为全部操作目录。未内联原件以 SHA256 锚定在 evidence-manifest.json，原件字节保持在计算证据树。")),"scripts/ShortEdgeBlendOracle.cs",Exec(0));
}
OpCase("local.regular-bore","local / 修改支撑面保孔 + 自由曲面倒圆后切削","boolean","local blend on modified support face","pass",
    new JsonArray(
        Check("bore-sections","9 段截面：半径/双接触位置/切向连续 + 独立 native 几何","pass",9,9,0,"sections","regular-bore.log PASS 行","evidence/operations-summaries.json"),
        Check("bore-lifecycle","独立生命周期：self XT receive、copy、删源、mirror-scale、PK full check/strict compare/mass、cleanup","pass",null,null,null,"","",null),
        Check("bore-fillet","全部 1 个自由曲面倒圆及后续切削 case 通过","pass",1,1,0,"cases","",null),
        Check("bore-exit","原外部退出码","pass",0,0,0,"exit code","regular-bore.exit=0",null)),
    "regular-bore.log","regular-bore.log",new[]{"regular-bore.log"});
OpCase("blend.support-chain-equivalent","blend / 等价支持链（asymmetric43 base 与分列支持）","blend","support blend chain","pass",
    new JsonArray(
        Check("support-chain","asymmetric43-base 通过","pass",null,null,null,"","support-chain.log PASS 行","evidence/support-chain.log"),
        Check("support-chain-exit","原外部退出码","pass",0,0,0,"exit code","support-chain.exit=0",null)),
    "support-chain.log","support-chain.log",new[]{"support-chain.log"});
OpCase("boolean.separated-network","boolean / 非邻接带独立操作（删源、重载及后续 Boolean）","boolean","separated network boolean","pass",
    new JsonArray(
        Check("separated","非邻接组件：fresh strict PK、删源、重载及后续 Boolean 通过","pass",null,null,null,"","separated-network.log PASS 行","evidence/separated-network.log"),
        Check("separated-exit","原外部退出码","pass",0,0,0,"exit code","separated-network.exit=0",null)),
    "separated-network.log","separated-network.log",new[]{"separated-network.log"});
OpCase("local.input-rigid-reps","local / 输入刚体变换代表（同参数前置变换）","blend","input rigid transform representatives","pass",
    new JsonArray(
        Check("rigid","PASS cases=1 mode=independent-construction","pass",1,1,0,"cases","input-rigid.log PASS 行","evidence/operations-summaries.json"),
        Check("rigid-exit","原外部退出码","pass",0,0,0,"exit code","input-rigid.exit=0",null)),
    "input-rigid.log",null,new[]{"input-rigid.log"});
OpCase("local.input-mirror-reps","local / 输入镜像变换代表（同参数前置变换）","blend","input mirror transform representatives","pass",
    new JsonArray(
        Check("mirror","PASS cases=1 mode=independent-construction","pass",1,1,0,"cases","input-mirror.log PASS 行","evidence/operations-summaries.json"),
        Check("mirror-exit","原外部退出码","pass",0,0,0,"exit code","input-mirror.exit=0",null)),
    "input-mirror.log",null,new[]{"input-mirror.log"});
OpCase("local.corner-variants","local / 七角点与消退变体（球面接口 G1/有限带采样）","blend","corner and degenerate variants","pass",
    new JsonArray(
        Check("corner-arrangement","共同 arrangement provenance uses=42","pass",42,42,0,"uses","corner.log PASS 行","evidence/corner.log"),
        Check("corner-g1","半径/中心与 27 球面接口位置采样；27 live-band G1 采样","pass",27,27,0,"samples","",null),
        Check("corner-finite","独立有限带几何采样=108，最大距离 1.0861311850794508E-12；源接触 G1 采样=54","pass",1.0861311850794508e-12,null,null,"distance","阈值见原 log","evidence/corner.log"),
        Check("corner-exit","原外部退出码","pass",0,0,0,"exit code","corner.exit=0",null)),
    "corner.log","corner.log",new[]{"corner.log"});
OpCase("local.analytic-local-five","local / 五个选定轴向倒圆/倒角代表","blend","axial fillet/chamfer representatives","pass",
    new JsonArray(
        Check("analytic-local","cylinder-rim-fillet、cylinder-both-rim-fillet、cylinder-rim-chamfer、cylinder-both-rim-chamfer 等通过","pass",null,null,null,"","analytic-local.log PASS 行","evidence/analytic-local.log"),
        Check("analytic-local-exit","原外部退出码","pass",0,0,0,"exit code","analytic-local.exit=0",null)),
    "analytic-local.log","analytic-local.log",new[]{"analytic-local.log"});
OpCase("comparator.controls-6plus5","comparator / 6 正例 + 5 负例对照（尺寸/trim/内部/pole 错误被拒）","blend","comparator positive and negative controls","pass",
    new JsonArray(
        Check("comparator-positives","6 正例通过","pass",6,6,0,"controls","comparator-controls.log","evidence/comparator-controls.log"),
        Check("comparator-negatives","5 负例（错误高度块/平移 wire/错误 trim/错误内部/pole）均被拒绝","pass",5,5,0,"controls","",null),
        Check("comparator-exit","原外部退出码","pass",0,0,0,"exit code","comparator-controls.exit=0",null)),
    "comparator-controls.log","comparator-controls.log",new[]{"comparator-controls.log"});

// 14. transverse red
AddCase("short-edge.transverse-two-cuts","short-edge / 横切两次 self cut：原始 inertia.yz 点值门槛红","blend","transverse self cuts (proof-only lifecycle)","fail",
    new JsonArray(
        Check("transverse-cut1","首切（self-constructed-product-copy-reflected-cut）normalized15=1.2044611267910417E-07 > 1E-7","fail",1.2044611267910417e-7,null,1e-7,"normalized15","accuracy=0.99999999999，worst=inertia.yz；原门槛 1E-7 不变","evidence/transverse.log"),
        Check("transverse-cut2","再切（reloaded-second-cut）normalized15=1.126468644220207E-07 > 1E-7","fail",1.126468644220207e-7,null,1e-7,"normalized15","accuracy=0.99999999999，worst=inertia.yz","evidence/transverse.log"),
        Check("transverse-api","两次实际 API 已 Publish（error=0）","pass",0,0,0,"error","",null),
        Check("transverse-structure","strict/full/finite/native witness/material 均通过","pass",null,null,null,"","完整命令仍失败；原外部 exit 134","evidence/transverse.log"),
        Check("transverse-exit","原外部退出码","fail",134,134,0,"exit code","transverse.exit=134，不能由其他绿色运行代签",null)),
    Ev(("原 transverse.log（脱敏副本）","evidence/transverse.log"),("原件哈希/大小/路径","evidence/evidence-manifest.json")),
    new JsonArray((JsonNode?)JsonValue.Create("失败归属：两次横切 cut 的原始 inertia.yz 点值门槛（0.99999999999 精度下）；不反向撤销独立通过的未切割 Fix 或标准切割。"),
        (JsonNode?)JsonValue.Create("两次横切输入身份不同（self 反射目标 57eecc02…、独立 native 反射目标 3047cc3e…），不能互签。BOUNDS 估计/moduli 为诊断，不证明真值。")),
    "scripts/ShortEdgeBlendOracle.cs",Exec(134));

// 15-16. probes red
AddCase("probes-510.self-replay","probes / self 构造 510 固定分类探针回放：15 差异红","blend","fixed classification probe replay (self)","fail",
    new JsonArray(
        Check("probes-self-total","固定探针总数","pass",510,510,0,"probes","SAVED PROBES total=510","evidence/probes-self.log"),
        Check("probes-self-mismatches","分类差异 15（native_class_changed=0，零 API 错误）","fail",15,0,0,"mismatches","原门槛为零差异；保留红","evidence/probes-self.log"),
        Check("probes-self-exit","原外部退出码","fail",134,134,0,"exit code","probes-self.exit=134",null)),
    Ev(("原 probes-self.log（脱敏副本）","evidence/probes-self.log"),("原件哈希/大小/路径","evidence/evidence-manifest.json")),
    new JsonArray((JsonNode?)JsonValue.Create("保留红；不能沿用“原生基线也全改变”的解释；未证明这些差异是本次增量引入，也未证明可忽略。")),
    "scripts/ShortEdgeBlendOracle.cs",Exec(134));
AddCase("probes-510.import-replay","probes / 导入（native editable roundtrip）510 固定分类探针回放：16 差异红","blend","fixed classification probe replay (imported)","fail",
    new JsonArray(
        Check("probes-import-total","固定探针总数","pass",510,510,0,"probes","SAVED PROBES total=510","evidence/probes-import.log"),
        Check("probes-import-mismatches","分类差异 16（native_class_changed=0，零 API 错误）","fail",16,0,0,"mismatches","原门槛为零差异；保留红","evidence/probes-import.log"),
        Check("probes-import-exit","原外部退出码","fail",134,134,0,"exit code","probes-import.exit=134",null)),
    Ev(("原 probes-import.log（脱敏副本）","evidence/probes-import.log"),("原件哈希/大小/路径","evidence/evidence-manifest.json")),
    new JsonArray((JsonNode?)JsonValue.Create("保留红；与 self 探针分开登记，互不代签。")),
    "scripts/ShortEdgeBlendOracle.cs",Exec(134));

// 17. overall M0-M5
AddCase("m0-m5.overall-delivery","overall / M0–M5 总交付：未完成，整体验收仍红","boolean","M0-M5 overall delivery","fail",
    new JsonArray(
        Check("overall-complete","M0–M5 总交付完成断言","fail",null,null,null,"","接受报告 4c7646e：阻断“整体 M0–M5 已完成”；未完成，整体验收仍红",null),
        Check("overall-accepted-scope","已接受范围：报告限定的计算操作交付（见各 case）","pass",null,null,null,"","接受候选在已验证操作范围内的计算交付；不代表任意 BLEND/ICurve、多方事件或全部建模操作",null),
        Check("overall-red-transverse","两横切惯量原始门槛红","fail",null,null,null,"","见 short-edge.transverse-two-cuts",null),
        Check("overall-red-probes","self/import 510 探针 15/16 分类差异红","fail",null,null,null,"","见 probes-510.*",null),
        Check("overall-untested","未测范围（多方事件、任意 ICurve/BLEND、全部本地操作目录、M1–M3 部分合同等）","missing",null,null,null,"","未执行不推断；缺口按原计划保留",null),
        Check("overall-performance","正式性能验收","missing",null,null,null,"","Mac 同 host 性能线独立交付中；未完成前不签倍率/加速，旧 610 或 Linux 诊断时间不代签",null),
        Check("overall-display","同实例 DisplayBody 代表（本次补采 standard）","pass",null,null,null,"","仅代表显示覆盖，不是整体完成证据",null)),
    Ev(("运行/来源/容量回执","run-receipt.json"),("全部原件哈希与未内联清单","evidence/evidence-manifest.json"),("原证据索引 726 条（原字节副本）","evidence/evidence-final-index.log")),
    new JsonArray((JsonNode?)JsonValue.Create("M0 复现锁定证据/M1 首个真实共同链路/M2 去表示依赖/M3 滚动网络与相互作用事件/M4 短边闭环/M5 回归清理交付：总交付未完成；上游各独立通过项保留，不被整体红项抹去，也不把单项全绿当整包绿。"),
        (JsonNode?)JsonValue.Create("family 字段仅为看板既有粗分类（blend|boolean 二值）；准确操作类别以标题/operation/notes 为准。旧 main610 数据不是本轮成果，本 run 只声称自己的范围。")),
    "docs/reviews/advanced-modeling/pk-native-final-review.md",new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=null,["exitCodes"]=new JsonArray(),["timedOut"]=false});

// ---- derived summaries and manifest (only extracts without an inlined raw twin)
File.WriteAllText(Path.Combine(evidenceDir,"operations-summaries.json"),new JsonObject
{
    ["schemaVersion"]=1,["note"]="Extracted original result lines for reference-only logs (display derivatives); originals remain byte-identical in ../gm-pk-native-delivery/temp_docs/pk-native-delivery/ with hashes in evidence/evidence-manifest.json. Red/aggregate raw logs are inlined under evidence/ and need no duplicate extract.",
    ["standardLifecycle"]=new JsonArray(standardLines.Select(l=>(JsonNode?)JsonValue.Create(l)).ToArray()),
    ["perOperation"]=new JsonObject(opSummaries.Select(kv=>new KeyValuePair<string,JsonNode?>(kv.Key,kv.Value)))
}.ToJsonString());
File.WriteAllText(Path.Combine(evidenceDir,"matrix-shards-summary.json"),new JsonObject{["schemaVersion"]=1,["matrixCaseCount"]=matrixNames.Length,["shards"]=shardSummary}.ToJsonString());
File.WriteAllText(Path.Combine(evidenceDir,"evidence-manifest.json"),new JsonObject{["schemaVersion"]=1,["note"]="Original evidence identity; reference-only rows were not inlined to stay under the fixed 30MiB static package gate and are not deleted.",["files"]=manifestFiles}.ToJsonString());

var hostConfig=new JsonObject{["os"]="Linux x86_64 (qingyu2)",["authorSuitesHost"]="same machine",["sdk"]="10.0.302",["runtime"]=".NET 10.0.10"};
var manifest=new JsonObject
{
    ["schemaVersion"]=1,
    ["run"]=new JsonObject
    {
        ["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["pkVersion"]="38.0.150",
        ["host"]=hostConfig,["configuration"]=configuration.DeepClone(),
        ["scope"]=new JsonObject{["kind"]="computation-acceptance-snapshot",["candidate"]=Candidate,["acceptanceReport"]=Review,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="固定计算候选 f9c59201 + 接受报告 4c7646e 的完整验收快照；性能线未完成不并入。"},
        ["provenance"]=new JsonObject
        {
            ["modelingSource"]=new JsonObject{["tree"]="gm-pk-native-delivery",["commit"]=Candidate,["runnerHashes"]=new JsonObject{["scripts/ShortEdgeBlendOracle.cs"]="af77db29008f9751c2a159e69d26ddc08b7ca6350baffc53d6c5a9cf20e99828",["scripts/RollingBooleanConsumerOracle.cs"]="e65a838b1544fae9843316c9b8674b4db1e38804148842ffe91c1a798965a981"}},
            ["acceptanceReport"]=new JsonObject{["tree"]="gm-pk-native-final-review",["commit"]=Review,["path"]="docs/reviews/advanced-modeling/pk-native-final-review.md",["sha256"]=ReportSha,["scope"]="接受报告限定的计算操作范围；阻断整体 M0–M5 完成断言"},
            ["collectorSource"]=new JsonObject{["tree"]="gm-delivery-board",["commit"]=collectorSha,["script"]="scripts/AssembleComputationAcceptance.cs",["note"]="仅采集/装配/展示适配；建模结果来源以上两者，未改 Native 方程/阈值/用户调查"},
            ["capture"]=new JsonObject{["worktree"]="gm-board-capture-f9c5920 (detached clean @ "+Candidate+")",["command"]="ShortEdgeBlendOracle --output-name=board-f9c5920-standard --event-arrangement --geometry --lifecycle --proof-only-lifecycle --material-ownership",["note"]="报告标准命令原样执行；同实例 DisplayBody receipt"},
            ["evidenceRoot"]="../gm-pk-native-delivery/temp_docs/pk-native-delivery",["evidenceIndexSha256"]=EvidenceIndexSha,
            ["originalExits"]=new JsonObject(exits.OrderBy(kv=>kv.Key,StringComparer.Ordinal).ToDictionary(kv=>kv.Key,kv=>(JsonNode?)JsonValue.Create(kv.Value))),
            ["capacity"]=new JsonObject{["packageGate"]="30MiB static bundle gate in fixed publish tool",["inlinedRawLogs"]="evidence-manifest.json kind=sanitized-display-derivative",["referenceOnlyRawLogs"]=new JsonArray(referenceOnly.Select(n=>(JsonNode?)JsonValue.Create(n+" + 8 matrix shard logs")).ToArray()),["note"]="未内联原件未删除：SHA256/大小/原路径全量记录，原件保持在计算证据树"},
            ["familyTaxonomyNote"]="family 为看板既有粗分类（blend|boolean）；准确操作类别以标题/operation/notes 为准。",
            ["performanceScope"]="Linux 诊断耗时非正式性能；本 run 无合格倍率（全部 null）；Mac 同 host 性能线未完成不并入。",
            ["caseStatusDefinitions"]=new JsonObject{["pass"]="该 case 现行必需检查通过，仅限所列参数/范围。",["fail"]="存在必需检查失败；原 exit 另行独立记录。",["missing"]="未执行/未达/无当前产物；不推断。",["diagnostic"]="诊断范围；不是合格验收通过。",["timeout"]="超预算。",["unsupported"]="适配未实现；不推断内核能力。"}
        }
    },
    ["cases"]=cases
};
var runJson=manifest.ToJsonString();
File.WriteAllText(Path.Combine(run,"manifest.json"),runJson);
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="计算交付验收快照：f9c59201 + review 4c7646e（M0–M5 未完成，含红项）",["manifest"]=$"runs/{runId}/manifest.json"})};
File.WriteAllText(Path.Combine(output,"index.json"),index.ToJsonString());
// ---- run receipt with capacity accounting (before validation so the record closes)
var runBytes=Directory.EnumerateFiles(run,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
var referenceBytes=referenceOnly.Sum(n=>new FileInfo(Path.Combine(evidenceRoot,n)).Length)+Enumerable.Range(0,8).Sum(i=>new FileInfo(Path.Combine(evidenceRoot,$"matrix-shard-{i}.log")).Length);
File.WriteAllText(Path.Combine(run,"run-receipt.json"),new JsonObject
{
    ["schemaVersion"]=1,["runId"]=runId,["generatedAt"]=now.ToString("O"),["collectorCommit"]=collectorSha,
    ["modelingCommit"]=Candidate,["acceptanceReport"]=Review,
    ["captureExit"]=0,["captureStartedUtc"]=File.ReadAllText(Path.Combine(captureRoot,"start.utc")).Trim(),["captureFinishedUtc"]=File.ReadAllText(Path.Combine(captureRoot,"end.utc")).Trim(),
    ["runBytesBeforeReceiptAndValidation"]=runBytes,["referenceOnlyBytes"]=referenceBytes,
    ["referenceOnlyLogs"]=new JsonArray(referenceOnly.Concat(Enumerable.Range(0,8).Select(i=>$"matrix-shard-{i}.log")).Select(n=>(JsonNode?)JsonValue.Create(n)).ToArray()),
    ["referenceOnlyNote"]="未内联原件字节合计 "+referenceBytes+"；值/exit/摘录/SHA256 全在记录内，原件未删除。若要求全部内联，固定 30MiB 静态包需额外容量或另定交付范围。",
    ["validation"]="see validation.json"
}.ToJsonString());
var validation=WebProgressDataValidation.Validate(output);
File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());
Console.WriteLine("RUN "+runId+" cases="+cases.Count+" runBytes="+(runBytes+new FileInfo(Path.Combine(run,"run-receipt.json")).Length+new FileInfo(Path.Combine(run,"validation.json")).Length)+" validationErrors="+validation["errors"]!.AsArray().Count);
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
