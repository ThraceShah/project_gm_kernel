#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var actor=Path.GetFullPath("../gm-operation-recording-r2",root);
var output=Path.Combine(root,"temp_docs/operation-2c06-review");Directory.CreateDirectory(output);
var inventory=new JsonArray();var assertions=0;
string Hash(string p)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
if(args.Contains("--verify-unchanged"))
{
    foreach(var entry in JsonNode.Parse(File.ReadAllBytes(Path.Combine(output,"artifact-inventory.json")))!.AsArray())
        if(Hash(Path.Combine(actor,entry!["path"]!.GetValue<string>()))!=entry["sha256"]!.GetValue<string>())throw new Exception("Artifact changed: "+entry["path"]);
    Console.WriteLine("PASS all inventoried actor artifacts unchanged");return;
}
JsonObject Read(string p){var file=Path.Combine(actor,p);inventory.Add((JsonNode)new JsonObject{["path"]=p,["sha256"]=Hash(file)});return JsonNode.Parse(File.ReadAllBytes(file))!.AsObject();}
void Check(bool good,string label){if(!good)throw new Exception(label);assertions++;Console.WriteLine("PASS "+label);}
var cases=new[]{"freeform-final","local-import","local-analytic","local-final-rejection","geometry-body","geometry-body-final","evaluation-offset","evaluation-offset-final","evaluation-offset-provenance","short-edge-final"};
foreach(var name in cases)
{
    var prefix="temp_docs/operation-leaf-r2/"+name+"/";var ledger=Read(prefix+"ledger.json");
    var ops=ledger["operations"]!.AsArray();
    foreach(var op in ops)
    {
        if(op!["options"]!=null)Check(op["parameterHash"]!.GetValue<string>()==Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(op["options"]!.ToJsonString()))),name+" recomputable parameter snapshot "+op["operationId"]);
        foreach(var side in new[]{"gm","pk"})
            foreach(var hash in op["outputHashes"]?[side]?["bodies"] as JsonArray??[])
                Check(Directory.GetFiles(Path.Combine(actor,prefix),"*.x_t").Any(file=>Hash(file)==hash!.GetValue<string>()),name+" saved output byte hash "+side);
    }
    Check(ops.All(o=>o!["timing"]!["correctnessQualified"]?.GetValue<bool>()!=true&&o["timing"]!["ratio"]==null),name+" withholds qualification and ratio");
    Check(ops.All(o=>new[]{"primaryChecks","auxiliaryChecks","representationContracts"}.All(cat=>o![cat]!.AsArray().All(c=>c!["owner"]!.GetValue<string>()==o["operationId"]!.GetValue<string>()))),name+" check ownership");
    var executionPath=Path.Combine(actor,prefix+"execution.json");
    if(File.Exists(executionPath))
    {
        var execution=Read(prefix+"execution.json");var inprocess=Read(prefix+"ledger.in-process.json");
        Check(execution["exitCode"]!.GetValue<int>()==(name.StartsWith("evaluation")?134:0),name+" raw supervisor exit");
        Check(JsonNode.DeepEquals(execution["exitCode"],ledger["legacyExitCode"]),name+" recorded outer exit");
        Console.WriteLine("INPROCESS "+name+" "+inprocess["legacyExitCode"]+" diagnostic="+inprocess["inProcessExitCode"]);
        foreach(var entry in execution["sourceHashes"]!.AsObject())
            Console.WriteLine("SOURCE "+name+" "+entry.Key+" "+entry.Value+" candidateMatch="+(File.Exists(Path.Combine(root,entry.Key))&&Hash(Path.Combine(root,entry.Key))==entry.Value!.GetValue<string>()));
    }
    foreach(var xt in Directory.GetFiles(Path.Combine(actor,prefix),"*.x_t"))inventory.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(actor,xt),["sha256"]=Hash(xt)});
    foreach(var log in new[]{"oracle.log","report.txt"}.Select(file=>Path.Combine(actor,prefix,file)).Where(File.Exists))inventory.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(actor,log),["sha256"]=Hash(log)});
    Console.WriteLine("LEDGER "+name+" "+ledger["scenarioStatus"]+" first="+ledger["firstFailedOperationId"]);
    foreach(var op in ops)Console.WriteLine("OP "+op!["operationId"]+" api="+op["api"]+" status="+op["status"]+" returns="+op["returns"]!.ToJsonString()+" primary="+op["primaryChecks"]!.AsArray().Count);
    if(name=="local-final-rejection")
    {
        var op=ops.Single()!;Check(op["returns"]!["gm"]!.GetValue<int>()==13&&op["returns"]!["pk"]!.GetValue<int>()==13&&op["status"]!.GetValue<string>()=="unverified","rejection remains 13/13 unverified");
        Check(op["primaryChecks"]!.AsArray().Count==13&&op["primaryChecks"]!.AsArray().All(c=>c!["status"]!.GetValue<string>()=="pass"),"13 independent rejection checks retained");
        var rollback=op["preconditions"]!.AsArray().Single(n=>n!["kind"]!.GetValue<string>()=="gm-rollback-xt")!;
        Check(JsonNode.DeepEquals(rollback["beforeSha256"],rollback["afterSha256"])&&rollback["beforeSha256"]!.GetValue<string>()==Hash(Path.Combine(actor,prefix+"separated-walls-in-own.x_t")),"GM rollback hashes bind saved bytes");
    }
    if(name=="evaluation-offset-provenance")
    {
        var byId=ops.ToDictionary(o=>o!["operationId"]!.GetValue<string>());
        Check(ledger["firstFailedOperationId"]!.GetValue<string>()=="op-dep-offset/delete-base-gm","evaluation first failure is GM base delete");
        Check(byId["op-dep-offset/delete-base-pk"]!["returns"]!["pk"]!.GetValue<int>()==22&&byId["op-dep-offset/delete-base-gm"]!["returns"]!["gm"]!.GetValue<int>()==5002,"actual ordered base deletion returns");
        Check(byId["op-dep-offset/delete-base-gm"]!["returns"]!["pk"]==null,"GM-only failure leaves PK null");
        foreach(var side in new[]{"positive","negative"})Check(byId["op-dep-offset/evaluate-"+side]!["primaryChecks"]!.AsArray().Single()!["status"]!.GetValue<string>()=="pass","prior "+side+" sampled pass retained");
    }
}
foreach(var spec in new[]{("operation-short-case-final","r2-short-edge-case-scope-final"),("operation-short-capture-final","r2-short-edge-owner-final"),("operation-short-scenario-owner","r2-short-edge-scenario-owner"),("operation-final-capture","r2-capture-shared-returns-final")})
{
    var prefix="temp_docs/"+spec.Item1+"/data/runs/"+spec.Item2+"/";
    if(!File.Exists(Path.Combine(actor,prefix+"manifest.json")))continue;
    var manifest=Read(prefix+"manifest.json");
    if(File.Exists(Path.Combine(actor,prefix+"validation.json")))Read(prefix+"validation.json");
    foreach(var c in manifest["cases"]!.AsArray().Where(c=>c!["operations"]!.AsArray().Count>0))
    {
        Console.WriteLine("CAPTURE "+spec.Item2+" "+c!["id"]+" status="+c["status"]+" perf="+c["performance"]!.ToJsonString());
        foreach(var op in c["operations"]!.AsArray())Console.WriteLine("CAPTUREOP "+op!["operationId"]+" primary="+op["primaryChecks"]!.AsArray().Count+" aux="+op["auxiliaryChecks"]!.AsArray().Count+" status="+op["status"]);
        foreach(var sampleDir in Directory.GetDirectories(Path.Combine(actor,prefix+"cases/"+c["id"])))
        {
            var sample=Path.GetRelativePath(actor,sampleDir).Replace('\\','/')+"/";var capture=Read(sample+"capture.json");var execution=Read(sample+"execution.json");
            Check(execution["exitCode"]!.GetValue<int>()==0,spec.Item2+" original process 0");
            Console.WriteLine("CAPTUREERRORS "+spec.Item2+" "+capture["captureErrors"]!.ToJsonString());
            foreach(var e in execution["sourceHashes"]!.AsObject().Where(e=>new[]{"scripts/WebProgressCapture.cs","scripts/ShortEdgeBlendOracle.cs","scripts/OperationAcceptanceLedger.cs","scripts/OperationAcceptanceRules.cs"}.Contains(e.Key)))Console.WriteLine("SOURCE "+spec.Item2+" "+e.Key+" "+e.Value+" candidateMatch="+(Hash(Path.Combine(root,e.Key))==e.Value!.GetValue<string>()));
            if(spec.Item1=="operation-short-case-final")
            {
                Check(capture["captureErrors"]!.AsArray().Count==0,"final ShortEdge has no capture errors");
                var op=capture["operations"]!.AsArray().Single()!;
                Check(op["primaryChecks"]!.AsArray().Count==26&&op["primaryChecks"]!.AsArray().All(check=>check!["owner"]!.GetValue<string>()==op["operationId"]!.GetValue<string>()&&check["status"]!.GetValue<string>()=="pass"),"26 primary same actual owner");
                Check(capture["checks"]!.AsArray().Count(check=>check!["owner"]!.GetValue<string>()=="scenario")>=2,"preparation checks retained at scenario");
                Check(c["performance"]!["ratioGmOverPk"]==null&&op["timing"]!["ratio"]==null,"ShortEdge input identity remains unqualified");
            }
        }
    }
}
File.WriteAllText(Path.Combine(output,"artifact-inventory.json"),inventory.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine("ASSERTIONS "+assertions+" FILES "+inventory.Count);
static string Here([CallerFilePath]string p="")=>p;
