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
global using ApiErrorCode=System.Int32;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string root="temp_docs/independent-localjet-ab0177e";
Require(!File.Exists(root+"/controls.json"),"preserve execution");
if(!ParasolidScriptHost.TryStartSession("Independent local complete spline jet",out var host,out var message))throw new Exception(message);
using var session=host;
var result=new JsonObject{["scope"]="Ordinary managed candidate, private routines bound to their actual types; manufactured controls and sealed actual GM vs independent PK products, no timing qualification",["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeSha256"]=Hash(typeof(KernelRuntime).Assembly.Location)};
var type=typeof(JointNestedBlendEvaluation);
result["localCache"]= (JsonNode)typeof(CacheChecks).GetMethod(nameof(CacheChecks.Run))!.MakeGenericMethod(type.GetNestedType("Occurrence",BindingFlags.NonPublic)!,type.GetNestedType("IntervalSplineContact",BindingFlags.NonPublic)!).Invoke(null,null)!;
unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");result["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
    var pairs=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
    var bounds=new JsonArray();var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM start");
    try
    {
        foreach(var item in new[]{("quality/00-curved-candidate",0),("dev-candidate/00-solidblend-candidate",5),("active-shortedge-lifecycle-capture",4)})
        {
            var folder=root+"/quality/original/temp_docs/local-spline-jet/"+item.Item1;var gm=Product(folder,$"gm-{item.Item2}-after-0.json");var pk=Product(folder,$"pk-{item.Item2}-after-0.json");var actual=Receive(gm,false);var reference=Receive(pk,false);
            try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,item.Item1);pairs[^1]!["gmXtSha256"]=Hash(gm);pairs[^1]!["pkXtSha256"]=Hash(pk);pairs[^1]!["originalStep"]=item.Item2;}
            finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
            if(item.Item1.StartsWith("dev-",StringComparison.Ordinal))
            {
                var own=Receive(gm,true);try
                {
                    Require(KernelRuntime.TryResolveBodySlot(own,out var slot),"GM body");var face=KernelRuntime.Bodies[slot].FirstFaceBody;var count=0;
                    CommandScratch.Bind(KernelRuntime.State.Session,0);
                    try{for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
                    {
                        var sf=KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag);var record=KernelRuntime.Surfaces[sf];if(record.Class!=SurfaceClass.BlendSurface)continue;
                        var data=KernelRuntime.BlendedEdgeDataPool[record.DataIndex];var program=KernelRuntime.GetNestedBlendProgram(data);var u=record.UMin+.47*(record.UMax-record.UMin);var box=new SurfaceParameterBox(u,u+.002*(record.UMax-record.UMin),.2,.8);
                        Span<IntervalNumber> a=stackalloc IntervalNumber[9],b=stackalloc IntervalNumber[9];JointNestedBlendEvaluation.ReuseSplineContactBounds=false;var off=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,box,a);JointNestedBlendEvaluation.ReuseSplineContactBounds=true;var on=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,box,b);
                        Require(off==AlgorithmStatus.Success&&on==off,"actual GM full bounds");CacheChecks.Bits(a,b);JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,box,b);
                        var before=GC.GetAllocatedBytesForCurrentThread();var hot=JointNestedBlendEvaluation.Bound(program,data.ProgramRoot,box,b);var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Require(hot==AlgorithmStatus.Success&&bytes==0,"actual bound zero managed allocation");
                        bounds.Add((JsonNode)new JsonObject{["sourceXtSha256"]=Hash(gm),["blendSurfaceTag"]=record.Header.Tag,["nineEnclosuresBitwise"]=true,["hotManagedBytes"]=bytes});count++;
                    }}finally{CommandScratch.Unbind(KernelRuntime.State.Session,0);JointNestedBlendEvaluation.ReuseSplineContactBounds=true;}
                    Require(count>0,"actual implicit blends checked");
                }finally{Check(KernelRuntime.EntityDelete(1,&own),"delete GM");}
            }
        }
        result["sealedProducts"]=pairs;result["actualGmBounds"]=bounds;result["pkSplineDerivativeControls"]=SplineControls();
    }
    finally{KernelRuntime.SessionStop();}
}
File.WriteAllText(root+"/controls.json",result.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("PASS independent local scope, full matrices/bounds, allocation and real PK product/derivative controls");

static unsafe JsonArray SplineControls()
{
    var rows=new JsonArray();foreach(var rational in new[]{false,true})
    {
        var data=CacheChecks.Data(0,rational);var source=CacheChecks.Surface(rational);var program=new NestedBlendProgram([source],[],[],data,new double[2048]);
        double* knots=stackalloc double[2]{0,1};BufferCount* mult=stackalloc BufferCount[2]{3,3};GeomTag surf;
        fixed(double* poles=data)
        {
            var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=source.Dimension,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,vertex=poles,form=PK_BSURF_form_unset_c,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=mult,v_knot_mult=mult,u_knot_type=PK_knot_unset_c,v_knot_type=PK_knot_unset_c,self_intersecting=PK_self_intersect_unset_c,convexity=PK_convexity_unset_c};Check(PK_BSURF_create(&sf,&surf),"independent PK spline");
        }
        try
        {
            var max=0.0;var observations=0;PK_VECTOR_t* actual=stackalloc PK_VECTOR_t[9];Span<KernelVector3> expected=stackalloc KernelVector3[9];SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<double> workspace=stackalloc double[2048];Span<IntervalNumber> hull=stackalloc IntervalNumber[18];
            foreach(var (u,v) in new[]{(.23,.67),(0.0,.67),(-.03,1.04),(1.03,-.02)})
            {
                var uv=new PK_UV_t();uv.param[0]=u;uv.param[1]=v;Check(PK_SURF_eval(surf,uv,2,2,PK_LOGICAL_false,actual),"PK native derivatives");Require(BSurfaceEvaluation.Evaluate(program.Spline(source),u,v,layout,workspace,expected)==AlgorithmStatus.Success,"GM derivatives");Require(JointNestedBlendEvaluation.IntervalSplineBounds(program,source,new(u),new(v),hull)==AlgorithmStatus.Success,"complete jet enclosure");
                // PK rectangular output has u varying fastest; the internal
                // SurfaceDerivativeLayout has v varying fastest.
                for(var i=0;i<9;i++)for(var axis=0;axis<3;axis++){var item=expected[layout.GetIndex(i%3,i/3)];var e=axis==0?item.X:axis==1?item.Y:item.Z;var a=actual[i].coord[axis];max=Math.Max(max,Math.Abs(a-e));Require(Math.Abs(a-e)<1e-10,"native derivative agreement");}
                BufferOffset[] indices=[0,1,3,2,4,6];for(var k=0;k<6;k++)for(var axis=0;axis<3;axis++){var x=actual[indices[k]].coord[axis];Require(x>=hull[3*k+axis].Lower-1e-12&&x<=hull[3*k+axis].Upper+1e-12,"PK derivative inside original enclosure, rounding slack control only");}observations++;
            }
            rows.Add((JsonNode)new JsonObject{["rational"]=rational,["nativeUvObservations"]=observations,["derivativesPerStation"]=9,["maxAbsoluteResidual"]=max,["scope"]="Point controls, not universal continuous enclosure proof"});
        }finally{Check(PK_ENTITY_delete(1,&surf),"delete spline");}
    }return rows;
}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Check(ApiErrorCode error,string label)=>Require(error==0,label+" error="+error);
static string Product(string folder,string name){var source=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;Require(Hash(path)==(string?)source["xtSha256"],"sealed actual product binding");return path;}
static unsafe GeomTag Receive(string path,bool own){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};BufferCount count;GeomTag* parts;Check(own?KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&count,&parts):PK_PART_receive_b(block,&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(own?KernelRuntime.MemoryFree(parts):PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};BufferCount count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}

static class CacheChecks
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static;
    delegate AlgorithmStatus Jac<TN,TC>(in NestedBlendProgram program,ReadOnlySpan<TN> nodes,ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,Span<IntervalNumber> matrix,Span<IntervalDual> variables,Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> charts,Span<TC> cache,bool cartesianSpline=false,bool localSplineScope=false);
    delegate AlgorithmStatus Fill<TC>(in NestedBlendProgram program,in BlendSurfaceInstruction source,IntervalNumber u,IntervalNumber v,ref TC contact);
    delegate bool Match<TC>(in TC contact,in BlendSurfaceInstruction source,IntervalNumber u,IntervalNumber v);
    public static JsonObject Run<TN,TC>() where TN:unmanaged where TC:unmanaged
    {
        var owner=typeof(JointNestedBlendEvaluation);var jac=owner.GetMethod("IntervalJacobian",Private)!.CreateDelegate<Jac<TN,TC>>();var fill=owner.GetMethod("CacheSplineContact",Private)!.CreateDelegate<Fill<TC>>();var match=owner.GetMethod("SplineContactMatches",Private)!.CreateDelegate<Match<TC>>();
        var node=new TN[1];foreach(var field in new[]{"Parent","FirstChild","SecondChild"})Set(ref node[0],field,-1);Set(ref node[0],"FirstUv",3);Set(ref node[0],"SecondUv",3);
        var cache=new TC[5];var fresh=new TC[5];IntervalNumber[] boxes=[0,.37,0,new(.2,.3),new(.6,.7)];var actual=new IntervalNumber[25];var expected=new IntervalNumber[25];var variables=new IntervalDual[5];var equations=new IntervalDual[5];BufferOffset[] charts=[0];var rows=new JsonArray();
        foreach(var rational in new[]{false,true})
        {
            var source=Surface(rational);var surfaces=new[]{source,source};var scalars=Data(0,rational);var spines=new BlendSpineInstruction[]{new(){First=0,Second=1,ChartCount=2}};var controls=new BlendChartInstruction[]{new(){Chord=new(){Y=1},Scale=1},new(){Position=new(){Y=1},Parameter=1,Scale=1}};var program=new NestedBlendProgram(surfaces,spines,controls,scalars,new double[2048]);cache.AsSpan().Clear();
            void Status(AlgorithmStatus s,string label){if(s!=AlgorithmStatus.Success)throw new Exception(label+" "+s);}
            Status(jac(program,node,boxes,.37,actual,variables,equations,charts,cache,false,true),"first fill");Require(match(cache[3],source,boxes[3],boxes[4]),"ready full jet");
            // Poison ONLY the cached values to distinguish a real cross-Jac read
            // from recomputation. This manufactured invalid entry is then discarded.
            var snapshot=cache[3];var field=typeof(TC).GetField("Values",BindingFlags.NonPublic|BindingFlags.Instance)!;var bytes=MemoryMarshal.AsBytes(cache.AsSpan());var offset=Marshal.OffsetOf<TC>(field.Name).ToInt32();var length=Marshal.SizeOf(field.FieldType);bytes.Slice(3*Unsafe.SizeOf<TC>()+offset,length).Fill(0xFF);
            Require(jac(program,node,boxes,.37,actual,variables,equations,charts,cache,false,true)==AlgorithmStatus.NotConverged,"same scope truly consumes prior jet");cache[3]=snapshot;
            foreach(var change in new[]{0,1,2,3})
            {
                if(change==1)boxes[3]=new(.21,.31);if(change==2){boxes[0]=new(.1);charts[0]=0;}if(change==3)boxes[3]=new(-0.0);
                Status(jac(program,node,boxes,new(.36,.38),actual,variables,equations,charts,cache,false,true),"local matrix");Status(jac(program,node,boxes,new(.36,.38),expected,variables,equations,charts,fresh),"standalone matrix");Bits(actual,expected);
            }
            Status(fill(program,source,new(0.0),boxes[4],ref cache[3]),"positive zero fill");Require(!match(cache[3],source,new(-0.0),boxes[4]),"signed zero isolation");
            source.UPeriodic=true;Status(fill(program,source,new(.2,.3),boxes[4],ref cache[3]),"periodic valid fill");Require(fill(program,source,new(1e15),boxes[4],ref cache[3])==AlgorithmStatus.NotConverged&&!match(cache[3],source,new(.2,.3),boxes[4]),"failed refresh invalidates old successful key");Status(fill(program,source,new(.2,.3),boxes[4],ref cache[3]),"retry");
            // A different owner with equal numeric metadata gets a new local span;
            // standalone private calls also invalidate their caller-supplied span.
            var other=new NestedBlendProgram(surfaces,spines,controls,Data(7,rational),new double[2048]);Status(jac(other,node,boxes,.37,actual,variables,equations,charts,cache),"different owner standalone");Status(jac(other,node,boxes,.37,expected,variables,equations,charts,new TC[5],false,true),"different owner fresh local scope");Bits(actual,expected);
            rows.Add((JsonNode)new JsonObject{["rational"]=rational,["matrixEntries"]=25,["trueCrossJacRead"]=true,["changedUvAndCartesianCoordinatesBitwise"]=true,["signedZeroSeparated"]=true,["failedRefreshRetry"]=true,["distinctOwnerFreshScope"]=true});boxes[3]=new(.2,.3);
        }
        // Empty read set must not even rewrite poisoned entries in local mode.
        Set(ref node[0],"FirstUv",-1);Set(ref node[0],"SecondUv",-1);MemoryMarshal.AsBytes(cache.AsSpan()).Fill(0xA5);var poison=MemoryMarshal.AsBytes(cache.AsSpan()).ToArray();var planes=new BlendSurfaceInstruction[]{new(){Analytic=new(SurfaceClass.Plane,default,new(){X=1},new(){Y=1}),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1}};var planeProgram=new NestedBlendProgram(planes,[new(){First=0,Second=1,ChartCount=2}],[new(){Chord=new(){Y=1},Scale=1},new(){Position=new(){Y=1},Parameter=1,Scale=1}]);
        Require(jac(planeProgram,node,boxes.AsSpan(0,3),.37,actual.AsSpan(0,9),variables,equations,charts,cache,false,true)==AlgorithmStatus.Success&&poison.AsSpan().SequenceEqual(MemoryMarshal.AsBytes(cache.AsSpan())),"empty read set no write");
        return new JsonObject{["controls"]=rows,["emptyReadSetPoisonUntouched"]=true,["scope"]="No permission to keep a span across source edit or a different program"};
    }
    public static BlendSurfaceInstruction Surface(bool rational)=>new(){IsBSpline=true,NormalSense=1,UDegree=2,VDegree=2,UCount=3,VCount=3,Dimension=rational?4:3,Rational=rational};
    public static double[] Data(double shift,bool rational){var dim=rational?4:3;var data=new double[9*dim+12];for(var u=0;u<3;u++)for(var v=0;v<3;v++){var w=rational?1+.15*u+.2*v:1;var at=dim*(3*u+v);data[at]=(u*.5+shift)*w;data[at+1]=v*.5*w;data[at+2]=(.3*u*u-.1*v*v+.07*u*v)*w;if(rational)data[at+3]=w;}double[] knots=[0,0,0,1,1,1];knots.CopyTo(data,9*dim);knots.CopyTo(data,9*dim+6);return data;}
    static void Set<T>(ref T value,string name,BufferOffset number)where T:struct{object boxed=value;typeof(T).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(boxed,number);value=(T)boxed;}
    public static void Bits(ReadOnlySpan<IntervalNumber> a,ReadOnlySpan<IntervalNumber> b){Require(a.Length==b.Length,"same size");for(var i=0;i<a.Length;i++)Require(BitConverter.DoubleToInt64Bits(a[i].Lower)==BitConverter.DoubleToInt64Bits(b[i].Lower)&&BitConverter.DoubleToInt64Bits(a[i].Upper)==BitConverter.DoubleToInt64Bits(b[i].Upper),"bitwise enclosure");}
    static void Require(bool value,string label){if(!value)throw new Exception(label);}
}
