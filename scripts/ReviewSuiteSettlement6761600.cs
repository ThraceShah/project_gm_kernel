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
var output=Path.GetFullPath(Path.Combine(script,args.Length>0?args[0]:"../docs/Pk_Performance_6761600_Suite_Audit.json"));
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
var derived=new JsonObject{["format"]="independent-6761600-suite-audit-v1",["archiveSha256"]=archiveHash,["archiveMembers"]=members,["expandedOriginalBytes"]=expanded,["rawSuiteExit"]=1,["total"]=1877,["passed"]=1875,["failed"]=2,["skipped"]=0,["recordTestsIncluded"]=12,["separateCapacityTestIncluded"]=false,["failures"]=failures,["authorDeclaredSource"]=receipt["suiteSourceAtLaunch"]!.DeepClone(),["compiledTestAssemblyShaAtLaunch"]=(JsonNode?)null,["sourceBindingLimit"]="No launch DLL hash was retained; current rebuilt assemblies cannot bind this historical execution.",["baselineFailureClassification"]="Pending original same-platform baseline evidence; no author label or Linux pass substitutes for it."};
File.WriteAllText(output,derived.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine("PASS original suite audit: external exit=1; total=1877 passed=1875 failed=2 skipped=0; no full-green claim");
foreach(var failure in failures)Console.WriteLine(failure!["test"]!.GetValue<string>());
static string A(XElement e,string name)=>e.Attribute(name)!.Value;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static string ScriptPath([CallerFilePath]string path="")=>path;
