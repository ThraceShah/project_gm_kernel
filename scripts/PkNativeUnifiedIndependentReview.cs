#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

var script=Path.GetDirectoryName(Source())!;
var root=Path.GetFullPath(Path.Combine(script,".."));
var author=Path.GetFullPath(Path.Combine(root,"../gm-native-unified-delivery"));
var originals=Path.Combine(author,"temp_docs/unified-delivery");
var output=Path.Combine(root,"temp_docs/unified-review-4b2405e");
Directory.CreateDirectory(output);
var mode=args.Single();
if(mode=="evidence")
{
    var receipt=new JsonObject();
    Require(Git("rev-parse","4b2405e:src/ProjectGmKernel.Native")=="133f56acb0cf485366cd4f13f457dc3b161655eb","native tree");
    Require(Git("rev-parse","4b2405e:src")=="0c5d23f6f4e3e7aabe5034888f92812c3ecb07ad","src tree");
    foreach(var parent in new[]{"97174392b97e8c5976e56e2890c58314c74c73b5","f9ffc0985eb1e5ce53f649af66751de7c6c77353"})
        Require(Git("merge-base",parent,"4b2405e")==parent,"real parent ancestor");
    foreach(var name in new[]{"final-source.sha256","final-artifacts.sha256","handoff-current-v2/files.sha256","suite-loaded-files.sha256"})
    {
        var file=Path.Combine(originals,name);var count=0;
        foreach(var line in File.ReadLines(file))
        {
            var relative=line[66..];var basePath=name.StartsWith("handoff-")?Path.GetDirectoryName(file)!:author;
            Require(Hash(Path.Combine(basePath,relative))==line[..64],"manifest "+relative);
            if(name=="final-source.sha256")Require(Hash(Path.Combine(root,relative))==line[..64],"review source "+relative);
            count++;
        }
        receipt[name]=new JsonObject{["sha256"]=Hash(file),["entries"]=count};
        Console.WriteLine($"MANIFEST {name} entries={count} match=True sha256={Hash(file)}");
    }
    var trxFile=Path.Combine(originals,"full-suite/full-suite.trx");
    Require(Hash(trxFile)=="90472933bdcb7245b8da6015cd0b6585ca81fcbf70234690c76fd49547d111ca","TRX hash");
    var trx=XDocument.Load(trxFile);var ns=trx.Root!.Name.Namespace;
    var counters=trx.Descendants(ns+"Counters").Single();
    Require((string?)counters.Attribute("total")=="1974"&&(string?)counters.Attribute("passed")=="1974"&&(string?)counters.Attribute("failed")=="0","suite counters");
    foreach(var name in new[]{"NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative","SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled"})
        Require(trx.Descendants(ns+"UnitTestResult").Single(x=>((string?)x.Attribute("testName"))!.EndsWith(name,StringComparison.Ordinal)).Attribute("outcome")!.Value=="Passed","previous failure current outcome");
    Require(File.ReadAllText(Path.Combine(originals,"full-suite-v1.exit")).Trim()=="0","raw suite exit");
    receipt["suite"]=new JsonObject{["rawExit"]=0,["total"]=1974,["trxSha256"]=Hash(trxFile)};
    foreach(var relative in new[]{"tests/KernelTests/bin/Release/net10.0/ProjectGmKernel.Native.dll","tests/KernelTests/bin/Release/net10.0/KernelTests.dll",
        "temp_docs/unified-delivery/current-runner-v2/04-shortedge-candidate/identity/kernel.dll","temp_docs/unified-delivery/current-runner-v2/04-shortedge-candidate/identity/oracle.dll"})
        Binding(Path.Combine(author,relative));
    var delivery=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"docs/reviews/advanced-modeling/pk-native-unified-delivery.receipt.json")))!;
    foreach(var item in delivery["cases"]!.AsArray())
    {
        var directory=Path.Combine(author,item!["privateSource"]!.GetValue<string>());
        Require(Hash(Path.Combine(directory,"capture.json"))==item["captureSha256"]!.GetValue<string>(),"capture bytes");
        var capture=Read(Path.Combine(directory,"capture.json"));var counts=capture["checks"]!.AsArray().GroupBy(x=>x!["status"]!.GetValue<string>()).ToDictionary(x=>x.Key,x=>x.Count());
        foreach(var status in new[]{"pass","fail","missing","diagnostic"})
            Require(counts.GetValueOrDefault(status)==item["basicCheckCounts"]![status]!.GetValue<int>(),"capture counts");
        var meshes=Directory.Exists(Path.Combine(directory,"meshes"))?Directory.GetFiles(Path.Combine(directory,"meshes"),"*.json"):[];
        Require(meshes.Length==item["meshCount"]!.GetValue<int>(),"mesh count");
        foreach(var meshPath in meshes)
        {
            var mesh=Read(meshPath);var source=mesh["source"]!;
            Require(Hash(Path.Combine(directory,source["xtArtifact"]!.GetValue<string>()))==source["xtSha256"]!.GetValue<string>(),"real source XT hash");
            Require(mesh["bodyId"]!.GetValue<string>().EndsWith(":"+source["sourceBodyTag"]!.GetValue<int>(),StringComparison.Ordinal),"real display body");
        }
        Console.WriteLine($"CAPTURE {item["id"]} steps={capture["steps"]!.AsArray().Count} meshes={meshes.Length} counts={string.Join(',',counts.Select(x=>$"{x.Key}:{x.Value}"))}");
    }
    var zip=Path.Combine(originals,"handoff-current-v2.zip");
    Require(Hash(zip)=="5197d647fbc47a15f3338e83f1c4063227fddcd4dfff92186c0274aa510495bf","handoff ZIP");
    using(var archive=System.IO.Compression.ZipFile.OpenRead(zip))
    {
        Require(archive.Entries.Count==156,"handoff count");
        foreach(var entry in archive.Entries)
        {
            Require(!new[]{".x_t",".dll",".pdb",".so",".sch_txt"}.Any(e=>entry.FullName.EndsWith(e,StringComparison.OrdinalIgnoreCase)),"no private binary/XT");
            using var stream=entry.Open();Require(Convert.ToHexStringLower(SHA256.HashData(stream))==Hash(Path.Combine(originals,"handoff-current-v2",entry.FullName)),"ZIP exact handoff bytes");
        }
    }
    receipt["handoffSha256"]=Hash(zip);receipt["checkerExit"]=0;
    File.WriteAllText(Path.Combine(output,"evidence.receipt.json"),receipt.ToJsonString());
    Console.WriteLine("EVIDENCE_CHECKER_PASS; original operation exits unchanged");
}
else if(mode=="execute")
{
    await Oracle("mirror8","ShortEdgeBlendOracle.cs",["--angle=1","--radius=0.5","--tolerance=1e-07","--bench-profile","--input-mirror","--input-scale=8","--geometry","--event-arrangement"],null);
    await Oracle("oblique","LocalOperationOracle.cs",["--blend-arrangement","--lifecycle","--managed-material","--then-boolean"],"oblique-unequal-chamfer");
    var filter=string.Join('|',new[]{"ReflectedScaledRollingSeedRetainsFiniteContactStationEnclosures","CertifiedSourceStationsNeedDisjointBoxesToPublishFragments","RejoinedSourceStationUsesAllCompatibleRootEnclosures","RediscoveredRootWitnessCannotIdentifyADifferentEvent","RepeatedDiscoveryOfOneIsolatedRootCanJoinButAnotherBranchOrOccurrenceCannot","DisjointCertifiedCutRootsSurviveTheOldCandidateEpsilon","CertifiedCutRediscoveryRefinesOneRootAndRejectsAnOverlappingDifferentOccurrence"}.Select(x=>"FullyQualifiedName~"+x));
    await Run("focused","dotnet",["test","tests/KernelTests","-c","Release","-p:UseSharedCompilation=false","-p:BuildInParallel=false","-p:GmInProcessLinkTasks=true","--filter",filter,"--logger","trx;LogFileName=review.trx","--results-directory",Path.Combine("temp_docs","unified-review-4b2405e","focused")],null);
}
else if(mode=="negative")
{
    var planar=Path.Combine(originals,"current-runner-v2/01-planar-candidate");
    foreach(var variant in new[]{"intact","no-modulus","no-body-check","no-compare","no-estimates-lower","no-evidence-links","rejected-compare-evidence"})
    {
        var derived=Path.Combine(output,"audit-"+variant);Require(!Directory.Exists(derived),"new derived directory");
        foreach(var source in Directory.EnumerateFiles(planar,"*",SearchOption.AllDirectories))
        {
            var relative=Path.GetRelativePath(planar,source);
            if(relative.EndsWith(".dll",StringComparison.Ordinal)||relative.EndsWith(".pdb",StringComparison.Ordinal))continue;
            var target=Path.Combine(derived,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);
        }
        var path=Path.Combine(derived,"capture.json");var capture=Read(path);var checks=capture["checks"]!.AsArray();
        foreach(var check in checks.ToArray())
        {
            var id=check!["id"]!.GetValue<string>();
            if(variant=="no-body-check"&&id.EndsWith("body-check",StringComparison.Ordinal)||variant=="no-compare"&&id.StartsWith("quality-compare-",StringComparison.Ordinal)){checks.Remove(check);continue;}
            if(id.StartsWith("quality-mass-",StringComparison.Ordinal))
            {
                if(variant=="no-modulus")check.AsObject().Remove("actualModulus");
                if(variant=="no-estimates-lower")foreach(var field in new[]{"referenceEstimate","actualEstimate","normalizedLower"})check.AsObject().Remove(field);
            }
            if(variant=="no-evidence-links"&&id.StartsWith("quality-",StringComparison.Ordinal))check.AsObject().Remove("evidence");
            if(variant=="rejected-compare-evidence"&&id.StartsWith("quality-compare-",StringComparison.Ordinal))
            {
                var report=Path.Combine(derived,check["evidence"]!.GetValue<string>());var data=Read(report);data["status"]="Rejected";File.WriteAllText(report,data.ToJsonString());
            }
        }
        if(variant=="no-evidence-links")Directory.Delete(Path.Combine(derived,"evidence"),true);
        File.WriteAllText(path,capture.ToJsonString());
        File.WriteAllText(Path.Combine(derived,"derived-from.json"),new JsonObject{["sourceCaptureSha256"]=Hash(Path.Combine(planar,"capture.json")),["mutation"]=variant,["modelingExecuted"]=false}.ToJsonString());
        await Run("audit-"+variant,"dotnet",["run","scripts/NativeModelingWorkComparison.cs","-c","Release","-p:UseSharedCompilation=false","--","--audit",Path.GetRelativePath(script,derived),"--repeat","1"],null);
    }
}
else throw new ArgumentException("evidence|execute|negative");

async Task Oracle(string name,string file,string[] extra,string? selector)
{
    var env=new Dictionary<string,string>{["GM_WEB_ROOT"]=root,["GM_WEB_COMMIT"]=Git("rev-parse","HEAD"),["GM_WEB_CAPTURE_COMMIT"]=Git("rev-parse","HEAD"),["GM_WEB_CAPTURE"]=Path.Combine(output,"capture-"+name)};
    if(selector!=null)env["GM_WEB_SELECT"]=selector;
    await Run(name,"dotnet",new[]{"run","--no-cache","scripts/"+file,"-c","Release","-p:BuildInParallel=false","-p:UseSharedCompilation=false","-p:GmInProcessLinkTasks=true","--","--output-name=independent-4b2405e-"+name}.Concat(extra).ToArray(),env);
}
async Task Run(string name,string executable,string[] argv,Dictionary<string,string>? env)
{
    var prefix=Path.Combine(output,name);Require(!File.Exists(prefix+".exit"),"immutable execution "+name);
    var info=new ProcessStartInfo(executable){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var arg in argv)info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";info.Environment.Remove("GM_WEB_SELECT");info.Environment.Remove("GM_WEB_CAPTURE");
    if(env!=null)foreach(var pair in env)info.Environment[pair.Key]=pair.Value;
    File.WriteAllText(prefix+".command.json",new JsonObject{["executable"]=executable,["argv"]=new JsonArray(argv.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["source"]=Git("rev-parse","HEAD")}.ToJsonString());
    using var process=Process.Start(info)!;Console.WriteLine($"START {name} pid={process.Id}");
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    File.WriteAllText(prefix+".log",await stdout+"\nSTDERR\n"+await stderr);File.WriteAllText(prefix+".exit",process.ExitCode+"\n");
    Console.WriteLine($"END {name} rawExit={process.ExitCode}");
}
void Binding(string assembly)
{
    using var peFile=File.OpenRead(assembly);using var pe=new PEReader(peFile);
    var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));
    using var pdbFile=File.OpenRead(Path.ChangeExtension(assembly,".pdb"));using var pdb=MetadataReaderProvider.FromPortablePdbStream(pdbFile);
    var reader=pdb.GetMetadataReader();Require(new Guid(reader.DebugMetadataHeader!.Id.AsSpan()[..16])==cv.Guid,"PE/PDB identity");
    var matched=0;var generated=0;
    foreach(var handle in reader.Documents)
    {
        var doc=reader.GetDocument(handle);var path=reader.GetString(doc.Name);
        var at=new[]{"/src/","/tests/","/scripts/"}.Select(x=>path.IndexOf(x,StringComparison.Ordinal)).FirstOrDefault(x=>x>=0,-1);
        if(at<0||path.Contains("/obj/",StringComparison.Ordinal)){generated++;continue;}
        Require(reader.GetGuid(doc.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16"),"SHA256 PDB source");
        Require(Hash(Path.Combine(root,path[(at+1)..]))==Convert.ToHexStringLower(reader.GetBlobBytes(doc.Hash)),"PDB source "+path[(at+1)..]);matched++;
    }
    var metadata=pe.GetMetadataReader();var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    Console.WriteLine($"BINDING {Path.GetRelativePath(author,assembly)} dll={Hash(assembly)} pdb={Hash(Path.ChangeExtension(assembly,".pdb"))} mvid={mvid} sources={matched} generated={generated}");
}
string Git(params string[] argv)
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,UseShellExecute=false};foreach(var arg in argv)info.ArgumentList.Add(arg);
    using var process=Process.Start(info)!;var value=process.StandardOutput.ReadToEnd();process.WaitForExit();Require(process.ExitCode==0,"git");return value.Trim();
}
static JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(path))!;
static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(file));}
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
static string Source([CallerFilePath]string path="")=>path;
