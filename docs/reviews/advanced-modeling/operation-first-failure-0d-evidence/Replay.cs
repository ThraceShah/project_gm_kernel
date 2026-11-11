#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var output=Path.Combine(root,"temp_docs/operation-first-failure-0d-review");
if(Directory.Exists(output))throw new IOException("Use a fresh review output directory; original artifacts are never overwritten");
Directory.CreateDirectory(output);var facts=new JsonArray();var assertions=0;
string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
void Check(bool good,string label){if(!good)throw new Exception(label);assertions++;Console.WriteLine("PASS "+label);}
async Task<(int Exit,string Output)> Run(string label,string workingDirectory,params string[] arguments)
{
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=workingDirectory,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var arg in arguments)start.ArgumentList.Add(arg);start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    var log=(await stdout)+(await stderr);File.WriteAllText(Path.Combine(output,label+".log"),log);File.WriteAllText(Path.Combine(output,label+".exit"),process.ExitCode.ToString());
    facts.Add((JsonNode)new JsonObject{["entry"]=label,["exit"]=process.ExitCode,["logSha256"]=Hash(System.Text.Encoding.UTF8.GetBytes(log))});
    return(process.ExitCode,log);
}
byte[] Git(string revision,string file)
{
    var start=new ProcessStartInfo("git"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true};start.ArgumentList.Add("show");start.ArgumentList.Add(revision+":"+file);
    using var process=Process.Start(start)!;using var bytes=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(bytes);process.WaitForExit();if(process.ExitCode!=0)throw new Exception("Cannot archive "+file);return bytes.ToArray();
}
var probePath="docs/reviews/advanced-modeling/operation-2c06-evidence/FirstFailureProbe.cs";
var probe=Git("40f6ac3bc6e0df5725bdfef3a1c4e1096250529d",probePath);
Check(Hash(probe)=="8ee9dc3e339c90164128a18bdc7b218bdac4e3b49451270f0579aed7d304b48c","original reviewer probe bytes fixed");
var actor=Path.GetFullPath("../gm-operation-recording-r2/temp_docs/operation-first-failure",root);var actorFiles=new JsonArray();
foreach(var name in new[]{"original-first-failure.json","before-first-failure.json","after-first-failure.json","before.log","after.log","evidence-check.log"}.Concat(Directory.GetFiles(Path.Combine(actor,"controls"),"ledger.json",SearchOption.AllDirectories).Select(p=>Path.GetRelativePath(actor,p))))
{
    var bytes=File.ReadAllBytes(Path.Combine(actor,name));var copy=Path.Combine(output,"author-original",name);Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.WriteAllBytes(copy,bytes);
    actorFiles.Add((JsonNode)new JsonObject{["path"]=name,["sha256"]=Hash(bytes)});
}
File.WriteAllText(Path.Combine(output,"author-inventory.json"),actorFiles.ToJsonString(new(){WriteIndented=true}));
foreach(var version in new[]{("before","2c06b9da80737f2d501bd5e1886c3ea1494908c7",1,"db8e77e95fa32e6354b912789662606036d88b477d59b2bcf9a29a389bb38988"),("after","0d611d174c65b30f66383526e374fd841ded39c5",0,"6c7c3624223cc30311f6ad7ede61560b2062d7e2f207a8a6d2ae65fa7a1fe81c")})
{
    var stage=Path.Combine(output,version.Item1);var sources=new JsonObject();
    foreach(var file in new[]{"scripts/Directory.Build.props","scripts/OperationAcceptanceLedger.cs","scripts/OperationAcceptanceRules.cs"})
    {var bytes=Git(version.Item2,file);var target=Path.Combine(stage,file);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllBytes(target,bytes);sources[file]=Hash(bytes);}
    var targetProbe=Path.Combine(stage,probePath);Directory.CreateDirectory(Path.GetDirectoryName(targetProbe)!);File.WriteAllBytes(targetProbe,probe);
    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(targetProbe)!,"Directory.Build.props"),Git("40f6ac3", "docs/reviews/advanced-modeling/operation-2c06-evidence/Directory.Build.props"));
    var result=await Run(version.Item1,stage,"run","--no-cache","--file",probePath,"-c","Release","-p:UseSharedCompilation=false");Check(result.Exit==version.Item3,version.Item1+" original probe exit "+result.Exit);
    var bytesResult=File.ReadAllBytes(Path.Combine(stage,"temp_docs/operation-2c06-review/first-failure.json"));File.WriteAllBytes(Path.Combine(output,version.Item1+"-first-failure.json"),bytesResult);
    Check(Hash(bytesResult)==version.Item4,version.Item1+" exact original JSON hash");facts.Add((JsonNode)new JsonObject{["stage"]=version.Item1,["revision"]=version.Item2,["sourceHashes"]=sources,["probeSha256"]=Hash(probe),["jsonSha256"]=Hash(bytesResult)});
}
var before=JsonNode.Parse(File.ReadAllBytes(Path.Combine(output,"before-first-failure.json")))!;var after=JsonNode.Parse(File.ReadAllBytes(Path.Combine(output,"after-first-failure.json")))!;
Check(before["firstFailedOperationId"]!.GetValue<string>()=="verify/delete-received"&&after["firstFailedOperationId"]!.GetValue<string>()=="verify","first cause corrected");
before.AsObject().Remove("firstFailedOperationId");after.AsObject().Remove("firstFailedOperationId");Check(JsonNode.DeepEquals(before,after),"only firstFailedOperationId differs; both failures and original exit retained");
var controls=await Run("controls",root,"run","--no-cache","--file","docs/reviews/advanced-modeling/operation-first-failure-0d-evidence/Controls.cs","-c","Release","-p:UseSharedCompilation=false");Check(controls.Exit==0,"independent five groups and bounded missing-order controls");
var templateRoot=Path.Combine(root,"temp_docs/operation-2c06-review/disk/legacy-missing");var template=JsonNode.Parse(File.ReadAllBytes(Path.Combine(templateRoot,"runs/test/manifest.json")))!;
foreach(var ledger in new[]{"before-first-failure.json","after-first-failure.json"}.Select(p=>Path.Combine(output,p)).Concat(Directory.GetFiles(Path.Combine(output,"controls"),"ledger.json",SearchOption.AllDirectories)))
{
    var label=Path.GetFileName(ledger)=="ledger.json"?Path.GetFileName(Path.GetDirectoryName(ledger)):Path.GetFileNameWithoutExtension(ledger);var data=Path.Combine(output,"disk",label);var run=Path.Combine(data,"runs/test");Directory.CreateDirectory(run);
    var manifest=template.DeepClone();var c=manifest["cases"]![0]!.AsObject();var recorded=JsonNode.Parse(File.ReadAllBytes(ledger))!.AsObject();
    foreach(var fact in recorded)c[fact.Key]=fact.Value?.DeepClone();
    c["status"]=recorded["scenarioStatus"]!.GetValue<string>()=="pass"&&recorded["checks"]!.AsArray().All(ch=>ch!["status"]!.GetValue<string>() is "pass" or "diagnostic")?"pass":"fail";c["performance"]!["kind"]="scenario-aggregate";c["performance"]!["correctnessQualified"]=false;c["performance"]!["ratioGmOverPk"]=null;
    File.WriteAllText(Path.Combine(run,"manifest.json"),manifest.ToJsonString());File.Copy(Path.Combine(templateRoot,"index.json"),Path.Combine(data,"index.json"));
    var result=await Run("disk-"+label,root,"run","--file","scripts/ValidateWebProgress.cs","-c","Release","-p:UseSharedCompilation=false","--",Path.GetRelativePath(Path.Combine(root,"scripts"),data));Check(result.Exit==0,"public disk CLI retains "+label+" as valid recorded result");
    var roundtrip=JsonNode.Parse(File.ReadAllBytes(Path.Combine(run,"manifest.json")))!["cases"]![0]!;
    Check(JsonNode.DeepEquals(roundtrip["firstFailedOperationId"],recorded["firstFailedOperationId"])&&JsonNode.DeepEquals(roundtrip["operations"],recorded["operations"]),label+" disk fields unchanged");
}
foreach(var entry in actorFiles)Check(Hash(File.ReadAllBytes(Path.Combine(actor,entry!["path"]!.GetValue<string>())))==entry["sha256"]!.GetValue<string>(),"author original unchanged "+entry["path"]);
File.WriteAllText(Path.Combine(output,"replay-facts.json"),facts.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("ASSERTIONS "+assertions+"; BCL only, no PK lifecycle or modeling acceptance");
static string Here([CallerFilePath]string p="")=>p;
