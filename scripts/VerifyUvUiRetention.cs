#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

var scripts=Path.GetDirectoryName(ScriptPath())!;
var root=Path.GetFullPath("..",scripts);
try
{
    if(args.Length!=1||args[0].Length!=40||!args[0].All(char.IsAsciiHexDigit))throw new ArgumentException("Exact production candidate SHA required.");
    var candidate=args[0];const string toolsCommit="1b935972eff529e13b46cb9b08ad9550e0a6aeb5";
    var work=Path.Combine(root,"temp_docs/uv-static/two-round");
    if(Directory.Exists(work))throw new IOException("Use a fresh isolated work directory; preserve earlier evidence.");
    var dist=Path.Combine(root,"web/dist");var ui=Hashes(dist,true);
    var uv=ui.Where(p=>p.Key.StartsWith("display/real-uv/",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
    Require(uv.Count==15&&!Directory.Exists(Path.Combine(dist,"data/display/real-uv")),"15 UI-owned assets, no old dist/data overlay");
    var originalUv=Hashes(Path.Combine(root,"web/public/display/real-uv"));
    Require(originalUv.All(p=>uv["display/real-uv/"+p.Key]==p.Value),"public resource bytes match built UI");
    var producer=Path.GetFullPath("../../gm-web-focus-layout/temp_docs/ui-c710-review/data",scripts);
    var history=Hashes(producer);
    Require(history.Count==607&&history["index.json"]=="117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990"&&!history.Keys.Any(p=>p.StartsWith("display/",StringComparison.Ordinal)),"exact 607-file producer without overlay");
    // Read-only local 3c failure fixture, not a claim about the current served UI.
    var previous=Path.GetFullPath("../../gm-web-real-uv-review/temp_docs/fusion-retention-repro/release/work/bundle-c8f9f9a7b74bc2de",scripts);
    var previousHashes=Hashes(previous);
    Require(!previousHashes.Keys.Any(p=>p.StartsWith("data/display/",StringComparison.Ordinal)),"previous test fixture has no manual overlay");
    var toolDir=Path.Combine(work,"tools/scripts");Directory.CreateDirectory(toolDir);
    var toolHashes=new JsonObject();
    foreach(var name in new[]{"Directory.Build.props","FreezeReviewedWebProgressUi.cs","PublishReviewedWebProgressUi.cs","ConfigureReviewedWebProgressUiHook.cs","ReviewedWebProgressRelease.cs","WebProgressDataValidation.cs","WebProgressPublishing.cs","WebProgressMetadata.cs"})
    {
        var bytes=Git("show",toolsCommit+":scripts/"+name);File.WriteAllBytes(Path.Combine(toolDir,name),bytes);toolHashes[name]=Hash(bytes);
    }
    var firstData=Path.Combine(work,"producer-round1");var freshData=Path.Combine(work,"producer-round2");
    Copy(producer,firstData);Copy(producer,freshData);
    // Freeze rejects an output contained by its source checkout. Use a detached
    // exact Git snapshot and copy only the finished UI files into its dist.
    var uiSource=Path.Combine(root,"temp_docs/uv-static/ui-source");
    Require(!Directory.Exists(uiSource),"fresh detached UI source required");
    _=Git("worktree","add","--detach",Rel(scripts,uiSource),candidate);
    foreach(var (path,sha) in ui)
    {
        var target=Path.Combine(uiSource,"web/dist",path);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(dist,path),target);Require(Hash(File.ReadAllBytes(target))==sha,"immutable built UI copy");
    }
    var release=Path.Combine(work,"release");var uiHash=TreeHash(ui);var fixtureReview=new string('0',40);
    Run(toolDir,"FreezeReviewedWebProgressUi.cs","--tools-commit",toolsCommit,"--ui-commit",candidate,"--review",fixtureReview,
        "--dist-hash",uiHash,"--previous-tree-hash",TreeHash(previousHashes),"--ui-tree",Rel(toolDir,uiSource),"--previous-bundle",Rel(toolDir,previous),"--release",Rel(toolDir,release));
    var frozenUi=Hashes(Path.Combine(release,"ui"));
    Require(TreeHash(frozenUi)==uiHash&&frozenUi.Count==18&&uv.All(p=>frozenUi[p.Key]==p.Value),"actual 1b freeze retains all 18 non-data UI files");
    var frozenScripts=Path.Combine(release,"tools/scripts");
    foreach(var name in new[]{"PublishReviewedWebProgressUi.cs","ConfigureReviewedWebProgressUiHook.cs","ReviewedWebProgressRelease.cs"})Require(Hash(File.ReadAllBytes(Path.Combine(frozenScripts,name)))==toolHashes[name]!.GetValue<string>(),"frozen exact tool "+name);
    var rounds=new JsonArray();string? bundleHash=null;
    foreach(var (data,round) in new[]{(firstData,"round1"),(freshData,"round2")})
    {
        Require(Hashes(data).Count==607&&!Directory.Exists(Path.Combine(data,"display")),"fresh round input has no UV namespace");
        Run(frozenScripts,"PublishReviewedWebProgressUi.cs","--mode","prepare","--dist","../../ui","--data",Rel(frozenScripts,data),
            "--baseline-data","../../previous-static/data","--work","../../"+round,"--ui-commit",candidate,"--dist-hash",uiHash,"--review",fixtureReview);
        var prepared=JsonNode.Parse(File.ReadAllBytes(Path.Combine(release,round,"prepared.json")))!;
        var bundle=Path.GetFullPath(prepared["bundle"]!.GetValue<string>(),Path.Combine(release,round));var files=Hashes(bundle);
        Require(uv.All(p=>files.TryGetValue(p.Key,out var hash)&&hash==p.Value),"15/15 UI resources retained in "+round);
        Require(!files.Keys.Any(p=>p.StartsWith("data/display/",StringComparison.Ordinal)),"no duplicate legacy overlay in "+round);
        Require(history.All(p=>files["data/"+p.Key]==p.Value)&&TreeHash(Hashes(data))==TreeHash(history),"607 files and original index unchanged in "+round);
        var bytes=Directory.GetFiles(bundle,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length);
        Require(files.Count==625&&bytes<=30L*1024*1024&&bytes<=32L*1024*1024,"625-file complete bundle within 30/32 MiB");
        var hash=TreeHash(files);if(bundleHash!=null)Require(bundleHash==hash,"two freshly prepared full bundle hashes equal");bundleHash=hash;
        rounds.Add(new JsonObject{["name"]=round,["bundle"]=Rel(root,bundle),["bundleHash"]=hash,["files"]=files.Count,["bytes"]=bytes,["retainedUv"]=15,["producerFiles"]=607,["prepared"]=prepared.DeepClone()});
    }
    Require(TreeHash(Hashes(producer))==TreeHash(history)&&TreeHash(Hashes(previous))==TreeHash(previousHashes),"read-only original inputs unchanged");
    var report=new JsonObject{["candidate"]=candidate,["tools"]=toolsCommit,["fixtureReviewPin"]=fixtureReview,["published"]=false,["uiHash"]=uiHash,["uiFiles"]=frozenUi.Count,["historicalFiles"]=history.Count,["indexSha256"]=history["index.json"],["toolHashes"]=toolHashes,["rounds"]=rounds};
    File.WriteAllText(Path.Combine(work,"retention-report.json"),report.ToJsonString());
    Console.WriteLine($"RETENTION_PASS ui={uiHash} frozenFiles=18 round1=15/15 round2=15/15 history=607 fullBundle={bundleHash}");return 0;
}
catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Rel(string from,string to)=>Path.GetRelativePath(from,to).Replace('\\','/');
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static Dictionary<string,string> Hashes(string dir,bool uiOnly=false)=>Directory.GetFiles(dir,"*",SearchOption.AllDirectories).Select(f=>(File:f,Path:Rel(dir,f))).Where(p=>!uiOnly||!p.Path.StartsWith("data/",StringComparison.Ordinal)).ToDictionary(p=>p.Path,p=>Hash(File.ReadAllBytes(p.File)),StringComparer.Ordinal);
static string TreeHash(Dictionary<string,string> files){using var h=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);foreach(var (p,s)in files.OrderBy(p=>p.Key,StringComparer.Ordinal)){h.AppendData(Encoding.UTF8.GetBytes(p));h.AppendData(Encoding.UTF8.GetBytes(s));}return Convert.ToHexStringLower(h.GetHashAndReset());}
static void Require(bool ok,string name){if(!ok)throw new InvalidDataException(name);}
static void Copy(string source,string target){foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)){var output=Path.Combine(target,Rel(source,file));Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output);}}
static byte[] Git(params string[] args){var start=new ProcessStartInfo("git"){WorkingDirectory=Path.GetDirectoryName(ScriptPath())!,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)start.ArgumentList.Add(a);using var p=Process.Start(start)!;using var output=new MemoryStream();p.StandardOutput.BaseStream.CopyTo(output);p.WaitForExit();Require(p.ExitCode==0,"fixed Git source read");return output.ToArray();}
static void Run(string dir,string script,params string[] args){var start=new ProcessStartInfo("dotnet"){WorkingDirectory=dir};foreach(var a in new[]{"run",script,"--"})start.ArgumentList.Add(a);foreach(var a in args)start.ArgumentList.Add(a);start.Environment["MSBUILDDISABLENODEREUSE"]="1";Console.WriteLine("PREPARE_ENTRY "+Rel(Path.GetFullPath("..",Path.GetDirectoryName(ScriptPath())!),Path.Combine(dir,script))+" "+string.Join(' ',args));using var p=Process.Start(start)!;p.WaitForExit();Require(p.ExitCode==0,script+" exit="+p.ExitCode);}
