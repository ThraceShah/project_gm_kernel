#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Narrow re-verification of the f463 event-update contract (isolated derived
// events; producer run from the --target tree; outputs only under
// temp_docs/f463-reverify/events of THIS script's tree). Required behaviors:
// row and summary move together; catalog fixed; a real new failure is never
// overwritten by an old green; wrong-parameter/wrong-platform events cannot
// cross-sign; unknown/empty host cannot bypass; never-verified rows can still
// receive their own platform's events; same-utc different-dimension events are
// not dropped; events cannot overwrite newer base facts; dimension/status
// pairing enforced; missing evidence cannot be green; bad events rejected and
// recorded; currentVersion not an arbitrary string. Each finding is classified
// error (requirement not met) or note (documented limitation).
var scripts=Path.GetDirectoryName(ScriptPath())!;
var reviewRoot=Path.GetFullPath("..",scripts);
var target=Path.GetFullPath(Option("--target",reviewRoot));Directory.SetCurrentDirectory(target);
var producer=Path.Combine(target,"scripts","BuildProjectOverview.cs");
var tmpDir=Path.Combine(reviewRoot,"temp_docs","f463-reverify","events");Directory.CreateDirectory(tmpDir);
var outDir=Path.Combine(reviewRoot,"docs","reviews","web","receipts");Directory.CreateDirectory(outDir);
var tests=new JsonArray();var findings=new JsonArray();
void Finding(string s){findings.Add((JsonNode)new JsonObject{{"verdict","error"},{"detail",s}});Console.WriteLine("ERROR "+s);}
void Note(string s){findings.Add((JsonNode)new JsonObject{{"verdict","note"},{"detail",s}});Console.WriteLine("NOTE  "+s);}

(string stdout,int exit) RunProducer(string? eventsPath,string outputPath,bool verify=false)
{
    var psi=new ProcessStartInfo("dotnet"){WorkingDirectory=target};
    psi.Environment["MSBUILDDISABLENODEREUSE"]="1";
    psi.ArgumentList.Add("run");psi.ArgumentList.Add(producer);
    if(eventsPath!=null){psi.ArgumentList.Add("--events");psi.ArgumentList.Add(eventsPath);}
    psi.ArgumentList.Add("--output");psi.ArgumentList.Add(outputPath);
    if(verify)psi.ArgumentList.Add("--verify");
    psi.RedirectStandardOutput=true;psi.RedirectStandardError=true;
    using var p=Process.Start(psi)!;var stdout=p.StandardOutput.ReadToEnd();p.WaitForExit();
    return (stdout,p.ExitCode);
}
string WriteEvents(string name,JsonArray events){var p=Path.Combine(tmpDir,name+".events.json");File.WriteAllText(p,events.ToJsonString());return p;}
JsonObject CaseUpdate(string catalogId,string dimension,string status,string evidence,string utc,string note,string? host=null,string? commit=null,string? currentVersion=null)
{
    var o=new JsonObject{{"kind","case-update"},{"catalogId",catalogId},{"dimension",dimension},{"status",status},
        {"lastVerified",new JsonObject{{"commit",commit??"audit"},{"tree","audit"},{"host",host??"audit-host"},{"exit","audit"},{"evidence",JsonValue.Create(evidence)}}},
        {"note",JsonValue.Create(note)},{"utc",JsonValue.Create(utc)}};
    if(currentVersion!=null)o["currentVersion"]=JsonValue.Create(currentVersion);
    return o;
}
void Test(string name,bool ok,string detail)
{
    tests.Add((JsonNode)new JsonObject{{"name",JsonValue.Create(name)},{"ok",JsonValue.Create(ok)},{"detail",JsonValue.Create(detail)}});
    Console.WriteLine((ok?"PASS ":"FAIL ")+name+" :: "+detail);
    if(!ok)Finding(name+" :: "+detail);
}
JsonArray Catalog(string outP)=>JsonNode.Parse(File.ReadAllText(outP))!.AsObject()["catalog"]!.AsArray();
JsonObject Row(string outP,string id)=>Catalog(outP).OfType<JsonObject>().First(c=>c["id"]!.GetValue<string>()==id);

var committed=JsonNode.Parse(File.ReadAllText(Path.Combine("web","data","project-overview.json")))!.AsObject();
string Norm(JsonObject o){var c=(JsonObject)o.DeepClone();c["generatedAt"]="X";return c.ToJsonString();}

// T0 baseline regeneration matches committed f463 overview
{
    var outP=Path.Combine(tmpDir,"out-baseline.json");
    var r=RunProducer(null,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    bool same=string.Equals(Norm(committed),Norm(fresh),StringComparison.Ordinal);
    Test("T0 baseline regeneration matches committed f463 overview",same,$"exit={r.exit}");
}

// T1 row and summary counters move together (post-event recount)
{
    var targetId="regular-curved.block-unite"; // f463: now pass via own marker; base host q2 Linux
    var outP=Path.Combine(tmpDir,"out-t1.json");
    var ev=WriteEvents("t1",new JsonArray{CaseUpdate(targetId,"function","fail","docs/audit-evidence.md","2026-11-12T10:00:00Z","isolated: flip to fail","q2 Linux x64")});
    var r=RunProducer(ev,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    int failRows=fresh["catalog"]!.AsArray().Count(c=>c["dimensions"]!["function"]!["status"]!.GetValue<string>()=="fail");
    int summaryFail=fresh["summary"]!["function:fail"]!.GetValue<int>();
    Test("T1 event row change and summary counters move together",failRows==summaryFail&&failRows==1,$"row flipped to fail; recomputed failRows={failRows} summary.function:fail={summaryFail}");
}

// T2 catalog fixed at 463 with delivery + unknown-id events
{
    var outP=Path.Combine(tmpDir,"out-t2.json");
    var ev=WriteEvents("t2",new JsonArray{CaseUpdate("no-such-case","function","pass","x","2026-11-12T10:00:00Z","isolated: unknown id"),new JsonObject{{"kind","delivery"},{"runId","audit"},{"label","audit"},{"commit","audit"},{"utc","2026-11-12T10:00:00Z"},{"note","isolated delivery event"}}});
    var r=RunProducer(ev,outP);
    Test("T2 catalog fixed at 463",Catalog(outP).Count==463,$"catalog={Catalog(outP).Count} exit={r.exit}");
}

// T3 real new failure not overwritten by a later-appended old green
{
    var targetId="local-blend.block-all-fillet";
    var outP=Path.Combine(tmpDir,"out-t3.json");
    var ev=WriteEvents("t3",new JsonArray{
        CaseUpdate(targetId,"function","fail","temp_docs/new-failure.log","2026-11-12T11:00:00Z","isolated: real NEW failure (later utc)","q2 Linux x64"),
        CaseUpdate(targetId,"function","pass","temp_docs/old-green.log","2026-11-10T09:00:00Z","isolated: OLD green (older utc, appended later)","q2 Linux x64"),
    });
    var r=RunProducer(ev,outP);
    var st=Row(outP,targetId)["dimensions"]!["function"]!["status"]!.GetValue<string>();
    Test("T3 later-appended old green cannot overwrite a real new failure",st=="fail",$"row.function={st} (file order: new-fail then old-green)");
}

// T4a cross-platform family cannot cross-sign
{
    var targetId="planar-boolean.overlap-two-axes-subtract"; // base host = Mac M4
    var outP=Path.Combine(tmpDir,"out-t4a.json");
    var ev=WriteEvents("t4a",new JsonArray{CaseUpdate(targetId,"mass","verified","temp_docs/win.log","2026-11-12T12:00:00Z","isolated: Windows claim",host:"Windows arm64")});
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!["mass"]!;
    var host=row["lastVerified"]!["host"]!.GetValue<string>();
    Test("T4a wrong-platform (Windows) event cannot change a Mac row",host.Contains("Mac",StringComparison.Ordinal)&&row["status"]!.GetValue<string>()=="verified",$"row.mass={row["status"]} host={host} (row was already verified; host must stay Mac)");
}

// T4b unknown/empty host must not bypass the platform check
{
    var targetId="local-blend.block-single-chamfer"; // base host = q2 Linux x64
    var outP=Path.Combine(tmpDir,"out-t4b.json");
    var ev=WriteEvents("t4b",new JsonArray{
        CaseUpdate(targetId,"mass","unresolved","temp_docs/unknown-host.log","2026-11-12T12:10:00Z","isolated: empty host claim",host:""),
        CaseUpdate(targetId,"function","fail","temp_docs/unknown-host2.log","2026-11-12T12:11:00Z","isolated: unknown host claim",host:"mystery-box"),
    });
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!;
    bool massChanged=row["mass"]!["status"]!.GetValue<string>()!="verified";
    bool funcChanged=row["function"]!["status"]!.GetValue<string>()=="fail";
    Test("T4b unknown/empty host events cannot silently sign (expect FAIL: host check bypassed)",!massChanged&&!funcChanged,$"mass→{row["mass"]!["status"]} function→{row["function"]!["status"]} (empty/unknown host evMac/evWin/evLin all false → passes guard)");
}

// T4c never-verified rows must still accept their own platform's events
{
    var targetId="nested-evaluation.depth3-sequential-D0"; // base host = '-' (never verified)
    var outP=Path.Combine(tmpDir,"out-t4c.json");
    var ev=WriteEvents("t4c",new JsonArray{CaseUpdate(targetId,"function","pass","temp_docs/linux-evidence.log","2026-11-12T12:20:00Z","isolated: legit Linux evidence",host:"q2 Linux x64")});
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!["function"]!;
    Test("T4c never-verified row accepts its own platform family's event (expect FAIL: platform lockout)",row["status"]!.GetValue<string>()=="pass",$"row.function={row["status"]} (base host='-' → rowLinux=false → Linux event rejected; unverified rows can never be updated from Mac/Linux)");
}

// T4d wrong-parameter/precision event must not cross-sign (identity binding)
{
    var targetId="local-blend.cylinder-rim-fillet";
    var outP=Path.Combine(tmpDir,"out-t4d.json");
    var ev=WriteEvents("t4d",new JsonArray{CaseUpdate(targetId,"function","fail","temp_docs/other-params.log","2026-11-12T12:30:00Z","isolated: totally different parameters (radius 9, tolerance 1e-3), same platform",host:"q2 Linux x64")});
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!["function"]!;
    Test("T4d wrong-parameter event cannot change the row (expect FAIL: no params/precision binding)",row["status"]!.GetValue<string>()!="fail",$"row.function={row["status"]} (event schema has no parameter/precision/source identity; host substring is not a binding)");
}

// T4e same-utc different-dimension events must not be dropped
{
    var targetId="local-blend.prism-all-fillet";
    var outP=Path.Combine(tmpDir,"out-t4e.json");
    var ev=WriteEvents("t4e",new JsonArray{
        CaseUpdate(targetId,"function","fail","temp_docs/ev-f.log","2026-11-12T12:40:00Z","isolated: function claim",host:"q2 Linux x64"),
        CaseUpdate(targetId,"mass","unresolved","temp_docs/ev-m.log","2026-11-12T12:40:00Z","isolated: mass claim, SAME utc",host:"q2 Linux x64"),
    });
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!;
    bool bothApplied=row["function"]!["status"]!.GetValue<string>()=="fail"&&row["mass"]!["status"]!.GetValue<string>()=="unresolved";
    Test("T4e same-utc different-dimension events both apply (expect FAIL: lastAppliedUtc is per catalogId)",bothApplied,$"function={row["function"]!["status"]} mass={row["mass"]!["status"]} (second event at equal utc rejected as 'stale')");
}

// T4f events older than the row's own base evidence must not overwrite newer facts
{
    var targetId="short-edge.a1-r0.5-t1e-07-reversefalse-default"; // f9 grid row, base evidence history 2026-11-11
    var outP=Path.Combine(tmpDir,"out-t4f.json");
    var ev=WriteEvents("t4f",new JsonArray{CaseUpdate(targetId,"function","fail","temp_docs/ancient.log","2026-11-01T00:00:00Z","isolated: claim dated BEFORE the 1654 window evidence, same platform family",host:"Mac M4 / macOS 27.2")});
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!["function"]!;
    Test("T4f event older than base evidence cannot overwrite newer facts (expect FAIL: no base-time comparison)",row["status"]!.GetValue<string>()!="fail",$"row.function={row["status"]} (the cell's 1654 window evidence is dated 2026-11-12; the guard compares events only to each other, never to base fact time)");
}

// T4g dimension/status pairing enforced
{
    var targetId="analytic-boolean.equal-overlap-subtract";
    var outP=Path.Combine(tmpDir,"out-t4g.json");
    var ev=WriteEvents("t4g",new JsonArray{CaseUpdate(targetId,"function","observed","temp_docs/bogus2.md","2026-11-12T12:50:00Z","isolated: 'observed' is a performance-only status",host:"Mac M4 / macOS 27.2")});
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!["function"]!;
    Test("T4g dimension/status pairing enforced (expect FAIL: global status pool)",row["status"]!.GetValue<string>()!="observed",$"function={row["status"]} (knownStatuses is a global pool; function+observed accepted)");
}

// T5 empty-evidence green rejected
{
    var targetId="mixed-analytic.seam-unite"; // mass base = unresolved; green claim must not land
    var outP=Path.Combine(tmpDir,"out-t5.json");
    var ev=WriteEvents("t5",new JsonArray{CaseUpdate(targetId,"mass","verified","","2026-11-12T13:00:00Z","isolated: green without evidence",host:"q2 Linux x64")});
    var r=RunProducer(ev,outP);
    var row=Row(outP,targetId)["dimensions"]!["mass"]!;
    var vr=RunProducer(ev,Path.Combine(tmpDir,"out-t5-verify.json"),verify:true);
    bool rejected=row["status"]!.GetValue<string>()=="unresolved"&&vr.stdout.Contains("green without evidence",StringComparison.Ordinal);
    Test("T5 empty-evidence green rejected and recorded",rejected,$"mass stays {row["status"]} verifyFlags={vr.stdout.Contains("green without evidence")}");
}

// T6 bad dimension/status/utc rejected
{
    var targetId="analytic-boolean.equal-overlap-subtract";
    var outP=Path.Combine(tmpDir,"out-t6.json");
    var ev=WriteEvents("t6",new JsonArray{CaseUpdate(targetId,"geometry","great","temp_docs/bogus.md","not-a-timestamp","isolated: garbage",host:"Mac M4")});
    var r=RunProducer(ev,outP);
    var dims=Row(outP,targetId)["dimensions"]!.AsObject();
    Test("T6 unknown dimension/status/utc rejected (row unchanged)",!dims.ContainsKey("geometry")&&dims["function"]!["status"]!.GetValue<string>()=="pass",$"dim keys={string.Join(",",dims.Select(kv=>kv.Key))} exit={r.exit}");
}

// T7 events entry tracked in the f463 candidate
{
    var tracked=File.Exists(Path.Combine("web","data","overview-events.json"));
    Test("T7 claimed update entry delivered in candidate tree",tracked,tracked?("present: "+File.ReadAllText(Path.Combine("web","data","overview-events.json")).Trim()):"missing");
}

// T8 currentVersion must not be an arbitrary string
{
    var targetId="local-blend.oblique-single-fillet";
    var outP=Path.Combine(tmpDir,"out-t8.json");
    var ev=WriteEvents("t8",new JsonArray{CaseUpdate(targetId,"mass","unresolved","temp_docs/ev8.log","2026-11-12T13:10:00Z","isolated: arbitrary currentVersion",host:"q2 Linux x64",currentVersion:"everything-fine-v99")});
    var r=RunProducer(ev,outP);
    var cv=Row(outP,targetId)["dimensions"]!["currentVersion"]?.GetValue<string>()??"";
    Test("T8 currentVersion accepts only known states (expect FAIL: arbitrary string)",cv=="reverified"||cv=="not-retested",$"currentVersion='{cv}' (raw event string written through)");
}

int errorCount=findings.OfType<JsonObject>().Count(f=>f["verdict"]!.GetValue<string>()=="error");
var receipt=new JsonObject{
    ["checkedUtc"]=JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")),
    ["target"]=JsonValue.Create(target),
    ["candidate"]=JsonValue.Create("f4632281e4df097998bfcb282508e43186816e43"),
    ["isolation"]=JsonValue.Create("all outputs under temp_docs/f463-reverify/events; target tree read-only"),
    ["tests"]=tests,
    ["findings"]=findings,
};
var receiptPath=Path.Combine(outDir,"f463-overview-events-audit.json");
File.WriteAllText(receiptPath,receipt.ToJsonString());
Console.WriteLine("RECEIPT "+receiptPath+" errors="+errorCount+" notes="+(findings.Count-errorCount));
return 0;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
static string ScriptPath([CallerFilePath]string path="")=>path;
