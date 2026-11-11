#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Assemble the fixed Blend functional acceptance snapshot run (candidate
// 60be7216 + accepted review 54f1b309) from the author's original evidence and
// same-instance display captures produced in the isolated exact-candidate
// worktree. Original author/review evidence bytes stay untouched; logs shipped
// here are sanitized display derivatives anchored by SHA256. No modeling runs
// here.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
const string Candidate="60be7216d646a296d7fd2d0b30b5f336a46a6a66";
const string DevBase="089939392bf2d756e22bcdaa97910e12023584c5";
const string Review="54f1b309d830ac78358ebcba78e1deec8e3c3b8f";
const string ReportSha="fc974d24ef8668eb15420386877aff052725062acfac4116213a239e85d0f1f7";
const string EvidenceDigest="9c89bde1cffcc7b40015ea9c562ac1d6ef9122e7ad559cb94e857173957ad25b";
const string ShortedgeProductSha="37ac232e62340c29f4f6ba94807775b7d39ba3a64d445c75791499c1a811b0ba";
const string ShortedgeTransformedSha="57eecc0294734c805d3ded1ab034fc4c78dec6da6d2300a6ce85a0e553981ad2";
var authorTree=Path.GetFullPath(Option("--author","../../gm-blend-functional-delivery"),scripts);
var reviewTree=Path.GetFullPath(Option("--review","../../gm-blend-60be-review"),scripts);
var captureRoot=Path.GetFullPath(Option("--capture","../../gm-board-blend-60be/temp_docs/board-60be-capture"),scripts);
var captureTree=Path.GetFullPath("../../gm-board-blend-60be",scripts);
var output=Path.GetFullPath(Option("--output","../temp_docs/web-progress-blend/data"),scripts);
var now=DateTimeOffset.UtcNow;
var runId=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-blend60be");
if(runId.IndexOfAny(Path.GetInvalidFileNameChars())>=0||runId.Contains('/')||runId.Contains('\\'))throw new ArgumentException("run-id must be one safe path component");
var collectorSha=Command("git",["rev-parse","HEAD"]);
void Require(bool condition,string message){if(!condition)throw new InvalidDataException("IDENTITY "+message);}
Require(Command("git",["-C",authorTree,"rev-parse","HEAD"])==Candidate,"author tree HEAD");
Require(Command("git",["-C",reviewTree,"rev-parse","HEAD"])==Review,"review tree HEAD");
Require(Command("git",["-C",captureTree,"rev-parse","HEAD"])==Candidate,"capture worktree HEAD");
Require(Hash(File.ReadAllBytes(Path.Combine(reviewTree,"docs/reviews/advanced-modeling/blend-60be-independent-review.md")))==ReportSha,"review report hash");
Require(Hash(File.ReadAllBytes(Path.Combine(authorTree,"docs/reviews/advanced-modeling/blend-functional-evidence.sha256")))==EvidenceDigest,"33-entry evidence index digest");
var exits=new Dictionary<string,int>(StringComparer.Ordinal);
foreach(var name in new[]{"prism-fillet","prism-chamfer","oblique-chamfer","axial-query","regular","chain","shortedge"})
    exits[name]=int.Parse(File.ReadAllText(Path.Combine(captureRoot,name+".exit")).Trim());
Require(exits["prism-fillet"]==0&&exits["prism-chamfer"]==0&&exits["oblique-chamfer"]==0&&exits["axial-query"]==1&&exits["regular"]==2&&exits["chain"]==2&&exits["shortedge"]==2,"display capture exit facts");
Require(Hash(File.ReadAllBytes(Path.Combine(captureTree,"temp_docs/short-edge-blend-oracle/board-60be-shortedge/a1-r0.5-t1E-07-reverseFalse-default-self.x_t")))==ShortedgeProductSha,"shortedge product equals the accepted author product");
foreach(var (file,sha) in new[]{
    (Path.Combine(authorTree,"temp_docs/blend-functional/final-integrated/complete-catalog.log"),"2b2d07c4167f96b48f1a7cac4fd25b8cdea95b6b29a3e9aef2c3618f45be8b45"),
    (Path.Combine(authorTree,"temp_docs/blend-functional/final-integrated/axial-material-query.log"),"898878a22c3812a66b331fdc68e8d153ceb7dbde6e1ab8c02da1008ef9ce321c"),
    (Path.Combine(reviewTree,"temp_docs/blend-60be-review/semantic-v3.log"),"23df946b7682aba5b68a2387fe5792ab05fd5ea9f9f50cb16b3bedc2828170f8"),
    (Path.Combine(reviewTree,"temp_docs/blend-60be-review/prism-chamfer.log"),"22a000e02d57a79c269ab4ce762142bf4f392ff0457dec8e94959ef10104bdfa")})
    Require(Hash(File.ReadAllBytes(file))==sha,Path.GetFileName(file)+" hash");

// ---- run directory and shipped evidence (sanitized display derivatives)
var run=Path.Combine(output,"runs",runId);
if(Directory.Exists(run))throw new IOException("Run already exists: immutable "+runId);
var evidenceDir=Path.Combine(run,"evidence");Directory.CreateDirectory(evidenceDir);
string Sanitize(string text)=>text.Replace(authorTree,".").Replace(reviewTree,".").Replace(captureTree,".").Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"~");
var evidenceManifest=new JsonArray();
void Ship(string original,string sourceTreeName,string shipped)
{
    var bytes=File.ReadAllBytes(original);var derivative=Encoding.UTF8.GetBytes(Sanitize(Encoding.UTF8.GetString(bytes)));
    File.WriteAllBytes(Path.Combine(evidenceDir,shipped),derivative);
    evidenceManifest.Add((JsonNode)new JsonObject{["original"]=sourceTreeName,["shipped"]="evidence/"+shipped,["originalBytes"]=bytes.Length,["originalSha256"]=Hash(bytes),["shippedSha256"]=Hash(derivative),["kind"]="sanitized-display-derivative"});
}
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/final-integrated/complete-catalog.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/complete-catalog.log","complete-catalog.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/final-integrated/axial-material-query.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log","axial-material-query.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/regular-contract-v1.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/regular-contract-v1.log","regular-contract-v1.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/chain-contract-v1.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/chain-contract-v1.log","chain-contract-v1.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/shortedge-single-v1.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/shortedge-single-v1.log","shortedge-single-v1.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/direction-controls-v5.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/direction-controls-v5.log","direction-controls-v5.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/final-integrated/face-sign-controls.log"),"../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/face-sign-controls.log","face-sign-controls.log");
Ship(Path.Combine(reviewTree,"temp_docs/blend-60be-review/semantic-v3.log"),"../gm-blend-60be-review/temp_docs/blend-60be-review/semantic-v3.log","semantic-v3.log");
Ship(Path.Combine(reviewTree,"temp_docs/blend-60be-review/prism-chamfer.log"),"../gm-blend-60be-review/temp_docs/blend-60be-review/prism-chamfer.log","prism-chamfer.log");
Ship(Path.Combine(authorTree,"docs/reviews/advanced-modeling/blend-functional-evidence.sha256"),"../gm-blend-functional-delivery/docs/reviews/advanced-modeling/blend-functional-evidence.sha256","evidence-index-33.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/final-api-evidence.sha256"),"../gm-blend-functional-delivery/temp_docs/blend-functional/final-api-evidence.sha256","evidence-index-107.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/extra-query-evidence.sha256"),"../gm-blend-functional-delivery/temp_docs/blend-functional/extra-query-evidence.sha256","evidence-index-78.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/final-integrated-evidence.sha256"),"../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated-evidence.sha256","evidence-index-24.log");
Ship(Path.Combine(authorTree,"temp_docs/blend-functional/shortedge-products.sha256"),"../gm-blend-functional-delivery/temp_docs/blend-functional/shortedge-products.sha256","evidence-index-14.log");

// ---- same-instance display capture import
var displayBinding=new JsonArray();
var captureLogHashes=new JsonObject();
foreach(var name in new[]{"prism-fillet","prism-chamfer","oblique-chamfer","axial-query","regular","chain","shortedge"})
    captureLogHashes[name]=Hash(File.ReadAllBytes(Path.Combine(captureRoot,name+".log")));
JsonArray ImportCapture(string capture,string caseId,string[]? stepNames=null)
{
    var prefix=$"cases/{caseId}/sample-0/{capture}";
    var src=Path.Combine(captureRoot,capture);
    foreach(var file in Directory.EnumerateFiles(src,"*",SearchOption.AllDirectories))
    {
        var relative=Path.GetRelativePath(src,file);var target=Path.Combine(run,prefix,relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
    }
    var captureJson=JsonNode.Parse(File.ReadAllText(Path.Combine(src,"capture.json")))!.AsObject();
    foreach(var step in captureJson["steps"]!.AsArray())foreach(var side in new[]{"gm","pk"})
    {
        foreach(var key in new[]{"before","after"})if(step![side]![key]!=null)step[side]![key]=prefix+"/"+step[side]![key]!.GetValue<string>();
        foreach(var key in new[]{"tools","afterBodies"})if(step![side]![key] is JsonArray paths)for(var p=0;p<paths.Count;p++)paths[p]=prefix+"/"+paths[p]!.GetValue<string>();
    }
    var steps=captureJson["steps"]!.DeepClone().AsArray();
    if(stepNames!=null)for(var i=0;i<steps.Count&&i<stepNames.Length;i++)steps[i]!["title"]=stepNames[i]+" 显示样本（60be 同实例补采 "+capture+"）：真实 DisplayBody，显示样本≠全类别证据范围";
    var shipped=new JsonArray();
    foreach(var file in Directory.EnumerateFiles(Path.Combine(run,prefix,"meshes"),"*",SearchOption.AllDirectories))
    {
        var bytes=File.ReadAllBytes(file);var mesh=JsonNode.Parse(bytes)!.AsObject();
        shipped.Add((JsonNode)new JsonObject{["file"]=prefix+"/meshes/"+Path.GetFileName(file),["sha256"]=Hash(bytes),["kernel"]=mesh["source"]!["kernel"]!.GetValue<string>(),["faces"]=mesh["faces"]!.AsArray().Count,["edges"]=mesh["edges"]!.AsArray().Count,["bodyId"]=mesh["bodyId"]!.GetValue<string>()});
    }
    displayBinding.Add((JsonNode)new JsonObject{["case"]=caseId,["capture"]=capture,["source"]="gm-board-blend-60be @ "+Candidate,["argvExit"]=exits[capture],["logSha256"]=captureLogHashes[capture]?.DeepClone(),["captureSha256"]=Hash(File.ReadAllBytes(Path.Combine(src,"capture.json"))),["meshes"]=shipped,["note"]="同实例 DisplayBody 补采（展示产物生成，非重跑 96/fullsuite）；GM mesh 来自 GM DisplayBody、PK mesh 来自 PK snapshot，未重建、未以 PK 代 GM。"});
    return steps;
}

// ---- builders
var host=new JsonObject{["os"]="Linux x64 (qingyu2)",["sdk"]="10.0.302",["runtime"]=".NET 10.0.10",["pk"]="Parasolid 38.0.150, precision 1e-8",["pkToy"]="d820719b6d8510d623972ab9d8efc65edbc312f9"};
var configuration=new JsonObject{["build"]="Release",["nativeAot"]=false,["timing"]="no performance measurement this round",["note"]="本轮不发布开发倍率（性能 code6616 已独审未正式测量）"};
JsonNode NullPerf()=>new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]="本轮无正式性能测量；倍率保持 null（不发布开发倍率）。",["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="Linux 诊断耗时非性能证据；Mac 性能线另行。",["measurementKind"]="not-run",["stableBenchmark"]=false,["qualifiedScope"]="Functional acceptance only; no performance certification."};
JsonObject Check(string id,string label,string status,double? gm,double? pk,double? threshold,string unit,string note,string? evidence=null)
    =>new JsonObject{["id"]=id,["label"]=label,["status"]=status,["gm"]=gm,["pk"]=pk,["absDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value):null,["normalizedDelta"]=gm.HasValue&&pk.HasValue?Math.Abs(gm.Value-pk.Value)/(1+Math.Abs(pk.Value)):null,["threshold"]=threshold,["unit"]=unit,["evidence"]=evidence,["note"]=note};
var cases=new JsonArray();
void AddCase(string id,string title,string status,JsonArray steps,JsonArray checks,JsonArray evidence,JsonArray notes,string operation)
{
    cases.Add((JsonNode)new JsonObject
    {
        ["id"]=id,["title"]=title,["family"]="blend",["operation"]=operation,["status"]=status,
        ["parameters"]=new JsonObject{{"candidate",Candidate},{"devBase",DevBase},{"acceptanceReview","../gm-blend-60be-review/docs/reviews/advanced-modeling/blend-60be-independent-review.md @ "+Review}},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]=operation,["parameters"]=new JsonObject{{"sourceTree","gm-board-blend-60be @ "+Candidate}}},["pk"]=new JsonObject{["name"]=operation,["parameters"]=new JsonObject{{"oracle","real Parasolid 38.0.150, identical numeric parameters"}}}},
        ["steps"]=steps,["checks"]=checks,["performance"]=NullPerf(),["evidence"]=evidence,["notes"]=notes,
        ["source"]=new JsonObject{["path"]="scripts/LocalOperationOracle.cs",["revision"]=Candidate},
        ["execution"]=new JsonObject{["selected"]=true,["attempted"]=true,["exitCode"]=null,["exitCodes"]=new JsonArray(),["timedOut"]=false}
    });
}
JsonNode DisplayNote()=>JsonValue.Create("显示样本为隔离 60be 树同实例补采（真实 DisplayBody，绑定见 evidence/display-binding.json）；显示样本≠全类别证据范围，全类别证据=作者 complete-catalog 17/17 与原索引。");
JsonNode NoModelNote()=>JsonValue.Create("报表/诊断类：无实际模型显示，不造模型。");
JsonNode NonClaim()=>JsonValue.Create("不签 Boolean、Mac 性能、任意 freeform、变半径、高 valence、整体 M0–M5 或总目标全完成；不以数量证明替功能。");
var evBasic=new JsonArray((JsonNode)new JsonObject{["label"]="原件索引 33 条（原字节）",["path"]="evidence/evidence-index-33.log"},(JsonNode)new JsonObject{["label"]="display 绑定（mesh 哈希/capture/log 哈希）",["path"]="evidence/display-binding.json"},(JsonNode)new JsonObject{["label"]="来源/收据回执",["path"]="evidence/run-receipt.json"});

// 1-4 local blend catalog rows + query gap
var filletSteps=ImportCapture("prism-fillet","blend.planar-fillet",new[]{"prism-all-fillet"});
AddCase("blend.planar-fillet","blend / 有限凸平面倒圆目录（block/parallel/all、oriented、prism、oblique）","pass",filletSteps,
    new JsonArray(
        Check("fillet-catalog","6 倒圆构造 + 各自独立生命周期（copy 删源/镜像缩放/可编辑 XT 重载/清理）全过","pass",6,6,0,"cases","complete-catalog.log 17/17 记录","evidence/complete-catalog.log"),
        Check("fillet-plane-query","同产品 plane 方向查询（生命周期各步）通过","pass",null,null,null,"","34 次同产品查询含本目录","evidence/complete-catalog.log"),
        Check("fillet-display","显示样本 prism-all-fillet 同实例 capture（28 检查，本次 exit 0、mass Verified）","pass",28,28,0,"checks","","evidence/display-binding.json"),
        Check("fillet-oblique-mass","oblique 单边物性 Unresolved（不转绿）","diagnostic",null,null,null,"","作者 oblique-single-fillet 2 次 mass Unresolved、exit 2 归属保留","evidence/complete-catalog.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),NonClaim()),"local blend fillet catalog");
var chamferSteps=ImportCapture("prism-chamfer","blend.planar-chamfer",new[]{"prism-all-chamfer"});
foreach(var s in ImportCapture("oblique-chamfer","blend.planar-chamfer",new[]{"oblique-unequal-chamfer"}))chamferSteps.Add(s!.DeepClone());
AddCase("blend.planar-chamfer","blend / 有限平面倒角目录（单/平行/全边、不等距、oblique、prism 全边）","pass",chamferSteps,
    new JsonArray(
        Check("chamfer-catalog","6 倒角构造 + 各自独立生命周期全过；目录物性 Verified","pass",6,6,0,"cases","含 inward 自然法向/负 surface sense/正 face sense 原生组合","evidence/complete-catalog.log"),
        Check("chamfer-oblique","oblique 指定面不等距：构造/严格有限方向/生命周期/清理全过，mass Verified，作者 exit 0","pass",0,0,0,"exit code","本次补采同名 case 亦 exit 0","evidence/display-binding.json"),
        Check("chamfer-display","显示样本 prism-all-chamfer + oblique-unequal-chamfer（各 28 检查）","pass",56,56,0,"checks","","evidence/display-binding.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),NonClaim()),"local blend chamfer catalog");
var axialNames=new[]{"cylinder-rim-fillet","cylinder-both-rim-fillet","cylinder-rim-chamfer","cylinder-both-rim-chamfer","cylinder-unequal-chamfer"};
var axialSteps=ImportCapture("axial-query","blend.axial-cylinder",axialNames);
AddCase("blend.axial-cylinder","blend / 轴向圆柱五操作 B-rep 与独立生命周期","pass",axialSteps,
    new JsonArray(
        Check("axial-catalog","5 圆柱操作（单/双 rim 倒圆、单/双 rim 等距倒角、不等距倒角）B-rep + 独立生命周期全过","pass",5,5,0,"cases","complete-catalog.log","evidence/complete-catalog.log"),
        Check("axial-display","显示样本 5 件同实例 capture（140 检查）","pass",140,140,0,"checks","","evidence/display-binding.json"),
        Check("axial-catalog-mass","目录物性（本目录计入 complete-catalog 的 mass_unresolved=2 归属见 blend.mass-contract-15）","diagnostic",null,null,null,"","","evidence/complete-catalog.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),NonClaim()),"axial cylinder blend operations");
AddCase("blend.axial-query-gap","blend / 五圆柱产品直接 GM BodyContainsVector 查询缺口（20 点全 5000，红）","fail",ImportCapture("axial-query","blend.axial-query-gap",axialNames),
    new JsonArray(
        Check("query-gm","GM BodyContainsVector：20 个真实 finite-plane side 点全部 error 5000","fail",20,0,0,"points","五产品各 4 点；原坐标/分类保留","evidence/axial-material-query.log"),
        Check("query-pk","PK 同点查询 error 0（Inside/Outside 分离结果）","pass",20,20,0,"points","","evidence/axial-material-query.log"),
        Check("query-exit","作者原 exit 1 + 本次复现 exit 1","fail",1,1,0,"exit code","不以任何绿色运行代签","evidence/axial-material-query.log"),
        Check("query-attribution","失败归属=独立查询 API 缺口；不否定上游 B-rep 构造/生命周期通过","pass",null,null,null,"","查询失败组合的后续生命周期未执行，Blifecycle 由 complete-catalog 独立覆盖","evidence/axial-material-query.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),(JsonNode?)JsonValue.Create("独立查询 API 缺口保留为红；不能算查询通过，也不能以它否定上游 B-rep 构造；已交共同 owner。")),"blend managed material query (BodyContainsVector)");

// 5-7 regular / chain / shortedge representatives
AddCase("blend.regular-band","blend / regular Plane/NURBS 常半径带代表（mass Unresolved，exit 2）","pass",ImportCapture("regular","blend.regular-band"),
    new JsonArray(
        Check("regular-geometry","结构、有限几何与原生对照通过（strict/full geometry）","pass",null,null,null,"","作者 regular-contract-v1 + 本次补采同参","evidence/regular-contract-v1.log"),
        Check("regular-mass","mass Unresolved 1 次（未决不转绿）；原 exit 2 保留","diagnostic",1,0,0,"items","上界合同未决不转绿","evidence/regular-contract-v1.log"),
        Check("regular-exit","原外部 exit 2（Unresolved 编码，照实保留）","diagnostic",2,2,0,"exit code","Unresolved 保持原 exit",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),NonClaim()),"regular plane/NURBS rolling band");
AddCase("blend.separate-support-chain","blend / 独立等价支持记录链代表（mass Unresolved ×3，exit 2）","pass",ImportCapture("chain","blend.separate-support-chain"),
    new JsonArray(
        Check("chain-geometry","构造、GM 重载、复制、删源、镜像缩放、清理全过（strict/full geometry）","pass",null,null,null,"","asymmetric43 separate-support 代表","evidence/chain-contract-v1.log"),
        Check("chain-mass","mass Unresolved 3 次（未决不转绿）；原 exit 2 保留","diagnostic",3,0,0,"items","","evidence/chain-contract-v1.log"),
        Check("chain-exit","原外部 exit 2（Unresolved 编码，照实保留）","diagnostic",2,2,0,"exit code","",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),NonClaim()),"separate support chain");
AddCase("blend.shortedge-single-op","blend / 短边单操作生命周期代表（6 mass Unresolved，exit 2）","pass",ImportCapture("shortedge","blend.shortedge-single-op"),
    new JsonArray(
        Check("shortedge-geometry","finite ICurve/双侧 UV/native fin box/双向 finite face domain/material + 两 self 生命周期全过","pass",null,null,null,"","blend-only-lifecycle（无后续 Boolean）","evidence/shortedge-single-v1.log"),
        Check("shortedge-product","self 产品 SHA256=37ac232e…b0ba 与作者接受成品一致；变换/重载=57eecc02…1ad2","pass",null,null,null,"","同源字节绑定；XT 正文不公开","evidence/evidence-index-14.log"),
        Check("shortedge-mass","6 次 mass Unresolved（未决不转绿）；原 exit 2 保留","diagnostic",6,0,0,"items","","evidence/shortedge-single-v1.log"),
        Check("shortedge-exit","原外部 exit 2（Unresolved 编码，照实保留）","diagnostic",2,2,0,"exit code","",null)),
    (JsonArray)evBasic.DeepClone(),new JsonArray(DisplayNote(),NonClaim()),"short-edge single-operation lifecycle");

// 8-15 query contract / proof / mass / review / unresolved / reuse / overall
AddCase("blend.face-orientation-query","blend / FaceAskOrientedSurf 两 sense 合成修正与四符号查询合同","pass",new JsonArray(),
    new JsonArray(
        Check("face-query-fix","raw face sense × surface sense 组合修正：同产品/同自然 frame 原生对照通过","pass",null,null,null,"","geometry/参数/记录不变；null surface 保持 FD 未定义","evidence/face-sign-controls.log"),
        Check("face-sign-fixtures","四 raw sign fixture：true/false/false/true，原 exit 0","pass",4,4,0,"fixtures","fixture 不冒充四个合法成品","evidence/face-sign-controls.log"),
        Check("face-query-regressions","相关回归 123/123、原 exit 0","pass",123,123,0,"tests","",null),
        Check("faceattach-boundary","负 surface reattach 组合未签（未测，见 blend.faceattach-reattach）","diagnostic",null,null,null,"","","evidence/run-receipt.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),(JsonNode?)JsonValue.Create("真实建模正例=blend.planar-chamfer 显示的单倒角/镜像重载产品；本 case 为查询合同验收。"),NonClaim()),"face orientation query contract");
AddCase("blend.direction-proof-21959","blend / 21959 条件方向判据（有限解析证明合同）","diagnostic",new JsonArray(),
    new JsonArray(
        Check("proof-contract","raw 21959 仅在唯一有限 Line/Circle/Ellipse 全段 coeff/native map/trim/端点 precision/两 incident fin 完整证明时条件通过；其余差异硬门；raw 比较 mask 保留","pass",null,null,null,"","条件证明，不是取消 sense 门","evidence/direction-controls-v5.log"),
        Check("proof-controls","direction-controls-v5：10 正/10 负，原 exit 0（含端点 precision 收紧）","pass",10,10,0,"controls","","evidence/direction-controls-v5.log"),
        Check("proof-certified","certified-directions：332 proof records、15 构造+生命周期几何全过；两次 oblique seed cleanup 失败保留（exit 1，后由 a0f68cb 集成修复）","diagnostic",332,null,null,"records","原 exit-1 运行不改写","evidence/evidence-index-33.log"),
        Check("proof-review-controls","独审新增 ellipse/圆柱负控：周期 seam 映射、端点 5e-9 偏移、半径 5e-9 超界均被拒；有界正例通过","pass",null,null,null,"","不以数量替功能","evidence/semantic-v3.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),NonClaim()),"finite analytic direction adjudication");
AddCase("blend.mass-contract-15","blend / 15 分量物性上界合同（Verified/Different/Unresolved）与未决清单","missing",new JsonArray(),
    new JsonArray(
        Check("mass-contract","合同：upper<1e-7=Verified、lower≥1e-7=Different、其余 Unresolved；15 分量 estimate/API 误差范围；几何独立验收","pass",null,null,null,"","不以数量替功能","evidence/run-receipt.json"),
        Check("mass-unresolved","未决清单：complete-catalog 2、regular 1、chain 3、shortedge 6、oblique-fillet 2（各自 exit 2 保留）","missing",14,0,0,"items","全部 Unresolved 照实显示，不转绿","evidence/complete-catalog.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),NonClaim()),"mass contract classification");
AddCase("blend.review-semantic-prism","blend / 独审语义 8 对成品与 prism 全边 chamfer 全生命周期（raw 0）","pass",new JsonArray(),
    new JsonArray(
        Check("semantic-pairs","8 对原/镜像重载成品完整 BODY_check 零、当前语义比较 raw 0；4 正/2 负独立证明控制过，原 exit 0","pass",8,8,0,"pairs","semantic-v3（v1 编译失败/v2 控制未进入证明均保留）","evidence/semantic-v3.log"),
        Check("prism-lifecycle","prism 全边 chamfer：构造/复制删源/反射缩放/重载/严格比较全过 + 32 plane query + 128 有限面材料查询 raw 0 + 两次 mass Verified，原 exit 0","pass",128,128,0,"material queries","","evidence/prism-chamfer.log"),
        Check("review-receipts","五收据/53 条目核验 0 差异（checker exit 0=身份核验，不补签失败操作）","pass",53,53,0,"entries","","evidence/evidence-index-33.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),(JsonNode?)JsonValue.Create("独审执行（gm-blend-60be-review）；显示样本见 blend.planar-chamfer 的 prism-all-chamfer。"),NonClaim()),"independent review semantic and prism lifecycle");
AddCase("blend.boundary-510","blend / 510 四向 On 分歧边界未决（历史/当前均未解决）","missing",new JsonArray(),
    new JsonArray(
        Check("boundary-510","固定 510 边界查询四向 On 分歧未决；未重跑、未豁免；共同有限 precision 合同留共同 owner","missing",null,null,null,"","历史红项沿用 f9 限定范围，不由本 run 转绿","evidence/run-receipt.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),NonClaim()),"fixed 510 boundary queries");
AddCase("blend.faceattach-reattach","blend / FaceAttach 负 surface query/copy/reattach 组合（静态线索未测）","missing",new JsonArray(),
    new JsonArray(
        Check("faceattach-clue","静态线索存在；组合未执行——不记实测失败也不签能力","missing",null,null,null,"","已交共同 owner；接受的生命周期不调用该组合","evidence/run-receipt.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),NonClaim()),"face attach reattach combination");
AddCase("blend.f9-historical-reuse","blend / f9 固定证据只读复用（历史接受，不冒充本轮新跑）","diagnostic",new JsonArray(),
    new JsonArray(
        Check("f9-reuse","96-case self Fix 矩阵、regular-bore、support-chain、corner、separated-network 按 f9 限定复用（726 索引摘要 94752804… 核验沿用；四 log 原 exit 0）","diagnostic",null,null,null,"","只读复用标明历史接受；本轮未重跑 96/1885/347","evidence/evidence-index-33.log")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),NonClaim()),"reused fixed f9 evidence");
AddCase("blend.overall-status","blend / 总体状态：限定接受，多项未决/缺口保留","fail",new JsonArray(),
    new JsonArray(
        Check("overall-accepted","接受范围=独审矩阵的 Blend B-rep 构造与独立生命周期（见各 case）","pass",null,null,null,"","限定接受","evidence/run-receipt.json"),
        Check("overall-query-gap","五圆柱产品查询缺口（红）","fail",20,0,0,"points","","evidence/axial-material-query.log"),
        Check("overall-mass","物性未决 14 项（含各 exit 2）","missing",14,0,0,"items","","evidence/complete-catalog.log"),
        Check("overall-boundary","510 边界未决 + FaceAttach 未测","missing",null,null,null,"","","evidence/run-receipt.json"),
        Check("overall-nonclaim","不签：Boolean、Mac 性能、任意 freeform、变半径、高 valence、整体 M0–M5/总目标全完成","fail",null,null,null,"","排除范围照实","evidence/run-receipt.json")),
    (JsonArray)evBasic.DeepClone(),new JsonArray(NoModelNote(),NonClaim()),"overall Blend functional status");

// ---- display binding + manifest + receipt
File.WriteAllText(Path.Combine(evidenceDir,"display-binding.json"),new JsonObject{["schemaVersion"]=1,["note"]="Same-instance display binding for the Blend 60be snapshot; capture worktree gm-board-blend-60be @ "+Candidate+"; display samples are representative captures, not the full category evidence scope.",["captures"]=captureLogHashes,["samples"]=displayBinding}.ToJsonString());
var manifest=new JsonObject
{
    ["schemaVersion"]=1,
    ["run"]=new JsonObject
    {
        ["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["pkVersion"]="38.0.150",
        ["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),
        ["scope"]=new JsonObject{["kind"]="blend-functional-acceptance-snapshot",["candidate"]=Candidate,["acceptanceReview"]=Review,["registered"]=cases.Count,["executed"]=cases.Count,["note"]="Blend 60be 限定接受矩阵快照：17/17 构造/生命周期 + 显示补采；查询缺口/物性未决/510/FaceAttach 保留；不签 Boolean/性能/总目标。"},
        ["provenance"]=new JsonObject
        {
            ["blendSource"]=new JsonObject{["tree"]="gm-blend-functional-delivery",["branch"]="feat/pk-native-blend-functional",["candidate"]=Candidate,["devBase"]=DevBase},
            ["acceptanceReview"]=new JsonObject{["tree"]="gm-blend-60be-review",["branch"]="review/pk-native-blend-60be",["commit"]=Review,["path"]="docs/reviews/advanced-modeling/blend-60be-independent-review.md",["sha256"]=ReportSha},
            ["collectorSource"]=new JsonObject{["tree"]="gm-delivery-board",["commit"]=collectorSha,["script"]="scripts/AssembleBlendAcceptance.cs",["note"]="仅装配/展示；建模来源为 60be 候选，显示为隔离树同实例补采"},
            ["displayCapture"]=new JsonObject{["worktree"]="gm-board-blend-60be (detached clean @ "+Candidate+")",["invocations"]="prism-fillet/prism-chamfer/oblique-chamfer/axial-query/regular/chain/shortedge",["exits"]=new JsonObject(exits.ToDictionary(kv=>kv.Key,kv=>(JsonNode?)JsonValue.Create(kv.Value))),["note"]="展示产物生成，非重跑 96/fullsuite；输出新目录不改原件"},
            ["receipts"]=new JsonObject{["evidenceIndex33"]=EvidenceDigest,["finalApi107"]="c2cac36671656bfccdcc34fc9ad316b1d8b0e7127ca56e77b30da7af2653e038",["extraQuery78"]="fb98501b0cb3c7451c0e29884824edc261b2b87fe553496996d0f1142e6962c7",["finalIntegrated24"]="0fb17be8b40f4d78d555f6f3c76aebdfc788f8a13f9c7f83748d340e6291bce8",["shortedge14"]="b055abaea9c88446d0983d911de087dbd406bcf25d647a9bf85bcc813919e56d"},
            ["originalExits"]=new JsonObject{["complete-catalog"]=2,["axial-material-query"]=1,["regular-contract-v1"]=2,["chain-contract-v1"]=2,["shortedge-single-v1"]=2,["direction-controls-v5"]=0,["face-sign-controls"]=0,["semantic-v3"]=0,["prism-chamfer"]=0},
            ["xtScope"]="XT 正文与私有依赖不公开；shortedge self 产品哈希绑定见 checks。",
            ["performanceScope"]="本轮不发布开发倍率（性能 code6616 已独审未正式测量）；全部倍率 null。",
            ["familyTaxonomyNote"]="family=blend（本交付为 Blend 功能族）。",
            ["caseStatusDefinitions"]=new JsonObject{["pass"]="该 case 现行必需检查通过，仅限所列范围。",["fail"]="存在必需检查失败；原 exit 独立记录。",["missing"]="未决/未测；不推断。",["diagnostic"]="诊断/合同类；不是无条件验收通过。"}
        }
    },
    ["cases"]=cases
};
File.WriteAllText(Path.Combine(run,"manifest.json"),manifest.ToJsonString());
var index=new JsonObject{["schemaVersion"]=1,["generatedAt"]=now.ToString("O"),["latestRunId"]=runId,["runs"]=new JsonArray((JsonNode)new JsonObject{["id"]=runId,["commit"]=Candidate,["generatedAt"]=now.ToString("O"),["label"]="Blend 功能交付验收快照：60be7216 + 独审 54f1b309（限定接受，查询缺口/物性未决保留）",["manifest"]=$"runs/{runId}/manifest.json"})};
File.WriteAllText(Path.Combine(output,"index.json"),index.ToJsonString());
var runBytes=Directory.EnumerateFiles(run,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
File.WriteAllText(Path.Combine(evidenceDir,"run-receipt.json"),new JsonObject
{
    ["schemaVersion"]=1,["runId"]=runId,["generatedAt"]=now.ToString("O"),["collectorCommit"]=collectorSha,
    ["blendCommit"]=Candidate,["acceptanceReview"]=Review,
    ["displayCaptureExits"]=new JsonObject(exits.ToDictionary(kv=>kv.Key,kv=>(JsonNode?)JsonValue.Create(kv.Value))),
    ["shortedgeProductSha256"]=ShortedgeProductSha,["shortedgeTransformedSha256"]=ShortedgeTransformedSha,
    ["runBytesBeforeReceiptAndValidation"]=runBytes,
    ["evidenceFiles"]=evidenceManifest,
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
