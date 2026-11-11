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
// operations, reusing the existing strict oracles as workload drivers. The
// measured interval is exactly the public modeling API call recorded by
// WebProgressCapture (capture.json timings = capture-only; oracle stdout
// TIME/FIX and BENCH api-ms include the opt-in web observer and are never
// used for ratios). Each sample must satisfy the full per-case qualification
// contract (expected API, per-rep unique paired owner, complete strict
// equivalence + double full BODY_check + 5 topology counts + 15 mass checks
// per captured pair, empty captureErrors, finite positive timings); any
// missing requirement marks the sample unqualified and the run exits
// non-zero. --validate-fixtures runs the real qualification/summary/exit
// pipeline over synthesized controlled capture fixtures (no oracle runs).

const string GateVersion="modeling-paired-benchmark-qualification-v4";

var scriptDirectory=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath(Path.Combine(scriptDirectory,".."));
var output=Path.GetFullPath(Option("--output",Path.Combine(root,"temp_docs","modeling-performance")));
if(args.Contains("--validate-fixtures",StringComparer.Ordinal))
{
    var failures=ValidateFixtures(Path.Combine(output,"selftest-fixtures"),Console.Out);
    Console.WriteLine(failures==0?"PASS modeling-paired-benchmark qualification self-test":$"FAIL {failures} fixture verdicts wrong");
    return failures==0?0:3;
}
if(args.Contains("--revalidate",StringComparer.Ordinal)||args.Contains("--revalidate-report",StringComparer.Ordinal))
{
    var reportMode=args.Contains("--revalidate-report",StringComparer.Ordinal);
    Verdict verdict;string scope;string subject;
    if(reportMode)
    {
        // Full re-verification: process state comes from the immutable run
        // report, so this covers the whole sample contract, not only capture.
        var at=Array.IndexOf(args,"--revalidate-report");
        var reportJson=JsonNode.Parse(File.ReadAllText(args[at+1]))!.AsObject();
        var caseId=args[at+2];var label=args[at+3];
        var caseNode=reportJson["cases"]!.AsArray().FirstOrDefault(c=>c!["id"]!.GetValue<string>()==caseId)??throw new ArgumentException("case not in report: "+caseId);
        var sample=caseNode["samples"]!.AsArray().FirstOrDefault(x=>x!["label"]!.GetValue<string>()==label)??throw new ArgumentException("sample not in report: "+label);
        var captureDirectory=Path.GetDirectoryName(Path.Combine(output,sample["capture"]!.GetValue<string>()))!;
        var text=File.Exists(Path.Combine(output,sample["oracleLog"]!.GetValue<string>()))?File.ReadAllText(Path.Combine(output,sample["oracleLog"]!.GetValue<string>())):"";
        var bench=ResolveCases(caseId).Single();
        verdict=Qualify(bench,File.Exists(Path.Combine(captureDirectory,"capture.json"))?JsonNode.Parse(File.ReadAllText(Path.Combine(captureDirectory,"capture.json")))!.AsObject():null,
            text,sample["processExit"]!.GetValue<int>(),sample["timedOut"]!.GetValue<bool>(),sample["expectedRepetitions"]?.GetValue<int>()??sample["repetitions"]!.GetValue<int>(),captureDirectory);
        scope="full-run (process exit/timeout verified from the immutable report)";
        subject=caseId+" "+label;
    }
    else
    {
        var at=Array.IndexOf(args,"--revalidate");
        var captureDirectory=args[at+1];var caseToken=args[at+2];var expectedReps=int.Parse(args[at+3],CultureInfo.InvariantCulture);
        var bench=ResolveCases(caseToken).Single();
        var capturePath=Path.Combine(captureDirectory,"capture.json");
        var capture=File.Exists(capturePath)?JsonNode.Parse(File.ReadAllText(capturePath))!.AsObject():null;
        var text=File.Exists(Path.Combine(captureDirectory,"oracle.log"))?File.ReadAllText(Path.Combine(captureDirectory,"oracle.log")):"";
        verdict=Qualify(bench,capture,text,0,false,expectedReps,captureDirectory);
        scope="capture-contract only (original process exit/timeout NOT verifiable from capture.json alone; use --revalidate-report for full-run verification)";
        subject=caseToken+" "+captureDirectory;
    }
    Console.WriteLine($"revalidate {subject}: qualified={verdict.Qualified} scope={scope}");
    foreach(var reason in verdict.Reasons)Console.WriteLine("  reason: "+reason);
    return verdict.Qualified?0:reportMode?6:5;
}
var mode=Option("--mode","measure");
if(mode is not ("measure" or "profile"))throw new ArgumentException("--mode must be measure or profile");
var coldCount=Number("--cold",3);
var warmCount=Number("--warm",5);
if(coldCount<1||warmCount<1||warmCount>63)throw new ArgumentException("--cold >= 1 and 1 <= --warm <= 63 required");
var resolved=ResolveCases(Option("--cases","sphereBoolean,planarBoolean,regularcurved,solidblend,shortedge"));

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
    ["schemaVersion"]=2,
    ["kind"]="modeling-paired-benchmark",
    ["gateVersion"]=GateVersion,
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
            ["timingScope"]="capture.json timings only (Stopwatch interval around the public modeling API call); stdout TIME/FIX lines and BENCH api-ms additionally include the opt-in web observer and mark/selection work and are never used for ratios",
            ["ratio"]="GM elapsed ms / PK elapsed ms per repetition; ratio reported only when every counted sample passed the full per-case qualification contract",
            ["callOrderLimitation"]="producer order is fixed per case (recorded in operation.callOrder) because the oracles are shared and not modified by this tool; tiny PK denominators are countered with multiple samples, not order rotation",
            ["sampleOrder"]="cheap cases first, heavy cases last, samples serial on one host",
            ["acceptance"]="each operation is independently accepted against real PK with the same parameters (fresh XT receive + full BODY_check + strict PK_DEBUG_BODY_compare + 5 topology counts + 15 mass properties inside each oracle); no combination workflow is mixed into these per-operation ratios"}}};
var casesJson=new JsonArray();report["cases"]=casesJson;
report["resolvedCases"]=new JsonArray(resolved.Select(c=>(JsonNode?)JsonValue.Create(c.Id)).ToArray());
report["resolvedCallOrder"]=new JsonArray(resolved.Select(c=>(JsonNode?)JsonValue.Create(c.Id+" -> "+c.CallOrder)).ToArray());
var requestedCount=resolved.Count;var attemptedCount=0;var qualifiedCount=0;var unqualifiedCount=0;var notRunCount=0;

foreach(var bench in resolved)
{
    Console.WriteLine($"CASE {bench.Id} mode={mode} cold={coldCount} warm={warmCount}");
    var caseJson=new JsonObject{
        ["id"]=bench.Id,
        ["operation"]=new JsonObject{
            ["api"]=bench.OperationApi,
            ["apiKind"]=bench.ApiKind,
            ["kind"]="independent-operation",
            ["combination"]=false,
            ["inputProvenance"]=bench.InputProvenance,
            ["callOrder"]=bench.CallOrder},
        ["qualificationContract"]=ContractJson(bench),
        ["oracle"]=new JsonObject{["script"]="scripts/"+bench.Script,["arguments"]=new JsonArray(bench.Args.Select(a=>(JsonNode?)JsonValue.Create(a)).ToArray()),["selector"]=bench.Selector},
        ["samples"]=new JsonArray()};
    casesJson.Add(caseJson);
    var samples=(JsonArray)caseJson["samples"]!;
    if(mode=="measure")
    {
        for(var cold=0;cold<coldCount;cold++)samples.Add(RunSample(bench,$"cold-{cold}",null,runDirectory,1));
        samples.Add(RunSample(bench,"warm",warmCount+1,runDirectory,warmCount+1));
    }
    else samples.Add(RunSample(bench,"profile",2,runDirectory,2,profile:true));
    if(mode=="measure")
    {
        caseJson["cold"]=Summarize(samples,"cold");
        caseJson["warm"]=Summarize(samples,"warm");
    }
    Save(report,runDirectory);
}
report["finishedAt"]=DateTimeOffset.UtcNow.ToString("O");
host["loadAverageAtEnd"]=LoadAverage();
foreach(var caseNode in casesJson)foreach(var sample in caseNode!["samples"]!.AsArray())
{
    attemptedCount++;
    if(sample!["qualified"]!.GetValue<bool>())qualifiedCount++;else{unqualifiedCount++;Console.WriteLine("UNQUALIFIED "+caseNode!["id"]+" "+sample["label"]+": "+string.Join("; ",((JsonArray)sample["qualificationReasons"]!).Select(r=>r!.GetValue<string>())));}
}
notRunCount=0;
var overallQualified=RunOverallQualified(requestedCount*(mode=="measure"?coldCount+1:1),attemptedCount,unqualifiedCount);
report["runSummary"]=new JsonObject{
    ["requestedCases"]=requestedCount,
    ["attemptedSamples"]=attemptedCount,
    ["qualifiedSamples"]=qualifiedCount,
    ["unqualifiedSamples"]=unqualifiedCount,
    ["notRunSamples"]=notRunCount,
    ["overallQualified"]=overallQualified,
    ["gateVersion"]=GateVersion,
    ["note"]="any unqualified or missing sample makes overallQualified false and the process exit non-zero; per-sample data stays verbatim, subset statistics are diagnostic only"};
if(mode=="measure")
{
    report["immutableReport"]="runs/"+runId+"/benchmark/report.json";
    var summaryPath=Path.Combine(output,"paired-benchmark-latest.json");
    File.WriteAllText(summaryPath+".tmp",report.ToJsonString());File.Move(summaryPath+".tmp",summaryPath,true);
}
else
{
    report["immutableReport"]="runs/"+runId+"/benchmark/report.json";
    var summaryPath=Path.Combine(output,"paired-benchmark-profile-latest.json");
    File.WriteAllText(summaryPath+".tmp",report.ToJsonString());File.Move(summaryPath+".tmp",summaryPath,true);
}
Save(report,runDirectory);
Console.WriteLine("REPORT "+Path.Combine("temp_docs","modeling-performance","runs",runId,"benchmark","report.json"));
Console.WriteLine("RUN_SUMMARY requested="+requestedCount+" attempted="+attemptedCount+" qualified="+qualifiedCount+" unqualified="+unqualifiedCount+" overallQualified="+overallQualified);
return overallQualified?0:4;

JsonObject RunSample(BenchCase bench,string label,int? repetitions,string runRoot,int expectedReps,bool profile=false)
{
    var caseDirectory=Path.Combine(runRoot,"cases",bench.Id,label);
    Directory.CreateDirectory(caseDirectory);
    var arguments=bench.Args.ToList();
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
    return FinishSample(bench,label,caseDirectory,process.ExitCode,timedOut,wallMs,text,expectedReps,loadBefore,loadAfter,
        Path.Combine("runs",runId,"benchmark","cases",bench.Id,label));
}

static JsonObject FinishSample(BenchCase bench,string label,string caseDirectory,int exitCode,bool timedOut,double wallMs,string text,int expectedReps,string loadBefore,string loadAfter,string relativeDirectory)
{
    var capturePath=Path.Combine(caseDirectory,"capture.json");
    var capture=File.Exists(capturePath)?JsonNode.Parse(File.ReadAllText(capturePath))!.AsObject():null;
    var verdict=Qualify(bench,capture,text,exitCode,timedOut,expectedReps,caseDirectory);
    var timings=capture?["timings"]?.AsArray();
    // Raw arrays keep capture row order verbatim. Authoritative GM/PK pairing
    // joins by rep owner (timing.step), never by array position: row order is
    // not part of the capture contract and permutations must not change any
    // ratio or the cold/warm attribution of rep 0.
    var gmRows=new List<(int Rep,double Ms)>();var pkRows=new List<(int Rep,double Ms)>();
    if(timings!=null)foreach(var row in timings.OfType<JsonObject>())
    {
        var kernel=row["kernel"]?.GetValue<string>();
        var entry=(Owner:row["step"] is JsonValue v&&v.TryGetValue<int>(out var owner)?owner:-1,Ms:SafeMs(row));
        if(kernel=="gm")gmRows.Add(entry);else if(kernel=="pk")pkRows.Add(entry);
    }
    var gm=gmRows.Select(r=>r.Ms).ToArray();var pk=pkRows.Select(r=>r.Ms).ToArray();
    var pairs=new JsonArray();
    foreach(var group in gmRows.GroupBy(r=>r.Rep).OrderBy(g=>g.Key))
    {
        var partners=pkRows.Where(r=>r.Rep==group.Key).ToList();
        if(group.Count()==1&&partners.Count==1)
        {
            // Serialization protection: a missing/non-finite ms keeps the
            // sample unqualified (Qualify already rejects it with a reason);
            // the pair persists null instead of NaN so the failure evidence
            // still writes out and any artifact write failure fails the run
            // closed instead of losing the record.
            var gmMs=group.Single().Ms;var pkMs=partners[0].Ms;
            if(!double.IsFinite(gmMs)||!double.IsFinite(pkMs))
                verdict.Reasons.Add($"rep {group.Key} pair holds non-finite ms (gm={gmMs}, pk={pkMs}); stored as null");
            pairs.Add(new JsonObject{["rep"]=group.Key,["gmMs"]=double.IsFinite(gmMs)?JsonValue.Create(gmMs):null,["pkMs"]=double.IsFinite(pkMs)?JsonValue.Create(pkMs):null});
        }
    }
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
        ["processExit"]=exitCode,
        ["timedOut"]=timedOut,
        ["processWallMs"]=wallMs,
        ["qualified"]=verdict.Qualified,
        ["qualificationReasons"]=new JsonArray(verdict.Reasons.Select(r=>(JsonNode?)JsonValue.Create(r)).ToArray()),
        ["gmMs"]=new JsonArray(gm.Select(v=>(JsonNode?)(double.IsFinite(v)?JsonValue.Create(v):null)).ToArray()),
        ["pkMs"]=new JsonArray(pk.Select(v=>(JsonNode?)(double.IsFinite(v)?JsonValue.Create(v):null)).ToArray()),
        ["pairsByRep"]=pairs,
        ["repetitions"]=pairs.Count,
        ["expectedRepetitions"]=expectedReps,
        ["benchPhases"]=benchLines,
        ["profiles"]=profileLines,
        ["loadAverageBefore"]=loadBefore,
        ["loadAverageAfter"]=loadAfter,
        ["oracleLog"]=Path.Combine(relativeDirectory,"oracle.log"),
        ["oracleLogSha256"]=Hash(Encoding.UTF8.GetBytes(text)),
        ["capture"]=Path.Combine(relativeDirectory,"capture.json"),
        ["pkVersion"]=capture?["pkVersion"]?.DeepClone(),
        ["sessionPrecision"]=capture?["sessionPrecision"]?.DeepClone()};
    Console.WriteLine($"  {label}: exit={exitCode} qualified={verdict.Qualified} gm=[{string.Join(",",gm.Select(v=>v.ToString("F1",CultureInfo.InvariantCulture)))}] pk=[{string.Join(",",pk.Select(v=>v.ToString("F3",CultureInfo.InvariantCulture)))}] wall={wallMs:F0}ms load={loadBefore}");
    return sample;
}


static Verdict Qualify(BenchCase bench,JsonObject? capture,string text,int exitCode,bool timedOut,int expectedReps,string caseDirectory)
{
    var reasons=new List<string>();
    if(timedOut)reasons.Add("timeout");
    if(exitCode!=0)reasons.Add("exit code "+exitCode);
    if(text.Contains("FAIL ",StringComparison.Ordinal))reasons.Add("oracle reported FAIL");
    if(capture==null){reasons.Add("missing capture.json");return new(false,reasons);}
    var captureErrors=capture["captureErrors"]?.AsArray();
    if(captureErrors!=null&&captureErrors.Count>0)reasons.Add("captureErrors non-empty");
    if(File.Exists(Path.Combine(caseDirectory,"export-errors.json"))||File.Exists(Path.Combine(caseDirectory,"capture-errors.json")))reasons.Add("observer export errors");
    if(text.Contains(": error CS",StringComparison.Ordinal)||text.Contains("The build failed.",StringComparison.Ordinal))reasons.Add("build failure");
    // Steps closure: the step table is the single owner registry. It must be
    // present, non-empty, uniquely and legally parseable ("operation-N", N>=1),
    // and every step title must be exactly the declared API of this case.
    // Broken steps never fall back to owner 0 or array-length guesses.
    var stepOwners=new Dictionary<int,string>();
    var steps=capture["steps"]?.AsArray();
    if(steps==null||steps.Count==0)reasons.Add("steps missing or empty");
    else foreach(var step in steps.OfType<JsonObject>())
    {
        var id=step["id"]?.GetValue<string>();
        if(id==null||!id.StartsWith("operation-",StringComparison.Ordinal)||!int.TryParse(id["operation-".Length..],NumberStyles.None,CultureInfo.InvariantCulture,out var number)||number<1)
        {reasons.Add("unparseable step id '"+(id??"<null>")+"'");continue;}
        if(!stepOwners.TryAdd(number-1,id))reasons.Add("duplicate step id "+id);
        var title=step["title"]?.GetValue<string>();
        if(title!=bench.OperationApi)reasons.Add($"step {id} title '{title}' != expected API '{bench.OperationApi}'");
    }
    // Timings closure: every row is validated; unknown kernels, orphan or
    // unparseable owners, api errors and non-finite/non-positive times are
    // never filtered out before validation.
    var timings=capture["timings"]?.AsArray();
    if(timings==null||timings.Count==0)reasons.Add("no captured timings");
    var gm=new List<int>();var pk=new List<int>();
    if(timings!=null)for(var row=0;row<timings.Count;row++)
    {
        var entry=timings[row] as JsonObject;
        if(entry==null){reasons.Add($"timing row {row} is not an object");continue;}
        var kernel=entry["kernel"]?.GetValue<string>();
        if(kernel is not ("gm" or "pk")){reasons.Add($"timing row {row} unknown kernel '{kernel}'");continue;}
        if(entry["step"] is not JsonValue valueNode||!valueNode.TryGetValue<int>(out var owner))
        {reasons.Add($"timing row {row} unparseable step owner");continue;}
        if(!stepOwners.ContainsKey(owner))reasons.Add($"timing row {row} owner step {owner} has no parseable step entry");
        var ms=SafeMs(entry);
        if(!double.IsFinite(ms)||ms<=0)reasons.Add($"{kernel} timing row {row} not finite positive: {ms}");
        if(entry["error"] is not JsonValue errorNode||!errorNode.TryGetValue<int>(out var error)||error!=0)reasons.Add($"{kernel} timing row {row} api error");
        if(entry["resultStatus"] is not JsonValue statusNode||!statusNode.TryGetValue<int>(out _))reasons.Add($"{kernel} timing row {row} missing resultStatus");
        (kernel=="gm"?gm:pk).Add(owner);
    }
    // Per-rep unique paired owner: repetitions 0..expectedReps-1 each own one
    // gm and one pk row and nothing else may exist.
    for(var rep=0;rep<expectedReps;rep++)
    {
        if(gm.Count(o=>o==rep)!=1)reasons.Add($"rep {rep} has {gm.Count(o=>o==rep)} gm owners, expected exactly 1");
        if(pk.Count(o=>o==rep)!=1)reasons.Add($"rep {rep} has {pk.Count(o=>o==rep)} pk owners, expected exactly 1");
    }
    foreach(var owner in gm.Concat(pk).Distinct())if(owner<0||owner>=expectedReps)reasons.Add($"timing owner step {owner} outside 0..{expectedReps-1}");
    if(timings!=null&&timings.Count!=2*expectedReps)reasons.Add($"timing row count {timings.Count} != 2*{expectedReps}");
    // Checks closure: every required primary must be present with status
    // exactly "pass"; diagnostic never satisfies a required primary. Every
    // remaining check row must pass or be an explicitly whitelisted
    // diagnostic; unknown statuses are rejected.
    var seenChecks=new HashSet<string>();
    var checks=capture["checks"]?.AsArray();
    var required=new HashSet<string>();
    for(var rep=0;rep<expectedReps;rep++)foreach(var id in RequiredCheckIds(rep,bench.ApiKind))required.Add(id);
    if(checks==null||checks.Count==0)reasons.Add("no checks recorded");
    else foreach(var check in checks.OfType<JsonObject>())
    {
        var id=check["id"]?.GetValue<string>();
        if(id==null){reasons.Add("check row without id");continue;}
        if(!seenChecks.Add(id))reasons.Add("duplicate check id "+id);
        var status=check["status"]?.GetValue<string>();
        if(required.Contains(id))
        {
            if(status!="pass")reasons.Add($"required primary {id} status {status} (diagnostic cannot satisfy a required primary)");
            continue;
        }
        if(status=="pass")continue;
        if(status=="diagnostic"&&bench.DiagnosticCheckIds.Contains(id,StringComparer.Ordinal))continue;
        reasons.Add($"check {id} status {status} is neither pass nor an allowed diagnostic");
    }
    foreach(var id in required)if(!seenChecks.Contains(id))reasons.Add("missing required check "+id);
    return new(reasons.Count==0,reasons);
}

static string[] RequiredCheckIds(int step,string apiKind)
{
    var prefix=step.ToString(CultureInfo.InvariantCulture);
    var required=new List<string>
    {
        $"{prefix}-body-count",$"{prefix}-equivalence",$"gm-{prefix}-api",$"pk-{prefix}-api",
        $"{prefix}-True-body-check",$"{prefix}-False-body-check"
    };
    for(var kind=0;kind<5;kind++)required.Add($"{prefix}-count-{kind.ToString(CultureInfo.InvariantCulture)}");
    for(var mass=0;mass<15;mass++)required.Add($"{prefix}-mass-{mass.ToString(CultureInfo.InvariantCulture)}");
    if(apiKind=="blend"){required.Add($"{prefix}-True-blend-fault");required.Add($"{prefix}-False-blend-fault");}
    return required.ToArray();
}

static JsonObject ContractJson(BenchCase bench)=>new()
{
    ["expectedApi"]=bench.OperationApi,
    ["apiKind"]=bench.ApiKind,
    ["expectedRepetitionsPerSample"]="cold=1, warm=N+1 (rep 0 is that process's cold sample)",
    ["requiredChecksPerCapturedPair"]=new JsonArray(RequiredCheckIds(0,bench.ApiKind).Select(id=>(JsonNode?)JsonValue.Create(id)).Append((JsonNode?)JsonValue.Create("(step 0 template; step ordinal is the captured pair index)")).ToArray()),
    ["ownerRule"]="each captured step must have exactly one gm and one pk timing (unique paired owner) and its title must equal expectedApi",
    ["negativeControls"]="timeout/exit!=0/FAIL line/build error/missing capture/captureErrors/export errors/non-finite or non-positive ms/api error/repetition mismatch/duplicated owner/missing or failing checks/missing side",
    ["gateVersion"]=GateVersion
};

List<BenchCase> ResolveCases(string csv)
{
    var tokens=csv.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
    var result=new List<BenchCase>();
    foreach(var token in tokens)
    {
        var matches=BenchCases().Where(c=>c.Id==token||c.Aliases.Contains(token,StringComparer.Ordinal)).ToList();
        if(matches.Count==0)throw new ArgumentException($"Unknown case '{token}'. Valid: {string.Join(", ",BenchCases().Select(c=>c.Id))} with aliases {string.Join(", ",BenchCases().SelectMany(c=>c.Aliases))}");
        if(matches.Count>1)throw new ArgumentException($"Ambiguous case '{token}' matches {string.Join(", ",matches.Select(m=>m.Id))}");
        if(result.Any(r=>r.Id==matches[0].Id))throw new ArgumentException($"Duplicate case '{token}'");
        result.Add(matches[0]);
    }
    return result;
}

static JsonObject Summarize(JsonArray samples,string kind)
{
    var cold=new List<double>();var coldPk=new List<double>();var warm=new List<double>();var warmPk=new List<double>();var unqualified=new JsonArray();
    foreach(var row in samples.OfType<JsonObject>())
    {
        var qualified=row["qualified"]!.GetValue<bool>();
        if(!qualified)
        {
            var raw=((JsonArray)row["gmMs"]!).Select(SafeNodeMs).Zip(((JsonArray)row["pkMs"]!).Select(SafeNodeMs)).ToArray();
            for(var i=0;i<raw.Length;i++)
                unqualified.Add((JsonNode?)JsonValue.Create($"{row["label"]}[row {i}] gm={raw[i].First.ToString("F3",CultureInfo.InvariantCulture)} pk={raw[i].Second.ToString("F3",CultureInfo.InvariantCulture)} reasons={string.Join("+",((JsonArray)row["qualificationReasons"]!).Select(r=>r!.GetValue<string>()))}"));
            continue;
        }
        // Authoritative pairs join by rep owner; cold/warm attribution comes
        // from the stable rep identity (rep 0 of a warm process is that
        // process's cold sample), never from array position.
        if(row["pairsByRep"] is not JsonArray pairs){unqualified.Add((JsonNode?)JsonValue.Create($"{row["label"]}: qualified sample without pairsByRep"));continue;}
        foreach(var pair in pairs.OfType<JsonObject>())
        {
            var rep=pair["rep"]!.GetValue<int>();
            var g=SafeNodeMs(pair["gmMs"]);var p=SafeNodeMs(pair["pkMs"]);
            var isWarm=row["kind"]!.GetValue<string>()=="warm"&&rep>0;
            if(isWarm){warm.Add(g);warmPk.Add(p);}
            else{cold.Add(g);coldPk.Add(p);}
        }
    }
    var gm=kind=="warm"?warm:cold;var pk=kind=="warm"?warmPk:coldPk;
    var ratios=new List<double>();for(var i=0;i<gm.Count;i++)if(pk[i]>0&&double.IsFinite(gm[i])&&double.IsFinite(pk[i]))ratios.Add(gm[i]/pk[i]);
    var result=new JsonObject{
        ["scope"]="capture-only operation elapsed, same input parameters per GM/PK pair",
        ["countedRepetitions"]=gm.Count,
        ["qualified"]=gm.Count>0,
        ["diagnosticOnly"]=unqualified.Count>0,
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

static int ValidateFixtures(string directory,TextWriter log)
{
    var bench=BenchCases()[0];var blend=BenchCases().First(b=>b.ApiKind=="blend");var failures=0;
    Directory.CreateDirectory(directory);
    JsonObject RunFixture(string name,BenchCase contract,int reps,Action<JsonObject> mutate,out JsonObject sample)
    {
        var caseDirectory=Path.Combine(directory,name);Directory.CreateDirectory(caseDirectory);
        var capture=ValidCapture(contract,reps);mutate(capture);
        File.WriteAllText(Path.Combine(caseDirectory,"capture.json"),capture.ToJsonString());
        File.WriteAllText(Path.Combine(caseDirectory,"oracle.log"),"PASS synthetic");
        sample=FinishSample(contract,name,caseDirectory,0,false,1000,"PASS synthetic",reps,"synthetic","synthetic",name);
        return sample;
    }
    IEnumerable<string> Reasons(JsonObject sample)=>((JsonArray)sample["qualificationReasons"]!).Select(r=>r!.GetValue<string>());
    void Check(string name,bool expectedQualified,Action<JsonObject> mutate)
    {
        RunFixture(name,bench,2,mutate,out var sample);
        var qualified=sample["qualified"]!.GetValue<bool>();
        var ok=qualified==expectedQualified;
        if(!ok)failures++;
        log.WriteLine($"  fixture {name}: qualified={qualified} expected={expectedQualified} {(ok?"ok":"WRONG")} {(qualified?"":string.Join("; ",Reasons(sample)))}");
    }
    void BlendCheck(string name,bool expectedQualified,Action<JsonObject> mutate)
    {
        RunFixture(name,blend,1,mutate,out var sample);
        var qualified=sample["qualified"]!.GetValue<bool>();
        var ok=qualified==expectedQualified;
        if(!ok)failures++;
        log.WriteLine($"  fixture {name}: qualified={qualified} expected={expectedQualified} {(ok?"ok":"WRONG")} {(qualified?"":string.Join("; ",Reasons(sample)))}");
    }
    // GPT v2 counterexamples: expectations are FALSE and must never soften.
    Check("valid",true,_=>{});
    Check("capture-errors",false,c=>c["captureErrors"]=new JsonArray((JsonNode?)JsonValue.Create("boom")));
    Check("nan-time",false,c=>c["timings"]![0]!.AsObject().Remove("ms"));
    // Serialization protection: a non-finite pair value persists as null with
    // a reason so the failure evidence writes out; the JSON write must not throw.
    {
        RunFixture("nan-time-json",bench,2,c=>c["timings"]![0]!.AsObject().Remove("ms"),out var nanSample);
        try
        {
            var written=nanSample.ToJsonString();
            var ok=!nanSample["qualified"]!.GetValue<bool>()&&written.Contains("\"gmMs\":null",StringComparison.Ordinal)
                &&((JsonArray)nanSample["qualificationReasons"]!).Any(r=>r!.GetValue<string>().Contains("non-finite ms",StringComparison.Ordinal));
            if(!ok)failures++;
            log.WriteLine($"  fixture nan-time-json: {(ok?"ok":"WRONG (expected unqualified sample with null pair value and non-finite reason)")}");
        }
        catch(Exception e){failures++;log.WriteLine($"  fixture nan-time-json: WRONG (JSON write threw {e.GetType().Name})");}
    }
    Check("zero-time",false,c=>c["timings"]![1]!["ms"]=JsonValue.Create(0.0));
    Check("negative-time",false,c=>c["timings"]![1]!["ms"]=JsonValue.Create(-1.0));
    Check("api-error",false,c=>c["timings"]![0]!["error"]=JsonValue.Create(5));
    Check("missing-rep",false,c=>c["timings"]!.AsArray().RemoveAt(0));
    Check("duplicated-owner",false,c=>c["timings"]!.AsArray().Add(c["timings"]![0]!.DeepClone()));
    Check("missing-side",false,c=>c["timings"]!.AsArray().RemoveAt(1));
    Check("wrong-api-title",false,c=>c["steps"]![0]!["title"]=JsonValue.Create("PK_BODY_fix_blends"));
    Check("missing-mass-check",false,c=>c["checks"]!.AsArray().RemoveAt(c["checks"]!.AsArray().Count-1));
    Check("failing-check",false,c=>c["checks"]![3]!["status"]=JsonValue.Create("fail"));
    Check("missing-body-check",false,c=>{var checks=c["checks"]!.AsArray();for(var i=checks.Count-1;i>=0;i--)if(checks[i]!["id"]!.GetValue<string>()=="0-False-body-check")checks.RemoveAt(i);});
    Check("gpt-deleted-steps",false,c=>c.Remove("steps"));
    Check("gpt-unparseable-step-id",false,c=>c["steps"]![0]!["id"]=JsonValue.Create("operation-abc"));
    Check("gpt-required-diagnostic",false,c=>{foreach(var check in c["checks"]!.AsArray())check!["status"]=JsonValue.Create("diagnostic");});
    Check("gpt-extra-bad-row",false,c=>c["timings"]!.AsArray().Add(new JsonObject{["step"]=99,["kernel"]="bogus",["ms"]=-5.0,["error"]=901,["resultStatus"]=901}));
    Check("gpt-duplicate-check-id",false,c=>c["checks"]!.AsArray().Add(c["checks"]![0]!.DeepClone()));
    Check("gpt-orphan-owner",false,c=>c["timings"]![2]!["step"]=JsonValue.Create(7));
    BlendCheck("blend-valid",true,_=>{});
    BlendCheck("blend-missing-fault-check",false,c=>{var checks=c["checks"]!.AsArray();for(var i=checks.Count-1;i>=0;i--)if(checks[i]!["id"]!.GetValue<string>()=="0-True-blend-fault")checks.RemoveAt(i);});
    // GPT v3 counterexample: the capture is LEGAL (owner-joined pairs are
    // rep0=(10,1) ratio 10 and rep1=(100,10) ratio 10) but row order is
    // permuted. Verdict must stay qualified and every ratio/cold-warm
    // attribution must come from rep identity, never array position.
    void AssertOrderFixture(string name,JsonObject sample)
    {
        var qualified=sample["qualified"]!.GetValue<bool>();
        var warm=Summarize(new JsonArray(sample),"warm");
        var cold=Summarize(new JsonArray(sample.DeepClone()),"cold");
        var warmRatios=((JsonArray)warm["sampleRatios"]!).Select(SafeNodeMs).ToArray();
        var coldRatios=((JsonArray)cold["sampleRatios"]!).Select(SafeNodeMs).ToArray();
        var ok=qualified&&warmRatios.Length==1&&warmRatios[0]==10.0&&coldRatios.Length==1&&coldRatios[0]==10.0
            &&((JsonArray)warm["gmMs"]!)[0]!.GetValue<double>()==100.0&&((JsonArray)warm["pkMs"]!)[0]!.GetValue<double>()==10.0
            &&((JsonArray)cold["gmMs"]!)[0]!.GetValue<double>()==10.0&&((JsonArray)cold["pkMs"]!)[0]!.GetValue<double>()==1.0;
        if(!ok)failures++;
        log.WriteLine($"  fixture {name}: qualified={qualified} warmRatio={(warmRatios.Length==1?warmRatios[0].ToString(CultureInfo.InvariantCulture):"n/a")} coldRatio={(coldRatios.Length==1?coldRatios[0].ToString(CultureInfo.InvariantCulture):"n/a")} {(ok?"ok":"WRONG (rep1 must pair GM100 with PK10 => ratio 10; rep0 GM10/PK1 => cold ratio 10; identity from rep owner)")}");
    }
    void OrderFixture(string name,JsonObject capture)
    {
        var caseDirectory=Path.Combine(directory,name);Directory.CreateDirectory(caseDirectory);
        File.WriteAllText(Path.Combine(caseDirectory,"capture.json"),capture.ToJsonString());
        File.WriteAllText(Path.Combine(caseDirectory,"oracle.log"),"PASS synthetic");
        AssertOrderFixture(name,FinishSample(bench,"warm",caseDirectory,0,false,1000,"PASS synthetic",2,"synthetic","synthetic",name));
    }
    // The persisted original counterexample byte-exact (review branch commit
    // 588af88, docs/reviews/advanced-modeling/qualifier-v3-a8ca-evidence/).
    var originalPath=Path.Combine(Path.GetDirectoryName(ScriptPath())!,"..","tests","KernelTests","Fixtures","qualifier","reordered-timings.capture.json");
    if(File.Exists(originalPath))OrderFixture("gpt-order-fixture-original",JsonNode.Parse(File.ReadAllText(originalPath))!.AsObject());
    else{failures++;log.WriteLine("  fixture gpt-order-fixture-original: WRONG (persisted original capture missing)");}
    // Reviewer's exact insertion code path (same bytes), plus permutation
    // invariance over any step/timing row order (gm-first, pk-first, reversed).
    JsonObject InsertionCapture(int[] order)
    {
        var capture=ValidCapture(bench,2);
        var rows=capture["timings"]!.AsArray();
        rows[0]!["ms"]=10.0;rows[1]!["ms"]=1.0;rows[2]!["ms"]=100.0;rows[3]!["ms"]=10.0;
        var permuted=new JsonArray();foreach(var index in order)permuted.Add(rows[index]!.DeepClone());
        capture["timings"]=permuted;
        return capture;
    }
    OrderFixture("gpt-order-fixture-insertion",InsertionCapture([0,3,2,1]));
    string SummaryOf(JsonObject capture,string name)
    {
        var caseDirectory=Path.Combine(directory,name);Directory.CreateDirectory(caseDirectory);
        File.WriteAllText(Path.Combine(caseDirectory,"capture.json"),capture.ToJsonString());
        File.WriteAllText(Path.Combine(caseDirectory,"oracle.log"),"PASS synthetic");
        var sample=FinishSample(bench,"warm",caseDirectory,0,false,1000,"PASS synthetic",2,"synthetic","synthetic",name);
        AssertOrderFixture(name,(JsonObject)sample.DeepClone());
        return Summarize(new JsonArray(sample),"warm").ToJsonString()+"|"+Summarize(new JsonArray(sample.DeepClone()),"cold").ToJsonString();
    }
    var reference=SummaryOf(InsertionCapture([0,1,2,3]),"order-identity");
    var invariant=reference==SummaryOf(InsertionCapture([0,2,1,3]),"order-gm-first")
        &&reference==SummaryOf(InsertionCapture([1,3,0,2]),"order-pk-first")
        &&reference==SummaryOf(InsertionCapture([3,2,1,0]),"order-reversed");
    if(!invariant)failures++;
    log.WriteLine($"  fixture order-permutation-invariance: {(invariant?"ok":"WRONG (summary must be invariant under every step/timing row order)")} ");
    // Summary/exit semantics: qualified subsets may be reported but the
    // unqualified sample stays verbatim and the run-level gate stays closed.
    var summaryProbe=new JsonArray();
    summaryProbe.Add(new JsonObject{["label"]="warm",["kind"]="warm",["qualified"]=true,["qualificationReasons"]=new JsonArray(),["gmMs"]=new JsonArray(1.0,1.0),["pkMs"]=new JsonArray(1.0,1.0),
        ["pairsByRep"]=new JsonArray(new JsonObject{["rep"]=0,["gmMs"]=1.0,["pkMs"]=1.0},new JsonObject{["rep"]=1,["gmMs"]=1.0,["pkMs"]=1.0})});
    summaryProbe.Add(new JsonObject{["label"]="warm",["kind"]="warm",["qualified"]=false,["qualificationReasons"]=new JsonArray((JsonNode?)JsonValue.Create("missing check")),["gmMs"]=new JsonArray(1.0,1.0),["pkMs"]=new JsonArray(1.0,1.0)});
    var summary=Summarize(summaryProbe,"warm");
    var subsetClean=summary["countedRepetitions"]!.GetValue<int>()==1&&summary["diagnosticOnly"]!.GetValue<bool>()&&summary["unqualifiedSamples"]!.AsArray().Count==2;
    if(!subsetClean){failures++;log.WriteLine("  fixture summary-subset: WRONG (expected 1 counted warm rep from the qualified sample, unqualified reps kept verbatim, diagnosticOnly=true)");}
    else log.WriteLine("  fixture summary-subset: ok (qualified subset retained, unqualified sample kept verbatim, diagnosticOnly=true)");
    if(RunOverallQualified(4,4,1)||!RunOverallQualified(4,4,0)||RunOverallQualified(4,3,0)||RunOverallQualified(4,4,2))
    {failures++;log.WriteLine("  fixture summary-hard-gate: WRONG (any unqualified or missing sample must keep the run unqualified)");}
    else log.WriteLine("  fixture summary-hard-gate: ok (any unqualified or missing sample keeps overallQualified false / non-zero exit)");
    return failures;
}

static bool RunOverallQualified(int expectedSamples,int attemptedSamples,int unqualifiedSamples)
    =>unqualifiedSamples==0&&attemptedSamples==expectedSamples;

static JsonObject ValidCapture(BenchCase bench,int reps)
{
    var timings=new JsonArray();var steps=new JsonArray();var checks=new JsonArray();
    for(var rep=0;rep<reps;rep++)
    {
        steps.Add(new JsonObject{["id"]=$"operation-{rep+1}",["title"]=bench.OperationApi,["gm"]=new JsonObject{["before"]=null,["after"]=null},["pk"]=new JsonObject{["before"]=null,["after"]=null}});
        timings.Add(new JsonObject{["step"]=rep,["kernel"]="gm",["ms"]=1.5,["error"]=0,["resultStatus"]=21650});
        timings.Add(new JsonObject{["step"]=rep,["kernel"]="pk",["ms"]=1.5,["error"]=0,["resultStatus"]=21650});
        foreach(var id in RequiredCheckIds(rep,bench.ApiKind))
            checks.Add(new JsonObject{["id"]=id,["label"]="synthetic",["status"]="pass",["gm"]=null,["pk"]=null,["absDelta"]=null,["normalizedDelta"]=null,["threshold"]=null,["unit"]="",["evidence"]=null,["note"]="synthetic fixture"});
    }
    return new JsonObject{["schemaVersion"]=1,["steps"]=steps,["checks"]=checks,["timings"]=timings,["captureErrors"]=new JsonArray(),["pkVersion"]="38.0.150",["sessionPrecision"]=7.105427357601002E-15};
}

static double Median(List<double> values){var a=values.Order().ToArray();return a.Length%2==1?a[a.Length/2]:(a[a.Length/2-1]+a[a.Length/2])/2;}
static double SafeMs(JsonObject row)
{
    try{return row["ms"]?.GetValue<double>()??double.NaN;}catch(Exception){return double.NaN;}
}
static double SafeNodeMs(JsonNode? node)
{
    try{return node?.GetValue<double>()??double.NaN;}catch(Exception){return double.NaN;}
}
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

static BenchCase[] BenchCases()=>[
    new("sphereBoolean.equal-overlap-subtract",["sphereBoolean"],"AnalyticBooleanOracle.cs",[],"equal-overlap-15902","gm-first-then-pk",
        "PK_BODY_boolean_2","boolean","independent construction, identical numeric parameters",1200,[]),
    new("planarBoolean.overlap-two-axes-subtract",["planarBoolean"],"BooleanOperationOracle.cs",[],"overlap-two-axes-15902","gm-first-then-pk",
        "PK_BODY_boolean_2","boolean","independent construction, identical numeric parameters",1200,[]),
    new("regularcurved.block-subtract",["regularcurved"],"RegularCurvedBooleanOracle.cs",["block-15902"],"block-15902","gm-first-then-pk",
        "PK_BODY_boolean_2","boolean","independent construction, identical numeric parameters",1800,[]),
    new("solidblend.plane-nurbs",["solidblend"],"RegularSolidBlendOracle.cs",["plane-nurbs","rounding-only"],null,"pk-first-then-gm",
        "PK_BODY_fix_blends","blend","independent construction, identical numeric parameters",1800,[]),
    new("shortedge.a1-r0.5-t1e-07",["shortedge"],"ShortEdgeBlendOracle.cs",["--angle=1","--radius=0.5","--tolerance=1e-07"],null,"pk-first-then-gm",
        "PK_BODY_fix_blends","blend","PK fresh seed construction + transmit; GM receives that XT (PK keeps its in-memory original, no receive)",5400,[]),
];

internal sealed record BenchCase(string Id,string[] Aliases,string Script,string[] Args,string? Selector,string CallOrder,string OperationApi,string ApiKind,string InputProvenance,int TimeoutSeconds,string[] DiagnosticCheckIds);
internal readonly record struct Verdict(bool Qualified,List<string> Reasons);
