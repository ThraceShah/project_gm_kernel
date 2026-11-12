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
const string target="3aff4e2b0247bd4f21e74e71d437a33dff1a3e24";
const string output="temp_docs/independent-accepted-integration-3aff4e2b";

if(args.SequenceEqual(new[]{"--final"}))
{
    var executions=new JsonArray();
    foreach(var (file,raw,reason) in new[]{
        ("temp_docs/accepted-integration-independent-extract-01.log",0,"safe extraction"),
        (output+"/quality-01.log",134,"checker wrongly required calculation source instead of actual b99c218e execution"),
        (output+"/quality-02.log",0,"actual quality execution source retained"),
        (output+"/measurement-01.log",0,"unique original measurement audit"),
        (output+"/bindings-01.log",0,"original material/proof bindings"),
        (output+"/controls-01.log",1,"referenced Native test-hook property not propagated"),
        (output+"/controls-02.log",143,"mistaken duplicate build terminated by its own PGID before execution"),
        (output+"/controls-03.log",1,"same missing referenced test-hook property; fixed with global command property"),
        (output+"/controls-04.log",134,"checker compared numeric above-degree zero-fill with analytic interval derivative"),
        (output+"/controls-05.log",134,"same checker failure diagnosed: rational V degree1, derivative2"),
        (output+"/controls-06.log",0,"correct analytic rational point oracle and combination controls"),
        (output+"/aggregate-01.log",0,"original manifests/source/tests and combined receipt"),
        (output+"/subdivision-01.log",134,"one-direction bowl pruned descendants before Improve, so it did not exercise the intended seed branch"),
        (output+"/subdivision-02.log",0,"two-direction bowl exercises actual descendants in all four combinations")})
        executions.Add((JsonNode)new JsonObject{["log"]=file,["sha256"]=Hash(file),["observedRawExit"]=raw,["reason"]=reason,["scope"]="Actual local tool completion, distinct from original producer exits; intermediate failed checker source revisions not individually sealed"});
    var subdivision=Read(output+"/subdivision.json");Require(subdivision.AsArray().Count==4,"four subdivision controls");
    foreach(var row in subdivision.AsArray())Require((string?)row!["status"]=="Success"&&(long)row["searches"]!>5,"actual descendants");
    var files=new JsonArray();foreach(var file in new[]{"scripts/ReviewAcceptedIntegrationEvidence.cs","scripts/ReviewAcceptedIntegrationMeasurement.cs","scripts/ReviewAcceptedIntegrationControls.cs","docs/Accepted_Optimization_Integration_Independent_Review.md","docs/accepted-integration-independent/receipt.json","docs/accepted-integration-independent/material-bindings.json"})files.Add((JsonNode)new JsonObject{["path"]=file,["sha256"]=Hash(file),["bytes"]=new FileInfo(file).Length});
    Require(Hash("temp_docs/incoming/mac-integration-3aff4e2b-quality.zip")=="c2b9e802c32217c9cc3392c108b0bc2684a5df804a46ac57e8c7763777f9e556"&&Hash("temp_docs/incoming/mac-integration-3aff4e2b-controlled-original.zip")=="dbcb4708a9c79e941144deff9e6a55b277f65a72415dab1f874bbda6f47379b6","original archives retained");
    Require(Git("diff","4026e59c827814e52559ec72d41ff282468e3894","--","src","tests").Length==0,"production/tests untouched");
    Write("docs/accepted-integration-independent/final-report-binding.json",new JsonObject{["firstPushedReviewCommit"]="7d4b1bdcb1f22e57d4949ac87ac46de71b6f610d",["files"]=files,["independentExecutions"]=executions,["additionalSubdivisionControls"]=subdivision,["environment"]="Actual ensure ENV_READY before builds; Linux x64 SDK10.0.302/runtime10.0.10/PK38.0.150; PKToy d820719b6d8510d623972ab9d8efc65edbc312f9",["scope"]="One fixed combination review; no fullsuite/family/Mac window rerun, extra fresh modeling producer, AOT, Windows, main, UI or deployment"});
    Console.WriteLine("PASS final source/report binding, original archives and separately observed independent exits");return;
}

if(args.SequenceEqual(new[]{"--receipt"}))
{
    const string baseline="271af03cfefedef12eea19337f1b2b3bca9fe96b",execution="b99c218e3592551e9bc73d5c509712871607e8bd",metadata="a105eb4854e9f138e409a12bcb041e877f254452",delivery="4026e59c827814e52559ec72d41ff282468e3894";
    foreach(var commit in new[]{target,execution,metadata,delivery})
    {
        Require(System.Text.Encoding.UTF8.GetString(Git("rev-parse",commit+":src/ProjectGmKernel.Native")).Trim()=="854d01fec8201646b256acbb8297d3388dd74cdd","fixed Native tree");
        Require(System.Text.Encoding.UTF8.GetString(Git("rev-parse",commit+":src")).Trim()=="545c317e7c6737b397708b80598e45c73aeb3bf5","fixed src tree");
    }
    foreach(var file in new[]{"AGENTS.md","docs/Computational_Kernel_Architecture.md","docs/Intersection_Local_Modeling_Architecture.md"})Require(Git("show",baseline+":"+file).SequenceEqual(File.ReadAllBytes(file))&&Git("show",delivery+":"+file).SequenceEqual(File.ReadAllBytes(file)),"unchanged actual constraints/architecture");
    var policyText=File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex/AGENTS.md"));var begin=policyText.IndexOf("<!-- orca-user-global-policy-20261111:begin -->",StringComparison.Ordinal);var end=policyText.IndexOf("<!-- orca-user-global-policy-20261111:end -->",StringComparison.Ordinal);
    var policy=HashBytes(System.Text.Encoding.UTF8.GetBytes(policyText[(policyText.IndexOf('\n',begin)+1)..end]));Require(policy=="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1","actual policy marker body including final newline");
    var original=output+"/quality/original/";var folder=original+"temp_docs/accepted-integration/";var measured=output+"/controlled-original/original/";var data=measured+"temp_docs/integration-controlled/";
    var manifests=new JsonArray();
    foreach(var (prefix,file,array) in new[]{(original,folder+"quality-original-files.json",true),(measured,data+"original-files.json",false)})
    {
        var node=Read(file);var members=array?node.AsArray():node["files"]!.AsArray();
        foreach(var item in members){var path=prefix+(string)item!["path"]!;Require(Hash(path)==(string?)item["sha256"]&&new FileInfo(path).Length==(long)item["bytes"]!,"original manifest member bytes");}
        manifests.Add((JsonNode)new JsonObject{["file"]=file,["members"]=members.Count,["sha256"]=Hash(file)});
    }
    var tests=new JsonArray();XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    foreach(var (name,total) in new[]{("focused",199),("integration-full",2043)})
    {
        var path=folder+"test-results/"+name+".trx";var doc=XDocument.Load(path);var c=doc.Descendants(ns+"Counters").Single();
        Require((int)c.Attribute("total")! ==total&&(int)c.Attribute("passed")! ==total&&(int)c.Attribute("failed")! ==0&&(int)c.Attribute("notExecuted")! ==0,"original test counters");
        var exit=folder+name+(name=="integration-full"?"-test":"")+".exit";Require(File.ReadAllText(exit).Trim()=="0","original raw test0");
        tests.Add((JsonNode)new JsonObject{["name"]=name,["total"]=total,["rawExit"]=0,["trxSha256"]=Hash(path)});
    }
    Require(File.ReadAllText(folder+"integration-full-build.exit").Trim()=="0","original build0");
    var before=Read(folder+"integration-full-binaries-before.json").AsArray();var after=Read(folder+"integration-full-binaries-after.json").AsArray();Require(before.Count==6&&after.Count==6,"six binary files");
    foreach(var item in before){var path=(string)item!["path"]!;var pair=after.Single(x=>(string?)x!["path"]==path)!;var file=folder+"integration-full-"+Path.GetFileName(path);Require((string?)pair["sha256"]==(string?)item["sha256"]&&Hash(file)==(string?)item["sha256"]&&new FileInfo(file).Length==(long)item["bytes"]!,"six before/after/sealed same identities");}
    var quality=Read(output+"/quality/audit.json");var components=0;
    foreach(var run in quality["runs"]!.AsArray())foreach(var file in Directory.GetFiles(folder+(string)run!["directory"]!+"/evidence","quality-mass-*.json"))foreach(var p in Read(file)["properties"]!.AsArray()){VerifyModulus(p!);components++;}
    var development=new JsonArray();
    foreach(var (side,source) in new[]{("baseline",baseline),("candidate",execution)})
    {
        var report=Read(folder+"dev-"+side+"/report.json");var sample=report["samples"]!.AsArray().Single()!;var dir=folder+"dev-"+side+"/"+(string)sample["directory"]!;var capture=Read(dir+"/capture.json");var id=Read(dir+"/identity/loaded.json");
        Require((string?)id["calculationCommit"]==source&&(int)sample["processExit"]! ==2&&File.ReadAllText(dir+"/process.exit").Trim()=="2","actual development source and producer2");
        var gm=Times(capture,"gm");var pk=Times(capture,"pk");Require(gm.Length==6&&pk.Length==6,"six development pairs");
        foreach(var line in File.ReadLines(dir+"/source.sha256"))Require(HashBytes(Git("show",source+":"+line[66..]))==line[..64],"development original source snapshot");
        var pairs=new JsonArray();for(var i=0;i<6;i++)pairs.Add((JsonNode)new JsonObject{["rep"]=i,["phase"]=i==0?"cold":"warm",["gmMs"]=gm[i],["pkMs"]=pk[i]});
        development.Add((JsonNode)new JsonObject{["side"]=side,["actualExecutionSource"]=source,["rawExit"]=2,["captureSha256"]=Hash(dir+"/capture.json"),["warmMedianMs"]=gm.Skip(1).Order().ElementAt(2),["pairs"]=pairs,["binary"]=Binary(dir+"/identity/kernel.dll",dir+"/identity/kernel.pdb",source)});
    }
    Require(Hash(data+"window/report.json")=="cb958f38a7c24d1890c9b76e64ddf4a9f4ab4aad4b0bd77b151d446bfd5999b2"&&Hash(data+"material-binding-current.json")=="a621f72a70bf040bb9e61bdf03fa0a22f20cea56f9770806d2da6beaa6ebdca3","report/material fixed identity");
    var extracts=new JsonArray();foreach(var kind in new[]{"quality","controlled-original"}){var item=Read(output+"/"+kind+"/extraction.json");item.AsObject().Remove("members");extracts.Add(item);}
    var measurement=Read(output+"/controlled-original/receipt-verified.json");measurement.AsObject().Remove("members");
    Write("docs/accepted-integration-independent/receipt.json",new JsonObject{["calculationCommit"]=target,["actualQualityExecutionSource"]=execution,["windowSource"]=metadata,["deliveryCommit"]=delivery,["baseline"]=baseline,["nativeTree"]="854d01fec8201646b256acbb8297d3388dd74cdd",["policyBodySha256"]=policy,["policyScope"]="Actual file read only; automatic runtime loading not established",["archives"]=extracts,["originalManifests"]=manifests,["originalTests"]=tests,["testFileScope"]="Six files before/after/sealed equal; no added test loader/start/end observation",["quality"]=quality,["qualityMassComponentsRecomputed"]=components,["development"]=development,["measurement"]=measurement,["controls"]=Read(output+"/controls.json"),["decisions"]=new JsonObject{["fixedNativeCombinationAndKeyConsumers"]="accepted within stated conservative contracts",["originalQuality"]="accepted as layered geometric/topological/lifecycle evidence; original missing retained",["uniqueWindow"]="accepted as descriptive observation only",["completeNumericalQualification"]="blocked by original Unresolved/missing",["performanceQualified"]="blocked by original producer2/runner2/false",["generalCausalCpuOverallPkGoal"]="unproven",["ab597MainUiDeploymentNextOptimization"]="not performed / not accepted"}});
    Console.WriteLine("PASS originals, actual execution identities, independent combination and unique descriptive window");return;
}
if(args.SequenceEqual(new[]{"--extract"}))
{
    foreach(var item in new[]{("quality","c2b9e802c32217c9cc3392c108b0bc2684a5df804a46ac57e8c7763777f9e556",18552422L,469,69933277L),("controlled-original","dbcb4708a9c79e941144deff9e6a55b277f65a72415dab1f874bbda6f47379b6",34012773L,1044,137748725L)})
    {
        var archive="temp_docs/incoming/mac-integration-3aff4e2b-"+item.Item1+".zip";var destination=output+"/"+item.Item1+"/original";
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
var original=output+"/quality/original/";var folder=original+"temp_docs/accepted-integration/";
foreach(var file in new[]{"docs/Accepted_Optimization_Integration_Work.md","docs/Accepted_Optimization_Integration_Quality.md","docs/accepted-integration/quality-receipt.json","docs/accepted-integration/source-binary-binding.json"})Require(File.ReadAllBytes(original+file).SequenceEqual(Git("show","a105eb4854e9f138e409a12bcb041e877f254452:"+file)),"fixed public document bytes "+file);
var binding=Read(original+"docs/accepted-integration/source-binary-binding.json");var binaries=new JsonArray();
foreach(var entry in binding["binaries"]!.AsArray())binaries.Add((JsonNode)Binary(original+(string)entry!["dll"]!,original+(string)entry["pdb"]!,"b99c218e3592551e9bc73d5c509712871607e8bd",entry));
var claimed=Read(original+"docs/accepted-integration/quality-receipt.json");var runs=new JsonArray();var strict=0;var mass=0;
foreach(var run in claimed["qualityRuns"]!.AsArray())
{
    var directory=(string)run!["directory"]!;var path=folder+directory;var capture=Read(path+"/capture.json");Require(Hash(path+"/capture.json")== (string?)run["captureSha256"],"quality capture hash");
    var exitPath=File.Exists(path+"/process.exit")?path+"/process.exit":folder+directory[..^8]+".exit";var raw=int.Parse(File.ReadAllText(exitPath));Require(raw==(int)run["originalExit"]!,"raw quality exit");
    Require(capture["captureErrors"]!.AsArray().Count==0,"capture errors");var counts=new JsonObject();foreach(var g in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!)){counts[g.Key]=g.Count();Require(g.Key!="fail"&&(int)run["checks"]![g.Key]! ==g.Count(),"original check counts");}
    var id=Read(path+"/identity/loaded.json");Require((string?)id["calculationCommit"]=="b99c218e3592551e9bc73d5c509712871607e8bd","quality actual source");
    foreach(var kind in new[]{"kernel","oracle"}){Require(Hash(path+"/identity/"+kind+".dll")== (string?)id[kind+"DllSha256"]&&Hash(path+"/identity/"+kind+".pdb")== (string?)id[kind+"PdbSha256"],"quality loaded bytes");Require(id.AsObject().Count(x=>x.Value is JsonValue value&&value.TryGetValue<string>(out var text)&&text==(string?)id[kind+"DllSha256"])>=2,"actual loaded hash witness");}
    if(File.Exists(path+"/source.sha256"))foreach(var line in File.ReadLines(path+"/source.sha256"))Require(HashBytes(Git("show",target+":"+line[66..]))==line[..64],"quality source manifest");
    foreach(var meshPath in Directory.GetFiles(path+"/meshes","*.json")){var source=Read(meshPath)["source"]!;if(source["xtArtifact"] is JsonValue artifact)Require(Hash(path+"/"+artifact.GetValue<string>())==(string?)source["xtSha256"]&&(string?)source["commit"]==(string?)id["calculationCommit"],"quality real product actual execution source");}
    foreach(var proofPath in Directory.GetFiles(path+"/evidence","quality-*.json"))
    {var proof=Read(proofPath);if(Path.GetFileName(proofPath).StartsWith("quality-compare",StringComparison.Ordinal)){var mask=proof["mask"]!;Require((string?)proof["status"]=="Verified"&&(int)mask["max_diffs"]!>0&&!(bool)mask["all_tests"]!&&!(bool)mask["acc_dev_tests"]!&&(bool)mask["tol_dev_tests"]!,"strict comparison mask/status");strict++;}else mass++;}
    runs.Add((JsonNode)new JsonObject{["directory"]=directory,["rawExit"]=raw,["checks"]=counts,["captureSha256"]=Hash(path+"/capture.json"),["steps"]=capture["steps"]!.AsArray().Count});
}
var trx=Directory.GetFiles(folder,"*.trx",SearchOption.AllDirectories).Single(x=>Hash(x)==(string?)claimed["fullSuite"]!["trxSha256"]);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="2043"&&(string?)counters.Attribute("executed")=="2043"&&(string?)counters.Attribute("passed")=="2043"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","full suite counters");
var receipt=new JsonObject{["scope"]="Independent original quality audit, no Mac code loading and no numerical or performance upgrade",["calculationCommit"]=target,["binaries"]=binaries,["runs"]=runs,["strictReports"]=strict,["massReports"]=mass,["trxSha256"]=Hash(trx),["fullSuiteTotal"]=2043};Write(output+"/quality/audit.json",receipt);Console.WriteLine($"Quality verified: {binaries.Count} PE/PDB, {runs.Count} groups, 2043 original tests, strict={strict}, mass={mass}");
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
