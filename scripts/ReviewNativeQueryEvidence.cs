#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var author=Path.GetFullPath(Path.Combine(root,"../gm-native-query-numerics-closure"));
if(args.Length!=1||args[0].Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("new receipt name required");
var output=Path.Combine(root,"temp_docs/native-query-independent",args[0]);
if(Directory.Exists(output))throw new InvalidOperationException("Independent receipts are immutable");
Directory.CreateDirectory(output);
var receipt=new JsonObject();var checks=0;
var result=Read(Path.Combine(root,"docs/query-numerics-closure/results.json"));
var manifest=Read(Path.Combine(root,"docs/query-numerics-closure/artifact-manifest.json")).AsArray();
foreach(var item in manifest)Verify(item!);
receipt["artifactsVerified"]=manifest.Count;
var sources=Read(Path.Combine(root,"docs/query-numerics-closure/source-manifest.json")).AsArray();
foreach(var item in sources)
{
    var path=S(item!,"path");var bytes=GitBytes("0ef2305f5f75a8f810bb8212219adcc1744b882f",path);
    Require(bytes.Length==item!["bytes"]!.GetValue<int>()&&Hash(bytes)==S(item!,"sha256"),"fixed Git source "+path);
}
receipt["fixedGitSourcesVerified"]=sources.Count;
var bindings=new JsonArray();
foreach(var binding in result["binarySourceBindings"]!.AsArray())
{
    var dll=Resolve(S(binding!["dll"]!,"path"));var pdb=Resolve(S(binding["pdb"]!,"path"));
    using var pe=new PEReader(File.OpenRead(dll));var md=pe.GetMetadataReader();
    var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));
    using var provider=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdb));var debug=provider.GetMetadataReader();
    Require(new Guid(debug.DebugMetadataHeader!.Id.AsSpan(0,16))==cv.Guid,"PE/PDB GUID");
    Require(md.GetGuid(md.GetModuleDefinition().Mvid).ToString()==S(binding,"mvid"),"MVID");
    var tracked=binding["documents"]!.AsArray().Where(x=>x!["tracked"]!.GetValue<bool>()).ToArray();
    foreach(var doc in tracked)
    {
        var path=S(doc!,"path");var found=false;
        foreach(var handle in debug.Documents)
        {
            var d=debug.GetDocument(handle);var name=debug.GetString(d.Name).Replace('\\','/');
            if(!name.EndsWith("/"+path,StringComparison.Ordinal)&&name!=path)continue;
            var bytes=GitBytes("0ef2305f5f75a8f810bb8212219adcc1744b882f",path);
            var checksum=debug.GetGuid(d.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")?SHA256.HashData(bytes):SHA1.HashData(bytes);
            Require(checksum.AsSpan().SequenceEqual(debug.GetBlobBytes(d.Hash)),"PDB fixed source "+path);found=true;break;
        }
        Require(found,"actual PDB document "+path);
    }
    Require(tracked.Length==binding["verifiedProjectDocuments"]!.GetValue<int>(),"tracked count");
    bindings.Add((JsonNode)new JsonObject{["role"]=S(binding,"role"),["documentsVerified"]=tracked.Length,["mvid"]=S(binding,"mvid")});
}
receipt["binarySourceBindings"]=bindings;
var executions=new JsonArray();
foreach(var execution in result["executions"]!.AsArray())
{
    var name=S(execution!,"name");var exit=int.Parse(File.ReadAllText(Resolve("temp_docs/query-numerics-closure/"+name+".exit")).Trim());
    Require(exit==execution!["rawExit"]!.GetValue<int>(),"raw exit "+name);
    executions.Add((JsonNode)new JsonObject{["name"]=name,["rawExit"]=exit});
}
receipt["originalExecutions"]=executions;
var trx=XDocument.Load(Resolve("temp_docs/query-numerics-closure/final-suite/closure-complete.trx"));
var counters=trx.Descendants(trx.Root!.Name.Namespace+"Counters").Single();
Require(counters.Attribute("total")!.Value=="2025"&&counters.Attribute("passed")!.Value=="2025"&&counters.Attribute("failed")!.Value=="0","original TRX");
var runs=new JsonArray();
foreach(var run in result["fixed510Runs"]!.AsArray())
{
    var rows=run!["classifications"]!.AsArray();Require(rows.Count==510&&rows.Select(x=>x!["row"]!.GetValue<int>()).Distinct().Count()==510,"unique 510");
    var input=File.ReadAllLines(Resolve(S(run["originalCoordinateSource"]!,"path")));
    var csv=File.ReadAllLines(Resolve(S(run["classificationCsv"]!,"path")));
    var headers=csv[0].Split(',');
    Require(csv.Length==511,"complete CSV");
    foreach(var row in rows)
    {
        var index=row!["row"]!.GetValue<int>();var p=input[index].Split(',');var fields=csv[index].Split(',');
        for(var column=0;column<headers.Length;column++)
        {
            var key=headers[column];if(key is "x" or "y" or "z")continue;
            Require(key=="status"?S(row,key)==fields[column]:row[key]!.GetValue<int>()==int.Parse(fields[column]),"original CSV column "+key);
        }
        foreach(var (key,pos) in new[]{("x",1),("y",2),("z",3)})
            Require(double.Parse(p[pos+4],System.Globalization.CultureInfo.InvariantCulture)==row[key]!.GetValue<double>()&&fields[pos]==S(row,"raw"+key.ToUpperInvariant()),"original XYZ "+index);
        Require(row["GM_error"]!.GetValue<int>()==0,"GM API error");
        var values=new[]{"PK_native","PK_roundtrip","GM_actual","PK_GM_fresh"}.Select(x=>row[x]!.GetValue<int>()).ToArray();
        var state=values.Any(x=>x is not (5701 or 5702 or 5703))||values.Contains(5701)&&values.Contains(5702)?"Different":values.Distinct().Count()==1?"Verified":"Unresolved";
        Require(state==S(row,"status"),"four-way classification");
    }
    var finite=run["finiteBoundaryEvidence"]!;var witnesses=0;var controls=0;
    foreach(var key in new[]{"nativeXtSha256","gmXtSha256"})Require(manifest.Any(x=>S(x!,"sha256")==S(finite,key)),"finite proof XT identity");
    foreach(var point in finite["points"]!.AsArray())
    {
        var bound=point!["gmActualBoundary"]!;var precision=bound["sessionPrecision"]!.GetValue<double>();
        if(S(bound,"boundaryConclusion")=="ProvenWithinSessionPrecisionOfActualFiniteBoundary")
        {Require(bound["boundarySquaredDistanceUpper"]!.GetValue<double>()<=precision*precision,"finite witness bound");witnesses++;}
        Require(bound["cells"]!.GetValue<int>()<=bound["cellBudget"]!.GetValue<int>(),"finite budget");
        foreach(var control in point["precisionControls"]!.AsArray()){Require(S(control!,"agreement")=="Verified","derived control");controls++;}
    }
    Require(witnesses==9,"finite witness count");
    runs.Add((JsonNode)new JsonObject{["run"]=S(run,"run"),["rowsVerified"]=rows.Count,["finiteWitnesses"]=witnesses,["derivedControls"]=controls,["unresolved"]=rows.Count(x=>S(x!,"status")=="Unresolved")});
}
receipt["fixed510"]=runs;
var mass=result["jointMassStandardsReassessment"]!;
var zip=Path.GetFullPath(Path.Combine(author,S(mass,"archivePath")));
Require(zip.StartsWith(Path.GetFullPath(Path.Combine(root,"../gm-joint-1850534-review"))+Path.DirectorySeparatorChar,StringComparison.Ordinal),"ZIP whitelist");
using(var stream=File.OpenRead(zip))Require(stream.Length==33896347&&Convert.ToHexStringLower(SHA256.HashData(stream))=="5a22e9acae103d1b3b6fcfcb920c7eb13ae526d42fdf5181ac8e78db332436f1","original ZIP");
using var archive=ZipFile.OpenRead(zip);Require(archive.Entries.Count==1038,"ZIP entries");
var paths=new HashSet<string>(StringComparer.Ordinal);var transitions=new Dictionary<string,int>();var cohorts=new Dictionary<string,int>();
foreach(var report in mass["reports"]!.AsArray())
{
    var path=S(report!,"sourcePath");Require(paths.Add(path),"unique mass report");
    var file=Path.GetFullPath(Path.Combine(author,path));
    Require(file.StartsWith(Path.GetFullPath(Path.Combine(root,"../gm-joint-1850534-review"))+Path.DirectorySeparatorChar,StringComparison.Ordinal),"mass whitelist");
    var bytes=File.ReadAllBytes(file);Require(bytes.Length==report!["sourceBytes"]!.GetValue<int>()&&Hash(bytes)==S(report,"sourceSha256"),"historical report bytes");
    var suffix="temp_docs/joint-controlled/window/";var entry=archive.GetEntry(path[path.IndexOf(suffix,StringComparison.Ordinal)..])!;
    using(var stream=entry.Open())Require(Convert.ToHexStringLower(SHA256.HashData(stream))==Hash(bytes),"original ZIP member");
    var old=report["oldProperties"]!.AsArray();var current=report["current"]!["properties"]!.AsArray();Require(old.Count==15&&current.Count==15,"15 components");
    Require(JsonNode.DeepEquals(JsonNode.Parse(bytes)!["properties"],old),"embedded historical properties equal original bytes");
    for(var i=0;i<15;i++)
    {
        var before=S(old[i]!,"agreementStatus");var after=S(current[i]!,"agreementStatus");
        Add(transitions,before+"->"+after);Add(cohorts,S(report,"cohort")+".before."+before);Add(cohorts,S(report,"cohort")+".after."+after);
    }
}
Require(paths.Count==72,"72 unique reports");receipt["jointReportCount"]=paths.Count;receipt["jointComponents"]=1080;
receipt["jointTransitions"]=JsonSerializerNode(transitions);receipt["jointCohorts"]=JsonSerializerNode(cohorts);receipt["checks"]=checks;
File.WriteAllText(Path.Combine(output,"receipt.json"),receipt.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"EVIDENCE_AUDIT_OK checks={checks} artifacts={manifest.Count} sources={sources.Count} bindings={bindings.Count} reports={paths.Count}");

void Add(Dictionary<string,int> counts,string key)=>counts[key]=counts.GetValueOrDefault(key)+1;
JsonNode JsonSerializerNode(Dictionary<string,int> values){var node=new JsonObject();foreach(var pair in values)node[pair.Key]=pair.Value;return node;}
void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);checks++;}
string Resolve(string path)
{
    if(Path.IsPathRooted(path))throw new InvalidOperationException("absolute artifact path");
    var full=Path.GetFullPath(Path.Combine(author,path));
    var allowed=new[]{author,Path.GetFullPath(Path.Combine(root,"../gm-pk-native-delivery"))};
    if(!allowed.Any(x=>full.StartsWith(x+Path.DirectorySeparatorChar,StringComparison.Ordinal)))throw new InvalidOperationException("artifact outside whitelist");
    return full;
}
void Verify(JsonNode item){using var stream=File.OpenRead(Resolve(S(item,"path")));Require(stream.Length==item["bytes"]!.GetValue<long>()&&Convert.ToHexStringLower(SHA256.HashData(stream))==S(item,"sha256"),"artifact identity "+S(item,"path"));}
byte[] GitBytes(string revision,string path)
{
    var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("show");start.ArgumentList.Add(revision+":"+path);
    using var process=Process.Start(start)!;using var output=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(output);process.WaitForExit();
    if(process.ExitCode!=0)throw new InvalidOperationException("fixed Git source missing "+path);return output.ToArray();
}
static JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllBytes(path))!;
static string S(JsonNode node,string key)=>node[key]!.GetValue<string>();
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Source([CallerFilePath]string path="")=>path;
