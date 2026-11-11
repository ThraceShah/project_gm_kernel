#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseOperationAcceptanceLedger=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

var here=Path.GetDirectoryName(Here())!;var root=Path.GetFullPath("../../../..",here);
var output=Path.Combine(root,"temp_docs/operation-2c06-review/scopes");Directory.CreateDirectory(output);
if(args.Length>0)
{
    var mode=args[0];var ledger=new OperationAcceptanceLedger("bcl-control","recording, no geometry");
    var prior=ledger.BeginOperation("prior","completed synthetic body-result evidence",api:"PK_BODY_boolean");prior.PrimaryContract="capture-body-result-v1";
    var suffixes=new List<string>{"body-count","True-body-check","False-body-check","equivalence"};for(var i=0;i<5;i++)suffixes.Add("count-"+i);for(var i=0;i<15;i++)suffixes.Add("mass-"+i);
    foreach(var suffix in suffixes)ledger.RecordCheck("prior","primary","0-"+suffix,"synthetic prior evidence","pass");
    ledger.RecordReturn("prior","gm",0);ledger.RecordReturn("prior","pk",0);ledger.EndOperation("prior");
    ledger.BeginOperation("parent","integration",api:"integration-lifecycle");
    try
    {
        if(mode=="cleanup")
        {
            using(var step=new OperationStepScope(ledger,"parent","evaluate","sampled-numerical-suite"))
            {ledger.RecordCheck(step.Id!,"primary","sampled-pass","synthetic sampled pass","pass");step.Complete();}
            try { }
            finally {using var cleanup=new OperationStepScope(ledger,"parent","delete","PK_ENTITY_delete");cleanup.Return("gm",5002);throw new InvalidOperationException("control-cleanup-throw");}
        }
        using var call=new OperationStepScope(ledger,"parent","call","PK_ENTITY_copy");
        if(mode=="after-return")call.Return("pk",17);
        throw new InvalidOperationException("control-unhandled-"+mode);
    }
    catch
    {
        ledger.EndOperation("parent","fail");ledger.AddUnreachedOperation("sibling",ledger.Operations.Count,"unreached sibling","lifecycle");throw;
    }
    finally
    {
        ledger.InProcessExitCode=Environment.ExitCode;ledger.EndScenario(null);ledger.SaveSidecar(Path.Combine(output,mode+".json"));
    }
}
var count=0;
void Check(bool good,string label){if(!good)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
foreach(var mode in new[]{"before-return","after-return","cleanup"})
{
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
    foreach(var arg in new[]{"run","--file",Path.GetRelativePath(root,Here()),"-p:UseSharedCompilation=false","--",mode})start.ArgumentList.Add(arg);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();await child.WaitForExitAsync();
    var error=await stderr;File.WriteAllText(Path.Combine(output,mode+".log"),(await stdout)+error);File.WriteAllText(Path.Combine(output,mode+".exit"),child.ExitCode.ToString());
    Check(child.ExitCode==134&&error.Contains("Unhandled exception.")&&error.Contains("control-"),mode+" preserves uncaught exception and actual outer 134");
    var json=JsonNode.Parse(File.ReadAllBytes(Path.Combine(output,mode+".json")))!;var ops=json["operations"]!.AsArray().ToDictionary(o=>o!["operationId"]!.GetValue<string>());
    Check(json["legacyExitCode"]==null&&json["inProcessExitCode"]!.GetValue<int>()==0,mode+" checkpoint does not sign process zero");
    Check(json["firstFailedOperationId"]!.GetValue<string>()=="parent/"+(mode=="cleanup"?"delete":"call"),mode+" failure belongs to deepest actual child");
    Check(ops["prior"]!["status"]!.GetValue<string>()=="pass"&&ops["prior"]!["primaryChecks"]!.AsArray().Count==24,mode+" retains prior primary status pass and all 24 checks");
    Check(ops["prior"]!["timing"]!["correctnessQualified"]!.GetValue<bool>()==false&&ops["prior"]!["timing"]!["ratio"]==null,mode+" prior missing identity and timing remain unqualified");
    Check(ops["sibling"]!["status"]!.GetValue<string>()=="not-run"&&ops["sibling"]!["returns"]!["gm"]==null,mode+" unreached sibling has no invented return");
    var call=ops["parent/"+(mode=="cleanup"?"delete":"call")]!;
    if(mode=="before-return")Check(call["returns"]!["gm"]==null&&call["returns"]!["pk"]==null,"pre-return exception preserves both nulls");
    if(mode=="after-return")Check(call["returns"]!["gm"]==null&&call["returns"]!["pk"]!.GetValue<int>()==17,"post-return exception preserves actual one-sided return");
    if(mode=="cleanup")Check(call["returns"]!["gm"]!.GetValue<int>()==5002&&call["returns"]!["pk"]==null&&ops["parent/evaluate"]!["primaryChecks"]![0]!["status"]!.GetValue<string>()=="pass","cleanup fail retains earlier sampled pass and distinct return owner");
}
foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
{
    var ledger=new OperationAcceptanceLedger("nonfinite","BCL recording");var operation=ledger.BeginOperation("timed","synthetic",api:"PK_BODY_boolean");
    ledger.RecordTiming(operation.Id,"gm",invalid);ledger.RecordTiming(operation.Id,"pk",1);ledger.EndScenario(null);
    var json=ledger.ToJsonObject();var timing=json["operations"]![0]!["timing"]!;
    Check(timing["gmMs"]==null&&timing["ratio"]==null&&!timing["correctnessQualified"]!.GetValue<bool>(),"nonfinite "+invalid+" sanitizes actual recording to null without qualification");
}
Console.WriteLine("ASSERTIONS "+count+"; BCL controls only, no model or PK API call");
static string Here([CallerFilePath]string path="")=>path;
