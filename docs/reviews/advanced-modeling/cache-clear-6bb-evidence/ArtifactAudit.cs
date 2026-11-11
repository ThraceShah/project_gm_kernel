#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

// Read only existing managed PE/PDB and diagnostic text. Never load/execute DLL.
var here=Path.GetDirectoryName(SourcePath())!;
var root=Path.GetFullPath("../../../..",here);
var evidence=args.Length==0?Path.Combine(root,"temp_docs/cache-clear-6bb-review/original"):Path.GetFullPath(args[0],here);
var dll=Path.Combine(evidence,"ProjectGmKernel.Native.dll");
var pdbPath=Path.ChangeExtension(dll,".pdb");
using var dllFile=File.OpenRead(dll);using var pe=new PEReader(dllFile);var metadata=pe.GetMetadataReader();
using var pdbFile=File.OpenRead(pdbPath);using var provider=MetadataReaderProvider.FromPortablePdbStream(pdbFile);var pdb=provider.GetMetadataReader();
Console.WriteLine($"DLL sha256={Hash(dll)} MVID={metadata.GetGuid(metadata.GetModuleDefinition().Mvid)}");
Console.WriteLine($"PDB sha256={Hash(pdbPath)}");
var id=new BlobContentId(pdb.DebugMetadataHeader!.Id);
var entry=pe.ReadDebugDirectory().Single(e=>e.Type==DebugDirectoryEntryType.CodeView);
var cv=pe.ReadCodeViewDebugDirectoryData(entry);
Check(id.Guid==cv.Guid&&id.Stamp==entry.Stamp,"PE CodeView GUID/stamp binds this portable PDB");
var wanted=new HashSet<string>(StringComparer.Ordinal){"NestedBlendBounds.cs","NestedBlendBoundsAttribution.cs","NestedBlendIntervalSupport.cs","NestedBlendParameterBounds.cs","JointNestedBlendEvaluation.cs","KernelRuntime.Memory.cs"};
var seen=new HashSet<string>(StringComparer.Ordinal);
foreach(var handle in pdb.Documents)
{
    var doc=pdb.GetDocument(handle);var path=pdb.GetString(doc.Name).Replace('\\','/');
    var name=Path.GetFileName(path);if(!wanted.Contains(name))continue;
    const string marker="/src/ProjectGmKernel.Native/";var at=path.IndexOf(marker,StringComparison.Ordinal);
    Check(at>=0,"PDB source path belongs to expected kernel project: "+name);
    var relative=path[(at+1)..];var actual=Hash(Path.Combine(root,relative));var expected=Convert.ToHexStringLower(pdb.GetBlobBytes(doc.Hash));
    Check(pdb.GetGuid(doc.HashAlgorithm)==new Guid("8829d00f-11b8-4213-878b-770e8597ac16")&&actual==expected,"PDB SHA256 matches fixed source: "+relative);
    Console.WriteLine($"SOURCE {relative} sha256={actual}");seen.Add(name);
}
Check(seen.SetEquals(wanted),"all six attribution/cache/lifecycle documents checked");
foreach(var handle in metadata.MethodDefinitions)
{
    var method=metadata.GetMethodDefinition(handle);var type=metadata.GetTypeDefinition(method.GetDeclaringType());
    if(metadata.GetString(type.Name)!="JointNestedBlendEvaluation")continue;
    var name=metadata.GetString(method.Name);if(name is not ("IntervalJacobian" or "BoundSegment" or "DumpAttribution" or "HasAnyUv"))continue;
    var body=pe.GetMethodBody(method.RelativeVirtualAddress);
    Console.WriteLine($"METHOD {name} ILBytes={body.GetILBytes()!.Length} InitLocals={body.LocalVariablesInitialized}");
}
var rawPath=Path.Combine(evidence,"run2.log");var lines=File.ReadAllLines(rawPath);
Console.WriteLine("RUN2 sha256="+Hash(rawPath));
Check(Hash(rawPath)=="afada1746a0178e5c72b0cb7319c68e7f1ece0b0273b04f29533b69f0ac90c80","received successful-run raw bytes unchanged");
var totalLine=lines.Single(x=>x.StartsWith("ATTRIBUTION total ",StringComparison.Ordinal));
var total=Fields(totalLine);var buckets=lines.Where(x=>x.StartsWith("ATTRIBUTION bucket ",StringComparison.Ordinal)).Select(Fields).ToArray();
long T(string key)=>long.Parse(total[key],System.Globalization.CultureInfo.InvariantCulture);
long Sum(string key)=>buckets.Sum(x=>long.Parse(x[key],System.Globalization.CultureInfo.InvariantCulture));
Check(T("jac-entered")==T("clear-reached")&&T("jac-entered")==T("jac-succeeded")+T("jac-failed"),"Jac/clear/return counts close");
foreach(var keys in new[]{("entered","jac-entered"),("clear-reached","clear-reached"),("succeeded","jac-succeeded"),("failed","jac-failed"),("clear-ticks","clear-ticks")})
    Check(Sum(keys.Item1)==T(keys.Item2),"buckets sum to total: "+keys.Item1);
Check(T("expansion-iters")+T("refinement-iters")==T("jac-entered"),"one Jac per expansion/refinement iteration in this log");
Check(T("bound-entered")==T("bound-evaluated")&&T("bound-entered")==T("bound-succeeded")+T("bound-failed-derived"),"recorded Bound arithmetic closes (not proof all success exits were hooked)");
Check(T("owner-thread")==T("dump-thread")&&T("foreign-thread-touches")==0&&T("out-of-range-n")==0,"recorded owner/bucket preconditions");
Check(buckets.All(x=>x["uv"]=="False"),"only no-UV buckets in this session");
Check(lines.Contains("RUN_EXIT=0")&&lines.Contains("PASS cases=1 mode=independent-construction"),"raw successful runner exit/scope recorded");
Console.WriteLine(totalLine);foreach(var line in lines.Where(x=>x.StartsWith("ATTRIBUTION bucket ",StringComparison.Ordinal)))Console.WriteLine(line);
Console.WriteLine($"REQUESTED_CLEAR_BYTES={checked(T("clear-reached")*22176L)} under 63x352 layout; NOT DRAM bytes");
Console.WriteLine("FIX_SELF "+lines.Single(x=>x.StartsWith("FIX self ",StringComparison.Ordinal)));
Console.WriteLine("PREFLIGHT_HAS_CURRENT_DLL="+File.ReadAllText(Path.Combine(evidence,"preflight.txt")).Contains(Hash(dll),StringComparison.Ordinal));
Console.WriteLine("PASS offline artifact/counter audit; POST-HOC artifacts do not prove at-run loaded DLL; no timing or native execution");
return 0;
static void Check(bool ok,string text){if(!ok)throw new InvalidDataException(text);Console.WriteLine("PASS "+text);}
static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static Dictionary<string,string> Fields(string line)=>Regex.Matches(line,@"(?<key>[\w-]+)=(?<value>\S+)").ToDictionary(x=>x.Groups["key"].Value,x=>x.Groups["value"].Value,StringComparer.Ordinal);
static string SourcePath([CallerFilePath]string path="")=>path;
