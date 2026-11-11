#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Globalization;
using System.Buffers.Binary;

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
var work="temp_docs/pk-native-algorithm-work/";
var measurement=work+"measurement-6616-gconic-candidate/";
var reportPath=measurement+"temp_docs/native-6616-gconic-controlled/report.json";
var report=Read(reportPath);var summary=Read("docs/native-modeling-gconic/operation-summary.json");
var receipt=Read("docs/native-modeling-gconic/measurement-receipt.json");
var materials=Read("docs/native-modeling-gconic/material-binding.json");var identities=Read("docs/native-modeling-gconic/loaded-identity.json");
foreach(var key in new[]{"rawReport","summary","materials","loadedIdentity","commonRunner"})
    Require(Hash(Original(S(receipt[key]!["path"])))==S(receipt[key]!["sha256"]),"receipt hash "+key);
string[] wrappers=["5392a06ea07267f093461876d84fc575849f5ea5","34ffe46b7010b44c221fae24b2371597e46ede36"];
string[] calculations=["6616cd60a55e50a5d8a5ada7274895c44609676b","74c29e0fc14cc40bc134a012b0eca3fecedc2947"];
string[] nativeTrees=["5b4c6104c600b0194bb543e2dc11efa89e2f0f1f","58c1667b0155ec46b53be42b9750c8dbd78a75dd"];
string[] srcTrees=["41c8b597c54753ab394bc99a8b2e59bb3d83a756","397526ef55c3b0376f3e2fc353375ba7ca18395b"];
var common=Read(work+"gconic-common-runner-hashes.json").AsArray();Require(common.Count==9,"nine common files");
var gitCache=new Dictionary<string,byte[]>(StringComparer.Ordinal);var sourceInfo=new JsonArray();
for(var side=0;side<2;side++)
{
    var label=side==0?"baseline":"candidate";
    Require(TextGit("rev-parse",wrappers[side]+":src/ProjectGmKernel.Native")==nativeTrees[side]&&TextGit("rev-parse",calculations[side]+":src/ProjectGmKernel.Native")==nativeTrees[side],"unchanged Native wrapper");
    Require(TextGit("rev-parse",wrappers[side]+":src")==srcTrees[side],"src tree");
    foreach(var declaration in new[]{receipt["sourceTrees"]![label]!,identities["sources"]![label]!})
        Require(S(declaration["wrapperSource"])==wrappers[side]&&S(declaration["srcTree"])==srcTrees[side]&&S(declaration["nativeTree"])==nativeTrees[side],"derived source declaration");
    Require(TextGit("diff","--name-only",calculations[side],wrappers[side],"--","src","Directory.Build.props","Directory.Build.targets")=="","wrapper changes no computation/build configuration");
    // The original report's misleading field name contains the whole src tree.
    Require(S(report[label+"NativeTree"])==srcTrees[side],"original src-tree field");
    foreach(var item in common)
    {
        var file=S(item!["file"]);Require(Digest(CachedGit(wrappers[side]+":"+file))==S(item["sha256"]),"common fixed runner "+file);
        if(file.EndsWith("Oracle.cs",StringComparison.Ordinal))
        {
            var current=Encoding.UTF8.GetString(CachedGit(wrappers[side]+":"+file));
            var prior=Encoding.UTF8.GetString(CachedGit("a73bc08dbd39:"+file));
            var stripped=current.Replace("if(args.Contains(\"--measurement-identity-only\",StringComparer.Ordinal)){_ = WebProgressCapture.Enabled;return;}","");
            Require(stripped.Split('\n').Where(x=>!string.IsNullOrWhiteSpace(x)).SequenceEqual(prior.Split('\n').Where(x=>!string.IsNullOrWhiteSpace(x))),"unchanged a73 checks/timing "+file);
        }
    }
    sourceInfo.Add((JsonNode)new JsonObject{["source"]=label,["calculation"]=calculations[side],["wrapper"]=wrappers[side],["nativeTree"]=nativeTrees[side],["srcTree"]=srcTrees[side]});
}
foreach(var (name,before,after,path) in new[]{("gconic-baseline-wrapper.diff",calculations[0],wrappers[0],"scripts"),("gconic-candidate-wrapper.diff","b47e25ec90f99385f9d2ce86ec88029bb4143e00",wrappers[1],"scripts"),("gconic-native-source.diff",calculations[0],calculations[1],"src")})
    Require(Git("diff",before,after,"--",path).AsSpan().SequenceEqual(File.ReadAllBytes(Original(work+name))),"fixed diff "+name);
Require(ReadExit(work+"gconic-controlled.exit")==0,"original harness exit");
var prep=Directory.GetFiles(Original(work),"gconic-prebuild-*.exit");Require(prep.Length==10&&prep.All(p=>File.ReadAllText(p).Trim()=="0"),"ten prebuild exits");
Require(ReadExit(work+"gconic-qualification.exit")==0,"qualification raw exit");
Require(File.ReadAllText(Original(work+"gconic-qualification.log")).Contains("six missing/failing/ownership controls",StringComparison.Ordinal),"qualification positive/six controls");
var beforeHashes=File.ReadAllBytes(Original(work+"gconic-artifacts-before.txt"));
Require(beforeHashes.AsSpan().SequenceEqual(File.ReadAllBytes(Original(work+"gconic-artifacts-after.txt"))),"output hashes before/after");
var allPairs=new JsonArray();var processes=new JsonArray();var meshCount=0;long meshBytes=0;var xtCount=0;var massCount=0;var diagnosticCount=0;
var samples=report["samples"]!.AsArray();Require(samples.Count==20,"twenty processes");
string[] caseNames=["sphere","planar","curved","solidblend","shortedge"];
var loads=new List<double>();
for(var index=0;index<samples.Count;index++)
{
    var sample=samples[index]!;var id=caseNames[index/4];var side=index%4 is 0 or 3?0:1;var source=side==0?"baseline":"candidate";
    var prefix=measurement+S(sample["directory"])+"/";var capture=Read(prefix+"capture.json");var log=File.ReadAllText(Original(prefix+"oracle.log"));
    Require(S(sample["case"])==id&&S(sample["source"])==source&&S(sample["commit"])==wrappers[side],"ABBA owner/order");
    Require(ReadExit(prefix+"process.exit")==0&&N(sample["processExit"])==0&&!sample["timedOut"]!.GetValue<bool>(),"original process exit");
    Require(Hash(Original(prefix+"capture.json"))==S(sample["captureSha256"])&&Hash(Original(prefix+"oracle.log"))==S(sample["logSha256"]),"capture/log hashes");
    Require(!log.Contains("FAIL ",StringComparison.Ordinal)&&!log.Contains("KERNEL_IDENTITY_ERROR",StringComparison.Ordinal)&&log.Contains("PASS ",StringComparison.Ordinal),"original log completion");
    foreach(var field in new[]{"loadBefore","loadAfter"})loads.Add(double.Parse(S(sample[field]).Split(' ',StringSplitOptions.RemoveEmptyEntries)[1],CultureInfo.InvariantCulture));
    var arguments=sample["arguments"]!.AsArray().Select(S).ToArray();
    string[] expectedArgs=id switch{"curved"=>["block-15902","--bench-repeat=3","--bench-profile"],"solidblend"=>["plane-nurbs","rounding-only","--bench-repeat=3","--bench-profile"],"shortedge"=>["--angle=1","--radius=0.5","--tolerance=1e-07","--bench-repeat=3","--bench-profile"],_=>["--bench-repeat=3","--bench-profile"]};
    Require(arguments.SequenceEqual(expectedArgs),"formal arguments without identity-only");
    var selector=id switch{"sphere"=>"equal-overlap-15902","planar"=>"overlap-two-axes-15902","curved"=>"block-15902",_=>null};
    Require((string?)sample["selector"]==selector,"selector");
    Require(S(capture["pkVersion"])=="38.0.150"&&D(capture["sessionPrecision"])==1e-8&&capture["captureErrors"]!.AsArray().Count==0,"original PK/precision/errors");
    var checks=capture["checks"]!.AsArray().ToDictionary(c=>S(c!["id"]),c=>c!);
    Require(checks.Values.All(c=>S(c["status"])=="pass"),"all original checks pass");
    var steps=capture["steps"]!.AsArray();var timings=capture["timings"]!.AsArray();Require(steps.Count==3&&timings.Count==6,"step/time inventory");
    var mat=materials["samples"]![index]!;Require(S(mat["case"])==id&&S(mat["source"])==source&&S(mat["measurementCommit"])==wrappers[side]&&S(mat["captureSha256"])==Hash(Original(prefix+"capture.json"))&&N(mat["rawExit"])==0,"material owner");
    var meshes=mat["meshes"]!.AsArray().ToDictionary(m=>S(m!["path"]),m=>m!);var seen=new HashSet<string>(StringComparer.Ordinal);
    var workLines=log.Split('\n').Where(l=>l.StartsWith("WORK ",StringComparison.Ordinal)).ToArray();var profileLines=log.Split('\n').Where(l=>l.StartsWith("PROFILE operation=",StringComparison.Ordinal)).ToArray();
    Require(workLines.Length==3&&profileLines.Length==3,"three complete work profiles");
    Require(log.Split('\n').Where(l=>l.StartsWith("WORK ",StringComparison.Ordinal)||l.StartsWith("PROFILE ",StringComparison.Ordinal)).SequenceEqual(sample["work"]!.AsArray().Select(S)),"report work from log");
    for(var rep=0;rep<3;rep++)
    {
        var step=steps[rep]!;Require(S(step["id"])=="operation-"+(rep+1)&&S(step["title"])==S(sample["api"]),"step owner/API");
        var order=id is "solidblend" or "shortedge"?new[]{"pk","gm"}:new[]{"gm","pk"};
        Require(S(sample["callOrder"])==string.Join(" then ",order.Select(x=>x.ToUpperInvariant())),"declared call order");
        var expectedStatus=id is "solidblend" or "shortedge"?18391:21650;
        for(var t=0;t<2;t++){var time=timings[2*rep+t]!;Require(N(time["step"])==rep&&S(time["kernel"])==order[t]&&N(time["error"])==0&&N(time["resultStatus"])==expectedStatus&&D(time["ms"])>0,"actual ordered API timings/status");}
        double Time(string kernel)=>D(timings.Single(t=>N(t!["step"])==rep&&S(t["kernel"])==kernel)!["ms"]);
        var gm=Time("gm");var pk=Time("pk");var originalPair=sample["pairs"]![rep]!;
        Require(D(originalPair["gmMs"])==gm&&D(originalPair["pkMs"])==pk&&D(originalPair["ratioGmOverPk"])==gm/pk&&N(originalPair["rep"])==rep&&S(originalPair["temperature"])==(rep==0?"cold":"warm"),"raw 120 times/60 pairs");
        var required=new List<string>{$"{rep}-body-count",$"{rep}-equivalence",$"gm-{rep}-api",$"pk-{rep}-api",$"{rep}-True-body-check",$"{rep}-False-body-check"};
        for(var c=0;c<5;c++)required.Add($"{rep}-count-{c}");for(var c=0;c<15;c++)required.Add($"{rep}-mass-{c}");
        if(expectedStatus==18391){required.Add($"{rep}-True-blend-fault");required.Add($"{rep}-False-blend-fault");}
        Require(required.All(checks.ContainsKey),"complete required quality checks");
        foreach(var key in required)
        {
            var check=checks[key];
            if(key.Contains("-mass-",StringComparison.Ordinal)){var error=Math.Abs(D(check["gm"])-D(check["pk"]))/ (1+Math.Abs(D(check["pk"])));Require(double.IsFinite(error)&&D(check["threshold"])==1e-7&&error<1e-7,"actual mass values");massCount++;}
            else if(key.EndsWith("-api",StringComparison.Ordinal)||key.EndsWith("body-check",StringComparison.Ordinal))Require(D(check["gm"]??check["pk"])==0,"API/full BODY faults");
            else if(key.EndsWith("blend-fault",StringComparison.Ordinal))Require(D(check["gm"]??check["pk"])==18391,"blend no fault");
            else if(key.Contains("-count",StringComparison.Ordinal))Require(D(check["gm"])==D(check["pk"])&&D(check["gm"])>=0,"actual counts");
        }
        var comparison=Read(prefix+$"evidence/compare-{rep}.json");var mask=comparison["mask"]!;
        Require(N(mask["max_diffs"])==1024&&!mask["all_tests"]!.GetValue<bool>()&&!mask["acc_dev_tests"]!.GetValue<bool>(),"strict compare mask");
        foreach(var flag in new[]{"num_tests","tol_dev_tests","tol_tests","class_tests","sense_tests","missing_tests","non_match_tests"})Require(mask[flag]!.GetValue<bool>(),"strict flag "+flag);
        Require(N(comparison["globalResult"])==21921&&comparison["globalDiffs"]!.AsArray().All(d=>N(d!["masters"])==N(d["similars"])),"actual global compare");
        var diffs=comparison["localDiffs"]!.AsArray();Require(diffs.All(d=>N(d!["type"])==21949&&d["diagnostic"]!.GetValue<bool>())&&(N(comparison["localResult"])==21931||N(comparison["localResult"])==21930&&diffs.Count>0),"only chart point diagnostics");diagnosticCount+=diffs.Count;
        foreach(var kernel in new[]{"gm","pk"})
        {
            var item=step[kernel]!;var references=new List<string>{S(item["before"]),S(item["after"])};
            foreach(var field in new[]{"tools","afterBodies"})references.AddRange(item[field]!.AsArray().Select(S));
            Require(item["afterBodies"]!.AsArray().Count>0,"actual product meshes");
            foreach(var reference in references)
            {
                var path=prefix+reference;if(!seen.Add(path))continue;Require(meshes.TryGetValue(path,out var binding),"bound capture mesh");var file=Original(path);
                Require(Hash(file)==S(binding!["sha256"])&&new FileInfo(file).Length==binding["bytes"]!.GetValue<long>()&&S(binding["step"])==S(step["id"])&&S(binding["kernel"])==kernel,"mesh bytes/step/kernel");
                var mesh=Read(path);Require(S(mesh["source"]!["commit"])==wrappers[side]&&S(mesh["source"]!["kernel"])==kernel.ToUpperInvariant()&&mesh["source"]!["freshXtReceive"]!.GetValue<bool>()==(kernel=="gm"),"mesh source/kernel "+path);
                meshCount++;meshBytes+=new FileInfo(file).Length;
            }
        }
        var row=new JsonObject{["case"]=id,["source"]=source,["process"]=index,["rep"]=rep,["temperature"]=rep==0?"cold":"warm",["gmMs"]=gm,["pkMs"]=pk,["pairedRatio"]=gm/pk};
        foreach(var line in new[]{profileLines[rep],workLines[rep]})foreach(var token in line.Split(' '))
        {
            var pieces=token.Split('=',2);if(pieces.Length==2&&double.TryParse(pieces[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var value))row[pieces[0]]=value;
        }
        var workload=summary["workload"]!.AsArray().Single(w=>S(w!["directory"])==S(sample["directory"])&&N(w["rep"])==rep)!;
        foreach(var field in workload.AsObject().Where(f=>f.Key is not ("case" or "source" or "directory" or "rep")))Require(D(row[field.Key])==D(field.Value),"summary workload "+field.Key);
        if(id=="shortedge")Require(D(row["roots"])==3518&&D(row["ssi-cells"])==4956&&D(row["winding"])==196&&D(row["budget-failures"])==0,"shortedge per-rep invariant workload");
        allPairs.Add((JsonNode)row);
    }
    Require(seen.Count==meshes.Count,"all bound meshes referenced");
    var xts=sample["xtArtifactsLastRepetition"]!.AsArray();var boundXt=mat["finalRepetitionXt"]!.AsArray();Require(xts.Count==boundXt.Count,"XT inventory");
    foreach(var xt in xts)
    {
        var path=measurement+S(xt!["path"]);var bound=boundXt.Single(b=>S(b!["path"])==path)!;
        Require(Hash(Original(path))==S(xt["sha256"])&&S(bound["sha256"])==S(xt["sha256"])&&N(xt["repetition"])==2&&N(bound["repetition"])==2&&S(bound["sourcePath"])==S(xt["sourcePath"]),"original final-rep XT");xtCount++;
    }
    if(id=="shortedge")Require(log.Split('\n').Count(l=>l.StartsWith("FINITE ICurve independent fillet count=3 ",StringComparison.Ordinal))==3,"three finite checks");
    if(id=="solidblend")Require(log.Split('\n').Count(l=>l=="PASS 9 sections: radius, both contact positions and tangent continuity, independent native geometry")==3,"nine sections per operation");
    var start=Read(prefix+"measurement-identity-start.json");var end=Read(prefix+"measurement-identity-end.json");
    var binary=Original(prefix+"binary/ProjectGmKernel.Native.dll");var pdb=Original(prefix+"binary/ProjectGmKernel.Native.pdb");
    using var peFile=File.OpenRead(binary);using var pe=new PEReader(peFile);var metadata=pe.GetMetadataReader();var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString();
    var debug=pe.ReadDebugDirectory().Single(d=>d.Type==DebugDirectoryEntryType.CodeView);var cv=pe.ReadCodeViewDebugDirectoryData(debug);
    using var pdbFile=File.OpenRead(pdb);using var provider=MetadataReaderProvider.FromPortablePdbStream(pdbFile);var reader=provider.GetMetadataReader();var pdbId=reader.DebugMetadataHeader!.Id;
    Require(new Guid(pdbId.AsSpan(0,16))==cv.Guid&&cv.Age==1&&BinaryPrimitives.ReadUInt32LittleEndian(pdbId.AsSpan(16,4))==debug.Stamp,"PE CodeView GUID/age/stamp portable PDB ID");
    foreach(var observer in new[]{start,end})
    {
        Require(S(observer["mvid"])==mvid&&S(observer["dllSha256"])==Hash(binary)&&S(observer["pdbSha256"])==Hash(pdb)&&S(observer["codeViewGuid"])==cv.Guid.ToString()&&N(observer["codeViewAge"])==cv.Age&&S(observer["pdbId"])==Convert.ToHexStringLower(pdbId.AsSpan())&&S(observer["sourceCommit"])==wrappers[side],"actual loaded observer versus archived metadata");
        Require(observer["dllBytes"]!.GetValue<long>()==new FileInfo(binary).Length&&observer["pdbBytes"]!.GetValue<long>()==new FileInfo(pdb).Length,"binary byte sizes");
    }
    Require(S(start["phase"])=="start"&&S(end["phase"])=="end","identity phases");
    var documents=new HashSet<string>(StringComparer.Ordinal);var unknown=new JsonArray();
    foreach(var handle in reader.Documents)
    {
        var document=reader.GetDocument(handle);var name=reader.GetString(document.Name).Replace('\\','/');var relative=name[name.IndexOf("src/ProjectGmKernel.Native/",StringComparison.Ordinal)..];
        var algorithm=reader.GetGuid(document.HashAlgorithm);var checksum=reader.GetBlobBytes(document.Hash);
        foreach(var observer in new[]{start,end})
        {
            var entry=observer["documents"]!.AsArray().Single(d=>S(d!["name"])==name)!;
            Require(S(entry["algorithm"])==algorithm.ToString()&&S(entry["hash"])==Convert.ToHexStringLower(checksum),"observer source-hash list from actual PDB");
        }
        if(relative.Contains("/obj/",StringComparison.Ordinal)){unknown.Add((JsonNode?)JsonValue.Create(relative));continue;}
        Require(documents.Add(relative),"unique tracked source");var bytes=CachedGit(wrappers[side]+":"+relative);
        var hash=algorithm==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")?SHA256.HashData(bytes):algorithm==new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460")?SHA1.HashData(bytes):throw new InvalidOperationException("PDB checksum algorithm");
        Require(hash.AsSpan().SequenceEqual(checksum),"Git source bytes versus portable PDB "+relative);
    }
    Require(documents.Count==(side==0?328:330)&&unknown.Count==3&&reader.Documents.Count==start["documents"]!.AsArray().Count&&reader.Documents.Count==end["documents"]!.AsArray().Count,"tracked/generated PDB coverage");
    var identity=identities["processes"]![index]!;
    foreach(var key in new[]{"mvid","dllSha256","pdbSha256","codeViewGuid"})Require(S(identity[key])==S(start[key]),"derived loaded identity "+key);
    Require(N(identity["verifiedTrackedSourceDocuments"])==documents.Count&&JsonNode.DeepEquals(identity["generatedOrUnmappedDocuments"],unknown)&&S(identity["directory"])==prefix.TrimEnd('/')&&S(identity["source"])==source,"derived identity scope");
    foreach(var phase in new[]{"start","end"})Require(log.Split('\n').Count(l=>l==$"KERNEL_IDENTITY phase={phase} mvid={mvid} dll={Hash(binary)} pdb={Hash(pdb)}")==1,"logged actual identity");
    foreach(var line in Encoding.UTF8.GetString(beforeHashes).Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(l=>l.Contains("gconic-"+source+"/",StringComparison.Ordinal)))
        Require(line[..64]==(line.EndsWith(".dll",StringComparison.Ordinal)?Hash(binary):Hash(pdb)),"window file hashes bind loaded archive");
    processes.Add((JsonNode)new JsonObject{["case"]=id,["source"]=source,["directory"]=prefix.TrimEnd('/'),["rawExit"]=0,["captureSha256"]=Hash(Original(prefix+"capture.json")),["logSha256"]=Hash(Original(prefix+"oracle.log")),["mvid"]=mvid,["dllSha256"]=Hash(binary),["pdbSha256"]=Hash(pdb),["codeViewGuid"]=cv.Guid.ToString(),["pdbId"]=Convert.ToHexStringLower(pdbId.AsSpan()),["trackedPdbChecksums"]=documents.Count,["unverifiedGeneratedDocuments"]=unknown,["checks"]=checks.Count});
}
Require(meshCount==312&&meshBytes==34973060&&xtCount==44&&allPairs.Count==60&&massCount==900,"complete material/pair/mass inventory");
Require(N(materials["meshFiles"])==meshCount&&materials["meshBytes"]!.GetValue<long>()==meshBytes&&N(materials["finalRepetitionXtFiles"])==xtCount&&N(materials["pairs"])==60&&N(materials["processes"])==20,"material summary totals");
Require(report["qualified"]!.GetValue<bool>()&&N(summary["qualifiedPairs"])==60&&N(summary["qualifiedProcesses"])==20&&N(receipt["processes"])==20&&N(receipt["pairs"])==60,"consistent report qualification totals");
var operations=new JsonArray();
foreach(var id in caseNames)
{
    var operation=new JsonObject{["id"]=id};var expected=summary["operations"]!.AsArray().Single(o=>S(o!["id"])==id)!;
    foreach(var source in new[]{"baseline","candidate"})
    {
        var sourceRow=new JsonObject();operation[source]=sourceRow;
        foreach(var temperature in new[]{"cold","warm"})
        {
            var pairs=allPairs.Where(p=>S(p!["case"])==id&&S(p["source"])==source&&S(p["temperature"])==temperature).ToArray();
            var gm=pairs.Select(p=>D(p!["gmMs"])).Order().ToArray();var pk=pairs.Select(p=>D(p!["pkMs"])).Order().ToArray();var ratios=pairs.Select(p=>D(p!["pairedRatio"])).Order().ToArray();
            var stats=new JsonObject{["n"]=pairs.Length,["gmMedianMs"]=Median(gm),["gmMinMs"]=gm[0],["gmMaxMs"]=gm[^1],["pkMedianMs"]=Median(pk),["pkMinMs"]=pk[0],["pkMaxMs"]=pk[^1],["ratioMedian"]=Median(ratios),["ratioOfMedians"]=Median(gm)/Median(pk)};
            Require(pairs.Length==(temperature=="warm"?4:2),"all cold/warm sample counts");
            foreach(var field in stats)Require(D(field.Value)==D(expected[source]![temperature]![field.Key]),"independent summary "+id+"/"+field.Key);
            sourceRow[temperature]=stats;
        }
    }
    var before=D(operation["baseline"]!["warm"]!["gmMedianMs"]);var after=D(operation["candidate"]!["warm"]!["gmMedianMs"]);
    operation["gmWarmSpeedup"]=before/after;operation["gmWarmChangePercent"]=100*(after/before-1);
    Require(D(operation["gmWarmSpeedup"])==D(expected["gmWarmSpeedup"])&&D(operation["gmWarmChangePercent"])==D(expected["gmWarmChangePercent"]),"summary warm comparison");
    operations.Add((JsonNode)operation);Console.WriteLine($"WARM {id} GM={before:R}->{after:R} change={100*(after/before-1):R}% speedup={before/after:R} candidatePairedPK={operation["candidate"]!["warm"]!["ratioMedian"]}");
}
Require(loads.Min()==4&&loads.Max()==5.43,"all recorded load bounds");
var evidence=new JsonObject{["format"]="independent-gconic-measurement-review-v1",["verdict"]="accepted-fixed-window-with-explicit-boundaries",["deliverySource"]="217fc6f811e06cbeb936753f77bd65ae4156d8e8",["priorConicReview"]="35e61e5e02e319d8f6b9eaba38f880e62f1bbf0c",["archiveSha256"]=Hash(archive),["archiveBytes"]=31524200,["members"]=665,["expandedBytes"]=131859212,["rawReport"]=reportPath,["rawReportSha256"]=Hash(Original(reportPath)),["sources"]=sourceInfo,["originalHarnessExit"]=0,["prebuildExitsZero"]=10,["qualificationPositiveAndSixControlsRawExit"]=0,["processes"]=processes,["pairs"]=allPairs,["operations"]=operations,["meshFilesVerified"]=meshCount,["meshBytesVerified"]=meshBytes,["rep2XtFilesVerified"]=xtCount,["massPointValuesRecomputed"]=massCount,["chartPointDiagnosticRecords"]=diagnosticCount,["startedUtc"]=report["startedUtc"]!.DeepClone(),["finishedUtc"]=report["finishedUtc"]!.DeepClone(),["load1mMin"]=loads.Min(),["load1mMax"]=loads.Max(),["originalFieldCaveat"]="Raw report and operation-summary baselineNativeTree/candidateNativeTree hold whole src trees; source entries here name Native and src trees separately.",["identityBoundary"]="Native portable PDB tracked documents match fixed Git bytes; three obj-generated documents per process unverified. No independent compiler reproduction or cryptographic execution attestation.",["scope"]="Five fixed operations in this one Mac window only, G+Conic together versus6616. Keep planar regression, all cold/outliers, original suite raw1 and mirror134. No new modelling/PK/math fixtures/ABBA,1887 integration,main or publication."};
evidence["archivePath"]="temp_docs/incoming/mac-6616-gconic-controlled-original.zip";
evidence["auditorSourceSha256"]=Hash(Source());
var bindings=new JsonArray();
foreach(var path in new[]{"docs/Pk_Native_GConic_Controlled_Measurement.md","docs/native-modeling-gconic/measurement-receipt.json","docs/native-modeling-gconic/operation-summary.json","docs/native-modeling-gconic/material-binding.json","docs/native-modeling-gconic/loaded-identity.json"})
    bindings.Add((JsonNode)new JsonObject{["path"]=path,["sha256"]=Hash(Original(path))});
evidence["fixedDeliveryBindings"]=bindings;
Directory.CreateDirectory(Path.Combine(root,"docs/native-modeling-algorithm"));
File.WriteAllText(Path.Combine(root,"docs/native-modeling-algorithm/conic-measurement-independent-evidence.json"),evidence.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS read-only audit processes={processes.Count} pairs={allPairs.Count} meshes={meshCount}/{meshBytes} XT={xtCount} PDBtracked=328/330 massValues={massCount} chartDiagnostics={diagnosticCount}");

JsonNode Read(string relative)=>JsonNode.Parse(File.ReadAllBytes(Original(relative)))!;
string Original(string relative){Require(!Path.IsPathRooted(relative)&&!relative.Contains('\\')&&!relative.Split('/').Any(p=>p is ".." or "."),"relative original path");return Path.Combine(originals,relative);}
int ReadExit(string relative)=>int.Parse(File.ReadAllText(Original(relative)).Trim(),CultureInfo.InvariantCulture);
byte[] CachedGit(string spec){if(!gitCache.TryGetValue(spec,out var bytes)){bytes=GitBytes(spec);gitCache.Add(spec,bytes);}return bytes;}
string TextGit(params string[] arguments)=>Encoding.UTF8.GetString(Git(arguments)).Trim();
byte[] Git(params string[] arguments)
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,UseShellExecute=false};foreach(var arg in arguments)info.ArgumentList.Add(arg);
    using var process=Process.Start(info)!;using var memory=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(memory);process.WaitForExit();Require(process.ExitCode==0,"git command");return memory.ToArray();
}
static string S(JsonNode? node)=>node!.GetValue<string>();
static int N(JsonNode? node)=>node!.GetValue<int>();
static double D(JsonNode? node)=>node is JsonValue value&&value.TryGetValue<double>(out var number)?number:node!.GetValue<int>();
static string Digest(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static double Median(double[] ordered)=>(ordered[(ordered.Length-1)/2]+ordered[ordered.Length/2])*.5;

byte[] GitBytes(string spec)
{
    var info=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,UseShellExecute=false};info.ArgumentList.Add("show");info.ArgumentList.Add(spec);
    using var process=Process.Start(info)!;using var memory=new MemoryStream();process.StandardOutput.BaseStream.CopyTo(memory);process.WaitForExit();Require(process.ExitCode==0,"git show");return memory.ToArray();
}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(file));}
static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException(label);}
