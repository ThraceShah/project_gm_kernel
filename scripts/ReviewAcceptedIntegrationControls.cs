#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property KernelTestHooks=true
// Run with -p:KernelTestHooks=true so the referenced Native project also exposes its existing controls.
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using BlendSurfaceIndex=System.Int32;
global using BlendSpineIndex=System.Int32;
global using BufferOffset=System.Int32;
global using DerivativeOrder=System.Int32;
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
const string root="temp_docs/independent-accepted-integration-3aff4e2b";
if(args.SequenceEqual(new[]{"--subdivision"}))
{
    Require(!File.Exists(root+"/subdivision.json"),"preserve subdivision control");var results=new JsonArray();
    try
    {
        foreach(var blossom in new[]{true,false})foreach(var seeds in new[]{true,false})
        {
            BezierSurfaceBounds.UseBlossomRestrictions=blossom;BSplineProjection.AllSubdivisionSeeds=seeds;
            double[] poles=new double[27],uk=[2,2,2,5,5,5],vk=[-3,-3,-3,-1,-1,-1],z=[.25,-.25,.25];
            for(var i=0;i<3;i++)for(var j=0;j<3;j++){var at=3*(3*i+j);poles[at]=i*.5;poles[at+1]=j*.5;poles[at+2]=z[i]+z[j]-.0625;}
            var surface=new BSurfaceView(2,2,3,3,3,false,false,false,poles,uk,vk);var target=new KernelVector3{X=.5,Y=.5,Z=-1};var point=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
            BSplineProjection.TestSeedSearches=0;var status=KernelRuntime.ClosestBSurface(surface,target,1e-9,ref point);var searches=BSplineProjection.TestSeedSearches;
            Require(status==AlgorithmStatus.Success&&searches>5,$"real descendant proposals executed {status} searches={searches}");Certificate(point.DistanceUpper,.87890625,1e-9);
            results.Add((JsonNode)new JsonObject{["blossom"]=blossom,["allSubdivisionSeeds"]=seeds,["searches"]=searches,["upper"]=point.DistanceUpper,["minimumSquared"]=.87890625,["status"]=status.ToString()});
        }
        foreach(var blossom in new[]{true,false}){var all=(long)results.Single(x=>(bool)x!["blossom"]! ==blossom&&(bool)x["allSubdivisionSeeds"]!)!["searches"]!;var few=(long)results.Single(x=>(bool)x!["blossom"]! ==blossom&&!(bool)x["allSubdivisionSeeds"]!)!["searches"]!;Require(all>few,"descendant seed reduction observed with both expressions");}
    }
    finally{BezierSurfaceBounds.UseBlossomRestrictions=false;BSplineProjection.AllSubdivisionSeeds=false;}
    File.WriteAllText(root+"/subdivision.json",results.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("PASS actual subdivision crossing in all four combinations");return;
}
Require(!File.Exists(root+"/controls.json"),"preserve execution");
if(!ParasolidScriptHost.TryStartSession("Independent cumulative integration review",out var host,out var message))throw new Exception(message);
using var session=host;
var rows=new JsonArray();var failures=new JsonArray();var pkRows=new JsonArray();var products=new JsonArray();var actualControls=new JsonArray();
var report=new JsonObject{["scope"]="Fixed combination crossing controls; finite controls do not replace accepted continuous contracts",["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeSha256"]=Hash(typeof(KernelRuntime).Assembly.Location)};
unsafe{var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM start");}
try
{
    foreach(var blossom in new[]{true,false})foreach(var seeds in new[]{true,false})
    {
        BezierSurfaceBounds.UseBlossomRestrictions=blossom;BSplineProjection.AllSubdivisionSeeds=seeds;
        foreach(var rational in new[]{false,true})
        {
            // Folded polynomial has two equal interior minimizers; positive
            // rational plane covers the native rectangular boundary.
            var poles=new double[3*2*(rational?4:3)];var dim=rational?4:3;
            double[] uk=[2,2,2,5,5,5],vk=[-3,-3,-1,-1];
            for(var i=0;i<3;i++)for(var j=0;j<2;j++)
            {
                var w=rational?(1+.5*i)*(1+.25*j):1;var at=dim*(2*i+j);
                poles[at]=(rational?i*.5:i==1?-.25:.25)*w;poles[at+1]=j*w;if(rational)poles[at+3]=w;
            }
            var surface=new BSurfaceView(2,1,3,2,dim,rational,false,false,poles,uk,vk);
            var target=rational?new KernelVector3{X=-.25,Y=.625,Z=.25}:new KernelVector3{X=.0625,Y=.375,Z=.25};
            var d2=rational?.125:.0625;var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
            BSplineProjection.TestSeedSearches=0;
            var status=KernelRuntime.ClosestBSurface(surface,target,1e-7,ref best);var searches=BSplineProjection.TestSeedSearches;
            Require(status==AlgorithmStatus.Success,"representative global completion "+status);Certificate(best.DistanceUpper,d2,1e-7);
            Require(best.U>=2&&best.U<=5&&best.V>=-3&&best.V<=-1,"finite original native UV");
            var warm=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};KernelRuntime.ClosestBSurface(surface,target,1e-7,ref warm);
            var hot=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};var start=GC.GetAllocatedBytesForCurrentThread();
            status=KernelRuntime.ClosestBSurface(surface,target,1e-7,ref hot);var alloc=GC.GetAllocatedBytesForCurrentThread()-start;
            Require(status==AlgorithmStatus.Success&&alloc==0,"hot projection zero managed allocation");
            rows.Add((JsonNode)new JsonObject{["blossom"]=blossom,["allSubdivisionSeeds"]=seeds,["rational"]=rational,["status"]=status.ToString(),["u"]=best.U,["v"]=best.V,["upper"]=best.DistanceUpper,["minimumSquared"]=d2,["searches"]=searches,["managedBytes"]=alloc});
            unsafe{PkControl(surface,poles,2,3,dim,rational,uk,vk,target,d2,pkRows);}
            Span<IntervalNumber> net=stackalloc IntervalNumber[BezierSurfaceBounds.MaxScalars];
            Require(BezierSurfaceBounds.Extract(surface,2,1,net)==AlgorithmStatus.Success,"extract native");
            var patch=new BezierSurfacePatch(net[..24],2,1,new(2,5,-3,-1));
            Span<IntervalNumber> j9=stackalloc IntervalNumber[9],j18=stackalloc IntervalNumber[18],j27=stackalloc IntervalNumber[27];
            var box=new SurfaceParameterBox(2.4,2.6,-2.5,-2.3);
            Require(BezierSurfaceBounds.Bound(patch,box,j9)==AlgorithmStatus.Success&&BezierSurfaceBounds.BoundSecond(patch,box,j18)==AlgorithmStatus.Success&&BezierSurfaceBounds.BoundRectangularSecond(patch,box,j27)==AlgorithmStatus.Success,"complete native jets");
            SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<KernelVector3> jets=stackalloc KernelVector3[9];Span<double> workspace=stackalloc double[4096];
            Require(BSurfaceEvaluation.Evaluate(surface,2.5,-2.4,layout,workspace,jets)==AlgorithmStatus.Success,"native point jets");
            for(var k=0;k<9;k++)for(var d=0;d<3;d++)
            {
                var value=d==0?jets[k].X:d==1?jets[k].Y:jets[k].Z;
                // PK-compatible numeric evaluation zero-fills above spline degree.
                // The continuous interval jet bounds analytic derivatives instead;
                // for this separable rational plane use its closed form in native UV.
                if(rational)
                {
                    var t=(2.5-2)/3;var s=(-2.4+3)/2;var i=k/3;var j=k%3;
                    value=d==0&&j==0?(i==0?.5*t+1-1/(1+t):i==1?(.5+1/Math.Pow(1+t,2))/3:-2/(9*Math.Pow(1+t,3)))
                        :d==1&&i==0?(j==0?1.25*s/(1+.25*s):j==1?1.25/(2*Math.Pow(1+.25*s,2)):-.15625/Math.Pow(1+.25*s,3)):0;
                }
                Require(j27[3*k+d].Lower<=value+1e-12&&j27[3*k+d].Upper>=value-1e-12,$"finite jet point enclosure blossom={blossom} rational={rational} k={k} d={d} value={value:R} interval={j27[3*k+d].Lower:R}..{j27[3*k+d].Upper:R}");
            }
        }
        Span<IntervalNumber> constant=stackalloc IntervalNumber[4];constant[3]=1;
        var pointPatch=new BezierSurfacePatch(constant,0,0,new(2,5,-3,-1));var partial=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};
        var failed=BSplineProjection.Closest(pointPatch,new(){Z=1},1e-30,ref partial);
        Require(failed==AlgorithmStatus.NumericalFailure,"original minimum-width failure retained");
        constant[3]=0;partial=new(){DistanceUpper=double.PositiveInfinity};Require(BSplineProjection.Closest(pointPatch,new(){Z=1},1e-7,ref partial)==AlgorithmStatus.NumericalFailure,"nonpositive weight rejects");constant[3]=1;
        // Exact focal point: stationarity alone passes refine, Hessian must reject.
        double[] data=[-.5,0,.25,-.5,1,.25,0,0,-.25,0,1,-.25,.5,0,.25,.5,1,.25,2,2,2,5,5,5,-3,-3,-1,-1];
        var node=new BlendSurfaceInstruction{IsBSpline=true,NormalSense=1,UDegree=2,VDegree=1,UCount=3,VCount=2,Dimension=3};
        var program=new NestedBlendProgram([node],[],[],data,new double[4096]);double u=3.5,v=-2.25;
        var focal=new TaylorVector(new(0,1),new(.375,1),new(.5,1));
        var nearest=NestedSplineEvaluation.DistanceFromSeed(program,node,focal,ref u,ref v,out _,out _);Require(nearest==AlgorithmStatus.Singular,"nearest Hessian retained");
        failures.Add((JsonNode)new JsonObject{["blossom"]=blossom,["allSubdivisionSeeds"]=seeds,["tooTight"]=failed.ToString(),["hessian"]=nearest.ToString()});
    }
    unsafe
    {
        PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"PK version");report["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
        ParasolidBodyCompare.Observer=row=>products.Add((JsonNode)row);
        foreach(var item in new[]{("quality/00-curved-candidate",0),("dev-candidate/00-solidblend-candidate",5),("active-shortedge-lifecycle-capture",4)})
        {
            var folder=root+"/quality/original/temp_docs/accepted-integration/"+item.Item1;var gm=Product(folder,$"gm-{item.Item2}-after-0.json");var pk=Product(folder,$"pk-{item.Item2}-after-0.json");
            var actual=Receive(gm);var reference=Receive(pk);
            try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,item.Item1);products[^1]!["gmXtSha256"]=Hash(gm);products[^1]!["pkXtSha256"]=Hash(pk);products[^1]!["step"]=item.Item2;}
            finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
            if(!item.Item1.StartsWith("dev-",StringComparison.Ordinal))continue;
            var own=ReceiveGm(gm);
            try
            {
                Require(KernelRuntime.TryResolveBodySlot(own,out var slot),"actual GM product");var face=KernelRuntime.Bodies[slot].FirstFaceBody;var count=0;
                CommandScratch.Bind(KernelRuntime.State.Session,0);
                try
                {
                    for(var i=0;i<KernelRuntime.Bodies[slot].FaceCountBody;i++,face=KernelRuntime.Faces[face].NextInBody)
                    {
                        var sf=KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag);var record=KernelRuntime.Surfaces[sf];if(record.Class!=SurfaceClass.BlendSurface)continue;
                        var data=KernelRuntime.BlendedEdgeDataPool[record.DataIndex];var p=KernelRuntime.GetNestedBlendProgram(data);var type=typeof(JointNestedBlendEvaluation);
                        var control=typeof(ActualChecks).GetMethod(nameof(ActualChecks.Run))!.MakeGenericMethod(type.GetNestedType("Occurrence",BindingFlags.NonPublic)!,type.GetNestedType("IntervalSplineContact",BindingFlags.NonPublic)!).CreateDelegate<ActualControl>();
                        actualControls.Add((JsonNode)control(p,data.ProgramRoot,record.UMin,record.UMax));count++;
                    }
                }
                finally{CommandScratch.Unbind(KernelRuntime.State.Session,0);}
                Require(count>0,"real implicit program consumed");
            }finally{Check(KernelRuntime.EntityDelete(1,&own),"delete GM product");}
        }
    }
}
finally{BezierSurfaceBounds.UseBlossomRestrictions=false;BSplineProjection.AllSubdivisionSeeds=false;JointNestedBlendEvaluation.ReuseSplineContactBounds=true;KernelRuntime.SessionStop();}
report["globalControls"]=rows;report["failures"]=failures;report["realPkProjectionControls"]=pkRows;report["sealedProducts"]=products;report["actualProgramControls"]=actualControls;
File.WriteAllText(root+"/controls.json",report.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("PASS fixed combination global certificates, native jets, original failure/Hessian, real implicit matrices/bounds/nearest and sealed products");

static unsafe GeomTag ReceiveGm(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};BufferCount count;GeomTag* parts;Check(KernelRuntime.PartReceiveB(*(M.PK_MEMORY_block_s*)&block,(M.PK_PART_receive_o_s*)&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(KernelRuntime.MemoryFree(parts),"free parts");}}}
static void Certificate(double upper,double minimumSquared,double tolerance)
{var a=Dyadic.Of(upper);Require(a*a>=Dyadic.Of(minimumSquared),"valid upper certificate");var gap=a-Dyadic.Of(tolerance);Require(gap*gap<=Dyadic.Of(minimumSquared),"closed global gap");}
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
            foreach(var old in new[]{BSplineProjection.AllSubdivisionSeeds})
            {
                BSplineProjection.AllSubdivisionSeeds=old;var ownRange=new M.PK_range_1_r_s();PK_range_result_t ownFound;var error=KernelRuntime.GeomRangeVector(actual,*(M.PK_VECTOR_s*)&vector,(M.PK_GEOM_range_vector_o_s*)&options,&ownFound,&ownRange);
                Require(error==0?ownFound==PK_range_result_found_c&&Math.Abs(ownRange.distance*ownRange.distance-d2)<2e-7:degree==2&&error==M.ParasolidConstants.PK_ERROR_cant_do_intersect,"GM public finite range/failure gate");
                rows.Add((JsonNode)new JsonObject{["rational"]=rational,["degree"]=degree,["allSubdivisionSeeds"]=old,["pkDistance"]=range.distance,["gmError"]=error,["gmDistance"]=error==0?JsonValue.Create(ownRange.distance):null,["pkU"]=range.end.parameters[0],["pkV"]=range.end.parameters[1],["gmU"]=error==0?JsonValue.Create(ownRange.end.parameters[0]):null,["gmV"]=error==0?JsonValue.Create(ownRange.end.parameters[1]):null,["knownMinimumSquared"]=d2});
            }
        }
        finally{Check(PK_ENTITY_delete(1,&reference),"delete PK surface");Check(KernelRuntime.EntityDelete(1,&actual),"delete GM surface");}
    }
}
readonly record struct Dyadic(BigInteger Numerator,IntegerExponent Exponent)
{
    public static Dyadic Of(double value){var bits=BitConverter.DoubleToInt64Bits(value);var e=(IntegerExponent)((bits>>52)&2047);var n=new BigInteger(bits&0xfffffffffffffL);if(e!=0)n+=BigInteger.One<<52;return new(bits<0?-n:n,e==0?-1074:e-1075);}
    public static Dyadic operator *(Dyadic a,Dyadic b)=>new(a.Numerator*b.Numerator,a.Exponent+b.Exponent);
    public static Dyadic operator -(Dyadic a,Dyadic b){var e=Math.Min(a.Exponent,b.Exponent);return new((a.Numerator<<(a.Exponent-e))-(b.Numerator<<(b.Exponent-e)),e);}
    public static bool operator <=(Dyadic a,Dyadic b)=>(a-b).Numerator.Sign<=0;
    public static bool operator >=(Dyadic a,Dyadic b)=>(a-b).Numerator.Sign>=0;
}

delegate JsonObject ActualControl(in NestedBlendProgram program,BlendSurfaceIndex root,double first,double last);
static class ActualChecks
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static;
    delegate AlgorithmStatus Core<TN>(in NestedBlendProgram program,BlendSpineIndex spine,BlendSurfaceIndex surface,double u,double v,in SurfaceDerivativeLayout layout,Span<KernelVector3> output,bool nearest,in TaylorVector path,out TaylorJet distance,out TaylorVector normal,Span<double> contacts,BufferOffset boundary,double boundaryValue,Span<TaylorJet> snapshot,Span<TN> nodes,Span<BufferCount> counts);
    delegate AlgorithmStatus Jac<TN,TC>(in NestedBlendProgram program,ReadOnlySpan<TN> nodes,ReadOnlySpan<IntervalNumber> boxes,IntervalNumber parameter,Span<IntervalNumber> matrix,Span<IntervalDual> variables,Span<IntervalDual> equations,ReadOnlySpan<BufferOffset> charts,Span<TC> cache,bool cartesianSpline=false,bool localSplineScope=false);
    public static JsonObject Run<TN,TC>(in NestedBlendProgram program,BlendSurfaceIndex root,double first,double last)where TN:unmanaged where TC:unmanaged
    {
        var owner=typeof(JointNestedBlendEvaluation);var core=owner.GetMethod("EvaluateCore",Private)!.CreateDelegate<Core<TN>>();var jac=owner.GetMethod("IntervalJacobian",Private)!.CreateDelegate<Jac<TN,TC>>();
        var rows=new JsonArray();var spine=program.Surfaces[root].Spine;var changedSnapshots=new List<double[]>();
        foreach(var blossom in new[]{true,false})foreach(var seeds in new[]{true,false})foreach(var fraction in new[]{.47,.59})
        {
            BezierSurfaceBounds.UseBlossomRestrictions=blossom;BSplineProjection.AllSubdivisionSeeds=seeds;
            var t=first+fraction*(last-first);var box=new SurfaceParameterBox(t,t+.002*(last-first),.2,.8);
            Span<IntervalNumber> off=stackalloc IntervalNumber[9],on=stackalloc IntervalNumber[9];
            JointNestedBlendEvaluation.ReuseSplineContactBounds=false;var a=JointNestedBlendEvaluation.Bound(program,root,box,off);
            JointNestedBlendEvaluation.ReuseSplineContactBounds=true;var b=JointNestedBlendEvaluation.Bound(program,root,box,on);
            Need(a==AlgorithmStatus.Success&&b==a,"real full nine");Bits(off,on);
            JointNestedBlendEvaluation.Bound(program,root,box,on);var before=GC.GetAllocatedBytesForCurrentThread();var hot=JointNestedBlendEvaluation.Bound(program,root,box,on);var bytes=GC.GetAllocatedBytesForCurrentThread()-before;Need(hot==a&&bytes==0,"real bound hot zero managed allocation");
            Span<TaylorJet> snapshot=stackalloc TaylorJet[63];Span<TN> nodes=stackalloc TN[16];Span<BufferCount> counts=stackalloc BufferCount[2];Span<KernelVector3> points=stackalloc KernelVector3[2];SurfaceDerivativeLayout.TryCreate(1,0,out var layout);
            Need(core(program,spine,-1,t,0,layout,points,false,default,out _,out _,default,-1,0,snapshot,nodes,counts)==AlgorithmStatus.Success,"real snapshot");
            var n=counts[0];var occurrences=nodes[..counts[1]];Span<IntervalNumber> boxes=stackalloc IntervalNumber[n],m1=stackalloc IntervalNumber[n*n],m2=stackalloc IntervalNumber[n*n];Span<IntervalDual> variables=stackalloc IntervalDual[n],equations=stackalloc IntervalDual[n];Span<BufferOffset> charts=stackalloc BufferOffset[occurrences.Length];Span<TC> cache=stackalloc TC[n],fresh=stackalloc TC[n];cache.Clear();fresh.Clear();
            for(var k=0;k<n;k++)boxes[k]=new(snapshot[k][0]-1e-8,snapshot[k][0]+1e-8);
            for(var k=0;k<occurrences.Length;k++){var s=Field(occurrences[k],"Spine");var offset=Field(occurrences[k],"Offset");var at=0;var sn=program.Spines[s];var c=program.Charts.Slice(sn.ChartOffset,sn.ChartCount);var parameter=k==0?t:snapshot[offset+3][0];while(at<c.Length-2&&c[at+1].Parameter<=parameter)at++;charts[k]=at;}
            Need(jac(program,occurrences,boxes,new(t-1e-8,t+1e-8),m1,variables,equations,charts,cache,false,true)==AlgorithmStatus.Success,"real local matrix");Need(jac(program,occurrences,boxes,new(t-1e-8,t+1e-8),m2,variables,equations,charts,fresh)==AlgorithmStatus.Success,"real standalone matrix");Bits(m1,m2);
            // Same scope permits changed UV boxes, never reusing the old key.
            var uv=Field(occurrences[0],"FirstUv");if(uv<0)uv=Field(occurrences[0],"SecondUv");Need(uv>=3,"actual spline readset");
            boxes[uv]=new(boxes[uv].Lower+1e-9,boxes[uv].Upper+1e-9);
            Need(jac(program,occurrences,boxes,new(t-1e-8,t+1e-8),m1,variables,equations,charts,cache,false,true)==AlgorithmStatus.Success,"real changed UV local");Need(jac(program,occurrences,boxes,new(t-1e-8,t+1e-8),m2,variables,equations,charts,fresh)==AlgorithmStatus.Success,"real changed UV standalone");Bits(m1,m2);
            SurfaceDerivativeLayout.TryCreate(0,0,out var pointLayout);Span<KernelVector3> p=stackalloc KernelVector3[1];Need(JointNestedBlendEvaluation.Evaluate(program,root,t,.5,pointLayout,p)==AlgorithmStatus.Success,"real point");
            var path=TaylorVector.Constant(p[0],1);var nearest=JointNestedBlendEvaluation.Distance(program,root,path,out var distance,out var normal);Need(nearest==AlgorithmStatus.Success&&distance.Finite&&normal.Finite&&Math.Abs(distance[0])<1e-7,"real nearest");
            rows.Add((JsonNode)new JsonObject{["blossom"]=blossom,["allSubdivisionSeeds"]=seeds,["fraction"]=fraction,["fullNineCacheBitwise"]=true,["matrixEntries"]=n*n,["changedUvStandaloneBitwise"]=true,["hotManagedBytes"]=bytes,["nearest"]=nearest.ToString(),["distance"]=distance[0],["firstSplineU"]=snapshot[uv][0]});
            if(blossom&&seeds)changedSnapshots.Add(snapshot[..n].ToArray().Select(x=>x[0]).ToArray());
        }
        Need(changedSnapshots.Count==2&&!changedSnapshots[0].SequenceEqual(changedSnapshots[1]),"distinct actual projection snapshots");
        return new JsonObject{["root"]=root,["differentStartSnapshots"]=true,["scope"]="Every snapshot owns a new local cache; no reuse permission across program or BoundSegment",["rows"]=rows};
    }
    static BufferOffset Field<T>(T node,string name)=> (BufferOffset)typeof(T).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(node)!;
    static void Need(bool ok,string name){if(!ok)throw new Exception(name);}
    static void Bits(ReadOnlySpan<IntervalNumber> a,ReadOnlySpan<IntervalNumber> b){for(var k=0;k<a.Length;k++)Need(BitConverter.DoubleToInt64Bits(a[k].Lower)==BitConverter.DoubleToInt64Bits(b[k].Lower)&&BitConverter.DoubleToInt64Bits(a[k].Upper)==BitConverter.DoubleToInt64Bits(b[k].Upper),"full interval matrix/bound bitwise");}
}
