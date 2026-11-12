#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

if(args.Length!=1||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("one new readback snapshot name required");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var output=Path.Combine(root,"temp_docs","query-numerics-closure",args[0]);if(Directory.Exists(output))throw new InvalidOperationException("Readback evidence is immutable");Directory.CreateDirectory(output);
var portal=new Uri("https://qingyu1-linux.tail77e06.ts.net/apps/gm-kernel-progress/");
const string runId="20261112T132500Z-queryab597";const string calculation="ab597e8a24b2ef23753507c8ca3e43d12edcc650";
var runRoot="data/runs/"+runId+"/";var reads=new JsonArray();var checks=new JsonArray();var issues=new JsonArray();var snapshots=new Dictionary<string,byte[]>(StringComparer.Ordinal);
using var handler=new HttpClientHandler{UseProxy=false};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(30)};
try
{
    var index=await Json("data/index.json");var overview=await Json("data/project-overview.json");var events=await Json("data/overview-events.json");var manifest=await Json(runRoot+"manifest.json");
    Check(index["runs"]!.AsArray().Any(x=>x!["id"]!.GetValue<string>()==runId&&x["commit"]!.GetValue<string>()==calculation),"new run indexed with calculation source");
    var provenance=manifest["run"]!["provenance"]!;
    Check(provenance["calculationCommit"]!.GetValue<string>()==calculation,"calculation source");
    Check(provenance["deliveryCommit"]!.GetValue<string>()=="f633b51db703df308cf4ac194e3c2b1661398d4d","delivery source");
    Check(provenance["reviewCommit"]!.GetValue<string>()=="3e7ac1b1273c56530bfbd50f0cb6bdffb08e4fd7","independent Native/math review");
    var resources=new SortedSet<string>(StringComparer.Ordinal);
    foreach(var c in manifest["cases"]!.AsArray())
    {
        foreach(var evidence in c!["evidence"]!.AsArray())resources.Add(runRoot+evidence!["path"]!.GetValue<string>());
        foreach(var check in c["checks"]!.AsArray())if(check!["evidence"] is JsonValue path)resources.Add(runRoot+path.GetValue<string>());
        foreach(var step in c["steps"]!.AsArray())foreach(var side in new[]{"gm","pk"})foreach(var stage in new[]{"before","after"})
            if(step![side]?[stage] is JsonValue path)resources.Add(runRoot+path.GetValue<string>());
    }
    foreach(var resource in resources)await Fetch(resource);
    var compressed=snapshots[runRoot+"evidence/results.gz.json"];
    Check(compressed.Length==provenance["compressedResultsBytes"]!.GetValue<int>()&&Hash(compressed)==provenance["compressedResultsSha256"]!.GetValue<string>(),"gzip wire hash/size match actual manifest");
    Check(compressed[0]==0x1f&&compressed[1]==0x8b,"gzip content magic independent of json extension");
    using var input=new MemoryStream(compressed);using var gzip=new GZipStream(input,CompressionMode.Decompress);using var decoded=new MemoryStream();await gzip.CopyToAsync(decoded);var raw=decoded.ToArray();
    var local=File.ReadAllBytes(Path.Combine(root,"docs","query-numerics-closure","results.json"));
    Check(raw.AsSpan().SequenceEqual(local)&&raw.Length==provenance["resultsBytes"]!.GetValue<int>()&&Hash(raw)==provenance["resultsSha256"]!.GetValue<string>(),"decoded full original results equal local bytes");
    File.WriteAllBytes(Path.Combine(output,"decoded-original-results.json"),raw);var original=JsonNode.Parse(raw)!;
    var axial=await Json(runRoot+"evidence/five-axial-queries.json");
    Check(JsonNode.DeepEquals(axial["points"],original["fixedOriginal20"]!["points"])&&JsonNode.DeepEquals(axial["sources"],original["fixedOriginal20"]!["sources"]),"all original 20 points and input sources retained");
    var query=await Json(runRoot+"evidence/query-510-summary.json");
    foreach(var r in query["runs"]!.AsArray())
    {
        var source=original["fixed510Runs"]!.AsArray().Single(x=>x!["run"]!.GetValue<string>()==r!["run"]!.GetValue<string>())!;
        foreach(var pair in new[]{("captureEnabled","captureEnabled"),("verifiedCount","rawFourWayVerified"),("unresolvedCount","rawFourWayUnresolved"),("differentCount","rawFourWayDifferent"),("nativeRoundtripDifferentRows","nativeRoundtripDifferentRows"),("gmFiniteBoundaryMemberships","provenGmFiniteBoundaryMemberships")})
            Check(JsonNode.DeepEquals(r![pair.Item1],source[pair.Item2]),"510 summary "+r!["run"]+" "+pair.Item1);
    }
    var mass=await Json(runRoot+"evidence/mass-reassessment-summary.json");
    Check(JsonNode.DeepEquals(mass["summary"],original["jointMassStandardsReassessment"]!["summary"])&&JsonNode.DeepEquals(mass["contract"],original["jointMassStandardsReassessment"]!["currentContract"]),"mass norm and all Joint before/after counts retain source");
    var catalog=overview["catalog"]!.AsArray();Check(catalog.Count==463&&catalog.Select(x=>x!["id"]!.GetValue<string>()).Distinct().Count()==463,"whole catalog 463 unique entries");
    var localCapture=JsonNode.Parse(File.ReadAllBytes(Path.Combine(root,"temp_docs","query-numerics-closure","final-capture-axial","capture.json")))!;
    foreach(var name in new[]{"cylinder-rim-fillet","cylinder-both-rim-fillet","cylinder-rim-chamfer","cylinder-both-rim-chamfer","cylinder-unequal-chamfer"})
    {
        var id="local-blend."+name;var row=catalog.Single(x=>x!["id"]!.GetValue<string>()==id)!;var detail=manifest["cases"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()==id)!;
        Check(row["dimensions"]!["query"]!["status"]!.GetValue<string>()=="pass"&&row["dimensions"]!["query"]!["lastVerified"]!["commit"]!.GetValue<string>()==calculation,id+" current query pass/source");
        Check(row["history"]!.AsArray().Any(x=>x!["status"]!.GetValue<string>()=="fail"&&x["note"]!.GetValue<string>().Contains("5000",StringComparison.Ordinal)),id+" historical 5000 preserved");
        var actual=localCapture["steps"]!.AsArray().Single(x=>x!["title"]!.GetValue<string>().StartsWith(name+" /",StringComparison.Ordinal))!;
        var mark=actual["gm"]!["apiParameters"]!["mark"]!;
        if(detail["parameters"]?["radius"] is JsonValue radius)
            Soft(JsonNode.DeepEquals(radius,mark["radius"]),id+" published radius="+radius+" differs from actual mark radius="+(mark["radius"]?.ToJsonString()??"null")+", range0="+mark["range0"]+", range1="+mark["range1"]);
        foreach(var step in detail["steps"]!.AsArray())foreach(var side in new[]{"gm","pk"})foreach(var stage in new[]{"before","after"})
        {
            var path=step![side]![stage]!.GetValue<string>();var remote=JsonNode.Parse(snapshots[runRoot+path]);var source=JsonNode.Parse(File.ReadAllBytes(Path.Combine(root,"temp_docs","query-numerics-closure","final-capture-axial",path)));
            Check(JsonNode.DeepEquals(remote,source),id+" actual "+side+" "+stage+" mesh/source JSON");
        }
    }
    Check(overview["currentVersion"]!["queryNumerics"]!.GetValue<string>()==calculation&&overview["currentVersion"]!["performanceProjection"]!.GetValue<string>()=="cc47871133fb7d536ffa7fd050066c51f4905708"&&overview["currentVersion"]!["label"]!.GetValue<string>().Contains("分源",StringComparison.Ordinal),"Projection and query sources remain separate");
    foreach(var id in new[]{"boundary.probes-510"})Check(overview["boundaryRegisters"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()==id)!["status"]!.GetValue<string>()=="unresolved","overview unresolved "+id);
    var probeCase=manifest["cases"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()=="query.probes-510-classification")!;
    Soft(probeCase["status"]!.GetValue<string>()!="pass"&&probeCase["performance"]!["correctnessQualified"]!.GetValue<bool>()==false,"run-detail 510 case is pass/correctnessQualified=true although original direct 15/16 U remain; overview correctly says unresolved");
    Soft(probeCase["source"]!["path"]!.GetValue<string>()=="scripts/ShortEdgeBlendOracle.cs","510 source path is "+probeCase["source"]!["path"]+" instead of the actual ShortEdgeBlendOracle producer");
    foreach(var c in manifest["cases"]!.AsArray())
    {
        var revision=c!["source"]!["revision"]!.GetValue<string>();var path=c["source"]!["path"]!.GetValue<string>();
        Soft(GitExists(revision,path),c["id"]+" source path "+path+" does not exist at claimed revision "+revision);
    }
    var suite=manifest["cases"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()=="suite.linux-managed-2025")!;
    Soft(suite["apis"]?["pk"]?["name"]?.GetValue<string>()!="dotnet test","managed suite detail claims a paired PK dotnet-test API/count; original is one GM managed suite, and expected counts must be labelled as such");
    var projection=index["runs"]!.AsArray().Single(x=>x!["id"]!.GetValue<string>()=="20261112T045837Z-projcc47871")!;
    await Json("data/"+projection["manifest"]!.GetValue<string>());
    var indexAgain=await client.GetByteArrayAsync(new Uri(portal,"data/index.json"));Check(indexAgain.AsSpan().SequenceEqual(snapshots["data/index.json"]),"index unchanged throughout snapshot");
    Console.WriteLine($"HTTPS_READBACK reads={reads.Count} checks={checks.Count} issues={issues.Count} fullOriginalSha={Hash(raw)}");if(issues.Count!=0)Environment.ExitCode=2;
}
finally
{
    File.WriteAllText(Path.Combine(output,"receipt.json"),new JsonObject{["runId"]=runId,["readOnly"]=true,["noModelingOrTestsExecuted"]=true,["sourceSha256"]=Hash(File.ReadAllBytes(Source())),["reads"]=reads,["checks"]=checks,["issues"]=issues}.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
}
async Task<byte[]> Fetch(string path)
{
    if(snapshots.TryGetValue(path,out var cached))return cached;var uri=new Uri(portal,path);if(uri.Host!=portal.Host||!uri.AbsolutePath.StartsWith(portal.AbsolutePath,StringComparison.Ordinal))throw new InvalidOperationException("External resource not allowed");
    using var response=await client.GetAsync(uri);var bytes=await response.Content.ReadAsByteArrayAsync();
    var saved=path.Replace('/','_');File.WriteAllBytes(Path.Combine(output,saved),bytes);reads.Add((JsonNode)new JsonObject{["url"]=uri.ToString(),["utc"]=DateTimeOffset.UtcNow.ToString("O"),["status"]=(int)response.StatusCode,["bytes"]=bytes.Length,["sha256"]=Hash(bytes),["saved"]=saved});
    response.EnsureSuccessStatusCode();snapshots.Add(path,bytes);return bytes;
}
async Task<JsonNode> Json(string path)=>JsonNode.Parse(await Fetch(path))!;
void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks.Add((JsonNode?)JsonValue.Create(label));}
void Soft(bool condition,string detail){if(!condition)issues.Add((JsonNode?)JsonValue.Create(detail));}
bool GitExists(string revision,string path){var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in new[]{"cat-file","-e",revision+":"+path})start.ArgumentList.Add(a);using var process=Process.Start(start)!;process.WaitForExit();return process.ExitCode==0;}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
