#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using GeomTag=System.Int32;
global using BufferOffset=System.Int32;
global using BufferCount=System.Int32;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string output="temp_docs/independent-section-2c0e4ea/controls.json";
Require(!File.Exists(output),"preserve independent execution");
var report=new JsonObject{["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeSha256"]=Hash(typeof(KernelRuntime).Assembly.Location)};
var type=typeof(SurfacePlaneIntersection);var section=type.GetNestedType("SectionRoots",BindingFlags.NonPublic)!;
var sizeMethod=typeof(Unsafe).GetMethod("SizeOf")!;var sectionSize=(int)sizeMethod.MakeGenericMethod(section).Invoke(null,null)!;
var baseArrays=128*Unsafe.SizeOf<SurfaceParameterBox>()+128*sizeof(int)+18*Unsafe.SizeOf<IntervalNumber>()+64*Unsafe.SizeOf<SurfaceIntersectionNode>()+32*Unsafe.SizeOf<OperationIntersectionPoint>()+32*Unsafe.SizeOf<ScalarRootIsolation>();
report["stack"]=new JsonObject{["sectionEntryBytes"]=sectionSize,["entries"]=64,["additionalBytes"]=64*sectionSize,["findExistingArrayBytes"]=baseArrays,["findAllExplicitArrayBytes"]=baseArrays+64*sectionSize,["rootFindExplicitArrayBytes"]=256*sizeof(double)+128*sizeof(int)+18*Unsafe.SizeOf<IntervalNumber>(),["pointBytes"]=Unsafe.SizeOf<OperationIntersectionPoint>(),["isolationBytes"]=Unsafe.SizeOf<ScalarRootIsolation>(),["nodeBytes"]=Unsafe.SizeOf<SurfaceIntersectionNode>()};
// A >2-root complete finite scalar set still has its original output capacity.
var poles=new IntervalNumber[32];var z=new[]{-3.0/32,13.0/96,-13.0/96,3.0/32};
for(var u=0;u<4;u++)for(var v=0;v<2;v++){var at=4*(2*u+v);poles[at]=u/3.0;poles[at+1]=v;poles[at+2]=z[u];poles[at+3]=1;}
var patch=new SurfaceIntersectionPatch(new BezierSurfacePatch(poles,3,1,new(0,1,0,1)));
var plane=new AnalyticSurface(SurfaceClass.Plane,default,new(){Z=1},new(){X=1});
var curve=new SurfaceIntersectionPatch(patch,false,.5,0,1);Span<OperationIntersectionPoint> roots=stackalloc OperationIntersectionPoint[32];var nr=0;
Require(SurfaceIsoPlaneRoots.Find(curve,plane,0,1,roots,ref nr)==AlgorithmStatus.Success&&nr==3,"complete three-root scalar output");
nr=0;Require(SurfaceIsoPlaneRoots.Find(curve,plane,0,1,roots[..2],ref nr)==AlgorithmStatus.OutputTooSmall,"three roots cannot fit two outputs");
var reuse=type.GetField("ReuseSectionRoots",BindingFlags.Static|BindingFlags.NonPublic)!;var hits=type.GetField("TestSectionReuses",BindingFlags.Static|BindingFlags.NonPublic)!;
Require(reuse!=null&&hits!=null,"testhooks assembly required");
var rows=new JsonArray();
try
{
    foreach(var mode in new[]{"three-branches","no-root-support","shifted-source","strict","output-small","budget-empty"})
    {
        var source=poles.ToArray();if(mode=="shifted-source")for(var i=2;i<source.Length;i+=4)source[i]+=2;
        var input=new SurfaceIntersectionPatch(new BezierSurfacePatch(source,3,1,new(0,1,0,1)));
        var support=mode=="no-root-support"?new AnalyticSurface(SurfaceClass.Plane,new(){Z=2},new(){Z=1},new(){X=1}):plane;
        var a=new SurfaceIntersectionNode[512];var b=new SurfaceIntersectionNode[512];var ea=new SurfaceIntersectionSegment[512];var eb=new SurfaceIntersectionSegment[512];var na=0;var nb=0;var nea=0;var neb=0;var ba=mode=="budget-empty"?0:32768;var bb=ba;
        reuse!.SetValue(null,false);var expected=SurfacePlaneIntersection.Find(input,support,mode=="output-small"?a.AsSpan(0,1):a,ref na,ea,ref nea,ref ba,strictSeparation:mode=="strict");
        reuse.SetValue(null,true);hits!.SetValue(null,0L);var status=SurfacePlaneIntersection.Find(input,support,mode=="output-small"?b.AsSpan(0,1):b,ref nb,eb,ref neb,ref bb,strictSeparation:mode=="strict");
        Require(expected==status&&na==nb&&nea==neb&&ba==bb,"complete graph/status/budget on-off "+mode);
        // Compare all values, never compare unspecified struct padding.
        for(var i=0;i<na;i++)Nodes(a[i],b[i]);for(var i=0;i<nea;i++){Require(ea[i].Start==eb[i].Start&&ea[i].End==eb[i].End,"edge connectivity");Bits(ea[i].ChordalError,eb[i].ChordalError);}
        if(mode=="three-branches")Require(status==AlgorithmStatus.Success&&Components(b.AsSpan(0,nb),eb.AsSpan(0,neb))==3&&(long)hits.GetValue(null)!>0,"three finite branches with actual reuse");
        if(mode is "no-root-support" or "shifted-source")Require(status==AlgorithmStatus.Success&&nb==0&&neb==0,"new input cannot borrow previous roots");
        if(mode=="output-small")Require(status==AlgorithmStatus.OutputTooSmall,"output capacity retained");
        if(mode=="budget-empty")Require(status==AlgorithmStatus.NotConverged&&nb==0&&neb==0,"new failed call cannot inherit prior success");
        rows.Add((JsonNode)new JsonObject{["case"]=mode,["status"]=status.ToString(),["nodes"]=nb,["edges"]=neb,["remaining"]=bb,["actualHits"]=(long)hits.GetValue(null)!});
    }
}
finally{reuse!.SetValue(null,true);}
report["controls"]=rows;
if(!ParasolidScriptHost.TryStartSession("Section independent sealed products",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");report["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
    var pairs=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
    foreach(var item in new[]{("quality/00-curved-candidate",0),("quality/01-solidblend-candidate",0),("active-shortedge-lifecycle-capture",4)})
    {
        var folder="temp_docs/independent-section-2c0e4ea/original/temp_docs/section-work/"+item.Item1;
        var gm=Product(folder,$"gm-{item.Item2}-after-0.json");var pk=Product(folder,$"pk-{item.Item2}-after-0.json");var actual=Receive(gm);var reference=Receive(pk);
        try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,item.Item1);pairs[^1]!["gmXtSha256"]=Hash(gm);pairs[^1]!["pkXtSha256"]=Hash(pk);pairs[^1]!["originalStep"]=item.Item2;}
        finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
    }
    report["sealedProducts"]=pairs;
}
File.WriteAllText(output,report.ToJsonString());Console.WriteLine("PASS independent scalar/graph/stack controls and three sealed real PK pairs");
static void Nodes(SurfaceIntersectionNode a,SurfaceIntersectionNode b){foreach(var p in new[]{(a.Point.X,b.Point.X),(a.Point.Y,b.Point.Y),(a.Point.Z,b.Point.Z),(a.U,b.U),(a.V,b.V),(a.S,b.S),(a.T,b.T),(a.SpatialError,b.SpatialError),(a.Isolation.Root.Lower,b.Isolation.Root.Lower),(a.Isolation.Root.Upper,b.Isolation.Root.Upper),(a.Isolation.MonotoneDomain.Lower,b.Isolation.MonotoneDomain.Lower),(a.Isolation.MonotoneDomain.Upper,b.Isolation.MonotoneDomain.Upper)})Bits(p.Item1,p.Item2);Require(a.RootAxis==b.RootAxis&&a.RemappedParameters==b.RemappedParameters&&a.Isolation.Certified==b.Isolation.Certified&&a.Isolation.Increasing==b.Isolation.Increasing,"all node flags");}
static BufferCount Components(ReadOnlySpan<SurfaceIntersectionNode> nodes,ReadOnlySpan<SurfaceIntersectionSegment> edges){var parent=Enumerable.Range(0,nodes.Length).ToArray();foreach(var e in edges){var a=Root(e.Start);var b=Root(e.End);parent[a]=b;}return Enumerable.Range(0,nodes.Length).Select(Root).Distinct().Count();BufferOffset Root(BufferOffset at){while(parent[at]!=at)at=parent[at];return at;}}
static void Bits(double a,double b)=>Require(BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b),"bitwise graph field");
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Check(int error,string label)=>Require(error==0,label+" error="+error);
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Product(string folder,string name){var source=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;Require(Hash(path)==(string?)source["xtSha256"],"sealed product hash binding");return path;}
static unsafe GeomTag Receive(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};BufferCount count;PK_PART_t* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};BufferCount count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}
