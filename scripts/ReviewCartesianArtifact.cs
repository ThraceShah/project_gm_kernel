using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var zipPath=Path.Combine(root,"temp_docs/incoming/mac-cartesian-ddd2311-quality.zip");
var destination=Path.Combine(root,"temp_docs/cartesian-independent-review/original");
if(args.Length==1)destination=Path.GetFullPath(Path.Combine(root,args[0]));
Require(new FileInfo(zipPath).Length==216112,"archive size");
Require(Hash(zipPath)=="f24b72186f221d91679b80d5f30a232951de7204492c01c396ac954ae8270adb","archive SHA256");
using var archive=ZipFile.OpenRead(zipPath);
var names=new HashSet<string>(StringComparer.Ordinal);
long bytes=0;
foreach(var entry in archive.Entries)
{
    var name=entry.FullName;
    Require(!Path.IsPathRooted(name)&&!name.Contains('\\')&&!name.Split('/').Any(x=>x is ".." or ".")&&!name.Contains(':'),"unsafe archive path");
    Require(((entry.ExternalAttributes>>16)&0xf000)!=0xa000,"archive symlink");
    Require(names.Add(name),"duplicate archive path");
    Require(Path.GetFullPath(Path.Combine(destination,name)).StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.Ordinal),"archive escape");
    bytes+=entry.Length;
}
Require(archive.Entries.Count==75&&bytes==1051242,"archive inventory");
Require(!Directory.Exists(destination),"fresh isolated destination required");
Directory.CreateDirectory(destination);
archive.ExtractToDirectory(destination);
using var receipt=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"docs/native-modeling-algorithm/cartesian-experiment-receipt.json")));
var count=0;
foreach(var item in receipt.RootElement.GetProperty("originalFiles").EnumerateArray())
{
    var path=item.GetProperty("path").GetString()!;
    var file=Path.Combine(destination,path);
    Require(File.Exists(file),"missing receipt file: "+path);
    Require(new FileInfo(file).Length==item.GetProperty("bytes").GetInt64(),"receipt size: "+path);
    Require(Hash(file)==item.GetProperty("sha256").GetString(),"receipt hash: "+path);
    count++;
}
Console.WriteLine($"archive verified: files={names.Count} uncompressedBytes={bytes} receiptFiles={count}");
foreach(var file in Directory.EnumerateFiles(destination,"*.exit",SearchOption.AllDirectories).Order())
    Console.WriteLine($"original exit {Path.GetRelativePath(destination,file)}: {File.ReadAllText(file).Trim()}");
foreach(var file in Directory.EnumerateFiles(destination,"*.trx",SearchOption.AllDirectories).Order())
{
    var doc=XDocument.Load(file);var counters=doc.Descendants().Single(x=>x.Name.LocalName=="Counters");
    Console.WriteLine($"original TRX {Path.GetRelativePath(destination,file)}: {string.Join(' ',counters.Attributes().Select(x=>$"{x.Name}={x.Value}"))}");
}
Console.WriteLine("PASS: integrity/extraction only; original operation exits retain their original meaning");
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
static string Source([CallerFilePath]string path="")=>path;
