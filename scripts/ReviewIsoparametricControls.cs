#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using GeomTag=System.Int32;
global using BufferCount=System.Int32;
global using BufferOffset=System.Int32;
global using ApiErrorCode=System.Int32;
global using EntityClass=System.Int32;
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
const string output="temp_docs/independent-isoparam-e7f0a48/controls.json";
Require(!File.Exists(output),"preserve independent execution");
var report=new JsonObject{["scope"]="Manufactured exact fields, finite consumers, cache isolation, real PK sealed products and native iso semantics; samples are controls, not continuous proofs",["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeSha256"]=Hash(typeof(KernelRuntime).Assembly.Location)};
if(!ParasolidScriptHost.TryStartSession("Independent native isoparametric review",out var host,out var message))throw new Exception(message);
using var pkSession=host;
var controls=new JsonArray();
foreach(var angle in new[]{Math.PI/2,.001,0,-Math.PI/3})foreach(var reverse in new[]{false,true})foreach(var transformed in new[]{false,true})
{
    var radius=transformed?.4:.2;var origin=transformed?new KernelVector3{X=3,Y=-2,Z=5}:default;
    var x=Map(new(){X=1},transformed);var z=Map(new(){Z=1},transformed);var end=Map(new(){X=Math.Cos(angle),Y=Math.Sin(angle)},transformed);
    BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Plane,origin,x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,origin,end,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,Add(origin,Scale(x,-radius)),x,z),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,Add(origin,Scale(end,-radius)),end,z),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=radius,Range1=radius,NormalSense=1,ReverseSection=reverse}];
    BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=CurveClass.Line,Origin=origin,First=z},First=0,Second=1,ChartCount=3}];
    BlendChartInstruction[] charts=[new(){Parameter=2,Position=Add(origin,Scale(z,2)),Scale=1},new(){Parameter=3,Position=Add(origin,Scale(z,3)),Scale=1},new(){Parameter=4,Position=Add(origin,Scale(z,4)),Scale=1}];
    var program=new NestedBlendProgram(surfaces,spines,charts);var patch=new SurfaceIntersectionPatch(program,4,new(2,4,0,1));Span<IntervalNumber> parent=stackalloc IntervalNumber[9],a=stackalloc IntervalNumber[9],b=stackalloc IntervalNumber[9];
    Require(patch.BoundCell(new(1.9,4.1,-.1,1.1),parent,out var certified)==AlgorithmStatus.Success,"whole parent proof including mathematical extension");
    BufferCount observations=0;
    foreach(var constantU in new[]{false,true})foreach(var station in new[]{2.0,3.0,4.0})
    {
        var at=constantU?station:.37;var lo=constantU?-.05:2.8;var hi=constantU?1.05:3.2;var box=new SurfaceParameterBox(lo,hi,0,0);var iso=new SurfaceIntersectionPatch(certified,constantU,at,lo,hi);
        SurfaceIntersectionPatch.UseCertifiedSectionBounds=false;Require(iso.Bound(box,a)==AlgorithmStatus.Success,"original bound");SurfaceIntersectionPatch.UseCertifiedSectionBounds=true;SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;
        Require(iso.Bound(box,b)==AlgorithmStatus.Success&&SurfaceIntersectionPatch.TestCertifiedSectionBounds==1,"admitted native iso");
        for(var d=3;d<6;d++){Bits(a[d].Lower,b[d].Lower);Bits(a[d].Upper,b[d].Upper);}for(var d=6;d<9;d++)Require(b[d].Lower==0&&b[d].Upper==0,"one-dimensional inactive derivative");
        for(var i=0;i<=16;i++)
        {
            var t=lo+(hi-lo)*i/16;var u=constantU?at:t;var v=constantU?t:at;var phase=angle*(reverse?v:1-v);var sign=reverse?1:-1;
            var p=Add(origin,Map(new(){X=-radius*Math.Cos(phase),Y=-radius*Math.Sin(phase),Z=u},transformed));
            var derivative=constantU?Map(new(){X=radius*angle*sign*Math.Sin(phase),Y=-radius*angle*sign*Math.Cos(phase)},transformed):z;
            Contains(b,p,0);Contains(b,derivative,3);observations++;
        }
        // JIT warmup precedes allocation measurement.
        var before=GC.GetAllocatedBytesForCurrentThread();Require(iso.Bound(box,b)==AlgorithmStatus.Success,"repeat bound");Require(GC.GetAllocatedBytesForCurrentThread()==before,"hot path zero allocation");
    }
    if(angle==Math.PI/2&&!reverse&&!transformed)
    {
        Gate(new SurfaceIntersectionPatch(patch,true,3,0,1),new(0,1,0,0),"no proof");
        Gate(new SurfaceIntersectionPatch(certified,true,4.2,0,1),new(0,1,0,0),"fixed coordinate outside proof");
        Gate(new SurfaceIntersectionPatch(certified,false,.5,1.8,4.2),new(1.8,4.2,0,0),"free coordinate outside proof");
        Gate(new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(certified,.01),true,3,0,1),new(0,1,0,0),"offset");
        Gate(new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(certified,(BufferOffset)0,.1),true,3,0,1),new(0,1,0,0),"sweep");
        Gate(new SurfaceIntersectionPatch(certified,2.5,.2,.1,.3,0,1),new(0,1,0,0),"parameter line");
        Gate(new SurfaceIntersectionPatch(new SurfaceIntersectionPatch(certified,true,3,0,1),true,.5,0,1),new(0,1,0,0),"repeated iso");
        var saved=spines[0].Curve;spines[0].Curve=default;Require(!JointNestedBlendEvaluation.CanBoundExplicitSection(program,4),"implicit rejected");spines[0].Curve=saved;
        surfaces[2].IsBlend=true;Require(!JointNestedBlendEvaluation.CanBoundExplicitSection(program,4),"nested support rejected");surfaces[2].IsBlend=false;surfaces[2].IsBSpline=true;Require(!JointNestedBlendEvaluation.CanBoundExplicitSection(program,4),"spline support rejected");surfaces[2].IsBSpline=false;
        var plane=new AnalyticSurface(SurfaceClass.Plane,new(){X=-.1},new(){X=1},new(){Z=1});var graph=new JsonArray();
        foreach(var useNew in new[]{false,true})foreach(var memo in new[]{false,true})
        {
            SurfaceIntersectionPatch.UseCertifiedSectionBounds=useNew;SurfacePlaneIntersection.ReuseSectionRoots=memo;SurfacePlaneIntersection.TestSectionReuses=0;SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;
            var nodes=new SurfaceIntersectionNode[512];var edges=new SurfaceIntersectionSegment[512];BufferCount nn=0,ne=0,remaining=32768;var status=SurfacePlaneIntersection.Find(patch,plane,nodes,ref nn,edges,ref ne,ref remaining,strictSeparation:true);
            Require(status==AlgorithmStatus.Success&&nn>1&&ne>0,"finite complete graph");for(var i=0;i<nn;i++){Require(Math.Abs(nodes[i].Point.X+.1)<1e-9&&Math.Abs(nodes[i].V-1.0/3)<1e-8,"native finite graph root");Require(nodes[i].U>=2&&nodes[i].U<=4,"finite U");}
            graph.Add((JsonNode)new JsonObject{["newBounds"]=useNew,["memo"]=memo,["nodes"]=nn,["edges"]=ne,["remaining"]=remaining,["memoHits"]=SurfacePlaneIntersection.TestSectionReuses,["sectionBounds"]=SurfaceIntersectionPatch.TestCertifiedSectionBounds});
        }
        Require(graph.Select(row=>row!["nodes"]!.ToJsonString()+"/"+row["edges"]!.ToJsonString()+"/"+row["remaining"]!.ToJsonString()).Distinct().Count()==1,"graph completeness/budget invariant across both mechanisms");report["graph"]=graph;
        SurfaceIntersectionPatch.UseCertifiedSectionBounds=true;SurfacePlaneIntersection.ReuseSectionRoots=true;
        unsafe
        {
            var options=new ProjectGmKernel.Native.Generated.PK_SESSION_start_o_s{o_t_version=1};Require(KernelRuntime.SessionStart(&options)==0,"managed session");
            try
            {
                ModelingPerformance.Enabled=true;ModelingPerformance.Context context;using var scope=new ModelingPerformance.Scope(&context,true);var tagged=new SurfaceIntersectionPatch(patch,123,7,1);Require(tagged.BoundCell(new(2,4,0,1),parent,out var proof)==AlgorithmStatus.Success,"tagged cell proof");
                var iso=new SurfaceIntersectionPatch(proof,true,3,0,1);Require(iso.Bound(new(0,1,0,0),a)==AlgorithmStatus.Success,"store certified iso");var hits=context.Profile.BoundCacheHits;Require(iso.Bound(new(0,1,0,0),b)==AlgorithmStatus.Success&&context.Profile.BoundCacheHits==hits+1,"certified iso cache hit");
                Require(!ModelingPerformance.TryBound(123,7,1,4,false,new(3,3,0,1),b,out _,double.NaN),"2D cannot consume 1D cache");
                Require(!ModelingPerformance.TryBound(123,8,1,4,false,new(3,3,0,1),b,out _,double.NaN,3),"generation isolation");Require(!ModelingPerformance.TryBound(123,7,-1,4,false,new(3,3,0,1),b,out _,double.NaN,3),"sense isolation");Require(!ModelingPerformance.TryBound(124,7,1,4,false,new(3,3,0,1),b,out _,double.NaN,3),"tag isolation");
                report["cache"]=new JsonObject{["oneDimensionalKind"]=5,["cacheHits"]=context.Profile.BoundCacheHits,["twoDimensionalSeparated"]=true,["generationSenseTagSeparated"]=true};
            }
            finally{ModelingPerformance.Enabled=false;KernelRuntime.SessionStop();}
        }
    }
    controls.Add((JsonNode)new JsonObject{["angle"]=angle,["reverse"]=reverse,["transformed"]=transformed,["analyticObservations"]=observations});
}
report["manufactured"]=controls;
// A curved explicit carrier exercises nonconstant analytic support normals.
{
    BlendSurfaceInstruction[] surfaces=[new(){Analytic=new(SurfaceClass.Sphere,default,new(){Z=1},new(){X=1},1),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},new(){Analytic=new(SurfaceClass.Sphere,default,new(){Z=1},new(){X=1},.8),NormalSense=1},new(){Analytic=new(SurfaceClass.Plane,new(){Z=-.2},new(){Z=1},new(){X=1}),NormalSense=1},new(){IsBlend=true,First=2,Second=3,Spine=0,Range0=.2,Range1=.2,NormalSense=1}];
    BlendSpineInstruction[] spines=[new(){Curve=new(){Kind=CurveClass.Circle,First=new(){X=1},Second=new(){Y=1}},First=0,Second=1,ChartCount=3}];
    BlendChartInstruction[] charts=[new(){Parameter=6.1},new(){Parameter=6.3},new(){Parameter=6.5}];var program=new NestedBlendProgram(surfaces,spines,charts);var patch=new SurfaceIntersectionPatch(program,4,new(6.1,6.5,0,1));Span<IntervalNumber> bounds=stackalloc IntervalNumber[9],old=stackalloc IntervalNumber[9];
    Require(patch.BoundCell(new(6.2,6.4,.2,.8),bounds,out var proof)==AlgorithmStatus.Success,"curved parent proof");
    foreach(var constantU in new[]{false,true})
    {
        var at=constantU?6.3:.4;var lo=constantU?.25:6.25;var hi=constantU?.75:6.35;var iso=new SurfaceIntersectionPatch(proof,constantU,at,lo,hi);SurfaceIntersectionPatch.UseCertifiedSectionBounds=false;Require(iso.Bound(new(lo,hi,0,0),old)==AlgorithmStatus.Success,"curved original");SurfaceIntersectionPatch.UseCertifiedSectionBounds=true;SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;Require(iso.Bound(new(lo,hi,0,0),bounds)==AlgorithmStatus.Success&&SurfaceIntersectionPatch.TestCertifiedSectionBounds==1,"curved admission");for(var d=3;d<6;d++){Bits(old[d].Lower,bounds[d].Lower);Bits(old[d].Upper,bounds[d].Upper);}
        for(var i=0;i<=16;i++){var t=lo+(hi-lo)*i/16;var u=constantU?at:t;var v=constantU?t:at;var angle=v*Math.PI/2;var rho=1-.2*Math.Sin(angle);Contains(bounds,new(){X=rho*Math.Cos(u),Y=rho*Math.Sin(u),Z=-.2*Math.Cos(angle)},0);Contains(bounds,constantU?new(){X=-.1*Math.PI*Math.Cos(angle)*Math.Cos(u),Y=-.1*Math.PI*Math.Cos(angle)*Math.Sin(u),Z=.1*Math.PI*Math.Sin(angle)}:new(){X=-rho*Math.Sin(u),Y=rho*Math.Cos(u)},3);}
    }
    report["curvedCarrier"]=new JsonObject{["type"]="native periodic circle with sphere/plane supports",["bothFreeDerivativesBitwise"]=true,["analyticObservations"]=34};
}
unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");report["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";var pairs=new JsonArray();var isoRows=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
    foreach(var item in new[]{("quality/00-curved-candidate",0),("quality/01-solidblend-candidate",0),("active-shortedge-lifecycle-capture",4)})
    {
        var folder="temp_docs/independent-isoparam-e7f0a48/quality/original/temp_docs/isoparametric-bound/"+item.Item1;var gm=Product(folder,$"gm-{item.Item2}-after-0.json");var pk=Product(folder,$"pk-{item.Item2}-after-0.json");var actual=Receive(gm);var reference=Receive(pk);
        try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,item.Item1);pairs[^1]!["gmXtSha256"]=Hash(gm);pairs[^1]!["pkXtSha256"]=Hash(pk);pairs[^1]!["originalStep"]=item.Item2;if(item.Item2==4){Iso(actual,"GM sealed",isoRows);Iso(reference,"PK independent sealed",isoRows);}}
        finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
    }
    report["sealedProducts"]=pairs;report["realNativeIso"]=isoRows;
}
File.WriteAllText(output,report.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("PASS manufactured analytic/gate/graph/cache controls and three sealed real PK pairs/native iso semantics");

static KernelVector3 Map(KernelVector3 p,bool transform)=>transform?new(){X=p.Z,Y=-p.X,Z=p.Y}:p;
static KernelVector3 Add(KernelVector3 a,KernelVector3 b)=>new(){X=a.X+b.X,Y=a.Y+b.Y,Z=a.Z+b.Z};
static KernelVector3 Scale(KernelVector3 p,double s)=>new(){X=p.X*s,Y=p.Y*s,Z=p.Z*s};
static void Contains(ReadOnlySpan<IntervalNumber> bounds,KernelVector3 p,BufferOffset at){Require(p.X>=bounds[at].Lower-2e-15&&p.X<=bounds[at].Upper+2e-15&&p.Y>=bounds[at+1].Lower-2e-15&&p.Y<=bounds[at+1].Upper+2e-15&&p.Z>=bounds[at+2].Lower-2e-15&&p.Z<=bounds[at+2].Upper+2e-15,"independent analytic enclosure");}
static void Gate(SurfaceIntersectionPatch patch,SurfaceParameterBox box,string label){Span<IntervalNumber> output=stackalloc IntervalNumber[9];SurfaceIntersectionPatch.TestCertifiedSectionBounds=0;patch.Bound(box,output);Require(SurfaceIntersectionPatch.TestCertifiedSectionBounds==0,"gate "+label);}
static void Bits(double a,double b)=>Require(BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b),"same free derivative path");
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Check(ApiErrorCode error,string label)=>Require(error==0,label+" error="+error);
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Product(string folder,string name){var source=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;Require(Hash(path)==(string?)source["xtSha256"],"sealed product hash binding");return path;}
static unsafe GeomTag Receive(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};BufferCount count;PK_PART_t* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};BufferCount count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}
static unsafe void Iso(GeomTag body,string label,JsonArray rows)
{
    BufferCount nf;PK_FACE_t* faces;Check(PK_BODY_ask_faces(body,&nf,&faces),"faces");PK_VECTOR_t* a=stackalloc PK_VECTOR_t[2];PK_VECTOR_t* b=stackalloc PK_VECTOR_t[2];
    try{for(BufferOffset i=0;i<nf;i++){GeomTag surface;EntityClass kind;Check(PK_FACE_ask_surf(faces[i],&surface),"surf");Check(PK_ENTITY_ask_class(surface,&kind),"class");if(kind!=PK_CLASS_blendsf)continue;PK_UVBOX_t box;Check(PK_FACE_find_uvbox(faces[i],&box),"UV box");for(BufferOffset axis=0;axis<2;axis++){var at=(box.param[axis]+box.param[axis+2])*.5;var options=new PK_SURF_make_curve_isoparam_o_t(PK_isoparam_prefer_spcurve_c,PK_LOGICAL_true);GeomTag curve;PK_INTERVAL_t range;Check(PK_SURF_make_curve_isoparam(surface,at,axis==0?PK_PARAM_direction_u_c:PK_PARAM_direction_v_c,&options,&curve,&range),"finite SP iso");double position=0,derivative=0;try{for(var station=0;station<5;station++){var free=box.param[1-axis]+station*.25*(box.param[3-axis]-box.param[1-axis]);var uv=new PK_UV_t();uv.param[axis]=at;uv.param[1-axis]=free;Check(PK_SURF_eval(surface,uv,axis==1?1:0,axis==0?1:0,PK_LOGICAL_false,a),"native UV");Check(PK_CURVE_eval(curve,free,1,b),"same native free parameter");double p=0,d=0;for(var k=0;k<3;k++){p+=Math.Pow(a[0].coord[k]-b[0].coord[k],2);d+=Math.Pow(a[1].coord[k]-b[1].coord[k],2);}position=Math.Max(position,Math.Sqrt(p));derivative=Math.Max(derivative,Math.Sqrt(d));}Require(position<1e-10&&derivative<1e-9,"native SP position and derivative");rows.Add((JsonNode)new JsonObject{["source"]=label,["axisConstant"]=axis,["at"]=at,["returnedFirst"]=range.value[0],["returnedLast"]=range.value[1],["maxPositionResidual"]=position,["maxDerivativeResidual"]=derivative});}finally{Check(PK_ENTITY_delete(1,&curve),"delete iso");}}}}finally{Check(PK_MEMORY_free(faces),"free faces");}
}
