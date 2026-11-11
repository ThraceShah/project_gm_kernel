#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var scripts=Path.GetDirectoryName(ScriptPath())!;
try
{
    if(args.Length!=2||args[0]!="--source-run"||Path.IsPathRooted(args[1]))throw new ArgumentException("--source-run <run directory relative to scripts/>");
    var source=Path.GetFullPath(args[1],scripts);
    var package=Path.GetFullPath("../web/data/display/real-uv",scripts);
    var items=JsonNode.Parse(File.ReadAllBytes(Path.Combine(package,"display-manifest.json")))!["items"]!.AsArray();
    foreach(var item in items)
    {
        var mesh=File.ReadAllBytes(Path.Combine(package,item!["meshPath"]!.GetValue<string>()));
        var iso=File.ReadAllBytes(Path.Combine(package,item["path"]!.GetValue<string>()));
        if(Hash(mesh)!=item["sourceMeshSha256"]!.GetValue<string>()||Hash(iso)!=item["sha256"]!.GetValue<string>())throw new InvalidDataException("Package byte SHA mismatch.");
        var receiptPath=Path.Combine(source,"cases","solid-blend.plane-nurbs","sample-0","display",item["sourceSnapshot"]!.GetValue<string>()+".receipt.json");
        if(Hash(File.ReadAllBytes(receiptPath))!=item["snapshotReceiptSha256"]!.GetValue<string>())throw new InvalidDataException("Original receipt SHA mismatch.");
    }
    Console.WriteLine("PASS four exact original mesh/receipt and generated ISO SHA bindings");
    var work=Path.GetFullPath("../temp_docs/real-uv/negative-"+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),scripts);
    var leaf=Path.Combine("cases","solid-blend.plane-nurbs","sample-0");
    foreach(var relative in new[]{"manifest.json",Path.Combine(leaf,"execution.json"),Path.Combine(leaf,"display","snapshot-3.receipt.json"),Path.Combine(leaf,"display","snapshot-3.x_t"),Path.Combine(leaf,"meshes","gm-0-after-0.json")})
    {
        var target=Path.Combine(work,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(source,relative),target);
    }
    var receiptFile=Path.Combine(work,leaf,"display","snapshot-3.receipt.json");
    var executionFile=Path.Combine(work,leaf,"execution.json");
    var originalReceipt=File.ReadAllBytes(receiptFile);var originalExecution=File.ReadAllBytes(executionFile);
    foreach(var test in new[]{"mesh SHA","source hash","face identifier"})
    {
        var receipt=JsonNode.Parse(originalReceipt)!;var execution=JsonNode.Parse(originalExecution)!;
        if(test=="mesh SHA")receipt["meshSha256"]=new string('0',64);
        if(test=="source hash")execution["sourceHashes"]!["scripts/WebProgressCapture.cs"]=new string('0',64);
        if(test=="face identifier")receipt["faceBindings"]![0]!["xtFaceIdentifier"]=-1;
        File.WriteAllText(receiptFile,receipt.ToJsonString());File.WriteAllText(executionFile,execution.ToJsonString());
        var output=Path.Combine(work,test.Replace(' ','-')+".iso.json");
        var start=new ProcessStartInfo("dotnet"){WorkingDirectory=scripts,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{"run","--file","ExportWebIsoparametrics.cs","--","--snapshot",Path.GetRelativePath(scripts,receiptFile),"--output",Path.GetRelativePath(scripts,output)})start.ArgumentList.Add(a);
        start.Environment["MSBUILDDISABLENODEREUSE"]="1";
        using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();child.WaitForExit();
        var diagnostic=(await stderr).Split('\n',StringSplitOptions.RemoveEmptyEntries).LastOrDefault();_ = await stdout;
        if(child.ExitCode==0||File.Exists(output))throw new InvalidDataException("Negative control accepted: "+test);
        Console.WriteLine($"PASS reject {test}: exit={child.ExitCode}; {diagnostic}");
    }
    return 0;
}
catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
