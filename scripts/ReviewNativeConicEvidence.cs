#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

// Read-only audit of the delivered originals; extraction never overwrites files.
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var archive=Path.Combine(root,"temp_docs/incoming/mac-conic-b47-quality.zip");
Require(new FileInfo(archive).Length==6810440,"archive bytes");
Require(Hash(archive)=="b83b3615bb6a88d21326c7fb629bd2a36e92d36f5c860177426eb9c7d44c81ee","archive SHA256");
var destination=Path.Combine(root,"temp_docs/conic-independent-review/originals");
using(var zip=ZipFile.OpenRead(archive))
{
    var names=new HashSet<string>(StringComparer.Ordinal);long bytes=0;
    foreach(var member in zip.Entries)
    {
        Require(!member.FullName.Contains('\\')&&!Path.IsPathRooted(member.FullName)&&member.FullName.Split('/').All(p=>p is not (".." or ".")),"safe member path");
        Require(((member.ExternalAttributes>>16)&0xF000)!=0xA000,"no symlink");
        Require(names.Add(member.FullName),"unique member");bytes+=member.Length;
    }
    Require(zip.Entries.Count==167&&bytes==24698347,"archive member count/expanded bytes");
    Console.WriteLine($"archive sha256={Hash(archive)} members={zip.Entries.Count} expandedBytes={bytes} safe=true");
    Directory.CreateDirectory(destination);
    foreach(var member in zip.Entries)
    {
        var path=Path.Combine(destination,member.FullName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(File.Exists(path))
        {
            using var stream=member.Open();
            Require(Hash(path)==Convert.ToHexStringLower(SHA256.HashData(stream)),"existing extraction unchanged");
        }
        else member.ExtractToFile(path,false);
    }
}
var receipt=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"docs/native-modeling-algorithm/conic-delivery-receipt.json"))).RootElement;
Require(Hash(Path.Combine(destination,"docs/native-modeling-algorithm/conic-delivery-receipt.json"))==Hash(Path.Combine(root,"docs/native-modeling-algorithm/conic-delivery-receipt.json")),"packaged receipt matches fixed receipt");
foreach(var (name,paths) in new (string,string[])[]{("conic-b47-source.diff",["scripts/ShortEdgeBlendOracle.cs","src","tests"]),("conic-quadratic-elimination-runner.diff",["scripts/ShortEdgeBlendOracle.cs"]),("conic-baseline-g-runner.diff",["scripts/ShortEdgeBlendOracle.cs"])})
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,UseShellExecute=false};
    foreach(var argument in new[]{"diff","94f60da64836da33eafaea7ce5912c9d5132660e..b47e25ec90f99385f9d2ce86ec88029bb4143e00","--"}.Concat(paths))info.ArgumentList.Add(argument);
    using var process=Process.Start(info)!;var diff=process.StandardOutput.ReadToEnd();process.WaitForExit();Require(process.ExitCode==0,"git diff");
    Require(diff==File.ReadAllText(Path.Combine(destination,"temp_docs/pk-native-algorithm-work",name)),"fixed source diff "+name);
    Console.WriteLine($"fixedDiff {name} matches=true");
}
var checkedFiles=0;var absent=new List<string>();
foreach(var item in receipt.GetProperty("originalFiles").EnumerateArray())
{
    var name=item.GetProperty("path").GetString()!;var path=Path.Combine(destination,name);
    if(!File.Exists(path)){absent.Add(name);continue;}
    Require(new FileInfo(path).Length==item.GetProperty("bytes").GetInt64(),"original size "+name);
    Require(Hash(path)==item.GetProperty("sha256").GetString(),"original hash "+name);checkedFiles++;
}
Console.WriteLine($"receipt verifiedFiles={checkedFiles} absentCount={absent.Count} absent={string.Join(',',absent)}");
Require(absent.Count==0,"receipt coverage");
foreach(var item in receipt.GetProperty("runtimeIdentities").EnumerateArray())
{
    var binary=Path.Combine(destination,item.GetProperty("binary").GetString()!);
    var expected=$"mvid={item.GetProperty("mvid").GetString()} bytes={new FileInfo(binary).Length} sha256={Hash(binary)}";
    Require(Mvid(binary)==item.GetProperty("mvid").GetString(),"runtime PE MVID");
    var lines=File.ReadAllLines(Path.Combine(destination,item.GetProperty("log").GetString()!)).Where(x=>x.StartsWith("KERNEL_IDENTITY ",StringComparison.Ordinal)).ToArray();
    Require(lines.Length==2&&lines[0]=="KERNEL_IDENTITY phase=start "+expected&&lines[1]=="KERNEL_IDENTITY phase=end "+expected,"per-process identity");
    Console.WriteLine($"runtime {item.GetProperty("log").GetString()} {expected} startEndMatch=true");
}
foreach(var item in receipt.GetProperty("suite").GetProperty("beforeBinaries").EnumerateArray())
{
    var name=Path.GetFileName(item.GetProperty("path").GetString()!);
    var binary=Path.Combine(destination,"temp_docs/pk-native-algorithm-work/conic-b47-full-"+name);
    Require(Mvid(binary)==item.GetProperty("mvid").GetString(),"suite PE MVID "+name);
    Require(Hash(binary)==item.GetProperty("sha256").GetString(),"suite PE hash "+name);
    var after=receipt.GetProperty("suite").GetProperty("afterBinaries").EnumerateArray().Single(e=>Path.GetFileName(e.GetProperty("path").GetString())==name);
    Require(Hash(binary)==after.GetProperty("sha256").GetString(),"suite end hash "+name);
    Console.WriteLine($"suiteBinary {name} mvid={Mvid(binary)} bytes={new FileInfo(binary).Length} sha256={Hash(binary)}");
}
Console.WriteLine($"archivedPdbCount={Directory.EnumerateFiles(destination,"*.pdb",SearchOption.AllDirectories).Count()} (PE identity is not an independent source-to-binary reproduction)");
var trx=XDocument.Load(Path.Combine(destination,"temp_docs/pk-native-algorithm-work/test-results/conic-b47-full.trx"));
var counters=trx.Descendants().Single(e=>e.Name.LocalName=="Counters");
Require((string?)counters.Attribute("total")=="1944"&&(string?)counters.Attribute("passed")=="1942"&&(string?)counters.Attribute("failed")=="2","TRX totals");
Console.WriteLine("suiteCounters "+counters.ToString(SaveOptions.DisableFormatting));
foreach(var result in trx.Descendants().Where(e=>e.Name.LocalName=="UnitTestResult"&&(string?)e.Attribute("outcome")=="Failed"))
    Console.WriteLine("suiteFailure "+result.Attribute("testName")+" "+result.Descendants().Single(e=>e.Name.LocalName=="Message").Value.Replace('\n',' '));
foreach(var suffix in new[]{"build","test","driver"})
{
    var name="temp_docs/pk-native-algorithm-work/conic-b47-full-"+suffix+".exit";
    Console.WriteLine($"originalExit {name}={File.ReadAllText(Path.Combine(destination,name)).Trim()}");
}
Console.WriteLine("PASS original evidence integrity; this process does not rerun original operations");

static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static string Mvid(string path){using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();return metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString();}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
