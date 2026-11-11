#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Current-format, fixed-source operation comparison. No historical report input.
if(args.Contains("--validate-qualification",StringComparer.Ordinal)){ValidateQualification();return 0;}
var script=Path.GetDirectoryName(ScriptPath())!;var root=Path.GetFullPath(Path.Combine(script,".."));
var baseline=Path.GetFullPath(Path.Combine(script,Option("--baseline","../temp_docs/pk-native-work-reduction/baseline-tree")));
var output=Path.GetFullPath(Path.Combine(script,Option("--output","../temp_docs/pk-native-work-reduction/controlled")));
if(Directory.Exists(output))throw new IOException("Use a fresh output directory; original samples are immutable");
Directory.CreateDirectory(output);
var baselineSha=Command(baseline,"git","rev-parse","HEAD");var candidateSha=Command(root,"git","rev-parse","HEAD");
foreach(var tree in new[]{baseline,root})if(Command(tree,"git","status","--porcelain","--untracked-files=no").Length!=0)throw new IOException("Source tree must have no tracked changes");
Bench[] cases=[
    new("sphere","AnalyticBooleanOracle.cs",[],"equal-overlap-15902","PK_BODY_boolean_2",false),
    new("planar","BooleanOperationOracle.cs",[],"overlap-two-axes-15902","PK_BODY_boolean_2",false),
    new("curved","RegularCurvedBooleanOracle.cs",["block-15902"],"block-15902","PK_BODY_boolean_2",false),
    new("solidblend","RegularSolidBlendOracle.cs",["plane-nurbs","rounding-only"],null,"PK_BODY_fix_blends",true),
    new("shortedge","ShortEdgeBlendOracle.cs",["--angle=1","--radius=0.5","--tolerance=1e-07"],null,"PK_BODY_fix_blends",true)];
var selection=Option("--cases",string.Join(',',cases.Select(c=>c.Id))).Split(',');
if(selection.Distinct().Count()!=selection.Length||selection.Any(id=>!cases.Any(c=>c.Id==id)))throw new ArgumentException("Unknown or duplicate case");
cases=cases.Where(c=>selection.Contains(c.Id)).ToArray();
foreach(var bench in cases)if(Hash(Path.Combine(baseline,"scripts",bench.Script))!=Hash(Path.Combine(root,"scripts",bench.Script)))throw new IOException("Oracle source differs between trees: "+bench.Script);
var samples=new JsonArray();var report=new JsonObject{
    ["format"]="native-modeling-work-comparison-v1",["startedUtc"]=DateTimeOffset.UtcNow.ToString("O"),
    ["baselineSha"]=baselineSha,["candidateSha"]=candidateSha,["baselineNativeTree"]=Command(baseline,"git","rev-parse","HEAD:src"),["candidateNativeTree"]=Command(root,"git","rev-parse","HEAD:src"),
    ["runnerSha256"]=Hash(ScriptPath()),["os"]=RuntimeInformation.OSDescription,["architecture"]=RuntimeInformation.ProcessArchitecture.ToString(),["runtime"]=RuntimeInformation.FrameworkDescription,["sdk"]=Command(root,"dotnet","--version"),
    ["cpu"]=Command(root,"sysctl","-n","machdep.cpu.brand_string"),["pkToySha"]=Command(root,"git","-C","third_party/PKToy","rev-parse","HEAD"),
    ["protocol"]="ABBA sources per case; three fresh-input operations per process; rep 0 cold, reps 1/2 warm; one process at a time; capture API interval only; unchanged oracle checks; fixed per-case GM/PK order; JIT managed build, default tiering; no cross-host ratio",["samples"]=samples};
var all=true;
foreach(var bench in cases)foreach(var source in new[]{"baseline","candidate","candidate","baseline"})
{
    var tree=source=="baseline"?baseline:root;var sha=source=="baseline"?baselineSha:candidateSha;
    var directory=Path.Combine(output,$"{samples.Count:D2}-{bench.Id}-{source}");Directory.CreateDirectory(directory);
    var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=tree};
    foreach(var arg in new[]{"run","--file",Path.Combine("scripts",bench.Script),"-c","Release","-p:UseSharedCompilation=false","-p:BuildInParallel=false","--"}.Concat(bench.Args).Concat(["--bench-repeat=3","--bench-profile"]))info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";info.Environment["GM_WEB_ROOT"]=tree;info.Environment["GM_WEB_CAPTURE"]=directory;info.Environment["GM_WEB_COMMIT"]=sha;
    if(bench.Selector!=null)info.Environment["GM_WEB_SELECT"]=bench.Selector;
    var load=Command(root,"sysctl","-n","vm.loadavg");var started=Stopwatch.GetTimestamp();var startedUtc=DateTime.UtcNow;
    using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
    var timeout=!process.WaitForExit(15*60*1000);if(timeout){process.Kill(entireProcessTree:true);process.WaitForExit();}
    var log=await stdout+"\nSTDERR\n"+await stderr;File.WriteAllText(Path.Combine(directory,"oracle.log"),log);
    var reasons=new List<string>();if(timeout)reasons.Add("timeout");if(process.ExitCode!=0)reasons.Add("process exit "+process.ExitCode);if(log.Contains("FAIL ",StringComparison.Ordinal)||log.Contains("The build failed.",StringComparison.Ordinal))reasons.Add("oracle/build failure");
    var pairs=new JsonArray();var capturePath=Path.Combine(directory,"capture.json");
    if(!File.Exists(capturePath))reasons.Add("missing capture");
    else
    {
        var capture=JsonNode.Parse(File.ReadAllText(capturePath))!.AsObject();Qualify(capture,bench,reasons);
        if(reasons.Count==0)for(var rep=0;rep<3;rep++)
        {
            var times=capture["timings"]!.AsArray().Where(t=>t!["step"]!.GetValue<int>()==rep).ToArray();
            var gm=times.Single(t=>t!["kernel"]!.GetValue<string>()=="gm")!["ms"]!.GetValue<double>();var pk=times.Single(t=>t!["kernel"]!.GetValue<string>()=="pk")!["ms"]!.GetValue<double>();
            pairs.Add((JsonNode)new JsonObject{["rep"]=rep,["temperature"]=rep==0?"cold":"warm",["gmMs"]=gm,["pkMs"]=pk,["ratioGmOverPk"]=gm/pk});
        }
    }
    foreach(var name in new[]{"export-errors.json","capture-errors.json"})if(File.Exists(Path.Combine(directory,name)))reasons.Add(name);
    var xtArtifacts=new JsonArray();
    var artifactDirectory=bench.Id switch{"sphere"=>"analytic-boolean-oracle","planar"=>"boolean-operation-oracle","curved"=>"regular-curved-boolean-oracle","solidblend"=>"regular-solid-blend-oracle",_=>"short-edge-blend-oracle"};
    var pattern=bench.Id switch{"sphere"=>"equal-overlap-15902-*.x_t","planar"=>"overlap-two-axes-15902-*.x_t","curved"=>"block-15902-*.x_t","solidblend"=>"plane-nurbs-fillet.x_t",_=>"a1-r0.5-t1E-07-reverseFalse-default-*.x_t"};
    var artifactSource=Path.Combine(tree,"temp_docs",artifactDirectory);
    if(Directory.Exists(artifactSource))foreach(var file in Directory.EnumerateFiles(artifactSource,pattern).Where(f=>File.GetLastWriteTimeUtc(f)>=startedUtc))
    {
        var target=Path.Combine(directory,"xt",Path.GetFileName(file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
        xtArtifacts.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(root,target),["sha256"]=Hash(target),["sourcePath"]=Path.GetRelativePath(tree,file),["repetition"]=2});
    }
    if(xtArtifacts.Count==0)reasons.Add("missing current-run final-repetition XT artifact");
    var qualified=reasons.Count==0;all&=qualified;
    samples.Add((JsonNode)new JsonObject{["case"]=bench.Id,["source"]=source,["commit"]=sha,["api"]=bench.Api,["qualified"]=qualified,["reasons"]=new JsonArray(reasons.Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray()),["pairs"]=qualified?pairs:null,
        ["arguments"]=new JsonArray(bench.Args.Concat(["--bench-repeat=3","--bench-profile"]).Select(a=>(JsonNode?)JsonValue.Create(a)).ToArray()),["selector"]=bench.Selector,["callOrder"]=bench.Blend?"PK then GM":"GM then PK",
        ["inputProvenance"]=bench.Id=="shortedge"?"PK constructs seed and retains its original; GM receives the unchanged PK seed XT and passes fresh-input comparison":"GM and PK construct inputs independently with identical numeric parameters",
        ["processExit"]=process.ExitCode,["timedOut"]=timeout,["processWallMs"]=Stopwatch.GetElapsedTime(started).TotalMilliseconds,["loadBefore"]=load,["loadAfter"]=Command(root,"sysctl","-n","vm.loadavg"),
        ["directory"]=Path.GetRelativePath(root,directory),["oracleSha256"]=Hash(Path.Combine(tree,"scripts",bench.Script)),["logSha256"]=Hash(Path.Combine(directory,"oracle.log")),["captureSha256"]=File.Exists(capturePath)?Hash(capturePath):null,
        ["xtArtifactsLastRepetition"]=xtArtifacts,["xtScope"]="Oracle output filenames are overwritten per repetition; these original bytes belong to final repetition 2 only. Every repetition retains its own capture, API checks and meshes.",
        ["work"]=new JsonArray(log.Split('\n').Where(l=>l.StartsWith("PROFILE ",StringComparison.Ordinal)||l.StartsWith("WORK ",StringComparison.Ordinal)).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray())});
    File.WriteAllText(Path.Combine(output,"report.json"),report.ToJsonString());
    Console.WriteLine($"{bench.Id} {source} qualified={qualified} exit={process.ExitCode} pairs={pairs.ToJsonString()} reasons={string.Join(';',reasons)}");
}
report["finishedUtc"]=DateTimeOffset.UtcNow.ToString("O");report["qualified"]=all;File.WriteAllText(Path.Combine(output,"report.json"),report.ToJsonString());
return all?0:1;

static void ValidateQualification()
{
    var bench=new Bench("test","",[],null,"PK_BODY_fix_blends",true);
    var steps=new JsonArray();var timings=new JsonArray();var checks=new JsonArray();
    for(var rep=0;rep<3;rep++)
    {
        steps.Add((JsonNode)new JsonObject{["id"]=$"operation-{rep+1}",["title"]=bench.Api});
        foreach(var kernel in new[]{"gm","pk"})timings.Add((JsonNode)new JsonObject{["step"]=rep,["kernel"]=kernel,["ms"]=1.0,["error"]=0,["resultStatus"]=18391});
        var ids=new List<string>{$"{rep}-body-count",$"{rep}-equivalence",$"gm-{rep}-api",$"pk-{rep}-api",$"{rep}-True-body-check",$"{rep}-False-body-check",$"{rep}-True-blend-fault",$"{rep}-False-blend-fault"};
        for(var i=0;i<5;i++)ids.Add($"{rep}-count-{i}");for(var i=0;i<15;i++)ids.Add($"{rep}-mass-{i}");
        foreach(var id in ids)checks.Add((JsonNode)new JsonObject{["id"]=id,["status"]="pass"});
    }
    var valid=new JsonObject{["sessionPrecision"]=1e-8,["pkVersion"]="38.0.150",["captureErrors"]=new JsonArray(),["steps"]=steps,["timings"]=timings,["checks"]=checks};
    var reasons=new List<string>();Qualify(valid,bench,reasons);if(reasons.Count!=0)throw new Exception("Positive qualification rejected: "+string.Join(';',reasons));
    Action<JsonObject>[] changes=[c=>c["timings"]![0]!["step"]=2,c=>c["timings"]![0]!["error"]=1,c=>c["checks"]!.AsArray().RemoveAt(0),c=>c["checks"]![0]!["status"]="diagnostic",c=>c.Remove("captureErrors"),c=>c["steps"]![0]!["title"]="wrong API"];
    foreach(var change in changes){var broken=valid.DeepClone().AsObject();change(broken);reasons.Clear();Qualify(broken,bench,reasons);if(reasons.Count==0)throw new Exception("Negative qualification accepted");}
    Console.WriteLine("PASS current-format operation qualification: valid sample and six missing/failing/ownership controls");
}

static void Qualify(JsonObject capture,Bench bench,List<string> reasons)
{
    if(capture["pkVersion"]?.GetValue<string>()!="38.0.150")reasons.Add("PK version must be 38.0.150");
    if(capture["sessionPrecision"]?.GetValue<double>()!=1e-8)reasons.Add("PK session precision must be 1e-8");
    if(capture["captureErrors"] is not JsonArray errors||errors.Count!=0)reasons.Add("missing/nonempty captureErrors");
    if(capture["steps"] is not JsonArray steps||steps.Count!=3){reasons.Add("expected three operation steps");return;}
    var ids=new HashSet<string>();foreach(var step in steps)
    {
        var id=step?["id"]?.GetValue<string>();if(id==null||!ids.Add(id)||!Enumerable.Range(1,3).Select(i=>"operation-"+i).Contains(id)||step?["title"]?.GetValue<string>()!=bench.Api)reasons.Add("wrong/duplicate operation owner or API");
    }
    if(capture["timings"] is not JsonArray times||times.Count!=6){reasons.Add("expected six timings");return;}
    foreach(var time in times)
    {
        var kernel=time?["kernel"]?.GetValue<string>();var owner=time?["step"]?.GetValue<int>()??-1;var ms=time?["ms"]?.GetValue<double>()??double.NaN;
        if(kernel is not ("gm" or "pk")||owner<0||owner>2||!double.IsFinite(ms)||ms<=0||time?["error"]?.GetValue<int>()!=0||time?["resultStatus"] is not JsonValue)reasons.Add("invalid timing/owner/API status");
    }
    for(var rep=0;rep<3;rep++)foreach(var kernel in new[]{"gm","pk"})if(times.Count(t=>t?["step"]?.GetValue<int>()==rep&&t?["kernel"]?.GetValue<string>()==kernel)!=1)reasons.Add("missing/duplicate timing pair");
    if(capture["checks"] is not JsonArray checks){reasons.Add("missing checks");return;}
    var passed=new HashSet<string>();foreach(var check in checks)
    {
        var id=check?["id"]?.GetValue<string>();if(id==null||!passed.Add(id)||check?["status"]?.GetValue<string>()!="pass")reasons.Add("missing/duplicate/nonpass check "+id);
    }
    for(var rep=0;rep<3;rep++)
    {
        var required=new List<string>{$"{rep}-body-count",$"{rep}-equivalence",$"gm-{rep}-api",$"pk-{rep}-api",$"{rep}-True-body-check",$"{rep}-False-body-check"};
        for(var i=0;i<5;i++)required.Add($"{rep}-count-{i}");for(var i=0;i<15;i++)required.Add($"{rep}-mass-{i}");
        if(bench.Blend){required.Add($"{rep}-True-blend-fault");required.Add($"{rep}-False-blend-fault");}
        foreach(var id in required)if(!passed.Contains(id))reasons.Add("missing required check "+id);
    }
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Command(string root,string file,params string[] args)
{
    var info=new ProcessStartInfo(file){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
    using var process=Process.Start(info)!;var text=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new IOException(error);return text.Trim();
}
internal sealed record Bench(string Id,string Script,string[] Args,string? Selector,string Api,bool Blend);
