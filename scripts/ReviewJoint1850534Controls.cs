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
global using BlendSurfaceIndex=System.Int32;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));
Directory.SetCurrentDirectory(root);
var report=new JsonObject();
report["q2NativeIdentity"]=new JsonObject{["mvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["dllSha256"]=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location)))};
var surfaces=Surfaces();BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=2},new(){First=0,Second=1,ChartOffset=2,ChartCount=2}];
BlendChartInstruction[] charts=[new(){Chord=new(){Y=1},Scale=1},new(){Position=new(){Y=1},Parameter=1,Scale=1},new(){Chord=new(){Y=1},Scale=2},new(){Position=new(){Y=1},Parameter=2,Scale=1}];
var entries=new JointTubeEntry[8];var p=new NestedBlendProgram(surfaces,spines,charts);var cached=new NestedBlendProgram(surfaces,spines,charts,tubeSource:new(41,1,1,1,7),tubeWork:entries);
Span<IntervalNumber> expected=stackalloc IntervalNumber[9],actual=stackalloc IntervalNumber[9];
var samples=0;var frameDifferences=0;
foreach(var width in new[]{0.0,.0001,.1})foreach(var v in new[]{-.01,0.0,.2,.7,1.0})foreach(BlendSurfaceIndex surface in new[]{4,7})
{
    var box=new SurfaceParameterBox(.4,.4+width,v,v+.01);
    Equal(JointNestedBlendEvaluation.Bound(p,surface,box,expected),JointNestedBlendEvaluation.Bound(cached,surface,box,actual),"frame status");Bits(expected,actual);
    {
        var radius=surface==4?.2:.4;var t=v*Math.PI*.5;
        Contains(actual[0],-radius*Math.Sin(t));Contains(actual[1],.4);Contains(actual[2],-radius*Math.Cos(t));
        Contains(actual[3],0);Contains(actual[4],1);Contains(actual[5],0);
        Contains(actual[6],-radius*Math.PI*.5*Math.Cos(t));Contains(actual[7],0);Contains(actual[8],radius*Math.PI*.5*Math.Sin(t));samples++;
    }
    if(surface==7)frameDifferences++;
}
// Full probing/replacement at fixed capacity, then revisit evicted keys.
for(GeomTag tag=100;tag<124;tag++)
{
    var view=new NestedBlendProgram(surfaces,spines,charts,tubeSource:new(tag,1,1,1,7),tubeWork:entries);
    Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(view,0,new(.3,.31,0,0),actual),"collision fill");Contains(actual[1],.3);
}
Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,0,new(.3,.31,0,0),actual),"collision revisit");Contains(actual[1],.3);
Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.BoundSpine(cached,1,new(.3,.31,0,0),actual),"different root");Contains(actual[1],.15);Require(actual[1].Upper<.2,"root gauge reused incorrectly");
// Failed frame is independent of the successfully proved center tube.
var failureBox=new SurfaceParameterBox(.6,.6001,0,1);
Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.Bound(cached,8,failureBox,actual),"invalid frame");
Require(entries.Any(x=>x.Source.Tag==41&&x.First==BitConverter.DoubleToInt64Bits(.6)),"tube may publish before frame");
Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(cached,4,failureBox,actual),"valid frame after failure");
Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.Bound(p,4,failureBox,expected),"reference after failure");Bits(expected,actual);
var before=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<100;i++)JointNestedBlendEvaluation.Bound(cached,4,failureBox,actual);var allocation=GC.GetAllocatedBytesForCurrentThread()-before;Require(allocation==0,"warm allocation");
report["tube"]=new JsonObject{["analyticChecks"]=samples,["surfaceOrVNegativeControls"]=frameDifferences,["collisionOwners"]=24,["frameFailureThenValid"]=true,["warmManagedBytes"]=allocation};

// Exact rational alternating-series oracle: no Math.Atan/Atan2 reference.
var angleChecks=0;var oldBranchRejected=0;var halfPi=8*AtanSeries(new Q(1,5))-2*AtanSeries(new Q(1,239));
foreach(var x in new[]{-1.0,-.5,-.125,0,.125,.5,1})foreach(var y in new[]{.5,1.0,2})foreach(var direction in new[]{0,1})
{
    Equal(AlgorithmStatus.Success,JointNestedBlendEvaluation.SupportingSectionAngle(new(x,direction==0?1:0),new(y,direction==1?1:0),out var angle),"angle");
    var ratio=Q.From(x)/Q.From(y);var exact=halfPi-Atan(ratio,halfPi);Encloses(angle.Value,exact.Low,exact.High);
    var derivative=(direction==0?-Q.From(y):Q.From(x))/(Q.From(x)*Q.From(x)+Q.From(y)*Q.From(y));Encloses(angle.Derivative,derivative,derivative);angleChecks++;if(x<=0)oldBranchRejected++;
}
foreach(var x in new[]{-1.0,0})Equal(AlgorithmStatus.NotConverged,JointNestedBlendEvaluation.SupportingSectionAngle(new(x,0),new(new(-1,0),0),out _),"branch reject");
report["angle"]=new JsonObject{["exactRationalChecks"]=angleChecks,["oldPositiveXBranchRejected"]=oldBranchRejected,["originNegativeAxisRejected"]=true};

unsafe
{
    Environment.SetEnvironmentVariable("P_SCHEMA",Path.Combine(root,"third_party/parasolid/schema"));
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM session");
    try
    {
        ModelingPerformance.Enabled=true;ModelingPerformance.CacheEnabled=true;
        var scratch=CommandScratch.ThreadArenaOffset;ModelingPerformance.Context failed=default;
        KernelRuntime.InjectAllocationFailure(0);
        using(var scope=new ModelingPerformance.Scope(&failed,immutableGeometry:true))
        {
            Require(ModelingPerformance.JointTubeWorkspace().IsEmpty,"optional allocation failure");KernelRuntime.InjectAllocationFailure(-1);
            Require(ModelingPerformance.JointTubeWorkspace().IsEmpty,"no allocation retry in failed scope");Require(CommandScratch.ThreadArenaOffset==scratch,"proof scratch consumed");
        }
        ModelingPerformance.Context parent=default,child=default,mutable=default;
        using(var scope=new ModelingPerformance.Scope(&parent,immutableGeometry:true))
        {
            var first=ModelingPerformance.JointTubeWorkspace();Require(first.Length==128&&first[0].Source.Tag==0,"initialized workspace");first[0].Source=new(77,1,1,1,7);
            using(var inner=new ModelingPerformance.Scope(&child,immutableGeometry:true))Require(ModelingPerformance.JointTubeWorkspace()[0].Source.Tag==0&&child.Tubes!=parent.Tubes,"nested context storage");
            Require(ModelingPerformance.JointTubeWorkspace()[0].Source.Tag==77,"parent restored");
            using(var inner=new ModelingPerformance.Scope(&mutable))Require(ModelingPerformance.JointTubeWorkspace().IsEmpty,"mutable scope denied");
        }
        Require(ModelingPerformance.JointTubeWorkspace().IsEmpty,"out of scope denied");
        // Two distinct real ICurve owners; fresh native compiler at each call.
        var plane=new M.PK_PLANE_sf_s();plane.basis_set.axis.coord[0]=1;plane.basis_set.ref_direction.coord[1]=1;GeomTag a,b,curve,other;
        Check(KernelRuntime.PlaneCreate(&plane,&a),"x plane");plane=new();plane.basis_set.axis.coord[2]=1;plane.basis_set.ref_direction.coord[0]=1;Check(KernelRuntime.PlaneCreate(&plane,&b),"z plane");
        Check(KernelRuntime.BindNestedBlendSpine(a,b,p,0,&curve),"curve owner");Check(KernelRuntime.BindNestedBlendSpine(a,b,p,0,&other),"second owner");
        ModelingPerformance.Context native=default;long cold,warm,distinct;
        CommandScratch.Bind(KernelRuntime.State.Session,0);Require(CommandScratch.IsBound,"native command scratch");
        using(var scope=new ModelingPerformance.Scope(&native,immutableGeometry:true))
        {
            Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedIntersectionCurveExtent(curve,.2,.3,out _),"fresh compiler cold");cold=native.Profile.JacobianCalls;
            Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedIntersectionCurveExtent(curve,.2,.3,out _),"fresh compiler warm");warm=native.Profile.JacobianCalls-cold;
            Equal(AlgorithmStatus.Success,KernelRuntime.CertifiedIntersectionCurveExtent(other,.2,.3,out _),"different actual owner");distinct=native.Profile.JacobianCalls-cold-warm;
            Require(cold>0&&warm==0&&distinct>0,"real compiler identity reuse");
        }
        CommandScratch.Unbind(KernelRuntime.State.Session,0);
        report["nativeScope"]=new JsonObject{["allocationFailureAndNoRetry"]=true,["scratchUnchanged"]=true,["nestedAndMutableIsolation"]=true,["coldJacobianCalls"]=cold,["warmJacobianCalls"]=warm,["differentOwnerJacobianCalls"]=distinct};
    }
    finally{KernelRuntime.InjectAllocationFailure(-1);ModelingPerformance.Enabled=false;Check(KernelRuntime.SessionStop(),"GM stop");}
    if(!ParasolidScriptHost.TryStartSession("Joint independent product review",out var host,out var message))throw new Exception(message);
    using(host)
    {
        PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"actual PK version");report["q2PkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
        var pairs=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
        foreach(var family in new[]{"active-mirror8-capture","active-nurbs-lifecycle-capture"})
        {
            var folder="temp_docs/independent-joint-1850534/original/temp_docs/joint-work/"+family;
            var gm=ReadProduct(folder,"gm-0-after-0.json");var pk=ReadProduct(folder,"pk-0-after-0.json");
            var product=Receive(gm.Path);var reference=Receive(pk.Path);Full(product);Full(reference);
            ParasolidBodyCompare.AssertEquivalent(reference,product,family);
            pairs[^1]!["gmXtSha256"]=gm.Hash;pairs[^1]!["pkXtSha256"]=pk.Hash;
        }
        report["realPkSealedProducts"]=pairs;
    }
}
Require(args.Length==1&&args[0].StartsWith("--output-name=",StringComparison.Ordinal),"one new output name is required");var outputName=args[0]["--output-name=".Length..];Require(outputName.Length>0&&outputName.All(x=>char.IsAsciiLetterOrDigit(x)||x is '-' or '_'),"safe output name");
var receipt="temp_docs/independent-joint-1850534/"+outputName+".json";Require(!File.Exists(receipt),"preserve control evidence");File.WriteAllText(receipt,report.ToJsonString());Console.WriteLine(report.ToJsonString());
static BlendSurfaceInstruction[] Surfaces()
{
    var x=new KernelVector3{X=1};var y=new KernelVector3{Y=1};var z=new KernelVector3{Z=1};
    return [new(){Analytic=new(SurfaceClass.Plane,default,x,y),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,z,x),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.2},x,y),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},z,x),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){X=-.4},x,y),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.4},z,x),NormalSense=1},new(){IsBlend=true,First=5,Second=6,Spine=0,Range0=.4,Range1=.4,NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=0,Range1=0,NormalSense=1}];
}
static void Equal(AlgorithmStatus a,AlgorithmStatus b,string label)=>Require(a==b,label+" "+a+"/"+b);
static void Contains(IntervalNumber box,double point)=>Require(box.Lower<=point&&point<=box.Upper,"analytic enclosure");
static void Bits(ReadOnlySpan<IntervalNumber>a,ReadOnlySpan<IntervalNumber>b){for(var i=0;i<9;i++)Require(BitConverter.DoubleToInt64Bits(a[i].Lower)==BitConverter.DoubleToInt64Bits(b[i].Lower)&&BitConverter.DoubleToInt64Bits(a[i].Upper)==BitConverter.DoubleToInt64Bits(b[i].Upper),"frame bit equality");}
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Check(int error,string label)=>Require(error==0,label+" error="+error);
static string Source([CallerFilePath]string path="")=>path;
static (string Path,string Hash) ReadProduct(string folder,string name){var mesh=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!;var source=mesh["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;var hash=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));Require(hash==(string?)source["xtSha256"],"product binding");return(path,hash);}
static unsafe GeomTag Receive(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"PK receive");try{Require(count==1,"one product");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"PK full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}
static void Encloses(IntervalNumber box,Q low,Q high){Require(Q.From(box.Lower)<=low&&high<=Q.From(box.Upper),"rational enclosure");}
static QB AtanSeries(Q x){var term=x;var sum=x;for(var i=1;i<240;i++){term=-term*x*x;sum+=term/new Q(2*i+1,1);}var next=-term*x*x/new Q(481,1);return next.N.Sign<0?new(sum+next,sum):new(sum,sum+next);}
static QB Atan(Q x,QB halfPi){if(x.N.Sign<0)return -Atan(-x,halfPi);if(new Q(1,2)<x)return halfPi/2-AtanSeries((new Q(1,1)-x)/(new Q(1,1)+x));return AtanSeries(x);}
readonly record struct QB(Q Low,Q High){public static QB operator +(QB a,QB b)=>new(a.Low+b.Low,a.High+b.High);public static QB operator -(QB a,QB b)=>new(a.Low-b.High,a.High-b.Low);public static QB operator -(QB a)=>new(-a.High,-a.Low);public static QB operator *(QB a,int b)=>new(a.Low*b,a.High*b);public static QB operator *(int b,QB a)=>a*b;public static QB operator /(QB a,int b)=>new(a.Low/new Q(b,1),a.High/new Q(b,1));}
readonly struct Q
{
    internal readonly BigInteger N,D;
    internal Q(BigInteger n,BigInteger d){if(d.Sign<0){n=-n;d=-d;}var g=BigInteger.GreatestCommonDivisor(n,d);N=n/g;D=d/g;}
    internal static Q From(double x){var bits=BitConverter.DoubleToInt64Bits(x);var exponent=(int)((bits>>52)&2047);BigInteger mantissa=(ulong)bits&0xfffffffffffffUL;if(exponent!=0)mantissa+=BigInteger.One<<52;var shift=exponent==0?-1074:exponent-1075;if(bits<0)mantissa=-mantissa;return shift>=0?new(mantissa<<shift,1):new(mantissa,BigInteger.One<<-shift);}
    public static Q operator +(Q a,Q b)=>new(a.N*b.D+b.N*a.D,a.D*b.D);public static Q operator -(Q a,Q b)=>a+-b;public static Q operator -(Q a)=>new(-a.N,a.D);public static Q operator *(Q a,Q b)=>new(a.N*b.N,a.D*b.D);public static Q operator *(Q a,int b)=>new(a.N*b,a.D);public static Q operator /(Q a,Q b)=>new(a.N*b.D,a.D*b.N);public static bool operator <=(Q a,Q b)=>a.N*b.D<=b.N*a.D;public static bool operator >=(Q a,Q b)=>b<=a;public static bool operator <(Q a,Q b)=>!(b<=a);public static bool operator >(Q a,Q b)=>b<a;
}
