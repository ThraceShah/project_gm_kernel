#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Independent audit of the project-overview candidate data facts (read-only over
// all originals). Re-derives per-scenario evidence from the sealed operation
// logs with a correct phase-aware parser, replicates the candidate builder's
// extractor semantics to pin the mechanism of every disagreement, checks
// selector provenance (cross-suite borrowing), the short-edge grid dual match,
// the named non-grid evidence (transverse/probes/standard argv+exits), and the
// 1654 measurement schema claims. Also demonstrates the extractor's latent
// failure modes on isolated synthetic logs. Writes a derived receipt under
// docs/reviews/web/receipts/. This checker's exit code is an audit exit code
// only; it never restates any original modeling process exit.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
var outDir=Path.Combine("docs","reviews","web","receipts");Directory.CreateDirectory(outDir);
var tmpDir=Path.Combine("temp_docs","independent-overview-audit");Directory.CreateDirectory(tmpDir);
var findings=new JsonArray();int Find(string kind,string detail){findings.Add((JsonNode?)JsonValue.Create(kind+" :: "+detail));Console.WriteLine("FINDING "+kind+" :: "+detail);return 0;}

// ---- inputs (all read-only originals)
var registry=JsonNode.Parse(File.ReadAllText(Path.Combine("scripts","web-progress-registry.json")))!.AsObject();
var overview=JsonNode.Parse(File.ReadAllText(Path.Combine("web","data","project-overview.json")))!.AsObject();
var rows=overview["catalog"]!.AsArray().OfType<JsonObject>().ToDictionary(c=>c["id"]!.GetValue<string>(),StringComparer.Ordinal);
var uniLog=Path.GetFullPath(Path.Combine("..","gm-native-unified-delivery","temp_docs","unified-delivery"),root);
var blendLog=Path.GetFullPath(Path.Combine("..","gm-blend-functional-delivery","temp_docs","blend-functional","final-integrated"),root);
var pkLog=Path.GetFullPath(Path.Combine("..","gm-pk-native-delivery","temp_docs","pk-native-delivery"),root);
var logs=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["regular45"]=Path.Combine(uniLog,"regular45-final.log"),
    ["mixed18"]=Path.Combine(uniLog,"mixed18-final.log"),
    ["complete"]=Path.Combine(blendLog,"complete-catalog.log"),
};
foreach(var kv in logs)if(!File.Exists(kv.Value))throw new FileNotFoundException(kv.Value);
// the oracle source that produced each log (from the log preambles/producer tree)
var logSource=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["regular45"]="scripts/RegularCurvedBooleanOracle.cs",
    ["mixed18"]="scripts/MixedAnalyticBooleanOracle.cs",
    ["complete"]="scripts/LocalBlendCatalog",
};

// ---- correct phase-aware parse: one record per (log,key), mass kept per phase
var phaseFindings=new JsonArray();
Dictionary<string,(List<(int v,int u,int d,string? agree,int line,bool pass,bool fail)> phases,int passLines,int failLines)> CorrectParse(string path)
{
    var res=new Dictionary<string,(List<(int,int,int,string?,int,bool,bool)>,int,int)>(StringComparer.Ordinal);
    void Ensure(string k,int ln){if(!res.ContainsKey(k))res[k]=(new(),0,0);}
    int ln=0;
    foreach(var raw in File.ReadLines(path))
    {
        ln++;
        var t=raw.Trim();
        if(t.StartsWith("MASS_BOUND ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k,0);
            var e=res[k];int v=e.Item1.Count>0?e.Item1[^1].Item1:0,u=e.Item1.Count>0?e.Item1[^1].Item2:0,d=e.Item1.Count>0?e.Item1[^1].Item3:0;
            bool closed=e.Item1.Count>0&&(e.Item1[^1].Item4!=null||e.Item1[^1].Item6);
            if(e.Item1.Count==0||closed)e.Item1.Add((0,0,0,null,ln,false,false));
            var last=e.Item1[^1];
            if(t.EndsWith("status=Verified",StringComparison.Ordinal))last=(last.Item1+1,last.Item2,last.Item3,last.Item4,last.Item5,false,false);
            else if(t.EndsWith("status=Unresolved",StringComparison.Ordinal))last=(last.Item1,last.Item2+1,last.Item3,last.Item4,last.Item5,false,false);
            else if(t.EndsWith("status=Different",StringComparison.Ordinal))last=(last.Item1,last.Item2,last.Item3+1,last.Item4,last.Item5,false,false);
            e.Item1[^1]=last;
        }
        else if(t.StartsWith("MASS_AGREEMENT ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k,0);
            var e=res[k];bool closed=e.Item1.Count>0&&(e.Item1[^1].Item4!=null||e.Item1[^1].Item6);
            if(e.Item1.Count==0||closed)e.Item1.Add((0,0,0,null,ln,false,false));
            var st=t.Contains("status=Verified",StringComparison.Ordinal)?"Verified":t.Contains("status=Different",StringComparison.Ordinal)?"Different":t.Contains("status=Unresolved",StringComparison.Ordinal)?"Unresolved":"?";
            var last=e.Item1[^1];e.Item1[^1]=(last.Item1,last.Item2,last.Item3,st,last.Item5,false,false);
        }
        else if(t.StartsWith("GEOMETRY_TOPOLOGY_PASS ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k,0);
            var e=res[k];res[k]=(e.Item1,e.Item2+1,e.Item3);
            if(e.Item1.Count==0)e.Item1.Add((0,0,0,null,ln,false,false));
            var last=e.Item1[^1];e.Item1[^1]=(last.Item1,last.Item2,last.Item3,last.Item4,last.Item5,true,false);
        }
        else if(t.StartsWith("GEOMETRY_TOPOLOGY_FAIL ",StringComparison.Ordinal)||t.StartsWith("FAIL ",StringComparison.Ordinal))
        {
            var k=t.Split(' ')[1].TrimEnd(':');Ensure(k,0);
            var e=res[k];res[k]=(e.Item1,e.Item2,e.Item3+1);
            if(e.Item1.Count==0)e.Item1.Add((0,0,0,null,ln,false,false));
            var last=e.Item1[^1];e.Item1[^1]=(last.Item1,last.Item2,last.Item3,last.Item4,last.Item5,false,true);
        }
    }
    return res;
}

// ---- candidate builder's extractor semantics, replicated exactly
Dictionary<string,(bool pass,bool fail,int v,int u,int d)> BuilderParse(string path)
{
    var scenarioAudit=new Dictionary<string,(bool,bool,int,int,int)>(StringComparer.Ordinal);
    string? current=null;bool pass=false,fail=false;int v=0,u=0,d=0;
    void Flush(){if(current!=null&&!scenarioAudit.ContainsKey(current))scenarioAudit[current]=(pass,fail,v,u,d);}
    foreach(var line in File.ReadLines(path))
    {
        var t=line.Trim();
        if(t.StartsWith("GEOMETRY_TOPOLOGY_PASS ",StringComparison.Ordinal)){Flush();current=t.Split(' ')[1].TrimEnd(':');pass=true;fail=false;v=0;u=0;d=0;continue;}
        if(t.StartsWith("GEOMETRY_TOPOLOGY_FAIL ",StringComparison.Ordinal)||t.StartsWith("FAIL ",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[1].TrimEnd(':');
            if(current!=key){Flush();current=key;}
            fail=true;continue;
        }
        if(t.StartsWith("MASS_BOUND ",StringComparison.Ordinal))
        {
            var key=t.Split(' ')[1].TrimEnd(':');
            if(current!=key){Flush();current=key;}
            if(t.EndsWith("status=Verified",StringComparison.Ordinal))v++;
            else if(t.EndsWith("status=Unresolved",StringComparison.Ordinal))u++;
            else if(t.EndsWith("status=Different",StringComparison.Ordinal))d++;
            continue;
        }
    }
    Flush();
    return scenarioAudit;
}

var correct=new Dictionary<string,Dictionary<string,(List<(int v,int u,int d,string? agree,int line,bool pass,bool fail)> phases,int passLines,int failLines)>>(StringComparer.Ordinal);
var builder=new Dictionary<string,Dictionary<string,(bool pass,bool fail,int v,int u,int d)>>(StringComparer.Ordinal);
foreach(var kv in logs){correct[kv.Key]=CorrectParse(kv.Value);builder[kv.Key]=BuilderParse(kv.Value);}

// ---- 1) per-row evidence re-derivation (own-suite only) vs committed overview
int borrowed=0,underCredited=0,overCredited=0,phaseMerged=0;
var rowAudit=new JsonArray();
foreach(var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
{
    var id=entry["id"]!.GetValue<string>();var suite=entry["suite"]!.GetValue<string>();
    var selector=entry["selector"]?.GetValue<string>();
    if(selector==null)continue;
    string? ownLog=suite=="local-blend"?"complete":suite=="mixed-analytic"?"mixed18":suite=="regular-curved"?"regular45":null;
    var appears=logs.Keys.Where(l=>correct[l].ContainsKey(selector)).ToList();
    if(appears.Count==0)continue;
    var row=rows[id];
    var f=row["dimensions"]!["function"]!["status"]!.GetValue<string>();
    var m=row["dimensions"]!["mass"]!["status"]!.GetValue<string>();
    string expectF;string expectM;string evidenceLog=appears[0];
    var rec=correct[evidenceLog][selector];
    bool ownEvidence=ownLog!=null&&appears.Contains(ownLog);
    if(ownEvidence)
    {
        var or=correct[ownLog!][selector];
        expectF=or.failLines>0?"fail":or.passLines>0?"pass":"unverified";
        int v=0,u=0,d=0;foreach(var ph in or.phases){v+=ph.v;u+=ph.u;d+=ph.d;}
        expectM=d>0?"different":u>0?"unresolved":v>0?"verified":"unverified";
        // phase merging: combined state must equal the state of the phase that
        // carries the key's own pass marker (the definitive stage), else flag.
        var defPhase=or.phases.LastOrDefault(p=>p.pass);
        if(or.phases.Count>1&&defPhase.agree!=null)
        {
            string defState=defPhase.agree;
            string comb=d>0?"Different":u>0?"Unresolved":v>0?"Verified":"?";
            if(!string.Equals(comb,defState,StringComparison.OrdinalIgnoreCase))
            {
                phaseMerged++;
                Find("mass-phase-merged",$"{id}: 合并 {or.phases.Count} 相态分量({v}V/{u}U/{d}D)得 {comb}，而含自身 PASS 的最终相态 MASS_AGREEMENT={defPhase.agree}（行 {defPhase.line}）");
            }
        }
    }
    else
    {
        expectF="unverified";expectM="unverified";
        borrowed++;
        Find("cross-suite-borrow",$"{id}: suite={suite}，selector 在 {string.Join("/",appears)} 日志出现但非本套件（registry source={entry["source"]!.GetValue<string>()}），看板签 function={f}/mass={m}，证据列={row["dimensions"]!["function"]!["lastVerified"]!["evidence"]!.GetValue<string>()}");
    }
    if(f!=expectF)
    {
        if(expectF=="pass"&&f=="unverified"){underCredited++;Find("under-credited-pass",$"{id}: 日志 {evidenceLog} 有自身 GEOMETRY_TOPOLOGY_PASS（{string.Join(",",rec.passLines)} 行）但看板=unverified（builder 首键 flush 语义丢失）");}
        else if(expectF=="unverified"&&f=="pass"){overCredited++;Find("over-credited-pass",$"{id}: 本套件无证据但看板=pass");}
        else Find("function-status-mismatch",$"{id}: overview={f} re-derived={expectF}");
    }
    if(m!=expectM&&!ownEvidence)Find("mass-borrowed",$"{id}: mass={m} 来自他套件日志 {evidenceLog}");
    else if(m!=expectM&&ownEvidence)Find("mass-status-mismatch",$"{id}: overview mass={m} re-derived(merged)={expectM}");
    rowAudit.Add((JsonNode)new JsonObject{{"id",JsonValue.Create(id)},{"suite",JsonValue.Create(suite)},{"logs",JsonValue.Create(string.Join("/",appears))},{"ownEvidence",JsonValue.Create(ownEvidence)},{"overviewFunction",JsonValue.Create(f)},{"derivedFunction",JsonValue.Create(expectF)},{"overviewMass",JsonValue.Create(m)},{"derivedMass",JsonValue.Create(expectM)}});
}

// ---- 2) builder-replica mechanism proof on real logs (first-key loss + stale inheritance)
foreach(var l in logs.Keys)
{
    var b=builder[l];var c=correct[l];
    foreach(var key in c.Keys)
    {
        var bb=b[key];var cc=c[key];
        bool realPass=cc.passLines>0;
        if(realPass&&!bb.pass)Find("builder-flush-loss",$"{l}/{key}: 真实 PASS 行 {string.Join(",",cc.passLines)}，builder 记录 pass=false（首键 flush 于自身标记之前，后续完整记录被 ContainsKey 丢弃）");
        if(!realPass&&bb.pass)Find("builder-stale-green",$"{l}/{key}: 无自身 PASS 标记，builder 记录 pass=true（前一键 PASS 的 stale 状态被继承）");
    }
}

// ---- 3) synthetic extractor failure modes (isolated derived logs)
void Synth(string name,Action<List<string>> build)
{
    var lines=new List<string>();build(lines);
    var p=Path.Combine(tmpDir,name+".log");File.WriteAllLines(p,lines);
    var b=BuilderParse(p);var c=CorrectParse(p);
    File.WriteAllText(Path.Combine(tmpDir,name+".result.json"),new JsonObject{
        ["builder"]=new JsonObject(b.Select(kv=>new KeyValuePair<string,JsonNode?>(kv.Key,new JsonObject{{"pass",kv.Value.pass},{"fail",kv.Value.fail},{"v",kv.Value.v},{"u",kv.Value.u},{"d",kv.Value.d}}))),
        ["correct"]=new JsonObject(c.Select(kv=>new KeyValuePair<string,JsonNode?>(kv.Key,new JsonObject{{"passLines",kv.Value.passLines},{"failLines",kv.Value.failLines},{"phases",kv.Value.phases.Count}}))),
    }.ToJsonString());
    Console.WriteLine("SYNTH "+name+" -> "+File.ReadAllText(Path.Combine(tmpDir,name+".result.json")));
}
Synth("first-key-pass-lost",l=>{l.Add("MASS_BOUND k1 property=x status=Verified");l.Add("GEOMETRY_TOPOLOGY_PASS k1");});
Synth("stale-pass-inheritance",l=>{l.Add("MASS_BOUND k1 property=x status=Verified");l.Add("GEOMETRY_TOPOLOGY_PASS k1");l.Add("MASS_BOUND k2 property=x status=Unresolved");});
Synth("fail-after-green-dropped",l=>{l.Add("MASS_BOUND k1 property=x status=Verified");l.Add("GEOMETRY_TOPOLOGY_PASS k1");l.Add("MASS_BOUND k2 property=x status=Verified");l.Add("GEOMETRY_TOPOLOGY_PASS k2");l.Add("FAIL k2 mass: mass mismatch");});

// ---- 4) named non-grid evidence facts (transverse/probes/standard)
string Read(string rel)=>File.ReadAllText(Path.Combine(pkLog,rel));
var transverseCase=Read("transverse.log").Split('\n').FirstOrDefault(l=>l.StartsWith("CASE ",StringComparison.Ordinal))?.Trim() ?? "";
var transverseFails=Read("transverse.log").Split('\n').Where(l=>l.Trim().StartsWith("FAIL ",StringComparison.Ordinal)||l.Trim().StartsWith("GEOMETRY_TOPOLOGY_FAIL ",StringComparison.Ordinal)).Select(l=>l.Trim()).ToList();
var transverseExit=File.ReadAllText(Path.Combine(pkLog,"transverse.exit")).Trim();
var standardCase=Read("standard.log").Split('\n').FirstOrDefault(l=>l.StartsWith("CASE ",StringComparison.Ordinal))?.Trim() ?? "";
var standardExit=File.ReadAllText(Path.Combine(pkLog,"standard.exit")).Trim();
var probesSelfFails=Read("probes-self.log").Split('\n').Where(l=>l.Trim().StartsWith("FAIL ",StringComparison.Ordinal)).Select(l=>l.Trim()).ToList();
var probesImportFails=Read("probes-import.log").Split('\n').Where(l=>l.Trim().StartsWith("FAIL ",StringComparison.Ordinal)).Select(l=>l.Trim()).ToList();
var probesSelfExit=File.ReadAllText(Path.Combine(pkLog,"probes-self.exit")).Trim();
var probesImportExit=File.ReadAllText(Path.Combine(pkLog,"probes-import.exit")).Trim();
Console.WriteLine("NAMED transverse "+transverseCase+" exit="+transverseExit+" fails="+transverseFails.Count);
foreach(var f in transverseFails)Console.WriteLine("  "+f);
Console.WriteLine("NAMED standard "+standardCase+" exit="+standardExit);
// rows: exactly one executed transverse scenario (CASE embeds the flags); the
// builder maps two catalog rows onto it.
var strictRow=rows["short-edge.transverse-strict-lifecycle"];var witnessRow=rows["short-edge.transverse-witness-lifecycle"];
if(!transverseCase.Contains("reload-before-cut",StringComparison.Ordinal))Find("named-case-flags","transverse.log CASE 不含 reload-before-cut，与 registry witness 行 flags 不符，需人工核对");
if(transverseCase.Contains("reload-before-cut",StringComparison.Ordinal)&&!transverseCase.Contains("transverse-local-cut-reload",StringComparison.Ordinal))Find("named-case-flags","transverse.log CASE 标记异常");
// both FAIL lines are mass gates, none geometry/topology
if(transverseFails.Any(f=>!f.Contains("mass",StringComparison.OrdinalIgnoreCase)))Find("named-fail-dimension","transverse.log 出现非 mass 的 FAIL 行");
if(transverseFails.Count!=2)Find("named-fail-count","transverse.log FAIL 行数="+transverseFails.Count+"（预期 2，来自 inertia-handoff 首/末两次 cut）");
if(strictRow["dimensions"]!["function"]!["status"]!.GetValue<string>()=="fail")Find("named-strict-no-evidence","short-edge.transverse-strict-lifecycle 被签 function=fail，但 f9 目录唯一 transverse 运行 argv= --require-native-cut-witness --require-cutter-chart-witness --reload-before-local-cut --transverse-local-cut（inertia-handoff.txt 记录的复现命令），即 witness 场景；strict 场景（无 reload/witness flags）无执行日志");
if(witnessRow["dimensions"]!["function"]!["status"]!.GetValue<string>()=="fail")Find("named-witness-dimension","short-edge.transverse-witness-lifecycle 被签 function=fail，但 transverse.log 两条 FAIL 均为 `mass: mass mismatch`（inertia.yz 点值门），几何/拓扑/API/有限边/材料无 FAIL 行；按功能/物性分维应 function=pass(执行范围)+mass=红(点值门)");
Console.WriteLine($"NAMED exits standard={standardExit} transverse={transverseExit} probes-self={probesSelfExit} probes-import={probesImportExit}");

// ---- 5) short-edge grid dual match re-verification (id-suffix + parameter tuple)
var matrixNames=File.ReadAllLines(Path.Combine(pkLog,"matrix-96-cases.txt")).Where(l=>l.Trim().Length>0).Select(l=>l.Trim()).ToArray();
var shortEdge=registry["cases"]!.AsArray().OfType<JsonObject>().Where(c=>c["suite"]!.GetValue<string>()=="short-edge").ToArray();
var gridOk=0;var gridBad=new List<string>();
foreach(var n in matrixNames)
{
    var id="short-edge."+n.ToLowerInvariant();
    var item=shortEdge.FirstOrDefault(c=>c["id"]!.GetValue<string>()==id);
    if(item==null){gridBad.Add(n);continue;}
    var p=item["parameters"]!.AsObject();
    var m=System.Text.RegularExpressions.Regex.Match(n,@"^a([0-9.]+)-r([0-9.]+)-t([0-9.eE+-]+)-reverse(True|False)-(.+)$");
    bool tupleOk=m.Success
        &&double.Parse(m.Groups[1].Value)==p["angle"]!.GetValue<double>()
        &&double.Parse(m.Groups[2].Value)==p["radius"]!.GetValue<double>()
        &&double.Parse(m.Groups[3].Value)==p["tolerance"]!.GetValue<double>()
        &&(m.Groups[4].Value=="True")==p["reverseSelection"]!.GetValue<bool>()
        &&string.Equals(m.Groups[5].Value,p["update"]!.GetValue<string>(),StringComparison.OrdinalIgnoreCase);
    if(tupleOk)gridOk++;else gridBad.Add(n+"(tuple)");
}
Console.WriteLine($"GRID dual-match ok={gridOk}/96 bad={gridBad.Count}");
if(gridBad.Count>0)Find("grid-mismatch",string.Join(",",gridBad));

// ---- 6) measurement schema claims vs sealed operation-summary.json
var opSummaryPath=Path.Combine("temp_docs","incoming","unified-performance-1654","extracted-native224","docs","unified-native-controlled","operation-summary.json");
var opSummary=JsonNode.Parse(File.ReadAllText(opSummaryPath))!.AsObject();
var sampleKeys=opSummary["allSamples"]!.AsArray().OfType<JsonObject>().First().Select(kv=>kv.Key).ToHashSet(StringComparer.Ordinal);
var opKeys=opSummary["operations"]!.AsArray().OfType<JsonObject>().First().Select(kv=>kv.Key).ToHashSet(StringComparer.Ordinal);
Console.WriteLine("MEASURE op fields: "+string.Join(",",opKeys));
Console.WriteLine("MEASURE sample fields: "+string.Join(",",sampleKeys));
if(!opKeys.Contains("rawExits"))Find("measurement-schema","operation-summary.json operations[] 无 rawExits 字段，builder 的 procExits=mop[\"rawExits\"] 恒为 null；逐进程 exit 事实只存在于 measurement-receipt.json（qualityStates exit2-Unresolved=16/exit0-Verified=4、runnerRawExit=2）");
if(!opKeys.Contains("performanceQualified"))Find("measurement-schema","operation-summary.json 无 performanceQualified 字段；\"performanceQualified=false\" 应引 measurement-receipt.json 的 runnerPerformanceQualified=false");
foreach(var op in opSummary["operations"]!.AsArray().OfType<JsonObject>())
{
    var qk=op["candidate"]!["qualityStatusCounts"]!.AsObject();
    int n=qk.Sum(kv=>kv.Value!.GetValue<int>());
    if(n!=2)Find("measurement-qk","qualityStatusCounts 计数异常 "+op["case"]!.GetValue<string>());
}
Find("measurement-label","builder 将 qualityStatusCounts（每 op 候选侧 2 个 rep 样本的状态计数）标注为「（分量级）」，与验证记录的 900/1080 分量总账不同口径，须改标签或引真实分量总账");
// planar/sample exits from allSamples
var exitTally=opSummary["allSamples"]!.AsArray().OfType<JsonObject>().GroupBy(s=>(s["rawExit"]!.GetValue<int>(),s["qualityStatus"]!.GetValue<string>())).ToDictionary(g=>g.Key,g=>g.Count());
Console.WriteLine("MEASURE sample exit/status tally: "+string.Join(", ",exitTally.Select(kv=>$"exit{kv.Key.Item1}/{kv.Key.Item2}={kv.Value}")));

// ---- 7) summary recomputation from committed rows
int Catalog()=>rows.Count;
Console.WriteLine("SUMMARY catalog="+Catalog()+" function:pass="+rows.Values.Count(r=>r["dimensions"]!["function"]!["status"]!.GetValue<string>()=="pass")
    +" function:fail="+rows.Values.Count(r=>r["dimensions"]!["function"]!["status"]!.GetValue<string>()=="fail")
    +" function:unverified="+rows.Values.Count(r=>r["dimensions"]!["function"]!["status"]!.GetValue<string>()=="unverified"));
var rowsWithoutEvidenceGreen=rows.Values.Where(r=>new[]{"function","mass","query","performance"}.Any(w=>r["dimensions"]![w]!["status"]!.GetValue<string>()=="pass"&&r["dimensions"]![w]!["lastVerified"]!["evidence"]!.GetValue<string>().Length==0)).Select(r=>r["id"]!.GetValue<string>()).ToList();
Console.WriteLine("GREEN-WITHOUT-EVIDENCE rows="+rowsWithoutEvidenceGreen.Count);

var receipt=new JsonObject{
    ["checkedUtc"]=JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")),
    ["candidate"]=JsonValue.Create("2bece5dfa952da02a3bf01a8d16f2abc0a31a276 (base 5222608e2d074643a1d516c7231ea84cf0933cef)"),
    ["counts"]=new JsonObject{{"borrowedRows",JsonValue.Create(borrowed)},{"underCreditedPass",JsonValue.Create(underCredited)},{"overCreditedPass",JsonValue.Create(overCredited)},{"phaseMergedRows",JsonValue.Create(phaseMerged)},{"gridDualOk",JsonValue.Create(gridOk)},{"findingsTotal",JsonValue.Create(findings.Count)}},
    ["namedFacts"]=new JsonObject{
        ["transverseCaseLine"]=JsonValue.Create(transverseCase),
        ["transverseExitFile"]=JsonValue.Create(transverseExit),
        ["transverseFailLines"]=new JsonArray(transverseFails.Select(f=>(JsonNode?)JsonValue.Create(f)).ToArray()),
        ["standardCaseLine"]=JsonValue.Create(standardCase),
        ["standardExitFile"]=JsonValue.Create(standardExit),
        ["probesSelfFailLines"]=new JsonArray(probesSelfFails.Select(f=>(JsonNode?)JsonValue.Create(f)).ToArray()),
        ["probesSelfExitFile"]=JsonValue.Create(probesSelfExit),
        ["probesImportFailLines"]=new JsonArray(probesImportFails.Select(f=>(JsonNode?)JsonValue.Create(f)).ToArray()),
        ["probesImportExitFile"]=JsonValue.Create(probesImportExit),
    },
    ["rowAudit"]=rowAudit,
    ["findings"]=findings,
};
var receiptPath=Path.Combine(outDir,"overview-data-audit.json");
File.WriteAllText(receiptPath,receipt.ToJsonString());
Console.WriteLine("RECEIPT "+receiptPath+" findings="+findings.Count);
return 0;
static string ScriptPath([CallerFilePath]string path="")=>path;
