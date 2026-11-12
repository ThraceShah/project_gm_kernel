#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.IO.Compression;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScriptPath())!,".."));
Directory.SetCurrentDirectory(root);
var registryPath=Path.GetFullPath(Option("--registry","web-progress-registry.json"),Path.GetDirectoryName(ScriptPath())!);
var registry=JsonNode.Parse(File.ReadAllText(registryPath))!.AsObject();
if(args.Contains("--list")){foreach(var item in registry["cases"]!.AsArray())Console.WriteLine($"{item!["id"]} adapter={item["meshAdapter"]}");return 0;}
var knownCases=registry["cases"]!.AsArray().Select(c=>c!["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
if(knownCases.Count!=registry["cases"]!.AsArray().Count)throw new InvalidDataException("Duplicate registry case IDs");
if(Option("--family","all") is not ("all" or "blend" or "boolean"))throw new ArgumentException("Unknown family");
if(Option("--suite","all")!="all"&&!registry["cases"]!.AsArray().Any(c=>c!["suite"]!.GetValue<string>()==Option("--suite","all")))throw new ArgumentException("Unknown suite");
if(Option("--case","representatives") is not ("representatives" or "all"))foreach(var selected in Option("--case","").Split(','))if(!knownCases.Contains(selected))throw new ArgumentException("Unknown case: "+selected);
var config=Option("--configuration","Release");var budget=Number("--seconds",600);var caseBudget=Number("--case-seconds",120);var samples=Number("--samples",1);
if(budget<1||caseBudget<1||samples<1||samples>31)throw new ArgumentException("Positive budgets and samples in 1..31 required.");
var commit=Command("git",["rev-parse","HEAD"]);var dirty=Command("git",["status","--porcelain","--untracked-files=no"]);
var now=DateTimeOffset.UtcNow;var id=Option("--run-id",now.ToString("yyyyMMddTHHmmssZ")+"-"+commit[..8]);
if(id.IndexOfAny(Path.GetInvalidFileNameChars())>=0||id.Contains('/')||id.Contains('\\')||id is "." or "..")throw new ArgumentException("run-id must be one safe path component");
var data=Path.GetFullPath(Option("--output","../temp_docs/web-progress/data"),Path.GetDirectoryName(ScriptPath())!);
var run=Path.Combine(data,"runs",id);if(Directory.Exists(run))throw new IOException("Run already exists: choose a new --run-id; existing evidence is immutable.");Directory.CreateDirectory(run);
var host=new JsonObject{["os"]=RuntimeInformation.OSDescription,["architecture"]=RuntimeInformation.OSArchitecture.ToString(),["processArchitecture"]=RuntimeInformation.ProcessArchitecture.ToString(),["logicalProcessors"]=Environment.ProcessorCount,["cpu"]=OperatingSystem.IsMacOS()?Command("sysctl",["-n","machdep.cpu.brand_string"]):ReadCpu(),["memoryBytes"]=OperatingSystem.IsMacOS()?Command("sysctl",["-n","hw.memsize"]):null,["sdk"]=Command("dotnet",["--version"]),["runtime"]=RuntimeInformation.FrameworkDescription,["stopwatchFrequency"]=Stopwatch.Frequency};
var configuration=new JsonObject{["build"]=config,["nativeAot"]=false,["timing"]="Stopwatch elapsed wall time around API only",["tieredCompilation"]=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")??"runtime-default",["cache"]="oracle defaults, fresh process per sample",["parallelism"]="serial PK sessions",["samplesRequested"]=samples,["resourceLimits"]=Environment.GetEnvironmentVariable("GM_WEB_RESOURCE_LIMITS")};
var cases=new JsonArray();
var manifest=new JsonObject{["schemaVersion"]=1,["run"]=new JsonObject{["id"]=id,["commit"]=commit,["generatedAt"]=now.ToString("O"),["pkVersion"]=null,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["scope"]=new JsonObject{["registryVersion"]=registry["version"]!.DeepClone(),["registered"]=registry["cases"]!.AsArray().Count,["family"]=Option("--family","all"),["suite"]=Option("--suite","all"),["case"]=Option("--case","representatives"),["seconds"]=budget,["caseSeconds"]=caseBudget},["provenance"]=new JsonObject{["pkToyCommit"]=Command("git",["-C","third_party/PKToy","rev-parse","HEAD"]),["dirtyAtStart"]=dirty.Length>0,["sourceHashes"]=SourceHashes(),["registryHash"]=Hash(File.ReadAllBytes(registryPath)),["workingDiffSha256"]=Hash(Encoding.UTF8.GetBytes(Command("git",["diff","--no-ext-diff"]))),["sourceMode"]="direct managed KernelRuntime plus independent PskernelSharp real PK",["registry"]= "registry.json",["reviewSources"]=registry["reviewSources"]?.DeepClone(),["implementationTree"]=Command("git",["rev-parse","HEAD:src"]),["limitations"]=registry["limitations"]!.DeepClone()}},["cases"]=cases};
File.WriteAllText(Path.Combine(run,"registry.json"),registry.ToJsonString());
var clock=Stopwatch.StartNew();var exporterBroken=false;var executed=0;var completed=false;
foreach(var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
{
    var caseId=entry["id"]!.GetValue<string>();var family=entry["family"]!.GetValue<string>();var suite=entry["suite"]!.GetValue<string>();
    var requested=Option("--case","representatives");var selected=(Option("--family","all") is "all"||Option("--family","all")==family)&&(Option("--suite","all") is "all"||Option("--suite","all")==suite)&&(requested=="all"||requested=="representatives"&&entry["representative"]?.GetValue<bool>()==true||requested.Split(',').Contains(caseId,StringComparer.Ordinal));
    var parameters=entry["parameters"]!.DeepClone();var api=entry["api"]!.GetValue<string>();
    var checks=new JsonArray();var evidence=new JsonArray();var notes=new JsonArray();
    var c=new JsonObject{["id"]=caseId,["title"]=entry["title"]?.DeepClone()??JsonValue.Create(caseId),["family"]=family,["operation"]=entry["operation"]!.DeepClone(),["status"]="missing",["parameters"]=parameters,["parameterHash"]=Hash(Encoding.UTF8.GetBytes(parameters.ToJsonString())),["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]=api,["parameters"]=parameters.DeepClone()},["pk"]=new JsonObject{["name"]=api,["parameters"]=parameters.DeepClone()}},["steps"]=new JsonArray(),["checks"]=checks,["performance"]=new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["samples"]=0,["warmup"]=0,["scope"]="operation-only; ratio = GM elapsed ms / PK elapsed ms; input construction, export, tessellation, comparison and I/O excluded",["correctnessQualified"]=false,["host"]=host.DeepClone(),["configuration"]=configuration.DeepClone(),["note"]="Not executed"},["evidence"]=evidence,["notes"]=notes,["source"]=new JsonObject{["path"]=entry["source"]!.DeepClone(),["revision"]=commit},["suite"]=suite,["execution"]=new JsonObject{["selected"]=selected,["attempted"]=false,["exitCode"]=null}};
    cases.Add((JsonNode)c);
 }
Save();
foreach(var entry in registry["cases"]!.AsArray().OfType<JsonObject>())
{
    var caseId=entry["id"]!.GetValue<string>();var c=cases.OfType<JsonObject>().Single(c=>c["id"]!.GetValue<string>()==caseId);
    var selected=c["execution"]!["selected"]!.GetValue<bool>();var notes=c["notes"]!.AsArray();var checks=c["checks"]!.AsArray();var evidence=c["evidence"]!.AsArray();
    if(!entry["meshAdapter"]!.GetValue<bool>())
    {c["status"]="unsupported";notes.Add((JsonNode?)JsonValue.Create("Registered source case; web snapshot/structured-check adapter is not implemented. No execution or pass inferred."));Save();continue;}
    if(!selected){notes.Add((JsonNode?)JsonValue.Create("Not selected for this run; current evidence is missing."));Save();continue;}
    if(clock.Elapsed.TotalSeconds>=budget){notes.Add((JsonNode?)JsonValue.Create("Execution budget exhausted before start; not run, not a timeout."));Save();continue;}
    executed++;Console.WriteLine("RUN "+caseId);c["execution"]!["attempted"]=true;
    var durationsGm=new List<double>();var durationsPk=new List<double>();var exitCodes=new JsonArray();var anyTimeout=false;var allValid=true;var oraclePassed=true;var anyExportError=false;var anyCapture=false;var evaluationOnly=false;
    for(var sample=0;sample<samples;sample++)
    {
        var caseDir=Path.Combine(run,"cases",caseId,"sample-"+sample);Directory.CreateDirectory(caseDir);
        var sampleSources=SourceHashes();
        var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
        foreach(var arg in new[]{"run","--file",entry["source"]!.GetValue<string>(),"-c",config,"-p:UseSharedCompilation=false","--"})info.ArgumentList.Add(arg);
        foreach(var arg in entry["arguments"]!.AsArray())info.ArgumentList.Add(arg!.GetValue<string>());
        info.Environment["MSBUILDDISABLENODEREUSE"]="1";
        info.Environment["GM_WEB_ROOT"]=root;info.Environment["GM_WEB_CAPTURE"]=caseDir;info.Environment["GM_WEB_COMMIT"]=commit;info.Environment["GM_WEB_SELECT"]=entry["selector"]?.GetValue<string>();info.Environment["GM_WEB_GEOMETRY_AUTHOR"]=entry["geometryAuthor"]?.GetValue<string>()??"independent GM and PK construction, identical numeric parameters";
        using var process=new Process{StartInfo=info};var processStarted=Stopwatch.GetTimestamp();process.Start();
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        var timedOut=false;var remaining=Math.Max(1,Math.Min(caseBudget,budget-clock.Elapsed.TotalSeconds));
        using(var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(remaining)))
        {try{await process.WaitForExitAsync(cancellation.Token);}catch(OperationCanceledException){timedOut=true;process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
        var output=await stdout;var errors=await stderr;
        // Logs contain project diagnostics only; strip checkout/user paths from the distributable evidence.
        var text=(output+"\nSTDERR\n"+errors).Replace(root,".").Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"~");
        File.WriteAllText(Path.Combine(caseDir,"oracle.log"),text);exitCodes.Add((JsonNode?)JsonValue.Create(process.ExitCode));anyTimeout|=timedOut;
        var prefix=$"cases/{caseId}/sample-{sample}/";
        evidence.Add((JsonNode)new JsonObject{["label"]=$"Oracle sample {sample} raw exit={process.ExitCode}"+(timedOut?" timeout":""),["path"]=prefix+"oracle.log"});
        File.WriteAllText(Path.Combine(caseDir,"execution.json"),new JsonObject{["exitCode"]=process.ExitCode,["timedOut"]=timedOut,["processElapsedMs"]=Stopwatch.GetElapsedTime(processStarted).TotalMilliseconds,["budgetSeconds"]=remaining,["arguments"]=Args(info),["sourceHashes"]=sampleSources,["logSha256"]=Hash(Encoding.UTF8.GetBytes(text))}.ToJsonString());
        evidence.Add((JsonNode)new JsonObject{["label"]="Execution provenance",["path"]=prefix+"execution.json"});
        c["execution"]!["exitCode"]=process.ExitCode;
        var capturePath=Path.Combine(caseDir,"capture.json");JsonObject? capture=File.Exists(capturePath)?JsonNode.Parse(File.ReadAllText(capturePath))!.AsObject():null;
        anyExportError|=File.Exists(Path.Combine(caseDir,"export-errors.json"))||text.Contains(": error CS",StringComparison.Ordinal)||text.Contains("The build failed.",StringComparison.Ordinal);
        if(capture!=null)
        {
            anyCapture=true;
            manifest["run"]!["pkVersion"]=capture["pkVersion"]!.DeepClone();
            c["sessionPrecision"]=capture["sessionPrecision"]!.DeepClone();
            if(sample==0)
            {
                var steps=capture["steps"]!.DeepClone().AsArray();
                foreach(var step in steps)foreach(var side in new[]{"gm","pk"})
                {
                    foreach(var key in new[]{"before","after"})if(step![side]![key]!=null)step[side]![key]=prefix+step[side]![key]!.GetValue<string>();
                    foreach(var key in new[]{"tools","afterBodies"})if(step![side]![key] is JsonArray paths)for(var p=0;p<paths.Count;p++)paths[p]=prefix+paths[p]!.GetValue<string>();
                }
                c["steps"]=steps;
                foreach(var side in new[]{"gm","pk"})
                {
                    var actualParameters=new JsonArray();foreach(var step in steps)if(step![side]?["apiParameters"]!=null)actualParameters.Add(step[side]!["apiParameters"]!.DeepClone());
                    c["apis"]![side]!["parameters"]!["actualOperations"]=actualParameters;
                }
                foreach(var check in capture["checks"]!.AsArray()){var copy=check!.DeepClone();copy["evidence"]=prefix+"capture.json";checks.Add(copy);}
            }
            var timings=capture["timings"]!.AsArray();var gm=timings.Where(t=>t!["kernel"]!.GetValue<string>()=="gm").ToArray();var pk=timings.Where(t=>t!["kernel"]!.GetValue<string>()=="pk").ToArray();
            if(gm.Length>0&&gm.Length==pk.Length&&timings.All(t=>t!["error"]!.GetValue<int>()==0)){durationsGm.Add(gm.Sum(t=>t!["ms"]!.GetValue<double>()));durationsPk.Add(pk.Sum(t=>t!["ms"]!.GetValue<double>()));}
            // A pure native-evaluation display has no operation timing by design
            // (the case is an evaluation, not a modelling API interval), so the
            // timing requirement is waived only for captures that declare it.
            var evaluationDisplay=capture["steps"]!.AsArray().Any(st=>st?["gm"]?["evaluation"]!=null||st?["pk"]?["evaluation"]!=null||st?["displayScope"]!=null);
            var hasTimings=gm.Length>0&&gm.Length==pk.Length;
            allValid&=capture["checks"]!.AsArray().Count>0&&capture["checks"]!.AsArray().All(ch=>ch!["status"]!.GetValue<string>() is "pass" or "diagnostic")&&(hasTimings||evaluationDisplay);
            if(evaluationDisplay&&!hasTimings)evaluationOnly=true;
            evidence.Add((JsonNode)new JsonObject{["label"]=$"Structured capture sample {sample}",["path"]=prefix+"capture.json"});
            foreach(var f in Directory.EnumerateFiles(caseDir,"*.json",SearchOption.AllDirectories).Where(f=>f.Contains(Path.DirectorySeparatorChar+"evidence"+Path.DirectorySeparatorChar)))evidence.Add((JsonNode)new JsonObject{["label"]="Raw comparison mask and differences",["path"]=prefix+Path.GetRelativePath(caseDir,f).Replace('\\','/')});
        }
        else allValid=false;
        var processAccepted=process.ExitCode==0&&!timedOut&&!text.Contains("FAIL ",StringComparison.Ordinal);
        oraclePassed&=processAccepted;allValid&=processAccepted;
        if(timedOut||clock.Elapsed.TotalSeconds>=budget)break;
    }
    if(entry["expectedStepCount"] is JsonValue expectedValue)
    {
        var expected=expectedValue.GetValue<int>();var steps=c["steps"]!.AsArray();
        while(steps.Count<expected)
        {
            var ordinal=steps.Count;steps.Add((JsonNode)new JsonObject{["id"]="unreached-"+ordinal,["title"]="Downstream operation not reached; see stage evidence",["gm"]=new JsonObject{["before"]=null,["after"]=null},["pk"]=new JsonObject{["before"]=null,["after"]=null}});
            checks.Add((JsonNode)new JsonObject{["id"]="unreached-"+ordinal,["label"]="Downstream operation evidence",["status"]="missing",["gm"]=null,["pk"]=null,["absDelta"]=null,["normalizedDelta"]=null,["threshold"]=null,["unit"]="",["evidence"]=null,["note"]="Execution did not reach this registered operation; no success inferred."});allValid=false;
        }
    }
    c["execution"]!["exitCodes"]=exitCodes;c["execution"]!["timedOut"]=anyTimeout;
    checks.Add((JsonNode)new JsonObject{["id"]="oracle-exit",["label"]="Original oracle exit code and acceptance",["status"]=anyTimeout?"timeout":oraclePassed?"pass":"fail",["gm"]=null,["pk"]=null,["absDelta"]=null,["normalizedDelta"]=null,["threshold"]=0,["unit"]="exit code",["evidence"]=evidence[0]!["path"]!.DeepClone(),["note"]="Original process exit codes are retained in execution.exitCodes; oracle failure is result data, not exporter success."});
    c["status"]=anyTimeout?"timeout":anyExportError||!anyCapture?"missing":allValid?"pass":"fail";
    exporterBroken|=anyExportError;
    var perf=c["performance"]!;perf["samples"]=durationsGm.Count;
    if(durationsGm.Count>0){perf["gmMs"]=Median(durationsGm);perf["pkMs"]=Median(durationsPk);}
    perf["correctnessQualified"]=allValid&&!anyExportError;
    if(allValid&&!anyExportError&&durationsPk.Count>0&&Median(durationsPk)>0)perf["ratioGmOverPk"]=Median(durationsGm)/Median(durationsPk);
    perf["note"]=evaluationOnly&&durationsGm.Count==0
        ?"纯原生评估显示：该用例是评估/求交而非建模 API 区间，因此不给操作耗时或 GM/PK 倍率（避免把评估采样时间冒充操作性能）；几何与样本点来自同次真实执行。"
        :"API-only elapsed totals across captured steps; median of fresh-process samples, warmup=0. Cold JIT/cache effects remain; no stable throughput claim. Ratio withheld unless both sides succeeded and every current acceptance check passed.";
    Console.WriteLine($"RESULT {caseId}: {c["status"]}, exits={exitCodes.ToJsonString()}");Save();
}
manifest["run"]!["scope"]!["executed"]=executed;manifest["run"]!["finishedAt"]=DateTimeOffset.UtcNow.ToString("O");manifest["run"]!["collectionElapsedSeconds"]=clock.Elapsed.TotalSeconds;completed=true;Save();
var validation=WebProgressDataValidation.Validate(data);
File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());
if(validation["errors"]!.AsArray().Count>0)exporterBroken=true;
if(Array.IndexOf(args,"--publish-data-to")>=0&&!exporterBroken)
{
    var destination=Path.GetFullPath(Option("--publish-data-to","../web/data"),Path.GetDirectoryName(ScriptPath())!);
    WebProgressPublishing.Publish(data,id,destination);Console.WriteLine("PUBLISHED_DATA "+Path.GetRelativePath(root,destination));
}
if(args.Contains("--package"))
{
    var bundle=Path.Combine(Path.GetDirectoryName(data)!,id+".zip");if(File.Exists(bundle))throw new IOException("Bundle exists.");
    ZipFile.CreateFromDirectory(data,bundle,CompressionLevel.SmallestSize,false);Console.WriteLine("PACKAGE "+Path.GetRelativePath(root,bundle));
}
Console.WriteLine("MANIFEST "+Path.GetRelativePath(root,Path.Combine(run,"manifest.json")));
Console.WriteLine("COUNTS "+string.Join(", ",cases.GroupBy(c=>c!["status"]!.GetValue<string>()).Select(g=>g.Key+"="+g.Count())));
return exporterBroken?2:0;

void Save()
{
    WebProgressMetadata.Apply(manifest,registry);
    File.WriteAllText(Path.Combine(run,"manifest.json.tmp"),manifest.ToJsonString());File.Move(Path.Combine(run,"manifest.json.tmp"),Path.Combine(run,"manifest.json"),true);
    if(!completed)return;
    var indexPath=Path.Combine(data,"index.json");var index=File.Exists(indexPath)?JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject():new JsonObject{["schemaVersion"]=1,["runs"]=new JsonArray()};
    index["generatedAt"]=DateTimeOffset.UtcNow.ToString("O");index["latestRunId"]=id;
    var runs=index["runs"]!.AsArray();var existing=runs.FirstOrDefault(r=>r!["id"]!.GetValue<string>()==id);if(existing!=null)runs.Remove(existing);
    runs.Insert(0,new JsonObject{["id"]=id,["commit"]=commit,["generatedAt"]=now.ToString("O"),["label"]="Current GM / real PK oracle data",["manifest"]=$"runs/{id}/manifest.json"});
    File.WriteAllText(indexPath+".tmp",index.ToJsonString());File.Move(indexPath+".tmp",indexPath,true);
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
int Number(string name,int fallback)=>int.Parse(Option(name,fallback.ToString()),System.Globalization.CultureInfo.InvariantCulture);
JsonObject SourceHashes(){var hashes=new JsonObject();foreach(var path in registry["cases"]!.AsArray().Select(c=>c!["source"]!.GetValue<string>()).Concat(new[]{"scripts/RefreshWebProgress.cs","scripts/WebProgressCapture.cs","scripts/WebProgressDataValidation.cs","scripts/WebProgressPublishing.cs","scripts/WebProgressMetadata.cs","scripts/Directory.Build.props","scripts/ParasolidScriptHost.cs","scripts/ParasolidBodyCompare.cs","src/ProjectGmKernel.DisplayBridge/ParasolidDisplayBridge.cs"}).Distinct())if(File.Exists(path))hashes[path]=Hash(File.ReadAllBytes(path));return hashes;}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static double Median(List<double> values){var array=values.Order().ToArray();return array.Length%2==1?array[array.Length/2]:(array[array.Length/2-1]+array[array.Length/2])/2;}
static JsonArray Args(ProcessStartInfo info){var a=new JsonArray();foreach(var arg in info.ArgumentList)a.Add((JsonNode?)JsonValue.Create(arg));return a;}
static string Command(string command,string[] arguments){var info=new ProcessStartInfo(command){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};foreach(var arg in arguments)info.ArgumentList.Add(arg);using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new IOException(command+" failed: "+p.StandardError.ReadToEnd());return output.Trim();}
static string ReadCpu(){var line=File.Exists("/proc/cpuinfo")?File.ReadLines("/proc/cpuinfo").FirstOrDefault(l=>l.StartsWith("model name")):null;return line??RuntimeInformation.OSArchitecture.ToString();}
