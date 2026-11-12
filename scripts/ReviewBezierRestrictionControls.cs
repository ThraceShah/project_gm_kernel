#!/usr/bin/env dotnet run
#:property TargetFramework=net10.0
#:property AllowUnsafeBlocks=true
#:property AssemblyName=BooleanOperationOracle
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
global using SplineDegree=System.Int32;
global using BufferOffset=System.Int32;
global using BufferCount=System.Int32;
global using GeomTag=System.Int32;
global using ApiErrorCode=System.Int32;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Bounds;
using ProjectGmKernel.Native.Geometry.Caching;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Geometry.Intersection;
using ProjectGmKernel.Native.Geometry.Projection;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;
using static parasolid;

Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(Source())!,".."));
const string root="temp_docs/independent-bezier-restriction-cccaa5e2";
var restrict=typeof(BezierSurfaceBounds).GetMethod("RestrictCurve",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<Restriction>();
var blossom=typeof(BezierSurfaceBounds).GetMethod("RestrictCurveBlossom",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<Restriction>();
if(args.SequenceEqual(new[]{"--stack"}))
{
    Span<IntervalNumber> input=stackalloc IntervalNumber[68],output=stackalloc IntervalNumber[68];input.Fill(new(1));
    restrict(input,16,new(.2),new(.8),output);restrict(input,16,new(.1),new(.3),output);restrict(input,16,new(-.1),new(1.1),output);Console.WriteLine("PASS actual p16 two conditioned routes and general route for JIT stack inspection");return;
}
Require(!File.Exists(root+"/controls.json"),"preserve execution");
var result=new JsonObject{["scope"]="Ordinary managed actual private candidate; independent exact polar algebra, derivative/native PK controls and sealed products; no timing qualification",["nativeMvid"]=typeof(KernelRuntime).Assembly.ManifestModule.ModuleVersionId.ToString(),["nativeSha256"]=Hash(typeof(KernelRuntime).Assembly.Location),["exactPolar"]=ExactControls(restrict,blossom),["nativeJet"]=JetControls()};
Console.WriteLine("PASS exact polar coefficients, alias/readset/zero allocation and exact native rectangular jets");
if(!ParasolidScriptHost.TryStartSession("Independent homogeneous restriction",out var host,out var message))throw new Exception(message);
using var session=host;
unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"version");result["realPkVersion"]=$"{version.major_revision}.{version.minor_revision}.{version.build_number}";
    result["pkMultipatch"]=PkControls();var pairs=new JsonArray();ParasolidBodyCompare.Observer=row=>pairs.Add((JsonNode)row);
    foreach(var (directory,step) in new[]{("quality/00-curved-candidate",0),("dev-candidate/00-solidblend-candidate",5),("active-shortedge-lifecycle-capture",4)})
    {
        var folder=root+"/quality/original/temp_docs/bezier-restriction/"+directory;var gm=Product(folder,$"gm-{step}-after-0.json");var pk=Product(folder,$"pk-{step}-after-0.json");var actual=Receive(gm);var reference=Receive(pk);
        try{Full(actual);Full(reference);ParasolidBodyCompare.AssertEquivalent(reference,actual,directory);pairs[^1]!["gmXtSha256"]=Hash(gm);pairs[^1]!["pkXtSha256"]=Hash(pk);pairs[^1]!["originalStep"]=step;}
        finally{Check(PK_ENTITY_delete(1,&actual),"delete actual");Check(PK_ENTITY_delete(1,&reference),"delete reference");}
    }
    result["sealedProducts"]=pairs;var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"GM start");try{result["concurrentCache"]=ConcurrentControl();}finally{KernelRuntime.SessionStop();}
}
File.WriteAllText(root+"/controls.json",result.ToJsonString(new(){WriteIndented=true}));Console.WriteLine("PASS real PK native multipatch, three sealed full BODY_check/strict comparisons and observed concurrent cache generations");

static JsonObject ExactControls(Restriction restrict,Restriction blossom)
{
    var checks=0;var aliases=0;var fallbacks=0;
    (IntervalNumber a,IntervalNumber b)[] domains=[(0,1),(.125,.375),(.625,.875),(.5,.5),(0,0),(1,1),(new(.5,Math.BitIncrement(.5)),new(.5,Math.BitIncrement(.5))),(new(.15,.21),new(.18,.24)),(new(.4,.6),new(.4,.6)),(.8,.2),(new(0,double.Epsilon),new(0,double.Epsilon))];
    foreach(var p in new[]{0,1,2,3,8,16})foreach(var exponent in new[]{-1070,-500,0,500})
    {
        var input=new IntervalNumber[4*(p+1)];var output=new IntervalNumber[input.Length];var old=new IntervalNumber[input.Length];
        for(var i=0;i<input.Length;i++){var x=Math.ScaleB((i%7)-3,exponent);var y=Math.ScaleB((i%7)-2,exponent);input[i]=new(x,y);}
        foreach(var (a,b) in domains)
        {
            restrict(input,p,a,b,output);
            foreach(var av in new[]{R.From(a.Lower),(R.From(a.Lower)+R.From(a.Upper))/2,R.From(a.Upper)})foreach(var bv in new[]{R.From(b.Lower),(R.From(b.Lower)+R.From(b.Upper))/2,R.From(b.Upper)})
            for(var k=0;k<=p;k++)
            {
                // Coefficients of product (1-a+a*z)^(p-k)(1-b+b*z)^k.
                // This is the polar basis, independent of either split net.
                var weights=PolarWeights(p,k,av,bv);
                for(var d=0;d<4;d++)for(var end=0;end<2;end++)
                {R value=0;for(var j=0;j<=p;j++)value+=weights[j]*R.From(((j+end)%2==0?input[4*j+d].Lower:input[4*j+d].Upper));Contains(output[4*k+d],value);checks++;}
            }
            if(p<2||b.Lower<.5&&(1-a).Lower<.5){blossom(input,p,a,b,old);Bits(output,old);fallbacks++;}
            if(p>=2&&(b.Lower>=.5||(1-a).Lower>=.5))foreach(var displacement in new[]{-3,0,3})
            {
                var n=input.Length;var storage=Enumerable.Repeat(new IntervalNumber(1234),n+8).ToArray();var read=displacement<0?4:1;var write=read+displacement;input.CopyTo(storage,read);var before=storage.ToArray();restrict(storage.AsSpan(read,n),p,a,b,storage.AsSpan(write,n));Bits(output,storage.AsSpan(write,n));for(var i=0;i<storage.Length;i++)if(i<write||i>=write+n)Require(storage[i].Lower==before[i].Lower&&storage[i].Upper==before[i].Upper,"outside active output unchanged");aliases++;
            }
        }
        foreach(var (a,b) in new[]{(-.25,.4),(.9,1.1),(-1.0,2.0)}){restrict(input,p,new(a),new(b),output);blossom(input,p,new(a),new(b),old);Bits(output,old);fallbacks++;}
    }
    var hot=new IntervalNumber[68];var destination=new IntervalNumber[68];for(var i=0;i<68;i++)hot[i]=new(i/128.0,(i+1)/128.0);for(var i=0;i<50;i++)restrict(hot,16,new(.2),new(.8),destination);var start=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<1000;i++)restrict(hot,16,new(.2),new(.8),destination);var bytes=GC.GetAllocatedBytesForCurrentThread()-start;Require(bytes==0,"hot actual restriction managed allocation");
    return new JsonObject{["exactRationalCoefficientChecks"]=checks,["partialAndFullAliasCases"]=aliases,["bitwiseGeneralCases"]=fallbacks,["degrees"]=new JsonArray(0,1,2,3,8,16),["scaleExponents"]=new JsonArray(-1070,-500,0,500),["hotCalls"]=1000,["hotManagedBytes"]=bytes,["scope"]="Finite exact polar controls; continuous proof is algebra and outward induction, not finite sampling"};
}
static R[] PolarWeights(SplineDegree p,BufferOffset k,R a,R b)
{
    var values=new R[p+1];values[0]=1;for(var r=0;r<p;r++){var t=r<p-k?a:b;var next=new R[p+1];for(var i=0;i<=r;i++){next[i]+=values[i]*(1-t);next[i+1]+=values[i]*t;}values=next;}return values;
}
static JsonObject JetControls()
{
    var checks=0;var cases=0;const double u0=2,u1=6,v0=-3,v1=5;
    foreach(var rational in new[]{false,true})
    {
        var poles=new IntervalNumber[36];var exact=new R[36];for(var i=0;i<3;i++)for(var j=0;j<3;j++){R w=rational?new R(8+i+2*j,8):1;R[] xyz=[new R(i,2),new R(j,2),new R(i*i-j*j+i*j,16)];for(var d=0;d<4;d++){exact[4*(3*i+j)+d]=d==3?w:w*xyz[d];poles[4*(3*i+j)+d]=new(exact[4*(3*i+j)+d].ToDouble());}}
        var patch=new BezierSurfacePatch(poles,2,2,new(u0,u1,v0,v1));var jet=new IntervalNumber[27];var second=new IntervalNumber[18];var first=new IntervalNumber[9];var position=new IntervalNumber[3];
        foreach(var box in new[]{new SurfaceParameterBox(2.25,3.25,-2.5,-.5),new SurfaceParameterBox(4.5,5.5,2,4),new SurfaceParameterBox(4,4,1,1),new SurfaceParameterBox(4,Math.BitIncrement(4),1,Math.BitIncrement(1)),new SurfaceParameterBox(2,6,-3,5)})
        {
            Require(BezierSurfaceBounds.BoundRectangularSecond(patch,box,jet)==AlgorithmStatus.Success&&BezierSurfaceBounds.BoundSecond(patch,box,second)==AlgorithmStatus.Success&&BezierSurfaceBounds.Bound(patch,box,first)==AlgorithmStatus.Success&&BezierSurfaceBounds.BoundPosition(patch,box,position)==AlgorithmStatus.Success,"complete bound success");
            foreach(var u in new[]{R.From(box.U0),(R.From(box.U0)+R.From(box.U1))/2,R.From(box.U1)})foreach(var v in new[]{R.From(box.V0),(R.From(box.V0)+R.From(box.V1))/2,R.From(box.V1)})
            {
                var h=new R[36];var xyz=new R[27];for(var i=0;i<3;i++)for(var j=0;j<3;j++)for(var d=0;d<4;d++)for(var a=0;a<3;a++)for(var b=0;b<3;b++)h[4*(3*i+j)+d]+=exact[4*(3*a+b)+d]*Basis(a,i,(u-2)/4)*Basis(b,j,(v+3)/8)/R.Pow(4,i)/R.Pow(8,j);
                for(var i=0;i<3;i++)for(var j=0;j<3;j++)for(var d=0;d<3;d++)
                {var value=h[4*(3*i+j)+d];for(var a=0;a<=i;a++)for(var b=0;b<=j;b++){if(a==i&&b==j)continue;value-=Choose(i,a)*Choose(j,b)*xyz[3*(3*a+b)+d]*h[4*(3*(i-a)+(j-b))+3];}xyz[3*(3*i+j)+d]=value/h[3];Contains(jet[3*(3*i+j)+d],xyz[3*(3*i+j)+d]);checks++;}
                BufferOffset[] at=[0,9,3,18,12,6];for(var k=0;k<6;k++)for(var d=0;d<3;d++)Contains(second[3*k+d],xyz[at[k]+d]);for(var k=0;k<3;k++)for(var d=0;d<3;d++)Contains(first[3*k+d],xyz[at[k]+d]);for(var d=0;d<3;d++)Contains(position[d],xyz[d]);
            }cases++;
        }
        var invalid=(IntervalNumber[])poles.Clone();for(var i=3;i<36;i+=4)invalid[i]=0;Require(BezierSurfaceBounds.BoundPosition(new(invalid,2,2,patch.Domain),patch.Domain,position)==AlgorithmStatus.NumericalFailure,"nonpositive weight refused");invalid=(IntervalNumber[])poles.Clone();for(var i=0;i<36;i+=4)invalid[i]=new(double.PositiveInfinity);Require(BezierSurfaceBounds.BoundPosition(new(invalid,2,2,patch.Domain),patch.Domain,position)==AlgorithmStatus.NumericalFailure,"nonfinite position refused");
    }
    double[] controls=new double[27],z=[.25,-.25,.25],knots=[0,0,0,1,1,1];for(var u=0;u<3;u++)for(var v=0;v<3;v++){var at=3*(3*u+v);controls[at]=u*.5;controls[at+1]=v*.5;controls[at+2]=z[u]+z[v]-.0625;}var source=new BSurfaceView(2,2,3,3,3,false,false,false,controls,knots,knots);var target=new KernelVector3{X=.5,Y=.5,Z=-1};var best=new ProjectionPoint{DistanceUpper=double.PositiveInfinity};Require(KernelRuntime.ClosestBSurface(source,target,1e-9,ref best)==AlgorithmStatus.Success&&best.DistanceUpper>=.9375-1e-12&&best.DistanceUpper<=.9375+1e-9&&Math.Abs(best.U-.5)<1e-7&&Math.Abs(best.V-.5)<1e-7,"original global projection gap and native UV");
    return new JsonObject{["nativeDomain"]="[2,6] x [-3,5]",["boxes"]=cases,["exactRectangularJetChecks"]=checks,["fullLayouts"]="3/9/18/27; original derivative polynomial, zero/narrow/full boxes",["positiveWeightAndFiniteFailures"]=true,["globalClosestOriginalGap"]=true};
}
// Explicit quadratic Bernstein power polynomials and their native derivatives.
static R Basis(BufferOffset i,BufferOffset d,R t)=>d switch{0=>i==0?(1-t)*(1-t):i==1?2*t*(1-t):t*t,1=>i==0?2*(t-1):i==1?2-4*t:2*t,_=>i==1?-4:2};
static BufferCount Choose(BufferCount n,BufferCount k)=>n==2&&k==1?2:1;
static unsafe JsonArray PkControls()
{
    var rows=new JsonArray();foreach(var rational in new[]{false,true})
    {
        var dim=rational?4:3;var poles=new double[12*dim];for(var i=0;i<4;i++)for(var j=0;j<3;j++){var w=rational?1+i/8.0+j/4.0:1;var at=dim*(3*i+j);poles[at]=i*.5*w;poles[at+1]=j*.5*w;poles[at+2]=(i*i-j*j+i*j)/16.0*w;if(rational)poles[at+3]=w;}
        double[] uk=[2,2,2,3.25,5,5,5],vk=[-3,-3,-3,1,1,1];var source=new BSurfaceView(2,2,4,3,dim,rational,false,false,poles,uk,vk);double* u=stackalloc double[3]{2,3.25,5};double* v=stackalloc double[2]{-3,1};BufferCount* um=stackalloc BufferCount[3]{3,1,3};BufferCount* vm=stackalloc BufferCount[2]{3,3};GeomTag surf;
        fixed(double* p=poles){var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=4,n_v_vertices=3,vertex_dim=dim,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,vertex=p,form=PK_BSURF_form_unset_c,n_u_knots=3,n_v_knots=2,u_knot=u,v_knot=v,u_knot_mult=um,v_knot_mult=vm,u_knot_type=PK_knot_unset_c,v_knot_type=PK_knot_unset_c,self_intersecting=PK_self_intersect_unset_c,convexity=PK_convexity_unset_c};Check(PK_BSURF_create(&sf,&surf),"PK multipatch native spline");}
        try
        {
            var max=0.0;var count=0;PK_VECTOR_t* actual=stackalloc PK_VECTOR_t[9];Span<KernelVector3> expected=stackalloc KernelVector3[9];SurfaceDerivativeLayout.TryCreate(2,2,out var layout);Span<double> workspace=stackalloc double[2048];Span<IntervalNumber> net=stackalloc IntervalNumber[36],hull=stackalloc IntervalNumber[27];
            foreach(var (x,y) in new[]{(2.0,-3.0),(2.3,-1.2),(3.1,.8),(3.5,-.7),(4.9,.9),(5.0,1.0)})
            {
                var uv=new PK_UV_t();uv.param[0]=x;uv.param[1]=y;Check(PK_SURF_eval(surf,uv,2,2,PK_LOGICAL_false,actual),"PK native derivative");Require(BSurfaceEvaluation.Evaluate(source,x,y,layout,workspace,expected)==AlgorithmStatus.Success,"GM multipatch eval");var span=x<3.25?2:3;Require(BezierSurfaceBounds.Extract(source,span,2,net)==AlgorithmStatus.Success,"native span extraction");var domain=new SurfaceParameterBox(uk[span],uk[span+1],-3,1);Require(BezierSurfaceBounds.BoundRectangularSecond(new(net,2,2,domain),new(x,x,y,y),hull)==AlgorithmStatus.Success,"native point rectangular hull");
                for(var i=0;i<9;i++)for(var d=0;d<3;d++){var value=expected[layout.GetIndex(i%3,i/3)];var e=d==0?value.X:d==1?value.Y:value.Z;var a=actual[i].coord[d];max=Math.Max(max,Math.Abs(e-a));Require(Math.Abs(e-a)<1e-10,"PK native jet agreement");var enclosure=hull[3*(3*(i%3)+i/3)+d];Require(enclosure.Lower-1e-11<=a&&a<=enclosure.Upper+1e-11,"PK point hull rounding control");}count++;
            }
            rows.Add((JsonNode)new JsonObject{["rational"]=rational,["nativeUvStations"]=count,["derivativesPerStation"]=9,["maxAbsoluteResidual"]=max,["scope"]="Real PK point controls on two native spans; slack only on PK point comparison, no production tolerance change"});
        }finally{Check(PK_ENTITY_delete(1,&surf),"delete spline");}
    }return rows;
}
static JsonObject ConcurrentControl()
{
    GeometryEvaluationCache.Clear();var identity=new GeometryIdentity(4242,1);long generation=0,hits=0,positiveHits=0,overlapHits=0,violations=0,epochs=0;var activeWriters=0;using var barrier=new Barrier(6);var tasks=new List<Task>();
    for(var i=0;i<64;i++)Publish(i*.25,0);
    // Dedicated threads and common start retain the corrected fixture shape.
    // Additional observations belong only to this independent execution.
    for(var w=0;w<3;w++)tasks.Add(Task.Factory.StartNew(()=>{Interlocked.Increment(ref activeWriters);barrier.SignalAndWait();try{for(var i=0;i<6000;i++){var g=Interlocked.Increment(ref generation);if(g%200==0){GeometryEvaluationCache.BumpModelGeometryEpoch();Interlocked.Increment(ref epochs);}Publish((i%64)*.25,g);}}finally{Interlocked.Decrement(ref activeWriters);}},CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default));
    for(var r=0;r<3;r++)tasks.Add(Task.Factory.StartNew(()=>{barrier.SignalAndWait();for(var i=0;i<6000;i++){var p=(i%64)*.25;var before=Volatile.Read(ref activeWriters);if(!GeometryEvaluationCache.TryGetExact(identity,p,ICurveQueryKind.RegularChartInterval,ChartSide.Right,0,0,out var sample))continue;Interlocked.Increment(ref hits);if(sample.Position.Z>0)Interlocked.Increment(ref positiveHits);if(before>0&&Volatile.Read(ref activeWriters)>0&&sample.Position.Z>0)Interlocked.Increment(ref overlapHits);if(sample.Parameter!=p||sample.Position.X!=p||sample.Position.Y!=2*p||sample.First.X!=3*p||sample.Position.Z!=sample.First.Z)Interlocked.Increment(ref violations);}},CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default));
    Task.WaitAll(tasks.ToArray());Require(generation==18000&&epochs==90&&positiveHits>0&&overlapHits>0&&violations==0,"observed live writer phase, post-prefill generation, original payload invariants");return new JsonObject{["writes"]=generation,["epochBumps"]=epochs,["hits"]=hits,["positiveGenerationHits"]=positiveHits,["hitsWithinActiveWriterLifetimes"]=overlapHits,["violations"]=violations,["scope"]="Observed one independent execution, not a retrospective per-phase claim about original full suite or proof of simultaneous instruction execution"};
    void Publish(double p,double g)=>GeometryEvaluationCache.Publish(identity,new CurveSample(p,new(){X=p,Y=2*p,Z=g},new(){X=3*p,Z=g},default,0,ICurveQueryKind.RegularChartInterval,ChartSide.Right,0,0,SampleSourceKind.CorrectedRoot,ICurveConstraintPlan.I3));
}
static string Source([CallerFilePath]string path="")=>path;
static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static void Contains(IntervalNumber box,R value){Require(!double.IsNaN(box.Lower)&&!double.IsNaN(box.Upper)&&box.Lower<=box.Upper&&(double.IsNegativeInfinity(box.Lower)||R.From(box.Lower)<=value)&&(double.IsPositiveInfinity(box.Upper)||value<=R.From(box.Upper)),"exact rational outside actual interval");}
static void Bits(ReadOnlySpan<IntervalNumber> a,ReadOnlySpan<IntervalNumber> b){for(var i=0;i<a.Length;i++)Require(BitConverter.DoubleToInt64Bits(a[i].Lower)==BitConverter.DoubleToInt64Bits(b[i].Lower)&&BitConverter.DoubleToInt64Bits(a[i].Upper)==BitConverter.DoubleToInt64Bits(b[i].Upper),"bitwise interval");}
static void Check(ApiErrorCode error,string label)=>Require(error==0,label+" error="+error);
static string Product(string folder,string name){var source=JsonNode.Parse(File.ReadAllText(folder+"/meshes/"+name))!["source"]!;var path=folder+"/"+(string)source["xtArtifact"]!;Require(Hash(path)==(string?)source["xtSha256"],"sealed actual product binding");return path;}
static unsafe GeomTag Receive(string path){var bytes=File.ReadAllBytes(path);fixed(byte* pointer=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,pointer);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};BufferCount count;GeomTag* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"receive");try{Require(count==1,"one body");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}}}
static unsafe void Full(GeomTag body){var options=new PK_BODY_check_o_t{max_faults=100};BufferCount count;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&count,&faults),"full check");try{Require(count==0,"full faults="+count);}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}}
delegate void Restriction(ReadOnlySpan<IntervalNumber> input,SplineDegree degree,IntervalNumber a,IntervalNumber b,Span<IntervalNumber> output);
readonly struct R
{
    readonly BigInteger numerator,denominator;
    BigInteger Den=>denominator.IsZero?BigInteger.One:denominator;
    public R(BigInteger n,BigInteger d){if(d.IsZero)throw new DivideByZeroException();if(d.Sign<0){n=-n;d=-d;}var g=BigInteger.GreatestCommonDivisor(n,d);numerator=n/g;denominator=d/g;}
    public static implicit operator R(int n)=>new(n,1);
    public static R From(double x){if(!double.IsFinite(x))throw new ArgumentException("finite rational source");var bits=BitConverter.DoubleToInt64Bits(x);var exp=(int)((bits>>52)&2047);BigInteger mantissa=bits&0xfffffffffffffL;if(exp!=0)mantissa+=BigInteger.One<<52;if(bits<0)mantissa=-mantissa;var power=exp==0?-1074:exp-1075;return power<0?new(mantissa,BigInteger.One<<-power):new(mantissa<<power,1);}
    public double ToDouble()=>(double)numerator/(double)Den;
    public static R Pow(R x,BufferCount n){R value=1;for(var i=0;i<n;i++)value*=x;return value;}
    public static R operator +(R a,R b)=>new(a.numerator*b.Den+b.numerator*a.Den,a.Den*b.Den);
    public static R operator -(R a,R b)=>new(a.numerator*b.Den-b.numerator*a.Den,a.Den*b.Den);
    public static R operator *(R a,R b)=>new(a.numerator*b.numerator,a.Den*b.Den);
    public static R operator /(R a,R b)=>new(a.numerator*b.Den,a.Den*b.numerator);
    public static bool operator <=(R a,R b)=>a.numerator*b.Den<=b.numerator*a.Den;
    public static bool operator >=(R a,R b)=>a.numerator*b.Den>=b.numerator*a.Den;
}
