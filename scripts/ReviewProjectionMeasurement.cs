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
const string archive="temp_docs/incoming/mac-projection-cc47871-controlled-original.zip",output="temp_docs/independent-projection-cc47871/measurement",original=output+"/original";
const string baseline="17eedbfd6be891db8c43494cd91a896d261242c4",candidate="cc47871133fb7d536ffa7fd050066c51f4905708",documentation="553b2c6c01d3862411f3b6a03735dc22ee20dc11";
var recheck=args.SequenceEqual(new[]{"--recheck"});Require(recheck||!Directory.Exists(output),"preserve measurement evidence");Directory.CreateDirectory(original);
Require(Hash(archive)=="9bfad64ff79211c4fbc6620e3a2678e3f966e8150d5b3dad778e5814c8128919","measurement ZIP hash");
using var zip=ZipFile.OpenRead(archive);var paths=new HashSet<string>();long expanded=0;
foreach(var member in zip.Entries)
{
    var path=member.FullName;Require(!Path.IsPathRooted(path)&&!path.Contains('\\')&&!path.Split('/').Contains("..")&&paths.Add(path)&&((member.ExternalAttributes>>16)&0xf000)!=0xa000,"safe member");
    var file=Path.Combine(original,path);Directory.CreateDirectory(Path.GetDirectoryName(file)!);
    if(!recheck){using var input=member.Open();using var target=new FileStream(file,FileMode.CreateNew,FileAccess.Write);input.CopyTo(target);}
    using var source=member.Open();Require(Hash(file)==Convert.ToHexStringLower(SHA256.HashData(source)),"extracted original");expanded+=member.Length;
}
Require(paths.Count==1037&&expanded==137327178,"measurement ZIP dimensions");
var data=original+"/temp_docs/projection-controlled";var window=data+"/window";
foreach(var path in new[]{"docs/Projection_Controlled_Measurement.md","docs/Native_Projection_Position_Bounds.md","docs/projection-controlled/measurement-receipt.json","docs/projection-controlled/operation-summary.json","docs/projection-controlled/source-binary-binding.json"})Require(File.ReadAllBytes(original+"/"+path).SequenceEqual(Git("show",documentation+":"+path)),"fixed documentation "+path);
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
        Require(Hash(dll)==(string?)id[kind=="kernel"?"loadedKernelAssemblySha256":"loadedFileAppAssemblySha256"],"actual loaded DLL");
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
        Require(matched==(kind=="kernel"?335:6),"tracked source documents");
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
var operations=new JsonArray();var author=Read(original+"/docs/projection-controlled/operation-summary.json");
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
    var family=rows.Where(x=>(string?)x!["case"]==label).ToArray();foreach(var field in new[]{"ssi-cells","root-cells","roots","budget-failures","winding"})Require(family.SelectMany(x=>x!["profile"]!.AsArray()).Select(x=>Field((string)x!,field)).Distinct().Count()==1,"unchanged work field "+field);
}
var receipt=new JsonObject{["scope"]="Independent fixed original measurement; descriptive observation only; raw2 and performanceQualified=false retained",["archiveSha256"]=Hash(archive),["archiveFiles"]=paths.Count,["expandedBytes"]=expanded,["startedUtc"]=report["startedUtc"]!.DeepClone(),["finishedUtc"]=report["finishedUtc"]!.DeepClone(),["sourceManifestRows"]=snapshots,["processes"]=20,["pairs"]=60,["apiTimes"]=120,["runnerExit"]=2,["meshFiles"]=meshCount,["meshBytes"]=meshBytes,["finalXtArtifactFiles"]=finalXt,["mainMassReports"]=mainReports,["extraMassReports"]=extraReports,["mainComponents"]=Counts(mainStates),["extraComponents"]=Counts(extraStates),["strictReports"]=strictReports,["operations"]=operations,["samples"]=rows,["binaries"]=binaries};
var repetitions=0;var rootLines=0;
foreach(var process in author["allSamples"]!.AsArray())
{
    var logPath=original+"/"+(string)process!["originalLog"]!;Require(Hash(logPath)==(string?)process["originalLogSha256"],"original log hash");var lines=File.ReadAllLines(logPath);var capturePath=Path.GetDirectoryName(logPath)+"/capture.json";Require(Hash(capturePath)==(string?)process["originalCaptureSha256"],"capture hash");var gm=Times(Read(capturePath),"gm");var pk=Times(Read(capturePath),"pk");
    foreach(var rep in process["repetitions"]!.AsArray())
    {
        var at=(int)rep!["rep"]!;Require((double)rep["gmMs"]! ==gm[at]&&(double)rep["pkMs"]! ==pk[at]&&(double)rep["pairedGmPk"]! ==gm[at]/pk[at],"per rep timings");
        foreach(var kind in new[]{"profile","work"}){var raw=(string)rep[kind+"Raw"]!;Require(lines[(int)rep[kind+"LineNumber"]!-1]==raw,"original line");CheckFields(rep[kind]!.AsObject(),raw);}
        foreach(var row in rep["rootWork"]!.AsArray()){var raw=(string)row!["raw"]!;Require(lines[(int)row["lineNumber"]!-1]==raw,"root work original line");CheckFields(row["fields"]!.AsObject(),raw);rootLines++;}repetitions++;
    }
}
Require(repetitions==60,"all repetitions");var builds=Directory.GetFiles(data,"prebuild-*.exit").Where(x=>Path.GetFileName(x)!="prebuild-driver.exit").ToArray();Require(builds.Length==10&&builds.All(x=>File.ReadAllText(x).Trim()=="0"),"ten managed prebuilds");
var loads=new List<double>();foreach(var file in Directory.GetFiles(data,"observed-*.json")){var observation=Read(file);Require(JsonNode.DeepEquals(observation["loadedIdentity"],Read(window+"/"+(string)observation["processDirectory"]!+"/identity/loaded.json"))&&observation["numericTemperatureCelsius"]==null,"observed identity/no numeric temperature");loads.Add(double.Parse(((string)observation["uptime"]!["stdout"]!).Split("load averages: ")[1].Split(' ',StringSplitOptions.RemoveEmptyEntries)[0],System.Globalization.CultureInfo.InvariantCulture));}Require(loads.Count==20&&loads.Min()==3.72&&loads.Max()==4.30,"actual process loads");
receipt["workAudit"]=new JsonObject{["repetitions"]=repetitions,["rootWorkLines"]=rootLines,["prebuildRaw0"]=builds.Length,["loadMin"]=loads.Min(),["loadMax"]=loads.Max(),["modulusComponentsRecomputed"]=1080};
var destination=output+(recheck?"/receipt-verified.json":"/receipt.json");Require(!File.Exists(destination),"preserve measurement receipt");File.WriteAllText(destination,receipt.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"measurement PASS: 20 processes, source rows={snapshots}, mesh={meshCount}/{meshBytes}, XT={finalXt}, mass={mainReports}+{extraReports}; "+Counts(mainStates).ToJsonString()+" / "+Counts(extraStates).ToJsonString());

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
