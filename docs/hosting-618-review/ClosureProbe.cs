#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
global using ExitCode=System.Int32;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

var here=Path.GetDirectoryName(Source())!;var repo=Path.GetFullPath("../..",here);
// Durable copy of the independently executed probe. Only its evidence output
// location differs: keep generated files under temp_docs, never under docs.
var work=Path.Combine(repo,"temp_docs","hosting-618-reproducer","matrix");if(Directory.Exists(work))throw new Exception("Use a fresh checkout/evidence directory; old evidence is not overwritten");Directory.CreateDirectory(work);
var site=Path.Combine(work,"site");var cache=Path.Combine(work,"cache");var mirror=Path.Combine(work,"mirror.json");var dist=Path.Combine(work,"dist");Directory.CreateDirectory(dist);File.WriteAllText(Path.Combine(dist,"index.html"),"INDEPENDENT_618_UI");
var prepare=await Cli("prepare","PrepareHostedWebProgressSite.cs","--no-build","--dist",dist,"--site",site);Require(prepare.Code==0,"prepare failed");
var release=Path.GetFileName(Directory.GetFiles(Path.Combine(site,"ui-releases"),"*.meta.json").Single()).Replace(".meta.json","");
void Put(string run,string name,string text){var path=Path.Combine(cache,"runs",run,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,text);}
void Run(string id)
{
    Put(id,"e.log",id+"_618_EVIDENCE");Put(id,"m.json","{\"schemaVersion\":1,\"bodyId\":\""+id+"\",\"source\":{\"kernel\":\"fixture\",\"geometryAuthor\":\"fixture\",\"tessellator\":\"fixture\",\"commit\":\"sha-"+id+"\",\"chordTolerance\":0.001},\"faces\":[],\"edges\":[]}");
    var manifest=JsonNode.Parse("""
    {"schemaVersion":1,"run":{"id":"ID","commit":"sha-ID","generatedAt":"now","pkVersion":null,"host":{},"configuration":{},"scope":{},"provenance":{}},"cases":[{"id":"c","title":"closure","family":"boolean","operation":"fixture","status":"fail","parameters":{},"apis":{"gm":{"name":"fixture","parameters":{}},"pk":{"name":"fixture","parameters":{}}},"steps":[{"id":"s","title":"closure","gm":{"before":"m.json","after":"m.json","tools":["m.json"],"afterBodies":["m.json"]},"pk":{"before":"m.json","after":"m.json","tools":["m.json"],"afterBodies":["m.json"]}}],"checks":[{"id":"k","label":"closure","status":"fail","gm":1,"pk":0,"absDelta":1,"normalizedDelta":1,"threshold":0.5,"unit":"count","evidence":"e.log","note":"fixture"}],"evidence":[{"label":"raw","path":"e.log"}],"performance":{"gmMs":null,"pkMs":null,"ratioGmOverPk":null,"samples":1,"warmup":0,"scope":"fixture","correctnessQualified":false,"host":{},"configuration":{},"note":"fixture"},"notes":[],"source":{"path":"fixture","revision":"fixture"}}]}
    """.Replace("ID",id))!.AsObject();
    if(id=="r1")
    {
        manifest["cases"]![0]!["evidence"]!.AsArray().Add(new JsonObject{["label"]="cross",["path"]="../q/raw.log"});
        manifest["cases"]![0]!["steps"]![0]!["gm"]!["tools"]!.AsArray().Add((JsonNode)"../r0/m.json");
    }
    Put(id,"manifest.json",manifest.ToJsonString());
}
void Index(string latest,params string[] ids)
{
    var node=new JsonObject{["schemaVersion"]=1,["generatedAt"]="now",["latestRunId"]=latest,["runs"]=new JsonArray(ids.Select(id=>(JsonNode)new JsonObject{["id"]=id,["commit"]="sha-"+id,["generatedAt"]="now",["label"]="fixture",["manifest"]=$"runs/{id}/manifest.json"}).ToArray())};
    File.WriteAllText(Path.Combine(cache,"index.json"),node.ToJsonString());
}
Run("r0");Run("r1");Put("q","raw.log","QUARANTINED_RAW_618");Put("q","manifest.json","{ invalid raw manifest");Index("r1","r1","r0");
Require((await Activate("target")).Code==0,"multi-run target activate");
var target=Read(Path.Combine(site,"site.json"))["generation"]!.GetValue<string>();
var metaPath=Path.Combine(site,"generations",target+".meta.json");var targetIndexPath=Path.Combine(site,"generations",target,"index.json");
var expectedTargetIndex=File.ReadAllBytes(targetIndexPath);var targetMeta=File.ReadAllBytes(metaPath);
Run("r2");Index("r2","r2");Require((await Activate("successor")).Code==0,"successor activate");
var pointer=File.ReadAllBytes(Path.Combine(site,"site.json"));var mirrorBytes=File.ReadAllBytes(mirror);
var storeFiles=Directory.GetFiles(site,"*",SearchOption.AllDirectories).Where(p=>p.Contains(Path.DirectorySeparatorChar+"generations"+Path.DirectorySeparatorChar)||p.Contains(Path.DirectorySeparatorChar+"data-store"+Path.DirectorySeparatorChar)).ToDictionary(p=>p,File.ReadAllBytes);
var sourceFiles=Directory.GetFiles(cache,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
using var host=new Process{StartInfo=new ProcessStartInfo("dotnet"){WorkingDirectory=repo,RedirectStandardOutput=true,RedirectStandardError=true}};
foreach(var argument in new[]{Path.Combine(repo,"web/host/bin/Release/net10.0/GmProgressHost.dll"),"--site",site,"--listen","127.0.0.1:"+port})host.StartInfo.ArgumentList.Add(argument);
host.Start();Console.WriteLine($"OWN_HOST pid={host.Id} port={port}");var hostOut=host.StandardOutput.ReadToEndAsync();var hostErr=host.StandardError.ReadToEndAsync();
using var http=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(10)};var checks=0;
try
{
    var ready=false;for(var i=0;i<100;i++){if(host.HasExited)throw new Exception("own host exited");try{using var h=await http.GetAsync("/healthz");if(h.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(100);}Require(ready,"own host readiness");
    var active=new Dictionary<string,byte[]>();foreach(var path in new[]{"/","/data/index.json","/data/runs/r2/manifest.json","/data/runs/r2/e.log","/data/runs/r2/m.json"})active[path]=await Get(path);
    await Reject("runs-empty",()=>Meta(m=>m["runs"]=new JsonObject()),"required run mapping missing");
    await Reject("runs-array",()=>Meta(m=>m["runs"]=new JsonArray()),"expected object");
    await Reject("missing-indexed-run",()=>Meta(m=>m["runs"]!.AsObject().Remove("r0")),"r0: required run mapping missing");
    await Reject("missing-cross-run",()=>Meta(m=>m["runs"]!.AsObject().Remove("q")),"q: required run mapping missing");
    await Reject("extra-run",()=>Meta(m=>m["runs"]!["unused"]=m["runs"]!["q"]!.DeepClone()),"mappings differ");
    await Reject("wrong-run-tree",()=>Meta(m=>m["runs"]!["r1"]=m["runs"]!["r0"]!.DeepClone()),"manifest run id mismatch");
    await Reject("tree-object-type",()=>File.WriteAllText(TreePath(),"[]"),"expected object");
    await Reject("tree-hash-identity",()=>{var t=Read(TreePath());t["treeHash"]=new string('0',64);File.WriteAllText(TreePath(),t.ToJsonString());},"tree record key mismatch");
    await Reject("tree-duplicate-path",()=>Tree(t=>t["files"]!.AsArray().Insert(1,t["files"]![0]!.DeepClone())),"unordered or duplicate");
    foreach(var file in new[]{"manifest.json","m.json","e.log"})await Reject("file-missing-"+file,()=>Tree(t=>t["files"]!.AsArray().Remove(t["files"]!.AsArray().Single(x=>x![0]!.GetValue<string>()==file))),"required file missing");
    await Reject("object-content-hash",()=>File.WriteAllText(ObjectPath("e.log"),"WRONG_BYTES"),"object content does not match key");
    await Reject("object-missing",()=>File.Delete(ObjectPath("e.log")),"generation closure:");
    await Reject("manifest-id",()=>Manifest(m=>m["run"]!["id"]="other"),"manifest run id mismatch");
    await Reject("manifest-commit",()=>Manifest(m=>m["run"]!["commit"]="other"),"manifest commit mismatch");
    await Reject("manifest-run-type",()=>Manifest(m=>m["run"]=new JsonArray()),"manifest run: expected object");
    await Reject("manifest-cases-type",()=>Manifest(m=>m["cases"]=new JsonObject()),"manifest cases: expected array");
    await Reject("manifest-tools-type",()=>Manifest(m=>m["cases"]![0]!["steps"]![0]!["gm"]!["tools"]=new JsonObject()),"tools: expected array");
    await Reject("cross-mesh-missing",()=>Manifest(m=>m["cases"]![0]!["steps"]![0]!["gm"]!["tools"]![1]="../r0/missing.json"),"required file missing");
    await Reject("manifest-reference-type",()=>Manifest(m=>m["cases"]![0]!["checks"]![0]!["evidence"]=new JsonObject()),"manifest reference: expected string");
    await Reject("generation-meta-id",()=>Meta(m=>m["id"]="gen-other"),"metadata id mismatch");
    await Reject("index-descriptor-run-binding",()=>IndexFault(i=>i["runs"]![0]!["manifest"]="runs/r0/manifest.json"),"manifest path does not bind indexed run");
    await Reject("generation-index-identity",()=>IndexFault(i=>i["generatedAt"]="changed"),"generation identity mismatch");
    await Reject("generation-tree-identity",()=>Manifest(m=>m["extra"]="different-valid-bytes"),"generation identity mismatch");
    Meta(m=>{var reordered=new JsonObject();foreach(var entry in m["runs"]!.AsObject().Reverse())reordered[entry.Key]=entry.Value!.DeepClone();m["runs"]=reordered;});
    var valid=await Rollback("valid-reordered-mapping");Require(valid.Code==0&&valid.Output.Contains("ROLLBACK_ACTIVATED"),"valid rollback failed");
    Require((await Get("/data/index.json")).AsSpan().SequenceEqual(expectedTargetIndex),"exact target index");
    foreach(var id in new[]{"r0","r1"})foreach(var file in new[]{"manifest.json","m.json","e.log"})Require((await Get($"/data/runs/{id}/{file}")).AsSpan().SequenceEqual(sourceFiles[Path.Combine(cache,"runs",id,file)]),"target HTTP bytes "+id+"/"+file);
    Require(Encoding.UTF8.GetString(await Get("/data/runs/q/raw.log"))=="QUARANTINED_RAW_618","raw cross-run positive");
    var restored=Read(Path.Combine(site,"site.json"));var mirrorNode=Read(mirror);
    Require(restored["generation"]!.GetValue<string>()==target&&restored["activationCount"]!.GetValue<int>()==3&&restored["previous"]!["dataRunId"]!.GetValue<string>()=="r2","restored identity/history");
    Require(mirrorNode["activationCount"]!.GetValue<int>()==3&&JsonNode.DeepEquals(mirrorNode["previous"],restored["previous"]),"restored mirror history");
    checks++;Console.WriteLine("PASS legal-multirun-reordered-map rollbackCLI=0 exact-index/7refs/history/mirror; invalid unindexed manifest not parsed");
    Require(sourceFiles.All(x=>File.ReadAllBytes(x.Key).AsSpan().SequenceEqual(x.Value)),"source unchanged");Console.WriteLine($"SUMMARY {checks}/{checks} passed source-unchanged=True");

    async Task Reject(string name,Action mutate,string reason)
    {
        try
        {
            mutate();var result=await Rollback(name);Require(result.Code==3&&result.Output.Contains("ACTIVATE_REFUSED_GENERATION_CORRUPT")&&result.Output.Contains(reason),name+" refusal mismatch: "+result.Output);
            Require(File.ReadAllBytes(Path.Combine(site,"site.json")).AsSpan().SequenceEqual(pointer)&&File.ReadAllBytes(mirror).AsSpan().SequenceEqual(mirrorBytes),name+" authority or mirror changed");
            foreach(var entry in active)Require((await Get(entry.Key)).AsSpan().SequenceEqual(entry.Value),name+" active HTTP bytes changed");
            checks++;Console.WriteLine($"PASS {name} rollbackCLI=3 reason={reason}; pointer/count/previous/mirror/5HTTPbytes unchanged");
        }
        finally{foreach(var path in Directory.GetFiles(site,"*",SearchOption.AllDirectories).Where(p=>p.Contains(Path.DirectorySeparatorChar+"generations"+Path.DirectorySeparatorChar)||p.Contains(Path.DirectorySeparatorChar+"data-store"+Path.DirectorySeparatorChar)))if(!storeFiles.ContainsKey(path))File.Delete(path);foreach(var entry in storeFiles){Directory.CreateDirectory(Path.GetDirectoryName(entry.Key)!);File.WriteAllBytes(entry.Key,entry.Value);}}
    }
}
finally{if(!host.HasExited){host.Kill(true);await host.WaitForExitAsync();}File.WriteAllText(Path.Combine(work,"host.log"),await hostOut+await hostErr);Console.WriteLine($"OWN_HOST_CLEANED pid={host.Id} exit={host.ExitCode}");}
void Meta(Action<JsonObject> mutate){var m=JsonNode.Parse(targetMeta)!.AsObject();mutate(m);File.WriteAllText(metaPath,m.ToJsonString());}
string TreePath(){var m=JsonNode.Parse(targetMeta)!;return Path.Combine(site,"data-store","trees",m["runs"]!["r1"]!.GetValue<string>()+".json");}
string ObjectPath(string name){var tree=Read(TreePath());return Path.Combine(site,"data-store","objects",tree["files"]!.AsArray().Single(p=>p![0]!.GetValue<string>()==name)![1]!.GetValue<string>());}
void Tree(Action<JsonObject> mutate)
{
    var tree=Read(TreePath());mutate(tree);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);foreach(var p in tree["files"]!.AsArray()){hash.AppendData(Encoding.UTF8.GetBytes(p![0]!.GetValue<string>()));hash.AppendData(Encoding.UTF8.GetBytes(p[1]!.GetValue<string>()));}
    var key=Convert.ToHexStringLower(hash.GetHashAndReset());tree["treeHash"]=key;File.WriteAllText(Path.Combine(site,"data-store","trees",key+".json"),tree.ToJsonString());Meta(m=>m["runs"]!["r1"]=key);
}
void Manifest(Action<JsonObject> mutate){var m=Read(ObjectPath("manifest.json"));mutate(m);var bytes=Encoding.UTF8.GetBytes(m.ToJsonString());var sha=Convert.ToHexStringLower(SHA256.HashData(bytes));File.WriteAllBytes(Path.Combine(site,"data-store","objects",sha),bytes);Tree(t=>t["files"]!.AsArray().Single(p=>p![0]!.GetValue<string>()=="manifest.json")![1]=sha);}
void IndexFault(Action<JsonObject> mutate){var i=JsonNode.Parse(expectedTargetIndex)!.AsObject();mutate(i);var bytes=Encoding.UTF8.GetBytes(i.ToJsonString());File.WriteAllBytes(targetIndexPath,bytes);Meta(m=>m["indexSha256"]=Convert.ToHexStringLower(SHA256.HashData(bytes)));}
Task<(ExitCode Code,string Output)> Activate(string name)=>Cli(name,"ActivateHostedWebProgressSite.cs","--site",site,"--data",cache,"--state",mirror,"--release",release);
Task<(ExitCode Code,string Output)> Rollback(string name)=>Cli(name,"ActivateHostedWebProgressSite.cs","--site",site,"--data",cache,"--state",mirror,"--rollback");
async Task<(ExitCode Code,string Output)> Cli(string name,string script,params string[] options)
{
    using var p=new Process{StartInfo=new ProcessStartInfo("dotnet"){WorkingDirectory=repo,RedirectStandardOutput=true,RedirectStandardError=true}};foreach(var a in new[]{"run","--no-cache","--file",Path.Combine(repo,"scripts",script),"--"}.Concat(options))p.StartInfo.ArgumentList.Add(a);p.StartInfo.Environment["MSBUILDDISABLENODEREUSE"]="1";p.Start();var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();await p.WaitForExitAsync();var output=await stdout+await stderr;File.WriteAllText(Path.Combine(work,name+".cli.log"),"exit="+p.ExitCode+"\n"+output);return (p.ExitCode,output);
}
async Task<byte[]> Get(string path){using var response=await http.GetAsync(path);Require(response.StatusCode==HttpStatusCode.OK,"HTTP "+path+" status="+(int)response.StatusCode);return await response.Content.ReadAsByteArrayAsync();}
static JsonObject Read(string path)=>JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
static string Source([CallerFilePath]string path="")=>path;
