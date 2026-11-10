#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
if(!args.Contains("--unpublished"))throw new ArgumentException("Only an unpublished artifact may be finalized; explicit --unpublished required.");
var scripts=Path.GetDirectoryName(ScriptPath())!;var data=Path.GetFullPath(Option("--data","../web/data"),scripts);var id=Option("--run-id","");
if(id.Length==0||id.Contains('/')||id.Contains('\\')||id is "." or "..")throw new ArgumentException("Safe exact --run-id required.");
var run=Path.Combine(data,"runs",id);var file=Path.Combine(run,"manifest.json");var registry=JsonNode.Parse(File.ReadAllText(Path.Combine(run,"registry.json")))!.AsObject();
var manifest=JsonNode.Parse(File.ReadAllText(file))!.AsObject();
if(manifest["run"]!["provenance"]?["metadataFinalization"]!=null)
{
    if(!args.Contains("--refresh-labels")){Console.WriteLine("Already finalized "+id);return 0;}
    var previousSignature=ResultsSignature(manifest);WebProgressMetadata.Apply(manifest,registry);
    if(previousSignature!=ResultsSignature(manifest))throw new InvalidDataException("Label refresh changed modeling results");
    manifest["run"]!["provenance"]!["metadataFinalization"]!["labelsRefreshedAt"]=DateTimeOffset.UtcNow.ToString("O");
    File.WriteAllText(file,manifest.ToJsonString());Console.WriteLine("Refreshed labels with unchanged results "+previousSignature);return 0;
}
var original=File.ReadAllBytes(file);var signature=ResultsSignature(manifest);WebProgressMetadata.Apply(manifest,registry);
if(signature!=ResultsSignature(manifest))throw new InvalidDataException("Metadata finalization changed modeling results.");
manifest["run"]!["provenance"]!["metadataFinalization"]=new JsonObject{["version"]=1,["appliedAt"]=DateTimeOffset.UtcNow.ToString("O"),["originalManifestSha256"]=Hash(original),["processorSha256"]=Hash(File.ReadAllBytes(ScriptPath())),["resultSignature"]=signature,["note"]="Additive capability/timing labels before first distribution; actual execution commit, statuses, parameters, steps, all checks, numeric timings/ratios and raw evidence are unchanged."};
var backup=Path.GetFullPath("../temp_docs/web-progress/metadata-before",scripts);Directory.CreateDirectory(backup);File.WriteAllBytes(Path.Combine(backup,id+".manifest.json"),original);
File.WriteAllText(file+".tmp",manifest.ToJsonString());File.Move(file+".tmp",file,true);
var validation=WebProgressDataValidation.Validate(data);File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());Console.WriteLine(validation.ToJsonString());return validation["errors"]!.AsArray().Count==0?0:2;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
static string ResultsSignature(JsonObject manifest)
{
    var cases=new JsonArray();foreach(var c in manifest["cases"]!.AsArray())
    {
        var result=new JsonObject();foreach(var field in new[]{"id","title","family","operation","status","parameters","apis","steps","checks","evidence","source","execution"})result[field]=c![field]?.DeepClone();
        var perf=new JsonObject();foreach(var field in new[]{"gmMs","pkMs","ratioGmOverPk","samples","warmup","scope","correctnessQualified","host","configuration","note"})perf[field]=c!["performance"]![field]?.DeepClone();result["performance"]=perf;cases.Add((JsonNode)result);
    }
    return Hash(Encoding.UTF8.GetBytes(cases.ToJsonString()));
}
