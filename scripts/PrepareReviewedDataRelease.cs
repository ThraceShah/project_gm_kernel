#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

// Prepare a new frozen owned release directory for a data-snapshot publish:
// tools/ and ui/ are byte copies of the fixed accepted release, previous-static
// is the current served bundle (the rollback target), and release.json pins the
// full content manifest. tools/, ui/, previous-static/ and release.json are
// frozen read-only, matching ReviewedWebProgressRelease.Frozen.
var scripts=Path.GetDirectoryName(ScriptPath())!;
var sourceRelease=Path.GetFullPath(Option("--source-release",""),scripts);
var servedBundle=Path.GetFullPath(Option("--served-bundle",""),scripts);
var output=Path.GetFullPath(Option("--output",""),scripts);
if(!Directory.Exists(sourceRelease)||!Directory.Exists(servedBundle))throw new ArgumentException("--source-release and --served-bundle must exist");
if(Directory.Exists(output))throw new IOException("output release dir already exists");
string Sha(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
foreach(var dir in new[]{"tools","ui"})
{
    Copy(Path.Combine(sourceRelease,dir),Path.Combine(output,dir));
}
Copy(servedBundle,Path.Combine(output,"previous-static"));
var files=new JsonObject();
foreach(var dir in new[]{"tools","ui","previous-static"})
foreach(var entry in Directory.GetFiles(Path.Combine(output,dir),"*",SearchOption.AllDirectories).OrderBy(f=>f,StringComparer.Ordinal))
{
    var relative=Path.GetRelativePath(Path.Combine(output,dir),entry).Replace('\\','/');
    files[dir+"/"+relative]=Sha(File.ReadAllBytes(entry));
}
string TreeHash(string dir)
{
    using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach(var entry in Directory.GetFiles(Path.Combine(output,dir),"*",SearchOption.AllDirectories).Select(f=>(Relative:Path.GetRelativePath(Path.Combine(output,dir),f).Replace('\\','/'),File:f)).OrderBy(p=>p.Relative,StringComparer.Ordinal))
    {hash.AppendData(Encoding.UTF8.GetBytes(entry.Relative));hash.AppendData(Encoding.UTF8.GetBytes(Sha(File.ReadAllBytes(entry.File))));}
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}
var uiHash=TreeHash("ui");
var oldBundleHash=TreeHash("previous-static");
var release=new JsonObject{["schemaVersion"]=1,["toolsCommit"]="1b935972eff529e13b46cb9b08ad9550e0a6aeb5",["uiCommit"]="015cd308e147356fecac96bb7354ab1e92c36a03",["review"]="f9ed821287c7bfb4e23963a10238c9de32a3854c",["uiHash"]=uiHash,["oldBundleHash"]=oldBundleHash,["files"]=files};
var releasePath=Path.Combine(output,"release.json");
File.WriteAllText(releasePath,release.ToJsonString());
Directory.CreateDirectory(Path.Combine(output,"work"));
foreach(var root in new[]{"tools","ui","previous-static"})
{
    var path=Path.Combine(output,root);
    foreach(var entry in Directory.GetFiles(path,"*",SearchOption.AllDirectories).Prepend(path).Concat(Directory.GetDirectories(path,"*",SearchOption.AllDirectories)))
        File.SetUnixFileMode(entry,File.GetUnixFileMode(entry)&~(UnixFileMode.UserWrite|UnixFileMode.GroupWrite|UnixFileMode.OtherWrite));
}
File.SetUnixFileMode(releasePath,File.GetUnixFileMode(releasePath)&~(UnixFileMode.UserWrite|UnixFileMode.GroupWrite|UnixFileMode.OtherWrite));
Console.WriteLine("RELEASE "+output);
Console.WriteLine("uiHash "+uiHash);
Console.WriteLine("oldBundleHash "+oldBundleHash);
Console.WriteLine("files "+files.Count);
Console.WriteLine("releaseSha256 "+Sha(File.ReadAllBytes(releasePath)));
return 0;
static void Copy(string source,string target)
{
    Directory.CreateDirectory(target);
    foreach(var dir in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))Directory.CreateDirectory(Path.Combine(target,Path.GetRelativePath(source,dir)));
    foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))File.Copy(file,Path.Combine(target,Path.GetRelativePath(source,file)));
}
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:at+1<args.Length?args[at+1]:throw new ArgumentException(name+" requires a value");}
static string ScriptPath([CallerFilePath]string path="")=>path;
