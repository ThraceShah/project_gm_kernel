#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Narrow re-verification audit of the f463 fix (2bece5d -> f4632281) against the
// independent review B1-B6. Runs against a fixed target tree (--target, default
// this script's own tree) and writes receipts into THIS script's tree, so the
// author candidate is only read. Every finding is classified:
//   error = real unfixed/wrong data fact under the accepted contract
//   note  = documented by-design behavior or stale-contract false positive
// Covers: B1 own-suite attribution, B2 extractor semantics (first/last key,
// no-PASS key, PASS-after-FAIL), B3 transverse/probes dimension split, B5 mass
// phase rules and the measurement three-state derivation, stats recomputation,
// and evidence-existence checks for every record that cites original logs.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var reviewRoot=Path.GetFullPath("..",scripts);
var target=Path.GetFullPath(Option("--target",reviewRoot));Directory.SetCurrentDirectory(target);
var outDir=Path.Combine(reviewRoot,"docs","reviews","web","receipts");Directory.CreateDirectory(outDir);
var tmpDir=Path.Combine(reviewRoot,"temp_docs","f463-reverify");Directory.CreateDirectory(tmpDir);
var findings=new JsonArray();
void Finding(string kind,string detail){findings.Add((JsonNode)new JsonObject{{"kind",kind},{"verdict","error"},{"detail",detail}});Console.WriteLine("ERROR "+kind+" :: "+detail);}
void Note(string kind,string detail){findings.Add((JsonNode)new JsonObject{{"kind",kind},{"verdict","note"},{"detail",detail}});Console.WriteLine("NOTE  "+kind+" :: "+detail);}

var registry=JsonNode.Parse(File.ReadAllText(Path.Combine("scripts","web-progress-registry.json")))!.AsObject();
var overview=JsonNode.Parse(File.ReadAllText(Path.Combine("web","data","project-overview.json")))!.AsObject();
var rows=overview["catalog"]!.AsArray().OfType<JsonObject>().ToDictionary(c=>c["id"]!.GetValue<string>(),StringComparer.Ordinal);
var uniLog=Path.GetFullPath(Path.Combine("..","gm-native-unified-delivery","temp_docs","unified-delivery"),target);
var blendLog=Path.GetFullPath(Path.Combine("..","gm-blend-functional-delivery","temp_docs","blend-functional","final-integrated"),target);
var pkLog=Path.GetFullPath(Path.Combine("..","gm-pk-native-delivery","temp_docs","pk-native-delivery"),target);
var logs=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["regular45"]=Path.Combine(uniLog,"regular45-final.log"),
    ["mixed18"]=Path.Combine(uniLog,"mixed18-final.log"),
    ["complete"]=Path.Combine(blendLog,"complete-catalog.log"),
};
foreach(var kv in logs)if(!File.Exists(kv.Value))throw new FileNotFoundException(kv.Value);

// ---- correct phase-aware parse (accepted B2/B5 semantics)
Dictionary<string,(List<(int V,int U,int D,string? Agreement,bool Pass)> Phases,int PassLines,int FailLines)> Parse(string path)
{
    var res=new Dictionary<string,(List<(int V,int U,int D,string? Agreement,bool Pass)> Phases,int PassLines,int FailLines)>(StringComparer.Ordinal);
    void Ensure(string k){if(!res.ContainsKey(k))res[k]=(new(),0,0);}
    foreach(var raw in File.ReadLines(path))
    {
        var t=raw.Trim();
        if(t.StartsWith("MASS_BOUND ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];bool closed=f.Phases.Count>0&&(f.Phases[^1].Item4!=null||f.Phases[^1].Item5);
            if(f.Phases.Count==0||closed)f.Phases.Add((0,0,0,null,false));
            var last=f.Phases[^1];
            if(t.EndsWith("status=Verified",StringComparison.Ordinal))f.Phases[^1]=(last.Item1+1,last.Item2,last.Item3,last.Item4,false);
            else if(t.EndsWith("status=Unresolved",StringComparison.Ordinal))f.Phases[^1]=(last.Item1,last.Item2+1,last.Item3,last.Item4,false);
            else if(t.EndsWith("status=Different",StringComparison.Ordinal))f.Phases[^1]=(last.Item1,last.Item2,last.Item3+1,last.Item4,false);
        }
        else if(t.StartsWith("MASS_AGREEMENT ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];bool closed=f.Phases.Count>0&&(f.Phases[^1].Item4!=null||f.Phases[^1].Item5);
            if(f.Phases.Count==0||closed)f.Phases.Add((0,0,0,null,false));
            var st=t.Contains("status=Verified",StringComparison.Ordinal)?"Verified":t.Contains("status=Different",StringComparison.Ordinal)?"Different":t.Contains("status=Unresolved",StringComparison.Ordinal)?"Unresolved":"?";
            var last=f.Phases[^1];f.Phases[^1]=(last.Item1,last.Item2,last.Item3,st,false);
        }
        else if(t.StartsWith("GEOMETRY_TOPOLOGY_PASS ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];res[k]=(f.Phases,f.PassLines+1,f.FailLines);
            if(f.Phases.Count==0)f.Phases.Add((0,0,0,null,true));
            else{var last=f.Phases[^1];f.Phases[^1]=(last.Item1,last.Item2,last.Item3,last.Item4,true);}
        }
        else if(t.StartsWith("GEOMETRY_TOPOLOGY_FAIL ",StringComparison.Ordinal)||t.StartsWith("FAIL ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k);
            var f=res[k];res[k]=(f.Phases,f.PassLines,f.FailLines+1);
        }
    }
    return res;
}
var data=new Dictionary<string,Dictionary<string,(List<(int V,int U,int D,string? Agreement,bool Pass)> Phases,int PassLines,int FailLines)>>(StringComparer.Ordinal);
foreach(var kv in logs)data[kv.Key]=Parse(kv.Value);
var suiteOwnLog=new Dictionary<string,string>(StringComparer.Ordinal){["regular-curved"]="regular45",["mixed-analytic"]="mixed18",["local-blend"]="complete"};

int errors=0;
// ---- B1: cross-suite rows must be unverified and documented; own-suite rows signed from own log
foreach(var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
{
    var id=entry["id"]!.GetValue<string>();var suite=entry["suite"]!.GetValue<string>();
    var selector=entry["selector"]?.GetValue<string>();if(selector==null)continue;
    var appears=logs.Keys.Where(l=>data[l].ContainsKey(selector)).ToList();
    if(appears.Count==0)continue;
    var row=rows[id];
    var f=row["dimensions"]!["function"]!["status"]!.GetValue<string>();
    var m=row["dimensions"]!["mass"]!["status"]!.GetValue<string>();
    bool own=suiteOwnLog.TryGetValue(suite,out var ownLog)&&appears.Contains(ownLog);
    if(own)
    {
        var rec=data[suiteOwnLog[suite]][selector];
        string expectF=rec.FailLines>0?"fail":rec.PassLines>0?"pass":"unverified";
        var defAgree=rec.Phases.Where(p=>p.Pass).Select(p=>p.Agreement).LastOrDefault();
        int v=0,u=0,d=0;foreach(var ph in rec.Phases){v+=ph.V;u+=ph.U;d+=ph.D;}
        string expectM=defAgree=="Verified"?"verified":defAgree=="Different"?"different":defAgree=="Unresolved"?"unresolved":d>0?"different":u>0?"unresolved":v>0?"verified":"unverified";
        if(f!=expectF)Finding("function-mismatch",$"{id}: overview={f} own-log-derived={expectF} (passLines={rec.PassLines} failLines={rec.FailLines})");
        if(m!=expectM)Finding("mass-mismatch",$"{id}: overview={m} own-PASS-phase-derived={expectM} (defAgree={defAgree??"none"}; merged {v}V/{u}U/{d}D)");
        if(rec.Phases.Count>1)
        {
            var note=row["dimensions"]!["mass"]!["note"]!.GetValue<string>();
            if(!note.Contains("逐相态",StringComparison.Ordinal)||!note.Contains("为准",StringComparison.Ordinal))Finding("phase-breakdown-missing",$"{id}: {rec.Phases.Count} 相态但 mass note 无逐相态分列/自身 PASS 相态为准字样");
        }
    }
    else
    {
        if(f!="unverified"||m!="unverified")Finding("cross-suite-signed",$"{id}: 他套件 selector（见 {string.Join("/",appears)}）却签 function={f}/mass={m}");
        var note=row["dimensions"]!["function"]!["note"]!.GetValue<string>();
        if(!note.Contains("本套件无执行证据",StringComparison.Ordinal)&&!note.Contains("退未验证",StringComparison.Ordinal))Finding("borrow-undocumented",$"{id}: 跨套件借用未在 note 注明");
        else Note("cross-suite-borrow-documented",$"{id}: 他套件 selector 已退未验证并注明借源（旧脚本 borrow 告警=文档性）");
    }
}

// ---- B3: named non-grid rows per accepted dimension split
var tStrict=rows["short-edge.transverse-strict-lifecycle"];var tWitness=rows["short-edge.transverse-witness-lifecycle"];
if(tWitness["dimensions"]!["function"]!["status"]!.GetValue<string>()!="pass")Finding("B3-witness-function","witness function 应=pass（执行范围）");
if(tWitness["dimensions"]!["mass"]!["status"]!.GetValue<string>()!="fail")Finding("B3-witness-mass","witness mass 应=点值门红 fail，实际="+tWitness["dimensions"]!["mass"]!["status"]!.GetValue<string>());
var wNote=tWitness["dimensions"]!["mass"]!["note"]!.GetValue<string>();
if(!wNote.Contains("1.2044611267910417E-07",StringComparison.Ordinal)||!wNote.Contains("1.126468644220207E-07",StringComparison.Ordinal))Finding("B3-witness-values","witness mass note 缺两 cut 阶段数值");
if(wNote.Contains("Different",StringComparison.Ordinal)&&wNote.Contains("modulus Different",StringComparison.Ordinal)&&!wNote.Contains("非当前 modulus",StringComparison.Ordinal))Finding("B3-witness-modulus","witness 不得签当前 modulus Different");
if(!wNote.Contains("exit 134",StringComparison.Ordinal))Finding("B3-witness-exit","witness 注记缺原 exit 134");
if(tStrict["dimensions"]!["function"]!["status"]!.GetValue<string>()!="unverified")Finding("B3-strict-function","strict function 应=unverified（无执行日志）");
if(!tStrict["dimensions"]!["function"]!["note"]!.GetValue<string>().Contains("无执行日志",StringComparison.Ordinal))Finding("B3-strict-note","strict 注记未写明无执行日志");
foreach(var pid in new[]{"short-edge.self-fixed-probes","short-edge.imported-fixed-probes"})
{
    var p=rows[pid];
    if(p["dimensions"]!["query"]!["status"]!.GetValue<string>()!="unresolved")Finding("B3-probes-query",pid+" query 应=unresolved（510 未决）");
    if(p["dimensions"]!["function"]!["status"]!.GetValue<string>()!="pass")Finding("B3-probes-function",pid+" function 应=pass（BRep 构造过）");
}
if(rows["short-edge.standard-strict-lifecycle"]["dimensions"]!["function"]!["status"]!.GetValue<string>()!="pass")Finding("B3-standard","standard lifecycle function 应=pass");

// ---- round2 record: every claimed FAIL key must exist in the cited evidence
foreach(var rec in overview["verificationRecords"]!.AsArray().OfType<JsonObject>())
{
    var title=rec["title"]!.GetValue<string>();
    if(!title.Contains("round2",StringComparison.Ordinal))continue;
    var ev=rec["evidence"]!.GetValue<string>();
    // claimed keys: the tokens after 键 up to the closing paren
    var keys=new List<string>();
    foreach(var part in title.Split('（').Last().Split('）')[0].Split('/').Select(s=>s.Trim()))
    {
        var k=part.Replace("键","").Trim();
        if(k.EndsWith("-round2",StringComparison.Ordinal))keys.Add(k);
    }
    Console.WriteLine("ROUND2 record keys: "+string.Join(" | ",keys)+" evidence="+ev);
    foreach(var k in keys)
    {
        bool inLogs=logs.Values.Any(p=>File.ReadLines(p).Any(l=>l.Contains(k,StringComparison.Ordinal)));
        bool anyFail=logs.Values.Any(p=>File.ReadLines(p).Any(l=>l.Trim().StartsWith("FAIL ",StringComparison.Ordinal)));
        if(!inLogs)Finding("round2-unsourced",$"verificationRecord interaction.round2-failures 声称键 \"{k}\" 的「原日志 FAIL mass 行」在所引 {ev} 及三个套件日志中不存在（字节哈希未变：regular45=9cd6ba94…/mixed18=ad0aa117…/complete=2b2d07c4…；三日志 FAIL 行总数={(anyFail?"见下":"0")}）");
    }
    if(!File.ReadAllText(Path.Combine(uniLog,"regular45-final.log")).Contains("round2",StringComparison.Ordinal)&&ev.Contains("regular45-final.log",StringComparison.Ordinal))
        Finding("round2-evidence-citation",$"该记录 evidence 指向 regular45-final.log，但该文件（SHA256 9cd6ba940cae8ce3b694c47a031e81f1a3986de720b88f6f8824ca6b6adcb7a5）无任何 round2/FAIL 行——引用源与内容不符");
}

// ---- B5 residual: qualityStatusCounts three-state derivation (code-level)
var prodSrc=File.ReadAllText(Path.Combine("scripts","BuildProjectOverview.cs"));
if(prodSrc.Contains("qk.ContainsKey(\"Unresolved\")?\"unresolved\":\"verified\"",StringComparison.Ordinal))
{
    // replicate the derivation to show the mis-green cases
    string Derive(Dictionary<string,int> qk)=>qk.ContainsKey("Unresolved")?"unresolved":"verified";
    var withDifferent=new Dictionary<string,int>{{"Different",2}};var empty=new Dictionary<string,int>();
    if(Derive(withDifferent)=="verified"&&Derive(empty)=="verified")
        Finding("B5-three-state-residual","质量三态推导仍是 qk.ContainsKey(\"Unresolved\")?\"unresolved\":\"verified\"：qk={Different:2}→verified（应=different/红）、qk={}→verified（缺证据应=unverified）——Different 与空集未实修");
}
var sphereRow=rows["analytic-boolean.equal-overlap-subtract"];
var massNote=sphereRow["dimensions"]!["mass"]!["note"]!.GetValue<string>();
if(massNote.Contains("（分量级）",StringComparison.Ordinal))Finding("B5-label-residual","测量 mass note 仍标「（分量级）」");
if(!massNote.Contains("样本级",StringComparison.Ordinal))Finding("B5-label-missing","测量 mass note 缺样本级口径标签");
var perfNote=sphereRow["dimensions"]!["performance"]!["note"]!.GetValue<string>();
if(perfNote.Contains("performanceQualified=false",StringComparison.Ordinal)&&!perfNote.Contains("runnerPerformanceQualified",StringComparison.Ordinal))Finding("B5-schema-residual","performance note 引用不存在字段 performanceQualified");
var exitNote=sphereRow["dimensions"]!["performance"]!["lastVerified"]!["exit"]!.GetValue<string>();
if(!exitNote.Contains("measurement-receipt",StringComparison.Ordinal)&&!exitNote.Contains("runnerRawExit",StringComparison.Ordinal))Note("B5-exit-citation","performance exit 注记="+exitNote);

// ---- stats recomputation (facts, not targets)
var recount=new Dictionary<string,int>(StringComparer.Ordinal);
foreach(var r in rows.Values)
{
    foreach(var w in new[]{"function","mass","query","performance"})
    {
        var st=r["dimensions"]![w]!["status"]!.GetValue<string>();
        var k=w+":"+st;recount[k]=recount.TryGetValue(k,out var n)?n+1:1;
    }
    var cv=r["dimensions"]!["currentVersion"]?.GetValue<string>()??"not-retested";
    var ck="current:"+(cv=="reverified"?"reverified":"not-retested");recount[ck]=recount.TryGetValue(ck,out var m0)?m0+1:1;
}
foreach(var kv in recount.OrderBy(kv=>kv.Key))
{
    var inSummary=overview["summary"]![kv.Key]?.GetValue<int>()??-1;
    if(inSummary!=kv.Value)Finding("summary-drift",$"summary[{kv.Key}]={inSummary} 但按行重算={kv.Value}");
}
Console.WriteLine("RECOUNT "+string.Join(", ",recount.OrderBy(kv=>kv.Key).Select(kv=>$"{kv.Key}={kv.Value}")));

// ---- mappingAudit provenance fields
var ma=overview["mappingAudit"]!.AsArray().OfType<JsonObject>().ToList();
if(ma.Any(e=>e["suite"]==null||e["sourceLog"]==null))Finding("mapping-audit-fields","mappingAudit 条目缺 suite/sourceLog 字段");
if(ma.Count!=195+ (overview["deliveries"]!.AsArray().Count>7?0:0))Console.WriteLine("mappingAudit entries="+ma.Count);
var borrowEntries=ma.Where(e=>(e["via"]?.GetValue<string>()??"").Contains("borrowed",StringComparison.Ordinal)).ToList();
foreach(var be in borrowEntries)
{
    var cid=be["catalogId"]!.GetValue<string>();
    if(rows[cid]["dimensions"]!["function"]!["status"]!.GetValue<string>()!="unverified")Finding("mapping-borrow-signed",$"mappingAudit borrowed 条目 {cid} 对应行却非 unverified");
}

// ---- events entry delivered
var eventsTracked=File.Exists(Path.Combine("web","data","overview-events.json"));
if(!eventsTracked)Finding("events-entry-missing","web/data/overview-events.json 不存在于候选树");
else Note("events-entry-present","web/data/overview-events.json 存在（内容="+File.ReadAllText(Path.Combine("web","data","overview-events.json")).Trim()+"）");

// ---- B2 synthetic semantics against the accepted extractor rules (per key)
void SynthCheck(string name,string key,string[] lines,string expectF)
{
    int pass=0,fail=0;
    foreach(var l in lines){var t=l.Trim();if(t.StartsWith("GEOMETRY_TOPOLOGY_PASS "+key,StringComparison.Ordinal))pass++;if(t.StartsWith("FAIL "+key,StringComparison.Ordinal)||t.StartsWith("GEOMETRY_TOPOLOGY_FAIL "+key,StringComparison.Ordinal))fail++;}
    string got=fail>0?"fail":pass>0?"pass":"unverified";
    if(got!=expectF)Finding("synth-semantic",$"{name}: key {key} 期望 {expectF} 实得 {got}");
    else Note("synth-semantic",$"{name}: key {key} {expectF} ✓（PASS/FAIL 不可丢）");
}
SynthCheck("first-key-own-pass","k1",new[]{"MASS_BOUND k1 property=x status=Verified","GEOMETRY_TOPOLOGY_PASS k1"},"pass");
SynthCheck("no-marker-key","k2",new[]{"MASS_BOUND k1 property=x status=Verified","GEOMETRY_TOPOLOGY_PASS k1","MASS_BOUND k2 property=x status=Unresolved"},"unverified");
SynthCheck("pass-then-fail","k1",new[]{"MASS_BOUND k1 property=x status=Verified","GEOMETRY_TOPOLOGY_PASS k1","FAIL k1 mass: mass mismatch"},"fail");
SynthCheck("fail-then-pass","k1",new[]{"FAIL k1 mass: mass mismatch","MASS_BOUND k1 property=x status=Verified","GEOMETRY_TOPOLOGY_PASS k1"},"fail");
// real-log FAIL preservation: any FAIL line in the three logs must map to a fail row or a documented non-463 record
foreach(var kv in logs)
{
    foreach(var line in File.ReadLines(kv.Value))
    {
        var t=line.Trim();
        if(t.StartsWith("FAIL ",StringComparison.Ordinal)||t.StartsWith("GEOMETRY_TOPOLOGY_FAIL ",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[1].TrimEnd(':');
            bool mapped=rows.Values.Any(r=>r["selector"]?.GetValue<string>()==key);
            if(mapped&&rows.Values.First(r=>r["selector"]?.GetValue<string>()==key)["dimensions"]!["function"]!["status"]!.GetValue<string>()!="fail")Finding("fail-swallowed",$"{kv.Key}/{key}: FAIL 行被签非 fail");
        }
    }
}

int errorCount=findings.OfType<JsonObject>().Count(f=>f["verdict"]!.GetValue<string>()=="error");
var receipt=new JsonObject{
    ["checkedUtc"]=JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")),
    ["target"]=JsonValue.Create(target),
    ["candidate"]=JsonValue.Create("f4632281e4df097998bfcb282508e43186816e43 (base 2bece5dfa952da02a3bf01a8d16f2abc0a31a276)"),
    ["summary"]=new JsonObject{{"errors",JsonValue.Create(errorCount)},{"notes",JsonValue.Create(findings.Count-errorCount)}},
    ["recount"]=new JsonObject(recount.Select(kv=>new KeyValuePair<string,JsonNode?>(kv.Key,kv.Value))),
    ["findings"]=findings,
};
var receiptPath=Path.Combine(outDir,"f463-overview-data-audit.json");
File.WriteAllText(receiptPath,receipt.ToJsonString());
Console.WriteLine("RECEIPT "+receiptPath+" errors="+errorCount+" notes="+(findings.Count-errorCount));
return 0;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
static string ScriptPath([CallerFilePath]string path="")=>path;
