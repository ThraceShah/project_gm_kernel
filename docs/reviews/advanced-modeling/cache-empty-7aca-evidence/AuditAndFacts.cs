#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Existing PE/PDB/text only, then six exact public Fact methods if requested.
// Does not rebuild the kernel, run a test suite, benchmark or call vendor PK.
var root=Path.GetFullPath("../../../..",Path.GetDirectoryName(Here())!);
var evidence=Path.Combine(root,"temp_docs/cache-empty-7aca-review");
var nativeDocs=new[]{"NestedBlendBounds.cs","NestedBlendIntervalSupport.cs","NestedBlendParameterBounds.cs","JointNestedBlendEvaluation.cs","KernelRuntime.Memory.cs"};
foreach(var item in new[]{("base",Path.Combine(evidence,"original/base-Native.dll")),("guard",Path.Combine(evidence,"runtime/ProjectGmKernel.Native.dll")),("production",Path.Combine(evidence,"production/ProjectGmKernel.Native.dll"))})
{
    using var stream=File.OpenRead(item.Item2);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
    using var file=File.OpenRead(Path.ChangeExtension(item.Item2,".pdb"));using var provider=MetadataReaderProvider.FromPortablePdbStream(file);var pdb=provider.GetMetadataReader();
    var id=new BlobContentId(pdb.DebugMetadataHeader!.Id);var debug=pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView);var cv=pe.ReadCodeViewDebugDirectoryData(debug);
    Check(cv.Guid==id.Guid&&debug.Stamp==id.Stamp,item.Item1+" DLL/PDB association");
    Console.WriteLine($"ARTIFACT {item.Item1} sha256={Hash(item.Item2)} MVID={md.GetGuid(md.GetModuleDefinition().Mvid)}");
    var seen=new HashSet<string>();var sourceCount=0;var generatedCount=0;
    foreach(var dh in pdb.Documents)
    {
        var doc=pdb.GetDocument(dh);var name=pdb.GetString(doc.Name).Replace('\\','/');var leaf=Path.GetFileName(name);
        var at=name.IndexOf("/src/ProjectGmKernel.Native/",StringComparison.Ordinal);if(at<0)continue;
        if(name.Contains("/obj/",StringComparison.Ordinal)){generatedCount++;continue;}
        var path=item.Item1=="base"&&leaf=="NestedBlendBounds.cs"?Path.Combine(evidence,"base-source",leaf):Path.Combine(root,name[(at+1)..]);
        if(!File.Exists(path))throw new InvalidDataException(item.Item1+" unexpected/missing Native source: "+name[(at+1)..]);
        var actual=Hash(path);if(actual!=Convert.ToHexStringLower(pdb.GetBlobBytes(doc.Hash)))throw new InvalidDataException(item.Item1+" Native source mismatch: "+name[(at+1)..]);sourceCount++;
        if(nativeDocs.Contains(leaf)){Check(true,item.Item1+" source "+leaf);seen.Add(leaf);}
    }
    Check(seen.Count==nativeDocs.Length,item.Item1+" all five relevant source documents");
    Console.WriteLine($"ALL_NATIVE_SOURCES {item.Item1} matched={sourceCount} excluded-obj-generated={generatedCount}");
    var methods=md.MethodDefinitions.Select(h=>(Handle:h,Method:md.GetMethodDefinition(h))).Where(x=>md.GetString(md.GetTypeDefinition(x.Method.GetDeclaringType()).Name)=="JointNestedBlendEvaluation").ToArray();
    Check(methods.All(x=>md.GetString(x.Method.Name)!="DumpAttribution"),item.Item1+" no diagnostic dump");
    var hooks=methods.Any(x=>md.GetString(x.Method.Name)=="TestSplineContactReuse");
    Check(hooks==(item.Item1!="production"),item.Item1+" expected testhooks/production configuration");
    foreach(var x in methods.Where(x=>md.GetString(x.Method.Name) is "BoundSegment" or "IntervalJacobian" or "HasAnyUv"))
    {var body=pe.GetMethodBody(x.Method.RelativeVirtualAddress);Console.WriteLine($"METHOD {item.Item1} {md.GetString(x.Method.Name)} InitLocals={body.LocalVariablesInitialized} ILBytes={body.GetILBytes()!.Length}");}
    foreach(var ch in pdb.CustomDebugInformation)
    {
        var text=Encoding.UTF8.GetString(pdb.GetBlobBytes(pdb.GetCustomDebugInformation(ch).Value));
        if(!text.Contains("optimization\0",StringComparison.Ordinal)||!text.Contains("language\0",StringComparison.Ordinal))continue;
        var values=text.Split('\0');foreach(var key in new[]{"language","optimization","platform","define"}){var at=Array.IndexOf(values,key);if(at>=0&&at+1<values.Length)Console.WriteLine($"OPTION {item.Item1} {key}={values[at+1]}");}
    }
}
var testDll=Path.Combine(evidence,"runtime/KernelTests.dll");
Check(Hash(testDll)=="cffe5f08250dca8dcb6eddf380cba78ae3f5e3bd4a7036ac15ceb25e1e0f9f83","exact final tests artifact");
using(var file=File.OpenRead(Path.ChangeExtension(testDll,".pdb")))
using(var provider=MetadataReaderProvider.FromPortablePdbStream(file))
{
    var pdb=provider.GetMetadataReader();
    using var testFile=File.OpenRead(testDll);using var testPe=new PEReader(testFile);
    var id=new BlobContentId(pdb.DebugMetadataHeader!.Id);var entry=testPe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView);var cv=testPe.ReadCodeViewDebugDirectoryData(entry);
    Check(cv.Guid==id.Guid&&entry.Stamp==id.Stamp,"tests DLL/PDB association");
    var wanted=new HashSet<string>{"NestedBlendCacheEmptyReadSetTests.cs","NestedBlendBoundsInitializationContractTests.cs","NestedBlendBoundsTests.cs","BlendIntersectionTests.cs"};
    foreach(var h in pdb.Documents){var doc=pdb.GetDocument(h);var name=pdb.GetString(doc.Name).Replace('\\','/');var leaf=Path.GetFileName(name);if(!wanted.Contains(leaf))continue;var at=name.IndexOf("/tests/KernelTests/",StringComparison.Ordinal);Check(at>=0&&Hash(Path.Combine(root,name[(at+1)..]))==Convert.ToHexStringLower(pdb.GetBlobBytes(doc.Hash)),"fixed test source "+leaf);wanted.Remove(leaf);}
    Check(wanted.Count==0,"all selected test source checksums");
}
var baseDump=Path.Combine(evidence,"original/base-dump.txt");var guardDump=Path.Combine(evidence,"original/guard-dump.txt");
Check(File.ReadAllBytes(baseDump).AsSpan().SequenceEqual(File.ReadAllBytes(guardDump)),"existing base/guard dump exact bytes");
Console.WriteLine($"DUMP sha256={Hash(baseDump)} lines={File.ReadAllLines(baseDump).Length}; not timing/DRAM evidence");
foreach(var file in new[]{"base.trx","guard.trx","base-section.trx","guard-section.trx"})
{var xml=XDocument.Load(Path.Combine(evidence,"original",file));var results=xml.Descendants().Where(x=>x.Name.LocalName=="UnitTestResult").ToArray();Check(results.All(x=>(string?)x.Attribute("outcome")=="Passed"),file+" retained results");Console.WriteLine($"TRX {file} results={results.Length}");}
if(!args.Contains("--facts"))return 0;
var runtime=Path.Combine(evidence,"runtime");
AssemblyLoadContext.Default.Resolving+=(_,name)=>File.Exists(Path.Combine(runtime,name.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,name.Name+".dll")):null;
var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(runtime,"ProjectGmKernel.Native.dll"));
Console.WriteLine($"LOADED_NATIVE relative={Path.GetRelativePath(root,native.Location)} sha256={Hash(native.Location)} MVID={native.ManifestModule.ModuleVersionId}");
var tests=AssemblyLoadContext.Default.LoadFromAssemblyPath(testDll);
var cases=new[]{("NestedBlendCacheEmptyReadSetTests","GuardScansBothSidesOfEveryOccurrenceAndTreatsAnyNonnegativeIndexAsLive"),("NestedBlendCacheEmptyReadSetTests","ActualJacobianSkipsOnlyEmptyReadSetsAndInvalidatesTheWholeCacheBeforeFailedFill"),("NestedBlendCacheEmptyReadSetTests","SharedUvFailedFillRetryAndDifferentProgramCannotReuseStaleReadyValues"),("NestedBlendCacheEmptyReadSetTests","SupportingBlendSectionConsumersKeepIndependentChecksWithReuseDisabled"),("NestedBlendBoundsInitializationContractTests","OptionalConsumersAreBitwiseStableWithSplineContactReuseDisabled"),("NestedBlendBoundsTests","CertifiedTubeEnclosesPositionsAndFirstDerivativesThroughoutAParameterCell")};
foreach(var c in cases)
{
    try{var type=tests.GetType("KernelTests."+c.Item1,true)!;var instance=Activator.CreateInstance(type)!;try{type.GetMethod(c.Item2)!.Invoke(instance,null);}finally{(instance as IDisposable)?.Dispose();}Console.WriteLine("PASS ACTUAL_FACT "+c.Item2);}
    catch(Exception ex){Console.WriteLine("FAIL ACTUAL_FACT "+c.Item2+" "+(ex is TargetInvocationException t?t.InnerException:ex));return 1;}
}
Console.WriteLine("PASS actual-facts=6; serial direct invocation, not VSTest discovery or a geometry benchmark");return 0;
static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
static void Check(bool ok,string label){if(!ok)throw new InvalidDataException(label);Console.WriteLine("PASS "+label);}
static string Here([CallerFilePath]string path="")=>path;
