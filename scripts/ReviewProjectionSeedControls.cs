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
global using IntegerExponent=System.Int32;
global using ApiErrorCode=System.Int32;
global using SplineDegree=System.Int32;
global using KnotMultiplicity=System.Int32;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;
Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string output="temp_docs/independent-projection-seed-a9085ed/controls.json";
if(args.SequenceEqual(new[]{"--bounds"})||args.SequenceEqual(new[]{"--bounds-check"}))
{
    var destination="temp_docs/independent-projection-seed-a9085ed/bounds-verified.json";var verified=CheckBounds();
    if(args[0]=="--bounds-check")Require(JsonNode.DeepEquals(verified,JsonNode.Parse(File.ReadAllText(destination))),"same corrected bounds");
    else{Require(!File.Exists(destination),"preserve bound verification");File.WriteAllText(destination,verified.ToJsonString());}
    Console.WriteLine("PASS corrected exact dyadic box minimum and maximum");return;
}
Require(!File.Exists(output),"preserve independent execution");
if(!ParasolidScriptHost.TryStartSession("Independent projection seed review",out var host,out var message))throw new Exception(message);
using var session=host;
var rows=new JsonArray();var failures=new JsonArray();var pkRows=new JsonArray();
var report=new JsonObject{["scope"]="Manufactured analytic minima and exact dyadic certificate controls, not sampled global proofs or timing qualification",["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeSha256"]=Hash(typeof(KernelRuntime).Assembly.Location)};
unsafe{var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");}
try
{
    foreach(var kind in new[]{"plane","rational","multipatch","folded","parabola"})foreach(var transform in new[]{false,true})
    {
        var curved=kind is "parabola" or "folded";var rational=kind=="rational";var split=kind=="multipatch";var degree=curved?2:1;var nu=curved||split?3:2;var dim=rational?4:3;
        var poles=new double[nu*2*dim];double[] uk=curved?[2,2,2,5,5,5]:split?[2,2,3.5,5,5]:[2,2,5,5],vk=[-3,-3,-1,-1];
        for(var i=0;i<nu;i++)for(var j=0;j<2;j++)
        {
            var x=(double)i/(nu-1);var p=new KernelVector3{X=kind=="folded"?(i==1?-.25:.25):curved?x-.5:x,Y=j,Z=kind=="parabola"?(i==1?-.25:.25):0};p=Map(p,transform);
            var w=rational?(1+.5*i)*(1+.25*j):1;var at=dim*(2*i+j);poles[at]=p.X*w;poles[at+1]=p.Y*w;poles[at+2]=p.Z*w;if(rational)poles[at+3]=w;
        }
        var surface=new BSurfaceView(degree,1,nu,2,dim,rational,false,false,poles,uk,vk);
        var queries=kind=="folded"?new[]{("two-equal-interior",new KernelVector3{X=.0625,Y=.375,Z=.25},.0625)}:curved?new[]{("two-equal-interior",new KernelVector3{Y=.375,Z=.5625},.3125),("flat-near-tangent",new KernelVector3{Y=.375,Z=.5},.25)}:new[]{("interior",new KernelVector3{X=.375,Y=.625,Z=.25},.0625),("edge",new KernelVector3{X=-.25,Y=.625,Z=.25},.125),("corner",new KernelVector3{X=-.25,Y=1.5,Z=.25},.375)};
        foreach(var (name,point,d2) in queries)
        {
            var target=Map(point,transform);var results=new JsonArray();
            foreach(var old in new[]{true,false})
            {
                BSplineProjection.AllSubdivisionSeeds=old;var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
                var status=KernelRuntime.ClosestBSurface(surface,target,1e-7,ref best);
                if(status!=AlgorithmStatus.Success)
                {
                    Require(kind=="parabola"&&(status==AlgorithmStatus.NotConverged||status==AlgorithmStatus.NumericalFailure),"unexpected global failure "+kind+" "+status);
                    results.Add((JsonNode)new JsonObject{["allSubdivisionSeeds"]=old,["status"]=status.ToString(),["partialUpperNotAResult"]=best.DistanceUpper});Console.WriteLine($"FAILURE retained {kind}/{name}/old={old}: {status}");continue;
                }
                Certificate(best.DistanceUpper,d2,1e-7);Require(best.U>=2&&best.U<=5&&best.V>=-3&&best.V<=-1,"native finite UV");
                var again=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};var start=GC.GetAllocatedBytesForCurrentThread();var repeated=KernelRuntime.ClosestBSurface(surface,target,1e-7,ref again);var allocation=GC.GetAllocatedBytesForCurrentThread()-start;Require(repeated==AlgorithmStatus.Success&&allocation==0,"hot managed allocation");
                results.Add((JsonNode)new JsonObject{["allSubdivisionSeeds"]=old,["status"]=status.ToString(),["u"]=best.U,["v"]=best.V,["upper"]=best.DistanceUpper,["knownMinimumSquared"]=d2,["allocationBytes"]=allocation});
            }
            rows.Add((JsonNode)new JsonObject{["geometry"]=kind,["case"]=name,["transformed"]=transform,["results"]=results});Console.WriteLine($"CONTROL {kind}/{name}/transform={transform}");
            if(!transform&&(name=="two-equal-interior"||name=="edge"&&rational))unsafe{PkControl(surface,poles,degree,nu,dim,rational,uk,vk,target,d2,pkRows);}
            if(name=="interior"||name=="two-equal-interior"||name=="flat-near-tangent")
            {
                var data=poles.Concat(uk).Concat(vk).ToArray();var node=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=1,UDegree=degree,VDegree=1,UCount=nu,VCount=2,Dimension=dim,Rational=rational};
                var program=new NestedBlendProgram(new[]{node},Array.Empty<BlendSpineInstruction>(),Array.Empty<BlendChartInstruction>(),data,new double[4096]);
                foreach(var old in new[]{true,false})
                {
                    BSplineProjection.AllSubdivisionSeeds=old;var result=NestedSplineEvaluation.Closest(program,node,target,out var u,out var v);
                    if(result!=AlgorithmStatus.Success){Require(kind=="parabola","unexpected nested failure");failures.Add((JsonNode)new JsonObject{["case"]=kind+"/"+name,["transformed"]=transform,["oldSeeds"]=old,["refine"]=result.ToString()});continue;}
                    SurfaceDerivativeLayout.TryCreate(0,0,out var layout);Span<KernelVector3> p=stackalloc KernelVector3[1];Span<double> scratch=stackalloc double[4096];Require(BSurfaceEvaluation.Evaluate(surface,u,v,layout,scratch,p)==AlgorithmStatus.Success,"native refined point");
                    Require(Math.Abs(Squared(p[0],target)-d2)<1e-12,"refined minimum");
                    var path=new TaylorVector(new(target.X,1),new(target.Y,1),new(target.Z,1));var distance=NestedSplineEvaluation.Distance(program,node,path,out _,out _);
                    Require(distance==(name=="flat-near-tangent"?AlgorithmStatus.Singular:AlgorithmStatus.Success),"original Hessian gate "+distance);
                    failures.Add((JsonNode)new JsonObject{["case"]=kind+"/"+name,["transformed"]=transform,["oldSeeds"]=old,["refine"]=result.ToString(),["distanceHessian"]=distance.ToString(),["u"]=u,["v"]=v});
                }
            }
        }
    }
    // Direct degree-zero directions are used by curve adapters; a collapsed
    // native domain is rejected rather than silently reparameterised.
    Span<IntervalNumber> constant=stackalloc IntervalNumber[4];constant[3]=1;
    foreach(var old in new[]{true,false})
    {
        BSplineProjection.AllSubdivisionSeeds=old;var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};var patch=new BezierSurfacePatch(constant,0,0,new(2,5,-3,-1));
        var status=BSplineProjection.Closest(patch,new(){Z=1},1e-30,ref best);Require(status==AlgorithmStatus.NumericalFailure,"minimum width is failure");
        var bad=new BezierSurfacePatch(constant,0,0,new(2,2,-3,-1));best=new(){DistanceUpper=double.PositiveInfinity};Require(BSplineProjection.Closest(bad,new(){Z=1},1e-7,ref best)==AlgorithmStatus.InvalidInput,"collapsed domain");
        constant[3]=0;best=new(){DistanceUpper=double.PositiveInfinity};Require(BSplineProjection.Closest(patch,new(){Z=1},1e-7,ref best)==AlgorithmStatus.NumericalFailure,"nonpositive weight");constant[3]=1;
        Require(BSplineProjection.Closest(patch,new(){Z=1},0,ref best)==AlgorithmStatus.InvalidInput,"invalid tolerance");
        failures.Add((JsonNode)new JsonObject{["case"]="degree-zero/resource/invalid",["oldSeeds"]=old,["tooTightStatus"]=status.ToString()});
    }
    report["privateOriginalBounds"]=CheckBounds();
}
finally{BSplineProjection.AllSubdivisionSeeds=false;KernelRuntime.SessionStop();}
unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");report["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
    var pairs=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
    foreach(var item in new[]{("quality/00-curved-candidate",0),("dev-candidate/00-solidblend-candidate",5),("active-shortedge-lifecycle-capture",4)})
    {
        var folder="temp_docs/independent-projection-seed-a9085ed/quality/original/temp_docs/projection-seed/"+item.Item1;var gm=Product(folder,$"gm-{item.Item2}-after-0.json");var pk=Product(folder,$"pk-{item.Item2}-after-0.json");var actual=Receive(gm);var reference=Receive(pk);
        try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,item.Item1);pairs[^1]!["gmXtSha256"]=Hash(gm);pairs[^1]!["pkXtSha256"]=Hash(pk);pairs[^1]!["originalStep"]=item.Item2;}
        finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
    }
    report["sealedProducts"]=pairs;
}
report["globalControls"]=rows;report["nestedAndFailureControls"]=failures;report["realPkProjectionControls"]=pkRows;
File.WriteAllText(output,report.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("PASS independent global/nested/failure/bounds/PK/sealed controls");

static KernelVector3 Map(KernelVector3 p,bool transform)=>transform?new(){X=p.Z+3,Y=-p.X-2,Z=p.Y+5}:p;
static double Squared(KernelVector3 p,KernelVector3 q)=>(p.X-q.X)*(p.X-q.X)+(p.Y-q.Y)*(p.Y-q.Y)+(p.Z-q.Z)*(p.Z-q.Z);
static void Certificate(double upper,double minimumSquared,double tolerance)
{var a=Dyadic.Of(upper);Require(a*a>=Dyadic.Of(minimumSquared),"valid upper certificate");var gap=a-Dyadic.Of(tolerance);Require(gap*gap<=Dyadic.Of(minimumSquared),"closed global gap");}
static JsonObject CheckBounds()
{
    var upper=typeof(BSplineProjection).GetMethod("UpperDistance",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<DistanceBound>();var lower=typeof(BSplineProjection).GetMethod("BoxDistance",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<DistanceBound>();
    Span<IntervalNumber> cube=stackalloc IntervalNumber[3];cube[0]=new(-.25,.5);cube[1]=new(-.5,.25);cube[2]=new(-.125,.125);var query=new KernelVector3{X=1,Y=-1,Z=.625};var lo=lower(cube,query);var hi=upper(cube,query);
    Require(Dyadic.Of(lo)*Dyadic.Of(lo)<=Dyadic.Of(.5*.5+.5*.5+.5*.5),"exact box lower");Require(Dyadic.Of(hi)*Dyadic.Of(hi)>=Dyadic.Of(1.25*1.25+1.25*1.25+.75*.75),"exact box upper");
    return new JsonObject{["lower"]=lo,["upper"]=hi,["exactMinimumSquared"]=.75,["exactMaximumSquared"]=3.6875,["exactDyadicChecks"]=true};
}
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Check(ApiErrorCode error,string label)=>Require(error==0,label+" error="+error);
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string Product(string folder,string name){var source=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;Require(Hash(path)==(string?)source["xtSha256"],"sealed product hash");return path;}
static unsafe GeomTag Receive(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};BufferCount count;PK_PART_t* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};BufferCount count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}
static unsafe void PkControl(in BSurfaceView surface,double[] poles,SplineDegree degree,BufferCount nu,BufferCount dimension,bool rational,double[] uk,double[] vk,KernelVector3 target,double d2,JsonArray rows)
{
    double[] uvalues=uk.Distinct().ToArray(),vvalues=vk.Distinct().ToArray();KnotMultiplicity[] um=uvalues.Select(x=>uk.Count(y=>x==y)).ToArray(),vm=vvalues.Select(x=>vk.Count(y=>x==y)).ToArray();
    fixed(double* p=poles)fixed(double* u=uvalues)fixed(double* v=vvalues)fixed(KnotMultiplicity* a=um)fixed(KnotMultiplicity* b=vm)
    {
        var sf=new PK_BSURF_sf_t{u_degree=degree,v_degree=1,n_u_vertices=nu,n_v_vertices=2,vertex_dim=dimension,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,vertex=p,form=PK_BSURF_form_unset_c,n_u_knots=uvalues.Length,n_v_knots=vvalues.Length,u_knot=u,v_knot=v,u_knot_mult=a,v_knot_mult=b,u_knot_type=PK_knot_unset_c,v_knot_type=PK_knot_unset_c,self_intersecting=PK_self_intersect_unset_c,convexity=PK_convexity_unset_c};
        GeomTag reference,actual;Check(PK_BSURF_create(&sf,&reference),"PK surface");Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&actual),"GM surface");
        try
        {
            var vector=new PK_VECTOR_t();vector.coord[0]=target.X;vector.coord[1]=target.Y;vector.coord[2]=target.Z;
            var options=new PK_GEOM_range_vector_o_t{opt_level=PK_range_opt_accuracy_c,have_tolerance=PK_LOGICAL_true,tolerance=1e-7};PK_range_result_t found;var range=new PK_range_1_r_t();Check(PK_GEOM_range_vector(reference,vector,&options,&found,&range),"PK global range");Require(found==PK_range_result_found_c&&Math.Abs(range.distance*range.distance-d2)<1e-10,"PK known finite minimum");
            foreach(var old in new[]{true,false})
            {
                BSplineProjection.AllSubdivisionSeeds=old;var ownRange=new M.PK_range_1_r_s();PK_range_result_t ownFound;var error=KernelRuntime.GeomRangeVector(actual,*(M.PK_VECTOR_s*)&vector,(M.PK_GEOM_range_vector_o_s*)&options,&ownFound,&ownRange);
                Require(error==0?ownFound==PK_range_result_found_c&&Math.Abs(ownRange.distance*ownRange.distance-d2)<2e-7:degree==2&&error==M.ParasolidConstants.PK_ERROR_cant_do_intersect,"GM public finite range/failure gate");
                rows.Add((JsonNode)new JsonObject{["rational"]=rational,["degree"]=degree,["allSubdivisionSeeds"]=old,["pkDistance"]=range.distance,["gmError"]=error,["gmDistance"]=error==0?JsonValue.Create(ownRange.distance):null,["pkU"]=range.end.parameters[0],["pkV"]=range.end.parameters[1],["gmU"]=error==0?JsonValue.Create(ownRange.end.parameters[0]):null,["gmV"]=error==0?JsonValue.Create(ownRange.end.parameters[1]):null,["knownMinimumSquared"]=d2});
            }
        }
        finally{Check(PK_ENTITY_delete(1,&reference),"delete PK surface");Check(KernelRuntime.EntityDelete(1,&actual),"delete GM surface");}
    }
}
delegate double DistanceBound(ReadOnlySpan<IntervalNumber> bounds,in KernelVector3 target);
readonly record struct Dyadic(BigInteger Numerator,IntegerExponent Exponent)
{
    public static Dyadic Of(double value){var bits=BitConverter.DoubleToInt64Bits(value);var e=(IntegerExponent)((bits>>52)&2047);var n=new BigInteger(bits&0xfffffffffffffL);if(e!=0)n+=BigInteger.One<<52;return new(bits<0?-n:n,e==0?-1074:e-1075);}
    public static Dyadic operator *(Dyadic a,Dyadic b)=>new(a.Numerator*b.Numerator,a.Exponent+b.Exponent);
    public static Dyadic operator -(Dyadic a,Dyadic b){var e=Math.Min(a.Exponent,b.Exponent);return new((a.Numerator<<(a.Exponent-e))-(b.Numerator<<(b.Exponent-e)),e);}
    public static bool operator <=(Dyadic a,Dyadic b)=>(a-b).Numerator.Sign<=0;
    public static bool operator >=(Dyadic a,Dyadic b)=>(a-b).Numerator.Sign>=0;
}
