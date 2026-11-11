#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var actor=Path.GetFullPath("../gm-operation-recording-r2",root);
var cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"dotnet/runfile");
var output=Path.Combine(root,"temp_docs/operation-2c06-review");var inventory=new JsonArray();
var names=new[]{"FreeformModelingOracle","LocalTopologyChangeOracle","GeometryTransformOracle","ParasolidEvaluationOracle","ShortEdgeBlendOracle","ValidateWebProgress","ScopeProbe","ArtifactAudit","DiskProbe","FirstFailureProbe"};
string Hash(string p)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
foreach(var dir in Directory.GetDirectories(cache).Where(p=>names.Any(n=>Path.GetFileName(p).StartsWith(n+"-"))))
foreach(var pdb in Directory.Exists(Path.Combine(dir,"bin"))?Directory.GetFiles(Path.Combine(dir,"bin"),"*.pdb",SearchOption.AllDirectories).Where(p=>names.Contains(Path.GetFileNameWithoutExtension(p))||Path.GetFileName(dir).StartsWith("ShortEdgeBlendOracle-")&&Path.GetFileName(p)=="BooleanOperationOracle.pdb"):[])
{
    using var stream=File.OpenRead(pdb);using var provider=MetadataReaderProvider.FromPortablePdbStream(stream);var reader=provider.GetMetadataReader();
    var documents=new JsonArray();var relevant=false;
    foreach(var handle in reader.Documents)
    {
        var doc=reader.GetDocument(handle);var path=reader.GetString(doc.Name);
        var belongs=path.StartsWith(root+"/")||path.StartsWith(actor+"/");if(!belongs)continue;relevant=true;
        var checksum=Convert.ToHexStringLower(reader.GetBlobBytes(doc.Hash));
        var actual=File.Exists(path)?Hash(path):null;
        documents.Add((JsonNode)new JsonObject{["path"]=Path.GetRelativePath(root,path),["pdbChecksum"]=checksum,["currentBytesMatch"]=checksum==actual});
    }
    if(!relevant)continue;
    var dll=Path.ChangeExtension(pdb,".dll");using var peStream=File.OpenRead(dll);using var pe=new PEReader(peStream);
    var cv=pe.ReadDebugDirectory().Single(e=>e.Type==DebugDirectoryEntryType.CodeView);var codeview=pe.ReadCodeViewDebugDirectoryData(cv);
    var pdbId=new BlobContentId(reader.DebugMetadataHeader!.Id);
    var frozen=Path.Combine(output,"pdb-copies",Path.GetFileName(dir),Path.GetFileName(Path.GetDirectoryName(pdb))!);Directory.CreateDirectory(frozen);
    foreach(var source in new[]{pdb,dll})
    {var target=Path.Combine(frozen,Path.GetFileName(source));if(!File.Exists(target))File.Copy(source,target);if(Hash(target)!=Hash(source))throw new Exception("Cache changed: "+Path.GetFileName(source));}
    var item=new JsonObject{["cachePath"]=Path.GetRelativePath(cache,pdb),["pdbSha256"]=Hash(pdb),["dllSha256"]=Hash(dll),["pdbGuidMatchesDll"]=codeview.Guid==pdbId.Guid,["documents"]=documents};inventory.Add((JsonNode)item);
    Console.WriteLine(item.ToJsonString());
}
File.WriteAllText(Path.Combine(output,"pdb-inventory.json"),inventory.ToJsonString(new(){WriteIndented=true}));
Console.WriteLine("CACHE_PAIRS "+inventory.Count+"; read-only present cache provenance, not original process attestation");
static string Here([CallerFilePath]string p="")=>p;
