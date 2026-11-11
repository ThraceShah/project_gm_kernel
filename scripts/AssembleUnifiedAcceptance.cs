#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed unified-kernel acceptance snapshot (4b2405e + runner 84c558c
// + review 583d917/B17) from the immutable handoff-current-v2 package. All 56
// meshes ship with their capture steps; quality check statuses stay verbatim.
// No modeling runs here; originals are never modified.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Candidate="4b2405e3945929c5bb93677a68b0ee79e430b2f3";
const string WholeTree="dcac18749b996dcd9a59b3ae0fb66149aee3f9e2";
const string NativeTree="133f56acb0cf485366cd4f13f457dc3b161655eb";
const string SrcTree="0c5d23f6f4e3e7aabe5034888f92812c3ecb07ad";
const string Runner="84c558c10135c117bf8be6629a157e6d230f1b2d";
const string DocCommit="8c2d6da82f4c83c4803fab408e424f4b6ca5f295";
const string Review="583d917890a9f654dbd4a1f4e96e5e5f261f68eb";
const string ReviewB1="b17f1aa4645c334cbbbe2c4674649a13e1470e2e";
const string MergeCommit="b9db9bb";
const string FunctionalParent="97174392b97e8c5976e56e2890c58314c74c73b5";
const string PerformanceParent="f9ffc0985eb1e5ce53f649af66751de7c6c77353";
const string ZipSha="5197d647fbc47a15f3338e83f1c4063227fddcd4dfff92186c0274aa510495bf";
const string TrxSha="90472933bdcb7245b8da6015cd0b6585ca81fcbf70234690c76fd49547d111ca";
var pkg=Path.GetFullPath(Option("--package","../temp_docs/incoming/unified-delivery/extracted"),scripts);
var reviewTree=Path.GetFullPath(Option("--review","../../gm-native-unified-review"),scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-unified/data"),scripts);
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-unified4b");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains('\\'))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Command("git",["-C",reviewTree,"rev-parse","HEAD"])==ReviewB1,"review tree HEAD (B1 closure)");
string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
// verify every package member against files.sha256
foreach(var line in File.ReadAllLines(Path.Combine(pkg,"files.sha256")))
{
    var parts=line.Split("  ",2);if(parts.Length!=2)continue;
    var p=Path.Combine(pkg,parts[1].Trim());
    if(!File.Exists(p))throw new InvalidDataException("package member missing "+parts[1]);
    Require(Hash(File.ReadAllBytes(p))==parts[0].Trim(),"package byte identity "+parts[1]);
}
var manifest=JsonNode.Parse(File.ReadAllText(Path.Combine(pkg,"manifest.json")))!.AsObject();
Require(manifest["calculationCommit"]!.GetValue<string>()==Candidate&&manifest["nativeTree"]!.GetValue<string>()==NativeTree,"manifest identity");
Require(manifest["fullSuite"]!["rawExit"]!.GetValue<int>()==0&&manifest["fullSuite"]!["total"]!.GetValue<int>()==1974&&manifest["fullSuite"]!["passed"]!.GetValue<int>()==1974&&manifest["fullSuite"]!["trxSha256"]!.GetValue<string>()==TrxSha,"full suite facts");
var runnerReport=JsonNode.Parse(File.ReadAllText(Path.Combine(pkg,"runner-report.json")))!.AsObject();
Require(runnerReport["performanceQualified"]!.GetValue<bool>()==false,"no inherited performance qualification");

var run=Path.Combine(output,"runs",runId);
if(Directory.Exists(run))throw new IOException("Run already exists: immutable "+runId);
var evidenceDir=Path.Combine(run,"evidence");Directory.CreateDirectory(evidenceDir);
var evidenceManifest=new JsonArray();
void Ship(string relSource,string shipped,bool verbatim,string kind)
{
    var bytes=File.ReadAllBytes(Path.Combine(pkg,relSource));
    var outBytes=verbatim?bytes:Encoding.UTF8.GetBytes(Sanitize(Encoding.UTF8.GetString(bytes)));
    File.WriteAllBytes(Path.Combine(evidenceDir,shipped),outBytes);
    evidenceManifest.Add((JsonNode)new JsonObject{["original"]="handoff-current-v2.zip:"+relSource,["shipped"]="evidence/"+shipped,["originalBytes"]=bytes.Length,["originalSha256"]=Hash(bytes),["shippedSha256"]=Hash(outBytes),["kind"]=kind});
}
string Sanitize(string text)=>text.Replace(pkg,".").Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"~");
Ship("manifest.json","unified-manifest.json",true,"verbatim-original");
Ship("runner-report.json","unified-runner-report.json",true,"verbatim-original");
Ship("files.sha256","files-sha256-index.log",true,"verbatim-original");
Ship("source.sha256","source-sha256-index.log",true,"verbatim-original");

// ---- group import: meshes + steps + verbatim capture/quality/identity + checks verbatim with run-prefixed evidence
var displayBinding=new JsonArray();
string CaseIdOf(string g)=>g switch{"oblique"=>"unified.oblique-chain","mirror8"=>"unified.mirror8","shortedge-lifecycle"=>"unified.shortedge-lifecycle","controls"=>"unified.controls","runner-curved"=>"unified.runner-curved","runner-nurbs"=>"unified.runner-nurbs","runner-shortedge"=>"unified.runner-shortedge",_=>throw new ArgumentException(g)};
var groupSteps=new Dictionary<string,JsonArray>(StringComparer.Ordinal);
var groupChecks=new Dictionary<string,JsonArray>(StringComparer.Ordinal);
foreach(var g in new[]{"oblique","mirror8","shortedge-lifecycle","controls","runner-curved","runner-nurbs","runner-shortedge"})
{
    var caseId=CaseIdOf(g);var prefix=$"cases/{caseId}/sample-0/{g}";
    var src=Path.Combine(pkg,"cases",g);
    foreach(var file in Directory.EnumerateFiles(src,"*",SearchOption.AllDirectories))
    {
        var rel=Path.GetRelativePath(src,file);var name=Path.GetFileName(file);
        var ext=Path.GetExtension(file);
        if(ext is not (".json" or ".log" or ".exit" or ".sha256"))continue;
        var shippedName=rel.Replace('\\','/');
        if(shippedName=="execution.exit")shippedName="execution-exit.log";
        if(ext==".sha256")shippedName=shippedName.Replace(".sha256","-sha256.log");
        var target=Path.Combine(run,prefix,shippedName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
    }
    var capture=JsonNode.Parse(File.ReadAllText(Path.Combine(src,"capture.json")))!.AsObject();
    Require(capture["captureErrors"]!.AsArray().Count==0,"capture errors empty: "+g);
    var steps=capture["steps"]!.DeepClone().AsArray();
    foreach(var step in steps)foreach(var side in new[]{"gm","pk"})
    {
        foreach(var key in new[]{"before","after"})if(step![side]![key]!=null)step[side]![key]=prefix+"/meshes/"+Path.GetFileName(step[side]![key]!.GetValue<string>());
        foreach(var key in new[]{"tools","afterBodies"})if(step![side]![key] is JsonArray paths)for(var p=0;p<paths.Count;p++)paths[p]=prefix+"/meshes/"+Path.GetFileName(paths[p]!.GetValue<string>());
    }
    groupSteps[g]=steps;
    var checks=new JsonArray();
    foreach(var row in capture["checks"]!.AsArray())
    {
        var copy=row!.DeepClone()!;
        if(copy["evidence"] is JsonValue ev&&ev.TryGetValue<string>(out var ep)&&ep.StartsWith("evidence/",StringComparison.Ordinal))copy["evidence"]=prefix+"/"+ep;
        checks.Add(copy);
    }
    groupChecks[g]=checks;
    var meshRows=new JsonArray();
    foreach(var file in Directory.Exists(Path.Combine(src,"meshes"))?Directory.EnumerateFiles(Path.Combine(src,"meshes")):Array.Empty<string>())
    {
        var bytes=File.ReadAllBytes(file);
        meshRows.Add((JsonNode)new JsonObject{["file"]=prefix+"/meshes/"+Path.GetFileName(file),["sha256"]=Hash(bytes),["bytes"]=bytes.Length});
    }
    var exitCode=int.Parse(File.ReadAllText(Path.Combine(src,"execution.exit")).Trim());
    var loaded=JsonNode.Parse(File.ReadAllText(Path.Combine(src,"identity","loaded.json")))!.AsObject();
    displayBinding.Add((JsonNode)new JsonObject{["group"]=g,["case"]=caseId,["steps"]=steps.Count,["meshes"]=meshRows,["rawExit"]=exitCode,["captureSha256"]=manifest["cases"]!.AsArray().First(c=>c!["id"]!.GetValue<string>()==g)!["captureSha256"]?.DeepClone(),["loaded"]=loaded.DeepClone(),["command"]=JsonNode.Parse(File.ReadAllText(File.Exists(Path.Combine(src,"execution.command.json"))?Path.Combine(src,"execution.command.json"):Path.Combine(src,"command.json")))!.DeepClone(),["note"]="同实例真实 DisplayBody（GM live/PK snapshot）；mesh 为原字节；缺 roles/tracking 如实（checks 原样）；XT/DLL/PDB 正文不公开。"});
}
File.WriteAllText(Path.Combine(evidenceDir,"display-binding.json"),new JsonObject{["schemaVersion"]=1,["note"]="Unified-kernel same-instance display binding; 56 real meshes across 7 capture groups; check statuses verbatim from the current contract captures.",["groups"]=displayBinding}.ToJsonString());

// ---- builders
var host=new JsonObject{["os"]="Linux x64 (qingyu2)",["sdk"]="10.0.302",["runtime"]=".NET 10.0.10",["pk"]="Parasolid 38.0.150",["pkToy"]="d820719b6d8510d623972ab9d8efc65edbc312f9"};
JsonNode Perf()=>new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]="Linux 不是正式性能；倍率全 null；时间若见为开发原观察。绝不把 217fc 旧 340x/7.7x 绑定到统一内核。",["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=new JsonObject{["build"]="Release",["nativeAot"]=false},["note"]="runner performanceQualified=false；GConic/6616 历史 run 独立保留不混。",["measurementKind"]="not-run",["stableBenchmark"]=false,["qualifiedScope"]="Unified-kernel functional/quality snapshot only."};
JsonObject Check(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,string? evidence=null)
    =>new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=gm,["pk"]=pk,["absDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value)):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=evidence,["note"]=note};
var cases=new JsonArray();
void AddCase(string id,string title,string status,JsonArray steps,JsonArray checks,JsonArray evidence,JsonArray notes)
{
    cases.Add((JsonNode)new JsonObject
    {
        ["id"]=id,["title"]=title,["family"]="boolean",["operation"]="unified kernel (4b2405e + runner 84c558c)",["status"]=status,
        ["parameters"]=new JsonObject{{"calculation",Candidate},{"runner",Runner},{"acceptanceReview","../gm-native-unified-review/docs/reviews/advanced-modeling/pk-native-unified-4b2405e-independent-review.md @ "+Review+" / B1 "+ReviewB1}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="unified kernel ops per capture",["parameters"]=new JsonObject{{"nativeTree",NativeTree},{"srcTree",SrcTree},{"wholeTree",WholeTree}}},["pk"]=new JsonObject{["name"]="real PK 38.0.150",["parameters"]=new JsonObject{{"oracle","same bytes where declared same-byte-fork"}}}},
        ["steps"]=steps,["checks"]=checks,["performance"]=Perf(),["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]="scripts/NativeModelingWorkComparison.cs / capture groups",["revision"]=Candidate},
        ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=null,["exitCodes"]=new JsonArray(),["timedOut"]=false}
    });
}
var evCore=new JsonArray((JsonNode)new JsonObject{["label"]="217fc… 不适用：本 run 原件为 handoff-current-v2（manifest/runner-report/files/source 清单，原字节）",["path"]="evidence/unified-manifest.json"},(JsonNode)new JsonObject{["label"]="逐组显示绑定（56 mesh 哈希/loaded identity/command）",["path"]="evidence/display-binding.json"},(JsonNode)new JsonObject{["label"]="来源/收据回执",["path"]="evidence/run-receipt.json"});
JsonNode NoModelNote()=>JsonValue.Create("报表/诊断类：无实际模型显示，不造模型。");
JsonNode GroupsNote()=>JsonValue.Create("上游 independent-construction 与下游 same-byte-fork 分列互不代签；shortedge 来源=PK seed→GM receive→GM Fix（不称 GM 独立构造）；curved roles/tracking missing 如实；回滚 901/failed 零产品=负控成功，不写 GM Boolean 成功；mass 只按 Verified/Different/Unresolved，missing 不绿。");
JsonNode LimitsNote()=>JsonValue.Create("限定保留：510 self15/import16/native7、五产品 BodyContains5000、FaceAttach 未测、curved roles 缺失、各质量 Unresolved；1974 全过不证明这些解决。整体 M0–M5 与 PK 目标仍未完成。");
JsonNode IdentityNote()=>JsonValue.Create("四层身份分列：计算 4b2405e/整树 dcac1874/src 0c5d23f6/Native 133f56ac；runner 质量门 84c558c（仅脚本，Native 不变）；文档 8c2d6da；独审 583d917+B1 续审 b17f1aa。原始模型/Capture 仍 4b 运行身份，不改标 84c、不以同 Native 树当同 DLL。");
foreach(var (g,title,status,extra) in new[]{
    ("oblique","unified / oblique 负 surface 倒角→生命周期→同字节 Boolean（2 步/10 mesh/123 全过 raw0）","pass",(JsonNode?)null),
    ("mirror8","unified / mirror8 实质修复：GM157/fault16059 已修（GM/PK 0、strict/full/finite 过；2 mass 未决 raw2）","diagnostic",JsonValue.Create("本 run 明示该实质修复：与历史输入 hash 完全相同的输入下，原 GM157/fault16059/Clip 已不再出现；修复为事件登记区间投影（bda3748），未改容差/预算/输入。mass 2 项 Unresolved 不转绿、原 raw2 保留。")),
    ("shortedge-lifecycle","unified / shortedge 双来源 lifecycle（5 步/28 mesh/133 过 77 未决 raw2）","diagnostic",(JsonNode?)null),
    ("controls","unified / 反例控制（27 过 9 失败 27 未决 raw0=成功负控，不冒正例）","diagnostic",JsonValue.Create("9 fail 行是被正确拒绝的反例（错误半径/trim Rejected、材料 Different、回滚 901/failed 零产品）；27 missing 照实。controls raw0=负控执行成功，不是正例全过。")),
    ("runner-curved","unified / curved 单操作（6 mesh/14 过 13 未决 raw2）","diagnostic",(JsonNode?)null),
    ("runner-nurbs","unified / NURBS 独立 fillet 单操作（4 mesh/16 过 13 未决 raw2）","diagnostic",(JsonNode?)null),
    ("runner-shortedge","unified / shortedge 单操作（4 mesh/35 过 17 未决 raw2）","diagnostic",(JsonNode?)null)})
{
    var notes=new JsonArray(GroupsNote(),LimitsNote(),IdentityNote());
    if(extra!=null)notes.Add(extra);
    var checks=(JsonArray)groupChecks[g].DeepClone();
    var steps=(JsonArray)groupSteps[g].DeepClone();
    checks.Add(Check("group-exit-"+g,"原外部 exit "+(g=="oblique"||g=="controls"?"0（执行成功）":"2（质量 Unresolved 编码，照实）"),"diagnostic",null,null,null,"exit code","原 exit 不被 checker 0 代签","evidence/display-binding.json"));
    AddCase(CaseIdOf(g),title,status,steps,checks,(JsonArray)evCore.DeepClone(),notes);
}
// 63-matrix report case
AddCase("unified.boolean-63-matrix","unified / 63 最终 Boolean 场景与整体操作清单（报告/独审引用，无模型）","diagnostic",new JsonArray(),
    new JsonArray(
        Check("matrix-63","regular45-final 45 场景 + mixed18-final 18 场景：几何/拓扑全过；mass 未决 40+30；各原 exit 2","diagnostic",63,63,0,"scenarios","不总加成混合通过率；按报告与独审引用","evidence/unified-manifest.json"),
        Check("matrix-lifecycle","shortedge-lifecycle-final（8 mass 未决/exit2）、mirror8-final（2 未决/exit2）、nurbs-lifecycle-final（未决/exit2）、rigid-small-final（过/exit0）分列","diagnostic",null,null,null,"","各独立操作结算",null),
        Check("matrix-negative","rollback-final（901 负控成功）、face-query-final（四 sense 控制过）、direction-controls-final（10 正/10 负过）、controls-off/on（CSV 60 行逐字同）各原 0","pass",0,0,0,"exit code","","evidence/unified-manifest.json"),
        Check("matrix-il","normal-il-final：托管 IL 3003 方法 CLEAN（无 AOT）","pass",3003,3003,0,"methods","",null)),
    (JsonArray)evCore.DeepClone(),new JsonArray(NoModelNote(),LimitsNote(),IdentityNote()));
AddCase("unified.full-suite-1974","unified / 完整 Native 套件 1974/1974（历史 2 项本次 Passed）","pass",new JsonArray(),
    new JsonArray(
        Check("suite-1974","完整 Native suite 1974/1974、0 failed、0 skipped，原 exit 0（8m44s；TRX "+TrxSha[..12]+"…）","pass",1974,1974,0,"tests","test Native 与普通构建制品身份分列","evidence/unified-manifest.json"),
        Check("suite-historical2","历史已知 2 项（BlendIntersectionTests.NativeCurveTube…、NestedBlendCacheEmptyReadSetTests.SupportingBlend…）本 TRX 实际 Passed；未改测试或历史记录","pass",2,2,0,"tests","历史失败记录保留；不推断其它能力解决",null),
        Check("suite-sources","333 tracked Native + 161 tests 源校验一致（817/2415 清单核 0）；生成文档分列","pass",333,333,0,"files","","evidence/source-sha256-index.log")),
    (JsonArray)evCore.DeepClone(),new JsonArray(NoModelNote(),LimitsNote()));
AddCase("unified.overall-status","unified / 总体：统一内核候选已接受，M0–M5/PK 目标仍未完成","fail",new JsonArray(),
    new JsonArray(
        Check("overall-accepted","接受范围=统一 merge 候选 4b2405e（功能 9717439+累计性能 f9ffc09 真合并）+ runner 84c558c 质量门修复 + 独审 583d917/B17","pass",null,null,null,"","B1 已解除当前单操作 runner 质量门；原 4b 假阳性作为历史问题保留，不显示为当前 blocked","evidence/unified-manifest.json"),
        Check("overall-mass","各组质量 Unresolved（oblique 0、mirror8 2、lifecycle 77、curved 13、nurbs 13、shortedge 17 项 missing 如实）","missing",null,null,null,"","missing 不绿",null),
        Check("overall-limits","510 self15/import16/native7、五产品 BodyContains5000、FaceAttach 未测、curved roles 缺失继续限定","missing",null,null,null,"","1974 全过不证明这些解决",null),
        Check("overall-target","M0–M5 与 PK 性能目标未完成；Linux 无正式性能、倍率全 null、217fc 旧倍率不绑定","fail",null,null,null,"","GConic/6616 历史 run 独立保留",null)),
    (JsonArray)evCore.DeepClone(),new JsonArray(NoModelNote(),LimitsNote(),IdentityNote()));

// ---- manifest + receipt
File.WriteAllText(Path.Combine(run,"manifest.json"),new JsonObject
{
    ["schemaVersion"]=1,
    ["run"]=new JsonObject
    {
        ["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["pkVersion"]="38.0.150",
        ["host"]=host.DeepClone(),["configuration"]=new JsonObject{["build"]="Release",["nativeAot"]=false,["source"]="handoff-current-v2 package; no new capture}"},
        ["scope"]=new JsonObject{["kind"]="unified-kernel-acceptance-snapshot",["candidate"]=Candidate,["acceptanceReview"]=Review,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="统一内核 4b2405e 快照：7 组 56 真实 mesh + 1974 套件 + 63 场景清单；质量/红项照实；无性能。"},
        ["provenance"]=new JsonObject
        {
            ["unifiedSource"]=new JsonObject{{"calculation",Candidate},{"wholeTree",WholeTree},{"srcTree",SrcTree},{"nativeTree",NativeTree},{"merge",MergeCommit},{"parents",new JsonObject{{"functional",FunctionalParent},{"performance",PerformanceParent}}},{"note","真 merge 功能 9717439+累计性能 f9ffc09；未含未审 Bezier 实验"}},
            ["runnerGate"]=new JsonObject{{"commit",Runner},{"note","仅脚本质量门修复（84c）；Native 不变；原始模型/Capture 仍 4b 运行身份，不改标"}},
            ["acceptanceReview"]=new JsonObject{{"tree","gm-native-unified-review"},{"branch","review/pk-native-unified-4b2405e"},{"commit",Review},{"b1Closure",ReviewB1},{"path","docs/reviews/advanced-modeling/pk-native-unified-4b2405e-independent-review.md"}},
            ["collectorSource"]=new JsonObject{{"tree","gm-delivery-board"},{"commit",collectorSha},{"script","scripts/AssembleUnifiedAcceptance.cs"},{"note","仅装配/展示；无新建模/积分/fullsuite/ABBA/采集"}},
            ["package"]=new JsonObject{{"zip","handoff-current-v2.zip"},{"sha256",ZipSha},{"members",156},{"meshFiles",56},{"note","XT/DLL/PDB/vendor 不公开；files.sha256 逐项核 0"}},
            ["mirror8Fix"]="bda3748 事件登记区间投影修复 GM157/fault16059（GM/PK 0、strict/full/finite 过，mass 2 未决 raw2）；输入 hash 与历史完全相同；未改容差/预算/输入。",
            ["fullSuite"]=new JsonObject{{"total",1974},{"passed",1974},{"rawExit",0},{"trxSha256",TrxSha},{"historicalTwoNowPassed",true}},
            ["performanceScope"]="Linux 非正式性能；倍率全 null；217fc/6616 旧倍率不绑定统一内核；GConic 历史 run 独立保留。",
            ["limitsNote"]="510/self15-import16/native7、BodyContains5000×5、FaceAttach 未测、curved roles 缺失、mass Unresolved、M0–M5/PK 目标未完成。",
            ["familyTaxonomyNote"]="family 为看板既有粗分类。",
            ["caseStatusDefinitions"]=new JsonObject{{"pass","该 case 现行必需检查通过，仅限所列范围。"},{"fail","存在必需检查失败/未完成目标。"},{"missing","缺失/未决；不推断、不转绿。"},{"diagnostic","诊断/控制/含未决项；不是无条件验收通过。"}}
        }
    },
    ["cases"]=cases
}.ToJsonString());
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="统一内核验收快照：4b2405e+84c558c（56 模型/1974 套件/mirror8 已修；M0–M5 未完成）",["manifest"]=$"runs/{runId}/manifest.json"})};
File.WriteAllText(Path.Combine(output,"index.json"),index.ToJsonString());
var runBytes=Directory.EnumerateFiles(run,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
File.WriteAllText(Path.Combine(evidenceDir,"run-receipt.json"),new JsonObject
{
    ["schemaVersion"]=1,["runId"]=runId,["generatedAt"]=now.ToString("O"),["collectorCommit"]=collectorSha,
    ["layers"]=new JsonObject{{"calculation",Candidate},{"runner",Runner},{"review",Review+"/"+ReviewB1},{"package",ZipSha},{"collector",collectorSha}},
    ["originalExits"]=new JsonObject{{"oblique",0},{"mirror8",2},{"shortedge-lifecycle",2},{"controls",0},{"runner-curved",2},{"runner-nurbs",2},{"runner-shortedge",2},{"full-suite",0}},
    ["runBytesBeforeReceiptAndValidation"]=runBytes,["evidenceFiles"]=evidenceManifest,
    ["validation"]="see validation.json"
}.ToJsonString());
var validation=WebProgressDataValidation.Validate(output);
File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());
Console.WriteLine("RUN "+runId+" cases="+cases.Count+" runBytes="+(runBytes+new FileInfo(Path.Combine(evidenceDir,"run-receipt.json")).Length+new FileInfo(Path.Combine(run,"validation.json")).Length)+" validationErrors="+validation["errors"]!.AsArray().Count);
return validation["errors"]!.AsArray().Count==0?0:2;
static string Command(string command,string[] arguments)
{
    var info=new ProcessStartInfo(command){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
    foreach(var arg in arguments)info.ArgumentList.Add(arg);
    using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();
    if(p.ExitCode!=0)throw new IOException(command+" failed: "+p.StandardError.ReadToEnd());
    return output.Trim();
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
static string ScriptPath([CallerFilePath]string path="")=>path;
