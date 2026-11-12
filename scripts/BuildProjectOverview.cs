#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Build/verify the project-wide overview (data/project-overview.json) from the
// fixed named catalog (web-progress-registry.json) and sealed acceptance
// evidence. Matching is explicit and unique: registry `selector` keys against
// original operation-log success markers, and short-edge grid names verified
// by both id-suffix and parameter tuple. Missing evidence is unverified (not
// fail); real failures are never overwritten by old greens. Optional
// --events file appends delivery records / case updates without shrinking the
// catalog; --verify regenerates to a temp copy and checks invariants.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
var output=Option("--output","web/data/project-overview.json");
var eventsPath=Option("--events","web/data/overview-events.json");
var verifyMode=args.Contains("--verify");
void Require(bool c,string m){if(!c)throw new InvalidDataException("OVERVIEW "+m);}

// ---- fixed read-only sources
var registry=JsonNode.Parse(File.ReadAllText("scripts/web-progress-registry.json"))!.AsObject();
var uniLog=Path.GetFullPath("../../gm-native-unified-delivery/temp_docs/unified-delivery",scripts);
var blendLog=Path.GetFullPath("../../gm-blend-functional-delivery",scripts);
var matrix96=Path.GetFullPath("../../gm-pk-native-delivery/temp_docs/pk-native-delivery/matrix-96-cases.txt",scripts);
var axialQuery=Path.GetFullPath("../../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log",scripts);
var uperfPkg=Path.GetFullPath("../temp_docs/incoming/unified-performance-1654/extracted-native224/docs/unified-native-controlled",scripts);
var opSummary=JsonNode.Parse(File.ReadAllText(Path.Combine(uperfPkg,"operation-summary.json")))!.AsObject();
foreach(var p in new[]{Path.Combine(uniLog,"regular45-final.log"),Path.Combine(uniLog,"mixed18-final.log"),Path.Combine(blendLog,"temp_docs/blend-functional/final-integrated/complete-catalog.log"),matrix96,axialQuery})Require(File.Exists(p),"missing evidence "+p);

// ---- strict per-scenario extraction: complete success = GEOMETRY_TOPOLOGY_PASS <key>
// (the oracle's full geometry/topology gate marker). Partial evidence alone is
// unverified (missing evidence is not failure). Explicit FAIL marks are fail.
// Per (log,key) phase-aware extraction: a MASS_BOUND phase closes at a
// MASS_AGREEMENT line or a pass/fail marker. Function derives from own
// pass/fail lines only; mass merges phases but keeps the per-phase breakdown
// and the definitive phase (own PASS) agreement so stages never conflate.
var scenarioData=new Dictionary<string,Dictionary<string,(List<(int V,int U,int D,string? Agreement,bool Pass)> Phases,int PassLines,int FailLines)>>(StringComparer.Ordinal);
void ReadScenarioLog(string path)
{
    var res=new Dictionary<string,(List<(int V,int U,int D,string? Agreement,bool Pass)> Phases,int PassLines,int FailLines)>(StringComparer.Ordinal);
    void Ensure(string k){if(!res.ContainsKey(k))res[k]=(new(),0,0);}
    foreach(var raw in File.ReadLines(path))
    {
        var t=raw.Trim();
        if(t.StartsWith("MASS_BOUND ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];bool closed=f.Phases.Count>0&&(f.Phases[^1].Agreement!=null||f.Phases[^1].Pass);
            if(f.Phases.Count==0||closed)f.Phases.Add((0,0,0,null,false));
            var last=f.Phases[^1];
            if(t.EndsWith("status=Verified",StringComparison.Ordinal))f.Phases[^1]=(last.V+1,last.U,last.D,last.Agreement,false);
            else if(t.EndsWith("status=Unresolved",StringComparison.Ordinal))f.Phases[^1]=(last.V,last.U+1,last.D,last.Agreement,false);
            else if(t.EndsWith("status=Different",StringComparison.Ordinal))f.Phases[^1]=(last.V,last.U,last.D+1,last.Agreement,false);
        }
        else if(t.StartsWith("MASS_AGREEMENT ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];bool closed=f.Phases.Count>0&&(f.Phases[^1].Agreement!=null||f.Phases[^1].Pass);
            if(f.Phases.Count==0||closed)f.Phases.Add((0,0,0,null,false));
            var st=t.Contains("status=Verified",StringComparison.Ordinal)?"Verified":t.Contains("status=Different",StringComparison.Ordinal)?"Different":t.Contains("status=Unresolved",StringComparison.Ordinal)?"Unresolved":"?";
            var last=f.Phases[^1];f.Phases[^1]=(last.V,last.U,last.D,st,false);
        }
        else if(t.StartsWith("GEOMETRY_TOPOLOGY_PASS ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];res[k]=(f.Phases,f.PassLines+1,f.FailLines);
            if(f.Phases.Count==0)f.Phases.Add((0,0,0,null,true));
            else{var last=f.Phases[^1];f.Phases[^1]=(last.V,last.U,last.D,last.Agreement,true);}
        }
        else if(t.StartsWith("GEOMETRY_TOPOLOGY_FAIL ",StringComparison.Ordinal)||t.StartsWith("FAIL ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];res[k]=(f.Phases,f.PassLines,f.FailLines+1);
        }
    }
    scenarioData[path]=res;
}
var logRegular=Path.Combine(uniLog,"regular45-final.log");
var logMixed=Path.Combine(uniLog,"mixed18-final.log");
var logComplete=Path.Combine(blendLog,"temp_docs/blend-functional/final-integrated/complete-catalog.log");
ReadScenarioLog(logRegular);ReadScenarioLog(logMixed);ReadScenarioLog(logComplete);
// suite -> own log: a selector signs only rows of its own suite's log
var suiteOwnLog=new Dictionary<string,string>(StringComparer.Ordinal){["regular-curved"]=logRegular,["mixed-analytic"]=logMixed,["local-blend"]=logComplete};
var suiteLogRel=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["regular-curved"]="../gm-native-unified-delivery/temp_docs/unified-delivery/regular45-final.log",
    ["mixed-analytic"]="../gm-native-unified-delivery/temp_docs/unified-delivery/mixed18-final.log",
    ["local-blend"]="../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/complete-catalog.log"
};

// ---- short-edge grid: exact dual verification (id-suffix + parameter tuple)
var matrixNames=File.ReadAllLines(matrix96).Where(l=>l.Trim().Length>0).Select(l=>l.Trim()).ToArray();
var shortEdgeItems=registry["cases"]!.AsArray().OfType<JsonObject>().Where(c=>c["suite"]!.GetValue<string>()=="short-edge").ToArray();
(string,double,double,double,bool,string)? NameTuple(string n)
{
    var m=System.Text.RegularExpressions.Regex.Match(n,@"^a([0-9.]+)-r([0-9.]+)-t([0-9.eE+-]+)-reverse(True|False)-(.+)$");
    return m.Success?((string)n,double.Parse(m.Groups[1].Value),double.Parse(m.Groups[2].Value),double.Parse(m.Groups[3].Value),m.Groups[4].Value=="True",m.Groups[5].Value.ToLowerInvariant()):null;
}
(string,double,double,double,bool,string)? RegTuple(JsonObject c)
{
    var p=c["parameters"]!.AsObject();
    return ((string)c["id"]!.GetValue<string>(),p["angle"]!.GetValue<double>(),p["radius"]!.GetValue<double>(),p["tolerance"]!.GetValue<double>(),p["reverseSelection"]!.GetValue<bool>(),p["update"]!.GetValue<string>().ToLowerInvariant());
}
var gridMatched=new Dictionary<string,string>(StringComparer.Ordinal); // matrix name -> registry id
var gridBad=new List<string>();
foreach(var n in matrixNames)
{
    var id="short-edge."+n.ToLowerInvariant();
    var item=shortEdgeItems.FirstOrDefault(c=>c["id"]!.GetValue<string>()==id);
    var nt=NameTuple(n);var rt=item!=null?RegTuple(item):null;
    if(item!=null&&nt!=null&&rt!=null&&nt.Value.Item2==rt.Value.Item2&&nt.Value.Item3==rt.Value.Item3&&nt.Value.Item4==rt.Value.Item4&&nt.Value.Item5==rt.Value.Item5&&nt.Value.Item6==rt.Value.Item6)gridMatched[n]=id;
    else gridBad.Add(n);
}
Require(gridBad.Count==0,"short-edge grid name/tuple mismatch: "+string.Join(",",gridBad));
var gridCovered=gridMatched.Values.ToHashSet(StringComparer.Ordinal);
var namedNonGrid=shortEdgeItems.Where(c=>!gridCovered.Contains(c["id"]!.GetValue<string>())).Select(c=>c["id"]!.GetValue<string>()).ToArray();
Require(namedNonGrid.Length==5,"unexpected short-edge non-grid set: "+string.Join(",",namedNonGrid));

// ---- explicit non-grid evidence (real history, never overwritten)
// transverse items are the real historical FAILURES (inertia gates); probes are
// unresolved query boundaries; standard lifecycle passed at f9 scope.
var nonGridFacts=new Dictionary<string,(string func,string mass,string query,string note,string evidence)>(StringComparer.Ordinal)
{
    ["short-edge.standard-strict-lifecycle"]=("pass","unverified","unverified","f9 标准生命周期几何/拓扑过（standard.log CASE + 原 exit 见 evidence）；当前模量口径未验证。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/standard.log"),
    ["short-edge.transverse-strict-lifecycle"]=("unverified","unverified","unverified","strict 场景（无 reload/witness flags）无执行日志→未验证；f9 唯一 transverse 执行为 witness 场景（inertia-handoff 复现命令带 witness/reload flags），历史契约：inertia.yz normalized15 首/末两 cut=1.2044611267910417E-07/1.126468644220207E-07>1E-7 点值门失败（原 exit 134）。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/transverse.log"),
    ["short-edge.transverse-witness-lifecycle"]=("pass","fail","unverified","witness CLI 一次执行：两 cut 均几何/拓扑/finite/material 通过（功能按已执行范围=pass）；mass 点值门失败（inertia.yz 两阶段 1.2044611267910417E-07/1.126468644220207E-07>1E-7；原 exit 134 单列）——历史点值红，非当前 modulus Different/Verified。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/transverse.log"),
    ["short-edge.self-fixed-probes"]=("pass","unverified","unresolved","BRep 构造过；query gap=510 分类边界 self 15 差异未决；原分类回归失败（self15）与原 exit 134 同列保留。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-self.log"),
    ["short-edge.imported-fixed-probes"]=("pass","unverified","unresolved","BRep 构造过；query gap=510 分类边界 import 16 差异未决；原分类回归失败（import16/native7）与原 exit 134 同列保留。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-import.log")
};

// ---- measurement five ops (explicit registry id mapping; per-process exits)
var opToRegistryId=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["sphere"]="analytic-boolean.equal-overlap-subtract",["planar"]="planar-boolean.overlap-two-axes-subtract",
    ["curved"]="regular-curved.block-subtract",["solidblend"]="solid-blend.plane-nurbs",["shortedge"]="short-edge.a1-r0.5-t1e-07-reversefalse-default"
};
foreach(var kv in opToRegistryId)Require(registry["cases"]!.AsArray().Any(e=>e!["id"]!.GetValue<string>()==kv.Value),"measurement mapping target missing "+kv.Value);
var measurementById=new Dictionary<string,JsonObject>(StringComparer.Ordinal);
foreach(var op in opSummary["operations"]!.AsArray().OfType<JsonObject>())
{
    var cid=opToRegistryId[op["case"]!.GetValue<string>()];
    measurementById[cid]=op;
}

// ---- cylinder query gap (explicit five ids; per the 60be operation table)
var cylinderQueryIds=new HashSet<string>(StringComparer.Ordinal){"local-blend.cylinder-rim-fillet","local-blend.cylinder-both-rim-fillet","local-blend.cylinder-rim-chamfer","local-blend.cylinder-both-rim-chamfer","local-blend.cylinder-unequal-chamfer"};

// ---- display links for items with real online artifacts (exact run/case/step context)
var displayLinks=new Dictionary<string,JsonObject>(StringComparer.Ordinal)
{
    ["analytic-boolean.equal-overlap-subtract"]=new JsonObject{{"runId","20261112T002227Z-uperf1654"},{"caseId","unifiedperf.sphere"},{"stepId","rep2-display"},{"note","1654 rep2 显示样本（本 case 参数）"}},
    ["planar-boolean.overlap-two-axes-subtract"]=new JsonObject{{"runId","20261112T002227Z-uperf1654"},{"caseId","unifiedperf.planar"},{"stepId","rep2-display"},{"note","1654 rep2 显示样本"}},
    ["regular-curved.block-subtract"]=new JsonObject{{"runId","20261112T002227Z-uperf1654"},{"caseId","unifiedperf.curved"},{"stepId","rep2-display"},{"note","1654 rep2 显示样本"}},
    ["solid-blend.plane-nurbs"]=new JsonObject{{"runId","20261112T002227Z-uperf1654"},{"caseId","unifiedperf.solidblend"},{"stepId","rep2-display"},{"note","1654 rep2 显示样本"}},
    ["short-edge.a1-r0.5-t1e-07-reversefalse-default"]=new JsonObject{{"runId","20261112T002227Z-uperf1654"},{"caseId","unifiedperf.shortedge"},{"stepId","rep2-display"},{"note","1654 rep2 显示样本（输入=PK seed→GM Fix）"}},
    ["local-blend.oblique-unequal-chamfer"]=new JsonObject{{"runId","20261111T190547Z-integrated1887"},{"caseId","integrated.oblique-upstream"},{"stepId","operation-1"},{"note","同字节链上游（independent-construction）显示"}},
    ["local-blend.prism-all-chamfer"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.planar-chamfer"},{"stepId","operation-1"},{"note","显示样本含 prism 全边倒角与 oblique 不等距"}},
    ["local-blend.prism-all-fillet"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.planar-fillet"},{"stepId","operation-1"},{"note","倒圆目录显示样本"}},
    ["local-blend.cylinder-rim-fillet"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.axial-cylinder"},{"stepId","operation-1"},{"note","五轴向操作同实例显示（步骤=各 cylinder case）"}},
    ["local-blend.cylinder-both-rim-fillet"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.axial-cylinder"},{"stepId","operation-2"},{"note","同上（步骤区分）"}},
    ["local-blend.cylinder-rim-chamfer"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.axial-cylinder"},{"stepId","operation-3"},{"note","同上（步骤区分）"}},
    ["local-blend.cylinder-both-rim-chamfer"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.axial-cylinder"},{"stepId","operation-4"},{"note","同上（步骤区分）"}},
    ["local-blend.cylinder-unequal-chamfer"]=new JsonObject{{"runId","20261111T174315Z-blend60be"},{"caseId","blend.axial-cylinder"},{"stepId","operation-5"},{"note","同上（步骤区分）"}}
};

var now=DateTimeOffset.UtcNow;
var counts=new Dictionary<string,int>(StringComparer.Ordinal);
void Count(string k){counts[k]=counts.TryGetValue(k,out var n)?n+1:1;}
string MassState(int v,int u,int d)=>d>0?"different":u>0?"unresolved":v>0?"verified":"unverified";
var catalogItems=new JsonArray();
var mappingAudit=new JsonArray();
foreach(var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
{
    var id=entry["id"]!.GetValue<string>();
    var suite=entry["suite"]!.GetValue<string>();
    var family=entry["family"]!.GetValue<string>();
    var selector=entry["selector"]?.GetValue<string>();
    var item=new JsonObject();
    item["id"]=JsonValue.Create(id);item["title"]=JsonValue.Create(entry["title"]!.GetValue<string>());
    item["suite"]=JsonValue.Create(suite);item["family"]=JsonValue.Create(family);
    item["operation"]=JsonValue.Create(entry["operation"]!.GetValue<string>());
    item["parametersDefinition"]=entry["parameters"]!.DeepClone();
    item["source"]=JsonValue.Create(entry["source"]!.GetValue<string>());
    item["selector"]=JsonValue.Create(selector);
    var dim=new JsonObject();
    var hist=new JsonArray();
    void Cell(string which,string status,string note,string evidence,string commit,string tree,string hostNote,string exitNote,string? utc=null)
    {
        var cell=new JsonObject();
        cell["status"]=JsonValue.Create(status);
        cell["lastVerified"]=new JsonObject{{"commit",JsonValue.Create(commit)},{"tree",JsonValue.Create(tree)},{"host",JsonValue.Create(hostNote)},{"exit",JsonValue.Create(exitNote)},{"evidence",JsonValue.Create(evidence)}};
        cell["note"]=JsonValue.Create(note);
        if(utc!=null)cell["utc"]=JsonValue.Create(utc);
        dim[which]=cell;
    }
    void Hist(string label,string status,string note){var h=new JsonObject();h["label"]=JsonValue.Create(label);h["status"]=JsonValue.Create(status);h["note"]=JsonValue.Create(note);hist.Add((JsonNode)h);}
    Cell("function","unverified","未取得当前可审计验证记录（保留在目录，不删）；未测≠不支持。","","-","-","-","-");
    Cell("mass","unverified","未取得物性记录。","","-","-","-","-");
    Cell("query","unverified","未取得查询/分类记录。","","-","-","-","-");
    Cell("performance","unverified","无性能观测。","","-","-","-","-");
    dim["currentVersion"]=JsonValue.Create("not-retested");
    // (1) 63 Boolean scenarios + local-blend 17: own-suite log only. Cross-suite
    // name collisions are documented as borrowed and stay unverified (never signed
    // by another suite's records). Function derives from own pass/fail lines; mass
    // merges phases but preserves the per-phase breakdown.
    if(selector!=null)
    {
        var borrowedLogs=new List<string>();
        foreach(var kv in scenarioData)if(kv.Value.ContainsKey(selector)&&kv.Key!=suiteOwnLog.GetValueOrDefault(suite))borrowedLogs.Add(Path.GetFileName(kv.Key));
        var borrowNote=borrowedLogs.Count>0?"selector 亦见于他套件日志 "+string.Join("/",borrowedLogs)+"（跨套件记录，不作本行证据）":"";
        (List<(int V,int U,int D,string? Agreement,bool Pass)> Phases,int PassLines,int FailLines)? own=null;
        if(suiteOwnLog.TryGetValue(suite,out var ownPath)&&scenarioData[ownPath].TryGetValue(selector,out var ownHit))own=ownHit;
        if(own!=null)
        {
            var func=own.Value.FailLines>0?"fail":own.Value.PassLines>0?"pass":"unverified";
            int v=0,u=0,d=0;foreach(var ph in own.Value.Phases){v+=ph.V;u+=ph.U;d+=ph.D;}
            var defAgree=own.Value.Phases.Where(p=>p.Pass).Select(p=>p.Agreement).LastOrDefault();
            // B5: the case's own final PASS phase is authoritative ("以自身 PASS
            // 相态为准"); merged counts stay in the note so prior phases never mix
            // into the case's own verdict.
            var mst=defAgree=="Verified"?"verified":defAgree=="Different"?"different":defAgree=="Unresolved"?"unresolved":d>0?"different":u>0?"unresolved":v>0?"verified":"unverified";
            var phaseNote=own.Value.Phases.Count>1?"逐相态："+string.Join("；",own.Value.Phases.Select((p,i)=>$"相{i+1} {p.V}V/{p.U}U/{p.D}D"+(p.Agreement!=null?$" agreement={p.Agreement}":"")))+$"；自身 PASS 相态 agreement={defAgree??"（无）"} 为准；合并仅计数不混账":"";
            var logRel=suiteLogRel[suite];
            var commit=suite=="local-blend"?"60be7216d646a296d7fd2d0b30b5f336a46a6a66":"4b2405e3945929c5bb93677a68b0ee79e430b2f3";
            var tree=suite=="local-blend"?"Native 60be":"Native 133f56ac";
            Cell("function",func,own.Value.PassLines>0?"GEOMETRY_TOPOLOGY_PASS 完整几何/拓扑门通过（自身标记 "+own.Value.PassLines+" 行）":own.Value.FailLines>0?"原日志真实失败标记（"+own.Value.FailLines+" 行）":"原日志缺完整成功标记（缺证据非失败）→未验证。"+(borrowNote!=""?"；"+borrowNote:""),logRel,commit,tree,"q2 Linux x64","批次 exit 2（逐 case 进程 exit 未单独记录，不伪造）",suite=="local-blend"?"2026-11-11T17:43:15Z":"2026-11-11T22:37:59Z");
            Cell("mass",mst,(v+u+d)==0?"原日志无 MASS_BOUND 行（分量未记录），不推断。":$"MASS_BOUND 逐分量合并 {v}V/{u}U/{d}D"+(phaseNote!=""?"；"+phaseNote:"")+(borrowNote!=""?"；"+borrowNote:""),logRel,commit,tree,"q2 Linux x64","批次 exit 2",suite=="local-blend"?"2026-11-11T17:43:15Z":"2026-11-11T22:37:59Z");
            Hist(commit.Substring(0,7)+" "+tree+"（q2 Linux）",func+" / "+mst,"逐案自身标记（本套件日志限定）；"+(phaseNote!=""?phaseNote+"；":"")+(borrowNote!=""?borrowNote+"。":""));
            mappingAudit.Add((JsonNode)new JsonObject{{"catalogId",JsonValue.Create(id)},{"suite",JsonValue.Create(suite)},{"sourceLog",JsonValue.Create(suiteLogRel[suite])},{"via",JsonValue.Create("own-suite selector="+selector)},{"status",JsonValue.Create(func)},{"borrowed",JsonValue.Create(borrowNote)}});
        }
        else if(borrowedLogs.Count>0)
        {
            Cell("function","unverified","本套件无执行证据；"+borrowNote+"→退未验证（不以同名 selector 签别套件）","-","-","-","-","-");
            Cell("mass","unverified","本套件无分量记录；"+borrowNote,"-","-","-","-","-");
            mappingAudit.Add((JsonNode)new JsonObject{{"catalogId",JsonValue.Create(id)},{"suite",JsonValue.Create(suite)},{"sourceLog",JsonValue.Create("-")},{"via",JsonValue.Create("cross-suite borrowed: "+string.Join("/",borrowedLogs))},{"status",JsonValue.Create("unverified")},{"borrowed",JsonValue.Create(borrowNote)}});
        }
    }
    // (2) short-edge 96 grid: f9 history (function pass; mass = current-modulus unverified)
    if(gridCovered.Contains(id))
    {
        Cell("function","pass","f9 96-case self Fix 矩阵几何/拓扑接受（名单逐项匹配：id 后缀+参数 tuple 双重核对）。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/matrix-96-cases.txt","f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","批次 exit 0（矩阵 shard）");
        Cell("mass","unverified","当前模量口径未验证：f9 仅有原 270 点值门记录，无三态模量结论（不新建实测 Unresolved）。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/evidence-final.sha256","f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","0");
        Hist("f9c59201 历史接受（q2 Linux）","pass / 点值门","96 矩阵几何/拓扑过；物性=点值门（非三态模量）。");
        mappingAudit.Add((JsonNode)new JsonObject{{"catalogId",JsonValue.Create(id)},{"suite",JsonValue.Create("short-edge")},{"sourceLog",JsonValue.Create("../gm-pk-native-delivery/temp_docs/pk-native-delivery/matrix-96-cases.txt")},{"via",JsonValue.Create("matrix-96 name+tuple dual match")},{"status",JsonValue.Create("pass")}});
    }
    // (3) named non-grid short-edge items (real failures / probes kept)
    if(nonGridFacts.TryGetValue(id,out var nf))
    {
        Cell("function",nf.func,nf.note,nf.evidence,"f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","见证据");
        Cell("mass",nf.mass,nf.note,nf.evidence,"f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","见证据（点值门历史/分阶段数值见注）");
        Cell("query",nf.query,nf.note,nf.evidence,"f9c59201c0b9c5d103dee45124e3f43ca59a0921","Native f9","q2 Linux x64","见证据");
        Hist("f9c59201 历史记录（q2 Linux）",nf.func,nf.note);
        mappingAudit.Add((JsonNode)new JsonObject{{"catalogId",JsonValue.Create(id)},{"suite",JsonValue.Create("short-edge")},{"sourceLog",JsonValue.Create(nf.evidence)},{"via",JsonValue.Create("named non-grid explicit evidence")},{"status",JsonValue.Create(nf.func)}});
    }
    // (4) cylinder query gap (explicit five)
    if(cylinderQueryIds.Contains(id))
    {
        Cell("query","fail","直接 GM BodyContainsVector 在 20 真实点全 error 5000、PK 0（原 exit 1）；独立查询 API 缺口，不否定 BRep 构造。","../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log","60be7216d646a296d7fd2d0b30b5f336a46a6a66","Native 60be","q2 Linux x64","1","2026-11-11T17:43:15Z");
        Hist("60be7216 查询记录（q2 Linux）","fail","20 点全 5000；历史/当前均未解。");
    }
    // (5) 1654 measurement five ops (current version re-verification)
    if(measurementById.TryGetValue(id,out var mop))
    {
        var ca=mop["candidate"]!;var b=mop["baseline"]!;
        var rom=ca["warmGmMedianMs"]!.GetValue<double>()/ca["warmPkMedianMs"]!.GetValue<double>();
        // real fields only: per-sample exits from allSamples rawExit/qualityStatus;
        // process-level exit facts live in measurement-receipt.json qualityStates
        // (exit2-Unresolved=16 / exit0-Verified=4) and runnerRawExit=2.
        var myCase=mop["case"]!.GetValue<string>();
        var sampleTally=string.Join(",",opSummary["allSamples"]!.AsArray().OfType<JsonObject>().Where(s=>s["case"]!.GetValue<string>()==myCase).GroupBy(s=>(s["rawExit"]!.GetValue<int>(),s["qualityStatus"]!.GetValue<string>())).Select(g=>$"{g.Key.Item1}x{g.Key.Item2}={g.Count()}"));
        Cell("performance","observed",
            $"1654 当前窗口：暖中位 基线 {b["warmGmMedianMs"]!.GetValue<double>().ToString("G17")}→候选 {ca["warmGmMedianMs"]!.GetValue<double>().ToString("G17")}ms，变化 {mop["changePercent"]!.GetValue<double>().ToString("G6")}%（基线→候选）；候选配对 GM/PK ratioMedian {ca["warmPairedGmPkMedian"]!.GetValue<double>().ToString("G6")} / 中位数之比 {rom.ToString("G6")}（两口径分列）；性能目标未达，runnerPerformanceQualified=false（measurement-receipt.json）。",
            "docs/uperf-1654-publish-receipt.md","1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7","Native 224564d1","Mac M4 / macOS 27.2","样本级原 exit=allSamples rawExit/qualityStatus（本 op "+sampleTally+"）；进程级见 measurement-receipt.json qualityStates（exit2-Unresolved=16/exit0-Verified=4）、runnerRawExit=2","2026-11-12T00:22:27Z");
        Cell("function","pass","1654 窗口质量门（fresh/full/strict/finite）通过（60 对全过）——当前版本复验。","docs/uperf-1654-publish-receipt.md","1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7","Native 224564d1","Mac M4 / macOS 27.2","runnerRawExit=2（measurement-receipt.json）","2026-11-12T00:22:27Z");
        var qk=ca["qualityStatusCounts"]!.AsObject();
        int qv=qk.ContainsKey("Verified")?qk["Verified"]!.GetValue<int>():0,qu=qk.ContainsKey("Unresolved")?qk["Unresolved"]!.GetValue<int>():0,qd=qk.ContainsKey("Different")?qk["Different"]!.GetValue<int>():0;
        var massStatus=qd>0?"different":qu>0?"unresolved":qv>0?"verified":"unverified";
        Cell("mass",massStatus,"1654 当前质量状态："+qk.ToJsonString()+"（每 op 候选侧 2 rep 样本状态计数，样本级口径）；物性分量总账另列：主 900 分量 312V/588U、唯一总账 1080=396V/684U（质量归档，不与样本级混算）。","docs/uperf-1654-publish-receipt.md","1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7","Native 224564d1","Mac M4 / macOS 27.2","样本级 "+sampleTally+"；进程级 measurement-receipt.json","2026-11-12T00:22:27Z");
        Cell("query","na","本窗口无查询/分类维度检查。","","-","-","-","-");
        Hist("1654 当前窗口（Mac M4）","当前版本复验","基线→候选变化 "+mop["changePercent"]!.GetValue<double>().ToString("G6")+"%；性能观测不改功能状态。");
        dim["currentVersion"]=JsonValue.Create("reverified");
        mappingAudit.Add((JsonNode)new JsonObject{{"catalogId",JsonValue.Create(id)},{"suite",JsonValue.Create(suite)},{"sourceLog",JsonValue.Create("operation-summary.json + measurement-receipt.json")},{"via",JsonValue.Create("explicit measurement mapping (op→registry id)")},{"status",JsonValue.Create("reverified")}});
    }
    item["dimensions"]=dim;item["history"]=hist;
    if(displayLinks.TryGetValue(id,out var link))item["display"]=link.DeepClone();
    Count("catalog");Count("family:"+family);
    catalogItems.Add((JsonNode)item);
}

// ---- events (repeatable update entry: deliveries + case updates; catalog stable)
var deliveries=new JsonArray();
void Delivery(string runId,string label,string commit,string utc,string note)
{
    var o=new JsonObject();o["runId"]=JsonValue.Create(runId);o["label"]=JsonValue.Create(label);o["commit"]=JsonValue.Create(commit);o["utc"]=JsonValue.Create(utc);o["note"]=JsonValue.Create(note);deliveries.Add((JsonNode)o);
}
Delivery("20261112T002227Z-uperf1654","统一性能 1654 窗口（当前版本复验 5 op）","1654dbf2","2026-11-12T00:22:27Z","current-version measurement window");
Delivery("20261111T223759Z-unified4b","统一内核 4b（功能/质量快照）","4b2405e","2026-11-11T22:37:59Z","unified snapshot");
Delivery("20261111T211605Z-gconic217fc","G+Conic 受控测量（独立历史窗口）","74c29e0","2026-11-11T21:16:05Z","historical measurement window");
Delivery("20261111T190547Z-integrated1887","功能集成 1887","1887e30d","2026-11-11T19:05:47Z","functional integration");
Delivery("20261111T174315Z-blend60be","Blend 60be 功能验收","60be7216","2026-11-11T17:43:15Z","blend acceptance");
Delivery("20261110T195008Z-610e3ce8","历史 main610 基线","610e3ce8","2026-11-10T19:50:08Z","historical baseline");
Delivery("archived","已归档快照（f9/676/6616/重复 run）","various","2026-11-10..11","release 只读 archive；成果目录保留");
// Event contract (R2): known explicit platform identities only; optional
// identity block (parameters/source) fully validated when present; stale
// guard per (catalogId, dimension); event utc must not predate the row's
// base evidence time (cells carry real utc only; unknown times are omitted,
// never fabricated); dimension/status pairing whitelist; currentVersion enum.
var knownHosts=new Dictionary<string,string>(StringComparer.Ordinal){["q2 Linux x64"]="Linux",["Mac M4 / macOS 27.2"]="Mac",["Windows arm64"]="Windows"};
var dimStatuses=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal)
{
    ["function"]=new(StringComparer.Ordinal){"pass","fail","unresolved","unverified"},
    ["mass"]=new(StringComparer.Ordinal){"verified","different","unresolved","unverified"},
    ["query"]=new(StringComparer.Ordinal){"fail","unresolved","unverified","na"},
    ["performance"]=new(StringComparer.Ordinal){"observed","unverified"}
};
var knownDims=new HashSet<string>(dimStatuses.Keys,StringComparer.Ordinal);
var knownCurrentVersions=new HashSet<string>(StringComparer.Ordinal){"reverified","not-retested"};
var rejectedEvents=new JsonArray();
var lastAppliedUtc=new Dictionary<string,DateTimeOffset>(StringComparer.Ordinal); // (catalogId|dimension)
if(File.Exists(eventsPath))
{
    var events=JsonNode.Parse(File.ReadAllText(eventsPath))!.AsArray();
    foreach(var e in events.OfType<JsonObject>())
    {
        if(e["kind"]?.GetValue<string>()=="delivery")
        {
            if(!DateTimeOffset.TryParse(e["utc"]?.GetValue<string>(),out _))rejectedEvents.Add((JsonNode?)JsonValue.Create("delivery bad utc"));
            else Delivery(e["runId"]!.GetValue<string>(),e["label"]!.GetValue<string>(),e["commit"]!.GetValue<string>(),e["utc"]!.GetValue<string>(),e["note"]!.GetValue<string>()!);
            continue;
        }
        string Reject(string why){rejectedEvents.Add((JsonNode?)JsonValue.Create(why));return why;}
        if(e["kind"]?.GetValue<string>()!="case-update"){Reject("unknown kind");continue;}
        var cid=e["catalogId"]?.GetValue<string>()??"";
        var target=catalogItems.OfType<JsonObject>().FirstOrDefault(c=>c["id"]!.GetValue<string>()==cid);
        if(target==null){Reject("unknown catalogId "+cid);continue;}
        var dimName=e["dimension"]?.GetValue<string>()??"";
        if(!knownDims.Contains(dimName)){Reject("bad dimension "+dimName+" for "+cid);continue;}
        var status=e["status"]?.GetValue<string>()??"";
        if(!dimStatuses[dimName].Contains(status)){Reject("status "+status+" not valid for dimension "+dimName+" ("+cid+")");continue;}
        if(!DateTimeOffset.TryParse(e["utc"]?.GetValue<string>(),out var when)){Reject("bad utc for "+cid);continue;}
        if(e["currentVersion"]!=null&&!knownCurrentVersions.Contains(e["currentVersion"]!.GetValue<string>())){Reject("bad currentVersion for "+cid);continue;}
        var lastVerified=e["lastVerified"];
        var evHost=lastVerified?["host"]?.GetValue<string>()??"";
        if(!knownHosts.TryGetValue(evHost,out var evFamily)){Reject("unknown platform identity '"+evHost+"' for "+cid);continue;}
        var dims=target!["dimensions"]!.AsObject();
        var baseCell=dims[dimName];
        var rowHost=baseCell?["lastVerified"]?["host"]?.GetValue<string>()??"-";
        // platform family: rows with a known family must match exactly; rows
        // still unverified (host '-') bind their platform family at first real
        // evidence instead of being locked out. Windows claims have no rows.
        bool rowMac=rowHost.Contains("Mac",StringComparison.Ordinal),rowLinux=rowHost.Contains("Linux",StringComparison.OrdinalIgnoreCase);
        if(evFamily=="Windows"&&(rowMac||rowLinux)){Reject("platform mismatch "+evHost+" for "+cid+" (row "+rowHost+")");continue;}
        if(rowMac&&evFamily!="Mac"){Reject("platform mismatch "+evHost+" for "+cid+" (row "+rowHost+")");continue;}
        if(rowLinux&&evFamily!="Linux"){Reject("platform mismatch "+evHost+" for "+cid+" (row "+rowHost+")");continue;}
        // optional identity block: when carried it must match the row exactly
        if(e["identity"]!=null)
        {
            var idn=e["identity"]!.AsObject();
            var idSource=idn["source"]?.GetValue<string>();
            if(idSource!=null&&idSource!=target!["source"]!.GetValue<string>()){Reject("identity source mismatch for "+cid+": "+idSource);continue;}
            if(idn["parameters"]!=null&&!JsonNode.DeepEquals(idn["parameters"],target!["parametersDefinition"])){Reject("identity parameters mismatch for "+cid);continue;}
        }
        // base-time guard: an event must not predate the row's real evidence
        var baseUtcText=baseCell?["utc"]?.GetValue<string>();
        if(baseUtcText!=null&&DateTimeOffset.TryParse(baseUtcText,out var baseUtc)&&when<baseUtc){Reject("event utc "+when.ToString("O")+" predates base evidence "+baseUtcText+" for "+cid+"."+dimName);continue;}
        // stale guard per (catalogId, dimension); equal utc across different
        // dimensions is legitimate and both apply
        var stKey=cid+"|"+dimName;
        if(lastAppliedUtc.TryGetValue(stKey,out var prevApplied)&&when<=prevApplied){Reject("stale event for "+cid+"."+dimName);continue;}
        if(status is "pass" or "verified" or "different")
        {
            var evd=lastVerified?["evidence"]?.GetValue<string>();
            if(string.IsNullOrEmpty(evd)){Reject("green without evidence for "+cid);continue;}
        }
        lastAppliedUtc[stKey]=when;
        var cell=new JsonObject();
        cell["status"]=JsonValue.Create(status);
        cell["lastVerified"]=lastVerified!=null?lastVerified.DeepClone():new JsonObject{{"commit",JsonValue.Create("none")},{"tree",JsonValue.Create("-")},{"host",JsonValue.Create(evHost)},{"exit",JsonValue.Create("-")},{"evidence",JsonValue.Create("")}};
        cell["note"]=JsonValue.Create((e["note"]?.GetValue<string>()??"")+"（事件 utc "+when.ToString("O")+"，平台 "+evHost+"）");
        cell["utc"]=JsonValue.Create(when.ToString("O"));
        dims[dimName]=cell;
        if(e["currentVersion"]!=null)dims["currentVersion"]=JsonValue.Create(e["currentVersion"]!.GetValue<string>());
        var h=new JsonObject();h["label"]=JsonValue.Create(e["utc"]!.GetValue<string>()+" 事件");h["status"]=JsonValue.Create(status);h["note"]=JsonValue.Create(e["note"]?.GetValue<string>()??"");
        ((JsonArray)target["history"]!).Add((JsonNode)h);
        mappingAudit.Add((JsonNode)new JsonObject{{"catalogId",JsonValue.Create(cid)},{"suite",JsonValue.Create(target!["suite"]!.GetValue<string>())},{"sourceLog",JsonValue.Create("overview-events.json")},{"via",JsonValue.Create("case-update event")},{"status",JsonValue.Create(status)}});
    }
}
// recount all row-derived counters from the post-event rows (B4: summary recompute)
foreach(var k in counts.Keys.Where(k=>k.Contains(':')&&(k.StartsWith("function:")||k.StartsWith("mass:")||k.StartsWith("query:")||k.StartsWith("performance:")||k.StartsWith("current:"))).ToList())counts.Remove(k);
foreach(var it in catalogItems.OfType<JsonObject>())
{
    var d=it["dimensions"]!.AsObject();
    foreach(var w in new[]{"function","mass","query","performance"})
    {
        var st=d[w]!["status"]!.GetValue<string>()+"";
        var ck=$"{w}:{st}";counts[ck]=(counts.TryGetValue(ck,out var c0)?c0:0)+1;
    }
    var cv=d["currentVersion"]?.GetValue<string>()??"not-retested";
    var vk="current:"+(cv=="reverified"?"reverified":"not-retested");
    counts[vk]=(counts.TryGetValue(vk,out var v0)?v0:0)+1;
}

// ---- boundary registers + verification records (not in modeling denominator)
var boundaries=new JsonArray();
void Boundary(string id,string title,string status,string note,string evidence)
{
    var o=new JsonObject();o["id"]=JsonValue.Create(id);o["title"]=JsonValue.Create(title);o["status"]=JsonValue.Create(status);o["note"]=JsonValue.Create(note);o["evidence"]=JsonValue.Create(evidence);o["inModelingDenominator"]=false;boundaries.Add((JsonNode)o);Count("boundary:"+status);
}
Boundary("boundary.probes-510","510 四向 On 分类边界分歧（self 15/import 16、native 自差 7）","unresolved","历史/当前均未决；共同有限 precision 合同留共同 owner；已映射 self/imported-fixed-probes 两行。","../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-self.log");
Boundary("boundary.faceattach-reattach","FaceAttach 负 surface query/copy/reattach","untested","仅静态线索，组合未执行。","docs/integration-1887-publish-receipt.md");
Boundary("boundary.bodycontains-axial","五圆柱产品直接 BodyContainsVector 查询缺口（20 点全 5000）","fail","已映射 cylinder-* 五 case 的 query 维度。","../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log");
Boundary("boundary.curved-roles","curved tracking/roles missing","unverified","如实中性，不补造。","docs/uperf-1654-publish-receipt.md");
var records=new JsonArray();
void Record(string id,string title,string status,string note,string evidence)
{
    var o=new JsonObject();o["id"]=JsonValue.Create(id);o["title"]=JsonValue.Create(title);o["status"]=JsonValue.Create(status);o["note"]=JsonValue.Create(note);o["evidence"]=JsonValue.Create(evidence);o["inModelingDenominator"]=false;records.Add((JsonNode)o);
}
Record("suite.mac-2000","Mac 统一包套件 2000/1998/2（原 raw 1）","fail","2 supportingBlend NotConverged 保留（同 Mac baseline 133 窄复现）；栈修复实质记录；不入建模分母。","docs/uperf-1654-publish-receipt.md");
Record("suite.linux-1974","Linux unified4b 套件 1974/1974（原 raw 0）","pass","历史 2 项本 TRX Passed；不代签 Mac 全绿。","docs/unified-4b-publish-receipt.md");
Record("mass.component-ledger","物性分量总账：主 900 分量 312V/588U/0D + 额外 180；唯一总账 1080 分量 396V/684U/0D","unresolved","分量不是功能 case；不重复计数。","docs/uperf-1654-publish-receipt.md");
Record("perf.targets","性能目标未达（各窗口分源口径）：1654 当前 shortedge 配对 387.35924x；历史窗口 217fc 340.1703x、6616 3423.49713x、676 9465.56563x","unresolved","窗口不相乘、口径不混；主体性能目标未达。","docs/uperf-1654-publish-receipt.md");

// ---- source / archive index
var sourceIndex=new JsonArray();
void Src(string name,string identity,string where,string note)
{
    var o=new JsonObject();o["name"]=JsonValue.Create(name);o["identity"]=JsonValue.Create(identity);o["where"]=JsonValue.Create(where);o["note"]=JsonValue.Create(note);sourceIndex.Add((JsonNode)o);
}
Src("统一性能 1654（当前版本）","1654dbf2/224564d1/metadata d014f76e/交付 73b06e10/独审 2f8aea49","../gm-unified-performance-1654-review + incoming/unified-performance-1654/","当前指定统一版本");
Src("统一内核 4b","4b2405e/133f56ac/独审 583d917+B17","../gm-native-unified-review + incoming/unified-delivery/","63 场景与 local17 的功能/质量历史来源");
Src("Blend 60be","60be7216/独审 54f1b309","../gm-blend-functional-delivery + ../gm-blend-60be-review","local-blend 17 与查询缺口来源");
Src("f9 计算","f9c59201/review 4c7646e","../gm-pk-native-delivery + retired-data-archive-4/","96 矩阵/横切红/510 探针来源");
Src("归档","retired-data-archive-3/4/5","release 只读 archive","归档 run 原字节可恢复；成果目录保留");
var summary=new JsonObject();
foreach(var kv in counts.OrderBy(kv=>kv.Key,StringComparer.Ordinal))summary[kv.Key]=JsonValue.Create(kv.Value);
summary["denominatorNote"]=JsonValue.Create("分母=固定目录 463 具名建模用例（稳定 ID）；边界/验证记录/单测/质量分量/性能样本不入分母；功能通过为多版本累计（各行历史分列），当前版本复验单独统计。");

var overview=new JsonObject();
overview["schemaVersion"]=1;overview["generatedAt"]=JsonValue.Create(now.ToString("O"));overview["catalogVersion"]=JsonValue.Create("2026-11-12.overview-catalog-v2");
overview["currentVersion"]=new JsonObject{{"label",JsonValue.Create("统一内核 1654（当前指定版本）")},{"calculation",JsonValue.Create("1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7")},{"native",JsonValue.Create("224564d1cdc0d0b6cf78c275282454751af2fd60")},{"measurementMetadata",JsonValue.Create("d014f76e41746a08b32e963d24b6a8b02fcaac67")},{"acceptanceReview",JsonValue.Create("2f8aea492d21f39269e514d9bcd3d8e792e3903b")},{"windowUtc",JsonValue.Create("2026-11-11T23:40:41.304661Z .. 23:42:45.017395Z")}};
overview["catalog"]=catalogItems;overview["boundaryRegisters"]=boundaries;overview["verificationRecords"]=records;
overview["deliveries"]=deliveries;overview["sourceIndex"]=sourceIndex;overview["summary"]=summary;overview["mappingAudit"]=mappingAudit;

if(verifyMode)
{
    // concentrated data-control verification (no PK/modeling runs)
    var issues=new JsonArray();
    void Issue(string s)=>issues.Add((JsonNode?)JsonValue.Create(s));
    if(catalogItems.Count!=463)Issue("catalog count changed: "+catalogItems.Count);
    // cross-suite borrow must never be green; own-suite attribution required
    foreach(var it in catalogItems.OfType<JsonObject>())
    {
        var f=it["dimensions"]!["function"]!["status"]!.GetValue<string>();
        var ev=it["dimensions"]!["function"]!["lastVerified"]!["evidence"]!.GetValue<string>();
        if(f=="pass"&&ev.Contains("regular45-final.log")&&it["suite"]!.GetValue<string>()!="regular-curved")Issue("cross-suite borrow signed green: "+it["id"]);
    }
    // PASS-after-FAIL and phase aggregation: any own FAIL line must yield fail
    foreach(var kv in scenarioData)foreach(var sk in kv.Value)if(sk.Value.FailLines>0&&sk.Value.PassLines>0)
    {
        var row=catalogItems.OfType<JsonObject>().FirstOrDefault(c=>c["selector"]?.GetValue<string>()==sk.Key);
        if(row!=null&&row["dimensions"]!["function"]!["status"]!.GetValue<string>()!="fail")Issue("PASS+FAIL key not fail: "+sk.Key+" @ "+kv.Key);
    }
    if(!counts.ContainsKey("current:reverified"))Issue("current reverify count missing");
    foreach(var r in records)if(r!["id"]!.GetValue<string>()=="interaction.round2-failures")Issue("fabricated record still present");
    foreach(var c0 in catalogItems.OfType<JsonObject>())
        foreach(var w0 in new string[]{"function","mass","query","performance"})
        {
            var cv0=c0["dimensions"]![w0]!["utc"]?.GetValue<string>();
            if(cv0!=null&&!DateTimeOffset.TryParse(cv0,out _))Issue("bad cell utc "+c0["id"]+"."+w0);
        }
    // T5 contract phrase: any green row without evidence (or a rejected
    // empty-evidence event) must be named here, never silently signed.
    foreach(var it in catalogItems.OfType<JsonObject>())
        foreach(var w in new[]{"function","mass","query","performance"})
        {
            var c=it["dimensions"]![w]!;
            if(c["status"]!.GetValue<string>() is ("pass" or "verified" or "different") && c["lastVerified"]!["evidence"]!.GetValue<string>().Length==0)Issue("green without evidence: "+it["id"]+"."+w);
        }
    foreach(var rj in rejectedEvents)if(rj!.GetValue<string>().Contains("green without evidence"))Issue(rj.GetValue<string>()+" (event rejected, row unchanged)");
    if(rejectedEvents.Count>0)Console.WriteLine("REJECTED-EVENTS "+string.Join(" | ",rejectedEvents.Select(r=>r!.GetValue<string>())));
    foreach(var kv in counts.OrderBy(kv=>kv.Key)){}
    var ids=catalogItems.OfType<JsonObject>().Select(c=>c["id"]!.GetValue<string>()).ToArray();
    if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)Issue("duplicate catalog ids");
    // wrong-parameter variants must not share rows
    var t1e5=ids.Where(i=>i.StartsWith("short-edge.a0.25-r0.25-t1e-05",System.StringComparison.Ordinal)).ToArray();
    var t1e7=ids.Where(i=>i.StartsWith("short-edge.a0.25-r0.25-t1e-07",System.StringComparison.Ordinal)).ToArray();
    if(t1e5.Length==0||t1e7.Length==0)Issue("tolerance variant rows missing");
    // transverse per B3: witness=function pass (executed scope) + mass fail
    // (point gate red, both cut stage values kept); strict=no execution evidence
    // -> unverified; never two CLI modeling failures, never modulus Different.
    {
        var w=catalogItems.OfType<JsonObject>().First(x=>x["id"]!.GetValue<string>()=="short-edge.transverse-witness-lifecycle");
        var s=catalogItems.OfType<JsonObject>().First(x=>x["id"]!.GetValue<string>()=="short-edge.transverse-strict-lifecycle");
        if(w["dimensions"]!["function"]!["status"]!.GetValue<string>()!="pass")Issue("witness function must be pass (executed scope)");
        if(w["dimensions"]!["mass"]!["status"]!.GetValue<string>()!="fail")Issue("witness mass must be point-gate fail");
        if(!w["dimensions"]!["mass"]!["note"]!.GetValue<string>().Contains("1.2044611267910417E-07"))Issue("witness mass note must keep both cut stage values");
        if(s["dimensions"]!["function"]!["status"]!.GetValue<string>()!="unverified")Issue("strict must be unverified (no execution evidence)");
    }
    foreach(var cid in cylinderQueryIds)
    {
        var c=catalogItems.OfType<JsonObject>().First(x=>x["id"]!.GetValue<string>()==cid);
        if(c["dimensions"]!["query"]!["status"]!.GetValue<string>()!="fail")Issue("query gap overwritten: "+cid);
    }
    // missing evidence not green
    foreach(var c in catalogItems.OfType<JsonObject>())
    {
        foreach(var w in new[]{"function","mass","query","performance"})
        {
            var cell=c["dimensions"]![w]!.AsObject();
            if(cell["status"]!.GetValue<string>()=="pass"&&cell["lastVerified"]!["evidence"]!.GetValue<string>().Length==0)Issue("green without evidence: "+c["id"]!.GetValue<string>()+" "+w);
        }
    }
    // regeneration consistency: produced overview must match the committed one
    // modulo generatedAt; a new event or source change must regenerate web/data.
    overview["generatedAt"]=JsonValue.Create("X");
    if(File.Exists(output))
    {
        var existing=JsonNode.Parse(File.ReadAllText(output))!.AsObject();
        existing["generatedAt"]=JsonValue.Create("X");
        if(!JsonNode.DeepEquals(overview,existing))Issue("overview drift: sources/events changed but web/data/project-overview.json not regenerated");
    }
    Console.WriteLine("VERIFY issues="+issues.Count);
    foreach(var i in issues)Console.WriteLine("  "+i!.ToJsonString());
    return issues.Count==0?0:3;
}
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output,overview.ToJsonString());
Console.WriteLine("OVERVIEW "+output+" catalog="+catalogItems.Count+" deliveries="+deliveries.Count+" bytes="+new FileInfo(output).Length);
Console.WriteLine("summary: "+string.Join(", ",counts.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+"="+kv.Value)));
Console.WriteLine("mappingAudit entries="+mappingAudit.Count);
return 0;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
static string ScriptPath([CallerFilePath]string path="")=>path;
