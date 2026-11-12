#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string target="ab0177ed2cfb6fab1b893c81562a0364fc487176";
const string output="temp_docs/independent-localjet-ab0177e";
if(args.SequenceEqual(new[]{"--extract"}))
{
    foreach(var item in new[]{("quality","ec50959c2ec008d5eb2f120d4b13cc8854e812d7c68f61262827be1451df0484",20579749L,531,78700033L),("controlled-original","f0d743e7a0f05147938d9ea8ddd12e03b0451df427c6de56615166493b3ae55c",33988439L,1037,137703906L)})
    {
        var archive="temp_docs/incoming/mac-localjet-ab0177e-"+item.Item1+".zip";var destination=output+"/"+item.Item1+"/original";
        Require(!Directory.Exists(destination),"preserve original extraction");Require(new FileInfo(archive).Length==item.Item3&&Hash(archive)==item.Item2,"archive bytes");
        using var zip=ZipFile.OpenRead(archive);var names=new HashSet<string>(StringComparer.Ordinal);var members=new JsonArray();long expanded=0;
        foreach(var entry in zip.Entries)
        {
            var name=entry.FullName;Require(!Path.IsPathRooted(name)&&!name.Contains('\\')&&!name.Contains(':')&&!name.Split('/').Any(x=>x is ".." or ".")&&names.Add(name)&&((entry.ExternalAttributes>>16)&0xf000)!=0xa000,"safe unique member");
            var path=Path.Combine(destination,name);Require(Path.GetFullPath(path).StartsWith(Path.GetFullPath(destination)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"isolated member");
            if(name.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);using(var source=entry.Open())using(var file=new FileStream(path,FileMode.CreateNew))source.CopyTo(file);
            using var check=entry.Open();var sha=Convert.ToHexStringLower(SHA256.HashData(check));Require(new FileInfo(path).Length==entry.Length&&Hash(path)==sha,"extracted member bytes");expanded+=entry.Length;
            members.Add((JsonNode)new JsonObject{["path"]=name,["bytes"]=entry.Length,["sha256"]=sha});
        }
        Require(names.Count==item.Item4&&expanded==item.Item5,"archive dimensions");Write(output+"/"+item.Item1+"/extraction.json",new JsonObject{["archive"]=archive,["sha256"]=Hash(archive),["entries"]=names.Count,["expandedBytes"]=expanded,["members"]=members});
        Console.WriteLine($"{item.Item1}: {names.Count} members / {expanded} bytes verified");
    }
    return;
}
Require(args.SequenceEqual(new[]{"--quality"}),"--extract or --quality");
{
var original=output+"/quality/original/";var folder=original+"temp_docs/local-spline-jet/";
foreach(var file in new[]{"docs/Local_Spline_Jet_Work.md","docs/Local_Spline_Jet_Quality.md","docs/local-spline-jet/quality-receipt.json","docs/local-spline-jet/source-binary-binding.json"})Require(File.ReadAllBytes(original+file).SequenceEqual(Git("show","85a595edbdd266e9a8b593086bd5535d9e16f375:"+file)),"fixed public document bytes "+file);
var binding=Read(original+"docs/local-spline-jet/source-binary-binding.json");var binaries=new JsonArray();
foreach(var entry in binding["binaries"]!.AsArray())binaries.Add((JsonNode)Binary(original+(string)entry!["dll"]!,original+(string)entry["pdb"]!,target,entry));
var claimed=Read(original+"docs/local-spline-jet/quality-receipt.json");var runs=new JsonArray();var strict=0;var mass=0;
foreach(var run in claimed["qualityRuns"]!.AsArray())
{
    var directory=(string)run!["directory"]!;var path=folder+directory;var capture=Read(path+"/capture.json");Require(Hash(path+"/capture.json")== (string?)run["captureSha256"],"quality capture hash");
    var exitPath=File.Exists(path+"/process.exit")?path+"/process.exit":folder+directory[..^8]+".exit";var raw=int.Parse(File.ReadAllText(exitPath));Require(raw==(int)run["originalExit"]!,"raw quality exit");
    Require(capture["captureErrors"]!.AsArray().Count==0,"capture errors");var counts=new JsonObject();foreach(var g in capture["checks"]!.AsArray().GroupBy(x=>(string)x!["status"]!)){counts[g.Key]=g.Count();Require(g.Key!="fail"&&(int)run["checks"]![g.Key]! ==g.Count(),"original check counts");}
    var id=Read(path+"/identity/loaded.json");Require((string?)id["calculationCommit"]==target,"quality actual source");
    foreach(var kind in new[]{"kernel","oracle"}){Require(Hash(path+"/identity/"+kind+".dll")== (string?)id[kind+"DllSha256"]&&Hash(path+"/identity/"+kind+".pdb")== (string?)id[kind+"PdbSha256"],"quality loaded bytes");Require(id.AsObject().Count(x=>x.Value is JsonValue value&&value.TryGetValue<string>(out var text)&&text==(string?)id[kind+"DllSha256"])>=2,"actual loaded hash witness");}
    if(File.Exists(path+"/source.sha256"))foreach(var line in File.ReadLines(path+"/source.sha256"))Require(HashBytes(Git("show",target+":"+line[66..]))==line[..64],"quality source manifest");
    foreach(var meshPath in Directory.GetFiles(path+"/meshes","*.json")){var source=Read(meshPath)["source"]!;if(source["xtArtifact"] is JsonValue artifact)Require(Hash(path+"/"+artifact.GetValue<string>())==(string?)source["xtSha256"]&&(string?)source["commit"]==target,"quality real product source");}
    foreach(var proofPath in Directory.GetFiles(path+"/evidence","quality-*.json"))
    {var proof=Read(proofPath);if(Path.GetFileName(proofPath).StartsWith("quality-compare",StringComparison.Ordinal)){var mask=proof["mask"]!;Require((string?)proof["status"]=="Verified"&&(int)mask["max_diffs"]!>0&&!(bool)mask["all_tests"]!&&!(bool)mask["acc_dev_tests"]!&&(bool)mask["tol_dev_tests"]!,"strict comparison mask/status");strict++;}else mass++;}
    runs.Add((JsonNode)new JsonObject{["directory"]=directory,["rawExit"]=raw,["checks"]=counts,["captureSha256"]=Hash(path+"/capture.json"),["steps"]=capture["steps"]!.AsArray().Count});
}
var trx=Directory.GetFiles(folder,"*.trx",SearchOption.AllDirectories).Single(x=>Hash(x)==(string?)claimed["fullSuite"]!["trxSha256"]);XNamespace ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";var counters=XDocument.Load(trx).Descendants(ns+"Counters").Single();Require((string?)counters.Attribute("total")=="2031"&&(string?)counters.Attribute("executed")=="2031"&&(string?)counters.Attribute("passed")=="2031"&&(string?)counters.Attribute("failed")=="0"&&(string?)counters.Attribute("notExecuted")=="0","full suite counters");
var receipt=new JsonObject{["scope"]="Independent original quality audit, no Mac code loading and no numerical or performance upgrade",["calculationCommit"]=target,["binaries"]=binaries,["runs"]=runs,["strictReports"]=strict,["massReports"]=mass,["trxSha256"]=Hash(trx),["fullSuiteTotal"]=2031};Write(output+"/quality/audit.json",receipt);Console.WriteLine($"Quality verified: {binaries.Count} PE/PDB, {runs.Count} groups, 2031 original tests, strict={strict}, mass={mass}");
}

static JsonObject Binary(string dll,string pdb,string commit,JsonNode? claim=null)
{
    using var pe=new PEReader(File.OpenRead(dll));var metadata=pe.GetMetadataReader();var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString();
    using var provider=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdb));var reader=provider.GetMetadataReader();var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));Require(new Guid(reader.DebugMetadataHeader!.Id.AsSpan(0,16))==cv.Guid,"PE/PDB link");
    var matched=0;var other=new JsonArray();foreach(var handle in reader.Documents){var doc=reader.GetDocument(handle);var name=reader.GetString(doc.Name).Replace('\\','/');var start=name.IndexOf("/src/",StringComparison.Ordinal);if(start<0)start=name.IndexOf("/tests/",StringComparison.Ordinal);if(start<0)start=name.IndexOf("/scripts/",StringComparison.Ordinal);var relative=start<0?"":name[(start+1)..];if(relative.Length==0||relative.Contains("/obj/",StringComparison.Ordinal)){other.Add((JsonNode?)JsonValue.Create(Path.GetFileName(name)));continue;}Require(reader.GetGuid(doc.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")&&SHA256.HashData(Git("show",commit+":"+relative)).SequenceEqual(reader.GetBlobBytes(doc.Hash)),"tracked PDB source "+relative);matched++;}
    if(claim!=null)Require(Hash(dll)==(string?)claim["dllSha256"]&&Hash(pdb)==(string?)claim["pdbSha256"]&&mvid==(string?)claim["mvid"]&&matched==(int)claim["matchedTrackedDocuments"]!,"binary receipt");
    return new JsonObject{["dll"]=dll,["dllSha256"]=Hash(dll),["pdbSha256"]=Hash(pdb),["mvid"]=mvid,["matchedTrackedDocuments"]=matched,["unmapped"]=other};
}
static JsonNode Read(string file)=>JsonNode.Parse(File.ReadAllText(file))!;
static string Hash(string file){using var stream=File.OpenRead(file);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static string HashBytes(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Write(string path,JsonNode value){Require(!File.Exists(path),"preserve audit receipt");Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,value.ToJsonString(new(){WriteIndented=true}));}
static byte[] Git(params string[] argv){var info=new ProcessStartInfo("git"){RedirectStandardOutput=true};foreach(var arg in argv)info.ArgumentList.Add(arg);using var child=Process.Start(info)!;using var memory=new MemoryStream();child.StandardOutput.BaseStream.CopyTo(memory);child.WaitForExit();Require(child.ExitCode==0,"fixed Git read");return memory.ToArray();}
static string Source([CallerFilePath]string path="")=>path;
