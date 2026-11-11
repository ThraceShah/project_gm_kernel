#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Prepare only: no portal client, service, host proxy, live path, or pruning operation.
var scripts=Path.GetDirectoryName(ScriptPath())!;
try
{
    var options=new Dictionary<string,string>();
    for(var i=0;i<args.Length;i+=2)
        if(i+1==args.Length||args[i] is not ("--data" or "--dist" or "--output" or "--candidate")||!options.TryAdd(args[i],args[i+1]))throw new ArgumentException("Unique --data --dist --output --candidate required.");
    string Resolve(string name)
    {
        var value=options[name];if(Path.IsPathRooted(value))throw new ArgumentException("Paths must be relative to scripts/.");
        return Path.GetFullPath(value,scripts);
    }
    var data=Resolve("--data");var dist=Resolve("--dist");var output=Resolve("--output");
    if(Directory.Exists(output))throw new IOException("Output must be a fresh isolated directory.");
    const string expectedIndex="117bb8e7ad20ea90aee004eb18428d2a220e5270795ab9ce81eff380843e3990";
    if(Hash(File.ReadAllBytes(Path.Combine(data,"index.json")))!=expectedIndex)throw new InvalidDataException("Read-only historical index does not match the dispatched live copy.");
    var historical=Files(data).ToDictionary(p=>Relative(data,p),p=>(Bytes:new FileInfo(p).Length,Sha256:Hash(File.ReadAllBytes(p))));
    var entries=new List<BundleFile>();
    void Copy(string source,string relative,string component)
    {
        var target=Path.Combine(output,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source,target,false);
        entries.Add(new(relative,new FileInfo(target).Length,Hash(File.ReadAllBytes(target)),component));
    }
    foreach(var file in Files(dist))
    {
        var relative=Relative(dist,file);
        if(relative.StartsWith("data/",StringComparison.Ordinal))continue;
        Copy(file,relative,"ui");
    }
    foreach(var file in Files(data))Copy(file,"data/"+Relative(data,file),"historical-data");
    var uv=Path.Combine(dist,"data","display","real-uv");
    if(!Directory.Exists(uv))throw new InvalidDataException("Built candidate has no independent U/V package.");
    foreach(var file in Files(uv))Copy(file,"data/display/real-uv/"+Relative(uv,file),"uv-display");
    foreach(var pair in historical)
    {
        var source=Path.Combine(data,pair.Key);var target=Path.Combine(output,"data",pair.Key);
        if(Hash(File.ReadAllBytes(source))!=pair.Value.Sha256||Hash(File.ReadAllBytes(target))!=pair.Value.Sha256)throw new InvalidDataException("Historical data bytes drifted: "+pair.Key);
    }
    foreach(var entry in entries)
    {
        if(Path.GetExtension(entry.Path).ToLowerInvariant() is ".x_t" or ".x_b" or ".so" or ".dll" or ".dylib")throw new InvalidDataException("Raw XT/vendor asset is not a public dependency: "+entry.Path);
        if(Path.GetExtension(entry.Path) is ".json" or ".log" or ".txt")
        {
            var content=File.ReadAllText(Path.Combine(output,entry.Path));
            if(content.Contains("/home/",StringComparison.Ordinal)||content.Contains("/Users/",StringComparison.Ordinal)||content.Contains("/private/",StringComparison.Ordinal))throw new InvalidDataException("Private path found in public text: "+entry.Path);
        }
    }
    entries.Sort((a,b)=>StringComparer.Ordinal.Compare(a.Path,b.Path));
    var total=entries.Sum(e=>e.Bytes);
    var canonical=string.Concat(entries.Select(e=>$"{e.Path}\0{e.Bytes}\0{e.Sha256}\n"));
    var bundleSha=Hash(Encoding.UTF8.GetBytes(canonical));
    var uiBytes=entries.Where(e=>e.Component=="ui").Sum(e=>e.Bytes);
    var historicalBytes=historical.Values.Sum(e=>e.Bytes);
    var uvBytes=entries.Where(e=>e.Component=="uv-display").Sum(e=>e.Bytes);
    var withinBudget=total<=30L*1024*1024;
    var report=new JsonObject
    {
        ["type"]="prepare-only-inspection-bundle",["published"]=false,["prepareScriptIndependentlyReviewed"]=false,
        ["candidate"]=options["--candidate"],["layoutParent"]="53f9871e55574ec66a96f12904293515ce40c23c",["uvParent"]="e8dd252b2a1baa848c2c59ad9c9b43dd9dc845ba",
        ["indexSha256"]=expectedIndex,["bundleSha256"]=bundleSha,["bundleHashMethod"]="SHA256 of sorted path NUL decimal-byte-count NUL file-SHA256 LF records",
        ["sourceData"]=Path.GetRelativePath(Path.GetFullPath("..",scripts),data).Replace('\\','/'),
        ["uiBytes"]=uiBytes,["historicalDataBytes"]=historicalBytes,["uvBytes"]=uvBytes,["totalBytes"]=total,
        ["budgetBytes"]=30L*1024*1024,["hardLimitBytes"]=32L*1024*1024,["withinBudget"]=withinBudget,
        ["files"]=new JsonArray(entries.Select(e=>(JsonNode)new JsonObject{["path"]=e.Path,["bytes"]=e.Bytes,["sha256"]=e.Sha256,["component"]=e.Component}).ToArray())
    };
    File.WriteAllText(output+".inventory.json",report.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"PREPARED_ONLY files={entries.Count} historical={historicalBytes} ui={uiBytes} uv={uvBytes} total={total} bundleSHA={bundleSha} indexSHA={expectedIndex}");
    if(!withinBudget)throw new InvalidDataException("Prepared bundle exceeds 30 MiB budget; no pruning or deployment performed.");
    return 0;
}
catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
static string ScriptPath([CallerFilePath]string path="")=>path;
static IEnumerable<string> Files(string directory)=>Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal);
static string Relative(string root,string path)=>Path.GetRelativePath(root,path).Replace('\\','/');
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
readonly record struct BundleFile(string Path,long Bytes,string Sha256,string Component);
