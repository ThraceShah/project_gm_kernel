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
    try{return ReadQualityReasons(directory,repeat);}
    catch(Exception error) when(error is System.Text.Json.JsonException or InvalidOperationException or FormatException or IOException)
    {return ["fail: unreadable current quality evidence: "+error.Message];}
}
static List<string> ReadQualityReasons(string directory,int repeat)
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
    foreach(var group in checks.GroupBy(c=>Text(c,"id")))if(group.Key==null||group.Count()!=1)reasons.Add("fail: missing/duplicate check identity");
    var identity=Read("identity/loaded.json");
    foreach(var field in new[]{"calculationCommit","captureCommit"})
        if(Text(identity,field) is not string commit||commit.Length!=40||!commit.All(char.IsAsciiHexDigit))reasons.Add("missing: loaded "+field);
    foreach(var field in new[]{"captureSourceFileSha256","bodyCompareSourceFileSha256","massSourceFileSha256","kernelDllSha256","kernelPdbSha256","oracleDllSha256","oraclePdbSha256"})
        if(!Digest(Text(identity,field)))reasons.Add("missing: loaded "+field);
    var commandPath=Path.Combine(directory,"command.json");
    if(File.Exists(commandPath))
    {
        var command=JsonNode.Parse(File.ReadAllText(commandPath))!;
        Same(command["commit"],identity?["calculationCommit"],"command calculation source");
        Same(command["captureCommit"],identity?["captureCommit"],"command capture source");
    }
    var exitPath=Path.Combine(directory,"process.exit");
    if(File.Exists(exitPath))
    {
        if(!int.TryParse(File.ReadAllText(exitPath).Trim(),out var rawExit)||rawExit is not (0 or 2))reasons.Add("fail: original process exit");
        else if(rawExit==2)reasons.Add("missing: original process unresolved");
    }
    var comparisons=new List<JsonNode>();var masses=new List<JsonNode>();
    foreach(var kind in new[]{"compare","mass"})
    {
        var observations=checks.Where(c=>Text(c,"id")?.StartsWith("quality-"+kind+"-",StringComparison.Ordinal)==true).ToArray();
        foreach(var group in observations.GroupBy(c=>Text(c,"evidence")))
        {
            var report=Read(group.Key);if(report==null)continue;
            Same(report["captureIdentity"],identity,"report loaded identity");
            var source=report["sourceBinding"];
            if(source==null)reasons.Add("missing: report source binding "+group.Key);
            else
            {
                Same(source["identity"],identity,"source loaded identity");
                if(Text(source,"stage") is not string stage||Text(report,"label") is not string label||label!=stage&&label!=stage+" strict")
                    reasons.Add("fail: report/source stage mismatch");
                if(Text(source,"kind")=="GM-fresh-vs-native")
                {
                    Same(report["referenceBody"],source["referenceNativeBody"],"reference entity");
                    Same(report["actualBody"],source["freshNativeBody"],"actual entity");
                    if(Text(source,"actualOriginKernel")!="GM"||!Digest(Text(source,"gmXtSha256"))||!Digest(Text(source,"referenceXtSha256")))
                        reasons.Add("missing: GM/native XT source identity");
                }
            }
            foreach(var summary in group)
            {
                if(source!=null)Same(summary!["sourceBinding"],source,"summary/report source binding");
                else if(summary!["sourceBinding"]!=null)reasons.Add("fail: summary invents source binding");
            }
            if(kind=="compare")
            {
                comparisons.Add(report);
                if(group.Count()!=1)reasons.Add("fail: comparison report reused by multiple summaries");
                foreach(var summary in group)
                {
                    if(Text(summary,"label")!=Text(report,"label")+" / current AssertEquivalent")reasons.Add("fail: compare label mismatch");
                    if(Text(summary,"status")!=(Text(report,"status")=="Verified"?"pass":"fail"))reasons.Add("fail: comparison summary contradicts report");
                }
                if(Text(report,"status")=="Rejected"||report["error"]!=null)reasons.Add("fail: AssertEquivalent rejected");
                else if(Text(report,"status")!="Verified")reasons.Add("missing: AssertEquivalent qualification");
                Zero(report,"nativeError");Same(report["referenceType"],report["actualType"],"compared body types");
                var mask=report["mask"];
                if(mask==null||!Number(mask,"max_diffs",out var limit)||limit<=0||mask["all_tests"]?.GetValue<bool>()!=false
                    ||mask["acc_dev_tests"]?.GetValue<bool>()!=false||mask["tol_dev_tests"]?.GetValue<bool>()!=true)
                    reasons.Add("missing: strict comparison mask");
                foreach(var field in new[]{"num_tests","class_tests","sense_tests","tol_tests","missing_tests","non_match_tests"})
                    if(mask?[field]?.GetValue<bool>()!=true)reasons.Add("missing: compare mask "+field);
                foreach(var field in new[]{"globalResult","localResult","facePairs"})if(!Number(report,field,out _))reasons.Add("missing: compare "+field);
                foreach(var field in new[]{"globalDiffs","localDiffs","facePairRecords"})if(report[field] is not JsonArray)reasons.Add("missing: raw compare "+field);
                if(report["globalDiffs"] is JsonArray global)foreach(var diff in global)
                    Same(diff?["masters"],diff?["similars"],"global comparison counts");
                if(report["localDiffs"] is JsonArray diffs)foreach(var diff in diffs)
                    if(diff?["diagnostic"]?.GetValue<bool>()!=true&&diff?["conditionalFiniteProof"]?["proved"]?.GetValue<bool>()!=true)
                        reasons.Add("fail: non-diagnostic compare difference without finite proof");
            }
            else
            {
                masses.Add(report);Zero(report,"referenceError");Zero(report,"actualError");
                if(report["error"]!=null)reasons.Add("fail: mass observation error");
                if(!Number(report,"accuracy",out var accuracy)||accuracy<=0||accuracy>1||!Number(report,"threshold",out var threshold)||threshold!=1e-7)
                    reasons.Add("missing: bounded mass accuracy/threshold");
                string[] names=["volume","mass","cog.x","cog.y","cog.z","inertia.xx","inertia.xy","inertia.xz","inertia.yx","inertia.yy","inertia.yz","inertia.zx","inertia.zy","inertia.zz","area"];
                if(report["properties"] is not JsonArray properties){reasons.Add("missing: mass properties");continue;}
                if(properties.Count!=names.Length||group.Count()!=names.Length)reasons.Add("missing: 15 bounded mass components and summaries");
                var aggregate="Verified";var complete=true;
                foreach(var name in names)
                {
                    var values=properties.Where(p=>Text(p,"property")==name).ToArray();
                    var summaries=group.Where(c=>Text(c,"label")==Text(report,"label")+" / "+name).ToArray();
                    if(values.Length!=1||summaries.Length!=1){reasons.Add("missing: unique mass component "+name);complete=false;continue;}
                    var value=values[0]!;var summary=summaries[0]!;
                    foreach(var field in new[]{"referenceEstimate","actualEstimate","referenceModulus","actualModulus","normalizedLower","normalizedUpper","agreementStatus"})
                        Same(summary[field],value[field],"mass "+name+" "+field);
                    Same(summary["stage"],report["label"],"mass stage");Same(summary["accuracy"],report["accuracy"],"mass accuracy");
                    Same(summary["threshold"],report["threshold"],"mass threshold");
                    if(!Number(value,"referenceEstimate",out var a)||!Number(value,"actualEstimate",out var b)
                        ||!Number(value,"referenceModulus",out var ra)||!Number(value,"actualModulus",out var rb)
                        ||!Number(value,"normalizedLower",out var lower)||!Number(value,"normalizedUpper",out var upper))
                    {reasons.Add("missing: finite bounded mass fields "+name);complete=false;continue;}
                    if(ra<0||rb<0||lower<0||upper<lower){reasons.Add("fail: invalid mass interval "+name);continue;}
                    // Same outward interval arithmetic as the current producer; no PK integration.
                    var a0=Math.BitDecrement(a-ra);var a1=Math.BitIncrement(a+ra);var b0=Math.BitDecrement(b-rb);var b1=Math.BitIncrement(b+rb);
                    var minAbs=a0<=0&&a1>=0?0:Math.Min(Math.Abs(a0),Math.Abs(a1));var maxAbs=Math.Max(Math.Abs(a0),Math.Abs(a1));
                    var gap=Math.Max(0,Math.BitDecrement(Math.Max(a0-b1,b0-a1)));var diameter=Math.BitIncrement(Math.Max(Math.Abs(a0-b1),Math.Abs(a1-b0)));
                    var expectedLower=Math.Max(0,Math.BitDecrement(gap/Math.BitIncrement(1+maxAbs)));
                    var expectedUpper=Math.BitIncrement(diameter/Math.BitDecrement(1+minAbs));
                    if(!double.IsFinite(expectedUpper)||lower!=expectedLower||upper!=expectedUpper)reasons.Add("fail: mass bounds contradict estimates/moduli "+name);
                    var state=upper<1e-7?"Verified":lower>=1e-7?"Different":"Unresolved";
                    if(Text(value,"agreementStatus")!=state||Text(summary,"status")!=(state=="Verified"?"pass":state=="Different"?"fail":"missing"))
                        reasons.Add("fail: mass state contradicts bounds "+name);
                    if(state=="Different")aggregate=state;else if(state=="Unresolved"&&aggregate=="Verified")aggregate=state;
                    if(state!="Verified")reasons.Add((state=="Different"?"fail: ":"missing: ")+"mass "+name+" "+state);
                    if(Text(source,"actualOriginKernel")=="GM")
                    {Same(summary["gm"],value["actualEstimate"],"GM mass estimate");Same(summary["pk"],value["referenceEstimate"],"PK mass estimate");}
                }
                if(complete&&Text(report,"agreementStatus")!=aggregate)reasons.Add("fail: mass aggregate contradicts components");
            }
        }
    }
    if(capture["steps"] is JsonArray actualSteps)for(var rep=0;rep<actualSteps.Count;rep++)
    {
        var step=actualSteps[rep]!;
        if(Text(step,"id")!="operation-"+(rep+1))reasons.Add("fail: operation identity/order mismatch");
        var gm=Read(Text(step["gm"],"after"));var pk=Read(Text(step["pk"],"after"));if(gm==null||pk==null)continue;
        var gs=gm["source"];var ps=pk["source"];
        Same(gs?["captureIdentity"],identity,"GM mesh loaded identity");
        var snapshots=comparisons.Where(r=>Text(r["sourceBinding"],"kind")=="same-instance-snapshot-pair"
            &&r["sourceBinding"]?["step"]?.GetValue<int>()==rep).ToArray();
        if(snapshots.Length!=1)reasons.Add("missing: unique step snapshot comparison "+rep);
        foreach(var snapshot in snapshots)
        {
            Same(snapshot["sourceBinding"]!["gmMesh"],step["gm"]!["after"],"step GM mesh");
            Same(snapshot["sourceBinding"]!["pkMesh"],step["pk"]!["after"],"step PK mesh");
            // Compare consumes retained PK copies, not the mesh tessellation
            // snapshots. The producer binds those same-call entities by step
            // and mesh links; their tags must not be equated to display tags.
            foreach(var field in new[]{"actualBody","referenceBody"})
                if(!Number(snapshot,field,out var tag)||tag<=0)reasons.Add("missing: retained snapshot entity "+field);
        }
        bool Product(JsonNode r)=>Text(r["sourceBinding"],"kind")=="GM-fresh-vs-native"
            &&Equal(r["sourceBinding"]?["gmLiveBody"],gs?["sourceBodyTag"])
            &&Equal(r["referenceBody"],ps?["sourceBodyTag"]);
        var productMass=masses.Where(Product).ToArray();var productCompare=comparisons.Where(Product).ToArray();
        if(productMass.Length!=1)reasons.Add("missing: unique step bounded mass report "+rep);
        if(productCompare.Length!=1)reasons.Add("missing: unique step independent comparison "+rep);
        foreach(var mass in productMass.Where(_=>productCompare.Length>0))
            if(productCompare.Count(c=>Equal(c["referenceBody"],mass["referenceBody"])&&Equal(c["actualBody"],mass["actualBody"])
                &&Equal(c["sourceBinding"]?["gmXtSha256"],mass["sourceBinding"]?["gmXtSha256"])
                &&Equal(c["sourceBinding"]?["referenceXtSha256"],mass["sourceBinding"]?["referenceXtSha256"]))!=1)
                reasons.Add("fail: step mass/compare native pair or XT source mismatch");
        foreach(var side in new[]{"gm","pk"})
        {
            var api=checks.SingleOrDefault(c=>Text(c,"id")==side+"-"+rep+"-api");if(api!=null)Zero(api,side);
        }
        foreach(var side in new[]{(Id:"True",Value:"gm"),(Id:"False",Value:"pk")})
        {
            var body=checks.SingleOrDefault(c=>Text(c,"id")==rep+"-"+side.Id+"-body-check");if(body!=null)Zero(body,side.Value);
        }
    }
    // A report with an explicit step must name a real current operation, even if
    // all expected steps also happen to have another apparently valid report.
    foreach(var report in comparisons.Where(r=>Text(r["sourceBinding"],"kind")=="same-instance-snapshot-pair"))
        if(report["sourceBinding"]?["step"]?.GetValue<int>() is not int ordinal||ordinal<0||ordinal>=repeat)reasons.Add("fail: comparison has invalid step");
    return reasons;

    JsonNode? Read(string? relative)
    {
        if(string.IsNullOrEmpty(relative)){reasons.Add("missing: required evidence link");return null;}
        var path=Path.GetFullPath(Path.Combine(directory,relative));var within=Path.GetRelativePath(directory,path);
        if(Path.IsPathRooted(relative)||within==".."||within.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))
        {reasons.Add("fail: evidence outside current capture");return null;}
        if(!File.Exists(path)){reasons.Add("missing: evidence file "+relative);return null;}
        return JsonNode.Parse(File.ReadAllText(path));
    }
    void Same(JsonNode? a,JsonNode? b,string label)
    {if(a==null||b==null)reasons.Add("missing: "+label);else if(!Equal(a,b))reasons.Add("fail: "+label+" mismatch");}
    void Zero(JsonNode node,string field)
    {if(!Number(node,field,out var value))reasons.Add("missing: API result "+field);else if(value!=0)reasons.Add("fail: API error "+field);}
}
static string? Text(JsonNode? node,string field)=>node?[field]?.GetValue<string>();
static bool Equal(JsonNode? a,JsonNode? b)=>a!=null&&b!=null&&JsonNode.DeepEquals(a,b);
static bool Number(JsonNode node,string field,out double value)
{value=0;return node[field] is JsonValue scalar&&scalar.TryGetValue(out value)&&double.IsFinite(value);}
static bool Digest(string? value)=>value?.Length==64&&value.All(c=>char.IsAsciiHexDigit(c));
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Git(string root,params string[] args)
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
    using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEnd();var stderr=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new IOException(stderr);return stdout.Trim();
}
internal sealed record Bench(string Id,string Script,string[] Args,string? Selector,string Artifacts);
