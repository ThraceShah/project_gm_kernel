#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var script=Path.GetDirectoryName(Source())!;var root=Path.GetFullPath(Path.Combine(script,".."));
var output=Path.GetFullPath(Path.Combine(script,Option("--output","../temp_docs/native-unified-run")));
var baselineOption=Option("--baseline","");
var baseline=baselineOption.Length==0?null:Path.GetFullPath(Path.Combine(script,baselineOption));
var repeat=int.Parse(Option("--repeat","3"),System.Globalization.CultureInfo.InvariantCulture);
if(repeat is <1 or >64)throw new ArgumentException("repeat must be in 1..64");
for(var i=0;i<args.Length;i+=2)if(args[i] is not ("--output" or "--baseline" or "--repeat" or "--cases" or "--audit")||i+1==args.Length)
    throw new ArgumentException("Expected --output, --baseline, --repeat, --cases or --audit, each with a value");
var audit=Option("--audit","");
if(audit.Length!=0)
{
    var reasons=QualityReasons(Path.GetFullPath(Path.Combine(script,audit)),repeat);foreach(var reason in reasons)Console.WriteLine(reason);
    Console.WriteLine($"capture quality outstanding={reasons.Count}");return reasons.Any(r=>r.StartsWith("fail:"))?1:reasons.Count!=0?2:0;
}
if(Directory.Exists(output))throw new IOException("Use a new output directory; evidence is immutable");
Bench[] benches=[
    new("sphere","AnalyticBooleanOracle.cs",[],"equal-overlap-15902","analytic-boolean-oracle"),
    new("planar","BooleanOperationOracle.cs",[],"overlap-two-axes-15902","boolean-operation-oracle"),
    new("curved","RegularCurvedBooleanOracle.cs",["block-15902"],"block-15902","regular-curved-boolean-oracle"),
    new("solidblend","RegularSolidBlendOracle.cs",["plane-nurbs","rounding-only"],null,"regular-solid-blend-oracle"),
    new("shortedge","ShortEdgeBlendOracle.cs",["--angle=1","--radius=0.5","--tolerance=1e-07"],null,"short-edge-blend-oracle")];
var selected=Option("--cases",string.Join(',',benches.Select(b=>b.Id))).Split(',');
if(selected.Distinct().Count()!=selected.Length||selected.Any(id=>!benches.Any(b=>b.Id==id)))throw new ArgumentException("Unknown or duplicate case");
benches=benches.Where(b=>selected.Contains(b.Id)).ToArray();
var candidateSha=Git(root,"rev-parse","HEAD");var baselineSha=baseline==null?null:Git(baseline,"rev-parse","HEAD");
foreach(var tree in baseline==null?new[]{root}:new[]{root,baseline})
    if(Git(tree,"status","--porcelain","--untracked-files=no").Length!=0)throw new IOException("Tracked source changes must be committed before execution");
if(baseline!=null)foreach(var file in benches.Select(b=>b.Script).Concat(new[]{"WebProgressCapture.cs","ParasolidBodyCompare.cs","ParasolidMassProperties.cs","ParasolidScriptHost.cs"}))
    if(Hash(Path.Combine(root,"scripts",file))!=Hash(Path.Combine(baseline,"scripts",file)))throw new IOException("Both sources require the same current runner and helper contract: "+file);
Directory.CreateDirectory(output);
var samples=new JsonArray();var report=new JsonObject{
    ["format"]="native-unified-run-v2",["startedUtc"]=DateTimeOffset.UtcNow.ToString("O"),["candidateSha"]=candidateSha,["baselineSha"]=baselineSha,
    ["candidateNativeTree"]=Git(root,"rev-parse","HEAD:src/ProjectGmKernel.Native"),["candidateSrcTree"]=Git(root,"rev-parse","HEAD:src"),
    ["runnerSha256"]=Hash(Source()),["os"]=RuntimeInformation.OSDescription,["architecture"]=RuntimeInformation.ProcessArchitecture.ToString(),
    ["runtime"]=RuntimeInformation.FrameworkDescription,["repeat"]=repeat,["protocol"]=baseline==null?"single source development/quality run":"same-host ABBA sources per case; rep0 cold; remaining reps warm; serial complete operations",
    ["performanceQualified"]=false,["performanceScope"]="Raw API elapsed observations and work counts only; review this new window before accepting performance. Linux execution never signs Mac speed.",
    ["samples"]=samples};
var worst=0;
foreach(var bench in benches)foreach(var side in baseline==null?new[]{"candidate"}:new[]{"baseline","candidate","candidate","baseline"})
{
    var tree=side=="baseline"?baseline!:root;var sha=side=="baseline"?baselineSha!:candidateSha;
    var sample=$"{samples.Count:D2}-{bench.Id}-{side}";var directory=Path.Combine(output,sample);Directory.CreateDirectory(directory);
    var name="unified-"+Guid.NewGuid().ToString("N");
    var info=new ProcessStartInfo("dotnet"){WorkingDirectory=tree,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    var argv=new[]{"run","--no-cache",Path.Combine("scripts",bench.Script),"-c","Release","-p:BuildInParallel=false","-p:UseSharedCompilation=false","-p:GmInProcessLinkTasks=true","--"}
        .Concat(bench.Args).Concat(new[]{"--output-name="+name,"--bench-repeat="+repeat,"--bench-profile"}).ToArray();
    foreach(var arg in argv)info.ArgumentList.Add(arg);
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";info.Environment["GM_WEB_ROOT"]=tree;info.Environment["GM_WEB_CAPTURE"]=directory;
    info.Environment["GM_WEB_COMMIT"]=sha;info.Environment["GM_WEB_CAPTURE_COMMIT"]=sha;
    info.Environment["GM_WEB_GEOMETRY_AUTHOR"]=bench.Id=="shortedge"?"PK seed received by GM; GM performs FixBlends":"GM and PK construct inputs independently";
    info.Environment.Remove("GM_WEB_SELECT");if(bench.Selector!=null)info.Environment["GM_WEB_SELECT"]=bench.Selector;
    var sourceFiles=Git(tree,"ls-files","src/ProjectGmKernel.Native","src/ProjectGmKernel.Xt","scripts").Split('\n');
    File.WriteAllLines(Path.Combine(directory,"source.sha256"),sourceFiles.Select(f=>Hash(Path.Combine(tree,f))+"  "+f));
    File.WriteAllText(Path.Combine(directory,"command.json"),new JsonObject{["executable"]="dotnet",["argv"]=new JsonArray(argv.Select(a=>(JsonNode?)JsonValue.Create(a)).ToArray()),
        ["commit"]=sha,["selector"]=bench.Selector,["captureCommit"]=sha,["outputName"]=name}.ToJsonString());
    using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
    File.WriteAllText(Path.Combine(directory,"process-start.json"),new JsonObject{["pid"]=process.Id,["startedUtc"]=DateTimeOffset.UtcNow.ToString("O")}.ToJsonString());
    await process.WaitForExitAsync();
    File.WriteAllText(Path.Combine(directory,"process.exit"),process.ExitCode+"\n");
    var log=await stdout+"\nSTDERR\n"+await stderr;File.WriteAllText(Path.Combine(directory,"oracle.log"),log);
    var artifactSource=Path.Combine(tree,"temp_docs",bench.Artifacts,name);
    var artifacts=new JsonArray();
    if(Directory.Exists(artifactSource))foreach(var file in Directory.EnumerateFiles(artifactSource,"*",SearchOption.AllDirectories))
    {
        var relative=Path.GetRelativePath(artifactSource,file);var target=Path.Combine(directory,"products",relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
        artifacts.Add((JsonNode)new JsonObject{["path"]="products/"+relative,["sha256"]=Hash(target)});
    }
    var reasons=QualityReasons(directory,repeat);
    var hard=process.ExitCode is not (0 or 2)||log.Contains("FAIL ",StringComparison.Ordinal)||reasons.Any(r=>r.StartsWith("fail:",StringComparison.Ordinal));
    var unresolved=!hard&&(process.ExitCode==2||reasons.Count!=0);
    var status=hard?"Failed":unresolved?"Unresolved":"Verified";worst=hard?1:worst==1?1:unresolved?2:worst;
    samples.Add((JsonNode)new JsonObject{["case"]=bench.Id,["side"]=side,["commit"]=sha,["directory"]=sample,["processExit"]=process.ExitCode,["qualityStatus"]=status,
        ["reasons"]=new JsonArray(reasons.Select(r=>(JsonNode?)JsonValue.Create(r)).ToArray()),["artifacts"]=artifacts,
        ["inputProvenance"]=bench.Id=="shortedge"?"PK seed XT received by GM; PK retains original; not independent GM construction":"GM/PK independent constructions with the same parameters",
        ["xtScope"]="Product filenames retain the last repetition only; capture retains each actual step mesh and quality report",
        ["work"]=new JsonArray(log.Split('\n').Where(l=>l.StartsWith("PROFILE ")||l.StartsWith("WORK ")||l.StartsWith("ROOT_WORK ")).Select(l=>(JsonNode?)JsonValue.Create(l)).ToArray())});
    File.WriteAllText(Path.Combine(output,"report.json"),report.ToJsonString());
    Console.WriteLine($"{sample}: rawExit={process.ExitCode} quality={status}; {reasons.Count} outstanding quality checks");
}
report["finishedUtc"]=DateTimeOffset.UtcNow.ToString("O");report["runnerExit"]=worst;File.WriteAllText(Path.Combine(output,"report.json"),report.ToJsonString());
return worst;

static List<string> QualityReasons(string directory,int repeat)
{
    var reasons=new List<string>();var capturePath=Path.Combine(directory,"capture.json");
    if(!File.Exists(capturePath))return ["fail: missing capture"];
    var capture=JsonNode.Parse(File.ReadAllText(capturePath))!;
    if(capture["captureErrors"] is not JsonArray errors||errors.Count!=0)reasons.Add("fail: missing/nonempty captureErrors");
    if(capture["steps"] is not JsonArray steps||steps.Count!=repeat)reasons.Add("fail: unexpected operation count");
    if(capture["timings"] is not JsonArray times||times.Count!=2*repeat)reasons.Add("fail: unexpected timing count");
    else for(var rep=0;rep<repeat;rep++)foreach(var kernel in new[]{"gm","pk"})
    {
        var pair=times.Where(t=>t?["step"]?.GetValue<int>()==rep&&t?["kernel"]?.GetValue<string>()==kernel).ToArray();
        if(pair.Length!=1||pair[0]?["error"]?.GetValue<int>()!=0)reasons.Add("fail: missing/duplicate/failed API timing");
    }
    if(capture["checks"] is not JsonArray checks)return ["fail: missing checks"];
    for(var rep=0;rep<repeat;rep++)
    {
        var required=new List<string>{$"{rep}-body-count",$"gm-{rep}-api",$"pk-{rep}-api",$"{rep}-True-body-check",$"{rep}-False-body-check"};
        for(var kind=0;kind<5;kind++)required.Add($"{rep}-count-{kind}");
        foreach(var id in required)if(checks.Count(c=>c?["id"]?.GetValue<string>()==id)!=1)reasons.Add("missing: required check "+id);
    }
    foreach(var check in checks)
    {
        var status=check?["status"]?.GetValue<string>();if(status is not ("pass" or "diagnostic"))reasons.Add((status=="fail"?"fail: ":"missing: ")+check?["id"]?.GetValue<string>());
        if(check?["evidence"] is JsonValue evidence&&!File.Exists(Path.Combine(directory,evidence.GetValue<string>())))reasons.Add("fail: referenced evidence missing");
    }
    var mass=checks.Where(c=>c?["id"]?.GetValue<string>().StartsWith("quality-mass-",StringComparison.Ordinal)==true).ToArray();
    if(mass.Length<15*repeat)reasons.Add("missing: bounded mass evidence");
    foreach(var value in mass)if(value?["referenceModulus"]==null||value?["actualModulus"]==null||value?["normalizedUpper"]==null||value?["agreementStatus"]?.GetValue<string>()!="Verified")reasons.Add("missing: unverified mass modulus");
    if(!checks.Any(c=>c?["id"]?.GetValue<string>().StartsWith("quality-compare-",StringComparison.Ordinal)==true))reasons.Add("missing: current AssertEquivalent observation");
    if(!File.Exists(Path.Combine(directory,"identity","loaded.json")))reasons.Add("missing: actual loaded identity");
    return reasons;
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Git(string root,params string[] args)
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
    using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEnd();var stderr=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new IOException(stderr);return stdout.Trim();
}
internal sealed record Bench(string Id,string Script,string[] Args,string? Selector,string Artifacts);
