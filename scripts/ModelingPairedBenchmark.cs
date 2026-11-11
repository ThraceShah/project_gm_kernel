#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Paired GM/real-PK modeling benchmark for representative solid-modeling
// operations. Reuses the existing strict oracles as workload drivers: each
// sample is one fresh oracle process (cold) or one fresh-input repetition
// inside a warmed process (warm, --bench-repeat). The measured interval is
// exactly the public modeling API call recorded by WebProgressCapture
// (capture.json timings), identical to the platform performance field scope.
// Input construction, observer meshing, XT export, comparison and I/O are
// timed separately and never folded into the operation ratio.

var scriptDirectory=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
var output=Path.GetFullPath(Option("--output",Path.Combine(root,"temp_docs","modeling-performance")));
var mode=Option("--mode","measure");
if(mode is not ("measure" or "profile"))throw new ArgumentException("--mode must be measure or profile");
var coldCount=Number("--cold",3);
var warmCount=Number("--warm",5);
if(coldCount<1||warmCount<1||warmCount>63)throw new ArgumentException("--cold >= 1 and 1 <= --warm <= 63 required");
var requested=Option("--cases","sphereBoolean,planarBoolean,regularcurved,solidblend,shortedge").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);

var cases=new[]{
    new BenchCase("sphereBoolean.equal-overlap-subtract","AnalyticBooleanOracle.cs",Array.Empty<string>(),"equal-overlap-15902","gm-first-then-pk",
        "PK_BODY_boolean_2 / KernelRuntime.BodyBoolean2","independent construction, identical numeric parameters",1200),
    new BenchCase("planarBoolean.overlap-two-axes-subtract","BooleanOperationOracle.cs",Array.Empty<string>(),"overlap-two-axes-15902","gm-first-then-pk",
        "PK_BODY_boolean_2 / KernelRuntime.BodyBoolean2","independent construction, identical numeric parameters",1200),
    new BenchCase("regularcurved.block-subtract","RegularCurvedBooleanOracle.cs",new[]{"block-15902"},"block-15902","gm-first-then-pk",
        "PK_BODY_boolean_2 / KernelRuntime.BodyBoolean2","independent construction, identical numeric parameters",1800),
    new BenchCase("solidblend.plane-nurbs","RegularSolidBlendOracle.cs",new[]{"plane-nurbs","rounding-only"},null,"pk-first-then-gm",
        "PK_BODY_fix_blends / KernelRuntime.BodyFixBlends","independent construction, identical numeric parameters",1800),
    new BenchCase("shortedge.a1-r0.5-t1e-07","ShortEdgeBlendOracle.cs",new[]{"--angle=1","--radius=0.5","--tolerance=1e-07"},null,"pk-first-then-gm",
        "PK_BODY_fix_blends / KernelRuntime.BodyFixBlends","shared PK-seeded input XT received unchanged by both producers",5400),
};
var unknown=requested.Where(r=>!cases.Any(c=>c.Id.StartsWith(r,StringComparison.Ordinal))).ToArray();
if(unknown.Length>0)throw new ArgumentException("Unknown case prefix: "+string.Join(',',unknown));
var selected=cases.Where(c=>requested.Any(r=>c.Id.StartsWith(r,StringComparison.Ordinal))).ToArray();
if(selected.Length==0)throw new ArgumentException("No cases selected");

var runId=DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ")+"-"+Command("git",["rev-parse","--short","HEAD"]);
var runDirectory=Path.Combine(output,"runs",runId,"benchmark");
Directory.CreateDirectory(runDirectory);
var host=new JsonObject{
    ["os"]=RuntimeInformation.OSDescription,
    ["architecture"]=RuntimeInformation.OSArchitecture.ToString(),
    ["processArchitecture"]=RuntimeInformation.ProcessArchitecture.ToString(),
    ["logicalProcessors"]=Environment.ProcessorCount,
    ["cpu"]=OperatingSystem.IsMacOS()?Command("sysctl",["-n","machdep.cpu.brand_string"]):Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
    ["memoryBytes"]=OperatingSystem.IsMacOS()?Command("sysctl",["-n","hw.memsize"]):null,
    ["loadAverageAtStart"]=LoadAverage()};
var report=new JsonObject{
    ["schemaVersion"]=1,
    ["kind"]="modeling-paired-benchmark",
    ["runId"]=runId,
    ["generatedAt"]=DateTimeOffset.UtcNow.ToString("O"),
    ["provenance"]=new JsonObject{
        ["commit"]=Command("git",["rev-parse","HEAD"]),
        ["nativeTreeHash"]=Command("git",["rev-parse","HEAD:src"]),
        ["pkToyCommit"]=Command("git",["-C","third_party/PKToy","rev-parse","HEAD"]),
        ["dirtyAtStart"]=Command("git",["status","--porcelain","--untracked-files=no"]).Length>0,
        ["host"]=host,
        ["sdk"]=Command("dotnet",["--version"]),
        ["runtime"]=RuntimeInformation.FrameworkDescription,
        ["tieredCompilation"]=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")??"runtime-default",
        ["threads"]="serial: one oracle process at a time, one PK session and one GM session per process, single-threaded API calls",
        ["cache"]="command-owned certified bound caches are cleared at every modeling API entry by kernel contract; no warm cache is shared between samples",
        ["protocol"]=new JsonObject{
            ["cold"]="first captured modeling API of a fresh oracle process (dotnet run --file); includes first-call JIT tiering and static initialization of the reached code paths after input construction already warmed part of the kernel",
            ["warm"]="same process, --bench-repeat, repetition 0 is that process's cold sample (counted in cold), repetitions 1..N counted as warm; every repetition builds fresh unoperated inputs and never re-executes an operation on an already modified body",
            ["timingScope"]="capture.json timings = Stopwatch interval around the public modeling API call only; oracle stdout TIME/FIX lines additionally include the opt-in web observer and are not used for ratios",
            ["ratio"]="GM elapsed ms / PK elapsed ms per repetition; ratio reported only when every counted sample passed the strict per-operation PK acceptance of that oracle",
            ["sampleOrder"]="cheap cases first, heavy cases last, samples serial on one host",
            ["acceptance"]="each operation is independently accepted against real PK with the same parameters (fresh XT receive + full BODY_check + strict PK_DEBUG_BODY_compare + mass properties inside each oracle); no combination workflow is mixed into these per-operation ratios"}}};
var casesJson=new JsonArray();report["cases"]=casesJson;

foreach(var bench in selected)
{
    Console.WriteLine($"CASE {bench.Id} mode={mode} cold={coldCount} warm={warmCount}");
    var caseJson=new JsonObject{
        ["id"]=bench.Id,
        ["operation"]=new JsonObject{
            ["api"]=bench.OperationApi,
            ["kind"]="independent-operation",
            ["combination"]=false,
            ["inputProvenance"]=bench.InputProvenance,
            ["callOrder"]=bench.CallOrder},
        ["oracle"]=new JsonObject{["script"]="scripts/"+bench.Script,["arguments"]=new JsonArray(bench.Args.Where(a=>a.Length>0).Select(a=>(JsonNode?)JsonValue.Create(a)).ToArray()),["selector"]=bench.Selector},
        ["samples"]=new JsonArray()};
    casesJson.Add(caseJson);
    var samples=(JsonArray)caseJson["samples"]!;
    if(mode=="measure")
    {
        for(var cold=0;cold<coldCount;cold++)
        {
            samples.Add(RunSample(bench,$"cold-{cold}",null,runDirectory));
            Save(report,runDirectory);
        }
        samples.Add(RunSample(bench,"warm",warmCount+1,runDirectory));
    }
    else samples.Add(RunSample(bench,"profile",2,runDirectory,profile:true));
    if(mode=="measure")
    {
        caseJson["cold"]=Summarize(samples,"cold");
        caseJson["warm"]=Summarize(samples,"warm");
    }
    Save(report,runDirectory);
}
report["finishedAt"]=DateTimeOffset.UtcNow.ToString("O");
host["loadAverageAtEnd"]=LoadAverage();
Save(report,runDirectory);
if(mode=="measure")
{
    // The summary pointer is only advanced by full measure runs; profile and
    // partial runs keep their immutable report under runs/<runId>/benchmark/.
    report["immutableReport"]="runs/"+runId+"/benchmark/report.json";
    var summaryPath=Path.Combine(output,"paired-benchmark-latest.json");
    File.WriteAllText(summaryPath+".tmp",report.ToJsonString());File.Move(summaryPath+".tmp",summaryPath,true);
}
Console.WriteLine("REPORT "+Path.Combine("temp_docs","modeling-performance","paired-benchmark-latest.json"));
Console.WriteLine("RUN "+Path.Combine("temp_docs","modeling-performance","runs",runId,"benchmark","report.json"));
return 0;

JsonObject RunSample(BenchCase bench,string label,int? repetitions,string runRoot,bool profile=false)
{
    var caseDirectory=Path.Combine(runRoot,"cases",bench.Id,label);
    Directory.CreateDirectory(caseDirectory);
    var arguments=bench.Args.Where(a=>a.Length>0).ToList();
    if(repetitions!=null)arguments.Add($"--bench-repeat={repetitions}");
    if(profile)arguments.Add("--bench-profile");
    var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
    foreach(var argument in new[]{"run","--file",Path.Combine("scripts",bench.Script),"-c","Release","-p:UseSharedCompilation=false"})info.ArgumentList.Add(argument);
    info.ArgumentList.Add("--");foreach(var argument in arguments)info.ArgumentList.Add(argument);
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";
    info.Environment["GM_WEB_ROOT"]=root;
    info.Environment["GM_WEB_CAPTURE"]=caseDirectory;
    info.Environment["GM_WEB_COMMIT"]=report["provenance"]!["commit"]!.GetValue<string>();
    if(bench.Selector!=null)info.Environment["GM_WEB_SELECT"]=bench.Selector;
    var loadBefore=LoadAverage();
    var started=Stopwatch.GetTimestamp();
    using var process=new Process{StartInfo=info};process.Start();
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
    var timedOut=false;
    if(!process.WaitForExit(bench.TimeoutSeconds*1000)){timedOut=true;process.Kill(entireProcessTree:true);process.WaitForExit();}
    var wallMs=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    var text=stdout.Result+"\nSTDERR\n"+stderr.Result;
    File.WriteAllText(Path.Combine(caseDirectory,"oracle.log"),text);
    var loadAfter=LoadAverage();
    var capturePath=Path.Combine(caseDirectory,"capture.json");
    var capture=File.Exists(capturePath)?JsonNode.Parse(File.ReadAllText(capturePath))!.AsObject():null;
    var timings=capture?["timings"]?.AsArray();
    var gm=timings==null?[]:timings.Where(t=>t!["kernel"]!.GetValue<string>()=="gm").Select(t=>t!["ms"]!.GetValue<double>()).ToArray();
    var pk=timings==null?[]:timings.Where(t=>t!["kernel"]!.GetValue<string>()=="pk").Select(t=>t!["ms"]!.GetValue<double>()).ToArray();
    var checks=capture?["checks"]?.AsArray();
    var checksQualified=checks!=null&&checks.Count>0&&checks.All(c=>c!["status"]!.GetValue<string>() is "pass" or "diagnostic");
    var paired=gm.Length>0&&gm.Length==pk.Length;
    var errorsFree=timings!=null&&timings.All(t=>t!["error"]!.GetValue<int>()==0);
    var oracleAccepted=process.ExitCode==0&&!timedOut&&!text.Contains("FAIL ",StringComparison.Ordinal);
    var qualified=checksQualified&&paired&&errorsFree&&oracleAccepted;
    var benchLines=new JsonArray();
    var profileLines=new JsonArray();
    foreach(var line in text.Split('\n'))
    {
        if(line.StartsWith("BENCH ",StringComparison.Ordinal))benchLines.Add((JsonNode?)JsonValue.Create(line.Trim()));
        if(line.StartsWith("PROFILE ",StringComparison.Ordinal))profileLines.Add((JsonNode?)JsonValue.Create(line.Trim()));
    }
    var sample=new JsonObject{
        ["label"]=label,
        ["kind"]=label.StartsWith("cold",StringComparison.Ordinal)?"cold":"warm",
        ["processExit"]=process.ExitCode,
        ["timedOut"]=timedOut,
        ["processWallMs"]=wallMs,
        ["qualified"]=qualified,
        ["qualification"]=new JsonObject{["captureChecks"]=checksQualified,["pairedTimings"]=paired,["apiErrorsFree"]=errorsFree,["oracleAccepted"]=oracleAccepted},
        ["gmMs"]=new JsonArray(gm.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),
        ["pkMs"]=new JsonArray(pk.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),
        ["repetitions"]=gm.Length,
        ["benchPhases"]=benchLines,
        ["profiles"]=profileLines,
        ["loadAverageBefore"]=loadBefore,
        ["loadAverageAfter"]=loadAfter,
        ["oracleLog"]=Path.Combine("runs",runId,"benchmark","cases",bench.Id,label,"oracle.log"),
        ["oracleLogSha256"]=Hash(Encoding.UTF8.GetBytes(text)),
        ["capture"]=Path.Combine("runs",runId,"benchmark","cases",bench.Id,label,"capture.json"),
        ["pkVersion"]=capture?["pkVersion"]?.DeepClone(),
        ["sessionPrecision"]=capture?["sessionPrecision"]?.DeepClone()};
    Console.WriteLine($"  {label}: exit={process.ExitCode} qualified={qualified} gm=[{string.Join(",",gm.Select(v=>v.ToString("F1",CultureInfo.InvariantCulture)))}] pk=[{string.Join(",",pk.Select(v=>v.ToString("F3",CultureInfo.InvariantCulture)))}] wall={wallMs:F0}ms load={loadBefore}");
    return sample;
}

static JsonObject Summarize(JsonArray samples,string kind)
{
    var cold=new List<double>();var coldPk=new List<double>();var warm=new List<double>();var warmPk=new List<double>();var unqualified=new JsonArray();
    foreach(var row in samples.OfType<JsonObject>())
    {
        var g=((JsonArray)row["gmMs"]!).Select(v=>v!.GetValue<double>()).ToArray();
        var p=((JsonArray)row["pkMs"]!).Select(v=>v!.GetValue<double>()).ToArray();
        var qualified=row["qualified"]!.GetValue<bool>();
        for(var i=0;i<Math.Min(g.Length,p.Length);i++)
        {
            var isWarm=row["kind"]!.GetValue<string>()=="warm"&&i>0;
            if(!qualified){unqualified.Add((JsonNode?)JsonValue.Create($"{row["label"]}[{i}] gm={g[i].ToString("F3",CultureInfo.InvariantCulture)} pk={p[i].ToString("F3",CultureInfo.InvariantCulture)}"));continue;}
            if(isWarm){warm.Add(g[i]);warmPk.Add(p[i]);}
            else{cold.Add(g[i]);coldPk.Add(p[i]);}
        }
    }
    var gm=kind=="warm"?warm:cold;var pk=kind=="warm"?warmPk:coldPk;
    var ratios=new List<double>();for(var i=0;i<gm.Count;i++)if(pk[i]>0)ratios.Add(gm[i]/pk[i]);
    var result=new JsonObject{
        ["scope"]="operation-only public API elapsed, same input parameters per GM/PK pair",
        ["countedRepetitions"]=gm.Count,
        ["qualified"]=gm.Count>0,
        ["gmMs"]=new JsonArray(gm.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),
        ["pkMs"]=new JsonArray(pk.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),
        ["sampleRatios"]=new JsonArray(ratios.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),
        ["unqualifiedSamples"]=unqualified};
    if(gm.Count>0)
    {
        result["gmMedianMs"]=Median(gm);result["pkMedianMs"]=Median(pk);
        result["gmMinMs"]=gm.Min();result["gmMaxMs"]=gm.Max();
        result["pkMinMs"]=pk.Min();result["pkMaxMs"]=pk.Max();
        result["gmP95Ms"]=P95(gm);result["pkP95Ms"]=P95(pk);
        if(ratios.Count>0&&Median(pk)>0)
        {
            result["ratioGmOverPkMedianOfSamples"]=Median(ratios);
            result["ratioGmOverPkOfMedians"]=Median(gm)/Median(pk);
        }
        else result["ratioGmOverPkMedianOfSamples"]=null;
    }
    else result["ratioGmOverPkMedianOfSamples"]=null;
    return result;
}

static double Median(List<double> values){var a=values.Order().ToArray();return a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2;}
static double P95(List<double> values){var a=values.Order().ToArray();return a[Math.Max(0,(int)Math.Ceiling(.95*a.Length)-1)];}
static void Save(JsonObject report,string runDirectory)
{
    Directory.CreateDirectory(runDirectory);
    File.WriteAllText(Path.Combine(runDirectory,"report.json.tmp"),report.ToJsonString());File.Move(Path.Combine(runDirectory,"report.json.tmp"),Path.Combine(runDirectory,"report.json"),true);
}
static string LoadAverage()
{
    try{var info=new ProcessStartInfo("sysctl",["-n","vm.loadavg"]){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();return output.Trim();}
    catch{return "unavailable";}
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
int Number(string name,int fallback)=>int.Parse(Option(name,fallback.ToString()),CultureInfo.InvariantCulture);
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Command(string command,string[] arguments)
{
    var info=new ProcessStartInfo(command){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
    foreach(var argument in arguments)info.ArgumentList.Add(argument);
    using var p=Process.Start(info)!;var output=p.StandardOutput.ReadToEnd();p.WaitForExit();
    if(p.ExitCode!=0)throw new IOException(command+" failed: "+p.StandardError.ReadToEnd());
    return output.Trim();
}
internal sealed record BenchCase(string Id,string Script,string[] Args,string? Selector,string CallOrder,string OperationApi,string InputProvenance,int TimeoutSeconds);
