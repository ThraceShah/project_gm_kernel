#!/usr/bin/env dotnet run
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
Require(args.Length==3&&args[2] is "positive" or "controls","capture directory, oracle directory, positive|controls");
var capture=Relative(args[0]);var oracle=Relative(args[1]);var data=JsonNode.Parse(File.ReadAllText(Path.Combine(capture,"capture.json")))!;
Require(data["captureErrors"]!.AsArray().Count==0,"no hidden observer errors");
var checks=data["checks"]!.AsArray();var rows=File.ReadLines(Path.Combine(oracle,"mass-properties.csv")).Skip(1)
    .Select(x=>x.Split(',')).ToDictionary(x=>(x[0].Trim('"').Replace("\"\"","\""),x[1]));
var reports=Directory.GetFiles(Path.Combine(capture,"evidence"),"quality-*.json").Select(x=>JsonNode.Parse(File.ReadAllText(x))!).ToArray();
var mass=reports.Where(x=>x["properties"] is JsonArray).ToArray();Require(mass.Length>0,"same-call native bounded mass reports");
foreach(var report in reports)
{
    Require(report["sourceBinding"] is JsonObject,"actual pair/source binding");var identity=report["captureIdentity"]!;
    Require(!string.IsNullOrEmpty(identity["calculationCommit"]?.GetValue<string>())&&!string.IsNullOrEmpty(identity["captureCommit"]?.GetValue<string>()),"pinned calculation and observer sources");
    foreach(var pair in new[]{("captureSourceFileSha256","WebProgressCapture.cs"),("bodyCompareSourceFileSha256","ParasolidBodyCompare.cs"),("massSourceFileSha256","ParasolidMassProperties.cs")})
        Require(identity[pair.Item1]!.GetValue<string>()==Hash(File.ReadAllBytes(Path.Combine(root,"scripts",pair.Item2))),"actual observer/helper source bytes");
}
foreach(var report in mass)
{
    var properties=report["properties"]!.AsArray();Require(properties.Count==15,"all 15 actual error-bound components");
    foreach(var item in properties)
    {
        var row=rows[(report["label"]!.GetValue<string>(),item!["property"]!.GetValue<string>())];
        foreach(var pair in new[]{("referenceEstimate",3),("referenceModulus",4),("actualEstimate",5),("actualModulus",6),("normalizedLower",7),("normalizedUpper",8)})
            Require(item[pair.Item1]!.GetValue<double>()==double.Parse(row[pair.Item2],CultureInfo.InvariantCulture),"receipt equals the same oracle CSV result");
        Require(item["agreementStatus"]!.GetValue<string>()==row[9],"exact helper classification retained");
    }
}
foreach(var check in checks.Where(c=>c?["agreementStatus"]?.GetValue<string>()=="Unresolved"))
    Require(check!["status"]!.GetValue<string>()!="pass","Unresolved never becomes green");
var geometry=reports.Where(x=>x["mask"]!=null).ToArray();Require(geometry.Length>0,"same-call comparison receipts");
foreach(var report in geometry)
{
    var mask=report["mask"]!;Require(mask["max_diffs"]!.GetValue<int>()>0&&!mask["all_tests"]!.GetValue<bool>()&&!mask["acc_dev_tests"]!.GetValue<bool>()&&mask["tol_dev_tests"]!.GetValue<bool>(),"unchanged required native mask");
    Require(report["facePairRecords"]!.AsArray().Count==report["facePairs"]!.GetValue<int>(),"complete raw pair records");
}
if(args[2]=="positive")
{
    Require(checks.All(c=>c!["status"]!.GetValue<string>() is "pass" or "diagnostic"),"positive capture has no missing or failed qualification");
    var proofs=geometry.SelectMany(r=>r["localDiffs"]!.AsArray()).Where(d=>d?["conditionalFiniteProof"]!=null).Select(d=>d!["conditionalFiniteProof"]!).ToArray();
    Require(proofs.Length>0,"actual conditional finite proofs executed");
    foreach(var proof in proofs)Require(proof["proved"]!.GetValue<bool>()&&proof["directedFinUses"]!.AsArray().Count==2&&proof["totalBound"]!.GetValue<double>()<=proof["tolerance"]!.GetValue<double>(),"same-call complete finite proof");
    var steps=data["steps"]!.AsArray();Require(steps.Count==2&&steps[0]!["sourceBinding"]!["kind"]!.GetValue<string>()=="independent-construction"&&steps[1]!["sourceBinding"]!["kind"]!.GetValue<string>()=="same-byte-fork","upstream and downstream origins remain distinct");
    foreach(var meshPath in Directory.GetFiles(Path.Combine(capture,"meshes"),"*.json"))
    {
        var mesh=JsonNode.Parse(File.ReadAllText(meshPath))!;var source=mesh["source"]!;
        Require(Hash(File.ReadAllBytes(Path.Combine(capture,source["xtArtifact"]!.GetValue<string>())))==source["xtSha256"]!.GetValue<string>(),"same-instance display source XT bytes");
        Require(mesh["bodyId"]!.GetValue<string>().EndsWith(":"+source["sourceBodyTag"]!.GetValue<int>(),StringComparison.Ordinal),"display original body binding");
    }
}
else
{
    foreach(var label in new[]{"native-wrong-radius","native-wrong-finite-trim"})Require(geometry.Any(r=>r["label"]!.GetValue<string>()==label&&r["status"]!.GetValue<string>()=="Rejected"),"real wrong geometry remains rejected");
    Require(checks.Any(c=>c?["label"]?.GetValue<string>()=="wrong-size-material"&&c["agreementStatus"]?.GetValue<string>()=="Different"&&c["status"]?.GetValue<string>()=="fail"),"real material flip remains red");
    Require(mass.Any(r=>r["agreementStatus"]!.GetValue<string>()=="Unresolved"),"actual native integration uncertainty observed");
}
Console.WriteLine($"CAPTURE_CONTRACT_PASS mode={args[2]} mass_calls={mass.Length} same_call_csv_rows={rows.Count} comparison_calls={geometry.Length}; receipt verification, no additional modeling qualification");

string Relative(string path){Require(!Path.IsPathRooted(path),"project-relative path");var result=Path.GetFullPath(Path.Combine(root,path));Require(result.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal),"path inside project");return result;}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
