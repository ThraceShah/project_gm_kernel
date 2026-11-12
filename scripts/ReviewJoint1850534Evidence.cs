#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
Directory.SetCurrentDirectory(root);
const string target="1850534979379127c8452e85ec2682a86470d96e";
const string package="temp_docs/incoming/mac-joint-1850534-quality.zip";
const string output="temp_docs/independent-joint-1850534";
const string original=output+"/original";
if(args.SequenceEqual(new[]{"--owner"}))
{
    var folder=original+"/temp_docs/joint-work/owner-trace-original";var expected=JsonNode.Parse(File.ReadAllText(folder+"/owner-key-analysis.json"))!;var result=new JsonArray();
    foreach(var item in expected["results"]!.AsArray())
    {
        var name=(string)item!["pidFile"]!;var stem=folder+"/owner-keys/"+name[..^"-calls.jsonl".Length];var definitions=File.ReadLines(stem+"-definitions.jsonl").Select(x=>JsonNode.Parse(x)!).ToDictionary(x=>(string)x["sha256"]!,x=>x["value"]!);var source=JsonNode.Parse(File.ReadAllText(stem+"-source.json"))!;
        var rows=File.ReadLines(stem+"-calls.jsonl").Select(x=>JsonNode.Parse(x)!).ToArray();var tubes=rows.Where(x=>(bool)x["immutable"]!&&x["tube"]!=null&&(int)definitions[(string)x["definition"]!]["owner"]!["Tag"]!>0).ToArray();
        var plain=tubes.Where(x=>new[]{"curvature","support","capture","anchors","acceleration","captureCharts"}.All(k=>(int)x["needs"]![k]! ==0)&&new[]{"reachBudget","require","requireCharts"}.All(k=>x["needs"]![k]!.AsArray().Count==0)).ToArray();Require(plain.All(x=>(int)x["needs"]!["rootChart"]! <0),"ordinary trace is not a fixed-gauge request");
        var fullDemand=OwnerStats(tubes,false,definitions);var projected=OwnerStats(plain,true,definitions);Require(JsonNode.DeepEquals(fullDemand,item["fullDemand"])&&JsonNode.DeepEquals(projected,item["plainUTubeDemand"]),"owner grouping recomputation");
        result.Add((JsonNode)new JsonObject{["case"]=item["case"]!.DeepClone(),["pid"]=source["pid"]!.DeepClone(),["source"]=source["source"]!.DeepClone(),["nativeMvid"]=source["nativeMvid"]!.DeepClone(),["calls"]=rows.Length,["ownerKnownSuccessfulTubes"]=tubes.Length,["ordinaryCalls"]=plain.Length,["fullDemand"]=fullDemand,["ordinaryUDemand"]=projected});
    }
    Require(!File.Exists(output+"/owner-verified.json"),"preserve owner audit");File.WriteAllText(output+"/owner-verified.json",result.ToJsonString());Console.WriteLine(result.ToJsonString());return;
}
if(args.SequenceEqual(new[]{"--receipt"}))
{
    var evidence=JsonNode.Parse(File.ReadAllText(output+"/evidence-receipt-verified.json"))!;evidence.AsObject().Remove("members");
    var measurement=JsonNode.Parse(File.ReadAllText(output+"/measurement/receipt.json"))!;measurement.AsObject().Remove("samples");measurement.AsObject().Remove("binaries");measurement["workErratumAudit"]=JsonNode.Parse(File.ReadAllText(output+"/measurement/erratum-receipt.json"));
    var controls=JsonNode.Parse(File.ReadAllText(output+"/controls-receipt-sealed.json"))!;evidence["independentOwnerGrouping"]=JsonNode.Parse(File.ReadAllText(output+"/owner-verified.json"));
    var q2folder=output+"/q2-one-curved/00-curved-candidate";var q2capture=JsonNode.Parse(File.ReadAllText(q2folder+"/capture.json"))!;var q2id=JsonNode.Parse(File.ReadAllText(q2folder+"/identity/loaded.json"))!;
    Require(Hash(File.ReadAllBytes(q2folder+"/identity/kernel.dll"))==(string?)q2id["kernelDllSha256"],"q2 loaded archive");
    var q2states=new JsonObject();foreach(var group in q2capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!))q2states[group.Key]=group.Count();
    var q2trx=XDocument.Load(output+"/q2-tests/focused.trx");XNamespace testns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var testcounts=q2trx.Descendants(testns+"Counters").Single();
    var document=new JsonObject{["scope"]="Independent review derivation, original bytes retained separately; accepted scope in accompanying Markdown",["qualityEvidence"]=evidence,["controls"]=controls,["measurement"]=measurement,["q2FocusedTests"]=new JsonObject{["total"]=(string?)testcounts.Attribute("total"),["passed"]=(string?)testcounts.Attribute("passed"),["failed"]=(string?)testcounts.Attribute("failed"),["trxSha256"]=Hash(File.ReadAllBytes(output+"/q2-tests/focused.trx"))},["q2CurrentCurvedOperation"]=new JsonObject{["originalExit"]=int.Parse(File.ReadAllText(q2folder+"/process.exit")),["captureSha256"]=Hash(File.ReadAllBytes(q2folder+"/capture.json")),["checks"]=q2states,["captureErrors"]=q2capture["captureErrors"]!.DeepClone(),["timings"]=q2capture["timings"]!.DeepClone(),["loadedIdentity"]=q2id}};
    const string destination="docs/reviews/advanced-modeling/joint-1850534-independent-receipt.json";Require(!File.Exists(destination),"preserve published receipt");File.WriteAllText(destination,document.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Independent aggregate receipt written");return;
}
var recheck=args.SequenceEqual(new[]{"--recheck"});
if(Directory.Exists(output)&&!recheck)throw new Exception("Review output already exists; preserve evidence");
Directory.CreateDirectory(original);
var zipHash=Hash(File.ReadAllBytes(package));
Require(zipHash=="6c7577890434800c19cd0da32cdf5f8db89ebf30f8c1cda83817380ce3b1f5a2","archive hash");
using var zip=ZipFile.OpenRead(package);
var members=new JsonArray();long bytes=0;
var paths=new HashSet<string>(StringComparer.Ordinal);
foreach(var member in zip.Entries)
{
    var name=member.FullName;
    Require(!Path.IsPathRooted(name)&&!name.Contains('\\')&&!name.Split('/').Any(x=>x=="..")&&paths.Add(name),"archive path");
    Require(((member.ExternalAttributes>>16)&0xf000)!=0xa000,"archive symlink");
    if(name.EndsWith('/'))continue;
    var path=Path.Combine(original,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    if(!recheck){using(var input=member.Open())using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))input.CopyTo(file);}
    using var check=member.Open();var hash=Convert.ToHexStringLower(SHA256.HashData(check));
    Require(hash==Hash(File.ReadAllBytes(path)),"extracted bytes");bytes+=member.Length;
    members.Add((JsonNode)new JsonObject{["path"]=name,["length"]=member.Length,["sha256"]=hash});
}
Require(members.Count==665&&bytes==139701335,"archive dimensions");
var publicFiles=new[]{"docs/Joint_Work_Reduction.md","docs/joint-work/quality-receipt.json","docs/joint-work/source-binary-binding.json"};
foreach(var file in publicFiles)Require(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(Path.Combine(original,file))),"public receipt bytes");
using var binding=JsonDocument.Parse(File.ReadAllBytes(publicFiles[2]));
var binaries=new JsonArray();
foreach(var entry in binding.RootElement.GetProperty("binaries").EnumerateArray())
{
    var dll=entry.GetProperty("dll").GetString()!;var pdb=entry.GetProperty("pdb").GetString()!;
    var db=File.ReadAllBytes(Path.Combine(original,dll));var pb=File.ReadAllBytes(Path.Combine(original,pdb));
    Require(Hash(db)==entry.GetProperty("dllSha256").GetString()&&Hash(pb)==entry.GetProperty("pdbSha256").GetString(),"binary hashes");
    using var pe=new PEReader(new MemoryStream(db));var metadata=pe.GetMetadataReader();
    var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid);Require(mvid.ToString()==entry.GetProperty("mvid").GetString(),"MVID");
    using var provider=MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(pb));var reader=provider.GetMetadataReader();
    var id=reader.DebugMetadataHeader!.Id;var guid=new Guid(id.AsSpan(0,16));
    var codeView=pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView);
    Require(pe.ReadCodeViewDebugDirectoryData(codeView).Guid==guid,"PE/PDB link");
    var matched=0;var unmapped=new List<string>();
    foreach(var handle in reader.Documents)
    {
        var document=reader.GetDocument(handle);var name=reader.GetString(document.Name).Replace('\\','/');
        var start=name.IndexOf("/src/",StringComparison.Ordinal);if(start<0)start=name.IndexOf("/tests/",StringComparison.Ordinal);
        var relative=start>=0?name[(start+1)..]:name;
        if(relative.Contains("/obj/",StringComparison.Ordinal)||!(relative.StartsWith("src/")||relative.StartsWith("tests/")))
        {unmapped.Add(Path.GetFileName(name));continue;}
        var source=Git("show",target+":"+relative);
        Require(reader.GetGuid(document.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16"),"PDB SHA256 algorithm");
        Require(SHA256.HashData(source).SequenceEqual(reader.GetBlobBytes(document.Hash)),"PDB tracked source "+relative);matched++;
    }
    Require(matched==entry.GetProperty("matchedTrackedDocuments").GetInt32(),"document count");
    binaries.Add((JsonNode)new JsonObject{["dll"]=dll,["sha256"]=Hash(db),["mvid"]=mvid.ToString(),["codeViewGuid"]=guid.ToString(),["matchedTrackedDocuments"]=matched,["unmapped"]=new JsonArray(unmapped.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray())});
}
using var quality=JsonDocument.Parse(File.ReadAllBytes(publicFiles[1]));
var runs=new JsonArray();
var artifacts=0;var manifests=0;var sourceRows=0;var compareReports=0;var massReports=0;var components=new Dictionary<string,int>();
foreach(var run in quality.RootElement.GetProperty("qualityRuns").EnumerateArray())
{
    var directory=run.GetProperty("directory").GetString()!;var folder=Path.Combine(original,"temp_docs/joint-work",directory);var capturePath=Path.Combine(folder,"capture.json");
    Require(Hash(File.ReadAllBytes(capturePath))==run.GetProperty("captureSha256").GetString(),"capture hash");
    using var capture=JsonDocument.Parse(File.ReadAllBytes(capturePath));
    var exit=directory.StartsWith("whole-",StringComparison.Ordinal)?Path.Combine(folder,"process.exit"):Path.Combine(original,"temp_docs/joint-work",directory[..^8]+".exit");
    var raw=int.Parse(File.ReadAllText(exit));Require(raw==run.GetProperty("originalExit").GetInt32(),"original raw exit");
    Require(capture.RootElement.GetProperty("captureErrors").GetArrayLength()==0,"capture errors");
    var counts=capture.RootElement.GetProperty("checks").EnumerateArray().GroupBy(x=>x.GetProperty("status").GetString()!).ToDictionary(x=>x.Key,x=>x.Count());
    foreach(var count in counts)Require(run.GetProperty("checks").GetProperty(count.Key).GetInt32()==count.Value,"check counts");
    using var loaded=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"identity/loaded.json")));
    Require(loaded.RootElement.GetProperty("calculationCommit").GetString()==target,"loaded source");
    Require(loaded.RootElement.GetProperty("kernelDllSha256").GetString()==Hash(File.ReadAllBytes(Path.Combine(folder,"identity/kernel.dll"))),"loaded kernel DLL");
    Require(loaded.RootElement.GetProperty("loadedKernelAssemblySha256").GetString()==loaded.RootElement.GetProperty("kernelDllSha256").GetString(),"actual loaded kernel");
    Require(loaded.RootElement.GetProperty("oracleDllSha256").GetString()==Hash(File.ReadAllBytes(Path.Combine(folder,"identity/oracle.dll"))),"loaded oracle DLL");
    foreach(var meshPath in Directory.GetFiles(Path.Combine(folder,"meshes"),"*.json"))
    {
        using var mesh=JsonDocument.Parse(File.ReadAllBytes(meshPath));var source=mesh.RootElement.GetProperty("source");
        if(!source.TryGetProperty("xtArtifact",out var artifact))continue;
        var xt=Path.Combine(folder,artifact.GetString()!);Require(Hash(File.ReadAllBytes(xt))==source.GetProperty("xtSha256").GetString(),"actual product bytes");
        Require(source.GetProperty("commit").GetString()==target,"source product commit");artifacts++;
    }
    var manifest=Path.Combine(folder,"source.sha256");
    if(File.Exists(manifest))
    {
        manifests++;foreach(var line in File.ReadLines(manifest))
        {if(string.IsNullOrWhiteSpace(line))continue;var path=line[66..];Require(Hash(Git("show",target+":"+path))==line[..64],"source manifest "+path);sourceRows++;}
    }
    foreach(var evidence in Directory.GetFiles(Path.Combine(folder,"evidence"),"quality-*.json"))
    {
        using var document=JsonDocument.Parse(File.ReadAllBytes(evidence));var data=document.RootElement;
        if(Path.GetFileName(evidence).StartsWith("quality-compare",StringComparison.Ordinal))
        {
            Require(data.GetProperty("status").GetString()=="Verified","original strict compare");
            var mask=data.GetProperty("mask");Require(mask.GetProperty("max_diffs").GetInt32()>0&&!mask.GetProperty("all_tests").GetBoolean()&&!mask.GetProperty("acc_dev_tests").GetBoolean()&&mask.GetProperty("tol_dev_tests").GetBoolean(),"current compare mask");compareReports++;
        }
        else{massReports++;CountStatuses(data,components);}
    }
    runs.Add((JsonNode)new JsonObject{["directory"]=directory,["originalExit"]=raw,["checks"]=JsonNode.Parse(run.GetProperty("checks").GetRawText()),["captureSha256"]=Hash(File.ReadAllBytes(capturePath))});
}
var trxPaths=Directory.GetFiles(original,"*.trx",SearchOption.AllDirectories);
var full=trxPaths.Single(x=>Hash(File.ReadAllBytes(x))==quality.RootElement.GetProperty("fullSuite").GetProperty("trxSha256").GetString());
var trx=XDocument.Load(full);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
var counters=trx.Descendants(ns+"Counters").Single();
Require((string?)counters.Attribute("total")=="2019"&&(string?)counters.Attribute("passed")=="2019"&&(string?)counters.Attribute("failed")=="0","full TRX");
var supporting=new JsonArray();foreach(var result in trx.Descendants(ns+"UnitTestResult").Where(x=>((string?)x.Attribute("testName"))?.Contains("SupportingBlend",StringComparison.Ordinal)==true))supporting.Add((JsonNode)new JsonObject{["name"]=(string?)result.Attribute("testName"),["outcome"]=(string?)result.Attribute("outcome")});
var sealedFolder=Path.Combine(original,"temp_docs/joint-work");
using(var before=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/joint-full-binaries-before.json")))using(var after=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/joint-full-binaries-after.json")))
{
    foreach(var file in before.RootElement.EnumerateArray())
    {
        var name=file.GetProperty("path").GetString()!;var hash=file.GetProperty("sha256").GetString();
        Require(after.RootElement.EnumerateArray().Count(x=>x.GetProperty("path").GetString()==name&&x.GetProperty("sha256").GetString()==hash)==1,"test files before/after");
        Require(Hash(File.ReadAllBytes(sealedFolder+"/joint-full-"+Path.GetFileName(name)))==hash,"sealed test file");
    }
}
Require(File.ReadAllText(sealedFolder+"/joint-full-build.exit").Trim()=="0"&&File.ReadAllText(sealedFolder+"/joint-full-test.exit").Trim()=="0","full raw exits");
using(var copies=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/owner-trace-original/copy-source-binding.json")))
{
    Require(copies.RootElement.GetArrayLength()==104,"owner trace 104 originals");
    foreach(var copy in copies.RootElement.EnumerateArray()){var path=Path.Combine(original,copy.GetProperty("copyRelativeToProjectRoot").GetString()!);Require(Hash(File.ReadAllBytes(path))==copy.GetProperty("sha256").GetString()&&new FileInfo(path).Length==copy.GetProperty("bytes").GetInt64(),"owner original copy hash");}
}
var diff=Git("diff","73b06e1004122f6897da10532d6253579a033aa4",target,"--","src","scripts","tests");Require(diff.SequenceEqual(File.ReadAllBytes(sealedFolder+"/package-versus-accepted.diff")),"fixed source diff");
var receipt=new JsonObject{["scope"]="Independent BCL byte/source audit; does not convert producer raw exits or prove geometry",["target"]=target,["archive"]=new JsonObject{["sha256"]=zipHash,["length"]=new FileInfo(package).Length,["files"]=members.Count,["expandedBytes"]=bytes},["binaries"]=binaries,["runs"]=runs,["fullSuite"]=new JsonObject{["trxSha256"]=Hash(File.ReadAllBytes(full)),["total"]=2019,["passed"]=2019,["failed"]=0,["supporting"]=supporting},["members"]=members};
receipt["productBindings"]=artifacts;receipt["sourceManifests"]=manifests;receipt["sourceManifestRows"]=sourceRows;receipt["strictCompareReports"]=compareReports;receipt["massReports"]=massReports;var states=new JsonObject();foreach(var pair in components)states[pair.Key]=pair.Value;receipt["massStatusOccurrences"]=states;receipt["fixedDiffSha256"]=Hash(diff);
var receiptPath=output+(recheck?"/evidence-receipt-verified.json":"/evidence-receipt.json");Require(!File.Exists(receiptPath),"preserve receipt");File.WriteAllText(receiptPath,receipt.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"archive={members.Count}/{bytes}; PE/PDB={binaries.Count}; runs={runs.Count}; full=2019/2019; supporting={supporting.Count}; evidence audit PASS");
static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
static byte[] Git(params string[] arguments){var info=new ProcessStartInfo("git"){RedirectStandardOutput=true};foreach(var arg in arguments)info.ArgumentList.Add(arg);using var process=Process.Start(info)!;using var memory=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(memory);process.WaitForExit();Require(process.ExitCode==0,"git read");return memory.ToArray();}
static string Source([CallerFilePath]string source="")=>source;
static void CountStatuses(JsonElement data,Dictionary<string,int> counts){if(data.ValueKind==JsonValueKind.Object){foreach(var property in data.EnumerateObject()){if(property.Name is "status" or "agreementStatus"&&property.Value.ValueKind==JsonValueKind.String){var status=property.Value.GetString()!;counts[status]=counts.GetValueOrDefault(status)+1;}else CountStatuses(property.Value,counts);}}else if(data.ValueKind==JsonValueKind.Array)foreach(var value in data.EnumerateArray())CountStatuses(value,counts);}
static JsonObject OwnerStats(JsonNode[] rows,bool project,Dictionary<string,JsonNode> definitions)
{
    var groups=rows.GroupBy(row=>{var key=new JsonObject();foreach(var field in new[]{"scope","sources","definition","spine","n","occurrences","fixedCharts","center","needs"})key[field]=row[field]!.DeepClone();key["u0"]=row["domain"]!["U0"]!.DeepClone();key["u1"]=row["domain"]!["U1"]!.DeepClone();if(!project){key["surface"]=row["surface"]!.DeepClone();key["v0"]=row["domain"]!["V0"]!.DeepClone();key["v1"]=row["domain"]!["V1"]!.DeepClone();}return key.ToJsonString();}).ToArray();
    return new JsonObject{["uniqueKeys"]=groups.Length,["repeatedCalls"]=groups.Sum(x=>x.Count()-1),["crossVGroups"]=groups.Count(x=>x.Select(row=>row["domain"]!["V0"]+"/"+row["domain"]!["V1"]).Distinct().Count()>1),["tubePayloadMismatches"]=groups.Count(x=>x.Select(row=>row["tube"]!.ToJsonString()).Distinct().Count()>1),["storedProgramRepeatedCalls"]=groups.Where(x=>(int)definitions[(string)x.First()["definition"]!]["owner"]!["Block"]! >=0).Sum(x=>x.Count()-1)};
}
