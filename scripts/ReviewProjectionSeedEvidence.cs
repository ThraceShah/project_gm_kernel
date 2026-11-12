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
const string target="a9085ed12bf811cd337bbf2f69964d0cd3957373";
const string output="temp_docs/independent-projection-seed-a9085ed";
if(args.SequenceEqual(new[]{"--supplement"}))
{
    var receipt=Read("docs/projection-seed-independent/receipt.json");var corrected=Read(output+"/bounds-verified.json");var old=receipt["controls"]!["privateOriginalBounds"]!;
    Require(JsonNode.DeepEquals(old["lower"],corrected["lower"])&&JsonNode.DeepEquals(old["upper"],corrected["upper"]),"unchanged original private bound values");
    var logs=new JsonArray();foreach(var (name,exit) in new[]{("extract",0),("quality-audit",0),("measurement-audit",0),("material-audit",0),("focused-driver",0),("consumer-driver",2),("controls-attempt1",134),("controls-attempt2",134),("controls-attempt3",0),("receipt-attempt1",134),("receipt-attempt2",0),("bounds-verified",0),("bounds-final-source",0)})
    {var path=output+"/"+name+".log";logs.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=Hash(path),["observedRawExit"]=exit,["exitProvenance"]="direct exec/wait tool result; child process.exit also retained where applicable"});}
    var scripts=new JsonArray();foreach(var path in new[]{"scripts/ReviewProjectionSeedEvidence.cs","scripts/ReviewProjectionSeedMeasurement.cs","scripts/ReviewProjectionSeedRuntime.cs","scripts/ReviewProjectionSeedControls.cs"})scripts.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=Hash(path)});
    var result=new JsonObject{["scope"]="Final review supplement; earlier generated evidence bytes retained",["priorReceiptSha256"]=Hash("docs/projection-seed-independent/receipt.json"),["reportSha256"]=Hash("docs/Projection_Seed_Independent_Review.md"),["policyMarkerBodySha256"]="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1",["policyScope"]="actual file read/hash, not automatic runtime loading",["decisions"]=new JsonObject{["mathematicsNativeConsumers"]="accepted within existing bounded-failure and certification contracts",["originalQuality"]="accepted as executed, missing retained",["uniqueWindow"]="accepted descriptive only",["allNumericalVerified"]="blocked: 684 original Unresolved components",["performanceQualified"]="blocked: original raw2/false",["universalCausalCpuAndOverallPkGoal"]="unproven",["abCombinationMainPortal"]="not executed"},["boundControlCorrection"]=new JsonObject{["supersedes"]="only initial privateOriginalBounds exact-minimum control claim; initial numeric outputs and logs unchanged",["oldWeakMinimumSquared"]=1.0625,["corrected"]=corrected,["executionRawExit"]=0,["finalSourceRecheckRawExit"]=0},["executions"]=logs,["finalScripts"]=scripts};
    Write("docs/projection-seed-independent/supplement.json",result);Console.WriteLine("PASS final supplement: preserved evidence, corrected dyadic bound control, raw execution exits");return;
}
if(args.SequenceEqual(new[]{"--extract"}))
{
    foreach(var item in new[]{("quality","d8e1895ca86a1f29737e67e1fe2d568876452de1950ba6b23d2122d998666a09",20526861L,524,78395422L),("controlled-original","5b0adf8f1d8b76ed5e9ac80883de7efd5890417260885bf107fb9b4dfe20808a",33987355L,1049,137713130L)})
    {
        var archive="temp_docs/incoming/mac-projection-seed-a9085ed-"+item.Item1+".zip";var destination=output+"/"+item.Item1+"/original";
        Require(!Directory.Exists(destination),"preserve extraction");Require(new FileInfo(archive).Length==item.Item3&&Hash(archive)==item.Item2,"archive bytes");
        using var zip=ZipFile.OpenRead(archive);var names=new HashSet<string>(StringComparer.Ordinal);var members=new JsonArray();long expanded=0;
        foreach(var entry in zip.Entries)
        {
            var name=entry.FullName;Require(!Path.IsPathRooted(name)&&!name.Contains('\\')&&!name.Contains(':')&&!name.Split('/').Any(x=>x is ".." or ".")&&names.Add(name)&&((entry.ExternalAttributes>>16)&0xf000)!=0xa000,"safe unique member");
            var path=Path.Combine(destination,name);Require(Path.GetFullPath(path).StartsWith(Path.GetFullPath(destination)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"isolated member");
            if(name.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);using(var source=entry.Open())using(var file=new FileStream(path,FileMode.CreateNew))source.CopyTo(file);
            using var check=entry.Open();Require(new FileInfo(path).Length==entry.Length&&Hash(path)==Convert.ToHexStringLower(SHA256.HashData(check)),"member bytes");expanded+=entry.Length;
            members.Add((JsonNode)new JsonObject{["path"]=name,["bytes"]=entry.Length,["sha256"]=Hash(path)});
        }
        Require(names.Count==item.Item4&&expanded==item.Item5,"archive dimensions");Write(output+"/"+item.Item1+"/extraction.json",new JsonObject{["archive"]=archive,["sha256"]=Hash(archive),["entries"]=names.Count,["expandedBytes"]=expanded,["members"]=members});
        Console.WriteLine($"{item.Item1}: {names.Count} members / {expanded} bytes verified");
    }
    return;
}
if(args.SequenceEqual(new[]{"--receipt"}))
{
    var original=output+"/quality/original/";var qroot=original+"temp_docs/projection-seed/";var moriginal=output+"/controlled-original/original/";var mroot=moriginal+"temp_docs/seed-controlled/";
    var manifests=new JsonArray();
    foreach(var item in new[]{(original,qroot+"quality-original-files.json",false),(moriginal,mroot+"original-files.json",true)})
    {
        var manifest=Read(item.Item2);var entries=(item.Item3?manifest["files"]!:manifest).AsArray();foreach(var entry in entries){var path=item.Item1+(string)entry!["path"]!;Require(Hash(path)==(string?)entry["sha256"]&&new FileInfo(path).Length==(long)entry["bytes"]!,"original manifest member");}
        manifests.Add((JsonNode)new JsonObject{["path"]=item.Item2,["sha256"]=Hash(item.Item2),["verifiedMembers"]=entries.Count});
    }
    var binaries=Read(qroot+"seed-full-binaries-before.json");var after=Read(qroot+"seed-full-binaries-after.json");Require(binaries.AsArray().Count==6&&after.AsArray().Count==6,"six suite files");
    foreach(var item in binaries.AsArray()){var path=(string)item!["path"]!;var match=after.AsArray().Single(x=>(string?)x!["path"]==path)!;Require((string?)match["sha256"]==(string?)item["sha256"]&&Hash(qroot+"seed-full-"+Path.GetFileName(path))==(string?)item["sha256"],"before/after/sealed same bytes");}
    foreach(var phase in new[]{"build","test"})Require(File.ReadAllText(qroot+"seed-full-"+phase+".exit").Trim()=="0","original suite raw0");Require(File.ReadAllText(qroot+"focused.exit").Trim()=="0","original focused raw0");
    var copy=Read(qroot+"cost-copy-binding.json");foreach(var item in copy["files"]!.AsArray()){var path=original+(string)item!["destination"]!;Require(Hash(path)==(string?)item["sha256"]&&new FileInfo(path).Length==(long)item["bytes"]!,"diagnostic preserved bytes");}
    var development=new JsonArray();
    foreach(var side in new[]{"baseline","candidate"})
    {
        var folder=qroot+"dev-"+side+"/00-solidblend-candidate";var capture=Read(folder+"/capture.json");Require(capture["steps"]!.AsArray().Count==6&&capture["captureErrors"]!.AsArray().Count==0,"six fresh development pairs");
        var row=new JsonObject{["side"]=side};foreach(var kernel in new[]{"gm","pk"}){var times=capture["timings"]!.AsArray().Where(x=>(string?)x!["kernel"]==kernel).OrderBy(x=>(int)x!["step"]!).ToArray();Require(times.Length==6&&times.All(x=>(int)x!["error"]! ==0),"development API0");row[kernel+"Ms"]=new JsonArray(times.Select(x=>x!["ms"]!.DeepClone()).ToArray());}
        var claimed=Read(qroot+"development-comparison.json")["observations"]!.AsArray().Single(x=>(string?)x!["side"]==side)!;
        foreach(var kernel in new[]{"gm","pk"})Require(JsonNode.DeepEquals(row[kernel+"Ms"],claimed[kernel+"Ms"]),"development raw times");Require(Hash(folder+"/oracle.log")== (string?)claimed["oracleLogSha256"]&&Hash(folder+"/capture.json")== (string?)claimed["captureSha256"],"development logs/capture");
        var gm=row["gmMs"]!.AsArray().Skip(1).Select(x=>(double)x!).Order().ToArray();Require(gm[2]==(double)claimed["warmGmMedianMs"]!,"development five warm median");row["warmMedianMs"]=gm[2];row["rawExit"]=2;development.Add((JsonNode)row);
    }
    var strictCounts=new Dictionary<string,int>();var conditions=0;var diagnostics=0;
    foreach(var root in new[]{qroot,mroot+"window/"})foreach(var file in Directory.GetFiles(root,"quality-compare-*.json",SearchOption.AllDirectories))
    {
        var proof=Read(file);var mask=proof["mask"]!;Require((string?)proof["status"]=="Verified"&&(int)mask["max_diffs"]!>0&&!(bool)mask["all_tests"]!&&!(bool)mask["acc_dev_tests"]!&&(bool)mask["tol_dev_tests"]!,"strict actual mask");
        foreach(var name in new[]{"num_tests","class_tests","sense_tests","tol_tests","missing_tests","non_match_tests"})Require((bool)mask[name]!,"strict structural mask "+name);
        Require((int)proof["globalResult"]! ==21921,"global no differences");
        foreach(var diff in proof["localDiffs"]!.AsArray())
        {
            if((bool)diff!["diagnostic"]!){Require((int)diff["type"]! ==21949,"chart-only diagnostic");diagnostics++;continue;}
            var finite=diff["conditionalFiniteProof"]!;Require((bool)finite["proved"]!&&(int)diff["type"]! ==21959,"specific finite curve-sense proof");conditions++;
        }
        strictCounts[root]=strictCounts.GetValueOrDefault(root)+1;
    }
    var quality=Read(output+"/quality/audit.json");var measurement=Read(output+"/controlled-original/receipt-verified.json");measurement.AsObject().Remove("members");
    Require(Hash(mroot+"window/report.json")=="08e736d3887045ef18b2a24f2b8e071a0c4336fde45d6c998d80c3e094eae1ec"&&Hash(mroot+"material-binding-current.json")=="87bc58b9d6e1f8e96434da61bc440cb6a5e4bd5e02ef76404c99defcc0325913","report/material bytes");
    var artifacts=new JsonArray();foreach(var sample in Read(mroot+"window/report.json")["samples"]!.AsArray())foreach(var item in sample!["artifacts"]!.AsArray())
    {
        var path=(string)item!["path"]!;if(!path.EndsWith(".x_t"))continue;var family=(string)sample["case"]!;
        var role=family switch{"sphere" or "planar"=>path.EndsWith("-managed.x_t")?"GM product":"PK reference product","solidblend"=>"GM product","curved"=>path.Contains("-gm-")?"GM input":path.Contains("-pk-")?"PK input":path.EndsWith("-native.x_t")?"PK reference product":"GM product","shortedge"=>path.EndsWith("-input.x_t")?"PK seed":path.EndsWith("-input-fresh.x_t")?"GM received input":path.EndsWith("-native-fresh.x_t")?"PK product received/re-exported via GM control":path.EndsWith("-native.x_t")?"PK reference product":"GM product",_=>throw new Exception("family")};
        Require(Hash(mroot+"window/"+(string)sample["directory"]!+"/"+path)==(string?)item["sha256"],"final artifact hash");artifacts.Add((JsonNode)new JsonObject{["directory"]=sample["directory"]!.DeepClone(),["path"]=path,["role"]=role,["sha256"]=item["sha256"]!.DeepClone(),["scope"]=role.Contains("input")||role=="PK seed"?"input, no product repetition claimed":"original final-file set; repetition from producer overwrite behavior, no invented file identity"});
    }
    var q2=output+"/linux-one-nurbs/";Require(File.ReadAllText(q2+"process.exit").Trim()=="2","independent consumer raw2");var fresh=Read(q2+"capture/capture.json");Require(fresh["captureErrors"]!.AsArray().Count==0&&!fresh["checks"]!.AsArray().Any(x=>(string?)x!["status"]=="fail"),"fresh no hard failure");
    var checks=new JsonObject();foreach(var g in fresh["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!))checks[g.Key]=g.Count();
    var focused=output+"/linux-focused/";Require(File.ReadAllText(focused+"process.exit").Trim()=="0","independent focused raw0");XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(focused+"focused.trx").Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="38"&&(string?)counters.Attribute("passed")=="38"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","focused counters");
    var executions=new JsonArray();foreach(var file in Directory.GetFiles(output,"*.log")){executions.Add((JsonNode)new JsonObject{["path"]=file,["sha256"]=Hash(file)});}
    var extraction=new JsonArray();foreach(var kind in new[]{"quality","controlled-original"}){var item=Read(output+"/"+kind+"/extraction.json");item.AsObject().Remove("members");extraction.Add(item);}
    var result=new JsonObject{["scope"]="Independent a9085ed complete package review; algorithms and original descriptive evidence only, no author changes/main/deployment/overall PK qualification",["calculationCommit"]=target,["baselineCommit"]="b8c74b95047577e52d1086b48b0b0a88ee3f4388",["metadataCommit"]="1e59c305c6e28c84ff8bd1090bcf9a33169a261d",["deliveryCommit"]="ea5a27b38de6abf766200a58dccae65596d9e996",["nativeTree"]="101e5ff835ac883884367d90979f9bba8bd7ea23",["srcTree"]="e981af6722fc2cefb282a4ef32d105b35b60b537",["archives"]=extraction,["originalManifests"]=manifests,["quality"]=quality,["measurement"]=measurement,["originalSuiteBeforeAfter"]=binaries,["preservedCostDiagnosticFiles"]=copy["files"]!.AsArray().Count,["developmentSeparateFromWindow"]=development,["strictAudit"]=new JsonObject{["qualityReports"]=strictCounts[qroot],["windowReports"]=strictCounts[mroot+"window/"],["chartDiagnostics"]=diagnostics,["conditionalFiniteCurveSenseProofs"]=conditions},["controls"]=Read(output+"/controls.json"),["focused"]=new JsonObject{["exit"]=0,["passed"]=38,["total"]=38,["trxSha256"]=Hash(focused+"focused.trx")},["freshConsumer"]=new JsonObject{["rawExit"]=2,["checks"]=checks,["captureSha256"]=Hash(q2+"capture/capture.json"),["scope"]="one Linux fresh complete consumer, excluded from Mac times"},["finalXtArtifacts"]=artifacts,["materialBindingsSha256"]=Hash("docs/projection-seed-independent/material-bindings.json"),["executions"]=executions};
    Write("docs/projection-seed-independent/receipt.json",result);Console.WriteLine($"PASS supplemental manifest/suite/development/strict audit: conditions={conditions}, diagnostics={diagnostics}, finalXT={artifacts.Count}; fresh "+checks.ToJsonString());
    return;
}
Require(args.SequenceEqual(new[]{"--quality"}),"--extract or --quality");
{
var original=output+"/quality/original/";var folder=original+"temp_docs/projection-seed/";
foreach(var file in new[]{"docs/Projection_Seed_Work.md","docs/Projection_Seed_Quality.md","docs/projection-seed/quality-receipt.json","docs/projection-seed/source-binary-binding.json"})Require(File.ReadAllBytes(original+file).SequenceEqual(Git("show","1e59c305c6e28c84ff8bd1090bcf9a33169a261d:"+file)),"fixed public document bytes "+file);
var binding=Read(original+"docs/projection-seed/source-binary-binding.json");var binaries=new JsonArray();
foreach(var entry in binding["binaries"]!.AsArray())binaries.Add((JsonNode)Binary(original+(string)entry!["dll"]!,original+(string)entry["pdb"]!,target,entry));
var claimed=Read(original+"docs/projection-seed/quality-receipt.json");var runs=new JsonArray();var strict=0;var mass=0;
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
var trx=Directory.GetFiles(folder,"*.trx",SearchOption.AllDirectories).Single(x=>Hash(x)==(string?)claimed["fullSuite"]!["trxSha256"]);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="2033"&&(string?)counters.Attribute("executed")=="2033"&&(string?)counters.Attribute("passed")=="2033"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","full suite counters");
var receipt=new JsonObject{["scope"]="Independent original quality audit, no Mac code loading and no numerical or performance upgrade",["calculationCommit"]=target,["binaries"]=binaries,["runs"]=runs,["strictReports"]=strict,["massReports"]=mass,["trxSha256"]=Hash(trx),["fullSuiteTotal"]=2033};Write(output+"/quality/audit.json",receipt);Console.WriteLine($"Quality verified: {binaries.Count} PE/PDB, {runs.Count} groups, 2033 original tests, strict={strict}, mass={mass}");
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
