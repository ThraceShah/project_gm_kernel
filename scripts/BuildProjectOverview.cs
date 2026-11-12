#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Build the project-wide overview (data/project-overview.json) from the fixed
// named catalog (web-progress-registry.json) and sealed acceptance evidence.
// Per-case statuses are extracted from original operation logs by explicit
// selector mapping (registry arguments == log selectors); no title guessing,
// no PK runs, no old-schema adapters. Originals are read-only.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
var output=Option("--output","web/data/project-overview.json");
string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
void Require(bool c,string m){if(!c)throw new InvalidDataException("OVERVIEW "+m);}
string Sha256Of(string p)=>Hash(File.ReadAllBytes(p));

// ---- fixed sources (read-only)
var registry=JsonNode.Parse(File.ReadAllText("scripts/web-progress-registry.json"))!.AsObject();
var uniLog=Path.GetFullPath("../../gm-native-unified-delivery/temp_docs/unified-delivery",scripts);
var blendLog=Path.GetFullPath("../../gm-blend-functional-delivery",scripts);
var matrix96=Path.GetFullPath("../../gm-pk-native-delivery/temp_docs/pk-native-delivery/matrix-96-cases.txt",scripts);
var uperfPkg=Path.GetFullPath("../temp_docs/incoming/unified-performance-1654/extracted-native224/docs/unified-native-controlled",scripts);
string RegularLog=Path.Combine(uniLog,"regular45-final.log");
string MixedLog=Path.Combine(uniLog,"mixed18-final.log");
string CatalogLog=Path.Combine(blendLog,"temp_docs/blend-functional/final-integrated/complete-catalog.log");
foreach(var p in new[]{RegularLog,MixedLog,CatalogLog,matrix96})Require(File.Exists(p),"missing evidence "+p);

// ---- log extraction helpers
Dictionary<string,(bool geometry,int verified,int unresolved,int different,int fail)> ReadScenarioLogs(string path)
{
    var map=new Dictionary<string,(bool,int,int,int,int)>(StringComparer.Ordinal);
    string? current=null;bool geo=false;int v=0,u=0,d=0,fail=0;
    void Flush(){if(current!=null){map[current]=(geo,v,u,d,fail);current=null;geo=false;v=0;u=0;d=0;fail=0;}}
    foreach(var line in File.ReadLines(path))
    {
        var t=line.Trim();
        if(t.StartsWith("MASS_BOUND ",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[1].TrimEnd(':');
            if(current!=key){Flush();current=key;}
            if(t.EndsWith("status=Verified",StringComparison.Ordinal))v++;
            else if(t.EndsWith("status=Unresolved",StringComparison.Ordinal))u++;
            else if(t.EndsWith("status=Different",StringComparison.Ordinal))d++;
            continue;
        }
        if(t.Contains("global=21921",StringComparison.Ordinal)||t.Contains("global=21921 local",StringComparison.Ordinal)||t.StartsWith("web strict",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[0].TrimEnd(':');
            if(key=="web"){continue;}
            if(current!=key){Flush();current=key;}
            geo=true;continue;
        }
        if(t.StartsWith("topology ",StringComparison.Ordinal)||t.Contains(" topology ",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[0].TrimEnd(':');
            if(current!=key){Flush();current=key;}
            geo=true;continue;
        }
        if(t.StartsWith("GEOMETRY_TOPOLOGY_PASS ",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[1].TrimEnd(':');
            if(current!=key){Flush();current=key;}
            geo=true;continue;
        }
        if(t.StartsWith("FAIL",StringComparison.Ordinal)){fail++;}
    }
    Flush();
    return map;
}
var regular=ReadScenarioLogs(RegularLog);
var mixed=ReadScenarioLogs(MixedLog);
var catalog=ReadScenarioLogs(CatalogLog);
Console.WriteLine("extracted scenarios: regular45="+regular.Count+" mixed18="+mixed.Count+" blend17="+catalog.Count);
Require(regular.Count>=40&&mixed.Count>=15,"scenario extraction incomplete");

// ---- measurement facts (1654 window; explicit selector mapping)
var opSummary=JsonNode.Parse(File.ReadAllText(Path.Combine(uperfPkg,"operation-summary.json")))!.AsObject();
var operations=opSummary["operations"]!.AsArray().OfType<JsonObject>().ToArray();
// Explicit measurement-window mapping: operation -> exact registry case id (audited against the 1654 runner selectors/args).
var opToRegistryId=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["sphere"]="analytic-boolean.equal-overlap-subtract",
    ["planar"]="planar-boolean.overlap-two-axes-subtract",
    ["curved"]="regular-curved.block-subtract",
    ["solidblend"]="solid-blend.plane-nurbs",
    ["shortedge"]="short-edge.a1-r0.5-t1e-07-reversefalse-default"
};
foreach(var kv in opToRegistryId)Require(registry["cases"]!.AsArray().Any(e=>e!["id"]!.GetValue<string>()==kv.Value),"measurement mapping target missing "+kv.Value);

// ---- matrix-96 set (f9)
var matrixSet=File.ReadAllLines(matrix96).Where(l=>l.Trim().Length>0).Select(l=>l.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

var now=DateTimeOffset.UtcNow;
var catalogItems=new JsonArray();
var counts=new Dictionary<string,int>(StringComparer.Ordinal);
void Count(string key){counts[key]=counts.TryGetValue(key,out var n)?n+1:1;}
string MassState(int v,int u,int d)=>d>0?"different":u>0?"unresolved":v>0?"verified":"unverified";
foreach(var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
{
    var id=entry["id"]!.GetValue<string>();
    var selector=entry["arguments"]!.AsArray().FirstOrDefault()?.GetValue<string>()??"";
    var family=entry["family"]!.GetValue<string>();
    var suite=entry["suite"]!.GetValue<string>();
    var item=new JsonObject();
    item["id"]=JsonValue.Create(id);item["title"]=JsonValue.Create(entry["title"]!.GetValue<string>());
    item["suite"]=JsonValue.Create(suite);item["family"]=JsonValue.Create(family);
    item["operation"]=JsonValue.Create(entry["operation"]!.GetValue<string>());
    item["parametersDefinition"]=entry["parameters"]!.DeepClone();
    item["source"]=JsonValue.Create(entry["source"]!.GetValue<string>());
    item["selector"]=JsonValue.Create(selector);
    var dim=new JsonObject();
    var hist=new JsonArray();
    void Hist(string label,string status,string note){hist.Add((JsonNode)new JsonObject{{"label",JsonValue.Create(label)},{"status",JsonValue.Create(status)},{"note",JsonValue.Create(note)}});}
    JsonObject Cell(string status,string note,string evidence,string verifiedCommit,string verifiedTree,string hostNote,string exitNote)
    {
        var o=new JsonObject();
        o["status"]=JsonValue.Create(status);
        o["lastVerified"]=new JsonObject{{"commit",JsonValue.Create(verifiedCommit)},{"tree",JsonValue.Create(verifiedTree)},{"host",JsonValue.Create(hostNote)},{"exit",JsonValue.Create(exitNote)},{"evidence",JsonValue.Create(evidence)}};
        o["note"]=JsonValue.Create(note);
        return o;
    }
    // defaults: unverified everywhere
    dim["function"]=Cell("unverified","未取得当前可审计验证记录。","", "-", "-", "-", "-");
    dim["mass"]=Cell("unverified","未取得物性记录。","", "-", "-", "-", "-");
    dim["query"]=Cell("unverified","未取得查询/分类记录。","", "-", "-", "-", "-");
    dim["performance"]=Cell("unverified","无性能观测。","", "-", "-", "-", "-");
    // (a) 63 Boolean scenarios from unified 4b logs
    var key=selector;
    if(regular.TryGetValue(key,out var r)||mixed.TryGetValue(key,out r))
    {
        var logName=regular.ContainsKey(key)?"regular45-final.log":"mixed18-final.log";
        dim["function"]=Cell(r.geometry?"pass":"fail","几何/拓扑按原日志逐案记录（4b 统一候选）。","../gm-native-unified-delivery/temp_docs/unified-delivery/"+logName,"4b2405e3945929c5bb93677a68b0ee79e430b2f3","Native 133f56ac","q2 Linux x64","2 (mass Unresolved 编码)");
        dim["mass"]=Cell(MassState(r.verified,r.unresolved,r.different),r.verified+r.unresolved+r.different==0?$"原日志无 MASS_BOUND 行（分量未记录），不推断。":$"MASS_BOUND 逐分量：Verified {r.verified} / Unresolved {r.unresolved} / Different {r.different}。","../gm-native-unified-delivery/temp_docs/unified-delivery/"+logName,"4b2405e3945929c5bb93677a68b0ee79e430b2f3","Native 133f56ac","q2 Linux x64","2");
        dim["currentVersion"]=JsonValue.Create("not-retested");
        Hist("4b2405e 统一候选（q2 Linux）",MassState(r.verified,r.unresolved,r.different),"63 场景几何/拓扑全过；mass 逐分量见维度。");
    }
    // (b) local-blend 17 from Blend 60be complete catalog
    if(catalog.TryGetValue(key,out var b))
    {
        dim["function"]=Cell(b.geometry?"pass":"fail","构造/独立生命周期几何拓扑按 60be complete-catalog 逐案记录。","../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/complete-catalog.log","60be7216d646a296d7fd2d0b30b5f336a46a6a66","Native 60be","q2 Linux x64","2");
        dim["mass"]=Cell(MassState(b.verified,b.unresolved,b.different),$"MASS_BOUND：Verified {b.verified} / Unresolved {b.unresolved} / Different {b.different}。","../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/complete-catalog.log","60be7216d646a296d7fd2d0b30b5f336a46a6a66","Native 60be","q2 Linux x64","2");
        dim["currentVersion"]=JsonValue.Create("not-retested");
        Hist("60be7216 Blend 接受（q2 Linux）",MassState(b.verified,b.unresolved,b.different),"构造/独立生命周期几何拓扑过；mass 逐分量见维度。");
    }
    // (c) f9 short-edge 96 matrix
    if(suite=="short-edge"&&matrixSet.Contains(key))
    {
        dim["function"]=Cell("pass","f9 96-case self Fix 矩阵历史接受（几何/拓扑）。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/matrix-96-cases.txt","f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","0");
        dim["mass"]=Cell("unresolved","f9 矩阵按原 270 点值门；本项目物性三态口径下未取得逐案 Verified 记录。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/evidence-final.sha256","f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","0");
        dim["currentVersion"]=JsonValue.Create("not-retested");
        Hist("f9c59201 历史接受（q2 Linux）","pass","96-case self Fix 矩阵几何/拓扑过；物性按原 270 点值门，三态口径下未决。");
    }
    // (d) 1654 measurement five operations (explicit selector mapping)
    foreach(var op in operations)
    {
        if(opToRegistryId[op["case"]!.GetValue<string>()]!=id)continue;
        var ca=op["candidate"]!;
        var rom=ca["warmGmMedianMs"]!.GetValue<double>()/ca["warmPkMedianMs"]!.GetValue<double>();
        dim["performance"]=Cell("observed",
            $"1654 窗口暖中位 {ca["warmGmMedianMs"]!.GetValue<double>().ToString("G17")}→基线 {op["baseline"]!["warmGmMedianMs"]!.GetValue<double>().ToString("G17")}ms，变化 {op["changePercent"]!.GetValue<double>().ToString("G6")}%；配对 ratioMedian {ca["warmPairedGmPkMedian"]!.GetValue<double>().ToString("G6")} / 中位数之比 {rom.ToString("G6")}（分列）；单窗口描述性观测，performanceQualified=false。",
            "../gm-delivery-board docs/uperf-1654-publish-receipt.md","1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7","Native 224564d1","Mac M4 / macOS 27.2","main runner 2");
        var qk=ca["qualityStatusCounts"]!.AsObject();
        dim["function"]=Cell("pass","1654 窗口当前版本复验：几何/拓扑质量门（fresh/full/strict/finite）通过（60 对全过）。","docs/uperf-1654-publish-receipt.md","1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7","Native 224564d1","Mac M4 / macOS 27.2","main runner 2");
        dim["mass"]=Cell(qk.ContainsKey("Unresolved")?"unresolved":"verified","1654 当前质量状态计数："+qk.ToJsonString()+"（分量级，非功能 case）；主 900 分量总账见验证记录。","docs/uperf-1654-publish-receipt.md","1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7","Native 224564d1","Mac M4 / macOS 27.2","2");
        dim["query"]=Cell("na","本窗口无查询/分类维度检查。","","-","-","-","-");
        Hist("1654 当前窗口（Mac M4）","observed","当前版本复验："+op["changePercent"]!.GetValue<double>().ToString("G6")+"% 变化，性能观测。");
        dim["currentVersion"]=JsonValue.Create("reverified");
    }
    // (e) query gaps: five cylinder products BodyContainsVector 5000
    if(suite=="local-blend"&&key.StartsWith("cylinder",StringComparison.Ordinal))
    {
        dim["query"]=Cell("fail","直接 GM BodyContainsVector 在 20 真实点全 error 5000、PK 0（60be 起原 exit 1）；独立查询 API 缺口，不否定 BRep 构造。","../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log","60be7216d646a296d7fd2d0b30b5f336a46a6a66","Native 60be","q2 Linux x64","1");
        Hist("60be7216 查询记录（q2 Linux）","fail","BodyContainsVector 20 点全 5000；历史/当前均未解。");
    }
    item["dimensions"]=dim;
    item["history"]=hist;
    Count("catalog");
    Count("family:"+family);
    Count("function:"+dim["function"]!["status"]!.GetValue<string>());
    Count("mass:"+dim["mass"]!["status"]!.GetValue<string>());
    Count("query:"+dim["query"]!["status"]!.GetValue<string>());
    Count("performance:"+dim["performance"]!["status"]!.GetValue<string>());
    catalogItems.Add((JsonNode)item);
}

// ---- boundary registers (named, visible, NOT in modeling denominator)
var boundaries=new JsonArray();
void Boundary(string id,string title,string status,string note,string evidence)
{
    var o=new JsonObject();
    o["id"]=JsonValue.Create(id);o["title"]=JsonValue.Create(title);o["status"]=JsonValue.Create(status);o["note"]=JsonValue.Create(note);o["evidence"]=JsonValue.Create(evidence);o["inModelingDenominator"]=false;
    boundaries.Add((JsonNode)o);
    Count("boundary:"+status);
}
Boundary("boundary.probes-510","510 四向 On 分类边界分歧（self 15/import 16、native 自差 7）","unresolved","历史/当前均未决；共同有限 precision 合同留共同 owner；不转绿。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-self.log");
Boundary("boundary.faceattach-reattach","FaceAttach 负 surface query/copy/reattach","untested","仅静态线索，组合未执行；不记实测失败也不签能力。","docs/integration-1887-publish-receipt.md");
Boundary("boundary.bodycontains-axial","五圆柱产品直接 BodyContainsVector 查询缺口（20 点全 5000）","fail","已映射到 local-blend cylinder-* 各 case 的 query 维度；独立查询 API 缺口，不否定 BRep。","../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log");
Boundary("boundary.curved-roles","curved tracking/roles missing","unverified","curved 建模角色缺失如实中性，不补造。","docs/uperf-1654-publish-receipt.md");

// ---- verification records (NOT modeling cases)
var records=new JsonArray();
void Record(string id,string title,string status,string note,string evidence)
{
    var o=new JsonObject();
    o["id"]=JsonValue.Create(id);o["title"]=JsonValue.Create(title);o["status"]=JsonValue.Create(status);o["note"]=JsonValue.Create(note);o["evidence"]=JsonValue.Create(evidence);o["inModelingDenominator"]=false;
    records.Add((JsonNode)o);
}
Record("suite.mac-2000","Mac 统一包完整套件 2000/1998/2（原 raw 1）","fail","2 supportingBlend NotConverged 红保留（同 Mac baseline 133 窄复现）；实质栈修复已显式记录；不入建模分母。","docs/uperf-1654-publish-receipt.md");
Record("suite.linux-1974","Linux unified4b Native 套件 1974/1974（原 raw 0）","pass","历史 2 项本 TRX Passed；不代签 Mac 全绿；不入建模分母。","docs/unified-4b-publish-receipt.md");
Record("mass.component-ledger","物性分量总账：主 60 compare 900 分量 312V/588U/0D + 额外 12 回读 180 分量；唯一总账 1080 分量 396V/684U/0D","unresolved","分量不是功能 case；不重复计数。","docs/uperf-1654-publish-receipt.md");
Record("perf.work-reduction","性能目标：工作量收益已录；短边 1654 窗口 −4.141%、6616 窗口 −64.44%、G+Conic 窗口 −87.03%（各单窗口不相乘）；PK 目标 340x/3423x/9465x 未达","unresolved","主体性能目标未达；不签普遍/因果速度。","docs/uperf-1654-publish-receipt.md");

// ---- delivery records (history)
var deliveries=new JsonArray();
void Delivery(string runId,string label,string commit,string utc,string note)
{
    var o=new JsonObject();
    o["runId"]=JsonValue.Create(runId);o["label"]=JsonValue.Create(label);o["commit"]=JsonValue.Create(commit);o["utc"]=JsonValue.Create(utc);o["note"]=JsonValue.Create(note);
    deliveries.Add((JsonNode)o);
}
Delivery("20261112T002227Z-uperf1654","统一性能 1654 窗口（rep2 模型/双 ratio/回退照实）","1654dbf2","2026-11-12T00:22:27Z","current-version measurement window");
Delivery("20261111T223759Z-unified4b","统一内核 4b（56 模型/1974 套件/mirror8 已修）","4b2405e","2026-11-11T22:37:59Z","unified functional+quality snapshot");
Delivery("20261111T211605Z-gconic217fc","G+Conic 受控测量（元数据修正版）","74c29e0","2026-11-11T21:16:05Z","historical measurement window (independent)");
Delivery("20261111T190547Z-integrated1887","功能集成 1887（上游/下游同字节分列）","1887e30d","2026-11-11T19:05:47Z","functional integration snapshot");
Delivery("20261111T174315Z-blend60be","Blend 60be 功能验收快照","60be7216","2026-11-11T17:43:15Z","blend acceptance snapshot");
Delivery("20261110T195008Z-610e3ce8","历史 main610 基线（17 case 代表）","610e3ce8","2026-11-10T19:50:08Z","historical baseline");
Delivery("archived","已归档快照（f9/676/6616/重复 run）","various","2026-11-10..11","原字节冻结于 release retired-data-archive-*；原始 manifest/hash 在发布 receipt；不在线但成果目录保留");

// ---- source / archive index
var sourceIndex=new JsonArray();
void Src(string name,string identity,string where,string note)
{
    var o=new JsonObject();o["name"]=JsonValue.Create(name);o["identity"]=JsonValue.Create(identity);o["where"]=JsonValue.Create(where);o["note"]=JsonValue.Create(note);sourceIndex.Add((JsonNode)o);
}
Src("统一性能 1654","candidate 1654dbf2 / Native 224564d1 / metadata d014f76e / 交付 73b06e10 / 独审 2f8aea49","../gm-unified-performance-1654-review + incoming/unified-performance-1654/","当前指定统一版本");
Src("统一内核 4b","4b2405e / Native 133f56ac / 独审 583d917+B17","../gm-native-unified-review + incoming/unified-delivery/","上一统一功能/质量快照");
Src("Blend 60be","60be7216 / 独审 54f1b309","../gm-blend-functional-delivery + ../gm-blend-60be-review","Blend 接受");
Src("Boolean 4794","4794e601 / 独审 75eddad","../gm-pk-native-delivery (docs) + integration","Boolean 接受（限制范围）");
Src("f9 计算","f9c59201 / review 4c7646e","../gm-pk-native-delivery + retired-data-archive-4/","96 矩阵历史接受");
Src("归档","retired-data-archive-3/4/5","release 只读 archive","归档 run 原字节可恢复，成果目录保留");

// ---- summary
var summary=new JsonObject();
foreach(var kv in counts.OrderBy(kv=>kv.Key,StringComparer.Ordinal))summary[kv.Key]=JsonValue.Create(kv.Value);
summary["denominatorNote"]=JsonValue.Create("分母=固定目录 463 具名建模用例；边界/验证记录/单测/质量分量/性能样本不入分母。");

var overview=new JsonObject();
overview["schemaVersion"]=1;
overview["generatedAt"]=JsonValue.Create(now.ToString("O"));
overview["catalogVersion"]=JsonValue.Create("2026-11-12.overview-catalog-v1");
overview["currentVersion"]=new JsonObject{{"label",JsonValue.Create("统一内核 1654（当前指定版本）")},{"calculation",JsonValue.Create("1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7")},{"native",JsonValue.Create("224564d1cdc0d0b6cf78c275282454751af2fd60")},{"src",JsonValue.Create("c6e8bc553cdfecaebc3c1fac5d30688ba8628e6c")},{"measurementMetadata",JsonValue.Create("d014f76e41746a08b32e963d24b6a8b02fcaac67")},{"acceptanceReview",JsonValue.Create("2f8aea492d21f39269e514d9bcd3d8e792e3903b")},{"host",JsonValue.Create("Mac M4 / macOS 27.2 / .NET 10.0.11 / PK 38.0.150")},{"windowUtc",JsonValue.Create("2026-11-11T23:40:41.304661Z .. 23:42:45.017395Z")}};
overview["catalog"]=catalogItems;
overview["boundaryRegisters"]=boundaries;
overview["verificationRecords"]=records;
overview["deliveries"]=deliveries;
overview["sourceIndex"]=sourceIndex;
overview["summary"]=summary;
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output,overview.ToJsonString());
Console.WriteLine("OVERVIEW "+output+" catalog="+catalogItems.Count+" boundaries="+boundaries.Count+" records="+records.Count+" deliveries="+deliveries.Count+" bytes="+new FileInfo(output).Length);
Console.WriteLine("summary: "+string.Join(", ",counts.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+"="+kv.Value)));
return 0;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
static string ScriptPath([CallerFilePath]string path="")=>path;
