#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// Explicit read-only analysis of the fixed Joint window's historical reports.
// Production APIs never read this historical format or select an old policy.
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(args.Length!=3||args.Any(Path.IsPathRooted)||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("new output name, fixed Joint window directory and original archive required");
var source=Path.Combine(root,args[1]);var output=Path.Combine(root,"temp_docs","mass-certification-review",args[0]);
if(Directory.Exists(output))throw new InvalidOperationException("Evidence is immutable");Directory.CreateDirectory(output);
var archiveBytes=File.ReadAllBytes(Path.Combine(root,args[2]));var archiveHash=Hash(archiveBytes);
if(archiveHash!="5a22e9acae103d1b3b6fcfcb920c7eb13ae526d42fdf5181ac8e78db332436f1")throw new InvalidOperationException("Fixed Joint original archive hash mismatch");
using var archive=ZipFile.OpenRead(Path.Combine(root,args[2]));
var paths=Directory.GetFiles(source,"quality-mass-*.json",SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
if(paths.Length!=72)throw new InvalidOperationException($"Fixed Joint window requires 72 original mass reports, found {paths.Length}");
ReadOnlySpan<string> names=["volume","mass","cog.x","cog.y","cog.z","inertia.xx","inertia.xy","inertia.xz","inertia.yx","inertia.yy","inertia.yz","inertia.zx","inertia.zy","inertia.zz","area"];
ReadOnlySpan<int> positions=[0,2,4,5,6,8,9,10,11,12,13,14,15,16,18];ReadOnlySpan<int> moduli=[1,3,7,7,7,17,17,17,17,17,17,17,17,17,19];
var reports=new JsonArray();var counts=new Dictionary<string,int>();var oldVerified=0;var oldUnresolved=0;var oldDifferent=0;
ParasolidMassProperties.BeginEvidence(output);
foreach(var path in paths)
{
    var bytes=File.ReadAllBytes(path);var original=JsonNode.Parse(bytes)!.AsObject();var properties=original["properties"]!.AsArray();
    var entryName="temp_docs/joint-controlled/window/"+Path.GetRelativePath(source,path).Replace('\\','/');
    var entry=archive.GetEntry(entryName)??throw new InvalidOperationException("Original archive entry missing: "+entryName);
    using(var stream=entry.Open())if(entry.Length!=bytes.Length||Convert.ToHexStringLower(SHA256.HashData(stream))!=Hash(bytes))throw new InvalidOperationException("Report differs from fixed archive");
    if(properties.Count!=15||original["referenceError"]!.GetValue<int>()!=0||original["actualError"]!.GetValue<int>()!=0)throw new InvalidOperationException("Complete successful native integration report required");
    var label=original["label"]!.GetValue<string>();var extra=label=="native via managed";var cohort=extra?"roundtrip":"main";
    Span<double> a=stackalloc double[20];Span<double> b=stackalloc double[20];a.Clear();b.Clear();
    var originalComponents=new JsonArray();
    for(var i=0;i<15;i++)
    {
        var p=properties[i]!;if(p["property"]!.GetValue<string>()!=names[i])throw new InvalidOperationException("Fixed native component order mismatch");
        a[positions[i]]=p["referenceEstimate"]!.GetValue<double>();b[positions[i]]=p["actualEstimate"]!.GetValue<double>();
        var ra=p["referenceModulus"]!.GetValue<double>();var rb=p["actualModulus"]!.GetValue<double>();
        if(i is 3 or 4 or >=6 and <=13)
        {if(a[moduli[i]]!=ra||b[moduli[i]]!=rb)throw new InvalidOperationException("Shared native modulus differs between components");}
        a[moduli[i]]=ra;b[moduli[i]]=rb;
        var state=p["agreementStatus"]!.GetValue<string>();if(state=="Verified")oldVerified++;else if(state=="Unresolved")oldUnresolved++;else if(state=="Different")oldDifferent++;else throw new InvalidOperationException("Unknown historical status");
        Count(cohort+".before."+state);originalComponents.Add(p.DeepClone());
    }
    var current=new JsonObject{["label"]=label,["properties"]=new JsonArray()};
    var aggregate=ParasolidMassProperties.Assess(a,b,label,original["accuracy"]!.GetValue<double>(),current);current["agreementStatus"]=aggregate.ToString();
    for(var i=0;i<15;i++)
    {
        var p=current["properties"]![i]!;var before=properties[i]!["agreementStatus"]!.GetValue<string>();var after=p["agreementStatus"]!.GetValue<string>();
        Count(cohort+".after."+after);Count(cohort+".transition."+before+"-"+after);
        p["beforeAgreementStatus"]=before;
    }
    reports.Add((JsonNode)new JsonObject{["sourcePath"]=Path.GetRelativePath(root,path),["sourceBytes"]=bytes.Length,["sourceSha256"]=Hash(bytes),
        ["cohort"]=cohort,["oldProperties"]=originalComponents,["current"]=current});
}
if(oldVerified!=396||oldUnresolved!=684||oldDifferent!=0)throw new InvalidOperationException("Fixed Joint 1080-component historical counts mismatch");
if(counts.GetValueOrDefault("main.before.Verified")!=312||counts.GetValueOrDefault("main.before.Unresolved")!=588||counts.GetValueOrDefault("roundtrip.before.Verified")!=84||counts.GetValueOrDefault("roundtrip.before.Unresolved")!=96)
    throw new InvalidOperationException("Fixed main/roundtrip cohort counts mismatch");
foreach(var cohort in new[]{"main","roundtrip"})foreach(var phase in new[]{"before","after"})foreach(var state in new[]{"Verified","Different","Unresolved"})counts.TryAdd(cohort+"."+phase+"."+state,0);
var summary=new JsonObject();foreach(var pair in counts.OrderBy(x=>x.Key,StringComparer.Ordinal))summary[pair.Key]=pair.Value;
File.WriteAllText(Path.Combine(output,"results.json"),new JsonObject{["scope"]="standards reassessment only; no PK execution or model requalification",
    ["fixedOriginalReportCount"]=72,["fixedOriginalComponentCount"]=1080,["oldContract"]="component abs difference divided by (1+abs(reference)); threshold 1e-7; reported native moduli",
    ["archivePath"]=args[2],["archiveBytes"]=archiveBytes.Length,["archiveSha256"]=archiveHash,
    ["currentContract"]="Euclidean cog / Rg; Frobenius inertia / trace; relative positive volume,mass,area; threshold 1e-7; outward bounds",
    ["helperSha256"]=Hash(File.ReadAllBytes(Path.Combine(root,"scripts","ParasolidMassProperties.cs"))),["scriptSha256"]=Hash(File.ReadAllBytes(Source())),
    ["summary"]=summary,["reports"]=reports}.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(summary.ToJsonString());
void Count(string key){counts[key]=counts.GetValueOrDefault(key)+1;}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
