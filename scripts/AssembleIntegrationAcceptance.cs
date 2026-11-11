#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed functional-integration snapshot (1887e30d current Capture
// + accepted parent scopes) from the immutable integration originals,
// reusing the complete same-instance capture-oblique-final-v1 and current
// products. No new capture runs here; XT bytes stay private (hashes only).
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Candidate="1887e30d9050480daf38546f8cdd2df6592bd094";
const string DocCommit="bddcbf77a412c00c1ca4790e12a94094d4543d05";
const string MergeCommit="dfc5defe9483009669288f3359a2a97ba9b3bf99";
const string BooleanParent="aca329607b376032a31283cc0e659fbd54916d97";
const string BlendParent="60be7216d646a296d7fd2d0b30b5f336a46a6a66";
const string BooleanSource="4794e60111e1382eafe69880077544fcc135521e";
const string BooleanReview="75eddad101f0adaaa4550180c73eb1d12fa64fe7";
const string BlendReview="54f1b309d830ac78358ebcba78e1deec8e3c3b8f";
const string SourceManifestSha="45af81fd2cc6572d9a25784a868580c85cd821e0c53c0ef4fbbc6a21f639054a";
const string ArtifactManifestSha="edb5c71f66a657b3fabcd995d6af92493928383705696f62f5b4f99a1624d9df";
const string TargetSha="0c3da45bd617f1222c1e8a809bb0a6bec8058fa901c5b99a82e6bf2431400ce3";
const string ToolSha="2bafc64cf83ebda0d48aef75ad7b08b24c28d151d7f0cc61c1b2e32d8c2ff89a";
const string GmProductSha="1d73665e25f3bfccccbe6c87b16da0232baa59a37fe81a580fbb8303e97eba53";
const string PkProductSha="ff6d96d89d1ab68582317c9c4786aec588346d164dcd70584e8fe3c10c14debc";
var originals=Path.GetFullPath(Option("--originals","../../gm-pk-native-delivery/temp_docs/pk-native-functional-integration"),scripts);
var productsDir=Path.GetFullPath(Option("--products","../../gm-pk-native-delivery/temp_docs/local-operation-oracle/integration-captured-final-v1"),scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-integration/data"),scripts);
var captureDir=Path.Combine(originals,"capture-oblique-final-v1");
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-integrated1887");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains('\\'))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Hash(File.ReadAllBytes(Path.Combine(originals,"final-source.sha256")))==SourceManifestSha,"537-source manifest hash");
Require(Hash(File.ReadAllBytes(Path.Combine(originals,"final-artifacts.sha256")))==ArtifactManifestSha,"68-artifact manifest hash");
var run=Path.Combine(output,"runs",runId);
if(Directory.Exists(run))throw new IOException("Run already exists: immutable "+runId);
var evidenceDir=Path.Combine(run,"evidence");Directory.CreateDirectory(evidenceDir);
var evidenceManifest=new JsonArray();
string Sanitize(string text)=>text.Replace(originals,".").Replace(productsDir,".").Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"~");
void Ship(string file,string source,string shipped,bool verbatim)
{
    var bytes=File.ReadAllBytes(file);
    var outBytes=verbatim?bytes:Encoding.UTF8.GetBytes(Sanitize(Encoding.UTF8.GetString(bytes)));
    File.WriteAllBytes(Path.Combine(evidenceDir,shipped),outBytes);
    evidenceManifest.Add((JsonNode)new JsonObject{["original"]=source,["shipped"]="evidence/"+shipped,["originalBytes"]=bytes.Length,["originalSha256"]=Hash(bytes),["shippedSha256"]=Hash(outBytes),["kind"]=verbatim?"verbatim-original":"sanitized-display-derivative"});
}
Ship(Path.Combine(originals,"final-source.sha256"),"pk-native-functional-integration/final-source.sha256","final-source.sha256.log",true);
Ship(Path.Combine(originals,"final-artifacts.sha256"),"pk-native-functional-integration/final-artifacts.sha256","final-artifacts.sha256.log",true);
Ship(Path.Combine(originals,"capture-declared-revision-resolved.txt"),"pk-native-functional-integration/capture-declared-revision-resolved.txt","capture-revision-resolution.log",true);
Ship(Path.Combine(originals,"captured-oblique-final-v1.log"),"pk-native-functional-integration/captured-oblique-final-v1.log","captured-oblique-final-v1.log",false);
Ship(Path.Combine(originals,"chamfer-rollback-v2.log"),"pk-native-functional-integration/chamfer-rollback-v2.log","chamfer-rollback-v2.log",false);
Ship(Path.Combine(originals,"controls-off-v1.log"),"pk-native-functional-integration/controls-off-v1.log","controls-off-v1.log",false);
Ship(Path.Combine(originals,"controls-on-v1.log"),"pk-native-functional-integration/controls-on-v1.log","controls-on-v1.log",false);
Ship(Path.Combine(originals,"merged-boundary-tests.log"),"pk-native-functional-integration/merged-boundary-tests.log","merged-boundary-tests.log",false);
Ship(Path.Combine(originals,"check-positive-v1.log"),"pk-native-functional-integration/check-positive-v1.log","check-positive-v1.log",false);
Ship(Path.Combine(originals,"check-controls-v1.log"),"pk-native-functional-integration/check-controls-v1.log","check-controls-v1.log",false);

// ---- same-instance capture import (capture.json + quality evidence + meshes; XT bytes stay out)
var prefix="cases/integrated/oblique-chain/sample-0";
foreach(var file in Directory.EnumerateFiles(Path.Combine(captureDir,"evidence"),"*",SearchOption.AllDirectories))
{
    if(Path.GetExtension(file) is not ".json")continue;
    var name=Path.GetFileName(file);var target=Path.Combine(run,prefix,"evidence",name);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
    evidenceManifest.Add((JsonNode)new JsonObject{["original"]="pk-native-functional-integration/capture-oblique-final-v1/evidence/"+name,["shipped"]=prefix+"/evidence/"+name,["originalSha256"]=Hash(File.ReadAllBytes(file)),["kind"]="verbatim-original-quality-evidence"});
}
foreach(var file in Directory.EnumerateFiles(Path.Combine(captureDir,"meshes")))
{
    var name=Path.GetFileName(file);var target=Path.Combine(run,prefix,"meshes",name);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
}
var capture=JsonNode.Parse(File.ReadAllText(Path.Combine(captureDir,"capture.json")))!.AsObject();
Require(capture["captureErrors"]!.AsArray().Count==0,"capture has no errors");
var stepJson=capture["steps"]!.DeepClone().AsArray();
var upstreamStep=stepJson[0]!;var booleanStep=stepJson[1]!;
Require(upstreamStep["sourceBinding"]?["kind"]?.GetValue<string>()=="independent-construction","step0 sourceBinding kind");
Require(booleanStep["sourceBinding"]?["kind"]?.GetValue<string>()=="same-byte-fork","step1 sourceBinding kind");
Require(booleanStep["sourceBinding"]!["targetXtSha256"]!.GetValue<string>()==TargetSha&&booleanStep["sourceBinding"]!["toolXtSha256"]!.GetValue<string>()==ToolSha,"same-byte hashes");
foreach(var step in stepJson)foreach(var side in new[]{"gm","pk"})
{
    foreach(var key in new[]{"before","after"})if(step![side]![key]!=null)step[side]![key]=prefix+"/meshes/"+Path.GetFileName(step[side]![key]!.GetValue<string>());
    foreach(var key in new[]{"tools","afterBodies"})if(step![side]![key] is JsonArray paths)for(var p=0;p<paths.Count;p++)paths[p]=prefix+"/meshes/"+Path.GetFileName(paths[p]!.GetValue<string>());
}
upstreamStep["title"]="oblique 不等距倒角自构造 + 独立生命周期（independent-construction；live GM 产品验证）";
booleanStep["title"]="同字节后续 subtract（same-byte-fork：target "+TargetSha[..12]+"… / tool "+ToolSha[..12]+"…；GM 产品 "+GmProductSha[..12]+"… / PK "+PkProductSha[..12]+"…）";
var allChecks=capture["checks"]!.DeepClone().AsArray();
var boundary=allChecks.ToList().FindIndex(c=>c!["id"]!.GetValue<string>()=="gm-1-api");
Require(boundary>0,"gm-1-api boundary present");
var upstreamChecks=new JsonArray();var booleanChecks=new JsonArray();
for(var i=0;i<allChecks.Count;i++)
{
    var row=allChecks[i]!.DeepClone()!;
    if(row["evidence"] is JsonValue ev&&ev.TryGetValue<string>(out var evPath)&&evPath.StartsWith("evidence/",StringComparison.Ordinal))row["evidence"]=prefix+"/"+evPath;
    (i<boundary?upstreamChecks:booleanChecks).Add(row);
}
upstreamChecks.Add((JsonNode)new JsonObject{["id"]="upstream-summary",["label"]="上游独立构造结论（不与下游 Boolean 互签）",["status"]="pass",["gm"]=null,["pk"]=null,["absDelta"]=null,["normalizedDelta"]=null,["threshold"]=null,["unit"]="",["evidence"]=prefix+"/evidence/quality-compare-11.json",["note"]="负 surface FaceAsk face=18542/surface=18541 实际 false==PK false；8 次条件有限方向证明；物性 Verified（agreementStatus 见 quality-mass 原件）"});
booleanChecks.Add((JsonNode)new JsonObject{["id"]="boolean-summary",["label"]="下游同字节 Boolean 结论（独立登记，不代签上游）",["status"]="pass",["gm"]=0,["pk"]=0,["absDelta"]=null,["normalizedDelta"]=null,["threshold"]=0,["unit"]="error",["evidence"]=prefix+"/evidence/quality-compare-44.json",["note"]="GM/PK 各 error=0/result=21650/count=1；fresh/full/finite 方向/FaceAsk/材料/物性 Verified + cleanup 原 0；target/tool 同字节哈希绑定"});

// ---- builders
var host=new JsonObject{["os"]="Linux x64 (qingyu2)",["sdk"]="10.0.302",["runtime"]=".NET 10.0.10",["pk"]="Parasolid 38.0.150",["pkToy"]="d820719b6d8510d623972ab9d8efc65edbc312f9"};
JsonNode Perf()=>new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]="无正式性能测量；不签性能资格。",["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=new JsonObject{["build"]="Release",["nativeAot"]=false},["note"]="不推导倍率。",["measurementKind"]="not-run",["stableBenchmark"]=false,["qualifiedScope"]="Functional integration only."};
JsonObject Check(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,string? evidence=null)
    =>new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=gm,["pk"]=pk,["absDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value)):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=evidence,["note"]=note};
var cases=new JsonArray();
void AddCase(string id,string title,string family,string status,JsonArray steps,JsonArray checks,JsonArray evidence,JsonArray notes)
{
    cases.Add((JsonNode)new JsonObject
    {
        ["id"]=id,["title"]=title,["family"]=family,["operation"]="functional integration (1887 current Capture)",["status"]=status,
        ["parameters"]=new JsonObject{{"candidate",Candidate},{"merge",MergeCommit},{"acceptanceParents",new JsonObject{{"boolean",BooleanSource},{"booleanReview",BooleanReview},{"blend",BlendParent},{"blendReview",BlendReview}}}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="LocalOperationOracle integration chain",["parameters"]=new JsonObject{{"source","gm-pk-native-delivery @ "+Candidate}}},["pk"]=new JsonObject{["name"]="real PK 38.0.150",["parameters"]=new JsonObject{{"oracle","same bytes where declared same-byte-fork"}}}},
        ["steps"]=steps,["checks"]=checks,["performance"]=Perf(),["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]="scripts/LocalOperationOracle.cs",["revision"]=Candidate},
        ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=0,["exitCodes"]=new JsonArray(0),["timedOut"]=false}
    });
}
var evCore=new JsonArray((JsonNode)new JsonObject{["label"]="当前 Capture capture.json（steps/checks）",["path"]=prefix+"/capture.json"},(JsonNode)new JsonObject{["label"]="537 source / 68 artifact 原清单（原字节）",["path"]="evidence/final-source.sha256.log"},(JsonNode)new JsonObject{["label"]="短 revision 外部解析收据（不改原件）",["path"]="evidence/capture-revision-resolution.log"},(JsonNode)new JsonObject{["label"]="来源/绑定/收据回执",["path"]="evidence/run-receipt.json"});
// copy capture.json into run
File.Copy(Path.Combine(captureDir,"capture.json"),Path.Combine(run,prefix,"capture.json"));
JsonNode Note1()=>JsonValue.Create("步骤来源已标注：step0=independent-construction（自构造+独立生命周期），step1=same-byte-fork（target/tool 同字节哈希绑定）；每张 mesh 绑定 live body/display snapshot/内容寻址 XT 哈希/loaded DLL（见 mesh source 与 quality-* 的 sourceBinding/captureIdentity）。归档中间产品标 archived，native-control 不冒 GM 产物。");
JsonNode Note2()=>JsonValue.Create("当前 Capture 只订阅同次共享 AssertEquivalent/MassProperties 结果；质量证据为 quality-compare-*/quality-mass-*（15 分量 estimate/modulus/normalized 上下界、agreementStatus=Verified/Different/Unresolved 原样保留），不查旧 compare-{ordinal}、无旧点值合同；缺 modulus/来源不能变绿。");
JsonNode NonClaim()=>JsonValue.Create("不签：全部 M0–M5/1909 全绿、未执行性能资格、GM Boolean 回滚成功；旧 63/17/123/290/96 按父接受范围复用展示，不说 1887 新跑。");
AddCase("integrated.oblique-upstream","integrated / oblique 不等距倒角自构造+独立生命周期（independent-construction）","blend","pass",
    new JsonArray(upstreamStep.DeepClone()),upstreamChecks,(JsonArray)evCore.DeepClone(),new JsonArray(Note1(),Note2(),NonClaim()));
AddCase("integrated.same-byte-boolean","integrated / 同字节后续 subtract（same-byte-fork，GM/PK 各 21650/1body）","boolean","pass",
    new JsonArray(booleanStep.DeepClone()),booleanChecks,(JsonArray)evCore.DeepClone(),new JsonArray(Note1(),Note2(),NonClaim(),JsonValue.Create("下游 Boolean 是上游 live 成品的同字节 fork，不是独立重建上游；两结论分列互不代签。")));
AddCase("integrated.rollback-injection","integrated / 后续 Boolean 分配失败注入回滚（GM 901/21653/0body）","boolean","diagnostic",
    new JsonArray(),
    new JsonArray(
        Check("rollback-upstream","上游 Blend 仍独立通过","pass",null,null,null,"","前序产品/生命周期不因下游注入失败而撤销","evidence/chamfer-rollback-v2.log"),
        Check("rollback-gm","GM 后续注入失败：error=901/21653/零产品，target/tool 原字节与池计数回滚","diagnostic",901,null,901,"error","预期内部失败路径；控制 raw 0 不签 GM Boolean 成功","evidence/chamfer-rollback-v2.log"),
        Check("rollback-native","同输入 native（PK）成功","diagnostic",null,null,null,"","与 GM 注入失败分开登记",null),
        Check("rollback-exit","控制原 exit 0","pass",0,0,0,"exit code","",null)),
    (JsonArray)evCore.DeepClone(),new JsonArray(Note1(),NonClaim()));
AddCase("integrated.contract-controls","integrated / 当前合同反例控制（off/on 各一，CSV 计数一致）","boolean","diagnostic",
    new JsonArray(),
    new JsonArray(
        Check("ctrl-wrongradius","真实错误半径：Rejected（保留）","pass",null,null,null,"","真实 native API 错误/Rejected 不被 exit 0 消除","evidence/controls-on-v1.log"),
        Check("ctrl-trim","有限 wire trim 错误：Rejected（保留）","pass",null,null,null,"","","evidence/controls-on-v1.log"),
        Check("ctrl-material","真实材料分类翻转：Different（保留）","pass",null,null,null,"","","evidence/controls-on-v1.log"),
        Check("ctrl-unresolved","30 项 Unresolved 分量未变绿（off/on 各 3 未决，60 行 CSV 完全相同）","missing",30,30,0,"components","agreementStatus 原样保留，不转绿","evidence/controls-off-v1.log"),
        Check("ctrl-exit","off/on 两次原 exit 各 0（控制本身通过）","pass",0,0,0,"exit code","",null)),
    (JsonArray)evCore.DeepClone(),new JsonArray(Note2(),NonClaim()));
AddCase("integrated.boundary-tests","integrated / 合并边界测试 13/13","boolean","pass",
    new JsonArray(),
    new JsonArray(
        Check("boundary-13","FaceAsk 四符号组合、sheet/oblique 拥有关系、native 非线性有限根正反例、圆柱接缝 reload：13/13","pass",13,13,0,"tests","原 exit 0","evidence/merged-boundary-tests.log")),
    (JsonArray)evCore.DeepClone(),new JsonArray(NonClaim()));
AddCase("integrated.build-receipts","integrated / 构建与收据核对（IL CLEAN、清单核对 0）","boolean","pass",
    new JsonArray(),
    new JsonArray(
        Check("il-guard","当前正常构建分配 IL guard：2925 方法 CLEAN（无 AOT 发布）","pass",2925,2925,0,"methods","","evidence/final-source.sha256.log"),
        Check("builds","8 个受影响 oracle/收据脚本构建成功","pass",8,8,0,"scripts","",null),
        Check("receipts","收据核对：positive 3 次物性/45 行/6 比较；controls 4 次/60 行/2 比较（只核对收据，不增加建模资格）","pass",null,null,null,"","","evidence/check-positive-v1.log"),
        Check("source-manifests","537 source / 68 artifact 清单哈希固定（45af81fd…/edb5c71f…）","pass",537,68,0,"entries","清单核对均 0","evidence/final-artifacts.sha256.log")),
    (JsonArray)evCore.DeepClone(),new JsonArray(NonClaim()));
AddCase("integrated.parent-scopes","integrated / 父接受范围复用与保留红项（非 1887 新跑）","blend","missing",
    new JsonArray(),
    new JsonArray(
        Check("parent-reuse","旧 63/17/123/290/96 固定证据按 Boolean 4794/Blend 60be 父接受范围复用展示","diagnostic",null,null,null,"","未在 1887 重跑；不扩大父独审范围",null),
        Check("parent-red-510","510 self/import 15/16 差异、当前 imported 未决 16、native 自差 7 保留","missing",null,null,null,"","独立事实，不转绿",null),
        Check("parent-red-query","Blend 五产品直接 BodyContains 5000 与不确定物性保留","missing",5,0,0,"products","原 5000 红不消失",null),
        Check("parent-suite","原完整单测 1907/2（原 exit 1）保留，不写 1909 全绿","missing",1907,1909,0,"tests","",null)),
    (JsonArray)evCore.DeepClone(),new JsonArray(NonClaim()));
AddCase("integrated.overall-status","integrated / 总体：有限真实交界+当前 Capture 源已接受，非整体全绿","boolean","fail",
    new JsonArray(),
    new JsonArray(
        Check("overall-accepted","接受范围=1887e30d 作为两已独审功能的有限真实交界+当前 Capture 源（537/68 清单重核匹配）","pass",null,null,null,"","协调已集中核收",null),
        Check("overall-rollback","回滚控制不签 GM Boolean 成功","fail",901,null,901,"error","见 integrated.rollback-injection",null),
        Check("overall-reds","保留红项/未决（510、BodyContains 5000、1907/2、30 Unresolved）","missing",null,null,null,"","见 integrated.parent-scopes/contract-controls",null),
        Check("overall-nonclaim","不签全部 M0–M5/1909 全绿、未执行性能、main 集成或上线声明","fail",null,null,null,"","排除范围照实",null)),
    (JsonArray)evCore.DeepClone(),new JsonArray(NonClaim()));

// ---- manifest + receipt
File.WriteAllText(Path.Combine(run,"manifest.json"),new JsonObject
{
    ["schemaVersion"]=1,
    ["run"]=new JsonObject
    {
        ["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["pkVersion"]="38.0.150",
        ["host"]=host.DeepClone(),["configuration"]=new JsonObject{["build"]="Release",["nativeAot"]=false,["capture"]="capture-oblique-final-v1 reused (same-instance originals; no new capture)"},
        ["scope"]=new JsonObject{["kind"]="functional-integration-acceptance-snapshot",["candidate"]=Candidate,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="1887e30d 有限真实交界 + 当前 Capture 源；上游/下游/回滚/控制分列；父红项保留。"},
        ["provenance"]=new JsonObject
        {
            ["integrationSource"]=new JsonObject{["commit"]=Candidate,["docCommit"]=DocCommit,["branch"]="feat/pk-native-functional-integration",["merge"]=MergeCommit,["parents"]=new JsonObject{{"booleanDoc",BooleanParent},{"blend",BlendParent}},["diff"]="11 scripts (397+/52-), no Native core/C ABI/model change"},
            ["parentAcceptance"]=new JsonObject{{"boolean",new JsonObject{{"source",BooleanSource},{"review",BooleanReview}}},{"blend",new JsonObject{{"source",BlendParent},{"review",BlendReview}}}},
            ["collectorSource"]=new JsonObject{["tree"]="gm-delivery-board",["commit"]=collectorSha,["script"]="scripts/AssembleIntegrationAcceptance.cs",["note"]="仅装配/展示；无新 Capture/建模"},
            ["capture"]=new JsonObject{["dir"]="capture-oblique-final-v1 (reused originals)",["declaredRevision"]="1887e30 (short; resolved externally by capture-declared-revision-resolved.txt; originals unchanged)",["fullReplayDeclaration"]=Candidate,["manifests"]=new JsonObject{{"source537",SourceManifestSha},{"artifacts68",ArtifactManifestSha}}},
            ["productHashes"]=new JsonObject{{"target",TargetSha},{"tool",ToolSha},{"gmProduct",GmProductSha},{"pkProduct",PkProductSha},{"loadedGmDll","fff3758e998c65bbfeaa78c5bdd84cc94c7379f330c90034355857c25aaa6d17"},{"loadedOracleDll","390d1e9a6a2149e8a4a8fcf39a6bc1ca2d808c36e6f9a062bd59d010a0198af8"}},
            ["sourceBindings"]="step0 independent-construction；step1 same-byte-fork；mesh 绑定 live body/display snapshot/内容寻址 XT 哈希/loaded DLL；XT 正文不公开（source-*.x_t 仅哈希）。",
            ["qualityFormat"]="当前 Capture 同次共享 AssertEquivalent/MassProperties：quality-compare-*/quality-mass-*（15 分量 estimate/modulus/normalized 上下界、agreementStatus 原样）；无旧 compare/点值合同。",
            ["xtScope"]="XT 正文私有；仅 SHA256 公开。",
            ["familyTaxonomyNote"]="family 为看板既有粗分类。",
            ["caseStatusDefinitions"]=new JsonObject{["pass"]="该 case 现行必需检查通过，仅限所列范围。",["fail"]="存在必需检查失败/非签项。",["missing"]="缺失/未决；不推断。",["diagnostic"]="诊断/控制类；不是验收通过。"}
        }
    },
    ["cases"]=cases
}.ToJsonString());
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="功能集成验收快照：1887e30d 当前 Capture（上游/下游同字节分列，父红项保留）",["manifest"]=$"runs/{runId}/manifest.json"})};
File.WriteAllText(Path.Combine(output,"index.json"),index.ToJsonString());
var runBytes=Directory.EnumerateFiles(run,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
File.WriteAllText(Path.Combine(evidenceDir,"run-receipt.json"),new JsonObject
{
    ["schemaVersion"]=1,["runId"]=runId,["generatedAt"]=now.ToString("O"),["collectorCommit"]=collectorSha,
    ["candidate"]=Candidate,["docCommit"]=DocCommit,
    ["originalExits"]=new JsonObject{["captured-oblique-final-v1"]=0,["chamfer-rollback-v2"]=0,["controls-off-v1"]=0,["controls-on-v1"]=0,["merged-boundary-tests"]=0,["check-positive-v1"]=0,["check-controls-v1"]=0,["il-guard"]=0},
    ["xtBytesNotShipped"]="source-*.x_t 保持私有（哈希在 mesh/quality 绑定）",
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
