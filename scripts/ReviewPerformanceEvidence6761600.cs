#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Globalization;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Read only the one fixed current-format evidence package. All path arguments
// are relative to this script; derived output never overwrites original data.
var script=Path.GetDirectoryName(ScriptPath())!;
var project=Path.GetFullPath(Path.Combine(script,".."));
var original=Path.GetFullPath(Path.Combine(script,args.Length>0?args[0]:"../temp_docs/pk-performance-review/mac-original"));
var output=Path.GetFullPath(Path.Combine(script,args.Length>1?args[1]:"../docs/Pk_Performance_6761600_Evidence_Audit.json"));
if(File.Exists(output))throw new IOException("Derived audit output already exists");
var archive=Path.GetFullPath(Path.Combine(script,"../../gm-delivery-board/temp_docs/incoming/mac-performance/mac-modeling-performance-6761600-raw.tar.gz"));
var archiveHash=Hash(archive);Require(archiveHash=="85be8a3e3d349cd2a755ecd7a67722746ec5cc9a391aaf389b79a6d294ce0333","immutable archive hash");
var members=0;long expandedBytes=0;
using(var zipped=File.OpenRead(archive))using(var unzip=new GZipStream(zipped,CompressionMode.Decompress))using(var tar=new TarReader(unzip))
while(tar.GetNextEntry() is { } entry)
{
    Require(entry.EntryType is TarEntryType.Directory or TarEntryType.RegularFile,"regular archive entry");
    Require(!Path.IsPathRooted(entry.Name)&&!entry.Name.Split('/').Contains(".."),"relative archive member");
    members++;expandedBytes+=entry.Length;
    if(entry.EntryType==TarEntryType.RegularFile)
    {
        Require(entry.DataStream!=null,"original member bytes");
        Require(Convert.ToHexStringLower(SHA256.HashData(entry.DataStream!))==Hash(Path.Combine(original,entry.Name)),"extracted bytes match archive");
    }
}
Require(members==556&&expandedBytes==36370821,"archive member count/size");
const string raw="temp_docs/pk-native-work-reduction/controlled-final/report.json";
var report=Read(Path.Combine(original,raw));var samples=report["samples"]!.AsArray();
Require(Hash(Path.Combine(original,raw))=="0b867277df0f35e5a9be369f7cf17fcd36308a2cbe9db5bea12d0dc080ece418","fixed report hash");
Require(S(report,"baselineSha")=="ecfff3aae1dbbe9dd48adbbdf2e8e635da735938"&&S(report,"candidateSha")=="6761600fef20602342cd9dc81bd4f7e4fb85d939","source commits");
Require(S(report,"baselineNativeTree")=="e314cc2307c5e77ea6f2b7685ba17836ae4ef5c8"&&S(report,"candidateNativeTree")=="01e03aa9ed10a8145556872e98fa1677e17a4cb7","source trees");
Require(Hash(Path.Combine(original,"scripts/NativeModelingWorkComparison.cs"))==S(report,"runnerSha256")&&Hash(Path.Combine(project,"scripts/NativeModelingWorkComparison.cs"))==S(report,"runnerSha256"),"runner original/fixed source");
Require(samples.Count==20&&report["qualified"]!.GetValue<bool>(),"report completion");
var cases=new[]{"sphere","planar","curved","solidblend","shortedge"};
var oracles=new[]{"AnalyticBooleanOracle.cs","BooleanOperationOracle.cs","RegularCurvedBooleanOracle.cs","RegularSolidBlendOracle.cs","ShortEdgeBlendOracle.cs"};
var derived=new JsonArray();var allWork=new JsonArray();var meshFiles=new HashSet<string>();var pairs=0;var xtFiles=0;var numericMassChecks=0;var fullChecks=0;var strictChecks=0;
double worstMass=0;var quality=new JsonArray();
for(var index=0;index<samples.Count;index++)
{
    var sample=samples[index]!;var kind=cases[index/4];var source=index%4 is 0 or 3?"baseline":"candidate";
    Require(S(sample,"case")==kind&&S(sample,"source")==source&&S(sample,"commit")==S(report,source+"Sha"),"ABBA source/commit order");
    Require(N(sample,"processExit")==0&&!sample["timedOut"]!.GetValue<bool>()&&sample["qualified"]!.GetValue<bool>()&&sample["reasons"]!.AsArray().Count==0,"original process settlement");
    Require(Hash(Path.Combine(project,"scripts",oracles[index/4]))==S(sample,"oracleSha256"),"pinned oracle bytes");
    var directory=Path.Combine(original,S(sample,"directory"));
    Require(Hash(Path.Combine(directory,"oracle.log"))==S(sample,"logSha256")&&Hash(Path.Combine(directory,"capture.json"))==S(sample,"captureSha256"),"log/capture original hashes");
    var log=File.ReadAllText(Path.Combine(directory,"oracle.log"));
    Require(!log.Contains("FAIL ",StringComparison.Ordinal)&&!log.Contains("WEB_EXPORT_ERROR",StringComparison.Ordinal),"oracle failure text");
    var capturedWork=log.Split('\n').Where(l=>l.StartsWith("WORK ")||l.StartsWith("PROFILE ")).ToArray();
    Require(capturedWork.SequenceEqual(sample["work"]!.AsArray().Select(n=>n!.GetValue<string>())),"raw work lines");
    var works=capturedWork.Where(l=>l.StartsWith("WORK ")).ToArray();var profiles=capturedWork.Where(l=>l.StartsWith("PROFILE operation=")).ToArray();
    Require(works.Length==3&&profiles.Length==3,"one work record per repetition");
    var capture=Read(Path.Combine(directory,"capture.json"));
    Require(S(capture,"pkVersion")=="38.0.150"&&N(capture,"sessionPrecision")==1e-8&&capture["captureErrors"]!.AsArray().Count==0,"PK/precision/capture errors");
    var steps=capture["steps"]!.AsArray();var timings=capture["timings"]!.AsArray();var checks=capture["checks"]!.AsArray();
    Require(steps.Count==3&&timings.Count==6,"three steps / six timings");
    var byId=checks.ToDictionary(n=>S(n!,"id"));Require(checks.All(c=>S(c!,"status")=="pass"),"all captured checks pass");
    var meshStart=meshFiles.Count;var massStart=numericMassChecks;double processWorst=0;
    for(var rep=0;rep<3;rep++)
    {
        var step=steps[rep]!;Require(S(step,"id")==$"operation-{rep+1}"&&S(step,"title")==S(sample,"api"),"step owner and API");
        var blend=index/4>=3;var order=blend?new[]{"pk","gm"}:new[]{"gm","pk"};
        Require(S(sample,"callOrder")== (blend?"PK then GM":"GM then PK"),"declared call order");
        var rawTimes=timings.Where(t=>N(t!,"step")==rep).ToArray();Require(rawTimes.Length==2&&rawTimes.Select(t=>S(t!,"kernel")).SequenceEqual(order),"actual timing ownership/order");
        var pair=sample["pairs"]![rep]!;Require(N(pair,"rep")==rep&&S(pair,"temperature")== (rep==0?"cold":"warm"),"pair temperature");
        foreach(var side in new[]{"gm","pk"})
        {
            var time=rawTimes.Single(t=>S(t!,"kernel")==side)!;
            Require(N(time,"error")==0&&double.IsFinite(N(time,"ms"))&&N(time,"ms")>0,"API return/time");
            // Pinned PK38 enums: blend_fault_no_fault / boolean_result_success.
            Require(N(time,"resultStatus")== (blend?18391:21650),"operation success/fault result");
            Require(N(time,"ms")==N(pair,side+"Ms"),"pair matches raw interval");
            Require(N(byId[$"{side}-{rep}-api"]!,side)==0,"API numeric check");
            var check=byId[$"{rep}-{(side=="gm"?"True":"False")}-body-check"]!;
            Require(N(check,side)==0&&S(check,"note")=="[]","full BODY_check zero faults");fullChecks++;
            if(blend)Require(N(byId[$"{rep}-{(side=="gm"?"True":"False")}-blend-fault"]!,side)==18391,"blend fault enum");
            var state=step[side]!;
            Require(JsonNode.DeepEquals(step["gm"]!["apiParameters"],step["pk"]!["apiParameters"]),"same GM/PK operation parameters");
            Require(state["afterBodies"]!.AsArray().Count==1,"one real output body");
            var paths=new[]{S(state,"before")}.Concat(state["afterBodies"]!.AsArray().Select(p=>p!.GetValue<string>())).Concat(state["tools"]!.AsArray().Select(p=>p!.GetValue<string>()));
            foreach(var path in paths)
            {
                var file=Path.Combine(directory,path);Require(meshFiles.Add(file),"distinct per-repetition mesh");var mesh=Read(file);
                Require(S(mesh["source"]!,"kernel")== (side=="gm"?"GM":"PK")&&S(mesh["source"]!,"commit")==S(sample,"commit"),"mesh kernel/source commit");
                Require(mesh["source"]!["freshXtReceive"]!.GetValue<bool>()== (side=="gm"),"mesh fresh XT provenance");
                // A complete spherical input has a face and no finite edges.
                Require(mesh["faces"]!.AsArray().Count>0&&mesh["edges"] is JsonArray,"actual face/edge mesh "+path);
                if(path==S(state,"after"))
                {
                    Require(mesh["faces"]!.AsArray().Count==N(byId[$"{rep}-count-2"]!,side)&&mesh["edges"]!.AsArray().Count==N(byId[$"{rep}-count-3"]!,side),"mesh and actual result structure counts");
                }
            }
        }
        Require(N(pair,"ratioGmOverPk")==N(pair,"gmMs")/N(pair,"pkMs"),"pair ratio from raw intervals");pairs++;
        Require(N(byId[$"{rep}-body-count"]!,"gm")==1&&N(byId[$"{rep}-body-count"]!,"pk")==1,"numeric body count");
        for(var k=0;k<5;k++){var check=byId[$"{rep}-count-{k}"]!;Require(N(check,"gm")==N(check,"pk")&&N(check,"gm")>=0,"numeric topology counts");}
        Require(byId.ContainsKey($"{rep}-equivalence"),"strict qualification check");
        var evidence=Read(Path.Combine(directory,$"evidence/compare-{rep}.json"));var mask=evidence["mask"]!;
        Require(N(mask,"max_diffs")==1024&&!mask["all_tests"]!.GetValue<bool>()&&!mask["acc_dev_tests"]!.GetValue<bool>(),"effective strict mask limits");
        foreach(var key in new[]{"num_tests","tol_dev_tests","tol_tests","class_tests","sense_tests","missing_tests","non_match_tests"})Require(mask[key]!.GetValue<bool>(),"effective strict mask "+key);
        Require(N(evidence,"globalResult")==21921&&evidence["globalDiffs"]!.AsArray().All(d=>N(d!,"masters")==N(d!,"similars")),"actual strict global result");
        var locals=evidence["localDiffs"]!.AsArray();Require(locals.All(d=>N(d!,"type")==21949&&d!["diagnostic"]!.GetValue<bool>()),"only chart-point local diagnostic");
        Require(N(evidence,"localResult")==21931||N(evidence,"localResult")==21930&&locals.Count>0,"actual local result");strictChecks++;
        for(var k=0;k<15;k++)
        {
            var check=byId[$"{rep}-mass-{k}"]!;var delta=Math.Abs(N(check,"gm")-N(check,"pk"))/(1+Math.Abs(N(check,"pk")));
            Require(double.IsFinite(delta)&&N(check,"threshold")==1e-7&&delta<1e-7&&Math.Abs(delta-N(check,"normalizedDelta"))<1e-20,"actual normalized mass value");
            processWorst=Math.Max(processWorst,delta);numericMassChecks++;
        }
        var work=new JsonObject{["case"]=kind,["source"]=source,["round"]=index%4,["rep"]=rep};
        foreach(var line in new[]{works[rep],profiles[rep]})foreach(Match match in Regex.Matches(line,@"([\w-]+)=([\d.]+)(?: |$)"))work[match.Groups[1].Value]=double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);
        Require(Math.Abs(N(work,"api-ms")-N(pair,"gmMs"))<=.00051,"profile API interval is captured interval");allWork.Add((JsonNode)work);
    }
    foreach(var artifact in sample["xtArtifactsLastRepetition"]!.AsArray())
    {Require(N(artifact!,"repetition")==2&&Hash(Path.Combine(original,S(artifact!,"path")))==S(artifact!,"sha256"),"final-repetition original XT hash");xtFiles++;}
    Require(sample["xtArtifactsLastRepetition"]!.AsArray().Count>0,"final original XT exists");worstMass=Math.Max(worstMass,processWorst);
    quality.Add((JsonNode)new JsonObject{["directory"]=S(sample,"directory"),["originalExit"]=N(sample,"processExit"),["actualMeshes"]=meshFiles.Count-meshStart,["numericMassChecks"]=numericMassChecks-massStart,["worstNormalizedMass"]=processWorst});
}
foreach(var kind in cases)
{
    var row=new JsonObject{["case"]=kind};
    foreach(var source in new[]{"baseline","candidate"})foreach(var temperature in new[]{"cold","warm"})
    {
        var group=samples.Where(s=>S(s!,"case")==kind&&S(s!,"source")==source).SelectMany(s=>s!["pairs"]!.AsArray()).Where(p=>S(p!,"temperature")==temperature).ToArray();
        var gm=group.Select(p=>N(p!,"gmMs")).ToArray();var pk=group.Select(p=>N(p!,"pkMs")).ToArray();
        row[source+"-"+temperature]=new JsonObject{["n"]=group.Length,["gmMs"]=new JsonArray(gm.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),["pkMs"]=new JsonArray(pk.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray()),["gmMedianMs"]=Median(gm),["pkMedianMs"]=Median(pk),["medianPairGmOverPk"]=Median(group.Select(p=>N(p!,"gmMs")/N(p!,"pkMs")))};
    }
    var before=N(row["baseline-warm"]!,"gmMedianMs");var after=N(row["candidate-warm"]!,"gmMedianMs");row["warmSpeedup"]=before/after;row["warmElapsedChangePercent"]=100*(after/before-1);derived.Add((JsonNode)row);
    var workRows=allWork.Where(w=>S(w!,"case")==kind).ToArray();
    foreach(var rep in Enumerable.Range(0,3))foreach(var key in new[]{"jacobians","directions","pairs","ssi-cells","root-cells","roots","budget-failures","winding","bound-requests","bound-evaluations","cache-hits"})Require(workRows.Where(w=>N(w!,"rep")==rep).Select(w=>N(w!,key)).Distinct().Count()==1,"matched-repetition work conservation: "+kind+" "+key);
}
var authorSummary=Read(Path.Combine(original,"docs/native-modeling-work-reduction/operation-summary.json"));
foreach(var operation in authorSummary["operations"]!.AsArray())
{
    var row=derived.Single(r=>S(r!,"case")==S(operation!,"id"))!;
    foreach(var source in new[]{"baseline","candidate"})foreach(var temperature in new[]{"cold","warm"})
    {
        var actual=row[source+"-"+temperature]!;var claimed=operation![source]![temperature]!;
        foreach(var key in new[]{"gmMedianMs","pkMedianMs"})Require(N(actual,key)==N(claimed,key),"author summary reproduced from raw");
        Require(N(actual,"medianPairGmOverPk")==N(claimed,"ratioMedian"),"author paired ratio reproduced");
    }
}
var audit=new JsonObject{["format"]="independent-performance-6761600-audit-v1",["archiveSha256"]=archiveHash,["archiveMembers"]=members,["expandedOriginalBytes"]=expandedBytes,["rawReport"]=raw,["rawReportSha256"]=Hash(Path.Combine(original,raw)),["originalProcesses"]=20,["pairs"]=pairs,["actualMeshes"]=meshFiles.Count,["finalRepXtFiles"]=xtFiles,["fullBodyChecks"]=fullChecks,["strictComparisons"]=strictChecks,["numericMassChecks"]=numericMassChecks,["worstNormalizedMass"]=worstMass,["observations"]=derived,["quality"]=quality,["actualWork"]=allWork,["fullSuiteAccepted"]=false};
Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,audit.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine($"PASS original evidence audit: processes=20 pairs={pairs} meshes={meshFiles.Count} final-rep-XT={xtFiles} full-BODY={fullChecks} strict={strictChecks} numeric-mass={numericMassChecks} worst={worstMass:R}");
foreach(var row in derived)Console.WriteLine($"{S(row!,"case")}: warm {N(row!["baseline-warm"]!,"gmMedianMs"):R} -> {N(row["candidate-warm"]!,"gmMedianMs"):R} ms, speedup={N(row,"warmSpeedup"):R}, GM/PK median={N(row["candidate-warm"]!,"medianPairGmOverPk"):R}");
static JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllText(path))!;
static string S(JsonNode node,string key)=>node[key]!.GetValue<string>();
static double N(JsonNode node,string key){var value=node[key]!.AsValue();return value.TryGetValue<double>(out var number)?number:value.GetValue<int>();}
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static double Median(IEnumerable<double> values){var sorted=values.Order().ToArray();return (sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])*.5;}
static void Require(bool condition,string label){if(!condition)throw new Exception(label);}
static string ScriptPath([CallerFilePath]string path="")=>path;
