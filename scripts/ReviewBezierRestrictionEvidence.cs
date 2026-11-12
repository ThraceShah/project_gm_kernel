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
const string target="cccaa5e2943f8f2515dd01b02c31492378502040";
const string output="temp_docs/independent-bezier-restriction-cccaa5e2";
if(args.SequenceEqual(new[]{"--receipt"}))
{
    const string baseline="1065e7c8f818ce10f6d3ee3edfe8f8bc33f3d582",fixture="0cc4363d82879392a92dd06a405923812b34695c",metadata="54cefeec76f7a61f264140e03c3bd13b695347c6",delivery="271af03cfefedef12eea19337f1b2b3bca9fe96b";
    foreach(var commit in new[]{target,fixture,metadata,delivery})
    {
        Require(System.Text.Encoding.UTF8.GetString(Git("rev-parse",commit+":src/ProjectGmKernel.Native")).Trim()=="4f4f92e065fea03bf7384b9a80fa6836326e6322","fixed Native tree");
        Require(System.Text.Encoding.UTF8.GetString(Git("rev-parse",commit+":src")).Trim()=="5f4b4afea064d457af9d09b3f39d7f420e519db6","fixed src tree");
    }
    foreach(var file in new[]{"AGENTS.md","docs/Computational_Kernel_Architecture.md","docs/Intersection_Local_Modeling_Architecture.md"})Require(Git("show",baseline+":"+file).SequenceEqual(File.ReadAllBytes(file))&&Git("show",delivery+":"+file).SequenceEqual(File.ReadAllBytes(file)),"unchanged actual architecture");
    var global=File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex/AGENTS.md"));var begin=global.IndexOf("<!-- orca-user-global-policy-20261111:begin -->",StringComparison.Ordinal);var end=global.IndexOf("<!-- orca-user-global-policy-20261111:end -->",StringComparison.Ordinal);var policy=HashBytes(System.Text.Encoding.UTF8.GetBytes(global[(global.IndexOf('\n',begin)+1)..end]));Require(policy=="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1","actual policy marker body");
    var original=output+"/quality/original/";var folder=original+"temp_docs/bezier-restriction/";var measuredOriginal=output+"/controlled-original/original/";var data=measuredOriginal+"temp_docs/restriction-controlled/";
    var manifests=new JsonArray();foreach(var (prefix,file,array) in new[]{(original,folder+"quality-original-files.json",true),(measuredOriginal,data+"original-files.json",false)})
    {var node=Read(file);var members=array?node.AsArray():node["files"]!.AsArray();foreach(var item in members){var path=prefix+(string)item!["path"]!;Require(Hash(path)==(string?)item["sha256"]&&new FileInfo(path).Length==(long)item["bytes"]!,"original manifest bytes");}manifests.Add((JsonNode)new JsonObject{["file"]=file,["members"]=members.Count,["sha256"]=Hash(file)});}
    var suiteRows=new JsonArray();XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    foreach(var (name,total,pass,fail,raw) in new[]{("focused",195,195,0,0),("cache-fixed",2,2,0,0),("restriction-full",2039,2038,1,1),("restriction-full-fixed",2039,2039,0,0)})
    {
        var path=folder+"test-results/"+name+".trx";var doc=XDocument.Load(path);var c=doc.Descendants(ns+"Counters").Single();Require((int)c.Attribute("total")! ==total&&(int)c.Attribute("passed")! ==pass&&(int)c.Attribute("failed")! ==fail&&(int)c.Attribute("notExecuted")! ==0,"original suite counters");
        var exit=folder+name+(name.StartsWith("restriction-full",StringComparison.Ordinal)?"-test":"")+".exit";Require(File.ReadAllText(exit).Trim()==raw.ToString(),"original test raw exit");
        var failures=new JsonArray();foreach(var row in doc.Descendants(ns+"UnitTestResult").Where(x=>(string?)x.Attribute("outcome")=="Failed"))failures.Add((JsonNode)new JsonObject{["name"]=(string?)row.Attribute("testName"),["message"]=row.Descendants(ns+"Message").Single().Value,["sourceEvidence"]=path});
        suiteRows.Add((JsonNode)new JsonObject{["name"]=name,["passed"]=pass,["failed"]=fail,["rawExit"]=raw,["trxSha256"]=Hash(path),["failures"]=failures});
    }
    var firstBinaries=new JsonArray();foreach(var item in Read("docs/bezier-restriction/initial-failed-source-binary-binding.json")["binaries"]!.AsArray())firstBinaries.Add((JsonNode)Binary(original+(string)item!["dll"]!,original+(string)item["pdb"]!,target,item));
    foreach(var phase in new[]{"restriction-full","restriction-full-fixed"})
    {
        Require(File.ReadAllText(folder+phase+"-build.exit").Trim()=="0","original build0");var before=Read(folder+phase+"-binaries-before.json").AsArray();var after=Read(folder+phase+"-binaries-after.json").AsArray();Require(before.Count==6&&after.Count==6,"six test files");
        foreach(var item in before){var path=(string)item!["path"]!;var pair=after.Single(x=>(string?)x!["path"]==path)!;var file=folder+phase+"-"+Path.GetFileName(path);Require((string?)pair["sha256"]==(string?)item["sha256"]&&Hash(file)==(string?)item["sha256"]&&new FileInfo(file).Length==(long)item["bytes"]!,"six original before-after identities");}
    }
    var quality=Read(output+"/quality/audit.json");var components=0;foreach(var run in quality["runs"]!.AsArray())foreach(var file in Directory.GetFiles(folder+(string)run!["directory"]!+"/evidence","quality-mass-*.json"))foreach(var p in Read(file)["properties"]!.AsArray()){VerifyModulus(p!);components++;}
    var development=new JsonArray();foreach(var (side,source) in new[]{("baseline",baseline),("candidate",target)})
    {
        var report=Read(folder+"dev-"+side+"/report.json");var sample=report["samples"]!.AsArray().Single()!;var dir=folder+"dev-"+side+"/"+(string)sample["directory"]!;var capture=Read(dir+"/capture.json");var id=Read(dir+"/identity/loaded.json");Require((string?)id["calculationCommit"]==source&&(int)sample["processExit"]! ==2,"development identity/raw2");var gm=Times(capture,"gm");var pk=Times(capture,"pk");Require(gm.Length==6&&pk.Length==6,"development six fresh pairs");var pairs=new JsonArray();for(var i=0;i<6;i++)pairs.Add((JsonNode)new JsonObject{["rep"]=i,["phase"]=i==0?"cold":"warm",["gmMs"]=gm[i],["pkMs"]=pk[i]});development.Add((JsonNode)new JsonObject{["side"]=side,["rawExit"]=2,["captureSha256"]=Hash(dir+"/capture.json"),["warmMedianMs"]=gm.Skip(1).Order().ElementAt(2),["pairs"]=pairs,["binary"]=Binary(dir+"/identity/kernel.dll",dir+"/identity/kernel.pdb",source)});
    }
    var cost=Read(folder+"cost-copy-binding.json")["files"]!.AsArray();Require(cost.Count==56,"actual copied trace snapshot");foreach(var row in cost){var file=original+(string)row!["destination"]!;Require(Hash(file)==(string?)row["sha256"]&&new FileInfo(file).Length==(long)row["bytes"]!,"copied trace bytes");}
    Require(Hash(data+"window/report.json")=="c9a838b50dbe9c6234f7873e5f831fd6766754f5296b1942b738e796b4db538b"&&Hash(data+"material-binding-current.json")=="e64ed139b9e18c4f8dd8b281c91ea34c1c4b82f27e6307f4ef7a48b743c1b744","original report/material identity");
    var extracts=new JsonArray();foreach(var kind in new[]{"quality","controlled-original"}){var item=Read(output+"/"+kind+"/extraction.json");item.AsObject().Remove("members");extracts.Add(item);}
    var measurement=Read(output+"/controlled-original/receipt-verified.json");measurement.AsObject().Remove("members");
    Write("docs/bezier-restriction-independent/receipt.json",new JsonObject{["calculationCommit"]=target,["fixtureCommit"]=fixture,["deliveryCommit"]=delivery,["policyBodySha256"]=policy,["policyScope"]="Actually read; runtime automatic loading not established",["archives"]=extracts,["originalManifests"]=manifests,["originalTests"]=suiteRows,["initialFailedBinaries"]=firstBinaries,["quality"]=quality,["qualityModulusComponentsRecomputed"]=components,["development"]=development,["copiedTraceSnapshotFiles"]=cost.Count,["measurement"]=measurement,["controls"]=Read(output+"/controls.json"),["claims"]="Finite Native mathematical/source/integration acceptance and descriptive observations only; original U/raw1/raw2 remain; no main, deployment, universal CPU or PK performance acceptance"});Console.WriteLine("PASS aggregate originals, source trees, raw failures, controls and one window");return;
}
if(args.SequenceEqual(new[]{"--extract"}))
{
    foreach(var item in new[]{("quality","4fa26c33cff4fbd130c4a598b2fa35b7c9fcf9965e4ed5979c717fb8f4820542",25308629L,566,98147963L),("controlled-original","f4b86e3473b79ed13ee6732d047172a1fc8af806d9dae93bb6c787a1b3491645",34012458L,1049,137745912L)})
    {
        var archive="temp_docs/incoming/mac-restriction-cccaa5e2-"+item.Item1+".zip";var destination=output+"/"+item.Item1+"/original";
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
var original=output+"/quality/original/";var folder=original+"temp_docs/bezier-restriction/";
foreach(var file in new[]{"docs/Bezier_Restriction_Work.md","docs/Bezier_Restriction_Quality.md","docs/bezier-restriction/quality-receipt.json","docs/bezier-restriction/source-binary-binding.json"})Require(File.ReadAllBytes(original+file).SequenceEqual(Git("show","54cefeec76f7a61f264140e03c3bd13b695347c6:"+file)),"fixed public document bytes "+file);
var binding=Read(original+"docs/bezier-restriction/source-binary-binding.json");var binaries=new JsonArray();
foreach(var entry in binding["binaries"]!.AsArray())binaries.Add((JsonNode)Binary(original+(string)entry!["dll"]!,original+(string)entry["pdb"]!,"0cc4363d82879392a92dd06a405923812b34695c",entry));
var claimed=Read(original+"docs/bezier-restriction/quality-receipt.json");var runs=new JsonArray();var strict=0;var mass=0;
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
var trx=Directory.GetFiles(folder,"*.trx",SearchOption.AllDirectories).Single(x=>Hash(x)==(string?)claimed["fullSuite"]!["trxSha256"]);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="2039"&&(string?)counters.Attribute("executed")=="2039"&&(string?)counters.Attribute("passed")=="2039"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","full suite counters");
var receipt=new JsonObject{["scope"]="Independent original quality audit, no Mac code loading and no numerical or performance upgrade",["calculationCommit"]=target,["binaries"]=binaries,["runs"]=runs,["strictReports"]=strict,["massReports"]=mass,["trxSha256"]=Hash(trx),["fullSuiteTotal"]=2039};Write(output+"/quality/audit.json",receipt);Console.WriteLine($"Quality verified: {binaries.Count} PE/PDB, {runs.Count} groups, 2039 original tests, strict={strict}, mass={mass}");
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

