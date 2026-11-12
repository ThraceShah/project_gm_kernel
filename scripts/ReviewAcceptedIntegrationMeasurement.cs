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

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string archive="temp_docs/incoming/mac-integration-3aff4e2b-controlled-original.zip",output="temp_docs/independent-accepted-integration-3aff4e2b/controlled-original",original=output+"/original";
const string baseline="271af03cfefedef12eea19337f1b2b3bca9fe96b",candidate="a105eb4854e9f138e409a12bcb041e877f254452",documentation="4026e59c827814e52559ec72d41ff282468e3894";
if(args.SequenceEqual(new[]{"--bindings"}))
{
    var bindingRoot=original+"/temp_docs/integration-controlled/window";var bindingReport=Read(bindingRoot+"/report.json");var bindingRows=new JsonArray();var bindingProofs=0;var bindingMissing=0;var bindingViaManaged=0;var bindingKinds=new Dictionary<string,int>();
    foreach(var bindingSample in bindingReport["samples"]!.AsArray())
    {
        var bindingFolder=bindingRoot+"/"+(string)bindingSample!["directory"]!;var bindingId=Read(bindingFolder+"/identity/loaded.json");var bindingCapture=Read(bindingFolder+"/capture.json");var bindingMeshes=new Dictionary<string,JsonNode>();
        foreach(var bindingMeshPath in Directory.GetFiles(bindingFolder+"/meshes","*.json"))
        {
            var bindingMesh=Read(bindingMeshPath);var bindingSource=bindingMesh["source"]!;var own=(string?)bindingSource["kernel"]=="GM";
            Require(JsonNode.DeepEquals(bindingSource["captureIdentity"],bindingId),"mesh actual loaded identity");Require((bool)bindingSource["freshXtReceive"]! ==own&&(int)bindingSource["snapshotNativeBodyTag"]!>0,"GM snapshot/PK live identity");
            Require((string?)bindingMesh["bodyId"]==(own?"gm:":"pk:")+(int)bindingSource["sourceBodyTag"]!,"mesh source tag");Require(Hash(bindingFolder+"/"+(string)bindingSource["xtArtifact"]!)==(string?)bindingSource["xtSha256"],"actual source XT bytes");
            bindingMeshes["meshes/"+Path.GetFileName(bindingMeshPath)]=bindingMesh;
            bindingRows.Add((JsonNode)new JsonObject{["path"]="temp_docs/integration-controlled/window/"+(string)bindingSample["directory"]!+"/meshes/"+Path.GetFileName(bindingMeshPath),["sha256"]=Hash(bindingMeshPath),["sourceKernel"]=bindingSource["kernel"]!.DeepClone(),["liveTag"]=bindingSource["sourceBodyTag"]!.DeepClone(),["snapshotNativeTag"]=bindingSource["snapshotNativeBodyTag"]!.DeepClone(),["sourceXtSha256"]=bindingSource["xtSha256"]!.DeepClone()});
        }
        foreach(var bindingProofPath in Directory.GetFiles(bindingFolder+"/evidence","quality-*.json"))
        {
            var bindingProof=Read(bindingProofPath);Require(JsonNode.DeepEquals(bindingProof["captureIdentity"],bindingId),"proof actual loaded identity");
            if(bindingProof["sourceBinding"] is not JsonObject bindingSource){bindingMissing++;continue;}
            Require(JsonNode.DeepEquals(bindingSource["identity"],bindingId),"proof source identity");var bindingKind=(string)bindingSource["kind"]!;bindingKinds[bindingKind]=bindingKinds.GetValueOrDefault(bindingKind)+1;
            if(bindingKind=="same-instance-snapshot-pair")
            {
                var bindingStep=bindingCapture["steps"]![(int)bindingSource["step"]!]!;Require(JsonNode.DeepEquals(bindingSource["gmMesh"],bindingStep["gm"]!["after"])&&JsonNode.DeepEquals(bindingSource["pkMesh"],bindingStep["pk"]!["after"]),"snapshot pair actual step mesh");
                Require(bindingMeshes[(string)bindingSource["gmMesh"]!]["source"]!["kernel"]!.GetValue<string>()=="GM"&&bindingMeshes[(string)bindingSource["pkMesh"]!]["source"]!["kernel"]!.GetValue<string>()=="PK","snapshot pair origin kernels");
            }
            else
            {
                Require(bindingKind=="GM-fresh-vs-native"&&(string?)bindingSource["actualOriginKernel"]=="GM","fresh GM source kind");
                if((string?)bindingSource["stage"]=="native via managed")
                {
                    // ShortEdgeBlendOracle receives the PK result into a new
                    // GM body and re-exports it for this separate control;
                    // WebProgressCapture has no mesh step for that body.
                    Require((string?)bindingSample["case"]=="shortedge"&&(int)bindingSource["gmLiveBody"]!>0,"PK product via managed control");bindingViaManaged++;
                }
                else Require(bindingMeshes.Values.Any(x=>(string?)x["source"]!["kernel"]=="GM"&&(int)x["source"]!["sourceBodyTag"]! ==(int)bindingSource["gmLiveBody"]!),"fresh GM actual live tag");
                Require((int)bindingProof["actualBody"]! ==(int)bindingSource["freshNativeBody"]!&&(int)bindingProof["referenceBody"]! ==(int)bindingSource["referenceNativeBody"]!,"fresh proof actual PK tags");
            }
            bindingProofs++;
        }
    }
    Require(bindingRows.Count==312&&bindingProofs==204&&bindingMissing==12&&bindingViaManaged==24,"complete mesh/proof binding set");
    var bindingResult=new JsonObject{["scope"]="Actual original mesh source and proof binding audit; source-less reports remain missing; no new modeling or display qualification",["meshes"]=bindingRows,["boundProofs"]=bindingProofs,["missingProofSourceBindings"]=bindingMissing,["pkProductViaManagedProofsWithoutDisplayMesh"]=bindingViaManaged,["proofKinds"]=Counts(bindingKinds)};var bindingDestination="docs/accepted-integration-independent/material-bindings.json";Require(!File.Exists(bindingDestination),"preserve binding receipt");Directory.CreateDirectory(Path.GetDirectoryName(bindingDestination)!);File.WriteAllText(bindingDestination,bindingResult.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS material bindings: 312 meshes, 204 bound proofs (24 PK-product via managed controls), 12 missing sources retained");return;
}
Require(args.SequenceEqual(new[]{"--recheck"})&&Directory.Exists(original),"--recheck an existing safe extraction or --bindings");
Require(Hash(archive)=="dbcb4708a9c79e941144deff9e6a55b277f65a72415dab1f874bbda6f47379b6","measurement ZIP hash");
using var zip=ZipFile.OpenRead(archive);var paths=new HashSet<string>();var members=new JsonArray();long expanded=0;
foreach(var member in zip.Entries)
{
    var path=member.FullName;Require(!Path.IsPathRooted(path)&&!path.Contains('\\')&&!path.Contains(':')&&!path.Split('/').Any(x=>x is "." or "..")&&paths.Add(path)&&((member.ExternalAttributes>>16)&0xf000)!=0xa000,"safe member");
    var file=Path.Combine(original,path);Require(Path.GetFullPath(file).StartsWith(Path.GetFullPath(original)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"isolated original member");
    using var source=member.Open();Require(Hash(file)==Convert.ToHexStringLower(SHA256.HashData(source)),"extracted original");expanded+=member.Length;members.Add((JsonNode)new JsonObject{["path"]=path,["length"]=member.Length,["sha256"]=Hash(file)});
}
Require(paths.Count==1044&&expanded==137748725,"measurement ZIP dimensions");
var data=original+"/temp_docs/integration-controlled";var window=data+"/window";
foreach(var path in new[]{"docs/Accepted_Optimization_Integration_Controlled_Measurement.md","docs/Accepted_Optimization_Integration_Work.md","docs/integration-controlled/measurement-receipt.json","docs/integration-controlled/operation-summary.json","docs/integration-controlled/source-binary-binding.json"})Require(File.ReadAllBytes(original+"/"+path).SequenceEqual(Git("show",documentation+":"+path)),"fixed documentation "+path);
var report=Read(window+"/report.json");Require((string?)report["candidateSha"]==candidate&&(string?)report["baselineSha"]==baseline&&(int)report["repeat"]! ==3&&(int)report["runnerExit"]! ==2&&!(bool)report["performanceQualified"]!,"fixed runner metadata");
Require(File.ReadAllText(data+"/runner.exit").Trim()=="2","original runner raw2");
var helpers=new[]{"NativeModelingWorkComparison.cs","AnalyticBooleanOracle.cs","BooleanOperationOracle.cs","RegularCurvedBooleanOracle.cs","RegularSolidBlendOracle.cs","ShortEdgeBlendOracle.cs","WebProgressCapture.cs","ParasolidBodyCompare.cs","ParasolidMassProperties.cs","ParasolidScriptHost.cs","NativeModelingQualityGateChecks.cs"};
foreach(var file in helpers)Require(Git("show",baseline+":scripts/"+file).SequenceEqual(Git("show",candidate+":scripts/"+file)),"same current helper "+file);
Require(HashBytes(Git("show",candidate+":scripts/NativeModelingWorkComparison.cs"))==(string?)report["runnerSha256"],"actual runner source");
var gitBytes=new Dictionary<string,byte[]>();var snapshots=0;var binaries=new JsonArray();var rows=new JsonArray();var meshCount=0;long meshBytes=0;var finalXt=0;var mainStates=new Dictionary<string,int>();var extraStates=new Dictionary<string,int>();var mainReports=0;var extraReports=0;var strictReports=0;
var expectedCases=new[]{"sphere","planar","curved","solidblend","shortedge"};var sides=new[]{"baseline","candidate","candidate","baseline"};
Require(report["samples"]!.AsArray().Count==20,"20 processes");
foreach(var (sample,index) in report["samples"]!.AsArray().Select((x,i)=>(x!,i)))
{
    var label=(string)sample["case"]!;var side=(string)sample["side"]!;var commit=side=="baseline"?baseline:candidate;var name=(string)sample["directory"]!;var folder=window+"/"+name;
    Require(label==expectedCases[index/4]&&side==sides[index%4]&&(string?)sample["commit"]==commit,"serial ABBA/order");
    var exit=int.Parse(File.ReadAllText(folder+"/process.exit"));Require(exit==(label=="planar"?0:2)&&exit==(int)sample["processExit"]!,"original process exit");
    Require((string?)sample["qualityStatus"]==(label=="planar"?"Verified":"Unresolved"),"original quality status");
    var capture=Read(folder+"/capture.json");var id=Read(folder+"/identity/loaded.json");var command=Read(folder+"/command.json");
    Require((string?)id["calculationCommit"]==commit&&(string?)id["captureCommit"]==commit&&(string?)command["commit"]==commit,"loaded/argv commit");
    Require(capture["captureErrors"]!.AsArray().Count==0&&!capture["checks"]!.AsArray().Any(x=>(string?)x!["status"]=="fail"),"no capture/hard failure");
    var argv=command["argv"]!.AsArray().Select(x=>(string)x!).ToArray();Require(argv.Contains("--bench-repeat=3")&&argv.Contains("--bench-profile"),"three fresh pairs argv");
    Require((string?)command["selector"]==(label=="sphere"?"equal-overlap-15902":label=="planar"?"overlap-two-axes-15902":label=="curved"?"block-15902":null),"actual selector");
    foreach(var line in File.ReadLines(folder+"/source.sha256")){var path=line[66..];var key=commit+":"+path;if(!gitBytes.TryGetValue(key,out var bytes))gitBytes[key]=bytes=Git("show",key);Require(HashBytes(bytes)==line[..64],"original source manifest "+path);snapshots++;}
    foreach(var kind in new[]{"kernel","oracle"})
    {
        var dll=folder+"/identity/"+kind+".dll";var pdb=folder+"/identity/"+kind+".pdb";
        Require(Hash(dll)==(string?)id[kind+"DllSha256"]&&Hash(pdb)==(string?)id[kind+"PdbSha256"],"archived DLL/PDB");
        Require(id.AsObject().Count(x=>x.Value is JsonValue value&&value.TryGetValue<string>(out var text)&&text==Hash(dll))>=2,"actual loaded hash witness");
        using var input=File.OpenRead(dll);using var pe=new PEReader(input);var metadata=pe.GetMetadataReader();var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString();Require(mvid==(string?)id[kind+"Mvid"],"actual loaded MVID");
        using var pstream=File.OpenRead(pdb);using var provider=MetadataReaderProvider.FromPortablePdbStream(pstream);var reader=provider.GetMetadataReader();var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));Require(new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16))==cv.Guid,"PE/PDB link");
        var matched=0;var other=new JsonArray();
        foreach(var handle in reader.Documents)
        {
            var doc=reader.GetDocument(handle);var path=reader.GetString(doc.Name).Replace('\\','/');var at=path.IndexOf("/src/",StringComparison.Ordinal);if(at<0)at=path.IndexOf("/scripts/",StringComparison.Ordinal);
            var relative=at<0?"":path[(at+1)..];var key=commit+":"+relative;
            if(relative.Length==0||relative.Contains("/obj/")||!gitBytes.TryGetValue(key,out var bytes)){other.Add((JsonNode?)JsonValue.Create(Path.GetFileName(path)));continue;}
            Require(reader.GetGuid(doc.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")&&SHA256.HashData(bytes).SequenceEqual(reader.GetBlobBytes(doc.Hash)),"PDB tracked bytes "+relative);matched++;
        }
        Require(matched==(kind=="kernel"?336:6),"tracked source documents");
        binaries.Add((JsonNode)new JsonObject{["directory"]=name,["kind"]=kind,["dllSha256"]=Hash(dll),["mvid"]=mvid,["matchedTrackedDocuments"]=matched,["unmapped"]=other});
    }
    var gm=Times(capture,"gm");var pk=Times(capture,"pk");Require(gm.Length==3&&pk.Length==3,"60 pairs/120 API times");
    var seen=new HashSet<string>();
    foreach(var step in capture["steps"]!.AsArray())foreach(var kernel in new[]{"gm","pk"})
    {
        var item=step![kernel]!;var references=new List<string>();foreach(var field in new[]{"before","after"})if(item[field] is JsonValue value)references.Add(value.GetValue<string>());foreach(var field in new[]{"tools","afterBodies"})if(item[field] is JsonArray array)references.AddRange(array.Select(x=>(string)x!));
        foreach(var relative in references)
        {
            if(!seen.Add(relative))continue;var file=folder+"/"+relative;meshCount++;meshBytes+=new FileInfo(file).Length;var source=Read(file)["source"]!;
            Require((string?)source["commit"]==commit,"mesh actual source");if(source["xtArtifact"] is JsonValue artifact)Require(Hash(folder+"/"+artifact.GetValue<string>())==(string?)source["xtSha256"],"fresh XT bytes");
        }
    }
    foreach(var artifact in sample["artifacts"]!.AsArray()){var path=(string)artifact!["path"]!;Require(Hash(folder+"/"+path)==(string?)artifact["sha256"],"final artifact bytes");if(path.EndsWith(".x_t"))finalXt++;}
    foreach(var file in Directory.GetFiles(folder+"/evidence","quality-*.json"))
    {
        var proof=Read(file);if(Path.GetFileName(file).StartsWith("quality-compare",StringComparison.Ordinal)){Require((string?)proof["status"]=="Verified","strict report status");var mask=proof["mask"]!;Require((int)mask["max_diffs"]!>0&&!(bool)mask["all_tests"]!&&!(bool)mask["acc_dev_tests"]!&&(bool)mask["tol_dev_tests"]!,"strict comparison mask");strictReports++;continue;}
        Require(proof["properties"]!.AsArray().Count==15,"15 component report");var extra=(string?)proof["label"]=="native via managed";var states=extra?extraStates:mainStates;if(extra)extraReports++;else mainReports++;
        foreach(var property in proof["properties"]!.AsArray()){VerifyModulus(property!);var status=(string)property!["agreementStatus"]!;states[status]=states.GetValueOrDefault(status)+1;Require(status!="Different","modulus Different");}
    }
    var log=File.ReadAllLines(folder+"/oracle.log");var profile=log.Where(x=>x.StartsWith("PROFILE operation=")).ToArray();var work=log.Where(x=>x.StartsWith("WORK jacobians=")).ToArray();
    Require(profile.Length==3&&work.Length==3,"three work profiles");
    rows.Add((JsonNode)new JsonObject{["case"]=label,["side"]=side,["directory"]=name,["originalExit"]=exit,["gmMs"]=Array(gm),["pkMs"]=Array(pk),["profile"]=ArrayStrings(profile),["work"]=ArrayStrings(work),["captureSha256"]=Hash(folder+"/capture.json")});
}
Require(meshCount==312&&meshBytes==35389380&&finalXt==64&&mainReports==60&&extraReports==12,"complete materials and unique mass reports");
var material=Read(data+"/material-binding-current.json");
foreach(var sample in material["samples"]!.AsArray())foreach(var field in new[]{"meshes","finalRepetitionArtifacts"})foreach(var file in sample![field]!.AsArray())Require(Hash(original+"/"+(string)file!["path"]!)==(string?)file["sha256"],"material manifest hash");
var operations=new JsonArray();var author=Read(original+"/docs/integration-controlled/operation-summary.json");
foreach(var label in expectedCases)
{
    var operation=new JsonObject{["case"]=label};
    foreach(var side in new[]{"baseline","candidate"})
    {
        var selected=rows.Where(x=>(string?)x!["case"]==label&&(string?)x["side"]==side).ToArray();var gm=selected.SelectMany(x=>Numbers(x!["gmMs"]!).Skip(1)).ToArray();var pk=selected.SelectMany(x=>Numbers(x!["pkMs"]!).Skip(1)).ToArray();var ratios=gm.Zip(pk,(g,p)=>g/p).ToArray();
        operation[side]=new JsonObject{["coldGmMs"]=Array(selected.Select(x=>(double)x!["gmMs"]![0]!)),["coldPkMs"]=Array(selected.Select(x=>(double)x!["pkMs"]![0]!)),["warmGmMs"]=Array(gm),["warmPkMs"]=Array(pk),["gmMedianMs"]=Median(gm),["pkMedianMs"]=Median(pk),["gmMinMs"]=gm.Min(),["gmMaxMs"]=gm.Max(),["pairedRatioMedian"]=Median(ratios),["ratioOfMedians"]=Median(gm)/Median(pk)};
        var claimed=author["operations"]!.AsArray().Single(x=>(string?)x!["case"]==label)![side]!;Require((double)claimed["warmGmMedianMs"]! ==Median(gm)&&(double)claimed["warmPairedGmPkMedian"]! ==Median(ratios),"independent raw statistic");
    }
    var b=(double)operation["baseline"]!["gmMedianMs"]!;var c=(double)operation["candidate"]!["gmMedianMs"]!;operation["changePercent"]=100*(c/b-1);operations.Add((JsonNode)operation);
    var family=rows.Where(x=>(string?)x!["case"]==label).ToArray();foreach(var field in new[]{"ssi-cells","budget-failures","winding"})Require(family.SelectMany(x=>x!["profile"]!.AsArray()).Select(x=>Field((string)x!,field)).Distinct().Count()==1,"unchanged work field "+field);
}
var receipt=new JsonObject{["scope"]="Independent fixed original measurement; descriptive observation only; raw2 and performanceQualified=false retained",["archiveSha256"]=Hash(archive),["archiveFiles"]=paths.Count,["expandedBytes"]=expanded,["startedUtc"]=report["startedUtc"]!.DeepClone(),["finishedUtc"]=report["finishedUtc"]!.DeepClone(),["sourceManifestRows"]=snapshots,["processes"]=20,["pairs"]=60,["apiTimes"]=120,["runnerExit"]=2,["meshFiles"]=meshCount,["meshBytes"]=meshBytes,["finalXtArtifactFiles"]=finalXt,["mainMassReports"]=mainReports,["extraMassReports"]=extraReports,["mainComponents"]=Counts(mainStates),["extraComponents"]=Counts(extraStates),["strictReports"]=strictReports,["operations"]=operations,["samples"]=rows,["binaries"]=binaries,["members"]=members};
var repetitions=0;var rootLines=0;
foreach(var process in author["allSamples"]!.AsArray())
{
    var logPath=original+"/"+(string)process!["originalLog"]!;Require(Hash(logPath)==(string?)process["originalLogSha256"],"original log hash");var lines=File.ReadAllLines(logPath);var capturePath=Path.GetDirectoryName(logPath)+"/capture.json";Require(Hash(capturePath)==(string?)process["originalCaptureSha256"],"capture hash");var captured=Read(capturePath);var gm=Times(captured,"gm");var pk=Times(captured,"pk");
    foreach(var rep in process["repetitions"]!.AsArray())
    {
        var at=(int)rep!["rep"]!;Require((double)rep["gmMs"]! ==gm[at]&&(double)rep["pkMs"]! ==pk[at]&&(double)rep["pairedGmPk"]! ==gm[at]/pk[at],"per rep timings");
        Require((string?)rep["phase"]==(at==0?"cold":"warm")&&captured["steps"]!.AsArray().Count==3,"original capture step and phase");
        foreach(var kernel in new[]{"gm","pk"})Require(captured["timings"]!.AsArray().Count(t=>(string?)t!["kernel"]==kernel&&(int)t["step"]! ==at&&(int)t["error"]! ==0)==1,"unique API time at actual capture step");
        Require(Math.Abs((double)rep["profile"]!["api-ms"]!-gm[at])<=.00050000001,"rounded profile time maps to actual API time");
        foreach(var kind in new[]{"profile","work"}){var raw=(string)rep[kind+"Raw"]!;Require(lines[(int)rep[kind+"LineNumber"]!-1]==raw,"original line");CheckFields(rep[kind]!.AsObject(),raw);}
        foreach(var row in rep["rootWork"]!.AsArray()){var raw=(string)row!["raw"]!;Require(lines[(int)row["lineNumber"]!-1]==raw,"root work original line");CheckFields(row["fields"]!.AsObject(),raw);rootLines++;}repetitions++;
    }
}
Require(repetitions==60,"all repetitions");var builds=Directory.GetFiles(data,"prebuild-*.exit").Where(x=>Path.GetFileName(x).StartsWith("prebuild-baseline-",StringComparison.Ordinal)||Path.GetFileName(x).StartsWith("prebuild-candidate-",StringComparison.Ordinal)).ToArray();Require(builds.Length==10&&builds.All(x=>File.ReadAllText(x).Trim()=="0"),"ten managed prebuilds");
Require(File.ReadAllText(data+"/prebuild-driver.exit").Trim()=="0","original prebuild driver raw0");
var loads=new List<double>();foreach(var file in Directory.GetFiles(data,"observed-*.json")){var observation=Read(file);Require(JsonNode.DeepEquals(observation["loadedIdentity"],Read(window+"/"+(string)observation["processDirectory"]!+"/identity/loaded.json"))&&observation["numericTemperatureCelsius"]==null,"observed identity/no numeric temperature");loads.Add(double.Parse(((string)observation["uptime"]!["stdout"]!).Split("load averages: ")[1].Split(' ',StringSplitOptions.RemoveEmptyEntries)[0],System.Globalization.CultureInfo.InvariantCulture));}Require(loads.Count==20&&loads.Min()==3.94&&loads.Max()==4.41,"actual process loads");
receipt["workAudit"]=new JsonObject{["repetitions"]=repetitions,["rootWorkLines"]=rootLines,["prebuildRaw0"]=builds.Length,["loadMin"]=loads.Min(),["loadMax"]=loads.Max(),["modulusComponentsRecomputed"]=1080};
var destination=output+"/receipt-verified.json";var serialized=receipt.ToJsonString(new JsonSerializerOptions{WriteIndented=true});if(File.Exists(destination))Require(File.ReadAllText(destination)==serialized,"existing derived receipt identical");else File.WriteAllText(destination,serialized);Console.WriteLine($"measurement PASS: 20 processes, source rows={snapshots}, mesh={meshCount}/{meshBytes}, XT={finalXt}, mass={mainReports}+{extraReports}; "+Counts(mainStates).ToJsonString()+" / "+Counts(extraStates).ToJsonString());

static JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(path))!;
static string Hash(string file){using var input=File.OpenRead(file);return Convert.ToHexStringLower(SHA256.HashData(input));}
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static byte[] Git(params string[] args){var info=new ProcessStartInfo("git"){RedirectStandardOutput=true};foreach(var arg in args)info.ArgumentList.Add(arg);using var process=Process.Start(info)!;using var output=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(output);process.WaitForExit();Require(process.ExitCode==0,"fixed git read");return output.ToArray();}
static string Source([CallerFilePath]string path="")=>path;
static string HashBytes(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static double[] Times(JsonNode capture,string kernel){var rows=capture["timings"]!.AsArray().Where(x=>(string?)x!["kernel"]==kernel).OrderBy(x=>(int)x!["step"]!).ToArray();Require(rows.All(x=>(int)x!["error"]! ==0),"API raw0");return rows.Select(x=>(double)x!["ms"]!).ToArray();}
static JsonArray Array(IEnumerable<double> numbers)=>new(numbers.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());
static JsonArray ArrayStrings(IEnumerable<string> values)=>new(values.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());
static double[] Numbers(JsonNode values)=>values.AsArray().Select(x=>(double)x!).ToArray();
static double Median(IEnumerable<double> values){var sorted=values.Order().ToArray();return (sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])*.5;}
static long Field(string line,string field)=>long.Parse(line.Split(' ').Single(x=>x.StartsWith(field+"=",StringComparison.Ordinal))[(field.Length+1)..]);
static JsonObject Counts(Dictionary<string,int> counts){var result=new JsonObject();foreach(var value in counts)result[value.Key]=value.Value;return result;}
static void CheckFields(JsonObject fields,string raw){foreach(var field in fields){var text=raw.Split(' ').Single(x=>x.StartsWith(field.Key+"=",StringComparison.Ordinal))[(field.Key.Length+1)..];if(field.Value!.GetValueKind()==JsonValueKind.String)Require(text==(string?)field.Value,"erratum parsed string");else Require(double.Parse(text,System.Globalization.CultureInfo.InvariantCulture)==(double)field.Value,"erratum parsed number");}}
static void VerifyModulus(JsonNode property)
{
    var a=(double)property["referenceEstimate"]!;var b=(double)property["actualEstimate"]!;var ra=(double)property["referenceModulus"]!;var rb=(double)property["actualModulus"]!;Require(double.IsFinite(a)&&double.IsFinite(b)&&double.IsFinite(ra)&&double.IsFinite(rb)&&ra>=0&&rb>=0,"valid modulus input");
    var a0=Math.BitDecrement(a-ra);var a1=Math.BitIncrement(a+ra);var b0=Math.BitDecrement(b-rb);var b1=Math.BitIncrement(b+rb);var min=a0<=0&&a1>=0?0:Math.Min(Math.Abs(a0),Math.Abs(a1));var max=Math.Max(Math.Abs(a0),Math.Abs(a1));var gap=Math.Max(0,Math.BitDecrement(Math.Max(a0-b1,b0-a1)));var diameter=Math.BitIncrement(Math.Max(Math.Abs(a0-b1),Math.Abs(a1-b0)));var lower=Math.Max(0,Math.BitDecrement(gap/Math.BitIncrement(1+max)));var upper=Math.BitIncrement(diameter/Math.BitDecrement(1+min));
    Require(lower==(double)property["normalizedLower"]!&&upper==(double)property["normalizedUpper"]!&&(upper<1e-7?"Verified":lower>=1e-7?"Different":"Unresolved")== (string?)property["agreementStatus"],"recomputed modulus bound/status");
}
