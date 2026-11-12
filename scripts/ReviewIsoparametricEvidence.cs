#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string target="e7f0a4876ef296b56f5f7c57a45f15617eac6918";
const string output="temp_docs/independent-isoparam-e7f0a48";
if(args.SequenceEqual(new[]{"--receipt"}))
{
    var original=output+"/quality/original/";var qroot=original+"temp_docs/isoparametric-bound/";var mroot=output+"/controlled-original/original/";var mdata=mroot+"temp_docs/isoparam-controlled/";
    var quality=Read(output+"/quality/audit.json");var measurement=Read(output+"/controlled-original/receipt-verified.json");measurement.AsObject().Remove("members");
    var extraction=new JsonArray();foreach(var kind in new[]{"quality","controlled-original"}){var item=Read(output+"/"+kind+"/extraction.json");item.AsObject().Remove("members");extraction.Add(item);}
    var originals=Read(qroot+"quality-original-files.json").AsArray();foreach(var item in originals){var file=original+(string)item!["path"]!;Require(Hash(file)==(string?)item["sha256"]&&new FileInfo(file).Length==(long)item["bytes"]!,"original quality sealed manifest");}
    var before=Read(qroot+"isoparam-full-binaries-before.json");var after=Read(qroot+"isoparam-full-binaries-after.json");Require(before.AsArray().Count==6&&after.AsArray().Count==6,"six full suite files");foreach(var item in before.AsArray()){var path=(string)item!["path"]!;var match=after.AsArray().Single(x=>(string?)x!["path"]==path)!;var sealedPath=qroot+"isoparam-full-"+Path.GetFileName(path);Require((string?)match["sha256"]==(string?)item["sha256"]&&Hash(sealedPath)==(string?)item["sha256"]&&new FileInfo(sealedPath).Length==(long)item["bytes"]!,"six full suite file identities unchanged");}
    foreach(var phase in new[]{"build","test"})Require(File.ReadAllText(qroot+"isoparam-full-"+phase+".exit").Trim()=="0","original full raw exits");Require(File.ReadAllText(qroot+"focused.exit").Trim()=="1"&&File.ReadAllText(qroot+"focused-fixed.exit").Trim()=="0","initial compilation failure retained");
    var research=Read(qroot+"research-copy-binding.json");foreach(var item in research["files"]!.AsArray()){var file=original+(string)item!["destination"]!;Require(Hash(file)==(string?)item["sha256"]&&new FileInfo(file).Length==(long)item["bytes"]!,"research original preserved");}
    var researchRoot=qroot+"pk-section-research-original/temp_docs/native-sections/";var wrong=Read(researchRoot+"pk-isoparam.json");var corrected=Read(researchRoot+"pk-isoparam-corrected.json");Require(wrong["rows"]!.AsArray().Count==24&&wrong["rows"]!.AsArray().All(x=>(int)x!["createError"]! !=0),"wrong UV original calls failed");Require(corrected["rows"]!.AsArray().Count==24&&corrected["rows"]!.AsArray().All(x=>(int)x!["createError"]! ==0)&&corrected["rows"]!.AsArray().Sum(x=>x!["samples"]!.AsArray().Count)==120,"corrected finite research observations");
    var reportHash=Hash(mdata+"window/report.json");var materialHash=Hash(mdata+"material-binding-current.json");Require(reportHash=="6cedd40434fa16b26d4badd90a7e984ae6d2a1bcc8fc0d58c831e54304bbe7ea"&&materialHash=="f31d2f751cdff6c82b8454076f0c61794a7b3757198c4c5bea781c194b3d7f61","original report/material binding");
    var final=new JsonArray();foreach(var sample in Read(mdata+"window/report.json")["samples"]!.AsArray())foreach(var item in sample!["artifacts"]!.AsArray())
    {
        var name=(string)item!["path"]!;if(!name.EndsWith(".x_t",StringComparison.Ordinal))continue;var family=(string)sample["case"]!;var role=family switch{"sphere" or "planar"=>name.EndsWith("-managed.x_t",StringComparison.Ordinal)?"GM product":"PK reference product","solidblend"=>"GM product","curved"=>name.Contains("-gm-",StringComparison.Ordinal)?"GM before input":name.Contains("-pk-",StringComparison.Ordinal)?"PK before input":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product","shortedge"=>name.EndsWith("-input.x_t",StringComparison.Ordinal)?"PK seed":name.EndsWith("-input-fresh.x_t",StringComparison.Ordinal)?"GM received fresh input":name.EndsWith("-native-fresh.x_t",StringComparison.Ordinal)?"PK product received/re-exported by GM":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product",_=>throw new Exception("unexpected family")};
        var path="temp_docs/isoparam-controlled/window/"+(string)sample["directory"]!+"/"+name;Require(Hash(mroot+path)==(string?)item["sha256"],"actual final XT");final.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=item["sha256"]!.DeepClone(),["source"]=sample["commit"]!.DeepClone(),["role"]=role,["repetition"]=role.EndsWith("product",StringComparison.Ordinal)||role=="PK product received/re-exported by GM"?2:null});
    }
    Require(final.Count==64,"actual final XT set");
    var q2=output+"/q2-one-nurbs/";var capture=Read(q2+"capture/capture.json");var counts=new JsonObject();foreach(var g in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!))counts[g.Key]=g.Count();Require(File.ReadAllText(q2+"process.exit").Trim()=="2"&&capture["captureErrors"]!.AsArray().Count==0&&(int)counts["pass"]! ==16&&(int)counts["missing"]! ==13,"actual complete consumer raw2/pass/missing");var identity=Read(q2+"capture/identity/loaded.json");Require(Hash(q2+"capture/identity/kernel.dll")== (string?)identity["kernelDllSha256"],"consumer loaded identity");
    var trx=output+"/q2-focused/focused.trx";XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="81"&&(string?)counters.Attribute("passed")=="81"&&File.ReadAllText(output+"/q2-focused/process.exit").Trim()=="0","actual focused controls");
    var global=File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex/AGENTS.md"));var start=global.IndexOf("<!-- orca-user-global-policy-20261111:begin -->",StringComparison.Ordinal);var end=global.IndexOf("<!-- orca-user-global-policy-20261111:end -->",StringComparison.Ordinal);var policy=HashBytes(System.Text.Encoding.UTF8.GetBytes(global[(global.IndexOf('\n',start)+1)..end]));Require(policy=="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1","actual policy marker body");
    foreach(var path in new[]{"AGENTS.md","docs/Computational_Kernel_Architecture.md","docs/Intersection_Local_Modeling_Architecture.md"})Require(Git("show","fef52f3252bfe9a345f1a2872e9b6f96bdcafccd:"+path).SequenceEqual(File.ReadAllBytes(path)),"unchanged constraints");
    var executions=new JsonArray();foreach(var row in new[]{("extract-01",1),("extract-02",0),("quality-01",134),("quality-02",0),("measurement-01",0),("bindings-01",0),("controls-01",134),("controls-02",0),("focused-driver",0),("consumer-driver",2),("receipt-01",1),("receipt-02",134)}){var file="temp_docs/isoparam-review-"+row.Item1+".log";executions.Add((JsonNode)new JsonObject{["path"]=file,["sha256"]=Hash(file),["observedProcessExit"]=row.Item2});}
    var result=new JsonObject{["scope"]="Fixed e7f0a48 independent review; no author change, UI review, main integration, deployment or overall PK qualification",["deliveryCommit"]="b8c74b95047577e52d1086b48b0b0a88ee3f4388",["calculationCommit"]=target,["measurementMetadataCommit"]="ad79d2d4c6b59a81be423de4faf1745ea62cb318",["nativeTree"]="6a84372805cefdaf5c10cb3e0f3e56f8eb12ffdc",["sourceTree"]="cee0c419bd3e42acdc2d95e75a350f03cd8d06fa",["policyBodySha256"]=policy,["policyScope"]="Actual file only; automatic runtime loading not claimed",["decisions"]=new JsonObject{["mathematicsAndAlgorithm"]="accepted",["keyIntegration"]="accepted",["recordedQuality"]="accepted within actual original scope",["uniqueWindow"]="accepted as descriptive observations",["allNumericalVerified"]="blocked by original Unresolved",["fullPerformanceQualification"]="blocked by original raw2/performanceQualified=false",["generalCausalSpeedupAndOverallPkGoal"]="unproven"},["archives"]=extraction,["quality"]=quality,["measurement"]=measurement,["controls"]=Read(output+"/controls.json"),["q2Focused"]=new JsonObject{["rawExit"]=0,["passed"]=81,["total"]=81,["trxSha256"]=Hash(trx)},["q2Consumer"]=new JsonObject{["rawExit"]=2,["checks"]=counts,["captureSha256"]=Hash(q2+"capture/capture.json"),["actualLoadedIdentity"]=identity,["scope"]="one Linux current-source consumer, excluded from original Mac timing window"},["originalReportSha256"]=reportHash,["originalMaterialSha256"]=materialHash,["finalXtFiles"]=final,["originalQualityShortedgeFinalRep"]=5,["sealedQualityManifestRows"]=originals.Count,["researchCopyFiles"]=research["files"]!.AsArray().Count,["executions"]=executions};
    Write("docs/isoparam-independent/receipt.json",result);Console.WriteLine("PASS independent aggregate receipt");return;
}
if(args.SequenceEqual(new[]{"--extract"}))
{
    foreach(var item in new[]{("quality","335a733b28a6875f9522a786cfb4f03af2cfdbd31bf15ccbafe5885d21d5ed0f",18760675L,529,71158542L),("controlled-original","bb03d55d39335688c01003c3ec94a7dd61c5c809fb34803265c79675db0f6197",33975610L,1037,137674519L)})
    {
        var archive="temp_docs/incoming/mac-isoparam-e7f0a48-"+item.Item1+".zip";var destination=output+"/"+item.Item1+"/original";
        Require(!Directory.Exists(destination),"preserve original extraction");Require(new FileInfo(archive).Length==item.Item3&&Hash(archive)==item.Item2,"archive bytes");
        using var zip=ZipFile.OpenRead(archive);var names=new HashSet<string>(StringComparer.Ordinal);var members=new JsonArray();long expanded=0;
        foreach(var entry in zip.Entries)
        {
            var name=entry.FullName;Require(!Path.IsPathRooted(name)&&!name.Contains('\\')&&!name.Contains(':')&&!name.Split('/').Any(x=>x is ".." or ".")&&names.Add(name)&&((entry.ExternalAttributes>>16)&0xf000)!=0xa000,"safe unique member");
            var path=Path.Combine(destination,name);Require(Path.GetFullPath(path).StartsWith(Path.GetFullPath(destination)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"isolated member");
            if(name.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);using(var source=entry.Open())using(var file=new FileStream(path,FileMode.CreateNew))source.CopyTo(file);
            using var check=entry.Open();var sha=Convert.ToHexStringLower(SHA256.HashData(check));Require(new FileInfo(path).Length==entry.Length&&Hash(path)==sha,"extracted member bytes");expanded+=entry.Length;
            members.Add((JsonNode)new JsonObject{["path"]=name,["bytes"]=entry.Length,["sha256"]=sha});
        }
        Require(names.Count==item.Item4&&expanded==item.Item5,"archive dimensions");Write(output+"/"+item.Item1+"/extraction.json",new JsonObject{["archive"]=archive,["sha256"]=Hash(archive),["entries"]=names.Count,["expandedBytes"]=expanded,["members"]=members});
        Console.WriteLine($"{item.Item1}: {names.Count} members / {expanded} bytes verified");
    }
    return;
}
Require(args.SequenceEqual(new[]{"--quality"}),"--extract or --quality");
{
var original=output+"/quality/original/";var folder=original+"temp_docs/isoparametric-bound/";
foreach(var file in new[]{"docs/Certified_Isoparametric_Bound_Work.md","docs/Certified_Isoparametric_Quality.md","docs/isoparametric-bound/quality-receipt.json","docs/isoparametric-bound/source-binary-binding.json"})Require(File.ReadAllBytes(original+file).SequenceEqual(Git("show","ad79d2d4c6b59a81be423de4faf1745ea62cb318:"+file)),"fixed public document bytes "+file);
var binding=Read(original+"docs/isoparametric-bound/source-binary-binding.json");var binaries=new JsonArray();
foreach(var entry in binding["binaries"]!.AsArray())binaries.Add((JsonNode)Binary(original+(string)entry!["dll"]!,original+(string)entry["pdb"]!,target,entry));
var claimed=Read(original+"docs/isoparametric-bound/quality-receipt.json");var runs=new JsonArray();var strict=0;var mass=0;
foreach(var run in claimed["qualityRuns"]!.AsArray())
{
    var directory=(string)run!["directory"]!;var path=folder+directory;var capture=Read(path+"/capture.json");Require(Hash(path+"/capture.json")== (string?)run["captureSha256"],"quality capture hash");
    var exitPath=File.Exists(path+"/process.exit")?path+"/process.exit":folder+directory[..^8]+".exit";var raw=int.Parse(File.ReadAllText(exitPath));Require(raw==(int)run["originalExit"]!,"raw quality exit");
    Require(capture["captureErrors"]!.AsArray().Count==0,"capture errors");var counts=new JsonObject();foreach(var g in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!)){counts[g.Key]=g.Count();Require(g.Key!="fail"&&(int)run["checks"]![g.Key]! ==g.Count(),"original check counts");}
    var id=Read(path+"/identity/loaded.json");Require((string?)id["calculationCommit"]==target,"quality actual source");
    foreach(var kind in new[]{"kernel","oracle"}){Require(Hash(path+"/identity/"+kind+".dll")== (string?)id[kind+"DllSha256"]&&Hash(path+"/identity/"+kind+".pdb")== (string?)id[kind+"PdbSha256"],"quality loaded bytes");Require((string?)id[kind=="kernel"?"loadedKernelAssemblySha256":"loadedFileAppAssemblySha256"]==(string?)id[kind+"DllSha256"],"actual loaded witness");}
    if(File.Exists(path+"/source.sha256"))foreach(var line in File.ReadLines(path+"/source.sha256"))Require(HashBytes(Git("show",target+":"+line[66..]))==line[..64],"quality source manifest");
    foreach(var meshPath in Directory.GetFiles(path+"/meshes","*.json")){var source=Read(meshPath)["source"]!;if(source["xtArtifact"] is JsonValue artifact)Require(Hash(path+"/"+artifact.GetValue<string>())==(string?)source["xtSha256"]&&(string?)source["commit"]==target,"quality real product source");}
    foreach(var proofPath in Directory.GetFiles(path+"/evidence","quality-*.json"))
    {var proof=Read(proofPath);if(Path.GetFileName(proofPath).StartsWith("quality-compare",StringComparison.Ordinal)){var mask=proof["mask"]!;Require((string?)proof["status"]=="Verified"&&(int)mask["max_diffs"]!>0&&!(bool)mask["all_tests"]!&&!(bool)mask["acc_dev_tests"]!&&(bool)mask["tol_dev_tests"]!,"strict comparison mask/status");strict++;}else mass++;}
    runs.Add((JsonNode)new JsonObject{["directory"]=directory,["rawExit"]=raw,["checks"]=counts,["captureSha256"]=Hash(path+"/capture.json"),["steps"]=capture["steps"]!.AsArray().Count});
}
var trx=Directory.GetFiles(folder,"*.trx",SearchOption.AllDirectories).Single(x=>Hash(x)==(string?)claimed["fullSuite"]!["trxSha256"]);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="2029"&&(string?)counters.Attribute("executed")=="2029"&&(string?)counters.Attribute("passed")=="2029"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","full suite counters");
var receipt=new JsonObject{["scope"]="Independent original quality audit, no Mac code loading and no numerical or performance upgrade",["calculationCommit"]=target,["binaries"]=binaries,["runs"]=runs,["strictReports"]=strict,["massReports"]=mass,["trxSha256"]=Hash(trx),["fullSuiteTotal"]=2029};Write(output+"/quality/audit.json",receipt);Console.WriteLine($"Quality verified: {binaries.Count} PE/PDB, {runs.Count} groups, 2029 original tests, strict={strict}, mass={mass}");
}

static JsonObject Binary(string dll,string pdb,string commit,JsonNode? claim=null)
{
    using var pe=new PEReader(File.OpenRead(dll));var metadata=pe.GetMetadataReader();var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString();
    using var provider=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdb));var reader=provider.GetMetadataReader();var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));Require(new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16))==cv.Guid,"PE/PDB link");
    var matched=0;var other=new JsonArray();foreach(var handle in reader.Documents){var doc=reader.GetDocument(handle);var name=reader.GetString(doc.Name).Replace('\\','/');var start=name.IndexOf("/src/",StringComparison.Ordinal);if(start<0)start=name.IndexOf("/tests/",StringComparison.Ordinal);if(start<0)start=name.IndexOf("/scripts/",StringComparison.Ordinal);var relative=start<0?"":name[(start+1)..];if(relative.Length==0||relative.Contains("/obj/",StringComparison.Ordinal)){other.Add((JsonNode?)JsonValue.Create(Path.GetFileName(name)));continue;}Require(reader.GetGuid(doc.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")&&SHA256.HashData(Git("show",commit+":"+relative)).SequenceEqual(reader.GetBlobBytes(doc.Hash)),"tracked PDB source "+relative);matched++;}
    if(claim!=null)Require(Hash(dll)==(string?)claim["dllSha256"]&&Hash(pdb)==(string?)claim["pdbSha256"]&&mvid==(string?)claim["mvid"]&&matched==(int)claim["matchedTrackedDocuments"]!,"binary receipt");
    return new JsonObject{["dll"]=dll,["dllSha256"]=Hash(dll),["pdbSha256"]=Hash(pdb),["mvid"]=mvid,["matchedTrackedDocuments"]=matched,["unmapped"]=other};
}
static JsonNode Read(string file)=>JsonNode.Parse(File.ReadAllText(file))!;
static string Hash(string file){using var stream=File.OpenRead(file);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static string HashBytes(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Write(string path,JsonNode value){Require(!File.Exists(path),"preserve audit receipt");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,value.ToJsonString(new(){WriteIndented=true}));}
static byte[] Git(params string[] argv){var info=new ProcessStartInfo("git"){RedirectStandardOutput=true};foreach(var arg in argv)info.ArgumentList.Add(arg);using var child=Process.Start(info)!;using var memory=new MemoryStream();child.StandardOutput.BaseStream.CopyTo(memory);child.WaitForExit();Require(child.ExitCode==0,"fixed Git read");return memory.ToArray();}
static string Source([CallerFilePath]string path="")=>path;
