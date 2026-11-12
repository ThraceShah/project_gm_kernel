#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

// Isolated derived-event audit for the project-overview candidate. Runs the
// real producer (scripts/BuildProjectOverview.cs) as a subprocess against
// synthetic append-only event files and temp outputs under
// temp_docs/independent-overview-audit/events/; originals (web/data and the
// sealed evidence) are never written. Required behaviors under test:
//   row and summary counters change together; the fixed catalog never shrinks;
//   a real new failure is not overwritten by an old green; wrong-parameter /
//   wrong-platform events cannot cross-sign; missing evidence cannot be green;
//   bad events are rejected or left unverified, never silently signed.
// Each test records observed behavior as a finding when the requirement is
// not met. Audit exit codes are separate from every original process exit.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);Directory.SetCurrentDirectory(root);
var producer=Path.Combine(scripts,"BuildProjectOverview.cs");
var tmpDir=Path.Combine("temp_docs","independent-overview-audit","events");Directory.CreateDirectory(tmpDir);
var outDir=Path.Combine("docs","reviews","web","receipts");Directory.CreateDirectory(outDir);
var tests=new JsonArray();var findings=new JsonArray();
void Finding(string s){findings.Add((JsonNode?)JsonValue.Create(s));Console.WriteLine("FINDING "+s);}

(string stdout,int exit) RunProducer(string? eventsPath,string outputPath,bool verify=false)
{
    var psi=new ProcessStartInfo("dotnet"){WorkingDirectory=root};
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
JsonObject CaseUpdate(string catalogId,string dimension,string status,string evidence,string utc,string note,string? host=null,string? commit=null)
    =>new JsonObject{{"kind","case-update"},{"catalogId",catalogId},{"dimension",dimension},{"status",status},
        {"lastVerified",new JsonObject{{"commit",commit??"audit"},{"tree","audit"},{"host",host??"audit-host"},{"exit","audit"},{"evidence",JsonValue.Create(evidence)}}},
        {"note",JsonValue.Create(note)},{"utc",JsonValue.Create(utc)}};
void Test(string name,bool ok,string detail)
{
    tests.Add((JsonNode)new JsonObject{{"name",JsonValue.Create(name)},{"ok",JsonValue.Create(ok)},{"detail",JsonValue.Create(detail)}});
    Console.WriteLine((ok?"PASS ":"FAIL ")+name+" :: "+detail);
    if(!ok)Finding(name+" :: "+detail);
}

var committed=JsonNode.Parse(File.ReadAllText(Path.Combine("web","data","project-overview.json")))!.AsObject();
string Norm(JsonObject o){var c=(JsonObject)o.DeepClone();c["generatedAt"]="X";return c.ToJsonString();}

// T0 baseline: no events -> output equals the committed overview (modulo generatedAt)
{
    var outP=Path.Combine(tmpDir,"out-baseline.json");
    var r=RunProducer(null,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    bool same=string.Equals(Norm(committed),Norm(fresh),StringComparison.Ordinal);
    Test("T0 baseline regeneration matches committed overview",same,okDetail(same,r));
}
string okDetail(bool ok,(string stdout,int exit) r)=>ok?($"exit={r.exit}"):($"exit={r.exit} stdout-tail={r.stdout[^Math.Min(200,r.stdout.Length)..].Replace("\n"," | ")}");

// T1 row and summary counters must change together
{
    var target="nested-evaluation.depth3-sequential-D0"; // a currently unverified row
    var outP=Path.Combine(tmpDir,"out-t1.json");
    var ev=WriteEvents("t1",new JsonArray{CaseUpdate(target,"function","pass","docs/audit-evidence.md","2026-11-12T10:00:00Z","isolated: claim pass")});
    var r=RunProducer(ev,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    var row=fresh["catalog"]!.AsArray().OfType<JsonObject>().First(c=>c["id"]!.GetValue<string>()==target);
    string rowStatus=row["dimensions"]!["function"]!["status"]!.GetValue<string>();
    int summaryPass=fresh["summary"]!["function:pass"]!.GetValue<int>();
    int committedPass=committed["summary"]!["function:pass"]!.GetValue<int>();
    bool coherent=rowStatus!="pass"||summaryPass==committedPass+1;
    Test("T1 event row change and summary counters move together",coherent,$"row.function={rowStatus} summary.function:pass {committedPass}->{summaryPass}");
}

// T2 the fixed catalog must not shrink
{
    var outP=Path.Combine(tmpDir,"out-t2.json");
    var ev=WriteEvents("t2",new JsonArray{CaseUpdate("no-such-case","function","pass","x","2026-11-12T10:00:00Z","isolated: unknown id"),new JsonObject{{"kind","delivery"},{"runId","audit"},{"label","audit"},{"commit","audit"},{"utc","2026-11-12T10:00:00Z"},{"note","isolated delivery event"}}});
    var r=RunProducer(ev,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    int n=fresh["catalog"]!.AsArray().Count;
    Test("T2 catalog fixed at 463 with delivery+unknown-id events",n==463,$"catalog={n}");
}

// T3 real new failure must not be overwritten by a later-appended old green
{
    var target="local-blend.block-all-fillet"; // currently pass
    var outP=Path.Combine(tmpDir,"out-t3.json");
    var ev=WriteEvents("t3",new JsonArray{
        CaseUpdate(target,"function","fail","temp_docs/new-failure.log","2026-11-12T11:00:00Z","isolated: real NEW failure (later utc)"),
        CaseUpdate(target,"function","pass","temp_docs/old-green.log","2026-11-10T09:00:00Z","isolated: OLD green (older utc, appended later)"),
    });
    var r=RunProducer(ev,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    var row=fresh["catalog"]!.AsArray().OfType<JsonObject>().First(c=>c["id"]!.GetValue<string>()==target);
    string rowStatus=row["dimensions"]!["function"]!["status"]!.GetValue<string>();
    int hist=row["history"]!.AsArray().Count;
    Test("T3 later-appended old green cannot overwrite a real new failure",rowStatus=="fail",$"row.function={rowStatus} historyEntries={hist} (file order: new-fail then old-green)");
}

// T4 wrong-parameter / wrong-platform events must not cross-sign
{
    var target="planar-boolean.overlap-two-axes-subtract"; // measurement row, platform=Mac M4
    var outP=Path.Combine(tmpDir,"out-t4.json");
    var ev=WriteEvents("t4",new JsonArray{
        CaseUpdate(target,"mass","verified","temp_docs/other-platform.log","2026-11-12T12:00:00Z","isolated: different parameters entirely, Windows arm64",host:"Windows arm64",commit:"deadbeef"),
    });
    var r=RunProducer(ev,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    var row=fresh["catalog"]!.AsArray().OfType<JsonObject>().First(c=>c["id"]!.GetValue<string>()==target);
    string rowStatus=row["dimensions"]!["mass"]!["status"]!.GetValue<string>();
    string host=row["dimensions"]!["mass"]!["lastVerified"]!["host"]!.GetValue<string>();
    Test("T4 wrong-parameter/wrong-platform event cannot change a row's status",rowStatus!="verified"||host.Contains("Mac",StringComparison.Ordinal),$"row.mass={rowStatus} lastVerified.host={host} (event schema carries no parameter/platform identity to check)");
}

// T5 missing evidence must not be green
{
    var target="mixed-analytic.seam-unite";
    var outP=Path.Combine(tmpDir,"out-t5.json");
    var ev=WriteEvents("t5",new JsonArray{CaseUpdate(target,"function","pass","","2026-11-12T13:00:00Z","isolated: green without evidence")});
    var r=RunProducer(ev,outP);
    var vr=RunProducer(ev,Path.Combine(tmpDir,"out-t5-verify.json"),verify:true);
    bool caught=vr.stdout.Contains("green without evidence",StringComparison.Ordinal);
    Test("T5 empty-evidence green is caught by --verify",caught,$"verifyExit={vr.exit} caught={caught}");
    // and a fabricated non-empty evidence string passes --verify untouched
    var ev2=WriteEvents("t5b",new JsonArray{CaseUpdate(target,"function","pass","fabricated-evidence-path","2026-11-12T13:30:00Z","isolated: fabricated evidence string")});
    var vr2=RunProducer(ev2,Path.Combine(tmpDir,"out-t5b-verify.json"),verify:true);
    bool fabricOk=!vr2.stdout.Contains("green without evidence",StringComparison.Ordinal);
    Test("T5b fabricated evidence text is not detectable (manual provenance required)",fabricOk,$"verifyExit={vr2.exit} (finding = authenticity not machine-checkable)");
}

// T6 bad events must be rejected or left unverified, never silently signed
{
    var target="analytic-boolean.equal-overlap-subtract";
    var outP=Path.Combine(tmpDir,"out-t6.json");
    var ev=WriteEvents("t6",new JsonArray{CaseUpdate(target,"geometry","great","temp_docs/bogus.md","not-a-timestamp","isolated: unknown dimension + unknown status + bad utc")});
    var r=RunProducer(ev,outP);
    var fresh=JsonNode.Parse(File.ReadAllText(outP))!.AsObject();
    var row=fresh["catalog"]!.AsArray().OfType<JsonObject>().First(c=>c["id"]!.GetValue<string>()==target);
    var dims=row["dimensions"]!.AsObject();
    bool hasBogus=dims.ContainsKey("geometry");
    bool untouched=dims["function"]!["status"]!.GetValue<string>()=="pass";
    Test("T6 unknown dimension/status/utc event is rejected or flagged, not written",!hasBogus,$"dimensions keys after event: {string.Join(",",dims.Select(kv=>kv.Key))}; function={dims["function"]!["status"]!.GetValue<string>()} (exit={r.exit})");
}

// T7 events entry file delivery: the claimed update entry must be part of the delivery
{
    bool tracked=false;
    var psi=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true};
    psi.ArgumentList.Add("ls-files");psi.ArgumentList.Add(Path.Combine("web","data","overview-events.json"));
    using(var p=Process.Start(psi)!){var o=p.StandardOutput.ReadToEnd();p.WaitForExit();tracked=o.Trim().Length>0;}
    Test("T7 claimed update entry web/data/overview-events.json is delivered/tracked",tracked,tracked?"tracked":"git ls-files empty: entry file absent from the candidate tree (also not gitignored)");
}

var receipt=new JsonObject{
    ["checkedUtc"]=JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")),
    ["candidate"]=JsonValue.Create("2bece5dfa952da02a3bf01a8d16f2abc0a31a276"),
    ["isolation"]=JsonValue.Create("all outputs under temp_docs/independent-overview-audit/events/; web/data and sealed evidence read-only"),
    ["tests"]=tests,
    ["findings"]=findings,
};
var receiptPath=Path.Combine(outDir,"overview-events-audit.json");
File.WriteAllText(receiptPath,receipt.ToJsonString());
Console.WriteLine("RECEIPT "+receiptPath+" tests="+tests.Count+" findings="+findings.Count);
return 0;
static string ScriptPath([CallerFilePath]string path="")=>path;
