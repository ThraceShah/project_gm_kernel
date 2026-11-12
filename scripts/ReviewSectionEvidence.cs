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
const string target="2c0e4ea54bc2c4c9e22f24965ce71c41d84635b4";
const string package="temp_docs/incoming/mac-section-2c0e4ea-quality.zip";
const string output="temp_docs/independent-section-2c0e4ea";
const string original=output+"/original";
if(args.SequenceEqual(new[]{"--receipt"}))
{
    var quality=JsonNode.Parse(File.ReadAllText(output+"/evidence-receipt-verified.json"))!;quality.AsObject().Remove("members");
    var measurement=JsonNode.Parse(File.ReadAllText(output+"/measurement/receipt-verified.json"))!;foreach(var key in new[]{"members","binaries"})measurement.AsObject().Remove(key);
    var material="temp_docs/section-controlled/material-binding-current.json";var window="temp_docs/section-controlled/window";var mroot=output+"/measurement/original/";var report=JsonNode.Parse(File.ReadAllText(mroot+window+"/report.json"))!;
    var final=new JsonArray();
    foreach(var sample in report["samples"]!.AsArray())foreach(var file in sample!["artifacts"]!.AsArray())
    {
        var name=(string)file!["path"]!;if(!name.EndsWith(".x_t",StringComparison.Ordinal))continue;
        var family=(string)sample["case"]!;var role=family switch
        {
            "sphere" or "planar"=>name.EndsWith("-managed.x_t",StringComparison.Ordinal)?"GM product":"PK reference product",
            "solidblend"=>"GM product",
            "curved"=>name.Contains("-gm-",StringComparison.Ordinal)?"GM before input":name.Contains("-pk-",StringComparison.Ordinal)?"PK before input":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product",
            "shortedge"=>name.EndsWith("-input.x_t",StringComparison.Ordinal)?"PK seed":name.EndsWith("-input-fresh.x_t",StringComparison.Ordinal)?"GM received fresh input":name.EndsWith("-native-fresh.x_t",StringComparison.Ordinal)?"PK product received and re-exported by GM":name.EndsWith("-native.x_t",StringComparison.Ordinal)?"PK reference product":"GM product",
            _=>throw new Exception("unexpected operation")
        };
        var path=window+"/"+(string)sample["directory"]!+"/"+name;Require(Hash(File.ReadAllBytes(mroot+path))==(string?)file["sha256"],"final actual XT hash");
        final.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=file["sha256"]!.DeepClone(),["role"]=role,["commit"]=sample["commit"]!.DeepClone(),["repetition"]=role.EndsWith("product",StringComparison.Ordinal)||role=="PK product received and re-exported by GM"?2:null});
    }
    Require(final.Count==64,"final product set");
    var q2=output+"/q2-one-nurbs";var capture=JsonNode.Parse(File.ReadAllText(q2+"/capture/capture.json"))!;var counts=new JsonObject();foreach(var group in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!))counts[group.Key]=group.Count();
    var identity=JsonNode.Parse(File.ReadAllText(q2+"/capture/identity/loaded.json"))!;Require(Hash(File.ReadAllBytes(q2+"/capture/identity/kernel.dll"))==(string?)identity["kernelDllSha256"],"q2 actual loaded bytes");
    XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var trx=XDocument.Load(output+"/q2-focused/focused.trx").Descendants(ns+"Counters").Single();
    var logs=new JsonArray();foreach(var item in new[]{("evidence-01",0),("evidence-02",0),("measurement-01",0),("measurement-02",0),("bindings-01",0),("controls-01",0),("focused-driver",0),("consumer-driver",2)}){var path="temp_docs/section-review-"+item.Item1+".log";logs.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=Hash(File.ReadAllBytes(path)),["observedProcessExit"]=item.Item2});}
    var global=File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex/AGENTS.md"));var start=global.IndexOf("<!-- orca-user-global-policy-20261111:begin -->",StringComparison.Ordinal);var end=global.IndexOf("<!-- orca-user-global-policy-20261111:end -->",StringComparison.Ordinal);var body=global[(global.IndexOf('\n',start)+1)..end];var policyHash=Hash(System.Text.Encoding.UTF8.GetBytes(body));Require(policyHash=="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1","actual policy file marker body");
    foreach(var path in new[]{"AGENTS.md","docs/Computational_Kernel_Architecture.md","docs/Intersection_Local_Modeling_Architecture.md"})Require(Git("show","553b2c6c01d3862411f3b6a03735dc22ee20dc11:"+path).SequenceEqual(File.ReadAllBytes(path)),"unchanged actual constraints");
    var aggregate=new JsonObject{["scope"]="Independent fixed Section candidate review; no main integration, deployment, UI review or overall PK qualification",["deliveryCommit"]="fef52f3252bfe9a345f1a2872e9b6f96bdcafccd",["calculationCommit"]=target,["measurementSourceCommit"]="a770a49d07b43dbe05d284f5e11c2d6adfc40fd6",["nativeTree"]="8fc38005545b10735c3612fdf6bd57460fae19af",["sourceTree"]="e8b7750c97e018ec6508a94fa8132817982158ff",["policyFileBodySha256"]=policyHash,["policyScope"]="File verified, no claim of runtime automatic loading",["decisions"]=new JsonObject{["algorithmAndKeyIntegration"]="accepted",["recordedGeometryTopologyLifecycle"]="accepted within original executed scope",["originalMacSuite"]="accepted as 2024/2024 raw build0/test0",["uniqueMacWindow"]="accepted as descriptive fixed-source observations",["allNumericalVerifiedUnderOriginalContract"]="blocked by original Unresolved",["generalOrCausalSpeedupAndOverallPkGoal"]="unproven",["publishableData"]="accepted with original source, states, limitations, all cold/warm samples and regressions"},["quality"]=quality,["measurement"]=measurement,["controls"]=JsonNode.Parse(File.ReadAllText(output+"/controls.json")),["q2Focused"]=new JsonObject{["actualExit"]=int.Parse(File.ReadAllText(output+"/q2-focused/process.exit")),["total"]=(string?)trx.Attribute("total"),["passed"]=(string?)trx.Attribute("passed"),["failed"]=(string?)trx.Attribute("failed"),["trxSha256"]=Hash(File.ReadAllBytes(output+"/q2-focused/focused.trx"))},["q2OneNurbs"]=new JsonObject{["rawExit"]=int.Parse(File.ReadAllText(q2+"/process.exit")),["checks"]=counts,["captureErrors"]=capture["captureErrors"]!.DeepClone(),["kernelMvid"]=identity["kernelMvid"]!.DeepClone(),["kernelDllSha256"]=identity["kernelDllSha256"]!.DeepClone(),["captureSha256"]=Hash(File.ReadAllBytes(q2+"/capture/capture.json")),["scope"]="One complete Linux current-source consumer, not Mac timings"},["materials"]=new JsonObject{["originalMaterialBindingPath"]=material,["originalMaterialBindingSha256"]=Hash(File.ReadAllBytes(mroot+material)),["finalXtFiles"]=final,["qualityShortedgeFinalProductRepetition"]=5,["boundary"]="GM live product, private PK snapshot and independent PK reference are separate; inputs/seeds are not fabricated rep2; original curved role/tracking missing retained"},["actualAuditExecutions"]=logs};
    var destination="docs/section-independent/receipt.json";Require(!File.Exists(destination),"preserve aggregate receipt");Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.WriteAllText(destination,aggregate.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Independent aggregate receipt written");return;
}
var recheck=args.SequenceEqual(new[]{"--recheck"});
if(Directory.Exists(output)&&!recheck)throw new Exception("Review output already exists; preserve evidence");
Directory.CreateDirectory(original);
var zipHash=Hash(File.ReadAllBytes(package));
Require(zipHash=="3138cd943c7ffc265e4f9200dfb93e188fbb4a829d024b1b24bc06fa1183bed0","archive hash");
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
Require(members.Count==589&&bytes==84860396,"archive dimensions");
var publicFiles=new[]{"docs/Section_Root_Work.md","docs/section-work/quality-receipt.json","docs/section-work/source-binary-binding.json","docs/Section_Root_Quality.md"};
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
    var directory=run.GetProperty("directory").GetString()!;var folder=Path.Combine(original,"temp_docs/section-work",directory);var capturePath=Path.Combine(folder,"capture.json");
    Require(Hash(File.ReadAllBytes(capturePath))==run.GetProperty("captureSha256").GetString(),"capture hash");
    using var capture=JsonDocument.Parse(File.ReadAllBytes(capturePath));
    var exit=directory.Contains('/')?Path.Combine(folder,"process.exit"):Path.Combine(original,"temp_docs/section-work",directory[..^8]+".exit");
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
Require((string?)counters.Attribute("total")=="2024"&&(string?)counters.Attribute("passed")=="2024"&&(string?)counters.Attribute("failed")=="0","full TRX");
var supporting=new JsonArray();foreach(var result in originalTrx.Descendants(originalNs+"UnitTestResult").Where(x=>((string?)x.Attribute("testName"))?.Contains("SupportingBlend",StringComparison.Ordinal)==true))supporting.Add((JsonNode)new JsonObject{["name"]=(string?)result.Attribute("testName"),["outcome"]=(string?)result.Attribute("outcome")});
var sealedFolder=Path.Combine(original,"temp_docs/section-work");
using(var manifest=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/quality-original-files.json")))
    foreach(var file in manifest.RootElement.EnumerateArray())
    {var path=original+"/"+file.GetProperty("path").GetString();Require(new FileInfo(path).Length==file.GetProperty("bytes").GetInt64()&&Hash(File.ReadAllBytes(path))==file.GetProperty("sha256").GetString(),"quality manifest original bytes");}
var diagnosticCopies=0;
using(var copied=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/diagnostic-copy-binding.json")))
    foreach(var file in copied.RootElement.GetProperty("files").EnumerateArray())
    {var path=original+"/"+file.GetProperty("destination").GetString();Require(new FileInfo(path).Length==file.GetProperty("bytes").GetInt64()&&Hash(File.ReadAllBytes(path))==file.GetProperty("sha256").GetString(),"diagnostic preserved original bytes");diagnosticCopies++;}
var traceFolder=sealedFolder+"/root-request-diagnostic-original/temp_docs/root-section";
Require(File.ReadAllText(traceFolder+"/driver.exit").Trim()=="134"&&File.ReadAllText(traceFolder+"/driver-fixed.exit").Trim()=="134"&&File.ReadAllText(traceFolder+"/driver-primitive.exit").Trim()=="2","diagnostic failures remain distinct");
var tracePath=traceFolder+"/once-primitive/root-sections.json";var trace=JsonNode.Parse(File.ReadAllText(tracePath))!;var scopes=new Dictionary<long,string>();var success=new Dictionary<string,JsonNode>();var repeatByRoots=new Dictionary<int,long>();long repeats=0,cells=0,boundsWork=0,jacobians=0;var laterFailure=0;var unequal=0;var maxRoots=0;
foreach(var r in trace["rows"]!.AsArray().Select(x=>x!))
{
    var invocation=(long)r["invocation"]!;var source=string.Join('|',new[]{"tag","generation","sense","root","blend","spine","domain","supportKind","supportBits"}.Select(k=>r[k]!.ToJsonString()));Require(!scopes.TryGetValue(invocation,out var prior)||prior==source,"diagnostic actual source/support/domain stable");scopes[invocation]=source;
    var key=invocation+"|"+string.Join('|',new[]{"axis","at","first","last","tolerance","strict","certified","rootBudget","rootCapacity","initialCount"}.Select(k=>r[k]!.ToJsonString()));var nr=(int)r["roots"]!;maxRoots=Math.Max(maxRoots,nr);
    Require((int)r["initialCount"]! ==0&&(int)r["rootCapacity"]! ==32&&(int)r["rootBudget"]! ==32768,"actual scalar resource contract");
    if(success.TryGetValue(key,out var first))
    {if((string?)r["status"]!="Success"){laterFailure++;continue;}repeats++;if(!JsonNode.DeepEquals(first["payload"],r["payload"])||(int)first["roots"]! !=nr)unequal++;cells+=(long)r["cells"]!;boundsWork+=(long)r["bounds"]!;jacobians+=(long)r["jacobians"]!;repeatByRoots[nr]=repeatByRoots.GetValueOrDefault(nr)+1;}
    else if((string?)r["status"]=="Success")success[key]=r;
}
Require(trace["rows"]!.AsArray().Count==4811&&scopes.Count==16&&success.Count==3667&&repeats==1038&&laterFailure==0&&unequal==0&&cells==1370&&boundsWork==4630&&jacobians==0&&maxRoots==24,"diagnostic independent grouping");
var diagnostic=new JsonObject{["scope"]="Separate historical diagnostic, no candidate binary or CPU qualification",["copiedFiles"]=diagnosticCopies,["traceSha256"]=Hash(File.ReadAllBytes(tracePath)),["requests"]=4811,["findCalls"]=16,["uniqueSuccessfulRequests"]=success.Count,["repeatedSuccess"]=repeats,["payloadMismatches"]=unequal,["laterFailure"]=laterFailure,["repeatedCells"]=cells,["repeatedBoundEvaluations"]=boundsWork,["repeatedJacobians"]=jacobians,["maxRoots"]=maxRoots,["repeatedZero"]=repeatByRoots[0],["repeatedOne"]=repeatByRoots[1],["repeatedTwo"]=repeatByRoots[2],["originalProbeExits"]=new JsonArray(134,134,2)};
using(var before=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/section-full-binaries-before.json")))using(var after=JsonDocument.Parse(File.ReadAllBytes(sealedFolder+"/section-full-binaries-after.json")))
{
    foreach(var file in before.RootElement.EnumerateArray())
    {
        var name=file.GetProperty("path").GetString()!;var hash=file.GetProperty("sha256").GetString();
        Require(after.RootElement.EnumerateArray().Count(x=>x.GetProperty("path").GetString()==name&&x.GetProperty("sha256").GetString()==hash)==1,"test files before/after");
        Require(Hash(File.ReadAllBytes(sealedFolder+"/section-full-"+Path.GetFileName(name)))==hash,"sealed test file");
    }
}
Require(File.ReadAllText(sealedFolder+"/section-full-build.exit").Trim()=="0"&&File.ReadAllText(sealedFolder+"/section-full-test.exit").Trim()=="0","full raw exits");
var focused=XDocument.Load(sealedFolder+"/test-results/focused.trx").Descendants(originalNs+"Counters").Single();Require((string?)focused.Attribute("total")=="146"&&(string?)focused.Attribute("passed")=="146"&&File.ReadAllText(sealedFolder+"/focused.exit").Trim()=="0","original focused 146 raw0");
var diff=Git("diff","553b2c6c01d3862411f3b6a03735dc22ee20dc11",target,"--","src","scripts","tests");Require(diff.SequenceEqual(File.ReadAllBytes(sealedFolder+"/package-versus-accepted.diff")),"fixed source diff");
var receipt=new JsonObject{["scope"]="Independent BCL byte/source audit; does not convert producer raw exits or prove geometry",["target"]=target,["archive"]=new JsonObject{["sha256"]=zipHash,["length"]=new FileInfo(package).Length,["files"]=members.Count,["expandedBytes"]=bytes},["binaries"]=binaries,["runs"]=runs,["fullSuite"]=new JsonObject{["trxSha256"]=Hash(File.ReadAllBytes(full)),["total"]=2024,["passed"]=2024,["failed"]=0,["supporting"]=supporting},["members"]=members};
receipt["productBindings"]=artifacts;receipt["sourceManifests"]=manifests;receipt["sourceManifestRows"]=sourceRows;receipt["strictCompareReports"]=compareReports;receipt["massReports"]=massReports;var states=new JsonObject();foreach(var pair in components)states[pair.Key]=pair.Value;receipt["massStatusOccurrences"]=states;receipt["fixedDiffSha256"]=Hash(diff);
receipt["historicalDiagnostic"]=diagnostic;
var receiptPath=output+(recheck?"/evidence-receipt-verified.json":"/evidence-receipt.json");Require(!File.Exists(receiptPath),"preserve receipt");File.WriteAllText(receiptPath,receipt.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"archive={members.Count}/{bytes}; PE/PDB={binaries.Count}; runs={runs.Count}; full=2024/2024; supporting={supporting.Count}; evidence audit PASS");
static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
static byte[] Git(params string[] arguments){var info=new ProcessStartInfo("git"){RedirectStandardOutput=true};foreach(var arg in arguments)info.ArgumentList.Add(arg);using var process=Process.Start(info)!;using var memory=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(memory);process.WaitForExit();Require(process.ExitCode==0,"git read");return memory.ToArray();}
static string Source([CallerFilePath]string source="")=>source;
static void CountStatuses(JsonElement data,Dictionary<string,int> counts){if(data.ValueKind==JsonValueKind.Object){foreach(var property in data.EnumerateObject()){if(property.Name is "status" or "agreementStatus"&&property.Value.ValueKind==JsonValueKind.String){var status=property.Value.GetString()!;counts[status]=counts.GetValueOrDefault(status)+1;}else CountStatuses(property.Value,counts);}}else if(data.ValueKind==JsonValueKind.Array)foreach(var value in data.EnumerateArray())CountStatuses(value,counts);}
