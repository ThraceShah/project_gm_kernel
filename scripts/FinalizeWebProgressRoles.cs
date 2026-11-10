#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property UseWebProgressValidation=true
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
if(!args.Contains("--unpublished"))throw new ArgumentException("Explicit --unpublished is required; this never rewrites distributed history.");
var scripts=Path.GetDirectoryName(ScriptPath())!;var data=Path.GetFullPath(Option("--data","../web/data"),scripts);var id=Option("--run-id","");
if(id.Length==0||id.Contains('/')||id.Contains('\\')||id is "." or "..")throw new ArgumentException("Safe exact run ID required");
var run=Path.Combine(data,"runs",id);var path=Path.Combine(run,"manifest.json");var manifest=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
if(manifest["run"]!["provenance"]!["roleCoverageFinalization"]!=null){Console.WriteLine("Already finalized roles");return 0;}
var originalManifest=Hash(File.ReadAllBytes(path));var seen=new HashSet<string>();var records=new JsonArray();
foreach(var c in manifest["cases"]!.AsArray())foreach(var step in c!["steps"]!.AsArray())foreach(var side in new[]{"gm","pk"})
{
    var references=new List<string>();if(step![side]!["after"] is JsonValue after)references.Add(after.GetValue<string>());
    foreach(var reference in step[side]?["afterBodies"]?.AsArray()??[])references.Add(reference!.GetValue<string>());
    foreach(var relative in references)
    {
        if(!seen.Add(relative))continue;var file=Path.GetFullPath(relative,run);if(!file.StartsWith(run+Path.DirectorySeparatorChar))throw new InvalidDataException("Escaping mesh path");
        var original=File.ReadAllBytes(file);var mesh=JsonNode.Parse(original)!.AsObject();var signature=GeometrySignature(mesh);
        foreach(var kind in new[]{"faces","edges"})foreach(var entity in mesh[kind]!.AsArray())if(entity!["role"]!.GetValue<string>()=="unchanged")entity["roleKnown"]=false;
        if(GeometrySignature(mesh)!=signature)throw new InvalidDataException("Geometry or IDs changed");
        var backup=Path.GetFullPath("../temp_docs/web-progress/roles-before/"+id+"/"+relative,scripts);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.WriteAllBytes(backup,original);
        File.WriteAllText(file,mesh.ToJsonString());records.Add((JsonNode)new JsonObject{["path"]=relative,["originalSha256"]=Hash(original),["finalSha256"]=Hash(File.ReadAllBytes(file)),["geometryAndIdsSha256"]=signature});
    }
}
var registry=JsonNode.Parse(File.ReadAllText(Path.Combine(run,"registry.json")))!.AsObject();WebProgressMetadata.Apply(manifest,registry);
manifest["run"]!["provenance"]!["roleCoverageFinalization"]=new JsonObject{["version"]=1,["appliedAt"]=DateTimeOffset.UtcNow.ToString("O"),["originalManifestSha256"]=originalManifest,["processorSha256"]=Hash(File.ReadAllBytes(ScriptPath())),["meshes"]=records,["note"]="Before first distribution, neutral output roles are explicitly unknown rather than claiming unchanged topology. Every original/final geometry-and-ID signature was equal; no position, normal, index, ID, selection, API tracking, check, status or numeric timing was changed."};
if(manifest["run"]!["provenance"]!["metadataFinalization"] is JsonObject prior)prior["note"]="Additive metadata labels; this first metadata pass preserved execution results. The separate roleCoverageFinalization records subsequent neutral-role qualification with unchanged geometry and IDs.";
File.WriteAllText(path,manifest.ToJsonString());var validation=WebProgressDataValidation.Validate(data);File.WriteAllText(Path.Combine(run,"validation.json"),validation.ToJsonString());Console.WriteLine(validation.ToJsonString());return validation["errors"]!.AsArray().Count==0?0:2;
string Option(string name,string fallback){var at=Array.IndexOf(args,name);return at<0?fallback:args[at+1];}
static string ScriptPath([CallerFilePath]string path="")=>path;
static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
static string GeometrySignature(JsonObject mesh)
{
    var copy=mesh.DeepClone();foreach(var kind in new[]{"faces","edges"})foreach(var entity in copy[kind]!.AsArray())entity!.AsObject().Remove("roleKnown");return Hash(Encoding.UTF8.GetBytes(copy.ToJsonString()));
}
