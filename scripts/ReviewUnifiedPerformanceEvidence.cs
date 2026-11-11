#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

// Read-only review of the sealed Mac package. Never load its assemblies.
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var measurement=args.Length==1&&args[0]=="unpack-measurement";
var zipPath=Path.Combine(root,measurement?"temp_docs/incoming/mac-unified-native224-controlled-original.zip":"temp_docs/incoming/mac-unified-performance-review.zip");
var original=Path.Combine(root,measurement?"temp_docs/independent-1654/measurement-original":"temp_docs/independent-1654/original");
var output=Path.Combine(root,"temp_docs/independent-1654");
var sourceCache=new Dictionary<string,byte[]>();
Directory.CreateDirectory(output);
if(args.Length==1&&args[0] is "unpack" or "unpack-measurement")
{
    Require(Hash(zipPath)==(measurement?"91ce2f8602db1d119d7e0262f6af07f588f131af9967faa5051634e5ba36f3ab":"d57ba2306dd083274fc4016720e454fb8f3f564afc639324c2465897f0877e26"),"sealed ZIP SHA256");
    using var zip=ZipFile.OpenRead(zipPath);var names=new HashSet<string>(StringComparer.Ordinal);long size=0;
    foreach(var entry in zip.Entries)
    {
        Require(entry.FullName.Length>0&&!entry.FullName.StartsWith('/')&&!entry.FullName.Contains('\\')&&!entry.FullName.Split('/').Any(p=>p is ".." or "."||p.Contains(':')),"safe relative ZIP name");
        Require(names.Add(entry.FullName)&&((entry.ExternalAttributes>>16)&0xf000)!=0xa000,"unique non-symlink member");
        size+=entry.Length;
    }
    Require(zip.Entries.Count==(measurement?1042:1158)&&size==(measurement?136874825:232287358)&&new FileInfo(zipPath).Length==(measurement?33889095:61315966),"ZIP inventory");
    if(!Directory.Exists(original))zip.ExtractToDirectory(original);
    var members=new JsonArray();
    foreach(var e in zip.Entries)
    {
        using var stream=e.Open();var hash=Convert.ToHexStringLower(SHA256.HashData(stream));
        Require(hash==Hash(Path.Combine(original,e.FullName)),"extracted bytes match member");
        members.Add((JsonNode)new JsonObject{["path"]=e.FullName,["bytes"]=e.Length,["sha256"]=hash});
    }
    Save(measurement?"measurement-archive.json":"archive.json",new JsonObject{["zipSha256"]=Hash(zipPath),["files"]=members.Count,["expandedBytes"]=size,["members"]=members});
    Console.WriteLine($"ARCHIVE SHA verified files={members.Count} expandedBytes={size} safeNames=true symlinks=0");return;
}
if(args.Length==1&&args[0]=="receipt")
{
    var summary=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"summary.json")))!;
    var evidence=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"evidence.json")))!;
    summary["binaries"]=evidence["binaries"]!.DeepClone();
    summary["fixedSourceDiffSha256"]=Hash(Path.Combine(output,"fixed-source.diff"));
    var runs=new JsonArray();
    foreach(var stem in new[]{"unpack","evidence-attempt1","evidence-attempt2","evidence-final","evidence-sealed","math-attempt1","math-attempt2","math-attempt3","math-final","products-attempt1"})
    {var log=Path.Combine(output,stem+".log");var exit=Path.Combine(output,stem+".exit");runs.Add((JsonNode)new JsonObject{["stem"]="temp_docs/independent-1654/"+stem,["rawExit"]=File.Exists(exit)?File.ReadAllText(exit).Trim():null,["logSha256"]=File.Exists(log)?Hash(log):null});}
    summary["independentRuns"]=runs;summary["initialUnloggedUnpackAttempt"]=new JsonObject{["rawExit"]=134,["cause"]="review script JSON reflection disabled after extraction; extracted original bytes subsequently matched sealed ZIP members"};
    summary["mathScope"]="34 exact original Bezier nets / 8262 rational jets; 80 synthetic and 16 actual producer block matrices; 54 exact rolling moment/full-tail or half-angle cases; n3/n7/n63 and n67 rejection";
    summary["productComparisonSha256"]=Hash(Path.Combine(output,"product-comparisons.json"));
    summary["noMacAssembliesExecuted"]=true;summary["formalMeasurement"]=File.Exists(Path.Combine(output,"measurement-summary.json"))?JsonNode.Parse(File.ReadAllText(Path.Combine(output,"measurement-summary.json"))):null;
    var destination=Path.Combine(root,"docs/reviews/advanced-modeling/unified-performance-1654-independent-review.receipt.json");Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.WriteAllText(destination,summary.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("RECEIPT saved relative review receipt");return;
}
Require(args.Length==1&&args[0]=="evidence","usage: unpack | evidence");
using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"archive.json")));
foreach(var member in manifest.RootElement.GetProperty("members").EnumerateArray())Require(Hash(Path.Combine(original,member.GetProperty("path").GetString()!))==member.GetProperty("sha256").GetString(),"immutable original member");
Require(Hash(zipPath)==manifest.RootElement.GetProperty("zipSha256").GetString(),"immutable ZIP");
var bindingPath="docs/unified-performance/source-binary-binding.json";
using var binding=JsonDocument.Parse(File.ReadAllText(Path.Combine(original,bindingPath)));
var binaries=new JsonArray();
foreach(var binary in binding.RootElement.GetProperty("binaries").EnumerateArray())
{
    var dll=binary.GetProperty("dll").GetString()!;var pdb=binary.GetProperty("pdb").GetString()!;
    Require(Hash(Path.Combine(original,dll))==binary.GetProperty("dllSha256").GetString(),"DLL SHA");
    Require(Hash(Path.Combine(original,pdb))==binary.GetProperty("pdbSha256").GetString(),"PDB SHA");
    using var peStream=File.OpenRead(Path.Combine(original,dll));using var pe=new PEReader(peStream);var metadata=pe.GetMetadataReader();
    var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    Require(mvid.ToString()==binary.GetProperty("mvid").GetString(),"MVID");
    using var pdbStream=File.OpenRead(Path.Combine(original,pdb));using var provider=MetadataReaderProvider.FromPortablePdbStream(pdbStream);var reader=provider.GetMetadataReader();
    var debugId=reader.DebugMetadataHeader!.Id;var guid=new Guid(debugId.AsSpan(0,16));
    var cv=pe.ReadDebugDirectory().Where(d=>d.Type==DebugDirectoryEntryType.CodeView).Select(pe.ReadCodeViewDebugDirectoryData).ToArray();
    Require(cv.Length==1&&cv[0].Guid==guid,"PE/PDB link");
    var matched=new List<string>();var unmapped=new List<string>();
    foreach(var handle in reader.Documents)
    {
        var doc=reader.GetDocument(handle);var path=reader.GetString(doc.Name).Replace('\\','/');
        var index=path.IndexOf("/src/",StringComparison.Ordinal);if(index<0)index=path.IndexOf("/tests/",StringComparison.Ordinal);
        if(index>=0)path=path[(index+1)..];
        if((path.StartsWith("src/")||path.StartsWith("tests/"))&&!path.Contains("/obj/"))
        {
            var bytes=GitBytes("1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7:"+path);
            Require(reader.GetGuid(doc.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16"),"PDB SHA256 algorithm");
            Require(SHA256.HashData(bytes).AsSpan().SequenceEqual(reader.GetBlobBytes(doc.Hash)),"tracked source checksum: "+path);matched.Add(path);
        }
        else unmapped.Add(path);
    }
    Require(matched.Count==binary.GetProperty("matchedTrackedDocuments").GetInt32(),"tracked document count");
    var unmappedJson=new JsonArray();foreach(var name in unmapped)unmappedJson.Add((JsonNode?)JsonValue.Create(name));
    binaries.Add((JsonNode)new JsonObject{["dll"]=dll,["pdb"]=pdb,["mvid"]=mvid.ToString(),["codeViewGuid"]=guid.ToString(),["trackedDocuments"]=matched.Count,["unmapped"]=unmappedJson});
    Console.WriteLine($"PEPDB {dll} MVID={mvid} tracked={matched.Count} unmapped={unmapped.Count}");
}
using var receipt=JsonDocument.Parse(File.ReadAllText(Path.Combine(original,"docs/unified-performance/quality-receipt.json")));
var captures=new JsonArray();
var qualitySummary=new JsonArray();
foreach(var run in receipt.RootElement.GetProperty("qualityRuns").EnumerateArray())
{
    var dir=run.GetProperty("directory").GetString()!;var capture=Path.Combine(original,"temp_docs/unified-performance",dir,"capture.json");
    Require(Hash(capture)==run.GetProperty("captureSha256").GetString(),"Capture SHA "+dir);
    using var doc=JsonDocument.Parse(File.ReadAllText(capture));
    var checks=doc.RootElement.GetProperty("checks").EnumerateArray().ToArray();
    Require(doc.RootElement.GetProperty("captureErrors").GetArrayLength()==run.GetProperty("captureErrors").GetInt32(),"Capture errors");
    Require(doc.RootElement.GetProperty("steps").GetArrayLength()==run.GetProperty("steps").GetInt32(),"Capture step count");
    var counts=new JsonObject();foreach(var group in checks.GroupBy(c=>c.GetProperty("status").GetString()!))counts[group.Key]=group.Count();
    foreach(var expected in run.GetProperty("checks").EnumerateObject())Require((int?)counts[expected.Name]==expected.Value.GetInt32(),"Capture actual check counts");
    if(dir.StartsWith("active",StringComparison.Ordinal))
    {
        Require(checks.All(c=>c.GetProperty("status").GetString() is "pass" or "missing"),"final Capture fail count");
        var identity=JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(capture)!,"identity/loaded.json")))!;
        Require((string?)identity["calculationCommit"]=="1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7"&&(string?)identity["kernelDllSha256"]=="b7fd887d3367d7e8830dc45751f5ca1237e1bb237e5e4532b6cd5f4337209180","final producer identity");
        foreach(var pair in new[]{("captureSourceFileSha256","WebProgressCapture.cs"),("bodyCompareSourceFileSha256","ParasolidBodyCompare.cs"),("massSourceFileSha256","ParasolidMassProperties.cs")})Require((string?)identity[pair.Item1]==Convert.ToHexStringLower(SHA256.HashData(GitBytes("1654dbf2:scripts/"+pair.Item2))),"actual helper source SHA");
        foreach(var check in checks)
        {
            if(check.TryGetProperty("captureIdentity",out var ci))Require(ci.GetProperty("kernelDllSha256").GetString()==(string?)identity["kernelDllSha256"],"per-check loaded identity");
            if(check.TryGetProperty("evidence",out var evidence)&&evidence.ValueKind==JsonValueKind.String)Require(File.Exists(Path.Combine(Path.GetDirectoryName(capture)!,evidence.GetString()!)),"same-call evidence exists");
        }
        var manifestPath=Path.Combine(Path.GetDirectoryName(capture)!,"source.sha256");var sourceCount=0;
        if(File.Exists(manifestPath))foreach(var line in File.ReadLines(manifestPath))
        {var relative=line[66..];Require(Convert.ToHexStringLower(SHA256.HashData(GitBytes("1654dbf2:"+relative)))==line[..64],"run source manifest "+relative);sourceCount++;}
        qualitySummary.Add((JsonNode)new JsonObject{["directory"]=dir,["checks"]=counts,["sourceManifestEntries"]=sourceCount,["captureSha256"]=Hash(capture)});
    }
    captures.Add((JsonNode)new JsonObject{["directory"]=dir,["sha256"]=Hash(capture),["data"]=JsonNode.Parse(doc.RootElement.GetRawText())});
}
var trx=new JsonArray();
foreach(var path in Directory.GetFiles(original,"*.trx",SearchOption.AllDirectories))
{
    var doc=XDocument.Load(path);var counters=doc.Descendants().First(e=>e.Name.LocalName=="Counters");
    var failures=new JsonArray();
    foreach(var e in doc.Descendants().Where(e=>e.Name.LocalName=="UnitTestResult"&&(string?)e.Attribute("outcome")=="Failed"))failures.Add((JsonNode)new JsonObject{["name"]=(string?)e.Attribute("testName"),["message"]=e.Descendants().FirstOrDefault(x=>x.Name.LocalName=="Message")?.Value,["stack"]=e.Descendants().FirstOrDefault(x=>x.Name.LocalName=="StackTrace")?.Value});
    var counts=new JsonObject();foreach(var a in counters.Attributes())counts[a.Name.LocalName]=a.Value;
    trx.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(original,path),["sha256"]=Hash(path),["counts"]=counts,["failures"]=failures});
    Console.WriteLine($"TRX {Path.GetRelativePath(original,path)} total={counts["total"]} passed={counts["passed"]} failed={counts["failed"]}");
}
Save("evidence.json",new JsonObject{["calculation"]="1654dbf2bb31ff867f40c3ae4eb66cdb3d601fb7",["zipSha256"]=Hash(zipPath),["binaries"]=binaries,["captures"]=captures,["trx"]=trx});
var comparisons=new JsonArray();
foreach(var kind in new[]{"unified-failure-ready","unified-stack-ready"})
{
    var stem="temp_docs/unified-performance/baseline-comparisons/"+kind;
    var rows=JsonNode.Parse(File.ReadAllText(Path.Combine(original,stem+"-comparison.json")))!["records"]!.AsArray();
    Require((string?)rows[0]!["testSha256"]==(string?)rows[1]!["testSha256"]&&(string?)rows[0]!["testPdbSha256"]==(string?)rows[1]!["testPdbSha256"],"same narrow comparison test DLL/PDB");
    foreach(var row in rows)
    {
        var side=(string)row!["side"]!;var app=stem+"-"+side+"-app/";
        foreach(var pair in new[]{("nativeSha256","ProjectGmKernel.Native.dll"),("nativePdbSha256","ProjectGmKernel.Native.pdb"),("testSha256","KernelTests.dll"),("testPdbSha256","KernelTests.pdb")})Require(Hash(Path.Combine(original,app+pair.Item2))==(string?)row[pair.Item1],"narrow comparison actual sealed assembly");
        VerifyFixtureSources(app,kind=="unified-stack-ready"?["RollingNetworkTests.cs"]:["BlendIntersectionTests.cs","NestedBlendCacheEmptyReadSetTests.cs"]);
        Require(File.ReadAllText(Path.Combine(original,stem+"-"+side+".exit")).Trim()==row["rawTestExit"]!.ToString(),"narrow original raw exit");
        if(side=="baseline")VerifyBaselinePdb(app);
    }
    comparisons.Add((JsonNode)new JsonObject{["comparison"]=kind,["records"]=rows.DeepClone()});
}
var suite="temp_docs/unified-performance/unified-bundle-active-full";
var before=JsonNode.Parse(File.ReadAllText(Path.Combine(original,suite+"-binaries-before.json")))!.AsArray();
var after=JsonNode.Parse(File.ReadAllText(Path.Combine(original,suite+"-binaries-after.json")))!.AsArray();
foreach(var file in before){var path=(string)file!["path"]!;var match=after.Single(x=>(string?)x!["path"]==path)!;var sha=(string)file["sha256"]!;Require(sha==(string?)match["sha256"]&&sha==Hash(Path.Combine(original,suite+"-"+Path.GetFileName(path))),"six sealed suite assembly files before/after");}
Save("summary.json",new JsonObject{["zipSha256"]=Hash(zipPath),["finalQuality"]=qualitySummary,["baselineComparisons"]=comparisons,["suiteSixFilesUnchanged"]=true,["suiteRawExit"]=File.ReadAllText(Path.Combine(original,suite+"-test.exit")).Trim()});
Console.WriteLine("PASS sealed bytes, static PE/PDB/source checks and original result inventory (no assembly execution)");

void Save(string name,JsonNode value)=>File.WriteAllText(Path.Combine(output,name),value.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
byte[] GitBytes(string spec)
{
    if(sourceCache.TryGetValue(spec,out var saved))return saved;
    var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("show");start.ArgumentList.Add(spec);
    using var p=Process.Start(start)!;using var stream=new MemoryStream();p.StandardOutput.BaseStream.CopyTo(stream);var error=p.StandardError.ReadToEnd();p.WaitForExit();Require(p.ExitCode==0,"git show "+spec+" "+error);var bytes=stream.ToArray();sourceCache.Add(spec,bytes);return bytes;
}
void VerifyBaselinePdb(string app)
{
    using var stream=File.OpenRead(Path.Combine(original,app+"ProjectGmKernel.Native.dll"));using var pe=new PEReader(stream);
    using var pdb=File.OpenRead(Path.Combine(original,app+"ProjectGmKernel.Native.pdb"));using var provider=MetadataReaderProvider.FromPortablePdbStream(pdb);var reader=provider.GetMetadataReader();
    Require(new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16))==pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(d=>d.Type==DebugDirectoryEntryType.CodeView)).Guid,"baseline PE/PDB");var matched=0;
    foreach(var handle in reader.Documents)
    {
        var doc=reader.GetDocument(handle);var path=reader.GetString(doc.Name).Replace('\\','/');var at=path.IndexOf("/src/",StringComparison.Ordinal);
        if(at<0||path.Contains("/obj/"))continue;path=path[(at+1)..];Require(reader.GetBlobBytes(doc.Hash).AsSpan().SequenceEqual(SHA256.HashData(GitBytes("84c558c1:"+path))),"baseline Native133 tracked source");matched++;
    }
    Require(matched==333,"baseline 333 source documents");Console.WriteLine("BASELINE Native133 PE/PDB 333 source checksums match fixed84c "+app);
}
void VerifyFixtureSources(string app,string[] names)
{
    using var stream=File.OpenRead(Path.Combine(original,app+"KernelTests.dll"));using var pe=new PEReader(stream);
    using var pdb=File.OpenRead(Path.Combine(original,app+"KernelTests.pdb"));using var provider=MetadataReaderProvider.FromPortablePdbStream(pdb);var reader=provider.GetMetadataReader();
    Require(new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16))==pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(d=>d.Type==DebugDirectoryEntryType.CodeView)).Guid,"comparison fixture PE/PDB link");
    foreach(var name in names)
    {
        var doc=reader.GetDocument(reader.Documents.Single(h=>reader.GetString(reader.GetDocument(h).Name).EndsWith("/"+name,StringComparison.Ordinal)));
        Require(reader.GetBlobBytes(doc.Hash).AsSpan().SequenceEqual(SHA256.HashData(GitBytes("1654dbf2:tests/KernelTests/"+name))),"narrow failure fixture matches current fixed test source");
    }
    Console.WriteLine("FIXTURE exact target source checksums "+string.Join(",",names)+" "+app);
}
static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static string Source([CallerFilePath]string path="")=>path;
