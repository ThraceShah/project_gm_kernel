#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// BCL-only: consume the one sealed Mac window; no model/API/timing execution.
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var folder=Path.Combine(root,"temp_docs/independent-1654");var original=Path.Combine(folder,"measurement-original");
var raw="temp_docs/unified-native-controlled/";var window=raw+"window/";
var sourceCache=new Dictionary<string,byte[]>();var binaryCache=new Dictionary<string,JsonObject>();
var archive=ReadLocal("measurement-archive.json");
foreach(var member in archive["members"]!.AsArray())Require(Hash(Path.Combine(original,S(member,"path")))==S(member,"sha256"),"immutable measurement member");
Require(Hash(Path.Combine(root,"temp_docs/incoming/mac-unified-native224-controlled-original.zip"))==S(archive,"zipSha256"),"immutable measurement ZIP");
foreach(var path in new[]{"docs/Unified_Native_Controlled_Measurement.md","docs/Unified_Native_Performance_Package.md","docs/unified-native-controlled/operation-summary.json","docs/unified-native-controlled/source-binary-binding.json","docs/unified-native-controlled/measurement-receipt.json"})Require(File.ReadAllBytes(Path.Combine(original,path)).AsSpan().SequenceEqual(Git("73b06e1004122f6897da10532d6253579a033aa4",path)),"fixed73b public documentation bytes");
string[] common=["NativeModelingWorkComparison.cs","NativeModelingQualityGateChecks.cs","AnalyticBooleanOracle.cs","BooleanOperationOracle.cs","RegularCurvedBooleanOracle.cs","RegularSolidBlendOracle.cs","ShortEdgeBlendOracle.cs","WebProgressCapture.cs","ParasolidBodyCompare.cs","ParasolidMassProperties.cs","ParasolidScriptHost.cs"];
foreach(var file in common)Require(Git("84c558c1","scripts/"+file).AsSpan().SequenceEqual(Git("d014f76e","scripts/"+file)),"eleven common current runner/helper bytes");
var report=Read(window+"report.json");var published=Read("docs/unified-native-controlled/operation-summary.json");var materials=Read(raw+"material-binding-current.json");
Require(S(materials,"reportSha256")==Hash(Path.Combine(original,window+"report.json")),"material report binding");
Require((int?)report["repeat"]==3&&(int?)report["runnerExit"]==2&&(bool?)report["performanceQualified"]==false,"original runner state");
Require(S(report,"baselineSha")=="84c558c10135c117bf8be6629a157e6d230f1b2d"&&S(report,"candidateSha")=="d014f76e41746a08b32e963d24b6a8b02fcaac67","fixed sources");
Require(S(report,"runnerSha256")==Convert.ToHexStringLower(SHA256.HashData(Git("84c558c1","scripts/NativeModelingWorkComparison.cs"))),"actual runner source");
var samples=report["samples"]!.AsArray();Require(samples.Count==20,"twenty original processes");
var processes=new JsonArray();var operations=new JsonArray();var roles=new JsonArray();var meshPaths=new HashSet<string>();var xtPaths=new HashSet<string>();
var load=new List<double>();long meshBytes=0;var zero=0;var unresolved=0;var api=0;var massStates=new Dictionary<string,int>();var primaryMassStates=new Dictionary<string,int>();
string[] cases=["sphere","planar","curved","solidblend","shortedge"];string[] sides=["baseline","candidate","candidate","baseline"];
for(var index=0;index<20;index++)
{
    var sample=samples[index]!;var op=cases[index/4];var side=sides[index%4];var dir=S(sample,"directory");var stem=window+dir+"/";var commit=side=="baseline"?S(report,"baselineSha"):S(report,"candidateSha");
    Require(dir==$"{index:D2}-{op}-{side}"&&S(sample,"case")==op&&S(sample,"side")==side&&S(sample,"commit")==commit,"actual complete ABBA order");
    var rawExit=int.Parse(File.ReadAllText(Path.Combine(original,stem+"process.exit")),CultureInfo.InvariantCulture);var expectedExit=op=="planar"?0:2;
    Require(rawExit==expectedExit&&(int?)sample["processExit"]==rawExit&&S(sample,"qualityStatus")== (rawExit==0?"Verified":"Unresolved"),"raw process exits / quality preserved");if(rawExit==0)zero++;else unresolved++;
    var command=Read(stem+"command.json");Require(S(command,"commit")==commit&&S(command,"captureCommit")==commit,"actual argv source");
    var argv=command["argv"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();Require(argv.Contains("--bench-repeat=3")&&argv.Contains("--bench-profile"),"three fresh repetitions requested");
    var script=common[2+index/4];
    string[] operationArgs=op switch{"curved"=>["block-15902"],"solidblend"=>["plane-nurbs","rounding-only"],"shortedge"=>["--angle=1","--radius=0.5","--tolerance=1e-07"],_=>[]};
    var expectedArgv=new[]{"run","--no-cache","scripts/"+script,"-c","Release","-p:BuildInParallel=false","-p:UseSharedCompilation=false","-p:GmInProcessLinkTasks=true","--"}.Concat(operationArgs).Concat(["--bench-repeat=3","--bench-profile"]);
    Require(argv.Where(a=>!a.StartsWith("--output-name=",StringComparison.Ordinal)).SequenceEqual(expectedArgv),"exact same operation parameters and invocation");
    Require((op=="sphere"&&S(command,"selector")=="equal-overlap-15902")||(op=="planar"&&S(command,"selector")=="overlap-two-axes-15902")||(op=="curved"&&S(command,"selector")=="block-15902")||((op is "solidblend" or "shortedge")&&command["selector"]==null),"exact selectors");
    var capture=Read(stem+"capture.json");var steps=capture["steps"]!.AsArray();var timings=capture["timings"]!.AsArray();
    Require(steps.Count==3&&timings.Count==6&&capture["captureErrors"]!.AsArray().Count==0,"complete original pairs and Capture");
    var identity=Read(stem+"identity/loaded.json");Require(S(identity,"calculationCommit")==commit&&S(identity,"captureCommit")==commit,"actual producer source observation");
    Require(S(identity,"loadedKernelAssemblySha256")==S(identity,"kernelDllSha256")&&S(identity,"loadedFileAppAssemblySha256")==S(identity,"oracleDllSha256"),"producer actual loaded assembly observation");
    var native=VerifyBinary(stem+"identity/kernel.dll",stem+"identity/kernel.pdb",commit,"src/ProjectGmKernel.Native/",identity,"kernel");
    Require((int?)native["matched"]==(side=="baseline"?333:334),"333/334 actual source documents");
    var oracle=VerifyBinary(stem+"identity/oracle.dll",stem+"identity/oracle.pdb",commit,"scripts/",identity,"oracle");
    foreach(var line in File.ReadLines(Path.Combine(original,stem+"source.sha256")))Require(line[..64]==Convert.ToHexStringLower(SHA256.HashData(Git(commit,line[66..]))),"actual process source manifest");
    var material=materials["samples"]!.AsArray().Single(x=>S(x,"directory")==dir)!;
    Require(S(material,"captureSha256")==Hash(Path.Combine(original,stem+"capture.json"))&&S(material,"oracleLogSha256")==Hash(Path.Combine(original,stem+"oracle.log")),"current material original capture/log binding");
    var gm=new double[3];var pk=new double[3];var stepMeshes=new HashSet<string>();
    for(var rep=0;rep<3;rep++)
    {
        foreach(var kernel in new[]{"gm","pk"})
        {
            var time=timings.Single(t=>(int?)t!["step"]==rep&&S(t,"kernel")==kernel)!;Require((int?)time["error"]==0,"all original API returns zero");var ms=(double)time["ms"]!;Require(ms>0&&double.IsFinite(ms),"positive API elapsed");(kernel=="gm"?gm:pk)[rep]=ms;api++;
            var step=steps[rep]![kernel]!;
            Require(JsonNode.DeepEquals(steps[rep]!["gm"]!["apiParameters"],steps[rep]!["pk"]!["apiParameters"]),"same GM/PK API parameters");
            var paths=new List<string>();foreach(var key in new[]{"before","after"})if(step[key] is JsonValue value)paths.Add(value.GetValue<string>());
            foreach(var key in new[]{"tools","afterBodies"})if(step[key] is JsonArray list)paths.AddRange(list.Select(x=>x!.GetValue<string>()));
            foreach(var path in paths.Distinct())
            {
                var relative=stem+path;Require(stepMeshes.Add(relative)||path==S(step,"after"),"mesh identity is local to actual step");
                if(!meshPaths.Add(relative))continue;
                var entry=material["meshes"]!.AsArray().Single(x=>S(x,"path")==relative)!;
                Require(S(entry,"step")==S(steps[rep],"id")&&S(entry,"kernel")==kernel&&S(entry,"sha256")==Hash(Path.Combine(original,relative)),"per-process/step mesh checksum");
                var bytes=new FileInfo(Path.Combine(original,relative)).Length;Require(bytes==(long?)entry["bytes"],"mesh bytes");meshBytes+=bytes;
                var mesh=Read(relative);var source=mesh["source"]!;
                Require(S(source,"commit")==commit&&S(source,"kernel")==kernel.ToUpperInvariant()&&S(source["captureIdentity"],"kernelDllSha256")==S(identity,"kernelDllSha256"),"mesh actual source/loaded identity");
                if(kernel=="gm")
                {
                    Require((bool?)source["freshXtReceive"]==true&&source["sourceBodyTag"]!=null&&source["snapshotNativeBodyTag"]!=null,"GM actual product private snapshot");
                    Require(Hash(Path.Combine(original,stem+S(source,"xtArtifact")))==S(source,"xtSha256"),"GM mesh source XT bytes");
                    if(path==S(step,"after"))
                    {
                        // CaptureDisplay and normal transmit have distinct XT
                        // byte representations. Bind the actual live body and
                        // the explicit same-step snapshot comparison separately.
                        Require(capture["checks"]!.AsArray().Any(c=>S(c?["sourceBinding"],"kind")=="GM-fresh-vs-native"&&(int?)c?["sourceBinding"]?["gmLiveBody"]==(int?)source["sourceBodyTag"]),"actual GM live product quality source");
                        Require(capture["checks"]!.AsArray().Any(c=>S(c?["sourceBinding"],"gmMesh")==path&&(int?)c?["sourceBinding"]?["step"]==rep),"same-step snapshot comparison mesh link");
                    }
                }
            }
        }
        if(op=="shortedge")Require(S(steps[rep]!["sourceBinding"],"kind")=="PK-seed-GM-receive","shortedge imported PK provenance");
    }
    var countedMassEvidence=new HashSet<string>();
    foreach(var check in capture["checks"]!.AsArray())
    {
        Require(S(check,"status") is "pass" or "missing" or "diagnostic","no hidden failed checks");
        if(check?["evidence"] is not JsonValue evidence)continue;var e=Read(stem+evidence.GetValue<string>());
        if(S(check,"id").StartsWith("quality-compare-",StringComparison.Ordinal))
        {
            Require(S(e,"status")=="Verified"&&(int?)e["nativeError"]==0,"actual compare verdict and native return");var mask=e["mask"]!;
            Require((int?)mask["max_diffs"]==1024&&(bool?)mask["all_tests"]==false&&(bool?)mask["acc_dev_tests"]==false&&(bool?)mask["tol_dev_tests"]==true,"strict original comparison mask");
            foreach(var key in new[]{"num_tests","class_tests","sense_tests","tol_tests","missing_tests","non_match_tests"})Require((bool?)mask[key]==true,"structural compare gate");
        }
        if(S(check,"id").StartsWith("quality-mass-",StringComparison.Ordinal)&&countedMassEvidence.Add(evidence.GetValue<string>()))
        {
            Require(e["properties"]!.AsArray().Count==15,"all 15 original modulus components");
            foreach(var property in e["properties"]!.AsArray()){var state=S(property,"agreementStatus");Require(state is "Verified" or "Unresolved","mass does not invent a green state");massStates[state]=massStates.GetValueOrDefault(state)+1;if(S(e,"label")!="native via managed")primaryMassStates[state]=primaryMassStates.GetValueOrDefault(state)+1;}
        }
    }
    foreach(var artifact in material["finalRepetitionArtifacts"]!.AsArray())
    {
        var path=S(artifact,"path");Require(Hash(Path.Combine(original,path))==S(artifact,"sha256"),"actual final file set hash");if((bool?)artifact!["isXtArtifact"]!=true)continue;Require(xtPaths.Add(path),"unique final XT file");
        var name=Path.GetFileName(path);string role;
        if(name.Contains("-input-fresh"))role="GM received fresh input";else if(name.Contains("-input"))role="PK input seed";
        else if(name.Contains("-before"))role=name.Contains("-gm-")?"GM input before operation":"PK input before operation";
        else if(name.Contains("-native-fresh"))role="PK output retransmitted by GM";
        else role=name.EndsWith("-reference.x_t")||name.EndsWith("-native.x_t")?"PK reference product":"GM product";
        roles.Add((JsonNode)new JsonObject{["directory"]=dir,["path"]=path,["sha256"]=S(artifact,"sha256"),["role"]=role,["retention"]=role.Contains("input",StringComparison.OrdinalIgnoreCase)?"shared/overwritten input file; no invented repetition identity":"last output filename after rep2; byte identity alone does not identify a repetition"});
    }
    var observed=Read(raw+"observed-"+dir+".json");Require(S(observed["loadedIdentity"],"kernelDllSha256")==S(identity,"kernelDllSha256")&&observed["numericTemperatureCelsius"]==null,"same observed identity and absent temperature");
    var match=Regex.Match(S(observed["uptime"],"stdout"),@"load averages:\s*([0-9.]+)");Require(match.Success,"actual load record");load.Add(double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture));
    processes.Add((JsonNode)new JsonObject{["directory"]=dir,["commit"]=commit,["rawExit"]=rawExit,["qualityStatus"]=S(sample,"qualityStatus"),["gmMs"]=Numbers(gm),["pkMs"]=Numbers(pk),["native"]=native.DeepClone(),["oracle"]=oracle.DeepClone()});
}
foreach(var op in cases)
{
    var expected=published["operations"]!.AsArray().Single(x=>S(x,"case")==op)!;var actual=new JsonObject{["case"]=op};
    foreach(var side in new[]{"baseline","candidate"})
    {
        var selected=processes.Where(x=>S(x,"directory").Contains("-"+op+"-",StringComparison.Ordinal)&&S(x,"directory").EndsWith("-"+side,StringComparison.Ordinal)).ToArray();
        var coldGm=selected.Select(x=>(double)x!["gmMs"]![0]!).ToArray();var coldPk=selected.Select(x=>(double)x!["pkMs"]![0]!).ToArray();
        var warmGm=selected.SelectMany(x=>x!["gmMs"]!.AsArray().Skip(1).Select(v=>(double)v!)).ToArray();var warmPk=selected.SelectMany(x=>x!["pkMs"]!.AsArray().Skip(1).Select(v=>(double)v!)).ToArray();
        var ratios=warmGm.Zip(warmPk,(g,p)=>g/p).ToArray();
        foreach(var field in new[]{("coldGmMs",coldGm),("coldPkMs",coldPk),("warmGmMs",warmGm),("warmPkMs",warmPk),("warmPairedGmPk",ratios)}){var e=expected[side]![field.Item1]!.AsArray();Require(e.Count==field.Item2.Length&&field.Item2.Select((v,i)=>Near(v,(double)e[i]!)).All(x=>x),"all cold/warm original values, no sample removal");}
        Require(Near(Median(warmGm),(double)expected[side]!["warmGmMedianMs"]!)&&Near(Median(ratios),(double)expected[side]!["warmPairedGmPkMedian"]!),"independent median recomputation");
        actual[side]=new JsonObject{["coldGmMs"]=Numbers(coldGm),["coldPkMs"]=Numbers(coldPk),["warmGmMs"]=Numbers(warmGm),["warmPkMs"]=Numbers(warmPk),["warmGmMedian"]=Median(warmGm),["warmGmMin"]=warmGm.Min(),["warmGmMax"]=warmGm.Max(),["warmPairedRatioMedian"]=Median(ratios),["ratioOfWarmMedians"]=Median(warmGm)/Median(warmPk)};
    }
    var change=100*((double)actual["candidate"]!["warmGmMedian"]!/(double)actual["baseline"]!["warmGmMedian"]!-1);Require(Near(change,(double)expected["changePercent"]!),"observed source median change");actual["changePercent"]=change;operations.Add((JsonNode)actual);
    Console.WriteLine($"WINDOW {op} GM {actual["baseline"]!["warmGmMedian"]} -> {actual["candidate"]!["warmGmMedian"]} ms change={change:F6}% pairedRatio={actual["candidate"]!["warmPairedRatioMedian"]}");
}
Require(zero==4&&unresolved==16&&api==120&&meshPaths.Count==312&&meshBytes==35389380&&xtPaths.Count==64,"complete original totals");
var prebuilds=Directory.GetFiles(Path.Combine(original,raw),"prebuild-*.exit").Where(p=>!p.EndsWith("prebuild-driver.exit",StringComparison.Ordinal)).ToArray();Require(prebuilds.Length==10&&prebuilds.All(p=>File.ReadAllText(p).Trim()=="0"),"ten separate prebuild raw exits");
foreach(var prebuild in Read(raw+"prebuild-source-binding.json").AsArray())
{
    var commit=S(prebuild,"commit");Require(commit==(S(prebuild,"side")=="baseline"?S(report,"baselineSha"):S(report,"candidateSha")),"fixed prebuild source");
    Require(prebuild!["scripts"]!.AsArray().Count==11,"eleven prebuild source hashes");
    foreach(var file in prebuild["scripts"]!.AsArray())Require(S(file,"sha256")==Convert.ToHexStringLower(SHA256.HashData(Git(commit,S(file,"file")))),"actual prebuild script source hash");
}
Require(File.ReadAllText(Path.Combine(original,raw+"runner.exit")).Trim()=="2"&&File.ReadAllText(Path.Combine(original,raw+"window-driver.exit")).Trim()=="0","original runner2 versus recording driver0");
Require(primaryMassStates.Values.Sum()==900&&massStates.Values.Sum()==1080,"900 primary components plus180 PK-product GM-roundtrip controls");
var host=Read(raw+"host-before.json");Require((int?)host["numericTemperatureProbe"]?["rawExit"]!=0,"original numeric temperature probe failed");
var mass=new JsonObject();foreach(var pair in massStates)mass[pair.Key]=pair.Value;
var primaryMass=new JsonObject();foreach(var pair in primaryMassStates)primaryMass[pair.Key]=pair.Value;
var result=new JsonObject{["zipSha256"]=S(archive,"zipSha256"),["reportSha256"]=Hash(Path.Combine(original,window+"report.json")),["startedUtc"]=S(report,"startedUtc"),["finishedUtc"]=S(report,"finishedUtc"),["rawZero"]=zero,["rawUnresolved2"]=unresolved,["runnerRawExit"]=2,["apiMeasurements"]=api,["meshFiles"]=meshPaths.Count,["meshBytes"]=meshBytes,["xtFinalFileSet"]=xtPaths.Count,["massPropertyStates"]=mass,["primary900MassPropertyStates"]=primaryMass,["loadMin"]=load.Min(),["loadMax"]=load.Max(),["numericTemperatureAvailable"]=false,["operations"]=operations,["processes"]=processes,["artifactRoles"]=roles};
File.WriteAllText(Path.Combine(folder,"measurement-summary.json"),result.ToJsonString());
Console.WriteLine($"PASS independent sealed-window audit processes=20 API=120 raw0={zero} raw2={unresolved} meshes={meshPaths.Count}/{meshBytes}B finalXTfiles={xtPaths.Count} load={load.Min()}..{load.Max()}; no model or timing rerun");

JsonObject VerifyBinary(string dll,string pdb,string commit,string trackedPrefix,JsonNode identity,string kind)
{
    var sha=Hash(Path.Combine(original,dll));Require(sha==S(identity,kind+"DllSha256")&&Hash(Path.Combine(original,pdb))==S(identity,kind+"PdbSha256"),"actual archived loaded DLL/PDB SHA");
    if(binaryCache.TryGetValue(sha,out var saved))return saved;
    using var stream=File.OpenRead(Path.Combine(original,dll));using var pe=new PEReader(stream);var md=pe.GetMetadataReader();var mvid=md.GetGuid(md.GetModuleDefinition().Mvid).ToString();Require(mvid==S(identity,kind+"Mvid"),"actual loaded MVID");
    using var ps=File.OpenRead(Path.Combine(original,pdb));using var provider=MetadataReaderProvider.FromPortablePdbStream(ps);var reader=provider.GetMetadataReader();var guid=new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16));Require(pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(d=>d.Type==DebugDirectoryEntryType.CodeView)).Guid==guid,"actual PE/PDB link");
    var count=0;var other=new JsonArray();
    foreach(var h in reader.Documents)
    {
        var d=reader.GetDocument(h);var path=reader.GetString(d.Name).Replace('\\','/');var index=path.IndexOf("/"+trackedPrefix,StringComparison.Ordinal);
        if(index<0||path.Contains("/obj/")){other.Add((JsonNode?)JsonValue.Create(path.Contains("/obj/")?path[(path.IndexOf("/src/",StringComparison.Ordinal)+1)..]:Path.GetFileName(path)));continue;}
        path=path[(index+1)..];Require(reader.GetBlobBytes(d.Hash).AsSpan().SequenceEqual(SHA256.HashData(Git(commit,path))),"actual portable-PDB tracked source checksum "+path);count++;
    }
    var result=new JsonObject{["sha256"]=sha,["pdbSha256"]=Hash(Path.Combine(original,pdb)),["mvid"]=mvid,["codeViewGuid"]=guid.ToString(),["matched"]=count,["unmappedOrGenerated"]=other};binaryCache.Add(sha,result);return result;
}
byte[] Git(string commit,string path)
{
    var key=commit+":"+path;if(sourceCache.TryGetValue(key,out var saved))return saved;
    var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("show");start.ArgumentList.Add(key);using var p=Process.Start(start)!;using var bytes=new MemoryStream();p.StandardOutput.BaseStream.CopyTo(bytes);var error=p.StandardError.ReadToEnd();p.WaitForExit();Require(p.ExitCode==0,"fixed Git source "+key+" "+error);var data=bytes.ToArray();sourceCache.Add(key,data);return data;
}
JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(Path.Combine(original,path)))!;
JsonNode ReadLocal(string path)=>JsonNode.Parse(File.ReadAllText(Path.Combine(folder,path)))!;
static string S(JsonNode? node,string key)=>node?[key]?.GetValue<string>()??"";
static JsonArray Numbers(IEnumerable<double> values)=>new(values.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray());
static double Median(IEnumerable<double> values){var sorted=values.Order().ToArray();return sorted.Length%2==0?(sorted[sorted.Length/2-1]+sorted[sorted.Length/2])*.5:sorted[sorted.Length/2];}
static bool Near(double a,double b)=>Math.Abs(a-b)<=1e-12*Math.Max(1,Math.Abs(b));
static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
static string Source([CallerFilePath]string path="")=>path;
