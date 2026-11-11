#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseOperationAcceptanceLedger=true
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);var output=Path.Combine(root,"temp_docs/operation-first-failure-0d-review/controls");Directory.CreateDirectory(output);var count=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
OperationRecord BodyResult(OperationAcceptanceLedger ledger,string id)
{
    var op=ledger.BeginOperation(id,"synthetic complete body-result recording",api:"PK_BODY_boolean",options:new JsonObject{["gm"]=new JsonObject{["function"]=1},["pk"]=new JsonObject{["function"]=1}});op.PrimaryContract="capture-body-result-v1";
    var names=new List<string>{"body-count","True-body-check","False-body-check","equivalence"};for(var i=0;i<5;i++)names.Add("count-"+i);for(var i=0;i<15;i++)names.Add("mass-"+i);
    foreach(var name in names)ledger.RecordCheck(id,"primary",op.Ordinal+"-"+name,"synthetic contract only","pass");ledger.RecordReturn(id,"gm",0);ledger.RecordReturn(id,"pk",0);ledger.EndOperation(id);return op;
}
void DeleteFails(OperationAcceptanceLedger ledger)
{
    try{using var child=new OperationStepScope(ledger,"parent","delete","PK_ENTITY_delete");child.Return("pk",22);throw new IOException("synthetic cleanup");}catch(IOException e) when(e.Message=="synthetic cleanup"){}
}
foreach(var name in new[]{"parent-before-cleanup","propagation-only","child-before-parent-owned","parent-pass-cleanup-fail","normal-second-rep"})
{
    var ledger=new OperationAcceptanceLedger(name,"independent BCL control");var parent=BodyResult(ledger,"parent");
    if(name=="parent-before-cleanup")ledger.RecordCheck("parent","primary","strict-compare","earlier independent verification","fail");
    if(name!="normal-second-rep")DeleteFails(ledger);
    if(name=="child-before-parent-owned")ledger.RecordCheck("parent","primary","strict-compare","later independent verification","fail");
    if(name is "parent-before-cleanup" or "propagation-only" or "child-before-parent-owned")ledger.EndOperation("parent","fail");
    if(name=="normal-second-rep")
    {
        ledger.RecordCheck(parent.Id,"auxiliary","observer","synthetic observer fail","fail");ledger.RecordCheck(parent.Id,"representation","representation","synthetic representation fail","fail");
        parent.GmMs=double.NaN;parent.PkMs=1;var second=BodyResult(ledger,"parent-rep-2");second.GmMs=2;second.PkMs=1;
        Check(!ReferenceEquals(parent,second)&&second.PrimaryChecks.All(c=>c["owner"]!.GetValue<string>()==second.Id)&&second.AuxiliaryChecks.Count==0,"second rep has independent owner and checks");
    }
    ledger.EndOperation("parent");ledger.EndScenario(0);
    var expected=name=="parent-before-cleanup"?"parent":name=="normal-second-rep"?null:"parent/delete";
    Check(ledger.FirstFailedOperationId==expected,name+" first owner follows recorded primary failure order");
    Check(parent.PrimaryStatus==(name is "parent-before-cleanup" or "propagation-only" or "child-before-parent-owned"?"fail":"pass"),name+" parent primary status retained");
    Check(parent.PrimaryChecks.Count(c=>c["status"]!.GetValue<string>()=="pass")==24,name+" all earlier primary passes retained");
    Check(ledger.ScenarioStatus==(name=="normal-second-rep"?"pass":"fail")&&ledger.LegacyExitCode==0,name+" scenario result and actual caller exit stay separate");
    if(name!="normal-second-rep")
    {
        var child=ledger.GetOperation("parent/delete")!;Check(child.Returns["gm"]==null&&child.Returns["pk"]!.GetValue<int>()==22&&child.Status=="fail",name+" one-sided actual return and failed child retained");
        Check(ledger.AllChecks.Any(c=>c["owner"]!.GetValue<string>()==child.Id&&c["id"]!.GetValue<string>().EndsWith("-api-return-pk")),name+" API return failure event present before terminal event");
    }
    if(name is "parent-before-cleanup" or "child-before-parent-owned")Check(parent.PrimaryChecks.Any(c=>c["id"]!.GetValue<string>()=="strict-compare"&&c["status"]!.GetValue<string>()=="fail"),name+" independent parent and child errors both retained");
    if(name=="normal-second-rep")Check(parent.HasAuxiliaryFailure&&parent.PrimaryStatus=="pass"&&parent.RepresentationStatus=="fail"&&parent.Timing["gmMs"]==null&&parent.Timing["ratio"]==null&&!parent.Timing["correctnessQualified"]!.GetValue<bool>(),"aux/representation failures and nonfinite timing do not become primary fail or qualified ratio");
    Check(ledger.Operations.All(o=>!o.Timing["correctnessQualified"]!.GetValue<bool>()&&o.Timing["ratio"]==null),name+" no invented same-input qualification");
    var folder=Path.Combine(output,name);Directory.CreateDirectory(folder);ledger.SaveSidecar(Path.Combine(folder,"ledger.json"));
}
var boundaries=new JsonObject();
var explicitOnly=new OperationAcceptanceLedger("explicit-only","no raw call returns");var earlyBegin=explicitOnly.BeginOperation("early-begin","first begin");var lateBegin=explicitOnly.BeginOperation("late-begin","second begin");
explicitOnly.EndOperation(lateBegin.Id,"fail");explicitOnly.EndOperation(earlyBegin.Id,"fail");explicitOnly.EndScenario(null);
Check(explicitOnly.FirstFailedOperationId==lateBegin.Id&&explicitOnly.AllChecks.Count==2&&lateBegin.Returns["gm"]==null&&lateBegin.Returns["pk"]==null,"explicit termination records failure order without inventing API returns or begin-order priority");boundaries["explicitEnd"]=explicitOnly.ToJsonObject();
var missing=new OperationAcceptanceLedger("missing-order","legacy/raw mutation control, no recorded failure events");var p=missing.BeginOperation("p","parent");var ch=missing.BeginOperation("p/child","child");ch.ParentOperationId=p.Id;p.SetTerminalStatus("fail");ch.SetTerminalStatus("fail");missing.EndScenario(null);
Check(missing.AllChecks.Count==0&&missing.FirstFailedOperationId==ch.Id,"no-check legacy input retains structural fallback; no chronology evidence created");boundaries["missingOrder"]=missing.ToJsonObject();
var unknown=new OperationAcceptanceLedger("unknown","unknown return control");var unknownOp=unknown.BeginOperation("unknown","unknown API");unknown.RecordReturn(unknownOp.Id,"gm",0);unknown.EndScenario(null);
Check(unknownOp.Returns["pk"]==null&&unknownOp.Status=="unverified"&&unknown.FirstFailedOperationId==null&&!unknownOp.Timing["correctnessQualified"]!.GetValue<bool>(),"unknown API and missing side remain unverified with no invented first failure");boundaries["unknown"]=unknown.ToJsonObject();
File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"boundary-facts.json"),boundaries.ToJsonString());Console.WriteLine("ASSERTIONS "+count+"; five groups plus bounded event-source/unknown controls, no PK calls");
static string Here([CallerFilePath]string p="")=>p;
