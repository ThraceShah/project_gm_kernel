#!/usr/bin/env dotnet run
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
var output=Path.Combine(root,"docs","query-numerics-closure");if(Directory.Exists(output))throw new InvalidOperationException("Published evidence is immutable; choose a new source revision instead of overwriting");
var privateRoot="temp_docs/query-numerics-closure";
var artifacts=new Dictionary<string,JsonObject>(StringComparer.Ordinal);var bindings=new JsonArray();var executions=new JsonArray();
var trackedPaths=new HashSet<string>(Git("ls-files","-z").Split('\0',StringSplitOptions.RemoveEmptyEntries),StringComparer.Ordinal);
var specs=new (string Name,int Exit,string Products,string? Capture)[]{
    ("final-axial",0,"temp_docs/local-operation-oracle/closure-final-axial","final-capture-axial"),
    ("final-self",2,"temp_docs/short-edge-blend-oracle/closure-final-self","final-capture-self"),
    ("final-import",2,"temp_docs/short-edge-blend-oracle/closure-final-import","final-capture-import"),
    ("final-contact",2,"temp_docs/regular-solid-blend-oracle/closure-final-contact","final-capture-contact"),
    ("final-fixed20",0,"temp_docs/native-query-replay/closure-final-fixed20",null),
    ("final-negative",0,"temp_docs/native-acceptance-contract/closure-final-negative","final-capture-negative"),
    ("final-self-direct",2,"temp_docs/short-edge-blend-oracle/closure-final-self-direct",null),
    ("final-import-direct",2,"temp_docs/short-edge-blend-oracle/closure-final-import-direct",null),
    ("final-native-mass",2,"temp_docs/mass-certification/closure-native-final-current",null),
    ("final-suite",0,"temp_docs/query-numerics-closure/final-suite",null),
    ("mass-review-final",0,"temp_docs/mass-certification-review/joint-physical-final",null)};
foreach(var spec in specs)
{
    var log=Rel(privateRoot,spec.Name+".log");var exit=Rel(privateRoot,spec.Name+".exit");var raw=int.Parse(File.ReadAllText(Full(exit)).Trim());
    if(raw!=spec.Exit)throw new InvalidOperationException($"{spec.Name} unexpected raw exit {raw}");
    Add(log);Add(exit);SealDirectory(spec.Products);
    var execution=new JsonObject{["name"]=spec.Name,["rawExit"]=raw,["expectedRawExit"]=spec.Exit,["log"]=Identity(log),["products"]=spec.Products,
        ["executedInvocation"]=Invocation(spec.Name),["invocationSource"]="worker tool execution record; no additional child argv observer was injected",
        ["executionKind"]=spec.Name=="mass-review-final"?"read-only historical standards reassessment":spec.Name=="final-suite"?"managed test execution":"real native/managed oracle execution"};
    if(spec.Capture!=null)
    {
        var directory=Rel(privateRoot,spec.Capture);SealDirectory(directory);var capture=Read(Rel(directory,"capture.json"));
        if(capture["captureErrors"]!.AsArray().Count!=0)throw new InvalidOperationException(spec.Name+" observer errors");
        var counts=new JsonObject();foreach(var group in capture["checks"]!.AsArray().Select(x=>x!["status"]!.GetValue<string>()).GroupBy(x=>x))counts[group.Key]=group.Count();
        execution["captureChecks"]=counts;execution["captureErrors"]=0;execution["capture"]=Identity(Rel(directory,"capture.json"));
        var massReports=new JsonArray();var compareReports=new JsonArray();
        foreach(var file in Directory.GetFiles(Full(Rel(directory,"evidence")),"quality-*.json").Order(StringComparer.Ordinal))
        {
            var relative=Path.GetRelativePath(root,file);var report=new JsonObject{["artifact"]=Identity(relative),["report"]=Read(relative).DeepClone()};
            if(Path.GetFileName(file).StartsWith("quality-mass-",StringComparison.Ordinal))massReports.Add((JsonNode)report);
            else if(Path.GetFileName(file).StartsWith("quality-compare-",StringComparison.Ordinal))compareReports.Add((JsonNode)report);
        }
        execution["actualMassReports"]=massReports;execution["actualStrictCompareReports"]=compareReports;
        var identity=Read(Rel(directory,"identity/loaded.json"));execution["loadedIdentity"]=identity.DeepClone();
        foreach(var role in new[]{"kernel","oracle"})
        {
            var dll=Rel(directory,"identity/"+role+".dll");var pdb=Rel(directory,"identity/"+role+".pdb");
            if(Hash(File.ReadAllBytes(Full(dll)))!=identity[role+"DllSha256"]!.GetValue<string>()||Hash(File.ReadAllBytes(Full(pdb)))!=identity[role+"PdbSha256"]!.GetValue<string>())throw new InvalidOperationException("Loaded identity seal differs");
            bindings.Add((JsonNode)Bind(dll,pdb,spec.Name+"-"+role));
        }
    }
    executions.Add((JsonNode)execution);
}
var trx=Rel(privateRoot,"final-suite/closure-complete.trx");var document=XDocument.Load(Full(trx));var ns=document.Root!.Name.Namespace;
var counters=document.Descendants(ns+"Counters").Single();
if(counters.Attribute("total")!.Value!="2025"||counters.Attribute("passed")!.Value!="2025"||counters.Attribute("failed")!.Value!="0")throw new InvalidOperationException("Complete suite counters differ");
foreach(var role in new[]{"ProjectGmKernel.Native","KernelTests"})
{
    var dll="tests/KernelTests/bin/Release/net10.0/"+role+".dll";var pdb=Path.ChangeExtension(dll,".pdb");
    bindings.Add((JsonNode)Bind(dll,pdb,"managed-suite-"+role));Add(dll);Add(pdb);
}
var queryReplay=Read("temp_docs/native-query-replay/closure-final-fixed20/results.json");
if(queryReplay["points"]!.AsArray().Count!=20||queryReplay["points"]!.AsArray().Any(x=>x!["agreement"]!.GetValue<string>()!="Verified"))throw new InvalidOperationException("Original 20 fixed queries must agree");
var fixedProbes=new JsonArray();
foreach(var name in new[]{"closure-final-self-direct","closure-final-import-direct","closure-final-self","closure-final-import"})
{
    var directory="temp_docs/short-edge-blend-oracle/"+name;var csv=Directory.GetFiles(Full(directory),"*-fixed-classifications.csv").Single();
    var lines=File.ReadAllLines(csv);var headers=lines[0].Split(',');var rows=new JsonArray();var nativeRoundtrip=new JsonArray();var different=0;var unresolved=0;
    foreach(var line in lines.Skip(1))
    {
        var fields=line.Split(',');if(fields.Length!=headers.Length)throw new InvalidOperationException("Current probe columns differ");
        var row=new JsonObject();for(var i=0;i<fields.Length;i++)
        {
            if(headers[i]=="status")row[headers[i]]=fields[i];
            else if(headers[i] is "x" or "y" or "z"){row[headers[i]]=double.Parse(fields[i],System.Globalization.CultureInfo.InvariantCulture);row["raw"+headers[i].ToUpperInvariant()]=fields[i];}
            else row[headers[i]]=int.Parse(fields[i],System.Globalization.CultureInfo.InvariantCulture);
        }
        rows.Add((JsonNode)row);
        if(row["PK_native"]!.GetValue<int>()!=row["PK_roundtrip"]!.GetValue<int>())nativeRoundtrip.Add((JsonNode?)JsonValue.Create(row["row"]!.GetValue<int>()));
        different+=row["status"]!.GetValue<string>()=="Different"?1:0;unresolved+=row["status"]!.GetValue<string>()=="Unresolved"?1:0;
    }
    if(rows.Count!=510||different!=0)throw new InvalidOperationException("Fixed probe count/API or material conflict");
    var finite=Directory.GetFiles(Full(directory),"*-finite-boundary.json").Single();var relation=Read(Path.GetRelativePath(root,finite));
    var points=relation["points"]!.AsArray();var controls=points.SelectMany(x=>x!["precisionControls"]!.AsArray()).ToArray();
    if(controls.Any(x=>x!["agreement"]!.GetValue<string>()!="Verified"))throw new InvalidOperationException("Separated precision controls disagree");
    fixedProbes.Add((JsonNode)new JsonObject{["run"]=name,["captureEnabled"]=!name.EndsWith("-direct",StringComparison.Ordinal),["rawFourWayDifferent"]=different,["rawFourWayUnresolved"]=unresolved,
        ["rawFourWayVerified"]=510-unresolved,["nativeRoundtripDifferentRows"]=nativeRoundtrip,["originalCoordinateSource"]=Identity("docs/reviews/advanced-modeling/short-edge-spine-probes.csv"),
        ["classificationCsv"]=Identity(Path.GetRelativePath(root,csv)),["classifications"]=rows,["finiteBoundaryEvidence"]=relation.DeepClone(),
        ["provenGmFiniteBoundaryMemberships"]=points.Count(x=>x!["gmActualBoundary"]!["boundaryConclusion"]!.GetValue<string>()=="ProvenWithinSessionPrecisionOfActualFiniteBoundary"),
        ["precisionControlsVerified"]=controls.Length});
}
var historical=new JsonArray();var pattern=new Regex(@"SAVED PROBE DIFF row=(\d+) p=\(([^,]+),([^,]+),([^)]+)\) native/roundtrip/self/fresh=(-?\d+)/(-?\d+)/(-?\d+)/(-?\d+)",RegexOptions.CultureInvariant);
foreach(var mode in new[]{"self","import"})
{
    var path="../gm-pk-native-delivery/temp_docs/pk-native-delivery/probes-"+mode+".log";var text=File.ReadAllText(Full(path));var observed=new JsonArray();var lineNumber=0;
    foreach(var line in text.Split('\n'))
    {
        lineNumber++;var m=pattern.Match(line);if(!m.Success)continue;
        observed.Add((JsonNode)new JsonObject{["row"]=int.Parse(m.Groups[1].Value),["rawX"]=m.Groups[2].Value,["rawY"]=m.Groups[3].Value,["rawZ"]=m.Groups[4].Value,
            ["PK_native"]=int.Parse(m.Groups[5].Value),["PK_roundtrip"]=int.Parse(m.Groups[6].Value),["GM_actual"]=int.Parse(m.Groups[7].Value),["PK_GM_fresh"]=int.Parse(m.Groups[8].Value),["line"]=lineNumber});
    }
    if(observed.Count!=(mode=="self"?15:16))throw new InvalidOperationException("Fixed historical mismatch count differs");
    Add(path);var models=new JsonArray();var directory="../gm-pk-native-delivery/temp_docs/short-edge-blend-oracle/native-probes-"+mode;
    foreach(var file in Directory.GetFiles(Full(directory),"*.x_t").Order(StringComparer.Ordinal))models.Add((JsonNode)Identity(Path.GetRelativePath(root,file)));
    historical.Add((JsonNode)new JsonObject{["mode"]=mode,["originalLog"]=Identity(path),["actualRecordedMismatchRows"]=observed,["modelSources"]=models,
        ["full510RawColumnsPersistedInOriginalLog"]=false,["unrecordedRawColumns"]=null,
        ["note"]="Original log records the differing rows and a 510-point summary; absent raw rows are not reconstructed. The original imported four-column CSV is preserved separately."});
}
var oldCsv="docs/reviews/advanced-modeling/short-edge-spine-probes.csv";var importedOriginal="../gm-pk-native-delivery/temp_docs/short-edge-blend-oracle/contract-probes-import-v1/a1-r0.5-t1E-07-reverseFalse-default-imported-fixed-classifications.csv";
Add(oldCsv);Add(importedOriginal);
var mass=Read("temp_docs/mass-certification-review/joint-physical-final/results.json");var nativeControls=Read("temp_docs/mass-certification/closure-native-final-current/results.json");
var contacts=Read("temp_docs/regular-solid-blend-oracle/closure-final-contact/plane-nurbs-contact-query-controls.json");if(contacts.AsArray().Count!=4)throw new InvalidOperationException("Four actual finite contact queries required");
var sourceManifest=new JsonArray();foreach(var path in Git("ls-files","-z","src/ProjectGmKernel.Native","src/ProjectGmKernel.Xt","tests/KernelTests","scripts").Split('\0',StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal))sourceManifest.Add((JsonNode)Identity(path));
sourceManifest.Add((JsonNode)Identity(Path.GetRelativePath(root,Source())));
Directory.CreateDirectory(output);
var result=new JsonObject{["format"]="native-query-numerics-closure",["baselineCommit"]="17eedbfd6be891db8c43494cd91a896d261242c4",["calculationCommit"]="ab597e8a24b2ef23753507c8ca3e43d12edcc650",
    ["evidenceSourceHead"]=Git("rev-parse","HEAD").Trim(),["policyBodySha256"]="55f16ce0cf53bbb37ddd91920445f32468eb8e2b5b6e28f6fb90856810afcca1",
    ["policyRuntimeAutoloadVerified"]=false,["executions"]=executions,["completeManagedSuite"]=new JsonObject{["total"]=2025,["passed"]=2025,["failed"]=0,["skipped"]=0,["rawExit"]=0,
        ["bindingScope"]="standard testhost output assemblies sealed and PDB/source verified after execution; no additional runtime test loader was injected"},
    ["fixedOriginal20"]=queryReplay,["contactQueries"]=contacts,["fixed510Runs"]=fixedProbes,["historical510"]=historical,["originalImportedFourColumnCsv"]=Identity(oldCsv),["previousCurrentImportedFourColumnCsv"]=Identity(importedOriginal),
    ["jointMassStandardsReassessment"]=mass,["currentNativeMassControls"]=nativeControls,["binarySourceBindings"]=bindings,
    ["scope"]=new JsonObject{["fiveAxialProductsNativeQueries"]=true,["finiteContactTopologyFix"]=true,["native510ThresholdDisagreementsRemain"]=true,["massTargetUnresolvedRemain"]=true,
        ["newMacMeasurements"]=false,["mainIntegrated"]=false,["independentlyAccepted"]=false,["published"]=false,["historicalEvidenceRewritten"]=false}};
File.WriteAllText(Path.Combine(output,"results.json"),result.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"source-manifest.json"),sourceManifest.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"artifact-manifest.json"),new JsonArray(artifacts.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>(JsonNode)x.Value).ToArray()).ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"EVIDENCE_COLLECTED executions={executions.Count} artifacts={artifacts.Count} binaryBindings={bindings.Count}; numeric Unresolved preserved");

JsonNode Read(string path)=>JsonNode.Parse(File.ReadAllBytes(Full(path)))!;
void SealDirectory(string directory){foreach(var path in Directory.GetFiles(Full(directory),"*",SearchOption.AllDirectories))Add(Path.GetRelativePath(root,path));}
void Add(string path){path=path.Replace('\\','/');artifacts.TryAdd(path,Identity(path));}
JsonObject Identity(string path){var bytes=File.ReadAllBytes(Full(path));return new(){["path"]=path.Replace('\\','/'),["bytes"]=bytes.Length,["sha256"]=Hash(bytes)};}
string Full(string path)=>Path.GetFullPath(Path.Combine(root,path));
static string Rel(string a,string b)=>Path.Combine(a,b).Replace('\\','/');
static string Source([CallerFilePath]string path="")=>path;
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
string Git(params string[] arguments){var start=new ProcessStartInfo("git"){WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in arguments)start.ArgumentList.Add(argument);using var process=Process.Start(start)!;var text=process.StandardOutput.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException(process.StandardError.ReadToEnd());return text;}
JsonObject Invocation(string name)
{
    var payload=name switch{
        "final-axial"=>"scripts/LocalOperationOracle.cs|--output-name=closure-final-axial|--axial-arrangement|--managed-material|--lifecycle",
        "final-self"=>"scripts/ShortEdgeBlendOracle.cs|--event-arrangement|--self-saved-probes|--lifecycle|--blend-only-lifecycle|--material-ownership|--require-native-fin-uvbox|--require-native-face-domains|--geometry|--output-name=closure-final-self",
        "final-import"=>"scripts/ShortEdgeBlendOracle.cs|--event-arrangement|--saved-probes|--import-only|--output-name=closure-final-import",
        "final-contact"=>"scripts/RegularSolidBlendOracle.cs|plane-nurbs|rounding-only|--event-arrangement|--contact-query-controls|--output-name=closure-final-contact",
        "final-fixed20"=>"scripts/NativeQueryReplay.cs|closure-final-fixed20|../gm-blend-functional-delivery/temp_docs/local-operation-oracle/blend-query-unsupported-axial|../gm-blend-functional-delivery/temp_docs/blend-functional/final-integrated/axial-material-query.log",
        "final-negative"=>"scripts/NativeAcceptanceContractOracle.cs|closure-final-negative|../gm-pk-native-delivery/temp_docs/native-acceptance-contract/imprint-verified/input.x_t",
        "final-self-direct"=>"scripts/ShortEdgeBlendOracle.cs|--event-arrangement|--self-saved-probes|--output-name=closure-final-self-direct",
        "final-import-direct"=>"scripts/ShortEdgeBlendOracle.cs|--event-arrangement|--saved-probes|--import-only|--output-name=closure-final-import-direct",
        "final-native-mass"=>"scripts/MassCertificationOracle.cs|closure-native-final-current|../gm-pk-native-delivery/temp_docs/native-acceptance-contract/imprint-verified/input.x_t",
        "mass-review-final"=>"scripts/ReviewMassCertification.cs|joint-physical-final|../gm-joint-1850534-review/temp_docs/independent-joint-1850534/measurement/original/temp_docs/joint-controlled/window|../gm-joint-1850534-review/temp_docs/incoming/mac-joint-185-controlled-original.zip",
        "final-suite"=>"",_=>throw new InvalidOperationException("Unknown executed invocation")};
    if(name=="final-suite")return new(){["executable"]="dotnet",["arguments"]="test tests/KernelTests -c Release --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -p:GmInProcessLinkTasks=true --logger trx;LogFileName=closure-complete.trx --results-directory temp_docs/query-numerics-closure/final-suite --blame-hang-timeout 10m",["MSBUILDDISABLENODEREUSE"]="1"};
    var parts=payload.Split('|');var args=new[]{"run","--no-cache",parts[0],"-c","Release","-p:BuildInParallel=false","-p:UseSharedCompilation=false","-p:GmInProcessLinkTasks=true","--"}.Concat(parts.Skip(1));
    return new(){["executable"]="dotnet",["arguments"]=new JsonArray(args.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),["MSBUILDDISABLENODEREUSE"]="1"};
}
JsonObject Bind(string dll,string pdb,string role)
{
    using var pe=new PEReader(File.OpenRead(Full(dll)));var metadata=pe.GetMetadataReader();var mvid=metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    var codeView=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));
    using var provider=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(Full(pdb)));var debug=provider.GetMetadataReader();
    if(new Guid(debug.DebugMetadataHeader!.Id.AsSpan(0,16))!=codeView.Guid)throw new InvalidOperationException("PE/PDB identity mismatch");
    var documents=new JsonArray();var tracked=0;
    foreach(var handle in debug.Documents)
    {
        var doc=debug.GetDocument(handle);var name=debug.GetString(doc.Name);var relative=Path.GetRelativePath(root,name).Replace('\\','/');
        if(!File.Exists(Full(relative)))
        {
            foreach(var prefix in new[]{"/src/","/scripts/","/tests/","/third_party/"}){var at=name.IndexOf(prefix,StringComparison.Ordinal);if(at>=0){relative=name[(at+1)..];break;}}
        }
        var algorithm=debug.GetGuid(doc.HashAlgorithm);var expected=debug.GetBlobBytes(doc.Hash);var state="unmapped-generated-or-external";
        if(File.Exists(Full(relative)))
        {
            var bytes=File.ReadAllBytes(Full(relative));var actual=algorithm==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")?SHA256.HashData(bytes):algorithm==new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460")?SHA1.HashData(bytes):throw new InvalidOperationException("Unsupported PDB checksum");
            var equal=actual.AsSpan().SequenceEqual(expected);
            if(!equal&&trackedPaths.Contains(relative))throw new InvalidOperationException("Tracked PDB source checksum differs: "+relative);
            state=equal?"verified":"generated-or-external-current-file-differs";
            if(equal&&trackedPaths.Contains(relative))tracked++;
        }
        else if(trackedPaths.Contains(relative))throw new InvalidOperationException("Tracked PDB document missing: "+relative);
        documents.Add((JsonNode)new JsonObject{["path"]=relative,["tracked"]=trackedPaths.Contains(relative),["checksumAlgorithm"]=algorithm.ToString(),["checksum"]=Convert.ToHexStringLower(expected),["state"]=state});
    }
    return new(){["role"]=role,["dll"]=Identity(dll),["pdb"]=Identity(pdb),["mvid"]=mvid.ToString(),["pdbGuid"]=codeView.Guid.ToString(),["verifiedProjectDocuments"]=tracked,["documents"]=documents};
}
