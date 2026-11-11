#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

var script=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath(Path.Combine(script,".."));
var original=Path.Combine(root,"temp_docs/pk-performance-review/mac-quality-original");
var earlier=Path.Combine(root,"temp_docs/pk-performance-review/mac-original");
var archive=Path.GetFullPath(Path.Combine(script,"../../gm-delivery-board/temp_docs/incoming/mac-performance/mac-modeling-quality-6761600.tar.gz"));
var output=Path.GetFullPath(Path.Combine(script,args.Length>0?args[0]:"../docs/Pk_Performance_6761600_Final_Quality_Audit.json"));
if(File.Exists(output))throw new IOException("Derived output already exists");
var archiveHash=Hash(archive);Require(archiveHash=="f51e1cde728f63bfd6af64520b5e5f2267813dc828be0cba394c472b3113887c","fixed quality archive");
var members=0;long expanded=0;
using(var stream=File.OpenRead(archive))using(var unzip=new GZipStream(stream,CompressionMode.Decompress))using(var tar=new TarReader(unzip))
while(tar.GetNextEntry() is { } entry)
{
    Require(entry.EntryType==TarEntryType.RegularFile&&!Path.IsPathRooted(entry.Name)&&!entry.Name.Split('/').Contains(".."),"regular relative member");
    members++;expanded+=entry.Length;
    Require(Convert.ToHexStringLower(SHA256.HashData(entry.DataStream!))==Hash(Path.Combine(original,entry.Name)),"original member byte hash");
}
Require(members==8&&expanded==2797231,"quality package member/byte count");
var receipt=JsonNode.Parse(File.ReadAllText(Path.Combine(original,"docs/native-modeling-work-reduction/suite-receipt.json")))!;
Require(receipt["suiteSourceAtLaunch"]!.GetValue<string>()=="6761600fef20602342cd9dc81bd4f7e4fb85d939"&&receipt["nativeTree"]!.GetValue<string>()=="01e03aa9ed10a8145556872e98fa1677e17a4cb7","declared source scope");
foreach(var artifact in receipt["artifacts"]!.AsArray())
{
    var path=artifact!["path"]!.GetValue<string>();var tree=path.Contains("complete-suite",StringComparison.Ordinal)?original:earlier;
    var file=Path.Combine(tree,path);Require(new FileInfo(file).Length==artifact["bytes"]!.GetValue<long>()&&Hash(file)==artifact["sha256"]!.GetValue<string>(),"referenced original log/TRX/exit");
}
const string prefix="temp_docs/pk-native-work-reduction/";
Require(File.ReadAllText(Path.Combine(original,prefix+"complete-suite.exit")).Trim()=="1","raw suite external exit");
var log=File.ReadAllText(Path.Combine(original,prefix+"complete-suite.log"));
Require(log.Contains("Failed:     2, Passed:  1875, Skipped:     0, Total:  1877",StringComparison.Ordinal),"original log settlement");
var trx=XDocument.Load(Path.Combine(original,prefix+"test-results/complete-suite.trx"));XNamespace ns=trx.Root!.Name.Namespace;
var results=trx.Descendants(ns+"UnitTestResult").ToArray();var counters=trx.Descendants(ns+"Counters").Single();
Require(results.Length==1877&&results.Count(r=>A(r,"outcome")=="Passed")==1875&&results.Count(r=>A(r,"outcome")=="Failed")==2,"actual TRX outcomes");
Require(results.Select(r=>A(r,"executionId")).Distinct().Count()==1877,"unique executions");
foreach(var key in new[]{"total","executed","passed","failed","notExecuted","timeout","aborted","error"})Require(int.Parse(A(counters,key))==receipt["counters"]![key]!.GetValue<int>(),"TRX/receipt count "+key);
var failed=results.Where(r=>A(r,"outcome")=="Failed").OrderBy(r=>A(r,"testName")).ToArray();
var expected=new[]{"KernelTests.BlendIntersectionTests.NativeCurveTubeBoundsItsSupportingBlendParameterAndDerivative","KernelTests.NestedBlendCacheEmptyReadSetTests.SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled"};
Require(failed.Select(r=>A(r,"testName")).SequenceEqual(expected),"original failed names");
var failures=new JsonArray();
foreach(var result in failed)
{
    var message=result.Descendants(ns+"Message").Single().Value;
    Require(message.Contains("Expected: Success",StringComparison.Ordinal)&&message.Contains("Actual:   NotConverged",StringComparison.Ordinal),"actual failure semantics");
    var claim=receipt["failures"]!.AsArray().Single(f=>f!["test"]!.GetValue<string>()==A(result,"testName"))!;
    Require(claim["message"]!.GetValue<string>()==message,"receipt matches original failure");
    failures.Add((JsonNode)new JsonObject{["test"]=A(result,"testName"),["message"]=message});
}
Require(results.Count(r=>A(r,"testName").StartsWith("KernelTests.NestedBlendJacobianRecordTests.",StringComparison.Ordinal))==12,"record tests included");
Require(!results.Any(r=>A(r,"testName").Contains("NestedBlendJacobianRecordCapacityTests",StringComparison.Ordinal)),"capacity test separate from suite");
foreach(var lifecycle in receipt["lifecycleChecks"]!.AsArray())Require(results.Single(r=>A(r,"testName")==lifecycle!["test"]!.GetValue<string>()).Attribute("outcome")!.Value=="Passed","actual lifecycle result");
Require(receipt["compiledTestAssemblyShaAtLaunch"]==null,"launch DLL hash explicitly absent");
var baselineOriginal=Path.Combine(root,"temp_docs/pk-performance-review/mac-baseline-original");
var baselineArchive=Path.GetFullPath(Path.Combine(script,"../../gm-delivery-board/temp_docs/incoming/mac-performance/mac-modeling-baseline-failures.tar.gz"));
var baselineHash=Hash(baselineArchive);Require(baselineHash=="0c93eb4ebedc840f0bb5975dffb66903ae912950e3a11d2f01132cc2f7cd57fd","fixed baseline comparison archive");
var baselineMembers=0;long baselineBytes=0;
using(var stream=File.OpenRead(baselineArchive))using(var unzip=new GZipStream(stream,CompressionMode.Decompress))using(var tar=new TarReader(unzip))
while(tar.GetNextEntry() is { } entry)
{
    Require(entry.EntryType is TarEntryType.RegularFile or TarEntryType.Directory&&!Path.IsPathRooted(entry.Name)&&!entry.Name.Split('/').Contains(".."),"baseline relative regular/directory member");
    baselineMembers++;baselineBytes+=entry.Length;
    if(entry.EntryType==TarEntryType.RegularFile)Require(Convert.ToHexStringLower(SHA256.HashData(entry.DataStream!))==Hash(Path.Combine(baselineOriginal,entry.Name)),"baseline extracted original byte hash");
}
Require(baselineMembers==24&&baselineBytes==150196,"baseline package member/byte count");
var comparisonPath=Path.Combine(baselineOriginal,prefix+"baseline-failure-comparison/report.json");
Require(Hash(comparisonPath)=="215c4dbe56c83fa19cd4d4cceb9ab20629754a25290cffe9b0b9da403035a326","original comparison report");
var comparison=JsonNode.Parse(File.ReadAllText(comparisonPath))!;var runs=comparison["runs"]!.AsArray();
Require(runs.Count==2&&comparison["cpu"]!.GetValue<string>()=="Apple M4"&&comparison["runtime"]!.GetValue<string>()==".NET 10.0.11"&&comparison["architecture"]!.GetValue<string>()=="Arm64","same-platform comparison");
foreach(var run in runs)
{
    var side=run!["source"]!.GetValue<string>();Require(side is "baseline" or "candidate","comparison side");
    Require(run["nativeTree"]!.GetValue<string>()==(side=="baseline"?"e314cc2307c5e77ea6f2b7685ba17836ae4ef5c8":"01e03aa9ed10a8145556872e98fa1677e17a4cb7"),"fixed comparison source tree");
    Require(run["originalTestFileSha256"]!.GetValue<string>()==Hash(Path.Combine(root,"tests/KernelTests/BlendIntersectionTests.cs"))&&run["guardTestFileSha256"]!.GetValue<string>()==Hash(Path.Combine(root,"tests/KernelTests/NestedBlendCacheEmptyReadSetTests.cs")),"same actual test source bytes");
    Require(run["argv"]!.AsArray().Any(a=>a!.GetValue<string>()=="--no-build")&&run["argv"]!.AsArray().Any(a=>a!.GetValue<string>()=="--no-restore"),"focused binary-preserving invocation");
    foreach(var phase in new[]{"build","test"})
    {
        var status=run[phase]!;var phaseLog=Path.Combine(baselineOriginal,status["log"]!.GetValue<string>());var wanted=phase=="build"?0:1;
        Require(status["rawExit"]!.GetValue<int>()==wanted&&!status["timedOut"]!.GetValue<bool>()&&File.ReadAllText(phaseLog+".exit").Trim()==wanted.ToString(),"actual focused build/test external exit");
        Require(Hash(phaseLog)==status["logSha256"]!.GetValue<string>(),"original comparison log hash");
    }
    foreach(var assembly in new[]{"testAssembly","nativeAssembly"})Require(run[assembly+"Sha256BeforeRun"]!.GetValue<string>()==run[assembly+"Sha256AfterRun"]!.GetValue<string>(),"recorded assembly fingerprint stability");
    var focusedPath=Path.Combine(baselineOriginal,run["trx"]!.GetValue<string>());Require(Hash(focusedPath)==run["trxSha256"]!.GetValue<string>(),"original focused TRX hash");
    var focused=XDocument.Load(focusedPath);var actual=focused.Descendants(ns+"UnitTestResult").OrderBy(r=>A(r,"testName")).ToArray();
    Require(actual.Length==2&&actual.Select(r=>A(r,"testName")).SequenceEqual(expected)&&actual.All(r=>A(r,"outcome")=="Failed"),"actual same two focused failures");
    for(var i=0;i<2;i++)Require(actual[i].Descendants(ns+"Message").Single().Value==failed[i].Descendants(ns+"Message").Single().Value,"focused/full-suite failure semantics");
    var env=File.ReadAllText(Path.Combine(baselineOriginal,prefix+$"failure-comparison-env-{side}.log"));Require(env.Contains("ENV_READY: profile=native pk=38.0 platform=mac-arm64",StringComparison.Ordinal),"actual environment readiness");
}
var derived=new JsonObject{["format"]="independent-6761600-final-quality-audit-v1",["archiveSha256"]=archiveHash,["archiveMembers"]=members,["expandedOriginalBytes"]=expanded,["rawSuiteExit"]=1,["total"]=1877,["passed"]=1875,["failed"]=2,["skipped"]=0,["recordTestsIncluded"]=12,["separateCapacityTestIncluded"]=false,["failures"]=failures,["authorDeclaredSource"]=receipt["suiteSourceAtLaunch"]!.DeepClone(),["compiledTestAssemblyShaAtLaunch"]=(JsonNode?)null,["sourceBindingLimit"]="No full-suite launch DLL hash was retained; later focused runs record stable before/after assembly hashes but cannot retroactively bind the original full-suite binary.",["baselineFailureClassification"]="Same two NotConverged failures independently verified in same-Mac fixed baseline and production-identical candidate focused originals; no newly observed failure among these two.",["baselineComparisonArchiveSha256"]=baselineHash,["baselineComparisonReportSha256"]=Hash(comparisonPath),["focusedSourceAndAssemblyReceipts"]=runs.DeepClone()};
File.WriteAllText(output,derived.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine("PASS original suite audit: external exit=1; total=1877 passed=1875 failed=2 skipped=0; no full-green claim");
Console.WriteLine("PASS same-Mac original focused comparison: both builds exit 0; both two-test runs exit 1 with the same two actual failures");
foreach(var failure in failures)Console.WriteLine(failure!["test"]!.GetValue<string>());
static string A(XElement e,string name)=>e.Attribute(name)!.Value;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static string ScriptPath([CallerFilePath]string path="")=>path;
