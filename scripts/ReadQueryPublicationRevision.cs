#!/usr/bin/env dotnet run
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

if(args.Length!=1||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("new narrow snapshot name required");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));var output=Path.Combine(root,"temp_docs","query-numerics-closure",args[0]);
if(Directory.Exists(output))throw new InvalidOperationException("Readback snapshots are immutable");Directory.CreateDirectory(output);
const string current="20261112T072500Z-queryab597",previous="20261112T132500Z-queryab597",calculation="ab597e8a24b2ef23753507c8ca3e43d12edcc650";
var runRoot="data/runs/"+current+"/";var portal=new Uri("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/");
using var handler=new HttpClientHandler{UseProxy=false};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(30)};
var reads=new JsonArray();var checks=new JsonArray();var meshBindings=new JsonArray();var archivedFiles=new JsonArray();var completed=false;
var first=Path.Combine(root,"temp_docs","query-numerics-closure","online-readback-v1");
try
{
    var manifestBytes=await Fetch(runRoot+"manifest.json",200);var manifest=JsonNode.Parse(manifestBytes)!;
    Check(Hash(manifestBytes)=="72822fa82b97dbdfcad89a1eeb24ca7d3ef18be2a82881180773450f3bc222df"&&manifestBytes.Length==25730,"fixed corrected manifest bytes/hash");
    var index=JsonNode.Parse(await Fetch("data/index.json",200))!;var overview=JsonNode.Parse(await Fetch("data/project-overview.json",200))!;
    var priorIndex=JsonNode.Parse(File.ReadAllBytes(Path.Combine(first,"data_index.json")))!;
    Check(index["runs"]!.AsArray().Any(x=>x!["id"]!.GetValue<string>()==current&&x["commit"]!.GetValue<string>()==calculation),"corrected run indexed with source");
    Check(!index["runs"]!.AsArray().Any(x=>x!["id"]!.GetValue<string>()==previous),"only erroneous run removed from active index");
    foreach(var old in priorIndex["runs"]!.AsArray().Where(x=>x!["id"]!.GetValue<string>()!=previous))
        Check(JsonNode.DeepEquals(old,index["runs"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()==old!["id"]!.GetValue<string>())) ,"other indexed run preserved "+old!["id"]);
    var p=manifest["run"]!["provenance"]!;
    Check(p["calculationCommit"]!.GetValue<string>()==calculation&&p["deliveryCommit"]!.GetValue<string>()=="f633b51db703df308cf4ac194e3c2b1661398d4d"&&p["reviewCommit"]!.GetValue<string>()=="3e7ac1b1273c56530bfbd50f0cb6bdffb08e4fd7","calculation/delivery/Native-review sources unchanged");
    var capture=JsonNode.Parse(File.ReadAllBytes(Path.Combine(root,"temp_docs","query-numerics-closure","final-capture-axial","capture.json")))!;
    foreach(var c in manifest["cases"]!.AsArray().Where(x=>x!["id"]!.GetValue<string>().StartsWith("local-blend.",StringComparison.Ordinal)))
    {
        var name=c!["id"]!.GetValue<string>()["local-blend.".Length..];var step=capture["steps"]!.AsArray().Single(x=>x!["title"]!.GetValue<string>().StartsWith(name+" /",StringComparison.Ordinal))!;
        var mark=step["gm"]!["apiParameters"]!["mark"]!;var operation=step["gm"]!["apiParameters"]!["operation"]!;
        foreach(var key in name.Contains("chamfer",StringComparison.Ordinal)?new[]{"radius","range0","range1","tolerance"}:new[]{"radius","tolerance"})
        {
            Check(JsonNode.DeepEquals(c["parameters"]![key],mark[key]),name+" actual "+key);
            foreach(var side in new[]{"gm","pk"})Check(JsonNode.DeepEquals(c["apis"]![side]!["parameters"]![key],mark[key]),name+" "+side+" actual "+key);
        }
        Check(JsonNode.DeepEquals(c["parameters"]!["markApi"],mark["name"])&&JsonNode.DeepEquals(c["parameters"]!["fixApi"],operation["name"]),name+" mark/fix separated");
    }
    JsonNode Case(string id)=>manifest["cases"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()==id)!;
    var probes=Case("query.probes-510-classification");
    Check(probes["status"]!.GetValue<string>()=="diagnostic"&&!probes["performance"]!["correctnessQualified"]!.GetValue<bool>(),"510 not green/qualified");
    Check(probes["source"]!["path"]!.GetValue<string>()=="scripts/ShortEdgeBlendOracle.cs","510 actual producer");
    Check(probes["parameters"]!["unresolvedDirectCount"]!.GetValue<int>()==15&&probes["parameters"]!["unresolvedImportCount"]!.GetValue<int>()==16,"510 direct unresolved 15/16 retained");
    Check(probes["checks"]!.AsArray().Where(x=>x!["id"]!.GetValue<string>()!="510-controls").All(x=>x!["status"]!.GetValue<string>()=="diagnostic"),"510 check qualification corrected");
    var mass=Case("mass.norm-reassessment-joint");
    Check(mass["source"]!["path"]!.GetValue<string>()=="scripts/ReviewMassCertification.cs"&&mass["source"]!["revision"]!.GetValue<string>()==calculation,"Joint actual reassessment source");
    Check(mass["operation"]!.GetValue<string>()=="reassess-sealed-mass-reports"&&mass["apis"]!["gm"]!["name"]!.GetValue<string>()=="read-only sealed reports reassessment"&&mass["performance"]!["note"]!.GetValue<string>().Contains("无新双边积分执行",StringComparison.Ordinal),"Joint read-only execution identity");
    var suite=Case("suite.linux-managed-2025");
    Check(suite["apis"]!["pk"]!["name"]!.GetValue<string>()=="not-applicable"&&suite["checks"]!.AsArray().All(x=>x!["pk"]==null),"suite PK not executed; no synthetic PK count");
    Check(suite["apis"]!["gm"]!["parameters"]!["passed"]!.GetValue<int>()==2025,"original GM suite count preserved");
    var faq=overview["verificationRecords"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()=="mass.faq-geometry-vs-mass")!;
    Check(!faq["note"]!.GetValue<string>().Contains("局部逼近",StringComparison.Ordinal)&&!faq["note"]!.GetValue<string>().Contains("1e-8/1e-5",StringComparison.Ordinal),"unsupported FAQ precision range removed");
    Check(overview["catalog"]!.AsArray().Count==463&&overview["summary"]!["query:pass"]!.GetValue<int>()==5,"whole catalog and five query passes retained");
    Check(overview["currentVersion"]!["queryNumerics"]!.GetValue<string>()==calculation&&overview["currentVersion"]!["label"]!.GetValue<string>().Contains("分源",StringComparison.Ordinal),"query/norm source remains separate");
    var wire=await Fetch(runRoot+"evidence/results.gz.json",200);var oldWire=File.ReadAllBytes(Path.Combine(first,"data_runs_"+previous+"_evidence_results.gz.json"));
    Check(wire.AsSpan().SequenceEqual(oldWire)&&Hash(wire)=="3f29e8dc9965978bbef9e7e0b7db1b509e4b6e119b9ebbd653368e91a29db270","corrected live gzip equals original actual HTTPS bytes");
    using(var stream=new GZipStream(new MemoryStream(wire),CompressionMode.Decompress))using(var decoded=new MemoryStream())
    {await stream.CopyToAsync(decoded);var raw=decoded.ToArray();var expected=File.ReadAllBytes(Path.Combine(root,"docs","query-numerics-closure","results.json"));Check(raw.AsSpan().SequenceEqual(expected),"decoded original payload unchanged");}
    await Fetch("data/runs/"+previous+"/manifest.json",404);
    var publishRoot=Path.GetFullPath(Path.Combine(root,"../../../../code/project_gm_kernel-progress-publisher/temp_docs/web-progress-publish"));
    var archive=Path.Combine(publishRoot,"archived-runs",previous);var archiveManifest=JsonNode.Parse(File.ReadAllBytes(Path.Combine(archive,"archive-manifest.json")))!;
    foreach(var file in archiveManifest["files"]!.AsArray())
    {
        var relative=file!["path"]!.GetValue<string>();var raw=File.ReadAllBytes(Path.Combine(archive,relative));
        Check(raw.Length==file["bytes"]!.GetValue<int>()&&Hash(raw)==file["sha256"]!.GetValue<string>(),"archived original "+relative);
        archivedFiles.Add(file.DeepClone());
        if(relative.StartsWith("meshes/",StringComparison.Ordinal))
        {
            var staged=File.ReadAllBytes(Path.Combine(publishRoot,"data","runs",current,relative));var priorLive=File.ReadAllBytes(Path.Combine(first,"data_runs_"+previous+"_"+relative.Replace('/','_')));
            Check(staged.AsSpan().SequenceEqual(raw)&&priorLive.AsSpan().SequenceEqual(raw),"mesh previous HTTP/archive/corrected stage hash chain "+relative);
            meshBindings.Add((JsonNode)new JsonObject{["path"]=relative,["bytes"]=raw.Length,["sha256"]=Hash(raw),["scope"]="previous actual HTTP bytes == immutable archive == corrected publication source; not refetched over HTTP"});
        }
    }
    Check(archivedFiles.Count==19&&meshBindings.Count==12,"complete erroneous metadata archive and 12 model sources retained");
    Check(Hash(File.ReadAllBytes(Path.Combine(archive,"manifest.json")))=="e8bb5c626e9e2154c24e5c53ac6238b8b0551966817d9be2ce63e313a777d742","original incorrect manifest retained byte-for-byte");
    completed=true;Console.WriteLine($"NARROW_HTTPS_READBACK reads={reads.Count} checks={checks.Count} issues=0 archiveFiles={archivedFiles.Count} unchangedMeshSources={meshBindings.Count}");
}
finally
{
    File.WriteAllText(Path.Combine(output,"receipt.json"),new JsonObject{["runId"]=current,["publicationCommit"]="4e5b97254f29668b8d736f41c92143b1a0783a1e",["readOnly"]=true,["noModelingTestsOrUiReview"]=true,["accepted"]=completed,
        ["sourceSha256"]=Hash(File.ReadAllBytes(Source())),["reads"]=reads,["checks"]=checks,["archivedFiles"]=archivedFiles,["unchangedMeshSourceBindings"]=meshBindings,["previousReadbackRawExit"]=2}.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
}
async Task<byte[]> Fetch(string path,int status)
{
    using var response=await client.GetAsync(new Uri(portal,path));var bytes=await response.Content.ReadAsByteArrayAsync();var saved=path.Replace('/','_');File.WriteAllBytes(Path.Combine(output,saved),bytes);
    reads.Add((JsonNode)new JsonObject{["url"]=new Uri(portal,path).ToString(),["utc"]=DateTimeOffset.UtcNow.ToString("O"),["status"]=(int)response.StatusCode,["bytes"]=bytes.Length,["sha256"]=Hash(bytes),["saved"]=saved});
    Check((int)response.StatusCode==status,"actual HTTP "+status+" "+path);return bytes;
}
void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add((JsonNode?)JsonValue.Create(label));}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
