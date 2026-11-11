#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

// A finite render-only package. Never refreshes capture or changes the historical data index.
var scripts=Path.GetDirectoryName(ScriptPath())!;
try
{
    if(args.Length!=2||args[0]!="--source-run"||Path.IsPathRooted(args[1]))throw new ArgumentException("--source-run <run directory relative to scripts/>");
    var source=Path.GetFullPath(args[1],scripts);
    var directory=Path.Combine(source,"cases","solid-blend.plane-nurbs","sample-0","display");
    var output=Path.GetFullPath("../web/data/display/real-uv",scripts);
    if(Directory.Exists(output))throw new IOException("Display package already exists; choose a fresh reviewed generation, never overwrite evidence.");
    Directory.CreateDirectory(output);
    Directory.CreateDirectory(Path.Combine(output,"meshes"));
    Directory.CreateDirectory(Path.Combine(output,"display"));
    var items=new JsonArray();var refs=new Dictionary<string,string>();
    foreach(var receiptPath in Directory.GetFiles(directory,"*.receipt.json").Order())
    {
        var receipt=JsonNode.Parse(File.ReadAllBytes(receiptPath))!.AsObject();
        var kernel=receipt["kernel"]!.GetValue<string>();var stage=receipt["stage"]!.GetValue<string>();
        if(kernel is not ("gm" or "pk")||stage is not ("before" or "after")||!refs.TryAdd(kernel+"-"+stage,"meshes/"+kernel+"-"+stage+".mesh.json"))throw new InvalidDataException("Ambiguous receipt kernel/stage.");
        var isoName=kernel+"-"+stage+".iso.json";var isoPath=Path.Combine(output,isoName);
        var start=new ProcessStartInfo("dotnet"){WorkingDirectory=scripts,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{"run","--file","ExportWebIsoparametrics.cs","--","--snapshot",Path.GetRelativePath(scripts,receiptPath),"--output",Path.GetRelativePath(scripts,isoPath)})start.ArgumentList.Add(a);
        start.Environment["MSBUILDDISABLENODEREUSE"]="1";
        using var child=Process.Start(start)!;
        var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();child.WaitForExit();
        Console.Write(await stdout);Console.Error.Write(await stderr);
        if(child.ExitCode!=0)throw new InvalidDataException($"Export {kernel}/{stage} exit {child.ExitCode}; incomplete package must not be used.");
        var iso=JsonNode.Parse(File.ReadAllBytes(isoPath))!.AsObject();
        if(stage=="after"&&iso["faces"]!.AsArray().Count==0)throw new InvalidDataException("Both after snapshots require genuine nonplanar U/V.");
        var meshName=refs[kernel+"-"+stage];
        File.Copy(Path.GetFullPath(receipt["meshPath"]!.GetValue<string>(),directory),Path.Combine(output,meshName));
        var item=new JsonObject{["meshPath"]=meshName,["path"]=isoName,["sha256"]=Hash(File.ReadAllBytes(isoPath))};
        foreach(var key in new[]{"sourceRun","sourceSnapshot","kernel","caseId","sampleId","stepId","stage","sourceMeshSha256","inputXtSha256","snapshotReceiptSha256","executionSha256","producerCommit","captureCodeSha256","receiptCodeSha256"})item[key]=iso[key]!.DeepClone();
        // Preserve the exact signed mesh bytes, including their relative receipt link.
        // That public link resolves to an explicitly derived summary, never a raw XT dependency.
        var summary=item.DeepClone().AsObject();summary.Remove("path");summary.Remove("sha256");
        summary["schemaVersion"]=1;summary["type"]="render-only-receipt-summary";summary["rawXtPublished"]=false;
        summary["originalReceiptSha256"]=iso["snapshotReceiptSha256"]!.DeepClone();
        summary["meshPath"]="../"+meshName;summary["derivativePath"]="../"+isoName;summary["derivativeSha256"]=item["sha256"]!.DeepClone();
        Write("display/"+receipt["sourceSnapshot"]!.GetValue<string>()+".receipt.json",summary);
        items.Add(item);
        Console.WriteLine($"VERIFIED {kernel}/{stage} receipt={Path.GetFileName(receiptPath)} faces={iso["faces"]!.AsArray().Count} mesh={iso["sourceMeshSha256"]}");
    }
    if(refs.Count!=4)throw new InvalidDataException("Expected independent GM/PK before/after receipts.");
    var display=new JsonObject{["schemaVersion"]=1,["type"]="verified-render-only-display-manifest",["items"]=items,["scope"]="One captured operation; no new modeling acceptance or timing",["rawXtPublished"]=false};
    Write("display-manifest.json",display);
    var sourceManifest=JsonNode.Parse(File.ReadAllBytes(Path.Combine(source,"manifest.json")))!;
    var commit=sourceManifest["run"]!["commit"]!.GetValue<string>();
    var generated=DateTimeOffset.UtcNow.ToString("O");
    var run=new JsonObject{["id"]="real-uv-display",["commit"]=commit,["generatedAt"]=generated,["label"]="真实 U/V · 独立显示样例",["manifest"]="display/real-uv/manifest.json"};
    Write("index.json",new JsonObject{["schemaVersion"]=1,["generatedAt"]=generated,["latestRunId"]="real-uv-display",["runs"]=new JsonArray(run)});
    var testCase=new JsonObject
    {
        ["id"]="solid-blend.plane-nurbs",["title"]="Plane / NURBS · 真实 U/V 显示样例",["family"]="blend",["operation"]="真实曲面 U/V（仅显示）",["status"]="diagnostic",
        ["parameters"]=new JsonObject{["sample"]="sample-0",["operation"]="operation-1",["parameterHash"]=null},
        ["apis"]=new JsonObject{["gm"]=new JsonObject{["name"]="GM 导出 XT → PK receive → PK_SURF_eval",["parameters"]=new JsonObject()},["pk"]=new JsonObject{["name"]="PK XT → PK receive → PK_SURF_eval",["parameters"]=new JsonObject()}},
        ["steps"]=new JsonArray(new JsonObject{["id"]="operation-1",["title"]="过渡操作前 / 后（receipt.stage）",["gm"]=new JsonObject{["before"]=refs["gm-before"],["after"]=refs["gm-after"]},["pk"]=new JsonObject{["before"]=refs["pk-before"],["after"]=refs["pk-after"]}}),
        ["checks"]=new JsonArray(),["performance"]=new JsonObject{["gmMs"]=null,["pkMs"]=null,["ratioGmOverPk"]=null,["correctnessQualified"]=false,["note"]="Render-only derived display; no performance measurement or geometric pass is inferred."},
        ["source"]=new JsonObject{["path"]="scripts/WebProgressCapture.cs",["revision"]=commit},
        ["evidence"]=new JsonArray(new JsonObject{["label"]="显示来源 / 快照 / mesh、XT、receipt 全文 SHA",["path"]="display-manifest.json"}),
        ["notes"]=new JsonArray("显式独立显示源 r2-curved-receipt-eecab6d / sample-0 / operation-1；原 live17 数据与性能不变。","U（青）=常数 u；V（紫）=常数 v。仅精确矩形 trim 的非平面支持；未覆盖面 unavailable。","真实 GM 导出 XT 由 PK receive 仅供显示求值；原始 XT 不公开，必要 hash 与双精度曲线完整提供。","parameterHash 尚未独立核验，保持 null；receipt 记录值仅作来源诊断。")
    };
    Write("manifest.json",new JsonObject{["schemaVersion"]=1,["run"]=new JsonObject{["id"]="real-uv-display",["commit"]=commit,["generatedAt"]=generated,["pkVersion"]="38.0.150",["scope"]="render-only; one case, before/after; no benchmark or acceptance"},["cases"]=new JsonArray(testCase)});
    Console.WriteLine($"DISPLAY_PACKAGE bytes={Directory.GetFiles(output,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length)}; no raw XT, no live index changes");
    void Write(string name,JsonObject value)=>File.WriteAllText(Path.Combine(output,name),value.ToJsonString());
    return 0;
}
catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
