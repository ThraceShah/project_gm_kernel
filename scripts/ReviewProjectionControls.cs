#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using SplineDegree=System.Int32;
global using GeomTag=System.Int32;
global using BufferOffset=System.Int32;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;
using static parasolid;

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string output="temp_docs/independent-projection-cc47871/controls.json";
Require(!File.Exists(output),"preserve control evidence");
var report=new JsonObject{["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeDllSha256"]=Hash(typeof(KernelRuntime).Assembly.Location)};
var input=new IntervalNumber[BezierSurfaceBounds.MaxScalars];var net=new IntervalNumber[input.Length+7];var independent=new IntervalNumber[input.Length];
Span<IntervalNumber> full=stackalloc IntervalNumber[9],position=stackalloc IntervalNumber[4],convenience=stackalloc IntervalNumber[3];
var combinations=0;var exactChecks=0;var statuses=new JsonObject();
foreach(var rational in new[]{false,true})for(SplineDegree p=0;p<=16;p++)for(SplineDegree q=0;q<=16;q++)
{
    var size=4*(p+1)*(q+1);
    for(var u=0;u<=p;u++)for(var v=0;v<=q;v++)for(var d=0;d<4;d++)input[4*(u*(q+1)+v)+d]=d==3?Weight(u,v,rational)/64.0:Pole(u,v,d)/1024.0;
    var patch=new BezierSurfacePatch(input.AsSpan(0,size),p,q,new(-2,2,4,8));
    foreach(var bounds in new[]{new[]{16,48,16,48},new[]{32,32,32,32},new[]{0,0,0,64},new[]{-1,4,60,65}})
    {
        var box=new SurfaceParameterBox(-2+bounds[0]/16.0,-2+bounds[1]/16.0,4+bounds[2]/16.0,4+bounds[3]/16.0);
        net.AsSpan().Fill(12345);position[3]=12345;
        var expected=BezierSurfaceBounds.Bound(patch,box,full);var actual=BezierSurfaceBounds.BoundPosition(patch,box,position,net);
        Require(expected==actual&&actual==BezierSurfaceBounds.BoundPosition(patch,box,convenience),"status equivalence");
        Require(net.AsSpan(size).ToArray().All(x=>x.Lower==12345&&x.Upper==12345)&&position[3].Lower==12345,"active prefix and output sentinel");
        statuses[actual.ToString()]=(int?)statuses[actual.ToString()]+1??1;
        if(actual!=AlgorithmStatus.Success)continue;
        for(var d=0;d<3;d++){Bits(full[d],position[d]);Bits(full[d],convenience[d]);}
        Require(BezierSurfaceBounds.Restrict(patch,box,independent)==AlgorithmStatus.Success,"independent restriction");for(var i=0;i<size;i++)Bits(independent[i],net[i]);
        // Independent exact Bernstein sums: integer controls, dyadic parameters,
        // BigInteger numerator/denominator; no production interval operations.
        foreach(var ku in new[]{bounds[0],(bounds[0]+bounds[1])/2,bounds[1]})foreach(var kv in new[]{bounds[2],(bounds[2]+bounds[3])/2,bounds[3]})
        {
            BigInteger weight=0;var xyz=new BigInteger[3];
            for(var u=0;u<=p;u++)for(var v=0;v<=q;v++)
            {
                var coefficient=Bernstein(p,u,ku)*Bernstein(q,v,kv);weight+=coefficient*Weight(u,v,rational);
                for(var d=0;d<3;d++)xyz[d]+=coefficient*Pole(u,v,d);
            }
            Require(weight.Sign>0,"exact positive weight");
            for(var d=0;d<3;d++){var exact=new Q(xyz[d],16*weight);Require(Q.From(position[d].Lower)<=exact&&exact<=Q.From(position[d].Upper),"independent exact enclosure");exactChecks++;}
        }
        combinations++;
    }
}
// Capacity, invalid domain/box, nonpositive weight, and coordinate overflow.
var simple=new BezierSurfacePatch(input.AsSpan(0,16),1,1,new(0,1,0,1));var unit=new SurfaceParameterBox(0,1,0,1);
Require(BezierSurfaceBounds.BoundPosition(simple,unit,position[..2],net)==AlgorithmStatus.WorkspaceTooSmall,"short output");
Require(BezierSurfaceBounds.BoundPosition(simple,unit,position,net.AsSpan(0,15))==AlgorithmStatus.WorkspaceTooSmall,"short workspace");
Require(BezierSurfaceBounds.BoundPosition(simple,new(1,0,0,1),position,net)==AlgorithmStatus.InvalidInput,"invalid box");
Require(BezierSurfaceBounds.BoundPosition(new(input.AsSpan(0,16),1,1,new(0,0,0,1)),unit,position,net)==AlgorithmStatus.InvalidInput,"invalid domain");
input.AsSpan().Clear();Require(BezierSurfaceBounds.BoundPosition(simple,unit,position,net)==AlgorithmStatus.NumericalFailure,"zero weights");
for(var i=0;i<4;i++)input[4*i+3]=-1;Require(BezierSurfaceBounds.BoundPosition(simple,unit,position,net)==AlgorithmStatus.NumericalFailure,"negative weights");
for(var i=0;i<4;i++){input[4*i]=double.MaxValue;input[4*i+3]=.5;}
Require(BezierSurfaceBounds.BoundPosition(simple,unit,position,net)==AlgorithmStatus.NumericalFailure,"coordinate quotient overflow");
// Position can be finite when omitted derivative arithmetic overflows. It
// does not claim regularity; full Newton jets still come from original Bound.
input.AsSpan().Clear();for(var i=0;i<4;i++){input[4*i]=i<2?-1e300:1e300;input[4*i+3]=1;}
var steep=new BezierSurfacePatch(input.AsSpan(0,16),1,1,new(0,1e-300,0,1));
Require(BezierSurfaceBounds.BoundPosition(steep,new(0,0,0,0),position,net)==AlgorithmStatus.Success,"position despite derivative overflow");
Require(BezierSurfaceBounds.Bound(steep,new(0,0,0,0),full)==AlgorithmStatus.Success&&!full[3].Finite,"original derivative status preserved");
input.AsSpan().Clear();for(var i=0;i<4;i++){input[4*i]=i/2;input[4*i+1]=i%2;input[4*i+3]=1;}
BezierSurfaceBounds.BoundPosition(simple,unit,position,net);BezierSurfaceBounds.BoundPosition(simple,unit,convenience);
var before=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<100;i++){BezierSurfaceBounds.BoundPosition(simple,unit,position,net);BezierSurfaceBounds.BoundPosition(simple,unit,convenience);}var allocation=GC.GetAllocatedBytesForCurrentThread()-before;Require(allocation==0,"both warmed paths zero managed allocation");
// Global nearest point has known finite-domain minima for interior, edge and
// corner targets. Each call traverses Improve and its independent stack nets.
foreach(var target in new[]{new KernelVector3{X=.25,Y=.75,Z=2},new KernelVector3{X=-1,Y=.5,Z=2},new KernelVector3{X=2,Y=2,Z=1}})
{
    var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Require(BSplineProjection.Closest(simple,target,1e-9,ref best)==AlgorithmStatus.Success,"global plane minimum");
    var x=Math.Clamp(target.X,0,1);var y=Math.Clamp(target.Y,0,1);var distance=Math.Sqrt(Math.Pow(target.X-x,2)+Math.Pow(target.Y-y,2)+target.Z*target.Z);
    Require(Math.Abs(best.U-x)<1e-8&&Math.Abs(best.V-y)<1e-8&&best.DistanceUpper>=distance&&best.DistanceUpper<=distance+1e-9,"known nearest point and upper distance");
}
report["bounds"]=new JsonObject{["degreePairs"]=578,["successfulBoxes"]=combinations,["exactCoordinateChecks"]=exactChecks,["statuses"]=statuses,["warmManagedBytes"]=allocation,["capacityInvalidWeightOverflowControls"]=true,["globalKnownMinima"]=3};
if(!ParasolidScriptHost.TryStartSession("Projection independent sealed products",out var host,out var message))throw new Exception(message);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");report["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
    var pairs=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
    foreach(var item in new[]{("quality/00-curved-candidate",0),("dev-candidate/00-solidblend-candidate",5),("active-shortedge-lifecycle-capture",4)})
    {
        var folder="temp_docs/independent-projection-cc47871/original/temp_docs/projection-bound/"+item.Item1;
        var gm=Product(folder,$"gm-{item.Item2}-after-0.json");var pk=Product(folder,$"pk-{item.Item2}-after-0.json");var actual=Receive(gm);var reference=Receive(pk);
        try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,item.Item1);pairs[^1]!["gmXtSha256"]=Hash(gm);pairs[^1]!["pkXtSha256"]=Hash(pk);pairs[^1]!["originalRep"]=item.Item2;}
        finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
    }
    report["sealedProducts"]=pairs;
}
File.WriteAllText(output,report.ToJsonString());Console.WriteLine($"PASS independent controls: boxes={combinations}, exact coordinates={exactChecks}, allocation={allocation}, PK pairs=3");
static long Pole(int u,int v,int d)=>d==0?7*u-3*v+5:d==1?2*u+5*v-9:u*u-2*v*v+u*v;
static long Weight(int u,int v,bool rational)=>rational?64+u+v:64;
static BigInteger Bernstein(SplineDegree n,BufferOffset i,int k){BigInteger choose=1;for(var j=1;j<=i;j++)choose=choose*(n-j+1)/j;return choose*BigInteger.Pow(k,i)*BigInteger.Pow(64-k,n-i);}
static void Bits(IntervalNumber a,IntervalNumber b)=>Require(BitConverter.DoubleToInt64Bits(a.Lower)==BitConverter.DoubleToInt64Bits(b.Lower)&&BitConverter.DoubleToInt64Bits(a.Upper)==BitConverter.DoubleToInt64Bits(b.Upper),"bitwise position/net equivalence");
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Check(int error,string label)=>Require(error==0,label+" error="+error);
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Product(string folder,string name){var source=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;Require(Hash(path)==(string?)source["xtSha256"],"sealed product hash binding");return path;}
static unsafe GeomTag Receive(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;PK_PART_t* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};int count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}
readonly struct Q(BigInteger n,BigInteger d)
{
    internal readonly BigInteger N=n,D=d;
    internal static Q From(double x){var bits=BitConverter.DoubleToInt64Bits(x);var exponent=(int)((bits>>52)&2047);BigInteger mantissa=(ulong)bits&0xfffffffffffffUL;if(exponent!=0)mantissa+=BigInteger.One<<52;var shift=exponent==0?-1074:exponent-1075;if(bits<0)mantissa=-mantissa;return shift>=0?new(mantissa<<shift,1):new(mantissa,BigInteger.One<<-shift);}
    public static bool operator <=(Q a,Q b)=>a.N*b.D<=b.N*a.D;
    public static bool operator >=(Q a,Q b)=>b<=a;
}
