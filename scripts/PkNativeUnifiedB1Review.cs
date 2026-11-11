#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var script=Path.GetDirectoryName(Source())!;
var root=Path.GetFullPath(Path.Combine(script,".."));
var author=Path.GetFullPath(Path.Combine(root,"../gm-native-unified-delivery"));
var output=Path.GetFullPath(Path.Combine(script,args.Single()));
if(Directory.Exists(output))throw new IOException("Use a new independent output directory");
Directory.CreateDirectory(Path.Combine(output,"scripts"));
const string source="84c558c10135c117bf8be6629a157e6d230f1b2d";
var runner=Path.Combine(output,"scripts/NativeModelingWorkComparison.cs");
var runnerBytes=await GitBytes("show",source+":scripts/NativeModelingWorkComparison.cs");
File.WriteAllBytes(runner,runnerBytes);
Require(Hash(runner)=="78c98d884640d185c07337d4d34ec02bdb14f9d5fa3876cfef6c19831b75f6a5","fixed runner hash");
foreach(var (path,expected) in new[]{("src/ProjectGmKernel.Native","133f56acb0cf485366cd4f13f457dc3b161655eb"),("src","0c5d23f6f4e3e7aabe5034888f92812c3ecb07ad")})
    Require(System.Text.Encoding.UTF8.GetString(await GitBytes("rev-parse",source+":"+path)).Trim()==expected,"unchanged "+path);
var prior=Path.Combine(root,"temp_docs/unified-review-4b2405e");
var authorAudits=Path.Combine(author,"temp_docs/unified-delivery/b1-gate-v3");
var actual=Path.Combine(author,"temp_docs/unified-delivery/current-runner-v2");
var cases=new List<(string Name,string Directory,int Expected)>();
foreach(var name in new[]{"intact","no-modulus","no-body-check","no-compare","no-estimates-lower","no-evidence-links","rejected-compare-evidence"})
    cases.Add((name,Path.Combine(prior,"audit-"+name),name=="intact"?0:name=="rejected-compare-evidence"?1:2));
foreach(var name in new[]{"wrong-step","duplicate-component","source-contradiction","report-no-estimate","false-verified-bounds","native-api-error","command-source-contradiction"})
    cases.Add((name,Path.Combine(authorAudits,name),name is "duplicate-component" or "report-no-estimate"?2:1));
cases.Add(("original-planar",Path.Combine(actual,"01-planar-candidate"),0));
foreach(var name in new[]{"00-sphere-candidate","02-curved-candidate","03-solidblend-candidate","04-shortedge-candidate"})
    cases.Add((name,Path.Combine(actual,name),2));
var handoff=Path.Combine(author,"temp_docs/unified-delivery/handoff-current-v2.zip");
var handoffHash=Hash(handoff);Require(handoffHash=="5197d647fbc47a15f3338e83f1c4063227fddcd4dfff92186c0274aa510495bf","unchanged handoff");
var results=new JsonArray();var failed=false;
foreach(var item in cases)
{
    var input=Hash(Path.Combine(item.Directory,"capture.json"));
    var originalFiles=Directory.EnumerateFiles(item.Directory,"*",SearchOption.AllDirectories)
        .Where(p=>Path.GetExtension(p) is not (".dll" or ".pdb")).ToDictionary(p=>p,Hash);
    var argv=new[]{"run",Path.GetRelativePath(root,runner),"-c","Release","-p:UseSharedCompilation=false","--","--audit",Path.GetRelativePath(Path.GetDirectoryName(runner)!,item.Directory),"--repeat","1"};
    var info=Start("dotnet",argv);using var process=Process.Start(info)!;
    var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();
    var prefix=Path.Combine(output,item.Name);
    File.WriteAllText(prefix+".log",await stdout+"\nSTDERR\n"+await stderr);File.WriteAllText(prefix+".exit",process.ExitCode+"\n");
    foreach(var file in originalFiles)Require(Hash(file.Key)==file.Value,"audit mutated input");
    Require(Directory.EnumerateFiles(item.Directory,"*",SearchOption.AllDirectories).Count(p=>Path.GetExtension(p) is not (".dll" or ".pdb"))==originalFiles.Count,"audit added input files");
    var result=new JsonObject{["name"]=item.Name,["input"]=Path.GetRelativePath(root,item.Directory),["inputCaptureSha256"]=input,
        ["argv"]=new JsonArray(argv.Select(a=>(JsonNode?)JsonValue.Create(a)).ToArray()),["rawAuditExit"]=process.ExitCode,["expectedAuditExit"]=item.Expected,
        ["logSha256"]=Hash(prefix+".log"),["exitSha256"]=Hash(prefix+".exit"),["inputFilesUnchanged"]=originalFiles.Count};
    results.Add((JsonNode)result);failed|=process.ExitCode!=item.Expected;
    Console.WriteLine($"{item.Name}: rawAuditExit={process.ExitCode} expected={item.Expected}");
}
Require(Hash(handoff)==handoffHash,"handoff unchanged after audit");
var receipt=new JsonObject{["reviewedRunnerCommit"]=source,["runnerSha256"]=Hash(runner),["reviewScriptSha256"]=Hash(Source()),
    ["priorReviewCommit"]="583d917890a9f654dbd4a1f4e96e5e5f261f68eb",["captureExecutionCommit"]="4b2405e3945929c5bb93677a68b0ee79e430b2f3",
    ["nativeTree"]="133f56acb0cf485366cd4f13f457dc3b161655eb",["handoffSha256"]=handoffHash,["modelingExecuted"]=false,
    ["results"]=results,["checkerExit"]=failed?1:0};
File.WriteAllText(Path.Combine(output,"receipt.json"),receipt.ToJsonString());
Console.WriteLine("B1 independent controls "+(failed?"FAILED":"PASS")+"; audit exits are not modeling exits");return failed?1:0;

ProcessStartInfo Start(string file,IEnumerable<string> args)
{
    var info=new ProcessStartInfo(file){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";foreach(var arg in args)info.ArgumentList.Add(arg);return info;
}
async Task<byte[]> GitBytes(params string[] args)
{
    using var process=Process.Start(Start("git",args))!;using var bytes=new MemoryStream();
    var stderr=process.StandardError.ReadToEndAsync();await process.StandardOutput.BaseStream.CopyToAsync(bytes);await process.WaitForExitAsync();
    Require(process.ExitCode==0,"git: "+await stderr);return bytes.ToArray();
}
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Source([CallerFilePath]string path="")=>path;
