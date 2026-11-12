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
const string target="ab0177ed2cfb6fab1b893c81562a0364fc487176";
const string output="temp_docs/independent-localjet-ab0177e";
if(args.SequenceEqual(new[]{"--receipt"}))
{
    const string delivery="1065e7c8f818ce10f6d3ee3edfe8f8bc33f3d582",metadata="85a595edbdd266e9a8b593086bd5535d9e16f375",baseline="b8c74b95047577e52d1086b48b0b0a88ee3f4388";
    var original=output+"/quality/original/";var folder=original+"temp_docs/local-spline-jet/";var measurementOriginal=output+"/controlled-original/original/";var data=measurementOriginal+"temp_docs/localjet-controlled/";
    var native="90573c1c23eaecebd931c5e08f259a20fedc1930";var src="232988716489967a9ce581b2bad8fa5756c64a83";
    foreach(var commit in new[]{target,metadata,delivery}){Require(System.Text.Encoding.UTF8.GetString(Git("rev-parse",commit+":src/ProjectGmKernel.Native")).Trim()==native&&System.Text.Encoding.UTF8.GetString(Git("rev-parse",commit+":src")).Trim()==src,"fixed Native/src trees");}
    foreach(var file in new[]{"AGENTS.md","docs/Computational_Kernel_Architecture.md","docs/Intersection_Local_Modeling_Architecture.md"})Require(Git("show",baseline+":"+file).SequenceEqual(File.ReadAllBytes(file))&&Git("show",target+":"+file).SequenceEqual(File.ReadAllBytes(file)),"actual unchanged architecture/constraints");
    var global=File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex/AGENTS.md"));var begin=global.IndexOf("<!-- orca-user-global-policy-20261111:begin -->",StringComparison.Ordinal);var end=global.IndexOf("<!-- orca-user-global-policy-20261111:end -->",StringComparison.Ordinal);var policy=HashBytes(System.Text.Encoding.UTF8.GetBytes(global[(global.IndexOf('\n',begin)+1)..end]));Require(policy=="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1","actual user policy marker body");
    var qualityManifest=Read(folder+"quality-original-files.json").AsArray();var measurementManifest=Read(data+"original-files.json")["files"]!.AsArray();
    foreach(var (prefix,manifest) in new[]{(original,qualityManifest),(measurementOriginal,measurementManifest)})foreach(var item in manifest){var file=prefix+(string)item!["path"]!;Require(Hash(file)==(string?)item["sha256"]&&new FileInfo(file).Length==(long)item["bytes"]!,"original member manifest");}
    var before=Read(folder+"localjet-full-binaries-before.json").AsArray();var after=Read(folder+"localjet-full-binaries-after.json").AsArray();Require(before.Count==6&&after.Count==6,"six full suite files");
    foreach(var item in before){var path=(string)item!["path"]!;var other=after.Single(x=>(string?)x!["path"]==path)!;var file=folder+"localjet-full-"+Path.GetFileName(path);Require((string?)other["sha256"]==(string?)item["sha256"]&&Hash(file)==(string?)item["sha256"]&&new FileInfo(file).Length==(long)item["bytes"]!,"sealed six suite identities unchanged");}
    foreach(var phase in new[]{"build","test"})Require(File.ReadAllText(folder+"localjet-full-"+phase+".exit").Trim()=="0","original suite raw0");
    XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var focused=new JsonArray();foreach(var (name,pass,fail,raw) in new[]{("focused",183,4,1),("focused-fixed",187,0,0)}){var file=folder+"test-results/"+name+".trx";var document=XDocument.Load(file);var counts=document.Descendants(ns+"Counters").Single();Require((int)counts.Attribute("total")! ==187&&(int)counts.Attribute("passed")! ==pass&&(int)counts.Attribute("failed")! ==fail&&File.ReadAllText(folder+name+".exit").Trim()==raw.ToString(),"original focused result");focused.Add((JsonNode)new JsonObject{["name"]=name,["passed"]=pass,["failed"]=fail,["rawExit"]=raw,["trxSha256"]=Hash(file)});}
    var cost=Read(folder+"cost-copy-binding.json")["files"]!.AsArray();Require(cost.Count==51,"51 copied external trace files");foreach(var row in cost){var file=original+(string)row!["destination"]!;Require(Hash(file)==(string?)row["sha256"]&&new FileInfo(file).Length==(long)row["bytes"]!,"external trace original copied bytes");}
    var costRoot=folder+"accepted-b8-cost-original/temp_docs/accepted-b8-cost/nurbs/";Require(File.ReadAllText(costRoot+"collector.exit").Trim()=="0"&&File.ReadAllText(costRoot+"oracle.exit").Trim()=="2","collector0 does not replace producer2");
    var q=Read(output+"/quality/audit.json");var massProperties=0;foreach(var run in q["runs"]!.AsArray())foreach(var file in Directory.GetFiles(folder+(string)run!["directory"]!+"/evidence","quality-mass-*.json"))foreach(var p in Read(file)["properties"]!.AsArray()){VerifyModulus(p!);massProperties++;}
    var development=new JsonArray();foreach(var (side,commit) in new[]{("baseline",baseline),("candidate",target)})
    {
        var report=Read(folder+"dev-"+side+"/report.json");var sample=report["samples"]!.AsArray().Single()!;var directory=folder+"dev-"+side+"/"+(string)sample["directory"]!;var capture=Read(directory+"/capture.json");var identity=Read(directory+"/identity/loaded.json");Require((string?)identity["calculationCommit"]==commit&&(int)sample["processExit"]! ==2,"development source/raw2");
        var gm=Times(capture,"gm");var pk=Times(capture,"pk");Require(gm.Length==6&&pk.Length==6,"development six pairs");var warm=gm.Skip(1).Order().ToArray();var pairs=new JsonArray();for(var i=0;i<gm.Length;i++)pairs.Add((JsonNode)new JsonObject{["rep"]=i,["phase"]=i==0?"cold":"warm",["gmMs"]=gm[i],["pkMs"]=pk[i]});
        Binary(directory+"/identity/kernel.dll",directory+"/identity/kernel.pdb",commit);
        development.Add((JsonNode)new JsonObject{["side"]=side,["rawExit"]=2,["captureSha256"]=Hash(directory+"/capture.json"),["warmGmMedianMs"]=warm[2],["pairs"]=pairs});
    }
    var measured=Read(output+"/controlled-original/receipt-verified.json");measured.AsObject().Remove("members");var extracts=new JsonArray();foreach(var kind in new[]{"quality","controlled-original"}){var item=Read(output+"/"+kind+"/extraction.json");item.AsObject().Remove("members");extracts.Add(item);}
    var finals=new JsonArray();foreach(var sample in Read(data+"window/report.json")["samples"]!.AsArray())foreach(var item in sample!["artifacts"]!.AsArray())
    {
        var name=(string)item!["path"]!;if(!name.EndsWith(".x_t",StringComparison.Ordinal))continue;var family=(string)sample["case"]!;var role=family switch{"sphere" or "planar"=>name.EndsWith("-managed.x_t",StringComparison.Ordinal)?"GM product":"PK reference product","solidblend"=>"GM product","curved"=>name.Contains("-gm-",StringComparison.Ordinal)?"GM before input":name.Contains("-pk-",StringComparison.Ordinal)?"PK before input":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product","shortedge"=>name.EndsWith("-input.x_t",StringComparison.Ordinal)?"PK seed":name.EndsWith("-input-fresh.x_t",StringComparison.Ordinal)?"GM received fresh input":name.EndsWith("-native-fresh.x_t",StringComparison.Ordinal)?"PK product received/re-exported by GM":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product",_=>throw new Exception("family")};
        var path="temp_docs/localjet-controlled/window/"+(string)sample["directory"]!+"/"+name;Require(Hash(measurementOriginal+path)==(string?)item["sha256"],"final source XT");finals.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=item["sha256"]!.DeepClone(),["source"]=sample["commit"]!.DeepClone(),["role"]=role,["repetition"]=role.EndsWith("product",StringComparison.Ordinal)||role=="PK product received/re-exported by GM"?2:null});
    }
    Require(finals.Count==64&&Hash(data+"window/report.json")=="c13e698e5c5507e3668866945911b9c6fe56fbeeba93a036b3752534fb8fc8cf"&&Hash(data+"material-binding-current.json")=="e2893893de201f670805b0b9961b9563c288f8dadeb8bd878a6e38fbc7ff5aa2","original report/material/final XT set");
    var consumer=output+"/q2-one-nurbs/";var live=Read(consumer+"capture/capture.json");var countsLive=new JsonObject();foreach(var group in live["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!))countsLive[group.Key]=group.Count();
    Require(File.ReadAllText(consumer+"process.exit").Trim()=="2"&&live["captureErrors"]!.AsArray().Count==0&&(int)countsLive["pass"]! ==16&&(int)countsLive["missing"]! ==13,"fresh complete consumer retains raw2/U");
    var actualId=Read(consumer+"capture/identity/loaded.json");foreach(var kind in new[]{"kernel","oracle"})Require(Hash(consumer+"capture/identity/"+kind+".dll")== (string?)actualId[kind+"DllSha256"]&&Hash(consumer+"capture/identity/"+kind+".pdb")== (string?)actualId[kind+"PdbSha256"],"fresh actual loaded bytes");
    var controls=Read(output+"/controls.json");Require(controls["sealedProducts"]!.AsArray().Count==3&&controls["sealedProducts"]!.AsArray().All(x=>(string?)x!["status"]=="Verified"),"three independent sealed PK pairs");
    var executions=new JsonArray();foreach(var (name,raw) in new[]{("extract-01",0),("quality-01",0),("measurement-01",0),("bindings-01",0),("controls-01",134),("controls-02",0),("consumer-driver",2)})executions.Add((JsonNode)new JsonObject{["path"]=output+"/"+name+".log",["sha256"]=Hash(output+"/"+name+".log"),["observedProcessExit"]=raw});
    var result=new JsonObject{["scope"]="Fixed ab0177e concentrated independent Native review; no main/UI/deployment/next optimization",["calculationCommit"]=target,["deliveryCommit"]=delivery,["metadataCommit"]=metadata,["nativeTree"]=native,["srcTree"]=src,["policyBodySha256"]=policy,["policyScope"]="Actual file read only; runtime auto-loading not claimed",["decisions"]=new JsonObject{["mathematicsSourceLifetimeIntegration"]="accepted within BoundSegment stable source scope",["originalGeometryTopologyLifecycle"]="accepted within actual recorded checks",["uniqueWindow"]="accepted as descriptive observations",["allNumericalVerified"]="blocked by original Unresolved/missing",["performanceQualification"]="blocked: original raw2/performanceQualified=false",["generalCausalPerformanceAndOverallPkGoal"]="unproven",["mainIntegrationDeployment"]="not performed"},["archives"]=extracts,["qualityOriginalManifestMembers"]=qualityManifest.Count,["measurementOriginalManifestMembers"]=measurementManifest.Count,["quality"]=q,["originalFocused"]=focused,["externalTraceCopiedFiles"]=cost.Count,["qualityModulusComponentsRecomputed"]=massProperties,["development"]=development,["measurement"]=measured,["controls"]=controls,["q2Consumer"]=new JsonObject{["rawExit"]=2,["checks"]=countsLive,["captureSha256"]=Hash(consumer+"capture/capture.json"),["actualLoadedIdentity"]=actualId,["scope"]="One fresh Linux consumer excluded from original Mac timing"},["finalXtFiles"]=finals,["executions"]=executions};
    Write("docs/localjet-independent/receipt.json",result);Console.WriteLine("PASS independent aggregate; retained original raw1/raw2 and review raw134");return;
}
if(args.SequenceEqual(new[]{"--extract"}))
{
    foreach(var item in new[]{("quality","ec50959c2ec008d5eb2f120d4b13cc8854e812d7c68f61262827be1451df0484",20579749L,531,78700033L),("controlled-original","f0d743e7a0f05147938d9ea8ddd12e03b0451df427c6de56615166493b3ae55c",33988439L,1037,137703906L)})
    {
        var archive="temp_docs/incoming/mac-localjet-ab0177e-"+item.Item1+".zip";var destination=output+"/"+item.Item1+"/original";
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
var original=output+"/quality/original/";var folder=original+"temp_docs/local-spline-jet/";
foreach(var file in new[]{"docs/Local_Spline_Jet_Work.md","docs/Local_Spline_Jet_Quality.md","docs/local-spline-jet/quality-receipt.json","docs/local-spline-jet/source-binary-binding.json"})Require(File.ReadAllBytes(original+file).SequenceEqual(Git("show","85a595edbdd266e9a8b593086bd5535d9e16f375:"+file)),"fixed public document bytes "+file);
var binding=Read(original+"docs/local-spline-jet/source-binary-binding.json");var binaries=new JsonArray();
foreach(var entry in binding["binaries"]!.AsArray())binaries.Add((JsonNode)Binary(original+(string)entry!["dll"]!,original+(string)entry["pdb"]!,target,entry));
var claimed=Read(original+"docs/local-spline-jet/quality-receipt.json");var runs=new JsonArray();var strict=0;var mass=0;
foreach(var run in claimed["qualityRuns"]!.AsArray())
{
    var directory=(string)run!["directory"]!;var path=folder+directory;var capture=Read(path+"/capture.json");Require(Hash(path+"/capture.json")== (string?)run["captureSha256"],"quality capture hash");
    var exitPath=File.Exists(path+"/process.exit")?path+"/process.exit":folder+directory[..^8]+".exit";var raw=int.Parse(File.ReadAllText(exitPath));Require(raw==(int)run["originalExit"]!,"raw quality exit");
    Require(capture["captureErrors"]!.AsArray().Count==0,"capture errors");var counts=new JsonObject();foreach(var g in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!)){counts[g.Key]=g.Count();Require(g.Key!="fail"&&(int)run["checks"]![g.Key]! ==g.Count(),"original check counts");}
    var id=Read(path+"/identity/loaded.json");Require((string?)id["calculationCommit"]==target,"quality actual source");
    foreach(var kind in new[]{"kernel","oracle"}){Require(Hash(path+"/identity/"+kind+".dll")== (string?)id[kind+"DllSha256"]&&Hash(path+"/identity/"+kind+".pdb")== (string?)id[kind+"PdbSha256"],"quality loaded bytes");Require(id.AsObject().Count(x=>x.Value is JsonValue value&&value.TryGetValue<string>(out var text)&&text==(string?)id[kind+"DllSha256"])>=2,"actual loaded hash witness");}
    if(File.Exists(path+"/source.sha256"))foreach(var line in File.ReadLines(path+"/source.sha256"))Require(HashBytes(Git("show",target+":"+line[66..]))==line[..64],"quality source manifest");
    foreach(var meshPath in Directory.GetFiles(path+"/meshes","*.json")){var source=Read(meshPath)["source"]!;if(source["xtArtifact"] is JsonValue artifact)Require(Hash(path+"/"+artifact.GetValue<string>())==(string?)source["xtSha256"]&&(string?)source["commit"]==target,"quality real product source");}
    foreach(var proofPath in Directory.GetFiles(path+"/evidence","quality-*.json"))
    {var proof=Read(proofPath);if(Path.GetFileName(proofPath).StartsWith("quality-compare",StringComparison.Ordinal)){var mask=proof["mask"]!;Require((string?)proof["status"]=="Verified"&&(int)mask["max_diffs"]!>0&&!(bool)mask["all_tests"]!&&!(bool)mask["acc_dev_tests"]!&&(bool)mask["tol_dev_tests"]!,"strict comparison mask/status");strict++;}else mass++;}
    runs.Add((JsonNode)new JsonObject{["directory"]=directory,["rawExit"]=raw,["checks"]=counts,["captureSha256"]=Hash(path+"/capture.json"),["steps"]=capture["steps"]!.AsArray().Count});
}
var trx=Directory.GetFiles(folder,"*.trx",SearchOption.AllDirectories).Single(x=>Hash(x)==(string?)claimed["fullSuite"]!["trxSha256"]);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="2031"&&(string?)counters.Attribute("executed")=="2031"&&(string?)counters.Attribute("passed")=="2031"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","full suite counters");
var receipt=new JsonObject{["scope"]="Independent original quality audit, no Mac code loading and no numerical or performance upgrade",["calculationCommit"]=target,["binaries"]=binaries,["runs"]=runs,["strictReports"]=strict,["massReports"]=mass,["trxSha256"]=Hash(trx),["fullSuiteTotal"]=2031};Write(output+"/quality/audit.json",receipt);Console.WriteLine($"Quality verified: {binaries.Count} PE/PDB, {runs.Count} groups, 2031 original tests, strict={strict}, mass={mass}");
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
static void VerifyModulus(JsonNode property)
{
    var a=(double)property["referenceEstimate"]!;var b=(double)property["actualEstimate"]!;var ra=(double)property["referenceModulus"]!;var rb=(double)property["actualModulus"]!;Require(double.IsFinite(a)&&double.IsFinite(b)&&double.IsFinite(ra)&&double.IsFinite(rb)&&ra>=0&&rb>=0,"valid modulus input");
    var a0=Math.BitDecrement(a-ra);var a1=Math.BitIncrement(a+ra);var b0=Math.BitDecrement(b-rb);var b1=Math.BitIncrement(b+rb);var min=a0<=0&&a1>=0?0:Math.Min(Math.Abs(a0),Math.Abs(a1));var max=Math.Max(Math.Abs(a0),Math.Abs(a1));var gap=Math.Max(0,Math.BitDecrement(Math.Max(a0-b1,b0-a1)));var diameter=Math.BitIncrement(Math.Max(Math.Abs(a0-b1),Math.Abs(a1-b0)));var lower=Math.Max(0,Math.BitDecrement(gap/Math.BitIncrement(1+max)));var upper=Math.BitIncrement(diameter/Math.BitDecrement(1+min));
    Require(lower==(double)property["normalizedLower"]!&&upper==(double)property["normalizedUpper"]!&&(upper<1e-7?"Verified":lower>=1e-7?"Different":"Unresolved")== (string?)property["agreementStatus"],"recomputed modulus bound/status");
}
static double[] Times(JsonNode capture,string kernel){var rows=capture["timings"]!.AsArray().Where(x=>(string?)x!["kernel"]==kernel).OrderBy(x=>(int)x!["step"]!).ToArray();Require(rows.All(x=>(int)x!["error"]! ==0),"API raw0");return rows.Select(x=>(double)x!["ms"]!).ToArray();}
