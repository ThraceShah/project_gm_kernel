#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var evidence=Path.Combine(root,"temp_docs/operation-followup-a0-review");
var controls=Path.Combine(evidence,"version-controls");var output=Path.Combine(evidence,"0935-controls");Directory.CreateDirectory(output);
var original=Path.Combine(controls,"numeric-version-forged-pass/runs/test/manifest.json");
if(Hash(original)!="ba1ac0f08a69b4fdc09c28d2826d740fbf4ec5d7c524de88e9ed88b266fc7d17")throw new InvalidDataException("Original a0 repro bytes changed");
var cases=new List<(string Label,string Data,int Expected)>
{
    ("original-numeric",Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(original)))!,1),
    ("complete-v2",Path.Combine(controls,"v2-complete"),0),
    ("truly-missing-v1",Path.Combine(controls,"missing-legacy"),0),
    ("missing-boolean2-unverified",Path.Combine(controls,"missing-boolean2-conservative"),0),
    ("missing-boolean2-forged-pass",Path.Combine(controls,"missing-boolean2-forged-pass"),1),
    ("unknown-forged-pass",Path.Combine(controls,"unknown-forged-pass"),1),
    // 0935 adds a reason even for unknown strings: retain this observable
    // compatibility boundary, rather than silently rewriting the old fixture.
    ("old-unknown-reasons",Path.Combine(controls,"unknown-conservative"),1)
};
void AddDerived(string label,JsonNode? version,bool unknownConservative=false)
{
    var data=Path.Combine(output,label);Directory.CreateDirectory(Path.Combine(data,"runs/test"));
    File.Copy(Path.Combine(controls,"numeric-version-forged-pass/index.json"),Path.Combine(data,"index.json"),true);
    var m=JsonNode.Parse(File.ReadAllBytes(original))!.AsObject();var op=m["cases"]![0]!["operations"]![0]!;
    op["interpretationVersion"]=version;
    if(unknownConservative)
    {
        op["status"]="unverified";op["primaryStatus"]="unverified";
        op["timing"]!["correctnessQualified"]=false;op["timing"]!["ratio"]=null;
        op["timing"]!["unqualifiedReasons"]=new JsonArray("unknown-or-invalid-interpretation-version","undeclared-or-unsupported-primary-contract","missing-required-primary","primary-not-pass");
    }
    File.WriteAllText(Path.Combine(data,"runs/test/manifest.json"),m.ToJsonString());cases.Add((label,data,unknownConservative?0:1));
}
AddDerived("explicit-null",null);AddDerived("explicit-object",new JsonObject());
AddDerived("unknown-current-reasons",JsonValue.Create("operation-rules-v99"),true);
cases.Add(("original-historical-data",Path.Combine(evidence,"old-only-data"),0));
var script=Path.Combine(evidence,"0935/scripts/ValidateWebProgress.cs");var failed=false;var summary=new JsonArray();
foreach(var c in cases)
{
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};
    foreach(var arg in new[]{"run","--file",Path.GetRelativePath(root,script),"-c","Release","-p:UseSharedCompilation=false","--",Path.GetRelativePath(Path.GetDirectoryName(script)!,c.Data)})start.ArgumentList.Add(arg);
    start.Environment["MSBUILDDISABLENODEREUSE"]="1";
    using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();process.WaitForExit();
    var text=await stdout+await stderr;File.WriteAllText(Path.Combine(output,c.Label+".log"),text);File.WriteAllText(Path.Combine(output,c.Label+".exit"),process.ExitCode.ToString());
    var ok=process.ExitCode==c.Expected;failed|=!ok;Console.WriteLine($"{(ok?"PASS":"FAIL")} {c.Label} exit={process.ExitCode} expected={c.Expected}");
    summary.Add((JsonNode)new JsonObject{["label"]=c.Label,["exit"]=process.ExitCode,["expected"]=c.Expected});
}
if(Hash(original)!="ba1ac0f08a69b4fdc09c28d2826d740fbf4ec5d7c524de88e9ed88b266fc7d17")throw new InvalidDataException("Validator modified original repro");
File.WriteAllText(Path.Combine(output,"summary.json"),summary.ToJsonString());return failed?1:0;
static string Hash(string file)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
static string Here([CallerFilePath]string path="")=>path;
