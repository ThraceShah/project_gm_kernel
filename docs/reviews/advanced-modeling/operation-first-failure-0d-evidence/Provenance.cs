#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);var output=Path.Combine(root,"temp_docs/operation-first-failure-0d-review");
var cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"dotnet/runfile");var pairs=new JsonArray();
string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
foreach(var directory in Directory.GetDirectories(cache).Where(p=>new[]{"FirstFailureProbe-","Controls-","ValidateWebProgress-"}.Any(prefix=>Path.GetFileName(p).StartsWith(prefix))))
foreach(var pdb in Directory.Exists(Path.Combine(directory,"bin"))?Directory.GetFiles(Path.Combine(directory,"bin"),"*.pdb",SearchOption.AllDirectories).Where(p=>new[]{"FirstFailureProbe.pdb","Controls.pdb","ValidateWebProgress.pdb"}.Contains(Path.GetFileName(p))):[])
{
    using var stream=File.OpenRead(pdb);using var provider=MetadataReaderProvider.FromPortablePdbStream(stream);var reader=provider.GetMetadataReader();
    var docs=new JsonArray();var relevant=false;
    foreach(var handle in reader.Documents)
    {
        var document=reader.GetDocument(handle);var path=reader.GetString(document.Name);
        if(path.StartsWith(output+"/")||path==Path.Combine(root,"docs/reviews/advanced-modeling/operation-first-failure-0d-evidence/Controls.cs")||path==Path.Combine(root,"scripts/ValidateWebProgress.cs"))relevant=true;
        if(!path.StartsWith(root+"/"))continue;
        var checksum=Convert.ToHexStringLower(reader.GetBlobBytes(document.Hash));docs.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(root,path),["checksum"]=checksum,["matchesCurrentBytes"]=File.Exists(path)&&Hash(path)==checksum});
    }
    if(!relevant)continue;
    var dll=Path.ChangeExtension(pdb,".dll");using var peStream=File.OpenRead(dll);using var pe=new PEReader(peStream);var entry=pe.ReadDebugDirectory().Single(e=>e.Type==DebugDirectoryEntryType.CodeView);
    var matches=pe.ReadCodeViewDebugDirectoryData(entry).Guid==new BlobContentId(reader.DebugMetadataHeader!.Id).Guid;
    if(!matches||docs.Any(d=>!d!["matchesCurrentBytes"]!.GetValue<bool>()))throw new Exception("Provenance mismatch: "+Path.GetFileName(directory));
    var frozen=Path.Combine(output,"pdb-copies",Path.GetFileName(directory),Path.GetFileName(Path.GetDirectoryName(pdb))!);Directory.CreateDirectory(frozen);
    File.Copy(pdb,Path.Combine(frozen,Path.GetFileName(pdb)),false);File.Copy(dll,Path.Combine(frozen,Path.GetFileName(dll)),false);
    pairs.Add((JsonNode)new JsonObject{["cachePath"]=Path.GetRelativePath(cache,pdb),["dllSha256"]=Hash(dll),["pdbSha256"]=Hash(pdb),["guidMatches"]=matches,["documents"]=docs});
}
var inventory=new JsonArray();
foreach(var file in Directory.GetFiles(output,"*",SearchOption.AllDirectories).Where(p=>!p.Contains("/pdb-copies/")).Order())inventory.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(output,file),["sha256"]=Hash(file)});
File.WriteAllText(Path.Combine(output,"pdb-inventory.json"),pairs.ToJsonString(new(){WriteIndented=true}));File.WriteAllText(Path.Combine(output,"artifact-inventory.json"),inventory.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine("PAIRS "+pairs.Count+" FILES "+inventory.Count+"; exact managed source checksums and PE/PDB GUID verified, frozen binaries copied");
if(pairs.Count!=4)throw new Exception("Expected before/after original probe, Controls, and current public validator");
static string Here([CallerFilePath]string p="")=>p;
