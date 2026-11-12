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
const string target="cc47871133fb7d536ffa7fd050066c51f4905708";
const string package="temp_docs/incoming/mac-projection-cc47871-quality.zip";
const string output="temp_docs/independent-projection-cc47871";
const string original=output+"/original";
if(args.SequenceEqual(new[]{"--receipt"}))
{
    var quality=JsonNode.Parse(File.ReadAllText(output+"/evidence-receipt-verified.json"))!;quality.AsObject().Remove("members");
    var measurement=JsonNode.Parse(File.ReadAllText(output+"/measurement/receipt-verified.json"))!;measurement.AsObject().Remove("samples");measurement.AsObject().Remove("binaries");
    var material="temp_docs/projection-controlled/material-binding-current.json";var window="temp_docs/projection-controlled/window";var mroot=output+"/measurement/original/";var report=JsonNode.Parse(File.ReadAllText(mroot+window+"/report.json"))!;
    var finalArtifacts=new JsonArray();var sources=new JsonArray();
    foreach(var sample in report["samples"]!.AsArray())
    {
        var family=(string)sample!["case"]!;var directory=window+"/"+(string)sample["directory"]!;
        sources.Add((JsonNode)new JsonObject{["directory"]=directory,["source"]=sample["commit"]!.DeepClone(),["side"]=sample["side"]!.DeepClone(),["rawExit"]=sample["processExit"]!.DeepClone(),["status"]=sample["qualityStatus"]!.DeepClone(),["loadedIdentity"]=JsonNode.Parse(File.ReadAllText(mroot+directory+"/identity/loaded.json"))});
        foreach(var file in sample["artifacts"]!.AsArray())
        {
            var name=(string)file!["path"]!;if(!name.EndsWith(".x_t",StringComparison.Ordinal))continue;
            var role=family switch
            {
                "sphere" or "planar"=>name.EndsWith("-managed.x_t",StringComparison.Ordinal)?"GM product":"PK reference product",
                "solidblend"=>"GM product",
                "curved"=>name.Contains("-gm-",StringComparison.Ordinal)?"GM before input":name.Contains("-pk-",StringComparison.Ordinal)?"PK before input":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product",
                "shortedge"=>name.EndsWith("-input.x_t",StringComparison.Ordinal)?"PK seed":name.EndsWith("-input-fresh.x_t",StringComparison.Ordinal)?"GM received fresh input":name.EndsWith("-native-fresh.x_t",StringComparison.Ordinal)?"PK product received and re-exported by GM":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product",
                _=>throw new Exception("unexpected family")
            };
            var path=directory+"/"+name;Require(Hash(File.ReadAllBytes(mroot+path))==(string?)file["sha256"],"final artifact hash");
            finalArtifacts.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=file["sha256"]!.DeepClone(),["role"]=role,["source"]=sample["commit"]!.DeepClone(),["processDirectory"]=sample["directory"]!.DeepClone(),["repetition"]=role.EndsWith("product",StringComparison.Ordinal)||role=="PK product received and re-exported by GM"?2:null,["boundary"]="Final file set; input and seed not assigned a fabricated repetition; product rep2 follows original producer overwrite, not byte identity alone"});
        }
    }
    var roleCounts=new JsonObject();foreach(var group in finalArtifacts.GroupBy(x=>(string)x!["role"]!))roleCounts[group.Key]=group.Count();Require(finalArtifacts.Count==64,"64 final files");
    var q2=output+"/q2-one-nurbs/00-solidblend-candidate";var capture=JsonNode.Parse(File.ReadAllText(q2+"/capture.json"))!;var counts=new JsonObject();foreach(var group in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!))counts[group.Key]=group.Count();
    var q2id=JsonNode.Parse(File.ReadAllText(q2+"/identity/loaded.json"))!;Require(Hash(File.ReadAllBytes(q2+"/identity/kernel.dll"))==(string?)q2id["kernelDllSha256"],"q2 actual loaded bytes");
    XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var trx=XDocument.Load(output+"/q2-focused/focused.trx").Descendants(ns+"Counters").Single();
    var auditLogs=new JsonArray();foreach(var file in new[]{"projection-review-evidence-01.log","projection-review-evidence-02.log","projection-review-measurement-01.log","projection-review-measurement-02.log","projection-review-controls-01.log","projection-review-consumer-01.log","projection-review-focused-driver.log"})auditLogs.Add((JsonNode)new JsonObject{["path"]="temp_docs/"+file,["sha256"]=Hash(File.ReadAllBytes("temp_docs/"+file)),["actualExit"]=file=="projection-review-consumer-01.log"?2:0});
    var document=new JsonObject{["scope"]="Independent fixed cc47871 review derivation; original exits and tri-state preserved; acceptance limits in accompanying Markdown",["decisions"]=new JsonObject{["algorithmAndIntegration"]="accepted",["recordedGeometryTopologyAndLifecycle"]="accepted within executed scope",["originalMacSuite"]="accepted as 2022/2022 raw build0/test0",["uniqueMacWindow"]="accepted as descriptive fixed-source observations",["allNumericVerifiedUnderOriginalContract"]="blocked by original Unresolved outcomes",["generalOrCausalSpeedupAndOverallPkGoal"]="unproven",["dashboardData"]="accepted for actual-source display with original states; UI, integration and deployment not reviewed"},["quality"]=quality,["measurement"]=measurement,["controls"]=JsonNode.Parse(File.ReadAllText(output+"/controls.json")),["q2Focused"]=new JsonObject{["actualExit"]=int.Parse(File.ReadAllText(output+"/q2-focused/process.exit")),["total"]=(string?)trx.Attribute("total"),["passed"]=(string?)trx.Attribute("passed"),["failed"]=(string?)trx.Attribute("failed"),["trxSha256"]=Hash(File.ReadAllBytes(output+"/q2-focused/focused.trx"))},["q2FreshNurbs"]=new JsonObject{["originalExit"]=int.Parse(File.ReadAllText(q2+"/process.exit")),["checks"]=counts,["captureErrors"]=capture["captureErrors"]!.DeepClone(),["loadedIdentity"]=q2id,["captureSha256"]=Hash(File.ReadAllBytes(q2+"/capture.json")),["scope"]="One complete current-source Linux API execution and quality; not Mac timing"},["dashboardData"]=new JsonObject{["originalArchive"]=measurement["archiveSha256"]!.DeepClone(),["originalMaterialBindingPath"]=material,["originalMaterialBindingSha256"]=Hash(File.ReadAllBytes(mroot+material)),["meshFiles"]=312,["meshBytes"]=35389380,["meshBoundary"]="GM live product, PK private snapshot and independent PK product are separate; no role repair for curved",["sourceDirectories"]=sources,["finalXtRoleCounts"]=roleCounts,["finalXtFiles"]=finalArtifacts,["qualityNurbsFinalProductRepetition"]=5},["auditExecutions"]=auditLogs};
    var destination="docs/projection-independent/receipt.json";Require(!File.Exists(destination),"preserve aggregate receipt");Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.WriteAllText(destination,document.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Independent aggregate receipt written");return;
}
if(args.SequenceEqual(new[]{"--focused"}))
{
    var folder=output+"/q2-focused";Require(!Directory.Exists(folder),"preserve focused execution");Directory.CreateDirectory(folder);
    var info=new ProcessStartInfo("setsid"){RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var value in new[]{"dotnet","test","tests/KernelTests/KernelTests.csproj","-c","Release","-p:UseSharedCompilation=false","-p:BuildInParallel=false","--filter","FullyQualifiedName~BezierPositionBoundsTests|FullyQualifiedName~BSplineProjectionTests|FullyQualifiedName~GeometryDistanceTests|FullyQualifiedName~NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative|FullyQualifiedName~SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled","--logger","trx;LogFileName=focused.trx","--results-directory",folder})info.ArgumentList.Add(value);
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";info.Environment["P_SCHEMA"]=Path.GetFullPath("third_party/parasolid/schema");
    using var process=Process.Start(info)!;File.WriteAllText(folder+"/owned-pgid.txt",process.Id+"\n");
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    File.WriteAllText(folder+"/stdout.log",await stdout);File.WriteAllText(folder+"/stderr.log",await stderr);File.WriteAllText(folder+"/process.exit",process.ExitCode+"\n");
    using var cleanup=Process.Start(new ProcessStartInfo("kill"){ArgumentList={"-TERM","--","-"+process.Id},RedirectStandardError=true})!;await cleanup.WaitForExitAsync();File.WriteAllText(folder+"/owned-group-cleanup.txt",cleanup.ExitCode+"\n"+await cleanup.StandardError.ReadToEndAsync());
    Console.WriteLine("q2 focused raw exit="+process.ExitCode);Environment.ExitCode=process.ExitCode;return;
}
var recheck=args.SequenceEqual(new[]{"--recheck"});
if(Directory.Exists(output)&&!recheck)throw new Exception("Review output already exists; preserve evidence");
Directory.CreateDirectory(original);
var zipHash=Hash(File.ReadAllBytes(package));
Require(zipHash=="c1f1b90ff6f8eed87d23cb960b16e3502eab77211f0d1d13bdce7f14812702b8","archive hash");
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
Require(members.Count==543&&bytes==79641496,"archive dimensions");
var publicFiles=new[]{"docs/Native_Projection_Position_Bounds.md","docs/projection-bound/quality-receipt.json","docs/projection-bound/source-binary-binding.json","docs/Projection_Bound_Quality.md"};
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
using var originalQuality=JsonDocument.Parse(File.ReadAllBytes(publicFiles[1]));
var runs=new JsonArray();
var artifacts=0;var manifests=0;var sourceRows=0;var compareReports=0;var massReports=0;var components=new Dictionary<string,int>();
foreach(var run in originalQuality.RootElement.GetProperty("qualityRuns").EnumerateArray())
{
    var directory=run.GetProperty("directory").GetString()!;var folder=Path.Combine(original,"temp_docs/projection-bound",directory);var capturePath=Path.Combine(folder,"capture.json");
    Require(Hash(File.ReadAllBytes(capturePath))==run.GetProperty("captureSha256").GetString(),"capture hash");
    using var capture=JsonDocument.Parse(File.ReadAllBytes(capturePath));
    var exit=directory.Contains('/')?Path.Combine(folder,"process.exit"):Path.Combine(original,"temp_docs/projection-bound",directory[..^8]+".exit");
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
var full=trxPaths.Single(x=>Hash(File.ReadAllBytes(x))==originalQuality.RootElement.GetProperty("fullSuite").GetProperty("trxSha256").GetString());
var originalTrx=XDocument.Load(full);XNamespace originalNs="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
var counters=originalTrx.Descendants(originalNs+"Counters").Single();
Require((string?)counters.Attribute("total")=="2022"&&(string?)counters.Attribute("passed")=="2022"&&(string?)counters.Attribute("failed")=="0","full TRX");
var supporting=new JsonArray();foreach(var result in originalTrx.Descendants(originalNs+"UnitTestResult").Where(x=>((string?)x.Attribute("testName"))?.Contains("SupportingBlend",StringComparison.Ordinal)==true))supporting.Add((JsonNode)new JsonObject{["name"]=(string?)result.Attribute("testName"),["outcome"]=(string?)result.Attribute("outcome")});
var sealedFolder=Path.Combine(original,"temp_docs/projection-bound");
using(var before=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/projection-full-binaries-before.json")))using(var after=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/projection-full-binaries-after.json")))
{
    foreach(var file in before.RootElement.EnumerateArray())
    {
        var name=file.GetProperty("path").GetString()!;var hash=file.GetProperty("sha256").GetString();
        Require(after.RootElement.EnumerateArray().Count(x=>x.GetProperty("path").GetString()==name&&x.GetProperty("sha256").GetString()==hash)==1,"test files before/after");
        Require(Hash(File.ReadAllBytes(sealedFolder+"/projection-full-"+Path.GetFileName(name)))==hash,"sealed test file");
    }
}
Require(File.ReadAllText(sealedFolder+"/projection-full-build.exit").Trim()=="0"&&File.ReadAllText(sealedFolder+"/projection-full-test.exit").Trim()=="0","full raw exits");
var focused=XDocument.Load(sealedFolder+"/test-results/focused.trx").Descendants(originalNs+"Counters").Single();Require((string?)focused.Attribute("total")=="132"&&(string?)focused.Attribute("passed")=="132"&&File.ReadAllText(sealedFolder+"/focused.exit").Trim()=="0","original focused 132 raw0");
using(var copies=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/sampling-copy-binding.json")))
{
    Require(copies.RootElement.GetProperty("files").GetArrayLength()==70,"70 copied sampling originals");
    foreach(var copy in copies.RootElement.GetProperty("files").EnumerateArray()){var file=sealedFolder+"/accepted-source-sampling-original/"+copy.GetProperty("relative").GetString();Require(Hash(File.ReadAllBytes(file))==copy.GetProperty("sha256").GetString()&&new FileInfo(file).Length==copy.GetProperty("bytes").GetInt64(),"sampling member bytes");}
}
var diff=Git("diff","17eedbfd6be891db8c43494cd91a896d261242c4",target,"--","src","scripts","tests");Require(diff.SequenceEqual(File.ReadAllBytes(sealedFolder+"/package-versus-accepted.diff")),"fixed source diff");
var receipt=new JsonObject{["scope"]="Independent BCL byte/source audit; does not convert producer raw exits or prove geometry",["target"]=target,["archive"]=new JsonObject{["sha256"]=zipHash,["length"]=new FileInfo(package).Length,["files"]=members.Count,["expandedBytes"]=bytes},["binaries"]=binaries,["runs"]=runs,["fullSuite"]=new JsonObject{["trxSha256"]=Hash(File.ReadAllBytes(full)),["total"]=2022,["passed"]=2022,["failed"]=0,["supporting"]=supporting},["members"]=members};
receipt["productBindings"]=artifacts;receipt["sourceManifests"]=manifests;receipt["sourceManifestRows"]=sourceRows;receipt["strictCompareReports"]=compareReports;receipt["massReports"]=massReports;var states=new JsonObject();foreach(var pair in components)states[pair.Key]=pair.Value;receipt["massStatusOccurrences"]=states;receipt["fixedDiffSha256"]=Hash(diff);
var receiptPath=output+(recheck?"/evidence-receipt-verified.json":"/evidence-receipt.json");Require(!File.Exists(receiptPath),"preserve receipt");File.WriteAllText(receiptPath,receipt.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"archive={members.Count}/{bytes}; PE/PDB={binaries.Count}; runs={runs.Count}; full=2022/2022; supporting={supporting.Count}; evidence audit PASS");
static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
static byte[] Git(params string[] arguments){var info=new ProcessStartInfo("git"){RedirectStandardOutput=true};foreach(var arg in arguments)info.ArgumentList.Add(arg);using var process=Process.Start(info)!;using var memory=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(memory);process.WaitForExit();Require(process.ExitCode==0,"git read");return memory.ToArray();}
static string Source([CallerFilePath]string source="")=>source;
static void CountStatuses(JsonElement data,Dictionary<string,int> counts){if(data.ValueKind==JsonValueKind.Object){foreach(var property in data.EnumerateObject()){if(property.Name is "status" or "agreementStatus"&&property.Value.ValueKind==JsonValueKind.String){var status=property.Value.GetString()!;counts[status]=counts.GetValueOrDefault(status)+1;}else CountStatuses(property.Value,counts);}}else if(data.ValueKind==JsonValueKind.Array)foreach(var value in data.EnumerateArray())CountStatuses(value,counts);}
