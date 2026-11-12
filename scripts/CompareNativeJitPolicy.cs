#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// One bounded ABBA comparison. All cases use one Release build; each process
// constructs fresh models twice. No case-specific runtime or algorithm tuning.
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var name=args.Single();Require(name.Length>0&&name.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"new output name");
var directory=Path.Combine(root,"temp_docs","native-jit-policy",name);Require(!Directory.Exists(directory),"immutable output");Directory.CreateDirectory(directory);
var source=(await Capture("git",["rev-parse","HEAD"],root)).Trim();
Require(string.IsNullOrWhiteSpace(await Capture("git",["status","--porcelain","--untracked-files=normal"],root)),"commit source before the fixed comparison");
var build=Path.Combine(directory,"managed");Directory.CreateDirectory(build);
var buildInfo=StartInfo("dotnet",root);foreach(var a in new[]{"build","scripts/RegularSolidBlendOracle.cs","-c","Release","-o",build,"-p:UseSharedCompilation=false","-p:BuildInParallel=false"})buildInfo.ArgumentList.Add(a);
buildInfo.Environment["MSBUILDDISABLENODEREUSE"]="1";
Require(await Run(buildInfo,Path.Combine(directory,"build"))==0,"managed build failed; this is not a modeling result");
var config=Directory.GetFiles(build,"*.runtimeconfig.json").Single();var stem=config[..^".runtimeconfig.json".Length];var assembly=stem+".dll";
Require(File.Exists(assembly),"managed oracle assembly");
var files=new JsonObject();foreach(var path in Directory.GetFiles(build).Where(p=>p.EndsWith(".dll",StringComparison.Ordinal)||p.EndsWith(".pdb",StringComparison.Ordinal)||p.EndsWith(".json",StringComparison.Ordinal)).Order(StringComparer.Ordinal))files[Path.GetFileName(path)]=Hash(path);
var manifest=new JsonObject{["source"]=source,["configuration"]="Release managed, no AOT",["host"]=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    ["architecture"]=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),["assemblies"]=files,["baseRuntimeConfig"]=JsonNode.Parse(File.ReadAllText(config)),
    ["policyDecisionRule"]="minimum total self API time over all four processes/families/repetitions; ties choose off; individual cases do not choose policy",
    ["scope"]="four complete Fix/Boolean families, two fresh models per family per process; ordinal zero is first-family-use, not universally process-cold",["runs"]=new JsonArray()};
var expectedNative=Hash(Path.Combine(build,"ProjectGmKernel.Native.dll"));var totals=new double[2];
foreach(var (tier,index) in new[]{(0,0),(1,1),(1,2),(0,3)})
{
    var runName=$"{name}-run{index}-tc{tier}";var prefix=Path.Combine(directory,$"run{index}-tc{tier}");
    var runtime=JsonNode.Parse(File.ReadAllText(config))!.AsObject();var options=runtime["runtimeOptions"]!.AsObject();
    var properties=options["configProperties"] as JsonObject;if(properties==null){properties=new JsonObject();options["configProperties"]=properties;}
    properties["System.Runtime.TieredCompilation"]=tier==1;properties["System.Runtime.TieredPGO"]=true;
    properties["System.Runtime.TieredCompilation.QuickJit"]=true;properties["System.Runtime.TieredCompilation.QuickJitForLoops"]=true;
    // ReadyToRun is controlled by its documented environment knob, not an
    // invented runtimeconfig property. Reject any pre-existing contrary key.
    Require(!properties.ContainsKey("System.Runtime.ReadyToRun"),"unexpected ReadyToRun runtimeconfig key");
    var runConfig=prefix+".runtimeconfig.json";File.WriteAllText(runConfig,runtime.ToJsonString(new(){WriteIndented=true}));
    var info=StartInfo("dotnet",root);var inherited=info.Environment.ToArray();info.Environment.Clear();
    var allowed=new[]{"PATH","HOME","TMPDIR","TMP","TEMP","LANG","LC_ALL","DOTNET_ROOT","DOTNET_ROOT_X64","DOTNET_ROOT_ARM64"};
    foreach(var entry in inherited)if(allowed.Contains(entry.Key,StringComparer.Ordinal))info.Environment[entry.Key]=entry.Value;
    info.Environment["DOTNET_TieredCompilation"]=tier.ToString(CultureInfo.InvariantCulture);info.Environment["DOTNET_TieredPGO"]="1";
    info.Environment["DOTNET_TC_QuickJit"]="1";info.Environment["DOTNET_TC_QuickJitForLoops"]="1";info.Environment["DOTNET_ReadyToRun"]="0";
    info.Environment["MSBUILDDISABLENODEREUSE"]="1";
    if(index<2)
    {
        var proof=StartInfo("dotnet",root);proof.Environment.Clear();foreach(var entry in info.Environment)proof.Environment[entry.Key]=entry.Value;
        proof.Environment["DOTNET_JitDisasm"]="*RuntimePolicyProof*";proof.Environment["DOTNET_JitStdOutFile"]=prefix+".jit.txt";
        foreach(var a in new[]{"exec","--runtimeconfig",runConfig,assembly,"--jit-proof-only"})proof.ArgumentList.Add(a);
        Require(await Run(proof,prefix+"-jit-proof")==0,"unmeasured runtime proof child");var disassembly=File.ReadAllText(prefix+".jit.txt");
        Require(disassembly.Contains(tier==0?"(FullOpts)":"Tier0",StringComparison.Ordinal),"actual JIT tier did not match the request");
    }
    foreach(var a in new[]{"exec","--runtimeconfig",runConfig,assembly,"plane-nurbs","depressed-roof","nurbs-nurbs","concave-nurbs-nurbs","--bench-repeat=2","--bench-profile","--output-name="+runName})info.ArgumentList.Add(a);
    Require(Hash(Path.Combine(build,"ProjectGmKernel.Native.dll"))==expectedNative,"same fixed Native IL");
    Console.WriteLine($"RUN {index} tiered={tier} source={source} same-IL={expectedNative}");
    var exit=await Run(info,prefix);var text=File.ReadAllText(prefix+".stdout.log");var rows=new JsonArray();var ordinal=new Dictionary<string,int>(StringComparer.Ordinal);var family="";var repetition=-1;
    foreach(var line in text.Split('\n'))
    {
        if(line.StartsWith("CASE ",StringComparison.Ordinal)){family=line[5..line.IndexOf(',')];ordinal.TryGetValue(family,out repetition);ordinal[family]=repetition+1;}
        if(!line.StartsWith("TIME self BODY_",StringComparison.Ordinal)&&!line.StartsWith("TIME PK BODY_",StringComparison.Ordinal))continue;
        var own=line.StartsWith("TIME self",StringComparison.Ordinal);var operation=line.Contains("BODY_fix_blends",StringComparison.Ordinal)?"fix_blends":"boolean";
        var at=line.IndexOf("ms=",StringComparison.Ordinal)+3;var end=line.IndexOf(',',at);var ms=double.Parse(line[at..end],CultureInfo.InvariantCulture);
        Require(line.TrimEnd().EndsWith("error=0",StringComparison.Ordinal),"actual operation failed");
        rows.Add((JsonNode)new JsonObject{["family"]=family,["repetition"]=repetition,["system"]=own?"GM":"PK",["operation"]=operation,["ms"]=ms});if(own)totals[tier]+=ms;
    }
    Require(rows.Count==32,"all independently executed operations recorded");
    var geometryPass=text.Contains("PASS all 8 freeform solid fillet and subsequent cutting cases",StringComparison.Ordinal)&&!text.Contains("FAIL ",StringComparison.Ordinal);
    var run=new JsonObject{["index"]=index,["tiered"]=tier,["rawExit"]=exit,["geometryPass"]=geometryPass,["runtimeConfigSha256"]=Hash(runConfig),["environment"]=new JsonObject(),["rows"]=rows};
    foreach(var entry in info.Environment)run["environment"]![entry.Key]=entry.Value;
    manifest["runs"]!.AsArray().Add((JsonNode)run);File.WriteAllText(Path.Combine(directory,"comparison.json"),manifest.ToJsonString(new(){WriteIndented=true}));
    Require(geometryPass&&(exit==0||exit==2),"full operation/XT validation failed; raw exit2 is only retained unresolved physical requests");
    Console.WriteLine($"COMPLETE {index} tiered={tier} raw-exit={exit} full geometry passed; mass remains separately reported");
}
manifest["gmApiTotalMsOff"]=totals[0];manifest["gmApiTotalMsOn"]=totals[1];manifest["selectedTieredCompilation"]=totals[1]<totals[0];
File.WriteAllText(Path.Combine(directory,"comparison.json"),manifest.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine($"SELECT tiered={(totals[1]<totals[0]?1:0)} all-family-api-ms off={totals[0]:F3} on={totals[1]:F3}");

static ProcessStartInfo StartInfo(string file,string root)=>new(file){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
static async Task<int> Run(ProcessStartInfo info,string prefix)
{
    using var process=Process.Start(info)??throw new InvalidOperationException("start process");
    await using var output=File.CreateText(prefix+".stdout.log");await using var error=File.CreateText(prefix+".stderr.log");
    var a=process.StandardOutput.BaseStream.CopyToAsync(output.BaseStream);var b=process.StandardError.BaseStream.CopyToAsync(error.BaseStream);
    await process.WaitForExitAsync();await Task.WhenAll(a,b);await File.WriteAllTextAsync(prefix+".exit",process.ExitCode.ToString(CultureInfo.InvariantCulture)+"\n");return process.ExitCode;
}
static async Task<string> Capture(string file,string[] args,string root){var info=StartInfo(file,root);foreach(var a in args)info.ArgumentList.Add(a);using var p=Process.Start(info)!;var a1=p.StandardOutput.ReadToEndAsync();var b=p.StandardError.ReadToEndAsync();await p.WaitForExitAsync();Require(p.ExitCode==0,await b);return await a1;}
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
static string Source([CallerFilePath]string file="")=>file;
