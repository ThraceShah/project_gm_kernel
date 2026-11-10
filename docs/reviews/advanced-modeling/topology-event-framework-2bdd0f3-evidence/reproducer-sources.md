# Review-only reproducer sources — 2bdd0f3

Save these sources at the indicated project-root-relative locations; all project/file references resolve from the script location. The native control uses only PskernelSharp and the existing ParasolidScriptHost; no GM project is referenced.

## temp_docs/opus-r2-r4-review/Directory.Build.props

```xml
<Project>
  <Import Project="../../scripts/Directory.Build.props" />
</Project>
```

## temp_docs/opus-r2-r4-review/NativeRoundtripControl.cs

```csharp
#!/usr/bin/env dotnet run
#:project ../../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property AllowUnsafeBlocks=true
using static parasolid;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
var here=Path.GetDirectoryName(ScriptPath())!;
var csv=Path.GetFullPath(Path.Combine(here,"..","..","docs","reviews","advanced-modeling","short-edge-spine-probes.csv"));
if(!ParasolidScriptHost.TryStartSession("pure native roundtrip control",out var session,out var skip))throw new Exception(skip);
using(session)unsafe
{
    PK_SESSION_kernel_version_t version;double precision;
    Check(PK_SESSION_ask_kernel_version(&version),"version");Check(PK_SESSION_ask_precision(&precision),"precision");
    Console.WriteLine($"PK {version.major_revision}.{version.minor_revision}.{version.build_number} precision={precision:R} CSV_SHA256={Hash(File.ReadAllBytes(csv))}");
    var native=Seed(1,0,false,0);CheckBody(native,"native seed");
    File.WriteAllBytes(Path.Combine(here,"native-seed.x_t"),Transmit(native));
    FilletNative(native);CheckBody(native,"native product");
    var bytes=Transmit(native);File.WriteAllBytes(Path.Combine(here,"native-product.x_t"),bytes);
    var received=Receive(bytes);CheckBody(received,"own native XT receive");
    ParasolidBodyCompare.AssertEquivalent(native,received,"pure native roundtrip");
    Console.WriteLine($"NATIVE_PRODUCT_SHA256={Hash(bytes)} transmit_format=text transmit_version=371 receive_format=text");
    var rows=File.ReadAllLines(csv);var differences=0;var changed=0;var apiErrors=0;
    using(var writer=new StreamWriter(Path.Combine(here,"native-control-all510.csv")))
    {
        writer.WriteLine("row,x,y,z,csv_native,csv_roundtrip,csv_self_import,csv_self_fresh,native,roundtrip,native_error,roundtrip_error,x_bits,y_bits,z_bits");
        for(var i=1;i<rows.Length;i++)
        {
            var f=rows[i].Split(',');Require(f.Length==12,"CSV columns");
            var p=new PK_VECTOR_t(double.Parse(f[5],CultureInfo.InvariantCulture),double.Parse(f[6],CultureInfo.InvariantCulture),double.Parse(f[7],CultureInfo.InvariantCulture));
            int a,b,ta,tb;var ea=PK_BODY_contains_vector(native,p,&a,&ta);var eb=PK_BODY_contains_vector(received,p,&b,&tb);
            if(ea!=0||eb!=0)apiErrors++;
            if(a!=int.Parse(f[8],CultureInfo.InvariantCulture))changed++;
            if(a!=b){differences++;Console.WriteLine($"DIFF row={i} p=({f[5]},{f[6]},{f[7]}) csv={f[8]}/{f[9]}/{f[10]}/{f[11]} native/roundtrip={a}/{b} error={ea}/{eb}");}
            writer.WriteLine($"{i},{f[5]},{f[6]},{f[7]},{f[8]},{f[9]},{f[10]},{f[11]},{a},{b},{ea},{eb},{BitConverter.DoubleToInt64Bits(p.coord[0]):X16},{BitConverter.DoubleToInt64Bits(p.coord[1]):X16},{BitConverter.DoubleToInt64Bits(p.coord[2]):X16}");
        }
    }
    Console.WriteLine($"CONTROL rows={rows.Length-1} differences={differences} native_baseline_changed={changed} api_errors={apiErrors}");
    // Perturbed coordinates are a separate diagnostic, after the exact replay.
    using(var writer=new StreamWriter(Path.Combine(here,"ulp-representatives.csv")))
    {
        writer.WriteLine("row,axis,ulps,x,y,z,native,roundtrip");
        foreach(var row in new[]{102,339})
        {
            var f=rows[row].Split(',');var x=new[]{double.Parse(f[5],CultureInfo.InvariantCulture),double.Parse(f[6],CultureInfo.InvariantCulture),double.Parse(f[7],CultureInfo.InvariantCulture)};
            for(var axis=0;axis<3;axis++)for(var k=-4;k<=4;k++)
            {
                var y=(double[])x.Clone();for(var s=0;s<Math.Abs(k);s++)y[axis]=k<0?Math.BitDecrement(y[axis]):Math.BitIncrement(y[axis]);
                var p=new PK_VECTOR_t(y[0],y[1],y[2]);int a,b,t;Check(PK_BODY_contains_vector(native,p,&a,&t),"perturbed native");Check(PK_BODY_contains_vector(received,p,&b,&t),"perturbed receive");
                writer.WriteLine(FormattableString.Invariant($"{row},{axis},{k},{y[0]:R},{y[1]:R},{y[2]:R},{a},{b}"));
            }
        }
    }
    foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())Require(!assembly.GetName().Name!.StartsWith("ProjectGmKernel",StringComparison.Ordinal),"GM assembly unexpectedly loaded");
    Console.WriteLine("GM_ASSEMBLIES=0; all construction/transmit/receive/classification calls use genuine pskernel");
    Check(PK_ENTITY_delete(1,&received),"delete roundtrip");Check(PK_ENTITY_delete(1,&native),"delete native");
    Require(rows.Length==511&&changed==0&&apiErrors==0,"control integrity");
    if(args.Contains("--strict-equality"))return differences==0?0:1;
}
return 0;

static unsafe void FilletNative(int body)
{
    int n;int* edges;Check(PK_BODY_ask_edges(body,&n,&edges),"edges");
    var selected=new int[2];var ends=stackalloc PK_VECTOR_t[2];var t=Math.Tan(Math.PI/180);
    var j=new PK_VECTOR_t(-1.5,-2.5,5);var k=new PK_VECTOR_t(-2.5,-1.5,4);var i=new PK_VECTOR_t(2.5,-2.5+4*t,5-4*t);
    try
    {
        for(var e=0;e<n;e++)
        {
            int curve,type;PK_INTERVAL_t range;PK_LOGICAL_t sense;
            Check(PK_EDGE_ask_geometry(edges[e],PK_LOGICAL_true,&curve,&type,ends,&range,&sense),"edge geometry");
            for(var s=0;s<2;s++)if(type==(s==0?PK_CLASS_ellipse:PK_CLASS_line)&&Pair(ends[0],ends[1],j,s==0?k:i))
            {Require(selected[s]==0,"unique selection");selected[s]=edges[e];}
        }
    }
    finally{Check(PK_MEMORY_free(edges),"free edges");}
    Require(selected[0]!=0&&selected[1]!=0,"selected ellipse and line");
    var mark=new PK_EDGE_set_blend_constant_o_t();mark.properties.tolerance=1e-7;int count;int* marked;
    fixed(int* selection=selected)Check(PK_EDGE_set_blend_constant(2,selection,.5,&mark,&count,&marked),"set blend");
    Check(PK_MEMORY_free(marked),"free marked");
    var fix=new PK_BODY_fix_blends_o_t{local_check=PK_LOGICAL_true,tolerance=1e-7};
    int* made;int fault,edgeFault,topolFault;
    var error=PK_BODY_fix_blends(body,&fix,&count,&made,null,null,&fault,&edgeFault,&topolFault);
    Console.WriteLine($"NATIVE_FIX error={error} faces={count} fault={fault} tolerance=1e-7 radius=.5 default_update");
    Check(error,"native fix");if(made!=null)Check(PK_MEMORY_free(made),"free made");
}
static unsafe byte[] Transmit(int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(PK_PART_transmit_b(1,&body,&options,&block),"transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(PK_MEMORY_block_f(&block),"free XT");}
}
static unsafe int Receive(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int n;int* parts;
        Check(PK_PART_receive_b(block,&options,&n,&parts),"receive");
        try{Require(n==1,"one part");return parts[0];}finally{Check(PK_MEMORY_free(parts),"free parts");}
    }
}
static unsafe void CheckBody(int body,string label)
{
    var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;
    Check(PK_BODY_check(body,&options,&n,&faults),label);try{Console.WriteLine($"{label}: full_check_faults={n}");Require(n==0,label+" faults");}finally{if(faults!=null)Check(PK_MEMORY_free(faults),"free faults");}
}
static unsafe PK_VECTOR_t Subtract(PK_VECTOR_t a,PK_VECTOR_t b)=>new(a.coord[0]-b.coord[0],a.coord[1]-b.coord[1],a.coord[2]-b.coord[2]);
static unsafe double Dot(PK_VECTOR_t a,PK_VECTOR_t b)=>a.coord[0]*b.coord[0]+a.coord[1]*b.coord[1]+a.coord[2]*b.coord[2];
static double Distance(PK_VECTOR_t a,PK_VECTOR_t b){var d=Subtract(a,b);return Math.Sqrt(Dot(d,d));}
static bool Pair(PK_VECTOR_t a,PK_VECTOR_t b,PK_VECTOR_t c,PK_VECTOR_t d)=>Distance(a,c)<1e-8&&Distance(b,d)<1e-8||Distance(a,d)<1e-8&&Distance(b,c)<1e-8;
static void Check(int error,string label){if(error!=0)throw new Exception(label+": error="+error);}
static void Require(bool value,string label){if(!value)throw new Exception(label);}
static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
static string ScriptPath([CallerFilePath]string path="")=>path;

// Seed copied verbatim from fixed candidate ShortEdgeBlendOracle.cs.
static unsafe int Seed(double degrees,double floor,bool splitRemote,double phase)
{
    var theta=degrees*Math.PI/180;var t=Math.Tan(theta);var f=-2.5+4*t;var h=5-4*t;
    PK_VECTOR_t[] p=[new(-2.5,2.5,floor),new(2.5,2.5,floor),new(2.5,f,floor),new(-1.5,-2.5,floor),new(-2.5,-1.5,floor),
        new(-2.5,2.5,4),new(-1.5,2.5,5),new(2.5,2.5,h),new(2.5,f,h),new(-1.5,-2.5,5),new(-2.5,-1.5,4)];
    int[][] loops=[[0,1,2,3,4],[0,5,6,7,1],[1,7,8,2],[2,8,9,3],[6,9,8,7],[0,4,10,5],[5,10,9,6],[4,3,9,10]];
    if(splitRemote)
    {
        // Subdivide the common bottom/back line in both directed loops.
        // The rolling neighbourhood and the two selection occurrences are
        // identical; the bodies supplied to the two kernels have 18 edges.
        p=[..p,new(0,2.5,floor)];
        loops[0]=[0,11,1,2,3,4];loops[1]=[0,5,6,7,1,11];
    }
    var classes=new List<int>();var parents=new List<int>();var children=new List<int>();var senses=new List<int>();
    int Add(int type){var i=classes.Count;classes.Add(type);return i;}
    void Rel(int a,int b,int s=PK_TOPOL_sense_none_c){parents.Add(a);children.Add(b);senses.Add(s);}
    var root=Add(PK_CLASS_body);var outside=Add(PK_CLASS_region);var inside=Add(PK_CLASS_region);var outerShell=Add(PK_CLASS_shell);var innerShell=Add(PK_CLASS_shell);
    Rel(root,outside,PK_TOPOL_sense_negative_c);Rel(root,inside,PK_TOPOL_sense_positive_c);Rel(outside,outerShell);Rel(inside,innerShell);
    var vertices=p.Select(_=>Add(PK_CLASS_vertex)).ToArray();var faces=new int[8];
    var edgeMap=new Dictionary<(int,int),int>();var edgeEnds=new Dictionary<int,(int,int)>();
    for(var i=0;i<loops.Length;i++)
    {
        faces[i]=Add(PK_CLASS_face);var loop=Add(PK_CLASS_loop);Rel(innerShell,faces[i],PK_TOPOL_sense_negative_c);Rel(outerShell,faces[i],PK_TOPOL_sense_positive_c);Rel(faces[i],loop);
        for(var j=0;j<loops[i].Length;j++)
        {
            var a=loops[i][j];var b=loops[i][(j+1)%loops[i].Length];var key=(Math.Min(a,b),Math.Max(a,b));
            if(!edgeMap.TryGetValue(key,out var edge))
            {
                edge=Add(PK_CLASS_edge);edgeMap.Add(key,edge);edgeEnds.Add(edge,(a,b));Rel(edge,vertices[a]);Rel(edge,vertices[b]);
            }
            var fin=Add(PK_CLASS_fin);var direction=edgeEnds[edge]==(a,b)?PK_TOPOL_sense_positive_c:PK_TOPOL_sense_negative_c;
            Rel(loop,fin);Rel(fin,edge,direction);Rel(edge,fin,direction);
        }
    }
    var options=new PK_BODY_create_topology_2_o_t{body_type=PK_BODY_type_solid_c};PK_BODY_create_topology_2_r_t creation;
    Check(PK_BODY_create_topology_2(classes.Count,classes.ToArray(),parents.Count,parents.ToArray(),children.ToArray(),senses.ToArray(),&options,&creation),"create finite topology");
    try
    {
        for(var fault=0;fault<creation.n_create_faults;fault++)Console.WriteLine($"CREATE STATUS {creation.create_faults[fault].state}: "+string.Join(",",new ReadOnlySpan<int>(creation.create_faults[fault].indices,creation.create_faults[fault].n_indices).ToArray()));
        for(var fault=0;fault<creation.n_create_faults;fault++)Require(creation.create_faults[fault].state==PK_BODY_state_ok_c,"topology creation faults");
        for(var i=0;i<p.Length;i++){var sf=new PK_POINT_sf_t(p[i]);int point;Check(PK_POINT_create(&sf,&point),"point");var vertex=creation.topols[vertices[i]];Check(PK_VERTEX_attach_points(1,&vertex,&point),"attach point");}
        foreach(var (index,ends) in edgeEnds)
        {
            var (a,b)=ends;int curve;
            var key=(Math.Min(a,b),Math.Max(a,b));
            if(key==(3,4)||key==(5,6))
            {
                var center=key==(3,4)?new PK_VECTOR_t(-1.5,-1.5,floor):new PK_VECTOR_t(-1.5,2.5,4);
                var axis=key==(3,4)?new PK_VECTOR1_t(0,0,1):new PK_VECTOR1_t(0,1,0);
                var sf=new PK_CIRCLE_sf_t(new PK_AXIS2_sf_t(center,axis,new PK_VECTOR1_t(-1,0,0)),1);
                Check(PK_CIRCLE_create(&sf,&curve),"circle");
            }
            else if(key==(9,10))
            {
                var q=1/Math.Sqrt(2);var sf=new PK_ELLIPSE_sf_t(new PK_AXIS2_sf_t(new PK_VECTOR_t(-1.5,-1.5,4),new PK_VECTOR1_t(0,-q,-q),new PK_VECTOR1_t(0,-q,q)),Math.Sqrt(2),1);
                Check(PK_ELLIPSE_create(&sf,&curve),"ellipse");
            }
            else
            {
                var d=Subtract(p[b],p[a]);var length=Math.Sqrt(Dot(d,d));
                var sf=new PK_LINE_sf_t(new PK_AXIS1_sf_t(p[a],new PK_VECTOR1_t(d.coord[0]/length,d.coord[1]/length,d.coord[2]/length)));
                Check(PK_LINE_create(&sf,&curve),"line");
            }
            var edge=creation.topols[index];
            var interval=new PK_INTERVAL_t(0,key is (3,4) or (5,6) or (9,10)?Math.PI/2:Distance(p[a],p[b]));
            var forward=key==(3,4)?a==4:key==(9,10)?a==9:true;var curveSense=forward?PK_LOGICAL_true:PK_LOGICAL_false;var intervalIndex=0;
            var attach=new PK_EDGE_attach_curves_o_t{n_intervals=1,intervals=&interval,interval_map=&intervalIndex,have_senses=PK_LOGICAL_true,senses=&curveSense};PK_ENTITY_track_r_t tracking;
            Check(PK_EDGE_attach_curves_2(1,&edge,&curve,&attach,&tracking),"attach curve");Check(PK_ENTITY_track_r_f(&tracking),"free attach tracking");
        }
        PK_VECTOR_t[] origins=[new(0,0,floor),new(0,2.5,0),new(2.5,0,0),new(-1.5,-2.5,0),new(-1.5,0,5),new(-2.5,0,0)];
        PK_VECTOR1_t[] normals=[new(0,0,-1),new(0,1,0),new(1,0,0),new(Math.Sin(theta),-Math.Cos(theta),0),new(Math.Sin(theta),0,Math.Cos(theta)),new(-1,0,0)];
        for(var i=0;i<8;i++)
        {
            int surface;
            if(i<6)
            {
                var normal=normals[i];var reference=Math.Abs(normal.coord[0])<.9?new PK_VECTOR1_t(1,0,0):new PK_VECTOR1_t(0,1,0);
                if(i==3||i==4)reference=new PK_VECTOR1_t(Math.Cos(theta),i==3?Math.Sin(theta):0,i==4?-Math.Sin(theta):0);
                var sf=new PK_PLANE_sf_t(new PK_AXIS2_sf_t(origins[i],normal,reference));Check(PK_PLANE_create(&sf,&surface),"plane");
            }
            else
            {
                var reference=i==6?new PK_VECTOR1_t(-Math.Cos(phase),0,Math.Sin(phase)):new PK_VECTOR1_t(-1,0,0);
                var sf=new PK_CYL_sf_t(new PK_AXIS2_sf_t(i==6?new PK_VECTOR_t(-1.5,0,4):new PK_VECTOR_t(-1.5,-1.5,0),i==6?new PK_VECTOR1_t(0,1,0):new PK_VECTOR1_t(0,0,1),reference),1);
                Check(PK_CYL_create(&sf,&surface),"cylinder");
            }
            var face=creation.topols[faces[i]];var sense=PK_LOGICAL_true;Check(PK_FACE_attach_surfs(1,&face,&surface,&sense),"attach surface");
        }
        return creation.body;
    }
    finally{Check(PK_BODY_create_topology_2_r_f(&creation),"free topology result");}
}

```

## temp_docs/opus-r2-r4-review/ScalarBoundsProbe.cs

```csharp
#!/usr/bin/env dotnet run
#:project ../../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property AssemblyName=KernelTests
using System.Numerics;
using ProjectGmKernel.Native.Computation;
using ProjectGmKernel.Native.Computation.Numerics;
using ProjectGmKernel.Native.Geometry.Evaluation;
using ProjectGmKernel.Native.Runtime;

foreach(var lower in new[]{1e12,-1e12,1e-300,.5,double.Epsilon})
{
    var upper=lower;for(var j=0;j<3;j++)upper=Math.BitIncrement(upper);
    var midpoint=lower+(upper-lower)*.5;
    var radius=JointNestedBlendEvaluation.MeanValueRadius(lower,midpoint,upper);
    Require(Rat.Of(radius)>=Rat.Of(midpoint)-Rat.Of(lower)&&Rat.Of(radius)>=Rat.Of(upper)-Rat.Of(midpoint),"radius exact rational enclosure");
    Console.WriteLine($"RADIUS lo={lower:R} hi={upper:R} mid={midpoint:R} corrected={radius:R}");
}
Span<IntervalNumber> centre=stackalloc IntervalNumber[9],box=stackalloc IntervalNumber[9];
var lo=1e12;var hi=Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(lo)));var mid=lo+(hi-lo)*.5;
centre[0]=mid;centre[1]=hi-mid;box[3]=1;box[4]=-1;
var cell=new BlendChartInstruction{Chord=new(){X=1}};
var bound=JointNestedBlendEvaluation.MeasureChordBox(centre,box,1,cell,JointNestedBlendEvaluation.MeanValueRadius(lo,mid,hi));
Require(bound.Distance>=hi-lo,"linear mean-value maximum");
Console.WriteLine($"LINEAR distance={bound.Distance:R} true_max={hi-lo:R}");
foreach(var angle in new[]{0.0,1e-6,.0087,.3,Math.BitDecrement(.3),Math.BitIncrement(.3),.3368,.4,1.0,2.0,Math.PI,-.4})
{
    var x=Rat.Of(angle);var x2=x*x;var p=Rat.Of(1)-x2/Rat.Of(2)+x2*x2/Rat.Of(24)-x2*x2*x2/Rat.Of(720)+x2*x2*x2*x2/Rat.Of(40320);
    var upper=JointNestedBlendEvaluation.CosineUpper(angle);
    // cap at 1 is justified independently by cos<=1. Otherwise verify exact
    // binary-input polynomial arithmetic, rather than compare with libm cos.
    Require(upper==1||Rat.Of(upper)>=p,"exact Taylor upper");
    Console.WriteLine($"TAYLOR x={angle:R} upper={upper:R} exact_polynomial_enclosed=True");
}
centre.Clear();box.Clear();box[3]=3;box[4]=4;
bound=JointNestedBlendEvaluation.MeasureChordBox(centre,box,3,cell,0);
Require(Rat.Of(bound.CosineLower)<=new Rat(3,5),"rational cosine lower");
Console.WriteLine($"COSINE lower={bound.CosineLower:R} exact=3/5");
var zero=new BlendChartInstruction();
Require(double.IsPositiveInfinity(JointNestedBlendEvaluation.MeasureChordBox(centre,box,3,zero,0).Distance),"zero chord rejects");
centre[0]=new(double.NaN);Require(double.IsPositiveInfinity(JointNestedBlendEvaluation.MeasureChordBox(centre,box,3,cell,0).Distance),"NaN centre rejects");
Console.WriteLine("INVALID zero_chord/NaN reject with infinite distance");
// Diagnostic input-contract observation: production creates positive angular
// limits; compare the internal overload on a deliberately negative budget.
BlendSurfaceInstruction[] supports=[new(){Analytic=new(SurfaceClass.Plane,default,new(){Z=1},new(){X=1}),NormalSense=1},new(){Analytic=new(SurfaceClass.Cylinder,default,new(){Z=1},new(){X=1},1),NormalSense=1}];
var original=Arc(0,.2,4,1);var upperParameter=original[^1].Parameter;var compact=Arc(0,.2,1,upperParameter/(2*Math.Sin(.1)));compact[^1].Parameter=upperParameter;
BlendSpineInstruction[] spines=[new(){First=0,Second=1,ChartCount=original.Length},new(){First=0,Second=1,ChartOffset=original.Length,ChartCount=2}];
var program=new NestedBlendProgram(supports,spines,original.Concat(compact).ToArray());
foreach(var angular in new[]{.3,-.3,double.NaN})
{
    var status=JointNestedBlendEvaluation.CertifyCompactBranch(program,0,1,[0.0,upperParameter],0,upperParameter,1e-10,4,angular,out var report);
    Console.WriteLine($"ANGULAR_INPUT limit={angular:R} status={status} bound={report.AngularBound:R}");
}
static BlendChartInstruction[] Arc(double start,double end,int cells,double scale)
{
    var p=new KernelVector3[cells+1];var t=new KernelVector3[cells+1];
    for(var i=0;i<=cells;i++){var a=start+(end-start)*i/cells;p[i]=new(){X=Math.Cos(a),Y=Math.Sin(a)};t[i]=new(){X=-Math.Sin(a),Y=Math.Cos(a)};}
    var parameters=new double[cells+1];var scales=new double[cells];var chords=new KernelVector3[cells];
    Require(OriginalChartParameterMap.Build(p,t,0,scale,parameters,scales,chords,out _,out _)==AlgorithmStatus.Success,"arc");
    return Enumerable.Range(0,cells+1).Select(i=>new BlendChartInstruction{Position=p[i],Parameter=parameters[i],Scale=i<cells?scales[i]:1,Chord=i<cells?chords[i]:default}).ToArray();
}
static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
readonly record struct Rat(BigInteger N,BigInteger D)
{
    public static Rat Of(double value)
    {
        var bits=BitConverter.DoubleToUInt64Bits(value);var exponent=(int)((bits>>52)&2047);var significand=bits&0xFFFFFFFFFFFFFUL;
        if(exponent==2047)throw new Exception("nonfinite rational");
        if(exponent!=0)significand|=1UL<<52;
        var power=exponent==0?-1074:exponent-1075;BigInteger numerator=significand;
        if((bits>>63)!=0)numerator=-numerator;
        return power>=0?new(numerator<<power,BigInteger.One):new(numerator,BigInteger.One<<-power);
    }
    public static Rat operator +(Rat a,Rat b)=>new(a.N*b.D+b.N*a.D,a.D*b.D);
    public static Rat operator -(Rat a,Rat b)=>new(a.N*b.D-b.N*a.D,a.D*b.D);
    public static Rat operator *(Rat a,Rat b)=>new(a.N*b.N,a.D*b.D);
    public static Rat operator /(Rat a,Rat b)=>new(a.N*b.D,a.D*b.N);
    public static bool operator >=(Rat a,Rat b)=>a.N*b.D>=b.N*a.D;
    public static bool operator <=(Rat a,Rat b)=>a.N*b.D<=b.N*a.D;
}
```
