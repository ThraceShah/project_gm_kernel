#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
if(args.Length!=4)throw new ArgumentException("source SHA, project-relative binary directory, project-relative evidence specification, new output name");
var sha=args[0];var binaryDirectory=Full(args[1]);var spec=JsonNode.Parse(File.ReadAllBytes(Full(args[2])))!.AsObject();var name=args[3];
if(sha.Length!=40||sha.Any(c=>!char.IsAsciiHexDigit(c))||Git("rev-parse",sha).Trim()!=sha)throw new ArgumentException("one fixed full source commit");
if(name.Length==0||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('-' or '_')))throw new ArgumentException("one output directory name");
var output=Full(Path.Combine("temp_docs","native-system-delivery",name));var archive=output+".zip";
if(Directory.Exists(output)||File.Exists(archive))throw new InvalidOperationException("Existing delivery bytes are immutable");
Directory.CreateDirectory(output);var sourceArchive=Path.Combine(output,"source.zip");Git("archive","--format=zip","--output="+sourceArchive,sha);
var manifest=new JsonObject{["sourceCommit"]=sha,["PKToyCommit"]=Git("ls-tree",sha,"third_party/PKToy").Trim().Split(' ')[2].Split('\t')[0],
    ["sourceArchive"]=Identity(sourceArchive),["independentlyAccepted"]=false,["performanceQualified"]=false,["executions"]=spec["executions"]!.DeepClone()};
var files=new JsonArray();var bindings=new JsonArray();using var source=ZipFile.OpenRead(sourceArchive);
foreach(var stem in new[]{"ProjectGmKernel.Native","ProjectGmKernel.Xt"})
{
    var dll=Path.Combine(binaryDirectory,stem+".dll");var pdb=Path.Combine(binaryDirectory,stem+".pdb");
    bindings.Add((JsonNode)Bind(dll,pdb));Copy(dll,Path.Combine("managed",stem+".dll"));Copy(pdb,Path.Combine("managed",stem+".pdb"));
}
var kernelHash=Hash(File.ReadAllBytes(Path.Combine(binaryDirectory,"ProjectGmKernel.Native.dll")));
foreach(var execution in manifest["executions"]!.AsArray())
{
    var log=Full(execution!["log"]!.GetValue<string>());var exit=Full(execution["exit"]!.GetValue<string>());
    var raw=int.Parse(File.ReadAllText(exit).Trim(),System.Globalization.CultureInfo.InvariantCulture);
    if(raw!=execution["rawExit"]!.GetValue<int>())throw new InvalidOperationException("Raw execution exit mismatch");
    var line=File.ReadLines(log).FirstOrDefault(x=>x.StartsWith("GM_ASSEMBLY sha256=",StringComparison.Ordinal));
    execution["observedKernelSha256"]=line?["GM_ASSEMBLY sha256=".Length..];
    execution["matchesPackagedKernelBytes"]=line=="GM_ASSEMBLY sha256="+kernelHash;
    Copy(log,Path.Combine("evidence",Path.GetRelativePath(root,log)));Copy(exit,Path.Combine("evidence",Path.GetRelativePath(root,exit)));
}
foreach(var node in spec["directories"]!.AsArray())
{
    var directory=Full(node!.GetValue<string>());
    foreach(var file in Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        Copy(file,Path.Combine("evidence",Path.GetRelativePath(root,file)));
}
Copy(Full(args[2]),"evidence-specification.json");manifest["binarySourceBindings"]=bindings;manifest["artifacts"]=files;
File.WriteAllText(Path.Combine(output,"manifest.json"),manifest.ToJsonString(new(){WriteIndented=true}));
ZipFile.CreateFromDirectory(output,archive,CompressionLevel.Optimal,false);
Console.WriteLine($"SEALED source={sha} files={files.Count} archive={Path.GetRelativePath(root,archive)} bytes={new FileInfo(archive).Length} sha256={Hash(File.ReadAllBytes(archive))}");

JsonObject Bind(string dll,string pdb)
{
    using var pe=new PEReader(File.OpenRead(dll));var metadata=pe.GetMetadataReader();
    var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));
    using var provider=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdb));var debug=provider.GetMetadataReader();
    if(cv.Guid!=new Guid(debug.DebugMetadataHeader!.Id.AsSpan(0,16)))throw new InvalidOperationException("PE/PDB mismatch");
    var documents=new JsonArray();var verified=0;
    foreach(var handle in debug.Documents)
    {
        var doc=debug.GetDocument(handle);var path=debug.GetString(doc.Name).Replace('\\','/');string? relative=null;
        foreach(var prefix in new[]{"src/","scripts/","tests/"})
        {
            var at=path.LastIndexOf("/"+prefix,StringComparison.Ordinal);if(at>=0){relative=path[(at+1)..];break;}
            if(path.StartsWith(prefix,StringComparison.Ordinal)){relative=path;break;}
        }
        var entry=relative==null?null:source.GetEntry(relative);var state="generated-or-external-unbound";
        if(entry!=null)
        {
            using var stream=entry.Open();var algorithm=debug.GetGuid(doc.HashAlgorithm);
            var actual=algorithm==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")?SHA256.HashData(stream)
                :algorithm==new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460")?SHA1.HashData(stream):throw new InvalidOperationException("Unknown PDB checksum");
            if(!actual.AsSpan().SequenceEqual(debug.GetBlobBytes(doc.Hash)))throw new InvalidOperationException("Source/PDB mismatch: "+relative);
            state="verified-fixed-source";verified++;
        }
        documents.Add((JsonNode)new JsonObject{["path"]=relative??Path.GetFileName(path),["state"]=state});
    }
    if(verified==0)throw new InvalidOperationException("No compiled documents bind to fixed source");
    return new(){["dll"]=Path.GetFileName(dll),["sha256"]=Hash(File.ReadAllBytes(dll)),["mvid"]=metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString(),
        ["pdbGuid"]=cv.Guid.ToString(),["verifiedDocuments"]=verified,["documents"]=documents};
}
void Copy(string file,string relative)
{
    // Explicit evidence directories only; never include vendor runtimes or credentials.
    var extension=Path.GetExtension(file).ToLowerInvariant();
    if(extension is ".so" or ".dylib" or ".dll" or ".pdb"&&Path.GetFileNameWithoutExtension(file) is not ("ProjectGmKernel.Native" or "ProjectGmKernel.Xt"))
        throw new InvalidOperationException("Unexpected binary in evidence: "+Path.GetRelativePath(root,file));
    var destination=Path.Combine(output,relative);if(File.Exists(destination))return;
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination);files.Add((JsonNode)Identity(destination));
}
JsonObject Identity(string path)=>new(){["path"]=Path.GetRelativePath(output,path).Replace('\\','/'),["bytes"]=new FileInfo(path).Length,["sha256"]=Hash(File.ReadAllBytes(path))};
string Full(string relative)
{
    if(Path.IsPathRooted(relative))throw new ArgumentException("Project-relative paths required");var full=Path.GetFullPath(Path.Combine(root,relative));
    if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new ArgumentException("Path outside project");return full;
}
string Git(params string[] arguments)
{
    var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);
    using var process=Process.Start(start)!;var text=process.StandardOutput.ReadToEnd();var error=process.StandardError.ReadToEnd();process.WaitForExit();
    if(process.ExitCode!=0)throw new InvalidOperationException(error);return text;
}
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Source([CallerFilePath]string path="")=>path;
