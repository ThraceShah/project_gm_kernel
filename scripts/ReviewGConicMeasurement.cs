#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var archive=Path.Combine(root,"temp_docs/incoming/mac-6616-gconic-controlled-original.zip");
var output=Path.Combine(root,"temp_docs/gconic-measurement-review");
var originals=Path.Combine(output,"originals");
Require(new FileInfo(archive).Length==31524200&&Hash(archive)=="acf7b872cc941708eda0d6b87e45da97d00336adf5167dd2c3fed1e7dee53e0e","original archive");
using(var zip=ZipFile.OpenRead(archive))
{
    var names=new HashSet<string>(StringComparer.Ordinal);long size=0;
    foreach(var entry in zip.Entries)
    {
        Require(!Path.IsPathRooted(entry.FullName)&&!entry.FullName.Contains('\\')&&entry.FullName.Split('/').All(x=>x is not (".." or ".")),"safe path");
        Require(((entry.ExternalAttributes>>16)&0xf000)!=0xa000&&names.Add(entry.FullName),"unique non-symlink entry");size+=entry.Length;
    }
    Require(zip.Entries.Count==665&&size==131859212,"archive inventory");
    Directory.CreateDirectory(originals);
    foreach(var entry in zip.Entries)
    {
        var path=Path.Combine(originals,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(File.Exists(path)){using var stream=entry.Open();Require(Hash(path)==Convert.ToHexStringLower(SHA256.HashData(stream)),"previous extraction untouched");}
        else entry.ExtractToFile(path,false);
    }
    Console.WriteLine($"ARCHIVE members={names.Count} expandedBytes={size} hash={Hash(archive)} safe=true");
}
foreach(var name in new[]{"docs/Pk_Native_GConic_Controlled_Measurement.md","docs/native-modeling-gconic/operation-summary.json","docs/native-modeling-gconic/measurement-receipt.json","docs/native-modeling-gconic/material-binding.json","docs/native-modeling-gconic/loaded-identity.json"})
{
    Require(GitBytes("217fc6f811e06cbeb936753f77bd65ae4156d8e8:"+name).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(originals,name))),"fixed Git original "+name);
    Console.WriteLine($"FIXED {name} hash={Hash(Path.Combine(originals,name))}");
}
Console.WriteLine("PASS archive integrity and fixed delivery documents; no original operation executed");

byte[] GitBytes(string spec)
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,UseShellExecute=false};info.ArgumentList.Add("show");info.ArgumentList.Add(spec);
    using var process=Process.Start(info)!;using var memory=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(memory);process.WaitForExit();Require(process.ExitCode==0,"git show");return memory.ToArray();
}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(file));}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
